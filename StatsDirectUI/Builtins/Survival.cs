using System;
using System.Collections.Generic;
using StatsDirect.Charting;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.Templates;
using StatsDirect.Utilities;
using static StatsDirect.Builtins.ExactBB;

namespace StatsDirect.Builtins
{
    public static class Survival
    {
        /// <summary>
        /// A record as it is sorted: its time (Tm), the number of its group (Gp) and its code (Cs: 1 dead, 0 censored).
        /// </summary>
        private class Trisvar
        {
            public double Tm { get; set; }
            public int Gp { get; set; }
            public int Cs { get; set; }
        }

        /// <summary>
        /// The order of records by time and, at the same time, by group, the group with the greater number first: the records of a
        /// group at a time then stand together.
        /// </summary>
        private class TrisvarByTmThenGp : IComparer<Trisvar>
        {
            private static int Compare(Trisvar x, Trisvar y)
            {
                //  First check TM
                if (x.Tm > y.Tm)
                    return 1;
                if (x.Tm < y.Tm)
                    return -1;

                //  Next check gp
                if (x.Gp > y.Gp)
                    return -1;
                if (x.Gp < y.Gp)
                    return 1;

                //  If we get here, there are no meaningful differences
                return 0;
            }

            // interface methods implemented by Compare
            int IComparer<Trisvar>.Compare(Trisvar x, Trisvar y) => Compare(x, y);
        }


        /// <summary>
        /// The order of records by time alone.
        /// </summary>
        private class TrisvarByTm : IComparer<Trisvar>
        {
            private static int Compare(Trisvar x, Trisvar y)
            {
                //  First check TM
                if (x.Tm > y.Tm)
                    return 1;
                if (x.Tm < y.Tm)
                    return -1;

                //  If we get here, there are no meaningful differences
                return 0;
            }

            // interface methods implemented by Compare
            int IComparer<Trisvar>.Compare(Trisvar x, Trisvar y) => Compare(x, y);

        }

        /// <summary>
        /// Kaplan-Meier (product-limit) estimates of survival, for one group of subjects or for each of several.  A record has a time, a
        /// code (0 censored, 1 dead; a code above 1 is that number of deaths at the time) and, if there are groups, a group.  A record
        /// with a blank cell is left out.  For each group the report has the table of the times (made by Plprep and Plest), the median
        /// survival time (see TimeOfHalf) with two confidence intervals, and the mean survival time with its confidence interval.  The
        /// estimates are passed on for the plots, and can be saved to the worksheet with a row for each subject (Plsave).
        /// </summary>
        /// <param name="host">The preferences for the display of numbers.</param>
        /// <param name="parameters">"times", "deaths" (the codes) and, if there are groups, "groups"; "gamma", the confidence level;
        /// "save", whether the estimates are to be saved.</param>
        public static StepOutput RptKaplan(IPreferences host, ParameterBag parameters)
        {
            double gamma = parameters["gamma"].AsDouble;
            if (gamma <= 0.0)
                gamma = 0.95;

            double cit = PDF.gauinv(1.0 - (1.0 - gamma) / 2.0);

            // Store the times data
            DataFrame timesFrame = parameters["times"].AsDataFrame;
            DoubleVariable timesVariable = timesFrame.Variables[0]as DoubleVariable;
            int rows = timesVariable.Length;
            ColumnData[] cd = new ColumnData[2 + 1 ];
            cd[1] = new ColumnData { Title = timesVariable.Title };
            double[] t = new double[rows + 1];
            for (int r = 1; r <= rows; r++)
                t[r] = timesVariable.Data[r - 1];

            // Store the death/event data
            DataFrame deathsFrame = parameters["deaths"].AsDataFrame;
            DoubleVariable deathsVariable = deathsFrame.Variables[0]as DoubleVariable;
            cd[2] = new ColumnData { Title = deathsVariable.Title };
            double[] d = new double[rows + 1 ];
            for (int r = 1; r <= rows; r++)
                d[r] = deathsVariable.Data[r - 1];

            // Store the group data: the value of a record is that of its group in the list of groups
            string gid;
            ClassifierVariable groupsVariable = null;
            double[] g = new double[rows + 1 ];
            if (parameters.ContainsKey("groups") && parameters["groups"] != null)
            {
                DataFrame groupsFrame = parameters["groups"].AsDataFrame;
                groupsVariable = groupsFrame.Variables[0] as ClassifierVariable;
                gid = groupsVariable.Title;
                cd[0] = new ColumnData { Title = groupsVariable.Title };
                for (int r = 1; r <= rows; r++)
                    g[r] = groupsVariable.Data[r - 1];
            }
            else
            {
                //  one group
                cd[0] = new ColumnData { Rows = rows };
                gid = string.Empty;
            }

            // A record with a blank time, death/event code or group is left out
            int complete = 0;
            for (int r = 1; r <= rows; r++)
            {
                if (t[r] != Constant.MISSING && d[r] != Constant.MISSING && g[r] != Constant.MISSING)
                {
                    complete++;
                    t[complete] = t[r];
                    d[complete] = d[r];
                    g[complete] = g[r];
                }
            }
            int leftOut = rows - complete;
            rows = complete;
            if (rows == 0)
                throw new TemplateOperationCancelledException("There are no records with a time and a death/event code" + (gid.Length > 0 ? " and a group." : "."), "Kaplan-Meier");

            // The groups are numbered from 1 in the order in which they are first met, and each keeps the label of its own group
            double[] gpid = new double[rows + 1];
            int groups = 0;
            for (int r = 1; r <= rows; r++)
            {
                int number = 0;
                for (int j = 1; j <= groups; j++)
                {
                    if (g[r] == gpid[j])
                    {
                        number = j;
                        break;
                    }
                }
                if (number == 0)
                {
                    groups++;
                    gpid[groups] = g[r];
                    number = groups;
                }
                g[r] = number;
            }
            string[] groupLabels = new string[groups + 1];
            if (groupsVariable != null)
            {
                for (int j = 1; j <= groups; j++)
                {
                    foreach (Group group in groupsVariable.Groups)
                    {
                        if (group.Id == gpid[j])
                        {
                            groupLabels[j] = group.Label;
                            break;
                        }
                    }
                    groupLabels[j] ??= groupsVariable.Groups[Convert.ToInt32(gpid[j])].Label;
                }
            }

            // a code above 1 is that number of deaths, each of which is given a record of its own
            int extra = 0;
            for (int r = 1; r <= rows; r++)
                if (d[r] > 1)
                    extra += Convert.ToInt32(d[r]) - 1;

            // Put the data back into the Public array
            // arr2 has a column for each record: row 0 the number of its group, row 1 its time and row 2 its code, which is now 0 or 1
            double[,] arr2 = new double[2 + 1, rows + extra + 1];
            ColumnData[] cdat1 = new ColumnData[2 + 1 ];
            for (int c = 0; c <= 2; c++)
            {
                cdat1[c] = cd[c];
                cdat1[c].Rows = rows + extra;
            }
            int ctr = 0;
            for (int r = 1; r <= rows; r++)
            {
                if (d[r] > 1 && d[r] != Constant.MISSING)
                {
                    for (int j = 1; j <= Convert.ToInt32(d[r]); j++)
                    {
                        ctr++;
                        arr2[0, ctr] = g[r];
                        arr2[1, ctr] = t[r];
                        arr2[2, ctr] = 1;
                    }
                }
                else
                {
                    if (d[r] < 0)
                        d[r] = 0;
                    ctr++;
                    arr2[0, ctr] = g[r];
                    arr2[1, ctr] = t[r];
                    arr2[2, ctr] = d[r];
                }
            }

            int nt = cdat1[0].Rows;
            int[] gnx = new int[groups + 1];
            int nmax = 0;
            for (int lap = 1; lap <= groups; lap++)
            {
                int j2 = 0;
                for (int j = 1; j <= nt; j++)
                    if (arr2[0, j] != Constant.MISSING && arr2[0, j] == lap)
                        j2++;
                gnx[lap] = j2;
                if (j2 > nmax)
                    nmax = j2;
            }

            double[,] stime = new double[nmax + 1, groups + 1];
            int[,] dead = new int[nmax + 1, groups + 1];
            double[,] h = new double[nmax + 1, groups + 1];
            double[,] s = new double[nmax + 1, groups + 1];
            int[] cnx = new int[groups + 1];

            bool save = parameters["save"].AsBoolean;

            ParameterBag outputParameters = new();
            IList<ParameterBag> groupList = new List<ParameterBag>();
            outputParameters.AddOutput("*group", groupList);
            // the report says how many records were left out for a blank cell
            if (leftOut > 0)
            {
                IList<ParameterBag> noteList = new List<ParameterBag>();
                ParameterBag noteParameters = new();
                noteParameters.AddOutput("note", leftOut == 1 ? "1 record with a blank cell was left out" : leftOut.ToString() + " records with a blank cell were left out");
                noteList.Add(noteParameters);
                outputParameters.AddOutput("*note", noteList);
            }
            else
            {
                outputParameters.AddOutput("*note", null);
            }
            DataFrame resultsFrame = new();
            for (int lap = 1; lap <= groups; lap++)
            {
                ParameterBag groupParameters = new();
                groupList.Add(groupParameters);

                int[] nat = new int[gnx[lap] + 2];
                int[] cen = new int[gnx[lap] + 1 ];
                int[] allcens = new int[gnx[lap] + 1 ];
                double[] alltime = new double[gnx[lap] + 2 ];
                Plprep(arr2, cdat1, stime, dead, nat, cen, gnx, out int nx, lap, out nt, allcens, alltime);

                if (gid.Length == 0)
                {
                    groupParameters.AddOutput("*grp", null);
                }
                else
                {
                    IList<ParameterBag> grpList = new List<ParameterBag>();
                    groupParameters.AddOutput("*grp", grpList);
                    ParameterBag grpParameters = new();
                    grpList.Add(grpParameters);
                    grpParameters.AddOutput("grp", gid + " = " + groupLabels[lap]);
                }
                cnx[lap] = nx;
                double[] vh = new double[nx + 1];
                double[] vs = new double[nx + 1];
                Plest(groupParameters, stime, nat, dead, cen, h, s, vh, vs, nx, lap);
                //  median survival time
                //  Hosmer & Lemeshow
                //  Andersen PK et al.. Statistical models based on counting processes. New York: Springer-Verlag 1993.
                //  The median is the first time at which S is a half or less (imed); where S is exactly a half from that time to the
                //  next time of death, it is the middle of the two times.  Its variance, for the first of the two intervals, is the
                //  variance of S at the median over the square of the slope of the survival curve there; the slope is taken between
                //  the last time at which S is a half plus a margin or more (iup) and the first time at which it is a half less the
                //  margin or less (ilp).  The margin (area) is 0.05 at every confidence level: it sets how much of the curve the slope
                //  is taken over, and the confidence level has no part in that.
                const int biglong = 999999;
                int imed = biglong;
                int ilp = biglong;
                int iup = 0;
                const double area = 0.05;
                const double p = 0.5;
                // S is a product of fractions: where it should be exactly a half, rounding can leave it a little to either side
                const double fuzz = HalfTolerance;
                int i;
                for (i = 1; i <= cnx[lap]; i++)
                {
                    if (s[i, lap] < p + fuzz && i < imed)
                        imed = i;
                    if (s[i, lap] <= p - area + fuzz && i < ilp)
                        ilp = i;
                    if (s[i, lap] >= p + area - fuzz && i > iup)
                        iup = i;
                }
                if (imed == biglong)
                    imed = 0;
                if (ilp == biglong)
                    ilp = 0;

                // the times of the group, S, and the confidence limits of S: S less and plus the normal deviate times its standard error
                double[] times = new double[cnx[lap] + 1];
                double[] curve = new double[cnx[lap] + 1];
                double[] lowerLimit = new double[cnx[lap] + 1];
                double[] upperLimit = new double[cnx[lap] + 1];
                for (i = 1; i <= cnx[lap]; i++)
                {
                    times[i] = stime[i, lap];
                    curve[i] = s[i, lap];
                    lowerLimit[i] = vs[i] == Constant.MISSING ? Constant.MISSING : s[i, lap] - cit * Math.Sqrt(vs[i]);
                    upperLimit[i] = vs[i] == Constant.MISSING ? Constant.MISSING : s[i, lap] + cit * Math.Sqrt(vs[i]);
                }
                double median = TimeOfHalf(curve, times, cnx[lap]);

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
                        ll = median - cit * Math.Sqrt(vartp);
                        ul = median + cit * Math.Sqrt(vartp);
                    }
                    else
                    {
                        ll = Constant.MISSING;
                        ul = Constant.MISSING;
                    }
                }
                groupParameters.AddOutput("med", imed != 0 ? host.RoundU(median) : "can not estimate");
                groupParameters.AddOutput("pc", gamma * 100);
                groupParameters.AddOutput("all", ll);
                groupParameters.AddOutput("aul", ul);
                //  Hosmer & Lemeshow
                //  Brookmeyer R, Crowley JJ. A confidence interval for the median survival time. Biometrics 1982;38:29-41.
                //  The limits of the median are the times at which the confidence limits of S come down to a half: the lower, the first
                //  time at which the lower limit of S is a half or less; the upper, the first time at which the upper limit of S is a
                //  half or less.  Between them are the times at which S differs from a half by no more than the normal deviate times
                //  its standard error, and the interval runs up to the time of death next after the last of those.  The upper limit of
                //  S has no value once S is 0, and so is not a half or less there.  A limit that is not reached is printed as infinite.
                double bll = TimeOfHalf(lowerLimit, times, cnx[lap]);
                double bul = TimeOfHalf(upperLimit, times, cnx[lap]);
                bool reached = imed != 0 || bll != Constant.MISSING;
                groupParameters.AddOutput("bll", bll != Constant.MISSING ? bll : reached ? double.NegativeInfinity : Constant.MISSING);
                groupParameters.AddOutput("bul", bul != Constant.MISSING ? bul : reached ? double.PositiveInfinity : Constant.MISSING);
                //  mean survival time
                //  Hosmer & Lemeshow
                //  Andersen PK et al.. Statistical models based on counting processes. New York: Springer-Verlag 1993.
                //  get largest observed event time tk and survival Stk, and largest time TL
                int last = cnx[lap];
                double stk = 0;
                double tk = 0;
                int lastk = 0;
                // a row holds every observation at its time, so the last death may share its row with censored observations
                for (i = last; i >= 1; i--)
                {
                    if (dead[i, lap] > 0)
                    {
                        tk = stime[i, lap];
                        stk = s[i, lap];
                        lastk = i;
                        break;
                    }
                }
                double tl = stime[last, lap];
                // get mean survival time mu: the area under the curve to the largest time, which with no deaths is that time
                double mu;
                if (lastk > 0)
                {
                    mu = 1.0 * stime[1, lap];
                    for (i = 1; i < lastk; i++)
                        mu += s[i, lap] * (stime[i + 1, lap] - stime[i, lap]);
                    if (tk != tl)
                        mu += stk * (tl - tk);
                }
                else
                {
                    mu = tl;
                }
                //  get variance of mu
                //  the sum, over the times with deaths, of the square of the area under the curve beyond the time, times
                //  d / (n (n - d)), d being the deaths at the time and n the number at risk; and then times D / (D - 1), D being all
                //  the deaths
                double vmu = 0;
                double totdead = 0;
                for (i = 1; i <= lastk; i++)
                {
                    double asq = 0;
                    int l;
                    for (l = i; l < lastk; l++)
                        asq += s[l, lap] * (stime[l + 1, lap] - stime[l, lap]);
                    asq += stk * (tl - tk);
                    asq *= asq;
                    double denom = nat[i] * (nat[i] - dead[i, lap]);
                    if (denom != 0.0)
                        vmu += asq * dead[i, lap] / denom;
                    totdead += dead[i, lap];
                }
                if (lastk == 0)
                    groupParameters.AddOutput("lim", "[limit: " + host.RoundU(tl) + ", no deaths] ");
                else if (tl != tk)
                    groupParameters.AddOutput("lim", "[limit: " + host.RoundU(tl) + " on " + host.RoundU(tk) + "] ");
                else
                    groupParameters.AddOutput("lim", string.Empty);
                groupParameters.AddOutput("mu", mu);
                if (totdead > 1 && vmu > 0)
                {
                    // Hosmer & Lemeshow always multiply by totdead / (totdead - 1#)
                    // SPSS does only if tk is part cens:- emailed Hosmer to check 16/4/00
                    vmu = vmu * totdead / (totdead - 1.0);
                    groupParameters.AddOutput("ll", mu - Math.Sqrt(vmu) * cit);
                    groupParameters.AddOutput("ul", mu + Math.Sqrt(vmu) * cit);
                }
                else
                {
                    groupParameters.AddOutput("ll", Constant.MISSING);
                    groupParameters.AddOutput("ul", Constant.MISSING);
                }
                if (save)
                    Plsave(resultsFrame, stime, nat, dead, s, h, vs, vh, nx, lap, gamma, allcens, alltime);
            }

            outputParameters.AddInput("ARR2", arr2);
            outputParameters.AddInput("CDAT1", cdat1);
            outputParameters.AddInput("h", h);
            outputParameters.AddInput("s", s);
            outputParameters.AddInput("stime", stime);
            outputParameters.AddInput("dead", dead);
            outputParameters.AddInput("ngroups", groups);
            outputParameters.AddInput("cnx", cnx);
            outputParameters.AddInput("glab", groupLabels);
            if (save)
                outputParameters.AddOutput("results", resultsFrame);

            return new StepOutput(outputParameters);
        }

        /// <summary>
        /// How near a half a proportion must be to be taken as a half: the square root of 2 to the power -52, the least by which a
        /// number can differ from 1.
        /// </summary>
        private const double HalfTolerance = 1.4901161193847656E-08;

        /// <summary>
        /// The first time at which a curve that comes down with time is a half or less.  If the curve is exactly a half at that time
        /// and is lower at a later time, the result is the middle of the two times: the curve is a half all the way between them.
        /// </summary>
        /// <param name="curve">The height of the curve at each time, from element 1; a height without a value is passed over.</param>
        /// <param name="times">The times, in order, from element 1.</param>
        /// <param name="n">The number of times.</param>
        /// <returns>The time, or the missing value if the curve is never a half or less.</returns>
        private static double TimeOfHalf(double[] curve, double[] times, int n)
        {
            int first = 0;
            for (int i = 1; i <= n && first == 0; i++)
                if (curve[i] != Constant.MISSING && curve[i] < 0.5 + HalfTolerance)
                    first = i;
            if (first == 0)
                return Constant.MISSING;
            if (Math.Abs(curve[first] - 0.5) < HalfTolerance)
            {
                for (int i = first + 1; i <= n; i++)
                    if (curve[i] != Constant.MISSING && curve[i] < curve[first])
                        return (times[first] + times[i]) / 2.0;
            }
            return times[first];
        }

        /// <summary>
        /// The plots that follow the Kaplan-Meier estimates, made by x_plgraph from what RptKaplan passed on.
        /// </summary>
        /// <param name="parameters">What RptKaplan passed on, with "use-markers" and "use-tics" for the look of the plots.</param>
        public static StepOutput RptKaplanMeierPlots(ParameterBag parameters)
        {
            int[] cnx = (int[])parameters["cnx"].AsObject;
            int[,] dead = (int[,])parameters["dead"].AsObject;
            string[] glab = (string[])parameters["glab"].AsObject;
            int groups = parameters["ngroups"].AsInt32;
            double[,] h = (double[,])parameters["h"].AsObject;
            double[,] s = (double[,])parameters["s"].AsObject;
            double[,] stime = (double[,])parameters["stime"].AsObject;
            bool useMarkers = parameters["use-markers"].AsBoolean;
            bool useTics = parameters["use-tics"].AsBoolean;
            ParameterBag outputParameters = new();
            IList<ParameterBag> chartList = new List<ParameterBag>();
            outputParameters.AddOutput("*chart", chartList);
            IList<IRenderable> imageList = x_plgraph(h, s, stime, dead, groups, cnx, glab, useTics, useMarkers);
            foreach (IRenderable renderable in imageList)
            {
                ParameterBag chartParameters = new();
                chartList.Add(chartParameters);
                chartParameters.AddOutput("chart", renderable);
            }
            return new StepOutput(outputParameters);
        }

        /// <summary>
        /// Five plots of the estimates of each group: S against time; H against time; log H against log time, which is a straight line
        /// if the times have a Weibull distribution; the normal deviate of S against log time, a straight line if they have a lognormal
        /// distribution; and H over time against time.  A point is left out of a plot if it has no place there: a time of zero where
        /// the logarithm of the time is wanted, or an H without a value.
        /// </summary>
        /// <param name="h">H: h[i, k] at time i of group k, each index from 1.</param>
        /// <param name="s">S, in the same way.</param>
        /// <param name="stime">The times.</param>
        /// <param name="dead">The deaths at each time.</param>
        /// <param name="groups">The number of groups.</param>
        /// <param name="cnx">The number of times of each group.</param>
        /// <param name="glab">The label of each group.</param>
        /// <param name="tic">Whether the censored times are marked.</param>
        /// <param name="marker">Whether the points are marked.</param>
        public static IList<IRenderable> x_plgraph(double[,] h, double[,] s, double[,] stime, int[,] dead, int groups, int[] cnx, string[] glab, bool tic, bool marker)
        {
            IList<IRenderable> outputImages = new List<IRenderable>();
            int gx = stime.GetUpperBound(0);
            foreach (KaplanMeierPlotMode plotMode in new[] { KaplanMeierPlotMode.Survival, KaplanMeierPlotMode.Hazard, KaplanMeierPlotMode.LogHazard, KaplanMeierPlotMode.LognormalSurvival, KaplanMeierPlotMode.HazardRate })
            {
                string xAxisTitle;
                string yAxisTitle;
                string title;
                // Reallocate x and y each time; appears less efficient, but we need to keep copies of the values as the charts are rendered quite a while after this is called.
                double[,] x = new double[gx + 1, groups + 1];
                double[,] y = new double[gx + 1, groups + 1];
                int[] outCnx = new int[cnx.Length];
                switch (plotMode)
                {
                    case KaplanMeierPlotMode.Survival:
                        xAxisTitle = "Times";
                        yAxisTitle = "Survivor";
                        title = "Survival Plot (PL estimates)";
                        break;
                    case KaplanMeierPlotMode.Hazard:
                        xAxisTitle = "Times";
                        yAxisTitle = "Hazard";
                        title = "Hazard Plot";
                        break;
                    case KaplanMeierPlotMode.LogHazard:
                        xAxisTitle = "Log Times";
                        yAxisTitle = "Log Hazard";
                        title = "Log Hazard Plot";
                        break;
                    case KaplanMeierPlotMode.LognormalSurvival:
                        xAxisTitle = "Log Times";
                        yAxisTitle = "Z (Survivor)";
                        title = "Lognormal Survival Plot";
                        break;
                    case KaplanMeierPlotMode.HazardRate:
                        xAxisTitle = "Times";
                        yAxisTitle = "Hazard / Time";
                        title = "Hazard Rate Plot";
                        break;
                    default:
                        throw new Exception("Unexpected j3");
                }

                for (int k = 1; k <= groups; k++)
                {
                    int nx = 0;
                    for (int j = 1; j <= cnx[k]; j++)
                    {
                        switch (plotMode)
                        {
                            case KaplanMeierPlotMode.Survival:
                                nx++;
                                x[nx, k] = stime[j, k];
                                y[nx, k] = s[j, k];
                                break;
                            case KaplanMeierPlotMode.Hazard:
                                if (h[j, k] != Constant.MISSING)
                                {
                                    nx++;
                                    x[nx, k] = stime[j, k];
                                    y[nx, k] = h[j, k];
                                }
                                break;
                            case KaplanMeierPlotMode.LogHazard:
                                if (h[j, k] != Constant.MISSING && stime[j, k] > 0 && h[j, k] > 0)
                                {
                                    nx++;
                                    x[nx, k] = Math.Log(stime[j, k]);
                                    y[nx, k] = Math.Log(h[j, k]);
                                }
                                break;
                            case KaplanMeierPlotMode.LognormalSurvival:
                                double q = PDF.gauinv(s[j, k], out int fault);
                                if (fault == 0 && stime[j, k] > 0)
                                {
                                    nx++;
                                    x[nx, k] = Math.Log(stime[j, k]);
                                    y[nx, k] = q;
                                }
                                break;
                            case KaplanMeierPlotMode.HazardRate:
                                if (h[j, k] != Constant.MISSING && stime[j, k] != 0)
                                {
                                    nx++;
                                    x[nx, k] = stime[j, k];
                                    y[nx, k] = h[j, k] / stime[j, k];
                                }
                                break;
                        }
                    }
                    outCnx[k] = nx;
                }
                outputImages.Add(ChartRendererFactory.PrepForLater(ChartType.KaplanMeier, new KaplanMeierOptions(dead, groups, outCnx, glab, tic, marker, x, y, plotMode, xAxisTitle, yAxisTitle, title)));
            }
            return outputImages;
        }

        /// <summary>
        /// The abridged life table.  For each interval of age but the last: the death rate M is the deaths over the population; the
        /// probability that somebody alive at the start of the interval dies in it is q = n M / (1 + (1 - a) n M), n being the length
        /// of the interval and a the fraction of it that those who die in it live through; of the l alive at its start (100,000 at
        /// birth) d = l q die in it; and the years lived in it are L = n (l - d) + a n d.  The last interval is open: all who enter
        /// it die in it, and the years lived in it are l / M.  T is the years lived in the interval and all those after it, and the
        /// expectation of life at the start of the interval is e = T / l.
        /// </summary>
        /// <param name="rows">The number of intervals, the open one among them.  Every list is from element 1.</param>
        /// <param name="d">The deaths observed in each interval.</param>
        /// <param name="p">The population of each interval.</param>
        /// <param name="a">The fraction of each interval that those who die in it live through.</param>
        /// <param name="sl">number living at age x = 'l'</param>
        /// <param name="rm">death rate of interval</param>
        /// <param name="r">range or interval length = 'n'</param>
        /// <param name="q">probability of dying in interval</param>
        /// <param name="dd">number dying in interval</param>
        /// <param name="yl">number of years lived in interval</param>
        /// <param name="t">number of years lived beyond age x</param>
        /// <param name="e">observed expectation of life at age x</param>
        private static void AbridgedLifetableBasics(int rows, double[] d, double[] p, double[] a, double[] sl, double[] rm, double[] r, double[] q, double[] dd, double[] yl, double[] t, double[] e)
        {
            sl[1] = 100000;
            for (int i = 1; i <= rows; i++)
            {
                rm[i] = d[i] / p[i];
                if (i == rows)
                {
                    dd[i] = sl[i];
                    q[i] = 1.0;
                    yl[i] = sl[i] / rm[i];
                    t[i] = yl[i];
                    e[i] = sl[i] == 0.0
                        ? Constant.MISSING
                        : t[i] / sl[i];
                }
                else
                {
                    q[i] = r[i] * rm[i] / (1.0 + (1.0 - a[i]) * r[i] * rm[i]);
                    dd[i] = sl[i] * q[i];
                    sl[i + 1] = sl[i] - dd[i];
                    yl[i] = r[i] * (sl[i] - dd[i]) + a[i] * r[i] * dd[i];
                }
            }
            for (int i = 1; i < rows; i++)
            {
                t[rows - i] = t[rows + 1 - i] + yl[rows - i];
                e[rows - i] = sl[rows - i] == 0.0
                    ? Constant.MISSING
                    : t[rows - i] / sl[rows - i];
            }
        }

        /// <summary>
        /// The age at the start of the interval in which most of the table's deaths fall (the last such interval, if two have as many),
        /// and the median length of life: the age at which half of the 100,000 are alive, taken along a straight line between the
        /// starts of the two intervals that it lies between.
        /// </summary>
        /// <param name="rows">The number of intervals.</param>
        /// <param name="dd">The number dying in each interval of the table.</param>
        /// <param name="emo">On return, the age at the start of the interval with most deaths.</param>
        /// <param name="emd">On return, the median; infinite if more than half are alive at the start of the open interval.</param>
        /// <param name="sl">The number alive at the start of each interval.</param>
        /// <param name="x">The age at the start of each interval.</param>
        private static void AbridgedLifetableMedianMode(int rows, double[] dd, out double emo, out double emd, double[] sl, double[] x)
        {
            // mode
            int j = 1;
            emo = dd[1];
            for (int i = 2; i <= rows; i++)
            {
                if (emo - dd[i] <= 0.0)
                {
                    j = i;
                    emo = dd[i];
                }
            }
            emo = x[j];
            // median e: beyond the table (infinite) when half of the cohort is still alive at the start of the open interval
            emd = double.PositiveInfinity;
            for (int i = 2; i <= rows; i++)
            {
                if (sl[i] - 50000.0 <= 0.0)
                {
                    emd = (50000.0 - sl[i]) / (sl[i - 1] - sl[i]);
                    emd = emd * (x[i - 1] - x[i]) + x[i];
                    break;
                }
            }
        }

        /// <summary>
        /// The variances of the life table.  That of the probability of dying is q^2 (1 - q) / D, D being the deaths observed in the
        /// interval.  That of the expectation of life at the start of an interval is the sum, over that interval and those after it
        /// but the open one, of l^2 [(1 - a) n + e']^2 var(q), over the square of the l of the interval itself; e' is the expectation
        /// at the end of the interval that the term belongs to.  The open interval adds nothing: its death rate is taken as known.
        /// </summary>
        /// <param name="rows">The number of intervals.</param>
        /// <param name="vq">On return, the variance of each probability of dying.</param>
        /// <param name="q">The probability of dying in each interval.</param>
        /// <param name="d">The deaths observed in each interval.</param>
        /// <param name="a">The fraction of each interval that those who die in it live through.  That of the open interval is made missing.</param>
        /// <param name="r">The length of each interval.</param>
        /// <param name="e">The expectation of life at the start of each interval.</param>
        /// <param name="sl">The number alive at the start of each interval.</param>
        /// <param name="ve">On return, the variance of each expectation of life.</param>
        /// <param name="vs">On return, for each interval, 100,000 w (1 - w), w being the proportion alive at its start.</param>
        private static void AbridgedLifetableVariance(int rows, double[] vq, double[] q, double[] d, double[] a, double[] r, double[] e, double[] sl, double[] ve, double[] vs)
        {
            double[] f = new double[rows + 1];
            double[] c = new double[rows + 1];
            for (int i = 1; i <= rows; i++)
            {
                vq[i] = q[i] * q[i] * (1.0 - q[i]) / d[i];
                if (i < rows)
                {
                    double b = (1.0 - a[i]) * r[i] + e[i + 1];
                    c[i] = sl[i] * sl[i] * b * b * vq[i];
                }
            }
            f[rows - 1] = c[rows - 1];
            for (int i = 1; i <= rows - 2; i++)
            {
                f[rows - 1 - i] = f[rows - i] + c[rows - 1 - i];
            }
            for (int i = 1; i < rows; i++)
            {
                ve[i] = sl[i] == 0.0
                    ? Constant.MISSING
                    : f[i] / (sl[i] * sl[i]);
                if (sl[1] == 0.0)
                {
                    vs[i] = Constant.MISSING;
                }
                else
                {
                    double w = sl[i] / sl[1];
                    vs[i] = sl[1] * w * (1.0 - w);
                }
            }
            if (sl[1] == 0.0)
            {
                vs[rows] = Constant.MISSING;
            }
            else
            {
                double w = sl[rows] / sl[1];
                vs[rows] = sl[1] * w * (1.0 - w);
            }
            ve[rows] = Constant.MISSING;
            vq[rows] = Constant.MISSING;
            a[rows] = Constant.MISSING;
        }

        /// <summary>
        /// Refuses data that an abridged life table cannot be made from, with a message that says what is wrong: columns of the wrong
        /// lengths, a blank cell, a length or a population that is not above zero, an open interval without deaths.
        /// </summary>
        /// <param name="parameters">As for RptAbridgedLifetable.</param>
        private static void AbridgedLifetableCheck(ParameterBag parameters)
        {
            const string title = "Abridged life table";
            double[] lengths = (parameters["intervals"].AsDataFrame.Variables[0] as DoubleVariable).Data;
            double[] population = (parameters["population"].AsDataFrame.Variables[0] as DoubleVariable).Data;
            double[] deaths = (parameters["deaths"].AsDataFrame.Variables[0] as DoubleVariable).Data;
            int rows = lengths.Length + 1;
            if (lengths.Length < 1)
                throw new TemplateOperationCancelledException("There must be at least one interval with a length, and the open interval after it.", title);
            if (population.Length != rows || deaths.Length != rows)
                throw new TemplateOperationCancelledException("There must be one more row of populations and of deaths than of interval lengths: the last row is for the open interval at the end of the table. There are " + lengths.Length + " lengths, " + population.Length + " populations and " + deaths.Length + " numbers of deaths.", title);
            for (int i = 0; i < rows; i++)
            {
                string row = "row " + (i + 1);
                if ((i < rows - 1 && lengths[i] == Constant.MISSING) || population[i] == Constant.MISSING || deaths[i] == Constant.MISSING)
                    throw new TemplateOperationCancelledException("Every interval must have a length, a population and a number of deaths: there is a blank cell in " + row + ".", title);
                if (i < rows - 1 && lengths[i] <= 0.0)
                    throw new TemplateOperationCancelledException("The length of an interval must be more than zero: see " + row + ".", title);
                if (population[i] <= 0.0)
                    throw new TemplateOperationCancelledException("The population of an interval must be more than zero: see " + row + ".", title);
            }
            if (deaths[rows - 1] == 0.0)
                throw new TemplateOperationCancelledException("The open interval at the end of the table must have deaths: its death rate is what closes the table.", title);
            foreach (string name in new[] { "fractions", "weights" })
            {
                if (!parameters.ContainsKey(name) || parameters[name] == null)
                    continue;
                double[] values = (parameters[name].AsDataFrame.Variables[0] as DoubleVariable).Data;
                int needed = name == "fractions" ? rows - 1 : rows;
                if (values.Length < needed)
                    throw new TemplateOperationCancelledException("There must be " + needed + " rows of " + name + ": there are " + values.Length + ".", title);
                for (int i = 0; i < needed; i++)
                    if (values[i] == Constant.MISSING)
                        throw new TemplateOperationCancelledException("There is a blank cell in row " + (i + 1) + " of the " + name + ".", title);
            }
        }

        /// <summary>
        /// An age as it is printed: a whole number without decimal places, any other number in full.
        /// </summary>
        private static string LifetabAge(double age)
        {
            return age == Math.Floor(age) ? Convert.ToInt32(age).ToString() : Formatting.XUnrounded(age);
        }

        /// <summary>
        /// The label of interval i of a life table, from the ages at which the intervals start.
        /// </summary>
        private static string LifetabInterval(int i, int rows, double[] x)
        {
            if (i == rows)
                return LifetabAge(x[i]) + " up";
            // an interval of whole years after the first is labelled by its first and last year ("1 to 4"); the first interval,
            // and any interval that does not start and end at whole years, by its two ends ("0 to 1", "0.5 to 1")
            double end = x[i + 1];
            if (i > 1 && x[i] == Math.Floor(x[i]) && end == Math.Floor(end))
                end -= 1;
            return LifetabAge(x[i]) + " to " + LifetabAge(end);
        }

        /// <summary>
        /// The product-limit estimates of a group, with the rows of its table.  S at a time is the product, over the times up to and
        /// with it, of (n - d) / n, n being the number at risk and d the deaths; its variance is S^2 times the sum of d / (n (n - d))
        /// (Greenwood's formula).  H is -log S, and its variance is that sum.  Once the last subject at risk has died S is 0 and H is
        /// infinite (it is held as missing and printed as infinite), and the two standard errors have no value.
        /// </summary>
        /// <param name="groupParameters">Where the rows of the table are put.</param>
        /// <param name="stime">The times: stime[j, lap] is time j of group lap, each index from 1.</param>
        /// <param name="nat">The number at risk at each time of the group.</param>
        /// <param name="dead">The deaths at each time.</param>
        /// <param name="cen">The number censored at each time of the group.</param>
        /// <param name="h">On return, H at each time.</param>
        /// <param name="s">On return, S at each time.</param>
        /// <param name="vh">On return, the variance of H at each time of the group.</param>
        /// <param name="vs">On return, the variance of S at each time of the group.</param>
        /// <param name="nx">The number of times of the group.</param>
        /// <param name="lap">The number of the group.</param>
        private static void Plest(ParameterBag groupParameters, double[,] stime, int[] nat, int[,] dead, int[] cen, double[,] h, double[,] s, double[] vh, double[] vs, int nx, int lap)
        {
            double var = 0;

            double s0 = 1.0;
            IList<ParameterBag> estList = new List<ParameterBag>();
            groupParameters.AddOutput("*est", estList);
            for (int j = 1; j <= nx; j++)
            {
                if (nat[j] > 0)
                {
                    s0 = s0 * Convert.ToDouble(nat[j] - dead[j, lap]) / Convert.ToDouble(nat[j]);
                    if (nat[j] - dead[j, lap] > 0)
                        var += Convert.ToDouble(dead[j, lap]) / (Convert.ToDouble(nat[j]) * Convert.ToDouble(nat[j] - dead[j, lap]));
                    else
                        var = 0.0;
                }
                else
                {
                    s0 = 0.0;
                    var = 0.0;
                }
                s[j, lap] = s0;
                if (s0 > 0.0)
                {
                    // -log(1) is a negative zero, which would print as -0
                    h[j, lap] = s0 == 1.0 ? 0.0 : -Math.Log(s0);
                    vs[j] = s0 * s0 * var;
                    vh[j] = var;
                }
                else
                {
                    h[j, lap] = Constant.MISSING;
                    vs[j] = Constant.MISSING;
                    vh[j] = Constant.MISSING;
                }
                ParameterBag estParameters = new();
                estList.Add(estParameters);
                estParameters.AddOutput("time", stime[j, lap]);
                estParameters.AddOutput("risk", nat[j]);
                estParameters.AddOutput("dead", dead[j, lap]);
                estParameters.AddOutput("cen", cen[j]);
                estParameters.AddOutput("s", s[j, lap]);
                estParameters.AddOutput("ses", vs[j] == Constant.MISSING ? Constant.MISSING : Math.Sqrt(vs[j]));
                estParameters.AddOutput("h", h[j, lap] == Constant.MISSING ? double.PositiveInfinity : h[j, lap]);
                estParameters.AddOutput("seh", vh[j] == Constant.MISSING ? Constant.MISSING : Math.Sqrt(vh[j]));
            }
        }

        ///  <summary>
        ///  GIVEN A SYMMETRIC MATRIX ORDER N AS LOWER TRIANGLE in A() CALCULATES AN UPPER TRIANGLE, U( ), SUCH THAT UPRIME * U = A.
        ///  A MUST BE POSITIVE SEMI-DEFINITE.  ETA IS SET TO MULTIPLYING FACTOR DETERMINING EFFECTIVE 0 FOR PIVOT.
        ///  The triangle is held in a list, row after row: element (i, j) of the lower triangle, j no more than i, is number
        ///  i (i - 1) / 2 + j.  A pivot that is nothing, within eta of the element it comes from, makes its row of U nothing and adds
        ///  1 to nullty: the matrix then has less than full rank.  (LAPACK's dpptrf makes the same factor of a packed matrix, but
        ///  only of one that is positive definite.)
        ///  </summary>
        ///  <param name="a">The matrix, from element 1.</param>
        ///  <param name="n">The order of the matrix.</param>
        ///  <param name="nn">The length of the list, n (n + 1) / 2.</param>
        ///  <param name="u">On return, the factor: element (i, j) of the upper triangle, i no more than j, is number j (j - 1) / 2 + i.</param>
        ///  <param name="nullty">On return, the number of pivots that are nothing: n less the rank.</param>
        ///  <param name="ifault">On return, 0; 1 if n is below 1; 2 if the matrix is not positive semi-definite; 3 if nn is not n (n + 1) / 2.</param>
        ///  <remarks>ALGORITHM AS 6 APPL. STATIST. (1968) VOL.17, P.195</remarks>
        private static void Chol(double[] a, int n, int nn, double[] u, ref int nullty, out int ifault)
        {
            double w = 0;

            const double eta = 0.000000001;

            ifault = 1;
            if (n <= 0)
                return;

            ifault = 3;
            if (nn != n * (n + 1) / 2)
                return;

            ifault = 2;
            nullty = 0;
            int j = 1;
            int k = 0;
            const double eta2 = eta * eta;
            int ii = 0;
            for (int icol = 1; icol <= n; icol++)
            {
                ii += icol;
                double x = eta2 * a[ii];
                int l = 0;
                int kk = 0;
                int irow;
                for (irow = 1; irow <= icol; irow++)
                {
                    kk += irow;
                    k += 1;
                    w = a[k];
                    int m = j;
                    int i;
                    for (i = 1; i <= irow; i++)
                    {
                        l += 1;
                        if (i == irow)
                            break;
                        w -= u[l] * u[m];
                        m += 1;
                    }
                    if (irow == icol)
                        break;
                    if (u[l] == 0)
                    {
                        if (w * w > Math.Abs(x * a[kk]))
                            return;
                        u[k] = 0;
                    }
                    else
                    {
                        u[k] = w / u[l];
                    }
                }
                if (Math.Abs(w) <= Math.Abs(eta * a[k]))
                {
                    u[k] = 0;
                    nullty += 1;
                }
                else
                {
                    if (w < 0)
                        return;
                    u[k] = Math.Sqrt(w);
                }
                j += icol;
            }
            ifault = 0;
        }

        /// <summary>
        /// The Wei-Lachin tests of two groups of subjects who each have a time to failure, which may be censored, at each of several
        /// repeats: for each repeat a test of the difference between the groups, and for the repeats together an omnibus test and a
        /// test of stochastic ordering; each by Gehan's generalised Wilcoxon statistic and by the log-rank statistic (XWeiLachin).
        /// A subject without a time at a repeat is kept, with a time before every other and censored, so that at that repeat the
        /// subject is at risk of nothing.
        /// </summary>
        /// <param name="parameters">"gid": the group of each subject (two groups); "nr": the number of repeats; "times" and "censor": a
        /// column of each for every repeat (1 for a failure, 0 for a censored time).</param>
        public static StepOutput RptWeiLachin(ParameterBag parameters)
        {
            int gid2 = 0;
            int[,] s;
            double[,] x;

            DataFrame gidFrame = parameters["gid"].AsDataFrame;
            ClassifierVariable gidVariable = gidFrame.Variables[0] as ClassifierVariable;
            if (gidVariable.GroupCount != 2)
                throw new TemplateOperationCancelledException("Group identifier must contain two groups and no missing data.", "Wei-Lachin");

            int rows = gidVariable.Length;
            int[] g = new int[rows + 1];
            int[] n = new int[2 + 1];
            int gid1 = Convert.ToInt32(gidVariable.Data[0]) + 1;
            for (int j = 1; j <= rows; j++)
            {
                if (gidVariable.Data[j - 1] + 1 != gid1)
                    gid2 = Convert.ToInt32(gidVariable.Data[j - 1]) + 1;
                g[j] = Convert.ToInt32(gidVariable.Data[j - 1]) + 1;
            }
            for (int j = 1; j <= rows; j++)
            {
                if (g[j] == gid1)
                    n[1]++;
                else if (g[j] == gid2)
                    n[2]++;
            }
            int nr = parameters["nr"].AsInt32;
            if (nr > 0)
            {
                double minTime = Constant.MISSING;
                s = new int[rows + 1, nr + 1];
                x = new double[rows + 1, nr + 1];

                DataFrame timesFrame = parameters["times"].AsDataFrame;
                DataFrame censorFrame = parameters["censor"].AsDataFrame;
                for (int j = 1; j <= nr; j++)
                {
                    DoubleVariable timesVariable = timesFrame.Variables[j - 1]as DoubleVariable;
                    DoubleVariable censorVariable = censorFrame.Variables[j - 1]as DoubleVariable;
                    for (int r = 1; r <= rows; r++)
                    {
                        x[r, j] = timesVariable.Data[r - 1];
                        if (x[r, j] < minTime)
                            minTime = x[r, j];
                    }
                    for (int r = 1; r <= rows; r++)
                    {
                        double dv = censorVariable.Data[r - 1];
                        if (0.0 == dv || 1.0 == dv)
                            s[r, j] = Convert.ToInt32(censorVariable.Data[r - 1]);
                        else if (Constant.MISSING == dv)
                            s[r, j] = 0;
                        else
                            throw new TemplateOperationCancelledException("Censorship value must be 0 or 1 only.", "Wei-Lachin");
                    }
                }
                //  find missing times, censor them, and code them as minimum observed time minus one
                //  if min time is 0 and all time 0 are censored, assume that is a missing data pattern
                //  (minTime starts at the value that stands for a missing number, which no time is below, and so it stays there: the
                //  code that a missing time is given is that value less 1, which in floating point is the same value.  A missing time
                //  therefore keeps the missing value, which is before every time, and is censored)
                double missingCode;
                if (minTime == 0.0)
                {
                    missingCode = 0.0;
                    for (int j = 1; j <= nr; j++)
                    {
                        for (int r = 1; r <= rows; r++)
                        {
                            if (x[r, j] == 0.0 & s[r, j] == 1)
                            {
                                missingCode = minTime - 1.0;
                                break;
                            }
                        }
                    }
                }
                else
                {
                    missingCode = minTime - 1.0;
                }
                for (int j = 1; j <= nr; j++)
                {
                    for (int r = 1; r <= rows; r++)
                    {
                        if (x[r, j] == Constant.MISSING)
                        {
                            x[r, j] = missingCode;
                            s[r, j] = 0;
                        }
                    }
                }
            }
            else
            {
                throw new TemplateOperationCancelledException("Must have at least one repeat", "Wei-Lachin");
            }

            ParameterBag outputParameters = new();
            IList<ParameterBag> outerList = new List<ParameterBag>();
            outputParameters.AddOutput("*outer", outerList);
            ParameterBag outerParameters = new();
            outerList.Add(outerParameters);
            XWeiLachin(outerParameters, nr, rows, n, g, s, x, 1, out int ifault);
            if (ifault == 0)
            {
                outerParameters = new ParameterBag();
                outerList.Add(outerParameters);
                XWeiLachin(outerParameters, nr, rows, n, g, s, x, 2, out ifault);
            }
            if (ifault != 0)
                throw new Exception("Error in calculation, report invalid");
            return new StepOutput(outputParameters);
        }

        /// <summary>
        /// The Wei-Lachin statistics for one of the two kinds of score.  For each repeat the statistic is the sum over the failures of
        /// the weight times (1 if the failure is in the first group, less the share of the first group among those at risk at the time),
        /// over the square root of the number of subjects.  The weight is 1 for the log-rank score (method 2) and, for Gehan's score
        /// (method 1), the share of all the subjects who are at risk at the time.  The variances and covariances of the statistics
        /// of the repeats are estimated from the part that each subject plays in them.  A repeat has the chi-square of its statistic
        /// squared over its variance; the repeats together have the omnibus chi-square, the quadratic form of the statistics in the
        /// generalised inverse of the matrix, on its rank as degrees of freedom, and the normal deviate for stochastic ordering, the sum
        /// of the statistics over the square root of the sum of all the elements of the matrix.
        /// The variance is a little small in small samples for the log-rank score: with 40 subjects and no difference between the
        /// groups the chi-square of a repeat has a mean of about 1.18 where it should be 1, with 80 about 1.10 and with 300 about 1.03.
        /// </summary>
        /// <param name="outputParameters">Where the results are put.</param>
        /// <param name="nr">The number of repeats.</param>
        /// <param name="nt">The number of subjects.</param>
        /// <param name="n">The number of subjects in each of the two groups.</param>
        /// <param name="g">The group of each subject, 1 or 2.</param>
        /// <param name="s">s[j, k] is 1 if subject j failed at repeat k and 0 if the time is censored.</param>
        /// <param name="x">x[j, k] is the time of subject j at repeat k.</param>
        /// <param name="method">1 for Gehan's score, 2 for the log-rank score.</param>
        /// <param name="ifault">On return, 0; 1 if nobody is at risk at a failure; 2 if a group has no subjects.</param>
        private static void XWeiLachin(ParameterBag outputParameters, int nr, int nt, int[] n, int[] g, int[,] s, double[,] x, int method, out int ifault)
        {
            int nn = (int)Math.Floor((double)nr * (nr + 1) / 2);
            int[,] y = new int[2 + 1, nt + 1];
            int[,] d = new int[2 + 1, nr + 1];
            double[] qe = new double[2 + 1];
            double[,] ees = new double[2 + 1, nr + 1];
            double[, ,] mu = new double[2 + 1, nt + 1, nr + 1];
            double[, ,] psi = new double[2 + 1, nt + 1, nr + 1];
            double[] sigma = new double[nn + 1];
            double[] siginv = new double[nn + 1];
            double[] wlt = new double[nr + 1];
            double[] nruniv = new double[nr + 1];
            double[, ,] sig = new double[2 + 1, nr + 1, nr + 1];
            ifault = 0;
            for (int i = 1; i <= 2; i++)
            {
                for (int k = 1; k <= nr; k++)
                {
                    ees[i, k] = 0.0;
                    d[i, k] = 0;
                    for (int k2 = 1; k2 <= nr; k2++)
                        sig[i, k, k2] = 0.0;
                }
            }
            //   Y = NUMBER AT RISK OF FAILURE
            //   ees: PARTIAL SUM USED in EQN. 1
            //   QE = Q*E (SEE EQN. 4)
            //   EVALUATE MU (EQN. 4)
            //   For a failure, y holds the numbers at risk in the two groups and qe[i] is the weight times the share of group i among
            //   those at risk.  A failure in the first group adds qe[2] to the sum of the first group, and a failure in the second
            //   adds qe[1] to the sum of the second: the difference between the two sums is the score.
            for (int k = 1; k <= nr; k++)
            {
                for (int j = 1; j <= nt; j++)
                {
                    for (int i = 1; i <= 2; i++)
                    {
                        y[i, j] = 0;
                        mu[i, j, k] = 0.0;
                    }
                    if (s[j, k] == 1)
                    {
                        for (int j2 = 1; j2 <= nt; j2++)
                            if (x[j2, k] >= x[j, k])
                                y[g[j2], j] = y[g[j2], j] + 1;
                        double sum = y[1, j] + y[2, j];
                        if (sum == 0.0)
                        {
                            ifault = 1;
                            return;
                        }
                        double q = 0;
                        if (method == 1)
                            q = sum / nt;
                        if (method == 2)
                            q = 1.0;
                        for (int i = 1; i <= 2; i++)
                            qe[i] = q * y[i, j] / sum;
                        mu[2, j, k] = qe[1];
                        mu[1, j, k] = qe[2];
                        ees[g[j], k] = ees[g[j], k] + mu[g[j], j, k];
                        d[g[j], k] = d[g[j], k] + 1;
                    }
                    else
                    {
                        mu[1, j, k] = 0.0;
                        mu[2, j, k] = 0.0;
                    }
                }
                //   EVALUATE PSI (EQN. 5)
                for (int j = 1; j <= nt; j++)
                {
                    for (int i = 1; i <= 2; i++)
                    {
                        psi[i, j, k] = 0.0;
                        for (int j2 = 1; j2 <= nt; j2++)
                        {
                            if (g[j2] == i && x[j2, k] <= x[j, k])
                            {
                                if (s[j2, k] == 1)
                                    psi[i, j, k] = psi[i, j, k] + mu[i, j2, k] / y[i, j2];
                            }
                        }
                    }
                }
            }
            //   COMPUTE ENTRIES in COVARIANCE MATRIX (SEE EQNS. 2 AND 3)
            for (int k1 = 1; k1 <= nr; k1++)
            {
                for (int k2 = 1; k2 <= k1; k2++)
                {
                    for (int j = 1; j <= nt; j++)
                    {
                        int i = g[j];
                        if (n[i] == 0)
                        {
                            ifault = 2;
                            return;
                        }
                        sig[i, k1, k2] = sig[i, k1, k2] + (mu[i, j, k1] * s[j, k1] - psi[i, j, k1]) * (mu[i, j, k2] * s[j, k2] - psi[i, j, k2]) / n[i];
                    }
                    int ipoint = (int)Math.Floor((double)k1 * (k1 - 1) / 2) + k2;
                    sigma[ipoint] = (n[1] * sig[1, k1, k2] + n[2] * sig[2, k1, k2]) / nt;
                }
            }
            //   COMPUTE WEI-LACHIN UNIVARIATE TEST NRUNIV (SEE EQNS 1 AND 6)
            outputParameters.AddOutput("title",
                                       method == 1 ? "Univariate Generalised Wilcoxon (Gehan)" : "Univariate Log-Rank");
            outputParameters.AddOutput("tot", nt);
            outputParameters.AddOutput("grp_1", n[1]);
            outputParameters.AddOutput("grp_2", n[2]);
            IList<ParameterBag> repeatsList = new List<ParameterBag>();
            outputParameters.AddOutput("*repeats", repeatsList);
            for (int k = 1; k <= nr; k++)
            {
                ParameterBag repeatsParameters = new();
                repeatsList.Add(repeatsParameters);
                wlt[k] = Math.Abs(ees[1, k] - ees[2, k]) < Constant.EPSILON * 100.0
                    ? 0.0
                    : (ees[1, k] - ees[2, k]) / Math.Sqrt(nt);
                int ipoint = (int)Math.Floor((double)k * (k + 1) / 2);
                repeatsParameters.AddOutput("time", k);
                repeatsParameters.AddOutput("fail_1", d[1, k]);
                repeatsParameters.AddOutput("fail_2", d[2, k]);
                repeatsParameters.AddOutput("t", wlt[k]);
                // a repeat with no difference, or with no variance (no failures, or one subject per group), has no test of its own;
                // the multivariate tests take the generalised inverse of the covariance matrix
                if (wlt[k] == 0.0 || sigma[ipoint] <= 0.0)
                {
                    repeatsParameters.AddOutput("var", Constant.MISSING);
                    repeatsParameters.AddOutput("chi", Constant.MISSING);
                    repeatsParameters.AddOutput("p", Constant.MISSING);
                }
                else
                {
                    nruniv[k] = wlt[k] / Math.Sqrt(sigma[ipoint]);
                    repeatsParameters.AddOutput("var", sigma[ipoint]);
                    repeatsParameters.AddOutput("chi", Math.Pow(nruniv[k], 2.0));
                    repeatsParameters.AddOutput("p", PDF.chivalp(Math.Pow(nruniv[k], 2.0), 1.0));
                }
            }

            //   COMPUTE INVERSE OF COVARIANCE MATRIX AND WEI-LACHIN
            //   MULTIVARIATE STATISTICS CHIOMB (FOR OMNIBUS TEST) AND
            //   NRSTOC (FOR TEST OF STOCHASTIC ORDERING)  (SEE EQN 7)
            int nullty = 0;
            Syminv(sigma, nr, nn, siginv, ref nullty, out int _);
            double chiomb = 0.0;
            double tsum = 0.0;
            double sigsum = 0.0;
            for (int j = 1; j <= nr; j++)
            {
                tsum += wlt[j];
                int ipoint = (int)Math.Floor((double)j * (j + 1) / 2);
                chiomb += wlt[j] * wlt[j] * siginv[ipoint];
                sigsum += sigma[ipoint];
                int jm1 = j - 1;
                for (int j2 = 1; j2 <= jm1; j2++)
                {
                    ipoint = (int)Math.Floor((double)j * (j - 1) / 2) + j2;
                    chiomb += 2.0 * wlt[j] * wlt[j2] * siginv[ipoint];
                    sigsum += 2.0 * sigma[ipoint];
                }
            }
            double nrstoc = sigsum > 0.0 ? tsum / Math.Sqrt(sigsum) : Constant.MISSING;
            outputParameters.AddOutput("title_multi",
                method == 1
                    ? "Multivariate Generalised Wilcoxon (Gehan)"
                    : "Multivariate Log-Rank");
            int zImax = 0;
            int zImin = 1;
            IList<ParameterBag> covarList = new List<ParameterBag>();
            outputParameters.AddOutput("*covar", covarList);
            for (int r = 1; r <= nr; r++)
            {
                IList<ParameterBag> matList = new List<ParameterBag>();
                covarList.Add(new ParameterBag("*mat", new FilledParameterBagListParameter(FilledParameterDirection.Output, matList)));
                zImax = r + zImax;
                for (int i = zImin; i <= zImax; i++)
                    matList.Add(new ParameterBag("cell", new FilledDoubleParameter(FilledParameterDirection.Output, sigma[i])));
                zImin = zImax + 1;
            }
            zImax = 0;
            zImin = 1;
            IList<ParameterBag> invCovarList = new List<ParameterBag>();
            outputParameters.AddOutput("*inv_covar", invCovarList);
            for (int r = 1; r <= nr; r++)
            {
                IList<ParameterBag> matList = new List<ParameterBag>();
                invCovarList.Add(new ParameterBag("*mat", new FilledParameterBagListParameter(FilledParameterDirection.Output, matList)));
                zImax = r + zImax;
                for (int i = zImin; i <= zImax; i++)
                    matList.Add(new ParameterBag("cell", new FilledDoubleParameter(FilledParameterDirection.Output, siginv[i])));
                zImin = zImax + 1;
            }

            outputParameters.AddOutput("rep", nr);
            outputParameters.AddOutput("stat", chiomb);
            // the omnibus statistic is a quadratic form in the generalised inverse, so its degrees of freedom are the rank
            int rank = nr - nullty;
            outputParameters.AddOutput("p_omnibus", rank > 0 ? PDF.chivalp(chiomb, rank) : Constant.MISSING);
            if (nullty > 0)
            {
                IList<ParameterBag> singularList = new List<ParameterBag>();
                outputParameters.AddOutput("*singular", singularList);
                ParameterBag singularParameters = new();
                singularList.Add(singularParameters);
                singularParameters.AddOutput("rank", rank);
            }
            else
            {
                outputParameters.AddOutput("*singular", null);
            }
            outputParameters.AddOutput("z", nrstoc);
            double p = nrstoc == Constant.MISSING ? Constant.MISSING : 1.0 - PDF.alnorm(Math.Abs(nrstoc));
            if (p > 1.0 - p)
                p = 1.0 - p;
            outputParameters.AddOutput("p_1", p);
            outputParameters.AddOutput("p_2", p == Constant.MISSING ? Constant.MISSING : p * 2.0);
        }

        ///  <summary>
        ///  FORMS in C( ) AS LOWER TRIANGLE, A GENERALIZED INVERSE OF THE POSITIVE SEMI-DEFINATE SYMMETRIC MATRIX A() ORDER N, STORED AS LOWER TRIANGLE.
        ///  The factor of Chol is made first, and the inverse from it a row at a time, from the last.  A row whose pivot is nothing
        ///  has a row and a column of nothing in the inverse.  (LAPACK's dpptri makes the inverse of a packed matrix from its factor.)
        ///  </summary>
        ///  <param name="a">The matrix, held as for Chol.</param>
        ///  <param name="n">The order of the matrix.</param>
        ///  <param name="nn">The length of the list, n (n + 1) / 2.</param>
        ///  <param name="c">On return, the generalised inverse, held in the same way.</param>
        ///  <param name="nullty">On return, n less the rank of the matrix.</param>
        ///  <param name="ifault">On return, 0, or the fault of Chol.</param>
        ///  <remarks>ALGORITHM AS 7 APPL. STATIST. (1968) VOL.17, P.198</remarks>
        private static void Syminv(double[] a, int n, int nn, double[] c, ref int nullty, out int ifault)
        {
            Chol(a, n, nn, c, ref nullty, out ifault);
            if (ifault != 0)
                return;

            int irow = n;
            int ndiag = nn;
            double[] w = new double[n + 1];
            do
            {
                int l = ndiag;
                if (c[ndiag] != 0.0)
                {
                    for (int i = irow; i <= n; i++)
                    {
                        w[i] = c[l];
                        l += i;
                    }
                    int icol = n;
                    int jcol = nn;
                    int mdiag = nn;
                    do
                    {
                        l = jcol;
                        double x = 0.0;
                        if (icol == irow)
                            x = 1.0 / w[irow];
                        int k = n;
                        do
                        {
                            if (k == irow)
                                break;
                            x -= w[k] * c[l];
                            k--;
                            l--;
                            if (l > mdiag)
                                l = l - k + 1;
                        }
                        while (true);
                        c[l] = x / w[irow];
                        if (icol == irow)
                            break;
                        mdiag -= icol;
                        icol--;
                        jcol--;
                    }
                    while (true);
                }
                else
                {
                    for (int j = irow; j <= n; j++)
                    {
                        c[l] = 0;
                        l += j;
                    }
                }
                ndiag -= irow;
                irow--;
            }
            while (irow != 0);
        }

        /// <summary>
        /// Adds to the frame that is saved ten columns for a group, with a row for each subject in order of time: the time, the code,
        /// S with its standard error and confidence limits, and H with its standard error and limits.  The limits of S are from those
        /// of log(-log S), whose variance is the variance of H over the square of log S, so that they lie between 0 and 1.  Where S
        /// is 1 its standard error is nothing and both limits are 1; where S is 0 the limits have no value.  The limits of H are H
        /// plus and minus the normal deviate times its standard error.
        /// </summary>
        /// <param name="resultsFrame">The frame that is saved.</param>
        /// <param name="stime">The times, as for Plest.</param>
        /// <param name="nat">The number at risk at each time of the group.</param>
        /// <param name="dead">The deaths at each time.</param>
        /// <param name="s">S at each time.</param>
        /// <param name="h">H at each time.</param>
        /// <param name="vs">The variance of S at each time of the group.</param>
        /// <param name="vh">The variance of H at each time of the group.</param>
        /// <param name="nx">The number of times of the group.</param>
        /// <param name="lap">The number of the group.</param>
        /// <param name="gamma">The confidence level.</param>
        /// <param name="allcens">The code of each subject of the group, in order of time, from element 1.</param>
        /// <param name="alltime">The time of each subject of the group, in the same order.</param>
        private static void Plsave(DataFrame resultsFrame, double[,] stime, int[] nat, int[,] dead, double[,] s, double[,] h, double[] vs, double[] vh, int nx, int lap, double gamma, int[] allcens, double[] alltime)
        {
            double p = (1.0 - gamma) / 2;
            double cit = PDF.gauinv(1.0 - p);
            int r = 0;  // rows written, from 0
            int k = 1;  // position in alltime and allcens, which Plprep fills from 1: reading them from 0 wrote a spurious first row for every group
            double sumn = 0.0;
            double sumd = 0.0;
            string g = Formatting.XRound(gamma * 100, 1);
            string grp = dead.GetUpperBound(1) > 1 ? " (group " + lap.ToString() + ")" : string.Empty;
            DoubleVariable timeVariable = new(nx, "Time" + grp);
            StringVariable deathVariable = new(nx, "Death/Event" + grp);
            DoubleVariable survivalVariable = new(nx, "Survival Proportion (S)" + grp);
            DoubleVariable seVariable = new(nx, "Approx. SE(S)" + grp);
            DoubleVariable selVariable = new(nx, g + "% LCI S" + grp);
            DoubleVariable seuVariable = new(nx, g + "% UCI S" + grp);
            DoubleVariable cumhVariable = new(nx, "Cumulative Hazard (H)" + grp);
            DoubleVariable sehVariable = new(nx, "Approx. SE(H)" + grp);
            DoubleVariable sehlVariable = new(nx, g + "% LCI H" + grp);
            DoubleVariable sehuVariable = new(nx, g + "% UCI H" + grp);
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
                    sumn += Convert.ToDouble(dead[j, lap]) / (Convert.ToDouble(nat[j]) * Convert.ToDouble(nat[j] - dead[j, lap]));
                    sumd += Math.Log(Convert.ToDouble(nat[j] - dead[j, lap]) / Convert.ToDouble(nat[j]));
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
                    timeVariable.SetData(r, stime[j, lap]);
                    deathVariable.SetData(r, allcens[k].ToString());
                    survivalVariable.SetData(r, s[j, lap]);
                    seVariable.SetData(r, vs[j] == Constant.MISSING ? Constant.MISSING : Math.Sqrt(vs[j]));
                    if (conus == Constant.MISSING)
                    {
                        if (vs[j] == Constant.MISSING)
                        {
                            selVariable.SetData(r, Constant.MISSING);
                            seuVariable.SetData(r, Constant.MISSING);
                        }
                        else
                        {
                            selVariable.SetData(r, s[j, lap] - cit * Math.Sqrt(vs[j]));
                            seuVariable.SetData(r, s[j, lap] + cit * Math.Sqrt(vs[j]));
                        }
                    }
                    else
                    {
                        selVariable.SetData(r, conls);
                        seuVariable.SetData(r, conus);
                    }
                    if (h[j, lap] == Constant.MISSING)
                    {
                        cumhVariable.SetData(r, Constant.MISSING);
                        sehVariable.SetData(r, Constant.MISSING);
                        sehlVariable.SetData(r, Constant.MISSING);
                        sehuVariable.SetData(r, Constant.MISSING);
                    }
                    else
                    {
                        cumhVariable.SetData(r, h[j, lap]);
                        sehVariable.SetData(r, Math.Sqrt(vh[j]));
                        sehlVariable.SetData(r, h[j, lap] - cit * Math.Sqrt(vh[j]));
                        sehuVariable.SetData(r, h[j, lap] + cit * Math.Sqrt(vh[j]));
                    }
                    r++;
                    k++;
                }
                while (alltime[k] == stime[j, lap]);
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

        /// <summary>
        /// Makes the table of a group from the records: its times in order, each once, with the deaths and the number censored at each
        /// and the number at risk just before each, which is the number of the group less those who died or were censored earlier.
        /// </summary>
        /// <param name="arr2">The records: a column for each, with the group in row 0, the time in row 1 and the code in row 2.</param>
        /// <param name="cdat1">Its first element has the number of records.</param>
        /// <param name="stime">On return, the times of the group.</param>
        /// <param name="dead">On return, the deaths at each time.</param>
        /// <param name="nat">On return, the number at risk at each time.</param>
        /// <param name="cen">On return, the number censored at each time.</param>
        /// <param name="gnx">The number of records of each group.</param>
        /// <param name="nx">On return, the number of times of the group.</param>
        /// <param name="lap">The number of the group.</param>
        /// <param name="nt">On return, the number of records.</param>
        /// <param name="allcens">On return, the code of each record of the group in order of time, from element 1.</param>
        /// <param name="alltime">On return, the time of each record of the group in the same order.</param>
        private static void Plprep(double[,] arr2, ColumnData[] cdat1, double[,] stime, int[,] dead, int[] nat, int[] cen, int[] gnx, out int nx, int lap, out int nt, int[] allcens, double[] alltime)
        {
            nt = cdat1[0].Rows;
            Trisvar[] q = new Trisvar[nt + 1];
            int nxx = 0;
            for (int j = 1; j <= nt; j++)
            {
                if (arr2[1, j] != Constant.MISSING & arr2[1, j] != Constant.MISSING & arr2[2, j] != Constant.MISSING)
                {
                    if (j >= 1)
                        nxx++;
                    q[j] = new Trisvar { Tm = arr2[1, j], Gp = Convert.ToInt32(arr2[0, j]), Cs = Convert.ToInt32(arr2[2, j]) };
                }
            }
            Array.Sort(q, 1, nt, new TrisvarByTmThenGp());
            nx = 0;
            nt = nxx;
            nat[1] = gnx[lap];
            for (int j = 1; j <= nt; j++)
            {
                if (q[j].Gp == lap)
                {
                    int wdr = 0;
                    nx += 1;
                    stime[nx, lap] = q[j].Tm;
                    dead[nx, lap] = q[j].Cs;
                    if (dead[nx, lap] == 0)
                        wdr = 1;
                    for (int j2 = j; j2 < nt; j2++)
                    {
                        if (q[j].Tm == q[j2 + 1].Tm && q[j2 + 1].Gp == q[j].Gp)
                        {
                            if (q[j2 + 1].Cs == 1)
                                dead[nx, lap]++;
                            else
                                wdr++;
                            j++;
                        }
                        else
                        {
                            break;
                        }
                    }
                    nat[nx + 1] = nat[nx] - dead[nx, lap] - wdr;
                    cen[nx] = wdr;
                }
            }
            int ctr = 0;
            for (int j = 1; j <= nt; j++)
            {
                if (q[j].Gp == lap)
                {
                    ctr++;
                    alltime[ctr] = q[j].Tm;
                    allcens[ctr] = q[j].Cs;
                }
            }
        }

        /// <summary>
        /// Counts the groups before the dialog box goes on: with more than two groups the test for trend needs a score for each group,
        /// and the scores 1, 2, 3 ... are offered for the user to change.
        /// </summary>
        /// <param name="parameters">"gid": the group of each record.</param>
        public static StepOutput RptLogRankPreprocess(ParameterBag parameters)
        {
            DataFrame gidFrame = parameters["gid"].AsDataFrame;
            ClassifierVariable gidVariable = gidFrame.Variables[0] as ClassifierVariable;
            int rows = gidVariable.Length;
            double[] g = new double[rows + 1];
            for (int r = 1; r <= rows; r++)
                g[r] = gidVariable.Data[r - 1] + 1;
            double[] groupIds = new double[rows + 1];   // filled from 1: every row may be its own group
            int igot = 0;
            for (int r = 1; r <= rows; r++)
            {
                double temp = g[r];
                if (temp != Constant.MISSING)
                {
                    bool found = false;
                    for (int j = 1; j <= igot; j++)
                    {
                        if (temp == groupIds[j])
                        {
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                        groupIds[++igot] = temp;
                }
            }

            int groups = igot;

            ParameterBag outputParameters = new();
            if (groups > 2)
            {
                DoubleVariable scores = new(groups, "scores");
                for (int j = 1; j <= groups; j++)
                    scores.Data[j - 1] = j;
                outputParameters.AddInput("group_scores", new DataFrame(scores));
            }
            return new StepOutput(outputParameters);
        }

        /// <summary>
        /// Reads the records for the log-rank and Wilcoxon tests.  The groups, and the strata if there are any, are numbered from 1 in
        /// the order in which they are first met.  A record whose code is above 1 stands for that number of deaths, and is given a
        /// record for each.  A blank cell is left as it is: the tests leave out a record that has one.
        /// </summary>
        /// <param name="parameters">As for RptLogRank.</param>
        /// <param name="rows">On return, the number of records, those added for codes above 1 among them.</param>
        /// <param name="gamma">On return, the confidence level.</param>
        /// <param name="cit">On return, the normal deviate of the confidence level.</param>
        /// <param name="groups">On return, the number of groups.</param>
        /// <param name="strata">On return, the number of strata, or 0 if there are none.</param>
        /// <param name="scores">On return, the scores of the groups for the test for trend, from element 1; nothing if there are two groups.</param>
        /// <param name="gid">On return, the title of the group variable.</param>
        /// <param name="groupIds">On return, for each group, the number of its group in the data plus 1.</param>
        /// <param name="groupLabels">On return, the labels of the groups of the data, from element 1.</param>
        /// <param name="stratumLabels">On return, the labels of the strata.</param>
        /// <param name="ifault">On return, false when the records have been read.</param>
        /// <param name="arr2">On return, the records: a column for each, with the group in row 0, the time in row 1, the code in row 2 and the
        /// stratum in row 3.</param>
        /// <param name="cdat1">On return, the number of records in each of its elements.</param>
        private static void Petoprep(ParameterBag parameters, out int rows, out double gamma, out double cit, out int groups, out int strata, out double[] scores, out string gid, out double[] groupIds, out string[] groupLabels, out string[] stratumLabels, out bool ifault, out double[,] arr2, out ColumnData[] cdat1)
        {
            ifault = true;
            gamma = parameters["gamma"].AsDouble;
            if (gamma < 0)
                throw new ArgumentException("gamma must be >= 0");

            double p = (1.0 - gamma) / 2.0;
            cit = PDF.gauinv(1.0 - p);

            DataFrame gidFrame = parameters["gid"].AsDataFrame;
            ClassifierVariable gidVariable = gidFrame.Variables[0] as ClassifierVariable;
            groupLabels = new string[gidVariable.GroupCount + 1];
            for (int j = 1; j <= gidVariable.GroupCount; j++)
                groupLabels[j] = gidVariable.Groups[j - 1].Label;
            rows = gidVariable.Length;
            gid = gidVariable.Title;
            double[] g = new double[rows + 1];
            for (int r = 1; r <= rows; r++)
                g[r] = gidVariable.Data[r - 1] + 1;
            groupIds = new double[rows + 1];   // filled from 1: every row may be its own group
            int igot = 0;
            for (int r = 1; r <= rows; r++)
            {
                double temp = g[r];
                if (temp != Constant.MISSING)
                {
                    bool found = false;
                    for (int j = 1; j <= igot; j++)
                    {
                        if (temp == groupIds[j])
                        {
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                        groupIds[++igot] = temp;
                }
            }
            // Trim gpid to the number of groups we got (presently in igot, and gpid[1..igot])
            double[] tempArray = new double[igot + 1];
            Array.Copy(groupIds, tempArray, igot + 1);
            groupIds = tempArray;

            groups = igot;
            for (int r = 1; r <= rows; r++)
            {
                for (int j = 1; j <= groups; j++)
                {
                    if (groupIds[j] == g[r])
                    {
                        g[r] = j;
                        break;
                    }
                }
            }

            DataFrame timesFrame = parameters["times"].AsDataFrame;
            DoubleVariable timesVariable = timesFrame.Variables[0] as DoubleVariable;
            double[] t = new double[rows + 1];
            for (int r = 1; r <= rows; r++)
                t[r] = timesVariable.Data[r - 1];

            DataFrame deathsFrame = parameters["deaths"].AsDataFrame;
            DoubleVariable deathsVariable = deathsFrame.Variables[0] as DoubleVariable;
            double[] c = new double[rows + 1];
            for (int r = 1; r <= rows; r++)
                c[r] = deathsVariable.Data[r - 1];

            double[] s = new double[rows + 1];
            if (parameters.ContainsKey("strata") && parameters["strata"] != null)
            {
                DataFrame strataFrame = parameters["strata"].AsDataFrame;
                ClassifierVariable strataVariable = strataFrame.Variables[0] as ClassifierVariable;
                stratumLabels = new string[strataVariable.GroupCount + 1];
                for (int j = 1; j <= strataVariable.GroupCount; j++)
                    stratumLabels[j] = strataVariable.Title + "=" + strataVariable.Groups[j - 1].Label;
                for (int r = 1; r <= rows; r++)
                    s[r] = strataVariable.Data[r - 1];
                double[] sid = new double[0 + 1];
                int igots = 0;
                for (int r = 1; r <= rows; r++)
                {
                    double temp = s[r];
                    if (temp != Constant.MISSING)
                    {
                        bool ok = true;
                        for (int j = 1; j <= igots; j++)
                        {
                            if (temp == sid[j])
                            {
                                ok = false;
                                break;
                            }
                        }
                        if (ok)
                        {
                            igots += 1;
                            // create temp variable for copying values 
                            double[] transTemp7 = new double[igots + 1];
                            Array.Copy(sid, transTemp7, Math.Min(sid.Length, transTemp7.Length));
                            sid = transTemp7;
                            sid[igots] = temp;
                        }
                    }
                }
                strata = igots;
                for (int r = 1; r <= rows; r++)
                {
                    for (int j = 1; j <= strata; j++)
                    {
                        if (sid[j] == s[r])
                        {
                            s[r] = j;
                            break;
                        }
                    }
                }
            }
            else
            {
                // No strata
                stratumLabels = null;
                strata = 0;
            }
            int extra = 0;
            for (int r = 1; r <= rows; r++)
                if (c[r] > 1)
                    extra += (int)Math.Floor(c[r]) - 1;

            // put the data back into the Public array
            arr2 = new double[3 + 1, rows + extra + 1];
            cdat1 = new ColumnData[3 + 1];
            for (int j = 0; j <= 3; j++)
                cdat1[j] = new ColumnData { Rows = rows + extra };
            int ctr = 0;
            for (int r = 1; r <= rows; r++)
            {
                if (c[r] > 1 && c[r] != Constant.MISSING)
                {
                    for (int j = 1; j <= (int)Math.Floor(c[r]); j++)
                    {
                        ctr++;
                        arr2[0, ctr] = g[r];
                        arr2[1, ctr] = t[r];
                        arr2[2, ctr] = 1;
                        arr2[3, ctr] = s[r];
                    }
                }
                else
                {
                    // a blank code is left as it is, so that its record is left out
                    if (c[r] < 0 && c[r] != Constant.MISSING)
                        c[r] = 0;
                    ctr++;
                    arr2[0, ctr] = g[r];
                    arr2[1, ctr] = t[r];
                    arr2[2, ctr] = c[r];
                    arr2[3, ctr] = s[r];
                }
            }
            rows += extra;

            if (groups < 2)
                throw new TemplateOperationCancelledException();
            if (groups > 2)
            {
                DataFrame scoresFrame = parameters["group_scores"].AsDataFrame;
                DoubleVariable scoresVariable = scoresFrame.Variables[0] as DoubleVariable;
                if (scoresVariable.Length != groups)
                    throw new ArgumentException("Scores must have the same length as the number of groups");
                scores = new double[groups + 1];
                Array.Copy(scoresVariable.Data, 0, scores, 1, groups);
            }
            else
            {
                scores = null; // Exactly two groups
            }
            ifault = false;
        }

        /// <summary>
        /// The abridged life table of a population, from the deaths and the population of each interval of age (see
        /// AbridgedLifetableBasics), with the variances of the probabilities of dying and of the expectations of life (see
        /// AbridgedLifetableVariance) and their confidence limits.  The expectation of life at birth and the median length of life are
        /// also given limits by simulation: the deaths of every interval are drawn from the Poisson distribution with the deaths
        /// observed as its mean, the table is made again, and the limits are the quantiles of what comes out.  The limits by
        /// simulation allow for the uncertainty of the death rate of the open interval, which the limits by formula do not, and are
        /// the wider for it; the draws are not the same from one run to the next.
        /// </summary>
        /// <param name="host">The preferences, and where progress is shown.</param>
        /// <param name="parameters">"intervals": the lengths of the intervals but the last, which is open; "population" and "deaths": a
        /// row for every interval, the open one among them; "fractions" (may be left out): the fraction of each interval that those who
        /// die in it live through, which if left out is a half, but 0.1 for a first interval of no more than 1 year and 0.4 for a
        /// second of no more than 5; "weights" (may be left out): a weight for each interval, by which the expectation of life at its
        /// start is multiplied; "gamma", the confidence level; "iterations", the number of tables to simulate, 3000 at the least;
        /// "save", whether the table is to be saved.</param>
        public static StepOutput RptAbridgedLifetable(IPreferencesAndProgressBar host, ParameterBag parameters)
        {
            string uti = null;

            bool util;

            // string lifetab = "Life table"; 
            double gamma = parameters["gamma"].AsDouble;
            double p0 = (1.0 - gamma) / 2.0;
            double cit = PDF.gauinv(1.0 - p0);

            // data that a table cannot be made from are refused, with a message that says what is wrong
            AbridgedLifetableCheck(parameters);

            DataFrame intervalsFrame = parameters["intervals"].AsDataFrame;
            DoubleVariable intervalsVariable = intervalsFrame.Variables[0]as DoubleVariable;
            int rows = intervalsVariable.Length + 1;
            double[] r = new double[rows + 1];
            double[] x = new double[rows + 1];
            double[] p = new double[rows + 1];
            double[] d = new double[rows + 1];
            double[] a = new double[rows + 1];
            double[] rm = new double[rows + 1];
            double[] q = new double[rows + 1];
            double[] dd = new double[rows + 1];
            double[] sl = new double[rows + 1];
            double[] yl = new double[rows + 1];
            double[] e = new double[rows + 1];
            double[] t = new double[rows + 1];
            double[] vq = new double[rows + 1];
            double[] ve = new double[rows + 1];
            double[] vs = new double[rows + 1];
            double[] u = new double[rows + 1 ];
            x[1] = 0.0;
            for (int i = 1; i < rows; i++)
            {
                r[i] = intervalsVariable.Data[i - 1];
                if (i < rows)
                    x[i + 1] = x[i] + r[i];
            }

            DataFrame populationFrame = parameters["population"].AsDataFrame;
            DoubleVariable populationVariable = populationFrame.Variables[0]as DoubleVariable;
            for (int i = 1; i <= rows; i++)
                p[i] = populationVariable.Data[i - 1];

            DataFrame deathsFrame = parameters["deaths"].AsDataFrame;
            DoubleVariable deathsVariable = deathsFrame.Variables[0]as DoubleVariable;
            bool novariance = false;
            for (int i = 1; i <= rows; i++)
            {
                d[i] = Math.Abs(deathsVariable.Data[i - 1]);
                if (d[i] <= 0.0)
                    novariance = true;
            }

            if (parameters.ContainsKey("fractions") && parameters["fractions"] != null)
            {
                DataFrame fractionsFrame = parameters["fractions"].AsDataFrame;
                DoubleVariable fractionsVariable = fractionsFrame.Variables[0]as DoubleVariable;
                for (int i = 1; i < rows; i++)
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
                DoubleVariable weightsVariable = weightsFrame.Variables[0]as DoubleVariable;
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

            bool saveDetails = parameters["save"].AsBoolean;

            // a death rate too high for its interval (a n M > 1) would give a probability of dying above 1 and a negative cohort
            AbridgedLifetableBasics(rows, d, p, a, sl, rm, r, q, dd, yl, t, e);
            for (int i = 1; i < rows; i++)
            {
                if (q[i] > 1.0)
                    throw new TemplateOperationCancelledException("The probability of dying in the interval " + LifetabInterval(i, rows, x) + " would be greater than 1: check its deaths, population and length.", "Abridged life table");
            }

            // simulation
            double[] dsim = new double[rows + 1 ];
            double[] esim = new double[simits + 1 ];
            double[] emdsim = new double[simits + 1 ];
            double emd;
            PoissonRNG rng = new();
            //  RNG.Seed(DefaultSeed()) not required as the default seed is used if the RNG isn't seeded on first call
            using (IProgressBar progress = host.StartProgress("Simulating...", true))
            {
                for (int j = 1; j <= simits; j++)
                {
                    if (progress.Update(j / (double)simits))
                        throw new TemplateOperationCancelledException();

                    for (int i = 1; i <= rows; i++)
                        dsim[i] = rng.GenPoisson(d[i]);
                    AbridgedLifetableBasics(rows, dsim, p, a, sl, rm, r, q, dd, yl, t, e);
                    esim[j] = e[1];
                    AbridgedLifetableMedianMode(rows, dd, out _, out emd, sl, x);
                    emdsim[j] = emd;
                }
            }
            // the limits at the chosen level
            Array.Sort(esim, 1, simits);
            double esimll = MathDbl.QuantileFromSorted(esim, simits, p0);
            double esimul = MathDbl.QuantileFromSorted(esim, simits, 1.0 - p0);
            Array.Sort(emdsim, 1, simits);
            double emdsimll = MathDbl.QuantileFromSorted(emdsim, simits, p0);
            double emdsimul = MathDbl.QuantileFromSorted(emdsim, simits, 1.0 - p0);

            // basic stats for abridged life table
            AbridgedLifetableBasics(rows, d, p, a, sl, rm, r, q, dd, yl, t, e);

            // Chiang variances
            if (!novariance)
            {
                AbridgedLifetableVariance(rows, vq, q, d, a, r, e, sl, ve, vs);
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
            AbridgedLifetableMedianMode(rows, dd, out _, out emd, sl, x);

            // population, deaths, death rate
            ParameterBag outputParameters = new();
            IList<ParameterBag> inputsList = new List<ParameterBag>();
            outputParameters.AddOutput("*inputs", inputsList);
            for (int i = 1; i <= rows; i++)
            {
                ParameterBag inputsParameters = new();
                inputsList.Add(inputsParameters);
                inputsParameters.AddOutput("int", LifetabInterval(i, rows, x));
                inputsParameters.AddOutput("pop", p[i]);
                inputsParameters.AddOutput("dead", d[i]);
                inputsParameters.AddOutput("rate", rm[i]);
            }

            // probability of dying q, se, ci
            outputParameters.AddOutput("pc", gamma * 100);

            IList<ParameterBag> pdyingList = new List<ParameterBag>();
            outputParameters.AddOutput("*pdying", pdyingList);
            for (int i = 1; i <= rows; i++)
            {
                ParameterBag pdyingParameters = new();
                pdyingList.Add(pdyingParameters);
                pdyingParameters.AddOutput("int", LifetabInterval(i, rows, x));
                pdyingParameters.AddOutput("q", q[i]);
                double se, lci, uci;
                if (vq[i] < 0.0 || vq[i] == Constant.MISSING)
                {
                    se = Constant.MISSING;
                    lci = Constant.MISSING;
                    uci = Constant.MISSING;
                }
                else
                {
                    se = Math.Sqrt(vq[i]);
                    lci = q[i] - cit * se;
                    uci = q[i] + cit * se;
                }
                pdyingParameters.AddOutput("se", se);
                pdyingParameters.AddOutput("lci", lci);
                pdyingParameters.AddOutput("uci", uci);
            }

            // numbers living, dying from a standard population of usually 100k, fraction of last interval of life a
            IList<ParameterBag> livingList = new List<ParameterBag>();
            outputParameters.AddOutput("*living", livingList);
            for (int i = 1; i <= rows; i++)
            {
                ParameterBag livingParameters = new();
                livingList.Add(livingParameters);
                livingParameters.AddOutput("int", LifetabInterval(i, rows, x));
                livingParameters.AddOutput("l", Convert.ToInt32(sl[i]).ToString());
                livingParameters.AddOutput("d", Convert.ToInt32(dd[i]).ToString());
                livingParameters.AddOutput("a", a[i]);
            }

            // years in interval, years beyond age x(i)
            IList<ParameterBag> yearsList = new List<ParameterBag>();
            outputParameters.AddOutput("*years", yearsList);
            for (int i = 1; i <= rows; i++)
            {
                ParameterBag yearsParameters = new();
                yearsList.Add(yearsParameters);
                yearsParameters.AddOutput("int", LifetabInterval(i, rows, x));
                yearsParameters.AddOutput("L", Convert.ToInt32(yl[i]).ToString());
                yearsParameters.AddOutput("T", Convert.ToInt32(t[i]).ToString());
            }

            // expectation of life e, se, ci
            IList<ParameterBag> expectationList = new List<ParameterBag>();
            outputParameters.AddOutput("*expectation", expectationList);
            for (int i = 1; i <= rows; i++)
            {
                ParameterBag expectationParameters = new();
                expectationList.Add(expectationParameters);
                expectationParameters.AddOutput("int", LifetabInterval(i, rows, x));
                expectationParameters.AddOutput("e", e[i]);
                double se, lci, uci;
                if (ve[i] < 0.0 || i == rows || ve[i] == Constant.MISSING)
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
                expectationParameters.AddOutput("se", se);
                expectationParameters.AddOutput("lci", lci);
                expectationParameters.AddOutput("uci", uci);
            }

            // healthy life expectancy
            if (util)
            {
                IList<ParameterBag> utilList = new List<ParameterBag>();
                outputParameters.AddOutput("*util", utilList);
                ParameterBag utilParameters = new();
                utilList.Add(utilParameters);
                utilParameters.AddOutput("uti", uti);
                IList<ParameterBag> adjustedList = new List<ParameterBag>();
                utilParameters.AddOutput("*adjusted", adjustedList);
                for (int i = 1; i <= rows; i++)
                {
                    ParameterBag adjustedParameters = new();
                    adjustedList.Add(adjustedParameters);
                    adjustedParameters.AddOutput("int", LifetabInterval(i, rows, x));
                    double eh;
                    if (sl[i] == 0.0 || sl[i] == Constant.MISSING)
                        eh = Constant.MISSING;
                    else
                        eh = u[i] * t[i] / sl[i];
                    adjustedParameters.AddOutput("eh", eh);
                }
            }
            else
            {
                outputParameters.AddOutput("*util", null);
            }

            // a median beyond the table is printed as more than the start of the open interval (two infinite order statistics interpolate to NaN)
            void AddMedian(string name, double value)
            {
                if (double.IsPositiveInfinity(value) || double.IsNaN(value))
                    outputParameters.AddOutput(name, "more than " + LifetabAge(x[rows]));
                else
                    outputParameters.AddOutput(name, value);
            }
            AddMedian("med", emd);
            outputParameters.AddOutput("its", simits);
            AddMedian("med_lci", emdsimll);
            AddMedian("med_uci", emdsimul);

            outputParameters.AddOutput("elb", e[1]);
            {
                double lci, uci;
                if (ve[1] < 0.0 || ve[1] == Constant.MISSING)
                {
                    lci = Constant.MISSING;
                    uci = Constant.MISSING;
                }
                else
                {
                    double se = Math.Sqrt(ve[1]);
                    lci = e[1] - cit * se;
                    uci = e[1] + cit * se;
                }
                outputParameters.AddOutput("elb_lci", lci);
                outputParameters.AddOutput("elb_uci", uci);
            }
            outputParameters.AddOutput("elb_mc_lci", esimll);
            outputParameters.AddOutput("elb_mc_uci", esimul);

            if (saveDetails)
            {
                DataFrame resultsFrame = new();
                string g = Formatting.XRound(gamma * 100, 1);
                StringVariable intervalVariable = new(rows, "Interval");
                DoubleVariable qHatVariable = new(rows, "Prob of dying [q hat]");
                DoubleVariable varQVariable = new(rows, "Var [q]");
                DoubleVariable lciQVariable = new(rows, g + "% LCI [q]");
                DoubleVariable uciQVariable = new(rows, g + "% UCI [q]");
                DoubleVariable lVariable = new(rows, "Alive at start [l]");
                DoubleVariable dVariable = new(rows, "Dying in interval [d]");
                DoubleVariable fractionAVariable = new(rows, "Fraction a");
                DoubleVariable ylVariable = new(rows, "Years in interval [L]");
                DoubleVariable tVariable = new(rows, "Years beyond [T]");
                DoubleVariable eVariable = new(rows, "Expectation of life [e]");
                DoubleVariable varEVariable = new(rows, "Var [e]");
                DoubleVariable lciEVariable = new(rows, g + "% LCI [e]");
                DoubleVariable uciEVariable = new(rows, g + "% UCI [e]");
                DoubleVariable aEVariable = new(rows, "Adj. expectn. of life [Ae]");

                resultsFrame.Variables.Add(intervalVariable);
                resultsFrame.Variables.Add(qHatVariable);
                resultsFrame.Variables.Add(varQVariable);
                resultsFrame.Variables.Add(lciQVariable);
                resultsFrame.Variables.Add(uciQVariable);
                resultsFrame.Variables.Add(lVariable);
                resultsFrame.Variables.Add(dVariable);
                resultsFrame.Variables.Add(fractionAVariable);
                resultsFrame.Variables.Add(ylVariable);
                resultsFrame.Variables.Add(tVariable);
                resultsFrame.Variables.Add(eVariable);
                resultsFrame.Variables.Add(varEVariable);
                resultsFrame.Variables.Add(lciEVariable);
                resultsFrame.Variables.Add(uciEVariable);
                if (util)
                    resultsFrame.Variables.Add(aEVariable);

                for (int i = 1; i <= rows; i++)
                {
                    intervalVariable.SetData(i - 1, LifetabInterval(i, rows, x));
                    qHatVariable.SetData(i - 1, q[i]);
                    varQVariable.SetData(i - 1, vq[i]);
                    double lci, uci;
                    if (vq[i] != Constant.MISSING && vq[i] >= 0.0)
                    {
                        lci = q[i] - Math.Sqrt(vq[i]) * cit;
                        uci = q[i] + Math.Sqrt(vq[i]) * cit;
                    }
                    else
                    {
                        lci = Constant.MISSING;
                        uci = Constant.MISSING;
                    }
                    lciQVariable.SetData(i - 1, lci);
                    uciQVariable.SetData(i - 1, uci);
                    lVariable.SetData(i - 1, sl[i]);
                    dVariable.SetData(i - 1, dd[i]);
                    fractionAVariable.SetData(i - 1, a[i]);
                    ylVariable.SetData(i - 1, yl[i]);
                    tVariable.SetData(i - 1, t[i]);
                    eVariable.SetData(i - 1, e[i]);
                    varEVariable.SetData(i - 1, ve[i]);
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
                    lciEVariable.SetData(i - 1, lci);
                    uciEVariable.SetData(i - 1, uci);
                    if (util)
                    {
                        double eh;
                        if (sl[i] == 0.0 || sl[i] == Constant.MISSING)
                            eh = Constant.MISSING;
                        else
                            eh = u[i] * t[i] / sl[i];
                        aEVariable.SetData(i - 1, eh);
                    }
                }
                outputParameters.AddOutput("results", resultsFrame);
            }
            return new StepOutput(outputParameters);
        }

        /// <summary>
        /// The least number that can have been alive at the start of a follow-up life table: the deaths and withdrawals of all its rows.
        /// The dialog box offers it as the number alive at the start, and takes no less.
        /// </summary>
        /// <param name="parameters">"times", "deaths" and "withdrawals".</param>
        public static StepOutput RptFollowUpLifetableCalculateNatst(ParameterBag parameters)
        {
            DoubleVariable timesVariable = parameters["times"].AsDataFrame.Variables[0] as DoubleVariable;
            DoubleVariable deathsVariable = parameters["deaths"].AsDataFrame.Variables[0] as DoubleVariable;
            DoubleVariable withdrawalsVariable = parameters["withdrawals"].AsDataFrame.Variables[0] as DoubleVariable;

            // the least number alive at the start: the deaths and withdrawals of the rows the table uses, those with a value in all three columns
            double natst = 0.0;
            for (int r = 0; r < timesVariable.Length; r++)
                if (timesVariable.Data[r] != Constant.MISSING && deathsVariable.Data[r] != Constant.MISSING && withdrawalsVariable.Data[r] != Constant.MISSING)
                    natst += deathsVariable.Data[r] + withdrawalsVariable.Data[r];

            ParameterBag outputParameters = new();
            outputParameters.AddOutput("natst-min", natst);
            return new StepOutput(outputParameters);
        }

        /// <summary>
        /// The follow-up (actuarial) life table of a group of subjects followed from a common start: for each interval of time the
        /// deaths and the number withdrawn.  Those withdrawn in an interval are taken to have been at risk for half of it, so that the
        /// adjusted number at risk is the number alive at the start of the interval less half of the number withdrawn; the probability
        /// of death in the interval is the deaths over that, and the proportion surviving to the end of an interval is the product of
        /// the probabilities of survival of the intervals up to it.  Its variance is by Greenwood's formula, and its confidence limits
        /// are from those of log(-log) of the proportion, so that they lie between 0 and 100 per cent.
        /// </summary>
        /// <param name="parameters">"times": the time at which each interval starts; "deaths" and "withdrawals": the numbers in each
        /// interval; "natst": the number alive at the start; "gamma": the confidence level.</param>
        public static StepOutput RptFollowUpLifetable(ParameterBag parameters)
        {
            double gamma = parameters["gamma"].AsDouble;
            double p = (1.0 - gamma) / 2.0;
            double cit = PDF.gauinv(1.0 - p);

            DataFrame timesFrame = parameters["times"].AsDataFrame;
            DoubleVariable timesVariable = timesFrame.Variables[0]as DoubleVariable;
            int rows = timesVariable.Length;
            ColumnData[] td = new ColumnData[2 + 1];
            double[] t = new double[rows + 1];
            td[0] = new ColumnData { Title = timesVariable.Title };

            for (int r = 1; r <= rows; r++)
                t[r] = timesVariable.Data[r - 1];

            DataFrame deathsFrame = parameters["deaths"].AsDataFrame;
            DoubleVariable deathsVariable = deathsFrame.Variables[0]as DoubleVariable;
            double[] d = new double[rows + 1];
            td[1] = new ColumnData { Title = deathsVariable.Title };

            for (int r = 1; r <= rows; r++)
                d[r] = deathsVariable.Data[r - 1];

            DataFrame withdrawalsFrame = parameters["withdrawals"].AsDataFrame;
            DoubleVariable withdrawalsVariable = withdrawalsFrame.Variables[0]as DoubleVariable;
            double[] w = new double[rows + 1];
            td[2] = new ColumnData { Title = withdrawalsVariable.Title };

            for (int r = 1; r <= rows; r++)
                w[r] = withdrawalsVariable.Data[r - 1];

            double natst = parameters["natst"].AsDouble;

            // The rows with a value in all three columns, in order of their (whole) starting times; the counts are kept as entered
            double[] tm = new double[rows + 1];
            int[] row = new int[rows + 1];
            int nx = 0;
            for (int j = 1; j <= rows; j++)
            {
                if (t[j] != Constant.MISSING && d[j] != Constant.MISSING && w[j] != Constant.MISSING)
                {
                    nx++;
                    tm[nx] = Math.Floor(t[j]);
                    row[nx] = j;
                }
            }
            if (nx == 0)
                throw new TemplateOperationCancelledException("There are no rows with a time, a number of deaths and a number withdrawn.", "Follow-up life table");
            double leaving = 0.0;
            for (int j = 1; j <= nx; j++)
            {
                if (d[row[j]] < 0.0 || w[row[j]] < 0.0)
                    throw new TemplateOperationCancelledException("A number of deaths or of withdrawals cannot be below zero: see row " + row[j] + ".", "Follow-up life table");
                leaving += d[row[j]] + w[row[j]];
            }
            if (natst < leaving)
                throw new TemplateOperationCancelledException("The number alive at the start, " + natst + ", is less than the deaths and withdrawals of the table, " + leaving + ".", "Follow-up life table");
            Array.Sort(tm, row, 1, nx);
            // Rows with the same starting time, including the last row, are one interval
            double[] tt = new double[nx + 1];
            double[] dd = new double[nx + 1];
            double[] ww = new double[nx + 1];
            int nt = 0;
            for (int j = 1; j <= nx; j++)
            {
                if (j == 1 || tm[j] != tm[j - 1])
                {
                    nt++;
                    tt[nt] = tm[j];
                }
                dd[nt] += d[row[j]];
                ww[nt] += w[row[j]];
            }
            t = tt;
            d = dd;
            w = ww;
            ParameterBag outputParameters = new();
            // the report says how many rows were left out for a blank cell
            if (nx < rows)
            {
                IList<ParameterBag> noteList = new List<ParameterBag>();
                ParameterBag noteParameters = new();
                noteParameters.AddOutput("note", rows - nx == 1 ? "1 row with a blank cell was left out" : (rows - nx).ToString() + " rows with a blank cell were left out");
                noteList.Add(noteParameters);
                outputParameters.AddOutput("*note", noteList);
            }
            else
            {
                outputParameters.AddOutput("*note", null);
            }
            double cump = 1.0;
            double var1 = 0.0;
            double natr = natst;
            double[] xcump = new double[nt + 1];
            double[] xvar = new double[nt + 1];
            double[] xp = new double[nt + 1];
            IList<ParameterBag> deathsList = new List<ParameterBag>();
            outputParameters.AddOutput("*deaths", deathsList);
            for (int j = 1; j <= nt; j++)
            {
                double en1 = natr - w[j] / 2.0;
                // en1 is the adjusted number at risk, q the probability of death in the interval and p that of survival; var1 is the
                // sum of q / (en1 p) over the intervals so far, and the variance of the proportion surviving is its square times var1
                // an interval that nobody is left to enter has no deaths, and leaves the survivors as they were
                double q = en1 > 0.0 ? d[j] / en1 : 0.0;
                p = 1.0 - q;
                cump = p * cump;
                if (p * en1 != 0.0)
                    var1 += q / (en1 * p);
                double var = cump * cump * var1;
                xp[j] = p;
                xcump[j] = cump;
                xvar[j] = var;
                ParameterBag deathsParameters = new();
                deathsList.Add(deathsParameters);
                string xx;
                if (j < nt)
                    xx = Convert.ToInt32(t[j]).ToString() + " to " + Convert.ToInt32(t[j + 1]).ToString();
                else
                    xx = Convert.ToInt32(t[j]).ToString() + " up";
                deathsParameters.AddOutput("int", xx);
                deathsParameters.AddOutput("death", d[j]);
                deathsParameters.AddOutput("wdrawn", w[j]);
                deathsParameters.AddOutput("risk", natr);
                if (j < nt)
                {
                    deathsParameters.AddOutput("nx", en1);
                    deathsParameters.AddOutput("q", q);
                }
                else
                {
                    deathsParameters.AddOutput("nx", Constant.MISSING);
                    deathsParameters.AddOutput("q", Constant.MISSING);
                }
                natr = natr - d[j] - w[j];
            }
            outputParameters.AddOutput("pc", gamma * 100);

            IList<ParameterBag> survivalList = new List<ParameterBag>();
            outputParameters.AddOutput("*survival", survivalList);

            ParameterBag survivalParameters = new();
            survivalList.Add(survivalParameters);
            // with a single interval the table has only the open interval
            survivalParameters.AddOutput("int", nt > 1 ? Convert.ToInt32(t[1]) + " to " + Convert.ToInt32(t[2]) : Convert.ToInt32(t[1]) + " up");
            survivalParameters.AddOutput("p", nt > 1 ? xp[1] : Constant.MISSING);
            survivalParameters.AddOutput("lx", 100.0);
            survivalParameters.AddOutput("var", Constant.MISSING);
            survivalParameters.AddOutput("lci", Constant.MISSING);
            survivalParameters.AddOutput("uci", Constant.MISSING);
            for (int j = 1; j < nt; j++)
            {
                cump = xcump[j];
                double var = xvar[j];
                double sd;
                double lc;
                double uc;
                if (var > 0.0)
                {
                    // the SD of lx% is Greenwood's; the limits are on the log(-log) scale
                    sd = 100.0 * Math.Sqrt(var);
                    double s = Math.Sqrt(var) / (-cump * Math.Log(cump));
                    lc = 100.0 * Math.Pow(cump, Math.Exp(cit * s));
                    uc = 100.0 * Math.Pow(cump, Math.Exp(-cit * s));
                }
                else
                {
                    sd = Constant.MISSING;
                    lc = Constant.MISSING;
                    uc = Constant.MISSING;
                }
                survivalParameters = new ParameterBag();
                survivalList.Add(survivalParameters);
                string xx = j < nt - 1
                    ? Convert.ToInt32(t[j + 1]).ToString() + " to " + Convert.ToInt32(t[j + 2]).ToString()
                    : Convert.ToInt32(t[j + 1]).ToString() + " up";
                survivalParameters.AddOutput("int", xx);
                survivalParameters.AddOutput("p", j < nt - 1 ? xp[j + 1] : Constant.MISSING);
                survivalParameters.AddOutput("lx", 100.0 * cump);
                survivalParameters.AddOutput("var", sd);
                survivalParameters.AddOutput("lci", lc);
                survivalParameters.AddOutput("uci", uc);
            }
            return new StepOutput(outputParameters);
        }

        /// <summary>
        /// The log-rank test and a generalised Wilcoxon test of the difference in survival between two or more groups, within strata if
        /// there are any.  At each time the deaths expected in a group are the deaths at that time times the share of the group among
        /// those at risk.  The rank statistic of a group is the sum over the times of a weight times (the deaths observed in it less
        /// the deaths expected), and the variances and covariances of the statistics are those of the hypergeometric distribution of
        /// the deaths among the groups, times the square of the weight.  The chi-square is the quadratic form of the statistics of all
        /// the groups but the last in the inverse of their matrix, on one degree of freedom fewer than there are groups.  With more
        /// than two groups there is also a test for trend with the scores of the groups.  With strata the statistics and the matrices
        /// are added up over the strata and the tests made again.
        /// The hazard ratio of two groups is the ratio of their deaths observed over deaths expected, with approximate limits; for two
        /// groups without strata there is also the hazard ratio by conditional maximum likelihood, with exact limits and P values.
        /// </summary>
        /// <param name="host">Where progress is shown.</param>
        /// <param name="parameters">"gid", "times", "deaths" (the codes) and, if there are strata, "strata"; "gamma", the confidence level;
        /// "wt_method", the weights of the Wilcoxon test (1 Peto-Prentice, 2 Gehan-Breslow, 3 Tarone-Ware); with more than two groups
        /// "group_scores".</param>
        public static StepOutput RptLogRank(IProgressBarHost host, ParameterBag parameters)
        {
            Petoprep(parameters, out int nt, out double gamma, out double cit, out int groups, out int strata, out double[] score, out string gid, out double[] gpid, out string[] glab, out string[] slab, out bool ifault, out double[,] arr2, out _);
            if (ifault)
                throw new TemplateOperationCancelledException();

            int wtMethod = Parsing.Cint_Txt(parameters["wt_method"].AsString);

            ParameterBag outputParameters = new();
            IList<ParameterBag> outerList = new List<ParameterBag>();
            outputParameters.AddOutput("*outer", outerList);
            // the report says how many records were left out for a blank cell: each has one row of arr2, as its code is not above 1
            int leftOut = 0;
            for (int j = 1; j <= nt; j++)
                if (arr2[0, j] == Constant.MISSING || arr2[1, j] == Constant.MISSING || arr2[2, j] == Constant.MISSING || (strata != 0 && arr2[3, j] == Constant.MISSING))
                    leftOut++;
            if (leftOut > 0)
            {
                IList<ParameterBag> noteList = new List<ParameterBag>();
                ParameterBag noteParameters = new();
                noteParameters.AddOutput("note", leftOut == 1 ? "1 record with a blank cell was left out" : leftOut.ToString() + " records with a blank cell were left out");
                noteList.Add(noteParameters);
                outputParameters.AddOutput("*note", noteList);
            }
            else
            {
                outputParameters.AddOutput("*note", null);
            }
            double[] tesum = new double[groups + 1];
            int[] tdg = new int[groups + 1];
            Trisvar[] q = new Trisvar[nt + 2];
            double[,] vsuml = new double[groups + 1, groups + 1];
            double[] u0Suml = new double[groups + 1];
            double[,] vsumw = new double[groups + 1, groups + 1];
            double[] u0Sumw = new double[groups + 1];
            int stratum = 0;
            do
            {
                // stratum loop
                if (strata != 0)
                    stratum += 1;
                int[] ng = new int[groups + 1];
                int[] dg = new int[groups + 1];
                int ntx = 0;
                for (int j = 1; j <= nt; j++)
                {
                    if ((strata == 0 || arr2[3, j] == stratum) && arr2[1, j] != Constant.MISSING && arr2[2, j] != Constant.MISSING && arr2[0, j] != Constant.MISSING)
                    {
                        ntx += 1;
                        q[ntx] = new Trisvar
                        {
                            Tm = arr2[1, j],
                            Cs = Convert.ToInt32(arr2[2, j]),
                            Gp = Convert.ToInt32(arr2[0, j])
                        };
                        ng[q[ntx].Gp] = ng[q[ntx].Gp] + 1;
                        if (q[ntx].Cs == 1)
                            dg[q[ntx].Gp] = dg[q[ntx].Gp] + 1;
                    }
                }
                Array.Sort(q, 1, ntx, new TrisvarByTm());
                int test = 0;
                // logrank then Wilcoxon test loop
                do
                {
                    test += 1;
                    int[] drop = new int[groups + 1];
                    int[] rg = new int[groups + 1];
                    int[] dead = new int[groups + 1];
                    double[,] v = new double[groups + 1, groups + 1];
                    double[] u0 = new double[groups + 1];
                    double[] esum = new double[groups + 1];
                    Rec2X2[] tbl = null;
                    if (groups == 2 && test == 1)
                    {
                        tbl = new Rec2X2[ntx + 1];
                    } // exact 2x2 test

                    for (int j = 1; j <= groups; j++)
                        rg[j] = ng[j];
                    double sv = 1.0;
                    int ne = 0;
                    for (int j = 1; j <= ntx; j++)
                    {
                        //  total number at risk = risktot for time J
                        double risktot = 0;
                        for (int j2 = 1; j2 <= groups; j2++)
                            risktot += rg[j2];
                        drop[q[j].Gp] = 1;
                        dead[q[j].Gp] = q[j].Cs;
                        int totd = q[j].Cs;
                        //  deaths at time J
                        int n = j;
                        do
                        {
                            if (n >= ntx)
                                break;
                            if (q[n].Tm == q[n + 1].Tm)
                            {
                                drop[q[n + 1].Gp] = drop[q[n + 1].Gp] + 1;
                                if (q[n + 1].Cs != 0)
                                {
                                    dead[q[n + 1].Gp] = dead[q[n + 1].Gp] + 1;
                                    totd += 1;
                                }
                                n += 1;
                            }
                            else
                            {
                                break;
                            }
                        }
                        while (true);
                        double deadx = totd;
                        for (int j2 = 1; j2 <= groups; j2++)
                        {
                            double jrisk = rg[j2];
                            double jprop = jrisk / risktot;
                            double expect = deadx * jprop;
                            esum[j2] = esum[j2] + expect;
                            // The weight: 1 for the log-rank test.  For the Wilcoxon test, by Peto and Prentice an estimate of the
                            // proportion surviving the time: the product, over the times up to and with this one, of
                            // (n - d + 1) / (n + 1), which is sv, the product over the earlier times, times the factor of this time;
                            // by Gehan and Breslow the number at risk, here over the number of records plus 1, which makes no
                            // difference to the test; by Tarone and Ware the square root of the number at risk.  n is the number at
                            // risk in all the groups and d the deaths at the time.
                            double wt = 0;
                            if (test == 2)
                            {
                                switch (wtMethod)
                                {
                                    case 1:
                                        wt = sv * (risktot - deadx + 1.0) / (risktot + 1.0);
                                        break;
                                    case 2:
                                        wt = risktot / (ntx + 1);
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
                            // the variance of the deaths in group j is n j (n - n j) d (n - d) / (n^2 (n - 1)), and the covariance of
                            // those in groups j and k is -n j n k d (n - d) / (n^2 (n - 1))
                            u0[j2] = u0[j2] + wt * (dead[j2] - expect);
                            for (int k = 1; k <= groups; k++)
                            {
                                double krisk = rg[k];
                                if (risktot > 1)
                                    v[j2, k] = k == j2
                                        ? v[j2, k] + wt * wt * (jrisk * (risktot - jrisk) * deadx * (risktot - deadx)) / (risktot * risktot * (risktot - 1.0))
                                        : v[j2, k] + wt * wt * -(jrisk * krisk * deadx * (risktot - deadx)) / (risktot * risktot * (risktot - 1.0));
                            }
                        }
                        // exact test for 2 groups - a table for each unique survival time
                        // the 2 by 2 table of a time: the deaths in the first group (A) of all the deaths at the time (M1), and the
                        // numbers at risk in the two groups (N1 and N0).  A table tells nothing if all at risk, or none, die
                        if (groups == 2 & test == 1)
                        {
                            if (j == 1)
                            {
                                ne = j;
                            }
                            else
                            {
                                if (q[j].Tm == q[j - 1].Tm)
                                {
                                    // skip = true; 
                                }
                                else
                                {
                                    ne += 1;
                                }
                            }
                            tbl[ne].A = dead[1];
                            tbl[ne].M1 = dead[1] + dead[2];
                            tbl[ne].N1 = rg[1];
                            tbl[ne].N0 = rg[2];
                            tbl[ne].Freq = 1;
                            tbl[ne].IsInformative = (dead[1] * (rg[2] - dead[2]) != 0) || (dead[2] * (rg[1] - dead[1]) != 0);
                        }
                        for (int j2 = 1; j2 <= groups; j2++)
                        {
                            rg[j2] = rg[j2] - drop[j2];
                            drop[j2] = 0;
                            dead[j2] = 0;
                        }
                        j = n;
                        // survivor function - needed for Peto-Prentice weights
                        sv = sv * (risktot - totd + 1.0) / (risktot + 1);
                    }
                    // invert the first groups-1 elements of the v matrix
                    double[,] vtemp = new double[groups, 1 + 1];
                    double[,] vinv = new double[groups + 1, groups + 1];
                    // save v for summing later if stratified
                    for (int j2 = 1; j2 <= groups; j2++)
                        for (int k = 1; k <= groups; k++)
                            vinv[j2, k] = v[j2, k];
                    int imfault = 0;
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
                        for (int j2 = 1; j2 < groups; j2++)
                            for (int k = 1; k < groups; k++)
                                vtemp[j2, 1] = vtemp[j2, 1] + vinv[j2, k] * u0[k];
                        // multiply tran(U0)*inv(V) by U0 to get the test statistic
                        x2 = 0.0;
                        for (int k = 1; k < groups; k++)
                            x2 += vtemp[k, 1] * u0[k];
                    }
                    //  trend statistic (c'U0)^2 / c'Vc
                    double x2Den;
                    double x2Num;
                    double x2T = 0;
                    if (groups > 2)
                    {
                        x2Num = 0.0;
                        for (int k = 1; k <= groups; k++)
                            x2Num += u0[k] * score[k];
                        x2Num *= x2Num;
                        vtemp = new double[groups + 1, 1 + 1];
                        x2Den = 0.0;
                        for (int j2 = 1; j2 <= groups; j2++)
                            for (int k = 1; k <= groups; k++)
                                vtemp[j2, 1] = vtemp[j2, 1] + v[j2, k] * score[k];
                        for (int k = 1; k <= groups; k++)
                            x2Den += vtemp[k, 1] * score[k];
                        x2T = x2Num / x2Den;
                    }
                    double p2M = 0; double p1M = 0; double p2F = 0; double p1F = 0; double llm = 0; double ulm = 0;
                    double llf = 0; double ulf = 0; double hr = 0;
                    if (test == 1 && groups == 2)
                    {
                        // exact test
                        bool useLogScale = false;
                        new ExactBB().Exact22K(host, 1, ne, Exact22KDataType.Type4, tbl, gamma, out hr, out ulf, out llf, out ulm, out llm, out p1F, out p2F, out p1M, out p2M, ref useLogScale, out int ierr);
                        if (ierr != 0)
                        {
                            hr = Constant.MISSING;
                            ulf = Constant.MISSING;
                            llf = Constant.MISSING;
                            ulm = Constant.MISSING;
                            llm = Constant.MISSING;
                            p1F = Constant.MISSING;
                            p2F = Constant.MISSING;
                            p1M = Constant.MISSING;
                            p2M = Constant.MISSING;
                        }
                    }
                    // Start of data output
                    ParameterBag outerParameters = new();
                    outerList.Add(outerParameters);
                    string testname;
                    if (test == 1)
                    {
                        testname = "Log-rank (Peto)";
                    }
                    else
                    {
                        string zx = null;
                        switch (wtMethod)
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

                        testname = $"Generalised Wilcoxon ({zx})";
                    }
                    outerParameters.AddOutput("title", testname);
                    if (strata != 0)
                        outerParameters.AddOutput("strata", $" * [STRATUM {stratum} of {strata}: {slab[stratum]}]");
                    double rr;
                    if (test == 1)
                    {
                        //  more detail with log-rank test
                        IList<ParameterBag> groupsList = new List<ParameterBag>();
                        outerParameters.AddOutput("*groups", groupsList);
                        for (int j = 1; j <= groups; j++)
                        {
                            ParameterBag groupsParameters = new();
                            groupsList.Add(groupsParameters);
                            groupsParameters.AddOutput("grp", $"{j} ({gid} = {glab[Convert.ToInt32(gpid[j])]})");
                            groupsParameters.AddOutput("obs", dg[j]);
                            groupsParameters.AddOutput("ext", esum[j]);
                            tesum[j] += esum[j];
                            tdg[j] += dg[j];
                            rr = esum[j] <= 0
                                ? Constant.MISSING
                                : dg[j] / esum[j];
                            groupsParameters.AddOutput("rel", rr);
                        }
                    }
                    else
                    {
                        //  plain matrix and statistic output with Wilcoxon
                        outerParameters.AddOutput("*groups", null);
                    }
                    //  test statistics and variance-covariance matrix
                    IList<ParameterBag> rankList = new List<ParameterBag>();
                    outerParameters.AddOutput("*rank", rankList);
                    for (int j = 1; j <= groups; j++)
                        rankList.Add(new ParameterBag("cell", new FilledDoubleParameter(FilledParameterDirection.Output, u0[j])));

                    IList<ParameterBag> covarList = new List<ParameterBag>();
                    outerParameters.AddOutput("*covar", covarList);
                    for (int j = 1; j <= groups; j++)
                    {
                        IList<ParameterBag> matList = new List<ParameterBag>();
                        covarList.Add(new ParameterBag("*mat", new FilledParameterBagListParameter(FilledParameterDirection.Output, matList)));
                        for (int j2 = 1; j2 <= groups; j2++)
                            matList.Add(new ParameterBag("cell", new FilledDoubleParameter(FilledParameterDirection.Output, v[j2, j]))); // v, not vinv: the leading block of vinv has been inverted in place above
                    }
                    //  test this stratum or whole
                    outerParameters.AddOutput("chi", x2);
                    if (x2 != Constant.MISSING)
                        outerParameters.AddOutput("p", PDF.chivalp(x2, groups - 1));
                    if (groups > 2)
                    {
                        IList<ParameterBag> trendsList = new List<ParameterBag>();
                        outerParameters.AddOutput("*trends", trendsList);
                        ParameterBag trendsParameters = new();
                        trendsList.Add(trendsParameters);
                        trendsParameters.AddOutput("trend", x2T);
                        trendsParameters.AddOutput("p_trend", PDF.chivalp(x2T, 1.0));
                    }
                    else
                    {
                        outerParameters.AddOutput("*trends", null);
                    }
                    if (strata != 0)
                    {
                        // sum score and variance matrices over strata for later combined calcs
                        for (int j2 = 1; j2 <= groups; j2++)
                        {
                            if (test == 1)
                                u0Suml[j2] += u0[j2];
                            else
                                u0Sumw[j2] += u0[j2];
                            for (int k = 1; k <= groups; k++)
                            {
                                if (test == 1)
                                    vsuml[j2, k] += v[j2, k];
                                else
                                    vsumw[j2, k] += v[j2, k];
                            }
                        }
                    }
                    if (stratum == strata && strata != 0)
                    {
                        // combined (deaths, extent of exposure to risk of death, relative rate):
                        IList<ParameterBag> strataList = new List<ParameterBag>();
                        outerParameters.AddOutput("*strata", strataList);
                        ParameterBag strataParameters = new();
                        strataList.Add(strataParameters);
                        strataParameters.AddOutput("test", testname);
                        IList<ParameterBag> stratumList = new List<ParameterBag>();
                        strataParameters.AddOutput("*stratum", stratumList);
                        for (int j3 = 1; j3 <= groups; j3++)
                        {
                            ParameterBag stratumParameters = new();
                            stratumList.Add(stratumParameters);
                            stratumParameters.AddOutput("grp", j3);
                            stratumParameters.AddOutput("res", tdg[j3]);
                            stratumParameters.AddOutput("sum", tesum[j3]);
                            stratumParameters.AddOutput("tot", tdg[j3] / tesum[j3]);
                        }
                        // get U0'inv(V)U0 from combined matrices
                        if (test == 1)
                        {
                            // stratified logrank
                            // A copy is inverted: gaussj works in place, and the trend statistic below needs the summed covariance
                            // matrix itself (it was being computed from the half-inverted one). imfault is cleared because gaussj
                            // only ever sets it, so a singular last stratum used to make the combined test missing.
                            vtemp = new double[groups, 1 + 1];
                            double[,] vsumlInv = (double[,])vsuml.Clone();
                            imfault = 0;
                            MathDbl.gaussj(vsumlInv, 1, groups - 1, vtemp, 1, ref imfault);
                            if (imfault != 0)
                            {
                                x2 = Constant.MISSING;
                            }
                            else
                            {
                                for (int j2 = 1; j2 < groups; j2++)
                                    for (int k = 1; k < groups; k++)
                                        vtemp[j2, 1] = vtemp[j2, 1] + vsumlInv[j2, k] * u0Suml[k];
                                x2 = 0.0;
                                for (int k = 1; k < groups; k++)
                                    x2 += vtemp[k, 1] * u0Suml[k];
                            }
                        }
                        else
                        {
                            // stratified Wilcoxon (a copy is inverted, as above)
                            vtemp = new double[groups, 1 + 1];
                            double[,] vsumwInv = (double[,])vsumw.Clone();
                            imfault = 0;
                            MathDbl.gaussj(vsumwInv, 1, groups - 1, vtemp, 1, ref imfault);
                            if (imfault != 0)
                            {
                                x2 = Constant.MISSING;
                            }
                            else
                            {
                                for (int j2 = 1; j2 < groups; j2++)
                                    for (int k = 1; k < groups; k++)
                                        vtemp[j2, 1] = vtemp[j2, 1] + vsumwInv[j2, k] * u0Sumw[k];
                                x2 = 0.0;
                                for (int k = 1; k < groups; k++)
                                    x2 += vtemp[k, 1] * u0Sumw[k];
                            }
                        }
                        strataParameters.AddOutput("chi_strata", x2);
                        strataParameters.AddOutput("p_strata", PDF.chivalp(x2, groups - 1));
                        if (groups > 2)
                        {
                            // trend statistic (c'U0)^2 / c'Vc
                            x2Num = 0.0;
                            for (int k = 1; k <= groups; k++)
                            {
                                if (test == 1)
                                    x2Num += u0Suml[k] * score[k];
                                else
                                    x2Num += u0Sumw[k] * score[k];
                            }
                            x2Num *= x2Num;
                            vtemp = new double[groups + 1, 1 + 1];
                            x2Den = 0.0;
                            for (int j2 = 1; j2 <= groups; j2++)
                            {
                                for (int k = 1; k <= groups; k++)
                                    vtemp[j2, 1] += test == 1
                                        ? vsuml[j2, k] * score[k]
                                        : vsumw[j2, k] * score[k];
                            }
                            for (int k = 1; k <= groups; k++)
                                x2Den += vtemp[k, 1] * score[k];
                            x2T = x2Num / x2Den;
                            IList<ParameterBag> strataTrendList = new List<ParameterBag>();
                            strataParameters.AddOutput("*strata_trend", strataTrendList);
                            ParameterBag strataTrendParameters = new();
                            strataTrendList.Add(strataTrendParameters);
                            strataTrendParameters.AddOutput("strata_trend", x2T);
                            strataTrendParameters.AddOutput("p_strata_trend", PDF.chivalp(x2T, 1.0));
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
                    if (test == 1 && (stratum == strata || strata == 0))
                    {
                        // Hazard Ratio" + " & approximate "
                        // for each pair of groups: (O j / E j) / (O k / E k), O being the deaths observed and E those expected, over the
                        // strata if there are any; the limits are those of its logarithm, whose standard error is taken as
                        // root (1 / E j + 1 / E k)
                        IList<ParameterBag> hazardsList = new List<ParameterBag>();
                        outerParameters.AddOutput("*hazards", hazardsList);
                        ParameterBag hazardsParameters = new();
                        hazardsList.Add(hazardsParameters);
                        hazardsParameters.AddOutput("pc", gamma * 100);

                        IList<ParameterBag> hazardList = new List<ParameterBag>();
                        hazardsParameters.AddOutput("*hazard", hazardList);
                        for (int j = 1; j < groups; j++)
                        {
                            for (int k = j + 1; k <= groups; k++)
                            {
                                rr = tesum[j] <= 0 ? Constant.MISSING : tdg[j] / tesum[j];
                                double rk = tesum[k] <= 0 ? Constant.MISSING : tdg[k] / tesum[k];
                                double ru;
                                double rl;
                                if (rr != Constant.MISSING && rk != Constant.MISSING && rr > 0 && rk > 0)
                                {
                                    rr /= rk;
                                    rl = Math.Exp(Math.Log(rr) - cit * Math.Sqrt(1.0 / tesum[j] + 1.0 / tesum[k]));
                                    ru = Math.Exp(Math.Log(rr) + cit * Math.Sqrt(1.0 / tesum[j] + 1.0 / tesum[k]));
                                }
                                else
                                {
                                    // with no deaths in one group the ratio is 0 or infinite and has no interval on the log scale
                                    if (rr != Constant.MISSING && rk != Constant.MISSING && rr + rk > 0)
                                        rr = rr > 0 ? double.PositiveInfinity : 0.0;
                                    else
                                        rr = Constant.MISSING;
                                    ru = Constant.MISSING;
                                    rl = Constant.MISSING;
                                }
                                ParameterBag hazardParameters = new();
                                hazardList.Add(hazardParameters);
                                hazardParameters.AddOutput("vs1", j);
                                hazardParameters.AddOutput("vs2", k);
                                hazardParameters.AddOutput("haz", rr);
                                hazardParameters.AddOutput("from", rl);
                                hazardParameters.AddOutput("to", ru);
                            }
                        }
                        if (groups == 2 && strata == 0)
                        {
                            IList<ParameterBag> cmlList = new List<ParameterBag>();
                            hazardsParameters.AddOutput("*cml", cmlList);
                            ParameterBag cmlParameters = new();
                            cmlList.Add(cmlParameters);
                            // exact Hazard Ratio
                            cmlParameters.AddOutput("hr", hr);
                            cmlParameters.AddOutput("pc", gamma * 100.0);
                            cmlParameters.AddOutput("llf", llf);
                            cmlParameters.AddOutput("ulf", ulf);
                            cmlParameters.AddOutput("p1f", p1F);
                            cmlParameters.AddOutput("p2f", p2F);
                            cmlParameters.AddOutput("llm", llm);
                            cmlParameters.AddOutput("ulm", ulm);
                            cmlParameters.AddOutput("p1m", p1M);
                            cmlParameters.AddOutput("p2m", p2M);
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
                while (!(test >= 2));
            }
            while (stratum != strata);
            return new StepOutput(outputParameters);
        }
    }
}
