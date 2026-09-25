using System;
using StatsDirect.Numerics.SpecialFunctions;

namespace StatsDirect.Numerics;

public static partial class PDF
{
    private const double LogTwo = 0.693147180559945309417232121458;

    /// <summary>Regularized incomplete beta; ifault: 1 shape, 2 coordinate, 3 calculation failure.</summary>
    public static double betain(double x, double p, double q, out int ifault)
        => betain(x, 1 - x, p, q, out ifault);

    /// <summary>Incomplete beta with an independently computed complementary coordinate.</summary>
    public static double betain(double x, double cx, double p, double q, out int ifault)
    {
        ifault = 1;
        if (!ValidShape(p) || !ValidShape(q))
            return x;
        ifault = 2;
        if (!(x >= 0 && x <= 1 && cx >= 0 && cx <= 1) || Math.Abs((x - .5) + (cx - .5)) > 4 * MathSupport.Eps)
            return x;
        try
        {
            double result = IncompleteBeta708.Evaluate(p, q, x, cx).Lower;
            ifault = 0;
            return result;
        }
        catch (Exception e) when (e is ArithmeticException || e is ArgumentOutOfRangeException)
        {
            ifault = 3;
            return double.NaN;
        }
    }

    /// <summary>Lower-tail beta quantile. ifault: 1/2 shape, 3 probability, 4 failure to converge.</summary>
    public static double xinbta(double pin, double qin, double p, out int ifault)
    {
        ifault = 1;
        if (!ValidShape(pin))
            return double.NaN;
        ifault = 2;
        if (!ValidShape(qin))
            return double.NaN;
        ifault = 3;
        if (!(p >= 0 && p <= 1))
            return double.NaN;
        try
        {
            double result = IncompleteBeta708.Inverse(pin, qin, p).X;
            ifault = 0;
            return result;
        }
        catch (Exception e) when (e is ArithmeticException || e is ArgumentOutOfRangeException)
        {
            ifault = 4;
            return double.NaN;
        }
    }

    // the shapes are the halved degrees of freedom, which is why those are validated halved: the smallest subnormal halves to 0
    private static bool ValidShape(double x) => x > 0 && double.IsFinite(x);

    // An infinite number of degrees of freedom is taken as 1e20 times the other number (1e20 at least, 1e300 at most), where
    // the F and t have their chi-square and normal limits to double precision (the difference is of the order of F times the
    // other degrees of freedom over the substitute) while the log odds of the coordinates stay moderate, which keeps their
    // figures. Far beyond the range where the substitute stands for infinity (an F below 1e-3 with an infinite numerator, or
    // above 1e3 with an infinite denominator, where the substitute's own tail would take over) the tail is taken from the
    // incomplete gamma function directly; a quantile that far out is the substitute's.
    private static double InfiniteDf(double otherDf) => Math.Min(1e300, 1e20 * Math.Max(1, otherDf));
    private const double FarBelow = 1e-3, FarAbove = 1e3;

    /// <summary>The lower tail of F(infinity, dfd) far below 1: the chi-square on dfd above dfd / f, from the incomplete gamma function</summary>
    private static BetaTails GammaTailBelow(double dfd, double f)
    {
        double z = dfd / (2 * f);
        if (double.IsPositiveInfinity(z))
            return new(1, 0, 0, double.NegativeInfinity, 0, "endpoint");
        double logLower = MathSupport.GammaUpper(dfd / 2, z).LogQ;
        return new(-MathSupport.Expm1(logLower), Math.Exp(logLower), MathSupport.Log1mExp(logLower), logLower, 0, "gamma tail");
    }

    /// <summary>The upper tail of F(dfn, infinity) far above 1: the chi-square on dfn above dfn f, from the incomplete gamma function</summary>
    private static BetaTails GammaTailAbove(double dfn, double f) => GammaTailAboveAt(dfn, dfn / 2 * f);

    /// <summary>The same, at z = dfn f / 2, formed by the caller so that it does not overflow before it must</summary>
    private static BetaTails GammaTailAboveAt(double dfn, double z)
    {
        if (double.IsPositiveInfinity(z))
            return new(0, 1, double.NegativeInfinity, 0, 0, "endpoint");
        double logUpper = MathSupport.GammaUpper(dfn / 2, z).LogQ;
        return new(Math.Exp(logUpper), -MathSupport.Expm1(logUpper), logUpper, MathSupport.Log1mExp(logUpper), 0, "gamma tail");
    }

    /// <summary>
    /// The z at which the logarithm of the upper incomplete gamma ratio Q(a, z) is logTarget, by Newton's method in z from a
    /// start close to it (the answer for the substitute for infinity): the slope of log Q in z is minus z^(a - 1) e^-z over
    /// Gamma(a) Q(a, z).
    /// </summary>
    private static double GammaTailInverse(double a, double logTarget, double zStart)
    {
        double z = zStart;
        for (int i = 0; i < 30; i++)
        {
            var gamma = MathSupport.GammaUpper(a, z);
            double step = (gamma.LogQ - logTarget) / -Math.Exp((a - 1) * Math.Log(z) - gamma.LogScaledUpper);
            if (!(z - step > 0) || !double.IsFinite(step))
                return z;
            z -= step;
            if (Math.Abs(step) <= 4 * MathSupport.Eps * z)
                break;
        }
        return z;
    }

    /// <summary>The logarithm of the upper (or lower) tail wanted, from a probability given for the lower or upper tail as itself or as a logarithm</summary>
    private static double LogTail(double p, bool lowerTail, bool logProbability, bool upper)
    {
        double logP = IncompleteBeta708.ProbabilityLog(p, logProbability);
        if (lowerTail != upper)
            return logP;
        return logProbability ? MathSupport.Log1mExp(logP) : MathSupport.Log1p(-p);
    }
    // + 0.0: the logarithm of a tail near 1 is log1p of minus the other tail, which for an other tail too small to register is
    // a negative zero that would be displayed as -0
    private static double SelectTail(BetaTails result, bool lower, bool log)
        => (log ? (lower ? result.LogLower : result.LogUpper) : (lower ? result.Lower : result.Upper)) + 0.0;

    private static BetaTails FTails(double f, double numeratorDf, double denominatorDf)
    {
        if (double.IsNaN(f) || f == Constant.MISSING)
            throw new ArgumentOutOfRangeException(nameof(f));
        // with both infinite, all the mass is at 1, where the tail is taken as a half
        if (double.IsPositiveInfinity(numeratorDf) && double.IsPositiveInfinity(denominatorDf))
            return f < 1 ? new(1, 0, 0, double.NegativeInfinity, 0, "endpoint")
                : f > 1 ? new(0, 1, double.NegativeInfinity, 0, 0, "endpoint")
                : new(.5, .5, -LogTwo, -LogTwo, 0, "symmetry");
        bool numeratorInfinite = double.IsPositiveInfinity(numeratorDf), denominatorInfinite = double.IsPositiveInfinity(denominatorDf);
        if (numeratorInfinite)
            numeratorDf = InfiniteDf(denominatorDf);
        else if (denominatorInfinite)
            denominatorDf = InfiniteDf(numeratorDf);
        if (!ValidShape(numeratorDf / 2) || !ValidShape(denominatorDf / 2))
            throw new ArgumentOutOfRangeException(nameof(numeratorDf));
        double a = denominatorDf / 2, b = numeratorDf / 2;
        if (f <= 0)
            return new(1, 0, 0, double.NegativeInfinity, 0, "endpoint");
        if (double.IsPositiveInfinity(f))
            return new(0, 1, double.NegativeInfinity, 0, 0, "endpoint");
        if (numeratorInfinite && f < FarBelow)
            return GammaTailBelow(denominatorDf, f);
        if (denominatorInfinite && f > FarAbove)
            return GammaTailAbove(numeratorDf, f);
        double r = numeratorDf / denominatorDf * f;
        // The ordinary ratio keeps its precision while it is a normal double; the logarithms serve where a product or quotient
        // overflows or underflows (a subnormal ratio would keep too few figures).
        if (r >= Constant.DBL_MIN && r <= 1)
            return IncompleteBeta708.Evaluate(a, b, 1 / (1 + r), r / (1 + r));
        double s = denominatorDf / numeratorDf / f;
        if (s > Constant.DBL_MIN && s < 1)
            return IncompleteBeta708.Evaluate(a, b, s / (1 + s), 1 / (1 + s));
        return IncompleteBeta708.FromLogit(a, b, Math.Log(denominatorDf) - Math.Log(numeratorDf) - Math.Log(f));
    }

    public static double FProbability(double f, double dfn, double dfd, bool lowerTail = false, bool logProbability = false)
    {
        try
        {
            return SelectTail(FTails(f, dfn, dfd), !lowerTail, logProbability);
        }
        catch (Exception e) when (e is ArgumentOutOfRangeException || e is ArithmeticException) { return double.NaN; }
    }

    /// <summary>F upper-tail probability.</summary>
    public static double fvalp(double f, double dfn, double dfd) => f < 0 ? double.NaN : FProbability(f, dfn, dfd);

    public static double FQuantile(double p, double dfn, double dfd, bool lowerTail = false, bool logProbability = false)
    {
        bool numeratorInfinite = double.IsPositiveInfinity(dfn), denominatorInfinite = double.IsPositiveInfinity(dfd);
        if (numeratorInfinite && denominatorInfinite)
            return PointMassQuantile(p, lowerTail, logProbability);
        if (numeratorInfinite)
            dfn = InfiniteDf(dfd);
        else if (denominatorInfinite)
            dfd = InfiniteDf(dfn);
        if (!ValidShape(dfn / 2) || !ValidShape(dfd / 2))
            return double.NaN;
        try
        {
            var beta = IncompleteBeta708.Inverse(dfd / 2, dfn / 2, p, !lowerTail, logProbability);
            // F = (dfd / dfn) (y / x) from the coordinates themselves while both are normal doubles and the quotient is finite:
            // the exponential of the logarithms loses a few figures at large degrees of freedom
            double f = double.NaN;
            if (beta.X >= Constant.DBL_MIN && beta.Y >= Constant.DBL_MIN)
                f = dfd / dfn * (beta.Y / beta.X);
            if (!double.IsFinite(f))
                f = Math.Exp(Math.Log(dfd) - Math.Log(dfn) + beta.LogY - beta.LogX);
            // far beyond the range where the substitute stands for infinity, the quantile is inverted from the incomplete
            // gamma function itself, from the substitute's answer as the start
            if (denominatorInfinite && f > FarAbove && double.IsFinite(f))
                f = 2 / dfn * GammaTailInverse(dfn / 2, LogTail(p, lowerTail, logProbability, true), dfn / 2 * f);
            else if (numeratorInfinite && f < FarBelow && f > 0)
                f = dfd / 2 / GammaTailInverse(dfd / 2, LogTail(p, lowerTail, logProbability, false), dfd / 2 / f);
            return f;
        }
        catch (Exception e) when (e is ArgumentOutOfRangeException || e is ArithmeticException) { return double.NaN; }
    }

    /// <summary>
    /// The F quantile with both degrees of freedom infinite: 0 for a lower tail of 0, infinity for an upper tail of 0, and
    /// otherwise 1, where all the mass is.
    /// </summary>
    private static double PointMassQuantile(double p, bool lowerTail, bool logProbability)
    {
        try
        {
            double logP = IncompleteBeta708.ProbabilityLog(p, logProbability);
            double logOther = logProbability ? MathSupport.Log1mExp(logP) : MathSupport.Log1p(-p);
            double logLower = lowerTail ? logP : logOther, logUpper = lowerTail ? logOther : logP;
            return double.IsNegativeInfinity(logLower) ? 0 : double.IsNegativeInfinity(logUpper) ? double.PositiveInfinity : 1;
        }
        catch (ArgumentOutOfRangeException)
        {
            return double.NaN;
        }
    }

    /// <summary>F upper-tail quantile; the historic argument order is denominator df first.</summary>
    public static double ffromp(double dfd, double dfn, double p) => FQuantile(p, dfn, dfd);

    private static BetaTails TTails(double t, double df)
    {
        bool infinite = double.IsPositiveInfinity(df);
        if (!ValidShape(infinite ? 1 : df / 2) || double.IsNaN(t) || t == Constant.MISSING)
            throw new ArgumentOutOfRangeException(nameof(t));
        if (t == 0)
            return new(.5, .5, -LogTwo, -LogTwo, 0, "symmetry");
        if (double.IsInfinity(t))
            return t > 0
            ? new(1, 0, 0, double.NegativeInfinity, 0, "endpoint")
            : new(0, 1, double.NegativeInfinity, 0, 0, "endpoint");
        double tt = t * t;
        // the F on 1 and df at t squared: with infinite df, FTails' substitute for infinity or, above t squared = 1e3, the
        // incomplete gamma function at half t squared, formed as (t / 2) t where t squared itself has overflowed; the
        // logarithmic form where t squared has overflowed or underflowed with finite df
        var beta = double.IsFinite(tt) && (tt > 0 || infinite) ? FTails(tt, 1, df)
            : infinite ? GammaTailAboveAt(1, Math.Abs(t) / 2 * Math.Abs(t))
            : IncompleteBeta708.FromLogit(df / 2, .5, Math.Log(df) - 2 * Math.Log(Math.Abs(t)));
        double logSmall = beta.LogLower - LogTwo;
        double small = Math.Exp(logSmall), large = -MathSupport.Expm1(logSmall), logLarge = MathSupport.Log1mExp(logSmall);
        return t > 0 ? new(large, small, logLarge, logSmall, beta.Iterations, beta.Method)
            : new(small, large, logSmall, logLarge, beta.Iterations, beta.Method);
    }

    public static double TProbability(double t, double df, bool lowerTail = false, bool logProbability = false)
    {
        try
        {
            return SelectTail(TTails(t, df), lowerTail, logProbability);
        }
        catch (Exception e) when (e is ArgumentOutOfRangeException || e is ArithmeticException) { return double.NaN; }
    }

    /// <summary>Student t upper-tail probability (one sided).</summary>
    public static double tvalp(double t, double df) => TProbability(t, df);

    public static double TQuantile(double p, double df, bool lowerTail = false, bool logProbability = false)
    {
        bool infinite = double.IsPositiveInfinity(df);
        if (infinite)
            df = InfiniteDf(1);
        if (!ValidShape(df / 2))
            return double.NaN;
        try
        {
            double logP = IncompleteBeta708.ProbabilityLog(p, logProbability);
            bool negative = logP > -LogTwo;
            double logSmall = negative ? (logProbability ? MathSupport.Log1mExp(logP) : MathSupport.Log1p(-p)) : logP;
            BetaQuantile beta;
            if (!logProbability)
                beta = IncompleteBeta708.Inverse(df / 2, .5, 2 * (negative ? 1 - p : p));
            else
                beta = IncompleteBeta708.Inverse(df / 2, .5, logSmall + LogTwo, true, true);
            // t = sqrt(df y / x) from the coordinates themselves while both are normal doubles and the quotient is finite, as for F
            double ratio = beta.X >= Constant.DBL_MIN && beta.Y >= Constant.DBL_MIN ? df * beta.Y / beta.X : double.PositiveInfinity;
            double t = double.IsFinite(ratio) ? Math.Sqrt(ratio) : Math.Exp(.5 * (Math.Log(df) + beta.LogY - beta.LogX));
            // far beyond the range where the substitute stands for infinity, from the incomplete gamma function itself: the two
            // tails together are Q(1/2, t squared / 2)
            if (infinite && t > 0 && double.IsFinite(t) && t / 2 * t > FarAbove)
                t = Math.Sqrt(GammaTailInverse(.5, logSmall + LogTwo, t / 2 * t)) * 1.4142135623730951;
            if (negative != lowerTail)
                t = -t;
            return t + 0.0;
        }
        catch (Exception e) when (e is ArgumentOutOfRangeException || e is ArithmeticException) { return double.NaN; }
    }

    /// <summary>Student t quantile from an upper-tail area, as required by historic callers.</summary>
    public static double tfromp(double p, double df) => TQuantile(p, df);
    public static double tfromp2(double p, double df)
    {
        if (!(p >= 0 && p <= 1))
            return double.NaN;
        return p > 0 && p < Constant.DBL_MIN
            ? TQuantile(Math.Log(p) - LogTwo, df, false, true)
            : TQuantile(p / 2, df);
    }

    // The chi-square and gamma distributions, as the F on the same degrees of freedom and an infinite denominator, with the
    // argument orders and fault codes of the routines they replace.
    /// <summary>
    /// upper tail area of the chi-square distribution: the F on df and an infinite number of degrees of freedom at x / df,
    /// which keeps every figure at any number of degrees of freedom
    /// </summary>
    public static double chivalp(double x, double df)
    {
        if (x < 0.0 || double.IsNaN(x))
            return double.NaN;
        return FProbability(x / df, df, double.PositiveInfinity);
    }

    /// <summary>
    /// tail area of the gamma distribution
    /// </summary>
    public static double gammad(double x, double p, out int ifault)
    {
        return gammad(x, p, false, out ifault);
    }

    /// <summary>
    /// lower tail area of the gamma distribution with shape p at x, or the upper tail if upper is true: the chi-square on
    /// 2p at 2x, which is the F on 2p and an infinite number of degrees of freedom at x / p. ifault 1 for an invalid argument.
    /// </summary>
    public static double gammad(double x, double p, bool upper, out int ifault)
    {
        ifault = 1;
        if (p <= 0.0 || x < 0.0 || double.IsNaN(x) || double.IsNaN(p))
            return 0.0;
        double ret = FProbability(x / p, 2.0 * p, double.PositiveInfinity, !upper);
        if (double.IsNaN(ret))
            return 0.0;
        ifault = 0;
        return ret;
    }

    /// <summary>
    /// chi-square percentage point for a lower tail area prob or, when upper is true, for an upper tail area prob: v times
    /// the F quantile on v and an infinite number of degrees of freedom, which keeps every figure at any number of degrees
    /// of freedom and at any tail area. ifault 1 for an invalid argument or no answer.
    /// </summary>
    public static double ppchi2(double prob, double v, out int ifault) => ppchi2(prob, v, false, out ifault);
    public static double ppchi2(double prob, double v, bool upper, out int ifault)
    {
        ifault = 1;
        if (!(prob >= 0.0 && prob <= 1.0) || !(v > 0.0))
            return -1.0;
        double ret = v * FQuantile(prob, v, double.PositiveInfinity, !upper);
        if (double.IsNaN(ret))
            return -1.0;
        ifault = 0;
        return ret;
    }
}
