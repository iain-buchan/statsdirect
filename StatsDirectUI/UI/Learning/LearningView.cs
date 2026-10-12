using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace StatsDirect.UI.Learning;

internal sealed class LearningView : StatsDirectForm
{
    internal readonly WebView2 Browser = new() { Dock = DockStyle.Fill };
    private readonly LearningService service;
    private readonly string storage;
    private readonly Label notice = new() { Dock = DockStyle.Bottom, AutoSize = true, Text = "Opening Learning…" };
    internal readonly TaskCompletionSource Ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal bool OptionsOnReady;
    internal string SourceId = "__none__";
    internal const string Page = "https://learning.statsdirect.invalid/Learn/index.html";
    internal LearningView(LearningService service, string storage)
    {
        this.service = service; this.storage = storage; Text = "Learning"; Width = 1100; Height = 800;
        Controls.Add(Browser); Controls.Add(notice);
        Shown += async (_, _) => await InitializeAsync();
        Activated += (_, _) => { if (Tag is WindowInformation info) SdApplication.SoleInstance.NoteFormActivated(info); service.RefreshWorkspace(); };
        FormClosed += (_, _) => { service.CancelQuestion(); Ready.TrySetCanceled(); };
        FormClosing += (_, e) => SdApplication.SoleInstance.NoteFormClosing(this, e);
    }
    private async Task InitializeAsync()
    {
        if (Browser.CoreWebView2 != null) return;
        try
        {
            var env = await CoreWebView2Environment.CreateAsync(null, System.IO.Path.Combine(storage, "WebView"));
            await Browser.EnsureCoreWebView2Async(env);
            var core = Browser.CoreWebView2;
            core.Settings.AreHostObjectsAllowed = false; core.Settings.AreDevToolsEnabled = false; core.Settings.IsStatusBarEnabled = false;
            core.SetVirtualHostNameToFolderMapping("learning.statsdirect.invalid", Configuration.SDConfiguration.InstallationDirectory, CoreWebView2HostResourceAccessKind.DenyCors);
            core.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
            core.NewWindowRequested += (_, e) => { e.Handled = true; OpenWeb(e.Uri); };
            core.NavigationStarting += (_, e) => { if (e.Uri != Page) { e.Cancel = true; OpenWeb(e.Uri); } };
            core.DownloadStarting += (_, e) => e.Cancel = true;
            await core.AddScriptToExecuteOnDocumentCreatedAsync("if(location.href === " + JsonSerializer.Serialize(Page) + ") window.webkit={messageHandlers:{statsDirectLearn:{postMessage:body=>window.chrome.webview.postMessage(body)}}};");
            core.WebMessageReceived += async (_, e) =>
            {
                if (e.Source != Page || Browser.Source?.AbsoluteUri != Page || e.WebMessageAsJson.Length > 15_000_000) return;
                try { if (JsonNode.Parse(e.WebMessageAsJson) is JsonObject message) await service.HandleAsync(this, message); }
                catch (Exception ex) { await CallAsync("notice", JsonValue.Create(ex.Message)); }
            };
            core.NavigationCompleted += (_, e) => { if (!e.IsSuccess) { notice.Text = "Learning could not open: " + e.WebErrorStatus; Ready.TrySetException(new IOException(notice.Text)); } };
            core.Navigate(Page);
        }
        catch (Exception ex) { notice.Text = "Learning could not open: " + ex.Message; Ready.TrySetException(ex); }
    }
    private static void OpenWeb(string text)
    {
        if (CoursePack.Web(text)) Process.Start(new ProcessStartInfo(text) { UseShellExecute = true });
    }
    internal async Task<JsonNode> CallAsync(string action, JsonNode value = null)
    {
        if (IsDisposed || Browser.CoreWebView2 == null) return null;
        string expression = "window.statsDirectLearn?.[" + JsonSerializer.Serialize(action) + "](" + (value?.ToJsonString() ?? "null") + ")";
        string result;
        try
        {
            result = await Browser.CoreWebView2.ExecuteScriptAsync("(()=>{try{const value=" + expression + ";" + (action == "restore" ? "const error=document.getElementById('loadError');if(error)throw Error(error.textContent);" : "") + "return {ok:true,value:value??null};}catch(e){return {ok:false,error:String(e.message)}}})()");
        }
        catch (ObjectDisposedException) when (IsDisposed) { return null; }
        if (IsDisposed) return null;
        var envelope = JsonNode.Parse(result);
        if (!envelope.B("ok")) throw new IOException(envelope.S("error", "The learning page did not complete this action."));
        // Platform wording belongs to the host; keep the pinned Mac content bundle intact.
        await Browser.CoreWebView2.ExecuteScriptAsync("for (const id of ['saveStatus','optionSaved','connectionPrivacy']) {const e=document.getElementById(id);if(e)e.textContent=e.textContent.replaceAll('on this Mac','on this PC').replaceAll('this Mac’s','this computer’s');}");
        return envelope["value"]?.DeepClone();
    }
    internal void Loaded() { notice.Text = "Learning record stored on this PC"; Ready.TrySetResult(); }
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
