using System.Text.Json;
using StatsDirect.R;

internal static partial class Program
{
    // The Distributions calculator's recipes have two branches: the probabilities of the value, which the comparison with the help's
    // script covers, and the value of a probability (the calculator's Invert), covered here: each inverse the help illustrates is
    // asked of the recipe through a record, and every figure the recipe prints must be among the figures the help's script prints
    private static void CheckInverseRecipes()
    {
        RRecipes.Folder = Metadata("Recipes");
        string examples = Metadata("Examples");
        string folder = Path.Combine(Path.GetTempPath(), "statsdirect-rscript-checks", "inverse");
        Directory.CreateDirectory(folder);
        (string op, string settings)[] cases =
        {
            ("DistributionNormal", "{\"upper\":0.025,\"asked\":\"invert-upper\"}"),
            ("DistributionNormal", "{\"lower\":0.975,\"asked\":\"invert-lower\"}"),
            ("DistributionNormal", "{\"two_sided\":0.05,\"asked\":\"invert-two-sided\"}"),
            ("DistributionT", "{\"df\":10,\"upper\":0.025,\"asked\":\"invert-upper\"}"),
            ("DistributionT", "{\"df\":10,\"lower\":0.975,\"asked\":\"invert-lower\"}"),
            ("DistributionChiSquare", "{\"df\":3,\"upper\":0.01,\"asked\":\"invert-upper\"}"),
            ("DistributionF", "{\"df\":6,\"df2\":8,\"upper\":0.05,\"asked\":\"invert-upper\"}"),
            ("DistributionQ", "{\"df\":36,\"df2\":4,\"upper\":0.05,\"asked\":\"invert-upper\"}"),
            ("DistributionNonCentralT", "{\"df\":10,\"df2\":1,\"upper\":0.1,\"asked\":\"invert-upper\"}"),
            ("DistributionPoisson", "{\"df\":14,\"upper\":0.025,\"asked\":\"invert-upper\"}"),
            ("DistributionPoisson", "{\"df\":14,\"two_sided\":0.025,\"asked\":\"invert-two-sided\"}"),
            // the rank correlation calculators: the help works from two rankings of 10 (S = 23, T = 52), so their forward branch is checked here too
            ("DistributionKendall", "{\"df\":10,\"df2\":23,\"asked\":\"calculate\"}"),
            ("DistributionKendall", "{\"df\":10,\"upper\":0.05,\"asked\":\"invert-upper\"}"),
            ("DistributionSpearman", "{\"df\":10,\"df2\":52,\"asked\":\"calculate\"}"),
            ("DistributionSpearman", "{\"df\":10,\"upper\":0.025,\"asked\":\"invert-upper\"}"),
        };
        Dictionary<string, HashSet<string>> help = new();
        List<string> failures = new();
        int i = 0;
        foreach ((string op, string settings) in cases)
        {
            if (!help.TryGetValue(op, out HashSet<string> figures))
                help[op] = figures = Figures(RunR(File.ReadAllText(Path.Combine(examples, op + ".R")).Replace("\r", ""), Path.Combine(folder, op + "-help"))).Split(' ').ToHashSet();
            string record = "{\"version\":1,\"operation\":\"" + op + "\",\"title\":\"Example\",\"run\":\"2026-10-11T03:00:00+01:00\",\"inputs\":[],\"parameters\":" + settings + ",\"values\":{}}";
            RScript script = RScriptWriter.Write(JsonDocument.Parse(record).RootElement, null, null);
            string output = RunR(script.Text, Path.Combine(folder, op + "-" + (++i)));
            string[] printed = Figures(output).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string[] missing = printed.Where(f => !figures.Contains(f)).ToArray();
            Console.WriteLine($"      {op} {settings}: {output.Trim().Replace("\n", " | ")}");
            if (printed.Length == 0 || missing.Length > 0) failures.Add($"{op} {settings}: " + (printed.Length == 0 ? "printed no figure" : "not in the help's output: " + string.Join(", ", missing)));
        }
        foreach (string f in failures) Console.WriteLine("      " + f);
        Check(failures.Count == 0, $"every inverse a distribution recipe gives prints figures the help's script prints: {cases.Length - failures.Count} of {cases.Length} cases");
    }
}
