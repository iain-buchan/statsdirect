internal static partial class Program
{
    // The body of the report holds nothing but the results: no header pane with a name and Help, no chart pane with a width and Fit;
    // the toolbar's picture tools appear while a picture is selected, and the handle for dragging a chart to another size stays
    private static async Task CheckPanesHidden()
    {
        await view.CallAsync("load", new { entries = Array.Empty<object>() });
        await Js("document.body.classList.remove('chart-preview')");   // the chart-options preview of an earlier check hides every control; this is the report window
        await view.AppendAsync("<p>A result</p><svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 200 100\"><rect width=\"200\" height=\"100\"/></svg>", "With a chart", "Chart", 1234);
        Check(await True("!document.querySelector('#results header, #results input, #results button:not(.report-chart-resize)')"), "the body of the report holds no headers, inputs or buttons of its own but the chart's resize handle");
        Check(await True("(()=>{const h=document.querySelector('.report-chart-resize');return !!h&&getComputedStyle(h).display!=='none'})()"), "the handle for dragging a chart to another size stays");
        Check(await True("(()=>{const t=document.querySelector('.report-picture-tools');return !!t&&(t.hidden||getComputedStyle(t).display==='none')})()"), "the toolbar's picture tools are hidden while no picture is selected");
        await ClickReport("#results svg");
        await Task.Delay(300);   // the tools follow the selection change, which the browser reports a tick later
        Check(await True("(()=>{const t=document.querySelector('.report-picture-tools');return !!t&&!t.hidden&&getComputedStyle(t).display!=='none'})()"), "the picture tools show while a picture is selected");
        await view.CallAsync("selectResult", new { id = (await Js("document.querySelector('.report-entry').id.slice(7)")).GetString() });
        Check(await True("(()=>{const s=getSelection();return s.rangeCount>0&&!s.isCollapsed&&s.getRangeAt(0).intersectsNode(document.querySelector('.report-entry'))})()"), "a result can be selected from the menu");
    }
}
