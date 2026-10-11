using System.Text.Json;
using System.Text.Json.Nodes;
using StatsDirect.Data;
using StatsDirect.R;
using StatsDirect.Templates;

internal static partial class Program
{
    // Real runs through the engine, not records built by hand: the help's example goes through the operation's own prompts, the
    // report step leaves the record, the writer writes the script, and its figures must be the help's, as for the built records.
    // These are the operations whose real runs the Mac found to differ from the help, to tell a recipe matter from a Mac one.  The
    // frames come from a sheet, so that the record holds pointers and the values the analysis used, as a run from the worksheet does.
    private static void CheckRealRuns()
    {
        RRecipes.Folder = Metadata("Recipes");
        string examples = Metadata("Examples");
        string folder = Path.Combine(Path.GetTempPath(), "statsdirect-rscript-checks", "real");
        Directory.CreateDirectory(folder);
        // the answers beyond the example's inputs and settings: the prompts the dialog would show; null skips an optional frame
        // mode: "" the example's own (exact, cut or subset), or "subset" where the dialog's labels differ from the help's (the 2 by 2
        // typed in has Present/Absent, where the help names the cholesterol levels, which carry figures)
        // repeats: inputs the operation asks for once per pass of a loop, one of the example's columns for each pass
        (string op, Dictionary<string, object> extra, string mode, string[] repeats)[] runs =
        {
            ("MiscRelRisk", new() { ["scrap"] = new double[] { 72, 20, 684, 553 }, ["pe"] = null, ["cco"] = 0.95 }, "subset", null),   // no population proportion exposed, as the help's example
            ("SizePaired", new() { ["d"] = 20.0, ["sd"] = 35.0, ["p"] = 0.8, ["a"] = 0.05 }, "", null),
            ("SizeUnPaired", new() { ["d"] = 5.0, ["sd"] = 12.0, ["m"] = 1.0, ["p"] = 0.9, ["a"] = 0.05 }, "", null),
            // the Mac's harness ran every example through its engine (11 Oct 2026) and found these: the kappa grid's parameter is
            // responsesCrosstab; the logistic regression's rest list took the grid the regression leaves in the bag; the survival plot's
            // and the Wei-Lachin test's frames are asked once per group or repeat, and the record kept the last ask only
            ("KappaScreen", new() { ["method"] = "1", ["doExactP"] = false, ["iterations"] = 1000, ["seed"] = 1 }, "", null),   // the dialog asks for the iterations and seed whatever doExactP says
            ("LogisticRegression", new() { ["grouping"] = "grouped", ["accuracy"] = "0.0000001", ["intercept"] = true, ["weights"] = false, ["gamma"] = 0.95 }, "", null),
            ("SurvivalPlot", new() { ["group-count"] = 2, ["ydatlNew"] = null, ["ydatuNew"] = null }, "", new[] { "xdatNew", "cdatNew" }),
            ("WeiLachin", new() { ["nr"] = 4 }, "", new[] { "scrapTimes", "scrapCensor" }),
            // (Crosstabs and LogRank are not run here: their classifiers and group identifiers are built by the worksheet's data
            // acquisition, which these checks do not have)
        };
        List<string> failures = new();
        foreach ((string op, Dictionary<string, object> extra, string mode, string[] repeats) in runs)
        {
            try
            {
                JsonElement example = JsonDocument.Parse(File.ReadAllText(Path.Combine(examples, op + ".json"))).RootElement;
                Dictionary<string, object> answers = new(extra);
                int column = 1;
                DataFrame Sheet(params (string title, double[] values)[] columns) { DataFrame f = SheetFrame("test.xlsx", op, column, 1, columns); column += columns.Length; return f; }
                (string title, double[] values) Column(JsonElement c) => (c.GetProperty("title").GetString(), c.GetProperty("values").EnumerateArray().Select(v => v.GetDouble()).ToArray());
                if (example.TryGetProperty("inputs", out JsonElement inputs))
                    foreach (JsonElement input in inputs.EnumerateArray())
                    {
                        string name = input.GetProperty("name").GetString();
                        (string title, double[] values)[] columns = input.GetProperty("columns").EnumerateArray().Select(Column).ToArray();
                        if (repeats != null && repeats.Contains(name))
                        {
                            answers[name] = new Queue<object>(columns.Select(c => (object)Sheet(c)));   // one column for each pass of the loop
                            if (op == "SurvivalPlot" && name == "xdatNew")   // the survival proportion the plot also asks for each group: not the script's business, so any values of the length
                                answers["ydatNew"] = new Queue<object>(columns.Select(c => (object)Sheet(("S", Enumerable.Repeat(1.0, c.values.Length).ToArray()))));
                        }
                        else if (op == "WeiLachin" && name == "gid")
                        {
                            // a group identifier as the worksheet's acquisition builds it: a classifier with the groups' codes 0, 1 ... and labels
                            double[] codes = columns[0].values;
                            double[] levels = codes.Distinct().OrderBy(v => v).ToArray();
                            ClassifierVariable classifier = new() { Title = columns[0].title };
                            classifier.EnsureLength(codes.Length);
                            for (int i = 0; i < codes.Length; i++) classifier.SetData(i, Array.IndexOf(levels, codes[i]));
                            foreach (double level in levels) classifier.Groups.Add(new Group(level.ToString(inv), Array.IndexOf(levels, level)) { NBin = codes.Count(v => v == level) });
                            DataFrame groups = new();
                            groups.Variables.Add(classifier);
                            answers[name] = groups;
                        }
                        else
                            answers[name] = Sheet(columns);
                    }
                if (example.TryGetProperty("settings", out JsonElement settings))
                    foreach (JsonProperty p in settings.EnumerateObject())
                        if (!answers.ContainsKey(p.Name))
                            answers[p.Name] = p.Value.ValueKind switch { JsonValueKind.Number => p.Value.GetDouble(), JsonValueKind.String => p.Value.GetString(), JsonValueKind.True => true, JsonValueKind.False => false, _ => null };
                var (run, _) = Run(op, answers);
                RScript script = RScriptWriter.Write(Record(run), null, null);
                if (!script.HasRecipe) { failures.Add(op + ": the run's script has no recipe"); continue; }
                string ours = Figures(RunR(script.Text, Path.Combine(folder, op)));
                string expected = Figures(RunR(File.ReadAllText(Path.Combine(examples, op + ".R")).Replace("\r", ""), Path.Combine(folder, op + "-help")));
                bool cut = example.TryGetProperty("cut", out JsonElement c2) && c2.ValueKind == JsonValueKind.True;
                bool subset = mode == "subset" || (example.TryGetProperty("subset", out JsonElement sb) && sb.ValueKind == JsonValueKind.True);
                HashSet<string> helpFigures = expected.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
                bool ok = ours == expected || (cut && ours.Length > 0 && expected.StartsWith(ours + " ", StringComparison.Ordinal)) || (subset && ours.Length > 0 && ours.Split(' ').All(helpFigures.Contains));
                if (!ok)
                {
                    string[] a = ours.Split(' '), b = expected.Split(' ');
                    int i = 0;
                    while (i < a.Length && i < b.Length && a[i] == b[i]) i++;
                    failures.Add($"{op}: differs from the {i + 1}th figure: run {(i < a.Length ? a[i] : "(end)")}, help {(i < b.Length ? b[i] : "(end)")} ({a.Length} and {b.Length} figures)");
                }
            }
            catch (Exception ex) { string text = (ex is NullReferenceException ? ex.ToString() : ex.Message).Replace("\r", "").Replace("\n", " | "); failures.Add($"{op}: {text.Substring(0, Math.Min(700, text.Length))}"); }
        }
        foreach (string f in failures) Console.WriteLine("      " + f);
        Check(failures.Count == 0, $"a real run through the engine gives the help's figures for each of the {runs.Length} operations the Mac found different ({runs.Length - failures.Count} agree)");
        // Crosstabs reads its classifiers as categories, so a real record holds "0" and "1" as text where the help's example has numbers:
        // the recipe scores them as the help does (the Mac's finding 4; the run itself needs the worksheet's classifiers, so the record is built)
        JsonNode crosstabs = JsonNode.Parse(File.ReadAllText(Path.Combine(examples, "Crosstabs.json")));
        foreach (JsonNode input in crosstabs["inputs"].AsArray())
            foreach (JsonNode col in input["columns"].AsArray())
            {
                JsonArray values = col["values"].AsArray();
                for (int i = 0; i < values.Count; i++) values[i] = JsonValue.Create(values[i].GetValue<double>().ToString(inv));
            }
        RScript textual = RScriptWriter.Write(JsonDocument.Parse(RecordOf(JsonDocument.Parse(crosstabs.ToJsonString()).RootElement)).RootElement, null, null);
        string textFigures = Figures(RunR(textual.Text, Path.Combine(folder, "Crosstabs-text")));
        string helpFiguresOf = Figures(RunR(File.ReadAllText(Path.Combine(examples, "Crosstabs.R")).Replace("\r", ""), Path.Combine(folder, "Crosstabs-help")));
        Check(textual.Text.Contains("sex <- c(\"") && textFigures == helpFiguresOf, "the crosstabs recipe on classifiers recorded as text gives the help's figures");
    }
}
