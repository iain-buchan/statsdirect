using StatsDirect.UI;
using StatsDirect.UI.Learning;
using System.Text.Json.Nodes;

internal static partial class Program
{
    private static async Task TestWorksheetRReport(frmMain main, frmScript script)
    {
        var console = script.InteractiveConsole;
        await console.Ready.WaitAsync(TimeSpan.FromSeconds(40));
        var browser = console.Browser.CoreWebView2;
        Check(await browser.ExecuteScriptAsync("""
            (() => {
                const button = document.getElementById('add-output-to-report');
                return !!button && !button.hidden && button.disabled;
            })()
            """) == "true", "worksheet AI R offers to add output only after a run produces it");

        var destination = (frmReportWebView)main.CreateReport();
        var alternative = (frmReportWebView)main.CreateReport();
        try
        {
            await Task.WhenAll(destination.LearningReport.Ready, alternative.LearningReport.Ready)
                .WaitAsync(TimeSpan.FromSeconds(40));
            destination.Activate();
            await Wait(() => ReferenceEquals(main.ActiveMdiChild, destination),
                "the user selects which report will receive worksheet R output");
            script.Activate();
            await Wait(() => ReferenceEquals(main.ActiveMdiChild, script),
                "the user returns to the reviewed worksheet R script");

            await console.SubmitAsync(await console.GetScriptAsync(), "script");
            await Wait(() => console.NativeState == "ready" && console.History.Any(entry =>
                entry.TryGetProperty("type", out var type) && type.GetString() == "commandFinished" &&
                entry.GetProperty("status").GetString() == "ok"),
                "actual R runs the worksheet answer to completion", 30000);

            const string addButtonReady = """
                (() => {
                    const button = document.getElementById('add-output-to-report');
                    return !button.hidden && !button.disabled;
                })()
                """;
            for (int i = 0; i < 100 && await browser.ExecuteScriptAsync(addButtonReady) != "true"; i++)
                await Task.Delay(50);
            Check(await browser.ExecuteScriptAsync(addButtonReady) == "true",
                "R's completed output enables the add-to-report command");
            await browser.ExecuteScriptAsync("document.getElementById('add-output-to-report').click()");

            int entries = 0;
            for (int i = 0; i < 100 && entries == 0; i++)
            {
                entries = await WorksheetAnalysisResultCount(destination);
                if (entries == 0) await Task.Delay(50);
            }
            Check(entries == 1 && await WorksheetAnalysisResultCount(alternative) == 0,
                "adding worksheet R output uses the last selected report");
            var content = JsonNode.Parse(await destination.LearningReport.Browser.CoreWebView2.ExecuteScriptAsync("""
                ({
                    text: document.getElementById('results').innerText,
                    svgCount: document.querySelectorAll('#results svg').length
                })
                """));
            string text = content.S("text");
            Check(content.I("svgCount") == 1 && text.Contains("Mean age: 49", StringComparison.Ordinal) &&
                !text.Contains("cat(", StringComparison.Ordinal) && !text.Contains("plot(age", StringComparison.Ordinal) &&
                !text.Contains("earlier_answer_only", StringComparison.Ordinal),
                "the report contains the actual numerical output and SVG without commands or earlier answer code");
        }
        finally
        {
            CloseTestDocument(alternative);
            CloseTestDocument(destination);
            script.Activate();
        }
    }
}
