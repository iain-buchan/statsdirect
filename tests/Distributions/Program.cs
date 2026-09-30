// The Distributions menu: checks by calculation.  The calculator of the menu (the control ctlPDF) is made without being shown, its
// boxes are filled as a user fills them, and what it puts into its boxes is compared with figures that are worked out from the
// distributions themselves (in the folder benchmarks, with the script that made them).  The studentized range, which the
// multiple comparisons of the analysis of variance use too, is gone through for probabilities that are refused, and the
// Newman-Keuls comparisons are made of groups that are far apart.
using System.Globalization;
using System.Reflection;
using System.Windows.Forms;
using StatsDirect.Builtins;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.Templates;
using StatsDirect.UI;

internal static class Program
{
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static readonly string[] BoxNames = { "txtPdf", "txtDf", "txtDf2", "txtLp", "txtUp", "txt2p" };
    private static readonly CultureInfo inv = CultureInfo.InvariantCulture;
    private static int failures, checks;

    private static void Say(bool ok, string what)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("FAIL  " + what); }
    }

    // ---- the calculator: the boxes after a value (p), or a lower tail, upper tail or two sided probability (lp, up, 2p), is
    // entered with the parameters a and b; "-" is a box that is left empty
    private static TextBox Box(ctlPDF c, string name) => (TextBox)typeof(ctlPDF).GetField(name, Any).GetValue(c);

    private static void Touch(ctlPDF c, string what)
    {
        FieldInfo last = typeof(ctlPDF).GetField("lastTouchedValue", Any);
        last.SetValue(c, Enum.Parse(last.FieldType, what));
    }

    private static Dictionary<string, string> Calculator(string kind, string action, string x, string a, string b)
    {
        Dictionary<string, string> boxes = new();
        try
        {
            using ctlPDF c = new(new DistributionOptions { SelectedTest = Enum.Parse<DistributionType>(kind) });
            string Fill(string text) => text == "-" ? string.Empty : text;
            Box(c, "txtDf").Text = Fill(a);
            Box(c, "txtDf2").Text = Fill(b);
            if (action == "p")
            {
                Box(c, "txtPdf").Text = Fill(x);
                Touch(c, x == "-" ? "Df2" : "Pdf");
                typeof(ctlPDF).GetMethod("Calculate", Any).Invoke(c, null);
            }
            else
            {
                Box(c, action == "lp" ? "txtLp" : action == "up" ? "txtUp" : "txt2p").Text = x;
                Touch(c, action == "lp" ? "Lp" : action == "up" ? "Up" : "P2");
                typeof(ctlPDF).GetMethod("Invert", Any).Invoke(c, new object[] { true });
            }
            foreach (string box in BoxNames) boxes[box] = Box(c, box).Text;
            string error = ((Label)typeof(ctlPDF).GetField("lblError", Any).GetValue(c)).Text;
            if (error.Length > 0) boxes["message"] = error;
        }
        catch (Exception ex)
        {
            Exception inner = ex.InnerException ?? ex;
            boxes["exception"] = inner.GetType().Name + ": " + inner.Message.Replace('\n', ' ').Replace('\r', ' ');
        }
        return boxes;
    }

    // ---- whether a box shows what is expected.  A box shows 15 decimal places, or 15 figures of a probability below 1e-15, or 7
    // figures of a value above 1e15; the boxes of the studentized range show 7 decimal places.  The room of a figure is what
    // the box can show of it, and a part in a thousand million of it: unless the benchmark gives the figure its own room.
    private static bool Agrees(string kind, string box, string shown, string expected, double room, out double by)
    {
        by = double.PositiveInfinity;
        if (expected == "error") return shown == "error";
        if (expected == "below 1e-15") return shown == "< 1E-15";
        double y = double.Parse(expected, inv);
        double x;
        if (shown == "< 1E-15")
        {
            by = Math.Abs(y);
            return Math.Abs(y) <= 1e-15 + (double.IsNaN(room) ? 0 : room);
        }
        if (!double.TryParse(shown, NumberStyles.Float, inv, out x)) return false;
        const double part = 1e-9;
        if (double.IsNaN(room))
        {
            if (kind == "Q") room = 6e-8;
            else if (Math.Abs(y) > 1e15) room = 1e-6 * Math.Abs(y);
            else if (y != 0 && Math.Abs(y) < 1e-15 && box != "txtPdf") room = Math.Abs(y) < 2.3e-308 ? Math.Abs(y) : part * Math.Abs(y);
            else room = 2e-15 + part * Math.Abs(y);
        }
        by = Math.Abs(x - y);
        return by <= room;
    }

    private static void Benchmarks(string folder)
    {
        int before = failures;
        Console.WriteLine("The cases of the benchmarks");
        Dictionary<string, List<(string box, string figure, double room)>> expected = new();
        foreach (string line in File.ReadAllLines(Path.Combine(folder, "expected.txt")))
        {
            string[] f = line.Split('\t');
            if (f.Length < 2) continue;
            int bar = f[0].LastIndexOf('|');
            string key = f[0].Substring(0, bar);
            if (!expected.ContainsKey(key)) expected[key] = new();
            expected[key].Add((f[0].Substring(bar + 1), f[1], f.Length > 2 ? double.Parse(f[2], inv) : double.NaN));
        }
        SortedDictionary<string, (int n, int bad, double worst, string what)> kinds = new(StringComparer.Ordinal);
        int compared = 0, cases = 0;
        double longest = 0; string longestCase = "";
        foreach (string line in File.ReadAllLines(Path.Combine(folder, "cases.txt")))
        {
            string[] f = line.Split('\t');
            if (f.Length < 6) continue;
            cases++;
            System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
            Dictionary<string, string> boxes = Calculator(f[0], f[2], f[3], f[4], f[5]);
            double seconds = clock.Elapsed.TotalSeconds;
            if (seconds > longest) { longest = seconds; longestCase = $"{f[0]} {f[2]} {f[3]} {f[4]} {f[5]}"; }
            string group = f[0] + " " + (f[2] == "p" ? "probabilities" : "value of a probability");
            if (!kinds.ContainsKey(group)) kinds[group] = (0, 0, 0, "");
            foreach ((string box, string figure, double room) in expected[f[0] + "|" + f[1]])
            {
                compared++;
                string shown = boxes.ContainsKey("exception") ? boxes["exception"] : boxes[box];
                bool ok = Agrees(f[0], box, shown, figure, room, out double by);
                (int n, int bad, double worst, string what) k = kinds[group];
                k.n++;
                if (!ok)
                {
                    k.bad++;
                    if (by >= k.worst) { k.worst = by; k.what = $"{f[2]} {f[3]} {f[4]} {f[5]}: the box {box} shows {shown}, expected {figure}"; }
                }
                kinds[group] = k;
            }
        }
        foreach (KeyValuePair<string, (int n, int bad, double worst, string what)> k in kinds)
            Say(k.Value.bad == 0, $"{k.Key}: {k.Value.bad} of {k.Value.n} figures differ from their benchmarks; the worst is {k.Value.what}");
        Say(longest < 10, $"the case that takes longest takes {longest:F1} seconds ({longestCase})");
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  against the benchmarks: {cases} cases, {compared} figures compared, in {kinds.Count} kinds; the case that takes longest takes {longest:F2} seconds");
    }

    // ---- the studentized range over degrees of freedom, numbers of means and ranges: every probability is given, is within
    // 0 to 1, and does not go down as the range goes up
    private static void Range()
    {
        int before = failures;
        Console.WriteLine("The studentized range, gone through");
        int values = 0;
        foreach (double df in new[] { 2.0, 5, 8, 12, 20, 30, 50, 100, 101, 200, 500, 800, 801, 2000, 5000, 5001, 20000, 20001, 25000, 25001, 1e5, 1e6 })
            foreach (double k in new[] { 2.0, 3, 8, 30 })
            {
                int refused = 0, outside = 0, down = 0;
                double last = 0, first = 0;
                bool integrated = k != 2 && (df < 8 || df < 2 * k || (df > 20000 && df < 1e6));      // which takes a twentieth of a second
                for (double q = 0.5; q <= 60; q += integrated ? 4 : 0.5)
                {
                    double p = PDF.probsr(q, k, df);
                    values++;
                    if (p == Constant.MISSING || double.IsNaN(p)) { refused++; if (first == 0) first = q; continue; }
                    if (p < 0 || p > 1) outside++;
                    if (p < last - 1e-7) down++;
                    last = p;
                }
                Say(refused == 0 && outside == 0 && down == 0, $"{k} means, {df} degrees of freedom: {refused} probabilities refused (the first at a range of {first}), {outside} outside 0 to 1, {down} below that of a smaller range");
            }
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  the studentized range: {values} probabilities");
    }

    // ---- Newman-Keuls comparisons of groups that are far apart, with numbers of residual degrees of freedom for which the
    // probability of a large range used to be refused: every difference is significant, and has a P within 0 to 1
    private static void Comparisons()
    {
        int before = failures;
        Console.WriteLine("Newman-Keuls comparisons of groups that are far apart");
        foreach ((int groups, int size) in new[] { (5, 11), (3, 6), (4, 201), (6, 5) })
        {
            DataFrame frame = new();
            for (int g = 0; g < groups; g++)
                frame.Variables.Add(new DoubleVariable(Enumerable.Range(0, size).Select(i => 10.0 * g + ((i * 7) % 11 - 5) / 5.0).ToArray(), "g" + g.ToString(inv)));
            ParameterBag p = new();
            p.AddInput("data", frame);
            p.AddInput("gamma", 0.95);
            string what = $"{groups} groups of {size}, {groups * (size - 1)} residual degrees of freedom";
            try
            {
                ParameterBag bag = Anova.RptNewmanKeuls(p).ParameterBag;
                List<ParameterBag> differences = ((System.Collections.IEnumerable)bag["*differences"].AsObject).Cast<ParameterBag>().ToList();
                List<ParameterBag> summary = ((System.Collections.IEnumerable)bag["*summary"].AsObject).Cast<ParameterBag>().ToList();
                int outside = differences.Count(d => !(d["p"].AsDouble >= 0 && d["p"].AsDouble <= 0.05));
                int untested = differences.Count(d => d.ContainsKey("stop_marker"));
                int alone = summary.Count(s => Convert.ToString(s["sigs"].AsObject, inv).Split(',').Length != groups - 1);
                Say(differences.Count == groups * (groups - 1) / 2 && outside == 0 && untested == 0 && alone == 0,
                    $"{what}: {differences.Count} differences, of which {outside} have a P that is not within 0 to 0.05 and {untested} were not tested; {alone} groups are not given as different from every other");
            }
            catch (Exception ex)
            {
                Exception inner = ex.InnerException ?? ex;
                Say(false, $"{what}: {inner.GetType().Name}: {inner.Message}");
            }
        }
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  the Newman-Keuls comparisons");
    }

    // ---- the value of the studentized range at a small lower tail probability: the value given for the probability has
    // the probability given back within a part in a hundred million, and the values of two cases are those of an independent
    // adaptive integration of the distribution (the range of normal variates mixed over the residual standard deviation)
    private static void Tails()
    {
        int before = failures;
        Console.WriteLine("The studentized range in a small lower tail");
        foreach ((double k, double df, double p, double value) in new[] { (30.0, 60.0, 1e-12, 0.8669439305), (30.0, 25000.0, 1e-16, 0.6769738494), (30.0, 60.0, 1e-6, 1.5306595381), (10.0, 20.0, 1e-8, 0.2649127683),
                                                                           (5.0, 10.0, 0.001, 0.0), (50.0, 100.0, 1e-9, 0.0), (4.0, 1000.0, 1e-7, 0.0), (30.0, 1e6, 1e-12, 0.0), (12.0, 40.0, 0.009, 0.0) })
        {
            System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
            double q = PDF.quantsr(p, k, df);
            double back = q == Constant.MISSING ? double.NaN : PDF.probsr(q, k, df);
            string what = $"{k} means, {df} degrees of freedom, lower tail {p}: value {q.ToString("R", inv)}, whose lower tail is {back.ToString("R", inv)}, in {clock.Elapsed.TotalSeconds:F1} seconds";
            Say(q != Constant.MISSING && Math.Abs(back - p) <= 1e-8 * p, what);
            if (value > 0)
                Say(Math.Abs(q - value) <= 1e-9 * value, $"{what}; the integration gives {value}");
        }
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  the small lower tails");
    }

    [STAThread]
    private static int Main(string[] args)
    {
        string which = args.Length > 0 ? args[0] : "all";
        string folder = Path.Combine(AppContext.BaseDirectory, "benchmarks");
        if (which is "all" or "benchmarks") Benchmarks(folder);
        if (which is "all" or "range") Range();
        if (which is "all" or "comparisons") Comparisons();
        if (which is "all" or "tails") Tails();
        Console.WriteLine();
        Console.WriteLine(failures == 0 ? $"ALL {checks} CHECKS PASS" : $"{failures} OF {checks} CHECKS FAILED");
        return failures == 0 ? 0 : 1;
    }
}
