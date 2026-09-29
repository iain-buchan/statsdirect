// The figures of the Rates menu worked out from their definitions, for cases drawn at random.  Nothing of the program is used to
// make what is expected, and the route is another than that of the benchmarks: a probability is a sum of the terms of the counts,
// each term made from the one next to it by the ratio of the two; a limit is found by halving a range until the sum that defines
// it has the probability that is wanted; the normal distribution is from the series and the continued fraction of the integral
// of its density; and the proportions that are most likely with a ratio are found by a search of the likelihood.
using StatsDirect.Templates;

// the terms of the counts from First, each over the greatest of them; a count that has no term here has a term below the least
// that was asked for
internal sealed class TermsOfCounts
{
    public int First;
    public double[] Over;
    public double Of(int count) => count < First || count >= First + Over.Length ? 0 : Over[count - First];

    // a sum of the terms from one count to another, the least of them first, with what is lost by each addition carried to the
    // next; the term of one count may be halved, and each term may have a weight
    public double Sum(int from, int to, int halved = -1, Func<int, double> weight = null)
    {
        double sum = 0, carried = 0;
        // the terms rise to the greatest and fall from it: from each end to the middle
        int a = Math.Max(from, First) - First, b = Math.Min(to, First + Over.Length - 1) - First, half = halved - First;
        while (a <= b)
        {
            int i = Over[a] <= Over[b] ? a++ : b--;
            double term = i == half ? 0.5 * Over[i] : Over[i];
            if (weight != null) term *= weight(i + First);
            term -= carried;
            double next = sum + term;
            carried = next - sum - term;
            sum = next;
        }
        return sum;
    }

    public double All => Sum(First, First + Over.Length - 1);
}

internal static partial class Program
{
    private const int NoEnd = int.MaxValue - 2;

    // The terms of the counts 0 to last over the greatest of them.  step(i) is the term of the count i + 1 over that of the count
    // i; it falls as the count rises, so that the terms rise to the greatest and fall from it.  The terms that are not above the
    // least that is asked for are left out.  No term is made that is below 1 part in 10^300 of the greatest: at the least number
    // that is held a term would stay where it is, whatever it is multiplied by.
    internal static TermsOfCounts TermsOver(int last, Func<int, double> step, double least)
    {
        // the first count from which the next term is the less
        int low = 0, high = last;
        while (low < high)
        {
            int mid = low + (high - low) / 2;
            if (step(mid) < 1) high = mid; else low = mid + 1;
        }
        List<double> above = new(), below = new();
        double t = 1;
        for (int i = low; i < last; i++)
        {
            t *= step(i);
            if (t <= least || t < 1e-300) break;
            above.Add(t);
        }
        t = 1;
        for (int i = low; i > 0; i--)
        {
            t /= step(i - 1);
            if (t <= least || t < 1e-300) break;
            below.Add(t);
        }
        double[] over = new double[below.Count + 1 + above.Count];
        for (int i = 0; i < below.Count; i++) over[below.Count - 1 - i] = below[i];
        over[below.Count] = 1;
        for (int i = 0; i < above.Count; i++) over[below.Count + 1 + i] = above[i];
        return new TermsOfCounts { First = low - below.Count, Over = over };
    }

    // the value, between two that are given, at which what is true below it becomes what is not: the range is halved until its ends
    // are next to each other
    internal static double Halve(double low, double high, Func<double, bool> below)
    {
        for (int i = 0; i < 300; i++)
        {
            double mid = 0.5 * (low + high);
            if (mid <= low || mid >= high) break;
            if (below(mid)) low = mid; else high = mid;
        }
        return 0.5 * (low + high);
    }

    // ---- the normal distribution
    // The probability above x.  Below 3 it is a half less the integral of the density from 0, which is the density times
    // x (1 + x^2 / 3 + x^4 / (3 * 5) + ...); from 3 it is the density over x + 1 / (x + 2 / (x + 3 / (x + ...)))
    internal static double NormalAbove(double x)
    {
        if (x < 0) return 1 - NormalAbove(-x);
        double density = Math.Exp(-0.5 * x * x) / Math.Sqrt(2 * Math.PI);
        if (x < 3)
        {
            double term = x, sum = x;
            for (int n = 1; n < 1000 && Math.Abs(term) > 1e-18 * Math.Abs(sum); n++) { term *= x * x / (2 * n + 1); sum += term; }
            return 0.5 - density * sum;
        }
        double f = x;
        for (int k = 300; k >= 1; k--) f = x + k / f;
        return density / f;
    }

    internal static double Deviate(double level) => Halve(0, 40, z => NormalAbove(z) > 0.5 * (1 - level));

    // ---- a Poisson count
    private static TermsOfCounts Poisson(double mean, double least) => TermsOver(NoEnd, i => mean / (i + 1.0), least);

    // the probabilities of as many events or more, and of as many or fewer, with a mean
    internal static (double orMore, double orFewer) PoissonTails(int events, double mean)
    {
        TermsOfCounts t = Poisson(mean, 0);
        double all = t.All;
        return (t.Sum(events, NoEnd) / all, t.Sum(0, events) / all);
    }

    // The limits of the mean of a Poisson count: the mean with which as many events or more have the probability alpha, and the
    // mean with which as many or fewer have it.  They are looked for within 40 standard deviations, and 40, of the count
    internal static (double lower, double upper) PoissonLimits(int events, double alpha)
    {
        double Tail(double mean, bool orMore)
        {
            TermsOfCounts t = Poisson(mean, 1e-40);
            return (orMore ? t.Sum(events, NoEnd) : t.Sum(0, events)) / t.All;
        }
        double spread = 40 * Math.Sqrt(events) + 40;
        double lower = events == 0 ? 0 : Halve(Math.Max(0, events - spread), events, m => Tail(m, true) < alpha);
        double upper = Halve(events, events + spread, m => Tail(m, false) > alpha);
        return (lower, upper);
    }

    // ---- a count of a total, with a proportion
    // the limits of Clopper and Pearson of a proportion, r of n: the proportion with which r or more have the probability alpha,
    // and the proportion with which r or fewer have it
    internal static (double lower, double upper) ProportionLimits(int r, int n, double alpha)
    {
        double Tail(double logOdds, bool orMore)
        {
            double odds = Math.Exp(logOdds);
            TermsOfCounts t = TermsOver(n, i => (n - i) / (i + 1.0) * odds, 1e-40);
            return (orMore ? t.Sum(r, n) : t.Sum(0, r)) / t.All;
        }
        double Proportion(double logOdds) => 1 / (1 + Math.Exp(-logOdds));
        double lower = r == 0 ? 0 : Proportion(Halve(-60, 60, x => Tail(x, true) < alpha));
        double upper = r == n ? 1 : Proportion(Halve(-60, 60, x => Tail(x, false) > alpha));
        return (lower, upper);
    }

    // The conditional analysis of two rates, a and b events in the person-times pt1 and pt2: with the total of the events given,
    // the events of the first group are a count of that total, with the odds ratio * pt1 / pt2.  The nine figures: the
    // estimate, Fisher's limits and the mid-P limits, Fisher's P values and the mid-P values
    internal static double[] Conditional(int a, int b, double pt1, double pt2, double level)
    {
        int m = a + b;
        double alpha = 0.5 * (1.0 - level);
        TermsOfCounts With(double logRatio, double least)
        {
            double odds = Math.Exp(logRatio) * pt1 / pt2;
            return TermsOver(m, i => (m - i) / (i + 1.0) * odds, least);
        }
        double Ratio(Func<TermsOfCounts, bool> below) => Math.Exp(Halve(-60, 60, x => below(With(x, 1e-40))));
        // the estimate: the ratio with which the mean of the count is the observed count
        double estimate = a == 0 ? 0 : b == 0 ? double.PositiveInfinity : Ratio(t => t.Sum(0, m, -1, i => i - a) < 0);
        double Limit(bool lower, bool halved) => lower && a == 0 ? 0 : !lower && b == 0 ? double.PositiveInfinity
            : Ratio(t => (lower ? t.Sum(a, m, halved ? a : -1) : t.Sum(0, a, halved ? a : -1)) < alpha * t.All == lower);
        TermsOfCounts none = With(0, 0);
        double all = none.All;
        double up = none.Sum(a, m) / all, down = none.Sum(0, a) / all;
        double noMore = none.Of(a) * (1.0 + 1e-7);
        double two = none.Sum(0, m, -1, i => none.Of(i) <= noMore ? 1 : 0) / all;
        double mid = Math.Min(none.Sum(a, m, a), none.Sum(0, a, a)) / all;
        return new[] { estimate, Limit(true, false), Limit(false, false), Limit(true, true), Limit(false, true), Math.Min(up, down), Math.Min(1, two), mid, Math.Min(1, 2 * mid) };
    }

    // ---- the ratio of two proportions
    // The score statistic of the ratio of x1 of n1 to x0 of n0, at a ratio: the first proportion less the ratio times the second,
    // over the standard error that the difference has with the proportions that are most likely with that ratio.  The second of
    // those is found by halving its range until the slope of the logarithm of the likelihood is 0; the first is the ratio times
    // it.  A proportion of 1 is at the end of the range, where the likelihood has no slope to be 0
    internal static double Score(double x1, double n1, double x0, double n0, double ratio)
    {
        double top = Math.Min(1, 1 / ratio);
        double Slope(double p) => (x1 + x0) / p - (n1 == x1 ? 0 : ratio * (n1 - x1) / (1 - ratio * p)) - (n0 == x0 ? 0 : (n0 - x0) / (1 - p));
        double p0 = Halve(0, top, p => Slope(p) > 0);
        double p1 = Math.Min(1, ratio * p0);
        double variance = p1 * (1 - p1) / n1 + ratio * ratio * p0 * (1 - p0) / n0;
        return (x1 / n1 - ratio * x0 / n0) / Math.Sqrt(variance);
    }

    // the limits of the ratio: the ratios at which the score statistic is the normal deviate of the confidence level, above and
    // below; the statistic falls as the ratio rises
    internal static (double lower, double upper) ScoreLimits(double x1, double n1, double x0, double n0, double z)
    {
        double lower = x1 == 0 ? 0 : Math.Exp(Halve(-60, 60, x => Score(x1, n1, x0, n0, Math.Exp(x)) > z));
        double upper = x0 == 0 ? double.PositiveInfinity : Math.Exp(Halve(-60, 60, x => Score(x1, n1, x0, n0, Math.Exp(x)) > -z));
        return (lower, upper);
    }

    // ---- what differs
    // the first of the figures of a report that is not what is expected of it, or nothing; a probability below 1e-280, of which the
    // terms here are not made, may be given as any number below it
    internal static string First(ParameterBag report, double tolerance, params (string name, double expected)[] figures)
    {
        foreach (var (name, expected) in figures)
        {
            if (!report.ContainsKey(name)) return $"{name} is not given";
            object given = report[name].AsObject;
            if (given is long whole) given = (double)whole;
            if (name.StartsWith("p") && name != "pc" && expected >= 0 && expected < 1e-280 && given is double small && small >= 0 && small < 1e-280) continue;
            double by = Differs(given, expected);
            if (by > tolerance) return $"{name} is {given}, and is to be {(expected == M ? "missing" : expected.ToString("R", inv))}";
        }
        return null;
    }

    private static void Definitions()
    {
        Console.WriteLine();
        Console.WriteLine("Cases drawn at random, against the definitions");
        Random random = new(20260929);
        double[] levels = { 0.9, 0.95, 0.99 };
        const double tolerance = 1e-8;

        // what is expected is first looked at itself: the normal distribution at values that are known, and the Poisson limits
        // put back into the sums that define them
        {
            int before = failures;
            Say(Math.Abs(NormalAbove(1.959963984540054) / 0.025 - 1) < 1e-13 && Math.Abs(NormalAbove(2.5758293035489) / 0.005 - 1) < 1e-12 && Math.Abs(NormalAbove(0) - 0.5) < 1e-16,
                $"the probability above 1.959963984540054 is {NormalAbove(1.959963984540054):R}, and above 2.5758293035489 it is {NormalAbove(2.5758293035489):R}");
            Say(Math.Abs(NormalAbove(3) / NormalAbove(2.9999999999) - 1) < 1e-9 && Math.Abs(NormalAbove(10) / 7.619853024160527e-24 - 1) < 1e-12,
                $"the series and the continued fraction meet at 3 ({NormalAbove(2.9999999999):R} and {NormalAbove(3):R}); above 10 the probability is {NormalAbove(10):R}");
            Say(Math.Abs(Deviate(0.95) - 1.959963984540054) < 1e-13, $"the normal deviate of the level 0.95 is {Deviate(0.95):R}");
            // 10 events: the limits of the mean at the level 0.95 are 4.795389 and 18.390356, and 0 events have the upper limit
            // -log(0.025)
            var (lower, upper) = PoissonLimits(10, 0.025);
            Say(Math.Abs(lower - 4.795389) < 1e-6 && Math.Abs(upper - 18.390356) < 1e-6 && Math.Abs(PoissonLimits(0, 0.025).upper + Math.Log(0.025)) < 1e-12,
                $"the limits of the mean of 10 events are {lower} and {upper}, and the upper limit of no events is {PoissonLimits(0, 0.025).upper:R}");
            var (orMore, orFewer) = PoissonTails(3, 2);
            double three = Math.Exp(-2) * 8 / 6;
            Say(Math.Abs(orFewer - Math.Exp(-2) * (1 + 2 + 2 + 8.0 / 6)) < 1e-15 && Math.Abs(orMore + orFewer - 1 - three) < 1e-15, $"3 events or fewer with the mean 2 have the probability {orFewer:R}, and 3 or more {orMore:R}");
            // 3 of 10: the limits of Clopper and Pearson at the level 0.95 are 0.066739 and 0.652453
            var (least, most) = ProportionLimits(3, 10, 0.025);
            Say(Math.Abs(least - 0.0667395) < 1e-6 && Math.Abs(most - 0.6524529) < 1e-6, $"the limits of 3 of 10 are {least} and {most}");
            Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  what is expected: the normal distribution, the limits of a Poisson mean and of a proportion");
        }

        // ---- two crude rates
        {
            int before = failures, cases = 0, without = 0;
            for (int n = 0; n < 300; n++)
            {
                int size = new[] { 6, 40, 300, 3000 }[random.Next(4)];
                int a = random.Next(size + 1), b = random.Next(size + 1);
                if (n % 9 == 0) a = 0;
                if (n % 11 == 0) b = 0;
                if (a + b == 0) a = 1;
                if (a == 0 || b == 0) without++;
                double pt1 = Math.Round((0.5 + 20 * random.NextDouble()) * Math.Max(a, 1) * new[] { 0.01, 1, 100, 10000 }[random.Next(4)], 3);
                double pt2 = Math.Round(pt1 * (0.2 + 4.8 * random.NextDouble()), 3);
                if (n % 13 == 0 && a > 0 && b > 0) pt2 = pt1 * b / a;   // the two rates the same, as nearly as the numbers can be
                double level = levels[random.Next(3)];
                double z = Deviate(level);
                double m = a + b, pt = pt1 + pt2, share = pt1 / pt;
                // the events of the first group, of all the events, against what the person-time expects of them
                double chi = (a - m * share) * (a - m * share) / (m * share * (1 - share));
                double difference = a / pt1 - b / pt2;
                // each rate has the variance of a Poisson count over its person-time
                double error = Math.Sqrt(a / pt1 / pt1 + b / pt2 / pt2);
                double[] nine = Conditional(a, b, pt1, pt2, level);
                string differs;
                try
                {
                    ParameterBag report = Two(a, b, pt1, pt2, level, true);
                    List<ParameterBag> exact = Rows(report, "*exact");
                    differs = First(report, tolerance, ("ir1", a / pt1), ("ir2", b / pt2), ("ird", difference), ("ird_from", difference - z * error), ("ird_to", difference + z * error),
                            ("p", 2 * NormalAbove(Math.Sqrt(chi))), ("irr", b == 0 ? double.PositiveInfinity : a / pt1 / (b / pt2)), ("irr_from", nine[1]), ("irr_to", nine[2]), ("pc", 100 * level), ("faults", 0))
                        // a chi-square of two rates that differ by rounding only is from a difference of nearly nothing
                        ?? (Math.Abs(report["xmh"].AsDouble - chi) <= tolerance * chi + 1e-20 ? null : $"xmh is {report["xmh"].AsDouble:R}, and is to be {chi:R}")
                        ?? (exact.Count != 1 ? "the report has no conditional analysis"
                            : First(exact[0], tolerance, ("eor", nine[0]), ("llf", nine[1]), ("ulf", nine[2]), ("llm", nine[3]), ("ulm", nine[4]), ("p1f", nine[5]), ("p2f", nine[6]), ("p1m", nine[7]), ("p2m", nine[8])));
                }
                catch (Exception ex) { differs = Message(ex); }
                cases++;
                if (differs != null) Say(false, $"{a} and {b} events in the person-times {pt1.ToString(inv)} and {pt2.ToString(inv)} at the level {level.ToString(inv)}: {differs}");
            }
            Say(failures == before, $"{cases} pairs of rates");
            Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  two crude rates: {cases} pairs, of which {without} have a group without events");
        }

        // ---- indirect standardization
        {
            int before = failures, cases = 0;
            for (int n = 0; n < 300; n++)
            {
                int k = 1 + random.Next(15);
                double multiplier = new[] { 1.0, 100, 1000, 100000, 1000000 }[random.Next(5)];
                double[] rates = new double[k], times = new double[k];
                double most = new[] { 50.0, 3000, 1e6 }[random.Next(3)];
                for (int i = 0; i < k; i++)
                {
                    rates[i] = Math.Round((0.00001 + 0.02 * random.NextDouble()) * multiplier, 6);
                    times[i] = Math.Round(1 + most * random.NextDouble(), 1);
                }
                double expected = 0;
                for (int i = 0; i < k; i++) expected += rates[i] / multiplier * times[i];
                int deaths = n % 7 == 0 ? 0 : Math.Min(40000, (int)Math.Round(expected * (0.2 + 2.8 * random.NextDouble())) + random.Next(4));
                double level = levels[random.Next(3)];
                var (lower, upper) = PoissonLimits(deaths, 0.5 * (1 - level));
                var (orMore, orFewer) = PoissonTails(deaths, expected);
                string differs;
                try
                {
                    ParameterBag report = Smr(level, multiplier.ToString(inv), deaths, rates, times, n % 2 == 1);
                    List<ParameterBag> groups = Rows(report, "*groups");
                    differs = First(report, tolerance, ("total", expected), ("ratio", deaths / expected), ("smr", Math.Floor(100 * deaths / expected + 0.5)), ("pc", 100 * level),
                        ("from", lower / expected), ("to", upper / expected), ("from100", Math.Round(100 * lower / expected)), ("to100", Math.Round(100 * upper / expected)), ("qty", deaths), ("p_hi", orMore), ("p_lo", orFewer));
                    if (differs == null && groups.Count != k) differs = $"the report has {groups.Count} strata";
                    for (int i = 0; i < k && differs == null; i++)
                        differs = First(groups[i], tolerance, ("group", rates[i] / multiplier), ("observed", times[i]), ("expected", rates[i] / multiplier * times[i]));
                }
                catch (Exception ex) { differs = Message(ex); }
                cases++;
                if (differs != null) Say(false, $"{deaths} deaths where {expected.ToString(inv)} are expected, in {k} strata, at the level {level.ToString(inv)}: {differs}");
            }
            Say(failures == before, $"{cases} standardized mortality ratios");
            Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  indirect standardization: {cases} sets of strata, from two columns and from the grid of the screen form in turn");
        }

        // ---- direct standardization
        {
            int before = failures, cases = 0, above = 0;
            for (int n = 0; n < 200; n++)
            {
                int k = 1 + random.Next(12);
                double multiplier = new[] { 1.0, 1000, 100000 }[random.Next(3)];
                double[] events = new double[k], times = new double[k], reference = new double[k];
                double most = new[] { 200.0, 5000, 100000 }[random.Next(3)], top = new[] { 0.002, 0.05, 0.6 }[random.Next(3)];
                double all = 0, time = 0, size = 0;
                // whether every stratum has a rate that a proportion can have
                bool proportions = true;
                for (int i = 0; i < k; i++)
                {
                    times[i] = Math.Round(20 + most * random.NextDouble());
                    events[i] = n % 8 == 0 ? 0 : Math.Min(times[i], Math.Floor(times[i] * top * random.NextDouble() + random.NextDouble()));
                    if (n % 17 == 0 && i == 0) events[i] = times[i];
                    // more events than person-time in the last stratum: 1 more, or more than twice as many
                    if (n % 9 == 4 && i == k - 1) events[i] = n % 2 == 0 ? times[i] + 1 : 2 * times[i] + 3;
                    if (events[i] > times[i]) proportions = false;
                    reference[i] = n % 3 == 0 ? Math.Round(random.NextDouble(), 4) : Math.Round(100 + 1e6 * random.NextDouble());
                    if (n % 10 == 0 && k > 1 && i == 1) reference[i] = 0;
                    all += events[i]; time += times[i]; size += reference[i];
                }
                if (!proportions) above++;
                if (size <= 0) reference[0] = 1;
                size = reference.Sum();
                double level = levels[random.Next(3)];
                double z = Deviate(level), alpha = 0.5 * (1 - level);
                // the rate of the reference population if its strata had the rates of the index population, and its variance
                // with each rate that of a Poisson count over its person-time, or that of a proportion
                double rate = 0, small = 0, any = 0;
                for (int i = 0; i < k; i++)
                {
                    double r = events[i] / times[i], w = reference[i] / size;
                    rate += w * r;
                    small += w * w * events[i] / times[i] / times[i];
                    any += w * w * r * (1 - r) / times[i];
                }
                string differs;
                try
                {
                    ParameterBag report = Direct(level, multiplier.ToString(inv), events, times, reference, n % 2 == 1);
                    List<ParameterBag> inputs = Rows(report, "*inputs"), limits = Rows(report, "*cis");
                    // with more events than person-time in a stratum the binomial model has no figures, and the report has a note
                    differs = First(report, tolerance, ("pc", 100 * level), ("events", all), ("stde", rate * time), ("crude", multiplier * all / time), ("stdr", multiplier * rate),
                        ("ser_any", proportions ? multiplier * Math.Sqrt(any) : M), ("from_any", proportions ? multiplier * (rate - z * Math.Sqrt(any)) : M), ("to_any", proportions ? multiplier * (rate + z * Math.Sqrt(any)) : M),
                        ("ser_small", multiplier * Math.Sqrt(small)), ("from_small", multiplier * (rate - z * Math.Sqrt(small))), ("to_small", multiplier * (rate + z * Math.Sqrt(small))));
                    if (differs == null && Rows(report, "*note").Count != (proportions ? 0 : 1)) differs = $"the report has {Rows(report, "*note").Count} notes";
                    if (differs == null)
                    {
                        // Dobson: the limits of the count of all the events, put on the scale of the rate
                        if (all > 0)
                        {
                            var (lower, upper) = PoissonLimits((int)all, alpha);
                            differs = First(report, tolerance, ("from_dobson", multiplier * (rate + Math.Sqrt(small / all) * (lower - all))), ("to_dobson", multiplier * (rate + Math.Sqrt(small / all) * (upper - all))));
                        }
                        else differs = First(report, tolerance, ("from_dobson", M), ("to_dobson", M));
                    }
                    if (differs == null && (inputs.Count != k || limits.Count != k)) differs = $"the report has {inputs.Count} and {limits.Count} strata";
                    for (int i = 0; i < k && differs == null; i++)
                    {
                        var (lower, upper) = PoissonLimits((int)events[i], alpha);
                        differs = First(inputs[i], tolerance, ("idxy", events[i]), ("idxn", times[i]), ("idxr", multiplier * events[i] / times[i]), ("refn", reference[i]), ("refw", reference[i] / size))
                            ?? First(limits[i], tolerance, ("idxr", multiplier * events[i] / times[i]), ("from", multiplier * lower / times[i]), ("to", multiplier * upper / times[i]));
                    }
                }
                catch (Exception ex) { differs = Message(ex); }
                cases++;
                if (differs != null) Say(false, $"direct standardization of {all} events in {k} strata at the level {level.ToString(inv)} (case {n}): {differs}");
            }
            Say(failures == before, $"{cases} directly standardized rates");
            Say(above >= 20, $"{above} of them have a stratum with more events than person-time");
            Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  direct standardization: {cases} sets of strata, from three columns and from the grid of the screen form in turn; {above} have a stratum with more events than person-time");
        }

        // ---- two populations standardized and compared
        {
            int before = failures, cases = 0, infinite = 0;
            for (int n = 0; n < 300; n++)
            {
                bool poisson = n % 2 == 0;
                int k = 1 + random.Next(10);
                double multiplier = new[] { 1.0, 1000, 100000 }[random.Next(3)];
                double[] a = new double[k], pt1 = new double[k], b = new double[k], pt2 = new double[k], reference = new double[k];
                double most = new[] { 300.0, 5000, 100000 }[random.Next(3)], top = new[] { 0.003, 0.05, 0.5 }[random.Next(3)];
                for (int i = 0; i < k; i++)
                {
                    // the binomial model is of subjects: whole numbers of them
                    pt1[i] = poisson ? Math.Round(20 + most * random.NextDouble(), 1) : Math.Round(20 + most * random.NextDouble());
                    pt2[i] = poisson ? Math.Round(pt1[i] * (0.3 + 2.7 * random.NextDouble()), 1) : Math.Round(pt1[i] * (0.3 + 2.7 * random.NextDouble()));
                    a[i] = Math.Min(Math.Floor(pt1[i]), Math.Floor(pt1[i] * top * random.NextDouble() + random.NextDouble()));
                    b[i] = Math.Min(Math.Floor(pt2[i]), Math.Floor(pt2[i] * top * random.NextDouble() + random.NextDouble()));
                    reference[i] = n % 3 == 0 ? Math.Round(0.0001 + random.NextDouble(), 4) : Math.Round(100 + 1e6 * random.NextDouble());
                }
                if (n % 6 == 0) a[random.Next(k)] = 0;
                if (n % 7 == 0) b[random.Next(k)] = 0;
                if (n % 23 == 0) Array.Clear(a);
                if (n % 29 == 0) Array.Clear(b);
                if (n % 10 == 0 && k > 1) reference[random.Next(k)] = 0;
                double size = reference.Sum();
                double level = levels[random.Next(3)];
                double z = Deviate(level), alpha = 0.5 * (1 - level);
                // the ratio of two rates and its limits: nothing if neither population has an event
                (double ratio, double lower, double upper) Ratio(double x, double t1, double y, double t2)
                {
                    if (x + y <= 0) return (M, M, M);
                    double ratio = y == 0 ? double.PositiveInfinity : x / t1 / (y / t2);
                    if (poisson)
                    {
                        double[] nine = Conditional((int)x, (int)y, t1, t2, level);
                        return (ratio, nine[1], nine[2]);
                    }
                    var (lower, upper) = ScoreLimits(x, t1, y, t2, z);
                    return (ratio, lower, upper);
                }
                // a standardized rate and its variance
                (double rate, double variance) Rate(double[] x, double[] t)
                {
                    double rate = 0, variance = 0;
                    for (int i = 0; i < k; i++)
                    {
                        double r = x[i] / t[i], w = reference[i] / size;
                        rate += w * r;
                        variance += poisson ? w * w * x[i] / t[i] / t[i] : w * w * r * (1 - r) / t[i];
                    }
                    return (rate, variance);
                }
                string differs;
                try
                {
                    ParameterBag report = Standardized(level, multiplier.ToString(inv), poisson ? "poisson" : "binomial", a, pt1, b, pt2, reference);
                    List<ParameterBag> strata = Rows(report, "*strata"), ratios = Rows(report, "*rates");
                    differs = strata.Count != k || ratios.Count != k + 1 ? $"the report has {strata.Count} strata and {ratios.Count} ratios" : null;
                    for (int i = 0; i <= k && differs == null; i++)
                    {
                        var (ratio, lower, upper) = i < k ? Ratio(a[i], pt1[i], b[i], pt2[i]) : Ratio(a.Sum(), pt1.Sum(), b.Sum(), pt2.Sum());
                        if (double.IsPositiveInfinity(ratio)) infinite++;
                        differs = First(ratios[i], tolerance, ("rr", ratio), ("lci", lower), ("uci", upper), ("wt", i < k ? reference[i] / size : 1));
                        if (differs != null) differs = $"row {i + 1} of the ratios: {differs}";
                    }
                    if (differs == null)
                    {
                        double x = a.Sum(), t1 = pt1.Sum(), y = b.Sum(), t2 = pt2.Sum();
                        (double lower, double upper) first, second;
                        if (poisson)
                        {
                            first = PoissonLimits((int)x, alpha); first = (first.lower / t1, first.upper / t1);
                            second = PoissonLimits((int)y, alpha); second = (second.lower / t2, second.upper / t2);
                        }
                        else { first = ProportionLimits((int)x, (int)t1, alpha); second = ProportionLimits((int)y, (int)t2, alpha); }
                        var (e, ve) = Rate(a, pt1);
                        var (u, vu) = Rate(b, pt2);
                        double ratio = e > 0 && u > 0 ? e / u : u > 0 ? 0 : e > 0 ? double.PositiveInfinity : M;
                        double error = e > 0 && u > 0 ? Math.Sqrt(ve / e / e + vu / u / u) : 0;
                        differs = First(report, tolerance, ("pc", 100 * level), ("cre", multiplier * x / t1), ("cre_from", multiplier * first.lower), ("cre_to", multiplier * first.upper),
                            ("crne", multiplier * y / t2), ("crne_from", multiplier * second.lower), ("crne_to", multiplier * second.upper),
                            ("sre", multiplier * e), ("sre_from", multiplier * (e - z * Math.Sqrt(ve))), ("sre_to", multiplier * (e + z * Math.Sqrt(ve))),
                            ("srne", multiplier * u), ("srne_from", multiplier * (u - z * Math.Sqrt(vu))), ("srne_to", multiplier * (u + z * Math.Sqrt(vu))),
                            ("srr", ratio), ("srr_from", e > 0 && u > 0 ? Math.Exp(Math.Log(ratio) - z * error) : M), ("srr_to", e > 0 && u > 0 ? Math.Exp(Math.Log(ratio) + z * error) : M));
                    }
                }
                catch (Exception ex) { differs = Message(ex); }
                cases++;
                if (differs != null) Say(false, $"two populations of {k} strata with the {(poisson ? "Poisson" : "binomial")} model at the level {level.ToString(inv)} (case {n}): {differs}");
            }
            Say(failures == before, $"{cases} pairs of populations");
            Say(infinite >= 20, $"{infinite} of the ratios are infinite");
            Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  two populations standardized and compared: {cases} pairs, half with each model; {infinite} ratios are infinite");
        }

        // ---- the confidence interval of a rate
        {
            int before = failures, cases = 0;
            for (int n = 0; n < 200; n++)
            {
                int events = n % 10 == 0 ? 0 : random.Next(new[] { 10, 200, 5000, 100000 }[random.Next(4)]);
                double time = Math.Round(0.5 + 1e5 * random.NextDouble() * random.NextDouble(), 2);
                double level = levels[random.Next(3)];
                var (lower, upper) = PoissonLimits(events, 0.5 * (1 - level));
                string differs;
                try
                {
                    ParameterBag report = Rate(events, time, level);
                    // the figures of the report are looked for by what they are, whatever their names
                    Dictionary<string, object> figures = new();
                    Collect("", report, figures);
                    bool Has(double expected) => figures.Values.Any(v => Differs(v, expected) <= tolerance);
                    differs = !Has(events / time) ? $"the rate {events / time} is not in the report" : !Has(lower / time) ? $"the lower limit {lower / time} is not in the report" : !Has(upper / time) ? $"the upper limit {upper / time} is not in the report" : null;
                }
                catch (Exception ex) { differs = Message(ex); }
                cases++;
                if (differs != null) Say(false, $"{events} events in the time {time.ToString(inv)} at the level {level.ToString(inv)}: {differs}");
            }
            Say(failures == before, $"{cases} rates");
            Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  the confidence interval of a rate: {cases} rates");
        }
    }
}
