using System.Text.RegularExpressions;
using StatsDirect.Data;
using StatsDirect.R;
using StatsDirect.Templates;

internal static partial class Program
{
    // The reviewer's recheck of 11 Oct 2026: intervals the recipes had fixed at 95% where the run's confidence level should rule, and a
    // predictor whose title has a space.  Each run goes through the engine at 90% or 99%, and the limit R prints is compared with the
    // engine's.
    private static void CheckOptions3()
    {
        RRecipes.Folder = Metadata("Recipes");
        string folder = Path.Combine(Path.GetTempPath(), "statsdirect-rscript-checks", "options3");
        Directory.CreateDirectory(folder);
        int column = 1;
        DataFrame Sheet(params (string title, double[] values)[] columns) { DataFrame f = SheetFrame(Workbook, "Options3", column, 1, columns); column += columns.Length; return f; }
        DataFrame Classifier(string title, double[] codes)
        {
            double[] levels = codes.Distinct().OrderBy(v => v).ToArray();
            ClassifierVariable classifier = new() { Title = title };
            classifier.EnsureLength(codes.Length);
            for (int i = 0; i < codes.Length; i++) classifier.SetData(i, Array.IndexOf(levels, codes[i]));
            foreach (double lv in levels) classifier.Groups.Add(new StatsDirect.Data.Group(lv.ToString(inv), Array.IndexOf(levels, lv)) { NBin = codes.Count(v => v == lv) });
            DataFrame frame = new();
            frame.Variables.Add(classifier);
            return frame;
        }
        string Script(OperationRun run) { RScript s = RScriptWriter.Write(Record(run), null, null); if (!s.HasRecipe) throw new Exception("FAIL the run's script has no recipe"); return s.Text; }
        string Rr(string s, string f) => RunR(s, f).Replace("\r", "");
        double Num(object o) => o is double d ? d : o is int i ? i : double.Parse(Regex.Match(o.ToString(), "^-?[0-9.]+(?:[eE][-+]?[0-9]+)?").Value, inv);
        IEnumerable<ParameterBag> Bags(ParameterBag bag)
        {
            yield return bag;
            foreach (KeyValuePair<string, FilledParameter> pair in bag)
                if (pair.Value?.AsObject is IList<ParameterBag> list)
                    foreach (ParameterBag inner in list)
                        foreach (ParameterBag b in Bags(inner)) yield return b;
        }
        List<double> Found(ParameterBag bag, string key) => Bags(bag).Where(b => b.ContainsKey(key) && b[key]?.AsObject != null).Select(b => Num(b[key].AsObject)).ToList();
        (double, double) Pair(string text, string pattern) { Match m = Regex.Match(text, pattern, RegexOptions.Multiline); if (!m.Success) throw new Exception("FAIL R did not print " + pattern); return (double.Parse(m.Groups[1].Value, inv), double.Parse(m.Groups[2].Value, inv)); }
        bool Near(double a, double b, double relative = 1e-4) => Math.Abs(a - b) <= relative * Math.Max(Math.Abs(a), Math.Abs(b)) + 6e-7;
        void Interval(string what, string script, string setting, (double from, double to) r, double engineFrom, double engineTo, double relative = 1e-4)
        {
            Console.WriteLine($"      {what}: engine {engineFrom:G7} to {engineTo:G7}, R {r.from:G7} to {r.to:G7}");
            Check(script.Contains(setting) && Near(r.from, engineFrom, relative) && Near(r.to, engineTo, relative), $"R's {what} are the engine's at the run's level");
        }

        // Poisson regression at 99%
        double[] counts = { 2, 3, 6, 7, 8, 9, 10, 12, 15 }, xp = { 1, 2, 3, 4, 5, 6, 7, 8, 9 }, exposure = { 10, 12, 15, 14, 16, 15, 18, 20, 22 };
        var (run, output) = Run("PoissonRegression", new() { ["gamma"] = 0.99, ["grouping"] = "individual", ["accuracy"] = "0.0000001", ["intercept"] = true, ["weights"] = false, ["has-exposure"] = "true", ["response"] = Sheet(("counts", counts)), ["exposure"] = Sheet(("exposure", exposure)), ["predictors"] = Sheet(("xp", xp)) });
        string script = Script(run);
        string r = Rr(script, Path.Combine(folder, "poisson"));
        ParameterBag xpRow = Bags(output.ParameterBag).First(b => b.ContainsKey("lab") && b["lab"].AsObject.ToString() == "xp");
        Interval("Poisson rate ratio limits at 99%", script, "confidence <- 0.99", Pair(r, "^xp -?[0-9.]+ -?[0-9.]+ (-?[0-9.]+) to (-?[0-9.]+)"), Num(xpRow["lci"].AsObject), Num(xpRow["uci"].AsObject));

        // conditional logistic regression at 99%, with a predictor whose title has a space
        double[] pair = { 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7, 8, 8, 9, 9, 10, 10 }, caseControl = { 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0 }, smoking = { 1, 0, 1, 0, 1, 1, 0, 1, 1, 0, 0, 0, 1, 0, 1, 1, 0, 0, 1, 0 };
        (run, output) = Run("ConditionalLogisticRegression", new() { ["gamma"] = 0.99, ["accuracy"] = "0.0000001", ["show-counts"] = false, ["stratum"] = Classifier("pair", pair), ["case-control"] = Sheet(("case", caseControl)), ["predictors"] = Sheet(("Smoking status", smoking)) });
        script = Script(run);
        r = Rr(script, Path.Combine(folder, "conditional"));
        Interval("conditional logistic odds ratio limits at 99%, for a predictor titled with a space", script, "confidence <- 0.99", Pair(r, "^`?Smoking status`? -?[0-9.]+ (-?[0-9.]+) to (-?[0-9.]+)"), Found(output.ParameterBag, "from")[0], Found(output.ParameterBag, "to")[0]);

        // simple linear regression at 90%
        double[] weight = { 2.1, 2.5, 2.8, 3.0, 3.2, 3.5, 3.8, 4.1 }, increase = { 15, 14, 12, 11, 10, 8, 7, 6 };
        (run, output) = Run("SimpleLinearRegression", new() { ["gamma"] = 0.9, ["y"] = Sheet(("increase", increase)), ["x"] = Sheet(("weight", weight)) });
        script = Script(run);
        r = Rr(script, Path.Combine(folder, "simple"));
        Interval("slope limits at 90%", script, "confidence <- 0.9", Pair(r, "^90% CI for population value of slope = (-?[0-9.]+) to (-?[0-9.]+)"), Found(output.ParameterBag, "se_from")[0], Found(output.ParameterBag, "se_to")[0]);   // the slope's limits (ci_from and ci_to are r's)

        // the Mann-Whitney test at 90%
        double[] farm = { 14.8, 7.3, 5.6, 6.3, 9.0, 4.2, 10.6, 12.5, 12.9, 16.1 }, town = { 12.7, 14.2, 12.6, 2.1, 17.7, 11.8, 16.9, 7.9, 16.0, 10.5 };
        (run, output) = Run("MannWhitney", new() { ["gamma"] = 0.9, ["data"] = Sheet(("farm", farm), ("town", town)) });
        script = Script(run);
        r = Rr(script, Path.Combine(folder, "mann-whitney"));
        Interval("Mann-Whitney difference limits at 90%", script, "confidence <- 0.9", Pair(r, "[0-9.]+ percent confidence interval:\\s*\\n\\s*(-?[0-9.]+)\\s+(-?[0-9.]+)"), Found(output.ParameterBag, "from")[0], Found(output.ParameterBag, "to")[0]);   // R names the level the exact interval reaches (91.1% here)

        // the diagnostic test at 90%
        (run, output) = Run("MiscDiagnostic", new() { ["cco"] = 0.9, ["scrap"] = new double[] { 40, 10, 15, 60 } });
        script = Script(run);
        r = Rr(script, Path.Combine(folder, "diagnostic"));
        Interval("exact odds ratio limits of the diagnostic test at 90%", script, "confidence <- 0.9", Pair(r, "^Conditional maximum likelihood estimate = -?[0-9.]+ \\((-?[0-9.]+) to (-?[0-9.]+)\\)"), Found(output.ParameterBag, "cmle_from")[0], Found(output.ParameterBag, "cmle_to")[0]);

        // agreement of three raters at 90%
        double[] r1 = { 10, 12, 14, 9, 11, 13, 15, 8 }, r2 = { 11, 12, 13, 10, 11, 14, 14, 9 }, r3 = { 9, 13, 14, 9, 12, 12, 16, 8 };
        (run, output) = Run("Agreement", new() { ["ci"] = 0.9, ["data"] = Sheet(("first", r1), ("second", r2), ("third", r3)) });
        script = Script(run);
        r = Rr(script, Path.Combine(folder, "agreement"));
        Interval("intraclass correlation limits at 90%", script, "confidence <- 0.9", Pair(r, "\\(90% CI = (-?[0-9.]+) to (-?[0-9.]+) \\)"), Found(output.ParameterBag, "icc_lcl")[0], Found(output.ParameterBag, "icc_ucl")[0]);

        // the proportion meta-analysis at 90%: each study's exact limits
        (run, output) = Run("ProportionMeta", new() { ["gamma"] = 0.9, ["method"] = "arcsine", ["strata"] = null, ["sn"] = Sheet(("n", new double[] { 50, 60, 40 })), ["sr"] = Sheet(("r", new double[] { 20, 30, 10 })) });
        script = Script(run);
        r = Rr(script, Path.Combine(folder, "proportion-meta"));
        Interval("first study's proportion limits at 90%", script, "confidence <- 0.9", Pair(r, "^ *1 +-?[0-9.]+ +(-?[0-9.]+) +(-?[0-9.]+) "), Found(output.ParameterBag, "from_y")[0], Found(output.ParameterBag, "to_y")[0]);

        // the incidence rate ratio meta-analysis at 90%: the exact limits
        MetaExact = true;   // the exact method is a preference of the program
        (run, output) = Run("MetaIncidenceRateRatio", new() { ["gamma"] = 0.9, ["strata"] = null, ["a"] = Sheet(("a", new double[] { 4, 3, 5 })), ["pt1"] = Sheet(("pt1", new double[] { 1000, 800, 1200 })), ["b"] = Sheet(("b", new double[] { 2, 3, 2 })), ["pt2"] = Sheet(("pt2", new double[] { 1100, 900, 1300 })) });   // small counts, within the reach of the exact method
        script = Script(run);
        r = Rr(script, Path.Combine(folder, "rate-ratio-meta"));   // (the exact preference stays on: the inconsistency's limits below are the non-central ones only with it)
        Interval("exact limits of the pooled rate ratio at 90%", script, "confidence <- 0.9", Pair(r, "^Exact Fisher 90% confidence interval = (-?[0-9.]+) to (-?[0-9.]+)"), Found(output.ParameterBag, "llf")[0], Found(output.ParameterBag, "ulf")[0]);

        // the effect size meta-analysis at 90%: the first study's exact limits and the inconsistency's limits
        double[] en = { 20, 25, 30 }, em = { 5.1, 4.8, 5.5 }, es = { 1.2, 1.5, 1.1 }, cn = { 22, 24, 28 }, cm = { 4.2, 4.1, 4.9 }, cs = { 1.3, 1.4, 1.0 };
        (run, output) = Run("Effect", new() { ["gamma"] = 0.9, ["type"] = "d", ["strata"] = null, ["en"] = Sheet(("en", en)), ["em"] = Sheet(("em", em)), ["es"] = Sheet(("es", es)), ["cn"] = Sheet(("cn", cn)), ["cm"] = Sheet(("cm", cm)), ["cs"] = Sheet(("cs", cs)) });
        script = Script(run);
        r = Rr(script, Path.Combine(folder, "effect"));
        // the report's two tables for each study: the exact limits of g (with the correction J) and the approximate limits of d
        ParameterBag exact1 = Bags(output.ParameterBag).First(b => b.ContainsKey("gj") && b.ContainsKey("lci")), approx1 = Bags(output.ParameterBag).First(b => b.ContainsKey("d") && b.ContainsKey("lci") && !b.ContainsKey("gj"));
        Interval("first study's exact effect size limits at 90%", script, "confidence <- 0.9", Pair(r, "^ *Trial 1 +-?[0-9.]+ +-?[0-9.]+ +(-?[0-9.]+) +(-?[0-9.]+)"), Num(exact1["lci"].AsObject), Num(exact1["uci"].AsObject));
        Interval("first study's approximate effect size limits at 90%", script, "confidence <- 0.9", Pair(r, "^ *Trial 1 +\\d+ +\\d+ +-?[0-9.]+ +(-?[0-9.]+) +(-?[0-9.]+)"), Num(approx1["lci"].AsObject), Num(approx1["uci"].AsObject));
        (double iLow, double iHigh) = Pair(r, "^I-squared \\(inconsistency\\) = -?[0-9.]+% \\(90% ?CI = (-?[0-9.]+)% to (-?[0-9.]+)%\\)");
        double engineILow = Found(output.ParameterBag, "llisq")[0], engineIHigh = Found(output.ParameterBag, "ulisq")[0];
        Console.WriteLine($"      inconsistency limits at 90%: engine {engineILow:G7}% to {engineIHigh:G7}%, R {iLow}% to {iHigh}%");
        Check(Math.Abs(Math.Round(engineILow, 1) - iLow) < 1e-9 && Math.Abs(Math.Round(engineIHigh, 1) - iHigh) < 1e-9, "R's inconsistency limits of the effect size meta-analysis are the engine's at the run's level, to the decimal place R prints");

        // the Mantel-Haenszel analysis at 90%: the inconsistency's limits
        (run, output) = Run("Mantel", new() { ["gamma"] = 0.9, ["strata"] = null, ["sn"] = Sheet(("sn", new double[] { 100, 120, 90 })), ["sr"] = Sheet(("sr", new double[] { 30, 45, 20 })), ["xn"] = Sheet(("xn", new double[] { 110, 100, 95 })), ["xr"] = Sheet(("xr", new double[] { 20, 25, 18 })) });
        script = Script(run);
        r = Rr(script, Path.Combine(folder, "mantel"));
        (iLow, iHigh) = Pair(r, "^I2 \\(inconsistency\\) = -?[0-9.]+% \\(90% ?CI = (-?[0-9.]+)% to (-?[0-9.]+)%\\)");
        engineILow = Found(output.ParameterBag, "llisq")[0]; engineIHigh = Found(output.ParameterBag, "ulisq")[0];
        Console.WriteLine($"      Mantel-Haenszel inconsistency limits at 90%: engine {engineILow:G7}% to {engineIHigh:G7}%, R {iLow}% to {iHigh}%");
        Check(script.Contains("confidence <- 0.9") && Math.Abs(Math.Round(engineILow, 1) - iLow) < 1e-9 && Math.Abs(Math.Round(engineIHigh, 1) - iHigh) < 1e-9, "R's inconsistency limits of the Mantel-Haenszel analysis are the engine's at the run's level, to the decimal place R prints");
        MetaExact = false;
    }
}
