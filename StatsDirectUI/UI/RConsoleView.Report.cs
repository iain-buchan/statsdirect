#nullable enable
using StatsDirect.R;
using System;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace StatsDirect.UI;

internal sealed partial class RConsoleView
{
    internal async Task<string> ReportOutputAsync()
    {
        string sourceSession = sessionId;
        long sourceGeneration = generation;
        string json = await ExecuteAsync("window.StatsDirectRConsole.snapshot().transcript");
        if (closing || sourceSession != sessionId || sourceGeneration != generation || NativeState is not ("ready" or "stopped"))
            throw new InvalidOperationException("Wait for R to finish before adding its output to a report.");

        using var transcript = JsonDocument.Parse(json);
        var result = new StringBuilder();
        var text = new StringBuilder();

        void FlushText()
        {
            if (text.Length == 0) return;
            string output = text.ToString().TrimEnd();
            if (output.Length > 0) result.Append("<pre>").Append(WebUtility.HtmlEncode(output)).Append("</pre>");
            text.Clear();
        }

        foreach (var entry in transcript.RootElement.EnumerateArray())
        {
            switch (entry.GetProperty("kind").GetString())
            {
                case "output":
                    text.Append(entry.GetProperty("text").GetString());
                    break;
                case "plot" when entry.GetProperty("hasPreview").GetBoolean():
                    FlushText();
                    var plot = FindPlot(entry.GetProperty("plotId").GetString()!, entry.GetProperty("revision").GetInt64());
                    string label = WebUtility.HtmlEncode(entry.GetProperty("label").GetString() ?? "R chart");
                    string source = plot.GetProperty("preview").GetString()!;
                    const string svgPrefix = "data:image/svg+xml;base64,";
                    const string pngPrefix = "data:image/png;base64,";
                    if (source.StartsWith(svgPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        string svg = Encoding.UTF8.GetString(Convert.FromBase64String(source[svgPrefix.Length..]));
                        var vector = RResultsParser.VectorPicture(ScriptOutputView.Sanitised(svg));
                        result.Append(vector.Svg);
                    }
                    else if (source.StartsWith(pngPrefix, StringComparison.Ordinal))
                    {
                        result.Append("<img alt=\"").Append(label).Append("\" src=\"").Append(source).Append("\" />");
                    }
                    break;
            }
            if ((long)result.Length + text.Length > 16 * 1024 * 1024)
                throw new InvalidOperationException("This R output is too large to add at once. Clear the console and rerun the part you want to keep.");
        }

        FlushText();
        if (result.Length == 0) throw new InvalidOperationException("Run the R script to produce text or a chart first.");
        return result.Insert(0, "<h2>R output</h2>").ToString();
    }
}
