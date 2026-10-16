using System.Text.Json;
using StatsDirect.Configuration;
using StatsDirect.UI;

internal static partial class Program
{
    // The charts a run writes appear in the output pane, a WebView2 page: an SVG as it is, drawn by the browser at the size its svg element
    // gives or narrowed to the pane, a PNG as an image, each after its name and with its file as a link, before the list of files written.
    static async Task CheckScriptCharts(frmMain main, Form grid)
    {
        string folder = SDConfiguration.MyStatsDirectRFolder;
        string svgFile = Path.Combine(folder, "shell-chart-check.svg"), pngFile = Path.Combine(folder, "shell-chart-check.png"), pdfFile = Path.Combine(folder, "shell-chart-check.pdf"), textFile = Path.Combine(folder, "shell-chart-check.txt");
        List<string> files = new() { svgFile, pngFile, pdfFile, textFile };
        // Preserve any pre-existing explicitly named outputs. Automatic charts now get fresh run identities.
        var previous = files.Where(File.Exists).ToDictionary(f => f, f => (Bytes: File.ReadAllBytes(f), Modified: File.GetLastWriteTimeUtc(f)));
        using var script = (Form)Call(main, "CreateScriptWindow");
        try
        {
            foreach (string f in files) if (File.Exists(f)) File.Delete(f);
            // A plot without a device of its own goes to the run's default SVG device, and a second
            // default device after dev.off() gets a file of its own rather than writing over the first; a PDF a script writes is shown too
            Call(script, "ShowR", "plot(c(2, 4, 3), main = \"default device\")\ndev.off()\nsvg(\"shell-chart-check.svg\", width = 5, height = 4)\nplot(1:3, main = \"shell\")\ndev.off()\npng(\"shell-chart-check.png\", width = 300, height = 200)\nplot(3:1)\ndev.off()\npdf(\"shell-chart-check.pdf\", width = 4, height = 3)\nplot(1:2)\ndev.off()\nwriteLines(\"x\", \"shell-chart-check.txt\")\nplot(c(2, 2), main = \"second default device\")\n", "Chart check", null, true);
            var pane = (Control)Field(script, "outputPane");
            var until = DateTime.UtcNow.AddSeconds(120);
            while (!pane.Text.Contains("End of run") && DateTime.UtcNow < until) await Task.Delay(100);
            await (Task)Call(pane, "SettledAsync");
            string text = pane.Text;
            string[] defaultNames = System.Text.RegularExpressions.Regex.Matches(text, @"Chart: (Rplot-[0-9a-f]{32}-[0-9]+-[0-9]+\.svg)")
                .Select(m => m.Groups[1].Value).ToArray();
            files.AddRange(defaultNames.Select(name => Path.Combine(folder, name)));
            Check(text.Contains("End of run") && File.Exists(svgFile) && File.Exists(pngFile) && File.Exists(pdfFile), "the script runs in R and writes an SVG, a PNG and a PDF chart to the R folder");
            Check(defaultNames.Length == 2 && defaultNames[0].EndsWith("-1-001.svg", StringComparison.Ordinal) && defaultNames[1].EndsWith("-2-001.svg", StringComparison.Ordinal) && defaultNames.All(name => File.Exists(Path.Combine(folder, name))) && !text.Contains("Rplots.pdf"),
                  "a plot without an explicit device goes to a unique SVG in the R folder, and a reopened default device gets its own file");
            string defaultFile = Path.Combine(folder, defaultNames[0]), secondFile = Path.Combine(folder, defaultNames[1]);
            var browser = (Microsoft.Web.WebView2.WinForms.WebView2)Field(pane, "browser");
            async Task<string> Eval(string js) => JsonSerializer.Deserialize<string>(await browser.CoreWebView2.ExecuteScriptAsync("String(" + js + ")"))!;
            Check(await Eval("document.querySelectorAll('img.svg-chart').length") == "3" && await Eval("document.querySelectorAll('img:not(.svg-chart)').length") == "1" && await Eval("document.querySelectorAll('iframe.pdf').length") == "1",
                  "the pane draws three isolated SVG charts, the PNG chart as an image and the explicit PDF in its viewer");
            string pngLink = new Uri(pngFile).AbsoluteUri, svgLink = new Uri(svgFile).AbsoluteUri, pdfLink = new Uri(pdfFile).AbsoluteUri, defaultLink = new Uri(defaultFile).AbsoluteUri, secondLink = new Uri(secondFile).AbsoluteUri;
            string textLink = new Uri(textFile).AbsoluteUri;
            string order = await Eval("Array.from(document.querySelectorAll('#t a')).map(function(a){return a.textContent+'='+a.getAttribute('href')}).join('|')");
            Check(order == $"Chart: {defaultNames[0]}={defaultLink}|Chart: {defaultNames[1]}={secondLink}|Chart: shell-chart-check.pdf={pdfLink}|Chart: shell-chart-check.png={pngLink}|Chart: shell-chart-check.svg={svgLink}|{textLink}={textLink}" && text.IndexOf("Files written", StringComparison.Ordinal) > text.IndexOf("Chart: shell-chart-check.svg", StringComparison.Ordinal),
                  "each chart's caption, in file-name order, is its one link, and the list of files written that follows names only the file not shown as a chart");
            // sizes: the SVG at its svg element's 5 by 4 inches (480 by 384 pixels) or narrowed to the pane keeping its shape; the PNG at its 300 by 200
            int paneWidth = (int)Math.Round(pane.ClientSize.Width * 96.0 / pane.DeviceDpi) - 16;
            string[] svgBox = (await Eval("(function(){var r=document.querySelectorAll('img.svg-chart')[2].getBoundingClientRect();return Math.round(r.width)+','+Math.round(r.height)})()")).Split(',');
            string[] imgBox = (await Eval("(function(){var i=document.querySelector('img:not(.svg-chart)');return i.naturalWidth+','+i.naturalHeight+','+Math.round(i.getBoundingClientRect().width)})()")).Split(',');
            int svgWidth = int.Parse(svgBox[0]), svgHeight = int.Parse(svgBox[1]);
            Check(Math.Abs(svgWidth - Math.Min(480, paneWidth)) <= 20 && Math.Abs(svgWidth * 384.0 / 480 - svgHeight) <= 2 && imgBox[0] == "300" && imgBox[1] == "200" && Math.Abs(int.Parse(imgBox[2]) - Math.Min(300, paneWidth)) <= 20,
                  $"the SVG stands at its own size or the pane's width keeping its shape ({svgWidth} by {svgHeight}; pane {paneWidth}), the PNG at its 300 by 200");
            // the transcript's look, drawn by the page: R's prompts and commands in their own colour, in a monospace face
            string look = await Eval("getComputedStyle(document.querySelector('.prompt')).color+'|'+getComputedStyle(document.body).fontFamily");
            Check(look.StartsWith("rgb(0, 0, 160)|Consolas", StringComparison.Ordinal), "R's prompts and commands are drawn in their own colour, in a monospace face (" + look + ")");
        }
        finally
        {
            foreach (string f in files)
                if (previous.TryGetValue(f, out var original)) { File.WriteAllBytes(f, original.Bytes); File.SetLastWriteTimeUtc(f, original.Modified); }
                else if (File.Exists(f)) File.Delete(f);
            grid.Activate();
            await Task.Delay(150);
        }
    }
}
