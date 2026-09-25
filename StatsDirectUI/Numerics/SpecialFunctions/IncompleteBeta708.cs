// The incomplete beta function ratio and its complement, with their logarithms, by the methods of DiDonato and Morris,
// Significant digit computation of the incomplete beta function ratios, NSWC TR 88-365 (1988): the power series, the shape
// recurrence, the incomplete gamma expansion, the weighted continued fraction and the central asymptotic expansion, each in its
// region. The equation numbers in the comments are the report's.
using System;
namespace StatsDirect.Numerics.SpecialFunctions;

internal readonly record struct BetaTails(double Lower, double Upper, double LogLower, double LogUpper, int Iterations, string Method);

internal static partial class IncompleteBeta708
{
    private const double Tolerance = 4 * MathSupport.Eps;
    private sealed class Work
    {
        internal int Iterations;
    }

    public static BetaTails Evaluate(double a, double b, double x) => Evaluate(a, b, x, 1 - x);
    public static BetaTails Evaluate(double a, double b, double x, double y)
    {
        if (!(a > 0) || !(b > 0) || !double.IsFinite(a) || !double.IsFinite(b))
            throw new ArgumentOutOfRangeException(nameof(a), "Both shapes must be positive and finite");
        if (!(x >= 0 && x <= 1 && y >= 0 && y <= 1) || Math.Abs((x - 0.5) + (y - 0.5)) > 4 * MathSupport.Eps)
            throw new ArgumentOutOfRangeException(nameof(x), "Supply x and its accurate complement y");
        if (x == 0)
            return new(0, 1, double.NegativeInfinity, 0, 0, "endpoint");
        if (y == 0)
            return new(1, 0, 0, double.NegativeInfinity, 0, "endpoint");
        if (a == b && x == y)
            return new(.5, .5, -Math.Log(2), -Math.Log(2), 0, "symmetry");
        if (b == 1)
            return Tails(a * MathSupport.LogCoordinate(x, y), false, new Work(), "power identity");
        if (a == 1)
            return Tails(b * MathSupport.LogCoordinate(y, x), true, new Work(), "power identity");
        var work = new Work();
        bool upper = false;
        double logTail;
        string method;
        if (Math.Min(a, b) <= 1)
        {
            if (x > 0.5)
            {
                Swap(ref a, ref b, ref x, ref y);
                upper = !upper;
            }
            if (Math.Max(a, b) <= 1)
            {
                if (a >= Math.Min(.2, b) || a * MathSupport.LogCoordinate(x, y) <= Math.Log(.9))
                {
                    logTail = PowerSeries(a, b, x, y, work);
                    method = "power series";
                }
                else if (x >= .3)
                {
                    logTail = PowerSeries(b, a, y, x, work);
                    upper = !upper;
                    method = "complement power series";
                }
                else
                {
                    logTail = ShiftedGamma(b, a, y, x, work);
                    upper = !upper;
                    method = "recurrence + gamma expansion";
                }
            }
            else if (b <= 1 || (x < .1 && a * Math.Log(b * x) <= Math.Log(.7)))
            {
                logTail = PowerSeries(a, b, x, y, work);
                method = "power series";
            }
            else if (x >= .3)
            {
                logTail = PowerSeries(b, a, y, x, work);
                upper = !upper;
                method = "complement power series";
            }
            else
            {
                logTail = ShiftedGamma(b, a, y, x, work);
                upper = !upper;
                method = "gamma expansion";
            }
        }
        else
        {
            double lambda = MathSupport.MeanDistance(a, b, x, y);
            if (lambda < 0)
            {
                Swap(ref a, ref b, ref x, ref y);
                lambda = -lambda;
                upper = !upper;
            }
            if (b < 40)
            {
                if (b * x <= .7)
                {
                    logTail = PowerSeries(a, b, x, y, work);
                    method = "power series";
                }
                else
                {
                    int count = (int)Math.Ceiling(b) - 1;
                    double fraction = b - count;
                    double recurrence = RaiseFirst(fraction, a, y, x, count, work);
                    double remainder = x <= .7 ? PowerSeries(a, fraction, x, y, work) : ShiftedGamma(a, fraction, x, y, work);
                    logTail = MathSupport.LogAdd(recurrence, remainder);
                    method = "shape recurrence";
                }
            }
            else if (Math.Min(a, b) >= 128 && lambda <= .025 * Math.Min(a, b))
            {
                logTail = CentralExpansion(a, b, lambda, work);
                method = "central asymptotic expansion";
            }
            else
            {
                logTail = ContinuedFraction(a, b, x, y, lambda, work);
                method = "weighted continued fraction";
            }
        }
        return Tails(logTail, upper, work, method);
    }
    private static void Swap(ref double a, ref double b, ref double x, ref double y)
    {
        (a, b) = (b, a);
        (x, y) = (y, x);
    }
    private static BetaTails Tails(double logTail, bool upper, Work work, string method)
    {
        if (double.IsNaN(logTail) || logTail > 0)
            throw new ArithmeticException("Invalid calculated beta tail");
        double tail = Math.Exp(logTail), other = -MathSupport.Expm1(logTail), logOther = MathSupport.Log1mExp(logTail);
        return upper ? new(other, tail, logOther, logTail, work.Iterations, method)
                     : new(tail, other, logTail, logOther, work.Iterations, method);
    }
    // Equation (12): integrate the binomial expansion of (1-t)^(b-1).
    private static double PowerSeries(double a, double b, double x, double y, Work work)
    {
        var sum = new AccurateSum();
        sum.Add(1);
        double product = 1;
        for (int n = 1; n <= 20000; n++)
        {
            work.Iterations++;
            product *= ((n - b) / n) * x;
            double term = product * (a / (a + n));
            sum.Add(term);
            if (Math.Abs(term) <= Tolerance * Math.Abs(sum.Value))
                return a * MathSupport.LogCoordinate(x, y) - MathSupport.LogShapeBeta(a, b) + Math.Log(sum.Value);
        }
        throw new ArithmeticException("Beta power series did not converge");
    }
    // Equation (13): I_x(a,b)-I_x(a+count,b), a finite positive sum.
    private static double RaiseFirst(double a, double b, double x, double y, int count, Work work)
    {
        if (count == 0)
            return double.NegativeInfinity;
        var sum = new AccurateSum();
        sum.Add(1);
        double term = 1;
        for (int j = 0; j < count - 1; j++)
        {
            work.Iterations++;
            term *= ((a + j) * x + b * x) / (a + j + 1);
            sum.Add(term);
            if (!double.IsFinite(sum.Value))
            {
                double logTerm = 0, logSum = 0;
                for (int k = 0; k < count - 1; k++)
                {
                    logTerm += MathSupport.LogAdd(Math.Log(a + k), Math.Log(b)) + MathSupport.LogCoordinate(x, y) - Math.Log(a + k + 1);
                    logSum = MathSupport.LogAdd(logSum, logTerm);
                }
                return MathSupport.LogBetaKernelOverShape(a, b, x, y) + logSum;
            }
        }
        return MathSupport.LogBetaKernelOverShape(a, b, x, y) + Math.Log(sum.Value);
    }
    private static double ShiftedGamma(double a, double b, double x, double y, Work work)
    {
        if (a >= 16)
            return GammaExpansion(a, b, x, y, work);
        const int count = 20;
        return MathSupport.LogAdd(RaiseFirst(a, b, x, y, count, work), GammaExpansion(a + count, b, x, y, work));
    }
    // Equation (14), derived using t=exp(-u/T). The series is normalized by J_0,
    // so its first term is one even when the beta probability underflows.
    private static double GammaExpansion(double a, double b, double x, double y, Work work)
    {
        double logX = MathSupport.LogCoordinate(x, y), t = a + (b - 1) / 2, u = -a * logX - (b - 1) * logX / 2;
        if (u == 0)
            return 0;
        var gamma = MathSupport.GammaUpper(b, u);
        double ratioLog = -b * MathSupport.Log1p((b - 1) / (2 * a))
            + a * MathSupport.Log1pMinusX(b / a) + (b - .5) * MathSupport.Log1p(b / a)
            + MathSupport.StirlingCorrection(a + b) - MathSupport.StirlingCorrection(a);
        double inverseJ0 = Math.Exp(b * Math.Log(u) - gamma.LogScaledUpper);
        const int limit = 60;
        double[] c = new double[limit + 1];
        c[0] = 1;
        for (int n = 1; n <= limit; n++)
            c[n] = c[n - 1] / ((2.0 * n) * (2 * n + 1));
        double[] powers = PowerCoefficients(c, b - 1, limit);
        double inv2T = .5 / t, ratioJ = 1, logPower = 1;
        var sum = new AccurateSum();
        sum.Add(1);
        for (int n = 1; n <= limit; n++)
        {
            work.Iterations++;
            double shape = b + 2 * (n - 1);
            ratioJ = (shape * inv2T) * ((shape + 1) * inv2T) * ratioJ
                + ((u * inv2T + (shape + 1) * inv2T) * inv2T) * logPower * inverseJ0;
            double term = powers[n] * ratioJ;
            sum.Add(term);
            if (Math.Abs(term) <= Tolerance * Math.Abs(sum.Value))
                return ratioLog + gamma.LogQ + Math.Log(sum.Value);
            logPower *= logX * logX / 4;
        }
        throw new ArithmeticException("Beta gamma expansion did not converge");
    }
    // Equations (15.1)-(15.3): the weighted continued fraction, evaluated by the modified Lentz method.
    private static double ContinuedFraction(double a, double b, double x, double y, double lambda, Work work)
    {
        const double tiny = 1e-300;
        double f = (a / (a + 1)) * (lambda + 1), c = f, d = 0;
        int stable = 0;
        for (int n = 1; n <= 20000; n++)
        {
            work.Iterations++;
            double denominator = a + 2 * n - 1;
            double alpha = n * ((b - n) * x) * ((a + n - 1) / denominator) * (((a + n - 1) * x + b * x) / denominator);
            double beta = n + n * ((b - n) * x) / denominator + (a + n) / (a + 2 * n + 1) * (lambda + 1 + n * (1 + y));
            d = beta + alpha * d;
            if (Math.Abs(d) < tiny)
                d = Math.CopySign(tiny, d);
            c = beta + alpha / c;
            if (Math.Abs(c) < tiny)
                c = Math.CopySign(tiny, c);
            d = 1 / d;
            double change = c * d;
            f *= change;
            stable = Math.Abs(change - 1) <= Tolerance ? stable + 1 : 0;
            if (stable >= 2)
                return MathSupport.LogBetaKernel(a, b, x, y) - Math.Log(f);
        }
        throw new ArithmeticException("Beta continued fraction did not converge");
    }
    // Coefficients of A(s)^exponent, A(0)=1, obtained by differentiating A^r.
    private static double[] PowerCoefficients(double[] a, double exponent, int degree)
    {
        double[] result = new double[degree + 1];
        result[0] = 1;
        for (int n = 1; n <= degree; n++)
        {
            var sum = new AccurateSum();
            for (int k = 1; k <= n; k++)
                sum.Add((((exponent + 1) * k - n) / n) * a[k] * result[n - k]);
            result[n] = sum.Value;
        }
        return result;
    }
    // Equation (16). Derive coefficients by Lagrange inversion of v=s*sqrt(A(s));
    // integrate each resulting monomial against exp(-u^2) using its recurrence.
    private static double CentralExpansion(double a, double b, double lambda, Work work)
    {
        double h = Math.Min(a, b) / Math.Max(a, b);
        double p = a <= b ? h / (1 + h) : 1 / (1 + h), q = a <= b ? 1 / (1 + h) : h / (1 + h);
        double invK = Math.Sqrt(2 * Math.Max(p, q) / Math.Min(a, b));
        double z2 = MathSupport.Deviance(a, b, lambda), z = Math.Sqrt(z2);
        const int degree = 40;
        double[] shape = new double[degree + 1];
        shape[0] = 1;
        double hn = 1;
        for (int n = 1; n <= degree; n++)
        {
            hn *= h;
            double sign = n % 2 == 0 ? 1 : -1;
            shape[n] = 2.0 / (n + 2) * (a <= b ? q + sign * p * hn : q * hn + sign * p);
        }
        double[] inverse = new double[degree + 1], density = new double[degree + 1];
        inverse[0] = density[0] = 1;
        double[] integral = new double[degree + 1];
        integral[0] = Math.Exp(MathSupport.GammaUpper(.5, z2).LogScaledUpper) / 2;
        integral[1] = invK / 2;
        var sum = new AccurateSum();
        sum.Add(integral[0]);
        double v = z * invK, power = v, last = double.PositiveInfinity;
        for (int n = 1; n <= degree; n++)
        {
            work.Iterations++;
            inverse[n] = PowerCoefficients(shape, -(n + 1) / 2.0, n)[n] / (n + 1);
            var coefficient = new AccurateSum();
            for (int k = 1; k <= n; k++)
                coefficient.Add(-inverse[k] * density[n - k]);
            density[n] = coefficient.Value;
            if (n >= 2)
            {
                integral[n] = .5 * invK * power + .5 * (n - 1) * invK * invK * integral[n - 2];
                power *= v;
            }
            double term = density[n] * integral[n];
            sum.Add(term);
            if (n >= 6 && n % 2 == 0 && Math.Abs(term) + Math.Abs(last) <= Tolerance * Math.Abs(sum.Value))
                return MathSupport.StirlingCorrection(a + b) - MathSupport.StirlingCorrection(a) - MathSupport.StirlingCorrection(b)
                    - z2 - Math.Log(Math.PI) / 2 + Math.Log(sum.Value);
            last = term;
        }
        throw new ArithmeticException("Central beta expansion did not converge");
    }
}
