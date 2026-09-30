using System;

namespace StatsDirect.Numerics
{
    public class MathDbl
    {
        //     Public Function ExpMinus1(ByVal x As Double) As Double
        //         ' Exp x - 1
        //         Dim y As Double, a As Double
        //         
        //         a = Abs(x)
        //         If a < EPSILON Then
        //             ExpMinus1 = x
        //             Exit Function
        //         End If
        //         If a > 0.697 Then
        //             ExpMinus1 = Exp(x) - 1.0
        //             Exit Function
        //         End If
        //         If a > 0.00000001 Then
        //             y = Exp(x) - 1.0
        //         Else
        //             y = (x / 2.0 + 1.0) * x
        //         End If
        //         y = y - (1.0 + y) * (DLNREL(y) - x)
        //         ExpMinus1 = y
        //     End Function

        //     Public Function ceil(ByVal Value As Double) As Double
        //         
        //         ceil = CDbl(CLng(Value + 0.5))
        //     End Function

        ///  <summary>
        ///  Returns Pearson's product moment correlation coefficient r or rho from a matching pair of vectors.
        ///  </summary>
        ///  <param name="x">Vector of independent observations</param>
        ///  <param name="y">Matching vector of dependent observations</param>
        ///  <param name="lowerBound">LowerBound-based input vector of n values</param>
        ///  <param name="n">Number of observations</param>
        /// <param name="noMissing"></param>
        /// <remarks></remarks>
        public static double corr(double[] x, double[] y, int lowerBound, int n, bool noMissing)
        {
            // Return a Pearson correlation coefficient. The sums of squares and products are taken about the means, in a
            // second pass: formed from the raw sums they lost digits when a mean was large compared with the spread, which
            // moved the Shapiro-Wilk and Shapiro-Francia statistics for values of about 1e6 or more.
            double sumx = 0.0;
            double sumy = 0.0;
            double nx = 0.0;
            for (int i = lowerBound; i < n + lowerBound; i++)
            {
                if (noMissing || (x[i] != Constant.MISSING & y[i] != Constant.MISSING))
                {
                    nx += 1.0;
                    sumx += x[i];
                    sumy += y[i];
                }
            }
            if (nx < 2.0)
            {
                return Constant.MISSING;
            }
            double meanx = sumx / nx;
            double meany = sumy / nx;
            double ssx = 0.0;
            double ssy = 0.0;
            double xy = 0.0;
            for (int i = lowerBound; i < n + lowerBound; i++)
            {
                if (noMissing || (x[i] != Constant.MISSING & y[i] != Constant.MISSING))
                {
                    double dx = x[i] - meanx;
                    double dy = y[i] - meany;
                    ssx += dx * dx;
                    ssy += dy * dy;
                    xy += dx * dy;
                }
            }
            double r = xy / Math.Sqrt(ssx * ssy);
            // rounding can take r just beyond 1 in size; it was set to +1 whichever the sign
            if (r > 1.0)
            {
                r = 1.0;
            }
            else if (r < -1.0)
            {
                r = -1.0;
            }
            return r;
        }


        //     Public Function pow10(ByVal x As Double) As Double
        //         If x > 307.0 Then
        //             pow10 = LMREAL
        //         ElseIf x < -307.0 Then
        //             pow10 = SPREAL
        //         Else
        //             pow10 = 10.0 ^ x
        //         End If
        //     End Function

        ///  <summary>
        ///  get 100*qc'th quantile from sorted 1-based vector r
        ///  </summary>
        ///  <param name="r"></param>
        ///  <param name="rx"></param>
        ///  <param name="qc"></param>
        ///  <returns></returns>
        ///  <remarks></remarks>
        public static double QuantileFromSorted(double[] r, int rx, double qc)
        {
            double iq = qc * (rx + 1);
            if (iq > rx)
            {
                iq = rx;
            }
            // below the first order statistic the quantile is the minimum; "iq < 0" could never be true, and r[0] is not an observation
            if (iq < 1)
            {
                iq = 1;
            }
            if (iq - Math.Floor(iq) == 0)
            {
                return r[Convert.ToInt32(iq)];
            }
            if (iq - Math.Floor(iq) != 0)
            {
                return r[(int)Math.Floor(iq)] + (r[(int)Math.Floor(iq) + 1] - r[(int)Math.Floor(iq)]) * (iq - Math.Floor(iq));
            }
            return 0;
        }

        public static double trapezoid_xy_roc(double[] rx, double[] ry, int lowerBound, int N)
        {
            if (N < 2)
                return Constant.MISSING;
            double s = 0.0;
            double[] xx = new double[N + 2];
            double[] yy = new double[N + 2];
            for (int i = 0; i < N; i++)
            {
                xx[1 + i] = rx[i + lowerBound];
                yy[1 + i] = ry[i + lowerBound];
            }
            xx[0] = 1.0;
            yy[0] = 1.0;
            xx[N + 1] = 0.0;
            yy[N + 1] = 0.0;
            for (int i = N + 1; i >= 1; i--)
            {
                double x = Math.Abs(xx[i] - xx[i - 1]);
                double y1 = yy[i];
                double y2 = yy[i - 1];
                s += x * y1 + x * Math.Abs(y2 - y1) / 2.0;
            }
            return s;
        }

        /// <summary>
        /// The confidence limits of the difference of paired proportions, that of the first response less that of the second,
        /// from the score limits of the two proportions and the correlation of the two responses.
        /// </summary>
        /// <remarks>
        /// Of N pairs, a respond both times, b the first time only, c the second time only and d neither time.  The proportions
        /// are p1 = (a + b) / N and p2 = (a + c) / N, and their difference is (b - c) / N.
        /// Each proportion has the score limits of a single proportion: for p1, with m = a + b,
        /// (m + (z^2 -/+ z root(z^2 + 4 m (N - m) / N)) / 2) / (N + z^2), which are l1 and u1; and l2 and u2 likewise for p2.
        /// The correlation of the two responses over the pairs is phi = (a d - b c) / root((a + b) (c + d) (a + c) (b + d)).
        /// If a d - b c is above 0 it has a correction for continuity: N / 2 is taken from a d - b c, which is made 0 if that
        /// leaves it below 0.  If a total of the table of the first response by the second is 0, phi is 0.
        /// The lower limit is the difference less root((p1 - l1)^2 - 2 phi (p1 - l1) (u2 - p2) + (u2 - p2)^2), and the upper
        /// limit is the difference plus root((u1 - p1)^2 - 2 phi (u1 - p1) (p2 - l2) + (p2 - l2)^2).
        /// </remarks>
        /// <param name="ia">The number of pairs that respond both times.</param>
        /// <param name="ib">The number that respond the first time only.</param>
        /// <param name="ic">The number that respond the second time only.</param>
        /// <param name="id">The number that respond neither time.</param>
        /// <param name="cl">On return, the lower limit, or missing.</param>
        /// <param name="cu">On return, the upper limit, or missing.</param>
        /// <param name="z">The normal deviate of the confidence level.</param>
        /// <param name="fault">On return, true if a number is below 0 or there are no pairs.</param>
        public static void Wilson(int ia, int ib, int ic, int id, out double cl, out double cu, double z, out bool fault)
        {
            fault = false;
            double zsq = z * z;
            int inl = ia + ib + ic + id;
            if (ia >= 0 && ib >= 0 && ic >= 0 && id >= 0 && inl > 0)
            {
                double a = Convert.ToDouble(ia);
                double b = Convert.ToDouble(ib);
                double c = Convert.ToDouble(ic);
                double d = Convert.ToDouble(id);
                double N = Convert.ToDouble(inl);
                // the difference of the two proportions, and the correlation of the two responses
                double th = (b - c) / N;
                double temp;
                double ph;
                if (b + c + a * d == 0.0)
                {
                    // icase = 3; 
                    ph = 0.0;
                }
                else if (a + d + b * c == 0.0)
                {
                    // icase = 4; 
                    ph = 0.0;
                }
                else if ((a + b) * (c + d) * (a + c) * (b + d) == 0.0)
                {
                    // icase = 2; 
                    ph = 0.0;
                }
                else
                {
                    // icase = 1; 
                    ph = a * d - b * c;
                    if (ph > 0)
                    {
                        if (ph - N / 2 > 0.0)
                        {
                            temp = ph - N / 2.0;
                        }
                        else { temp = 0.0; }
                        ph = temp / Math.Sqrt((a + b) * (c + d) * (a + c) * (b + d));
                    }
                    else
                    {
                        ph /= Math.Sqrt((a + b) * (c + d) * (a + c) * (b + d));
                    }
                }
                // the score limits of the first proportion (l2, u2) and of the second (l3, u3), and how far each is from its proportion
                double den = N + zsq;
                double u2 = (a + b + 0.5 * (zsq + z * Math.Sqrt(zsq + 4.0 * (a + b) * (c + d) / N))) / den;
                double l2 = (a + b + 0.5 * (zsq - z * Math.Sqrt(zsq + 4.0 * (a + b) * (c + d) / N))) / den;
                double u3 = (a + c + 0.5 * (zsq + z * Math.Sqrt(zsq + 4.0 * (a + c) * (b + d) / N))) / den;
                double l3 = (a + c + 0.5 * (zsq - z * Math.Sqrt(zsq + 4.0 * (a + c) * (b + d) / N))) / den;
                double dl2 = (a + b) / N - l2;
                double du2 = u2 - (a + b) / N;
                double dl3 = (a + c) / N - l3;
                double du3 = u3 - (a + c) / N;
                // the limits of the difference; what is under a root is taken as 0 if rounding has made it less
                if (Math.Pow(dl2, 2.0) - 2.0 * ph * dl2 * du3 + Math.Pow(du3, 2.0) > 0.0)
                {
                    temp = Math.Pow(dl2, 2.0) - 2.0 * ph * dl2 * du3 + Math.Pow(du3, 2.0);
                }
                else { temp = 0.0; }
                cl = th - Math.Sqrt(temp);
                if (Math.Pow(du2, 2.0) - 2.0 * ph * du2 * dl3 + Math.Pow(dl3, 2.0) > 0.0)
                {
                    temp = Math.Pow(du2, 2.0) - 2.0 * ph * du2 * dl3 + Math.Pow(dl3, 2.0);
                }
                else { temp = 0.0; }
                cu = th + Math.Sqrt(temp);
            }
            else
            {
                cl = Constant.MISSING;
                cu = Constant.MISSING;
                fault = true;
            }
        }

        ///  <summary>
        ///  Large sample approximation for Spearman Rho P
        ///  </summary>
        ///  <param name="N"></param>
        ///  <param name="dix"></param>
        ///  <param name="ifault"></param>
        ///  <returns></returns>
        public static double bigprho(long N, double dix, out int ifault)
        {
            ifault = 1;
            if (N <= 1)
                return 1.0;

            ifault = 0;
            if (dix <= 0.0)
                return 1.0;

            if (dix > Convert.ToDouble(N) * (Convert.ToDouble(N) * Convert.ToDouble(N) - 1.0) / 3.0)
                return 0.0;

            double djs = Math.Floor(dix);
            // S is always even: an odd value stands for the even number above it
            if (djs != 2.0 * Math.Floor(djs / 2.0))
                djs += 1.0;
            double b = 1.0 / Convert.ToDouble(N);
            double x = (6.0 * (djs - 1.0) * b / (1.0 / (b * b) - 1.0) - 1.0) * Math.Sqrt(1.0 / b - 1.0);
            double y = x * x;
            double z = y * b * (0.0879 + 0.0151 * b - y * (0.0072 - 0.0831 * b + y * b * (0.0131 - 0.00046 * y)));
            double u = x * b * (0.2274 + b * (0.2531 + 0.1745 * b) + y * (-0.0758 + b * (0.1033 + 0.3932 * b) - z));
            double bigprhoReturn = u / Math.Exp(y / 2.0) + 1.0 - PDF.alnorm(x);
            if (bigprhoReturn < 0.0)
                return 0.0;
            if (bigprhoReturn > 1.0)
                return 1.0;
            return bigprhoReturn;
        }


        ///  <summary>
        ///  
        ///  </summary>
        ///  <param name="a">Array, used dimensions (LowerBound..LowerBound+N-1, LowerBound..LowerBound+N-1?)</param>
        /// <param name="lowerBound"></param>
        /// <param name="N"></param>
        ///  <param name="b">0-based array, dimensions (LowerBound..LowerBound+M-1, LowerBound+..LowerBound+M-1)</param>
        ///  <param name="M"></param>
        ///  <param name="ifault"></param>
        ///  <remarks></remarks>
        public static void gaussj(double[,] a, int lowerBound, int N, double[,] b, int M, ref int ifault)
        {
            int icol = 0; int irow = 0;

            int[] indxc = new int[lowerBound + N];
            int[] indxr = new int[lowerBound + N];
            long[] ipiv = new long[lowerBound + N];
            if (N > a.GetUpperBound(0) + 1 || N > a.GetUpperBound(1) + 1 || M > b.GetUpperBound(1) + 1 || N > b.GetUpperBound(0) + 1)
            {
                ifault = 1;
                return;
            }
            for (int j = lowerBound; j < lowerBound + N; j++)
            {
                ipiv[j] = 0;
            }
            for (int i = lowerBound; i < lowerBound + N; i++)
            {
                double BIG = 0.0;
                for (int j = lowerBound; j < lowerBound + N; j++)
                {
                    if (ipiv[j] != 1)
                    {
                        for (int k = lowerBound; k < lowerBound + N; k++)
                        {
                            if (ipiv[k] == 0)
                            {
                                if (Math.Abs(a[j, k]) >= BIG)
                                {
                                    BIG = Math.Abs(a[j, k]);
                                    irow = j;
                                    icol = k;
                                }
                            }
                            else if (ipiv[k] > 1)
                            {
                                //  singularity
                                ifault = 2;
                                return;
                            }
                        }
                    }
                }
                ipiv[icol] = ipiv[icol] + 1;
                if (irow != icol)
                {
                    for (int L = lowerBound; L < lowerBound + N; L++)
                    {
                        double dum = a[irow, L];
                        a[irow, L] = a[icol, L];
                        a[icol, L] = dum;
                    }
                    for (int L = lowerBound; L < lowerBound + M; L++)
                    {
                        double dum = b[irow, L];
                        b[irow, L] = b[icol, L];
                        b[icol, L] = dum;
                    }
                }
                indxr[i] = irow;
                indxc[i] = icol;
                if (a[icol, icol] == 0)
                {
                    // singularity
                    ifault = 3;
                    return;
                }
                double pivinv = 1.0 / a[icol, icol];
                a[icol, icol] = 1.0;
                for (int L = lowerBound; L < lowerBound + N; L++)
                {
                    a[icol, L] = a[icol, L] * pivinv;
                }
                for (int L = lowerBound; L < lowerBound + M; L++)
                {
                    b[icol, L] = b[icol, L] * pivinv;
                }
                for (int ll = lowerBound; ll < lowerBound + N; ll++)
                {
                    if (ll != icol)
                    {
                        double dum = a[ll, icol];
                        a[ll, icol] = 0.0;
                        for (int L = lowerBound; L < lowerBound + N; L++)
                        {
                            a[ll, L] = a[ll, L] - a[icol, L] * dum;
                        }
                        for (int L = lowerBound; L < lowerBound + M; L++)
                        {
                            b[ll, L] = b[ll, L] - b[icol, L] * dum;
                        }
                    }
                }
            }
            for (int L = lowerBound + N - 1; L >= lowerBound; L--)
            {
                if (indxr[L] != indxc[L])
                {
                    for (int k = lowerBound; k < lowerBound + N; k++)
                    {
                        double dum = a[k, indxr[L]];
                        a[k, indxr[L]] = a[k, indxc[L]];
                        a[k, indxc[L]] = dum;
                    }
                }
            }
        }


        ///  <summary>
        ///  Calculate and return the mean and standard deviation of the doubles in x[0] to x[k - 1] inclusive.
        ///  </summary>
        ///  <param name="x">The array of values</param>
        ///  <param name="k">The number of values.  Set to the number of non-MISSING values.</param>
        ///  <param name="xmean">The output mean, or Constant.MISSING</param>
        ///  <param name="xsd">The output standard deviation, or Constant.MISSING</param>
        ///  <remarks></remarks>
        public static void MeanSD(double[] x, ref int k, out double xmean, out double xsd)
        {

            double xsum = 0.0;
            int ctr = 0;
            for (int i = 0; i < k; i++)
            {
                if (x[i] != Constant.MISSING)
                {
                    ctr++;
                    xsum += x[i];
                }
            }
            if (ctr < 2)
            {
                xmean = Constant.MISSING;
                xsd = Constant.MISSING;
                return;
            }
            xmean = xsum / ctr;
            double xss = 0.0;
            for (int i = 0; i < k; i++)
                if (x[i] != Constant.MISSING)
                    xss += (x[i] - xmean) * (x[i] - xmean);
            double xvar = xss / (ctr - 1);
            xsd = xvar >= 0 ? Math.Sqrt(xvar) : Constant.MISSING;
            k = ctr;
        }

        //     Function chivalp(ByVal x As Double, ByVal df As Double) As Double
        //         Dim fault As Long
        //         
        //         If x = MISSING Then
        //             chivalp = MISSING
        //         Else
        //             fault = 0
        //             chivalp = 1.0 - GAMMAD(x / 2.0, df / 2.0, fault)
        //             If fault <> 0 Then chivalp = MISSING
        //         End If
        //     End Function

        //     Function fvalp(ByVal f As Double, ByVal dfn As Double, ByVal dfd As Double) As Double
        //         Dim fault As Long
        //         
        //         fault = 0
        //         fvalp = BETAIN(dfd / (dfd + dfn * f), dfd / 2.0, dfn / 2.0, fault)
        //         If fault <> 0 Then fvalp = MISSING
        //     End Function

        /// <summary>
        /// Kendall's S of an upper tail probability: the greatest S, not below 0, of which the probability of that S or more is
        /// P or more; the tau of that S is returned.
        /// </summary>
        /// <param name="P">Upper tail probability</param>
        /// <param name="pu">The probability of ix or more, which is P or more</param>
        /// <param name="ix">The S that was found</param>
        /// <param name="nx">Number of observations</param>
        /// <param name="ifault">Not 0 if there is no such S, which is so if P is above the probability of an S of 0 or more</param>
        public static double taufromp(double P, out double pu, out int ix, ref int nx, out int ifault)
        {
            double taufrompReturn = 0;
            ix = -5;
            do
            {
                ix += 10;
                ifault = 0;
                pu = kendp(ix, nx, ref ifault);
                if (pu < P)
                    break;
                // The search used to stop at a score of 1000, so the quantile could never be larger than about 1000. The
                // quantile grows as n to the power 1.5 and passes 1000 at about 133 pairs for a two sided 95% interval,
                // after which intervals built on it (the slope in nonparametric regression) became far too narrow.
                // The largest possible score is n(n - 1)/2.
                if (ix > Convert.ToDouble(nx) * Convert.ToDouble(nx - 1) / 2.0)
                    break;
            }
            while (true);
            do
            {
                ix -= 1;
                // A score of 0, 1 or 2 is as possible an answer as any other: 1 is the largest score whose upper tail is
                // at least 0.5 with 10 pairs (exactly half the orderings), and 0 with 4 or 20 pairs. The search used to
                // stop at 3 and report a fault. It still stops at 0, since tau is taken from 0 to 1.
                if (ix < 0)
                {
                    ifault = 1;
                    break;
                }
                // The score has the parity of n(n - 1)/2. Up to 1000 observations kendp takes an impossible score as the
                // next possible one, so this skip changes nothing; above 1000 its Edgeworth series does not (when the series
                // was used above 50 it gave a score of 307 for the 2.5% point with 60 observations, where the largest
                // possible score is 306).
                if ((ix + (long)nx * (nx - 1) / 2) % 2 != 0)
                    continue;
                ifault = 0;
                pu = kendp(ix, nx, ref ifault);
                if (ifault == 0 & (pu > P || Math.Abs(pu - P) < 0.00000000000001))
                {
                    taufrompReturn = Convert.ToDouble(ix) / (Convert.ToDouble(nx) * Convert.ToDouble(nx - 1) / 2.0);
                    break;
                }
            }
            while (true);
            return taufrompReturn;
        }

        public static T[,] Transpose<T>(T[,] x)
        {
            int cols = x.GetUpperBound(0);
            int rows = x.GetUpperBound(1);
            T[,] z = new T[rows + 1, cols + 1];
            for (int c = 1; c <= cols; c++)
                for (int r = 1; r <= rows; r++)
                    z[r, c] = x[c, r];
            return z;
        }

        ///  <summary>
        ///  A version of kendp that gives a boolean error value rather than an integer error value.
        ///  </summary>
        ///  <param name="k"></param>
        ///  <param name="N"></param>
        ///  <param name="ifault"></param>
        ///  <returns></returns>
        ///  <remarks></remarks>
        public static double kendp(int k, int N, out bool ifault)
        {
            int my_ifault = 0;
            double result = kendp(k, N, ref my_ifault);
            ifault = my_ifault != 0;
            return result;
        }

        /// <summary>
        /// Upper tail probability of Kendall's S: that of an S of k or more with N observations that have no ties.
        /// </summary>
        /// <remarks>
        /// Up to 1000 observations the orders are counted, as the proportion of the orders of N things with each number of
        /// discordant pairs, built up from those of N - 1 things: the N-th thing is put in one of N places among the others,
        /// each equally likely, and makes 0 to N - 1 new discordant pairs. An S that there cannot be (one that is odd where
        /// the number of pairs is even, or even where it is odd) has the probability of the next S above it. The proportions
        /// are kept for the last N, so that a search over S costs one count. Above 1000 observations a series about the
        /// normal distribution is used, with k less 1 for continuity; it is within 0.0000005 of the counted probabilities
        /// with 51 observations and within 0.00000003 with 100, and nearer with more, but in the far tail less good in
        /// proportion (with 60 observations 1.39e-9 for 1.50e-9). The series does not look whether there can be an S of k:
        /// it is for the caller to see that k is within the pairs. (The orders used to be counted up to 50 observations,
        /// and the series used from 51.)
        /// </remarks>
        /// <param name="k">S, the concordant pairs less the discordant</param>
        /// <param name="N">Number of observations</param>
        /// <param name="ifault">1 if N is below 2, 2 if there is no memory for the counts, 3 if k is beyond the number of pairs
        /// (up to 1000 observations); it is left as it was if there is no fault</param>
        public static double kendp(int k, int N, ref int ifault)
        {
            if (N <= 1)
            {
                ifault = 1;
                return 0.0;
            }
            if (N > KendallCountedTo)
            {
                //  Edgeworth series
                double[] h = new double[16];
                double dn = Convert.ToDouble(N);
                double x = Convert.ToDouble(k - 1) / Math.Sqrt((6.0 + dn * (5.0 - dn * (3.0 + 2.0 * dn))) / -18.0);
                h[1] = x;
                h[2] = x * x - 1.0;
                for (int i = 3; i <= 15; i++)
                {
                    h[i] = x * h[i - 1] - Convert.ToDouble(i - 1) * h[i - 2];
                }
                double r = 1.0 / dn;
                double sc1 = h[3] * (-0.09 + r * (0.045 + r * (-0.5325 + r * 0.506)));
                double sc2 = h[5] * (0.036735 + r * (-0.036735 + r * 0.3214)) + h[7] * (0.00405 + r * (-0.023336 + r * 0.07787));
                double sc3 = h[9] * (-0.0033061 - r * 0.0065166) + h[11] * (-0.0001215 + r * 0.0025927) + r * (h[13] * 0.00014878 + h[15] * 0.0000027338);
                double sc = r * (sc1 + r * (sc2 + r * sc3));
                double series = 1.0 - PDF.alnorm(x) + sc * 0.398942 * Math.Exp(-0.5 * x * x);
                return Math.Min(1.0, Math.Max(0.0, series));
            }
            long pairs = (long)N * (N - 1) / 2;
            if (Math.Abs((long)k) > pairs)
            {
                ifault = 3;
                return 0.0;
            }
            double[] atMost;
            try
            {
                atMost = KendallDiscordantAtMost(N);
            }
            catch (OutOfMemoryException)
            {
                ifault = 2;
                return 0.0;
            }
            //  S = pairs - 2D: an S of k or more is D of (pairs - k) / 2 or fewer, rounded down (an S of the wrong parity has the
            //  probability of the next S above it)
            return atMost[(int)((pairs - k) / 2)];
        }

        //  the most observations for which the orders are counted; the series is used above it
        private const int KendallCountedTo = 1000;
        private static readonly object kendallLock = new();
        private static int kendallCountedN;
        private static double[] kendallAtMost;

        /// <summary>
        /// The proportion of the orders of N things that have d discordant pairs or fewer, for d from 0 to N (N - 1) / 2, kept
        /// for the last N asked for.
        /// </summary>
        private static double[] KendallDiscordantAtMost(int N)
        {
            lock (kendallLock)
            {
                if (kendallCountedN == N)
                    return kendallAtMost;
                int pairs = N * (N - 1) / 2;
                double[] p = new double[pairs + 1];   //  the proportion of orders with each number of discordant pairs
                double[] next = new double[pairs + 1];
                p[0] = 1.0;
                int most = 0;   //  the most discordant pairs among the things placed so far
                for (int j = 2; j <= N; j++)
                {
                    //  the j-th thing is put in one of j places among the j - 1 before it, each equally likely, and makes 0 to
                    //  j - 1 new discordant pairs: the new proportions are sums of the old over a window of j, divided by j. The
                    //  distribution is symmetric, so the lower half is summed (there each window sum is dominated by its latest
                    //  term, which keeps the tail accurate) and mirrored
                    int newMost = most + j - 1;
                    double sum = 0.0;
                    for (int d = 0; d <= newMost / 2; d++)
                    {
                        if (d <= most)
                            sum += p[d];
                        if (d >= j)
                            sum -= p[d - j];
                        next[d] = sum / j;
                    }
                    for (int d = 0; d <= newMost / 2; d++)
                        next[newMost - d] = next[d];
                    (p, next) = (next, p);
                    most = newMost;
                }
                double[] atMost = new double[pairs + 1];
                double total = 0.0;
                for (int d = 0; d <= pairs; d++)
                {
                    total += p[d];
                    atMost[d] = Math.Min(1.0, total);
                }
                kendallCountedN = N;
                kendallAtMost = atMost;
                return atMost;
            }
        }

        /// <summary>
        /// Upper-side P for Spearman's rho: P(S &lt;= ix), including ix, where S is the sum of squared rank differences.
        /// </summary>
        /// <remarks>
        /// ExFortran.prho gives P(S &gt;= ix), including ix, and treats an odd ix as the next even number because S is always even.
        /// The complement of P(S &lt;= ix) is therefore P(S &gt;= the next attainable score above ix): ix + 2 if ix is even, as in
        /// RptSpearman, and ix + 1 if it is odd (it used to be ix + 3, so that an odd ix had the P of the score above it).
        /// </remarks>
        public static double prhoUpper(int nx, int ix, out int ifault)
        {
            //  Below the smallest score, or so far above the largest that the next score does not fit an int: prho is called only to validate nx
            long next = (long)ix + 2 - (ix & 1);
            if (ix < 0 || next > int.MaxValue)
            {
                ExFortran.prho(nx, -1, out ifault);
                return ix < 0 ? 0.0 : 1.0;
            }
            return 1.0 - ExFortran.prho(nx, (int)next, out ifault);
        }

        /// <summary>
        /// Critical value of Spearman's rho: the rho of the largest score ix whose upper-side P, P(S &lt;= ix), does not exceed P.
        /// </summary>
        /// <param name="P">Upper-side probability</param>
        /// <param name="pu">The upper-side P attained at ix, which is at most P</param>
        /// <param name="ix">The critical score, the sum of squared rank differences</param>
        /// <param name="nx">Number of pairs of observations</param>
        /// <param name="ifault">Non-zero if there is no such score (even rho = 1 has an upper-side P above P) or nx is out of range</param>
        public static double rhofromp(double P, out double pu, out int ix, int nx, out int ifault)
        {
            const double tolerance = 0.00000000000001;
            ix = 0;
            //  S is always even and runs from 0 (rho = 1) to n(n^2 - 1)/3 (rho = -1)
            double maxScore = Convert.ToDouble(nx) * (Convert.ToDouble(nx) * Convert.ToDouble(nx) - 1.0) / 3.0;
            pu = prhoUpper(nx, 0, out ifault);
            if (ifault != 0)
                return 0;
            if (maxScore > int.MaxValue - 4 || pu > P + tolerance)
            {
                ifault = 1;
                return 0;
            }

            //  P(S <= ix) does not decrease with ix, so bisect on the half scores: low always satisfies the condition, high never does
            int low = 0;
            int high = Convert.ToInt32(maxScore / 2.0);
            if (prhoUpper(nx, 2 * high, out ifault) <= P + tolerance)
                low = high;
            while (high - low > 1)
            {
                int mid = low + (high - low) / 2;
                if (prhoUpper(nx, 2 * mid, out ifault) <= P + tolerance)
                    low = mid;
                else
                    high = mid;
            }
            ix = 2 * low;
            pu = prhoUpper(nx, ix, out ifault);
            return 1.0 - Convert.ToDouble(ix) / (maxScore / 2.0);
        }


        //     Function tvalp(ByVal t As Double, ByVal df As Double) As Double
        //         
        //         If t = MISSING Then
        //             tvalp = MISSING
        //             Exit Function
        //         End If
        //         tvalp = fvalp(t * t, 1.0, df)
        //         If tvalp = MISSING Then
        //             Exit Function
        //         Else
        //             tvalp = tvalp * 0.5
        //             If t < 0.0 Then tvalp = 1.0 - tvalp
        //         End If
        //     End Function

        public static void pone(double p0, double dpsi, double r, out double dp1, out bool imposs)
        {
            double Q0 = 1.0 - p0;
            double temp1 = 2 * Math.Pow(dpsi, 2.0) * Math.Pow(p0, 2.0) + 2.0 * dpsi * p0 * Q0 + Math.Pow(dpsi - 1.0, 2.0) * p0 * Q0 * Math.Pow(r, 2.0);
            double temp2 = (dpsi - 1.0) * p0 * Q0 * r * Math.Sqrt(Math.Pow(r, 2.0) * Math.Pow(dpsi - 1.0, 2.0) + 4.0 * dpsi);
            double temp3 = 2.0 * (Math.Pow(dpsi * p0 + Q0, 2.0) + Math.Pow(r, 2.0) * Math.Pow(dpsi - 1.0, 2.0) * p0 * Q0);
            dp1 = (temp1 - temp2) / temp3;
            double Q1 = 1.0 - dp1;
            Q0 = 1.0 - p0;
            double temp4 = r * Math.Sqrt(dp1 * p0 * Q0 * Q1);
            double p00 = Q1 * Q0 + temp4;
            double p11 = dp1 * p0 + temp4;
            double p10 = dp1 * Q0 - temp4;
            double p01 = p0 * Q1 - temp4;
            double min = p00;
            double max = p00;
            if (p11 > max)
                max = p11;
            if (p10 > max)
                max = p10;
            if (p01 > max)
                max = p01;
            if (p11 < min)
                min = p11;
            if (p10 < min)
                min = p10;
            if (p01 < min)
                min = p01;
            double pl = min;
            double pu = max;
            imposs = (pl < 0.0 || pu > 1.0);
        }

        /// <summary>
        /// The confidence limits of the difference of two proportions, that of the first sample less that of the second, of
        /// Miettinen and Nurminen: the differences at which the score statistic has the value z squared.
        /// </summary>
        /// <remarks>
        /// For a difference d the two proportions that are most likely with that difference are found: p2 of the second sample,
        /// and p1 = p2 + d of the first.  With a of M responding in the first sample and b of N in the second, the derivative of
        /// the logarithm of the likelihood is a / p1 - (M - a) / (1 - p1) + b / p2 - (N - b) / (1 - p2), which falls as p2
        /// rises: p2 is at the end of its range if the derivative has one sign over the range, and is found by halving the range
        /// if not.  The statistic is the square of the difference observed less d, over the variance
        /// (p1 (1 - p1) / M + p2 (1 - p2) / N) (M + N) / (M + N - 1).  It is 0 at the difference observed and without limit at
        /// -1 and at 1, and each limit is found by halving the range between, until it has all its figures.
        /// </remarks>
        /// <param name="a">The number responding in the first sample; the numbers need not be whole.</param>
        /// <param name="M">The size of the first sample.</param>
        /// <param name="b">The number responding in the second sample.</param>
        /// <param name="N">The size of the second sample.</param>
        /// <param name="xl">On return, the lower limit; missing if the numbers are not those of two samples.</param>
        /// <param name="xu">On return, the upper limit; missing if the numbers are not those of two samples.</param>
        /// <param name="z">The normal deviate of the confidence level.</param>
        /// <param name="Conf">The confidence level, of which no use is made.</param>
        public static void uppci(double a, double M, double b, double N, out double xl, out double xu, double z, double Conf)
        {
            xl = Constant.MISSING;
            xu = Constant.MISSING;
            if (a < 0 | b < 0 | M - a < 0 | N - b < 0 | M <= 0 | N <= 0)
                return;
            double observed = a / M - b / N;

            // the score statistic at the difference d, less what it is to be at a limit
            double Statistic(double d)
            {
                // the range of the second proportion: both proportions are to be from 0 to 1
                double low = Math.Max(0.0, -d);
                double high = Math.Min(1.0, 1.0 - d);
                // the derivative of the logarithm of the likelihood; a count of nothing has no part in it
                double Slope(double p2)
                {
                    double p1 = p2 + d;
                    double slope = 0.0;
                    if (a > 0.0)
                        slope += a / p1;
                    if (M - a > 0.0)
                        slope -= (M - a) / (1.0 - p1);
                    if (b > 0.0)
                        slope += b / p2;
                    if (N - b > 0.0)
                        slope -= (N - b) / (1.0 - p2);
                    return slope;
                }
                double p;
                if (low >= high)
                    p = low;
                else
                {
                    // At the low end of the range a proportion is 0, and the derivative is without limit if there is a response in
                    // that sample; at the high end a proportion is 1, and it is without limit the other way if there is a subject
                    // without a response in that sample
                    bool rises = (low + d <= 0.0 && a > 0.0) || (low <= 0.0 && b > 0.0) || Slope(low) > 0.0;
                    bool falls = (high + d >= 1.0 && M - a > 0.0) || (high >= 1.0 && N - b > 0.0) || Slope(high) < 0.0;
                    if (!rises)
                        p = low;
                    else if (!falls)
                        p = high;
                    else
                    {
                        double below = low;
                        double above = high;
                        for (int i = 0; i < 200; i++)
                        {
                            double mid = 0.5 * (below + above);
                            if (mid <= below || mid >= above)
                                break;
                            if (Slope(mid) > 0.0)
                                below = mid;
                            else
                                above = mid;
                        }
                        p = 0.5 * (below + above);
                    }
                }
                double q = Math.Max(0.0, Math.Min(1.0, p + d));
                double variance = (q * (1.0 - q) / M + p * (1.0 - p) / N) * (M + N) / (M + N - 1.0);
                double gap = observed - d;
                if (variance <= 0.0)
                    return gap == 0.0 ? -z * z : double.PositiveInfinity;
                return gap * gap / variance - z * z;
            }

            // the limit between the difference observed, where the statistic is 0, and the end of the range, where it is
            // without limit
            double Limit(double end)
            {
                if (observed == end)
                    return end;
                double inner = observed;
                double outer = end;
                for (int i = 0; i < 200; i++)
                {
                    double mid = 0.5 * (inner + outer);
                    if (mid == inner || mid == outer)
                        break;
                    if (Statistic(mid) > 0.0)
                        outer = mid;
                    else
                        inner = mid;
                }
                return 0.5 * (inner + outer);
            }
            xl = Limit(-1.0);
            xu = Limit(1.0);
        }

        /// <summary>
        /// The confidence limits of a proportion of Clopper and Pearson: the lower limit is the proportion with which r or more of
        /// N has the probability (1 - cco) / 2, and the upper limit that with which r or fewer has it.
        /// </summary>
        /// <remarks>
        /// The limits are from quantiles of the F distribution.  With F the value that F with 2 (N - r + 1) and 2 r degrees of
        /// freedom is above with probability (1 - cco) / 2, the lower limit is r / (r + (N - r + 1) F); with F that of 2 (r + 1)
        /// and 2 (N - r) degrees of freedom, the upper limit is (r + 1) F / (N - r + (r + 1) F).  If r is 0 the lower limit is 0,
        /// and if r is N the upper limit is 1: the interval is then one sided, with the confidence level cco + (1 - cco) / 2.
        /// </remarks>
        /// <param name="r">The number with the characteristic.</param>
        /// <param name="N">The number of observations.</param>
        /// <param name="pil">On return, the lower limit.</param>
        /// <param name="piu">On return, the upper limit.</param>
        /// <param name="cco">The confidence level.</param>
        /// <param name="warn">On return, what a report says after the limits of a one sided interval, or nothing.</param>
        public static void binci(double r, double N, out double pil, out double piu, double cco, out string warn)
        {

            double rp1l = 2.0 * N - 2.0 * r + 2.0;
            double rp2l = 2.0 * r;
            double rp1u = 2.0 * r + 2.0;
            double rp2u = 2.0 * N - 2.0 * r;
            if (r == 0.0)
            {
                pil = 0.0;
            }
            else
            {
                double fivl = PDF.ffromp(rp2l, rp1l, (1.0 - cco) / 2.0);
                if (fivl == Constant.MISSING)
                    pil = Constant.MISSING;
                else
                    pil = r / (r + (N - r + 1.0) * fivl);
            }
            if (r == N)
            {
                piu = 1.0;
            }
            else
            {
                double fivu = PDF.ffromp(rp2u, rp1u, (1.0 - cco) / 2.0);
                if (fivu == Constant.MISSING)
                    piu = Constant.MISSING;
                else
                    piu = (r + 1.0) / (r + 1.0 + (N - r) * (1.0 / fivu));
            }
            if (r == 0.0 || r == N)
                warn = " [" + StatsDirect.Utilities.Formatting.XRound(100.0 * (cco + (1.0 - cco) / 2.0), 1) + "% one-sided CI]";
            else
                warn = string.Empty;
        }

        public static void civ(long df, out double cit, double GAMMA, out double P0)
        {
            double P;

            if (df == 0)
            {
                P = (1.0 - GAMMA) / 2.0;
                cit = PDF.gauinv(1.0 - P);
                P0 = 1.0 - GAMMA;
            }
            else
            {
                P = (1.0 - GAMMA) / 2.0;
                P0 = 1.0 - GAMMA;
                if (P > 1.0 - P)
                    P = 1.0 - P;
                cit = PDF.tfromp(P, Convert.ToDouble(df));
            }
        }

        /// <summary>
        /// Koopman (1984) score confidence limits for the ratio of two binomial proportions, (tp / column1total) / (fp / column2total)
        /// </summary>
        /// <param name="fp">events in the denominator group</param>
        /// <param name="tp">events in the numerator group</param>
        /// <param name="column2total">size of the denominator group</param>
        /// <param name="column1total">size of the numerator group</param>
        /// <param name="zc">the normal deviate of the confidence level</param>
        /// <param name="thetal">lower limit</param>
        /// <param name="thetau">upper limit</param>
        /// <remarks>the score statistic is taken in the form (p1 - theta p0) / sqrt(p1~ q1~ / n1 + theta^2 p0~ q0~ / n0), which is Koopman's chi-square
        /// wherever the constrained estimates lie inside (0, 1) and stays finite where one of them is 1, so a group in which every subject has the event
        /// needs no adjustment of its total; the limits are then the reciprocals of the limits for the ratio the other way up</remarks>
        public static void lr_ci(double fp, double tp, double column2total, double column1total, double zc, out double thetal, out double thetau)
        {
            double lastz = 0;

            if (fp == 0.0 && tp == 0.0)
            {
                thetal = 0.0;
                thetau = double.PositiveInfinity;
            }
            else
            {
                double x0 = fp;
                double x1 = tp;
                double n0 = column2total;
                double n1 = column1total;
                if (n1 == 0.0 | n0 == 0.0)
                {
                    thetal = Constant.MISSING;
                    thetau = Constant.MISSING;
                    return;
                }
                if (x0 == n0 && x1 == n1)
                {
                    // every subject of both groups has the event: the score equation has closed-form roots either side of 1
                    thetal = n1 / (n1 + zc * zc);
                    thetau = (n0 + zc * zc) / n0;
                    return;
                }
                double P0 = x0 / n0;
                double P1 = x1 / n1;
                double uhat = 1.0 / (x1 + 0.5) + 1.0 / (x0 + 0.5) - 1.0 / (n0 + 0.5) - 1.0 / (n1 + 0.5);
                double N = n0 + n1;
                double logthetahat = Math.Log((x1 + 0.5) / (n1 + 0.5)) - Math.Log((x0 + 0.5) / (n0 + 0.5));
                thetau = Math.Exp(logthetahat) * Math.Exp(zc * Math.Sqrt(uhat));
                thetal = Math.Exp(logthetahat) * Math.Exp(-zc * Math.Sqrt(uhat));
                int i;
                for (i = 1; i <= 2; i++)
                {
                    double za2 = zc;
                    double temptheta1 = i == 1 ? thetau : thetal;
                    double temptheta2 = 0.9 * temptheta1;
                    double ztemp1 = lr_z(ref temptheta1, out double a, out double b, out double c, ref N, ref n0, ref n1, ref x0, ref x1);
                    double diff1 = Math.Abs(za2 - Math.Abs(ztemp1));
                    double ztemp2 = lr_z(ref temptheta2, out a, out b, out c, ref N, ref n0, ref n1, ref x0, ref x1);
                    double diff2 = Math.Abs(za2 - Math.Abs(ztemp2));
                    lr_diff(diff1, diff2, out double theta1, out double theta0, temptheta1, temptheta2, out double z1, out double z0, ztemp1, ztemp2, out double zcritical);
                    int cnt = 0;
                    double theta2;
                    do
                    {
                        if (i == 1)
                        {
                            za2 = -zc;
                        }
                        theta2 = Math.Exp(Math.Log(theta0) + (za2 - z0) / (z1 - z0) * Math.Log(theta1 / theta0));
                        temptheta1 = theta1;
                        temptheta2 = theta2;
                        ztemp2 = lr_z(ref temptheta2, out a, out b, out c, ref N, ref n0, ref n1, ref x0, ref x1);
                        if (ztemp2 == Constant.MISSING)
                        {
                            cnt = 5001;
                            break;
                        }
                        ztemp1 = z1;
                        diff1 = Math.Abs(za2 - ztemp1);
                        diff2 = Math.Abs(za2 - ztemp2);
                        lr_diff(diff1, diff2, out theta1, out theta0, temptheta1, temptheta2, out z1, out z0, ztemp1, ztemp2, out zcritical);
                        cnt += 1;
                        if (cnt > 5000)
                        {
                            break;
                        }
                        if (zcritical == lastz & zcritical < 0.001)
                            break;
                        lastz = zcritical;
                    }
                    while (!(zcritical < 0.0000001));
                    if (i == 1)
                    {
                        if (cnt < 5000)
                        {
                            thetau = lr_refine(theta2, za2, N, n0, n1, x0, x1);
                        }
                        else
                        {
                            thetau = P0 == 0.0 ? double.PositiveInfinity : Constant.MISSING;
                        }
                    }
                    else
                    {
                        if (cnt < 5000)
                        {
                            thetal = lr_refine(theta2, za2, N, n0, n1, x0, x1);
                        }
                        else
                        {
                            thetal = P1 == 0.0 ? 0.0 : Constant.MISSING;
                        }
                    }
                }
            }
        }


        /// <summary>
        /// Refines a score limit from the secant search, which stops when the score z is within 1e-7 of its critical value and so leaves a wide limit
        /// accurate to about seven significant figures, by bisection on log theta until theta itself is resolved; theta is returned unchanged when
        /// the score does not change sign about it
        /// </summary>
        private static double lr_refine(double theta, double za2, double N, double n0, double n1, double x0, double x1)
        {
            double f(double t)
            {
                double z = lr_z(ref t, out _, out _, out _, ref N, ref n0, ref n1, ref x0, ref x1);
                return z == Constant.MISSING ? double.NaN : z - za2;
            }
            double lo = theta;
            double hi = theta;
            double flo = 0.0;
            double fhi = 0.0;
            bool bracketed = false;
            for (double delta = 0.000001; delta <= 1.0 && !bracketed; delta *= 10.0)
            {
                lo = theta * Math.Exp(-delta);
                hi = theta * Math.Exp(delta);
                flo = f(lo);
                fhi = f(hi);
                bracketed = flo * fhi < 0.0;
            }
            if (!bracketed)
            {
                return theta;
            }
            for (int k = 0; k < 200; k++)
            {
                double mid = Math.Sqrt(lo) * Math.Sqrt(hi);
                if (mid <= lo || mid >= hi)
                {
                    break;
                }
                double fmid = f(mid);
                if (double.IsNaN(fmid))
                {
                    break;
                }
                if (fmid * flo <= 0.0)
                {
                    hi = mid;
                    fhi = fmid;
                }
                else
                {
                    lo = mid;
                    flo = fmid;
                }
            }
            return Math.Abs(flo) <= Math.Abs(fhi) ? lo : hi;
        }


        // TRANSMISSINGCOMMENT: Method lr_diff
        public static void lr_diff(double diff1, double diff2, out double theta1, out double theta0, double temptheta1, double temptheta2, out double z1, out double z0, double ztemp1, double ztemp2, out double zcritical)
        {
            if (diff1 < diff2)
            {
                theta1 = temptheta1;
                theta0 = temptheta2;
                z1 = ztemp1;
                z0 = ztemp2;
                zcritical = diff1;
            }
            else
            {
                theta0 = temptheta1;
                theta1 = temptheta2;
                z0 = ztemp1;
                z1 = ztemp2;
                zcritical = diff2;
            }
        }


        /// <summary>
        /// The constrained maximum likelihood estimate of the denominator proportion p0 given p1 = theta p0: the smaller root of a p^2 + b p + C = 0 is the
        /// stationary point of the profile likelihood, and the maximum is on the boundary p0 = 1 or p0 = 1 / theta when that root lies beyond it, as it
        /// does over part of the range when every subject of a group has the event
        /// </summary>
        public static double lr_ptilde(double theta, double a, double b, double C)
        {
            double root = 2.0 * C / (-b + Math.Sqrt(Math.Max(0.0, b * b - 4.0 * a * C)));
            return Math.Min(root, Math.Min(1.0, 1.0 / theta));
        }


        /// <summary>
        /// The score z for the ratio theta: (p1 - theta p0) / sqrt(p1~ q1~ / n1 + theta^2 p0~ q0~ / n0) with the constrained estimates p0~ and p1~ = theta p0~,
        /// which is the square root of Koopman's chi-square wherever those estimates lie inside (0, 1) and stays finite where one of them is 1
        /// </summary>
        public static double lr_z(ref double thetahat, out double a, out double b, out double C, ref double N, ref double n0, ref double n1, ref double x0, ref double x1)
        {
            a = N * thetahat;
            b = -((x0 + n1) * thetahat + x1 + n0);
            C = x0 + x1;
            double p0tilde = lr_ptilde(thetahat, a, b, C);
            double p1tilde = p0tilde * thetahat;
            double vtilde = p1tilde * (1.0 - p1tilde) / n1 + thetahat * thetahat * p0tilde * (1.0 - p0tilde) / n0;
            if (!(vtilde > 0.0))
            {
                return Constant.MISSING;
            }
            return (x1 / n1 - thetahat * x0 / n0) / Math.Sqrt(vtilde);
        }

        ///  <summary>
        ///  
        ///  </summary>
        ///  <param name="xz"></param>
        ///  <returns></returns>
        ///  <remarks>Changed from SD2 version - this returns a double, the old one also formatted that to a pval.  It's now up to the caller to format.</remarks>
        public static double zvalp2(double xz)
        {
            double P = 1.0 - PDF.alnorm(xz);
            if (P > 1.0 - P)
            {
                P = 1.0 - P;
            }
            return P * 2.0;
        }


        public double vector_max(double[] x, int start, int fin)
        {
            double z = double.MinValue;

            for (int i = start; i <= fin; i++)
            {
                if (x[i] > z)
                {
                    z = x[i];
                }
            }
            return z;
        }


        // TRANSMISSINGCOMMENT: Method vector_min
        public static double vector_min(double[] x, int start, int fin)
        {
            double z = double.MaxValue;
            for (int i = start; i <= fin; i++)
            {
                if (x[i] < z)
                {
                    z = x[i];
                }
            }
            return z;
        }


        // TRANSMISSINGCOMMENT: Method rtoz
        public static double rtoz(double r)
        {
            return 0.5 * Math.Log((1 + r) / (1 - r));
        }


        // TRANSMISSINGCOMMENT: Method ztor
        public static double ztor(double z)
        {
            return (Math.Exp(2.0 * z) - 1) / (Math.Exp(2.0 * z) + 1);
        }


        ///  <summary>
        ///  empirical cumulative distribution function ecdf from a vector x
        ///  </summary>
        ///  <param name="x"></param>
        ///  <param name="fn">Output variable, must be assigned to be at least x.Length </param>
        ///  <param name="err"></param>
        ///  <remarks></remarks>
        public static void ecdf(double[] x, double[] fn, out int err)
        {
            int n = x.Length - 1;
            if (n < 3)
            {
                err = 1;
                return;
            }
            err = 0;
            int[] f = new int[n + 1];
            double[] xc = new double[n + 1];
            double[] z = new double[n + 1];
            int i, j;
            int ii = 0;
            for (i = 0; i <= n; i++)
            {
                if (x[i] != Constant.MISSING)
                {
                    z[ii] = x[i];
                    ii += 1;
                }
                else
                {
                    fn[i] = Constant.MISSING;
                }
            }
            n = ii - 1;
            Array.Sort(z, 0, ii);
            i = 0;
            ii = 0;
            do
            {
                int k = 1;
                for (j = i + 1; j <= n; j++)
                {
                    if (z[j] == z[i])
                        k += 1;
                    else
                        break;
                }
                f[ii] = k;
                xc[ii] = z[i];
                if (i + k > n)
                {
                    break;
                }
                i += k;
                ii += 1;
            }
            while (true);
            double[] pecdf = new double[ii + 1];
            n += 1;
            pecdf[0] = 10000.0 * (f[0] / Convert.ToDouble(n)) / 10000.0;
            for (i = 1; i <= ii; i++)
            {
                // allow for rounding error
                pecdf[i] = 10000.0 * (pecdf[i - 1] + f[i] / Convert.ToDouble(n)) / 10000.0;
            }
            for (i = 0; i < x.Length; i++)
            {
                for (j = 0; j <= ii; j++)
                {
                    if (x[i] == xc[j])
                    {
                        fn[i] = pecdf[j];
                        break;
                    }
                }
            }
        }

        public static void zscore(double[] x, ref double[] z, bool doecdf, out int err)
        {
            int n = x.Length - 1;
            if (n < 3)
            {
                err = 1;
                return;
            }
            err = 0;
            if (doecdf == false)
            {
                double mu = 0;
                int i;
                int nx = 0;
                for (i = 0; i <= n; i++)
                {
                    if (x[i] != Constant.MISSING)
                    {
                        mu += x[i];
                        nx += 1;
                    }
                }
                mu /= Convert.ToDouble(nx);
                double sd = 0;
                for (i = 0; i <= n; i++)
                {
                    if (sd > 1.0E+300)
                    {
                        err = 2;
                        return;
                    }
                    if (x[i] != Constant.MISSING)
                    {
                        sd += Math.Pow(x[i] - mu, 2.0);
                    }
                }
                sd /= Convert.ToDouble(nx - 1);
                sd = Math.Sqrt(sd);
                z = new double[n + 1];
                for (i = 0; i <= n; i++)
                {
                    if (x[i] == Constant.MISSING)
                    {
                        z[i] = Constant.MISSING;
                    }
                    else
                    {
                        z[i] = (x[i] - mu) / sd;
                    }
                }

            }
            else
            {
                double[] fn = new double[n + 1];
                ecdf(x, fn, out err);
                if (err != 0)
                {
                    err = 3;
                    return;
                }
                int i;
                for (i = 0; i <= n; i++)
                {
                    double qp = fn[i];
                    if (qp == Constant.MISSING)
                    {
                        z[i] = Constant.MISSING;
                    }
                    else
                    {
                        if (qp <= 0.0)
                        {
                            qp = 0.0000000000001;
                        }
                        if (qp >= 1.0)
                        {
                            qp = 0.9999999999999;
                        }
                        double qz = Math.Abs(PDF.gauinv(qp, out int fault));
                        if (qp < 0.5)
                        {
                            qz = -qz;
                        }
                        if (fault == 0)
                        {
                            z[i] = qz;
                        }
                        else { z[i] = Constant.MISSING; }
                    }
                }
            }
        }
    }
}
