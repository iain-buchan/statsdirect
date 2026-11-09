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
    private static readonly string[] Recipes539Parts = { "gof", "kendall", "mann-whitney", "routing", "agreement", "fallback", "paired", "pca" };
    private static void CheckRecipes539(string folder, string only = null)
    {
        if (only != null && !Recipes539Parts.Contains(only)) throw new ArgumentException("Unknown 5.3.9 recipe check: " + only);
        RRecipes.Folder = Metadata("Recipes");
        Directory.CreateDirectory(folder);
        if (only is null or "gof") CheckGoodnessOfFit539(Path.Combine(folder, "gof"));
        if (only is null or "kendall") CheckKendall539(Path.Combine(folder, "kendall"));
        if (only is null or "mann-whitney") CheckMannWhitney539(Path.Combine(folder, "mann-whitney"));
        if (only is null or "routing") CheckFollowOnRouting539(Path.Combine(folder, "routing"));
        if (only is null or "agreement") CheckAgreement539(Path.Combine(folder, "agreement"));
        if (only is null or "fallback") CheckConfidenceFallback539(Path.Combine(folder, "fallback"));
        if (only is null or "paired") CheckPairedNearConstant539(Path.Combine(folder, "paired"));
        if (only is null or "pca") CheckPcaTitles539(Path.Combine(folder, "pca"));
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

    // 3. Mann-Whitney: the report uses the normal approximation when both samples are above 100 or the exact distribution would
    //    need more than a million cells, and K for the difference from the normal approximation when the exact distribution
    //    of U would be too large; the replay always built the dense exact table (200 + 200 took 47 s and 3.2 GB).
    private static void CheckMannWhitney539(string folder)
    {
        void Case(string label, double[] x, double[] y, double confidence, bool normal, bool approximateK, int timeout = 90) => Case539("mann-whitney " + label, () =>
        {
            Check(x.Concat(y).Distinct().Count() == x.Length + y.Length || label.StartsWith("ties", StringComparison.Ordinal) || label.StartsWith("confidence", StringComparison.Ordinal), label + ": an untied case has no ties");
            var cells = new RecordCells(Cells539("First sample", x), Cells539("Second sample", y));
            int rows = Math.Max(x.Length, y.Length) + 1;
            var (run, output) = Run("MannWhitney", new() { ["data"] = Select539(cells, rows, 0, 1), ["gamma"] = confidence });
            ParameterBag b = output.ParameterBag;
            double used = confidence > 0 && confidence < 1 ? confidence : .95;
            string stats = Convert.ToString(b["stats"].AsObject, inv);
            Check(stats.StartsWith("Normalised statistic", StringComparison.Ordinal) == normal, label + ": the case exercises the report's " + (normal ? "normal approximation" : "exact distribution"));
            ParameterBag conf = Rows539(b, "*conf")[0];
            string k = Convert.ToString(conf["k"].AsObject, inv);
            Check(k.EndsWith("(approx)", StringComparison.Ordinal) == approximateK, label + ": the case exercises the report's " + (approximateK ? "approximate" : "exact") + " K");
            string assertion = "stopifnot(confidence == " + used.ToString("R", inv) + ", K == " + Regex.Match(k, @"^\d+").Value + ")\n" +
                Equal539("lower", new[] { Num539(b, "p_l") }) + Equal539("upper", new[] { Num539(b, "p_u") }) + Equal539("min(1, 2 * min(lower, upper))", new[] { Num539(b, "p_2") }) +
                Equal539("theta, ci", new[] { "theta", "tll", "tul" }.Select(key => Num539(b, key)), 1e-8) +
                Equal539("median(d), d[K], rev(d)[K]", new[] { "median", "from", "to" }.Select(key => Num539(conf, key)), 1e-12);
            string text = Replay539(run, assertion, Path.Combine(folder, label), cells.Read, timeout: timeout);
            Match coverage = Regex.Match(text, @"^([0-9.]+)% confidence interval for the difference: K = (\d+)( \(approx\))?\s*$", RegexOptions.Multiline);
            Check(coverage.Success && Math.Abs(double.Parse(coverage.Groups[1].Value, inv) - Num539(conf, "pc")) < .0051 && coverage.Groups[3].Success == approximateK,
                label + ": the coverage and K of the interval are printed as the report has them");
            Check(text.Contains("Normalised statistic") == normal, label + ": the P values say whether they are exact or from the normal approximation");
        });
        double[] Values(int n, double offset, bool ties) => Enumerable.Range(0, n).Select(i => ties ? Math.Round(20 + 8 * Math.Sin(i * .7 + offset) + i * 7 % 5) : 20 + 8 * Math.Sin(i * .7 + offset) + i / 997.0 + offset / 1e5).ToArray();
        Case("ties-200-200", Values(200, 0, true), Values(200, 1.3, true), .95, normal: true, approximateK: false);
        Case("untied-200-200", Values(200, 0, false), Values(200, 1.3, false), .95, normal: true, approximateK: false);
        Case("ties-60-300", Values(60, 0, true), Values(300, 2.1, true), .9, normal: true, approximateK: false);
        Case("untied-exact-120-60", Values(120, 0, false), Values(60, .4, false), .95, normal: false, approximateK: false);
        // inside the report's exact range, where the dense table of the rank sums would need over 5 GB
        Case("untied-exact-100-2500", Values(100, 0, false), Values(2500, .9, false), .95, normal: false, approximateK: true);
        Case("untied-101-2000", Values(101, 0, false), Values(2000, .9, false), .9, normal: true, approximateK: true);
        double[] farm = { 14.8, 7.3, 5.6, 6.3, 9, 4.2, 10.6, 12.5, 12.9, 16.1, 11.4, 2.7 }, town = { 12.7, 14.2, 12.6, 2.1, 17.7, 11.8, 16.9, 7.9, 16, 10.6, 5.6, 5.6, 7.6, 11.3, 8.3, 6.7, 3.6, 1, 2.4, 6.4, 9.1, 6.7, 18.6, 3.2, 6.2, 6.1, 15.3, 10.6, 1.8, 5.9, 9.9, 10.6, 14.8, 5, 2.6, 4 };
        foreach (double confidence in new[] { 0d, 1d }) Case("confidence-" + confidence, farm, town, confidence, normal: false, approximateK: false);
    }

    // 4. The multiple comparisons continue a one way, two way or replicated two way analysis of variance, and the regression
    //    analysis of variance a multiple linear or polynomial regression: each follow-on replays the analysis it continues,
    //    whose residual mean square and degrees of freedom are those the native comparisons use.
    private static void CheckFollowOnRouting539(string folder)
    {
        string[] comparisons = { "Tukey", "Bonferroni", "Scheffe", "NewmanKeuls", "Dunnett" };
        Dictionary<string, object> Answers(string op) => op switch
        {
            "Bonferroni" => new() { ["gamma"] = .95, ["variables"] = new[] { 0, 1 }, ["comparisons"] = 6 },
            "Dunnett" => new() { ["gamma"] = .95, ["indexvariable"] = new[] { 0 } },
            _ => new() { ["gamma"] = .95 }
        };
        void Comparisons(string root, ParameterBag rootBag, string file, string table, double residualMeanSquare, double residualDf)
        {
            foreach (string op in comparisons) Case539("routing " + op + " after " + root, () =>
            {
                var (run, output) = Run(op, Answers(op), rootBag);
                JsonElement record = Record(run);
                Check(record.GetProperty("rootOperation").GetString() == root && RRecipes.Find(record)?.File == file, op + " after " + root + " is replayed by " + file);
                RScript legacy = RScriptWriter.Write(Legacy539(run), null, null);
                Check(RRecipes.Find(Legacy539(run)) == null && !legacy.HasRecipe && legacy.Detail.Contains("does not identify its original analysis", StringComparison.Ordinal),
                    op + " after " + root + " saved without its root analysis (before 5.2.3) asks for a rerun rather than guessing the analysis");
                ParameterBag b = output.ParameterBag;
                string error = op switch
                {
                    "Tukey" => Equal539("sqrt(anova(fit)['Residuals', 'Mean Sq'])", new[] { Num539(b, "psd") }),
                    "Bonferroni" => Equal539("anova(fit)['Residuals', 'Df']", new[] { Num539(b, "df") }),
                    _ => ""
                };
                Replay539(run, table + Equal539("anova(fit)['Residuals', 'Mean Sq'], anova(fit)['Residuals', 'Df']", new[] { residualMeanSquare, residualDf }) + error,
                    Path.Combine(folder, root + "-" + op), setup: "pdf(NULL)\n");
            });
        }
        // Clotting times of plasma from eight subjects treated in four ways (Armitage and Berry 1994), the help's two way example.
        double[][] treatments = {
            new[] { 8.4, 12.8, 9.6, 9.8, 8.4, 8.6, 8.9, 7.9 }, new[] { 9.4, 15.2, 9.1, 8.8, 8.2, 9.9, 9.0, 8.1 },
            new[] { 9.8, 12.9, 11.2, 9.9, 8.5, 9.8, 9.2, 8.2 }, new[] { 12.2, 14.4, 9.8, 12.0, 8.5, 10.9, 10.4, 10.0 } };
        var cells = new RecordCells(treatments.Select((t, i) => Cells539("Treatment " + (char)('A' + i), t)).ToArray());
        var (_, oneWay) = Run("OneWay", new() { ["data"] = Select539(cells, 9, 0, 1, 2, 3) });
        ParameterBag o = oneWay.ParameterBag;
        Comparisons("OneWay", o, "one-way-anova.R", Equal539("a$'Sum Sq', a$Df", new[] { "b_sum", "w_sum", "b_df", "w_df" }.Select(k => Num539(o, k))), Num539(o, "w_mean"), Num539(o, "w_df"));
        var (_, twoWay) = Run("TwoWay", new() { ["data"] = Select539(cells, 9, 0, 1, 2, 3) });
        ParameterBag t2 = twoWay.ParameterBag;
        Comparisons("TwoWay", t2, "two-way.R", Equal539("anova(fit)$'Sum Sq', anova(fit)$Df", new[] { "sub_sum", "grp_sum", "res_sum", "sub_df", "grp_df", "res_df" }.Select(k => Num539(t2, k))), Num539(t2, "res_mean"), Num539(t2, "res_df"));
        // The replicated two-way example (three subjects, three treatments, three repeats). Its two-level frame is asked
        // repeat by repeat through dialogs that need a window, so the checks give the frame as the grid builds it (TwoLevel.cs).
        DataFrame2D replicated = new();
        foreach (double[][] block in new[] {
            new[] { new[] { 9.8, 10.1, 9.8 }, new[] { 9.9, 9.5, 10.0 }, new[] { 11.3, 10.7, 10.7 } },
            new[] { new[] { 9.2, 8.6, 9.2 }, new[] { 9.1, 9.1, 9.4 }, new[] { 10.3, 10.7, 10.2 } },
            new[] { new[] { 8.4, 7.9, 8.0 }, new[] { 8.6, 8.0, 8.0 }, new[] { 9.8, 10.1, 10.1 } } })
            replicated.Variables.Add(block.Select(values => (IVariable)new DoubleVariable(values, "")).ToList());
        var (_, replicate) = Run("ReplicateTwoWay", new() { ["data2d"] = replicated });
        ParameterBag r2 = replicate.ParameterBag;
        Comparisons("ReplicateTwoWay", r2, "replicate-two-way.R", Equal539("anova(fit)$'Sum Sq', anova(fit)$Df", new[] { "sub_sum", "grp_sum", "int_sum", "res_sum", "sub_df", "grp_df", "int_df", "res_df" }.Select(k => Num539(r2, k))), Num539(r2, "res_mean"), Num539(r2, "res_df"));

        // The regression analysis of variance after a polynomial regression (the help's example, degree 2 and then 3)
        double[] size = { 1290, 1350, 1470, 1600, 1710, 1840, 1980, 2230, 2400, 2930 }, kwh = { 1182, 1172, 1264, 1493, 1571, 1711, 1804, 1840, 1956, 1954 };
        var poly = new RecordCells(Cells539("Size", size), Cells539("kWh", kwh));
        string Anova(ParameterBag a) => Equal539("ssreg, degree, ssreg / degree, ssres, df, ssres / df, ssreg + ssres, df + degree, sqrt(ssres / df), f[1], pf(f[1], f[2], f[3], lower.tail = FALSE), 100 * summary(fit)$r.squared, 100 * summary(fit)$adj.r.squared, sum(diff(e)^2) / sum(e^2)",
            new[] { "reg_sum", "reg_df", "reg_mean", "res_sum", "res_df", "res_mean", "tot_sum", "tot_df", "mse", "f", "p", "r2", "ra2", "dw" }.Select(k => Num539(a, k)));
        foreach (string root in new[] { "PolynomialRegression", "PolynomialRegressionChangeDegree" }) Case539("routing regression ANOVA after " + root, () =>
        {
            var (_, fitted) = Run(root, new() { ["y"] = Select539(poly, 11, 1), ["x"] = Select539(poly, 11, 0), ["degree"] = root == "PolynomialRegression" ? "2" : "3" });
            var (run, anova) = Run("MultipleLinearRegressionAnova", new() { ["ci"] = .95 }, fitted.ParameterBag);
            JsonElement record = Record(run);
            Check(record.GetProperty("rootOperation").GetString() == root && RRecipes.Find(record)?.File == "polynomial-regression.R", "the regression ANOVA after " + root + " is replayed by polynomial-regression.R");
            Check(RRecipes.Find(Legacy539(run))?.File == "polynomial-regression.R", "a regression ANOVA after " + root + " saved without its root analysis is known by its y and x frames");
            Replay539(run, Anova(anova.ParameterBag), Path.Combine(folder, root + "-anova"));
        });
        // ... and after the multiple linear regression it continues, as before
        Case539("routing regression ANOVA after MultipleLinearRegression", () =>
        {
        double[] x1 = { 2.26, 1.81, 1.78, 1.54, 2.06, 1.74, 2.56, 2.29, 1.80, 2.32, 2.04, 1.88 }, x2 = { 66.2, 52.7, 72.1, 67.4, 69.9, 71.3, 88.6, 68.2, 59.5, 73.8, 68.4, 58.1 }, y = { 7, 10, 18, 4, 10, 13, 21, 12, 9, 65, 20, 31 };
        var mlr = new RecordCells(Cells539("Outcome", y), Cells539("First", x1), Cells539("Second", x2));
        var (_, model) = Run("MultipleLinearRegression", new() { ["outcome"] = Select539(mlr, 13, 0), ["predictors"] = Select539(mlr, 13, 1, 2), ["ci"] = .95, ["calculateIntercept"] = true, ["useWeights"] = false });
        var (mlrRun, mlrAnova) = Run("MultipleLinearRegressionAnova", new() { ["ci"] = .95 }, model.ParameterBag);
        Check(RRecipes.Find(Record(mlrRun))?.File == "multiple-linear-regression.R" && RRecipes.Find(Legacy539(mlrRun))?.File == "multiple-linear-regression.R",
            "the regression ANOVA after a multiple linear regression keeps its recipe, with or without its saved root analysis");
        Replay539(mlrRun, Equal539("ssreg, k, ssres, df, sqrt(ssres / df), summary(fit)$fstatistic[1], 100 * summary(fit)$r.squared, 100 * summary(fit)$adj.r.squared",
            new[] { "reg_sum", "reg_df", "res_sum", "res_df", "mse", "f", "r2", "ra2" }.Select(k => Num539(mlrAnova.ParameterBag, k))), Path.Combine(folder, "MultipleLinearRegression-anova"));
        });
    }

    // 5. Agreement: Kendall's tau of the subjects' SDs against their means is undefined when every subject has the same SD
    //    (or mean), which stopped the replay; above 1,000 untied subjects the report's P is its Edgeworth series (kendp).
    private static void CheckAgreement539(string folder)
    {
        void Case(string label, double[][] raters, double confidence = .95, int timeout = 120) => Case539("agreement " + label, () =>
        {
            int n = raters[0].Length;
            var cells = new RecordCells(raters.Select((r, i) => Cells539("Rater " + (i + 1), r)).ToArray());
            var (run, output) = Run("Agreement", new() { ["data"] = Select539(cells, n + 1, Enumerable.Range(0, raters.Length).ToArray()), ["ci"] = confidence });
            ParameterBag b = output.ParameterBag;
            bool undefined = Num539(b, "tau") == Constant.MISSING;
            string assertion = Equal539("sqrt(2) * qnorm((1 + confidence) / 2) * sw, sw", new[] { Num539(b, "rep"), Num539(b, "wssd") }, 1e-8) +
                (undefined ? "stopifnot(is.na(kt$estimate), is.na(kt$p.value))\n" : Equal539("kt$estimate", new[] { Num539(b, "tau") }) + Equal539("kt$p.value", new[] { Num539(b, "p2") }, 1e-12));
            string text = Replay539(run, assertion, Path.Combine(folder, label), setup: "pdf(NULL)\n", timeout: timeout);
            if (undefined) Check(Regex.IsMatch(text, @"Kendall's tau b = \*\s+two sided P = \*"), label + ": tau and its P are shown as undefined, as the report shows them");
            Match repeatability = Regex.Match(text, @"Repeatability \(for alpha = [0-9.]+\) = " + Figure539);
            Check(repeatability.Success && Math.Abs(double.Parse(repeatability.Groups[1].Value, inv) - Num539(b, "rep")) <= 5.1e-7, label + ": the repeatability line is printed");
        });
        double[] first = { 12, 15, 9, 20, 17, 11, 14, 18, 10, 16 };
        Case("perfect-agreement", new[] { first, first.ToArray() });
        Case("constant-offset", new[] { first, first.Select(v => v + 2).ToArray() });
        Case("three-offsets", new[] { first, first.Select(v => v + 1).ToArray(), first.Select(v => v + 2).ToArray() }, .9);
        foreach ((int n, double share) in new[] { (1001, .4925), (1001, .495), (1100, .497), (1500, .499) })
        {
            int[] d = Permutation539(n, (long)((long)n * (n - 1) / 2 * share));
            double[] mean = Enumerable.Range(0, n).Select(i => 1000d + 10 * i).ToArray();
            Case("untied-" + n + "-" + share, new[] { mean.Select((m, i) => m + d[i]).ToArray(), mean.Select((m, i) => m - d[i]).ToArray() }, timeout: 240);
        }
        double[] tiedMean = Enumerable.Range(0, 1200).Select(i => 500d + 3 * i).ToArray();
        Case("tied-1200", new[] { tiedMean.Select((m, i) => m + 1 + i % 37).ToArray(), tiedMean.Select((m, i) => m - 1 - i % 37).ToArray() }, timeout: 240);
    }

    // 6. A confidence level typed as 100% (recorded 1) or 0% is taken as 95% by every one of these reports (XLevel); their
    //    recipes replayed the typed level, halted (Mann-Whitney), or printed 0 to infinity. The Cox hazard ratios had no
    //    fallback in the report either and printed a point interval labelled 100%.
    private static void CheckConfidenceFallback539(string folder)
    {
        double[] x = { 8, 12, 15, 11, 19, 14, 9, 16, 21, 18, 13, 17 }, y = { 9, 10, 13, 12, 17, 16, 14, 18, 22, 19, 15, 20 };
        RecordCells two = new(Cells539("First", x), Cells539("Second", y));
        double[] longer = Enumerable.Range(1, 30).Select(i => 50 + 9 * Math.Sin(i * 1.3) + i * .37).ToArray();
        double[] partner = longer.Select((v, i) => v + 4 * Math.Cos(i * .9) - .6).ToArray();
        RecordCells pairs = new(Cells539("Before", longer), Cells539("After", partner));
        double[] weights = Enumerable.Range(1, 30).Select(i => 1d + i % 4).ToArray();
        RecordCells weighted = new(Cells539("Value", longer), Cells539("Weight", weights));
        foreach (double confidence in new[] { 0d, 1d })
        {
            string level = confidence.ToString("R", inv);
            string Folder(string op) => Path.Combine(folder, op + "-" + level);
            const string Used = "stopifnot(confidence == 0.95)\n";

            Case539("fallback Wilcoxon " + level, () =>
            {
                var (run, output) = Run("Wilcoxon", new() { ["data"] = Select539(pairs, 31, 0, 1), ["gamma"] = confidence });
                ParameterBag b = output.ParameterBag;
                int k = int.Parse(Regex.Match(Convert.ToString(DeferredBags538(b).First(bag => bag.ContainsKey("k"))["k"].AsObject, inv), @"K = ([0-9]+)").Groups[1].Value, inv);
                Replay539(run, Used + "stopifnot(K == " + k + ")\n" + Equal539("median(a), a[K], rev(a)[K]", new[] { Found539(b, "med_diff"), Found539(b, "from"), Found539(b, "to") }), Folder("Wilcoxon"));
            });
            Case539("fallback Spearman " + level, () =>
            {
                var (run, output) = Run("Spearman", new() { ["data"] = Select539(two, 13, 0, 1), ["gamma"] = confidence });
                Replay539(run, Used + Equal539("ci", new[] { Found539(output.ParameterBag, "from"), Found539(output.ParameterBag, "to") }), Folder("Spearman"));
            });
            Case539("fallback ZUnpaired " + level, () =>
            {
                var (run, output) = Run("ZUnpaired", new() { ["data"] = Select539(two, 13, 0, 1), ["gamma"] = confidence });
                Replay539(run, Used + Equal539("d - qnorm((1 + confidence) / 2) * se, d + qnorm((1 + confidence) / 2) * se", new[] { Found539(output.ParameterBag, "from"), Found539(output.ParameterBag, "to") }), Folder("ZUnpaired"));
            });
            Case539("fallback ReferenceRange " + level, () =>
            {
                var (run, output) = Run("ReferenceRange", new() { ["data"] = Select539(pairs, 31, 0), ["gamma"] = confidence, ["reference-interval"] = .9, ["do_conservative"] = true });
                ParameterBag b = output.ParameterBag;
                string text = Replay539(run, Used, Folder("ReferenceRange"));
                double[] lower = Printed539(text, @"^95% confidence interval for lower range limit = " + Figure539 + " to " + Figure539);
                double[] upper = Printed539(text, @"^95% confidence interval for upper range limit = " + Figure539 + " to " + Figure539);
                Check(lower.Length == 2 && upper.Length == 2 && new[] { (lower[0], "lx_l"), (lower[1], "ux_l"), (upper[0], "lx_u"), (upper[1], "ux_u") }.All(p => Math.Abs(p.Item1 - Found539(b, p.Item2)) <= 5.1e-7),
                    "ReferenceRange at a recorded " + level + ": the replay gives the report's 95% limits of the range limits");
            });
            Case539("fallback Quantile " + level, () =>
            {
                var (run, output) = Run("Quantile", new() { ["data"] = Select539(pairs, 31, 0), ["gamma"] = confidence, ["conservative-ci"] = false, ["quantile"] = .5 });
                string text = Replay539(run, "stopifnot(conf == 0.95)\n", Folder("Quantile"));
                double[] quantile = Printed539(text, @"^Approximate 95% CI \([^)]*\) = " + Figure539 + " to " + Figure539);
                Check(quantile.Length == 2 && Math.Abs(quantile[0] - Found539(output.ParameterBag, "from")) <= 5.1e-6 * Math.Abs(quantile[0]) && Math.Abs(quantile[1] - Found539(output.ParameterBag, "to")) <= 5.1e-6 * Math.Abs(quantile[1]),
                    "Quantile at a recorded " + level + ": the replay gives the report's 95% interval");
            });
            Case539("fallback QuickSummary " + level, () =>
            {
                // The quick summary is shown in a dialog rather than the report, so its record is written here as the report
                // step writes one (RunRecord.Write), from the frame the grid acquired and the recorded level.
                string summary = null;
                ParameterBag acquired = null;
                var select = Select539(pairs, 31, 0);
                Func<ITemplateProcessor, Parameter, ParameterBag, ITemplateHost, ParameterBag> keep = (processor, parameter, context, host) => acquired = select(processor, parameter, context, host);
                Run("QuickSummary", new() { ["data"] = keep, ["gamma"] = confidence }, amend: options => { if (options is SummaryStatisticsOptions s) summary = s.Text; });
                OperationRun run = new(Load("QuickSummary"));
                run.Note(new FrameParameter { Name = "data" }, "Select data for quick summary", value: acquired["data"]);
                ParameterBag recorded = new(); recorded.AddInput("data", acquired["data"].AsDataFrame); recorded.AddInput("gamma", confidence);
                run.Record = RunRecord.Write(run, recorded);
                string text = Replay539(run, Used, Folder("QuickSummary"));
                double[] nativeUpper = Printed539(summary ?? "", @"^95% Upper CL of mean\s+" + Figure539), nativeLower = Printed539(summary ?? "", @"^95% Lower CL of mean\s+" + Figure539);
                double[] replayUpper = Printed539(text, @"^95% Upper CL of mean " + Figure539), replayLower = Printed539(text, @"^95% Lower CL of mean " + Figure539);
                Check(nativeUpper.Length == 1 && nativeLower.Length == 1 && replayUpper.Length == 1 && replayLower.Length == 1 && Math.Abs(nativeUpper[0] - replayUpper[0]) <= 5.1e-7 && Math.Abs(nativeLower[0] - replayLower[0]) <= 5.1e-7,
                    "QuickSummary at a recorded " + level + ": the replay gives the report's 95% limits of the mean");
            });
            Case539("fallback Gini " + level, () =>
            {
                var (run, output) = Run("Gini", new() { ["data"] = Select539(pairs, 31, 0), ["weights"] = null, ["gamma"] = confidence, ["boots"] = 200, ["seed"] = 7 });
                Check(Found539(output.ParameterBag, "pc") == 95, "the native Gini report takes " + level + " as 95%");
                string text = Replay539(run, Used, Folder("Gini"));
                Check(!text.Contains("100%") && !Regex.IsMatch(text, @"(^|\s)0% "), "Gini at a recorded " + level + ": the replay labels its intervals 95%");
            });
            Case539("fallback Crossover " + level, () =>
            {
                double[] d1 = { 8, 14, 8, 9, 11, 3, 6, 0, 13, 10, 7, 13, 8, 7 }, p1 = { 5, 10, 0, 7, 6, 5, 0, 0, 12, 2, 5, 13, 10, 7 }, d2 = { 12, 6, 13, 8, 8, 4, 8, 2, 8, 9, 7, 7, 10 }, p2 = { 11, 8, 9, 8, 9, 8, 14, 4, 13, 7, 10, 6, 7 };
                RecordCells crossover = new(Cells539("Drug 1", d1), Cells539("Placebo 1", p1), Cells539("Drug 2", d2), Cells539("Placebo 2", p2));
                var (run, output) = Run("Crossover", new() { ["gamma"] = confidence, ["group1drug"] = Select539(crossover, 15, 0), ["group1placebo"] = Select539(crossover, 15, 1), ["group1baseline"] = null, ["group2drug"] = Select539(crossover, 15, 2), ["group2placebo"] = Select539(crossover, 15, 3), ["group2baseline"] = null });
                ParameterBag b = output.ParameterBag;
                string text = Replay539(run, Used, Folder("Crossover"));
                double[] effect = Printed539(text, @"^effect magnitude = " + Figure539 + " 95% CI = " + Figure539 + " to " + Figure539);
                Check(effect.Length == 3 && Math.Abs(effect[1] - Found539(b, "treatment_from")) <= 5.1e-7 && Math.Abs(effect[2] - Found539(b, "treatment_to")) <= 5.1e-7,
                    "Crossover at a recorded " + level + ": the replay gives the report's 95% interval of the treatment effect");
            });
            Case539("fallback NonparametricLinearRegression " + level, () =>
            {
                var (run, output) = Run("NonparametricLinearRegression", new() { ["outcome"] = Select539(pairs, 31, 1), ["predictor"] = Select539(pairs, 31, 0), ["gamma"] = confidence });
                Replay539(run, Used + Equal539("np_result$lower, np_result$upper", new[] { Found539(output.ParameterBag, "from"), Found539(output.ParameterBag, "to") }), Folder("NonparametricLinearRegression"));
            });
            Case539("fallback TSingleSummary " + level, () =>
            {
                var (run, output) = Run("TSingleSummary", new() { ["nx"] = 20, ["mu"] = 130.05, ["sd1"] = 9.79, ["mu0"] = 120d, ["gamma"] = confidence });
                Replay539(run, Used + Equal539("r$conf.int - mu0", new[] { Found539(output.ParameterBag, "from"), Found539(output.ParameterBag, "to") }), Folder("TSingleSummary"));
            });
            Case539("fallback TUnpairedSummary " + level, () =>
            {
                var (run, output) = Run("TUnpairedSummary", new() { ["nx1"] = 12, ["um1"] = 120d, ["sd1"] = 21.6, ["nx2"] = 7, ["um2"] = 101d, ["sd2"] = 18.3, ["gamma"] = confidence });
                Replay539(run, Used + Equal539("equal$conf.int", new[] { Found539(output.ParameterBag, "from"), Found539(output.ParameterBag, "to") }), Folder("TUnpairedSummary"));
            });
            Case539("fallback WeightedUnivariateSummary " + level, () =>
            {
                var (run, output) = Run("WeightedUnivariateSummary", new() { ["data"] = Select539(weighted, 31, 0), ["weights"] = Select539(weighted, 31, 1), ["gamma"] = confidence, ["report-centile-type"] = "1", ["report-udca"] = .05, ["report-udcb"] = .95, ["output-to-frame"] = false });
                var fields = DeferredBags538(output.ParameterBag).Where(bag => bag.ContainsKey("title") && bag.ContainsKey("*results")).ToList();
                double NativeRow(string title) => fields.Where(bag => Convert.ToString(bag["title"].AsObject, inv) == title).Select(bag => Num539(Rows539(bag, "*results")[0], "result")).DefaultIfEmpty(double.NaN).First();
                string text = Replay539(run, Used, Folder("WeightedUnivariateSummary"));
                double[] limits = Printed539(text, @"^Upper 95% CL of mean " + Figure539 + @"\s+Lower 95% CL of mean " + Figure539);
                Check(limits.Length == 2 && Math.Abs(limits[0] - NativeRow("Upper 95% CL of mean")) <= 5.1e-7 && Math.Abs(limits[1] - NativeRow("Lower 95% CL of mean")) <= 5.1e-7,
                    "WeightedUnivariateSummary at a recorded " + level + ": the replay gives the report's 95% limits of the mean");
            });
            Case539("fallback TimeSeriesSummary " + level, () =>
            {
                double[] times = { 0, 1, 2, 4, 0, 1, 2, 4, 0, 1, 2, 4, 0, 1, 2, 4 }, levels = { 0, 3.1, 4.2, 2.0, 0, 2.6, 3.9, 2.4, 0, 3.8, 5.1, 2.2, 0, 2.2, 3.3, 1.7 };
                RecordCells series = new(Cells539("Time", times), Cells539("Level", levels), new object[] { "Patient" }.Concat(Enumerable.Range(0, 16).Select(i => (object)("P" + (i / 4 + 1)))).ToArray());
                var (run, output) = Run("TimeSeriesSummary", new() { ["times"] = Select539(series, 17, 0), ["observations"] = Select539(series, 17, 1), ["subjectIds"] = Select539(series, 17, 2), ["groups"] = null, ["addZeroObservationAtZeroTime"] = false, ["ci"] = confidence, ["doExactP"] = false, ["iterations"] = 1000, ["seed"] = 1 });
                ParameterBag b = output.ParameterBag;
                Check(Num539(b, "ciOutput") == 95, "the native time series summary takes " + level + " as 95%");
                ParameterBag group = Rows539(b, "*group")[0];
                string text = Replay539(run, Used, Folder("TimeSeriesSummary"));
                double[] auc = Printed539(text, @"^95% t-interval for mean AUC: " + Figure539 + " to " + Figure539);
                Check(auc.Length == 2 && Math.Abs(auc[0] - Num539(group, "aucTLcl")) <= .0051 && Math.Abs(auc[1] - Num539(group, "aucTUcl")) <= .0051,
                    "TimeSeriesSummary at a recorded " + level + ": the replay gives the report's 95% t-interval of the mean AUC");
            });
        }

        // The Cox hazard ratios: the report now takes 0% or 100% as 95%, as its other reports do; R's coxph limits at 95%
        // are the independent reference for the native limits at a recorded 100%.
        double[] coxTimes = { 6, 6, 6, 7, 10, 13, 16, 22, 23, 6, 9, 10, 11, 17, 19, 20, 25, 32, 32, 34, 35, 1, 1, 2, 2, 3, 4, 4, 5, 5, 8, 8, 8, 8, 11, 11, 12, 12, 15, 17, 22, 23 };
        double[] coxEvents = coxTimes.Select((_, i) => i % 5 == 4 ? 0d : 1d).ToArray();
        double[] coxGroup = coxTimes.Select((_, i) => i < 21 ? 1d : 0d).ToArray();
        RecordCells cox = new(Cells539("Time", coxTimes), Cells539("Event", coxEvents), Cells539("Treated", coxGroup));
        var (_, coxFit) = Run("CoxRegression", new() { ["times"] = Select539(cox, coxTimes.Length + 1, 0), ["events"] = Select539(cox, coxTimes.Length + 1, 1), ["predictors"] = Select539(cox, coxTimes.Length + 1, 2), ["strata"] = null, ["accuracy"] = 1e-9, ["centre-continuous-covariates"] = true, ["useTimesAdjustment"] = false });
        var (_, at95) = Run("CoxHazardRatios", new() { ["gamma"] = .95 }, coxFit.ParameterBag);
        ParameterBag reference = Rows539(at95.ParameterBag, "*hazard")[0];
        foreach (double confidence in new[] { 0d, 1d, .9 }) Case539("fallback CoxHazardRatios " + confidence.ToString("R", inv), () =>
        {
            var (run, output) = Run("CoxHazardRatios", new() { ["gamma"] = confidence }, coxFit.ParameterBag);
            double used = confidence > 0 && confidence < 1 ? confidence : .95;
            ParameterBag hazard = Rows539(output.ParameterBag, "*hazard")[0];
            Check(Num539(output.ParameterBag, "pc2") == used * 100 && (used != .95 || new[] { "ec", "ell", "eul" }.All(key => Num539(hazard, key) == Num539(reference, key))),
                "the native Cox hazard ratios at a recorded " + confidence.ToString("R", inv) + " use " + used * 100 + "%");
            string text = Replay539(run, "stopifnot(confidence == " + used.ToString("R", inv) + ")\n" +
                Equal539("exp(coef(fit)), exp(coef(fit) - qnorm((1 + confidence) / 2) * sqrt(diag(vcov(fit)))), exp(coef(fit) + qnorm((1 + confidence) / 2) * sqrt(diag(vcov(fit))))",
                    new[] { "ec", "ell", "eul" }.Select(key => Num539(hazard, key)), 1e-6), Path.Combine(folder, "CoxHazardRatios-" + confidence.ToString("R", inv)), setup: "pdf(NULL)\n");
            Check(text.Contains(" " + (used * 100).ToString("G", inv) + "% CI ="), "the replayed Cox hazard ratios are labelled " + used * 100 + "%");
        });
    }

    // 7. Paired t: R's t.test refuses differences whose standard error is below 10 eps times their mean ("data are essentially
    //    constant"), such as decimal data with a constant shift, where the report still gives t, P and the interval.
    private static void CheckPairedNearConstant539(string folder)
    {
        foreach (double shift in new[] { .1, .3, 1.1 })
        foreach (bool paired in new[] { true, false })
        foreach (bool agreement in paired ? new[] { false, true } : new[] { false }) Case539("paired shift " + shift + " " + paired + " " + agreement, () =>
        {
            // typed decimals, as in a worksheet: 5.1, 6.2 ... and 5.1 - shift, 6.2 - shift ... each the double nearest its decimal
            double[] first = Enumerable.Range(0, 6).Select(i => Math.Round(5.1 + 1.1 * i, 10)).ToArray(), second = first.Select(v => Math.Round(v - shift, 10)).ToArray();
            RecordCells cells = paired ? new(Cells539("First", first), Cells539("Second", second)) : new(Cells539("Differences", first.Select((v, i) => v - second[i])));
            var (run, output) = Run("TPaired", new() { ["data"] = Select539(cells, 7, paired ? new[] { 0, 1 } : new[] { 0 }), ["gamma"] = .95, ["doAgreement"] = agreement });
            ParameterBag b = output.ParameterBag;
            Check(Num539(b, "sd") > 0, "the shift of " + shift + " leaves differences that are not exactly constant");
            string assertion = Equal539("n, mean(d), sd(d), r$parameter", new[] { "n", "mean", "sd", "df" }.Select(k => Num539(b, k)), 1e-12) +
                Equal539("r$statistic", new[] { Num539(b, "t") }, 1e-6) + Equal539("r$p.value", new[] { Num539(b, "tail_2") }, 1e-5) +
                Equal539("r$conf.int", new[] { Num539(b, "from"), Num539(b, "to") }, 1e-12);
            Replay539(run, assertion, Path.Combine(folder, "shift-" + shift + "-" + paired + "-" + agreement), setup: "pdf(NULL)\n");
        });
    }

    // 8. PCA: worksheet titles that repeat are kept apart in the record by held keys ("dup", "dup_2"); the replay printed the
    //    key where the report prints the title in the line of reversed items.
    private static void CheckPcaTitles539(string folder)
    {
        double[][] items = {
            new[] { 1.2, .4, -.3, 2.1, .8, -1, .2, 1.5, -.6, .9, .1, -.2 }, new[] { -1, -.7, .1, -1.8, -.6, 1.3, -.5, -1.2, .4, -1.1, .2, 0 },
            new[] { 1.5, .2, -.6, 2.4, .6, -.8, 0, 1.9, -.9, .6, .3, -.5 }, new[] { .9, .5, .1, 1.7, 1, -1.1, .4, 1.4, -.3, 1.2, 0, .1 },
            new[] { -.8, -.6, .2, -1.6, -1.1, .9, -.2, -1.3, .5, -.7, -.1, .3 } };
        foreach (string[] titles in new[] { new[] { "A", "dup", "C", "D", "dup" }, new[] { "Same", "Same", "Same", "Other", "Same" } })
        foreach (string operation in new[] { "PCRCorrelation", "PCRCovariance", "PCRCovarianceNoFollowOns" }) Case539("pca " + operation + " " + string.Join("-", titles), () =>
        {
            var cells = new RecordCells(items.Select((v, i) => Cells539(titles[i], v)).ToArray());
            var answers = new Dictionary<string, object> { ["data"] = Select539(cells, 13, 0, 1, 2, 3, 4), ["correctForReversal"] = true };
            if (operation != "PCRCorrelation") answers["ci"] = .95;
            var (run, output) = Run(operation, answers);
            string reversed = Convert.ToString(DeferredBags538(output.ParameterBag).First(b => b.ContainsKey("warn"))["warn"].AsObject, inv);
            Check(reversed.StartsWith("Sign was reversed for:", StringComparison.Ordinal), operation + ": the native report reverses items");
            string label = operation + "-" + string.Join("-", titles.Distinct());
            string text = Replay539(run, "stopifnot(identical(names(q), " + RScriptWriter.Vector(titles) + "))\n", Path.Combine(folder, label));
            Check(text.Contains(reversed), label + ": the replay names the reversed items by their worksheet titles, as the report does");
            if (operation != "PCRCovariance") return;
            var (follow, _) = Run("Cronbach", new() { ["ci"] = .9 }, output.ParameterBag);
            text = Replay539(follow, "stopifnot(identical(names(q), " + RScriptWriter.Vector(titles) + "))\n", Path.Combine(folder, label + "-cronbach"));
            Check(text.Contains(reversed), label + ": the reliability follow-on keeps the worksheet titles");
        });
    }

}
