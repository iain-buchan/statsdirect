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
            ExFortran.poisson(mean, (int)Math.Floor(k), out double phi, out double _, out double _, out int fault);
            return fault != 0 ? Constant.MISSING : phi;
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
            ExFortran.poisson(mean, (int)events, out double phi, out double plo, out double _, out int fault);
            if (fault != 0)
                return Constant.MISSING;
            return events >= mean ? phi : plo;
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
            ExFortran.bino((int)Math.Floor(n), p, (int)Math.Floor(r), out double _, out double _, out double dphi, out int fault);
            return fault != 0 ? Constant.MISSING : dphi;
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

        public static double Ppois(double k, double mean, bool lowerTail, bool logP)
        {
            ExFortran.poisson(mean, (int)Math.Floor(k), out double phi, out double plo, out double _, out int fault);
            if (fault != 0)
                return Constant.MISSING;
            double p = lowerTail ? plo : 1.0 - plo; // P(X > k), the complement of the lower tail, as in R; phi is P(X >= k)
            if (logP)
                p = Math.Log(p);
            return p;
        }

        public static double Qpois(double p, double mean, bool lowerTail, bool logP)
        {
            if (logP)
                p = Math.Exp(p);
            if (!lowerTail)
                p = 1.0 - p;
            // The smallest k with P(X <= k) >= p, as R's qpois, by stepping up the engine's own lower tail. poissonNl, used
            // here before, could never return 0 or 1 (QPOIS(0.05, 2) gave 2).
            if (double.IsNaN(p) || p < 0.0 || p > 1.0 || mean < 0.0)
                return Constant.MISSING;
            if (p == 1.0)
                return double.PositiveInfinity;
            for (int k = 0; k < 100000000; k++)
            {
                ExFortran.poisson(mean, k, out double _, out double plo, out double _, out int fault);
                if (fault != 0)
                    return Constant.MISSING;
                if (plo >= p - 1.0E-13) // a little slack, so that a p that is exactly a tail value is not missed through rounding
                    return k;
            }
            return Constant.MISSING;
        }

        public static double Pbinom(double r, double n, double p, bool lowerTail, bool logP)
        {
            ExFortran.bino((int)Math.Floor(n), p, (int)Math.Floor(r), out double _, out double dplo, out double dphi, out int fault);
            if (fault != 0)
                return Constant.MISSING;
            double pOut = lowerTail ? dplo : 1.0 - dplo; // P(X > r), as in R; dphi is P(X >= r)
            if (logP)
                pOut = Math.Log(pOut);
            return pOut;
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
            double p = PDF.chivalp(q, df);
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
