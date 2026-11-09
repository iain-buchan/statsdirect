internal static partial class Program
{
    // One run of an operation, one item of the report: the editor takes more of a result into the item appended for the run (a chart
    // after its analysis), and the window stays at the item's start
    private static async Task CheckSameRun()
    {
        await view.CallAsync("load", new { entries = Array.Empty<object>() });
        string earlier = await view.AppendAsync("<div style=\"height:2000px\">An earlier result</div>", "Unpaired t test", "TUnpaired", 1244, focusResult: true);
        Check(view.HelpContext == 1244, "the first analysis sets native Help to its topic");
        string id = await view.AppendAsync("<p>The test's text</p>", "Paired t test", "TPaired", 1239, focusResult: true);
        Check(await True($"document.activeElement.id==='results'&&getSelection().isCollapsed&&document.getElementById('result-{id}').contains(getSelection().anchorNode)"),
            "a new analysis moves the insertion cursor from the earlier result into the new result");
        Check(view.HelpContext == 1239, "native Help uses the new result's topic as soon as the append completes");
        await view.ExtendAsync(id, "<p>The test's chart</p>");
        Check(await True("(()=>{const bodies=document.querySelectorAll('.report-entry');return document.querySelectorAll('.report-entry').length===2&&bodies[1].textContent.includes(\"The test's text\")&&bodies[1].textContent.includes(\"The test's chart\")&&bodies[1].textContent.indexOf(\"text\")<bodies[1].textContent.indexOf(\"chart\")})()"), "a further output of the same run joins the item appended for the run, after its text");
        Check(await True($"(()=>{{const node=document.getElementById('result-{id}'),bar=document.querySelector('.report-toolbar'),top=node.getBoundingClientRect().top,below=bar?bar.getBoundingClientRect().bottom:0;return top>=below&&top<below+40}})()"), "the window stays at the start of the item");

        int topic = -1;
        void RememberTopic(int value) => topic = value;
        view.TopicRequested += RememberTopic;
        try
        {
            const string help = "document.querySelector('[data-host-command=help]').click()";
            await Js(help);
            await view.CallAsync("snapshot");
            Check(topic == 1239, "Method help opens the latest analysis topic after its chart is appended");
            await Js($"getSelection().collapse(document.getElementById('result-{earlier}'),0)");
            await view.CallAsync("snapshot");
            await Js(help);
            await view.CallAsync("snapshot");
            Check(topic == 1244, "moving the cursor into an earlier result changes Method help to that analysis");
        }
        finally { view.TopicRequested -= RememberTopic; }
    }
}
