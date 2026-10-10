internal static partial class Program
{
    // The editor's side of the context menu: the target of a right-click is recorded for the menu that C# shows, and a result, or all
    // of them, can be selected from it as a click on a heading and Ctrl+A select them
    private static async Task CheckContextMenu()
    {
        // a report loaded gives its entries new ids: the second's is read from the page
        await view.CallAsync("load", new { entries = new[] { new { id = "first", title = "First", operation = "One", helpContextId = 1001, html = "<p>One</p>" }, new { id = "second", title = "Second", operation = "Two", helpContextId = 1002, html = "<p>Two</p>" } } });
        string second = (await Js("document.querySelectorAll('.report-entry')[1].id.slice(7)")).GetString();
        await Js("document.querySelectorAll('.report-body')[1].dispatchEvent(new MouseEvent('contextmenu',{bubbles:true,cancelable:true}))");
        var target = await view.CallAsync("contextTarget");
        Check(target.GetProperty("resultId").GetString() == second && target.GetProperty("helpContextId").GetInt32() == 1002 && target.GetProperty("editable").GetBoolean() && !target.GetProperty("hasSelection").GetBoolean(), "a right-click records the result under the pointer, its help topic and that nothing is selected: " + target.GetRawText());
        await view.CallAsync("selectResult", new { id = second });
        Check(await True($"document.querySelectorAll('.report-result-selected').length===1&&document.querySelector('.report-result-selected').id==='result-{second}'"), "Select result selects the result under the pointer");
        await Js("document.querySelectorAll('.report-body')[1].dispatchEvent(new MouseEvent('contextmenu',{bubbles:true,cancelable:true}))");
        Check((await view.CallAsync("contextTarget")).GetProperty("hasSelection").GetBoolean(), "with the result selected, a right-click records that text is selected");
        await view.CallAsync("selectAll");
        Check(await True("document.querySelectorAll('.report-result-selected').length===2"), "Select all selects every result");
    }
}
