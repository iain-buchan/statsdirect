using System.Text.RegularExpressions;
using StatsDirect.Charting;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.R;
using StatsDirect.Templates;
using StatsDirect.Utilities;

internal static partial class Program
{
    // The model items of 5.3.10. As for 5.3.9, each case is acquired from worksheet cells, run through the template processor, and
    // compared with its native output bag, with R's replay of the recorded run and with an independent calculation in R. Figures that
    // were not to move are compared exactly with those of 5.3.9.
    private static void CheckModels5310(string folder, string only = null)
    {
        RRecipes.Folder = Metadata("Recipes");
        Directory.CreateDirectory(folder);
        if (only is null or "aliased")
            CheckModels5310AliasedRatios(folder);
        if (only is null or "polynomial")
            CheckModels5310SaturatedPolynomial(folder);
        if (only is null or "hl")
            CheckModels5310HosmerLemeshowWholeTotals(folder);
    }

    private const string Models5310Dropped = "* error: drop this variable *";

    /// <summary>The named figures of each row of a table of ratios, leaving out the rows of one predictor.</summary>
    private static double[] Models5310Figures(IEnumerable<ParameterBag> rows, string except, params string[] keys) =>
        rows.Where(row => row["par"].AsString != except).SelectMany(row => keys.Select(key => Models539Number(row, key))).ToArray();

    /// <summary>Whether a row carries the marker of an aliased column in place of its ratio, with nothing for the figures that depend on it.</summary>
    private static bool Models5310Marked(ParameterBag row, string ratio, params string[] unavailable) =>
        row[ratio].AsObject is string marker && marker == Models5310Dropped && unavailable.All(key => row[key].AsObject is double value && value == Constant.MISSING);

    /// <summary>Figures that 5.3.10 was not to move: each is the same double as 5.3.9 gave.</summary>
    private static void Models5310Same(string label, IEnumerable<double> actual, params double[] expected) =>
        Check(actual.SequenceEqual(expected), label + ": every figure is the one 5.3.9 gave, to the last bit");

    // Item 1: a column aliased with the intercept has a coefficient of 0 with a standard error of 0. The main reports mark its row
    // "* error: drop this variable *"; the incidence rate ratios that follow a Poisson regression, and the bootstrap odds ratios that
    // follow a logistic regression, showed it as a ratio of 1 (1 to 1), and offered it as a sub-population for the ratios.
    private static void CheckModels5310AliasedRatios(string folder)
    {
        double[] veteran = { 0, 1, 0, 1, 1, 0, 0, 1, 1, 0, 1, 0 }, ones = Enumerable.Repeat(1d, 12).ToArray();
        string[] figures = { "est", "irr", "lci", "uci" };
        // 5.3.9: Veteran and Dose for the whole study, then Dose among the veterans and among the others
        double[] rateRatios =
        {
            0.18113279022396056, 1.1985743276796967, 0.8272897637772104, 1.7364900206352718, 0.03515023270198123, 1.0357753044341849, 0.9814154558874496, 1.093146103253098,
            0.03515023270198123, 1.2414536891394363, 0.8494326563463599, 1.8143960569011632, 0.03515023270198123, 1.0357753044341849, 0.9814154558874496, 1.093146103253098
        };
        var sheet = Models539Sheet(("Outcome", Models539Counts), ("Prior", ones), ("Exposure", Models539Exposures), ("Veteran", veteran), ("Dose", Models539Dose), ("Fixed", ones));
        ParameterBag Ratios(ParameterBag model) => Run("PoissonRegressionIRR", new() { ["gamma"] = .95, ["cov"] = new[] { true } }, model).output.ParameterBag;
        double[] Estimable(ParameterBag ratios) => Models539Block(ratios, "pop").SelectMany(table => Models5310Figures(Models539Block(table, "par"), "Fixed", figures)).ToArray();

        ParameterBag plain = Ratios(Run("PoissonRegression", Models539PoissonAnswers(sheet, new[] { 3, 4 }, true, true, false)).output.ParameterBag);
        Models5310Same("incidence rate ratios with no aliased column", Estimable(plain), rateRatios);

        var (run, output) = Run("PoissonRegression", Models539PoissonAnswers(sheet, new[] { 3, 4, 5 }, true, true, false));
        ParameterBag ratios = Ratios(output.ParameterBag);
        var tables = Models539Block(ratios, "pop");
        Models5310Same("the incidence rate ratios of the columns beside an aliased one", Estimable(ratios), rateRatios);
        Check(tables.Select(table => table["pop"].AsString).SequenceEqual(new[] { "whole study (baseline relative risk)", "Veteran = 1", "Veteran = 0" }) &&
            ((StringVariable)ratios["dichotomousCovariates"].AsDataFrame.Variables[0]).Data.SequenceEqual(new[] { "Veteran" }),
            "an aliased column is not offered as a sub-population for the incidence rate ratios");
        var aliased = tables.Select(table => Models539Block(table, "par").Where(row => row["par"].AsString == "Fixed").ToArray()).ToArray();
        Check(aliased.All(rows => rows.Length == 1 && Models539Number(rows[0], "est") == 0 && Models5310Marked(rows[0], "irr", "lci", "uci")),
            "an aliased column has the row '" + Models5310Dropped + "' in each table of incidence rate ratios, not a ratio of 1 (1 to 1)");
        // the fit without the column, in R: the ratios, their Wald limits, and among the veterans exp of the sum of two coefficients
        // with the standard error of that sum
        ParameterBag among = Models539Block(tables[1], "par").First(row => row["par"].AsString == "Dose");
        string oracle = "y <- " + Models539Vector(Models539Counts) + "\nt <- " + Models539Vector(Models539Exposures) + "\nVeteran <- " + Models539Vector(veteran) + "\nDose <- " + Models539Vector(Models539Dose) + "\n" +
            "g <- glm(y ~ Veteran + Dose, offset = log(t), family = poisson, control = glm.control(epsilon = 1e-14, maxit = 100))\n" +
            "b <- coef(g)[-1]; v <- vcov(g)[-1, -1]; q <- qnorm(0.975); half <- q * sqrt(diag(v)); both <- q * sqrt(v[1, 1] + v[2, 2] + 2 * v[1, 2])\n" +
            Models539Equal("rbind(b, exp(b), exp(b - half), exp(b + half))", Models5310Figures(Models539Block(tables[0], "par"), "Fixed", figures)) +
            Models539Equal("b[2], exp(sum(b)), exp(sum(b) - both), exp(sum(b) + both)", Models5310Figures(new[] { among }, "Fixed", figures)) +
            "stopifnot(sum(is.na(coef(fit))) == 1L)\n";
        string rendered = Models539Verify(folder, "poisson-aliased-ratios", run, oracle);
        Check(Regex.IsMatch(rendered, @"^Fixed \* error: drop this variable \*", RegexOptions.Multiline) && Regex.IsMatch(rendered, @"^Veteran = 1: Fixed \* error: drop this variable \*", RegexOptions.Multiline),
            "the replay marks the aliased column in the incidence rate ratios, for the whole study and among the veterans");
        Check(rendered.Contains("Veteran = 1: Dose " + Models539R(among, "est") + " " + Models539R(among, "irr") + " " + Models539R(among, "lci") + " to " + Models539R(among, "uci")),
            "the replay prints the native incidence rate ratio of the column beside it among the veterans");

        // the bootstrap that follows a logistic regression: its first row, labelled Constant, is the intercept
        double[] responders = { 2, 3, 5, 7, 9, 12, 14, 15, 17, 18, 19, 19 }, oddsRatios = { 0.06083232460536803, 1.6187020421104712, 0.9424383310481235 };
        var logistic = Models539Sheet(("Responders", responders), ("Total", Enumerable.Repeat(20d, 12).ToArray()), ("Prior", ones), ("Dose", Models539Dose), ("Veteran", veteran), ("Fixed", ones));
        IList<ParameterBag> Bootstrap(ParameterBag model) => Models539Block(Run("LogisticRegressionBootstrap", new() { ["boots"] = 50, ["gamma"] = .95 }, model).output.ParameterBag, "parameters");
        Models5310Same("bootstrap odds ratios with no aliased column",
            Models5310Figures(Bootstrap(Run("LogisticRegression", Models539LogisticAnswers(logistic, new[] { 3, 4 })).output.ParameterBag), "Fixed", "obs"), oddsRatios);
        var (logisticRun, logisticOutput) = Run("LogisticRegression", Models539LogisticAnswers(logistic, new[] { 3, 4, 5 }));
        var bootstrap = Bootstrap(logisticOutput.ParameterBag);
        Models5310Same("the bootstrap odds ratios of the columns beside an aliased one", Models5310Figures(bootstrap, "Fixed", "obs"), oddsRatios);
        Check(bootstrap.Select(row => row["par"].AsString).SequenceEqual(new[] { "Constant", "Dose", "Veteran", "Fixed" }) && Models5310Marked(bootstrap[3], "obs", "bias", "lci", "uci"),
            "an aliased column has the row '" + Models5310Dropped + "' in the bootstrap odds ratios, not a ratio of 1 (1 to 1)");
        // the re-samples are random: their bias and limits can only be asked to be numbers, with the limits either side of each other
        Check(bootstrap.Skip(1).Take(2).All(row => double.IsFinite(Models539Number(row, "bias")) && Models539Number(row, "lci") > 0 && Models539Number(row, "lci") <= Models539Number(row, "uci") && double.IsFinite(Models539Number(row, "uci"))),
            "the columns beside it keep a bootstrap bias and confidence limits");
        string logisticOracle = "r <- " + Models539Vector(responders) + "\nDose <- " + Models539Vector(Models539Dose) + "\nVeteran <- " + Models539Vector(veteran) + "\n" +
            "g <- glm(cbind(r, 20 - r) ~ Dose + Veteran, family = binomial, control = glm.control(epsilon = 1e-14, maxit = 100))\n" +
            Models539Equal("exp(coef(g)[-1])", Models5310Figures(bootstrap.Skip(1), "Fixed", "obs")) + "stopifnot(sum(is.na(coef(fit))) == 1L)\n";
        Models539Verify(folder, "logistic-aliased-bootstrap", logisticRun, logisticOracle);
    }

    private static (OperationRun run, ParameterBag model) Models5310Polynomial(Func<DataAcquisitionMode, int[], DataFrame> sheet, int degree)
    {
        var (run, output) = Run("PolynomialRegression", new() { ["x"] = Models539Select(sheet, 1), ["y"] = Models539Select(sheet, 0), ["degree"] = degree.ToString(inv) });
        return (run, output.ParameterBag);
    }

    // Item 2: a polynomial through as many points as it has coefficients. Its main report now keeps the exact fit, as multiple
    // regression has since 5.3.9 (it was made again by singular value decomposition, with "QR solution failed and SVD used", t = 0 and
    // P = *), and shows what needs a residual mean square as unavailable. The replay had stopped at the first P value.
    private static void CheckModels5310SaturatedPolynomial(string folder)
    {
        string[] analysis = { "reg_sum", "res_sum", "tot_sum", "res_df", "res_mean", "mse", "f", "p", "r2", "ra2", "dw" };
        ParameterBag ordinary = Models5310Polynomial(Models539Sheet(("Y", Models539Y), ("X", Models539X1)), 2).model, ordinaryAnova = Models539Anova(ordinary);
        Models5310Same("a quadratic through ten points",
            Models539Block(Models539Block(ordinary, "row")[0], "col").SelectMany(c => new[] { Models539Number(c, "val_b"), Models539Number(c, "t"), Models539Number(c, "p") }).Concat(analysis.Select(key => Models539Number(ordinaryAnova, key))),
            4.286028029048921, 1.0583864588869962, 0.32502912274495005, 1.2582356509199937, 0.7768380197956684, 0.46269269314272027, -0.044955312953218876, -0.3084262417564866, 0.766734068361262,
            31.58498150329033, 39.773258496709694, 71.35824000000002, 7, 5.681894070958528, 2.383672391701202, 2.779441248713916, 0.1292750308423055, 44.2625567885227, 28.337573013814897, 3.026239601659038);

        double[] y = { 3, 1, 7, 4 }, x = { 1, 2, 4, 5 };
        double[] gappyY = { 3, Constant.MISSING, 1, 7, Constant.MISSING }, gappyX = { 1, 3, 2, 4, 5 };
        foreach (var (label, sheet, degree, used) in new[]
        {
            ("three-points", Models539Sheet(("Y", y.Take(3).ToArray()), ("X", x.Take(3).ToArray())), 2, 3),
            ("missing-values", Models539Sheet(("Y", gappyY), ("X", gappyX)), 2, 3),
            ("cubic", Models539Sheet(("Y", y), ("X", x)), 3, 4)
        })
        {
            var (run, model) = Models5310Polynomial(sheet, degree);
            Check(Models539Warnings(model).Any(w => w.StartsWith("saturated model", StringComparison.Ordinal)) && !Models539Warnings(model).Any(w => w.Contains("SVD")),
                "polynomial " + label + ": the exact fit is kept, with a warning that it is saturated");
            var coefficients = Models539Block(Models539Block(model, "row")[0], "col");
            Check(coefficients.Count == degree + 1 && coefficients.All(c => double.IsNaN(Models539Number(c, "t")) && !double.IsFinite(Models539Number(c, "p"))) &&
                coefficients.Skip(1).All(c => double.IsNaN(Models539Number(Models539Block(c, "r")[0], "rp"))),
                "polynomial " + label + ": no t, P or partial correlation without residual df");
            ParameterBag anova = Models539Anova(model);
            Check(new[] { "res_mean", "mse", "f", "p", "ra2", "dw" }.All(key => Models539Number(anova, key) == Constant.MISSING) && Models539Number(anova, "res_df") == 0,
                "polynomial " + label + ": no residual mean square, root MSE, F, P, adjusted R squared or Durbin-Watson without residual df");
            Check(Math.Abs(Models539Number(anova, "res_sum")) <= 1e-9 * Models539Number(anova, "tot_sum") && Math.Abs(Models539Number(anova, "r2") - 100) <= 1e-7,
                "polynomial " + label + ": the exact fit has no residual sum of squares and R squared of 100%");
            string oracle = "Y <- " + Models539Vector(y.Take(used)) + "\nX <- " + Models539Vector(x.Take(used)) + "\nm <- lm(Y ~ poly(X, " + degree.ToString(inv) + ", raw = TRUE))\n" +
                Models539Equal("coef(m)", coefficients.Select(c => Models539Number(c, "val_b")), 1e-9) +
                Models539Equal("sum((fitted(m) - mean(Y))^2), m$df.residual", new[] { Models539Number(anova, "reg_sum"), 0 }, 1e-9);
            string output = Models539Verify(folder, "polynomial-saturated-" + label, run, oracle);
            Check(Regex.Matches(output, @"t = \*  P = \*").Count == degree + 1 && output.Contains("Root MSE = *") && Regex.IsMatch(output, @"F = \* +P = \*") && output.Contains("Ra2 = *%"),
                "polynomial " + label + ": the replay prints * for each statistic that needs residual df");
            if (label != "three-points")
                continue;
            // what follows the fit still takes it: 25/3 - 7x + 5x^2/3, which is 7/3 at 3 and has an area of 7.5 from 1 to 4
            Dictionary<string, object> Columns() => new() { ["x"] = Models539Select(sheet, 1), ["y"] = Models539Select(sheet, 0) };
            Dictionary<string, object> at = Columns();
            at["newx"] = 3d;
            at["gamma"] = .95;
            ParameterBag interpolated = Run("PolynomialRegressionInterpolation", at, model).output.ParameterBag, area = Run("PolynomialAreaUnderCurve", Columns(), model).output.ParameterBag;
            Check(Close(Models539Number(interpolated, "y_res"), 7.0 / 3, 1e-12) && new[] { "from_conf", "to_conf", "from_pred", "to_pred" }.All(key => !double.IsFinite(Models539Number(interpolated, key))),
                "polynomial " + label + ": interpolation gives the value on the curve, with no confidence or prediction limits");
            Check(Close(Models539Number(area, "poly_auc"), 7.5, 1e-12) && Close(Models539Number(area, "trap_auc"), 10, 1e-12),
                "polynomial " + label + ": the areas under the curve and under the points are those of the exact fit");
        }
    }

    // Item 3: the tolerance of the Hosmer-Lemeshow cuts, 1e-9 of the total since 5.3.9, passed 0.1 above a total of 1e8 and then moved
    // whole cumulative counts across a cut. Six doses whose totals are k, 2k+1, 2k+2, 2k+2, 2k+2 and k+2, a total of 10k+9: a tenth is
    // k + 0.9, each dose ends at or after the cut that falls due, and whole-number arithmetic puts each in a group of its own.
    private static void CheckModels5310HosmerLemeshowWholeTotals(string folder)
    {
        double[] rates = { 0.10, 0.22, 0.30, 0.52, 0.61, 0.80 }, dose = { 1, 2, 3, 4, 5, 6 }, ones = Enumerable.Repeat(1d, 6).ToArray();
        foreach (double n in new[] { 2009d, 200000009d, 600000009d, 1500000009d })
        {
            double k = Math.Floor(n / 10);
            double[] totals = { k, 2 * k + 1, 2 * k + 2, 2 * k + 2, 2 * k + 2, k + 2 }, responders = totals.Select((t, i) => Math.Round(t * rates[i])).ToArray();
            string label = "hl-total-" + n.ToString(inv);
            var sheet = Models539Sheet(("Responders", responders), ("Total", totals), ("Prior", ones), ("Dose", dose));
            var (run, output) = Run("LogisticRegression", Models539LogisticAnswers(sheet, new[] { 3 }));
            ParameterBag model = Run("LogisticRegressionModel", new(), output.ParameterBag).output.ParameterBag;
            double hl = Models539Number(model, "hl");
            if (n == 2009)
                Models5310Same("Hosmer-Lemeshow with a total of 2009", new[] { hl, Models539Number(model, "df_hl"), Models539Number(model, "p_hl") }, 7.432228201319823, 4, 0.11473514154120618);
            Check(totals.Sum() == n && Models539Number(model, "df_hl") == 4, label + ": the six doses make six groups and 4 df, as whole frequencies always did");
            // the cuts in whole-number arithmetic, exact in doubles: floor(n / 10), then floor of the larger of the cumulative total
            // plus a tenth and the next multiple of a tenth; six groups of one dose make the test Pearson's chi-square
            string oracle = "o <- rowsum(cbind(observed = responders * weight, expected = fitted(fit) * total * weight, total = total * weight), predictors[[1]])\n" +
                "n <- sum(o[, 'total']); cum <- 0; grp <- 0; cut <- n %/% 10; g <- integer(nrow(o))\n" +
                "for (i in seq_len(nrow(o))) {\n" +
                "  if (cum > 0 && cum >= cut && grp < 9) { grp <- grp + 1; cut <- max((10 * cum + n) %/% 10, ((grp + 1) * n) %/% 10) }\n" +
                "  g[i] <- grp; cum <- cum + o[i, 'total']\n}\n" +
                "o <- rowsum(o, g)\nstopifnot(n == " + n.ToString(inv) + ", nrow(o) == 6L)\n" +
                "oracle_hl <- sum((o[, 'observed'] - o[, 'expected'])^2 / (o[, 'expected'] * (1 - o[, 'expected'] / o[, 'total'])))\n" +
                Models539Equal("oracle_hl, sum(residuals(fit, type = 'pearson')^2), hl, hldf", new[] { hl, hl, hl, 4 });
            string rendered = Models539Verify(folder, label, run, oracle);
            Check(rendered.Contains("Hosmer-Lemeshow test = " + Models539R(model, "hl") + "  df = 4 "), label + ": the replay prints the native Hosmer-Lemeshow test");
        }
    }
}
