using Microsoft.Web.WebView2.WinForms;
using StatsDirect.UI;
using System.Collections;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

internal static partial class Program
{
    static async Task CheckChartViewer(frmMain main)
    {
        var host = await NewConsole(main);
        await host.Start(); RememberWorker(host);
        var first = (await host.Run("plot(1:3,main='Viewer identity')")).Single(e => Kind(e) == "plot");
        Form original = await OpenViewer(host, first);
        Form repeated = await OpenViewer(host, first);
        Check(ReferenceEquals(original, repeated) && ViewerWindows(host).Length == 1,
            "Open chart reuses the native viewer for the exact retained chart revision");
        var updated = (await host.Run("abline(h=2,col='red')")).Single(e => Kind(e) == "plot");
        Form changed = await OpenViewer(host, updated);
        var other = (await host.Run("plot(1:3,main='Viewer identity')")).Single(e => Kind(e) == "plot");
        Form identical = await OpenViewer(host, other);
        Check(!ReferenceEquals(original, changed) && !ReferenceEquals(original, identical)
            && Text(first, "plotId") == Text(updated, "plotId") && Number(updated, "revision") > Number(first, "revision")
            && Text(other, "plotId") != Text(first, "plotId") && ViewerWindows(host).Length == 3,
            "different revisions and identical-looking independent charts keep distinct native viewers");
        foreach (var form in ViewerWindows(host)) form.Close();

        const string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" id=\"viewer-test\" width=\"1000\" height=\"600\" viewBox=\"0 0 1000 600\"><rect width=\"1000\" height=\"600\" fill=\"white\"/><rect width=\"40\" height=\"40\" fill=\"red\"/><rect x=\"960\" y=\"560\" width=\"40\" height=\"40\" fill=\"blue\"/><circle cx=\"500\" cy=\"300\" r=\"80\" fill=\"green\"/></svg>";
        var fixture = (await host.Run("writeLines(" + JsonSerializer.Serialize(svg)
            + ", 'viewer-fixture.svg', useBytes=TRUE)")).Single(e => Kind(e) == "plot" && Text(e, "label") == "viewer-fixture.svg");
        Form viewer = await OpenViewer(host, fixture);
        viewer.Size = new Size(700, 470);
        await ViewerReady(viewer);
        await WaitViewer(viewer, "(()=>{const s=window.ChartViewer.snapshot();return s.zoom===1&&s.width<=s.viewportWidth+2&&s.height<=s.viewportHeight+2})()");
        var bar = viewer.Controls.OfType<ToolStrip>().Single();
        string[] buttons = bar.Items.OfType<ToolStripButton>().Select(i => i.Name ?? "").ToArray();
        Check(new[] { "RChartZoomOut", "RChartZoomPercent", "RChartZoomIn", "RChartZoomFit" }.All(buttons.Contains),
            "the chart viewer has the compact native minus, percentage, plus and Fit controls");
        var fit = await ViewerSnapshot(viewer);
        Console.WriteLine("Fitted chart layout: " + fit.GetRawText());
        Check(fit.GetProperty("zoom").GetDouble() == 1 && fit.GetProperty("naturalWidth").GetDouble() == 1000
            && fit.GetProperty("naturalHeight").GetDouble() == 600
            && fit.GetProperty("width").GetDouble() <= fit.GetProperty("viewportWidth").GetDouble() + 2
            && fit.GetProperty("height").GetDouble() <= fit.GetProperty("viewportHeight").GetDouble() + 2,
            "a new chart fits as a sharp SVG image with its complete original dimensions retained");

        string exportRoot = Path.Combine(artifacts, "viewer-exports"); Directory.CreateDirectory(exportRoot);
        string svgBefore = Path.Combine(exportRoot, "fit.svg"), pngBefore = Path.Combine(exportRoot, "fit.png");
        await InvokeAsync(viewer, "SaveChartAsync", "svg", svgBefore);
        await InvokeAsync(viewer, "SaveChartAsync", "png", pngBefore);
        byte[] vectorBefore = File.ReadAllBytes(svgBefore), rasterBefore = File.ReadAllBytes(pngBefore);
        CheckFullViewerPng(rasterBefore);

        ((ToolStripButton)bar.Items["RChartZoomIn"]!).PerformClick();
        await WaitViewerZoom(viewer, 1.25);
        await InvokeAsync(viewer, "SetZoomAsync", 4.0);
        await ViewerJS(viewer, "window.ChartViewer.pan(230,170)");
        var zoomed = await ViewerSnapshot(viewer);
        Check(zoomed.GetProperty("zoom").GetDouble() == 4 && zoomed.GetProperty("left").GetDouble() > 0
            && zoomed.GetProperty("top").GetDouble() > 0
            && zoomed.GetProperty("scrollWidth").GetDouble() > zoomed.GetProperty("viewportWidth").GetDouble()
            && zoomed.GetProperty("scrollHeight").GetDouble() > zoomed.GetProperty("viewportHeight").GetDouble()
            && bar.Items["RChartZoomPercent"]!.Text == "400%",
            "native zoom updates its percentage and permits horizontal and vertical panning");
        ViewerKey(viewer, Keys.Control | Keys.OemMinus);
        await WaitViewerZoom(viewer, 3.2);
        ViewerKey(viewer, Keys.Control | Keys.Oemplus);
        await WaitViewerZoom(viewer, 4);
        ViewerKey(viewer, Keys.Control | Keys.D0);
        await WaitViewerZoom(viewer, 1);
        Check(await ViewerJS(viewer, "(()=>{const s=window.ChartViewer.snapshot();return s.left<=1&&s.top<=1})()") == "true",
            "Control plus/minus/zero zoom the chart and Fit restores the complete chart without residual pan");
        await ViewerJS(viewer, "document.getElementById('viewport').dispatchEvent(new WheelEvent('wheel',{deltaY:-100,ctrlKey:true,bubbles:true,cancelable:true,clientX:200,clientY:120}))");
        Check((await ViewerSnapshot(viewer)).GetProperty("zoom").GetDouble() > 1,
            "Control-wheel and trackpad-style zoom gestures enlarge the vector chart");
        await InvokeAsync(viewer, "FitAsync");
        double beforeResize = (await ViewerSnapshot(viewer)).GetProperty("width").GetDouble();
        viewer.Size = new Size(850, 570);
        await WaitViewer(viewer, "window.ChartViewer.snapshot().width>" + beforeResize.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Check((await ViewerSnapshot(viewer)).GetProperty("zoom").GetDouble() == 1,
            "resizing a fitted viewer refits the original chart while keeping its 100 percent baseline");

        await InvokeAsync(viewer, "SetZoomAsync", 4.0);
        viewer.Activate(); ((WebView2)Get(viewer, "Browser")!).Focus();
        await WaitViewerSettled(viewer);
        await ViewerJS(viewer, "(()=>{const v=document.getElementById('viewport');v.scrollLeft=(v.scrollWidth-v.clientWidth)/2;v.scrollTop=(v.scrollHeight-v.clientHeight)/2})()");
        await ViewerJS(viewer, "(()=>{window.__viewerPointers=[];for(const type of ['pointerdown','pointermove','pointerup','gotpointercapture','lostpointercapture'])document.addEventListener(type,e=>window.__viewerPointers.push({type:e.type,x:e.clientX,y:e.clientY,id:e.pointerId,target:e.target.id,trusted:e.isTrusted}),true)})()");
        var beforeDrag = await ViewerSnapshot(viewer);
        int dragX = (int)(beforeDrag.GetProperty("viewportWidth").GetDouble() * .65), dragY = (int)(beforeDrag.GetProperty("viewportHeight").GetDouble() * .65);
        int movedX = (int)(beforeDrag.GetProperty("viewportWidth").GetDouble() * .35), movedY = (int)(beforeDrag.GetProperty("viewportHeight").GetDouble() * .35);
        await ViewerMouse(viewer, "mouseMoved", dragX, dragY, "none", 0);
        await ViewerMouse(viewer, "mousePressed", dragX, dragY, "left", 1);
        await ViewerMouse(viewer, "mouseMoved", movedX, movedY, "left", 1);
        await ViewerMouse(viewer, "mouseReleased", movedX, movedY, "left", 0);
        var afterDrag = await ViewerSnapshot(viewer);
        Console.WriteLine("Chart drag before: " + beforeDrag.GetRawText() + "; after: " + afterDrag.GetRawText()
            + "; events: " + await ViewerJS(viewer, "window.__viewerPointers"));
        Check(afterDrag.GetProperty("left").GetDouble() > beforeDrag.GetProperty("left").GetDouble() + (dragX - movedX) * .7
            && afterDrag.GetProperty("top").GetDouble() > beforeDrag.GetProperty("top").GetDouble() + (dragY - movedY) * .7,
            "dragging the actual browser's pointer pans the enlarged chart in both directions");
        string svgAfter = Path.Combine(exportRoot, "zoomed.svg"), pngAfter = Path.Combine(exportRoot, "zoomed.png");
        await InvokeAsync(viewer, "SaveChartAsync", "svg", svgAfter);
        await InvokeAsync(viewer, "SaveChartAsync", "png", pngAfter);
        var afterSave = await ViewerSnapshot(viewer);
        Check(vectorBefore.SequenceEqual(File.ReadAllBytes(svgAfter)) && SameViewerPng(rasterBefore, File.ReadAllBytes(pngAfter))
            && Math.Abs(afterSave.GetProperty("left").GetDouble() - afterDrag.GetProperty("left").GetDouble()) < 1
            && Math.Abs(afterSave.GetProperty("top").GetDouble() - afterDrag.GetProperty("top").GetDouble()) < 1
            && afterSave.GetProperty("zoom").GetDouble() == 4,
            "saving while zoomed and panned retains the full SVG, pixel-identical independent PNG and unchanged viewing position");
        string otherAsset = Text(other, "path"); byte[] otherAssetBefore = File.ReadAllBytes(otherAsset);
        bool protectedOther = false;
        try { await InvokeAsync(viewer, "SaveChartAsync", "svg", otherAsset); }
        catch (InvalidOperationException) { protectedOther = true; }
        Check(protectedOther && otherAssetBefore.SequenceEqual(File.ReadAllBytes(otherAsset)),
            "viewer Save As rejects another retained chart's path and leaves that chart's immutable bytes unchanged");

        var menu = viewer.GetType().GetFields(Members).Select(f => f.GetValue(viewer)).OfType<ContextMenuStrip>().Single();
        Check(menu.Items.Cast<ToolStripItem>().Select(i => (i.Text ?? "").Replace("&", "")).SequenceEqual(
            new[] { "Copy Chart", "Save Chart as SVG…", "Save Chart as PNG…" }),
            "the chart viewer context menu contains exactly Copy Chart and the SVG/PNG save commands, with no Open item");
        for (int i = 0; i < 3; i++) { menu.Show(viewer, new Point(20, 60)); menu.Close(); }
        Check(!menu.IsDisposed, "closing the viewer's chart menu retains it for subsequent copy and save actions");

        await InvokeAsync(host.View, "ClearOutputAsync");
        await host.TwoFrames();
        Check(!viewer.IsDisposed && await host.JS("document.querySelectorAll('figure.plot').length===0") == "true",
            "clearing inline Console output leaves an open chart viewer and its retained asset available");
        string retained = (string)Get(viewer, "AssetPath")!;
        byte[] retainedBytes = File.ReadAllBytes(retained);
        File.WriteAllText(Text(fixture, "path"), "The original R snapshot was changed by this owned test fixture.");
        await InvokeAsync(viewer, "SaveChartAsync", "svg", Path.Combine(exportRoot, "after-clear.svg"));
        Check(vectorBefore.SequenceEqual(File.ReadAllBytes(Path.Combine(exportRoot, "after-clear.svg")))
            && retainedBytes.SequenceEqual(File.ReadAllBytes(retained)),
            "viewer export uses the retained immutable chart even after its original R file changes");
        File.Delete(Text(fixture, "path"));
        Check(ReferenceEquals(viewer, await OpenViewer(host, fixture)),
            "opening an already retained chart reuses its viewer even after the original R asset is removed");
        await CheckViewerClipboard(viewer, vectorBefore, rasterBefore);

        var pngPlot = (await host.Run("png('viewer-raster.png',width=80,height=40);par(mar=c(0,0,0,0));plot.new();rect(0,0,1,1,col='blue');dev.off()"))
            .Single(e => Kind(e) == "plot" && Text(e, "label") == "viewer-raster.png");
        Form pngViewer = await OpenViewer(host, pngPlot);
        var pngMenu = pngViewer.GetType().GetFields(Members).Select(f => f.GetValue(pngViewer)).OfType<ContextMenuStrip>().Single();
        string nativePng = Path.Combine(exportRoot, "native-png.png");
        await InvokeAsync(pngViewer, "SetZoomAsync", 3.0);
        await InvokeAsync(pngViewer, "SaveChartAsync", "png", nativePng);
        Check(!pngMenu.Items[1].Enabled && File.ReadAllBytes(nativePng).SequenceEqual(File.ReadAllBytes(Text(pngPlot, "path"))),
            "a native PNG viewer disables SVG saving and preserves its original PNG bytes while zoomed");

        var large = (await host.Run("writeLines(c('<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"80\" height=\"40\"><!--',strrep('x',21L*1024L*1024L),'--><rect width=\"80\" height=\"40\" fill=\"red\"/></svg>'),'viewer-large.svg',useBytes=TRUE)"))
            .Single(e => Kind(e) == "plot" && Text(e, "label") == "viewer-large.svg");
        Form largeViewer = await OpenViewer(host, large);
        Check(new FileInfo((string)Get(largeViewer, "AssetPath")!).Length > 20 * 1024 * 1024
            && (await ViewerSnapshot(largeViewer)).GetProperty("naturalWidth").GetDouble() == 80,
            "a large SVG without an inline preview opens through its retained asset without NavigateToString's size limit");
        string largeAsset = (string)Get(largeViewer, "AssetPath")!;
        string pendingPath = Path.Combine(exportRoot, "closed-during-render.png");
        await ViewerJS(viewer, "window.ChartViewer.renderPng=()=>new Promise(resolve=>{window.__pendingViewerRender=resolve})");
        Task pendingSave = (Task)Call(viewer, "SaveChartAsync", "png", pendingPath)!;
        await WaitViewer(viewer, "typeof window.__pendingViewerRender==='function'");
        await host.Restart(); RememberWorker(host);
        Check(viewer.IsDisposed && pngViewer.IsDisposed && largeViewer.IsDisposed && ViewerWindows(host).Length == 0,
            "Restart closes native chart viewers and clears their exact-revision window cache");
        bool pendingRejected = false;
        try { await pendingSave.WaitAsync(TimeSpan.FromSeconds(8)); }
        catch (Exception error) when (error is ObjectDisposedException or InvalidOperationException or TaskCanceledException or COMException) { pendingRejected = true; }
        Check(pendingRejected && !File.Exists(pendingPath),
            "closing a viewer during asynchronous rendering cancels the pending save without writing a stale chart");
        await Wait(() => !File.Exists(retained) && !File.Exists(largeAsset), "Closed viewers did not remove their private retained assets.");
        Check(true, "closed chart viewers clean up only their own retained files");
        bool rejected = false;
        try { await InvokeAsync(viewer, "SaveChartAsync", "png", Path.Combine(exportRoot, "closed.png")); }
        catch (Exception error) when (error is ObjectDisposedException or InvalidOperationException or TaskCanceledException) { rejected = true; }
        Check(rejected && !File.Exists(Path.Combine(exportRoot, "closed.png")),
            "a closed viewer rejects later export requests without publishing or writing a stale chart");

        var disposalPlot = (await host.Run("plot(1:3)")).Single(e => Kind(e) == "plot");
        Form disposalViewer = await OpenViewer(host, disposalPlot);
        Set(host.Document, "Dirty", false); host.Document.Dispose();
        Check(disposalViewer.IsDisposed, "closing an R document disposes its native viewer and browser");
        await CheckPefrViewer(main);
    }

    static async Task CheckPefrViewer(frmMain main)
    {
        var host = await NewConsole(main); await host.Start(); RememberWorker(host);
        string source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "TPaired.R"));
        var agreement = (await host.Run(source, "95% Limits of agreement =", "script")).Single(e => Kind(e) == "plot");
        Form viewer = await OpenViewer(host, agreement);
        string exportRoot = Path.Combine(artifacts, "viewer-exports"); Directory.CreateDirectory(exportRoot);
        string fitSvg = Path.Combine(exportRoot, "pefr-fit.svg"), fitPng = Path.Combine(exportRoot, "pefr-fit.png");
        await InvokeAsync(viewer, "SaveChartAsync", "svg", fitSvg);
        await InvokeAsync(viewer, "SaveChartAsync", "png", fitPng);
        byte[] vector = File.ReadAllBytes(fitSvg), raster = File.ReadAllBytes(fitPng);
        var document = XDocument.Parse(Encoding.UTF8.GetString(vector));
        var strokes = document.Descendants().Where(n => n.Name.LocalName == "path"
            && (n.Attribute("fill")?.Value == "none" || (n.Attribute("style")?.Value ?? "").Contains("fill:none", StringComparison.Ordinal))).ToArray();
        int points = strokes.Count(n => (n.Attribute("d")?.Value ?? "").Contains('C'));
        int redLimits = strokes.Count(n => ((n.Attribute("stroke")?.Value ?? "") + (n.Attribute("style")?.Value ?? ""))
            .Replace(" ", "").Contains("rgb(100%,0%,0%)", StringComparison.Ordinal));
        Check(points == 9 && redLimits == 2,
            "the actual paired-PEFR agreement chart retains its nine unfilled data points and two red limits of agreement");
        await InvokeAsync(viewer, "SetZoomAsync", 3.81);
        await ViewerJS(viewer, "window.ChartViewer.pan(9999,9999)");
        var before = await ViewerSnapshot(viewer);
        Check(before.GetProperty("zoom").GetDouble() == 3.81 && before.GetProperty("left").GetDouble() > 0
            && before.GetProperty("top").GetDouble() > 0,
            "the actual PEFR chart is substantially zoomed and panned in both axes before testing its actions");
        string zoomSvg = Path.Combine(exportRoot, "pefr-zoomed.svg"), zoomPng = Path.Combine(exportRoot, "pefr-zoomed.png");
        await InvokeAsync(viewer, "SaveChartAsync", "svg", zoomSvg);
        await InvokeAsync(viewer, "SaveChartAsync", "png", zoomPng);
        var after = await ViewerSnapshot(viewer);
        Check(vector.SequenceEqual(File.ReadAllBytes(zoomSvg)) && SameViewerPng(raster, File.ReadAllBytes(zoomPng))
            && after.GetProperty("zoom").GetDouble() == before.GetProperty("zoom").GetDouble()
            && Math.Abs(after.GetProperty("left").GetDouble() - before.GetProperty("left").GetDouble()) < 1
            && Math.Abs(after.GetProperty("top").GetDouble() - before.GetProperty("top").GetDouble()) < 1,
            "PEFR SVG/PNG saving at 381 percent preserves every point, limit and caption geometry with pixel-identical full output and unchanged pan");
        await CheckViewerClipboard(viewer, vector, raster);
        File.WriteAllText(Path.Combine(artifacts, "pefr-viewer-manual.json"), JsonSerializer.Serialize(new
        {
            RawSVG = Text(agreement, "path"), FitSVG = fitSvg, FitPNG = fitPng,
            ZoomedSVG = zoomSvg, ZoomedPNG = zoomPng, Points = points, RedLimits = redLimits,
            Zoom = after.GetProperty("zoom").GetDouble(), PanX = after.GetProperty("left").GetDouble(), PanY = after.GetProperty("top").GetDouble(),
            Note = "Actual R help recipe; these retained paths can seed a separate owned viewer for native SaveFileDialog UI verification."
        }, new JsonSerializerOptions { WriteIndented = true }));
        viewer.Close(); host.Document.Activate();
    }

    static Form[] ViewerWindows(ConsoleHost host) => ((IDictionary)Get(host.View, "chartWindows")!).Values.Cast<Form>().ToArray();
    static async Task<Form> OpenViewer(ConsoleHost host, JsonElement plot)
    {
        Call(host.View, "OpenPlot", Text(plot, "plotId"), (long?)Number(plot, "revision"));
        Form viewer = (Form)((IDictionary)Get(host.View, "chartWindows")!)[Text(plot, "plotId") + ":" + Number(plot, "revision")]!;
        await ViewerReady(viewer);
        return viewer;
    }
    static async Task ViewerReady(Form viewer) => await ((Task)Get(viewer, "Ready")!).WaitAsync(TimeSpan.FromSeconds(45));
    static async Task<string> ViewerJS(Form viewer, string source) => await ((WebView2)Get(viewer, "Browser")!).CoreWebView2.ExecuteScriptAsync(source);
    static async Task<JsonElement> ViewerSnapshot(Form viewer) => JsonSerializer.Deserialize<JsonElement>(await ViewerJS(viewer, "window.ChartViewer.snapshot()"));
    static void ViewerKey(Form viewer, Keys keys)
    {
        var message = Message.Create(viewer.Handle, 0x0100, (nint)(keys & Keys.KeyCode), 0);
        if (!(bool)Call(viewer, "ProcessCmdKey", message, keys)!) throw new InvalidOperationException("The native viewer did not handle " + keys);
    }
    static Task ViewerMouse(Form viewer, string type, int x, int y, string button, int buttons) =>
        ((WebView2)Get(viewer, "Browser")!).CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent",
            JsonSerializer.Serialize(new { type, x, y, button, buttons, clickCount = 1 }));
    static async Task WaitViewerZoom(Form viewer, double zoom) => await WaitViewer(viewer, "Math.abs(window.ChartViewer.snapshot().zoom-" + zoom.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")<0.001");
    static async Task WaitViewer(Form viewer, string condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (await ViewerJS(viewer, condition) != "true")
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("The chart viewer did not reach: " + condition);
            await Task.Delay(30);
        }
    }
    static async Task WaitViewerSettled(Form viewer)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        string previous = ""; long changed = 0;
        while (watch.ElapsedMilliseconds < 15000)
        {
            string geometry = await ViewerJS(viewer, "(()=>{const s=ChartViewer.snapshot();return {width:s.width,height:s.height,vw:s.viewportWidth,vh:s.viewportHeight,sw:s.scrollWidth,sh:s.scrollHeight,focused:document.hasFocus()}})()");
            if (geometry != previous) { previous = geometry; changed = watch.ElapsedMilliseconds; }
            if (watch.ElapsedMilliseconds - changed >= 250 && await ViewerJS(viewer, "document.hasFocus()") == "true") return;
            await Task.Delay(30);
        }
        throw new TimeoutException("The activated chart viewer did not reach stable layout: " + previous);
    }
    static void CheckFullViewerPng(byte[] png)
    {
        using var stream = new MemoryStream(png); using var bitmap = new Bitmap(stream);
        Color red = bitmap.GetPixel(10, 10), blue = bitmap.GetPixel(bitmap.Width - 11, bitmap.Height - 11), green = bitmap.GetPixel(bitmap.Width / 2, bitmap.Height / 2);
        Check(bitmap.Width == 2000 && bitmap.Height == 1200 && red.R > 240 && red.G < 15 && red.B < 15
            && blue.B > 240 && blue.R < 15 && blue.G < 15 && green.G > 100 && green.R < 15 && green.B < 15,
            "PNG export rasterizes the full original vector at two times its natural dimensions, including both opposite corners");
    }

    [DllImport("user32.dll", EntryPoint = "GetClipboardSequenceNumber")]
    static extern uint ViewerClipboardSequence();
    static async Task CheckViewerClipboard(Form viewer, byte[] expectedSvg, byte[] expectedPng)
    {
        if (Environment.GetEnvironmentVariable("STATSDIRECT_SKIP_CLIPBOARD") == "1") { Console.WriteLine("SKIP chart viewer clipboard checks by request"); return; }
        uint originalSequence = ViewerClipboardSequence(), ownedSequence = 0;
        var saved = new DataObject(); var retained = new List<IDisposable>();
        bool hadClipboard;
        try
        {
            var original = Clipboard.GetDataObject(); hadClipboard = original != null;
            if (original != null) foreach (string format in original.GetFormats(false))
            {
                if (format == DataFormats.Bitmap || format == typeof(Bitmap).FullName)
                {
                    if (saved.GetDataPresent(DataFormats.Bitmap)) continue;
                    var image = (original.GetData(DataFormats.Bitmap) as Image ?? Clipboard.GetImage())?.Clone() as Image
                        ?? throw new InvalidOperationException("Unavailable clipboard image");
                    saved.SetData(DataFormats.Bitmap, true, image); retained.Add(image); continue;
                }
                object value = original.GetData(format, false) ?? throw new InvalidOperationException("Unavailable clipboard format: " + format);
                value = value switch
                {
                    MemoryStream memory => new MemoryStream(memory.ToArray()), Image image => image.Clone(),
                    byte[] bytes => bytes.ToArray(), string[] strings => strings.ToArray(),
                    string or bool or char or int or long or float or double or decimal => value,
                    _ => throw new InvalidOperationException("Opaque clipboard format: " + format)
                };
                saved.SetData(format, false, value); if (value is IDisposable resource) retained.Add(resource);
            }
        }
        catch (Exception error)
        {
            foreach (var resource in retained) resource.Dispose();
            Console.WriteLine("SKIP chart viewer physical clipboard checks: " + error.Message); return;
        }
        try
        {
            if (ViewerClipboardSequence() != originalSequence) { Console.WriteLine("SKIP chart viewer copy: another application changed the clipboard"); return; }
            var beforeCopy = await ViewerSnapshot(viewer);
            var menu = viewer.GetType().GetFields(Members).Select(f => f.GetValue(viewer)).OfType<ContextMenuStrip>().Single();
            menu.Items[0].PerformClick();
            await Wait(() => ViewerClipboardSequence() != originalSequence, "The native viewer's Copy Chart menu did not publish a picture.");
            uint observedSequence = ViewerClipboardSequence();
            var copied = Clipboard.GetDataObject()!;
            if (copied.GetData(DataFormats.UnicodeText) as string != viewer.Text
                || !copied.GetDataPresent("StatsDirect.ReportFragment.HTML.v1", false))
            { Console.WriteLine("SKIP chart viewer copy capture: another application supplied the clipboard"); return; }
            byte[] svg = ClipboardBytes(copied, "image/svg+xml"), png = ClipboardBytes(copied, "PNG");
            if (ViewerClipboardSequence() != observedSequence)
            { Console.WriteLine("SKIP chart viewer copy capture: the clipboard changed while being read"); return; }
            ownedSequence = observedSequence;
            var afterCopy = await ViewerSnapshot(viewer);
            string expectedViewBox = XDocument.Parse(Encoding.UTF8.GetString(expectedSvg)).Root!.Attribute("viewBox")!.Value;
            Check(ownedSequence != originalSequence && copied.GetDataPresent(DataFormats.Bitmap)
                && XDocument.Parse(Encoding.UTF8.GetString(svg).TrimEnd('\0')).Root!.Attribute("viewBox")!.Value == expectedViewBox
                && Encoding.UTF8.GetString(svg).TrimEnd('\0') == Encoding.UTF8.GetString(expectedSvg)
                && SameViewerPng(png, expectedPng)
                && afterCopy.GetProperty("zoom").GetDouble() == beforeCopy.GetProperty("zoom").GetDouble()
                && Math.Abs(afterCopy.GetProperty("left").GetDouble() - beforeCopy.GetProperty("left").GetDouble()) < 1
                && Math.Abs(afterCopy.GetProperty("top").GetDouble() - beforeCopy.GetProperty("top").GetDouble()) < 1,
                "actual Copy Chart menu while zoomed and panned publishes the complete retained SVG plus pixel-identical full PNG and bitmap formats without changing the view");
        }
        finally
        {
            if (ownedSequence != 0 && ViewerClipboardSequence() == ownedSequence)
            {
                var current = Clipboard.GetDataObject();
                if (current?.GetData(DataFormats.UnicodeText) as string == viewer.Text
                    && current.GetDataPresent("StatsDirect.ReportFragment.HTML.v1", false)
                    && ViewerClipboardSequence() == ownedSequence)
                {
                    if (hadClipboard) Clipboard.SetDataObject(saved, true, 5, 100); else Clipboard.Clear();
                }
            }
            foreach (var resource in retained) resource.Dispose();
        }
    }
    static byte[] ClipboardBytes(IDataObject data, string format) => data.GetData(format, false) switch
    {
        MemoryStream stream => stream.ToArray(), byte[] bytes => bytes, string text => Encoding.UTF8.GetBytes(text),
        _ => throw new InvalidOperationException("Copy supplied no " + format)
    };
    static bool SameViewerPng(byte[] first, byte[] second)
    {
        static (Size Size, byte[] Pixels) Pixels(byte[] png)
        {
            using var stream = new MemoryStream(png); using var decoded = new Bitmap(stream);
            using var image = new Bitmap(decoded.Width, decoded.Height, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(image)) graphics.DrawImageUnscaled(decoded, 0, 0);
            var data = image.LockBits(new Rectangle(Point.Empty, image.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                byte[] result = new byte[image.Width * image.Height * 4];
                for (int row = 0; row < image.Height; row++) Marshal.Copy(data.Scan0 + row * data.Stride, result, row * image.Width * 4, image.Width * 4);
                return (image.Size, result);
            }
            finally { image.UnlockBits(data); }
        }
        var a = Pixels(first); var b = Pixels(second); return a.Size == b.Size && a.Pixels.SequenceEqual(b.Pixels);
    }
}
