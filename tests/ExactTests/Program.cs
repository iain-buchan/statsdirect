// The Exact Tests on Counts menu: checks by calculation.  The sign test, Fisher's exact test, the expanded Fisher-Irwin test, the
// test of matched pairs, the exact confidence interval of the odds ratio and the confidence interval of a Poisson rate are given
// cases, and every figure is compared with a benchmark worked out in R (in the folder benchmarks, with the scripts that made them).
// In Definitions.cs the same figures are worked out here from the definitions, by sums of probabilities and by bisection, for cases
// drawn at random.
using System.Globalization;
using System.Reflection;
using StatsDirect.Builtins;
using StatsDirect.Data;
using StatsDirect.Templates;

internal sealed class NoProgress : IProgressBarHost, IProgressBar
{
    public IProgressBar StartProgress(string operationDescription, bool provideProgress, bool display = true) => this;
    public void Finish() { }
    public bool Update(double fractionComplete) => false;
    public void Dispose() { }
}

internal static partial class Program
{
    internal const double M = double.MinValue;
    internal static int failures, checks;
    internal static readonly CultureInfo inv = CultureInfo.InvariantCulture;
    private static readonly NoProgress none = new();

    internal static void Say(bool ok, string what)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("FAIL  " + what); }
    }

    internal static string Message(Exception ex)
    {
        Exception inner = ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex;
        return inner.GetType().Name + ": " + inner.Message.Replace('\n', ' ').Replace('\r', ' ');
    }

    private static ParameterBag Bag(params (string name, double value)[] inputs)
    {
        ParameterBag p = new();
        foreach (var (name, value) in inputs) p.AddInput(name, value);
        return p;
    }

    internal static ParameterBag Sign(double n, double r, double level) => Exact.RptExactSign(Bag(("n", n), ("r", r), ("cco", level))).ParameterBag;
    internal static ParameterBag Fisher(double a, double b, double c, double d) => Exact.RptExactFisher(Bag(("a", a), ("b", b), ("c", c), ("d", d))).ParameterBag;
    internal static ParameterBag Expanded(double a, double b, double c, double d) => Exact.RptExactFisherX(Bag(("a", a), ("b", b), ("c", c), ("d", d))).ParameterBag;
    internal static ParameterBag Pairs(double a, double b, double c, double d, double level) => Exact.RptExactMcNamar(Bag(("a", a), ("b", b), ("c", c), ("d", d), ("gamma", level))).ParameterBag;
    internal static ParameterBag Odds(double a, double b, double c, double d, double level) => Exact.RptExactORCML(none, Bag(("a", a), ("b", b), ("c", c), ("d", d), ("gamma", level))).ParameterBag;
    internal static ParameterBag Rate(double events, double time, double level) => Exact.RptRatePoissonCI(Bag(("revents", events), ("tar", time), ("cco", level))).ParameterBag;

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
        // a probability of the expanded table is printed as text
        if (given is string text && double.TryParse(text, NumberStyles.Float, inv, out double printed)) given = printed;
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
        foreach (string line in File.ReadAllLines(Path.Combine(folder, "cases-exact.txt")))
        {
            string[] f = line.Split('\t');
            if (f.Length < 3) continue;
            double[] v = f.Skip(2).Select(x => double.Parse(x, inv)).ToArray();
            string name = f[1];
            switch (f[0])
            {
                case "sign": Report("sign|" + name, () => Sign(v[0], v[1], v[2]), figures); break;
                case "fisher":
                    Report("fisher|" + name, () => Fisher(v[0], v[1], v[2], v[3]), figures);
                    Report("fisherx|" + name, () => Expanded(v[0], v[1], v[2], v[3]), figures);
                    break;
                case "mcnemar": Report("mcnemar|" + name, () => Pairs(v[0], v[1], v[2], v[3], v[4]), figures); break;
                case "orci": Report("orci|" + name, () => Odds(v[0], v[1], v[2], v[3], v[4]), figures); break;
                case "prate": Report("prate|" + name, () => Rate(v[0], v[1], v[2]), figures); break;
            }
        }
        Dictionary<string, (int n, int bad, double worst, string what)> kinds = new();
        foreach (string line in File.ReadAllLines(Path.Combine(folder, "r-exact.txt")))
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
            // a figure may differ from its benchmark by one part in ten million
            if (by > 1e-7) { k.Item2++; if (by > k.Item3) { k.Item3 = by; k.Item4 = $"{part[0]}: {(have ? value : "not given")}, benchmark {part[1]}"; } }
            kinds[kind] = k;
        }
        foreach (var k in kinds.OrderBy(k => k.Key, StringComparer.Ordinal))
            Say(k.Value.bad == 0, $"{k.Key}: {k.Value.bad} of {k.Value.n} figures differ from their benchmarks; the worst is {k.Value.what}");
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  against the benchmarks: {compared} figures compared, in {kinds.Count} kinds");
    }

    private static int Main(string[] args)
    {
        string which = args.Length > 0 ? args[0] : "all";
        string folder = Path.Combine(AppContext.BaseDirectory, "benchmarks");
        if (which is "all" or "benchmarks") Benchmarks(folder);
        if (which is "all" or "definitions") Definitions();
        if (which is "all" or "limits") Limits();
        if (which is "all" or "scales") Scales();
        if (which is "all" or "many") Many();
        Console.WriteLine();
        Console.WriteLine(failures == 0 ? $"ALL {checks} CHECKS PASS" : $"{failures} OF {checks} CHECKS FAILED");
        return failures == 0 ? 0 : 1;
    }
}
