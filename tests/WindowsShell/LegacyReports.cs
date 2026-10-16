using StatsDirect.UI;
using System.Reflection;
using System.Text;
using System.Text.Json;

internal static partial class Program
{
    static async Task CheckLegacyReportOpening(frmMain main)
    {
        // Exercise the compiled application's importer before any CSV/XLSB can
        // register legacy encodings globally and mask a fresh-start failure.
        string source = Path.Combine(Path.GetTempPath(), "StatsDirect-legacy-" + Guid.NewGuid().ToString("N") + ".rtf");
        string saved = Path.ChangeExtension(source, ".html");
        byte[] original = Encoding.ASCII.GetBytes(@"{\rtf1\ansi\ansicpg1252\deff0{\fonttbl{\f0 Arial;}}\f0\fs20 Legacy report: Caf\'e9 \'96 \'a3 95% CI \u945?}");
        File.WriteAllBytes(source, original);
        Form report = null;
        try
        {
            var importer = typeof(frmMain).Assembly.GetType("StatsDirect.UI.WebReports.LegacyReport")!;
            // Fail directly in the harness instead of leaving a modal error
            // prompt open if the application's importer regresses.
            importer.GetMethod("Read", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, new object[] { source });
            Check(true, "compiled application imports RTF before any spreadsheet in a fresh process");
            Check((bool)Call(main, "OpenFile", source, false) && main.ActiveMdiChild?.GetType().Name == "frmReportWebView", "File Open dispatches RTF to the report editor");
            report = main.ActiveMdiChild!;
            var view = Field(report, "view");
            await ((Task)Get(view, "Ready")).WaitAsync(TimeSpan.FromSeconds(45));
            var until = DateTime.UtcNow.AddSeconds(15);
            while ((bool)Field(report, "opening") && DateTime.UtcNow < until) await Task.Delay(30);
            var browser = (Microsoft.Web.WebView2.WinForms.WebView2)Field(view, "Browser");
            string text = JsonSerializer.Deserialize<string>(await browser.CoreWebView2.ExecuteScriptAsync("document.getElementById('results').innerText"))!;
            Check(!(bool)Field(report, "opening") && text.Contains("Legacy report: Café – £ 95% CI α"), "RTF content is displayed by the actual File Open workflow");
            Check(Get(report, "Path") == null && report.Text == Path.GetFileName(source), "RTF opens as a named imported report requiring Save As");
            await (Task)Call(view, "SaveAsync", saved, "Converted legacy report", "html");
            await (Task)Call(view, "OpenHtmlAsync", File.ReadAllText(saved), "Reopened legacy report");
            string reopened = JsonSerializer.Deserialize<string>(await browser.CoreWebView2.ExecuteScriptAsync("document.getElementById('results').innerText"))!;
            Check(reopened.Contains("Legacy report: Café – £ 95% CI α") && File.ReadAllBytes(source).SequenceEqual(original), "converted report saves and reopens as HTML while the original RTF stays unchanged");
        }
        finally
        {
            if (report != null) await CloseReportForTest(report);
            File.Delete(source); File.Delete(saved);
        }
    }

    static async Task CloseReportForTest(Form report)
    {
        report.GetType().GetProperty("Dirty", Internal)!.SetValue(report, false);
        report.Close();
        // Report closing awaits a browser snapshot. Let that normal close finish
        // before disposal, so MDI cannot still be using the report's icon.
        var until = DateTime.UtcNow.AddSeconds(10);
        while (!report.IsDisposed && DateTime.UtcNow < until) await Task.Delay(30);
        if (!report.IsDisposed) throw new Exception("The report did not finish closing.");
    }
}
