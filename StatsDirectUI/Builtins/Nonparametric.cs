using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.Templates;
using StatsDirect.Utilities;

using System;
using System.Collections.Generic;
namespace StatsDirect.Builtins
{
    public static class Nonparametric
    {

        ///  <summary>
        ///  Returns a list containing a single blank results dictionary.  A handy helper where a template needs to show an error message in a block, but the message has no parameters.
        ///  </summary>
        ///  <returns></returns>
        ///  <remarks></remarks>
        private static IList<ParameterBag> OneOutputElement()
        {
            return new List<ParameterBag> { new ParameterBag() };
        }


        ///  <summary>
        ///  
        ///  </summary>
        ///  <param name="x">Input 1-dimensional 1-based array of values.</param>
        ///  <param name="lx">Number of values in x</param>
        ///  <param name="L">Input 1-based array containing lengths of columns</param>
        ///  <param name="cols">Number of columns in L</param>
        ///  <param name="h"></param>
        ///  <param name="ha"></param>
        ///  <param name="t"></param>
        ///  <param name="w1">Output 1-based ranked array (x, ranked)</param>
        ///  <param name="fault">0: Success. 1: At least 2 columns required. 2: Negative column length.3: column lengths don't add up to lx.</param>
        ///  <remarks></remarks>
        private static void x_kwt(double[] x, int lx, int[] L, int cols, out double h, ref double ha, ref double t, ref double[] w1, out int fault)
        {
            XPreprocessKwt(x, lx, L, cols, ref t, ref w1, out fault);
            XRunKwt(w1, lx, L, cols, out h, ref ha, t);
        }


        ///  <summary>
        ///  
        ///  </summary>
        ///  <param name="w1">Input 1-dimensional 1-based array of ranks.</param>
        ///  <param name="lx">Number of values in x</param>
        ///  <param name="L">Input 1-based array containing lengths of columns</param>
        ///  <param name="cols">Number of columns in L</param>
        ///  <param name="h"></param>
        ///  <param name="ha">h, adjusted for ties</param>
        ///  <param name="t">Input correction factor from Rank</param>
        ///  <remarks></remarks>
        private static void XRunKwt(double[] w1, int lx, int[] L, int cols, out double h, ref double ha, double t)
        {


            double rs = 0.0;
            for (int i = 1; i <= cols; i++)
            {
                int l1 = 1;
                if (i != 1)
                {
                    int I1 = i - 1;
                    for (int j = 1; j <= I1; j++)
                    {
                        l1 = l1 + L[j];
                    }
                }
                int l2 = l1 + L[i] - 1;
                double rj = 0.0;
                for (int j = l1; j <= l2; j++)
                {
                    rj += w1[j];
                }
                rj = rj * rj / Convert.ToDouble(L[i]);
                rs += rj;
            }

            double xn = lx;
            h = 12.0 * rs / (xn * (xn + 1.0)) - 3.0 * (xn + 1.0);

            if (t != 0.0)
            {
                double S2 = 1.0 - 12.0 * t / (xn * xn * xn - xn);
                if (S2 <= 0.0)
                {
                    ha = Constant.MISSING;
                }
                else { ha = h / S2; }
            }
        }


        ///  <summary>
        ///  
        ///  </summary>
        ///  <param name="x">Input 1-dimensional 1-based array of values.</param>
        ///  <param name="lx">Number of values in x</param>
        ///  <param name="L">Input 1-based array containing lengths of columns</param>
        ///  <param name="cols">Number of columns in L</param>
        ///  <param name="t"></param>
        ///  <param name="w1">Output 1-based ranked array (x, ranked)</param>
        ///  <param name="fault">0: Success. 1: At least 2 columns required. 2: Negative column length.3: column lengths don't add up to lx. 4: Unknown.</param>
        ///  <remarks></remarks>
        private static void XPreprocessKwt(double[] x, int lx, int[] L, int cols, ref double t, ref double[] w1, out int fault)
        {
            int i;

            fault = 1;
            if (cols < 2)
            {
                return;
            }
            fault = 2;
            int lsum = 0;
            for (i = 1; i <= cols; i++)
            {
                if (L[i] <= 0)
                {
                    return;
                }
                lsum += L[i];
            }

            fault = 3;
            if (lsum != lx)
            {
                return;
            }

            for (i = 2; i <= lx; i++)
            {
                if (x[i] != x[1])
                {
                    break;
                }
                if (i == lx)
                {
                    i = lx + 1;
                    break;
                }
            }
            if (i > lx)
            {
                fault = 4;
                return;
            }

            ExFortran.Rank(x, w1, 1, lx, 1, out t);
            fault = 0;

        }

        public static void XQci(double qc, int rx, double[] r, ref double xq, double GAMMA, out double ll, out double ul, ref double cover, bool conservative, ref bool cap_upper, ref bool cap_lower, out int fault)
        {
            double ll_plox; double ul_plox;
            double ul_id = 0; double ll_id = 0;

            // get 100*qc'th quantile from sorted vector r
            double iq = qc * (rx + 1);
            if (iq > rx)
            {
                iq = rx;
            }
            if (iq < 0)
            {
                iq = 1;
            }
            if (iq - Math.Floor(iq) == 0)
            {
                xq = r[Convert.ToInt32(iq)];
            }
            if (iq - Math.Floor(iq) != 0)
            {
                xq = r[((int)(Math.Floor(iq)))] + (r[((int)(Math.Floor(iq))) + 1] - r[((int)(Math.Floor(iq)))]) * (iq - Math.Floor(iq));
            }
            double z = Math.Abs(PDF.gauinv((1.0 - GAMMA) / 2.0, out fault));
            if (fault != 0)
            {
                ll = Constant.MISSING;
                ul = Constant.MISSING;
                return;
            }
            double rxs = Convert.ToDouble(rx);
            cap_lower = false;
            cap_upper = false;
            if (rx > 200)
            {
                ll_id = rxs * qc - z * Math.Sqrt(rxs * qc * (1 - qc));
                ul_id = rxs * qc + z * Math.Sqrt(rxs * qc * (1 - qc));
                if (ll_id < 1.0)
                {
                    ll_id = 0.0;
                    cap_lower = true;
                }
                if (ul_id + 1 > rxs)
                {
                    ul_id = rxs - 1;
                    cap_upper = true;
                }
                ll = r[((int)(Math.Floor(ll_id))) + 1];
                ul = r[((int)(Math.Floor(ul_id))) + 1];
                double scrap_term;
                double scrap_phi;
                ExFortran.bino(rx, qc, Convert.ToInt32(ul_id - 1.0), out scrap_term, out ul_plox, out scrap_phi, out fault);
                ExFortran.bino(rx, qc, Convert.ToInt32(ll_id - 1.0), out scrap_term, out ll_plox, out scrap_phi, out fault);
                cover = (ul_plox - ll_plox) * 100.0;
            }
            else
            {
                double[] plox = new double[rx + 1 /* VB to C# conversion */ ];
                int Q;
                for (Q = 0; Q <= rx; Q++)
                {
                    double scrap_term;
                    double scrap_phi;
                    ExFortran.bino(rx, qc, Q, out scrap_term, out plox[Q], out scrap_phi, out fault);
                    if (fault != 0)
                    {
                        ll = Constant.MISSING;
                        ul = Constant.MISSING;
                        return;
                    }
                }
                double ll_cut;
                double ul_cut;
                double ll_min;
                double ul_min;
                if (conservative)
                {
                    ul_min = 99;
                    ll_min = 99;
                    ul_plox = 0.0;
                    ll_plox = 0.0;
                    ul_cut = 1.0 - (1.0 - GAMMA) / 2.0;
                    ll_cut = (1.0 - GAMMA) / 2.0;
                    ul_id = 0.0;
                    ll_id = 0.0;
                    for (Q = 0; Q <= rx; Q++)
                    {
                        if (Math.Abs(plox[Q] - ul_cut) < ul_min & plox[Q] >= ul_cut)
                        {
                            ul_min = Math.Abs(plox[Q] - ul_cut);
                            ul_plox = plox[Q];
                            ul_id = Convert.ToDouble(Q);
                        }
                        if (Math.Abs(plox[Q] - ll_cut) < ll_min & plox[Q] <= ll_cut)
                        {
                            ll_min = Math.Abs(plox[Q] - ll_cut);
                            ll_plox = plox[Q];
                            ll_id = Convert.ToDouble(Q);
                        }
                    }
                }
                else
                {
                    ul_min = 99;
                    ll_min = 99;
                    ul_plox = 0.0;
                    ll_plox = 0.0;
                    ul_cut = 1.0 - (1.0 - GAMMA) / 2.0;
                    ll_cut = (1.0 - GAMMA) / 2.0;
                    for (Q = 0; Q <= rx; Q++)
                    {
                        if (Math.Abs(plox[Q] - ul_cut) < ul_min)
                        {
                            ul_min = Math.Abs(plox[Q] - ul_cut);
                            ul_plox = plox[Q];
                            ul_id = Convert.ToDouble(Q);
                        }
                        if (Math.Abs(plox[Q] - ll_cut) < ll_min)
                        {
                            ll_min = Math.Abs(plox[Q] - ll_cut);
                            ll_plox = plox[Q];
                            ll_id = Convert.ToDouble(Q);
                        }
                    }
                }
                if (ll_id < 1.0)
                {
                    ll_id = 0.0;
                    cap_lower = true;
                    double scrap_term;
                    double scrap_phi;
                    ExFortran.bino(rx, qc, Convert.ToInt32(ll_id), out scrap_term, out ll_plox, out scrap_phi, out fault);
                }
                if (ul_id + 1.0 > rxs)
                {
                    ul_id = rxs - 1.0;
                    cap_upper = true;
                    double scrap_term;
                    double scrap_phi;
                    ExFortran.bino(rx, qc, Convert.ToInt32(ul_id), out scrap_term, out ul_plox, out scrap_phi, out fault);
                }
                ll = r[((int)(Math.Floor(ll_id))) + 1];
                ul = r[((int)(Math.Floor(ul_id))) + 1];
                cover = (ul_plox - ll_plox) * 100.0;
            }
            if (fault != 0)
            {
                ll = Constant.MISSING;
                ul = Constant.MISSING;
            }
        }


        private static void x_kstwo(double[] d1, int n1, double[] d2, int n2, out double D, out double DP, out double dn)
        {
            Array.Sort(d1, 1, n1);
            Array.Sort(d2, 1, n2);
            double en1 = Convert.ToDouble(n1);
            double en2 = Convert.ToDouble(n2);
            int J1 = 1;
            int j2 = 1;
            double f1n = 0.0;
            double f2n = 0.0;
            DP = 0.0;
            dn = 0.0;
            while (J1 <= n1 & j2 <= n2)
            {
                double dt;
                if (d1[J1] < d2[j2])
                {
                    f1n = Convert.ToDouble(J1) / en1;
                    dt = f1n - f2n;
                    if (dt > DP)
                    {
                        DP = dt;
                    }
                    J1 = J1 + 1;
                }
                else if (d1[J1] > d2[j2])
                {
                    f2n = Convert.ToDouble(j2) / en2;
                    dt = f2n - f1n;
                    if (dt > dn)
                    {
                        dn = dt;
                    }
                    j2 = j2 + 1;
                }
                else
                {
                    f1n = Convert.ToDouble(J1) / en1;
                    J1 = J1 + 1;
                    f2n = Convert.ToDouble(j2) / en2;
                    j2 = j2 + 1;
                    while (J1 <= n1)
                    {
                        if (d1[J1] == d1[J1 - 1])
                        {
                            f1n = Convert.ToDouble(J1) / en1;
                            J1 = J1 + 1;
                        }
                        else
                        {
                            break;
                        }
                    }
                    while (j2 <= n2)
                    {
                        if (d2[j2] == d2[j2 - 1])
                        {
                            f2n = Convert.ToDouble(j2) / en2;
                            j2 = j2 + 1;
                        }
                        else
                        {
                            break;
                        }
                    }
                    dt = f1n - f2n;
                    if (dt > DP)
                    {
                        DP = dt;
                    }
                    dt = f2n - f1n;
                    if (dt > dn)
                    {
                        dn = dt;
                    }
                }
            }
            DP = Math.Max(0, DP);
            dn = Math.Max(0, dn);
            D = Math.Max(DP, dn);
        }


        private static void x_dokend(ITemplateHost host, ref double cit, ref int nx, ref double[] x, ref double[] y, ref int nxx, out double P, out double Q, ref double s, ref double hn, out double siga, out double sigb, ref double varf, ref double tau, ref double ll, ref double ul, out bool fault)
        {
            double sigbt3 = 0; double sigbt2 = 0; double sigbt1 = 0;
            int ytvn = 0; double sigat3 = 0; double sigat2 = 0; double sigat1 = 0;
            int xtvn = 0;

            double[] xtv = new double[nx + 1 ];
            double[] ytv = new double[nx + 1 ];
            double[] C = new double[nx + 1 ];
            fault = true;
            siga = 0;
            sigb = 0;
            P = 0.0;
            Q = 0.0;
            if (nx >= 2)
            {
                nxx = nx;
                double gd;
                if (cit > 0.0)
                {
                    gd = (nxx - 1) * 2.0;
                }
                else
                {
                    gd = nxx - 1.0;
                }
                host.StartProgress("Calculating Kendall", true);
                int N;
                int pn;
                for (pn = 1; pn <= nxx - 1; pn++)
                {
                    if (host.UpdateProgress(Convert.ToDouble(pn) / gd))
                        throw new TemplateOperationCancelledException();
                    int xtie = 0;
                    int ytie = 0;
                    for (N = pn + 1; N <= nxx; N++)
                    {
                        if ((x[pn] > x[N] & y[pn] > y[N]) | (x[pn] < x[N] & y[pn] < y[N]))
                        {
                            P = P + 1;
                        }
                        if ((x[pn] > x[N] & y[pn] < y[N]) | (x[pn] < x[N] & y[pn] > y[N]))
                        {
                            Q = Q + 1;
                        }
                        if (x[pn] == x[N])
                        {
                            xtie = xtie + 1;
                        }
                        if (y[pn] == y[N])
                        {
                            ytie = ytie + 1;
                        }
                    }
                    int count = xtie + 1;
                    bool OK;
                    if (count > 1)
                    {
                        OK = true;
                        for (N = 1; N <= xtvn; N++)
                        {
                            if (x[pn] == xtv[N])
                            {
                                OK = false;
                                break;
                            }
                        }
                        if (OK)
                        {
                            xtvn = xtvn + 1;
                            xtv[xtvn] = x[pn];
                            siga = siga + count * (count - 1) / 2.0;
                            sigat1 = sigat1 + Convert.ToDouble(count) * Convert.ToDouble(count - 1);
                            sigat2 = sigat2 + (Convert.ToDouble(count) * Convert.ToDouble(count - 1) * Convert.ToDouble(count - 2));
                            sigat3 = sigat3 + (Convert.ToDouble(count) * Convert.ToDouble(count - 1) * Convert.ToDouble((2 * count) + 5));
                        }
                    }
                    count = ytie + 1;
                    if (count > 1)
                    {
                        OK = true;
                        for (N = 1; N <= ytvn; N++)
                        {
                            if (y[pn] == ytv[N])
                            {
                                OK = false;
                                break;
                            }
                        }
                        if (OK)
                        {
                            ytvn = ytvn + 1;
                            ytv[ytvn] = y[pn];
                            sigb = sigb + Convert.ToDouble(count) * Convert.ToDouble(count - 1) / 2.0;
                            sigbt1 = sigbt1 + Convert.ToDouble(count) * Convert.ToDouble(count - 1);
                            sigbt2 = sigbt2 + (Convert.ToDouble(count) * Convert.ToDouble(count - 1) * Convert.ToDouble(count - 2));
                            sigbt3 = sigbt3 + (Convert.ToDouble(count) * Convert.ToDouble(count - 1) * Convert.ToDouble((2 * count) + 5));
                        }
                    }
                }
                s = P - Q;
                double xn = Convert.ToDouble(nx);
                hn = xn * (xn - 1.0) / 2.0;
                double tievar1 = ((xn * (xn - 1.0) * ((2.0 * xn) + 5.0)) - sigat3 - sigbt3) / 18.0;
                double tievar2 = (sigat2 * sigbt2) / ((9.0 * xn) * (xn - 1.0) * (xn - 2.0));
                double tievar3 = (sigat1 * sigbt1) / (2.0 * xn * (xn - 1.0));
                double tievar = tievar1 + tievar2 + tievar3;
                double kendvar = xn * (xn - 1.0) * ((2.0 * xn) + 5.0) / 18.0;
                varf = siga != 0 | sigb != 0 ? tievar : kendvar;
                tau = s / Math.Sqrt((hn - siga) * (hn - sigb));
                if (cit > 0.0)
                {
                    // Hollander & Wolfe P383 - Samara-Randles confidence interval
                    double dnx = Convert.ToDouble(nxx);
                    double cbar = 2.0 * s / dnx;
                    double cix = 0;
                    for (pn = 1; pn <= nxx; pn++)
                    {
                        if (host.UpdateProgress(dnx + Convert.ToDouble(pn) / gd))
                            throw new TemplateOperationCancelledException();
                        for (N = 1; N <= nxx; N++)
                        {
                            if (pn != N)
                            {
                                if ((x[pn] > x[N] & y[pn] > y[N]) | (x[pn] < x[N] & y[pn] < y[N]))
                                {
                                    C[pn] = C[pn] + 1;
                                }
                                if ((x[pn] > x[N] & y[pn] < y[N]) | (x[pn] < x[N] & y[pn] > y[N]))
                                {
                                    C[pn] = C[pn] - 1;
                                }
                            }
                        }
                        cix = cix + (C[pn] - cbar) * (C[pn] - cbar);
                    }
                    double vr = (2.0 / (dnx * (dnx - 1.0))) * ((2.0 * (dnx - 2.0) / (dnx * (dnx - 1.0) * (dnx - 1.0))) * cix + 1.0 - tau * tau);
                    ll = tau - cit * Math.Sqrt(vr);
                    if (ll < -1.0)
                    {
                        ll = -1.0;
                    }
                    ul = tau + cit * Math.Sqrt(vr);
                    if (ul > 1.0)
                    {
                        ul = 1.0;
                    }
                }
                else
                {
                    ll = Constant.MISSING;
                    ul = Constant.MISSING;
                }
                host.FinishProgress();
            }
            fault = false;
        }


        private static void x_invu(int n2, int n1, double GAMMA, ref double lev, ref int k, out bool fault)
        {
            double pcum = 0;
            double total = 0;
            int Q;

            double alphat = (1 - GAMMA) / 2;
            if ((n1 > 30 & n2 > 30) | (n1 > 100 | n2 > 100))
            {
                double n1s = Convert.ToDouble(n1);
                double n2s = Convert.ToDouble(n2);
                int ifault;
                double transTemp0 = (PDF.gauinv(alphat, out ifault) * Math.Sqrt((n1s * n2s * (n1s + n2s)) / 12.0));
                k = Convert.ToInt32((n1s * (n1s + n2s + 1.0)) / 2.0) + ((int)(Math.Floor(transTemp0)));
                k = k - Convert.ToInt32(n1s * ((n1s + 1.0) / 2.0));
                lev = alphat;
            }
            int min = n2 < n1 ? n2 : n1;
            int lfr = n2 * n1 + 1;
            int lwrk = 1 + min + ((int)(Math.Floor((double)(n2 * n1) / 2)));
            if (lfr > 200000 | lwrk > 100000)
            {
                //  make do with the approximate k above
                fault = true;
                return;
            }
            double[] frqncy = new double[lfr + 1 /* VB to C# conversion */];
            double[] work = new double[lwrk + 1 /* VB to C# conversion */];
            XUdist(n2, n1, ref frqncy, ref lfr, ref work, ref lwrk, out fault);
            for (Q = 1; Q <= lfr; Q++)
            {
                total = total + frqncy[Q];
            }
            double unitd = 1.0 / total;
            for (Q = 0; Q <= lfr; Q++)
            {
                pcum = pcum + unitd * frqncy[Q + 1];
                if (pcum > alphat)
                {
                    break;
                }
            }
            lev = pcum - (unitd * frqncy[Q + 1]);
            k = Q;
        }


        private static void XUdist(int M, int N, ref double[] frqncy, ref int lfr, ref double[] work, ref int lwrk, out bool fault)
        {
            fault = true;
            try
            {
                int min = Math.Min(M, N);
                if (min < 1)
                {
                    return;
                }
                int mn1 = M * N + 1;
                if (lfr < mn1)
                {
                    return;
                }
                int max = Math.Max(M, N);
                int n1 = max + 1;
                int i;
                for (i = 1; i <= n1; i++)
                {
                    frqncy[i] = 1;
                }
                if (min == 1)
                {
                    fault = false;
                    return;
                }
                if (lwrk < Math.Floor((double)(min + 1) / 2) + min)
                {
                    return;
                }
                n1 = n1 + 1;
                for (i = n1; i <= mn1; i++)
                {
                    frqncy[i] = 0;
                }
                work[1] = 0;
                int z_in = max;
                for (i = 2; i <= min; i++)
                {
                    work[i] = 0;
                    z_in = z_in + max;
                    n1 = z_in + 2;
                    double L = 1 + z_in / 2;
                    int k = i;
                    int j;
                    for (j = 1; j <= ((int)(Math.Floor(L))); j++)
                    {
                        k = k + 1;
                        n1 = n1 - 1;
                        double sum = frqncy[j] + work[j];
                        frqncy[j] = sum;
                        work[k] = sum - frqncy[n1];
                        frqncy[n1] = sum;
                    }
                }
                fault = false;
            }
            catch (OverflowException)
            {
                fault = true;
            }
        }


        ///  <summary>
        ///      LOWER TAIL PROBABILITY P FOR MANN-WHINEY STATISTIC IV
        ///      CASE WHERE THERE ARE NO TIES in THE POOLED SAMPLE
        ///  </summary>
        ///  <param name="n1"></param>
        ///  <param name="n2"></param>
        ///  <param name="iv"></param>
        ///  <param name="P"></param>
        ///  <remarks>
        ///      NEUMANN, N. - SOME PROCEDURES FOR CALCULATING THE
        ///                    DISTRIBUTIONS OF ELEMENTARY NONPARAMETRIC
        ///                    STATISTICS.
        ///      STAT. SOFTWARE NEWSLETTER, VOL. 14, NO 3., 1988
        /// </remarks>
        private static void XMwupNtLtp(int n1, int n2, int iv, out double P)
        {
            int j;
            int i;
            int m2; int m1;

            double[] wrk = new double[n1 * (((int)(Math.Floor((double)n2 / 2))) + 1)];
            if (n1 < n2)
            {
                m1 = n1;
                m2 = n2;
            }
            else
            {
                m1 = n2;
                m2 = n1;
            }
            int lim = iv + 1;
            double binom = 1;
            wrk[1] = 1;
            for (i = 1; i <= m1; i++)
            {
                binom = binom * Convert.ToDouble(m2 + i) / Convert.ToDouble(i);
                int upper;
                if ((i * m2 + 1) < lim)
                {
                    upper = (i * m2 + 1);
                }
                else { upper = lim; }
                int lower = i + m2 + 1;
                for (j = upper; j >= lower; j--)
                {
                    wrk[j] = wrk[j] - wrk[j - lower + 1];
                }
                for (j = i + 1; j <= upper; j++)
                {
                    wrk[j] = wrk[j] + wrk[j - i];
                }
            }
            wrk[1] = wrk[1] / binom;
            for (j = 2; j <= lim; j++)
            {
                wrk[j] = wrk[j - 1] + wrk[j] / binom;
            }
            P = wrk[iv + 1];
            if (P > 1)
            {
                P = 1;
            }
            if (P < 0)
            {
                P = 0;
            }
        }

        private static double XMwupNt(int n1, int n2, double u)
        {
            double P = 0;
            bool fault;

            if (n1 < 1 || n2 < 1)
            {
                fault = true;
            }
            else if (u < 0)
            {
                fault = true;
            }
            else
            {
                fault = false;
                int nm = n1 * n2;
                int iv = ((int)(Math.Floor(u)));
                if ((2 * iv) <= nm)
                {
                    XMwupNtLtp(n1, n2, iv, out P);
                }
                else
                {
                    iv = nm - iv;
                    XMwupNtLtp(n1, n2, iv, out P);
                }
            }
            return fault ? Constant.MISSING : P;
        }


        private static double XMwupTi(int n1, int n2, double[] ranks, double u)
        {
            double P = 0;
            bool fault; int ifault = 0;

            int[] iwrk = new int[2 * (n1 + n2 + 1) + 1 ]; //  1-based
            if (n1 < 1 | n2 < 1)
            {
                fault = true;
            }
            else if (u < 0)
            {
                fault = true;
            }
            else
            {
                fault = false;
                int nsum = n1 + n2;
                int i;
                for (i = 1; i <= nsum; i++)
                {
                    iwrk[i] = Convert.ToInt32(2 * ranks[i]);
                }
                Array.Sort(iwrk, 1, nsum);
                int nm = 2 * n1 * n2;
                int iv = Convert.ToInt32(2.0 * u);
                if (2 * iv <= nm)
                {
                    ExFortran.wmwpx(n1, n2, ref iwrk, iv, ref P, out ifault);
                }
                else
                {
                    iv = nm - iv;
                    ExFortran.wmwpx(n2, n1, ref iwrk, iv, ref P, out ifault);
                }
            }
            return fault || ifault != 0 ? Constant.MISSING : P;
        }


        private static ParameterBag x_mwcon(ITemplateHost host, ref double[] x, int k, int n1, int n2)
        {
            double median = 0; double kl = 0;
            int occurrences;
            int j;
            int midl; int midu;
            ParameterBag outputParameters = new ParameterBag();

            if (k > Int32.MaxValue || k == -99)
            {
                host.Error("Sample is too large for exact confidence interval calculation.", "Mann-Whitney"); // , ACTIVE_HELP_ID
                outputParameters.AddOutput("median", Formatting.ASTERISK);
                outputParameters.AddOutput("from", Formatting.ASTERISK);
                outputParameters.AddOutput("to", Formatting.ASTERISK);
                return outputParameters;
            }
            int limit = n1 * n2;
            if (limit % 2 == 0)
            {
                midu = ((int)(Math.Floor((double)limit / 2))) + 1;
                midl = ((int)(Math.Floor((double)limit / 2)));
            }
            else
            {
                midu = ((int)(Math.Floor((double)(limit + 1) / 2)));
                midl = midu;
            }
            host.StartProgress("Calculating Confidence Interval", true);
            int[] xx = new int[n1 + 1 /* VB to C# conversion */ ];
            int[] yy = new int[n2 + 1 /* VB to C# conversion */ ];
            Array.Sort(x, n1 + 1, n2);
            Array.Sort(x, 1, n1);

            //  Find the largest value in the array (at the upper limit of one of the sorts)...
            double bigx = x[n1 + n2];
            if (x[n1] > bigx)
            {
                bigx = x[n1];
            }
            //  ... and set an appropriate scale so that value fits within the range of an Integer.
            int scaler = 100000;
            do
            {
                if (bigx * Convert.ToDouble(scaler) < Convert.ToDouble(Int32.MaxValue) / 10.0)
                {
                    break;
                }
                scaler = Convert.ToInt32(scaler / 10);
            }
            while (true);
            for (j = 1; j <= n1; j++)
            {
                xx[j] = Convert.ToInt32(x[j] * scaler);
            }
            for (j = 1; j <= n2; j++)
            {
                yy[j] = Convert.ToInt32(x[n1 + j] * scaler);
            }
            bool domed = true;
            bool dokl = true;
            int goal = midu + k;
            int C = xx[1] - yy[n2] - 1;
            int i = 0;
            while (i < midu)
            {
                C = ExFortran.findnext(out occurrences, C, xx, n1, yy, n2);
                i = i + occurrences;
                if (host.UpdateProgress(i / (double)goal))
                {
                    outputParameters.AddOutput("median", Formatting.ASTERISK);
                    outputParameters.AddOutput("from", Formatting.ASTERISK);
                    outputParameters.AddOutput("to", Formatting.ASTERISK);
                    return outputParameters;
                }
                if (i >= k)
                {
                    if (dokl)
                    {
                        dokl = false;
                        kl = C / (double)scaler;
                    }
                }
                if (i >= midl)
                {
                    if (domed)
                    {
                        domed = false;
                        median = C / (double)scaler;
                    }
                }
            }
            if (midu != midl)
            {
                if (domed)
                {
                    median = C / (double)scaler;
                }
                else
                {
                    median = (median + C / (double)scaler) / 2;
                }
            }
            for (j = 1; j <= n1; j++)
            {
                xx[j] = -xx[j];
            }
            for (j = 1; j <= n2; j++)
            {
                yy[j] = -yy[j];
            }
            Array.Sort(xx, 1, n1);
            Array.Sort(yy, 1, n2);
            C = xx[1] - yy[n2] - 1;
            i = 0;
            while (i < k)
            {
                C = ExFortran.findnext(out occurrences, C, xx, n1, yy, n2);
                i = i + occurrences;
                if (host.UpdateProgress((midu + i) / (double)goal))
                {
                    outputParameters.AddOutput("median", Formatting.ASTERISK);
                    outputParameters.AddOutput("from", Formatting.ASTERISK);
                    outputParameters.AddOutput("to", Formatting.ASTERISK);
                    return outputParameters;
                }
            }
            double ku = -C / (double)scaler;
            host.FinishProgress();
            outputParameters.AddOutput("median", host.RoundU(median));
            outputParameters.AddOutput("from", host.RoundU(kl));
            outputParameters.AddOutput("to", host.RoundU(ku));
            return outputParameters;
        }


        private static double XXmdn(double[] x, int nx, int n1, int n2)
        {
            int N; int j;
            double[] ax;

            if (n2 == 1)
            {
                ax = new double[n1 + 1 /* VB to C# conversion */ ];
                for (j = 1; j <= n1; j++)
                {
                    ax[j] = x[j];
                }
                N = n1;
            }
            else
            {
                ax = new double[n2 + 1 /* VB to C# conversion */ ];
                for (j = n1 + 1; j <= nx; j++)
                {
                    ax[j - n1] = x[j];
                }
                N = n2;
            }
            if (N >= 2)
            {
                Array.Sort(ax, 1, N);
                double mdn = 0.5 * (N + 1);
                double median;
                if (mdn - Math.Floor(mdn) != 0)
                    median = ((ax[Convert.ToInt32(mdn - 0.5)] + ax[Convert.ToInt32(mdn + 0.5)]) / 2.0);
                else
                    median = ax[Convert.ToInt32(mdn)];
                return median;
            }
            return 0;
        }

        public static ParameterBag RptCuzick(ITemplateHost host, ParameterBag parameters)
        {
            double varz = 0; double ez = 0; double st = 0; double tie;
            int n = 0; int count = 0;

            DataFrame frame = parameters["data"].AsDataFrame;
            double[] t = new double[2];
            int[] gn = new int[frame.VariableCount];
            for (int j = 0; j < frame.VariableCount; j++)
            {
                int chuck = 0;
                DoubleVariable v = frame.Variables[j]as DoubleVariable;
                for (int k = 0; k < v.Length; k++)
                {
                    if (v.Data[k] != Constant.MISSING)
                    {
                        n++;
                        // create temp variable for copying values 
                        double[] transTemp2 = new double[n + 1];
                        Array.Copy(t, transTemp2, Math.Min(t.Length, transTemp2.Length));
                        t = transTemp2; // TODO: This could be sped up by allocating t in larger blocks and trimming after the loop.
                        t[n] = v.Data[k];
                    }
                    else
                    {
                        chuck++;
                    }
                }
                gn[j] = v.Length - chuck;
            }

            // Fill in scores
            double[] score = new double[frame.VariableCount];
            for (int j = 0; j < frame.VariableCount; j++)
                score[j] = j + 1;

            // If non-default scores exist, fill them in
            if (parameters.ContainsKey("scores") && null != parameters["scores"].Data)
            {
                DataFrame scoreFrame = parameters["scores"].AsDataFrame;
                DoubleVariable scoreVariable = scoreFrame.Variables[0]as DoubleVariable;
                for (int j = 0; j < Math.Min(frame.VariableCount, scoreVariable.Length); j++)
                    score[j] = scoreVariable.Data[j];
            }

            double[] r = new double[n + 1];
            ExFortran.Rank(t, r, 1, n, 1, out tie);
            for (int j = 0; j <= frame.VariableCount - 1; j++)
            {
                for (int k = 1; k <= gn[j]; k++)
                {
                    count++;
                    st += r[count] * score[j];
                }
                ez += score[j] * Convert.ToDouble(gn[j]) / Convert.ToDouble(n);
                varz += score[j] * score[j] * Convert.ToDouble(gn[j]) / Convert.ToDouble(n);
            }
            varz -= ez * ez;
            double et = Convert.ToDouble(n) / 2.0 * Convert.ToDouble(n + 1) * ez;
            double vart = (Convert.ToDouble(n) * Convert.ToDouble(n) * Convert.ToDouble(n + 1)) / 12.0 * varz;
            double stat = (st - et) / Math.Sqrt(vart);

            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("groups", frame.VariableCount.ToString());
            outputParameters.AddOutput("obs", n.ToString());
            string qtq = string.Empty;
            for (int j = 0; j < frame.VariableCount; j++)
            {
                qtq += frame.Variables[j].Title + " (" + score[j] + ")";
                if (j != frame.VariableCount - 1)
                    qtq += ", ";
            }
            outputParameters.AddOutput("order", qtq);
            outputParameters.AddOutput("ez", host.RoundU(ez));
            outputParameters.AddOutput("varz", host.RoundU(varz));
            outputParameters.AddOutput("t", host.RoundU(st));
            outputParameters.AddOutput("et", host.RoundU(et));
            outputParameters.AddOutput("vart", host.RoundU(vart));
            outputParameters.AddOutput("z", host.RoundU(stat));
            double P = 1.0 - PDF.alnorm(Math.Abs(stat));
            if (P > 1.0 - P)
            {
                P = 1.0 - P;
            }
            outputParameters.AddOutput("p_1", host.pval(P));
            outputParameters.AddOutput("p_2", host.pval(P * 2.0));
            if (tie != 0)
            {
                ParameterBag tiesParameters = new ParameterBag();
                IList<ParameterBag> tiesList = new List<ParameterBag>();
                tiesList.Add(tiesParameters);
                outputParameters.AddOutput("*ties", tiesList);
                vart = vart * (1.0 - ((tie * 12.0) / (Convert.ToDouble(n) * ((Convert.ToDouble(n) * Convert.ToDouble(n)) - 1.0))));
                stat = (st - et) / Math.Sqrt(vart);
                tiesParameters.AddOutput("varttie", host.RoundU(vart));
                tiesParameters.AddOutput("ztie", host.RoundU(stat));
                P = 1.0 - PDF.alnorm(Math.Abs(stat));
                if (P > 1.0 - P)
                    P = 1.0 - P;
                tiesParameters.AddOutput("p_1tie", host.pval(P));
                tiesParameters.AddOutput("p_2tie", host.pval(P * 2.0));
            }
            else
            {
                outputParameters.AddOutput("*ties", null);
            }
            return outputParameters;
        }


        public static ParameterBag RptDiversity(ITemplateHost host, ParameterBag parameters)
        {
            double cit; double P0;
            double bias = 0; double biasx = 0;
            double thetase = 0; double thetasex = 0;

            DataFrame frame = parameters["data"].AsDataFrame;
            double GAMMA = parameters["gamma"].AsDouble;
            int boots = parameters["boots"].AsInt32;
            int boots_divisor = Math.Max(1, boots / 1000);
            if (GAMMA <= 0)
            {
                throw new TemplateOperationCancelledException();
            }
            MathDbl.civ(0, out cit, GAMMA, out P0);
            MersenneTwister rnd = new MersenneTwister(); //  Self-seeded
            double[] r = new double[frame.MaxRows + 1 ];

            ParameterBag outputParameters = new ParameterBag();
            IList<ParameterBag> varList = new List<ParameterBag>();
            outputParameters.AddOutput("*var", varList);
            for (int k = 0; k <= frame.VariableCount - 1; k++)
            {
                DoubleVariable v = frame.Variables[k]as DoubleVariable;
                host.StartProgress("Bootstrapping diversity indices for " + v.Title, true);

                int rx = 0;
                double sumn = 0.0;
                double sumnx = 0.0;
                double sumnp2 = 0.0;
                double sumnp3 = 0.0;
                double sumnlogn = 0.0;
                double sumnlognsq = 0.0;
                int singletons = 0;
                int doubletons = 0;
                foreach (double val in v.Data)
                {
                    if (val != Constant.MISSING & val > 0.0 && val == Math.Floor(val))
                    {
                        rx = rx + 1;
                        r[rx] = val;
                        sumn = sumn + r[rx];
                        sumnx = sumnx + r[rx] * (r[rx] - 1.0);
                        sumnlogn = sumnlogn + r[rx] * Math.Log(r[rx]);
                        sumnlognsq = sumnlognsq + r[rx] * Math.Pow(Math.Log(r[rx]), 2.0);
                        if (Convert.ToInt32(r[rx]) == 1)
                        {
                            singletons = singletons + 1;
                        }
                        if (Convert.ToInt32(r[rx]) == 2)
                        {
                            doubletons = doubletons + 1;
                        }
                    }
                }
                if (rx < 3)
                {
                    host.FinishProgress();
                    host.Error("Too few observations.  Please use three or more non-zero positive integers.", "Diversity indices"); // , App.helpfile, ACTIVE_HELP_ID)
                    throw new TemplateOperationCancelledException();
                }
                double simpson = 1.0 - sumnx / (sumn * (sumn - 1.0));
                double shannon = (sumn * Math.Log(sumn) - sumnlogn) / sumn;
                double np;
                for (int j = 1; j <= rx; j++)
                {
                    np = r[j] / sumn;
                    sumnp2 = sumnp2 + Math.Pow(np, 2.0);
                    sumnp3 = sumnp3 + Math.Pow(np, 3.0);
                }
                double simvars = (4.0 * sumn * (sumn - 1.0) * (sumn - 2.0) * sumnp3 + 2.0 * sumn * (sumn - 1.0) * sumnp2 - 2.0 * sumn * (sumn - 1.0) * (2.0 * sumn - 3.0) * Math.Pow(sumnp2, 2.0)) / (Math.Pow((sumn * (sumn - 1.0)), 2.0));
                double simvar = (sumnp3 - (Math.Pow(sumnp2, 2.0))) / (0.25 * sumn);
                double simcl;
                double simcu;
                double simse;
                if (simvar < 0.0)
                {
                    simse = Constant.MISSING;
                    simcl = Constant.MISSING;
                    simcu = Constant.MISSING;
                }
                else
                {
                    simse = Math.Sqrt(simvar);
                    simcl = simpson - cit * simse;
                    simcu = simpson + cit * simse;
                }
                double shanvars = (sumnlognsq - (Math.Pow(sumnlogn, 2.0) / sumn)) / Math.Pow(sumn, 2.0) + Convert.ToDouble(rx - 1) / (2.0 * Math.Pow(sumn, 2.0));
                double shanvar = (sumnlognsq - (Math.Pow(sumnlogn, 2.0) / sumn)) / Math.Pow(sumn, 2.0);
                double shancl;
                double shancu;
                double shanse;
                if (shanvar < 0.0)
                {
                    shanse = Constant.MISSING;
                    shancl = Constant.MISSING;
                    shancu = Constant.MISSING;
                }
                else
                {
                    shanse = Math.Sqrt(shanvar);
                    shancl = shannon - cit * shanse;
                    shancu = shannon + cit * shanse;
                }
                int gtot = Convert.ToInt32(sumn);

                double sumnm1 = sumn - 1.0;
                double[] rb = new double[rx + 1 ];
                int[] cx = new int[rx + 1 ];
                double[] simpsonb = new double[boots + 1 ];
                double[] shannonb = new double[boots + 1 ];
                double[] simpsonbz = new double[boots + 1 ];
                double[] shannonbz = new double[boots + 1 ];
                // get resample boots times
                double theta = 0.0;
                double thetax = 0.0;
                int ctr = 0;
                int ctrx = 0;
                bool OK = true;
                bool studentfault = false;
                // work out cut-offs for scaling pick points
                cx[1] = Convert.ToInt32(r[1]);
                for (int j = 2; j <= rx; j++)
                {
                    cx[j] = cx[j - 1] + Convert.ToInt32(r[j]);
                }
                int pick;
                for (int i = 1; i <= boots; i++)
                {
                    sumn = 0.0;
                    sumnx = 0.0;
                    sumnp2 = 0.0;
                    sumnp3 = 0.0;
                    sumnlogn = 0.0;
                    sumnlognsq = 0.0;
                    for (int j = 1; j <= rx; j++)
                    {
                        rb[j] = 0.0;
                    }
                    // get N members at random
                    for (int j = 1; j <= gtot; j++)
                    {
                        pick = Convert.ToInt32(sumnm1 * rnd.NextDouble()) + 1;
                        for (int jj = 1; jj <= rx; jj++)
                        {
                            if (pick <= cx[jj])
                            {
                                rb[jj] = rb[jj] + 1;
                                break;
                            }
                        }
                    }
                    for (int j = 1; j <= rx; j++)
                    {
                        if (rb[j] > 0.0)
                        {
                            sumn = sumn + rb[j];
                            sumnx = sumnx + rb[j] * (rb[j] - 1.0);
                            sumnlogn = sumnlogn + rb[j] * Math.Log(rb[j]);
                            sumnlognsq = sumnlognsq + rb[j] * Math.Pow(Math.Log(rb[j]), 2.0);
                        }
                    }
                    for (int j = 1; j <= rx; j++)
                    {
                        np = rb[j] / sumn;
                        sumnp2 = sumnp2 + Math.Pow(np, 2.0);
                        sumnp3 = sumnp3 + Math.Pow(np, 3.0);
                    }
                    simpsonb[i] = 1.0 - sumnx / (sumn * (sumn - 1.0));
                    shannonb[i] = (sumn * Math.Log(sumn) - sumnlogn) / sumn;
                    double xse = (sumnp3 - (Math.Pow(sumnp2, 2.0))) / (0.25 * sumn);
                    if (xse > 0.0)
                    {
                        // simpsonbz(i) = Abs((simpsonb(i) - simpson) / Sqr(xse))
                        simpsonbz[i] = (simpsonb[i] - simpson) / Math.Sqrt(xse);
                    }
                    else
                    {
                        studentfault = true;
                    }
                    xse = (sumnlognsq - (Math.Pow(sumnlogn, 2.0) / sumn)) / Math.Pow(sumn, 2.0);
                    if (xse > 0.0)
                    {
                        // shannonbz(i) = Abs((shannonb(i) - shannon) / Sqr(xse))
                        shannonbz[i] = (shannonb[i] - shannon) / Math.Sqrt(xse);
                    }
                    else
                    {
                        studentfault = true;
                    }
                    theta = theta + simpsonb[i];
                    thetax = thetax + shannonb[i];
                    if (simpsonb[i] <= simpson)
                    {
                        ctr = ctr + 1;
                    }
                    if (shannonb[i] <= shannon)
                    {
                        ctrx = ctrx + 1;
                    }
                    if (i % boots_divisor == 0)
                    {
                        if (host.UpdateProgress(i / (double)boots))
                        {
                            OK = false;
                            break;
                        }
                    }
                }
                double nbcl;
                double nbcu;
                double nbclx;
                double nbcux;
                double blt;
                double bltx;
                double but;
                double butx;
                if (OK)
                {
                    //  get bias and bootstrap variance
                    theta = theta / Convert.ToDouble(boots);
                    thetax = thetax / Convert.ToDouble(boots);
                    double thetasq = 0.0;
                    double thetasqx = 0.0;
                    for (int i = 1; i <= boots; i++)
                    {
                        thetasq = thetasq + Math.Pow((simpsonb[i] - theta), 2.0);
                        thetasqx = thetasqx + Math.Pow((shannonb[i] - thetax), 2.0);
                    }
                    thetase = Math.Sqrt((1.0 / Convert.ToDouble(boots - 1)) * thetasq);
                    thetasex = Math.Sqrt((1.0 / Convert.ToDouble(boots - 1)) * thetasqx);
                    bias = simpson - theta;
                    biasx = shannon - thetax;
                    //  sort boostrap arrays
                    Array.Sort(simpsonb, 1, boots);
                    Array.Sort(shannonb, 1, boots);
                    Array.Sort(simpsonbz, 1, boots);
                    Array.Sort(shannonbz, 1, boots);
                    double alpha = 1.0 - GAMMA;
                    // ------------------------------------------
                    // 'percentile
                    // q = alpha / 2#
                    // pick = CLng(CDbl(boots - 1) * q) + 1
                    // bl = simpsonb(pick)
                    // blx = shannonb(pick)
                    // pick = CLng(CDbl(boots - 1) * (1# - q)) + 1
                    // bu = simpsonb(pick)
                    // bux = shannonb(pick)
                    // ------------------------------------------
                    // normal
                    double citt;
                    MathDbl.civ(boots - 1, out citt, GAMMA, out P0);
                    nbcl = simpson - citt * thetase;
                    nbcu = simpson + citt * thetase;
                    nbclx = shannon - citt * thetasex;
                    nbcux = shannon + citt * thetasex;
                    // bootstrap-t
                    double Q = alpha / 2.0;
                    pick = Convert.ToInt32(Convert.ToDouble(boots - 1) * Q) + 1;
                    if (studentfault || simse == Constant.MISSING)
                    {
                        but = Constant.MISSING;
                    }
                    else
                    {
                        but = simpson - simpsonbz[pick] * simse;
                    }
                    if (studentfault || shanse == Constant.MISSING)
                    {
                        butx = Constant.MISSING;
                    }
                    else
                    {
                        butx = shannon - shannonbz[pick] * shanse;
                    }
                    pick = Convert.ToInt32(Convert.ToDouble(boots - 1) * (1.0 - Q)) + 1;
                    if (studentfault || simse == Constant.MISSING)
                    {
                        blt = Constant.MISSING;
                    }
                    else
                    {
                        blt = simpson - simpsonbz[pick] * simse;
                    }
                    if (studentfault || shanse == Constant.MISSING)
                    {
                        bltx = Constant.MISSING;
                    }
                    else
                    {
                        bltx = shannon - shannonbz[pick] * shanse;
                    }
                    // centred
                    double av;
                    if (but != Constant.MISSING && blt != Constant.MISSING)
                    {
                        av = (but - blt) / 2.0;
                        blt = simpson - av;
                        but = simpson + av;
                    }
                    else
                    {
                        blt = Constant.MISSING;
                        but = Constant.MISSING;
                    }
                    if (butx != Constant.MISSING && bltx != Constant.MISSING)
                    {
                        av = (butx - bltx) / 2.0;
                        bltx = shannon - av;
                        butx = shannon + av;
                    }
                    else
                    {
                        bltx = Constant.MISSING;
                        butx = Constant.MISSING;
                    }
                    // symmetrized bootstrap-t, Vives et al 2002
                    // q = alpha / 2#
                    // pick = CLng(CDbl(boots - 1) * q) + 1
                    // If studentfault = True Or simse = Constant.MISSING Then
                    //  blt = Constant.MISSING
                    // Else
                    //  blt = simpson - simpsonbz(pick) * simse
                    // End If
                    // If studentfault = True Or shanse = Constant.MISSING Then
                    //  bltx = Constant.MISSING
                    // Else
                    //  bltx = shannon - shannonbz(pick) * shanse
                    // End If
                    // If studentfault = True Or simse = Constant.MISSING Then
                    //  but = Constant.MISSING
                    // Else
                    //  but = simpson + simpsonbz(pick) * simse
                    // End If
                    // If studentfault = True Or shanse = Constant.MISSING Then
                    //  butx = Constant.MISSING
                    // Else
                    //  butx = shannon + shannonbz(pick) * shanse
                    // End If
                    // --------------------------------------
                    // 'BC
                    // z0 = CDbl(ctr / boots)
                    // z0 = GAUINV(z0, 0)
                    // p1 = ALNORM(2# * z0 - cit)
                    // p2 = ALNORM(2# * z0 + cit)
                    // pick = CLng(CDbl(boots - 1) * p1) + 1
                    // bcal = simpsonb(pick)
                    // pick = CLng(CDbl(boots - 1) * p2) + 1
                    // bcau = simpsonb(pick)
                    // z0 = CDbl(ctrx / boots)
                    // z0 = GAUINV(z0, 0)
                    // p1 = ALNORM(2# * z0 - cit)
                    // p2 = ALNORM(2# * z0 + cit)
                    // pick = CLng(CDbl(boots - 1) * p1) + 1
                    // bcalx = shannonb(pick)
                    // pick = CLng(CDbl(boots - 1) * p2) + 1
                    // bcaux = shannonb(pick)
                    // ---------------------------------------
                    // 'BCa
                    // 'get influence moments - Armitage P 303
                    // iter = 0
                    // For i = 1 To rx
                    //  sumn = 0#
                    //  sumnx = 0#
                    //  sumnlogn = 0#
                    //  sumnlognsq = 0#
                    //  For j = 1 To rx
                    //   If j = i Then rm1 = r(j) - 1# Else rm1 = r(j)
                    //   If rm1 > 0# Then
                    //    sumn = sumn + rm1
                    //    sumnx = sumnx + rm1 * (rm1 - 1#)
                    //    sumnlogn = sumnlogn + rm1 * Log(rm1)
                    //    sumnlognsq = sumnlognsq + rm1 * Log(rm1) ^ 2#
                    //   End If
                    //  Next
                    //  sumsim = sumsim + (1# - sumnx / (sumn * (sumn - 1#))) * r(i)
                    //  sumshan = sumshan + ((sumn * Log(sumn) - sumnlogn) / sumn) * r(i)
                    // Next
                    // simbar = sumsim / CDbl(gtot - 1)
                    // shanbar = sumshan / CDbl(gtot - 1)
                    // For i = 1 To rx
                    //  sumn = 0#
                    //  sumnx = 0#
                    //  sumnlogn = 0#
                    //  sumnlognsq = 0#
                    //  For j = 1 To rx
                    //   If j = i Then rm1 = r(j) - 1# Else rm1 = r(j)
                    //   If rm1 > 0# Then
                    //    sumn = sumn + rm1
                    //    sumnx = sumnx + rm1 * (rm1 - 1#)
                    //    sumnlogn = sumnlogn + rm1 * Log(rm1)
                    //    sumnlognsq = sumnlognsq + rm1 * Log(rm1) ^ 2#
                    //   End If
                    //  Next
                    //  sim2 = sim2 + ((1# - sumnx / (sumn * (sumn - 1#))) - simbar) ^ 2#
                    //  sim3 = sim3 + ((1# - sumnx / (sumn * (sumn - 1#))) - simbar) ^ 3#
                    //  shan2 = shan2 + (((sumn * Log(sumn) - sumnlogn) / sumn) - shanbar) ^ 2#
                    //  shan3 = shan3 + (((sumn * Log(sumn) - sumnlogn) / sumn) - shanbar) ^ 3#
                    // Next
                    // accel = sim3 / (6# * sim2 ^ 1.5)
                    // accelx = shan3 / (6# * shan2 ^ 1.5)
                    // z0 = CDbl(ctr / boots)
                    // z0 = GAUINV(z0, 0)
                    // p1 = ALNORM(z0 + (z0 - cit) / (1# - accel * (z0 - cit)))
                    // p2 = ALNORM(z0 + (z0 + cit) / (1# - accel * (z0 + cit)))
                    // pick = CLng(CDbl(boots - 1) * p1) + 1
                    // bcal = simpsonb(pick)
                    // pick = CLng(CDbl(boots - 1) * p2) + 1
                    // bcau = simpsonb(pick)
                    // z0 = CDbl(ctrx / boots)
                    // z0 = GAUINV(z0, 0)
                    // p1 = ALNORM(z0 + (z0 - cit) / (1# - accelx * (z0 - cit)))
                    // p2 = ALNORM(z0 + (z0 + cit) / (1# - accelx * (z0 + cit)))
                    // pick = CLng(CDbl(boots - 1) * p1) + 1
                    // bcalx = shannonb(pick)
                    // pick = CLng(CDbl(boots - 1) * p2) + 1
                    // bcaux = shannonb(pick)
                }
                else
                {
                    //bl = Constant.MISSING; 
                    //bu = Constant.MISSING; 
                    //blx = Constant.MISSING; 
                    //bux = Constant.MISSING; 
                    nbcl = Constant.MISSING;
                    nbcu = Constant.MISSING;
                    nbclx = Constant.MISSING;
                    nbcux = Constant.MISSING;
                    blt = Constant.MISSING;
                    but = Constant.MISSING;
                    bltx = Constant.MISSING;
                    butx = Constant.MISSING;
                    //bcal = Constant.MISSING; 
                    //bcau = Constant.MISSING; 
                    //bcalx = Constant.MISSING; 
                    //bcaux = Constant.MISSING; 
                }
                host.FinishProgress();

                // Chao 1984 extrapolation
                if (singletons < 1)
                {
                    singletons = 1;
                }
                if (doubletons < 1)
                {
                    doubletons = 1;
                }
                double a = Convert.ToDouble(singletons);
                double b = Convert.ToDouble(doubletons);
                int stotal = rx + Convert.ToInt32(Math.Pow(a, 2.0) / (2.0 * b));
                double stotalvar = b * (Math.Pow(((a / b) / 4.0), 4.0) + Math.Pow((a / b), 3.0) + Math.Pow((a / b / 2.0), 2.0));
                int stotalcl;
                int stotalcu;
                if (stotalvar < 0.0)
                {
                    stotalcl = -1;
                    stotalcu = -1;
                }
                else
                {
                    stotalcl = Convert.ToInt32(Convert.ToDouble(stotal) - cit * Math.Sqrt(stotalvar));
                    stotalcu = Convert.ToInt32(Convert.ToDouble(stotal) + cit * Math.Sqrt(stotalvar));
                }

                ParameterBag varParameters = new ParameterBag();
                varList.Add(varParameters);
                varParameters.AddOutput("ti", v.Title);
                varParameters.AddOutput("n", gtot.ToString());
                if (rx != v.Length)
                {
                    varParameters.AddOutput("msg", "(note " + (v.Length - rx).ToString() + " other observations not used)");
                }
                else
                {
                    varParameters.AddOutput("msg", string.Empty);
                }
                varParameters.AddOutput("s", rx.ToString());
                varParameters.AddOutput("stotal", stotal.ToString());
                varParameters.AddOutput("se-largeSample", host.RoundU(Base.SafeSqrt(stotalvar)));
                varParameters.AddOutput("pc", Formatting.XRound(GAMMA * 100, 2));
                varParameters.AddOutput("from-largeSample", stotalcl == -1 ? Formatting.ASTERISK : stotalcl.ToString());
                varParameters.AddOutput("to-largeSample", stotalcu == -1 ? Formatting.ASTERISK : stotalcu.ToString());

                varParameters.AddOutput("simpson", host.RoundU(simpson));
                varParameters.AddOutput("dom", host.RoundU(1.0 - simpson));
                varParameters.AddOutput("ds",
                                        simpson != 1.0
                                            ? host.RoundU(1.0 / (1.0 - simpson))
                                            : host.RoundU(Constant.MISSING));
                varParameters.AddOutput("se-simpson-largeSample", host.RoundU(Base.SafeSqrt(simvar)));
                varParameters.AddOutput("ses-simpson", host.RoundU(Base.SafeSqrt(simvars)));
                varParameters.AddOutput("from-simpson-largeSample", host.RoundU(simcl));
                varParameters.AddOutput("to-simpson-largeSample", host.RoundU(simcu));
                varParameters.AddOutput("boots", boots.ToString("N0"));
                varParameters.AddOutput("bias-simpson", host.RoundU(bias));
                varParameters.AddOutput("se-simpson-bootstrap", host.RoundU(thetase));
                varParameters.AddOutput("from-simpson-bootstrap", host.RoundU(nbcl));
                varParameters.AddOutput("to-simpson-bootstrap", host.RoundU(nbcu));
                varParameters.AddOutput("from-simpson-bootstrap-t", host.RoundU(blt));
                varParameters.AddOutput("to-simpson-bootstrap-t", host.RoundU(but));

                varParameters.AddOutput("shannon", host.RoundU(shannon));
                varParameters.AddOutput("se-shannon-largeSample", host.RoundU(Base.SafeSqrt(shanvar)));
                varParameters.AddOutput("ses-shannon", host.RoundU(Base.SafeSqrt(shanvars)));
                varParameters.AddOutput("from-shannon-largeSample", host.RoundU(shancl));
                varParameters.AddOutput("to-shannon-largeSample", host.RoundU(shancu));
                varParameters.AddOutput("bias-shannon", host.RoundU(biasx));
                varParameters.AddOutput("se-shannon-bootstrap", host.RoundU(thetasex));
                varParameters.AddOutput("from-shannon-bootstrap", host.RoundU(nbclx));
                varParameters.AddOutput("to-shannon-bootstrap", host.RoundU(nbcux));
                varParameters.AddOutput("from-shannon-bootstrap-t", host.RoundU(bltx));
                varParameters.AddOutput("to-shannon-bootstrap-t", host.RoundU(butx));
            }

            return outputParameters;
        }


        public static ParameterBag RptMannWhitney(ITemplateHost host, ParameterBag parameters)
        {
            double lev = 0;
            double r1 = 0; double xf = 0; double z = 0; double u = 0;
            int cnt = 0; int n; int k = 0;

            double gamma = parameters["gamma"].AsDouble;
            if (gamma <= 0)
                return new ParameterBag();

            DataFrame frame = parameters["data"].AsDataFrame;
            DoubleVariable v0 = frame.Variables[0]as DoubleVariable;
            DoubleVariable v1 = frame.Variables[1]as DoubleVariable;
            double[] x = new double[v0.Length + v1.Length + 1 ];
            double[] w1 = new double[v0.Length + v1.Length + 1 ];

            for (n = 0; n <= v0.Length - 1; n++)
            {
                if (v0.Data[n] != Constant.MISSING)
                {
                    cnt = cnt + 1;
                    x[cnt] = v0.Data[n];
                }
            }
            int n1 = cnt;

            for (n = 0; n <= v1.Length - 1; n++)
            {
                if (v1.Data[n] != Constant.MISSING)
                {
                    cnt = cnt + 1;
                    x[cnt] = v1.Data[n];
                }
            }
            int n2 = cnt - n1;
            n = cnt;

            bool fault;
            NonParametric.x_mwut(x, n, n1, n2, w1, ref u, ref z, ref xf, ref r1, out fault);
            double uprime = n1 * n2 - u;

            ParameterBag outputParameters = new ParameterBag();

            outputParameters.AddOutput("sample_1", v0.Title);
            outputParameters.AddOutput("obs_1", n1.ToString());
            outputParameters.AddOutput("median_1", host.RoundU(XXmdn(x, n, n1, 1)));
            outputParameters.AddOutput("ranksum", host.RoundU(r1));

            outputParameters.AddOutput("sample_2", v1.Title);
            outputParameters.AddOutput("obs_2", n2.ToString());
            outputParameters.AddOutput("median_2", host.RoundU(XXmdn(x, n, n1, n2)));

            outputParameters.AddOutput("u", host.RoundU(u));
            outputParameters.AddOutput("u_prime", host.RoundU(uprime));

            if (!(fault))
            {
                string adj = xf > 0 ? " (adjusted for ties)" : string.Empty;
                double n1d = Convert.ToDouble(n1);
                double nd = Convert.ToDouble(n);
                double dimlim = n1d + n1d * (n1d + 1.0) * nd - (n1d * (n1d + 1.0) * (2.0 * n1d + 1.0)) / 3.0 + 1.0;
                double P;
                double pl;
                if ((n1 > 100 && n2 > 100) || (xf != 0 && dimlim > 1000000) || (xf == 0 && n1 * (((int)(Math.Floor((double)n2 / 2))) + 1) > 1000000))
                {
                    outputParameters.AddOutput("stats", "Normalised statistic = " + host.RoundU(z) + adj);
                    pl = PDF.alnorm(z);
                    if (pl > 1.0 - pl)
                        P = 1.0 - pl;
                    else
                        P = pl;
                    outputParameters.AddOutput("p_l", host.pval(pl));
                    outputParameters.AddOutput("p_u", host.pval(1.0 - pl));
                    outputParameters.AddOutput("p_2", host.pval(P * 2.0));
                }
                else
                {
                    outputParameters.AddOutput("stats", "Exact probability" + adj + ":");
                    pl = xf == 0 ? XMwupNt(n1, n2, u) : XMwupTi(n1, n2, w1, u);
                    P = pl > 1.0 - pl ? 1.0 - pl : pl;
                    outputParameters.AddOutput("p_l", host.pval(pl));
                    outputParameters.AddOutput("p_u", host.pval(1.0 - pl));
                    outputParameters.AddOutput("p_2", host.pval(P * 2.0));
                }

                outputParameters.AddOutput("pc0", Formatting.XRound(gamma * 100, 1));
                outputParameters.AddOutput("theta", host.RoundU(uprime / (n1 * n2)));
                outputParameters.AddOutput("tll", host.RoundU(ThetaLl(uprime, n1, n2, gamma)));
                outputParameters.AddOutput("tul", host.RoundU(ThetaUl(uprime, n1, n2, gamma)));

                if (n1 < 4 || n2 < 4)
                {
                    //  "CI not calculated if n1 or n2 < 4"
                    IList<ParameterBag> noconfList = new List<ParameterBag>();
                    noconfList.Add(new ParameterBag());
                    outputParameters.AddOutput("*noconf", noconfList);
                    outputParameters.AddOutput("*conf", null);
                }
                else
                {
                    IList<ParameterBag> confList = new List<ParameterBag>();
                    bool approx;
                    x_invu(n2, n1, gamma, ref lev, ref k, out approx);
                    ParameterBag confParameters = x_mwcon(host, ref x, k, n1, n2);
                    confParameters.AddOutput("pc", Formatting.XRound((1 - lev * 2) * 100, 1));
                    if (approx)
                        confParameters.AddOutput("k", k.ToString() + " (approx) ");
                    else
                        confParameters.AddOutput("k", k.ToString());
                    confList.Add(confParameters);
                    outputParameters.AddOutput("*conf", confList);
                    outputParameters.AddOutput("*noconf", null);
                }
            }
            return outputParameters;
        }

        /// <summary>
        /// Newcombe's Method 5 quadratic minimization for the Mann-Whitney theta (U/mn)
        /// </summary>
        /// <param name="upper"></param>
        /// <param name="tzpre"></param>
        /// <param name="ypre"></param>
        /// <param name="lp"></param>
        /// <param name="ln"></param>
        /// <param name="z"></param>
        /// <param name="t"></param>
        /// <param name="m"></param>
        /// <param name="n"></param>
        /// <returns></returns>
        private static double theta_tzmin(bool upper, double tzpre, double ypre, double lp, double ln, double z, double t, int m, int n)
        {
            const double prec = Double.Epsilon * 10;
            double y = 0;
            int i;
            for (i = 1; i <= 100; i++)
            {
                if (tzpre < 0)
                {
                    y = (lp + ypre) / 2;
                }
                else
                {
                    y = (ln + ypre) / 2;
                }
                double tz = ThetaTzfn(upper, y, z, t, m, n);
                if (tzpre >= 0) lp = ypre;
                if (tzpre <= 0) ln = ypre;
                ypre = y;
                tzpre = tz;
                if (Math.Abs(tz) < prec) break;
            }
            return y;
        }

        private static double ThetaTzfn(bool upper, double y, double z, double t, int m, int n)
        {
            double offset = z * Math.Sqrt(y * (1.0 - y) * (1.0 + (0.5 * (m + n) - 1.0) * ((1.0 - y) / (2.0 - y) + y / (1.0 + y))) / (m * n));
            return upper ? y - offset - t : y + offset - t;
        }

        /// <summary>
        /// Newcombe's Method 5 lower confidence limit for the Mann-Whitney theta (U'/mn)
        /// </summary>
        /// <param name="u"></param>
        /// <param name="m"></param>
        /// <param name="n"></param>
        /// <param name="gamma"></param>
        /// <returns></returns>
        private static double ThetaLl(double u, int m, int n, double gamma)
        {
            double alpha = (1.0 - gamma) / 2.0;
            int ifault;
            double z = PDF.gauinv(1.0 - alpha, out ifault);

            double t = u / (m * n);

            if (t == 0)
                return 0;
            
            if (t < 0 || t > 1)
                return Double.NaN;

            const double y0 = 0;
            double tz0 = ThetaTzfn(false, y0, z, t, m, n);
            const double y1 = 1;
            double tz1 = ThetaTzfn(false, y1, z, t, m, n);
            const double y2 = 0.5;
            double tz2 = ThetaTzfn(false, y2, z, t, m, n);
            double lp = tz1 < 0 ? Double.NaN : y1;
            double ln = tz0 > 0 ? Double.NaN : y0;
            if (Double.IsNaN(lp) || Double.IsNaN(ln))
                return Double.NaN;
            else
                return theta_tzmin(false, tz2, y2, lp, ln, z, t, m, n);
        }

        /// <summary>
        /// Newcombe's Method 5 upper confidence limit for the Mann-Whitney theta (U'/mn)
        /// </summary>
        /// <param name="u"></param>
        /// <param name="m"></param>
        /// <param name="n"></param>
        /// <param name="gamma"></param>
        /// <returns></returns>
        private static double ThetaUl(double u, int m, int n, double gamma)
        {
            double alpha = (1.0 - gamma) / 2.0;
            int ifault;
            double z = PDF.gauinv(1.0 - alpha, out ifault);

            double t = u / (m * n);

            if (t == 1)
                return 1;
            
            if (t < 0 || t > 1)
                return Double.NaN;

            const double y0 = 0;
            double tz0 = ThetaTzfn(true, y0, z, t, m, n);
            const double y1 = 1;
            double tz1 = ThetaTzfn(true, y1, z, t, m, n);
            const double y2 = 0.5;
            double tz2 = ThetaTzfn(true, y2, z, t, m, n);
            double lp = tz1 < 0 ? Double.NaN : y1;
            double ln = tz0 > 0 ? Double.NaN : y0;
            if (Double.IsNaN(lp) || Double.IsNaN(ln))
                return Double.NaN;
            else
                return theta_tzmin(true, tz2, y2, lp, ln, z, t, m, n);
        }

        public static ParameterBag RptSpearman(ITemplateHost host, ParameterBag parameters)
        {
            double GAMMA = parameters["gamma"].AsDouble;
            if (GAMMA <= 0)
                throw new Exception("Gamma must be greater than zero");

            DataFrame frame = parameters["data"].AsDataFrame;
            DoubleVariable v0 = frame.Variables[0]as DoubleVariable;
            DoubleVariable v1 = frame.Variables[1]as DoubleVariable;
            double[] prk = new double[v0.Length + 1 ];
            double[] prk1 = new double[v0.Length + 1 ];
            int nx = 0;
            for (int n = 0; n < v0.Length; n++)
            {
                if (v0.Data[n] != Constant.MISSING && v1.Data[n] != Constant.MISSING)
                {
                    nx++;
                    prk[nx] = v0.Data[n];
                    prk1[nx] = v1.Data[n];
                }
            }
            if (nx < 2)
                return new ParameterBag();

            double[] rka = new double[nx + 1 ];
            double[] rkb = new double[nx + 1 ];

            double xf;
            ExFortran.Rank(prk, rka, 1, nx, 1, out xf);
            double xf1;
            ExFortran.Rank(prk1, rkb, 1, nx, 1, out xf1);
            bool hasTies = xf != 0.0 || xf1 != 0.0;

            double srkd2 = 0;
            double srksq = 0;
            double srksqa = 0;
            double srksqb = 0;
            for (int n = 1; n <= nx; n++)
            {
                srkd2 += ((rka[n] - rkb[n]) * (rka[n] - rkb[n]));
                srksq += rka[n] * rkb[n];
                srksqa += rka[n] * rka[n];
                srksqb += rkb[n] * rkb[n];
            }
            double corr = nx * Math.Pow(((nx + 1.0) / 2.0), 2.0);
            double sr = (srksq - corr) / (Math.Sqrt(srksqa - corr) * Math.Sqrt(srksqb - corr));
            int ifault;
            double cit = PDF.gauinv(GAMMA + ((1 - GAMMA) / 2), out ifault);
            int nnx = nx;
            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("sample_1", v0.Title);
            outputParameters.AddOutput("sample_2", v1.Title);
            outputParameters.AddOutput("obs", nx.ToString());
            outputParameters.AddOutput("rho", host.RoundU(sr));

            outputParameters.AddOutput("*ties", hasTies ? OneOutputElement() : null);

            if (Math.Abs(sr) == 1)
            {
                // CI not calculated when rho is 1 or -1.
                outputParameters.AddOutput("*noci", OneOutputElement());
                outputParameters.AddOutput("*ci", null);
            }
            else
            {
                outputParameters.AddOutput("*noci", null);
                IList<ParameterBag> ciList = new List<ParameterBag>();
                ParameterBag ciParameters = new ParameterBag();
                ciList.Add(ciParameters);
                outputParameters.AddOutput("*ci", ciList);
                double fz = 0.5 * Math.Log((1.0 + sr) / (1.0 - sr));
                double fz1 = fz - (cit / Math.Sqrt(nx - 3.0));
                double fz2 = fz + (cit / Math.Sqrt(nx - 3.0));
                double con1 = (Math.Exp(2.0 * fz1) - 1.0) / (Math.Exp(2.0 * fz1) + 1.0);
                double con2 = (Math.Exp(2.0 * fz2) - 1.0) / (Math.Exp(2.0 * fz2) + 1.0);
                ciParameters.AddOutput("pc", (100 * GAMMA).ToString());
                ciParameters.AddOutput("from", host.RoundU(con1));
                ciParameters.AddOutput("to", host.RoundU(con2));
            }
            if (nx < 4)
            {
                // Can not consider probability with very small samples (n < 4)
                outputParameters.AddOutput("*lown", OneOutputElement());
                outputParameters.AddOutput("*results", null);
                outputParameters.AddOutput("ix", Constant.MISSING);
            }
            else
            {
                outputParameters.AddOutput("*lown", null);
                IList<ParameterBag> resultsList = new List<ParameterBag>();
                ParameterBag resultsParameters = new ParameterBag();
                resultsList.Add(resultsParameters);
                outputParameters.AddOutput("*results", resultsList);
                double nxs = nnx;
                double dix = ((1.0 - sr) * (nxs * ((nxs * nxs) - 1.0))) / 6.0;
                outputParameters.AddOutput("ix", host.RoundU(dix));
                double qix = nxs * (nxs * nxs - 1.0) / 3.0;
                int fault;
                double pl;
                if (dix >= int.MaxValue || qix >= int.MaxValue)
                    pl = MathDbl.bigprho(Convert.ToInt64(nxs), dix, out fault);
                else
                    pl = ExFortran.prho(Convert.ToInt32(nxs), Convert.ToInt32(dix), out fault);
                double P = pl > 1.0 - pl ? 1.0 - pl : pl;

                if (fault != 0)
                {
                    // P not calculable
                    resultsParameters.AddOutput("*nop", OneOutputElement());
                    resultsParameters.AddOutput("*results", null);
                }
                else
                {
                    resultsParameters.Add("*nop", null);
                    IList<ParameterBag> results2List = new List<ParameterBag>();
                    ParameterBag results2Parameters = new ParameterBag();
                    results2List.Add(results2Parameters);
                    resultsParameters.AddOutput("*results", results2List);
                    if (hasTies)
                    {
                        double p2Approximate = PDF.tvalp(Math.Abs(sr) * Math.Sqrt(nx - 2) / Math.Sqrt(1.0 - (sr * sr)), nx - 2);
                        results2Parameters.AddOutput("p_u", host.pval(Constant.MISSING));
                        results2Parameters.AddOutput("p_l", host.pval(Constant.MISSING));
                        results2Parameters.AddOutput("p_2", host.pval(p2Approximate));
                    }
                    else
                    {
                        results2Parameters.AddOutput("p_u", host.pval(1.0 - pl));
                        results2Parameters.AddOutput("p_l", host.pval(pl));
                        results2Parameters.AddOutput("p_2", host.pval(P * 2.0));
                    }
                }
            }
            return outputParameters;
        }

        public static ParameterBag RptNpRegression(ITemplateHost host, ParameterBag parameters)
        {
            double Intercept = 0;
            double uci; double lci; double mdn = 0;
            int i;
            int ix; int ifault; int index = 0;
            double pu;
            double ptau; double tau_ul = 0; double tau_ll = 0;
            double tau = 0; double varf = 0; double sigb; double siga; double hn = 0; double s = 0; double Q;
            double P; int nxx = 0; double ymdn = 0; double xmdn = 0;
            string taulab;
            bool fault;


            double GAMMA = parameters["gamma"].AsDouble;
            if (GAMMA <= 0.0)
                GAMMA = 0.95;
            double cit = PDF.gauinv(1.0 - (1.0 - GAMMA) / 2.0, out ifault);

            DataFrame outcomeFrame = parameters["outcome"].AsDataFrame;
            DoubleVariable v0 = outcomeFrame.Variables[0]as DoubleVariable;
            int rows = v0.Length;
            string ytitle = v0.Title;

            // If index <> 2 Then
            DataFrame predictorFrame = parameters["predictor"].AsDataFrame;
            DoubleVariable v1 = predictorFrame.Variables[0]as DoubleVariable;

            string xtitle = v1.Title;
            // Else
            // Exit Function
            // End If

            double[] x = new double[rows + 1 ];
            double[] y = new double[rows + 1 ];
            int ctr = 0;
            for (i = 1; i <= rows; i++)
            {
                if (index == 2)
                {
                    if (v1.Data[i - 1] != Constant.MISSING)
                    {
                        ctr = ctr + 1;
                        x[ctr] = v1.Data[i - 1];
                    }
                }
                else
                {
                    if (v1.Data[i - 1] != Constant.MISSING & v0.Data[i - 1] != Constant.MISSING)
                    {
                        ctr = ctr + 1;
                        x[ctr] = v1.Data[i - 1];
                        y[ctr] = v0.Data[i - 1];
                    }
                }
            }
            rows = ctr;

            if (rows <= 4)
            {
                host.Error("Too few observations", "Nonparametric Regression");
                throw new TemplateOperationCancelledException();
            }

            // get x and y medians in order to calculate intercepts later
            double[] axo = new double[rows + 1 ];
            double[] ayo = new double[rows + 1];
            for (i = 1; i <= rows; i++)
            {
                axo[i] = x[i];
                ayo[i] = y[i];
            }
            Array.Sort(axo, 1, rows);
            Array.Sort(ayo, 1, rows);
            double imdn = 0.5 * (rows + 1);
            if (imdn < 1)
            {
                imdn = 1;
            }
            if (imdn > rows)
            {
                imdn = rows;
            }
            int fiximdn = ((int)(Math.Floor(imdn)));
            if (imdn - Math.Floor(imdn) == 0)
            {
                xmdn = axo[fiximdn];
            }
            if (imdn - Math.Floor(imdn) != 0)
            {
                xmdn = axo[fiximdn] + (axo[fiximdn + 1] - axo[fiximdn]) * (imdn - Math.Floor(imdn));
            }
            if (imdn - Math.Floor(imdn) == 0)
            {
                ymdn = ayo[fiximdn];
            }
            if (imdn - Math.Floor(imdn) != 0)
            {
                ymdn = ayo[fiximdn] + (ayo[fiximdn + 1] - ayo[fiximdn]) * (imdn - Math.Floor(imdn));
            }

            // rank correlation
            x_dokend(host, ref cit, ref rows, ref x, ref y, ref nxx, out P, out Q, ref s, ref hn, out siga, out sigb, ref varf, ref tau, ref tau_ll, ref tau_ul, out fault);
            if (fault)
            {
                tau = Constant.MISSING;
                taulab = string.Empty;
                ptau = Constant.MISSING;
            }
            else
            {
                if (siga != 0 | sigb != 0)
                {
                    taulab = "tau b";
                }
                else
                {
                    taulab = "tau";
                }
                double kz;
                if (s < 0)
                {
                    kz = (s + 1.0) / Math.Sqrt(varf);
                }
                else
                {
                    kz = (s - 1.0) / Math.Sqrt(varf);
                }
                double pl = PDF.alnorm(kz);
                if (pl < 1.0 - pl)
                {
                    ptau = pl * 2.0;
                }
                else { ptau = (1.0 - pl) * 2.0; }
            }

            // regression
            P = (1.0 - GAMMA) / 2.0;
            if (P < 0 | P > 1)
            {
                P = 0.025;
            }
            MathDbl.taufromp(P, out pu, out ix, ref rows, out ifault);
            int cnt = Convert.ToInt32(rows * (rows - 1) / 2);
            if (cnt < 2000000)
            {
                double[] pws = new double[cnt + 1 ];
                if (ifault == 0)
                {
                    cnt = 0;
                    for (i = 1; i <= rows - 1; i++)
                    {
                        int j;
                        for (j = i + 1; j <= rows; j++)
                        {
                            if (x[i] != x[j])
                            {
                                cnt = cnt + 1;
                                if (x[i] != Constant.MISSING & y[j] != Constant.MISSING)
                                {
                                    pws[cnt] = (y[i] - y[j]) / (x[i] - x[j]);
                                }
                            }
                        }
                    }
                    Array.Sort(pws, 1, cnt);
                    int ri = ((int)(Math.Floor(0.5 * (cnt - ix))));
                    int SI = Convert.ToInt32(0.5 * (cnt + ix));
                    imdn = 0.5 * (cnt + 1);
                    fiximdn = ((int)(Math.Floor(imdn)));
                    if (imdn < 1)
                    {
                        imdn = 1;
                    }
                    if (imdn > cnt)
                    {
                        imdn = cnt;
                    }
                    if (imdn - Math.Floor(imdn) == 0)
                    {
                        mdn = pws[fiximdn];
                    }
                    if (imdn - Math.Floor(imdn) != 0)
                    {
                        mdn = pws[fiximdn] + (pws[fiximdn + 1] - pws[fiximdn]) * (imdn - Math.Floor(imdn));
                    }
                    lci = pws[ri];
                    uci = pws[SI];
                    Intercept = ymdn - mdn * xmdn;
                }
                else
                {
                    mdn = Constant.MISSING;
                    lci = Constant.MISSING;
                    uci = Constant.MISSING;
                }
            }
            else
            {
                mdn = Constant.MISSING;
                lci = Constant.MISSING;
                uci = Constant.MISSING;
            }

            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("ytitle", ytitle);
            outputParameters.AddOutput("xtitle", xtitle);
            outputParameters.AddOutput("obs", rows.ToString());

            if (mdn == Constant.MISSING)
            {
                // not enough memory or other error for regression
                outputParameters.AddOutput("*cannotcalculate", OneOutputElement());
                outputParameters.AddOutput("*results", null);
            }
            else
            {
                outputParameters.AddOutput("*cannotcalculate", null);
                ParameterBag resultsParameters = new ParameterBag();
                IList<ParameterBag> resultsList = new List<ParameterBag>();
                resultsList.Add(resultsParameters);
                outputParameters.AddOutput("*results", resultsList);
                resultsParameters.AddOutput("pc", (100 * GAMMA).ToString());
                resultsParameters.AddOutput("mdn", host.RoundU(mdn));
                resultsParameters.AddOutput("from", host.RoundU(lci));
                resultsParameters.AddOutput("to", host.RoundU(uci));
                resultsParameters.AddOutput("intercept", host.RoundU(Intercept));
            }

            outputParameters.AddOutput("taulab", taulab);
            outputParameters.AddOutput("tau", host.RoundU(tau));
            outputParameters.AddOutput("p_2", host.pval(ptau));

            //  Cache a few values for the chart
            outputParameters.AddOutput("mdnValue", mdn);
            outputParameters.AddOutput("interceptValue", Intercept);

            return outputParameters;
        }

        public static ParameterBag RptWilcoxon(ITemplateHost host, ParameterBag parameters)
        {
            double gamma = parameters["gamma"].AsDouble;
            if (gamma <= 0)
                throw new Exception("Gamma must be greater than zero");

            DataFrame frame = parameters["data"].AsDataFrame;
            DoubleVariable v0 = frame.Variables[0]as DoubleVariable;
            double[] x = new double[v0.Length + 1];
            double[] y = new double[v0.Length + 1];

            int n;
            string txc;
            if (frame.VariableCount == 1)
            {
                int cnt = 0;
                for (n = 0; n < v0.Length; n++)
                {
                    if (v0.Data[n] != Constant.MISSING)
                    {
                        ++cnt;
                        y[cnt] = 0;
                        x[cnt] = v0.Data[n];
                    }
                }
                n = cnt;
                txc = "(N/A * differences used)";
            }
            else
            {
                int cnt = 0;
                DoubleVariable v1 = frame.Variables[1]as DoubleVariable;
                for (n = 0; n < v0.Length; n++)
                {
                    if (v0.Data[n] != Constant.MISSING & v1.Data[n] != Constant.MISSING)
                    {
                        ++cnt;
                        y[cnt] = v1.Data[n];
                        x[cnt] = v0.Data[n];
                    }
                }
                n = cnt;
                txc = v1.Title;
            }

            if (n < 2)
                throw new TemplateOperationCancelledException();

            double xf, ned, w, pl, pu, p2;
            int n1;
            try
            {
                XWilcoxonSignedRanks(x, y, n, out w, out n1, out ned, out xf, out pl, out pu, out p2);
            }
            catch (Exception)
            {
                host.Error("Calculation Error", "Wilcoxon");
                throw new TemplateOperationCancelledException();
            }

            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("sample_1", v0.Title);
            outputParameters.AddOutput("sample_2", txc);
            outputParameters.AddOutput("non_0", n1.ToString());

            if (ned == 0)
                outputParameters.AddOutput("sum", "Sum of ranks for positive differences = " + host.RoundU(w));
            else
                outputParameters.AddOutput("sum", "Sum of signed ranks for all differences = " + host.RoundU(w));

            string adj = xf != 0 ? " (adjusted for ties)" : string.Empty;

            if (ned != 0)
            {
                outputParameters.AddOutput("stats", "Normalised statistic " + adj + "= " + host.RoundU(ned));
            }
            else
            {
                outputParameters.AddOutput("stats", "Exact probability" + adj + ":");
            }

            outputParameters.AddOutput("p_l", host.pval(pl));
            outputParameters.AddOutput("p_u", host.pval(pu));
            outputParameters.AddOutput("p_2", host.pval(p2));

            if (n1 < 4)
            {
                //  "CI not calculated if n1 or n2 < 4"
                IList<ParameterBag> noconfList = new List<ParameterBag>();
                noconfList.Add(new ParameterBag());
                outputParameters.AddOutput("*noconf", noconfList);
                outputParameters.AddOutput("*conf", null);
            }
            else
            {
                IList<ParameterBag> confList = new List<ParameterBag>();
                double lev;
                int k;
                XSRK(n, gamma, out k, out lev);
                ParameterBag confParameters = XSrcon(host, n, k, x, y);

                if (lev != -99)
                {
                    confParameters.AddOutput("pc", Formatting.XRound(lev * 100, 1) + "% confidence interval for difference between population medians:");
                    confParameters.AddOutput("k", "K = " + k.ToString());
                }
                else
                {
                    confParameters.AddOutput("pc", "Approximate " + Formatting.XRound(gamma * 100, 1) + "% confidence interval");
                    confParameters.AddOutput("k", "for difference between population medians:");
                }
                confList.Add(confParameters);
                outputParameters.AddOutput("*conf", confList);
                outputParameters.AddOutput("*noconf", null);
            }

            return outputParameters;
        }

        /// <summary>
        /// Calculate Wilcoxon signed ranks.
        /// </summary>
        /// <param name="x">1-based array of values</param>
        /// <param name="y">1-based array of values</param>
        /// <param name="n">Number of values in x and y</param>
        /// <param name="w"></param>
        /// <param name="nonzero"></param>
        /// <param name="ned"></param>
        /// <param name="xf"></param>
        /// <param name="pLower"></param>
        /// <param name="pUpper"></param>
        /// <param name="p2"></param>
        /// <param name="fault"></param>
        private static void XWilcoxonSignedRanks(double[] x, double[] y, int n, out double w, out int nonzero, out double ned, out double xf, out double pLower, out double pUpper, out double p2)
        {

            if (n < 1)
                throw new ArgumentException("At least one pair of values required", "n");

            //count the number of non-zero differences and record their signs and absolute values
            nonzero = 0;
            double[] d = new double[n + 1];
            bool[] isPositiveDifference = new bool[n + 1];
            int nz = 0;
            for (int i = 1; i <= n; i++)
            {
                double delta = x[i] - y[i];
                if (delta != 0.0)
                {
                    ++nonzero;
                    d[nonzero] = Math.Abs(delta);
                    isPositiveDifference[nonzero] = delta > 0.0;
                }
                else
                {
                    nz++;
                }
            }
            if (nonzero < 1)
                throw new ArgumentException("All differences are zero; at least one nonzero difference is required", "x, y");

            //rank the non-zero differences and calculate the tie correction factor (ties^3-ties)/12
            double[] r = new double[nonzero + 1];
            ExFortran.Rank(d, r, 1, nonzero, 1, out xf);

            //compute the test statistic as a sum of ranks for positive differences
            w = 0.0;
            for (int i = 1; i <= nonzero; i++)
            {
                if (isPositiveDifference[i])
                    w += r[i];
            }
            double wmax = nonzero * (nonzero + 1) / 2.0;

            if (n > 200)
            {
                //compute the normalized test
                double var = 0.0;
                double q = 0.0;
                for (int i = 1; i <= nonzero; i++)
                {
                    var += r[i] * r[i];
                    if (!isPositiveDifference[i])
                        q += r[i] * -1;
                    else
                        q += r[i];
                }
                ned = q / Math.Sqrt(var);

                //lower tail P
                pLower = w == wmax ? 1.0 : PDF.alnorm((q + 1.0) / var);

                //upper tail P
                if (q > 0.0)
                    pUpper = 1.0 - PDF.alnorm(ned);
                else
                    pUpper = w == 0.0 ? 1.0 : PDF.alnorm((q - 1.0) / var);

                //two tailed P	
                p2 = PDF.alnorm(ned);
                p2 = 2.0 * Math.Min(p2, 1.0 - p2);
            }
            else
            {
                //compute the exact test
                int iwmax = Convert.ToInt32(wmax);

                int[] ir = new int[nonzero + 1];
                int iw;
                if (xf == 0.0)
                {
                    for (int i = 1; i <= nonzero; i++)
                        ir[i] = Convert.ToInt32(r[i]);
                    iw = Convert.ToInt32(w);
                }
                else
                {
                    for (int i = 1; i <= nonzero; i++)
                        ir[i] = Convert.ToInt32(2 * r[i]);
                    iw = Convert.ToInt32(2 * w);
                    iwmax = 2 * iwmax;
                }
                Array.Sort(ir, 1, nonzero);

                //lower tail P
                if (iw == iwmax)
                {
                    pLower = 1.0;
                }
                else if (2 * iw > iwmax)
                {
                    pLower = XWilcoxonSignedRankLowerTailProbability(ir, iwmax - iw - 1, nonzero);
                    pLower = 1.0 - pLower;
                }
                else
                {
                    pLower = XWilcoxonSignedRankLowerTailProbability(ir, iw, nonzero);
                }

                //upper tail P
                if (iw == 0)
                {
                    pUpper = 1.0;
                }
                else if (2 * iw > iwmax)
                {
                    pUpper = XWilcoxonSignedRankLowerTailProbability(ir, iwmax - iw, nonzero);
                }
                else
                {
                    pUpper = XWilcoxonSignedRankLowerTailProbability(ir, iw - 1, nonzero);
                    pUpper = 1.0 - pUpper;
                }

                //two tailed P		
                if (2 * iw > iwmax)
                    p2 = 2.0 * XWilcoxonSignedRankLowerTailProbability(ir, iwmax - iw, nonzero);
                else
                    p2 = 2.0 * XWilcoxonSignedRankLowerTailProbability(ir, iw, nonzero);

                if (p2 > 1.0)
                    p2 = 1.0;

                ned = 0;
            }
        }

        /// <summary>
        /// Given a vector of ranks and the Wilcoxon signed ranks test statistic
        /// this calculates a lower side probability based on Norbert Neumann's
        /// shift algorithm in Statistical Software Newsletter 1988.
        /// </summary>
        /// <param name="rank"></param>
        /// <param name="score"></param>
        /// <param name="n"></param>
        /// <returns></returns>
        private static double XWilcoxonSignedRankLowerTailProbability(int[] rank, int score, int n)
        {

            int iwork = n * (n / 2) + (n / 2) + 1;
            double[] prob = new double[iwork + 1];
            int i;
            for (i = 1; i <= score + 1; i++)
                prob[i] = 1.0;

            score = Math.Abs(score);

            int upper = 0;
            for (int j = 1; j <= n; j++)
            {
                int shift = rank[j];
                if (shift >= score + 1)
                {
                    prob[score + 1] = prob[score + 1] / (Math.Pow(2, (n - j + 1)));
                    break;
                }
                upper = upper + shift;
                int limit = upper + 1;
                if (upper > score)
                    limit = score + 1;
                for (int k = limit; k >= 1; k--)
                {
                    prob[k] = 0.5 * prob[k];
                    if (shift <= k - 1)
                        prob[k] = prob[k] + 0.5 * prob[k - shift];
                }
            }
            double p = prob[score + 1];
            if (p < 0.0)
                p = 0.0;
            if (p > 1.0)
                p = 1.0;
            return p;
        }

        private static void XSRK(int sizei, double gamma, out int k, out double lev)
        {
            double alpha = (1 - gamma) / 2;
            if (sizei < 4)
                k = -99;
            else if (sizei >= 4 && sizei < 200)
                k = wsr_inv(alpha, sizei);
            else
            {
                double s = Convert.ToDouble(sizei);
                k = Convert.ToInt32(Math.Floor(((s * (s + 1)) / 4) + (PDF.gauinv(alpha) * Math.Sqrt((s * (s + 1) * ((2 * s) + 1)) / 24)))) + 1;
            }
            if (k == -99)
            {
                lev = -99;
                return;
            }
            if (sizei > 1000)
            {
                lev = 1.0 - (alpha * 2.0);
                return;
            }
            double p = wsr_p(k - 1, sizei);
            if (p > 0.5)
            {
                p = 1.0 - p;
            }
            lev = 1.0 - (p * 2.0);
        }

        /// <summary>
        /// upper tail P for Wilcoxon signed rank statistic x and sample size n
        /// </summary>
        /// <param name="x"></param>
        /// <param name="n"></param>
        /// <returns></returns>
        private static double wsr_p(double x, int n)
        {
            if (n <= 0)
                return Double.NaN;
            x = Math.Floor(x + 1e-7);
            if (x < 0.0) return 0.0;
            if (x >= n * (n + 1) / 2.0) return 1.0;

            double[] w = new double[((n * (n + 1) / 2) / 2) + 1];

            double f = Math.Exp(-n * Math.Log(2.0));
            double p = 0;
            if (x <= (n * (n + 1) / 4.0))
            {
                for (int i = 0; i <= x; i++)
                    p += wsr_enum(i, n, ref w) * f;
            }
            else
            {
                x = n * (n + 1) / 2.0 - x;
                for (int i = 0; i < x; i++)
                    p += wsr_enum(i, n, ref w) * f;
            }

            return p;
        }

        /// <summary>
        /// Inverse of Wilcoxon signed ranks statistic distribution for sample size n and upper tail probability p.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="n"></param>
        /// <returns></returns>
        private static int wsr_inv(double x, int n)
        {
            if (n <= 0)
                return -99;
            if (x <= 0)
                return 0;
            if (x >= 1)
                return n * (n + 1) / 2;

            double[] w = new double[((n * (n + 1) / 2) / 2) + 1];

            double f = Math.Exp(-n * Math.Log(2.0));
            double p = 0;
            int q = 0;
            if (x <= 0.5)
            {
                x = x - 10 * Double.Epsilon;
                for (; ; )
                {
                    p += wsr_enum(q, n, ref w) * f;
                    if (p >= x)
                        break;
                    q++;
                }
            }
            else
            {
                x = 1 - x + 10 * Double.Epsilon;
                for (; ; )
                {
                    p += wsr_enum(q, n, ref w) * f;
                    if (p > x)
                    {
                        q = n * (n + 1) / 2 - q;
                        break;
                    }
                    q++;
                }
            }

            return q;
        }

        /// <summary>
        /// enumeration within the Wilcoxon signed ranks statistic distribution
        /// </summary>
        /// <param name="k"></param>
        /// <param name="n"></param>
        /// <param name="w"></param>
        /// <returns></returns>
        private static double wsr_enum(int k, int n, ref double[] w)
        {
            int u = n * (n + 1) / 2;
            int c = (u / 2);

            if (k < 0 || k > u) return 0;
            if (k > c) k = u - k;

            if (n == 1) return 1.0;
            if (w[0] == 1.0) return w[k];

            w[0] = w[1] = 1.0;
            for (int j = 2; j < n + 1; ++j)
            {
                int fin = Math.Min(j * (j + 1) / 2, c);
                for (int i = fin; i >= j; --i)
                {
                    w[i] += w[i - j];
                }
            }

            return w[k];
        }

        private static ParameterBag XSrcon(ITemplateHost host, int size, int k, double[] x, double[] y)
        {
            double median = 0; double kl = 0;
            int occurences;
            int midl;
            int midu;
            int j;

            ParameterBag outputParameters = new ParameterBag();
            int limit = Convert.ToInt32(size * (size + 1) / 2);
            if (limit > int.MaxValue || k > int.MaxValue)
            {
                host.Error("Sample is too large for exact confidence interval calculation.", "Signed Ranks");
                outputParameters.AddOutput("from", Formatting.ASTERISK);
                outputParameters.AddOutput("to", Formatting.ASTERISK);
                outputParameters.AddOutput("med_diff", Formatting.ASTERISK);
                return outputParameters;
            }
            host.StartProgress("Calculating Confidence Interval", true);
            double bigx = x[1];
            for (j = 1; j <= size; j++)
            {
                if (x[j] > bigx)
                    bigx = x[j];
                if (y[j] > bigx)
                    bigx = y[j];
            }

            double scaler = 100000;
            do
            {
                if (bigx * scaler < Convert.ToDouble(long.MaxValue) / 10.0)
                    break;
                scaler = scaler / 10;
            }
            while (true);

            long[] xx = new long[size + 1 ];
            for (j = 1; j <= size; j++)
            {
                xx[j] = Convert.ToInt64((x[j] - y[j]) * scaler);
            }
            Array.Sort(xx, 1, size);
            if (limit % 2 == 0)
            {
                midu = Convert.ToInt32(Math.Floor((double)limit / 2) + 1);
                midl = ((int)(Math.Floor((double)limit / 2)));
            }
            else
            {
                midu = ((int)(Math.Floor((double)(limit + 1) / 2)));
                midl = midu;
            }
            bool domed = true;
            bool dokl = true;
            int goal = midu + k;
            long c = 2 * xx[1] - 1;
            int i = 0;
            while (i < midu)
            {
                c = ExFortran.pairnext(out occurences, c, xx, size);
                i = i + occurences;
                if (host.UpdateProgress(i / (double)goal))
                {
                    outputParameters.AddOutput("from", Formatting.ASTERISK);
                    outputParameters.AddOutput("to", Formatting.ASTERISK);
                    outputParameters.AddOutput("med_diff", Formatting.ASTERISK);
                    return outputParameters;
                }
                if (i >= k)
                {
                    if (dokl)
                    {
                        dokl = false;
                        kl = c / scaler / 2.0;
                    }
                }
                if (i >= midl)
                {
                    if (domed)
                    {
                        domed = false;
                        median = c / scaler / 2.0;
                    }
                }
            }
            if (midu != midl)
            {
                if (domed)
                {
                    median = c / scaler / 2.0;
                }
                else
                {
                    median = (median + c / scaler / 2.0) / 2.0;
                }
            }
            for (j = 1; j <= size; j++)
            {
                xx[j] = -xx[j];
            }
            Array.Sort(xx, 1, size);
            c = 2 * xx[1] - 1;
            i = 0;
            while (i < k)
            {
                c = ExFortran.pairnext(out occurences, c, xx, size);
                i = i + occurences;
                if (host.UpdateProgress((midu + i) / (double)goal))
                {
                    outputParameters.AddOutput("from", Formatting.ASTERISK);
                    outputParameters.AddOutput("to", Formatting.ASTERISK);
                    outputParameters.AddOutput("med_diff", Formatting.ASTERISK);
                    return outputParameters;
                }
            }
            double ku = -c / scaler / 2.0;
            host.FinishProgress();
            outputParameters.AddOutput("from", host.RoundU(kl));
            outputParameters.AddOutput("to", host.RoundU(ku));
            outputParameters.AddOutput("med_diff", host.RoundU(median));
            return outputParameters;
        }

        public static ParameterBag RptSmirnov(ITemplateHost host, ParameterBag parameters)
        {
            int n1 = 0; int n2 = 0; int ifault;
            double D; double dn; double DP;


            DataFrame frame = parameters["data"].AsDataFrame;
            DoubleVariable v0 = frame.Variables[0]as DoubleVariable;
            DoubleVariable v1 = frame.Variables[1]as DoubleVariable;

            double[] d1 = new double[v0.Length + 1 ];
            double[] d2 = new double[v1.Length + 1 ];
            foreach (double v in v0.Data)
            {
                if (v != Constant.MISSING)
                {
                    n1 = n1 + 1;
                    d1[n1] = v;
                }
            }
            foreach (double v in v1.Data)
            {
                if (v != Constant.MISSING)
                {
                    n2 = n2 + 1;
                    d2[n2] = v;
                }
            }
            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("x", v0.Title);
            outputParameters.AddOutput("y", v1.Title);
            x_kstwo(d1, n1, d2, n2, out D, out DP, out dn);
            outputParameters.AddOutput("d", host.RoundU(D));
            double P = ExFortran.ksp2(n1, n2, ref D, out ifault);
            if (ifault != 0)
            {
                P = Constant.MISSING;
            }
            outputParameters.AddOutput("p", host.pval(P));
            outputParameters.AddOutput("sample_1", v0.Title);
            outputParameters.AddOutput("sample_2", v1.Title);
            outputParameters.AddOutput("d_l", host.RoundU(DP));
            P = ExFortran.ksp2(n1, n2, ref DP, out ifault) / 2.0;
            if (ifault != 0)
            {
                P = Constant.MISSING;
            }
            outputParameters.AddOutput("p_l", host.pval_half(P));
            outputParameters.AddOutput("d_r", host.RoundU(dn));
            P = ExFortran.ksp2(n1, n2, ref dn, out ifault) / 2.0;
            if (ifault != 0)
            {
                P = Constant.MISSING;
            }
            outputParameters.AddOutput("p_r", host.pval_half(P));

            return outputParameters;
        }

        public static ParameterBag RptQuantile(ITemplateHost host, ParameterBag parameters)
        {
            bool do_conservative = parameters["conservative-ci"].AsBoolean;
            double GAMMA = parameters["gamma"].AsDouble;
            double qc = parameters["quantile"].AsDouble;
            if (qc >= 1 || qc <= 0)
                qc = 0.5;

            DataFrame frame = parameters["data"].AsDataFrame;

            double[] r = new double[frame.MaxRows + 1 ];

            ParameterBag outputParameters = new ParameterBag();
            IList<ParameterBag> variableList = new List<ParameterBag>();
            outputParameters.AddOutput("*variable", variableList);
            for (int k = 0; k < frame.VariableCount; k++)
            {
                DoubleVariable v = frame.Variables[k]as DoubleVariable;
                int rx = 0;
                foreach (double val in v.Data)
                {
                    if (val != Constant.MISSING)
                    {
                        rx++;
                        r[rx] = val;
                    }
                }
                Array.Sort(r, 1, rx);

                bool cap_lower = false; bool cap_upper = false;
                double cover = 0; double ul; double ll; double xq = 0;
                int fault;
                XQci(qc, rx, r, ref xq, GAMMA, out ll, out ul, ref cover, do_conservative, ref cap_upper, ref cap_lower, out fault);

                ParameterBag variableParameters = new ParameterBag();
                variableParameters.AddOutput("sample", v.Title);
                variableParameters.AddOutput("size", rx.ToString());
                variableParameters.AddOutput("quantile_name", qc == 0.5 ? "median" : qc.ToString());
                variableParameters.AddOutput("value", host.RoundU(xq));
                variableParameters.AddOutput("pc", (GAMMA * 100).ToString());
                variableParameters.AddOutput("type", do_conservative ? "(conservative)" : "(non-conservative)");
                string x = cap_lower ? "* " : string.Empty;
                variableParameters.AddOutput("from", x + host.RoundU(ll));
                x = cap_upper ? "* " : string.Empty;
                variableParameters.AddOutput("to", x + host.RoundU(ul));
                x = cap_lower || cap_upper ? "  (* limit capped at min/max)" : string.Empty;
                variableParameters.AddOutput("exact", host.RoundU(cover) + "%" + x);
                variableList.Add(variableParameters);
            }

            return outputParameters;
        }


        public static ParameterBag RptKendall(ITemplateHost host, ParameterBag parameters)
        {
            int ifault; int nxx = 0; int N;
            double ps;
            double gam; double tau_ul = 0; double tau_ll = 0; double tau = 0;
            double varf = 0; double sigb; double siga; double hn = 0; double s = 0; double Q; double P;
            bool fault;

            double GAMMA = parameters["gamma"].AsDouble;
            if (GAMMA <= 0.0)
            {
                GAMMA = 0.95;
            }
            double cit = PDF.gauinv(1.0 - (1.0 - GAMMA) / 2.0, out ifault);

            DataFrame frame = parameters["data"].AsDataFrame;
            DoubleVariable v0 = frame.Variables[0]as DoubleVariable;
            DoubleVariable v1 = frame.Variables[1]as DoubleVariable;
            int rx = v0.Length;
            double[] x = new double[rx + 1 ];
            double[] y = new double[rx + 1 ];
            int nx = 0;
            for (N = 0; N <= rx - 1; N++)
            {
                if (v0.Data[N] != Constant.MISSING & v1.Data[N] != Constant.MISSING)
                {
                    nx = nx + 1;
                    x[nx] = v0.Data[N];
                    y[nx] = v1.Data[N];
                }
            }
            x_dokend(host, ref cit, ref nx, ref x, ref y, ref nxx, out P, out Q, ref s, ref hn, out siga, out sigb, ref varf, ref tau, ref tau_ll, ref tau_ul, out fault);

            if (fault)
            {
                throw new TemplateOperationCancelledException();
            }

            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("sample_1", v0.Title);
            outputParameters.AddOutput("sample_2", v1.Title);
            outputParameters.AddOutput("obs", nxx.ToString());
            outputParameters.AddOutput("con", P.ToString());
            outputParameters.AddOutput("dis", Q.ToString());
            outputParameters.AddOutput("tie", Convert.ToInt64(siga + sigb).ToString());
            outputParameters.AddOutput("s", s.ToString());
            outputParameters.AddOutput("ses", host.RoundU(Math.Sqrt(varf)));
            if (P + Q > 0)
            {
                gam = (P - Q) / (P + Q);
            }
            else { gam = Constant.MISSING; }
            outputParameters.AddOutput("gam", host.RoundU(gam));

            if (siga != 0 | sigb != 0)
            {
                outputParameters.AddOutput("tb", "tau b");
            }
            else
            {
                outputParameters.AddOutput("tb", "tau");
            }
            outputParameters.AddOutput("tau", host.RoundU(tau));
            outputParameters.AddOutput("pc", Formatting.XRound(GAMMA * 100, 1));
            outputParameters.AddOutput("ll", host.RoundU(tau_ll));
            outputParameters.AddOutput("ul", host.RoundU(tau_ul));

            if (siga != 0 | sigb != 0)
            {
                outputParameters.AddOutput("adj", " (adjusted for ties)");
            }
            else
            {
                outputParameters.AddOutput("adj", string.Empty);
            }
            IList<ParameterBag> smallSampleList = nxx < 11 ? new List<ParameterBag> { new ParameterBag() } : null;
            outputParameters.AddOutput("*smallsample", smallSampleList);
            // not used simpler variance in Conover
            // kzl = ((s - 1#) * Sqr(18#)) / Sqr(CDbl(nxx) * (CDbl(nxx) - 1#) * (2# * CDbl(nxx) + 5#))
            double kz = s / Math.Sqrt(varf);
            double pl = PDF.alnorm(kz);
            if (pl < 1.0 - pl)
            {
                ps = pl;
            }
            else { ps = 1.0 - pl; }
            outputParameters.AddOutput("kz", host.RoundU(kz));
            outputParameters.AddOutput("p_u", host.pval(1.0 - pl));
            outputParameters.AddOutput("p_l", host.pval(pl));
            outputParameters.AddOutput("p_2", host.pval(ps * 2.0));
            if (s < 0)
            {
                kz = (s + 1.0) / Math.Sqrt(varf);
            }
            else
            {
                kz = (s - 1.0) / Math.Sqrt(varf);
            }
            pl = PDF.alnorm(kz);
            if (pl < 1.0 - pl)
            {
                ps = pl;
            }
            else { ps = 1.0 - pl; }
            outputParameters.AddOutput("kzcc", host.RoundU(kz));
            outputParameters.AddOutput("p_ucc", host.pval(1.0 - pl));
            outputParameters.AddOutput("p_lcc", host.pval(pl));
            outputParameters.AddOutput("p_2cc", host.pval(ps * 2.0));

            ifault = 0;
            int ls = Convert.ToInt32(s);
            //if ( Information.Err().Number != 0 ) 
            //{ 
            //    ps = Constant.MISSING; 
            //} 
            //else 
            {
                pl = 1.0 - MathDbl.kendp(ls, nxx, ref ifault);
                if (ifault != 0)
                {
                    ps = Constant.MISSING;
                    pl = Constant.MISSING;
                }
                else
                {
                    if (pl > 1.0 - pl)
                    {
                        ps = 1.0 - pl;
                    }
                    else { ps = pl; }
                }
            }
            if (siga != 0 | sigb != 0)
            {
                outputParameters.AddOutput("adjexact", " (NOT adjusted for ties)");
            }
            else
            {
                outputParameters.AddOutput("adjexact", string.Empty);
            }
            outputParameters.AddOutput("p_uexact", host.pval(1.0 - pl));
            outputParameters.AddOutput("p_lexact", host.pval(pl));
            outputParameters.AddOutput("p_2exact", host.pval(ps * 2));

            return outputParameters;
        }


        public static ParameterBag RptFriedmanSimulateExactP(ITemplateHost host, ParameterBag parameters)
        {
            DataFrame frame = parameters["data"].AsDataFrame;
            int iterations = parameters["iterations"].AsInt32;
            double ci = parameters["ci"].AsDouble;
            int seed = parameters["seed"].AsInt32;
            int boots_divisor = Math.Max(1, iterations / 1000);

            host.StartProgress("Simulating exact P", true);

            double[,] x;
            int N;
            int treatments;
            bool allAreBinary;
            bool numbersAreSmall;
            PreprocessFriedman(frame, out x, out N, out treatments, out allAreBinary, out numbersAreSmall);

            double A2 = 0;
            double B2 = 0;
            double t1 = 0;
            double t2 = 0;
            double nd = 0;
            double[] w2 = new double[treatments + 1 ];
            CalcFriedman(x, w2, N, treatments, ref A2, ref B2, ref t1, ref t2, ref nd);
            double actualT = allAreBinary ? t1 : t2;

            int q = 0;
            MersenneTwister rnd = new MersenneTwister(seed);
            int iteration;
            for (iteration = 1; iteration <= iterations; iteration++)
            {
                if (iteration % boots_divisor == 0)
                {
                    if (host.UpdateProgress(iteration / (double)iterations))
                        break;
                }
                ShuffleValuesWithinRows(x, rnd, treatments, N);
                CalcFriedman(x, w2, N, treatments, ref A2, ref B2, ref t1, ref t2, ref nd);
                double t = allAreBinary ? t1 : t2;
                if (t >= actualT)
                    q += 1;
            }
            int actualIterations = iteration - 1;

            ParameterBag outputParameters = new ParameterBag();
            double p = Convert.ToDouble(q) / Convert.ToDouble(actualIterations);
            outputParameters.AddOutput("p", host.pval(p));
            //  CI
            double ll; double ul;
            string warn;
            MathDbl.binci(Convert.ToDouble(q), Convert.ToDouble(actualIterations), out ll, out ul, ci, out warn);
            outputParameters.AddOutput("pc", Formatting.XRound(100.0 * ci, 2));
            outputParameters.AddOutput("ll", host.RoundU(ll));
            outputParameters.AddOutput("ul", host.RoundU(ul) + warn);
            outputParameters.AddOutput("k", actualIterations.ToString("N0"));
            outputParameters.AddOutput("seed_fmt", seed.ToString());
            host.FinishProgress();
            return outputParameters;
        }


        ///  <summary>
        ///  Randomly move values in each row of x between columns.  Values will never be moved between rows.
        ///  </summary>
        ///  <param name="x">The (1,1)-based array whose values are to be shuffled</param>
        ///  <param name="rnd">The random number generator from which to take values</param>
        /// <param name="cols"></param>
        /// <param name="rows"></param>
        /// <remarks></remarks>
        public static void ShuffleValuesWithinRows(double[,] x, MersenneTwister rnd, int cols, int rows)
        {
            for (int row = 1; row <= rows; row++)
            {
                //  TODO: Is there a "better" shuffle than this?
                for (int col = 1; col <= cols; col++)
                {
                    int from = rnd.NextInteger(1, cols);
                    double tmp = x[col, row];
                    x[col, row] = x[from, row];
                    x[from, row] = tmp;
                }
            }
        }


        ///  <summary>
        ///  Randomly shuffle values between lowerBound and upperBound in x.
        ///  </summary>
        ///  <param name="x">The lowerBound-based array whose values are to be shuffled</param>
        ///  <param name="rnd">The random number generator from which to take values</param>
        /// <param name="lowerBound"></param>
        /// <param name="upperBound"></param>
        /// <remarks></remarks>
        public static void ShuffleValuesWithinArray(double[] x, MersenneTwister rnd, int lowerBound, int upperBound)
        {
            for (int i = lowerBound; i <= upperBound; i++)
            {
                int from = rnd.NextInteger(lowerBound, upperBound);
                double tmp = x[i];
                x[i] = x[from];
                x[from] = tmp;
            }
        }


        public static ParameterBag RptFriedman(ITemplateHost host, ParameterBag parameters)
        {
            DataFrame frame = parameters["data"].AsDataFrame;

            double[] w2;
            int N;
            double A2 = 0;
            double B2 = 0;
            double t1 = 0;
            double t2 = 0;
            double nd = 0;
            bool allAreBinary;
            bool numbersAreSmall;
            CalcFriedman(frame, out w2, out N, ref A2, ref B2, ref t1, ref t2, ref nd, out allAreBinary, out numbersAreSmall);

            string tlist = string.Empty; string rlist = string.Empty;
            for (int D = 0; D <= frame.VariableCount - 1; D++)
            {
                if (D == 0)
                {
                    tlist = frame.Variables[D].Title;
                    rlist = Formatting.XRound(w2[D + 1] / nd, 2);
                }
                else
                {
                    tlist = tlist + ", " + frame.Variables[D].Title;
                    rlist = rlist + ", " + Formatting.XRound(w2[D + 1] / nd, 2);
                }
            }
            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("tlist", tlist);
            outputParameters.AddOutput("rlist", rlist);

            outputParameters.AddOutput("a2", host.RoundU(A2));
            outputParameters.AddOutput("nb", N.ToString());
            outputParameters.AddOutput("t1", host.RoundU(t1));
            outputParameters.AddOutput("df", (frame.VariableCount - 1).ToString());

            outputParameters.AddOutput("t2", host.RoundU(t2));

            int df = (N - 1) * (frame.VariableCount - 1);
            double dfn = frame.VariableCount - 1;
            double dfd = df;
            double P;
            if (allAreBinary)
            {
                P = PDF.chivalp(t1, dfn);
                outputParameters.AddOutput("testName", "Cochran");
            }
            else
            {
                P = PDF.fvalp(t2, dfn, dfd);
                outputParameters.AddOutput("testName", "Friedman");
            }
            outputParameters.AddOutput("p", host.pval(P));

            if (P <= 0.05)
            {
                ParameterBag messageParameters = new ParameterBag();
                messageParameters.AddOutput("msg", "At least one of your sample populations tends to yield larger observations than at least one other sample population.");
                IList<ParameterBag> messageList = new List<ParameterBag>();
                messageList.Add(messageParameters);
                outputParameters.AddOutput("*message", messageList);

            }
            else
            {
                outputParameters.AddOutput("*message", null);
            }

            if (P <= 0.05)
            {
                ParameterBag warnParameters = new ParameterBag();
                warnParameters.AddOutput("warning", "Numbers are small: use simulated exact probability instead.");
                List<ParameterBag> warnList = new List<ParameterBag> { warnParameters };
                outputParameters.AddOutput("*warn", warnList);

            }
            else
            {
                outputParameters.AddOutput("*warn", null);
            }

            //  Cache values for possible further calculation
            outputParameters.Add("w2", new FilledParameter(FilledParameterDirection.Input, w2));
            outputParameters.Add("N", new FilledParameter(FilledParameterDirection.Input, N));
            outputParameters.Add("A2", new FilledParameter(FilledParameterDirection.Input, A2));
            outputParameters.Add("B2", new FilledParameter(FilledParameterDirection.Input, B2));

            return outputParameters;
        }


        private static void PreprocessFriedman(DataFrame frame, out double[,] x, out int N, out int treatments, out bool allAreBinary, out bool numbersAreSmall)
        {
            x = new double[frame.VariableCount + 1, frame.Variables[0].Length + 1];
            allAreBinary = true;
            int qty = 0;
            int positiveCellCount = 0;
            for (int j = 0; j <= frame.Variables[0].Length - 1; j++)
            {
                bool skip = false;
                for (int D = 0; D <= frame.VariableCount - 1; D++)
                {
                    if ((frame.Variables[D] as DoubleVariable).Data[j] == Constant.MISSING)
                    {
                        skip = true;
                    }
                }
                if (!(skip))
                {
                    qty = qty + 1;
                    for (int D = 0; D <= frame.VariableCount - 1; D++)
                    {
                        double dat = (frame.Variables[D] as DoubleVariable).Data[j];
                        x[D + 1, qty] = dat;
                        if ((dat > 0))
                        {
                            positiveCellCount += 1;
                        }
                        if (allAreBinary && !((dat == 1.0 || dat == 0.0)))
                        {
                            allAreBinary = false;
                        }
                    }
                }
            }

            N = qty;
            treatments = frame.VariableCount;
            numbersAreSmall = positiveCellCount < 25;
        }


        ///  <summary>
        ///  Calculate and set w2, N, A2, B2 from the values in frame.
        ///  </summary>
        ///  <param name="frame"></param>
        ///  <param name="w2"></param>
        ///  <param name="N"></param>
        ///  <param name="A2"></param>
        ///  <param name="B2"></param>
        /// <param name="t1"></param>
        /// <param name="t2"></param>
        /// <param name="nd"></param>
        /// <param name="allAreBinary"></param>
        /// <param name="numbersAreSmall"></param>
        /// <remarks></remarks>
        private static void CalcFriedman(DataFrame frame, out double[] w2, out int N, ref double A2, ref double B2, ref double t1, ref double t2, ref double nd, out bool allAreBinary, out bool numbersAreSmall)
        {
            double[,] x;
            int treatments;
            PreprocessFriedman(frame, out x, out N, out treatments, out allAreBinary, out numbersAreSmall);
            w2 = new double[treatments + 1 ];
            CalcFriedman(x, w2, N, treatments, ref A2, ref B2, ref t1, ref t2, ref nd);
        }


        ///  <summary>
        ///  Calculate and set w2, N, A2, B2 from the values in x, N and treatments.
        ///  </summary>
        ///  <param name="x"></param>
        ///  <param name="w2">Values will be set.  Must already be defained and of length treatments.</param>
        ///  <param name="N"></param>
        ///  <param name="treatments"></param>
        ///  <param name="A2"></param>
        ///  <param name="B2"></param>
        ///  <param name="t1"></param>
        ///  <param name="t2"></param>
        ///  <param name="nd"></param>
        ///  <remarks></remarks>
        private static void CalcFriedman(double[,] x, double[] w2, int N, int treatments, ref double A2, ref double B2, ref double t1, ref double t2, ref double nd)
        {
            double[] w1 = new double[treatments + 1 ];

            if (N > 1)
            {
                double[] x1d = new double[treatments + 1 ];
                for (int col = 1; col <= treatments; col++)
                {
                    x1d[col] = x[col, 1];
                }
                double xf;
                ExFortran.Rank(x1d, w2, 1, treatments, 0, out xf);
                A2 = 0;
                for (int g = 1; g <= treatments; g++)
                {
                    A2 += w2[g] * w2[g];
                }

                if (N != 1)
                {
                    for (int j = 2; j <= N; j++)
                    {
                        for (int col = 1; col <= treatments; col++)
                        {
                            x1d[col] = x[col, j];
                        }
                        ExFortran.Rank(x1d, w1, 1, treatments, 0, out xf);
                        for (int g = 1; g <= treatments; g++)
                        {
                            w2[g] += w1[g];
                            A2 += w1[g] * w1[g];
                        }
                    }
                }

                B2 = 0;
                for (int g = 1; g <= treatments; g++)
                {
                    B2 += w2[g] * w2[g];
                }

                nd = Convert.ToDouble(N);
                double kd = Convert.ToDouble(treatments);
                B2 = B2 / nd;
                //  Conover P 370
                double C1 = ((nd * kd * (kd + 1.0) * (kd + 1.0)) / 4.0);
                t2 = (nd - 1.0) * (B2 - C1) / (A2 - B2);
                t1 = ((kd - 1.0) * (B2 * nd - nd * C1)) / (A2 - C1);
            }
        }


        public static ParameterBag RptFrMultiple(ITemplateHost host, ParameterBag parameters)
        {
            DataFrame frame = parameters["data"].AsDataFrame;
            double confidence = parameters["confidence"].AsDouble;

            double[] w2;
            int N;
            double A2 = 0;
            double B2 = 0;
            if (parameters.ContainsKey("w2") && parameters.ContainsKey("N") && parameters.ContainsKey("A2") && parameters.ContainsKey("B2"))
            {
                w2 = ((double[])(parameters["w2"].Data));
                N = parameters["N"].AsInt32;
                A2 = parameters["A2"].AsDouble;
                B2 = parameters["B2"].AsDouble;
            }
            else
            {
                //  Calculate parameters
                double t1 = 0;
                double t2 = 0;
                double nd = 0;
                bool allAreBinary;
                bool numbersAreSmall;
                CalcFriedman(frame, out w2, out N, ref A2, ref B2, ref t1, ref t2, ref nd, out allAreBinary, out numbersAreSmall);
            }

            double dfq = (N - 1) * (frame.VariableCount - 1);
            double P = confidence;
            if (P == 0)
            {
                P = 0.05;
            }
            if (P > 1.0 - P)
            {
                P = 1.0 - P;
            }
            P = P / 2.0;
            double tval = PDF.tfromp(P, dfq);
            double tcriq = Math.Pow(Math.Abs(2 * N * (A2 - B2) / (dfq)), 0.5);
            double tcrit = tcriq * tval;

            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("df", Formatting.XRound(dfq, 0));
            outputParameters.AddOutput("t", host.RoundU(tval));

            IList<ParameterBag> pairList = new List<ParameterBag>();
            for (int g = 1; g <= frame.VariableCount - 1; g++)
            {
                for (int j = g + 1; j <= frame.VariableCount; j++)
                {
                    ParameterBag pairParameters = new ParameterBag();
                    double stata = w2[g] - w2[j];
                    pairParameters.AddOutput("compare", frame.Variables[g - 1].Title + " vs. " + frame.Variables[j - 1].Title);
                    pairParameters.AddOutput("diff", Math.Abs(stata) > tcrit ? "significant" : "not significant");
                    pairParameters.AddOutput("val", "|" + host.RoundU(stata) + "| > " + host.RoundU(tcrit));
                    P = PDF.tvalp(Math.Abs(stata / tcriq), dfq);
                    if (P > 1.0 - P)
                    {
                        P = 1.0 - P;
                    }
                    pairParameters.AddOutput("p", host.pval(2.0 * P));
                    pairList.Add(pairParameters);
                }
            }
            outputParameters.AddOutput("*pair", pairList);
            return outputParameters;
        }


        public static ParameterBag RptKruskal(ITemplateHost host, ParameterBag parameters)
        {
            DataFrame frame = parameters["data"].AsDataFrame;

            int prelx = 0;
            foreach (Variable v in frame.Variables)
            {
                prelx = prelx + v.Length;
            }
            double[] x = new double[prelx + 1 ];
            int[] L = new int[frame.VariableCount + 1 ];

            int qty = 0;
            for (int D = 0; D < frame.VariableCount; D++)
            {
                int cnt = 0;
                DoubleVariable v = frame.Variables[D]as DoubleVariable;
                foreach (double val in v.Data)
                {
                    if (val != Constant.MISSING)
                    {
                        qty = qty + 1;
                        cnt = cnt + 1;
                        x[qty] = val;
                    }
                }
                L[D + 1] = cnt;
            }
            int lx = qty;

            double[] w1 = new double[lx + 1 ];
            double h;
            double ha = 0;
            double t = 0;
            int ifault;
            x_kwt(x, lx, L, frame.VariableCount, out h, ref ha, ref t, ref w1, out ifault);

            string tlist = string.Empty;
            for (int D = 0; D <= frame.VariableCount - 1; D++)
            {
                if (D > 0)
                {
                    tlist = tlist + ", ";
                }
                tlist = tlist + frame.Variables[D].Title;
            }

            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("tlist", tlist);

            int df = frame.VariableCount - 1;
            outputParameters.AddOutput("grps", frame.VariableCount.ToString());
            outputParameters.AddOutput("df", df.ToString());
            outputParameters.AddOutput("tot_obs", lx.ToString());

            outputParameters.AddOutput("*fault", ifault != 0 ? OneOutputElement() : null);
            outputParameters.AddOutput("t", host.RoundU(h));
            double P = h == Constant.MISSING ? Constant.MISSING : PDF.chivalp(h, Convert.ToDouble(df));
            outputParameters.AddOutput("p", host.pval(P));

            if (t != 0)
            {
                ParameterBag tiesParameters = new ParameterBag();
                List<ParameterBag> tiesList = new List<ParameterBag> { tiesParameters };
                outputParameters.AddOutput("*ties", tiesList);
                tiesParameters.AddOutput("t_ties", host.RoundU(ha));
                P = ha == Constant.MISSING ? Constant.MISSING : PDF.chivalp(ha, Convert.ToDouble(df));
                tiesParameters.AddOutput("p_ties", host.pval(P));
            }
            else
            {
                outputParameters.AddOutput("*ties", null);
            }

            if (P <= 0.05)
            {

                ParameterBag messageParameters = new ParameterBag();
                messageParameters.AddOutput("msg", "At least one of your sample populations tends to yield larger observations than at least one other sample population.");
                IList<ParameterBag> messageList = new List<ParameterBag>();
                messageList.Add(messageParameters);
                outputParameters.AddOutput("*message", messageList);
            }
            else
            {
                outputParameters.AddOutput("*message", null);
            }

            return outputParameters;
        }


        public static ParameterBag RptKruskalSimulateExactP(ITemplateHost host, ParameterBag parameters)
        {
            DataFrame frame = parameters["data"].AsDataFrame;
            int iterations = parameters["iterations"].AsInt32;
            double ci = parameters["ci"].AsDouble;
            int seed = parameters["seed"].AsInt32;
            int bootsDivisor = Math.Max(1, iterations / 1000);

            host.StartProgress("Simulating exact P", true);

            int prelx = 0;
            foreach (Variable v in frame.Variables)
            {
                prelx = prelx + v.Length;
            }
            double[] x = new double[prelx + 1 ];
            int[] l = new int[frame.VariableCount + 1 ];

            int qty = 0;
            for (int D = 0; D <= frame.VariableCount - 1; D++)
            {
                int cnt = 0;
                DoubleVariable v = frame.Variables[D]as DoubleVariable;
                foreach (double val in v.Data)
                {
                    if (val != Constant.MISSING)
                    {
                        qty++;
                        cnt++;
                        x[qty] = val;
                    }
                }
                l[D + 1] = cnt;
            }
            int lx = qty;

            double[] w1 = new double[lx + 1 ];
            double t = 0;
            int ifault;
            int cols = frame.VariableCount;
            XPreprocessKwt(x, lx, l, cols, ref t, ref w1, out ifault);

            double actualh; //  Without ties
            double actualha = 0; //  With ties
            XRunKwt(w1, lx, l, cols, out actualh, ref actualha, t);

            double actual = t != 0 ? actualha : actualh;

            int r = 0;
            MersenneTwister rnd = new MersenneTwister(seed);
            int iteration;
            for (iteration = 1; iteration <= iterations; iteration++)
            {
                if (iteration % bootsDivisor == 0)
                {
                    if (host.UpdateProgress(iteration / (double)iterations))
                        break;
                }
                ShuffleValuesWithinArray(w1, rnd, 1, lx);
                double h;
                double ha = 0;
                XRunKwt(w1, lx, l, cols, out h, ref ha, t);
                double thisOne = t != 0 ? ha : h;
                if (thisOne >= actual)
                {
                    r += 1;
                }
            }
            int actualIterations = iteration - 1;

            ParameterBag outputParameters = new ParameterBag();
            double p = Convert.ToDouble(r) / Convert.ToDouble(actualIterations);
            outputParameters.AddOutput("p", host.pval(p));
            //  CI
            double ll; double ul;
            string warn;
            MathDbl.binci(Convert.ToDouble(r), Convert.ToDouble(actualIterations), out ll, out ul, ci, out warn);
            outputParameters.AddOutput("pc", Formatting.XRound(100.0 * ci, 2));
            outputParameters.AddOutput("ll", host.RoundU(ll));
            outputParameters.AddOutput("ul", host.RoundU(ul) + warn);
            outputParameters.AddOutput("k", actualIterations.ToString("N0"));
            outputParameters.AddOutput("seed_fmt", seed.ToString());
            host.FinishProgress();
            return outputParameters;
        }


        public static ParameterBag RptKwMultiple(ITemplateHost host, ParameterBag parameters)
        {
            double[] ri;
            double[] x;

            DataFrame frame = parameters["data"].AsDataFrame;

            double confidence = parameters["confidence"].AsDouble;

            // treatment groups
            int k = frame.VariableCount;

            // Steel-Dwass-Critchlow-Fligner method
            double P = confidence;
            if (P == 0)
            {
                P = 0.95;
            }
            double qval = PDF.quantsr(P, Convert.ToDouble(k), 1000000.0);

            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("q", host.RoundU(qval));

            IList<ParameterBag> variableList = new List<ParameterBag>();
            for (int i = 1; i <= frame.VariableCount - 1; i++)
            {
                for (int j = i + 1; j <= k; j++)
                {

                    int i0 = i - 1;
                    int j0 = j - 1;
                    int kn = frame.Variables[i0].Length + frame.Variables[j0].Length;
                    x = new double[kn + 1 ];
                    ri = new double[kn + 1 ];

                    int ki = 0;
                    foreach (double val in (frame.Variables[i0] as DoubleVariable).Data)
                    {
                        if (val != Constant.MISSING)
                        {
                            ki = ki + 1;
                            x[ki] = val;
                        }
                    }

                    int kj = 0;
                    foreach (double val in (frame.Variables[j0] as DoubleVariable).Data)
                    {
                        if (val != Constant.MISSING)
                        {
                            kj = kj + 1;
                            x[ki + kj] = val;
                        }
                    }

                    kn = ki + kj;
                    double ct;
                    ExFortran.Rank(x, ri, 1, kn, 5, out ct);

                    double sr1 = 0.0;
                    double sr2 = 0.0;
                    for (int N = 1; N <= ki; N++)
                    {
                        sr1 = sr1 + ri[N];
                    }
                    for (int N = ki + 1; N <= ki + kj; N++)
                    {
                        sr2 = sr2 + ri[N];
                    }
                    double njj;
                    double nii;
                    double wij;
                    if (ki < kj)
                    {
                        wij = sr1;
                        nii = Convert.ToDouble(ki);
                        njj = Convert.ToDouble(kj);
                    }
                    else
                    {
                        wij = sr2;
                        nii = Convert.ToDouble(kj);
                        njj = Convert.ToDouble(ki);
                    }

                    double v = (nii * njj) / 24.0;
                    v = v * (nii + njj + 1.0 - (ct / ((nii + njj) * (nii + njj - 1.0))));
                    double wx = (wij - (nii * (nii + njj + 1)) / 2.0) / Math.Sqrt(v);

                    ParameterBag variableParameters = new ParameterBag();
                    variableParameters.AddOutput("compare", frame.Variables[i0].Title + " vs. " + frame.Variables[j0].Title);
                    variableParameters.AddOutput("diff", Math.Abs(wx) > qval ? "significant" : "not significant");
                    variableParameters.AddOutput("val", "|" + host.RoundU(wx) + "| > " + host.RoundU(qval));
                    P = 1.0 - PDF.probsr(Math.Abs(wx), Convert.ToDouble(k), 1000000.0);
                    variableParameters.AddOutput("p", host.pval(P));
                    variableList.Add(variableParameters);
                }
            }
            outputParameters.AddOutput("*dwass", variableList);

            //  Re-do Kruskal-Wallis test (from rpt_kruskal)
            int prelx = 0;
            foreach (Variable varbl in frame.Variables)
            {
                prelx = prelx + varbl.Length;
            }
            x = new double[prelx + 1];
            int[] L = new int[frame.VariableCount + 1];

            int qty = 0;
            for (int D = 0; D < frame.VariableCount; D++)
            {
                int cnt = 0;
                DoubleVariable varbl = frame.Variables[D]as DoubleVariable;
                foreach (double val in varbl.Data)
                {
                    if (val != Constant.MISSING)
                    {
                        qty = qty + 1;
                        cnt = cnt + 1;
                        x[qty] = val;
                    }
                }
                L[D + 1] = cnt;
            }
            int lx = qty;

            double[] w1 = new double[lx + 1];
            double h;
            double ha = 0;
            double t = 0;
            int ifault;
            x_kwt(x, lx, L, frame.VariableCount, out h, ref ha, ref t, ref w1, out ifault);
            //  End copy from rpt_kruskal

            // Conover-Iman method
            P = confidence;
            if (P == 0)
            {
                P = 0.05;
            }
            if (P > 1.0 - P)
            {
                P = 1.0 - P;
            }
            P = P / 2.0;
            double df = lx - frame.VariableCount;
            double tval = PDF.tfromp(P, df);
            outputParameters.AddOutput("df", Formatting.XRound(df, 0));
            outputParameters.AddOutput("t", host.RoundU(tval));

            // get rank sums for each group
            ri = new double[k + 1 ];
            qty = 0;
            for (int D = 1; D <= k; D++)
            {
                for (int N = 1; N <= L[D]; N++)
                {
                    qty = qty + 1;
                    ri[D] = ri[D] + w1[qty];
                }
            }

            // get inequalities (Fisher LSD on ranks) for each pair
            double S2 = Convert.ToDouble(lx) * (Convert.ToDouble(lx) + 1) / 12;
            double s2x = (S2 * (Convert.ToDouble(lx) - 1.0 - h)) / Convert.ToDouble(lx - frame.VariableCount);

            IList<ParameterBag> inequalityList = new List<ParameterBag>();
            for (int i = 1; i <= frame.VariableCount - 1; i++)
            {
                for (int j = i + 1; j <= k; j++)
                {
                    double stata = Math.Abs(ri[i] / L[i] - ri[j] / L[j]);
                    double statq = Math.Sqrt(s2x) * Math.Sqrt((1.0 / L[i]) + (1.0 / L[j]));
                    double statb = tval * statq;

                    ParameterBag inequalityParameters = new ParameterBag();
                    inequalityParameters.AddOutput("compare", frame.Variables[i - 1].Title + " and " + frame.Variables[j - 1].Title);
                    inequalityParameters.AddOutput("diff", stata > statb ? "significant" : "not significant");
                    inequalityParameters.AddOutput("val", host.RoundU(stata) + " > " + host.RoundU(statb));
                    P = PDF.tvalp(Math.Abs(stata / statq), df);
                    if (P > 1.0 - P)
                    {
                        P = 1.0 - P;
                    }
                    inequalityParameters.AddOutput("p", host.pval(2.0 * P));
                    inequalityList.Add(inequalityParameters);
                }
            }
            outputParameters.AddOutput("*conover", inequalityList);
            return outputParameters;
        }


        public static ParameterBag RptSqRank(ITemplateHost host, ParameterBag parameters)
        {
            double ru = 0;
            double P;
            double sj2n;
            double r4;
            double sbar;
            double t;
            int N;
            int cnt;
            int D;


            DataFrame frame = parameters["data"].AsDataFrame;

            double confidence = parameters["confidence"].AsDouble;

            double[] mean = new double[frame.VariableCount];
            int[] L = new int[frame.VariableCount];
            double[] sj = new double[frame.VariableCount];
            int nx = 0;
            for (D = 0; D < frame.VariableCount; D++)
            {
                cnt = 0;
                double sum = 0;
                DoubleVariable varbl = frame.Variables[D]as DoubleVariable;
                foreach (double val in varbl.Data)
                {
                    if (val != Constant.MISSING)
                    {
                        cnt = cnt + 1;
                        nx = nx + 1;
                        sum = sum + val;
                    }
                }
                L[D] = cnt;
                mean[D] = sum / Convert.ToDouble(cnt);
            }
            double[] x = new double[nx + 1 ];
            double[] r = new double[nx + 1 ];

            int qty = 0;
            for (D = 0; D <= frame.VariableCount - 1; D++)
            {
                DoubleVariable varbl = frame.Variables[D]as DoubleVariable;
                foreach (double val in varbl.Data)
                {
                    if (val != Constant.MISSING)
                    {
                        qty = qty + 1;
                        x[qty] = Math.Abs(val - mean[D]);
                    }
                }
            }

            ExFortran.Rank(x, r, 1, nx, 1, out t);

            ParameterBag outputParameters = new ParameterBag();
            if (frame.VariableCount > 2)
            {
                cnt = 0;
                sbar = 0;
                r4 = 0;
                sj2n = 0;
                for (D = 0; D < frame.VariableCount; D++)
                {
                    for (N = 1; N <= L[D]; N++)
                    {
                        cnt = cnt + 1;
                        sj[D] = sj[D] + r[cnt] * r[cnt];
                        r4 = r4 + Math.Pow(r[cnt], 4.0);
                    }
                    sbar = sbar + sj[D];
                    sj2n = sj2n + (sj[D] * sj[D]) / Convert.ToDouble(L[D]);
                }
                sbar = sbar / nx;
                double d2 = (1.0 / Convert.ToDouble(nx - 1)) * (r4 - Convert.ToDouble(nx) * sbar * sbar);
                double t2 = (1.0 / d2) * (sj2n - Convert.ToDouble(nx) * sbar * sbar);
                double x2 = t2;
                int df = frame.VariableCount - 1;

                outputParameters.AddOutput("x2", host.RoundU(x2));
                outputParameters.AddOutput("df", df.ToString());
                P = PDF.chivalp(x2, Convert.ToDouble(df));
                outputParameters.AddOutput("p", host.pval(P));

                if (P < confidence)
                {
                    IList<ParameterBag> pairwiseList = new List<ParameterBag>();
                    ParameterBag pairwiseParameters = new ParameterBag();
                    pairwiseList.Add(pairwiseParameters);
                    P = confidence;
                    if (P == 0)
                    {
                        P = 0.05;
                    }
                    if (P > 1 - P)
                    {
                        P = 1 - P;
                    }
                    P = P / 2;
                    df = nx - frame.VariableCount;
                    double tval = PDF.tfromp(P, Convert.ToDouble(df));
                    pairwiseParameters.AddOutput("df", df.ToString());
                    pairwiseParameters.AddOutput("t", host.RoundU(tval));
                    IList<ParameterBag> pairList = new List<ParameterBag>();
                    pairwiseParameters.AddOutput("*pair", pairList);
                    int i;
                    for (i = 0; i <= frame.VariableCount - 2; i++)
                    {
                        int j;
                        for (j = i + 1; j <= frame.VariableCount - 1; j++)
                        {
                            double stata = Math.Abs(sj[i] / Convert.ToDouble(L[i]) - sj[j] / Convert.ToDouble(L[j]));
                            double statq = Math.Sqrt(d2 * ((Convert.ToDouble(nx) - 1.0 - t2) / Convert.ToDouble(nx - frame.VariableCount))) * Math.Sqrt(1.0 / Convert.ToDouble(L[i]) + 1.0 / Convert.ToDouble(L[j]));
                            double statb = tval * statq;
                            ParameterBag pairParameters = new ParameterBag();
                            pairList.Add(pairParameters);
                            pairParameters.AddOutput("compare", frame.Variables[i].Title + " and " + frame.Variables[j].Title);
                            pairParameters.AddOutput("dif",
                                                     stata > statb
                                                         ? "VARIANCES SEEM DIFFERENT"
                                                         : "variances not different");
                            pairParameters.AddOutput("val", host.RoundU(stata) + ", " + host.RoundU(statb));
                            P = PDF.tvalp(Math.Abs(stata / statq), Convert.ToDouble(df));
                            if (P > 1.0 - P)
                            {
                                P = 1.0 - P;
                            }
                            pairParameters.AddOutput("p_pair", host.pval(2.0 * P));
                        }
                    }
                    outputParameters.AddOutput("*pairwise", pairwiseList);
                }
                else
                {
                    outputParameters.AddOutput("*pairwise", null);
                }
            }
            else
            {
                cnt = 0;
                sbar = 0;
                r4 = 0;
                sj2n = 0;
                for (D = 0; D <= frame.VariableCount - 1; D++)
                {
                    for (N = 1; N <= L[D]; N++)
                    {
                        cnt = cnt + 1;
                        sj[D] = sj[D] + r[cnt] * r[cnt];
                        r4 = r4 + Math.Pow(r[cnt], 4.0);
                    }
                    sbar = sbar + sj[D];
                    if (D == 0)
                    {
                        ru = sj[D];
                    }
                    sj2n = sj2n + (sj[D] * sj[D]) / Convert.ToDouble(L[D]);
                }
                sbar = sbar / Convert.ToDouble(nx);
                int nm = L[0] * L[1];
                double t1 = (ru - Convert.ToDouble(L[0]) * sbar) / Math.Sqrt((Convert.ToDouble(nm) / Convert.ToDouble(nx * (nx - 1))) * r4 - nm / (nx - 1.0) * sbar * sbar);
                double z = t1;
                outputParameters.AddOutput("z", host.RoundU(z));
                P = 1.0 - PDF.alnorm(Math.Abs(z));
                if (P > 1 - P)
                {
                    P = 1 - P;
                }
                outputParameters.AddOutput("p2", host.pval(P * 2));
                outputParameters.AddOutput("p1", host.pval(P));
            }
            return outputParameters;
        }


        public static ParameterBag RptGini(ITemplateHost host, ParameterBag parameters)
        {
            double sumsqdev = 0;
            double bgini2 = 0; double bgini3 = 0; double bcal = 0; double bcau = 0;
            double thetase = 0; double cit; double P0;
            double bias = 0;

            DataFrame frame = parameters["data"].AsDataFrame;
            double GAMMA = parameters["gamma"].AsDouble;
            int boots = parameters["boots"].AsInt32;
            int boots_divisor = Math.Max(1, boots / 1000);
            if (GAMMA <= 0)
                throw new TemplateOperationCancelledException();

            MathDbl.civ(0, out cit, GAMMA, out P0);

            MersenneTwister rng = new MersenneTwister(); //  Seeds itself

            double[] r = new double[frame.MaxRows + 1 ];

            ParameterBag outputParameters = new ParameterBag();
            List<ParameterBag> outputList = new List<ParameterBag>();
            outputParameters.AddOutput("*data", outputList);
            int rx = 0;
            for (int k = 0; k <= frame.VariableCount - 1; k++)
            {
                DoubleVariable v = frame.Variables[k]as DoubleVariable;
                host.StartProgress("Bootstrapping Gini coefficient for " + v.Title, true);
                rx = 0;
                double vtot = 0.0;
                foreach (double val in v.Data)
                {
                    if (val != Constant.MISSING & val > 0.0)
                    {
                        rx++;
                        r[rx] = val;
                        vtot += vtot;
                    }
                }
                double vmean = vtot / rx;
                Array.Sort(r, 1, rx);

                double sumx = 0;
                double sumy = 0;
                for (int j = 1; j <= rx; j++)
                { // ascending order required

                    sumx = sumx + r[j];
                    sumy = sumy + Convert.ToDouble(2 * j - rx - 1) * r[j];
                    sumsqdev = sumsqdev + (r[j] - vmean) * (r[j] - vmean);
                }
                double gini = sumy / (Convert.ToDouble(rx) * sumx);

                double drxm1 = Convert.ToDouble(rx - 1);
                double cv = Math.Sqrt(sumsqdev / drxm1) / vmean;

                double[] rb = new double[rx + 1 ];
                double[] ginib = new double[boots + 1 ];
                // get resample boots times
                double theta = 0.0;
                int ctr = 0;
                bool OK = true;
                int pick;
                for (int i = 1; i <= boots; i++)
                {
                    for (int j = 1; j <= rx; j++)
                    {
                        pick = Convert.ToInt32(drxm1 * rng.NextDouble()) + 1;
                        rb[j] = r[pick];
                    }
                    Array.Sort(rb, 1, rx);
                    sumx = 0.0;
                    sumy = 0.0;
                    for (int j = 1; j <= rx; j++)
                    { // ascending order required

                        sumx = sumx + rb[j];
                        sumy = sumy + Convert.ToDouble(2 * j - rx - 1) * rb[j];
                    }
                    ginib[i] = sumy / (Convert.ToDouble(rx) * sumx);
                    theta = theta + ginib[i];
                    if (ginib[i] <= gini)
                    {
                        ctr = ctr + 1;
                    }
                    if (i % boots_divisor == 0)
                    {
                        if (host.UpdateProgress(i / (double)boots))
                        {
                            OK = false;
                            break;
                        }
                    }
                }
                double bl;
                double bu;
                if (OK)
                {
                    // get bias and bootstrap variance
                    theta = theta / Convert.ToDouble(boots);
                    double thetasq = 0.0;
                    for (int i = 1; i <= boots; i++)
                    {
                        thetasq = thetasq + Math.Pow((ginib[i] - theta), 2.0);
                    }
                    thetase = Base.SafeSqrt((1.0 / Convert.ToDouble(boots - 1)) * thetasq);
                    bias = gini - theta;
                    // sort bootstraps
                    Array.Sort(ginib, 1, boots);
                    double Q = 1.0 - GAMMA > GAMMA ? 1.0 - GAMMA : GAMMA;
                    Q = (1.0 - Q) / 2.0;
                    // percentile
                    pick = Convert.ToInt32(Convert.ToDouble(boots - 1) * Q) + 1;
                    bl = ginib[pick];
                    pick = Convert.ToInt32(Convert.ToDouble(boots - 1) * (1.0 - Q)) + 1;
                    bu = ginib[pick];
                    // BC
                    int ifault;
                    // BCa
                    double bgini = 0.0;
                    for (int i = 1; i <= rx; i++)
                    {
                        sumx = 0.0;
                        sumy = 0.0;
                        for (int j = 1; j <= rx; j++)
                        {
                            if (j != i)
                            {
                                sumx = sumx + r[j];
                                sumy = sumy + Convert.ToDouble(2 * j - rx - 1) * r[j];
                            }
                        }
                        bgini = bgini + sumy / (Convert.ToDouble(rx - 1) * sumx);
                    }
                    bgini = bgini / Convert.ToDouble(rx);
                    for (int i = 1; i <= rx; i++)
                    {
                        sumx = 0.0;
                        sumy = 0.0;
                        for (int j = 1; j <= rx; j++)
                        {
                            if (j != i)
                            {
                                sumx = sumx + r[j];
                                sumy = sumy + Convert.ToDouble(2 * j - rx - 1) * r[j];
                            }
                        }
                        bgini2 = bgini2 + Math.Pow((sumy / (Convert.ToDouble(rx - 1) * sumx) - bgini), 2.0);
                        bgini3 = bgini3 + Math.Pow((sumy / (Convert.ToDouble(rx - 1) * sumx) - bgini), 3.0);
                    }
                    double accel = bgini3 / (6.0 * Math.Pow(bgini2, 1.5));
                    double z0 = ctr / (double)boots;
                    z0 = PDF.gauinv(z0, out ifault);
                    double P1 = PDF.alnorm(z0 + (z0 - cit) / (1.0 - accel * (z0 - cit)));
                    double P2 = PDF.alnorm(z0 + (z0 + cit) / (1.0 - accel * (z0 + cit)));
                    pick = Convert.ToInt32(Convert.ToDouble(boots - 1) * P1) + 1;
                    bcal = ginib[pick];
                    pick = Convert.ToInt32(Convert.ToDouble(boots - 1) * P2) + 1;
                    bcau = ginib[pick];
                }
                else
                {
                    bl = Constant.MISSING;
                    bu = Constant.MISSING;
                }
                host.FinishProgress();

                ParameterBag varParameters = new ParameterBag();
                outputList.Add(varParameters);
                varParameters.AddOutput("ti", v.Title);
                varParameters.AddOutput("n", rx.ToString());
                if (rx != v.Length)
                {
                    varParameters.AddOutput("msg", "(note " + (v.Length - rx).ToString() + " other observations not used)");
                }
                else
                {
                    varParameters.AddOutput("msg", string.Empty);
                }
                varParameters.AddOutput("cv", host.RoundU(cv));
                varParameters.AddOutput("boots", boots.ToString("N0"));
                varParameters.AddOutput("bias", host.RoundU(bias));
                varParameters.AddOutput("se", host.RoundU(thetase));

                varParameters.AddOutput("gini", host.RoundU(gini));
                varParameters.AddOutput("pc", Formatting.XRound(GAMMA * 100, 2));
                varParameters.AddOutput("from", host.RoundU(bl));
                varParameters.AddOutput("to", host.RoundU(bu));
                varParameters.AddOutput("BCafrom", host.RoundU(bcal));
                varParameters.AddOutput("BCato", host.RoundU(bcau));

                double unbias = Convert.ToDouble(rx) / Convert.ToDouble(rx - 1);
                varParameters.AddOutput("gini-unbiased", host.RoundU(gini * unbias));
                varParameters.AddOutput("from-unbiased", host.RoundU(bl * unbias));
                varParameters.AddOutput("to-unbiased", host.RoundU(bu * unbias));
                varParameters.AddOutput("BCafrom-unbiased", host.RoundU(bcal * unbias));
                varParameters.AddOutput("BCato-unbiased", host.RoundU(bcau * unbias));

            }

            //  In the single-variable case, plot as well
            if (frame.VariableCount == 1)
            {
                double[] x = new double[rx];
                double[] y = new double[rx];
                double vtot = 0.0;
                for (int j = 1; j <= rx; j++)
                {
                    vtot += r[j];
                    x[j - 1] = Convert.ToDouble(j) / Convert.ToDouble(rx);
                }
                double lasty = 0.0;
                for (int j = 1; j <= rx; j++)
                {
                    y[j - 1] = lasty + r[j] / vtot;
                    lasty = y[j - 1];
                }
                outputParameters.AddOutput("X", new DataFrame(new DoubleVariable(x), frame.Variables[0].Title));
                outputParameters.AddOutput("Y", new DataFrame(new DoubleVariable(y), frame.Variables[0].Title));
            }
            else
            {
                //  TODO: HACK: We really need a template processor that removes placeholders if they're not present
                outputParameters.AddOutput("chart", null);
            }
            return outputParameters;
        }

    }


}
