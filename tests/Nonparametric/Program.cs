// The Nonparametric menu: checks by calculation.  Each report of the menu is given the inputs of a case, and what it gives the
// report is compared with figures that are worked out from the definitions of the statistics (in the folder benchmarks, with
// the script that made them).  The shuffles of the simulated exact P values are checked for every order being equally likely,
// the simulated P values against the exact P by the count of every arrangement, and the distribution of Kendall's score at the
// most observations for which the program counts the orders, and just above it, where it uses a series, against a count made
// here.
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using StatsDirect.Builtins;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.Templates;

internal sealed class NoProgress : IProgressBarHost, IProgressBar
{
    public IProgressBar StartProgress(string operationDescription, bool provideProgress, bool display = true) => this;
    public void Finish() { }
    public bool Update(double fractionComplete) => false;
    public void Dispose() { }
}

// the preferences: six decimal places, and the default of everything else
public class PreferencesProxy : DispatchProxy
{
    protected override object Invoke(MethodInfo method, object[] arguments)
    {
        switch (method.Name)
        {
            case "get_PDecimalPlaces":
            case "get_DisplayDecimalPlaces": return 6;
        }
        Type type = method.ReturnType;
        return type == typeof(void) ? null : type.IsValueType ? Activator.CreateInstance(type) : null;
    }
}

// what the reports ask of the program about them: a progress bar that shows nothing, the preferences, and the words of a
// message, which the program would show in a window
public class NonparametricHost : DispatchProxy
{
    public static List<string> Messages = new();
    private static readonly NoProgress none = new();
    private static readonly SDPreferences preferences = DispatchProxy.Create<SDPreferences, PreferencesProxy>();

    protected override object Invoke(MethodInfo method, object[] arguments)
    {
        switch (method.Name)
        {
            case "StartProgress": return none;
            case "get_Preferences": return preferences;
            case "RoundU":
            case "pval":
            case "pval_half": return ((double)arguments[0]).ToString("R", CultureInfo.InvariantCulture);
            case "Error":
            case "Warning": Messages.Add(method.Name + ": " + string.Join(" / ", arguments.Select(a => a?.ToString()))); return null;
        }
        Type type = method.ReturnType;
        return type == typeof(void) ? null : type.IsValueType ? Activator.CreateInstance(type) : null;
    }

    public static ITemplateHost New() => DispatchProxy.Create<ITemplateHost, NonparametricHost>();
}

internal static class Program
{
    private static readonly CultureInfo inv = CultureInfo.InvariantCulture;
    private static readonly ITemplateHost host = NonparametricHost.New();
    private static int failures, checks;

    private static void Say(bool ok, string what)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("FAIL  " + what); }
    }

    // a figure that there is none of is the missing value, or what is not a number
    private static string Text(object o) => o switch
    {
        null => "null",
        double d => d == double.MinValue || double.IsNaN(d) ? "missing" : d.ToString("R", inv),
        IFormattable f => f.ToString(null, inv),
        _ => o.ToString()
    };

    // a table of numbers: bars between the columns, commas between the numbers, "*" for a number that is missing
    private static DataFrame Numbers(string value, string title)
    {
        DataFrame frame = new();
        int column = 0;
        foreach (string c in value.Split('|'))
            frame.Variables.Add(new DoubleVariable(c.Length == 0 ? new double[0] : c.Split(',').Select(v => v == "*" ? double.MinValue : double.Parse(v, inv)).ToArray(), title + (++column).ToString(inv)));
        return frame;
    }

    // everything that a report is given, by the name of the output: the rows of a list as "*list.rows" and "*list[i].output",
    // lists within lists in the same way; the columns of a table for the worksheet as "table[j].title" and "table[j][i]"
    private static void Collect(string prefix, ParameterBag bag, Dictionary<string, string> given)
    {
        foreach (string k in bag.Keys)
        {
            object o;
            try { o = bag[k].AsObject; } catch { continue; }
            if (o is StatsDirect.Charting.ChartDefinition) continue;
            if (o is DataFrame frame)
            {
                int j = 0;
                foreach (IVariable variable in frame.Variables)
                {
                    j++;
                    given[$"{prefix}{k}[{j}].title"] = variable.Title;
                    for (int i = 0; i < variable.Length; i++) given[$"{prefix}{k}[{j}][{i + 1}]"] = Text(variable.DataAsObject(i));
                }
                given[$"{prefix}{k}.columns"] = j.ToString(inv);
            }
            else if (o is System.Collections.IEnumerable rows && o is not string)
            {
                int i = 0;
                foreach (object row in rows)
                {
                    i++;
                    if (row is ParameterBag r) Collect($"{prefix}{k}[{i}].", r, given);
                }
                given[$"{prefix}{k}.rows"] = i.ToString(inv);
            }
            else given[prefix + k] = Text(o);
        }
    }

    // ---- a report of the menu with the inputs of a case (key=value, with semicolons between them): everything that it gives
    // the report, and a refusal as "refused"
    private static Dictionary<string, string> Report(string name, string inputs)
    {
        Dictionary<string, string> given = new();
        ParameterBag p = new();
        foreach (string pair in inputs.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            int eq = pair.IndexOf('=');
            string key = pair.Substring(0, eq), value = pair.Substring(eq + 1);
            if (key is "data" or "weights" or "outcome" or "predictor" or "scores") p.AddInput(key, Numbers(value, key == "data" ? "c" : key));
            else if (key is "seed" or "iterations" or "boots") p.AddInput(key, int.Parse(value, inv));
            else if (key == "sided") p.AddInput(key, value);
            else if (value is "true" or "false") p.AddInput(key, value == "true");
            else if (double.TryParse(value, NumberStyles.Float, inv, out double number)) p.AddInput(key, number);
            else p.AddInput(key, value);
        }
        NonparametricHost.Messages.Clear();
        try
        {
            MethodInfo report = typeof(Nonparametric).GetMethod(name, BindingFlags.Public | BindingFlags.Static);
            object[] arguments = report.GetParameters().Length == 2 ? new object[] { host, p } : new object[] { p };
            StepOutput step = (StepOutput)report.Invoke(null, arguments);
            if (step == null) { given["refused"] = "no report"; return given; }
            if (step.ParameterBag != null) Collect("", step.ParameterBag, given);
        }
        catch (Exception ex)
        {
            Exception inner = ex.InnerException ?? ex;
            given["refused"] = inner.GetType().Name + ": " + inner.Message.Replace('\n', ' ').Replace('\r', ' ');
        }
        return given;
    }

    // ---- whether what is given is what is expected.  A figure may differ by a part in a thousand million, or by the part that
    // the benchmark gives it ("value~part", or "value~room!" for a room that is not a part of the value).  An asterisk is a
    // figure that there is none of; a list that is not there has no rows.  Anything that is not a number is to be the same text.
    private static bool Number(string text, out double value)
    {
        if (text == "Infinity") { value = double.PositiveInfinity; return true; }
        if (text == "-Infinity") { value = double.NegativeInfinity; return true; }
        return double.TryParse(text, NumberStyles.Float, inv, out value);
    }

    private static bool Agrees(string output, string given, string expected)
    {
        if (given == "*") given = "missing";
        if ((given == "absent" || given == "null") && output.EndsWith(".rows", StringComparison.Ordinal)) given = "0";
        double part = 1e-9, room = -1;
        Match own = Regex.Match(expected, @"^(.*)~([0-9.eE+-]+)(!?)$");
        if (own.Success)
        {
            expected = own.Groups[1].Value;
            if (own.Groups[3].Value == "!") room = double.Parse(own.Groups[2].Value, inv); else part = double.Parse(own.Groups[2].Value, inv);
        }
        if (Number(given, out double x) && Number(expected, out double y))
            return x == y || Math.Abs(x - y) <= (room >= 0 ? room : part * Math.Abs(y) + 1e-300);
        return given == expected;
    }

    // the kind of an output: its name without the numbers of its rows
    private static string Kind(string report, string output) => report.Replace("Rpt", "") + ": " + (output == "refused" ? "what is refused" : Regex.Replace(output, @"\[\d+\]", "[]"));

    private static void Benchmarks(string folder)
    {
        int before = failures;
        Console.WriteLine("The cases of the benchmarks");
        Dictionary<string, List<(string output, string figure)>> expected = new();
        foreach (string line in File.ReadAllLines(Path.Combine(folder, "expected.txt")))
        {
            string[] f = line.Split('\t');
            if (f.Length < 2) continue;
            string[] k = f[0].Split('|', 3);
            string key = k[0] + "|" + k[1];
            if (!expected.ContainsKey(key)) expected[key] = new();
            expected[key].Add((k[2], f[1]));
        }
        SortedDictionary<string, (int n, int bad, string what)> kinds = new(StringComparer.Ordinal);
        int compared = 0, cases = 0;
        double longest = 0; string longestCase = "";
        foreach (string line in File.ReadAllLines(Path.Combine(folder, "cases.txt")))
        {
            string[] f = line.Split('\t');
            if (f.Length < 3) continue;
            cases++;
            System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
            Dictionary<string, string> given = Report(f[0], f[2]);
            double seconds = clock.Elapsed.TotalSeconds;
            if (seconds > longest) { longest = seconds; longestCase = f[0] + " " + f[1]; }
            foreach ((string output, string figure) in expected[f[0] + "|" + f[1]])
            {
                compared++;
                string group = Kind(f[0], output);
                if (!kinds.ContainsKey(group)) kinds[group] = (0, 0, "");
                string shown = given.ContainsKey(output) ? given[output] : given.ContainsKey("refused") ? "refused, " + given["refused"] : "absent";
                (int n, int bad, string what) k = kinds[group];
                k.n++;
                if (!Agrees(output, shown, figure))
                {
                    k.bad++;
                    if (k.what.Length == 0) k.what = $"{(f[2].Length > 120 ? f[2].Substring(0, 120) + "..." : f[2])}: {output} is {shown}, expected {figure}";
                }
                kinds[group] = k;
            }
        }
        foreach (KeyValuePair<string, (int n, int bad, string what)> k in kinds)
            Say(k.Value.bad == 0, $"{k.Key}: {k.Value.bad} of {k.Value.n} differ from their benchmarks; the first is {k.Value.what}");
        Say(longest < 10, $"the case that takes longest takes {longest:F1} seconds ({longestCase})");
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  against the benchmarks: {cases} cases, {compared} figures compared, in {kinds.Count} kinds; the case that takes longest takes {longest:F2} seconds");
    }

    // ---- the shuffles of the simulated exact P values: every order is to be equally likely.  Each shuffle is made many times
    // from one seed, and the counts of the orders are compared with equal counts by chi-square: with 3 things, 6 orders and
    // 60,000 shuffles, a chi-square above 30 (5 degrees of freedom, P about 0.00001) fails.  The exchange of each place with a
    // place drawn from all the places, which the program used to make, has counts of about 8,889 and 11,111 for 10,000 and a
    // chi-square of about 740.  With 4 things, 24 orders and 240,000 shuffles, the bound is 60 (23 degrees of freedom).
    private static double ChiSquare(IEnumerable<int> counts, int kinds, int total)
    {
        double expected = (double)total / kinds;
        return counts.Sum(c => (c - expected) * (c - expected) / expected) + (kinds - counts.Count()) * expected;
    }

    private static void Shuffles()
    {
        int before = failures;
        Console.WriteLine("The shuffles of the simulated exact P values");
        foreach ((int n, int draws, double bound) in new[] { (3, 60000, 30.0), (4, 240000, 60.0) })
        {
            MersenneTwister mt = new(20260930);
            Dictionary<string, int> arrayCounts = new(), rowCounts = new();
            double[] x = new double[n + 1];
            double[,] rows = new double[n + 1, 3];
            for (int d = 0; d < draws; d++)
            {
                for (int i = 1; i <= n; i++) x[i] = i;
                Nonparametric.ShuffleValuesWithinArray(x, mt, 1, n);
                string order = string.Join("", Enumerable.Range(1, n).Select(i => ((int)x[i]).ToString(inv)));
                arrayCounts[order] = arrayCounts.GetValueOrDefault(order) + 1;
            }
            // two rows, both shuffled: the orders of both are counted
            for (int d = 0; d < draws / 2; d++)
            {
                for (int i = 1; i <= n; i++) { rows[i, 1] = i; rows[i, 2] = i; }
                Nonparametric.ShuffleValuesWithinRows(rows, mt, n, 2);
                for (int r = 1; r <= 2; r++)
                {
                    string order = string.Join("", Enumerable.Range(1, n).Select(i => ((int)rows[i, r]).ToString(inv)));
                    rowCounts[order] = rowCounts.GetValueOrDefault(order) + 1;
                }
            }
            int orders = Enumerable.Range(1, n).Aggregate(1, (a, b) => a * b);
            double chiArray = ChiSquare(arrayCounts.Values, orders, draws), chiRows = ChiSquare(rowCounts.Values, orders, draws);
            Say(arrayCounts.Count == orders && chiArray <= bound, $"the shuffle of {n} values in an array: {arrayCounts.Count} orders seen of {orders}, chi-square {chiArray:F1} for equal chances in {draws} shuffles (bound {bound})");
            Say(rowCounts.Count == orders && chiRows <= bound, $"the shuffle of {n} values within rows: {rowCounts.Count} orders seen of {orders}, chi-square {chiRows:F1} for equal chances in {draws} shuffles (bound {bound})");
            Console.WriteLine($"      {n} values: chi-square {chiArray:F1} for the array and {chiRows:F1} within rows, on {orders - 1} degrees of freedom");
        }
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  the shuffles");
    }

    // ---- the simulated exact P values against the exact P by the count of every arrangement: the Kruskal-Wallis test with
    // groups of 3, 2 and 4 values (1,260 ways of choosing which values are in which group), the Friedman test with 4 blocks of
    // 3 treatments (1,296 orders within the blocks), and Cochran's Q with 6 blocks of 3 (46,656).  The program's P, from
    // 200,000 draws with a seed, is to be within 4.5 standard errors of the exact P, the standard error being that of a
    // proportion of 200,000 draws.  The statistics are worked out here from their definitions.
    private static double[] MidRanks(double[] values)
    {
        int n = values.Length;
        int[] order = Enumerable.Range(0, n).OrderBy(i => values[i]).ToArray();
        double[] ranks = new double[n];
        for (int i = 0; i < n;)
        {
            int j = i;
            while (j + 1 < n && values[order[j + 1]] == values[order[i]]) j++;
            for (int k = i; k <= j; k++) ranks[order[k]] = (i + j) / 2.0 + 1;
            i = j + 1;
        }
        return ranks;
    }

    // H of Kruskal and Wallis from the ranks of the pooled values and the group of each, with the correction for ties
    private static double KruskalH(double[] ranks, int[] group, int groups)
    {
        int n = ranks.Length;
        double[] sums = new double[groups]; int[] sizes = new int[groups];
        for (int i = 0; i < n; i++) { sums[group[i]] += ranks[i]; sizes[group[i]]++; }
        double h = 0;
        for (int g = 0; g < groups; g++) h += sums[g] * sums[g] / sizes[g];
        h = 12.0 / (n * (n + 1.0)) * h - 3 * (n + 1.0);
        double ties = 0;
        foreach (IGrouping<double, double> t in ranks.GroupBy(r => r)) { int c = t.Count(); ties += (double)c * c * c - c; }
        return ties > 0 ? h / (1 - ties / ((double)n * n * n - n)) : h;
    }

    // every way of giving the values to the groups in their sizes, and the proportion of them with H at least the observed
    private static double KruskalExact(double[][] groups, out double observed)
    {
        double[] values = groups.SelectMany(g => g).ToArray();
        double[] ranks = MidRanks(values);
        int[] sizes = groups.Select(g => g.Length).ToArray();
        int[] label = new int[values.Length];
        int at = 0;
        for (int g = 0; g < groups.Length; g++) for (int i = 0; i < sizes[g]; i++) label[at++] = g;
        observed = KruskalH(ranks, label, groups.Length);
        long ways = 0, reached = 0;
        int[] left = (int[])sizes.Clone();
        int[] assigned = new int[values.Length];
        double bound = observed - 1e-9;
        void Assign(int pos)
        {
            if (pos == values.Length) { ways++; if (KruskalH(ranks, assigned, groups.Length) >= bound) reached++; return; }
            for (int g = 0; g < groups.Length; g++)
            {
                if (left[g] == 0) continue;
                left[g]--; assigned[pos] = g;
                Assign(pos + 1);
                left[g]++;
            }
        }
        Assign(0);
        return (double)reached / ways;
    }

    // Conover's T2 of the Friedman test, or T1 for values of 0 and 1 (Cochran's Q), from the ranks within the blocks
    private static double FriedmanStatistic(double[][] blockRanks, int k, bool binary)
    {
        int n = blockRanks.Length;
        double a = 0; double[] sums = new double[k];
        foreach (double[] r in blockRanks) for (int j = 0; j < k; j++) { a += r[j] * r[j]; sums[j] += r[j]; }
        double b = sums.Sum(s => s * s) / n, c = n * k * (k + 1.0) * (k + 1.0) / 4;
        return binary ? (k - 1) * (b * n - n * c) / (a - c) : (n - 1) * (b - c) / (a - b);
    }

    // every order of the ranks within every block, and the proportion of them with the statistic at least the observed
    private static double FriedmanExact(double[][] blocks, bool binary, out double observed)
    {
        int n = blocks.Length, k = blocks[0].Length;
        double[][] ranks = blocks.Select(MidRanks).ToArray();
        observed = FriedmanStatistic(ranks, k, binary);
        double bound = observed - 1e-9;
        // the orders of k places
        List<int[]> orders = new();
        void Permute(int[] p, int from) { if (from == k) { orders.Add((int[])p.Clone()); return; } for (int i = from; i < k; i++) { (p[from], p[i]) = (p[i], p[from]); Permute(p, from + 1); (p[from], p[i]) = (p[i], p[from]); } }
        Permute(Enumerable.Range(0, k).ToArray(), 0);
        long ways = 0, reached = 0;
        double[][] arranged = ranks.Select(r => (double[])r.Clone()).ToArray();
        void Arrange(int block)
        {
            if (block == n) { ways++; if (FriedmanStatistic(arranged, k, binary) >= bound) reached++; return; }
            foreach (int[] order in orders)
            {
                for (int j = 0; j < k; j++) arranged[block][j] = ranks[block][order[j]];
                Arrange(block + 1);
            }
        }
        Arrange(0);
        return (double)reached / ways;
    }

    private static void CheckSimulated(string what, Dictionary<string, string> given, double exact, int iterations)
    {
        bool ok = given.ContainsKey("p") && Number(given["p"], out double p);
        Number(given.GetValueOrDefault("p", "x"), out double simulated);
        double se = Math.Sqrt(exact * (1 - exact) / iterations);
        ok = ok && Math.Abs(simulated - exact) <= 4.5 * se && given.GetValueOrDefault("k", "") == iterations.ToString(inv);
        Say(ok, $"{what}: simulated P {given.GetValueOrDefault("p", given.GetValueOrDefault("refused", "absent"))} for an exact P of {exact:F6} (standard error {se:F6}), {given.GetValueOrDefault("k", "?")} draws");
        Console.WriteLine($"      {what}: exact P {exact:F6}, simulated {simulated:F6}, {Math.Abs(simulated - exact) / se:F2} standard errors apart");
    }

    private static void Simulated()
    {
        int before = failures;
        Console.WriteLine("The simulated exact P values against the count of every arrangement");
        double[][] groups = { new[] { 1.2, 3.4, 2.2 }, new[] { 5.1, 4.4 }, new[] { 0.5, 2.9, 6.0, 1.1 } };
        double exact = KruskalExact(groups, out double h);
        CheckSimulated("Kruskal-Wallis, groups of 3, 2 and 4", Report("RptKruskalSimulateExactP", "data=1.2,3.4,2.2|5.1,4.4|0.5,2.9,6,1.1;iterations=200000;seed=7;ci=0.99"), exact, 200000);
        double[][] tied = { new[] { 2.0, 3.0, 2.0 }, new[] { 5.0, 3.0 }, new[] { 1.0, 2.0, 5.0, 1.0 } };
        exact = KruskalExact(tied, out h);
        CheckSimulated("Kruskal-Wallis with ties, groups of 3, 2 and 4", Report("RptKruskalSimulateExactP", "data=2,3,2|5,3|1,2,5,1;iterations=200000;seed=11;ci=0.99"), exact, 200000);
        // the Friedman test: 4 blocks of 3, the columns being the treatments
        double[][] blocks = { new[] { 5.4, 5.5, 5.55 }, new[] { 5.85, 5.7, 5.9 }, new[] { 5.2, 5.6, 5.5 }, new[] { 5.3, 5.5, 5.4 } };
        exact = FriedmanExact(blocks, false, out double t2);
        CheckSimulated("Friedman, 4 blocks of 3", Report("RptFriedmanSimulateExactP", "data=5.4,5.85,5.2,5.3|5.5,5.7,5.6,5.5|5.55,5.9,5.5,5.4;iterations=200000;seed=3;ci=0.99"), exact, 200000);
        double[][] binary = { new[] { 1.0, 0, 0 }, new[] { 1.0, 1, 0 }, new[] { 0.0, 0, 0 }, new[] { 1.0, 0, 1 }, new[] { 1.0, 1, 0 }, new[] { 1.0, 1, 1 } };
        exact = FriedmanExact(binary, true, out double q);
        CheckSimulated("Cochran's Q, 6 blocks of 3", Report("RptFriedmanSimulateExactP", "data=1,1,0,1,1,1|0,1,0,0,1,1|0,0,0,1,0,1;iterations=200000;seed=5;ci=0.99"), exact, 200000);
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  the simulated exact P values");
    }

    // ---- the distribution of Kendall's score: the proportion of the orders of n things with each number of discordant pairs,
    // built up a thing at a time (the j-th thing is put among the j - 1 before it in one of j places, each equally likely, and
    // makes 0 to j - 1 new discordant pairs), which is the definition.  The program counts the orders up to 1000 observations
    // and uses a series above: at 1000 its P is to be within 1e-12 of the count at every score, and at 1001 the series within
    // 1e-6, and within a thousandth of itself for P from 0.001 to 0.05.  The count of the program at 1000 is to take under 2
    // seconds.
    private static double[] DiscordantAtMost(int n)
    {
        int pairs = n * (n - 1) / 2;
        double[] p = new double[pairs + 1], prefix = new double[pairs + 2];
        p[0] = 1;
        int most = 0;
        for (int j = 2; j <= n; j++)
        {
            for (int d = 0; d <= most; d++) prefix[d + 1] = prefix[d] + p[d];
            int newMost = most + j - 1;
            for (int d = newMost; d >= 0; d--)
            {
                int hi = Math.Min(d, most), lo = Math.Max(0, d - j + 1);
                p[d] = hi >= lo ? (prefix[hi + 1] - prefix[lo]) / j : 0;
            }
            most = newMost;
        }
        double[] atMost = new double[pairs + 1];
        double total = 0;
        for (int d = 0; d <= pairs; d++) { total += p[d]; atMost[d] = Math.Min(1, total); }
        return atMost;
    }

    private static void Series()
    {
        int before = failures;
        Console.WriteLine("The distribution of Kendall's score at 1000 observations and above");
        foreach (int n in new[] { 1000, 1001 })
        {
            double[] atMost = DiscordantAtMost(n);
            int pairs = n * (n - 1) / 2;
            System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
            double first = MathDbl.kendp(pairs / 4, n, out bool fault);
            double seconds = clock.Elapsed.TotalSeconds;
            double worst = 0, worstTail = 0;
            for (int s = pairs % 2; s <= pairs; s += 2)
            {
                double exact = atMost[(pairs - s) / 2];
                double given = MathDbl.kendp(s, n, out fault);
                if (fault) given = double.NaN;
                worst = Math.Max(worst, double.IsNaN(given) ? double.PositiveInfinity : Math.Abs(given - exact));
                if (exact < 0.05 && exact >= 0.001) worstTail = Math.Max(worstTail, double.IsNaN(given) ? double.PositiveInfinity : Math.Abs(given / exact - 1));
            }
            if (n <= 1000)
            {
                Say(worst <= 1e-12, $"Kendall's score, {n} observations: P(S >= s) at every s against the count of the orders, the worst difference {worst:E2}");
                Say(seconds < 2, $"Kendall's score, {n} observations: the count takes {seconds:F2} seconds");
            }
            else
            {
                Say(worst <= 1e-6, $"Kendall's score, {n} observations, by the series: the worst difference from the count of the orders is {worst:E2}");
                Say(worstTail <= 1e-3, $"Kendall's score, {n} observations, by the series: a P from 0.001 to 0.05 differs by {worstTail:P3} of itself");
            }
            Console.WriteLine($"      {n} observations: worst difference {worst:E2}, and {worstTail:P4} of a P from 0.001 to 0.05; the first P took {seconds:F2} seconds");
        }
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  the distribution of Kendall's score");
    }

    private static int Main(string[] args)
    {
        string folder = Path.Combine(AppContext.BaseDirectory, "benchmarks");
        HashSet<string> parts = new(args);
        if (parts.Count == 0 || parts.Contains("benchmarks")) Benchmarks(folder);
        if (parts.Count == 0 || parts.Contains("shuffles")) Shuffles();
        if (parts.Count == 0 || parts.Contains("simulated")) Simulated();
        if (parts.Count == 0 || parts.Contains("series")) Series();
        Console.WriteLine();
        Console.WriteLine(failures == 0 ? $"ALL {checks} CHECKS PASS" : $"{failures} OF {checks} CHECKS FAILED");
        return failures == 0 ? 0 : 1;
    }
}
