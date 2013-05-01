using StatsDirect.Charting;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.Templates;
using StatsDirect.Utilities;

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace StatsDirect.Builtins
{
    public class Meta
    {
        public static StepResult RptPetoMeta(ITemplateHost host, ParameterBag parameters)
        {
            double rmh = 0; double isq; double llisq; double ulisq;
            double z; double poru; double porl; double por;
            double cit; double p;
            bool stratlab; int i;
            int fault;

            double cco = parameters["gamma"].AsDouble;
            if (cco > 0)
            {
                p = (1.0 - cco) / 2.0;
                cit = PDF.gauinv(1.0 - p, out fault);
            }
            else
            {
                cco = 0.95;
                cit = PDF.gauinv(0.975, out fault);
            }

            DataFrame snFrame = parameters["sn"].AsDataFrame;
            DoubleVariable snVariable = snFrame.Variables[0].AsDoubleVariable;
            int k = snVariable.Length;
            double[] sn = new double[k + 1 /* for VB to C# conversion */];
            for (i = 1; i <= k; i++)
            {
                sn[i] = snVariable.Data[i - 1];
            }

            DataFrame srFrame = parameters["sr"].AsDataFrame;
            DoubleVariable srVariable = srFrame.Variables[0].AsDoubleVariable;
            double[] sr = new double[k + 1 /* for VB to C# conversion */];
            for (i = 1; i <= k; i++)
            {
                sr[i] = srVariable.Data[i - 1];
            }

            DataFrame xnFrame = parameters["xn"].AsDataFrame;
            DoubleVariable xnVariable = xnFrame.Variables[0].AsDoubleVariable;
            double[] xn = new double[k + 1 /* for VB to C# conversion */];
            for (i = 1; i <= k; i++)
            {
                xn[i] = xnVariable.Data[i - 1];
            }

            DataFrame xrFrame = parameters["xr"].AsDataFrame;
            DoubleVariable xrVariable = xrFrame.Variables[0].AsDoubleVariable;
            double[] xr = new double[k + 1 /* for VB to C# conversion */];
            for (i = 1; i <= k; i++)
            {
                xr[i] = xrVariable.Data[i - 1];
            }

            string[] title = new string[k + 1 /* for VB to C# conversion */ ];
            if (parameters.ContainsKey("strata") && parameters["strata"].Data != null)
            {
                stratlab = true;
                DataFrame strataFrame = parameters["strata"].AsDataFrame;
                StringVariable strataVariable = strataFrame.Variables[0].AsStringVariable;
                for (i = 1; i <= k; i++)
                {
                    string buf = strataVariable.Data[i - 1].Trim();
                    if (buf.Length > 0)
                    {
                        if (buf.Length > 50)
                        {
                            buf = buf.Substring(0, 50);
                        }
                        title[i] = buf;
                    }
                    else
                    {
                        title[i] = "stratum " + i.ToString();
                    }
                }
            }
            else
            {
                stratlab = false;
                for (i = 1; i <= k; i++)
                {
                    title[i] = "stratum " + i.ToString();
                }
            }

            double[,] o = new double[k + 1 /* for VB to C# conversion */, 4 + 1 /* for VB to C# conversion */];
            double[] oe = new double[k + 1 /* for VB to C# conversion */];
            double[] odr = new double[k + 1 /* for VB to C# conversion */];
            double[] odrl = new double[k + 1 /* for VB to C# conversion */ ];
            double[] odru = new double[k + 1 /* for VB to C# conversion */ ];
            double[] odw = new double[k + 1 /* for VB to C# conversion */];
            double[] odz = new double[k + 1 /* for VB to C# conversion */];
            double[] odx = new double[k + 1 /* for VB to C# conversion */];
            bool[] lerr = new bool[k + 1 /* for VB to C# conversion */];
            bool[] uerr = new bool[k + 1 /* for VB to C# conversion */];
            bool[] cced = new bool[k + 1 /* for VB to C# conversion */ ];
            double[] standardizedEffect = new double[k + 1];
            double[] se = new double[k + 1];
            for (i = 1; i <= k; i++)
            {
                cced[i] = false;
                o[i, 1] = Math.Abs(sr[i]);
                o[i, 3] = Math.Abs(sn[i] - sr[i]);
                if (sr[i] < 0 | sn[i] < 0 | sn[i] < sr[i])
                {
                    throw new InvalidDataException();
                }
                o[i, 2] = Math.Abs(xr[i]);
                o[i, 4] = Math.Abs(xn[i] - xr[i]);
                if (xr[i] < 0 | xn[i] < 0 | xn[i] < xr[i])
                {
                    throw new InvalidDataException();
                }
            }

            // see Fleiss paper
            double sumoe = 0.0;
            double sumv = 0.0;
            for (i = 1; i <= k; i++)
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
                    sumoe = sumoe + oe[i];
                    double v = (a + b) * (c + d) * (a + c) * (b + d) / (n * n * (n - 1));
                    sumv = sumv + v;
                    odw[i] = v;
                    odx[i] = n;
                    if (v > 0)
                    {
                        se[i] = v; // TODO: Correct?
                        standardizedEffect[i] = oe[i]/v;
                        odr[i] = Math.Exp(oe[i] / v);
                        odz[i] = oe[i] / Math.Sqrt(v);
                        odrl[i] = Math.Exp((oe[i] - cit * Math.Sqrt(v)) / v);
                        odru[i] = Math.Exp((oe[i] + cit * Math.Sqrt(v)) / v);
                    }
                    else
                    {
                        odr[i] = Constant.MISSING;
                        odrl[i] = Constant.MISSING;
                        odru[i] = Constant.MISSING;
                        odz[i] = Constant.MISSING;
                    }
                }
                else
                {
                    odr[i] = Constant.MISSING;
                    odw[i] = Constant.MISSING;
                    odrl[i] = Constant.MISSING;
                    odru[i] = Constant.MISSING;
                    odz[i] = Constant.MISSING;
                }
            }

            // pooled peto odds ratio
            if (sumv > 0.0)
            {
                por = Math.Exp(sumoe / sumv);
                porl = Math.Exp((sumoe - cit * Math.Sqrt(sumv)) / sumv);
                poru = Math.Exp((sumoe + cit * Math.Sqrt(sumv)) / sumv);
                z = sumoe / Math.Sqrt(sumv);
            }
            else
            {
                throw new InvalidDataException();
            }

            // combinability
            double qc = 0.0;
            int realk = 0;
            for (i = 1; i <= k; i++)
            {
                if (IncludeTable(o, i))
                {
                    realk++;
                    double lori = oe[i] / odw[i];
                    qc += Math.Pow((lori - Math.Log(por)), 2.0) * odw[i];
                }
            }

            // RTF_LoadTemplate("peto.rtf")
            ParameterBag outputParameters = new ParameterBag();
            IList<ParameterBag> inputsList = new List<ParameterBag>();
            outputParameters.AddOutput("*inputs", inputsList);
            for (i = 1; i <= k; i++)
            {
                ParameterBag inputsParameters = new ParameterBag();
                inputsList.Add(inputsParameters);
                inputsParameters.AddOutput("st", i.ToString());
                inputsParameters.AddOutput("a", o[i, 1].ToString());
                inputsParameters.AddOutput("b", o[i, 2].ToString());
                inputsParameters.AddOutput("c", o[i, 3].ToString());
                inputsParameters.AddOutput("d", o[i, 4].ToString());
                inputsParameters.AddOutput("lb", GetMetaLabel(host, o, i, stratlab, cced, title));
            }

            outputParameters.AddOutput("pc", Formatting.XRound(cco * 100, 2));

            IList<ParameterBag> oddsList = new List<ParameterBag>();
            outputParameters.AddOutput("*odds", oddsList);
            for (i = 1; i <= k; i++)
            {
                ParameterBag oddsParameters = new ParameterBag();
                oddsList.Add(oddsParameters);
                oddsParameters.AddOutput("st", i.ToString());
                oddsParameters.AddOutput("oe", host.RoundU(oe[i]));
                oddsParameters.AddOutput("or", host.RoundU(odr[i]));
                oddsParameters.AddOutput("lci", host.RoundU(odrl[i]));
                oddsParameters.AddOutput("uci", host.RoundU(odru[i]));
                oddsParameters.AddOutput("wt", host.RoundU(100 * odw[i] / Formatting.dsum(odw, 1)));
                oddsParameters.AddOutput("lb", GetMetaLabel(host, o, i, stratlab, cced, title));
                oddsParameters.AddOutput("standardized_effect", host.RoundU(standardizedEffect[i]));
                oddsParameters.AddOutput("se", host.RoundU(se[i]));
            }

            IList<ParameterBag> zList = new List<ParameterBag>();
            outputParameters.AddOutput("*z", zList);
            for (i = 1; i <= k; i++)
            {
                ParameterBag zParameters = new ParameterBag();
                zList.Add(zParameters);
                zParameters.AddOutput("st", i.ToString());
                zParameters.AddOutput("v", host.RoundU(odw[i]));
                zParameters.AddOutput("z", host.RoundU(odz[i]));
                if (odz[i] != Constant.MISSING)
                {
                    p = 1.0 - PDF.alnorm(odz[i]);
                    if (p > 1.0 - p)
                    {
                        p = 1.0 - p;
                    }
                    zParameters.AddOutput("p", host.pval(2.0 * p));
                }
                else
                {
                    zParameters.AddOutput("p", Formatting.ASTERISK);
                }
                zParameters.AddOutput("lb", GetMetaLabel(host, o, i, stratlab, cced, title));
            }

            outputParameters.AddOutput("por", host.RoundU(por));
            outputParameters.AddOutput("from", host.RoundU(porl));
            outputParameters.AddOutput("to", host.RoundU(poru));

            outputParameters.AddOutput("z", host.RoundU(z));
            p = 1.0 - PDF.alnorm(z);
            if (p > 1.0 - p)
            {
                p = 1.0 - p;
            }
            outputParameters.AddOutput("p_z", host.pval(2.0 * p));

            outputParameters.AddOutput("qc", host.RoundU(qc));
            outputParameters.AddOutput("df", (realk - 1).ToString());
            outputParameters.AddOutput("xp", host.pval(PDF.chivalp(qc, Convert.ToDouble(realk - 1))));
            IsquareNcc(host, qc, realk, cco, cit, out isq, out llisq, out ulisq);
            outputParameters.AddOutput("isq", Formatting.XRound(isq, 1));
            outputParameters.AddOutput("pc1", Formatting.XRound(cco * 100, 1));
            outputParameters.AddOutput("llisq", Formatting.XRound(llisq, 1));
            outputParameters.AddOutput("ulisq", Formatting.XRound(ulisq, 1));

            IList<ParameterBag> eggerList = new List<ParameterBag>();
            outputParameters.AddOutput("*egger", eggerList);
            ParameterBag eggerParameters = new ParameterBag();
            eggerList.Add(eggerParameters);
            Metabias(host, eggerParameters, odr, odrl, odru, k, ref cco, Transformation.Log);

            IList<ParameterBag> horboldList = new List<ParameterBag>();
            outputParameters.AddOutput("*horbold", horboldList);
            ParameterBag horboldParameters = new ParameterBag();
            horboldList.Add(horboldParameters);
            ModMetabias(host, horboldParameters, o, k, cco, 1);

            IList<ParameterBag> chartList = new List<ParameterBag>();
            outputParameters.AddOutput("*chart", chartList);
            ParameterBag chartParameters;

            if (k > 3)
            {
                using (ChartRenderer ch = new ChartRenderer(ChartDefinition.Empty()))
                {
                    string rtf = ch.PlotBiasMAAndReturnRtf(host, odr, odx, odw, k, "Peto odds ratio", odrl, odru, cco, cit, por, Transformation.Log, false);
                    chartParameters = new ParameterBag();
                    chartList.Add(chartParameters);
                    chartParameters.AddOutput("chart", rtf);
                }
            }

            using (ChartRenderer ch = new ChartRenderer(ChartDefinition.Empty()))
            {
                string rtf = ch.PlotLAbbeAndReturnRtf(host, k, o, rmh);
                chartParameters = new ParameterBag();
                chartList.Add(chartParameters);
                chartParameters.AddOutput("chart", rtf);
            }

            using (ChartRenderer ch = new ChartRenderer(ChartDefinition.Empty()))
            {
                bool scrap;
                string rtf = ch.PlotMHAndReturnRtf(host, k, o, odw, title, por, porl, poru, cco, odr, odrl, odru, lerr, uerr, "Peto odds ratio plot", 1, "Peto odds ratio", out scrap, "Pooled Peto odds ratio");
                chartParameters = new ParameterBag();
                chartList.Add(chartParameters);
                chartParameters.AddOutput("chart", rtf);
            }

            if (k > 2)
            {
                using (ChartRenderer ch = new ChartRenderer(ChartDefinition.Empty()))
                {
                    string rtf = ch.PlotBiasMAAndReturnRtf(host, odw, oe, oe, k, "Peto weights", odrl, odru, cco, cit, por, Transformation.None, true);
                    chartParameters = new ParameterBag();
                    chartList.Add(chartParameters);
                    chartParameters.AddOutput("chart", rtf);
                }
            }

            return new StepResult(StepSuccess.Success, outputParameters);
        }


        public static void Metabias(ITemplateHost host, ParameterBag outputParameters, double[] t, double[] tl, double[] tu, int n, ref double cco, Transformation xform)
        {
            string tau = null; string p2 = null;
            int i;
            int irank = 0; int nrmiss = 0; int ifault = 0;
            double[] seb = null; double[] bd = null;
            double rdf = 0; double rss = 0;
            double a;
            double cit;
            double cla; double cua;
            double prob;
            int fault;

            if (cco > 0)
            {
                cit = PDF.gauinv(1.0 - ((1.0 - cco) / 2.0), out fault);
            }
            else
            {
                cco = 0.95;
                cit = PDF.gauinv(0.975, out fault);
            }

            // setup basic variables
            // bool DoC = true; 
            int P = 2;
            int nx = 0;
            for (i = 1; i <= n; i++)
            {
                if (t[i] != Constant.MISSING && tl[i] != Constant.MISSING && tu[i] != Constant.MISSING && !(double.IsInfinity(tl[i])) && !(double.IsInfinity(tu[i])))
                {
                    nx = nx + 1;
                }
            }
            if (nx < 4)
            {
                ifault = -3;
            }
            else
            {
                double[] y = new double[nx + 1 /* for VB to C# conversion */ ];
                double[,] x = new double[nx + 1 /* for VB to C# conversion */, P + 1 /* for VB to C# conversion */];
                double[] wt = new double[nx + 1 /* for VB to C# conversion */ ];
                double[] var = new double[nx + 1 /* for VB to C# conversion */];
                double[] tt = new double[nx + 1 /* for VB to C# conversion */];
                double[] ts = new double[nx + 1 /* for VB to C# conversion */ ];
                nx = 0;
                double se;
                switch (xform)
                {
                    case Transformation.Log:
                        for (i = 1; i <= n; i++)
                        {
                            if (t[i] != Constant.MISSING && tl[i] != Constant.MISSING && tu[i] != Constant.MISSING && !(double.IsInfinity(tl[i])) && !(double.IsInfinity(tu[i])) && tu[i] - tl[i] != 0.0 && t[i] > 0.0 && tl[i] > 0.0 && tu[i] > 0.0)
                            {
                                se = ((Math.Log(tu[i]) - Math.Log(tl[i])) / 2) / cit;
                                if (se != 0.0)
                                {
                                    nx = nx + 1;
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
                        for (i = 1; i <= n; i++)
                        {
                            if (t[i] != Constant.MISSING & tl[i] != Constant.MISSING & tu[i] != Constant.MISSING & !(double.IsInfinity(tl[i])) & !(double.IsInfinity(tu[i])) & tu[i] - tl[i] != 0.0 & t[i] > 0.0 & tl[i] > 0.0 & tu[i] > 0.0)
                            {
                                se = ((MathDbl.rtoz(tu[i]) - MathDbl.rtoz(tl[i])) / 2) / cit;
                                if (se != 0.0)
                                {
                                    nx = nx + 1;
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
                        for (i = 1; i <= n; i++)
                        {
                            if (t[i] != Constant.MISSING & tl[i] != Constant.MISSING & tu[i] != Constant.MISSING & !(double.IsInfinity(tl[i])) & !(double.IsInfinity(tu[i])))
                            {
                                se = ((tu[i] - tl[i]) / 2) / cit;
                                if (se != 0)
                                {
                                    nx = nx + 1;
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
                if (ifault != -3)
                {
                    double sumwt = 0.0;
                    double sumwtt = 0.0;
                    for (i = 1; i <= nx; i++)
                    {
                        double wx = 1.0 / var[i];
                        sumwt = sumwt + wx;
                        sumwtt = sumwtt + tt[i] * wx;
                    }
                    for (i = 1; i <= nx; i++)
                    {
                        double vt = var[i] - 1.0 / sumwt;
                        ts[i] = (tt[i] - sumwtt / sumwt) / Math.Sqrt(vt);
                    }
                    Anova.XAgreeKendall(host, ref ts, ref var, 1, ref nx, out tau, out p2);
                }

                // setup regression call
                seb = new double[P + 1 /* for VB to C# conversion */];
                bd = new double[P * P + 1 /* for VB to C# conversion */];
                int incep = 1;
                int indep = 1;
                int iwt = 1;
                double[,] xx = new double[nx + 1 /* for VB to C# conversion */, indep + 1 + iwt + 1 /* for VB to C# conversion */];
                double[,] r = new double[P + 1 /* for VB to C# conversion */, P + 1 /* for VB to C# conversion */];
                double[] D = new double[P + 1 /* for VB to C# conversion */ ];
                double[] xmin = new double[P + 1 /* for VB to C# conversion */ ];
                double[] XMax = new double[P + 1 /* for VB to C# conversion */ ];
                double[] WK = new double[2 * (P + 1) + 1 /* for VB to C# conversion */];
                int[] idum = new int[1 + 1 /* for VB to C# conversion */];
                for (i = 1; i <= nx; i++)
                {
                    int j;
                    for (j = 1 + incep; j <= indep + incep; j++)
                    {
                        xx[i, j - incep] = x[i, j];
                    }
                    xx[i, indep + 1] = wt[i];
                    xx[i, indep + 2] = y[i];
                }
                int iwtcol = indep + 1;
                Regress1.glsqr(0, incep, 0, nx, indep + iwt + 1, xx, -indep, idum, -1, idum, 0, iwtcol, bd, r, D, ref irank, ref rdf, ref rss, ref nrmiss, xmin, XMax, WK, ref ifault);
                if (ifault == 0)
                {
                    double[,] covb = new double[P + 1 /* for VB to C# conversion */, P + 1 /* for VB to C# conversion */];
                    Regress1.rcovarb(P, r, 1.0, covb, ref ifault);
                    double rms = rss / rdf;
                    Regress1.rcovarb(P, r, rms, covb, ref ifault);
                    for (i = 1; i <= P; i++)
                    {
                        seb[i] = Math.Sqrt(covb[i, i]);
                    }
                }
            }

            if (ifault == 0)
            {
                double scrap;
                double citt;
                MathDbl.civ(nx - P, out citt, cco, out scrap);
                Debug.Assert(null != seb);
                double tz = Math.Abs(bd[1] / seb[1]);
                prob = PDF.tvalp(tz, Convert.ToDouble(nx - P));
                if (prob > 1.0 - prob)
                {
                    prob = 1.0 - prob;
                }
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

            if (ifault == -3)
            {
                tau = "<too few strata>";
                p2 = Formatting.ASTERISK;
            }
            outputParameters.AddOutput("tau", tau);
            outputParameters.AddOutput("p2", p2);

            if (ifault == -3)
            {
                outputParameters.AddOutput("a", tau);
                cla = Constant.MISSING;
                cua = Constant.MISSING;
                prob = Constant.MISSING;
            }
            else
            {
                outputParameters.AddOutput("a", host.RoundU(a));
            }

            outputParameters.AddOutput("pc_egger", Formatting.XRound(100.0 * cco, 2));
            outputParameters.AddOutput("cl", host.RoundU(cla));
            outputParameters.AddOutput("cu", host.RoundU(cua));
            outputParameters.AddOutput("p", host.pval(prob));

        }


        public static StepResult RptRiskDifferenceMeta(ITemplateHost host, ParameterBag parameters)
        {
            double dsul = 0; double dsll = 0;
            double dsx2 = 0; double qc = 0; double sk = 0; double x2Rmh = 0; double ul = 0; double ll = 0; double rmh = 0; double cit;
            double dsrd = 0;
            double isq; double llisq; double ulisq; double tausq = 0;
            int i;
            int ierr; bool stratlab;

            double cco = parameters["gamma"].AsDouble;
            if (cco > 0)
            {
                double p = (1.0 - cco) / 2.0;
                int scrap;
                cit = PDF.gauinv(1.0 - p, out scrap);
            }
            else
            {
                cco = 0.95;
                int scrap;
                cit = PDF.gauinv(0.975, out scrap);
            }

            DataFrame snFrame = parameters["sn"].AsDataFrame;
            DoubleVariable snVariable = snFrame.Variables[0].AsDoubleVariable;
            int k = snVariable.Length;
            double[] sn = new double[k + 1 /* for VB to C# conversion */];
            for (i = 1; i <= k; i++)
            {
                sn[i] = snVariable.Data[i - 1];
            }

            DataFrame srFrame = parameters["sr"].AsDataFrame;
            DoubleVariable srVariable = srFrame.Variables[0].AsDoubleVariable;
            double[] sr = new double[k + 1 /* for VB to C# conversion */ ];
            for (i = 1; i <= k; i++)
            {
                sr[i] = srVariable.Data[i - 1];
            }

            DataFrame xnFrame = parameters["xn"].AsDataFrame;
            DoubleVariable xnVariable = xnFrame.Variables[0].AsDoubleVariable;
            double[] xn = new double[k + 1 /* for VB to C# conversion */ ];
            for (i = 1; i <= k; i++)
            {
                xn[i] = xnVariable.Data[i - 1];
            }

            DataFrame xrFrame = parameters["xr"].AsDataFrame;
            DoubleVariable xrVariable = xrFrame.Variables[0].AsDoubleVariable;
            double[] xr = new double[k + 1 /* for VB to C# conversion */];
            for (i = 1; i <= k; i++)
            {
                xr[i] = xrVariable.Data[i - 1];
            }

            string[] title = new string[k + 1 /* for VB to C# conversion */ ];
            if (parameters.ContainsKey("strata") && parameters["strata"].Data != null)
            {
                stratlab = true;
                DataFrame strataFrame = parameters["strata"].AsDataFrame;
                StringVariable strataVariable = strataFrame.Variables[0].AsStringVariable;
                for (i = 1; i <= k; i++)
                {
                    string buf = strataVariable.Data[i - 1].Trim();
                    if (buf.Length > 0)
                    {
                        if (buf.Length > 50)
                        {
                            buf = buf.Substring(0, 50);
                        }
                        title[i] = buf;
                    }
                    else
                    {
                        title[i] = "stratum " + i.ToString();
                    }
                }
            }
            else
            {
                stratlab = false;
                for (i = 1; i <= k; i++)
                {
                    title[i] = "stratum " + i.ToString();
                }
            }

            double[,] o = new double[k + 1 /* for VB to C# conversion */, 4 + 1 /* for VB to C# conversion */];
            double[] rkr = new double[k + 1 /* for VB to C# conversion */];
            double[] rkw = new double[k + 1 /* for VB to C# conversion */ ];
            double[] dsw = new double[k + 1 /* for VB to C# conversion */ ];
            double[] rkrl = new double[k + 1 /* for VB to C# conversion */ ];
            double[] rkru = new double[k + 1 /* for VB to C# conversion */ ];
            double[] rkx = new double[k + 1 /* for VB to C# conversion */ ];
            bool[] lerr = new bool[k + 1 /* for VB to C# conversion */ ];
            bool[] uerr = new bool[k + 1 /* for VB to C# conversion */ ];
            bool[] cced = new bool[k + 1 /* for VB to C# conversion */ ];
            double[] standardizedEffect = new double[k+1];
            double[] se = new double[k + 1];
            for (i = 1; i <= k; i++)
            {
                o[i, 1] = Math.Abs(sr[i]);
                o[i, 3] = Math.Abs(sn[i] - sr[i]);
                if (sr[i] < 0 | sn[i] < 0 | sn[i] < sr[i])
                {
                    throw new InvalidDataException();
                }
                o[i, 2] = Math.Abs(xr[i]);
                o[i, 4] = Math.Abs(xn[i] - xr[i]);
                if (xr[i] < 0 | xn[i] < 0 | xn[i] < xr[i])
                {
                    throw new InvalidDataException();
                }
            }

            Riskdifma(host, k, o, ref rmh, ref ll, ref ul, ref x2Rmh, ref sk, ref cit, ref cco, ref rkr, ref rkw, ref dsw, ref rkrl, ref rkru, ref rkx, ref lerr, ref uerr, ref qc, ref dsrd, ref dsx2, ref dsll, ref dsul, ref tausq, ref cced, standardizedEffect, se, out ierr);
            if (ierr == -1)
            {
                throw new InvalidDataException();
            }

            //  RTF_LoadTemplate("rdmeta.rtf")
            ParameterBag outputParameters = new ParameterBag();

            IList<ParameterBag> inputsList = new List<ParameterBag>();
            outputParameters.AddOutput("*inputs", inputsList);
            for (i = 1; i <= k; i++)
            {
                ParameterBag inputsParameters = new ParameterBag();
                inputsList.Add(inputsParameters);
                inputsParameters.AddOutput("st", i.ToString());
                inputsParameters.AddOutput("a", o[i, 1].ToString());
                inputsParameters.AddOutput("b", o[i, 2].ToString());
                inputsParameters.AddOutput("c", o[i, 3].ToString());
                inputsParameters.AddOutput("d", o[i, 4].ToString());
                string tmp = stratlab ? title[i] : "";
                if (cced[i])
                {
                    tmp = tmp + " [CC = ";
                    tmp = host.Preferences.MetaCC == -9.0
                              ? tmp + "treatment arm"
                              : tmp + host.Preferences.MetaCC.ToString();
                    tmp = tmp + "]";
                }
                inputsParameters.AddOutput("lb", tmp);
            }

            outputParameters.AddOutput("pc", Formatting.XRound(cco * 100, 2));
            outputParameters.AddOutput("method", host.Preferences.MetaExact ? "Miettinen" : "approximate");
            IList<ParameterBag> differencesList = new List<ParameterBag>();
            outputParameters.AddOutput("*differences", differencesList);
            for (i = 1; i <= k; i++)
            {
                ParameterBag differencesParameters = new ParameterBag();
                differencesList.Add(differencesParameters);
                differencesParameters.AddOutput("st", i.ToString());
                differencesParameters.AddOutput("rd", host.RoundU(rkr[i]));
                differencesParameters.AddOutput("lci", host.RoundU(rkrl[i]));
                differencesParameters.AddOutput("uci", host.RoundU(rkru[i]));
                differencesParameters.AddOutput("wt", host.RoundU(100 * rkw[i] / Formatting.dsum(rkw, 1)));
                differencesParameters.AddOutput("dwt", host.RoundU(100 * dsw[i] / Formatting.dsum(dsw, 1)));
                differencesParameters.AddOutput("lb", stratlab ? title[i] : "");
                differencesParameters.AddOutput("standardized_effect", host.RoundU(standardizedEffect[i]));
                differencesParameters.AddOutput("se", host.RoundU(se[i]));
                // double a = o[ i, 1 ]; 
                // double b = o[ i, 2 ]; 
                // double C = o[ i, 3 ]; 
                // double D = o[ i, 4 ]; 
                // double N = a + b + C + D; 
            }

            outputParameters.AddOutput("rmh", host.RoundU(rmh));
            outputParameters.AddOutput("from", host.RoundU(ll));
            outputParameters.AddOutput("to", host.RoundU(ul));

            outputParameters.AddOutput("x2", host.RoundU(x2Rmh));
            outputParameters.AddOutput("df", 1.ToString());
            outputParameters.AddOutput("xp", host.pval(PDF.chivalp(x2Rmh, 1.0)));

            outputParameters.AddOutput("qc", host.RoundU(qc));
            outputParameters.AddOutput("df_cochran", (k - 1).ToString());
            outputParameters.AddOutput("xp_cochran", host.pval(PDF.chivalp(qc, Convert.ToDouble(k - 1))));
            outputParameters.AddOutput("tausq", host.RoundU(tausq));
            IsquareNcc(host, qc, k, cco, cit, out isq, out llisq, out ulisq);
            outputParameters.AddOutput("isq", Formatting.XRound(isq, 1));
            outputParameters.AddOutput("pc1", Formatting.XRound(cco * 100, 1));
            outputParameters.AddOutput("llisq", Formatting.XRound(llisq, 1));
            outputParameters.AddOutput("ulisq", Formatting.XRound(ulisq, 1));

            outputParameters.AddOutput("dsrd", host.RoundU(dsrd));
            outputParameters.AddOutput("dsll", host.RoundU(dsll));
            outputParameters.AddOutput("dsul", host.RoundU(dsul));
            outputParameters.AddOutput("dsx2", host.RoundU(dsx2));
            outputParameters.AddOutput("df_ds", 1.ToString());
            outputParameters.AddOutput("xp_ds", host.pval(PDF.chivalp(dsx2, 1.0)));

            IList<ParameterBag> eggerList = new List<ParameterBag>();
            outputParameters.AddOutput("*egger", eggerList);
            ParameterBag eggerParameters = new ParameterBag();
            eggerList.Add(eggerParameters);
            Metabias(host, eggerParameters, rkr, rkrl, rkru, k, ref cco, Transformation.None);

            IList<ParameterBag> chartList = new List<ParameterBag>();
            outputParameters.AddOutput("*chart", chartList);
            ParameterBag chartParameters;

            //  Ensure no accidental (0,0) plots
            rkr[0] = Constant.MISSING;
            rkx[0] = Constant.MISSING;
            rkw[0] = Constant.MISSING;

            if (k > 3)
            {
                using (ChartRenderer ch = new ChartRenderer(ChartDefinition.Empty()))
                {
                    string rtf = ch.PlotBiasMAAndReturnRtf(host, rkr, rkx, rkw, k, "Risk difference", rkrl, rkru, cco, cit, rmh, Transformation.None, false);
                    chartParameters = new ParameterBag();
                    chartList.Add(chartParameters);
                    chartParameters.AddOutput("chart", rtf);
                }
            }

            bool bfault;
            using (ChartRenderer ch = new ChartRenderer(ChartDefinition.Empty()))
            {
                string rtf = ch.PlotMHRDAndReturnRtf(host, k, o, rkw, title, rmh, ll, ul, cco, rkr, rkrl, rkru, lerr, uerr, "Risk difference meta-analysis plot [fixed effects]", 1, "risk difference", out bfault);
                chartParameters = new ParameterBag();
                chartList.Add(chartParameters);
                chartParameters.AddOutput("chart", rtf);
            }

            using (ChartRenderer ch = new ChartRenderer(ChartDefinition.Empty()))
            {
                string rtf = ch.PlotMHRDAndReturnRtf(host, k, o, dsw, title, dsrd, dsll, dsul, cco, rkr, rkrl, rkru, lerr, uerr, "Risk difference meta-analysis plot [random effects]", 1, "risk difference", out bfault);
                chartParameters = new ParameterBag();
                chartList.Add(chartParameters);
                chartParameters.AddOutput("chart", rtf);
            }

            return new StepResult(StepSuccess.Success, outputParameters);
        }


        public static StepResult RptRelativeRiskMeta(ITemplateHost host, ParameterBag parameters)
        {
            double dsul = 0; double dsll = 0;
            double dsx2 = 0; double dsrr = 0; double qc = 0; double sk = 0; double x2Rmh = 0; double ul = 0; double ll = 0; double rmh = 0; double cit;
            double isq; double llisq; double ulisq; double tausq = 0;
            int i;
            int realk; int ierr; bool stratlab;
            int ifault;

            double cco = parameters["gamma"].AsDouble;
            if (cco > 0)
            {
                double p = (1.0 - cco) / 2.0;
                cit = PDF.gauinv(1.0 - p, out ifault);
            }
            else
            {
                cco = 0.95;
                cit = PDF.gauinv(0.975, out ifault);
            }

            DataFrame snFrame = parameters["sn"].AsDataFrame;
            DoubleVariable snVariable = snFrame.Variables[0].AsDoubleVariable;
            int k = snVariable.Length;
            double[] sn = new double[k + 1 /* for VB to C# conversion */ ];
            for (i = 1; i <= k; i++)
            {
                sn[i] = snVariable.Data[i - 1];
            }

            DataFrame srFrame = parameters["sr"].AsDataFrame;
            DoubleVariable srVariable = srFrame.Variables[0].AsDoubleVariable;
            double[] sr = new double[k + 1 /* for VB to C# conversion */ ];
            for (i = 1; i <= k; i++)
            {
                sr[i] = srVariable.Data[i - 1];
            }

            DataFrame xnFrame = parameters["xn"].AsDataFrame;
            DoubleVariable xnVariable = xnFrame.Variables[0].AsDoubleVariable;
            double[] xn = new double[k + 1 /* for VB to C# conversion */];
            for (i = 1; i <= k; i++)
            {
                xn[i] = xnVariable.Data[i - 1];
            }

            DataFrame xrFrame = parameters["xr"].AsDataFrame;
            DoubleVariable xrVariable = xrFrame.Variables[0].AsDoubleVariable;
            double[] xr = new double[k + 1 /* for VB to C# conversion */];
            for (i = 1; i <= k; i++)
            {
                xr[i] = xrVariable.Data[i - 1];
            }

            string[] title = new string[k + 1 /* for VB to C# conversion */];
            if (parameters.ContainsKey("strata") && parameters["strata"].Data != null)
            {
                stratlab = true;
                DataFrame strataFrame = parameters["strata"].AsDataFrame;
                StringVariable strataVariable = strataFrame.Variables[0].AsStringVariable;
                for (i = 1; i <= k; i++)
                {
                    string buf = strataVariable.Data[i - 1].Trim();
                    if (buf.Length > 0)
                    {
                        if (buf.Length > 50)
                        {
                            buf = buf.Substring(0, 50);
                        }
                        title[i] = buf;
                    }
                    else
                    {
                        title[i] = "stratum " + i.ToString();
                    }
                }
            }
            else
            {
                stratlab = false;
                for (i = 1; i <= k; i++)
                {
                    title[i] = "stratum " + i.ToString();
                }
            }

            double[,] o = new double[k + 1 /* for VB to C# conversion */, 4 + 1 /* for VB to C# conversion */];
            double[] rkr = new double[k + 1 /* for VB to C# conversion */];
            double[] rkw = new double[k + 1 /* for VB to C# conversion */];
            double[] dsw = new double[k + 1 /* for VB to C# conversion */];
            double[] rkrl = new double[k + 1 /* for VB to C# conversion */ ];
            double[] rkru = new double[k + 1 /* for VB to C# conversion */];
            double[] rkx = new double[k + 1 /* for VB to C# conversion */];
            bool[] lerr = new bool[k + 1 /* for VB to C# conversion */];
            bool[] uerr = new bool[k + 1 /* for VB to C# conversion */];
            bool[] cced = new bool[k + 1 /* for VB to C# conversion */];
            double[] axll = new double[k + 1 /* for VB to C# conversion */ ];
            double[] axul = new double[k + 1 /* for VB to C# conversion */ ];
            for (i = 1; i <= k; i++)
            {
                o[i, 1] = Math.Abs(sr[i]);
                o[i, 3] = Math.Abs(sn[i] - sr[i]);
                if (sr[i] < 0 || sn[i] < 0 || sn[i] < sr[i])
                {
                    throw new InvalidDataException("All data values must be >= 0, and the number responding must be less than the sample size");
                }
                o[i, 2] = Math.Abs(xr[i]);
                o[i, 4] = Math.Abs(xn[i] - xr[i]);
                if (xr[i] < 0 || xn[i] < 0 || xn[i] < xr[i])
                {
                    throw new InvalidDataException("All data values must be >= 0, and the number responding must be less than the sample size");
                }
            }

            Relriskma(host, ref k, out realk, ref o, ref rmh, ref ll, ref ul, ref x2Rmh, ref sk, ref cit, ref cco, ref rkr, ref rkw, ref dsw, ref rkrl, ref rkru, ref rkx, ref lerr, ref uerr, ref qc, ref dsrr, ref dsx2, ref dsll, ref dsul, ref tausq, ref cced, out ierr);
            if (ierr == -1)
            {
                throw new InvalidDataException("relriskma() returned an error");
            }

            //  RTF_LoadTemplate("rrmeta.rtf")
            ParameterBag outputParameters = new ParameterBag();

            IList<ParameterBag> inputsList = new List<ParameterBag>();
            outputParameters.AddOutput("*inputs", inputsList);
            for (i = 1; i <= k; i++)
            {
                ParameterBag inputsParameters = new ParameterBag();
                inputsList.Add(inputsParameters);
                inputsParameters.AddOutput("st", i.ToString());
                inputsParameters.AddOutput("a", o[i, 1].ToString());
                inputsParameters.AddOutput("b", o[i, 2].ToString());
                inputsParameters.AddOutput("c", o[i, 3].ToString());
                inputsParameters.AddOutput("d", o[i, 4].ToString());
                inputsParameters.AddOutput("lb", stratlab ? title[i] : "");
            }

            outputParameters.AddOutput("pc", Formatting.XRound(cco * 100, 2));
            outputParameters.AddOutput("method", host.Preferences.MetaExact ? "Koopman" : "approximate");

            IList<ParameterBag> risksList = new List<ParameterBag>();
            outputParameters.AddOutput("*risks", risksList);
            for (i = 1; i <= k; i++)
            {
                ParameterBag risksParameters = new ParameterBag();
                risksList.Add(risksParameters);
                risksParameters.AddOutput("st", i.ToString());
                risksParameters.AddOutput("rr", host.RoundU(rkr[i]));
                risksParameters.AddOutput("lci", host.RoundU(rkrl[i]));
                risksParameters.AddOutput("uci", host.RoundU(rkru[i]));
                risksParameters.AddOutput("wt", host.RoundU(100 * rkw[i] / Formatting.dsum(rkw, 1)));
                risksParameters.AddOutput("dwt", host.RoundU(100 * dsw[i] / Formatting.dsum(dsw, 1)));
                risksParameters.AddOutput("lb", GetMetaLabel(host, o, i, stratlab, cced, title));
            }

            outputParameters.AddOutput("rr", host.RoundU(rmh));
            outputParameters.AddOutput("from", host.RoundU(ll));
            outputParameters.AddOutput("to", host.RoundU(ul));

            outputParameters.AddOutput("x2", host.RoundU(x2Rmh));
            outputParameters.AddOutput("df", 1.ToString());
            outputParameters.AddOutput("xp", host.pval(PDF.chivalp(x2Rmh, 1.0)));

            outputParameters.AddOutput("qc", host.RoundU(qc));
            outputParameters.AddOutput("df_cochran", (realk - 1).ToString());
            outputParameters.AddOutput("xp_cochran", host.pval(PDF.chivalp(qc, Convert.ToDouble(realk - 1))));
            outputParameters.AddOutput("tausq", host.RoundU(tausq));
            IsquareNcc(host, qc, realk, cco, cit, out isq, out llisq, out ulisq);
            outputParameters.AddOutput("isq", Formatting.XRound(isq, 1));
            outputParameters.AddOutput("pc1", Formatting.XRound(cco * 100, 1));
            outputParameters.AddOutput("llisq", Formatting.XRound(llisq, 1));
            outputParameters.AddOutput("ulisq", Formatting.XRound(ulisq, 1));


            outputParameters.AddOutput("dsrr", host.RoundU(dsrr));
            outputParameters.AddOutput("dsll", host.RoundU(dsll));
            outputParameters.AddOutput("dsul", host.RoundU(dsul));
            outputParameters.AddOutput("dsx2", host.RoundU(dsx2));
            outputParameters.AddOutput("df_ds", 1.ToString());
            outputParameters.AddOutput("xp_ds", host.pval(PDF.chivalp(dsx2, 1.0)));

            GetAproxrrCI(host, o, k, cit, axll, axul);

            IList<ParameterBag> eggerList = new List<ParameterBag>();
            outputParameters.AddOutput("*egger", eggerList);
            ParameterBag eggerParameters = new ParameterBag();
            eggerList.Add(eggerParameters);
            Metabias(host, eggerParameters, rkr, axll, axul, k, ref cco, Transformation.Log);

            IList<ParameterBag> horboldList = new List<ParameterBag>();
            outputParameters.AddOutput("*horbold", horboldList);
            ParameterBag horboldParameters = new ParameterBag();
            horboldList.Add(horboldParameters);
            ModMetabias(host, horboldParameters, o, k, cco, 2);

            IList<ParameterBag> chartList = new List<ParameterBag>();
            outputParameters.AddOutput("*chart", chartList);
            ParameterBag chartParameters;

            if (k > 3)
            {
                using (ChartRenderer ch = new ChartRenderer(ChartDefinition.Empty()))
                {
                    string rtf = ch.PlotBiasMAAndReturnRtf(host, rkr, rkx, rkw, k, "Relative risk", axll, axul, cco, cit, rmh, Transformation.Log, false);
                    chartParameters = new ParameterBag();
                    chartList.Add(chartParameters);
                    chartParameters.AddOutput("chart", rtf);
                }
            }

            using (ChartRenderer ch = new ChartRenderer(ChartDefinition.Empty()))
            {
                string rtf = ch.PlotLAbbeAndReturnRtf(host, k, o, rmh);
                chartParameters = new ParameterBag();
                chartList.Add(chartParameters);
                chartParameters.AddOutput("chart", rtf);
            }

            using (ChartRenderer ch = new ChartRenderer(ChartDefinition.Empty()))
            {
                bool fault;
                string rtf = ch.PlotMHAndReturnRtf(host, k, o, rkw, title, rmh, ll, ul, cco, rkr, rkrl, rkru, lerr, uerr, "Relative risk meta-analysis plot (fixed effects)", 1, "relative risk", out fault, null);
                chartParameters = new ParameterBag();
                chartList.Add(chartParameters);
                chartParameters.AddOutput("chart", rtf);
            }

            using (ChartRenderer ch = new ChartRenderer(ChartDefinition.Empty()))
            {
                bool fault;
                string rtf = ch.PlotMHAndReturnRtf(host, k, o, dsw, title, dsrr, dsll, dsul, cco, rkr, rkrl, rkru, lerr, uerr, "Relative risk meta-analysis plot (random effects)", 1, "relative risk", out fault, null);
                chartParameters = new ParameterBag();
                chartList.Add(chartParameters);
                chartParameters.AddOutput("chart", rtf);
            }

            return new StepResult(StepSuccess.Success, outputParameters);
        }


        public static StepResult RptEffect(ITemplateHost host, ParameterBag parameters)
        {
            double dsul = 0; double dsll = 0; double dsz = 0; double dsd = 0;
            double tausq = 0; double sumsqwt; double qc = 0; double dplusul = 0; double dplusll = 0; double dplusz = 0;
            double vardplus; double dplus = 0; double wt; double sumdwt; double sumwt;
            double n; double cit;
            double isq; double llisq; double ulisq;
            int i;
            int proc; bool stratlab = false;
            double[] em = null; double[] es = null;
            double[] cm = null; double[] cs = null;
            double[] d;
            double[] lcid; double[] ucid;
            double[] rkw; double[] rkx;
            bool poolok; bool gotg;
            int fault;

            double cco = parameters["gamma"].AsDouble;
            if (cco > 0)
            {
                double p = (1.0 - cco) / 2.0;
                cit = PDF.gauinv(1.0 - p, out fault);
            }
            else
            {
                cco = 0.95;
                cit = PDF.gauinv(0.975, out fault);
            }

            string type = parameters["type"].AsString.ToLower();
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
            DoubleVariable enVariable = enFrame.Variables[0].AsDoubleVariable;
            int k = enVariable.Length;
            double[] en = new double[k + 1 /* for VB to C# conversion */ ];
            double[] g = new double[k + 1 /* for VB to C# conversion */ ];
            string[] title = new string[k + 1 /* for VB to C# conversion */ ];
            for (i = 1; i <= k; i++)
            {
                en[i] = enVariable.Data[i - 1];
            }

            if (proc != 2)
            {
                DataFrame emFrame = parameters["em"].AsDataFrame;
                DoubleVariable emVariable = emFrame.Variables[0].AsDoubleVariable;
                em = new double[k + 1 /* for VB to C# conversion */ ];
                for (i = 1; i <= k; i++)
                {
                    em[i] = emVariable.Data[i - 1];
                }

                DataFrame esFrame = parameters["es"].AsDataFrame;
                DoubleVariable esVariable = esFrame.Variables[0].AsDoubleVariable;
                es = new double[k + 1 /* for VB to C# conversion */ ];
                for (i = 1; i <= k; i++)
                {
                    es[i] = esVariable.Data[i - 1];
                }
            }

            DataFrame cnFrame = parameters["cn"].AsDataFrame;
            DoubleVariable cnVariable = cnFrame.Variables[0].AsDoubleVariable;
            k = cnVariable.Length;
            double[] cn = new double[k + 1 /* for VB to C# conversion */ ];
            for (i = 1; i <= k; i++)
            {
                cn[i] = cnVariable.Data[i - 1];
            }

            if (proc == 2)
            {
                gotg = true;
                DataFrame gFrame = parameters["g"].AsDataFrame;
                DoubleVariable gVariable = gFrame.Variables[0].AsDoubleVariable;
                k = gVariable.Length;
                //  ReDim g(k)
                for (i = 1; i <= k; i++)
                {
                    g[i] = gVariable.Data[i - 1];
                }
            }
            else
            {
                gotg = false;
                DataFrame cmFrame = parameters["cm"].AsDataFrame;
                DoubleVariable cmVariable = cmFrame.Variables[0].AsDoubleVariable;
                cm = new double[k + 1 /* for VB to C# conversion */ ];
                for (i = 1; i <= k; i++)
                {
                    cm[i] = cmVariable.Data[i - 1];
                }

                DataFrame csFrame = parameters["cs"].AsDataFrame;
                DoubleVariable csVariable = csFrame.Variables[0].AsDoubleVariable;
                cs = new double[k + 1 /* for VB to C# conversion */ ];
                for (i = 1; i <= k; i++)
                {
                    cs[i] = csVariable.Data[i - 1];
                }
            }

            //  ReDim title(k)
            if (parameters.ContainsKey("strata") && parameters["strata"].Data != null)
            {
                stratlab = true;
                DataFrame strataFrame = parameters["strata"].AsDataFrame;
                StringVariable strataVariable = strataFrame.Variables[0].AsStringVariable;
                for (i = 1; i <= k; i++)
                {
                    string buf = strataVariable.Data[i - 1].Trim();
                    if (buf.Length > 0)
                    {
                        if (buf.Length > 50)
                        {
                            buf = buf.Substring(0, 50);
                        }
                        title[i] = buf;
                    }
                    else
                    {
                        title[i] = "stratum " + i.ToString();
                    }
                }
            }
            else
            {
                for (i = 1; i <= k; i++)
                {
                    title[i] = "stratum " + i.ToString();
                }
            }

            if (proc != 3)
            {

                // single effect analysis
                d = new double[k + 1 /* for VB to C# conversion */ ];
                double[] gj = new double[k + 1 /* for VB to C# conversion */];
                lcid = new double[k + 1 /* for VB to C# conversion */];
                ucid = new double[k + 1 /* for VB to C# conversion */ ];
                double[] lcig = new double[k + 1 /* for VB to C# conversion */ ];
                double[] ucig = new double[k + 1 /* for VB to C# conversion */ ];
                rkw = new double[k + 1 /* for VB to C# conversion */];
                rkx = new double[k + 1 /* for VB to C# conversion */ ];
                Debug.Assert(null != es);
                poolok = k > 1;
                if (!(gotg))
                {
                    for (i = 1; i <= k; i++)
                    {
                        n = cn[i] + en[i];
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
                for (i = 1; i <= k; i++)
                {
                    n = cn[i] + en[i];
                    rkx[i] = n;
                    if (g[i] != Constant.MISSING)
                    {
                        double m = n - 2;
                        if (m < 200)
                        {
                            gj[i] = Math.Exp(PDF.alogam(m / 2.0)) / (Math.Sqrt(m / 2.0) * Math.Exp(PDF.alogam((m - 1.0) / 2.0)));
                        }
                        else
                        {
                            gj[i] = 1.0 - 3.0 / (4.0 * m - 1.0);
                        }
                        d[i] = gj[i] * g[i];
                        vard = n / (cn[i] * en[i]) + Math.Pow(d[i], 2.0) / (2.0 * n);
                        lcid[i] = d[i] - cit * Math.Sqrt(vard);
                        ucid[i] = d[i] + cit * Math.Sqrt(vard);
                        double z = Math.Sqrt(cn[i] * en[i] / n);
                        Ginterval(g[i], Convert.ToInt32(n - 2), z, (1.0 - cco) / 2.0, lcid[i], ucid[i], out lcig[i], out ucig[i]);
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

                // pooled analysis
                if (poolok)
                {
                    sumwt = 0;
                    sumdwt = 0;
                    for (i = 1; i <= k; i++)
                    {
                        n = cn[i] + en[i];
                        vard = n / (cn[i] * en[i]) + Math.Pow(d[i], 2.0) / (2.0 * n);
                        wt = 1.0 / vard;
                        rkw[i] = wt;
                        sumwt += wt;
                        sumdwt += d[i] * wt;
                    }
                    dplus = sumdwt / sumwt;
                    vardplus = 1 / sumwt;
                    dplusz = dplus / Math.Sqrt(vardplus);
                    dplusll = dplus - cit * Math.Sqrt(vardplus);
                    dplusul = dplus + cit * Math.Sqrt(vardplus);
                    qc = 0;
                    sumwt = 0;
                    sumsqwt = 0;
                    for (i = 1; i <= k; i++)
                    {
                        n = cn[i] + en[i];
                        vard = n / (cn[i] * en[i]) + (d[i] * d[i]) / (2.0 * n);
                        wt = 1.0 / vard;
                        qc += wt * Math.Pow((d[i] - dplus), 2.0);
                        sumwt += wt;
                        sumsqwt += wt * wt;
                    }

                    // DerSimonian-Laird treatment
                    if ((sumwt - sumsqwt / sumwt) == 0.0)
                    {
                        tausq = 0.0;
                    }
                    else
                    {
                        tausq = (qc - Convert.ToDouble(k - 1)) / (sumwt - sumsqwt / sumwt);
                    }
                    if (tausq < 0)
                    {
                        tausq = 0;
                    }
                    sumwt = 0;
                    sumdwt = 0;
                    for (i = 1; i <= k; i++)
                    {
                        n = cn[i] + en[i];
                        vard = n / (cn[i] * en[i]) + (d[i] * d[i]) / (2.0 * n);
                        wt = 1.0 / vard;
                        wt = 1.0 / (tausq + 1.0 / wt);
                        sumwt += wt;
                        sumdwt += d[i] * wt;
                    }

                    dsd = sumdwt / sumwt;
                    dsz = sumdwt / Math.Sqrt(sumwt);
                    dsll = dsd - cit / Math.Sqrt(sumwt);
                    dsul = dsd + cit / Math.Sqrt(sumwt);
                }

                //  RTF_LoadTemplate("effect.rtf")
                ParameterBag outputParameters = new ParameterBag();
                outputParameters.AddOutput("pc", Formatting.XRound(cco * 100, 2));

                IList<ParameterBag> exactList = new List<ParameterBag>();
                outputParameters.AddOutput("*exact", exactList);
                for (i = 1; i <= k; i++)
                {
                    ParameterBag exactParameters = new ParameterBag();
                    exactList.Add(exactParameters);
                    exactParameters.AddOutput("st", i.ToString());
                    exactParameters.AddOutput("gj", host.RoundU(gj[i]));
                    exactParameters.AddOutput("g", host.RoundU(g[i]));
                    exactParameters.AddOutput("lci", host.RoundU(lcig[i]));
                    exactParameters.AddOutput("uci", host.RoundU(ucig[i]));
                    exactParameters.AddOutput("lb", stratlab ? title[i] : "");
                }

                IList<ParameterBag> approximateList = new List<ParameterBag>();
                outputParameters.AddOutput("*approximate", approximateList);
                for (i = 1; i <= k; i++)
                {
                    ParameterBag approximateParameters = new ParameterBag();
                    approximateList.Add(approximateParameters);
                    approximateParameters.AddOutput("st", i.ToString());
                    approximateParameters.AddOutput("ne", en[i].ToString());
                    approximateParameters.AddOutput("nc", cn[i].ToString());
                    approximateParameters.AddOutput("d", host.RoundU(d[i]));
                    approximateParameters.AddOutput("lci", host.RoundU(lcid[i]));
                    approximateParameters.AddOutput("uci", host.RoundU(ucid[i]));
                    approximateParameters.AddOutput("lb", stratlab ? title[i] : "");
                }

                IList<ParameterBag> poolOkList = new List<ParameterBag>();
                outputParameters.AddOutput("*poolok", poolOkList);
                if (poolok)
                {
                    ParameterBag poolOkParameters = new ParameterBag();
                    poolOkList.Add(poolOkParameters);
                    poolOkParameters.AddOutput("dplus", host.RoundU(dplus));
                    poolOkParameters.AddOutput("from", host.RoundU(dplusll));
                    poolOkParameters.AddOutput("to", host.RoundU(dplusul));
                    poolOkParameters.AddOutput("z", host.RoundU(dplusz));
                    poolOkParameters.AddOutput("p", host.pval(MathDbl.zvalp2(dplusz)));
                    poolOkParameters.AddOutput("qc", host.RoundU(qc));
                    poolOkParameters.AddOutput("df", (k - 1).ToString());
                    poolOkParameters.AddOutput("xp", host.pval(PDF.chivalp(qc, Convert.ToDouble(k - 1))));
                    poolOkParameters.AddOutput("tausq", host.RoundU(tausq));
                    IsquareNcc(host, qc, k, cco, cit, out isq, out llisq, out ulisq);
                    poolOkParameters.AddOutput("isq", Formatting.XRound(isq, 1));
                    poolOkParameters.AddOutput("pc1", Formatting.XRound(cco * 100, 1));
                    poolOkParameters.AddOutput("llisq", Formatting.XRound(llisq, 1));
                    poolOkParameters.AddOutput("ulisq", Formatting.XRound(ulisq, 1));
                    poolOkParameters.AddOutput("dsrd", host.RoundU(dsd));
                    poolOkParameters.AddOutput("dsll", host.RoundU(dsll));
                    poolOkParameters.AddOutput("dsul", host.RoundU(dsul));
                    poolOkParameters.AddOutput("dz", host.RoundU(dsz));
                    poolOkParameters.AddOutput("dp", host.pval(MathDbl.zvalp2(dsz)));
                }

                IList<ParameterBag> eggerList = new List<ParameterBag>();
                outputParameters.AddOutput("*egger", eggerList);
                ParameterBag eggerParameters = new ParameterBag();
                eggerList.Add(eggerParameters);
                Metabias(host, eggerParameters, d, lcid, ucid, k, ref cco, Transformation.None);

                IList<ParameterBag> chartList = new List<ParameterBag>();
                outputParameters.AddOutput("*chart", chartList);
                ParameterBag chartParameters;

                if (k > 3)
                {
                    using (ChartRenderer ch = new ChartRenderer(ChartDefinition.Empty()))
                    {
                        string rtf = ch.PlotBiasMAAndReturnRtf(host, d, rkx, rkw, k, "Effect size", lcid, ucid, cco, cit, dplus, Transformation.None, false);
                        chartParameters = new ParameterBag();
                        chartList.Add(chartParameters);
                        chartParameters.AddOutput("chart", rtf);
                    }
                }

                using (ChartRenderer ch = new ChartRenderer(ChartDefinition.Empty()))
                {
                    string rtf = ch.PlotEffectAndReturnRtf(host, k, cn, en, title, dplus, dplusll, dplusul, cco, d, lcid, ucid, "Effect size meta-analysis plot [fixed effects]", 1, "effect size");
                    chartParameters = new ParameterBag();
                    chartList.Add(chartParameters);
                    chartParameters.AddOutput("chart", rtf);
                }

                using (ChartRenderer ch = new ChartRenderer(ChartDefinition.Empty()))
                {
                    string rtf = ch.PlotEffectAndReturnRtf(host, k, cn, en, title, dsd, dsll, dsul, cco, d, lcid, ucid, "Effect size meta-analysis plot [random effects]", 1, "effect size");
                    chartParameters = new ParameterBag();
                    chartList.Add(chartParameters);
                    chartParameters.AddOutput("chart", rtf);
                }

                return new StepResult(StepSuccess.Success, outputParameters);
            }
            else
            {
                // single wmd analysis
                d = new double[k + 1 /* for VB to C# conversion */ ];
                lcid = new double[k + 1 /* for VB to C# conversion */];
                ucid = new double[k + 1 /* for VB to C# conversion */ ];
                rkw = new double[k + 1 /* for VB to C# conversion */];
                rkx = new double[k + 1 /* for VB to C# conversion */];
                poolok = k > 1;
                for (i = 1; i <= k; i++)
                {
                    n = cn[i] + en[i];
                    rkx[i] = n;
                    if (en[i] > 0 & cn[i] > 0 & cs[i] > 0)
                    {
                        double spool = ((en[i] - 1.0) * Math.Pow(es[i], 2.0) + (cn[i] - 1.0) * Math.Pow(cs[i], 2.0)) / (en[i] + cn[i] - 2.0);
                        double sed = Math.Sqrt(spool * (1.0 / en[i] + 1.0 / cn[i]));
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

                // pooled wmd analysis
                if (poolok)
                {
                    sumwt = 0;
                    sumdwt = 0;
                    for (i = 1; i <= k; i++)
                    {
                        wt = 1.0 / (Math.Pow(es[i], 2.0) / en[i] + Math.Pow(cs[i], 2.0) / cn[i]);
                        rkw[i] = wt;
                        sumwt = sumwt + wt;
                        sumdwt = sumdwt + d[i] * wt;
                    }
                    dplus = sumdwt / sumwt;
                    vardplus = 1.0 / sumwt;
                    dplusz = dplus / Math.Sqrt(vardplus);
                    dplusll = dplus - cit * Math.Sqrt(vardplus);
                    dplusul = dplus + cit * Math.Sqrt(vardplus);
                    qc = 0;
                    sumwt = 0;
                    sumsqwt = 0;
                    for (i = 1; i <= k; i++)
                    {
                        wt = 1.0 / (Math.Pow(es[i], 2.0) / en[i] + Math.Pow(cs[i], 2.0) / cn[i]);
                        qc = qc + wt * Math.Pow((d[i] - dplus), 2.0);
                        sumwt = sumwt + wt;
                        sumsqwt = sumsqwt + wt * wt;
                    }

                    // DerSimonian-Laird treatment
                    if ((sumwt - sumsqwt / sumwt) == 0.0)
                    {
                        tausq = 0.0;
                    }
                    else
                    {
                        tausq = (qc - Convert.ToDouble(k - 1)) / (sumwt - sumsqwt / sumwt);
                    }
                    if (tausq < 0)
                    {
                        tausq = 0;
                    }
                    sumwt = 0;
                    sumdwt = 0;
                    for (i = 1; i <= k; i++)
                    {
                        wt = 1.0 / (Math.Pow(es[i], 2.0) / en[i] + Math.Pow(cs[i], 2.0) / cn[i]);
                        wt = 1.0 / (tausq + 1.0 / wt);
                        sumwt = sumwt + wt;
                        sumdwt = sumdwt + d[i] * wt;
                    }
                    dsd = sumdwt / sumwt;
                    dsz = sumdwt / Math.Sqrt(sumwt);
                    dsll = dsd - cit / Math.Sqrt(sumwt);
                    dsul = dsd + cit / Math.Sqrt(sumwt);
                }

                //  RTF_LoadTemplate("effectwm.rtf")
                ParameterBag outputParameters = new ParameterBag();
                outputParameters.AddOutput("pc", Formatting.XRound(cco * 100, 2));
                IList<ParameterBag> approximateList = new List<ParameterBag>();
                outputParameters.AddOutput("*approximate", approximateList);
                for (i = 1; i <= k; i++)
                {
                    ParameterBag approximateParameters = new ParameterBag();
                    approximateList.Add(approximateParameters);
                    approximateParameters.AddOutput("st", i.ToString());
                    approximateParameters.AddOutput("ne", en[i].ToString());
                    approximateParameters.AddOutput("nc", cn[i].ToString());
                    approximateParameters.AddOutput("d", host.RoundU(d[i]));
                    approximateParameters.AddOutput("lci", host.RoundU(lcid[i]));
                    approximateParameters.AddOutput("uci", host.RoundU(ucid[i]));
                    approximateParameters.AddOutput("lb", stratlab ? title[i] : "");
                }

                IList<ParameterBag> poolOkList = new List<ParameterBag>();
                outputParameters.AddOutput("*poolok", poolOkList);
                if (poolok)
                {
                    ParameterBag poolOkParameters = new ParameterBag();
                    poolOkList.Add(poolOkParameters);
                    poolOkParameters.AddOutput("dplus", host.RoundU(dplus));
                    poolOkParameters.AddOutput("from", host.RoundU(dplusll));
                    poolOkParameters.AddOutput("to", host.RoundU(dplusul));
                    poolOkParameters.AddOutput("z", host.RoundU(dplusz));
                    poolOkParameters.AddOutput("p", host.pval(MathDbl.zvalp2(dplusz)));
                    poolOkParameters.AddOutput("qc", host.RoundU(qc));
                    poolOkParameters.AddOutput("df", (k - 1).ToString());
                    poolOkParameters.AddOutput("xp", host.pval(PDF.chivalp(qc, Convert.ToDouble(k - 1))));
                    poolOkParameters.AddOutput("tausq", host.RoundU(tausq));
                    IsquareNcc(host, qc, k, cco, cit, out isq, out llisq, out ulisq);
                    poolOkParameters.AddOutput("isq", Formatting.XRound(isq, 1));
                    poolOkParameters.AddOutput("pc1", Formatting.XRound(cco * 100, 1));
                    poolOkParameters.AddOutput("llisq", Formatting.XRound(llisq, 1));
                    poolOkParameters.AddOutput("ulisq", Formatting.XRound(ulisq, 1));
                    poolOkParameters.AddOutput("dsrd", host.RoundU(dsd));
                    poolOkParameters.AddOutput("dsll", host.RoundU(dsll));
                    poolOkParameters.AddOutput("dsul", host.RoundU(dsul));
                    poolOkParameters.AddOutput("dz", host.RoundU(dsz));
                    poolOkParameters.AddOutput("zp", host.pval(MathDbl.zvalp2(dsz)));
                }

                IList<ParameterBag> eggerList = new List<ParameterBag>();
                outputParameters.AddOutput("*egger", eggerList);
                ParameterBag eggerParameters = new ParameterBag();
                eggerList.Add(eggerParameters);
                Metabias(host, eggerParameters, d, lcid, ucid, k, ref cco, Transformation.None);

                IList<ParameterBag> chartList = new List<ParameterBag>();
                outputParameters.AddOutput("*chart", chartList);
                ParameterBag chartParameters;

                if (k > 3)
                {
                    using (ChartRenderer ch = new ChartRenderer(ChartDefinition.Empty()))
                    {
                        string rtf = ch.PlotBiasMAAndReturnRtf(host, d, rkx, rkw, k, "Effect size", lcid, ucid, cco, cit, dplus, Transformation.None, false);
                        chartParameters = new ParameterBag();
                        chartList.Add(chartParameters);
                        chartParameters.AddOutput("chart", rtf);
                    }
                }

                // bool bfault = false; 
                using (ChartRenderer ch = new ChartRenderer(ChartDefinition.Empty()))
                {
                    string rtf = ch.PlotEffectAndReturnRtf(host, k, cn, en, title, dplus, dplusll, dplusul, cco, d, lcid, ucid, "Effect size meta-analysis plot [fixed effects]", 1, "weighted mean difference");
                    chartParameters = new ParameterBag();
                    chartList.Add(chartParameters);
                    chartParameters.AddOutput("chart", rtf);
                }

                using (ChartRenderer ch = new ChartRenderer(ChartDefinition.Empty()))
                {
                    string rtf = ch.PlotEffectAndReturnRtf(host, k, cn, en, title, dsd, dsll, dsul, cco, d, lcid, ucid, "Effect size meta-analysis plot [random effects]", 1, "weighted mean difference");
                    chartParameters = new ParameterBag();
                    chartList.Add(chartParameters);
                    chartParameters.AddOutput("chart", rtf);
                }

                return new StepResult(StepSuccess.Success, outputParameters);
            }
        }


        private static void Ginterval(double g, int df, double z, double alpha, double lcid, double ucid, out double lcig, out double ucig)
        {
            int fault;

            double t = g * z;
            double al = 1.0 - alpha;
            double au = alpha;
            const double acc = 0.0001;
            double x = t; // LCID* z; 
            double na = ExFortran.pnct(t, df, x, out fault);
            double delta = Math.Abs(na - al);
            double last = delta;
            double gstep = x;
            int cnt = 0;
            double gtry;

            // lcig = Constant.MISSING;
            do
            {
                cnt = cnt + 1;
                if (cnt > 100)
                {
                    break;
                }
                gstep = gstep / 10.0;
                gtry = x + gstep;
                na = ExFortran.pnct(t, df, gtry, out fault);
            }
            while (!(na > 0 & na < 1));
            if (Math.Abs(na - al) > delta)
            {
                gstep = -gstep;
            }
            cnt = 0;
            gtry = x;
            do
            {
                cnt = cnt + 1;
                if (cnt > 500)
                {
                    lcig = Constant.MISSING;
                    break;
                }
                gtry = gtry + gstep;
                na = ExFortran.pnct(t, df, gtry, out fault);
                delta = Math.Abs(na - al);
                if (delta < acc)
                {
                    lcig = gtry / z;
                    break;
                }
                if (delta > last)
                {
                    gstep = -gstep / 10.0;
                }
                last = delta;
            }
            while (true);

            x = ucid * z;
            na = ExFortran.pnct(t, df, x, out fault);
            delta = Math.Abs(na - au);
            last = delta;
            gstep = x;
            cnt = 0;
            do
            {
                cnt = cnt + 1;
                if (cnt > 100)
                {
                    break;
                }
                gstep = gstep / 10.0;
                gtry = x + gstep;
                na = ExFortran.pnct(t, df, gtry, out fault);
            }
            while (!(na > 0 & na < 1));
            if (Math.Abs(na - au) > delta)
            {
                gstep = -gstep;
            }
            cnt = 0;
            gtry = x;
            ucig = Constant.MISSING;
            do
            {
                cnt = cnt + 1;
                if (cnt > 500)
                {
                    lcig = Constant.MISSING;
                    break;
                }
                gtry = gtry + gstep;
                na = ExFortran.pnct(t, df, gtry, out fault);
                delta = Math.Abs(na - au);
                if (delta < acc)
                {
                    ucig = gtry / z;
                    break;
                }
                if (delta > last)
                {
                    gstep = -gstep / 10.0;
                }
                last = delta;
            }
            while (true);
        }

        public static void Relriskma(ITemplateHost host, ref int k, out int realk, ref double[,] o, ref double rmh, ref double ll, ref double ul, ref double x2Rmh, ref double sk, ref double cit, ref double cco, ref double[] rkr, ref double[] rkw, ref double[] dsw, ref double[] rkrl, ref double[] rkru, ref double[] rkx, ref bool[] lerr, ref bool[] uerr, ref double qc, ref double dsrr, ref double dsx2, ref double dsll, ref double dsul, ref double tausq, ref bool[] cced, out int ierr)
        {
            ierr = -1;
            double siga = 0.0;
            double sumwt = 0.0;
            double svd1 = 0.0;
            double svd2 = 0.0;
            double svd3 = 0.0;
            realk = 0;

            for (int i = 1; i <= k; i++)
            {
                double a = o[i, 1];
                double b = o[i, 2];
                double c = o[i, 3];
                double d = o[i, 4];
                double n = a + b + c + d;
                rkx[i] = n;
                if (n <= 0)
                {
                    throw new InvalidDataException();
                }

                if (IncludeTable(o, i))
                {
                    realk++;

                    if (host.Preferences.MetaExact)
                    {
                        // try Koopman rr and ci for stratum before continuity correction
                        MathDbl.lr_ci(b, a, b + d, a + c, cit, out rkrl[i], out rkru[i]);
                        lerr[i] = (rkrl[i] == Constant.MISSING);
                        uerr[i] = (rkru[i] == Constant.MISSING);
                    }

                    // get rr continuity corrected if neccessary
                    if (a <= 0.0 || b <= 0.0 || c <= 0.0 || d <= 0.0)
                    {
                        cced[i] = true;
                        ContinuityCorrect(host, a, b, c, d, out a, out b, out c, out d);
                    }
                    else
                    {
                        cced[i] = false;
                    }
                    rkr[i] = (a / (a + c)) / (b / (b + d));
                    if (!(host.Preferences.MetaExact))
                    {
                        // approximate se of log rr
                        double selogrr = Math.Sqrt(1.0 / a + 1.0 / b - 1.0 / (a + c) - 1.0 / (b + d));
                        rkrl[i] = Math.Exp(Math.Log(rkr[i]) - selogrr * cit);
                        rkru[i] = Math.Exp(Math.Log(rkr[i]) + selogrr * cit);
                        lerr[i] = false;
                        uerr[i] = false;
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
            {
                Utilities.Utilities.Swap(ref ll, ref ul);
            }
            x2Rmh = Math.Pow((Math.Log(rmh) / Math.Sqrt(serr)), 2.0);

            // Q (combinability)
            qc = 0.0;
            sumwt = 0.0;
            double sumsqwt = 0.0;
            for (int i = 1; i <= k; i++)
            {
                if (IncludeTable(o, i))
                {
                    double a = o[i, 1];
                    double b = o[i, 2];
                    double c = o[i, 3];
                    double d = o[i, 4];
                    if (a <= 0.0 || b <= 0.0 || c <= 0.0 || d <= 0.0)
                    {
                        ContinuityCorrect(host, a, b, c, d, out a, out b, out c, out d);
                    }
                    double n = a + b + c + d;
                    // Weight = b * ( a + C ) / N; - unused
                    // using weight as 1/variance
                    svd1 = ((a + b) * (a + c) * (b + d) - a * b * n) / Math.Pow(n, 2.0);
                    svd2 = a * (b + d) / n;
                    svd3 = b * (a + c) / n;
                    double wt = 1.0 / (svd1 / (svd2 * svd3));
                    double lrri = Math.Log((a / (a + c)) / (b / (b + d)));
                    qc += wt * Math.Pow((lrri - Math.Log(rmh)), 2.0);
                    sumwt += wt;
                    sumsqwt += wt * wt;
                }
            }

            // DerSimonian-Laird random effects
            if ((sumwt - sumsqwt / sumwt) == 0.0)
            {
                tausq = 0.0;
            }
            else
            {
                tausq = (qc - Convert.ToDouble(realk - 1)) / (sumwt - sumsqwt / sumwt);
            }
            if (tausq < 0.0)
            {
                tausq = 0.0;
            }
            double wlrr = 0.0;
            sumwt = 0.0;
            // sumsqwt = 0.0; 
            for (int i = 1; i <= k; i++)
            {
                if (IncludeTable(o, i))
                {
                    double a = o[i, 1];
                    double b = o[i, 2];
                    double c = o[i, 3];
                    double d = o[i, 4];
                    if (a <= 0.0 || b <= 0.0 || c <= 0.0 || d <= 0.0)
                    {
                        ContinuityCorrect(host, a, b, c, d, out a, out b, out c, out d);
                    }
                    double n = a + b + c + d;
                    // using weight as 1/var
                    svd1 = ((a + b) * (a + c) * (b + d) - a * b * n) / Math.Pow(n, 2.0);
                    svd2 = a * (b + d) / n;
                    svd3 = b * (a + c) / n;
                    double wt = 1.0 / (svd1 / (svd2 * svd3));
                    double weight = 1.0 / (tausq + 1.0 / wt);
                    dsw[i] = weight;
                    double lrri = Math.Log((a / (a + c)) / (b / (b + d)));
                    wlrr = wlrr + lrri * weight;
                    sumwt = sumwt + weight;
                }
            }
            dsrr = Math.Exp(wlrr / sumwt);
            dsx2 = Math.Pow(wlrr, 2.0) / sumwt;
            dsll = Math.Exp(wlrr / sumwt - cit / Math.Sqrt(sumwt));
            dsul = Math.Exp(wlrr / sumwt + cit / Math.Sqrt(sumwt));
            if (dsll > dsul)
            {
                Utilities.Utilities.Swap(ref dsll, ref dsul);
            }
            ierr = 0;
        }


        private static void Riskdifma(ITemplateHost host, int k, double[,] o, ref double rmh, ref double ll, ref double ul, ref double x2Rmh, ref double sk, ref double cit, ref double cco, ref double[] rkr, ref double[] rkw, ref double[] dsw, ref double[] rkrl, ref double[] rkru, ref double[] rkx, ref bool[] lerr, ref bool[] uerr, ref double qc, ref double dsrd, ref double dsx2, ref double dsll, ref double dsul, ref double tausq, ref bool[] cced, double[] standardizedEffect, double[] se, out int ierr)
        {
            double wt;
            double rkrs;
            double a; double b; double c; double d; double vark;
            int i;

            ierr = -1;
            double sumlk = 0.0;
            double mhn = 0.0;
            double mhd = 0.0;
            for (i = 1; i <= k; i++)
            {
                a = o[i, 1];
                b = o[i, 2];
                c = o[i, 3];
                d = o[i, 4];
                double n = a + b + c + d;
                rkx[i] = n;
                // rd and ci for stratum
                if ((b + d) <= 0.0 || (a + c) <= 0.0)
                {
                    rkr[i] = Constant.MISSING;
                    rkrl[i] = Constant.MISSING;
                    rkru[i] = Constant.MISSING;
                    lerr[i] = true;
                    uerr[i] = true;
                }
                else
                {
                    rkr[i] = a / (a + c) - b / (b + d);
                    if (host.Preferences.MetaExact)
                    {
                        double r1 = a;
                        double n1 = a + c;
                        double r2 = b;
                        double n2 = b + d;
                        MathDbl.uppci(Convert.ToInt32(r1), Convert.ToInt32(n1), Convert.ToInt32(r2), Convert.ToInt32(n2), out rkrl[i], out rkru[i], cit, 100.0 * cco);
                    }
                }
                //  rd across strata
                if (n <= 0)
                    throw new InvalidDataException();

                // standard weights - do this before continuity correction
                double nmn = (a + c) * (b + d) / n;
                rkw[i] = nmn;
                mhn = mhn + (a * (b + d) / n - b * (a + c) / n);
                mhd = mhd + nmn;
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
                //  Greenland-Robins pooled risk difference
                double lk = (a * c * Math.Pow((b + d), 3.0) + b * d * Math.Pow((a + c), 3.0)) / ((a + c) * (b + d) * Math.Pow(n, 2.0));
                sumlk = sumlk + lk;
                // inverse variance weights
                // rkw(i) = 1# / vark
                if (!(host.Preferences.MetaExact))
                {
                    vark = a * c / Math.Pow((a + c), 3.0) + b * d / Math.Pow((b + d), 3.0);
                    se[i] = Math.Sqrt(vark);
                    standardizedEffect[i] = rkr[i];
                    rkrl[i] = rkr[i] - cit * se[i];
                    rkru[i] = rkr[i] + cit * se[i];
                }
                if (rkrl[i] != Constant.MISSING)
                {
                    lerr[i] = false;
                }
                else { lerr[i] = true; }
                if (rkru[i] != Constant.MISSING)
                {
                    uerr[i] = false;
                }
                else { uerr[i] = true; }
            }
            rmh = mhn / mhd;
            double serd = Math.Sqrt(sumlk / (Math.Pow(mhd, 2.0)));
            ll = rmh - serd * cit;
            ul = rmh + serd * cit;
            if (ll > ul)
            {
                Utilities.Utilities.Swap(ref ll, ref ul);
            }
            x2Rmh = Math.Pow((rmh / serd), 2.0);
            // Q (combinability)
            qc = 0.0;
            double sumwt = 0.0;
            double sumsqwt = 0.0;
            for (i = 1; i <= k; i++)
            {
                a = o[i, 1];
                b = o[i, 2];
                c = o[i, 3];
                d = o[i, 4];
                // nmn = ( a + C ) * ( b + D ) / N; 
                rkrs = a / (a + c) - b / (b + d);
                if (a <= 0.0 || b <= 0.0 || c <= 0.0 || d <= 0.0)
                {
                    ContinuityCorrect(host, a, b, c, d, out a, out b, out c, out d);
                }
                vark = a * c / Math.Pow((a + c), 3.0) + b * d / Math.Pow((b + d), 3.0);
                wt = 1.0 / vark;
                qc += wt * Math.Pow((rkrs - rmh), 2.0);
                sumwt += wt;
                sumsqwt += wt * wt;
            }
            // DerSimonian-Laird random effects
            if ((sumwt - sumsqwt / sumwt) == 0.0)
            {
                tausq = 0.0;
            }
            else
            {
                tausq = (qc - (k - 1.0)) / (sumwt - sumsqwt / sumwt);
            }
            if (tausq < 0.0)
            {
                tausq = 0.0;
            }
            double wrd = 0.0;
            sumwt = 0.0;
            // sumsqwt = 0.0; 
            for (i = 1; i <= k; i++)
            {
                a = o[i, 1];
                b = o[i, 2];
                c = o[i, 3];
                d = o[i, 4];
                // nmn = ( ( a + C ) * ( b + D ) ) / N; 
                rkrs = a / (a + c) - b / (b + d);
                standardizedEffect[i] = rkrs;
                se[i] = 0; // TODO: What?
                if (a <= 0.0 || b <= 0.0 || c <= 0.0 || d <= 0.0)
                {
                    ContinuityCorrect(host, a, b, c, d, out a, out b, out c, out d);
                }
                vark = a * c / Math.Pow((a + c), 3) + b * d / Math.Pow((b + d), 3);
                wt = 1.0 / vark;
                double weight = 1.0 / (tausq + 1.0 / wt);
                dsw[i] = weight;
                wrd += rkrs * weight;
                sumwt += weight;
            }
            dsrd = wrd / sumwt;
            dsx2 = Math.Pow(wrd, 2.0) / sumwt;
            dsll = wrd / sumwt - cit / Math.Sqrt(sumwt);
            dsul = wrd / sumwt + cit / Math.Sqrt(sumwt);
            if (dsll > dsul)
            {
                Utilities.Utilities.Swap(ref dsll, ref dsul);
            }
            ierr = 0;
        }


        public static StepResult RptMetaIncidenceRateRatio(ITemplateHost host, ParameterBag parameters)
        {
            return RptMetaIncidenceRate(host, parameters, 2);
        }


        public static StepResult RptMetaIncidenceRateDifference(ITemplateHost host, ParameterBag parameters)
        {
            return RptMetaIncidenceRate(host, parameters, 1);
        }


        private static StepResult RptMetaIncidenceRate(ITemplateHost host, ParameterBag parameters, int index)
        {
            double p2M = 0; double p1M = 0; double p2F = 0; double p1F = 0; double llm = 0; double ulm = 0;
            double llf = 0; double ulf = 0; double eor = 0; double dsirr = 0; double dsul; double dsll; double tausq;
            double dz; double dsird = 0; double qc; double zrmh; double ul; double ll; double rmh;
            double cit;
            double isq; double llisq; double ulisq;
            int i;
            int ierr; double realk; bool stratlab;

            double cco = parameters["gamma"].AsDouble;
            if (cco > 0)
            {
                double p = (1.0 - cco) / 2.0;
                int scrap;
                cit = PDF.gauinv(1.0 - p, out scrap);
            }
            else
            {
                cco = 0.95;
                int scrap;
                cit = PDF.gauinv(0.975, out scrap);
            }

            DataFrame aFrame = parameters["a"].AsDataFrame;
            DoubleVariable aVariable = aFrame.Variables[0].AsDoubleVariable;
            int k = aVariable.Length;
            double[] a = new double[k + 1];
            for (i = 1; i <= k; i++)
            {
                a[i] = aVariable.Data[i - 1];
            }

            DataFrame pt1Frame = parameters["pt1"].AsDataFrame;
            DoubleVariable pt1Variable = pt1Frame.Variables[0].AsDoubleVariable;
            double[] pt1 = new double[k + 1];
            for (i = 1; i <= k; i++)
            {
                pt1[i] = pt1Variable.Data[i - 1];
            }

            DataFrame bFrame = parameters["b"].AsDataFrame;
            DoubleVariable bVariable = bFrame.Variables[0].AsDoubleVariable;
            double[] b = new double[k + 1];
            for (i = 1; i <= k; i++)
            {
                b[i] = bVariable.Data[i - 1];
            }

            DataFrame pt2Frame = parameters["pt2"].AsDataFrame;
            DoubleVariable pt2Variable = pt2Frame.Variables[0].AsDoubleVariable;
            double[] pt2 = new double[k + 1];
            for (i = 1; i <= k; i++)
            {
                pt2[i] = pt2Variable.Data[i - 1];
            }

            string[] title = new string[k + 1];
            if (parameters.ContainsKey("strata") && parameters["strata"].Data != null)
            {
                stratlab = true;
                DataFrame strataFrame = parameters["strata"].AsDataFrame;
                StringVariable strataVariable = strataFrame.Variables[0].AsStringVariable;
                for (i = 1; i <= k; i++)
                {
                    string buf = strataVariable.Data[i - 1].Trim();
                    if (buf.Length > 0)
                    {
                        if (buf.Length > 50)
                        {
                            buf = buf.Substring(0, 50);
                        }
                        title[i] = buf;
                    }
                    else
                    {
                        title[i] = "stratum " + i.ToString();
                    }
                }
            }
            else
            {
                stratlab = false;
                for (i = 1; i <= k; i++)
                {
                    title[i] = "stratum " + i.ToString();
                }
            }

            double[,] o = new double[k + 1, 4 + 1];
            double[] rkr = new double[k + 1];
            double[] rkw = new double[k + 1];
            double[] dsw = new double[k + 1];
            double[] rkrl = new double[k + 1];
            double[] rkru = new double[k + 1];
            bool[] lerr = new bool[k + 1];
            bool[] uerr = new bool[k + 1];
            double[] standardizedEffect = new double[k + 1];
            double se;
            if (index == 1)
            {
                IrdMeta(k, a, b, pt1, pt2, out rmh, out ll, out ul, out zrmh, ref cit, ref cco, rkr, rkw, dsw, rkrl, rkru, lerr, uerr, out qc, out dsird, out dz, out dsll, out dsul, out realk, out tausq, standardizedEffect, out se, out ierr);
            }
            else
            {
                IrrMeta(k, a, b, pt1, pt2, out rmh, out ll, out ul, out zrmh, ref cit, ref cco, rkr, rkw, dsw, rkrl, rkru, lerr, uerr, out qc, out dsirr, out dz, out dsll, out dsul, out realk, out tausq, standardizedEffect, out se, out ierr);
            }
            if (ierr == -1)
            {
                throw new InvalidDataException();
            }

            if (index == 2)
            {
                // Try exact IRR
                if (host.Preferences.MetaExact)
                {
                    ExactBB.Rec2X2[] tbl = new ExactBB.Rec2X2[k + 1 /* for VB to C# conversion */];
                    for (i = 1; i <= k; i++)
                    {
                        tbl[i].Freq = 1;
                        tbl[i].A = a[i];
                        tbl[i].M1 = b[i] + a[i];
                        tbl[i].N1 = pt1[i];
                        tbl[i].N0 = pt2[i];
                        tbl[i].Informative = (a[i] * pt1[i] != 0.0) | (b[i] * pt2[i] != 0.0);
                    }
                    bool useLogScale = false;
                    new ExactBB().Exact22K(host, k, 1, tbl, cco, ref eor, out ulf, out llf, out ulm, out llm, out p1F, out p2F, out p1M, out p2M, ref useLogScale, out ierr);
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

            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("pc", Formatting.XRound(cco * 100, 2));

            IList<ParameterBag> inputsList = new List<ParameterBag>();
            outputParameters.AddOutput("*inputs", inputsList);
            for (i = 1; i <= k; i++)
            {
                ParameterBag inputsParameters = new ParameterBag();
                inputsList.Add(inputsParameters);
                inputsParameters.AddOutput("st", i.ToString());
                inputsParameters.AddOutput("a", a[i].ToString());
                inputsParameters.AddOutput("pt1", host.RoundU(pt1[i]));
                inputsParameters.AddOutput("b", b[i].ToString());
                inputsParameters.AddOutput("pt2", host.RoundU(pt2[i]));
                inputsParameters.AddOutput("lb", stratlab ? title[i] : "");
            }

            IList<ParameterBag> irList = new List<ParameterBag>();
            outputParameters.AddOutput("*ir", irList);
            for (i = 1; i <= k; i++)
            {
                ParameterBag irParameters = new ParameterBag();
                irList.Add(irParameters);
                irParameters.AddOutput("st", i.ToString());
                irParameters.AddOutput(index == 1 ? "ird" : "irr", host.RoundU(rkr[i]));
                irParameters.AddOutput("lci", host.RoundU(rkrl[i]));
                irParameters.AddOutput("uci", host.RoundU(rkru[i]));
                irParameters.AddOutput("wt",
                                       rkw[i] != Constant.MISSING
                                           ? host.RoundU(100 * rkw[i] / Formatting.dsum(rkw, 1))
                                           : Formatting.ASTERISK);
                irParameters.AddOutput("dwt",
                                       dsw[i] != Constant.MISSING
                                           ? host.RoundU(100 * dsw[i] / Formatting.dsum(dsw, 1))
                                           : Formatting.ASTERISK);
                irParameters.AddOutput("lb", stratlab ? title[i] : "");
                irParameters.AddOutput("standardized_effect", host.RoundU(standardizedEffect[i]));
                irParameters.AddOutput("se", host.RoundU(se));
            }

            outputParameters.AddOutput("rmh", host.RoundU(rmh));
            outputParameters.AddOutput("from", host.RoundU(ll));
            outputParameters.AddOutput("to", host.RoundU(ul));
            outputParameters.AddOutput("z", host.RoundU(zrmh));
            outputParameters.AddOutput("p_z", host.pval(MathDbl.zvalp2(zrmh)));
            if (index == 2)
            {
                if (ierr == -9)
                {
                    outputParameters.AddOutput("*poolok", null);
                }
                else
                {
                    IList<ParameterBag> poolokList = new List<ParameterBag>();
                    outputParameters.AddOutput("*poolok", poolokList);
                    ParameterBag poolokParameters = new ParameterBag();
                    poolokList.Add(poolokParameters);
                    poolokParameters.AddOutput("eor", host.RoundU(eor));
                    poolokParameters.AddOutput("llf", host.RoundU(llf));
                    poolokParameters.AddOutput("ulf", host.RoundU(ulf));
                    poolokParameters.AddOutput("p1f", host.pval(p1F));
                    poolokParameters.AddOutput("p2f", host.pval(p2F));
                    poolokParameters.AddOutput("llm", host.RoundU(llm));
                    poolokParameters.AddOutput("ulm", host.RoundU(ulm));
                    poolokParameters.AddOutput("p1m", host.pval(p1M));
                    poolokParameters.AddOutput("p2m", host.pval(p2M));
                }
            }

            outputParameters.AddOutput("qc", host.RoundU(qc));
            outputParameters.AddOutput("df", (realk - 1).ToString());
            outputParameters.AddOutput("xp", host.pval(PDF.chivalp(qc, realk - 1)));
            outputParameters.AddOutput("tausq", host.RoundU(tausq));
            // Call isquare(qc, k, cit, isq, llisq, ulisq)
            IsquareNcc(host, qc, k, cco, cit, out isq, out llisq, out ulisq);
            outputParameters.AddOutput("isq", Formatting.XRound(isq, 1));
            outputParameters.AddOutput("pc1", Formatting.XRound(cco * 100, 1));
            outputParameters.AddOutput("llisq", Formatting.XRound(llisq, 1));
            outputParameters.AddOutput("ulisq", Formatting.XRound(ulisq, 1));

            if (index == 1)
            {
                outputParameters.AddOutput("dsird", host.RoundU(dsird));
            }
            else
            {
                outputParameters.AddOutput("dsirr", host.RoundU(dsirr));
            }
            outputParameters.AddOutput("dsll", host.RoundU(dsll));
            outputParameters.AddOutput("dsul", host.RoundU(dsul));
            outputParameters.AddOutput("dz", host.RoundU(dz));
            outputParameters.AddOutput("dp", host.pval(MathDbl.zvalp2(dz)));


            IList<ParameterBag> eggerList = new List<ParameterBag>();
            outputParameters.AddOutput("*egger", eggerList);
            ParameterBag eggerParameters = new ParameterBag();
            eggerList.Add(eggerParameters);
            Transformation xform = Transformation.None;
            if (index != 1)
            {
                xform = Transformation.Log;
            }
            Metabias(host, eggerParameters, rkr, rkrl, rkru, k, ref cco, xform);

            double[] ptt = new double[k + 1 /* for VB to C# conversion */ ];
            for (i = 1; i <= k; i++)
            {
                o[i, 1] = pt1[i];
                o[i, 2] = pt2[i];
                o[i, 3] = pt1[i];
                o[i, 4] = pt2[i];
                if (pt2[i] == Constant.MISSING | pt1[i] == Constant.MISSING)
                {
                    ptt[i] = Constant.MISSING;
                }
                else { ptt[i] = pt1[i] + pt2[i]; }
            }

            IList<ParameterBag> chartList = new List<ParameterBag>();
            outputParameters.AddOutput("*chart", chartList);
            ParameterBag chartParameters;

            if (index == 1)
            {
                if (k > 3)
                {
                    using (ChartRenderer ch = new ChartRenderer(ChartDefinition.Empty()))
                    {
                        string rtf = ch.PlotBiasMAAndReturnRtf(host, rkr, ptt, rkw, k, "Incidence rate difference", rkrl, rkru, cco, cit, rmh, Transformation.None, false);
                        chartParameters = new ParameterBag();
                        chartList.Add(chartParameters);
                        chartParameters.AddOutput("chart", rtf);
                    }
                }

                bool bfault;
                using (ChartRenderer ch = new ChartRenderer(ChartDefinition.Empty()))
                {
                    string rtf = ch.PlotMHRDAndReturnRtf(host, k, o, rkw, title, rmh, ll, ul, cco, rkr, rkrl, rkru, lerr, uerr, "Incidence rate difference meta-analysis plot [fixed effects]", 1, "incidence rate difference", out bfault);
                    chartParameters = new ParameterBag();
                    chartList.Add(chartParameters);
                    chartParameters.AddOutput("chart", rtf);
                }

                using (ChartRenderer ch = new ChartRenderer(ChartDefinition.Empty()))
                {
                    string rtf = ch.PlotMHRDAndReturnRtf(host, k, o, dsw, title, dsird, dsll, dsul, cco, rkr, rkrl, rkru, lerr, uerr, "Incidence rate difference meta-analysis plot [random effects]", 1, "incidence rate difference", out bfault);
                    chartParameters = new ParameterBag();
                    chartList.Add(chartParameters);
                    chartParameters.AddOutput("chart", rtf);
                }
            }
            else
            {
                if (k > 3)
                {
                    using (ChartRenderer ch = new ChartRenderer(ChartDefinition.Empty()))
                    {
                        string rtf = ch.PlotBiasMAAndReturnRtf(host, rkr, ptt, rkw, k, "Incidence rate ratio", rkrl, rkru, cco, cit, rmh, Transformation.Log, false);
                        chartParameters = new ParameterBag();
                        chartList.Add(chartParameters);
                        chartParameters.AddOutput("chart", rtf);
                    }
                }

                bool fault;
                using (ChartRenderer ch = new ChartRenderer(ChartDefinition.Empty()))
                {
                    string rtf = ch.PlotMHAndReturnRtf(host, k, o, rkw, title, rmh, ll, ul, cco, rkr, rkrl, rkru, lerr, uerr, "Incidence rate ratio meta-analysis plot [fixed effects]", 1, "incidence rate ratio", out fault, null);
                    chartParameters = new ParameterBag();
                    chartList.Add(chartParameters);
                    chartParameters.AddOutput("chart", rtf);
                }

                using (ChartRenderer ch = new ChartRenderer(ChartDefinition.Empty()))
                {
                    string rtf = ch.PlotMHAndReturnRtf(host, k, o, dsw, title, dsirr, dsll, dsul, cco, rkr, rkrl, rkru, lerr, uerr, "Incidence rate ratio meta-analysis plot [random effects]", 1, "incidence rate ratio", out fault, null);
                    chartParameters = new ParameterBag();
                    chartList.Add(chartParameters);
                    chartParameters.AddOutput("chart", rtf);
                }
            }
            return new StepResult(StepSuccess.Success, outputParameters);
        }


        public static StepResult RptMantel(ITemplateHost host, ParameterBag parameters)
        {
            double p2M = 0; double p1M = 0;
            double p2F = 0; double p1F = 0; double llm = 0; double ulm = 0; double llf = 0; double ulf = 0; double eor = 0; double tausq = 0;
            double dsul; double dsll; double dsx2; double dsor; double bd = 0; double qc = 0; double sk;
            double x2; double ul; double ll; double rmh; double cit;
            double isq; double llisq; double ulisq;
            int i;
            int ierr;
            int realk;
            bool stratlab;

            double cco = parameters["gamma"].AsDouble;
            if (cco > 0)
            {
                double p = (1.0 - cco) / 2.0;
                int scrap;
                cit = PDF.gauinv(1.0 - p, out scrap);
            }
            else
            {
                cco = 0.95;
                int scrap;
                cit = PDF.gauinv(0.975, out scrap);
            }
            DataFrame snFrame = parameters["sn"].AsDataFrame;
            DoubleVariable snVariable = snFrame.Variables[0].AsDoubleVariable;
            int k = snVariable.Length;
            double[] sn = new double[k + 1 /* for VB to C# conversion */];
            for (i = 1; i <= k; i++)
            {
                sn[i] = snVariable.Data[i - 1];
            }

            DataFrame srFrame = parameters["sr"].AsDataFrame;
            DoubleVariable srVariable = srFrame.Variables[0].AsDoubleVariable;
            double[] sr = new double[k + 1 /* for VB to C# conversion */ ];
            for (i = 1; i <= k; i++)
            {
                sr[i] = srVariable.Data[i - 1];
            }

            DataFrame xnFrame = parameters["xn"].AsDataFrame;
            DoubleVariable xnVariable = xnFrame.Variables[0].AsDoubleVariable;
            double[] xn = new double[k + 1 /* for VB to C# conversion */ ];
            for (i = 1; i <= k; i++)
            {
                xn[i] = xnVariable.Data[i - 1];
            }

            DataFrame xrFrame = parameters["xr"].AsDataFrame;
            DoubleVariable xrVariable = xrFrame.Variables[0].AsDoubleVariable;
            double[] xr = new double[k + 1 /* for VB to C# conversion */ ];
            for (i = 1; i <= k; i++)
            {
                xr[i] = xrVariable.Data[i - 1];
            }

            string[] title = new string[k + 1 /* for VB to C# conversion */];
            if (parameters.ContainsKey("strata") && parameters["strata"].Data != null)
            {
                stratlab = true;
                DataFrame strataFrame = parameters["strata"].AsDataFrame;
                StringVariable strataVariable = strataFrame.Variables[0].AsStringVariable;
                for (i = 1; i <= k; i++)
                {
                    string buf = strataVariable.Data[i - 1].Trim();
                    if (buf.Length > 0)
                    {
                        if (buf.Length > 50)
                        {
                            buf = buf.Substring(0, 50);
                        }
                        title[i] = buf;
                    }
                    else
                    {
                        title[i] = "stratum " + i.ToString();
                    }
                }
            }
            else
            {
                stratlab = false;
                for (i = 1; i <= k; i++)
                {
                    title[i] = "stratum " + i.ToString();
                }
            }

            double[,] o = new double[k + 1 /* for VB to C# conversion */, 4 + 1 /* for VB to C# conversion */];
            double[] odr = new double[k + 1 /* for VB to C# conversion */];
            double[] odrl = new double[k + 1 /* for VB to C# conversion */ ];
            double[] odru = new double[k + 1 /* for VB to C# conversion */ ];
            double[] odw = new double[k + 1 /* for VB to C# conversion */];
            double[] dswt = new double[k + 1 /* for VB to C# conversion */ ];
            double[] odx = new double[k + 1 /* for VB to C# conversion */ ];
            bool[] lerr = new bool[k + 1 /* for VB to C# conversion */ ];
            bool[] uerr = new bool[k + 1 /* for VB to C# conversion */];
            bool[] cced = new bool[k + 1 /* for VB to C# conversion */];
            double[] axll = new double[k + 1 /* for VB to C# conversion */];
            double[] axul = new double[k + 1 /* for VB to C# conversion */];
            for (i = 1; i <= k; i++)
            {
                o[i, 1] = Math.Abs(sr[i]);
                o[i, 3] = Math.Abs(sn[i] - sr[i]);
                if (sr[i] < 0 | sn[i] < 0 | sn[i] < sr[i])
                {
                    throw new InvalidDataException();
                }
                o[i, 2] = Math.Abs(xr[i]);
                o[i, 4] = Math.Abs(xn[i] - xr[i]);
                if (xr[i] < 0.0 | xn[i] < 0.0 | xn[i] < xr[i])
                {
                    throw new InvalidDataException();
                }
            }

            Mantel(host, true, k, out realk, o, out rmh, out ll, out ul, out x2, out sk, cit, ref cco, ref odr, ref odw, ref dswt, ref odrl, ref odru, ref odx, ref lerr, ref uerr, ref qc, ref bd, out dsor, out dsx2, out dsll, out dsul, ref cced, ref tausq, out ierr);
            if (ierr != 0)
            {
                if (ierr != 99)
                {
                    throw new InvalidDataException();
                }
                throw new TemplateOperationCancelledException();
            }

            // Try exact Mantel
            if (host.Preferences.MetaExact)
            {
                ExactBB.Rec2X2[] tbl = new ExactBB.Rec2X2[k + 1 /* for VB to C# conversion */];
                for (i = 1; i <= k; i++)
                {
                    tbl[i].Freq = 1;
                    tbl[i].A = o[i, 1];
                    tbl[i].M1 = o[i, 1] + o[i, 2];
                    tbl[i].N1 = o[i, 1] + o[i, 3];
                    tbl[i].N0 = o[i, 2] + o[i, 4];
                    tbl[i].Informative = (o[i, 1] * o[i, 4] != 0.0) | (o[i, 2] * o[i, 3] != 0.0);
                }
                bool useLogScale = false;
                new ExactBB().Exact22K(host, k, 1, tbl, cco, ref eor, out ulf, out llf, out ulm, out llm, out p1F, out p2F, out p1M, out p2M, ref useLogScale, out ierr);
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

            //  RTF_LoadTemplate("mantel.rtf")
            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("pc", Formatting.XRound(cco * 100, 2));

            IList<ParameterBag> inputsList = new List<ParameterBag>();
            outputParameters.AddOutput("*inputs", inputsList);
            for (i = 1; i <= k; i++)
            {
                ParameterBag inputsParameters = new ParameterBag();
                inputsList.Add(inputsParameters);
                inputsParameters.AddOutput("st", i.ToString());
                inputsParameters.AddOutput("a", o[i, 1].ToString());
                inputsParameters.AddOutput("b", o[i, 2].ToString());
                inputsParameters.AddOutput("c", o[i, 3].ToString());
                inputsParameters.AddOutput("d", o[i, 4].ToString());
                inputsParameters.AddOutput("lb", stratlab ? title[i] : "");
            }

            outputParameters.AddOutput("method", host.Preferences.MetaExact ? "CML" : "logit");

            IList<ParameterBag> orList = new List<ParameterBag>();
            outputParameters.AddOutput("*or", orList);
            for (i = 1; i <= k; i++)
            {
                ParameterBag orParameters = new ParameterBag();
                orList.Add(orParameters);
                orParameters.AddOutput("st", i.ToString());
                orParameters.AddOutput("or", host.RoundU(odr[i]));
                orParameters.AddOutput("standardized_effect", host.RoundU(odr[i] > 0 ? Math.Log(odr[i]) : 0));
                orParameters.AddOutput("se", "TODO: ?");
                orParameters.AddOutput("lci", host.RoundU(odrl[i]));
                orParameters.AddOutput("uci", host.RoundU(odru[i]));
                orParameters.AddOutput("wt", host.RoundU(100 * odw[i] / Formatting.dsum(odw, 1)));
                orParameters.AddOutput("dwt", host.RoundU(100 * dswt[i] / Formatting.dsum(dswt, 1)));
                string tmp = GetMetaLabel(host, o, i, stratlab, cced, title);
                if (host.Preferences.DelayContinuityCorrection)
                {
                    tmp = tmp.Replace("[CC", "[late CC");
                }
                orParameters.AddOutput("lb", tmp);
                if (host.Preferences.MetaExact & ((i) == Constant.MISSING || odru[i] == Constant.MISSING))
                {
                    OrciCorn(host, ref cco, ref o[i, 1], ref o[i, 2], ref o[i, 3], ref o[i, 4], out odr[i], out odrl[i], out odru[i]);
                    orParameters = new ParameterBag();
                    orList.Add(orParameters);
                    orParameters.AddOutput("st", "* " + i.ToString());
                    orParameters.AddOutput("or", "");
                    orParameters.AddOutput("standardized_effect", "");
                    orParameters.AddOutput("lci", host.RoundU(odrl[i]));
                    orParameters.AddOutput("uci", host.RoundU(odru[i]));
                    orParameters.AddOutput("wt", "");
                    orParameters.AddOutput("dwt", "");
                    orParameters.AddOutput("lb", " * [Cornfield limits]");
                }
            }

            if (sk == 0)
            {
                outputParameters.AddOutput("meth", "Sato");
                outputParameters.AddOutput("odds", "undefined");
                outputParameters.AddOutput("from", host.RoundU(ll));
                outputParameters.AddOutput("to", Formatting.INFRES);
            }
            else
            {
                outputParameters.AddOutput("meth", "Robins-Breslow-Greenland");
                outputParameters.AddOutput("odds", host.RoundU(rmh));
                outputParameters.AddOutput("from", host.RoundU(ll));
                outputParameters.AddOutput("to", host.RoundU(ul));
            }
            outputParameters.AddOutput("chi_mantel", host.RoundU(x2));
            outputParameters.AddOutput("chi_p", host.pval(PDF.chivalp(x2, 1.0)));

            IList<ParameterBag> cmlList = new List<ParameterBag>();
            outputParameters.AddOutput("*cml", cmlList);
            if (ierr != -9)
            {
                ParameterBag cmlParameters = new ParameterBag();
                cmlList.Add(cmlParameters);
                cmlParameters.AddOutput("eor", host.RoundU(eor));
                cmlParameters.AddOutput("llf", host.RoundU(llf));
                cmlParameters.AddOutput("ulf", host.RoundU(ulf));
                cmlParameters.AddOutput("p1f", host.pval(p1F));
                cmlParameters.AddOutput("p2f", host.pval(p2F));
                cmlParameters.AddOutput("llm", host.RoundU(llm));
                cmlParameters.AddOutput("ulm", host.RoundU(ulm));
                cmlParameters.AddOutput("p1m", host.pval(p1M));
                cmlParameters.AddOutput("p2m", host.pval(p2M));
            }

            outputParameters.AddOutput("bd", host.RoundU(bd));
            outputParameters.AddOutput("df", (realk - 1).ToString());
            outputParameters.AddOutput("xp", host.pval(PDF.chivalp(bd, Convert.ToDouble(realk - 1))));

            outputParameters.AddOutput("qc", host.RoundU(qc));
            outputParameters.AddOutput("df_cochran", (realk - 1).ToString());
            outputParameters.AddOutput("xp_cochran", host.pval(PDF.chivalp(qc, Convert.ToDouble(realk - 1))));
            outputParameters.AddOutput("tausq", host.RoundU(tausq));

            IsquareNcc(host, qc, realk, cco, cit, out isq, out llisq, out ulisq);
            outputParameters.AddOutput("isq", Formatting.XRound(isq, 1));
            outputParameters.AddOutput("pc1", Formatting.XRound(cco * 100, 1));
            outputParameters.AddOutput("llisq", Formatting.XRound(llisq, 1));
            outputParameters.AddOutput("ulisq", Formatting.XRound(ulisq, 1));

            outputParameters.AddOutput("dsor", host.RoundU(dsor));
            outputParameters.AddOutput("dsll", host.RoundU(dsll));
            outputParameters.AddOutput("dsul", host.RoundU(dsul));
            outputParameters.AddOutput("dsx2", host.RoundU(dsx2));
            outputParameters.AddOutput("df_ds", 1.ToString());
            outputParameters.AddOutput("xp_ds", host.pval(PDF.chivalp(dsx2, 1.0)));

            GetLogitCi(host, o, k, cit, axll, axul);

            IList<ParameterBag> eggerList = new List<ParameterBag>();
            outputParameters.AddOutput("*egger", eggerList);
            ParameterBag eggerParameters = new ParameterBag();
            eggerList.Add(eggerParameters);
            Metabias(host, eggerParameters, odr, axll, axul, k, ref cco, Transformation.Log);

            IList<ParameterBag> horboldList = new List<ParameterBag>();
            outputParameters.AddOutput("*horbold", horboldList);
            ParameterBag horboldParameters = new ParameterBag();
            horboldList.Add(horboldParameters);
            ModMetabias(host, horboldParameters, o, k, cco, 1);

            IList<ParameterBag> chartList = new List<ParameterBag>();
            outputParameters.AddOutput("*chart", chartList);
            ParameterBag chartParameters;

            if (k > 3)
            {
                using (ChartRenderer ch = new ChartRenderer(ChartDefinition.Empty()))
                {
                    string rtf = ch.PlotBiasMAAndReturnRtf(host, odr, odx, odw, k, "Odds ratio", axll, axul, cco, cit, rmh, Transformation.Log, false);
                    chartParameters = new ParameterBag();
                    chartList.Add(chartParameters);
                    chartParameters.AddOutput("chart", rtf);
                }
            }

            using (ChartRenderer ch = new ChartRenderer(ChartDefinition.Empty()))
            {
                string rtf = ch.PlotLAbbeAndReturnRtf(host, k, o, rmh);
                chartParameters = new ParameterBag();
                chartList.Add(chartParameters);
                chartParameters.AddOutput("chart", rtf);
            }

            if (sk != 0)
            {
                bool fault;

                using (ChartRenderer ch = new ChartRenderer(ChartDefinition.Empty()))
                {
                    string rtf = ch.PlotMHAndReturnRtf(host, k, o, odw, title, rmh, ll, ul, cco, odr, odrl, odru, lerr, uerr, "Odds ratio meta-analysis plot [fixed effects]", 1, "odds ratio", out fault, null);
                    chartParameters = new ParameterBag();
                    chartList.Add(chartParameters);
                    chartParameters.AddOutput("chart", rtf);
                }

                using (ChartRenderer ch = new ChartRenderer(ChartDefinition.Empty()))
                {
                    string rtf = ch.PlotMHAndReturnRtf(host, k, o, dswt, title, dsor, dsll, dsul, cco, odr, odrl, odru, lerr, uerr, "Odds ratio meta-analysis plot [random effects]", 1, "odds ratio", out fault, null);
                    chartParameters = new ParameterBag();
                    chartList.Add(chartParameters);
                    chartParameters.AddOutput("chart", rtf);
                }
            }
            return new StepResult(StepSuccess.Success, outputParameters);
        }


        public static void Mantel(ITemplateHost host, bool fromSheet, int k, out int realk, double[,] o, out double rmh, out double ll, out double ul, out double x2, out double sk, double cit, ref double cco, ref double[] odr, ref double[] odw, ref double[] dswt, ref double[] odrl, ref double[] odru, ref double[] odx, ref bool[] lerr, ref bool[] uerr, ref double qc, ref double bd, out double dsor, out double dsx2, out double dsll, out double dsul, ref bool[] cced, ref double tausq, out int ierr)
        {
            double lori; double vrbgi;
            double weight; double n; double a; double b; double c; double d;
            double pp; double qq; double ss; double rr;
            int i;

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
            for (i = 1; i <= k; i++)
            {
                if (o[i, 1] * o[i, 4] != 0.0)
                {
                    rkok = true;
                }
            }

            for (i = 1; i <= k; i++)
            {
                a = o[i, 1];
                b = o[i, 2];
                c = o[i, 3];
                d = o[i, 4];
                n = a + b + c + d;
                odx[i] = n;
                if (n <= 0)
                {
                    throw new InvalidDataException();
                }

                if (host.Preferences.MetaExact)
                {
                    double eor = 0;
                    OrciCmle(host, cco, a, b, c, d, odr[i], ref eor, out odrl[i], out odru[i], out lerr[i], out uerr[i]);
                }

                if (host.Preferences.DelayContinuityCorrection == false)
                {
                    rkok = false;
                }

                // MH across strata
                if (IncludeTable(o, i))
                {
                    // only do cc at this stage if absolutely necessary (all a or all d cells zero)
                    if (rkok == false)
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
                    realk = realk + 1;
                    eai = eai + a - (a + c) * (a + b) / n;
                    vari = vari + ((a + c) * (b + d) * (a + b) * (c + d)) / (Math.Pow(n, 2.0) * (n - 1.0));
                    rr = a * d / n;
                    ss = b * c / n;
                    pp = (a + d) / n;
                    qq = (b + c) / n;
                    w = w + (qq + 1.0 / n) * rr + (pp + 1.0 / n) * ss;
                    rk = rk + rr;
                    sk = sk + ss;
                    svd1 = svd1 + pp * rr;
                    svd2 = svd2 + (qq * rr + pp * ss);
                    svd3 = svd3 + qq * ss;
                    weight = b * c / n;
                    odw[i] = weight;
                    sumwt = sumwt + weight;
                }

                if (IncludeTable(o, i) == false)
                {
                    odr[i] = Constant.MISSING;
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
                        if (a <= 0 | b <= 0 | c <= 0 | d <= 0)
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
                    odr[i] = (a * d) / (b * c);
                    if (!(host.Preferences.MetaExact))
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
                ul = (2.0 * rk * sk + cit * cit * w + sq) / 2.0 / rk / rk;
                ll = Math.Exp(1.0 / ul);
                ul = Constant.MISSING;
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
            {
                Utilities.Utilities.Swap(ref ll, ref ul);
            }
            x2 = Math.Pow((Math.Abs(eai) - 0.5), 2.0) / vari;

            // Q (combinability)
            qc = 0.0;
            bd = 0.0;
            double wlor = 0.0;
            sumwt = 0.0;
            double sumsqwt = 0.0;
            for (i = 1; i <= k; i++)
            {
                a = o[i, 1];
                b = o[i, 2];
                c = o[i, 3];
                d = o[i, 4];
                // Breslow-Day - must do it before continuity correction -->
                double n1 = a + b;
                double n0 = c + d;
                double m1 = a + c;
                double m2 = b + d;
                if (n1 != 0 & n0 != 0 & m1 != 0 & m2 != 0)
                {
                    // quadratic coefficients ax² + bx + c = 0
                    double qda = 1.0 - rmh;
                    double qdb = m2 - n1 + (m1 + n1) * rmh;
                    double qdc = -m1 * n1 * rmh;
                    double ea = (-qdb + Math.Sqrt((Math.Pow(qdb, 2.0)) - 4.0 * qda * qdc)) / (2.0 * qda);
                    // give the rest of the expected table e.g. Breslow & Day P 144
                    double varea = 1.0 / (1.0 / ea + 1.0 / (n1 - ea) + 1.0 / (m1 - ea) + 1.0 / (n0 - m1 + ea));
                    bd = bd + (a - ea) * (a - ea) / varea;
                }
                // <--
                if (IncludeTable(o, i))
                {
                    if (a <= 0 | b <= 0 | c <= 0 | d <= 0)
                    {
                        ContinuityCorrect(host, a, b, c, d, out a, out b, out c, out d);
                    }
                    n = a + b + c + d;
                    rr = a * d / n;
                    ss = b * c / n;
                    pp = (a + d) / n;
                    qq = (b + c) / n;
                    svd1 = pp * rr;
                    svd2 = qq * rr + pp * ss;
                    svd3 = qq * ss;
                    vrbgi = svd1 / 2.0 / rr / rr + svd2 / 2.0 / rr / ss + svd3 / 2.0 / ss / ss;
                    weight = 1.0 / vrbgi;
                    lori = Math.Log((a * d) / (b * c));
                    qc += weight * Math.Pow((lori - Math.Log(rmh)), 2.0);
                    wlor += lori * weight;
                    sumwt += weight;
                    sumsqwt += weight * weight;
                }
            }
            // DerSimonian-Laird
            if ((sumwt * sumwt - sumsqwt) == 0.0)
            {
                tausq = 0.0;
            }
            else
            {
                tausq = ((qc - Convert.ToDouble(realk - 1)) * sumwt) / (sumwt * sumwt - sumsqwt);
            }
            if (tausq < 0.0)
            {
                tausq = 0.0;
            }
            wlor = 0.0;
            sumwt = 0.0;
            for (i = 1; i <= k; i++)
            {
                a = o[i, 1];
                b = o[i, 2];
                c = o[i, 3];
                d = o[i, 4];
                if (IncludeTable(o, i))
                {
                    if (a <= 0.0 || b <= 0.0 || c <= 0.0 || d <= 0.0)
                    {
                        ContinuityCorrect(host, a, b, c, d, out a, out b, out c, out d);
                    }
                    n = a + b + c + d;
                    rr = a * d / n;
                    ss = b * c / n;
                    pp = (a + d) / n;
                    qq = (b + c) / n;
                    svd1 = pp * rr;
                    svd2 = qq * rr + pp * ss;
                    svd3 = qq * ss;
                    vrbgi = svd1 / 2.0 / rr / rr + svd2 / 2.0 / rr / ss + svd3 / 2.0 / ss / ss;
                    weight = 1.0 / (tausq + vrbgi);
                    dswt[i] = weight;
                    lori = Math.Log((a * d) / (b * c));
                    wlor += lori * weight;
                    sumwt += weight;
                }
            }
            dsor = Math.Exp(wlor / sumwt);
            dsx2 = Math.Pow(wlor, 2.0) / sumwt;
            dsll = Math.Exp(wlor / sumwt - cit / Math.Sqrt(sumwt));
            dsul = Math.Exp(wlor / sumwt + cit / Math.Sqrt(sumwt));
            if (dsll > dsul)
            {
                Utilities.Utilities.Swap(ref dsll, ref dsul);
            }
            ierr = 0;
        }


        public static void GetLogitCi(ITemplateHost host, double[,] o, int k, double cit, double[] axll, double[] axul)
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
                    double odr = (a * d) / (b * c);
                    double se = Math.Sqrt(1.0 / a + 1.0 / b + 1.0 / c + 1.0 / d);
                    axll[i] = Math.Exp(Math.Log(odr) - cit * se);
                    axul[i] = Math.Exp(Math.Log(odr) + cit * se);
                }
            }
        }


        public static void GetAproxrrCI(ITemplateHost host, double[,] o, int k, double cit, double[] axll, double[] axul)
        {
            for (int i = 1; i <= k; i++)
            {
                double a = o[i, 1];
                double b = o[i, 2];
                double c = o[i, 3];
                double d = o[i, 4];
                if (IncludeTable(o, i))
                {
                    if (a <= 0.0 || b <= 0.0 || c <= 0.0 || d <= 0.0)
                    {
                        ContinuityCorrect(host, a, b, c, d, out a, out b, out c, out d);
                    }
                    double rkr = (a / (a + c)) / (b / (b + d));
                    // approximate se of log rr
                    double se = Math.Sqrt(1.0 / a + 1.0 / b - 1.0 / (a + c) - 1.0 / (b + d));
                    axll[i] = Math.Exp(Math.Log(rkr) - se * cit);
                    axul[i] = Math.Exp(Math.Log(rkr) + se * cit);
                }
            }
        }


        private static void OrciCmle(ITemplateHost host, double cco, double a, double b, double c, double d, double odr, ref double eor, out double llf, out double ulf, out bool lerr, out bool uerr)
        {
            if ((((a == 0.0) && (b == 0.0)) || ((a == (a + c)) && (b == (b + d)))))
            {
                ulf = double.PositiveInfinity;
                llf = 0.0;
                eor = Constant.MISSING;
                /* Unused
                p1f = Constant.MISSING; 
                p2f = Constant.MISSING; 
                p1m = Constant.MISSING; 
                p2m = Constant.MISSING; 
                 */
            }
            else
            {
                if ((a * d != 0) || (b * c != 0))
                {
                    ExactBB.Rec2X2[] tabl = new ExactBB.Rec2X2[1 + 1 /* for VB to C# conversion */];
                    tabl[1].Freq = 1;
                    tabl[1].A = a;
                    tabl[1].M1 = a + b;
                    tabl[1].N1 = a + c;
                    tabl[1].N0 = b + d;
                    tabl[1].Informative = (a * d != 0) || (b * c != 0);
                    bool useLogScale = false;
                    double ulm;
                    int ierr;
                    double llm;
                    double p1M; double p2M; double p1F; double p2F;
                    new ExactBB().Exact22K(host, 1, 1, tabl, cco, ref eor, out ulf, out llf, out ulm, out llm, out p1F, out p2F, out p1M, out p2M, ref useLogScale, out ierr);
                }
                else
                {
                    eor = Constant.MISSING;
                    llf = Constant.MISSING;
                    ulf = Constant.MISSING;
                    /* unused
                    p1f = Constant.MISSING; 
                    p2f = Constant.MISSING; 
                    p1m = Constant.MISSING; 
                    p2m = Constant.MISSING; 
                     */
                }
            }
            lerr = (llf == Constant.MISSING);
            uerr = (ulf == Constant.MISSING);
        }


        private static void IrdMeta(int k, double[] a, double[] b, double[] pt1, double[] pt2, out double rmh, out double ll, out double ul, out double zrmh, ref double cit, ref double cco, double[] rkr, double[] rkw, double[] dsw, double[] rkrl, double[] rkru, bool[] lerr, bool[] uerr, out double qc, out double dsrd, out double dz, out double dsll, out double dsul, out double realk, out double tausq, double[] standardizedEffect, out double se, out int ierr)
        {
            ierr = -1;
            double sumwt = 0.0;
            double sumwi = 0.0;
            realk = 0.0;
            for (int i = 1; i <= k; i++)
            {
                // ird and ci for stratum
                double pt = pt1[i] + pt2[i];
                double m = a[i] + b[i];
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
                    realk = realk + 1.0;
                    double ir1 = a[i] / pt1[i];
                    double ir2 = b[i] / pt2[i];
                    double ird = ir1 - ir2;
                    double xmh = ((a[i] - (m * pt1[i]) / pt) * (a[i] - (m * pt1[i]) / pt)) / ((m * pt1[i] * pt2[i]) / (pt * pt));
                    if (xmh == 0)
                    {
                        rkrl[i] = Constant.MISSING;
                        lerr[i] = true;
                    }
                    else
                    {
                        rkrl[i] = ird - cit * Math.Sqrt((ird * ird) / xmh);
                    }
                    if (xmh == 0)
                    {
                        rkru[i] = Constant.MISSING;
                        uerr[i] = true;
                    }
                    else
                    {
                        rkru[i] = ird + cit * Math.Sqrt((ird * ird) / xmh);
                    }
                    rkr[i] = ird;
                    standardizedEffect[i] = ird;
                    //  pooled incidence risk difference
                    double vark = a[i] / (pt1[i] * pt1[i]) + b[i] / (pt2[i] * pt2[i]);
                    rkw[i] = 1.0 / vark;
                    sumwt += rkw[i];
                    sumwi += rkw[i] * ird;
                }
            }
            rmh = sumwi / sumwt;
            se = Math.Sqrt(1.0 / sumwt);
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
                    qc += rkw[i] * Math.Pow((rkr[i] - rmh), 2.0);
                    sumwt += rkw[i];
                    sumsqwt += rkw[i] * rkw[i];
                }
            }

            // DerSimonian-Laird random effects
            if ((sumwt - sumsqwt / sumwt) == 0.0)
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

        private static void IrrMeta(int k, double[] a, double[] b, double[] pt1, double[] pt2, out double rmh, out double ll, out double ul, out double zrmh, ref double cit, ref double cco, double[] rkr, double[] rkw, double[] dsw, double[] rkrl, double[] rkru, bool[] lerr, bool[] uerr, out double qc, out double dsirr, out double dz, out double dsll, out double dsul, out double realk, out double tausq, double[] standardizedEffect, out double se, out int ierr)
        {
            ierr = -1;
            double sumwt = 0.0;
            double sumwi = 0.0;
            realk = 0.0;
            for (int i = 1; i <= k; i++)
            {
                // irr and ci for stratum
                // double pt = pt1[ i ] + pt2[ i ]; - unused
                // double M = a[ i ] + b[ i ]; - unused
                if (a[i] + b[i] <= 0.0 | b[i] <= 0.0 | pt1[i] <= 0.0 | pt2[i] <= 0.0)
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
                    realk = realk + 1.0;
                    double ir1 = a[i] / pt1[i];
                    double ir2 = b[i] / pt2[i];
                    double p = cco + (1.0 - cco) / 2.0;
                    double f;
                    if (a[i] == 0.0)
                    {
                        rkrl[i] = 0.0;
                    }
                    else
                    {
                        f = PDF.ffromp(2.0 * a[i], 2.0 * (b[i] + 1.0), 1.0 - p);
                        rkrl[i] = (pt2[i] / pt1[i]) * (a[i] / (b[i] + 1.0)) * (1.0 / f);
                    }
                    if (b[i] == 0.0)
                    {
                        rkru[i] = Constant.MISSING;
                        rkr[i] = Constant.MISSING;
                    }
                    else
                    {
                        f = PDF.ffromp(2.0 * b[i], 2.0 * (a[i] + 1.0), 1.0 - p);
                        rkru[i] = (pt2[i] / pt1[i]) * ((a[i] + 1.0) / b[i]) * f;
                        rkr[i] = ir1 / ir2;
                        if (rkr[i] > 0)
                        standardizedEffect[i] = Math.Log(rkr[i]);
                    }
                    // pooled incidence rate ratio
                    // vark = 1# / a(i) + 1# / b(i) - as expressed in Lau paper on AZT
                    rkw[i] = (a[i] * b[i]) / (a[i] + b[i]);
                    sumwt += rkw[i];
                    if (rkr[i] > 0)
                    {
                        sumwi += rkw[i] * Math.Log(rkr[i]);
                    }
                }
            }
            rmh = Math.Exp(sumwi / sumwt);
            se = Math.Sqrt(1.0 / sumwt);
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
                    {
                        qc += rkw[i] * Math.Pow((Math.Log(rkr[i]) - Math.Log(rmh)), 2.0);
                    }
                    sumwt = sumwt + rkw[i];
                    sumsqwt = sumsqwt + rkw[i] * rkw[i];
                }
            }
            // DerSimonian-Laird random effects
            if ((sumwt - sumsqwt / sumwt) == 0.0)
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
            // sumsqwt = 0.0; - unused
            for (int i = 1; i <= k; i++)
            {
                if (rkw[i] != Constant.MISSING)
                {
                    double weight = 1.0 / (tausq + 1.0 / rkw[i]);
                    dsw[i] = weight;
                    if (rkr[i] > 0)
                    {
                        wrd += Math.Log(rkr[i]) * weight;
                    }
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


        public static StepResult RptMetaSummary(ITemplateHost host, ParameterBag parameters)
        {
            double dsul; double dsll; double dsrr;
            double tausq;
            double zrmh; double ulrmh; double llrmh;
            double cit;
            int i;
            double isq; double llisq; double ulisq;
            bool stratlab;
            double[] odx = null;

            double cco = parameters["gamma"].AsDouble;
            if (cco > 0)
            {
                double p = (1.0 - cco) / 2.0;
                int scrap;
                cit = PDF.gauinv(1.0 - p, out scrap);
            }
            else
            {
                cco = 0.95;
                int scrap;
                cit = PDF.gauinv(0.975, out scrap);
            }

            string statx = parameters["statx"].AsString;
            bool useRatio = parameters["use_ratio"].AsBoolean;
            string stat = parameters["stat_in"].AsString;
            bool useCI = "true".Equals(parameters["use_ci"].AsString.ToLower());

            DataFrame yFrame = parameters["y"].AsDataFrame;
            DoubleVariable yVariable = yFrame.Variables[0].AsDoubleVariable;
            int k = yVariable.Length;
            double[] y = new double[k + 2];
            double[] seY = new double[k + 2];
            double[] llY = new double[k + 2];
            double[] ulY = new double[k + 2];
            string[] title = new string[k + 2];
            int[] pg = new int[k + 2];
            for (i = 1; i <= k; i++)
            {
                y[i] = yVariable.Data[i - 1];
                pg[i] = 0;
                if (useRatio)
                {
                    if (y[i] <= 0.0)
                    {
                        throw new InvalidDataException();
                    }
                }
            }
            // pooled indicator for last element - needed by plot_cp
            pg[k + 1] = -1;

            if (useCI)
            {
                DataFrame llYFrame = parameters["ll_y"].AsDataFrame;
                DoubleVariable llYVariable = llYFrame.Variables[0].AsDoubleVariable;
                for (i = 1; i <= k; i++)
                {
                    llY[i] = llYVariable.Data[i - 1];
                }

                DataFrame ulYFrame = parameters["ul_y"].AsDataFrame;
                DoubleVariable ulYVariable = ulYFrame.Variables[0].AsDoubleVariable;
                for (i = 1; i <= k; i++)
                {
                    ulY[i] = ulYVariable.Data[i - 1];
                }

                for (i = 1; i <= k; i++)
                {
                    if (llY[i] == Constant.MISSING)
                    {
                        llY[i] = 0.0;
                    }
                    if (ulY[i] == Constant.MISSING)
                    {
                        if (useRatio)
                        {
                            ulY[i] = 1.0;
                        }
                        else
                        {
                            throw new InvalidDataException();
                        }
                    }
                    if (llY[i] > ulY[i])
                    {
                        Utilities.Utilities.Swap(ref llY[i], ref ulY[i]);
                    }
                    if (useRatio)
                    {
                        seY[i] = ((Math.Log(ulY[i]) - Math.Log(llY[i])) / 2.0) / cit;
                    }
                    else
                    {
                        seY[i] = ((ulY[i] - llY[i]) / 2.0) / cit;
                    }
                }
            }
            else
            {
                // tmp = use_ratio ? "LOG " : ""; - unused
                DataFrame seYFrame = parameters["se_y"].AsDataFrame;
                DoubleVariable seYVariable = seYFrame.Variables[0].AsDoubleVariable;
                for (i = 1; i <= k; i++)
                {
                    seY[i] = seYVariable.Data[i - 1];
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

            if (parameters.ContainsKey("studies") && parameters["studies"].Data != null)
            {
                stratlab = true;
                DataFrame strataFrame = parameters["studies"].AsDataFrame;
                StringVariable strataVariable = strataFrame.Variables[0].AsStringVariable;
                for (i = 1; i <= k; i++)
                {
                    string buf = strataVariable.Data[i - 1].Trim();
                    if (buf.Length > 0)
                    {
                        if (buf.Length > 50)
                        {
                            buf = buf.Substring(0, 50);
                        }
                        title[i] = buf;
                    }
                    else
                    {
                        title[i] = "study " + i.ToString();
                    }
                }
            }
            else
            {
                stratlab = false;
                for (i = 1; i <= k; i++)
                {
                    title[i] = "study " + i.ToString();
                }
            }
            title[k + 1] = ChartRenderer.combo_ti("");

            // Pool
            double sumwt = 0.0;
            double sumsqwt = 0.0;
            double sumywt = 0.0;
            double[] wt = new double[k + 2];
            double[] dswt = new double[k + 2];
            for (i = 1; i <= k; i++)
            {
                if (seY[i] == 0.0)
                {
                    throw new InvalidDataException();
                }
                wt[i] = 1.0 / (seY[i] * seY[i]);
                sumwt = sumwt + wt[i];
                sumsqwt = sumsqwt + wt[i] * wt[i];
                if (useRatio)
                {
                    sumywt = sumywt + Math.Log(y[i]) * wt[i];
                }
                else
                {
                    sumywt = sumywt + y[i] * wt[i];
                }
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
                {
                    Utilities.Utilities.Swap(ref llrmh, ref ulrmh);
                }
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
            for (i = 1; i <= k; i++)
            {
                if (useRatio)
                {
                    qc = qc + wt[i] * Math.Pow((Math.Log(y[i]) - Math.Log(rmh)), 2.0);
                }
                else
                {
                    qc = qc + wt[i] * Math.Pow((y[i] - rmh), 2.0);
                }
            }

            // DerSimonian-Laird random effects
            if ((sumwt - sumsqwt / sumwt) == 0.0)
            {
                tausq = 0.0;
            }
            else
            {
                tausq = (qc - Convert.ToDouble(k - 1)) / (sumwt - sumsqwt / sumwt);
            }
            if (tausq < 0.0)
            {
                tausq = 0.0;
            }
            double wlrr = 0.0;
            sumwt = 0.0;
            // sumsqwt = 0.0; - unused
            for (i = 1; i <= k; i++)
            {
                double weight = 1.0 / (tausq + 1.0 / wt[i]);
                dswt[i] = weight;
                sumwt += weight;
                if (useRatio)
                {
                    wlrr += Math.Log(y[i]) * weight;
                }
                else
                {
                    wlrr += y[i] * weight;
                }
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

            //  RTF_LoadTemplate("genmeta.rtf")
            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("stat", stat);
            outputParameters.AddOutput("pc", Formatting.XRound(cco * 100, 2));

            IList<ParameterBag> studiesList = new List<ParameterBag>();
            outputParameters.AddOutput("*studies", studiesList);
            for (i = 1; i <= k; i++)
            {
                ParameterBag studiesParameters = new ParameterBag();
                studiesList.Add(studiesParameters);
                studiesParameters.AddOutput("st", i.ToString());
                studiesParameters.AddOutput("y", host.RoundU(y[i]));
                studiesParameters.AddOutput("se", host.RoundU(seY[i]));
                studiesParameters.AddOutput("from", host.RoundU(llY[i]));
                studiesParameters.AddOutput("to", host.RoundU(ulY[i]));
                studiesParameters.AddOutput("wt", host.RoundU(100 * wt[i] / Formatting.dsum(wt, 1)));
                studiesParameters.AddOutput("dwt", host.RoundU(100 * dswt[i] / Formatting.dsum(dswt, 1)));
                studiesParameters.AddOutput("standardized_effect", host.RoundU(y[i])); // TODO: Correct?  Is re-using se correct?
                studiesParameters.AddOutput("lb", stratlab ? title[i] : "");
            }

            outputParameters.AddOutput("stat_fixed", stat.ToLower());
            outputParameters.AddOutput("rmh", host.RoundU(rmh));
            outputParameters.AddOutput("from_fixed", host.RoundU(llrmh));
            outputParameters.AddOutput("to_fixed", host.RoundU(ulrmh));

            outputParameters.AddOutput("task", "test " + stat + " " + statx.ToLower());
            outputParameters.AddOutput("z", host.RoundU(zrmh));
            outputParameters.AddOutput("p_fixed", host.pval(MathDbl.zvalp2(zrmh)));

            outputParameters.AddOutput("qc", host.RoundU(qc));
            outputParameters.AddOutput("df", (k - 1).ToString());
            outputParameters.AddOutput("xp", host.pval(PDF.chivalp(qc, Convert.ToDouble(k - 1))));
            outputParameters.AddOutput("tausq", host.RoundU(tausq));
            IsquareNcc(host, qc, k, cco, cit, out isq, out llisq, out ulisq);
            outputParameters.AddOutput("isq", Formatting.XRound(isq, 1));
            outputParameters.AddOutput("pc1", Formatting.XRound(cco * 100, 1));
            outputParameters.AddOutput("llisq", Formatting.XRound(llisq, 1));
            outputParameters.AddOutput("ulisq", Formatting.XRound(ulisq, 1));

            outputParameters.AddOutput("dsstat", stat.ToLower());
            outputParameters.AddOutput("dsrr", host.RoundU(dsrr));
            outputParameters.AddOutput("dsll", host.RoundU(dsll));
            outputParameters.AddOutput("dsul", host.RoundU(dsul));

            outputParameters.AddOutput("zstat", "test " + stat + statx.ToLower());
            outputParameters.AddOutput("dz", host.RoundU(dsz));
            outputParameters.AddOutput("dp", host.pval(MathDbl.zvalp2(dsz)));

            IList<ParameterBag> biasList = new List<ParameterBag>();
            outputParameters.AddOutput("*bias", biasList);
            ParameterBag biasParameters = new ParameterBag();
            biasList.Add(biasParameters);
            Transformation xform = Transformation.None;
            if (useRatio)
            {
                xform = Transformation.Log;
            }
            Metabias(host, biasParameters, y, llY, ulY, k, ref cco, xform);

            IList<ParameterBag> chartList = new List<ParameterBag>();
            outputParameters.AddOutput("*chart", chartList);
            ParameterBag chartParameters;

            if (k > 3)
            {
                using (ChartRenderer ch = new ChartRenderer(ChartDefinition.Empty()))
                {
                    string rtf = ch.PlotBiasMAAndReturnRtf(host, y, odx, wt, k, stat.ToLower(), llY, ulY, cco, cit, rmh, xform, false);
                    chartParameters = new ParameterBag();
                    chartList.Add(chartParameters);
                    chartParameters.AddOutput("chart", rtf);
                }
            }

            y[k + 1] = rmh;
            llY[k + 1] = llrmh;
            ulY[k + 1] = ulrmh;
            using (ChartRenderer ch = new ChartRenderer(ChartDefinition.Empty()))
            {
                string rtf = ch.PlotCPAndReturnRtf(host, k + 1, title, y, llY, ulY, wt, pg, "Summary meta-analysis plot [fixed effects]", stat.ToLower() + " (" + Formatting.XRound(cco * 100, 1) + "% confidence interval" + ")", xform);
                chartParameters = new ParameterBag();
                chartList.Add(chartParameters);
                chartParameters.AddOutput("chart", rtf);
            }

            y[k + 1] = dsrr;
            llY[k + 1] = dsll;
            ulY[k + 1] = dsul;
            using (ChartRenderer ch = new ChartRenderer(ChartDefinition.Empty()))
            {
                string rtf = ch.PlotCPAndReturnRtf(host, k + 1, title, y, llY, ulY, dswt, pg, "Summary meta-analysis plot [random effects]", stat.ToLower() + " (" + Formatting.XRound(cco * 100, 1) + "% confidence interval" + ")", xform);
                chartParameters = new ParameterBag();
                chartList.Add(chartParameters);
                chartParameters.AddOutput("chart", rtf);
            }

            return new StepResult(StepSuccess.Success, outputParameters);
        }

        public static StepResult RptMetaCorrelation(ITemplateHost host, ParameterBag parameters)
        {
            double tausq;
            double cit;
            int i;
            double isq; double llisq; double ulisq;
            bool stratlab;
            double[] odx = null;

            double cco = parameters["gamma"].AsDouble;
            if (cco > 0)
            {
                double p = (1.0 - cco) / 2.0;
                int scrap;
                cit = PDF.gauinv(1.0 - p, out scrap);
            }
            else
            {
                cco = 0.95;
                int scrap;
                cit = PDF.gauinv(0.975, out scrap);
            }

            DataFrame rFrame = parameters["r"].AsDataFrame;
            DoubleVariable rVariable = rFrame.Variables[0].AsDoubleVariable; //  Ends up in y
            int k = rVariable.Length;
            double[] y = new double[k + 1 + 1 /* for VB to C# conversion */ ];
            // double[] n = new double[k + 1 + 1 /* for VB to C# conversion */ ]; - unused
            string[] title = new string[k + 1 + 1 /* for VB to C# conversion */ ];
            int[] pg = new int[k + 1 + 1 /* for VB to C# conversion */ ];
            for (i = 1; i <= k; i++)
            {
                y[i] = rVariable.Data[i - 1];
                pg[i] = 0;
                if (y[i] < -1.0 || y[i] > 1.0)
                {
                    throw new Exception("r(" + i + ") must be between -1 and 1");
                }
            }
            // pooled indicator for last element - needed by plot_cp
            pg[k + 1] = -1;

            DataFrame nFrame = parameters["n"].AsDataFrame;
            DoubleVariable nVariable = nFrame.Variables[0].AsDoubleVariable;
            double[] seY = new double[k + 1 + 1 /* for VB to C# conversion */ ];
            double[] llY = new double[k + 1 + 1 /* for VB to C# conversion */ ];
            double[] ulY = new double[k + 1 + 1 /* for VB to C# conversion */ ];
            double[] ss = new double[k + 1 + 1 /* for VB to C# conversion */ ];
            for (i = 1; i <= k; i++)
            {
                double sampleSize = nVariable.Data[i - 1];
                if (sampleSize < 3)
                {
                    throw new Exception("All values of n must be at least 3. n(" + i + ") is less than 3");
                }
                ss[i] = sampleSize;
                seY[i] = Math.Sqrt(1 / (sampleSize - 3));
                llY[i] = MathDbl.ztor(MathDbl.rtoz(y[i]) - cit * seY[i]);
                ulY[i] = MathDbl.ztor(MathDbl.rtoz(y[i]) + cit * seY[i]);
            }

            if (parameters.ContainsKey("studies") && parameters["studies"].Data != null)
            {
                stratlab = true;
                DataFrame strataFrame = parameters["studies"].AsDataFrame;
                StringVariable strataVariable = strataFrame.Variables[0].AsStringVariable;
                for (i = 1; i <= k; i++)
                {
                    string buf = strataVariable.Data[i - 1].Trim();
                    if (buf.Length > 0)
                    {
                        if (buf.Length > 50)
                        {
                            buf = buf.Substring(0, 50);
                        }
                        title[i] = buf;
                    }
                    else
                    {
                        title[i] = "study " + i.ToString();
                    }
                }
            }
            else
            {
                stratlab = false;
                for (i = 1; i <= k; i++)
                {
                    title[i] = "study " + i.ToString();
                }
            }
            title[k + 1] = ChartRenderer.combo_ti("");

            // Pool
            double sumwt = 0.0;
            double sumsqwt = 0.0;
            double sumywt = 0.0;
            double[] wt = new double[k + 1 + 1 /* for VB to C# conversion */ ];
            double[] dswt = new double[k + 1 + 1 /* for VB to C# conversion */ ];
            for (i = 1; i <= k; i++)
            {
                if (seY[i] == 0.0)
                {
                    throw new InvalidDataException();
                }
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
            {
                Utilities.Utilities.Swap(ref llrmh, ref ulrmh);
            }
            double zrmh = MathDbl.rtoz(rmh) / sermh;

            // Q (combinability)
            double qc = 0.0;
            for (i = 1; i <= k; i++)
            {
                qc += wt[i] * Math.Pow((MathDbl.rtoz(y[i]) - MathDbl.rtoz(rmh)), 2.0);
            }

            // DerSimonian-Laird random effects
            if ((sumwt - sumsqwt / sumwt) == 0.0)
            {
                tausq = 0.0;
            }
            else
            {
                tausq = (qc - Convert.ToDouble(k - 1)) / (sumwt - sumsqwt / sumwt);
            }
            if (tausq < 0.0)
            {
                tausq = 0.0;
            }
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
            {
                Utilities.Utilities.Swap(ref dsll, ref dsul);
            }

            // Schmidt-Hunter
            double varR = 0, wmrCrll, wmrCrul;
            double wmr = 0.0;
            double tot = 0.0;
            for (i = 1; i <= k; i++)
            {
                tot += ss[i];
            }
            for (i = 1; i <= k; i++)
            {
                wmr += y[i] * ss[i];
            }
            wmr /= tot;
            for (i = 1; i <= k; i++)
            {
                varR += ss[i] * Math.Pow((y[i] - wmr), 2.0);
            }
            varR /= tot;
            double varE = (Math.Pow((1.0 - Math.Pow(wmr, 2.0)), 2.0)) / ((tot / Convert.ToDouble(k)) - 1.0);
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
            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("stat", stat);
            outputParameters.AddOutput("pc", Formatting.XRound(cco * 100, 2));

            IList<ParameterBag> studiesList = new List<ParameterBag>();
            outputParameters.AddOutput("*studies", studiesList);
            for (i = 1; i <= k; i++)
            {
                ParameterBag studiesParameters = new ParameterBag();
                studiesList.Add(studiesParameters);
                studiesParameters.AddOutput("st", i.ToString());
                studiesParameters.AddOutput("n", host.RoundU(ss[i]));
                studiesParameters.AddOutput("y", host.RoundU(y[i]));
                studiesParameters.AddOutput("from", host.RoundU(llY[i]));
                studiesParameters.AddOutput("to", host.RoundU(ulY[i]));
                studiesParameters.AddOutput("wt", host.RoundU(100 * wt[i] / Formatting.dsum(wt, 1)));
                studiesParameters.AddOutput("dwt", host.RoundU(100 * dswt[i] / Formatting.dsum(dswt, 1)));
                studiesParameters.AddOutput("nwt", host.RoundU(100 * ss[i] / Formatting.dsum(ss, 1)));
                studiesParameters.AddOutput("standardized_effect", host.RoundU(y[i])); // TODO: Correct?
                studiesParameters.AddOutput("se", host.RoundU(Math.Sqrt(sumwt))); // TODO: Correct?
                studiesParameters.AddOutput("lb", stratlab ? title[i] : "");
            }

            outputParameters.AddOutput("stat_fixed", stat.ToLower());
            outputParameters.AddOutput("rmh", host.RoundU(rmh));
            outputParameters.AddOutput("from_fixed", host.RoundU(llrmh));
            outputParameters.AddOutput("to_fixed", host.RoundU(ulrmh));

            outputParameters.AddOutput("z", host.RoundU(zrmh));
            outputParameters.AddOutput("p_fixed", host.pval(MathDbl.zvalp2(zrmh)));

            outputParameters.AddOutput("qc", host.RoundU(qc));
            outputParameters.AddOutput("df", (k - 1).ToString());
            outputParameters.AddOutput("xp", host.pval(PDF.chivalp(qc, Convert.ToDouble(k - 1))));
            outputParameters.AddOutput("tausq", host.RoundU(tausq));

            IsquareNcc(host, qc, k, cco, cit, out isq, out llisq, out ulisq);
            outputParameters.AddOutput("isq", Formatting.XRound(isq, 1));
            outputParameters.AddOutput("pc1", Formatting.XRound(cco * 100, 1));
            outputParameters.AddOutput("llisq", Formatting.XRound(llisq, 1));
            outputParameters.AddOutput("ulisq", Formatting.XRound(ulisq, 1));

            outputParameters.AddOutput("dsstat", stat.ToLower());
            outputParameters.AddOutput("dsrr", host.RoundU(dsrr));
            outputParameters.AddOutput("dsll", host.RoundU(dsll));
            outputParameters.AddOutput("dsul", host.RoundU(dsul));

            outputParameters.AddOutput("dz", host.RoundU(dsz));
            outputParameters.AddOutput("dp", host.pval(MathDbl.zvalp2(dsz)));

            outputParameters.AddOutput("wmr", host.RoundU(wmr));
            outputParameters.AddOutput("wmr_lcl", host.RoundU(wmrLcl));
            outputParameters.AddOutput("wmr_ucl", host.RoundU(wmrUcl));
            outputParameters.AddOutput("wmr_z", host.RoundU(wmrZ));
            outputParameters.AddOutput("wmr_p", host.pval(wmrP));
            outputParameters.AddOutput("var_r", host.RoundU(varR));
            outputParameters.AddOutput("var_e", host.RoundU(varE));
            outputParameters.AddOutput("var_p", host.RoundU(varP));
            outputParameters.AddOutput("wmr_crll", host.RoundU(wmrCrll));
            outputParameters.AddOutput("wmr_crul", host.RoundU(wmrCrul));
            outputParameters.AddOutput("wmr4", host.RoundU(wmr / 4.0));
            outputParameters.AddOutput("sres", host.RoundU(sres));
            outputParameters.AddOutput("percvar", host.RoundU(percvar));
            outputParameters.AddOutput("het_x2", host.RoundU(hetX2));
            outputParameters.AddOutput("het_p", host.pval(hetP));

            IList<ParameterBag> biasList = new List<ParameterBag>();
            outputParameters.AddOutput("*bias", biasList);
            ParameterBag biasParameters = new ParameterBag();
            biasList.Add(biasParameters);
            Metabias(host, biasParameters, y, llY, ulY, k, ref cco, Transformation.Z);

            IList<ParameterBag> chartList = new List<ParameterBag>();
            outputParameters.AddOutput("*chart", chartList);
            ParameterBag chartParameters;

            if (k > 3)
            {
                using (ChartRenderer ch = new ChartRenderer(ChartDefinition.Empty()))
                {
                    string rtf = ch.PlotBiasMAAndReturnRtf(host, y, odx, wt, k, "Correlation", llY, ulY, cco, cit, wmr, Transformation.Z, false);
                    chartParameters = new ParameterBag();
                    chartList.Add(chartParameters);
                    chartParameters.AddOutput("chart", rtf);
                }
            }

            y[k + 1] = rmh;
            llY[k + 1] = llrmh;
            ulY[k + 1] = ulrmh;
            using (ChartRenderer ch = new ChartRenderer(ChartDefinition.Empty()))
            {
                string rtf = ch.PlotCPAndReturnRtf(host, k + 1, title, y, llY, ulY, wt, pg, "Correlation (Hedges-Olkin fixed effects) meta-analysis plot", stat + " (" + Formatting.XRound(cco * 100, 1) + "% confidence interval" + ")", Transformation.None);
                chartParameters = new ParameterBag();
                chartList.Add(chartParameters);
                chartParameters.AddOutput("chart", rtf);
            }

            y[k + 1] = dsrr;
            llY[k + 1] = dsll;
            ulY[k + 1] = dsul;
            using (ChartRenderer ch = new ChartRenderer(ChartDefinition.Empty()))
            {
                string rtf = ch.PlotCPAndReturnRtf(host, k + 1, title, y, llY, ulY, wt, pg, "Correlation (Hedges-Olkin random effects) meta-analysis plot", stat + " (" + Formatting.XRound(cco * 100, 1) + "% confidence interval" + ")", Transformation.None);
                chartParameters = new ParameterBag();
                chartList.Add(chartParameters);
                chartParameters.AddOutput("chart", rtf);
            }

            y[k + 1] = wmr;
            llY[k + 1] = wmrLcl;
            ulY[k + 1] = wmrUcl;
            using (ChartRenderer ch = new ChartRenderer(ChartDefinition.Empty()))
            {
                string rtf = ch.PlotCPAndReturnRtf(host, k + 1, title, y, llY, ulY, wt, pg, "Correlation (Schmidt-Hunter) meta-analysis plot", stat + " (" + Formatting.XRound(cco * 100, 1) + "% confidence interval" + ")", Transformation.None);
                chartParameters = new ParameterBag();
                chartList.Add(chartParameters);
                chartParameters.AddOutput("chart", rtf);
            }

            return new StepResult(StepSuccess.Success, outputParameters);
        }


        public static void OrciCorn(ITemplateHost host, ref double conflev, ref double a, ref double b, ref double c, ref double d, out double odr, out double ll, out double ul)
        {
            //  ref Alan Agresti R script http://web.stat.ufl.edu/~aa/cda/R/two_sample/R2/
            if (b * c == 0.0)
            {
                double aa;
                double bb;
                double cc;
                double dd;
                ContinuityCorrect(host, a, b, c, d, out aa, out bb, out cc, out dd);
                odr = (aa * dd) / (bb * cc);
            }
            else
            {
                odr = (a * d) / (b * c);
            }
            double x1 = a;
            double n1 = a + c;
            double x2 = b;
            double n2 = b + d;
            double px = x1 / n1;
            double py = x2 / n2;
            double theta;
            if ((((x1 == 0.0) & (x2 == 0.0)) | ((x1 == n1) & (x2 == n2))))
            {
                ul = double.PositiveInfinity;
                ll = 0.0;
            }
            else if (((x1 == 0.0) | (x2 == n2)))
            {
                ll = 0.0;
                theta = 0.01 / n2;
                ul = CornfieldLimit(x1, n1, x2, n2, conflev, ref theta, 1.0);
            }
            else if (((x1 == n1) | (x2 == 0.0)))
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


        private static double CornfieldLimit(double x, double nx, double y, double ny, double conflev, ref double lim, double t)
        {
            double ci = 0;
            int fault;

            double z = PDF.ppchi2(conflev, 1.0, out fault);
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
                    score = (Math.Pow((nx * (px - p1D)), 2.0)) * (1.0 / (nx * p1D * (1 - p1D)) + 1.0 / (ny * p2D * (1.0 - p2D)));
                    ci = lim;
                    if ((t == 0.0))
                    {
                        lim = ci / 1.001;
                    }
                    else
                    {
                        lim = ci * 1.001;
                    }
                    iter = iter + 1;
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
        /// 
        /// </summary>
        /// <param name="q"></param>
        /// <param name="k"></param>
        /// <param name="cit"></param>
        /// <param name="isq"></param>
        /// <param name="ll"></param>
        /// <param name="ul"></param>
        /// <remarks>Higgins P, Thompson S. Quantifying heterogeneity in meta-analysis. Stats in Medicine 2002; 21: 1539-1558</remarks>
        public void Isquare(double q, int k, double cit, out double isq, out double ll, out double ul)
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
                    selh = Math.Sqrt((1.0 / (2.0 * Convert.ToDouble(k - 2))) * (1.0 - (1.0 / (3.0 * Math.Pow(Convert.ToDouble(k - 2), 2.0)))));
                }
                double llh = Math.Exp(Math.Log(Math.Sqrt(hsq)) - cit * selh);
                double ulh = Math.Exp(Math.Log(Math.Sqrt(hsq)) + cit * selh);
                ll = 100.0 * Math.Max(0, (Math.Pow(llh, 2.0) - 1.0) / (Math.Pow(llh, 2.0)));
                ul = 100.0 * Math.Max(0, (Math.Pow(ulh, 2.0) - 1.0) / (Math.Pow(ulh, 2.0)));
            }
        }


        private static void ContinuityCorrect(ITemplateHost host, double a, double b, double c, double d, out double ax, out double bx, out double cx, out double dx)
        {
            double x = host.Preferences.MetaCC == 0.0 ? 0.5 : host.Preferences.MetaCC;
            if (x > 0.0 & x < 1.0)
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
                if (nt > 0.0)
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

        public static StepResult RptProportionMeta(ITemplateHost host, ParameterBag parameters)
        {
            double tausq;
            double cit;
            double isq; double llisq; double ulisq;
            bool stratlab;

            double cco = parameters["gamma"].AsDouble;
            if (cco > 0)
            {
                double p = (1.0 - cco) / 2.0;
                int scrap;
                cit = PDF.gauinv(1.0 - p, out scrap);
            }
            else
            {
                cco = 0.95;
                int scrap;
                cit = PDF.gauinv(0.975, out scrap);
            }

            double fudge = Parsing.Cdbl_Txt(parameters["fudge"].AsString);

            DataFrame snFrame = parameters["sn"].AsDataFrame;
            DoubleVariable snVariable = snFrame.Variables[0].AsDoubleVariable;
            int k = snVariable.Length;
            double[] sn = new double[k + 1 + 1 /* for VB to C# conversion */ ];
            double[] y = new double[k + 1 + 1 /* for VB to C# conversion */ ];
            double[] seY = new double[k + 1 + 1 /* for VB to C# conversion */ ];
            double[] llY = new double[k + 1 + 1 /* for VB to C# conversion */ ];
            double[] ulY = new double[k + 1 + 1 /* for VB to C# conversion */ ];
            int[] pg = new int[k + 1 + 1 /* for VB to C# conversion */ ];
            for (int i = 1; i <= k; i++)
            {
                sn[i] = snVariable.Data[i - 1];
                pg[i] = 0;
            }
            pg[k + 1] = -1;

            DataFrame srFrame = parameters["sr"].AsDataFrame;
            DoubleVariable srVariable = srFrame.Variables[0].AsDoubleVariable;
            double[] sr = new double[k + 1 /* for VB to C# conversion */ ];
            bool allRZero = true;
            bool allREqualN = true;
            for (int i = 1; i <= k; i++)
            {
                double r = srVariable.Data[i - 1];
                sr[i] = r;
                if (r > 0)
                    allRZero = false;
                if (r < sn[i])
                    allREqualN = false;
                if (sr[i] < 0.0 || sn[i] < sr[i])
                {
                    throw new InvalidDataException("Each value of r must be between 0 and its corresponding n");
                }
            }

            string[] title = new string[k + 1 + 1 /* for VB to C# conversion */ ];
            if (parameters.ContainsKey("strata") && parameters["strata"].Data != null)
            {
                stratlab = true;
                DataFrame strataFrame = parameters["strata"].AsDataFrame;
                StringVariable strataVariable = strataFrame.Variables[0].AsStringVariable;
                for (int i = 1; i <= k; i++)
                {
                    string buf = strataVariable.Data[i - 1].Trim();
                    if (buf.Length > 0)
                    {
                        if (buf.Length > 50)
                            buf = buf.Substring(0, 50);
                        title[i] = buf;
                    }
                    else
                        title[i] = "stratum " + i.ToString();
                }
            }
            else
            {
                stratlab = false;
                for (int i = 1; i <= k; i++)
                    title[i] = "stratum " + i.ToString();
            }
            title[k + 1] = ChartRenderer.combo_ti("");

            // Pool
            double sumwt = 0.0;
            double sumsqwt = 0.0;
            double sumywt = 0.0;
            double[] wt = new double[k + 1 + 1 /* for VB to C# conversion */ ];
            double[] dswt = new double[k + 1 + 1 /* for VB to C# conversion */ ];
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
                qc += wt[i] * Math.Pow((y[i] - rmh), 2.0);

            // DerSimonian-Laird random effects
            if ((sumwt - sumsqwt / sumwt) == 0.0)
                tausq = 0.0;
            else
                tausq = (qc - Convert.ToDouble(k - 1)) / (sumwt - sumsqwt / sumwt);
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
            double[,] o = new double[k + 1 /* for VB to C# conversion */, 4 + 1 /* for VB to C# conversion */];
            rmh = ArcsineInv(rmh, sn);
            llrmh = ArcsineInv(llrmh, sn);
            ulrmh = ArcsineInv(ulrmh, sn);
            dspr = ArcsineInv(dspr, sn);
            dsll = ArcsineInv(dsll, sn);
            dsul = ArcsineInv(dsul, sn);

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

            //  RTF_LoadTemplate("propmeta.rtf")
            ParameterBag outputParameters = new ParameterBag();

            IList<ParameterBag> inputsList = new List<ParameterBag>();
            outputParameters.AddOutput("*inputs", inputsList);
            for (int i = 1; i <= k; i++)
            {
                ParameterBag inputsParameters = new ParameterBag();
                inputsList.Add(inputsParameters);
                inputsParameters.AddOutput("st", i.ToString());
                inputsParameters.AddOutput("r", sr[i].ToString());
                inputsParameters.AddOutput("n", sn[i].ToString());
                inputsParameters.AddOutput("lb", stratlab ? title[i] : "");
            }

            outputParameters.AddOutput("pc", Formatting.XRound(cco * 100, 2));

            IList<ParameterBag> proportionsList = new List<ParameterBag>();
            outputParameters.AddOutput("*proportions", proportionsList);
            for (int i = 1; i <= k; i++)
            {
                ParameterBag proportionsParameters = new ParameterBag();
                proportionsList.Add(proportionsParameters);
                proportionsParameters.AddOutput("st", i.ToString());
                proportionsParameters.AddOutput("p", host.RoundU(sr[i] / sn[i]));
                string tmp;
                MathDbl.binci(sr[i], sn[i], out llY[i], out ulY[i], cco, out tmp);
                proportionsParameters.AddOutput("from_y", host.RoundU(llY[i]));
                tmp = "";
                proportionsParameters.AddOutput("to_y", host.RoundU(ulY[i]) + tmp);
                proportionsParameters.AddOutput("wt", host.RoundU(100 * wt[i] / Formatting.dsum(wt, 1)));
                proportionsParameters.AddOutput("dwt", host.RoundU(100 * dswt[i] / Formatting.dsum(dswt, 1)));
                proportionsParameters.AddOutput("standardized_effect", host.RoundU(y[i] > 0 ? Math.Log(y[i]) : 0));
                proportionsParameters.AddOutput("se", host.RoundU(seY[i]));
                if (stratlab)
                {
                    tmp = title[i] + tmp;
                }
                proportionsParameters.AddOutput("lb", tmp);
            }

            outputParameters.AddOutput("rmh", host.RoundU(rmh));
            outputParameters.AddOutput("from", host.RoundU(llrmh));
            outputParameters.AddOutput("to", host.RoundU(ulrmh));

            outputParameters.AddOutput("qc", host.RoundU(qc));
            outputParameters.AddOutput("df", (k - 1).ToString());
            outputParameters.AddOutput("xp", host.pval(PDF.chivalp(qc, Convert.ToDouble(k - 1))));
            outputParameters.AddOutput("tausq", host.RoundU(tausq));
            // Call isquare(qc, k, cit, isq, llisq, ulisq)
            IsquareNcc(host, qc, k, cco, cit, out isq, out llisq, out ulisq);
            outputParameters.AddOutput("isq", Formatting.XRound(isq, 1));
            outputParameters.AddOutput("pc1", Formatting.XRound(cco * 100, 1));
            outputParameters.AddOutput("llisq", Formatting.XRound(llisq, 1));
            outputParameters.AddOutput("ulisq", Formatting.XRound(ulisq, 1));

            outputParameters.AddOutput("dspr", host.RoundU(dspr));
            outputParameters.AddOutput("from_ds", host.RoundU(dsll));
            outputParameters.AddOutput("to_ds", host.RoundU(dsul));

            IList<ParameterBag> eggerList = new List<ParameterBag>();
            outputParameters.AddOutput("*egger", eggerList);
            ParameterBag eggerParameters = new ParameterBag();
            eggerList.Add(eggerParameters);
            Metabias(host, eggerParameters, y, llY, ulY, k, ref cco, Transformation.None);

            IList<ParameterBag> harbordList = new List<ParameterBag>();
            outputParameters.AddOutput("*harbord", harbordList);
            ParameterBag harbordParameters = new ParameterBag();
            harbordList.Add(harbordParameters);
            ModMetabias(host, harbordParameters, o, k, cco, 3);

            IList<ParameterBag> chartList = new List<ParameterBag>();
            outputParameters.AddOutput("*chart", chartList);
            ParameterBag chartParameters;

            if (k > 3)
            {
                using (ChartRenderer ch = new ChartRenderer(ChartDefinition.Empty()))
                {
                    string rtf = ch.PlotBiasMAAndReturnRtf(host, y, sn, wt, k, "Proportion", llY, ulY, cco, cit, rmh, Transformation.None, false);
                    chartParameters = new ParameterBag();
                    chartList.Add(chartParameters);
                    chartParameters.AddOutput("chart", rtf);
                }
            }

            y[k + 1] = rmh;
            llY[k + 1] = llrmh;
            ulY[k + 1] = ulrmh;

            using (ChartRenderer ch = new ChartRenderer(ChartDefinition.Empty()))
            {
                string rtf = ch.PlotCPAndReturnRtf(host, k + 1, title, y, llY, ulY, wt, pg, "Proportion meta-analysis plot [fixed effects]", "proportion" + " (" + Formatting.XRound(cco * 100, 1) + "% confidence interval" + ")", Transformation.None);
                chartParameters = new ParameterBag();
                chartList.Add(chartParameters);
                chartParameters.AddOutput("chart", rtf);
            }

            y[k + 1] = dspr;
            llY[k + 1] = dsll;
            ulY[k + 1] = dsul;
            using (ChartRenderer ch = new ChartRenderer(ChartDefinition.Empty()))
            {
                string rtf = ch.PlotCPAndReturnRtf(host, k + 1, title, y, llY, ulY, dswt, pg, "Proportion meta-analysis plot [random effects]", "proportion" + " (" + Formatting.XRound(cco * 100, 1) + "% confidence interval" + ")", Transformation.None);
                chartParameters = new ParameterBag();
                chartList.Add(chartParameters);
                chartParameters.AddOutput("chart", rtf);
            }

            return new StepResult(StepSuccess.Success, outputParameters);
        }


        private static double ArcsineP(double r, double n)
        {
            // Anscombe (1948)
            // arcsine_p = ASin(Math.Sqrt((r + 3# / 8#) / (N + 3# / 4#)))
            // 
            // Freeman-Tukey
            return Math.Asin(Math.Sqrt(r / (n + 1.0))) + Math.Asin(Math.Sqrt((r + 1.0) / (n + 1.0)));
        }


        private static double ArcsineSe(double n, double fudge)
        {
            // Anscombe (1948)
            // arcsine_se = (N ^ (-0.5)) / 2#
            // 
            // Freeman-Tukey
            //  or N + 0.5
            return Math.Sqrt(1.0 / (n + fudge));
        }


        private static double ArcsineInv(double t, double[] n)
        {
            // Anscombe (1948)
            // arcsine_inv = Sin(P) ^ 2#
            // 
            // Freeman-Tukey
            // arcsine_inv = Math.Sin(t / 2.0) ^ 2.0

            double hmn = 0;
            //  n(0) is empty, n(n.Length - 1) is empty
            for (int i = 1; i <= n.Length - 2; i++)
            {
                hmn += 1.0 / n[i];
            }
            hmn = (n.Length - 2) / hmn;
            return 0.5 * (1.0 - Math.Sign(Math.Cos(t)) * Math.Pow((1.0 - Math.Pow((Math.Sin(t) + (Math.Sin(t) - 1.0 / Math.Sin(t)) / hmn), 2.0)), 0.5));
        }


        private static bool IncludeTable(double[,] o, int i)
        {
            return !((o[i, 1] == 0.0 && o[i, 2] == 0.0) || (o[i, 3] == 0.0 && o[i, 4] == 0.0));
        }


        public static string GetMetaLabel(ITemplateHost host, double[,] o, int i, bool stratlab, bool[] cced, string[] title)
        {
            string tmp;
            if (IncludeTable(o, i))
            {
                tmp = stratlab ? title[i] : "";
                if (cced[i])
                {
                    tmp += " [CC = ";
                    tmp += host.Preferences.MetaCC == -9.0 ? "treatment arm" : host.Preferences.MetaCC.ToString();
                    tmp += "]";
                }
            }
            else
            {
                tmp = "* (excluded)";
            }
            return tmp;
        }


        public static void ModMetabias(ITemplateHost host, ParameterBag outputParameters, double[,] o, int k, double cco, int method)
        {
            double ll; double ul;
            double se = 0; double ncco;
            double sys = 0;
            int i;

            //  Horbord et al 2006
            // get linear regression of z on sqr(v)
            if (cco > 0.0)
            {
                ncco = cco - (1.0 - cco) / 2.0;
            }
            else
            {
                ncco = 0.95;
            }
            double sumx = 0.0;
            double sumy = 0.0;
            double sumxy = 0.0;
            double sxs = 0.0;
            int realk = 0;
            for (i = 1; i <= k; i++)
            {
                if (IncludeTable(o, i))
                {
                    realk = realk + 1;
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
                            // relative risk parameters from Whitehead
                            z = a - b * c;
                            v = b * c * (1.0 - c);
                        }
                        else if (method == 2)
                        {
                            // relative risk parameters from Whitehead
                            z = (a * n - (a + b) * (a + c)) / (c + d);
                            v = (b + d) * (a + c) * (a + b) / (n * (c + d));
                        }
                        else
                        {
                            // efficient score
                            z = a - (a + b) * (a + c) / n;
                            // use profile likelihood variance rather than conditional likelihood of Peto method
                            v = (a + b) * (c + d) * (a + c) * (b + d) / Math.Pow(n, 3.0);
                        }
                        double x = Math.Sqrt(v);
                        double y = z / Math.Sqrt(v);
                        sumx = sumx + x;
                        sxs = sxs + x * x;
                        sys = sys + y * y;
                        sumy = sumy + y;
                        sumxy = sumxy + x * y;
                    }
                }
            }
            double ssx = sxs - ((sumx * sumx) / Convert.ToDouble(realk));
            double ssy = sys - ((sumy * sumy) / Convert.ToDouble(realk));
            double xy = sumxy - (sumx * sumy / Convert.ToDouble(realk));
            double slope = xy / ssx;
            double yInt = (sumy / Convert.ToDouble(realk)) - slope * (sumx / Convert.ToDouble(realk));
            double ssreg = (xy * xy) / ssx;
            double ssres = ssy - ssreg;
            double bias = yInt;
            if (realk > 2 & ssres >= 0.0)
            {
                double mnsqr = ssres / Convert.ToDouble(realk - 2);
                se = Math.Sqrt(mnsqr * (1.0 / Convert.ToDouble(realk) + Math.Pow((sumx / Convert.ToDouble(realk)), 2.0) / ssx));
                double scrap;
                double cit;
                MathDbl.civ(realk - 2, out cit, ncco, out scrap);
                ll = bias - se * cit;
                ul = bias + se * cit;
            }
            else
            {
                ll = Constant.MISSING;
                ul = Constant.MISSING;
            }
            double t = bias / se;
            double p2 = PDF.tvalp(Math.Abs(t), Convert.ToDouble(realk - 2));
            if (p2 > 1.0 - p2)
            {
                p2 = 1.0 - p2;
            }
            p2 = 2.0 * p2;
            outputParameters.AddOutput("a", host.RoundU(bias));
            outputParameters.AddOutput("pc_horbold", Formatting.XRound(100.0 * ncco, 2));
            outputParameters.AddOutput("cl", host.RoundU(ll));
            outputParameters.AddOutput("cu", host.RoundU(ul));
            outputParameters.AddOutput("p", host.pval(p2));
        }

        public static void IsquareNcc(ITemplateHost host, double q, int k, double cco, double cit, out double i2, out double ll, out double ul)
        {
            double SElnH;
            double minLbNc;
            int ierr = 0;
            double lbI2H; double ubI2H;

            double df = Convert.ToDouble(k - 1);
            double dk = Convert.ToDouble(k);

            i2 = Constant.MISSING;
            ll = Constant.MISSING;
            ul = Constant.MISSING;

            if (q < 0)
                return;

            // Calculate I-squared even with only one degree of freedom.
            if (df < 1)
                return;
            i2 = Math.Max(0.0, (100.0 * (q - df) / q));
            if (df < 2)
                return;
            if (cco < 0.1 || cco > 0.99)
                return;

            double level = 100.0 * cco;
            double levelci = level * 0.005 + 0.5;
            double clevelci = 1.0 - levelci;

            double h2 = q / df;
            double i22 = Math.Max(0.0, (h2 - 1.0) / h2);
            if (Math.Sqrt(h2) < 1.0)
            {
                h2 = 1.0;
            }

            //  CI for H (Higgins & Thompson, 2002 Stat in Med)
            if (q > k)
            {
                SElnH = 0.5 * ((Math.Log(q) - Math.Log(df)) / (Math.Sqrt(2.0 * q) - Math.Sqrt(2.0 * dk - 3.0)));
            }
            else
            {
                SElnH = Math.Sqrt((1.0 / (2.0 * (dk - 2.0)) * (1.0 - 1.0 / (3.0 * Math.Pow((dk - 2.0), 2.0)))));
            }
            // double LB_H_III = Math.Exp( Math.Log( Math.Sqrt( H2 ) ) - cit * SElnH ); 
            // double UB_H_III = Math.Exp( Math.Log( Math.Sqrt( H2 ) ) + cit * SElnH ); 
            // if ( LB_H_III < 1.0 )
            // { 
            // LB_H_III = 1.0; 
            // } 
            //  CI for H (P 1550 Higgins & Thompson)
            // double LB_I2_HT = Math.Max( 0.0, ( Math.Pow( LB_H_III, 2.0 ) - 1.0 ) / Math.Pow( LB_H_III, 2.0 ) ); 
            // double UB_I2_HT = ( Math.Pow( UB_H_III, 2.0 ) - 1.0 ) / Math.Pow( UB_H_III, 2.0 ); 

            //  CI interval for I2 based var(logH), formula not indicated in (Higgins & Thompson, 2002 Stat in Med)
            double varI2 = 4.0 * Math.Pow(SElnH, 2.0) / Math.Exp(4.0 * Math.Log(Math.Sqrt(h2)));
            double lbI2 = i22 - cit * Math.Sqrt(varI2);
            double ubI2 = i22 + cit * Math.Sqrt(varI2);
            if (lbI2 < 0.0)
            {
                lbI2 = 0.0;
            }
            if (ubI2 > 1.0)
            {
                ubI2 = 1.0;
            }
            ll = 100.0 * lbI2;
            ul = 100.0 * ubI2;

            if (!(host.Preferences.MetaExact))
            {
                return;
            }

            //  Iterative solution to seek CI for non-centrality parameter (and then for H and I2)
            //  non-centrality (nc) parameter = (Q-df)
            double nc = Math.Max(0.0, q - df);
            double endp = nc + 1000.0;

            //  check if Q < df , in this case no need to seek the lower bound
            if (q < df)
            {
                minLbNc = 0.0;
            }
            else
            {
                minLbNc = IsquareBrentRoot(0, endp, ref nc, ref df, ref clevelci, ref ierr);
                if (ierr != 0)
                {
                    minLbNc = Constant.MISSING;
                }
            }

            double minUbNc = IsquareBrentRoot(0, endp, ref nc, ref df, ref levelci, ref ierr);
            if (ierr != 0)
            {
                minUbNc = Constant.MISSING;
            }

            //  transform lower bound for non-centrality parameter (Q-df) in lower bound for H and I2
            if (minLbNc != Constant.MISSING)
            {
                double lbHH = Math.Max(1.0, Math.Sqrt(minLbNc / df));
                lbI2H = Math.Max(0.0, (Math.Pow(lbHH, 2.0) - 1.0) / Math.Pow(lbHH, 2.0));
            }
            else
            {
                // LB_H_H = Constant.MISSING; 
                lbI2H = Constant.MISSING;
            }

            if (minUbNc != Constant.MISSING)
            {
                double ubHH = Math.Sqrt(minUbNc / df);
                ubI2H = (Math.Pow(ubHH, 2.0) - 1.0) / Math.Pow(ubHH, 2.0);
            }
            else
            {
                // UB_H_H = Constant.MISSING; 
                ubI2H = Constant.MISSING;
            }

            // if all goes well - assign the Higgins non-central chi-square interval as the result
            if (lbI2H == Constant.MISSING)
            {
                ll = Constant.MISSING;
            }
            else
            {
                ll = 100.0 * lbI2H;
            }
            if (ubI2H == Constant.MISSING)
            {
                ul = Constant.MISSING;
            }
            else
            {
                ul = 100.0 * ubI2H;
            }

        }

        /// <summary>
        /// Brent alternative to Pegasus method for root finding - can be faster when high precision demanded
        /// </summary>
        /// <param name="xl">lower bound of search interval</param>
        /// <param name="xu">upper bound of search interval</param>
        /// <param name="nc"></param>
        /// <param name="df"></param>
        /// <param name="clev"></param>
        /// <param name="ierr"></param>
        /// <returns></returns>
        private static double IsquareBrentRoot(double xl, double xu, ref double nc, ref double df, ref double clev, ref int ierr)
        {
            double d = 0;
            const double tolerance = 0.000001;
            const int maxIter = 300;

            double e = 0.0;
            double a = xl;
            double b = xu;

            double fa = ExFortran.nchi2(df, nc, a) - clev;
            if (fa == Constant.MISSING)
            {
                return 0;
            }
            double fb = ExFortran.nchi2(df, nc, b) - clev;
            if (fb == Constant.MISSING)
            {
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

                if ((fb > 0.0 && fc > 0.0) || (fb < 0.0 && fc < 0.0))
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

                double tol1 = 2.0 * Constant.EPSILON * Math.Abs(b) + 0.5 * tolerance;

                double xm = 0.5 * (c - b);

                if ((Math.Abs(xm) <= tol1 | fb == 0.0))
                {
                    break; /* TRANSWARNING: check that break is in correct scope */
                }

                if ((Math.Abs(e) >= tol1 & Math.Abs(fa) > Math.Abs(fb)))
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

                fb = ExFortran.nchi2(df, nc, b) - clev;
                if (fb == Constant.MISSING)
                {
                    return 0;
                }
            }
            while (true);

            return b;
        }

    }



}
