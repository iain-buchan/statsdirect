using StatsDirect.UI;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

internal static partial class Program
{
    static async Task CheckReportSaveAs(frmMain main)
    {
        string folder = Path.Combine(Path.GetTempPath(), "StatsDirect-save-as-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var report = (Form)Call(main, "CreateReport");
        try
        {
            var view = Field(report, "view");
            await ((Task)Get(view, "Ready")).WaitAsync(TimeSpan.FromSeconds(45));
            await (Task)Call(view, "OpenHtmlAsync", "<p>Original report</p><table><tr><td>Estimate</td><td>1.25</td></tr></table>", "Save As check");
            var browser = (Microsoft.Web.WebView2.WinForms.WebView2)Field(view, "Browser");
            using (var dialog = (SaveFileDialog)Call(report, "CreateSaveReportDialog", true))
                Check(dialog.Filter.Split('|').Where((_, i) => i % 2 == 1).SequenceEqual(new[] { "*.html", "*.docx", "*.pdf" }) && dialog.FilterIndex == 1 && dialog.DefaultExt == "html" && dialog.AddExtension,
                    "report Save As offers HTML, Word and PDF, with HTML selected and extensions enabled");
            using (var dialog = (SaveFileDialog)Call(report, "CreateSaveReportDialog", false))
                Check(dialog.Filter.Split('|').Length == 2 && dialog.Filter.EndsWith("|*.html"), "ordinary first Save and save-on-close retain the editable HTML format");

            async Task Edit(string text)
            {
                await browser.CoreWebView2.ExecuteScriptAsync("(()=>{const p=document.querySelector('#results .report-entry p');p.textContent=" + JsonSerializer.Serialize(text) + ";p.dispatchEvent(new InputEvent('input',{bubbles:true,inputType:'insertText'}));})()");
                var until = DateTime.UtcNow.AddSeconds(10);
                while (!(bool)Get(report, "Dirty") && DateTime.UtcNow < until) await Task.Delay(30);
                Check((bool)Get(report, "Dirty"), "editing the browser report marks the document unsaved");
            }
            async Task ExportCopies(string stem, string expectedPath)
            {
                string title = report.Text;
                foreach (var (format, index) in new[] { ("docx", 2), ("pdf", 3) })
                {
                    string destination = Path.Combine(folder, stem + "." + format);
                    bool savedEditableReport = await (Task<bool>)Call(report, "SaveReportFileAsync", destination, index);
                    if (format == "docx")
                    {
                        using var zip = ZipFile.OpenRead(destination);
                        using var stream = zip.GetEntry("word/document.xml")!.Open();
                        var doc = XDocument.Load(stream); XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
                        Check(doc.Descendants(w + "t").Any(t => t.Value == "1.25") && doc.Descendants(w + "tbl").Any(), "Save As Word produces a real DOCX with editable report text and table");
                    }
                    else Check(Encoding.ASCII.GetString(File.ReadAllBytes(destination), 0, 5) == "%PDF-", "Save As PDF produces a real PDF");
                    Check(!savedEditableReport && (bool)Get(report, "Dirty") && (string)Get(report, "Path") == expectedPath && report.Text == title,
                        $"Save As {format} preserves the report name, HTML save location and unsaved edits");
                }
            }
            await Edit("First edit");
            await ExportCopies("new-report", null);
            string html = Path.Combine(folder, "editable.html");
            Check(await (Task<bool>)Call(report, "SaveReportFileAsync", html, 1) && !(bool)Get(report, "Dirty") && (string)Get(report, "Path") == html,
                "Save As HTML establishes the editable report's path and clears its saved edits");
            byte[] savedHtml = File.ReadAllBytes(html);
            await Edit("Later edit");
            await ExportCopies("existing-report", html);
            Check(File.ReadAllBytes(html).SequenceEqual(savedHtml), "exporting Word and PDF leaves the previous HTML file unchanged");
            byte[] word = File.ReadAllBytes(Path.Combine(folder, "existing-report.docx"));
            Check(await (Task<bool>)Call(report, "SaveReportAsync", false) && File.ReadAllText(html).Contains("Later edit") && !(bool)Get(report, "Dirty"),
                "ordinary Save after exporting writes the latest changes to the original HTML report");
            Check(File.ReadAllBytes(Path.Combine(folder, "existing-report.docx")).SequenceEqual(word), "ordinary Save never overwrites the exported Word document");
            using var next = (SaveFileDialog)Call(report, "CreateSaveReportDialog", true);
            Check(next.FilterIndex == 1 && next.FileName == html, "reopening Save As returns to HTML and the report's existing save path");
        }
        finally
        {
            report.GetType().GetProperty("Dirty", Internal)!.SetValue(report, false); report.Close(); report.Dispose();
            foreach (string name in new[] { "new-report.docx", "new-report.pdf", "existing-report.docx", "existing-report.pdf", "editable.html" }) File.Delete(Path.Combine(folder, name));
            Directory.Delete(folder);
        }
    }
}
