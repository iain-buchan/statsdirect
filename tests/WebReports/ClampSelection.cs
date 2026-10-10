internal static partial class Program
{
    // A selection of the whole page, toolbar and all, becomes the selection of all results, so that a copy goes through the editor
    private static async Task CheckClampedSelection()
    {
        await view.CallAsync("load", new { entries = new[] { new { id = "a", title = "First", operation = "One", helpContextId = 0, html = "<p>First result text</p>" }, new { id = "b", title = "Second", operation = "Two", helpContextId = 0, html = "<p>Second result text</p>" } } });
        // Ctrl+A with the focus on the page, outside the results
        await Js("document.body.focus();getSelection().removeAllRanges();document.body.dispatchEvent(new KeyboardEvent('keydown',{key:'a',ctrlKey:true,bubbles:true,cancelable:true}))");
        Check(await True("(()=>{const r=getSelection().getRangeAt(0),results=document.getElementById('results');return document.querySelectorAll('.report-result-selected').length===2&&results.contains(r.startContainer)&&results.contains(r.endContainer)})()"), "Ctrl+A outside the results selects all results, not the page");
        // a selection of the whole page, as the browser's select all makes it, before the clipboard is prepared
        await Js("getSelection().selectAllChildren(document.body)");
        var prepared = await view.CallAsync("prepareClipboard", new { cut = false });
        string html = prepared.ValueKind == System.Text.Json.JsonValueKind.Object && prepared.TryGetProperty("html", out var h) ? h.GetString() : "";
        Check(html.Contains("First result text") && html.Contains("Second result text") && !html.Contains("Add text"), "a selection of the whole page is clamped to the results when the clipboard is prepared");
    }
}
