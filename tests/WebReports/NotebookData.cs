using StatsDirect.R;
using StatsDirect.Templates;
using StatsDirect.UI.WebReports;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static partial class Program
{
    private static void CheckNotebookMoreData(JsonElement snapshot)
    {
        const string noRecipe = "NotebookDataWithoutAnRRecipe";
        JsonObject Document(params JsonObject[] records)
        {
            var document = JsonNode.Parse(snapshot.GetRawText())!.AsObject();
            var entries = new JsonArray();
            for (int i = 0; i < records.Length; i++)
            {
                var entry = document["entries"]![0]!.DeepClone().AsObject();
                entry["id"] = "notebook-data-" + i;
                entry["title"] = "Data analysis " + (i + 1);
                entry["operation"] = records[i]["operation"]!.DeepClone();
                entry["record"] = records[i].DeepClone();
                entries.Add(entry);
            }
            document["entries"] = entries;
            return document;
        }
        JsonElement Export(JsonObject document, Func<FramePointer, FrameValues>? reader = null) => JsonDocument.Parse(
            ReportNotebook.Write(JsonSerializer.SerializeToElement(document), "Complete input handover", reader!, "5.2.8-data-check")).RootElement.Clone();
        JsonElement Saved(JsonElement notebook, int entry = 0) => notebook.GetProperty("metadata").GetProperty("statsdirect").GetProperty("entries")[entry];
        JsonElement Datasets(JsonElement notebook, int entry = 0) => Saved(notebook, entry).GetProperty("datasets");
        string[] Code(JsonElement notebook) => notebook.GetProperty("cells").EnumerateArray().Where(cell => cell.GetProperty("cell_type").GetString() == "code")
            .Select(cell => cell.GetProperty("source").GetString()!).ToArray();
        JsonArray Values(params object?[] values) => JsonSerializer.SerializeToNode(values)!.AsArray();
        JsonObject Column(string title, params object?[] values) => new() { ["title"] = title, ["values"] = Values(values) };
        JsonObject Record(params JsonObject[] inputs)
        {
            var record = NotebookRecord(noRecipe);
            record.Remove("data");
            record["inputs"] = new JsonArray(inputs.Select(input => (JsonNode)input.DeepClone()).ToArray());
            return record;
        }
        JsonObject Pointer(string name, string workbook, params object?[] values) => new()
        {
            ["name"] = name, ["kind"] = "frame", ["file"] = workbook, ["path"] = @"C:\notebook-data\" + workbook,
            ["sheet"] = "Original inputs", ["mode"] = "NumericReplaceMissing",
            ["columns"] = new JsonArray(new JsonObject
            {
                ["title"] = "Measure", ["column"] = 2, ["rows"] = new JsonArray(2, values.Length + 1),
                ["hasTitle"] = true, ["hash"] = RunRecord.Hash(values!)
            })
        };
        FrameValues ReadColumn(object?[] data, bool moved = false)
        {
            var values = new FrameValues();
            values.Columns.Add(new ColumnValues { Title = "Measure", Values = data!, Moved = moved });
            return values;
        }
        void Refuses(JsonObject document, Func<FramePointer, FrameValues>? reader, string label, string? explanation = null)
        {
            string message = "";
            try { Export(document, reader); }
            catch (InvalidOperationException ex) { message = ex.Message; }
            Check(message.StartsWith("Cannot export", StringComparison.Ordinal) && (explanation == null || message.Contains(explanation, StringComparison.OrdinalIgnoreCase)), label);
        }

        // These are distinct recorded analyses and workbooks; the first pointer is used twice, including a moved column.
        object?[] firstData = { 1d, 2d, null, 4d }, secondData = { 101d, 202d, 303d, 404d };
        JsonObject first = Record(Pointer("observations", "First.xlsx", firstData));
        JsonObject second = Record(Pointer("observations", "Second.xlsx", secondData));
        second["parameters"]!["gamma"] = 0.8;
        var reads = new Dictionary<string, int>(StringComparer.Ordinal);
        JsonElement multiple = Export(Document(first, second, first), pointer =>
        {
            reads[pointer.File] = reads.GetValueOrDefault(pointer.File) + 1;
            if (pointer.File == "First.xlsx")
            {
                pointer.Columns[0].Column = 5;
                return ReadColumn(firstData, true);
            }
            return ReadColumn(secondData);
        });
        Check(reads.Count == 2 && reads["First.xlsx"] == 1 && reads["Second.xlsx"] == 1,
            "multiple notebook analyses resolve distinct workbooks and read a reused pointer only once");
        Check(Datasets(multiple, 0)[0].GetProperty("columns")[0].GetProperty("values")[0].GetDouble() == 1 &&
            Datasets(multiple, 1)[0].GetProperty("columns")[0].GetProperty("values")[0].GetDouble() == 101,
            "each notebook analysis retains its own workbook values without cross-analysis substitution");
        string[] multipleCode = Code(multiple);
        Check(multipleCode.Length == 3 && multipleCode.All(code => code.Contains("\nlocal({\n") && code.EndsWith("\n})\n", StringComparison.Ordinal)),
            "each exported analysis runs in its own local R scope so earlier analysis variables cannot leak forward");
        Check(Datasets(multiple)[0].GetProperty("columns")[0].GetProperty("moved").GetBoolean() &&
            Datasets(multiple)[0].GetProperty("columns")[0].GetProperty("verifiedAgainstRun").GetBoolean() && multipleCode[0].Contains("column 5") &&
            Datasets(multiple)[0].GetProperty("resolvedPointer").GetProperty("Columns")[0].GetProperty("Column").GetInt32() == 5,
            "moved columns preserve their resolved location and are verified against the original actual-value hash");
        Check(!Saved(multiple).GetProperty("record").TryGetProperty("data", out _) &&
            Saved(multiple).GetProperty("record").GetProperty("inputs")[0].GetProperty("columns")[0].GetProperty("column").GetInt32() == 2 &&
            JsonNode.DeepEquals(JsonNode.Parse(Saved(multiple).GetProperty("record").GetRawText()), first),
            "rereading and relocating data does not rewrite the original run record or pretend its values were held");

        int editedReads = 0;
        JsonElement heldOriginal = Export(Document(NotebookRecord()), _ =>
        {
            editedReads++;
            return new FrameValues { Unavailable = "source workbook was edited or deleted" };
        });
        Check(editedReads == 0 && Datasets(heldOriginal)[0].GetProperty("columns")[0].GetProperty("values")[0].GetDouble() == 312,
            "held original data are preferred even after their source workbook is changed or unavailable");

        JsonObject repeated = NotebookRecord();
        JsonObject extraFirst = Pointer("extra", "Unused.xlsx", 7123d);
        extraFirst["inherited"] = true;
        JsonObject extraSecond = Pointer("extra", "Unused.xlsx", 8456d);
        extraSecond["inherited"] = true;
        extraSecond["held"] = "extra#2";
        repeated["inputs"]!.AsArray().Add(extraFirst);
        repeated["inputs"]!.AsArray().Add(extraSecond);
        repeated["data"]!["extra"] = new JsonObject { ["Measure"] = Values(7123d) };
        repeated["data"]!["extra#2"] = new JsonObject { ["Measure"] = Values(8456d) };
        JsonElement unused = Export(Document(repeated), _ => throw new Exception("A held repeated input must not be reread"));
        Check(Datasets(unused).GetArrayLength() == 3 &&
            Datasets(unused)[1].GetProperty("columns")[0].GetProperty("values")[0].GetDouble() == 7123 &&
            Datasets(unused)[2].GetProperty("columns")[0].GetProperty("values")[0].GetDouble() == 8456,
            "complete notebook metadata retains both repeated held inputs, including data unused by the chosen recipe");
        Check(!Code(unused)[0].Contains("7123") && !Code(unused)[0].Contains("8456"),
            "the paired recipe can omit inherited extras without dropping them from complete notebook data");

        foreach (int count in new[] { RunRecord.EmbeddingThreshold, RunRecord.EmbeddingThreshold + 1 })
        {
            object?[] data = Enumerable.Range(0, count).Select(index => (object?)(index + 0.125)).ToArray();
            JsonObject record = Record(Pointer("large", "Boundary.xlsx", data));
            if (count == RunRecord.EmbeddingThreshold) record["data"] = new JsonObject { ["large"] = new JsonObject { ["Measure"] = Values(data) } };
            int boundaryReads = 0;
            JsonElement boundary = Export(Document(record), _ => { boundaryReads++; return ReadColumn(data); });
            JsonElement retained = Datasets(boundary)[0].GetProperty("columns")[0].GetProperty("values");
            Check(retained.GetArrayLength() == count && retained[count - 1].GetDouble() == count - 1 + 0.125 &&
                Code(boundary)[0].Contains((count - 1 + 0.125).ToString("R", CultureInfo.InvariantCulture)) && boundaryReads == (count > RunRecord.EmbeddingThreshold ? 1 : 0),
                "all " + count + " values survive the report embedding boundary in metadata and standalone replay code");
        }

        JsonObject table = new()
        {
            ["name"] = "counts", ["kind"] = "table", ["counts"] = new JsonArray(Values(1d, 2d), Values(3d, 4d)),
            ["rowLabels"] = Values("Exposed", "Unexposed"), ["columnLabels"] = Values("Event", "No event"),
            ["columns"] = new JsonArray(Column("Event", 1d, 3d), Column("No event", 2d, 4d))
        };
        JsonObject nested = new()
        {
            ["name"] = "blocks", ["kind"] = "frame2d", ["value"] = new JsonObject
            {
                ["frames"] = new JsonArray(new JsonArray(Column("Within block", 11d, null, 13d)), new JsonArray())
            }
        };
        JsonObject covariance = new()
        {
            ["name"] = "groups", ["kind"] = "covariance", ["value"] = new JsonObject
            {
                ["frames"] = new JsonArray(new JsonArray(Column("Predictor", 0d, 1d), Column("", 10d, 11d), Column("", 20d, 21d)))
            }
        };
        JsonElement structured = Export(Document(Record(table, nested, covariance)));
        Check(Datasets(structured)[0].GetProperty("counts")[1][1].GetDouble() == 4 &&
            Datasets(structured)[0].GetProperty("originalInput").GetProperty("rowLabels")[0].GetString() == "Exposed" &&
            Code(structured)[0].Contains("matrix(c(1, 2, 3, 4)"),
            "entered table counts and labels remain complete and generate their original matrix replay");
        Check(Datasets(structured)[1].GetProperty("frames")[0][0].GetProperty("values")[1].ValueKind == JsonValueKind.Null &&
            Datasets(structured)[1].GetProperty("frames")[1].GetArrayLength() == 0 && Code(structured)[0].Contains("blocks <- list("),
            "nested frame data preserve missing values and explicitly empty combinations");
        Check(Datasets(structured)[2].GetProperty("frames")[0][2].GetProperty("values")[1].GetDouble() == 21 &&
            Code(structured)[0].Contains("groups <- list("), "grouped covariance data preserve predictor levels and every outcome replicate");

        const string recordedDate = "2026-10-24T11:22:33.0000000Z";
        JsonObject typed = Record(new JsonObject
        {
            ["name"] = "typed", ["kind"] = "entered", ["columns"] = new JsonArray(
                Column("Identifier", "0012", "A β", null), Column("Number", -1.25d, 0d, null),
                Column("Flag", true, false, null), Column("Date", recordedDate, null, recordedDate))
        }, new JsonObject { ["name"] = "enabled", ["kind"] = "boolean", ["value"] = true });
        typed["parameters"]!["label"] = "0012";
        typed["parameters"]!["enabled"] = true;
        JsonElement typedNotebook = Export(Document(typed));
        JsonElement typedColumns = Datasets(typedNotebook)[0].GetProperty("columns");
        Check(typedColumns[0].GetProperty("values")[0].GetString() == "0012" &&
            typedColumns[1].GetProperty("values")[0].ValueKind == JsonValueKind.Number &&
            typedColumns[2].GetProperty("values")[0].ValueKind == JsonValueKind.True &&
            typedColumns[2].GetProperty("values")[1].ValueKind == JsonValueKind.False &&
            typedColumns[2].GetProperty("values")[2].ValueKind == JsonValueKind.Null &&
            typedColumns[3].GetProperty("values")[0].GetString() == recordedDate && Datasets(typedNotebook)[1].GetProperty("value").GetBoolean(),
            "notebook input metadata preserves text identifiers, numbers, boolean flags, ISO dates and missing values without type guesses");
        Check(!Saved(typedNotebook).GetProperty("hasRecipe").GetBoolean() &&
            typedNotebook.GetProperty("cells").EnumerateArray().Any(cell => cell.GetProperty("cell_type").GetString() == "markdown" &&
                cell.GetProperty("source").GetString()!.Contains("An R analysis recipe is unavailable")) && Code(typedNotebook)[0].Contains("Add your analysis here"),
            "methods without recipes retain available data and settings with an explicit analysis-unavailable note");
        Check(typedNotebook.GetProperty("metadata").GetProperty("statsdirect").GetProperty("appVersion").GetString() == "5.2.8-data-check" &&
            !typedNotebook.GetProperty("metadata").GetProperty("language_info").TryGetProperty("version", out _),
            "notebook provenance records the supplied StatsDirect version without fabricating an unexecuted R version");
        DateTime acquiredDate = new(2026, 10, 24, 11, 22, 33, DateTimeKind.Utc);
        object?[] dateValues = { acquiredDate, null };
        JsonElement dateNotebook = Export(Document(Record(Pointer("dates", "Dates.xlsx", dateValues))), _ => ReadColumn(dateValues));
        Check(Datasets(dateNotebook)[0].GetProperty("columns")[0].GetProperty("values")[0].GetString() == acquiredDate.ToString("o", CultureInfo.InvariantCulture) &&
            Code(dateNotebook)[0].Contains(acquiredDate.ToString("o", CultureInfo.InvariantCulture)),
            "reread date values retain RunRecord ISO precision in both notebook data and R replay");

        JsonObject pointerOnly = Document(first);
        Refuses(pointerOnly, _ => new FrameValues { Unavailable = "original workbook absent" },
            "unavailable pointer data abort the whole export with an actionable recovery message", "unchanged original workbook");
        Refuses(pointerOnly, _ => ReadColumn(new object?[] { 999d, 2d, null, 4d }),
            "an actual hash mismatch is refused even if a reader incorrectly leaves its Changed flag false", "differs from");
        var markedChanged = ReadColumn(firstData);
        markedChanged.Columns[0].Changed = true;
        Refuses(pointerOnly, _ => markedChanged, "a reader's explicit changed-data warning also blocks export", "differs from");
        JsonObject missingInputs = NotebookRecord();
        missingInputs.Remove("inputs");
        Refuses(Document(missingInputs), null, "a missing inputs array is refused instead of exporting a paired recipe with NULL data", "inputs array");
        JsonObject missingParameters = NotebookRecord();
        missingParameters.Remove("parameters");
        Refuses(Document(missingParameters), null, "a missing parameters object is refused instead of exporting an unknown confidence level", "parameters object");
        JsonObject emptyContainers = Record();
        emptyContainers["parameters"] = new JsonObject();
        JsonElement emptyInputs = Export(Document(emptyContainers));
        Check(Datasets(emptyInputs).GetArrayLength() == 0 && Code(emptyInputs).Length == 1 &&
            Saved(emptyInputs).GetProperty("record").GetProperty("inputs").GetArrayLength() == 0 &&
            !Saved(emptyInputs).GetProperty("record").GetProperty("parameters").EnumerateObject().Any(),
            "explicitly empty input and settings containers remain valid for analyses that need no recorded data");
        JsonObject engineDocument = Document(emptyContainers);
        engineDocument["engineVersion"] = "5.2.8-engine-test";
        JsonElement engineMetadata = Export(engineDocument).GetProperty("metadata").GetProperty("statsdirect");
        Check(engineMetadata.GetProperty("engineVersion").GetString() == "5.2.8-engine-test" &&
            engineMetadata.GetProperty("appVersion").GetString() == "5.2.8-data-check",
            "a supplied engine version is retained separately from the actual exporting application version");
        engineDocument["engineVersion"] = 528;
        Refuses(engineDocument, null, "an optional engine version must be a string when supplied", "engineVersion");
        JsonObject missingColumn = NotebookRecord();
        missingColumn["data"]!["data"]!.AsObject().Remove("PEFR After");
        Refuses(Document(missingColumn), null, "incomplete held columns cannot produce an apparently complete replay", "incomplete held columns");
        JsonObject omittedInput = Record(new JsonObject { ["name"] = "blocks", ["kind"] = "frame2d", ["value"] = new JsonObject { ["omitted"] = true } });
        Refuses(Document(omittedInput), null, "omitted nested input data are refused", "omitted or truncated input");
        JsonObject truncatedInput = Record(new JsonObject
        {
            ["name"] = "groups", ["kind"] = "covariance", ["value"] = new JsonObject { ["frames"] = new JsonArray(), ["truncated"] = true }
        });
        Refuses(Document(truncatedInput), null, "explicitly truncated covariance input data are refused", "omitted or truncated input");
        JsonObject malformedNested = Record(new JsonObject
        {
            ["name"] = "blocks", ["kind"] = "frame2d", ["value"] = new JsonObject { ["frames"] = new JsonArray(new JsonArray(new JsonObject { ["title"] = "Lost values" })) }
        });
        Refuses(Document(malformedNested), null, "nested columns without their values are refused", "values array");
        JsonObject omittedSetting = NotebookRecord();
        omittedSetting["parameters"]!["nestedDefaults"] = new JsonObject { ["omitted"] = true };
        Refuses(Document(omittedSetting), null, "omission markers in input settings are refused", "omitted or truncated input");
        JsonObject incompleteOutput = NotebookRecord();
        incompleteOutput["values"] = new JsonObject
        {
            ["nativeResultMatrix"] = new JsonObject { ["omitted"] = true },
            ["nativeDiagnostics"] = new JsonObject { ["truncated"] = true }
        };
        JsonElement originalOutput = Export(Document(incompleteOutput));
        Check(Saved(originalOutput).GetProperty("record").GetProperty("values").GetProperty("nativeResultMatrix").GetProperty("omitted").GetBoolean() &&
            Code(originalOutput).Length == 1 && Datasets(originalOutput)[0].GetProperty("columns")[0].GetProperty("values").GetArrayLength() == 9,
            "abbreviated native output metadata does not block replay when every actual input is complete");
    }
}
