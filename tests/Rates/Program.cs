// The Rates menu: checks by calculation.  The comparison of two crude rates, indirect standardization and the SMR, direct
// standardization, the comparison of two standardized rates and the confidence interval of a rate are given cases, and every
// figure is compared with a benchmark worked out in R (in the folder benchmarks, with the scripts that made them).  In
// Definitions.cs the figures are worked out here from their definitions for cases drawn at random, and in Limits.cs the reports
// are looked at where the numbers are at the ends of what they can be.
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

// what the report of two rates asks of the program about it: a progress bar that shows nothing, and the faults that it is told of
public class RatesHost : DispatchProxy
{
    public static int Faults;
    private static readonly NoProgress none = new();

    protected override object Invoke(MethodInfo method, object[] arguments)
    {
        if (method.Name == "Error") Faults++;
        if (method.Name == "StartProgress") return none;
        Type type = method.ReturnType;
        return type == typeof(void) ? null : type.IsValueType ? Activator.CreateInstance(type) : null;
    }

    public static ITemplateHost New() => DispatchProxy.Create<ITemplateHost, RatesHost>();
}

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

    private static DataFrame Frame(params double[][] columns)
    {
        DataFrame frame = new();
        for (int i = 0; i < columns.Length; i++) frame.Variables.Add(new DoubleVariable(columns[i], "c" + (i + 1).ToString(inv)));
        return frame;
    }

    private static DataFrame Labels(string[] labels)
    {
        DataFrame frame = new();
        frame.Variables.Add(new StringVariable(labels, "labels"));
        return frame;
    }

    // ---- the reports of the menu
    internal static ParameterBag Two(double a, double b, double pt1, double pt2, double level, bool conditional)
    {
        ParameterBag p = new();
        p.AddInput("gamma", level); p.AddInput("a", a); p.AddInput("b", b); p.AddInput("pt1", pt1); p.AddInput("pt2", pt2); p.AddInput("do_cml", conditional);
        RatesHost.Faults = 0;
        ParameterBag report = Analysis.RptRateCompareTwo(RatesHost.New(), p).ParameterBag;
        report.AddOutput("faults", RatesHost.Faults);
        return report;
    }

    // the rates and the person-times from two columns of a worksheet, or from the grid of the screen form
    internal static ParameterBag Smr(double level, string multiplier, int deaths, double[] rates, double[] times, bool screen, string[] labels = null)
    {
        ParameterBag p = new();
        p.AddInput("cco", level); p.AddInput("nunit", multiplier); p.AddInput("dead", deaths);
        if (screen) p.AddInput("data", Frame(rates, times));
        else { p.AddInput("rates", Frame(rates)); p.AddInput("times", Frame(times)); }
        if (labels != null) p.AddInput("strata", Labels(labels));
        return Rates.RptRateSmr(p).ParameterBag;
    }

    internal static ParameterBag Direct(double level, string multiplier, double[] events, double[] times, double[] reference, bool screen, string[] labels = null)
    {
        ParameterBag p = new();
        p.AddInput("cco", level); p.AddInput("nunit", multiplier);
        if (screen)
        {
            p.AddInput("data", Frame(events, times, reference));
            return Analysis.RptRateDirectStd(p).ParameterBag;
        }
        p.AddInput("idxn", Frame(events)); p.AddInput("times", Frame(times)); p.AddInput("refn", Frame(reference));
        if (labels != null) p.AddInput("strata", Labels(labels));
        return Rates.RptRateDirect(p).ParameterBag;
    }

    internal static ParameterBag Standardized(double level, string multiplier, string model, double[] a, double[] pt1, double[] b, double[] pt2, double[] reference, string[] labels = null)
    {
        ParameterBag p = new();
        p.AddInput("cco", level); p.AddInput("nunit", multiplier); p.AddInput("model", model);
        p.AddInput("a", Frame(a)); p.AddInput("pt1", Frame(pt1)); p.AddInput("b", Frame(b)); p.AddInput("pt2", Frame(pt2)); p.AddInput("ref", Frame(reference));
        if (labels != null) p.AddInput("strata", Labels(labels));
        return Rates.RptStdrr(p).ParameterBag;
    }

    internal static ParameterBag Rate(double events, double time, double level)
    {
        ParameterBag p = new();
        p.AddInput("cco", level); p.AddInput("revents", events); p.AddInput("tar", time);
        return Exact.RptRatePoissonCI(p).ParameterBag;
    }

    internal static List<ParameterBag> Rows(ParameterBag bag, string block) =>
        !bag.ContainsKey(block) || bag[block].AsObject == null ? new List<ParameterBag>() : ((System.Collections.IEnumerable)bag[block].AsObject).Cast<ParameterBag>().ToList();

    // every figure of a report, under the name of the figure; the figures of the rows of a block have the name of the block and the
    // number of the row before them
    private static void Collect(string prefix, ParameterBag bag, Dictionary<string, object> figures)
    {
        foreach (string name in bag.Keys)
        {
            if (name == "*chart") continue;
            if (name.StartsWith("*"))
            {
                List<ParameterBag> rows = Rows(bag, name);
                for (int i = 0; i < rows.Count; i++) Collect(prefix + "|" + name.Substring(1) + "." + (i + 1).ToString(inv), rows[i], figures);
                figures[prefix + "|" + name.Substring(1) + ".rows"] = rows.Count;
                continue;
            }
            object value;
            try { value = bag[name].AsObject; } catch { continue; }
            if (value is long whole) value = (double)whole;
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

    private static double[] List(string text) => text.Split(',').Select(x => x == "NA" ? M : double.Parse(x, inv)).ToArray();

    private static void Benchmarks(string folder)
    {
        Console.WriteLine("The cases of the benchmarks");
        int before = failures, compared = 0;
        Dictionary<string, object> figures = new();
        System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
        double longest = 0; string slowest = "";
        foreach (string line in File.ReadAllLines(Path.Combine(folder, "cases-rates.txt")))
        {
            string[] f = line.Split('\t');
            if (f.Length < 3) continue;
            string key = f[0] + "|" + f[1];
            double level = double.Parse(f[2], inv);
            double start = clock.Elapsed.TotalSeconds;
            switch (f[0])
            {
                case "two": Report(key, () => Two(double.Parse(f[3], inv), double.Parse(f[4], inv), double.Parse(f[5], inv), double.Parse(f[6], inv), level, f[7] == "1"), figures); break;
                case "smr": Report(key, () => Smr(level, f[3], int.Parse(f[4], inv), List(f[5]), List(f[6]), f[7] == "1"), figures); break;
                case "direct": Report(key, () => Direct(level, f[3], List(f[4]), List(f[5]), List(f[6]), f[7] == "1"), figures); break;
                case "stdrr": Report(key, () => Standardized(level, f[3], f[4], List(f[5]), List(f[6]), List(f[7]), List(f[8]), List(f[9])), figures); break;
                case "rate": Report(key, () => Rate(double.Parse(f[3], inv), double.Parse(f[4], inv), level), figures); break;
            }
            double taken = clock.Elapsed.TotalSeconds - start;
            if (taken > longest) { longest = taken; slowest = line.Replace('\t', ' '); }
        }
        Dictionary<string, (int n, int bad, double worst, string what)> kinds = new();
        foreach (string line in File.ReadAllLines(Path.Combine(folder, "r-rates.txt")))
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
        // the conditional analysis of two rates makes a coefficient for each event: 780,000 events take some seconds
        Say(longest < 10.0, $"the case that takes longest ({slowest}) takes {longest:F2} seconds");
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
