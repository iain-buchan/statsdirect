using System;

using StatsDirect.Templates;
using StatsDirect.Numerics;

namespace StatsDirect.Charting
{
    public class AxisScaler
    {
        ///  <summary>
        ///  Try to get a neat axis division suitable for values between qmin and qmax.
        ///  </summary>
        ///  <param name="qmin">The smallest value likely to be plotted on the axis. OUTPUT: May be modified if there are no values so that it is 0; will never otherwise be modified.</param>
        ///  <param name="qmin">The smallest value greater than zero likely to be plotted on the axis. Used for log scales; may be zero if scaleType is known to be Linear.</param>
        ///  <param name="qmax">The largest value likely to be plotted on the axis. OUTPUT: May be modified if there are no values so that it is 1; will never otherwise be modified.</param>
        ///  <param name="div">OUTPUT: The number of equal divisions in the scale.</param>
        ///  <param name="zmin">OUTPUT: The value of the lowest division.</param>
        ///  <param name="zint">OUTPUT: The value of the interval between divisions.</param>
        ///  <param name="minorTicsPerMajorTic">OUTPUT: The number of divisions between major tics.</param>
        ///  <param name="scaleType"></param>
        ///  <remarks></remarks>
        public static void Q_Axis(ref double qmin, double qMinGreaterThanZero, ref double qmax, out int div, out double zmin, out double zint, out int minorTicsPerMajorTic, ScaleType scaleType)
        {
            //  If we have no points at all, the choice is irrelevant so we might as well do it the easy way.
            if (qmin > qmax)
            {
                qmin = 0.0;
                qmax = 1.0;
            }

            switch (scaleType)
            {
                case ScaleType.Log10:
                    {
                        //  Start at the first power of 10 smaller than or equal to qminGreaterThanZero, stop at the first power of 10 greater than or equal to qmax.
                        int minPower = (int)Math.Floor(Math.Log10(qMinGreaterThanZero));
                        int maxPower = qmax <= 0 ? int.MinValue : (int)Math.Ceiling(Math.Log10(Math.Max(qMinGreaterThanZero, qmax)));
                        div = maxPower - minPower;
                        zmin = minPower;
                        zint = 1;
                        if (div <= 5)
                        {
                            minorTicsPerMajorTic = 3;
                            div *= 3;
                            zint /= 3.0;
                        }
                        else
                        {
                            minorTicsPerMajorTic = 1;
                        }
                    } break;
                case ScaleType.LogNatural:
                    {
                        //  Start at the first power of 2 smaller than or equal to qmin, stop at the first power of 2 greater than or equal to qmax.
                        double scaler = 1.0 / Math.Log(2);
                        int minPower = (int)Math.Floor(Math.Log(qMinGreaterThanZero) * scaler);
                        int maxPower = qmax <= 0 ? int.MinValue : (int)Math.Ceiling(Math.Log(Math.Max(qmax, qMinGreaterThanZero)) * scaler);
                        div = maxPower - minPower;
                        zmin = minPower;
                        zint = 1;
                        minorTicsPerMajorTic = 1;
                    } break;
                default:
                    //  Assume linear
                    if (qmin == 0.0 && qmax == 1.0)
                    {
                        minorTicsPerMajorTic = 5;
                        div = 20;
                        zmin = 0.0;
                        zint = 0.05;
                        return;
                    }

                    div = 20;
                    Axis(ref qmin, ref qmax, div, out zmin, out zint);
                    int pref;
                    Q_Axis_ShiftMin(qmin, qmax, ref zmin, ref zint, ref div, out pref);

                    const int tries = 4;
                    int[] trydiv = new int[tries + 1];
                    trydiv[1] = 15;
                    trydiv[2] = 25;
                    trydiv[3] = 16;
                    trydiv[4] = 24;
                    int i;
                    for (i = 1; i <= tries; i++)
                    {
                        int ndiv = trydiv[i];
                        double nzmin, nzint;
                        Axis(ref qmin, ref qmax, ndiv, out nzmin, out nzint);
                        int npref;
                        Q_Axis_ShiftMin(qmin, qmax, ref nzmin, ref nzint, ref ndiv, out npref);
                        bool shorteq;
                        bool shorter;
                        Q_Axis_Neater(ref zmin, ref nzmin, ref zint, ref nzint, out shorteq, out shorter, ref div, ref ndiv);
                        if (shorter || (shorteq && npref < pref))
                        {
                            pref = npref;
                            div = ndiv;
                            zint = nzint;
                            zmin = nzmin;
                        }
                    }

                    minorTicsPerMajorTic = div % 5 == 0 ? 5 : 4;
                    break;
            }
        }

        private static void Q_Axis_Neater(ref double zmin, ref double nzmin, ref double zint, ref double nzint, out bool shorteq, out bool shorter, ref int div, ref int ndiv)
        {
            int dp, ipow;
            double lab1 = div % 5 == 0 ? 5.0 : 4.0;
            Q_Axis_Scale01(nzmin, out ipow, out dp);
            int newlen = dp;
            Q_Axis_Scale01(nzint * lab1, out ipow, out dp);
            newlen = newlen + dp;
            Q_Axis_Scale01(zmin, out ipow, out dp);
            int oldlen = dp;
            Q_Axis_Scale01(zint * lab1, out ipow, out dp);
            oldlen = oldlen + dp;
            shorter = newlen < oldlen;
            shorteq = newlen <= oldlen;
            if (zmin < 0.0 & zmin + zint * Convert.ToDouble(div) > 0.0)
            {
                const double acc = SDGlobalStub.EPSNEG * 10.0;
                bool ok = false;
                double x = zmin;
                int j;
                for (j = 0; j <= div; j++)
                {
                    x = x + zint;
                    if (Math.Abs(x) < acc)
                    {
                        ok = true;
                        break;
                    }
                }
                if (ok == false || shorter)
                {
                    shorter = false;
                    x = nzmin;
                    for (j = 0; j <= ndiv; j++)
                    {
                        x = x + nzint;
                        if (Math.Abs(x) < acc)
                        {
                            // ok = true; 
                            shorter = true;
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Scale a real number to lie between zero and one
        /// </summary>
        /// <param name="x"></param>
        /// <param name="ipow"></param>
        /// <param name="dp"></param>
        private static void Q_Axis_Scale01(double x, out int ipow, out int dp)
        {
            if (x == 0.0)
            {
                dp = 0;
                ipow = 1; // TODO: For the sake of argument
                return;
            }
            ipow = ((int)(Math.Floor(Math.Log(Math.Abs(x)) / Math.Log(10.0)))) + 1;
            double sc = x / (Math.Pow(10.0, ipow));
            if (sc == 1.0)
                dp = 1;
            else
                dp = sc.ToString().Length - 2;
        }

        ///  <summary>
        ///  
        ///  </summary>
        ///  <param name="qmin">Smallest data value</param>
        ///  <param name="qmax">Largest data value</param>
        ///  <param name="zmin">Axis minimum (output)</param>
        ///  <param name="zint">Axis intervals</param>
        ///  <param name="div"></param>
        ///  <param name="pref"></param>
        ///  <remarks></remarks>
        private static void Q_Axis_ShiftMin(double qmin, double qmax, ref double zmin, ref double zint, ref int div, out int pref)
        {
            int ipow, i;
            // shift zmin to a nice spot
            if (zmin != 0.0)
            {
                ipow = ((int)(Math.Floor(Math.Log(Math.Abs(zmin)) / Math.Log(10.0)))) + 1;
                const int tries = 14;
                double[] ztry = new double[tries + 1];
                ztry[1] = 1.0;
                ztry[2] = 0.5;
                ztry[3] = 0.1;
                ztry[4] = 0.2;
                ztry[5] = 0.3;
                ztry[6] = 0.4;
                ztry[7] = 0.6;
                ztry[8] = 0.8;
                ztry[9] = 0.7;
                ztry[10] = 0.9;
                ztry[11] = 0.15;
                ztry[12] = 0.25;
                ztry[13] = 0.75;
                ztry[14] = 0.05;
                // data must cover target% of interval
                const double target = 0.7;
                for (i = 1; i <= tries; i++)
                {
                    double nzmin = ztry[i] * Math.Pow(10.0, ipow);
                    if (zmin < 0.0)
                    {
                        nzmin = -nzmin;
                    }
                    double nzmax = nzmin + zint * Convert.ToDouble(div);
                    double cover;
                    if (nzmin <= qmin & nzmax >= qmax)
                    {
                        cover = Math.Abs(qmax - qmin) / Math.Abs(nzmax - nzmin);
                        if (cover > target)
                        {
                            zmin = nzmin;
                            break;
                        }
                    }
                    else
                    {
                        if (nzmax < qmax)
                        {
                            if (div % 5 == 0)
                            {
                                if (div < 25)
                                {
                                    nzmax = nzmin + zint * Convert.ToDouble(div + 5);
                                    if (nzmin <= qmin & nzmax >= qmax)
                                    {
                                        cover = Math.Abs(qmax - qmin) / Math.Abs(nzmax - nzmin);
                                        if (cover > target)
                                        {
                                            zmin = nzmin;
                                            div = div + 5;
                                            break;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                if (div < 24)
                                {
                                    nzmax = nzmin + zint * Convert.ToDouble(div + 4);
                                    if (nzmin <= qmin & nzmax >= qmax)
                                    {
                                        cover = Math.Abs(qmax - qmin) / Math.Abs(nzmax - nzmin);
                                        if (cover > target)
                                        {
                                            zmin = nzmin;
                                            div = div + 4;
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            // score the look of the ticks and labelled intervals
            pref = 0;
            for (i = 1; i <= 2; i++)
            {
                double z;
                if (i == 1)
                {
                    z = zint;
                }
                else
                {
                    double lab1 = div % 5 == 0 ? 5.0 : 4.0;
                    z = zint * lab1;
                }
                ipow = ((int)(Math.Floor(Math.Log(Math.Abs(z)) / Math.Log(10.0)))) + 1;
                double scaled = z / (Math.Pow(10.0, ipow));
                if (scaled == 0.0 || scaled == 1.0)
                    pref = pref + 0;
                else if (scaled == 0.5 || scaled == 0.1)
                    pref = pref + 1;
                else if (scaled == 0.15 || scaled == 0.2 || scaled == 0.25 || scaled == 0.3)
                    pref = pref + 2;
                else if (scaled == 0.4 || scaled == 0.6)
                    pref = pref + 3;
                else
                    pref = pref + 4;

            }
        }

        public static void Axis(ref double zmn, ref double zmx, int nstep, out double znmin, out double zstep)
        {
            double[] r = { 0.1, 0.15, 0.2, 0.25, 0.4, 0.5, 0.6, 0.75, 0.8 };

            //  can replace with smallest relative spacing constant EPSNEG
            const double xmp = SDGlobalStub.EPSNEG;
            if (Math.Abs(zmn- Constant.MISSING)<Constant.EPSNEG || Math.Abs(zmx)==Constant.MISSING || double.IsInfinity(zmn) || double.IsInfinity(zmx) || double.IsNaN (zmn) || double.IsNaN (zmx))
            {
                znmin = 0;
                zstep = 0;
                return;
            }
            if (Math.Abs(zmn - zmx) < 1e-10)
            {
                zmn = zmn - 1.0;
                zmx = zmx + 1.0;
            }
            if (nstep < 1)
            {
                znmin = 0;
                zstep = 0;
                return;
            }
            double rnstpz = nstep;
            double rint = (zmx - zmn) / (rnstpz + 0.1);
            if (rint <= 0.0)
            {
                znmin = 0;
                zstep = 0;
                return;
            }

            double znmax;
            do
            {
                int mint = Convert.ToInt32(Math.Log(rint) / Math.Log(10) - 2.0);
                double tenn = Math.Pow(10.0, mint);
                bool jump = false;
                double ar = 0;
                for (int j = 1; j <= 11; j++)
                {
                    for (int i = 0; i <= 8; i++)
                    {
                        ar = r[i];
                        if (ar * tenn >= rint)
                        {
                            jump = true;
                            break;
                        }
                    }
                    if (jump == false)
                    {
                        tenn = tenn * 10.0;
                    }
                    else
                    {
                        break;
                    }
                }
                zstep = tenn * ar;
                ar = zstep * Math.Floor((1.0 + 2.0 * xmp) * zmn / zstep);
                if (double.IsInfinity(ar))
                {
                    znmin = 0;
                    zstep = 0;
                    return;
                }
                while (!((ar - zstep * 0.05) <= zmn))
                {
                    ar = ar - zstep;
                }
                znmin = ar;
                znmax = znmin + zstep * (rnstpz + 0.04);
                if (znmax >= zmx)
                {
                    break;
                }
                rint = rint * 1.05;
            }
            while (true);
            int maxA = Convert.ToInt32(Math.Log(Math.Abs(znmax)) / Math.Log(10.0) + 1.0);
            if (Math.Abs(znmin) > Math.Abs(znmax))
            {
                maxA = Convert.ToInt32(Math.Log(Math.Abs(znmin)) / Math.Log(10.0) + 1.0);
            }
            if (maxA < 0)
            {
                maxA = 0;
            }
            int maxB = Convert.ToInt32(Math.Log(zstep) / Math.Log(10.0) - 2.5);
            if (maxB > 0)
            {
                maxB = 0;
            }
            maxB = -maxB;
            if (maxA + maxB >= 10)
            {
                return;
            }
            double znm = znmin;
            for (int i = 0; i <= 9; i++)
            {
                if (znm + zstep * (rnstpz + 0.04) < zmx)
                {
                    break;
                }
                znmin = znm;
                znm = znmin * Math.Pow(10.0, (maxB - i));
                if (znm < 0.0)
                {
                    znm = znm - 1.0;
                }
                znm = Math.Floor(znm) / Math.Pow(10.0, (maxB - i));
            }
        }

        public static double Axis_Q0(double zmin, double Q)
        {
            if (Math.Abs(zmin) > SDGlobalStub.EPSILON)
            {
                if (Math.Abs(Q) < SDGlobalStub.EPSILON)
                {
                    return 0.0;
                }
            }
            return Q;
        }

        public static void v_axis(ref double qmin, ref double qmax, ref int cm, out double zmin, out double zint)
        {
            if (cm > 0)
            {
                Axis(ref qmin, ref qmax, cm, out zmin, out zint);
            }
            else
            {
                zmin = qmin;
                zint = qmax - qmin;
            }
        }

        ///  <summary>
        ///  
        ///  </summary>
        ///  <param name="stepp">The interval between labels</param>
        ///  <param name="znmin">The value at the zeroth division</param>
        ///  <param name="nstep">The number of divisions</param>
        ///  <param name="sp">The number of minor (unlabeled) tics per major (labeled) tic</param>
        ///  <param name="scaleType"></param>
        ///  <returns></returns>
        ///  <remarks></remarks>
        public static string AxisMask(double stepp, double znmin, int nstep, int sp, ScaleType scaleType)
        {
            //  Handle input scaling - if this isn't a linear scale, our input values are the transformed versions
            switch (scaleType)
            {
                case ScaleType.Log10:
                    znmin = Math.Pow(10, znmin);
                    break;
                case ScaleType.LogNatural:
                    //  We use powers of 2 for the labels, not powers of e
                    znmin = Math.Pow(2, znmin);
                    break;
                // Else do nothing - other scales are linear
            }


            //  Work out how many decimal places we should be using
            int numberOfDecimalPlaces;
            if (stepp > 0.000001)
            {
                //  How many decimal places in the division between major tics?
                string q = (stepp * sp).ToString();
                int xp = q.IndexOf(SDGlobalStub.DECP_CHAR, StringComparison.Ordinal) + 1;
                numberOfDecimalPlaces = xp == 0 ? 0 : q.Length - xp;

                //  How many decimal places in the smallest valued label?
                string q2 = Math.Abs(znmin).ToString();
                int xp2 = q2.IndexOf(SDGlobalStub.DECP_CHAR, StringComparison.Ordinal) + 1;
                int dp2 = xp2 == 0 ? 0 : q2.Length - xp2;

                //  Use the longer DPs
                if (dp2 > numberOfDecimalPlaces & xp2 != 0)
                    numberOfDecimalPlaces = dp2;
                else
                {
                    if (xp == 0)
                        numberOfDecimalPlaces = 0;
                }
            }
            else
            {
                numberOfDecimalPlaces = -1;
            }
            int maxc = 1;
            double x = Math.Abs(znmin) + Math.Abs(nstep * stepp);
            if (x > 0.0)
                maxc += Math.Abs(((int)(Math.Floor(Math.Log(x) / Math.Log(10)))));
            if (znmin < 0)
                maxc++;
            if (maxc > 6)
                numberOfDecimalPlaces = -1;
            string axisMaskReturn = "";
            if (numberOfDecimalPlaces > 0)
                axisMaskReturn = new string('#', maxc - 1) + "0." + new string('0', numberOfDecimalPlaces);
            else if (numberOfDecimalPlaces == 0)
                axisMaskReturn = new string('#', maxc - 1) + "0";

            if (axisMaskReturn.Length > 9 || numberOfDecimalPlaces < 0)
                axisMaskReturn = "E";

            return axisMaskReturn;
        }
    }
}
