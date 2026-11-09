using System.Text.RegularExpressions;
using StatsDirect.Charting;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.R;
using StatsDirect.Templates;
using StatsDirect.Utilities;

internal static partial class Program
{
    // The regression engine and model recipes in 5.3.9. Each case is acquired from worksheet cells, run through the template processor,
    // and compared with its native output bag, with R's replay of the recorded run and, where a native figure changed, with an
    // independent fit of the literal data in R.
    private static void CheckModels539(string folder)
    {
        RRecipes.Folder = Metadata("Recipes");
        Directory.CreateDirectory(folder);
        CheckModels539FewerRows(folder);
        CheckModels539LonePredictor(folder);
        CheckModels539NoPredictorLeft(folder);
        CheckModels539ZeroResidualDf(folder);
        CheckModels539ConstantPredictor(folder);
    }

    private static readonly double[] Models539Y = { 8.73, 8.83, 14.58, 9.89, 9.16, 11.35, 5.24, 7.19, 13.24, 7.65 };
    private static readonly double[] Models539X1 = { 7.55, 5.12, 7.15, 5.93, 3.56, 4.63, 2.06, 2.18, 9.42, 7.08 };
    private static readonly double[] Models539X2 = { 1, 0, 1, 1, 0, 1, 0, 1, 1, 0 };

    /// <summary>Worksheet cells, a column for each (title, values); a missing value is a blank cell. Acquired as the grid acquires a selection.</summary>
    private static Func<DataAcquisitionMode, int[], DataFrame> Models539Sheet(params (string title, double[] values)[] columns)
    {
        RecordCells cells = new(columns.Select(c => new object[] { c.title }.Concat(c.values.Select(v => v == Constant.MISSING ? null : (object)v)).ToArray()).ToArray());
        int rows = columns[0].values.Length + 1;
        return (mode, selected) =>
        {
            DataFrame frame = new();
            foreach (int column in selected)
                foreach (IVariable variable in cells.Acquire(column, 0, rows, mode).Variables)
                    frame.Variables.Add(variable);
            return frame;
        };
    }

    private static DataFrame Models539Select(Func<DataAcquisitionMode, int[], DataFrame> sheet, params int[] columns) => sheet(DataAcquisitionMode.NumericPreserveRows, columns);
    private static double Models539Number(ParameterBag bag, string key) => Convert.ToDouble(bag[key].AsObject, inv);
    private static IList<ParameterBag> Models539Block(ParameterBag bag, string key) => (IList<ParameterBag>)bag["*" + key].AsObject;
    private static string Models539Vector(IEnumerable<double> values) => RScriptWriter.Vector(values.Cast<object>().ToArray());
    private static string Models539Equal(string expression, IEnumerable<double> expected, double tolerance = 1e-8) =>
        "stopifnot(isTRUE(all.equal(unname(c(" + expression + "))," + Models539Vector(expected) + ",tolerance=" + tolerance.ToString("R", inv) + ",check.attributes=FALSE)))\n";
    private static string[] Models539Warnings(ParameterBag bag) => Models539Block(bag, "warn").Select(w => w["warn"].AsString).ToArray();

    /// <summary>R run on the replay of the recorded run, with the given checks after it: the output, which must reach the end.</summary>
    private static string Models539Verify(string folder, string label, OperationRun run, string assertions)
    {
        RScript script = RScriptWriter.Write(Record(run), null, null);
        Check(script.HasRecipe && !script.DataUnavailable, label + " exports the acquired data and settings for replay");
        string result = RunR(script.Text + "\n" + assertions + "\ncat('MODELS_539_OK\\n')\n", Path.Combine(folder, label));
        Check(result.Contains("MODELS_539_OK"), label + " replays in R and agrees with the native figures");
        return result;
    }

    private static (OperationRun run, ParameterBag model) Models539Linear(Func<DataAcquisitionMode, int[], DataFrame> sheet, int outcome, int weights, int[] predictors, bool intercept)
    {
        Dictionary<string, object> answers = new()
        {
            ["outcome"] = Models539Select(sheet, outcome), ["predictors"] = Models539Select(sheet, predictors), ["ci"] = .95,
            ["calculateIntercept"] = intercept, ["useWeights"] = weights >= 0
        };
        if (weights >= 0)
            answers["weights"] = Models539Select(sheet, weights);
        var (run, output) = Run("MultipleLinearRegression", answers);
        return (run, output.ParameterBag);
    }

    private static ParameterBag Models539Anova(ParameterBag model) => Run("MultipleLinearRegressionAnova", new() { ["ci"] = .95 }, model).output.ParameterBag;

    // Item 1: rows left out for missing values or zero weights must not leave fewer rows than parameters (a residual df of -1 and a
    // meaningless F); both routes are refused, with a message.
    private static void CheckModels539FewerRows(string folder)
    {
        double[] two = { 1, 1, 0, 0, 0, 0, 0, 0, 0, 0 }, one = { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        double[] gappy = (double[])Models539Y.Clone();
        for (int i = 1; i < 9; i++) gappy[i] = Constant.MISSING;
        var sheet = Models539Sheet(("Y", Models539Y), ("Two", two), ("One", one), ("X1", Models539X1), ("X2", Models539X2), ("Gappy", gappy));
        foreach (var (label, outcome, weights, predictors) in new[] { ("zero weights", 0, 1, new[] { 3, 4 }), ("missing outcomes", 5, -1, new[] { 3, 4 }), ("one positive weight", 0, 2, new[] { 3 }) })
        {
            try
            {
                var (_, model) = Models539Linear(sheet, outcome, weights, predictors, true);
                Check(false, "multiple regression with " + label + " must refuse fewer usable rows than parameters (residual df " + (Models539Anova(model)["res_df"].AsObject) + ")");
            }
            catch (Exception ex) when (ex.GetBaseException().Message.Contains("fewer complete observations with positive prior weight than parameters"))
            {
                Check(true, "multiple regression with " + label + " refuses fewer usable rows than parameters");
            }
        }
    }

    // Item 2: a lone aliased predictor (a constant beside the intercept, a column of zeros without one) is dropped and refitted, so that
    // the residual df, root MSE and adjusted R squared are those of the model without it, as lm gives them.
    private static void CheckModels539LonePredictor(string folder)
    {
        double[] weights = { 1, 2, 1, 3, 2, 1, 2, 3, 1, 2 };
        var sheet = Models539Sheet(("Y", Models539Y), ("W", weights), ("C", Enumerable.Repeat(3.0, 10).ToArray()), ("Z", new double[10]));
        foreach (var (label, weighted, predictor, intercept) in new[] { ("constant", false, 2, true), ("weighted-constant", true, 2, true), ("zeros-no-intercept", false, 3, false) })
        {
            var (run, model) = Models539Linear(sheet, 0, weighted ? 1 : -1, new[] { predictor }, intercept);
            string title = predictor == 2 ? "C" : "Z";
            Check(Models539Warnings(model).Any(w => w.StartsWith(title + " dropped from the model", StringComparison.Ordinal)), label + ": the aliased predictor is dropped with a warning");
            var coefficients = Models539Block(Models539Block(model, "row")[0], "col");
            Check(coefficients.Count == (intercept ? 1 : 0), label + ": the refit has the intercept alone, or nothing without one");
            ParameterBag anova = Models539Anova(model);
            Check(Models539Number(anova, "reg_df") == 0 && Models539Number(anova, "f") == Constant.MISSING && Models539Number(anova, "p") == Constant.MISSING && Models539Number(anova, "reg_mean") == Constant.MISSING,
                label + ": no regression df, so no regression mean square, F or P");
            string data = "Y <- " + Models539Vector(Models539Y) + "\nW <- " + Models539Vector(weighted ? weights : Enumerable.Repeat(1.0, 10)) + "\n";
            string oracle = data + "m <- lm(Y ~ " + (intercept ? "1" : "0") + ", weights = W)\nms <- summary(m)\n" +
                Models539Equal("m$df.residual, ms$sigma, 100 * ms$r.squared, 100 * ms$adj.r.squared, sum(W * residuals(m)^2), m$df.residual",
                    new[] { "res_df", "mse", "r2", "ra2", "res_sum", "tot_df" }.Select(key => Models539Number(anova, key)), 1e-10) +
                (intercept ? Models539Equal("coef(m)", new[] { Models539Number(coefficients[0], "val_b") }, 1e-12) : "");
            string output = Models539Verify(folder, "lone-" + label, run, oracle);
            Check(Regex.IsMatch(output, @"^Residual " + Regex.Escape(Models539R(anova, "res_sum")) + " " + Models539Number(anova, "res_df").ToString(inv) + " ", RegexOptions.Multiline) &&
                Regex.IsMatch(output, @"^Root MSE = " + Regex.Escape(Models539R(anova, "mse")) + @"\s*$", RegexOptions.Multiline),
                label + ": the replay prints the native residual line and root MSE");
        }
    }

    /// <summary>A figure as the replay prints it: to 6 places with trailing zeros dropped.</summary>
    private static string Models539R(ParameterBag bag, string key) => Math.Round(Models539Number(bag, key), 6).ToString("0.######", inv);

    // Item 3: with every predictor dropped beside the intercept the ANOVA has no regression df (it had one, and read a slope that is not
    // there); its follow-ons run, and best subset selection says there is nothing to choose.
    private static void CheckModels539NoPredictorLeft(string folder)
    {
        foreach (int rows in new[] { 10, 3 })
        {
            double[] y = Models539Y.Take(rows).ToArray();
            var sheet = Models539Sheet(("Y", y), ("C1", Enumerable.Repeat(3.0, rows).ToArray()), ("C2", Enumerable.Repeat(5.0, rows).ToArray()));
            var (run, model) = Models539Linear(sheet, 0, -1, new[] { 1, 2 }, true);
            Check(Models539Warnings(model).Any(w => w.StartsWith("C1, C2 dropped from the model", StringComparison.Ordinal)), rows + " rows: both constants are dropped beside the intercept");
            ParameterBag anova = Models539Anova(model);
            Check(Models539Number(anova, "reg_df") == 0 && Models539Number(anova, "res_df") == rows - 1 && Models539Number(anova, "f") == Constant.MISSING && Models539Number(anova, "r2") == 0,
                rows + " rows: the ANOVA of the intercept alone has no regression df and no F");
            string oracle = "Y <- " + Models539Vector(y) + "\nm <- lm(Y ~ 1)\nms <- summary(m)\n" +
                Models539Equal("ms$sigma, 100 * ms$adj.r.squared, deviance(m), sum((Y - mean(Y))^2)", new[] { "mse", "ra2", "res_sum", "tot_sum" }.Select(key => Models539Number(anova, key)), 1e-10);
            Models539Verify(folder, "no-predictor-left-" + rows, run, oracle);
            if (rows != 10)
                continue;
            foreach (var (name, answers) in new (string, Dictionary<string, object>)[]
            {
                ("MultipleLinearRegressionParameterDetail", new()), ("MultipleLinearRegressionPrediction", new() { ["candidatePredictors"] = model["candidatePredictors"].AsDataFrame }),
                ("MultipleLinearRegressionResiduals", new() { ["options"] = new Dictionary<string, bool>() }), ("PlotMultipleLinearRegressionResiduals", new()), ("Xxi", new() { ["outputToGrid"] = false })
            })
            {
                Run(name, answers, model);
                Check(true, "the intercept-only model's " + name + " follow-on runs");
            }
            try
            {
                Run("MultipleLinearRegressionBestSubset", new() { ["selectedPredictors"] = new bool[0], ["selector"] = "minimumMallows" }, model);
                Check(false, "best subset selection must refuse a model with no predictors left");
            }
            catch (Exception ex) when (ex.GetBaseException().Message.Contains("No subset of the predictors can be chosen"))
            {
                Check(true, "best subset selection says a model with no predictors left has no subset to choose");
            }
        }
    }

    // Items 14 and 10: as many usable rows as parameters, reached through zero weights or missing values, is an exact fit. Native t, P, F,
    // residual mean square, root MSE, adjusted R squared and Durbin-Watson are unavailable (R gives NaN), the residual sum of squares is
    // 0 and R squared 100%; the replay completes and prints *. (With these weights the singular value fallback had given t and F of 0,
    // adjusted R squared of minus infinity, and a residual sum of squares of 53.02 on 0 df with R squared 30.34%.)
    private static void CheckModels539ZeroResidualDf(string folder)
    {
        double[] y = Models539Y.Take(7).ToArray(), x1 = Models539X1.Take(7).ToArray(), x2 = Models539X2.Take(7).ToArray();
        double[] weights = { 1, 2, 1, 0, 0, 0, 0 };
        double[] gappy = (double[])y.Clone();
        gappy[1] = gappy[3] = gappy[4] = gappy[5] = Constant.MISSING;
        var sheet = Models539Sheet(("Y", y), ("W", weights), ("X1", x1), ("X2", x2), ("Gappy", gappy));
        foreach (var (label, outcome, weighted, used) in new[] { ("zero-weights", 0, true, new[] { 0, 1, 2 }), ("missing-values", 4, false, new[] { 0, 2, 6 }) })
        {
            var (run, model) = Models539Linear(sheet, outcome, weighted ? 1 : -1, new[] { 2, 3 }, true);
            Check(Models539Warnings(model).Any(w => w.StartsWith("saturated model", StringComparison.Ordinal)) && !Models539Warnings(model).Any(w => w.Contains("SVD")),
                label + ": the exact fit is kept, with a warning that it is saturated");
            var coefficients = Models539Block(Models539Block(model, "row")[0], "col");
            Check(coefficients.All(c => double.IsNaN(Models539Number(c, "t")) && !double.IsFinite(Models539Number(c, "p"))) &&
                coefficients.Skip(1).All(c => double.IsNaN(Models539Number(Models539Block(c, "r")[0], "rp"))),
                label + ": no t, P or partial correlation without residual df");
            ParameterBag anova = Models539Anova(model);
            Check(new[] { "res_mean", "mse", "f", "p", "ra2", "dw" }.All(key => Models539Number(anova, key) == Constant.MISSING) && Models539Number(anova, "res_df") == 0,
                label + ": no residual mean square, root MSE, F, P, adjusted R squared or Durbin-Watson without residual df");
            Check(Math.Abs(Models539Number(anova, "res_sum")) <= 1e-9 * Models539Number(anova, "tot_sum") && Math.Abs(Models539Number(anova, "r2") - 100) <= 1e-7,
                label + ": the exact fit has no residual sum of squares and R squared of 100%");
            string data = "Y <- " + Models539Vector(used.Select(i => y[i])) + "\nX1 <- " + Models539Vector(used.Select(i => x1[i])) + "\nX2 <- " + Models539Vector(used.Select(i => x2[i])) +
                "\nW <- " + Models539Vector(used.Select(i => weighted ? weights[i] : 1.0)) + "\n";
            string oracle = data + "m <- lm(Y ~ X1 + X2, weights = W)\n" +
                Models539Equal("coef(m)", coefficients.Select(c => Models539Number(c, "val_b")), 1e-9) +
                Models539Equal("sum((W * (fitted(m) - weighted.mean(Y, W)))^2 / W), m$df.residual", new[] { Models539Number(anova, "reg_sum"), 0 }, 1e-9);
            string output = Models539Verify(folder, "zero-df-" + label, run, oracle);
            Check(Regex.Matches(output, @"t = \*  P = \*").Count == 3 && output.Contains("Root MSE = *") && Regex.IsMatch(output, @"F = \* +P = \*") && output.Contains("Ra2 = *%"),
                label + ": the replay prints * for each statistic that needs residual df");
        }
    }

    private static Dictionary<string, object> Models539PoissonAnswers(Func<DataAcquisitionMode, int[], DataFrame> sheet, int[] predictors, bool intercept, bool exposed, bool weighted) => new()
    {
        ["response"] = Models539Select(sheet, 0), ["weight"] = weighted ? Models539Select(sheet, 1) : null, ["exposure"] = exposed ? Models539Select(sheet, 2) : null,
        ["predictors"] = Models539Select(sheet, predictors), ["gamma"] = .95, ["grouping"] = "individual", ["accuracy"] = "0.00000000001",
        ["intercept"] = intercept, ["weights"] = weighted, ["has-exposure"] = exposed ? "true" : "false"
    };

    private static readonly double[] Models539Counts = { 2, 5, 9, 14, 20, 11, 6, 4, 8, 7, 12, 16 };
    private static readonly double[] Models539Exposures = { 100, 120, 140, 160, 180, 150, 110, 90, 130, 120, 150, 170 };
    private static readonly double[] Models539Dose = Enumerable.Range(0, 12).Select(i => 1.5 + i).ToArray();

    private static Dictionary<string, object> Models539LogisticAnswers(Func<DataAcquisitionMode, int[], DataFrame> sheet, int[] predictors, bool intercept = true, bool grouped = true) => grouped
        ? new()
        {
            ["response"] = Models539Select(sheet, 0), ["total"] = Models539Select(sheet, 1), ["weight"] = Models539Select(sheet, 2), ["predictors"] = Models539Select(sheet, predictors),
            ["gamma"] = .95, ["grouping"] = "grouped", ["accuracy"] = "0.00000000001", ["intercept"] = intercept, ["weights"] = true
        }
        : new()
        {
            ["response"] = Models539Select(sheet, 0), ["weight"] = Models539Select(sheet, 1), ["predictors"] = Models539Select(sheet, predictors),
            ["gamma"] = .95, ["grouping"] = "individual", ["accuracy"] = "0.00000000001", ["intercept"] = intercept, ["weights"] = true
        };

    // Item 7: with an intercept, a nonzero constant predictor is aliased: rank 1 and n - 1 residual df, the fit of the intercept alone,
    // with a coefficient of 0 for it (it had stopped the singular value decomposition, or left rank 2, 10 df and coefficients of 1e14).
    // The logistic fit shares the routine.
    private static void CheckModels539ConstantPredictor(string folder)
    {
        foreach (double c in new[] { 0.7, 1.3, 2, 3 })
            foreach (bool exposed in new[] { false, true })
            {
                var sheet = Models539Sheet(("Outcome", Models539Counts), ("Prior", Enumerable.Repeat(1d, 12).ToArray()), ("Exposure", Models539Exposures), ("Constant", Enumerable.Repeat(c, 12).ToArray()));
                string label = "poisson-constant-" + c.ToString(inv) + (exposed ? "-exposed" : "");
                var (run, output) = Run("PoissonRegression", Models539PoissonAnswers(sheet, new[] { 3 }, true, exposed, false));
                ParameterBag model = Run("PoissonRegressionModel", new(), output.ParameterBag).output.ParameterBag;
                var unscaled = Models539Block(model, "unscaled");
                double intercept = Math.Log(Models539Counts.Sum() / (exposed ? Models539Exposures.Sum() : 12.0));
                Check(Models539Number(model, "rank") == 1 && Models539Number(model, "idf") == 11 && Close(Models539Number(unscaled[0], "coef"), intercept, 1e-9) &&
                    Models539Number(unscaled[1], "coef") == 0 && Models539Number(unscaled[1], "err") == 0,
                    label + ": rank 1, 11 residual df and the intercept log(sum y / sum t), with no coefficient for the constant");
                string oracle = "y <- " + Models539Vector(Models539Counts) + "\nt <- " + Models539Vector(exposed ? Models539Exposures : Enumerable.Repeat(1.0, 12)) + "\n" +
                    "g <- glm(y ~ 1, offset = log(t), family = poisson, control = glm.control(epsilon = 1e-14, maxit = 100))\n" +
                    Models539Equal("deviance(g), g$df.residual, coef(g), sqrt(vcov(g)[1, 1])", new[] { Models539Number(model, "dev"), Models539Number(model, "idf"), Models539Number(unscaled[0], "coef"), Models539Number(unscaled[0], "err") }, 1e-8) +
                    Models539Equal("fit$rank, fit$df.residual, coef(fit)[1]", new[] { 1, 11, Models539Number(unscaled[0], "coef") }, 1e-8) + "stopifnot(is.na(coef(fit)[2]))\n";
                Models539Verify(folder, label, run, oracle);
            }
        double[] response = { 2, 3, 4, 5, 6, 8, 10, 11, 12, 14, 15, 17 }, weights = { 1, 2, 3, 1, 2, 3, 1, 2, 3, 1, 2, 3 };
        foreach (double c in new[] { 0.7, 2 })
        {
            var sheet = Models539Sheet(("Outcome", response), ("Total", Enumerable.Repeat(20d, 12).ToArray()), ("Prior", weights), ("Constant", Enumerable.Repeat(c, 12).ToArray()));
            string label = "logistic-constant-" + c.ToString(inv);
            var (run, output) = Run("LogisticRegression", Models539LogisticAnswers(sheet, new[] { 3 }));
            ParameterBag model = Run("LogisticRegressionModel", new(), output.ParameterBag).output.ParameterBag;
            var parameters = Models539Block(model, "parameters");
            Check(Models539Number(model, "rank") == 1 && Models539Number(model, "idf") == 2 && Models539Number(parameters[1], "coef") == 0,
                label + ": the three weight patterns give rank 1 and 2 residual df, with no coefficient for the constant");
            // the records stacked into their three patterns of prior weight, as the native analysis stacks them
            double[] r = Enumerable.Range(1, 3).Select(w => response.Where((_, i) => weights[i] == w).Sum()).ToArray(), n = Enumerable.Repeat(80.0, 3).ToArray();
            string oracle = "r <- " + Models539Vector(r) + "\nn <- " + Models539Vector(n) + "\nw <- c(1, 2, 3)\n" +
                "g <- glm(cbind(r, n - r) ~ 1, family = binomial, weights = w, control = glm.control(epsilon = 1e-14, maxit = 100))\n" +
                Models539Equal("deviance(g), g$df.residual, coef(g)", new[] { Models539Number(model, "dev"), Models539Number(model, "idf"), Models539Number(parameters[0], "coef") }, 1e-8) +
                "stopifnot(fit$rank == 1L, is.na(coef(fit)[2]))\n";
            Models539Verify(folder, label, run, oracle);
        }
    }

}
