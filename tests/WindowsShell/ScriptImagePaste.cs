using System.Drawing.Imaging;
using System.Text;
using System.Text.RegularExpressions;
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
        if (Environment.GetEnvironmentVariable("STATSDIRECT_SKIP_CLIPBOARD") == "1") { Console.WriteLine("SKIP ScriptImagePaste clipboard checks (STATSDIRECT_SKIP_CLIPBOARD=1 preserves the user's clipboard)"); return; }
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
                    // A bitmap from another process comes only through the converting read; it goes back as the one Bitmap format.
                    if (format == DataFormats.Bitmap || format == typeof(Bitmap).FullName)
                    {
                        if (saved.GetDataPresent(DataFormats.Bitmap)) continue;
                        // a plain CF_BITMAP from a Win32 write comes only through GetImage, as the report window knows
                    Image kept = (original.GetData(DataFormats.Bitmap) as Image ?? Clipboard.GetImage())?.Clone() as Image ?? throw new InvalidOperationException("Unavailable clipboard format: " + format);
                        saved.SetData(DataFormats.Bitmap, true, kept); retained.Add(kept); continue;
                    }
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
                    bool integrated = source.Parent?.GetType().Name == "RConsoleView";
                    await CopyRChartImage(source, integrated
                        ? "figure.plot img[alt='shell-chart-check." + (kind == "R-SVG" ? "svg" : "png") + "']"
                        : kind == "R-SVG" ? "img.svg-chart[alt='shell-chart-check.svg']" : "img:not(.svg-chart)[alt='shell-chart-check.png']");
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
                    {
                        // As a chart copied from a report: the private fragment with R's root in points (a report keeps the pane's size),
                        // the SVG form with plain numbers, the PNG, the bitmap, the HTML with the PNG, and the text, published the
                        // report's way, which Office pastes as the SVG with the rasters in Paste Special
                        Check(Clipboard.GetDataObject()?.GetData("StatsDirect.ReportFragment.HTML.v1", false) is string fragment
                            && fragment.StartsWith("<svg", StringComparison.Ordinal) && fragment.Contains("width=\"360pt\"", StringComparison.Ordinal)
                            && Clipboard.GetDataObject()?.GetData("image/svg+xml", false) is MemoryStream svgStream
                            && Encoding.UTF8.GetString(svgStream.ToArray()).Contains("<svg", StringComparison.OrdinalIgnoreCase)
                            && Clipboard.GetDataObject()?.GetDataPresent("PNG", false) == true && Clipboard.ContainsImage()
                            && Clipboard.GetDataObject()?.GetData(DataFormats.Html) is string html && html.Contains("data:image/png", StringComparison.Ordinal)
                            && Clipboard.GetDataObject()?.GetDataPresent(DataFormats.UnicodeText) == true,
                            "R SVG Copy image is published as a report's chart copy: fragment, SVG form, PNG, bitmap, HTML and text");
                        // R's labels as positioned paths, not glyph references, for PowerPoint's conversion of the graphic to shapes
                        Check(Clipboard.GetDataObject()?.GetData("StatsDirect.ReportFragment.HTML.v1", false) is string flatFragment
                            && !flatFragment.Contains("<use ", StringComparison.Ordinal) && flatFragment.Contains("<g transform=\"translate(", StringComparison.Ordinal)
                            && Clipboard.GetDataObject()?.GetData("image/svg+xml", false) is MemoryStream flatSvg
                            && !Encoding.UTF8.GetString(flatSvg.ToArray()).Contains("<use ", StringComparison.Ordinal),
                            "R SVG Copy image carries its labels as positioned paths, with no glyph references, in the fragment and the SVG form");
                        // The SVG form's root is sized in plain numbers, the viewBox's, each attribute left where R wrote it, xmlns first:
                        // PowerPoint's Paste command took a root sized in points as the PNG, and refused the size moved to the front.
                        string svgRoot = Clipboard.GetDataObject()?.GetData("image/svg+xml", false) is MemoryStream svgForm
                            ? Regex.Match(Encoding.UTF8.GetString(svgForm.ToArray()), "<svg\\b[^>]*>").Value : "";
                        Check(svgRoot.StartsWith("<svg xmlns=\"http://www.w3.org/2000/svg\"", StringComparison.Ordinal) && svgRoot.Contains("viewBox=\"0 0 360 288\"", StringComparison.Ordinal) && svgRoot.Contains(" width=\"360\"", StringComparison.Ordinal)
                            && svgRoot.Contains(" height=\"288\"", StringComparison.Ordinal) && !svgRoot.Contains("pt\"", StringComparison.Ordinal),
                            "the R SVG copy's SVG form is sized in its viewBox's plain numbers");
                        // A RichEdit paste, as the script editor's, takes the copy's bitmap as its picture, as a report's chart copy gives
                        // it, rather than embedding anything as an object.
                        using (var editor = new RichTextBox())
                        {
                            _ = editor.Handle;
                            editor.Paste();
                            Check(editor.Rtf.Contains("\\pict", StringComparison.Ordinal) && !editor.Rtf.Contains("\\object", StringComparison.Ordinal),
                                "a RichEdit paste of the R SVG copy inserts its picture, not an embedded object");
                        }
                    }
                    string probe = Environment.GetEnvironmentVariable("STATSDIRECT_PASTE_PROBE");
                    if (!string.IsNullOrEmpty(probe))
                    {
                        // An opt-in probe of another application's paste while the clipboard holds this copy: a PowerShell script, such
                        // as one driving PowerPoint and Word through COM (the developer's tools, outside the repository), that exits 0 when
                        // both pastes keep a vector, 2 when either is a raster, 4 when Office is absent, and prints a line "PowerPoint: vector"
                        // or "PowerPoint: raster only", and the same for Word. The SVG copy must be a vector in PowerPoint (Word's COM paste
                        // takes the HTML form); the native PNG copy shows the probe telling a raster apart.
                        var start = new System.Diagnostics.ProcessStartInfo("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -File \"" + probe + "\"")
                        { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
                        using var run = System.Diagnostics.Process.Start(start)!;
                        Task<string> output = run.StandardOutput.ReadToEndAsync(), errors = run.StandardError.ReadToEndAsync();
                        using var patience = new CancellationTokenSource(TimeSpan.FromMinutes(3));
                        try { await run.WaitForExitAsync(patience.Token); }
                        catch (OperationCanceledException) { run.Kill(entireProcessTree: true); }
                        string probeOutput = await output + await errors;
                        Console.WriteLine("      " + kind + " paste probe (exit " + run.ExitCode + "): " + probeOutput.Trim().Replace("\n", "\n      "));
                        if (run.ExitCode == 4) Console.WriteLine("SKIP paste probe: Office is not available here.");
                        else
                        {
                            Check(run.ExitCode is 0 or 2, "the paste probe " + Path.GetFileName(probe) + " completes its pastes (exit " + run.ExitCode + ")");
                            // PowerPoint takes the SVG copy as a vector (its own Paste does too); Word's COM paste takes the HTML form the
                            // report-style copy carries, which its own Paste may not: the probe reports it, the check does not require it
                            Check(kind == "R-SVG" ? probeOutput.Contains("PowerPoint: vector", StringComparison.Ordinal) : run.ExitCode == 2, kind == "R-SVG"
                                ? "the paste probe " + Path.GetFileName(probe) + " receives the R SVG copy as a vector in PowerPoint"
                                : "the paste probe " + Path.GetFileName(probe) + " reports the native PNG copy as a raster");
                        }
                    }
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
                    if (kind == "R-SVG")
                        Check(await browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('#results svg').getAttribute('width')") == "\"360pt\"",
                            "R-SVG pastes into the report at the size the pane shows, its root in R's points");
                    foreach (var (key, code) in new[] { ("z", 90), ("y", 89) })
                    {
                        report.Activate(); browser.Focus();
                        await browser.CoreWebView2.ExecuteScriptAsync("window.__imagePasteKeys=[];document.addEventListener('keydown',e=>window.__imagePasteKeys.push({key:e.key,ctrl:e.ctrlKey,target:e.target.id,prevented:e.defaultPrevented}),{once:true});");
                        await browser.CoreWebView2.ExecuteScriptAsync("document.getElementById('results').focus({preventScroll:true})");
                        foreach (string type in new[] { "keyDown", "keyUp" })
                            await browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent", JsonSerializer.Serialize(new { type, key, windowsVirtualKeyCode = code, modifiers = 2 }));
                        string received = await browser.CoreWebView2.ExecuteScriptAsync("JSON.stringify(window.__imagePasteKeys)");
                        using (var events = JsonDocument.Parse(JsonSerializer.Deserialize<string>(received)!))
                            Check(events.RootElement.EnumerateArray().Any(e => e.GetProperty("key").GetString() == key && e.GetProperty("ctrl").GetBoolean() && e.GetProperty("prevented").GetBoolean()),
                                kind + " report receives and handles the native Ctrl+" + key.ToUpperInvariant() + " shortcut: " + received);
                        await (Task)Call(view, "CallAsync", "snapshot", null);
                        var changedBy = DateTime.UtcNow.AddSeconds(10);
                        while (await Present() != (key == "y") && DateTime.UtcNow < changedBy) await Task.Delay(30);
                        if (await Present() != (key == "y"))
                            Console.WriteLine("      Image history diagnostic: " + await browser.CoreWebView2.ExecuteScriptAsync("JSON.stringify({keys:window.__imagePasteKeys,focus:document.activeElement?.outerHTML?.slice(0,180),focused:document.hasFocus(),undo:document.querySelector('[data-format=undo]')?.disabled,redo:document.querySelector('[data-format=redo]')?.disabled,body:document.getElementById('results').innerHTML})"));
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
                    if (kind is "SVG-only" or "PNG-only")
                    {
                        async Task<bool> PublishReportClipboard(string command)
                        {
                            if (ImagePasteClipboardSequence() != ownedSequence)
                            { Console.WriteLine("SKIP remaining report image-copy checks: another application changed the clipboard."); return false; }
                            await (Task)Call(view, "ClipboardCommandAsync", command);
                            ownedSequence = ImagePasteClipboardSequence();
                            return true;
                        }
                        if (!await CheckReportPictureCopy(report, view, browser, vector, folder, PublishReportClipboard)) return;
                    }
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

    private static async Task<bool> CheckReportPictureCopy(Form report, object view, WebView2 browser, bool vector,
        string folder, Func<string, Task<bool>> publish)
    {
        string kind = vector ? "SVG" : "PNG", selector = vector ? "#results svg" : "#results img";
        async Task<bool> True(string expression) => await browser.CoreWebView2.ExecuteScriptAsync(expression) == "true";
        async Task<int> Pictures() => int.Parse(await browser.CoreWebView2.ExecuteScriptAsync("document.querySelectorAll('#results .report-media').length"));
        async Task SelectPicture()
        {
            browser.ZoomFactor = 1;
            report.Activate(); browser.Focus();
            await browser.CoreWebView2.ExecuteScriptAsync("document.querySelector(" + JsonSerializer.Serialize(selector) + ").scrollIntoView({block:'center'})");
            await Task.Delay(75); // Let MDI activation, focus and the scroll settle before native pointer input.
            using var rectangle = JsonDocument.Parse(await browser.CoreWebView2.ExecuteScriptAsync("""
                (()=>{
                  const i=document.querySelector(SELECTOR),r=i.getBoundingClientRect(),toolbar=document.getElementById('report-toolbar').getBoundingClientRect();
                  const left=Math.max(1,r.left),right=Math.min(innerWidth-2,r.right),top=Math.max(toolbar.bottom+2,1,r.top),bottom=Math.min(innerHeight-2,r.bottom);
                  const x=left+Math.min((right-left)/2,50),y=(top+bottom)/2,hit=document.elementFromPoint(x,y);
                  window.reportImageCopyClicks=[];
                  for(const type of ['pointerdown','pointerup','click'])document.addEventListener(type,e=>reportImageCopyClicks.push({type,target:e.target.tagName,media:!!e.target.closest('.report-media')}),{capture:true,once:true});
                  return {x,y,visible:right>left&&bottom>top,hit:hit?.tagName,correctTarget:hit?.closest('.report-media')===i.closest('.report-media'),
                    rect:{left:r.left,top:r.top,width:r.width,height:r.height},viewport:{width:innerWidth,height:innerHeight},toolbarBottom:toolbar.bottom,
                    editing:document.body.classList.contains('report-editing'),active:document.activeElement?.tagName,focus:document.hasFocus()};
                })()
                """.Replace("SELECTOR", JsonSerializer.Serialize(selector))));
            Console.WriteLine("      " + kind + " report copy click: " + rectangle.RootElement.GetRawText()
                + "; active MDI=" + (report.MdiParent?.ActiveMdiChild == report) + "; browserFocus=" + browser.ContainsFocus);
            Check(rectangle.RootElement.GetProperty("visible").GetBoolean() && rectangle.RootElement.GetProperty("correctTarget").GetBoolean(),
                kind + " report image has a visible native click target outside the toolbar");
            foreach (string type in new[] { "mousePressed", "mouseReleased" })
            {
                await browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent", JsonSerializer.Serialize(new
                {
                    type, x = rectangle.RootElement.GetProperty("x").GetDouble(), y = rectangle.RootElement.GetProperty("y").GetDouble(),
                    button = "left", buttons = type == "mousePressed" ? 1 : 0, clickCount = 1
                }));
                Console.WriteLine("      " + kind + " report " + type + ": " + await browser.CoreWebView2.ExecuteScriptAsync("(()=>{const i=document.querySelector(" + JsonSerializer.Serialize(selector) + "),r=i.getBoundingClientRect(),s=getSelection();return {scrollY,rectTop:r.top,selectionCount:s?.rangeCount,selected:!!StatsDirectReportEditor.selectedPicture(),clicks:window.reportImageCopyClicks};})()"));
            }
            var until = DateTime.UtcNow.AddSeconds(2);
            bool selected;
            do { selected = await True("!!StatsDirectReportEditor.selectedPicture()"); if (!selected) await Task.Delay(30); }
            while (!selected && DateTime.UtcNow < until);
            if (!selected) Console.WriteLine("      " + kind + " report copy selection: " + await browser.CoreWebView2.ExecuteScriptAsync("(()=>{const s=getSelection(),r=s?.rangeCount?s.getRangeAt(0):null;return {clicks:window.reportImageCopyClicks,active:document.activeElement?.outerHTML?.slice(0,150),focus:document.hasFocus(),editing:document.body.classList.contains('report-editing'),selection:r?{collapsed:r.collapsed,start:r.startContainer.nodeName,startParent:r.startContainer.parentElement?.tagName,startOffset:r.startOffset,end:r.endContainer.nodeName,endOffset:r.endOffset}:null};})()"));
            Check(selected, kind + " report image is selected through an actual browser click");
        }
        async Task History(string command)
        {
            await (Task)Call(view, "CallAsync", "command", new { name = command });
            await (Task)Call(view, "CallAsync", "snapshot", null);
        }
        void ImageConsumerContract(string command)
        {
            using var image = Clipboard.GetImage();
            var contents = Clipboard.GetDataObject();
            Check(Clipboard.ContainsImage() && image != null && image.Width > 0 && image.Height > 0
                && contents?.GetDataPresent("PNG", false) == true,
                kind + " report " + command + " supplies a native image and PNG for chat/image consumers");
            Check(contents?.GetData("StatsDirect.ReportFragment.HTML.v1", false) is string fragment
                && fragment.Contains(vector ? "<svg" : "data:image/png", StringComparison.OrdinalIgnoreCase)
                && contents.GetDataPresent("image/svg+xml", false) == vector,
                kind + " report " + command + " retains the private picture and SVG vector format where applicable");
            Check(contents?.GetData(DataFormats.Html) is string html && html.Contains("data:image/png", StringComparison.Ordinal)
                && contents.GetDataPresent(DataFormats.UnicodeText),
                kind + " report " + command + " keeps its Office HTML and text formats");
        }

        await SelectPicture();
        if (!await publish("copy")) return false;
        ImageConsumerContract("Copy");
        Check(await Pictures() == 1, kind + " report Copy leaves its selected image in place");
        string copiedFragment = (string)Clipboard.GetDataObject().GetData("StatsDirect.ReportFragment.HTML.v1", false);
        foreach (var (label, fragment) in new[]
        {
            ("picture and text", copiedFragment + "<p>Editable caption</p>"),
            ("table containing a picture", "<table><tr><td>" + copiedFragment + "</td></tr></table>")
        })
        {
            var exported = (Task<JsonElement>)Call(view, "CallAsync", "officeClipboard", new { html = fragment });
            using var payload = JsonDocument.Parse((await exported).GetString());
            Check(!payload.RootElement.TryGetProperty("image", out _),
                kind + " " + label + " stays rich content rather than becoming a native single-image selection");
        }
        await SelectPicture();
        if (!await publish("cut")) return false;
        ImageConsumerContract("Cut");
        Check(await Pictures() == 0, kind + " report Cut removes its image after publishing native clipboard formats");
        await History("undo");
        Check(await Pictures() == 1, kind + " report image Cut undoes as one transaction");
        await browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-command=addText]').click()");
        await browser.CoreWebView2.ExecuteScriptAsync("(()=>{const p=[...document.querySelectorAll('#results .report-entry p')].at(-1),r=document.createRange();document.getElementById('results').focus();r.selectNodeContents(p);getSelection().removeAllRanges();getSelection().addRange(r);})()");
        await (Task)Call(view, "ClipboardCommandAsync", "paste");
        Check(await Pictures() == 2 && await True(vector
            ? "document.querySelectorAll('#results svg').length===2&&new Set([...document.querySelectorAll('#results svg [id]')].map(n=>n.id)).size===document.querySelectorAll('#results svg [id]').length"
            : "document.querySelectorAll('#results img[src^=\"data:image/png\"]').length===2"),
            kind + " copied report image repastes with vector identity isolation or embedded PNG");
        await History("undo");
        Check(await Pictures() == 1, kind + " copied report image Paste undoes in one step");
        await History("redo");
        string path = Path.Combine(folder, "report-copy-" + kind + ".html");
        await (Task)Call(view, "SaveAsync", path, "Copied picture", "html");
        await (Task)Call(view, "OpenHtmlAsync", File.ReadAllText(path), "Reopened copied picture");
        Check(await Pictures() == 2, kind + " copied report pictures survive redo, HTML saving and reopening");

        await (Task)Call(view, "AppendAsync", "<p>Text kept as editable text</p>", "Text copy check", "", 0, null, null);
        await browser.CoreWebView2.ExecuteScriptAsync("(()=>{const p=document.querySelector('#results .report-entry:last-child p'),r=document.createRange();document.getElementById('results').focus();r.selectNodeContents(p);getSelection().removeAllRanges();getSelection().addRange(r);})()");
        if (!await publish("copy")) return false;
        Check(!Clipboard.ContainsImage() && Clipboard.GetDataObject()?.GetDataPresent("PNG", false) != true
            && Clipboard.GetText().Contains("Text kept as editable text", StringComparison.Ordinal),
            kind + " report text-only Copy remains editable text without a native image representation");
        await (Task)Call(view, "CallAsync", "selectAll", null);
        if (!await publish("copy")) return false;
        Check(!Clipboard.ContainsImage() && Clipboard.GetDataObject()?.GetDataPresent("PNG", false) != true
            && Clipboard.GetDataObject()?.GetData("StatsDirect.ReportFragment.HTML.v1", false) is string mixed
            && mixed.Contains("Text kept as editable text", StringComparison.Ordinal)
            && mixed.Contains(vector ? "<svg" : "data:image/png", StringComparison.OrdinalIgnoreCase),
            kind + " report mixed picture/text Copy remains rich report content without being labelled a single image");
        return true;
    }

    private static async Task CopyRChartImage(WebView2 browser, string selector)
    {
        if (browser.Parent?.GetType().Name == "RConsoleView")
        {
            // Click the integrated card's real shared UI action. The outer fixture
            // waits for native SVG/PNG publication before reading the clipboard.
            string invoked = await browser.CoreWebView2.ExecuteScriptAsync("(()=>{const i=document.querySelector(" + JsonSerializer.Serialize(selector) + "),b=i?.closest('figure.plot')?.querySelectorAll('button');const copy=b&&[...b].find(x=>x.textContent==='Copy chart');if(!copy)return false;copy.click();return true})()");
            Check(invoked == "true", "the integrated R chart exposes and invokes its exact-revision Copy chart action");
            return;
        }
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
