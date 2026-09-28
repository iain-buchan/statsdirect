// The exact limits and P values of an odds ratio and of a rate ratio for tables of which one total is much less than the others.
// The coefficients of the polynomial of the method are then of very unlike sizes: on the ordinary scale the least of them are
// below the least number that is held, and the work is to be done on the scale of logarithms.  What is expected is worked out
// here in numbers of 60 figures: the coefficients are whole numbers of any size, the sums are of the coefficients times powers
// of the ratio, and the estimate and the limits are found by halving a range of ratios until it has no number between its ends.
using System.Numerics;
using System.Reflection;
using StatsDirect.Builtins;
using StatsDirect.Templates;

// what the report of two rates asks of the program about it: a progress bar that shows nothing, and the faults that it is told of
public class RatesHost : DispatchProxy
{
    public static int Faults;
    private static readonly NoProgress none = new();

    protected override object Invoke(MethodInfo method, object[] arguments)
    {
        if (method.Name == "Error") Faults++;
        if (method.Name == "StartProgress") return none;
        Type type = method.ReturnType;
        return type == typeof(void) ? null : type.IsValueType ? Activator.CreateInstance(type) : null;
    }

    public static ITemplateHost New() => DispatchProxy.Create<ITemplateHost, RatesHost>();
}

// A number above 0, or 0, of 60 figures: a whole number times a power of 10
internal readonly struct Big
{
    private const int Kept = 60;
    public readonly BigInteger Figures;
    public readonly int Power;

    public Big(BigInteger figures, int power)
    {
        if (!figures.IsZero)
        {
            int digits = (int)Math.Floor(BigInteger.Log10(figures)) + 1;
            if (digits > Kept + 1) { figures /= BigInteger.Pow(10, digits - Kept); power += digits - Kept; }
        }
        Figures = figures; Power = power;
    }

    // a double as it is held: its whole number of 53 bits times a power of 2, which is a whole number over a power of 10
    public static Big Of(double x)
    {
        if (x == 0) return new Big(0, 0);
        long bits = BitConverter.DoubleToInt64Bits(x);
        int exponent = (int)((bits >> 52) & 0x7FF);
        long mantissa = bits & 0xFFFFFFFFFFFFFL;
        if (exponent == 0) exponent++; else mantissa |= 1L << 52;
        exponent -= 1075;
        return exponent >= 0 ? new Big(mantissa * BigInteger.Pow(2, exponent), 0) : new Big(mantissa * BigInteger.Pow(5, -exponent), exponent);
    }

    public static Big operator *(Big a, Big b) => new(a.Figures * b.Figures, a.Power + b.Power);

    public static Big operator +(Big a, Big b)
    {
        if (a.Figures.IsZero) return b;
        if (b.Figures.IsZero) return a;
        if (a.Power < b.Power) (a, b) = (b, a);
        int more = a.Power - b.Power;
        // a number that is less than the last figure of the other adds nothing
        return more > 3 * Kept ? a : new Big(a.Figures * BigInteger.Pow(10, more) + b.Figures, b.Power);
    }

    public static bool operator <(Big a, Big b)
    {
        if (b.Figures.IsZero) return false;
        if (a.Figures.IsZero) return true;
        int common = Math.Min(a.Power, b.Power);
        if (a.Power - common > 4 * Kept) return false;
        if (b.Power - common > 4 * Kept) return true;
        return a.Figures * BigInteger.Pow(10, a.Power - common) < b.Figures * BigInteger.Pow(10, b.Power - common);
    }

    public static bool operator >(Big a, Big b) => b < a;

    // one number over another, as a double
    public static double Over(Big a, Big b)
    {
        if (a.Figures.IsZero) return 0;
        BigInteger quotient = a.Figures * BigInteger.Pow(10, 2 * Kept) / b.Figures;
        int power = a.Power - b.Power - 2 * Kept;
        // the quotient in 17 figures, and its power of 10
        int digits = (int)Math.Floor(BigInteger.Log10(quotient)) + 1;
        if (digits > 17) { quotient /= BigInteger.Pow(10, digits - 17); power += digits - 17; }
        return power < -340 ? 0 : double.Parse(quotient.ToString() + "E" + power.ToString(), System.Globalization.CultureInfo.InvariantCulture);
    }
}

internal static partial class Program
{
    // the numbers of ways to take 0 to n of n, each made from the one before it
    private static BigInteger[] WaysOf(int n)
    {
        BigInteger[] ways = new BigInteger[n + 1];
        BigInteger c = 1;
        for (int k = 0; k <= n; k++)
        {
            ways[k] = c;
            c = c * (n - k) / (k + 1);
        }
        return ways;
    }

    // The coefficients, and the least value of the first count.  Odds ratio, the table a b / c d: the ways of each first count with
    // the totals of the table.  Rate ratio, a and b events in the person-times c and d: the ways of each number of the events being
    // of the first group, times the person-times to the powers of the events
    private static (int least, Big[] coefficients) Coefficients(bool rate, int a, int b, int c, int d)
    {
        if (rate)
        {
            int m = a + b;
            BigInteger[] ways = WaysOf(m);
            return (0, Enumerable.Range(0, m + 1).Select(i => new Big(ways[i] * BigInteger.Pow(c, i) * BigInteger.Pow(d, m - i), 0)).ToArray());
        }
        int row = a + b, first = a + c, second = b + d;
        int least = Math.Max(0, row - second), most = Math.Min(row, first);
        BigInteger[] ofFirst = WaysOf(first), ofSecond = WaysOf(second);
        return (least, Enumerable.Range(least, most - least + 1).Select(i => new Big(ofFirst[i] * ofSecond[row - i], 0)).ToArray());
    }

    // the sum of the coefficients from one place to another, each times its power of the ratio and times a weight
    private static Big Sum(Big[] coefficients, int from, int to, Big ratio, Func<int, Big> weight)
    {
        Big sum = new(0, 0);
        for (int i = to; i >= from; i--) sum = sum * ratio + coefficients[i] * weight(i);
        for (int i = 0; i < from; i++) sum *= ratio;
        return sum;
    }

    // The nine figures: the estimate, Fisher's limits and the mid-P limits, Fisher's P values and the mid-P values
    private static double[] Expected(Big[] coefficients, int observed, double level)
    {
        int last = coefficients.Length - 1;
        Big one = Big.Of(1), half = Big.Of(0.5), alpha = Big.Of(0.5 * (1.0 - level));
        // the ratio at which what is below it becomes what is not: the range is halved, on the scale of logarithms, until its
        // ends are next to each other
        double Halve(Func<double, bool> below)
        {
            double low = 1e-300, high = 1e300;
            for (int i = 0; i < 2000; i++)
            {
                double mid = Math.Exp(0.5 * (Math.Log(low) + Math.Log(high)));
                if (mid <= low || mid >= high) mid = 0.5 * (low + high);
                if (mid <= low || mid >= high) break;
                if (below(mid)) low = mid; else high = mid;
            }
            return 0.5 * (low + high);
        }
        // the estimate: the ratio with which the mean of the count is the observed count
        double estimate = observed == 0 ? 0 : observed == last ? double.PositiveInfinity
            : Halve(r => Sum(coefficients, 0, last, Big.Of(r), i => Big.Of(i)) < Sum(coefficients, 0, last, Big.Of(r), _ => one) * Big.Of(observed));
        // A tail: the observed count or more (upper) or the observed count or fewer, the observed count with all of its
        // probability or the half of it; to be put beside the sum of all
        Big Tail(Big ratio, bool upper, bool halved)
        {
            Func<int, Big> weight = i => i == observed && halved ? half : one;
            return upper ? Sum(coefficients, observed, last, ratio, weight) : Sum(coefficients, 0, observed, ratio, weight);
        }
        // a lower limit is the ratio with which the tail above has the probability alpha, and that tail rises with the ratio; an
        // upper limit is the ratio with which the tail below has it, and that tail falls
        double Limit(bool lower, bool halved) => lower && observed == 0 ? 0 : !lower && observed == last ? double.PositiveInfinity
            : Halve(r => { Big ratio = Big.Of(r); return Tail(ratio, lower, halved) < Sum(coefficients, 0, last, ratio, _ => one) * alpha == lower; });
        Big all = Sum(coefficients, 0, last, one, _ => one);
        double Share(Big part) => Big.Over(part, all);
        double up = Share(Sum(coefficients, observed, last, one, _ => one)), down = Share(Sum(coefficients, 0, observed, one, _ => one));
        double own = Share(coefficients[observed]);
        Big noMore = coefficients[observed] * Big.Of(1.0 + 1e-7);
        Big two = new(0, 0);
        for (int i = 0; i <= last; i++) if (!(coefficients[i] > noMore)) two += coefficients[i];
        double mid = Math.Min(up, down) - own / 2;
        // a tail that is nearly nothing is not to be made from a difference of doubles
        if (mid < 1e-3) mid = Math.Min(Share(Sum(coefficients, observed, last, one, i => i == observed ? half : one)), Share(Sum(coefficients, 0, observed, one, i => i == observed ? half : one)));
        return new[] { estimate, Limit(true, false), Limit(false, false), Limit(true, true), Limit(false, true), Math.Min(up, down), Math.Min(1, Share(two)), mid, Math.Min(1, 2 * mid) };
    }

    private static readonly string[] nine = { "the estimate", "Fisher's lower limit", "Fisher's upper limit", "the lower mid-P limit", "the upper mid-P limit", "Fisher's one sided P", "Fisher's two sided P", "the one sided mid-P", "the two sided mid-P" };

    // what differs of the nine figures that are given, or nothing; a probability below 1e-280 may be given as nothing
    private static string NotAs(double[] given, double[] expected)
    {
        for (int i = 0; i < 9; i++)
        {
            bool same = given[i] == expected[i] || given[i] != M && Math.Abs(given[i] - expected[i]) <= 1e-9 * Math.Abs(expected[i]) || i >= 5 && expected[i] < 1e-280 && given[i] >= 0 && given[i] < 1e-280;
            if (!same) return $"{nine[i]} is {(given[i] == M ? "missing" : given[i].ToString("R", inv))}, and is to be {expected[i].ToString("R", inv)}";
        }
        return null;
    }

    private static string OfMethod(bool rate, int a, int b, int c, int d, double level, out bool logarithms, out double seconds)
    {
        ExactBB.Rec2X2[] t = new ExactBB.Rec2X2[1];
        t[0].Freq = 1; t[0].A = a; t[0].M1 = a + b;
        if (rate) { t[0].N1 = c; t[0].N0 = d; t[0].IsInformative = (double)a * c != 0 || (double)b * d != 0; }
        else { t[0].N1 = a + c; t[0].N0 = b + d; t[0].IsInformative = (double)a * d != 0 || (double)b * c != 0; }
        logarithms = false;
        System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
        new ExactBB().Exact22K(none, 0, 1, rate ? ExactBB.Exact22KDataType.Type3 : ExactBB.Exact22KDataType.Type1, t, level, out double e, out double uf, out double lf, out double um, out double lm,
            out double p1, out double p2, out double m1, out double m2, ref logarithms, out int fault);
        seconds = clock.Elapsed.TotalSeconds;
        if (fault != 0) return $"the method has the fault {fault}";
        var (least, coefficients) = Coefficients(rate, a, b, c, d);
        return NotAs(new[] { e, lf, uf, lm, um, p1, p2, m1, m2 }, Expected(coefficients, a - least, level));
    }

    private static void Scales()
    {
        Console.WriteLine();
        Console.WriteLine("Tables of which one total is much less than the others");
        int before = failures, tables = 0, onLogarithms = 0;
        double longest = 0;
        void One(bool rate, int a, int b, int c, int d)
        {
            string differs;
            bool logarithms = false; double seconds = 0;
            try { differs = OfMethod(rate, a, b, c, d, tables % 3 == 0 ? 0.99 : 0.95, out logarithms, out seconds); } catch (Exception ex) { differs = Message(ex); }
            tables++;
            if (logarithms) onLogarithms++;
            if (seconds > longest) longest = seconds;
            if (differs != null) Say(false, $"{(rate ? "the rate ratio of the events" : "the odds ratio of the table")} {a} {b} / {c} {d}: {differs}");
        }
        // The rate ratio has its estimate from the rates: for 1,999 events and 1 in the person-times 2 and 1 it is 999.5
        {
            var (_, coefficients) = Coefficients(true, 1999, 1, 2, 1);
            double estimate = Expected(coefficients, 1999, 0.95)[0];
            Say(Math.Abs(estimate - 999.5) < 1e-11, $"the estimate that is expected of the rate ratio of 1999 and 1 events in the person-times 2 and 1 is {estimate}, and the ratio of the rates is 999.5");
        }
        // The first row and the first column of m, the others of 9 or 40 times m less m; the first count at each end of its
        // range, near to each end, and where it is expected.  Each table also on its side; and the same numbers of events, with
        // person-times of 9 or 40 to 1 and of 1 to them
        foreach (int m in new[] { 250, 400, 1100 })
            foreach (int times in new[] { 9, 40 })
                foreach (int from in new[] { 0, 1, 20, -1, -2, -21, 1000000 })
                {
                    int a = from == 1000000 ? m / (times + 1) : from >= 0 ? from : m + 1 + from;
                    One(false, a, m - a, m - a, times * m - m + a);
                    One(false, m - a, a, times * m - m + a, m - a);
                    One(true, a, m - a, times, 1);
                    One(true, a, m - a, 1, times);
                }
        Say(failures == before, $"the exact method of {tables} tables");
        Say(onLogarithms > 50 && onLogarithms < tables, $"{onLogarithms} of the {tables} tables are worked out on the scale of logarithms");
        Say(longest < 2, $"the table that takes longest takes {longest:F2} seconds");
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  the estimate, the four limits and the four P values of {tables} tables, of which {onLogarithms} are worked out on the scale of logarithms; the table that takes longest takes {longest:F2} seconds");

        // ---- the reports
        before = failures;
        {
            ParameterBag report = Odds(400, 0, 0, 3600, 0.95);
            var (least, coefficients) = Coefficients(false, 400, 0, 0, 3600);
            double[] expected = Expected(coefficients, 400 - least, 0.95);
            double[] given = { report["eor"].AsDouble, report["llf"].AsDouble, report["ulf"].AsDouble, report["llm"].AsDouble, report["ulm"].AsDouble, report["p1f"].AsDouble, report["p2f"].AsDouble, report["p1m"].AsDouble, report["p2m"].AsDouble };
            string differs = NotAs(given, expected);
            Say(differs == null && Rows(report, "*note").Count == 0 && Math.Abs(report["llf"].AsDouble - 194966.9235971809) < 1e-6 && Math.Abs(report["llm"].AsDouble - 265497.8891638003) < 1e-6,
                $"the report of the odds ratio of 400 0 / 0 3600: the lower limits are {report["llf"].AsObject} and {report["llm"].AsObject}, and it has nothing to say of a method that was not completed ({differs})");
        }
        foreach (var (a, b, first, second) in new[] { (400, 0, 9, 1), (0, 400, 9, 1), (400, 0, 1, 9), (20, 1080, 40, 1) })
        {
            ParameterBag p = Bag(("a", a), ("b", b), ("pt1", first), ("pt2", second), ("gamma", 0.95));
            p.AddInput("do_cml", true);
            RatesHost.Faults = 0;
            ParameterBag report = Analysis.RptRateCompareTwo(RatesHost.New(), p).ParameterBag;
            List<ParameterBag> exact = Rows(report, "*exact");
            var (least, coefficients) = Coefficients(true, a, b, first, second);
            double[] expected = Expected(coefficients, a, 0.95);
            string differs = exact.Count != 1 ? "the report has no exact figures"
                : NotAs(new[] { exact[0]["eor"].AsDouble, exact[0]["llf"].AsDouble, exact[0]["ulf"].AsDouble, exact[0]["llm"].AsDouble, exact[0]["ulm"].AsDouble, exact[0]["p1f"].AsDouble, exact[0]["p2f"].AsDouble, exact[0]["p1m"].AsDouble, exact[0]["p2m"].AsDouble }, expected);
            Say(differs == null && RatesHost.Faults == 0, $"the report of two rates, {a} and {b} events in the person-times {first} and {second}: {differs}; the host was told of {RatesHost.Faults} faults");
        }
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  the reports of the odds ratio and of two rates");
    }
}
