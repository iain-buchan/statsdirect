using StatsDirect.UI;
using System.Text.Json;

internal static partial class Program
{
    static async Task CheckReportNotebook(frmMain main)
    {
        string folder = Path.Combine(Path.GetTempPath(), "StatsDirect-notebook-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var report = (Form)Call(main, "CreateReport");
        try
        {
            var view = Field(report, "view");
            await ((Task)Get(view, "Ready")).WaitAsync(TimeSpan.FromSeconds(45));
            var menu = main.MainMenuStrip!.Items.OfType<ToolStripMenuItem>().Single(i => i.Text.Replace("&", "") == "File");
            var export = menu.DropDownItems.OfType<ToolStripMenuItem>().Single(i => i.Name == "webReportExportNotebook");
            Check(export.Text == "Export Jupyter notebook..." && export.DropDownItems.Count == 0
                  && !menu.DropDownItems.OfType<ToolStripMenuItem>().Any(i => i.Name == "webReportExport"),
                "report File menu replaces the duplicate Word/PDF Export submenu with Export Jupyter notebook");
            using (var saveAs = (SaveFileDialog)Call(report, "CreateSaveReportDialog", true))
                Check(saveAs.Filter.Contains("*.docx") && saveAs.Filter.Contains("*.pdf") && saveAs.FilterIndex == 1,
                    "HTML, Word and PDF remain available through report Save As");
            using (var notebookDialog = (SaveFileDialog)Call(report, "CreateNotebookDialog"))
                Check(notebookDialog.Filter.EndsWith("|*.ipynb") && notebookDialog.DefaultExt == "ipynb" && notebookDialog.AddExtension,
                    "notebook export chooses a Jupyter ipynb file with its standard extension");
            string record = """
                {"version":1,"operation":"TPaired","parameters":{"gamma":0.95},
                 "inputs":[{"name":"data","kind":"entered","columns":[
                    {"title":"Before","values":[312,242,340,388,296,254,391,402,290]},
                    {"title":"After","values":[300,201,232,312,220,256,328,330,231]}]}]}
                """;
            await (Task)Call(view, "AppendAsync", "<p>Original result and notes</p><table><tr><td>Estimate</td><td>56</td></tr></table>", "Paired review", "TPaired", 1150, record, null!);
            string html = Path.Combine(folder, "source.html"), notebook = Path.Combine(folder, "review.ipynb");
            Check(await (Task<bool>)Call(report, "SaveReportFileAsync", html, 1), "source report saves as editable HTML before notebook export");
            byte[] savedHtml = File.ReadAllBytes(html);
            var browser = (Microsoft.Web.WebView2.WinForms.WebView2)Field(view, "Browser");
            await browser.CoreWebView2.ExecuteScriptAsync("(()=>{const p=document.querySelector('#results .report-entry p');p.textContent='Edited interpretation β';p.dispatchEvent(new InputEvent('input',{bubbles:true,inputType:'insertText'}));})()");
            var until = DateTime.UtcNow.AddSeconds(5);
            while (!(bool)Get(report, "Dirty") && DateTime.UtcNow < until) await Task.Delay(20);
            Check((bool)Get(report, "Dirty"), "report has unsaved edited narrative before notebook export");
            string title = report.Text;
            await (Task)Call(report, "ExportNotebookFileAsync", notebook);
            using (var document = JsonDocument.Parse(File.ReadAllText(notebook)))
            {
                var cells = document.RootElement.GetProperty("cells").EnumerateArray().ToArray();
                Check(cells.Any(c => c.GetProperty("cell_type").GetString() == "markdown" && c.GetProperty("source").GetString()!.Contains("Edited interpretation β"))
                      && cells.Any(c => c.GetProperty("cell_type").GetString() == "code" && c.GetProperty("source").GetString()!.Contains("t.test")),
                    "actual report export writes edited report content plus its recorded data and R replay");
            }
            Check((bool)Get(report, "Dirty") && (string)Get(report, "Path") == html && report.Text == title && File.ReadAllBytes(html).SequenceEqual(savedHtml),
                "notebook export preserves dirty state, report title, HTML save path and previous HTML file");
            bool refusedHtml = false;
            try { await (Task)Call(report, "ExportNotebookFileAsync", html); } catch (IOException) { refusedHtml = true; }
            Check(refusedHtml && File.ReadAllBytes(html).SequenceEqual(savedHtml), "notebook export cannot overwrite the editable source report");
            // The existing destination must survive a later failed complete-data export.
            string missing = """
                {"version":1,"operation":"TPaired","parameters":{"gamma":0.95},"inputs":[
                  {"name":"data","kind":"frame2d","value":{"omitted":true}}]}
                """;
            byte[] savedNotebook = File.ReadAllBytes(notebook);
            await (Task)Call(view, "AppendAsync", "<p>Missing original data</p>", "Missing data", "TPaired", 1150, missing, null!);
            bool refusedData = false;
            try { await (Task)Call(report, "ExportNotebookFileAsync", notebook); } catch (InvalidOperationException) { refusedData = true; }
            Check(refusedData && File.ReadAllBytes(notebook).SequenceEqual(savedNotebook),
                "omitted analysis data aborts export without overwriting an existing notebook");
            Check(!Directory.EnumerateFiles(folder, ".statsdirect-*").Any(), "notebook export leaves no partial temporary files after success or refusal");
            await CheckNotebookOpening(main, notebook);
        }
        finally
        {
            await CloseReportForTest(report);
            foreach (string file in Directory.EnumerateFiles(folder)) File.Delete(file);
            Directory.Delete(folder);
        }
    }
}
