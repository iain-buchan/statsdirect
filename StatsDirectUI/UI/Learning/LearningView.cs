using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace StatsDirect.UI.Learning;

internal sealed class LearningView : StatsDirectForm
{
    internal WebView2 Browser { get; private set; } = new() { Dock = DockStyle.Fill };
    private readonly LearningService service;
    private readonly string storage;
    private readonly Label notice = new() { Dock = DockStyle.Bottom, AutoSize = true, Text = "Opening Learning…" };
    internal TaskCompletionSource Ready { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal string Generation { get; private set; } = Guid.NewGuid().ToString("N");
    internal bool NeedsRecovery { get; private set; }
    internal bool Restoring { get; private set; }
    internal JsonObject ReplyEvent { get; private set; }
    private readonly Button reopen = new() { Dock = DockStyle.Top, AutoSize = true, Text = "Reopen Learning", Visible = false };
    internal bool OptionsOnReady;
    internal string SourceId = "__none__";
    internal const string Page = "https://learning.statsdirect.invalid/Learn/index.html";
    internal LearningView(LearningService service, string storage)
    {
        this.service = service; this.storage = storage; Text = "Learning"; Width = 1100; Height = 800;
        Controls.Add(Browser); Controls.Add(notice); Controls.Add(reopen);
        reopen.Click += async (_, _) => await RecoverAsync();
        Shown += async (_, _) => await InitializeAsync();
        Activated += (_, _) => { if (Tag is WindowInformation info) SdApplication.SoleInstance.NoteFormActivated(info); service.RefreshWorkspace(); };
        FormClosed += (_, _) => { service.LearningClosed(this); Ready.TrySetCanceled(); };
        FormClosing += (_, e) => SdApplication.SoleInstance.NoteFormClosing(this, e);
    }
    private async Task InitializeAsync()
    {
        var browser = Browser;
        if (browser.CoreWebView2 != null) return;
        try
        {
            var env = await CoreWebView2Environment.CreateAsync(null, System.IO.Path.Combine(storage, "WebView"));
            if (IsDisposed || browser != Browser) return;
            await browser.EnsureCoreWebView2Async(env);
            if (IsDisposed || browser != Browser) return;
            var core = browser.CoreWebView2;
            core.Settings.AreHostObjectsAllowed = false; core.Settings.AreDevToolsEnabled = false; core.Settings.IsStatusBarEnabled = false;
            core.SetVirtualHostNameToFolderMapping("learning.statsdirect.invalid", Configuration.SDConfiguration.InstallationDirectory, CoreWebView2HostResourceAccessKind.DenyCors);
            core.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
            core.NewWindowRequested += (_, e) => { e.Handled = true; OpenWeb(e.Uri); };
            core.NavigationStarting += (_, e) => { if (e.Uri != Page) { e.Cancel = true; OpenWeb(e.Uri); } };
            core.DownloadStarting += (_, e) => e.Cancel = true;
            core.ProcessFailed += (_, e) =>
            {
                if (e.ProcessFailedKind is CoreWebView2ProcessFailedKind.BrowserProcessExited or CoreWebView2ProcessFailedKind.RenderProcessExited or CoreWebView2ProcessFailedKind.RenderProcessUnresponsive)
                    Failed(browser);
            };
            string generation = Generation;
            await core.AddScriptToExecuteOnDocumentCreatedAsync("if(location.href === " + JsonSerializer.Serialize(Page) + ") window.webkit={messageHandlers:{statsDirectLearn:{postMessage:body=>window.chrome.webview.postMessage({...body,viewGeneration:" + JsonSerializer.Serialize(generation) + "})}}};");
            core.WebMessageReceived += async (_, e) =>
            {
                if (IsDisposed || NeedsRecovery || browser != Browser || e.Source != Page || browser.Source?.AbsoluteUri != Page || e.WebMessageAsJson.Length > 15_000_000) return;
                try { if (JsonNode.Parse(e.WebMessageAsJson) is JsonObject message && message.S("viewGeneration") == Generation) await service.HandleAsync(this, message); }
                catch (Exception ex) { await CallAsync("notice", JsonValue.Create(ex.Message)); }
            };
            core.NavigationCompleted += (_, e) => { if (!e.IsSuccess && browser == Browser && !IsDisposed) Failed(browser); };
            core.Navigate(Page);
        }
        catch (Exception) { Failed(browser); }
    }
    private void Failed(WebView2 browser)
    {
        if (IsDisposed || browser != Browser || NeedsRecovery) return;
        NeedsRecovery = true; notice.Text = "Learning stopped. Reopen it to continue with your saved conversation.";
        reopen.Visible = true; browser.Visible = false;
        Ready.TrySetException(new IOException(notice.Text));
    }
    internal async Task RecoverAsync()
    {
        if (IsDisposed || !NeedsRecovery) return;
        var previous = Browser;
        Controls.Remove(previous); previous.Dispose();
        Browser = new WebView2 { Dock = DockStyle.Fill };
        Controls.Add(Browser); Browser.BringToFront();
        Generation = Guid.NewGuid().ToString("N");
        Ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        NeedsRecovery = false; Restoring = true; reopen.Visible = false;
        notice.Text = "Restoring Learning…";
        await InitializeAsync();
    }
    internal void BeginReply(string id, string lessonId)
    { ReplyEvent = new JsonObject { ["action"] = "partial", ["value"] = new JsonObject { ["id"] = id, ["text"] = "" }, ["partial"] = "", ["lessonId"] = lessonId }; }
    internal void AcknowledgeReply(JsonNode state)
    {
        if (ReplyEvent == null || ReplyEvent.S("action") == "partial") return;
        string id = ReplyEvent["value"].S("id");
        if (state.A("conversation").Any(c => c.S("id") == id) || state?["diagnostics"].S("requestID") == id) ReplyEvent = null;
    }
    private static void OpenWeb(string text)
    {
        if (CoursePack.Web(text)) Process.Start(new ProcessStartInfo(text) { UseShellExecute = true });
    }
    internal async Task<JsonNode> CallAsync(string action, JsonNode value = null)
    {
        if (IsDisposed) return null;
        if (action is "partial" or "reply" or "tutorError" && ReplyEvent?["value"].S("id") == value.S("id"))
        {
            ReplyEvent["action"] = action; ReplyEvent["value"] = value.DeepClone();
            if (action == "partial") ReplyEvent["partial"] = value.S("text");
        }
        var browser = Browser;
        if (NeedsRecovery || browser.CoreWebView2 == null) return null;
        string expression = "window.statsDirectLearn?.[" + JsonSerializer.Serialize(action) + "](" + (value?.ToJsonString() ?? "null") + ")";
        string result;
        try
        {
            result = await browser.CoreWebView2.ExecuteScriptAsync("(()=>{try{const value=" + expression + ";" + (action == "restore" ? "const error=document.getElementById('loadError');if(error)throw Error(error.textContent);" : "") + "return {ok:true,value:value??null};}catch(e){return {ok:false,error:String(e.message)}}})()");
        }
        catch (Exception) when (IsDisposed || NeedsRecovery || browser != Browser) { return null; }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException)
        { Failed(browser); return null; }
        if (IsDisposed || NeedsRecovery || browser != Browser) return null;
        if (result is null or "null") { Failed(browser); return null; }
        var envelope = JsonNode.Parse(result);
        if (!envelope.B("ok")) throw new IOException(envelope.S("error", "The learning page did not complete this action."));
        // Platform wording belongs to the host; keep the pinned Mac content bundle intact.
        try { await browser.CoreWebView2.ExecuteScriptAsync("for (const id of ['saveStatus','optionSaved','connectionPrivacy']) {const e=document.getElementById(id);if(e)e.textContent=e.textContent.replaceAll('on this Mac','on this PC').replaceAll('this Mac’s','this computer’s');}"); }
        catch (Exception) when (IsDisposed || NeedsRecovery || browser != Browser) { }
        return envelope["value"]?.DeepClone();
    }
    internal void Loaded() { Restoring = false; notice.Text = "Learning record stored on this PC"; Ready.TrySetResult(); }
    internal override bool SaveContents() => service.SaveCurrent();
    internal override bool SaveAsContents() { _ = CallAsync("exportRecord"); return true; }
    internal override void EditCopy() => Browser.CoreWebView2?.ExecuteScriptAsync("document.execCommand('copy')");
    internal override void EditCut() => Browser.CoreWebView2?.ExecuteScriptAsync("document.execCommand('cut')");
    internal override void EditPaste() => Browser.CoreWebView2?.ExecuteScriptAsync("document.execCommand('paste')");
    internal override void Print() => Browser.CoreWebView2?.ShowPrintUI();
    internal override void ShowHelp() => SdApplication.SoleInstance.ShowHelp(this, "1000");
    public override IList<Pane> AvailablePanes => new[] { SelectedPane };
    public override Pane SelectedPane => new("Learning", Tag as WindowInformation, this);
    public override bool SelectPane(Pane pane) { Activate(); return true; }
}
