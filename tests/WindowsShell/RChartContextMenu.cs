using System.Drawing.Imaging;
using System.Reflection;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Web.WebView2.WinForms;

internal static partial class Program
{
    static void CheckRChartMenu(ContextMenuStrip menu, bool vector)
    {
        var items = menu.Items.OfType<ToolStripMenuItem>().ToArray();
        Check(items.Select(i => i.Text).SequenceEqual(new[] { "&Copy", "Save chart as &SVG…", "Save chart as &PNG…" }),
            "the R chart menu matches report Copy and SVG/PNG save commands without Open");
        Check(items[0].Enabled && items[1].Enabled == vector && items[2].Enabled,
            vector ? "an SVG chart offers Copy and both genuine export formats" : "a raster chart offers Copy and PNG without pretending to be vector SVG");
    }

    static async Task CheckRChartExports(Form script, WebView2 browser, string svgFile, string pngFile)
    {
        var console = RConsole(script);
        var plots = RPlots(script);
        var svg = plots.Single(p => p.TryGetProperty("originalPath", out var file) && file.GetString() == svgFile);
        var png = plots.Single(p => p.TryGetProperty("originalPath", out var file) && file.GetString() == pngFile);
        string folder = Path.Combine(Path.GetDirectoryName(svgFile)!, "chart-exports");
        Directory.CreateDirectory(folder);
        string Id(JsonElement plot) => plot.GetProperty("plotId").GetString()!;
        long Revision(JsonElement plot) => plot.GetProperty("revision").GetInt64();
        string Asset(JsonElement plot) => plot.GetProperty("path").GetString()!;
        Task Save(JsonElement plot, string format, string path) => (Task)Call(console, "SavePlotAsync", Id(plot), Revision(plot), format, path);
        async Task Reject(Func<Task> action, string message)
        {
            bool rejected = false;
            try { await action(); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or InvalidDataException || ex is TargetInvocationException { InnerException: ArgumentException or InvalidOperationException or InvalidDataException }) { rejected = true; }
            Check(rejected, message);
        }

        // The menu outlives its Closed event: WinForms dispatches an item's click afterwards.
        Call(console, "ShowChartContextMenu", Id(svg), Revision(svg), new Point(4, 4));
        var vectorMenu = (ContextMenuStrip)Field(console, "chartContextMenu");
        CheckRChartMenu(vectorMenu, true);
        var copy = (ToolStripMenuItem)vectorMenu.Items[0];
        vectorMenu.Close(ToolStripDropDownCloseReason.ItemClicked);
        Check(!vectorMenu.IsDisposed && !copy.IsDisposed, "closing the R image menu keeps its pending Copy item alive");
        Call(console, "ShowChartContextMenu", Id(png), Revision(png), new Point(4, 4));
        var rasterMenu = (ContextMenuStrip)Field(console, "chartContextMenu");
        Check(ReferenceEquals(vectorMenu, rasterMenu) && !vectorMenu.IsDisposed,
            "the R view safely reuses its native image popup for the next chart");
        CheckRChartMenu(rasterMenu, false);
        rasterMenu.Close();

        byte[] svgBytes = File.ReadAllBytes(Asset(svg)), pngBytes = File.ReadAllBytes(Asset(png));
        string svgOutput = Path.Combine(folder, "saved-chart.svg"), pngOutput = Path.Combine(folder, "saved-chart.png");
        await Save(svg, "svg", svgOutput);
        byte[] savedSvg = File.ReadAllBytes(svgOutput);
        var document = XDocument.Load(svgOutput);
        using var originalSvgStream = new MemoryStream(svgBytes);
        var originalSvg = XDocument.Load(originalSvgStream);
        var sourcePaths = originalSvg.Descendants().Where(e => e.Name.LocalName == "path"
            && !e.Ancestors().Any(a => a.Name.LocalName == "defs")).Select(e => (string)e.Attribute("d")).ToHashSet();
        var savedPaths = document.Descendants().Where(e => e.Name.LocalName == "path").Select(e => (string)e.Attribute("d")).ToHashSet();
        Check(document.Root?.Name.LocalName == "svg" && sourcePaths.Count > 9 && sourcePaths.IsSubsetOf(savedPaths)
            && (string)document.Root.Attribute("viewBox") == (string)originalSvg.Root.Attribute("viewBox")
            && (string)document.Root.Attribute("width") == "360" && (string)document.Root.Attribute("height") == "288",
            "Save as SVG retains chart geometry and dimensions in an Office-compatible vector file");
        var definitions = originalSvg.Descendants().Where(e => e.Attribute("id") != null).ToDictionary(e => (string)e.Attribute("id"));
        int visibleGlyphs = originalSvg.Descendants().Where(e => e.Name.LocalName == "use").Count(e =>
        {
            string href = e.Attributes().FirstOrDefault(a => a.Name.LocalName == "href")?.Value;
            return href?.StartsWith('#') == true && definitions.TryGetValue(href[1..], out var glyph)
                && glyph.Descendants().Any(d => d.Name.LocalName == "path");
        });
        Check(visibleGlyphs > 9 && !document.Descendants().Any(e => e.Name.LocalName == "use")
            && document.Descendants().Count(e => e.Name.LocalName == "g" && e.Attribute("transform") != null) >= visibleGlyphs,
            "saved SVG retains the R labels as positioned paths instead of fragile glyph references");
        var dimensions = JsonSerializer.Deserialize<int[]>(await browser.CoreWebView2.ExecuteScriptAsync(
            "(()=>{const image=[...document.images].find(i=>i.alt==='shell-chart-check.svg');return [image.naturalWidth,image.naturalHeight]})()"))!;
        await browser.CoreWebView2.ExecuteScriptAsync("window.StatsDirectRConsole.setMode('script')");
        Check(await browser.CoreWebView2.ExecuteScriptAsync("window.StatsDirectRConsole.snapshot().mode==='script'&&document.getElementById('console-panel').hidden") == "true",
            "the PNG export fixture hides console images by switching to Script");
        await Save(svg, "png", pngOutput);
        using (var image = new Bitmap(pngOutput))
            Check(image.RawFormat.Guid == ImageFormat.Png.Guid && image.Width == dimensions[0] * 2 && image.Height == dimensions[1] * 2,
                "Save as PNG renders the SVG at twice its natural size rather than saving the small on-screen preview");
        await browser.CoreWebView2.ExecuteScriptAsync("window.StatsDirectRConsole.setMode('console')");
        string rasterOutput = Path.Combine(folder, "saved-native.png");
        await Save(png, "png", rasterOutput);
        using (var image = new Bitmap(rasterOutput))
            Check(File.ReadAllBytes(rasterOutput).SequenceEqual(pngBytes) && image.Width == 300 && image.Height == 200,
                "saving an R PNG preserves its original encoded bytes and 300 by 200 pixels");

        string refused = Path.Combine(folder, "must-stay.svg");
        byte[] keep = "Existing export must survive a rejected command"u8.ToArray();
        File.WriteAllBytes(refused, keep);
        await Reject(() => Save(png, "svg", refused), "a raster chart cannot be exported as misleading SVG");
        await Reject(() => (Task)Call(console, "SavePlotAsync", "missing-chart", (long?)1, "svg", refused), "saving a removed chart is rejected");
        await Reject(() => (Task)Call(console, "SavePlotAsync", Id(svg), (long?)long.MaxValue, "svg", refused), "saving an obsolete or unknown chart revision is rejected");
        await Reject(() => Save(svg, "pdf", refused), "unsupported chart export formats are rejected");
        Check(File.ReadAllBytes(refused).SequenceEqual(keep), "rejected chart exports never overwrite an existing destination");
        await Reject(() => Save(svg, "svg", Asset(svg)), "Save As cannot overwrite the immutable chart used by its card");
        await Reject(() => Save(svg, "svg", Asset(png)), "Save As cannot overwrite another card's immutable chart asset");
        Check(File.ReadAllBytes(Asset(svg)).SequenceEqual(svgBytes) && File.ReadAllBytes(Asset(png)).SequenceEqual(pngBytes),
            "rejected internal-file destinations leave both original chart assets intact");

        // The user's original filename may change independently of the captured card.
        byte[] original = File.ReadAllBytes(svgFile);
        try
        {
            File.WriteAllText(svgFile, "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"1\" height=\"1\"><rect width=\"1\" height=\"1\"/></svg>");
            await Save(svg, "svg", svgOutput);
            Check(File.ReadAllBytes(svgOutput).SequenceEqual(savedSvg), "overwriting the user's named output cannot change an earlier card's exported SVG");
        }
        finally { File.WriteAllBytes(svgFile, original); }

        var active = plots.Last(p => !p.TryGetProperty("originalPath", out _));
        byte[] earlier = File.ReadAllBytes(Asset(active));
        string oldOutput = Path.Combine(folder, "earlier-revision.svg");
        await Save(active, "svg", oldOutput);
        byte[] earlierExport = File.ReadAllBytes(oldOutput);
        int before = RFinished(script);
        Call(script, "ShowR", "abline(h=2,col='blue')\n", "Chart revision check", null, true);
        await CompleteRCommand(script, before);
        var revised = RPlots(script).Last(p => Id(p) == Id(active));
        await Save(active, "svg", oldOutput);
        Check(Revision(revised) > Revision(active) && File.ReadAllBytes(oldOutput).SequenceEqual(earlierExport)
            && File.ReadAllBytes(Asset(active)).SequenceEqual(earlier)
            && !File.ReadAllBytes(Asset(revised)).SequenceEqual(earlier),
            "Save As uses the selected older revision even after a later R command updates the active chart");

        before = RFinished(script);
        Call(script, "ShowR", "file.copy('shell-chart-check.svg','same-chart-different-name.svg',overwrite=TRUE)\n", "Identical chart identity check", null, true);
        await CompleteRCommand(script, before);
        var duplicate = RPlots(script).Single(p => p.TryGetProperty("label", out var label) && label.GetString() == "same-chart-different-name.svg");
        Check(Id(duplicate) != Id(svg) && File.ReadAllBytes(Asset(duplicate)).SequenceEqual(svgBytes),
            "two different R chart cards can legitimately contain identical SVG bytes");
        foreach (var (label, expected) in new[] { ("shell-chart-check.svg", svg), ("same-chart-different-name.svg", duplicate) })
        {
            var menu = await OpenRChartMenu(browser, "figure.plot img[alt='" + label + "']");
            Check((string)Field(console, "contextPlotId") == Id(expected)
                && (long)Field(console, "contextPlotRevision") == Revision(expected),
                "right-click selects the exact " + label + " card even when another chart has identical pixels");
            menu.Close();
        }
    }

    static async Task<ContextMenuStrip> OpenRChartMenu(WebView2 browser, string selector)
    {
        var console = browser.Parent!;
        (Field(console, "chartContextMenu") as ContextMenuStrip)?.Close();
        var decodedDeadline = DateTime.UtcNow.AddSeconds(10);
        while (await browser.CoreWebView2.ExecuteScriptAsync("(()=>{const image=document.querySelector(" + JsonSerializer.Serialize(selector)
            + ");return !!image?.complete&&image.naturalWidth>0})()") != "true")
        {
            if (DateTime.UtcNow >= decodedDeadline) throw new TimeoutException("The R chart was not decoded before opening its menu.");
            await Task.Delay(25);
        }
        using var rectangle = JsonDocument.Parse(await browser.CoreWebView2.ExecuteScriptAsync(
            "(()=>{const image=document.querySelector(" + JsonSerializer.Serialize(selector) + ");if(!image?.complete||!image.naturalWidth)throw new Error('R chart is not decoded');image.scrollIntoView({block:'center'});const r=image.getBoundingClientRect();return {x:r.left+r.width/2,y:r.top+r.height/2};})()"));
        foreach (string type in new[] { "mousePressed", "mouseReleased" })
            await browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent", JsonSerializer.Serialize(new
            {
                type, x = rectangle.RootElement.GetProperty("x").GetDouble(), y = rectangle.RootElement.GetProperty("y").GetDouble(),
                button = "right", buttons = type == "mousePressed" ? 2 : 0, clickCount = 1
            }));
        var deadline = DateTime.UtcNow.AddSeconds(10);
        ContextMenuStrip menu;
        do
        {
            menu = Field(console, "chartContextMenu") as ContextMenuStrip;
            if (menu != null && !menu.IsDisposed && menu.Visible) return menu;
            await Task.Delay(25);
        } while (DateTime.UtcNow < deadline);
        throw new TimeoutException("The actual R chart right-click did not display its native image menu.");
    }
}
