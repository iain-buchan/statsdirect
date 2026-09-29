// The Sample Size menu: checks by calculation.  Each report of the menu is given the inputs of a case, and what it gives the
// report is compared with figures that are worked out from the definitions of the methods (in the folder benchmarks, with the
// script that made them): sample sizes, numbers of controls, the notes of a report, and the words of what is refused.
using System.Globalization;
using System.Reflection;
using StatsDirect.Builtins;
using StatsDirect.Templates;

internal static class Program
{
    private static readonly CultureInfo inv = CultureInfo.InvariantCulture;
    private static int failures, checks;

    private static void Say(bool ok, string what)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("FAIL  " + what); }
    }

    private static string Text(object o) => o switch
    {
        null => "null",
        double d => d.ToString("R", inv),
        IFormattable f => f.ToString(null, inv),
        _ => o.ToString()
    };

    // ---- a report of the menu with the inputs of a case (key=value, with semicolons between them): everything that it gives
    // the report, by the name of the output; the rows of a list as "*list.rows" and "*list[i].output"; a refusal as "refused"
    private static Dictionary<string, string> Report(string name, string inputs)
    {
        Dictionary<string, string> given = new();
        ParameterBag p = new();
        foreach (string pair in inputs.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            int eq = pair.IndexOf('=');
            string key = pair.Substring(0, eq), value = pair.Substring(eq + 1);
            if (double.TryParse(value, NumberStyles.Float, inv, out double number)) p.AddInput(key, number);
            else p.AddInput(key, value);
        }
        try
        {
            MethodInfo report = typeof(Formula).GetMethod(name, BindingFlags.Public | BindingFlags.Static);
            StepOutput step = (StepOutput)report.Invoke(null, new object[] { p });
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
                else
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

    // ---- whether what is given is what is expected: a number within a part in a thousand million, so that a number of
    // subjects below a thousand million is to be the same; of a refusal the words that are expected are to be in it
    private static bool Agrees(string output, string given, string expected)
    {
        if (output == "refused") return given.Contains(expected, StringComparison.Ordinal);
        if (double.TryParse(given, NumberStyles.Float, inv, out double x) && double.TryParse(expected, NumberStyles.Float, inv, out double y))
            return Math.Abs(x - y) <= 1e-9 * Math.Abs(y);
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
                string group = f[0].Replace("RptSize", "") + (output == "refused" ? ": what is refused" : output.StartsWith("*") ? ": the notes and the controls of the report" : ": " + output);
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
