// The Proportions menu: checks by calculation.  A single proportion, paired proportions and two independent proportions are given
// cases, and every figure is compared with a benchmark worked out in R (in the folder benchmarks, with the scripts that made them).
// In Definitions.cs the figures are worked out here from their definitions for cases drawn at random, and in Limits.cs the reports
// are looked at where the numbers are at the ends of what they can be.
using System.Globalization;
using System.Reflection;
using StatsDirect.Builtins;
using StatsDirect.Templates;

internal static partial class Program
{
    internal const double M = double.MinValue;
    internal static int failures, checks;
    internal static readonly CultureInfo inv = CultureInfo.InvariantCulture;

    internal static void Say(bool ok, string what)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("FAIL  " + what); }
    }

    internal static string Message(Exception ex)
    {
        Exception inner = ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex;
        return inner.GetType().Name + ": " + inner.Message.Replace("\n", " ").Replace("\r", " ");
    }

    private static ParameterBag Bag(params (string name, double value)[] inputs)
    {
        ParameterBag p = new();
        foreach (var (name, value) in inputs) p.AddInput(name, value);
        return p;
    }

    // ---- the reports of the menu
    internal static ParameterBag Single(double n, double r, double pi, double level) => Analysis.RptPropSingle(Bag(("n", n), ("r", r), ("qpi", pi), ("cco", level))).ParameterBag;
    internal static ParameterBag Paired(double n, double r, double s, double t, double level) => Analysis.RptPropPairs(Bag(("n", n), ("r", r), ("s", s), ("t", t), ("cco", level))).ParameterBag;
    internal static ParameterBag Unpaired(double n1, double r1, double n2, double r2, double level) => Analysis.RptPropUnPaired(Bag(("n1", n1), ("r1", r1), ("n2", n2), ("r2", r2), ("cco", level))).ParameterBag;

    internal static List<ParameterBag> Rows(ParameterBag bag, string block) =>
        !bag.ContainsKey(block) || bag[block].AsObject == null ? new List<ParameterBag>() : ((System.Collections.IEnumerable)bag[block].AsObject).Cast<ParameterBag>().ToList();

    // every figure of a report, under the name of the figure; the figures of the rows of a block have the name of the block and the
    // number of the row before them
    private static void Collect(string prefix, ParameterBag bag, Dictionary<string, object> figures)
    {
        foreach (string name in bag.Keys)
        {
            if (name.StartsWith("*"))
            {
                List<ParameterBag> rows = Rows(bag, name);
                for (int i = 0; i < rows.Count; i++) Collect(prefix + "|" + name.Substring(1) + "." + (i + 1).ToString(inv), rows[i], figures);
                continue;
            }
            object value;
            try { value = bag[name].AsObject; } catch { continue; }
            if (value is double or int or string) figures[prefix + "|" + name] = value;
        }
    }

    private static void Report(string key, Func<ParameterBag> report, Dictionary<string, object> figures)
    {
        try
        {
            Collect(key, report(), figures);
            figures[key + "|ok"] = 1;
        }
        catch (Exception ex) { figures[key + "|ok"] = 0; figures[key + "|error"] = Message(ex); }
    }

    // A figure of the program against one that is expected: the difference as a part of what is expected
    internal static double Differs(object given, double expected)
    {
        double x = given switch { double d => d, int i => i, _ => double.NaN };
        bool none = double.IsNaN(expected) || expected == M, noneGiven = double.IsNaN(x) || x == M;
        if (none || noneGiven) return none && noneGiven && given != null ? 0 : double.PositiveInfinity;
        if (x == expected) return 0;
        if (double.IsInfinity(x) || double.IsInfinity(expected)) return double.PositiveInfinity;
        return Math.Abs(x - expected) / Math.Max(Math.Abs(expected), 1e-300);
    }

    private static void Benchmarks(string folder)
    {
        Console.WriteLine("The cases of the benchmarks");
        int before = failures, compared = 0;
        Dictionary<string, object> figures = new();
        System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
        double longest = 0; string slowest = "";
        foreach (string line in File.ReadAllLines(Path.Combine(folder, "cases-prop.txt")))
        {
            string[] f = line.Split('\t');
            if (f.Length < 3) continue;
            double[] v = f.Skip(2).Select(x => double.Parse(x, inv)).ToArray();
            string key = f[0] + "|" + f[1];
            double start = clock.Elapsed.TotalSeconds;
            switch (f[0])
            {
                case "single": Report(key, () => Single(v[0], v[1], v[2], v[3]), figures); break;
                case "paired": Report(key, () => Paired(v[0], v[1], v[2], v[3], v[4]), figures); break;
                case "unpaired": Report(key, () => Unpaired(v[0], v[1], v[2], v[3], v[4]), figures); break;
            }
            double taken = clock.Elapsed.TotalSeconds - start;
            if (taken > longest) { longest = taken; slowest = line.Replace('\t', ' '); }
        }
        Dictionary<string, (int n, int bad, double worst, string what)> kinds = new();
        foreach (string line in File.ReadAllLines(Path.Combine(folder, "r-prop.txt")))
        {
            string[] part = line.Split('\t');
            string[] key = part[0].Split('|');
            string kind = key[0] + "|" + string.Join("|", key.Skip(2).Select(k => System.Text.RegularExpressions.Regex.Replace(k, @"\.\d+$", ".#")));
            bool have = figures.TryGetValue(part[0], out object value);
            double by;
            if (part[1].StartsWith("\"")) by = have && value is string text && "\"" + text + "\"" == part[1] ? 0 : double.PositiveInfinity;
            else
            {
                double expected = part[1] switch { "Inf" => double.PositiveInfinity, "-Inf" => double.NegativeInfinity, "NA" => M, "NaN" => double.NaN, _ => double.Parse(part[1], inv) };
                by = have ? Differs(value, expected) : double.PositiveInfinity;
            }
            compared++;
            var k = kinds.TryGetValue(kind, out var had) ? had : (0, 0, 0.0, "");
            k.Item1++;
            // a figure may differ from its benchmark by one part in a hundred million
            if (by > 1e-8) { k.Item2++; if (by > k.Item3) { k.Item3 = by; k.Item4 = $"{part[0]}: {(have ? value : "not given")}, benchmark {part[1]}"; } }
            kinds[kind] = k;
        }
        foreach (var k in kinds.OrderBy(k => k.Key, StringComparer.Ordinal))
            Say(k.Value.bad == 0, $"{k.Key}: {k.Value.bad} of {k.Value.n} figures differ from their benchmarks; the worst is {k.Value.what}");
        // the time is as the standard deviation of a count, and not as the size of the sample
        Say(longest < 2.0, $"the case that takes longest ({slowest}) takes {longest:F2} seconds");
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  against the benchmarks: {compared} figures compared, in {kinds.Count} kinds; the case that takes longest takes {longest:F2} seconds");
    }

    private static int Main(string[] args)
    {
        string which = args.Length > 0 ? args[0] : "all";
        string folder = Path.Combine(AppContext.BaseDirectory, "benchmarks");
        if (which is "all" or "benchmarks") Benchmarks(folder);
        if (which is "all" or "definitions") Definitions();
        if (which is "all" or "limits") Limits();
        Console.WriteLine();
        Console.WriteLine(failures == 0 ? $"ALL {checks} CHECKS PASS" : $"{failures} OF {checks} CHECKS FAILED");
        return failures == 0 ? 0 : 1;
    }
}
