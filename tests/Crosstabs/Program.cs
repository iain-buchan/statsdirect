// The Crosstabs menu: checks by calculation.  Tables are put through the Crosstabs report and through the analysis of an r by c table,
// and every figure that has a benchmark is compared with it.  The benchmarks are in the folder benchmarks, with the R scripts that made
// them.  The exact test is also compared, in Exact.cs and Large.cs, with the sum over every table with the same totals, which is
// worked out here; Large.cs has the progress bar of the test too.
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
    // the exact test is made for a table of no more than so many subjects: that of a large table takes minutes
    private const double ExactTo = 60;

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

    internal static Dictionary<string, object> Report(string key, Func<ParameterBag> report)
    {
        Dictionary<string, object> figures = new();
        HostProxy.Questions.Clear();
        try
        {
            Collect(key, report(), figures);
            figures[key + "|ok"] = 1;
        }
        catch (Exception ex) { figures[key + "|ok"] = 0; figures[key + "|error"] = Message(ex); }
        return figures;
    }

    // A classifier as the worksheet gives it: the groups are numbered in the order in which their labels are first met
    internal static DataFrame Classifier(string title, IList<string> labels)
    {
        ClassifierVariable v = new();
        v.EnsureLength(labels.Count);
        Dictionary<string, Group> groups = new();
        for (int i = 0; i < labels.Count; i++)
        {
            if (labels[i] == null) { v.Data[i] = M; continue; }
            if (!groups.TryGetValue(labels[i], out Group g)) { g = new Group(labels[i], groups.Count); groups.Add(labels[i], g); }
            g.NBin++;
            v.Data[i] = g.Id;
        }
        v.EnsureGroups(groups.Count);
        foreach (Group g in groups.Values) v.Groups[(int)g.Id] = g;
        v.Title = title;
        DataFrame frame = new();
        frame.Variables.Add(v);
        return frame;
    }

    // The Crosstabs operation: the step that looks at the classifiers, and then the report
    internal static ParameterBag Crosstabs(DataFrame rows, DataFrame columns, DataFrame strata, double level, string study, double[] rowScores, double[] columnScores, bool exact = true, bool simulate = false)
    {
        ITemplateHost host = HostProxy.New();
        ParameterBag p = new();
        p.AddInput("c1", rows);
        p.AddInput("c2", columns);
        if (strata != null) p.AddInput("c3", strata);
        ParameterBag first = Tables.RptCrosstabsPreprocess(host, p).ParameterBag;
        foreach (string name in first.Keys) p.AddInput(name, first[name].AsObject);
        if (!p["strat"].AsBoolean)
        {
            p.AddInput("doExact", exact); p.AddInput("show_pc", true); p.AddInput("xp", true); p.AddInput("cs", true); p.AddInput("xs", true);
            p.AddInput("specify_scores", rowScores != null); p.AddInput("doMonteCarlo", simulate);
            if (simulate) { p.AddInput("iterations", 20000); p.AddInput("seed", 12345); p.AddInput("ci", 0.99); }
            HostProxy.Scores1 = rowScores; HostProxy.Scores2 = columnScores;
        }
        else if (p["xcats"].AsInt32 == 2 && p["ycats"].AsInt32 == 2) p.AddInput("study_type", study);
        else
        {
            p.AddInput("values1", rowScores ?? Enumerable.Range(1, p["ycats"].AsInt32).Select(i => (double)i).ToArray());
            p.AddInput("values2", columnScores ?? Enumerable.Range(1, p["xcats"].AsInt32).Select(i => (double)i).ToArray());
        }
        p.AddInput("cco", level);
        return Tables.RptCrosstabs(host, p).ParameterBag;
    }

    // the subjects of a table, one for each count, the last cells first so that the groups are not numbered in the order of their labels
    internal static (List<string> rows, List<string> columns, List<string> strata) Subjects(double[][][] t)
    {
        List<string> r = new(), c = new(), s = new();
        for (int k = t.Length - 1; k >= 0; k--)
            for (int i = t[k].Length - 1; i >= 0; i--)
                for (int j = t[k][i].Length - 1; j >= 0; j--)
                    for (int n = 0; n < (int)t[k][i][j]; n++)
                    {
                        r.Add((i + 1).ToString(inv));
                        c.Add((j + 1).ToString(inv));
                        s.Add((k + 1).ToString(inv));
                    }
        return (r, c, s);
    }

    internal static ParameterBag Analysis(double[][] t, double level, double[] rowScores, double[] columnScores, bool exact, bool simulate = false, int iterations = 100000, int seed = 12345)
    {
        int rows = t.Length, cols = t[0].Length;
        double[,] o = new double[rows + 1, cols + 1];
        for (int i = 0; i < rows; i++) for (int j = 0; j < cols; j++) o[i + 1, j + 1] = t[i][j];
        HostProxy.Scores1 = rowScores; HostProxy.Scores2 = columnScores;
        double cco = level;
        return Tables.SChi(HostProxy.New(), ref cco, o, rows, cols, exact, simulate, true, true, true, true, rowScores != null, 0.99, iterations, seed);
    }

    private static double[] Scores(string text) => text == "-" ? null : text.Split(',').Select(v => double.Parse(v, inv)).ToArray();

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
            int rows = int.Parse(head[3], inv), cols = int.Parse(head[4], inv), strata = int.Parse(head[5], inv);
            double level = double.Parse(head[6], inv);
            double[] rowScores = Scores(head[7]), columnScores = Scores(head[8]);
            double[][][] t = new double[strata][][];
            for (int k = 0; k < strata; k++)
            {
                t[k] = new double[rows][];
                for (int i = 0; i < rows; i++) t[k][i] = lines[++at].Split('\t').Select(v => double.Parse(v, inv)).ToArray();
            }
            double total = t.Sum(s => s.Sum(r => r.Sum()));
            bool whole = t.All(s => s.All(r => r.All(v => v == Math.Floor(v))));
            if (kind == "rc")
            {
                // the analysis of the table as it is given, empty rows and columns and all
                Add(Report(name + "|schi", () => Analysis(t[0], level, rowScores, columnScores, total <= ExactTo)));
                // the exact P values by simulation, for a table small enough for every table with its totals to be listed
                if (whole && total <= 60 && rows * cols <= 9 && rows > 1 && cols > 1)
                    Add(Report(name + "|mc", () => Analysis(t[0], level, rowScores, columnScores, false, true)));
                // the report from the subjects: the categories are those that have subjects
                if (whole)
                {
                    var (r, c, _) = Subjects(t);
                    foreach (bool symmetrise in new[] { false, true })
                    {
                        HostProxy.Symmetrise = symmetrise;
                        Add(Report(name + "|xtab.sym" + (symmetrise ? "1" : "0"), () => Crosstabs(Classifier("Rows", r), Classifier("Columns", c), null, level, null, rowScores, columnScores, total <= ExactTo)));
                    }
                    HostProxy.Symmetrise = false;
                }
            }
            else
            {
                var (r, c, s) = Subjects(t);
                if (rows == 2 && cols == 2)
                    foreach (string study in new[] { "casecontrol", "cohort" })
                        Add(Report(name + "|" + study, () => Crosstabs(Classifier("Rows", r), Classifier("Columns", c), Classifier("Strata", s), level, study, null, null)));
                else Add(Report(name + "|cmh", () => Crosstabs(Classifier("Rows", r), Classifier("Columns", c), Classifier("Strata", s), level, null, rowScores, columnScores)));
            }
        }
        return all;
    }

    // The figures of the program against the benchmarks: a figure may differ from its benchmark by one part in a million (of itself,
    // or of 1 if it is less than 1); a label or a warning must be the same.  A P value that is simulated is compared with the exact
    // P value, from which it may differ by 4.5 of its standard errors, and the limits that are given with it must hold the exact value.
    private static void Compare(Dictionary<string, object> figures, string file)
    {
        int before = failures, compared = 0, simulated = 0;
        Dictionary<string, (int n, int bad, string worst, double by)> groups = new();
        Dictionary<string, string> alias = new() { ["p2.fisher"] = "p2", ["p2.enumerated"] = "p2", ["x23.mantelhaen"] = "x23" };
        foreach (string line in File.ReadLines(file))
        {
            string[] part = line.Split('\t');
            if (part.Length != 2) continue;
            string key = part[0];
            string[] names = key.Split('|');
            string name = names[^1];
            string group = names[1] + " " + string.Join(" ", names.Skip(2).Select(p => System.Text.RegularExpressions.Regex.Replace(p, @"\.\d+$", "")));
            if (name == "p.exact")
            {
                string stem = key.Substring(0, key.Length - "|p.exact".Length);
                double exact = double.Parse(part[1], inv);
                bool given = figures.TryGetValue(stem + "|p", out object p) & figures.TryGetValue(stem + "|its", out object its) & figures.TryGetValue(stem + "|ll", out object ll) & figures.TryGetValue(stem + "|ul", out object ul);
                simulated++;
                if (!given) { Say(false, $"{stem}: no simulated P value"); continue; }
                double se = Math.Sqrt(exact * (1 - exact) / Convert.ToDouble(its));
                double by = se > 0 ? Math.Abs((double)p - exact) / se : ((double)p == exact ? 0 : 99);
                Say(by <= 4.5 && exact >= (double)ll - 1e-12 && exact <= (double)ul + 1e-12, $"{stem}: the simulated P value {p} ({ll} to {ul}) against the exact {exact}: {by:F2} standard errors");
                continue;
            }
            string found = alias.TryGetValue(name, out string other) ? key.Substring(0, key.Length - name.Length) + other : key;
            bool have = figures.TryGetValue(found, out object value);
            double by2;
            if (part[1].StartsWith("\"") || value is string)
                by2 = have && value is string text && "\"" + text + "\"" == part[1] ? 0 : double.PositiveInfinity;
            else
            {
                double expected = part[1] switch { "Inf" => double.PositiveInfinity, "-Inf" => double.NegativeInfinity, "NA" => M, "NaN" => double.NaN, _ => double.Parse(part[1], inv) };
                double given = have ? value switch { double d => d, int i => i, _ => double.NaN } : double.NaN;
                bool none = double.IsNaN(expected) || expected == M, noneGiven = !have || double.IsNaN(given) || given == M;
                by2 = none && noneGiven && have ? 0 : none || noneGiven ? double.PositiveInfinity : given == expected ? 0
                    : double.IsInfinity(given) || double.IsInfinity(expected) ? double.PositiveInfinity : Math.Abs(given - expected) / Math.Max(1, Math.Abs(expected));
            }
            compared++;
            var g = groups.TryGetValue(group, out var had) ? had : (0, 0, "", 0.0);
            g.Item1++;
            if (by2 > 1e-6) { g.Item2++; if (by2 > g.Item4) { g.Item4 = by2; g.Item3 = $"{key}: {(have ? value : "not given")}, benchmark {part[1]}"; } }
            groups[group] = g;
        }
        foreach (var g in groups.OrderBy(g => g.Key, StringComparer.Ordinal))
            Say(g.Value.bad == 0, $"{g.Key}: {g.Value.bad} of {g.Value.n} figures differ from their benchmarks; the worst is {g.Value.worst}");
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  against the benchmarks: {compared} figures compared, in {groups.Count} kinds; {simulated} simulated P values against their exact values");
    }

    private static int Main(string[] args)
    {
        string which = args.Length > 0 ? args[0] : "all";
        string folder = Path.Combine(AppContext.BaseDirectory, "benchmarks");
        if (which is "all" or "benchmarks")
        {
            Console.WriteLine("The tables of the benchmarks");
            Compare(Figures(Path.Combine(folder, "cases-xtab.txt")), Path.Combine(folder, "r-xtab.txt"));
        }
        if (which is "all" or "exact") Exact();
        if (which is "all" or "large") { LargeTotals(); WiderTables(folder); }
        if (which is "all" or "progress") ProgressOfTheTest(folder);
        if (which is "all" or "report") Reports();
        Console.WriteLine();
        Console.WriteLine(failures == 0 ? $"ALL {checks} CHECKS PASS" : $"{failures} OF {checks} CHECKS FAILED");
        return failures == 0 ? 0 : 1;
    }
}
