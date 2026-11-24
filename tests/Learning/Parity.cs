using StatsDirect.UI;
using StatsDirect.UI.Learning;
using System.Text.Json.Nodes;

internal static partial class Program
{
    static async Task TestParity(frmMain main, LearningService service, frmSpreadsheetGear grid)
    {
        foreach (var lesson in JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Learn","lessons.json"))).AsArray())
            Check(service.TeachingContext(lesson,new JsonObject()).Length<32000,"updated lesson fits native tutor context: "+lesson.S("id"));
        const string header = "BUNDLED STATSDIRECT LESSON:\n";
        JsonObject PromptLesson(JsonObject lesson) => JsonNode.Parse(service.TeachingContext(lesson, new JsonObject(), true)[header.Length..]).AsObject();
        var small = JsonNode.Parse("""{"id":"budget-test","r":"print(1)","review":{"notes":"Lesson reviewed"},"practiceQuestions":[{"id":"budget-question","stem":"What is one?","correct":"A","review":{"notes":"Question reviewed"}}]}""").AsObject();
        Check(JsonNode.DeepEquals(PromptLesson(small), small), "a lesson inside the budget retains R and review metadata in the tutor prompt");

        var longR = small.DeepClone().AsObject();
        longR["r"] = new string('r', 10000);
        var withoutR = PromptLesson(longR);
        Check(withoutR["r"] == null && withoutR["review"] != null && withoutR["practiceQuestions"][0]["review"] != null,
            "long R is dropped first and review metadata is retained when it fits");

        var longReview = small.DeepClone().AsObject();
        longReview["review"]["notes"] = new string('l', 6000);
        longReview["practiceQuestions"][0]["review"]["notes"] = new string('q', 6000);
        string authored = longReview.ToJsonString();
        var withoutReviews = PromptLesson(longReview);
        Check(withoutReviews["r"] == null && withoutReviews["review"] == null && withoutReviews["practiceQuestions"][0]["review"] == null &&
            withoutReviews["practiceQuestions"][0].S("correct") == "A" && withoutReviews["practiceQuestions"][0].S("stem") == "What is one?" &&
            longReview.ToJsonString() == authored, "a large review is omitted only from the outgoing prompt, preserving the authored lesson and question");

        var oversized = small.DeepClone().AsObject();
        oversized["summary"] = new string('s', 10000);
        await Reject(() => { PromptLesson(oversized); return Task.CompletedTask; }, "irreducibly large lesson content is still rejected");

        var consent = new AISharingConsent();
        Check(!consent.Accepts(["A"]) && !consent.Accepts([]), "new application session has no document approval");
        int revision = consent.Revision;
        consent.Approve(["report"]);
        Check(consent.Accepts(["report"]) && !consent.Accepts(["A"]) && !consent.Accepts([]),
            "report confirmation approves exactly its named document without approving a workbook");
        consent.Approve(["A", "B"]);
        Check(consent.Accepts(["A", "B", "report"]) && consent.Revision == revision, "separate confirmations preserve explicitly approved document IDs");
        consent.Revoke();
        Check(!consent.Accepts(["A"]) && !consent.Accepts(["B"]) && !consent.Accepts(["report"]) && consent.Revision > revision,
            "explicit revocation removes all approved documents and invalidates previously captured contexts");

        var report = (frmReportWebView)main.CreateReport(); await report.LearningReport.Ready;
        string record = "{\"version\":1,\"operation\":\"Test\",\"values\":{\"hidden\":\"PRIVATE_RAW_DATA\"},\"inputs\":[],\"source\":\"C:/PRIVATE_FILE.xlsx\"}";
        string item = await report.LearningReport.AppendAsync("<h2>Visible result</h2><p>P = 0.04</p><span style='display:none'>PRIVATE_HIDDEN_TEXT</span><svg xmlns='http://www.w3.org/2000/svg' width='200' height='60'><text x='10' y='30'>Visible chart label</text><text style='display:none'>PRIVATE_HIDDEN_CHART</text></svg>", "Visible title", "Test", 0, record);
        await Task.Delay(80);
        service.RevokeAISharing();
        service.Sharing.Approve([report.Id()]);
        var context = new QuestionContext([report], true, CancellationToken.None, service.Sharing);
        await context.CaptureReportsAsync(item);
        string initial = context.InitialContext().ToJsonString();
        Check(initial.Contains("Visible result") && initial.Contains("Visible chart label") && !initial.Contains("PRIVATE_"), "Report AI shares visible text and chart labels, excluding hidden records, paths and hidden elements");
        Check(context.Tools.All(t => !t.S("name").Contains("analysis") && t.S("name") != "statsdirect_read_data"), "report-only AI has no analysis or unapproved grid tools");
        await report.LearningReport.AppendAsync("<p>Changed after Send</p>", "Later", "", 0);
        var saved = await context.CallAsync("statsdirect_read_document", new JsonObject { ["documentId"] = report.Id() });
        Check(!saved.ToJsonString().Contains("Changed after Send"), "report edits after Send do not alter that question's snapshot");
        await report.LearningReport.AppendAsync("<p>"+new string('x',19000)+"</p>","Long report","",0);
        var longContext=new QuestionContext([report],true,CancellationToken.None,service.Sharing);await longContext.CaptureReportsAsync();
        Check(longContext.InitialContext(14000).B("readDetailsWithTools") && !longContext.InitialContext(14000).ContainsKey("currentReport"),"long report uses approved tool snapshot instead of exceeding Learning's initial-context budget");
        service.RevokeAISharing();
        service.Sharing.Approve([grid.Id()]);
        service.ShowReportAI(report,item); var pane = (ScriptAIPane)Field(service,"ai");
        pane.ShareScript.Checked = true; pane.ShareWorkbook.Checked = false;
        Check(!pane.Acknowledge.Checked && !pane.Send.Enabled && pane.ShareScript.Text.Contains("report"),
            "Report AI requires confirmation for the report even when the workbook is already approved");
        pane.Acknowledge.Checked = true;
        Check(pane.Acknowledge.Checked && pane.Send.Enabled && service.Sharing.Accepts([report.Id()]),
            "Report AI can use the explicitly approved report");
        pane.NewConversation(); Check(pane.Acknowledge.Checked && pane.ShareScript.Checked, "New conversation keeps app-session acceptance and sharing choices");
        pane.Composer.Text = "Interpret this report"; await pane.SendAsync(); await pane.Transcript.SettledAsync();
        Check((await pane.Transcript.TextAsync()).Contains("Mock answer"), "Report AI completes a question through the shared native conversation transport");

        var page = pane.Transcript;
        page.Update(new JsonArray(new JsonObject { ["role"]="assistant", ["text"]="A formula: $x^2 + \\beta$.\n\n```r\nt.test(x)\n```" }), "", true);
        await page.SettledAsync();
        var math = JsonNode.Parse(await page.Browser.CoreWebView2.ExecuteScriptAsync("({math:document.querySelectorAll('.katex').length,fonts:document.fonts.check('16px KaTeX_Main'),buttons:document.querySelectorAll('[data-code=\"open\"]').length})"));
        Check(math.I("math") > 0 && math.I("buttons") == 1, "shared AI renderer displays offline TeX and R-code actions");
        page.Update(new JsonArray(), "```r\nx <- 2\n```", true); await page.SettledAsync();
        var pendingButtons = JsonNode.Parse(await page.Browser.CoreWebView2.ExecuteScriptAsync("[...document.querySelectorAll('[data-code]')].every(b=>b.disabled)"));
        Check(pendingButtons.GetValue<bool>(), "code actions remain disabled while a reply is incomplete");
        var stale = new QuestionContext([grid],true,CancellationToken.None,service.Sharing);
        service.RevokeAISharing(); await Reject(()=>stale.CallAsync("statsdirect_workspace",new()),"revoked session invalidates already captured question contexts");
        pane.Hide(); CloseTestDocument(report);
        service.ShareAIDocument = service.ShareAIWorkbook = false;
        var csharp=(frmScript)main.CreateScriptWindow();
        ((RichTextBox)Field(csharp,"rtbDoc")).Text="return 123;";
        var csharpContext=new QuestionContext([csharp],true,CancellationToken.None);
        Check(csharpContext.InitialContext().ToJsonString().Contains("CSharp")&&csharpContext.RSource.Length==0,"C# context is labelled accurately and cannot be injected as source into Open in R");
        CloseTestDocument(csharp);
    }
    static void CloseTestDocument(StatsDirectForm document)
    { typeof(StatsDirectForm).GetProperty("Dirty",Members)!.SetValue(document,false); document.Close(); }
}
