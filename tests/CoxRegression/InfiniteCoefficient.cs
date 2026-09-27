// Cox regression: a coefficient that may be infinite, and the accuracy of the coefficients at the default precision.
using StatsDirect.Builtins;
using StatsDirect.Templates;

internal static partial class Program
{
    private const string MayBeInfinite = "may be infinite";

    // the precision that the dialog box offers: the default of "accuracy" in the definition of the operation, read as the program reads it
    private static double DialogPrecision([System.Runtime.CompilerServices.CallerFilePath] string here = "")
    {
        string definition = File.ReadAllText(Path.Combine(Path.GetDirectoryName(here), "..", "..", "StatsDirectUI", "Assets", "Operations", "CoxRegression.xml"));
        var found = System.Text.RegularExpressions.Regex.Match(definition, @"<name>accuracy</name>\s*<prompt>[^<]*</prompt>\s*<default-value>([^<]+)</default-value>");
        return double.Parse(found.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
    }

    // When everybody with a predictor of 1 has the event before anybody else's time, the likelihood rises for as long as the coefficient of that
    // predictor grows, towards the likelihood of the model in which the predictor makes strata instead: those with 1 have left before the others
    // have their events, and beside them the others count for nothing.  The fit should end, with a warning that names the predictor and no
    // other, and the other coefficients and the log likelihood should be those of that model.
    private static void InfiniteCoefficient()
    {
        Console.WriteLine("A coefficient that may be infinite");
        System.Random random = new(5);
        double precision = DialogPrecision();
        int sets = 0, warned = 0;
        double worstCoefficient = 0, worstLikelihood = 0, leastSeparating = double.MaxValue;
        List<string> problems = new();
        for (int set = 1; set <= 60; set++)
        {
            int n = 24 + random.Next(60), early = n / 3, strata = set % 3 == 0 ? 2 : 1;
            bool frequencies = set % 4 == 0;
            List<Subject> data = new();
            for (int i = 0; i < n; i++)
            {
                bool first = i < early;
                double w = Math.Round(random.NextDouble() * 4 - 2, 2), v = random.NextDouble() < 0.5 ? 0 : 1;
                double time = first ? 1 + random.Next(10) : 20 + random.Next(40);
                double code = first ? (frequencies ? 1 + i % 3 : 1) : (random.NextDouble() < 0.7 ? 1 : 0);
                data.Add(new Subject(time, code, new[] { w, first ? 1.0 : 0.0, v }, strata > 1 ? i % strata + 1 : 1));
            }
            sets++;
            try
            {
                ParameterBag bag = Regression(data, 3, false, precision);
                StepOutput fit = Coxreg.RptCoxRegression(bag);
                List<string> warnings = Rows(fit, "*warn").Select(row => row["warn"].AsString).ToList();
                if (warnings.Count == 1 && warnings[0].Contains(MayBeInfinite) && warnings[0].StartsWith("the coefficient of Z2 may")) warned++;
                else problems.Add($"set {set}: " + (warnings.Count == 0 ? "no warning" : string.Join(" | ", warnings.Select(t => t.Substring(0, Math.Min(70, t.Length))))));
                // the model in which the predictor makes strata: the other two predictors, within the strata and the two values of the predictor
                List<Subject> within = data.Select(s => new Subject(s.Time, s.Code, new[] { s.Z[0], s.Z[2] }, 10 * s.Stratum + s.Z[1])).ToList();
                var limit = Maximise(within, 2);
                double[,,] arr3 = (double[,,])fit.ParameterBag["ARR3"].AsObject;
                double[,] arr2 = (double[,])fit.ParameterBag["ARR2"].AsObject;
                worstCoefficient = Math.Max(worstCoefficient, Math.Max(Math.Abs(arr3[1, 1, 1] - limit.beta[0]), Math.Abs(arr3[1, 3, 1] - limit.beta[1])));
                worstLikelihood = Math.Max(worstLikelihood, Math.Abs(arr2[2, 0] - limit.ll) / Math.Abs(limit.ll));
                leastSeparating = Math.Min(leastSeparating, arr3[1, 2, 1]);
            }
            catch (Exception ex)
            {
                problems.Add($"set {set}: " + Message(ex));
            }
        }
        Say(warned == sets, $"{sets} sets in which a predictor separates: {warned} fitted, with a warning that names it and no other" + (problems.Count > 0 ? "; " + string.Join("; ", problems.Take(4)) : ""), true);
        Check("the other coefficients are those of the model in which the predictor makes strata", worstCoefficient, 1e-5, true);
        Check("the log likelihood is that of the same model", worstLikelihood, 1e-5, true);
        Say(leastSeparating > 10, $"the coefficient of the predictor that separates is large: {leastSeparating:F2} at the least", true);

        // A predictor that puts every event in order (the later the time the less the predictor, with no two times the same) predicts the events
        // perfectly: the likelihood rises towards 1 and its logarithm towards 0, and the change in the log likelihood never becomes small beside
        // the log likelihood itself.  The fit cannot converge, and should say so.
        int perfect = 0, said = 0;
        for (int set = 1; set <= 20; set++)
        {
            int n = 24 + random.Next(40);
            int[] order = Enumerable.Range(0, n).OrderBy(i => random.NextDouble()).ToArray();
            Subject[] data = new Subject[n];
            for (int r = 0; r < n; r++)
                data[order[r]] = new Subject(r + 1, random.NextDouble() < 0.7 ? 1 : 0, new[] { Math.Round(300.0 - 2.5 * r - random.NextDouble(), 2), Math.Round(random.NextDouble() * 4 - 2, 2) }, 1);
            perfect++;
            try { Coxreg.RptCoxRegression(Regression(data.ToList(), 2, true, precision)); }
            catch (Exception ex) { if (Message(ex).Contains("Calculation failed to converge")) said++; }
        }
        Say(said == perfect, $"{perfect} sets in which a predictor puts every event in order: {said} said not to have converged", true);
    }

    // The precision that the dialog box offers is 0.000000001, which is the default of R.  The iterations end when the log likelihood changes by
    // no more than that much of itself, which leaves the coefficients a little short of the values that make the likelihood greatest.
    private static void DefaultPrecision()
    {
        Console.WriteLine("The coefficients at the default precision");
        double precision = DialogPrecision();
        Say(precision == 0.000000001, $"the precision that the dialog box offers is 0.000000001 ({precision:R} in the definition of the operation)", true);
        System.Random random = new(101);
        double worst = 0;
        int sets = 0, warned = 0;
        for (int set = 1; set <= 150; set++)
        {
            int n = 20 + random.Next(120), p = 1 + random.Next(4), strata = 1 + random.Next(3);
            bool ties = random.Next(3) != 0, frequencies = random.Next(3) == 0;
            // some of the predictors have nothing to do with the outcome, so that their coefficients are near 0
            double[] truth = Enumerable.Range(0, p).Select(k => random.Next(3) == 0 ? 0.0 : (random.NextDouble() * 2 - 1) * (random.Next(4) == 0 ? 2.5 : 0.8)).ToArray();
            List<Subject> data = new();
            for (int i = 0; i < n; i++)
            {
                double[] z = Enumerable.Range(0, p).Select(k => k % 2 == 0 ? Math.Round(random.NextDouble() * 4 - 2, 2) : (random.NextDouble() < 0.5 ? 0.0 : 1.0)).ToArray();
                double hazard = Math.Exp(Enumerable.Range(0, p).Sum(k => truth[k] * z[k]));
                double time = -Math.Log(1 - random.NextDouble()) / hazard * 10;
                time = ties ? Math.Ceiling(time) : Math.Round(time, 4) + (i + 1) * 1e-6;
                bool isEvent = random.NextDouble() < 0.75;
                data.Add(new Subject(time, isEvent ? (frequencies ? 1 + i % 3 : 1) : 0, z, strata > 1 ? i % strata + 1 : 1));
            }
            try
            {
                StepOutput fit = Coxreg.RptCoxRegression(Regression(data, p, false, precision));
                double[,,] arr3 = (double[,,])fit.ParameterBag["ARR3"].AsObject;
                var reference = Maximise(data, p);
                if (reference.beta.Any(v => double.IsNaN(v) || Math.Abs(v) > 50)) continue;
                worst = Math.Max(worst, Enumerable.Range(0, p).Max(k => Math.Abs(arr3[1, k + 1, 1] - reference.beta[k])));
                if (Rows(fit, "*warn").Any(row => row["warn"].AsString.Contains(MayBeInfinite))) warned++;
                sets++;
            }
            catch (Exception ex)
            {
                Say(false, $"set {set}: " + Message(ex), true);
            }
        }
        Check($"{sets} sets: the coefficients, against those that make the likelihood greatest", worst, 5e-8, true);
        Say(warned == 0, $"no warning of an infinite coefficient in any of them ({warned} given)", true);
    }
}
