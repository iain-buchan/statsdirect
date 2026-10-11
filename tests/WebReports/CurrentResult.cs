using System.Text.Json;

internal static partial class Program
{
    // The result for the toolbar's R button, which has no result under a pointer to go by: the one the selection or focus is in; else the
    // latest that has a record of its run, as the Mac's R menu takes the latest; none in a report with neither.  An entry the cursor is in
    // that has no record is reported as such, so that the button can say so rather than open another result's script.
    private static async Task CheckCurrentResult()
    {
        var record = new { operation = "TPaired", inputs = new object[0] };
        await view.CallAsync("load", new { entries = new[] {
            new { title = "First", operation = "One", helpContextId = 1001, html = "<p>One</p>", record = (object)record },
            new { title = "Text", operation = "", helpContextId = 0, html = "<p>A note</p>", record = (object)null },
            new { title = "Third", operation = "Three", helpContextId = 1003, html = "<p>Three</p>", record = (object)record },
            new { title = "Fourth", operation = "Four", helpContextId = 1004, html = "<p>Four</p>", record = (object)null } } });
        string[] ids = (await Js("[...document.querySelectorAll('.report-entry')].map(e => e.dataset.resultId)")).EnumerateArray().Select(e => e.GetString()).ToArray();
        Check(ids.Length == 4, "the four entries are on the page");
        await Js("(() => { getSelection().removeAllRanges(); if (document.activeElement && document.activeElement.blur) document.activeElement.blur(); return true; })()");
        JsonElement target = await view.CallAsync("currentTarget");
        string Id() => target.GetProperty("resultId").ValueKind == JsonValueKind.String ? target.GetProperty("resultId").GetString() : null;
        string How() => target.GetProperty("how").GetString();
        bool HasRecord() => target.GetProperty("hasRecord").GetBoolean();
        Check(Id() == ids[2] && How() == "latest" && HasRecord() && target.GetProperty("title").GetString() == "Third" && target.GetProperty("operation").GetString() == "Three",
              "with the cursor in no result, the R button takes the latest result that has a record, not the last result");
        await view.CallAsync("selectResult", new { id = ids[0] });
        target = await view.CallAsync("currentTarget");
        Check(Id() == ids[0] && How() == "selection" && HasRecord() && target.GetProperty("operation").GetString() == "One", "with a result selected, the R button takes that result");
        await Js("(() => { const p = document.querySelectorAll('.report-entry')[1].querySelector('p'); const r = document.createRange(); r.setStart(p.firstChild, 2); r.collapse(true); const s = getSelection(); s.removeAllRanges(); s.addRange(r); return true; })()");
        target = await view.CallAsync("currentTarget");
        Check(Id() == ids[1] && How() == "selection" && !HasRecord() && target.GetProperty("title").GetString() == "Text",
              "with the cursor in a note that has no record, the R button is told of that entry and that it has no record");
        await view.CallAsync("load", new { entries = new[] { new { title = "Note", operation = "", helpContextId = 0, html = "<p>Only a note</p>" } } });
        await Js("(() => { getSelection().removeAllRanges(); if (document.activeElement && document.activeElement.blur) document.activeElement.blur(); return true; })()");
        target = await view.CallAsync("currentTarget");
        Check(Id() == null && How() == "none" && !HasRecord(), "a report with no recorded result gives the R button nothing");
    }
}
