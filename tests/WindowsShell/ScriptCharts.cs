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
        string svgFile = Path.Combine(folder, "shell-chart-check.svg"), pngFile = Path.Combine(folder, "shell-chart-check.png");
        foreach (string f in new[] { svgFile, pngFile }) if (File.Exists(f)) File.Delete(f);
        using var script = (Form)Call(main, "CreateScriptWindow");
        try
        {
            Call(script, "ShowR", "svg(\"shell-chart-check.svg\", width = 5, height = 4)\nplot(1:3, main = \"shell\")\ndev.off()\npng(\"shell-chart-check.png\", width = 300, height = 200)\nplot(3:1)\ndev.off()\n", "Chart check", null, true);
            var pane = (Control)Field(script, "outputPane");
            var until = DateTime.UtcNow.AddSeconds(120);
            while (!pane.Text.Contains("End of run") && DateTime.UtcNow < until) await Task.Delay(100);
            await (Task)Call(pane, "SettledAsync");
            string text = pane.Text;
            Check(text.Contains("End of run") && File.Exists(svgFile) && File.Exists(pngFile), "the script runs in R and writes an SVG and a PNG chart to the R folder");
            var browser = (Microsoft.Web.WebView2.WinForms.WebView2)Field(pane, "browser");
            async Task<string> Eval(string js) => JsonSerializer.Deserialize<string>(await browser.CoreWebView2.ExecuteScriptAsync("String(" + js + ")"))!;
            Check(await Eval("document.querySelectorAll('svg').length") == "1" && await Eval("document.querySelectorAll('img').length") == "1", "the pane draws the SVG chart as it is and the PNG chart as an image");
            string pngLink = new Uri(pngFile).AbsoluteUri, svgLink = new Uri(svgFile).AbsoluteUri;
            string order = await Eval("Array.from(document.querySelectorAll('.chart,a')).map(function(e){return e.textContent}).join('|')");
            Check(order == $"Chart: shell-chart-check.png|{pngLink}|Chart: shell-chart-check.svg|{svgLink}|{pngLink}|{svgLink}" && text.IndexOf("Files written", StringComparison.Ordinal) > text.IndexOf("Chart: shell-chart-check.svg", StringComparison.Ordinal),
                  "each chart follows its name in file-name order with its file as a link, before the list of files written");
            // sizes: the SVG at its svg element's 5 by 4 inches (480 by 384 pixels) or narrowed to the pane keeping its shape; the PNG at its 300 by 200
            int paneWidth = (int)Math.Round(pane.ClientSize.Width * 96.0 / pane.DeviceDpi) - 16;
            string[] svgBox = (await Eval("(function(){var r=document.querySelector('svg').getBoundingClientRect();return Math.round(r.width)+','+Math.round(r.height)})()")).Split(',');
            string[] imgBox = (await Eval("(function(){var i=document.querySelector('img');return i.naturalWidth+','+i.naturalHeight+','+Math.round(i.getBoundingClientRect().width)})()")).Split(',');
            int svgWidth = int.Parse(svgBox[0]), svgHeight = int.Parse(svgBox[1]);
            Check(Math.Abs(svgWidth - Math.Min(480, paneWidth)) <= 20 && Math.Abs(svgWidth * 384.0 / 480 - svgHeight) <= 2 && imgBox[0] == "300" && imgBox[1] == "200" && Math.Abs(int.Parse(imgBox[2]) - Math.Min(300, paneWidth)) <= 20,
                  $"the SVG stands at its own size or the pane's width keeping its shape ({svgWidth} by {svgHeight}; pane {paneWidth}), the PNG at its 300 by 200");
            // the transcript's look, drawn by the page: R's prompts and commands in their own colour, in a monospace face
            string look = await Eval("getComputedStyle(document.querySelector('.prompt')).color+'|'+getComputedStyle(document.body).fontFamily");
            Check(look.StartsWith("rgb(0, 0, 160)|Consolas", StringComparison.Ordinal), "R's prompts and commands are drawn in their own colour, in a monospace face (" + look + ")");
        }
        finally
        {
            foreach (string f in new[] { svgFile, pngFile }) if (File.Exists(f)) File.Delete(f);
            grid.Activate();
            await Task.Delay(150);
        }
    }
}
