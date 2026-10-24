using StatsDirect.R;
using StatsDirect.UI.WebReports;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static partial class Program
{
    /// <summary>The notebook as nbformat writes it: every object's keys sorted, and cell sources and output texts split into lines.</summary>
    private static JsonNode AsJupyterSaves(JsonNode notebook)
    {
        static JsonNode Lines(JsonNode text)
        {
            if (text is not JsonValue value || !value.TryGetValue(out string s)) return text;
            string[] parts = s.Split('\n');
            var lines = parts.Take(parts.Length - 1).Select(p => (JsonNode)(p + "\n")).ToList();
            if (parts[^1].Length > 0) lines.Add(parts[^1]);
            return new JsonArray(lines.ToArray());
        }
        static JsonNode Sorted(JsonNode node) => node switch
        {
            JsonObject o => new JsonObject(o.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => KeyValuePair.Create(p.Key, Sorted(p.Value)))),
            JsonArray a => new JsonArray(a.Select(Sorted).ToArray()),
            null => null,
            _ => node.DeepClone(),
        };
        var copy = notebook.AsObject();
        foreach (var cell in copy["cells"]!.AsArray())
        {
            cell!["source"] = Lines(cell["source"]);
            foreach (var output in cell["outputs"]?.AsArray() ?? new JsonArray())
            {
                if (output!["text"] != null) output["text"] = Lines(output["text"]);
                foreach (var mime in output["data"]?.AsObject().Where(d => d.Key.StartsWith("text/", StringComparison.Ordinal)).Select(d => d.Key).ToList() ?? new List<string>()) output["data"]![mime] = Lines(output["data"]![mime]);
            }
        }
        return Sorted(copy);
    }

    private static async Task CheckNotebookImport()
    {
        CheckNotebookImportData();
        var notebook = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(output, "review.ipynb")))!.AsObject();
        // A notebook saved by Jupyter tooling comes back with its object keys sorted and its text split into lines, as nbformat's writer
        // does both: the import must read it as the original, held data included (it compared the held data by position until 24 Oct 2026).
        {
            var pristine = ReportNotebookImport.Read(notebook.ToJsonString(), "Review notebook");
            ReportNotebookImport.ImportPlan resaved = null; string refusal = null;
            try { resaved = ReportNotebookImport.Read(AsJupyterSaves(notebook.DeepClone()).ToJsonString(), "Review notebook"); }
            catch (InvalidOperationException ex) { refusal = ex.Message; }
            if (refusal != null) Console.WriteLine("      the re-saved notebook was refused: " + refusal);
            Check(resaved != null && resaved.Script == pristine.Script && resaved.Tables.Length == pristine.Tables.Length && pristine.Tables.Length > 0
                && resaved.Tables.Zip(pristine.Tables).All(t => t.First.Title == t.Second.Title && t.First.Columns.Length == t.Second.Columns.Length
                    && t.First.Columns.Zip(t.Second.Columns).All(c => c.First.Title == c.Second.Title && c.First.Values.SequenceEqual(c.Second.Values))),
                "a notebook re-saved by Jupyter tooling, keys sorted and text split into lines, imports as the original with its held data");
        }
        var cells = notebook["cells"]!.AsArray();
        var reportCell = cells.First(c => c!["metadata"]?["statsdirect"]?["role"]?.GetValue<string>() == "original StatsDirect report")!;
        reportCell["source"] = reportCell["source"]!.GetValue<string>().Replace("Reviewer’s note: café", "Colleague’s revised interpretation: café");
        var code = cells.First(c => c!["cell_type"]!.GetValue<string>() == "code")!;
        code["source"] = "# Colleague's current R edits\n" + code["source"]!.GetValue<string>();
        cells.Add(new JsonObject { ["id"] = "colleague-advice", ["cell_type"] = "markdown", ["metadata"] = new JsonObject(),
            ["source"] = new JsonArray("## Colleague’s advice\n\n", "Keep **all nine pairs**. <script>window.notebookExecuted=true</script><img src='https://example.invalid/external.png'><iframe src='file:///C:/private'></iframe>") });
        string Svg(string colour) => "<svg xmlns='http://www.w3.org/2000/svg' width='20' height='20'><defs><clipPath id='same'><rect width='20' height='20'/></clipPath></defs><rect width='20' height='20' fill='" + colour + "' clip-path='url(#same)' onload='window.notebookExecuted=true'/></svg>";
        foreach (string colour in new[] { "red", "blue" })
            cells.Add(new JsonObject { ["id"] = "attachment-" + colour, ["cell_type"] = "markdown", ["metadata"] = new JsonObject(),
                ["source"] = "![" + colour + "](attachment:shared.svg)", ["attachments"] = new JsonObject { ["shared.svg"] = new JsonObject { ["image/svg+xml"] = Svg(colour) } } });
        const string gif = "R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7";
        string jpeg = (await Js("(()=>{const c=document.createElement('canvas');c.width=c.height=2;c.getContext('2d').fillRect(0,0,2,2);return c.toDataURL('image/jpeg').split(',')[1];})()")).GetString()!;
        foreach (var image in new[] { (Mime: "image/gif", Data: gif, Name: "gif"), (Mime: "image/jpeg", Data: jpeg, Name: "jpeg") })
            cells.Add(new JsonObject { ["id"] = "attachment-" + image.Name, ["cell_type"] = "markdown", ["metadata"] = new JsonObject(),
                ["source"] = "![" + image.Name + "](attachment:raster)", ["attachments"] = new JsonObject { ["raster"] = new JsonObject { [image.Mime] = image.Data } } });
        // The notebook has complete captured data even though its historical workbook is no longer present.
        notebook["metadata"]!["statsdirect"]!["entries"]![0]!["record"]!.AsObject().Remove("data");
        var plan = ReportNotebookImport.Read(notebook.ToJsonString(), "Review notebook");
        Check(plan.Script.Contains("Colleague's current R edits") && plan.Tables.Length > 0, "notebook import retains current R code and recovers embedded datasets without a source workbook");
        Check(plan.Tables.Any(t => t.Columns.Any(c => c.Values.Any(v => Equals(v, 312d)))), "notebook data recovery keeps actual analysis values");
        await view.OpenNotebookAsync(plan.Entries);
        string html = (await Js("document.getElementById('results').innerHTML")).GetString()!;
        Check(html.Contains("Colleague’s revised interpretation") && html.Contains("Colleague’s advice") && html.Contains("<strong") && !html.Contains("Reviewer’s note: café"),
            "opened report uses current notebook cells and added Markdown rather than stale export metadata");
        Check(!html.Contains("<script", StringComparison.OrdinalIgnoreCase) && !html.Contains("<iframe", StringComparison.OrdinalIgnoreCase)
            && !html.Contains("onload=", StringComparison.OrdinalIgnoreCase) && !html.Contains("src=\"https://", StringComparison.OrdinalIgnoreCase)
            && await True("window.notebookExecuted!==true"), "notebook HTML and SVG pass the report sanitizer and external images are not loaded");
        Check(await True("(()=>{const svgs=[...document.querySelectorAll('#results svg')];return svgs.some(s=>s.innerHTML.includes('red'))&&svgs.some(s=>s.innerHTML.includes('blue'))&&new Set([...document.querySelectorAll('#results svg [id]')].map(n=>n.id)).size===document.querySelectorAll('#results svg [id]').length;})()"),
            "cell-local attachment names and repeated SVG identifiers retain independent charts");
        Check(html.Contains("<ol") && html.Contains("**literal**") && html.Contains("<table"), "notebook reopening preserves numbered lists, literal report text and tables");
        Check(await True("[...document.querySelectorAll('#results img')].some(i=>i.src.startsWith('data:image/gif;'))&&[...document.querySelectorAll('#results img')].some(i=>i.src.startsWith('data:image/jpeg;'))"),
            "edited notebook GIF and JPEG attachments reopen as readable embedded pictures");
        JsonElement state = await view.CallAsync("snapshot");
        var recorded = state.GetProperty("entries").EnumerateArray().First(e => e.TryGetProperty("record", out _));
        var replay = RScriptWriter.Write(recorded.GetProperty("record"), "Restored paired test", _ => throw new Exception("Restored inputs must be held in the imported record"));
        Check(!replay.DataUnavailable && replay.Text.Contains("312") && replay.Text.Contains("t.test"), "Continue in R can use restored recorded data without accessing the old workbook");
        Check(recorded.GetProperty("record").TryGetProperty("notebookImport", out _), "saved report records retain the notebook's original-record provenance");
        string reopened = Path.Combine(output, "notebook-imported-report.html");
        await view.SaveAsync(reopened, plan.Title, "html");
        await view.OpenHtmlAsync(await File.ReadAllTextAsync(reopened), plan.Title);
        Check((await Js("document.getElementById('results').innerHTML")).GetString()!.Contains("Colleague’s revised interpretation"), "the restored editable HTML report saves and reopens its current notebook narrative");
        var before = (await Js("document.getElementById('results').innerHTML")).GetString();
        var bad = JsonNode.Parse(plan.Entries.GetRawText())!.AsArray();
        bad.Add(new JsonObject { ["id"] = "broken", ["title"] = "Broken image", ["cells"] = new JsonArray(new JsonObject
        { ["source"] = "![missing](attachment:missing.png)", ["cellType"] = "markdown", ["attachments"] = new JsonObject() }) });
        bool refused = false;
        try { await view.OpenNotebookAsync(JsonSerializer.SerializeToElement(bad)); } catch (InvalidOperationException) { refused = true; }
        Check(refused && before == (await Js("document.getElementById('results').innerHTML")).GetString(), "a broken later notebook section cannot partially replace an existing report");
        await File.WriteAllTextAsync(Path.Combine(output, "edited-review.ipynb"), notebook.ToJsonString(), new UTF8Encoding(false));
    }
}
