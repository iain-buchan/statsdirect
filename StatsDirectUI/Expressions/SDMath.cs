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
            if ((n < 0.0))
                return Constant.MISSING;
            if ((n == 0.0))
                return 1.0;
            if ((0.0 <= n) && (n <= 50.0))
            {
                if (n != Math.Floor(n))
                    return Math.Exp(PDF.alogam(n + 1.0));
                double Q = 1.0;
                for (int j = Convert.ToInt32(n); j >= 1; j--)
                    Q *= j;
                return Q;
            }
            double a = PDF.alogam(n + 1.0);
            double maxExp = Math.Log(Constant.LMREAL);
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

        public static double Clog(double arg)
        {
            return Math.Log(arg) / Math.Log(10.0);
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
            double term = Math.Exp(arg) / (1.0 + Math.Exp(arg));
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
            int ifault;
            double term = PDF.gauinv(1.0 - arg, out ifault);
            if (ifault != 0)
                throw new ArgumentOutOfRangeException("arg", arg, "gauinv returned fault");
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
                throw new ArgumentOutOfRangeException("x", x, "logit: x must be between 0 and 1");
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
            double phi;
            double plo;
            double term;
            int fault;
            ExFortran.poisson(mean, (int)Math.Floor(k), out phi, out plo, out term, out fault);
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
            double phi;
            double plo;
            double term;
            int fault;
            ExFortran.poisson(mean, (int)Math.Floor(k), out phi, out plo, out term, out fault);
            return fault != 0 ? Constant.MISSING : phi;
        }

        public static double InvPoissonTail(double mean, double p)
        {
            double phi;
            double plo;
            double term;
            int fault;
            int nl;
            ExFortran.poissonNl(1, p, mean, out term, out phi, out plo, out nl, out fault);
            return fault != 0 ? Constant.MISSING : nl;
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
            int fault;
            double dterm;
            double dplo;
            double dphi;
            ExFortran.bino((int)Math.Floor(n), p, (int)Math.Floor(r), out dterm, out dplo, out dphi, out fault);
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
            int fault;
            double dterm;
            double dplo;
            double dphi;
            ExFortran.bino((int)Math.Floor(n), p, (int)Math.Floor(r), out dterm, out dplo, out dphi, out fault);
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
            int fault;
            double dterm;
            double dplo;
            double dphi;
            ExFortran.bino((int)Math.Floor(n), p, (int)Math.Floor(r), out dterm, out dplo, out dphi, out fault);
            return fault != 0 ? Constant.MISSING : dphi;
        }

        public static double Chi2Tail(double df, double q)
        {
            return PDF.chivalp(q, df);
        }

        public static double InvChi2Tail(double df, double p)
        {
            int fault;
            double result = PDF.ppchi2(p, df, out fault);
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
            int ifault;
            double term = PDF.gauinv(p, out ifault);
            if (ifault != 0)
                return Constant.MISSING;
            return term;
        }

        public static double Pt(double q, double df, double ncp, bool lowerTail, bool logP)
        {
            double p;
            if (ncp == Constant.MISSING)
                p = PDF.tvalp(q, df);
            else
            {
                int fault;
                p = ExFortran.pnct(q, (int)Math.Floor(df), ncp, out fault);
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
            int fault;
            double q = ExFortran.tnct(p, (int)Math.Floor(df), ncp, out fault);
            if (fault != 0)
                return Constant.MISSING;
            return q;
        }

        public static double Dpois(double k, double mean, bool logP)
        {
            double phi;
            double plo;
            double term;
            int fault;
            ExFortran.poisson(mean, (int)Math.Floor(k), out phi, out plo, out term, out fault);
            if (fault != 0)
                return Constant.MISSING;
            if (logP)
                term = Math.Log(term);
            return term;
        }

        public static double Ppois(double k, double mean, bool lowerTail, bool logP)
        {
            double phi;
            double plo;
            double term;
            int fault;
            ExFortran.poisson(mean, (int)Math.Floor(k), out phi, out plo, out term, out fault);
            if (fault != 0)
                return Constant.MISSING;
            double p = lowerTail ? plo : phi;
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
            double phi;
            double plo;
            double term;
            int fault;
            int nl;
            ExFortran.poissonNl(1, p, mean, out term, out phi, out plo, out nl, out fault);
            return fault != 0 ? Constant.MISSING : nl;
        }

        public static double Pbinom(double r, double n, double p, bool lowerTail, bool logP)
        {
            if (logP)
                p = Math.Exp(p);
            int fault;
            double dterm;
            double dplo;
            double dphi;
            ExFortran.bino((int)Math.Floor(n), p, (int)Math.Floor(r), out dterm, out dplo, out dphi, out fault);
            if (fault != 0)
                return Constant.MISSING;
            double pOut = lowerTail ? dplo : dphi;
            if (logP)
                pOut = Math.Log(pOut);
            return pOut;
        }

        public static double Dbinom(double r, double n, double p, bool logP)
        {
            if (logP)
                p = Math.Exp(p);
            int fault;
            double dterm;
            double dplo;
            double dphi;
            ExFortran.bino((int)Math.Floor(n), p, (int)Math.Floor(r), out dterm, out dplo, out dphi, out fault);
            if (fault != 0)
                return Constant.MISSING;
            if (logP)
                dterm = Math.Log(dterm);
            return dterm;
        }

        public static double Pchisq(double q, double df, bool lowerTail, bool logP)
        {
            double p = PDF.chivalp(q, df);
            if (!lowerTail)
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
            int fault;
            double result = PDF.ppchi2(p, df, out fault);
            return fault != 0 ? Constant.MISSING : result;
        }

        public static double Pf(double q, double df1, double df2, bool lowerTail, bool logP)
        {
            double p = PDF.fvalp(q, df1, df2);
            if (!lowerTail)
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
            return PDF.ffromp(df1, df2, p);
        }

        public static double Idiv(double numerator, double denominator)
        {
            // Integer division, so the loss of fraction is entirely deliberate!
            // ReSharper disable PossibleLossOfFraction
            return (int)numerator / (int)denominator;
            // ReSharper restore PossibleLossOfFraction
        }
    }
}
