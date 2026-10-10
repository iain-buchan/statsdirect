internal static partial class Program
{
    // One run of an operation, one item of the report: the editor takes more of a result into the item appended for the run (a chart
    // after its analysis), and the window stays at the item's start
    private static async Task CheckSameRun()
    {
        await view.CallAsync("load", new { entries = Array.Empty<object>() });
        await view.AppendAsync("<div style=\"height:2000px\">An earlier result</div>", "Earlier", "Earlier", 0);
        string id = await view.AppendAsync("<p>The test's text</p>", "Paired t test", "TPaired", 1150);
        await view.ExtendAsync(id, "<p>The test's chart</p>");
        Check(await True("(()=>{const bodies=document.querySelectorAll('.report-body');return document.querySelectorAll('.report-entry').length===2&&bodies[1].textContent.includes(\"The test's text\")&&bodies[1].textContent.includes(\"The test's chart\")&&bodies[1].textContent.indexOf(\"text\")<bodies[1].textContent.indexOf(\"chart\")})()"), "a further output of the same run joins the item appended for the run, after its text");
        Check(await True($"(()=>{{const node=document.getElementById('result-{id}'),bar=document.querySelector('.report-toolbar'),top=node.getBoundingClientRect().top,below=bar?bar.getBoundingClientRect().bottom:0;return top>=below&&top<below+40}})()"), "the window stays at the start of the item");
    }
}
