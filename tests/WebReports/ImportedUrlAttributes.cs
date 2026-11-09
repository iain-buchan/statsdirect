using System.Text.Json;

internal static partial class Program
{
    // An opened report is sanitized as a paste is: an SVG presentation attribute (fill, stroke, filter, clip-path, mask, marker-*)
    // that names an external url(...) is removed, while a reference to the chart's own definitions is kept, renamed with its IDs.
    private static async Task CheckImportedUrlAttributes()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="120" height="60" viewBox="0 0 120 60">
              <defs>
                <linearGradient id="own-paint"><stop offset="0" stop-color="#235f89"/><stop offset="1" stop-color="#235f89"/></linearGradient>
                <clipPath id="own-clip"><rect width="60" height="60"/></clipPath>
                <marker id="own-marker" markerWidth="4" markerHeight="4" refX="2" refY="2"><circle cx="2" cy="2" r="2"/></marker>
              </defs>
              <rect width="60" height="60" fill="url(#own-paint)" clip-path="url('#own-clip')"/>
              <path d="M10 50L50 10" stroke="#000" marker-end="url(#own-marker)"/>
              <rect x="60" width="60" height="60" fill="url(https://invalid.example/paint.svg#p)" stroke="url('http://invalid.example/stroke.svg#s')"
                filter="url(//invalid.example/filter.svg#f)" clip-path="url(https://invalid.example/clip.svg#c)" mask="URL( https://invalid.example/mask.svg#m )"/>
              <path d="M70 50L110 10" stroke="#000" marker-start="url(https://invalid.example/m.svg#a)" marker-mid="url(file:///C:/m.svg#b)" marker-end="url(https://invalid.example/m.svg#c)"/>
            </svg>
            """;
        // every attribute of a chart, other than its style, that holds a url(...): its element, its name and whether it names one of
        // the same chart's own definitions
        async Task<string[]> References(int chart)
        {
            JsonElement found = await Js($$"""
                (()=>{
                  const svg=document.querySelectorAll('.report-body svg')[{{chart}}],own=new Set([...svg.querySelectorAll('[id]')].map(e=>e.id));
                  const result=[];
                  for(const el of [svg,...svg.querySelectorAll('*')])for(const attr of [...el.attributes]){
                    if(attr.name==='style'||!/url\s*\(/i.test(attr.value))continue;
                    const match=/^url\(\s*["']?#([^"')]+)["']?\s*\)$/i.exec(attr.value.trim());
                    result.push(el.localName+' '+attr.name+' '+(match&&own.has(match[1])?'own':'external '+attr.value));
                  }
                  return result;
                })()
                """);
            return found.EnumerateArray().Select(e => e.GetString()).Order(StringComparer.Ordinal).ToArray();   // attribute order is the sanitizer's
        }
        string[] expected = new[] { "rect fill own", "rect clip-path own", "path marker-end own" }.Order(StringComparer.Ordinal).ToArray();
        await view.OpenHtmlAsync("<p>Opened chart</p>" + svg + "<p>Paste here</p>", "External SVG references");
        string[] opened = await References(0);
        Check(opened.SequenceEqual(expected), "an opened report keeps a chart's own url(#...) references and removes external ones from fill, stroke, filter, clip-path, mask and markers: " + string.Join("; ", opened));
        Check(await True("!document.querySelector('.report-body').innerHTML.includes('invalid.example') && !document.querySelector('.report-body').innerHTML.includes('file:')"),
            "no external address of the opened chart reaches the report");

        await Js("(()=>{const b=document.querySelector('.report-body');b.focus();const p=b.querySelector('p:last-child');const r=document.createRange();r.selectNodeContents(p);r.collapse(false);getSelection().removeAllRanges();getSelection().addRange(r);})()");
        string token = (await view.CallAsync("preparePaste")).GetProperty("token").GetString();
        Check((await view.CallAsync("paste", new { token, payload = new { html = svg, text = "" } })).GetBoolean(), "the same chart pastes through the typed report API");
        string[] pasted = await References(1);
        Check(pasted.SequenceEqual(opened), "opening and pasting the chart keep the same references: " + string.Join("; ", pasted));
    }
}
