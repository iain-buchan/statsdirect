// Agreement of continuous measurements (Anova.RptAgreement): the intraclass correlation coefficient with its confidence interval, the within-subjects
// standard deviation, the repeatability coefficient, the limits of agreement of two columns, Kendall's rank correlation of the standard deviation
// of each subject with its mean, and what is passed to the plots.  The sums of squares are taken here in decimal arithmetic.
using StatsDirect.Builtins;
using StatsDirect.Data;
using StatsDirect.Templates;

internal static partial class Program
{
    private static void IntraclassCase(string title, double[][] columns, double gamma, double tolerance = 1e-9)
    {
        int before = failures;
        try
        {
            ParameterBag bag = new();
            DataFrame frame = new(new DoubleVariable((double[])columns[0].Clone(), "M1"));
            for (int c = 1; c < columns.Length; c++) frame.Variables.Add(new DoubleVariable((double[])columns[c].Clone(), "M" + (c + 1)));
            bag.AddInput("data", frame);
            bag.AddInput("ci", gamma);
            ParameterBag o = Anova.RptAgreement(new NoProgress(), bag).ParameterBag;

            int m = columns.Length;
            int[] rows = Enumerable.Range(0, columns[0].Length).Where(r => columns.All(c => c[r] != M)).ToArray();
            int n = rows.Length;
            // the one way analysis of variance, subjects as the groups, with the sums about the means taken in decimal arithmetic
            decimal grand = 0;
            foreach (int r in rows) for (int c = 0; c < m; c++) grand += (decimal)columns[c][r];
            grand /= n * m;
            decimal between = 0, within = 0;
            double[] mean = new double[n], sd = new double[n], greatest = new double[n], absolute = new double[n], difference = new double[n];
            for (int k = 0; k < n; k++)
            {
                int r = rows[k];
                decimal subject = 0;
                for (int c = 0; c < m; c++) subject += (decimal)columns[c][r];
                subject /= m;
                between += m * (subject - grand) * (subject - grand);
                decimal ss = 0;
                for (int c = 0; c < m; c++) ss += ((decimal)columns[c][r] - subject) * ((decimal)columns[c][r] - subject);
                within += ss;
                mean[k] = (double)subject;
                sd[k] = Math.Sqrt((double)ss / (m - 1));
                for (int c = 0; c < m; c++) absolute[k] += Math.Abs(columns[c][r] - mean[k]);
                greatest[k] = columns[0][r] - columns[1][r];
                for (int a = 0; a < m - 1; a++)
                    for (int b = a + 1; b < m; b++)
                        if (Math.Abs(columns[a][r] - columns[b][r]) > Math.Abs(greatest[k])) greatest[k] = columns[a][r] - columns[b][r];
                difference[k] = columns[0][r] - columns[1][r];
            }
            double df1 = n - 1, df2 = n * (m - 1.0);
            double msb = (double)between / df1, msw = (double)within / df2;
            double icc = (msb - msw) / (msb + (m - 1) * msw), f = msb / msw;
            double fu = FWithUpperTail((1 - gamma) / 2, df1, df2), fl = FWithUpperTail(1 - (1 - gamma) / 2, df1, df2);
            double lower = Math.Max(-1.0 / (m - 1), Math.Min(1, (f / fu - 1) / (f / fu + m - 1)));
            double upper = Math.Max(-1.0 / (m - 1), Math.Min(1, (f / fl - 1) / (f / fl + m - 1)));
            double z = NormalQuantile(1 - (1 - gamma) / 2);
            Check(title + ": intraclass correlation coefficient", Relative(o["icc"].AsDouble, icc), tolerance);
            Check(title + ": its confidence limits", Relative(o["icc_lcl"].AsDouble, lower) + Relative(o["icc_ucl"].AsDouble, upper), Math.Max(tolerance, 1e-8));
            Check(title + ": its degrees of freedom", Math.Abs(o["icc_df1"].AsDouble - df1) + Math.Abs(o["icc_df2"].AsDouble - df2), 0);
            Check(title + ": within-subjects standard deviation", Relative(o["wssd"].AsDouble, Math.Sqrt(msw)), tolerance);
            Check(title + ": repeatability", Relative(o["rep"].AsDouble, Math.Sqrt(2) * z * Math.Sqrt(msw)), Math.Max(tolerance, 1e-9));
            if (m == 2)
            {
                double md = difference.Average(), sdd = Math.Sqrt(difference.Sum(v => (v - md) * (v - md)) / (n - 1));
                ParameterBag all = Rows(o, "*all")[0];
                Check(title + ": limits of agreement", Relative(all["from"].AsDouble, md - z * sdd) + Relative(all["to"].AsDouble, md + z * sdd), 1e-8);
                Check(title + ": the width of the limits of agreement", Math.Abs((all["to"].AsDouble - all["from"].AsDouble) / (2 * z * sdd) - 1), 1e-9);
            }
            else Say(!o.ContainsKey("*all"), title + ": no limits of agreement with more than two columns");

            // Kendall's rank correlation of the standard deviations with the means
            double con = 0, dis = 0;
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                {
                    double product = Math.Sign(sd[i] - sd[j]) * Math.Sign(mean[i] - mean[j]);
                    if (product > 0) con++; else if (product < 0) dis++;
                }
            double[] t = sd.GroupBy(v => v).Select(g => (double)g.Count()).ToArray(), u = mean.GroupBy(v => v).Select(g => (double)g.Count()).ToArray();
            double tiesX = t.Sum(v => v * (v - 1) / 2), tiesY = u.Sum(v => v * (v - 1) / 2), pairs = n * (n - 1) / 2.0, s = con - dis;
            if (n > 2 && pairs > tiesX && pairs > tiesY)
            {
                double tau = s / Math.Sqrt((pairs - tiesX) * (pairs - tiesY));
                Check(title + ": Kendall's tau", Math.Abs(o["tau"].AsDouble - tau), 1e-12);
                double p2;
                if (tiesX + tiesY == 0 && n <= 50) p2 = Math.Min(1, 2 * KendallUpper(n, Math.Abs(s)));
                else
                {
                    double variance = (n * (n - 1.0) * (2 * n + 5) - t.Sum(v => v * (v - 1) * (2 * v + 5)) - u.Sum(v => v * (v - 1) * (2 * v + 5))) / 18
                        + t.Sum(v => v * (v - 1) * (v - 2)) * u.Sum(v => v * (v - 1) * (v - 2)) / (9.0 * n * (n - 1) * (n - 2))
                        + t.Sum(v => v * (v - 1)) * u.Sum(v => v * (v - 1)) / (2.0 * n * (n - 1));
                    p2 = 2 * NormalUpper(Math.Max(0, Math.Abs(s) - 1) / Math.Sqrt(variance));
                }
                if (tiesX + tiesY > 0 || n <= 50) Check(title + ": its two sided P", Math.Abs(o["p2"].AsDouble - p2), 1e-7);
                Say(o["tau_b_addendum"].AsString == (tiesX + tiesY > 0 ? "b " : ""), title + ": called tau b when there are ties");
            }

            // what is passed to the plots
            double[] av = ((DoubleVariable)o["av"].AsDataFrame.Variables[0]).Data, ssd = ((DoubleVariable)o["ssd"].AsDataFrame.Variables[0]).Data, mxd = ((DoubleVariable)o["mxd"].AsDataFrame.Variables[0]).Data;
            double[] xxm = ((DoubleVariable)o["xxm"].AsDataFrame.Variables[0]).Data, pvalues = ((DoubleVariable)o["pvalues"].AsDataFrame.Variables[0]).Data;
            Say(av.Length == n && ssd.Length == n && mxd.Length == n && xxm.Length == n && pvalues.Length == n, title + ": a point for each subject in each plot");
            if (av.Length == n)
            {
                Check(title + ": means of the subjects", Enumerable.Range(0, n).Max(k => Relative(av[k], mean[k])), 1e-12);
                Check(title + ": standard deviations of the subjects", Enumerable.Range(0, n).Max(k => Relative(ssd[k], sd[k])), Math.Max(tolerance, 1e-9));
                Check(title + ": greatest differences within the subjects", Enumerable.Range(0, n).Max(k => Relative(mxd[k], greatest[k])), 1e-12);
                double[] ordered = absolute.OrderBy(v => v).ToArray();
                Check(title + ": sums of the differences from the mean, in order", Enumerable.Range(0, n).Max(k => Relative(xxm[k], ordered[k])), Math.Max(tolerance, 1e-9));
                Check(title + ": chi-square quantiles from 0.01 to 0.99", Enumerable.Range(0, n).Max(k => Relative(pvalues[k], ChiSquareQuantile(0.01 + k * 0.98 / (n - 1), m - 1))), 1e-6);
            }
            Check(title + ": mean difference", Relative(o["mean"].AsDouble, m == 2 ? difference.Average() : o["mean"].AsDouble), 1e-9);
        }
        catch (Exception ex) { checks++; failures++; Console.WriteLine("FAIL  " + title + ": " + Message(ex)); }
        if (failures == before) Console.WriteLine("ok    " + title);
    }

    private static void Intraclass()
    {
        Console.WriteLine("Agreement of continuous measurements");
        // the example of the help: four peak flow measurements of twenty children (Bland and Altman 1996a)
        double[][] peak =
        {
            new double[] { 190, 220, 260, 210, 270, 280, 260, 275, 280, 320, 300, 270, 320, 335, 350, 360, 330, 335, 400, 430 },
            new double[] { 220, 200, 260, 300, 265, 280, 280, 275, 290, 290, 300, 250, 330, 320, 320, 320, 340, 385, 420, 460 },
            new double[] { 200, 240, 240, 280, 280, 270, 280, 275, 300, 300, 310, 330, 330, 335, 340, 350, 380, 360, 425, 480 },
            new double[] { 200, 230, 280, 265, 270, 275, 300, 305, 290, 290, 300, 370, 330, 375, 365, 345, 390, 370, 420, 470 }
        };
        IntraclassCase("the example of the help, 95%", peak, 0.95);
        IntraclassCase("the example of the help, 99%", peak, 0.99);
        IntraclassCase("two of its columns", new[] { peak[0], peak[1] }, 0.95);

        System.Random random = new(31);
        for (int set = 1; set <= 24; set++)
        {
            int n = 5 + random.Next(40), m = 2 + random.Next(4);
            double origin = new[] { 0.0, 50.0, 1000.0 }[set % 3], spread = new[] { 1.0, 10.0 }[set % 2];
            bool whole = set % 4 == 0;
            double[][] columns = Enumerable.Range(0, m).Select(c => new double[n]).ToArray();
            for (int r = 0; r < n; r++)
            {
                double subject = origin + spread * (random.NextDouble() * 6 - 3);
                for (int c = 0; c < m; c++) columns[c][r] = Math.Round(subject + spread * (random.NextDouble() - 0.5) * (1 + 0.2 * Math.Abs(subject - origin) / spread), whole ? 0 : 2);
            }
            if (set % 5 == 0) { columns[0][2] = M; columns[m - 1][n - 1] = M; }
            IntraclassCase($"set {set}: {n} subjects, {m} measurements each, about {origin}" + (whole ? ", whole numbers" : "") + (set % 5 == 0 ? ", two values missing" : ""), columns, new[] { 0.95, 0.9, 0.99 }[set % 3]);
        }

        // values that are large beside their spread
        foreach (double origin in new[] { 1e5, 1e7, 1e9 })
        {
            int n = 20, m = 3;
            double[][] columns = Enumerable.Range(0, m).Select(c => new double[n]).ToArray();
            for (int r = 0; r < n; r++)
            {
                double subject = origin + random.NextDouble() * 6 - 3;
                for (int c = 0; c < m; c++) columns[c][r] = Math.Round(subject + random.NextDouble() - 0.5, 2);
            }
            IntraclassCase($"values about {origin:G3} with a spread of units", columns, 0.95, Math.Max(1e-9, 4e-15 * origin));
        }
        // two columns that differ by much more than the differences vary
        foreach (double offset in new[] { 1e4, 1e6 })
        {
            int n = 25;
            double[][] columns = { new double[n], new double[n] };
            for (int r = 0; r < n; r++)
            {
                columns[0][r] = Math.Round(50 + 10 * random.NextDouble(), 1);
                columns[1][r] = Math.Round(columns[0][r] + offset + random.NextDouble() - 0.5, 1);
            }
            IntraclassCase($"two columns {offset:G3} apart, the differences with a spread of tenths", columns, 0.95, 1e-9);
        }
    }
}
