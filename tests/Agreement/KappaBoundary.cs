// Agreement of categories at its limits: raters who agree on every subject, raters who all but do, and raters who use one category only.
// With perfect agreement every measure is 1 and the variances behind the confidence intervals are nothing, so that the standard errors are 0
// and the limits 1; the standard errors for the tests, which suppose the raters independent, are as for any other table.  With one category
// only the agreement expected is 1 and kappa, 0 over 0, has no value.
using StatsDirect.Builtins;
using StatsDirect.Data;
using StatsDirect.Templates;

internal static partial class Program
{
    private static bool Number(ParameterBag report, string name) => report[name].AsObject is double v && double.IsFinite(v) && v != M && v != -M;

    private static void PerfectAgreement(string title, ParameterBag report, double[,] o, double[,] w)
    {
        int before = failures;
        double n = 0;
        foreach (double count in o) n += count;
        int k = o.GetLength(0);
        double[,] p = Proportions(o);
        double[] r = Marginal(p, true), c = Marginal(p, false);
        double[,] independent = new double[k, k];
        for (int i = 0; i < k; i++) for (int j = 0; j < k; j++) independent[i, j] = r[i] * c[j];
        foreach (string name in new[] { "kappa", "kappaw", "spi", "gama" })
            Say(Number(report, name) && Math.Abs(report[name].AsDouble - 1) <= 1e-12, title + ": " + name + " is 1");
        foreach (string name in new[] { "seci", "sekwci", "segama" })
            Say(Number(report, name) && report[name].AsDouble >= 0 && report[name].AsDouble <= 1e-7, title + ": " + name + " is 0" + (Number(report, name) ? "" : " (it is not a number)"));
        foreach (string name in new[] { "from", "to", "fromw", "tow", "gamacil", "gamaciu" })
            Say(Number(report, name) && Math.Abs(report[name].AsDouble - 1) <= 1e-7, title + ": " + name + " is 1" + (Number(report, name) ? "" : " (it is not a number)"));
        double[,] identity = Identity(k);
        double se0 = DeltaMethod(q => WeightedKappa(q, identity), independent, n), sew0 = DeltaMethod(q => WeightedKappa(q, w), independent, n);
        Check(title + ": standard error of kappa for the test", Number(report, "se") ? Relative(report["se"].AsDouble, se0) : double.PositiveInfinity, 1e-6);
        Check(title + ": standard error of weighted kappa for the test", Number(report, "sekw") ? Relative(report["sekw"].AsDouble, sew0) : double.PositiveInfinity, 1e-6);
        Check(title + ": z and P of kappa", Number(report, "z") && Number(report, "p") ? Math.Abs(report["z"].AsDouble - 1 / se0) / (1 / se0) + Math.Abs(report["p"].AsDouble - NormalUpper(1 / se0)) : double.PositiveInfinity, 1e-5);
        Check(title + ": z and P of weighted kappa", Number(report, "zw") && Number(report, "pw") ? Math.Abs(report["zw"].AsDouble - 1 / sew0) / (1 / sew0) + Math.Abs(report["pw"].AsDouble - NormalUpper(1 / sew0)) : double.PositiveInfinity, 1e-5);
        if (failures == before) Console.WriteLine("ok    " + title);
    }

    private static void KappaBoundary()
    {
        Console.WriteLine();
        Console.WriteLine("Agreement of categories at its limits");
        System.Random random = new(59);
        List<double[]> diagonals = new()
        {
            new double[] { 19, 34 }, new double[] { 12, 9, 14 }, new double[] { 7, 11, 3, 29 }, new double[] { 1, 1 }, new double[] { 5, 5, 5, 5, 5 },
            new double[] { 3, 1000 }, new double[] { 1, 2, 3, 4, 5, 6 }
        };
        for (int set = 0; set < 40; set++) diagonals.Add(Enumerable.Range(0, 2 + set % 5).Select(i => (double)(1 + random.Next(set % 3 == 0 ? 500 : 40))).ToArray());
        int number = 0;
        foreach (double[] diagonal in diagonals)
        {
            number++;
            int k = diagonal.Length, method = 1 + number % 3;
            double[,] o = new double[k, k];
            for (int i = 0; i < k; i++) o[i, i] = diagonal[i];
            double[,] w = method <= 2 ? Weights(k, method) : new double[k, k];
            if (method == 3)
                for (int i = 0; i < k; i++)
                    for (int j = 0; j < k; j++) w[i, j] = i == j ? 1 : Math.Round(0.7 / (1 + Math.Abs(i - j)), 2);
            string title = $"perfect agreement, {string.Join(", ", diagonal)} in {k} categories, " + new[] { "", "linear weights", "quadratic weights", "weights given" }[method];
            try { PerfectAgreement(title, Screen(o, method, w, 0.95), o, w); }
            catch (Exception ex) { checks++; failures++; Console.WriteLine("FAIL  " + title + ": " + Message(ex)); }
        }

        // from columns of ratings
        {
            string title = "perfect agreement from columns of ratings, 19 and 34 in two categories";
            try
            {
                string[] ratings = Enumerable.Repeat("yes", 19).Concat(Enumerable.Repeat("no", 34)).ToArray();
                ParameterBag bag = new();
                DataFrame frame = new(Classifier("First", ratings));
                frame.Variables.Add(Classifier("Second", ratings));
                bag.AddInput("responses", frame);
                bag.AddInput("ci", 0.95);
                bag.AddInput("method", "1");
                // in order of label: no, yes
                PerfectAgreement(title, Tables.RptKappa(new Plain(), bag).ParameterBag, new double[,] { { 34, 0 }, { 0, 19 } }, Weights(2, 1));
            }
            catch (Exception ex) { checks++; failures++; Console.WriteLine("FAIL  " + title + ": " + Message(ex)); }
        }

        // all but perfect agreement: one subject of many off the diagonal, where the variance is small beside the terms that make it up
        foreach (int n in new[] { 200, 5000, 100000 })
        {
            double[,] o = { { Math.Floor(n * 0.3), 1, 0 }, { 0, Math.Floor(n * 0.5), 0 }, { 0, 0, n - Math.Floor(n * 0.3) - Math.Floor(n * 0.5) - 1 } };
            string title = $"one subject of {n} off the diagonal";
            int before = failures;
            try { TwoRaters(title, Screen(o, 1, Weights(3, 1), 0.95), o, Weights(3, 1), 0.95); }
            catch (Exception ex) { checks++; failures++; Console.WriteLine("FAIL  " + title + ": " + Message(ex)); }
            if (failures == before) Console.WriteLine("ok    " + title);
        }

        // one category only: the agreement expected is 1, and kappa has no value.  What is given must be shown as having none
        {
            string title = "every subject put in the same category by both raters";
            try
            {
                ParameterBag report = Screen(new double[,] { { 53, 0 }, { 0, 0 } }, 1, Weights(2, 1), 0.95);
                foreach (string name in new[] { "kappa", "se", "seci", "from", "to", "kappaw", "sekw", "sekwci", "fromw", "tow", "p", "pw" })
                    Say(!Number(report, name), title + ": " + name + " has no value", false);
                Check(title + ": agreement observed and expected are both 100%", Math.Abs(report["po"].AsDouble - 100) + Math.Abs(report["pe"].AsDouble - 100), 1e-9, true);
            }
            catch (Exception ex) { checks++; failures++; Console.WriteLine("FAIL  " + title + ": " + Message(ex)); }
        }
    }
}
