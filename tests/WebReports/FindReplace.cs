using System.Text.Json;

internal static partial class Program
{
    // Find on the toolbar for a report: a bar at the top of the window, as the script window's, whose searches the view answers: the next
    // match after the selection and round to the start, the selection replaced when it is the term, and every match replaced
    private static async Task CheckFindReplace()
    {
        await Js("StatsDirectReportEditor.setEditing(true)");
        await view.AppendAsync("<p>zebra ostrich Zebra quokka</p>", "Find and replace", "", 0);
        async Task<string> Selected() => (await Js("getSelection().toString()")).GetString();
        Check(await view.FindNextAsync("zebra", false) && await Selected() == "zebra", "Find selects the first match");
        Check(await view.FindNextAsync("zebra", false) && await Selected() == "Zebra", "Find next goes on to the next match, whatever its case");
        Check(await view.FindNextAsync("zebra", false) && await Selected() == "zebra", "Find next goes round to the start");
        Check(!await view.FindNextAsync("wombat", false), "a term the report does not hold is not found");
        Check(await view.FindNextAsync("Zebra", true) && await Selected() == "Zebra", "matching case finds only the match of that case");
        Check(await view.ReplaceSelectionAsync("Zebra", "emu", true) && await True("document.body.innerText.includes('ostrich emu quokka')"), "Replace replaces the selected match");
        Check(await view.ReplaceAllAsync("zebra", "wombat", false) == 1 && await True("document.body.innerText.includes('wombat ostrich emu quokka')"), "Replace all replaces every match");
    }
}
