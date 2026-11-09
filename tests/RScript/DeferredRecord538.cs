using System.Reflection;
using System.Text.Json;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.R;
using StatsDirect.Templates;
using StatsDirect.UI;
using StatsDirect.Utilities;

internal static partial class Program
{
    private static void CheckDeferredRecord538(string folder)
    {
        Directory.CreateDirectory(folder);
        double Number(ParameterBag b, string key) => Convert.ToDouble(b[key].AsObject, inv);
        foreach (int longCount in new[] { 40, 6000 })
        foreach (bool title in new[] { false, true })
        {
            const int shortCount = 15;
            object[] first = Enumerable.Range(0, shortCount).Select(i => (object)(100d + i * .7 + Math.Sin(i))).ToArray();
            object[] second = Enumerable.Range(0, longCount).Select(i => (object)(95d + i * .4 + Math.Cos(i))).ToArray();
            if (title) { first = new object[] { "First" }.Concat(first).ToArray(); second = new object[] { "Second" }.Concat(second).ToArray(); }
            var cells = new RecordCells(first, second);
            var x = cells.Acquire(0, 0, first.Length, DataAcquisitionMode.NumericPreserveRows);
            var y = cells.Acquire(1, 0, second.Length, DataAcquisitionMode.NumericPreserveRows);
            DataFrame frame = new(); frame.Variables.Add(x.Variables[0]); frame.Variables.Add(y.Variables[0]);
            // The same helper called after the user accepts the unequal-length
            // warning, exercised headlessly so release/signing UI is not disturbed.
            var processor = typeof(CellSelection).Assembly.GetType("StatsDirect.UI.GridSelectionProcessor");
            processor.GetMethod("PadAcquiredFrame", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { frame, frame.MaxRows });
            var (run, output) = Run("TPaired", new() { ["data"] = frame, ["gamma"] = .95, ["doAgreement"] = false });
            var record = Record(run); var input = AcquisitionInput(record, "data");
            Check(input.GetProperty("kind").GetString() == "entered" && input.GetProperty("columns").EnumerateArray().All(c => c.GetProperty("values").GetArrayLength() == longCount), "accepted padded selection freezes complete aligned values even above embedding threshold");
            bool read = false;
            FrameValues ClosedWorkbook(FramePointer _) { read = true; return new FrameValues { Unavailable = "Workbook deliberately closed" }; }
            var script = RScriptWriter.Write(record, null, ClosedWorkbook);
            double[] expected = new[] { "n", "mean", "sd", "df", "t", "tail_2", "from", "to" }.Select(k => Number(output.ParameterBag, k)).ToArray();
            string assertion = "\nstopifnot(isTRUE(all.equal(unname(c(n,mean(d),sd(d),r$parameter,r$statistic,r$p.value,r$conf.int))," + RScriptWriter.Vector(expected.Cast<object>().ToArray()) + ",tolerance=1e-10,check.attributes=FALSE)))\ncat('PADDED_NATIVE_OK\\n')\n";
            Check(!script.DataUnavailable && !read && RunR(script.Text + assertion, Path.Combine(folder, "padded-" + longCount + "-" + title)).Contains("PADDED_NATIVE_OK"), "padded replay with closed workbook reproduces native paired statistics without a false change warning");
            string notebook = AcquisitionNotebook(record, ClosedWorkbook);
            Check(!read && RunR(notebook + "\ncat('PADDED_NOTEBOOK_OK\\n')\n", Path.Combine(folder, "padded-notebook-" + longCount + "-" + title)).Contains("PADDED_NOTEBOOK_OK"), "notebook retains accepted padding without an impossible worksheet reread");
        }

        double[] values = Enumerable.Range(1, 12).Select(i => i * .37).Concat(Enumerable.Repeat(Constant.MISSING, 9988)).ToArray();
        DataFrame pointer = SheetFrame(Workbook, "Deferred capture", 0, 0, ("Outcome", values));
        ((WorksheetOrigin)pointer.Variables[0].Origin).Mode = DataAcquisitionMode.NumericPreserveRows;
        ParameterBag bag = new(); bag.AddInput("outcome", pointer);
        DataFrame dummy = new(); dummy.Variables.Add(Column("Stage II", values.Select((v, i) => RunRecord.Missing(v) ? Constant.MISSING : i % 2).ToArray())); bag.AddInput("predictors", dummy);
        OperationRun fallback = new(new Operation { Name = "CoxRegression" });
        fallback.Note(new FrameParameter { Name = "outcome", PreserveRowAlignment = true }, "Outcome", value: bag["outcome"]);
        fallback.Note(new FrameParameter { Name = "predictors", PreserveRowAlignment = true }, "Predictors"); // defensive value-less frame note
        var captured = AcquisitionRecord(fallback, bag);
        Check(AcquisitionInput(captured, "outcome").GetProperty("columns")[0].GetProperty("rows")[1].GetInt32() == 13 && AcquisitionInput(captured, "predictors").GetProperty("columns")[0].GetProperty("values").GetArrayLength() == 12, "value-less fallback preserves the row-aligned entered contract and trims both frames together");
        Check(captured.GetProperty("data").GetProperty("outcome").GetProperty("Outcome").GetArrayLength() == 12, "embedding budget uses captured values rather than unused selected rows");
        OperationRun inherited = new(new Operation { Name = "CoxModelAnalysis" }, bag);
        var inheritedRecord = AcquisitionRecord(inherited, bag);
        Check(AcquisitionInput(inheritedRecord, "predictors").GetProperty("columns")[0].GetProperty("values").GetArrayLength() == 12 && AcquisitionInput(inheritedRecord, "outcome").GetProperty("columns")[0].GetProperty("rows")[1].GetInt32() == 13, "follow-on inherits fallback snapshots and their aligned-entered contract");
        // Fallback captures can see transformed arrays after an analysis. They
        // must not rewrite physical worksheet bounds using those later tails.
        OperationRun late = new(new Operation { Name = "TPaired" });
        late.Note(new FrameParameter { Name = "outcome", PreserveRowAlignment = true }, "Late outcome");
        var lateRecord = AcquisitionRecord(late, bag);
        Check(AcquisitionInput(lateRecord, "outcome").GetProperty("columns")[0].GetProperty("rows")[1].GetInt32() == 10001, "post-analysis fallback does not infer a new physical cutoff from transformed data");

        OperationRun hashRun = new(new Operation { Name = "Hash concurrency" });
        hashRun.Note(new FrameParameter { Name = "outcome", PreserveRowAlignment = true }, "Outcome", value: bag["outcome"]);
        object snapshot = typeof(RunInput).GetProperty("Snapshot", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hashRun.Inputs[0]);
        Array columns = (Array)snapshot.GetType().GetField("Columns", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(snapshot);
        object column = columns.GetValue(0);
        var hashAt = column.GetType().GetMethod("HashAt", BindingFlags.Instance | BindingFlags.NonPublic);
        string[] expectedHashes = Enumerable.Range(12, 32).Select(n => RunRecord.Hash(AcquisitionValues(values.Take(n).ToArray()))).ToArray();
        Parallel.For(0, 512, i =>
        {
            string actual = (string)hashAt.Invoke(column, new object[] { 12 + i % 32 });
            if (actual != expectedHashes[i % 32]) throw new Exception("Concurrent prefix hash changed");
        });
        Check(true, "concurrent exports clone the captured hash state safely and retain every expected prefix hash");

        foreach (string operation in new[] { "Cronbach", "PCRCoefficients", "PCRMatrix", "PCRScores" })
        {
            foreach (string parent in new[] { "PCRCorrelation", "PCRCovariance", "PCRCovarianceNoFollowOns" })
            {
                using var record = JsonDocument.Parse(JsonSerializer.Serialize(new { operation, rootOperation = parent }));
                Check(RRecipes.Find(record.RootElement).File == (parent == "PCRCorrelation" ? "pca.R" : "pca-covariance.R"), "PCA continuation selects its actual correlation/covariance parent: " + operation + "/" + parent);
            }
            using var unknown = JsonDocument.Parse(JsonSerializer.Serialize(new { operation }));
            Check(RRecipes.Find(unknown.RootElement) == null, "ambiguous legacy PCA continuation cannot silently choose the wrong parent: " + operation);
        }
    }
}
