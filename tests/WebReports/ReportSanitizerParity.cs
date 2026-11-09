using System.Text.Json;

internal static partial class Program
{
    // Opening a report and pasting into one sanitize alike (StatsDirect Mac review F8, 9 November 2026). A paste kept a video
    // poster and table and cell backgrounds that opening removes, and both paths kept animateColor. A style attribute keeps only
    // its filtered declarations, so an external image in MathML or behind image-set() reaches neither path. WebView2 already
    // filtered those last two; the guards matter where an element has no style object.
    private static async Task CheckReportSanitizerParity()
    {
        const string payload = """
            <video poster="https://invalid.example/poster.png"></video>
            <table background="https://invalid.example/t.png"><tr><td background="//invalid.example/c.png">c</td></tr></table>
            <svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="9" height="9"><rect fill="red" width="9" height="9"><animateColor attributeName="fill" values="red;blue" dur="1s" repeatCount="indefinite"/></rect><animateColor xlink:href="#t" attributeName="href" values="javascript:alert(1)"/></svg>
            <math><mi style="background-image:url(https://invalid.example/m.png);color:red">x</mi></math>
            <p style="mask:image-set(&quot;https://invalid.example/m.png&quot; 1x);color:red">p</p>
            """;
        // what reached the report body that neither path may keep, and how many copies of the harmless content arrived
        async Task<(string[] Leaks, int Cells, int Rects)> Inspect()
        {
            JsonElement found = await Js("""
                (()=>{
                  const body=document.querySelector('.report-body'),leaks=[];
                  for(const el of body.querySelectorAll('*')){
                    const name=el.localName.toLowerCase();
                    if(['video','audio','source','track','animatecolor'].includes(name))leaks.push('element '+name);
                    for(const attr of [...el.attributes])
                      if(['poster','background','cite','download'].includes(attr.name.toLowerCase())||/invalid\.example|javascript:/i.test(attr.value))leaks.push(name+' '+attr.name+'="'+attr.value+'"');
                  }
                  return {leaks,cells:[...body.querySelectorAll('td')].filter(td=>td.textContent==='c').length,
                    rects:[...body.querySelectorAll('rect')].filter(r=>r.getAttribute('fill')==='red').length};
                })()
                """);
            return (found.GetProperty("leaks").EnumerateArray().Select(e => e.GetString()).ToArray(), found.GetProperty("cells").GetInt32(), found.GetProperty("rects").GetInt32());
        }
        await view.OpenHtmlAsync("<p>Opened content</p>" + payload + "<p>Paste here</p>", "Sanitizer parity");
        var opened = await Inspect();
        Check(opened.Leaks.Length == 0, "an opened report keeps no media element, poster, background or animateColor and no external address: " + string.Join("; ", opened.Leaks));
        Check(opened.Cells == 1 && opened.Rects == 1, $"an opened report keeps the table cell and the chart's rectangle ({opened.Cells} cells, {opened.Rects} rectangles)");

        await Js("(()=>{const b=document.querySelector('.report-body');b.focus();const p=b.querySelector('p:last-child');const r=document.createRange();r.selectNodeContents(p);r.collapse(false);getSelection().removeAllRanges();getSelection().addRange(r);})()");
        string token = (await view.CallAsync("preparePaste")).GetProperty("token").GetString();
        Check((await view.CallAsync("paste", new { token, payload = new { html = payload, text = "" } })).GetBoolean(), "the same content pastes through the typed report API");
        var pasted = await Inspect();
        Check(pasted.Leaks.Length == 0, "a paste keeps no media element, poster, background or animateColor and no external address, as opening does: " + string.Join("; ", pasted.Leaks));
        Check(pasted.Cells == 2 && pasted.Rects == 2, $"a paste keeps the table cell and the chart's rectangle ({pasted.Cells} cells, {pasted.Rects} rectangles)");
    }
}
