using System.Text.Json;
using Microsoft.Web.WebView2.Core;

internal static partial class Program
{
    private static async Task CheckToolbar()
    {
        async Task Width(int width)
        {
            await view.Browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Emulation.setDeviceMetricsOverride", JsonSerializer.Serialize(new { width, height = 760, deviceScaleFactor = 1, mobile = false }));
            await Task.Delay(100);
        }
        await Width(1400);
        await view.CallAsync("load", new { entries = new[] {
            new {title="First", operation="TPaired", helpContextId=1239, html="<p>Alpha words</p>", record=(object)new {operation="TPaired", inputs=new object[0]}},
            new {title="Note", operation="", helpContextId=0, html="<p>Only a note</p>", record=(object)null}
        }});
        Check(await True("document.querySelectorAll('[role=toolbar]').length===1 && [...document.querySelectorAll('.document-toolbar-row > button')].slice(0,7).map(b=>b.dataset.hostCommand||b.dataset.command).join('|')==='save|undo|redo|cut|copy|paste|find'"), "one report toolbar follows the shared Save, history, clipboard, Find order");
        Check(await True("document.querySelector('[data-command=bold]').closest('.document-toolbar-row')!==null && document.querySelector('[data-host-command=save]').getBoundingClientRect().top===document.querySelector('#report-more-toggle').getBoundingClientRect().top"), "essential formatting and Save remain in one row at ordinary widths");
        string firstId = (await Js("document.querySelector('.report-entry').dataset.resultId")).GetString();
        await view.CallAsync("selectResult", new { id=firstId });
        await Task.Delay(30);
        await Js("window.toolbarOriginalBold=document.querySelector('[data-command=bold]');window.toolbarOriginalUndo=document.querySelector('[data-command=undo]');document.querySelector('[data-command=bold]').click()");
        Check(await True("getComputedStyle(document.createTreeWalker(document.querySelector('.report-entry p'),NodeFilter.SHOW_TEXT).nextNode().parentElement).fontWeight==='700'"), "toolbar formatting applies to the selected report result");
        await Js("document.querySelector('[data-command=undo]').click()");
        await view.CallAsync("snapshot");
        Check(await True("document.querySelector('.report-entry').textContent==='Alpha words' && document.querySelector('.report-entry p b,.report-entry p strong')===null && !document.querySelector('[data-command=redo]').disabled"), "toolbar Undo uses existing history and enables Redo");
        await view.CallAsync("selectResult", new { id=firstId });
        await Width(360);
        Check(await True("document.querySelector('[data-command=bold]')===window.toolbarOriginalBold && document.querySelector('[data-command=undo]')===window.toolbarOriginalUndo && document.querySelector('.report-overflow-commands').contains(window.toolbarOriginalBold)"), "narrow layout moves the original formatting controls into More without resetting history");
        Check(await True("document.querySelector('.document-toolbar-row').scrollWidth<=document.querySelector('.document-toolbar-row').clientWidth+1"), "narrow report toolbar fits without wrapping or horizontal scrolling");
        await Js("document.querySelector('#report-more-toggle').focus();document.querySelector('#report-more-toggle').dispatchEvent(new KeyboardEvent('keydown',{key:'ArrowDown',bubbles:true,cancelable:true}))");
        Check(await True("!document.querySelector('#report-more-tools').hidden && document.querySelector('#report-more-tools').contains(document.activeElement)"), "keyboard opens More and enters its controls");
        await Js("window.toolbarOriginalBold.click()");
        Check(await True("getComputedStyle(document.createTreeWalker(document.querySelector('.report-entry p'),NodeFilter.SHOW_TEXT).nextNode().parentElement).fontWeight==='700'"), "formatting in More retains the report selection");
        await Js("document.querySelector('#report-more-toggle').click();document.querySelector('#report-more-toggle').focus();document.querySelector('#report-more-toggle').dispatchEvent(new KeyboardEvent('keydown',{key:'Escape',bubbles:true,cancelable:true}))");
        Check(await True("document.querySelector('#report-more-tools').hidden && document.activeElement.id==='report-more-toggle'"), "Escape closes More and returns focus");
        await Width(1400);
        await view.CallAsync("selectResult", new { id=firstId });
        string command = null; int help = -1;
        Action<string> receive = value => command=value; Action<int> topic = value => help=value;
        view.CommandRequested += receive; view.TopicRequested += topic;
        Check(await True("[...document.querySelectorAll('[data-host-command]')].map(b=>b.dataset.hostCommand).filter(x=>['help','ai','r'].includes(x)).join('|')==='help|ai|r'"), "report toolbar keeps Help, AI and R in Mac command order");
        await Js("document.querySelector('[data-host-command=ai]').click()");await Task.Delay(50);
        Check(command=="ai","report toolbar AI dispatches to the native dockable assistant");
        try
        {
            await Js("document.querySelector('[data-host-command=save]').click()");
            await view.CallAsync("snapshot");
            Check(command=="save", "one-click Save reaches the native save handler");
            await Js("document.querySelector('[data-host-command=find]').click()");
            await view.CallAsync("snapshot");
            Check(command=="find", "Find reaches the native report find bar");
            await Js("document.querySelector('[data-host-command=help]').focus();document.querySelector('[data-host-command=help]').click();document.querySelector('[data-host-command=r]').focus();document.querySelector('[data-host-command=r]').click()");
            JsonElement target = await view.CurrentResultAsync();
            Check(help==1239 && command=="continueInR" && target.GetProperty("resultId").GetString()==firstId, "Help and R keep the selected analysis after keyboard toolbar focus");
        }
        finally { view.CommandRequested -= receive; view.TopicRequested -= topic; }
        await Js("(()=>{const p=document.querySelectorAll('.report-entry p')[1],r=document.createRange();r.selectNodeContents(p);getSelection().removeAllRanges();getSelection().addRange(r);WindowsReportToolbar.refresh()})()");
        Check(await True("document.querySelector('[data-host-command=r]').disabled"), "R is disabled for a selected note rather than silently continuing a different result");
        // a disabled button is scanned, so its tooltip gives the reason; enabled, it names what it continues
        Check(await True("document.querySelector('[data-host-command=r]').title==='Continue in R: put the cursor in a result; this text has no record of a run' && document.querySelector('[data-host-command=r]').getAttribute('aria-label')==='Continue in R: put the cursor in a result; this text has no record of a run'"), "the disabled R button's tooltip says the note has no record of a run");
        await view.CallAsync("selectResult", new { id=firstId });
        await Js("WindowsReportToolbar.refresh()");
        Check(await True("!document.querySelector('[data-host-command=r]').disabled && document.querySelector('[data-host-command=r]').title==='Continue in R: the result the cursor is in, or the latest result, as an R script'"), "the enabled R button's tooltip names the result it continues");
        await Js("getSelection().removeAllRanges();WindowsReportToolbar.refresh()");
        Check(await True("!document.querySelector('[data-host-command=r]').disabled && document.querySelector('[data-host-command=r]').title==='Continue in R: the result the cursor is in, or the latest result, as an R script'"), "with the cursor in no result, R stays enabled for the latest recorded result");
        using (var png = File.Create(System.IO.Path.Combine(output,"single-toolbar-wide.png"))) await view.Browser.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,png);
        await Width(360);
        await Js("document.querySelector('#report-more-toggle').click()");
        using (var png = File.Create(System.IO.Path.Combine(output,"single-toolbar-narrow.png"))) await view.Browser.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,png);
        // a report with no recorded result: the button is disabled and says to run an analysis first
        await Width(1400);
        await view.CallAsync("load", new { entries = new[] { new {title="Note", operation="", helpContextId=0, html="<p>Only a note</p>"} } });
        await Js("getSelection().removeAllRanges();WindowsReportToolbar.refresh()");
        Check(await True("document.querySelector('[data-host-command=r]').disabled && document.querySelector('[data-host-command=r]').title==='Continue in R: run an analysis into this report first'"), "with no recorded result in the report, the disabled R button's tooltip says to run an analysis first");
        await view.Browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Emulation.clearDeviceMetricsOverride", "{}");
    }
}
