using System.Text.Json;
using StatsDirect.Data;
using StatsDirect.R;
using StatsDirect.Templates;

internal static partial class Program
{
    // Real runs through the engine, not records built by hand: the help's example goes through the operation's own prompts, the
    // report step leaves the record, the writer writes the script, and its figures must be the help's, as for the built records.
    // These are the operations whose real runs the Mac found to differ from the help, to tell a recipe matter from a Mac one.
    private static void CheckRealRuns()
    {
        RRecipes.Folder = Metadata("Recipes");
        string examples = Metadata("Examples");
        string folder = Path.Combine(Path.GetTempPath(), "statsdirect-rscript-checks", "real");
        Directory.CreateDirectory(folder);
        // the answers beyond the example's inputs and settings: the prompts the dialog would show; null skips an optional frame
        // mode: "" the example's own (exact, cut or subset), or "subset" where the dialog's labels differ from the help's (the 2 by 2
        // typed in has Present/Absent, where the help names the cholesterol levels, which carry figures)
        (string op, Dictionary<string, object> extra, string mode)[] runs =
        {
            ("MiscRelRisk", new() { ["scrap"] = new double[] { 72, 20, 684, 553 }, ["pe"] = 0.5, ["cco"] = 0.95 }, "subset"),
            ("SizePaired", new() { ["d"] = 20.0, ["sd"] = 35.0, ["p"] = 0.8, ["a"] = 0.05 }, ""),
            ("SizeUnPaired", new() { ["d"] = 5.0, ["sd"] = 12.0, ["m"] = 1.0, ["p"] = 0.9, ["a"] = 0.05 }, ""),
            // (Crosstabs and LogRank are not run here: their classifiers and group identifiers are built by the worksheet's data
            // acquisition, which these checks do not have)
        };
        List<string> failures = new();
        foreach ((string op, Dictionary<string, object> extra, string mode) in runs)
        {
            try
            {
                JsonElement example = JsonDocument.Parse(File.ReadAllText(Path.Combine(examples, op + ".json"))).RootElement;
                Dictionary<string, object> answers = new(extra);
                if (example.TryGetProperty("inputs", out JsonElement inputs))
                    foreach (JsonElement input in inputs.EnumerateArray())
                    {
                        DataFrame frame = new();
                        foreach (JsonElement c in input.GetProperty("columns").EnumerateArray())
                            frame.Variables.Add(Column(c.GetProperty("title").GetString(), c.GetProperty("values").EnumerateArray().Select(v => v.GetDouble()).ToArray()));
                        answers[input.GetProperty("name").GetString()] = frame;
                    }
                if (example.TryGetProperty("settings", out JsonElement settings))
                    foreach (JsonProperty p in settings.EnumerateObject())
                        if (!answers.ContainsKey(p.Name))
                            answers[p.Name] = p.Value.ValueKind switch { JsonValueKind.Number => p.Value.GetDouble(), JsonValueKind.String => p.Value.GetString(), JsonValueKind.True => true, JsonValueKind.False => false, _ => null };
                var (run, _) = Run(op, answers);
                RScript script = RScriptWriter.Write(Record(run), null, null);
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
    }
}
