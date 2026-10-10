using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;

internal static partial class Program
{
    private static async Task InteroperabilityChecks()
    {
        const string fixture = """
            <h1>Windows Office interoperability</h1>
            <p class="ci">Confidence interval: 1.25 to 2.75</p>
            <table class="interop"><tbody><tr><th colspan="2">Merged heading</th></tr>
            <tr><td class="sides">Border sample</td><td>12.50</td></tr>
            <tr><td>Negative</td><td>-2.5</td></tr>
            <tr><td>Exponent</td><td>1.2e-7</td></tr>
            <tr><td>Percentage</td><td>95%</td></tr>
            <tr><td>Identifier</td><td>00123</td></tr>
            <tr><td>Formula-like label</td><td>=1+1</td></tr>
            <tr><td>Long identifier</td><td>1234567890123456</td></tr>
            <tr><td>Inequality</td><td>&lt;0.001</td></tr></tbody></table>
            <svg xmlns="http://www.w3.org/2000/svg" width="384" height="192" viewBox="0 0 384 192"><rect width="384" height="192" fill="white"/><path d="M30 160L350 20" stroke="blue"/><text x="40" y="30" font-family="Arial" font-size="16">Editable vector chart</text></svg>
            """;
        await view.CallAsync("load", new { entries = new[] { new { html = fixture, title = "Office fixture", helpContextId = 1020 } } });
        // Import intentionally removes arbitrary classes. Apply this stylesheet
        // to the live editor after import, as with generated report styles.
        await Js("(()=>{const c=[...document.querySelectorAll('.report-body td')].find(c=>c.textContent==='Border sample');c.removeAttribute('style');c.className='sides';})()");
        await Js("document.head.insertAdjacentHTML('beforeend',`<style id=interop-style>.report-body .ci{color:#0000ff}.report-body .sides{font:12pt Georgia;color:#800080;background:#ffff00;border-top:2px solid #ff0000;border-right:4px dashed #ff8000;border-bottom:6px solid #0000ff;border-left:8px dotted #008000}</style>`)");
        const string presentation = """
            (()=>[...document.querySelectorAll('.report-body')].map(body=>{
              const clone=body.cloneNode(true);clone.querySelectorAll('.report-controls').forEach(e=>e.remove());
              return {html:clone.innerHTML,styles:[...body.querySelectorAll('*')].filter(e=>!e.closest('.report-controls')).map(e=>{
                const c=getComputedStyle(e);return [e.tagName,...['font-family','font-size','font-weight','color','background-color','border-top','border-right','border-bottom','border-left'].map(p=>c.getPropertyValue(p))];})};}))()
            """;
        var beforeResize = await view.CallAsync("snapshot");
        var form = view.FindForm();
        form.Width -= 260;
        await Js("window.interopRendered=false;requestAnimationFrame(()=>requestAnimationFrame(()=>window.interopRendered=true))");
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!await True("window.interopRendered===true") && DateTime.UtcNow < deadline) await Task.Delay(20);
        Check(await True("window.interopRendered===true"), "report resize waits for a rendered frame");
        var resized = await view.CallAsync("snapshot");
        Check(beforeResize.GetProperty("revision").GetInt64() == resized.GetProperty("revision").GetInt64(), "responsive editor layout does not count as a report edit");
        await Js("(()=>{const b=document.querySelector('.report-body'),r=document.createRange();b.focus();r.selectNodeContents(b);getSelection().removeAllRanges();getSelection().addRange(r);})()");
        var selection = await view.CallAsync("prepareClipboard", new { cut = false });
        using var office = JsonDocument.Parse((await view.CallAsync("officeClipboard", new { html = selection.GetProperty("html").GetString() })).GetString());
        string html = office.RootElement.GetProperty("html").GetString();
        await File.WriteAllTextAsync(Path.Combine(output, "office-clipboard.html"), html);
        await File.WriteAllTextAsync(Path.Combine(output, "office-clipboard.cfhtml"), StatsDirect.UI.WebReports.ReportClipboard.EncodeHtml(html));
        await Js("window.officeFixture=new DOMParser().parseFromString(" + JsonSerializer.Serialize(html) + ",'text/html')");
        Check(await True("(()=>{const c=[...officeFixture.querySelectorAll('td')].find(c=>c.textContent==='Border sample');return c.style.borderTop==='1.5pt solid rgb(255, 0, 0)'&&c.style.borderRight==='3pt dashed rgb(255, 128, 0)'&&c.style.borderBottom==='4.5pt solid rgb(0, 0, 255)'&&c.style.borderLeft==='6pt dotted rgb(0, 128, 0)'&&c.style.fontFamily==='Georgia'&&c.style.fontSize==='12pt';})()"), "Office clipboard preserves stylesheet-only cell font and four distinct border sides");
        Check(await True("(()=>{const cells=[...officeFixture.querySelectorAll('td')],cell=t=>cells.find(c=>c.textContent===t);return cell('12.50').getAttribute('x:num')==='12.5'&&cell('-2.5').getAttribute('x:num')==='-2.5'&&cell('1.2e-7').hasAttribute('x:num')&&cell('95%').getAttribute('x:num')==='0.95';})()"), "Office clipboard carries decimal, negative, exponential and percentage numbers");
        Check(await True("(()=>{const cells=[...officeFixture.querySelectorAll('td')];return ['00123','=1+1','1234567890123456','<0.001'].every(t=>{const c=cells.find(c=>c.textContent===t);return !c.hasAttribute('x:num')&&c.getAttribute('style').includes('mso-number-format');});})()"), "identifiers and formula-like labels retain Office text hints");
        Check(await True("officeFixture.querySelector('th').colSpan===2&&!!officeFixture.querySelector('img[src^=\"data:image/png\"]')"), "Office clipboard retains merged cells and a PNG chart fallback");
        Check(await True("!officeFixture.querySelector('img[src^=\"data:image/png\"]').parentElement.querySelector('br')"), "no line break follows a chart in the Office clipboard");
        // Selection itself changes the chart's selection adornment. Start the
        // export invariant after preparing the selection/clipboard.
        string before = (await Js(presentation)).GetRawText();
        foreach (string format in new[] { "html", "docx", "pdf" })
            await view.SaveAsync(Path.Combine(output, "office-export." + format), "Office fixture", format);
        await File.WriteAllTextAsync(Path.Combine(output, "presentation-before.json"), before);
        await File.WriteAllTextAsync(Path.Combine(output, "presentation-after.json"), (await Js(presentation)).GetRawText());
        Check((await Js(presentation)).GetRawText() == before && (await view.CallAsync("snapshot")).GetProperty("revision").GetInt64() == resized.GetProperty("revision").GetInt64(), "HTML, Word and PDF exports preserve all report content/styles and revision after resizing (excluding editor controls)");
        using (var zip = ZipFile.OpenRead(Path.Combine(output, "office-export.docx")))
        {
            using var reader = new StreamReader(zip.GetEntry("word/document.xml").Open());
            var doc = XDocument.Parse(await reader.ReadToEndAsync());
            XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            var cell = doc.Descendants(w + "tc").First(c => c.Descendants(w + "t").Any(t => t.Value == "Border sample"));
            var borders = cell.Element(w + "tcPr")?.Element(w + "tcBorders");
            bool Border(string edge, string style, string size, string color) => (string)borders?.Element(w + edge)?.Attribute(w + "val") == style && (string)borders?.Element(w + edge)?.Attribute(w + "sz") == size && (string)borders?.Element(w + edge)?.Attribute(w + "color") == color;
            Check(Border("top", "single", "12", "FF0000") && Border("right", "dashed", "24", "FF8000") && Border("bottom", "single", "36", "0000FF") && Border("left", "dotted", "48", "008000"), "Word export retains the four individual border styles, widths and colours");
            Check(doc.Descendants(w + "gridSpan").Any(e => (string)e.Attribute(w + "val") == "2") && zip.Entries.Any(e => e.FullName.EndsWith(".svg")) && zip.Entries.Any(e => e.FullName.EndsWith(".png")), "Word export contains the merged cell, SVG chart and PNG fallback");
            XNamespace wp = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";
            Check(doc.Descendants(wp + "extent").Any(e => (string)e.Attribute("cx") == "3657600" && (string)e.Attribute("cy") == "1828800"), "Word chart retains its four-by-two-inch physical size");
        }
        await view.OpenHtmlAsync(await File.ReadAllTextAsync(Path.Combine(output, "office-export.html")), "Reopened Office fixture");
        Check(await True("(()=>{const c=[...document.querySelectorAll('.report-body td')].find(c=>c.textContent==='Border sample'),s=getComputedStyle(c);return s.borderTopWidth==='2px'&&s.borderRightStyle==='dashed'&&s.borderLeftStyle==='dotted'&&s.fontFamily==='Georgia'&&document.querySelector('.report-body th').colSpan===2;})()"), "editable HTML reopening retains the merged table and stylesheet-based presentation");
    }
}
