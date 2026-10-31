using System.Text.Json;
using Microsoft.Web.WebView2.WinForms;
using StatsDirect.UI;

internal static partial class Program
{
    // The actual integrated console retains automatic and named file-device charts.
    static async Task CheckScriptCharts(frmMain main, Form grid)
    {
        string folder = Path.Combine(Path.GetTempPath(), "StatsDirect-script-charts-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string svgFile = Path.Combine(folder, "shell-chart-check.svg"), pngFile = Path.Combine(folder, "shell-chart-check.png");
        string pdfFile = Path.Combine(folder, "shell-chart-check.pdf"), textFile = Path.Combine(folder, "shell-chart-check.txt");
        using var script = (Form)Call(main, "CreateScriptWindow");
        try
        {
            await PrepareRConsole(script, folder);
            int before = RFinished(script);
            Call(script, "ShowR", "plot(c(2,4,3),main='default device')\ndev.off()\nsvg('shell-chart-check.svg',width=5,height=4)\nplot(1:9,c(4,2,7,1,9,3,8,5,6),main='Owned nine-point Office fixture',xlab='Observation',ylab='Value')\ndev.off()\npng('shell-chart-check.png',width=300,height=200)\nplot(3:1)\ndev.off()\npdf('shell-chart-check.pdf',width=4,height=3)\nplot(1:2)\ndev.off()\nwriteLines('x','shell-chart-check.txt')\nplot(c(2,2),main='second default device')\n", "Chart check", null, true);
            await CompleteRCommand(script, before);
            var plots = RPlots(script);
            var automatic = plots.Where(p => !p.TryGetProperty("originalPath", out _)).ToArray();
            Check(File.Exists(svgFile) && File.Exists(pngFile) && File.Exists(pdfFile) && File.Exists(textFile),
                "the integrated R script writes its named SVG, PNG, PDF and text outputs");
            Check(automatic.Length == 2 && automatic.All(p => p.GetProperty("mime").GetString() == "image/svg+xml")
                && automatic.Select(p => p.GetProperty("path").GetString()).Distinct().Count() == 2
                && automatic.All(p => File.Exists(p.GetProperty("path").GetString()!))
                && !((string)Get(RConsole(script), "Transcript")).Contains("Rplots.pdf"),
                "default and reopened default devices retain separate, uniquely named SVG chart assets");
            var named = plots.Where(p => p.TryGetProperty("originalPath", out _)).ToArray();
            Check(named.Length == 3 && named.Select(p => p.GetProperty("originalPath").GetString()).ToHashSet(StringComparer.OrdinalIgnoreCase)
                .SetEquals(new[] { svgFile, pngFile, pdfFile }),
                "each explicit SVG, PNG and PDF appears once with its original filename and immutable saved asset");
            var browser = RBrowser(script);
            async Task<string> Eval(string js) => JsonSerializer.Deserialize<string>(await browser.CoreWebView2.ExecuteScriptAsync("String(" + js + ")"))!;
            var until = DateTime.UtcNow.AddSeconds(10);
            while (await Eval("[...document.images].every(i=>i.complete&&i.naturalWidth>0)") != "true" && DateTime.UtcNow < until) await Task.Delay(30);
            Check(await Eval("document.querySelectorAll('figure.plot img[src^=\"data:image/svg+xml\"]').length") == "3"
                && await Eval("document.querySelectorAll('figure.plot img[src^=\"data:image/png\"]').length") == "1"
                && await Eval("[...document.querySelectorAll('figure.plot')].filter(f=>f.querySelector('button')?.textContent==='Open vector PDF').length") == "1",
                "the console draws three isolated SVGs and one PNG and provides the explicit PDF's vector-open action");
            string[] captions = JsonSerializer.Deserialize<string[]>(await browser.CoreWebView2.ExecuteScriptAsync(
                "[...document.querySelectorAll('figure.plot figcaption')].map(c=>c.textContent)"))!;
            Check(captions.SequenceEqual(plots.Select(p => p.GetProperty("label").GetString()))
                && REvents(script).Count(e => e.GetProperty("type").GetString() == "notice" && e.TryGetProperty("filePath", out var path) && path.GetString() == textFile) == 1,
                "chart captions retain event order and filenames while the non-chart file is reported once");
            string svgBox = await Eval("(()=>{const i=[...document.images].find(i=>i.alt==='shell-chart-check.svg'),r=i.getBoundingClientRect();return [r.width,r.height,i.parentElement.clientWidth].join(',')})()");
            string pngBox = await Eval("(()=>{const i=[...document.images].find(i=>i.alt==='shell-chart-check.png'),r=i.getBoundingClientRect();return [i.naturalWidth,i.naturalHeight,r.width].join(',')})()");
            double[] svgSize = svgBox.Split(',').Select(double.Parse).ToArray(), pngSize = pngBox.Split(',').Select(double.Parse).ToArray();
            Check(svgSize[0] > 0 && svgSize[0] <= Math.Min(480, svgSize[2]) + 2 && Math.Abs(svgSize[0] * .8 - svgSize[1]) <= 2
                && pngSize[0] == 300 && pngSize[1] == 200 && pngSize[2] > 0 && pngSize[2] <= 300,
                "the SVG fits its own size or console width without distortion and the PNG retains its 300 by 200 pixels");
            string look = await Eval("getComputedStyle(document.getElementById('console-input')).color+'|'+getComputedStyle(document.getElementById('console-input')).fontFamily");
            Check(look.StartsWith("rgb(6, 69, 173)|", StringComparison.Ordinal) && look.Contains("Consolas"),
                "R commands use the shared blue code colour and a monospace face");
            await CheckScriptImagePaste(main, browser, svgFile, pngFile);
        }
        finally
        {
            script.Dispose();
            await DeleteRFixtureDirectory(folder, "StatsDirect-script-charts-");
            grid.Activate(); await Task.Delay(150);
        }
    }
}
