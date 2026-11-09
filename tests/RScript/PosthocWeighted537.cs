using System.Globalization;
using StatsDirect.Data;
using StatsDirect.R;
using StatsDirect.Templates;
using StatsDirect.Utilities;

internal static partial class Program
{
    private static void CheckPosthocWeighted537(string folder)
    {
        double[] y = { 8.73, 8.83, 14.58, 9.89, 9.16, 11.35, 5.24, 7.19, 13.24, 7.65, 8.36, 7.69, 12.42, 5.96, 12.51, 10.52, 5.53, 11.18, 14.38 };
        double[] w = { 1, 1, 4, 2, 4, 4, 3, 2, 3, 1, 3, 1, 4, 1, 2, 4, 2, 3, 4 };
        double[] x1 = { 7.55, 5.12, 7.15, 5.93, 3.56, 4.63, 2.06, 2.18, 9.42, 7.08, .55, 2.28, 2.73, .07, 7.53, 5.75, .42, 9.61, 8.74 };
        double[] x2 = { 1, 0, 1, 1, 0, 1, 0, 1, 1, 0, 1, 1, 1, 1, 0, 1, 1, 0, 0 };
        var predictors = new[] { ("X1", x1), ("X2", x2) };
        var twice = ("TwiceX1", x1.Select(x => 2 * x).ToArray());
        var thrice = ("ThreeX2", x2.Select(x => 3 * x).ToArray());
        CheckFit("scaled-duplicate", y, w, predictors, twice);
        CheckFit("two-duplicates", y, w, predictors, twice, thrice);

        double[] sbp = { 72.9, 68.4, 83, 63.2, 65.5, 71.9, 68.4, 72.2, 80, 69.9, 68, 72.4, 62.5, 76.4, 70, 63.6, 63.7, 79.4, 66.8, 64.8, 79.3, 67.7, 65, 80.5, 67.5, 76.5, 72.5, 67.7, 65.2, 62.5 };
        double[] prior = { 3, 2, 1, 2, 3, 1, 2, 3, 1, 1, 3, 1, 2, 3, 1, 2, 1, 1, 3, 1, 1, 1, 3, 3, 1, 1, 3, 1, 1, 2 };
        double[] age = { 63.6, 25.6, 55, 24.4, 27.8, 44.6, 55.1, 63.9, 60.3, 43, 21.5, 40.7, 30.7, 39.6, 47.5, 27.4, 28.7, 60.6, 30.3, 41.8, 58.5, 51.5, 28.7, 60.1, 40.2, 48.1, 34.3, 53, 59.3, 26.5 };
        double[] Dummy(int group) => Enumerable.Range(0, sbp.Length).Select(i => i % 3 == group ? 1d : 0d).ToArray();
        CheckFit("full-dummy-set", sbp, prior, new[] { ("Age", age), ("GroupA", Dummy(0)), ("GroupB", Dummy(1)) }, ("GroupC", Dummy(2)));

        void CheckFit(string label, double[] outcome, double[] weights, (string title, double[] values)[] retained, params (string title, double[] values)[] dropped)
        {
            var columns = new[] { ("Y", outcome), ("W", weights) }.Concat(retained).Concat(dropped).ToArray();
            var sheet = new RecordCells(columns.Select(c => new object[] { c.Item1 }.Concat(c.Item2.Cast<object>()).ToArray()).ToArray());
            DataFrame Select(int column) => sheet.Acquire(column, 0, outcome.Length + 1, DataAcquisitionMode.NumericPreserveRows);
            DataFrame x = new();
            for (int i = 2; i < columns.Length; i++)
                x.Variables.Add(Select(i).Variables[0]);
            var (run, model) = Run("MultipleLinearRegression", new()
            {
                ["outcome"] = Select(0), ["weights"] = Select(1), ["predictors"] = x,
                ["ci"] = .95, ["calculateIntercept"] = true, ["useWeights"] = true
            });
            ParameterBag bag = model.ParameterBag;
            var warnings = Rows(bag, "warn").Select(row => row["warn"].AsString).ToArray();
            Check(dropped.All(c => warnings.Any(warning => warning.Contains(c.title, StringComparison.Ordinal) &&
                warning.Contains("dropped from the model", StringComparison.Ordinal))), label + " drops the specified collinear predictors");

            ParameterBag detail = Run("MultipleLinearRegressionParameterDetail", new(), bag).output.ParameterBag;
            ParameterBag anova = Run("MultipleLinearRegressionAnova", new() { ["ci"] = .95 }, bag).output.ParameterBag;
            ParameterBag residuals = Run("MultipleLinearRegressionResiduals", new() { ["options"] = new Dictionary<string, bool>() }, bag).output.ParameterBag;
            var coefficients = Rows(detail, "v");
            Check(coefficients.Select(row => row["label"].AsString).SequenceEqual(new[] { "constant" }.Concat(retained.Select(c => c.title))),
                label + " retains the intended reduced model");
            var fitted = Rows(residuals, "yfit");
            Check(fitted.Count == outcome.Length, label + " reports every residual from the refit");

            // Construct the reduced model independently from the fixture, with the actual prior weights.
            string inputs = string.Join(", ", columns.Take(retained.Length + 2).Select(c => c.Item1 + " = " + Vector(c.Item2)));
            string oracle = "d <- data.frame(" + inputs + ")\n" +
                "m <- lm(Y ~ " + string.Join(" + ", retained.Select(c => c.title)) + ", data = d, weights = W)\n" +
                "s <- summary(m)\na <- anova(m)\ne <- residuals(m)\n" +
                "expect <- function(actual, expected) {\n" +
                "  comparison <- all.equal(as.double(actual), as.double(expected), tolerance = 1e-9)\n" +
                "  if (!isTRUE(comparison)) stop(deparse(substitute(actual)), ': ', comparison)\n" +
                "}\n" +
                Expect("coef(m)", coefficients.Select(row => Number(row, "coef"))) +
                Expect("s$coefficients[, 2]", coefficients.Select(row => Number(row, "err"))) +
                Expect("c(sum(head(a$'Sum Sq', -1)), sum(head(a$Df, -1)), tail(a$'Sum Sq', 1), m$df.residual, s$sigma, s$fstatistic[1], 100 * s$r.squared, 100 * s$adj.r.squared, sum(diff(e)^2) / sum(e^2))",
                    new[] { "reg_sum", "reg_df", "res_sum", "res_df", "mse", "f", "r2", "ra2", "dw" }.Select(key => Number(anova, key))) +
                Expect("fitted(m)", fitted.Select(row => Number(row, "fit_y"))) +
                Expect("e", fitted.Select(row => Number(row, "res"))) +
                "cat('WEIGHTED_REFIT_OK\\n')\n";
            RScript replay = RScriptWriter.Write(Record(run), null, null);
            Check(replay.HasRecipe && !replay.DataUnavailable, label + " generates a replay of the recorded weighted model");
            string output = RunR(replay.Text + "\n" + oracle +
                "expect(fit$rank, m$rank)\n" +
                "expect(fitted(fit), fitted(m))\n" +
                "expect(residuals(fit), residuals(m))\n", Path.Combine(folder, label));
            Check(output.Contains("WEIGHTED_REFIT_OK"),
                label + " coefficients, SE, ANOVA, Durbin-Watson and residuals agree with independent weighted lm");
            CheckAnovaRow("Regression", "reg_sum", "reg_df", "reg_mean");
            CheckAnovaRow("Residual", "res_sum", "res_df", "res_mean");
            CheckAnovaRow("Total (corrected)", "tot_sum", "tot_df");

            void CheckAnovaRow(string heading, params string[] keys)
            {
                string[] fields = output.Split('\n').Where(line => line.StartsWith(heading + " ", StringComparison.Ordinal))
                    .Select(line => line[heading.Length..].Split((char[])null, StringSplitOptions.RemoveEmptyEntries))
                    .SingleOrDefault(cells => cells.Length == keys.Length);
                Check(fields != null && fields.Zip(keys).All(pair =>
                    double.TryParse(pair.First, NumberStyles.Float, inv, out double value) &&
                    Math.Abs(value - Number(anova, pair.Second)) <= 6e-7),
                    label + " generated replay prints the native " + heading + " ANOVA row");
            }
        }

        static IList<ParameterBag> Rows(ParameterBag bag, string key) => (IList<ParameterBag>)bag["*" + key].AsObject;
        static double Number(ParameterBag bag, string key) => Convert.ToDouble(bag[key].AsObject, inv);
        static string Vector(IEnumerable<double> values) => RScriptWriter.Vector(values.Cast<object>().ToArray());
        static string Expect(string expression, IEnumerable<double> values) => "expect(" + expression + ", " + Vector(values) + ")\n";
    }
}
