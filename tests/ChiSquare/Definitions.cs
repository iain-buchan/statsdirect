// The figures of the Chi-square Tests menu from their definitions, for cases drawn at random.  A chi-square is the sum over the
// cells of a table of the squared difference of the observed and the expected count over the expected count, or a sum over the rows
// of the squared difference of a proportion from the proportion of all the rows; a P value is from the incomplete gamma function,
// which is worked out here; the P value of a simulation is compared with the probability of the tables that it counts, from every
// table that has the totals of the table observed.  Nothing of the program is used to make what is expected.
using StatsDirect.Templates;

internal static partial class Program
{
    // The logarithm of the gamma function of a whole number or of a whole number and a half, by adding logarithms
    private static double LogGammaOfHalves(int twice)
    {
        double s = 0;
        if (twice % 2 == 0) { for (int i = 2; i < twice / 2; i++) s += Math.Log(i); return s; }
        s = 0.5 * Math.Log(Math.PI);
        for (double x = 0.5; x < twice / 2.0 - 0.25; x += 1.0) s += Math.Log(x);
        return s;
    }

    // The probability that chi-square with df degrees of freedom is above x: the incomplete gamma function of df / 2 at x / 2, by
    // its series where x / 2 is below df / 2 + 1 and by its continued fraction above
    internal static double Above(double chi, int df)
    {
        if (double.IsNaN(chi)) return double.NaN;
        if (chi <= 0) return 1;
        double a = df / 2.0, x = chi / 2.0;
        double front = Math.Exp(-x + a * Math.Log(x) - LogGammaOfHalves(df));
        if (x < a + 1)
        {
            double term = 1 / a, sum = term;
            for (int n = 1; n < 100000; n++)
            {
                term *= x / (a + n);
                sum += term;
                if (term < sum * 1e-17) break;
            }
            return 1 - front * sum;
        }
        // Lentz's way with the continued fraction
        const double tiny = 1e-300;
        double b = x + 1 - a, c = 1 / tiny, d = 1 / b, h = d;
        for (int i = 1; i < 100000; i++)
        {
            double an = -i * (i - a);
            b += 2;
            d = an * d + b; if (Math.Abs(d) < tiny) d = tiny;
            c = b + an / c; if (Math.Abs(c) < tiny) c = tiny;
            d = 1 / d;
            double by = d * c;
            h *= by;
            if (Math.Abs(by - 1) < 1e-16) break;
        }
        return front * h;
    }

    // The root of a function that rises (or falls) through 0 between two values, by bisection
    internal static double Root(Func<double, double> f, double low, double high)
    {
        bool rises = f(high) > f(low);
        for (int i = 0; i < 200; i++)
        {
            double mid = 0.5 * (low + high);
            if (mid == low || mid == high) break;
            if (f(mid) > 0 == rises) high = mid; else low = mid;
        }
        return 0.5 * (low + high);
    }

    // the normal deviate of a confidence level: its square is the chi-square with 1 degree of freedom that is exceeded with the
    // probability 1 - level
    internal static double Deviate(double level) => Root(z => Above(z * z, 1) - (1 - level), 0, 40);

    private static string First(ParameterBag report, params (string figure, double expected)[] figures) => First(report, 1e-9, figures);

    // the first of the figures of a report that is not what is expected, or nothing; a P value may differ by a hundred times the
    // part by which a statistic may, for the P value of a large chi-square changes by many times the part by which the chi-square does
    private static string First(ParameterBag report, double tolerance, params (string figure, double expected)[] figures)
    {
        foreach (var (figure, expected) in figures)
        {
            if (!report.ContainsKey(figure)) return $"{figure} is not given";
            object given = report[figure].AsObject;
            double by = Differs(given, expected);
            double allowed = figure.EndsWith("_p") || figure.EndsWith("_px") ? 100 * tolerance : tolerance;
            if (by > allowed && !(given is double x && Math.Abs(expected) < 1e-12 && Math.Abs(x) < 1e-9))
                return $"{figure} is {given}, and is to be {(expected == M ? "missing" : expected.ToString("R", inv))}";
        }
        return null;
    }

    private static double[] Whole(System.Random random, int n, int most) => Enumerable.Range(0, n).Select(_ => (double)random.Next(0, most + 1)).ToArray();

    // ---- the 2 by 2 test
    private static string TwoByTwoDiffers(double a, double b, double c, double d, double level, int study)
    {
        ParameterBag report = TwoByTwo(a, b, c, d, level, study, false);
        double[,] o = { { a, b }, { c, d } };
        double n = a + b + c + d;
        double[] row = { a + b, c + d }, column = { a + c, b + d };
        double chi = 0, corrected = 0;
        for (int i = 0; i < 2; i++)
            for (int j = 0; j < 2; j++)
            {
                double e = row[i] * column[j] / n;
                chi += (o[i, j] - e) * (o[i, j] - e) / e;
                double less = Math.Max(0, Math.Abs(o[i, j] - e) - 0.5);
                corrected += less * less / e;
            }
        string differs = First(report, ("chi", chi), ("chi_p", Above(chi, 1)), ("yates_chi", corrected), ("yates_chi_p", Above(corrected, 1)),
            ("pearson", Math.Sqrt(chi / (chi + n))), ("vs", Math.Sign(a * d - b * c) * Math.Sqrt(chi / n)));
        if (differs != null || study != 0 || a * d == 0 || b * c == 0) return differs;
        // the limits of Woolf: the logarithm of the odds ratio has the variance 1 / a + 1 / b + 1 / c + 1 / d
        ParameterBag odds = Rows(report, "*odds")[0];
        double z = Deviate(level), l = Math.Log(a * d / (b * c)), se = Math.Sqrt(1 / a + 1 / b + 1 / c + 1 / d);
        return First(odds, ("odds", a * d / (b * c)), ("woolf_ci_1", Math.Exp(l - z * se)), ("woolf_ci_2", Math.Exp(l + z * se)));
    }

    // ---- the 2 by k test: the chi-square of the proportions of the rows about the proportion of them all, and the part of it that
    // the regression of the proportions on the scores accounts for
    private static (double total, double trend) OfRows(double[] s, double[] f, double[] v)
    {
        int k = s.Length;
        double[] n = s.Zip(f, (x, y) => x + y).ToArray();
        double all = n.Sum(), p = s.Sum() / all;
        double total = 0, mean = 0;
        for (int i = 0; i < k; i++) { total += n[i] * (s[i] / n[i] - p) * (s[i] / n[i] - p); mean += n[i] * v[i] / all; }
        total /= p * (1 - p);
        double cross = 0, square = 0;
        for (int i = 0; i < k; i++) { cross += n[i] * (s[i] / n[i] - p) * (v[i] - mean); square += n[i] * (v[i] - mean) * (v[i] - mean); }
        return (total, cross * cross / (p * (1 - p) * square));
    }

    private static string TwoByKDiffers(int trend, double[] s, double[] f, double[] v)
    {
        ParameterBag report = TwoByK(trend, s, f, v);
        int k = s.Length;
        double[] scores = trend == 2 ? v : Enumerable.Range(1, k).Select(i => (double)i).ToArray();
        var (total, linear) = OfRows(s, f, scores);
        string differs = First(report, ("chi", total), ("chi_abs", Math.Sqrt(total)), ("totdf", k - 1), ("chi_p", Above(total, k - 1)));
        if (differs != null || trend == 0) return differs;
        List<ParameterBag> z = Rows(report, "*z");
        if (z.Count != 1) return $"the report has {z.Count} tests for trend";
        differs = First(z[0], ("chi_lin", linear), ("chi_1df", Math.Sqrt(linear)), ("chi_lin_p", Above(linear, 1)));
        if (differs != null || k < 3) return differs;
        double left = total - linear;
        if (Math.Abs(left) < 1e-9 * total) left = 0;
        // what is left is a difference: it has the figures that the two chi-squares have after those that they share
        return First(Rows(z[0], "*non")[0], 1e-9 * Math.Max(1, total / Math.Max(left, 1e-300)), ("chi_non", left), ("df", k - 2), ("chi_non_p", Above(left, k - 2)));
    }

    // The probability of the tables whose chi-square for trend is no less than that of the table observed, among the tables that
    // have its totals: every such table is gone through, with the probability that the totals give it
    internal static double TrendProbability(int[] s, int[] n, double[] v)
    {
        int k = n.Length, successes = s.Sum(), all = n.Sum();
        double[] lf = new double[all + 1];
        for (int i = 2; i <= all; i++) lf[i] = lf[i - 1] + Math.Log(i);
        double Ways(int of, int take) => lf[of] - lf[take] - lf[of - take];
        double p = (double)successes / all, mean = 0, square = 0;
        for (int i = 0; i < k; i++) mean += n[i] * v[i] / all;
        for (int i = 0; i < k; i++) square += n[i] * (v[i] - mean) * (v[i] - mean);
        double Statistic(int[] a)
        {
            double cross = 0;
            for (int i = 0; i < k; i++) cross += (a[i] - n[i] * p) * (v[i] - mean);
            return cross * cross / (p * (1 - p) * square);
        }
        double observed = Statistic(s), probability = 0;
        int[] table = new int[k];
        void Go(int row, int left, double log)
        {
            if (row == k - 1)
            {
                if (left > n[row]) return;
                table[row] = left;
                if (Statistic(table) >= observed * (1 - 1e-9)) probability += Math.Exp(log + Ways(n[row], left) - Ways(all, successes));
                return;
            }
            for (int x = 0; x <= Math.Min(n[row], left); x++) { table[row] = x; Go(row + 1, left - x, log + Ways(n[row], x)); }
        }
        Go(0, successes, 0);
        return probability;
    }

    // ---- Woolf's analysis: the mean of the logarithms of the odds ratios with the reciprocals of their variances as weights
    private static string WoolfDiffers(double level, double[] t, bool sheet)
    {
        int k = t.Length / 4;
        ParameterBag report = sheet
            ? WoolfSheet(level, false, Enumerable.Range(0, k).SelectMany(i => new[] { t[4 * i] + t[4 * i + 1], t[4 * i], t[4 * i + 2] + t[4 * i + 3], t[4 * i + 2] }).ToArray())
            : WoolfTyped(level, false, t);
        double z = Deviate(level);
        foreach (bool haldane in new[] { false, true })
        {
            string x = haldane ? "x" : "";
            List<ParameterBag> block = Rows(report, haldane ? "*combined_with_haldane" : "*combined_no_haldane");
            bool every = Enumerable.Range(0, t.Length).All(i => t[i] > 0);
            bool expected = k > 1 && (haldane || every);
            if (block.Count != (expected ? 1 : 0)) return $"the tables together, {(haldane ? "with" : "without")} the correction: {block.Count} blocks";
            if (!expected) continue;
            double[] y = new double[k], w = new double[k];
            for (int i = 0; i < k; i++)
            {
                double a = t[4 * i], b = t[4 * i + 1], c = t[4 * i + 2], d = t[4 * i + 3];
                y[i] = haldane ? Math.Log((a + 0.5) * (d + 0.5) / ((b + 0.5) * (c + 0.5))) : Math.Log(a * d / (b * c));
                w[i] = 1 / (haldane ? 1 / (a + 1) + 1 / (b + 1) + 1 / (c + 1) + 1 / (d + 1) : 1 / a + 1 / b + 1 / c + 1 / d);
            }
            double mean = y.Zip(w, (u, v) => u * v).Sum() / w.Sum(), se = Math.Sqrt(1 / w.Sum());
            double between = y.Zip(w, (u, v) => v * (u - mean) * (u - mean)).Sum();
            string differs = First(block[0], 1e-8, ("mean" + x, mean), ("odds" + x, Math.Exp(mean)), ("var" + x, 1 / w.Sum()), ("se" + x, se),
                ("ci_from" + x, mean - z * se), ("ci_to" + x, mean + z * se), ("odds_from" + x, Math.Exp(mean - z * se)), ("odds_to" + x, Math.Exp(mean + z * se)),
                ("chi_2" + x, mean * mean * w.Sum()), ("chi_p" + x, Above(mean * mean * w.Sum(), 1)), ("df" + x, k - 1));
            if (differs != null) return differs;
            // the chi-square for heterogeneity is a sum of squares about the mean: the program has it as a difference
            differs = First(block[0], Math.Max(1e-8, 1e-13 * y.Zip(w, (u, v) => v * u * u).Sum() / Math.Max(between, 1e-300)), ("het_chi_2" + x, between), ("het_chi_p" + x, Above(between, k - 1)));
            if (differs != null) return differs;
        }
        return null;
    }

    // ---- the test of Mantel and Haenszel: the sum over the tables of the first count less its expectation, against the sum of its
    // variances, with the totals of each table given; and the pooled odds ratio with the variance of Robins, Breslow and Greenland
    private static string MantelDiffers(double level, double[] t)
    {
        int k = t.Length / 4;
        ParameterBag report = MantelTyped(level, false, t);
        double difference = 0, variance = 0, r = 0, s = 0, pr = 0, psqr = 0, qs = 0;
        bool every = t.All(x => x > 0);
        for (int i = 0; i < k; i++)
        {
            double a = t[4 * i], b = t[4 * i + 1], c = t[4 * i + 2], d = t[4 * i + 3], n = a + b + c + d;
            if ((a + b) * (c + d) * (a + c) * (b + d) <= 0) continue;
            difference += a - (a + b) * (a + c) / n;
            variance += (a + b) * (c + d) * (a + c) * (b + d) / (n * n * (n - 1));
            r += a * d / n; s += b * c / n;
            pr += (a + d) / n * (a * d / n); psqr += (a + d) / n * (b * c / n) + (b + c) / n * (a * d / n); qs += (b + c) / n * (b * c / n);
        }
        double less = Math.Abs(difference) >= 0.5 ? 0.5 : 0;
        double chi = (Math.Abs(difference) - less) * (Math.Abs(difference) - less) / variance;
        string differs = First(report, 1e-8, ("chi_mantel", chi), ("chi_p", Above(chi, 1)));
        if (differs != null || !every) return differs;
        double z = Deviate(level), se = Math.Sqrt(pr / (2 * r * r) + psqr / (2 * r * s) + qs / (2 * s * s));
        return First(report, 1e-8, ("odds", r / s), ("from", Math.Exp(Math.Log(r / s) - z * se)), ("to", Math.Exp(Math.Log(r / s) + z * se)));
    }

    // every figure of the tables that are typed against the figure of the same tables from a worksheet
    internal static string RoutesDiffer(double level, bool exact, double[] t)
    {
        Dictionary<string, object> typed = Report("x", () => MantelTyped(level, exact, t)), sheet = Report("x", () => MantelSheet(level, exact, t));
        if ((int)typed["x|ok"] != (int)sheet["x|ok"]) return $"typed: {(typed.TryGetValue("x|error", out object e) ? e : "a report")}; from the worksheet: {(sheet.TryGetValue("x|error", out object g) ? g : "a report")}";
        foreach (var figure in sheet)
        {
            if (figure.Key == "x|error") continue;
            if (!typed.TryGetValue(figure.Key, out object value)) return $"{figure.Key} is not given for the tables that are typed";
            bool same = value is string ? value.Equals(figure.Value) : figure.Value is double expected ? Differs(value, expected) <= 1e-12 : Convert.ToDouble(value, inv) == Convert.ToDouble(figure.Value, inv);
            if (!same) return $"{figure.Key} is {value} for the tables that are typed, and {figure.Value} from the worksheet";
        }
        return null;
    }

    private static string Table(IEnumerable<double> t) => string.Join(" ", t.Select(x => x.ToString(inv)));

    private static void Definitions()
    {
        Console.WriteLine();
        Console.WriteLine("Cases drawn at random, against the definitions");
        System.Random random = new(20260929);

        // 2 by 2 tables of tens, of hundreds and of hundreds of thousands, as each kind of study
        int before = failures, done = 0;
        foreach (int most in new[] { 12, 40, 300, 5000, 400000 })
            for (int i = 0; i < 240; i++)
            {
                double[] t = Whole(random, 4, most);
                if ((t[0] + t[1]) * (t[2] + t[3]) * (t[0] + t[2]) * (t[1] + t[3]) <= 0 || (i % 3 == 1 && t[1] == 0)) continue;
                // the exact method of the odds ratio is for the smaller tables here: it has its own checks
                int study = most > 5000 ? 2 : i % 3 == 0 ? 0 : i % 3 == 1 ? 1 : 2;
                double level = new[] { 0.95, 0.99, 0.9 }[i % 3];
                string differs;
                try { differs = TwoByTwoDiffers(t[0], t[1], t[2], t[3], level, study); } catch (Exception ex) { differs = Message(ex); }
                done++;
                if (differs != null) Say(false, $"the table {Table(t)}: {differs}");
            }
        Say(failures == before, $"the 2 by 2 test: {done} tables");
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  the 2 by 2 test: chi-square with and without the correction, the coefficients and the limits of Woolf, {done} tables");

        // 2 by k tables of 2 to 9 rows, with each kind of scores
        before = failures; done = 0;
        foreach (int most in new[] { 15, 200, 30000, 20000000 })
            for (int i = 0; i < 300; i++)
            {
                int k = random.Next(2, 10);
                double[] s = Whole(random, k, most), f = Whole(random, k, most);
                double[] v = Enumerable.Range(0, k).Select(_ => Math.Round(random.NextDouble() * 20 - 5, 1)).ToArray();
                if (s.Zip(f, (x, y) => x + y).Any(n => n <= 0) || s.Sum() <= 0 || f.Sum() <= 0 || v.Distinct().Count() < 2) continue;
                string differs;
                try { differs = TwoByKDiffers(i % 3, s, f, v); } catch (Exception ex) { differs = Message(ex); }
                done++;
                if (differs != null) Say(false, $"the rows {Table(s)} / {Table(f)}, scores {(i % 3 == 2 ? Table(v) : i % 3 == 1 ? "1 to k" : "none")}: {differs}");
            }
        Say(failures == before, $"the 2 by k test: {done} tables");
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  the 2 by k test: chi-square, the chi-square for trend and what is left, {done} tables");

        // The simulated exact P value of the test for trend, against the probability from every table with the totals of the table
        // observed; 5 standard errors of a share of 100,000 tables are allowed, which 1 simulation in 1,700,000 exceeds by chance
        before = failures; done = 0;
        for (int i = 0; i < 60; i++)
        {
            int k = random.Next(3, 6);
            int[] n = Enumerable.Range(0, k).Select(_ => random.Next(2, 10)).ToArray();
            int[] s = n.Select(x => random.Next(0, x + 1)).ToArray();
            // scores of few values, so that many tables have the chi-square of the table observed
            double[] v = i % 2 == 0 ? Enumerable.Range(1, k).Select(x => (double)x).ToArray() : Enumerable.Range(0, k).Select(_ => (double)random.Next(0, 4)).ToArray();
            if (s.Sum() == 0 || s.Sum() == n.Sum() || v.Distinct().Count() < 2) continue;
            double probability = TrendProbability(s, n, v);
            const int tables = 100000;
            ParameterBag report = Simulated(i % 2 == 0 ? 1 : 2, s.Select(x => (double)x).ToArray(), n.Zip(s, (x, y) => (double)(x - y)).ToArray(), v, tables, 1 + random.Next(1000000), 0.99);
            List<ParameterBag> result = Rows(report, "*result");
            done++;
            if (result.Count != 1) { Say(false, $"successes {string.Join(" ", s)} of {string.Join(" ", n)}: no P value is simulated"); continue; }
            double p = result[0]["p"].AsDouble, error = Math.Sqrt(probability * (1 - probability) / tables);
            if (Math.Abs(p - probability) > 5 * error) Say(false, $"successes {string.Join(" ", s)} of {string.Join(" ", n)}, scores {Table(v)}: the simulated P value is {p}, and the probability is {probability}");
            // the limits are those of a share of the tables drawn
            var (lower, upper) = (result[0]["ll"].AsDouble, result[0]["ul"].AsDouble);
            if (!(lower <= p && p <= upper)) Say(false, $"successes {string.Join(" ", s)} of {string.Join(" ", n)}: the limits {lower} to {upper} do not have the simulated P value {p} between them");
        }
        Say(failures == before, $"the simulated P value of the test for trend: {done} tables");
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  the simulated exact P value of the test for trend, against every table with the same totals, {done} tables");

        // series of 2 by 2 tables
        before = failures; done = 0;
        foreach (int most in new[] { 15, 300, 60000 })
            for (int i = 0; i < 200; i++)
            {
                int k = random.Next(1, 9);
                double[] t = Whole(random, 4 * k, most);
                // a table in three has an empty cell, among the small tables
                if (most > 15 || i % 3 != 0) t = t.Select(x => x + 1).ToArray();
                bool rows = Enumerable.Range(0, k).All(j => t[4 * j] + t[4 * j + 1] > 0 && t[4 * j + 2] + t[4 * j + 3] > 0);
                double level = new[] { 0.95, 0.99, 0.9 }[i % 3];
                string differs = null;
                try
                {
                    if (rows) differs = WoolfDiffers(level, t, i % 2 == 1);
                    if (differs != null) differs = "Woolf: " + differs;
                    bool pooled = Enumerable.Range(0, k).Any(j => (t[4 * j] + t[4 * j + 1]) * (t[4 * j + 2] + t[4 * j + 3]) * (t[4 * j] + t[4 * j + 2]) * (t[4 * j + 1] + t[4 * j + 3]) > 0);
                    if (differs == null && pooled) differs = MantelDiffers(level, t);
                    // the exact methods with the smaller tables
                    if (differs == null) differs = RoutesDiffer(level, most <= 300, t);
                }
                catch (Exception ex) { differs = Message(ex); }
                done++;
                if (differs != null) Say(false, $"the tables {Table(t)}: {differs}");
            }
        Say(failures == before, $"series of 2 by 2 tables: {done} series");
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  Woolf's analysis, the test of Mantel and Haenszel, and the tables that are typed against the same from a worksheet, {done} series");
    }
}
