using System.Drawing;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using StatsDirect.UI;

internal static partial class Program
{
    // Real R SVGs reuse glyph and clip identifiers. These deliberately conflicting
    // pictures exercise the native output pane without running R or using its folder.
    static async Task CheckScriptSvgIsolation(frmMain main, Form grid, string artifactFolder = null)
    {
        string folder = Path.Combine(Path.GetTempPath(), "StatsDirect-svg-isolation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        artifactFolder ??= folder;
        Directory.CreateDirectory(artifactFolder);
        string red = Path.Combine(folder, "red.svg"), blue = Path.Combine(folder, "blue.svg");
        string Svg(string colour, int width) => $$"""
            <svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="300" height="100" viewBox="0 0 300 100">
            <style>.ink { fill: {{colour}}; }</style>
            <defs><path id="glyph0" d="M10 10H{{10 + width}}V90H10Z"/>
            <clipPath id="clip0"><rect x="110" y="10" width="{{width}}" height="80"/></clipPath>
            <linearGradient id="paint0"><stop offset="0" stop-color="{{colour}}"/><stop offset="1" stop-color="{{colour}}"/></linearGradient></defs>
            <rect width="300" height="100" fill="white"/>
            <use xlink:href="#glyph0" class="ink"/>
            <rect x="110" y="10" width="80" height="80" fill="url(#paint0)" clip-path="url(#clip0)"/>
            <rect x="210" y="10" width="80" height="80" class="ink"/>
            </svg>
            """;
        await File.WriteAllTextAsync(red, Svg("#ff0000", 80));
        await File.WriteAllTextAsync(blue, Svg("#0000ff", 40));
        byte[] redBytes = await File.ReadAllBytesAsync(red), blueBytes = await File.ReadAllBytesAsync(blue);
        using var script = (Form)Call(main, "CreateScriptWindow");
        try
        {
            var pane = (Control)Field(script, "outputPane");
            await (Task)Get(pane, "Ready");
            Call(pane, "AppendChart", new FileInfo(red));
            Call(pane, "AppendChart", new FileInfo(blue));
            Call(pane, "AppendChart", new FileInfo(red)); // the same file appended again
            await (Task)Call(pane, "SettledAsync");
            var browser = (WebView2)Field(pane, "browser");
            async Task<JsonDocument> Eval(string js) => JsonDocument.Parse(await browser.CoreWebView2.ExecuteScriptAsync(js));
            for (int i = 0; i < 100; i++)
            {
                using var loaded = await Eval("Array.from(document.images).every(i=>i.complete&&i.naturalWidth>0)");
                if (loaded.RootElement.GetBoolean()) break;
                await Task.Delay(30);
            }
            using (var count = await Eval("document.querySelectorAll('#t>svg,#t>img').length"))
                Check(count.RootElement.GetInt32() == 3, "the output pane retains all three SVGs, including a repeated file");

            bool coloursCorrect = true, glyphsAndClipsCorrect = true;
            for (int i = 0; i < 3; i++)
            {
                await browser.CoreWebView2.ExecuteScriptAsync($"document.querySelectorAll('#t>svg,#t>img')[{i}].scrollIntoView({{block:'center'}})");
                await Task.Delay(100); // let the browser paint the scrolled chart
                using var bounds = await Eval($"(()=>{{let r=document.querySelectorAll('#t>svg,#t>img')[{i}].getBoundingClientRect();return [r.left,r.top,r.width,r.height,innerWidth]}})()");
                double[] box = bounds.RootElement.EnumerateArray().Select(n => n.GetDouble()).ToArray();
                using var png = new MemoryStream();
                await browser.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, png);
                await File.WriteAllBytesAsync(Path.Combine(artifactFolder, $"svg-isolation-{i + 1}.png"), png.ToArray());
                png.Position = 0;
                using var image = new Bitmap(png);
                double scale = image.Width / box[4];
                Color Pixel(int x, int y) => image.GetPixel(
                    Math.Clamp((int)Math.Round((box[0] + x * box[2] / 300) * scale), 0, image.Width - 1),
                    Math.Clamp((int)Math.Round((box[1] + y * box[3] / 100) * scale), 0, image.Height - 1));
                bool Colour(Color c) => i == 1 ? c.B > 220 && c.R < 40 && c.G < 40 : c.R > 220 && c.G < 40 && c.B < 40;
                bool White(Color c) => c.R > 230 && c.G > 230 && c.B > 230;
                coloursCorrect &= Colour(Pixel(30, 50)) && Colour(Pixel(130, 50)) && Colour(Pixel(250, 50));
                glyphsAndClipsCorrect &= i == 1 ? White(Pixel(70, 50)) && White(Pixel(170, 50)) : Colour(Pixel(70, 50)) && Colour(Pixel(170, 50));
                Console.WriteLine($"      SVG {i + 1}: glyph {Pixel(30, 50)}, paint {Pixel(130, 50)}, CSS {Pixel(250, 50)}");
            }
            Console.WriteLine("      SVG rendering captures: " + artifactFolder);
            Check(coloursCorrect, "each SVG retains its own glyph colour, gradient and CSS after other charts and a repeat are appended");
            Check(glyphsAndClipsCorrect, "reused glyph and clipping identifiers draw each SVG's own shapes");
            using (var layout = await Eval("(()=>{document.body.style.width='164px';let p=document.querySelectorAll('#t>svg,#t>img')[1],r=p.getBoundingClientRect();return [r.width,r.height]})()"))
            {
                double width = layout.RootElement[0].GetDouble(), height = layout.RootElement[1].GetDouble();
                Check(width > 0 && width <= 164 && Math.Abs(width / height - 3) < 0.02, "SVG output narrows to the pane while keeping its aspect ratio");
            }
            using (var links = await Eval("Array.from(document.querySelectorAll('#t .chart a')).map(a=>a.getAttribute('href')).join('|')"))
                Check(links.RootElement.GetString() == new Uri(red).AbsoluteUri + "|" + new Uri(blue).AbsoluteUri + "|" + new Uri(red).AbsoluteUri,
                    "each SVG caption retains the original chart file link");
            byte[] redAfter = await File.ReadAllBytesAsync(red), blueAfter = await File.ReadAllBytesAsync(blue);
            Check(redBytes.SequenceEqual(redAfter) && blueBytes.SequenceEqual(blueAfter),
                "displaying SVGs leaves the source chart files unchanged");
        }
        finally
        {
            script.Dispose();
            grid.Activate();
            // Retain screenshots when the default artifact folder is used.
            File.Delete(red); File.Delete(blue);
            // the captures stay only where an artifact folder was asked for; the check's own folder goes
            if (artifactFolder == folder) Directory.Delete(folder, true);
            else if (!Directory.EnumerateFileSystemEntries(folder).Any()) Directory.Delete(folder);
        }
    }
}
