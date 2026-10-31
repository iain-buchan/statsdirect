using StatsDirect.R;
using StatsDirect.UI;
using StatsDirect.UI.Learning;
using System.Diagnostics;
using System.Text.Json.Nodes;

internal static partial class Program
{
    static async Task TestConversationR(frmMain main, LearningService service, frmSpreadsheetGear grid)
    {
        const string source = "before <- c(312,242,340,388,296,254,391,402,290)\nafter <- c(300,201,232,312,220,256,328,330,231)\n";
        var original = service.OpenScript(source, "PEFR source", false);
        service.ShowScriptAI(original);
        var pane = (ScriptAIPane)Field(service,"ai");
        pane.ShareScript.Checked = true; pane.Acknowledge.Checked = true; pane.Composer.Text = "PEFR_CONTEXT";
        await pane.SendAsync(); await pane.Transcript.SettledAsync();
        ((RichTextBox)Field(original,"rtbDoc")).Text = "before <- after <- 999\n";
        var paneView = (Control)Field(pane,"view");
        IEnumerable<Control> Controls(Control c) => c.Controls.Cast<Control>().SelectMany(x => new[]{x}.Concat(Controls(x)));
        var open = Controls(paneView).OfType<Button>().Single(b=>b.Text=="Open R code");
        int count=service.Documents.OfType<frmScript>().Count(); open.PerformClick();
        await Wait(()=>service.Documents.OfType<frmScript>().Count()==count+1,"bottom Open R code creates a reviewable script");
        var exported=(frmScript)main.ActiveMdiChild;
        Check(exported.LearningScript.Contains(source)&&!exported.LearningScript.Contains("999"),"AI export uses the original shared snapshot after the source script is edited");
        Check(!exported.LearningRunning&&exported.LearningOutput.Length==0,"opening suggested R code does not run it");
        await RunConversationR(exported.LearningScript,"ai-pefr-open", "stopifnot(length(before)==9, length(after)==9, abs(unname(t.test(before, after, paired=TRUE)$statistic)-4.9257744860354)<1e-11, identical(d, before-after))\ncat('PEFR_CONTEXT_OK\\n')", "PEFR_CONTEXT_OK");
        Check(((RichTextBox)Field(original,"rtbDoc")).Text=="before <- after <- 999\n","AI export leaves the user's source edits unchanged");

        // A later block requires d from the first block in that same reply.
        service.ShowScriptAI(original); await pane.Transcript.SettledAsync();
        count=service.Documents.OfType<frmScript>().Count();
        await pane.Transcript.Browser.CoreWebView2.ExecuteScriptAsync("document.querySelectorAll('[data-code=open]')[1].click()");
        await Wait(()=>service.Documents.OfType<frmScript>().Count()==count+1,"per-block Open in R opens the selected reply with its context");
        var blockExport=(frmScript)main.ActiveMdiChild;
        Check(blockExport.LearningScript.Contains(source)&&blockExport.LearningScript.IndexOf("d <- before - after")<blockExport.LearningScript.IndexOf("t.test(before"),"per-block export includes preceding definitions in their original order");

        // Question-only follow-ups can refer to earlier shared data without sharing current edits.
        service.ShowScriptAI(original); pane.ShareScript.Checked=false; pane.Composer.Text="PEFR_FOLLOWUP";
        await pane.SendAsync(); await pane.Transcript.SettledAsync();
        open.PerformClick(); await Task.Delay(100);
        var followup=(frmScript)main.ActiveMdiChild;
        Check(followup.LearningScript.Contains(source)&&!followup.LearningScript.Contains("999"),"a question-only follow-up retains earlier data without reading unshared source edits");
        await RunConversationR(followup.LearningScript,"ai-pefr-followup", "cat('FOLLOWUP_CONTEXT_OK\\n')", "FOLLOWUP_CONTEXT_OK");
        service.ShowScriptAI(original); await pane.Transcript.SettledAsync();
        await pane.Transcript.Browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-code=open]').click()"); await Task.Delay(100);
        var earlier=(frmScript)main.ActiveMdiChild;
        Check(earlier.LearningScript.Contains(source)&&!earlier.LearningScript.Contains("stopifnot(length(d)"),"an older reply keeps its own context and excludes later replies");

        service.ShowScriptAI(original); pane.NewConversation(); pane.Composer.Text="A new unrelated example";
        await pane.SendAsync(); await pane.Transcript.SettledAsync(); open.PerformClick(); await Task.Delay(100);
        Check(!((frmScript)main.ActiveMdiChild).LearningScript.Contains("before <-"),"a new conversation does not inherit old or unshared data");
        pane.Hide();

        grid.LoadLearningExample(JsonNode.Parse("""[{"title":"Before","values":[312,242,340]},{"title":"After","values":[300,201,232]}]""")!.AsArray());
        var context=new QuestionContext([grid],true,CancellationToken.None);
        var data=await context.CallAsync("statsdirect_read_data",new JsonObject{["documentId"]=grid.Id(),["firstRow"]=2,["lastRow"]=4,["columns"]=new JsonArray(0,1)});
        string datasetId=RScriptWriter.Quote(data.S("datasetId"));
        string aliases=$"before <- .statsdirect_data[[{datasetId}]][['column_0']]\nafter <- .statsdirect_data[[{datasetId}]][['column_1']]\n";
        var messages=new JsonArray(new JsonObject{["role"]="assistant",["rData"]=context.RData,["text"]="```r\n"+aliases+"d <- before - after\n```"});
        grid.LoadLearningExample(JsonNode.Parse("""[{"title":"Replaced","values":[999]}]""")!.AsArray());
        Check(data.S("rCode")==context.RData,"worksheet tools expose the same R bindings used by the local export");
        await RunConversationR(ConversationRScript.Build(messages,0),"ai-workbook-snapshot","stopifnot(identical(before,c(312,242,340)),identical(after,c(300,201,232)))\ncat('WORKBOOK_CONTEXT_OK\\n')","WORKBOOK_CONTEXT_OK");
        var literal=new JsonObject{["datasetId"]="escaped",["columns"]=new JsonArray(new JsonObject{["index"]=3,["values"]=new JsonArray("quoted\"\\\ntext",null,"plain")},new JsonObject{["index"]=7,["values"]=new JsonArray(2,null,4)})};
        await RunConversationR(ConversationRScript.Dataset(literal),"ai-literal-data","stopifnot(identical(.statsdirect_data[['escaped']][['column_3']],c(\"quoted\\\"\\\\\\ntext\",NA,\"plain\")),identical(.statsdirect_data[['escaped']][['column_7']],c(2,NA,4)))\ncat('LITERAL_CONTEXT_OK\\n')","LITERAL_CONTEXT_OK");
        Check(ConversationRScript.Blocks("```python\nno_r()\n```\n~~~RSCRIPT\nx <- 1\n~~~\n```r\nincomplete <-").SequenceEqual(new[]{"x <- 1"}),"R extraction accepts Rscript and tilde fences but excludes other languages and unfinished blocks");
        Check(ConversationRScript.Build(new JsonArray(new JsonObject{["role"]="user",["rSource"]=source,["text"]="```r\nmean(before)\n```"}),0,0).Contains(source),"R blocks in the user's own question still open with their source context");
    }
    static async Task RunConversationR(string code,string name,string assertions,string marker)
    {
        string folder=Path.Combine(artifacts,name);Directory.CreateDirectory(folder);
        string path=Path.Combine(folder,"check.R");File.WriteAllText(path,code+"\n"+assertions);
        string executable=Environment.GetEnvironmentVariable("STATSDIRECT_RSCRIPT")??Path.Combine(StatsDirect.R.RController.PreferredRVersion()?.BinPath??@"C:\Program Files\R\R-none\bin\x64","Rscript.exe");
        var start=new ProcessStartInfo(executable){WorkingDirectory=folder,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        start.ArgumentList.Add("--vanilla");start.ArgumentList.Add(path);
        using var process=Process.Start(start)!;
        Task<string> stdout=process.StandardOutput.ReadToEndAsync(),stderr=process.StandardError.ReadToEndAsync();
        try{await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(45));}catch{process.Kill(true);throw;}
        string output=await stdout+await stderr;File.WriteAllText(Path.Combine(folder,"output.txt"),output);
        Check(process.ExitCode==0&&output.Contains(marker)&&!output.Contains("Error"),name+": real R executes the exported script and validates the original data ("+output.Trim().TakeLast(100).Aggregate("",(s,c)=>s+c)+")");
    }
}
