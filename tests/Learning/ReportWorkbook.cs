using StatsDirect.UI;
using StatsDirect.UI.Learning;
using System.Text.Json;

internal static partial class Program
{
    // The workbook the report's AI pane offers for sharing is the one the result came from, not the last workbook activated.
    static async Task TestReportWorkbookDefault(frmMain main, LearningService service, frmSpreadsheetGear grid)
    {
        var a = (frmSpreadsheetGear)main.CreateGrid("Result workbook A");
        var b = (frmSpreadsheetGear)main.CreateGrid("Later workbook B");
        b.Activate(); service.RefreshWorkspace(); await Task.Delay(100);
        string aPath = ((IGrid)a).WorkbookPath;
        var report = (frmReportWebView)main.CreateReport(); await report.LearningReport.Ready;
        string record = JsonSerializer.Serialize(new { version = 1, operation = "Test", inputs = new[] { new { name = "x", mode = "NumericReplaceMissing", file = Path.GetFileName(aPath), path = aPath, sheet = "Sheet1", columns = new object[0] } }, source = new { file = Path.GetFileName(aPath), unsaved = true } });
        string fromA = await report.LearningReport.AppendAsync("<h2>Result from A</h2><p>P = 0.04</p>", "Result from A", "Test", 0, record);
        string note = await report.LearningReport.AppendAsync("<h2>A note</h2><p>No run.</p>", "A note", "", 0);
        await Task.Delay(80);
        service.ShowReportAI(report, fromA); var pane = (ScriptAIPane)Field(service, "ai");
        await Wait(() => pane.Workbook.SelectedItem?.ToString() == "Result workbook A", "Report AI offers the workbook the result came from, not the last workbook activated");
        Check(service.Sharing.DataDocumentId == a.Id(), "the session's data document follows the result's workbook");
        service.ShowReportAI(report, note); await Task.Delay(400);
        Check(pane.Workbook.SelectedItem?.ToString() == "Result workbook A", "a result without a record leaves the offered workbook as it was");
        // the stage as it was found: the harness workbook active and chosen for sharing, the pane shown
        pane.Workbook.SelectedItem = pane.Workbook.Items.Cast<object>().First(c => c.ToString() == grid.Text);
        grid.Activate(); service.RefreshWorkspace(); await Task.Delay(100);
    }
}
