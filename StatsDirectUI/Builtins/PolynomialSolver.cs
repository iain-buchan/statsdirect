using System;

using StatsDirect.Numerics;
using StatsDirect.Templates;

namespace StatsDirect.Builtins
{
    public class PolynomialSolver
    {
        private const int MAX_ITER = 300; // Max # of iterations to bracket/converge to a root
        private const double TOLERANCE = 0.000000000001; // Relative tolerance in results (do not use < 1e-15 if Pegasus rootfinder used)

        /// <summary>
        /// The polynomial of conditional coefficients
        /// </summary>
        protected double[] polyDenominator { get; set; }
        /// <summary>
        /// The degree of polyDenominator
        /// </summary>
        protected int degDenominator { get; set; }

        /// <summary>
        /// The "numerator" polynomial in Func
        /// </summary>
        protected double[] polyNumerator { get; set; }
        /// <summary>
        /// The degree of polyNumerator
        /// </summary>
        protected int degNumerator { get; set; }

        protected double value { get; set; } // Used in defining Func

        protected bool UseLogScale { get; set; }

        /// <summary>
        /// get log(exp(a)+exp(b)) avoiding overflow due to exp(a) or exp(b)
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static double SumLog(double a, double b)
        {
            double big = Math.Max(a, b);
            return Math.Log(Math.Exp(a - big) + Math.Exp(b - big)) + big;
        }

        /// <summary>
        /// This routine multiplies together two polynomials P1 and P2 to obtain the product polynomial P3.
        /// </summary>
        /// <param name="host"></param>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        /// <param name="deg1"></param>
        /// <param name="deg2"></param>
        /// <param name="job"></param>
        /// <remarks>Reference 'Algorithms 2nd ed.', by R. Sedgewick (Addison-Wesley, 1988), p. 522.</remarks>
        protected static (double[] result, int resultDegree, int ierr) MultPoly(IProgressBarHost host, double[] p1, double[] p2, int deg1, int deg2, string job, bool logScale)
        {
            int resultDegree = deg1 + deg2;
            double[] p3 = new double[resultDegree + 1];
            bool couldBeSlow = Convert.ToDouble(deg1) * Convert.ToDouble(deg2) > 300000;

            double initialValue = logScale
                ? -Constant.MISSING
                : 0.0;
            for (int i = 0; i <= resultDegree; i++)
                p3[i] = initialValue;

            using (IProgressBar progress = host.StartProgress("Multiplying polynomials: " + job, true, couldBeSlow))
            {
                if (logScale)
                {
                    for (int i = 0; i <= deg1; i++)
                    {
                        for (int j = 0; j <= deg2; j++)
                        {
                            if (p3[i + j] == -Constant.MISSING)
                                p3[i + j] = p1[i] + p2[j];
                            else
                                p3[i + j] = SumLog(p1[i] + p2[j], p3[i + j]);
                        }
                        if (couldBeSlow)
                        {
                            if (progress.Update(i / (double)deg1))
                                return (p3, resultDegree, 1);
                        }
                    }
                }
                else
                {
                    for (int i = 0; i <= deg1; i++)
                    {
                        for (int j = 0; j <= deg2; j++)
                            p3[i + j] = p1[i] * p2[j] + p3[i + j];
                        if (couldBeSlow)
                        {
                            if (progress.Update(i / (double)deg1))
                                return (p3, resultDegree, 1);
                        }
                    }

                    //  Test for overflow; if so, set an appropriate error value.
                    for (int i = 0; i <= resultDegree; i++)
                        if (double.IsInfinity(p3[i]) || double.IsNaN(p3[i]))
                            return (p3, resultDegree, 6); // Old VB6 code for an overflow
                }
            }

            // If we get here, there were no errors
            return (p3, resultDegree, 0);
        }

        private double Func(double r, out int ierr)
        {
            ierr = 0;
            // The root (value at which func = 0) of this function is the conditional MLE of the common odds ratio
            // or common rate ratio, or an exact confidence limit depending on how the
            // global variables VALUE, POLYN, and POLYD are defined.

            double numer = EvalPoly(polyNumerator, degNumerator, r, UseLogScale);
            double denom = EvalPoly(polyDenominator, degDenominator, r, UseLogScale);

            if (UseLogScale)
            {
                if (r <= 1.0)
                    return Math.Exp(numer - denom) - value;
                if (degDenominator - degNumerator == 0)
                    return Math.Exp(numer - denom) - value;
                return Math.Exp(numer - Math.Log(r) * Convert.ToDouble(degDenominator - degNumerator) - denom) - value;
            }
            if (denom == 0.0)
            {
                ierr = 6;
                return 0;
            }
            if (r <= 1.0)
                return numer / denom - value;
            return numer / Math.Pow(r, degDenominator - degNumerator) / denom - value;
        }

        ///  <summary>
        ///  This routine returns the value of the polynomial c, a polynomial of
        ///  conditional coefficients of degree DEGC, evaluated at an odds ratio or
        ///  rate ratio R. If R > 1.0 then the poly evaluated is C / R^(DEGC) - helps avoid overflows.
        ///  Horner's method is used to evaluate the polynomial.
        ///  </summary>
        ///  <param name="c"></param>
        ///  <param name="degC"></param>
        ///  <param name="r"></param>
        ///  <returns></returns>
        ///  <remarks></remarks>
        private static double EvalPoly(double[] c, int degC, double r, bool logScale)
        {
            double y;
            if (logScale)
            {
                if (r == 0.0)
                {
                    y = c[0];
                }
                else if (r <= 1.0)
                {
                    y = c[degC];
                    if (r < 1)
                    {
                        for (int i = degC - 1; i >= 0; i--)
                            y = SumLog(y + Math.Log(r), c[i]);
                    }
                    else
                    {
                        for (int i = degC - 1; i >= 0; i--)
                            y = SumLog(y, c[i]);
                    }
                }
                else
                {
                    y = c[0];
                    double z = Math.Log(1.0 / r);
                    for (int i = 1; i <= degC; i++)
                        y = SumLog(y + z, c[i]);
                }
            }
            else
            {
                if (r == 0.0)
                {
                    y = c[0];
                }
                else if (r <= 1.0)
                {
                    y = c[degC];
                    if (r < 1.0)
                    {
                        for (int i = degC - 1; i >= 0; i--)
                            y = y * r + c[i];
                    }
                    else
                    {
                        for (int i = degC - 1; i >= 0; i--)
                            y += c[i];
                    }
                }
                else
                {
                    y = c[0];
                    double z = 1.0 / r;
                    for (int i = 1; i <= degC; i++)
                        y = y * z + c[i];
                }
            }

            return y;
        }

        /// <summary>
        /// Given a positive non-zero starting value APPROX, this routine searches for
        /// a bracket to the root of the function Func on the interval [0, infinity)
        /// so that on output F0 * F1 &lt;= 0 which guarantees that a root lies in the
        /// interval [X0, X1].
        /// </summary>
        /// <param name="approx"></param>
        /// <param name="x0"></param>
        /// <param name="x1"></param>
        /// <param name="f0"></param>
        /// <param name="f1"></param>
        /// <param name="ierr"></param>
        private void BracketRoot(double approx, out double x0, out double x1, out double f0, ref double f1, out int ierr)
        {
            int iter = 0;
            x1 = Math.Max(0.5, approx); // X1 is the upper bound
            x0 = 0.0; // X0 is the lower bound
            f0 = Func(x0, out ierr); // Func at X0
            if (ierr != 0)
                return;
            f1 = Func(x1, out ierr); // Func at X1
            if (ierr != 0)
                return;

            // if necessary, increase X1 until F1 and F0 have different signs
            while ((f1 * f0 > 0.0) && (iter < MAX_ITER))
            {
                iter += 1;
                x0 = x1;
                f0 = f1;
                x1 = x1 * 1.5 * iter;
                f1 = Func(x1, out ierr);
                if (ierr != 0)
                    return;
            }
        }

        /// <summary>
        /// This Sub returns a single real root of the function Func on the
        /// interval [X0, X1] to within a relative tolerance TOLERANCE. The Sub
        /// implements an elegant modified regula falsi algorithm (the Pegasus
        /// modification). Brent's method for root solving is slightly faster but more
        /// complex.
        /// </summary>
        /// <param name="x0"></param>
        /// <param name="x1"></param>
        /// <param name="f0"></param>
        /// <param name="f1"></param>
        /// <param name="root"></param>
        /// <param name="ierr">
        /// 0 = no error,
        /// 1 = X0 and X1 don't bracket the root (i.e. F0 * F1 > 0),
        /// 2 = root not found in MAXITER iterations.
        /// </param>
        /// <remarks>
        /// Reference
        ///    Jarrat, P., A review of methods for solving non-linear algebraic
        ///    equations in one variable, in Rabinowitz, P. (ed.), Numerical Methods
        ///    for Nonlinear Algebraic Equations, 1973, Gordon & Breach, Science
        ///    Publ., New York.
        ///</remarks>
        private double Zero(ref double x0, ref double x1, ref double f0, ref double f1, out int ierr)
        {
            ierr = 0; // Initialize

            if (Math.Abs(f0) < Math.Abs(f1))
            {
                // Make X1 best approx to root
                double swap = x0;
                x0 = x1;
                x1 = swap;
                swap = f0;
                f0 = f1;
                f1 = swap;
            }

            bool found = f1 == 0.0;
            if ((!found) && f0 * f1 > 0.0)
            {
                // Root not bracketed
                ierr = 1;
            }

            // Converge to root
            int iter = 0;
            while (found == false && iter < MAX_ITER && ierr == 0)
            {
                iter += 1;
                double x2 = x1 - f1 * (x1 - x0) / (f1 - f0);
                double f2 = Func(x2, out ierr);
                if (ierr != 0)
                    return Constant.MISSING;
                if (f1 * f2 < 0.0)
                {
                    // X0 not retained
                    x0 = x1;
                    f0 = f1;
                }
                else
                {
                    // X0 retained => modify F0
                    f0 = f0 * f1 / (f1 + f2); // The Pegasus modification
                }
                x1 = x2;
                f1 = f2;
                found = (Math.Abs(x1 - x0) < Math.Abs(x1) * TOLERANCE) || (f1 == 0.0);
            }

            if (!found && (iter >= MAX_ITER) && (ierr == 0))
                ierr = 2; // Too many iterations

            return x1; // Estimated root
        }

        /// <summary>
        /// This routine returns the root of Func above on the interval [0, infinity).
        /// </summary>
        /// <param name="approx"></param>
        /// <param name="root"></param>
        /// <param name="ierr"></param>
        protected double Converge(double approx, out int ierr)
        {
            double f1 = 0;

            if (double.IsInfinity(approx)) approx = 1.0;
            BracketRoot(approx, out double x0, out double x1, out double f0, ref f1, out ierr);
            if (ierr != 0)
                return Constant.MISSING;

            return Zero(ref x0, ref x1, ref f0, ref f1, out ierr);
        }
    }
}
