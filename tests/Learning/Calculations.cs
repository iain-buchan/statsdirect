using StatsDirect.R;
using StatsDirect.Templates;
using StatsDirect.UI;
using StatsDirect.UI.Learning;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static partial class Program
{
    static async Task TestCalculations(frmMain main, frmSpreadsheetGear grid)
    {
        var prefs = SdApplication.SoleInstance.Preferences; bool oldDefault = prefs.CanDefaultConfidenceInterval; double oldLevel = prefs.DefaultConfidenceInterval;
        prefs.CanDefaultConfidenceInterval = true; prefs.DefaultConfidenceInterval = .95;
        try
        {
            grid.LoadLearningExample(JsonNode.Parse("""
                [{"title":"PEFR Before","values":[312,242,340,388,296,254,391,402,290]},
                 {"title":"PEFR After","values":[300,201,232,312,220,256,328,330,231]}]
                """)!.AsArray());
            using var stop = new CancellationTokenSource(); var context = new QuestionContext([grid],false,stop.Token);
            var data = await context.CallAsync("statsdirect_read_data",new JsonObject { ["documentId"]=grid.Id(),["firstRow"]=2,["lastRow"]=10,["columns"]=new JsonArray(0,1) });
            JsonObject Args(string op, string id = null) => new() { ["operation"]=op,["datasetId"]=id??data.S("datasetId") };
            var request = Args("TPaired"); request["agreement"] = true;
            var first = context.CallAsync("statsdirect_run_analysis",request);
            var duplicate = context.CallAsync("statsdirect_run_analysis",(JsonObject)request.DeepClone());
            var result = await first; var second = await duplicate;
            var report = main.MdiChildren.OfType<frmReportWebView>().Single(r=>r.Id()==result.S("reportId"));
            var state = await report.LearningReport.CallAsync("snapshot");
            Check(result.S("itemId")==second.S("itemId") && state.GetProperty("entries").GetArrayLength()==1,"duplicate in-flight requests calculate and append exactly once");
            var record = (await report.LearningReport.RecordAsync(result.S("itemId"))).GetProperty("record");
            Check(Math.Abs(record.GetProperty("values").GetProperty("t").GetDouble()-4.9257744860354)<1e-11 && record.GetProperty("values").GetProperty("n").GetDouble()==9,"approved nine-pair PEFR reaches the real paired t engine");
            Check(result.B("hasSVG") && record.GetProperty("parameters").GetProperty("doAgreement").GetBoolean(),"agreement choice and SVG charts are kept with the result");
            Check(!result.ToJsonString().Contains("workbook-path") && !result.ToJsonString().Contains("\"inputs\""),"model result excludes the local run record and filesystem provenance");
            var headings = JsonNode.Parse(await report.LearningReport.Browser.CoreWebView2.ExecuteScriptAsync("[...document.querySelectorAll('.report-entry h1')].map(e=>e.textContent.trim()).join('|')"))!.GetValue<string>();
            Check(headings=="Paired t test|Two sample analysis of agreement","learning report keeps its two engine section headings without a duplicate host title");
            RRecipes.Folder=Path.Combine(AppContext.BaseDirectory,"R");
            var script=RScriptWriter.Write(record,"PEFR",FrameReader.Read);
            Check(script.HasRecipe&&!script.DataUnavailable&&script.Text.Contains("312")&&script.Text.Contains("agreement",StringComparison.OrdinalIgnoreCase),"Continue in R resolves the actual sheet values and agreement recipe");
            File.WriteAllText(Path.Combine(artifacts,"learning-pefr.R"),script.Text);
            var r=RController.PreferredRVersion();
            string output=await Task.Run(()=>RController.RunScriptCapturingOutput(script.Text,r));
            File.WriteAllText(Path.Combine(artifacts,"learning-pefr-R-output.txt"),output);
            Check(output.Contains("4.9258")&&output.Contains("0.001156")&&!output.Contains("Error"),"real installed R replays PEFR t/P and agreement without error");
            var fresh = new QuestionContext([grid],false,CancellationToken.None);
            await Reject(()=>fresh.CallAsync("statsdirect_run_analysis",request),"dataset IDs cannot be reused in a later question");
            await Reject(()=>context.CallAsync("statsdirect_run_analysis",Args("LinearRegression")),"unsupported operations require the normal analysis form");
            var injected=Args("TPaired");injected["values"]=new JsonArray(1,2);
            await Reject(()=>context.CallAsync("statsdirect_run_analysis",injected),"model supplied numeric values are rejected");
            var mistyped=Args("TPaired");mistyped["agreement"]="yes";
            await Reject(()=>context.CallAsync("statsdirect_run_analysis",mistyped),"mistyped tool settings are rejected rather than silently defaulted");
            var readonlyContext=new QuestionContext([grid],true,CancellationToken.None);
            await Reject(()=>readonlyContext.CallAsync("statsdirect_run_analysis",request),"script AI remains read-only even with an approved worksheet");
            foreach(var op in new[]{"UnivariateSummary","Wilcoxon","TUnpaired","Spearman","Agreement","Normality"})
            {var answer=await context.CallAsync("statsdirect_run_analysis",Args(op));Check(answer.S("state")=="completed","real engine + report completes "+op);}
            var one=await context.CallAsync("statsdirect_read_data",new JsonObject { ["documentId"]=grid.Id(),["firstRow"]=2,["lastRow"]=10,["columns"]=new JsonArray(0) });
            var quick=await context.CallAsync("statsdirect_run_analysis",Args("QuickSummary",one.S("datasetId")));
            Check(quick.S("state")=="completed"&&quick.S("text").Contains("Valid data"),"QuickSummary preserves its native text result and a replay record");
            await Reject(()=>context.CallAsync("statsdirect_run_analysis",Args("QuickSummary")),"invalid column dimensions are rejected, never cropped");
            var titles=await context.CallAsync("statsdirect_read_data",new JsonObject { ["documentId"]=grid.Id(),["firstRow"]=1,["lastRow"]=10,["columns"]=new JsonArray(0,1) });
            await Reject(()=>context.CallAsync("statsdirect_run_analysis",Args("TPaired",titles.S("datasetId"))),"literal headings are not silently reinterpreted as numeric missing values");
            // Make two rows missing in different columns: complete paired rows must number seven, not eight.
            var workbookView=(SpreadsheetGear.Windows.Forms.WorkbookView)Field(grid,"workbookView");
            workbookView.GetLock();try{workbookView.ActiveWorksheet.Cells[1,0].ClearContents();workbookView.ActiveWorksheet.Cells[2,1].ClearContents();}finally{workbookView.ReleaseLock();}
            var missing=new QuestionContext([grid],false,CancellationToken.None);
            var aligned=await missing.CallAsync("statsdirect_read_data",new JsonObject { ["documentId"]=grid.Id(),["firstRow"]=2,["lastRow"]=10,["columns"]=new JsonArray(0,1) });
            var paired=await missing.CallAsync("statsdirect_run_analysis",Args("TPaired",aligned.S("datasetId")));
            Check(paired["values"]["n"].GetValue<double>()==7,"paired missing values retain worksheet row alignment");
            await Reject(()=>context.CallAsync("statsdirect_run_analysis",request),"changed worksheet cannot reuse a prior cached result");
            grid.LoadLearningExample(JsonNode.Parse("""[{"title":"Present","values":[12,7]},{"title":"Absent","values":[4,17]}]""")!.AsArray());
            var tableContext=new QuestionContext([grid],false,CancellationToken.None);
            var table=await tableContext.CallAsync("statsdirect_read_data",new JsonObject { ["documentId"]=grid.Id(),["firstRow"]=2,["lastRow"]=3,["columns"]=new JsonArray(0,1) });
            foreach(var op in new[]{"Chi2by2","ExactFisher"})
            {
                await Reject(()=>tableContext.CallAsync("statsdirect_run_analysis",Args(op,table.S("datasetId"))),op+" requires explicit study design");
                var a=Args(op,table.S("datasetId"));a["studyType"]="cohort";var answer=await tableContext.CallAsync("statsdirect_run_analysis",a);
                var saved=(await report.LearningReport.RecordAsync(answer.S("itemId"))).GetProperty("record");
                var counts=saved.GetProperty("parameters").GetProperty("counts");
                Check(answer.S("state")=="completed"&&counts[0][0].GetDouble()==12&&counts[0][1].GetDouble()==4&&counts[1][0].GetDouble()==7,op+" keeps row/column orientation and real engine output");
            }
            var canceled=new QuestionContext([grid],false,stop.Token);
            var canceledData=await canceled.CallAsync("statsdirect_read_data",new JsonObject { ["documentId"]=grid.Id(),["firstRow"]=2,["lastRow"]=3,["columns"]=new JsonArray(0,1) });
            int before=(await report.LearningReport.CallAsync("snapshot")).GetProperty("entries").GetArrayLength();
            var pending=canceled.CallAsync("statsdirect_run_analysis",Args("TPaired",canceledData.S("datasetId")));stop.Cancel();
            await Reject(()=>pending,"cancellation while a calculation is pending prevents publication");
            Check((await report.LearningReport.CallAsync("snapshot")).GetProperty("entries").GetArrayLength()==before&&!main.LearningCalculationRunning,"cancelled calculation adds no result and releases the engine");
            var changing=new QuestionContext([grid],false,CancellationToken.None);
            var changingData=await changing.CallAsync("statsdirect_read_data",new JsonObject { ["documentId"]=grid.Id(),["firstRow"]=2,["lastRow"]=3,["columns"]=new JsonArray(0,1) });
            var stale=changing.CallAsync("statsdirect_run_analysis",Args("TUnpaired",changingData.S("datasetId")));
            workbookView.GetLock();try{workbookView.ActiveWorksheet.Cells[1,0].Value=13d;}finally{workbookView.ReleaseLock();}
            await Reject(()=>stale,"a worksheet edit while calculation is pending is rechecked before publication");
            Check((await report.LearningReport.CallAsync("snapshot")).GetProperty("entries").GetArrayLength()==before,"stale in-flight calculation leaves the report unchanged");
            await Reject(()=>report.AppendLearningResultAsync("<p>Should not appear</p>","TPaired",record.GetRawText(),()=>throw new OperationCanceledException()),"consent cancellation at the final queued append boundary rejects publication");
            string next=await report.AppendLearningResultAsync("<p>Later approved append</p>","TPaired",record.GetRawText(),()=>{});
            Check(next.Length>0,"a cancelled queued append does not poison subsequent report edits");
            foreach(var flag in new[]{"hasErrors","hasStaleFormulas","complete"})
            {var bad=(JsonObject)table.DeepClone();bad[flag]=flag!="complete";await Reject(()=>{new TutorCalculation("TPaired",bad,"book",false,"",SdApplication.SoleInstance,CancellationToken.None).Run();return Task.CompletedTask;},flag+" range status is enforced before execution");}
        }
        finally { prefs.CanDefaultConfidenceInterval=oldDefault;prefs.DefaultConfidenceInterval=oldLevel; }
    }
}
