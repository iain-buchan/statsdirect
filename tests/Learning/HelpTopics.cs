using StatsDirect.Templates;
using StatsDirect.UI;
using StatsDirect.UI.Learning;
using System.Reflection;

internal static partial class Program
{
    // Help from the Learning window opens the Learning topic, and Help from a report whose cursor is in no result opens the Report
    // window topic: the help's own ids for them, not the general topic.
    static async Task TestHelpTopics(frmMain main, LearningService service)
    {
        var app = typeof(frmMain).Assembly.GetType("StatsDirect.UI.SdApplication")!.GetProperty("SoleInstance", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        async Task<string> WaitForTopic(string expected)
        {
            string source = "";
            for (int i = 0; i < 200; i++)
            {
                await Task.Delay(50);
                object help = Field(app, "htmlHelp");
                object view = help.GetType().GetField("view", Members)?.GetValue(help);
                var browser = view?.GetType().GetField("Browser", Members)?.GetValue(view) as Microsoft.Web.WebView2.WinForms.WebView2;
                source = browser?.Source?.ToString() ?? "";
                if (source.Contains(expected)) break;
            }
            return source;
        }
        service.View.ShowHelp();
        string learning = await WaitForTopic("basics/learning.htm");
        Check(learning.Contains("basics/learning.htm"), "Help from the Learning window opens the Learning topic (" + learning + ")");
        var report = (frmReportWebView)main.CreateReport(); await report.LearningReport.Ready;
        report.ShowHelp();
        string window = await WaitForTopic("basics/report_window.htm");
        Check(window.Contains("basics/report_window.htm"), "Help from a report whose cursor is in no result opens the Report window topic (" + window + ")");
        var paired = TemplateFactory.Operations["TPaired"];
        var chart = new ReportVectorPicture("<svg xmlns='http://www.w3.org/2000/svg' width='16' height='16'><rect width='16' height='16'/></svg>", 16, 16);
        main.MdiChildren.OfType<frmSpreadsheetGear>().First().Activate();
        var application = (SdApplication)app;
        application.ActiveHelpTopic = paired.HelpContext.ChmId;
        try { ((IUserInterface)application).OutputReport(chart, paired, null, null); }
        finally { application.ActiveHelpTopic = 0; }
        var browser = report.LearningReport.Browser;
        await Wait(() => main.ActiveMdiChild == report && report.LearningReport.HelpContext == paired.HelpContext.ChmId,
            "output activates the chosen report and sets native Help for a chart-only result");
        await browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-host-command=help]').click()");
        string method = await WaitForTopic("parametric_methods/paired_t.htm");
        Check(method.Contains("parametric_methods/paired_t.htm"), "the report Help button opens the new analysis topic (" + method + ")");
        report.Dispose();
        object service2 = Field(app, "htmlHelp");
        service2.GetType().GetMethod("Hide", Members)!.Invoke(service2, null);   // the stage as it was found: the help pane hidden
    }
}
