using Microsoft.Web.WebView2.Core;
using StatsDirect.Charting;
using StatsDirect.TemplateProcessing;
using StatsDirect.Templates;
using StatsDirect.UI.WebReports;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

internal static partial class Program
{
    private static int checks;
    private static string output;
    private static ReportView view;
    private static void Check(bool ok, string description) { if (!ok) throw new Exception(description); checks++; Console.WriteLine("PASS " + description); }
    private static async Task<JsonElement> Js(string script) => JsonDocument.Parse(await view.Browser.CoreWebView2.ExecuteScriptAsync(script)).RootElement.Clone();
    private static async Task<bool> True(string script) => (await Js(script)).ValueKind == JsonValueKind.True;
    [STAThread]
    static int Main(string[] args)
    {
        output = args.Length > 0 ? System.IO.Path.GetFullPath(args[0]) : System.IO.Path.Combine(AppContext.BaseDirectory, "artifacts");
        Directory.CreateDirectory(output);
        ApplicationConfiguration.Initialize();
        using var form = new Form { Width = 1180, Height = 850, ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-16000, -16000), Text = "StatsDirect report integration tests" };
        view = new ReportView(System.IO.Path.Combine(AppContext.BaseDirectory, "ReportEditor"), System.IO.Path.Combine(output, "browser-profile")) { Dock = DockStyle.Fill };
        form.Controls.Add(view);
        view.Notice += message => Console.WriteLine("NOTICE " + message);
        int exit = 1;
        form.Shown += async (_, _) =>
        {
        try { await Run(); await InteroperabilityChecks(); Console.WriteLine($"PASS {checks} Windows WebView2 checks"); exit = 0; }
            catch (Exception ex) { Console.WriteLine("FAIL " + ex); }
            finally { form.Close(); }
        };
        Application.Run(form);
        return exit;
    }

    private static async Task Run()
    {
        foreach (string family in new[] { "Arial", "Georgia" })
        {
            var chosen = new FontDescriptor(family, 1, 13.5f);
            Check(FontDescriptor.TryParse(chosen.ToString(), out var restored) && chosen.Equals(restored), $"saved {family} chart font roundtrips with its style and point size");
        }
        Check(FontDescriptor.TryParse("Arial;0;15", out var legacyFont) && legacyFont.SizeInPoints == 15, "legacy chart fonts without a point suffix still load");
        var applicationAssembly = typeof(CreoleHtmlReportRenderer).Assembly;
        Check(!applicationAssembly.GetReferencedAssemblies().Any(a => a.Name.StartsWith("DevExpress", StringComparison.OrdinalIgnoreCase)), "the application assembly has no DevExpress references");
        Check(applicationAssembly.GetType("StatsDirect.UI.frmReportRichEdit") == null && applicationAssembly.GetType("StatsDirect.TemplateProcessing.RtfRenderer") == null, "the retired report form and RTF renderer are absent");
        string unicode = "<html><head><style>p{color:red}</style></head><body><!--StartFragment--><p>β ± café 😀</p><!--EndFragment--></body></html>";
        string cf = ReportClipboard.EncodeHtml(unicode);
        Check(ReportClipboard.DecodeHtml(cf) == unicode, "CF_HTML roundtrip retains Unicode and styles");
        int from = int.Parse(System.Text.RegularExpressions.Regex.Match(cf, @"StartFragment:(\d+)").Groups[1].Value);
        int to = int.Parse(System.Text.RegularExpressions.Regex.Match(cf, @"EndFragment:(\d+)").Groups[1].Value);
        Check(Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(cf), from, to - from) == "<p>β ± café 😀</p>", "CF_HTML offsets are bytes, including non-ASCII characters");
        await view.Ready.WaitAsync(TimeSpan.FromSeconds(45));
        Check(view.Browser.CoreWebView2.Source == ReportView.Origin + "/index.html", "bundled editor loads in the native WebView2 runtime");
        Check(await True("typeof StatsDirectReportEditor.command === 'function' && !document.getElementById('report-edit-toggle') && !document.getElementById('report-format-tools').hidden"), "editor starts with formatting controls available and no Edit report toggle");
        string svg = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 640 300'><defs><clipPath id='plotclip'><rect width='640' height='300'/></clipPath></defs><rect width='640' height='300' fill='white'/><path d='M50 30V250H610' stroke='#333' fill='none'/><g clip-path='url(#plotclip)' fill='#087f8c'><circle cx='170' cy='180' r='6'/><circle cx='320' cy='120' r='6'/><circle cx='470' cy='90' r='6'/></g><text x='320' y='285' text-anchor='middle'>Mean of measurements</text><text x='320' y='24' text-anchor='middle'>Agreement plot</text></svg>";
        string table = "<h2>Paired analysis</h2><p>Mean difference β = 1.25; P &lt; 0.0001</p><table><thead><tr><th colspan='2'>Results</th></tr></thead><tbody><tr><td>Lower limit</td><td>0.75</td></tr><tr><td>Upper limit</td><td>1.75</td></tr></tbody></table>";
        await view.AppendAsync(table + svg, "Paired analysis & agreement", "TPaired", 1150);
        Check(await True("document.querySelectorAll('.report-entry').length===1 && document.querySelector('svg text').getAttribute('text-anchor')==='middle'"), "report and vector chart append with their layout");
        Check(await True("document.querySelector('.report-body').isContentEditable"), "new analysis output is immediately editable without a mode change");
        await Js("(()=>{const p=document.querySelector('.report-body p');p.textContent='Edited β interpretation';p.dispatchEvent(new InputEvent('input',{bubbles:true,inputType:'insertText'}));})()");
        await view.AppendAsync("<p>Second analysis</p>", "Second analysis", "Agreement", 1002);
        Check(await True("document.querySelector('.report-body').textContent.includes('Edited β interpretation') && document.querySelectorAll('.report-entry').length===2"), "appending an analysis preserves existing report edits");
        await view.CallAsync("command", new { name = "undo" });
        Check(await True("document.querySelector('.report-body').textContent.includes('Mean difference')"), "native bridge implements undo after an append");
        await view.CallAsync("command", new { name = "redo" });
        Check(await True("document.querySelector('.report-body').textContent.includes('Edited β interpretation')"), "redo restores the edited text");
        await Js("(()=>{const p=document.querySelector('.report-body p');p.focus();const r=document.createRange();r.selectNodeContents(p);const s=getSelection();s.removeAllRanges();s.addRange(r);})()");
        await view.CallAsync("command", new { name = "bold" });
        Check(await True("document.querySelector('.report-body p').innerHTML.includes('font-weight') || !!document.querySelector('.report-body p b')"), "Chromium formatting command changes the selected text");
        await Js("(()=>{const b=document.querySelector('.report-body');b.focus();const r=document.createRange();r.selectNodeContents(b);const s=getSelection();s.removeAllRanges();s.addRange(r);})()");
        var selected = await view.CallAsync("prepareClipboard", new { cut = false });
        string fragment = selected.GetProperty("html").GetString();
        Check(fragment.Contains("<svg") && fragment.Contains("colspan=\"2\""), "private clipboard preserves SVG and merged table cells");
        using var office = JsonDocument.Parse((await view.CallAsync("officeClipboard", new { html = fragment })).GetString());
        string publicHtml = office.RootElement.GetProperty("html").GetString();
        Check(publicHtml.Contains("data:image/png") && publicHtml.Contains("<table"), "Office clipboard uses image fallbacks and real HTML tables");
        string cutToken = (await view.CallAsync("prepareClipboard", new { cut = true })).GetProperty("token").GetString();
        Check((await view.CallAsync("cut", new { token = cutToken })).GetBoolean(), "transactional cut removes the prepared selection");
        await view.CallAsync("command", new { name = "undo" });
        Check(await True("!!document.querySelector('.report-body svg') && !!document.querySelector('.report-body table')"), "undo cut restores both chart and table");
        await Js("(()=>{const b=document.querySelectorAll('.report-body')[1];b.focus();const r=document.createRange();r.selectNodeContents(b);r.collapse(false);const s=getSelection();s.removeAllRanges();s.addRange(r);})()");
        string token = (await view.CallAsync("preparePaste")).GetProperty("token").GetString();
        Check((await view.CallAsync("paste", new { token, payload = new { html = fragment, text = "" } })).GetBoolean(), "rich fragment pastes into another report section");
        Check(await True("document.querySelectorAll('.report-body svg').length===2 && new Set([...document.querySelectorAll('clipPath')].map(n=>n.id)).size===2"), "pasted SVG identifiers do not collide");
        await view.CallAsync("command", new { name = "undo" });
        Check(await True("document.querySelectorAll('.report-body svg').length===1"), "undo paste is one transaction");
        string htmlPath = System.IO.Path.Combine(output, "edited-report.html");
        await view.SaveAsync(htmlPath, "Edited report", "html");
        string html = await File.ReadAllTextAsync(htmlPath);
        Check(html.Contains("statsdirect-report-data") && html.Contains("Edited β interpretation"), "editable HTML retains report content and metadata");
        Check(!html.Contains("src=\"vendor/") && !html.Contains("onclick="), "saved HTML has no application scripts or event handlers");
        await Js("StatsDirectReportEditor.setEditing(false)");
        await view.OpenHtmlAsync(html, "Reopened report");
        Check(await True("document.querySelectorAll('.report-entry').length===2 && document.querySelector('.report-body').textContent.includes('Edited β interpretation') && [...document.querySelectorAll('.report-body')].every(b=>b.isContentEditable) && snapshot().revision===0"), "saved reports reopen ready to edit without becoming modified");
        Check(await True("document.querySelector('svg text').getAttribute('text-anchor')==='middle'"), "SVG text alignment survives HTML reopening");
        var state = await view.CallAsync("snapshot");
        Check(state.GetProperty("entries")[0].GetProperty("helpContextId").GetInt32() == 1150, "context help metadata survives save/reopen");
        await view.SaveAsync(System.IO.Path.Combine(output, "edited-report.docx"), "Edited report", "docx");
        using (var zip = ZipFile.OpenRead(System.IO.Path.Combine(output, "edited-report.docx")))
        {
            using var reader = new StreamReader(zip.GetEntry("word/document.xml").Open());
            string xml = await reader.ReadToEndAsync();
            _ = XDocument.Parse(xml);
            Check(xml.Contains("Edited β interpretation") && xml.Contains("w:tbl") && xml.Contains("gridSpan"), "DOCX contains editable text and a merged table");
            Check(zip.Entries.Any(e=>e.FullName.EndsWith(".svg")) && zip.Entries.Any(e=>e.FullName.EndsWith(".png")), "DOCX includes vector charts and PNG fallbacks");
        }
        await view.SaveAsync(System.IO.Path.Combine(output, "edited-report.pdf"), "Edited report", "pdf");
        Check(Encoding.ASCII.GetString(File.ReadAllBytes(System.IO.Path.Combine(output, "edited-report.pdf")),0,4)=="%PDF", "native WebView2 PDF export completes");
        using (var png = File.Create(System.IO.Path.Combine(output, "editor.png"))) await view.Browser.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, png);
        await view.OpenHtmlAsync("<p>Safe import</p><script>window.bad=1</script><img src='https://invalid.example/track'><svg onload='window.bad=2'><text>Chart</text></svg><iframe src='file:///C:/Windows/win.ini'></iframe>", "Untrusted file");
        Check(await True("!window.bad && !document.querySelector('.report-body script,.report-body iframe,.report-body [onload],.report-body img[src^=\"https:\"]')"), "import removes active content and external resources");
        try { await view.CallAsync("not-a-command"); throw new Exception("Unexpected bridge command accepted"); }
        catch (InvalidOperationException) { Check(true, "unknown bridge methods are rejected"); }
        Check(!view.Browser.CoreWebView2.Settings.AreHostObjectsAllowed, "no native object bridge is exposed to imported documents");
        string real = EngineExample.PairedAgreement();
        Check(real.Contains("56.1111111111") && real.Contains("<svg") && !real.Contains("Chart not drawn"), "production paired-t and agreement engine outputs render to HTML and SVG");
        Check(System.Text.RegularExpressions.Regex.Matches(real, "font-family:[^;]+;").Select(m => m.Value).All(f => f.Contains("Arial")) && real.Contains("font-family:'Arial'"), "production SVG chart text uses the Arial preferences");
        await view.OpenHtmlAsync(real, "Paired t test and agreement");
        Check(await True("document.querySelector('.report-body').textContent.includes('Paired t test') && !!document.querySelector('.report-body svg')"), "real engine report displays in the editor");
        await Js("window.scrollTo(0,0)");
        using (var png = File.Create(System.IO.Path.Combine(output, "engine-example.png"))) await view.Browser.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, png);
        string receivedCommand = null;
        view.CommandRequested += command => receivedCommand = command;
        await Js("document.dispatchEvent(new KeyboardEvent('keydown',{key:'s',ctrlKey:true,bubbles:true,cancelable:true}))");
        await view.CallAsync("snapshot");
        Check(receivedCommand == "save", "Ctrl+S reaches the report save command instead of browser Save Page");
        view.Browser.CoreWebView2.Reload();
        await Task.Delay(100);
        Check(await True("document.querySelector('.report-body').textContent.includes('Paired t test')"), "browser reload cannot discard the report");
        await view.CallAsync("load", new { entries = new[] {
            new { html="<p>Alpha words</p>",title="Source",helpContextId=0 },
            new { html="<p>Destination</p>",title="Target",helpContextId=0 } } });
        view.Browser.ZoomFactor = .6;
        await Task.Delay(100);
        await Js("StatsDirectReportEditor.setEditing(true);window.scrollTo(0,0)");
        var positions = await Js("(()=>{const p=document.querySelector('.report-body p'),q=document.querySelectorAll('.report-body p')[1];p.focus();const r=document.createRange();r.selectNodeContents(p);const s=getSelection();s.removeAllRanges();s.addRange(r);const a=r.getBoundingClientRect(),b=q.getBoundingClientRect();return {sx:a.left+10,sy:a.top+a.height/2,dx:b.left+20,dy:b.top+b.height/2};})()");
        double sx=positions.GetProperty("sx").GetDouble(),sy=positions.GetProperty("sy").GetDouble(),dx=positions.GetProperty("dx").GetDouble(),dy=positions.GetProperty("dy").GetDouble();
        async Task Mouse(string type,double x,double y,int buttons=0,int modifiers=2) => await view.Browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent",JsonSerializer.Serialize(new {type,x,y,button=type is "mousePressed" or "mouseReleased"?"left":"none",buttons,clickCount=1,modifiers}));
        await Mouse("mouseMoved",sx,sy);
        await Mouse("mousePressed",sx,sy,1);
        await Mouse("mouseMoved",sx+12,sy,1);
        await Mouse("mouseMoved",dx,dy,1);
        await Mouse("mouseReleased",dx,dy);
        await view.CallAsync("snapshot");
        Check(await True("[...document.querySelectorAll('.report-body')].every(b=>b.textContent.includes('Alpha words'))"), "native Chromium Ctrl-drag copies between report sections");
        await view.CallAsync("command", new {name="undo"});
        Check(await True("document.querySelectorAll('.report-body')[0].textContent.includes('Alpha words') && !document.querySelectorAll('.report-body')[1].textContent.includes('Alpha words')"), "Ctrl-drag copy undoes as one transaction");
        await Js("(()=>{const p=document.querySelector('.report-body p');p.focus();const r=document.createRange();r.selectNodeContents(p);const s=getSelection();s.removeAllRanges();s.addRange(r);})()");
        await Mouse("mouseMoved",sx,sy,0,0);
        await Mouse("mousePressed",sx,sy,1,0);
        await Mouse("mouseMoved",sx+12,sy,1,0);
        await Mouse("mouseMoved",dx,dy,1,0);
        await Mouse("mouseReleased",dx,dy,0,0);
        await view.CallAsync("snapshot");
        Check(await True("!document.querySelectorAll('.report-body')[0].textContent.includes('Alpha words') && document.querySelectorAll('.report-body')[1].textContent.includes('Alpha words')"), "native drag moves text across report sections");
        await view.CallAsync("command", new {name="undo"});
        Check(await True("document.querySelectorAll('.report-body')[0].textContent.includes('Alpha words') && !document.querySelectorAll('.report-body')[1].textContent.includes('Alpha words')"), "one undo restores BOTH sections of a drag move");
        await CheckStandardPresentation(real);
        await CheckTemplateStyles(real);
        await CheckDeletion();
        await CheckRetirement();
    }

    private static async Task CheckStandardPresentation(string real)
    {
        await view.CallAsync("load", new { entries = Array.Empty<object>() });
        await view.AppendAsync(real, "Paired t test and agreement", "TPaired", 1150);
        await view.Browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Emulation.setDeviceMetricsOverride", "{\"width\":1600,\"height\":950,\"deviceScaleFactor\":1,\"mobile\":false}");
        await Js("window.scrollTo(0,0)");
        Check(await True("(()=>{const b=document.querySelector('.report-body'),p=b.querySelector('p'),h=b.querySelector('h1');return b.getBoundingClientRect().left===24 && getComputedStyle(document.body).maxWidth==='none' && getComputedStyle(p).fontFamily.startsWith('Arial') && Math.abs(parseFloat(getComputedStyle(p).fontSize)-40/3)<.01 && getComputedStyle(h).fontSize===getComputedStyle(p).fontSize;})()"), "wide report starts at the left margin with standard Arial 10pt text and headings");
        Check(await True("(()=>{const s=document.querySelector('.report-body svg'),box=s.getBoundingClientRect();return s.getAttribute('width')==='6in' && Math.abs(box.width-576)<1 && box.left===24;})()"), "engine chart keeps its six-inch report width and left alignment on a wide screen");
        await view.AppendAsync("<p><span class='ci'>CI marker</span> <span class='pval'>P marker</span> <span class='score'>Score marker</span> <span class='warn'>Warning marker</span> <span class='subtotal'>Subtotal marker</span> <span class='model'>Model marker</span> <span class='grandtotal'>Grand total marker</span></p><p style='font:18pt Georgia;color:#800080'>User formatting</p>", "Palette", "", 0);
        const string palette = "[...document.querySelectorAll('.report-body')].at(-1).querySelectorAll('span')";
        Check(await True($"JSON.stringify([...{palette}].map(e=>getComputedStyle(e).color))===JSON.stringify(['rgb(0, 0, 255)','rgb(0, 127, 0)','rgb(0, 127, 127)','rgb(255, 0, 0)','rgb(127, 0, 0)','rgb(0, 0, 127)','rgb(0, 0, 127)'])"), "all seven semantic result colours match the standard RTF palette");
        Check(await True("(()=>{const p=[...document.querySelectorAll('.report-body p')].find(p=>p.textContent==='User formatting');return getComputedStyle(p).fontSize==='24px' && getComputedStyle(p).color==='rgb(128, 0, 128)';})()"), "user formatting overrides the default report font and colour");
        await Js("(()=>{const b=document.querySelector('.report-body'),r=document.createRange();b.focus();r.selectNodeContents(b);getSelection().removeAllRanges();getSelection().addRange(r);})()");
        var fragment = await view.CallAsync("prepareClipboard", new { cut = false });
        using var clipboard = JsonDocument.Parse((await view.CallAsync("officeClipboard", new { html = fragment.GetProperty("html").GetString() })).GetString());
        string clipboardHtml = clipboard.RootElement.GetProperty("html").GetString();
        Check(clipboardHtml.Contains("color: rgb(0, 0, 255)") && clipboardHtml.Contains("color: rgb(0, 127, 0)") && clipboardHtml.Contains("Arial"), "Office clipboard retains CI blue, P-value green and Arial");
        string before = (await view.CallAsync("snapshot")).GetRawText();
        // Export BEFORE any import has helpfully inlined the stylesheet. This
        // reproduces losing class-based colours in a freshly generated report.
        await view.SaveAsync(System.IO.Path.Combine(output, "standard-style.docx"), "Standard report style", "docx");
        using (var zip = ZipFile.OpenRead(System.IO.Path.Combine(output, "standard-style.docx")))
        {
            using var reader = new StreamReader(zip.GetEntry("word/document.xml").Open());
            var doc = XDocument.Parse(await reader.ReadToEndAsync());
            XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            string RunColour(string marker) => doc.Descendants(w + "r").First(r => r.Descendants(w + "t").Any(t => t.Value.Contains(marker))).Element(w + "rPr")?.Element(w + "color")?.Attribute(w + "val")?.Value;
            Check(RunColour("CI marker") == "0000FF" && RunColour("P marker") == "007F00" && RunColour("Warning marker") == "FF0000" && doc.Descendants(w + "rFonts").Any(f => (string)f.Attribute(w + "ascii") == "Arial"), "fresh DOCX export resolves stylesheet colours and report fonts into Word runs");
        }
        Check((await view.CallAsync("snapshot")).GetRawText() == before, "export styling does not mutate the report or create an edit");
        string htmlPath = System.IO.Path.Combine(output, "standard-style.html");
        await view.SaveAsync(htmlPath, "Standard report style", "html");
        await view.OpenHtmlAsync(await File.ReadAllTextAsync(htmlPath), "Reopened standard report");
        Check(await True("(()=>{const spans=[...document.querySelectorAll('.report-body span')],find=t=>spans.find(s=>s.textContent===t);return getComputedStyle(find('CI marker')).color==='rgb(0, 0, 255)' && getComputedStyle(find('P marker')).color==='rgb(0, 127, 0)' && getComputedStyle(find('Warning marker')).color==='rgb(255, 0, 0)' && getComputedStyle(document.querySelector('.report-body p')).fontFamily.startsWith('Arial');})()"), "editable HTML reopening retains semantic colours and the standard font");
        await Js("window.getSelection().removeAllRanges();window.scrollTo(0,0)");
        using (var png = File.Create(System.IO.Path.Combine(output, "standard-style.png"))) await view.Browser.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, png);
    }
}
