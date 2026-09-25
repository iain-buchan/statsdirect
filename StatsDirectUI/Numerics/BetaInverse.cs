using System;

namespace StatsDirect.Numerics.SpecialFunctions;

/// <summary>Both coordinates, including their logs when a coordinate underflows.</summary>
internal readonly record struct BetaQuantile(double X, double Y, double LogX, double LogY)
{
    internal BetaQuantile Mirror() => new(Y, X, LogY, LogX);

    internal static BetaQuantile FromLogit(double z)
    {
        double small = Math.Exp(-Math.Abs(z));
        double logLarge = -MathSupport.Log1p(small);
        double logSmall = -Math.Abs(z) + logLarge;
        double smallCoordinate = small / (1 + small);
        var result = new BetaQuantile(smallCoordinate, 1 - smallCoordinate, logSmall, logLarge);
        return z <= 0 ? result : result.Mirror();
    }
}

internal static partial class IncompleteBeta708
{
    /// <summary>Evaluate from a log odds without losing a subnormal coordinate.</summary>
    internal static BetaTails FromLogit(double a, double b, double z) => TailsAt(a, b, BetaQuantile.FromLogit(z));

    private static BetaTails TailsAt(double a, double b, BetaQuantile point)
    {
        if (point.LogX < -708)
            return TinyCoordinate(a, b, point.LogX, false);
        if (point.LogY < -708)
            return TinyCoordinate(b, a, point.LogY, true);
        return Evaluate(a, b, point.X, point.Y);
    }

    // The integrated binomial series, with bx formed from logs. Below exp(-708),
    // bx < 6 for every finite b, so this remains a short series. Retain
    // log(x) itself: exp(log(x)) may be zero or have few significant digits.
    private static BetaTails TinyCoordinate(double a, double b, double logX, bool upper)
    {
        double x = Math.Exp(logX), bx = Math.Exp(Math.Log(b) + logX);
        double product = 1;
        var sum = new AccurateSum();
        sum.Add(1);
        for (int n = 1; n <= 1000; n++)
        {
            product *= x - bx / n;
            double term = product * (a / (a + n));
            sum.Add(term);
            if (Math.Abs(term) <= 4 * MathSupport.Eps * Math.Abs(sum.Value))
            {
                double logTail = a * logX - MathSupport.LogShapeBeta(a, b) + Math.Log(sum.Value);
                return Tails(logTail, upper, new Work { Iterations = n }, "log-coordinate power series");
            }
        }
        throw new ArithmeticException("Log-coordinate beta series did not converge");
    }

    /// <summary>
    /// The beta quantile for a lower (or upper) tail probability, given as itself or as a logarithm, by Newton's method in log
    /// odds within a bracket, solving for the smaller of the two tails; both coordinates come back with their logarithms.
    /// </summary>
    internal static BetaQuantile Inverse(double a, double b, double probability,
        bool lowerTail = true, bool logProbability = false, int maxIterations = 256)
    {
        if (!(a > 0 && b > 0) || !double.IsFinite(a) || !double.IsFinite(b))
            throw new ArgumentOutOfRangeException(nameof(a));
        double logP = ProbabilityLog(probability, logProbability);
        double logQ = logProbability ? MathSupport.Log1mExp(logP) : MathSupport.Log1p(-probability);
        if (!lowerTail)
            (logP, logQ) = (logQ, logP);
        if (double.IsNegativeInfinity(logP))
            return BetaQuantile.FromLogit(double.NegativeInfinity);
        if (double.IsNegativeInfinity(logQ))
            return BetaQuantile.FromLogit(double.PositiveInfinity);
        if (a == b && logP == logQ)
            return BetaQuantile.FromLogit(0);
        bool mirror = logP > logQ;
        if (mirror)
        {
            (a, b) = (b, a);
            (logP, logQ) = (logQ, logP);
        }

        // The first term of the power series for the tail gives a starting scale, not an answer: every answer that is not an
        // endpoint is found within a bracket. Where that term is no use, the same term for the other tail (which places a
        // quantile whose far coordinate is minute, as for a shape below 1e-37) and failing both the mean.
        double logStart = (logP + Math.Log(a) + MathSupport.LogBeta(a, b)) / a;
        double logOther = (logQ + Math.Log(b) + MathSupport.LogBeta(a, b)) / b;
        double start = logStart < -1 ? logStart - MathSupport.Log1mExp(logStart)
            : logOther < -1 ? MathSupport.Log1mExp(logOther) - logOther
            : Math.Log(a) - Math.Log(b);
        if (double.IsInfinity(start))
        {
            // a shape so small (subnormal) that the leading term places the quantile at an end
            var endpoint = BetaQuantile.FromLogit(start);
            return mirror ? endpoint.Mirror() : endpoint;
        }
        // The residual of the log tail at log odds z against the log probability asked for, and its slope: the derivative of
        // the tail with respect to the log odds is the density times x(1 - x), that is x^a (1 - x)^b / B(a, b), so the slope of
        // the log tail is that over the tail. The slope only guides the next trial value, so the plain logarithmic form serves
        // where a coordinate has underflowed.
        (double Residual, double Slope) At(double z)
        {
            var point = BetaQuantile.FromLogit(z);
            var tails = TailsAt(a, b, point);
            double residual = tails.LogLower - logP;
            if (double.IsNaN(residual))
                throw new ArithmeticException("Invalid inverse-beta residual");
            double logKernel = point.X > 0 && point.Y > 0
                ? MathSupport.LogBetaKernel(a, b, point.X, point.Y)
                : a * point.LogX + b * point.LogY - MathSupport.LogBeta(a, b);
            return (residual, Math.Exp(logKernel - tails.LogLower));
        }
        var atStart = At(start);
        double low = start, high = start;
        double best = start, bestResidual = atStart.Residual, bestSlope = atStart.Slope, error = Math.Abs(atStart.Residual);
        double step = Math.Max(1, Math.Abs(start) / 16);
        bool bracketed = atStart.Residual == 0;
        // Doubling steps from the start towards the root; each point passed moves that end of the bracket, and the first
        // point beyond the root closes it.
        for (int i = 0; i < 128 && !bracketed; i++)
        {
            double point = atStart.Residual < 0 ? start + step : start - step;
            if (!double.IsFinite(point))
                throw new ArithmeticException("Could not bracket beta quantile");
            var at = At(point);
            if (Math.Abs(at.Residual) < error)
            {
                best = point;
                bestResidual = at.Residual;
                bestSlope = at.Slope;
                error = Math.Abs(at.Residual);
            }
            if (at.Residual < 0)
                low = point;
            else
                high = point;
            bracketed = atStart.Residual < 0 ? at.Residual >= 0 : at.Residual <= 0;
            step *= 2;
        }
        if (!bracketed)
            throw new ArithmeticException("Could not bracket beta quantile");
        // Newton's method from the best point so far: the log tail is concave in the log odds, so a step from below stays
        // below the root. A step that would leave the bracket, or that follows a Newton step which failed to halve the
        // residual, is replaced by bisection, so the bracket always closes. The answer is the point with the smallest
        // residual, and running out of iterations is a failure, not an answer.
        bool converged = false, bisect = false;
        int iteration = 0;
        for (; iteration < maxIterations && !converged; iteration++)
        {
            double point = bisect ? double.NaN : best - bestResidual / bestSlope;
            if (!bisect && point == best)
                break; // a Newton step below the resolution of the log odds
            // a Newton step on to the end of the bracket on its own side: that end was evaluated already and was no better,
            // so the residual is at its rounding floor and the root is within it (bisecting the far end from here would
            // take some fifty evaluations for no better answer)
            if (!bisect && (bestResidual < 0 ? point == low : point == high))
                break;
            bool newton = point > low && point < high;
            if (!newton)
                point = low / 2 + high / 2;
            if (point == low || point == high || high - low <= 4 * MathSupport.Eps * Math.Max(1, Math.Abs(point)))
                break;
            if (point == best)
            {
                // the midpoint is the best point itself: the bracket closes on to its side of the root without another evaluation
                if (bestResidual < 0)
                    low = best;
                else
                    high = best;
                continue;
            }
            var at = At(point);
            converged = at.Residual == 0 || (newton && Math.Abs(point - best) <= 4 * MathSupport.Eps * Math.Max(1, Math.Abs(point)));
            bisect = newton && Math.Abs(at.Residual) > error / 2;
            if (Math.Abs(at.Residual) < error)
            {
                best = point;
                bestResidual = at.Residual;
                bestSlope = at.Slope;
                error = Math.Abs(at.Residual);
            }
            if (at.Residual < 0)
                low = point;
            else
                high = point;
        }
        if (!converged && iteration == maxIterations)
            throw new ArithmeticException("Inverse beta did not converge");
        var result = Polish(BetaQuantile.FromLogit(best), -bestResidual / bestSlope);
        return mirror ? result.Mirror() : result;
    }

    /// <summary>
    /// The last step of Newton's method applied to the coordinates themselves rather than to the log odds: the log odds hold
    /// the answer only to their own rounding, which is the smaller coordinate's relative precision times |z|, and the residual
    /// at the rounded point measures that. The step, dx = x y dz, is taken while it is small and the coordinates are normal
    /// doubles; otherwise the log odds stand.
    /// </summary>
    private static BetaQuantile Polish(BetaQuantile point, double stepZ)
    {
        if (!(Math.Abs(stepZ) < 1e-6) || Math.Min(point.X, point.Y) < Constant.DBL_MIN)
            return point;
        double change = stepZ * point.X * point.Y;
        double x = point.X + change, y = point.Y - change;
        if (x <= y)
            y = 1 - x;
        else
            x = 1 - y;
        if (!(x > 0 && y > 0))
            return point;
        return new BetaQuantile(x, y, x > .5 ? MathSupport.Log1p(-y) : Math.Log(x), y > .5 ? MathSupport.Log1p(-x) : Math.Log(y));
    }

    internal static double ProbabilityLog(double p, bool logProbability)
    {
        if (double.IsNaN(p) || p == Constant.MISSING || (logProbability ? p > 0 : p < 0 || p > 1))
            throw new ArgumentOutOfRangeException(nameof(p));
        return logProbability ? p : Math.Log(p);
    }
}
