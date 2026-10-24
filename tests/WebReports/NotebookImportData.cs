using StatsDirect.R;
using StatsDirect.Templates;
using StatsDirect.UI.WebReports;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static partial class Program
{
    private static void CheckNotebookImportData()
    {
        const string noRecipe = "NotebookImportWithoutRecipe";
        const string pixel = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=";
        JsonArray Values(params object?[] values) => JsonSerializer.SerializeToNode(values)!.AsArray();
        JsonObject Column(string name, params object?[] values) => new() { ["title"] = name, ["values"] = Values(values) };
        JsonObject Record(params JsonObject[] inputs) => new()
        {
            ["version"] = 1, ["operation"] = noRecipe, ["parameters"] = new JsonObject(),
            ["inputs"] = new JsonArray(inputs.Select(input => (JsonNode)input.DeepClone()).ToArray())
        };
        JsonObject Notebook(JsonObject record, Func<FramePointer, FrameValues>? reader = null, bool repeat = false)
        {
            var entries = new JsonArray();
            for (int index = 0; index < (repeat ? 2 : 1); index++)
                entries.Add(new JsonObject
                {
                    ["id"] = "import-result-" + index, ["title"] = "Saved analysis " + index, ["operation"] = noRecipe,
                    ["html"] = "<p>STALE SAVED ENTRY</p>", ["record"] = record.DeepClone(),
                    ["cells"] = new JsonArray(new JsonObject { ["source"] = "<p>Original current report</p>" })
                });
            var snapshot = new JsonObject
            {
                ["version"] = 1, ["revision"] = 0, ["reportHtml"] = "<html><body>STALE FULL REPORT</body></html>", ["entries"] = entries
            };
            return JsonNode.Parse(ReportNotebook.Write(JsonSerializer.SerializeToElement(snapshot), "Imported notebook title", reader!, "5.2.8-import-check"))!.AsObject();
        }
        ReportNotebookImport.ImportPlan Read(JsonObject notebook, string title = "Fallback notebook") => ReportNotebookImport.Read(notebook.ToJsonString(), title);
        JsonArray Cells(JsonObject notebook) => notebook["cells"]!.AsArray();
        JsonObject Provenance(JsonObject notebook) => notebook["metadata"]!["statsdirect"]!.AsObject();
        JsonObject Saved(JsonObject notebook, int index = 0) => Provenance(notebook)["entries"]![index]!.AsObject();
        JsonObject Dataset(JsonObject notebook, int index = 0) => Saved(notebook)["datasets"]![index]!.AsObject();
        void Refuses(JsonObject notebook, string description, string? detail = null)
        {
            string message = "";
            try { Read(notebook); }
            catch (InvalidOperationException ex) { message = ex.Message; }
            Check(message.Length > 0 && (detail == null || message.Contains(detail, StringComparison.OrdinalIgnoreCase)), description);
        }

        JsonObject typedInput = new()
        {
            ["name"] = "entered", ["kind"] = "entered", ["columns"] = new JsonArray(
                Column("Number", 1.25d, null, -3d), Column("Identifier", "0012", "β", null),
                Column("Flag", true, false, null), Column("Date", "2026-10-24T11:22:33.0000000Z", null, "2026-10-25T00:00:00.0000000Z"))
        };
        JsonObject notebook = Notebook(Record(typedInput));
        JsonObject report = Cells(notebook).OfType<JsonObject>().Single(cell => cell["metadata"]?["statsdirect"]?["role"]?.GetValue<string>() == "original StatsDirect report");
        report["source"] = new JsonArray("<p>EDITED CURRENT ", "REPORT β</p>");
        report["attachments"] = new JsonObject { ["plot.svg"] = new JsonObject
        {
            ["image/svg+xml"] = "<svg xmlns='http://www.w3.org/2000/svg'><title>Current plot</title></svg>", ["image/png"] = pixel
        } };
        JsonObject movedReport = report.DeepClone().AsObject();
        movedReport["id"] = "moved-current-report";
        movedReport["source"] = "<p>SECOND CURRENT REPORT MOVED FIRST</p>";
        movedReport.Remove("attachments");
        Cells(notebook).Insert(0, movedReport);
        JsonObject code = Cells(notebook).OfType<JsonObject>().Single(cell => cell["cell_type"]!.GetValue<string>() == "code");
        code["source"] = new JsonArray("answer <- 99\n", "print(answer)\n");
        code["execution_count"] = 7;
        code["outputs"] = new JsonArray(new JsonObject { ["output_type"] = "stream", ["name"] = "stdout", ["text"] = "OLD EXECUTED OUTPUT" });
        Cells(notebook).Add(new JsonObject { ["id"] = "new-note", ["cell_type"] = "markdown", ["metadata"] = new JsonObject(), ["source"] = "New notebook note **with Markdown**" });
        Cells(notebook).Add(new JsonObject { ["id"] = "raw-note", ["cell_type"] = "raw", ["metadata"] = new JsonObject(), ["source"] = new JsonArray("Raw notebook ", "note") });
        var plan = Read(notebook);
        string restoredSources = string.Join("\n", plan.Entries.EnumerateArray().SelectMany(entry => entry.GetProperty("cells").EnumerateArray()).Select(cell => cell.GetProperty("source").GetString()));
        Check(plan.Title == "Imported notebook title" && restoredSources.Contains("EDITED CURRENT REPORT β") &&
            !restoredSources.Contains("STALE SAVED ENTRY") && !restoredSources.Contains("STALE FULL REPORT"),
            "notebook import restores current report cell edits instead of stale saved HTML metadata");
        Check(restoredSources.IndexOf("SECOND CURRENT REPORT MOVED FIRST", StringComparison.Ordinal) < restoredSources.IndexOf("EDITED CURRENT REPORT β", StringComparison.Ordinal),
            "moved report cells are restored in the notebook's current order");
        Check(restoredSources.Contains("New notebook note **with Markdown**") && restoredSources.Contains("Raw notebook note"),
            "new untagged Markdown and raw cells are retained as imported report notes");
        Check(!restoredSources.Contains("Exported from StatsDirect") && !restoredSources.Contains("An R analysis recipe is unavailable")
            && plan.Entries.EnumerateArray().Count(entry => entry.GetProperty("title").GetString()!.StartsWith("Notebook note ", StringComparison.Ordinal)) == 2,
            "the exporter's own notes, written afresh at each export, are not re-imported as report sections, and only the user's notes are numbered");
        var thousand = new JsonArray();
        for (int index = 0; index < 1000; index++) thousand.Add(new JsonObject
        {
            ["id"] = "plain-" + index, ["title"] = "Note " + index, ["operation"] = "", ["html"] = "<p>Note " + index + "</p>", ["record"] = null,
            ["cells"] = new JsonArray(new JsonObject { ["source"] = "<p>Note " + index + "</p>" })
        });
        var thousandSnapshot = new JsonObject { ["version"] = 1, ["revision"] = 0, ["reportHtml"] = "<html><body>Thousand</body></html>", ["entries"] = thousand };
        JsonObject thousandNotebook = JsonNode.Parse(ReportNotebook.Write(JsonSerializer.SerializeToElement(thousandSnapshot), "Thousand", null!, "5.2.9-check"))!.AsObject();
        int thousandSections = 0; string thousandRefusal = "";
        try { thousandSections = Read(thousandNotebook).Entries.GetArrayLength(); } catch (InvalidOperationException ex) { thousandRefusal = ex.Message; }
        if (thousandRefusal.Length > 0) Console.WriteLine("      the notebook of 1,000 sections was refused: " + thousandRefusal);
        Check(thousandSections == 1000, "a report of 1,000 sections, as many as the export allows, opens again: the exporter's notes do not count against the section limit");
        Check(plan.Script.Contains("answer <- 99\nprint(answer)") && !plan.Script.Contains("OLD EXECUTED OUTPUT") && !plan.Script.Contains("Add your analysis here"),
            "import opens current code sources without evaluating them or substituting stale code and outputs");
        Check(restoredSources.Contains("OLD EXECUTED OUTPUT") &&
            (restoredSources.Contains("Saved notebook R output") || plan.Entries.GetRawText().Contains("Saved notebook R output")),
            "saved R output is retained as a clearly labelled report note without evaluating the code");
        Check(plan.Entries.EnumerateArray().SelectMany(entry => entry.GetProperty("cells").EnumerateArray()).Any(cell =>
            cell.TryGetProperty("attachments", out var attachments) && attachments.GetProperty("plot.svg").GetProperty("image/png").GetString() == pixel),
            "current report attachments survive as descriptors for the existing HTML sanitizer");
        Check(JsonNode.DeepEquals(JsonNode.Parse(plan.Provenance.GetRawText()), Provenance(notebook)),
            "the import plan retains original StatsDirect provenance without modifying its records");
        var typedColumns = plan.Tables.SelectMany(table => table.Columns).ToArray();
        Check(typedColumns.Single(column => column.Title == "Number").Values[0] is double number && number == 1.25 &&
            typedColumns.Single(column => column.Title == "Number").Values[1] == null &&
            typedColumns.Single(column => column.Title == "Identifier").Values[0] is string identifier && identifier == "0012" &&
            typedColumns.Single(column => column.Title == "Flag").Values[0] is bool flag && flag &&
            typedColumns.Single(column => column.Title == "Flag").Values[1] is bool noFlag && !noFlag &&
            typedColumns.Single(column => column.Title == "Date").Values[0] is string date && date == "2026-10-24T11:22:33.0000000Z",
            "new worksheet plans preserve numeric, text, flag, ISO date and missing input values exactly");

        JsonObject fallback = notebook.DeepClone().AsObject();
        Provenance(fallback).Remove("title");
        Check(Read(fallback, "Chosen filename").Title == "Chosen filename", "an imported notebook without a stored title uses its filename fallback");
        var repeated = Read(Notebook(Record(typedInput), repeat: true));
        Check(repeated.Tables.Length == 1 && repeated.Entries.EnumerateArray().Count(entry => entry.TryGetProperty("record", out var run) && run.ValueKind == JsonValueKind.Object) == 2,
            "identical datasets shared by two report analyses create one new worksheet while retaining both analyses");

        object[] originalValues = { 12d, null!, 34d };
        JsonObject pointer = new()
        {
            ["name"] = "source", ["kind"] = "frame", ["file"] = "Workbook-no-longer-exists.xlsx", ["path"] = @"C:\missing-notebook-source\Workbook-no-longer-exists.xlsx",
            ["sheet"] = "Missing source", ["mode"] = "NumericReplaceMissing", ["columns"] = new JsonArray(new JsonObject
            {
                ["title"] = "Original values", ["column"] = 4, ["rows"] = new JsonArray(2, 4), ["hasTitle"] = true, ["hash"] = RunRecord.Hash(originalValues)
            })
        };
        JsonObject external = Notebook(Record(pointer), _ =>
        {
            var values = new FrameValues();
            values.Columns.Add(new ColumnValues { Title = "Original values", Values = originalValues });
            return values;
        });
        var embedded = Read(external);
        JsonElement embeddedRecord = embedded.Entries.EnumerateArray().Single(entry => entry.TryGetProperty("record", out var record) && record.ValueKind == JsonValueKind.Object).GetProperty("record");
        Check(embeddedRecord.GetProperty("data").GetProperty("source").GetProperty("Original values")[2].GetDouble() == 34 &&
            !embedded.Provenance.GetProperty("entries")[0].GetProperty("record").TryGetProperty("data", out _),
            "import rehydrates replay data in a record copy without opening absent original workbooks or rewriting provenance");
        Check(embeddedRecord.GetProperty("inputs")[0].GetProperty("path").GetString() == pointer["path"]!.GetValue<string>() &&
            embeddedRecord.GetProperty("inputs")[0].GetProperty("columns")[0].GetProperty("hash").GetString() == RunRecord.Hash(originalValues),
            "rehydration retains original workbook pointers and verifies their actual-value hashes");
        JsonElement archival = embeddedRecord.GetProperty("notebookImport");
        Check(JsonElement.DeepEquals(archival.GetProperty("originalRecord"), embedded.Provenance.GetProperty("entries")[0].GetProperty("record")) &&
            archival.GetProperty("appVersion").GetString() == "5.2.8-import-check" &&
            archival.GetProperty("datasets")[0].GetProperty("columns")[0].GetProperty("verifiedAgainstRun").GetBoolean() &&
            !archival.GetProperty("datasets")[0].GetProperty("columns")[0].TryGetProperty("values", out _),
            "restored run records retain compact source and verification provenance through later report saves");
        bool refusedTwin = false;
        try { Notebook(Record(pointer, pointer), _ => { var values = new FrameValues(); values.Columns.Add(new ColumnValues { Title = "Original values", Values = originalValues }); return values; }); }
        catch (InvalidOperationException ex) when (ex.Message.Contains("ambiguous held-data key")) { refusedTwin = true; }
        Check(refusedTwin, "export refuses two workbook inputs under one held-data key up front, as the import does, rather than writing a notebook that cannot be opened");
        JsonObject deletedReports = notebook.DeepClone().AsObject();
        foreach (JsonNode current in Cells(deletedReports).ToArray())
            if (current?["metadata"]?["statsdirect"]?["role"]?.GetValue<string>() == "original StatsDirect report") Cells(deletedReports).Remove(current);
        Check(!Read(deletedReports).Entries.EnumerateArray().Any(entry => entry.GetProperty("record").ValueKind == JsonValueKind.Object),
            "removing all current report cells does not resurrect stale native report sections from metadata");

        JsonObject table = new()
        {
            ["name"] = "counts", ["kind"] = "table", ["counts"] = new JsonArray(Values(1d, 2d), Values(3d, 4d)),
            ["rowLabels"] = Values("Exposed", "Unexposed"), ["columnLabels"] = Values("Event", "No event"),
            ["columns"] = new JsonArray(Column("Event", 1d, 3d), Column("No event", 2d, 4d))
        };
        JsonObject nested = new()
        {
            ["name"] = "nested", ["kind"] = "frame2d", ["value"] = new JsonObject
            {
                ["frames"] = new JsonArray(new JsonArray(Column("Nested score", 11d, null, 13d)))
            }
        };
        JsonObject covariance = new()
        {
            ["name"] = "covariance", ["kind"] = "covariance", ["value"] = new JsonObject
            {
                ["frames"] = new JsonArray(new JsonArray(Column("Predictor", 0d, 1d), Column("Replicates 0", 10d, 11d), Column("Replicates 1", 20d, 21d)))
            }
        };
        JsonObject structuredNotebook = Notebook(Record(table, nested, covariance));
        var structured = Read(structuredNotebook);
        var structuredColumns = structured.Tables.SelectMany(sheet => sheet.Columns).ToArray();
        Check(structuredColumns.Any(column => column.Title == "Event" && column.Values.OfType<double>().Contains(3d)) &&
            structuredColumns.Any(column => column.Title == "Nested score" && column.Values.Length == 3 && column.Values[1] == null) &&
            structuredColumns.Any(column => column.Title == "Predictor" && column.Values.OfType<double>().Contains(1d)) &&
            structuredColumns.Any(column => column.Title == "Replicates 1" && column.Values.OfType<double>().Contains(21d)),
            "table counts, nested data and grouped covariance inputs become complete typed worksheet plans");

        JsonObject python = notebook.DeepClone().AsObject();
        python["metadata"]!["kernelspec"]!["name"] = "python3";
        python["metadata"]!["kernelspec"]!["language"] = "python";
        Refuses(python, "unsupported non-R notebook kernels are refused before any documents are created", "unsupported kernel");
        JsonObject generic = notebook.DeepClone().AsObject();
        generic["metadata"]!.AsObject().Remove("statsdirect");
        Refuses(generic, "general notebooks without StatsDirect handover metadata are refused clearly", "not a StatsDirect-exported notebook");
        JsonObject foreign = python.DeepClone().AsObject();
        foreign["metadata"]!.AsObject().Remove("statsdirect");
        Refuses(foreign, "a notebook of another kernel without StatsDirect metadata is refused as a general notebook, not for its kernel", "not a StatsDirect-exported notebook");
        JsonObject version = notebook.DeepClone().AsObject();
        Provenance(version)["version"] = 99;
        Refuses(version, "unsupported StatsDirect metadata versions are refused", "metadata version is unsupported");
        JsonObject format = notebook.DeepClone().AsObject();
        format["nbformat"] = 5;
        Refuses(format, "unsupported notebook major formats are refused", "nbformat 4");
        JsonObject minor = notebook.DeepClone().AsObject();
        minor["nbformat_minor"] = -1;
        Refuses(minor, "invalid notebook minor versions are refused", "minor version is unsupported");
        JsonObject later = notebook.DeepClone().AsObject();
        later["nbformat_minor"] = 7;
        bool accepted; try { accepted = ReportNotebookImport.Read(later.ToJsonString(), "later") != null; } catch (InvalidOperationException) { accepted = false; }
        Check(accepted, "a later minor version of nbformat 4, as a newer IDE may write, is read: minor versions are compatible by definition");
        JsonObject longText = notebook.DeepClone().AsObject();
        Dataset(longText)["columns"]![1]!["values"]![0] = new string('x', 32768);
        Refuses(longText, "worksheet text longer than Excel's cell limit is refused before a truncated worksheet is written", "32,767");
        JsonObject deep = notebook.DeepClone().AsObject();
        JsonNode nest = JsonValue.Create(1);
        for (int index = 0; index < 55; index++) nest = new JsonArray(nest);
        Saved(deep)["record"]!["parameters"]!["deep"] = nest;
        Refuses(deep, "a notebook nested deeper than the report browser can be sent is refused before any document is created", "malformed");
        JsonObject missingDatasets = notebook.DeepClone().AsObject();
        Saved(missingDatasets).Remove("datasets");
        Refuses(missingDatasets, "a recorded analysis without its complete datasets cannot be partially imported", "missing datasets");
        JsonObject unavailable = external.DeepClone().AsObject();
        Dataset(unavailable).Remove("columns");
        Dataset(unavailable)["Unavailable"] = "Original values unavailable";
        Refuses(unavailable, "unavailable embedded frame data are refused instead of resolving external workbook paths", "unavailable input data");
        JsonObject changed = external.DeepClone().AsObject();
        Dataset(changed)["columns"]![0]!["values"]![0] = 999;
        Refuses(changed, "embedded values that fail their original column hash cannot be rehydrated silently", "differ from the recorded analysis hash");
        JsonObject truncated = structuredNotebook.DeepClone().AsObject();
        Dataset(truncated, 1).Remove("frames");
        Dataset(truncated, 1)["omitted"] = true;
        Refuses(truncated, "omitted nested datasets are refused before opening a partial report", "omitted, truncated, unavailable or changed");
        JsonObject invalidSource = notebook.DeepClone().AsObject();
        Cells(invalidSource)[0]!["source"] = new JsonArray("Valid line", 42);
        Refuses(invalidSource, "cell source arrays containing non-text items are refused", "must contain only strings");
        JsonObject tooManyCells = notebook.DeepClone().AsObject();
        var manyCells = new JsonArray();
        for (int index = 0; index < 20001; index++) manyCells.Add(new JsonObject
        {
            ["id"] = "note-" + index, ["cell_type"] = "markdown", ["metadata"] = new JsonObject(), ["source"] = "note"
        });
        tooManyCells["cells"] = manyCells;
        Refuses(tooManyCells, "notebook cell limits are checked before any import side effects", "20,000 cells");
        JsonObject tooManySections = notebook.DeepClone().AsObject();
        var sectionCells = new JsonArray();
        for (int index = 0; index < 1001; index++) sectionCells.Add(new JsonObject
        {
            ["id"] = "section-" + index, ["cell_type"] = "markdown", ["metadata"] = new JsonObject(), ["source"] = "Notebook note " + index
        });
        tooManySections["cells"] = sectionCells;
        Refuses(tooManySections, "report-section limits are validated before either app host starts creating documents", "1,000 sections");
        object[] expansionValues = Enumerable.Range(0, 256).Select(index => (object)(index + new string('x', 1536))).ToArray();
        JsonObject expansionPointer = pointer.DeepClone().AsObject();
        expansionPointer["columns"]![0]!["rows"] = new JsonArray(2, expansionValues.Length + 1);
        expansionPointer["columns"]![0]!["hash"] = RunRecord.Hash(expansionValues);
        JsonObject expansion = Notebook(Record(expansionPointer), _ =>
        {
            var values = new FrameValues();
            values.Columns.Add(new ColumnValues { Title = "Original values", Values = expansionValues });
            return values;
        });
        JsonObject repeatedReport = Cells(expansion).OfType<JsonObject>().Single(cell => cell["metadata"]?["statsdirect"]?["role"]?.GetValue<string>() == "original StatsDirect report");
        var interleaved = new JsonArray();
        for (int index = 0; index < 100; index++)
        {
            var current = repeatedReport.DeepClone().AsObject();
            current["id"] = "expanded-report-" + index;
            interleaved.Add(current);
            interleaved.Add(new JsonObject { ["id"] = "expanded-note-" + index, ["cell_type"] = "markdown", ["metadata"] = new JsonObject(), ["source"] = "Interleaved note " + index });
        }
        expansion["cells"] = interleaved;
        Check(expansion.ToJsonString().Length < 2_000_000, "the repeated-section expansion regression uses a moderate source notebook");
        Refuses(expansion, "repeated discontiguous report groups are budgeted before cloning large restored records", "expanded report");
        var wideColumns = new JsonArray();
        for (int index = 0; index < 16385; index++) wideColumns.Add(Column("Column " + index));
        JsonObject tooWide = Notebook(Record(new JsonObject { ["name"] = "wide", ["kind"] = "entered", ["columns"] = wideColumns }));
        Refuses(tooWide, "worksheet plans exceeding Excel's column limit are refused before import", "16,384-column");
        var tooManyFrames = new JsonArray();
        for (int index = 0; index < 1001; index++) tooManyFrames.Add(new JsonArray(Column("Group " + index, (double)index)));
        JsonObject tooManySheets = Notebook(Record(new JsonObject
        {
            ["name"] = "many", ["kind"] = "frame2d", ["value"] = new JsonObject { ["frames"] = tooManyFrames }
        }));
        Refuses(tooManySheets, "more than 1000 distinct worksheet datasets cannot create a partial import", "1,000 worksheets");
        JsonObject nonfinite = notebook.DeepClone().AsObject();
        Dataset(nonfinite)["columns"]![0]!["values"]![0] = "NONFINITE_INPUT_SENTINEL";
        string nonfiniteJson = nonfinite.ToJsonString().Replace("\"NONFINITE_INPUT_SENTINEL\"", "1e400", StringComparison.Ordinal);
        bool rejectedNonfinite = false;
        try { ReportNotebookImport.Read(nonfiniteJson, "Nonfinite input"); }
        catch (InvalidOperationException) { rejectedNonfinite = true; }
        Check(rejectedNonfinite, "nonfinite numeric input values are refused before worksheets are created");
    }
}
