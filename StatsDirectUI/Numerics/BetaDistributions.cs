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
    private static BetaTails GammaTailAbove(double dfn, double f)
    {
        double z = dfn * f / 2;
        if (double.IsPositiveInfinity(z))
            return new(0, 1, double.NegativeInfinity, 0, 0, "endpoint");
        double logUpper = MathSupport.GammaUpper(dfn / 2, z).LogQ;
        return new(Math.Exp(logUpper), -MathSupport.Expm1(logUpper), logUpper, MathSupport.Log1mExp(logUpper), 0, "gamma tail");
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
        if (double.IsPositiveInfinity(dfn) && double.IsPositiveInfinity(dfd))
            return PointMassQuantile(p, lowerTail, logProbability);
        if (double.IsPositiveInfinity(dfn))
            dfn = InfiniteDf(dfd);
        else if (double.IsPositiveInfinity(dfd))
            dfd = InfiniteDf(dfn);
        if (!ValidShape(dfn / 2) || !ValidShape(dfd / 2))
            return double.NaN;
        try
        {
            var beta = IncompleteBeta708.Inverse(dfd / 2, dfn / 2, p, !lowerTail, logProbability);
            // F = (dfd / dfn) (y / x) from the coordinates themselves while both are normal doubles and the quotient is finite:
            // the exponential of the logarithms loses a few figures at large degrees of freedom
            if (beta.X >= Constant.DBL_MIN && beta.Y >= Constant.DBL_MIN)
            {
                double f = dfd / dfn * (beta.Y / beta.X);
                if (double.IsFinite(f))
                    return f;
            }
            return Math.Exp(Math.Log(dfd) - Math.Log(dfn) + beta.LogY - beta.LogX);
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
        // the F on 1 and df at t squared (with infinite df, the endpoint where t squared has overflowed, and otherwise FTails'
        // substitute for infinity or, above t squared = 1e3, the incomplete gamma function); the logarithmic form where t
        // squared has overflowed or underflowed with finite df
        var beta = double.IsFinite(tt) && (tt > 0 || infinite) ? FTails(tt, 1, df)
            : infinite ? new BetaTails(0, 1, double.NegativeInfinity, 0, 0, "endpoint")
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
        if (double.IsPositiveInfinity(df))
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
}
