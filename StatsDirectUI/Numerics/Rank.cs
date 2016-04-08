using System;

namespace StatsDirect.Numerics
{
    public partial class ExFortran
    {
        ///  <summary>
        ///  Ranks the input values in a, returning the ranks in r.  r need not be initialised; it will be blanked.
        ///  </summary>
        ///  <param name="a">LowerBound-based input vector of n values</param>
        ///  <param name="r">LowerBound-based output vector of length n. smallest value is ranked 1, largest is ranked n. ties are assigned average of tied ranks</param>
        ///  <param name="lowerBound">The lower bound of the array being passed in, typically 0 or 1.</param>
        ///  <param name="n">number of values passed in (so upper bound is n + LowerBound - 1)</param>
        ///  <param name="qt">input code for calculation of correction factor:
        ///           qt = 1: xf=sum((ntie^3-ntie)/12.0)
        ///           qt = 2: xf=sum(ntie*(ntie-1.0)/2.0)
        ///           qt = 3: xf=sum(ntie*(ntie-1)*(2*ntie+5)
        ///           qt = 4: xf=sum(ntie*(ntie-1)*(ntie-2))
        ///           qt = 5: xf=sum(ntie*(ntie-1)*(ntie+1))
        ///           where ntie is the number of observations tied for a given rank</param>
        ///  <param name="xf">correction factor</param>
        ///  <remarks></remarks>
        public static void Rank(double[] a, double[] r, int lowerBound, int n, int qt, out double xf)
        {
            int i;
            int upperBound = n + lowerBound - 1;
            for (i = lowerBound; i <= upperBound; i++)
            {
                r[i] = 0.0;
            }
            xf = 0.0;
            //      find ranks 
            for (i = lowerBound; i <= upperBound; i++)
            {
                //         test whether point already ranked
                if (r[i] <= 0.0)
                {
                    //            data point to be ranked
                    int nxlt = 0;
                    int ntie = 0;
                    double x = a[i];
                    int j;
                    for (j = lowerBound; j <= upperBound; j++)
                    {
                        if (a[j] < x)
                        {
                            //                  count number of data points which are smaller
                            nxlt = nxlt + 1;
                        }
                        else if (a[j] == x)
                        {
                            //                  count number of data points which are equal.
                            //                  mark these by setting their ranks to -1.
                            ntie = ntie + 1;
                            r[j] = -1.0;
                        }
                    }
                    //            test for tie
                    if (ntie <= 1)
                    {
                        //               store rank of untied data points
                        r[i] = Convert.ToDouble(nxlt) + 1.0;
                        //               store rank of tied data points
                    }
                    else if (ntie > 1)
                    {
                        double p;
                        if (ntie % 2 == 0)
                        {
                            p = Convert.ToDouble(nxlt) + Convert.ToDouble(ntie / 2) + 0.5;
                        }
                        else
                        {
                            p = Convert.ToDouble(nxlt) + Convert.ToDouble((ntie + 1) / 2);
                        }
                        for (j = i; j <= upperBound; j++)
                        {
                            if (r[j] == -1.0)
                            {
                                r[j] = p;
                            }
                        }
                        if (qt == 1)
                        {
                            xf = xf + (Math.Pow(ntie, 3) - ntie) / 12.0;
                        }
                        else if (qt == 2)
                        {
                            xf = xf + ntie * (ntie - 1.0) / 2.0;
                        }
                        else if (qt == 3)
                        {
                            xf = xf + Convert.ToDouble(ntie * (ntie - 1) * (2 * ntie + 5));
                        }
                        else if (qt == 4)
                        {
                            xf = xf + Convert.ToDouble(ntie * (ntie - 1) * (ntie - 2));
                        }
                        else if (qt == 5)
                        {
                            xf = xf + Convert.ToDouble(ntie * (ntie - 1) * (ntie + 1));
                        }
                    }
                }
            }
        }


        ///  <summary>
        ///  this routine calculates the lower tail probability p for
        ///  the wilcoxon-mann-whiney statistic u for sample sizes
        ///  n1 and n2 for the case of ties in the pooled sample.
        ///  see procedure wmw_dist in
        ///  neumann, n. - some procedures for calculating the distributions of elementary nonparametric statistics. stat. software newsletter, vol. 14, no 3., 1988
        ///  </summary>
        ///  <param name="n1"></param>
        ///  <param name="n2"></param>
        ///  <param name="iwrk">A 1-based array of integers</param>
        ///  <param name="iv"></param>
        ///  <param name="p"></param>
        ///  <param name="ifault">0 if no errors, set to non-zero if there was an error</param>
        ///  <remarks></remarks>
        public static void wmwpx(int n1, int n2, ref int[] iwrk, int iv, ref double p, out int ifault)
        {
            int dummy;
            int i; int ir1;
            int m; int m1; int m2; int mwmax; int n;
            int i1 = 0;
            bool change;
            ifault = 0;
            int nn = Math.Min(n1, n2);
            int nsum = n1 + n2;
            int nwrk = nn + nn * (nn + 1) * nsum - ((int)(Math.Floor((double)(nn * (nn + 1) * (2 * nn + 1)) / 3))) + 1;
            double[] wrk = new double[nwrk + 1];
            if (i1 != 0)
            {
                ifault = 10;
                return;
            }
            if (n1 < n2)
            {
                m1 = n1;
                m2 = n2;
                change = false;
            }
            else
            {
                m1 = n2;
                m2 = n1;
                change = true;
            }
            int l1 = nsum;
            int l2 = nsum + m1 + 1;
            int low = 0;
            int high = 0;
            int space = 0;
            iwrk[l1 + 1] = 0;
            iwrk[l2 + 1] = 0;
            for (m = 1; m <= m1; m++)
            {
                iwrk[l1 + m + 1] = 0;
                iwrk[l2 + m + 1] = space + 1;
                low = low + iwrk[m];
                high = high + iwrk[nsum + 1 - m];
                dummy = high - low + 1;
                space = space + dummy;
            }
            if ((change))
            {
                mwmax = nsum * (nsum + 1) - low;
            }
            else
            {
                mwmax = high;
            }
            if (iv == mwmax - n1 * (n1 + 1))
            {
                p = 1.0;
                return;
            }
            for (i = 0; i <= space; i++)
            {
                wrk[i + 1] = 1.0;
            }
            for (n = 1; n <= nsum; n++)
            {
                dummy = Math.Min(n, m1);
                for (m = dummy; m >= 1; m--)
                {
                    int shift = iwrk[n] - iwrk[m];
                    iwrk[l1 + m + 1] = iwrk[l1 + m] + shift;
                    double lambda = Convert.ToDouble(m) / Convert.ToDouble(n);
                    int j;
                    for (j = 0; j <= iwrk[l1 + m + 1]; j++)
                    {
                        int k = iwrk[l2 + m + 1] + j;
                        wrk[k + 1] = (1.0 - lambda) * wrk[k + 1];
                        if (shift <= j)
                        {
                            wrk[k + 1] = wrk[k + 1] + lambda * wrk[iwrk[l2 + m] + j - shift + 1];
                        }
                    }
                }
            }
            if (!(change))
            {
                ir1 = low - m1 * (m1 + 1);
                p = wrk[iwrk[l2 + m1 + 1] + iv - ir1 + 1];
            }
            else
            {
                ir1 = (nsum * (nsum + 1) - high) - m2 * (m2 + 1);
                p = 1.0 - wrk[iwrk[l2 + m1 + 1] + iwrk[l1 + m1 + 1] - iv + ir1];
            }
        }

        public static int findnext(out int occ, int c, int[] x, int lenx, int[] y, int leny)
        {
            int yii = 1;
            int xii = 1;
            int xki = lenx;
            int yki = 1;
            const int ymini = 1;
            int xmaxi = lenx;
            int ymaxi = leny;
            occ = 1;
            while (x[xii] - c <= y[ymini] && xii != xmaxi)
            {
                xii = xii + 1;
            }
            if (xii == xmaxi)
            {
                return x[xki] - y[yki];
            }
            do
            {
                while (x[xii] - y[yii] > c)
                {
                    if (x[xii] - y[yii] == x[xki] - y[yki])
                    {
                        occ = occ + 1;
                    }
                    if (x[xki] - y[yki] > x[xii] - y[yii] && x[xii] - y[yii] != c)
                    {
                        occ = 1;
                        yki = yii;
                        xki = xii;
                    }
                    if (yii == ymaxi)
                        break;
                    yii++;
                }
                if (yii > ymini)
                {
                    yii--;
                }
                while (yii > ymini && y[yii] == y[yii - 1])
                {
                    yii--;
                }
                if (xii == xmaxi)
                    break;
                xii++;
            }
            while (true);
            return x[xki] - y[yki];
        }

        public static long pairnext(out int occ, long c, long[] x, int lenx)
        {
            occ = 1;
            int xii = 1;
            int yki = lenx;
            int yii = 1;
            int ymini = lenx;
            int xki = lenx;
            int xmaxi = lenx;
            const int ymaxi = 1;
            while (x[xii] - c <= -x[ymini] && xii != xmaxi)
            {
                xii = xii + 1;
                yii = xii;
            }
            if (xii == xmaxi)
            {
                return x[xki] + x[yki];
            }
            do
            {
                while (x[xii] + x[yii] > c)
                {
                    if (x[xii] + x[yii] == x[xki] + x[yki])
                    {
                        occ = occ + 1;
                    }
                    if (x[xki] + x[yki] > x[xii] + x[yii] && x[xii] + x[yii] != c)
                    {
                        occ = 1;
                        yki = yii;
                        xki = xii;
                    }
                    if (yii == ymaxi)
                        break;
                    yii = yii - 1;
                }
                if (ymini > yii)
                {
                    yii = yii + 1;
                }
                while (xii >= yii && ymini > yii && x[yii] == x[yii + 1])
                {
                    yii = yii + 1;
                }
                if (xii == xmaxi)
                    break;
                xii = xii + 1;
            }
            while (true);
            return x[xki] + x[yki];
        }

        ///  <summary>
        ///  Returns upper side probability associated with:
        ///  Spearman score statistic ix
        ///  n pairs of obserations
        /// 
        ///  To evaluate the probability of obtaining a value greater than or
        ///  equal to is, where is=(n**3-n)*(1-r)/6, r=Spearman's rho and n
        ///  must be greater than 1
        ///  </summary>
        ///  <param name="n">Number of pairs of observations</param>
        ///  <param name="ix">Spearman score statistic</param>
        ///  <param name="ifault"></param>
        ///  <remarks>
        ///  Modified Algorithm AS 89   Appl. Statist. (1975) Vol.24, No. 3, P377.
        /// 
        ///  7/7/2002 Dr Iain Buchan (StatsDirect Ltd)
        ///  translated to FORTRAN 90;
        ///  increased exact enumeration from 7 to 10 pairs of observations.
        ///  corrected exact enumeration (values at least as extreme rather than more extreme);
        ///  </remarks>
        public static double prho(int n, int ix, out int ifault)
        {
            ifault = 1;
            if (n <= 1)
                return 1.0;

            ifault = 0;
            if ((ix < 0))
                return 1.0;

            if ((ix > Math.Floor((double)n * (n * n - 1) / 3.0)))
                return 0.0;

            int js = ix;
            if ((js != 2 * Math.Floor(js / 2.0)))
                js += 1;

            if ((n <= 10))
            {
                //  Exact evaluation for 10 or fewer pairs of observations
                int[] l = new int[11];
                int nfac = 1;
                for (int i = 1; i <= n; i++)
                {
                    nfac *= i;
                    l[i] = i;
                }
                if ((js == Math.Floor((double)n * (n * n - 1) / 3.0)))
                    return 1.0 / Convert.ToDouble(nfac);

                int ifr = 0;
                for (int m = 1; m <= nfac; m++)
                {
                    int ise = 0;
                    for (int i = 1; i <= n; i++)
                        ise += (i - l[i]) * (i - l[i]);
                    if ((js < ise))
                        ifr++;
                    int n1 = n;
                    do
                    {
                        int mt = l[1];
                        int nn = n1 - 1;
                        for (int i = 1; i <= nn; i++)
                            l[i] = l[i + 1];
                        l[n1] = mt;
                        if ((l[n1] != n1 || n1 == 2))
                            break;
                        n1--;
                        if ((m == nfac))
                            break;
                    }
                    while (true);
                }
                return Convert.ToDouble(ifr) / Convert.ToDouble(nfac);
            }
            else
            {
                //  Evaluation by Edgeworth series expansion
                double b = 1.0 / Convert.ToDouble(n);
                double x = (6.0 * (Convert.ToDouble(js) - 1.0) * b / (1.0 / (b * b) - 1.0) - 1.0) * Math.Sqrt(1.0 / b - 1.0);
                double y = x * x;
                double u = x * b * (0.2274 + b * (0.2531 + 0.1745 * b) + y * (-0.0758 + b * (0.1033 + 0.3932 * b) - y * b * (0.0879 + 0.0151 * b - y * (0.0072 - 0.0831 * b + y * b * (0.0131 - 0.00046 * y)))));
                double prhoReturn = u / Math.Exp(y / 2.0) + 1.0 - PDF.alnorm(x);
                if ((prhoReturn < 0.0))
                    return 0.0;
                if ((prhoReturn > 1.0))
                    return 1.0;
                return prhoReturn;
            }
        }

        public static double ksp2(int n1, int n2, ref double d, out int ifault)
        {
            ifault = 0;
            double p;
            if ((d < Constant.DBL_MIN))
            {
                return 1.0;
            }
            int m = Math.Min(n1, n2);
            int n = Math.Max(n1, n2);
            if ((m * n <= 10000))
            {
                double[] u = new double[n + 2];
                double x = Convert.ToDouble(m * n) * d - 0.5;
                u[1] = 1.0;
                for (int j = 1; j <= n; j++)
                {
                    u[j + 1] = 1.0;
                    if ((Convert.ToDouble(m * j) > x))
                    {
                        u[j + 1] = 0.0;
                    }
                }
                for (int i = 1; i <= m; i++)
                {
                    double w = Convert.ToDouble(i) / Convert.ToDouble(i + n);
                    u[1] = w * u[1];
                    if ((Convert.ToDouble(n * i) > x))
                    {
                        u[1] = 0.0;
                    }
                    for (int j = 1; j <= n; j++)
                    {
                        u[j + 1] = u[j] + u[j + 1] * w;
                        if ((Convert.ToDouble(Math.Abs(n * i - m * j)) > x))
                        {
                            u[j + 1] = 0.0;
                        }
                    }
                }
                p = u[n + 1];
                p = 1.0 - p;
                p = Math.Min(1.0, p);
                p = Math.Max(0.0, p);
            }
            else if ((m < Math.Floor((double)n / 10) && m < 80))
            {
                double z = d;
                if ((m != 1))
                {
                    z = z - 0.5 / Convert.ToDouble(n);
                }
                z = Math.Max(0.0, z);
                double tp = 2.0 * kspx(m, z);
                p = Math.Min(1.0, tp);
            }
            else
            {
                double z = Math.Sqrt(Convert.ToDouble(m * n) / Convert.ToDouble(m + n)) * d + 0.5 / Math.Sqrt(Convert.ToDouble(n));
                double a = -2.0 * z * z;
                if ((-a < Constant.DBL_MIN))
                {
                    return 1.0;
                }
                double sr = Math.Sqrt(Math.Log(Constant.DBL_MIN) / a);
                double fac = 2.0;
                p = 0.0;
                const double eps1 = 0.000005;
                for (int j = 1; j <= 500; j++)
                {
                    double xj = Convert.ToDouble(j);
                    if ((xj < sr))
                    {
                        double term = fac * Math.Exp(a * xj * xj);
                        p = p + term;
                        double aterm = Math.Abs(term);
                        if ((aterm < eps1 * p))
                        {
                            p = Math.Min(1.0, p);
                            return p;
                        }
                        fac = -fac;
                    }
                    else
                    {
                        p = Math.Min(1.0, p);
                        return p;
                    }
                }
                //  fails to converge, P set to 1.0
                ifault = 3;
                return 1.0;
            }
            return p;
        }

        private static double kspx(int n, double d)
        {
            double p;
            if ((d < Constant.DBL_MIN))
            {
                p = 1.0;
            }
            else if (((1.0 - d) < Constant.DBL_MIN))
            {
                p = 0.0;
            }
            else if ((n == 1))
            {
                p = 1.0 - d;
            }
            else if ((n <= 100))
            {
                double xn = Convert.ToDouble(n);
                double vj = 1.0 / xn;
                double v1 = d;
                double z = 1.0 - d;
                double v2 = z;
                double y = xn * z;
                int lim1 = ((int)(Math.Floor((1.0 - Constant.EPSILON) * y)));
                p = 0.0;
                double cc = 1.0;
                for (int j = 1; j <= lim1; j++)
                {
                    double xj = Convert.ToDouble(j);
                    cc = cc * ((xn - xj + 1.0) / xj);
                    v1 = v1 + vj;
                    v2 = v2 - vj;
                    p = p + cc * Math.Pow(v1, j - 1) * Math.Pow(v2, n - j);
                }
                p = p * d + Math.Pow(z, n);
            }
            else
            {
                double a = -2.0 * (Convert.ToDouble(n) + 2.0) * d * d;
                p = (a < Math.Log(Constant.DBL_MIN)) ? 0.0 : Math.Exp(a);
            }
            return Math.Min(p, 1.0);
        }

        // 
        //       SUBROUTINE GSMIRN (NX, NY, KIND, M, DSTAT, P, Q, IFAULT)
        // 	!dec$attributes dllexport :: gsmirn
        // 	implicit real*8 (a-h,o-z),integer(i-n)
        // 	automatic
        //  c
        //  c  ALGORITHM AS 288 APPL.STATIST. (1994), VOL.43, NO.1
        //  c
        //  c  P-value calculation for the generalized two-sample 
        //  c  Smirnov tests.
        //  c  The tests are conditional on ties in the pooled sample.
        //  c
        //       LOGICAL NEWRCT
        // 	DIMENSION P(*), M(*)
        //       DATA ONE /1.0/ ZERO /0.0/ EPS /1E-6/
        //       DATA SMALL /1E-35/ SMALLN /-80.5904782547916/
        //       DATA ALN2 /0.69314718056/ CHKNUM /1E32/ ITERUP /116/
        //  c
        //       N = NX + NY
        //       IFAULT = 1
        //       IF (NX .LE. 0 .OR. NY .LE. 0) RETURN
        //       IFAULT = 2
        //       IF (KIND .LT. 1 .OR. KIND .GT. 3) RETURN
        //       IFAULT = 4
        //       ICAT = 0
        //       L = 0
        //     1 L = L + 1
        //       IF (M(L) .LE. 0) RETURN
        //       ICAT = ICAT + M(L)
        //       IF (ICAT - N) 1, 3, 2
        //     2 RETURN
        //  c
        //     3 IFAULT = 0
        //       Q = ONE
        //       DELTA = DSTAT - EPS
        //       IF (DELTA .LE. ZERO) RETURN
        //       P(1) = ONE
        //  c
        //  c  Parameters to define a set for trajectories to lie within it
        //  c
        //       SLOPE = NX / dfloat(N)
        //       DEVIAT = SLOPE * DELTA * NY
        //  c*    DELTA = DEVIAT
        //       NEWRCT = .TRUE.
        //       ICAT = 1
        //       NTIES = M(1)
        //       ILE = 0
        //       IRI = 0
        //  c
        //  c  Variables to prevent from overflows in P
        //  c
        //       IC = ITERUP
        //       NOFDIV = 0
        //       SCL = ONE
        //  c
        //       DO 100 L = 1, N - 1
        //         IF (NTIES .EQ. 1) THEN
        //  c
        //  c  Calculate boundaries for current L (if Lth value in the
        //  c  pooled sample is unique or `last' in a series of tied values)
        //  c
        //           DL = L * SLOPE
        //  c*        T = DFLOAT (L) / N
        //  c*        DEVIAT = DELTA * SQRT (T * (ONE - T))
        //           IRI = MIN0 (INT (DL + DEVIAT), L, NX)
        //           ILE = MAX0 (INT (DL - DEVIAT + ONE), L - NY, 0)
        //           ICAT = ICAT + 1
        //           NTIES = M(ICAT)
        //           NEWRCT = .TRUE.
        //         ELSE
        //  c
        //  c      Calculations for tied observations
        //  c
        //           NTIES = NTIES - 1
        //  c
        //  c  If we have the first observation with the new value (that
        //  c  is not unique), then determine the ICATth `rectangle'
        //  c
        //           IF (NEWRCT) THEN
        //             NEWRCT = .FALSE.
        //             L2 = L + NTIES
        //  c*          IF (L2 .EQ. N) THEN
        //  c*            IRI2 = NX
        //  c*            ILE2 = NX
        //  c*          ELSE
        //               DL = L2 * SLOPE
        //  c*            T = FLOAT (L2) / N
        //  c*            DEVIAT = DELTA * SQRT (T * (ONE - T))
        //  c
        //  c  X axis boundaries of the subset of `line' L(l+1) within 
        //  c  ICATth rectangle
        //  c
        //               IRI2 = MIN0 (INT (DL + DEVIAT), L2, NX)
        //               ILE2 = MAX0 (INT (DL - DEVIAT + ONE),  L2 - NY, 0)
        //  c*          ENDIF
        //  c
        //  c  Four sides of the rectangle on the X and Y axes
        //  c
        //             ILEFT = ILE
        //             IRIGHT = IRI2
        //             JUPP = L2 - ILE2
        //             JLOW = L - IRI - 1
        //           ENDIF
        //  c
        //  c  Calculate boundaries for current L (Lth value is tied)
        //  c
        //           ILE = MAX0 (ILEFT, L - JUPP)
        //           IRI = MIN0 (IRIGHT, L - JLOW)
        //         ENDIF
        //  c
        //  c  Set the left (right) boundary for the one-sided test
        //  c
        //         GOTO (30, 20, 10) KIND
        //    10   IRI = MIN0 (NX, L)
        //         GOTO 30
        //    20   ILE = MAX0 (0, L - NY)
        //  c
        //  c  Calculate the number of trajectories p(i,j) for current L
        //  c
        //    30   ILES = MAX0 (1, ILE)
        //         IRIS = MIN0 (L - 1, IRI)
        //  
        //         DO 50 I = IRIS, ILES, - 1
        //    50   P(I + 1) = P(I + 1) + P(I)
        //  c
        //  c  Check whether elements of P are large enough to multiply 
        //  c  them by SMALL
        //  c
        //         IC = IC - 1
        //         IF (IC .LE. 0) THEN
        //           DL = ZERO
        //           DO 60 I = ILES + 1, IRIS + 1
        //    60     DL = DMAX1 (P(I), DL)
        //           IF (DL .EQ. ZERO) RETURN
        //           IF (DL .GT. CHKNUM) THEN
        //             DO 65 I = ILES + 1, IRIS + 1
        //    65       P(I) = P(I) * SMALL
        //             IC = ITERUP
        //             NOFDIV = NOFDIV + 1
        //             SCL = SCL * SMALL
        //           ELSE
        //  c
        //  c  Estimate the number of iterations for DL=Pmax to became 
        //  c  of order 1/SMALL
        //  c
        //             IC = (- SMALLN - DLOG (DL)) / ALN2
        //           END IF
        //         END IF
        //  c
        //  c  Define, whether boundaries lie on the left and lower sides
        //  c  of the rectangle R and define boundary values for the next 
        //  c  iterations
        //  c
        //         IF (ILE .EQ. 0) THEN
        //           P(ILES) = SCL
        //         ELSE
        //           P(ILES) = ZERO
        //         ENDIF
        //  
        //         IF (IRI .EQ. L) THEN
        //           P(IRIS + 2) = SCL
        //         ELSE
        //           P(IRIS + 2) = ZERO
        //         ENDIF
        //  c
        //   100 CONTINUE
        //  c
        //       DL = P(NX + 1) + P(NX)
        //       IF (DL .EQ. ZERO) RETURN
        //  c
        //  c  The P-value
        //  c
        //       Q=ONE-EXP(dfloat(NX) + dfloat(NY) + DLOG(DL) 
        //      *    - NOFDIV * SMALLN - dfloat(N))
        //  c
        //  c  Q=1 is allowable, Q<=0 is not (accuracy loss due to 
        //  c  rounding errors)
        //  c
        //       IF (Q .LE. ZERO) IFAULT = 3
        //       END

        ///  <summary>
        ///  cumulative and point binomial distribution
        ///  </summary>
        ///  <param name="n"></param>
        ///  <param name="p"></param>
        ///  <param name="k"></param>
        ///  <param name="term"></param>
        ///  <param name="plo"></param>
        ///  <param name="phi"></param>
        ///  <param name="ifault"></param>
        ///  <remarks></remarks>
        public static void bino(int n, double p, int k, out double term, out double plo, out double phi, out int ifault)
        {
            term = Constant.MISSING;
            if ((p < 0.0 || p > 1.0))
            {
                ifault = 1;
                plo = Constant.MISSING;
                phi = Constant.MISSING;
                return;
            }
            if ((n < k))
            {
                ifault = 2;
                plo = Constant.MISSING;
                phi = Constant.MISSING;
                return;
            }
            ifault = 0;
            double sml = Math.Log(Constant.DBL_MIN);
            double xn = Convert.ToDouble(n);
            // double xk = Convert.ToDouble( k ); 
            plo = 0.0;
            double xn1 = xn + 1.0;
            for (int i = 0; i <= k; i++)
            {
                double xi = Convert.ToDouble(i);
                term = PDF.alogam(xn1) - PDF.alogam(xi + 1.0) - PDF.alogam(xn1 - xi) + xi * Math.Log(p) + (xn - xi) * Math.Log(1.0 - p);
                if ((term > sml))
                {
                    plo = plo + Math.Exp(term);
                }
            }
            if ((term > sml))
            {
                term = Math.Exp(term);
            }
            if ((term < 0.0))
            {
                term = 0.0;
            }
            phi = 1.0 - plo + term;
        }


        // Public Shared Sub bino2(ByRef n As Integer, ByRef p As Double, ByRef k As Integer, ByRef p1 As Double, ByRef p2 As Double, ByRef ifault As Double)
        //     If (p < 0.0 OrElse p > 1.0) Then
        //         ifault = 1
        //         Return
        //     ElseIf (n < k) Then
        //         ifault = 2
        //         Return
        //     Else
        //         ifault = 0
        //     End If
        //     If (p = 0.0) Then
        //         p1 = 0.5
        //         p2 = 1.0
        //         Return
        //     End If
        //     If (p = 1.0) Then
        //         If (k = n) Then
        //             p1 = 0.5
        //             p2 = 1.0
        //         Else
        //             p1 = 0.0
        //             p2 = 0.0
        //         End If
        //         Return
        //     End If
        //     Dim sml As Double = Math.Log(Constant.DBL_MIN)
        //     Dim xn As Double = CDbl(n)
        //     Dim xn1 As Double = xn + 1.0
        //     Dim xk As Double = CDbl(k)
        //     Dim plo As Double = 0.0
        //     p1 = plo
        //     p2 = plo
        //     Dim term As Double
        //     For i As Integer = 0 To k
        //         Dim xi As Double = CDbl(i)
        //         term = PDF.alogam(xn1) - PDF.alogam(xi + 1.0) - PDF.alogam(xn1 - xi) + xi * Math.Log(p) + (xn - xi) * Math.Log(1.0 - p)
        //         If (term > sml) Then plo = plo + Math.Exp(term)
        //     Next
        //     If (term > sml) Then term = Math.Exp(term)
        //     If (term < 0.0) Then term = 0.0
        //     Dim phi As Double = 1.0 - plo + term
        //     If (phi < plo) Then
        //         p1 = phi
        //     Else
        //         p1 = plo
        //     End If
        //     p2 = p1
        //     Dim z As Double = term + Constant.DBL_LRS
        //     Dim znp As Double = CDbl(n) * p
        //     If (CDbl(k) >= znp) Then
        //         For i As Integer = 0 To k - 1
        //             Dim xi As Double = CDbl(i)
        //             term = PDF.alogam(xn1) - PDF.alogam(xi + 1.0) - PDF.alogam(xn1 - xi) + xi * Math.Log(p) + (xn - xi) * Math.Log(1.0 - p)
        //             If (term > sml) Then
        //                 Dim px As Double = Math.Exp(term)
        //                 If (px < z) Then p2 = p2 + px
        //             End If
        //         Next
        //     Else
        //         For i As Integer = CInt(Fix(znp)) To n
        //             Dim xi As Double = CDbl(i)
        //             term = PDF.alogam(xn1) - PDF.alogam(xi + 1.0) - PDF.alogam(xn1 - xi) + xi * Math.Log(p) + (xn - xi) * Math.Log(1.0 - p)
        //             If (term > sml) Then
        //                 Dim px As Double = Math.Exp(term)
        //                 If (px < z) Then p2 = p2 + px
        //             End If
        //         Next
        //     End If
        // End Sub

        // /// ' <summary>
        // /// ' cumulative binomial distribution for mid-point inference
        // /// ' </summary>
        // /// ' <param name="n"></param>
        // /// ' <param name="p"></param>
        // /// ' <param name="k"></param>
        // /// ' <param name="p1"></param>
        // /// ' <param name="p2"></param>
        // /// ' <param name="ifault"></param>
        // /// ' <remarks></remarks>
        // Public Shared Sub binomid(ByRef n As Integer, ByRef p As Double, ByRef k As Integer, ByRef p1 As Double, ByRef p2 As Double, ByRef ifault As Integer)
        //     If (p < 0.0 OrElse p > 1.0) Then
        //         ifault = 1
        //         Return
        //     ElseIf (n < k) Then
        //         ifault = 2
        //         Return
        //     Else
        //         ifault = 0
        //     End If
        //     If (p = 0.0) Then
        //         p1 = 0.5
        //         p2 = 1.0
        //         Return
        //     End If
        //     If (p = 1.0) Then
        //         If (k = n) Then
        //             p1 = 0.5
        //             p2 = 1.0
        //         Else
        //             p1 = 0.0
        //             p2 = 0.0
        //         End If
        //         Return
        //     End If
        //     Dim sml As Double = Math.Log(Constant.DBL_MIN)
        //     Dim xn As Double = CDbl(n)
        //     Dim xk As Double = CDbl(k)
        //     Dim plo As Double = 0.0
        //     Dim xn1 As Double = xn + 1.0
        //     Dim term As Double
        //     For i As Integer = 0 To k
        //         Dim xi As Double = CDbl(i)
        //         term = PDF.alogam(xn1) - PDF.alogam(xi + 1.0) - PDF.alogam(xn1 - xi) + xi * Math.Log(p) + (xn - xi) * Math.Log(1.0 - p)
        //         If (term > sml) Then plo = plo + Math.Exp(term)
        //     Next
        //     If (term > sml) Then term = Math.Exp(term)
        //     If (term < 0.0) Then term = 0.0
        //     Dim phi As Double = 1.0 - plo + term
        //     If (phi < plo) Then
        //         p1 = phi
        //     Else
        //         p1 = plo
        //     End If
        //     p1 = p1 - term / 2.0
        //     p2 = 2.0 * p1
        //     Return
        // End Sub
    }

}
