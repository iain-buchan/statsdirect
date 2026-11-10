using StatsDirect.R;
using StatsDirect.UI.WebReports;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static partial class Program
{
    // The report's context menu through the handler that shows it. A right-click that the browser itself delivers raises
    // CoreWebView2.ContextMenuRequested; ReportView.ShowContextMenu then asks the editor for the target, reads its record and builds
    // and shows the menu. The label checks call ContinueInRTextAsync themselves after a DOM event that raises nothing in the host, so
    // a failure of the handler around it went unseen, and an exception that leaves an async void handler ends the program.
    private static async Task CheckRealContextMenu()
    {
        RRecipes.Folder = Path.Combine(AppContext.BaseDirectory, "R");
        JsonObject unreadable = NotebookRecord("BoxWhiskerPlot");
        unreadable["parameters"] = new JsonArray(1);
        var cases = new (string title, JsonObject record, string label, string what)[]
        {
            ("Paired t test", NotebookRecord("TPaired"), "Continue in &R", "a result whose record resolves to a recipe"),
            ("Reliability saved without its analysis", NotebookRecord("Cronbach"), "Open data and settings in &R", "a follow-on saved without the analysis it continued, which has no recipe"),
            ("Box and whisker plot with settings that cannot be read", unreadable, "Open data and settings in &R", "a result whose record cannot be read (its parameters are not an object, and resolving its recipe throws)"),
        };
        var entries = cases.Select((c, i) => new { id = "menu-" + i, title = c.title, operation = c.record["operation"]!.GetValue<string>(), helpContextId = 0,
            html = "<p>" + c.title + "</p>", record = (JsonElement?)JsonSerializer.SerializeToElement(c.record) }).ToList();
        await view.CallAsync("load", new { entries });
        // the premise of each case, from the records as the editor holds them
        bool[] resolves = new bool[cases.Length], throws = new bool[cases.Length];
        string[] ids = new string[cases.Length];
        for (int i = 0; i < cases.Length; i++)
        {
            ids[i] = (await Js($"document.querySelectorAll('.report-entry')[{i}].dataset.resultId")).GetString();
            JsonElement record = (await view.RecordAsync(ids[i])).GetProperty("record");
            try { resolves[i] = RRecipes.Find(record) != null; }
            catch (InvalidOperationException) { throws[i] = true; }
        }
        Check(resolves.SequenceEqual(new[] { true, false, false }) && throws.SequenceEqual(new[] { false, false, true }),
            "of the three results to be right-clicked, the first resolves to a recipe, the follow-on to none, and resolving the third's throws");

        FieldInfo shown = typeof(ReportView).GetField("contextMenu", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new Exception("ReportView no longer keeps the menu it shows in its contextMenu field");
        List<string> notices = new(), asked = new();
        void Noticed(string message) => notices.Add(message);
        void Asked(string id) => asked.Add(id);
        view.Notice += Noticed;
        view.RRequested += Asked;
        try
        {
            for (int i = 0; i < cases.Length; i++)
            {
                notices.Clear();
                asked.Clear();
                var before = (ContextMenuStrip)shown.GetValue(view);
                JsonElement at = await Js($"(()=>{{const e=document.querySelectorAll('.report-entry')[{i}],p=e.querySelector('p');p.scrollIntoView({{block:'center'}});const r=p.getBoundingClientRect(),x=r.left+10,y=r.top+r.height/2;return {{x,y,inside:e.contains(document.elementFromPoint(x,y))}};}})()");
                if (!at.GetProperty("inside").GetBoolean()) throw new Exception("the point to right-click is not in the result: " + at);
                await Task.Delay(75);
                foreach (string type in new[] { "mousePressed", "mouseReleased" })
                    await view.Browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent", JsonSerializer.Serialize(new { type, x = at.GetProperty("x").GetDouble(),
                        y = at.GetProperty("y").GetDouble(), button = "right", buttons = type == "mousePressed" ? 2 : 0, clickCount = 1 }));
                // the handler replaces the menu of the last right-click and shows the new one; a failure inside it is given as a notice
                ContextMenuStrip menu = null;
                for (DateTime until = DateTime.UtcNow.AddSeconds(10); menu == null && notices.Count == 0 && DateTime.UtcNow < until; )
                {
                    await Task.Delay(20);
                    if (shown.GetValue(view) is ContextMenuStrip now && now != before && now.Visible) menu = now;
                }
                var target = await view.CallAsync("contextTarget");
                ToolStripMenuItem item = menu?.Items.OfType<ToolStripMenuItem>().FirstOrDefault(m => m.Text.EndsWith("in &R", StringComparison.Ordinal));
                string seen = menu == null ? "no menu was shown" + (notices.Count > 0 ? ", with the notice \"" + string.Join(" | ", notices) + "\"" : "")
                    : item == null ? "the menu has no R item: " + string.Join(" | ", menu.Items.OfType<ToolStripMenuItem>().Select(m => m.Text))
                    : "\"" + item.Text.Replace("&", "") + "\"" + (item.Enabled ? "" : ", disabled");
                bool enabled = item?.Enabled == true;
                menu?.Close();
                // the item's click is handled after the menu has closed, as it is when the user chooses it
                item?.PerformClick();
                Check(menu != null && notices.Count == 0 && target.GetProperty("resultId").GetString() == ids[i] && item?.Text == cases[i].label && enabled && asked.SequenceEqual(new[] { ids[i] }),
                    $"a real right-click on {cases[i].what} shows the report's menu with \"{cases[i].label.Replace("&", "")}\", which asks for that result in R: {seen}");
            }
        }
        finally
        {
            view.Notice -= Noticed;
            view.RRequested -= Asked;
        }
    }
}
