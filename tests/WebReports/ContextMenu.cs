internal static partial class Program
{
    // The editor's side of the context menu: the target of a right-click is recorded for the menu that C# shows, and a result, or all
    // of them, can be selected from it as a click on a heading and Ctrl+A select them
    private static async Task CheckContextMenu()
    {
        // a report loaded gives its entries new ids: the second's is read from the page
        await view.CallAsync("load", new { entries = new[] { new { id = "first", title = "First", operation = "One", helpContextId = 1001, html = "<p>One</p>" }, new { id = "second", title = "Second", operation = "Two", helpContextId = 1002, html = "<p>Two</p>" } } });
        string second = (await Js("document.querySelectorAll('.report-entry')[1].id.slice(7)")).GetString();
        await Js("document.querySelectorAll('.report-entry')[1].dispatchEvent(new MouseEvent('contextmenu',{bubbles:true,cancelable:true}))");
        var target = await view.CallAsync("contextTarget");
        Check(target.GetProperty("resultId").GetString() == second && target.GetProperty("helpContextId").GetInt32() == 1002 && target.GetProperty("editable").GetBoolean() && !target.GetProperty("hasSelection").GetBoolean(), "a right-click records the result under the pointer, its help topic and that nothing is selected: " + target.GetRawText());
        await view.CallAsync("selectResult", new { id = second });
        Check(await True($"(()=>{{const s=getSelection(),e=document.querySelectorAll('.report-entry');if(!s.rangeCount||s.isCollapsed)return false;const r=s.getRangeAt(0);return r.intersectsNode(e[1])&&!r.intersectsNode(e[0])}})()"), "Select result selects the result under the pointer");
        await Js("document.querySelectorAll('.report-entry')[1].dispatchEvent(new MouseEvent('contextmenu',{bubbles:true,cancelable:true}))");
        Check((await view.CallAsync("contextTarget")).GetProperty("hasSelection").GetBoolean(), "with the result selected, a right-click records that text is selected");
        await view.CallAsync("selectAll");
        Check(await True("(()=>{const s=getSelection(),e=document.querySelectorAll('.report-entry');if(!s.rangeCount||s.isCollapsed)return false;const r=s.getRangeAt(0);return r.intersectsNode(e[0])&&r.intersectsNode(e[1])})()"), "Select all selects every result");
    }
}
