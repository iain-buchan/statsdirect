// Cox regression: predictors that carry nothing of their own, the iteration for the baseline survival at a time with tied events, the limit on the
// iterations of the fit, the figures of a record that the fit leaves out, and what is said when the fit does not converge.
using System.Reflection;
using StatsDirect.Builtins;
using StatsDirect.Data;
using StatsDirect.Templates;

internal static partial class Program
{
    private static void OtherChecks()
    {
        RedundantPredictors();
        TiedEvents();
        IterationLimit();
        RecordsLeftOutByTheFit();
        NotConverged();
        InfiniteCoefficient();
        JointSeparation();
        DefaultPrecision();
    }

    private static ParameterBag Regression(double[] time, double[] code, double[][] columns, double accuracy)
    {
        ParameterBag bag = new();
        bag.AddInput("times", new DataFrame(new DoubleVariable((double[])time.Clone(), "Time")));
        bag.AddInput("events", new DataFrame(new DoubleVariable((double[])code.Clone(), "Event")));
        DataFrame predictors = new(new DoubleVariable((double[])columns[0].Clone(), "Z1"));
        for (int k = 1; k < columns.Length; k++) predictors.Variables.Add(new DoubleVariable((double[])columns[k].Clone(), "Z" + (k + 1)));
        bag.AddInput("predictors", predictors);
        bag.AddInput("accuracy", accuracy);
        bag.AddInput("centre-continuous-covariates", true);
        return bag;
    }

    // A predictor that does not vary, or that is determined by others, should be dropped wherever it stands, with a warning that names it.  The
    // fit of the other predictors should be the fit that is made without it, and the degrees of freedom should not count it.
    private static void RedundantPredictors()
    {
        Console.WriteLine("Predictors that carry nothing of their own");
        System.Random random = new(9);
        const int n = 60;
        double[] time = new double[n], code = new double[n], z1 = new double[n], z2 = new double[n];
        for (int i = 0; i < n; i++)
        {
            z1[i] = Math.Round(random.NextDouble() * 4 - 2, 2);
            z2[i] = random.NextDouble() < 0.5 ? 0 : 1;
            time[i] = Math.Ceiling(-Math.Log(1 - random.NextDouble()) / Math.Exp(0.6 * z1[i] - 0.8 * z2[i]) * 10);
            code[i] = random.NextDouble() < 0.75 ? 1 : 0;
        }
        double[] constant = Enumerable.Repeat(3.0, n).ToArray();
        double[] sum = z1.Zip(z2, (a, b) => a + b).ToArray();
        double[] copy = (double[])z1.Clone();
        double[] square = z1.Select(v => v * v).ToArray();

        void Case(string title, double[][] columns, int redundant)
        {
            try
            {
                double[][] others = columns.Where((c, k) => k != redundant).ToArray();
                StepOutput without = Coxreg.RptCoxRegression(Regression(time, code, others, 1e-10));
                ParameterBag bag = Regression(time, code, columns, 1e-10);
                StepOutput with = Coxreg.RptCoxRegression(bag);
                double[,,] a = (double[,,])with.ParameterBag["ARR3"].AsObject, b = (double[,,])without.ParameterBag["ARR3"].AsObject;
                double worst = 0;
                for (int k = 0, m = 0; k < columns.Length; k++)
                {
                    if (k == redundant)
                    {
                        worst = Math.Max(worst, Math.Abs(a[1, k + 1, 1]) + Math.Abs(a[1, k + 1, 2]));
                        continue;
                    }
                    m++;
                    worst = Math.Max(worst, Math.Abs(a[1, k + 1, 1] - b[1, m, 1]) + Math.Abs(a[1, k + 1, 2] - b[1, m, 2]));
                }
                Check(title + ": the coefficients and standard errors are those of the fit without it", worst, 1e-7, true);
                List<ParameterBag> rows = Rows(with, "*pred");
                Say(rows[redundant]["b"].AsDouble == M && rows[redundant]["z"].AsDouble == M && rows[redundant]["p"].AsDouble == M, title + ": no coefficient, z or P is shown for it", true);
                Say(with.ParameterBag["df"].AsDouble == columns.Length - 1 && Math.Abs(with.ParameterBag["p_dev"].AsDouble - without.ParameterBag["p_dev"].AsDouble) < 1e-12, title + ": the degrees of freedom and P of the chi-square do not count it", true);
                List<ParameterBag> warnings = Rows(with, "*warn");
                Say(warnings.Count == 1 && warnings[0]["warn"].AsString.StartsWith("Z" + (redundant + 1) + " dropped from the model"), title + ": the warning names it", true);
                List<ParameterBag> hazards = Rows(Coxreg.RptCoxHazardRatios(PassedOn(bag, with, "gamma", 0.95)), "*hazard");
                Say(hazards[redundant]["ec"].AsDouble == M && hazards[redundant]["ell"].AsDouble == M && hazards[redundant]["eul"].AsDouble == M, title + ": no hazard ratio is shown for it", true);
                Say(Coxreg.RptCoxModelAnalysis(PassedOn(bag, with)).ParameterBag["df"].AsDouble == columns.Length - 1, title + ": the model analysis does not count it", true);
            }
            catch (Exception ex)
            {
                Say(false, title + ": " + Message(ex), true);
            }
        }
        Case("a constant, first of three", new[] { constant, z1, z2 }, 0);
        Case("a constant, second of three", new[] { z1, constant, z2 }, 1);
        Case("a constant, last of three", new[] { z1, z2, constant }, 2);
        Case("the sum of the two before it", new[] { z1, z2, sum }, 2);
        Case("a copy of the first, second of three", new[] { z1, copy, z2 }, 1);
        Case("a copy of the first, second of four", new[] { z1, copy, z2, square }, 1);
        Case("the sum of the two before it, third of four", new[] { z1, z2, sum, square }, 2);
        Case("a constant, second of four", new[] { z1, constant, z2, square }, 1);
    }

    // The iteration for the factor of the baseline survival at a time with tied events, against bisection, on many sets of hazard ratios, from
    // times at which hardly anybody is left at risk beside those with the event to times at which many are.
    private static void TiedEvents()
    {
        Console.WriteLine("The baseline survival at a time with tied events");
        MethodInfo solve = typeof(Coxreg).GetMethod("AlphaSolve", BindingFlags.NonPublic | BindingFlags.Static);
        System.Random random = new(3);
        int trials = 200000, failed = 0;
        double worst = 0;
        for (int trial = 0; trial < trials; trial++)
        {
            int d = 2 + random.Next(6);
            double spread = new[] { 0.3, 1.0, 2.5, 5.0 }[random.Next(4)];
            List<double> theta = new(), frequency = new();
            double sum = 0, dead = 0;
            for (int i = 0; i < d; i++)
            {
                theta.Add(Math.Exp(spread * (random.NextDouble() * 2 - 1)));
                frequency.Add(random.Next(3) == 0 ? 1 + random.Next(4) : 1);
                sum += frequency[i] * theta[i];
                dead += frequency[i];
            }
            double others = Math.Exp(new[] { -6.0, -3.0, 0.0, 3.0, 6.0 }[random.Next(5)] * random.NextDouble()) * Math.Exp(spread * (random.NextDouble() * 2 - 1));
            double risk = sum + others;
            double low = 0, high = 1;
            for (int it = 0; it < 300; it++)
            {
                double mid = (low + high) / 2, g = 0;
                for (int i = 0; i < d; i++) g += frequency[i] * theta[i] / (1 - Math.Pow(mid, theta[i]));
                if (g < risk) low = mid; else high = mid;
            }
            double got = (double)solve.Invoke(null, new object[] { theta, frequency, dead, risk });
            if (got == M || double.IsNaN(got)) failed++;
            else worst = Math.Max(worst, Math.Abs(got - (low + high) / 2));
        }
        Say(failed == 0, $"{trials} sets of tied events: {failed} without an answer", true);
        Check("the answers, against bisection", worst, 1e-9, true);
    }

    // the fit itself, called as the report calls it; the data a column at a time: the times, the censoring codes (0 for an event), the
    // frequencies and the predictors
    private static (int fault, double[,] caze, double[] b) Fit(double[] time, double[] censor, double[] frequency, double[][] z, int maxit, double accuracy)
    {
        int n = time.Length, p = z.Length, columns = 3 + p;
        double[] x = new double[n * columns + 1];
        for (int i = 0; i < n; i++)
        {
            x[1 + i] = time[i];
            x[n + 1 + i] = censor[i];
            x[2 * n + 1 + i] = frequency[i];
            for (int k = 0; k < p; k++) x[(3 + k) * n + 1 + i] = z[k][i];
        }
        int[] nvef = Enumerable.Repeat(1, p + 1).ToArray();
        int[] indef = new int[p + 1];
        for (int k = 1; k <= p; k++) indef[k] = 3 + k;
        object[] a =
        {
            n, columns, x, n, 1, 3, 0, 2, 0, maxit, accuracy, p, nvef, indef, 0, 0, new double[p + 1, 5], p, 0.0, new double[p + 1, p + 1], p,
            new double[p + 1], new double[n + 1, 7], n, new double[p + 1], new int[n + 1], 0, 0
        };
        typeof(Coxreg).GetMethod("coxreg", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, a);
        double[,] coef = (double[,])a[16];
        return ((int)a[27], (double[,])a[22], Enumerable.Range(1, p).Select(i => coef[i, 1]).ToArray());
    }

    private static void Data(System.Random random, int n, double slope, out double[] time, out double[] censor, out double[] z1, out double[] z2)
    {
        time = new double[n]; censor = new double[n]; z1 = new double[n]; z2 = new double[n];
        for (int i = 0; i < n; i++)
        {
            z1[i] = Math.Round(random.NextDouble() * 4 - 2, 2);
            z2[i] = random.NextDouble() < 0.5 ? 0 : 1;
            time[i] = Math.Ceiling(-Math.Log(1 - random.NextDouble()) / Math.Exp(slope * z1[i] - 0.8 * z2[i]) * 10);
            censor[i] = random.NextDouble() < 0.75 ? 0 : 1;
        }
    }

    // A fit needs a certain number of iterations, N: with any limit of N or more the loop stops at the Nth with the same coefficients.  A limit
    // of exactly N is therefore enough, and a limit of N - 1 is not.
    private static void IterationLimit()
    {
        Console.WriteLine("The limit on the iterations of the fit");
        System.Random random = new(21);
        for (int set = 1; set <= 6; set++)
        {
            Data(random, 40 + 10 * set, 0.3 * set, out double[] time, out double[] censor, out double[] z1, out double[] z2);
            double[] ones = Enumerable.Repeat(1.0, time.Length).ToArray();
            var converged = Fit(time, censor, ones, new[] { z1, z2 }, 30, 1e-9);
            int needed = 0;
            for (int maxit = 1; maxit <= 12 && needed == 0; maxit++)
                if (Fit(time, censor, ones, new[] { z1, z2 }, maxit, 1e-9).b.SequenceEqual(converged.b)) needed = maxit;
            Say(converged.fault == 0 && needed > 1 && Fit(time, censor, ones, new[] { z1, z2 }, needed, 1e-9).fault == 0 && Fit(time, censor, ones, new[] { z1, z2 }, needed - 1, 1e-9).fault == 5,
                $"set {set}: the fit needs {needed} iterations; a limit of {needed} is enough, and a limit of {needed - 1} gives fault 5", true);
        }
    }

    // A record that the fit itself leaves out (here one with a frequency of 0) has no figures, and the figures of the other records are those
    // of the fit without it.
    private static void RecordsLeftOutByTheFit()
    {
        Console.WriteLine("A record that the fit leaves out");
        System.Random random = new(8);
        Data(random, 40, 0.6, out double[] time, out double[] censor, out double[] z1, out double[] z2);
        double[] frequency = Enumerable.Repeat(1.0, 40).ToArray();
        int[] leftOut = { 7, 20 };
        foreach (int r in leftOut) frequency[r] = 0;
        var with = Fit(time, censor, frequency, new[] { z1, z2 }, 30, 1e-10);
        int[] kept = Enumerable.Range(0, 40).Except(leftOut).ToArray();
        var without = Fit(kept.Select(i => time[i]).ToArray(), kept.Select(i => censor[i]).ToArray(), kept.Select(i => 1.0).ToArray(), new[] { kept.Select(i => z1[i]).ToArray(), kept.Select(i => z2[i]).ToArray() }, 30, 1e-10);
        Say(with.fault == 0 && without.fault == 0, "both fits are made", true);
        Say(leftOut.All(r => Enumerable.Range(1, 4).All(c => with.caze[r + 1, c] == M)), "columns 1 to 4 of the figures of a record that is left out are missing", true);
        double worst = 0;
        for (int k = 0; k < kept.Length; k++)
            for (int c = 1; c <= 6; c++)
            {
                double a = with.caze[kept[k] + 1, c], b = without.caze[k + 1, c];
                if (a != b) worst = Math.Max(worst, Math.Abs(a - b));
            }
        Check("the figures of the other records are those of the fit without it", worst, 1e-10, true);
    }

    // With a precision of 0, which the dialog box allows, the change in the log likelihood has to vanish altogether, and in some sets of data it
    // never quite does.  The fit then either gives its result or says that it has not converged; it does not blame a predictor.
    private static void NotConverged()
    {
        Console.WriteLine("A fit that cannot converge");
        System.Random random = new(4);
        int results = 0, notConverged = 0;
        List<string> others = new();
        for (int set = 1; set <= 200; set++)
        {
            Data(random, 20 + random.Next(60), 0.6, out double[] time, out double[] censor, out double[] z1, out double[] z2);
            double[] code = censor.Select(c => 1 - c).ToArray();
            try { Coxreg.RptCoxRegression(Regression(time, code, new[] { z1, z2 }, 0.0)); results++; }
            catch (Exception ex)
            {
                string message = Message(ex);
                if (message.Contains("Calculation failed to converge")) notConverged++; else if (!others.Contains(message)) others.Add(message);
            }
        }
        Say(others.Count == 0 && notConverged > 0 && results > 0, $"200 sets with a precision of 0: {results} results, {notConverged} that did not converge and said so" + (others.Count > 0 ? "; other messages: " + string.Join(" | ", others) : ""), true);
    }
}
