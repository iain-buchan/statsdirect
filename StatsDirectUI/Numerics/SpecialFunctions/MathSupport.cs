// The elementary and gamma function helpers of the incomplete beta routines: a compensated sum, log1p, expm1, log-gamma,
// log-beta and the upper incomplete gamma function.
using System;
namespace StatsDirect.Numerics.SpecialFunctions;

internal struct AccurateSum
{
    private double total, correction;
    public void Add(double x)
    {
        double next = total + x;
        correction += Math.Abs(total) >= Math.Abs(x) ? (total - next) + x : (x - next) + total;
        total = next;
    }
    public readonly double Value => total + correction;
}

internal static class MathSupport
{
    internal const double Eps = 2.22044604925031308085e-16;
    internal const double LogTwoPi = 1.83787706640934548356;
    internal const double Euler = 0.57721566490153286061;

    internal static double Log1p(double x)
    {
        if (x == -1)
            return double.NegativeInfinity;
        if (x < -1 || double.IsNaN(x))
            return double.NaN;
        double rounded = 1 + x;
        if (rounded == 1)
            return x;
        return Math.Log(rounded) - ((rounded - 1) - x) / rounded;
    }
    internal static double Log1pMinusX(double x)
    {
        if (Math.Abs(x) > 0.5)
            return Log1p(x) - x;
        double power = x * x;
        var sum = new AccurateSum();
        for (int n = 2; n <= 100; n++)
        {
            double term = (n % 2 == 0 ? -power : power) / n;
            sum.Add(term);
            if (term == 0 || Math.Abs(term) < Eps * Math.Abs(sum.Value))
                return sum.Value;
            power *= x;
        }
        throw new ArithmeticException("log1p(x)-x did not converge");
    }
    internal static double Expm1(double x)
    {
        if (Math.Abs(x) > 0.5)
            return Math.Exp(x) - 1;
        double term = x;
        var sum = new AccurateSum();
        sum.Add(x);
        for (int k = 2; k < 80; k++)
        {
            term *= x / k;
            sum.Add(term);
            if (term == 0 || Math.Abs(term) <= Eps * Math.Abs(sum.Value))
                return sum.Value;
        }
        throw new ArithmeticException("expm1 did not converge");
    }
    internal static double Log1mExp(double x) => x < -0.6931471805599453
        ? Log1p(-Math.Exp(x)) : Math.Log(-Expm1(x));
    internal static double LogAdd(double x, double y)
    {
        if (double.IsNegativeInfinity(x))
            return y;
        if (double.IsNegativeInfinity(y))
            return x;
        return Math.Max(x, y) + Log1p(Math.Exp(-Math.Abs(x - y)));
    }
    internal static double LogCoordinate(double x, double complement) => x > 0.5 ? Log1p(-complement) : Math.Log(x);

    // log Gamma(1 + z), |z| <= 1/2: the Taylor series of DLMF 5.7.3, whose coefficients are the zeta values in GammaCoefficients.cs.
    private static double LogGammaTaylor(double z)
    {
        var coefficients = GammaCoefficients.Values;
        double p = coefficients[^1];
        for (int i = coefficients.Length - 2; i >= 0; i--)
            p = Math.FusedMultiplyAdd(p, z, coefficients[i]);
        return z * Math.FusedMultiplyAdd(z, p, -Euler);
    }
    internal static double LogGamma1p(double x)
    {
        if (Math.Abs(x) <= 0.5)
            return LogGammaTaylor(x);
        return LogGamma(1 + x);
    }
    internal static double StirlingCorrection(double x)
    {
        // Bernoulli-number expansion, x >= 16.
        double v = 1 / x, t = v * v;
        return v * (1.0 / 12 + t * (-1.0 / 360 + t * (1.0 / 1260 + t * (-1.0 / 1680 + t * (1.0 / 1188 + t * (-691.0 / 360360 + t * (1.0 / 156 + t * (-3617.0 / 122400 + t * (43867.0 / 244188)))))))));
    }
    private static readonly double[] StirlingCoefficients = { 1.0 / 12, -1.0 / 360, 1.0 / 1260, -1.0 / 1680, 1.0 / 1188, -691.0 / 360360, 1.0 / 156, -3617.0 / 122400, 43867.0 / 244188 };
    // Stirling's correction at x less that at x + d, for a d that may be far smaller than x (the difference of the two
    // evaluations was lost to rounding once x + d rounded to x): term by term, c x^-n (1 - (1 + d / x)^-n) for n = 1, 3, 5, ...
    internal static double StirlingDifference(double x, double d)
    {
        double logRatio = Log1p(d / x), power = 1 / x, sum = 0;
        for (int k = 0; k < StirlingCoefficients.Length; k++)
        {
            sum += StirlingCoefficients[k] * power * -Expm1(-(2 * k + 1) * logRatio);
            power /= x * x;
        }
        return sum;
    }
    internal static double LogGamma(double x)
    {
        if (!(x > 0) || !double.IsFinite(x))
            throw new ArgumentOutOfRangeException(nameof(x));
        if (x >= 16)
            return (x - 0.5) * Math.Log(x) - x + LogTwoPi / 2 + StirlingCorrection(x);
        if (x < 0.5)
            return LogGammaTaylor(x) - Math.Log(x);
        var sum = new AccurateSum();
        while (x > 1.5)
        {
            x -= 1;
            sum.Add(Math.Log(x));
        }
        sum.Add(LogGammaTaylor(x - 1));
        return sum.Value;
    }
    // log Gamma(b) - log Gamma(b+a), arranged to avoid subtracting large log-Gamma values.
    internal static double LogGammaRatio(double b, double a)
    {
        var sum = new AccurateSum();
        while (b < 16)
        {
            sum.Add(Log1p(a / b));
            b += 1;
        }
        double h = a / b;
        sum.Add(-a * Math.Log(b));
        sum.Add(-b * Log1pMinusX(h));
        sum.Add(-(a - 0.5) * Log1p(h));
        sum.Add(StirlingDifference(b, a));
        return sum.Value;
    }
    internal static double LogBeta(double a, double b)
    {
        if (a > b)
            (a, b) = (b, a);
        if (a < 16)
            return LogGamma(a) + LogGammaRatio(b, a);
        double h = a / b, lp = Math.Log(h) - Log1p(h), lq = -Log1p(h);
        var sum = new AccurateSum();
        sum.Add(a * lp);
        sum.Add(b * lq);
        sum.Add(-0.5 * (Math.Log(a) + lq) + LogTwoPi / 2);
        sum.Add(StirlingCorrection(a) + StirlingCorrection(b) - StirlingCorrection(a + b));
        return sum.Value;
    }
    // log(a B(a, b)), which is small when a is: for a small a through log Gamma(1 + a) and the ratio of gammas, so that it is
    // not the difference of log B(a, b) and log a, which then agree to their first figures and leave the rest to rounding;
    // otherwise plainly, the ratio of gammas for a larger a being a sum of many logarithms that costs figures
    internal static double LogShapeBeta(double a, double b)
        => b == 1 ? 0 : a == 1 ? -Math.Log(b) : a < .1 ? LogGamma1p(a) + LogGammaRatio(b, a) : LogBeta(a, b) + Math.Log(a);
    internal static double DifferenceProducts(double a, double b, double c, double d)
    {
        double p = a * b, q = c * d;
        return (p - q) + (Math.FusedMultiplyAdd(a, b, -p) - Math.FusedMultiplyAdd(c, d, -q));
    }
    internal static double MeanDistance(double a, double b, double x, double y)
    {
        // Use the smaller coordinate as authoritative; 1-x can have far fewer
        // relative digits than an independently supplied small y (and vice versa).
        double sum = a + b;
        if (double.IsFinite(sum))
        {
            double remainder = a >= b ? (a - sum) + b : (b - sum) + a;
            return x <= y ? Math.FusedMultiplyAdd(-sum, x, a) - remainder * x
                        : Math.FusedMultiplyAdd(sum, y, -b) + remainder * y;
        }
        return DifferenceProducts(a, y, b, x);
    }
    internal static double Deviance(double a, double b, double lambda)
        => -a * Log1pMinusX(-lambda / a) - b * Log1pMinusX(lambda / b);
    // The kernel log(x^a y^b / B(a, b)), and the kernel over the first shape, log(x^a y^b / (a B(a, b))). With both shapes
    // below 16 the plain form serves; with a larger shape of 16 or more and the smaller of 1 or more, the plain form's terms
    // b log y and log B(a, b) agree to their first figures and leave the rest to rounding, so the kernel is formed as its value
    // at the mean less the divergence of the coordinates from the means, neither of which is large; a smaller shape below 1
    // keeps the plain form, whose terms are then of the size of the answer.
    internal static double LogBetaKernel(double a, double b, double x, double y)
        => Math.Max(a, b) < 16 || Math.Min(a, b) < 1 ? a * LogCoordinate(x, y) + b * LogCoordinate(y, x) - LogBeta(a, b)
            : KernelAtMean(a, b, false) - Divergence(a, b, x, y);
    internal static double LogBetaKernelOverShape(double a, double b, double x, double y)
        => Math.Max(a, b) < 16 || Math.Min(a, b) < 1 ? a * LogCoordinate(x, y) + b * LogCoordinate(y, x) - LogShapeBeta(a, b)
            : KernelAtMean(a, b, true) - Divergence(a, b, x, y);
    // The kernel at the mean, log(p^a q^b / B(a, b)) with p = a / (a + b) and q = b / (a + b): through Stirling's series for
    // both shapes when both are 16 or more, and otherwise through log Gamma of the smaller (log Gamma(1 + a) in place of
    // log Gamma(a) + log a when the kernel over the shape a is wanted and a is the smaller).
    private static double KernelAtMean(double a, double b, bool overA)
    {
        double small = Math.Min(a, b), large = Math.Max(a, b);
        double stirling = StirlingCorrection(a + b) - StirlingCorrection(large);
        if (small >= 16)
            return (Math.Log(small) - Log1p(small / large) - LogTwoPi) / 2 + stirling - StirlingCorrection(small) - (overA ? Math.Log(a) : 0);
        double logGamma = overA && a == small ? LogGamma1p(small) : LogGamma(small) + (overA ? Math.Log(a) : 0);
        return small * Math.Log(small) - logGamma - small - Log1p(small / large) / 2 + stirling;
    }
    // The divergence of the coordinates from the means, a log(p / x) + b log(q / y): each shape's log ratio through log1p
    // about the mean and from the ratio itself away from it, each as a function of the mean distance lambda, so that the
    // linear parts cancel exactly (the differences of two logarithms lost figures in proportion to the shapes).
    private static double Divergence(double a, double b, double x, double y)
    {
        double lambda = MeanDistance(a, b, x, y);
        double ea = -lambda / a, eb = lambda / b;
        double ra = x * (1 + b / a), rb = y * (1 + a / b);
        double termA = ra >= .5 && ra <= 2 ? Log1pMinusX(ea) : Math.Log(ra) - ea;
        double termB = rb >= .5 && rb <= 2 ? Log1pMinusX(eb) : Math.Log(rb) - eb;
        return -(a * termA + b * termB);
    }
    // Upper incomplete Gamma, with the exponential removed, and regularized log tail.
    internal static (double LogQ, double LogScaledUpper) GammaUpper(double a, double z)
    {
        if (z == 0)
            return (0, LogGamma(a));
        if (!(a > 0) || z < 0 || !double.IsFinite(z))
            throw new ArgumentOutOfRangeException(nameof(z));
        if (z <= 1.5 && a <= 1)
        {
            // Integrate the power series for exp(-t); factor the leading 1/a explicitly.
            var sum = new AccurateSum();
            double term = 1;
            for (int n = 1; n <= 200; n++)
            {
                term *= -z / n;
                double v = term / (a + n);
                sum.Add(v);
                if (Math.Abs(v) <= Eps * Math.Abs(sum.Value))
                    break;
                if (n == 200)
                    throw new ArithmeticException("Gamma series did not converge");
            }
            double lg1 = LogGamma1p(a);
            double logP = a * Math.Log(z) - lg1 + Log1p(a * sum.Value);
            double q = -Expm1(logP);
            // log(1 - P) through log1p where P is small, since log(q) would be 0 there; q / a stays well scaled when a is small
            double logQ = Log1mExp(logP);
            return (logQ, lg1 + Math.Log(q / a) + z);
        }
        // DLMF 8.9.2. Modified Lentz evaluation, with finite iteration limit.
        const double tiny = 1e-300;
        double f = z + 1 - a, c = f, d = 0;
        for (int n = 1; n <= 20000; n++)
        {
            double alpha = n * (a - n), beta = z + 2 * n + 1 - a;
            d = beta + alpha * d;
            if (Math.Abs(d) < tiny)
                d = Math.CopySign(tiny, d);
            c = beta + alpha / c;
            if (Math.Abs(c) < tiny)
                c = Math.CopySign(tiny, c);
            d = 1 / d;
            double change = c * d;
            f *= change;
            if (Math.Abs(change - 1) <= 4 * Eps)
            {
                double scaled = a * Math.Log(z) - Math.Log(f);
                return (scaled - z - LogGamma(a), scaled);
            }
        }
        throw new ArithmeticException("Gamma fraction did not converge");
    }
}
