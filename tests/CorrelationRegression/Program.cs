// Kendall's and Spearman's rank correlation, nonparametric linear regression and the least squares of simple linear regression, each through the
// program's own routine and against figures worked out here from the definitions.
using System.Numerics;
using System.Reflection;
using StatsDirect.Builtins;
using StatsDirect.Data;
using StatsDirect.Templates;

internal sealed class NoProgress : IProgressBarHost, IProgressBar
{
    public IProgressBar StartProgress(string operationDescription, bool provideProgress, bool display = true) => this;
    public void Finish() { }
    public bool Update(double fractionComplete) => false;
    public void Dispose() { }
}

internal static partial class Program
{
    private const double M = double.MinValue;
    private static int failures, checks;

    private static void Check(string what, double worst, double tolerance)
    {
        checks++;
        bool ok = worst <= tolerance;
        if (!ok) { failures++; Console.WriteLine($"FAIL  {what}: worst difference {worst:E2} (tolerance {tolerance:E0})"); }
    }

    private static string Message(Exception ex)
    {
        Exception inner = ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex;
        return inner.GetType().Name + ": " + inner.Message;
    }

    private static double NormalUpper(double z)
    {
        // the upper tail of the normal distribution, by the series or the continued fraction of the error function
        double x = Math.Abs(z) / Math.Sqrt(2);
        double erfc;
        if (x < 2.5)
        {
            double sum = x, term = x;
            for (int k = 1; k < 200; k++) { term *= -x * x / k; sum += term / (2 * k + 1); }
            erfc = 1 - 2 / Math.Sqrt(Math.PI) * sum;
        }
        else
        {
            double f = 0;
            for (int k = 60; k >= 1; k--) f = k / 2.0 / (x + f);
            erfc = Math.Exp(-x * x) / Math.Sqrt(Math.PI) / (x + f);
        }
        double upper = erfc / 2;
        return z >= 0 ? upper : 1 - upper;
    }

    private static double NormalQuantile(double p)
    {
        double low = -40, high = 40;
        for (int i = 0; i < 200; i++) { double mid = (low + high) / 2; if (1 - NormalUpper(mid) < p) low = mid; else high = mid; }
        return (low + high) / 2;
    }

    // the number of orderings of n things with each number of inversions: from it the distribution of Kendall's score without ties
    private static BigInteger[] Inversions(int n)
    {
        BigInteger[] counts = { BigInteger.One };
        for (int m = 2; m <= n; m++)
        {
            BigInteger[] next = new BigInteger[counts.Length + m - 1];
            for (int k = 0; k < counts.Length; k++)
                for (int j = 0; j < m; j++) next[k + j] += counts[k];
            counts = next;
        }
        return counts;
    }

    // P(S >= s) for Kendall's score S of n pairs without ties; S = N - 2 (inversions), N = n(n - 1)/2
    private static double KendallUpper(int n, double s)
    {
        BigInteger[] counts = Inversions(n);
        int pairs = n * (n - 1) / 2;
        BigInteger total = BigInteger.Zero, upper = BigInteger.Zero;
        for (int k = 0; k <= pairs; k++)
        {
            total += counts[k];
            if (pairs - 2 * k >= s) upper += counts[k];
        }
        return (double)upper / (double)total;
    }

    private static double[] Ranks(double[] v)
    {
        int n = v.Length;
        double[] r = new double[n];
        for (int i = 0; i < n; i++)
        {
            int less = v.Count(w => w < v[i]), equal = v.Count(w => w == v[i]);
            r[i] = less + (equal + 1) / 2.0;
        }
        return r;
    }

    private static double Median(IEnumerable<double> values)
    {
        double[] v = values.OrderBy(w => w).ToArray();
        return v.Length % 2 == 1 ? v[v.Length / 2] : (v[v.Length / 2 - 1] + v[v.Length / 2]) / 2;
    }

    private static void Kendall(string title, double[] x, double[] y, double gamma)
    {
        ParameterBag bag = new();
        DataFrame frame = new(new DoubleVariable((double[])x.Clone(), "X"));
        frame.Variables.Add(new DoubleVariable((double[])y.Clone(), "Y"));
        bag.AddInput("data", frame);
        bag.AddInput("gamma", gamma);
        try
        {
            ParameterBag o = Nonparametric.RptKendall(new NoProgress(), bag).ParameterBag;
            int[] ok = Enumerable.Range(0, x.Length).Where(i => x[i] != M && y[i] != M).ToArray();
            double[] a = ok.Select(i => x[i]).ToArray(), b = ok.Select(i => y[i]).ToArray();
            int n = a.Length;
            double con = 0, dis = 0, tiesX = 0, tiesY = 0;
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                {
                    double product = Math.Sign(a[i] - a[j]) * Math.Sign(b[i] - b[j]);
                    if (product > 0) con++; else if (product < 0) dis++;
                    if (a[i] == a[j]) tiesX++;
                    if (b[i] == b[j]) tiesY++;
                }
            double s = con - dis, pairs = n * (n - 1) / 2.0;
            double tau = s / Math.Sqrt((pairs - tiesX) * (pairs - tiesY));
            // the variance of the score: from the sizes of the groups of tied values
            double[] t = a.GroupBy(v => v).Select(g => (double)g.Count()).ToArray(), u = b.GroupBy(v => v).Select(g => (double)g.Count()).ToArray();
            double variance = (n * (n - 1.0) * (2 * n + 5) - t.Sum(v => v * (v - 1) * (2 * v + 5)) - u.Sum(v => v * (v - 1) * (2 * v + 5))) / 18;
            if (n > 2) variance += t.Sum(v => v * (v - 1) * (v - 2)) * u.Sum(v => v * (v - 1) * (v - 2)) / (9.0 * n * (n - 1) * (n - 2));
            variance += t.Sum(v => v * (v - 1)) * u.Sum(v => v * (v - 1)) / (2.0 * n * (n - 1));
            // the interval: from the score of each point against all the others
            double[] c = new double[n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    if (i != j) c[i] += Math.Sign(a[i] - a[j]) * Math.Sign(b[i] - b[j]);
            double mean = 2 * s / n;
            double vr = 2.0 / (n * (n - 1.0)) * (2 * (n - 2.0) / (n * (n - 1.0) * (n - 1.0)) * c.Sum(v => (v - mean) * (v - mean)) + 1 - tau * tau);
            double deviate = NormalQuantile(1 - (1 - gamma) / 2);
            Check(title + ": concordant, discordant, tied pairs and the score", Math.Abs(o["con"].AsDouble - con) + Math.Abs(o["dis"].AsDouble - dis) + Math.Abs(Convert.ToDouble(o["tie"].AsObject) - tiesX - tiesY) + Math.Abs(o["s"].AsDouble - s), 0);
            Check(title + ": tau b", Math.Abs(o["tau"].AsDouble - tau), 1e-13);
            Check(title + ": gamma", Math.Abs(o["gam"].AsDouble - s / (con + dis)), 1e-13);
            Check(title + ": standard error of the score", Math.Abs(o["ses"].AsDouble - Math.Sqrt(variance)) / Math.Sqrt(variance), 1e-13);
            Check(title + ": confidence limits", Math.Abs(o["ll"].AsDouble - Math.Max(-1, tau - deviate * Math.Sqrt(vr))) + Math.Abs(o["ul"].AsDouble - Math.Min(1, tau + deviate * Math.Sqrt(vr))), 1e-9);
            double z = s / Math.Sqrt(variance), zcc = (s - Math.Sign(s)) / Math.Sqrt(variance);
            Check(title + ": z and its P values", Math.Abs(o["kz"].AsDouble - z) + Math.Abs(o["p_u"].AsDouble - NormalUpper(z)) + Math.Abs(o["p_l"].AsDouble - (1 - NormalUpper(z))) + Math.Abs(o["p_2"].AsDouble - 2 * Math.Min(NormalUpper(z), 1 - NormalUpper(z))), 1e-9);
            Check(title + ": z with the continuity correction and its P values", Math.Abs(o["kzcc"].AsDouble - zcc) + Math.Abs(o["p_ucc"].AsDouble - NormalUpper(zcc)) + Math.Abs(o["p_2cc"].AsDouble - 2 * Math.Min(NormalUpper(zcc), 1 - NormalUpper(zcc))), 1e-9);
            if (n <= 50 && tiesX + tiesY == 0)
            {
                double upper = KendallUpper(n, s), lower = KendallUpper(n, -s);
                Check(title + ": exact P values", Math.Abs(o["p_uexact"].AsDouble - upper) + Math.Abs(o["p_lexact"].AsDouble - lower) + Math.Abs(o["p_2exact"].AsDouble - Math.Min(1, 2 * Math.Min(upper, lower))), 1e-9);
            }
        }
        catch (Exception ex) { checks++; failures++; Console.WriteLine("FAIL  " + title + ": " + Message(ex)); }
    }

    private static void Spearman(string title, double[] x, double[] y, double gamma)
    {
        ParameterBag bag = new();
        DataFrame frame = new(new DoubleVariable((double[])x.Clone(), "X"));
        frame.Variables.Add(new DoubleVariable((double[])y.Clone(), "Y"));
        bag.AddInput("data", frame);
        bag.AddInput("gamma", gamma);
        try
        {
            ParameterBag o = Nonparametric.RptSpearman(bag).ParameterBag;
            int[] ok = Enumerable.Range(0, x.Length).Where(i => x[i] != M && y[i] != M).ToArray();
            double[] ra = Ranks(ok.Select(i => x[i]).ToArray()), rb = Ranks(ok.Select(i => y[i]).ToArray());
            int n = ra.Length;
            double ma = ra.Average(), mb = rb.Average();
            double rho = ra.Zip(rb, (p, q) => (p - ma) * (q - mb)).Sum() / Math.Sqrt(ra.Sum(p => (p - ma) * (p - ma)) * rb.Sum(q => (q - mb) * (q - mb)));
            Check(title + ": rho", Math.Abs(o["rho"].AsDouble - rho), 1e-13);
            bool ties = ra.Distinct().Count() < n || rb.Distinct().Count() < n;
            if (n >= 4 && 1 - Math.Abs(rho) > 1e-12)
            {
                ParameterBag ci = ((System.Collections.IEnumerable)o["*ci"].AsObject).Cast<ParameterBag>().First();
                double deviate = NormalQuantile(1 - (1 - gamma) / 2), fz = 0.5 * Math.Log((1 + rho) / (1 - rho));
                Check(title + ": confidence limits", Math.Abs(ci["from"].AsDouble - Math.Tanh(fz - deviate / Math.Sqrt(n - 3))) + Math.Abs(ci["to"].AsDouble - Math.Tanh(fz + deviate / Math.Sqrt(n - 3))), 1e-9);
            }
            if (n >= 4 && n <= 9 && !ties)
            {
                // the exact distribution: every ordering of the ranks of y against those of x
                double d = ra.Zip(rb, (p, q) => (p - q) * (p - q)).Sum();
                long atLeast = 0, atMost = 0, total = 0;
                int[] perm = Enumerable.Range(1, n).ToArray();
                void Visit(int k)
                {
                    if (k == n)
                    {
                        double sum = 0;
                        for (int i = 0; i < n; i++) sum += (i + 1 - perm[i]) * (double)(i + 1 - perm[i]);
                        total++;
                        if (sum >= d) atLeast++;
                        if (sum <= d) atMost++;
                        return;
                    }
                    for (int i = k; i < n; i++) { (perm[k], perm[i]) = (perm[i], perm[k]); Visit(k + 1); (perm[k], perm[i]) = (perm[i], perm[k]); }
                }
                Visit(0);
                ParameterBag results = ((System.Collections.IEnumerable)((System.Collections.IEnumerable)o["*results"].AsObject).Cast<ParameterBag>().First()["*results"].AsObject).Cast<ParameterBag>().First();
                // the sum of squared differences of ranks is large when rho is negative: the lower side of rho is P(sum >= observed)
                double lower = (double)atLeast / total, upper = (double)atMost / total;
                Check(title + ": the sum of squared differences of ranks", Math.Abs(o["ix"].AsDouble - d), 1e-9);
                Check(title + ": exact P values", Math.Abs(results["p_l"].AsDouble - lower) + Math.Abs(results["p_u"].AsDouble - upper) + Math.Abs(results["p_2"].AsDouble - Math.Min(1, 2 * Math.Min(lower, upper))), 1e-6);
            }
        }
        catch (Exception ex) { checks++; failures++; Console.WriteLine("FAIL  " + title + ": " + Message(ex)); }
    }

    private static void Regression(string title, double[] x, double[] y, double gamma)
    {
        ParameterBag bag = new();
        bag.AddInput("outcome", new DataFrame(new DoubleVariable((double[])y.Clone(), "Y")));
        bag.AddInput("predictor", new DataFrame(new DoubleVariable((double[])x.Clone(), "X")));
        bag.AddInput("gamma", gamma);
        try
        {
            ParameterBag o = Nonparametric.RptNpRegression(new NoProgress(), bag).ParameterBag;
            int[] ok = Enumerable.Range(0, x.Length).Where(i => x[i] != M && y[i] != M).ToArray();
            double[] a = ok.Select(i => x[i]).ToArray(), b = ok.Select(i => y[i]).ToArray();
            int n = a.Length;
            List<double> slopes = new();
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                    if (a[i] != a[j]) slopes.Add((b[i] - b[j]) / (a[i] - a[j]));
            slopes.Sort();
            double slope = Median(slopes), intercept = Median(b) - slope * Median(a);
            ParameterBag results = ((System.Collections.IEnumerable)o["*results"].AsObject).Cast<ParameterBag>().First();
            Check(title + ": the slope, the median of the slopes of the pairs", Math.Abs(results["mdn"].AsDouble - slope), 1e-12);
            Check(title + ": the intercept", Math.Abs(results["intercept"].AsDouble - intercept), 1e-12);
            if (n <= 50)
            {
                // w is the greatest score whose upper tail is at least (1 - gamma) / 2; the limits are the rth slope from each end, r = (N - w) / 2 rounded down
                double p = (1 - gamma) / 2;
                int pairs = n * (n - 1) / 2, w = -1;
                for (int score = pairs; score >= 0; score--)
                    if ((score + pairs) % 2 == 0 && KendallUpper(n, score) >= p - 1e-14) { w = score; break; }
                int count = slopes.Count, r = (int)Math.Floor(0.5 * (count - w));
                double from = results["from"].AsDouble, to = results["to"].AsDouble;
                if (r < 1) Check(title + ": no limits, too few pairs", (from == M && to == M) ? 0 : 1, 0);
                else Check(title + ": confidence limits", Math.Abs(from - slopes[r - 1]) + Math.Abs(to - slopes[count - r]), 1e-12);
            }
            // the rank correlation that the report gives with it
            double con = 0, dis = 0;
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                {
                    double product = Math.Sign(a[i] - a[j]) * Math.Sign(b[i] - b[j]);
                    if (product > 0) con++; else if (product < 0) dis++;
                }
            double pairsAll = n * (n - 1) / 2.0;
            double tiesX = a.GroupBy(v => v).Sum(g => g.Count() * (g.Count() - 1) / 2.0), tiesY = b.GroupBy(v => v).Sum(g => g.Count() * (g.Count() - 1) / 2.0);
            Check(title + ": tau b", Math.Abs(o["tau"].AsDouble - (con - dis) / Math.Sqrt((pairsAll - tiesX) * (pairsAll - tiesY))), 1e-13);
        }
        catch (Exception ex) { checks++; failures++; Console.WriteLine("FAIL  " + title + ": " + Message(ex)); }
    }

    // least squares with the sums taken exactly, in decimal arithmetic, for data that are whole numbers of thousandths
    private static void LeastSquares(string title, double[] x, double[] y)
    {
        SimpleLinearRegressionContext context = new((double[])x.Clone(), (double[])y.Clone(), "X", "Y");
        context.CalculateLeastSquaresMethod();
        int n = x.Length;
        decimal sx = 0, sy = 0;
        for (int i = 0; i < n; i++) { sx += (decimal)x[i]; sy += (decimal)y[i]; }
        decimal mx = sx / n, my = sy / n, ssx = 0, ssy = 0, sxy = 0;
        for (int i = 0; i < n; i++)
        {
            decimal dx = (decimal)x[i] - mx, dy = (decimal)y[i] - my;
            ssx += dx * dx; ssy += dy * dy; sxy += dx * dy;
        }
        double slope = (double)(sxy / ssx), intercept = (double)(my - sxy / ssx * mx);
        double r = (double)sxy / Math.Sqrt((double)ssx * (double)ssy);
        double ms = (double)(ssy - sxy * sxy / ssx) / (n - 2);
        Check(title + ": slope", Math.Abs(context.Slope - slope) / Math.Abs(slope), 1e-9);
        Check(title + ": intercept", Math.Abs(context.YIntercept - intercept) / Math.Max(1, Math.Abs(intercept)), 1e-9);
        Check(title + ": correlation coefficient", Math.Abs(context.R - r), 1e-9);
        Check(title + ": sums of squares about the mean of x and of y", Math.Abs(context.SSX - (double)ssx) / (double)ssx + Math.Abs(context.SSY - (double)ssy) / (double)ssy, 1e-9);
        Check(title + ": residual mean square", Math.Abs(context.MS - ms) / Math.Max(ms, 1e-300), 1e-6);
    }

    private static int Main()
    {
        System.Random random = new(17);
        for (int set = 1; set <= 40; set++)
        {
            int n = set <= 12 ? 4 + set % 6 : 8 + random.Next(40);
            bool ties = set % 3 == 0, gaps = set % 5 == 0;
            double gamma = new[] { 0.95, 0.9, 0.99 }[set % 3 == 0 ? 0 : set % 2];
            double[] x = new double[n], y = new double[n];
            for (int i = 0; i < n; i++)
            {
                x[i] = ties ? random.Next(8) : Math.Round(random.NextDouble() * 100, 3) + i * 1e-4;
                y[i] = ties ? random.Next(6) + (random.Next(3) == 0 ? x[i] : 0) : Math.Round(0.4 * x[i] + random.NextDouble() * 60, 3) + i * 1e-5;
            }
            if (gaps) { x[1] = M; y[n - 2] = M; }
            string title = $"set {set} ({n} pairs{(ties ? ", ties" : "")}{(gaps ? ", two values missing" : "")}, {gamma * 100}%)";
            if (x.Where((v, i) => v != M && y[i] != M).Distinct().Count() < 2 || y.Where((v, i) => v != M && x[i] != M).Distinct().Count() < 2) continue;
            Kendall("Kendall, " + title, x, y, gamma);
            Spearman("Spearman, " + title, x, y, gamma);
            if (x.Count(v => v != M) > 6) Regression("nonparametric regression, " + title, x, y, gamma);
        }
        Console.WriteLine($"rank correlation and nonparametric regression: {checks} checks, {failures} failed");
        int before = checks, failed = failures;
        double[] Line(double origin, double step, int n, out double[] y)
        {
            double[] x = new double[n];
            y = new double[n];
            for (int i = 0; i < n; i++)
            {
                x[i] = origin + Math.Round(step * i + random.Next(5) * step / 10, 3);
                y[i] = Math.Round(3 + 2 * (x[i] - origin) + random.NextDouble() * 4, 3);
            }
            return x;
        }
        LeastSquares("x from 0", Line(0, 1, 20, out double[] y1), y1);
        LeastSquares("x about 2000 (years)", Line(2000, 1, 20, out double[] y2), y2);
        LeastSquares("x about 45000 (dates as day numbers)", Line(45000, 7, 30, out double[] y3), y3);
        LeastSquares("x about 1.7e9 (times in seconds)", Line(1700000000, 60, 30, out double[] y4), y4);
        LeastSquares("x about 1e6, steps of 0.01", Line(1000000, 0.01, 25, out double[] y5), y5);
        double[] x6 = Line(0, 1, 20, out double[] y6);
        LeastSquares("y about 1e9", x6, y6.Select(v => v + 1e9).ToArray());
        Console.WriteLine($"least squares: {checks - before} checks, {failures - failed} failed");
        SpearmanDistribution();
        KendallDistribution();

        Console.WriteLine(failures == 0 ? $"ALL {checks} CHECKS PASS" : $"{failures} OF {checks} CHECKS FAILED");
        return failures == 0 ? 0 : 1;
    }
}
