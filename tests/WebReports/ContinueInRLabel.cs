using StatsDirect.R;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static partial class Program
{
    // The R item of a reopened report's context menu names what the script written from each result's record will be: the recipe the
    // record resolves to, for a follow-on that of the analysis it continued, or only the data and settings when it resolves to none
    private static async Task CheckContinueInRLabel()
    {
        RRecipes.Folder = Path.Combine(AppContext.BaseDirectory, "R");
        JsonObject Record(string operation, string rootOperation = null, JsonObject parameters = null)
        {
            JsonObject record = NotebookRecord(operation);
            if (rootOperation != null) record["rootOperation"] = rootOperation;
            if (parameters != null) record["parameters"] = parameters;
            return record;
        }
        var results = new (string title, JsonObject record)[]
        {
            ("Paired t test", Record("TPaired")),
            ("Reliability of covariance components", Record("Cronbach", "PCRCovariance")),
            ("Reliability saved without its analysis", Record("Cronbach")),
            ("Probit plot saved without its model", Record("ProbitPlot")),
            ("Logit estimates saved with their model", Record("ProbitMore", parameters: new JsonObject { ["doseResponseModel"] = "logit", ["gamma"] = 0.95 })),
            ("Contracted frequencies", Record("ContractFrequencies")),
        };
        var entries = results.Select((result, i) => new { id = "label-" + i, title = result.title, operation = result.record["operation"]!.GetValue<string>(), helpContextId = 0,
            html = "<p>" + result.title + "</p>", record = (JsonElement?)JsonSerializer.SerializeToElement(result.record) }).ToList();
        entries.Add(new { id = "label-none", title = "Paired t test without a record", operation = "TPaired", helpContextId = 0, html = "<p>No record</p>", record = (JsonElement?)null });
        await view.CallAsync("load", new { entries });
        // the report saved and opened again: its results come back with new ids and the records kept in its metadata
        await view.CallAsync("load", new { entries = (await view.CallAsync("snapshot")).GetProperty("entries") });
        string[] labels = new string[entries.Count];
        for (int i = 0; i < entries.Count; i++)
        {
            await Js($"document.querySelectorAll('.report-entry')[{i}].dispatchEvent(new MouseEvent('contextmenu',{{bubbles:true,cancelable:true}}))");
            var target = await view.CallAsync("contextTarget");
            string id = target.GetProperty("resultId").GetString();
            string label = await view.ContinueInRTextAsync(id, target.GetProperty("hasRecord").GetBoolean());
            JsonElement record = (await view.RecordAsync(id)).GetProperty("record");
            bool recipe = record.ValueKind == JsonValueKind.Object && RScriptWriter.Write(record, entries[i].title, _ => throw new Exception("held data need no workbook")).HasRecipe;
            labels[i] = label.Replace("&", "");
            Check(label == (recipe ? "Continue in &R" : "Open data and settings in &R"), $"the reopened \"{entries[i].title}\" offers \"{labels[i]}\", as its script {(recipe ? "has" : "has no")} recipe");
        }
        Check(labels.SequenceEqual(new[] { "Continue in R", "Continue in R", "Open data and settings in R", "Open data and settings in R", "Continue in R", "Open data and settings in R", "Open data and settings in R" }),
            "follow-ons saved without the analysis they continued, operations without a recipe and results without a record offer only their data and settings: " + string.Join(" | ", labels));
        Check(await view.ContinueInRTextAsync("no-such-result", true) == "Open data and settings in &R",
            "a result whose record cannot be read still has its menu, with the data and settings item");
    }
}
