using StatsDirect.UI;
using StatsDirect.UI.Learning;
using System.Text.Json;
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

        int CompletedRuns() => console.History.Count(entry =>
            entry.TryGetProperty("type", out var type) && type.GetString() == "commandFinished" &&
            entry.GetProperty("status").GetString() == "ok");

        async Task RunR(string source, string description)
        {
            int completed = CompletedRuns();
            await console.SubmitAsync(source, "script");
            await Wait(() => console.NativeState == "ready" && CompletedRuns() > completed, description, 30000);
        }

        async Task AddOutput(frmReportWebView report, int expectedCount)
        {
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

            int entries = await WorksheetAnalysisResultCount(report);
            for (int i = 0; i < 100 && entries < expectedCount; i++)
            {
                await Task.Delay(50);
                entries = await WorksheetAnalysisResultCount(report);
            }
            Check(entries == expectedCount, $"the selected report contains {expectedCount} R output block(s)");
        }

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

            await RunR(await console.GetScriptAsync(), "actual R runs the worksheet answer to completion");
            await AddOutput(destination, 1);
            Check(await WorksheetAnalysisResultCount(alternative) == 0,
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

            script.Activate();
            await console.ClearOutputAsync();
            await RunR("""plot(c(1, 5, 2), main = "Second R run", xlab = "Batch index", ylab = "Response")""",
                "a separate R run produces the second chart");
            await AddOutput(destination, 2);
            var separateRuns = await InspectWorksheetReportCharts(destination, "worksheet-separate-runs.html",
                "two charts added from separate R runs");

            alternative.Activate();
            await Wait(() => ReferenceEquals(main.ActiveMdiChild, alternative),
                "the user selects a fresh report for two charts from one run");
            script.Activate();
            await console.ClearOutputAsync();
            await RunR("""
                plot(c(2, 3, 1), main = "First chart in one run", ylab = "Number")
                plot(c(8, 3, 9), main = "Second chart in one run", ylab = "Reading")
                """, "one R run produces two different charts");
            await AddOutput(alternative, 1);
            var oneRun = await InspectWorksheetReportCharts(alternative, "worksheet-one-run.html",
                "two charts added from a single R run");
            Check(separateRuns.Live && oneRun.Live,
                $"Live report SVG IDs are unique and references resolve within each chart (separate runs = {separateRuns.Live}, one run = {oneRun.Live})");
            Check(separateRuns.Saved && oneRun.Saved,
                $"Saved HTML SVG IDs are unique and references resolve within each chart (separate runs = {separateRuns.Saved}, one run = {oneRun.Saved})");
        }
        finally
        {
            CloseTestDocument(alternative);
            CloseTestDocument(destination);
            script.Activate();
        }
    }

    private static async Task<(bool Live, bool Saved)> InspectWorksheetReportCharts(
        frmReportWebView report, string fileName, string scenario)
    {
        var view = report.LearningReport;
        string path = Path.Combine(root, fileName);
        await view.SaveAsync(path, "Worksheet R charts", "html");
        string saved = JsonSerializer.Serialize(await File.ReadAllTextAsync(path));
        var content = JsonNode.Parse(await view.Browser.CoreWebView2.ExecuteScriptAsync($$"""
            (() => {
                const inspect = (document, selector) => {
                    const charts = [...document.querySelectorAll(selector)];
                    const ids = charts.flatMap(svg =>
                        [svg, ...svg.querySelectorAll('[id]')].map(element => element.id).filter(Boolean));
                    const local = charts.every(svg => {
                        const references = [];
                        for (const element of [svg, ...svg.querySelectorAll('*')]) {
                            for (const { localName, value } of element.attributes) {
                                if (localName === 'href' && value.startsWith('#'))
                                    references.push(value.slice(1));
                                for (const match of value.matchAll(/url\(\s*['"]?#([^\s'")]+)['"]?\s*\)/g))
                                    references.push(match[1]);
                            }
                        }
                        return references.length > 0 &&
                            references.every(id => svg.contains(document.getElementById(id)));
                    });
                    return { count: charts.length, isolated: local && ids.length === new Set(ids).size };
                };
                // Parse the exported page directly. Reopening in the editor would rename SVG IDs again.
                const exported = new DOMParser().parseFromString({{saved}}, 'text/html');
                return { live: inspect(document, '#results svg'), saved: inspect(exported, '.report-entry svg') };
            })()
            """));
        var live = content["live"];
        var exported = content["saved"];
        Check(live.I("count") == 2 && exported.I("count") == 2,
            $"{scenario} are both retained as SVG in the report and saved HTML");
        return (live["isolated"].GetValue<bool>(), exported["isolated"].GetValue<bool>());
    }
}
