// The exact limits and P values of an odds ratio and of a rate ratio with hundreds of thousands of subjects or of events.  The
// method then works on the scale of logarithms, and the logarithms of its coefficients are numbers of hundreds of thousands and
// more: a sum that is made a term at a time, each step giving the logarithm of the sum so far, loses at each of its steps what a
// number of that size is held to.  What is expected is worked out here from the terms over the greatest of them: the greatest term
// is 1, and each other term is made from the one next to it, on the side of the greatest, by the ratio of the two, so that no
// number is large and nothing is taken from the logarithm of a large number.
using StatsDirect.Builtins;
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
}

internal static partial class Program
{
    // The terms of the counts 0 to last over the greatest of them, with a ratio: a term is the number of ways of its count times
    // the ratio to the power of the count.  step(i) is the number of ways of the count i + 1 over that of the count i; it falls
    // as the count rises, so that the terms rise to the greatest and fall from it.  The terms below the least that is asked for
    // are left out.  No term is made that is below 1 part in 10^300 of the greatest: at the least number that is held a term
    // would stay where it is, whatever it is multiplied by.
    private static TermsOfCounts TermsOver(int last, Func<int, double> step, double ratio, double least)
    {
        // the first count from which the next term is the less
        int low = 0, high = last;
        while (low < high)
        {
            int mid = low + (high - low) / 2;
            if (step(mid) * ratio < 1) high = mid; else low = mid + 1;
        }
        List<double> above = new(), below = new();
        double t = 1;
        for (int i = low; i < last; i++)
        {
            t *= step(i) * ratio;
            if (t <= least || t < 1e-300) break;
            above.Add(t);
        }
        t = 1;
        for (int i = low; i > 0; i--)
        {
            t /= step(i - 1) * ratio;
            if (t <= least || t < 1e-300) break;
            below.Add(t);
        }
        double[] over = new double[below.Count + 1 + above.Count];
        for (int i = 0; i < below.Count; i++) over[below.Count - 1 - i] = below[i];
        over[below.Count] = 1;
        for (int i = 0; i < above.Count; i++) over[below.Count + 1 + i] = above[i];
        return new TermsOfCounts { First = low - below.Count, Over = over };
    }

    // The nine figures: the estimate, Fisher's limits and the mid-P limits, Fisher's P values and the mid-P values
    private static double[] ExpectedOfMany(int last, int observed, Func<int, double> step, double level)
    {
        double alpha = 0.5 * (1.0 - level);
        // The ratio at which what is below it becomes what is not: a range of logarithms of the ratio is halved until its ends
        // are next to each other.  A term that is less than 1 part in 10^40 of the greatest adds nothing to a sum that is a
        // part of the whole
        double Halve(Func<TermsOfCounts, bool> below)
        {
            double low = -60, high = 60;
            for (int i = 0; i < 200; i++)
            {
                double mid = 0.5 * (low + high);
                if (mid <= low || mid >= high) break;
                if (below(TermsOver(last, step, Math.Exp(mid), 1e-40))) low = mid; else high = mid;
            }
            return Math.Exp(0.5 * (low + high));
        }
        // the estimate: the ratio with which the mean of the count is the observed count
        double estimate = observed == 0 ? 0 : observed == last ? double.PositiveInfinity
            : Halve(t => t.Sum(0, last, -1, i => i - observed) < 0);
        // a lower limit is the ratio with which the observed count or more has the probability alpha, and that tail rises with the
        // ratio; an upper limit is the ratio with which the observed count or fewer has it, and that tail falls
        double Limit(bool lower, bool halved) => lower && observed == 0 ? 0 : !lower && observed == last ? double.PositiveInfinity
            : Halve(t => (lower ? t.Sum(observed, last, halved ? observed : -1) : t.Sum(0, observed, halved ? observed : -1)) < alpha * t.Sum(0, last) == lower);
        // the P values are of the ratio 1, and may be of a tail far from the greatest term: every term that can be held is made
        TermsOfCounts none = TermsOver(last, step, 1, 0);
        double all = none.Sum(0, last);
        double up = none.Sum(observed, last) / all, down = none.Sum(0, observed) / all;
        double noMore = none.Of(observed) * (1.0 + 1e-7);
        double two = none.Sum(0, last, -1, i => none.Of(i) <= noMore ? 1 : 0) / all;
        double mid = Math.Min(none.Sum(observed, last, observed), none.Sum(0, observed, observed)) / all;
        return new[] { estimate, Limit(true, false), Limit(false, false), Limit(true, true), Limit(false, true), Math.Min(up, down), Math.Min(1, two), mid, Math.Min(1, 2 * mid) };
    }

    private static void Many()
    {
        Console.WriteLine();
        Console.WriteLine("Hundreds of thousands of subjects or of events");
        int before = failures, tables = 0, onLogarithms = 0, toBeOnLogarithms = 0;
        double longest = 0;

        // what is expected is first put beside the numbers of 60 figures, for tables that those can be made for
        foreach (var (rate, a, b, c, d) in new[] { (false, 30, 220, 370, 3380), (false, 1, 1099, 1099, 42801), (true, 20, 1080, 40, 1), (true, 640, 460, 1, 9), (false, 700, 400, 400, 8400) })
        {
            var (least, coefficients) = Coefficients(rate, a, b, c, d);
            double[] exact = Expected(coefficients, a - least, 0.95);
            int row = a + b, first = a + c, second = b + d;
            double[] here = rate
                ? ExpectedOfMany(row, a, i => (double)(row - i) / (i + 1) * c / d, 0.95)
                : ExpectedOfMany(coefficients.Length - 1, a - least, i => (double)(first - least - i) * (row - least - i) / ((double)(least + i + 1) * (second - row + least + i + 1)), 0.95);
            string differs = null;
            for (int i = 0; i < 9 && differs == null; i++)
                if (!(here[i] == exact[i] || Math.Abs(here[i] - exact[i]) <= 1e-12 * Math.Abs(exact[i]) || i >= 5 && exact[i] < 1e-280 && here[i] < 1e-280))
                    differs = $"{nine[i]} is {here[i]:R} from the terms over the greatest of them and {exact[i]:R} in numbers of 60 figures";
            Say(differs == null, $"what is expected of {a} {b} / {c} {d}: {differs}");
        }

        void One(bool rate, int a, int b, double c, double d, double level)
        {
            string differs = null;
            bool logarithms = false; double seconds = 0;
            int last = 0;
            try
            {
                ExactBB.Rec2X2[] t = new ExactBB.Rec2X2[1];
                t[0].Freq = 1; t[0].A = a; t[0].M1 = a + b;
                if (rate) { t[0].N1 = c; t[0].N0 = d; } else { t[0].N1 = a + c; t[0].N0 = b + d; }
                t[0].IsInformative = true;
                System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
                new ExactBB().Exact22K(none, 0, 1, rate ? ExactBB.Exact22KDataType.Type3 : ExactBB.Exact22KDataType.Type1, t, level, out double e, out double uf, out double lf, out double um, out double lm,
                    out double p1, out double p2, out double m1, out double m2, ref logarithms, out int fault);
                seconds = clock.Elapsed.TotalSeconds;
                double[] given = { e, lf, uf, lm, um, p1, p2, m1, m2 };
                int row = a + b;
                double first = a + c, second = b + d;
                int least = rate ? 0 : (int)Math.Max(0, row - second);
                last = rate ? row : (int)Math.Min(row, first) - least;
                double[] expected = rate
                    ? ExpectedOfMany(last, a, i => (double)(row - i) / (i + 1) * (c / d), level)
                    : ExpectedOfMany(last, a - least, i => (first - least - i) * (row - least - i) / ((least + i + 1.0) * (second - row + least + i + 1.0)), level);
                if (fault != 0) differs = $"the method has the fault {fault}";
                // The estimate and the limits are to have 9 figures.  A probability is from the logarithms of the coefficients,
                // which are numbers of hundreds of thousands and are made one from another: it is to have 7 figures
                for (int i = 0; i < 9 && differs == null; i++)
                    if (!(given[i] == expected[i] || given[i] != M && Math.Abs(given[i] - expected[i]) <= (i < 5 ? 1e-9 : 1e-7) * Math.Abs(expected[i]) || i >= 5 && expected[i] < 1e-280 && given[i] >= 0 && given[i] < 1e-280))
                        differs = $"{nine[i]} is {(given[i] == M ? "missing" : given[i].ToString("R", inv))}, and is to be {expected[i].ToString("R", inv)}";
            }
            catch (Exception ex) { differs = Message(ex); }
            tables++;
            if (logarithms) onLogarithms++;
            if (last >= 100000 && !logarithms) toBeOnLogarithms++;
            if (seconds > longest) longest = seconds;
            Say(differs == null, $"{(rate ? "the rate ratio of the events" : "the odds ratio of the table")} {a} {b} / {c.ToString(inv)} {d.ToString(inv)} at the level {level.ToString(inv)}: {differs}");
        }
        // events in person-times
        One(true, 400000, 380000, 1e9, 1e9, 0.99);
        One(true, 115490, 180044, 162984000, 420144000, 0.95);
        One(true, 250000, 250500, 2.5e8, 2.5e8, 0.95);
        One(true, 73193, 29654, 10307.5, 3605.08, 0.99);
        One(true, 300000, 1200, 7.5e6, 31000, 0.95);
        One(true, 12, 600000, 40, 1.9e6, 0.95);
        // tables; the last two have a column of 120 or fewer, and the first count of each has few values
        One(false, 200000, 190000, 300000, 310000, 0.95);
        One(false, 120000, 80000, 380000, 420000, 0.99);
        One(false, 250000, 50, 250000, 70, 0.95);
        One(false, 15, 300000, 40, 600000, 0.95);
        Say(toBeOnLogarithms == 0 && onLogarithms > 0, $"{onLogarithms} of the {tables} are worked out on the scale of logarithms, and {toBeOnLogarithms} with 100,000 values of the first count or more are not");
        Say(longest < 10, $"the one that takes longest takes {longest:F2} seconds");
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  the estimate, the four limits and the four P values of {tables} tables and pairs of rates, of which {onLogarithms} are worked out on the scale of logarithms; the one that takes longest takes {longest:F2} seconds");
    }
}
