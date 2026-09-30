// The Analysis of Variance menu: checks by calculation.  Each report of the menu is given the inputs of a case, and what it gives
// the report is compared with figures that are worked out from the definitions of the methods (in the folder benchmarks, with
// the script that made them): one way, two way, replicated two way, fully nested, Latin square and crossover analyses, the
// comparisons that follow them (Bonferroni, Tukey, Scheffe, Newman-Keuls and Dunnett), and the equality of variance tests.
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

// what the reports ask of the program about them: a progress bar that shows nothing, the preferences, and the words of a
// message, which the program would show in a window
public class AnovaHost : DispatchProxy
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

    public static ITemplateHost New() => DispatchProxy.Create<ITemplateHost, AnovaHost>();
}

internal static class Program
{
    private static readonly CultureInfo inv = CultureInfo.InvariantCulture;
    private static readonly ITemplateHost host = AnovaHost.New();
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

    private static DoubleVariable Column(string c, string title) =>
        new(c.Length == 0 ? new double[0] : c.Split(',').Select(v => v == "*" ? double.MinValue : double.Parse(v, inv)).ToArray(), title);

    // a table of numbers: bars between the columns, commas between the numbers, "*" for a number that is missing
    private static DataFrame Numbers(string value, string title)
    {
        DataFrame frame = new();
        int column = 0;
        foreach (string c in value.Split('|'))
            frame.Variables.Add(Column(c, title + (++column).ToString(inv)));
        return frame;
    }

    // a two dimensional table, tildes between its frames: the blocks of a replicated two way analysis, each a table of the
    // treatments; the groups of a nested analysis, each a table of the subgroups
    private static DataFrame2D Numbers2D(string value)
    {
        DataFrame2D frame = new() { Name = "d" };
        int f = 0;
        foreach (string block in value.Split('~'))
        {
            f++;
            IList<IVariable> list = new List<IVariable>();
            int column = 0;
            foreach (string c in block.Split('|'))
                list.Add(Column(c, "f" + f.ToString(inv) + "c" + (++column).ToString(inv)));
            frame.Variables.Add(list);
        }
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
            else if (o is double[] doubles) given[prefix + k] = string.Join(",", doubles.Select(d => Text(d)));
            else if (o is int[] ints) given[prefix + k] = string.Join(",", ints);
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

    // the inputs of a case (key=value, with semicolons between them); "after" names the analysis that the report follows
    private static ParameterBag Inputs(string inputs, out string after)
    {
        after = null;
        ParameterBag p = new();
        foreach (string pair in inputs.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            int eq = pair.IndexOf('=');
            string name = pair.Substring(0, eq), value = pair.Substring(eq + 1);
            if (name == "after") after = value;
            else if (name == "data2d") p.AddInput(name, Numbers2D(value));
            else if (name is "data" or "observations" or "column" or "row" or "treatment" or "group1drug" or "group1placebo" or "group1baseline" or "group2drug" or "group2placebo" or "group2baseline")
                p.AddInput(name, Numbers(value, name == "data" ? "c" : name));
            else if (name is "variables" or "indexvariable") p.AddInput(name, value.Split(',').Select(v => int.Parse(v, inv)).ToArray());
            else if (name is "comparisons") p.AddInput(name, int.Parse(value, inv));
            else if (value is "true" or "false") p.AddInput(name, value == "true");
            else if (double.TryParse(value, NumberStyles.Float, inv, out double number)) p.AddInput(name, number);
            else p.AddInput(name, value);
        }
        return p;
    }

    private static StepOutput Run(string name, ParameterBag p)
    {
        MethodInfo report = typeof(Anova).GetMethod(name, BindingFlags.Public | BindingFlags.Static);
        object[] arguments = report.GetParameters().Length == 2 ? new object[] { host, p } : new object[] { p };
        return (StepOutput)report.Invoke(null, arguments);
    }

    // ---- a report of the menu with the inputs of a case: everything that it gives the report, and a refusal as "refused".  A
    // report that follows an analysis (the comparisons, the nested means) is given what that analysis caches for it, as the
    // program gives it
    private static Dictionary<string, string> Report(string name, string inputs)
    {
        Dictionary<string, string> given = new();
        AnovaHost.Messages.Clear();
        try
        {
            ParameterBag p = Inputs(inputs, out string after);
            if (after != null)
            {
                StepOutput first = Run(after, Inputs(inputs, out _));
                foreach (string k in first.ParameterBag.Keys)
                {
                    object o;
                    try { o = first.ParameterBag[k].AsObject; } catch { continue; }
                    if (!p.ContainsKey(k)) p.AddInput(k, o);
                }
            }
            StepOutput step = Run(name, p);
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
    // figure that there is none of; a list that is not there has no rows.  Of a refusal the words that are expected are to be in
    // it; anything else is to be the same text.
    private static bool Number(string text, out double value)
    {
        if (text == "Infinity") { value = double.PositiveInfinity; return true; }
        if (text == "-Infinity") { value = double.NegativeInfinity; return true; }
        return double.TryParse(text, NumberStyles.Float, inv, out value);
    }

    private static bool Agrees(string output, string given, string expected)
    {
        if (output == "refused") return given.Contains(expected, StringComparison.Ordinal);
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

    private static int Main(string[] args)
    {
        string folder = Path.Combine(AppContext.BaseDirectory, "benchmarks");
        HashSet<string> parts = new(args);
        if (parts.Count == 0 || parts.Contains("benchmarks")) Benchmarks(folder);
        Console.WriteLine();
        Console.WriteLine(failures == 0 ? $"ALL {checks} CHECKS PASS" : $"{failures} OF {checks} CHECKS FAILED");
        return failures == 0 ? 0 : 1;
    }
}
