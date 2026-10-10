internal static partial class Program
{
    // The panes in the body of the report are not shown: a result's header with its Help, and a chart's width and Fit
    private static async Task CheckPanesHidden()
    {
        await view.CallAsync("load", new { entries = Array.Empty<object>() });
        await view.AppendAsync("<p>A result</p><svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 200 100\"><rect width=\"200\" height=\"100\"/></svg>", "With a chart", "Chart", 1234);
        Check(await True("(()=>{const r=document.querySelector('.report-entry > header').getBoundingClientRect();return r.width<=1&&r.height<=1})()"), "a result's header pane, its name and Help, is out of view");
        Check(await True("[...document.querySelectorAll('.report-media > .report-controls, .report-chart > .report-controls')].every(c=>getComputedStyle(c).display==='none')"), "a chart's width and Fit pane is not shown");
        await view.CallAsync("selectResult", new { id = (await Js("document.querySelector('.report-entry').id.slice(7)")).GetString() });
        Check(await True("document.querySelectorAll('.report-result-selected').length===1"), "a result can still be selected through the hidden header");
    }
}
