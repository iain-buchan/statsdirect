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
        async Task<string> Shown(Action show, string expected)
        {
            show();
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
        string learning = await Shown(() => service.View.ShowHelp(), "basics/learning.htm");
        Check(learning.Contains("basics/learning.htm"), "Help from the Learning window opens the Learning topic (" + learning + ")");
        var report = (frmReportWebView)main.CreateReport(); await report.LearningReport.Ready;
        string window = await Shown(() => report.ShowHelp(), "basics/report_window.htm");
        Check(window.Contains("basics/report_window.htm"), "Help from a report whose cursor is in no result opens the Report window topic (" + window + ")");
        report.Dispose();
        object service2 = Field(app, "htmlHelp");
        service2.GetType().GetMethod("Hide", Members)!.Invoke(service2, null);   // the stage as it was found: the help pane hidden
    }
}
