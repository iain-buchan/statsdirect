// The figures of the Exact Tests on Counts menu from their definitions, for cases drawn at random: probabilities are added up from
// the logarithms of factorials, which are made here by adding logarithms, and confidence limits are found by bisection of the sum
// of probabilities that defines them.  Nothing of the program is used to make what is expected.
using StatsDirect.Data;
using StatsDirect.Templates;

internal static partial class Program
{
    private static double[] lf = { 0, 0 };

    // the logarithms of the factorials of 0 to n
    private static void Factorials(int n)
    {
        if (lf.Length > n) return;
        double[] more = new double[n + 1];
        Array.Copy(lf, more, lf.Length);
        for (int i = lf.Length; i <= n; i++) more[i] = more[i - 1] + Math.Log(i);
        lf = more;
    }

    private static double Sum(IEnumerable<double> terms) { double s = 0; foreach (double t in terms.OrderBy(t => t)) s += t; return s; }

    // The root of a function that rises (or falls) through 0 between two values, by bisection
    private static double Root(Func<double, double> f, double low, double high)
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

    // The probabilities of the values that the first cell of a 2 by 2 table can have, of which the first row has p and the second
    // q subjects, and the first column r: the values are from least up
    private static (int least, double[] probability) FirstCell(int p, int q, int r)
    {
        Factorials(p + q);
        int least = Math.Max(0, r - q), most = Math.Min(p, r);
        double all = lf[p + q] - lf[r] - lf[p + q - r];
        double[] probability = new double[most - least + 1];
        for (int x = least; x <= most; x++)
            probability[x - least] = Math.Exp(lf[p] - lf[x] - lf[p - x] + lf[q] - lf[r - x] - lf[q - r + x] - all);
        return (least, probability);
    }

    private static string Table(double a, double b, double c, double d) => $"{a.ToString(inv)} {b.ToString(inv)} / {c.ToString(inv)} {d.ToString(inv)}";

    // Fisher's exact test and the expanded test of a table, against the definitions; nothing is said if they agree
    private static string FisherDiffers(int a0, int b0, int c0, int d0, bool rows)
    {
        // the table as the reports arrange it
        int a = Math.Min(a0, d0), d = Math.Max(a0, d0), b = Math.Min(b0, c0), c = Math.Max(b0, c0);
        int p = a + b, q = c + d, r = a + c;
        var (least, probability) = FirstCell(p, q, r);
        double observed = probability[a - least];
        double expectation = (double)p * r / (p + q);
        double lower = Sum(probability.Take(a - least + 1)), upper = Sum(probability.Skip(a - least));
        double one = a > expectation ? upper : lower;
        double two = Math.Min(1, Sum(probability.Where(x => x <= observed * (1 + 1e-7))));
        foreach (var (name, report) in new[] { ("Fisher's exact test", Fisher(a0, b0, c0, d0)), ("the expanded test", Expanded(a0, b0, c0, d0)) })
        {
            (string figure, double expected)[] figures =
            {
                ("tab3_a1", a), ("tab3_b1", b), ("tab3_a2", c), ("tab3_b2", d), ("tab3_c1", p), ("tab3_c2", q), ("tab3_a3", r), ("tab3_b3", b + d), ("tab3_c3", p + q),
                ("exp_a", expectation), ("p_1", one), ("p_1d", Math.Min(1, 2 * one)), ("p_2", two), ("mid_p", one - observed / 2), ("mid_p_2", Math.Min(1, 2 * (one - observed / 2)))
            };
            foreach (var (figure, expected) in figures)
            {
                double by = Differs(report[figure].AsObject, expected);
                if (by > 1e-9) return $"{name}: {figure} is {report[figure].AsObject}, and is to be {expected}";
            }
            string tail = a > expectation ? "(upper tail)" : "(lower tail)";
            if ((string)report["tail_1"].AsObject != tail) return $"{name}: the tail is {report["tail_1"].AsObject}, and is to be {tail}";
        }
        if (!rows) return null;
        // the rows of the expanded test: every value of the first cell from 0 to the total of the first row, with its probability and
        // the probabilities of no more and of no less
        List<ParameterBag> listed = Rows(Expanded(a0, b0, c0, d0), "*row");
        if (listed.Count != p + 1) return $"the expanded test has {listed.Count} rows, and the first cell can be 0 to {p}";
        for (int x = 0; x <= p; x++)
        {
            double each = x - least >= 0 && x - least < probability.Length ? probability[x - least] : 0;
            double below = Sum(probability.Take(Math.Max(0, Math.Min(probability.Length, x - least + 1))));
            double above = Sum(probability.Skip(Math.Max(0, x - least)));
            foreach (var (figure, expected) in new[] { ("a", (double)x), ("ind_p", each), ("lower", below), ("upper", above) })
            {
                object given = listed[x][figure].AsObject;
                double printed = given is string s ? double.Parse(s, System.Globalization.NumberStyles.Float, inv) : Convert.ToDouble(given, inv);
                // a probability is printed with 14 decimal places, or with 11 figures
                if (Math.Abs(printed - expected) > Math.Max(1.5e-14, 1e-9 * expected)) return $"the expanded test, row {x + 1}: {figure} is printed as {given}, and is {expected}";
            }
        }
        return null;
    }

    // The exact odds ratio of a table against the definitions
    private static string OddsDiffer(int a, int b, int c, int d, double level)
    {
        ParameterBag report = Odds(a, b, c, d, level);
        int m1 = a + b, n1 = a + c, n0 = b + d;
        int least = Math.Max(0, m1 - n0), most = Math.Min(m1, n1);
        Factorials(n1 + n0);
        (string figure, double expected)[] figures;
        double odds = (double)a * d == 0 && (double)b * c == 0 ? M : (double)a * d == 0 ? 0 : (double)b * c == 0 ? double.PositiveInfinity : (double)a * d / ((double)b * c);
        if (least == most)
            figures = new[] { ("odds", odds), ("eor", 0.0), ("llf", 0.0), ("ulf", double.PositiveInfinity), ("llm", 0.0), ("ulm", double.PositiveInfinity), ("p1f", 1.0), ("p2f", 1.0), ("p1m", 0.5), ("p2m", 1.0) };
        else
        {
            // the logarithm of the number of tables with each value of the first cell
            double[] ways = Enumerable.Range(least, most - least + 1).Select(x => lf[n1] - lf[x] - lf[n1 - x] + lf[n0] - lf[m1 - x] - lf[n0 - m1 + x]).ToArray();
            double[] With(double logOdds)
            {
                double[] w = ways.Select((v, i) => v + logOdds * (least + i)).ToArray();
                double greatest = w.Max();
                double[] e = w.Select(v => Math.Exp(v - greatest)).ToArray();
                double total = Sum(e);
                return e.Select(v => v / total).ToArray();
            }
            int at = a - least;
            double Mean(double l) { double[] pr = With(l); return Sum(pr.Select((v, i) => v * (least + i))); }
            double Above(double l, double half) { double[] pr = With(l); return Sum(pr.Skip(at + 1)) + half * pr[at]; }
            double Below(double l, double half) { double[] pr = With(l); return Sum(pr.Take(at)) + half * pr[at]; }
            double alpha = 1 - level;
            double[] atOne = With(0);
            double observed = atOne[at], up = Sum(atOne.Skip(at)), down = Sum(atOne.Take(at + 1));
            double mid = Math.Min(up - observed / 2, down - observed / 2);
            figures = new[]
            {
                ("odds", odds),
                ("eor", a == least ? 0 : a == most ? double.PositiveInfinity : Math.Exp(Root(l => Mean(l) - a, -300, 300))),
                ("llf", a == least ? 0 : Math.Exp(Root(l => Above(l, 1) - alpha / 2, -300, 300))),
                ("ulf", a == most ? double.PositiveInfinity : Math.Exp(Root(l => Below(l, 1) - alpha / 2, -300, 300))),
                ("llm", a == least ? 0 : Math.Exp(Root(l => Above(l, 0.5) - alpha / 2, -300, 300))),
                ("ulm", a == most ? double.PositiveInfinity : Math.Exp(Root(l => Below(l, 0.5) - alpha / 2, -300, 300))),
                ("p1f", Math.Min(up, down)), ("p2f", Math.Min(1, Sum(atOne.Where(x => x <= observed * (1 + 1e-7))))), ("p1m", mid), ("p2m", Math.Min(1, 2 * mid))
            };
        }
        foreach (var (figure, expected) in figures)
        {
            double by = Differs(report[figure].AsObject, expected);
            if (by > 1e-8) return $"{figure} is {report[figure].AsObject}, and is to be {(expected == M ? "missing" : expected.ToString("R", inv))}";
        }
        return null;
    }

    // The limits of a proportion of r in n (Clopper and Pearson): the proportion with which r or more has the probability alpha / 2,
    // and that with which r or fewer has it
    private static (double lower, double upper) ProportionLimits(int r, int n, double level)
    {
        Factorials(n);
        double alpha = 1 - level;
        double Tail(double p, int from, int to) => Sum(Enumerable.Range(from, to - from + 1).Select(k => Math.Exp(lf[n] - lf[k] - lf[n - k] + k * Math.Log(p) + (n - k) * Math.Log(1 - p))));
        double lower = r == 0 ? 0 : Root(p => Tail(p, r, n) - alpha / 2, 1e-300, 1 - 1e-16);
        double upper = r == n ? 1 : Root(p => Tail(p, 0, r) - alpha / 2, 1e-300, 1 - 1e-16);
        return (lower, upper);
    }

    // The probability of k or fewer of n being on one side, when either side is as likely
    private static double Half(int k, int n)
    {
        Factorials(n);
        return Sum(Enumerable.Range(0, k + 1).Select(i => Math.Exp(lf[n] - lf[i] - lf[n - i] - n * Math.Log(2))));
    }

    private static string SignDiffers(int n, int r, double level)
    {
        ParameterBag report = Sign(n, r, level);
        double one = Half(Math.Min(r, n - r), n);
        var (lower, upper) = ProportionLimits(r, n, level);
        ParameterBag exact = Rows(report, "*exact")[0];
        (string figure, object given, double expected)[] figures =
        {
            ("sample", report["sample"].AsObject, n), ("sample_1", report["sample_1"].AsObject, r), ("prob_1", exact["prob_1"].AsObject, one), ("prob_2", exact["prob_2"].AsObject, Math.Min(1, 2 * one)),
            ("z", report["z"].AsObject, Math.Max(0, Math.Abs(n / 2.0 - r) - 0.5) / Math.Sqrt(n / 4.0)), ("lower", report["lower"].AsObject, lower), ("upper", report["upper"].AsObject, upper),
            ("prop", report["prop"].AsObject, (double)r / n), ("ci", report["ci"].AsObject, 100 * level)
        };
        foreach (var (figure, given, expected) in figures)
            if (Differs(given, expected) > 1e-8) return $"{figure} is {given}, and is to be {expected.ToString("R", inv)}";
        if (Rows(report, "*large").Count != 0) return "the report says that the sample is too large";
        return null;
    }

    private static string PairsDiffer(int a, int b, int c, int d, double level)
    {
        ParameterBag report = Pairs(a, b, c, d, level);
        int n = b + c, r = Math.Max(b, c);
        var (lower, upper) = ProportionLimits(b, n, level);
        double two = Math.Min(1, 2 * Half(n - r, n));   // r or more one way is as likely as n - r or fewer
        (string figure, double expected)[] figures =
        {
            ("chi", (double)(b - c) * (b - c) / n), ("yates_chi", Math.Pow(Math.Max(Math.Abs(b - c) - 1, 0), 2) / n), ("risk", c > 0 ? (double)b / c : double.PositiveInfinity),
            ("from", b == 0 ? 0 : lower / (1 - lower)), ("to", b == n ? double.PositiveInfinity : upper / (1 - upper)), ("f", r / (n - r + 1.0)), ("tail_2", two), ("pc", 100 * level)
        };
        foreach (var (figure, expected) in figures)
            if (Differs(report[figure].AsObject, expected) > 1e-8) return $"{figure} is {report[figure].AsObject}, and is to be {expected.ToString("R", inv)}";
        if (Rows(report, "*r_prime").Count != (two < 0.05 ? 1 : 0)) return $"the two sided P value is {two}, and the report {(two < 0.05 ? "does not say" : "says")} that the ratio differs from 1";
        return null;
    }

    // The limits of the mean of a Poisson count of x: the mean with which x or more has the probability alpha / 2, and that with
    // which x or fewer has it
    private static string RateDiffers(int x, double time, double level)
    {
        ParameterBag report = Rate(x, time, level);
        int most = x + (int)(60 * Math.Sqrt(x + 1)) + 60;
        Factorials(most);
        double alpha = 1 - level;
        double Tail(double mean, int from, int to) => Sum(Enumerable.Range(from, to - from + 1).Select(k => Math.Exp(k * Math.Log(mean) - mean - lf[k])));
        double lower = x == 0 ? 0 : Root(m => Tail(m, x, most) - alpha / 2, 1e-300, most);
        double upper = Root(m => Tail(m, 0, x) - alpha / 2, 1e-300, most);
        foreach (var (figure, expected) in new[] { ("events", (double)x), ("time", time), ("rate", x / time), ("from", lower / time), ("to", upper / time), ("pc", 100 * level) })
            if (Differs(report[figure].AsObject, expected) > 1e-8) return $"{figure} is {report[figure].AsObject}, and is to be {expected.ToString("R", inv)}";
        return null;
    }

    private static void Definitions()
    {
        Console.WriteLine();
        Console.WriteLine("Cases drawn at random, against the definitions");
        System.Random random = new(20260928);
        double[] levels = { 0.95, 0.99, 0.9, 0.5, 0.999 };
        int[] Drawn(int most)
        {
            // a table of two groups, with the same or with different proportions in its first column
            int n1 = 1 + random.Next(most), n2 = 1 + random.Next(most);
            double p1 = 0.02 + 0.96 * random.NextDouble(), p2 = random.Next(2) == 0 ? p1 : 0.02 + 0.96 * random.NextDouble();
            int a = 0, c = 0;
            for (int i = 0; i < n1; i++) if (random.NextDouble() < p1) a++;
            for (int i = 0; i < n2; i++) if (random.NextDouble() < p2) c++;
            return new[] { a, n1 - a, c, n2 - c };
        }

        int before = failures, tables = 0;
        for (int trial = 0; trial < 2600; trial++)
        {
            int[] t = trial < 1500 ? Drawn(25) : trial < 2300 ? Drawn(300) : Drawn(4000);
            // one table in four has the same totals both ways, so that its distribution is the same from both ends
            if (trial % 4 == 0) { int k = t[0] + t[1]; t = new[] { t[0], k - t[0], k - t[0], t[0] }; }
            if (t[0] + t[1] == 0 || t[2] + t[3] == 0 || t[0] + t[2] == 0 || t[1] + t[3] == 0) continue;
            tables++;
            string differs;
            try { differs = FisherDiffers(t[0], t[1], t[2], t[3], trial < 2300); } catch (Exception ex) { differs = Message(ex); }
            Say(differs == null, $"the table {Table(t[0], t[1], t[2], t[3])}: {differs}");
        }
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  Fisher's exact test and the expanded test: {tables} tables");

        before = failures; tables = 0;
        for (int trial = 0; trial < 900; trial++)
        {
            int[] t = trial < 500 ? Drawn(25) : trial < 800 ? Drawn(300) : Drawn(3000);
            if (trial % 4 == 0) { int k = t[0] + t[1]; t = new[] { t[0], k - t[0], k - t[0], t[0] }; }
            tables++;
            string differs;
            try { differs = OddsDiffer(t[0], t[1], t[2], t[3], levels[random.Next(levels.Length)]); } catch (Exception ex) { differs = Message(ex); }
            Say(differs == null, $"the odds ratio of the table {Table(t[0], t[1], t[2], t[3])}: {differs}");
        }
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  the exact odds ratio: {tables} tables");

        before = failures;
        int cases = 0;
        for (int trial = 0; trial < 700; trial++)
        {
            int n = trial < 400 ? 1 + random.Next(60) : trial < 650 ? 1 + random.Next(1500) : 1 + random.Next(20000);
            int r = trial % 5 == 0 ? new[] { 0, n, n / 2, (n + 1) / 2, 1 }[random.Next(5)] : random.Next(n + 1);
            cases++;
            string differs;
            try { differs = SignDiffers(n, Math.Min(r, n), levels[random.Next(levels.Length)]); } catch (Exception ex) { differs = Message(ex); }
            Say(differs == null, $"the sign test of {r} of {n}: {differs}");
        }
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  the sign test: {cases} cases");

        before = failures; cases = 0;
        for (int trial = 0; trial < 700; trial++)
        {
            int most = trial < 400 ? 40 : trial < 650 ? 1000 : 10000;
            int b = random.Next(most + 1), c = trial % 6 == 0 ? new[] { 0, b, b + 1 }[random.Next(3)] : random.Next(most + 1);
            if (b + c == 0) continue;
            cases++;
            string differs;
            try { differs = PairsDiffer(random.Next(50), b, c, random.Next(50), levels[random.Next(levels.Length)]); } catch (Exception ex) { differs = Message(ex); }
            Say(differs == null, $"matched pairs of which {b} and {c} differ: {differs}");
        }
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  matched pairs: {cases} cases");

        before = failures; cases = 0;
        for (int trial = 0; trial < 400; trial++)
        {
            int x = trial < 250 ? random.Next(60) : trial < 380 ? random.Next(3000) : random.Next(200000);
            cases++;
            string differs;
            try { differs = RateDiffers(x, Math.Exp(random.NextDouble() * 20 - 5), levels[random.Next(levels.Length)]); } catch (Exception ex) { differs = Message(ex); }
            Say(differs == null, $"a Poisson rate of {x} events: {differs}");
        }
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  the Poisson rate: {cases} cases");
    }

    // Counts that are not whole numbers, tables with an empty row or column, and tables that are too large
    private static void Limits()
    {
        Console.WriteLine();
        Console.WriteLine("At the limits");
        int before = failures;
        // counts that are not whole numbers are rounded, a half to the even number
        foreach (var (given, rounded) in new[] { (new[] { 2.5, 3.5, 4.5, 5.5 }, new[] { 2.0, 4, 4, 6 }), (new[] { 10.4, 3.6, 7.2, 12.49 }, new[] { 10.0, 4, 7, 12 }), (new[] { 0.5, 1.5, 6.5, 7.5 }, new[] { 0.0, 2, 6, 8 }) })
        {
            foreach (var (name, report) in new (string, Func<double[], ParameterBag>)[] { ("Fisher's exact test", t => Fisher(t[0], t[1], t[2], t[3])), ("the expanded test", t => Expanded(t[0], t[1], t[2], t[3])), ("the exact odds ratio", t => Odds(t[0], t[1], t[2], t[3], 0.95)) })
            {
                ParameterBag of = report(given), whole = report(rounded);
                string differs = null;
                foreach (string figure in whole.Keys.Where(k => !k.StartsWith("*")))
                    if (!Equals(of[figure].AsObject, whole[figure].AsObject)) differs ??= $"{figure} is {of[figure].AsObject}, and for the counts rounded {whole[figure].AsObject}";
                Say(differs == null, $"{name} of {Table(given[0], given[1], given[2], given[3])} is that of {Table(rounded[0], rounded[1], rounded[2], rounded[3])}: {differs}");
            }
        }
        // a table with an empty row or an empty column: Fisher's exact test is not made; the odds ratio has limits of 0 and infinity
        foreach (int[] t in new[] { new[] { 0, 0, 3, 4 }, new[] { 3, 4, 0, 0 }, new[] { 0, 3, 0, 4 }, new[] { 3, 0, 4, 0 }, new[] { 0, 0, 0, 0 }, new[] { 1, 0, 0, 0 } })
        {
            string refused;
            try { Fisher(t[0], t[1], t[2], t[3]); refused = "a report"; } catch (Exception ex) { refused = Message(ex); }
            Say(refused.StartsWith("InvalidDataException"), $"Fisher's exact test of {Table(t[0], t[1], t[2], t[3])} gives {refused}");
            string differs;
            try { differs = OddsDiffer(t[0], t[1], t[2], t[3], 0.95); } catch (Exception ex) { differs = Message(ex); }
            Say(differs == null, $"the odds ratio of the table {Table(t[0], t[1], t[2], t[3])}: {differs}");
        }
        // a table that is too large for the exact odds ratio: the report says so
        {
            ParameterBag report = Odds(1000000, 2000000, 1500000, 2500000, 0.95);
            List<ParameterBag> notes = Rows(report, "*note");
            Say(notes.Count == 1 && ((string)notes[0]["note"].AsObject).StartsWith("The table is too large"), $"a table of 7,000,000: the report says {(notes.Count == 1 ? notes[0]["note"].AsObject : notes.Count + " things")}");
            Say(new[] { "eor", "llf", "ulf", "llm", "ulm", "p1f", "p2f", "p1m", "p2m" }.All(f => report[f].AsObject is double v && v == M), "a table of 7,000,000: the figures of the report are missing");
            report = Odds(10, 2, 3, 15, 0.95);
            Say(Rows(report, "*note").Count == 0, "the table 10 2 / 3 15: the report has nothing to say of a failure");
        }
        // small tails: the one sided P values of the odds ratio are those of Fisher's exact test of the same table
        foreach (int[] t in new[] { new[] { 144, 623, 140, 151 }, new[] { 2, 60, 55, 3 }, new[] { 60, 2, 3, 55 }, new[] { 400, 900, 700, 300 }, new[] { 5, 300, 290, 8 } })
        {
            double fisher = (double)Fisher(t[0], t[1], t[2], t[3])["p_1"].AsObject, odds = (double)Odds(t[0], t[1], t[2], t[3], 0.95)["p1f"].AsObject;
            Say(fisher > 0 && Math.Abs(fisher - odds) <= 1e-9 * fisher, $"the table {Table(t[0], t[1], t[2], t[3])}: the one sided P value of Fisher's exact test is {fisher}, and that of the odds ratio {odds}");
        }
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  counts that are not whole numbers, empty rows and columns, a table that is too large, small tails");
    }
}
