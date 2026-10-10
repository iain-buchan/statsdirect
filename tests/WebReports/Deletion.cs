using System.Text.Json;

internal static partial class Program
{
    private const string DeletionChart = "<svg xmlns='http://www.w3.org/2000/svg' width='300' height='120' viewBox='0 0 300 120'><rect width='300' height='120' fill='white'/><path d='M10 100L250 20' stroke='blue'/><text x='20' y='30'>Chart β</text></svg>";
    private static async Task Key(string key, int code, int modifiers = 0)
    {
        foreach (string type in new[] { "keyDown", "keyUp" })
            await view.Browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent", JsonSerializer.Serialize(new { type, key, windowsVirtualKeyCode = code, modifiers }));
        await view.CallAsync("snapshot");
    }
    private static async Task ClickReport(string selector)
    {
        var rect = await Js($"(()=>{{const e=document.querySelector({JsonSerializer.Serialize(selector)});e.scrollIntoView({{block:'center'}});const r=e.getBoundingClientRect();return {{x:r.left+Math.min(r.width/2,50),y:r.top+r.height/2}};}})()");
        foreach (string type in new[] { "mousePressed", "mouseReleased" })
            await view.Browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent", JsonSerializer.Serialize(new { type, x=rect.GetProperty("x").GetDouble(),y=rect.GetProperty("y").GetDouble(), button="left", buttons=type=="mousePressed"?1:0, clickCount=1 }));
    }
    private static async Task Selection(string selector, int start, int end)
    {
        await Js($"(()=>{{const e=document.querySelector({JsonSerializer.Serialize(selector)});e.closest('.report-body').focus();const r=document.createRange();r.setStart(e.firstChild,{start});r.setEnd(e.firstChild,{end});getSelection().removeAllRanges();getSelection().addRange(r);}})()");
    }
    private static async Task LoadDeletionFixture()
    {
        await view.CallAsync("load", new { entries = new[] {
            new {html="<p>Alpha words</p>",title="First",operation="TPaired",helpContextId=1150},
            new {html="<p><span class='ci'>Confidence interval</span></p>"+DeletionChart+"<p>After chart</p>",title="Second",operation="Agreement",helpContextId=1002},
            new {html="<p>Omega words</p>",title="Third",operation="Description",helpContextId=42}
        }});
        await Js("StatsDirectReportEditor.setEditing(true);window.scrollTo(0,0)");
    }
    private static async Task CheckDeletion()
    {
        view.Browser.ZoomFactor=1;
        await LoadDeletionFixture();
        Check(await True("![...document.querySelectorAll('button')].some(b=>/Remove result|Remove plot|Undo removal/.test(b.textContent))"), "removal buttons and separate removal undo are absent");

        await Selection(".report-entry:first-child p",6,11);
        await Key("Delete",46);
        Check(await True("document.querySelector('.report-body p').textContent==='Alpha '"), "native Delete removes selected text only");
        await Key("z",90,2);
        Check(await True("document.querySelector('.report-body p').textContent==='Alpha words'"), "Ctrl+Z restores deleted text");
        await Key("y",89,2);
        Check(await True("document.querySelector('.report-body p').textContent==='Alpha '"), "Ctrl+Y redoes text deletion");
        await Key("z",90,2);

        await ClickReport(".report-body svg");
        await Key("Delete",46);
        Check(await True("!document.querySelector('.report-body svg') && document.querySelectorAll('.report-entry').length===3 && document.querySelectorAll('.report-body')[1].textContent.includes('After chart')"), "clicking a chart then Delete removes its vector object without neighbouring text");
        await Key("z",90,2);
        Check(await True("!!document.querySelector('.report-body svg path') && document.querySelector('.report-body svg').getAttribute('viewBox')==='0 0 300 120'"), "Ctrl+Z restores the complete SVG chart");
        await ClickReport(".report-body svg");
        await Key("Backspace",8);
        Check(await True("!document.querySelector('.report-body svg')"), "Backspace also deletes a selected chart");
        await Key("z",90,2);

        await Selection(".report-entry:nth-child(2) .report-body > p:last-child",0,0);
        await Key("Backspace",8);
        Check(await True("!document.querySelector('.report-body svg') && document.querySelectorAll('.report-body')[1].textContent.includes('After chart')"), "Backspace at the start of the following paragraph deletes the adjacent chart");
        await Key("z",90,2);
        await Js("(()=>{const b=document.querySelectorAll('.report-body')[1],p=b.querySelector('p'),r=document.createRange();b.focus();r.selectNodeContents(p);r.collapse(false);getSelection().removeAllRanges();getSelection().addRange(r);})()");
        await Key("Delete",46);
        Check(await True("!document.querySelector('.report-body svg')"), "Delete at the end of the preceding paragraph deletes the adjacent chart");
        await Key("z",90,2);

        await Selection(".report-entry:first-child p",6,11);
        await Key("Delete",46);
        await ClickReport(".report-entry:nth-child(2) header h2");
        await Key("Delete",46);
        Check(await True("document.querySelectorAll('.report-entry').length===2 && !document.querySelector('.report-body svg')"), "selecting a result heading then Delete removes the whole result");
        await Key("z",90,2);
        var restored=await view.CallAsync("snapshot");
        Check(restored.GetProperty("entries")[1].GetProperty("operation").GetString()=="Agreement" && restored.GetProperty("entries")[1].GetProperty("helpContextId").GetInt32()==1002 && await True("document.querySelector('.report-body p').textContent==='Alpha ' && !!document.querySelector('.report-body svg')"), "first Undo restores result order, metadata and chart while retaining the preceding text edit");
        await Key("z",90,2);
        Check(await True("document.querySelector('.report-body p').textContent==='Alpha words'"), "second Undo restores the earlier text edit in chronological order");
        await Key("y",89,2);
        await Key("y",89,2);
        Check(await True("document.querySelectorAll('.report-entry').length===2 && document.querySelector('.report-body p').textContent==='Alpha '"), "Redo reapplies both text and whole-result deletion in order");

        await LoadDeletionFixture();
        await Js("(()=>{const a=document.querySelector('.report-body p').firstChild,z=document.querySelectorAll('.report-body')[2].querySelector('p').firstChild,r=document.createRange();a.parentElement.closest('.report-body').focus();r.setStart(a,6);r.setEnd(z,6);getSelection().removeAllRanges();getSelection().addRange(r);})()");
        await Key("Delete",46);
        Check(await True("document.querySelectorAll('.report-entry').length===2 && document.querySelectorAll('.report-body')[0].textContent==='Alpha ' && document.querySelectorAll('.report-body')[1].textContent==='words'"), "cross-result selection deletes partial text and the fully selected middle result atomically");
        await Key("z",90,2);
        Check(await True("document.querySelectorAll('.report-entry').length===3 && document.querySelectorAll('.report-body')[0].textContent==='Alpha words' && document.querySelectorAll('.report-body')[2].textContent==='Omega words' && !!document.querySelector('.report-body svg')"), "one Undo restores both partial sections and the intervening whole result");

        await Js("document.querySelector('.report-body').focus()");
        await Key("a",65,2);
        await Key("Backspace",8);
        Check((await view.CallAsync("snapshot")).GetProperty("entries").GetArrayLength()==0, "Ctrl+A and Backspace delete the whole report including every chart");
        await Key("z",90,2);
        Check((await view.CallAsync("snapshot")).GetProperty("entries").GetArrayLength()==3 && await True("!!document.querySelector('.report-body svg')"), "Undo works even after all report sections were removed");

        await ClickReport(".report-entry:nth-child(2) header h2");
        await Key("Delete",46);
        await view.AppendAsync("<p>New engine result</p>","Arrived later","NewOperation",99);
        await Key("z",90,2);
        Check(await True("document.querySelectorAll('.report-entry').length===4 && document.querySelectorAll('.report-body')[3].textContent==='New engine result' && !!document.querySelector('.report-body svg')"), "restoring a deleted result preserves newly appended engine output");

        await LoadDeletionFixture();
        await Js("StatsDirectReportEditor.setEditing(false);document.querySelector('header').focus()");
        var readOnly=(await view.CallAsync("snapshot")).GetRawText();
        await Key("Delete",46);
        Check((await view.CallAsync("snapshot")).GetRawText()==readOnly, "Delete cannot remove a result in reading mode");
        await Js("StatsDirectReportEditor.setEditing(true);const size=document.querySelector('[data-format=fontSize]');size.focus();size.value='12';size.setSelectionRange(0,2)");
        var beforeField=(await view.CallAsync("snapshot")).GetRawText();
        await Key("Backspace",8);
        Check((await view.CallAsync("snapshot")).GetRawText()==beforeField && await True("document.querySelector('[data-format=fontSize]').value===''"), "Backspace in a formatting field edits only that field");

        await view.CallAsync("load",new {entries=new[]{new {html="<table><tr><td colspan='2'>Merged</td></tr><tr><td>Left</td><td>Right</td></tr></table>",title="Table",operation="Table",helpContextId=1}}});
        await Js("StatsDirectReportEditor.setEditing(true)");
        await Selection("td[colspan]",1,5);
        await Key("Delete",46);
        Check(await True("document.querySelector('td[colspan]').textContent==='Md' && document.querySelector('td[colspan]').colSpan===2 && document.querySelectorAll('td').length===3"), "partial table deletion preserves merged cells and unselected data");
        await Key("z",90,2);
        Check(await True("document.querySelector('td[colspan]').textContent==='Merged'"), "Undo restores deleted table text");

        await LoadDeletionFixture();
        await ClickReport(".report-entry:nth-child(2) header h2");
        await Key("Delete",46);
        string filename=Path.Combine(output,"deleted-result.html");
        await view.SaveAsync(filename,"Deletion check","html");
        await view.OpenHtmlAsync(await File.ReadAllTextAsync(filename),"Reopened deletion");
        Check((await view.CallAsync("snapshot")).GetProperty("entries").GetArrayLength()==2 && await True("!document.querySelector('.report-body svg')"), "saving and reopening retains whole-result deletion without resurrecting metadata");

        await LoadDeletionFixture();
        await Selection(".report-entry:first-child p",5,5);
        await Key("Backspace",8);
        Check(await True("document.querySelector('.report-body p').textContent==='Alph words'"), "ordinary caret Backspace still edits text natively");
        await Key("z",90,2);
        Check(await True("document.querySelector('.report-body p').textContent==='Alpha words'"), "Undo restores ordinary native text deletion");
        await Selection(".report-entry:first-child p",0,5);
        await view.Browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.insertText","{\"text\":\"New\"}");
        await view.CallAsync("snapshot");
        await Key("y",89,2);
        Check(await True("document.querySelector('.report-body p').textContent==='New words'"), "new typing after Undo invalidates the abandoned Redo branch");

        await view.CallAsync("load",new {entries=new[]{new {html="",title="Empty result",operation="Empty",helpContextId=7}}});
        await Js("StatsDirectReportEditor.setEditing(true);document.querySelector('header').focus()");
        await Key("Delete",46);
        Check((await view.CallAsync("snapshot")).GetProperty("entries").GetArrayLength()==0, "keyboard focus on a heading can delete even an empty result");
        await Key("z",90,2);
        Check((await view.CallAsync("snapshot")).GetProperty("entries")[0].GetProperty("helpContextId").GetInt32()==7, "Undo restores an empty result and its metadata");

        const string pixel="data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=";
        await view.CallAsync("load",new {entries=new[]{new {html="<p>Before</p><img width='100' height='100' src='"+pixel+"'><p>After</p>",title="Picture",operation="Image",helpContextId=1}}});
        await Js("StatsDirectReportEditor.setEditing(true);document.querySelector('.report-media').focus()");
        await Key("Delete",46);
        Check(await True("!document.querySelector('.report-body img') && document.querySelector('.report-body').textContent==='BeforeAfter'"), "keyboard-selected raster pictures delete without neighbouring text");
        await Key("z",90,2);
        Check(await True("document.querySelector('.report-body img').getAttribute('src').startsWith('data:image/png')"), "Undo restores the raster picture data");

        await LoadDeletionFixture();
        await ClickReport(".report-entry:first-child header h2");
        await Js("document.querySelectorAll('header')[2].querySelector('h2').dispatchEvent(new MouseEvent('click',{bubbles:true,shiftKey:true}))");
        await Key("Delete",46);
        Check((await view.CallAsync("snapshot")).GetProperty("entries").GetArrayLength()==0, "Shift-clicking result headings selects a contiguous group for deletion");
        await Key("z",90,2);
        Check((await view.CallAsync("snapshot")).GetProperty("entries").GetArrayLength()==3, "one Undo restores a group of deleted results");
    }
}
