using System.Text.Json;
using System.Text.RegularExpressions;
using StatsDirect.Data;
using StatsDirect.R;
using StatsDirect.Templates;

internal static partial class Program
{
    // Runs with the options the help's examples do not set, and with sizes the examples do not have (Iain's reviewer, 11 Oct 2026, on
    // 5.1.0: the scripts fitted the help's example rather than the run).  Each run goes through the engine with its options, the writer
    // writes the script, R runs it, and the figures R prints are compared with the engine's own.  The data are small and made up: R is
    // the independent route to the same model.
    private static void CheckOptions()
    {
        RRecipes.Folder = Metadata("Recipes");
        string folder = Path.Combine(Path.GetTempPath(), "statsdirect-rscript-checks", "options");
        Directory.CreateDirectory(folder);
        int column = 1;
        DataFrame Sheet(params (string title, double[] values)[] columns) { DataFrame f = SheetFrame(Workbook, "Options", column, 1, columns); column += columns.Length; return f; }
        string Script(OperationRun run) { RScript s = RScriptWriter.Write(Record(run), null, null); if (!s.HasRecipe) throw new Exception("FAIL the run's script has no recipe"); return s.Text; }
        IList<ParameterBag> Block(ParameterBag bag, string name) => (IList<ParameterBag>)bag[bag.ContainsKey(name) ? name : "*" + name].AsObject;   // a block list is keyed with a star
        double Num(ParameterBag bag, string key) { object o = bag[key].AsObject; return o is double d ? d : o is int i ? i : double.Parse(o.ToString(), inv); }   // a report value, whatever its type
        double[] Numbers(string text, string pattern) => Regex.Matches(text, pattern, RegexOptions.Multiline).Select(m => double.Parse(m.Groups[1].Value, inv)).ToArray();
        // R prints the recipe's figures to 6 decimal places, so a figure near 0 can differ from the engine's by half a unit in the sixth
        bool Near(double a, double b, double relative = 1e-5) => Math.Abs(a - b) <= relative * Math.Max(Math.Abs(a), Math.Abs(b)) + 6e-7;
        bool Agree(double[] r, double[] engine, double relative = 1e-5) => r.Length == engine.Length && r.Length > 0 && r.Zip(engine).All(pair => Near(pair.First, pair.Second, relative));
        string Shown(double[] v) => string.Join(" ", v.Select(x => x.ToString("G7", inv)));
        // a frame of categories as the worksheet's acquisition builds one (strata): a classifier with the codes 0, 1 ... and their labels
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

        // multiple linear regression without an intercept and with weights: lm gets the formula without the intercept and the weights
        double[] y = { 12.1, 14.3, 15.2, 18.9, 20.4, 22.8, 23.9, 27.1, 29.6, 30.2, 33.8, 35.1 };
        double[] x1 = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };
        double[] x2 = { 3.2, 1.5, 4.8, 2.2, 5.9, 3.1, 6.4, 2.8, 7.1, 4.4, 8.3, 5.0 };
        double[] w = { 1, 2, 1, 3, 1, 2, 1, 3, 1, 2, 1, 3 };
        var (run, output) = Run("MultipleLinearRegression", new() { ["ci"] = 0.95, ["calculateIntercept"] = false, ["useWeights"] = true, ["outcome"] = Sheet(("y", y)), ["weights"] = Sheet(("w", w)), ["predictors"] = Sheet(("x1", x1), ("x2", x2)) });
        string script = Script(run);
        Check(script.Contains("intercept <- FALSE") && script.Contains("weights <- c(1, 2, 1, 3") && script.Contains("intercept = intercept") && script.Contains("weights = weights"),
              "the regression's script carries the run's intercept option and weights and hands them to lm");
        string r = RunR(script, Path.Combine(folder, "mlr"));
        IList<ParameterBag> cols = Block(Block(output.ParameterBag, "row")[0], "col");
        double[] engineB = cols.Select(c => Num(c, "val_b")).ToArray(), engineT = cols.Select(c => Num(c, "t")).ToArray();
        double[] rB = Numbers(r, "^.+ b\\d+ = (-?[0-9.]+)"), rT = Numbers(r, "^.+ b\\d+ = .*  t = (-?[0-9.]+)");
        Console.WriteLine($"      weighted regression without an intercept: engine b {Shown(engineB)}, R b {Shown(rB)}; t {Shown(engineT)} and {Shown(rT)}");
        Check(Agree(rB, engineB) && Agree(rT, engineT) && engineB.Length == 2, "R's weighted regression without an intercept gives the engine's two coefficients and t statistics");

        // logistic regression of individual responses with one predictor and a 99% interval: the rows are merged by covariate pattern as the
        // report does, the one predictor's loop runs, and the interval is the run's level
        double[] dose = { 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4 };
        double[] response = { 0, 0, 0, 1, 0, 0, 1, 1, 0, 1, 1, 1, 1, 1, 1, 0 };
        (run, output) = Run("LogisticRegression", new() { ["grouping"] = "individual", ["accuracy"] = "0.0000001", ["intercept"] = true, ["weights"] = false, ["gamma"] = 0.99, ["response"] = Sheet(("response", response)), ["predictors"] = Sheet(("dose", dose)) });
        script = Script(run);
        Check(script.Contains("total <- NULL") && script.Contains("confidence <- 0.99") && script.Contains("if (is.null(total))"), "the logistic script of individual responses has no totals, the run's confidence level, and the merging of the rows");
        r = RunR(script, Path.Combine(folder, "logistic-individual"));
        IList<ParameterBag> slopes = Block(output.ParameterBag, "var").Where(v => v.ContainsKey("*ci")).ToList();   // the predictors' rows: the intercept's has no odds ratio
        double[] engineOr = slopes.Select(v => Num(v, "or")).ToArray(), engineLo = slopes.Select(v => Num(Block(v, "ci")[0], "lci")).ToArray(), engineHi = slopes.Select(v => Num(Block(v, "ci")[0], "uci")).ToArray();
        double[] rOr = Numbers(r, "^\\S+ odds ratio = (-?[0-9.]+)"), rLo = Numbers(r, "^\\S+ odds ratio = -?[0-9.]+ +\\( +(-?[0-9.]+) to"), rHi = Numbers(r, "^\\S+ odds ratio = -?[0-9.]+ +\\( +-?[0-9.]+ to (-?[0-9.]+)");
        double rDev = Numbers(r, "^Deviance goodness of fit chi-square = (-?[0-9.]+)")[0], engineDev = Num(output.ParameterBag, "dev");
        Console.WriteLine($"      individual logistic: engine OR {Shown(engineOr)} ({Shown(engineLo)} to {Shown(engineHi)}), deviance {engineDev:G7}; R {Shown(rOr)} ({Shown(rLo)} to {Shown(rHi)}), deviance {rDev:G7}");
        Check(Agree(rOr, engineOr) && Agree(rLo, engineLo, 1e-4) && Agree(rHi, engineHi, 1e-4) && engineOr.Length == 1 && Near(rDev, engineDev),
              "R's logistic regression of the individual responses gives the engine's odds ratio, its 99% limits and the deviance of the merged rows");

        // logistic regression of grouped counts without an intercept and with prior weights
        double[] total = { 20, 20, 20, 20, 20, 20 }, responders = { 3, 6, 9, 12, 14, 17 }, level = { -2, -1, 0, 1, 2, 3 }, second = { 1, 0, 1, 0, 1, 0 }, prior = { 1, 2, 1, 2, 1, 2 };
        (run, output) = Run("LogisticRegression", new() { ["grouping"] = "grouped", ["accuracy"] = "0.0000001", ["intercept"] = false, ["weights"] = true, ["gamma"] = 0.95, ["total"] = Sheet(("total", total)), ["response"] = Sheet(("responders", responders)), ["weight"] = Sheet(("prior", prior)), ["predictors"] = Sheet(("level", level), ("second", second)) });
        script = Script(run);
        r = RunR(script, Path.Combine(folder, "logistic-weighted"));
        slopes = Block(output.ParameterBag, "var").Where(v => v.ContainsKey("*ci")).ToList();
        engineOr = slopes.Select(v => Num(v, "or")).ToArray(); rOr = Numbers(r, "^\\S+ odds ratio = (-?[0-9.]+)");
        Console.WriteLine($"      weighted logistic without an intercept: engine OR {Shown(engineOr)}, R {Shown(rOr)}");
        Check(script.Contains("intercept <- FALSE") && script.Contains("weight <- c(1, 2, 1, 2, 1, 2)") && Agree(rOr, engineOr) && engineOr.Length == 2,
              "R's logistic regression without an intercept and with the prior weights gives the engine's two odds ratios");

        // Poisson regression of counts alone (no exposure) with one predictor, then with an exposure and prior weights
        double[] counts = { 2, 3, 6, 7, 8, 9, 10, 12, 15 }, xp = { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        (run, output) = Run("PoissonRegression", new() { ["gamma"] = 0.95, ["grouping"] = "individual", ["accuracy"] = "0.0000001", ["intercept"] = true, ["weights"] = false, ["has-exposure"] = "false", ["response"] = Sheet(("counts", counts)), ["predictors"] = Sheet(("xp", xp)) });
        script = Script(run);
        Check(script.Contains("exposure <- NULL"), "the Poisson script of counts alone has no exposure");
        r = RunR(script, Path.Combine(folder, "poisson-counts"));
        IList<ParameterBag> preds = Block(output.ParameterBag, "pred");
        double[] engineCoef = preds.Select(p => Num(p, "res")).ToArray(), rCoef = Numbers(r, "^.+ b\\d+ = (-?[0-9.]+)  z = ");
        Console.WriteLine($"      Poisson of counts alone: engine b {Shown(engineCoef)}, R b {Shown(rCoef)}");
        Check(Agree(rCoef, engineCoef) && engineCoef.Length == 2, "R's Poisson regression of the counts alone with one predictor gives the engine's two coefficients");
        double[] exposure = { 10, 12, 15, 14, 16, 15, 18, 20, 22 }, pw = { 1, 1, 2, 1, 2, 1, 1, 2, 1 };
        (run, output) = Run("PoissonRegression", new() { ["gamma"] = 0.95, ["grouping"] = "individual", ["accuracy"] = "0.0000001", ["intercept"] = true, ["weights"] = true, ["has-exposure"] = "true", ["response"] = Sheet(("counts", counts)), ["exposure"] = Sheet(("exposure", exposure)), ["weight"] = Sheet(("pw", pw)), ["predictors"] = Sheet(("xp", xp)) });
        script = Script(run);
        r = RunR(script, Path.Combine(folder, "poisson-weighted"));
        preds = Block(output.ParameterBag, "pred");
        engineCoef = preds.Select(p => Num(p, "res")).ToArray(); rCoef = Numbers(r, "^.+ b\\d+ = (-?[0-9.]+)  z = ");
        Console.WriteLine($"      Poisson with an exposure and prior weights: engine b {Shown(engineCoef)}, R b {Shown(rCoef)}");
        Check(script.Contains("weight <- c(1, 1, 2, 1, 2, 1, 1, 2, 1)") && Agree(rCoef, engineCoef) && engineCoef.Length == 2, "R's Poisson regression with the exposure as an offset and the prior weights gives the engine's coefficients");

        // polynomial regression of degree 3: the script fits the run's degree
        double[] py = { 1.2, 1.9, 4.1, 8.3, 15.2, 26.1, 40.8, 60.9, 86.2, 118.1, 157.5, 204.2 };
        (run, output) = Run("PolynomialRegression", new() { ["degree"] = "3", ["y"] = Sheet(("y", py)), ["x"] = Sheet(("x", x1)) });
        script = Script(run);
        r = RunR(script, Path.Combine(folder, "polynomial"));
        cols = Block(Block(output.ParameterBag, "row")[0], "col");
        engineB = cols.Select(c => Num(c, "val_b")).ToArray(); rB = Numbers(r, "^.+ b\\d+ *= (-?[0-9.]+)");
        Console.WriteLine($"      polynomial of degree 3: engine b {Shown(engineB)}, R b {Shown(rB)}");
        Check(Regex.IsMatch(script, "^degree <- \"?3\"?", RegexOptions.Multiline) && script.Contains("poly(size, .(degree), raw = TRUE)") && Agree(rB, engineB) && engineB.Length == 4, "R's polynomial regression of degree 3 gives the engine's four coefficients");

        // Cox regression with strata: the model is stratified as the run was
        double[] times = { 6, 7, 10, 15, 19, 25, 2, 4, 8, 12, 14, 20, 23, 30 }, events = { 1, 1, 1, 1, 0, 1, 1, 1, 1, 0, 1, 1, 1, 0 }, treatment = { 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1 }, stratum = { 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2 };
        (run, output) = Run("CoxRegression", new() { ["accuracy"] = 0.000000001, ["centre-continuous-covariates"] = true, ["useTimesAdjustment"] = false, ["times"] = Sheet(("times", times)), ["events"] = Sheet(("events", events)), ["predictors"] = Sheet(("treatment", treatment)), ["strata"] = Classifier("stratum", stratum) });
        script = Script(run);
        r = RunR(script, Path.Combine(folder, "cox"));
        double engineCox = Num(Block(output.ParameterBag, "pred")[0], "b"), rCox = Numbers(r, "^\\S+ b1 = (-?[0-9.]+)")[0];
        Console.WriteLine($"      stratified Cox: engine b {engineCox:G7}, R b {rCox:G7}");
        Check(script.Contains("stratum <- c(") && script.Contains("strata(sd_stratum)") && Near(rCox, engineCox), "R's stratified Cox regression gives the engine's coefficient");

        // conditional logistic regression with one predictor: the loops take the run's number of coefficients
        // ten matched pairs, case then control: five pairs with the case alone exposed, one with the control alone, four concordant, so that
        // the estimate is finite (log 5)
        double[] pair = { 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7, 8, 8, 9, 9, 10, 10 }, caseControl = { 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0 },
                 exposure1 = { 1, 0, 1, 0, 1, 1, 0, 1, 1, 0, 0, 0, 1, 0, 1, 1, 0, 0, 1, 0 };
        (run, output) = Run("ConditionalLogisticRegression", new() { ["gamma"] = 0.95, ["accuracy"] = "0.0000001", ["show-counts"] = false, ["stratum"] = Classifier("pair", pair), ["case-control"] = Sheet(("case", caseControl)), ["predictors"] = Sheet(("exposure1", exposure1)) });
        script = Script(run);
        r = RunR(script, Path.Combine(folder, "conditional"));
        double engineCond = Num(Block(output.ParameterBag, "est")[0], "b"), rCond = Numbers(r, "^exposure1 (-?[0-9.]+) -?[0-9.]+  z =")[0];
        Console.WriteLine($"      conditional logistic with one predictor: engine b {engineCond:G7}, R b {rCond:G7}");
        Check(Near(rCond, engineCond, 1e-4), "R's conditional logistic regression with one predictor runs and gives the engine's coefficient");

        // the log-rank test with strata and the Gehan-Breslow weight: the script's stratified test agrees with R's survdiff on the run's strata
        string logrank = "{\"operation\":\"LogRank\",\"file\":\"log-rank.R\",\"inputs\":[" +
            "{\"name\":\"gid\",\"columns\":[{\"title\":\"group\",\"values\":[1,2,1,2,1,2,1,2,1,2,1,2,1,2]}]}," +
            "{\"name\":\"times\",\"columns\":[{\"title\":\"time\",\"values\":[6,7,10,15,19,25,2,4,8,12,14,20,23,30]}]}," +
            "{\"name\":\"deaths\",\"columns\":[{\"title\":\"dead\",\"values\":[1,1,1,1,0,1,1,1,1,0,1,1,1,0]}]}," +
            "{\"name\":\"strata\",\"columns\":[{\"title\":\"stratum\",\"values\":[1,1,1,1,1,1,1,2,2,2,2,2,2,2]}]}]," +
            "\"settings\":{\"gamma\":0.95,\"wt_method\":\"2\"}}";
        script = RScriptWriter.Write(JsonDocument.Parse(RecordOf(JsonDocument.Parse(logrank).RootElement)).RootElement, null, null).Text;
        r = RunR(script, Path.Combine(folder, "logrank"));
        double survdiff = Numbers(r, "Chisq= *(-?[0-9.]+) +on 1 degrees of freedom")[^1], combined = Numbers(r, "^overall chi-square = (-?[0-9.]+)")[0];
        Console.WriteLine($"      stratified log-rank: survdiff {survdiff:G7}, the script's combined test {combined:G7}");
        Check(script.Contains("wt_method <- \"2\"") && r.Contains("Generalised Wilcoxon (Gehan-Breslow)") && r.Contains("STRATUM 2 of 2") && !r.Contains("1976, 1990, 2240") && Math.Abs(Math.Round(combined, 1) - survdiff) < 1e-9,   // survdiff prints its chi-square rounded to one decimal place
              "the log-rank script runs the stratified test on the run's strata, agreeing with survdiff, with the run's Wilcoxon weight and none of the help's data");

        // a bar chart of four series and a box plot of three groups: the scripts draw the run's series and groups
        string bar = "{\"operation\":\"BarPlot\",\"file\":\"bar-plot.R\",\"inputs\":[{\"name\":\"labels\",\"columns\":[{\"title\":\"Blood group\",\"values\":[\"O\",\"A\",\"B\",\"AB\"]}]}," +
            "{\"name\":\"values\",\"columns\":[{\"title\":\"First\",\"values\":[44,45,8,3]},{\"title\":\"Second\",\"values\":[79,16,4,1]},{\"title\":\"Third\",\"values\":[61,39,0,0]},{\"title\":\"Fourth\",\"values\":[50,30,15,5]}]}]}";
        string barScript = RScriptWriter.Write(JsonDocument.Parse(RecordOf(JsonDocument.Parse(bar).RootElement)).RootElement, null, null).Text;
        string barResult = RunR(barScript + "\nstopifnot(identical(unname(bar_values),rbind(c(44,45,8,3),c(79,16,4,1),c(61,39,0,0),c(50,30,15,5))),identical(colnames(bar_values),c('O','A','B','AB')))\ncat('FOUR_SERIES_OK\\n')\n", Path.Combine(folder, "bar"));
        string box = "{\"operation\":\"BoxWhiskerPlotText\",\"file\":\"box-whisker-plot-text.R\",\"list\":[{\"title\":\"One\",\"values\":[12,15,11,19,22,14,30]},{\"title\":\"Two\",\"values\":[20,25,21,29,32,24,27]},{\"title\":\"Three\",\"values\":[5,8,11,9,6,40,7]}]}";
        script = RScriptWriter.Write(JsonDocument.Parse(RecordOf(JsonDocument.Parse(box).RootElement)).RootElement, null, null).Text;
        r = RunR(script, Path.Combine(folder, "box"));
        Check(barResult.Contains("FOUR_SERIES_OK") && Regex.Matches(r, "^\\S+ \\(n = 7\\)", RegexOptions.Multiline).Count == 3, "the bar chart of four series and the box plot of three groups draw the run's series and groups");
    }
}
