#nullable enable annotations
using StatsDirect.UI.WebReports;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

internal static partial class Program
{
    private static async Task CheckOfficeSvgClipboard()
    {
        await view.Ready.WaitAsync(TimeSpan.FromSeconds(45));
        await view.CallAsync("load", new { entries = Array.Empty<object>() });
        string report = EngineExample.PairedAgreement();
        await view.AppendAsync(report, "Production PEFR agreement", "TPaired", 1150);
        string source = (await Js("document.querySelector('#results svg').outerHTML")).GetString()!;
        string original = (await Js("new XMLSerializer().serializeToString(StatsDirectReportEditor.exportableSvg(document.querySelector('#results svg')))")).GetString()!;
        string outlined = OfficeSvg.ForClipboard(original);
        var before = XDocument.Parse(original);
        var after = XDocument.Parse(outlined);
        int labels = before.Descendants().Count(n => n.Name.LocalName == "text");
        Check(labels > 9 && !after.Descendants().Any(n => n.Name.LocalName is "text" or "tspan")
            && after.Descendants().Count(n => n.Name.LocalName == "path") >= labels,
            "the real PEFR report's clipboard SVG outlines every Arial label for Word shape conversion");
        Check((string)before.Root!.Attribute("viewBox") == (string)after.Root!.Attribute("viewBox")
            && OriginalSvgShapes(before).SequenceEqual(OriginalSvgShapes(after, originalOnly: before)),
            "outlining the production chart preserves its viewport, axes, open markers and agreement lines");
        Check(before.Descendants().Any(n => n.Name.LocalName == "text" && ((string?)n.Attribute("transform"))?.Contains("-90") == true)
            && after.Descendants().Any(n => ((string?)n.Attribute("transform"))?.Contains("-90") == true),
            "the production report's rotated Y-axis label keeps its transform in the Office vector");
        Check(OfficeSvg.ForClipboard(outlined) == outlined,
            "an already outlined clipboard SVG passes through unchanged");
        await CompareOfficeSvgRendering("production-pefr", original, outlined);
        await File.WriteAllTextAsync(Path.Combine(output, "office-production-report.html"), "<!doctype html><html><body>" + report + "</body></html>");

        const string fontFixture = """
            <svg xmlns="http://www.w3.org/2000/svg" width="620" height="180" viewBox="0 0 620 180">
              <rect width="620" height="180" fill="white"/>
              <defs><clipPath id="label-clip"><rect x="8" y="2" width="604" height="166"/></clipPath></defs>
              <g clip-path="url(#label-clip)" font-family="Arial,sans-serif" font-size="30" fill="#1259a7">
                <text x="14" y="40">O8 BQ β μ ± 95%</text>
                <text x="14" y="83" font-weight="700" font-style="italic">Bold italic β ± 1.25</text>
                <text x="14" y="126" font-family="StatsDirectMissingFont,Arial,sans-serif">Font fallback β μ</text>
              </g>
              <g transform="translate(590 150) rotate(-90)" font-family="Arial" font-size="22" fill="black"><text x="0" y="0">Rotated Ω</text></g>
            </svg>
            """;
        string fontOutlined = OfficeSvg.ForClipboard(fontFixture);
        var fontDocument = XDocument.Parse(fontOutlined);
        Check(!fontDocument.Descendants().Any(n => n.Name.LocalName == "text")
            && fontDocument.Descendants().Count(n => n.Name.LocalName == "path") >= 4
            && fontDocument.Descendants().Any(n => (string?)n.Attribute("fill-rule") == "evenodd")
            && fontDocument.Descendants().Any(n => (string?)n.Attribute("clip-path") == "url(#label-clip)"),
            "bold italic, Unicode and fallback-font labels become clipped vector outlines with glyph holes retained");
        await CompareOfficeSvgRendering("fonts-and-holes", fontFixture, fontOutlined);

        const string pathsOnly = "<svg xmlns='http://www.w3.org/2000/svg' width='80' height='40' viewBox='0 0 80 40'><g transform='translate(3 2)'><path d='M2 2H30V30H2ZM8 8V24H24V8Z' fill-rule='evenodd' fill='blue'/><path d='M40 5L70 30' fill='none' stroke='red' stroke-width='2'/></g></svg>";
        Check(OfficeSvg.ForClipboard(pathsOnly) == pathsOnly,
            "a known-good R-style SVG containing positioned paths is not rewritten");
        const string complex = "<svg xmlns='http://www.w3.org/2000/svg' width='200' height='60'><text x='5' y='30' font-family='Arial' font-size='20'><tspan x='5'>H</tspan><tspan dy='5'>2</tspan></text></svg>";
        Check(XDocument.Parse(OfficeSvg.ForClipboard(complex)).Descendants().Any(n => n.Name.LocalName == "tspan"),
            "complex positioned spans remain intact instead of receiving a lossy outline guess");
        const string unspecified = "<svg xmlns='http://www.w3.org/2000/svg' width='200' height='60'><text x='5' y='30'>Unknown inherited font</text></svg>";
        Check(XDocument.Parse(OfficeSvg.ForClipboard(unspecified)).Descendants().Any(n => n.Name.LocalName == "text"),
            "text without an explicit font remains intact rather than assuming the report's external CSS");

        await CheckNativeOfficeSvgCopy(source);
    }

    private static string[] OriginalSvgShapes(XDocument document, XDocument? originalOnly = null)
    {
        static bool Shape(XElement n) => n.Name.LocalName is "line" or "ellipse" or "circle" or "rect" or "polyline" or "polygon" or "path";
        var shapes = document.Descendants().Where(Shape).Select(n => n.ToString(SaveOptions.DisableFormatting));
        if (originalOnly != null)
        {
            var original = OriginalSvgShapes(originalOnly).ToHashSet(StringComparer.Ordinal);
            shapes = shapes.Where(original.Contains);
        }
        return shapes.ToArray();
    }

    private static async Task CompareOfficeSvgRendering(string label, string original, string outlined)
    {
        await File.WriteAllTextAsync(Path.Combine(output, label + "-before.svg"), original, new UTF8Encoding(false));
        await File.WriteAllTextAsync(Path.Combine(output, label + "-outlined.svg"), outlined, new UTF8Encoding(false));
        string expression = $$"""
            (async () => {
              const draw = async markup => {
                const svg = new DOMParser().parseFromString(markup, 'image/svg+xml').documentElement;
                const box = svg.getAttribute('viewBox').trim().split(/[\s,]+/).map(Number);
                const w = Math.round(box[2]), h = Math.round(box[3]);
                svg.setAttribute('width', w); svg.setAttribute('height', h);
                const image = new Image(); image.src = 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(new XMLSerializer().serializeToString(svg));
                await image.decode();
                const canvas = document.createElement('canvas'); canvas.width = w * 2; canvas.height = h * 2;
                const context = canvas.getContext('2d', {willReadFrequently:true}); context.fillStyle = 'white';
                context.fillRect(0, 0, canvas.width, canvas.height); context.drawImage(image, 0, 0, canvas.width, canvas.height);
                return {pixels:context.getImageData(0, 0, canvas.width, canvas.height).data, png:canvas.toDataURL('image/png'), width:canvas.width, height:canvas.height};
              };
              const a = await draw({{JsonSerializer.Serialize(original)}}), b = await draw({{JsonSerializer.Serialize(outlined)}});
              let beforeInk=0, afterInk=0, union=0, overlap=0, redBefore=0, redAfter=0;
              const bounds = pixels => {
                let left=a.width, top=a.height, right=-1, bottom=-1;
                for(let y=0; y<a.height; y++)for(let x=0; x<a.width; x++) {
                  const i=(y*a.width+x)*4;
                  if(Math.min(pixels[i],pixels[i+1],pixels[i+2])<160){left=Math.min(left,x);top=Math.min(top,y);right=Math.max(right,x);bottom=Math.max(bottom,y);}
                }
                return [left,top,right,bottom];
              };
              for(let i=0; i<a.pixels.length; i+=4) {
                const ia=Math.min(a.pixels[i],a.pixels[i+1],a.pixels[i+2])<160, ib=Math.min(b.pixels[i],b.pixels[i+1],b.pixels[i+2])<160;
                beforeInk+=ia; afterInk+=ib; union+=ia||ib; overlap+=ia&&ib;
                redBefore+=a.pixels[i]>180&&a.pixels[i+1]<100&&a.pixels[i+2]<100;
                redAfter+=b.pixels[i]>180&&b.pixels[i+1]<100&&b.pixels[i+2]<100;
              }
              return {beforeInk,afterInk,union,overlap,redBefore,redAfter,beforeBounds:bounds(a.pixels),afterBounds:bounds(b.pixels),beforePng:a.png,afterPng:b.png};
            })()
            """;
        using var result = JsonDocument.Parse(await view.Browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Runtime.evaluate",
            JsonSerializer.Serialize(new { expression, awaitPromise = true, returnByValue = true })));
        if (result.RootElement.TryGetProperty("exceptionDetails", out var error)) throw new Exception(error.GetRawText());
        var image = result.RootElement.GetProperty("result").GetProperty("value");
        foreach (var (field, suffix) in new[] { ("beforePng", "-before.png"), ("afterPng", "-outlined.png") })
        {
            string data = image.GetProperty(field).GetString()!;
            await File.WriteAllBytesAsync(Path.Combine(output, label + suffix), Convert.FromBase64String(data[(data.IndexOf(',') + 1)..]));
        }
        var metrics = image.EnumerateObject().Where(p => !p.Name.EndsWith("Png", StringComparison.Ordinal)).ToDictionary(p => p.Name, p => p.Value.Clone());
        await File.WriteAllTextAsync(Path.Combine(output, label + "-pixels.json"), JsonSerializer.Serialize(metrics, new JsonSerializerOptions { WriteIndented = true }));
        double inkRatio = image.GetProperty("afterInk").GetDouble() / image.GetProperty("beforeInk").GetDouble();
        double overlap = image.GetProperty("overlap").GetDouble() / image.GetProperty("union").GetDouble();
        int[] beforeBounds = image.GetProperty("beforeBounds").EnumerateArray().Select(n => n.GetInt32()).ToArray();
        int[] afterBounds = image.GetProperty("afterBounds").EnumerateArray().Select(n => n.GetInt32()).ToArray();
        Console.WriteLine($"      {label}: ink ratio {inkRatio:F4}; overlap {overlap:F4}; bounds {string.Join(',', beforeBounds)} / {string.Join(',', afterBounds)}");
        Check(image.GetProperty("beforeInk").GetInt32() > 500 && inkRatio is > .75 and < 1.25 && overlap > .60
            && beforeBounds.Zip(afterBounds).All(pair => Math.Abs(pair.First - pair.Second) <= 3),
            label + " preserves the chart and label positions at 2x resolution after outlining");
        Check(image.GetProperty("redBefore").GetInt32() == image.GetProperty("redAfter").GetInt32(),
            label + " leaves non-text coloured drawing pixels unchanged");
    }

    private static async Task CheckNativeOfficeSvgCopy(string source)
    {
        if (Environment.GetEnvironmentVariable("STATSDIRECT_SKIP_CLIPBOARD") == "1")
        { Console.WriteLine("SKIP native outlined-SVG clipboard checks (preserving the user's clipboard)"); return; }
        IDataObject? original = Clipboard.GetDataObject();
        var saved = new DataObject();
        var retained = new List<IDisposable>();
        try
        {
            if (original != null)
                foreach (string format in original.GetFormats(false))
                {
                    if (format == DataFormats.Bitmap || format == typeof(Bitmap).FullName)
                    {
                        if (saved.GetDataPresent(DataFormats.Bitmap)) continue;
                        var bitmap = (original.GetData(DataFormats.Bitmap) as Image ?? Clipboard.GetImage())?.Clone() as Image
                            ?? throw new InvalidOperationException("Unavailable clipboard format: " + format);
                        retained.Add(bitmap); saved.SetData(DataFormats.Bitmap, true, bitmap); continue;
                    }
                    object value = original.GetData(format, false) ?? throw new InvalidOperationException("Unavailable clipboard format: " + format);
                    value = value switch { MemoryStream stream => new MemoryStream(stream.ToArray()), byte[] bytes => bytes.ToArray(), string text => text, _ => throw new InvalidOperationException("Cannot safely preserve clipboard format: " + format) };
                    if (value is IDisposable resource) retained.Add(resource);
                    saved.SetData(format, false, value);
                }
        }
        catch (Exception error)
        {
            foreach (var resource in retained) resource.Dispose();
            Console.WriteLine("SKIP native outlined-SVG clipboard checks: " + error.Message); return;
        }
        uint ownedSequence = 0;
        try
        {
            await Js("document.querySelector('#results svg').dispatchEvent(new MouseEvent('click',{bubbles:true,cancelable:true}))");
            Check(await True("!!StatsDirectReportEditor.selectedPicture()"), "the production report chart is selected for native Copy");
            long revision = (await view.CallAsync("snapshot")).GetProperty("revision").GetInt64();
            await view.ClipboardCommandAsync("copy");
            ownedSequence = OfficeSvgClipboardSequence();
            var copied = Clipboard.GetDataObject();
            string vector = copied?.GetData(ReportClipboard.SvgFormat, false) switch
            {
                MemoryStream stream => Encoding.UTF8.GetString(stream.ToArray()).TrimEnd('\0'),
                string text => text,
                _ => throw new Exception("Native Copy supplied no SVG.")
            };
            string fragment = copied.GetData(ReportClipboard.FragmentFormat, false) as string ?? "";
            Check(!XDocument.Parse(vector).Descendants().Any(n => n.Name.LocalName is "text" or "tspan")
                && fragment.Contains("<text", StringComparison.Ordinal) && fragment.Contains("PEFR", StringComparison.Ordinal),
                "actual native Copy outlines only the public SVG while keeping editable report text in its private fragment");
            Check(copied.GetDataPresent("PNG", false) && Clipboard.ContainsImage(),
                "actual native Copy retains PNG and bitmap alternatives with the outlined SVG");
            Check((await Js("document.querySelector('#results svg').outerHTML")).GetString() == source
                && (await view.CallAsync("snapshot")).GetProperty("revision").GetInt64() == revision,
                "actual native chart Copy leaves the source report's SVG and undo revision unchanged");
            await File.WriteAllTextAsync(Path.Combine(output, "production-native-clipboard.svg"), vector, new UTF8Encoding(false));
            await File.WriteAllTextAsync(Path.Combine(output, "production-native-private-fragment.html"), fragment, new UTF8Encoding(false));
        }
        finally
        {
            if (ownedSequence != 0 && OfficeSvgClipboardSequence() == ownedSequence)
                for (int attempt = 0; ; attempt++)
                    try { if (original == null) Clipboard.Clear(); else Clipboard.SetDataObject(saved, true, 0, 0); break; }
                    catch (ExternalException) when (attempt < 40) { await Task.Delay(50); }
            foreach (var resource in retained) resource.Dispose();
        }
    }

    [DllImport("user32.dll", EntryPoint = "GetClipboardSequenceNumber")]
    private static extern uint OfficeSvgClipboardSequence();
}
