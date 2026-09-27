// Cox regression: coefficients that may be infinite because several predictors separate the subjects between them.
using StatsDirect.Builtins;
using StatsDirect.Templates;

internal static partial class Program
{
    // The subjects who have the event early are separated from the rest not by one predictor but by the sum of several: the likelihood rises
    // for as long as the coefficients of those predictors grow together.  The movement of the fit from one iteration to the next is then
    // shared among the coefficients, and the fit should end with a warning that names every one of them, and no predictor that has no part.
    private static void JointSeparation()
    {
        Console.WriteLine("Coefficients that may be infinite, of predictors that separate the subjects between them");
        double dialog = DialogPrecision();

        // p predictors and 2p records: p events at time 1, in which the predictors add up to 0.2 p, and p records censored at time 2, in
        // which they add up to -0.2 p.  In each record one predictor, a different one each time, is 0.02 above the others
        foreach (int p in new[] { 2, 3, 5, 8, 12, 20 })
            foreach (double precision in new[] { 1e-5, 1e-7, dialog })
            {
                List<Subject> data = new();
                for (int j = 0; j < p; j++)
                    data.Add(new Subject(1, 1, Enumerable.Range(0, p).Select(i => 0.2 - 0.02 / p + (i == j ? 0.02 : 0)).ToArray(), 1));
                for (int j = 0; j < p; j++)
                    data.Add(new Subject(2, 0, Enumerable.Range(0, p).Select(i => -0.2 - 0.02 / p + (i == j ? 0.02 : 0)).ToArray(), 1));
                string title = $"{p} predictors that separate {2 * p} records between them, precision {precision:G2}";
                try
                {
                    StepOutput fit = Coxreg.RptCoxRegression(Regression(data, p, false, precision));
                    List<string> warnings = Rows(fit, "*warn").Select(row => row["warn"].AsString).Where(t => t.Contains(MayBeInfinite)).ToList();
                    string expected = "the coefficient of " + string.Join(", ", Enumerable.Range(1, p).Select(i => "Z" + i)) + " may";
                    Say(warnings.Count == 1 && warnings[0].StartsWith(expected), title + ": a warning that names them all" + (warnings.Count == 0 ? " (no warning)" : warnings[0].StartsWith(expected) ? "" : " (" + warnings[0].Substring(0, Math.Min(80, warnings[0].Length)) + ")"), true);
                }
                catch (Exception ex) { Say(false, title + ": " + Message(ex), true); }
            }

        // Three predictors whose sum separates, though no one of them does, with two more that have nothing to do with it.  The sum is 0.6 in
        // every subject with an early event and -0.6 in every other: were it to vary among those with early events, the order of their events
        // would hold the coefficients back, and the likelihood would have a greatest value
        System.Random random = new(19);
        int sets = 0, right = 0;
        List<string> problems = new();
        foreach (double precision in new[] { 1e-3, 1e-5, dialog })
            for (int set = 1; set <= 40; set++)
            {
                int n = 30 + random.Next(50), early = n / 3;
                List<Subject> data = new();
                bool overlap = true;
                for (int i = 0; i < n; i++)
                {
                    bool first = i < early;
                    double z1 = Math.Round(random.NextDouble() * 2 - 1, 2), z2 = Math.Round(random.NextDouble() * 2 - 1, 2);
                    double sum = first ? 0.6 : -0.6;
                    double w = Math.Round(random.NextDouble() * 4 - 2, 2), v = random.NextDouble() < 0.5 ? 0 : 1;
                    double time = first ? 1 + random.Next(10) : 20 + random.Next(40);
                    double code = first ? 1 : (random.NextDouble() < 0.7 ? 1 : 0);
                    data.Add(new Subject(time, code, new[] { z1, w, z2, v, Math.Round(sum - z1 - z2, 2) }, 1));
                }
                // no one of the three separates by itself: its values among the early events and among the rest overlap
                foreach (int k in new[] { 0, 2, 4 })
                    if (data.Take(early).Min(s => s.Z[k]) > data.Skip(early).Max(s => s.Z[k]) || data.Take(early).Max(s => s.Z[k]) < data.Skip(early).Min(s => s.Z[k])) overlap = false;
                if (!overlap) continue;
                sets++;
                try
                {
                    StepOutput fit = Coxreg.RptCoxRegression(Regression(data, 5, false, precision));
                    List<string> warnings = Rows(fit, "*warn").Select(row => row["warn"].AsString).Where(t => t.Contains(MayBeInfinite)).ToList();
                    if (warnings.Count == 1 && warnings[0].StartsWith("the coefficient of Z1, Z3, Z5 may")) right++;
                    else problems.Add($"set {set} at {precision:G2}: " + (warnings.Count == 0 ? "no warning" : warnings[0].Substring(0, Math.Min(60, warnings[0].Length))));
                }
                catch (Exception ex) { problems.Add($"set {set} at {precision:G2}: " + Message(ex)); }
            }
        Say(right == sets, $"{sets} fits in which the sum of three predictors separates and two predictors have no part: {right} with a warning that names the three and no other" + (problems.Count > 0 ? "; " + string.Join("; ", problems.Take(5)) : ""), true);
    }
}
