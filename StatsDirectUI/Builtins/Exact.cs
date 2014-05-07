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
                    double p = f;
                    if (r != 0.0)
                    {
                        long i;
                        for (i = 1; i <= Convert.ToInt64(r); i++)
                        {
                            f = f * (n - Convert.ToDouble(i) + 1.0) / Convert.ToDouble(i);
                            p = p + f;
                        }
                    }
                    double p2 = 2.0 * p;
                    if (p2 > 1.0)
                    {
                        p2 = 1.0;
                    }
                    List<ParameterBag> exactList = new List<ParameterBag>();
                    outputParameters.AddOutput("*exact", exactList);
                    ParameterBag exactParameters = new ParameterBag();
                    exactList.Add(exactParameters);
                    exactParameters.AddOutput("prob_2", host.pval(p2));
                    exactParameters.AddOutput("prob_1", host.pval(p));
                    outputParameters.AddOutput("*large", null);
                }
                else
                {
                    outputParameters.AddOutput("*exact", null);
                    List<ParameterBag> largeList = new List<ParameterBag>();
                    outputParameters.AddOutput("*large", largeList);
                    largeList.Add(new ParameterBag());
                }

                double d = Math.Abs(n / 2.0 - r) - 0.5;
                double x9;
                if (d < 0.0)
                {
                    x9 = 0.0;
                }
                else { x9 = d / Math.Sqrt(n / 4.0); }

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
            StepResult outputResult = Tables.SFisher(host, ref a, ref b, ref c, ref d, ref fault);
            if (fault != 0)
            {
                return new StepResult(StepSuccess.Failed, null);
            }
            return outputResult;
        }


        public static StepResult RptExactFisherX(ITemplateHost host, ParameterBag parameters)
        {
            int fault = 0;
            int t;

            int a = Convert.ToInt32(parameters["a"].AsDouble);
            int b = Convert.ToInt32(parameters["b"].AsDouble);
            int c = Convert.ToInt32(parameters["c"].AsDouble);
            int d = Convert.ToInt32(parameters["d"].AsDouble);

            //  RTF_LoadTemplate("fisherx.rtf") Then
            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("tab_a1", a.ToString());
            outputParameters.AddOutput("tab_b1", b.ToString());
            outputParameters.AddOutput("tab_a2", c.ToString());
            outputParameters.AddOutput("tab_b2", d.ToString());

            if (a > d)
            {
                t = a;
                a = d;
                d = t;
            }
            if (b > c)
            {
                t = b;
                b = c;
                c = t;
            }

            int p = a + b;
            int q = c + d;
            int r = a + c;
            int s = b + d;
            int n = p + q;

            if (p <= 0 || q <= 0 || r <= 0 || s <= 0)
            {
                throw new InvalidDataException();
            }

            outputParameters.AddOutput("tab3_a1", a.ToString());
            outputParameters.AddOutput("tab3_b1", b.ToString());
            outputParameters.AddOutput("tab3_c1", p.ToString());
            outputParameters.AddOutput("tab3_a2", c.ToString());
            outputParameters.AddOutput("tab3_b2", d.ToString());
            outputParameters.AddOutput("tab3_c2", q.ToString());
            outputParameters.AddOutput("tab3_a3", r.ToString());
            outputParameters.AddOutput("tab3_b3", s.ToString());
            outputParameters.AddOutput("tab3_c3", n.ToString());

            double b0 = 1.0;
            double n1 = n;
            double s1 = s;
            do
            {
                if (b0 > 1.0E+300 | s1 <= 0.0)
                {
                    fault = 1;
                    break;
                }
                b0 = b0 * n1 / s1;
                s1 = s1 - 1.0;
                n1 = n1 - 1.0;
            }
            while (n1 > Convert.ToDouble(q));

            double e1 = Convert.ToDouble(p) * Convert.ToDouble(r) / Convert.ToDouble(n);

            outputParameters.AddOutput("exp_a", e1.ToString());

            List<ParameterBag> headerList = new List<ParameterBag>();
            outputParameters.AddOutput("*header", headerList);
            List<ParameterBag> rowList = new List<ParameterBag>();
            outputParameters.AddOutput("*row", rowList);
            if (fault != 0)
            {
                double ptwo;
                double zP1;
                Tables.Fisherp(a, b, c, d, out zP1, out ptwo, out fault);
                outputParameters.AddOutput("tail_1", "");
                if (fault != 0)
                {
                    outputParameters.AddOutput("p_1", "err");
                    outputParameters.AddOutput("p_1d", "err");
                    outputParameters.AddOutput("p_2", "err");
                }
                else
                {
                    outputParameters.AddOutput("p_1", host.RoundU(zP1));
                    outputParameters.AddOutput("p_1d", host.RoundU(zP1 * 2.0));
                    outputParameters.AddOutput("p_2", host.RoundU(ptwo));
                }
                const string x = "not calculated";
                outputParameters.AddOutput("mid_p", x);
                outputParameters.AddOutput("mid_p_2", x);
            }
            else
            {
                double[] f1 = new double[p + 2 ];
                double[] g1 = new double[p + 2 ];
                double[] h1 = new double[p + 2 ];
                int a1 = 0;
                int q1 = q - r;
                int p1 = p;
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
                rowParameters.AddOutput("a", a1.ToString());
                rowParameters.AddOutput("lower", Formatting.pr15(g));
                rowParameters.AddOutput("ind_p", Formatting.pr15(h));
                rowParameters.AddOutput("upper", Formatting.pr15(f));
                int a2;
                do
                {
                    a1 = a1 + 1;
                    q1 = q1 + 1;
                    h = h * Convert.ToDouble(p1) / Convert.ToDouble(a1) * Convert.ToDouble(r1) / Convert.ToDouble(q1);
                    f = f + h;
                    a2 = a1 + 1;
                    f1[a2] = f;
                    h1[a2] = h;
                    p1 = p1 - 1;
                    r1 = r1 - 1;
                }
                while (p1 > 0L);

                //   UPPER TAIL PROBABILITIES WOULD BE SUBJECT TO SUBTRACTION ERRORS
                //   IF CALCULATED BY 1 - F. THEREFORE ......

                g = 0.0;
                int j;
                for (j = a2; j >= 2; j--)
                {
                    g = g + h1[j];
                    g1[j] = g;
                }
                // int Start = 1; 
                for (j = 2; j <= a2; j++)
                {
                    rowParameters = new ParameterBag();
                    rowList.Add(rowParameters);
                    rowParameters.AddOutput("a", (j - 1).ToString());
                    rowParameters.AddOutput("lower", Formatting.pr15(f1[j]));
                    rowParameters.AddOutput("ind_p", Formatting.pr15(h1[j]));
                    rowParameters.AddOutput("upper", Formatting.pr15(g1[j]));
                }

                a1 = a + 1;
                h = 1.00000000000001 * h1[a1];
                double midP;
                if (a > e1)
                {

                    g = g1[a1];
                    f = 0.0;
                    for (j = 1; j <= a2; j++)
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
                    midP = g - h1[a1] / 2.0;

                }
                else
                {

                    f = f1[a1];
                    g = 0.0;
                    for (j = a2; j >= 1; j--)
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
                    midP = f - h1[a1] / 2.0;

                }

                double z = f + g;
                if (z > 1.0)
                {
                    z = 1.0;
                }
                outputParameters.AddOutput("p_2", host.RoundU(z));

                outputParameters.AddOutput("mid_p", host.RoundU(midP));
                outputParameters.AddOutput("mid_p_2", host.RoundU(Math.Min(midP * 2.0, 1.0)));

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
            double[,] o = new double[k + 1, 5];
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

            return Tables.Woolf(host, o, k, showIntermediates, cit, cco, out ierr);
        }


        public static StepResult RptExactMcNamar(ITemplateHost host, ParameterBag parameters)
        {
            double ul;
            double ll;

            double ba = parameters["a"].AsDouble;
            double bb = parameters["b"].AsDouble;
            double bc = parameters["c"].AsDouble;
            double bd = parameters["d"].AsDouble;
            double gamma = parameters["gamma"].AsDouble;
            if (gamma <= 0.0 || gamma >= 1.0)
            {
                gamma = 0.95;
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
            double n = bb + bc;
            x2 = x2 * x2 / n;
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
            double p = (1 - gamma) / 2.0;
            double dfn = 2.0 * (s + 1.0);
            double dfd = 2.0 * r;
            double llf = PDF.ffromp(dfd, dfn, p);
            dfn = 2.0 * (r + 1.0);
            dfd = 2.0 * s;
            double ulf = PDF.ffromp(dfd, dfn, p);
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
            outputParameters.AddOutput("pc", (gamma * 100).ToString());
            string llx = ll == Constant.MISSING ? Formatting.INFRESNEG : host.RoundU(ll);
            outputParameters.AddOutput("from", llx);
            string ulx = ul == Constant.MISSING ? Formatting.INFRES : host.RoundU(ul);
            outputParameters.AddOutput("to", ulx);

            double f = r / (s + 1.0);
            p = PDF.fvalp(f, 2.0 * (s + 1.0), 2.0 * r) * 2.0;
            if (p > 1.0)
            {
                p = 1.0;
            }

            outputParameters.AddOutput("f", host.RoundU(f));
            outputParameters.AddOutput("tail_2", host.pval(p));
            List<ParameterBag> rPrimeList = new List<ParameterBag>();
            outputParameters.AddOutput("*r_prime", rPrimeList);
            if (p < 0.05)
            {
                rPrimeList.Add(new ParameterBag());
            }

            return new StepResult(StepSuccess.Success, outputParameters);
        }


        public static StepResult RptExactORCML(ITemplateHost host, ParameterBag parameters)
        {
            double odr;
            int ierr;
            double p2m;
            double p1m;
            double p2f;
            double p1f;
            double llm;
            double ulm;
            double llf;
            double ulf;
            double eor = 0;
            double a,b,c,d;
            // Gart replaced by CML in May 2001

            double cco = parameters["gamma"].AsDouble;
            if (cco <= 0.0 || cco >= 1.0)
            {
                cco = 0.95;
            }

            a = parameters["a"].AsDouble;
            b = parameters["b"].AsDouble;
            c = parameters["c"].AsDouble;
            d = parameters["d"].AsDouble;

            //  RTF_LoadTemplate("orci.rtf")
            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("tab_a1", a.ToString());
            outputParameters.AddOutput("tab_b1", b.ToString());
            outputParameters.AddOutput("tab_a2", c.ToString());
            outputParameters.AddOutput("tab_b2", d.ToString());

            //ExactBB.Rec2X2[] tabl = new ExactBB.Rec2X2[1 + 1];
            //tabl[1].Freq = 1;
            //tabl[1].A = table[1];
            //tabl[1].M1 = table[1] + table[2];
            //tabl[1].N1 = table[1] + table[3];
            //tabl[1].N0 = table[2] + table[4];
            //tabl[1].Informative = (table[1] * table[4] != 0) | (table[2] * table[3] != 0);
            //bool useLogScale = false;
            //new ExactBB().Exact22K(host, 1, 1, tabl, cco, ref eor, out ulf, out llf, out ulm, out llm, out p1F, out p2F, out p1M, out p2M, ref useLogScale, out ierr);

            //if (table[2] * table[3] > 0)
            //{
            //    obsOr = (table[1] * table[4]) / (table[2] * table[3]);
            //}
            //else
            //{
            //    obsOr = double.PositiveInfinity;
            //}
            ExactBB.OddsRatioCMLE(host, cco, a, b, c, d, ref eor, out llf, out ulf, out llm, out ulm, out p1f, out p2f, out p1m, out p2m, out ierr);
            odr = ExactBB.OddsRatio(a, b, c, d);
            outputParameters.AddOutput("odds", host.RoundU(odr));

            //if (ierr != 0)
            //{
            //    eor = Constant.MISSING;
            //    llf = Constant.MISSING;
            //    ulf = Constant.MISSING;
            //    p1f = Constant.MISSING;
            //    p2f = Constant.MISSING;
            //    llm = Constant.MISSING;
            //    ulm = Constant.MISSING;
            //    p1m = Constant.MISSING;
            //    p2m = Constant.MISSING;
            //}

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
