using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using StatsDirect.UI;

internal static partial class Program
{
    [DllImport("user32.dll", EntryPoint = "GetClipboardSequenceNumber")]
    private static extern uint ImagePasteClipboardSequence();

    static async Task CheckScriptImagePaste(frmMain main, WebView2 source, string svgFile, string pngFile)
    {
        Type converter = typeof(frmMain).Assembly.GetType("StatsDirect.UI.WebReports.ReportClipboard")!;
        string ImageHtml(IDataObject data) => (string)converter.GetMethod("ImageHtml", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { data })!;
        using var picture = new Bitmap(17, 11);
        using (var drawing = Graphics.FromImage(picture)) drawing.Clear(Color.FromArgb(255, 30, 90, 180));
        using var png = new MemoryStream(); picture.Save(png, ImageFormat.Png);
        using var pngData = new MemoryStream(png.ToArray());
        var pngOnly = new DataObject(); pngOnly.SetData("PNG", false, pngData);
        var bitmapOnly = new DataObject(); bitmapOnly.SetData(DataFormats.Bitmap, false, picture);
        foreach (var (name, data) in new[] { ("PNG-only", pngOnly), ("Bitmap-only", bitmapOnly) })
        {
            string html = ImageHtml(data);
            byte[] bytes = Convert.FromBase64String(html.Split("base64,", StringSplitOptions.None)[1].Split('"')[0]);
            using var stream = new MemoryStream(bytes); using var decoded = new Bitmap(stream);
            Check(decoded.Width == 17 && decoded.Height == 11 && decoded.GetPixel(5, 5).ToArgb() == picture.GetPixel(5, 5).ToArgb(),
                name + " clipboard content converts to a self-contained PNG without HTML or text");
        }
        var svgOnly = new DataObject(); svgOnly.SetData("image/svg+xml", false, File.ReadAllText(svgFile));
        Check(ImageHtml(svgOnly).Contains("<svg", StringComparison.OrdinalIgnoreCase), "SVG-only clipboard content retains vector markup");
        const string legacySvg = "<?xml version=\"1.0\"?><!DOCTYPE svg PUBLIC \"-//W3C//DTD SVG 1.1//EN\" \"http://www.w3.org/Graphics/SVG/1.1/DTD/svg11.dtd\"><svg xmlns=\"http://www.w3.org/2000/svg\" width=\"20\" height=\"10\"><rect width=\"20\" height=\"10\" fill=\"red\"/></svg>";
        var svgUnicode = new DataObject();
        svgUnicode.SetData("image/svg+xml", false, new MemoryStream(System.Text.Encoding.Unicode.GetPreamble()
            .Concat(System.Text.Encoding.Unicode.GetBytes(legacySvg)).ToArray()));
        Check(ImageHtml(svgUnicode).Contains("<rect") && !ImageHtml(svgUnicode).Contains("DOCTYPE"),
            "UTF-16 SVG clipboard data accepts a legacy doctype without loading its external DTD");

        // Materialise every original format before writing anything. Unsupported
        // delayed/opaque payloads skip physical checks instead of risking user data.
        IDataObject original; var saved = new DataObject(); var retained = new List<IDisposable>();
        uint originalSequence = ImagePasteClipboardSequence(), ownedSequence = 0;
        try
        {
            original = Clipboard.GetDataObject();
            if (original != null)
                foreach (string format in original.GetFormats(false))
                {
                    object value = original.GetData(format, false) ?? throw new InvalidOperationException("Unavailable clipboard format: " + format);
                    value = value switch
                    {
                        MemoryStream memory => new MemoryStream(memory.ToArray()),
                        Image image => image.Clone(),
                        byte[] bytes => bytes.ToArray(),
                        string[] strings => strings.ToArray(),
                        string or bool or char or int or long or float or double or decimal => value,
                        _ => throw new InvalidOperationException("Unsupported clipboard format: " + format)
                    };
                    saved.SetData(format, false, value);
                    if (value is IDisposable resource) retained.Add(resource);
                }
        }
        catch (Exception ex)
        {
            foreach (var resource in retained) resource.Dispose();
            Console.WriteLine("SKIP physical image clipboard checks: could not preserve the existing clipboard (" + ex.Message + ").");
            return;
        }

        string folder = Path.Combine(Path.GetTempPath(), "StatsDirect-image-paste-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        async Task WriteClipboard(IDataObject data)
        {
            for (int attempt = 0; ; attempt++)
                try { Clipboard.SetDataObject(data, true, 0, 0); ownedSequence = ImagePasteClipboardSequence(); return; }
                catch (ExternalException) when (attempt < 40) { await Task.Delay(50); }
        }
        try
        {
            foreach (string kind in new[] { "R-SVG", "R-PNG", "SVG-only", "PNG-only", "Bitmap-only" })
            {
                if (ImagePasteClipboardSequence() != (ownedSequence == 0 ? originalSequence : ownedSequence))
                { Console.WriteLine("SKIP remaining physical image clipboard checks: another application changed the clipboard."); break; }
                if (kind.StartsWith("R-", StringComparison.Ordinal))
                {
                    source.FindForm()?.Activate(); source.Focus();
                    uint before = ImagePasteClipboardSequence();
                    await CopyRChartImage(source, kind == "R-SVG" ? "img.svg-chart[alt='shell-chart-check.svg']" : "img:not(.svg-chart)[alt='shell-chart-check.png']");
                    var until = DateTime.UtcNow.AddSeconds(10);
                    bool payloadReady = false;
                    do
                    {
                        try
                        {
                            var copied = Clipboard.GetDataObject();
                            payloadReady = ImagePasteClipboardSequence() != before && (kind == "R-SVG"
                                ? copied?.GetData("StatsDirect.ReportFragment.HTML.v1", false) is string vectorFragment && vectorFragment.Contains("<svg", StringComparison.OrdinalIgnoreCase)
                                : copied?.GetDataPresent("PNG", false) == true || copied?.GetDataPresent(DataFormats.Bitmap) == true);
                        }
                        catch (ExternalException) { }
                        if (!payloadReady) await Task.Delay(30);
                    } while (!payloadReady && DateTime.UtcNow < until);
                    ownedSequence = ImagePasteClipboardSequence();
                    var contents = Clipboard.GetDataObject();
                    Console.WriteLine("      " + kind + " clipboard formats: " + string.Join(", ", contents?.GetFormats(false).Select(format =>
                    {
                        try { return format + "=" + (contents.GetData(format, false)?.GetType().FullName ?? "null"); }
                        catch (Exception ex) { return format + "=" + ex.GetType().Name; }
                    }) ?? Array.Empty<string>()));
                    if (!payloadReady)
                    {
                        string output = (source.FindForm() == null ? source.Parent : (Control)Field(source.FindForm()!, "outputPane"))?.Text ?? "";
                        Console.WriteLine("      R chart copy notices: " + string.Join(" | ", output.Split('\n').Where(line => line.Contains("Could not copy chart", StringComparison.Ordinal))));
                    }
                    Check(ownedSequence != before, kind + (kind == "R-SVG"
                        ? " offers its SVG copy command and its production handler publishes the clipboard"
                        : " uses the real output browser's native Copy image command"));
                    if (kind == "R-SVG")
                        Check(Clipboard.GetDataObject()?.GetData("StatsDirect.ReportFragment.HTML.v1", false) is string fragment
                            && fragment.Contains("<svg", StringComparison.OrdinalIgnoreCase),
                            "R SVG Copy image provides the private vector report fragment");
                }
                else await WriteClipboard(kind == "SVG-only" ? svgOnly : kind == "PNG-only" ? pngOnly : bitmapOnly);

                Form report = (Form)Call(main, "CreateReport");
                try
                {
                    object view = Field(report, "view");
                    await ((Task)Get(view, "Ready")).WaitAsync(TimeSpan.FromSeconds(45));
                    var browser = (WebView2)Field(view, "Browser");
                    await (Task)Call(view, "CallAsync", "load", new { entries = Array.Empty<object>() });
                    await browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-command=addText]').click()");
                    await browser.CoreWebView2.ExecuteScriptAsync("(()=>{const p=document.querySelector('#results .report-entry p');document.getElementById('results').focus();const r=document.createRange();r.selectNodeContents(p);getSelection().removeAllRanges();getSelection().addRange(r);})()");
                    await (Task)Call(view, "ClipboardCommandAsync", "paste");
                    bool vector = kind is "R-SVG" or "SVG-only";
                    string imageSelector = vector ? "#results svg" : "#results img[src^='data:image/png;base64,']";
                    async Task<bool> Present() => await browser.CoreWebView2.ExecuteScriptAsync("!!document.querySelector(" + JsonSerializer.Serialize(imageSelector) + ")") == "true";
                    Check(await Present(), kind + " pastes into the newly added report text area as " + (vector ? "vector SVG" : "an embedded picture"));
                    foreach (var (key, code) in new[] { ("z", 90), ("y", 89) })
                    {
                        report.Activate(); browser.Focus();
                        await browser.CoreWebView2.ExecuteScriptAsync("document.getElementById('results').focus({preventScroll:true})");
                        foreach (string type in new[] { "keyDown", "keyUp" })
                            await browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent", JsonSerializer.Serialize(new { type, key, windowsVirtualKeyCode = code, modifiers = 2 }));
                        await (Task)Call(view, "CallAsync", "snapshot", null);
                        var changedBy = DateTime.UtcNow.AddSeconds(10);
                        while (await Present() != (key == "y") && DateTime.UtcNow < changedBy) await Task.Delay(30);
                        Check(await Present() == (key == "y"), kind + (key == "z" ? " image paste is undone in one step" : " image paste can be redone"));
                    }
                    string savedPath = Path.Combine(folder, kind + ".html");
                    await (Task)Call(view, "SaveAsync", savedPath, "Pasted image", "html");
                    string savedHtml = File.ReadAllText(savedPath);
                    Check(savedHtml.Contains(vector ? "<svg" : "data:image/png;base64,", StringComparison.OrdinalIgnoreCase)
                        && !savedHtml.Contains(new Uri(svgFile).AbsoluteUri) && !savedHtml.Contains(new Uri(pngFile).AbsoluteUri),
                        kind + " saves its own picture without an R-folder file dependency");
                    string sourcePath = kind == "R-SVG" ? svgFile : kind == "R-PNG" ? pngFile : null;
                    string heldPath = sourcePath == null ? null : Path.Combine(folder, Path.GetFileName(sourcePath));
                    try
                    {
                        if (sourcePath != null) File.Move(sourcePath, heldPath);
                        await (Task)Call(view, "OpenHtmlAsync", savedHtml, "Reopened image");
                        Check(await Present(), kind + " survives saving and reopening after its source file is unavailable");
                        string loaded = vector
                            ? "document.querySelector('#results svg').querySelectorAll('path,rect,line,circle,polyline,polygon,text,use').length>0"
                            : "(()=>{const i=document.querySelector('#results img');return i.complete&&i.naturalWidth>0&&i.naturalHeight>0;})()";
                        var until = DateTime.UtcNow.AddSeconds(10);
                        bool rendered;
                        do { rendered = await browser.CoreWebView2.ExecuteScriptAsync(loaded) == "true"; if (!rendered) await Task.Delay(30); }
                        while (!rendered && DateTime.UtcNow < until);
                        Check(rendered, kind + " reopened picture renders independently of its source file");
                    }
                    finally { if (heldPath != null && File.Exists(heldPath)) File.Move(heldPath, sourcePath); }
                }
                finally { await CloseReportForTest(report); }
            }
        }
        finally
        {
            if (ownedSequence != 0 && ImagePasteClipboardSequence() == ownedSequence)
            {
                if (original == null || saved.GetFormats(false).Length == 0)
                    for (int attempt = 0; ; attempt++)
                        try { Clipboard.Clear(); break; } catch (ExternalException) when (attempt < 40) { await Task.Delay(50); }
                else await WriteClipboard(saved);
            }
            foreach (var resource in retained) resource.Dispose();
            foreach (string file in Directory.GetFiles(folder)) File.Delete(file);
            Directory.Delete(folder);
        }
    }

    private static async Task CopyRChartImage(WebView2 browser, string selector)
    {
        var selected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async void Copy(object sender, CoreWebView2ContextMenuRequestedEventArgs args)
        {
            var deferral = args.GetDeferral();
            try
            {
                bool svg = selector.StartsWith("img.svg-chart[", StringComparison.Ordinal);
                bool CustomImage(CoreWebView2ContextMenuItem item) => item.Kind == CoreWebView2ContextMenuItemKind.Command
                    && !string.IsNullOrWhiteSpace(item.Label) && item.Name == item.Label;
                if (svg)
                {
                    // WebView2 can omit data: SourceUri. The real pane then resolves
                    // its DOM target asynchronously before replacing Copy image.
                    var until = DateTime.UtcNow.AddSeconds(5);
                    while (!args.MenuItems.Any(CustomImage) && DateTime.UtcNow < until) await Task.Delay(20);
                }
                string sourceUri = args.ContextMenuTarget.HasSourceUri ? args.ContextMenuTarget.SourceUri : "";
                Console.WriteLine("      R chart copy menu: HasSourceUri=" + args.ContextMenuTarget.HasSourceUri + "; source="
                    + sourceUri[..Math.Min(sourceUri.Length, 80)] + "; items="
                    + string.Join(" | ", args.MenuItems.Select(m => m.Name + "/" + m.Label + "/" + m.CommandId)));
                var item = args.MenuItems.FirstOrDefault(m => svg ? CustomImage(m) : m.Name == "copyImage");
                args.Handled = true;
                if (item == null) throw new InvalidOperationException("Copy image is missing from the R chart menu.");
                if (svg)
                {
                    // WebView2 does not dispatch CustomItemSelected when a test
                    // supplies SelectedCommandId. Exercise its registered command's
                    // production handler after verifying that the real menu offers it.
                    string uri = JsonSerializer.Deserialize<string>(await browser.CoreWebView2.ExecuteScriptAsync("contextChartSource()"))!;
                    await (Task)Call(browser.Parent!, "CopySvgChartAsync", uri);
                }
                else args.SelectedCommandId = item.CommandId;
                Console.WriteLine("      selected R chart command: " + item.Name + "/" + item.Label + "/" + item.CommandId);
                selected.TrySetResult();
            }
            catch (Exception ex) { selected.TrySetException(ex); }
            finally { deferral.Complete(); }
        }
        browser.CoreWebView2.ContextMenuRequested += Copy;
        try
        {
            using var rectangle = JsonDocument.Parse(await browser.CoreWebView2.ExecuteScriptAsync("(()=>{const i=document.querySelector(" + JsonSerializer.Serialize(selector) + ");i.scrollIntoView({block:'center'});const r=i.getBoundingClientRect();return {x:r.left+r.width/2,y:r.top+r.height/2};})()"));
            await Task.Delay(75);
            foreach (string type in new[] { "mousePressed", "mouseReleased" })
                await browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent", JsonSerializer.Serialize(new { type, x = rectangle.RootElement.GetProperty("x").GetDouble(), y = rectangle.RootElement.GetProperty("y").GetDouble(), button = "right", buttons = type == "mousePressed" ? 2 : 0, clickCount = 1 }));
            await selected.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally { browser.CoreWebView2.ContextMenuRequested -= Copy; }
    }
}
