// The Descriptive menu: checks by calculation.  Each report of the menu is given the inputs of a case, and what it gives the
// report is compared with figures that are worked out from the definitions of the statistics (in the folder benchmarks, with
// the script that made them): the univariate summary with weights and without, the quick summary, the time series summary
// with its bootstrap, and the words of what is refused.
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
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

// what the reports ask of the program about them: a progress bar that shows nothing, the preferences, and the text of the quick
// summary, which the program would show in a window
public class DescriptiveHost : DispatchProxy
{
    public static string Shown;
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
            case "Amend":
                if (arguments[0] is SummaryStatisticsOptions options) Shown = options.Text;
                return null;
        }
        Type type = method.ReturnType;
        return type == typeof(void) ? null : type.IsValueType ? Activator.CreateInstance(type) : null;
    }

    public static ITemplateHost New() => DispatchProxy.Create<ITemplateHost, DescriptiveHost>();
}

internal static class Program
{
    private static readonly CultureInfo inv = CultureInfo.InvariantCulture;
    private static readonly ITemplateHost host = DescriptiveHost.New();
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

    // a column of labels, each with the number of its first place among the labels that differ, from 0, as the program numbers
    // the labels of a column of the worksheet
    private static DataFrame Labels(string value, string title)
    {
        List<string> found = new();
        List<int> counts = new();
        string[] cells = value.Split(',');
        ClassifierVariable v = new() { Title = title };
        v.EnsureLength(cells.Length);
        for (int r = 0; r < cells.Length; r++)
        {
            int at = found.IndexOf(cells[r]);
            if (at < 0) { found.Add(cells[r]); counts.Add(0); at = found.Count - 1; }
            counts[at]++;
            v.Data[r] = at;
        }
        v.EnsureGroups(found.Count);
        for (int i = 0; i < found.Count; i++) v.Groups[i] = new StatsDirect.Data.Group(found[i], i) { NBin = counts[i] };
        DataFrame frame = new();
        frame.Variables.Add(v);
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
    // the report, the lines of the text of the quick summary as "text.name", a refusal as "refused"
    private static Dictionary<string, string> Report(string name, string inputs)
    {
        Dictionary<string, string> given = new();
        ParameterBag p = new();
        foreach (string pair in inputs.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            int eq = pair.IndexOf('=');
            string key = pair.Substring(0, eq), value = pair.Substring(eq + 1);
            if (key is "data" or "weights" or "times" or "observations") p.AddInput(key, Numbers(value, key == "data" ? "c" : key));
            else if (key is "subjectIds" or "groups") p.AddInput(key, Labels(value, key));
            else if (key is "seed" or "iterations") p.AddInput(key, int.Parse(value, inv));
            else if (key == "report-centile-type") p.AddInput(key, value);
            else if (value is "true" or "false") p.AddInput(key, value == "true");
            else if (double.TryParse(value, NumberStyles.Float, inv, out double number)) p.AddInput(key, number);
            else p.AddInput(key, value);
        }
        DescriptiveHost.Shown = null;
        try
        {
            MethodInfo report = typeof(Describe).GetMethod(name, BindingFlags.Public | BindingFlags.Static);
            object[] arguments = report.GetParameters().Length == 2 ? new object[] { host, p } : new object[] { p };
            StepOutput step = (StepOutput)report.Invoke(null, arguments);
            if (step == null) { given["refused"] = "no report"; return given; }
            if (step.ParameterBag != null) Collect("", step.ParameterBag, given);
            if (DescriptiveHost.Shown != null)
                foreach (string shown in DescriptiveHost.Shown.Split('\n'))
                {
                    string s = shown.TrimEnd('\r');
                    if (s.Trim().Length == 0) continue;
                    if (s.StartsWith("Title: ")) { given["text.Title"] = s.Substring(7); continue; }
                    // the name of a line has 24 places
                    given["text." + (s.Length > 24 ? s.Substring(0, 24).Trim() : s.Trim())] = s.Length > 24 ? s.Substring(24) : "";
                }
        }
        catch (Exception ex)
        {
            Exception inner = ex.InnerException ?? ex;
            given["refused"] = inner.GetType().Name + ": " + inner.Message.Replace('\n', ' ').Replace('\r', ' ');
        }
        return given;
    }

    // ---- whether what is given is what is expected.  A figure may differ by a part in a thousand million.  A line of the quick
    // summary has 6 decimal places and 15 figures at most, and may differ by half of the last of them; if it is given as a
    // power of ten, by that part of the figure.  An asterisk is a figure that there is none of.  Of a refusal the words that
    // are expected are to be in it; anything else is to be the same text.
    private static bool Number(string text, out double value)
    {
        if (text == "Infinity") { value = double.PositiveInfinity; return true; }
        if (text == "-Infinity") { value = double.NegativeInfinity; return true; }
        return double.TryParse(text, NumberStyles.Float, inv, out value);
    }

    private static bool Near(double x, double y) => x == y || Math.Abs(x - y) <= 1e-9 * Math.Abs(y);

    private static bool Agrees(string output, string given, string expected)
    {
        if (output == "refused") return given.Contains(expected, StringComparison.Ordinal);
        if (given == "*") given = "missing";
        if (Number(given, out double x) && Number(expected, out double y))
        {
            if (!output.StartsWith("text.", StringComparison.Ordinal)) return Near(x, y);
            double figures = y == 0 ? 0 : 0.501 * Math.Pow(10, Math.Floor(Math.Log10(Math.Abs(y))) - 14);
            double room = given.Contains('E') ? 5.01e-7 * Math.Abs(y) : Math.Max(5.01e-7, figures);
            return x == y || Math.Abs(x - y) <= room;
        }
        return given == expected;
    }

    // the kind of an output: its name without the numbers of its rows; a statistic of the summary keeps its number
    private static string Kind(string report, string output)
    {
        if (output == "refused") return report.Replace("Rpt", "") + ": what is refused";
        Match statistic = Regex.Match(output, @"^\*fields\[(\d+)\]\.\*results\[\d+\]\.result$");
        string plain = statistic.Success ? "statistic " + statistic.Groups[1].Value : Regex.Replace(output, @"\[\d+\]", "[]");
        return report.Replace("Rpt", "") + ": " + plain;
    }

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

    // ---- the bootstrap of the time series summary.  The benchmark is a bootstrap that was made 10 times with other random
    // numbers: a figure of the program is to be within 4.5 standard deviations of the 10 of their mean, the standard
    // deviation being that of the difference of one bootstrap from the mean of 10; where the 10 are the same the figure is to be
    // theirs.  The program is given a seed, so that its figures are the same in every run.
    private static void Bootstrap(string folder)
    {
        int before = failures;
        Console.WriteLine("The bootstrap of the time series summary");
        Dictionary<string, List<(string output, double centre, double spread)>> expected = new();
        foreach (string line in File.ReadAllLines(Path.Combine(folder, "bootstrap-expected.txt")))
        {
            string[] f = line.Split('\t');
            if (f.Length < 3) continue;
            string[] k = f[0].Split('|', 3);
            string key = k[0] + "|" + k[1];
            if (!expected.ContainsKey(key)) expected[key] = new();
            expected[key].Add((k[2], double.Parse(f[1], inv), double.Parse(f[2], inv)));
        }
        int compared = 0, cases = 0;
        double longest = 0, furthest = 0;
        foreach (string line in File.ReadAllLines(Path.Combine(folder, "bootstrap-cases.txt")))
        {
            string[] f = line.Split('\t');
            if (f.Length < 3) continue;
            cases++;
            System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
            Dictionary<string, string> given = Report(f[0], f[2]);
            longest = Math.Max(longest, clock.Elapsed.TotalSeconds);
            int bad = 0; string what = "";
            foreach ((string output, double centre, double spread) in expected[f[0] + "|" + f[1]])
            {
                compared++;
                bool ok = given.ContainsKey(output) && Number(given[output], out double x) && double.IsFinite(x);
                if (ok)
                {
                    Number(given[output], out x);
                    double by = spread > 0 ? Math.Abs(x - centre) / (spread * Math.Sqrt(1.1)) : 0;
                    ok = Near(x, centre) || (spread > 0 && by <= 4.5);
                    if (spread > 0) furthest = Math.Max(furthest, by);
                }
                if (!ok)
                {
                    bad++;
                    if (what.Length == 0) what = $"{output} is {(given.ContainsKey(output) ? given[output] : given.ContainsKey("refused") ? "refused, " + given["refused"] : "absent")}, expected {centre.ToString("R", inv)} (standard deviation {spread.ToString("R", inv)})";
                }
            }
            Say(bad == 0, $"bootstrap, {f[1]}: {bad} figures are not within the chance of the draws of their benchmarks; the first is {what}");
        }
        Say(longest < 30, $"the bootstrap that takes longest takes {longest:F1} seconds");
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  the bootstrap: {cases} cases, {compared} figures compared; the furthest is {furthest:F2} standard deviations from its benchmark; the case that takes longest takes {longest:F2} seconds");
    }

    private static int Main(string[] args)
    {
        string folder = Path.Combine(AppContext.BaseDirectory, "benchmarks");
        HashSet<string> parts = new(args);
        if (parts.Count == 0 || parts.Contains("benchmarks")) Benchmarks(folder);
        if (parts.Count == 0 || parts.Contains("bootstrap")) Bootstrap(folder);
        Console.WriteLine();
        Console.WriteLine(failures == 0 ? $"ALL {checks} CHECKS PASS" : $"{failures} OF {checks} CHECKS FAILED");
        return failures == 0 ? 0 : 1;
    }
}
