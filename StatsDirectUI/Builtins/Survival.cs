using System;
using System.Collections.Generic;
using StatsDirect.Charting;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.Templates;
using StatsDirect.Utilities;

namespace StatsDirect.Builtins
{
    public class Survival
    {
        private class Trisvar
        {
            public double TM;
            public int gp;
            public int cs;
        }

        private class TrisvarByTmThenGp : IComparer<Trisvar>
        {
            private int Compare(Trisvar x, Trisvar y)
            {
                //  First check TM
                if (x.TM > y.TM)
                    return 1;
                if (x.TM < y.TM)
                    return -1;

                //  Next check gp
                if (x.gp > y.gp)
                    return -1;
                if (x.gp < y.gp)
                    return 1;

                //  If we get here, there are no meaningful differences
                return 0;
            }
            // interface methods implemented by Compare
            int IComparer<Trisvar>.Compare(Trisvar x, Trisvar y)
            {
                return Compare(x, y);
            }

        }


        private class TrisvarByTm : IComparer<Trisvar>
        {
            private int Compare(Trisvar x, Trisvar y)
            {
                //  First check TM
                if (x.TM > y.TM)
                    return 1;
                if (x.TM < y.TM)
                    return -1;

                //  If we get here, there are no meaningful differences
                return 0;
            }
            // interface methods implemented by Compare
            int IComparer<Trisvar>.Compare(Trisvar x, Trisvar y)
            {
                return Compare(x, y);
            }

        }


        public static StepResult RptKaplan(ITemplateHost host, ParameterBag parameters)
        {
            int lc = 0; int nmax = 0; int j;
            int lap;
            int lastk = 0; int groups;
            double Stk = 0; double tk = 0;
            string gid;

            double GAMMA = parameters["gamma"].AsDouble;
            if (GAMMA <= 0.0)
            {
                GAMMA = 0.95;
            }
            int fault;
            double cit = PDF.gauinv(1.0 - (1.0 - GAMMA) / 2.0, out fault);
            double[] gpid = new double[1 + 1 /* for VB to C# conversion */];
            string[] glab = new string[1 + 1 /* for VB to C# conversion */];
            int r;
            int C;
            double[] g;

            // Store the times data
            DataFrame timesFrame = parameters["times"].AsDataFrame;
            DoubleVariable timesVariable = timesFrame.Variables[0].AsDoubleVariable;
            int rows = timesVariable.Length;
            ColumnData[] cd = new ColumnData[2 + 1 /* for VB to C# conversion */ ];
            cd[1] = new ColumnData { Title = timesVariable.Title };
            double[] t = new double[rows + 1 /* for VB to C# conversion */];
            for (r = 1; r <= rows; r++)
            {
                t[r] = timesVariable.Data[r - 1];
            }

            // Store the death/event data
            DataFrame deathsFrame = parameters["deaths"].AsDataFrame;
            DoubleVariable deathsVariable = deathsFrame.Variables[0].AsDoubleVariable;
            cd[2] = new ColumnData { Title = deathsVariable.Title };
            double[] D = new double[rows + 1 /* for VB to C# conversion */ ];
            int extra = 0;
            for (r = 1; r <= rows; r++)
            {
                D[r] = deathsVariable.Data[r - 1];
                if (D[r] > 1)
                {
                    extra = extra + Convert.ToInt32(D[r]) - 1;
                }
            }

            if (parameters.ContainsKey("groups") && parameters["groups"] != null)
            {
                DataFrame groupsFrame = parameters["groups"].AsDataFrame;
                ClassifierVariable groupsVariable = groupsFrame.Variables[0].AsClassifierVariable;
                glab = new string[groupsVariable.GroupCount + 1 /* for VB to C# conversion */ ];
                for (j = 1; j <= groupsVariable.GroupCount; j++)
                {
                    glab[j] = groupsVariable.Groups[j - 1].Label;
                }
                gid = groupsVariable.Title;
                int zbase = 0;
                foreach (double v in groupsVariable.Data)
                {
                    if (v == 0)
                    {
                        zbase = 1;
                        break; /* TRANSWARNING: check that break is in correct scope */
                    }
                }
                // Store the group data
                cd[0] = new ColumnData { Title = groupsVariable.Title };
                g = new double[rows + 1 /* for VB to C# conversion */ ];
                for (r = 1; r <= rows; r++)
                {
                    g[r] = groupsVariable.Data[r - 1] + zbase;
                }
                gpid = new double[0 + 1 /* for VB to C# conversion */ ];
                int igot = 0;
                for (r = 1; r <= rows; r++)
                {
                    double temp = g[r];
                    if (temp != Constant.MISSING)
                    {
                        bool OK = true;
                        for (j = 1; j <= igot; j++)
                        {
                            if (temp == gpid[j])
                            {
                                OK = false;
                                break; /* TRANSWARNING: check that break is in correct scope */
                            }
                        }
                        if (OK)
                        {
                            igot = igot + 1;
                            // create temp variable for copying values 
                            double[] transTemp5 = new double[igot + 1 /* for VB to C# conversion */ ];
                            Array.Copy(gpid, transTemp5, Math.Min(gpid.Length, transTemp5.Length));
                            gpid = transTemp5;
                            gpid[igot] = temp;
                        }
                    }
                }
                groups = igot;
                for (r = 1; r <= rows; r++)
                {
                    for (j = 1; j <= groups; j++)
                    {
                        if (gpid[j] == g[r])
                        {
                            g[r] = j;
                            break; /* TRANSWARNING: check that break is in correct scope */
                        }
                    }
                }
            }
            else
            {
                //  one group
                cd[0] = new ColumnData { Rows = rows };
                g = new double[rows + 1 /* for VB to C# conversion */ ];
                for (r = 1; r <= rows; r++)
                {
                    g[r] = 1;
                }
                groups = 1;
                gid = "";
            }

            // Put the data back into the Public array
            double[,] ARR2 = new double[2 + 1 /* for VB to C# conversion */, rows + extra + 1 /* for VB to C# conversion */];
            ColumnData[] CDAT1 = new ColumnData[2 + 1 /* for VB to C# conversion */ ];
            for (C = 0; C <= 2; C++)
            {
                CDAT1[C] = cd[C];
                CDAT1[C].Rows = rows + extra;
            }
            int ctr = 0;
            for (r = 1; r <= rows; r++)
            {
                if (D[r] > 1 & D[r] != Constant.MISSING)
                {
                    for (j = 1; j <= Convert.ToInt32(D[r]); j++)
                    {
                        ctr = ctr + 1;
                        ARR2[0, ctr] = g[r];
                        ARR2[1, ctr] = t[r];
                        ARR2[2, ctr] = 1;
                    }
                }
                else
                {
                    if (D[r] < 0)
                    {
                        D[r] = 0;
                    }
                    ctr = ctr + 1;
                    ARR2[0, ctr] = g[r];
                    ARR2[1, ctr] = t[r];
                    ARR2[2, ctr] = D[r];
                }
            }

            int nt = CDAT1[0].Rows;
            int[] gnx = new int[groups + 1 /* for VB to C# conversion */];
            for (lap = 1; lap <= groups; lap++)
            {
                int j2 = 0;
                for (j = 1; j <= nt; j++)
                {
                    if (ARR2[0, j] != Constant.MISSING & ARR2[0, j] == lap)
                    {
                        j2 = j2 + 1;
                    }
                }
                gnx[lap] = j2;
                if (j2 > nmax)
                {
                    nmax = j2;
                }
            }
            //  RTF_LoadTemplate("kap_meir.rtf") Then
            double[,] stime = new double[nmax + 1 /* for VB to C# conversion */, groups + 1 /* for VB to C# conversion */];
            int[,] dead = new int[nmax + 1 /* for VB to C# conversion */, groups + 1 /* for VB to C# conversion */];
            double[,] h = new double[nmax + 1 /* for VB to C# conversion */, groups + 1 /* for VB to C# conversion */];
            double[,] s = new double[nmax + 1 /* for VB to C# conversion */, groups + 1 /* for VB to C# conversion */];
            int[] cnx = new int[groups + 1 /* for VB to C# conversion */];

            bool save = parameters["save"].AsBoolean;

            ParameterBag outputParameters = new ParameterBag();
            IList<ParameterBag> groupList = new List<ParameterBag>();
            outputParameters.AddOutput("*group", groupList);
            DataFrame resultsFrame = new DataFrame();
            for (lap = 1; lap <= groups; lap++)
            {
                ParameterBag groupParameters = new ParameterBag();
                groupList.Add(groupParameters);

                int[] nat = new int[gnx[lap] + 1 + 1 /* for VB to C# conversion */];
                int[] cen = new int[gnx[lap] + 1 /* for VB to C# conversion */ ];
                int[] allcens = new int[gnx[lap] + 1 /* for VB to C# conversion */ ];
                double[] alltime = new double[gnx[lap] + 1 + 1 /* for VB to C# conversion */ ];
                int nx;
                x_plprep(ref ARR2, ref CDAT1, ref stime, ref dead, ref nat, ref cen, ref gnx, out nx, ref lap, out nt, ref allcens, ref alltime);

                if (gid.Length == 0)
                {
                    groupParameters.AddOutput("*grp", null);
                }
                else
                {
                    IList<ParameterBag> grpList = new List<ParameterBag>();
                    groupParameters.AddOutput("*grp", grpList);
                    ParameterBag grpParameters = new ParameterBag();
                    grpList.Add(grpParameters);
                    grpParameters.AddOutput("grp", gid + " = " + glab[Convert.ToInt32(gpid[lap])]);
                }
                cnx[lap] = nx;
                double[] vh = new double[nx + 1 /* for VB to C# conversion */];
                double[] vs = new double[nx + 1 /* for VB to C# conversion */];
                x_plest(host, groupParameters, ref stime, ref nat, ref dead, ref cen, ref h, ref s, ref vh, ref vs, ref nx, ref lap);
                //  median survival time
                //  Hosmer & Lemeshow
                //  Andersen PK et al.. Statistical models based on counting processes. New York: Springer-Verlag 1993.
                const int biglong = 999999;
                int imed = biglong;
                int ilp = biglong;
                int iup = 0;
                double area = 1.0 - GAMMA;
                const double P = 0.5;
                int i;
                for (i = 1; i <= cnx[lap]; i++)
                {
                    if (s[i, lap] <= P & i < imed)
                    {
                        imed = i;
                    }
                    if (s[i, lap] <= P - area & i < ilp)
                    {
                        ilp = i;
                    }
                    if (s[i, lap] >= P + area & i > iup)
                    {
                        iup = i;
                    }
                }
                if (imed == biglong)
                {
                    imed = 0;
                }
                if (ilp == biglong)
                {
                    ilp = 0;
                }
                double ul;
                double ll;
                if (vs[imed] == Constant.MISSING | ilp == 0 | iup == 0 | stime[ilp, lap] - stime[iup, lap] == 0)
                {
                    ll = Constant.MISSING;
                    ul = Constant.MISSING;
                }
                else
                {
                    double ftp = (s[iup, lap] - s[ilp, lap]) / (stime[ilp, lap] - stime[iup, lap]);
                    double vartp = vs[imed] / (ftp * ftp);
                    if (vartp >= 0)
                    {
                        ll = stime[imed, lap] - cit * Math.Sqrt(vartp);
                        ul = stime[imed, lap] + cit * Math.Sqrt(vartp);
                    }
                    else
                    {
                        ll = Constant.MISSING;
                        ul = Constant.MISSING;
                    }
                }
                groupParameters.AddOutput("med", imed != 0 ? host.RoundU(stime[imed, lap]) : "can not estimate");
                groupParameters.AddOutput("pc", Formatting.XRound(GAMMA * 100, 1));
                groupParameters.AddOutput("all", host.RoundU(ll));
                groupParameters.AddOutput("aul", host.RoundU(ul));
                //  Hosmer & Lemeshow
                //  Brookmeyer R, Crowley JJ. A confidence interval for the median survival time. Biometrics 1982;38:29-41.
                imed = 0;
                int iucl = 0;
                int ilcl = biglong;
                for (i = 1; i <= cnx[lap]; i++)
                {
                    if (s[i, lap] <= 0.5 & imed == 0)
                    {
                        imed = i;
                    }
                    if (vs[i] != Constant.MISSING & vs[i] > 0.0)
                    {
                        if (Math.Abs(s[i, lap] - 0.5) / Math.Sqrt(vs[i]) <= cit)
                        {
                            if (i < ilcl)
                            {
                                ilcl = i;
                            }
                            if (i > iucl)
                            {
                                iucl = i;
                            }
                        }
                    }
                }
                if (ilcl == biglong)
                {
                    ilcl = 0;
                }
                if (ilcl != 0)
                {
                    groupParameters.AddOutput("bll", host.RoundU(stime[ilcl, lap]));
                }
                else
                {
                    groupParameters.AddOutput("bll", imed == 0 ? Formatting.ASTERISK : Formatting.INFRESNEG);
                }
                if (iucl != 0 & iucl <= cnx[lap] & imed > 0)
                {
                    groupParameters.AddOutput("bul", host.RoundU(stime[iucl, lap]));
                }
                else
                {
                    groupParameters.AddOutput("bul", imed == 0 ? Formatting.ASTERISK : Formatting.INFRES);
                }
                //  mean survival time
                //  Hosmer & Lemeshow
                //  Andersen PK et al.. Statistical models based on counting processes. New York: Springer-Verlag 1993.
                //  get largest observed event time tk and survival Stk, and largest time TL
                int last = cnx[lap];
                for (i = last; i >= 1; i--)
                {
                    if (cen[i] == 0)
                    {
                        tk = stime[i, lap];
                        Stk = s[i, lap];
                        lastk = i;
                        break; /* TRANSWARNING: check that break is in correct scope */
                    }
                }
                double tl = stime[last, lap];
                // get mean survival time mu
                double mu = 1.0 * stime[1, lap];
                for (i = 1; i <= lastk - 1; i++)
                {
                    mu = mu + s[i, lap] * (stime[i + 1, lap] - stime[i, lap]);
                }
                if (tk != tl)
                {
                    mu = mu + Stk * (tl - tk);
                }
                //  are there both censored and uncensored at tk?
                bool last_cen = false;
                for (i = lastk; i >= 1; i--)
                {
                    if (stime[i, lap] != tk)
                    {
                        break; /* TRANSWARNING: check that break is in correct scope */
                    }
                    if (cen[i] == 1)
                    {
                        last_cen = true;
                        break; /* TRANSWARNING: check that break is in correct scope */
                    }
                }
                //  get variance of mu
                double vmu = 0;
                double totdead = 0;
                for (i = 1; i <= lastk; i++)
                {
                    double asq = 0;
                    int L;
                    for (L = i; L <= lastk - 1; L++)
                    {
                        asq = asq + s[L, lap] * (stime[L + 1, lap] - stime[L, lap]);
                    }
                    if (!(last_cen))
                    {
                        asq = asq + Stk * (tl - tk);
                    }
                    asq = asq * asq;
                    double denom = Convert.ToDouble(nat[i] * (nat[i] - dead[i, lap]));
                    if (denom != 0.0)
                    {
                        vmu = vmu + asq * Convert.ToDouble(dead[i, lap]) / denom;
                    }
                    totdead = totdead + dead[i, lap];
                }
                if (tl != tk)
                {
                    groupParameters.AddOutput("lim", "[limit: " + host.RoundU(tl) + " on " + host.RoundU(tk) + "] ");
                }
                else
                {
                    groupParameters.AddOutput("lim", "");
                }
                groupParameters.AddOutput("mu", host.RoundU(mu));
                if (totdead > 1 & vmu > 0)
                {
                    // Hosmer & Lemeshow always multiply by totdead / (totdead - 1#)
                    // SPSS does only if tk is part cens:- emailed Hosmer to check 16/4/00
                    vmu = vmu * totdead / (totdead - 1.0);
                    groupParameters.AddOutput("ll", host.RoundU(mu - Math.Sqrt(vmu) * cit));
                    groupParameters.AddOutput("ul", host.RoundU(mu + Math.Sqrt(vmu) * cit));
                }
                else
                {
                    groupParameters.AddOutput("ll", host.RoundU(Constant.MISSING));
                    groupParameters.AddOutput("ul", host.RoundU(Constant.MISSING));
                }
                if (save)
                {
                    x_plsave(resultsFrame, ref stime, ref nat, ref dead, ref s, ref h, ref vs, ref vh, ref nx, ref lap, ref GAMMA, ref lc, ref allcens, ref alltime);
                }
            }

            outputParameters.Add("ARR2", new FilledParameter(true, ARR2));
            outputParameters.Add("CDAT1", new FilledParameter(true, CDAT1));
            outputParameters.Add("h", new FilledParameter(true, h));
            outputParameters.Add("s", new FilledParameter(true, s));
            outputParameters.Add("stime", new FilledParameter(true, stime));
            outputParameters.Add("dead", new FilledParameter(true, dead));
            outputParameters.Add("ngroups", new FilledParameter(true, groups));
            outputParameters.Add("cnx", new FilledParameter(true, cnx));
            outputParameters.Add("glab", new FilledParameter(true, glab));
            if (save)
            {
                outputParameters.AddOutput("results", resultsFrame);
            }
            return new StepResult(StepSuccess.Success, outputParameters);
        }

        public static StepResult RptKaplanMeierPlots(ITemplateHost host, ParameterBag parameters)
        {
            int[] cnx = ((int[])(parameters["cnx"].Data));
            int[,] dead = ((int[,])(parameters["dead"].Data));
            string[] glab = ((string[])(parameters["glab"].Data));
            int groups = parameters["ngroups"].AsInt32;
            double[,] h = ((double[,])(parameters["h"].Data));
            double[,] s = ((double[,])(parameters["s"].Data));
            double[,] stime = ((double[,])(parameters["stime"].Data));
            bool useMarkers = parameters["use-markers"].AsBoolean;
            bool useTics = parameters["use-tics"].AsBoolean;
            ParameterBag outputParameters = new ParameterBag();
            IList<ParameterBag> chartList = new List<ParameterBag>();
            outputParameters.AddOutput("*chart", chartList);
            using (ChartRenderer ch = new ChartRenderer(ChartDefinition.Empty()))
            {
                IList<string> imageList = ch.x_plgraph(host, h, s, stime, dead, groups, cnx, glab, useTics, useMarkers);
                foreach (string rtf in imageList)
                {
                    ParameterBag chartParameters = new ParameterBag();
                    chartList.Add(chartParameters);
                    chartParameters.AddOutput("chart", rtf);
                }
            }
            return new StepResult(StepSuccess.Success, outputParameters);
        }


        private static void x_ab_lifetable_basics(ref int rows, ref double[] D, ref double[] P, ref double[] a, ref double[] sl, ref double[] rm, ref double[] r, ref double[] Q, ref double[] dd, ref double[] yl, ref double[] t, ref double[] e)
        {
            int i;

            sl[1] = 100000;
            //  sl is number living at age x = 'l'
            //  rm is death rate of interval
            //  r is range or interval length = 'n'
            //  q is probability of dying in interval
            //  dd is  number dying in interval
            //  yl is number of years lived in interval
            //  t is number of years lived beyond age x
            //  e is observed expectation of life at age x
            for (i = 1; i <= rows; i++)
            {
                rm[i] = D[i] / P[i];
                if (i == rows)
                {
                    dd[i] = sl[i];
                    Q[i] = 1.0;
                    yl[i] = sl[i] / rm[i];
                    t[i] = yl[i];
                    if (sl[i] == 0.0)
                    {
                        e[i] = Constant.MISSING;
                    }
                    else
                    {
                        e[i] = t[i] / sl[i];
                    }
                }
                else
                {
                    Q[i] = (r[i] * rm[i]) / (1.0 + (1.0 - a[i]) * r[i] * rm[i]);
                    dd[i] = sl[i] * Q[i];
                    sl[i + 1] = sl[i] - dd[i];
                    yl[i] = r[i] * (sl[i] - dd[i]) + (a[i] * r[i] * dd[i]);
                }
            }
            for (i = 1; i <= rows - 1; i++)
            {
                t[rows - i] = t[rows + 1 - i] + yl[rows - i];
                if (sl[rows - i] == 0.0)
                {
                    e[rows - i] = Constant.MISSING;
                }
                else
                {
                    e[rows - i] = t[rows - i] / sl[rows - i];
                }
            }
        }


        private static void x_ab_lifetable_median_mode(ref int rows, ref double[] dd, out double emo, out double emd, ref double[] sl, ref double[] x)
        {
            int i;

            // mode
            int j = 1;
            emo = dd[1];
            for (i = 2; i <= rows; i++)
            {
                if (emo - dd[i] <= 0.0)
                {
                    j = i;
                    emo = dd[i];
                }
            }
            emo = x[j];
            // median e
            emd = x[rows];
            for (i = 2; i <= rows; i++)
            {
                if (sl[i] - 50000.0 <= 0.0)
                {
                    emd = (50000.0 - sl[i]) / (sl[i - 1] - sl[i]);
                    emd = emd * (x[i - 1] - x[i]) + x[i];
                    break; /* TRANSWARNING: check that break is in correct scope */
                }
            }
        }


        private static void x_ab_lifetable_variance(ref int rows, ref double[] vq, ref double[] Q, ref double[] D, ref double[] a, ref double[] r, ref double[] e, ref double[] sl, ref double[] ve, ref double[] vs)
        {
            int i;
            double w;

            double[] f = new double[rows + 1 /* for VB to C# conversion */];
            double[] C = new double[rows + 1 /* for VB to C# conversion */];
            for (i = 1; i <= rows; i++)
            {
                vq[i] = (Q[i] * Q[i] * (1.0 - Q[i])) / D[i];
                if (i < rows)
                {
                    double b = ((1.0 - a[i]) * r[i]) + e[i + 1];
                    C[i] = sl[i] * sl[i] * b * b * vq[i];
                }
            }
            f[rows - 1] = C[rows - 1];
            for (i = 1; i <= rows - 2; i++)
            {
                f[rows - 1 - i] = f[rows - i] + C[rows - 1 - i];
            }
            for (i = 1; i <= rows - 1; i++)
            {
                if (sl[i] == 0.0)
                {
                    ve[i] = Constant.MISSING;
                }
                else
                {
                    ve[i] = f[i] / (sl[i] * sl[i]);
                }
                if (sl[1] == 0.0)
                {
                    vs[i] = Constant.MISSING;
                }
                else
                {
                    w = sl[i] / sl[1];
                    vs[i] = sl[1] * w * (1.0 - w);
                }
            }
            if (sl[1] == 0.0)
            {
                vs[rows] = Constant.MISSING;
            }
            else
            {
                w = sl[rows] / sl[1];
                vs[rows] = sl[1] * w * (1.0 - w);
            }
            ve[rows] = Constant.MISSING;
            vq[rows] = Constant.MISSING;
            a[rows] = Constant.MISSING;
        }


        private static string x_lifetab_interval(int i, int rows, double[] x)
        {
            int j = i == 1 ? 0 : 1;
            if (i < rows)
            {
                return Convert.ToInt32(x[i]).ToString() + " to " + Convert.ToInt32(x[i + 1] - j).ToString();
            }
            return Convert.ToInt32(x[i]).ToString() + " up";
        }


        private static void x_plest(ITemplateHost host, ParameterBag groupParameters, ref double[,] stime, ref int[] nat, ref int[,] dead, ref int[] cen, ref double[,] h, ref double[,] s, ref double[] vh, ref double[] vs, ref int nx, ref int lap)
        {
            double var = 0;
            int j;

            double s0 = 1.0;
            IList<ParameterBag> estList = new List<ParameterBag>();
            groupParameters.AddOutput("*est", estList);
            for (j = 1; j <= nx; j++)
            {
                if (nat[j] > 0)
                {
                    s0 = s0 * Convert.ToDouble(nat[j] - dead[j, lap]) / Convert.ToDouble(nat[j]);
                    if (nat[j] - dead[j, lap] > 0)
                    {
                        var = var + Convert.ToDouble(dead[j, lap]) / (Convert.ToDouble(nat[j]) * Convert.ToDouble(nat[j] - dead[j, lap]));
                    }
                    else
                    {
                        var = 0.0;
                    }
                }
                else
                {
                    s0 = 0.0;
                    var = 0.0;
                }
                s[j, lap] = s0;
                if (s0 > 0.0)
                {
                    h[j, lap] = -Math.Log(s0);
                    vs[j] = s0 * s0 * var;
                    vh[j] = var;
                }
                else
                {
                    h[j, lap] = Constant.MISSING;
                    vs[j] = Constant.MISSING;
                    vh[j] = Constant.MISSING;
                }
                ParameterBag estParameters = new ParameterBag();
                estList.Add(estParameters);
                estParameters.AddOutput("time", host.RoundU(stime[j, lap]));
                estParameters.AddOutput("risk", nat[j].ToString());
                estParameters.AddOutput("dead", dead[j, lap].ToString());
                estParameters.AddOutput("cen", cen[j].ToString());
                estParameters.AddOutput("s", host.RoundU(s[j, lap]));
                estParameters.AddOutput("ses",
                                        vs[j] == Constant.MISSING ? Formatting.ASTERISK : host.RoundU(Math.Sqrt(vs[j])));
                estParameters.AddOutput("h", h[j, lap] == Constant.MISSING ? Formatting.INFRES : host.RoundU(h[j, lap]));
                estParameters.AddOutput("seh",
                                        vh[j] == Constant.MISSING ? Formatting.ASTERISK : host.RoundU(Math.Sqrt(vh[j])));
            }
        }


        ///  <summary>
        ///  GIVEN A SYMMETRIC MATRIX ORDER N AS LOWER TRIANGLE in A() CALCULATES AN UPPER TRIANGLE, U( ), SUCH THAT UPRIME * U = A.
        ///  A MUST BE POSITIVE SEMI-DEFINITE.  ETA IS SET TO MULTIPLYING FACTOR DETERMINING EFFECTIVE 0 FOR PIVOT.
        ///  </summary>
        ///  <param name="a"></param>
        ///  <param name="N"></param>
        ///  <param name="nn"></param>
        ///  <param name="u"></param>
        ///  <param name="nullty"></param>
        ///  <param name="ifault"></param>
        ///  <remarks>ALGORITHM AS 6 APPL. STATIST. (1968) VOL.17, P.195</remarks>
        private static void x_chol(ref double[] a, ref int N, ref int nn, ref double[] u, ref int nullty, out int ifault)
        {
            int icol;
            double w = 0;

            const double eta = 0.000000001;
            ifault = 1;
            if (N <= 0)
            {
                return;
            }
            ifault = 3;
            if (nn != N * (N + 1) / 2)
            {
                return;
            }
            ifault = 2;
            nullty = 0;
            int j = 1;
            int k = 0;
            const double eta2 = eta * eta;
            int ii = 0;
            for (icol = 1; icol <= N; icol++)
            {
                ii = ii + icol;
                double x = eta2 * a[ii];
                int L = 0;
                int kk = 0;
                int irow;
                for (irow = 1; irow <= icol; irow++)
                {
                    kk = kk + irow;
                    k = k + 1;
                    w = a[k];
                    int M = j;
                    int i;
                    for (i = 1; i <= irow; i++)
                    {
                        L = L + 1;
                        if (i == irow)
                        {
                            break; /* TRANSWARNING: check that break is in correct scope */
                        }
                        w = w - u[L] * u[M];
                        M = M + 1;
                    }
                    if (irow == icol)
                    {
                        break; /* TRANSWARNING: check that break is in correct scope */
                    }
                    if (u[L] == 0)
                    {
                        if (w * w > Math.Abs(x * a[kk]))
                        {
                            return;
                        }
                        u[k] = 0;
                    }
                    else
                    {
                        u[k] = w / u[L];
                    }
                }
                if (Math.Abs(w) <= Math.Abs(eta * a[k]))
                {
                    u[k] = 0;
                    nullty = nullty + 1;
                }
                else
                {
                    if (w < 0)
                    {
                        return;
                    }
                    u[k] = Math.Sqrt(w);
                }
                j = j + icol;
            }
            ifault = 0;
        }


        public static StepResult RptWeiLachin(ITemplateHost host, ParameterBag parameters)
        {
            int ifault;
            int gid_2 = 0; int j;
            int[,] s;
            double[,] x;

            DataFrame gidFrame = parameters["gid"].AsDataFrame;
            ClassifierVariable gidVariable = gidFrame.Variables[0].AsClassifierVariable;
            if (gidVariable.GroupCount != 2)
            {
                host.Error("Group identifier must contain two groups and no missing data.", "Wei-Lachin");
                throw new TemplateOperationCancelledException();
            }
            int rows = gidVariable.Length;
            int[] g = new int[rows + 1 /* for VB to C# conversion */];
            int[] N = new int[2 + 1 /* for VB to C# conversion */];
            int gid_1 = Convert.ToInt32(gidVariable.Data[0]) + 1;
            for (j = 1; j <= rows; j++)
            {
                if (gidVariable.Data[j - 1] + 1 != gid_1)
                {
                    gid_2 = Convert.ToInt32(gidVariable.Data[j - 1]) + 1;
                }
                g[j] = Convert.ToInt32(gidVariable.Data[j - 1]) + 1;
            }
            for (j = 1; j <= rows; j++)
            {
                if (g[j] == gid_1)
                {
                    N[1] = N[1] + 1;
                }
                else if (g[j] == gid_2)
                {
                    N[2] = N[2] + 1;
                }
            }
            int nr = parameters["nr"].AsInt32;
            if (nr > 0)
            {
                double min_time = Constant.MISSING;
                s = new int[rows + 1 /* for VB to C# conversion */, nr + 1 /* for VB to C# conversion */];
                x = new double[rows + 1 /* for VB to C# conversion */, nr + 1 /* for VB to C# conversion */];

                DataFrame timesFrame = parameters["times"].AsDataFrame;
                DataFrame censorFrame = parameters["censor"].AsDataFrame;
                int r;
                for (j = 1; j <= nr; j++)
                {
                    DoubleVariable timesVariable = timesFrame.Variables[j - 1].AsDoubleVariable;
                    DoubleVariable censorVariable = censorFrame.Variables[j - 1].AsDoubleVariable;
                    for (r = 1; r <= rows; r++)
                    {
                        x[r, j] = timesVariable.Data[r - 1];
                        if (x[r, j] < min_time)
                        {
                            min_time = x[r, j];
                        }
                    }
                    for (r = 1; r <= rows; r++)
                    {
                        double dv = censorVariable.Data[r - 1];
                        if (0.0 == dv || 1.0 == dv)
                            s[r, j] = Convert.ToInt32(censorVariable.Data[r - 1]);
                        else if (Constant.MISSING == dv)
                            s[r, j] = 0;
                        else
                        {
                            host.Error("Censorship value must be 0 or 1 only.", "Wei-Lachin");
                            throw new TemplateOperationCancelledException();
                        }
                    }
                }
                //  find missing times, censor them, and code them as minimum observed time minus one
                //  if min time is 0 and all time 0 are censored, assume that is a missing data pattern
                double missing_code;
                if (min_time == 0.0)
                {
                    missing_code = 0.0;
                    for (j = 1; j <= nr; j++)
                    {
                        for (r = 1; r <= rows; r++)
                        {
                            if (x[r, j] == 0.0 & s[r, j] == 1)
                            {
                                missing_code = min_time - 1.0;
                                break; /* TRANSWARNING: check that break is in correct scope */
                            }
                        }
                    }
                }
                else
                {
                    missing_code = min_time - 1.0;
                }
                for (j = 1; j <= nr; j++)
                {
                    for (r = 1; r <= rows; r++)
                    {
                        if (x[r, j] == Constant.MISSING)
                        {
                            x[r, j] = missing_code;
                            s[r, j] = 0;
                        }
                    }
                }
            }
            else
            {
                host.Error("Must have at least one repeat", "Wei-Lachin");
                throw new TemplateOperationCancelledException();
            }
            //  RTF_LoadTemplate("wei.rtf")
            ParameterBag outputParameters = new ParameterBag();
            IList<ParameterBag> outerList = new List<ParameterBag>();
            outputParameters.AddOutput("*outer", outerList);
            ParameterBag outerParameters = new ParameterBag();
            outerList.Add(outerParameters);
            x_wl(host, outerParameters, nr, rows, N, g, s, x, 1, out ifault);
            if (ifault == 0)
            {
                outerParameters = new ParameterBag();
                outerList.Add(outerParameters);
                x_wl(host, outerParameters, nr, rows, N, g, s, x, 2, out ifault);
            }
            if (ifault != 0)
            {
                host.Error("Error in calculation, report invalid", null);
            }
            return new StepResult(StepSuccess.Success, outputParameters);
        }


        private static void x_wl(ITemplateHost host, ParameterBag outputParameters, int nr, int nt, int[] N, int[] g, int[,] s, double[,] x, int method, out int ifault)
        {
            int ifail; int nullty = 0; int j2; int i; int K2; int K1; int ipoint; int r;
            int j; int k;
            double Q = 0;
            string tx;

            int nn = ((int)(Math.Floor((double)nr * (nr + 1) / 2)));
            int[,] y = new int[2 + 1 /* for VB to C# conversion */, nt + 1 /* for VB to C# conversion */];
            int[,] D = new int[2 + 1 /* for VB to C# conversion */, nr + 1 /* for VB to C# conversion */];
            double[] qe = new double[2 + 1 /* for VB to C# conversion */];
            double[,] ees = new double[2 + 1 /* for VB to C# conversion */, nr + 1 /* for VB to C# conversion */];
            double[, ,] mu = new double[2 + 1 /* for VB to C# conversion */, nt + 1 /* for VB to C# conversion */, nr + 1 /* for VB to C# conversion */];
            double[, ,] psi = new double[2 + 1 /* for VB to C# conversion */, nt + 1 /* for VB to C# conversion */, nr + 1 /* for VB to C# conversion */];
            double[] sigma = new double[nn + 1 /* for VB to C# conversion */];
            double[] siginv = new double[nn + 1 /* for VB to C# conversion */];
            double[] wlt = new double[nr + 1 /* for VB to C# conversion */];
            double[] nruniv = new double[nr + 1 /* for VB to C# conversion */];
            double[, ,] sig = new double[2 + 1 /* for VB to C# conversion */, nr + 1 /* for VB to C# conversion */, nr + 1 /* for VB to C# conversion */];
            ifault = 0;
            for (i = 1; i <= 2; i++)
            {
                for (k = 1; k <= nr; k++)
                {
                    ees[i, k] = 0.0;
                    D[i, k] = 0;
                    for (K2 = 1; K2 <= nr; K2++)
                    {
                        sig[i, k, K2] = 0.0;
                    }
                }
            }
            //   Y = NUMBER AT RISK OF FAILURE
            //   ees: PARTIAL SUM USED in EQN. 1
            //   QE = Q*E (SEE EQN. 4)
            //   EVALUATE MU (EQN. 4)
            for (k = 1; k <= nr; k++)
            {
                for (j = 1; j <= nt; j++)
                {
                    for (i = 1; i <= 2; i++)
                    {
                        y[i, j] = 0;
                        mu[i, j, k] = 0.0;
                    }
                    if (s[j, k] == 1)
                    {
                        for (j2 = 1; j2 <= nt; j2++)
                        {
                            if (x[j2, k] >= x[j, k])
                            {
                                y[g[j2], j] = y[g[j2], j] + 1;
                            }
                        }
                        double sum = y[1, j] + y[2, j];
                        if (sum == 0.0)
                        {
                            ifault = 1;
                            return;
                        }
                        if (method == 1)
                        {
                            Q = sum / Convert.ToDouble(nt);
                        }
                        if (method == 2)
                        {
                            Q = 1.0;
                        }
                        for (i = 1; i <= 2; i++)
                        {
                            qe[i] = Q * Convert.ToDouble(y[i, j]) / sum;
                        }
                        mu[2, j, k] = qe[1];
                        mu[1, j, k] = qe[2];
                        ees[g[j], k] = ees[g[j], k] + mu[g[j], j, k];
                        D[g[j], k] = D[g[j], k] + 1;
                    }
                    else
                    {
                        mu[1, j, k] = 0.0;
                        mu[2, j, k] = 0.0;
                    }
                }
                //   EVALUATE PSI (EQN. 5)
                for (j = 1; j <= nt; j++)
                {
                    for (i = 1; i <= 2; i++)
                    {
                        psi[i, j, k] = 0.0;
                        for (j2 = 1; j2 <= nt; j2++)
                        {
                            if (g[j2] == i & x[j2, k] <= x[j, k])
                            {
                                if (s[j2, k] == 1)
                                {
                                    psi[i, j, k] = psi[i, j, k] + mu[i, j2, k] / Convert.ToDouble(y[i, j2]);
                                }
                            }
                        }
                    }
                }
            }
            //   COMPUTE ENTRIES in COVARIANCE MATRIX (SEE EQNS. 2 AND 3)
            for (K1 = 1; K1 <= nr; K1++)
            {
                for (K2 = 1; K2 <= K1; K2++)
                {
                    for (j = 1; j <= nt; j++)
                    {
                        i = g[j];
                        if (N[i] == 0)
                        {
                            ifault = 2;
                            return;
                        }
                        sig[i, K1, K2] = sig[i, K1, K2] + (mu[i, j, K1] * Convert.ToDouble(s[j, K1]) - psi[i, j, K1]) * (mu[i, j, K2] * Convert.ToDouble(s[j, K2]) - psi[i, j, K2]) / Convert.ToDouble(N[i]);
                    }
                    ipoint = ((int)(Math.Floor((double)K1 * (K1 - 1) / 2))) + K2;
                    sigma[ipoint] = (Convert.ToDouble(N[1]) * sig[1, K1, K2] + Convert.ToDouble(N[2]) * sig[2, K1, K2]) / Convert.ToDouble(nt);
                }
            }
            //   COMPUTE WEI-LACHIN UNIVARIATE TEST NRUNIV (SEE EQNS 1 AND 6)
            outputParameters.AddOutput("title",
                                       method == 1 ? "Univariate Generalised Wilcoxon (Gehan)" : "Univariate Log-Rank");
            outputParameters.AddOutput("tot", nt.ToString());
            outputParameters.AddOutput("grp_1", N[1].ToString());
            outputParameters.AddOutput("grp_2", N[2].ToString());
            IList<ParameterBag> repeatsList = new List<ParameterBag>();
            outputParameters.AddOutput("*repeats", repeatsList);
            for (k = 1; k <= nr; k++)
            {
                ParameterBag repeatsParameters = new ParameterBag();
                repeatsList.Add(repeatsParameters);
                if (Math.Abs(ees[1, k] - ees[2, k]) < Constant.EPSILON * 100.0)
                {
                    wlt[k] = 0.0;
                }
                else
                {
                    wlt[k] = (ees[1, k] - ees[2, k]) / Math.Sqrt(Convert.ToDouble(nt));
                }
                ipoint = ((int)(Math.Floor((double)k * (k + 1) / 2)));
                if (sigma[ipoint] == 0)
                {
                    ifault = 3;
                    return;
                }
                nruniv[k] = wlt[k] / Math.Sqrt(sigma[ipoint]);
                repeatsParameters.AddOutput("time", k.ToString());
                repeatsParameters.AddOutput("fail_1", D[1, k].ToString());
                repeatsParameters.AddOutput("fail_2", D[2, k].ToString());
                repeatsParameters.AddOutput("t", host.RoundU(wlt[k]));
                if (wlt[k] == 0.0)
                {
                    repeatsParameters.AddOutput("var", Formatting.ASTERISK);
                    repeatsParameters.AddOutput("chi", Formatting.ASTERISK);
                    repeatsParameters.AddOutput("p", Formatting.ASTERISK);
                }
                else
                {
                    repeatsParameters.AddOutput("var", host.RoundU(sigma[ipoint]));
                    repeatsParameters.AddOutput("chi", host.RoundU(Math.Pow(nruniv[k], 2.0)));
                    repeatsParameters.AddOutput("p", host.pval(PDF.chivalp(Math.Pow(nruniv[k], 2.0), 1.0)));
                }
            }

            //   COMPUTE INVERSE OF COVARIANCE MATRIX AND WEI-LACHIN
            //   MULTIVARIATE STATISTICS CHIOMB (FOR OMNIBUS TEST) AND
            //   NRSTOC (FOR TEST OF STOCHASTIC ORDERING)  (SEE EQN 7)
            x_syminv(ref sigma, ref nr, ref nn, ref siginv, ref nullty, out ifail);
            double chiomb = 0.0;
            double tsum = 0.0;
            double sigsum = 0.0;
            for (j = 1; j <= nr; j++)
            {
                tsum = tsum + wlt[j];
                ipoint = ((int)(Math.Floor((double)j * (j + 1) / 2)));
                chiomb = chiomb + wlt[j] * wlt[j] * siginv[ipoint];
                sigsum = sigsum + sigma[ipoint];
                int jm1 = j - 1;
                for (j2 = 1; j2 <= jm1; j2++)
                {
                    ipoint = ((int)(Math.Floor((double)j * (j - 1) / 2))) + j2;
                    chiomb = chiomb + 2.0 * wlt[j] * wlt[j2] * siginv[ipoint];
                    sigsum = sigsum + 2.0 * sigma[ipoint];
                }
            }
            double nrstoc = tsum / Math.Sqrt(sigsum);
            outputParameters.AddOutput("title_multi",
                                       method == 1
                                           ? "Multivariate Generalised Wilcoxon (Gehan)"
                                           : "Multivariate Log-Rank");
            int z_Imax = 0;
            int z_Imin = 1;
            IList<ParameterBag> covarList = new List<ParameterBag>();
            outputParameters.AddOutput("*covar", covarList);
            for (r = 1; r <= nr; r++)
            {
                z_Imax = r + z_Imax;
                tx = "";
                for (i = z_Imin; i <= z_Imax; i++)
                {
                    tx = tx + host.RoundU(sigma[i]) + "\t";
                }
                ParameterBag covarParameters = new ParameterBag();
                covarList.Add(covarParameters);
                covarParameters.AddOutput("mat", tx);
                z_Imin = z_Imax + 1;
            }
            z_Imax = 0;
            z_Imin = 1;
            IList<ParameterBag> invCovarList = new List<ParameterBag>();
            outputParameters.AddOutput("*inv_covar", invCovarList);
            for (r = 1; r <= nr; r++)
            {
                z_Imax = r + z_Imax;
                tx = "";
                for (i = z_Imin; i <= z_Imax; i++)
                {
                    tx = tx + host.RoundU(siginv[i]) + "\t";
                }
                ParameterBag invCovarParameters = new ParameterBag();
                invCovarList.Add(invCovarParameters);
                invCovarParameters.AddOutput("mat", tx);
                z_Imin = z_Imax + 1;
            }

            outputParameters.AddOutput("rep", nr.ToString());
            outputParameters.AddOutput("stat", host.RoundU(chiomb));
            outputParameters.AddOutput("p_omnibus", host.pval(PDF.chivalp(chiomb, Convert.ToDouble(nr))));
            outputParameters.AddOutput("z", host.RoundU(nrstoc));
            double P = 1.0 - PDF.alnorm(Math.Abs(nrstoc));
            if (P > 1.0 - P)
            {
                P = 1.0 - P;
            }
            outputParameters.AddOutput("p_1", host.pval(P));
            outputParameters.AddOutput("p_2", host.pval(P * 2.0));
        }


        ///  <summary>
        ///  FORMS in C( ) AS LOWER TRIANGLE, A GENERALIZED INVERSE OF THE POSITIVE SEMI-DEFINATE SYMMETRIC MATRIX A() ORDER N, STORED AS LOWER TRIANGLE.
        ///  </summary>
        ///  <param name="a"></param>
        ///  <param name="N"></param>
        ///  <param name="nn"></param>
        ///  <param name="C"></param>
        ///  <param name="nullty"></param>
        ///  <param name="ifault"></param>
        ///  <remarks>ALGORITHM AS 7 APPL. STATIST. (1968) VOL.17, P.198</remarks>
        private static void x_syminv(ref double[] a, ref int N, ref int nn, ref double[] C, ref int nullty, out int ifault)
        {
            double[] w = new double[N + 1 /* for VB to C# conversion */ ];
            x_chol(ref a, ref N, ref nn, ref C, ref nullty, out ifault);
            if (ifault != 0)
            {
                return;
            }
            int irow = N;
            int ndiag = nn;
            do
            {
                int L = ndiag;
                if (C[ndiag] != 0.0)
                {
                    int i;
                    for (i = irow; i <= N; i++)
                    {
                        w[i] = C[L];
                        L = L + i;
                    }
                    int icol = N;
                    int jcol = nn;
                    int mdiag = nn;
                    do
                    {
                        L = jcol;
                        double x = 0.0;
                        if (icol == irow)
                        {
                            x = 1.0 / w[irow];
                        }
                        int k = N;
                        do
                        {
                            if (k == irow)
                            {
                                break; /* TRANSWARNING: check that break is in correct scope */
                            }
                            x = x - w[k] * C[L];
                            k = k - 1;
                            L = L - 1;
                            if (L > mdiag)
                            {
                                L = L - k + 1;
                            }
                        }
                        while (true);
                        C[L] = x / w[irow];
                        if (icol == irow)
                        {
                            break; /* TRANSWARNING: check that break is in correct scope */
                        }
                        mdiag = mdiag - icol;
                        icol = icol - 1;
                        jcol = jcol - 1;
                    }
                    while (true);
                }
                else
                {
                    int j;
                    for (j = irow; j <= N; j++)
                    {
                        C[L] = 0;
                        L = L + j;
                    }
                }
                ndiag = ndiag - irow;
                irow = irow - 1;
            }
            while (irow != 0);
        }


        private static void x_plsave(DataFrame resultsFrame, ref double[,] stime, ref int[] nat, ref int[,] dead, ref double[,] s, ref double[,] h, ref double[] vs, ref double[] vh, ref int nx, ref int lap, ref double GAMMA, ref int lc, ref int[] allcens, ref double[] alltime)
        {
            double P = (1.0 - GAMMA) / 2;
            int ifault;
            double cit = PDF.gauinv(1.0 - P, out ifault);
            int r = 0;
            double sumn = 0.0;
            double sumd = 0.0;
            string g = Formatting.XRound(GAMMA * 100, 1);
            string grp = dead.GetUpperBound(1) > 1 ? " (group " + lap.ToString() + ")" : "";
            DoubleVariable timeVariable = new DoubleVariable(nx, "Time" + grp);
            StringVariable deathVariable = new StringVariable(nx, "Death/Event" + grp);
            DoubleVariable survivalVariable = new DoubleVariable(nx, "Survival Proportion (S)" + grp);
            DoubleVariable seVariable = new DoubleVariable(nx, "Approx. SE(S)" + grp);
            DoubleVariable selVariable = new DoubleVariable(nx, g + "% LCI S" + grp);
            DoubleVariable seuVariable = new DoubleVariable(nx, g + "% UCI S" + grp);
            DoubleVariable cumhVariable = new DoubleVariable(nx, "Cumulative Hazard (H)" + grp);
            DoubleVariable sehVariable = new DoubleVariable(nx, "Approx. SE(H)" + grp);
            DoubleVariable sehlVariable = new DoubleVariable(nx, g + "% LCI H" + grp);
            DoubleVariable sehuVariable = new DoubleVariable(nx, g + "% UCI H" + grp);
            resultsFrame.Variables.Add(timeVariable);
            resultsFrame.Variables.Add(deathVariable);
            resultsFrame.Variables.Add(survivalVariable);
            resultsFrame.Variables.Add(seVariable);
            resultsFrame.Variables.Add(selVariable);
            resultsFrame.Variables.Add(seuVariable);
            resultsFrame.Variables.Add(cumhVariable);
            resultsFrame.Variables.Add(sehVariable);
            resultsFrame.Variables.Add(sehlVariable);
            resultsFrame.Variables.Add(sehuVariable);
            for (int j = 1; j <= nx; j++)
            {
                double conus;
                double conls;
                if (nat[j] - dead[j, lap] > 0)
                {
                    sumn = sumn + (Convert.ToDouble(dead[j, lap]) / (Convert.ToDouble(nat[j]) * (Convert.ToDouble(nat[j] - dead[j, lap]))));
                    sumd = sumd + Math.Log(Convert.ToDouble(nat[j] - dead[j, lap]) / Convert.ToDouble(nat[j]));
                    if (sumd == 0.0)
                    {
                        conus = Constant.MISSING;
                        conls = Constant.MISSING;
                    }
                    else
                    {
                        double vart = sumn / (sumd * sumd);
                        if (s[j, lap] < 1.0 && s[j, lap] > 0.0)
                        {
                            double vt = Math.Log(-Math.Log(s[j, lap]));
                            conus = vt - cit * Math.Sqrt(vart);
                            conls = vt + cit * Math.Sqrt(vart);
                            conus = Math.Exp(-Math.Exp(conus));
                            conls = Math.Exp(-Math.Exp(conls));
                        }
                        else
                        {
                            conus = Constant.MISSING;
                            conls = Constant.MISSING;
                        }
                    }
                }
                else
                {
                    conus = Constant.MISSING;
                    conls = Constant.MISSING;
                }

                do
                {
                    timeVariable.set_Data(r, stime[j, lap]);
                    deathVariable.set_Data(r, allcens[r].ToString());
                    survivalVariable.set_Data(r, s[j, lap]);
                    seVariable.set_Data(r, vs[j] == Constant.MISSING ? Constant.MISSING : Math.Sqrt(vs[j]));
                    if (conus == Constant.MISSING)
                    {
                        if (vs[j] == Constant.MISSING)
                        {
                            selVariable.set_Data(r, Constant.MISSING);
                            seuVariable.set_Data(r, Constant.MISSING);
                        }
                        else
                        {
                            selVariable.set_Data(r, s[j, lap] - (cit * Math.Sqrt(vs[j])));
                            seuVariable.set_Data(r, s[j, lap] + (cit * Math.Sqrt(vs[j])));
                        }
                    }
                    else
                    {
                        selVariable.set_Data(r, conls);
                        seuVariable.set_Data(r, conus);
                    }
                    if (h[j, lap] == Constant.MISSING)
                    {
                        cumhVariable.set_Data(r, Constant.MISSING);
                        sehVariable.set_Data(r, Constant.MISSING);
                        sehlVariable.set_Data(r, Constant.MISSING);
                        sehuVariable.set_Data(r, Constant.MISSING);
                    }
                    else
                    {
                        cumhVariable.set_Data(r, h[j, lap]);
                        sehVariable.set_Data(r, Math.Sqrt(vh[j]));
                        sehlVariable.set_Data(r, h[j, lap] - (cit * Math.Sqrt(vh[j])));
                        sehuVariable.set_Data(r, h[j, lap] + (cit * Math.Sqrt(vh[j])));
                    }
                    r = r + 1;
                }
                while (alltime[r] == stime[j, lap]);
            }
            timeVariable.TruncateDataToLength(r);
            deathVariable.TruncateDataToLength(r);
            survivalVariable.TruncateDataToLength(r);
            seVariable.TruncateDataToLength(r);
            selVariable.TruncateDataToLength(r);
            seuVariable.TruncateDataToLength(r);
            cumhVariable.TruncateDataToLength(r);
            sehVariable.TruncateDataToLength(r);
            sehlVariable.TruncateDataToLength(r);
            sehuVariable.TruncateDataToLength(r);
        }


        private static void x_plprep(ref double[,] ARR2, ref ColumnData[] CDAT1, ref double[,] stime, ref int[,] dead, ref int[] nat, ref int[] cen, ref int[] gnx, out int nx, ref int lap, out int nt, ref int[] allcens, ref double[] alltime)
        {
            nt = CDAT1[0].Rows;
            Trisvar[] Q = new Trisvar[nt + 1 /* for VB to C# conversion */];
            int nxx = 0;
            for (int j = 1; j <= nt; j++)
            {
                if (ARR2[1, j] != Constant.MISSING & ARR2[1, j] != Constant.MISSING & ARR2[2, j] != Constant.MISSING)
                {
                    if (j >= 1)
                    {
                        nxx = nxx + 1;
                    }
                    Q[j] = new Trisvar { TM = ARR2[1, j], gp = Convert.ToInt32(ARR2[0, j]), cs = Convert.ToInt32(ARR2[2, j]) };
                }
            }
            Array.Sort(Q, 1, nt, new TrisvarByTmThenGp());
            nx = 0;
            nt = nxx;
            nat[1] = gnx[lap];
            for (int j = 1; j <= nt; j++)
            {
                if (Q[j].gp == lap)
                {
                    int wdr = 0;
                    nx = nx + 1;
                    stime[nx, lap] = Q[j].TM;
                    dead[nx, lap] = Q[j].cs;
                    if (dead[nx, lap] == 0)
                    {
                        wdr = 1;
                    }
                    int j2;
                    for (j2 = j; j2 <= nt - 1; j2++)
                    {
                        if (Q[j].TM == Q[j2 + 1].TM & Q[j2 + 1].gp == Q[j].gp)
                        {
                            if (Q[j2 + 1].cs == 1)
                            {
                                dead[nx, lap] = dead[nx, lap] + 1;
                            }
                            else
                            {
                                wdr = wdr + 1;
                            }
                            j = j + 1;
                        }
                        else
                        {
                            break; /* TRANSWARNING: check that break is in correct scope */
                        }
                    }
                    nat[nx + 1] = nat[nx] - dead[nx, lap] - wdr;
                    cen[nx] = wdr;
                }
            }
            int ctr = 0;
            for (int j = 1; j <= nt; j++)
            {
                if (Q[j].gp == lap)
                {
                    ctr = ctr + 1;
                    alltime[ctr] = Q[j].TM;
                    allcens[ctr] = Q[j].cs;
                }
            }
        }


        private static void x_petoprep(ITemplateHost host, ParameterBag parameters, ref int rows, out double GAMMA, ref double cit, ref int groups, ref int strata, ref double[] score, ref string gid, ref double[] gpid, ref string[] glab, ref string[] slab, out bool ifault, ref double[,] ARR2, ref ColumnData[] CDAT1)
        {
            int r; int j;
            double temp;
            bool OK;

            ifault = true;
            GAMMA = parameters["gamma"].AsDouble;
            if (GAMMA < 0)
            {
                throw new ArgumentException("gamma must be >= 0");
            }
            double P = (1.0 - GAMMA) / 2.0;
            int iifault;
            cit = PDF.gauinv(1.0 - P, out iifault);

            DataFrame gidFrame = parameters["gid"].AsDataFrame;
            ClassifierVariable gidVariable = gidFrame.Variables[0].AsClassifierVariable;
            glab = new string[gidVariable.GroupCount + 1 /* for VB to C# conversion */ ];
            for (j = 1; j <= gidVariable.GroupCount; j++)
            {
                glab[j] = gidVariable.Groups[j - 1].Label;
            }
            rows = gidVariable.Length;
            gid = gidVariable.Title;
            double[] g = new double[rows + 1 /* for VB to C# conversion */];
            for (r = 1; r <= rows; r++)
            {
                g[r] = gidVariable.Data[r - 1] + 1;
            }
            gpid = new double[0 + 1 /* for VB to C# conversion */ ];
            int igot = 0;
            for (r = 1; r <= rows; r++)
            {
                temp = g[r];
                if (temp != Constant.MISSING)
                {
                    OK = true;
                    for (j = 1; j <= igot; j++)
                    {
                        if (temp == gpid[j])
                        {
                            OK = false;
                            break; /* TRANSWARNING: check that break is in correct scope */
                        }
                    }
                    if (OK)
                    {
                        igot = igot + 1;
                        // create temp variable for copying values 
                        double[] transTemp6 = new double[igot + 1 /* for VB to C# conversion */ ];
                        Array.Copy(gpid, transTemp6, Math.Min(gpid.Length, transTemp6.Length));
                        gpid = transTemp6;
                        gpid[igot] = temp;
                    }
                }
            }
            groups = igot;
            for (r = 1; r <= rows; r++)
            {
                for (j = 1; j <= groups; j++)
                {
                    if (gpid[j] == g[r])
                    {
                        g[r] = j;
                        break; /* TRANSWARNING: check that break is in correct scope */
                    }
                }
            }

            DataFrame timesFrame = parameters["times"].AsDataFrame;
            DoubleVariable timesVariable = timesFrame.Variables[0].AsDoubleVariable;
            double[] t = new double[rows + 1 /* for VB to C# conversion */];
            for (r = 1; r <= rows; r++)
            {
                t[r] = timesVariable.Data[r - 1];
            }

            DataFrame deathsFrame = parameters["deaths"].AsDataFrame;
            DoubleVariable deathsVariable = deathsFrame.Variables[0].AsDoubleVariable;
            double[] C = new double[rows + 1 /* for VB to C# conversion */];
            for (r = 1; r <= rows; r++)
            {
                C[r] = deathsVariable.Data[r - 1];
            }

            double[] s = new double[rows + 1 /* for VB to C# conversion */];
            if (parameters.ContainsKey("strata") && parameters["strata"] != null)
            {
                DataFrame strataFrame = parameters["strata"].AsDataFrame;
                ClassifierVariable strataVariable = strataFrame.Variables[0].AsClassifierVariable;
                slab = new string[strataVariable.GroupCount + 1 /* for VB to C# conversion */];
                for (j = 1; j <= strataVariable.GroupCount; j++)
                {
                    slab[j] = strataVariable.Title + "=" + strataVariable.Groups[j - 1].Label;
                }
                for (r = 1; r <= rows; r++)
                {
                    s[r] = strataVariable.Data[r - 1];
                }
                double[] sid = new double[0 + 1 /* for VB to C# conversion */];
                int igots = 0;
                for (r = 1; r <= rows; r++)
                {
                    temp = s[r];
                    if (temp != Constant.MISSING)
                    {
                        OK = true;
                        for (j = 1; j <= igots; j++)
                        {
                            if (temp == sid[j])
                            {
                                OK = false;
                                break; /* TRANSWARNING: check that break is in correct scope */
                            }
                        }
                        if (OK)
                        {
                            igots = igots + 1;
                            // create temp variable for copying values 
                            double[] transTemp7 = new double[igots + 1 /* for VB to C# conversion */];
                            Array.Copy(sid, transTemp7, Math.Min(sid.Length, transTemp7.Length));
                            sid = transTemp7;
                            sid[igots] = temp;
                        }
                    }
                }
                strata = igots;
                for (r = 1; r <= rows; r++)
                {
                    for (j = 1; j <= strata; j++)
                    {
                        if (sid[j] == s[r])
                        {
                            s[r] = j;
                            break; /* TRANSWARNING: check that break is in correct scope */
                        }
                    }
                }
            }
            int extra = 0;
            for (r = 1; r <= rows; r++)
            {
                if (C[r] > 1)
                {
                    extra = extra + ((int)(Math.Floor(C[r]))) - 1;
                }
            }
            // put the data back into the Public array
            ARR2 = new double[3 + 1 /* for VB to C# conversion */, rows + extra + 1 /* for VB to C# conversion */];
            CDAT1 = new ColumnData[3 + 1 /* for VB to C# conversion */];
            for (j = 0; j <= 3; j++)
            {
                CDAT1[j] = new ColumnData { Rows = rows + extra };

            }
            int ctr = 0;
            for (r = 1; r <= rows; r++)
            {
                if (C[r] > 1 & C[r] != Constant.MISSING)
                {
                    for (j = 1; j <= ((int)(Math.Floor(C[r]))); j++)
                    {
                        ctr = ctr + 1;
                        ARR2[0, ctr] = g[r];
                        ARR2[1, ctr] = t[r];
                        ARR2[2, ctr] = 1;
                        ARR2[3, ctr] = s[r];
                    }
                }
                else
                {
                    if (C[r] < 0)
                    {
                        C[r] = 0;
                    }
                    ctr = ctr + 1;
                    ARR2[0, ctr] = g[r];
                    ARR2[1, ctr] = t[r];
                    ARR2[2, ctr] = C[r];
                    ARR2[3, ctr] = s[r];
                }
            }
            rows = rows + extra;
            if (groups > 2)
            {
                score = new double[groups + 1 /* for VB to C# conversion */ ];
                bool wasCancelled;
                bool use123 = host.GetBoolean("Use 1, 2, 3... etc. for group scores in trend test?", "Log-rank and Wilcoxon", true, out wasCancelled);
                if (wasCancelled)
                {
                    throw new TemplateOperationCancelledException();
                }
                if (use123)
                {
                    for (j = 1; j <= groups; j++)
                    {
                        score[j] = Convert.ToDouble(j);
                    }
                }
                else
                {
                    for (j = 1; j <= groups; j++)
                    {
                        score[j] = host.GetDouble("Score/weight for group " + j.ToString(), "Log rank & Wilcoxon", j, out wasCancelled);
                        if (wasCancelled)
                        {
                            throw new TemplateOperationCancelledException();
                        }
                    }
                }
            }
            else if (groups < 2)
            {
                throw new TemplateOperationCancelledException();
            }
            ifault = false;
        }


        public static StepResult RptAbridgedLifetable(ITemplateHost host, ParameterBag parameters)
        {
            double se; double lci; double uci;

            double eh;
            double emo; double emd;

            string uti = null;

            bool util;

            // string lifetab = "Life table"; 
            double GAMMA = parameters["gamma"].AsDouble;
            double P0 = (1.0 - GAMMA) / 2.0;
            int ifault;
            double cit = PDF.gauinv(1.0 - P0, out ifault);

            DataFrame intervalsFrame = parameters["intervals"].AsDataFrame;
            DoubleVariable intervalsVariable = intervalsFrame.Variables[0].AsDoubleVariable;
            int rows = intervalsVariable.Length + 1;
            double[] r = new double[rows + 1 /* for VB to C# conversion */];
            double[] x = new double[rows + 1 /* for VB to C# conversion */];
            double[] P = new double[rows + 1 /* for VB to C# conversion */];
            double[] D = new double[rows + 1 /* for VB to C# conversion */];
            double[] a = new double[rows + 1 /* for VB to C# conversion */];
            double[] rm = new double[rows + 1 /* for VB to C# conversion */];
            double[] Q = new double[rows + 1 /* for VB to C# conversion */];
            double[] dd = new double[rows + 1 /* for VB to C# conversion */];
            double[] sl = new double[rows + 1 /* for VB to C# conversion */];
            double[] yl = new double[rows + 1 /* for VB to C# conversion */];
            double[] e = new double[rows + 1 /* for VB to C# conversion */];
            double[] t = new double[rows + 1 /* for VB to C# conversion */];
            double[] vq = new double[rows + 1 /* for VB to C# conversion */];
            double[] ve = new double[rows + 1 /* for VB to C# conversion */];
            double[] vs = new double[rows + 1 /* for VB to C# conversion */];
            double[] u = new double[rows + 1 /* for VB to C# conversion */ ];
            x[1] = 0.0;
            for (int i = 1; i <= rows - 1; i++)
            {
                r[i] = intervalsVariable.Data[i - 1];
                if (i < rows)
                    x[i + 1] = x[i] + r[i];
            }

            DataFrame populationFrame = parameters["population"].AsDataFrame;
            DoubleVariable populationVariable = populationFrame.Variables[0].AsDoubleVariable;
            for (int i = 1; i <= rows; i++)
                P[i] = populationVariable.Data[i - 1];

            DataFrame deathsFrame = parameters["deaths"].AsDataFrame;
            DoubleVariable deathsVariable = deathsFrame.Variables[0].AsDoubleVariable;
            bool novariance = false;
            for (int i = 1; i <= rows; i++)
            {
                D[i] = Math.Abs(deathsVariable.Data[i - 1]);
                if (D[i] <= 0.0)
                    novariance = true;
            }

            if (parameters.ContainsKey("fractions") && parameters["fractions"] != null)
            {
                DataFrame fractionsFrame = parameters["fractions"].AsDataFrame;
                DoubleVariable fractionsVariable = fractionsFrame.Variables[0].AsDoubleVariable;
                for (int i = 1; i <= rows - 1; i++)
                    a[i] = fractionsVariable.Data[i - 1];
            }
            else
            {
                for (int i = 1; i <= rows; i++)
                    a[i] = 0.5;
                if (r[1] <= 1.0)
                    a[1] = 0.1;
                if (r[2] <= 5.0)
                    a[2] = 0.4;
            }

            if (parameters.ContainsKey("weights") && parameters["weights"] != null)
            {
                DataFrame weightsFrame = parameters["weights"].AsDataFrame;
                DoubleVariable weightsVariable = weightsFrame.Variables[0].AsDoubleVariable;
                uti = weightsVariable.Title;
                util = true;
                for (int i = 1; i <= rows; i++)
                    u[i] = weightsVariable.Data[i - 1];
            }
            else
            {
                util = false;
                for (int i = 1; i <= rows; i++)
                    u[i] = 1.0;
            }

            int simits = Parsing.Cint_Txt(parameters["iterations"].AsString);
            if (simits < 3000)
                simits = 3000;

            bool save_details = parameters["save"].AsBoolean;

            // simulation
            double[] dsim = new double[rows + 1 /* for VB to C# conversion */ ];
            double[] esim = new double[simits + 1 /* for VB to C# conversion */ ];
            double[] emdsim = new double[simits + 1 /* for VB to C# conversion */ ];
            PoissonRNG RNG = new PoissonRNG();
            //  RNG.Seed(DefaultSeed()) not required as the default seed is used if the RNG isn't seeded on first call
            host.StartProgress("Simulating...");
            for (int j = 1; j <= simits; j++)
            {
                if (host.UpdateProgress(j / (double)simits))
                {
                    host.FinishProgress();
                    throw new TemplateOperationCancelledException();
                }
                for (int i = 1; i <= rows; i++)
                    dsim[i] = RNG.GenPoisson(D[i]);
                x_ab_lifetable_basics(ref rows, ref dsim, ref P, ref a, ref sl, ref rm, ref r, ref Q, ref dd, ref yl, ref t, ref e);
                esim[j] = e[1];
                x_ab_lifetable_median_mode(ref rows, ref dd, out emo, out emd, ref sl, ref x);
                emdsim[j] = emd;
            }
            host.FinishProgress();
            Array.Sort(esim, 1, simits);
            double esimll = MathDbl.quantile_from_sorted(esim, simits, 0.05);
            double esimul = MathDbl.quantile_from_sorted(esim, simits, 0.95);
            Array.Sort(emdsim, 1, simits);
            double emdsimll = MathDbl.quantile_from_sorted(emdsim, simits, 0.05);
            double emdsimul = MathDbl.quantile_from_sorted(emdsim, simits, 0.95);

            // basic stats for abridged life table
            x_ab_lifetable_basics(ref rows, ref D, ref P, ref a, ref sl, ref rm, ref r, ref Q, ref dd, ref yl, ref t, ref e);

            // Chiang variances
            if (!novariance)
            {
                x_ab_lifetable_variance(ref rows, ref vq, ref Q, ref D, ref a, ref r, ref e, ref sl, ref ve, ref vs);
            }
            else
            {
                for (int i = 1; i <= rows; i++)
                {
                    ve[i] = Constant.MISSING;
                    vs[i] = Constant.MISSING;
                    vq[i] = Constant.MISSING;
                }
            }

            // mode e
            x_ab_lifetable_median_mode(ref rows, ref dd, out emo, out emd, ref sl, ref x);

            // population, deaths, death rate
            ParameterBag outputParameters = new ParameterBag();
            IList<ParameterBag> inputsList = new List<ParameterBag>();
            outputParameters.AddOutput("*inputs", inputsList);
            for (int i = 1; i <= rows; i++)
            {
                ParameterBag inputsParameters = new ParameterBag();
                inputsList.Add(inputsParameters);
                inputsParameters.AddOutput("int", x_lifetab_interval(i, rows, x));
                inputsParameters.AddOutput("pop", host.RoundU(P[i]));
                inputsParameters.AddOutput("dead", host.RoundU(D[i]));
                inputsParameters.AddOutput("rate", host.RoundU(rm[i]));
            }

            // probability of dying q, se, ci
            outputParameters.AddOutput("pc", Formatting.XRound(GAMMA * 100, 1));

            IList<ParameterBag> pdyingList = new List<ParameterBag>();
            outputParameters.AddOutput("*pdying", pdyingList);
            for (int i = 1; i <= rows; i++)
            {
                ParameterBag pdyingParameters = new ParameterBag();
                pdyingList.Add(pdyingParameters);
                pdyingParameters.AddOutput("int", x_lifetab_interval(i, rows, x));
                pdyingParameters.AddOutput("q", host.RoundU(Q[i]));
                if (vq[i] < 0.0 | vq[i] == Constant.MISSING)
                {
                    se = Constant.MISSING;
                    lci = Constant.MISSING;
                    uci = Constant.MISSING;
                }
                else
                {
                    se = Math.Sqrt(vq[i]);
                    lci = Q[i] - cit * se;
                    uci = Q[i] + cit * se;
                }
                pdyingParameters.AddOutput("se", host.RoundU(se));
                pdyingParameters.AddOutput("lci", host.RoundU(lci));
                pdyingParameters.AddOutput("uci", host.RoundU(uci));
            }

            // numbers living, dying from a standard population of usually 100k, fraction of last interval of life a
            IList<ParameterBag> livingList = new List<ParameterBag>();
            outputParameters.AddOutput("*living", livingList);
            for (int i = 1; i <= rows; i++)
            {
                ParameterBag livingParameters = new ParameterBag();
                livingList.Add(livingParameters);
                livingParameters.AddOutput("int", x_lifetab_interval(i, rows, x));
                livingParameters.AddOutput("l", Convert.ToInt32(sl[i]).ToString());
                livingParameters.AddOutput("d", Convert.ToInt32(dd[i]).ToString());
                livingParameters.AddOutput("a", host.RoundU(a[i]));
            }

            // years in interval, years beyond age x(i)
            IList<ParameterBag> yearsList = new List<ParameterBag>();
            outputParameters.AddOutput("*years", yearsList);
            for (int i = 1; i <= rows; i++)
            {
                ParameterBag yearsParameters = new ParameterBag();
                yearsList.Add(yearsParameters);
                yearsParameters.AddOutput("int", x_lifetab_interval(i, rows, x));
                yearsParameters.AddOutput("L", Convert.ToInt32(yl[i]).ToString());
                yearsParameters.AddOutput("T", Convert.ToInt32(t[i]).ToString());
            }

            // expectation of life e, se, ci
            IList<ParameterBag> expectationList = new List<ParameterBag>();
            outputParameters.AddOutput("*expectation", expectationList);
            for (int i = 1; i <= rows; i++)
            {
                ParameterBag expectationParameters = new ParameterBag();
                expectationList.Add(expectationParameters);
                expectationParameters.AddOutput("int", x_lifetab_interval(i, rows, x));
                expectationParameters.AddOutput("e", host.RoundU(e[i]));
                if (ve[i] < 0.0 | i == rows | ve[i] == Constant.MISSING)
                {
                    se = Constant.MISSING;
                    lci = Constant.MISSING;
                    uci = Constant.MISSING;
                }
                else
                {
                    se = Math.Sqrt(ve[i]);
                    lci = e[i] - cit * se;
                    uci = e[i] + cit * se;
                }
                expectationParameters.AddOutput("se", host.RoundU(se));
                expectationParameters.AddOutput("lci", host.RoundU(lci));
                expectationParameters.AddOutput("uci", host.RoundU(uci));
            }

            // healthy life expectancy
            if (util)
            {
                IList<ParameterBag> utilList = new List<ParameterBag>();
                outputParameters.AddOutput("*util", utilList);
                ParameterBag utilParameters = new ParameterBag();
                utilList.Add(utilParameters);
                utilParameters.AddOutput("uti", uti);
                IList<ParameterBag> adjustedList = new List<ParameterBag>();
                utilParameters.AddOutput("*adjusted", adjustedList);
                for (int i = 1; i <= rows; i++)
                {
                    ParameterBag adjustedParameters = new ParameterBag();
                    adjustedList.Add(adjustedParameters);
                    adjustedParameters.AddOutput("int", x_lifetab_interval(i, rows, x));
                    if (sl[i] == 0.0 || sl[i] == Constant.MISSING)
                        eh = Constant.MISSING;
                    else
                        eh = (u[i] * t[i]) / sl[i];
                    adjustedParameters.AddOutput("eh", host.RoundU(eh));
                }
            }
            else
            {
                outputParameters.AddOutput("*util", null);
            }

            outputParameters.AddOutput("med", host.RoundU(emd));
            outputParameters.AddOutput("its", simits.ToString());
            outputParameters.AddOutput("med_lci", host.RoundU(emdsimll));
            outputParameters.AddOutput("med_uci", host.RoundU(emdsimul));

            outputParameters.AddOutput("elb", host.RoundU(e[1]));
            if (ve[1] < 0.0 || ve[1] == Constant.MISSING)
            {
                se = Constant.MISSING;
                lci = Constant.MISSING;
                uci = Constant.MISSING;
            }
            else
            {
                se = Math.Sqrt(ve[1]);
                lci = e[1] - cit * se;
                uci = e[1] + cit * se;
            }
            outputParameters.AddOutput("elb_lci", host.RoundU(lci));
            outputParameters.AddOutput("elb_uci", host.RoundU(uci));
            outputParameters.AddOutput("elb_mc_lci", host.RoundU(esimll));
            outputParameters.AddOutput("elb_mc_uci", host.RoundU(esimul));

            if (save_details)
            {
                DataFrame resultsFrame = new DataFrame();
                string g = Formatting.XRound(GAMMA * 100, 1);
                StringVariable intervalVariable = new StringVariable(rows, "Interval");
                DoubleVariable qHatVariable = new DoubleVariable(rows, "Prob of dying [q hat]");
                DoubleVariable varQVariable = new DoubleVariable(rows, "Var [q]");
                DoubleVariable lciQVariable = new DoubleVariable(rows, g + "% LCI [q]");
                DoubleVariable uciQVariable = new DoubleVariable(rows, g + "% UCI [q]");
                DoubleVariable lVariable = new DoubleVariable(rows, "Alive at start [l]");
                DoubleVariable dVariable = new DoubleVariable(rows, "Dying in interval [d]");
                DoubleVariable fractionAVariable = new DoubleVariable(rows, "Fraction a");
                DoubleVariable ylVariable = new DoubleVariable(rows, "Years in interval [L]");
                DoubleVariable TVariable = new DoubleVariable(rows, "Years beyond [T]");
                DoubleVariable eVariable = new DoubleVariable(rows, "Expectation of life [e]");
                DoubleVariable varEVariable = new DoubleVariable(rows, "Var [e]");
                DoubleVariable lciEVariable = new DoubleVariable(rows, g + "% LCI [e]");
                DoubleVariable uciEVariable = new DoubleVariable(rows, g + "% UCI [e]");
                DoubleVariable aEVariable = new DoubleVariable(rows, "Adj. expectn. of life [Ae]");

                resultsFrame.Variables.Add(intervalVariable);
                resultsFrame.Variables.Add(qHatVariable);
                resultsFrame.Variables.Add(varQVariable);
                resultsFrame.Variables.Add(lciQVariable);
                resultsFrame.Variables.Add(uciQVariable);
                resultsFrame.Variables.Add(lVariable);
                resultsFrame.Variables.Add(dVariable);
                resultsFrame.Variables.Add(fractionAVariable);
                resultsFrame.Variables.Add(ylVariable);
                resultsFrame.Variables.Add(TVariable);
                resultsFrame.Variables.Add(eVariable);
                resultsFrame.Variables.Add(varEVariable);
                resultsFrame.Variables.Add(lciEVariable);
                resultsFrame.Variables.Add(uciEVariable);
                if (util)
                    resultsFrame.Variables.Add(aEVariable);

                for (int i = 1; i <= rows; i++)
                {
                    int j = i == 1 ? 0 : 1;
                    string xx;
                    if (i < rows)
                        xx = Convert.ToInt32(x[i]).ToString() + " to " + Convert.ToInt32(x[i + 1] - j).ToString();
                    else
                        xx = Convert.ToInt32(x[i]).ToString() + " up";
                    intervalVariable.set_Data(i - 1, xx);
                    qHatVariable.set_Data(i - 1, Q[i]);
                    varQVariable.set_Data(i - 1, vq[i]);
                    if (vq[i] != Constant.MISSING & vq[i] >= 0.0)
                    {
                        lci = Q[i] - Math.Sqrt(vq[i]) * cit;
                        uci = Q[i] + Math.Sqrt(vq[i]) * cit;
                    }
                    else
                    {
                        lci = Constant.MISSING;
                        uci = Constant.MISSING;
                    }
                    lciQVariable.set_Data(i - 1, lci);
                    uciQVariable.set_Data(i - 1, uci);
                    lVariable.set_Data(i - 1, sl[i]);
                    dVariable.set_Data(i - 1, dd[i]);
                    fractionAVariable.set_Data(i - 1, a[i]);
                    ylVariable.set_Data(i - 1, yl[i]);
                    TVariable.set_Data(i - 1, t[i]);
                    eVariable.set_Data(i - 1, e[i]);
                    varEVariable.set_Data(i - 1, ve[i]);
                    if (ve[i] != Constant.MISSING & ve[i] >= 0.0)
                    {
                        lci = e[i] - Math.Sqrt(ve[i]) * cit;
                        uci = e[i] + Math.Sqrt(ve[i]) * cit;
                    }
                    else
                    {
                        lci = Constant.MISSING;
                        uci = Constant.MISSING;
                    }
                    lciEVariable.set_Data(i - 1, lci);
                    uciEVariable.set_Data(i - 1, uci);
                    if (util)
                    {
                        if (sl[i] == 0.0 | sl[i] == Constant.MISSING)
                        {
                            eh = Constant.MISSING;
                        }
                        else
                        {
                            eh = (u[i] * t[i]) / sl[i];
                        }
                        aEVariable.set_Data(i - 1, eh);
                    }
                }
                outputParameters.AddOutput("results", resultsFrame);
            }
            return new StepResult(StepSuccess.Success, outputParameters);
        }

        public static StepResult RptFollowUpLifetableCalculateNatst(ITemplateHost host, ParameterBag parameters)
        {
            DataFrame deathsFrame = parameters["deaths"].AsDataFrame;
            DoubleVariable deathsVariable = deathsFrame.Variables[0].AsDoubleVariable;

            double natst = 0.0;
            foreach (double d in deathsVariable.Data)
            {
                if (d != Constant.MISSING)
                    natst += d;
            }

            DataFrame withdrawalsFrame = parameters["withdrawals"].AsDataFrame;
            DoubleVariable withdrawalsVariable = withdrawalsFrame.Variables[0].AsDoubleVariable;

            foreach (double w in withdrawalsVariable.Data)
            {
                if (w != Constant.MISSING)
                    natst += w;
            }

            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("natst-min", natst);
            return new StepResult(StepSuccess.Success, outputParameters);
        }

        public static StepResult RptFollowUpLifetable(ITemplateHost host, ParameterBag parameters)
        {
            const string nan = Formatting.ASTERISK;
            double gamma = parameters["gamma"].AsDouble;
            double P = (1.0 - gamma) / 2.0;
            double cit = PDF.gauinv(1.0 - P);

            DataFrame timesFrame = parameters["times"].AsDataFrame;
            DoubleVariable timesVariable = timesFrame.Variables[0].AsDoubleVariable;
            int rows = timesVariable.Length;
            ColumnData[] td = new ColumnData[2 + 1 /* for VB to C# conversion */];
            double[] t = new double[rows + 1 /* for VB to C# conversion */];
            td[0] = new ColumnData { Title = timesVariable.Title };

            for (int r = 1; r <= rows; r++)
                t[r] = timesVariable.Data[r - 1];

            DataFrame deathsFrame = parameters["deaths"].AsDataFrame;
            DoubleVariable deathsVariable = deathsFrame.Variables[0].AsDoubleVariable;
            double[] D = new double[rows + 1 /* for VB to C# conversion */];
            td[1] = new ColumnData { Title = deathsVariable.Title };

            for (int r = 1; r <= rows; r++)
                D[r] = deathsVariable.Data[r - 1];

            DataFrame withdrawalsFrame = parameters["withdrawals"].AsDataFrame;
            DoubleVariable withdrawalsVariable = withdrawalsFrame.Variables[0].AsDoubleVariable;
            double[] w = new double[rows + 1 /* for VB to C# conversion */];
            td[2] = new ColumnData { Title = withdrawalsVariable.Title };

            for (int r = 1; r <= rows; r++)
                w[r] = withdrawalsVariable.Data[r - 1];

            double natst = parameters["natst"].AsDouble;

            Trisvar[] qx = new Trisvar[rows + 1 /* for VB to C# conversion */ ];
            int nx = 0;
            for (int j = 1; j <= rows; j++)
            {
                if (t[j] != Constant.MISSING && D[j] != Constant.MISSING && w[j] != Constant.MISSING)
                {
                    nx++;
                    qx[j] = new Trisvar { TM = Math.Floor(t[j]), gp = Convert.ToInt32(w[j]), cs = Convert.ToInt32(D[j]) };
                }
            }
            Array.Sort(qx, 1, nx, new TrisvarByTm());
            t = new double[nx + 1 /* for VB to C# conversion */];
            D = new double[nx + 1 /* for VB to C# conversion */];
            w = new double[nx + 1 /* for VB to C# conversion */];
            int nt = 0;
            for (int j = 1; j <= nx; j++)
            {
                int k;
                for (k = j + 1; k <= nx; k++)
                {
                    if (qx[k].TM != qx[j].TM || k == nx)
                        break;
                }
                int cnt = k - j;
                nt++;
                t[nt] = qx[j].TM;
                for (k = 1; k <= cnt; k++)
                {
                    w[nt] += qx[j + k - 1].gp;
                    D[nt] += qx[j + k - 1].cs;
                }
                j = j + cnt - 1;
            }
            ParameterBag outputParameters = new ParameterBag();
            double cump = 1.0;
            double var1 = 0.0;
            double natr = natst;
            double[] xcump = new double[nt + 1 /* for VB to C# conversion */];
            double[] xvar = new double[nt + 1 /* for VB to C# conversion */];
            double[] xp = new double[nt + 1 /* for VB to C# conversion */];
            IList<ParameterBag> deathsList = new List<ParameterBag>();
            outputParameters.AddOutput("*deaths", deathsList);
            for (int j = 1; j <= nt; j++)
            {
                double en1 = natr - w[j] / 2.0;
                double Q = D[j] / en1;
                P = 1.0 - Q;
                cump = P * cump;
                if ((P * en1) != 0.0)
                {
                    var1 = var1 + Q / (en1 * P);
                }
                double var = cump * cump * var1;
                xp[j] = P;
                xcump[j] = cump;
                xvar[j] = var;
                ParameterBag deathsParameters = new ParameterBag();
                deathsList.Add(deathsParameters);
                string xx;
                if (j < nt)
                    xx = Convert.ToInt32(t[j]).ToString() + " to " + Convert.ToInt32(t[j + 1]).ToString();
                else
                    xx = Convert.ToInt32(t[j]).ToString() + " up";
                deathsParameters.AddOutput("int", xx);
                deathsParameters.AddOutput("death", D[j].ToString());
                deathsParameters.AddOutput("wdrawn", w[j].ToString());
                deathsParameters.AddOutput("risk", natr.ToString());
                if (j < nt)
                {
                    deathsParameters.AddOutput("nx", en1.ToString());
                    deathsParameters.AddOutput("q", host.RoundU(Q));
                }
                else
                {
                    deathsParameters.AddOutput("nx", nan);
                    deathsParameters.AddOutput("q", nan);
                }
                natr = natr - D[j] - w[j];
            }
            outputParameters.AddOutput("pc", Formatting.XRound(gamma * 100, 1));

            IList<ParameterBag> survivalList = new List<ParameterBag>();
            outputParameters.AddOutput("*survival", survivalList);

            ParameterBag survivalParameters = new ParameterBag();
            survivalList.Add(survivalParameters);
            survivalParameters.AddOutput("int", Convert.ToInt32(t[1]).ToString() + " to " + Convert.ToInt32(t[2]).ToString());
            survivalParameters.AddOutput("p", host.RoundU(xp[1]));
            survivalParameters.AddOutput("lx", host.RoundU(100.0));
            survivalParameters.AddOutput("var", nan);
            survivalParameters.AddOutput("lci", nan);
            survivalParameters.AddOutput("uci", nan);
            for (int j = 1; j <= nt - 1; j++)
            {
                cump = xcump[j];
                double var = xvar[j];
                string sd;
                string lc;
                string uc;
                if (var > 0.0)
                {
                    double s = Math.Sqrt(var);
                    s = s / (-cump * Math.Log(cump));
                    sd = host.RoundU(100.0 * s);
                    lc = host.RoundU(100.0 * Math.Pow(cump, Math.Exp(cit * s)));
                    uc = host.RoundU(100.0 * Math.Pow(cump, Math.Exp(-cit * s)));
                }
                else
                {
                    sd = nan;
                    lc = nan;
                    uc = nan;
                }
                survivalParameters = new ParameterBag();
                survivalList.Add(survivalParameters);
                string xx;
                if (j < nt - 1)
                    xx = Convert.ToInt32(t[j + 1]).ToString() + " to " + Convert.ToInt32(t[j + 2]).ToString();
                else
                    xx = Convert.ToInt32(t[j + 1]).ToString() + " up";
                survivalParameters.AddOutput("int", xx);
                survivalParameters.AddOutput("p", j < nt - 1 ? host.RoundU(xp[j + 1]) : nan);
                survivalParameters.AddOutput("lx", host.RoundU(100.0 * cump));
                survivalParameters.AddOutput("var", sd);
                survivalParameters.AddOutput("lci", lc);
                survivalParameters.AddOutput("uci", uc);
            }
            return new StepResult(StepSuccess.Success, outputParameters);
        }


        public static StepResult RptLogRank(ITemplateHost host, ParameterBag parameters)
        {
            int imfault = 0;
            int strata = 0; int groups = 0;
            int stratum = 0;
            int nt = 0;
            double p2m = 0; double p1m = 0; double p2f = 0; double p1f = 0; double llm = 0; double ulm = 0;
            double llf = 0; double ulf = 0; double hr = 0; double x2t = 0;
            int ne = 0;
            double wt = 0;
            double cit = 0; double GAMMA;
            string gid = null;
            string zx = null;
            ExactBB.Rec2x2[] tbl = null;
            bool ifault;

            double[] score = new double[0 + 1 /* for VB to C# conversion */ ];
            double[] gpid = new double[1 + 1 /* for VB to C# conversion */];
            string[] glab = new string[1 + 1 /* for VB to C# conversion */];
            string[] slab = new string[1 + 1 /* for VB to C# conversion */];
            double[,] ARR2 = null;
            ColumnData[] CDAT1 = null;
            x_petoprep(host, parameters, ref nt, out GAMMA, ref cit, ref groups, ref strata, ref score, ref gid, ref gpid, ref glab, ref slab, out ifault, ref ARR2, ref CDAT1);
            if (ifault)
            {
                throw new TemplateOperationCancelledException();
            }

            int wt_method = int.Parse(parameters["wt_method"].AsString);
            //  RTF_LoadTemplate("logrank.rtf")
            ParameterBag outputParameters = new ParameterBag();
            IList<ParameterBag> outerList = new List<ParameterBag>();
            outputParameters.AddOutput("*outer", outerList);
            double[] tesum = new double[groups + 1 /* for VB to C# conversion */];
            int[] tdg = new int[groups + 1 /* for VB to C# conversion */];
            Trisvar[] Q = new Trisvar[nt + 1 + 1 /* for VB to C# conversion */];
            double[,] vsuml = new double[groups + 1 /* for VB to C# conversion */, groups + 1 /* for VB to C# conversion */];
            double[] u0suml = new double[groups + 1 /* for VB to C# conversion */];
            double[,] vsumw = new double[groups + 1 /* for VB to C# conversion */, groups + 1 /* for VB to C# conversion */];
            double[] u0sumw = new double[groups + 1 /* for VB to C# conversion */];
            do
            {
                // stratum loop
                if (strata != 0)
                {
                    stratum = stratum + 1;
                }
                int[] ng = new int[groups + 1 /* for VB to C# conversion */];
                int[] dg = new int[groups + 1 /* for VB to C# conversion */ ];
                int ntx = 0;
                int j;
                for (j = 1; j <= nt; j++)
                {
                    if ((strata == 0 | ARR2[3, j] == stratum) & (ARR2[1, j] != Constant.MISSING & ARR2[2, j] != Constant.MISSING & ARR2[0, j] != Constant.MISSING))
                    {
                        ntx = ntx + 1;
                        Q[ntx] = new Trisvar
                                     {
                                         TM = ARR2[1, j],
                                         cs = Convert.ToInt32(ARR2[2, j]),
                                         gp = Convert.ToInt32(ARR2[0, j])
                                     };
                        ng[Q[ntx].gp] = ng[Q[ntx].gp] + 1;
                        if (Q[ntx].cs == 1)
                        {
                            dg[Q[ntx].gp] = dg[Q[ntx].gp] + 1;
                        }
                    }
                }
                Array.Sort(Q, 1, ntx, new TrisvarByTm());
                int Test = 0;
                // logrank then Wilcoxon test loop
                do
                {
                    Test = Test + 1;
                    int[] drop = new int[groups + 1 /* for VB to C# conversion */];
                    int[] rg = new int[groups + 1 /* for VB to C# conversion */];
                    int[] dead = new int[groups + 1 /* for VB to C# conversion */];
                    double[,] v = new double[groups + 1 /* for VB to C# conversion */, groups + 1 /* for VB to C# conversion */];
                    double[] u0 = new double[groups + 1 /* for VB to C# conversion */ ];
                    double[] esum = new double[groups + 1 /* for VB to C# conversion */ ];
                    if (groups == 2 & Test == 1)
                    {
                        tbl = new ExactBB.Rec2x2[ntx + 1 /* for VB to C# conversion */];
                    } // exact 2x2 test

                    for (j = 1; j <= groups; j++)
                    {
                        rg[j] = ng[j];
                    }
                    double sv = 1.0;
                    int k;
                    int j2;
                    for (j = 1; j <= ntx; j++)
                    {
                        //  total number at risk = risktot for time J
                        double risktot = 0;
                        for (j2 = 1; j2 <= groups; j2++)
                        {
                            risktot = risktot + rg[j2];
                        }
                        drop[Q[j].gp] = 1;
                        dead[Q[j].gp] = Q[j].cs;
                        int totd = Q[j].cs;
                        //  deaths at time J
                        int N = j;
                        do
                        {
                            if (N >= ntx)
                            {
                                break; /* TRANSWARNING: check that break is in correct scope */
                            }
                            if (Q[N].TM == Q[N + 1].TM)
                            {
                                drop[Q[N + 1].gp] = drop[Q[N + 1].gp] + 1;
                                if (Q[N + 1].cs != 0)
                                {
                                    dead[Q[N + 1].gp] = dead[Q[N + 1].gp] + 1;
                                    totd = totd + 1;
                                }
                                N = N + 1;
                            }
                            else
                            {
                                break; /* TRANSWARNING: check that break is in correct scope */
                            }
                        }
                        while (true);
                        double deadx = Convert.ToDouble(totd);
                        for (j2 = 1; j2 <= groups; j2++)
                        {
                            double jrisk = Convert.ToDouble(rg[j2]);
                            double jprop = jrisk / risktot;
                            double expect = deadx * jprop;
                            esum[j2] = esum[j2] + expect;
                            if (Test == 2)
                            {
                                switch (wt_method)
                                {
                                    case 1:
                                        wt = sv * risktot / (risktot + 1.0);
                                        break;
                                    case 2:
                                        wt = risktot / Convert.ToDouble(ntx + 1);
                                        break;
                                    case 3:
                                        wt = Math.Sqrt(risktot);
                                        break;
                                }

                            }
                            else
                            {
                                wt = 1.0;
                            }
                            // rank statistic sum and estimated covariance matrix
                            u0[j2] = u0[j2] + (wt * (Convert.ToDouble(dead[j2]) - expect));
                            for (k = 1; k <= groups; k++)
                            {
                                double krisk = Convert.ToDouble(rg[k]);
                                if (risktot > 1)
                                {
                                    // double kprop = krisk / risktot; 
                                    if (k == j2)
                                    {
                                        v[j2, k] = v[j2, k] + wt * wt * (jrisk * (risktot - jrisk) * deadx * (risktot - deadx)) / (risktot * risktot * (risktot - 1.0));
                                    }
                                    else
                                    {
                                        v[j2, k] = v[j2, k] + wt * wt * -(jrisk * krisk * deadx * (risktot - deadx)) / (risktot * risktot * (risktot - 1.0));
                                    }
                                }
                            }
                        }
                        // exact test for 2 groups - a table for each unique survival time
                        if (groups == 2 & Test == 1)
                        {
                            if (j == 1)
                            {
                                ne = j;
                            }
                            else
                            {
                                if (Q[j].TM == Q[j - 1].TM)
                                {
                                    // skip = true; 
                                }
                                else
                                {
                                    ne = ne + 1;
                                }
                            }
                            tbl[ne].a = Convert.ToDouble(dead[1]);
                            tbl[ne].m1 = Convert.ToDouble(dead[1]) + Convert.ToDouble(dead[2]);
                            tbl[ne].n1 = Convert.ToDouble(rg[1]);
                            tbl[ne].n0 = Convert.ToDouble(rg[2]);
                            tbl[ne].freq = 1;
                            tbl[ne].informative = (dead[1] * (rg[2] - dead[2]) != 0) | (dead[2] * (rg[1] - dead[1]) != 0);
                        }
                        for (j2 = 1; j2 <= groups; j2++)
                        {
                            rg[j2] = rg[j2] - drop[j2];
                            drop[j2] = 0;
                            dead[j2] = 0;
                        }
                        j = N;
                        // survivor function - needed for Peto-Prentice weights
                        sv = sv * (risktot - Convert.ToDouble(totd) + 1.0) / risktot + 1;
                    }
                    // invert the first groups-1 elements of the v matrix
                    double[,] vtemp = new double[groups - 1 + 1 /* for VB to C# conversion */, 1 + 1 /* for VB to C# conversion */];
                    double[,] vinv = new double[groups + 1 /* for VB to C# conversion */, groups + 1 /* for VB to C# conversion */];
                    // save v for summing later if stratified
                    for (j2 = 1; j2 <= groups; j2++)
                    {
                        for (k = 1; k <= groups; k++)
                        {
                            vinv[j2, k] = v[j2, k];
                        }
                    }
                    // invert the matrix by Gauss-Jordan elimination - fault if singular
                    MathDbl.gaussj(vinv, 1, groups - 1, vtemp, 1, ref imfault);
                    double x2;
                    if (imfault != 0)
                    {
                        x2 = Constant.MISSING;
                    }
                    else
                    {
                        // multiply transpose of U0 by inverse of V
                        for (j2 = 1; j2 <= groups - 1; j2++)
                        {
                            for (k = 1; k <= groups - 1; k++)
                            {
                                vtemp[j2, 1] = vtemp[j2, 1] + vinv[j2, k] * u0[k];
                            }
                        }
                        // multiply tran(U0)*inv(V) by U0 to get the test statistic
                        x2 = 0.0;
                        for (k = 1; k <= groups - 1; k++)
                        {
                            x2 = x2 + vtemp[k, 1] * u0[k];
                        }
                    }
                    //  trend statistic (c'U0)^2 / c'Vc
                    double x2den;
                    double x2num;
                    if (groups > 2)
                    {
                        x2num = 0.0;
                        for (k = 1; k <= groups; k++)
                        {
                            x2num = x2num + u0[k] * score[k];
                        }
                        x2num = x2num * x2num;
                        vtemp = new double[groups + 1 /* for VB to C# conversion */, 1 + 1 /* for VB to C# conversion */];
                        x2den = 0.0;
                        for (j2 = 1; j2 <= groups; j2++)
                        {
                            for (k = 1; k <= groups; k++)
                            {
                                vtemp[j2, 1] = vtemp[j2, 1] + v[j2, k] * score[k];
                            }
                        }
                        for (k = 1; k <= groups; k++)
                        {
                            x2den = x2den + vtemp[k, 1] * score[k];
                        }
                        x2t = x2num / x2den;
                    }
                    if (Test == 1 & groups == 2)
                    {
                        // exact test
                        bool useLogScale = false;
                        int ierr;
                        ExactBB.Exact22k(host, ne, 4, tbl, GAMMA, ref hr, out ulf, out llf, out ulm, out llm, ref p1f, ref p2f, ref p1m, ref p2m, ref useLogScale, out ierr);
                        if (ierr != 0)
                        {
                            hr = Constant.MISSING;
                            ulf = Constant.MISSING;
                            llf = Constant.MISSING;
                            ulm = Constant.MISSING;
                            llm = Constant.MISSING;
                            p1f = Constant.MISSING;
                            p2f = Constant.MISSING;
                            p1m = Constant.MISSING;
                            p2m = Constant.MISSING;
                        }
                    }
                    // Start of data output
                    ParameterBag outerParameters = new ParameterBag();
                    outerList.Add(outerParameters);
                    string testname;
                    if (Test == 1)
                    {
                        testname = "Log-rank (Peto)";
                    }
                    else
                    {
                        switch (wt_method)
                        {
                            case 1:
                                zx = "Peto-Prentice";
                                break;
                            case 2:
                                zx = "Gehan-Breslow";
                                break;
                            case 3:
                                zx = "Tarone-Ware";
                                break;
                        }

                        testname = "Generalised Wilcoxon (" + zx + ")";
                    }
                    outerParameters.AddOutput("title", testname);
                    if (strata != 0)
                    {
                        outerParameters.AddOutput("strata", " * [STRATUM " + stratum.ToString() + " of " + strata.ToString() + ": " + slab[stratum] + "]");
                    }
                    else
                    {
                        outerParameters.AddOutput("strata", "");
                    }
                    double rr;
                    if (Test == 1)
                    {
                        //  more detail with log-rank test
                        IList<ParameterBag> groupsList = new List<ParameterBag>();
                        outerParameters.AddOutput("*groups", groupsList);
                        for (j = 1; j <= groups; j++)
                        {
                            ParameterBag groupsParameters = new ParameterBag();
                            groupsList.Add(groupsParameters);
                            groupsParameters.AddOutput("grp", j.ToString() + " (" + gid + " = " + glab[Convert.ToInt32(gpid[j])] + ")");
                            groupsParameters.AddOutput("obs", dg[j].ToString());
                            groupsParameters.AddOutput("ext", host.RoundU(esum[j]));
                            tesum[j] = tesum[j] + esum[j];
                            tdg[j] = tdg[j] + dg[j];
                            if (esum[j] <= 0)
                            {
                                rr = Constant.MISSING;
                            }
                            else { rr = Convert.ToDouble(dg[j]) / esum[j]; }
                            groupsParameters.AddOutput("rel", host.RoundU(rr));
                        }
                    }
                    else
                    {
                        //  plain matrix and statistic output with Wilcoxon
                        outerParameters.AddOutput("*groups", null);
                    }
                    //  test statistics and variance-covariance matrix
                    string x = "";
                    for (j = 1; j <= groups; j++)
                    {
                        x = x + host.RoundU(u0[j]) + "\t";
                    }
                    outerParameters.AddOutput("rank", x);
                    IList<ParameterBag> covarList = new List<ParameterBag>();
                    outerParameters.AddOutput("*covar", covarList);
                    for (j = 1; j <= groups; j++)
                    {
                        x = "";
                        for (j2 = 1; j2 <= groups; j2++)
                        {
                            x = x + host.RoundU(vinv[j2, j]) + "\t";
                        }
                        ParameterBag covarParameters = new ParameterBag();
                        covarList.Add(covarParameters);
                        covarParameters.AddOutput("mat", x);
                    }
                    //  test this stratum or whole
                    outerParameters.AddOutput("chi", host.RoundU(x2));
                    outerParameters.AddOutput("p",
                                              x2 != Constant.MISSING
                                                  ? host.pval(PDF.chivalp(x2, Convert.ToDouble(groups - 1)))
                                                  : "");
                    if (groups > 2)
                    {
                        IList<ParameterBag> trendsList = new List<ParameterBag>();
                        outerParameters.AddOutput("*trends", trendsList);
                        ParameterBag trendsParameters = new ParameterBag();
                        trendsList.Add(trendsParameters);
                        trendsParameters.AddOutput("trend", host.RoundU(x2t));
                        trendsParameters.AddOutput("p_trend", host.pval(PDF.chivalp(x2t, 1.0)));
                    }
                    else
                    {
                        outerParameters.AddOutput("*trends", null);
                    }
                    if (strata != 0)
                    {
                        // sum score and variance matrices over strata for later combined calcs
                        for (j2 = 1; j2 <= groups; j2++)
                        {
                            if (Test == 1)
                            {
                                u0suml[j2] = u0suml[j2] + u0[j2];
                            }
                            else
                            {
                                u0sumw[j2] = u0sumw[j2] + u0[j2];
                            }
                            for (k = 1; k <= groups; k++)
                            {
                                if (Test == 1)
                                {
                                    vsuml[j2, k] = vsuml[j2, k] + v[j2, k];
                                }
                                else
                                {
                                    vsumw[j2, k] = vsumw[j2, k] + v[j2, k];
                                }
                            }
                        }
                    }
                    if (stratum == strata & strata != 0)
                    {
                        // combined (deaths, extent of exposure to risk of death, relative rate):
                        IList<ParameterBag> strataList = new List<ParameterBag>();
                        outerParameters.AddOutput("*strata", strataList);
                        ParameterBag strataParameters = new ParameterBag();
                        strataList.Add(strataParameters);
                        strataParameters.AddOutput("test", testname);
                        IList<ParameterBag> stratumList = new List<ParameterBag>();
                        strataParameters.AddOutput("*stratum", stratumList);
                        int j3;
                        for (j3 = 1; j3 <= groups; j3++)
                        {
                            ParameterBag stratumParameters = new ParameterBag();
                            stratumList.Add(stratumParameters);
                            stratumParameters.AddOutput("grp", j3.ToString());
                            stratumParameters.AddOutput("res", tdg[j3].ToString());
                            stratumParameters.AddOutput("sum", host.RoundU(tesum[j3]));
                            stratumParameters.AddOutput("tot", host.RoundU(Convert.ToDouble(tdg[j3]) / tesum[j3]));
                        }
                        // get U0'inv(V)U0 from combined matrices
                        if (Test == 1)
                        {
                            // stratified logrank
                            vtemp = new double[groups - 1 + 1 /* for VB to C# conversion */, 1 + 1 /* for VB to C# conversion */];
                            MathDbl.gaussj(vsuml, 1, groups - 1, vtemp, 1, ref imfault);
                            if (imfault != 0)
                            {
                                x2 = Constant.MISSING;
                            }
                            else
                            {
                                for (j2 = 1; j2 <= groups - 1; j2++)
                                {
                                    for (k = 1; k <= groups - 1; k++)
                                    {
                                        vtemp[j2, 1] = vtemp[j2, 1] + vsuml[j2, k] * u0suml[k];
                                    }
                                }
                                x2 = 0.0;
                                for (k = 1; k <= groups - 1; k++)
                                {
                                    x2 = x2 + vtemp[k, 1] * u0suml[k];
                                }
                            }
                        }
                        else
                        {
                            // stratified Wilcoxon
                            vtemp = new double[groups - 1 + 1 /* for VB to C# conversion */, 1 + 1 /* for VB to C# conversion */];
                            MathDbl.gaussj(vsumw, 1, groups - 1, vtemp, 1, ref imfault);
                            if (imfault != 0)
                            {
                                x2 = Constant.MISSING;
                            }
                            else
                            {
                                for (j2 = 1; j2 <= groups - 1; j2++)
                                {
                                    for (k = 1; k <= groups - 1; k++)
                                    {
                                        vtemp[j2, 1] = vtemp[j2, 1] + vsumw[j2, k] * u0sumw[k];
                                    }
                                }
                                x2 = 0.0;
                                for (k = 1; k <= groups - 1; k++)
                                {
                                    x2 = x2 + vtemp[k, 1] * u0sumw[k];
                                }
                            }
                        }
                        strataParameters.AddOutput("chi_strata", host.RoundU(x2));
                        strataParameters.AddOutput("p_strata", host.pval(PDF.chivalp(x2, Convert.ToDouble(groups - 1))));
                        if (groups > 2)
                        {
                            // trend statistic (c'U0)^2 / c'Vc
                            x2num = 0.0;
                            for (k = 1; k <= groups; k++)
                            {
                                if (Test == 1)
                                {
                                    x2num = x2num + u0suml[k] * score[k];
                                }
                                else
                                {
                                    x2num = x2num + u0sumw[k] * score[k];
                                }
                            }
                            x2num = x2num * x2num;
                            vtemp = new double[groups + 1 /* for VB to C# conversion */, 1 + 1 /* for VB to C# conversion */];
                            x2den = 0.0;
                            for (j2 = 1; j2 <= groups; j2++)
                            {
                                for (k = 1; k <= groups; k++)
                                {
                                    if (Test == 1)
                                    {
                                        vtemp[j2, 1] = vtemp[j2, 1] + vsuml[j2, k] * score[k];
                                    }
                                    else
                                    {
                                        vtemp[j2, 1] = vtemp[j2, 1] + vsumw[j2, k] * score[k];
                                    }
                                }
                            }
                            for (k = 1; k <= groups; k++)
                            {
                                x2den = x2den + vtemp[k, 1] * score[k];
                            }
                            x2t = x2num / x2den;
                            IList<ParameterBag> strataTrendList = new List<ParameterBag>();
                            strataParameters.AddOutput("*strata_trend", strataTrendList);
                            ParameterBag strataTrendParameters = new ParameterBag();
                            strataTrendList.Add(strataTrendParameters);
                            strataTrendParameters.AddOutput("strata_trend", host.RoundU(x2t));
                            strataTrendParameters.AddOutput("p_strata_trend", host.pval(PDF.chivalp(x2t, 1.0)));
                        }
                        else
                        {
                            strataParameters.AddOutput("*strata_trend", null);
                        }
                    }
                    else
                    {
                        outerParameters.AddOutput("*strata", null);
                    }
                    if (Test == 1 & (stratum == strata | strata == 0))
                    {
                        // Hazard Ratio" + " & approximate "
                        IList<ParameterBag> hazardsList = new List<ParameterBag>();
                        outerParameters.AddOutput("*hazards", hazardsList);
                        ParameterBag hazardsParameters = new ParameterBag();
                        hazardsList.Add(hazardsParameters);
                        hazardsParameters.AddOutput("pc", Formatting.XRound(GAMMA * 100, 2));

                        IList<ParameterBag> hazardList = new List<ParameterBag>();
                        hazardsParameters.AddOutput("*hazard", hazardList);
                        for (j = 1; j <= groups - 1; j++)
                        {
                            for (k = j + 1; k <= groups; k++)
                            {
                                if (tesum[j] <= 0)
                                {
                                    rr = Constant.MISSING;
                                }
                                else { rr = Convert.ToDouble(tdg[j]) / tesum[j]; }
                                double rk;
                                if (tesum[k] <= 0)
                                {
                                    rk = Constant.MISSING;
                                }
                                else { rk = Convert.ToDouble(tdg[k]) / tesum[k]; }
                                double ru;
                                double rl;
                                if (rr != Constant.MISSING & rk != 0)
                                {
                                    rr = rr / rk;
                                    rl = Math.Exp(Math.Log(rr) - cit * Math.Sqrt(1.0 / tesum[j] + 1.0 / tesum[k]));
                                    ru = Math.Exp(Math.Log(rr) + cit * Math.Sqrt(1.0 / tesum[j] + 1.0 / tesum[k]));
                                }
                                else
                                {
                                    rr = Constant.MISSING;
                                    ru = Constant.MISSING;
                                    rl = Constant.MISSING;
                                }
                                ParameterBag hazardParameters = new ParameterBag();
                                hazardList.Add(hazardParameters);
                                hazardParameters.AddOutput("vs", "Group " + j.ToString() + " vs. Group " + k.ToString());
                                hazardParameters.AddOutput("haz", host.RoundU(rr));
                                hazardParameters.AddOutput("from", host.RoundU(rl));
                                hazardParameters.AddOutput("to", host.RoundU(ru));
                            }
                        }
                        if (groups == 2 & strata == 0)
                        {
                            IList<ParameterBag> cmlList = new List<ParameterBag>();
                            hazardsParameters.AddOutput("*cml", cmlList);
                            ParameterBag cmlParameters = new ParameterBag();
                            cmlList.Add(cmlParameters);
                            // exact Hazard Ratio
                            cmlParameters.AddOutput("hr", host.RoundU(hr));
                            cmlParameters.AddOutput("pc", Formatting.XRound(GAMMA * 100.0, 2));
                            cmlParameters.AddOutput("llf", host.RoundU(llf));
                            cmlParameters.AddOutput("ulf", host.RoundU(ulf));
                            cmlParameters.AddOutput("p1f", host.pval(p1f));
                            cmlParameters.AddOutput("p2f", host.pval(p2f));
                            cmlParameters.AddOutput("llm", host.RoundU(llm));
                            cmlParameters.AddOutput("ulm", host.RoundU(ulm));
                            cmlParameters.AddOutput("p1m", host.pval(p1m));
                            cmlParameters.AddOutput("p2m", host.pval(p2m));
                        }
                        else
                        {
                            hazardsParameters.AddOutput("*cml", null);
                        }
                    }
                    else
                    {
                        outerParameters.AddOutput("*hazards", null);
                    }
                }
                while (!(Test >= 2));
            }
            while (stratum != strata);
            return new StepResult(StepSuccess.Success, outputParameters);
        }
    }
}
