using Microsoft.Web.WebView2.Core;
using System.Drawing;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

internal static partial class Program
{
    // An inline report must give each imported/pasted chart its own definitions
    // and stylesheet. Serialising charts individually would hide a live-DOM
    // collision, so sample the real WebView rendering as well as its markup.
    private static async Task CheckSvgImportIsolation()
    {
        string Svg(string colour, int width) => $$"""
            <svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="300" height="100" viewBox="0 0 300 100">
              <style>.same-ink { fill: {{colour}}; }</style>
              <defs>
                <path id="same-glyph" d="M10 10H{{10 + width}}V90H10Z"/>
                <clipPath id="same-clip=="><rect x="110" y="10" width="{{width}}" height="80"/></clipPath>
                <linearGradient id="same-paint=="><stop offset="0" stop-color="{{colour}}"/><stop offset="1" stop-color="{{colour}}"/></linearGradient>
              </defs>
              <rect width="300" height="100" fill="white"/>
              <use xlink:href="#same-glyph" class="same-ink"/>
              <rect x="110" y="10" width="80" height="80" style="fill:url('#same-paint==');clip-path:url('#same-clip==')"/>
              <rect x="210" y="10" width="80" height="80" class="same-ink"/>
              <use href="https://invalid.example/chart.svg#external"/>
              <rect width="0" height="0" style="fill:url(https://invalid.example/paint.svg#external)"/>
            </svg>
            """;
        string charts = Svg("#ff0000", 80) + Svg("#0000ff", 40) + Svg("#ff0000", 80);
        string html = "<!doctype html><html><head><meta charset='utf-8'></head><body><p>Independent R charts</p>" + charts + "<p>Paste here</p></body></html>";

        async Task Structure(string phase, int count)
        {
            var result = await Js("""
                (()=>{
                  const charts=[...document.querySelectorAll('.report-body svg')],ids=charts.flatMap(svg=>[svg,...svg.querySelectorAll('[id]')].filter(e=>e.id).map(e=>e.id));
                  let local=true,external=false;
                  for(const svg of charts){
                    const own=new Set([svg,...svg.querySelectorAll('[id]')].filter(e=>e.id).map(e=>e.id));
                    for(const el of [svg,...svg.querySelectorAll('*')])for(const attr of [...el.attributes]){
                      if(/^(href|xlink:href|src)$/i.test(attr.name)){
                        if(attr.value.startsWith('#'))local&&=own.has(attr.value.slice(1));
                        else if(/^(https?:|file:)/i.test(attr.value))external=true;
                      }
                      for(const match of attr.value.matchAll(/url\(\s*(?:"([^"]*)"|'([^']*)'|([^)]*))\s*\)/gi)){
                        const url=(match[1]??match[2]??match[3]).trim();
                        if(url.startsWith('#'))local&&=own.has(url.slice(1));else external=true;
                      }
                    }
                  }
                  return {count:charts.length,unique:ids.length===new Set(ids).size,local,external};
                })()
                """);
            Check(result.GetProperty("count").GetInt32() == count && result.GetProperty("unique").GetBoolean(),
                phase + " retains every SVG with distinct definition IDs");
            Check(result.GetProperty("local").GetBoolean() && !result.GetProperty("external").GetBoolean(),
                phase + " resolves SVG references within their own chart and removes external resources");
        }

        async Task Pixels(string phase, int count)
        {
            bool colours = true, shapes = true;
            for (int i = 0; i < count; i++)
            {
                await Js($"document.querySelectorAll('.report-body svg')[{i}].scrollIntoView({{block:'center',inline:'nearest'}})");
                await Task.Delay(80);
                var bounds = await Js($"(()=>{{const r=document.querySelectorAll('.report-body svg')[{i}].getBoundingClientRect();return [r.left,r.top,r.width,r.height,innerWidth];}})()");
                double[] box = bounds.EnumerateArray().Select(n => n.GetDouble()).ToArray();
                using var stream = new MemoryStream();
                await view.Browser.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, stream);
                await File.WriteAllBytesAsync(Path.Combine(output, $"svg-import-{phase}-{i + 1}.png"), stream.ToArray());
                stream.Position = 0;
                using var image = new Bitmap(stream);
                double scale = image.Width / box[4];
                Color Pixel(int x, int y) => image.GetPixel(
                    Math.Clamp((int)Math.Round((box[0] + x * box[2] / 300) * scale), 0, image.Width - 1),
                    Math.Clamp((int)Math.Round((box[1] + y * box[3] / 100) * scale), 0, image.Height - 1));
                bool Blue = i % 3 == 1;
                bool Ink(Color c) => Blue ? c.B > 220 && c.R < 40 && c.G < 40 : c.R > 220 && c.B < 40 && c.G < 40;
                bool White(Color c) => c.R > 230 && c.G > 230 && c.B > 230;
                colours &= Ink(Pixel(30, 50)) && Ink(Pixel(130, 50)) && Ink(Pixel(250, 50));
                shapes &= Blue ? White(Pixel(70, 50)) && White(Pixel(170, 50)) : Ink(Pixel(70, 50)) && Ink(Pixel(170, 50));
                Console.WriteLine($"      {phase} SVG {i + 1}: glyph {Pixel(30, 50)}, gradient {Pixel(130, 50)}, CSS {Pixel(250, 50)}");
            }
            Check(colours, phase + " renders every chart's own stylesheet, glyph colour and gradient");
            Check(shapes, phase + " renders each chart's own glyph and clipping geometry, including '=' references");
        }

        await view.OpenHtmlAsync(html, "SVG import isolation");
        await Structure("import", 3);
        await Pixels("import", 3);

        await Js("(()=>{const b=document.querySelector('.report-body');b.focus();const p=b.querySelector('p:last-child');const r=document.createRange();r.selectNodeContents(p);r.collapse(false);getSelection().removeAllRanges();getSelection().addRange(r);})()");
        string token = (await view.CallAsync("preparePaste")).GetProperty("token").GetString();
        long revision = (await view.CallAsync("snapshot")).GetProperty("revision").GetInt64();
        Check((await view.CallAsync("paste", new { token, payload = new { html = charts, text = "" } })).GetBoolean(),
            "multiple SVGs paste through the typed report API without using the system clipboard");
        Check((await view.CallAsync("snapshot")).GetProperty("revision").GetInt64() == revision + 1,
            "pasting all three SVGs records one edit");
        await Structure("paste", 6);
        await Pixels("paste", 6);

        await view.CallAsync("command", new { name = "undo" });
        await Structure("undo", 3);
        await view.CallAsync("command", new { name = "redo" });
        await Structure("redo", 6);

        string path = Path.Combine(output, "svg-import-isolation.html");
        await view.SaveAsync(path, "SVG import isolation", "html");
        await view.OpenHtmlAsync(await File.ReadAllTextAsync(path), "Reopened SVG import isolation");
        await Structure("reopen", 6);
        await Pixels("reopen", 6);

        string beforeExport = (await view.CallAsync("snapshot")).GetRawText();
        string wordPath = Path.Combine(output, "svg-import-isolation.docx");
        await view.SaveAsync(wordPath, "SVG import isolation", "docx");
        using (var zip = ZipFile.OpenRead(wordPath))
        {
            async Task<XDocument> Xml(string name)
            {
                using var reader = new StreamReader(zip.GetEntry(name).Open());
                return XDocument.Parse(await reader.ReadToEndAsync());
            }
            var doc = await Xml("word/document.xml");
            var rels = await Xml("word/_rels/document.xml.rels");
            var targets = rels.Root.Elements().ToDictionary(e => (string)e.Attribute("Id"), e => "word/" + (string)e.Attribute("Target"));
            XNamespace a = "http://schemas.openxmlformats.org/drawingml/2006/main", r = "http://schemas.openxmlformats.org/officeDocument/2006/relationships",
                svgNs = "http://schemas.microsoft.com/office/drawing/2016/SVG/main";
            string[] vectors = doc.Descendants(svgNs + "svgBlip").Select(e => targets[(string)e.Attribute(r + "embed")]).ToArray();
            string[] fallbacks = doc.Descendants(a + "blip").Select(e => targets[(string)e.Attribute(r + "embed")]).ToArray();
            Check(vectors.Length == 6 && vectors.Distinct().Count() == 6 && vectors.All(name => name.EndsWith(".svg") && zip.GetEntry(name) != null),
                "Word export retains all six charts as separate SVG assets");
            // The Word library may deduplicate identical fallback PNG bytes;
            // every drawing must still have its own valid fallback reference.
            Check(fallbacks.Length == 6 && fallbacks.All(name => name.EndsWith(".png") && zip.GetEntry(name) != null),
                "all six Word charts include PNG fallback references");
            bool local = true;
            foreach (string name in vectors)
            {
                var svg = await Xml(name);
                var elements = svg.Root.DescendantsAndSelf().ToArray();
                string[] ids = elements.Attributes("id").Select(attr => attr.Value).ToArray();
                var own = ids.ToHashSet();
                local &= ids.Length == own.Count;
                foreach (var attr in elements.Attributes())
                {
                    if (attr.Name.LocalName == "href") local &= attr.Value.StartsWith('#') && own.Contains(attr.Value[1..]);
                    foreach (Match match in Regex.Matches(attr.Value, """url\(\s*(?:"([^"]*)"|'([^']*)'|([^)]*))\s*\)""", RegexOptions.IgnoreCase))
                    {
                        string url = (match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Success ? match.Groups[2].Value : match.Groups[3].Value).Trim();
                        local &= url.StartsWith('#') && own.Contains(url[1..]);
                    }
                }
            }
            Check(local, "each Word SVG retains its own glyph, gradient and clipping references without external dependencies");
        }
        string pdfPath = Path.Combine(output, "svg-import-isolation.pdf");
        await view.SaveAsync(pdfPath, "SVG import isolation", "pdf");
        byte[] pdf = await File.ReadAllBytesAsync(pdfPath);
        Check(pdf.Length > 4 && System.Text.Encoding.ASCII.GetString(pdf, 0, 4) == "%PDF", "the six-chart report exports as a PDF");
        Check((await view.CallAsync("snapshot")).GetRawText() == beforeExport, "Word and PDF exports leave the six-chart editable report unchanged");
    }
}
