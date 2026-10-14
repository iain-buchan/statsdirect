using Microsoft.Web.WebView2.Core;
using StatsDirect.UI;
using StatsDirect.UI.Learning;
using System.Text.Json.Nodes;

internal static partial class Program
{
    static async Task CrashLearning(LearningView view)
    {
        var failed=new TaskCompletionSource();
        view.Browser.CoreWebView2.ProcessFailed+=(_,_)=>failed.TrySetResult();
        try { await view.Browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Page.crash","{}").WaitAsync(TimeSpan.FromSeconds(2)); } catch { }
        await failed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await Wait(()=>view.NeedsRecovery,"Learning detects the isolated renderer crash");
    }
    static async Task RecoverLearning(LearningView view)
    {
        var old=view.Browser;
        await view.RecoverAsync();await view.Ready.Task.WaitAsync(TimeSpan.FromSeconds(30));await Task.Delay(120);
        Check(old.IsDisposed&&old!=view.Browser&&!view.NeedsRecovery,"Learning recovery replaces the failed WebView");
    }
    static async Task<LearningView> TestLearningRecovery(frmMain main, LearningService service, LearningView view)
    {
        view.Activate();view.SourceId="__none__";service.RefreshWorkspace();
        await LearningAsk(service,view,"Recovery fixture");
        await JS(view,"document.getElementById('question').value='Keep my draft';document.getElementById('question').dispatchEvent(new Event('input'))");
        await Wait(()=>service.State.S("draft")=="Keep my draft","draft is retained natively before the crash");
        string generation=view.Generation;
        await CrashLearning(view);await RecoverLearning(view);
        Check((await JS(view,"document.getElementById('question').value")).GetValue<string>()=="Keep my draft"&&(await JS(view,"document.getElementById('messages').textContent.includes('Mock answer')")).GetValue<bool>(),"recovery restores saved conversation and draft");
        Check(view.Generation!=generation&&!view.Browser.CoreWebView2.Settings.AreHostObjectsAllowed&&!view.Browser.CoreWebView2.Settings.AreDevToolsEnabled,"recovery creates a fresh restricted bridge generation");
        Check((await JS(view,"document.querySelector('meta[http-equiv=Content-Security-Policy]').content.includes(\"connect-src 'none'\")")).GetValue<bool>(),"recovered Learning page retains its network restrictions");
        int scripts=service.Documents.OfType<frmScript>().Count();
        await JS(view,"document.querySelector('[data-code=r]').click()");
        await Wait(()=>service.Documents.OfType<frmScript>().Count()==scripts+1,"recovered R link invokes the host once");
        view.Activate();
        await JS(view,"document.getElementById('question').value='COPY_SELECTION_CHECK';document.getElementById('send').click()");
        await Wait(()=>Field(service,"requestId") is string,"a slow mock question is pending before renderer failure");
        string id=(string)Field(service,"requestId");object request=Field(service,"question");
        await view.CallAsync("partial",new JsonObject{["id"]=id,["text"]="Initial streamed reply"});await Task.Delay(100);
        await CrashLearning(view);
        await view.CallAsync("partial",new JsonObject{["id"]=id,["text"]="Latest partial received while unavailable"});
        await RecoverLearning(view);
        Check((await JS(view,"document.getElementById('streamedReply').textContent.includes('Latest partial received while unavailable')")).GetValue<bool>()&&ReferenceEquals(Field(service,"question"),request),"recovery replays the latest partial without resending the question");
        Check((await JS(view,"document.getElementById('send').disabled && !!document.getElementById('stop')")).GetValue<bool>(),"the recovered pending question keeps Send disabled and Stop available");
        await CrashLearning(view);
        await view.CallAsync("reply",new JsonObject{["id"]=id,["text"]="Completed while unavailable\n```r\nrecovered <- 42\n```",["model"]="mock"});
        await RecoverLearning(view);
        Check(service.State.A("conversation").Count(c=>c.S("id")==id)==1&&service.State.A("conversation").Any(c=>c.S("text").Contains("Completed while unavailable")),"completion while unavailable is replayed and saved exactly once");
        Check(service.LearningRScript(id,service.State.S("lesson"),0).Contains("recovered <- 42"),"completed reply keeps its R bridge after recovery");
        service.CancelQuestion();await Wait(()=>Field(service,"question")==null,"the original mock question cancels after replay testing");
        await Task.Delay(100);
        await JS(view,"document.getElementById('question').value='COPY_SELECTION_CHECK';document.getElementById('send').click()");
        await Wait(()=>Field(service,"requestId") is string,"another pending reply starts before close recovery");
        string closingId=(string)Field(service,"requestId");
        await view.CallAsync("partial",new JsonObject{["id"]=closingId,["text"]="Keep this received text on closing"});await Task.Delay(100);
        await CrashLearning(view);view.Close();
        await Wait(()=>Field(service,"question")==null,"closing the failed Learning window cancels further sharing");
        service.Show(false);view=service.View;await view.Ready.Task.WaitAsync(TimeSpan.FromSeconds(30));await Task.Delay(120);
        Check(service.State.A("conversation").Count(c=>c.S("id")==closingId&&c.S("text").Contains("Keep this received text on closing")&&c.S("text").Contains("stopped before completion"))==1,"closing a failed window preserves the received partial once, explicitly marked incomplete");
        Check(view.SourceId=="__none__","reopening does not restore a document-sharing grant");
        await LearningAsk(service,view,"After renderer recovery");
        Check(service.State.A("conversation").Last().S("text").Contains("Mock answer"),"normal mocked conversation resumes after recovery");
        return view;
    }
}
