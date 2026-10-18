using StatsDirect.UI;
using StatsDirect.UI.Learning;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static partial class Program
{
    // Workbook sharing is one explicit choice covering every open workbook. Titles
    // are labels; distinct document/dataset IDs identify each read and R snapshot.
    static async Task TestReportWorkbookDefault(frmMain main, LearningService service, frmSpreadsheetGear grid)
    {
        var a = (frmSpreadsheetGear)main.CreateGrid("Shared duplicate title");
        var b = (frmSpreadsheetGear)main.CreateGrid("Shared duplicate title");
        b.Text = a.Text;
        a.LoadLearningExample(new JsonArray(new JsonObject { ["title"] = "Measurement", ["values"] = new JsonArray(101, 102) }));
        b.LoadLearningExample(new JsonArray(new JsonObject { ["title"] = "Measurement", ["values"] = new JsonArray(201, 202) }));
        string aId = a.Id(), bId = b.Id();
        var report = (frmReportWebView)main.CreateReport(); await report.LearningReport.Ready;
        string aPath = ((IGrid)a).WorkbookPath;
        string record = JsonSerializer.Serialize(new { version = 1, operation = "Test", inputs = new[] { new { name = "x", mode = "NumericReplaceMissing", file = Path.GetFileName(aPath), path = aPath, sheet = "Sheet1", columns = new object[0] } }, source = new { file = Path.GetFileName(aPath), unsaved = true } });
        string fromA = await report.LearningReport.AppendAsync("<h2>Result from A</h2><p>P = 0.04</p>", "Result from A", "Test", 0, record);
        string note = await report.LearningReport.AppendAsync("<h2>A note</h2><p>No run.</p>", "A note", "", 0);
        service.ShowReportAI(report, fromA);
        var pane = (ScriptAIPane)Field(service, "ai");
        pane.ShareScript.Checked = false; pane.ShareWorkbook.Checked = false;
        service.RevokeAISharing(); pane.NewConversation(); service.RefreshWorkspace();
        frmSpreadsheetGear fresh = null;
        try
        {
            IEnumerable<Control> Descendants(Control control) => control.Controls.Cast<Control>().SelectMany(child => new[] { child }.Concat(Descendants(child)));
            string[] OpenIds() => service.Documents.OfType<frmSpreadsheetGear>().Select(book => book.Id()).OrderBy(id => id).ToArray();
            static string[] Ids(JsonNode overview) => overview.A("documents").Select(doc => doc.S("documentId")).OrderBy(id => id).ToArray();
            JsonObject Read(string id) => new() { ["documentId"] = id, ["firstRow"] = 2, ["lastRow"] = 3, ["columns"] = new JsonArray(0) };
            var paneView = (Control)Field(pane, "view");
            Check(pane.ShareWorkbook.Text == "Share open workbook data" && !Descendants(paneView).OfType<ComboBox>().Any(),
                "AI has one Share open workbook data choice and no workbook selector");
            Check(aId != bId && main.MdiChildren.Count(c => c.Text == a.Text) == 2, "same-title workbooks retain different document identities");
            var unshared = new QuestionContext(pane.SharedWorkbooks, true, CancellationToken.None, service.Sharing);
            Check(pane.SharedWorkbooks.Length == 0 && Ids(unshared.Overview()).Length == 0 && unshared.Tools.All(tool => tool.S("name") != "statsdirect_read_data"),
                "unchecked workbook sharing exposes no workbook names, IDs, values or data-reading tool");
            await Reject(() => unshared.CallAsync("statsdirect_read_data", Read(aId)), "a known open-workbook ID cannot bypass unchecked sharing");

            pane.ShareWorkbook.Checked = true;
            var approvedIds = OpenIds();
            Check(approvedIds.Contains(aId) && approvedIds.Contains(bId) && pane.SharedWorkbooks.Select(book => book.Id()).OrderBy(id => id).SequenceEqual(approvedIds),
                "workbook sharing covers every open workbook, independently of the report's original workbook");
            Check(!pane.Acknowledge.Checked && !pane.Send.Enabled && !service.Sharing.Accepts(approvedIds),
                "checking Share open workbook data still requires explicit confirmation");
            pane.Composer.Text = "Do not send before confirmation"; await pane.SendAsync();
            Check(((Label)Field(pane, "status")).Text.Contains("confirm sharing"), "unconfirmed all-workbook sharing is blocked before tutor access");
            pane.Acknowledge.Checked = true;
            await Wait(() => service.Sharing.Accepts(approvedIds) && pane.Send.Enabled, "confirmation approves exactly the currently open workbook set");
            var context = new QuestionContext(pane.SharedWorkbooks, true, CancellationToken.None, service.Sharing);
            Check(Ids(context.Overview()).SequenceEqual(approvedIds) && context.Tools.All(tool => tool.S("name") != "statsdirect_read_document"),
                "all-workbook context retains every workbook ID without also sharing the displayed report");
            var aData = await context.CallAsync("statsdirect_read_data", Read(aId));
            var bData = await context.CallAsync("statsdirect_read_data", Read(bId));
            Check(aData.S("documentId") == aId && bData.S("documentId") == bId && aData.S("workbook") == a.Text && bData.S("workbook") == b.Text
                && aData.A("columns")[0].A("values")[0].GetValue<double>() == 101 && bData.A("columns")[0].A("values")[0].GetValue<double>() == 201,
                "same-title workbook reads retain their actual identities and literal values");
            Check(aData.S("datasetId") != bData.S("datasetId") && context.RData.Contains(aId) && context.RData.Contains(bId)
                && context.RData.Contains(aData.S("datasetId")) && context.RData.Contains(bData.S("datasetId")),
                "local R snapshots retain separate dataset and workbook identities for both selected workbooks");

            // The mock requests the scope and reads one literal cell from every grid,
            // exercising the real Send path rather than only constructing contexts here.
            pane.Composer.Text = "ALL_OPEN_WORKBOOKS"; await pane.SendAsync(); await pane.Transcript.SettledAsync();
            var messages = (JsonArray)Field(Field(pane, "chat"), "Messages");
            var lastReply = messages.Last(message => message.S("role") == "assistant");
            var received = JsonNode.Parse(lastReply.S("text"));
            Check(received.A("scopeIds").Select(id => id.GetValue<string>()).OrderBy(id => id).SequenceEqual(approvedIds)
                && received.A("readIds").Select(id => id.GetValue<string>()).OrderBy(id => id).SequenceEqual(approvedIds)
                && received.A("datasetIds").Select(id => id.GetValue<string>()).Distinct().Count() == approvedIds.Length,
                "the real AI Send path offers and reads every approved workbook with distinct dataset IDs");
            int aIndex = received.A("readIds").Select(id => id.GetValue<string>()).ToList().IndexOf(aId);
            int bIndex = received.A("readIds").Select(id => id.GetValue<string>()).ToList().IndexOf(bId);
            Check(received.A("titles")[aIndex].GetValue<string>() == a.Text && received.A("titles")[bIndex].GetValue<string>() == b.Text
                && received.A("values")[aIndex].GetValue<double>() == 101 && received.A("values")[bIndex].GetValue<double>() == 201,
                "the tutor receives each duplicate-title workbook's own values and labels");
            string frozenRData = lastReply.S("rData");
            a.LoadLearningExample(new JsonArray(new JsonObject { ["title"] = "Replacement", ["values"] = new JsonArray(999) }));
            var exported = ConversationRScript.Build(new JsonArray(new JsonObject { ["role"] = "assistant", ["rData"] = frozenRData, ["text"] = "```r\ninvisible(.statsdirect_data)\n```" }), 0);
            Check(exported.Contains(aId) && exported.Contains(bId) && exported.Contains(frozenRData) && exported.Contains("101") && !exported.Contains("999"),
                "Open in R retains original literal snapshots and workbook identities after one workbook is edited");
            await Reject(() => context.CallAsync("statsdirect_read_data", Read(aId)), "a question captured before a worksheet edit cannot reread changed values");
            service.ShowReportAI(report, note); b.Activate(); a.Activate(); service.RefreshWorkspace();
            Check(pane.Acknowledge.Checked && service.Sharing.Accepts(approvedIds), "switching active workbooks or report results does not narrow or revoke an unchanged approved set");

            var beforeOpen = new QuestionContext(pane.SharedWorkbooks, true, CancellationToken.None, service.Sharing);
            int revision = service.Sharing.Revision;
            fresh = (frmSpreadsheetGear)main.CreateGrid("Newly opened workbook");
            fresh.LoadLearningExample(new JsonArray(new JsonObject { ["title"] = "New data", ["values"] = new JsonArray(301) }));
            service.RefreshWorkspace();
            await Wait(() => !pane.Acknowledge.Checked && !pane.Send.Enabled, "opening another workbook requires confirmation of the enlarged sharing set");
            Check(service.Sharing.Revision > revision && !service.Sharing.Accepts(OpenIds()), "a newly opened workbook cannot silently inherit the earlier approval");
            await Reject(() => beforeOpen.CallAsync("statsdirect_workspace", new()), "opening a workbook invalidates an already captured all-workbook question");
            await Reject(() => beforeOpen.CallAsync("statsdirect_read_data", Read(fresh.Id())), "the new workbook ID cannot be read under the earlier question's approval");
            pane.Acknowledge.Checked = true;
            var enlarged = new QuestionContext(pane.SharedWorkbooks, true, CancellationToken.None, service.Sharing);
            var freshData = await enlarged.CallAsync("statsdirect_read_data", new JsonObject { ["documentId"] = fresh.Id(), ["firstRow"] = 2, ["lastRow"] = 2, ["columns"] = new JsonArray(0) });
            Check(Ids(enlarged.Overview()).SequenceEqual(OpenIds()) && freshData.S("documentId") == fresh.Id()
                && freshData.A("columns")[0].A("values")[0].GetValue<double>() == 301, "fresh confirmation allows the new workbook with its correct identity and values");

            CloseTestDocument(b); service.RefreshWorkspace();
            await Wait(() => !pane.Acknowledge.Checked, "closing a shared workbook refreshes the approved workbook set");
            await Reject(() => enlarged.CallAsync("statsdirect_read_data", Read(bId)), "a closed workbook cannot be read from its stale question context");
            string aDataset = StatsDirect.R.RScriptWriter.Quote(received.A("datasetIds")[aIndex].GetValue<string>());
            string bDataset = StatsDirect.R.RScriptWriter.Quote(received.A("datasetIds")[bIndex].GetValue<string>());
            string snapshotAssertions = $"a <- .statsdirect_data[[{aDataset}]]\nb <- .statsdirect_data[[{bDataset}]]\n" +
                $"stopifnot(identical(a[['column_0']],101),identical(b[['column_0']],201)," +
                $"identical(attr(a,'statsdirect')$documentId,{StatsDirect.R.RScriptWriter.Quote(aId)})," +
                $"identical(attr(b,'statsdirect')$documentId,{StatsDirect.R.RScriptWriter.Quote(bId)})," +
                $"identical(attr(a,'statsdirect')$workbook,{StatsDirect.R.RScriptWriter.Quote(received.A("titles")[aIndex].GetValue<string>())})," +
                $"identical(attr(b,'statsdirect')$workbook,{StatsDirect.R.RScriptWriter.Quote(received.A("titles")[bIndex].GetValue<string>())}))\ncat('ALL_OPEN_CONTEXT_OK\\n')";
            await RunConversationR(exported, "ai-all-open-workbooks", snapshotAssertions, "ALL_OPEN_CONTEXT_OK");
            pane.Acknowledge.Checked = true;
            var remaining = new QuestionContext(pane.SharedWorkbooks, true, CancellationToken.None, service.Sharing);
            Check(!Ids(remaining.Overview()).Contains(bId), "a closed workbook's ID is absent from subsequent context metadata");
            await Reject(() => remaining.CallAsync("statsdirect_read_data", Read(bId)), "knowing a closed workbook ID cannot reopen its data in a later question");
        }
        finally
        {
            pane.ShareScript.Checked = false; pane.ShareWorkbook.Checked = false;
            if (!a.IsDisposed) CloseTestDocument(a);
            if (!b.IsDisposed) CloseTestDocument(b);
            if (fresh?.IsDisposed == false) CloseTestDocument(fresh);
            grid.Activate(); service.RefreshWorkspace();
            service.ShowReportAI(report, note); pane.Composer.Clear();
        }
    }
}
