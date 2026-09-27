// Agreement of categories (Tables.RptKappa, Analysis.RptKappaScreen): for two raters Cohen's kappa, weighted kappa, Scott's pi and Gwet's AC1 with
// their standard errors, the tests of Maxwell and of McNemar generalised, and the interval for a 2 by 2 table; for more raters the kappa of each
// category and of all together.  The standard errors are checked against the delta method, carried out numerically: the variance of a function of
// the proportions of a table of N is (the sum of p g^2 less the square of the sum of p g) / N, g being its derivatives.
using StatsDirect.Builtins;
using StatsDirect.Data;
using StatsDirect.Templates;

internal static partial class Program
{
    private static double[] Marginal(double[,] p, bool rows)
    {
        int k = p.GetLength(0);
        double[] m = new double[k];
        for (int i = 0; i < k; i++) for (int j = 0; j < k; j++) m[rows ? i : j] += p[i, j];
        return m;
    }

    private static double[,] Proportions(double[,] o)
    {
        int k = o.GetLength(0);
        double n = 0;
        foreach (double v in o) n += v;
        double[,] p = new double[k, k];
        for (int i = 0; i < k; i++) for (int j = 0; j < k; j++) p[i, j] = o[i, j] / n;
        return p;
    }

    // kappa with weights w: agreement observed and expected are the sums of w p and of w (row proportion)(column proportion)
    private static double WeightedKappa(double[,] p, double[,] w)
    {
        int k = p.GetLength(0);
        double[,] q = Proportions(p);
        double[] r = Marginal(q, true), c = Marginal(q, false);
        double po = 0, pe = 0;
        for (int i = 0; i < k; i++) for (int j = 0; j < k; j++) { po += w[i, j] * q[i, j]; pe += w[i, j] * r[i] * c[j]; }
        return (po - pe) / (1 - pe);
    }

    private static double ScottPi(double[,] p)
    {
        int k = p.GetLength(0);
        double[,] q = Proportions(p);
        double[] r = Marginal(q, true), c = Marginal(q, false);
        double po = 0, pe = 0;
        for (int i = 0; i < k; i++) { po += q[i, i]; pe += Math.Pow((r[i] + c[i]) / 2, 2); }
        return (po - pe) / (1 - pe);
    }

    private static double GwetAc1(double[,] p)
    {
        int k = p.GetLength(0);
        double[,] q = Proportions(p);
        double[] r = Marginal(q, true), c = Marginal(q, false);
        double po = 0, pe = 0;
        for (int i = 0; i < k; i++) { po += q[i, i]; double pi = (r[i] + c[i]) / 2; pe += pi * (1 - pi); }
        pe /= k - 1;
        return (po - pe) / (1 - pe);
    }

    // the standard error of a function of the proportions by the delta method, at the proportions p, for a table of n
    private static double DeltaMethod(Func<double[,], double> f, double[,] p, double n)
    {
        int k = p.GetLength(0);
        double sum = 0, sumSquares = 0;
        for (int i = 0; i < k; i++)
            for (int j = 0; j < k; j++)
            {
                double h = 1e-6;
                double[,] up = (double[,])p.Clone(), down = (double[,])p.Clone();
                up[i, j] += h; down[i, j] -= h;
                double g = (f(up) - f(down)) / (2 * h);
                sum += p[i, j] * g;
                sumSquares += p[i, j] * g * g;
            }
        return Math.Sqrt(Math.Max(0, sumSquares - sum * sum) / n);
    }

    private static double[,] Identity(int k) { double[,] w = new double[k, k]; for (int i = 0; i < k; i++) w[i, i] = 1; return w; }

    private static double[,] Weights(int k, int kind)
    {
        double[,] w = new double[k, k];
        for (int i = 0; i < k; i++)
            for (int j = 0; j < k; j++)
                w[i, j] = kind == 2 ? 1 - Math.Pow((i - j) / (double)(k - 1), 2) : 1 - Math.Abs(i - j) / (double)(k - 1);
        return w;
    }

    private static double[,] Solve(double[,] a, int n, double[] b, out bool singular)
    {
        // Gauss-Jordan with pivoting on the leading n by n block: the solution of a x = b as a column
        double[,] w = new double[n, n + 1];
        for (int i = 0; i < n; i++) { for (int j = 0; j < n; j++) w[i, j] = a[i, j]; w[i, n] = b[i]; }
        singular = false;
        for (int c = 0; c < n; c++)
        {
            int pivot = c;
            for (int r = c + 1; r < n; r++) if (Math.Abs(w[r, c]) > Math.Abs(w[pivot, c])) pivot = r;
            if (Math.Abs(w[pivot, c]) < 1e-12) { singular = true; return null; }
            for (int j = 0; j <= n; j++) (w[c, j], w[pivot, j]) = (w[pivot, j], w[c, j]);
            double d = w[c, c];
            for (int j = 0; j <= n; j++) w[c, j] /= d;
            for (int r = 0; r < n; r++) if (r != c) { double f = w[r, c]; for (int j = 0; j <= n; j++) w[r, j] -= f * w[c, j]; }
        }
        double[,] x = new double[n, 1];
        for (int i = 0; i < n; i++) x[i, 0] = w[i, n];
        return x;
    }

    // the checks of one table of two raters, o[i, j] being the number put in category i by the first and in j by the second
    private static void TwoRaters(string title, ParameterBag report, double[,] o, double[,] w, double gamma)
    {
        int k = o.GetLength(0);
        double n = 0;
        foreach (double count in o) n += count;
        double[,] p = Proportions(o);
        double[] r = Marginal(p, true), c = Marginal(p, false);
        double[,] independent = new double[k, k];
        for (int i = 0; i < k; i++) for (int j = 0; j < k; j++) independent[i, j] = r[i] * c[j];
        double z = NormalQuantile(1 - (1 - gamma) / 2);
        double[,] identity = Identity(k);

        double po = 0, pe = 0, pow = 0, pew = 0;
        for (int i = 0; i < k; i++) { po += p[i, i]; pe += r[i] * c[i]; for (int j = 0; j < k; j++) { pow += w[i, j] * p[i, j]; pew += w[i, j] * r[i] * c[j]; } }
        double kappa = WeightedKappa(p, identity), kappaW = WeightedKappa(p, w);
        Check(title + ": agreement observed and expected", Math.Abs(report["po"].AsDouble - 100 * po) + Math.Abs(report["pe"].AsDouble - 100 * pe), 1e-9);
        Check(title + ": kappa", Math.Abs(report["kappa"].AsDouble - kappa), 1e-12);
        double se0 = DeltaMethod(q => WeightedKappa(q, identity), independent, n), se = DeltaMethod(q => WeightedKappa(q, identity), p, n);
        Check(title + ": standard error of kappa for the test", Relative(report["se"].AsDouble, se0), 1e-6);
        Check(title + ": standard error of kappa for the interval", Relative(report["seci"].AsDouble, se), 1e-6);
        Check(title + ": interval of kappa", Math.Abs(report["from"].AsDouble - Math.Max(-1, kappa - z * se)) + Math.Abs(report["to"].AsDouble - Math.Min(1, kappa + z * se)), 1e-6);
        Check(title + ": z and P of kappa", Math.Abs(report["z"].AsDouble - kappa / se0) / Math.Max(1, Math.Abs(kappa / se0)) + Math.Abs(report["p"].AsDouble - NormalUpper(kappa / se0)), 1e-5);

        Check(title + ": weighted agreement observed and expected", Math.Abs(report["pow"].AsDouble - 100 * pow) + Math.Abs(report["pew"].AsDouble - 100 * pew), 1e-9);
        Check(title + ": weighted kappa", Math.Abs(report["kappaw"].AsDouble - kappaW), 1e-12);
        double sew0 = DeltaMethod(q => WeightedKappa(q, w), independent, n), sew = DeltaMethod(q => WeightedKappa(q, w), p, n);
        Check(title + ": standard error of weighted kappa for the test", Relative(report["sekw"].AsDouble, sew0), 1e-6);
        Check(title + ": standard error of weighted kappa for the interval", Relative(report["sekwci"].AsDouble, sew), 1e-6);
        // the weights as the report prints them
        List<ParameterBag> printed = Rows(report, "*weights");
        double worstWeight = 0;
        for (int i = 0; i < k; i++)
        {
            List<ParameterBag> row = Rows(printed[i], "*tot");
            for (int j = 0; j < k; j++)
            {
                object shown = row[j]["tot"].AsObject;
                double value = shown is string text ? double.Parse(text, System.Globalization.CultureInfo.CurrentCulture) : Convert.ToDouble(shown);
                worstWeight = Math.Max(worstWeight, Math.Abs(value - w[i, j]));
            }
        }
        Check(title + ": the weights printed are the weights given, row for row", worstWeight, 1e-6);

        Check(title + ": Scott's pi", Math.Abs(report["spi"].AsDouble - ScottPi(p)), 1e-12);
        Check(title + ": Gwet's AC1", Math.Abs(report["gama"].AsDouble - GwetAc1(p)), 1e-12);
        double seGwet = DeltaMethod(GwetAc1, p, n);
        Check(title + ": standard error of AC1", Relative(report["segama"].AsDouble, seGwet), 1e-6);
        Check(title + ": agreement printed with AC1 is the agreement observed", Math.Abs(Convert.ToDouble(report["gamapc"].AsObject) - 100 * po), 0.005);

        // Maxwell's test of the marginal totals, and McNemar's test generalised
        double[] d = new double[k];
        double[,] v = new double[k, k];
        for (int i = 0; i < k; i++)
        {
            d[i] = n * (r[i] - c[i]);
            for (int j = 0; j < k; j++) v[i, j] = i == j ? n * (r[i] + c[i]) - 2 * o[i, i] : -(o[i, j] + o[j, i]);
        }
        double[,] x = Solve(v, k - 1, d, out bool singular);
        if (!singular)
        {
            double chi = 0;
            for (int i = 0; i < k - 1; i++) chi += d[i] * x[i, 0];
            Check(title + ": Maxwell's chi-square", Relative(report["x2"].AsDouble, chi), 1e-8);
            Check(title + ": its P", Math.Abs(report["pmaxwell"].AsDouble - ChiSquareUpper(chi, k - 1)), 1e-7);
        }
        double bowker = 0; int emptyPairs = 0;
        for (int i = 0; i < k - 1; i++) for (int j = i + 1; j < k; j++) { if (o[i, j] + o[j, i] > 0) bowker += Math.Pow(o[i, j] - o[j, i], 2) / (o[i, j] + o[j, i]); else emptyPairs++; }
        int pairs = k * (k - 1) / 2 - emptyPairs;
        if (pairs > 0)
        {
            Check(title + ": McNemar's chi-square generalised", Relative(Convert.ToDouble(report["x2m"].AsObject), bowker), 1e-10);
            Check(title + ": its degrees of freedom, the pairs of categories with a subject", Math.Abs(Convert.ToDouble(report["dfmcnemar"].AsObject) - pairs), 0);
            Check(title + ": its P", Math.Abs(Convert.ToDouble(report["pmcnemar"].AsObject) - ChiSquareUpper(bowker, pairs)), 1e-7);
        }
        else
        {
            object given = report["x2m"].AsObject;
            Say(given is string || Convert.ToDouble(given) == M, title + ": no McNemar's test when no two categories share a subject");
            Say(Convert.ToDouble(report["pmcnemar"].AsObject) == M, title + ": and no P for it");
        }
        if (emptyPairs > 0) sparse++;

        if (k == 2)
        {
            // the interval for a 2 by 2 table: the values of kappa at which the chi-square of goodness of fit of the model with a common
            // proportion is the square of the normal deviate
            List<ParameterBag> interval = Rows(report, "*deci");
            double n1 = o[0, 0], n2 = o[0, 1] + o[1, 0], n3 = o[1, 1], pi = (2 * n1 + n2) / (2 * n);
            double Fit(double kap)
            {
                double e1 = n * (pi * pi + kap * pi * (1 - pi)), e2 = n * 2 * pi * (1 - pi) * (1 - kap), e3 = n * ((1 - pi) * (1 - pi) + kap * pi * (1 - pi));
                return Math.Pow(n1 - e1, 2) / e1 + Math.Pow(n2 - e2, 2) / e2 + Math.Pow(n3 - e3, 2) / e3 - z * z;
            }
            double estimate = 1 - n2 / (2 * n * pi * (1 - pi));
            if (interval.Count == 1 && n2 > 0)
            {
                double least = -Math.Min(pi / (1 - pi), (1 - pi) / pi) + 1e-9;
                double Root(double a, double b) { for (int i = 0; i < 200; i++) { double m = (a + b) / 2; if (Math.Sign(Fit(m)) == Math.Sign(Fit(a))) a = m; else b = m; } return (a + b) / 2; }
                if (Fit(least) > 0)
                    Check(title + ": lower limit for a 2 by 2 table", Math.Abs(interval[0]["lwr"].AsDouble - Root(least, estimate)), 1e-7);
                Check(title + ": upper limit for a 2 by 2 table", Math.Abs(interval[0]["upr"].AsDouble - Root(estimate, 1 - 1e-12)), 1e-7);
            }
        }
    }

    private static int sparse;      // the tables with a pair of categories in which nobody was put

    private static ParameterBag Screen(double[,] o, int method, double[,] userWeights, double gamma)
    {
        int k = o.GetLength(0);
        ParameterBag bag = new();
        DataFrame table = new(new DoubleVariable(Enumerable.Range(0, k).Select(i => o[i, 0]).ToArray(), "C1"));
        for (int j = 1; j < k; j++) table.Variables.Add(new DoubleVariable(Enumerable.Range(0, k).Select(i => o[i, j]).ToArray(), "C" + (j + 1)));
        bag.AddInput("responsesCrosstab", table);
        bag.AddInput("ci", gamma);
        bag.AddInput("method", method.ToString());
        if (method == 3)
        {
            // the table of weights as it is laid out on the worksheet: a column of the block for each column of the table
            DataFrame weights = new(new DoubleVariable(Enumerable.Range(0, k).Select(i => userWeights[i, 0]).ToArray(), "W1"));
            for (int j = 1; j < k; j++) weights.Variables.Add(new DoubleVariable(Enumerable.Range(0, k).Select(i => userWeights[i, j]).ToArray(), "W" + (j + 1)));
            bag.AddInput("weights", weights);
        }
        return Analysis.RptKappaScreen(new Plain(), bag).ParameterBag;
    }

    private static void Kappa()
    {
        Console.WriteLine();
        Console.WriteLine("Agreement of categories, two raters, from a table");
        System.Random random = new(41);
        for (int set = 1; set <= 40; set++)
        {
            int k = 2 + set % 5, n = 30 + random.Next(200);
            double[,] o = new double[k, k];
            double agreement = 0.3 + 0.5 * random.NextDouble();
            for (int s = 0; s < n; s++)
            {
                int i = random.Next(k);
                int j = random.NextDouble() < agreement ? i : Math.Min(k - 1, Math.Max(0, i + random.Next(3) - 1 + (random.Next(4) == 0 ? 1 : 0)));
                o[i, j]++;
            }
            int method = 1 + set % 4;      // 1 linear, 2 quadratic, 3 given and symmetric, 4 given and not symmetric
            double[,] w = method <= 2 ? Weights(k, method) : new double[k, k];
            if (method >= 3)
                for (int i = 0; i < k; i++)
                    for (int j = i; j < k; j++)
                    {
                        w[i, j] = i == j ? 1 : Math.Round(Math.Max(0, 1 - 0.4 * (j - i) - 0.1 * random.NextDouble()), 2);
                        w[j, i] = method == 3 ? w[i, j] : Math.Round(w[i, j] * 0.5, 2);
                    }
            double gamma = new[] { 0.95, 0.9, 0.99 }[set % 3];
            string title = $"set {set}: {k} categories, {n} subjects, " + new[] { "", "linear weights", "quadratic weights", "weights given, symmetric", "weights given, not symmetric" }[method];
            int before = failures;
            try { TwoRaters(title, Screen(o, Math.Min(method, 3), w, gamma), o, w, gamma); }
            catch (Exception ex) { checks++; failures++; Console.WriteLine("FAIL  " + title + ": " + Message(ex)); }
            if (failures == before) Console.WriteLine("ok    " + title);
        }
        // perfect agreement: nobody off the diagonal, so no test of symmetry
        {
            double[,] o = { { 12, 0, 0 }, { 0, 9, 0 }, { 0, 0, 14 } };
            string title = "perfect agreement in three categories";
            try
            {
                ParameterBag report = Screen(o, 1, null, 0.95);
                Check(title + ": kappa", Math.Abs(report["kappa"].AsDouble - 1), 1e-12, true);
                Say(report["x2m"].AsDouble == M && report["pmcnemar"].AsDouble == M, title + ": no McNemar's test", true);
            }
            catch (Exception ex) { checks++; failures++; Console.WriteLine("FAIL  " + title + ": " + Message(ex)); }
        }
        Console.WriteLine($"      ({sparse} of the tables had a pair of categories in which nobody was put)");

        ManyRaters();
        FromColumns();
        Symmetrise();
        KappaSimulated();
    }
}
