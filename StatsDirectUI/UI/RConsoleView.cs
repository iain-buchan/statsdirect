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
    private readonly Dictionary<string, JsonElement> plots = new();
    private readonly Dictionary<string, JsonElement> latestPlots = new();
    private readonly HashSet<string> outputFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly StringBuilder transcript = new();
    private readonly LinkLabel displayFailure = new() { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8), Visible = false, Text = "The R display stopped. Reload display (your R workspace is retained)." };
    private JsonElement sessionEnvelope;
    private string sessionId = Guid.NewGuid().ToString("N"), script = "";
    private long generation, offlineSeq, historySize;
    private bool browserFailed, recovering;
    private bool startingPage, pageReady, closing;
    internal RConsoleSession? Session { get; private set; }
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
    internal Action<string> PlotOpener { get; set; } = path => Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });

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
            core.NavigationCompleted += (_, e) => { if (!e.IsSuccess) ReportNotice("The R console page could not load: " + e.WebErrorStatus); };
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
                RVersion? version = RController.PreferredRVersion();
                while (version == null)
                {
                    if (!RController.UserMightHaveInstalledR()) throw new OperationCanceledException("R was not installed. The script has been retained.");
                    version = RController.PreferredRVersion();
                }
                Session = new RConsoleSession(helper, version.InstallPath, WorkingDirectory);
                Session.Event += OnSessionEvent;
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
            history.Clear(); historySize = 0; plots.Clear(); latestPlots.Clear(); outputFiles.Clear(); transcript.Clear();
            sessionEnvelope = item.Clone(); NativeState = "starting";
            if (pageReady) SendSession();
            return;
        }
        if (type == "state") NativeState = item.GetProperty("state").GetString()!;
        if (type == "exited") NativeState = "stopped";
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
    private void SendSession()
    {
        if (!pageReady || closing) return;
        var envelope = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(sessionEnvelope.GetRawText())!;
        envelope["script"] = JsonSerializer.SerializeToElement(script);
        envelope["capabilities"] = JsonSerializer.SerializeToElement(new { plotMimes = new[] { "image/svg+xml", "image/png", "application/pdf" }, help = true, ai = true, consoleInput = true, rawStdin = false, openPlot = true, copyPlot = true });
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
        history.RemoveAll(x => x.GetProperty("type").GetString() is "inputEcho" or "output" or "notice" or "plot");
        historySize = history.Sum(x => (long)x.GetRawText().Length);
        transcript.Clear();
        await ExecuteAsync("window.StatsDirectRConsole.clearOutput()");
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
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
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
        string path = Path.GetFullPath(PlotMetadata(id, revision).GetProperty("path").GetString()!);
        string relative = Path.GetRelativePath(Path.GetFullPath(WorkingDirectory), path);
        if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) || !File.Exists(path)) throw new InvalidOperationException("The chart is outside this R session's output folder or has been removed.");
        PlotOpener(path);
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
            chartContextMenu?.Dispose();
            if (Session != null) { Session.Event -= OnSessionEvent; Session.Dispose(); }
            ready.TrySetCanceled();
        }
        base.Dispose(disposing);
    }
}
