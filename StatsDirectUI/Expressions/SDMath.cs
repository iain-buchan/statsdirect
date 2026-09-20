using System;
using StatsDirect.Numerics;

namespace StatsDirect.Expressions
{
    /// <summary>
    /// Implementations of functions referred to by the expression calculator.
    /// </summary>
    public static class SDMath
    {
        public static double Factorial(double n)
        {
            if (n < 0.0)
                return Constant.MISSING;
            if (n == 0.0)
                return 1.0;
            if (0.0 <= n && n <= 50.0)
            {
                if (n != Math.Floor(n))
                    return Math.Exp(PDF.alogam(n + 1.0));
                double Q = 1.0;
                for (int j = Convert.ToInt32(n); j >= 1; j--)
                    Q *= j;
                return Q;
            }
            double a = PDF.alogam(n + 1.0);
            double maxExp = Math.Log(double.MaxValue);
            return a > maxExp ? Constant.MISSING : Math.Exp(a);
        }

        public static double Asinh(double arg)
        {
            return Math.Log(arg + Math.Sqrt(arg * arg + 1.0));
        }

        public static double Acosh(double arg)
        {
            return Math.Log(arg + Math.Sqrt(arg * arg - 1.0));
        }

        public static double Atanh(double arg)
        {
            return Math.Log((1.0 + arg) / (1.0 - arg)) / 2.0;
        }

        public static double Asech(double arg)
        {
            return Math.Log((Math.Sqrt(-arg * arg + 1.0) + 1.0) / arg);
        }

        public static double Acsch(double arg)
        {
            return Math.Log((Math.Sign(arg) * Math.Sqrt(arg * arg + 1.0) + 1.0) / arg);
        }

        public static double Acoth(double arg)
        {
            return Math.Log((arg + 1.0) / (arg - 1.0)) / 2.0;
        }

        public static double Asec(double arg)
        {
            return Math.Atan(arg / Math.Sqrt(arg * arg - 1.0)) + Math.Sign(Math.Sign(arg) - 1.0) * (2.0 * Math.Atan(1.0));
        }

        public static double Acsc(double arg)
        {
            return Math.Atan(arg / Math.Sqrt(arg * arg - 1.0)) + (Math.Sign(arg) - 1.0) * (2.0 * Math.Atan(1.0));
        }

        public static double Acot(double arg)
        {
            return Math.Atan(arg) + 2.0 * Math.Atan(1.0);
        }

        public static double Sinh(double arg)
        {
            return (Math.Exp(arg) - Math.Exp(-arg)) / 2.0;
        }

        // CLOG (common logarithm) and COSH are in the function registry, and CLOG is in the help, but neither method existed,
        // so an expression using them could not be compiled.
        public static double Cosh(double arg)
        {
            return Math.Cosh(arg);
        }

        public static double Clog(double arg)
        {
            return Math.Log10(arg);
        }

        public static double Tanh(double arg)
        {
            return (Math.Exp(arg) - Math.Exp(-arg)) / (Math.Exp(arg) + Math.Exp(-arg));
        }

        public static double Sech(double arg)
        {
            return 2.0 / (Math.Exp(arg) + Math.Exp(-arg));
        }

        public static double Csch(double arg)
        {
            return 2.0 / (Math.Exp(arg) - Math.Exp(-arg));
        }

        public static double Coth(double arg)
        {
            return (Math.Exp(arg) + Math.Exp(-arg)) / (Math.Exp(arg) - Math.Exp(-arg));
        }

        public static double Cexp(double arg)
        {
            return Math.Exp(arg * Math.Log(10.0));
        }

        public static double LogFactorial(double arg)
        {
            return arg < 0.0 ? Constant.MISSING : PDF.alogam(arg + 1.0);
        }

        public static double Sec(double arg)
        {
            return 1.0 / Math.Cos(arg);
        }

        public static double Csc(double arg)
        {
            return 1.0 / Math.Sin(arg);
        }

        public static double Cot(double arg)
        {
            return 1.0 / Math.Tan(arg);
        }

        public static double Alogit(double arg)
        {
            // exp(arg) / (1 + exp(arg)) is infinity over infinity, NaN, for arg above about 709.8
            double term = arg >= 0.0 ? 1.0 / (1.0 + Math.Exp(-arg)) : Math.Exp(arg) / (1.0 + Math.Exp(arg));
            if (term <= Constant.EPSNEG)
                return 0.0;
            if (term >= 1.0 - Constant.EPSNEG)
                return 1.0;
            return term;
        }

        public static double Lz(double arg)
        {
            return PDF.alnorm(arg);
        }

        public static double Uz(double arg)
        {
            return 1.0 - PDF.alnorm(arg);
        }

        public static double Iz(double arg)
        {
            double term = PDF.gauinv(1.0 - arg, out int ifault);
            if (ifault != 0)
                throw new ArgumentOutOfRangeException(nameof(arg), arg, "gauinv returned fault");
            return term;
        }

        public static double Deg(double arg)
        {
            return arg * 0.0174532925199433;
        }

        public static double Rad(double arg)
        {
            return arg * 57.2957795130824;
        }

        public static double Logit(double x)
        {
            if (x < 0.0 || x > 1.0)
                throw new ArgumentOutOfRangeException(nameof(x), x, "logit: x must be between 0 and 1");
            if (x == 0.0)
                x = Constant.EPSNEG;
            else if (x == 1.0)
                x = 1.0 - Constant.EPSNEG;
            return Math.Log(x / (1.0 - x));
        }

        public static double Cint(double arg)
        {
            return Convert.ToDouble(Convert.ToInt64(arg));
        }

        public static double TTail(double df, double q)
        {
            return PDF.tvalp(q, df);
        }

        public static double InvTTail(double df, double p)
        {
            return PDF.tfromp(p, df);
        }

        /// <summary>
        /// probability of k events, which is the density
        /// </summary>
        /// <param name="mean"></param>
        /// <param name="k"></param>
        /// <returns></returns>
        public static double Poissonp(double mean, double k)
        {
            ExFortran.poisson(mean, (int)Math.Floor(k), out double _, out double _, out double term, out int fault);
            return fault != 0 ? Constant.MISSING : term;
        }

        /// <summary>
        /// probability of k or more events
        /// </summary>
        /// <param name="mean"></param>
        /// <param name="k"></param>
        /// <returns></returns>
        public static double PoissonTail(double mean, double k)
        {
            return PoissonAtLeast(Math.Floor(k), mean);
        }

        /// <summary>
        /// P(X &gt;= k) for a Poisson variable, computed directly as the lower incomplete gamma ratio P(k, mean), by the identity
        /// between the Poisson sum and the incomplete gamma integral. The engine's routine returns 1 - P(X &lt;= k) + P(X = k), which
        /// loses a small upper tail entirely: P(X &gt; 100) with a mean of 2 came out as 0 where it is 3.7e-131.
        /// </summary>
        private static double PoissonAtLeast(double k, double mean)
        {
            if (double.IsNaN(k) || double.IsNaN(mean) || mean < 0.0 || mean == Constant.MISSING || k == Constant.MISSING)
                return Constant.MISSING;
            if (k <= 0.0)
                return 1.0;
            if (mean == 0.0)
                return 0.0;
            double tail = PDF.gammad(mean, k, false, out int fault);
            return fault != 0 ? Constant.MISSING : tail;
        }

        /// <summary>
        /// P(X &gt;= r) for a binomial variable, computed directly as the incomplete beta ratio I_p(r, n - r + 1), by the identity
        /// between the binomial sum and the incomplete beta integral, not as 1 - P(X &lt;= r) + P(X = r).
        /// </summary>
        private static double BinomialAtLeast(double r, double n, double p)
        {
            if (double.IsNaN(r) || double.IsNaN(n) || double.IsNaN(p) || n < 0.0 || p < 0.0 || p > 1.0 || r == Constant.MISSING || n == Constant.MISSING || p == Constant.MISSING)
                return Constant.MISSING;
            if (r <= 0.0)
                return 1.0;
            if (r > n)
                return 0.0;
            double tail = PDF.betain(p, r, n - r + 1.0, out int fault);
            return fault != 0 ? Constant.MISSING : tail;
        }

        /// <summary>
        /// probability of k or a more extreme number of events, in the direction away from the mean:
        /// P(X &gt;= k) when k is at or above the mean, P(X &lt;= k) when k is below it
        /// </summary>
        /// <param name="mean"></param>
        /// <param name="k"></param>
        /// <returns></returns>
        /// <remarks>
        /// The author's definition (20 September 2026). Since version 3 this had taken a probability and returned a count,
        /// by a bisection on the lower tail that could never return 0 or 1.
        /// </remarks>
        public static double InvPoissonTail(double mean, double k)
        {
            double events = Math.Floor(k);
            if (events >= mean)
                return PoissonAtLeast(events, mean);
            ExFortran.poisson(mean, (int)events, out double _, out double plo, out double _, out int fault);
            return fault != 0 ? Constant.MISSING : plo;
        }

        /// <summary>
        /// r or fewer sucesses
        /// </summary>
        /// <param name="n"></param>
        /// <param name="r"></param>
        /// <param name="p"></param>
        /// <returns></returns>
        public static double Binomial(double n, double r, double p)
        {
            ExFortran.bino((int)Math.Floor(n), p, (int)Math.Floor(r), out double _, out double dplo, out double _, out int fault);
            return fault != 0 ? Constant.MISSING : dplo;
        }

        /// <summary>
        /// exactly r sucesses
        /// </summary>
        /// <param name="n"></param>
        /// <param name="r"></param>
        /// <param name="p"></param>
        /// <returns></returns>
        public static double Binomialp(double n, double r, double p)
        {
            ExFortran.bino((int)Math.Floor(n), p, (int)Math.Floor(r), out double dterm, out double _, out double _, out int fault);
            return fault != 0 ? Constant.MISSING : dterm;
        }

        /// <summary>
        /// r or more sucesses
        /// </summary>
        /// <param name="n"></param>
        /// <param name="r"></param>
        /// <param name="p"></param>
        /// <returns></returns>
        public static double BinomialTail(double n, double r, double p)
        {
            return BinomialAtLeast(Math.Floor(r), Math.Floor(n), p);
        }

        public static double Chi2Tail(double df, double q)
        {
            return PDF.chivalp(q, df);
        }

        public static double InvChi2Tail(double df, double p)
        {
            double result = PDF.ppchi2(1.0 - p, df, out int fault); // p is an upper tail area, as CHI2TAIL returns; ppchi2 inverts a lower tail
            return fault != 0 ? Constant.MISSING : result;
        }

        public static double Ftail(double dfn, double dfd, double q)
        {
            return PDF.fvalp(q, dfn, dfd);
        }

        public static double InvFtail(double dfn, double dfd, double p)
        {
            return PDF.ffromp(dfd, dfn, p);
        }

        public static double Pnorm(double q, double mean, double sd, bool lowerTail, bool logP)
        {
            double p = PDF.alnorm((q - mean) / sd);
            if (!lowerTail)
                p = 1.0 - p;
            if (logP)
                p = Math.Log(p);
            return p;
        }

        public static double Qnorm(double p, double mean, double sd, bool lowerTail, bool logP)
        {
            if (logP)
                p = Math.Exp(p);
            if (!lowerTail)
                p = 1.0 - p;
            double term = PDF.gauinv(p, out int ifault);
            if (ifault != 0)
                return Constant.MISSING;
            // the quantile of Normal(mean, sd); mean and sd used to be ignored
            return mean + sd * term;
        }

        public static double Pt(double q, double df, double ncp, bool lowerTail, bool logP)
        {
            double p;
            if (ncp == Constant.MISSING)
                p = PDF.tvalp(-q, df); // tvalp is the upper tail area: the lower tail at q is the upper tail at -q
            else
            {
                p = ExFortran.pnct(q, (int)Math.Floor(df), ncp, out int fault);
                if (fault != 0)
                    return Constant.MISSING;
            }
            if (!lowerTail)
                p = 1.0 - p;
            if (logP)
                p = Math.Log(p);
            return p;
        }

        public static double Qt(double p, double df, double ncp, bool lowerTail, bool logP)
        {
            if (logP)
                p = Math.Exp(p);
            if (!lowerTail)
                p = 1.0 - p;
            // central t when no non-centrality parameter is given; tfromp takes an upper tail area
            if (ncp == Constant.MISSING)
                return -PDF.tfromp(p, df);
            double q = ExFortran.tnct(p, (int)Math.Floor(df), ncp, out int fault);
            if (fault != 0)
                return Constant.MISSING;
            return q;
        }

        public static double Dpois(double k, double mean, bool logP)
        {
            ExFortran.poisson(mean, (int)Math.Floor(k), out double _, out double _, out double term, out int fault);
            if (fault != 0)
                return Constant.MISSING;
            if (logP)
                term = Math.Log(term);
            return term;
        }

        /// <summary>
        /// P(X &lt;= k) for a Poisson variable as the upper incomplete gamma ratio Q(k + 1, mean), the other tail of the same
        /// function that gives PoissonAtLeast, so the two tails sum to 1; 1 for a mean of zero.
        /// PPOIS and QPOIS share it so that one inverts the other. The engine's summation (POISSON) agrees with it to about
        /// 1e-11; near 1 with a mean of 10000 that sum is out by 5e-12, which was enough to put QPOIS one count short.
        /// </summary>
        private static double PoissonAtMost(double k, double mean)
        {
            if (double.IsNaN(k) || double.IsNaN(mean) || mean < 0.0 || mean == Constant.MISSING || k == Constant.MISSING)
                return Constant.MISSING;
            if (k < 0.0)
                return 0.0;
            if (mean == 0.0)
                return 1.0;
            double tail = PDF.gammad(mean, k + 1.0, true, out int fault);
            return fault != 0 ? Constant.MISSING : tail;
        }

        public static double Ppois(double k, double mean, bool lowerTail, bool logP)
        {
            // The upper tail is P(X > k) = P(X >= k + 1), and it is computed directly: taking it as 1 - P(X <= k)
            // lost it altogether once it fell below about 1e-16 (and its logarithm with it).
            double events = Math.Floor(k);
            double lower = PoissonAtMost(events, mean), upper = PoissonAtLeast(events + 1.0, mean);
            if (lower == Constant.MISSING || upper == Constant.MISSING)
                return Constant.MISSING;
            if (!logP)
                return lowerTail ? lower : upper;
            double smallLog = double.NaN;
            if (mean > 0.0 && events >= 0.0)
            {
                // the logarithm of a tail too small for a double, summed in log space from the probability of the nearest count
                if (!lowerTail && upper < TinyTail)
                    smallLog = LogPoissonTerm(events + 1.0, mean) + Math.Log(TailSeries(j => mean / (events + 1.0 + j)));
                if (lowerTail && lower < TinyTail)
                    smallLog = LogPoissonTerm(events, mean) + Math.Log(TailSeries(j => j > events ? 0.0 : (events - j + 1.0) / mean));
            }
            return LogOfTail(lowerTail ? lower : upper, lowerTail ? upper : lower, smallLog);
        }

        private const double TinyTail = 1.0E-290;

        /// <summary>
        /// The logarithm of a tail probability: through log1p of the other tail when the probability is near 1 (where log(1 - tiny)
        /// would be 0), and from a sum in log space when it is too small for a double.
        /// </summary>
        private static double LogOfTail(double tail, double otherTail, double logWhenTiny)
        {
            if (tail < TinyTail && !double.IsNaN(logWhenTiny))
                return logWhenTiny;
            if (tail > 0.5)
                return Log1p(-otherTail);
            return Math.Log(tail);
        }

        /// <summary>log(1 + x), accurate for small x, where log(1 + x) computed directly loses x altogether</summary>
        private static double Log1p(double x)
        {
            double u = 1.0 + x;
            if (u == 1.0)
                return x;
            // the rounding error made in forming 1 + x is cancelled by dividing by the same (u - 1)
            return Math.Log(u) * x / (u - 1.0);
        }

        /// <summary>1 + r1 + r1 r2 + r1 r2 r3 + ..., where ratio(j) is the jth ratio of successive terms (j = 1, 2, ...); stops when a term no longer counts.</summary>
        private static double TailSeries(Func<int, double> ratio)
        {
            double sum = 1.0, term = 1.0;
            for (int j = 1; j < 100000000; j++)
            {
                term *= ratio(j);
                if (term <= 0.0 || double.IsNaN(term))
                    break;
                sum += term;
                if (term < sum * 1.0E-17)
                    break;
            }
            return sum;
        }

        private static double LogPoissonTerm(double k, double mean)
        {
            return -mean + k * Math.Log(mean) - PDF.alogam(k + 1.0);
        }

        private static double LogBinomialTerm(double r, double n, double p)
        {
            return PDF.alogam(n + 1.0) - PDF.alogam(r + 1.0) - PDF.alogam(n - r + 1.0) + r * Math.Log(p) + (n - r) * Math.Log(1.0 - p);
        }

        /// <summary>
        /// The Poisson quantile: the smallest k with P(X &lt;= k) &gt;= p (for the upper tail, the smallest k with
        /// P(X &gt; k) &lt;= p), 0 for a mean of zero, infinity for a lower tail probability of 1.
        /// </summary>
        /// <remarks>
        /// The guard against rounding when p is exactly a tail value is RELATIVE to p. An absolute slack of 1e-13 was used
        /// here first, which made every p below 1e-13 return 0. The tail is compared with p on the scale p was given on - upper
        /// tail with upper tail, logarithm with logarithm - so a small upper tail is not lost in 1 - p, nor a tiny one in exp(p).
        /// The search brackets and bisects on k, the tail being monotone in k.
        /// </remarks>
        public static double Qpois(double p, double mean, bool lowerTail, bool logP)
        {
            if (double.IsNaN(p) || double.IsNaN(mean) || mean < 0.0 || p == Constant.MISSING || mean == Constant.MISSING)
                return Constant.MISSING;
            if (logP ? p > 0.0 : p < 0.0 || p > 1.0)
                return Constant.MISSING;
            if (mean == 0.0)
                return 0.0;
            double none = logP ? double.NegativeInfinity : 0.0, all = logP ? 0.0 : 1.0;
            if (p == (lowerTail ? none : all))
                return 0.0;
            if (p == (lowerTail ? all : none))
                return double.PositiveInfinity;

            // Rounding guard, so that a p which is exactly a tail value gives that count and not the next one. A fixed few
            // multiples of eps would suit tails good to 1e-15. The tails here carry the rounding of
            // exp(k ln(mean) - mean - lgamma(k + 1)), about eps times the size of those three parts (3e-11 with a mean of 10000),
            // and that error is relative to whichever tail is the smaller, the one computed directly; on the log scale it is an
            // absolute error in the logarithm of a small tail and a relative one in the logarithm of a tail near 1.
            const double eps = 2.220446049250313E-16;
            double logMean = Math.Abs(Math.Log(mean));
            double smallSide = logP ? Math.Min(1.0, Math.Abs(p)) : Math.Min(p, 1.0 - p);
            // true when k is at or beyond the quantile
            bool Reached(double k, out bool failed)
            {
                double tail = Ppois(k, mean, lowerTail, logP);
                failed = tail == Constant.MISSING || double.IsNaN(tail);
                double accuracy = 8.0 * eps * (1.0 + mean + (k + 1.0) * logMean + Math.Abs(PDF.alogam(k + 2.0)));
                double slack = accuracy * smallSide + 8.0 * eps * Math.Abs(p);
                if (lowerTail)
                    return tail >= p - slack;
                // an upper tail is not nudged past certainty, which every k would satisfy
                if (p + slack >= (logP ? 0.0 : 1.0))
                    slack = 0.0;
                return tail <= p + slack;
            }

            // bracket: low is short of the quantile (or is -1), high has reached it
            double low = -1.0;
            double high = Math.Max(1.0, Math.Ceiling(mean));
            bool failedHere;
            while (!Reached(high, out failedHere))
            {
                if (failedHere || high > 1.0E9)
                    return Constant.MISSING;
                low = high;
                high *= 2.0;
            }
            if (failedHere)
                return Constant.MISSING;
            while (high - low > 1.0)
            {
                double mid = Math.Floor((low + high) / 2.0);
                if (Reached(mid, out failedHere))
                    high = mid;
                else
                    low = mid;
                if (failedHere)
                    return Constant.MISSING;
            }
            return high;
        }

        public static double Pbinom(double r, double n, double p, bool lowerTail, bool logP)
        {
            double successes = Math.Floor(r), trials = Math.Floor(n);
            // P(X > r) = P(X >= r + 1), computed directly (see BinomialAtLeast); 1 - P(X <= r) lost small upper tails
            double upper = BinomialAtLeast(successes + 1.0, trials, p);
            if (upper == Constant.MISSING)
                return Constant.MISSING;
            double lower;
            if (successes < 0.0)
                lower = 0.0;
            else if (successes >= trials || p == 0.0)
                lower = 1.0;
            else if (p == 1.0)
                lower = 0.0; // fewer than n successes cannot happen; the engine's sum takes log(0) when p is 0 or 1
            else
            {
                ExFortran.bino((int)trials, p, (int)successes, out double _, out lower, out double _, out int fault);
                if (fault != 0)
                    return Constant.MISSING;
            }
            if (!logP)
                return lowerTail ? lower : upper;
            double smallLog = double.NaN;
            if (p > 0.0 && p < 1.0 && successes >= 0.0)
            {
                // the logarithm of a tail too small for a double, summed in log space from the probability of the nearest count
                double odds = p / (1.0 - p);
                if (!lowerTail && upper < TinyTail && successes + 1.0 <= trials)
                    smallLog = LogBinomialTerm(successes + 1.0, trials, p) + Math.Log(TailSeries(j => (trials - successes - j) / (successes + 1.0 + j) * odds));
                if (lowerTail && lower < TinyTail)
                    smallLog = LogBinomialTerm(successes, trials, p) + Math.Log(TailSeries(j => (successes - j + 1.0) / (trials - successes + j) / odds));
            }
            return LogOfTail(lowerTail ? lower : upper, lowerTail ? upper : lower, smallLog);
        }

        public static double Dbinom(double r, double n, double p, bool logP)
        {
            ExFortran.bino((int)Math.Floor(n), p, (int)Math.Floor(r), out double dterm, out double _, out double _, out int fault);
            if (fault != 0)
                return Constant.MISSING;
            if (logP)
                dterm = Math.Log(dterm);
            return dterm;
        }

        public static double Pchisq(double q, double df, bool lowerTail, bool logP)
        {
            // chi-square cannot be negative, so everything lies above a negative q (the engine's routine refuses one)
            double p = q < 0.0 && df > 0.0 && q != Constant.MISSING ? 1.0 : PDF.chivalp(q, df);
            // chivalp is the upper tail area
            if (lowerTail)
                p = 1.0 - p;
            if (logP)
                p = Math.Log(p);
            return p;
        }

        public static double Qchisq(double p, double df, bool lowerTail, bool logP)
        {
            if (logP)
                p = Math.Exp(p);
            if (!lowerTail)
                p = 1.0 - p;
            double result = PDF.ppchi2(p, df, out int fault);
            return fault != 0 ? Constant.MISSING : result;
        }

        public static double Pf(double q, double df1, double df2, bool lowerTail, bool logP)
        {
            double p = PDF.fvalp(q, df1, df2);
            // fvalp is the upper tail area
            if (lowerTail)
                p = 1.0 - p;
            if (logP)
                p = Math.Log(p);
            return p;
        }

        public static double Qf(double p, double df1, double df2, bool lowerTail, bool logP)
        {
            if (logP)
                p = Math.Exp(p);
            if (!lowerTail)
                p = 1.0 - p;
            return PDF.ffromp(df2, df1, 1.0 - p); // ffromp(denominator df, numerator df, upper tail area); p here is a lower tail area
        }

        // Unary minus for the expression evaluator. A missing value, which is a huge negative sentinel, stays missing: a plain
        // minus sign would turn it into +1.8E308, which nothing downstream recognises as missing.
        public static int Negate(int value)
        {
            return 0 - value;
        }

        public static double Negate(double value)
        {
            return value == Constant.MISSING ? value : 0.0 - value; // 0 - x, not -x: the negative of zero is zero, not the IEEE "-0"
        }

        public static double Idiv(double numerator, double denominator)
        {
            // Integer division: both operands lose their fractions, then so does the quotient. In doubles, because (int) casts
            // threw for a divisor between -1 and 1 (stopping a whole worksheet column) and saturated above 2^31.
            if (numerator == Constant.MISSING || denominator == Constant.MISSING || double.IsNaN(numerator) || double.IsNaN(denominator))
                return Constant.MISSING;
            double n = Math.Truncate(numerator), d = Math.Truncate(denominator);
            return d == 0.0 ? Constant.MISSING : Math.Truncate(n / d);
        }
    }
}
