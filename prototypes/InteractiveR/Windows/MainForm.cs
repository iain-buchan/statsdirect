using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Text;
using System.Text.Json;

namespace StatsDirect.InteractiveR;

internal sealed partial class MainForm : Form
{
    private const string Host = "rconsole.statsdirect.local";
    private readonly WebView2 browser = new() { Dock = DockStyle.Fill };
    private readonly PrototypeOptions options;
    private readonly RConsoleSession session;
    private readonly List<JsonElement> history = new();
    private readonly Dictionary<string, JsonElement> plots = new();
    private readonly Dictionary<string, JsonElement> latestPlots = new();
    private JsonElement? sessionEnvelope;
    private bool pageReady, started, closing;
    private string? scriptPath;
    private string initialScript = "";
    private string currentScript = "";
    private bool initialRun;
    private string? sessionId;
    private long generation;
    private string nativeState = "starting";

    internal MainForm(PrototypeOptions options)
    {
        this.options = options;
        session = new(options.NativeHelper, options.RHome, options.WorkingDirectory);
        Text = "StatsDirect interactive R console — prototype";
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new(1100, 740); MinimumSize = new(660, 480);
        StartPosition = FormStartPosition.CenterScreen;
        Controls.Add(browser);
        var menu = new MenuStrip();
        var file = new ToolStripMenuItem("File");
        file.DropDownItems.Add("Open R script…", null, async (_, _) => await OpenScriptAsync());
        file.DropDownItems.Add("Save R script…", null, async (_, _) => SaveScript(await BrowserScriptAsync()));
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add("Close prototype", null, (_, _) => Close());
        menu.Items.Add(file); Controls.Add(menu); MainMenuStrip = menu;
        if (options.Script is not null)
        {
            scriptPath = Path.GetFullPath(options.Script);
            initialScript = File.ReadAllText(scriptPath, Encoding.UTF8);
            currentScript = initialScript;
            initialRun = true; // Explicit --script retains Continue in R's automatic execution behaviour.
        }
        session.Event += OnSessionEvent;
        Shown += async (_, _) =>
        {
            // Constructor sizes are physical pixels in this standalone host.
            // Match the intended CSS viewport on high-DPI displays, then cap it
            // to the current monitor rather than reducing the user's text scale.
            double scale = DeviceDpi / 96.0;
            var area = Screen.FromControl(this).WorkingArea;
            ClientSize = new(Math.Min((int)(1100 * scale), Math.Max(660, area.Width - 100)), Math.Min((int)(740 * scale), Math.Max(480, area.Height - 160)));
            CenterToScreen();
            await StartPageAsync();
        };
        FormClosed += (_, _) => { closing = true; session.Dispose(); };
    }

    private async Task StartPageAsync()
    {
        try
        {
            string userData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StatsDirect", "InteractiveRPrototype", "WebView2");
            var environment = await CoreWebView2Environment.CreateAsync(null, userData);
            if (closing) return;
            await browser.EnsureCoreWebView2Async(environment);
            if (closing) return;
            var core = browser.CoreWebView2;
            core.Settings.AreHostObjectsAllowed = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.IsGeneralAutofillEnabled = false;
            core.Settings.IsPasswordAutosaveEnabled = false;
            core.SetVirtualHostNameToFolderMapping(Host, Path.Combine(AppContext.BaseDirectory, "Web"), CoreWebView2HostResourceAccessKind.DenyCors);
            core.NavigationStarting += (_, e) => e.Cancel = !Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host != Host;
            core.NewWindowRequested += (_, e) => e.Handled = true;
            core.DownloadStarting += (_, e) => e.Cancel = true;
            core.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
            core.WebMessageReceived += async (_, e) => await ReceiveAsync(e);
            core.ProcessFailed += (_, e) => ShowFailure("The console page stopped: " + e.ProcessFailedKind);
            await core.AddScriptToExecuteOnDocumentCreatedAsync("window.chrome.webview.addEventListener('message', event => window.StatsDirectRConsole?.receive(event.data));");
            core.Navigate($"https://{Host}/index.html");
        }
        catch (Exception error) { ShowFailure(error.Message); }
    }

    private void ShowFailure(string message)
    {
        if (closing) return;
        Controls.Add(new Label { Text = message, Dock = DockStyle.Top, AutoSize = true, Padding = new(12), ForeColor = Color.DarkRed });
    }

    private void OnSessionEvent(JsonElement item)
    {
        if (closing || IsDisposed) return;
        if (InvokeRequired)
        {
            var copy = item.Clone();
            try { BeginInvoke(new Action(() => OnSessionEvent(copy))); }
            catch (InvalidOperationException) when (closing || IsDisposed) { }
            return;
        }
        string? type = item.TryGetProperty("type", out var typeValue) ? typeValue.GetString() : null;
        if (type == "state" && item.TryGetProperty("state", out var state)) nativeState = state.GetString() ?? "starting";
        if (item.TryGetProperty("sessionId", out var idValue)) sessionId = idValue.GetString();
        if (item.TryGetProperty("generation", out var generationValue)) generation = generationValue.GetInt64();
        if (type == "session")
        {
            history.Clear(); plots.Clear(); latestPlots.Clear(); nativeState = "starting";
            sessionEnvelope = item.Clone();
            if (pageReady) SendSession();
        }
        else
        {
            if (type == "plot" && item.TryGetProperty("plotId", out var plotId))
            {
                string id = plotId.GetString()!;
                latestPlots[id] = item.Clone();
                plots[id + ":" + item.GetProperty("revision").GetInt64()] = item.Clone();
            }
            history.Add(item.Clone());
            if (pageReady) browser.CoreWebView2.PostWebMessageAsJson(item.GetRawText());
        }
        if (type == "state" && item.TryGetProperty("state", out var stateValue) && stateValue.GetString() == "ready" && initialRun)
        {
            initialRun = false;
            _ = SubmitInitialAsync();
        }
        if (type == "state" && nativeState == "ready" && options.UiSmokeResult is not null && !smokeStarted)
        {
            smokeStarted = true;
            _ = RunUiSmokeAsync();
        }
    }

    private void SendSession()
    {
        if (sessionEnvelope is null) return;
        // One initialization envelope owns replay; do not separately resend those events.
        var envelope = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(sessionEnvelope.Value.GetRawText())!;
        envelope["script"] = JsonSerializer.SerializeToElement(currentScript);
        envelope["scriptName"] = JsonSerializer.SerializeToElement(scriptPath is null ? "Untitled.R" : Path.GetFileName(scriptPath));
        envelope["workingDirectory"] = JsonSerializer.SerializeToElement(options.WorkingDirectory);
        envelope["events"] = JsonSerializer.SerializeToElement(history);
        browser.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(envelope));
    }

    private async Task SubmitInitialAsync()
    {
        try { await session.SubmitAsync(initialScript, "continueInR"); }
        catch (Exception error) { ReportError(error.Message); }
    }

    private async Task ReceiveAsync(CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (closing || !e.Source.StartsWith($"https://{Host}/", StringComparison.Ordinal)) return;
        try
        {
            using var message = JsonDocument.Parse(e.WebMessageAsJson);
            var item = message.RootElement;
            if (!item.TryGetProperty("channel", out var channel) || channel.GetString() != "statsdirect-r-console") return;
            string? type = item.TryGetProperty("action", out var action) ? action.GetString() : item.GetProperty("type").GetString();
            string TextValue(string name) => item.TryGetProperty(name, out var value) ? value.GetString() ?? "" : "";
            switch (type)
            {
                case "ready":
                    pageReady = true;
                    if (started) SendSession();
                    else { started = true; await session.StartAsync(); }
                    return;
            }
            if (sessionId is null || TextValue("sessionId") != sessionId ||
                !item.TryGetProperty("generation", out var incomingGeneration) || incomingGeneration.GetInt64() != generation) return;
            switch (type)
            {
                case "submit": await session.SubmitAsync(TextValue("source"), TextValue("origin")); break;
                case "inputReply":
                    var request = item.GetProperty("requestId");
                    long requestId = request.ValueKind == JsonValueKind.String ? long.Parse(request.GetString()!, System.Globalization.CultureInfo.InvariantCulture) : request.GetInt64();
                    await session.ReplyAsync(requestId, TextValue("text")); break;
                case "interrupt": await session.InterruptAsync(); break;
                case "restart": currentScript = await BrowserScriptAsync(); await session.RestartAsync(); break;
                case "saveScript": SaveScript(TextValue("source")); break;
                case "scriptChanged": currentScript = TextValue("source"); break;
                case "openScript": await OpenScriptAsync(); break;
                case "openPlot": OpenPlot(TextValue("plotId"), item.TryGetProperty("revision", out var openRevision) ? openRevision.GetInt64() : null); break;
                case "copyPlot": await CopyPlotAsync(TextValue("plotId"), item.TryGetProperty("revision", out var copyRevision) ? copyRevision.GetInt64() : null); break;
                case "help": ReportNotice("Use Console for commands and answers. Script and Console share one R workspace. Stop interrupts the current operation; Restart R clears variables. This is an isolated prototype."); break;
                case "ai": ReportNotice("The AI pane is not connected in this isolated prototype. The released StatsDirect application is unchanged."); break;
            }
        }
        catch (Exception error) { ReportError(error.Message); }
    }

    private void ReportError(string message)
    {
        ReportNotice(message);
        session.PublishState();
    }

    private void ReportNotice(string message)
    {
        if (!closing) session.PublishNotice(message);
    }

    private void SaveScript(string source)
    {
        using var dialog = new SaveFileDialog { Filter = "R script (*.R)|*.R|All files (*.*)|*.*", DefaultExt = "R", AddExtension = true, FileName = scriptPath ?? "Untitled.R" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        File.WriteAllText(dialog.FileName, source, new UTF8Encoding(false));
        scriptPath = dialog.FileName;
        currentScript = source;
        ReportNotice("Script saved: " + scriptPath);
    }

    private async Task<string> BrowserScriptAsync()
    {
        if (!pageReady) return currentScript;
        return JsonSerializer.Deserialize<string>(await browser.CoreWebView2.ExecuteScriptAsync("window.StatsDirectRConsole.snapshot().script")) ?? currentScript;
    }

    private async Task OpenScriptAsync()
    {
        using var dialog = new OpenFileDialog { Filter = "R script (*.R)|*.R|All files (*.*)|*.*" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        string source = File.ReadAllText(dialog.FileName, Encoding.UTF8);
        scriptPath = dialog.FileName;
        currentScript = source;
        // A session envelope denotes a new generation. Updating a script must
        // preserve the live console model and workspace instead of replaying it.
        await browser.CoreWebView2.ExecuteScriptAsync($$"""
            (() => {
              window.StatsDirectRConsole.setMode('script');
              document.getElementById('script-source').select();
              window.StatsDirectRConsole.paste({{JsonSerializer.Serialize(source)}});
            })()
            """);
    }
}
