using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StatsDirect.Data;
using StatsDirect.Templates;

internal static partial class Program
{
    // Bland and Altman's peak flow readings, the help's example of agreement: two matched columns
    private static readonly double[] first = { 190, 220, 260, 210, 270, 280, 260, 275, 280, 320, 300, 270, 320, 335, 350, 360, 330, 335, 400, 430 };
    private static readonly double[] second = { 220, 200, 260, 300, 265, 280, 280, 275, 290, 290, 300, 250, 330, 320, 320, 320, 340, 385, 420, 460 };
    private static string Workbook => Path.GetFullPath(Path.Combine(Metadata("Operations"), "..", "Data", "test.xlsx"));

    /// <summary>The hash as the record defines it, worked out here on its own: SHA-256 of the values as text joined by line feeds, the first 16 hex characters.</summary>
    private static string DefinedHash(IEnumerable<string> texts) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", texts))), 0, 8).ToLowerInvariant();

    /// <summary>The paired t statistic from its definition: the mean difference over its standard error.</summary>
    private static double PairedT(double[] x, double[] y)
    {
        int n = x.Length;
        double[] d = new double[n];
        for (int i = 0; i < n; i++) d[i] = x[i] - y[i];
        double mean = d.Average(), ss = d.Sum(v => (v - mean) * (v - mean));
        return mean / Math.Sqrt(ss / (n - 1) / n);
    }

    // The record of a run: the paired t test on two columns read from a sheet of the example workbook
    private static void CheckRecord()
    {
        DataFrame data = SheetFrame(Workbook, "Agreement", 0, 0, ("1st", first), ("2nd", second));   // columns A and B, rows 1 to 21, the titles in row 1
        var (run, output) = Run("TPaired", new() { ["data"] = data, ["gamma"] = 0.95, ["doAgreement"] = false });
        Check(run != null && run.Operation.Name == "TPaired" && output?.ParameterBag != null, "the report step hands the report the run of the operation");
        JsonElement r = Record(run);
        Console.WriteLine("      record: " + run.Record.Length + " characters; title " + r.GetProperty("title").GetString());
        Check(r.GetProperty("version").GetInt32() == 1 && r.GetProperty("operation").GetString() == "TPaired" && r.GetProperty("title").GetString().Length > 0
              && DateTime.TryParse(r.GetProperty("run").GetString(), inv, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime started) && (DateTime.Now - started).TotalMinutes < 5,
              "the record names the operation, its title and the time of the run");
        JsonElement source = r.GetProperty("source");
        Check(source.GetProperty("file").GetString() == "test.xlsx" && source.GetProperty("path").GetString() == Workbook
              && DateTime.TryParse(source.GetProperty("modified").GetString(), inv, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime modified) && modified == File.GetLastWriteTime(Workbook),
              "the record names the data file, its path and when it was last written");
        JsonElement[] inputs = r.GetProperty("inputs").EnumerateArray().ToArray();
        Check(inputs.Select(i => i.GetProperty("name").GetString()).SequenceEqual(new[] { "data", "gamma", "doAgreement" })
              && inputs.Select(i => i.GetProperty("kind").GetString()).SequenceEqual(new[] { "frame", "confidence", "boolean" }),
              "the inputs are listed in the order they were asked, with their kinds: " + string.Join(", ", inputs.Select(i => i.GetProperty("name").GetString() + ":" + i.GetProperty("kind").GetString())));
        Check(inputs[0].GetProperty("prompt").GetString() == "Select 2 matched columns (or 1 of differences)", "an input carries the prompt the user saw");
        JsonElement frame = inputs[0];
        JsonElement[] columns = frame.GetProperty("columns").EnumerateArray().ToArray();
        Check(frame.GetProperty("mode").GetString() == "NumericReplaceMissing" && frame.GetProperty("file").GetString() == "test.xlsx" && frame.GetProperty("path").GetString() == Workbook
              && frame.GetProperty("sheet").GetString() == "Agreement" && columns.Length == 2,
              "a frame read from a sheet is recorded as pointers: the file, the sheet and its columns");
        Check(columns[0].GetProperty("title").GetString() == "1st" && columns[0].GetProperty("column").GetInt32() == 1 && columns[1].GetProperty("column").GetInt32() == 2
              && columns[0].GetProperty("rows").EnumerateArray().Select(v => v.GetInt32()).SequenceEqual(new[] { 1, 21 }) && columns[0].GetProperty("hasTitle").GetBoolean(),
              "a column's pointer gives its title, its 1-based column number, the rows of the selection and whether the title was in it");
        Check(columns[0].GetProperty("hash").GetString() == DefinedHash(first.Select(v => v.ToString("R", inv)))
              && columns[1].GetProperty("hash").GetString() == DefinedHash(second.Select(v => v.ToString("R", inv))),
              "a column's hash is SHA-256 of its values as text joined by line feeds, the first 16 hex characters");
        Check(!columns[0].TryGetProperty("values", out _), "a pointer column carries no values of its own");
        Check(inputs[1].GetProperty("value").GetDouble() == 0.95 && inputs[2].GetProperty("value").GetBoolean() == false, "the scalar inputs carry their values, the confidence level as a proportion");
        JsonElement parameters = r.GetProperty("parameters");
        Check(parameters.GetProperty("gamma").GetDouble() == 0.95 && parameters.GetProperty("doAgreement").GetBoolean() == false && !parameters.TryGetProperty("data", out _),
              "the settings hold every scalar input and no frame");
        JsonElement values = r.GetProperty("values");
        double t = PairedT(first, second);
        Check(values.TryGetProperty("t", out JsonElement tValue) && Math.Abs(tValue.GetDouble() - t) < 1e-9, $"the results hold the engine's scalar outputs: t = {tValue} (from the definition {t.ToString("R", inv)})");
        Check(!values.TryGetProperty("data", out _) && !values.EnumerateObject().Any(p => p.Name.StartsWith("statsdirect")), "the results hold no frame and nothing of the program's own bookkeeping");
        JsonElement embedded = r.GetProperty("data").GetProperty("data");
        Check(embedded.GetProperty("1st").EnumerateArray().Select(v => v.GetDouble()).SequenceEqual(first) && embedded.GetProperty("2nd").EnumerateArray().Select(v => v.GetDouble()).SequenceEqual(second),
              "the values of a small run are kept with the record, by input and column title");
    }

    // The threshold: a run with more than 5,000 values in its pointer frames keeps none of them; a frame typed in keeps its values whatever its size
    private static void CheckThreshold()
    {
        double[] x = Enumerable.Range(0, 2600).Select(i => 100 + Math.Sin(i) * 10).ToArray(), y = Enumerable.Range(0, 2600).Select(i => 101 + Math.Cos(i) * 10).ToArray();
        var (run, _) = Run("TPaired", new() { ["data"] = SheetFrame(Workbook, "Big", 3, 0, ("x", x), ("y", y)), ["gamma"] = 0.9, ["doAgreement"] = false });
        JsonElement r = Record(run);
        Check(!r.TryGetProperty("data", out _) && r.GetProperty("inputs")[0].GetProperty("columns")[1].GetProperty("column").GetInt32() == 5,
              "a run of 5,200 values keeps pointers only: " + run.Record.Length + " characters of record");
        Check(run.Record.Length < 2000, "the record of a run with 5,200 values is under 2,000 characters");
        DataFrame entered = new();
        entered.Variables.Add(Column("x", x));
        entered.Variables.Add(Column("y", y));
        (run, _) = Run("TPaired", new() { ["data"] = entered, ["gamma"] = 0.9, ["doAgreement"] = false });
        r = Record(run);
        JsonElement input = r.GetProperty("inputs")[0];
        Check(input.GetProperty("kind").GetString() == "entered" && input.GetProperty("columns")[0].GetProperty("values").GetArrayLength() == 2600
              && input.GetProperty("columns")[1].GetProperty("values")[0].GetDouble() == y[0] && !r.TryGetProperty("source", out _),
              "a frame typed in, with no sheet to point to, keeps its values whatever their number, and names no data file");
    }

    // The hash and the text of values: missing values, text, dates and flags, as the two versions must agree on them
    private static void CheckHash()
    {
        DoubleVariable numbers = new(new[] { 1.0, StatsDirect.Numerics.Constant.MISSING, 2.5, 1.0 / 3 }, "n");
        Check(RunRecord.Hash(numbers) == DefinedHash(new[] { "1", "", "2.5", (1.0 / 3).ToString("R", inv) }), "a missing number hashes as nothing and a number in round-trip form");
        StringVariable texts = new(new[] { "a b", "", "é" }, "s");
        Check(RunRecord.Hash(texts) == DefinedHash(new[] { "a b", "", "é" }), "text hashes as it is");
        Check(RunRecord.Text(true) == "TRUE" && RunRecord.Text(new DateTime(2026, 10, 10, 9, 30, 0)) == "2026-10-10T09:30:00.0000000" && RunRecord.Text(null) == "",
              "flags hash as TRUE or FALSE, dates in ISO 8601, nothing as nothing");
        object[] values = RunRecord.Values(numbers);
        Check(values[1] == null && (double)values[0] == 1 && values.Length == 4, "the values of a column give null for a missing number");
    }
}
