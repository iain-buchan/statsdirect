// The figures of the Proportions menu from their definitions, for cases drawn at random.  Probabilities are added up from the
// logarithms of factorials, which are made here by adding logarithms, with what each sum leaves out carried; a confidence limit is found by bisection of what defines it;
// and the limits of the difference of two proportions are put into the statistic that defines them, with the most likely
// proportions found by a search of the likelihood.  Nothing of the program is used to make what is expected.
using StatsDirect.Templates;

internal static partial class Program
{
    private static double[] lf = { 0, 0 }, left = { 0, 0 };

    // The logarithms of the factorials of 0 to n.  A sum of millions of logarithms would be out in its seventh figure after the
    // point from the rounding of each step, so what a step leaves out is kept beside the sum and goes into the next
    private static void Factorials(int n)
    {
        if (lf.Length > n) return;
        double[] more = new double[n + 1], rest = new double[n + 1];
        Array.Copy(lf, more, lf.Length);
        Array.Copy(left, rest, left.Length);
        for (int i = lf.Length; i <= n; i++)
        {
            double term = Math.Log(i) + rest[i - 1];
            more[i] = more[i - 1] + term;
            rest[i] = term - (more[i] - more[i - 1]);
        }
        lf = more;
        left = rest;
    }

    // the logarithm of the number of ways to take k of n
    private static double Ways(int n, int k)
    {
        Factorials(n);
        return lf[n] - lf[k] - lf[n - k] + (left[n] - left[k] - left[n - k]);
    }

    // the logarithm of 1 + x, which keeps the figures of a small x
    private static double Log1p(double x)
    {
        double u = 1 + x;
        return u == 1 ? x : Math.Log(u) * x / (u - 1);
    }

    private static double Sum(IEnumerable<double> terms) { double s = 0; foreach (double t in terms.OrderBy(t => t)) s += t; return s; }

    // The root of a function that rises (or falls) through 0 between two values, by bisection
    internal static double Root(Func<double, double> f, double low, double high)
    {
        bool rises = f(high) > f(low);
        for (int i = 0; i < 300; i++)
        {
            double mid = 0.5 * (low + high);
            if (mid == low || mid == high) break;
            if (f(mid) > 0 == rises) high = mid; else low = mid;
        }
        return 0.5 * (low + high);
    }

    // The probability that a normal deviate is above z, from the integral of its density over steps of which each is done by the
    // rule of Simpson; and the deviate of a confidence level
    private static double AboveNormal(double z)
    {
        if (z < 0) return 1 - AboveNormal(-z);
        const int steps = 20000;
        double top = Math.Max(z + 12, 12), h = (top - z) / steps, s = 0;
        double Density(double x) => Math.Exp(-0.5 * x * x) / Math.Sqrt(2 * Math.PI);
        for (int i = 0; i <= steps; i++) s += Density(z + i * h) * (i == 0 || i == steps ? 1 : i % 2 == 1 ? 4 : 2);
        return s * h / 3;
    }

    private static readonly Dictionary<double, double> deviates = new();
    internal static double Deviate(double level)
    {
        if (!deviates.TryGetValue(level, out double z)) deviates[level] = z = Root(x => AboveNormal(x) - (1 - level) / 2, 0, 10);
        return z;
    }

    // the probabilities of the counts 0 to n of a binomial count
    private static double[] Binomial(int n, double pi)
    {
        Factorials(n);
        double[] pr = new double[n + 1];
        for (int k = 0; k <= n; k++)
            pr[k] = pi <= 0 ? (k == 0 ? 1 : 0) : pi >= 1 ? (k == n ? 1 : 0) : Math.Exp(Ways(n, k) + k * Math.Log(pi) + (n - k) * Log1p(-pi));
        return pr;
    }

    // the score limits of a proportion: the proportions that are z of their own standard errors from the proportion observed
    private static (double lower, double upper) ScoreLimits(double r, double n, double z)
    {
        double p = r / n;
        double f(double x) => (p - x) * (p - x) - z * z * x * (1 - x) / n;
        return (r == 0 ? 0 : Root(f, 0, Math.Min(p, 1 - 1e-15)), r == n ? 1 : Root(f, Math.Max(p, 1e-300), 1));
    }

    // the first of the figures of a report that is not what is expected, or nothing
    private static string First(ParameterBag report, double tolerance, params (string figure, double expected)[] figures)
    {
        foreach (var (figure, expected) in figures)
        {
            if (!report.ContainsKey(figure)) return $"{figure} is not given";
            object given = report[figure].AsObject;
            if (Differs(given, expected) > tolerance && !(given is double x && Math.Abs(expected) < 1e-300 && Math.Abs(x) < 1e-300))
                return $"{figure} is {given}, and is to be {(expected == M ? "missing" : expected.ToString("R", inv))}";
        }
        return null;
    }

    private static string SingleDiffers(int n, int r, double pi, double level)
    {
        ParameterBag report = Single(n, r, pi, level);
        double[] pr = Binomial(n, pi);
        double lower = Sum(pr.Take(r + 1)), upper = Sum(pr.Skip(r));
        double one = Math.Min(lower, upper), mid = one - pr[r] / 2;
        double two = Math.Min(1, Sum(pr.Where(x => x <= pr[r] * (1 + 1e-7))));
        double alpha = 1 - level, z = Deviate(level);
        // the limits of Clopper and Pearson: the proportion with which r or more has the probability alpha / 2, and that with which
        // r or fewer has it
        double Tail(double p, int from, int to) { double[] q = Binomial(n, p); return Sum(q.Skip(from).Take(to - from + 1)); }
        double least = r == 0 ? 0 : Root(p => Tail(p, r, n) - alpha / 2, 1e-300, 1 - 1e-16);
        double greatest = r == n ? 1 : Root(p => Tail(p, 0, r) - alpha / 2, 1e-300, 1 - 1e-16);
        var (from, to) = ScoreLimits(r, n, z);
        return First(report, 1e-8, ("prop", (double)r / n), ("null", pi), ("p_1_exact", one), ("p_2_exact", two), ("p_1_approx", mid), ("p_2_approx", Math.Min(1, 2 * mid)),
            ("lower_exact", least), ("upper_exact", greatest), ("lower_approx", from), ("upper_approx", to));
    }

    private static string PairedDiffers(int n, int r, int s, int t, double level)
    {
        ParameterBag report = Paired(n, r, s, t, level);
        double[] pr = Binomial(s + t, 0.5);
        int less = Math.Min(s, t);
        double one = Sum(pr.Take(less + 1)), mid = one - pr[less] / 2;
        string differs = First(report, 1e-9, ("prop_1", (double)(r + s) / n), ("prop_2", (double)(r + t) / n), ("prop_diff", (double)(s - t) / n));
        if (differs != null) return differs;
        List<ParameterBag> exact = Rows(report, "*exact");
        if (exact.Count != 1) return $"the report has {exact.Count} exact tests";
        differs = First(exact[0], 1e-9, ("cum_1", one), ("cum_2", Math.Min(1, 2 * one)), ("cum_1_mid", mid), ("cum_2_mid", Math.Min(1, 2 * mid)));
        if (differs != null) return differs;
        // The limits of the difference: from the score limits of the two proportions, and the correlation of the two responses
        // over the pairs, which has a correction for continuity if it is above 0
        double z = Deviate(level);
        double a = r, b = s, c = t, d = n - r - s - t;
        var (l1, u1) = ScoreLimits(a + b, n, z);
        var (l2, u2) = ScoreLimits(a + c, n, z);
        double p1 = (a + b) / n, p2 = (a + c) / n;
        double phi = 0;
        if ((a + b) * (c + d) * (a + c) * (b + d) > 0)
        {
            double covariance = a / n - p1 * p2, spread = Math.Sqrt(p1 * (1 - p1) * p2 * (1 - p2));
            phi = covariance > 0 ? Math.Max(covariance - 0.5 / n, 0) / spread : covariance / spread;
        }
        double low = p1 - p2 - Math.Sqrt(Math.Max(0, (p1 - l1) * (p1 - l1) - 2 * phi * (p1 - l1) * (u2 - p2) + (u2 - p2) * (u2 - p2)));
        double high = p1 - p2 + Math.Sqrt(Math.Max(0, (u1 - p1) * (u1 - p1) - 2 * phi * (u1 - p1) * (p2 - l2) + (p2 - l2) * (p2 - l2)));
        return First(report, 1e-8, ("lower", low), ("upper", high));
    }

    // The score statistic of the difference d of two proportions, with the two proportions that are most likely with that
    // difference: the likelihood has one peak, which is found by taking thirds from the range of the second proportion
    private static double ScoreStatistic(double d, double r1, double n1, double r2, double n2)
    {
        double Part(double x, double p) => x == 0 ? 0 : p <= 0 ? double.NegativeInfinity : x * Math.Log(p);
        double Rest(double x, double p) => x == 0 ? 0 : p >= 1 ? double.NegativeInfinity : x * Log1p(-p);
        double Likelihood(double p2) => Part(r1, p2 + d) + Rest(n1 - r1, p2 + d) + Part(r2, p2) + Rest(n2 - r2, p2);
        double low = Math.Max(0, -d), high = Math.Min(1, 1 - d);
        for (int i = 0; i < 400 && high > low; i++)
        {
            double third = (high - low) / 3, x = low + third, y = high - third;
            if (x == low || y == high) break;
            if (Likelihood(x) < Likelihood(y)) low = x; else high = y;
        }
        // the ends of the range are tried too
        double p = 0.5 * (low + high), best = Likelihood(p);
        foreach (double end in new[] { Math.Max(0, -d), Math.Min(1, 1 - d) }) if (Likelihood(end) > best) { p = end; best = Likelihood(end); }
        double q = Math.Min(1, Math.Max(0, p + d));
        double variance = (q * (1 - q) / n1 + p * (1 - p) / n2) * (n1 + n2) / (n1 + n2 - 1.0);
        double gap = r1 / n1 - r2 / n2 - d;
        return variance <= 0 ? (gap == 0 ? 0 : double.PositiveInfinity) : gap * gap / variance;
    }

    private static string UnpairedDiffers(int n1, int r1, int n2, int r2, double level, double tolerance = 1e-9)
    {
        ParameterBag report = Unpaired(n1, r1, n2, r2, level);
        double difference = (double)r1 / n1 - (double)r2 / n2, z = Deviate(level);
        string differs = First(report, 1e-9, ("prop_1", (double)r1 / n1), ("prop_2", (double)r2 / n2), ("prop_diff", difference));
        if (differs != null) return differs;
        // the limits are where the statistic has the value of chi-square for the level, one each side of the difference observed;
        // a limit at the end of the range is where the difference observed is
        double from = report["from"].AsDouble, to = report["to"].AsDouble;
        if (!(from <= difference && difference <= to)) return $"the limits {from} to {to} do not have the difference {difference} between them";
        foreach (var (limit, end, name) in new[] { (from, -1.0, "lower"), (to, 1.0, "upper") })
        {
            if (difference == end) { if (limit != end) return $"the {name} limit is {limit}, and the difference observed is {end}"; continue; }
            double statistic = ScoreStatistic(limit, r1, n1, r2, n2);
            // the search of the likelihood is good to 8 figures of the proportion, and the statistic to 6
            if (!(Math.Abs(statistic - z * z) <= 2e-6 * z * z)) return $"the {name} limit is {limit}, at which the statistic is {statistic} and not {z * z}";
        }
        // the exact mid-P value: from the probabilities of the first count with the totals of the table given
        int a = r1, b = n1 - r1, c = r2, d = n2 - r2;
        List<ParameterBag> exact = Rows(report, "*exact2");
        if (a + c == 0 || b + d == 0)
        {
            if (exact.Count != 0) return "an exact P value is given for a table with an empty row or column";
        }
        else
        {
            int first = a + c, second = b + d, row = a + b;
            int least = Math.Max(0, row - second), most = Math.Min(row, first);
            double all = Ways(first + second, row);
            double[] pr = Enumerable.Range(least, most - least + 1).Select(x => Math.Exp(Ways(first, x) + Ways(second, row - x) - all)).ToArray();
            double lower = Sum(pr.Take(a - least + 1)), upper = Sum(pr.Skip(a - least)), point = pr[a - least];
            if (exact.Count != 1) return "no exact P value is given";
            differs = First(exact[0], tolerance, ("mp", Math.Min(1, 2 * (Math.Min(lower, upper) - point / 2))));
            if (differs != null) return differs;
        }
        double pooled = (double)(r1 + r2) / (n1 + n2), se = Math.Sqrt(pooled * (1 - pooled) * (1.0 / n1 + 1.0 / n2));
        return se == 0 ? null : First(report, 1e-9, ("se", se), ("z", difference / se));
    }

    private static void Definitions()
    {
        Console.WriteLine();
        Console.WriteLine("Cases drawn at random, against the definitions");
        System.Random random = new(20261001);
        double[] levels = { 0.95, 0.99, 0.9 };

        int before = failures, done = 0;
        foreach (int most in new[] { 12, 60, 400, 3000 })
            for (int i = 0; i < 100; i++)
            {
                int n = random.Next(1, most + 1), r = random.Next(0, n + 1);
                double pi = i % 7 == 0 ? 0.5 : i % 7 == 1 ? (double)r / n : i % 11 == 2 ? 0 : i % 11 == 3 ? 1 : Math.Round(random.NextDouble(), 3);
                string differs;
                try { differs = SingleDiffers(n, r, pi, levels[i % 3]); } catch (Exception ex) { differs = Message(ex); }
                done++;
                if (differs != null) Say(false, $"{r} of {n}, null proportion {pi.ToString(inv)}: {differs}");
            }
        Say(failures == before, $"a single proportion: {done} cases");
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  a single proportion: the exact and mid-P values, the limits of Clopper and Pearson and the score limits, {done} cases");

        before = failures; done = 0;
        foreach (int most in new[] { 6, 40, 300, 6000 })
            for (int i = 0; i < 150; i++)
            {
                int r = random.Next(0, most + 1), s = random.Next(0, most + 1), t = random.Next(0, most + 1), d = random.Next(0, most + 1);
                if (i % 9 == 0) t = 0;
                if (i % 13 == 0) { r = 0; d = 0; }
                if (r + s + t + d == 0) continue;
                string differs;
                try { differs = PairedDiffers(r + s + t + d, r, s, t, levels[i % 3]); } catch (Exception ex) { differs = Message(ex); }
                done++;
                if (differs != null) Say(false, $"both {r}, first only {s}, second only {t} of {r + s + t + d}: {differs}");
            }
        Say(failures == before, $"paired proportions: {done} cases");
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  paired proportions: the exact and mid-P values, and the limits of the difference, {done} cases");

        before = failures; done = 0;
        foreach (int most in new[] { 5, 30, 300, 8000 })
            for (int i = 0; i < 150; i++)
            {
                int n1 = random.Next(1, most + 1), n2 = random.Next(1, most + 1);
                int r1 = random.Next(0, n1 + 1), r2 = random.Next(0, n2 + 1);
                // none responding in one sample and none but responders in the other; none, or all, in both
                if (i % 6 == 0) { r1 = 0; r2 = n2; }
                if (i % 6 == 1) { r1 = n1; r2 = 0; }
                if (i % 15 == 2) { r1 = 0; r2 = 0; }
                if (i % 15 == 3) { r1 = n1; r2 = n2; }
                if (i % 15 == 4) r1 = 0;
                string differs;
                try { differs = UnpairedDiffers(n1, r1, n2, r2, levels[i % 3]); } catch (Exception ex) { differs = Message(ex); }
                done++;
                if (differs != null) Say(false, $"{r1} of {n1} against {r2} of {n2}: {differs}");
            }
        Say(failures == before, $"two independent proportions: {done} cases");
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  two independent proportions: the limits of the difference, the exact mid-P value and the normal deviate, {done} cases");
    }
}
