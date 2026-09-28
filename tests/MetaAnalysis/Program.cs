// The Meta-analysis menu: checks by calculation.  Sets of studies are put through the reports of the menu, and every figure of each
// report that has a benchmark is compared with it.  The benchmarks are in the folder benchmarks, with the R scripts that made them:
// the figures of R's packages meta and metafor, and the exact and score figures from the distributions themselves.
using System.Globalization;
using System.Reflection;
using StatsDirect.Builtins;
using StatsDirect.Data;
using StatsDirect.Templates;

internal static partial class Program
{
    internal const double M = double.MinValue;
    internal static int failures, checks;
    private static readonly CultureInfo inv = CultureInfo.InvariantCulture;

    internal static void Say(bool ok, string what)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("FAIL  " + what); }
    }

    internal static List<ParameterBag> Rows(ParameterBag bag, string block) =>
        !bag.ContainsKey(block) || bag[block].AsObject == null ? new List<ParameterBag>() : ((System.Collections.IEnumerable)bag[block].AsObject).Cast<ParameterBag>().ToList();

    internal static string Message(Exception ex)
    {
        Exception inner = ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex;
        return inner.GetType().Name + ": " + inner.Message.Replace('\n', ' ').Replace('\r', ' ');
    }

    internal static DataFrame Column(string title, IEnumerable<double> values) => new(new DoubleVariable(values.ToArray(), title));

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
                figures[prefix + "|" + name + "|rows"] = rows.Count;
                for (int i = 0; i < rows.Count; i++) Collect(prefix + "|" + name.Substring(1) + "." + (i + 1).ToString(inv), rows[i], figures);
                continue;
            }
            object value;
            try { value = bag[name].AsObject; } catch { continue; }
            if (value is double or int or string) figures[prefix + "|" + name] = value;
        }
    }

    internal static Dictionary<string, object> Report(string key, Func<Host, StepOutput> report)
    {
        Dictionary<string, object> figures = new();
        try
        {
            Collect(key, report(new Host()).ParameterBag, figures);
            figures[key + "|ok"] = 1;
        }
        catch (Exception ex) { figures[key + "|ok"] = 0; figures[key + "|error"] = Message(ex); }
        return figures;
    }

    internal static readonly (string name, bool exact, double cc, bool delay)[] settings =
    {
        ("x1.tacc.d0", true, -9, false), ("x0.tacc.d0", false, -9, false), ("x0.cc5.d0", false, 0.5, false), ("x0.cc5.d1", false, 0.5, true),
    };

    internal static void Set((string name, bool exact, double cc, bool delay) s)
    {
        PreferencesProxy.MetaExact = s.exact; PreferencesProxy.MetaCC = s.cc; PreferencesProxy.Delay = s.delay;
    }

    internal static StepOutput Binary(string analysis, Host h, double level, double[] r1, double[] n1, double[] r2, double[] n2)
    {
        ParameterBag b = new();
        b.AddInput("gamma", level);
        b.AddInput("sr", Column("r1", r1)); b.AddInput("sn", Column("n1", n1));
        b.AddInput("xr", Column("r2", r2)); b.AddInput("xn", Column("n2", n2));
        return analysis switch
        {
            "or" => Meta.RptMantel(h, b),
            "peto" => Meta.RptPetoMeta(h, b),
            "rr" => Meta.RptRelativeRiskMeta(h, b),
            _ => Meta.RptRiskDifferenceMeta(h, b),
        };
    }

    // the figures of the program for every case of a file of cases (the README has the form of the file)
    private static Dictionary<string, object> Figures(string file)
    {
        Dictionary<string, object> all = new();
        void Add(Dictionary<string, object> some) { foreach (var f in some) all[f.Key] = f.Value; }
        string[] lines = File.ReadAllLines(file);
        for (int at = 0; at < lines.Length; at++)
        {
            string[] head = lines[at].Split('\t');
            if (head[0] != "case") continue;
            string name = head[1], kind = head[2];
            int k = int.Parse(head[3], inv);
            double level = double.Parse(head[4], inv);
            double[][] row = Enumerable.Range(1, k).Select(i => lines[at + i].Split('\t').Select(v => double.Parse(v, inv)).ToArray()).ToArray();
            at += k;
            double[] col(int j) => row.Select(r => r[j]).ToArray();
            foreach (var s in settings)
            {
                if (kind != "bin" && s.name != "x1.tacc.d0" && s.name != "x0.tacc.d0") continue;
                Set(s);
                string key = name + "|" + s.name;
                ParameterBag bag() { ParameterBag b = new(); b.AddInput("gamma", level); return b; }
                switch (kind)
                {
                    case "bin":
                        foreach (string analysis in new[] { "or", "peto", "rr", "rd" })
                        {
                            if (analysis == "peto" && s.name != "x1.tacc.d0" && s.name != "x0.tacc.d0") continue;
                            Add(Report(key + "|" + analysis, h => Binary(analysis, h, level, col(0), col(1), col(2), col(3))));
                        }
                        break;
                    case "rate":
                        foreach (bool ratio in new[] { true, false })
                            Add(Report(key + "|" + (ratio ? "irr" : "ird"), h =>
                            {
                                ParameterBag b = bag();
                                b.AddInput("a", Column("a", col(0))); b.AddInput("pt1", Column("pt1", col(1)));
                                b.AddInput("b", Column("b", col(2))); b.AddInput("pt2", Column("pt2", col(3)));
                                return ratio ? Meta.RptMetaIncidenceRateRatio(h, b) : Meta.RptMetaIncidenceRateDifference(h, b);
                            }));
                        break;
                    case "cont":
                        foreach (string type in new[] { "d", "m" })
                            Add(Report(key + "|" + (type == "d" ? "smd" : "wmd"), h =>
                            {
                                ParameterBag b = bag();
                                b.AddInput("type", type);
                                b.AddInput("en", Column("ne", col(0))); b.AddInput("em", Column("me", col(1))); b.AddInput("es", Column("se", col(2)));
                                b.AddInput("cn", Column("nc", col(3))); b.AddInput("cm", Column("mc", col(4))); b.AddInput("cs", Column("sc", col(5)));
                                return Meta.RptEffect(h, b);
                            }));
                        break;
                    case "prop":
                        foreach (string method in new[] { "doubleArcsine", "arcsine" })
                            Add(Report(key + "|prop." + method, h =>
                            {
                                ParameterBag b = bag();
                                b.AddInput("method", method);
                                b.AddInput("sr", Column("r", col(0))); b.AddInput("sn", Column("n", col(1)));
                                return Meta.RptProportionMeta(h, b);
                            }));
                        break;
                    case "cor":
                        Add(Report(key + "|cor", h =>
                        {
                            ParameterBag b = bag();
                            b.AddInput("r", Column("r", col(0))); b.AddInput("n", Column("n", col(1)));
                            return Meta.RptMetaCorrelation(h, b);
                        }));
                        break;
                    case "gen":
                        foreach (bool ratio in new[] { false, true })
                        {
                            if (ratio && col(0).Any(v => v <= 0)) continue;
                            foreach (bool limits in new[] { false, true })
                                Add(Report(key + "|gen." + (ratio ? "ratio" : "plain") + (limits ? ".ci" : ".se"), h =>
                                {
                                    ParameterBag b = bag();
                                    b.AddInput("statx", " differs from " + (ratio ? "1" : "0")); b.AddInput("use_ratio", ratio); b.AddInput("stat_in", "Statistic");
                                    b.AddInput("use_ci", limits ? "true" : "false");
                                    b.AddInput("y", Column("y", col(0)));
                                    if (limits) { b.AddInput("ll_y", Column("ll", col(2))); b.AddInput("ul_y", Column("ul", col(3))); }
                                    else b.AddInput("se_y", Column("se", col(1)));
                                    return Meta.RptMetaSummary(h, b);
                                }));
                        }
                        break;
                }
            }
        }
        return all;
    }

    private static double Number(string text) => text switch { "Inf" => double.PositiveInfinity, "-Inf" => double.NegativeInfinity, "NA" => M, _ => double.Parse(text, inv) };

    // The figures of the program against the benchmarks of a file: a figure may differ from its benchmark by one part in ten million
    // (of itself, or of 1 if it is less than 1), and a limit of I-squared by one in a million, which is what the search for it is good to
    private static void Compare(string title, Dictionary<string, object> figures, string file)
    {
        int before = failures, compared = 0;
        Dictionary<string, (int n, int bad, string worst, double by)> groups = new();
        foreach (string line in File.ReadLines(file))
        {
            string[] part = line.Split('\t');
            if (part.Length != 2) continue;
            string key = part[0];
            double expected = Number(part[1]);
            string[] names = key.Split('|');
            string name = names[^1], group = names[2] + " " + (names.Length > 4 ? names[3].Split('.')[0] + "." : "") + name;
            double tolerance = name is "llisq" or "ulisq" ? 1e-6 : 1e-7;
            double given = figures.TryGetValue(key, out object value) ? value switch { double d => d, int i => i, _ => double.NaN } : double.NaN;
            double by = given == expected ? 0 : double.IsNaN(given) || given == M || expected == M || double.IsInfinity(given) || double.IsInfinity(expected) ? double.PositiveInfinity
                : Math.Abs(given - expected) / Math.Max(1, Math.Abs(expected));
            compared++;
            var g = groups.TryGetValue(group, out var had) ? had : (0, 0, "", 0.0);
            g.Item1++;
            if (by > tolerance) { g.Item2++; if (by > g.Item4) { g.Item4 = by; g.Item3 = $"{key}: {(figures.TryGetValue(key, out object v) ? v : "not given")}, benchmark {part[1]}"; } }
            groups[group] = g;
        }
        foreach (var g in groups.OrderBy(g => g.Key, StringComparer.Ordinal))
            Say(g.Value.bad == 0, $"{title}, {g.Key}: {g.Value.bad} of {g.Value.n} figures differ from their benchmarks; the worst is {g.Value.worst}");
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  {title}: {compared} figures compared, in {groups.Count} kinds");
    }

    private static int Main(string[] args)
    {
        string which = args.Length > 0 ? args[0] : "all";
        string folder = Path.Combine(AppContext.BaseDirectory, "benchmarks");
        if (which is "all" or "bin")
        {
            Console.WriteLine("Odds ratio, Peto odds ratio, relative risk and risk difference");
            Dictionary<string, object> figures = Figures(Path.Combine(folder, "cases-bin.txt"));
            Compare("against R's meta", figures, Path.Combine(folder, "r-bin.txt"));
            Compare("against the distributions themselves", figures, Path.Combine(folder, "x-bin.txt"));
        }
        if (which is "all" or "other")
        {
            Console.WriteLine();
            Console.WriteLine("Incidence rates, effect size, proportion, correlation and summary");
            Dictionary<string, object> figures = Figures(Path.Combine(folder, "cases-other.txt"));
            Compare("against R's meta and metafor", figures, Path.Combine(folder, "r-other.txt"));
            Compare("against the distributions themselves", figures, Path.Combine(folder, "x-other.txt"));
        }
        if (which is "all" or "limits") Limits();
        if (which is "all" or "precision") EffectSizePrecision();
        Console.WriteLine();
        Console.WriteLine(failures == 0 ? $"ALL {checks} CHECKS PASS" : $"{failures} OF {checks} CHECKS FAILED");
        return failures == 0 ? 0 : 1;
    }
}
