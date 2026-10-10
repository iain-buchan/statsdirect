using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using StatsDirect.R;

internal static partial class Program
{
    // The recipes against the definitions of the operations they are mapped to: every operation named exists, every setting a recipe
    // takes is a parameter of each operation it serves, and the data it takes are of the kind the operation asks for
    private static void CheckRecipes()
    {
        RRecipes.Folder = Metadata("Recipes");
        string[] operations = RRecipes.Operations.ToArray();
        Check(operations.Length >= 28 && operations.All(op => File.Exists(Path.Combine(Metadata("Operations"), op + ".xml"))), $"every operation of the manifest ({operations.Length}) has a definition here");
        List<string> mismatches = new();
        foreach (string op in operations)
        {
            RRecipe recipe = RRecipes.Find(op);
            string text = RRecipes.Recipe(recipe.File);
            string xml = File.ReadAllText(Path.Combine(Metadata("Operations"), op + ".xml"));
            HashSet<string> parameters = Regex.Matches(xml, "<name>([^<]+)</name>").Select(m => m.Groups[1].Value.Trim()).Skip(1).ToHashSet();
            parameters.UnionWith(Regex.Matches(xml, "<(?:top-left|top-right|bottom-left|bottom-right)-name>([^<]+)</").Select(m => m.Groups[1].Value.Trim()));   // the cells of a 2 by 2 table typed in
            if (Regex.IsMatch(xml, "<builtin function-name=\"dist[A-Za-z]+\""))
                parameters.UnionWith(new[] { "value", "df", "df2", "lower", "upper", "two_sided", "asked" });   // the Distributions calculator's boxes, which it records itself
            Match settings = Regex.Match(text, "^# settings:\\s*(.*)$", RegexOptions.Multiline);
            if (settings.Success)
                foreach (string item in settings.Groups[1].Value.Split(',').Select(x => x.Trim()).Where(x => x.Contains('=')))
                {
                    string parameter = item[(item.IndexOf('=') + 1)..].Trim();
                    if (!parameters.Contains(parameter)) mismatches.Add($"{op}: {recipe.File} takes the setting \"{parameter}\"");
                }
            if (Regex.IsMatch(text, "^# (data|matrix):", RegexOptions.Multiline) && !xml.Contains("<frame>")) mismatches.Add($"{op}: {recipe.File} takes columns, and the operation asks for no frame");
            if (Regex.IsMatch(text, "^# table:", RegexOptions.Multiline) && !xml.Contains("<double-2-by-2>")) mismatches.Add($"{op}: {recipe.File} takes a 2 by 2 table, and the operation asks for none");
            if (!Regex.IsMatch(text, "^# (data|table|matrix|settings):", RegexOptions.Multiline)) mismatches.Add($"{op}: {recipe.File} has no header");
        }
        mismatches = mismatches.Distinct().ToList();
        foreach (string mismatch in mismatches) Console.WriteLine("      mismatch: " + mismatch);
        Check(mismatches.Count == 0, "every setting and kind of data a recipe takes is what the operations it serves ask for");
    }

    /// <summary>The installed R's Rscript, as the program finds it.</summary>
    private static string Rscript()
    {
        RVersion r = RController.CheckR().OrderByDescending(v => v.MajorVersion).ThenByDescending(v => v.MinorVersion).ThenByDescending(v => v.Revision).FirstOrDefault()
                     ?? throw new Exception("FAIL R is not installed on this PC, so the scripts cannot be run");
        return Path.Combine(r.BinPath, "Rscript.exe");
    }

    private static string RunR(string script, string folder)
    {
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "script.R");
        File.WriteAllText(path, script);
        ProcessStartInfo start = new(Rscript(), $"--vanilla --encoding=UTF-8 \"{path}\"") { WorkingDirectory = folder, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        using Process p = Process.Start(start);
        Task<string> errors = p.StandardError.ReadToEndAsync();
        string output = p.StandardOutput.ReadToEnd();
        p.WaitForExit();
        if (p.ExitCode != 0) throw new Exception($"FAIL R exited with code {p.ExitCode}:\n{output}\n{errors.Result}");
        return output;
    }

    private static double Figure(string output, string pattern) => double.Parse(Regex.Match(output, pattern).Groups[1].Value, inv);
    private static bool Close(double a, double b, double relative) => Math.Abs(a - b) <= relative * Math.Max(Math.Abs(a), Math.Abs(b)) + 1e-12;

    // The script of a result, and R run on it: a short script of data, settings and the analysis; the recipe of the paired t test
    // reproduces the engine's figures
    private static void CheckScript()
    {
        RRecipes.Folder = Metadata("Recipes");
        var (run, _) = Run("TPaired", new() { ["data"] = SheetFrame(Workbook, "Agreement", 0, 0, ("1st", first), ("2nd", second)), ["gamma"] = 0.95, ["doAgreement"] = false });
        JsonElement record = Record(run);
        RScript script = RScriptWriter.Write(record, "Paired t test of peak flow", null);   // the values are kept with the record, so nothing is read
        Check(script.HasRecipe && script.Detail.StartsWith("Paired t test") && !script.DataUnavailable, "the paired t test has a recipe, and the data of a small run come from the record");
        string text = script.Text;
        Console.WriteLine("      script head: " + text.Replace("\n", " | ").Substring(0, Math.Min(300, text.Length)));
        Check(text.StartsWith("# Paired t test of peak flow, from StatsDirect, ") && text.Contains("# Data: \"test.xlsx\", sheet \"Agreement\", the values the analysis used."),
              "the script's head names the analysis and where its data came from");
        Check(text.Contains("before <- c(190, 220, 260, 210") && text.Contains("   # 1st, column 1, rows 1 to 21\n") && text.Contains("after <- c(220, 200, 260, 300") && text.Contains("confidence <- 0.95\n"),
              "the data are plain named vectors with the column's title and place beside them, and the settings plain values");
        Check(!text.Contains("setNames") && !text.Contains("data_frames") && !text.Contains("sd_columns") && !text.Contains("original_results") && !text.Contains("input_history"), "nothing of the program's machinery is in the script");
        Check(text.Contains("t.test(before, after, paired = TRUE, conf.level = confidence)"), "the paired recipe follows, taking the confidence level from the settings");
        string folder = Path.Combine(Path.GetTempPath(), "statsdirect-rscript-checks");
        string output = RunR(text, folder);
        double t = Figure(output, "t = ([-0-9.eE+]+), df = 19"), p = Figure(output, "p-value = ([0-9.eE-]+)");
        Match ci = Regex.Match(output, "percent confidence interval:\\s*\\n\\s*([-0-9.eE+]+)\\s+([-0-9.eE+]+)");
        double from = double.Parse(ci.Groups[1].Value, inv), to = double.Parse(ci.Groups[2].Value, inv);
        JsonElement values = record.GetProperty("values");
        Console.WriteLine($"      R: t = {t}, p = {p}, CI {from} to {to}; engine: t = {values.GetProperty("t").GetDouble()}, tail_2 = {values.GetProperty("tail_2").GetDouble()}, {values.GetProperty("from").GetDouble()} to {values.GetProperty("to").GetDouble()}");
        Check(Close(t, values.GetProperty("t").GetDouble(), 1e-4) && Close(p, values.GetProperty("tail_2").GetDouble(), 1e-3), "R's paired t statistic and two-sided P agree with the engine's to the figures R prints");
        Check(Close(from, values.GetProperty("from").GetDouble(), 1e-4) && Close(to, values.GetProperty("to").GetDouble(), 1e-4), "R's confidence interval of the mean difference agrees with the engine's");
        // a run with too many values to keep: the data are read again by the pointers, and a changed column is noted
        double[] x = Enumerable.Range(0, 2600).Select(i => 100 + Math.Sin(i) * 10).ToArray(), y = Enumerable.Range(0, 2600).Select(i => 101 + Math.Cos(i) * 10).ToArray();
        (run, _) = Run("TPaired", new() { ["data"] = SheetFrame(Workbook, "Big", 3, 0, ("x", x), ("y", y)), ["gamma"] = 0.95, ["doAgreement"] = false });
        record = Record(run);
        FramePointer asked = null;
        FrameValues Read(FramePointer pointer)
        {
            asked = pointer;
            FrameValues read = new();
            read.Columns.Add(new ColumnValues { Title = "x", Values = x.Cast<object>().ToArray() });
            read.Columns.Add(new ColumnValues { Title = "y", Values = y.Cast<object>().ToArray(), Changed = true });
            return read;
        }
        script = RScriptWriter.Write(record, null, Read);
        Check(asked != null && asked.Name == "data" && asked.File == "test.xlsx" && asked.Sheet == "Big" && asked.Columns.Count == 2 && asked.Columns[0].Column == 4 && asked.Columns[1].Column == 5
              && asked.Columns[0].FirstRow == 1 && asked.Columns[0].LastRow == 2601 && asked.Columns[0].Hash.Length == 16 && asked.Mode == "NumericReplaceMissing",
              "a large run's data are asked for by the pointers of the record: file, sheet, columns, rows, mode and hash");
        Check(script.Text.Contains("# NOTE: \"y\" has changed in the workbook since the analysis ran; these are its values now.") && !script.Text.Contains("\"x\" has changed")
              && Regex.IsMatch(script.Text, "# Data: \"test.xlsx\", sheet \"Big\", read on \\d+ \\w+ 20\\d\\d \\d\\d:\\d\\d\\."),
              "a column whose hash no longer matches is noted, and the head says where and when the data were read");
        Check(Regex.IsMatch(script.Text, "^before <- c\\(100, 108\\.414709848078\\d*, ", RegexOptions.Multiline) && script.Text.Contains("   # x, column 4, rows 1 to 2601\n"), "the values read are written into the script under the recipe's names");
        script = RScriptWriter.Write(record, null, pointer => new FrameValues { Unavailable = "the workbook test.xlsx is not open and was not found" });
        Check(script.DataUnavailable && script.Text.Contains("before <- NULL   # x, column 4, rows 1 to 2601: not read, see above") && script.Text.Contains("could not be read (the workbook test.xlsx is not open and was not found): put the values in by hand."),
              "when the workbook cannot be read the script says so and leaves the vectors for the user");
        // an operation with no recipe: the data and settings alone, under names made from the titles
        JsonElement bare = JsonDocument.Parse("{\"version\":1,\"operation\":\"NoSuchMethod\",\"title\":\"Some method\",\"run\":\"2026-10-10T22:00:00+01:00\",\"inputs\":[{\"name\":\"data\",\"prompt\":\"Select data\",\"kind\":\"entered\",\"columns\":[{\"title\":\"Peak flow (1st)\",\"values\":[1,null,3]}]},{\"name\":\"k\",\"prompt\":\"Groups\",\"kind\":\"integer\",\"value\":2}],\"parameters\":{\"k\":2,\"label\":\"x \\\"y\\\"\"},\"values\":{\"stat\":1.5}}").RootElement;
        script = RScriptWriter.Write(bare, null, null);
        output = RunR(script.Text, folder);
        Check(!script.HasRecipe && script.Text.Contains("peak_flow_1st <- c(1, NA, 3)   # Peak flow (1st)\n") && script.Text.Contains("k <- 2   # k\n") && script.Text.Contains("label <- \"x \\\"y\\\"\"   # label\n") && script.Text.TrimEnd().EndsWith("# Add your analysis here.") && output.Trim().Length == 0,
              "without a recipe the script holds the data and settings under safe names, escapes text, and runs in R");
    }
}
