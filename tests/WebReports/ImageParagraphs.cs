using System.Text.Json;

internal static partial class Program
{
    private const string ParagraphPixel = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=";
    private const string ParagraphRecord = "{\"version\":1,\"operation\":\"ImageParagraphCheck\",\"parameters\":{\"gamma\":0.95},\"values\":{\"marker\":17}}";
    private static async Task<string> LoadImageParagraphFixture(string html)
    {
        await view.CallAsync("load", new { entries = Array.Empty<object>() });
        await view.AppendAsync("<p>Earlier result</p>", "Earlier", "First", 1);
        string id = await view.AppendAsync(html, "Picture result", "ImageParagraphCheck", 1150, ParagraphRecord);
        await view.AppendAsync("<p>Following result</p>", "Following", "Last", 2);
        await Js("StatsDirectReportEditor.setEditing(true);window.scrollTo(0,0)");
        return id;
    }
    private static async Task CaretAfterPicture()
    {
        await Js("(()=>{const image=document.querySelector('.report-entry:nth-child(2) .report-media');document.getElementById('results').focus();const r=document.createRange();r.setStartAfter(image);r.collapse(true);getSelection().removeAllRanges();getSelection().addRange(r);})()");
    }
    private static async Task ParagraphEnter()
    {
        // The text field also exercises Chromium's native default action when
        // the report handler deliberately leaves an ordinary text caret alone.
        await view.Browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent", JsonSerializer.Serialize(new { type = "keyDown", key = "Enter", code = "Enter", windowsVirtualKeyCode = 13, text = "\r", unmodifiedText = "\r" }));
        await view.Browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent", "{\"type\":\"keyUp\",\"key\":\"Enter\",\"code\":\"Enter\",\"windowsVirtualKeyCode\":13}");
        await view.CallAsync("snapshot");
    }
    private static async Task ParagraphType(string text)
    {
        await view.Browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.insertText", JsonSerializer.Serialize(new { text }));
        await view.CallAsync("snapshot");
    }
    private const string EditablePictureParagraph = "(()=>{const e=document.querySelector('.report-entry:nth-child(2)'),p=e.querySelector('.report-media').nextElementSibling,s=getSelection();return p?.tagName==='P'&&p.isContentEditable&&!p.closest('[contenteditable=false]')&&s.isCollapsed&&p.contains(s.anchorNode)&&e.contains(p);})()";
    private static async Task CheckImageParagraphs()
    {
        view.Browser.ZoomFactor = 1;
        foreach (bool vector in new[] { true, false })
        {
            string kind = vector ? "SVG" : "PNG";
            string picture = vector ? DeletionChart : "<img width='100' height='100' alt='Raster check' src='" + ParagraphPixel + "'>";
            string id = await LoadImageParagraphFixture("<p>Original result</p>" + picture);
            string ids = (await Js("JSON.stringify([...document.querySelectorAll('.report-entry')].map(e=>e.id))")).GetString();
            await CaretAfterPicture();
            await ParagraphEnter();
            Check(await True(EditablePictureParagraph), kind + " caret-after-picture Enter creates an editable paragraph in the same result");
            Check((await Js("JSON.stringify([...document.querySelectorAll('.report-entry')].map(e=>e.id))")).GetString() == ids &&
                  (await view.RecordAsync(id)).GetProperty("record").GetProperty("values").GetProperty("marker").GetInt32() == 17 &&
                  (await view.CallAsync("snapshot")).GetProperty("entries")[1].GetProperty("helpContextId").GetInt32() == 1150,
                  kind + " Enter preserves result order, identity and analysis metadata without adding a result");
            await Key("z", 90, 2);
            Check(await True("(()=>{const e=document.querySelector('.report-entry:nth-child(2)');return e.querySelectorAll('p').length===1&&!!e.querySelector('.report-media');})()"), kind + " paragraph insertion undoes without deleting its picture");
            await Key("y", 89, 2);
            Check(await True("document.querySelector('.report-entry:nth-child(2) .report-media').nextElementSibling?.tagName==='P'"), kind + " paragraph insertion redoes");
            // Redo restores content; explicitly place the user's text caret in
            // the restored paragraph before exercising real browser typing.
            await Js("(()=>{const p=document.querySelector('.report-entry:nth-child(2) .report-media').nextElementSibling,r=document.createRange();document.getElementById('results').focus();r.selectNodeContents(p);r.collapse(true);getSelection().removeAllRanges();getSelection().addRange(r);})()");
            await ParagraphType("Interpretation after " + kind);
            Check(await True("document.querySelector('.report-entry:nth-child(2) .report-media').nextElementSibling.textContent===" + JsonSerializer.Serialize("Interpretation after " + kind)), kind + " typing after Enter stays in its new text paragraph");
            await Key("z", 90, 2);
            Check(await True("(()=>{const p=document.querySelector('.report-entry:nth-child(2) .report-media').nextElementSibling;return p?.tagName==='P'&&p.textContent==='';})()"), kind + " typing Undo retains the preceding Enter transaction");
            await Key("y", 89, 2);
            string path = Path.Combine(output, "image-paragraph-" + kind.ToLowerInvariant() + ".html");
            await view.SaveAsync(path, "Picture paragraph", "html");
            await view.OpenHtmlAsync(await File.ReadAllTextAsync(path), "Reopened picture paragraph");
            var reopened = (await view.CallAsync("snapshot")).GetProperty("entries");
            Check(reopened.GetArrayLength() == 3 && reopened[1].GetProperty("html").GetString().Contains("Interpretation after " + kind) &&
                  reopened[1].GetProperty("record").GetProperty("values").GetProperty("marker").GetInt32() == 17 &&
                  await True(vector ? "!!document.querySelector('.report-entry:nth-child(2) svg path')" : "document.querySelector('.report-entry:nth-child(2) img').src.startsWith('data:image/png')"),
                  kind + " paragraph, picture and record survive HTML saving and reopening");

            await LoadImageParagraphFixture(picture + "<p>Existing following text</p>");
            await ClickReport(vector ? ".report-entry:nth-child(2) svg" : ".report-entry:nth-child(2) img");
            await ParagraphEnter();
            await ParagraphType("Selected picture interpretation");
            Check(await True("(()=>{const e=document.querySelector('.report-entry:nth-child(2)'),p=e.querySelector('.report-media').nextElementSibling;return p?.tagName==='P'&&p.textContent==='Selected picture interpretation'&&p.nextElementSibling?.textContent==='Existing following text'&&e.querySelectorAll('.report-media').length===1;})()"), kind + " selected-picture Enter inserts before existing text and preserves the picture");
        }

        await LoadImageParagraphFixture("<div class='report-chart' contenteditable='false'>" + DeletionChart + "</div>");
        await CaretAfterPicture();
        await ParagraphEnter();
        Check(await True("(()=>{const e=document.querySelector('.report-entry:nth-child(2)'),c=e.querySelector('.report-chart'),p=c.nextElementSibling;return p?.tagName==='P'&&p.isContentEditable&&p.parentElement===e&&!p.closest('[contenteditable=false]');})()"), "Enter exits a protected chart container into editable text in the same result");

        await LoadImageParagraphFixture("<p><img width='100' height='100' src='" + ParagraphPixel + "'></p>");
        await CaretAfterPicture();
        await ParagraphEnter();
        Check(await True("(()=>{const e=document.querySelector('.report-entry:nth-child(2)'),p=e.querySelector('img').closest('p');return !!p&&p.nextElementSibling?.tagName==='P'&&p.nextElementSibling.isContentEditable&&!e.querySelector('p p');})()"), "Enter after a picture-only paragraph creates a sibling paragraph rather than nested paragraphs");

        await LoadImageParagraphFixture("<table><tr><td>" + DeletionChart + "</td><td>Other cell</td></tr></table>");
        await CaretAfterPicture();
        await ParagraphEnter();
        Check(await True("(()=>{const cell=document.querySelector('.report-entry:nth-child(2) td'),p=cell.querySelector('.report-media').nextElementSibling;return p?.tagName==='P'&&p.parentElement===cell&&p.isContentEditable&&cell.nextElementSibling.textContent==='Other cell'&&!document.querySelector('tr > p');})()"), "Enter after a picture in a table cell keeps text inside that cell");

        await LoadImageParagraphFixture(DeletionChart + "<p>AlphaOmega</p>");
        await Selection(".report-entry:nth-child(2) > p", 5, 5);
        await ParagraphEnter();
        Check(await True("(()=>{const e=document.querySelector('.report-entry:nth-child(2)'),p=[...e.querySelectorAll(':scope > p')];return p.length===2&&p[0].textContent==='Alpha'&&p[1].textContent==='Omega'&&e.querySelectorAll('.report-media').length===1;})()"), "ordinary text Enter still splits a paragraph with Chromium's native editing");

        await LoadImageParagraphFixture(DeletionChart);
        await Selection(".report-entry:nth-child(3) > p", 0, 0);
        await ParagraphEnter();
        Check(await True("document.querySelector('.report-entry:nth-child(2)').querySelectorAll('p').length===0&&document.querySelector('.report-entry:nth-child(3)').textContent==='Following result'"), "Enter at the following result's text caret does not cross back into the previous chart result");

        await LoadImageParagraphFixture(DeletionChart);
        await CaretAfterPicture();
        Check(await True("(()=>{const p=document.querySelector('.report-entry:nth-child(2)'),html=p.innerHTML;for(const flags of [{shiftKey:true},{ctrlKey:true},{altKey:true},{metaKey:true},{isComposing:true}]){const e=new KeyboardEvent('keydown',{key:'Enter',bubbles:true,cancelable:true,...flags});document.getElementById('results').dispatchEvent(e);if(e.defaultPrevented||p.innerHTML!==html)return false;}return true;})()"), "modified and composing Enter keys are left to the browser");
        await Js("StatsDirectReportEditor.setEditing(false)");
        string before = (await view.CallAsync("snapshot")).GetProperty("entries").GetRawText();
        await ParagraphEnter();
        Check((await view.CallAsync("snapshot")).GetProperty("entries").GetRawText() == before, "Enter cannot add a picture paragraph in reading mode");
        await Js("StatsDirectReportEditor.setEditing(true)");
        await CaretAfterPicture();
        await Js("document.getElementById('results').dispatchEvent(new InputEvent('beforeinput',{inputType:'insertParagraph',bubbles:true,cancelable:true}))");
        await view.CallAsync("snapshot");
        Check(await True(EditablePictureParagraph), "paragraph beforeinput also creates text after a picture for non-keyboard input");
    }
}
