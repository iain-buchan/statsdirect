// The Parametric Methods menu: checks by calculation.  Each report of the menu is given the inputs of a case, and what it gives
// the report is compared with figures that are worked out from the definitions of the methods (in the folder benchmarks, with
// the script that made them): the t tests, the normal distribution (z) tests, the F (variance ratio) test, the confidence limits
// of a Poisson mean, the reference range, the tests of normality, and the words of what is refused.
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

// what the reports ask of the program about them: numbers are given to a report in all their figures, and progress is not shown
internal sealed class Host : IPreferencesAndProgressBar
{
    private readonly NoProgress none = new();
    public string RoundU(double amount) => amount.ToString("R", CultureInfo.InvariantCulture);
    public string pval(double p) => p.ToString("R", CultureInfo.InvariantCulture);
    public string pval_half(double p) => p.ToString("R", CultureInfo.InvariantCulture);
    public SDPreferences Preferences { get; } = DispatchProxy.Create<SDPreferences, PreferencesProxy>();
    public IProgressBar StartProgress(string operationDescription, bool provideProgress, bool display = true) => none;
}

internal static class Program
{
    private static readonly CultureInfo inv = CultureInfo.InvariantCulture;
    private static readonly Host host = new();
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

    // ---- a report of the menu with the inputs of a case (key=value, with semicolons between them; "data" has the columns of a
    // table, with bars between the columns and commas between the numbers, and "*" for a number that is missing): everything
    // that it gives the report, by the name of the output; the rows of a list as "*list.rows" and "*list[i].output"; a
    // refusal as "refused".  The numbers of observations of a summary are whole numbers, and "true" and "false" are given as such.
    private static Dictionary<string, string> Report(string name, string inputs)
    {
        Dictionary<string, string> given = new();
        ParameterBag p = new();
        foreach (string pair in inputs.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            int eq = pair.IndexOf('=');
            string key = pair.Substring(0, eq), value = pair.Substring(eq + 1);
            if (key == "data")
            {
                DataFrame frame = new();
                int column = 0;
                foreach (string c in value.Split('|'))
                    frame.Variables.Add(new DoubleVariable(c.Split(',').Select(v => v == "*" ? double.MinValue : double.Parse(v, inv)).ToArray(), "c" + (++column).ToString(inv)));
                p.AddInput(key, frame);
            }
            else if (key is "nx" or "nx1" or "nx2") p.AddInput(key, int.Parse(value, inv));
            else if (value is "true" or "false") p.AddInput(key, value == "true");
            else if (double.TryParse(value, NumberStyles.Float, inv, out double number)) p.AddInput(key, number);
            else p.AddInput(key, value);
        }
        try
        {
            MethodInfo report = typeof(Parametric).GetMethod(name, BindingFlags.Public | BindingFlags.Static);
            object[] arguments = report.GetParameters().Length == 2 ? new object[] { host, p } : new object[] { p };
            StepOutput step = (StepOutput)report.Invoke(null, arguments);
            if (step == null) { given["refused"] = "no report"; return given; }
            ParameterBag bag = step.ParameterBag;
            foreach (string k in bag.Keys)
            {
                object o = bag[k].AsObject;
                if (o is System.Collections.IEnumerable rows && o is not string)
                {
                    int i = 0;
                    foreach (object row in rows)
                    {
                        i++;
                        if (row is ParameterBag r)
                            foreach (string rk in r.Keys) given[$"{k}[{i}].{rk}"] = Text(r[rk].AsObject);
                    }
                    given[k + ".rows"] = i.ToString(inv);
                }
                else if (o is not StatsDirect.Charting.ChartDefinition)
                    given[k] = Text(o);
            }
        }
        catch (Exception ex)
        {
            Exception inner = ex.InnerException ?? ex;
            given["refused"] = inner.GetType().Name + ": " + inner.Message.Replace('\n', ' ').Replace('\r', ' ');
        }
        return given;
    }

    // ---- whether what is given is what is expected.  A figure may differ by a part in a thousand million; a figure that a
    // report has with a comma after it is compared as a figure, and is to have the comma; of a refusal the words that are
    // expected are to be in it; anything else is to be the same text.
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
        if (Number(given, out double x) && Number(expected, out double y)) return Near(x, y);
        if (given.EndsWith(',') && expected.EndsWith(',') && Number(given.TrimEnd(','), out x) && Number(expected.TrimEnd(','), out y))
            return Near(x, y);
        return given == expected;
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
            if (seconds > longest) { longest = seconds; longestCase = f[0] + " " + f[2]; }
            foreach ((string output, string figure) in expected[f[0] + "|" + f[1]])
            {
                compared++;
                string plain = System.Text.RegularExpressions.Regex.Replace(output, @"\[\d+\]", "[]");
                string group = f[0].Replace("Rpt", "") + (output == "refused" ? ": what is refused" : ": " + plain);
                if (!kinds.ContainsKey(group)) kinds[group] = (0, 0, "");
                string shown = given.ContainsKey(output) ? given[output] : given.ContainsKey("refused") ? "refused, " + given["refused"] : "absent";
                (int n, int bad, string what) k = kinds[group];
                k.n++;
                if (!Agrees(output, shown, figure))
                {
                    k.bad++;
                    if (k.what.Length == 0) k.what = $"{f[2]}: {output} is {shown}, expected {figure}";
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
        Benchmarks(folder);
        Console.WriteLine();
        Console.WriteLine(failures == 0 ? $"ALL {checks} CHECKS PASS" : $"{failures} OF {checks} CHECKS FAILED");
        return failures == 0 ? 0 : 1;
    }
}
