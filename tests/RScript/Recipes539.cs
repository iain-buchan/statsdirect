using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using StatsDirect.Builtins;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.R;
using StatsDirect.TemplateProcessing;
using StatsDirect.Templates;
using StatsDirect.UI;
using StatsDirect.Utilities;

internal static partial class Program
{
    // The Continue in R recipes of the 5.3.9 review: scripts that stopped where the report completed, replayed another
    // analysis, or did not take the report's confidence fallback, and the native Royston K2 and Cox hazard-ratio limits.
    // Each case acquires its columns from worksheet cells through GridSelectionProcessor.FillFrameParameter (and the
    // CellArrayProcessor under it), runs the operation through the template processor, writes the script from the record
    // the report step left, runs it in R within a time limit, and compares R's figures with the native output bag.
    private static readonly string[] Recipes539Parts = { "gof", "kendall" };
    private static void CheckRecipes539(string folder, string only = null)
    {
        if (only != null && !Recipes539Parts.Contains(only)) throw new ArgumentException("Unknown 5.3.9 recipe check: " + only);
        RRecipes.Folder = Metadata("Recipes");
        Directory.CreateDirectory(folder);
        if (only is null or "gof") CheckGoodnessOfFit539(Path.Combine(folder, "gof"));
        if (only is null or "kendall") CheckKendall539(Path.Combine(folder, "kendall"));
        if (Failed539.Count > 0) throw new Exception("FAIL " + Failed539.Count + " of the 5.3.9 recipe cases: " + string.Join(" | ", Failed539));
    }

    // Normally the first failure stops the run, as in every other check. With STATSDIRECT_RECIPES539_CONTINUE=1, used to
    // show what fails before a fix, a failing case is noted with R's last words and the other cases still run.
    private static readonly List<string> Failed539 = new();
    private static void Case539(string label, Action body)
    {
        if (Environment.GetEnvironmentVariable("STATSDIRECT_RECIPES539_CONTINUE") != "1") { body(); return; }
        try { body(); }
        catch (Exception ex)
        {
            string[] lines = ex.Message.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries);
            string said = lines[0] + (lines.Length > 1 ? " ... " + string.Join(" / ", lines.Skip(Math.Max(1, lines.Length - 2))) : "");
            Failed539.Add(label + ": " + said);
            Console.WriteLine("FAILED CASE " + label + ": " + said);
        }
    }

    // A column of worksheet cells: its title in the first row, then its values.
    private static object[] Cells539(string title, IEnumerable<double> values) => new object[] { title }.Concat(values.Select(v => (object)v)).ToArray();

    // The answer to a frame prompt: the columns acquired from the cells as the grid acquires a selection,
    // through GridSelectionProcessor.FillFrameParameter with the frame's own mode, lengths and alignment.
    private static Func<ITemplateProcessor, Parameter, ParameterBag, ITemplateHost, ParameterBag> Select539(RecordCells cells, int rows, params int[] columns) =>
        (processor, parameter, context, host) =>
        {
            IGrid grid = DispatchProxy.Create<IGrid, AlignedGrid>();
            var proxy = (AlignedGrid)(object)grid; proxy.Cells = cells; proxy.Host = host;
            proxy.Selection = new CellSelection { LongestRowCount = rows };
            foreach (int c in columns)
                proxy.Selection.ColumnSelections.Add(new CellColumnSelection(cells) { ColumnIndex = c, RowIndex = 0, RowCount = rows, WorkbookPath = Workbook, WorksheetName = "Recipes 5.3.9" });
            Type type = typeof(CellSelection).Assembly.GetType("StatsDirect.UI.GridSelectionProcessor");
            try { return (ParameterBag)type.GetMethod("FillFrameParameter", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(Activator.CreateInstance(type, grid), new object[] { parameter, processor, context }); }
            catch (TargetInvocationException ex) { throw new InvalidOperationException(ex.GetBaseException().ToString(), ex); }
        };

    private static double Num539(ParameterBag bag, string key)
    {
        object value = bag[key].AsObject;
        if (value is string text) return double.Parse(Regex.Match(text, @"[-+]?(?:\d+\.?\d*|\.\d+)(?:[Ee][-+]?\d+)?").Value, inv);
        return Convert.ToDouble(value, inv);
    }
    private static double Found539(ParameterBag bag, string key) => Num539(DeferredBags538(bag).First(b => b.ContainsKey(key) && b[key]?.AsObject != null), key);
    private static IList<ParameterBag> Rows539(ParameterBag bag, string key) => (IList<ParameterBag>)bag[key].AsObject;
    private static string Vector539(IEnumerable<double> values) => RScriptWriter.Vector(values.Select(v => v == Constant.MISSING || double.IsNaN(v) ? null : (object)v).ToArray());
    private static string Equal539(string expression, IEnumerable<double> expected, double tolerance = 1e-9) =>
        "stopifnot(isTRUE(all.equal(unname(c(" + expression + ")), " + Vector539(expected) + ", tolerance = " + tolerance.ToString("R", inv) + ", check.attributes = FALSE)))\n";
    private static double[] Printed539(string text, string pattern) =>
        Regex.Match(text, pattern, RegexOptions.Multiline) is { Success: true } m ? m.Groups.Cast<System.Text.RegularExpressions.Group>().Skip(1).Select(g => double.Parse(g.Value, inv)).ToArray() : Array.Empty<double>();
    private const string Figure539 = @"(-?[0-9]+(?:\.[0-9]+)?(?:[eE][-+]?[0-9]+)?)";

    // The script of the run's record, run in R within the time limit with the assertions after it.
    private static string Replay539(OperationRun run, string assertion, string folder, Func<FramePointer, FrameValues> read = null, string setup = "", int timeout = 120)
    {
        RScript script = RScriptWriter.Write(Record(run), null, read);
        string label = Path.GetFileName(folder);
        Check(script.HasRecipe && !script.DataUnavailable && !script.Text.Contains("has changed in the workbook"), label + ": the record keeps or rereads the selected data and has its recipe");
        string output = DeferredR538(setup + script.Text + "\n" + assertion + "\ncat('RECIPES_539_OK\\n')\n", folder, timeout);
        Check(output.Contains("RECIPES_539_OK"), label + ": R reproduces the native report");
        return output;
    }

    // The run's record as saved before 5.2.3, which did not name the root analysis of a follow-on.
    private static JsonElement Legacy539(OperationRun run)
    {
        JsonObject record = JsonNode.Parse(run.Record).AsObject();
        record.Remove("rootOperation");
        return JsonSerializer.SerializeToElement(record);
    }

    // An ordering of 1..n with the given number of inversions (greedy Lehmer code).
    private static int[] Permutation539(int n, long inversions)
    {
        List<int> available = Enumerable.Range(1, n).ToList(); int[] values = new int[n];
        for (int j = 0; j < n; j++) { int k = (int)Math.Min(inversions, n - 1 - j); values[j] = available[k]; available.RemoveAt(k); inversions -= k; }
        return values;
    }

    // 1. Expected counts (or percentages, or proportions that do not add up to 1) are scaled to the observed total by the
    //    report; chisq.test needs rescale.p = TRUE to do the same.
    private static void CheckGoodnessOfFit539(string folder)
    {
        void Case(string label, double[] observed, double[] expected, bool simulate = false) => Case539("gof " + label, () =>
        {
            string[] names = observed.Select((_, i) => "Group " + (char)('A' + i)).ToArray();
            var cells = new RecordCells(Cells539("Observed", observed), Cells539("Expected", expected), new object[] { "Category" }.Concat(names).ToArray());
            int rows = observed.Length + 1;
            var (run, output) = Run("ChiSquareGoodnessOfFit", new() { ["observed"] = Select539(cells, rows, 0), ["expected"] = Select539(cells, rows, 1), ["names"] = Select539(cells, rows, 2) });
            ParameterBag bag = output.ParameterBag;
            string assertion = Equal539("sum(observed), test$parameter", new[] { Num539(bag, "n"), Num539(bag, "df") }) +
                Equal539("test$expected", Rows539(bag, "*frequencies").Select(r => Num539(r, "e"))) +
                (Num539(bag, "chi2") == 0 ? "stopifnot(abs(test$statistic) < 1e-12, abs(test$p.value - 1) < 1e-12)\n" : Equal539("test$statistic, test$p.value", new[] { Num539(bag, "chi2"), Num539(bag, "p") })) +
                "stopifnot(identical(names(observed), " + RScriptWriter.Vector(names) + "))\n";
            Replay539(run, assertion, Path.Combine(folder, label));
            if (!simulate) return;
            var (follow, _) = Run("ChiGFSimulateExactP", new() { ["iterations"] = 2000, ["seed"] = 11, ["ci"] = .99 }, bag);
            Replay539(follow, assertion, Path.Combine(folder, label + "-simulated"));
        });
        Case("counts-equal-total", new double[] { 10, 20, 30, 25, 15 }, new double[] { 10, 20, 30, 25, 15 }, simulate: true);
        Case("counts-other-total", new double[] { 12, 18, 33, 22, 15 }, new double[] { 20, 40, 60, 50, 30 });
        Case("percentages", new double[] { 67, 83, 29, 8 }, new double[] { 44, 45, 8, 3 });
        Case("proportions-below-one", new double[] { 9, 21, 28, 30, 12 }, new double[] { .1, .2, .3, .25, .1 });
        Case("proportions-help", new double[] { 67, 83, 29, 8 }, new double[] { .44, .45, .08, .03 });
    }

    // 2. Kendall's rank correlation: the replay used to stop on any tie, gave R's normal approximation as the exact test from
    //    50 pairs, and did not keep tau's limits within -1 and 1. The report's exact P counts the orderings without ties
    //    through 1,000 pairs and uses its Edgeworth series above (kendp), whether or not there are ties.
    private static void CheckKendall539(string folder)
    {
        void Case(string label, double[] x, double[] y, double confidence = .95, int timeout = 120) => Case539("kendall " + label, () =>
        {
            int n = x.Length;
            var cells = new RecordCells(Cells539("First ranking", x), Cells539("Second ranking", y));
            var (run, output) = Run("Kendall", new() { ["data"] = Select539(cells, n + 1, 0, 1), ["gamma"] = confidence });
            ParameterBag b = output.ParameterBag;
            double used = confidence > 0 && confidence < 1 ? confidence : .95;
            bool ties = Num539(b, "tie") > 0;
            Check(Num539(b, "pc") == used * 100, label + ": the native report uses its confidence fallback");
            string assertion = "stopifnot(n == " + Num539(b, "obs").ToString(inv) + ", confidence == " + used.ToString("R", inv) + ")\n" +
                Equal539("concordant, discordant, tied, score", new[] { "con", "dis", "tie", "s" }.Select(k => Num539(b, k))) +
                Equal539("se, score / (concordant + discordant), tau, limits", new[] { "ses", "gam", "tau", "ll", "ul" }.Select(k => Num539(b, k))) +
                Equal539("upper", new[] { Num539(b, "p_uexact") }) + Equal539("lower", new[] { Num539(b, "p_lexact") }) +
                Equal539("min(1, 2 * min(upper, lower))", new[] { Num539(b, "p_2exact") });
            string text = Replay539(run, assertion, Path.Combine(folder, label), timeout: timeout);
            Check(text.Contains("Kendall's tau b =") == ties && text.Contains("Kendall's tau =") == !ties, label + ": tau b is named when there are ties, as in the report");
            var z = Regex.Matches(text, @"^z = ([-0-9.]+)\s+Upper side P = ([0-9.]+)\s+Lower side P = ([0-9.]+)\s+Two sided P = ([0-9.]+)", RegexOptions.Multiline);
            bool Near(Match m, string zKey, string upperKey, string lowerKey, string twoKey) =>
                Math.Abs(double.Parse(m.Groups[1].Value, inv) - Num539(b, zKey)) <= 5.1e-7 * Math.Max(1, Math.Abs(Num539(b, zKey))) &&
                new[] { (2, upperKey), (3, lowerKey), (4, twoKey) }.All(g => Math.Abs(double.Parse(m.Groups[g.Item1].Value, inv) - Num539(b, g.Item2)) <= 5.1e-5);
            Check(z.Count == 2 && Near(z[0], "kz", "p_u", "p_l", "p_2") && Near(z[1], "kzcc", "p_ucc", "p_lcc", "p_2cc"), label + ": the normal approximations, with and without the continuity correction, are the report's");
        });
        double[] career = { 4, 10, 3, 1, 9, 2, 6, 7, 8, 5 }, psychology = { 5, 8, 6, 2, 10, 3, 9, 4, 7, 1 };
        Case("help-example", career, psychology);
        foreach (int n in new[] { 49, 50, 80, 171, 400, 1000, 1001, 1500 })
        {
            long pairs = (long)n * (n - 1) / 2;
            int[] order = Permutation539(n, (long)(pairs * .48));
            Case("untied-" + n, Enumerable.Range(1, n).Select(i => 100 + 2.5 * i).ToArray(), order.Select(v => (double)v).ToArray(), .9, timeout: 240);
        }
        Case("ties-x", new double[] { 1, 2, 2, 3, 4, 4, 4, 5, 6, 7, 8, 8 }, new double[] { 3, 1, 4, 2, 6, 5, 9, 7, 8, 12, 10, 11 });
        Case("ties-y", new double[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 }, new double[] { 2, 1, 1, 3, 5, 5, 4, 7, 7, 7, 9, 8, 10, 10, 12 });
        Case("ties-in-both", new double[] { 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7, 8, 8, 9, 9, 10, 10 }, new double[] { 1, 1, 3, 2, 2, 4, 4, 4, 6, 5, 5, 7, 9, 9, 8, 8, 10, 12, 11, 11 });
        Case("ties-heavy-60", Enumerable.Range(0, 60).Select(i => (double)(i % 6)).ToArray(), Enumerable.Range(0, 60).Select(i => (double)((i * 7 + i / 6) % 5)).ToArray(), .99);
        Case("ties-1100", Enumerable.Range(0, 1100).Select(i => (double)(i / 3)).ToArray(), Permutation539(1100, 1100L * 1099 / 2 * 47 / 100).Select(v => (double)(v / 2)).ToArray(), timeout: 240);
        Case("clamped-upper", new double[] { 1, 2, 3, 4, 5, 6, 7, 8 }, new double[] { 1, 2, 3, 4, 5, 6, 8, 7 });
        Case("clamped-lower", new double[] { 1, 2, 3, 4, 5, 6, 7, 8 }, new double[] { 8, 7, 6, 5, 4, 3, 1, 2 });
        foreach (double confidence in new[] { 0d, 1d }) Case("confidence-" + confidence, career, psychology, confidence);
        var (_, clamped) = Run("Kendall", new() { ["data"] = Select539(new RecordCells(Cells539("x", new double[] { 1, 2, 3, 4, 5, 6, 7, 8 }), Cells539("y", new double[] { 1, 2, 3, 4, 5, 6, 8, 7 })), 9, 0, 1), ["gamma"] = .95 });
        Check(Num539(clamped.ParameterBag, "ul") == 1, "the clamped case reaches the report's upper bound of tau");
    }

}
