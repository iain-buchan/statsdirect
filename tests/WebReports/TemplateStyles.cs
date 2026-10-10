using Microsoft.Web.WebView2.Core;
using StatsDirect.TemplateProcessing;
using StatsDirect.Templates;
using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;

internal static partial class Program
{
    private static async Task CheckTemplateStyles(string paired)
    {
        // Render a semantic template through the production HTML renderer.
        // These roles are shared by all analyses, rather than special-casing
        // the paired-t report or fixing the text of a saved demonstration.
        const string template = """
            <report>
            <title>Template title</title>
            <subtitle>Template subtitle</subtitle>
            <p>Ordinary text <b>Bold text</b> <i>Italic text</i> <u>Underlined text</u></p>
            <p>Subscript x<sub>1</sub> and superscript x<sup>2</sup></p>
            <pre>Monospaced text</pre>
            <table>
            <tr><th>Variable</th><th>Estimate</th><th>Interval</th></tr>
            <tr><td>PEFR</td><td>56.1</td><td><ci>29.8 to 82.4</ci></td></tr>
            <tr><th colspan="2">Grouped header</th><td><pval>0.0012</pval></td></tr>
            </table>
            </report>
            """;
        var parameters = new ParameterBag();
        string html = new CreoleHtmlReportRenderer().Render(null, template, parameters);
        Check(html.Contains("<h1>") && html.Contains("<th>") && html.Contains("<pre>"), "the shared template retains semantic headings, table headers and preformatted text");
        await view.CallAsync("load", new { entries = Array.Empty<object>() });
        await view.AppendAsync(paired, "Paired test", "TPaired", 1239);
        const string pairedSize = "(()=>{const b=document.querySelector('.report-body'),walker=document.createTreeWalker(b,NodeFilter.SHOW_TEXT);let n,count=0;while(n=walker.nextNode()){if(!n.textContent.trim()||n.parentElement.closest('svg,.report-controls'))continue;count++;if(Math.abs(parseFloat(getComputedStyle(n.parentElement).fontSize)-40/3)>.01)return false;}return count>0&&b.textContent.includes('For differences between PEFR Before and PEFR After:');})()";
        Check(await True(pairedSize), "every freshly generated paired-t text run, including the PEFR label, is 10pt");
        await view.AppendAsync(html, "Template styles", "", 0);
        const string templateStyle = "(()=>{const b=document.querySelectorAll('.report-body')[1],s=e=>getComputedStyle(e),ten=e=>Math.abs(parseFloat(s(e).fontSize)-40/3)<.01;return [...b.querySelectorAll('p,h1,h2,td,th,pre')].every(ten)&&s(b.querySelector('h1')).fontWeight==='700'&&s(b.querySelector('h2')).fontWeight==='400'&&s(b.querySelector('h1')).textDecorationLine.includes('underline')&&s(b.querySelector('h2')).textDecorationLine.includes('underline')&&s(b.querySelector('pre')).fontFamily.includes('Courier New')&&s(b.querySelector('b')).fontWeight==='700'&&s(b.querySelector('i')).fontStyle==='italic';})()";
        Check(await True(templateStyle), "template titles, subtitles, body, table cells and monospace text keep the standard size and emphasis");
        const string tableStyle = "(()=>{const b=document.querySelectorAll('.report-body')[1],s=e=>getComputedStyle(e);return [...b.querySelectorAll('td,th')].every(e=>s(e).textAlign==='left'&&s(e).borderBottomWidth==='0px'&&s(e).backgroundColor==='rgba(0, 0, 0, 0)')&&[...b.querySelectorAll('th')].every(e=>s(e).fontWeight==='400'&&s(e).textDecorationLine.includes('underline'))&&b.querySelector('th[colspan]').colSpan===2;})()";
        Check(await True(tableStyle), "template tables retain the established style: left aligned, borderless, unshaded and with underlined regular-weight headers");
        await Js("(()=>{const b=document.querySelectorAll('.report-body')[1],r=document.createRange();b.focus();r.selectNodeContents(b);getSelection().removeAllRanges();getSelection().addRange(r);})()");
        var selection = await view.CallAsync("prepareClipboard", new { cut = false });
        using var office = JsonDocument.Parse((await view.CallAsync("officeClipboard", new { html = selection.GetProperty("html").GetString() })).GetString());
        string clipboard = office.RootElement.GetProperty("html").GetString();
        await Js("window.templateClipboard=" + JsonSerializer.Serialize(clipboard));
        await File.WriteAllTextAsync(Path.Combine(output, "template-clipboard.html"), clipboard);
        Check(await True("(()=>{const d=new DOMParser().parseFromString(templateClipboard,'text/html'),cells=[...d.querySelectorAll('td,th')];return cells.length===8&&cells.every(c=>Math.abs(parseFloat(c.style.fontSize)-40/3)<.01&&c.style.textAlign==='left'&&(!c.style.borderBottomWidth||c.style.borderBottomWidth==='0px'))&&[...d.querySelectorAll('th')].every(c=>c.style.fontWeight==='400'&&c.style.textDecorationLine.includes('underline'));})()"), "Office clipboard retains the template's 10pt table text and header styling");
        Check(clipboard.Contains("x:num=\"56.1\"") && clipboard.Contains("mso-number-format:&quot;General&quot;") && clipboard.Contains("mso-number-format:&quot;\\@&quot;") && clipboard.Contains("colspan=\"2\"") && clipboard.Contains("color: rgb(0, 0, 255)"), "Office table styling preserves Excel number/text formats, merged cells and CI colours");
        string docxPath = Path.Combine(output, "template-styles.docx");
        await view.SaveAsync(docxPath, "Template report styles", "docx");
        using (var archive = ZipFile.OpenRead(docxPath))
        {
            using var reader = new StreamReader(archive.GetEntry("word/document.xml").Open());
            var doc = XDocument.Parse(await reader.ReadToEndAsync());
            XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            XElement Run(string label) => doc.Descendants(w + "r").First(r => r.Descendants(w + "t").Any(t => t.Value == label));
            XElement Style(string label) => Run(label).Element(w + "rPr");
            Check(new[] { "Variable", "Estimate", "PEFR", "56.1", "29.8 to 82.4", "Monospaced text", "Template title", "Template subtitle" }.All(t => (string)Style(t)?.Element(w + "sz")?.Attribute(w + "val") == "20"), "Word export keeps template headings, plain cell text and preformatted text at 10pt");
            Check(Style("Variable")?.Element(w + "u") != null && (string)Style("Variable")?.Element(w + "b")?.Attribute(w + "val") == "false" && (string)Style("29.8 to 82.4")?.Element(w + "color")?.Attribute(w + "val") == "0000FF", "Word table runs retain regular underlined headers and semantic colours");
            Check(doc.Descendants().Where(e => e.Name == w + "tcBorders" || e.Name == w + "tblBorders").SelectMany(b => b.Elements()).All(b => (string)b.Attribute(w + "val") == "none") && !doc.Descendants(w + "tcPr").Elements(w + "shd").Any(), "Word tables do not acquire cell/table borders or shaded headings");
        }
        string htmlPath = Path.Combine(output, "template-styles.html");
        await view.SaveAsync(htmlPath, "Template report styles", "html");
        await view.OpenHtmlAsync(await File.ReadAllTextAsync(htmlPath), "Reopened template report");
        Check(await True(pairedSize) && await True(templateStyle) && await True(tableStyle), "save and reopen retain the generated text sizes, template emphasis and table styling");
        await Js("getSelection().removeAllRanges();window.scrollTo(0,0)");
        using (var png = File.Create(Path.Combine(output, "template-styles.png")))
            await view.Browser.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, png);
        // User formatting remains an editor feature; only generated defaults
        // are standardised. Copy/export must not silently erase later changes.
        await Js("(()=>{const c=document.querySelectorAll('.report-body')[1].querySelector('td');c.style.cssText='font:18pt Georgia;color:purple;background-color:yellow;text-align:right';c.textContent='User cell';})()");
        await Js("(()=>{const b=document.querySelectorAll('.report-body')[1],r=document.createRange();b.focus();r.selectNodeContents(b);getSelection().removeAllRanges();getSelection().addRange(r);})()");
        selection = await view.CallAsync("prepareClipboard", new { cut = false });
        using var custom = JsonDocument.Parse((await view.CallAsync("officeClipboard", new { html = selection.GetProperty("html").GetString() })).GetString());
        await Js("window.templateClipboard=" + JsonSerializer.Serialize(custom.RootElement.GetProperty("html").GetString()));
        Check(await True("(()=>{const d=new DOMParser().parseFromString(templateClipboard,'text/html'),c=[...d.querySelectorAll('td')].find(e=>e.textContent==='User cell');return c.style.fontSize==='24px'&&c.style.fontFamily.includes('Georgia')&&c.style.color==='rgb(128, 0, 128)'&&c.style.backgroundColor==='rgb(255, 255, 0)';})()"), "Office copying preserves a deliberately reformatted table cell");
        string customDocxPath = Path.Combine(output, "template-styles-custom.docx");
        await view.SaveAsync(customDocxPath, "Custom cell style", "docx");
        using (var archive = ZipFile.OpenRead(customDocxPath))
        {
            using var reader = new StreamReader(archive.GetEntry("word/document.xml").Open());
            var doc = XDocument.Parse(await reader.ReadToEndAsync());
            XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            var run = doc.Descendants(w + "r").First(r => r.Descendants(w + "t").Any(t => t.Value == "User cell"));
            var properties = run.Element(w + "rPr");
            Check((string)properties?.Element(w + "sz")?.Attribute(w + "val") == "36" && (string)properties?.Element(w + "rFonts")?.Attribute(w + "ascii") == "Georgia" && (string)properties?.Element(w + "color")?.Attribute(w + "val") == "800080" && (string)run.Ancestors(w + "tc").First().Element(w + "tcPr")?.Element(w + "shd")?.Attribute(w + "fill") == "FFFF00", "Word export preserves the user's cell font, size, colour and shading");
        }
        htmlPath = Path.Combine(output, "template-styles-custom.html");
        await view.SaveAsync(htmlPath, "Template report styles", "html");
        await view.OpenHtmlAsync(await File.ReadAllTextAsync(htmlPath), "Reopened template report");
        Check(await True("(()=>{const c=[...document.querySelectorAll('.report-body td')].find(e=>e.textContent==='User cell');return getComputedStyle(c).fontSize==='24px'&&getComputedStyle(c).fontFamily.includes('Georgia');})()"), "save and reopen also retain the user's deliberate cell formatting");
    }
}
