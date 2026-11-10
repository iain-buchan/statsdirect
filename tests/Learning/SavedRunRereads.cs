using StatsDirect.Data;
using StatsDirect.R;
using StatsDirect.TemplateProcessing;
using StatsDirect.Templates;
using StatsDirect.UI;
using StatsDirect.UI.WebReports;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static partial class Program
{
    // Saved runs whose values were too many to keep with the result, read back from the worksheet they point to as Continue in R and
    // the notebook export read them: the actual grid's ReadColumns through FrameReader. Each run is the template processor's, with
    // its frames acquired from the same worksheet by the production selection code, and R must give the native figures.
    static async Task TestSavedRunRereads(frmMain main)
    {
        CultureInfo inv = CultureInfo.InvariantCulture;
        double Number(ParameterBag bag, string key) => Convert.ToDouble(bag[key].AsObject, inv);
        string Vector(IEnumerable<double> values) => "c(" + string.Join(", ", values.Select(v => v.ToString("R", inv))) + ")";

        await WithGrid(main, "Mac padded record", async grid =>
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "MacRecords", "Q3a-padded-record.json"));
            Check(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).Equals(
                "2a2977183b7338a45495410fc7e36f91208d6c1525b29c2142a1b868031313ee", StringComparison.OrdinalIgnoreCase),
                "the Mac paired fixture retains its exact bytes");
            JsonElement record = JsonDocument.Parse(bytes).RootElement.Clone();
            RScript held = RScriptWriter.Write(record, null, _ => throw new InvalidOperationException("The record holds its own values."));
            Check(held.HasRecipe && !held.DataUnavailable && held.Text.Contains("after <- c(135, 136.4, 141.3, 139, 152.6, NA, NA, NA)"),
                "the Mac record's original eight rows are written before the paired recipe drops incomplete pairs");
            string assertion = "stopifnot(n == 5, length(after) == 5L, isTRUE(all.equal(as.numeric(r$statistic), 4.952164557433743, tolerance=1e-12)))\ncat('MAC_PADDED_OK\\n')";
            await RunConversationR(held.Text, "mac-padded-held", assertion, "MAC_PADDED_OK");
            Notebook(record, "Mac paired t test", "the original Mac record's notebook");

            var view = (SpreadsheetGear.Windows.Forms.WorkbookView)Field(grid, "workbookView");
            view.GetLock();
            try { view.ActiveWorksheet.Name = "Data"; }
            finally { view.ReleaseLock(); }
            Fill(grid, 0, "Before", new object[] { 142.5, 138.0, 150.2, 145.0, 160.75, 155.0, 149.1, 151.0 });
            Fill(grid, 1, "After", new object[] { 135.0, 136.4, 141.3, 139.0, 152.6 });
            JsonObject pointerOnly = JsonNode.Parse(bytes).AsObject();
            pointerOnly.Remove("data");
            pointerOnly["inputs"][0]["path"] = grid.Text;
            JsonElement localRecord = JsonSerializer.SerializeToElement(pointerOnly);
            FrameValues reread = FrameReader.Read(FramePointer.From(SavedInput(localRecord, "data")));
            Check(reread.Unavailable == null && reread.Columns.All(c => !c.Changed && c.Values.Length == 8) &&
                reread.Columns[1].Values.Skip(5).All(v => v == null), "Windows rereads the Mac padded record with both recorded hashes (" + Read(reread) + ")");
            await RunConversationR(RScriptWriter.Write(localRecord, null, FrameReader.Read).Text, "mac-padded-reread", assertion, "MAC_PADDED_OK");
            ((IGrid)grid).SetValues(2, 1, new object[,] { { 999.0 } });
            Check(RScriptWriter.Write(localRecord, null, FrameReader.Read).Text.Contains("\"After\" has changed in the workbook"),
                "editing a cell is detected when rereading the Mac record");
        });

        // A multi-column category frame whose first column's range is the longer: the reread sized its buffer from the last column.
        await WithGrid(main, "Saved frequency, longer range first", async grid =>
        {
            Fill(grid, 0, "Response one", Enumerable.Range(0, 3000).Select(k => (object)(double)(1 + k * 7 % 5)));
            Fill(grid, 1, "Response two", Enumerable.Range(0, 2600).Select(k => (object)(double)(1 + k * 3 % 4)));
            var (run, output) = RunOnGrid(grid, "Frequency", new() { ["data"] = "A1:A3001,B1:B2601", ["sortBy"] = "value", ["sortOrder"] = "asc" });
            JsonElement record = JsonDocument.Parse(run.Record).RootElement.Clone();
            JsonElement input = SavedInput(record, "data");
            Check(!record.TryGetProperty("data", out _) && input.GetProperty("kind").GetString() == "frame"
                && input.GetProperty("columns")[0].GetProperty("rows")[1].GetInt32() == 3001 && input.GetProperty("columns")[1].GetProperty("rows")[1].GetInt32() == 2601,
                "frequency of 5,600 categories is saved pointer-only, the longer range first");
            FrameValues reread = FrameReader.Read(FramePointer.From(input));
            Check(reread.Unavailable == null && reread.Columns.Count == 2 && reread.Columns.All(c => !c.Changed)
                && reread.Columns[0].Values.Length == 3000 && reread.Columns[1].Values.Length == 2600,
                "the actual reread of a category frame with its longer range first gives every value with its recorded hash (" + Read(reread) + ")");
            RScript script = RScriptWriter.Write(record, "Frequency", FrameReader.Read);
            Check(script.HasRecipe && !script.DataUnavailable && !script.Text.Contains("has changed in the workbook"), "the frequency replay reads its pointer-only data without a change note");
            const string checkColumn = """
                check_column <- function(title, total, labels, frequencies) {
                  first <- match(title, output)
                  header <- first + match("Value  Frequency  Relative %  Cumulative  Cumulative Relative %",
                                          output[seq.int(first + 1L, length(output))])
                  stopifnot(identical(output[header - 1L], paste("Total =", total)))
                  rows <- strsplit(trimws(output[header + seq_along(labels)]), "[[:space:]]+")
                  stopifnot(identical(vapply(rows, `[`, "", 1L), labels),
                            identical(as.numeric(vapply(rows, `[`, "", 2L)), frequencies))
                }
                """;
            var columns = (IList<ParameterBag>)output.ParameterBag["*variable"].AsObject;
            var expected = columns.Select(column =>
            {
                var bins = (IList<ParameterBag>)column["*bin"].AsObject;
                string title = RScriptWriter.Quote((string)column["ti"].AsObject);
                string total = Number(column, "n").ToString("R", inv);
                string labels = "c(" + string.Join(", ", bins.Select(b => RScriptWriter.Quote((string)b["x"].AsObject))) + ")";
                string frequencies = Vector(bins.Select(b => Number(b, "fx")));
                return $"check_column({title}, {total}, {labels}, {frequencies})";
            });
            await RunConversationR("output <- capture.output({\n" + script.Text + "\n})", "saved-frequency-longer-first",
                checkColumn + "\n" + string.Join("\n", expected) + "\ncat('SAVED_FREQUENCY_OK\\n')", "SAVED_FREQUENCY_OK");
            Notebook(record, "Frequency", "the frequency notebook export");

            // the longer range last, which was always read right, reads as before
            var (lastRun, _) = RunOnGrid(grid, "Frequency", new() { ["data"] = "B1:B2601,A1:A3001", ["sortBy"] = "value", ["sortOrder"] = "asc" });
            FrameValues last = FrameReader.Read(FramePointer.From(SavedInput(JsonDocument.Parse(lastRun.Record).RootElement.Clone(), "data")));
            Check(last.Unavailable == null && last.Columns.All(c => !c.Changed) && last.Columns.Select(c => c.Values.Length).SequenceEqual(new[] { 2600, 3000 })
                && last.Columns[0].Values.SequenceEqual(reread.Columns[1].Values) && last.Columns[1].Values.SequenceEqual(reread.Columns[0].Values),
                "the same columns with the longer range last reread the same values (" + Read(last) + ")");
        });

        // The same with two raters: equal counts of ratings, but the first range runs three blank rows past the last rating.
        await WithGrid(main, "Saved kappa, unequal ranges", async grid =>
        {
            Fill(grid, 0, "First rater", Enumerable.Range(0, 2597).Select(k => (object)(double)(1 + k * 7 % 3)));
            Fill(grid, 1, "Second rater", Enumerable.Range(0, 2597).Select(k => (object)(double)(1 + (k * 7 + (k % 4 == 0 ? 1 : 0)) % 3)));
            Fill(grid, 3, "Notes", Enumerable.Range(0, 2604).Select(k => (object)"seen"));   // the sheet's used rows run past both selections
            var (run, output) = RunOnGrid(grid, "Kappa", new() { ["ci"] = 0.95, ["responses"] = "A1:A2601,B1:B2598", ["method"] = "1" });
            JsonElement record = JsonDocument.Parse(run.Record).RootElement.Clone();
            JsonElement input = SavedInput(record, "responses");
            Check(!record.TryGetProperty("data", out _) && input.GetProperty("columns")[0].GetProperty("rows")[1].GetInt32() == 2601,
                "kappa of 2,597 paired ratings is saved pointer-only with the first rater's longer range");
            FrameValues reread = FrameReader.Read(FramePointer.From(input));
            Check(reread.Unavailable == null && reread.Columns.All(c => !c.Changed && c.Values.Length == 2597),
                "the actual reread of the raters' unequal ranges gives 2,597 ratings each with their recorded hashes (" + Read(reread) + ")");
            RScript script = RScriptWriter.Write(record, "Kappa", FrameReader.Read);
            Check(script.HasRecipe && !script.DataUnavailable, "the kappa replay reads its pointer-only data");
            ParameterBag k = output.ParameterBag;
            await RunConversationR(script.Text, "saved-kappa-unequal-ranges",
                "stopifnot(n == 2597, isTRUE(all.equal(unname(c(kappa, se0, se, kappaw, se0w, sew, (po - spe) / (1 - spe))), " +
                Vector(new[] { "kappa", "se", "seci", "kappaw", "sekw", "sekwci", "spi" }.Select(key => Number(k, key))) + ", tolerance = 1e-10, check.attributes = FALSE)))\ncat('SAVED_KAPPA_OK\\n')",
                "SAVED_KAPPA_OK");
            Notebook(record, "Kappa", "the kappa notebook export");
        });

        // A run saved by 5.3.5 or 5.3.6 after the user accepted padding: its frame is pointers, and the hash of the shorter column
        // includes the missing values that padded it to the longer one. 5.3.7 saves such a frame as entered values (WasPadded), so the
        // older record is made here by the same writer without that flag: Before and After hash as that record of these data does.
        await WithGrid(main, "Saved paired t, padded by 5.3.6", async grid =>
        {
            Fill(grid, 0, "Before", Enumerable.Range(0, 6000).Select(k => (object)(200.0 + 3 * k + k % 4 * 0.25)));
            Fill(grid, 1, "After", Enumerable.Range(0, 15).Select(k => (object)(195.0 + 2.5 * k + k % 3 * 0.5)));
            ((IGrid)grid).SetValues(6004, 0, new object[,] { { 999.0, 999.0 } });   // beyond both selections: never read
            var (run, output) = RunOnGrid(grid, "TPaired", new() { ["data"] = new Padded("A1:A6001,B1:B16"), ["gamma"] = 0.95, ["doAgreement"] = false });
            JsonElement record = JsonDocument.Parse(run.Record).RootElement.Clone();
            JsonElement input = SavedInput(record, "data");
            JsonElement columns = input.GetProperty("columns");
            Check(input.GetProperty("kind").GetString() == "frame" && !record.TryGetProperty("data", out _)
                && columns[0].GetProperty("rows")[1].GetInt32() == 6001 && columns[1].GetProperty("rows")[1].GetInt32() == 16
                && columns[0].GetProperty("hash").GetString() == "072508da8fc71fc2" && columns[1].GetProperty("hash").GetString() == "2ee352f51ee9c6a9",
                "the padded pair is saved as the 5.3.6 pointer record: rows 1 to 6001 and 1 to 16, the After hash over 6,000 values");
            ParameterBag native = output.ParameterBag;
            Check(Number(native, "n") == 15, "native paired t uses the 15 complete pairs");
            FrameValues reread = FrameReader.Read(FramePointer.From(input));
            Check(reread.Unavailable == null && reread.Columns.All(c => !c.Changed && c.Values.Length == 6000) && reread.Columns[1].Values.Skip(15).All(v => v == null),
                "the actual reread pads the shorter column back to its recorded hash, without a false change");
            RScript script = RScriptWriter.Write(record, "Paired t test", FrameReader.Read);
            Check(script.HasRecipe && !script.DataUnavailable && !script.Text.Contains("has changed in the workbook"), "the padded record replays without a change note");
            string expected = Vector(new[] { "n", "mean", "sd", "df", "t", "tail_2", "from", "to" }.Select(key => Number(native, key)));
            string assertion = "stopifnot(isTRUE(all.equal(unname(c(n, mean(d), sd(d), r$parameter, r$statistic, r$p.value, r$conf.int)), " + expected + ", tolerance = 1e-10, check.attributes = FALSE)))\ncat('SAVED_PADDED_OK\\n')";
            await RunConversationR(script.Text, "saved-padded-pairs", assertion, "SAVED_PADDED_OK");
            // the notebook's replay runs in its own local scope: its printed figures are those of the native run
            await RunConversationR(Notebook(record, "Paired t test", "the padded notebook export"), "saved-padded-notebook", "cat('SAVED_PADDED_NOTEBOOK_OK\\n')", "SAVED_PADDED_NOTEBOOK_OK");
            string printed = File.ReadAllText(Path.Combine(artifacts, "saved-padded-notebook", "output.txt"));
            Check(printed.Contains("(n = 15)") && printed.Contains("95% CI = " + Number(native, "from").ToString("F6", inv) + " to " + Number(native, "to").ToString("F6", inv)),
                "the notebook replay of the padded record prints the native pairs and interval");

            // An edit to the shorter column is still a change: padding never hides it, and the notebook refuses the edited values.
            ((IGrid)grid).SetValues(4, 1, new object[,] { { 1000.0 } });
            FrameValues edited = FrameReader.Read(FramePointer.From(input));
            Check(edited.Unavailable == null && !edited.Columns[0].Changed && edited.Columns[1].Changed && edited.Columns[1].Values.Length == 15,
                "an edited cell of the padded column is reported as changed and is not padded");
            Check(RScriptWriter.Write(record, "Paired t test", FrameReader.Read).Text.Contains("NOTE: \"After\" has changed in the workbook"), "the edited padded column keeps its change note");
            Check(!NotebookAccepted(record, "Paired t test"), "the notebook export refuses the edited padded column");
            ((IGrid)grid).SetValues(4, 1, new object[,] { { 195.0 + 2.5 * 3 } });
            Check(FrameReader.Read(FramePointer.From(input)).Columns.All(c => !c.Changed), "restoring the edited cell restores the padded record's values");
        });

        // Padding with hidden rows in the selections: the hash is of the visible values the analysis took, padded to the longer column.
        await WithGrid(main, "Saved paired t, padded and filtered", async grid =>
        {
            Fill(grid, 0, "Before", Enumerable.Range(0, 6000).Select(k => (object)(150.0 + 2 * k + k % 5 * 0.5)));
            Fill(grid, 1, "After", Enumerable.Range(0, 20).Select(k => (object)(149.0 + 2 * k + k % 3 * 0.25)));
            var view = (SpreadsheetGear.Windows.Forms.WorkbookView)Field(grid, "workbookView");
            view.GetLock();
            try { foreach (int row in new[] { 5, 12, 3000 }) view.ActiveWorksheet.Cells[row, 0].EntireRow.Hidden = true; }
            finally { view.ReleaseLock(); }
            var (run, output) = RunOnGrid(grid, "TPaired", new() { ["data"] = new Padded("A1:A6001,B1:B21"), ["gamma"] = 0.95, ["doAgreement"] = false });
            JsonElement record = JsonDocument.Parse(run.Record).RootElement.Clone();
            JsonElement input = SavedInput(record, "data");
            FrameValues reread = FrameReader.Read(FramePointer.From(input));
            Check(input.GetProperty("kind").GetString() == "frame" && reread.Unavailable == null && reread.Columns.All(c => !c.Changed && c.Values.Length == 5997),
                "a padded record with hidden rows rereads its visible values, padded back to the recorded hash");
            ParameterBag native = output.ParameterBag;
            string expected = Vector(new[] { "n", "mean", "sd", "df", "t", "tail_2", "from", "to" }.Select(key => Number(native, key)));
            await RunConversationR(RScriptWriter.Write(record, "Paired t test", FrameReader.Read).Text, "saved-padded-filtered",
                "stopifnot(n == 18, isTRUE(all.equal(unname(c(n, mean(d), sd(d), r$parameter, r$statistic, r$p.value, r$conf.int)), " + expected + ", tolerance = 1e-10, check.attributes = FALSE)))\ncat('SAVED_FILTERED_PADDED_OK\\n')",
                "SAVED_FILTERED_PADDED_OK");
            // showing a hidden row inside the shorter range changes what the analysis would read: never padded into a match
            view.GetLock();
            try { view.ActiveWorksheet.Cells[12, 0].EntireRow.Hidden = false; }
            finally { view.ReleaseLock(); }
            Check(FrameReader.Read(FramePointer.From(input)).Columns.All(c => c.Changed), "showing a hidden row after the run is still reported as a change of both columns");
        });

        // Two columns padded to a third in one record of 5.3.5 or 5.3.6. The padding is taken for every shorter column or for none: when
        // both recover their recorded hashes the record replays as it was saved, and when one of them has been edited the other's padding
        // alone would still recover its hash, but the frame is then not the one the analysis used, so neither column is padded.
        await WithGrid(main, "Saved Friedman, two columns padded by 5.3.6", async grid =>
        {
            double[] second = { 24.5, 22.5, 26.5, 25.5, 21.5, 28.5, 23.5, 27.5, 24.25, 26.25, 22.25, 25.25 }, third = { 26.75, 25.75, 21.75, 27.75, 29.75, 20.75, 24.75, 23.75, 28.75 };
            Fill(grid, 0, "Diet A", Enumerable.Range(0, 6000).Select(k => (object)(20.0 + k * 7 % 11 + k * 0.001)));
            Fill(grid, 1, "Diet B", second.Select(v => (object)v));
            Fill(grid, 2, "Diet C", third.Select(v => (object)v));
            var (run, output) = RunOnGrid(grid, "Friedman", new() { ["data"] = new Padded("A1:A6001,B1:B13,C1:C10") });
            JsonElement record = JsonDocument.Parse(run.Record).RootElement.Clone();
            JsonElement input = SavedInput(record, "data");
            JsonElement columns = input.GetProperty("columns");
            Check(input.GetProperty("kind").GetString() == "frame" && !record.TryGetProperty("data", out _)
                && new[] { 6001, 13, 10 }.Select((last, c) => columns[c].GetProperty("rows")[1].GetInt32() == last).All(same => same),
                "three columns, two of them padded, are saved as the 5.3.6 pointer record: rows 1 to 6001, 1 to 13 and 1 to 10");
            ParameterBag native = output.ParameterBag;
            Check(Number(native, "nb") == 9 && Number(native, "df") == 2, "native Friedman uses the 9 complete blocks of the three treatments");
            FrameValues reread = FrameReader.Read(FramePointer.From(input));
            Check(reread.Unavailable == null && reread.Columns.All(c => !c.Changed && c.Values.Length == 6000)
                && reread.Columns[1].Values.Skip(12).All(v => v == null) && reread.Columns[2].Values.Skip(9).All(v => v == null),
                "the actual reread pads both shorter columns back to their recorded hashes, without a false change (" + Read(reread) + ")");
            RScript script = RScriptWriter.Write(record, "Friedman test", FrameReader.Read);
            Check(script.HasRecipe && !script.DataUnavailable && !script.Text.Contains("has changed in the workbook"), "the record with two padded columns replays without a change note");
            await RunConversationR(script.Text, "saved-two-padded",
                "stopifnot(isTRUE(all.equal(unname(friedman_result[c('n', 'k', 'a2', 't1', 't2', 'p')]), " + Vector(new[] { Number(native, "nb"), Number(native, "df") + 1 }.Concat(new[] { "a2", "t1", "t2", "p" }.Select(key => Number(native, key)))) +
                ", tolerance = 1e-10, check.attributes = FALSE)))\ncat('SAVED_TWO_PADDED_OK\n')",
                "SAVED_TWO_PADDED_OK");

            // An edit to either shorter column leaves the other's padding able to recover its hash: neither is padded, the edited column
            // keeps its change note and the notebook refuses the values, whichever of the two comes first in the selection.
            foreach (var (column, row, title, entered) in new[] { (2, 4, "Diet C", third[3]), (1, 7, "Diet B", second[6]) })
            {
                ((IGrid)grid).SetValues(row, column, new object[,] { { 1000.0 } });
                FrameValues edited = FrameReader.Read(FramePointer.From(input));
                Check(edited.Unavailable == null && !edited.Columns[0].Changed && edited.Columns.Skip(1).All(c => c.Changed) && edited.Columns.Select(c => c.Values.Length).SequenceEqual(new[] { 6000, 12, 9 }),
                    "with an edited cell in " + title + ", one of two padded columns, neither column is padded and both are reported as changed (" + Read(edited) + ")");
                Check(RScriptWriter.Write(record, "Friedman test", FrameReader.Read).Text.Contains("NOTE: \"" + title + "\" has changed in the workbook"), "the edited " + title + " keeps its change note beside the other padded column");
                Check(!NotebookAccepted(record, "Friedman test"), "the notebook export refuses the record with " + title + " edited");
                ((IGrid)grid).SetValues(row, column, new object[,] { { entered } });
                Check(FrameReader.Read(FramePointer.From(input)).Columns.All(c => !c.Changed && c.Values.Length == 6000), "restoring the edited cell of " + title + " restores both paddings of the record");
            }
        });

        await WithGrid(main, "Saved one way, two columns padded by 5.3.6", async grid =>
        {
            Fill(grid, 0, "Group one", Enumerable.Range(0, 3000).Select(k => (object)(10.0 + k % 17 * 0.5)));
            Fill(grid, 1, "Group two", Enumerable.Range(0, 2400).Select(k => (object)(11.0 + k % 13 * 0.25)));
            Fill(grid, 2, "Group three", Enumerable.Range(0, 40).Select(k => (object)(12.0 + k % 7 * 0.75)));
            var (run, output) = RunOnGrid(grid, "OneWay", new() { ["data"] = new Padded("A1:A3001,B1:B2401,C1:C41") });
            JsonElement record = JsonDocument.Parse(run.Record).RootElement.Clone();
            JsonElement input = SavedInput(record, "data");
            FramePointer Pointer() => FramePointer.From(input);
            var pointer = Pointer();
            Check(!record.TryGetProperty("data", out _) && pointer.Mode == "NumericReplaceMissing"
                && pointer.Columns.Select(c => c.LastRow).SequenceEqual(new[] { 3001, 2401, 41 })
                && pointer.Columns.Select(c => c.Hash).SequenceEqual(new[] { "e69e1717ffb9ae6c", "a51ae29acc5c03fe", "cc605a61a19f8ced" }),
                "the one-way padded record has the three expected original ranges and hashes");

            void CheckRead(FramePointer p, int[] lengths, bool[] changed, string label)
            {
                FrameValues values = FrameReader.Read(p);
                Check(values.Unavailable == null && values.Columns.Select(c => c.Values.Length).SequenceEqual(lengths)
                    && values.Columns.Select(c => c.Changed).SequenceEqual(changed), label + " (" + Read(values) + ")");
            }

            CheckRead(pointer, new[] { 3000, 3000, 3000 }, new[] { false, false, false }, "both shorter one-way columns recover their recorded padding");
            ParameterBag native = output.ParameterBag;
            string expected = Vector(new[] { "f", "b_sum", "w_sum", "w_df" }.Select(key => Number(native, key)));
            RScript script = RScriptWriter.Write(record, null, FrameReader.Read);
            Check(!script.DataUnavailable && !script.Text.Contains("has changed in the workbook"), "the padded one-way record replays without a change note");
            await RunConversationR(script.Text, "saved-one-way-padded",
                "stopifnot(isTRUE(all.equal(c(a$\"F value\"[1], a$\"Sum Sq\"[1], a$\"Sum Sq\"[2], a$Df[2]), " + expected
                + ", tolerance=1e-10)), isTRUE(all.equal(" + expected
                + ", c(405.5196746169783, 3011.5213463541654, 20188.46752083341, 5437), tolerance=1e-10)))\ncat('ONE_WAY_PADDED_OK\\n')",
                "ONE_WAY_PADDED_OK");

            foreach (var (row, column, original) in new[] { (6, 2, 15.75), (8, 1, 12.75) })
            {
                ((IGrid)grid).SetValues(row, column, new object[,] { { 777.0 } });
                CheckRead(pointer, new[] { 3000, 2400, 40 }, new[] { false, true, true }, "editing either shorter column prevents both paddings");
                string edited = RScriptWriter.Write(record, null, FrameReader.Read).Text;
                Check(edited.Contains("\"Group two\" has changed") && edited.Contains("\"Group three\" has changed") && !edited.Contains("\"Group one\" has changed"),
                    "the one-way replay flags both shortened columns");
                ((IGrid)grid).SetValues(row, column, new object[,] { { original } });
            }
            ((IGrid)grid).SetValues(101, 0, new object[,] { { -1.0 } });
            CheckRead(pointer, new[] { 3000, 3000, 3000 }, new[] { true, false, false }, "editing the longest column keeps the two recorded paddings");
            ((IGrid)grid).SetValues(101, 0, new object[,] { { 17.5 } });

            var legacy = Pointer();
            legacy.Columns[2].Hash = null;
            CheckRead(legacy, new[] { 3000, 3000, 40 }, new[] { false, false, false }, "a hashless short column keeps its read length");
            legacy = Pointer();
            legacy.Columns[0].Hash = null;
            CheckRead(legacy, new[] { 3000, 3000, 3000 }, new[] { false, false, false }, "a hashless longest column still provides the padding length");
            legacy = Pointer();
            legacy.Columns[1].Hash = "3deb850a40bd344a";
            CheckRead(legacy, new[] { 3000, 2400, 3000 }, new[] { false, false, false }, "a hash of unpadded values leaves only the other short column padded");
            legacy.Columns[2].Hash = "777f199ffe4c27da";
            CheckRead(legacy, new[] { 3000, 2400, 40 }, new[] { false, false, false }, "hashes of both unpadded values preserve both shorter lengths");
        });

        // Raters whose columns are unequal, padded when the user accepts the warning that "the jagged ends of columns will be padded with
        // missing data": the padded rows are missing ratings, not ratings of the shorter column's first category (its id, 0, was the
        // padding). Kappa is then of the seven subjects both raters rated, as base R computes it from those pairs alone.
        await WithGrid(main, "Kappa, padded raters", async grid =>
        {
            double[] first = { 1, 2, 3, 2, 1, 3, 2, 2, 1, 3, 3, 1 }, second = { 1, 2, 2, 2, 3, 3, 1 };
            Fill(grid, 0, "First rater", first.Select(v => (object)v));
            Fill(grid, 1, "Second rater", second.Select(v => (object)v));
            var (run, output) = RunOnGrid(grid, "Kappa", new() { ["ci"] = 0.95, ["responses"] = new Padded("A1:A13,B1:B8", Legacy: false), ["method"] = "1" });
            ParameterBag k = output.ParameterBag;
            double pairs = ((IEnumerable<ParameterBag>)k["*y"].AsObject).SelectMany(y => (IEnumerable<ParameterBag>)y["*tot"].AsObject).Sum(c => Number(c, "tot"));
            Check(pairs == 7, "native kappa of padded raters tabulates the 7 subjects both rated, not the 12 rows of the padded frame (" + pairs + ")");
            JsonElement record = JsonDocument.Parse(run.Record).RootElement.Clone();
            JsonElement input = SavedInput(record, "responses");
            JsonElement padded = input.GetProperty("columns")[1].GetProperty("values");
            Check(input.GetProperty("kind").GetString() == "entered" && padded.GetArrayLength() == 12 && padded.EnumerateArray().Skip(7).All(v => v.ValueKind == JsonValueKind.Null),
                "the record keeps the padded rows of the second rater as missing values");
            RScript script = RScriptWriter.Write(record, "Kappa", FrameReader.Read);
            Check(script.HasRecipe && !script.DataUnavailable, "the padded kappa replays from its record");
            string reference = "first <- " + Vector(first.Take(7)) + "\nsecond <- " + Vector(second) + "\n" +
                "tab <- table(factor(first, 1:3), factor(second, 1:3))\nN <- sum(tab)\npo_ref <- sum(diag(tab)) / N\npe_ref <- sum(rowSums(tab) * colSums(tab)) / N^2\n" +
                "kappa_ref <- (po_ref - pe_ref) / (1 - pe_ref)\n";
            await RunConversationR(script.Text, "saved-kappa-padded",
                reference + "stopifnot(N == 7, n == 7, isTRUE(all.equal(kappa_ref, 11 / 32)), isTRUE(all.equal(c(" + Number(k, "kappa").ToString("R", inv) + ", " + (Number(k, "po") / 100).ToString("R", inv) + "), c(kappa_ref, po_ref), tolerance = 1e-12)), " +
                "isTRUE(all.equal(unname(c(kappa, se0, se, kappaw, se0w, sew, (po - spe) / (1 - spe))), " +
                Vector(new[] { "kappa", "se", "seci", "kappaw", "sekw", "sekwci", "spi" }.Select(key => Number(k, key))) + ", tolerance = 1e-10, check.attributes = FALSE)))\ncat('SAVED_KAPPA_PADDED_OK\\n')",
                "SAVED_KAPPA_PADDED_OK");
        });

        // Three raters, the third with fewer ratings: the padded subjects have two ratings, not a third of the first category, and the
        // kappa of Fleiss and Cuzick (1979) for unequal numbers of ratings is that of the ratings given, computed in base R.
        await WithGrid(main, "Kappa, three padded raters", async grid =>
        {
            double[][] raters = { new double[] { 1, 2, 1, 1, 2, 2, 1, 2 }, new double[] { 1, 2, 2, 1, 2, 1, 1, 2 }, new double[] { 1, 1, 2, 1, 2 } };
            for (int c = 0; c < raters.Length; c++) Fill(grid, c, "Rater " + (c + 1), raters[c].Select(v => (object)v));
            var (_, output) = RunOnGrid(grid, "Kappa", new() { ["ci"] = 0.95, ["responses"] = new Padded("A1:A9,B1:B9,C1:C6", Legacy: false) });
            ParameterBag k = output.ParameterBag;
            Check(Convert.ToInt32(k["categories"].AsObject, inv) == 2 && k["r"].AsObject as string == "2 to 3 (median 3)",
                "native kappa of three padded raters counts 2 to 3 ratings per subject (" + k["r"].AsObject + ")");
            string reference = "X <- cbind(" + string.Join(", ", raters.Select(r => Vector(r.Concat(Enumerable.Repeat(double.NaN, 8 - r.Length))).Replace("NaN", "NA"))) + ")\n" +
                "m <- rowSums(!is.na(X))\nx <- rowSums(X == 1, na.rm = TRUE)\nn <- length(m)\nmbar <- mean(m)\npbar <- sum(x) / sum(m)\n" +
                "B <- sum((x - m * pbar)^2 / m) / n\nW <- sum(x * (m - x) / m) / (n * (mbar - 1))\nkappa_ref <- (B - W) / (B + (mbar - 1) * W)\n";
            await RunConversationR("", "saved-kappa-three-padded",
                reference + "stopifnot(identical(as.vector(m), c(3, 3, 3, 3, 3, 2, 2, 2)), isTRUE(all.equal(" + Number(k, "k").ToString("R", inv) + ", kappa_ref, tolerance = 1e-12)))\ncat('SAVED_KAPPA_THREE_OK\\n')",
                "SAVED_KAPPA_THREE_OK");
        });
    }

    /// <summary>A frame answered as the user's selection of these ranges, padded as the program pads it when the user accepts unequal
    /// columns: the production acquisition and padding, without the warning's dialog. A legacy answer is as 5.3.5 and 5.3.6 padded it,
    /// without the flag 5.3.7 added; otherwise the flag stays, as the program now sets it.</summary>
    sealed record Padded(string Address, bool Legacy = true);

    static async Task WithGrid(frmMain main, string name, Func<frmSpreadsheetGear, Task> test)
    {
        var grid = (frmSpreadsheetGear)main.CreateGrid(name);
        try { await test(grid); }
        finally { CloseTestDocument(grid); }
    }

    static void Fill(frmSpreadsheetGear grid, int column, string title, IEnumerable<object> values)
    {
        object[] cells = new object[] { title }.Concat(values).ToArray();
        object[,] block = new object[cells.Length, 1];
        for (int i = 0; i < cells.Length; i++) block[i, 0] = cells[i];
        ((IGrid)grid).SetValues(0, column, block);
    }

    static void Select(frmSpreadsheetGear grid, string address)
    {
        var view = (SpreadsheetGear.Windows.Forms.WorkbookView)Field(grid, "workbookView");
        view.GetLock();
        try { view.ActiveWorksheet.Range[address].Select(); }
        finally { view.ReleaseLock(); }
    }

    /// <summary>What a reread gave, for the label of a check: why it could not be read, or each column's count and whether it changed.</summary>
    static string Read(FrameValues values) => values.Unavailable ?? string.Join(", ", values.Columns.Select(c => c.Values.Length + (c.Changed ? " changed" : "")));

    static JsonElement SavedInput(JsonElement record, string name) => record.GetProperty("inputs").EnumerateArray().First(i => i.GetProperty("name").GetString() == name);

    static (OperationRun run, StepOutput output) RunOnGrid(frmSpreadsheetGear grid, string operation, Dictionary<string, object> answers)
    {
        var host = new SavedRunHost(grid, answers);
        ITemplateProcessor processor = new TemplateProcessor(host);
        StepOutput output = processor.Execute(TemplateFactory.Operations[operation], new ParameterBag());
        if (host.LastRun?.Record == null) throw new Exception(operation + " left no record of its run");
        return (host.LastRun, output);
    }

    static string Notebook(JsonElement record, string title, string label)
    {
        string json = ReportNotebook.Write(NotebookSnapshot(record, title), title, FrameReader.Read, "test");
        string code = string.Join("\n", JsonDocument.Parse(json).RootElement.GetProperty("cells").EnumerateArray()
            .Where(c => c.GetProperty("cell_type").GetString() == "code").Select(c => c.GetProperty("source").GetString()));
        Check(code.Contains("local({"), label + " accepts the reread values");
        return code;
    }

    static bool NotebookAccepted(JsonElement record, string title)
    {
        try { ReportNotebook.Write(NotebookSnapshot(record, title), title, FrameReader.Read, "test"); return true; }
        catch (InvalidOperationException) { return false; }
    }

    static JsonElement NotebookSnapshot(JsonElement record, string title) => JsonSerializer.SerializeToElement(new JsonObject
    {
        ["version"] = 1, ["engineVersion"] = "test", ["reportHtml"] = "<p>" + title + "</p>",
        ["entries"] = new JsonArray(new JsonObject
        {
            ["id"] = "saved-run", ["title"] = title, ["operation"] = record.GetProperty("operation").GetString(), ["html"] = "<p>" + title + "</p>",
            ["cells"] = new JsonArray(new JsonObject { ["source"] = title }), ["record"] = JsonNode.Parse(record.GetRawText())
        })
    });

    /// <summary>The host of a run on the worksheet: a frame prompt is answered by selecting its ranges on the grid and acquiring them with
    /// the program's own selection code, every other prompt from the answers given; the run the report step hands it is kept.</summary>
    sealed class SavedRunHost : ITemplateHost, IProgressBar
    {
        readonly frmSpreadsheetGear grid;
        readonly Dictionary<string, object> answers;
        internal OperationRun LastRun { get; private set; }
        internal SavedRunHost(frmSpreadsheetGear grid, Dictionary<string, object> answers) { this.grid = grid; this.answers = answers; }
        public Operation Operation { get; set; }
        public IDictionary<string, ParameterBag> SessionParametersPerOperation { get; } = new Dictionary<string, ParameterBag>();
        public ParameterBag SessionParametersAcrossOperations => new();
        public SDPreferences Preferences => SdApplication.SoleInstance.Preferences;
        public ParameterBag Amend(IFillable options, ParameterBag context) => context;
        public bool CanCombine(Parameter parameter) => false;
        public void Error(string message, string caption) => throw new Exception("the operation refused: " + message);
        public void Warning(string message, string caption) { }
        public bool GetBoolean(string prompt, string title, bool initialValue, out bool cancelled) { cancelled = false; return initialValue; }
        public ParameterBag FillAndValidateCombinedParameters(ITemplateProcessor processor, ParameterBag context) => throw new NotImplementedException();
        public ParameterBag FillParameter(ITemplateProcessor processor, Parameter parameter, ParameterBag context, bool shouldCombine)
        {
            if (parameter.HasAcquireIfTrue && !parameter.AcquireIfTrue(processor, context)) return new ParameterBag();
            if (!answers.TryGetValue(parameter.Name, out object value)) throw new Exception($"the operation asked for {parameter.Name}, which these checks do not answer");
            if (parameter is FrameParameter frame && value is string address)
            {
                Select(grid, address);
                return new GridSelectionProcessor(grid).FillFrameParameter(parameter, processor, context);
            }
            if (parameter is FrameParameter legacy && value is Padded padded)
            {
                // the acquisition GetCellEqual makes and, on OK to its unequal-length warning, its padding of the jagged ends
                Select(grid, padded.Address);
                DataFrame acquired = grid.GetCellArray(0, legacy.SelectionMode, legacy.MinimumColumns(processor, context), legacy.MaximumColumns(processor, context),
                    legacy.Prompt(processor, context), null, false, false, out bool cancelled, out bool pivoted, processor.NextOriginGroup());
                if (acquired == null || cancelled || pivoted || acquired.Variables.All(v => v.Length == acquired.MaxRows)) throw new Exception("the padded selection was not acquired as unequal columns");
                GridSelectionProcessor.PadAcquiredFrame(acquired, acquired.MaxRows);
                if (padded.Legacy)
                    foreach (IVariable variable in acquired.Variables) ((WorksheetOrigin)variable.Origin).WasPadded = false;
                return new ParameterBag(parameter.Name, FilledParameterFactory.Input(acquired));
            }
            return new ParameterBag(parameter.Name, FilledParameterFactory.Input(value));
        }
        public void PrepareParameter(ITemplateProcessor processor, Parameter parameter, ParameterBag context) { }
        public IScriptEngine GetScriptEngine(string language) => ScriptEngine.CanHandle(language) ? new ScriptEngine() : null;
        public void OutputFrame(DataFrame frame, bool keepSelection, bool isFormulae, string missingIndicator, PaneAndPosition preferredOutputLocation, RelativePosition defaultPosition) { }
        public object OutputReport(IRenderable renderable, Operation operation, object preferredOutputLocation, object run) { LastRun = run as OperationRun; return null; }
        public string pval(double p) => p.ToString("R", CultureInfo.InvariantCulture);
        public string pval_half(double p) => p.ToString("R", CultureInfo.InvariantCulture);
        public string RoundU(double amount) => amount.ToString("R", CultureInfo.InvariantCulture);
        public IProgressBar StartProgress(string operationDescription, bool provideProgress, bool display = true) => this;
        public void Finish() { }
        public bool Update(double fractionComplete) => false;
        public void Dispose() { }
    }
}
