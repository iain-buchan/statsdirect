// The Chi-square Tests menu: checks by calculation.  The 2 by 2 test, the 2 by k test with its test for trend and its simulated
// exact P value, the Mantel-Haenszel test of tables that are typed and Woolf's analysis are given cases, and every figure is
// compared with a benchmark worked out in R (in the folder benchmarks, with the scripts that made them).  In Definitions.cs the
// figures are worked out here from their definitions for cases drawn at random, in Simulations.cs the simulated exact P values
// are compared with the probabilities of the tables that they count, and in Limits.cs the reports are looked at where there is no
// test to make.
using System.Globalization;
using System.Reflection;
using StatsDirect.Builtins;
using StatsDirect.Data;
using StatsDirect.Templates;

internal static partial class Program
{
    internal const double M = double.MinValue;
    internal static int failures, checks;
    internal static readonly CultureInfo inv = CultureInfo.InvariantCulture;
    internal static readonly Host host = new();

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

    internal static DataFrame Frame(params double[][] columns)
    {
        DataFrame frame = new();
        for (int i = 0; i < columns.Length; i++) frame.Variables.Add(new DoubleVariable(columns[i], "c" + (i + 1).ToString(inv)));
        return frame;
    }

    // ---- the reports of the menu
    internal static ParameterBag TwoByTwo(double a, double b, double c, double d, double level, int study, bool fisher)
    {
        ParameterBag p = new();
        p.AddInput("a", a); p.AddInput("b", b); p.AddInput("c", c); p.AddInput("d", d); p.AddInput("cco", level);
        p.AddInput("study_type", study == 0 ? "casecontrol" : study == 1 ? "cohort" : "neither");
        p.AddInput("doFisher", fisher);
        return Chi.RptChi2By2(host, p).ParameterBag;
    }

    // trend: 0 without the test for trend, 1 with the scores 1 to k, 2 with the scores given
    internal static ParameterBag TwoByK(int trend, double[] successes, double[] failures, double[] scores)
    {
        ParameterBag p = new();
        p.AddInput("data", trend == 2 ? Frame(successes, failures, scores) : Frame(successes, failures));
        return (trend == 0 ? Chi.RptChi2ByNWithoutTrend(p) : trend == 1 ? Chi.RptChi2ByNLinearTrend(p) : Chi.RptChi2ByNWithTrend(p)).ParameterBag;
    }

    internal static ParameterBag Simulated(int trend, double[] successes, double[] failures, double[] scores, int tables, int seed, double level)
    {
        ParameterBag p = new();
        p.AddInput("data", trend == 2 ? Frame(successes, failures, scores) : Frame(successes, failures));
        p.AddInput("iterations", tables); p.AddInput("seed", seed); p.AddInput("ci", level);
        // the simulation is the second step of the analysis, and is handed what the first step hands on
        ParameterBag first = TwoByK(trend, successes, failures, scores);
        foreach (string name in first.Keys.Where(k => first[k].IsInputParameter && !p.ContainsKey(k))) p.AddInput(name, first[name].AsDouble);
        return Chi.RptChi2ByNWithTrendSimulateExactP(host, p).ParameterBag;
    }

    // the tables as they are typed: two columns, and two rows for each table
    internal static DataFrame Typed(double[] t)
    {
        int k = t.Length / 4;
        double[] left = new double[2 * k], right = new double[2 * k];
        for (int i = 0; i < k; i++) { left[2 * i] = t[4 * i]; right[2 * i] = t[4 * i + 1]; left[2 * i + 1] = t[4 * i + 2]; right[2 * i + 1] = t[4 * i + 3]; }
        return Frame(left, right);
    }

    internal static ParameterBag MantelTyped(double level, bool exact, double[] tables)
    {
        ParameterBag p = new();
        p.AddInput("data", Typed(tables));
        p.AddInput("cco", level); p.AddInput("try_exact", exact); p.AddInput("plot_forest", false);
        return Chi.RptChiMantel(host, p).ParameterBag;
    }

    // the same tables from a worksheet: the groups are the columns of a table that is typed, and the outcome is its first row
    internal static ParameterBag MantelSheet(double level, bool exact, double[] t)
    {
        int k = t.Length / 4;
        double[] sn = new double[k], sr = new double[k], xn = new double[k], xr = new double[k];
        for (int i = 0; i < k; i++) { sr[i] = t[4 * i]; xr[i] = t[4 * i + 1]; sn[i] = sr[i] + t[4 * i + 2]; xn[i] = xr[i] + t[4 * i + 3]; }
        ParameterBag p = new();
        p.AddInput("sn", Frame(sn)); p.AddInput("sr", Frame(sr)); p.AddInput("xn", Frame(xn)); p.AddInput("xr", Frame(xr));
        p.AddInput("gamma", level);
        bool was = PreferencesProxy.MetaExact;
        PreferencesProxy.MetaExact = exact;
        try { return Meta.RptMantel(host, p).ParameterBag; } finally { PreferencesProxy.MetaExact = was; }
    }

    internal static ParameterBag WoolfTyped(double level, bool intermediates, double[] tables)
    {
        ParameterBag p = new();
        p.AddInput("data", Typed(tables));
        p.AddInput("cco", level); p.AddInput("show_intermediates", intermediates);
        return Chi.RptChiWoolf(p).ParameterBag;
    }

    // v: for each table the size of the first group and the number of it with the outcome, and the same of the second group
    internal static ParameterBag WoolfSheet(double level, bool intermediates, double[] v)
    {
        int k = v.Length / 4;
        double[] sn = new double[k], sr = new double[k], xn = new double[k], xr = new double[k];
        for (int i = 0; i < k; i++) { sn[i] = v[4 * i]; sr[i] = v[4 * i + 1]; xn[i] = v[4 * i + 2]; xr[i] = v[4 * i + 3]; }
        ParameterBag p = new();
        p.AddInput("sn", Frame(sn)); p.AddInput("sr", Frame(sr)); p.AddInput("xn", Frame(xn)); p.AddInput("xr", Frame(xr));
        p.AddInput("cco", level); p.AddInput("show_intermediates", intermediates);
        return Tables.RptChiWoolfWorksheet(p).ParameterBag;
    }

    internal static ParameterBag Fisher(double a, double b, double c, double d)
    {
        ParameterBag p = new();
        p.AddInput("a", a); p.AddInput("b", b); p.AddInput("c", c); p.AddInput("d", d);
        return Exact.RptExactFisher(p).ParameterBag;
    }

    internal static List<ParameterBag> Rows(ParameterBag bag, string block) =>
        !bag.ContainsKey(block) || bag[block].AsObject == null ? new List<ParameterBag>() : ((System.Collections.IEnumerable)bag[block].AsObject).Cast<ParameterBag>().ToList();

    // every figure of a report, under the name of the figure; the figures of the rows of a block have the name of the block and the
    // number of the row before them, and the number of the rows of a block is a figure too
    internal static void Collect(string prefix, ParameterBag bag, Dictionary<string, object> figures)
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
            if (value is double or int or string) figures[prefix + "|" + name] = value;
        }
    }

    internal static Dictionary<string, object> Report(string key, Func<ParameterBag> report, Dictionary<string, object> figures = null)
    {
        figures ??= new Dictionary<string, object>();
        try
        {
            Collect(key, report(), figures);
            figures[key + "|ok"] = 1;
        }
        catch (Exception ex) { figures[key + "|ok"] = 0; figures[key + "|error"] = Message(ex); }
        return figures;
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

    // whether a figure is a statistic that can be nothing, of which the rounding of a sum can leave a residue: a chi-square, its root
    // or a logarithm of an odds ratio, and not a probability
    private static bool CanBeNothing(string key)
    {
        string name = key.Substring(key.LastIndexOf('|') + 1);
        return System.Text.RegularExpressions.Regex.IsMatch(name, "^(chi|chi_abs|chi_lin|chi_1df|chi_non|chi_2x?|chix?|het_chi_2x?|table_chi(_2)?|yates_chi(_2)?|log|meanx?|ci_(from|to)x?|vs|dif)$");
    }

    private static double Number(string text) => text switch { "Inf" => double.PositiveInfinity, "-Inf" => double.NegativeInfinity, "NA" => M, "NaN" => double.NaN, _ => double.Parse(text, inv) };

    // The figures of a file of benchmarks against those of the program; tolerance: the part of a figure by which it may differ
    private static int Compare(string file, Dictionary<string, object> figures, double tolerance, Func<string, bool> skip = null)
    {
        int compared = 0;
        Dictionary<string, (int n, int bad, double worst, string what)> kinds = new();
        foreach (string line in File.ReadAllLines(file))
        {
            string[] part = line.Split('\t');
            if (part.Length < 2 || (skip != null && skip(part[0]))) continue;
            string[] key = part[0].Split('|');
            string kind = key[0] + "|" + string.Join("|", key.Skip(2).Select(k => System.Text.RegularExpressions.Regex.Replace(k, @"\.\d+$", ".#")));
            bool have = figures.TryGetValue(part[0], out object value);
            double by;
            if (part[1].StartsWith("\"")) by = have && value is string text && "\"" + text + "\"" == part[1] ? 0 : double.PositiveInfinity;
            else
            {
                double expected = Number(part[1]);
                by = have ? Differs(value, expected) : double.PositiveInfinity;
                // a statistic that is nothing, or a residue of rounding in its place
                if (by > tolerance && have && value is double x && CanBeNothing(part[0]) && Math.Abs(expected) < 1e-12 && Math.Abs(x) < 1e-9) by = 0;
            }
            compared++;
            var k = kinds.TryGetValue(kind, out var had) ? had : (0, 0, 0.0, "");
            k.Item1++;
            if (by > tolerance) { k.Item2++; if (by > k.Item3) { k.Item3 = by; k.Item4 = $"{part[0]}: {(have ? value : "not given")}, benchmark {part[1]}"; } }
            kinds[kind] = k;
        }
        foreach (var k in kinds.OrderBy(k => k.Key, StringComparer.Ordinal))
            Say(k.Value.bad == 0, $"{k.Key}: {k.Value.bad} of {k.Value.n} figures differ from their benchmarks; the worst is {k.Value.what}");
        return compared;
    }

    private static (double[] s, double[] f, double[] v) RowsOf(double[] v, int from, int k) =>
        (Enumerable.Range(0, k).Select(i => v[from + 3 * i]).ToArray(), Enumerable.Range(0, k).Select(i => v[from + 3 * i + 1]).ToArray(), Enumerable.Range(0, k).Select(i => v[from + 3 * i + 2]).ToArray());

    private static void Benchmarks(string folder)
    {
        Console.WriteLine("The cases of the benchmarks");
        int before = failures;
        Dictionary<string, object> figures = new();
        foreach (string line in File.ReadAllLines(Path.Combine(folder, "cases-chi.txt")))
        {
            string[] f = line.Split('\t');
            if (f.Length < 3) continue;
            double[] v = f.Skip(2).Select(x => double.Parse(x, inv)).ToArray();
            string key = f[0] + "|" + f[1];
            switch (f[0])
            {
                case "chi22": Report(key, () => TwoByTwo(v[0], v[1], v[2], v[3], v[4], (int)v[5], v[6] != 0), figures); break;
                case "chi2k":
                {
                    var (s, fl, w) = RowsOf(v, 2, (int)v[1]);
                    Report(key, () => TwoByK((int)v[0], s, fl, w), figures);
                    break;
                }
                case "sim2k":
                {
                    var (s, fl, w) = RowsOf(v, 5, (int)v[1]);
                    Report(key, () => TwoByK((int)v[0], s, fl, w), figures);
                    Report(key + "|sim", () => Simulated((int)v[0], s, fl, w, (int)v[2], (int)v[3], v[4]), figures);
                    break;
                }
                case "mantel": Report(key, () => MantelTyped(v[0], v[1] != 0, v.Skip(3).ToArray()), figures); break;
                case "woolf": Report(key, () => WoolfTyped(v[0], v[1] != 0, v.Skip(3).ToArray()), figures); break;
                case "woolfws": Report(key, () => WoolfSheet(v[0], v[1] != 0, v.Skip(3).ToArray()), figures); break;
            }
        }
        // A figure may differ from its benchmark by one part in ten million.  The P values of the simulation are looked at below
        int compared = Compare(Path.Combine(folder, "r-chi.txt"), figures, 1e-7, key => key.Contains("|sim|p_"));

        // The simulated P value is the share of the tables drawn whose chi-square for trend is no less than that of the table
        // observed; the benchmark is the probability of such a table, from every table that has the totals of the one observed.
        // The two may differ by chance: by 5 standard errors of the share, which 1 simulation in 1,700,000 exceeds
        int simulations = 0;
        foreach (string line in File.ReadAllLines(Path.Combine(folder, "r-chi.txt")).Where(l => l.Contains("|sim|p_exact\t")))
        {
            string[] part = line.Split('\t');
            string root = part[0].Substring(0, part[0].Length - "p_exact".Length);
            double exact = double.Parse(part[1], inv);
            simulations++;
            if (!figures.TryGetValue(root + "result.1|p", out object p) || !figures.TryGetValue(root + "result.1|k", out object n))
            {
                Say(false, $"{root}: no P value is simulated ({(figures.TryGetValue(root + "note.1|note", out object note) ? note : "nothing is said")})");
                continue;
            }
            double tables = double.Parse((string)n, inv);
            double error = Math.Sqrt(exact * (1 - exact) / tables);
            Say(Math.Abs((double)p - exact) <= 5 * error, $"{root}: the simulated P value is {p} from {n} tables, and the probability is {exact}: they differ by {Math.Abs((double)p - exact) / error:F1} standard errors");
        }
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  against the benchmarks: {compared} figures compared, and {simulations} simulated P values");
    }

    // Fisher's exact test of tables that are too large to be tabulated from 0, which the 2 by 2 chi-square test gives when the
    // expected counts are few or the exact method of the odds ratio cannot be used
    private static void Large(string folder)
    {
        Console.WriteLine();
        Console.WriteLine("Fisher's exact test of large tables");
        int before = failures;
        Dictionary<string, object> figures = new();
        System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
        double longest = 0; string slowest = "";
        foreach (string line in File.ReadAllLines(Path.Combine(folder, "cases-fisher-large.txt")))
        {
            string[] f = line.Split('\t');
            if (f.Length < 6) continue;
            double[] v = f.Skip(2).Select(x => double.Parse(x, inv)).ToArray();
            double start = clock.Elapsed.TotalSeconds;
            Report("fisher|" + f[1], () => Fisher(v[0], v[1], v[2], v[3]), figures);
            double taken = clock.Elapsed.TotalSeconds - start;
            if (taken > longest) { longest = taken; slowest = string.Join(" ", f.Skip(2)); }
        }
        // a P value may differ from its benchmark by one part in a thousand million
        int compared = Compare(Path.Combine(folder, "r-fisher-large.txt"), figures, 1e-9);
        // the time is as the standard deviation of the first count, and not as the size of the table
        Say(longest < 2.0, $"the table that takes longest ({slowest}) takes {longest:F2} seconds");
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  {compared} figures compared; the table that takes longest takes {longest:F2} seconds");
    }

    private static int Main(string[] args)
    {
        string which = args.Length > 0 ? args[0] : "all";
        string folder = Path.Combine(AppContext.BaseDirectory, "benchmarks");
        if (which is "all" or "benchmarks") Benchmarks(folder);
        if (which is "all" or "large") Large(folder);
        if (which is "all" or "definitions") Definitions();
        if (which is "all" or "simulations") Simulations();
        if (which is "all" or "moved") Moved();
        if (which is "all" or "limits") Limits(folder);
        Console.WriteLine();
        Console.WriteLine(failures == 0 ? $"ALL {checks} CHECKS PASS" : $"{failures} OF {checks} CHECKS FAILED");
        return failures == 0 ? 0 : 1;
    }
}
