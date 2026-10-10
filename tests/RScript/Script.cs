using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using StatsDirect.R;

internal static partial class Program
{
    // The R recipes shared with the Mac version, against the definitions of the operations they are mapped to: every operation named
    // exists, and every input or setting a recipe must have (a column set it reads by name with no alternative, a setting it reads with
    // no default) is a parameter of each operation it serves.  A setting read with a default is optional by design.
    private static void CheckRecipes()
    {
        RRecipes.Folder = Metadata("Recipes");
        string[] operations = RRecipes.Operations.ToArray();
        Check(operations.Length >= 38 && operations.All(op => File.Exists(Path.Combine(Metadata("Operations"), op + ".xml"))), $"every operation of the manifest ({operations.Length}) has a definition here");
        Check(RRecipes.Helpers.Contains("sd_columns <- function") && RRecipes.Helpers.Contains("confidence <- sd_parameter"), "helpers.R defines the readers the recipes use");
        List<string> mismatches = new();
        foreach (string op in operations)
        {
            RRecipe recipe = RRecipes.Find(op);
            string text = RRecipes.Recipe(recipe.File);
            HashSet<string> parameters = Regex.Matches(File.ReadAllText(Path.Combine(Metadata("Operations"), op + ".xml")), "<name>([^<]+)</name>").Select(m => m.Groups[1].Value.Trim()).Skip(1).ToHashSet();
            // the column sets a recipe reads: required when it reads only one
            string[] columnSets = Regex.Matches(text, "sd_columns\\(\\s*(?:\"([^\"]+)\")?\\s*\\)").Select(m => m.Groups[1].Success ? m.Groups[1].Value : "data").Distinct().ToArray();
            if (columnSets.Length == 1 && !parameters.Contains(columnSets[0])) mismatches.Add($"{op}: {recipe.File} reads the columns \"{columnSets[0]}\"");
            if (columnSets.Length > 1 && !columnSets.Any(parameters.Contains)) mismatches.Add($"{op}: {recipe.File} reads one of the column sets {string.Join(", ", columnSets.Select(c => "\"" + c + "\""))}");
            foreach (Match m in Regex.Matches(text, "sd_parameter\\(\\s*\"([^\"]+)\"\\s*\\)"))
                if (!parameters.Contains(m.Groups[1].Value)) mismatches.Add($"{op}: {recipe.File} reads the setting \"{m.Groups[1].Value}\" with no default");
        }
        mismatches = mismatches.Distinct().ToList();
        foreach (string mismatch in mismatches) Console.WriteLine("      mismatch: " + mismatch);
        Check(mismatches.Count == 0, "every column set and setting a recipe must have is a parameter of the operations it serves");
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

    // The script of a result, and R run on it: the recipe of the paired t test reproduces the engine's figures
    private static void CheckScript()
    {
        RRecipes.Folder = Metadata("Recipes");
        var (run, _) = Run("TPaired", new() { ["data"] = SheetFrame(Workbook, "Agreement", 0, 0, ("1st", first), ("2nd", second)), ["gamma"] = 0.95, ["doAgreement"] = false });
        JsonElement record = Record(run);
        RScript script = RScriptWriter.Write(record, "Paired t test of peak flow", null);   // the values are kept with the record, so nothing is read
        Check(script.HasRecipe && script.Detail.StartsWith("Paired t test") && !script.DataUnavailable, "the paired t test has a recipe, and the data of a small run come from the record");
        string text = script.Text;
        Check(text.Contains("operation <- \"TPaired\"") && text.Contains("analysis_title <- \"Paired t test of peak flow\"") && Regex.IsMatch(text, "parameters <- setNames\\(list\\([^)]*0\\.95") && text.Contains("\"gamma\""),
              "the script names the operation and the title and holds the settings");
        Check(text.Contains("data_frames[[\"data\"]] <- setNames(list(c(190, 220, 260") && text.Contains("), c(\"1st\", \"2nd\"))"), "the data frame is written by input name with its columns by title");
        Check(Regex.IsMatch(text, "input_history <- list\\(setNames\\(list\\(\"data\",\\s+\"Select 2 matched columns") && text.Contains("\"data_frame\""), "the history of the inputs refers to the data frame");
        Check(text.Contains("sd_columns <- function") && text.Contains("t.test(x, y, paired = TRUE, conf.level = confidence)"), "the helpers and the paired recipe follow");
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
        Check(script.Text.Contains("# NOTE: the values of \"y\" are not those the analysis used") && !script.Text.Contains("the values of \"x\" are not") && script.Text.Contains("was read from \"test.xlsx\", sheet \"Big\", columns 4 to 5, rows 1 to 2601"),
              "a column whose hash no longer matches is noted, and the head says where the data were read from");
        Check(Regex.IsMatch(script.Text, "data_frames\\[\\[\"data\"\\]\\] <- setNames\\(list\\(c\\(100, 108\\.414709848078\\d*, "), "the values read are written into the script");
        script = RScriptWriter.Write(record, null, pointer => new FrameValues { Unavailable = "the workbook test.xlsx is not open and was not found" });
        Check(script.DataUnavailable && script.Text.Contains("data_frames[[\"data\"]] <- NULL") && script.Text.Contains("could not be read: the workbook test.xlsx is not open and was not found. Put its columns (\"x\", \"y\") into data_frames[[\"data\"]] by hand"),
              "when the workbook cannot be read the script says so and leaves the frame for the user");
        // an operation with no recipe: the data and settings alone
        JsonElement bare = JsonDocument.Parse("{\"version\":1,\"operation\":\"NoSuchMethod\",\"title\":\"Some method\",\"run\":\"2026-10-10T22:00:00+01:00\",\"inputs\":[{\"name\":\"data\",\"prompt\":\"Select data\",\"kind\":\"entered\",\"columns\":[{\"title\":\"a\",\"values\":[1,null,3]}]},{\"name\":\"k\",\"prompt\":\"Groups\",\"kind\":\"integer\",\"value\":2}],\"parameters\":{\"k\":2,\"label\":\"x \\\"y\\\"\"},\"values\":{\"stat\":1.5}}").RootElement;
        script = RScriptWriter.Write(bare, null, null);
        output = RunR(script.Text, folder);
        string shown = output.Replace("\r", "").Replace("\n", " | ");
        Check(!script.HasRecipe && script.Text.Contains("data_frames[[\"data\"]] <- setNames(list(c(1, NA_real_, 3)), c(\"a\"))") && script.Text.Contains("\"x \\\"y\\\"\"")
              && output.Contains("not yet available") && Regex.IsMatch(output, "\\$ a: num \\[1:3\\] 1 NA 3") && output.Contains("$k") && output.Contains("[1] 2") && output.Contains("[1] \"x \\\"y\\\"\""),
              "without a recipe the script carries the data and settings, escapes text, and runs in R to show them: " + shown.Substring(0, Math.Min(400, shown.Length)));
    }
}
