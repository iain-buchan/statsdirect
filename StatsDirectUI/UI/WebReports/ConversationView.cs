using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace StatsDirect.UI.WebReports;

/// <summary>
/// The Script AI conversation as a page: each message's Markdown rendered, R code blocks with Copy and Open in R, the view following
/// a streamed reply while the reader is at its end.  The page gets the whole conversation after every change and renders only what
/// changed; nothing from the page is trusted but its two requests, and no page but the bundled one can load.
/// </summary>
internal sealed class ConversationView : UserControl
{
    internal const string Origin = "https://tutor.statsdirect.invalid";
    internal WebView2 Browser { get; private set; } = new() { Dock = DockStyle.Fill };
    private readonly string assets, userData;
    private TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private JsonObject last;      // the conversation as last given, sent again if the page loads afresh
    private int sequence, context;
    private bool starting, failed, disposing;
    internal Task Ready => ready.Task;
    /// <summary>A code block's button: "copy" or "open", and the code.</summary>
    internal event Action<string, string, int, int> CodeRequested;
    internal event Action<string> Notice;
    /// <summary>Opens a link the reader clicked, in the system browser unless a host says otherwise.</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal Action<string> OpenLink { get; set; }

    internal ConversationView(string assetDirectory, string userDataDirectory)
    {
        assets = assetDirectory;
        userData = userDataDirectory;
        OpenLink = OpenExternal;
        Controls.Add(Browser);
        Load += async (_, _) => await InitializeAsync();
        VisibleChanged += async (_, _) => { if (Visible) await ReopenAsync(); };
    }

    private async Task InitializeAsync()
    {
        if (starting || disposing || IsDisposed) return;
        starting = true;
        var browser = Browser;
        try
        {
            if (!File.Exists(Path.Combine(assets, "conversation.html"))) throw new FileNotFoundException("The conversation page is missing.");
            CoreWebView2Environment environment = await CoreWebView2Environment.CreateAsync(null, userData);
            if (!Current(browser)) return;
            await browser.EnsureCoreWebView2Async(environment);
            if (!Current(browser)) return;
            var core = browser.CoreWebView2;
            core.Settings.AreHostObjectsAllowed = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.IsGeneralAutofillEnabled = false;
            core.Settings.IsPasswordAutosaveEnabled = false;
            core.Settings.IsZoomControlEnabled = false;
            core.SetVirtualHostNameToFolderMapping("tutor.statsdirect.invalid", assets, CoreWebView2HostResourceAccessKind.DenyCors);
            core.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
            core.DownloadStarting += (_, e) => e.Cancel = true;
            core.NewWindowRequested += (_, e) => { e.Handled = true; if (Current(browser) && e.IsUserInitiated) OpenLink?.Invoke(e.Uri); };
            // the browser's menu kept to copying: the rest, reloading among it, would lose the page's state or leave the conversation
            core.ContextMenuRequested += (_, e) =>
            {
                for (int i = e.MenuItems.Count - 1; i >= 0; i--)
                    if (e.MenuItems[i].Name is not ("copy" or "selectAll")) e.MenuItems.RemoveAt(i);
            };
            bool first = true;
            core.NavigationStarting += (_, e) =>
            {
                if (!Current(browser)) { e.Cancel = true; return; }
                bool local = e.Uri == Origin + "/conversation.html";
                if (local && first) { first = false; return; }
                e.Cancel = true;
                if (!local && e.IsUserInitiated) OpenLink?.Invoke(e.Uri);
            };
            core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            core.WebResourceRequested += (_, e) =>
            {
                if (!e.Request.Uri.StartsWith(Origin + "/", StringComparison.Ordinal))
                    e.Response = environment.CreateWebResourceResponse(null, 403, "External resources are disabled", "Content-Type: text/plain");
            };
            core.WebMessageReceived += (sender, e) => { if (Current(browser) && !failed) Receive(sender, e); };
            core.ProcessFailed += (_, _) => { if (Current(browser)) Fail(new InvalidOperationException("The conversation page stopped. Close and reopen the AI pane.")); };
            core.NavigationCompleted += (_, e) =>
            {
                if (!Current(browser) || failed) return;
                if (!e.IsSuccess) { if (e.WebErrorStatus != CoreWebView2WebErrorStatus.OperationCanceled) Fail(new IOException("The conversation page could not load: " + e.WebErrorStatus)); return; }
                if (last != null) Post(last);
                ready.TrySetResult();
            };
            core.Navigate(Origin + "/conversation.html");
        }
        catch (Exception ex) { if (Current(browser)) Fail(ex); }
    }

    private bool Current(WebView2 browser) => !disposing && !IsDisposed && browser == Browser && !browser.IsDisposed;

    // Close hides the pane, preserving conversation/drafts. On reopening, replace
    // a failed WebView rather than trying to post into its dead renderer. All
    // security restrictions are reapplied, and NavigationCompleted replays last.
    internal async Task ReopenAsync()
    {
        if (!failed || disposing || IsDisposed) return;
        failed = false;
        starting = false;
        var previous = Browser;
        Browser = new WebView2 { Dock = DockStyle.Fill };
        ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Controls.Remove(previous);
        previous.Dispose();
        Controls.Add(Browser);
        await InitializeAsync();
    }

    private void Fail(Exception ex)
    {
        if (disposing || IsDisposed) return;
        failed = true;
        if (ready.Task.IsCompleted) ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        ready.TrySetException(ex);
        Notice?.Invoke(ex.Message + " If the WebView2 Runtime is missing, run StatsDirectSetup.exe to install it.");
    }

    private static bool Trusted(string source) => Uri.TryCreate(source, UriKind.Absolute, out var u) && u.GetLeftPart(UriPartial.Authority) == Origin;

    private void Receive(object sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (!Trusted(e.Source)) return;
        try
        {
            if (e.WebMessageAsJson.Length > 1_000_000) return;
            using var json = JsonDocument.Parse(e.WebMessageAsJson);
            var m = json.RootElement;
            if (m.ValueKind != JsonValueKind.Object || !m.TryGetProperty("action", out var action) || action.GetString() != "code") return;
            // A queued click from a different script's page must not use the new conversation's data.
            if (!m.TryGetProperty("context", out var ctx) || !ctx.TryGetInt32(out int requestedContext) || requestedContext != context) return;
            string what = m.TryGetProperty("what", out var w) && w.ValueKind == JsonValueKind.String ? w.GetString() : "";
            string code = m.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : "";
            int messageIndex = m.TryGetProperty("messageIndex", out var mi) && mi.TryGetInt32(out int index) ? index : -1;
            int blockIndex = m.TryGetProperty("blockIndex", out var bi) && bi.TryGetInt32(out int block) ? block : -1;
            var messages = last?["messages"] as JsonArray;
            if (messages == null || messageIndex < 0 || messageIndex >= messages.Count) return;
            var original = MessageText(messages[messageIndex], "text");
            var blocks = CompletedCodeBlocks.Read(original, rOnly: what == "open");
            if (what == "copy" && Array.IndexOf(blocks, code) < 0) return;
            if (what == "open" && (blockIndex < 0 || blockIndex >= blocks.Length || blocks[blockIndex] != code)) return;
            if (what is "copy" or "open" && code.Length > 0 && code.Length <= 200_000) CodeRequested?.Invoke(what, code, messageIndex, blockIndex);
        }
        catch (Exception ex) { Notice?.Invoke(ex.Message); }
    }

    /// <summary>The whole conversation as it stands: the messages, each with a role and its text, and the reply being streamed.</summary>
    internal void Update(JsonArray messages, string partial, bool reset)
    {
        var list = new JsonArray();
        foreach (var message in messages)
            list.Add(new JsonObject { ["role"] = MessageText(message, "role"), ["text"] = MessageText(message, "text") });
        if (reset) context++;
        var m = new JsonObject { ["action"] = "update", ["sequence"] = ++sequence, ["context"] = context, ["reset"] = reset, ["partial"] = partial ?? "", ["messages"] = list };
        last = m;
        if (!failed && ready.Task.IsCompletedSuccessfully) Post(m);
    }
    private static string MessageText(JsonNode node, string key) => node is JsonObject o && o[key] is JsonValue v && v.TryGetValue(out string s) ? s : "";
    private void Post(JsonObject m)
    {
        try { Browser.CoreWebView2?.PostWebMessageAsJson(m.ToJsonString()); }
        catch (Exception ex) { Notice?.Invoke(ex.Message); }
    }

    /// <summary>Waits until the page has applied the latest conversation given to it.</summary>
    internal async Task SettledAsync()
    {
        await Ready;
        for (int i = 0; i < 250; i++)
        {
            var state = await StateAsync();
            if (state.TryGetProperty("applied", out var applied) && applied.GetInt32() >= sequence) return;
            await Task.Delay(20);
        }
        throw new TimeoutException("The conversation page did not apply the latest messages.");
    }
    private async Task<JsonElement> JsAsync(string expression)
    {
        await Ready;
        string json = await Browser.CoreWebView2.ExecuteScriptAsync(expression);
        return JsonDocument.Parse(json).RootElement.Clone();
    }
    /// <summary>The page's state: count of messages, the streamed reply's length, renders, the scroll position and the text shown.</summary>
    internal Task<JsonElement> StateAsync() => JsAsync("conversation.state()");
    internal async Task<string> TextAsync() => (await StateAsync()).GetProperty("text").GetString();
    internal async Task<bool> EndInViewAsync() => (await StateAsync()).GetProperty("atEnd").GetBoolean();
    internal async Task<double> ScrollTopAsync() => (await StateAsync()).GetProperty("scrollTop").GetDouble();
    internal async Task<bool> SelectAsync(string text) => (await JsAsync("conversation.select(" + JsonSerializer.Serialize(text) + ")")).GetBoolean();
    internal async Task<string> SelectionAsync() => (await JsAsync("conversation.selection()")).GetString();
    internal async Task<bool> ScrollToTextAsync(string text) => (await JsAsync("conversation.scrollToText(" + JsonSerializer.Serialize(text) + ")")).GetBoolean();
    internal async Task<bool> ScrollToEndAsync() => (await JsAsync("conversation.scrollToEnd()")).GetBoolean();

    private void OpenExternal(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme is "http" or "https")
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch (Exception ex) { Notice?.Invoke(ex.Message); }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            this.disposing = true;
            ready.TrySetCanceled();
            Browser.Dispose();
        }
        base.Dispose(disposing);
    }
}
