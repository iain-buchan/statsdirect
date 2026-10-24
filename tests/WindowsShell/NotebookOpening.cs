using Microsoft.Web.WebView2.WinForms;
using StatsDirect.UI;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static partial class Program
{
    static async Task CheckNotebookOpening(frmMain main, string exportedNotebook)
    {
        var originalWindows = main.MdiChildren.ToHashSet();
        var previous = main.ActiveMdiChild;
        string folder = Path.GetDirectoryName(exportedNotebook)!;
        string imported = Path.Combine(folder, "colleague.ipynb"), sentinel = Path.Combine(folder, "should-not-run.txt");
        var notebook = JsonNode.Parse(File.ReadAllText(exportedNotebook))!;
        var cells = notebook["cells"]!.AsArray();
        var reportCell = cells.First(c => c!["metadata"]?["statsdirect"]?["role"]?.GetValue<string>() == "original StatsDirect report")!;
        reportCell["source"] = reportCell["source"]!.GetValue<string>().Replace("Edited interpretation β", "Colleague’s final interpretation");
        var codeCell = cells.First(c => c!["cell_type"]!.GetValue<string>() == "code")!;
        codeCell["source"] = "writeLines('unexpected execution', " + JsonSerializer.Serialize(sentinel.Replace('\\', '/')) + ")\n" + codeCell["source"]!.GetValue<string>();
        File.WriteAllText(imported, notebook.ToJsonString());
        byte[] originalNotebook = File.ReadAllBytes(imported);
        try
        {
            Check(((OpenFileDialog)Field(main, "openFileDialog")).Filter.Contains("*.ipynb"), "File Open includes StatsDirect Jupyter notebooks");
            Check((bool)Call(main, "OpenFile", imported, false), "the actual File Open dispatcher recognizes ipynb files");
            Check(await ((Task<bool>)Get(main, "PendingNotebookOpen")).WaitAsync(TimeSpan.FromSeconds(45)), "notebook File Open completes successfully");
            var created = main.MdiChildren.Where(f => !originalWindows.Contains(f)).ToArray();
            var report = created.Single(f => f.GetType().Name == "frmReportWebView");
            var script = created.OfType<frmScript>().Single();
            var grid = created.Single(f => f.GetType().Name == "frmSpreadsheetGear");
            Check(created.Length == 3 && main.ActiveMdiChild == report, "opening a notebook restores report, data and R tabs and leaves the report active");
            Check(created.All(f => f.Text.Contains("colleague", StringComparison.OrdinalIgnoreCase)), "restored document tabs identify the notebook file that was opened");
            var view = Field(report, "view");
            var browser = (WebView2)Field(view, "Browser");
            string html = JsonSerializer.Deserialize<string>(await browser.CoreWebView2.ExecuteScriptAsync("document.getElementById('results').innerHTML"))!;
            Check(html.Contains("Colleague’s final interpretation") && !html.Contains("Edited interpretation β"), "the notebook report displays the colleague's current cells rather than archived metadata");
            Check(((string)Get(script, "LearningScript")).Contains("writeLines('unexpected execution'"),
                "the restored script retains the current notebook R code");
            Check(!(bool)Get(script, "LearningRunning") && !File.Exists(sentinel), "opening a notebook never starts R or executes its code");
            Check(created.All(f => (bool)Get(f, "Dirty") && Get(f, "Path") == null), "imported report, code and workbook are unsaved documents with no notebook overwrite path");
            var workbook = (SpreadsheetGear.Windows.Forms.WorkbookView)Field(grid, "workbookView");
            workbook.GetLock();
            try
            {
                Check(workbook.ActiveWorkbook.Worksheets[0].Cells[1, 0].Value is double before && before == 312d
                    && workbook.ActiveWorkbook.Worksheets[0].Cells[1, 1].Value is double after && after == 300d,
                    "restored worksheet contains the notebook's actual paired data as numbers");
            }
            finally { workbook.ReleaseLock(); }
            Check(File.ReadAllBytes(imported).SequenceEqual(originalNotebook), "opening leaves the original notebook file byte-for-byte unchanged");

            var opened = main.MdiChildren.ToHashSet();
            string broken = Path.Combine(folder, "broken.ipynb");
            var bad = JsonNode.Parse(File.ReadAllText(imported))!;
            bad["cells"]!.AsArray().Add(new JsonObject { ["id"] = "bad-svg", ["cell_type"] = "markdown", ["metadata"] = new JsonObject(),
                ["source"] = "![broken](attachment:bad.svg)", ["attachments"] = new JsonObject { ["bad.svg"] = new JsonObject { ["image/svg+xml"] = "not an SVG" } } });
            File.WriteAllText(broken, bad.ToJsonString());
            bool refused = false;
            try { await (Task)Call(main, "ImportNotebookAsync", broken); } catch (InvalidOperationException) { refused = true; } catch (IOException) { refused = true; }
            Check(refused && opened.SetEquals(main.MdiChildren) && main.ActiveMdiChild == report,
                "a failed notebook import rolls back only its own new windows and preserves existing work");
            string invalidText = Path.Combine(folder, "invalid-encoding.ipynb");
            byte[] invalidBytes = System.Text.Encoding.UTF8.GetBytes(notebook.ToJsonString());
            int badByte = invalidBytes.AsSpan().IndexOf(System.Text.Encoding.UTF8.GetBytes("final interpretation"));
            if (badByte < 0) throw new Exception("Notebook encoding fixture is missing its text marker");
            invalidBytes[badByte] = 0xff;
            File.WriteAllBytes(invalidText, invalidBytes);
            bool refusedEncoding = false;
            try { await (Task)Call(main, "ImportNotebookAsync", invalidText); } catch (System.Text.DecoderFallbackException) { refusedEncoding = true; }
            Check(refusedEncoding && opened.SetEquals(main.MdiChildren), "invalid notebook UTF-8 is refused without changing text or opening partial documents");
        }
        finally
        {
            foreach (var child in main.MdiChildren.Where(f => !originalWindows.Contains(f))) Call(child, "DiscardNotebookImport");
            if (previous != null && !previous.IsDisposed) previous.Activate();
        }
    }
}
