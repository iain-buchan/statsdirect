internal static partial class Program
{
    // A result added to the report scrolls into view: its top just below the sticky toolbar, which is what the user wants to see next
    private static async Task CheckScrolling()
    {
        await view.CallAsync("load", new { entries = Array.Empty<object>() });
        await view.AppendAsync("<p>First result</p>", "First", "First", 0);
        Check(await True("window.scrollY===0"), "the first result of a short report leaves the window at the top");
        await view.AppendAsync("<div style=\"height:2400px\">A tall result</div>", "Tall", "Tall", 0);
        await view.AppendAsync("<p>The latest result</p>", "Latest", "Latest", 0);
        string state = (await Js("(()=>{const node=[...document.querySelectorAll('.report-entry')].pop(),bar=document.querySelector('.report-toolbar');return JSON.stringify({scrollY:window.scrollY,top:node.getBoundingClientRect().top,below:bar?bar.getBoundingClientRect().bottom:0,docHeight:document.documentElement.scrollHeight,viewHeight:window.innerHeight})})()")).GetString();
        Check(await True("(()=>{const node=[...document.querySelectorAll('.report-entry')].pop(),bar=document.querySelector('.report-toolbar'),top=node.getBoundingClientRect().top,below=bar?bar.getBoundingClientRect().bottom:0;return window.scrollY>0&&top>=below&&top<below+40})()"), "a result added after a tall one scrolls into view just below the toolbar: " + state);
        Check(await True("getComputedStyle(document.getElementById('results')).paddingBottom!=='0px'"), "the page is given room below the last result for that");
    }
}
