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
    internal readonly WebView2 Browser = new() { Dock = DockStyle.Fill };
    private readonly string assets, userData;
    private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private JsonObject last;      // the conversation as last given, sent again if the page loads afresh
    private int sequence;
    private bool starting;
    internal Task Ready => ready.Task;
    /// <summary>A code block's button: "copy" or "open", and the code.</summary>
    internal event Action<string, string> CodeRequested;
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
    }

    private async Task InitializeAsync()
    {
        if (starting) return;
        starting = true;
        try
        {
            if (!File.Exists(Path.Combine(assets, "conversation.html"))) throw new FileNotFoundException("The conversation page is missing.");
            CoreWebView2Environment environment = await CoreWebView2Environment.CreateAsync(null, userData);
            await Browser.EnsureCoreWebView2Async(environment);
            var core = Browser.CoreWebView2;
            core.Settings.AreHostObjectsAllowed = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.IsGeneralAutofillEnabled = false;
            core.Settings.IsPasswordAutosaveEnabled = false;
            core.Settings.IsZoomControlEnabled = false;
            core.SetVirtualHostNameToFolderMapping("tutor.statsdirect.invalid", assets, CoreWebView2HostResourceAccessKind.DenyCors);
            core.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
            core.DownloadStarting += (_, e) => e.Cancel = true;
            core.NewWindowRequested += (_, e) => { e.Handled = true; if (e.IsUserInitiated) OpenLink?.Invoke(e.Uri); };
            // the browser's menu kept to copying: the rest, reloading among it, would lose the page's state or leave the conversation
            core.ContextMenuRequested += (_, e) =>
            {
                for (int i = e.MenuItems.Count - 1; i >= 0; i--)
                    if (e.MenuItems[i].Name is not ("copy" or "selectAll")) e.MenuItems.RemoveAt(i);
            };
            bool first = true;
            core.NavigationStarting += (_, e) =>
            {
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
            core.WebMessageReceived += Receive;
            core.ProcessFailed += (_, _) => Fail(new InvalidOperationException("The conversation page stopped. Close and reopen the Script AI pane."));
            core.NavigationCompleted += (_, e) =>
            {
                if (!e.IsSuccess) { if (e.WebErrorStatus != CoreWebView2WebErrorStatus.OperationCanceled) Fail(new IOException("The conversation page could not load: " + e.WebErrorStatus)); return; }
                if (last != null) Post(last);
                ready.TrySetResult();
            };
            core.Navigate(Origin + "/conversation.html");
        }
        catch (Exception ex) { Fail(ex); }
    }

    private void Fail(Exception ex)
    {
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
            string what = m.TryGetProperty("what", out var w) && w.ValueKind == JsonValueKind.String ? w.GetString() : "";
            string code = m.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : "";
            if (what is "copy" or "open" && code.Length > 0 && code.Length <= 200_000) CodeRequested?.Invoke(what, code);
        }
        catch (Exception ex) { Notice?.Invoke(ex.Message); }
    }

    /// <summary>The whole conversation as it stands: the messages, each with a role and its text, and the reply being streamed.</summary>
    internal void Update(JsonArray messages, string partial, bool reset)
    {
        var list = new JsonArray();
        foreach (var message in messages)
            list.Add(new JsonObject { ["role"] = Text(message, "role"), ["text"] = Text(message, "text") });
        var m = new JsonObject { ["action"] = "update", ["sequence"] = ++sequence, ["reset"] = reset, ["partial"] = partial ?? "", ["messages"] = list };
        last = m;
        if (ready.Task.IsCompletedSuccessfully) Post(m);
    }
    private static string Text(JsonNode node, string key) => node is JsonObject o && o[key] is JsonValue v && v.TryGetValue(out string s) ? s : "";
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
        if (disposing) Browser.Dispose();
        base.Dispose(disposing);
    }
}
