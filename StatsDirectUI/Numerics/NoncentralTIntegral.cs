using System;
using System.Collections.Generic;
using StatsDirect.Numerics.SpecialFunctions;

namespace StatsDirect.Numerics;

// An integral of the defining normal/chi-square mixture, used when the finite-df recurrence in pnct
// would lose its starting terms or require too many steps. No large-noncentrality normal approximation.
internal static class NoncentralTIntegral
{
    private static readonly double[] Nodes16 = { .98940093499164993260, .94457502307323257608, .86563120238783174388, .75540440835500303390, .61787624440264374845, .45801677765722738634, .28160355077925891323, .09501250983763744019 };
    private static readonly double[] Weights16 = { .02715245941175409485, .06225352393864789286, .09515851168249278481, .12462897125553387205, .14959598881657673208, .16915651939500253819, .18260341504492358887, .18945061045506849629 };
    private static readonly double[] Nodes8 = { .96028985649753623168, .79666647741362673959, .52553240991632898582, .18343464249564980494 };
    private static readonly double[] Weights8 = { .10122853629037625915, .22238103445337447054, .31370664587788728734, .36268378337836198297 };

    // exp(x) - 1 - x without losing its quadratic leading term near zero.
    private static double Expm1MinusX(double x)
    {
        if (Math.Abs(x) > .5)
            return MathSupport.Expm1(x) - x;
        double term = x * x / 2;
        var sum = new AccurateSum();
        sum.Add(term);
        for (int k = 3; k < 40; k++)
        {
            term *= x / k;
            sum.Add(term);
            if (Math.Abs(term) <= MathSupport.Eps * Math.Abs(sum.Value))
                break;
        }
        return sum.Value;
    }

    internal static double Cdf(double t, int df, double delta)
    {
        // If S=sqrt(V/df), V~chi-square(df), then P(T<=t)=E[Phi(t*S-delta)].
        // u=sqrt(2*df)*log(S) has a smooth density with mode zero at every df, and width of
        // order one even at huge df. Form its density about the mode to avoid large log-gamma cancellation.
        double a = df / 2.0, scale = Math.Sqrt(2.0 * df);
        double logHeight = a >= 16 ? -MathSupport.LogTwoPi / 2 - MathSupport.StirlingCorrection(a)
            : Math.Log(2 / scale) + a * Math.Log(a) - a - MathSupport.LogGamma(a);
        double Rate(double u) => a * Expm1MinusX(2 * u / scale);
        double Integrand(double u)
        {
            double logS = u / scale;
            // Near S=1 retain S-1 accurately. Away from it, multiply by S directly: using
            // expm1 near -1 would lose the small product t*S through cancellation of t.
            double normalArgument = Math.Abs(logS) < .5
                ? Math.FusedMultiplyAdd(t, MathSupport.Expm1(logS), t - delta)
                : Math.FusedMultiplyAdd(t, Math.Exp(logS), -delta);
            return Math.Exp(logHeight - Rate(u)) * PDF.alnorm(normalArgument);
        }

        // The chi-square Chernoff bound is exp(-Rate(u)) on either side of its mean.
        double Bound(int sign, double cutoff)
        {
            double outside = sign * 8.0, inside = 0;
            while (Rate(outside) < cutoff)
                outside *= 2;
            for (int i = 0; i < 50; i++)
            {
                double mid = (outside + inside) / 2;
                if (Rate(mid) < cutoff) inside = mid; else outside = mid;
            }
            return outside;
        }
        double Evaluate(double cutoff, double tolerance)
        {
            double lower = Bound(-1, cutoff), upper = Bound(1, cutoff);
            var cuts = new List<double> { lower, upper, 0 };
            // Unit panels resolve the density. Explicit transition points resolve Phi even for a huge t:
            // an adaptive rule alone could overlook a very narrow transition between its nodes.
            for (double u = Math.Ceiling(lower); u < upper; u++)
                if (u != 0) cuts.Add(u);
            foreach (double z in new[] { -8.0, 0.0, 8.0 })
            {
                if (t == 0) break;
                double s = (delta + z) / t;
                if (!(s > 0) || !double.IsFinite(s)) continue;
                double logS = s > .5 && s < 2 ? MathSupport.Log1p(((delta - t) + z) / t) : Math.Log(s);
                double u = scale * logS;
                if (u > lower && u < upper) cuts.Add(u);
            }
            cuts.Sort();
            int budget = 8192;
            var total = new AccurateSum();
            for (int i = 1; i < cuts.Count; i++)
            {
                if (cuts[i] == cuts[i - 1]) continue;
                double value = Integrate(Integrand, cuts[i - 1], cuts[i], tolerance / (cuts.Count - 1), 20, ref budget);
                if (!double.IsFinite(value)) return double.NaN;
                total.Add(value);
            }
            double result = total.Value;
            return result >= -2e-14 && result <= 1 + 2e-14 ? Math.Clamp(result, 0, 1) : double.NaN;
        }
        // A fixed absolute truncation error is not sufficient for a tiny probability. First estimate
        // the area, then extend the chi-square limits so the omitted mass is small relative to it.
        // Zero in the first pass can mean the relevant mass lay beyond its integration interval.
        double probability = Evaluate(42, 2e-14);
        for (int pass = 0; pass < 3 && probability < 1e-8; pass++)
        {
            double cutoff = Math.Min(745, probability > 0 ? 40 - Math.Log(probability) : 745);
            // The first interval may have omitted most of this tail. Estimate again over the wider
            // interval before choosing a relative tolerance, rather than demanding accuracy relative
            // to an arbitrarily small, truncated first estimate.
            double wider = Evaluate(cutoff, 2e-14);
            double refined = Evaluate(cutoff, Math.Max(1e-300, 2e-10 * wider));
            if (refined == 0 || !double.IsFinite(refined) || Math.Abs(refined - probability) <= 1e-8 * refined)
                return refined;
            probability = refined;
        }
        return probability;
    }

    private static double Rule(Func<double, double> f, double lo, double hi, double[] nodes, double[] weights)
    {
        double mid = (lo + hi) / 2, half = (hi - lo) / 2;
        var sum = new AccurateSum();
        for (int i = 0; i < nodes.Length; i++)
            sum.Add(weights[i] * (f(mid - half * nodes[i]) + f(mid + half * nodes[i])));
        return half * sum.Value;
    }

    private static double Integrate(Func<double, double> f, double lo, double hi, double tolerance, int depth, ref int budget)
    {
        if (--budget < 0) return double.NaN;
        double fine = Rule(f, lo, hi, Nodes16, Weights16), coarse = Rule(f, lo, hi, Nodes8, Weights8);
        // The caller scales the total tolerance to the estimated probability for small tails.
        if (Math.Abs(fine - coarse) <= tolerance + 16 * MathSupport.Eps * Math.Abs(fine))
            return fine;
        if (depth == 0 || !double.IsFinite(fine)) return double.NaN;
        double mid = (lo + hi) / 2;
        return Integrate(f, lo, mid, tolerance / 2, depth - 1, ref budget) +
               Integrate(f, mid, hi, tolerance / 2, depth - 1, ref budget);
    }
}
