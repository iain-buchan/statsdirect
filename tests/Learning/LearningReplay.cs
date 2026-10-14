using StatsDirect.UI;
using StatsDirect.UI.Learning;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static partial class Program
{
    static async Task LearningAsk(LearningService service, LearningView view, string question)
    {
        int before=service.State.A("conversation").Count;
        await JS(view,"document.getElementById('question').value="+JsonSerializer.Serialize(question)+";document.getElementById('send').click()");
        await Wait(()=>Field(service,"requestId")==null&&service.State.A("conversation").Skip(before).Any(c=>c.S("role")=="assistant"),"Learning reply completes: "+question);
        await Task.Delay(100);
    }
    static async Task<LearningView> TestLearningReplay(frmMain main, LearningService service, LearningView view, frmSpreadsheetGear grid)
    {
        var fresh=(JsonObject)service.State.DeepClone();fresh["id"]=Guid.NewGuid().ToString();fresh["conversation"]=new JsonArray();fresh["lesson"]="precision";fresh["view"]="study";
        await view.CallAsync("restore",fresh);await Task.Delay(100);
        const string source="before <- c(312,242,340,388,296,254,391,402,290)\nafter <- c(300,201,232,312,220,256,328,330,231)\n";
        string fullSource=source+string.Concat(Enumerable.Repeat("# a source comment longer than the AI excerpt\n",290))+"local_marker <- 'LOCAL_REPLAY_ONLY'\n";
        var original=service.OpenScript(fullSource,"Learning source",false);
        service.ConfirmSharing=_=>LearningService.SharingDecision.Share;
        view.Activate();view.SourceId=original.Id();service.RefreshWorkspace();
        await LearningAsk(service,view,"PEFR_CONTEXT");
        string replyId=service.State.A("conversation").Last(c=>c.S("role")=="assistant").S("id");
        ((RichTextBox)Field(original,"rtbDoc")).Text="before <- after <- 999";
        async Task<frmScript> Open(string id,int block)
        {
            string nativeCode=service.LearningRScript(id,"precision",block);
            Check(nativeCode.Length>0,"native Learning replay resolves the selected message and block");
            int count=service.Documents.OfType<frmScript>().Count();
            await JS(view,"Array.from(document.querySelectorAll('[data-message-id]')).find(e=>e.dataset.messageId==="+JsonSerializer.Serialize(id)+").querySelectorAll('[data-code=r]')["+block+"].click()");
            await Wait(()=>service.Documents.OfType<frmScript>().Count()==count+1,"Learning Open in R opens one script");
            return (frmScript)main.ActiveMdiChild;
        }
        var exported=await Open(replyId,1);
        Check(exported.LearningScript.Contains(fullSource)&&!exported.LearningScript.Contains("999"),"Learning export keeps the full approved source snapshot after edits, beyond the AI excerpt limit");
        Check(!exported.LearningRunning&&exported.LearningOutput.Length==0,"Learning Open in R never executes suggested code");
        await RunConversationR(exported.LearningScript,"learning-pefr","stopifnot(length(before)==9,identical(d,before-after),abs(unname(t.test(before,after,paired=TRUE)$statistic)-4.9257744860354)<1e-11)\ncat('LEARNING_PEFR_OK\\n')","LEARNING_PEFR_OK");
        var first=await Open(replyId,0);
        Check(first.LearningScript.Contains("d <- before - after")&&!first.LearningScript.Contains("t.test(before"),"Learning first block excludes later blocks in that reply");
        view.Activate();view.SourceId="__none__";service.RefreshWorkspace();
        await LearningAsk(service,view,"PEFR_FOLLOWUP");
        string followId=service.State.A("conversation").Last().S("id");
        var follow=await Open(followId,0);
        await RunConversationR(follow.LearningScript,"learning-followup","cat('LEARNING_FOLLOWUP_OK\\n')","LEARNING_FOLLOWUP_OK");
        var older=await Open(replyId,0);
        Check(!older.LearningScript.Contains("stopifnot(length(d)"),"Learning old replies exclude subsequent conversation code");
        view.Activate();await LearningAsk(service,view,"LOCAL_CONTEXT_PRIVACY");
        Check(service.State.A("conversation").Last().S("text")=="LOCAL_METADATA_EXCLUDED","local replay snapshots are excluded from later AI requests");
        var record=await JS(view,"(()=>{let result;const p=window.webkit.messageHandlers.statsDirectLearn,old=p.postMessage;p.postMessage=m=>result=m;window.statsDirectLearn.exportRecord();p.postMessage=old;return result})()");
        Check(!record.ToJsonString().Contains("LOCAL_REPLAY_ONLY")&&!record.ToJsonString().Contains("rContexts"),"learning-record exports exclude local R snapshot metadata");
        Check(File.ReadAllText(Path.Combine(service.Storage,"portfolio.json")).Contains("LOCAL_REPLAY_ONLY"),"approved R context persists locally for reopening");
        string oldGeneration=view.Generation;
        view.Close();await Task.Delay(100);service.Show(false);view=service.View;await view.Ready.Task.WaitAsync(TimeSpan.FromSeconds(30));await Task.Delay(100);
        Check(service.LearningRScript(replyId,"precision",0).Contains(fullSource),"Learning R source survives closing and reopening the portfolio");
        int scripts=service.Documents.OfType<frmScript>().Count();
        await service.HandleAsync(view,new JsonObject{["action"]="openRCode",["generation"]=oldGeneration,["lessonID"]="precision",["messageID"]=replyId,["blockIndex"]=0});
        Check(service.Documents.OfType<frmScript>().Count()==scripts,"R clicks from a replaced Learning browser are rejected");
        await JS(view,"document.querySelector('[data-lesson=epidemiology]').click()");await Task.Delay(80);
        await service.HandleAsync(view,new JsonObject{["action"]="openRCode",["generation"]=view.Generation,["lessonID"]="precision",["messageID"]=replyId,["blockIndex"]=0});
        Check(service.Documents.OfType<frmScript>().Count()==scripts,"R clicks from a previous lesson are rejected");
        await LearningAsk(service,view,"An unrelated lesson");
        string otherId=service.State.A("conversation").Last().S("id");
        Check(!service.LearningRScript(otherId,"epidemiology",0).Contains("before <-"),"R replay is isolated to the selected lesson");
        // Exercise a real dynamic worksheet read, including blank rows and quoted strings.
        grid.LoadLearningExample(JsonNode.Parse("""[{"title":"Values","values":[2,0,4,6]},{"title":"Labels","values":[0,0,0,0]}]""")!.AsArray());
        var workbook=(SpreadsheetGear.Windows.Forms.WorkbookView)Field(grid,"workbookView");
        workbook.GetLock();
        try { var cells=workbook.ActiveWorksheet.Cells;cells[1,1].Value="quoted\"\\text";cells[2,0].Clear();cells[2,1].Clear();cells[3,1].Value="plain";cells[4,1].Value="end"; }
        finally { workbook.ReleaseLock(); }
        view.SourceId=grid.Id();service.RefreshWorkspace();await LearningAsk(service,view,"LEARN_WORKSHEET");
        string dataId=service.State.A("conversation").Last().S("id");
        var contexts=(JsonObject)service.State["rContexts"];
        Check(contexts[dataId].S("rData").Contains("column_0"),"Learning worksheet tool supplies and retains explicit R column bindings");
        grid.LoadLearningExample(JsonNode.Parse("""[{"title":"Changed","values":[999]}]""")!.AsArray());
        string code=service.LearningRScript(dataId,"epidemiology",0);
        await RunConversationR(code,"learning-worksheet","v <- .statsdirect_data[[length(.statsdirect_data)]]\nstopifnot(identical(v$column_0,c(2,NA,4,6)),identical(v$column_1,c('quoted\"\\\\text',NA,'plain','end')))\ncat('LEARNING_WORKSHEET_OK\\n')","LEARNING_WORKSHEET_OK");
        var reset=(JsonObject)service.State.DeepClone();reset["id"]=Guid.NewGuid().ToString();reset["conversation"]=new JsonArray();
        await view.CallAsync("restore",reset);await Task.Delay(100);
        Check(((JsonObject)service.State["rContexts"]).Count==0,"a new Learning portfolio cannot inherit old replay snapshots");
        view.SourceId="__none__";service.RefreshWorkspace();
        return view;
    }
}
