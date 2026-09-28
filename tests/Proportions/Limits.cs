// The reports of the Proportions menu where the numbers are at the ends of what they can be: what is refused and with what words,
// null proportions of 0 and 1, none and all responding, no pairs that differ, samples of millions, numbers that are not whole
// numbers, and one sample without responders against one with none but responders.
using StatsDirect.Templates;

internal static partial class Program
{
    // what a report is refused with, or nothing if it is given
    private static string Refused(Func<ParameterBag> report)
    {
        try { report(); return null; } catch (Exception ex) { return Message(ex); }
    }

    // the first figure of one report that is not the figure of another, or nothing
    private static string Unlike(ParameterBag one, ParameterBag other)
    {
        Dictionary<string, object> a = new(), b = new();
        Collect("", one, a);
        Collect("", other, b);
        foreach (string name in a.Keys.Union(b.Keys))
            if (!a.ContainsKey(name) || !b.ContainsKey(name) || !a[name].Equals(b[name]))
                return $"{name}: {(a.ContainsKey(name) ? a[name] : "none")} and {(b.ContainsKey(name) ? b[name] : "none")}";
        return null;
    }

    private static double Seconds(Action what)
    {
        System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
        what();
        return clock.Elapsed.TotalSeconds;
    }

    private static void Limits()
    {
        Console.WriteLine();
        Console.WriteLine("At the limits");
        int before = failures;

        // ---- a single proportion
        {
            string refused = Refused(() => Single(10, 11, 0.5, 0.95));
            Say(refused != null && refused.Contains("can not be more than the total"), $"11 of 10 is refused, and the refusal says why ({refused})");
            refused = Refused(() => Single(10, -1, 0.5, 0.95));
            Say(refused != null && refused.Contains("can not be below 0"), $"-1 of 10 is refused, and the refusal says why ({refused})");
            Say(Refused(() => Single(0, 0, 0.5, 0.95)) != null, "0 of 0 is refused");
            Say(Refused(() => Single(0.4, 0, 0.5, 0.95)) != null, "0 of 0.4, which is 0 of 0 when it is rounded, is refused");

            // a null proportion of 0 or 1: the count can be nothing but 0, or nothing but all
            foreach (var (r, pi, p, mid) in new[] { (0, 0.0, 1.0, 0.5), (3, 0.0, 0.0, 0.0), (12, 0.0, 0.0, 0.0), (12, 1.0, 1.0, 0.5), (11, 1.0, 0.0, 0.0), (0, 1.0, 0.0, 0.0) })
            {
                string differs = First(Single(12, r, pi, 0.95), 0, ("null", pi), ("p_1_exact", p), ("p_2_exact", p), ("p_1_approx", mid), ("p_2_approx", Math.Min(1, 2 * mid)));
                Say(differs == null, $"{r} of 12 with the null proportion {pi}: {differs}");
            }
            // a null proportion that is not from 0 to 1 is taken as 0 or as 1
            Say(Unlike(Single(12, 3, -0.2, 0.95), Single(12, 3, 0, 0.95)) == null && Unlike(Single(12, 3, 1.5, 0.95), Single(12, 3, 1, 0.95)) == null,
                "a null proportion of -0.2 is taken as 0, and one of 1.5 as 1");
            // a confidence level that is not between 0 and 1 is taken as 0.95
            Say(Unlike(Single(12, 3, 0.5, 0), Single(12, 3, 0.5, 0.95)) == null && Unlike(Single(12, 3, 0.5, 1), Single(12, 3, 0.5, 0.95)) == null,
                "a confidence level of 0 or of 1 is taken as 0.95");

            // none and all responding: the limit on that side is 0 or 1, and the other is that of one side
            foreach (double level in new[] { 0.95, 0.99 })
            {
                ParameterBag none = Single(25, 0, 0.5, level), all = Single(25, 25, 0.5, level);
                double limit = 1 - Math.Pow((1 - level) / 2, 1.0 / 25);
                string differs = First(none, 1e-12, ("lower_exact", 0), ("upper_exact", limit), ("lower_approx", 0)) ?? First(all, 1e-12, ("lower_exact", 1 - limit), ("upper_exact", 1), ("upper_approx", 1));
                Say(differs == null, $"0 and 25 of 25 at the level {level}: {differs}");
                string side = $"{100 * (level + (1 - level) / 2):0.#}% one-sided";
                Say(none["warn_exact"].AsString.Contains(side) && all["warn_exact"].AsString.Contains(side) && Single(25, 1, 0.5, level)["warn_exact"].AsString == "",
                    $"and the report says that the interval is of one side ({none["warn_exact"].AsString.Trim()}), which it does not say of 1 of 25");
            }

            // numbers that are not whole numbers are rounded, a half to the even number
            Say(Unlike(Single(20.4, 7.6, 0.3, 0.95), Single(20, 8, 0.3, 0.95)) == null, "7.6 of 20.4 has the report of 8 of 20");
            Say(Unlike(Single(10.5, 2.5, 0.3, 0.95), Single(10, 2, 0.3, 0.95)) == null && Unlike(Single(11.5, 3.5, 0.3, 0.95), Single(12, 4, 0.3, 0.95)) == null,
                "2.5 of 10.5 has the report of 2 of 10, and 3.5 of 11.5 that of 4 of 12");

            // Samples of more than a million: the P values are binomial, and the report names them so.  The limits are left to the
            // benchmarks, for the limits of the definitions would take a minute
            foreach (var (n, r, pi) in new[] { (1000001, 500700, 0.5), (2000000, 1001300, 0.5), (2000000, 6400, 0.003), (3000000, 2250000, 0.75), (1500000, 14, 0.00001) })
            {
                ParameterBag report = null;
                double taken = Seconds(() => report = Single(n, r, pi, 0.95));
                double[] pr = Binomial(n, pi);
                double one = Math.Min(Sum(pr.Take(r + 1)), Sum(pr.Skip(r))), mid = one - pr[r] / 2;
                string differs = First(report, 1e-8, ("p_1_exact", one), ("p_2_exact", Math.Min(1, Sum(pr.Where(x => x <= pr[r] * (1 + 1e-7))))), ("p_1_approx", mid), ("p_2_approx", Math.Min(1, 2 * mid)));
                Say(differs == null, $"{r} of {n} with the null proportion {pi}: {differs}");
                Say(report["ap_1_exact"].AsString == "Binomial" && report["ap_2_exact"].AsString == "Binomial" && report["ap_1_approx"].AsString == "Binomial" && report["ap_2_approx"].AsString == "Binomial",
                    $"{r} of {n}: the P values are named Binomial");
                Say(taken < 2, $"{r} of {n} takes {taken:F2} seconds");
            }
            // The largest sample that can be asked for: with a null proportion of a half the tail is that of a normal deviate, with
            // the correction for continuity, to a part in a million
            {
                const int n = int.MaxValue;
                double sd = Math.Sqrt(n / 4.0);
                foreach (double z in new[] { 0.4, 1.7, 3.1 })
                {
                    int r = (int)Math.Round(n / 2.0 + z * sd);
                    ParameterBag report = null;
                    double taken = Seconds(() => report = Single(n, r, 0.5, 0.95));
                    double tail = AboveNormal((r - 0.5 - n / 2.0) / sd);
                    Say(Differs(report["p_1_exact"].AsObject, tail) < 1e-6 && Differs(report["p_2_exact"].AsObject, 2 * tail) < 1e-6 && taken < 2,
                        $"{r} of {n}: the P value of one side is {report["p_1_exact"].AsObject} and that of two is {report["p_2_exact"].AsObject}, for {tail} and twice that, in {taken:F2} seconds");
                }
            }
        }
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  a single proportion");

        // ---- paired proportions
        before = failures;
        {
            string refused = Refused(() => Paired(0, 0, 0, 0, 0.95));
            Say(refused != null && refused.Contains("must be at least 1"), $"no pairs: refused, and the refusal says why ({refused})");
            refused = Refused(() => Paired(10, 4, 4, 3, 0.95));
            Say(refused != null && refused.Contains("must be at least the sum"), $"4, 4 and 3 of 10 pairs: refused, and the refusal says why ({refused})");
            refused = Refused(() => Paired(10, 4, -1, 3, 0.95));
            Say(refused != null && refused.Contains("can not be below 0"), $"4, -1 and 3 of 10 pairs: refused, and the refusal says why ({refused})");

            // no pairs that differ: there is nothing against the null hypothesis
            ParameterBag report = Paired(10, 4, 0, 0, 0.95);
            List<ParameterBag> exact = Rows(report, "*exact");
            Say(exact.Count == 1 && First(exact[0], 0, ("cum_1", 1), ("cum_2", 1), ("cum_1_mid", 0.5), ("cum_2_mid", 1)) == null && report["prop_diff"].AsDouble == 0
                && report["lower"].AsDouble == -report["upper"].AsDouble && report["upper"].AsDouble > 0,
                "10 pairs of which none differ: the P values are 1, that of one side with the mid-P method is a half, and the limits are the same each side of 0");
            // one pair
            foreach (var (r, s, t, one) in new[] { (0, 1, 0, 0.5), (0, 0, 1, 0.5), (1, 0, 0, 1.0), (0, 0, 0, 1.0) })
            {
                report = Paired(1, r, s, t, 0.95);
                exact = Rows(report, "*exact");
                Say(exact.Count == 1 && First(exact[0], 0, ("cum_1", one), ("cum_2", 1), ("cum_1_mid", one / 2), ("cum_2_mid", one)) == null, $"one pair ({r}, {s}, {t}): the P value of one side is {one}");
            }
            // The pairs that differ all one way, which has the probability of a half to the power of their number
            foreach (int m in new[] { 12, 300, 1074, 1075, 5000 })
            {
                report = Paired(m + 10, 3, m, 0, 0.95);
                exact = Rows(report, "*exact");
                double p = Math.ScaleB(1.0, -m);
                Say(exact.Count == 1 && First(exact[0], 1e-12, ("cum_1", p), ("cum_2", 2 * p), ("cum_1_mid", p / 2), ("cum_2_mid", p)) == null && Unlike(report, Paired(m + 10, 3, 0, m, 0.95)) != null
                    && Unlike(exact[0], Rows(Paired(m + 10, 3, 0, m, 0.95), "*exact")[0]) == null,
                    $"{m} pairs that differ, all of them one way: the P value of one side is {p}, whichever way it is");
            }
            // More than 1,074 pairs that differ: the test is exact, and the report has no other.  Here by the definition, with the
            // probabilities from the logarithms of factorials
            foreach (var (n, r, s, t) in new[] { (3000, 700, 600, 500), (3000, 100, 1100, 1180), (2000000, 5, 1000000, 997600), (1200000, 400000, 800, 799200) })
            {
                double taken = Seconds(() => report = Paired(n, r, s, t, 0.95));
                exact = Rows(report, "*exact");
                double[] pr = Binomial(s + t, 0.5);
                int less = Math.Min(s, t);
                double one = Sum(pr.Take(less + 1)), mid = one - pr[less] / 2;
                string differs = exact.Count != 1 ? $"the report has {exact.Count} exact tests" : First(exact[0], 1e-8, ("cum_1", one), ("cum_2", Math.Min(1, 2 * one)), ("cum_1_mid", mid), ("cum_2_mid", Math.Min(1, 2 * mid)));
                Say(differs == null && !report.ContainsKey("*approx") && taken < 2, $"{s} and {t} pairs that differ: {differs}; {taken:F2} seconds");
            }
            // numbers that are not whole numbers
            Say(Unlike(Paired(50.2, 19.6, 12.4, 2.5, 0.95), Paired(50, 20, 12, 2, 0.95)) == null, "19.6, 12.4 and 2.5 of 50.2 pairs have the report of 20, 12 and 2 of 50");
            // the largest number of pairs that can be asked for
            refused = Refused(() => report = Paired(int.MaxValue, 1000, 1000000000, 999960000, 0.95));
            Say(refused == null && report["lower"].AsDouble != M && report["lower"].AsDouble < report["prop_diff"].AsDouble && report["prop_diff"].AsDouble < report["upper"].AsDouble,
                $"{int.MaxValue} pairs: the report has the limits of the difference ({refused})");
        }
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  paired proportions");

        // ---- two independent proportions
        before = failures;
        {
            string refused = Refused(() => Unpaired(10, 11, 10, 3, 0.95));
            Say(refused != null && refused.Contains("group 1 can not be more than its total"), $"11 of 10 in the first sample: refused, and the refusal says why ({refused})");
            refused = Refused(() => Unpaired(10, 3, 10, 11, 0.95));
            Say(refused != null && refused.Contains("group 2 can not be more than its total"), $"11 of 10 in the second sample: refused, and the refusal says why ({refused})");
            refused = Refused(() => Unpaired(10, -1, 10, 3, 0.95));
            Say(refused != null && refused.Contains("can not be below 0"), $"-1 of 10: refused, and the refusal says why ({refused})");
            Say(Refused(() => Unpaired(0, 0, 10, 3, 0.95)) != null && Refused(() => Unpaired(10, 3, 0, 0, 0.95)) != null, "a sample of 0 is refused");

            // One sample without responders and the other with none but responders: the difference is -1, which is the lower limit,
            // and the upper limit is where the statistic has the value of chi-square.  With the difference d the most likely
            // proportion of the second sample is n2 (1 - d) / (n1 + n2), or -d if that is more, as the derivative of the
            // likelihood (1 - p2 - d) ^ n1 p2 ^ n2 says
            foreach (var (n1, n2) in new[] { (1, 4), (4, 1), (1, 1), (3, 3), (10, 2), (2, 10), (100, 7), (7, 100), (1, 300), (300, 1), (2500, 2500) })
                foreach (double level in new[] { 0.95, 0.99 })
                {
                    double z = Deviate(level);
                    double Statistic(double d)
                    {
                        double p2 = Math.Min(1, Math.Max(-d, n2 * (1 - d) / (n1 + n2))), p1 = Math.Max(0, p2 + d);
                        double variance = (p1 * (1 - p1) / n1 + p2 * (1 - p2) / n2) * (n1 + n2) / (n1 + n2 - 1.0);
                        return 1 + d == 0 ? 0 : variance <= 0 ? double.PositiveInfinity : (1 + d) * (1 + d) / variance;
                    }
                    double limit = Root(d => Math.Min(Statistic(d), 1e300) - z * z, -1, 1);
                    ParameterBag report = Unpaired(n1, 0, n2, n2, level), mirror = Unpaired(n1, n1, n2, 0, level);
                    string differs = First(report, 1e-9, ("prop_diff", -1), ("from", -1), ("to", limit)) ?? First(mirror, 1e-9, ("prop_diff", 1), ("from", -limit), ("to", 1));
                    Say(differs == null, $"0 of {n1} against {n2} of {n2}, and {n1} of {n1} against 0 of {n2}, at the level {level}: {differs}");
                }
            // the case of the change log
            Say(Math.Abs(Unpaired(1, 0, 4, 4, 0.95)["to"].AsDouble + 0.020218) < 5e-7, $"0 of 1 against 4 of 4: the upper limit is {Unpaired(1, 0, 4, 4, 0.95)["to"].AsDouble}");

            // none responding in both samples, and all: no test, and limits each side of 0
            foreach (var (r1, r2) in new[] { (0, 0), (8, 11) })
            {
                ParameterBag report = Unpaired(8, r1, 11, r2, 0.95);
                Say(Rows(report, "*exact2").Count == 0 && Rows(report, "*approx2").Count == 0 && report["se"].AsDouble == M && report["z"].AsDouble == M
                    && report["prop_diff"].AsDouble == 0 && report["from"].AsDouble < 0 && report["to"].AsDouble > 0,
                    $"{r1} of 8 against {r2} of 11: the report has no test, and has limits each side of 0 ({report["from"].AsDouble} to {report["to"].AsDouble})");
            }

            // Large samples: the exact mid-P value is given, here against the definition
            foreach (var (n1, r1, n2, r2, tolerance) in new[] { (3000, 1000, 2500, 900, 1e-9), (40000, 12000, 50000, 15300, 1e-8), (2000000, 600000, 3000000, 901000, 1e-8), (3, 0, 1000000, 5, 1e-8), (2500000, 1249000, 2500000, 1251000, 1e-8) })
            {
                string differs = null;
                double taken = Seconds(() => { try { differs = UnpairedDiffers(n1, r1, n2, r2, 0.95, tolerance); } catch (Exception ex) { differs = Message(ex); } });
                Say(differs == null && taken < 3, $"{r1} of {n1} against {r2} of {n2}: {differs}; {taken:F2} seconds");
            }
            {
                List<ParameterBag> exact = Rows(Unpaired(3000, 1000, 2500, 900, 0.95), "*exact2");
                Say(exact.Count == 1 && Math.Abs(exact[0]["mp"].AsDouble - 0.0385174) < 5e-8, "1,000 of 3,000 against 900 of 2,500: the exact mid-P value is 0.0385174");
            }

            // numbers that are not whole numbers
            Say(Unlike(Unpaired(257.4, 41.3, 244.2, 64.4, 0.95), Unpaired(257, 41, 244, 64, 0.95)) == null, "41.3 of 257.4 against 64.4 of 244.2 has the report of 41 of 257 against 64 of 244");
            Say(Unlike(Unpaired(10.5, 2.5, 11.5, 6.5, 0.95), Unpaired(10, 2, 12, 6, 0.95)) == null, "2.5 of 10.5 against 6.5 of 11.5 has the report of 2 of 10 against 6 of 12");

            // the largest samples that can be asked for
            {
                ParameterBag report = null;
                refused = null;
                double taken = Seconds(() => refused = Refused(() => report = Unpaired(int.MaxValue, 1073741000, int.MaxValue, 1073700000, 0.95)));
                Say(refused == null && report["from"].AsDouble < report["prop_diff"].AsDouble && report["prop_diff"].AsDouble < report["to"].AsDouble && Rows(report, "*exact2").Count == 1 && taken < 3,
                    $"two samples of {int.MaxValue}: the report has the limits and the exact mid-P value ({refused}); {taken:F2} seconds");
                if (refused == null)
                {
                    // the difference is 1.25 standard errors: the mid-P value is that of the normal deviate, to a part in 100,000
                    double z = report["z"].AsDouble;
                    object mp = Rows(report, "*exact2").Count == 1 ? Rows(report, "*exact2")[0]["mp"].AsObject : "not given";
                    Say(Differs(mp, 2 * AboveNormal(z)) < 1e-5, $"and the mid-P value is {mp}, for {2 * AboveNormal(z)} of the normal deviate {z}");
                    double statistic = ScoreStatistic(report["from"].AsDouble, 1073741000, int.MaxValue, 1073700000, int.MaxValue), chi = Deviate(0.95) * Deviate(0.95);
                    Say(Math.Abs(statistic - chi) < 1e-4 * chi, $"and the statistic at the lower limit is {statistic}, for {chi}");
                }
            }
        }
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  two independent proportions");
    }
}
