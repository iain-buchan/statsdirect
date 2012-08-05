using System;
using System.Collections.Generic;

using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.Templates;
using StatsDirect.Utilities;

namespace StatsDirect.Builtins
{
    public class Exact
    {
        public static StepResult RptExactSign(ITemplateHost host, ParameterBag parameters)
        {
            double n = parameters["n"].AsDouble;
            double r = parameters["r"].AsDouble;
            if (r > n)
            {
                double temp = r;
                r = n;
                n = temp;
            }
            ParameterBag outputParameters = new ParameterBag();
            if (n > 0.0)
            {
                double acr = r;
                double cco = parameters["cco"].AsDouble;
                if (cco <= 0.0 | cco >= 1.0)
                {
                    cco = 0.95;
                }
                if (r > n / 2.0)
                {
                    r = n - r;
                }
                double f = Math.Pow(0.5, n);
                outputParameters.AddOutput("sample", n.ToString());
                outputParameters.AddOutput("sample_1", acr.ToString());
                if (f > 0.0)
                {
                    double P = f;
                    if (r != 0.0)
                    {
                        long i;
                        for (i = 1; i <= Convert.ToInt64(r); i++)
                        {
                            f = f * (n - Convert.ToDouble(i) + 1.0) / Convert.ToDouble(i);
                            P = P + f;
                        }
                    }
                    double P2 = 2.0 * P;
                    if (P2 > 1.0)
                    {
                        P2 = 1.0;
                    }
                    List<ParameterBag> exactList = new List<ParameterBag>();
                    outputParameters.AddOutput("*exact", exactList);
                    ParameterBag exactParameters = new ParameterBag();
                    exactList.Add(exactParameters);
                    exactParameters.AddOutput("prob_2", host.RoundU(P2));
                    exactParameters.AddOutput("prob_1", host.RoundU(P));
                    outputParameters.AddOutput("*large", null);
                }
                else
                {
                    outputParameters.AddOutput("*exact", null);
                    List<ParameterBag> largeList = new List<ParameterBag>();
                    outputParameters.AddOutput("*large", largeList);
                    largeList.Add(new ParameterBag());
                }

                double D = Math.Abs(n / 2.0 - r) - 0.5;
                double x9;
                if (D < 0.0)
                {
                    x9 = 0.0;
                }
                else { x9 = D / Math.Sqrt(n / 4.0); }

                outputParameters.AddOutput("z", host.RoundU(x9));
                outputParameters.AddOutput("p_2", host.zvalp2(x9));
                outputParameters.AddOutput("p_1", host.zvalp1(x9));

                r = acr;
                outputParameters.AddOutput("ci", Formatting.XRound(cco * 100, 2));

                double piu;
                string warn;
                double pil;
                MathDbl.binci(r, n, out pil, out piu, cco, out warn);

                outputParameters.AddOutput("lower", host.RoundU(pil));
                outputParameters.AddOutput("prop", host.RoundU(r / n));
                outputParameters.AddOutput("upper", host.RoundU(piu) + warn);

            }
            else
            {
                throw new InvalidDataException();
            }
            return new StepResult(StepSuccess.Success, outputParameters);
        }


        public static StepResult RptExactFisher(ITemplateHost host, ParameterBag parameters)
        {
            int fault = 0;
            //  RTF_LoadTemplate("fisher.rtf") Then
            int a = Convert.ToInt32(parameters["a"].AsDouble);
            int b = Convert.ToInt32(parameters["b"].AsDouble);
            int c = Convert.ToInt32(parameters["c"].AsDouble);
            int d = Convert.ToInt32(parameters["d"].AsDouble);
            StepResult outputResult = Tables.s_fisher(host, ref a, ref b, ref c, ref d, ref fault);
            if (fault != 0)
            {
                return new StepResult(StepSuccess.Failed, null);
            }
            return outputResult;
        }


        public static StepResult RptExactFisherX(ITemplateHost host, ParameterBag parameters)
        {
            double ptwo = 0;
            double z_p1 = 0;
            int fault = 0;
            int t;

            int a = Convert.ToInt32(parameters["a"].AsDouble);
            int b = Convert.ToInt32(parameters["b"].AsDouble);
            int C = Convert.ToInt32(parameters["c"].AsDouble);
            int D = Convert.ToInt32(parameters["d"].AsDouble);

            //  RTF_LoadTemplate("fisherx.rtf") Then
            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("tab_a1", a.ToString());
            outputParameters.AddOutput("tab_b1", b.ToString());
            outputParameters.AddOutput("tab_a2", C.ToString());
            outputParameters.AddOutput("tab_b2", D.ToString());

            if (a > D)
            {
                t = a;
                a = D;
                D = t;
            }
            if (b > C)
            {
                t = b;
                b = C;
                C = t;
            }

            int P = a + b;
            int Q = C + D;
            int r = a + C;
            int s = b + D;
            int N = P + Q;

            if (P <= 0 || Q <= 0 || r <= 0 || s <= 0)
            {
                throw new InvalidDataException();
            }

            outputParameters.AddOutput("tab3_a1", a.ToString());
            outputParameters.AddOutput("tab3_b1", b.ToString());
            outputParameters.AddOutput("tab3_c1", P.ToString());
            outputParameters.AddOutput("tab3_a2", C.ToString());
            outputParameters.AddOutput("tab3_b2", D.ToString());
            outputParameters.AddOutput("tab3_c2", Q.ToString());
            outputParameters.AddOutput("tab3_a3", r.ToString());
            outputParameters.AddOutput("tab3_b3", s.ToString());
            outputParameters.AddOutput("tab3_c3", N.ToString());

            double b0 = 1.0;
            double n1 = N;
            double S1 = s;
            do
            {
                if (b0 > 1.0E+300 | S1 <= 0.0)
                {
                    fault = 1;
                    break;
                }
                b0 = b0 * n1 / S1;
                S1 = S1 - 1.0;
                n1 = n1 - 1.0;
            }
            while (n1 > Convert.ToDouble(Q));

            double E1 = Convert.ToDouble(P) * Convert.ToDouble(r) / Convert.ToDouble(N);

            outputParameters.AddOutput("exp_a", E1.ToString());

            List<ParameterBag> headerList = new List<ParameterBag>();
            outputParameters.AddOutput("*header", headerList);
            List<ParameterBag> rowList = new List<ParameterBag>();
            outputParameters.AddOutput("*row", rowList);
            if (fault != 0)
            {
                Tables.fisherp(ref a, ref b, ref C, ref D, ref z_p1, ref ptwo, out fault);
                outputParameters.AddOutput("tail_1", "");
                if (fault != 0)
                {
                    outputParameters.AddOutput("p_1", "err");
                    outputParameters.AddOutput("p_1d", "err");
                    outputParameters.AddOutput("p_2", "err");
                }
                else
                {
                    outputParameters.AddOutput("p_1", host.RoundU(z_p1));
                    outputParameters.AddOutput("p_1d", host.RoundU(z_p1 * 2.0));
                    outputParameters.AddOutput("p_2", host.RoundU(ptwo));
                }
                const string x = "not calculated";
                outputParameters.AddOutput("mid_p", x);
                outputParameters.AddOutput("mid_p_2", x);
            }
            else
            {
                double[] f1 = new double[P + 1 + 1 /* for VB to C# conversion */ ];
                double[] g1 = new double[P + 1 + 1 /* for VB to C# conversion */ ];
                double[] h1 = new double[P + 1 + 1 /* for VB to C# conversion */ ];
                int A1 = 0;
                int Q1 = Q - r;
                int P1 = P;
                int r1 = r;
                double h = 1.0 / b0;
                double f = h;
                f1[1] = f;
                h1[1] = h;
                double g = 1.0;
                g1[1] = 1.0;

                headerList.Add(new ParameterBag());
                ParameterBag rowParameters = new ParameterBag();
                rowList.Add(rowParameters);
                rowParameters.AddOutput("a", A1.ToString());
                rowParameters.AddOutput("lower", Formatting.pr15(g));
                rowParameters.AddOutput("ind_p", Formatting.pr15(h));
                rowParameters.AddOutput("upper", Formatting.pr15(f));
                int A2;
                do
                {
                    A1 = A1 + 1;
                    Q1 = Q1 + 1;
                    h = h * Convert.ToDouble(P1) / Convert.ToDouble(A1) * Convert.ToDouble(r1) / Convert.ToDouble(Q1);
                    f = f + h;
                    A2 = A1 + 1;
                    f1[A2] = f;
                    h1[A2] = h;
                    P1 = P1 - 1;
                    r1 = r1 - 1;
                }
                while (P1 > 0L);

                //   UPPER TAIL PROBABILITIES WOULD BE SUBJECT TO SUBTRACTION ERRORS
                //   IF CALCULATED BY 1 - F. THEREFORE ......

                g = 0.0;
                int j;
                for (j = A2; j >= 2; j--)
                {
                    g = g + h1[j];
                    g1[j] = g;
                }
                // int Start = 1; 
                for (j = 2; j <= A2; j++)
                {
                    rowParameters = new ParameterBag();
                    rowList.Add(rowParameters);
                    rowParameters.AddOutput("a", (j - 1).ToString());
                    rowParameters.AddOutput("lower", Formatting.pr15(f1[j]));
                    rowParameters.AddOutput("ind_p", Formatting.pr15(h1[j]));
                    rowParameters.AddOutput("upper", Formatting.pr15(g1[j]));
                }

                A1 = a + 1;
                h = 1.00000000000001 * h1[A1];
                double mid_p;
                if (a > E1)
                {

                    g = g1[A1];
                    f = 0.0;
                    for (j = 1; j <= A2; j++)
                    {
                        if (h1[j] > h)
                        {
                            break;
                        }
                        f = f1[j];
                    }

                    double g2 = 2.0 * g;
                    if (g2 > 1.0)
                    {
                        g2 = 1.0;
                    }
                    outputParameters.AddOutput("tail_1", "(upper tail)");
                    outputParameters.AddOutput("p_1", host.RoundU(g));
                    outputParameters.AddOutput("p_1d", host.RoundU(g2));
                    mid_p = g - h1[A1] / 2.0;

                }
                else
                {

                    f = f1[A1];
                    g = 0.0;
                    for (j = A2; j >= 1; j--)
                    {
                        if (h1[j] > h)
                        {
                            break;
                        }
                        g = g1[j];
                    }
                    double f2 = 2.0 * f;
                    if (f2 > 1.0)
                    {
                        f2 = 1.0;
                    }
                    outputParameters.AddOutput("tail_1", "(lower tail)");
                    outputParameters.AddOutput("p_1", host.RoundU(f));
                    outputParameters.AddOutput("p_1d", host.RoundU(f2));
                    mid_p = f - h1[A1] / 2.0;

                }

                double z = f + g;
                if (z > 1.0)
                {
                    z = 1.0;
                }
                outputParameters.AddOutput("p_2", host.RoundU(z));

                outputParameters.AddOutput("mid_p", host.RoundU(mid_p));
                outputParameters.AddOutput("mid_p_2", host.RoundU(Math.Min(mid_p * 2.0, 1.0)));

            }
            return new StepResult(StepSuccess.Success, outputParameters);
        }

        public static StepResult RptChiWoolf(ITemplateHost host, ParameterBag parameters)
        {
            int rc;
            bool ierr;

            DataFrame datFrame = parameters["dat"].AsDataFrame;
            DoubleVariable datV0 = datFrame.Variables[0].AsDoubleVariable;
            DoubleVariable datV1 = datFrame.Variables[1].AsDoubleVariable;
            int rows = datFrame.MaxRows;
            if (rows <= 0)
            {
                throw new InvalidDataException();
            }

            double cco = parameters["cco"].AsDouble;
            if (cco <= 0.0 || cco >= 1.0)
            {
                cco = 0.95;
            }
            int fault;
            double cit = PDF.gauinv(cco + (1.0 - cco) / 2.0, out fault);

            bool showIntermediates = parameters["show_intermediates"].AsBoolean;

            int k = rows / 3;
            double[,] o = new double[k + 1 /* for VB to C# conversion */, 5];
            int cnt = 0;
            for (rc = 1; rc <= rows; rc += 2)
            {
                cnt++;
                double rtd = datV0.Data[rc - 1];
                o[cnt, 1] = rtd;
                rtd = datV1.Data[rc - 1];
                o[cnt, 2] = rtd;
                rtd = datV0.Data[rc];
                o[cnt, 3] = rtd;
                rtd = datV1.Data[rc];
                o[cnt, 4] = rtd;
            }

            return Tables.Woolf(host, ref o, ref k, ref showIntermediates, ref cit, ref cco, out ierr);
        }


        public static StepResult RptExactMcNamar(ITemplateHost host, ParameterBag parameters)
        {
            double ul;
            double ll;

            double ba = parameters["a"].AsDouble;
            double bb = parameters["b"].AsDouble;
            double bc = parameters["c"].AsDouble;
            double bd = parameters["d"].AsDouble;
            double GAMMA = parameters["gamma"].AsDouble;
            if (GAMMA <= 0.0 | GAMMA >= 1.0)
            {
                GAMMA = 0.95;
            }

            //  RTF_LoadTemplate("mcnamar.rtf")
            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("tab_a1", Convert.ToInt64(ba).ToString());
            outputParameters.AddOutput("tab_b1", Convert.ToInt64(bb).ToString());
            outputParameters.AddOutput("tab_a2", Convert.ToInt64(bc).ToString());
            outputParameters.AddOutput("tab_b2", Convert.ToInt64(bd).ToString());

            if (bb + bc <= 0.0)
            {
                throw new InvalidDataException();
            }
            double x2 = ((Math.Abs(bb - bc)) * (Math.Abs(bb - bc))) / (bb + bc);
            outputParameters.AddOutput("chi", host.RoundU(x2));
            outputParameters.AddOutput("chi_p", host.pval(PDF.chivalp(x2, 1.0)));

            x2 = Math.Abs(bb - bc) - 1.0;
            double N = bb + bc;
            x2 = x2 * x2 / N;
            outputParameters.AddOutput("yates_chi", host.RoundU(x2));
            outputParameters.AddOutput("yates_chi_p", host.pval(PDF.chivalp(x2, 1.0)));

            string rr = bc > 0.0 ? host.RoundU(bb / bc) : Formatting.INFRES;
            outputParameters.AddOutput("risk", rr);

            double r = bb;
            double s = bc;

            if (r < s)
            {
                Utilities.Utilities.Swap(ref r, ref s);
            }
            double P = (1 - GAMMA) / 2.0;
            double dfn = 2.0 * (s + 1.0);
            double dfd = 2.0 * r;
            double llf = PDF.ffromp(dfd, dfn, P);
            dfn = 2.0 * (r + 1.0);
            dfd = 2.0 * s;
            double ulf = PDF.ffromp(dfd, dfn, P);
            if (llf > 0.0)
            {
                ll = r / ((s + 1.0) * llf);
            }
            else
            {
                ll = Constant.MISSING;
            }
            if (s > 0.0)
            {
                ul = ((r + 1.0) * ulf) / s;
            }
            else
            {
                ul = Constant.MISSING;
            }

            if (bc > bb)
            {
                if (ll != Constant.MISSING)
                {
                    ll = 1.0 / ll;
                }
                if (ul != Constant.MISSING)
                {
                    ul = 1.0 / ul;
                }
            }
            if (ll != Constant.MISSING & ul != Constant.MISSING)
            {
                if (ul < ll)
                {
                    Utilities.Utilities.Swap(ref ll, ref ul);
                }
            }
            outputParameters.AddOutput("pc", (GAMMA * 100).ToString());
            string llx = ll == Constant.MISSING ? Formatting.INFRESNEG : host.RoundU(ll);
            outputParameters.AddOutput("from", llx);
            string ulx = ul == Constant.MISSING ? Formatting.INFRES : host.RoundU(ul);
            outputParameters.AddOutput("to", ulx);

            double f = r / (s + 1.0);
            P = PDF.fvalp(f, 2.0 * (s + 1.0), 2.0 * r) * 2.0;
            if (P > 1.0)
            {
                P = 1.0;
            }

            outputParameters.AddOutput("f", host.RoundU(f));
            outputParameters.AddOutput("tail_2", host.pval(P));
            List<ParameterBag> rPrimeList = new List<ParameterBag>();
            outputParameters.AddOutput("*r_prime", rPrimeList);
            if (P < 0.05)
            {
                rPrimeList.Add(new ParameterBag());
            }

            return new StepResult(StepSuccess.Success, outputParameters);
        }


        public static StepResult RptExactORCML(ITemplateHost host, ParameterBag parameters)
        {
            double obs_or;
            int ierr;
            double p2m = 0;
            double p1m = 0;
            double p2f = 0;
            double p1f = 0;
            double llm;
            double ulm;
            double llf;
            double ulf;
            double eor = 0;
            double[] Table = new double[4 + 1 /* for VB to C# conversion */ ];
            // Gart replaced by CML in May 2001

            double cco = parameters["gamma"].AsDouble;
            if (cco <= 0.0 | cco >= 1.0)
            {
                cco = 0.95;
            }

            Table[1] = parameters["a"].AsDouble;
            Table[2] = parameters["b"].AsDouble;
            Table[3] = parameters["c"].AsDouble;
            Table[4] = parameters["d"].AsDouble;

            //  RTF_LoadTemplate("orci.rtf")
            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("tab_a1", Table[1].ToString());
            outputParameters.AddOutput("tab_b1", Table[2].ToString());
            outputParameters.AddOutput("tab_a2", Table[3].ToString());
            outputParameters.AddOutput("tab_b2", Table[4].ToString());

            ExactBB.Rec2x2[] tabl = new ExactBB.Rec2x2[1 + 1 /* for VB to C# conversion */];
            tabl[1].freq = 1;
            tabl[1].a = Table[1];
            tabl[1].m1 = Table[1] + Table[2];
            tabl[1].n1 = Table[1] + Table[3];
            tabl[1].n0 = Table[2] + Table[4];
            tabl[1].informative = (Table[1] * Table[4] != 0) | (Table[2] * Table[3] != 0);
            bool useLogScale = false;
            ExactBB.Exact22k(host, 1, 1, tabl, cco, ref eor, out ulf, out llf, out ulm, out llm, ref p1f, ref p2f, ref p1m, ref p2m, ref useLogScale, out ierr);

            if (Table[2] * Table[3] > 0)
            {
                obs_or = (Table[1] * Table[4]) / (Table[2] * Table[3]);
            }
            else
            {
                obs_or = double.PositiveInfinity;
            }
            outputParameters.AddOutput("odds", host.RoundU(obs_or));

            if (ierr != 0)
            {
                eor = Constant.MISSING;
                llf = Constant.MISSING;
                ulf = Constant.MISSING;
                p1f = Constant.MISSING;
                p2f = Constant.MISSING;
                llm = Constant.MISSING;
                ulm = Constant.MISSING;
                p1m = Constant.MISSING;
                p2m = Constant.MISSING;
            }

            outputParameters.AddOutput("eor", host.RoundU(eor));
            outputParameters.AddOutput("pc", Formatting.XRound(cco * 100, 2));
            outputParameters.AddOutput("llf", host.RoundU(llf));
            outputParameters.AddOutput("ulf", host.RoundU(ulf));
            outputParameters.AddOutput("p1f", host.pval(p1f));
            outputParameters.AddOutput("p2f", host.pval(p2f));
            outputParameters.AddOutput("llm", host.RoundU(llm));
            outputParameters.AddOutput("ulm", host.RoundU(ulm));
            outputParameters.AddOutput("p1m", host.pval(p1m));
            outputParameters.AddOutput("p2m", host.pval(p2m));
            return new StepResult(StepSuccess.Success, outputParameters);
        }


        public static StepResult RptRatePoissonCI(ITemplateHost host, ParameterBag parameters)
        {
            double cco = parameters["cco"].AsDouble;
            double alpha = 1.0 - cco;
            if (alpha <= 0.0 | alpha >= 1.0)
            {
                alpha = 0.05;
            }
            double revents = parameters["revents"].AsDouble;
            double tar = parameters["tar"].AsDouble;
            if (tar <= 0.0)
            {
                tar = 1.0;
                parameters["tar"] = new FilledParameter(true, 1.0);
            }
            //  RTF_LoadTemplate("prate.rtf")
            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("events", host.RoundU(revents));
            outputParameters.AddOutput("time", host.RoundU(tar));
            outputParameters.AddOutput("rate", host.RoundU(revents / tar));

            outputParameters.AddOutput("pc", Formatting.XRound(cco * 100, 2));

            double xu;
            double xl;
            Rates.poisson_ci(alpha, revents, tar, out xl, out xu);
            outputParameters.AddOutput("from", host.RoundU(xl));
            outputParameters.AddOutput("to", host.RoundU(xu));
            return new StepResult(StepSuccess.Success, outputParameters);
        }
    }
}
