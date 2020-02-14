using System;

using StatsDirect.Numerics;
using StatsDirect.Templates;

namespace StatsDirect.Builtins
{
    public class ExactBB
    {
        //   This is a bare-bones program which calculates the conditional maximum
        //   likelihood estimate, exact confidence limits, and exact P-values for
        //   either an an odds ratio (given a series of 2x2 tables with person-count
        //   denominators) or a rate ratio (given a series of 2x2 tables with person-
        //   time denominators). It utilizes an efficient algorithm for calculating
        //   the coefficients of the conditional distribution as described in the
        //   references. To increase execution speed, the arithmetic is performed on
        //   the natural scale (not the log scale). If overflow ocurrs then a log
        //   scale is used.
        // 
        //   References
        //      1. Martin,D Austin,H (1991) An efficient program for computing
        //         conditional maximum likelihood estimates and exact confidence
        //         limits for a common odds ratio. Epidemiology 2, 359-362.
        //      2. Martin,DO Austin,H Exact estimates for a rate ratio.
        //         Submitted to Epidemiology.
        // 
        //   Author David O. Martin, MD, MPH
        //   Translation and extension (log scaling) by Iain Buchan
        //   Last mod 20/5/2001

        private const int MAXDEGREE = 1000000; // Max degree of a polynomial
        private const int MAX_ITER = 300; // Max # of iterations to bracket/converge to a root
        private const double TOLERANCE = 0.000000000001; // Relative tolerance in results (do not use < 1e-15 if Pegasus rootfinder used)

        /// <summary>
        /// Data for one "unique" 2x2 table
        /// </summary>
        public struct Rec2X2
        {
            public double A { get; set; }
            public double M1 { get; set; }
            public double N1 { get; set; }
            public double N0 { get; set; }
            public int Freq { get; set; }
            public bool Informative { get; set; }
        }

        private double[] polyD; // The polynomial of conditional coefficients
        private int degD; // The degree of polyD

        private double[] polyN; // The "numerator" polynomial in Func
        private int degN; // The degree of polyN

        private double value; // Used in defining Func

        private int sumA; // Sum of the observed "a" cells
        private int minSumA; // Lowest value of "a" cell sum w/ given margins
        private int maxSumA; // Highest value of "a" cell sum w/ given margins

        private bool logScale;

        private static double MAXEXP;

/*
        /// <summary>
        /// Brent alternative to Pegasus method for root finding - can be faster when high precision demanded
        /// </summary>
        /// <param name="xl">lower bound of search interval</param>
        /// <param name="xu">upper bound of search interval</param>
        /// <param name="ierr"></param>
        /// <returns></returns>
        private double BrentRoot(double xl, double xu, out int ierr)
        {
            double brentRootReturn = 0;
            double d = 0;

            double e = 0.0;
            double a = xl;
            double b = xu;

            double fa = Func(a, out ierr);
            if (ierr != 0)
            {
                return brentRootReturn;
            }
            double fb = Func(b, out ierr);
            if (ierr != 0)
            {
                return brentRootReturn;
            }
            double c = b;
            double fc = fb;

            ierr = 0;
            int nIter = 0;

            do
            {
                nIter = nIter + 1;
                if (nIter > MAX_ITER)
                {
                    ierr = 1;
                    break;
                }

                if ((fb > 0.0 & fc > 0.0) | (fb < 0.0 & fc < 0.0))
                {
                    c = a;
                    fc = fa;
                    d = b - a;
                    e = d;
                }

                if ((Math.Abs(fc) < Math.Abs(fb)))
                {
                    a = b;
                    b = c;
                    c = a;
                    fa = fb;
                    fb = fc;
                    fc = fa;
                }

                double tol1 = 2.0 * Constant.EPSILON * Math.Abs(b) + 0.5 * TOLERANCE;

                double xm = 0.5 * (c - b);

                if ((Math.Abs(xm) <= tol1 | fb == 0.0))
                {
                    break;
                }

                if ((Math.Abs(e) >= tol1 && Math.Abs(fa) > Math.Abs(fb)))
                {
                    double s = fb / fa;
                    double p;
                    double q;
                    if ((a == c))
                    {
                        p = 2.0 * xm * s;
                        q = 1.0 - s;
                    }
                    else
                    {
                        q = fa / fc;
                        double r = fb / fc;
                        p = s * (2.0 * xm * q * (q - r) - (b - a) * (r - 1.0));
                        q = (q - 1.0) * (r - 1.0) * (s - 1.0);
                    }

                    if ((p > 0.0))
                    {
                        q = -q;
                    }

                    p = Math.Abs(p);
                    double xmin = Math.Abs(e * q);
                    double tmp = 3.0 * xm * q - Math.Abs(tol1 * q);

                    if ((xmin < tmp))
                    {
                        xmin = tmp;
                    }

                    if ((2.0 * p < xmin))
                    {
                        e = d;
                        d = p / q;
                    }
                    else
                    {
                        d = xm;
                        e = d;
                    }
                }
                else
                {
                    d = xm;
                    e = d;
                }

                a = b;
                fa = fb;

                if ((Math.Abs(d) > tol1))
                {
                    b = b + d;
                }
                else
                {
                    if (xm < 0.0)
                    {
                        b = b - Math.Abs(tol1);
                    }
                    else
                    {
                        b = b + Math.Abs(tol1);
                    }
                }

                fb = Func(b, out ierr);
                if (ierr != 0)
                {
                    return brentRootReturn;
                }
            }
            while (true);

            brentRootReturn = b;

            return brentRootReturn;
        }
*/
        public void Exact22K(IPreferencesAndProgressBar host, int numTables, int dataType, Rec2X2[] tables, double confLevel, out double cMLE, out double upFishLim, out double loFishLim, out double upMidPLim, out double loMidPLim, out double fishP1, out double fishP2, out double midP1, out double midP2, ref bool useLogScale, out int ierr)
        {
            //   Stratified case-control data, matched case-control data, and
            //   stratified person-time data are all held in a record (Rec2x2). With
            //   stratified case-control data, the record is defined so that
            // 
            //                            Exposed        Non-Exposed       Total
            //         Diseased              a                 b             m1
            //         Non-Diseased          c                 d             m0
            //         --------------------------------------------------------
            //         Total                 n1                n0             t
            // 
            //   For stratified case-control data, FREQ is set to 1. For matched case-
            //   control data, the record is defined in the same way except that FREQ
            //   corresponds to the frequency of like 2x2 tables. Note that for
            //   matched data, M1 must ALWAYS equal 1.
            // 
            //   For stratified person-time data, the record is defined so that
            // 
            //                            Exposed        Non-Exposed       Total
            //         Diseased              a                 b             m1
            //         Person-Time           n1                n0             t
            // 
            //   For stratified person-time data, FREQ is set to 1. For all types of
            //   data, the variable INFORMATIVE is TRUE if no margins of the given 2x2
            //   table are zero, otherwise INFORMATIVE is FALSE.
            // 
            //   For failure time data, a/b is events at time t in exposed/unexposed and
            //   c/d is number at risk at time t minus a/b.
            // 
            //   dataType 1 - odds ratio
            //   Tables(i).freq = 1
            //   Tables(i).a = d(0)
            //   Tables(i).m1 = d(0) + d(1)  'cases
            //   Tables(i).n1 = d(0) + d(2)  'exposed
            //   Tables(i).n0 = d(1) + d(3)  'unexposed
            //   Tables(i).informative = (d(0) * d(3) <> 0) Or (d(1) * d(2) <> 0)
            // 
            //   dataType 2 - matched RR
            //   Tables(i).freq = d(3)
            //   Tables(i).a = d(0)
            //   Tables(i).m1 = d(0) + 1# - d(0)  'cases
            //   Tables(i).n1 = d(0) + d(1)  'exposed
            //   Tables(i).n0 = 1# - d(0) + d(2)  'unexposed
            //   Tables(i).informative = (d(0) * d(3) <> 0) Or (d(1) * d(2) <> 0)
            // 
            //   dataType 3
            //   Tables(i).freq = 1
            //   Tables(i).a = d(0)
            //   Tables(i).m1 = d(0) + d(1)  'cases
            //   Tables(i).n1 = d(2)         'exposed
            //   Tables(i).n0 = d(3)         'unexposed
            //   Tables(i).informative = (d(0) * d(3) <> 0) Or (d(1) * d(2) <> 0)
            // 
            //   dataType 4
            //   Tables(i).freq = 1
            //   Tables(i).a = d(0)
            //   Tables(i).m1 = d(0) + d(1)  'events at time t
            //   Tables(i).n1 = d(2)         'exposed at risk
            //   Tables(i).n0 = d(3)         'unexposed at risk
            //   Tables(i).informative = (d(0) * d(3) <> 0) Or (d(1) * d(2) <> 0)


            MAXEXP = Math.Log(double.MaxValue);
            //  Make sure that exact calculations can be performed
            logScale = useLogScale;
            CheckData(dataType, numTables, tables, out ierr);
            if (ierr == 1 || ierr == 2)
            {
                ierr = -ierr;
                polyD = null;
                polyN = null;
                loFishLim = Constant.MISSING;
                upFishLim = Constant.MISSING;
                loMidPLim = Constant.MISSING;
                upMidPLim = Constant.MISSING;
                fishP1 = Constant.MISSING;
                fishP2 = Constant.MISSING;
                midP1 = Constant.MISSING;
                midP2 = Constant.MISSING;
                cMLE = Constant.MISSING;
                return;
            }
            //  Try on natural scale first then log scale if overflow
            CalcPoly(host, dataType, numTables, tables, out ierr);
            if (ierr == 7)
            {
                polyD = null;
                polyN = null;
                loFishLim = Constant.MISSING;
                upFishLim = Constant.MISSING;
                loMidPLim = Constant.MISSING;
                upMidPLim = Constant.MISSING;
                fishP1 = Constant.MISSING;
                fishP2 = Constant.MISSING;
                midP1 = Constant.MISSING;
                midP2 = Constant.MISSING;
                cMLE = Constant.MISSING;
                return;
            }
            cMLE = 0;
            if (ierr == 0)
            {
                CalcCmle(1.0, out cMLE, ref ierr);
            }
            if (ierr != 0)
            {
                logScale = true;
                CalcPoly(host, dataType, numTables, tables, out ierr);
                if (ierr == 0)
                    CalcCmle(1.0, out cMLE, ref ierr);
            }
            if (ierr == 0)
            {
                CalcExactLim(false, true, cMLE, confLevel, out upFishLim, ref ierr);
                CalcExactLim(true, true, cMLE, confLevel, out loFishLim, ref ierr);
                CalcExactLim(false, false, cMLE, confLevel, out upMidPLim, ref ierr);
                CalcExactLim(true, false, cMLE, confLevel, out loMidPLim, ref ierr);
                CalcExactPVals(out fishP1, out fishP2, out midP1, out midP2, ref ierr);
                useLogScale = logScale;
            }
            else
            {
                cMLE = Constant.MISSING;
                upFishLim = Constant.MISSING;
                loFishLim = Constant.MISSING;
                upMidPLim = Constant.MISSING;
                loMidPLim = Constant.MISSING;
                fishP1 = Constant.MISSING;
                fishP2 = Constant.MISSING;
                midP1 = Constant.MISSING;
                midP2 = Constant.MISSING;
            }
            polyD = null;
            polyN = null;
        }

        /// <summary>
        /// get log(exp(a)-exp(b)) avoiding overflow due to exp(a) or exp(b)
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        private static double SubLog(double a, double b)
        {
            if (a == b)
                return 0.0;
            double big = a > b ? a : b;
            return Math.Log(Math.Exp(a - big) - Math.Exp(b - big)) + big;
        }

        /// <summary>
        /// get log(exp(a)+exp(b)) avoiding overflow due to exp(a) or exp(b)
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        private static double SumLog(double a, double b)
        {
            double big = Math.Max(a, b);
            return Math.Log(Math.Exp(a - big) + Math.Exp(b - big)) + big;
        }

        /// <summary>
        /// Returns the combination y choose x
        /// </summary>
        /// <param name="y"></param>
        /// <param name="x"></param>
        /// <returns></returns>
        private static double Comb(double y, double x)
        {
            double f = 1.0;

            for (int i = 1; i <= Convert.ToInt32(Math.Min(x, y - x)); i++)
            {
                f = f * y / Convert.ToDouble(i);
                y -= 1.0;
            }
            return f;
        }

        /// <summary>
        /// This routine determines if the data allow exact estimates to be calculated.
        /// It MUST be called once prior to calling CalcPoly() given below.
        /// </summary>
        /// <param name="dataType">
        /// indicates the type of data to be analyzed (1 = stratified case-control,
        /// 2 = matched case-control, 3 = stratified person-time).</param>
        /// <param name="numTables"></param>
        /// <param name="tables"></param>
        /// <param name="ierr"> Exact estimates
        /// can only be calculated if ierr = 0.
        /// 
        /// Errors  0 = can calc exact estimates, i.e., no error,
        ///         1 = too much data (MAXDEGREE too small),
        ///         2 = no informative strata,
        ///         3 = matched table a > 1.
        ///</param>
        private void CheckData(int dataType, int numTables, Rec2X2[] tables, out int ierr)
        {
            int i;

            ierr = 0;

            if (dataType == 2)
            {
                for (i = 1; i <= numTables; i++)
                {
                    if (tables[i].A > 1.0)
                    {
                        ierr = 3;
                        return;
                    }
                }
            }

            // Compute the global vars SUMA, MINSUMA, MAXSUMA
            sumA = 0;
            minSumA = 0;
            maxSumA = 0;

            for (i = 1; i <= numTables; i++)
            {
                Rec2X2 transTemp2 = tables[i];
                if (transTemp2.Informative)
                {
                    sumA = Convert.ToInt32(transTemp2.A) * transTemp2.Freq + sumA;
                    if (dataType == 3)
                    {
                        // Person-time data
                        minSumA = 0;
                        maxSumA = Convert.ToInt32(transTemp2.M1) * transTemp2.Freq + maxSumA;
                    }
                    else
                    {
                        // Case-control or survival data
                        minSumA = Convert.ToInt32(Math.Max(0.0, transTemp2.M1 - transTemp2.N0)) * transTemp2.Freq + minSumA;
                        maxSumA = Convert.ToInt32(Math.Min(transTemp2.M1, transTemp2.N1)) * transTemp2.Freq + maxSumA;
                    }
                }

            }

            // Check for errors
            if (maxSumA - minSumA > MAXDEGREE)
            {
                // Poly too small
                ierr = 1;
            }
            else if (minSumA == maxSumA)
            {
                // No informative strata }
                ierr = 2;
            }

        }

        ///  <summary>
        ///  This routine multiplies together two polynomials P1 and P2 to obtain the product polynomial P3.
        ///  </summary>
        /// <param name="host"></param>
        /// <param name="p1"></param>
        ///  <param name="p2"></param>
        ///  <param name="deg1"></param>
        ///  <param name="deg2"></param>
        ///  <param name="p3"></param>
        ///  <param name="deg3"></param>
        ///  <param name="ierr"></param>
        /// <param name="job"></param>
        /// <remarks>Reference 'Algorithms 2nd ed.', by R. Sedgewick (Addison-Wesley, 1988), p. 522.</remarks>
        private void MultPoly(IPreferencesAndProgressBar host, double[] p1, double[] p2, int deg1, int deg2, double[] p3, out int deg3, out int ierr, string job)
        {
            deg3 = deg1 + deg2;
            bool couldBeSlow = Convert.ToDouble(deg1) * Convert.ToDouble(deg2) > 300000;

            if (logScale)
                for (int i = 0; i <= deg3; i++)
                    p3[i] = -Constant.MISSING;
            else
                for (int i = 0; i <= deg3; i++)
                    p3[i] = 0.0;

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
                            {
                                ierr = 1;
                                return;
                            }
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
                            {
                                ierr = 1;
                                return;
                            }
                        }
                    }

                    //  Test for overflow; if so, set an appropriate error value.
                    for (int i = 0; i <= deg3; i++)
                    {
                        if (double.IsInfinity(p3[i]) || double.IsNaN(p3[i]))
                        {
                            ierr = 6; //  Old VB6 code for an overflow
                            return;
                        }
                    }
                }
            }

            // If we get here, there were no errors
            ierr = 0;
        }

        ///  <summary>
        ///  Outputs to P the coefficients of the binomial expansion of (C0 + C1*R)^F.
        ///  </summary>
        ///  <param name="c0"></param>
        ///  <param name="c1"></param>
        ///  <param name="f"></param>
        ///  <param name="p"></param>
        ///  <param name="degP"></param>
        ///  <param name="ierr"></param>
        ///  <remarks>
        ///  An alternative to this Sub would be to multiply the polynomial
        ///  (C0 + C1*R) by itself (F-1) times but using the binomial expansion is much
        ///  faster.</remarks>
        private void BinomialExpansion(double c0, double c1, int f, double[] p, out int degP, ref int ierr)
        {
            degP = f;

            if (logScale)
            {
                p[degP] = Math.Log(c1) * Math.Log(Convert.ToDouble(degP));
                for (int i = degP - 1; i >= 0; i--)
                {
                    p[i] = p[i + 1] + Math.Log(c0) + Math.Log(Convert.ToDouble(i + 1)) - (Math.Log(c1) + Math.Log(Convert.ToDouble(degP - i)));
                }
            }
            else
            {
                p[degP] = Math.Pow(c1, Convert.ToDouble(degP));
                for (int i = degP - 1; i >= 0; i--)
                {
                    p[i] = p[i + 1] * c0 * Convert.ToDouble(i + 1) / (c1 * Convert.ToDouble(degP - i));
                    if (double.IsInfinity(p[i]) || double.IsNaN(p[i]))
                    {
                        ierr = 6; //  Old VB6 code for overflow
                        return;
                    }
                }
            }
        }

        ///  <summary>
        ///  This routine outputs the stratum-specific polynomial of conditional
        ///  distribution coefficients due to a SINGLE 2x2 table with person-count
        ///  denominators. If the 2x2 table is uninformative, then degDi is set to 0
        ///  and polyDi[0] to 1.0. Note that the coefficients are scaled so that
        ///  the first coefficient is equal to 1.0.
        ///  </summary>
        ///  <param name="table"></param>
        ///  <param name="polyDi"></param>
        ///  <param name="degDi"></param>
        ///  <param name="ierr"></param>
        ///  <remarks></remarks>
        private void PolyStratCc(ref Rec2X2 table, double[] polyDi, out int degDi, out int ierr)
        {
            degDi = 0;
            ierr = 0;
            polyDi[0] = logScale ? 0.0 : 1.0;

            Rec2X2 transTemp3 = table;
            if (transTemp3.Informative)
            {
                double minA = Math.Max(0.0, transTemp3.M1 - transTemp3.N0); // Min val of the "a" cell w/ these margins
                double maxA = Math.Min(transTemp3.M1, transTemp3.N1); // Max val of the "a" cell w/ these margins
                degDi = Convert.ToInt32(Math.Floor(maxA - minA)); // The degree of this table's polynomial - IEB 18 Jul 18: can be too large if input cell data are not integer so take floor

                // The polynomial coefficients are scaled so that the first
                // coef is 1.0. Note the recursive relation between coefficients.
                double aa = minA; // Corresponds to the "a" cell
                double bb = transTemp3.M1 - minA + 1.0; // Corresponds to the "b" cell
                double cc = transTemp3.N1 - minA + 1.0; // Corresponds to the "c" cell
                double dd = transTemp3.N0 - transTemp3.M1 + minA; // Corresponds to the "d" cell

                if (logScale)
                {
                    for (int i = 1; i <= degDi; i++)
                    {
                        double xi = Convert.ToDouble(i);
                        polyDi[i] = polyDi[i - 1] + Math.Log((bb - xi) / (aa + xi) * ((cc - xi) / (dd + xi)));
                    }
                }
                else
                {
                    for (int i = 1; i <= degDi; i++)
                    {
                        double xi = Convert.ToDouble(i);
                        polyDi[i] = polyDi[i - 1] * ((bb - xi) / (aa + xi)) * ((cc - xi) / (dd + xi));
                        //  Overflow test
                        if (double.IsInfinity(polyDi[i]) || double.IsNaN(polyDi[i]))
                        {
                            ierr = 6; //  Old VB6 code for overflow
                            return;
                        }
                    }
                }
            }

        }

        /// <summary>
        /// This routine outputs the polynomial of conditional distribution
        /// coefficients due to a single matched table. (A single matched table is
        /// generally equivalent to a number of sparse 2x2 tables.) If the table is
        /// uninformative (e.g., cases and controls all exposed), then degEi is set
        /// to 0 and polyEi[0] to 1.0.
        /// </summary>
        /// <param name="table"></param>
        /// <param name="polyEi"></param>
        /// <param name="degEi"></param>
        /// <param name="ierr"></param>
        private void PolyMatchCc(ref Rec2X2 table, double[] polyEi, out int degEi, ref int ierr)
        {
            degEi = 0;
            polyEi[0] = 1;

            if (table.Informative)
            {
                double c0 = Comb(table.N1, 0.0) * Comb(table.N0, table.M1);
                double c1 = Comb(table.N1, 1.0) * Comb(table.N0, table.M1 - 1.0);
                BinomialExpansion(c0, c1, table.Freq, polyEi, out degEi, ref ierr);
            }
        }

        /// <summary>
        /// This routine outputs the stratum-specific polynomial of conditional
        /// distribution coefficients due to a SINGLE 2x2 table with person-time
        /// denominators. If the 2x2 table is uninformative, then degDi is set to 0
        /// and polyDi[0] to 1.0.
        /// </summary>
        /// <param name="table"></param>
        /// <param name="polyDi"></param>
        /// <param name="degDi"></param>
        /// <param name="ierr"></param>
        /// <remarks>This routine is based on the algorithm discussed in
        /// the paper by Martin and Austin, 'Exact estimates for a rate ratio',
        /// Epidemiology (in press).
        /// </remarks>
        private void PolyStratPt1(Rec2X2 table, double[] polyDi, out int degDi, ref int ierr)
        {
            degDi = 0;
            polyDi[0] = 1.0;

            if (table.Informative)
                BinomialExpansion(table.N0 / table.N1, 1.0, Convert.ToInt32(table.M1), polyDi, out degDi, ref ierr);
        }

/*
        ///  <summary>
        ///  This routine outputs the stratum-specific polynomial of conditional
        ///  distribution coefficients due to a SINGLE 2x2 table with person-time
        ///  denominators. If the 2x2 table is uninformative, then degDi is set to 0
        ///  and polyDi[0] to 1.0.
        ///  </summary>
        ///  <param name="table"></param>
        ///  <param name="polyDi"></param>
        ///  <param name="degDi"></param>
        ///  <param name="ierr"></param>
        ///  <remarks>
        ///  This routine is an alternative to PolyStratPT1. It is based on
        ///  PolyStratCC() and the idea that by letting the c and d cells of a 2x2
        ///  table approach infinity, the noncentral hypergeometric becomes binomial.
        ///  Unlike the above routine, this routine scales the coefficients so that
        ///  the first coefficient is always 1.0.
        ///  </remarks>
        private void PolyStratPt2(ref Rec2X2 table, ref double[] polyDi, out int degDi, ref int ierr)
        {
            degDi = 0;
            polyDi[0] = 1.0;

            if (table.Informative)
            {
                degDi = Convert.ToInt32(table.M1); // The degree of this table's polynomial

                // The polynomial coefficients are scaled so that the first
                // coef is 1.0. Note the recursive relation between coefficients

                double aa = 0; // Corresponds to the "a" cell
                double bb = table.M1 + 1; // Corresponds to the "b" cell
                if (logScale)
                {
                    for (int i = 1; i <= degDi; i++)
                    {
                        double xi = Convert.ToDouble(i);
                        polyDi[i] = polyDi[i - 1] + Math.Log(((bb - xi) / (aa + xi)) * (table.N1 / table.N0));
                    }
                }
                else
                {
                    for (int i = 1; i <= degDi; i++)
                    {
                        double xi = Convert.ToDouble(i);
                        polyDi[i] = polyDi[i - 1] * ((bb - xi) / (aa + xi)) * (table.N1 / table.N0);
                        //  Overflow test
                        if (double.IsInfinity(polyDi[i]) || double.IsNaN(polyDi[i]))
                        {
                            ierr = 6; //  Old VB6 code for overflow
                            return;
                        }
                    }
                }
            }
        }
*/

        ///  <summary>
        ///  This routine outputs the "main" polynomial of conditional distribution
        ///  coefficients which will subsequently be used to calculate the conditional
        ///  maximum likelihood estimate, exact confidence limits, and exact P-values.
        ///  For a given data set, this routine MUST be called once before calling
        ///  CalcExactPVals(), CalcCmle(), and CalcExactLim().
        ///  </summary>
        /// <param name="host"></param>
        /// <param name="dataType">indicates the type of data to be analyzed
        ///  (1 = stratified case-control,
        ///  2 = matched case-control, 3 = stratified person-time).</param>
        ///  <param name="numTables"></param>
        ///  <param name="tables"></param>
        ///  <param name="ierr"></param>
        ///  <remarks></remarks>
        private void CalcPoly(IPreferencesAndProgressBar host, int dataType, int numTables, Rec2X2[] tables, out int ierr)
        {
            ierr = 0;

            int polydim = maxSumA - minSumA;
            double[] poly1 = new double[polydim + 1];
            double[] poly2 = new double[polydim + 1];
            polyD = new double[polydim + 1];
            polyN = new double[polydim + 1];

            switch (dataType)
            {
                case 1:
                case 4:
                    PolyStratCc(ref tables[1], polyD, out degD, out ierr); // Stratified case-control/survival
                    break;
                case 2:
                    PolyMatchCc(ref tables[1], polyD, out degD, ref ierr); // Matched case-control
                    break;
                case 3:
                    PolyStratPt1(tables[1], polyD, out degD, ref ierr); // Stratified person-time
                    break;
            }

            if (ierr != 0)
            {
                return;
            }

            // Multiply polynomials
            for (int i = 2; i <= numTables; i++)
            {
                if (tables[i].Informative)
                {
                    int deg1 = degD;
                    int deg2;
                    Array.Copy(polyD, poly1, polyD.Length);
                    switch (dataType)
                    {
                        case 1:
                        case 4:
                            PolyStratCc(ref tables[i], poly2, out deg2, out ierr); // Stratified case-control
                            break;
                        case 2:
                            PolyMatchCc(ref tables[i], poly2, out deg2, ref ierr); // Matched case-control }
                            break;
                        case 3:
                            PolyStratPt1(tables[i], poly2, out deg2, ref ierr); // Stratified person-time }
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(dataType), dataType, "Datatype must be 1 to 4");
                    }

                    if (ierr != 0)
                    {
                        return;
                    }
                    MultPoly(host, poly1, poly2, deg1, deg2, polyD, out degD, out ierr, (i - 1).ToString() + " of " + (numTables - 1).ToString());
                    if (ierr != 0)
                    {
                        return;
                    }
                }
            }
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
        private double EvalPoly(double[] c, int degC, double r)
        {
            int i;
            double y; double z;

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
                        for (i = degC - 1; i >= 0; i--)
                        {
                            y = SumLog(y + Math.Log(r), c[i]);
                        }
                    }
                    else
                    {
                        for (i = degC - 1; i >= 0; i--)
                        {
                            y = SumLog(y, c[i]);
                        }
                    }
                }
                else
                {
                    y = c[0];
                    z = Math.Log(1.0 / r);
                    for (i = 1; i <= degC; i++)
                    {
                        y = SumLog(y + z, c[i]);
                    }
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
                        for (i = degC - 1; i >= 0; i--)
                        {
                            y = y * r + c[i];
                        }
                    }
                    else
                    {
                        for (i = degC - 1; i >= 0; i--)
                        {
                            y += c[i];
                        }
                    }
                }
                else
                {
                    y = c[0];
                    z = 1.0 / r;
                    for (i = 1; i <= degC; i++)
                    {
                        y = y * z + c[i];
                    }
                }

            }

            return y;
        }

        private double Func(double r, out int ierr)
        {
            ierr = 0;
            // The root (value at which func = 0) of this function is the conditional MLE of the common odds ratio
            // or common rate ratio, or an exact confidence limit depending on how the
            // global variables VALUE, POLYN, and POLYD are defined.

            double numer = EvalPoly(polyN, degN, r);
            double denom = EvalPoly(polyD, degD, r);

            if (logScale)
            {
                if (r <= 1.0)
                    return Math.Exp(numer - denom) - value;
                if (degD - degN == 0)
                    return Math.Exp(numer - denom) - value;
                return Math.Exp(numer - Math.Log(r) * Convert.ToDouble(degD - degN) - denom) - value;
            }
            if (denom == 0.0)
            {
                ierr = 6;
                return 0;
            }
            if (r <= 1.0)
                return numer / denom - value;
            return numer / Math.Pow(r, degD - degN) / denom - value;
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
            {
                return;
            }
            f1 = Func(x1, out ierr); // Func at X1
            if (ierr != 0)
            {
                return;
            }

            // if necessary, increase X1 until F1 and F0 have different signs
            while ((f1 * f0 > 0.0) & (iter < MAX_ITER))
            {
                iter += 1;
                x0 = x1;
                f0 = f1;
                x1 = x1 * 1.5 * iter;
                f1 = Func(x1, out ierr);
                if (ierr != 0)
                {
                    return;
                }
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
        private void Zero(ref double x0, ref double x1, ref double f0, ref double f1, out double root, out int ierr)
        {
            ierr = 0; // Initialize
            int iter = 0;

            if (Math.Abs(f0) < Math.Abs(f1))
            { // Make X1 best approx to root

                double swap = x0;
                x0 = x1;
                x1 = swap;
                swap = f0;
                f0 = f1;
                f1 = swap;
            }

            bool found = f1 == 0.0;
            if (found == false && f0 * f1 > 0.0)
            {
                ierr = 1; // Root not bracketed
            }

            // Converge to root
            while (found == false && iter < MAX_ITER && ierr == 0)
            {
                iter += 1;
                double x2 = x1 - f1 * (x1 - x0) / (f1 - f0);
                double f2 = Func(x2, out ierr);
                if (ierr != 0)
                {
                    root = Constant.MISSING;
                    return;
                }
                if (f1 * f2 < 0.0)
                { // X0 not retained

                    x0 = x1;
                    f0 = f1;
                }
                else
                { // X0 retained => modify F0

                    f0 = f0 * f1 / (f1 + f2); // The Pegasus modification
                }
                x1 = x2;
                f1 = f2;
                found = (Math.Abs(x1 - x0) < Math.Abs(x1) * TOLERANCE) | (f1 == 0.0);
            }

            root = x1; // Estimated root
            if (!found & (iter >= MAX_ITER) & (ierr == 0))
            {
                ierr = 2; // Too many iterations
            }

        }

        /// <summary>
        /// This routine returns the root of Func above on the interval [0, infinity).
        /// </summary>
        /// <param name="approx"></param>
        /// <param name="root"></param>
        /// <param name="ierr"></param>
        private void Converge(double approx, out double root, out int ierr)
        {
            double f1 = 0;

            if (double.IsInfinity(approx)) approx=1.0;
            BracketRoot(approx, out double x0, out double x1, out double f0, ref f1, out ierr);
            if (ierr != 0)
            {
                root = Constant.MISSING;
                return;
            }

            // alternative Brent root method - good if tol <1e-15
            // root = BrentRoot(x0, x1, ierr)
            Zero(ref x0, ref x1, ref f0, ref f1, out root, out ierr);

        }

        /// <summary>
        /// This routine returns the exact P-values as defined in 'Modern
        /// Epidemiology ' by K. J. Rothman (Little, Brown, and Co., 1986).
        /// </summary>
        /// <param name="fishP1"></param>
        /// <param name="fishP2"></param>
        /// <param name="midP1"></param>
        /// <param name="midP2"></param>
        /// <param name="ierr"></param>
        private void CalcExactPVals(out double fishP1, out double fishP2, out double midP1, out double midP2, ref int ierr)
        {
            int i;
            double denom; double upFishPVal; double loFishPVal; double upMidPPVal; double loMidPPVal;

            int diff = sumA - minSumA;
            double upTail = polyD[degD];
            double upZ = polyD[degD] <= polyD[diff] ? polyD[degD] : 0.0;
            double loZ = 0.0;

            if (logScale)
            {
                for (i = degD - 1; i >= diff; i--)
                {
                    upTail = SumLog(upTail, polyD[i]);
                    if (polyD[i] <= polyD[diff])
                    {
                        upZ = SumLog(upZ, polyD[i]);
                    }
                }
                denom = upTail;
                for (i = diff - 1; i >= 0; i--)
                {
                    denom = SumLog(denom, polyD[i]);
                    if (polyD[i] <= polyD[diff])
                    {
                        loZ = SumLog(loZ, polyD[i]);
                    }
                }
                upFishPVal = ZExp(upTail - denom, out ierr);
                loFishPVal = 1.0 - ZExp(SubLog(upTail, polyD[diff]) - denom, out ierr);
                fishP1 = Math.Min(upFishPVal, loFishPVal);
                fishP2 = ZExp(SumLog(upZ, loZ) - denom, out ierr);
                upMidPPVal = ZExp(SubLog(upTail, Math.Log(0.5) + polyD[diff]) - denom, out ierr);
                loMidPPVal = 1.0 - upMidPPVal;
                midP1 = Math.Min(upMidPPVal, loMidPPVal);
                midP2 = Math.Min(2.0 * midP1, 1.0);
            }
            else
            {
                for (i = degD - 1; i >= diff; i--)
                {
                    upTail += polyD[i];
                    if (polyD[i] <= polyD[diff])
                    {
                        upZ += polyD[i];
                    }
                }
                denom = upTail;
                for (i = diff - 1; i >= 0; i--)
                {
                    denom += polyD[i];
                    if (polyD[i] <= polyD[diff])
                    {
                        loZ += polyD[i];
                    }
                }
                if (denom == 0)
                {
                    ierr = 6;
                    fishP1 = Constant.MISSING; 
                    fishP2 = Constant.MISSING; 
                    midP1 = Constant.MISSING; 
                    midP2 = Constant.MISSING; 
                }
                else
                {
                    upFishPVal = upTail / denom;
                    loFishPVal = 1.0 - (upTail - polyD[diff]) / denom;
                    fishP1 = Math.Min(upFishPVal, loFishPVal);
                    fishP2 = (upZ + loZ) / denom;
                    upMidPPVal = (upTail - 0.5 * polyD[diff]) / denom;
                    loMidPPVal = 1.0 - upMidPPVal;
                    midP1 = Math.Min(upMidPPVal, loMidPPVal);
                    midP2 = Math.Min(2.0 * midP1, 1.0);
                }
            }
        }

        /// <summary>
        /// This routine returns the conditional maximum likelihood estimate of the
        /// common odds ratio or common rate ratio. APPROX may be set to 1.0 if no
        /// estimate is available, but the solution is obtained faster with a good
        /// approximation. CMLE returns as Constant.MISSING if convergence to a solution did not
        /// occur in MAXITER iterations.
        /// </summary>
        /// <param name="approx"></param>
        /// <param name="cMLE"></param>
        /// <param name="ierr"></param>
        private void CalcCmle(double approx, out double cMLE, ref int ierr)
        {
            if (minSumA < sumA && sumA < maxSumA)
            { // Can calc point estimate
                value = sumA; // The sum of the observed "a" cells
                degN = degD; // Degree of the numerator polynomial
                int i;
                if (logScale)
                {
                    for (i = 0; i <= degN; i++)
                    { // Defines the numerator polynomial

                        if (minSumA + i == 0)
                        {
                            polyN[i] = -1.0E+300; // safe to use v. small number as exp(<minexp) does not underflow but returns 0
                        }
                        else
                        {
                            polyN[i] = Math.Log(Convert.ToDouble(minSumA + i)) + polyD[i];
                        }
                    }
                }
                else
                {
                    for (i = 0; i <= degN; i++)
                    { // Defines the numerator polynomial

                        polyN[i] = Convert.ToDouble(minSumA + i) * polyD[i];
                    }
                }
                Converge(approx, out cMLE, out ierr); // Solves so that Func(cmle) = 0
                if (ierr != 0)
                {
                    cMLE = Constant.MISSING;
                } // Failed convergence
            }
            else if (sumA == minSumA)
            { // Point estimate = 0

                cMLE = 0.0;
            }
            else if (sumA == maxSumA)
            { // Point estimate = inf

                cMLE = double.PositiveInfinity;
            }
            else
            {
                // Something odd happened.
                cMLE = Constant.MISSING;
            }
        }

        /// <summary>
        /// This routine returns an exact confidence limit for the common odds ratio
        /// or common rate ratio with the level of confidence determined by CONFLEVEL
        /// which *must* satisfy 0 &lt;= CONFLEVEL &lt; 1. APPROX may be set to 1.0 if no
        /// estimate is available, but the solution is obtained faster with a good
        /// approximation. LIMIT returns as Constant.MISSING if convergence to a solution did not
        /// occur in MAXITER iterations.
        /// </summary>
        /// <param name="lower"></param>
        /// <param name="fisher"></param>
        /// <param name="approx"></param>
        /// <param name="confLevel"></param>
        /// <param name="limit"></param>
        /// <param name="ierr"></param>
        private void CalcExactLim(bool lower, bool fisher, double approx, double confLevel, out double limit, ref int ierr)
        {
            if (sumA == minSumA)
            { // Point estimate = 0 => lower limit = 0

                if (lower)
                {
                    limit = 0;
                    return;
                }
            }
            else if (sumA == maxSumA)
            { // Point estimate = inf => upper limit = inf

                if (lower == false)
                {
                    limit = double.PositiveInfinity;
                    return;
                }
            }

            if (lower)
            {
                value = 0.5 * (1.0 + confLevel); // = 1 - alpha / 2
            }
            else
            {
                value = 0.5 * (1.0 - confLevel); // = alpha / 2
            }

            // Degree of numerator polynomial
            if (lower && fisher)
            {
                degN = sumA - minSumA - 1;
            }
            else
            {
                degN = sumA - minSumA;
            }

            Array.Copy(polyD, polyN, polyD.Length);

            // Mid-P adjustment
            if (logScale)
            {
                if (!fisher)
                {
                    polyN[degN] = polyD[degN] - Math.Log(2.0);
                }
            }
            else
            {
                if (!fisher)
                {
                    polyN[degN] = 0.5 * polyD[degN];
                }
            }

            Converge(approx, out limit, out ierr); // Solves so that Func(limit) = 0

            if (ierr != 0)
            {
                limit = Constant.MISSING;
            } // Failed convergence
        }

        private static double ZExp(double z, out int ierr)
        {
            // no need to check for z<minexp as exp in vb returns 0 and does not underflow
            if (z > MAXEXP)
            {
                ierr = 6;
                return Constant.MISSING;
            }
            ierr = 0;
            return Math.Exp(z);
        }

        public static void OddsRatioCI(IPreferencesAndProgressBar host, double cco, double a, double b, double c, double d, out double eor, out double llf, out double ulf, out bool lerr, out bool uerr)
        {
            if (a == 0 && b == 0 || c == 0 && d == 0)
            {
                ulf = double.PositiveInfinity;
                llf = 0;
                eor = 0;
            }
            else
            {
                if (a * d != 0 || b * c != 0)
                {
                    Rec2X2[] tabl = new Rec2X2[1 + 1];
                    tabl[1].Freq = 1;
                    tabl[1].A = a;
                    tabl[1].M1 = a + b;
                    tabl[1].N1 = a + c;
                    tabl[1].N0 = b + d;
                    tabl[1].Informative = a * d != 0 || b * c != 0;
                    bool useLogScale = false;
                    new ExactBB().Exact22K(host, 1, 1, tabl, cco, out eor, out ulf, out llf, out double _, out double _, out double _, out double _, out double _, out double _, ref useLogScale, out int _);
                }
                else
                {
                    eor = Constant.MISSING;
                    llf = Constant.MISSING;
                    ulf = Constant.MISSING;
                }
                if ((a == 0) | (d == 0))
                {
                    eor = 0;
                    llf = 0;
                }
                else if ((b == 0) | (c == 0))
                {
                    eor = double.PositiveInfinity;
                    ulf = double.PositiveInfinity;
                }
            }
            lerr = llf == Constant.MISSING;
            uerr = ulf == Constant.MISSING;
        }
        
        public static void OddsRatioCMLE(IPreferencesAndProgressBar host, double cco, double a, double b, double c, double d, out double eor, out double llf, out double ulf, out double llm, out double ulm, out double p1f, out double p2f, out double p1m, out double p2m, out int ierr)
        {
            eor = Constant.MISSING;
            llf = Constant.MISSING;
            ulf = Constant.MISSING;
            llm = Constant.MISSING;
            ulm = Constant.MISSING;
            p1f = Constant.MISSING;
            p2f = Constant.MISSING;
            p1m = Constant.MISSING;
            p2m = Constant.MISSING;
            ierr = 0;
            if (a == 0 && b == 0 || c == 0 && d == 0)
            {
                ulf = double.PositiveInfinity;
                llf = 0.0;
                eor = 0.0;
            }
            else
            {
                if (a * d != 0 || b * c != 0)
                {
                    Rec2X2[] tabl = new Rec2X2[1 + 1];
                    tabl[1].Freq = 1;
                    tabl[1].A = a;
                    tabl[1].M1 = a + b;
                    tabl[1].N1 = a + c;
                    tabl[1].N0 = b + d;
                    tabl[1].Informative = a * d != 0 || b * c != 0;
                    bool useLogScale = false;
                    new ExactBB().Exact22K(host, 1, 1, tabl, cco, out eor, out ulf, out llf, out ulm, out llm, out p1f, out p2f, out p1m, out p2m, ref useLogScale, out ierr);
                }
                if ((a == 0) | (d == 0))
                {
                    eor = 0;
                    llf = 0;
                }
                else if ((b == 0) | (c == 0))
                {
                    eor = double.PositiveInfinity;
                    ulf = double.PositiveInfinity;
                }
            }
        }

        public static double OddsRatio(double diseasedExposed, double healthyExposed, double diseasedNotExposed, double healthyNotExposed)
        {
            if (diseasedExposed == 0 || healthyNotExposed == 0)
                return 0;
            if (healthyExposed == 0 || diseasedNotExposed == 0)
                return double.PositiveInfinity;
            return diseasedExposed * healthyNotExposed / (healthyExposed * diseasedNotExposed);
        }
    }
}
