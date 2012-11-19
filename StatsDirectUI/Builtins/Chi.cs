using System;
using System.Collections.Generic;
using System.Diagnostics;
using StatsDirect.Charting;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.Templates;
using StatsDirect.Utilities;

namespace StatsDirect.Builtins
{
    public class Chi
    {
        public static StepResult RptChi2By2(ITemplateHost host, ParameterBag parameters)
        {
            double eor = 0;
            int fault;

            double cco = parameters["cco"].AsDouble;
            string studyType = parameters["study_type"].AsString;
            bool isCaseControl = "casecontrol".Equals(studyType);
            bool isCohort = "cohort".Equals(studyType);
            bool doFisher = parameters.ContainsKey("doFisher") && parameters["doFisher"].AsBoolean;

            if (cco <= 0.0 || cco >= 1.0)
                cco = 0.95;
            double cit = PDF.gauinv(cco + (1.0 - cco) / 2.0, out fault);

            double a = parameters["a"].AsDouble;
            double b = parameters["b"].AsDouble;
            double c = parameters["c"].AsDouble;
            double d = parameters["d"].AsDouble;
            double p = a + b;
            double q = c + d;
            double r = a + c;
            double s = b + d;
            double n = p + q;
            ParameterBag outputParameters = new ParameterBag();
            //  RTF_LoadTemplate("chi2x2.rtf")
            if (!((fault == 0 && (p > 0 || q > 0 || r > 0 || s > 0) && (p * q * r * s > 0))))
            {
                throw new InvalidDataException();
            }
            outputParameters.AddOutput("tab3_a1", host.RoundU(a));
            outputParameters.AddOutput("tab3_b1", host.RoundU(b));
            outputParameters.AddOutput("tab3_c1", host.RoundU(p));
            outputParameters.AddOutput("tab3_a2", host.RoundU(c));
            outputParameters.AddOutput("tab3_b2", host.RoundU(d));
            outputParameters.AddOutput("tab3_c2", host.RoundU(q));
            outputParameters.AddOutput("tab3_a3", host.RoundU(r));
            outputParameters.AddOutput("tab3_b3", host.RoundU(s));
            outputParameters.AddOutput("tab3_c3", host.RoundU(n));

            double e1 = p * r / n;
            double e2 = p * s / n;
            double e3 = q * r / n;
            double e4 = q * s / n;

            outputParameters.AddOutput("tab_a1", host.RoundU(e1));
            outputParameters.AddOutput("tab_b1", host.RoundU(e2));
            outputParameters.AddOutput("tab_a2", host.RoundU(e3));
            outputParameters.AddOutput("tab_b2", host.RoundU(e4));

            double f = a * d - b * c;
            double x2 = f * f * n / (p * q * r * s);
            outputParameters.AddOutput("chi", host.RoundU(x2));
            outputParameters.AddOutput("chi_p", host.pval(PDF.chivalp(x2, 1.0)));

            f = Math.Abs(f) - n / 2;
            if (f < 0)
            {
                f = 0;
            }
            double x2C = f * f * n / (p * q * r * s);
            outputParameters.AddOutput("yates_chi", host.RoundU(x2C));
            outputParameters.AddOutput("yates_chi_p", host.pval(PDF.chivalp(x2C, 1.0)));

            // coefficients (see Agresti p 23-4)
            double p1 = Math.Sqrt(x2 / (x2 + n));
            double c1 = ((a * d) - (b * c)) / Math.Sqrt(p * q * r * s);
            outputParameters.AddOutput("pearson", host.RoundU(p1));
            outputParameters.AddOutput("vs", host.RoundU(c1));

            List<ParameterBag> warnList = new List<ParameterBag>();
            outputParameters.AddOutput("*warn", warnList);
            if (e1 < 5 || e2 < 5 || e3 < 5 || e4 < 5 || n < 20)
            {
                ParameterBag warnParameters = new ParameterBag();
                warnList.Add(warnParameters);
                string wrn = n < 20 ? "Number of observations" : "Expected frequencies";
                warnParameters.AddOutput("wrn", wrn);
            }

            bool doneExact = false;

            List<ParameterBag> oddsList = new List<ParameterBag>();
            outputParameters.AddOutput("*odds", oddsList);
            List<ParameterBag> relRiskList = new List<ParameterBag>();
            outputParameters.AddOutput("*relrisk", relRiskList);
            if (isCaseControl)
            {
                ParameterBag oddsParameters = new ParameterBag();
                oddsList.Add(oddsParameters);
                // Woolf/logit CI
                double yodr;
                double odr;
                double xodr;
                if (b * c > 0.0 & a * d > 0.0)
                {
                    odr = (a * d) / (b * c);
                    double seodr = Math.Sqrt(1.0 / a + 1.0 / b + 1.0 / c + 1.0 / d);
                    yodr = Math.Exp(Math.Log(odr) - cit * seodr);
                    xodr = Math.Exp(Math.Log(odr) + cit * seodr);
                }
                else
                {
                    odr = Constant.MISSING;
                    yodr = Constant.MISSING;
                    xodr = Constant.MISSING;
                }
                oddsParameters.AddOutput("odds", host.RoundU(odr));
                oddsParameters.AddOutput("woolf_ci", host.RoundU(cco * 100.0));
                oddsParameters.AddOutput("woolf_ci_1", host.RoundU(yodr));
                oddsParameters.AddOutput("woolf_ci_2", host.RoundU(xodr));
                // CMLE
                ExactBB.Rec2X2[] tabl = new ExactBB.Rec2X2[1 + 1 /* for VB to C# conversion */];
                tabl[1].Freq = 1;
                tabl[1].A = a;
                tabl[1].M1 = a + b;
                tabl[1].N1 = a + c;
                tabl[1].N0 = b + d;
                tabl[1].Informative = (a * d != 0) | (b * c != 0);
                bool useLogScale = false;
                int ierr;
                double llm;
                double ulf;
                double ulm;
                double llf;
                double p2M;
                double p1M;
                double p2F;
                double p1F;
                new ExactBB().Exact22K(host, 1, 1, tabl, cco, ref eor, out ulf, out llf, out ulm, out llm, out p1F, out p2F, out p1M, out p2M, ref useLogScale, out ierr);
                if (ierr != 0)
                {
                    // eor = Constant.MISSING; 
                    ulf = Constant.MISSING;
                    llf = Constant.MISSING;
                    ulm = Constant.MISSING;
                    llm = Constant.MISSING;
                    p1F = Constant.MISSING;
                    p2F = Constant.MISSING;
                    p1M = Constant.MISSING;
                    p2M = Constant.MISSING;
                }
                else
                {
                    doneExact = true;
                }
                oddsParameters.AddOutput("ci", Formatting.XRound(cco * 100.0, 2));
                oddsParameters.AddOutput("llf", host.RoundU(llf));
                oddsParameters.AddOutput("ulf", host.RoundU(ulf));
                oddsParameters.AddOutput("p1f", host.pval(p1F));
                oddsParameters.AddOutput("p2f", host.pval(p2F));
                oddsParameters.AddOutput("llm", host.RoundU(llm));
                oddsParameters.AddOutput("ulm", host.RoundU(ulm));
                oddsParameters.AddOutput("p1m", host.pval(p1M));
                oddsParameters.AddOutput("p2m", host.pval(p2M));
            }
            else if (isCohort)
            {
                //  RTF_LoadTemplate("relrisk.rtf")
                relRiskList.Add(Analysis.RptMiscRelRisk(host, parameters).ParameterBag);
            }

            List<ParameterBag> fisherList = new List<ParameterBag>();
            outputParameters.AddOutput("*fisher", fisherList);
            if (!(doneExact))
            {
                if (e1 < 5 || e2 < 5 || e3 < 5 || e4 < 5 || n < 20)
                {
                    //  RTF_LoadTemplate("fisher.rtf")
                    fisherList.Add(Exact.RptExactFisher(host, parameters).ParameterBag);
                }
                else
                {
                    if (doFisher)
                    {
                        //  RTF_LoadTemplate("fisher.rtf")
                        fisherList.Add(Exact.RptExactFisher(host, parameters).ParameterBag);
                    }
                }
            }
            return new StepResult(StepSuccess.Success, outputParameters);
        }


        public static StepResult RptChi2ByNWithoutTrend(ITemplateHost host, ParameterBag parameters)
        {
            return RptChi2ByN(host, parameters, 0);
        }


        public static StepResult RptChi2ByNLinearTrend(ITemplateHost host, ParameterBag parameters)
        {
            return RptChi2ByN(host, parameters, 1);
        }


        public static StepResult RptChi2ByNWithTrend(ITemplateHost host, ParameterBag parameters)
        {
            return RptChi2ByN(host, parameters, 2);
        }


        private static StepResult RptChi2ByN(ITemplateHost host, ParameterBag parameters, int z)
        {
            double k4 = 0;
            double k2 = 0;
            double k1 = 0;
            double c = 0;
            double t = 0;
            double b = 0;
            double a = 0;

            DataFrame datFrame = parameters["data"].AsDataFrame;
            if (datFrame.VariableCount < ((z > 1) ? 3 : 2))
                throw new InvalidDataException("Please fill in all columns of the data");
            DoubleVariable datV0 = datFrame.Variables[0].AsDoubleVariable;
            DoubleVariable datV1 = datFrame.Variables[1].AsDoubleVariable;
            DoubleVariable datV2 = null;
            if (z > 1)
            {
                datV2 = datFrame.Variables[2].AsDoubleVariable;
            }
            int rows = datFrame.MaxRows;
            double[] f = new double[rows + 1 /* for VB to C# conversion */ ];
            double[] g = new double[rows + 1 /* for VB to C# conversion */ ];
            double[] h = new double[rows + 1 /* for VB to C# conversion */ ];
            double[] s = new double[rows + 1 /* for VB to C# conversion */ ];

            //  RTF_LoadTemplate("chi2xc.rtf") Then
            ParameterBag outputParameters = new ParameterBag();
            for (int r = 1; r <= rows; r++)
            {
                double a1 = datV0.Data[r - 1];
                double b1 = datV1.Data[r - 1];
                double t1 = a1 + b1;
                if (t1 <= 0.0)
                    throw new InvalidDataException();

                Debug.Assert(z != 2 || datV2 != null);
                double s1 = z == 2 ? datV2.Data[r - 1] : r;
                f[r] = a1;
                g[r] = b1;
                h[r] = t1;
                s[r] = s1;
                a += a1;
                b += b1;
                t += t1;
                c += a1 * a1 / t1;
                k1 += s1 * a1;
                k2 += s1 * b1;
                k4 += s1 * s1 * (a1 + b1);
            }
            double n1 = 0;
            List<ParameterBag> rowList = new List<ParameterBag>();
            outputParameters.AddOutput("*row", rowList);
            for (int r = 1; r <= rows; r++)
            {
                double a1 = f[r];
                double b1 = g[r];
                double t1 = h[r];
                double s1 = s[r];
                double e1 = a * t1 / t;
                if (e1 < 5)
                    n1++;
                double e2 = b * t1 / t;
                if (e2 < 5)
                    n1++;
                ParameterBag rowParameters = new ParameterBag();
                rowList.Add(rowParameters);
                rowParameters.AddOutput("obs_succ", a1.ToString());
                rowParameters.AddOutput("obs_fail", b1.ToString());
                rowParameters.AddOutput("obs_tot", t1.ToString());
                rowParameters.AddOutput("obs_pc", Formatting.XRound(100 * a1 / t1, 2));
                rowParameters.AddOutput("score", s1.ToString());

                rowParameters.AddOutput("exp_succ", host.RoundU(e1));
                rowParameters.AddOutput("exp_fail", host.RoundU(e2));
            }

            outputParameters.AddOutput("tot_succ", a.ToString());
            outputParameters.AddOutput("tot_fail", b.ToString());
            outputParameters.AddOutput("tot_tot", t.ToString());
            outputParameters.AddOutput("tot_pc", Formatting.XRound(100 * a / t, 2));

            List<ParameterBag> warnList = new List<ParameterBag>();
            outputParameters.AddOutput("*warn", warnList);
            if (n1 != 0)
            {
                ParameterBag warnParameters = new ParameterBag();
                warnList.Add(warnParameters);
                warnParameters.AddOutput("num", n1.ToString());
                warnParameters.AddOutput("den", (2 * rows).ToString());
            }

            double n2 = rows - 1;
            double x2 = (t * c - a * a) * t / (a * b);

            outputParameters.AddOutput("chi", host.RoundU(x2));
            outputParameters.AddInput("x2", x2); //  For use with follow-on functions
            outputParameters.AddOutput("chi_abs", host.RoundU(Math.Sqrt(x2)));
            outputParameters.AddOutput("totdf", n2.ToString());
            outputParameters.AddOutput("chi_p", host.pval(PDF.chivalp(x2, n2)));

            List<ParameterBag> zList = new List<ParameterBag>();
            outputParameters.AddOutput("*z", zList);
            if (z != 0)
            {
                c = x2;
                double k8 = b / a;
                double d = (k4 - Math.Pow((k1 + k2), 2.0) / t) / k8;
                if (d < 0)
                {
                    d = 0;
                }
                d = Math.Sqrt(d);
                double x1 = (k1 - k2 / k8) / d;
                x2 = x1 * x1;
                n2 = 1;
                ParameterBag zParameters = new ParameterBag();
                zList.Add(zParameters);

                zParameters.AddOutput("chi_lin", host.RoundU(x2));
                outputParameters.AddInput("x2_lin", x2); //  For use with follow-on functions
                zParameters.AddOutput("chi_1df", host.RoundU(x1));
                zParameters.AddOutput("chi_lin_p", host.pval(PDF.chivalp(x2, n2)));

                x2 = c - x2;
                n2 = rows - 2;
                zParameters.AddOutput("chi_non", host.RoundU(x2));
                zParameters.AddOutput("df", n2.ToString());
                zParameters.AddOutput("chi_non_p", host.pval(PDF.chivalp(x2, n2)));
            }

            return new StepResult(StepSuccess.Success, outputParameters);
        }


        public static StepResult RptChiMantel(ITemplateHost host, ParameterBag parameters)
        {
            double p2M = 0;
            double p1M = 0;
            double p2F = 0;
            double p1F = 0;
            double llm = 0;
            double ulm = 0;
            double llf = 0;
            double ulf = 0;
            double eor = 0;
            double dsul;
            double dsll;
            double dsx2;
            double dsor;
            double bd = 0;
            double qc = 0;
            double sk;
            double x2;
            double ul;
            double ll;
            double rmh;
            double isq; double tausq = 0;
            double llisq;
            double ulisq;
            int r;
            int realk; int i; int ierr;

            DataFrame datFrame = parameters["data"].AsDataFrame;
            DoubleVariable datV0 = datFrame.Variables[0].AsDoubleVariable;
            DoubleVariable datV1 = datFrame.Variables[1].AsDoubleVariable;
            int rows = datFrame.MaxRows;

            if (rows <= 0)
                throw new InvalidDataException();

            int k = rows / 2;
            double[,] o = new double[k + 1 /* for VB to C# conversion */, 4 + 1 /* for VB to C# conversion */];
            double[] odr = new double[k + 1 /* for VB to C# conversion */ ];
            double[] odw = new double[k + 1 /* for VB to C# conversion */ ];
            double[] dswt = new double[k + 1 /* for VB to C# conversion */ ];
            double[] odrl = new double[k + 1 /* for VB to C# conversion */ ];
            double[] odru = new double[k + 1 /* for VB to C# conversion */ ];
            double[] odx = new double[k + 1 /* for VB to C# conversion */ ];
            bool[] lerr = new bool[k + 1 /* for VB to C# conversion */ ];
            bool[] uerr = new bool[k + 1 /* for VB to C# conversion */];
            string[] title = new string[k + 1 /* for VB to C# conversion */ ];
            bool[] cced = new bool[k + 1 /* for VB to C# conversion */ ];
            double[] axll = new double[k + 1 /* for VB to C# conversion */ ];
            double[] axul = new double[k + 1 /* for VB to C# conversion */ ];
            for (r = 1; r <= rows; r += 2)
            {
                int strat = 1 + r / 2;
                title[strat] = "stratum " + strat.ToString();
                double rtd = datV0.Data[r - 1];
                o[strat, 1] = rtd;
                rtd = datV1.Data[r - 1];
                o[strat, 3] = rtd;
                rtd = datV0.Data[r];
                o[strat, 2] = rtd;
                rtd = datV1.Data[r];
                o[strat, 4] = rtd;
            }

            double cco = parameters["cco"].AsDouble;
            if (cco <= 0.0 | cco >= 1.0)
            {
                cco = 0.95;
            }

            bool plotForest = parameters["plot_forest"].AsBoolean;
            int fault;
            double cit = PDF.gauinv(cco + (1.0 - cco) / 2.0, out fault);

            Meta.Mantel(host, false, k, out realk, o, out rmh, out ll, out ul, out x2, out sk, cit, ref cco, ref odr, ref odw, ref dswt, ref odrl, ref odru, ref odx, ref lerr, ref uerr, ref qc, ref bd, out dsor, out dsx2, out dsll, out dsul, ref cced, ref tausq, out ierr);
            if (ierr != 0)
                return null;

            // Try exact Mantel
            bool tryExact = parameters["try_exact"].AsBoolean;
            if (tryExact)
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
            List<ParameterBag> inputsList = new List<ParameterBag>();
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
                inputsParameters.AddOutput("lb", "");
            }
            outputParameters.AddOutput("pc", Formatting.XRound(cco * 100, 2));
            outputParameters.AddOutput("method", host.Preferences.MetaExact ? "CML" : "logit");
            List<ParameterBag> orList = new List<ParameterBag>();
            outputParameters.AddOutput("*or", orList);
            for (i = 1; i <= k; i++)
            {
                ParameterBag orParameters = new ParameterBag();
                orList.Add(orParameters);
                orParameters.AddOutput("st", i.ToString());
                orParameters.AddOutput("or", host.RoundU(odr[i]));
                orParameters.AddOutput("lci", host.RoundU(odrl[i]));
                orParameters.AddOutput("uci", host.RoundU(odru[i]));
                orParameters.AddOutput("wt", host.RoundU(100 * odw[i] / Formatting.dsum(odw, 1)));
                orParameters.AddOutput("dwt", host.RoundU(100 * dswt[i] / Formatting.dsum(dswt, 1)));
                orParameters.AddOutput("lb", Meta.GetMetaLabel(host, o, i, false, cced, title));
                if (host.Preferences.MetaExact & ((i) == Constant.MISSING | odru[i] == Constant.MISSING))
                {
                    Meta.OrciCorn(host, ref cco, ref o[i, 1], ref o[i, 2], ref o[i, 3], ref o[i, 4], out odr[i], out odrl[i], out odru[i]);
                    orParameters = new ParameterBag();
                    orList.Add(orParameters);
                    orParameters.AddOutput("st", "* " + i.ToString());
                    orParameters.AddOutput("or", "");
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

            List<ParameterBag> cmlList = new List<ParameterBag>();
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
            Meta.IsquareNcc(host, qc, realk, cco, cit, out isq, out llisq, out ulisq);
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

            Meta.GetLogitCi(host, o, k, cit, axll, axul);

            List<ParameterBag> eggerList = new List<ParameterBag>();
            outputParameters.AddOutput("*egger", eggerList);
            ParameterBag eggerParameters = new ParameterBag();
            eggerList.Add(eggerParameters);
            Meta.Metabias(host, eggerParameters, odr, axll, axul, k, ref cco, Transformation.Log);

            List<ParameterBag> horboldList = new List<ParameterBag>();
            outputParameters.AddOutput("*horbold", horboldList);
            ParameterBag horboldParameters = new ParameterBag();
            horboldList.Add(horboldParameters);
            Meta.ModMetabias(host, horboldParameters, o, k, cco, 1);

            IList<ParameterBag> chartList = new List<ParameterBag>();
            outputParameters.AddOutput("*chart", chartList);

            if (plotForest)
            {
                using (ChartRenderer ch = new ChartRenderer(ChartDefinition.Empty()))
                {
                    bool scrap;
                    string rtf = ch.PlotMHAndReturnRtf(host, k, o, odw, title, rmh, ll, ul, cco, odr, odrl, odru, lerr, uerr, "Odds ratio meta-analysis plot [fixed effects]", 1, "odds ratio", out scrap, null);
                    ParameterBag chartParameters = new ParameterBag();
                    chartList.Add(chartParameters);
                    chartParameters.AddOutput("chart", rtf);
                }

                using (ChartRenderer ch = new ChartRenderer(ChartDefinition.Empty()))
                {
                    bool scrap;
                    string rtf = ch.PlotMHAndReturnRtf(host, k, o, dswt, title, dsor, dsll, dsul, cco, odr, odrl, odru, lerr, uerr, "Odds ratio meta-analysis plot [random effects]", 1, "odds ratio", out scrap, null);
                    ParameterBag chartParameters = new ParameterBag();
                    chartList.Add(chartParameters);
                    chartParameters.AddOutput("chart", rtf);
                }
            }
            return new StepResult(StepSuccess.Success, outputParameters);
        }


        public static StepResult RptChiRbyC(ITemplateHost host, ParameterBag parameters)
        {
            double cco = parameters["cco"].AsDouble;
            DataFrame dataFrame = parameters["data"].AsDataFrame;
            int rows = dataFrame.MaxRows;
            int cols = dataFrame.VariableCount;
            double[,] a = new double[rows + 1 /* for VB to C# conversion */, cols + 1 /* for VB to C# conversion */];
            double t = 0;
            for (int r = 1; r <= rows; r++)
            {
                for (int c = 1; c <= cols; c++)
                {
                    double a1 = dataFrame.Variables[c - 1].AsDoubleVariable.Data[r - 1];
                    a[r, c] = a1;
                    t += a1;
                }
            }
            if (t <= 0.0)
                throw new InvalidDataException();

            bool doExact = parameters["doExact"].AsBoolean;
            bool pc = parameters["show_pc"].AsBoolean;
            bool xp = parameters["xp"].AsBoolean;
            bool cs = parameters["cs"].AsBoolean;
            bool xs = parameters["xs"].AsBoolean;
            bool specifyScores = parameters["specify_scores"].AsBoolean;

            StepResult outputResult = Tables.SChi(host, ref cco, a, rows, cols, doExact, pc, xp, cs, xs, specifyScores);
            return outputResult;
        }

        public static StepResult RptChiWoolf(ITemplateHost host, ParameterBag parameters)
        {
            int rc;
            bool ierr;

            DataFrame datFrame = parameters["data"].AsDataFrame;
            DoubleVariable datV0 = datFrame.Variables[0].AsDoubleVariable;
            DoubleVariable datV1 = datFrame.Variables[1].AsDoubleVariable;
            int rows = datFrame.MaxRows;
            if (rows <= 0)
            {
                throw new InvalidDataException();
            }

            double cco = parameters["cco"].AsDouble;
            if (cco <= 0.0 | cco >= 1.0)
                cco = 0.95;

            int fault;
            double cit = PDF.gauinv(cco + (1.0 - cco) / 2.0, out fault);

            bool showIntermediates = parameters["show_intermediates"].AsBoolean;

            int k = rows / 2;
            double[,] o = new double[k + 1 /* for VB to C# conversion */, 5];
            int cnt = 0;
            for (rc = 1; rc <= rows; rc += 2)
            {
                cnt = cnt + 1;
                double rtd = datV0.Data[rc - 1];
                o[cnt, 1] = rtd;
                rtd = datV1.Data[rc - 1];
                o[cnt, 2] = rtd;
                rtd = datV0.Data[rc];
                o[cnt, 3] = rtd;
                rtd = datV1.Data[rc];
                o[cnt, 4] = rtd;
            }

            return Tables.Woolf(host, o, k, showIntermediates, cit, cco, out ierr);
        }

        public static StepResult RptChi2ByNWithTrendSimulateExactP(ITemplateHost host, ParameterBag parameters)
        {
            int iterations = parameters["iterations"].AsInt32;
            double ci = parameters["ci"].AsDouble;
            int seed = parameters["seed"].AsInt32;
            double x2 = parameters["x2_lin"].AsDouble;

            DataFrame datFrame = parameters["data"].AsDataFrame;
            bool hasSpecifiedTrend = datFrame.VariableCount == 3;
            DoubleVariable datV0 = datFrame.Variables[0].AsDoubleVariable;
            DoubleVariable datV1 = datFrame.Variables[1].AsDoubleVariable;
            DoubleVariable datV2 = null;
            if (hasSpecifiedTrend)
            {
                datV2 = datFrame.Variables[2].AsDoubleVariable;
            }
            int rows = datFrame.MaxRows;
            const int cols = 2;

            int[,] x = new int[rows + 1 /* for VB to C# conversion */, 3];
            double[] wt = new double[rows + 1 /* for VB to C# conversion */ ];
            for (int row = 1; row <= rows; row++)
            {
                x[row, 1] = Convert.ToInt32(datV0.Data[row - 1]);
                x[row, 2] = Convert.ToInt32(datV1.Data[row - 1]);
                if (hasSpecifiedTrend)
                {
                    wt[row] = datV2.Data[row - 1];
                }
                else
                {
                    wt[row] = row;
                }
            }

            int r;
            int actualIterations;
            int ierror = 0;
            Chi2TrendResample(host, x, wt, rows, cols, x2, iterations, out r, out actualIterations, seed, ref ierror);

            ParameterBag outputParameters = new ParameterBag();
            if (ierror == 0 || ierror == -1 /* interrupted but partial results returned */ )
            {
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
            }
            else
            {
                outputParameters.AddOutput("p", "P = * (cancelled)");
            }
            host.FinishProgress();
            return new StepResult(StepSuccess.Success, outputParameters);
        }

        ///  <summary>
        ///  Simulated exact P for Cochran-Armitage trend test
        ///  </summary>
        /// <param name="host"></param>
        /// <param name="x">(1..nrow,1..ncol) input 2 by k table</param>
        ///  <param name="wt">(1..nrow) input weights</param>
        ///  <param name="nrow">rows</param>
        ///  <param name="ncol">columns (could modify this for r by c)</param>
        ///  <param name="x2">chi-square for trend (could add independence chi-square too)</param>
        ///  <param name="iter">Monte Carlo iterations</param>
        ///  <param name="r">Monte Carlo P numerator</param>
        /// <param name="actualIterations">The number of Monte Carlo iterations actually performed</param>
        /// <param name="iseed">RNG seed (0 for automatic)</param>
        ///  <param name="ierror">return non-zero if fault (-1 if interrupted)</param>
        ///  <remarks></remarks>
        private static void Chi2TrendResample(ITemplateHost host, int[,] x, double[] wt, int nrow, int ncol, double x2, int iter, out int r, out int actualIterations, int iseed, ref int ierror)
        {
            int[] ncolt = new int[ncol + 1 /* for VB to C# conversion */];
            int[] nrowt = new int[nrow + 1 /* for VB to C# conversion */ ];
            int ntotal = 0;
            int i;
            int j;
            MersenneTwister rng = new MersenneTwister();

            int bootsDivisor = Math.Max(1, iter / 1000);

            host.StartProgress("Simulating exact P");

            if (iseed != 0)
            {
                rng.Seed(iseed);
            }
            else { rng.Seed(); }

            for (j = 1; j <= nrow; j++)
            {
                for (i = 1; i <= ncol; i++)
                {
                    nrowt[j] += x[j, i];
                    ncolt[i] += x[j, i];
                }
            }

            int maxtot = 5000000;
            bool primed = false;

            double[] fact = new double[1 + 1 /* for VB to C# conversion */ ];
            int[] jwork = new int[1 + 1 /* for VB to C# conversion */ ];

            r = 0;
            for (i = 1; i <= iter; i++)
            {
                if (i % bootsDivisor == 0)
                {
                    if (host.UpdateProgress(i / (double)iter))
                    {
                        ierror = -1; //  Interrupted
                        break;
                    }
                }
                Rcont2(1, nrow, ncol, nrowt, ncolt, ref primed, ref x, ref fact, ref ntotal, ref maxtot, ref jwork, out ierror, ref rng);
                if (Chi2Trend(x, wt, nrow) >= x2)
                {
                    r += 1;
                }
            }
            actualIterations = i - 1;
            host.FinishProgress();
        }

        private static double Chi2Trend(int[,] x, double[] wt, int rows)
        {
            double a = 0, b = 0, t = 0, k1 = 0, k2 = 0, k4 = 0;
            for (int r = 1; r <= rows; r++)
            {
                double a1 = x[r, 1];
                double b1 = x[r, 2];
                double s1 = wt[r];
                a += a1;
                b += b1;
                double t1 = a1 + b1;
                if (t1 <= 0.0)
                {
                    return 0.0;
                }
                t += t1;
                k1 += s1 * a1;
                k2 += s1 * b1;
                k4 += s1 * s1 * (a1 + b1);
            }

            double k8 = b / a;
            double d = (k4 - Math.Pow((k1 + k2), 2.0) / t) / k8;
            if (d < 0)
            {
                d = 0;
            }
            d = Math.Sqrt(d);
            double x1 = (k1 - k2 / k8) / d;

            return x1 * x1;
        }

        ///  <remarks>
        ///     WM Patefield,
        ///     Algorithm AS 159:
        ///     An Efficient Method of Generating RXC Tables with Given Row and Column Totals,
        ///     Applied Statistics, Volume 30, Number 1, 1981, pages 91-97.
        ///  </remarks>
        ///  <param name="lowerBound">0 for 0-based arrays (indices 0..nrow-1, 0..ncol-1); 1 for 1-based arrays (indices 1..nrow, 1..ncol).</param>
        /// <param name="matrix"></param>
        /// <param name="fact">1..(nrow+ncol) to store log-factorials</param>
        /// <param name="nrow"></param>
        /// <param name="ncol"></param>
        /// <param name="nrowt"></param>
        /// <param name="ncolt"></param>
        /// <param name="primed"></param>
        /// <param name="ntotal"></param>
        /// <param name="maxtot"></param>
        /// <param name="jwork"></param>
        /// <param name="ierror"></param>
        /// <param name="rng"></param>
        public static void Rcont2(int lowerBound, int nrow, int ncol, int[] nrowt, int[] ncolt, ref bool primed, ref int[,] matrix, ref double[] fact, ref int ntotal, ref int maxtot, ref int[] jwork, out int ierror, ref MersenneTwister rng)
        {
            ierror = 0;

            //   On user's signal, set up the factorial table.
            if (primed == false)
            {
                primed = true;
                if (nrow <= 1)
                {
                    ierror = 1;
                    return;
                }
                if (ncol <= 1)
                {
                    ierror = 2;
                    return;
                }
                for (int i = lowerBound; i <= nrow - 1 + lowerBound; i++)
                {
                    if (nrowt[i] <= 0)
                    {
                        ierror = 3;
                        return;
                    }
                }
                for (int j = lowerBound; j <= ncol - 1 + lowerBound; j++)
                {
                    if (ncolt[j] <= 0)
                    {
                        ierror = 4;
                        return;
                    }
                }
                int ncolsum = 0;
                int nrowsum = 0;
                for (int i = lowerBound; i <= nrow - 1 + lowerBound; i++)
                {
                    nrowsum += nrowt[i];
                }
                for (int j = lowerBound; j <= ncol - 1 + lowerBound; j++)
                {
                    ncolsum += ncolt[j];
                }
                if (ncolsum != nrowsum)
                {
                    ierror = 6;
                    return;
                }
                ntotal = nrowsum;
                if (maxtot < ntotal)
                {
                    ierror = 5;
                    return;
                }
                fact = new double[ntotal + 1 + 1 /* for VB to C# conversion */ ];
                //   Calculate log-factorials.
                double x = 0.0;
                fact[1] = 0.0;
                for (int i = 1; i <= ntotal; i++)
                {
                    x += Math.Log(i);
                    fact[i + 1] = x;
                }
            }

            //   Construct a random matrix.

            for (int j = lowerBound; j <= ncol - 2 + lowerBound; j++)
            {
                jwork[j] = ncolt[j];
            }

            int jc = ntotal;
            int ib = 0;

            for (int l = lowerBound; l <= nrow - 2 + lowerBound; l++)
            {

                int nrowtl = nrowt[l];
                int ia = nrowtl;
                int ic = jc;
                jc -= nrowtl;

                for (int m = lowerBound; m <= ncol - 2 + lowerBound; m++)
                {

                    int id = jwork[m];
                    int ie = ic;
                    ic -= id;
                    ib = ie - ia;
                    int ii = ib - id;

                    //   Test for zero entries in matrix.

                    if (ie == 0)
                    {
                        ia = 0;
                        for (int j = lowerBound; j <= ncol - 1 + lowerBound; j++)
                        {
                            matrix[l, j] = 0;
                        }
                        break; /* TRANSWARNING: check that break is in correct scope */
                    }

                    //   Generate a pseudo-random number.

                    double r = rng.NextDouble();

                    //   Compute the conditional expected value of MATRIX(L,M).

                    bool done1 = false;
                    bool done2 = false;

                    int nlm;
                    do
                    {

                        nlm = ((int)(Math.Floor(Convert.ToDouble(ia * id) / Convert.ToDouble(ie) + 0.5)));

                        int iap = ia + 1;
                        int idp = id + 1;
                        int igp = idp - nlm;
                        int ihp = iap - nlm;
                        int nlmp = nlm + 1;
                        int iip = ii + nlmp;
                        double x = Math.Exp(fact[iap] + fact[ib + 1] + fact[ic + 1] + fact[idp] - fact[ie + 1] - fact[nlmp] - fact[igp] - fact[ihp] - fact[iip]);

                        if (r <= x)
                        {
                            break; /* TRANSWARNING: check that break is in correct scope */
                        }

                        double sumprb = x;
                        double y = x;
                        int nll = nlm;
                        bool lsp = false;
                        bool lsm = false;

                        //   Increment entry in row L, column M.

                        while (lsp == false)
                        {

                            int j = (id - nlm) * (ia - nlm);

                            if (j == 0)
                                lsp = true;
                            else
                            {

                                nlm += 1;
                                x *= Convert.ToDouble(j) / Convert.ToDouble(nlm * (ii + nlm));
                                sumprb += x;

                                if ((r <= sumprb))
                                {
                                    done1 = true;
                                    break; /* TRANSWARNING: check that break is in correct scope */
                                }

                            }

                            done2 = false;

                            while (lsm == false)
                            {

                                //   Decrement the entry in row L, column M.

                                j = nll * (ii + nll);

                                if (j == 0)
                                {
                                    lsm = true;
                                    break; /* TRANSWARNING: check that break is in correct scope */
                                }

                                nll -= 1;
                                y *= Convert.ToDouble(j) / Convert.ToDouble((id - nll) * (ia - nll));
                                sumprb += y;

                                if ((r <= sumprb))
                                {
                                    nlm = nll;
                                    done2 = true;
                                    break; /* TRANSWARNING: check that break is in correct scope */
                                }

                                if (lsp == false)
                                {
                                    break; /* TRANSWARNING: check that break is in correct scope */
                                }

                            }

                            if (done2)
                            {
                                break; /* TRANSWARNING: check that break is in correct scope */
                            }

                        }

                        if (done1 || done2)
                        {
                            break; /* TRANSWARNING: check that break is in correct scope */
                        }

                        r = rng.NextDouble();
                        r = sumprb * r;

                    }
                    while (true);

                    matrix[l, m] = nlm;
                    ia -= nlm;
                    jwork[m] -= nlm;

                }

                matrix[l, ncol - 1 + lowerBound] = ia;

            }

            //   Compute the last row.
            for (int m = lowerBound; m <= ncol - 2 + lowerBound; m++)
            {
                matrix[nrow - 1 + lowerBound, m] = jwork[m];
            }
            matrix[nrow - 1 + lowerBound, ncol - 1 + lowerBound] = ib - matrix[nrow - 1 + lowerBound, ncol - 2 + lowerBound];
        }
    }
}
