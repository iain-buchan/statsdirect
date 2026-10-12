using Microsoft.Web.WebView2.Core;
using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;

internal static partial class Program
{
    private static async Task CheckChartAlignment()
    {
        await view.Browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Emulation.setDeviceMetricsOverride", JsonSerializer.Serialize(new { width=1180, height=850, deviceScaleFactor=1, mobile=false }));
        view.Browser.ZoomFactor=1;
        string Svg(string label,string colour)=>$"<svg aria-label='{label}' width='240' height='120' viewBox='0 0 240 120' xmlns='http://www.w3.org/2000/svg'><rect width='240' height='120' fill='{colour}'/><text x='8' y='22'>{label}</text></svg>";
        const string png="data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAIAAAABCAYAAAD0In+KAAAAD0lEQVR4nGP4v4XhPwgDABj/BWWqqcXlAAAAAElFTkSuQmCC";
        string fixture="<p>Text alignment stays independent.</p>"+Svg("Left chart","#dd2211")+Svg("Centre chart","#11bb33")+Svg("Right chart","#2244dd")+$"<img alt='Right picture' src='{png}' style='width:160px;height:80px'>";
        await view.CallAsync("load",new {html=fixture,title="Chart alignment"});
        await Js("document.querySelector('.report-body').focus();window.scrollTo(0,0)");await Task.Delay(100);
        const string geometry="(()=>[...document.querySelectorAll('.report-body .report-media')].map(w=>{const p=w.querySelector('svg,img'),b=p.getBoundingClientRect(),r=w.getBoundingClientRect();return {left:b.left-r.left,right:r.right-b.right,width:b.width};}))()";
        async Task Aligned(string label,params string[] expected)
        {
            var positions=(await Js(geometry)).EnumerateArray().ToArray();
            bool ok=positions.Length==expected.Length;
            for(int i=0;ok&&i<expected.Length;i++)
            {
                double left=positions[i].GetProperty("left").GetDouble(),right=positions[i].GetProperty("right").GetDouble();
                ok=expected[i]=="left"?Math.Abs(left)<1:expected[i]=="right"?Math.Abs(right)<1:Math.Abs(left-right)<1;
            }
            Check(ok,label+" "+JsonSerializer.Serialize(positions));
        }
        async Task Select(int index)
        {
            await Js($"document.querySelectorAll('.report-body .report-media')[{index}].querySelector('svg,img').dispatchEvent(new MouseEvent('click',{{bubbles:true}}))");await Task.Delay(45);
        }
        async Task Align(string command)
        {
            await Js($"(()=>{{const b=document.querySelector('[data-command={command}]');b.dispatchEvent(new MouseEvent('mousedown',{{bubbles:true,cancelable:true}}));b.click();}})()");await Task.Delay(45);
        }
        async Task<long> Revision()=>(await view.CallAsync("snapshot")).GetProperty("revision").GetInt64();
        async Task Undo()=>await view.CallAsync("command",new{name="undo"});
        await Aligned("new SVG and raster charts default to the left margin","left","left","left","left");
        await Select(1);
        Check(await True("document.querySelector('[data-command=justifyLeft]').getAttribute('aria-pressed')==='true'&&document.querySelector('[data-command=justifyFull]').disabled"),"selected chart reports left alignment and disables Justify");
        long revision=await Revision();await Align("justifyCenter");
        await Aligned("Centre aligns only the selected SVG","left","center","left","left");
        Check(await Revision()==revision+1&&await True("document.querySelector('[data-command=justifyCenter]').getAttribute('aria-pressed')==='true'&&getComputedStyle(document.querySelector('.report-body p')).textAlign!=='center'"),"one chart alignment makes one edit, updates its button and leaves neighbouring text alone");
        revision=await Revision();await Align("justifyCenter");
        Check(await Revision()==revision,"repeated chart alignment does not add an undo transaction");
        await Undo();await Aligned("single Undo restores chart alignment","left","left","left","left");
        await view.CallAsync("command",new{name="redo"});await Aligned("single Redo restores Centre","left","center","left","left");
        await Select(2);Check(await True("StatsDirectReportEditor.menuCommand('justifyRight')"),"native menu alignment handles a selected chart");
        await Select(3);await Align("justifyRight");await Aligned("Right aligns both SVG and raster pictures","left","center","right","right");
        Check(await True("[...document.querySelectorAll('.report-body svg')].every(p=>{const original=p.cloneNode(true);original.style.fontFamily='Arial';original.style.fill='#345678';original.style.marginTop='16px';original.style.marginBottom='16px';const before=original.getAttribute('style'),svg=StatsDirectReportEditor.exportableSvg(original);return ![...svg.style].some(n=>n.startsWith('margin'))&&svg.style.fontFamily==='Arial'&&svg.style.fill==='rgb(52, 86, 120)'&&original.getAttribute('style')===before;})"),"standalone SVG drops report margins for every alignment, retains paint/font styles and does not mutate its source");
        await Js("(()=>{const p=document.querySelector('.report-body p');document.querySelector('.report-body').focus();getSelection().selectAllChildren(p);})()");await Task.Delay(45);
        Check(await True("StatsDirectReportEditor.selectedPicture()===null&&!document.querySelector('[data-command=justifyFull]').disabled"),"selecting text restores text alignment controls");
        await Align("justifyCenter");Check(await True("getComputedStyle(document.querySelector('.report-body p')).textAlign==='center'"),"alignment still works on text after selecting a chart");
        await Aligned("text alignment preserves all picture positions","left","center","right","right");
        await Js("(()=>{document.querySelector('.report-body').focus();const w=document.querySelectorAll('.report-media'),r=document.createRange();r.setStartBefore(w[0]);r.setEndAfter(w[1]);getSelection().removeAllRanges();getSelection().addRange(r);})()");
        Check(await True("StatsDirectReportEditor.selectedPicture()===null"),"a multiple-chart selection does not target its first chart");
        await Js("(()=>{const b=document.querySelector('.report-body'),r=document.createRange();r.setStartBefore(b.querySelector('p'));r.setEndAfter(b.querySelector('.report-media'));getSelection().removeAllRanges();getSelection().addRange(r);})()");
        Check(await True("StatsDirectReportEditor.selectedPicture()===null"),"mixed text and picture selection is not treated as one picture");
        foreach(var (index,delta,growth) in new[]{(0,30,30),(1,30,60),(2,-30,30)})
        {
            await Select(index);revision=await Revision();
            var result=await Js($$"""
                (()=>{const w=document.querySelectorAll('.report-media')[{{index}}],p=w.querySelector('svg'),h=w.querySelector('button'),b=p.getBoundingClientRect(),r=h.getBoundingClientRect();
                  const x=r.left+r.width/2,y=r.top+r.height/2,ev=(type,target,dx)=>target.dispatchEvent(new PointerEvent(type,{bubbles:true,cancelable:true,pointerId:19,button:0,clientX:x+dx,clientY:y}));
                  ev('pointerdown',h,0);ev('pointermove',document,{{delta}});ev('pointerup',document,{{delta}});
                  const a=p.getBoundingClientRect();ev('pointerdown',h,0);ev('pointermove',document,20);document.dispatchEvent(new KeyboardEvent('keydown',{key:'Escape',bubbles:true,cancelable:true}));
                  return {growth:a.width-b.width,anchor:{{index}}===0?a.left-b.left:{{index}}===1?(a.left+a.right-b.left-b.right)/2:a.right-b.right,cancelled:p.getBoundingClientRect().width-a.width};})()
                """);
            Check(Math.Abs(result.GetProperty("growth").GetDouble()-growth)<1&&Math.Abs(result.GetProperty("anchor").GetDouble())<1&&Math.Abs(result.GetProperty("cancelled").GetDouble())<1,"chart "+index+" resize follows its free corner, retains its anchor and cancels with Escape");
            Check(await Revision()==revision+1,"resize makes one edit and Escape adds none for chart "+index);await Undo();
        }
        await Aligned("undoing each resize retains all three alignments","left","center","right","right");
        await Js("document.querySelectorAll('.report-media')[2].focus();getSelection().removeAllRanges()");
        Check(await True("StatsDirectReportEditor.menuCommand('justifyCenter')"),"focused chart can align with no text selection");
        await Aligned("keyboard-focused chart moves to the centre","left","center","center","right");
        await Select(2);await Js("document.querySelector('[data-command=justifyRight]').focus();document.querySelector('[data-command=justifyRight]').click()");await Task.Delay(45);
        Check(await True("document.querySelector('[data-command=justifyRight]').getAttribute('aria-pressed')==='true'"),"toolbar keyboard focus retains chart target and reports its new alignment");
        await Select(2);await view.CallAsync("command",new{name="pictureFit"});await view.CallAsync("command",new{name="pictureWidth",value=240});
        await Aligned("Fit then narrowing preserves the chosen right alignment","left","center","right","right");
        var selected=await view.CallAsync("prepareClipboard",new{cut=false});string privateHtml=selected.GetProperty("html").GetString();
        using var office=JsonDocument.Parse((await view.CallAsync("officeClipboard",new{html=privateHtml})).GetString());string publicHtml=office.RootElement.GetProperty("html").GetString();
        await File.WriteAllTextAsync(Path.Combine(output,"chart-alignment-clipboard.html"),publicHtml);
        await File.WriteAllTextAsync(Path.Combine(output,"chart-alignment-clipboard.cfhtml"),StatsDirect.UI.WebReports.ReportClipboard.EncodeHtml(publicHtml));
        await Js("window.alignmentClipboard="+JsonSerializer.Serialize(publicHtml));
        Check(await True("(()=>{const d=new DOMParser().parseFromString(alignmentClipboard,'text/html'),p=d.querySelector('img');return p.src.startsWith('data:image/png')&&p.style.marginLeft==='auto'&&parseFloat(p.style.marginRight)===0&&p.parentElement.style.textAlign==='right';})()"),"Office SVG-to-PNG fallback retains right alignment");
        await Js("document.querySelector('.report-body').focus()");
        await view.Browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Emulation.setEmulatedMedia","{\"media\":\"print\"}");
        Check(await True("[...document.querySelectorAll('.report-body,.report-media')].every(e=>getComputedStyle(e).outlineStyle==='none')"),"printing omits focused report and chart editing outlines");
        await view.Browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Emulation.setEmulatedMedia","{\"media\":\"\"}");
        foreach(string format in new[]{"html","docx","pdf"})await view.SaveAsync(Path.Combine(output,"chart-alignment."+format),"Chart alignment",format);
        VerifyAlignmentWord("chart-alignment.docx");
        foreach(var (html,label) in new[]{(privateHtml,"private SVG"),(publicHtml,"public raster fallback")})
        {
            await view.CallAsync("load",new{html="<p>Paste here</p>",title="Paste alignment"});
            await Js("document.querySelector('.report-body').focus();getSelection().selectAllChildren(document.querySelector('.report-body'))");
            string token=(await view.CallAsync("preparePaste")).GetProperty("token").GetString();revision=await Revision();
            Check((await view.CallAsync("paste",new{token,payload=new{html,text=""}})).GetBoolean(),label+" clipboard content pastes");
            await Aligned(label+" paste keeps its right alignment","right");
            Check(await Revision()==revision+1,label+" paste is one edit");await Undo();
            Check(await True("!document.querySelector('.report-body svg,.report-body img')"),label+" paste undoes in one step");
        }
        await view.OpenHtmlAsync(await File.ReadAllTextAsync(Path.Combine(output,"chart-alignment.html")),"Reopened chart alignment");
        await Aligned("saved HTML preserves explicit picture alignments","left","center","right","right");
        await view.Browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Emulation.setDeviceMetricsOverride",JsonSerializer.Serialize(new{width=900,height=850,deviceScaleFactor=1,mobile=false}));await Task.Delay(100);
        await Aligned("reopened chart alignments remain responsive after resizing","left","center","right","right");
        await view.SaveAsync(Path.Combine(output,"chart-alignment-reopened.docx"),"Reopened alignment","docx");VerifyAlignmentWord("chart-alignment-reopened.docx");
        using(var shot=File.Create(Path.Combine(output,"chart-alignment-reopened.png")))await view.Browser.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,shot);
        await view.OpenHtmlAsync("<style>.saved-centre{margin-left:auto;margin-right:auto}.saved-right{margin-left:auto;margin-right:0}</style>"+Svg("Imported centre","#11bb33").Replace("<svg ","<svg class='saved-centre' ")+Svg("Imported right","#2244dd").Replace("<svg ","<svg class='saved-right' "),"Imported alignment");
        await Aligned("explicit imported stylesheet alignments override Windows defaults","center","right");
        await view.OpenHtmlAsync("<style>.saved-centre{margin-left:auto;margin-right:auto}</style><div class='report-body'>"+Svg("Imported report centre","#11bb33").Replace("<svg ","<svg class='saved-centre' ")+"</div>","Imported report wrapper");
        await Aligned("imported report wrappers cannot override the document's explicit picture alignment","center");
        await view.Browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Emulation.clearDeviceMetricsOverride","{}");
    }
    private static void VerifyAlignmentWord(string file)
    {
        using var zip=ZipFile.OpenRead(Path.Combine(output,file));using var reader=new StreamReader(zip.GetEntry("word/document.xml").Open());var doc=XDocument.Parse(reader.ReadToEnd());
        XNamespace w="http://schemas.openxmlformats.org/wordprocessingml/2006/main",wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";
        var actual=doc.Descendants(w+"p").Where(p=>p.Descendants(wp+"docPr").Any()).ToDictionary(p=>(string)p.Descendants(wp+"docPr").First().Attribute("descr"),p=>(string)p.Element(w+"pPr")?.Element(w+"jc")?.Attribute(w+"val"));
        Check(actual.Count==4&&actual["Left chart"]=="left"&&actual["Centre chart"]=="center"&&actual["Right chart"]=="right"&&actual["Right picture"]=="right",file+" drawing paragraphs preserve left/centre/right alignment");
        Check(zip.Entries.Count(e=>e.FullName.EndsWith(".svg"))==3&&zip.Entries.Count(e=>e.FullName.EndsWith(".png"))>=4,file+" keeps the three SVGs and raster fallbacks");
    }
}
