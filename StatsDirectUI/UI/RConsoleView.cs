#nullable enable
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using StatsDirect.Configuration;
using StatsDirect.R;
using StatsDirect.R.Interactive;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace StatsDirect.UI;

/// <summary>The R document's browser and persistent, separately hosted R workspace.</summary>
internal sealed partial class RConsoleView : UserControl
{
    private const string Origin = "https://rconsole.statsdirect.invalid";
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal WebView2 Browser { get; private set; } = new() { Dock = DockStyle.Fill };
    private TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly SemaphoreSlim startingR = new(1, 1);
    private readonly List<JsonElement> history = new();
    private readonly List<string> commandHistory = new();
    private readonly Dictionary<string, JsonElement> plots = new();
    private readonly Dictionary<string, JsonElement> latestPlots = new();
    private readonly HashSet<string> outputFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly StringBuilder transcript = new();
    private readonly LinkLabel displayFailure = new() { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8), Visible = false, Text = "The R display stopped. Reload display (your R workspace is retained)." };
    private JsonElement sessionEnvelope;
    private string sessionId = Guid.NewGuid().ToString("N"), script = "";
    private long generation, offlineSeq, historySize, commandHistorySize;
    private bool browserFailed, recovering;
    private bool startingPage, pageReady, closing;
    internal IRSession? Session { get; private set; }
    internal Task Ready => ready.Task;
    internal string NativeState { get; private set; } = "ready";
    internal IReadOnlyList<JsonElement> History => history;
    internal string Transcript => transcript.ToString();
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal string WorkingDirectory { get; set; } = SDConfiguration.MyStatsDirectRFolder;
    internal bool HasSelection { get; private set; }
    internal bool Editable { get; private set; } = true;
    internal bool CanUndo { get; private set; }
    internal bool CanRedo { get; private set; }
    internal string SelectedText { get; private set; } = "";
    internal event Action? ScriptChanged;
    internal event Action? SaveRequested;
    internal new event Action? HelpRequested;
    internal event Action? AIRequested;
    internal event Action? MoreRequested;
    internal event Action<string>? CommandRequested;
    internal event Action<string>? Notice;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal Action<string>? PlotOpener { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal string Script
    {
        get => script;
        set
        {
            if (script == value) return;
            script = value ?? "";
            if (pageReady) _ = ExecuteAsync("window.StatsDirectRHost.setScript(" + JsonSerializer.Serialize(script) + ")");
        }
    }

    internal RConsoleView()
    {
        Controls.Add(Browser);
        Controls.Add(displayFailure);
        displayFailure.LinkClicked += async (_, _) => { try { await RecoverDisplayAsync(); } catch (Exception ex) { Notice?.Invoke(ex.Message); } };
        sessionEnvelope = JsonSerializer.SerializeToElement(new { protocolVersion = 1, type = "session", sessionId, generation = 0, seq = 0 });
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (!startingPage) { startingPage = true; _ = InitializePageAsync(); }
    }

    private async Task InitializePageAsync()
    {
        try
        {
            string profile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StatsDirect", "RConsole", "WebView2");
            var environment = await CoreWebView2Environment.CreateAsync(null, profile);
            if (closing) return;
            await Browser.EnsureCoreWebView2Async(environment);
            if (closing) return;
            var core = Browser.CoreWebView2;
            core.Settings.AreHostObjectsAllowed = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.IsGeneralAutofillEnabled = false;
            core.Settings.IsPasswordAutosaveEnabled = false;
            core.SetVirtualHostNameToFolderMapping(new Uri(Origin).Host, Path.Combine(AppContext.BaseDirectory, "RConsole"), CoreWebView2HostResourceAccessKind.DenyCors);
            core.NavigationStarting += (_, e) => e.Cancel = !e.Uri.StartsWith(Origin + "/", StringComparison.Ordinal);
            // A page that fails to load faults Ready, so Run and Continue in R fail at once rather than waiting for ever, and shows the reload link
            core.NavigationCompleted += (_, e) =>
            {
                if (e.IsSuccess || closing || pageReady || e.WebErrorStatus == CoreWebView2WebErrorStatus.OperationCanceled) return;
                FailPage("The R console page could not load: " + e.WebErrorStatus);
            };
            core.NewWindowRequested += (_, e) => e.Handled = true;
            core.DownloadStarting += (_, e) => e.Cancel = true;
            core.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
            core.WebMessageReceived += async (_, e) => await ReceiveAsync(e);
            core.ContextMenuRequested += ContextMenuRequested;
            core.ProcessFailed += (_, e) =>
            {
                pageReady = false;
                browserFailed |= e.ProcessFailedKind == CoreWebView2ProcessFailedKind.BrowserProcessExited;
                displayFailure.Visible = true; displayFailure.BringToFront();
                Notice?.Invoke("The R display stopped. Reload display to return to the same R workspace.");
            };
            await core.AddScriptToExecuteOnDocumentCreatedAsync("window.chrome.webview.addEventListener('message', e => window.StatsDirectRConsole?.receive(e.data));");
            await core.AddScriptToExecuteOnDocumentCreatedAsync(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "RConsole", "windows-host.js")));
            core.Navigate(Origin + "/index.html");
        }
        catch (Exception ex)
        {
            ready.TrySetException(ex);
            var failure = new Label { Dock = DockStyle.Fill, Text = "The R display could not open: " + ex.Message, Padding = new Padding(12), ForeColor = Color.DarkRed };
            Controls.Add(failure); failure.BringToFront();
        }
    }

    private void FailPage(string message)
    {
        ready.TrySetException(new IOException(message));
        _ = ready.Task.Exception;   // observed here: the callers that await Ready get it as their own
        displayFailure.Text = message + " Reload display (your R workspace is retained).";
        displayFailure.Visible = true; displayFailure.BringToFront();
        Notice?.Invoke(message);
    }

    /// <summary>The newest installed 64-bit R of at least the given series, such as "4.2", the oldest the worker takes; null when none is.</summary>
    internal static RVersion? ConsoleRVersion(string minimum)
    {
        string[] parts = minimum.Split('.');
        int major = int.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture), minor = int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
        RVersion? preferred = null;
        foreach (RVersion candidate in RController.CheckR())
            if (candidate.IsX64 && (candidate.MajorVersion > major || candidate.MajorVersion == major && candidate.MinorVersion >= minor) && candidate.CompareTo(preferred) > 0)
                preferred = candidate;
        return preferred;
    }

    /// <summary>Asks the worker to stop, as closing the window does before the window goes, so that R ends its session tidily.</summary>
    internal async Task ShutdownAsync()
    {
        if (Session == null) return;
        try { await Session.StopAsync().WaitAsync(TimeSpan.FromSeconds(4)); }
        catch (Exception error) when (error is TimeoutException or IOException or InvalidOperationException or ObjectDisposedException) { }
    }

    internal async Task StartAsync()
    {
        await Ready;
        await startingR.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(closing, this);
            if (Session == null)
            {
                string helper = Path.Combine(AppContext.BaseDirectory, "StatsDirect.RConsole.Native.exe");
                if (!File.Exists(helper)) throw new FileNotFoundException("The R console component is missing. Repair or reinstall StatsDirect; your script has been retained.", helper);
                // The worker takes any 64-bit R from the minimum it states, and the newest such R installed runs the console; an installed R
                // the worker does not take, older or 32-bit, runs each command through its Rscript instead, as the window did before the
                // console, with a notice; with no R at all the Set up R dialog offers the current R
                // (STATSDIRECT_R_CONSOLE_MINIMUM is the checks' hook: a minimum above any installed R exercises the Rscript fallback)
                string minimum = Environment.GetEnvironmentVariable("STATSDIRECT_R_CONSOLE_MINIMUM") ?? RConsoleSession.CompiledRSeries(helper) ?? RInstallation.MinimumSeries;
                RVersion? version = ConsoleRVersion(minimum), any = RController.PreferredRVersion();
                while (any == null)
                {
                    if (!RController.UserMightHaveInstalledR())
                        throw new OperationCanceledException("R was not installed. The script has been retained.");
                    version = ConsoleRVersion(minimum); any = RController.PreferredRVersion();
                }
                RController.HousekeepOnce(WorkingDirectory);   // the automatic chart files of earlier runs and sessions, a week old or more
                string setup = "More > Set up R installs R " + RInstallation.Version + " beside the R you have.";
                Session = version != null
                    ? new RConsoleSession(helper, version.InstallPath, WorkingDirectory)
                    : new RScriptSession(Path.Combine(any.BinPath, "Rscript.exe"), any.VersionString + (any.IsX64 ? "" : " (32-bit)"), WorkingDirectory,
                        "The interactive console needs 64-bit R " + minimum + " or later. " + setup);
                Session.Event += OnSessionEvent;
                if (version != null)
                {
                    try { await Session.StartAsync(); }
                    catch (Exception error) when (error is IOException or TimeoutException or InvalidOperationException or System.ComponentModel.Win32Exception)
                    {
                        // The worker could not start with this R, or Windows could not run the worker at all: rather than leave the window without R,
                        // each command runs through this R's Rscript, the notice says why, and Restart R tries the console again
                        Session.Event -= OnSessionEvent; Session.Dispose();
                        Session = new RScriptSession(Path.Combine(version.BinPath, "Rscript.exe"), version.VersionString, WorkingDirectory,
                            "The interactive console could not start (" + error.Message.TrimEnd('.') + "). Restart R tries it again.");
                        Session.Event += OnSessionEvent;
                    }
                }
            }
            await Session.StartAsync();
        }
        finally { startingR.Release(); }
    }

    internal async Task SubmitAsync(string source, string origin = "console")
    {
        await StartAsync();
        await Session!.SubmitAsync(source, origin);
    }
    internal Task InterruptAsync() => Session?.InterruptAsync() ?? Task.CompletedTask;
    internal async Task RestartAsync()
    {
        await GetScriptAsync();
        // From the Rscript fallback, Restart R chooses the session afresh: the console when it can start now, which an R installed meanwhile allows
        if (Session is RScriptSession runner)
        {
            await startingR.WaitAsync();
            try { runner.Event -= OnSessionEvent; runner.Dispose(); Session = null; }
            finally { startingR.Release(); }
        }
        if (Session == null) await StartAsync(); else await Session.RestartAsync();
    }
    internal async Task<string> GetScriptAsync()
    {
        if (!pageReady || closing) return script;
        string? source = JsonSerializer.Deserialize<string>(await ExecuteAsync("window.StatsDirectRConsole.snapshot().script").WaitAsync(TimeSpan.FromSeconds(10)));
        UpdateScript(source ?? script);
        return script;
    }
    internal async Task<string> ExecuteAsync(string code)
    {
        await Ready;
        if (closing) return "null";
        return await Browser.CoreWebView2.ExecuteScriptAsync(code);
    }
    internal Task SetModeAsync(string mode) => ExecuteAsync("window.StatsDirectRConsole.setMode(" + JsonSerializer.Serialize(mode) + ")");
    internal void FocusInput() { if (pageReady) _ = ExecuteAsync("window.StatsDirectRHost.focus()"); }

    private async Task ReceiveAsync(CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (closing || !e.Source.StartsWith(Origin + "/", StringComparison.Ordinal)) return;
        try
        {
            using var message = JsonDocument.Parse(e.WebMessageAsJson);
            var item = message.RootElement;
            string Text(string key) => item.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
            string channel = Text("channel"), action = Text("action");
            if (channel == "statsdirect-r-host")
            {
                if (action == "more") MoreRequested?.Invoke();
                else if (action is "save" or "find" or "replace" or "print" or "help") CommandRequested?.Invoke(action);
                else if (action == "editorState")
                {
                    bool Flag(string key) => item.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.True;
                    HasSelection = Flag("hasSelection"); Editable = Flag("editable");
                    CanUndo = Flag("canUndo"); CanRedo = Flag("canRedo"); SelectedText = Text("selectedText");
                }
                return;
            }
            if (channel != "statsdirect-r-console") return;
            if (action == "ready")
            {
                pageReady = true;
                displayFailure.Visible = false;
                SendSession();
                if (ready.Task.IsFaulted) ready = new(TaskCreationOptions.RunContinuationsAsynchronously);   // a page that failed to load and loaded after all, by the browser's own reload
                ready.TrySetResult();
                return;
            }
            if (Text("sessionId") != sessionId || !item.TryGetProperty("generation", out var gen) || gen.GetInt64() != generation) return;
            switch (action)
            {
                case "submit": await SubmitAsync(Text("source"), Text("origin")); break;
                case "inputReply":
                    if (Session == null) throw new InvalidOperationException("R has no pending question.");
                    await Session.ReplyAsync(long.Parse(Text("requestId"), System.Globalization.CultureInfo.InvariantCulture), Text("text")); break;
                case "interrupt": await InterruptAsync(); break;
                case "restart": await RestartAsync(); break;
                case "scriptChanged": UpdateScript(Text("source")); break;
                case "saveScript": UpdateScript(Text("source")); SaveRequested?.Invoke(); break;
                case "help": HelpRequested?.Invoke(); break;
                case "ai": await GetScriptAsync(); AIRequested?.Invoke(); break;
                case "openFile": OpenOutputFile(Text("path")); break;
                case "openPlot": OpenPlot(Text("plotId"), item.GetProperty("revision").GetInt64()); break;
                case "copyPlot": await CopyPlotAsync(Text("plotId"), item.GetProperty("revision").GetInt64()); break;
            }
        }
        catch (Exception ex) { ReportNotice(ex.Message); Session?.PublishState(); if (Session == null) SendOfflineState(); }
    }

    private void UpdateScript(string value)
    {
        if (script == value) return;
        script = value;
        ScriptChanged?.Invoke();
    }
    private void OnSessionEvent(JsonElement item)
    {
        if (closing || IsDisposed) return;
        // Queue UI-originated state notices too: applying them immediately could
        // overtake earlier worker events and make the page drop older sequence IDs.
        var copy = item.Clone();
        try { BeginInvoke(new Action(() => ApplySessionEvent(copy))); }
        catch (InvalidOperationException) when (closing || IsDisposed) { }
    }
    private void ApplySessionEvent(JsonElement item)
    {
        if (closing || IsDisposed) return;
        string? type = item.GetProperty("type").GetString();
        // Native events queued before a restart must not restore an older generation.
        long incoming = item.GetProperty("generation").GetInt64();
        if (incoming < generation) return;
        sessionId = item.GetProperty("sessionId").GetString()!; generation = incoming;
        if (type == "session")
        {
            CloseChartWindows();
            history.Clear(); historySize = 0; plots.Clear(); latestPlots.Clear(); outputFiles.Clear(); transcript.Clear();
            sessionEnvelope = item.Clone(); NativeState = "starting";
            if (pageReady) SendSession();
            return;
        }
        if (type == "state") NativeState = item.GetProperty("state").GetString()!;
        if (type == "exited") NativeState = "stopped";
        if (type == "inputEcho") RememberCommand(item);
        if (type == "plot")
        {
            string id = item.GetProperty("plotId").GetString()!;
            // Keep immutable file metadata after a preview scrolls out of the UI;
            // retaining every base64 revision here would grow without a bound.
            var metadata = item.EnumerateObject().Where(p => p.Name != "preview").ToDictionary(p => p.Name, p => p.Value.Clone());
            if (item.TryGetProperty("preview", out var preview) && preview.ValueKind == JsonValueKind.String) metadata["previewKey"] = JsonSerializer.SerializeToElement(PreviewKey(preview.GetString()!));
            var saved = JsonSerializer.SerializeToElement(metadata);
            plots[id + ":" + item.GetProperty("revision").GetInt64()] = saved;
            latestPlots[id] = saved;
        }
        if (type is "inputEcho" or "output" or "notice")
        {
            if (type == "inputEcho") transcript.Append(item.TryGetProperty("prompt", out var prompt) ? prompt.GetString() : "> ");
            if (item.TryGetProperty("text", out var text)) transcript.Append(text.GetString());
            if (type is "inputEcho" or "notice") transcript.AppendLine();
            if (transcript.Length > 200000) transcript.Remove(0, transcript.Length - 200000);
        }
        if (type == "notice" && item.TryGetProperty("filePath", out var filePath)) outputFiles.Add(Path.GetFullPath(filePath.GetString()!));
        history.Add(item.Clone());
        historySize += item.GetRawText().Length;
        // Replay is bounded independently of the R workspace and saved chart files.
        while (history.Count > 500 || history.Count > 1 && historySize > 24 * 1024 * 1024)
        {
            historySize -= history[0].GetRawText().Length; history.RemoveAt(0);
        }
        if (pageReady) Browser.CoreWebView2.PostWebMessageAsJson(item.GetRawText());
    }
    private void RememberCommand(JsonElement item)
    {
        if (!item.TryGetProperty("origin", out var origin) || origin.GetString() is not ("console" or "script" or "continueInR")
            || !item.TryGetProperty("text", out var text)) return;
        string source = text.GetString() ?? "";
        // Match JavaScript trim's blank test without changing the stored command:
        // U+FEFF is whitespace there; U+0085 is not.
        if (!source.Any(c => c == '\u0085' || !char.IsWhiteSpace(c) && c != '\uFEFF')
            || commandHistory.Count > 0 && commandHistory[^1] == source) return;
        commandHistory.Add(source); commandHistorySize += source.Length;
        // Same limits as the page's recall; keep a single large batch intact.
        while (commandHistory.Count > 1 && (commandHistory.Count > 100 || commandHistorySize > 500000))
        {
            commandHistorySize -= commandHistory[0].Length; commandHistory.RemoveAt(0);
        }
    }
    private void SendSession()
    {
        if (!pageReady || closing) return;
        var envelope = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(sessionEnvelope.GetRawText())!;
        envelope["script"] = JsonSerializer.SerializeToElement(script);
        // Recall belongs to the document, independently of its clearable output.
        envelope["commandHistory"] = JsonSerializer.SerializeToElement(commandHistory);
        // The session's own capabilities stay, among them whether its workspace persists (the Rscript fallback's does not, and the page's status
        // line says so); this view adds what it provides: help, AI and the chart actions
        var capabilities = envelope.TryGetValue("capabilities", out var stated) && stated.ValueKind == JsonValueKind.Object
            ? JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(stated.GetRawText())! : new Dictionary<string, JsonElement>();
        capabilities["plotMimes"] = JsonSerializer.SerializeToElement(new[] { "image/svg+xml", "image/png", "application/pdf" });
        foreach (var (name, provided) in new[] { ("help", true), ("ai", true), ("consoleInput", true), ("rawStdin", false), ("openPlot", true), ("copyPlot", true) })
            capabilities[name] = JsonSerializer.SerializeToElement(provided);
        envelope["capabilities"] = JsonSerializer.SerializeToElement(capabilities);
        envelope["events"] = JsonSerializer.SerializeToElement(history);
        Browser.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(envelope));
        if (Session == null) SendOfflineState();
        else Session.PublishState(); // The latest pending prompt can outlive the bounded replay.
    }
    private void SendOfflineState() => Browser.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { protocolVersion = 1, type = "state", sessionId, generation, seq = ++offlineSeq, state = "ready", prompt = "> " }));
    internal void ReportNotice(string message)
    {
        Notice?.Invoke(message);
        if (closing) return;
        if (Session != null) Session.PublishNotice(message);
        else if (pageReady) OnSessionEvent(JsonSerializer.SerializeToElement(new { protocolVersion = 1, type = "notice", sessionId, generation, seq = ++offlineSeq, text = message }));
    }
    internal async Task ClearOutputAsync()
    {
        // Questions are transcript rows too. The R session owns any live request;
        // republish it after clearing rather than replaying historical questions.
        history.RemoveAll(x => x.GetProperty("type").GetString() is "inputRequested" or "inputEcho" or "output" or "notice" or "plot");
        historySize = history.Sum(x => (long)x.GetRawText().Length);
        transcript.Clear();
        await ExecuteAsync("window.StatsDirectRConsole.clearOutput()");
        Session?.PublishState();
    }
    internal async Task RecoverDisplayAsync()
    {
        if (recovering) return;
        recovering = true;
        try
        {
            try { await GetScriptAsync(); } catch { /* Renderer failure: retain the last acknowledged source. */ }
            pageReady = false;
            ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
            if (browserFailed)
            {
                Browser.Dispose();
                Browser = new WebView2 { Dock = DockStyle.Fill };
                Controls.Add(Browser); Browser.BringToFront(); displayFailure.BringToFront();
                browserFailed = false;
                await InitializePageAsync();
            }
            else Browser.CoreWebView2.Reload();
            await Ready;
        }
        finally { recovering = false; }
    }

    private void OpenOutputFile(string path)
    {
        path = Path.GetFullPath(path);
        string relative = Path.GetRelativePath(Path.GetFullPath(WorkingDirectory), path);
        if (!outputFiles.Contains(path) || Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) || !File.Exists(path)) throw new InvalidOperationException("That output file is no longer available in this R folder.");
        // A document opens in its program; a file of any other kind, one Windows would run, is shown in its folder instead
        string extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension is ".csv" or ".tsv" or ".txt" or ".dat" or ".rds" or ".rdata" or ".rda" or ".json" or ".xml" or ".html" or ".htm" or ".md" or ".xlsx" or ".xls" or ".docx" or ".pdf" or ".r" or ".rmd" or ".log")
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        else Process.Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { "/select,", path }, UseShellExecute = false });   // the path its own argument, quoted when it has spaces
    }
    private static string PreviewKey(string source) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
    private JsonElement PlotMetadata(string id, long? revision)
    {
        if (!(revision.HasValue ? plots.TryGetValue(id + ":" + revision.Value, out var plot) : latestPlots.TryGetValue(id, out plot))) throw new InvalidOperationException("That R chart is no longer available.");
        return plot;
    }
    private JsonElement FindPlot(string id, long? revision)
    {
        var plot = PlotMetadata(id, revision);
        long wantedRevision = plot.GetProperty("revision").GetInt64();
        var visible = history.LastOrDefault(e => e.GetProperty("type").GetString() == "plot" && e.GetProperty("plotId").GetString() == id && e.GetProperty("revision").GetInt64() == wantedRevision);
        if (visible.ValueKind == JsonValueKind.Object && visible.TryGetProperty("preview", out var cached) && cached.ValueKind == JsonValueKind.String) return visible;
        string mime = plot.GetProperty("mime").GetString()!;
        if (mime is not ("image/svg+xml" or "image/png")) return plot;
        var metadata = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(plot.GetRawText())!;
        string path = plot.TryGetProperty("previewPath", out var previewPath) ? previewPath.GetString()! : plot.GetProperty("path").GetString()!;
        var info = new FileInfo(path);
        if (!info.Exists || info.Length > 20 * 1024 * 1024) throw new InvalidOperationException("The chart file is no longer available for copying.");
        metadata["preview"] = JsonSerializer.SerializeToElement("data:" + mime + ";base64," + Convert.ToBase64String(File.ReadAllBytes(path)));
        return JsonSerializer.SerializeToElement(metadata);
    }
    internal void OpenPlot(string id, long? revision)
    {
        var plot = PlotMetadata(id, revision);
        string path = Path.GetFullPath(plot.GetProperty("path").GetString()!);
        string relative = Path.GetRelativePath(Path.GetFullPath(WorkingDirectory), path);
        bool retained = PlotOpener == null && chartWindows.TryGetValue(id + ":" + plot.GetProperty("revision").GetInt64(), out var existing) && !existing.IsDisposed;
        if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) || !retained && !File.Exists(path)) throw new InvalidOperationException("The chart is outside this R session's output folder or has been removed.");
        if (PlotOpener is { } opener) { opener(path); return; }
        string mime = plot.GetProperty("mime").GetString()!;
        if (mime == "image/svg+xml" || mime == "image/png" && path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            string title = plot.TryGetProperty("label", out var label) ? label.GetString() ?? "R chart" : "R chart";
            ShowChartWindow(id, plot.GetProperty("revision").GetInt64(), path, mime, title);
        }
        else Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }
    private async void ContextMenuRequested(object? sender, CoreWebView2ContextMenuRequestedEventArgs e)
    {
        if (e.ContextMenuTarget.Kind != CoreWebView2ContextMenuTargetKind.Image) return;
        e.Handled = true;
        using var deferral = e.GetDeferral();
        try
        {
            JsonElement target = JsonSerializer.Deserialize<JsonElement>(await ExecuteAsync("window.StatsDirectRHost.contextChartTarget()"));
            if (target.ValueKind != JsonValueKind.Object || !target.TryGetProperty("plotId", out var selectedId)
                || selectedId.ValueKind != JsonValueKind.String || !target.TryGetProperty("revision", out var selectedRevision)
                || !selectedRevision.TryGetInt64(out long revision)) return;
            string id = selectedId.GetString()!;
            ShowChartContextMenu(id, revision, PointToClient(Cursor.Position));
        }
        catch (Exception ex) { ReportNotice(ex.Message); }
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing && !closing)
        {
            closing = true;
            CloseChartWindows();
            chartContextMenu?.Dispose();
            if (Session != null) { Session.Event -= OnSessionEvent; Session.Dispose(); }
            ready.TrySetCanceled();
        }
        base.Dispose(disposing);
    }
}
