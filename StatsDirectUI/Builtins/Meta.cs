using StatsDirect.Charting;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.Templates;
using StatsDirect.Utilities;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using static StatsDirect.Builtins.ExactBB;

namespace StatsDirect.Builtins
{
    /// <summary>
    /// The analyses of the Meta-analysis menu, and the pooling of tables that the chi-square and crosstabs menus share with them.
    ///
    /// What the analyses of the Meta-analysis menu share.  Each study gives an estimate y on the scale of the pooling (the logarithm
    /// for a ratio, Fisher's z for a correlation, the double arcsine for a proportion) with a variance v, and has the weight
    /// w = 1 / v.  The fixed effects estimate is the sum of w y over the sum of w, with the variance 1 over the sum of w.
    /// Cochran's Q is the sum of w (y - pooled)^2, on one degree of freedom fewer than there are studies.  The variance between
    /// the studies by the method of moments (DerSimonian-Laird) is (Q - df) / (sum of w - sum of w^2 / sum of w), not below 0; the
    /// random effects weights are 1 / (v + that variance), and the random effects estimate is pooled with them in the same way.
    /// I-squared is 100 (Q - df) / Q, not below 0 (see IsquareNcc).  The bias indicators are in Metabias and ModMetabias.
    ///
    /// A table of two groups by an event is held as o[i, 1] = a, the events of the first group; o[i, 2] = b, the events of the
    /// second; o[i, 3] = c and o[i, 4] = d, the subjects without the event of the first group and of the second; n is their total.
    /// </summary>
    public static class Meta
    {
        /// <summary>
        /// Peto odds ratio meta-analysis.  For each study O - E is a less its expectation (a + b)(a + c) / n, and V is the variance
        /// of a with the totals of the table given, (a + b)(c + d)(a + c)(b + d) / (n^2 (n - 1)).  The logarithm of the odds ratio of
        /// the study is (O - E) / V with the variance 1 / V, and that of the pooled odds ratio is the sum of O - E over the sum of V,
        /// with the variance 1 over the sum of V.  The weight of a study is V.  A study without a variance (no events, events in
        /// every subject, a group of nobody, one subject) is left out.
        /// </summary>
        /// <param name="host">The preferences, and where progress is shown.</param>
        /// <param name="parameters">"sn" and "sr": the number of subjects and the number with the event in the first group of each
        /// study; "xn" and "xr": the same of the second group; "strata" (may be left out): the labels of the studies; "gamma": the
        /// confidence level.</param>
        public static StepOutput RptPetoMeta(IPreferencesAndProgressBar host, ParameterBag parameters)
        {
            double cco = parameters["gamma"].AsDouble;
            if (cco <= 0)
                cco = 0.95;
            double cit = PDF.gauinv(1.0 - (1.0 - cco) / 2.0);

            DataFrame snFrame = parameters["sn"].AsDataFrame;
            DoubleVariable snVariable = (DoubleVariable)snFrame.Variables[0];
            DataFrame srFrame = parameters["sr"].AsDataFrame;
            DoubleVariable srVariable = (DoubleVariable)srFrame.Variables[0];
            DataFrame xnFrame = parameters["xn"].AsDataFrame;
            DoubleVariable xnVariable = (DoubleVariable)xnFrame.Variables[0];
            DataFrame xrFrame = parameters["xr"].AsDataFrame;
            DoubleVariable xrVariable = (DoubleVariable)xrFrame.Variables[0];
            int rawRows = snVariable.Length;

            DoubleArraysAndBooleans copiesRemovingMissingRows = Numerics.Utilities.RemoveMissingRows(new[] { snVariable.Data, srVariable.Data, xnVariable.Data, xrVariable.Data }, 0, rawRows, 1);
            double[] sn = copiesRemovingMissingRows.ArraysWithMissingRowsRemoved[0];
            double[] sr = copiesRemovingMissingRows.ArraysWithMissingRowsRemoved[1];
            double[] xn = copiesRemovingMissingRows.ArraysWithMissingRowsRemoved[2];
            double[] xr = copiesRemovingMissingRows.ArraysWithMissingRowsRemoved[3];

            string[] title = MakeTitles(parameters, "strata", "stratum {0}", rawRows, out bool hasUserSuppliedLabels);

            int k = copiesRemovingMissingRows.ArraysWithMissingRowsRemoved[0].Length - 1; /* 1-based */
            title = Numerics.Utilities.CopyValidRows(title, copiesRemovingMissingRows.ValidRowsInOriginal, 0, rawRows, 1, k);

            double[,] o = new double[k + 1, 4 + 1];
            double[] oe = new double[k + 1];
            double[] odr = new double[k + 1];
            double[] odrv = new double[k + 1];
            double[] odrl = new double[k + 1];
            double[] odru = new double[k + 1];
            double[] odw = new double[k + 1];
            double[] odz = new double[k + 1];
            double[] odx = new double[k + 1];
            bool[] allFalse = new bool[k + 1];
            bool[] included = new bool[k + 1];
            for (int i = 1; i <= k; i++)
            {
                o[i, 1] = Math.Abs(sr[i]);
                o[i, 3] = Math.Abs(sn[i] - sr[i]);
                if (sr[i] < 0 || sn[i] < 0 || sn[i] < sr[i])
                    throw new InvalidDataException();
                o[i, 2] = Math.Abs(xr[i]);
                o[i, 4] = Math.Abs(xn[i] - xr[i]);
                if (xr[i] < 0 || xn[i] < 0 || xn[i] < xr[i])
                    throw new InvalidDataException();
                included[i] = IncludeTable(o, i);
            }

            // see Fleiss paper
            double sumoe = 0.0;
            double sumv = 0.0;
            for (int i = 1; i <= k; i++)
            {
                double a = o[i, 1];
                double b = o[i, 2];
                double c = o[i, 3];
                double d = o[i, 4];
                double n = a + b + c + d;
                if (n > 0)
                {
                    double e = (a + b) * (a + c) / n;
                    oe[i] = a - e;
                    sumoe += oe[i];
                    // one subject gives no variance (0 / 0): the study is left out with the others that have none
                    double v = n > 1.0 ? (a + b) * (c + d) * (a + c) * (b + d) / (n * n * (n - 1)) : 0.0;
                    sumv += v;
                    odw[i] = v;
                    odx[i] = n;
                    if (v > 0)
                    {
                        odr[i] = Math.Exp(oe[i] / v);
                        odz[i] = oe[i] / Math.Sqrt(v);
                        odrv[i] = 1.0 / v; // variance of the log odds ratio (O - E) / V
                        odrl[i] = Math.Exp((oe[i] - cit * Math.Sqrt(v)) / v);
                        odru[i] = Math.Exp((oe[i] + cit * Math.Sqrt(v)) / v);
                    }
                    else
                    {
                        // an empty arm (or an empty outcome) gives V = 0: the table has no log odds ratio and is excluded
                        included[i] = false;
                        odr[i] = Constant.MISSING;
                        odrv[i] = Constant.MISSING;
                        odrl[i] = Constant.MISSING;
                        odru[i] = Constant.MISSING;
                        odz[i] = Constant.MISSING;
                    }
                }
                else
                {
                    included[i] = false;
                    odr[i] = Constant.MISSING;
                    odw[i] = Constant.MISSING;
                    odrv[i] = Constant.MISSING;
                    odrl[i] = Constant.MISSING;
                    odru[i] = Constant.MISSING;
                    odz[i] = Constant.MISSING;
                }
            }

            // pooled peto odds ratio
            double poru; double porl; double por; double z;
            if (sumv > 0.0)
            {
                por = Math.Exp(sumoe / sumv);
                porl = Math.Exp((sumoe - cit * Math.Sqrt(sumv)) / sumv);
                poru = Math.Exp((sumoe + cit * Math.Sqrt(sumv)) / sumv);
                z = sumoe / Math.Sqrt(sumv);
            }
            else
            {
                throw new TemplateOperationCancelledException("None of the studies can be pooled: each of them has no events, or events in every subject, or a group with no subjects.", "Peto odds ratio meta-analysis");
            }

            // combinability
            // Q: the sum over the studies of V times the square of what the logarithm of the odds ratio of the study differs from
            // that of the pooled odds ratio by
            double qc = 0.0;
            int realk = 0;
            for (int i = 1; i <= k; i++)
            {
                if (included[i])
                {
                    realk++;
                    double lori = oe[i] / odw[i];
                    qc += Math.Pow(lori - Math.Log(por), 2.0) * odw[i];
                }
            }

            ParameterBag outputParameters = new();
            IList<ParameterBag> inputsList = new List<ParameterBag>();
            outputParameters.AddOutput("*inputs", inputsList);
            for (int i = 1; i <= k; i++)
            {
                ParameterBag inputsParameters = new();
                inputsList.Add(inputsParameters);
                inputsParameters.AddOutput("st", i);
                inputsParameters.AddOutput("a", o[i, 1]);
                inputsParameters.AddOutput("b", o[i, 2]);
                inputsParameters.AddOutput("c", o[i, 3]);
                inputsParameters.AddOutput("d", o[i, 4]);
                inputsParameters.AddOutput("lb", included[i] ? GetMetaLabel(host, o, i, hasUserSuppliedLabels, allFalse, title) : "* (excluded)");
            }

            outputParameters.AddOutput("pc", cco * 100);

            IList<ParameterBag> oddsList = new List<ParameterBag>();
            outputParameters.AddOutput("*odds", oddsList);
            for (int i = 1; i <= k; i++)
            {
                ParameterBag oddsParameters = new();
                oddsList.Add(oddsParameters);
                oddsParameters.AddOutput("st", i);
                oddsParameters.AddOutput("oe", oe[i]);
                oddsParameters.AddOutput("or", odr[i]);
                oddsParameters.AddOutput("yi", included[i] ? Math.Log(odr[i]) : Constant.MISSING);
                oddsParameters.AddOutput("vi", odrv[i]);
                oddsParameters.AddOutput("lci", odrl[i]);
                oddsParameters.AddOutput("uci", odru[i]);
                oddsParameters.AddOutput("wt", 100 * odw[i] / Formatting.dsum(odw, 1));
                oddsParameters.AddOutput("lb", included[i] ? GetMetaLabel(host, o, i, hasUserSuppliedLabels, allFalse, title) : "* (excluded)");
            }

            IList<ParameterBag> zList = new List<ParameterBag>();
            outputParameters.AddOutput("*z", zList);
            for (int i = 1; i <= k; i++)
            {
                ParameterBag zParameters = new();
                zList.Add(zParameters);
                zParameters.AddOutput("st", i);
                zParameters.AddOutput("v", odw[i]);
                zParameters.AddOutput("z", odz[i]);
                if (odz[i] != Constant.MISSING)
                {
                    double p = 1.0 - PDF.alnorm(odz[i]);
                    if (p > 1.0 - p)
                        p = 1.0 - p;
                    zParameters.AddOutput("p", 2.0 * p);
                }
                else
                {
                    zParameters.AddOutput("p", Formatting.ASTERISK);
                }
                zParameters.AddOutput("lb", included[i] ? GetMetaLabel(host, o, i, hasUserSuppliedLabels, allFalse, title) : "* (excluded)");
            }

            outputParameters.AddOutput("por", por);
            outputParameters.AddOutput("from", porl);
            outputParameters.AddOutput("to", poru);

            outputParameters.AddOutput("z", z);
            double pz = 1.0 - PDF.alnorm(z);
            if (pz > 1.0 - pz)
                pz = 1.0 - pz;
            outputParameters.AddOutput("p_z", 2.0 * pz);

            outputParameters.AddOutput("qc", realk > 1 ? qc : 0.0);   // 0 by definition with one stratum, not the rounding residue of one squared deviation
            outputParameters.AddOutput("df", realk - 1);
            outputParameters.AddOutput("xp", PDF.chivalp(qc, realk - 1));
            IsquareNcc(host, qc, realk, cco, cit, out double isq, out double llisq, out double ulisq);
            outputParameters.AddOutput("isq", isq);
            outputParameters.AddOutput("pc1", cco * 100);
            outputParameters.AddOutput("llisq", llisq);
            outputParameters.AddOutput("ulisq", ulisq);

            IList<ParameterBag> eggerList = new List<ParameterBag>();
            outputParameters.AddOutput("*egger", eggerList);
            ParameterBag eggerParameters = new();
            eggerList.Add(eggerParameters);
            bool biasReported = Metabias(host, eggerParameters, odr, odrl, odru, k, ref cco, Transformation.Log);
            FewStrata(outputParameters, eggerList, biasReported);

            IList<ParameterBag> harbordList = new List<ParameterBag>();
            outputParameters.AddOutput("*harbord", harbordList);
            ParameterBag harbordParameters = new();
            harbordList.Add(harbordParameters);
            ModMetabias(host, harbordParameters, o, k, cco, 1);
            if (!biasReported)
                harbordList.Clear();

            IList<ParameterBag> chartList = new List<ParameterBag>();
            outputParameters.AddOutput("*chart", chartList);
            ParameterBag chartParameters;

            if (k > 3)
            {
                chartParameters = new ParameterBag();
                chartList.Add(chartParameters);
                chartParameters.AddOutput("chart", ChartRendererFactory.PrepForLater(ChartType.BiasMA, new BiasMAOptions(odr, odx, odw, k, "Peto odds ratio", odrl, odru, cco, cit, por, Transformation.Log, false)));
            }

            chartParameters = new ParameterBag();
            chartList.Add(chartParameters);
            chartParameters.AddOutput("chart", ChartRendererFactory.PrepForLater(ChartType.LAbbe, new LAbbeOptions(k, o, por, true)));

            chartParameters = new ParameterBag();
            chartList.Add(chartParameters);
            chartParameters.AddOutput("chart", ChartRendererFactory.PrepForLater(ChartType.MH, new MHOptions(1, k, odw, title, por, porl, poru, cco, odr, odrl, odru, allFalse, allFalse, included, "Peto odds ratio plot", 1, "Peto odds ratio" /* , "Pooled Peto odds ratio" */)));

            if (k > 2)
            {
                chartParameters = new ParameterBag();
                chartList.Add(chartParameters);
                chartParameters.AddOutput("chart", ChartRendererFactory.PrepForLater(ChartType.BiasMA, new BiasMAOptions(odw, oe, oe, k, "Peto weights", odrl, odru, cco, cit, por, Transformation.None, true)));
            }

            return new StepOutput(outputParameters);
        }

        /// <summary>
        /// Confidence level for the intervals of the bias (small study effect) tests of Egger and Harbord: twice the alpha of the analysis, so 90% where the analysis uses 95%.
        /// </summary>
        /// <remarks>These tests have low power, so they are conventionally judged at P &lt; 0.1 and reported with the matching 90% interval (Egger et al. 1997; Harbord et al. 2006).</remarks>
        /// <param name="cco">Confidence level of the analysis, as a proportion</param>
        private static double BiasTestConfidenceLevel(double cco)
        {
            double level = 1.0 - 2.0 * (1.0 - cco);
            return level > 0.0 && level < 1.0 ? level : 0.9;
        }

        /// <summary>
        /// The bias indicator lines are printed only when they were given; otherwise their list is emptied and a one-element list carries
        /// the sentence that says the indicators need more than three studies
        /// </summary>
        internal static void FewStrata(ParameterBag outputParameters, IList<ParameterBag> biasList, bool reported)
        {
            IList<ParameterBag> fewList = new List<ParameterBag>();
            if (!reported)
            {
                biasList.Clear();
                fewList.Add(new ParameterBag());
            }
            outputParameters.AddOutput("*fewStrata", fewList);
        }

        /// <summary>
        /// The bias indicators of Begg and Mazumdar and of Egger, from the estimate of each study and its confidence limits.  The
        /// standard error of a study is taken back from its limits, (upper - lower) / (2 z) on the scale of the pooling, so the limits
        /// must be the estimate plus and minus z standard errors on that scale and at the confidence level of the analysis.
        /// Begg and Mazumdar: each estimate less the fixed effects pooled estimate, over the root of (its variance less the variance of
        /// the pooled estimate), is correlated with the variance by Kendall's rank correlation (Anova.XAgreeKendall).
        /// Egger: the estimate over its standard error is regressed on 1 over the standard error; the intercept is the bias, with a
        /// t test on two degrees of freedom fewer than there are studies.  Nothing is given with fewer than four studies.
        /// </summary>
        /// <param name="host">Where progress is shown.</param>
        /// <param name="outputParameters">Where the results are put.</param>
        /// <param name="t">The estimate of each study, from element 1.  A study with a missing estimate or limit is left out.</param>
        /// <param name="tl">The lower limit of each study.</param>
        /// <param name="tu">The upper limit of each study.</param>
        /// <param name="n">The number of studies.</param>
        /// <param name="cco">The confidence level of the limits; made 0.95 if it is not above 0.</param>
        /// <param name="xform">The scale of the pooling: the logarithm, Fisher's z, or the estimates as they are.</param>
        /// <returns>True if the indicators are given: false if there are fewer than four studies.</returns>
        public static bool Metabias(IProgressBarHost host, ParameterBag outputParameters, double[] t, double[] tl, double[] tu, int n, ref double cco, Transformation xform)
        {
            double cit;
            if (cco > 0)
            {
                cit = PDF.gauinv(1.0 - (1.0 - cco) / 2.0);
            }
            else
            {
                cco = 0.95;
                cit = PDF.gauinv(0.975);
            }

            // setup basic variables
            // bool DoC = true; 
            int P = 2;
            int nx = 0;
            for (int i = 1; i <= n; i++)
                if (t[i] != Constant.MISSING && tl[i] != Constant.MISSING && tu[i] != Constant.MISSING && !double.IsInfinity(tl[i]) && !double.IsInfinity(tu[i]))
                    nx++;

            double tau = Constant.MISSING; double p2 = Constant.MISSING;
            bool isLowPower = false;
            double[] seb = null; double[] bd = null;
            double rdf = 0; double rss = 0;
            //  Egger's regression of the standardised effect on precision has no answer when every study has the same precision (the predictor is
            //  constant) or when the line fits every study exactly (no residual variance, so no standard error or P)
            bool eggerComputable = false;

            bool tooFewStrata = nx < 4;
            if (!tooFewStrata)
            {
                double[] y = new double[nx + 1];
                double[,] x = new double[nx + 1, P + 1];
                double[] wt = new double[nx + 1];
                double[] var = new double[nx + 1];
                double[] tt = new double[nx + 1];
                double[] ts = new double[nx + 1];
                nx = 0;
                double se;
                switch (xform)
                {
                    case Transformation.Log:
                        for (int i = 1; i <= n; i++)
                        {
                            if (t[i] != Constant.MISSING && tl[i] != Constant.MISSING && tu[i] != Constant.MISSING && !double.IsInfinity(tl[i]) && !double.IsInfinity(tu[i]) && tu[i] - tl[i] != 0.0 && t[i] > 0.0 && tl[i] > 0.0 && tu[i] > 0.0)
                            {
                                se = (Math.Log(tu[i]) - Math.Log(tl[i])) / 2 / cit;
                                if (se != 0.0)
                                {
                                    nx += 1;
                                    tt[nx] = Math.Log(t[i]);
                                    y[nx] = tt[nx] / se;
                                    x[nx, 2] = 1.0 / se;
                                    x[nx, 1] = 1.0;
                                    var[nx] = se * se;
                                    wt[nx] = 1.0;
                                }
                            }
                        }
                        break;
                    case Transformation.Z:
                        for (int i = 1; i <= n; i++)
                        {
                            //  Fisher's z is defined for any correlation strictly between -1 and 1. This test used to require the correlation and both limits to be positive, as the log transformation above does, which silently left out every study with a correlation or lower limit at or below zero.
                            if (t[i] != Constant.MISSING & tl[i] != Constant.MISSING & tu[i] != Constant.MISSING & !double.IsInfinity(tl[i]) & !double.IsInfinity(tu[i]) & tu[i] - tl[i] != 0.0 & Math.Abs(t[i]) < 1.0 & Math.Abs(tl[i]) < 1.0 & Math.Abs(tu[i]) < 1.0)
                            {
                                se = (MathDbl.rtoz(tu[i]) - MathDbl.rtoz(tl[i])) / 2 / cit;
                                if (se != 0.0)
                                {
                                    nx += 1;
                                    tt[nx] = MathDbl.rtoz(t[i]);
                                    y[nx] = tt[nx] / se;
                                    x[nx, 2] = 1.0 / se;
                                    x[nx, 1] = 1.0;
                                    var[nx] = se * se;
                                    wt[nx] = 1.0;
                                }
                            }
                        }
                        break;
                    case Transformation.None:
                        for (int i = 1; i <= n; i++)
                        {
                            if (t[i] != Constant.MISSING & tl[i] != Constant.MISSING & tu[i] != Constant.MISSING & !double.IsInfinity(tl[i]) & !double.IsInfinity(tu[i]))
                            {
                                se = (tu[i] - tl[i]) / 2 / cit;
                                if (se != 0)
                                {
                                    nx += 1;
                                    tt[nx] = t[i];
                                    y[nx] = tt[nx] / se;
                                    x[nx, 2] = 1.0 / se;
                                    x[nx, 1] = 1.0;
                                    var[nx] = se * se;
                                    wt[nx] = 1.0;
                                }
                            }
                        }
                        break;
                }

                // Begg's method
                double sumwt = 0.0;
                double sumwtt = 0.0;
                for (int i = 1; i <= nx; i++)
                {
                    double wx = 1.0 / var[i];
                    sumwt += wx;
                    sumwtt += tt[i] * wx;
                }
                for (int i = 1; i <= nx; i++)
                {
                    double vt = var[i] - 1.0 / sumwt;
                    ts[i] = (tt[i] - sumwtt / sumwt) / Math.Sqrt(vt);
                }
                Anova.XAgreeKendall(host, ts, var, 1, ref nx, out tau, out p2, out isLowPower, out bool isTauB);

                // setup regression call
                seb = new double[P + 1];
                bd = new double[P * P + 1];
                int incep = 1;
                int indep = 1;
                int iwt = 1;
                double[,] xx = new double[nx + 1, indep + 1 + iwt + 1];
                double[,] r = new double[P + 1, P + 1];
                double[] D = new double[P + 1];
                double[] xMin = new double[P + 1];
                double[] xMax = new double[P + 1];
                double[] wk = new double[2 * (P + 1) + 1];
                int[] idum = new int[1 + 1];
                for (int i = 1; i <= nx; i++)
                {
                    int j;
                    for (j = 1 + incep; j <= indep + incep; j++)
                        xx[i, j - incep] = x[i, j];
                    xx[i, indep + 1] = wt[i];
                    xx[i, indep + 2] = y[i];
                }
                int iwtcol = indep + 1;
                int irank = 0; int nrmiss = 0; int ifault = 0;
                Regress1.glsqr(0, incep, 0, nx, indep + iwt + 1, xx, -indep, idum, -1, idum, 0, iwtcol, bd, r, D, ref irank, ref rdf, ref rss, ref nrmiss, xMin, xMax, wk, ref ifault);
                double minPrecision = double.MaxValue; double maxPrecision = double.MinValue; double sumysq = 0.0;
                for (int i = 1; i <= nx; i++)
                {
                    minPrecision = Math.Min(minPrecision, x[i, 2]);
                    maxPrecision = Math.Max(maxPrecision, x[i, 2]);
                    sumysq += y[i] * y[i];
                }
                //  precisions that agree to a relative 1e-12 are the same (they are computed from the limits), and a residual sum of squares below that fraction of the sum of squares is a perfect fit
                eggerComputable = ifault == 0 && irank == P && rdf > 0 && maxPrecision - minPrecision > 1e-12 * maxPrecision && rss > 1e-12 * sumysq;
                if (eggerComputable)
                {
                    double[,] covb = new double[P + 1, P + 1];
                    Regress1.rcovarb(P, r, 1.0, covb, ref ifault);
                    double rms = rss / rdf;
                    Regress1.rcovarb(P, r, rms, covb, ref ifault);
                    for (int i = 1; i <= P; i++)
                        seb[i] = Math.Sqrt(covb[i, i]);
                }
            }

            //  cco and cit above recover each study's standard error from its limits, so they stay at the level of the analysis; only Egger's own interval uses the bias test level
            double biasCco = BiasTestConfidenceLevel(cco);
            double a, prob, cla, cua;
            if (!tooFewStrata && eggerComputable)
            {
                MathDbl.civ(nx - P, out double citt, biasCco, out double _);
                Debug.Assert(null != seb);
                double tz = Math.Abs(bd[1] / seb[1]);
                prob = PDF.tvalp(tz, Convert.ToDouble(nx - P));
                if (prob > 1.0 - prob)
                    prob = 1.0 - prob;
                prob = 2.0 * prob;
                a = bd[1];
                cla = a - seb[1] * citt;
                cua = a + seb[1] * citt;
            }
            else
            {
                a = Constant.MISSING;
                cla = Constant.MISSING;
                cua = Constant.MISSING;
                prob = Constant.MISSING;
            }

            if (tooFewStrata)
            {
                outputParameters.AddOutput("warnTooFewStrata", "<too few strata>");
                p2 = Constant.MISSING;
            }
            else
            {
                outputParameters.AddOutput("tau", tau);
            }
            outputParameters.AddOutput("p2", p2);
            //  Kendall's test has low power below eleven studies (Begg & Mazumdar 1994); the note follows the P value
            outputParameters.AddOutput("low_power_warn", !tooFewStrata && isLowPower && tau != Constant.MISSING && !double.IsNaN(tau) ? " (low power)" : string.Empty);

            if (tooFewStrata)
            {
                outputParameters.AddOutput("a", tau);
                cla = Constant.MISSING;
                cua = Constant.MISSING;
                prob = Constant.MISSING;
            }
            else
            {
                outputParameters.AddOutput("a", a);
            }

            outputParameters.AddOutput("pc_egger", 100.0 * biasCco);
            outputParameters.AddOutput("cl", cla);
            outputParameters.AddOutput("cu", cua);
            outputParameters.AddOutput("p", prob);
            return !tooFewStrata;
        }

        /// <summary>
        /// Risk difference meta-analysis: the report of what Riskdifma works out, with the bias indicators of Begg and Mazumdar and of
        /// Egger, which take the standard error of each study from the limits that Riskdifma makes from its variance.
        /// </summary>
        /// <param name="host">The preferences, and where progress is shown.</param>
        /// <param name="parameters">As for RptPetoMeta.</param>
        public static StepOutput RptRiskDifferenceMeta(IPreferencesAndProgressBar host, ParameterBag parameters)
        {
            double cco = parameters["gamma"].AsDouble;
            if (cco <= 0)
                cco = 0.95;
            double cit = PDF.gauinv(1.0 - (1.0 - cco) / 2.0);

            DataFrame snFrame = parameters["sn"].AsDataFrame;
            DoubleVariable snVariable = (DoubleVariable)snFrame.Variables[0];
            DataFrame srFrame = parameters["sr"].AsDataFrame;
            DoubleVariable srVariable = (DoubleVariable)srFrame.Variables[0];
            DataFrame xnFrame = parameters["xn"].AsDataFrame;
            DoubleVariable xnVariable = (DoubleVariable)xnFrame.Variables[0];
            DataFrame xrFrame = parameters["xr"].AsDataFrame;
            DoubleVariable xrVariable = (DoubleVariable)xrFrame.Variables[0];
            int rawRows = snVariable.Length;

            DoubleArraysAndBooleans copiesRemovingMissingRows = Numerics.Utilities.RemoveMissingRows(new[] { snVariable.Data, srVariable.Data, xnVariable.Data, xrVariable.Data }, 0, rawRows, 1);
            double[] sn = copiesRemovingMissingRows.ArraysWithMissingRowsRemoved[0];
            double[] sr = copiesRemovingMissingRows.ArraysWithMissingRowsRemoved[1];
            double[] xn = copiesRemovingMissingRows.ArraysWithMissingRowsRemoved[2];
            double[] xr = copiesRemovingMissingRows.ArraysWithMissingRowsRemoved[3];

            string[] title = MakeTitles(parameters, "strata", "stratum {0}", rawRows, out bool hasUserSuppliedLabels);

            int k = copiesRemovingMissingRows.ArraysWithMissingRowsRemoved[0].Length - 1; /* 1-based */
            title = Numerics.Utilities.CopyValidRows(title, copiesRemovingMissingRows.ValidRowsInOriginal, 0, rawRows, 1, k);

            double[,] o = new double[k + 1, 4 + 1];
            double[] rkr = new double[k + 1];
            double[] rkw = new double[k + 1];
            double[] dsw = new double[k + 1];
            double[] rkrl = new double[k + 1];
            double[] rkru = new double[k + 1];
            double[] rkx = new double[k + 1];
            bool[] lerr = new bool[k + 1];
            bool[] uerr = new bool[k + 1];
            bool[] cced = new bool[k + 1];
            bool[] included = new bool[k + 1];
            // the limits of each study from its standard error, which the bias indicators take the standard error back from
            double[] wll = new double[k + 1];
            double[] wul = new double[k + 1];
            for (int i = 1; i <= k; i++)
            {
                o[i, 1] = Math.Abs(sr[i]);
                o[i, 3] = Math.Abs(sn[i] - sr[i]);
                if (sr[i] < 0 || sn[i] < 0 || sn[i] < sr[i])
                    throw new InvalidDataException();
                o[i, 2] = Math.Abs(xr[i]);
                o[i, 4] = Math.Abs(xn[i] - xr[i]);
                if (xr[i] < 0 || xn[i] < 0 || xn[i] < xr[i])
                    throw new InvalidDataException();
            }

            Riskdifma(host, k, o, out double rmh, out double ll, out double ul, out double x2Rmh, cit, cco, rkr, rkw, dsw, rkrl, rkru, rkx, lerr, uerr, out double qc, out double dsrd, out double dsx2, out double dsll, out double dsul, out double tausq, cced, included, wll, wul, out int realk, out int ierr);
            if (ierr == -1)
                throw new InvalidDataException();

            ParameterBag outputParameters = new();

            IList<ParameterBag> inputsList = new List<ParameterBag>();
            outputParameters.AddOutput("*inputs", inputsList);
            for (int i = 1; i <= k; i++)
            {
                ParameterBag inputsParameters = new();
                inputsList.Add(inputsParameters);
                inputsParameters.AddOutput("st", i);
                inputsParameters.AddOutput("a", o[i, 1]);
                inputsParameters.AddOutput("b", o[i, 2]);
                inputsParameters.AddOutput("c", o[i, 3]);
                inputsParameters.AddOutput("d", o[i, 4]);
                string tmp = hasUserSuppliedLabels ? title[i] : string.Empty;
                if (cced[i])
                {
                    tmp += " [CC = ";
                    tmp += host.Preferences.MetaCC == -9.0
                              ? "treatment arm"
                              : host.Preferences.MetaCC.ToString();
                    tmp += "]";
                }
                if (!included[i])
                    tmp = "* (excluded)";
                inputsParameters.AddOutput("lb", tmp);
            }

            outputParameters.AddOutput("pc", cco * 100);
            outputParameters.AddOutput("method", host.Preferences.MetaExact ? "Miettinen" : "approximate");
            IList<ParameterBag> differencesList = new List<ParameterBag>();
            outputParameters.AddOutput("*differences", differencesList);
            for (int i = 1; i <= k; i++)
            {
                ParameterBag differencesParameters = new();
                differencesList.Add(differencesParameters);
                differencesParameters.AddOutput("st", i);
                differencesParameters.AddOutput("rd", rkr[i]);
                differencesParameters.AddOutput("lci", rkrl[i]);
                differencesParameters.AddOutput("uci", rkru[i]);
                differencesParameters.AddOutput("wt", 100 * rkw[i] / Formatting.dsum(rkw, 1));
                differencesParameters.AddOutput("dwt", 100 * dsw[i] / Formatting.dsum(dsw, 1));
                differencesParameters.AddOutput("lb", included[i] ? (hasUserSuppliedLabels ? title[i] : string.Empty) : "* (excluded)");
                differencesParameters.AddOutput("yi", rkr[i]);
                differencesParameters.AddOutput("vi", VarianceOfRiskDifference(host, o, i));
                // double a = o[ i, 1 ]; 
                // double b = o[ i, 2 ]; 
                // double C = o[ i, 3 ]; 
                // double D = o[ i, 4 ]; 
                // double N = a + b + C + D; 
            }

            outputParameters.AddOutput("rmh", rmh);
            outputParameters.AddOutput("from", ll);
            outputParameters.AddOutput("to", ul);

            outputParameters.AddOutput("x2", x2Rmh);
            outputParameters.AddOutput("df", 1);
            outputParameters.AddOutput("xp", x2Rmh == Constant.MISSING ? Constant.MISSING : PDF.chivalp(x2Rmh, 1.0));

            outputParameters.AddOutput("qc", realk > 1 ? qc : 0.0);   // 0 by definition with one stratum, not the rounding residue of one squared deviation
            outputParameters.AddOutput("df_cochran", realk - 1);
            outputParameters.AddOutput("xp_cochran", PDF.chivalp(qc, realk - 1));
            outputParameters.AddOutput("tausq", tausq);
            IsquareNcc(host, qc, realk, cco, cit, out double isq, out double llisq, out double ulisq);
            outputParameters.AddOutput("isq", isq);
            outputParameters.AddOutput("pc1", cco * 100);
            outputParameters.AddOutput("llisq", llisq);
            outputParameters.AddOutput("ulisq", ulisq);

            outputParameters.AddOutput("dsrd", dsrd);
            outputParameters.AddOutput("dsll", dsll);
            outputParameters.AddOutput("dsul", dsul);
            outputParameters.AddOutput("dsx2", dsx2);
            outputParameters.AddOutput("df_ds", 1);
            outputParameters.AddOutput("xp_ds", PDF.chivalp(dsx2, 1.0));

            IList<ParameterBag> eggerList = new List<ParameterBag>();
            outputParameters.AddOutput("*egger", eggerList);
            ParameterBag eggerParameters = new();
            eggerList.Add(eggerParameters);
            bool biasReported = Metabias(host, eggerParameters, rkr, wll, wul, k, ref cco, Transformation.None);
            FewStrata(outputParameters, eggerList, biasReported);

            IList<ParameterBag> chartList = new List<ParameterBag>();
            outputParameters.AddOutput("*chart", chartList);
            ParameterBag chartParameters;

            //  Ensure no accidental (0,0) plots
            rkr[0] = Constant.MISSING;
            rkx[0] = Constant.MISSING;
            rkw[0] = Constant.MISSING;

            if (k > 3)
            {
                chartParameters = new ParameterBag();
                chartList.Add(chartParameters);
                chartParameters.AddOutput("chart", ChartRendererFactory.PrepForLater(ChartType.BiasMA, new BiasMAOptions(rkr, rkx, rkw, k, "Risk difference", wll, wul, cco, cit, rmh, Transformation.None, false)));
            }

            chartParameters = new ParameterBag();
            chartList.Add(chartParameters);
            chartParameters.AddOutput("chart", ChartRendererFactory.PrepForLater(ChartType.MHRD, new MHOptions(1, k, rkw, title, rmh, ll, ul, cco, rkr, rkrl, rkru, lerr, uerr, included, "Risk difference meta-analysis plot [fixed effects]", 1, "risk difference")));

            chartParameters = new ParameterBag();
            chartList.Add(chartParameters);
            chartParameters.AddOutput("chart", ChartRendererFactory.PrepForLater(ChartType.MHRD, new MHOptions(1, k, dsw, title, dsrd, dsll, dsul, cco, rkr, rkrl, rkru, lerr, uerr, included, "Risk difference meta-analysis plot [random effects]", 1, "risk difference")));

            return new StepOutput(outputParameters);
        }

        /// <summary>
        /// Relative risk meta-analysis: the report of what RelativeRiskMA works out, with the bias indicators of Begg and Mazumdar and
        /// of Egger from the approximate limits of each study (GetAproxrrCI), and that of Harbord and Egger (ModMetabias).
        /// </summary>
        /// <param name="host">The preferences, and where progress is shown.</param>
        /// <param name="parameters">As for RptPetoMeta.</param>
        public static StepOutput RptRelativeRiskMeta(IPreferencesAndProgressBar host, ParameterBag parameters)
        {
            const int lowerBound = 1;

            double cco = parameters["gamma"].AsDouble;
            if (cco <= 0)
                cco = 0.95;
            double cit = PDF.gauinv(1.0 - (1.0 - cco) / 2.0);

            DataFrame snFrame = parameters["sn"].AsDataFrame;
            DoubleVariable snVariable = (DoubleVariable)snFrame.Variables[0];
            DataFrame srFrame = parameters["sr"].AsDataFrame;
            DoubleVariable srVariable = (DoubleVariable)srFrame.Variables[0];
            DataFrame xnFrame = parameters["xn"].AsDataFrame;
            DoubleVariable xnVariable = (DoubleVariable)xnFrame.Variables[0];
            DataFrame xrFrame = parameters["xr"].AsDataFrame;
            DoubleVariable xrVariable = (DoubleVariable)xrFrame.Variables[0];
            int rawRows = snVariable.Length;

            DoubleArraysAndBooleans copiesRemovingMissingRows = Numerics.Utilities.RemoveMissingRows(new[] { snVariable.Data, srVariable.Data, xnVariable.Data, xrVariable.Data }, 0, rawRows, 1);
            double[] sn = copiesRemovingMissingRows.ArraysWithMissingRowsRemoved[0];
            double[] sr = copiesRemovingMissingRows.ArraysWithMissingRowsRemoved[1];
            double[] xn = copiesRemovingMissingRows.ArraysWithMissingRowsRemoved[2];
            double[] xr = copiesRemovingMissingRows.ArraysWithMissingRowsRemoved[3];

            string[] title = MakeTitles(parameters, "strata", "stratum {0}", rawRows, out bool hasUserSuppliedLabels);

            int k = copiesRemovingMissingRows.ArraysWithMissingRowsRemoved[0].Length - 1; /* 1-based */
            title = Numerics.Utilities.CopyValidRows(title, copiesRemovingMissingRows.ValidRowsInOriginal, 0, rawRows, 1, k);

            double[,] o = new double[k + lowerBound, 4 + 1];
            double[] axll = new double[k + lowerBound];
            double[] axul = new double[k + lowerBound];
            for (int i = 1; i <= k; i++)
            {
                o[i, 1] = Math.Abs(sr[i]);
                o[i, 3] = Math.Abs(sn[i] - sr[i]);
                if (sr[i] < 0 || sn[i] < 0 || sn[i] < sr[i])
                    throw new InvalidDataException("All data values must be >= 0, and the number responding must be less than the sample size");

                o[i, 2] = Math.Abs(xr[i]);
                o[i, 4] = Math.Abs(xn[i] - xr[i]);
                if (xr[i] < 0 || xn[i] < 0 || xn[i] < xr[i])
                    throw new InvalidDataException("All data values must be >= 0, and the number responding must be less than the sample size");
            }

            RelativeRiskMA(host, lowerBound, k, out int realk, o, out double rmh, out double ll, out double ul, out double x2Rmh, cit, out double[] rkr, out double[] rkw, out double[] dsw, out double[] rkrl, out double[] rkru, out double[] rkx, out bool[] lerr, out bool[] uerr, out double qc, out double dsrr, out double dsx2, out double dsll, out double dsul, out double tausq, out bool[] cced, out bool[] included, out int ierr);
            if (ierr == -1)
                throw new InvalidDataException("relriskma() returned an error");
            if (realk == 0)
                throw new TemplateOperationCancelledException("None of the studies can be pooled: each of them has no events, or a group with no subjects.", "Relative risk meta-analysis");

            ParameterBag outputParameters = new();

            IList<ParameterBag> inputsList = new List<ParameterBag>();
            outputParameters.AddOutput("*inputs", inputsList);
            for (int i = 1; i <= k; i++)
            {
                ParameterBag inputsParameters = new();
                inputsList.Add(inputsParameters);
                inputsParameters.AddOutput("st", i);
                inputsParameters.AddOutput("a", o[i, 1]);
                inputsParameters.AddOutput("b", o[i, 2]);
                inputsParameters.AddOutput("c", o[i, 3]);
                inputsParameters.AddOutput("d", o[i, 4]);
                inputsParameters.AddOutput("lb", hasUserSuppliedLabels ? title[i] : string.Empty);
            }

            outputParameters.AddOutput("pc", cco * 100);
            outputParameters.AddOutput("method", host.Preferences.MetaExact ? "Koopman" : "approximate");

            IList<ParameterBag> risksList = new List<ParameterBag>();
            outputParameters.AddOutput("*risks", risksList);
            for (int i = 1; i <= k; i++)
            {
                ParameterBag risksParameters = new();
                risksList.Add(risksParameters);
                risksParameters.AddOutput("st", i);
                risksParameters.AddOutput("rr", rkr[i]);
                risksParameters.AddOutput("yi", rkr[i] > 0 ? Math.Log(rkr[i]) : 0);
                risksParameters.AddOutput("vi", VarianceOfLogRelativeRisk(host, o, i));
                risksParameters.AddOutput("lci", rkrl[i]);
                risksParameters.AddOutput("uci", rkru[i]);
                risksParameters.AddOutput("wt", 100 * rkw[i] / Formatting.dsum(rkw, 1));
                risksParameters.AddOutput("dwt", 100 * dsw[i] / Formatting.dsum(dsw, 1));
                risksParameters.AddOutput("lb", GetMetaLabel(host, included[i], i, hasUserSuppliedLabels, cced, title));
            }

            outputParameters.AddOutput("rr", rmh);
            outputParameters.AddOutput("from", ll);
            outputParameters.AddOutput("to", ul);

            outputParameters.AddOutput("x2", x2Rmh);
            outputParameters.AddOutput("df", 1);
            outputParameters.AddOutput("xp", PDF.chivalp(x2Rmh, 1.0));

            outputParameters.AddOutput("qc", realk > 1 ? qc : 0.0);   // 0 by definition with one stratum, not the rounding residue of one squared deviation
            outputParameters.AddOutput("df_cochran", realk - 1);
            outputParameters.AddOutput("xp_cochran", PDF.chivalp(qc, realk - 1));
            outputParameters.AddOutput("tausq", tausq);
            IsquareNcc(host, qc, realk, cco, cit, out double isq, out double llisq, out double ulisq);
            outputParameters.AddOutput("isq", isq);
            outputParameters.AddOutput("pc1", cco * 100);
            outputParameters.AddOutput("llisq", llisq);
            outputParameters.AddOutput("ulisq", ulisq);


            outputParameters.AddOutput("dsrr", dsrr);
            outputParameters.AddOutput("dsll", dsll);
            outputParameters.AddOutput("dsul", dsul);
            outputParameters.AddOutput("dsx2", dsx2);
            outputParameters.AddOutput("df_ds", 1);
            outputParameters.AddOutput("xp_ds", PDF.chivalp(dsx2, 1.0));

            GetAproxrrCI(host, o, k, cit, axll, axul);

            IList<ParameterBag> eggerList = new List<ParameterBag>();
            outputParameters.AddOutput("*egger", eggerList);
            ParameterBag eggerParameters = new();
            eggerList.Add(eggerParameters);
            bool biasReported = Metabias(host, eggerParameters, rkr, axll, axul, k, ref cco, Transformation.Log);
            FewStrata(outputParameters, eggerList, biasReported);

            IList<ParameterBag> harbordList = new List<ParameterBag>();
            outputParameters.AddOutput("*harbord", harbordList);
            ParameterBag harbordParameters = new();
            harbordList.Add(harbordParameters);
            ModMetabias(host, harbordParameters, o, k, cco, 2);
            if (!biasReported)
                harbordList.Clear();

            IList<ParameterBag> chartList = new List<ParameterBag>();
            outputParameters.AddOutput("*chart", chartList);
            ParameterBag chartParameters;

            if (k > 3)
            {
                chartParameters = new ParameterBag();
                chartList.Add(chartParameters);
                chartParameters.AddOutput("chart", ChartRendererFactory.PrepForLater(ChartType.BiasMA, new BiasMAOptions(rkr, rkx, rkw, k, "Relative risk", axll, axul, cco, cit, rmh, Transformation.Log, false)));
            }

            chartParameters = new ParameterBag();
            chartList.Add(chartParameters);
            chartParameters.AddOutput("chart", ChartRendererFactory.PrepForLater(ChartType.LAbbe, new LAbbeOptions(k, o, rmh)));

            chartParameters = new ParameterBag();
            chartList.Add(chartParameters);
            chartParameters.AddOutput("chart", ChartRendererFactory.PrepForLater(ChartType.MH, new MHOptions(1, k, rkw, title, rmh, ll, ul, cco, rkr, rkrl, rkru, lerr, uerr, included, "Relative risk meta-analysis plot (fixed effects)", 1, "relative risk")));

            chartParameters = new ParameterBag();
            chartList.Add(chartParameters);
            chartParameters.AddOutput("chart", ChartRendererFactory.PrepForLater(ChartType.MH, new MHOptions(1, k, dsw, title, dsrr, dsll, dsul, cco, rkr, rkrl, rkru, lerr, uerr, included, "Relative risk meta-analysis plot (random effects)", 1, "relative risk")));

            return new StepOutput(outputParameters);
        }

        // TODO: Move this somewhere more sensible now that it's used by functions outside meta.
        /// <summary>
        /// The labels of the studies, from element 0: those of the column of labels, cut to 50 characters, and for a row without a
        /// label (or beyond a column of labels that is shorter than the data) the row's number in the given form.
        /// </summary>
        /// <param name="parameters">The parameters of the analysis.</param>
        /// <param name="parameterName">The name of the column of labels among them; it may be absent.</param>
        /// <param name="missingTitleFormat">The form of the label of a row without one, for example "stratum {0}".</param>
        /// <param name="expectedRows">The number of rows of data.</param>
        /// <param name="hasUserSuppliedLabels">On return, whether there is a column of labels.</param>
        /// <param name="extraElementsAtEnd">The number of elements to add at the end, for the label of a pooled estimate.</param>
        internal static string[] MakeTitles(ParameterBag parameters, string parameterName, string missingTitleFormat, int expectedRows, out bool hasUserSuppliedLabels, int extraElementsAtEnd = 0)
        {
            string[] title;
            hasUserSuppliedLabels = parameters.ContainsKey(parameterName) && parameters[parameterName].HasData;
            if (hasUserSuppliedLabels)
            {
                DataFrame strataFrame = parameters[parameterName].AsDataFrame;
                StringVariable strataVariable = (StringVariable)strataFrame.Variables[0];
                if (extraElementsAtEnd > 0 || strataVariable.Data.Length < expectedRows)
                {
                    // Allocate a new array to hold the extra rows, or the rows beyond a label column shorter than the data
                    title = new string[expectedRows + extraElementsAtEnd];
                    Array.Copy(strataVariable.Data, title, strataVariable.Data.Length);
                }
                else
                {
                    // No extra rows, just nick the data from the variable
                    title = strataVariable.Data;
                }
                for (int i = 0; i < expectedRows; i++)
                {
                    string buf = i < strataVariable.Data.Length ? strataVariable.Data[i].Trim() : string.Empty;
                    if (buf.Length > 0)
                    {
                        if (buf.Length > 50)
                            buf = buf.Substring(0, 50);
                        title[i] = buf;
                    }
                    else
                        title[i] = string.Format(missingTitleFormat, i + 1);
                }
            }
            else
            {
                title = new string[expectedRows + extraElementsAtEnd];
                for (int i = 0; i < expectedRows; i++)
                    title[i] = string.Format(missingTitleFormat, i + 1);
            }
            return title;
        }

        /// <summary>
        /// Effect size meta-analysis, of the difference between the means of two groups.
        /// Standardised: g is the difference between the means over the pooled standard deviation of the two groups (or g is given);
        /// d = J g is g without its bias, J being the ratio of gamma functions G(m / 2) / (root(m / 2) G((m - 1) / 2)) with
        /// m = n - 2; the variance of d is n / (n1 n2) + d^2 / (2 n).  The exact limits of g are from the non-central t distribution
        /// (Ginterval); the approximate limits of d are d plus and minus z standard errors.
        /// Weighted mean difference (type "m"): the difference between the means as it is, with the variance s1^2 / n1 + s2^2 / n2.
        /// Either is pooled as the summary of the class describes.  A study without a standard deviation stops the pooling.
        /// </summary>
        /// <param name="host">The preferences, and where progress is shown.</param>
        /// <param name="parameters">"type": "g" if g is given, "m" for the weighted mean difference, anything else for d from
        /// means and standard deviations; "en", "em", "es": number, mean and standard deviation of the first (experimental) group;
        /// "cn", "cm", "cs": those of the second (control) group; "g": g, if it is given; "strata" (may be left out); "gamma".</param>
        public static StepOutput RptEffect(IPreferencesAndProgressBar host, ParameterBag parameters)
        {
            double cco = parameters["gamma"].AsDouble;
            if (cco <= 0)
                cco = 0.95;
            double cit = PDF.gauinv(1.0 - (1.0 - cco) / 2.0);

            string type = parameters["type"].AsString.ToLower(CultureInfo.InvariantCulture);
            int proc;
            switch (type)
            {
                case "g":
                    proc = 2;
                    break;
                case "m":
                    proc = 3;
                    break;
                default:
                    proc = 1;
                    break;
            }

            DataFrame enFrame = parameters["en"].AsDataFrame;
            DoubleVariable enVariable = (DoubleVariable)enFrame.Variables[0];
            int rawRows = enVariable.Length;

            DoubleVariable emVariable = null;
            DoubleVariable esVariable = null;
            if (proc != 2)
            {
                DataFrame emFrame = parameters["em"].AsDataFrame;
                emVariable = (DoubleVariable)emFrame.Variables[0];

                DataFrame esFrame = parameters["es"].AsDataFrame;
                esVariable = (DoubleVariable)esFrame.Variables[0];
            }

            DataFrame cnFrame = parameters["cn"].AsDataFrame;
            DoubleVariable cnVariable = (DoubleVariable)cnFrame.Variables[0];

            DoubleVariable gVariable = null;
            DoubleVariable cmVariable = null;
            DoubleVariable csVariable = null;
            bool gotg = proc == 2;
            if (gotg)
            {
                DataFrame gFrame = parameters["g"].AsDataFrame;
                gVariable = (DoubleVariable)gFrame.Variables[0];
            }
            else
            {
                DataFrame cmFrame = parameters["cm"].AsDataFrame;
                cmVariable = (DoubleVariable)cmFrame.Variables[0];

                DataFrame csFrame = parameters["cs"].AsDataFrame;
                csVariable = (DoubleVariable)csFrame.Variables[0];
            }

            DoubleArraysAndBooleans copiesRemovingMissingRows = Numerics.Utilities.RemoveMissingRows(new[] { enVariable.Data, emVariable?.Data, esVariable?.Data, gVariable?.Data, cnVariable.Data, cmVariable?.Data, csVariable?.Data }, 0, rawRows, 1);
            double[] en = copiesRemovingMissingRows.ArraysWithMissingRowsRemoved[0];
            double[] em = copiesRemovingMissingRows.ArraysWithMissingRowsRemoved[1];
            double[] es = copiesRemovingMissingRows.ArraysWithMissingRowsRemoved[2];
            double[] g = copiesRemovingMissingRows.ArraysWithMissingRowsRemoved[3];
            double[] cn = copiesRemovingMissingRows.ArraysWithMissingRowsRemoved[4];
            double[] cm = copiesRemovingMissingRows.ArraysWithMissingRowsRemoved[5];
            double[] cs = copiesRemovingMissingRows.ArraysWithMissingRowsRemoved[6];

            string[] title = MakeTitles(parameters, "strata", "stratum {0}", rawRows, out bool hasUserSuppliedLabels);

            int k = copiesRemovingMissingRows.ArraysWithMissingRowsRemoved[0].Length - 1; /* 1-based */
            title = Numerics.Utilities.CopyValidRows(title, copiesRemovingMissingRows.ValidRowsInOriginal, 0, rawRows, 1, k);

            if (proc != 3)
            {
                // single effect analysis
                double[] d = new double[k + 1];
                double[] gj = new double[k + 1];
                double[] lcid = new double[k + 1];
                double[] ucid = new double[k + 1];
                double[] lcig = new double[k + 1];
                double[] ucig = new double[k + 1];
                double[] rkw = new double[k + 1];
                double[] rkx = new double[k + 1];
                //  with g given there are no standard deviations
                Debug.Assert(gotg || null != es);
                bool poolok = k > 1;
                if (!gotg)
                {
                    g = new double[k + 1];
                    for (int i = 1; i <= k; i++)
                    {
                        double n = cn[i] + en[i];
                        if (((en[i] - 1.0) * Math.Pow(es[i], 2.0) + (cn[i] - 1.0) * Math.Pow(cs[i], 2.0)) / (n - 2.0) > 0)
                        {
                            double s = Math.Sqrt(((en[i] - 1.0) * Math.Pow(es[i], 2.0) + (cn[i] - 1.0) * Math.Pow(cs[i], 2.0)) / (n - 2.0));
                            g[i] = (em[i] - cm[i]) / s;
                        }
                        else
                        {
                            g[i] = Constant.MISSING;
                        }
                    }
                }

                double vard;
                for (int i = 1; i <= k; i++)
                {
                    double n = cn[i] + en[i];
                    rkx[i] = n;
                    if (g[i] != Constant.MISSING)
                    {
                        double m = n - 2;
                        gj[i] = Math.Exp(PDF.alogam(m / 2.0) - PDF.alogam((m - 1.0) / 2.0)) / Math.Sqrt(m / 2.0);
                        d[i] = gj[i] * g[i];
                        vard = n / (cn[i] * en[i]) + Math.Pow(d[i], 2.0) / (2.0 * n);
                        lcid[i] = d[i] - cit * Math.Sqrt(vard);
                        ucid[i] = d[i] + cit * Math.Sqrt(vard);
                        double z = Math.Sqrt(cn[i] * en[i] / n);
                        Ginterval(g[i], Convert.ToInt32(n - 2), z, (1.0 - cco) / 2.0, out lcig[i], out ucig[i]);
                    }
                    else
                    {
                        poolok = false;
                        g[i] = Constant.MISSING;
                        gj[i] = Constant.MISSING;
                        d[i] = Constant.MISSING;
                        lcid[i] = Constant.MISSING;
                        ucid[i] = Constant.MISSING;
                        lcig[i] = Constant.MISSING;
                        ucig[i] = Constant.MISSING;
                    }
                }

                ParameterBag outputParameters = new();
                outputParameters.AddOutput("pc", cco * 100);

                IList<ParameterBag> exactList = new List<ParameterBag>();
                outputParameters.AddOutput("*exact", exactList);
                for (int i = 1; i <= k; i++)
                {
                    ParameterBag exactParameters = new();
                    exactList.Add(exactParameters);
                    exactParameters.AddOutput("st", i);
                    exactParameters.AddOutput("gj", gj[i]);
                    exactParameters.AddOutput("g", g[i]);
                    exactParameters.AddOutput("lci", lcig[i]);
                    exactParameters.AddOutput("uci", ucig[i]);
                    exactParameters.AddOutput("lb", hasUserSuppliedLabels ? title[i] : string.Empty);
                }

                IList<ParameterBag> approximateList = new List<ParameterBag>();
                outputParameters.AddOutput("*approximate", approximateList);
                for (int i = 1; i <= k; i++)
                {
                    ParameterBag approximateParameters = new();
                    approximateList.Add(approximateParameters);
                    approximateParameters.AddOutput("st", i);
                    approximateParameters.AddOutput("ne", en[i]);
                    approximateParameters.AddOutput("nc", cn[i]);
                    approximateParameters.AddOutput("d", d[i]);
                    approximateParameters.AddOutput("lci", lcid[i]);
                    approximateParameters.AddOutput("uci", ucid[i]);
                    approximateParameters.AddOutput("lb", hasUserSuppliedLabels ? title[i] : string.Empty);
                }

                IList<ParameterBag> poolOkList = new List<ParameterBag>();
                outputParameters.AddOutput("*poolok", poolOkList);
                // pooled analysis
                double dsd = 0; double dsll = 0; double dsul = 0;
                double dplus = 0; double dplusll = 0; double dplusul = 0;
                double sumwt = 0; double sumdwt = 0;
                double[] hedgesOlkinWeights = new double[k + 1];
                double[] derSimonianLairdWeights = new double[k + 1];

                if (poolok)
                {
                    for (int i = 1; i <= k; i++)
                    {
                        double n = cn[i] + en[i];
                        vard = n / (cn[i] * en[i]) + Math.Pow(d[i], 2.0) / (2.0 * n);
                        double wt = 1.0 / vard;
                        rkw[i] = wt;
                        sumwt += wt;
                        sumdwt += d[i] * wt;
                    }
                    dplus = sumdwt / sumwt;
                    double vardplus = 1 / sumwt;
                    double dplusz = dplus / Math.Sqrt(vardplus);
                    dplusll = dplus - cit * Math.Sqrt(vardplus);
                    dplusul = dplus + cit * Math.Sqrt(vardplus);
                    double qc = 0;
                    sumwt = 0;
                    double sumsqwt = 0;
                    for (int i = 1; i <= k; i++)
                    {
                        double n = cn[i] + en[i];
                        vard = n / (cn[i] * en[i]) + d[i] * d[i] / (2.0 * n);
                        double wt = 1.0 / vard;
                        qc += wt * Math.Pow(d[i] - dplus, 2.0);
                        sumwt += wt;
                        sumsqwt += wt * wt;
                        hedgesOlkinWeights[i] = wt;
                    }
                    double sumHedgesOlkinWeights = sumwt;

                    // DerSimonian-Laird treatment
                    double tausq;
                    if (sumwt - sumsqwt / sumwt == 0.0)
                        tausq = 0.0;
                    else
                        tausq = (qc - Convert.ToDouble(k - 1)) / (sumwt - sumsqwt / sumwt);
                    if (tausq < 0)
                        tausq = 0;
                    sumwt = 0;
                    sumdwt = 0;
                    for (int i = 1; i <= k; i++)
                    {
                        double n = cn[i] + en[i];
                        vard = n / (cn[i] * en[i]) + d[i] * d[i] / (2.0 * n);
                        double wt = 1.0 / vard;
                        wt = 1.0 / (tausq + 1.0 / wt);
                        sumwt += wt;
                        sumdwt += d[i] * wt;
                        derSimonianLairdWeights[i] = wt;
                    }
                    double sumDerSimonianLairdWeights = sumwt;

                    dsd = sumdwt / sumwt;
                    double dsz = sumdwt / Math.Sqrt(sumwt);
                    dsll = dsd - cit / Math.Sqrt(sumwt);
                    dsul = dsd + cit / Math.Sqrt(sumwt);

                    ParameterBag poolOkParameters = new();
                    poolOkList.Add(poolOkParameters);
                    poolOkParameters.AddOutput("dplus", dplus);
                    poolOkParameters.AddOutput("from", dplusll);
                    poolOkParameters.AddOutput("to", dplusul);
                    poolOkParameters.AddOutput("z", dplusz);
                    poolOkParameters.AddOutput("p", MathDbl.zvalp2(dplusz));
                    poolOkParameters.AddOutput("qc", k > 1 ? qc : 0.0);   // 0 by definition with one stratum, not the rounding residue of one squared deviation
                    poolOkParameters.AddOutput("df", k - 1);
                    poolOkParameters.AddOutput("xp", PDF.chivalp(qc, k - 1));
                    poolOkParameters.AddOutput("tausq", tausq);
                    IsquareNcc(host, qc, k, cco, cit, out double isq, out double llisq, out double ulisq);
                    poolOkParameters.AddOutput("isq", isq);
                    poolOkParameters.AddOutput("pc1", cco * 100);
                    poolOkParameters.AddOutput("llisq", llisq);
                    poolOkParameters.AddOutput("ulisq", ulisq);
                    poolOkParameters.AddOutput("dsrd", dsd);
                    poolOkParameters.AddOutput("dsll", dsll);
                    poolOkParameters.AddOutput("dsul", dsul);
                    poolOkParameters.AddOutput("dz", dsz);
                    poolOkParameters.AddOutput("dp", MathDbl.zvalp2(dsz));

                    IList<ParameterBag> weightsList = new List<ParameterBag>();
                    outputParameters.AddOutput("*weights", weightsList);
                    for (int i = 1; i <= k; i++)
                    {
                        ParameterBag weightsParameters = new();
                        weightsList.Add(weightsParameters);
                        weightsParameters.AddOutput("st", i);
                        weightsParameters.AddOutput("howt", 100.0 * hedgesOlkinWeights[i] / sumHedgesOlkinWeights);
                        weightsParameters.AddOutput("dswt", 100.0 * derSimonianLairdWeights[i] / sumDerSimonianLairdWeights);
                        weightsParameters.AddOutput("lb", hasUserSuppliedLabels ? title[i] : string.Empty);
                    }
                }

                IList<ParameterBag> eggerList = new List<ParameterBag>();
                outputParameters.AddOutput("*egger", eggerList);
                ParameterBag eggerParameters = new();
                eggerList.Add(eggerParameters);
                bool biasReported = Metabias(host, eggerParameters, d, lcid, ucid, k, ref cco, Transformation.None);
                FewStrata(outputParameters, eggerList, biasReported);

                IList<ParameterBag> chartList = new List<ParameterBag>();
                outputParameters.AddOutput("*chart", chartList);
                ParameterBag chartParameters;

                if (k > 3)
                {
                    chartParameters = new ParameterBag();
                    chartList.Add(chartParameters);
                    chartParameters.AddOutput("chart", ChartRendererFactory.PrepForLater(ChartType.BiasMA, new BiasMAOptions(d, rkx, rkw, k, "Effect size", lcid, ucid, cco, cit, dplus, Transformation.None, false)));
                }

                chartParameters = new ParameterBag();
                chartList.Add(chartParameters);
                chartParameters.AddOutput("chart", ChartRendererFactory.PrepForLater(ChartType.Effect, new EffectOptions(k, cn, en, title, dplus, dplusll, dplusul, cco, d, lcid, ucid, "Effect size meta-analysis plot [fixed effects]", 1, "effect size")));

                chartParameters = new ParameterBag();
                chartList.Add(chartParameters);
                chartParameters.AddOutput("chart", ChartRendererFactory.PrepForLater(ChartType.Effect, new EffectOptions(k, cn, en, title, dsd, dsll, dsul, cco, d, lcid, ucid, "Effect size meta-analysis plot [random effects]", 1, "effect size")));

                return new StepOutput(outputParameters);
            }
            else
            {
                // single wmd analysis
                double[] d = new double[k + 1];
                double[] lcid = new double[k + 1];
                double[] ucid = new double[k + 1];
                double[] rkw = new double[k + 1];
                double[] rkx = new double[k + 1];
                bool poolok = k > 1;
                for (int i = 1; i <= k; i++)
                {
                    double n = cn[i] + en[i];
                    rkx[i] = n;
                    if (en[i] > 0 & cn[i] > 0 & cs[i] > 0)
                    {
                        // the standard error of the difference is from the variance of each group, as the weights of the pooling are
                        double sed = Math.Sqrt(Math.Pow(es[i], 2.0) / en[i] + Math.Pow(cs[i], 2.0) / cn[i]);
                        d[i] = em[i] - cm[i];
                        lcid[i] = d[i] - cit * sed;
                        ucid[i] = d[i] + cit * sed;
                    }
                    else
                    {
                        d[i] = Constant.MISSING;
                        lcid[i] = Constant.MISSING;
                        ucid[i] = Constant.MISSING;
                        poolok = false;
                    }
                }

                ParameterBag outputParameters = new();
                outputParameters.AddOutput("pc", cco * 100);
                IList<ParameterBag> approximateList = new List<ParameterBag>();
                outputParameters.AddOutput("*approximate", approximateList);
                for (int i = 1; i <= k; i++)
                {
                    ParameterBag approximateParameters = new();
                    approximateList.Add(approximateParameters);
                    approximateParameters.AddOutput("st", i);
                    approximateParameters.AddOutput("ne", en[i]);
                    approximateParameters.AddOutput("nc", cn[i]);
                    approximateParameters.AddOutput("d", d[i]);
                    approximateParameters.AddOutput("lci", lcid[i]);
                    approximateParameters.AddOutput("uci", ucid[i]);
                    approximateParameters.AddOutput("lb", hasUserSuppliedLabels ? title[i] : string.Empty);
                }


                IList<ParameterBag> poolOkList = new List<ParameterBag>();
                outputParameters.AddOutput("*poolok", poolOkList);
                // pooled wmd analysis
                double dsd = 0; double dsll = 0; double dsul = 0;
                double dplus = 0; double dplusll = 0; double dplusul = 0;
                if (poolok)
                {
                    double sumwt = 0;
                    double sumdwt = 0;
                    for (int i = 1; i <= k; i++)
                    {
                        double wt = 1.0 / (Math.Pow(es[i], 2.0) / en[i] + Math.Pow(cs[i], 2.0) / cn[i]);
                        rkw[i] = wt;
                        sumwt += wt;
                        sumdwt += d[i] * wt;
                    }
                    dplus = sumdwt / sumwt;
                    double vardplus = 1.0 / sumwt;
                    double dplusz = dplus / Math.Sqrt(vardplus);
                    dplusll = dplus - cit * Math.Sqrt(vardplus);
                    dplusul = dplus + cit * Math.Sqrt(vardplus);
                    double qc = 0;
                    sumwt = 0;
                    double sumsqwt = 0;
                    for (int i = 1; i <= k; i++)
                    {
                        double wt = 1.0 / (Math.Pow(es[i], 2.0) / en[i] + Math.Pow(cs[i], 2.0) / cn[i]);
                        qc += wt * Math.Pow(d[i] - dplus, 2.0);
                        sumwt += wt;
                        sumsqwt += wt * wt;
                    }

                    // DerSimonian-Laird treatment
                    double tausq;
                    if (sumwt - sumsqwt / sumwt == 0.0)
                        tausq = 0.0;
                    else
                        tausq = (qc - Convert.ToDouble(k - 1)) / (sumwt - sumsqwt / sumwt);
                    if (tausq < 0)
                        tausq = 0;
                    sumwt = 0;
                    sumdwt = 0;
                    for (int i = 1; i <= k; i++)
                    {
                        double wt = 1.0 / (Math.Pow(es[i], 2.0) / en[i] + Math.Pow(cs[i], 2.0) / cn[i]);
                        wt = 1.0 / (tausq + 1.0 / wt);
                        sumwt += wt;
                        sumdwt += d[i] * wt;
                    }
                    dsd = sumdwt / sumwt;
                    double dsz = sumdwt / Math.Sqrt(sumwt);
                    dsll = dsd - cit / Math.Sqrt(sumwt);
                    dsul = dsd + cit / Math.Sqrt(sumwt);

                    ParameterBag poolOkParameters = new();
                    poolOkList.Add(poolOkParameters);
                    poolOkParameters.AddOutput("dplus", dplus);
                    poolOkParameters.AddOutput("from", dplusll);
                    poolOkParameters.AddOutput("to", dplusul);
                    poolOkParameters.AddOutput("z", dplusz);
                    poolOkParameters.AddOutput("p", MathDbl.zvalp2(dplusz));
                    poolOkParameters.AddOutput("qc", k > 1 ? qc : 0.0);   // 0 by definition with one stratum, not the rounding residue of one squared deviation
                    poolOkParameters.AddOutput("df", k - 1);
                    poolOkParameters.AddOutput("xp", PDF.chivalp(qc, k - 1));
                    poolOkParameters.AddOutput("tausq", tausq);
                    IsquareNcc(host, qc, k, cco, cit, out double isq, out double llisq, out double ulisq);
                    poolOkParameters.AddOutput("isq", isq);
                    poolOkParameters.AddOutput("pc1", cco * 100);
                    poolOkParameters.AddOutput("llisq", llisq);
                    poolOkParameters.AddOutput("ulisq", ulisq);
                    poolOkParameters.AddOutput("dsrd", dsd);
                    poolOkParameters.AddOutput("dsll", dsll);
                    poolOkParameters.AddOutput("dsul", dsul);
                    poolOkParameters.AddOutput("dz", dsz);
                    poolOkParameters.AddOutput("zp", MathDbl.zvalp2(dsz));
                }

                IList<ParameterBag> eggerList = new List<ParameterBag>();
                outputParameters.AddOutput("*egger", eggerList);
                ParameterBag eggerParameters = new();
                eggerList.Add(eggerParameters);
                bool biasReported = Metabias(host, eggerParameters, d, lcid, ucid, k, ref cco, Transformation.None);
                FewStrata(outputParameters, eggerList, biasReported);

                IList<ParameterBag> chartList = new List<ParameterBag>();
                outputParameters.AddOutput("*chart", chartList);
                ParameterBag chartParameters;

                if (k > 3)
                {
                    chartParameters = new ParameterBag();
                    chartList.Add(chartParameters);
                    chartParameters.AddOutput("chart", ChartRendererFactory.PrepForLater(ChartType.BiasMA, new BiasMAOptions(d, rkx, rkw, k, "Effect size", lcid, ucid, cco, cit, dplus, Transformation.None, false)));
                }

                // bool bfault = false; 
                chartParameters = new ParameterBag();
                chartList.Add(chartParameters);
                chartParameters.AddOutput("chart", ChartRendererFactory.PrepForLater(ChartType.Effect, new EffectOptions(k, cn, en, title, dplus, dplusll, dplusul, cco, d, lcid, ucid, "Effect size meta-analysis plot [fixed effects]", 1, "weighted mean difference")));

                chartParameters = new ParameterBag();
                chartList.Add(chartParameters);
                chartParameters.AddOutput("chart", ChartRendererFactory.PrepForLater(ChartType.Effect, new EffectOptions(k, cn, en, title, dsd, dsll, dsul, cco, d, lcid, ucid, "Effect size meta-analysis plot [random effects]", 1, "weighted mean difference")));

                return new StepOutput(outputParameters);
            }
        }

        /// <summary>
        /// The exact confidence limits of g.  t = g z has the non-central t distribution on df degrees of freedom whose
        /// non-centrality is z times the effect size, z being the root of n1 n2 / (n1 + n2).  The lower limit is the effect size at
        /// which the distribution function at t is 1 - alpha, and the upper that at which it is alpha.
        /// </summary>
        /// <param name="g">The difference between the means over the pooled standard deviation.</param>
        /// <param name="df">The degrees of freedom, n1 + n2 - 2.</param>
        /// <param name="z">The root of n1 n2 / (n1 + n2).</param>
        /// <param name="alpha">The probability in each tail.</param>
        /// <param name="lcig">On return, the lower limit; missing if it was not found.</param>
        /// <param name="ucig">On return, the upper limit; missing if it was not found.</param>
        private static void Ginterval(double g, int df, double z, double alpha, out double lcig, out double ucig)
        {
            double t = g * z;
            double lower = NoncentralityOfT(t, df, 1.0 - alpha);
            double upper = NoncentralityOfT(t, df, alpha);
            lcig = lower == Constant.MISSING ? Constant.MISSING : lower / z;
            ucig = upper == Constant.MISSING ? Constant.MISSING : upper / z;
        }

        /// <summary>
        /// The non-centrality at which the non-central t distribution function at t, on df degrees of freedom, is p.  The function
        /// falls as the non-centrality rises: an interval about t is widened until the function is above p at its lower end and below
        /// p at its upper, and is then halved until its ends meet.
        /// </summary>
        /// <returns>The non-centrality, or the missing value if the distribution function could not be worked out.</returns>
        private static double NoncentralityOfT(double t, int df, double p)
        {
            double Difference(double delta)
            {
                double v = ExFortran.pnct(t, df, delta, out int fault);
                return fault != 0 || v == Constant.MISSING || double.IsNaN(v) ? double.NaN : v - p;
            }

            double width = Math.Max(1.0, Math.Abs(t));
            double lo = t - width;
            double hi = t + width;
            double flo = Difference(lo);
            double fhi = Difference(hi);
            for (int i = 0; i < 60 && !(flo > 0.0 && fhi < 0.0); i++)
            {
                if (double.IsNaN(flo) || double.IsNaN(fhi))
                    return Constant.MISSING;
                width *= 2.0;
                if (!(flo > 0.0))
                {
                    lo = t - width;
                    flo = Difference(lo);
                }
                if (!(fhi < 0.0))
                {
                    hi = t + width;
                    fhi = Difference(hi);
                }
            }
            if (!(flo > 0.0 && fhi < 0.0))
                return Constant.MISSING;
            for (int i = 0; i < 200 && hi - lo > 1.0E-13 * Math.Max(1.0, Math.Abs(lo) + Math.Abs(hi)); i++)
            {
                double mid = 0.5 * (lo + hi);
                double fm = Difference(mid);
                if (double.IsNaN(fm))
                    return Constant.MISSING;
                if (fm > 0.0)
                    lo = mid;
                else
                    hi = mid;
            }
            return 0.5 * (lo + hi);
        }

        /// <summary>
        /// The relative risk of each table and the pooled relative risk of Mantel and Haenszel, for the meta-analysis and for the
        /// crosstabs with strata.  The relative risk of a table is (a / (a + c)) / (b / (b + d)); a table with a cell of nothing
        /// has the continuity correction in every cell first (ContinuityCorrect).  The pooled relative risk is the sum of
        /// a (b + d) / n over the sum of b (a + c) / n, the terms of the denominator being the weights; the variance of its logarithm
        /// is the sum of ((a + b)(a + c)(b + d) - a b n) / n^2 over the product of the two sums.  With the continuity correction
        /// delayed the pooling is from the counts as they are, if there is an event in each group among the tables.
        /// Q and the random effects figures are from the logarithm of the relative risk of each table with its variance
        /// 1 / a + 1 / b - 1 / (a + c) - 1 / (b + d), and Q is about the pooled relative risk of Mantel and Haenszel.
        /// </summary>
        /// <param name="host">The preferences: the exact method (Koopman's limits for each table), the continuity correction.</param>
        /// <param name="lowerBound">The element at which the tables start.</param>
        /// <param name="k">The number of tables.</param>
        /// <param name="realk">On return, the number of tables that are pooled.</param>
        /// <param name="o">The tables (see the summary of the class).</param>
        /// <param name="rmh">On return, the pooled relative risk.</param>
        /// <param name="ll">On return, its lower limit.</param>
        /// <param name="ul">On return, its upper limit.</param>
        /// <param name="x2Rmh">On return, the square of the logarithm of the pooled relative risk over its standard error.</param>
        /// <param name="cit">The normal deviate of the confidence level.</param>
        /// <param name="rkr">On return, the relative risk of each table.</param>
        /// <param name="rkw">On return, the fixed effects weight of each table.</param>
        /// <param name="dsw">On return, the random effects weight of each table.</param>
        /// <param name="rkrl">On return, the lower limit of each table.</param>
        /// <param name="rkru">On return, the upper limit of each table.</param>
        /// <param name="rkx">On return, the total of each table.</param>
        /// <param name="lerr">On return, whether the lower limit of each table is missing.</param>
        /// <param name="uerr">On return, whether the upper limit of each table is missing.</param>
        /// <param name="qc">On return, Cochran's Q.</param>
        /// <param name="dsrr">On return, the random effects pooled relative risk.</param>
        /// <param name="dsx2">On return, the square of its logarithm over its standard error.</param>
        /// <param name="dsll">On return, its lower limit.</param>
        /// <param name="dsul">On return, its upper limit.</param>
        /// <param name="tausq">On return, the variance between the tables.</param>
        /// <param name="cced">On return, whether each table had the continuity correction.</param>
        /// <param name="included">On return, whether each table is pooled (see IncludeRelativeRisk).</param>
        /// <param name="ierr">On return, 0.</param>
        public static void RelativeRiskMA(IPreferences host, int lowerBound, int k, out int realk, double[,] o, out double rmh, out double ll, out double ul, out double x2Rmh, double cit, out double[] rkr, out double[] rkw, out double[] dsw, out double[] rkrl, out double[] rkru, out double[] rkx, out bool[] lerr, out bool[] uerr, out double qc, out double dsrr, out double dsx2, out double dsll, out double dsul, out double tausq, out bool[] cced, out bool[] included, out int ierr)
        {
            ierr = -1;
            double siga = 0.0;
            double sumwt = 0.0;
            double svd1 = 0.0;
            double svd2 = 0.0;
            double svd3 = 0.0;
            realk = 0;

            rkr = new double[k + lowerBound];
            rkw = new double[k + lowerBound];
            dsw = new double[k + lowerBound];
            rkrl = new double[k + lowerBound];
            rkru = new double[k + lowerBound];
            rkx = new double[k + lowerBound];
            lerr = new bool[k + lowerBound];
            uerr = new bool[k + lowerBound];
            cced = new bool[k + lowerBound];
            included = new bool[k + lowerBound];

            // With the continuity correction delayed, the pooled relative risk of Mantel and Haenszel is from the counts as they are,
            // which it can be if there is an event in each group among the studies
            bool raw = false;
            if (host.Preferences.DelayContinuityCorrection)
            {
                bool eventsFirst = false;
                bool eventsSecond = false;
                for (int i = lowerBound; i < lowerBound + k; i++)
                {
                    if (IncludeRelativeRisk(o, i))
                    {
                        if (o[i, 1] > 0.0)
                            eventsFirst = true;
                        if (o[i, 2] > 0.0)
                            eventsSecond = true;
                    }
                }
                raw = eventsFirst && eventsSecond;
            }

            for (int i = lowerBound; i < lowerBound + k; i++)
            {
                double a = o[i, 1];
                double b = o[i, 2];
                double c = o[i, 3];
                double d = o[i, 4];
                double n = a + b + c + d;
                rkx[i] = n;
                if (n <= 0)
                    throw new InvalidDataException();

                included[i] = IncludeRelativeRisk(o, i);
                if (included[i])
                {
                    realk++;

                    if (host.Preferences.MetaExact)
                    {
                        // try Koopman rr and ci for stratum before continuity correction
                        MathDbl.lr_ci(b, a, b + d, a + c, cit, out rkrl[i], out rkru[i]);
                        lerr[i] = rkrl[i] == Constant.MISSING;
                        uerr[i] = rkru[i] == Constant.MISSING;
                    }

                    // get rr continuity corrected if neccessary
                    if (a <= 0.0 || b <= 0.0 || c <= 0.0 || d <= 0.0)
                    {
                        cced[i] = true;
                        ContinuityCorrect(host, a, b, c, d, out a, out b, out c, out d);
                        // the total is that of the corrected cells
                        n = a + b + c + d;
                    }
                    else
                    {
                        cced[i] = false;
                    }
                    rkr[i] = a / (a + c) / (b / (b + d));
                    if (!host.Preferences.MetaExact)
                    {
                        // approximate se of log rr
                        double selogrr = Math.Sqrt(1.0 / a + 1.0 / b - 1.0 / (a + c) - 1.0 / (b + d));
                        rkrl[i] = Math.Exp(Math.Log(rkr[i]) - selogrr * cit);
                        rkru[i] = Math.Exp(Math.Log(rkr[i]) + selogrr * cit);
                        lerr[i] = false;
                        uerr[i] = false;
                    }

                    if (raw)
                    {
                        a = o[i, 1];
                        b = o[i, 2];
                        c = o[i, 3];
                        d = o[i, 4];
                        n = a + b + c + d;
                    }
                    //  Rothman-Boice combined risk ratio
                    double weight = b * (a + c) / n;
                    rkw[i] = weight;
                    sumwt += weight;
                    siga += a * (b + d) / n;
                    // Greenland-Robins variance
                    svd1 += ((a + b) * (a + c) * (b + d) - a * b * n) / Math.Pow(n, 2.0);
                    svd2 += a * (b + d) / n;
                    svd3 += b * (a + c) / n;

                }
                else
                {
                    rkr[i] = Constant.MISSING;
                    rkrl[i] = 0.0;
                    rkru[i] = double.PositiveInfinity;
                    lerr[i] = false;
                    uerr[i] = false;
                }

            }

            rmh = siga / sumwt;
            double serr = svd1 / (svd2 * svd3);
            ll = Math.Exp(Math.Log(rmh) - Math.Sqrt(serr * cit * cit));
            ul = Math.Exp(Math.Log(rmh) + Math.Sqrt(serr * cit * cit));
            if (ll > ul)
                Utilities.Utilities.Swap(ref ll, ref ul);
            x2Rmh = Math.Pow(Math.Log(rmh) / Math.Sqrt(serr), 2.0);

            // Q (combinability)
            qc = 0.0;
            sumwt = 0.0;
            double sumsqwt = 0.0;
            for (int i = lowerBound; i < lowerBound + k; i++)
            {
                if (included[i])
                {
                    double a = o[i, 1];
                    double b = o[i, 2];
                    double c = o[i, 3];
                    double d = o[i, 4];
                    if (a <= 0.0 || b <= 0.0 || c <= 0.0 || d <= 0.0)
                        ContinuityCorrect(host, a, b, c, d, out a, out b, out c, out d);

                    double n = a + b + c + d;
                    // Weight = b * ( a + C ) / N; - unused
                    // using weight as 1/variance
                    svd1 = ((a + b) * (a + c) * (b + d) - a * b * n) / Math.Pow(n, 2.0);
                    svd2 = a * (b + d) / n;
                    svd3 = b * (a + c) / n;
                    double wt = 1.0 / (svd1 / (svd2 * svd3));
                    double lrri = Math.Log(a / (a + c) / (b / (b + d)));
                    qc += wt * Math.Pow(lrri - Math.Log(rmh), 2.0);
                    sumwt += wt;
                    sumsqwt += wt * wt;
                }
            }

            // DerSimonian-Laird random effects
            if (sumwt - sumsqwt / sumwt == 0.0)
                tausq = 0.0;
            else
                tausq = (qc - Convert.ToDouble(realk - 1)) / (sumwt - sumsqwt / sumwt);
            if (tausq < 0.0)
                tausq = 0.0;
            double wlrr = 0.0;
            sumwt = 0.0;
            // sumsqwt = 0.0; 
            for (int i = lowerBound; i < lowerBound + k; i++)
            {
                if (included[i])
                {
                    double a = o[i, 1];
                    double b = o[i, 2];
                    double c = o[i, 3];
                    double d = o[i, 4];
                    if (a <= 0.0 || b <= 0.0 || c <= 0.0 || d <= 0.0)
                        ContinuityCorrect(host, a, b, c, d, out a, out b, out c, out d);
                    double n = a + b + c + d;
                    // using weight as 1/var
                    svd1 = ((a + b) * (a + c) * (b + d) - a * b * n) / Math.Pow(n, 2.0);
                    svd2 = a * (b + d) / n;
                    svd3 = b * (a + c) / n;
                    double wt = 1.0 / (svd1 / (svd2 * svd3));
                    double weight = 1.0 / (tausq + 1.0 / wt);
                    dsw[i] = weight;
                    double lrri = Math.Log(a / (a + c) / (b / (b + d)));
                    wlrr += lrri * weight;
                    sumwt += weight;
                }
            }
            dsrr = Math.Exp(wlrr / sumwt);
            dsx2 = Math.Pow(wlrr, 2.0) / sumwt;
            dsll = Math.Exp(wlrr / sumwt - cit / Math.Sqrt(sumwt));
            dsul = Math.Exp(wlrr / sumwt + cit / Math.Sqrt(sumwt));
            if (dsll > dsul)
                Utilities.Utilities.Swap(ref dsll, ref dsul);
            ierr = 0;
        }

        /// <summary>
        /// The risk difference of each study, a / (a + c) - b / (b + d), and the pooled risk difference of Mantel and Haenszel: the
        /// mean of the differences with the weights (a + c)(b + d) / n, whose variance is the sum of
        /// (a c (b + d)^3 + b d (a + c)^3) / ((a + c)(b + d) n^2) over the square of the sum of the weights.  The variance of the
        /// difference of a study is a c / (a + c)^3 + b d / (b + d)^3.  A study with a cell of nothing has the continuity correction
        /// in every cell for its variance and, unless the correction is delayed, for its weight and its part of the variance of the
        /// pooled difference; the difference itself is from the counts as they are.  Q is about the pooled difference of Mantel and
        /// Haenszel.  A study with a group of nobody is left out.
        /// </summary>
        /// <param name="wll">On return, the difference of each study less z standard errors: for the bias indicators.</param>
        /// <param name="wul">On return, the difference of each study plus z standard errors.</param>
        /// <param name="included">On return, whether each study is pooled.</param>
        /// <param name="realk">On return, the number of studies that are pooled.</param>
        /// <remarks>The other parameters are as those of RelativeRiskMA.  With the exact method the limits of each study are
        /// those of Miettinen (MathDbl.uppci).</remarks>
        private static void Riskdifma(IPreferences host, int k, double[,] o, out double rmh, out double ll, out double ul, out double x2Rmh, double cit, double cco, double[] rkr, double[] rkw, double[] dsw, double[] rkrl, double[] rkru, double[] rkx, bool[] lerr, bool[] uerr, out double qc, out double dsrd, out double dsx2, out double dsll, out double dsul, out double tausq, bool[] cced, bool[] included, double[] wll, double[] wul, out int realk, out int ierr)
        {
            ierr = -1;
            realk = 0;
            double sumlk = 0.0;
            double mhn = 0.0;
            double mhd = 0.0;
            double[] vark = new double[k + 1];
            // With the continuity correction delayed, the pooled risk difference of Mantel and Haenszel, its weights and its variance
            // are from the counts as they are; otherwise from the corrected counts of the studies with a cell of nothing
            bool raw = host.Preferences.DelayContinuityCorrection;
            for (int i = 1; i <= k; i++)
            {
                double a = o[i, 1];
                double b = o[i, 2];
                double c = o[i, 3];
                double d = o[i, 4];
                double n = a + b + c + d;
                rkx[i] = n;
                if (n <= 0)
                    throw new InvalidDataException();

                // rd and ci for stratum: a group of nobody has no risk, and its study is left out
                included[i] = a + c > 0.0 && b + d > 0.0;
                if (!included[i])
                {
                    rkr[i] = Constant.MISSING;
                    rkrl[i] = Constant.MISSING;
                    rkru[i] = Constant.MISSING;
                    wll[i] = Constant.MISSING;
                    wul[i] = Constant.MISSING;
                    lerr[i] = true;
                    uerr[i] = true;
                    cced[i] = false;
                    rkw[i] = 0.0;
                    dsw[i] = 0.0;
                    continue;
                }
                realk++;
                rkr[i] = a / (a + c) - b / (b + d);
                if (host.Preferences.MetaExact)
                {
                    double r1 = a;
                    double n1 = a + c;
                    double r2 = b;
                    double n2 = b + d;
                    MathDbl.uppci(Convert.ToInt32(r1), Convert.ToInt32(n1), Convert.ToInt32(r2), Convert.ToInt32(n2), out rkrl[i], out rkru[i], cit, 100.0 * cco);
                }

                if (a <= 0.0 || b <= 0.0 || c <= 0.0 || d <= 0.0)
                {
                    ContinuityCorrect(host, a, b, c, d, out a, out b, out c, out d);
                    n = a + b + c + d;
                    cced[i] = true;
                }
                else
                {
                    cced[i] = false;
                }
                // the variance of the risk difference of the study, and the limits that go with it
                vark[i] = a * c / Math.Pow(a + c, 3.0) + b * d / Math.Pow(b + d, 3.0);
                double se = Math.Sqrt(vark[i]);
                wll[i] = rkr[i] - cit * se;
                wul[i] = rkr[i] + cit * se;
                if (!host.Preferences.MetaExact)
                {
                    rkrl[i] = wll[i];
                    rkru[i] = wul[i];
                }
                lerr[i] = rkrl[i] == Constant.MISSING;
                uerr[i] = rkru[i] == Constant.MISSING;

                if (raw)
                {
                    a = o[i, 1];
                    b = o[i, 2];
                    c = o[i, 3];
                    d = o[i, 4];
                    n = a + b + c + d;
                }
                //  rd across strata: the weights of Mantel and Haenszel
                double nmn = (a + c) * (b + d) / n;
                rkw[i] = nmn;
                mhn += rkr[i] * nmn;
                mhd += nmn;
                //  Greenland-Robins pooled risk difference
                double lk = (a * c * Math.Pow(b + d, 3.0) + b * d * Math.Pow(a + c, 3.0)) / ((a + c) * (b + d) * Math.Pow(n, 2.0));
                sumlk += lk;
            }
            if (realk == 0)
                throw new TemplateOperationCancelledException("No study has subjects in both groups: there is nothing to pool.", "Risk difference meta-analysis");

            rmh = mhn / mhd;
            double serd = Math.Sqrt(sumlk / Math.Pow(mhd, 2.0));
            ll = rmh - serd * cit;
            ul = rmh + serd * cit;
            if (ll > ul)
                Utilities.Utilities.Swap(ref ll, ref ul);
            // without an event, or without a non-event, in any study the counts as they are give the pooled difference no variance
            x2Rmh = serd > 0.0 ? Math.Pow(rmh / serd, 2.0) : Constant.MISSING;
            // Q (combinability)
            qc = 0.0;
            double sumwt = 0.0;
            double sumsqwt = 0.0;
            for (int i = 1; i <= k; i++)
            {
                if (!included[i])
                    continue;
                double wt = 1.0 / vark[i];
                qc += wt * Math.Pow(rkr[i] - rmh, 2.0);
                sumwt += wt;
                sumsqwt += wt * wt;
            }
            // DerSimonian-Laird random effects
            if (sumwt - sumsqwt / sumwt == 0.0)
            {
                tausq = 0.0;
            }
            else
            {
                tausq = (qc - (realk - 1.0)) / (sumwt - sumsqwt / sumwt);
            }
            if (tausq < 0.0)
            {
                tausq = 0.0;
            }
            double wrd = 0.0;
            sumwt = 0.0;
            for (int i = 1; i <= k; i++)
            {
                if (!included[i])
                    continue;
                double weight = 1.0 / (tausq + vark[i]);
                dsw[i] = weight;
                wrd += rkr[i] * weight;
                sumwt += weight;
            }
            dsrd = wrd / sumwt;
            dsx2 = Math.Pow(wrd, 2.0) / sumwt;
            dsll = wrd / sumwt - cit / Math.Sqrt(sumwt);
            dsul = wrd / sumwt + cit / Math.Sqrt(sumwt);
            if (dsll > dsul)
                Utilities.Utilities.Swap(ref dsll, ref dsul);
            ierr = 0;
        }

        /// <summary>
        /// Which of the two comparisons of incidence rates is wanted.
        /// </summary>
        private enum MetaIncidenceRateMode
        {
            Difference = 1,
            Ratio = 2
        }

        public static StepOutput RptMetaIncidenceRateRatio(IPreferencesAndProgressBar host, ParameterBag parameters) => RptMetaIncidenceRate(host, parameters, MetaIncidenceRateMode.Ratio);

        public static StepOutput RptMetaIncidenceRateDifference(IPreferencesAndProgressBar host, ParameterBag parameters) => RptMetaIncidenceRate(host, parameters, MetaIncidenceRateMode.Difference);

        /// <summary>
        /// Incidence rate meta-analysis: the report of the rate difference (IrdMeta) or of the rate ratio (IrrMeta).  With the exact
        /// method the rate ratio also has the estimate by conditional maximum likelihood with its exact limits: the events of the
        /// first group of a study, given the events of both groups, have the binomial distribution whose odds are the rate ratio
        /// times the person-time of the first group over that of the second.
        /// </summary>
        /// <param name="host">The preferences, and where progress is shown.</param>
        /// <param name="parameters">"a" and "pt1": the events and the person-time of the first group of each study; "b" and "pt2":
        /// those of the second; "strata" (may be left out); "gamma".</param>
        /// <param name="mode">The difference or the ratio.</param>
        private static StepOutput RptMetaIncidenceRate(IPreferencesAndProgressBar host, ParameterBag parameters, MetaIncidenceRateMode mode)
        {
            double cco = parameters["gamma"].AsDouble;
            if (cco <= 0)
                cco = 0.95;
            double cit = PDF.gauinv(1.0 - (1.0 - cco) / 2.0);

            DataFrame aFrame = parameters["a"].AsDataFrame;
            DoubleVariable aVariable = (DoubleVariable)aFrame.Variables[0];
            DataFrame pt1Frame = parameters["pt1"].AsDataFrame;
            DoubleVariable pt1Variable = (DoubleVariable)pt1Frame.Variables[0];
            DataFrame bFrame = parameters["b"].AsDataFrame;
            DoubleVariable bVariable = (DoubleVariable)bFrame.Variables[0];
            DataFrame pt2Frame = parameters["pt2"].AsDataFrame;
            DoubleVariable pt2Variable = (DoubleVariable)pt2Frame.Variables[0];
            int rawRows = aVariable.Length;

            DoubleArraysAndBooleans copiesRemovingMissingRows = Numerics.Utilities.RemoveMissingRows(new[] { aVariable.Data, pt1Variable.Data, bVariable.Data, pt2Variable.Data }, 0, rawRows, 1);
            double[] a = copiesRemovingMissingRows.ArraysWithMissingRowsRemoved[0];
            double[] pt1 = copiesRemovingMissingRows.ArraysWithMissingRowsRemoved[1];
            double[] b = copiesRemovingMissingRows.ArraysWithMissingRowsRemoved[2];
            double[] pt2 = copiesRemovingMissingRows.ArraysWithMissingRowsRemoved[3];

            string[] title = MakeTitles(parameters, "strata", "stratum {0}", rawRows, out bool hasUserSuppliedLabels);

            int k = copiesRemovingMissingRows.ArraysWithMissingRowsRemoved[0].Length - 1; /* 1-based */
            title = Numerics.Utilities.CopyValidRows(title, copiesRemovingMissingRows.ValidRowsInOriginal, 0, rawRows, 1, k);

            double[,] o = new double[k + 1, 4 + 1];
            double[] rkr = new double[k + 1];
            double[] rkw = new double[k + 1];
            double[] dsw = new double[k + 1];
            double[] rkrl = new double[k + 1];
            double[] rkru = new double[k + 1];
            bool[] lerr = new bool[k + 1];
            bool[] uerr = new bool[k + 1];
            bool[] included = new bool[k + 1];
            double zrmh; double ul; double ll; double rmh;
            double dz; double dsird = 0; double dsirr = 0; double qc;
            double dsul; double dsll; double tausq;
            int ierr; double realk;

            // The number that is added to the events of both groups of a study without an event in one of its groups: that of the
            // preference if it is a number between 0 and 1, otherwise a half (the treatment arm correction is of the sizes of groups)
            double cc = host.Preferences.MetaCC > 0.0 && host.Preferences.MetaCC < 1.0 ? host.Preferences.MetaCC : 0.5;
            bool[] cced = new bool[k + 1];
            if (mode == MetaIncidenceRateMode.Difference)
                IrdMeta(k, a, b, pt1, pt2, cc, cced, out rmh, out ll, out ul, out zrmh, ref cit, ref cco, rkr, rkw, dsw, rkrl, rkru, lerr, uerr, out qc, out dsird, out dz, out dsll, out dsul, out realk, out tausq, out ierr);
            else
                IrrMeta(k, a, b, pt1, pt2, cc, cced, out rmh, out ll, out ul, out zrmh, ref cit, ref cco, rkr, rkw, dsw, rkrl, rkru, lerr, uerr, out qc, out dsirr, out dz, out dsll, out dsul, out realk, out tausq, out ierr);
            if (ierr == -1)
                throw new InvalidDataException();
            if (realk == 0.0)
                throw new TemplateOperationCancelledException("None of the studies can be pooled: each of them has no events, or a group with no person-time.", mode == MetaIncidenceRateMode.Difference ? "Incidence rate difference meta-analysis" : "Incidence rate ratio meta-analysis");

            double p2M = 0; double p1M = 0; double p2F = 0; double p1F = 0; double llm = 0; double ulm = 0; double llf = 0; double ulf = 0; double eor = 0;
            if (mode == MetaIncidenceRateMode.Ratio)
            {
                // Try exact IRR
                if (host.Preferences.MetaExact)
                {
                    Rec2X2[] tbl = new Rec2X2[k + 1];
                    for (int i = 1; i <= k; i++)
                    {
                        tbl[i].Freq = 1;
                        tbl[i].A = a[i];
                        tbl[i].M1 = b[i] + a[i];
                        tbl[i].N1 = pt1[i];
                        tbl[i].N0 = pt2[i];
                        tbl[i].IsInformative = (a[i] * pt1[i] != 0.0) | (b[i] * pt2[i] != 0.0);
                    }
                    bool useLogScale = false;
                    // person-time data: the conditional distribution of a given a + b is binomial with the person-times as its odds
                    new ExactBB().Exact22K(host, 1, k, Exact22KDataType.Type3, tbl, cco, out eor, out ulf, out llf, out ulm, out llm, out p1F, out p2F, out p1M, out p2M, ref useLogScale, out ierr);
                }
                else
                {
                    ierr = -9;
                }
                if (ierr != 0)
                {
                    eor = Constant.MISSING;
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

            ParameterBag outputParameters = new();
            outputParameters.AddOutput("pc", cco * 100);

            IList<ParameterBag> inputsList = new List<ParameterBag>();
            outputParameters.AddOutput("*inputs", inputsList);
            for (int i = 1; i <= k; i++)
            {
                ParameterBag inputsParameters = new();
                inputsList.Add(inputsParameters);
                inputsParameters.AddOutput("st", i);
                inputsParameters.AddOutput("a", a[i]);
                inputsParameters.AddOutput("pt1", pt1[i]);
                inputsParameters.AddOutput("b", b[i]);
                inputsParameters.AddOutput("pt2", pt2[i]);
                // a study that is left out is marked, and so is one that has the continuity correction, as in the other reports
                string label = hasUserSuppliedLabels ? title[i] : string.Empty;
                if (rkr[i] == Constant.MISSING)
                    label = "* (excluded)";
                else if (cced[i])
                    label += " [CC = " + cc.ToString() + "]";
                inputsParameters.AddOutput("lb", label);
            }

            IList<ParameterBag> irList = new List<ParameterBag>();
            outputParameters.AddOutput("*ir", irList);
            for (int i = 1; i <= k; i++)
            {
                ParameterBag irParameters = new();
                irList.Add(irParameters);
                irParameters.AddOutput("st", i);
                irParameters.AddOutput(mode == MetaIncidenceRateMode.Difference ? "ird" : "irr", rkr[i]);
                irParameters.AddOutput("lci", rkrl[i]);
                irParameters.AddOutput("uci", rkru[i]);
                irParameters.AddOutput("wt",
                    rkw[i] != Constant.MISSING
                        ? 100 * rkw[i] / Formatting.dsum(rkw, 1)
                        : Constant.MISSING);
                irParameters.AddOutput("dwt",
                    dsw[i] != Constant.MISSING
                        ? 100 * dsw[i] / Formatting.dsum(dsw, 1)
                        : Constant.MISSING);
                irParameters.AddOutput("lb", rkr[i] == Constant.MISSING ? "* (excluded)" : hasUserSuppliedLabels ? title[i] : string.Empty);
                if (rkr[i] == Constant.MISSING)
                {
                    irParameters.AddOutput("yi", Constant.MISSING);
                    irParameters.AddOutput("vi", Constant.MISSING);
                }
                else if (mode == MetaIncidenceRateMode.Difference)
                {
                    irParameters.AddOutput("yi", rkr[i]);
                    irParameters.AddOutput("vi", VarianceFromCI(rkrl[i], rkru[i], cit, false));
                }
                else
                {
                    irParameters.AddOutput("yi", rkr[i] > 0 ? Math.Log(rkr[i]) : 0);
                    irParameters.AddOutput("vi", 1.0 / rkw[i]);   // the variance of the logarithm of the rate ratio, which the weight is 1 over
                }

            }
            outputParameters.AddOutput("rmh", rmh);
            outputParameters.AddOutput("from", ll);
            outputParameters.AddOutput("to", ul);
            outputParameters.AddOutput("z", zrmh);
            outputParameters.AddOutput("p_z", MathDbl.zvalp2(zrmh));
            if (mode == MetaIncidenceRateMode.Ratio)
            {
                if (ierr == -9)
                {
                    outputParameters.AddOutput("*poolok", null);
                }
                else
                {
                    IList<ParameterBag> poolokList = new List<ParameterBag>();
                    outputParameters.AddOutput("*poolok", poolokList);
                    ParameterBag poolokParameters = new();
                    poolokList.Add(poolokParameters);
                    poolokParameters.AddOutput("eor", eor);
                    poolokParameters.AddOutput("llf", llf);
                    poolokParameters.AddOutput("ulf", ulf);
                    poolokParameters.AddOutput("p1f", p1F);
                    poolokParameters.AddOutput("p2f", p2F);
                    poolokParameters.AddOutput("llm", llm);
                    poolokParameters.AddOutput("ulm", ulm);
                    poolokParameters.AddOutput("p1m", p1M);
                    poolokParameters.AddOutput("p2m", p2M);
                }
            }

            outputParameters.AddOutput("qc", realk > 1 ? qc : 0.0);   // 0 by definition with one stratum, not the rounding residue of one squared deviation
            outputParameters.AddOutput("df", realk - 1);
            outputParameters.AddOutput("xp", PDF.chivalp(qc, realk - 1));
            outputParameters.AddOutput("tausq", tausq);
            IsquareNcc(host, qc, Convert.ToInt32(realk), cco, cit, out double isq, out double llisq, out double ulisq);
            outputParameters.AddOutput("isq", isq);
            outputParameters.AddOutput("pc1", cco * 100);
            outputParameters.AddOutput("llisq", llisq);
            outputParameters.AddOutput("ulisq", ulisq);

            if (mode == MetaIncidenceRateMode.Difference)
                outputParameters.AddOutput("dsird", dsird);
            else
                outputParameters.AddOutput("dsirr", dsirr);
            outputParameters.AddOutput("dsll", dsll);
            outputParameters.AddOutput("dsul", dsul);
            outputParameters.AddOutput("dz", dz);
            outputParameters.AddOutput("dp", MathDbl.zvalp2(dz));


            IList<ParameterBag> eggerList = new List<ParameterBag>();
            outputParameters.AddOutput("*egger", eggerList);
            ParameterBag eggerParameters = new();
            eggerList.Add(eggerParameters);
            Transformation xform = Transformation.None;
            if (mode != MetaIncidenceRateMode.Difference)
                xform = Transformation.Log;
            // The bias indicators take the standard error of each study from limits that are the estimate plus and minus a multiple of
            // it: those of the rate difference are such limits; for the rate ratio, whose limits in the report are exact, they are
            // made here from the standard error of its logarithm, the root of 1 over the weight of the study
            double[] wll = rkrl;
            double[] wul = rkru;
            if (mode == MetaIncidenceRateMode.Ratio)
            {
                wll = new double[k + 1];
                wul = new double[k + 1];
                for (int i = 1; i <= k; i++)
                {
                    if (rkr[i] == Constant.MISSING || rkr[i] <= 0.0)
                    {
                        wll[i] = Constant.MISSING;
                        wul[i] = Constant.MISSING;
                    }
                    else
                    {
                        double se = Math.Sqrt(1.0 / rkw[i]);
                        wll[i] = Math.Exp(Math.Log(rkr[i]) - cit * se);
                        wul[i] = Math.Exp(Math.Log(rkr[i]) + cit * se);
                    }
                }
            }
            bool biasReported = Metabias(host, eggerParameters, rkr, wll, wul, k, ref cco, xform);
            FewStrata(outputParameters, eggerList, biasReported);

            double[] ptt = new double[k + 1];
            for (int i = 1; i <= k; i++)
            {
                o[i, 1] = pt1[i];
                o[i, 2] = pt2[i];
                o[i, 3] = pt1[i];
                o[i, 4] = pt2[i];
                included[i] = IncludeTable(o, i);
                if (pt2[i] == Constant.MISSING || pt1[i] == Constant.MISSING)
                    ptt[i] = Constant.MISSING;
                else
                    ptt[i] = pt1[i] + pt2[i];
            }

            IList<ParameterBag> chartList = new List<ParameterBag>();
            outputParameters.AddOutput("*chart", chartList);
            ParameterBag chartParameters;

            if (mode == MetaIncidenceRateMode.Difference)
            {
                if (k > 3)
                {
                    chartParameters = new ParameterBag();
                    chartList.Add(chartParameters);
                    chartParameters.AddOutput("chart", ChartRendererFactory.PrepForLater(ChartType.BiasMA, new BiasMAOptions(rkr, ptt, rkw, k, "Incidence rate difference", wll, wul, cco, cit, rmh, Transformation.None, false)));
                }

                chartParameters = new ParameterBag();
                chartList.Add(chartParameters);
                chartParameters.AddOutput("chart", ChartRendererFactory.PrepForLater(ChartType.MHRD, new MHOptions(1, k, rkw, title, rmh, ll, ul, cco, rkr, rkrl, rkru, lerr, uerr, null, "Incidence rate difference meta-analysis plot [fixed effects]", 1, "incidence rate difference")));

                chartParameters = new ParameterBag();
                chartList.Add(chartParameters);
                chartParameters.AddOutput("chart", ChartRendererFactory.PrepForLater(ChartType.MHRD, new MHOptions(1, k, dsw, title, dsird, dsll, dsul, cco, rkr, rkrl, rkru, lerr, uerr, null, "Incidence rate difference meta-analysis plot [random effects]", 1, "incidence rate difference")));
            }
            else
            {
                if (k > 3)
                {
                    chartParameters = new ParameterBag();
                    chartList.Add(chartParameters);
                    chartParameters.AddOutput("chart", ChartRendererFactory.PrepForLater(ChartType.BiasMA, new BiasMAOptions(rkr, ptt, rkw, k, "Incidence rate ratio", wll, wul, cco, cit, rmh, Transformation.Log, false)));
                }

                chartParameters = new ParameterBag();
                chartList.Add(chartParameters);
                chartParameters.AddOutput("chart", ChartRendererFactory.PrepForLater(ChartType.MH, new MHOptions(1, k, rkw, title, rmh, ll, ul, cco, rkr, rkrl, rkru, lerr, uerr, included, "Incidence rate ratio meta-analysis plot [fixed effects]", 1, "incidence rate ratio")));

                chartParameters = new ParameterBag();
                chartList.Add(chartParameters);
                chartParameters.AddOutput("chart", ChartRendererFactory.PrepForLater(ChartType.MH, new MHOptions(1, k, dsw, title, dsirr, dsll, dsul, cco, rkr, rkrl, rkru, lerr, uerr, included, "Incidence rate ratio meta-analysis plot [random effects]", 1, "incidence rate ratio")));
            }
            return new StepOutput(outputParameters);
        }
        /// <summary>
        /// Odds ratio meta-analysis: the report of what Mantel works out.  With the exact method there is also the pooled odds ratio by
        /// conditional maximum likelihood, with exact (Fisher) and mid-P limits and P values (ExactBB.Exact22K): given the totals of
        /// every table, the sum of the a cells has a distribution that depends on the common odds ratio alone.
        /// The bias indicators of Begg and Mazumdar and of Egger are from the logit limits of each study (GetLogitCi).
        /// </summary>
        /// <param name="host">The preferences, and where progress is shown.</param>
        /// <param name="parameters">As for RptPetoMeta.</param>
        public static StepOutput RptMantel(IPreferencesAndProgressBar host, ParameterBag parameters)
        {
            double p2M = 0; double p1M = 0;
            double p2F = 0; double p1F = 0; double llm = 0; double ulm = 0; double llf = 0; double ulf = 0; double eor = 0;

            double cco = parameters["gamma"].AsDouble;
            if (cco <= 0)
                cco = 0.95;
            double cit = PDF.gauinv(1.0 - (1.0 - cco) / 2.0);

            DataFrame snFrame = parameters["sn"].AsDataFrame;
            DoubleVariable snVariable = (DoubleVariable)snFrame.Variables[0];
            DataFrame srFrame = parameters["sr"].AsDataFrame;
            DoubleVariable srVariable = (DoubleVariable)srFrame.Variables[0];
            DataFrame xnFrame = parameters["xn"].AsDataFrame;
            DoubleVariable xnVariable = (DoubleVariable)xnFrame.Variables[0];
            DataFrame xrFrame = parameters["xr"].AsDataFrame;
            DoubleVariable xrVariable = (DoubleVariable)xrFrame.Variables[0];
            int rawRows = snVariable.Length;

            DoubleArraysAndBooleans copiesRemovingMissingRows = Numerics.Utilities.RemoveMissingRows(new[] { snVariable.Data, srVariable.Data, xnVariable.Data, xrVariable.Data }, 0, rawRows, 1);
            double[] sn = copiesRemovingMissingRows.ArraysWithMissingRowsRemoved[0];
            double[] sr = copiesRemovingMissingRows.ArraysWithMissingRowsRemoved[1];
            double[] xn = copiesRemovingMissingRows.ArraysWithMissingRowsRemoved[2];
            double[] xr = copiesRemovingMissingRows.ArraysWithMissingRowsRemoved[3];

            string[] title = MakeTitles(parameters, "strata", "stratum {0}", rawRows, out bool hasUserSuppliedLabels);

            int k = copiesRemovingMissingRows.ArraysWithMissingRowsRemoved[0].Length - 1; /* 1-based */
            title = Numerics.Utilities.CopyValidRows(title, copiesRemovingMissingRows.ValidRowsInOriginal, 0, rawRows, 1, k);

            double[,] o = new double[k + 1, 4 + 1];
            double[] axll = new double[k + 1];
            double[] axul = new double[k + 1];
            for (int i = 1; i <= k; i++)
            {
                o[i, 1] = Math.Abs(sr[i]);
                o[i, 3] = Math.Abs(sn[i] - sr[i]);
                if (sr[i] < 0 | sn[i] < 0 | sn[i] < sr[i])
                    throw new InvalidDataException();
                o[i, 2] = Math.Abs(xr[i]);
                o[i, 4] = Math.Abs(xn[i] - xr[i]);
                if (xr[i] < 0.0 | xn[i] < 0.0 | xn[i] < xr[i])
                    throw new InvalidDataException();
            }

            Mantel(host, 1, k, out int realk, o, out double rmh, out double ll, out double ul, out double x2, out double sk, cit, cco, out double[] odr, out double[] odw, out double[] dswt, out double[] odrl, out double[] odru, out double[] odx, out bool[] lerr, out bool[] uerr, out double qc, out double bd, out double dsor, out double dsx2, out double dsll, out double dsul, out bool[] cced, out double tausq, out bool[] included, out int ierr);
            if (ierr != 0)
            {
                if (ierr != 99)
                    throw new InvalidDataException();
                throw new TemplateOperationCancelledException();
            }
            if (realk == 0)
                throw new TemplateOperationCancelledException("None of the studies can be pooled: each of them has no events, or events in every subject, or a group with no subjects.", "Odds ratio meta-analysis");

            // Try exact Mantel
            if (host.Preferences.MetaExact)
            {
                Rec2X2[] tbl = new Rec2X2[k + 1];
                for (int i = 1; i <= k; i++)
                {
                    tbl[i].Freq = 1;
                    tbl[i].A = o[i, 1];
                    tbl[i].M1 = o[i, 1] + o[i, 2];
                    tbl[i].N1 = o[i, 1] + o[i, 3];
                    tbl[i].N0 = o[i, 2] + o[i, 4];
                    tbl[i].IsInformative = (o[i, 1] * o[i, 4] != 0.0) | (o[i, 2] * o[i, 3] != 0.0);
                }
                bool useLogScale = false;
                new ExactBB().Exact22K(host, 1, k, Exact22KDataType.Type1, tbl, cco, out eor, out ulf, out llf, out ulm, out llm, out p1F, out p2F, out p1M, out p2M, ref useLogScale, out ierr);
            }
            else
            {
                ierr = -9;
            }
            if (ierr != 0)
            {
                eor = Constant.MISSING;
                ulf = Constant.MISSING;
                llf = Constant.MISSING;
                ulm = Constant.MISSING;
                llm = Constant.MISSING;
                p1F = Constant.MISSING;
                p2F = Constant.MISSING;
                p1M = Constant.MISSING;
                p2M = Constant.MISSING;
            }

            ParameterBag outputParameters = new();
            outputParameters.AddOutput("pc", cco * 100);

            IList<ParameterBag> inputsList = new List<ParameterBag>();
            outputParameters.AddOutput("*inputs", inputsList);
            for (int i = 1; i <= k; i++)
            {
                ParameterBag inputsParameters = new();
                inputsList.Add(inputsParameters);
                inputsParameters.AddOutput("st", i);
                inputsParameters.AddOutput("a", o[i, 1]);
                inputsParameters.AddOutput("b", o[i, 2]);
                inputsParameters.AddOutput("c", o[i, 3]);
                inputsParameters.AddOutput("d", o[i, 4]);
                inputsParameters.AddOutput("lb", hasUserSuppliedLabels ? title[i] : string.Empty);
            }

            outputParameters.AddOutput("method", host.Preferences.MetaExact ? "CML" : "logit");

            IList<ParameterBag> orList = new List<ParameterBag>();
            outputParameters.AddOutput("*or", orList);
            for (int i = 1; i <= k; i++)
            {
                ParameterBag orParameters = new();
                orList.Add(orParameters);
                orParameters.AddOutput("st", i);
                orParameters.AddOutput("or", odr[i]);
                orParameters.AddOutput("yi", odr[i] > 0 ? Math.Log(odr[i]) : 0);
                orParameters.AddOutput("vi", VarianceOfLogOddsRatio(host, o, i));
                orParameters.AddOutput("lci", odrl[i]);
                orParameters.AddOutput("uci", odru[i]);
                orParameters.AddOutput("wt", 100 * odw[i] / Formatting.dsum(odw, 1));
                orParameters.AddOutput("dwt", 100 * dswt[i] / Formatting.dsum(dswt, 1));
                string tmp = GetMetaLabel(host, o, i, hasUserSuppliedLabels, cced, title);
                if (host.Preferences.DelayContinuityCorrection)
                    tmp = tmp.Replace("[CC", "[late CC");
                orParameters.AddOutput("lb", tmp);
                //if (host.Preferences.MetaExact & ((i) == Constant.MISSING || odru[i] == Constant.MISSING))
                //{
                //    OrciCorn(host, ref cco, ref o[i, 1], ref o[i, 2], ref o[i, 3], ref o[i, 4], out odr[i], out odrl[i], out odru[i]);
                //    orParameters = new ParameterBag();
                //    orList.Add(orParameters);
                //    orParameters.AddOutput("st", "* " + i.ToString());
                //    orParameters.AddOutput("or", string.Empty);
                //    orParameters.AddOutput("standardized_effect", string.Empty);
                //    orParameters.AddOutput("lci", host.RoundU(odrl[i]));
                //    orParameters.AddOutput("uci", host.RoundU(odru[i]));
                //    orParameters.AddOutput("wt", string.Empty);
                //    orParameters.AddOutput("dwt", string.Empty);
                //    orParameters.AddOutput("lb", " * [Cornfield limits]");
                //}
            }

            if (sk == 0)
            {
                outputParameters.AddOutput("meth", "Sato");
                outputParameters.AddOutput("odds", "undefined");
                outputParameters.AddOutput("from", ll);
                outputParameters.AddOutput("to", double.PositiveInfinity);
            }
            else
            {
                outputParameters.AddOutput("meth", "Robins-Breslow-Greenland");
                outputParameters.AddOutput("odds", rmh);
                outputParameters.AddOutput("from", ll);
                outputParameters.AddOutput("to", ul);
            }
            outputParameters.AddOutput("chi_mantel", x2);
            outputParameters.AddOutput("chi_p", PDF.chivalp(x2, 1.0));

            IList<ParameterBag> cmlList = new List<ParameterBag>();
            outputParameters.AddOutput("*cml", cmlList);
            if (ierr != -9)
            {
                ParameterBag cmlParameters = new();
                cmlList.Add(cmlParameters);
                cmlParameters.AddOutput("eor", eor);
                cmlParameters.AddOutput("llf", llf);
                cmlParameters.AddOutput("ulf", ulf);
                cmlParameters.AddOutput("p1f", p1F);
                cmlParameters.AddOutput("p2f", p2F);
                cmlParameters.AddOutput("llm", llm);
                cmlParameters.AddOutput("ulm", ulm);
                cmlParameters.AddOutput("p1m", p1M);
                cmlParameters.AddOutput("p2m", p2M);
            }

            outputParameters.AddOutput("bd", realk > 1 ? bd : 0.0);   // 0 by definition with one stratum, not the rounding residue of one squared deviation
            outputParameters.AddOutput("df", realk - 1);
            outputParameters.AddOutput("xp", PDF.chivalp(bd, realk - 1));

            outputParameters.AddOutput("qc", realk > 1 ? qc : 0.0);   // 0 by definition with one stratum, not the rounding residue of one squared deviation
            outputParameters.AddOutput("df_cochran", realk - 1);
            outputParameters.AddOutput("xp_cochran", PDF.chivalp(qc, realk - 1));
            outputParameters.AddOutput("tausq", tausq);

            IsquareNcc(host, qc, realk, cco, cit, out double isq, out double llisq, out double ulisq);
            outputParameters.AddOutput("isq", isq);
            outputParameters.AddOutput("pc1", cco * 100);
            outputParameters.AddOutput("llisq", llisq);
            outputParameters.AddOutput("ulisq", ulisq);

            outputParameters.AddOutput("dsor", dsor);
            outputParameters.AddOutput("dsll", dsll);
            outputParameters.AddOutput("dsul", dsul);
            outputParameters.AddOutput("dsx2", dsx2);
            outputParameters.AddOutput("df_ds", 1);
            outputParameters.AddOutput("xp_ds", PDF.chivalp(dsx2, 1.0));

            GetLogitCi(host, o, k, cit, axll, axul);

            IList<ParameterBag> eggerList = new List<ParameterBag>();
            outputParameters.AddOutput("*egger", eggerList);
            ParameterBag eggerParameters = new();
            eggerList.Add(eggerParameters);
            bool biasReported = Metabias(host, eggerParameters, odr, axll, axul, k, ref cco, Transformation.Log);
            FewStrata(outputParameters, eggerList, biasReported);

            IList<ParameterBag> harbordList = new List<ParameterBag>();
            outputParameters.AddOutput("*harbord", harbordList);
            ParameterBag harbordParameters = new();
            harbordList.Add(harbordParameters);
            ModMetabias(host, harbordParameters, o, k, cco, 1);
            if (!biasReported)
                harbordList.Clear();

            IList<ParameterBag> chartList = new List<ParameterBag>();
            outputParameters.AddOutput("*chart", chartList);
            ParameterBag chartParameters;

            if (k > 3)
            {
                chartParameters = new ParameterBag();
                chartList.Add(chartParameters);
                chartParameters.AddOutput("chart", ChartRendererFactory.PrepForLater(ChartType.BiasMA, new BiasMAOptions(odr, odx, odw, k, "Odds ratio", axll, axul, cco, cit, rmh, Transformation.Log, false)));
            }

            chartParameters = new ParameterBag();
            chartList.Add(chartParameters);
            chartParameters.AddOutput("chart", ChartRendererFactory.PrepForLater(ChartType.LAbbe, new LAbbeOptions(k, o, rmh, true)));

            if (sk != 0)
            {
                chartParameters = new ParameterBag();
                chartList.Add(chartParameters);
                chartParameters.AddOutput("chart", ChartRendererFactory.PrepForLater(ChartType.MH, new MHOptions(1, k, odw, title, rmh, ll, ul, cco, odr, odrl, odru, lerr, uerr, included, "Odds ratio meta-analysis plot [fixed effects]", 1, "odds ratio")));

                chartParameters = new ParameterBag();
                chartList.Add(chartParameters);
                chartParameters.AddOutput("chart", ChartRendererFactory.PrepForLater(ChartType.MH, new MHOptions(1, k, dswt, title, dsor, dsll, dsul, cco, odr, odrl, odru, lerr, uerr, included, "Odds ratio meta-analysis plot [random effects]", 1, "odds ratio")));
            }
            return new StepOutput(outputParameters);
        }

        /// <summary>
        /// The odds ratio of each table and the pooled odds ratio of Mantel and Haenszel, for the meta-analysis, the Mantel-Haenszel
        /// test and the crosstabs with strata.  The odds ratio of a table is a d / (b c); a table with a cell of nothing has the
        /// continuity correction in every cell first (ContinuityCorrect).  The pooled odds ratio is R / S, R being the sum of a d / n
        /// and S the sum of b c / n, whose terms are the weights; the variance of its logarithm is
        /// sum(P R) / (2 R^2) + sum(P S + Q R) / (2 R S) + sum(Q S) / (2 S^2), with P = (a + d) / n and Q = (b + c) / n for each
        /// table.  With the continuity correction delayed the pooling is from the counts as they are, if a d is above nothing in
        /// some table.  If S is nothing the pooled odds ratio is infinite, and the lower limit is Sato's.
        /// The chi-square of Mantel and Haenszel is from the counts as they are: the square of (the sum of a less its expectation,
        /// less a half for continuity if it is a half or more) over the sum of the variances of a with the totals given.
        /// Cochran's Q and the random effects figures are from the logarithm of the odds ratio of each table with its variance
        /// 1 / a + 1 / b + 1 / c + 1 / d, and Q is about the pooled odds ratio of Mantel and Haenszel.  The statistic of Breslow and
        /// Day is from the counts as they are: for each table the a that the pooled odds ratio would give with the totals of the
        /// table, and its variance.
        /// </summary>
        /// <param name="x2">On return, the chi-square of Mantel and Haenszel.</param>
        /// <param name="sk">On return, S, the sum of b c / n.</param>
        /// <param name="odr">On return, the odds ratio of each table.</param>
        /// <param name="odw">On return, the fixed effects weight of each table.</param>
        /// <param name="dswt">On return, the random effects weight of each table.</param>
        /// <param name="bd">On return, the statistic of Breslow and Day.</param>
        /// <param name="included">On return, whether each table is pooled (see IncludeTable).</param>
        /// <param name="ierr">On return, 0.</param>
        /// <remarks>The other parameters are as those of RelativeRiskMA.  With the exact method the limits of each table are the
        /// conditional exact limits (ExactBB.OddsRatioCI).</remarks>
        public static void Mantel(IPreferencesAndProgressBar host, int lowerBound, int k, out int realk, double[,] o, out double rmh, out double ll, out double ul, out double x2, out double sk, double cit, double cco, out double[] odr, out double[] odw, out double[] dswt, out double[] odrl, out double[] odru, out double[] odx, out bool[] lerr, out bool[] uerr, out double qc, out double bd, out double dsor, out double dsx2, out double dsll, out double dsul, out bool[] cced, out double tausq, out bool[] included, out int ierr)
        {
            odr = new double[k + lowerBound];
            odrl = new double[k + lowerBound];
            odru = new double[k + lowerBound];
            odw = new double[k + lowerBound];
            dswt = new double[k + lowerBound];
            odx = new double[k + lowerBound];
            lerr = new bool[k + lowerBound];
            uerr = new bool[k + lowerBound];
            cced = new bool[k + lowerBound];
            included = new bool[k + lowerBound];

            ierr = -1;
            double eai = 0.0;
            double vari = 0.0;
            double rk = 0.0;
            sk = 0.0;
            double w = 0.0;
            double svd1 = 0.0;
            double svd2 = 0.0;
            double svd3 = 0.0;
            double sumwt = 0.0;
            realk = 0;

            bool rkok = false;
            for (int i = lowerBound; i < k + lowerBound; i++)
                if (o[i, 1] * o[i, 4] != 0.0)
                    rkok = true;

            for (int i = lowerBound; i < k + lowerBound; i++)
            {
                double a = o[i, 1];
                double b = o[i, 2];
                double c = o[i, 3];
                double d = o[i, 4];
                double n = a + b + c + d;
                odx[i] = n;
                if (n <= 0)
                    throw new InvalidDataException();

                if (!host.Preferences.DelayContinuityCorrection)
                    rkok = false;

                // MH across strata
                included[i] = IncludeTable(o, i);
                if (included[i])
                {
                    // only do cc at this stage if absolutely necessary (all a or all d cells zero)
                    if (!rkok)
                    {
                        if (a <= 0 || b <= 0 || c <= 0 || d <= 0)
                        {
                            ContinuityCorrect(host, a, b, c, d, out a, out b, out c, out d);
                            n = a + b + c + d;
                            cced[i] = true;
                        }
                        else
                        {
                            cced[i] = false;
                        }
                    }
                    realk += 1;
                    // the chi-square of Mantel and Haenszel is from the counts as they are: it needs no correction of the cells
                    {
                        double ar = o[i, 1];
                        double br = o[i, 2];
                        double cr = o[i, 3];
                        double dr = o[i, 4];
                        double nr = ar + br + cr + dr;
                        eai = eai + ar - (ar + cr) * (ar + br) / nr;
                        if (nr > 1.0)
                            vari += (ar + cr) * (br + dr) * (ar + br) * (cr + dr) / (Math.Pow(nr, 2.0) * (nr - 1.0));
                    }
                    double rr = a * d / n;
                    double ss = b * c / n;
                    double pp = (a + d) / n;
                    double qq = (b + c) / n;
                    w = w + (qq + 1.0 / n) * rr + (pp + 1.0 / n) * ss;
                    rk += rr;
                    sk += ss;
                    svd1 += pp * rr;
                    svd2 += (qq * rr + pp * ss);
                    svd3 += qq * ss;
                    double weight = b * c / n;
                    odw[i] = weight;
                    sumwt += weight;
                }

                if (!included[i])
                {
                    odr[i] = 0;
                    odrl[i] = 0;
                    odru[i] = double.PositiveInfinity;
                    lerr[i] = false;
                    uerr[i] = false;
                }
                else
                {
                    if (rkok)
                    {
                        // do cc if not done earlier
                        if (a <= 0 || b <= 0 || c <= 0 || d <= 0)
                        {
                            ContinuityCorrect(host, a, b, c, d, out a, out b, out c, out d);
                            // N = a + b + C + D; 
                            cced[i] = true;
                        }
                        else
                        {
                            cced[i] = false;
                        }
                    }
                    odr[i] = a * d / (b * c);
                    if (host.Preferences.MetaExact)
                    {
                        // the conditional exact limits are those of the observed table: a zero cell gives a limit of 0 or infinity
                        OddsRatioCI(host, cco, o[i, 1], o[i, 2], o[i, 3], o[i, 4], out double _, out odrl[i], out odru[i], out lerr[i], out uerr[i]);
                    }
                    else
                    {
                        // go for horrid logit se if you must
                        double se = Math.Sqrt(1.0 / a + 1.0 / b + 1.0 / c + 1.0 / d);
                        odrl[i] = Math.Exp(Math.Log(odr[i]) - cit * se);
                        odru[i] = Math.Exp(Math.Log(odr[i]) + cit * se);
                        lerr[i] = false;
                        uerr[i] = false;
                    }
                }
            }

            if (sk == 0)
            {
                // SATO T. BIOMETRICS 46 71-80
                rmh = Constant.MISSING;
                double sq = Math.Sqrt((4.0 * rk * sk + cit * cit * w) * cit * cit * w);
                // ll = ( 2.0 * rk * sk + cit * cit * w - sq ) / 2.0 / rk / rk; 
                // the limits are the odds ratios psi for which (R - psi S)^2 is no more than z^2 psi W; what is worked out here is the
                // upper limit of 1 / psi, and with S nothing there is no upper limit of psi
                ul = (2.0 * rk * sk + cit * cit * w + sq) / 2.0 / rk / rk;
                ll = 1.0 / ul;
                ul = double.PositiveInfinity;
            }
            else
            {
                rmh = rk / sk;
                //  RBG
                double vrbg = svd1 / 2.0 / rk / rk + svd2 / 2.0 / rk / sk + svd3 / 2.0 / sk / sk;
                ll = Math.Exp(Math.Log(rk / sk) - Math.Sqrt(cit * cit * vrbg));
                ul = Math.Exp(Math.Log(rk / sk) + Math.Sqrt(cit * cit * vrbg));
            }
            if (ll > ul)
                Utilities.Utilities.Swap(ref ll, ref ul);
            // the correction of a half for continuity is made only if the difference is a half or more
            x2 = Math.Pow(Math.Abs(eai) - (Math.Abs(eai) >= 0.5 ? 0.5 : 0.0), 2.0) / vari;

            // Q (combinability)
            qc = 0.0;
            bd = 0.0;
            double wlor = 0.0;
            sumwt = 0.0;
            double sumsqwt = 0.0;
            for (int i = lowerBound; i < k + lowerBound; i++)
            {
                double a = o[i, 1];
                double b = o[i, 2];
                double c = o[i, 3];
                double d = o[i, 4];
                // Breslow-Day - must do it before continuity correction -->
                double n1 = a + b;
                double n0 = c + d;
                double m1 = a + c;
                double m2 = b + d;
                if (n1 != 0 && n0 != 0 && m1 != 0 && m2 != 0)
                {
                    // quadratic coefficients ax² + bx + c = 0
                    double qda = 1.0 - rmh;
                    double qdb = m2 - n1 + (m1 + n1) * rmh;
                    double qdc = -m1 * n1 * rmh;
                    double ea = qda == 0.0
                        ? -qdc / qdb // a pooled odds ratio of exactly 1 leaves the linear term only: the fitted a is (a + b)(a + c) / n
                        : (-qdb + Math.Sqrt(Math.Pow(qdb, 2.0) - 4.0 * qda * qdc)) / (2.0 * qda);
                    // give the rest of the expected table e.g. Breslow & Day P 144
                    double varea = 1.0 / (1.0 / ea + 1.0 / (n1 - ea) + 1.0 / (m1 - ea) + 1.0 / (n0 - m1 + ea));
                    bd += (a - ea) * (a - ea) / varea;
                }
                // <--
                if (included[i])
                {
                    if (a <= 0 || b <= 0 || c <= 0 || d <= 0)
                        ContinuityCorrect(host, a, b, c, d, out a, out b, out c, out d);
                    double n = a + b + c + d;
                    double rr = a * d / n;
                    double ss = b * c / n;
                    double pp = (a + d) / n;
                    double qq = (b + c) / n;
                    svd1 = pp * rr;
                    svd2 = qq * rr + pp * ss;
                    svd3 = qq * ss;
                    double vrbgi = svd1 / 2.0 / rr / rr + svd2 / 2.0 / rr / ss + svd3 / 2.0 / ss / ss;
                    double weight = 1.0 / vrbgi;
                    double lori = Math.Log(a * d / (b * c));
                    qc += weight * Math.Pow(lori - Math.Log(rmh), 2.0);
                    wlor += lori * weight;
                    sumwt += weight;
                    sumsqwt += weight * weight;
                }
            }
            // DerSimonian-Laird
            if (sumwt * sumwt - sumsqwt == 0.0)
                tausq = 0.0;
            else
                tausq = (qc - (realk - 1)) * sumwt / (sumwt * sumwt - sumsqwt);
            if (tausq < 0.0)
                tausq = 0.0;
            wlor = 0.0;
            sumwt = 0.0;
            for (int i = lowerBound; i < k + lowerBound; i++)
            {
                double a = o[i, 1];
                double b = o[i, 2];
                double c = o[i, 3];
                double d = o[i, 4];
                if (included[i])
                {
                    if (a <= 0.0 || b <= 0.0 || c <= 0.0 || d <= 0.0)
                        ContinuityCorrect(host, a, b, c, d, out a, out b, out c, out d);
                    double n = a + b + c + d;
                    double rr = a * d / n;
                    double ss = b * c / n;
                    double pp = (a + d) / n;
                    double qq = (b + c) / n;
                    svd1 = pp * rr;
                    svd2 = qq * rr + pp * ss;
                    svd3 = qq * ss;
                    double vrbgi = svd1 / 2.0 / rr / rr + svd2 / 2.0 / rr / ss + svd3 / 2.0 / ss / ss;
                    double weight = 1.0 / (tausq + vrbgi);
                    dswt[i] = weight;
                    double lori = Math.Log(a * d / (b * c));
                    wlor += lori * weight;
                    sumwt += weight;
                }
            }
            dsor = Math.Exp(wlor / sumwt);
            dsx2 = Math.Pow(wlor, 2.0) / sumwt;
            dsll = Math.Exp(wlor / sumwt - cit / Math.Sqrt(sumwt));
            dsul = Math.Exp(wlor / sumwt + cit / Math.Sqrt(sumwt));
            if (dsll > dsul)
                Utilities.Utilities.Swap(ref dsll, ref dsul);
            ierr = 0;
        }

        /// <summary>
        /// The variance of the logarithm of the odds ratio of table i, 1 / a + 1 / b + 1 / c + 1 / d, with the continuity correction
        /// of a table with a cell of nothing: the variance that Cochran's Q and the random effects weights are made from, which the
        /// reports show.  A table that is not pooled has none.
        /// </summary>
        public static double VarianceOfLogOddsRatio(IPreferences host, double[,] o, int i)
        {
            if (!IncludeTable(o, i))
                return Constant.MISSING;
            double a = o[i, 1];
            double b = o[i, 2];
            double c = o[i, 3];
            double d = o[i, 4];
            if (a <= 0.0 || b <= 0.0 || c <= 0.0 || d <= 0.0)
                ContinuityCorrect(host, a, b, c, d, out a, out b, out c, out d);
            return 1.0 / a + 1.0 / b + 1.0 / c + 1.0 / d;
        }

        /// <summary>
        /// The variance of the logarithm of the relative risk of table i, 1 / a + 1 / b - 1 / (a + c) - 1 / (b + d), with the
        /// continuity correction of a table with a cell of nothing.  A table that is not pooled has none.
        /// </summary>
        public static double VarianceOfLogRelativeRisk(IPreferences host, double[,] o, int i)
        {
            if (!IncludeRelativeRisk(o, i))
                return Constant.MISSING;
            double a = o[i, 1];
            double b = o[i, 2];
            double c = o[i, 3];
            double d = o[i, 4];
            if (a <= 0.0 || b <= 0.0 || c <= 0.0 || d <= 0.0)
                ContinuityCorrect(host, a, b, c, d, out a, out b, out c, out d);
            return 1.0 / a + 1.0 / b - 1.0 / (a + c) - 1.0 / (b + d);
        }

        /// <summary>
        /// The variance of the risk difference of table i, a c / (a + c)^3 + b d / (b + d)^3, with the continuity correction of a
        /// table with a cell of nothing.  A table with a group of nobody has none.
        /// </summary>
        public static double VarianceOfRiskDifference(IPreferences host, double[,] o, int i)
        {
            double a = o[i, 1];
            double b = o[i, 2];
            double c = o[i, 3];
            double d = o[i, 4];
            if (a + c <= 0.0 || b + d <= 0.0)
                return Constant.MISSING;
            if (a <= 0.0 || b <= 0.0 || c <= 0.0 || d <= 0.0)
                ContinuityCorrect(host, a, b, c, d, out a, out b, out c, out d);
            return a * c / Math.Pow(a + c, 3.0) + b * d / Math.Pow(b + d, 3.0);
        }

        /// <summary>
        /// The logit limits of the odds ratio of each table, the odds ratio times and over the exponential of z times the root of
        /// 1 / a + 1 / b + 1 / c + 1 / d, with the continuity correction of a table with a cell of nothing: the limits from which the
        /// bias indicators take the standard errors, whatever the method of the limits of the report.
        /// </summary>
        public static void GetLogitCi(IPreferences host, double[,] o, int k, double cit, double[] axll, double[] axul)
        {
            for (int i = 1; i <= k; i++)
            {
                double a = o[i, 1];
                double b = o[i, 2];
                double c = o[i, 3];
                double d = o[i, 4];
                // double N = a + b + C + D; 
                if (IncludeTable(o, i) == false)
                {
                    axll[i] = 0;
                    axul[i] = double.PositiveInfinity;
                }
                else
                {
                    if (a <= 0.0 || b <= 0.0 || c <= 0.0 || d <= 0.0)
                    {
                        ContinuityCorrect(host, a, b, c, d, out a, out b, out c, out d);
                        // N = a + b + C + D; 
                    }
                    double odr = a * d / (b * c);
                    double se = Math.Sqrt(1.0 / a + 1.0 / b + 1.0 / c + 1.0 / d);
                    axll[i] = Math.Exp(Math.Log(odr) - cit * se);
                    axul[i] = Math.Exp(Math.Log(odr) + cit * se);
                }
            }
        }

        /// <summary>
        /// The approximate limits of the relative risk of each table, the relative risk times and over the exponential of z times the
        /// root of 1 / a + 1 / b - 1 / (a + c) - 1 / (b + d), with the continuity correction of a table with a cell of nothing: the
        /// limits from which the bias indicators take the standard errors, whatever the method of the limits of the report.
        /// </summary>
        public static void GetAproxrrCI(IPreferences host, double[,] o, int k, double cit, double[] axll, double[] axul)
        {
            for (int i = 1; i <= k; i++)
            {
                double a = o[i, 1];
                double b = o[i, 2];
                double c = o[i, 3];
                double d = o[i, 4];
                if (IncludeRelativeRisk(o, i))
                {
                    if (a <= 0.0 || b <= 0.0 || c <= 0.0 || d <= 0.0)
                        ContinuityCorrect(host, a, b, c, d, out a, out b, out c, out d);
                    double rkr = a / (a + c) / (b / (b + d));
                    // approximate se of log rr
                    double se = Math.Sqrt(1.0 / a + 1.0 / b - 1.0 / (a + c) - 1.0 / (b + d));
                    axll[i] = Math.Exp(Math.Log(rkr) - se * cit);
                    axul[i] = Math.Exp(Math.Log(rkr) + se * cit);
                }
            }
        }

        /// <summary>
        /// The incidence rate difference of each study, a / t1 - b / t2, with the variance a / t1^2 + b / t2^2, pooled as the summary of
        /// the class describes.  A study without an event in one of its groups has the continuity correction cc added to a and to b
        /// in its variance; its difference is from the events as they are.  A study without an event in either group, or without
        /// person-time in a group, is left out.
        /// </summary>
        /// <param name="cc">The continuity correction.</param>
        /// <param name="cced">On return, whether each study had the continuity correction.</param>
        private static void IrdMeta(int k, double[] a, double[] b, double[] pt1, double[] pt2, double cc, bool[] cced, out double rmh, out double ll, out double ul, out double zrmh, ref double cit, ref double cco, double[] rkr, double[] rkw, double[] dsw, double[] rkrl, double[] rkru, bool[] lerr, bool[] uerr, out double qc, out double dsrd, out double dz, out double dsll, out double dsul, out double realk, out double tausq, out int ierr)
        {
            double sumwt = 0.0;
            double sumwi = 0.0;
            realk = 0.0;
            for (int i = 1; i <= k; i++)
            {
                // ird and ci for stratum
                if (a[i] + b[i] <= 0.0 || pt1[i] <= 0.0 || pt2[i] <= 0.0)
                {
                    rkr[i] = Constant.MISSING;
                    rkw[i] = Constant.MISSING;
                    rkrl[i] = Constant.MISSING;
                    rkru[i] = Constant.MISSING;
                    lerr[i] = true;
                    uerr[i] = true;
                }
                else
                {
                    realk += 1.0;
                    double ir1 = a[i] / pt1[i];
                    double ir2 = b[i] / pt2[i];
                    double ird = ir1 - ir2;
                    // without an event in one of the groups the events of both have the correction, for the variance alone
                    cced[i] = a[i] <= 0.0 || b[i] <= 0.0;
                    double ac = cced[i] ? a[i] + cc : a[i];
                    double bc = cced[i] ? b[i] + cc : b[i];
                    // the limits of the study are from the variance that its weight is made from
                    double vark = ac / (pt1[i] * pt1[i]) + bc / (pt2[i] * pt2[i]);
                    rkrl[i] = ird - cit * Math.Sqrt(vark);
                    rkru[i] = ird + cit * Math.Sqrt(vark);
                    rkr[i] = ird;
                    //  pooled incidence risk difference
                    rkw[i] = 1.0 / vark;
                    sumwt += rkw[i];
                    sumwi += rkw[i] * ird;
                }
            }
            rmh = sumwi / sumwt;
            double se = Math.Sqrt(1.0 / sumwt);
            ll = rmh - se * cit;
            ul = rmh + se * cit;
            if (ll > ul)
            {
                double t = ll;
                ll = ul;
                ul = t;
            }
            zrmh = rmh / se;

            // Q (combinability)
            qc = 0.0;
            sumwt = 0.0;
            double sumsqwt = 0.0;
            for (int i = 1; i <= k; i++)
            {
                if (rkw[i] != Constant.MISSING)
                {
                    qc += rkw[i] * Math.Pow(rkr[i] - rmh, 2.0);
                    sumwt += rkw[i];
                    sumsqwt += rkw[i] * rkw[i];
                }
            }

            // DerSimonian-Laird random effects
            if (sumwt - sumsqwt / sumwt == 0.0)
                tausq = 0.0;
            else
                tausq = (qc - (realk - 1.0)) / (sumwt - sumsqwt / sumwt);
            if (tausq < 0.0)
                tausq = 0.0;
            double wrd = 0.0;
            sumwt = 0.0;
            // sumsqwt = 0.0; - unused
            for (int i = 1; i <= k; i++)
            {
                if (rkw[i] != Constant.MISSING)
                {
                    double weight = 1.0 / (tausq + 1.0 / rkw[i]);
                    dsw[i] = weight;
                    wrd += rkr[i] * weight;
                    sumwt += weight;
                }
                else
                {
                    dsw[i] = Constant.MISSING;
                }
            }
            dsrd = wrd / sumwt;
            dz = wrd / Math.Sqrt(sumwt);
            dsll = wrd / sumwt - cit / Math.Sqrt(sumwt);
            dsul = wrd / sumwt + cit / Math.Sqrt(sumwt);
            if (dsll > dsul)
            {
                double t = dsll;
                dsll = dsul;
                dsul = t;
            }
            ierr = 0;
        }

        /// <summary>
        /// The incidence rate ratio of each study, (a / t1) / (b / t2), pooled on the scale of its logarithm, whose variance is
        /// 1 / a + 1 / b, as the summary of the class describes.  The limits of each study are exact: those of the binomial proportion
        /// a of a + b, by the F distribution, put on the scale of the rate ratio.  A study without an event in one of its groups has
        /// the continuity correction cc added to a and to b, in its rate ratio and in its variance; its exact limits are from the
        /// events as they are, and one of them is 0 or infinity.  A study without an event in either group, or without person-time
        /// in a group, is left out.
        /// </summary>
        /// <param name="cc">The continuity correction.</param>
        /// <param name="cced">On return, whether each study had the continuity correction.</param>
        private static void IrrMeta(int k, double[] a, double[] b, double[] pt1, double[] pt2, double cc, bool[] cced, out double rmh, out double ll, out double ul, out double zrmh, ref double cit, ref double cco, double[] rkr, double[] rkw, double[] dsw, double[] rkrl, double[] rkru, bool[] lerr, bool[] uerr, out double qc, out double dsirr, out double dz, out double dsll, out double dsul, out double realk, out double tausq, out int ierr)
        {
            double sumwt = 0.0;
            double sumwi = 0.0;
            realk = 0.0;
            for (int i = 1; i <= k; i++)
            {
                // irr and ci for stratum: a stratum with no events in either group tells nothing of the rate ratio and is not pooled
                if (a[i] + b[i] <= 0.0 || pt1[i] <= 0.0 || pt2[i] <= 0.0)
                {
                    rkr[i] = Constant.MISSING;
                    rkw[i] = Constant.MISSING;
                    rkrl[i] = Constant.MISSING;
                    rkru[i] = Constant.MISSING;
                    lerr[i] = true;
                    uerr[i] = true;
                }
                else
                {
                    realk += 1.0;
                    double p = cco + (1.0 - cco) / 2.0;
                    // the exact limits are from the events as they are: without an event in the first group the lower limit is 0,
                    // and without one in the second there is no upper limit
                    if (a[i] <= 0.0)
                    {
                        rkrl[i] = 0.0;
                    }
                    else
                    {
                        double f = PDF.ffromp(2.0 * a[i], 2.0 * (b[i] + 1.0), 1.0 - p);
                        rkrl[i] = pt2[i] / pt1[i] * (a[i] / (b[i] + 1.0)) * (1.0 / f);
                    }
                    if (b[i] <= 0.0)
                    {
                        rkru[i] = double.PositiveInfinity;
                    }
                    else
                    {
                        double f = PDF.ffromp(2.0 * b[i], 2.0 * (a[i] + 1.0), 1.0 - p);
                        rkru[i] = pt2[i] / pt1[i] * ((a[i] + 1.0) / b[i]) * f;
                    }
                    lerr[i] = false;
                    uerr[i] = false;
                    // without an event in one of the groups the events of both have the correction
                    cced[i] = a[i] <= 0.0 || b[i] <= 0.0;
                    double ac = cced[i] ? a[i] + cc : a[i];
                    double bc = cced[i] ? b[i] + cc : b[i];
                    rkr[i] = ac / pt1[i] / (bc / pt2[i]);
                    // pooled incidence rate ratio
                    // vark = 1# / a(i) + 1# / b(i) - as expressed in Lau paper on AZT
                    rkw[i] = ac * bc / (ac + bc);
                    sumwt += rkw[i];
                    if (rkr[i] > 0)
                        sumwi += rkw[i] * Math.Log(rkr[i]);
                }
            }
            rmh = Math.Exp(sumwi / sumwt);
            double se = Math.Sqrt(1.0 / sumwt);
            ll = Math.Exp(Math.Log(rmh) - se * cit);
            ul = Math.Exp(Math.Log(rmh) + se * cit);
            if (ll > ul)
            {
                double t = ll;
                ll = ul;
                ul = t;
            }
            zrmh = Math.Log(rmh) / se;
            // Q (combinability)
            qc = 0.0;
            sumwt = 0.0;
            double sumsqwt = 0.0;
            for (int i = 1; i <= k; i++)
            {
                if (rkw[i] != Constant.MISSING)
                {
                    if (rkr[i] > 0)
                        qc += rkw[i] * Math.Pow(Math.Log(rkr[i]) - Math.Log(rmh), 2.0);
                    sumwt += rkw[i];
                    sumsqwt += rkw[i] * rkw[i];
                }
            }
            // DerSimonian-Laird random effects
            if (sumwt - sumsqwt / sumwt == 0.0)
                tausq = 0.0;
            else
                tausq = (qc - (realk - 1.0)) / (sumwt - sumsqwt / sumwt);
            if (tausq < 0.0)
                tausq = 0.0;
            double wrd = 0.0;
            sumwt = 0.0;
            // sumsqwt = 0.0; - unused
            for (int i = 1; i <= k; i++)
            {
                if (rkw[i] != Constant.MISSING)
                {
                    double weight = 1.0 / (tausq + 1.0 / rkw[i]);
                    dsw[i] = weight;
                    if (rkr[i] > 0)
                        wrd += Math.Log(rkr[i]) * weight;
                    sumwt += weight;
                }
                else
                {
                    dsw[i] = Constant.MISSING;
                }
            }
            dsirr = Math.Exp(wrd / sumwt);
            dz = wrd / Math.Sqrt(sumwt);
            dsll = Math.Exp(wrd / sumwt - cit / Math.Sqrt(sumwt));
            dsul = Math.Exp(wrd / sumwt + cit / Math.Sqrt(sumwt));
            if (dsll > dsul)
            {
                double t = dsll;
                dsll = dsul;
                dsul = t;
            }
            ierr = 0;
        }

        /// <summary>
        /// Summary meta-analysis, of a statistic that each study gives with its standard error or with its confidence limits, pooled
        /// as the summary of the class describes.  For a ratio the pooling is of the logarithm of the statistic, and a standard error
        /// that is given is that of the logarithm.  From limits the standard error is (upper - lower) / (2 z), the limits being
        /// taken to be at the confidence level of the analysis.  A study with a blank cell is left out.
        /// </summary>
        /// <param name="host">The preferences, and where progress is shown.</param>
        /// <param name="parameters">"y": the statistic of each study; "use_ci": "true" if the limits "ll_y" and "ul_y" are given,
        /// otherwise the standard error "se_y"; "use_ratio": whether the statistic is a ratio; "stat_in" and "statx": the name of the
        /// statistic and the words of the test, for the report; "studies" (may be left out): the labels; "gamma".</param>
        public static StepOutput RptMetaSummary(IPreferencesAndProgressBar host, ParameterBag parameters)
        {
            double dsul; double dsll; double dsrr;
            double tausq;
            double zrmh; double ulrmh; double llrmh;
            double[] odx = null;

            double cco = parameters["gamma"].AsDouble;
            if (cco <= 0)
                cco = 0.95;
            double cit = PDF.gauinv(1.0 - (1.0 - cco) / 2.0);

            string statx = parameters["statx"].AsString;
            bool useRatio = parameters["use_ratio"].AsBoolean;
            string stat = parameters["stat_in"].AsString;
            bool useCI = "true".Equals(parameters["use_ci"].AsString.ToLower(CultureInfo.InvariantCulture));

            DataFrame yFrame = parameters["y"].AsDataFrame;
            DoubleVariable yVariable = (DoubleVariable)yFrame.Variables[0];
            double[] y = yVariable.Data;

            int rawRows = y.Length;

            double[] llY;
            double[] ulY;
            double[] seY;
            if (useCI)
            {
                DataFrame llYFrame = parameters["ll_y"].AsDataFrame;
                DoubleVariable llYVariable = (DoubleVariable)llYFrame.Variables[0];
                llY = llYVariable.Data;

                DataFrame ulYFrame = parameters["ul_y"].AsDataFrame;
                DoubleVariable ulYVariable = (DoubleVariable)ulYFrame.Variables[0];
                ulY = ulYVariable.Data;

                seY = new double[rawRows];

                for (int i = 0; i < rawRows; i++)
                {
                    if (llY[i] == Constant.MISSING || ulY[i] == Constant.MISSING)
                    {
                        // a study with a blank confidence limit has no standard error: it is left out with the other incomplete rows
                        seY[i] = Constant.MISSING;
                        continue;
                    }
                    if (llY[i] > ulY[i])
                        Utilities.Utilities.Swap(ref llY[i], ref ulY[i]);
                    if (useRatio)
                        seY[i] = (Math.Log(ulY[i]) - Math.Log(llY[i])) / 2.0 / cit;
                    else
                        seY[i] = (ulY[i] - llY[i]) / 2.0 / cit;
                }
            }
            else
            {
                DataFrame seYFrame = parameters["se_y"].AsDataFrame;
                DoubleVariable seYVariable = (DoubleVariable)seYFrame.Variables[0];
                seY = seYVariable.Data;

                llY = new double[rawRows];
                ulY = new double[rawRows];

                for (int i = 0; i < rawRows; i++)
                {
                    if (useRatio)
                    {
                        llY[i] = Math.Exp(Math.Log(y[i]) - cit * seY[i]);
                        ulY[i] = Math.Exp(Math.Log(y[i]) + cit * seY[i]);
                    }
                    else
                    {
                        llY[i] = y[i] - cit * seY[i];
                        ulY[i] = y[i] + cit * seY[i];
                    }
                }
            }

            DoubleArraysAndBooleans copiesRemovingMissingRows = Numerics.Utilities.RemoveMissingRows(new[] { y, llY, ulY, seY }, 0, rawRows, 1, 1);
            y = copiesRemovingMissingRows.ArraysWithMissingRowsRemoved[0];
            llY = copiesRemovingMissingRows.ArraysWithMissingRowsRemoved[1];
            ulY = copiesRemovingMissingRows.ArraysWithMissingRowsRemoved[2];
            seY = copiesRemovingMissingRows.ArraysWithMissingRowsRemoved[3];
            int k = copiesRemovingMissingRows.ArraysWithMissingRowsRemoved[0].Length - 2; /* 1-based, spare at end */

            // Pooling
            CorrelationRowType[] pg = new CorrelationRowType[k + 2];
            for (int i = 1; i <= k; i++)
                pg[i] = CorrelationRowType.Study;
            // pooled indicator for last element - needed by plot_cp
            pg[k + 1] = CorrelationRowType.Pooled;

            string[] title = MakeTitles(parameters, "studies", "study {0}", rawRows, out bool hasUserSuppliedLabels);
            title = Numerics.Utilities.CopyValidRows(title, copiesRemovingMissingRows.ValidRowsInOriginal, 0, rawRows, 1, k, 1);
            title[k + 1] = Charting.Renderer.AbstractForestishChartRenderer.ComboTi(string.Empty);

            // Pool
            double sumwt = 0.0;
            double sumsqwt = 0.0;
            double sumywt = 0.0;
            double[] wt = new double[k + 2];
            double[] dswt = new double[k + 2];
            for (int i = 1; i <= k; i++)
            {
                if (seY[i] == 0.0)
                    throw new InvalidDataException();
                wt[i] = 1.0 / (seY[i] * seY[i]);
                sumwt += wt[i];
                sumsqwt += wt[i] * wt[i];
                if (useRatio)
                    sumywt += Math.Log(y[i]) * wt[i];
                else
                    sumywt += y[i] * wt[i];
            }
            wt[k + 1] = Constant.MISSING;
            dswt[k + 1] = Constant.MISSING;
            double rmh = useRatio ? Math.Exp(sumywt / sumwt) : sumywt / sumwt;
            double sermh = Math.Pow(sumwt, -0.5);
            if (useRatio)
            {
                llrmh = Math.Exp(Math.Log(rmh) - sermh * cit);
                ulrmh = Math.Exp(Math.Log(rmh) + sermh * cit);
                if (llrmh > ulrmh)
                    Utilities.Utilities.Swap(ref llrmh, ref ulrmh);
                zrmh = Math.Log(rmh) / sermh;
            }
            else
            {
                llrmh = rmh - sermh * cit;
                ulrmh = rmh + sermh * cit;
                zrmh = rmh / sermh;
            }

            // Q (combinability)
            double qc = 0.0;
            for (int i = 1; i <= k; i++)
            {
                if (useRatio)
                    qc += wt[i] * Math.Pow(Math.Log(y[i]) - Math.Log(rmh), 2.0);
                else
                    qc += wt[i] * Math.Pow(y[i] - rmh, 2.0);
            }

            // DerSimonian-Laird random effects
            if (sumwt - sumsqwt / sumwt == 0.0)
                tausq = 0.0;
            else
                tausq = (qc - Convert.ToDouble(k - 1)) / (sumwt - sumsqwt / sumwt);
            if (tausq < 0.0)
                tausq = 0.0;
            double wlrr = 0.0;
            sumwt = 0.0;
            // sumsqwt = 0.0; - unused
            for (int i = 1; i <= k; i++)
            {
                double weight = 1.0 / (tausq + 1.0 / wt[i]);
                dswt[i] = weight;
                sumwt += weight;
                if (useRatio)
                    wlrr += Math.Log(y[i]) * weight;
                else
                    wlrr += y[i] * weight;
            }
            if (useRatio)
            {
                dsrr = Math.Exp(wlrr / sumwt);
                dsll = Math.Exp(wlrr / sumwt - cit / Math.Sqrt(sumwt));
                dsul = Math.Exp(wlrr / sumwt + cit / Math.Sqrt(sumwt));
            }
            else
            {
                dsrr = wlrr / sumwt;
                dsll = wlrr / sumwt - cit / Math.Sqrt(sumwt);
                dsul = wlrr / sumwt + cit / Math.Sqrt(sumwt);
            }
            double dsz = wlrr / Math.Sqrt(sumwt);
            if (dsll > dsul)
            {
                Utilities.Utilities.Swap(ref dsll, ref dsul);
            }

            ParameterBag outputParameters = new();
            outputParameters.AddOutput("stat", stat);
            outputParameters.AddOutput("pc", cco * 100);

            IList<ParameterBag> studiesList = new List<ParameterBag>();
            outputParameters.AddOutput("*studies", studiesList);
            for (int i = 1; i <= k; i++)
            {
                ParameterBag studiesParameters = new();
                studiesList.Add(studiesParameters);
                studiesParameters.AddOutput("st", i);
                studiesParameters.AddOutput("y", y[i]);
                studiesParameters.AddOutput("se", seY[i]);
                studiesParameters.AddOutput("from", llY[i]);
                studiesParameters.AddOutput("to", ulY[i]);
                studiesParameters.AddOutput("wt", 100 * wt[i] / Formatting.dsum(wt, 1));
                studiesParameters.AddOutput("dwt", 100 * dswt[i] / Formatting.dsum(dswt, 1));
                studiesParameters.AddOutput("standardized_effect", useRatio ? Math.Log(y[i]) : y[i]); // the statistic on the scale of its standard error and weight
                studiesParameters.AddOutput("lb", hasUserSuppliedLabels ? title[i] : string.Empty);
            }

            outputParameters.AddOutput("stat_fixed", stat.ToLower(CultureInfo.CurrentCulture));
            outputParameters.AddOutput("rmh", rmh);
            outputParameters.AddOutput("from_fixed", llrmh);
            outputParameters.AddOutput("to_fixed", ulrmh);

            outputParameters.AddOutput("task", "test " + stat + " " + statx.ToLower(CultureInfo.CurrentCulture));
            outputParameters.AddOutput("z", zrmh);
            outputParameters.AddOutput("p_fixed", MathDbl.zvalp2(zrmh));

            outputParameters.AddOutput("qc", k > 1 ? qc : 0.0);   // 0 by definition with one stratum, not the rounding residue of one squared deviation
            outputParameters.AddOutput("df", k - 1);
            outputParameters.AddOutput("xp", PDF.chivalp(qc, k - 1));
            outputParameters.AddOutput("tausq", tausq);
            IsquareNcc(host, qc, k, cco, cit, out double isq, out double llisq, out double ulisq);
            outputParameters.AddOutput("isq", isq);
            outputParameters.AddOutput("pc1", cco * 100);
            outputParameters.AddOutput("llisq", llisq);
            outputParameters.AddOutput("ulisq", ulisq);

            outputParameters.AddOutput("dsstat", stat.ToLower(CultureInfo.CurrentCulture));
            outputParameters.AddOutput("dsrr", dsrr);
            outputParameters.AddOutput("dsll", dsll);
            outputParameters.AddOutput("dsul", dsul);

            outputParameters.AddOutput("zstat", "test " + stat + statx.ToLower(CultureInfo.CurrentCulture));
            outputParameters.AddOutput("dz", dsz);
            outputParameters.AddOutput("dp", MathDbl.zvalp2(dsz));

            IList<ParameterBag> biasList = new List<ParameterBag>();
            outputParameters.AddOutput("*bias", biasList);
            ParameterBag biasParameters = new();
            biasList.Add(biasParameters);
            Transformation xform = Transformation.None;
            if (useRatio)
                xform = Transformation.Log;
            bool biasReported = Metabias(host, biasParameters, y, llY, ulY, k, ref cco, xform);
            FewStrata(outputParameters, biasList, biasReported);

            IList<ParameterBag> chartList = new List<ParameterBag>();
            outputParameters.AddOutput("*chart", chartList);
            ParameterBag chartParameters;

            if (k > 3)
            {
                chartParameters = new ParameterBag();
                chartList.Add(chartParameters);
                chartParameters.AddOutput("chart", ChartRendererFactory.PrepForLater(ChartType.BiasMA, new BiasMAOptions(ShallowCopy(y), odx, wt, k, stat.ToLower(CultureInfo.CurrentCulture), ShallowCopy(llY), ShallowCopy(ulY), cco, cit, rmh, xform, false)));
            }

            y[k + 1] = rmh;
            llY[k + 1] = llrmh;
            ulY[k + 1] = ulrmh;
            chartParameters = new ParameterBag();
            chartList.Add(chartParameters);
            chartParameters.AddOutput("chart", ChartRendererFactory.PrepForLater(ChartType.Correlation, new CorrelationOptions(k + 1, title, ShallowCopy(y), ShallowCopy(llY), ShallowCopy(ulY), wt, pg, "Summary meta-analysis plot [fixed effects]", stat.ToLower(CultureInfo.CurrentCulture) + " (" + Formatting.XRound(cco * 100, 1) + "% confidence interval" + ")", xform, !useRatio)));

            y[k + 1] = dsrr;
            llY[k + 1] = dsll;
            ulY[k + 1] = dsul;
            chartParameters = new ParameterBag();
            chartList.Add(chartParameters);
            chartParameters.AddOutput("chart", ChartRendererFactory.PrepForLater(ChartType.Correlation, new CorrelationOptions(k + 1, title, ShallowCopy(y), ShallowCopy(llY), ShallowCopy(ulY), dswt, pg, "Summary meta-analysis plot [random effects]", stat.ToLower(CultureInfo.CurrentCulture) + " (" + Formatting.XRound(cco * 100, 1) + "% confidence interval" + ")", xform, !useRatio)));

            return new StepOutput(outputParameters);
        }

        /// <summary>
        /// Correlation meta-analysis.  By the method of Hedges and Olkin each correlation r is put on the scale of Fisher's z, whose
        /// variance is 1 / (n - 3), pooled as the summary of the class describes, and the results are put back on the scale of r.
        /// By the method of Schmidt and Hunter the correlations themselves are pooled with the numbers of subjects as weights: the
        /// mean r; the variance of the correlations about it, with the same weights; the variance that sampling error accounts for,
        /// (1 - mean r squared)^2 / (mean n - 1); and what is left, the variance of the population correlations, not below 0.  The
        /// limits of the mean are from the standard error root(variance of r / k), the credibility limits from the variance that is
        /// left, and the chi-square of heterogeneity is k times the variance of r over the variance of sampling error, on k - 1
        /// degrees of freedom.
        /// </summary>
        /// <param name="host">The preferences, and where progress is shown.</param>
        /// <param name="parameters">"r": the correlation of each study, between -1 and 1; "n": its number of subjects, above 3;
        /// "studies" (may be left out): the labels; "gamma".</param>
        public static StepOutput RptMetaCorrelation(IPreferencesAndProgressBar host, ParameterBag parameters)
        {
            double tausq;
            double cit;
            int i;
            bool stratlab;
            double[] odx = null;

            double cco = parameters["gamma"].AsDouble;
            if (cco > 0)
            {
                double p = (1.0 - cco) / 2.0;
                cit = PDF.gauinv(1.0 - p);
            }
            else
            {
                cco = 0.95;
                cit = PDF.gauinv(0.975);
            }

            DataFrame rFrame = parameters["r"].AsDataFrame;
            DoubleVariable rVariable = (DoubleVariable) rFrame.Variables[0]; //  Ends up in y
            int k = rVariable.Length;
            double[] y = new double[k + 2];
            // double[] n = new double[k + 2 ]; - unused
            string[] title = new string[k + 2];
            CorrelationRowType[] pg = new CorrelationRowType[k + 2];
            for (i = 1; i <= k; i++)
            {
                y[i] = rVariable.Data[i - 1];
                pg[i] = CorrelationRowType.Study;
                if (y[i] <= -1.0 || y[i] >= 1.0)
                    throw new Exception($"r({i}) must be between -1 and 1 exclusive: a correlation of exactly 1 or -1 has no finite Fisher z");
            }
            // pooled indicator for last element - needed by plot_cp
            pg[k + 1] = CorrelationRowType.Pooled;

            DataFrame nFrame = parameters["n"].AsDataFrame;
            DoubleVariable nVariable = (DoubleVariable)nFrame.Variables[0];
            double[] seY = new double[k + 2];
            double[] llY = new double[k + 2];
            double[] ulY = new double[k + 2];
            double[] ss = new double[k + 2];
            for (i = 1; i <= k; i++)
            {
                double sampleSize = nVariable.Data[i - 1];
                if (sampleSize <= 3)
                    throw new Exception($"All values of n must be greater than 3 (the variance of Fisher's z is 1/(n - 3)). n({i}) is not");

                ss[i] = sampleSize;
                seY[i] = Math.Sqrt(1 / (sampleSize - 3));
                llY[i] = MathDbl.ztor(MathDbl.rtoz(y[i]) - cit * seY[i]);
                ulY[i] = MathDbl.ztor(MathDbl.rtoz(y[i]) + cit * seY[i]);
            }

            if (parameters.ContainsKey("studies") && parameters["studies"].HasData)
            {
                stratlab = true;
                DataFrame strataFrame = parameters["studies"].AsDataFrame;
                StringVariable strataVariable = (StringVariable)strataFrame.Variables[0];
                for (i = 1; i <= k; i++)
                {
                    string buf = strataVariable.Data[i - 1].Trim();
                    if (buf.Length > 0)
                    {
                        if (buf.Length > 50)
                            buf = buf.Substring(0, 50);
                        title[i] = buf;
                    }
                    else
                    {
                        title[i] = "study " + i;
                    }
                }
            }
            else
            {
                stratlab = false;
                for (i = 1; i <= k; i++)
                    title[i] = "study " + i;
            }
            title[k + 1] = Charting.Renderer.AbstractForestishChartRenderer.ComboTi(string.Empty);

            // Pool
            double sumwt = 0.0;
            double sumsqwt = 0.0;
            double sumywt = 0.0;
            double[] wt = new double[k + 2];
            double[] dswt = new double[k + 2];
            for (i = 1; i <= k; i++)
            {
                if (seY[i] == 0.0)
                    throw new InvalidDataException();
                wt[i] = ss[i] - 3;
                sumwt += wt[i];
                sumsqwt += wt[i] * wt[i];
                sumywt += MathDbl.rtoz(y[i]) * wt[i];
            }
            wt[k + 1] = Constant.MISSING;
            dswt[k + 1] = Constant.MISSING;
            double rmh = MathDbl.ztor(sumywt / sumwt);
            double sermh = Math.Pow(sumwt, -0.5);
            double llrmh = MathDbl.ztor(MathDbl.rtoz(rmh) - sermh * cit);
            double ulrmh = MathDbl.ztor(MathDbl.rtoz(rmh) + sermh * cit);
            if (llrmh > ulrmh)
                Utilities.Utilities.Swap(ref llrmh, ref ulrmh);
            double zrmh = MathDbl.rtoz(rmh) / sermh;

            // Q (combinability)
            double qc = 0.0;
            for (i = 1; i <= k; i++)
                qc += wt[i] * Math.Pow(MathDbl.rtoz(y[i]) - MathDbl.rtoz(rmh), 2.0);

            // DerSimonian-Laird random effects
            if (sumwt - sumsqwt / sumwt == 0.0)
                tausq = 0.0;
            else
                tausq = (qc - Convert.ToDouble(k - 1)) / (sumwt - sumsqwt / sumwt);
            if (tausq < 0.0)
                tausq = 0.0;
            double wlrr = 0.0;
            sumwt = 0.0;
            // sumsqwt = 0.0; - unused
            for (i = 1; i <= k; i++)
            {
                double weight = 1.0 / (tausq + 1.0 / wt[i]);
                dswt[i] = weight;
                sumwt += weight;
                wlrr += MathDbl.rtoz(y[i]) * weight;
            }
            double dsrr = MathDbl.ztor(wlrr / sumwt);
            double dsll = MathDbl.ztor(wlrr / sumwt - cit / Math.Sqrt(sumwt));
            double dsul = MathDbl.ztor(wlrr / sumwt + cit / Math.Sqrt(sumwt));
            double dsz = wlrr / Math.Sqrt(sumwt);
            if (dsll > dsul)
                Utilities.Utilities.Swap(ref dsll, ref dsul);

            // Schmidt-Hunter
            double varR = 0, wmrCrll, wmrCrul;
            double wmr = 0.0;
            double tot = 0.0;
            for (i = 1; i <= k; i++)
                tot += ss[i];
            for (i = 1; i <= k; i++)
                wmr += y[i] * ss[i];
            wmr /= tot;
            for (i = 1; i <= k; i++)
                varR += ss[i] * Math.Pow(y[i] - wmr, 2.0);
            varR /= tot;
            double varE = Math.Pow(1.0 - Math.Pow(wmr, 2.0), 2.0) / (tot / Convert.ToDouble(k) - 1.0);
            double percvar = 100.0 * varE / varR;
            double varP = varR - varE;
            if (varP < 0.0)
            {
                varP = 0.0;
                percvar = 100.0;
            }
            double wmrZ = wmr / Math.Sqrt(varR / Convert.ToDouble(k));
            double wmrP = MathDbl.zvalp2(wmrZ);
            double wmrLcl = wmr - cit * Math.Sqrt(varR / Convert.ToDouble(k));
            double wmrUcl = wmr + cit * Math.Sqrt(varR / Convert.ToDouble(k));
            double sres = Math.Sqrt(varP);
            if (varP > 0.0)
            {
                wmrCrll = wmr - cit * sres;
                wmrCrul = wmr + cit * sres;
            }
            else
            {
                wmrCrll = Constant.MISSING;
                wmrCrul = Constant.MISSING;
            }
            double hetX2 = Convert.ToDouble(k) * varR / varE;
            double hetP = PDF.chivalp(hetX2, Convert.ToDouble(k - 1));

            const string stat = "Correlation";
            ParameterBag outputParameters = new();
            outputParameters.AddOutput("stat", stat);
            outputParameters.AddOutput("pc", cco * 100);

            IList<ParameterBag> studiesList = new List<ParameterBag>();
            outputParameters.AddOutput("*studies", studiesList);
            for (i = 1; i <= k; i++)
            {
                ParameterBag studiesParameters = new();
                studiesList.Add(studiesParameters);
                studiesParameters.AddOutput("st", i);
                studiesParameters.AddOutput("n", ss[i]);
                studiesParameters.AddOutput("y", y[i]);
                studiesParameters.AddOutput("from", llY[i]);
                studiesParameters.AddOutput("to", ulY[i]);
                studiesParameters.AddOutput("wt", 100 * wt[i] / Formatting.dsum(wt, 1));
                studiesParameters.AddOutput("dwt", 100 * dswt[i] / Formatting.dsum(dswt, 1));
                studiesParameters.AddOutput("nwt", 100 * ss[i] / Formatting.dsum(ss, 1));
                studiesParameters.AddOutput("yi", MathDbl.rtoz(y[i]));
                studiesParameters.AddOutput("vi", seY[i] * seY[i]);
                studiesParameters.AddOutput("lb", stratlab ? title[i] : string.Empty);
            }

            outputParameters.AddOutput("stat_fixed", stat.ToLower(CultureInfo.CurrentCulture));
            outputParameters.AddOutput("rmh", rmh);
            outputParameters.AddOutput("from_fixed", llrmh);
            outputParameters.AddOutput("to_fixed", ulrmh);

            outputParameters.AddOutput("z", zrmh);
            outputParameters.AddOutput("p_fixed", MathDbl.zvalp2(zrmh));

            outputParameters.AddOutput("qc", k > 1 ? qc : 0.0);   // 0 by definition with one stratum, not the rounding residue of one squared deviation
            outputParameters.AddOutput("df", k - 1);
            outputParameters.AddOutput("xp", PDF.chivalp(qc, k - 1));
            outputParameters.AddOutput("tausq", tausq);

            IsquareNcc(host, qc, k, cco, cit, out double isq, out double llisq, out double ulisq);
            outputParameters.AddOutput("isq", isq);
            outputParameters.AddOutput("pc1", cco * 100);
            outputParameters.AddOutput("llisq", llisq);
            outputParameters.AddOutput("ulisq", ulisq);

            outputParameters.AddOutput("dsstat", stat.ToLower(CultureInfo.CurrentCulture));
            outputParameters.AddOutput("dsrr", dsrr);
            outputParameters.AddOutput("dsll", dsll);
            outputParameters.AddOutput("dsul", dsul);

            outputParameters.AddOutput("dz", dsz);
            outputParameters.AddOutput("dp", MathDbl.zvalp2(dsz));

            outputParameters.AddOutput("wmr", wmr);
            outputParameters.AddOutput("wmr_lcl", wmrLcl);
            outputParameters.AddOutput("wmr_ucl", wmrUcl);
            outputParameters.AddOutput("wmr_z", wmrZ);
            outputParameters.AddOutput("wmr_p", wmrP);
            outputParameters.AddOutput("var_r", varR);
            outputParameters.AddOutput("var_e", varE);
            outputParameters.AddOutput("var_p", varP);
            outputParameters.AddOutput("wmr_crll", wmrCrll);
            outputParameters.AddOutput("wmr_crul", wmrCrul);
            outputParameters.AddOutput("wmr4", wmr / 4.0);
            outputParameters.AddOutput("sres", sres);
            outputParameters.AddOutput("percvar", percvar);
            outputParameters.AddOutput("het_x2", hetX2);
            outputParameters.AddOutput("het_p", hetP);

            IList<ParameterBag> biasList = new List<ParameterBag>();
            outputParameters.AddOutput("*bias", biasList);
            ParameterBag biasParameters = new();
            biasList.Add(biasParameters);
            bool biasReported = Metabias(host, biasParameters, y, llY, ulY, k, ref cco, Transformation.Z);
            FewStrata(outputParameters, biasList, biasReported);

            IList<ParameterBag> chartList = new List<ParameterBag>();
            outputParameters.AddOutput("*chart", chartList);
            ParameterBag chartParameters;

            if (k > 3)
            {
                chartParameters = new ParameterBag();
                chartList.Add(chartParameters);
                chartParameters.AddOutput("chart", ChartRendererFactory.PrepForLater(ChartType.BiasMA, new BiasMAOptions(ShallowCopy(y), odx, wt, k, "Correlation", ShallowCopy(llY), ShallowCopy(ulY), cco, cit, wmr, Transformation.Z, false)));
            }

            y[k + 1] = rmh;
            llY[k + 1] = llrmh;
            ulY[k + 1] = ulrmh;
            chartParameters = new ParameterBag();
            chartList.Add(chartParameters);
            chartParameters.AddOutput("chart", ChartRendererFactory.PrepForLater(ChartType.Correlation, new CorrelationOptions(k + 1, title, ShallowCopy(y), ShallowCopy(llY), ShallowCopy(ulY), wt, pg, "Correlation (Hedges-Olkin fixed effects) meta-analysis plot", stat + " (" + Formatting.XRound(cco * 100, 1) + "% confidence interval" + ")", Transformation.None, false)));

            y[k + 1] = dsrr;
            llY[k + 1] = dsll;
            ulY[k + 1] = dsul;
            chartParameters = new ParameterBag();
            chartList.Add(chartParameters);
            chartParameters.AddOutput("chart", ChartRendererFactory.PrepForLater(ChartType.Correlation, new CorrelationOptions(k + 1, title, ShallowCopy(y), ShallowCopy(llY), ShallowCopy(ulY), wt, pg, "Correlation (Hedges-Olkin random effects) meta-analysis plot", stat + " (" + Formatting.XRound(cco * 100, 1) + "% confidence interval" + ")", Transformation.None, false)));

            y[k + 1] = wmr;
            llY[k + 1] = wmrLcl;
            ulY[k + 1] = wmrUcl;
            chartParameters = new ParameterBag();
            chartList.Add(chartParameters);
            chartParameters.AddOutput("chart", ChartRendererFactory.PrepForLater(ChartType.Correlation, new CorrelationOptions(k + 1, title, y, llY, ulY, wt, pg, "Correlation (Schmidt-Hunter) meta-analysis plot", stat + " (" + Formatting.XRound(cco * 100, 1) + "% confidence interval" + ")", Transformation.None, false)));

            return new StepOutput(outputParameters);
        }

        /// <summary>
        /// Limits of the odds ratio of a table by the score test (Cornfield): see CornfieldLimit.  No report calls it at present.
        /// </summary>
        public static void OrciCorn(IPreferences host, ref double conflev, ref double a, ref double b, ref double c, ref double d, out double odr, out double ll, out double ul)
        {
            //  ref Alan Agresti R script http://web.stat.ufl.edu/~aa/cda/R/two_sample/R2/
            double aa;
            double bb;
            double cc;
            double dd;
            if (b * c == 0.0)
            {
                ContinuityCorrect(host, a, b, c, d, out aa, out bb, out cc, out dd);
                odr = aa * dd / (bb * cc);
            }
            else
            {
                odr = a * d / (b * c);
                aa = a;
                bb = b;
                cc = c;
                dd = d;
            }
            double x1 = aa;
            double n1 = aa + cc;
            double x2 = bb;
            double n2 = bb + dd;
            double px = x1 / n1;
            double py = x2 / n2;
            double theta;
            if (((aa == 0.0) & (bb == 0.0)) | ((aa == aa + cc) & (bb == bb + dd)))
            {
                ul = double.PositiveInfinity;
                ll = 0.0;
            }
            else if ((aa == 0.0) | (bb == n2))
            {
                ll = 0.0;
                theta = 0.01 / n2;
                ul = CornfieldLimit(x1, n1, x2, n2, conflev, ref theta, 1.0);
            }
            else if ((aa == n1) | (bb == 0.0))
            {
                ul = double.PositiveInfinity;
                theta = 100.0 * n1;
                ll = CornfieldLimit(x1, n1, x2, n2, conflev, ref theta, 0.0);
            }
            else
            {
                theta = px / (1 - px) / (py / (1 - py)) / 1.1;
                ll = CornfieldLimit(x1, n1, x2, n2, conflev, ref theta, 0.0);
                theta = px / (1 - px) / (py / (1 - py)) * 1.1;
                ul = CornfieldLimit(x1, n1, x2, n2, conflev, ref theta, 1.0);
            }
        }

        /// <summary>
        /// One limit of the odds ratio by the score test.  From its starting value the odds ratio is moved by one part in a thousand
        /// at a time, down for the lower limit (t = 0) and up for the upper (t = 1), until the score statistic of the table at
        /// that odds ratio reaches the chi-square of the confidence level.
        /// </summary>
        private static double CornfieldLimit(double x, double nx, double y, double ny, double conflev, ref double lim, double t)
        {
            double ci = 0;

            double z = PDF.ppchi2(conflev, 1.0, out int fault);
            if (fault != 0)
            {
                lim = Constant.MISSING;
            }
            else
            {
                double px = x / nx;
                double score = 0.0;
                int iter = 0;
                while (score < z)
                {
                    double a = ny * (lim - 1.0);
                    double b = nx * lim + ny - (x + y) * (lim - 1.0);
                    double c = -(x + y);
                    double p2D = (-b + Math.Sqrt(Math.Pow(b, 2.0) - 4.0 * a * c)) / (2.0 * a);
                    double p1D = p2D * lim / (1.0 + p2D * (lim - 1.0));
                    score = Math.Pow(nx * (px - p1D), 2.0) * (1.0 / (nx * p1D * (1.0 - p1D)) + 1.0 / (ny * p2D * (1.0 - p2D)));
                    ci = lim;
                    if (t == 0.0)
                    {
                        lim = ci / 1.001;
                    }
                    else
                    {
                        lim = ci * 1.001;
                    }
                    iter += 1;
                    if (iter > 1000000)
                    {
                        ci = Constant.MISSING;
                        break;
                    }
                }
                return ci;
            }
            return 0;
        }

        /// <summary>
        /// I-squared with its test-based limits, as IsquareNcc gives them when the exact method is off.  No report calls it at present.
        /// </summary>
        /// <param name="q">Cochran's Q.</param>
        /// <param name="k">The number of studies.</param>
        /// <param name="cit">The normal deviate of the confidence level.</param>
        /// <param name="isq">On return, I-squared as a percentage.</param>
        /// <param name="ll">On return, the lower limit; missing with fewer than three studies.</param>
        /// <param name="ul">On return, the upper limit.</param>
        /// <remarks>Higgins P, Thompson S. Quantifying heterogeneity in meta-analysis. Stats in Medicine 2002; 21: 1539-1558</remarks>
        public static void Isquare(double q, int k, double cit, out double isq, out double ll, out double ul)
        {
            double df = Convert.ToDouble(k - 1);
            double hsq = q / df;
            isq = 100.0 * Math.Max(0, (hsq - 1.0) / hsq);
            if (k < 3)
            {
                ll = Constant.MISSING;
                ul = Constant.MISSING;
            }
            else
            {
                double selh;
                if (q > Convert.ToDouble(k))
                {
                    selh = 0.5 * (Math.Log(q) - Math.Log(df)) / (Math.Sqrt(2.0 * q) - Math.Sqrt(2.0 * Convert.ToDouble(k) - 3.0));
                }
                else
                {
                    selh = Math.Sqrt(1.0 / (2.0 * Convert.ToDouble(k - 2)) * (1.0 - 1.0 / (3.0 * Math.Pow(Convert.ToDouble(k - 2), 2.0))));
                }
                double llh = Math.Exp(Math.Log(Math.Sqrt(hsq)) - cit * selh);
                double ulh = Math.Exp(Math.Log(Math.Sqrt(hsq)) + cit * selh);
                ll = 100.0 * Math.Max(0, (Math.Pow(llh, 2.0) - 1.0) / Math.Pow(llh, 2.0));
                ul = 100.0 * Math.Max(0, (Math.Pow(ulh, 2.0) - 1.0) / Math.Pow(ulh, 2.0));
            }
        }

        /// <summary>
        /// The continuity correction of a table with a cell of nothing, by the preference MetaCC.  A number between 0 and 1 (0.5 if
        /// the preference is 0) is added to every cell.  For the treatment arm correction (-9) what is added to the cells of a group
        /// is in proportion to the size of the group: n1 / (n1 + n2) to a and c, the cells of the first group, and n2 / (n1 + n2) to
        /// b and d; if a group has nobody, 0.5 is added to every cell.
        /// </summary>
        private static void ContinuityCorrect(IPreferences host, double a, double b, double c, double d, out double ax, out double bx, out double cx, out double dx)
        {
            double x = host.Preferences.MetaCC == 0.0 ? 0.5 : host.Preferences.MetaCC;
            if (x > 0.0 && x < 1.0)
            {
                ax = a + x;
                bx = b + x;
                cx = c + x;
                dx = d + x;
            }
            else if (x == -9.0)
            {
                double nt = a + c;
                double nc = b + d;
                // the correction of each group is in proportion to the size of the group: both are above nothing when both groups
                // have subjects, and no cell of the corrected table is then nothing
                if (nt > 0.0 && nc > 0.0)
                {
                    double r = nc / nt;
                    bx = b + r / (r + 1.0);
                    dx = d + r / (r + 1.0);
                    ax = a + 1.0 / (r + 1.0);
                    cx = c + 1.0 / (r + 1.0);
                }
                else
                {
                    x = 0.5;
                    ax = a + x;
                    bx = b + x;
                    cx = c + x;
                    dx = d + x;
                }
            }
            else
            {
                x = 0.5;
                ax = a + x;
                bx = b + x;
                cx = c + x;
                dx = d + x;
            }
        }

        /// <summary>
        /// Proportion meta-analysis.  The proportion of each study is put on the scale of the double arcsine of Freeman and Tukey
        /// (ArcsineP), whose variance is 1 / (n + 0.5), pooled as the summary of the class describes, and the results are put back on
        /// the scale of a proportion (ArcsineInv) by the method that is chosen.  The limits of each study are the exact limits of a
        /// binomial proportion.  If no study has an event the pooled proportion and its lower limit are given as 0, and if every
        /// subject of every study has one the pooled proportion and its upper limit as 1.
        /// </summary>
        /// <param name="host">The preferences, and where progress is shown.</param>
        /// <param name="parameters">"sn" and "sr": the number of subjects and the number with the event of each study; "method":
        /// "doubleArcsine" for the inverse of the double arcsine with the harmonic mean of the numbers of subjects, anything else
        /// for the square of the sine of half the pooled value; "strata" (may be left out); "gamma".</param>
        public static StepOutput RptProportionMeta(IPreferencesAndProgressBar host, ParameterBag parameters)
        {
            double cco = parameters["gamma"].AsDouble;
            VarianceStabilisationMethod method = "doubleArcsine".Equals(parameters["method"].AsString) ? VarianceStabilisationMethod.DoubleArcsine : VarianceStabilisationMethod.ArcsineSquareRoot;
            if (cco < 0)
                cco = 0.95;
            double cit = PDF.gauinv(1.0 - (1.0 - cco) / 2.0);

            double fudge = 0.5;

            DataFrame snFrame = parameters["sn"].AsDataFrame;
            DoubleVariable snVariable = (DoubleVariable)snFrame.Variables[0];
            DataFrame srFrame = parameters["sr"].AsDataFrame;
            DoubleVariable srVariable = (DoubleVariable)srFrame.Variables[0];
            int rawRows = snVariable.Length;

            DoubleArraysAndBooleans copiesRemovingMissingRows = Numerics.Utilities.RemoveMissingRows(new[] { snVariable.Data, srVariable.Data }, 0, rawRows, 1, 1);
            double[] sn = copiesRemovingMissingRows.ArraysWithMissingRowsRemoved[0];
            double[] sr = copiesRemovingMissingRows.ArraysWithMissingRowsRemoved[1];

            string[] title = MakeTitles(parameters, "strata", "stratum {0}", rawRows, out bool hasUserSuppliedLabels);

            int k = copiesRemovingMissingRows.ArraysWithMissingRowsRemoved[0].Length - 2; /* 1-based, 1 extra for pooling */
            title = Numerics.Utilities.CopyValidRows(title, copiesRemovingMissingRows.ValidRowsInOriginal, 0, rawRows, 1, k, 1);
            title[k + 1] = Charting.Renderer.AbstractForestishChartRenderer.ComboTi(string.Empty);

            bool allRZero = true;
            bool allREqualN = true;
            for (int i = 1; i <= k; i++)
            {
                if (sr[i] > 0)
                    allRZero = false;
                if (sr[i] < sn[i])
                    allREqualN = false;
                if (sr[i] < 0.0 || sn[i] < sr[i])
                    throw new InvalidDataException("Each value of r must be between 0 and its corresponding n");
            }

            double[] y = new double[k + 2];
            double[] seY = new double[k + 2];
            double[] llY = new double[k + 2];
            double[] ulY = new double[k + 2];
            CorrelationRowType[] pg = new CorrelationRowType[k + 2];
            for (int i = 1; i <= k; i++)
                pg[i] = CorrelationRowType.Study;
            pg[k + 1] = CorrelationRowType.Pooled;

            // Pool
            double sumwt = 0.0;
            double sumsqwt = 0.0;
            double sumywt = 0.0;
            double[] wt = new double[k + 2];
            double[] dswt = new double[k + 2];
            for (int i = 1; i <= k; i++)
            {
                // arcsine transformation to stabilize the variance of the proportion
                y[i] = ArcsineP(sr[i], sn[i]);
                seY[i] = ArcsineSe(sn[i], fudge);
                if (seY[i] == 0.0)
                    throw new InvalidDataException();
                wt[i] = 1.0 / (seY[i] * seY[i]);
                sumwt += wt[i];
                sumsqwt += wt[i] * wt[i];
                sumywt += y[i] * wt[i];
            }
            wt[k + 1] = Constant.MISSING;
            dswt[k + 1] = Constant.MISSING;
            double rmh = sumywt / sumwt;
            double sermh = Math.Pow(sumwt, -0.5);
            double llrmh = rmh - sermh * cit;
            double ulrmh = rmh + sermh * cit;
            // double x2rmh = Math.Pow( ( rmh / sermh ), 2.0 ); - never used

            // Q (combinability)
            double qc = 0.0;
            for (int i = 1; i <= k; i++)
                qc += wt[i] * Math.Pow(y[i] - rmh, 2.0);

            // DerSimonian-Laird random effects
            double tausq;
            if (sumwt - sumsqwt / sumwt == 0.0)
                tausq = 0.0;
            else
                tausq = (qc - (k - 1)) / (sumwt - sumsqwt / sumwt);
            if (tausq < 0.0)
                tausq = 0.0;
            double wlrr = 0.0;
            sumwt = 0.0;
            // sumsqwt = 0.0; unused
            for (int i = 1; i <= k; i++)
            {
                double weight = 1.0 / (tausq + 1.0 / wt[i]);
                dswt[i] = weight;
                sumwt += weight;
                wlrr += y[i] * weight;
            }
            double dspr = wlrr / sumwt;
            double dsll = wlrr / sumwt - cit / Math.Sqrt(sumwt);
            double dsul = wlrr / sumwt + cit / Math.Sqrt(sumwt);
            // double dsx2 = Math.Pow( wlrr, 2.0 ) / sumwt; - unused
            if (dsll > dsul)
                Utilities.Utilities.Swap(ref dsll, ref dsul);

            // convert back to proportion scale
            double[,] o = new double[k + 1, 4 + 1];
            rmh = ArcsineInv(rmh, sn, method);
            llrmh = ArcsineInv(llrmh, sn, method);
            ulrmh = ArcsineInv(ulrmh, sn, method);
            dspr = ArcsineInv(dspr, sn, method);
            dsll = ArcsineInv(dsll, sn, method);
            dsul = ArcsineInv(dsul, sn, method);

            // Set limits (ticket #495, 2012-05-08)
            if (allRZero)
            {
                rmh = 0;
                llrmh = 0;
            }
            else if (allREqualN)
            {
                rmh = 1;
                ulrmh = 1;
            }

            for (int i = 1; i <= k; i++)
            {
                y[i] = sr[i] / sn[i];
                o[i, 1] = sr[i];
                o[i, 2] = sn[i];
                o[i, 3] = rmh;
            }

            ParameterBag outputParameters = new();

            IList<ParameterBag> inputsList = new List<ParameterBag>();
            outputParameters.AddOutput("*inputs", inputsList);
            for (int i = 1; i <= k; i++)
            {
                ParameterBag inputsParameters = new();
                inputsList.Add(inputsParameters);
                inputsParameters.AddOutput("st", i);
                inputsParameters.AddOutput("r", sr[i]);
                inputsParameters.AddOutput("n", sn[i]);
                inputsParameters.AddOutput("lb", hasUserSuppliedLabels ? title[i] : string.Empty);
            }

            outputParameters.AddOutput("pc", cco * 100);

            IList<ParameterBag> proportionsList = new List<ParameterBag>();
            outputParameters.AddOutput("*proportions", proportionsList);
            for (int i = 1; i <= k; i++)
            {
                ParameterBag proportionsParameters = new();
                proportionsList.Add(proportionsParameters);
                proportionsParameters.AddOutput("st", i);
                proportionsParameters.AddOutput("p", sr[i] / sn[i]);
                MathDbl.binci(sr[i], sn[i], out llY[i], out ulY[i], cco, out string tmp);
                proportionsParameters.AddOutput("from_y", llY[i]);
                proportionsParameters.AddOutput("to_y", ulY[i]);
                proportionsParameters.AddOutput("wt", 100 * wt[i] / Formatting.dsum(wt, 1));
                proportionsParameters.AddOutput("dwt", 100 * dswt[i] / Formatting.dsum(dswt, 1));
                //  the standardised effect is the Freeman-Tukey transform, on the scale of the variance beside it and of the pooling (y[i] now holds the proportion for the charts)
                proportionsParameters.AddOutput("yi", ArcsineP(sr[i], sn[i]));
                proportionsParameters.AddOutput("vi", seY[i] * seY[i]);
                if (hasUserSuppliedLabels)
                    tmp = title[i] + tmp;
                proportionsParameters.AddOutput("lb", tmp);
            }

            outputParameters.AddOutput("methodLabel", method == VarianceStabilisationMethod.ArcsineSquareRoot ? "Stuart-Ord (inverse double arcsine square root)" : "Miller (exact inverse Freeman-Tukey double arcsine)");

            outputParameters.AddOutput("rmh", rmh);
            outputParameters.AddOutput("from", llrmh);
            outputParameters.AddOutput("to", ulrmh);

            outputParameters.AddOutput("qc", k > 1 ? qc : 0.0);   // 0 by definition with one stratum, not the rounding residue of one squared deviation
            outputParameters.AddOutput("df", k - 1);
            outputParameters.AddOutput("xp", PDF.chivalp(qc, k - 1));
            outputParameters.AddOutput("tausq", tausq);
            IsquareNcc(host, qc, k, cco, cit, out double isq, out double llisq, out double ulisq);
            outputParameters.AddOutput("isq", isq);
            outputParameters.AddOutput("pc1", cco * 100);
            outputParameters.AddOutput("llisq", llisq);
            outputParameters.AddOutput("ulisq", ulisq);

            outputParameters.AddOutput("dspr", dspr);
            outputParameters.AddOutput("from_ds", dsll);
            outputParameters.AddOutput("to_ds", dsul);

            IList<ParameterBag> eggerList = new List<ParameterBag>();
            outputParameters.AddOutput("*egger", eggerList);
            ParameterBag eggerParameters = new();
            eggerList.Add(eggerParameters);
            bool biasReported = Metabias(host, eggerParameters, y, llY, ulY, k, ref cco, Transformation.None);
            FewStrata(outputParameters, eggerList, biasReported);

            IList<ParameterBag> harbordList = new List<ParameterBag>();
            outputParameters.AddOutput("*harbord", harbordList);
            ParameterBag harbordParameters = new();
            harbordList.Add(harbordParameters);
            ModMetabias(host, harbordParameters, o, k, cco, 3);
            if (!biasReported)
                harbordList.Clear();

            IList<ParameterBag> chartList = new List<ParameterBag>();
            outputParameters.AddOutput("*chart", chartList);
            ParameterBag chartParameters;

            if (k > 3)
            {
                chartParameters = new ParameterBag();
                chartList.Add(chartParameters);
                chartParameters.AddOutput("chart", ChartRendererFactory.PrepForLater(ChartType.BiasMA, new BiasMAOptions(ShallowCopy(y), sn, wt, k, "Proportion", ShallowCopy(llY), ShallowCopy(ulY), cco, cit, rmh, Transformation.None, false)));
            }

            y[k + 1] = rmh;
            llY[k + 1] = llrmh;
            ulY[k + 1] = ulrmh;

            chartParameters = new ParameterBag();
            chartList.Add(chartParameters);
            chartParameters.AddOutput("chart", ChartRendererFactory.PrepForLater(ChartType.Correlation, new CorrelationOptions(k + 1, title, ShallowCopy(y), ShallowCopy(llY), ShallowCopy(ulY), wt, pg, "Proportion meta-analysis plot [fixed effects]", "proportion" + " (" + Formatting.XRound(cco * 100, 1) + "% confidence interval" + ")", Transformation.None, false)));

            y[k + 1] = dspr;
            llY[k + 1] = dsll;
            ulY[k + 1] = dsul;
            chartParameters = new ParameterBag();
            chartList.Add(chartParameters);
            chartParameters.AddOutput("chart", ChartRendererFactory.PrepForLater(ChartType.Correlation, new CorrelationOptions(k + 1, title, ShallowCopy(y), ShallowCopy(llY), ShallowCopy(ulY), dswt, pg, "Proportion meta-analysis plot [random effects]", "proportion" + " (" + Formatting.XRound(cco * 100, 1) + "% confidence interval" + ")", Transformation.None, false)));

            return new StepOutput(outputParameters);
        }

        /// <summary>
        /// The double arcsine of r events in n subjects: arcsin root(r / (n + 1)) + arcsin root((r + 1) / (n + 1)).
        /// </summary>
        private static double ArcsineP(double r, double n)
        {
            // Anscombe (1948)
            // arcsine_p = ASin(Math.Sqrt((r + 3# / 8#) / (N + 3# / 4#)))
            // 
            // Freeman-Tukey
            return Math.Asin(Math.Sqrt(r / (n + 1.0))) + Math.Asin(Math.Sqrt((r + 1.0) / (n + 1.0)));
        }

        /// <summary>
        /// The standard error of the double arcsine of a proportion of n subjects, root(1 / (n + fudge)); fudge is 0.5.
        /// </summary>
        private static double ArcsineSe(double n, double fudge)
        {
            // Anscombe (1948)
            // arcsine_se = (N ^ (-0.5)) / 2#
            // 
            // Freeman-Tukey
            //  or N + 0.5
            return Math.Sqrt(1.0 / (n + fudge));
        }

        /// <summary>
        /// The proportion that has the double arcsine t.  By the first method, the square of the sine of t / 2.  By the second, with
        /// m the harmonic mean of the numbers of subjects of the studies: 0.5 (1 - sign(cos t) root(1 - (sin t + (sin t - 1 / sin t) / m)^2)),
        /// and 0 or 1 if t is beyond the double arcsine of no events or of m events in m subjects.
        /// </summary>
        /// <param name="t">The double arcsine.</param>
        /// <param name="n">The numbers of subjects of the studies, from element 1; the last element is not one of them.</param>
        /// <param name="method">The method.</param>
        private static double ArcsineInv(double t, double[] n, VarianceStabilisationMethod method)
        {
            // Anscombe (1948)
            // arcsine_inv = Sin(P) ^ 2#
            switch (method)
            {
                case VarianceStabilisationMethod.ArcsineSquareRoot:
                    return Math.Pow(Math.Sin(t / 2.0), 2);
                case VarianceStabilisationMethod.DoubleArcsine:
                    double hmn = 0;
                    //  n(0) is empty, n(n.Length - 1) is empty
                    for (int i = 1; i <= n.Length - 2; i++)
                    {
                        hmn += 1.0 / n[i];
                    }
                    hmn = (n.Length - 2) / hmn;

                    if (t > ArcsineP(hmn, hmn))
                        return 1.0;
                    if (t < ArcsineP(0, hmn))
                        return 0;
                    return 0.5 * (1.0 - Math.Sign(Math.Cos(t)) * Math.Sqrt(1.0 - Math.Pow(Math.Sin(t) + (Math.Sin(t) - 1.0 / Math.Sin(t)) / hmn, 2.0)));
                default:
                    throw new ArgumentOutOfRangeException(nameof(method), method, "Only Arcsine and DoubleArcsine are known");
            }
        }

        /// <summary>
        /// Whether a table is taken into the pooling of the odds ratio: it is left out if there is no event in either group, if every
        /// subject of both groups has the event, or if a group has nobody.
        /// </summary>
        private static bool IncludeTable(double[,] o, int i)
        {
            // a group of nobody tells nothing of the difference between the groups
            if (o[i, 1] + o[i, 3] <= 0.0 || o[i, 2] + o[i, 4] <= 0.0)
                return false;
            return !(o[i, 1] == 0.0 && o[i, 2] == 0.0 || o[i, 3] == 0.0 && o[i, 4] == 0.0);
        }

        /// <summary>
        /// Whether a study is taken into the pooling of the relative risk: it is left out if there is no event in either group, or if a
        /// group has nobody.  A study in which every subject has the event has a relative risk, of 1, and is taken in.
        /// </summary>
        private static bool IncludeRelativeRisk(double[,] o, int i)
        {
            if (o[i, 1] + o[i, 3] <= 0.0 || o[i, 2] + o[i, 4] <= 0.0)
                return false;
            return !(o[i, 1] == 0.0 && o[i, 2] == 0.0);
        }

        /// <summary>
        /// The label of table i in a report: its own label if the tables have labels, with a note of the continuity correction if it
        /// had one, or "* (excluded)" if it is not pooled (IncludeTable).
        /// </summary>
        public static string GetMetaLabel(IPreferences host, double[,] o, int i, bool stratlab, bool[] cced, string[] title)
        {
            return GetMetaLabel(host, IncludeTable(o, i), i, stratlab, cced, title);
        }

        /// <summary>
        /// The label of table i in a report, for a table that is known to be pooled or not.
        /// </summary>
        public static string GetMetaLabel(IPreferences host, bool included, int i, bool stratlab, bool[] cced, string[] title)
        {
            if (included)
                return (stratlab ? title[i] : string.Empty)
                    + (cced[i]
                        ? " [CC = " + (host.Preferences.MetaCC == -9.0 ? "treatment arm" : host.Preferences.MetaCC.ToString()) + "]"
                        : string.Empty);
            else
                return "* (excluded)";
        }

        /// <summary>
        /// The bias indicator of Harbord and Egger: for each study the score Z of the test of no difference and its variance V, from
        /// the counts as they are; Z / root(V) is regressed on root(V), and the intercept is the bias, with a t test on two degrees of
        /// freedom fewer than there are studies.  Nothing is given with fewer than four studies.
        /// For the odds ratio (method 1) Z = a - (a + b)(a + c) / n and V is the variance of a with the totals of the table given.
        /// For the relative risk (method 2) Z = (a n - (a + b)(a + c)) / (c + d) and V = (a + b)(a + c)(b + d) / (n (c + d)).
        /// For a proportion (method 3) the table has the events in o[i, 1], the number of subjects in o[i, 2] and the pooled
        /// proportion p in o[i, 3]: Z = events - n p and V = n p (1 - p).
        /// </summary>
        /// <param name="host">The preferences.</param>
        /// <param name="outputParameters">Where the results are put.</param>
        /// <param name="o">The tables.</param>
        /// <param name="k">The number of tables.</param>
        /// <param name="cco">The confidence level of the analysis; the limits of the bias are at the level of BiasTestConfidenceLevel.</param>
        /// <param name="method">1, 2 or 3, as above.</param>
        public static void ModMetabias(IPreferences host, ParameterBag outputParameters, double[,] o, int k, double cco, int method)
        {
            //  Horbord et al 2006
            // get linear regression of z on sqr(v)
            //  This was cco - (1 - cco) / 2, which gave a 92.5% interval at 95%; the help has always described, and earlier versions printed, a 90% interval
            double ncco = BiasTestConfidenceLevel(cco > 0.0 ? cco : 0.95);
            double sumx = 0.0;
            double sumy = 0.0;
            double sumxy = 0.0;
            double sxs = 0.0;
            double sys = 0;
            int realk = 0;
            for (int i = 1; i <= k; i++)
            {
                if (IncludeTable(o, i))
                {
                    double a = o[i, 1];
                    double b = o[i, 2];
                    double c = o[i, 3];
                    double d = o[i, 4];
                    double n = a + b + c + d;
                    if (n > 0)
                    {
                        double v;
                        double z;
                        if (method == 3)
                        {
                            if (b <= 0 || c <= 0)
                            {
                                a += 0.5;
                                b += 0.5;
                                c += 0.5;
                            }
                            // relative risk parameters from Whitehead
                            z = a - b * c;
                            v = b * c * (1.0 - c);
                        }
                        else if (method == 2)
                        {
                            // the score and its variance are from the counts as they are: a cell of nothing needs no correction
                            // relative risk parameters from Whitehead
                            z = (a * n - (a + b) * (a + c)) / (c + d);
                            v = (b + d) * (a + c) * (a + b) / (n * (c + d));
                        }
                        else
                        {
                            // the score and its variance are from the counts as they are: a cell of nothing needs no correction
                            // efficient score
                            z = a - (a + b) * (a + c) / n;
                            // hypergeometric variance of the score (Harbord, Egger and Sterne 2006)
                            v = (a + b) * (c + d) * (a + c) * (b + d) / (n * n * (n - 1.0));
                        }
                        // a score without a variance (no events, no subjects without the event, one subject) tells nothing
                        if (!(v > 0.0) || double.IsInfinity(v))
                            continue;
                        realk += 1;
                        double x = Math.Sqrt(v);
                        double y = z / Math.Sqrt(v);
                        sumx += x;
                        sxs += x * x;
                        sys += y * y;
                        sumy += y;
                        sumxy += x * y;
                    }
                }
            }
            double ssx = sxs - sumx * sumx / realk;
            double ssy = sys - sumy * sumy / realk;
            double xy = sumxy - sumx * sumy / realk;
            double slope = xy / ssx;
            double yInt = sumy / realk - slope * (sumx / realk);
            double ssreg = xy * xy / ssx;
            double ssres = ssy - ssreg;
            //  As with the Begg-Mazumdar and Egger tests, nothing is reported below four studies (the help says more than three are needed), nor when
            //  every study has the same precision (no slope) or the line fits every study exactly (no residual variance, so no standard error or P)
            bool notReported = realk < 4 || !(ssx > 1e-12 * sxs) || !(ssres > 1e-12 * sys);
            double bias = notReported ? Constant.MISSING : yInt;
            double ll; double ul;
            double se = 0;
            if (!notReported & ssres >= 0.0)
            {
                double mnsqr = ssres / (realk - 2);
                se = Math.Sqrt(mnsqr * (1.0 / realk + Math.Pow(sumx / realk, 2.0) / ssx));
                MathDbl.civ(realk - 2, out double cit, ncco, out double _);
                ll = bias - se * cit;
                ul = bias + se * cit;
            }
            else
            {
                ll = Constant.MISSING;
                ul = Constant.MISSING;
            }
            double p2;
            if (notReported)
            {
                p2 = Constant.MISSING;
            }
            else
            {
                double t = bias / se;
                p2 = PDF.tvalp(Math.Abs(t), realk - 2);
                if (p2 > 1.0 - p2)
                    p2 = 1.0 - p2;
                p2 = 2.0 * p2;
            }
            outputParameters.AddOutput("a", bias);
            outputParameters.AddOutput("pc_harbord", 100.0 * ncco);
            outputParameters.AddOutput("cl", ll);
            outputParameters.AddOutput("cu", ul);
            outputParameters.AddOutput("p", p2);
        }

        /// <summary>
        /// I-squared, 100 (Q - df) / Q and not below 0, with its confidence limits.  With the exact method off the limits are
        /// test-based: those of the logarithm of H, H^2 being Q / df, from its standard error; they need three studies.  With the
        /// exact method on they are from the non-central chi-square distribution of Q (see the comments below).
        /// </summary>
        /// <param name="host">The preferences: the exact method.</param>
        /// <param name="q">Cochran's Q.</param>
        /// <param name="k">The number of studies.</param>
        /// <param name="cco">The confidence level.</param>
        /// <param name="cit">Its normal deviate.</param>
        /// <param name="i2">On return, I-squared as a percentage; missing with fewer than two studies.</param>
        /// <param name="ll">On return, the lower limit.</param>
        /// <param name="ul">On return, the upper limit.</param>
        public static void IsquareNcc(IPreferences host, double q, int k, double cco, double cit, out double i2, out double ll, out double ul)
        {
            double SElnH;
            int ierr = 0;

            double df = Convert.ToDouble(k - 1);
            double dk = Convert.ToDouble(k);

            i2 = Constant.MISSING;
            ll = Constant.MISSING;
            ul = Constant.MISSING;

            //  A Q that could not be computed (not a number, or infinite from a study with an infinite variance) leaves every figure missing
            if (q < 0 || double.IsNaN(q) || double.IsInfinity(q))
                return;

            // Calculate I-squared even with only one degree of freedom.
            if (df < 1)
                return;
            i2 = Math.Max(0.0, 100.0 * (q - df) / q);
            //  The interval is given at whatever level the analysis uses
            if (cco <= 0.0 || cco >= 1.0)
                return;
            //  With one degree of freedom (two studies) the test-based interval has no standard error; the exact interval is still given
            if (df < 2 && !host.Preferences.MetaExact)
                return;
            double levelci = 1.0 - (1.0 - cco) / 2.0;
            double clevelci = 1.0 - levelci;
            if (df >= 2)
            {
            //  H^2 = Q/df, truncated at 1 where Q < df
            double h2 = Math.Max(1.0, q / df);

            //  CI for H (Higgins & Thompson, 2002 Stat in Med)
            if (q > k)
                SElnH = 0.5 * ((Math.Log(q) - Math.Log(df)) / (Math.Sqrt(2.0 * q) - Math.Sqrt(2.0 * dk - 3.0)));
            else
                SElnH = Math.Sqrt(1.0 / (2.0 * (dk - 2.0)) * (1.0 - 1.0 / (3.0 * Math.Pow(dk - 2.0, 2.0))));

            //  Test-based interval for H, exp(ln H -/+ z SE(ln H)) with H at least 1, converted to I2 = (H^2 - 1)/H^2 (Higgins & Thompson 2002, p 1550);
            //  this is the interval when the exact option is off
            double lbH2 = Math.Pow(Math.Max(1.0, Math.Exp(Math.Log(Math.Sqrt(h2)) - cit * SElnH)), 2.0);
            double ubH2 = Math.Pow(Math.Exp(Math.Log(Math.Sqrt(h2)) + cit * SElnH), 2.0);
            ll = 100.0 * (lbH2 - 1.0) / lbH2;
            ul = 100.0 * (ubH2 - 1.0) / ubH2;
            }

            if (!host.Preferences.MetaExact)
                return;

            //  Iterative solution to seek CI for non-centrality parameter (and then for H and I2)
            //  Q is taken as non-central chi-square with df degrees of freedom and non-centrality parameter lambda, estimated by Q - df. Each limit for lambda
            //  is the value at which the distribution function evaluated at the observed Q equals the tail probability: the lower limit where P(chi2 <= Q) is
            //  1 - alpha/2 and the upper where it is alpha/2 (Hedges & Pigott 2001; Higgins & Thompson 2002, section 4.2). H^2 = (df + lambda)/df, so
            //  I2 = lambda/(df + lambda).
            double nc = Math.Max(0.0, q - df);

            double minLbNc = IsquareNoncentrality(q, df, nc, levelci, ref ierr);
            if (ierr != 0)
                minLbNc = Constant.MISSING;

            double minUbNc = IsquareNoncentrality(q, df, nc, clevelci, ref ierr);
            if (ierr != 0)
                minUbNc = Constant.MISSING;

            // if all goes well - assign the Higgins non-central chi-square interval as the result
            if (minLbNc == Constant.MISSING)
                ll = Constant.MISSING;
            else
                ll = 100.0 * minLbNc / (df + minLbNc);
            if (minUbNc == Constant.MISSING)
                ul = Constant.MISSING;
            else
                ul = 100.0 * minUbNc / (df + minUbNc);
        }

        /// <summary>
        /// The non-centrality parameter at which the non-central chi-square distribution function evaluated at q equals prob; the function falls as the
        /// parameter rises, so the answer is 0 when the central distribution function at q is already at or below prob
        /// </summary>
        /// <param name="q">observed Q</param>
        /// <param name="df">degrees of freedom</param>
        /// <param name="nc">the point estimate of the non-centrality parameter, from which the search interval is built</param>
        /// <param name="prob">the value the distribution function is to take at q</param>
        /// <param name="ierr">0 if a root was found</param>
        private static double IsquareNoncentrality(double q, double df, double nc, double prob, ref int ierr)
        {
            ierr = 0;
            double f0 = ExFortran.nchi2(df, 0.0, q);
            if (f0 == Constant.MISSING)
            {
                ierr = 1;
                return 0;
            }
            if (f0 <= prob)
                return 0;
            //  widen the search interval until the distribution function at its far end has fallen below prob
            double endp = nc + 1000.0;
            for (int i = 0; i < 40; i++)
            {
                double fe = ExFortran.nchi2(df, endp, q);
                if (fe == Constant.MISSING)
                {
                    ierr = 1;
                    return 0;
                }
                if (fe < prob)
                    return IsquareBrentRoot(0, endp, q, df, prob, ref ierr);
                endp *= 2.0;
            }
            ierr = 1;
            return 0;
        }

        /// <summary>
        /// Brent alternative to Pegasus method for root finding - can be faster when high precision demanded:
        /// the non-centrality parameter in [xl, xu] at which the non-central chi-square distribution function evaluated at q equals clev
        /// </summary>
        /// <param name="xl">lower bound of search interval</param>
        /// <param name="xu">upper bound of search interval</param>
        /// <param name="qobs">the value at which the distribution function is evaluated</param>
        /// <param name="df"></param>
        /// <param name="clev"></param>
        /// <param name="ierr"></param>
        /// <returns></returns>
        private static double IsquareBrentRoot(double xl, double xu, double qobs, double df, double clev, ref int ierr)
        {
            double d = 0;
            const double tolerance = 0.000001;
            const int maxIter = 300;

            double e = 0.0;
            double a = xl;
            double b = xu;

            double fa = ExFortran.nchi2(df, a, qobs) - clev;
            if (fa == Constant.MISSING)
            {
                ierr = 1;
                return 0;
            }
            double fb = ExFortran.nchi2(df, b, qobs) - clev;
            if (fb == Constant.MISSING)
            {
                ierr = 1;
                return 0;
            }

            double c = b;
            double fc = fb;

            ierr = 0;
            int nIter = 0;

            do
            {
                nIter++;
                if (nIter > maxIter)
                {
                    ierr = 1;
                    break;
                }

                if (fb > 0.0 && fc > 0.0 || fb < 0.0 && fc < 0.0)
                {
                    c = a;
                    fc = fa;
                    d = b - a;
                    e = d;
                }

                if (Math.Abs(fc) < Math.Abs(fb))
                {
                    a = b;
                    b = c;
                    c = a;
                    fa = fb;
                    fb = fc;
                    fc = fa;
                }

                double tol1 = 2.0 * Constant.EPSILON * Math.Abs(b) + 0.5 * tolerance;

                double xm = 0.5 * (c - b);

                if (Math.Abs(xm) <= tol1 | fb == 0.0)
                    break;

                if (Math.Abs(e) >= tol1 & Math.Abs(fa) > Math.Abs(fb))
                {
                    double s = fb / fa;
                    double p;
                    double q;
                    if (a == c)
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

                    if (p > 0.0)
                    {
                        q = -q;
                    }

                    p = Math.Abs(p);
                    double xmin = Math.Abs(e * q);
                    double tmp = 3.0 * xm * q - Math.Abs(tol1 * q);

                    if (xmin < tmp)
                    {
                        xmin = tmp;
                    }

                    if (2.0 * p < xmin)
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

                if (Math.Abs(d) > tol1)
                {
                    b += d;
                }
                else
                {
                    if (xm < 0.0)
                    {
                        b -= Math.Abs(tol1);
                    }
                    else
                    {
                        b += Math.Abs(tol1);
                    }
                }

                fb = ExFortran.nchi2(df, b, qobs) - clev;
                if (fb == Constant.MISSING)
                {
                    ierr = 1;
                    return 0;
                }
            }
            while (true);

            return b;
        }

        /// <summary>
        /// The variance that confidence limits imply if they are an estimate plus and minus z standard errors: the square of
        /// (upper - lower) / (2 z), on the scale of the logarithm for a ratio.
        /// </summary>
        public static double VarianceFromCI(double ll, double ul, double cit, bool logtransform)
        {
            if (cit <= 0)
                return Constant.MISSING;
            return logtransform
                ? Math.Pow((Math.Log(ul) - Math.Log(ll)) / 2 / cit, 2)
                : Math.Pow((ul - ll) / 2 / cit, 2);
        }

        /// <summary>
        /// A copy of a list: the charts are drawn after the report is made, and must not see the changes made to the list meanwhile.
        /// </summary>
        private static T[] ShallowCopy<T>(T[] original)
        {
            T[] copy = new T[original.Length];
            Array.Copy(original, copy, original.Length);
            return copy;
        }
    }
}
