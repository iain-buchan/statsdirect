using System;
using System.Collections.Generic;
using StatsDirect.Utilities;
using StatsDirect.Templates;
using StatsDirect.Numerics;
using StatsDirect.Data;
using StatsDirect.Charting;

namespace StatsDirect.Builtins
{
    public static class Parametric
    {
        private static void univariate(double[] arr1, int nx, out double sum, out double mean, out double var)
        {
            sum = 0;
            double sumsqdev = 0;
            for (int N = 1; N <= nx; N++)
            {
                sum = sum + arr1[N];
            }
            mean = sum / Convert.ToDouble(nx);
            for (int N = 1; N <= nx; N++)
            {
                if (Math.Abs(sumsqdev) > 1.0E+300)
                {
                    sumsqdev = Constant.MISSING;
                    break;
                }
                sumsqdev = sumsqdev + (arr1[N] - mean) * (arr1[N] - mean);
            }
            if (sumsqdev == Constant.MISSING)
            {
                var = Constant.MISSING;
            }
            else
            {
                var = sumsqdev / Convert.ToDouble(nx - 1);
            }
        }


        private static void para(DataFrame Frame, double[] mean, double[] ss, double[] var, double[] sd, double[] sem, int[] tnx)
        {
            for (int D = 0; D <= Frame.VariableCount - 1; D++)
            {
                int nx = 0;
                double sum = 0.0;
                double sumsq = 0.0;
                foreach (double v in (Frame.Variables[D] as DoubleVariable).Data)
                {
                    if (v != Constant.MISSING)
                    {
                        nx = nx + 1;
                        sum = sum + v;
                        sumsq = sumsq + (v * v);
                    }
                }
                tnx[D] = nx;
                mean[D] = sum / Convert.ToDouble(tnx[D]);
                ss[D] = sumsq - ((sum * sum) / Convert.ToDouble(tnx[D]));
                double sumsqdev = 0.0;
                foreach (double v in (Frame.Variables[D] as DoubleVariable).Data)
                {
                    if (v != Constant.MISSING)
                    {
                        if (Math.Abs(sumsqdev) > 1.0E+300)
                        {
                            sumsqdev = Constant.MISSING;
                            break;
                        }
                        sumsqdev = sumsqdev + (v - mean[D]) * (v - mean[D]);
                    }
                }
                if (sumsqdev == Constant.MISSING)
                {
                    var[D] = Constant.MISSING;
                }
                else
                {
                    var[D] = sumsqdev / Convert.ToDouble(tnx[D] - 1);
                }
                sd[D] = Math.Sqrt(var[D]);
                sem[D] = sd[D] / Math.Sqrt(Convert.ToDouble(tnx[D]));
            }
        }


        public static ParameterBag RptVarianceRatio(ITemplateHost host, ParameterBag parameters)
        {
            int bot; int top;

            double[] mean = new double[1 + 1 /* VB to C# conversion */ ];
            double[] ss = new double[1 + 1 /* VB to C# conversion */ ];
            double[] var = new double[1 + 1 /* VB to C# conversion */ ];
            double[] sd = new double[1 + 1 /* VB to C# conversion */ ];
            double[] sem = new double[1 + 1 /* VB to C# conversion */ ];
            int[] tnx = new int[1 + 1 /* VB to C# conversion */ ];
            DataFrame data = parameters["data"].AsDataFrame;
            // RTF_LoadTemplate("variance.rtf")
            para(data, mean, ss, var, sd, sem, tnx);
            if (Math.Abs(var[0]) > Math.Abs(var[1]))
            {
                top = 0;
                bot = 1;
            }
            else
            {
                top = 1;
                bot = 0;
            }
            double f = var[top] / var[bot];
            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("title_0", data.Variables[top].Title);
            outputParameters.AddOutput("df_0", (tnx[top] - 1).ToString());
            outputParameters.AddOutput("var_0", host.RoundU(var[top]));
            outputParameters.AddOutput("title_1", data.Variables[bot].Title);
            outputParameters.AddOutput("df_1", (tnx[bot] - 1).ToString());
            outputParameters.AddOutput("var_1", host.RoundU(var[bot]));
            outputParameters.AddOutput("f", host.RoundU(f));
            double P = PDF.fvalp(f, Convert.ToDouble(tnx[top] - 1), Convert.ToDouble(tnx[bot] - 1));
            outputParameters.AddOutput("p_1", host.pval(P));
            if (P > 0.5)
            {
                P = 0.5;
            }
            outputParameters.AddOutput("p_2", host.pval(P * 2));
            return outputParameters;
        }

        public static ParameterBag RptReferenceRange(ITemplateHost host, ParameterBag parameters)
        {
            double cover = 0; double ul; double ll; double xq = 0;
            double o = 0;
            double P0; double z;
            int j; int k = 0;
            int fault;
            bool capUpper = false; bool capLower = false;

            double[] mean = new double[2];
            double[] ss = new double[2];
            double[] var = new double[2];
            double[] sd = new double[2];
            double[] sem = new double[2];
            int[] tnx = new int[2];

            DataFrame data = parameters["data"].AsDataFrame;
            DoubleVariable variable = data.Variables[0]as DoubleVariable;
            if (variable.Data.Length < 8)
            {
                host.Error("Too few data for this method (minimum 8)", "Reference Range");
                throw new TemplateOperationCancelledException();
            }
            bool do_conservative = parameters["do_conservative"].AsBoolean;
            double GAMMA = parameters["gamma"].AsDouble;
            if (GAMMA <= 0.0 || GAMMA >= 1.0)
            {
                GAMMA = 0.95;
            }
            double qrr = parameters["reference-interval"].AsDouble;
            double qrz = Math.Abs(PDF.gauinv((1.0 - qrr) / 2.0, out fault));
            if (fault != 0 || qrr < 0.0 || qrr > 1.0)
            {
                host.Error("Coverage not possible.", "Reference Range");
                throw new TemplateOperationCancelledException();
            }
            //  RTF_LoadTemplate("refrange.rtf")
            MathDbl.civ(0, out z, GAMMA, out P0);
            para(data, mean, ss, var, sd, sem, tnx);
            double xbar = mean[0];
            double s = sd[((int)(Math.Floor(o)))];
            int N = tnx[0];
            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("name", variable.Title);
            outputParameters.AddOutput("mean", host.RoundU(xbar));
            outputParameters.AddOutput("size", N.ToString());
            outputParameters.AddOutput("sd", host.RoundU(s));
            // normal version
            outputParameters.AddOutput("qrr", Formatting.XRound(100.0 * qrr, 2));
            double lrr = xbar - qrz * s;
            double urr = xbar + qrz * s;
            outputParameters.AddOutput("lrr", host.RoundU(lrr));
            outputParameters.AddOutput("urr", host.RoundU(urr));
            double serr = Math.Sqrt((s * s) / Convert.ToDouble(N) + (qrz * qrz * s * s) / (2.0 * Convert.ToDouble(N)));
            double lx = lrr - serr * z;
            double ux = lrr + serr * z;
            outputParameters.AddOutput("pc", Formatting.XRound(100.0 * GAMMA, 2));
            outputParameters.AddOutput("lx_l", host.RoundU(lx));
            outputParameters.AddOutput("ux_l", host.RoundU(ux));
            lx = urr - serr * z;
            ux = urr + serr * z;
            outputParameters.AddOutput("lx_u", host.RoundU(lx));
            outputParameters.AddOutput("ux_u", host.RoundU(ux));

            // log-normal version
            double[] r = new double[variable.Length + 1 /* VB to C# conversion */ ];
            double sum = 0;
            double sumsq = 0;
            N = 0;
            bool ok = true;
            for (j = 0; j <= data.Variables[k].Length - 1; j++)
            {
                double v = (data.Variables[k] as DoubleVariable).Data[j];
                if (v != Constant.MISSING)
                {
                    if (v >= 0.0)
                    {
                        N = N + 1;
                        r[N] = Math.Log(variable.Data[j]);
                        sum = sum + r[N];
                        sumsq = sumsq + r[N] * r[N];
                    }
                    else
                    {
                        ok = false;
                        break;
                    }
                }
            }
            if (!(ok))
            {
                outputParameters.AddOutput("*lognormal", null);
            }
            else
            {
                IList<ParameterBag> lognormalList = new List<ParameterBag>();
                outputParameters.AddOutput("*lognormal", lognormalList);
                ParameterBag lognormalParameters = new ParameterBag();
                lognormalList.Add(lognormalParameters);
                xbar = sum / Convert.ToDouble(N);
                double sumsqdev = 0.0;
                for (j = 1; j <= N; j++)
                {
                    if (Math.Abs(sumsqdev) > 1.0E+300)
                    {
                        sumsqdev = Constant.MISSING;
                        break;
                    }
                    sumsqdev = sumsqdev + (r[j] - xbar) * (r[j] - xbar);
                }
                s = sumsqdev == Constant.MISSING ? Constant.MISSING : Math.Sqrt(sumsqdev / Convert.ToDouble(N - 1));
                lrr = xbar - qrz * s;
                urr = xbar + qrz * s;
                lognormalParameters.AddOutput("lrr_lognormal", host.RoundU(Math.Exp(lrr)));
                lognormalParameters.AddOutput("urr_lognormal", host.RoundU(Math.Exp(urr)));
                serr = Math.Sqrt((s * s) / Convert.ToDouble(N) + (qrz * qrz * s * s) / (2.0 * Convert.ToDouble(N)));
                lx = Math.Exp(lrr - serr * z);
                ux = Math.Exp(lrr + serr * z);
                lognormalParameters.AddOutput("lx_l_lognormal", host.RoundU(lx));
                lognormalParameters.AddOutput("ux_l_lognormal", host.RoundU(ux));
                lx = Math.Exp(urr - serr * z);
                ux = Math.Exp(urr + serr * z);
                lognormalParameters.AddOutput("lx_u_lognormal", host.RoundU(lx));
                lognormalParameters.AddOutput("ux_u_lognormal", host.RoundU(ux));
            }
            // percentile version
            r = new double[variable.Length + 1 /* VB to C# conversion */ ];
            int rx = 0;
            for (j = 0; j <= data.Variables[k].Length - 1; j++)
            {
                if ((data.Variables[k] as DoubleVariable).Data[j] != Constant.MISSING)
                {
                    rx = rx + 1;
                    r[rx] = (data.Variables[k] as DoubleVariable).Data[j];
                }
            }
            Array.Sort(r, 1, rx);
            double qc = (1.0 - qrr) / 2.0;
            Nonparametric.XQci(qc, rx, r, ref xq, GAMMA, out ll, out ul, ref cover, do_conservative, ref capUpper, ref capLower, out fault);
            outputParameters.AddOutput("qx_any", host.RoundU(qc));
            outputParameters.AddOutput("qxv_any", host.RoundU(xq));
            string contype = do_conservative ? "(conservative)" : "(non-conservative)";
            outputParameters.AddOutput("type", contype);
            string x = capLower ? "* " : string.Empty;
            outputParameters.AddOutput("from_any", x + host.RoundU(ll));
            x = capUpper ? "* " : string.Empty;
            outputParameters.AddOutput("to_any", x + host.RoundU(ul));
            x = capLower | capUpper ? "  (* limit capped at min/max)" : string.Empty;
            outputParameters.AddOutput("co_any", host.RoundU(cover) + "%" + x);
            qc = 1.0 - ((1.0 - qrr) / 2.0);
            Nonparametric.XQci(qc, rx, r, ref xq, GAMMA, out ll, out ul, ref cover, do_conservative, ref capUpper, ref capLower, out fault);
            outputParameters.AddOutput("qx", host.RoundU(qc));
            outputParameters.AddOutput("qxv", host.RoundU(xq));
            x = capLower ? "* " : string.Empty;
            outputParameters.AddOutput("from", x + host.RoundU(ll));
            x = capUpper ? "* " : string.Empty;
            outputParameters.AddOutput("to", x + host.RoundU(ul));
            x = capLower | capUpper ? "  (* limit capped at min/max)" : string.Empty;
            outputParameters.AddOutput("co", host.RoundU(cover) + "%" + x);

            return outputParameters;
        }


        private static void x_poisson(double[] x, int nobs, double percent, out double mean, out double tlower, out double tupper, ref int fault)
        {
            mean = Constant.MISSING;
            tlower = Constant.MISSING;
            tupper = Constant.MISSING;
            if (nobs < 1)
            {
                fault = 1;
                return;
            }
            double sum = 0.0;
            for (int i = 1; i <= nobs; i++)
            {
                if (x[i] < 0)
                {
                    fault = 2;
                    return;
                }
                sum += x[i];
            }
            double dn = nobs;
            mean = sum / dn;
            if (percent <= 0.0 | percent >= 100.0)
            {
                fault = 3;
                return;
            }
            double al1 = 0.005 * (100.0 - percent);
            double al2 = 1.0 - al1;
            if (sum <= 0.0)
            {
                tupper = -Math.Log(al1) / dn;
                tlower = 0.0;
            }
            else
            {
                double df1 = 2.0 * sum;
                double chi2;
                if (sum > 0.9 & sum < 1.1)
                {
                    tlower = -Math.Log(al2) / dn;
                }
                else
                {
                    chi2 = PDF.ppchi2(al1, df1, out fault);
                    if (fault != 0)
                        return;
                    tlower = chi2 * 0.5 / dn;
                }
                double df2 = df1 + 2.0;
                chi2 = PDF.ppchi2(al2, df2, out fault);
                if (fault != 0)
                    return;
                tupper = chi2 * 0.5 / dn;
            }
        }


        public static ParameterBag rptPoissonConfidenceInterval(ITemplateHost host, ParameterBag parameters)
        {
            DataFrame data = parameters["data"].AsDataFrame;
            double percent2 = parameters["gamma"].AsDouble * 100.0;
            if (percent2 <= 0.0 || percent2 >= 100.0)
                percent2 = 95.0;
            double percent1 = 100.0 - 2.0 * (100.0 - percent2);

            ParameterBag outputParameters = new ParameterBag();
            IList<ParameterBag> sampleList = new List<ParameterBag>();
            outputParameters.AddOutput("*sample", sampleList);
            foreach (Variable varbl in data.Variables)
            {
                DoubleVariable variable = varbl as DoubleVariable;
                double[] x = new double[variable.Length + 1 ];
                int nobs = 0;
                bool not_int = false;
                bool non_neg = false;
                foreach (double v in variable.Data)
                {
                    if (v != Constant.MISSING)
                    {
                        nobs++;
                        x[nobs] = v;
                        if (x[nobs] != Math.Floor(v))
                            not_int = true;
                        if (x[nobs] < 0)
                            non_neg = true;
                    }
                }
                ParameterBag sampleParameters = new ParameterBag();
                sampleList.Add(sampleParameters);
                sampleParameters.AddOutput("ti", variable.Title);
                string wrn = non_neg
                    ? "(error - negative values used)"
                    : (not_int
                        ? "(warning - source data not integers)"
                        : string.Empty);
                sampleParameters.AddOutput("warn", wrn);
                sampleParameters.AddOutput("n", nobs.ToString());

                int fault = 0;
                double tupper2; double tlower2; double that;
                x_poisson(x, nobs, percent2, out that, out tlower2, out tupper2, ref fault);
                if (fault != 0)
                {
                    tlower2 = Constant.MISSING;
                    tupper2 = Constant.MISSING;
                }
                double tupper1; double tlower1;
                x_poisson(x, nobs, percent1, out that, out tlower1, out tupper1, ref fault);
                if (fault != 0)
                {
                    that = Constant.MISSING;
                    tlower1 = Constant.MISSING;
                    tupper1 = Constant.MISSING;
                }
                sampleParameters.AddOutput("mean", host.RoundU(that));
                sampleParameters.AddOutput("pc2", Math.Round(percent2, 1).ToString());
                sampleParameters.AddOutput("pc1", Math.Round(percent1, 1).ToString());
                sampleParameters.AddOutput("lower2", host.RoundU(tlower2));
                sampleParameters.AddOutput("upper2", host.RoundU(tupper2));
                sampleParameters.AddOutput("lower1", host.RoundU(tlower1));
                sampleParameters.AddOutput("upper1", host.RoundU(tupper1));
            }
            return outputParameters;
        }

        public static ParameterBag RptZSingle(ITemplateHost host, ParameterBag parameters)
        {
            return RptNormalZ(host, parameters, 1);
        }


        public static ParameterBag RptZUnpaired(ITemplateHost host, ParameterBag parameters)
        {
            return RptNormalZ(host, parameters, 2);
        }


        private static ParameterBag RptNormalZ(ITemplateHost host, ParameterBag parameters, int mode)
        {
            double gsumsq = 0;
            double gsum = 0;
            double P; double statz; double P0; double cit;

            double[] mean = new double[1 + 1 /* VB to C# conversion */ ];
            double[] ss = new double[1 + 1 /* VB to C# conversion */ ];
            double[] var = new double[1 + 1 /* VB to C# conversion */ ];
            double[] sd = new double[1 + 1 /* VB to C# conversion */ ];
            double[] sem = new double[1 + 1 /* VB to C# conversion */ ];
            int[] tnx = new int[1 + 1 /* VB to C# conversion */ ];

            double GAMMA = parameters["gamma"].AsDouble;
            if (mode == 2)
            {
                DataFrame Data = parameters["data"].AsDataFrame;
                //  RTF_LoadTemplate("z_norm2.rtf")
                MathDbl.civ(0, out cit, GAMMA, out P0);
                para(Data, mean, ss, var, sd, sem, tnx);
                ParameterBag outputParameters = new ParameterBag();
                IList<ParameterBag> sampleList = new List<ParameterBag>();
                outputParameters.AddOutput("*sample", sampleList);
                int D;
                for (D = 0; D <= 1; D++)
                {
                    ParameterBag sampleParameters = new ParameterBag();
                    sampleList.Add(sampleParameters);
                    sampleParameters.AddOutput("name", Data.Variables[D].Title);
                    sampleParameters.AddOutput("mean", host.RoundU(mean[D]));
                    sampleParameters.AddOutput("var", host.RoundU(var[D]));
                    sampleParameters.AddOutput("size", tnx[D].ToString());
                }
                double cse = Math.Sqrt((var[0] / tnx[0]) + (var[1] / tnx[1]));
                outputParameters.AddOutput("error", host.RoundU(cse));
                outputParameters.AddOutput("pc", Formatting.XRound(100 * (1 - P0), 2));
                outputParameters.AddOutput("from", host.RoundU((mean[0] - mean[1]) - (cse * cit)));
                outputParameters.AddOutput("to", host.RoundU((mean[0] - mean[1]) + (cse * cit)));
                statz = ((mean[0] - mean[1]) / cse);
                outputParameters.AddOutput("z", host.RoundU(statz));
                P = 1.0 - PDF.alnorm(Math.Abs(statz));
                if (P > 1 - P)
                {
                    P = 1 - P;
                }
                outputParameters.AddOutput("p_1", host.pval(P));
                outputParameters.AddOutput("p_2", host.pval(P * 2));
                if (tnx[0] < 30 | tnx[1] < 30)
                {
                    IList<ParameterBag> warnList = new List<ParameterBag>();
                    warnList.Add(new ParameterBag());
                    outputParameters.AddOutput("*warn", warnList);
                }
                else
                {
                    outputParameters.AddOutput("*warn", null);
                }
                return outputParameters;
            }
            else
            {
                DataFrame Data = parameters["data"].AsDataFrame;
                DoubleVariable v0 = Data.Variables[0]as DoubleVariable;
                double pm = parameters["popmean"].AsDouble;
                double psd = parameters.ContainsKey("popsd") && parameters["popsd"] != null
                                 ? parameters["popsd"].AsDouble
                                 : Constant.MISSING;
                //  RTF_LoadTemplate("z_norm1.rtf")
                MathDbl.civ(0, out cit, GAMMA, out P0);
                para(Data, mean, ss, var, sd, sem, tnx);
                ParameterBag outputParameters = new ParameterBag();
                outputParameters.AddOutput("name", v0.Title);
                outputParameters.AddOutput("mean", host.RoundU(mean[0]));
                outputParameters.AddOutput("pop_mean", host.RoundU(pm));
                outputParameters.AddOutput("size", tnx[0].ToString());
                outputParameters.AddOutput("sd", host.RoundU(sd[0]));
                string tmp;
                if (psd == Constant.MISSING || psd == 0.0)
                {
                    tmp = "not known";
                    statz = (mean[0] - pm) / (sd[0] / Math.Sqrt(tnx[0]));
                }
                else
                {
                    tmp = host.RoundU(psd);
                    statz = (mean[0] - pm) / (psd / Math.Sqrt(tnx[0]));
                }
                outputParameters.AddOutput("psd", tmp);
                outputParameters.AddOutput("pc", Formatting.XRound(100 * (1 - P0), 2));
                outputParameters.AddOutput("for", pm == 0 ? "for the mean" : "for mean difference");
                outputParameters.AddOutput("from", host.RoundU((mean[0] - pm) - (cit * sem[0])));
                outputParameters.AddOutput("to", host.RoundU((mean[0] - pm) + (cit * sem[0])));
                outputParameters.AddOutput("z", host.RoundU(statz));
                P = 1.0 - PDF.alnorm(Math.Abs(statz));
                if (P > 1 - P)
                {
                    P = 1 - P;
                }
                outputParameters.AddOutput("p_1", host.pval(P));
                outputParameters.AddOutput("p_2", host.pval(P * 2));
                int nx = 0;
                foreach (double val in v0.Data)
                {
                    if (val != Constant.MISSING & val > 0)
                    {
                        nx = nx + 1;
                        double logVal = Math.Log(val);
                        gsum = gsum + logVal;
                        gsumsq = gsumsq + (logVal * logVal);
                    }
                }
                double urr;
                double lrr;
                double gmean;
                if (nx > 1)
                {
                    gmean = gsum / Convert.ToDouble(nx);
                    double gss = gsumsq - ((gsum * gsum) / Convert.ToDouble(nx));
                    double gvar = gss / (Convert.ToDouble(nx - 1));
                    double gsd = Math.Sqrt(gvar);
                    lrr = gmean - gsd * cit;
                    urr = gmean + gsd * cit;
                    gmean = Math.Exp(gmean);
                    lrr = Math.Exp(lrr);
                    urr = Math.Exp(urr);
                }
                else
                {
                    gmean = Constant.MISSING;
                    lrr = Constant.MISSING;
                    urr = Constant.MISSING;
                }
                outputParameters.AddOutput("gmean", host.RoundU(gmean));
                outputParameters.AddOutput("pc2", Formatting.XRound(100 * (1 - P0), 2));
                outputParameters.AddOutput("lrr", host.RoundU(lrr));
                outputParameters.AddOutput("urr", host.RoundU(urr));
                if (tnx[0] < 30)
                {
                    IList<ParameterBag> warnList = new List<ParameterBag>();
                    warnList.Add(new ParameterBag());
                    outputParameters.AddOutput("*warn", warnList);
                }
                else
                {
                    outputParameters.AddOutput("*warn", null);
                }
                return outputParameters;
            }
        }


        public static ParameterBag RptNormality(ITemplateHost host, ParameterBag parameters)
        {
            // ASSUME: Data passed in was acquired with NumericSkipMissing and has no missing values.
            DataFrame frame = parameters["data"].AsDataFrame;

            ParameterBag outputParameters = new ParameterBag();
            List<ParameterBag> outputList = new List<ParameterBag>();
            outputParameters.AddOutput("*variable", outputList);

            foreach (Variable v in frame.Variables)
            {
                DoubleVariable v0 = v as DoubleVariable;
                double[] data = v0.Data;
                int n = data.Length;

                // variable
                ParameterBag variableParameters = new ParameterBag();
                outputList.Add(variableParameters);
                variableParameters.AddOutput("sample", v0.Title);
                variableParameters.AddOutput("n", n.ToString());

                // D'Agostino omnibus skewness and kurtosis test
                double mean, sd, skewness, kurtosis, b1, b1P, b2, b2P, k2, k2P;
                normality_sk(data, 0, n, out mean, out sd, out skewness, out kurtosis, out b1, out b1P, out b2, out b2P, out k2, out k2P);
                variableParameters.AddOutput("mean", host.RoundU(mean));
                variableParameters.AddOutput("sd", host.RoundU(sd));
                string xtra = n < 8 ? string.Empty : ",";
                variableParameters.AddOutput("skewness", host.RoundU(skewness) + xtra);
                variableParameters.AddOutput("kurtosis", host.RoundU(kurtosis) + xtra);
                if (n < 8)
                {
                    variableParameters.AddOutput("b1_p", string.Empty);
                    variableParameters.AddOutput("b2_p", string.Empty);
                    variableParameters.AddOutput("k2", "Not calculated if sample size < 8");
                    variableParameters.AddOutput("k2_p", string.Empty);
                }
                else
                {
                    variableParameters.AddOutput("b1_p", host.pval(b1P));
                    variableParameters.AddOutput("b2_p", host.pval(b2P));
                    variableParameters.AddOutput("k2", host.RoundU(k2) + ",");
                    variableParameters.AddOutput("k2_p", host.pval(k2P));
                }

                // Shapiro-Wilk
                double sw_w, sw_p, sw_z = 0, sw_v = 0;
                normality_sw(data, 0, n, out sw_w, out sw_p, ref sw_z, ref sw_v);
                if (n < 3)
                {
                    variableParameters.AddOutput("sw_w", "Not calculated if sample size < 3");
                    variableParameters.AddOutput("sw_v", string.Empty);
                    variableParameters.AddOutput("sw_p", string.Empty);
                }
                else
                {
                    variableParameters.AddOutput("sw_w", host.RoundU(sw_w) + ",");
                    variableParameters.AddOutput("sw_v", "V = " + host.RoundU(sw_v) + ",");
                    xtra = n > 2000 ? ": Test unreliable with more than 2000 observations." : string.Empty;
                    variableParameters.AddOutput("sw_p", host.pval(sw_p) + xtra);
                }

                // Shapiro-Francia
                double sf_w, sf_p, sf_v, sf_z;
                normality_sf(data, 0, n, out sf_w, out sf_v, out sf_z, out sf_p);
                if (n < 5)
                {
                    variableParameters.AddOutput("sf_w", "Not calculated if sample size < 5");
                    variableParameters.AddOutput("sf_v", string.Empty);
                    variableParameters.AddOutput("sf_p", string.Empty);
                }
                else
                {
                    variableParameters.AddOutput("sf_w", host.RoundU(sf_w) + ",");
                    variableParameters.AddOutput("sf_v", "V' = " + host.RoundU(sf_v) + ",");
                    xtra = n > 5000 ? ": Test unreliable with more than 5000 observations." : string.Empty;
                    variableParameters.AddOutput("sf_p", host.pval(sf_p) + xtra);
                }

                double pmin = Constant.MISSING;
                if (sw_p != Constant.MISSING)
                {
                    pmin = sw_p;
                }
                if (sf_p != Constant.MISSING && sf_p < pmin)
                {
                    pmin = sf_p;
                }
                if (pmin != Constant.MISSING)
                {
                    if (pmin < 0.05)
                    {
                        variableParameters.AddOutput("result", "Sample unlikely to be from a normal distribution");
                    }
                    else if (pmin < 0.1)
                    {
                        variableParameters.AddOutput("result", "Tests not quite significant but do not assume normality");
                    }
                    else
                    {
                        variableParameters.AddOutput("result", "No non-normality detected by tests: examine plot");
                    }
                }
                else
                {
                    variableParameters.AddOutput("result", "Error in calculation");
                }

                NormalOptions nOptions = new NormalOptions(host.Preferences.ShouldUseColour) {ShouldScaleZ = true, Method = NormalOptions.ScoreMethod.Blom};
                ChartDefinition cd = new ChartDefinition {ChartOptions = nOptions};
                cd.XSeries.Add(new DoubleSeries(data, v0.Title));

                using (ChartRenderer ch = new ChartRenderer(cd))
                {
                    string rtf = ch.PlotNormalAndReturnRtf(host, data);
                    variableParameters.AddOutput("chart", rtf);
                }
            }
            return outputParameters;
        }


        ///  <summary>
        ///  Royston's adjusted D'Agnostio ombibus test of skewness and kurtosis
        ///  </summary>
        ///  <param name="x">Vector of observations with zero lower bound</param>
        /// <param name="sd"></param>
        /// <param name="skewness">Fisher G1 coefficient of skewness</param>
        ///  <param name="lowerBound">Lower bound of observation vector</param>
        ///  <param name="n">Number of observations</param>
        ///  <param name="kurtosis">Fisher G2 coefficient of kurtosis</param>
        ///  <param name="sqrtb1">sqrt(B1)</param>
        ///  <param name="p_b1">Significance of sqrt(B1)</param>
        ///  <param name="b2">B2</param>
        ///  <param name="p_b2">Significance of B2</param>
        ///  <param name="k2">Royston adjusted omnibus test statistic K2</param>
        ///  <param name="p_k2">Significance of K2</param>
        /// <param name="mean"></param>
        /// <remarks>
        ///  D'Agostino RB, Belanger A, D'Agostino RB Jr. A suggestion for using powerful and informative tests of normality. American Statistician 1990;44(4):316-321.
        ///  Royston JP. Comment on sg3.4 and an improved D'Agostino test. sg3.5. Stata Technical Bulletin 1991;3:23-24.
        ///  </remarks>
        public static void normality_sk(double[] x, int lowerBound, int n, out double mean, out double sd, out double skewness, out double kurtosis, out double sqrtb1, out double p_b1, out double b2, out double p_b2, out double k2, out double p_k2)
        {
            // set on error exit values first
            sd = Constant.MISSING;
            skewness = Constant.MISSING;
            kurtosis = Constant.MISSING;
            sqrtb1 = Constant.MISSING;
            b2 = Constant.MISSING;
            p_b1 = Constant.MISSING;
            p_b2 = Constant.MISSING;
            k2 = Constant.MISSING;
            p_k2 = Constant.MISSING;

            // basic sums
            double nx = 0.0;
            double sum = 0.0;
            int i;
            for (i = lowerBound; i < n + lowerBound; i++)
            {
                if (x[i] != Constant.MISSING)
                {
                    nx += 1.0;
                    sum += x[i];
                }
            }
            mean = sum / nx;

            // moments of deviation from the mean - agrees with R moments package whereas Stata seems to have a rounding error at 7 or so significant digits
            double m1 = 0.0;
            double m2 = 0.0;
            double m3 = 0.0;
            double m4 = 0.0;
            // bool toobig = false; 
            for (i = lowerBound; i < n + lowerBound; i++)
            {
                double s = x[i] - mean;
                m2 = m2 + Math.Pow(s, 2.0);
                m3 = m3 + Math.Pow(s, 3.0);
                m4 = m4 + Math.Pow(s, 4.0);
                if (m4 > 1.0E+300)
                    return;
            }
            double var = (m2 - m1 * 2.0 / nx) / (nx - 1.0);
            sd = Math.Sqrt(var);
            if (var == 0.0)
                return;
            m2 = m2 / nx;
            m3 = m3 / nx;
            m4 = m4 / nx;
            skewness = m3 * Math.Pow(m2, (-1.5));
            kurtosis = m4 * Math.Pow(m2, (-2.0));
            if (n < 8)
                return;

            // tests of skewness, kurtosis and omnibus k2
            sqrtb1 = (nx - 2.0) / Math.Sqrt(nx * (nx - 1.0)) * skewness;
            double y = skewness * Math.Sqrt(((nx + 1.0) * (nx + 3.0)) / (6.0 * (nx - 2.0)));
            double beta2 = (3.0 * (nx * nx + 27.0 * nx - 70.0) * (nx + 1.0) * (nx + 3.0)) / ((nx - 2.0) * (nx + 5.0) * (nx + 7.0) * (nx + 9.0));
            double w2 = -1 + Math.Sqrt(2.0 * (beta2 - 1.0));
            double delta = 1.0 / Math.Sqrt(Math.Log(Math.Sqrt(w2)));
            double alpha = Math.Sqrt(2.0 / (w2 - 1.0));
            double z_b1 = Math.Abs(delta * Math.Log(y / alpha + Math.Sqrt(Math.Pow((y / alpha), 2.0) + 1.0)));
            p_b1 = 2.0 - 2.0 * PDF.alnorm(z_b1);

            b2 = 3.0 * (nx - 1.0) / (nx + 1.0) + (nx - 2.0) * (nx - 3.0) / ((nx + 1.0) * (nx - 1.0)) * kurtosis;
            double meanb2 = 3.0 * (nx - 1.0) / (nx + 1.0);
            double varb2 = (24.0 * nx * (nx - 2.0) * (nx - 3.0)) / (Math.Pow((nx + 1.0), 2.0) * (nx + 3.0) * (nx + 5.0));
            double xx = (kurtosis - meanb2) / Math.Sqrt(varb2);
            double moment = ((6.0 * (nx * nx - 5.0 * nx + 2.0)) / ((nx + 7.0) * (nx + 9.0))) * Math.Sqrt((6.0 * (nx + 3.0) * (nx + 5.0)) / (nx * (nx - 2.0) * (nx - 3.0)));
            double a = 6.0 + (8.0 / moment) * (2.0 / moment + Math.Sqrt(1.0 + 4.0 / (Math.Pow(moment, 2.0))));
            double z_b2 = Math.Abs(((1.0 - 2.0 / (9.0 * a)) - Math.Pow(((1.0 - 2.0 / a) / (1.0 + xx * Math.Sqrt(2.0 / (a - 4.0)))), (1.0 / 3.0))) / Math.Sqrt(2.0 / (9.0 * a)));
            p_b2 = 2.0 - 2.0 * PDF.alnorm(z_b2);

            k2 = z_b1 * z_b1 + z_b2 * z_b2;
            p_k2 = PDF.chivalp(k2, 2.0);
            // Royston adjustment
            int ifault;
            double zc2 = -PDF.gauinv(Math.Exp(-0.5 * k2), out ifault);
            if (ifault == 0)
            {
                double logn = Math.Log(nx);
                double cut = 0.55 * (Math.Pow(nx, 0.2)) - 0.21;
                double a1 = (-5.0 + 3.46 * logn) * Math.Exp(-1.37 * logn);
                double b1 = 1.0 + (0.854 - 0.148 * logn) * Math.Exp(-0.55 * logn);
                double b2mb1 = 2.13 / (1.0 - 2.37 * logn);
                double a2 = a1 - b2mb1 * cut;
                double b2x = b2mb1 + b1;
                double z;
                if (zc2 < -1.0)
                {
                    z = zc2;
                }
                else if (zc2 < cut)
                {
                    z = a1 + b1 * zc2;
                }
                else
                {
                    z = a2 + b2x * zc2;
                }
                double p = 1.0 - PDF.alnorm(z);
                if (p != 0.0)
                {
                    k2 = -2.0 * Math.Log(p);
                    p_k2 = p;
                }
            }

        }


        ///  <summary> Shapiro-Francia test for normality</summary>
        ///  <param name="x">Vector of observations</param>
        ///  <param name="lowerBound">Lower bound of observation vector</param>
        ///  <param name="n">Number of observations</param>
        ///  <param name="w">W test statistic</param>
        ///  <param name="p">Significance of W</param>
        ///  <param name="z">Normalised test statistic for W</param>
        ///  <param name="v">(1-W)/(median of 1-W)</param>
        ///  <remarks></remarks>
        private static void normality_sw(double[] x, int lowerBound, int n, out double w, out double p, ref double z, ref double v)
        {
            // set on error exit values first
            w = Constant.MISSING;
            p = Constant.MISSING;

            // clean observations
            double[] q = new double[n + 1];
            int i;
            int k = 0;
            for (i = lowerBound; i < n + lowerBound; i++)
            {
                if (x[i] != Constant.MISSING)
                {
                    k += 1;
                    q[k] = x[i];
                }
            }
            if (n < 3.0)
                return;

            // ranks
            double[] r = new double[n + 1];
            double xf;
            Array.Sort(q, 1, k);
            ExFortran.Rank(q, r, 1, k, 1, out xf);

            // normalised coefficients
            double nx = Convert.ToDouble(k);
            if (k == 3)
            {
                for (i = 1; i <= k; i++)
                    r[i] = Math.Sqrt(0.5) * Convert.ToDouble(i - 2);
            }
            else
            {
                for (i = 1; i <= k; i++)
                {
                    int ifault;
                    r[i] = PDF.gauinv((r[i] - 0.375) / (nx + 0.25), out ifault);
                    if (ifault != 0)
                        return;
                }
                double mean = 0.0;
                for (i = 1; i <= k; i++)
                    mean += r[i];
                mean = mean / nx;
                double m1 = 0.0;
                double m2 = 0.0;
                // bool toobig = false; 
                for (i = 1; i <= k; i++)
                {
                    double sx = r[i] - mean;
                    m2 = m2 + Math.Pow(sx, 2.0);
                    if (m2 > 1.0E+300)
                        return;
                }
                double var = (m2 - m1 * 2.0 / n) / (nx - 1.0);
                if (var == 0.0)
                    return;
                double summ2 = var * (nx - 1.0);
                double xx = 1.0 / Math.Sqrt(nx);
                double a1 = r[k] / Math.Sqrt(summ2) + xx * (0.221157 + xx * (-0.147981 + xx * (-2.07119 + xx * (4.434685 - xx * 2.706056))));
                int i1;
                double fac;
                if (k > 5)
                {
                    i1 = 3;
                    double a2 = r[k - 1] / Math.Sqrt(summ2) + xx * (0.042981 + xx * (-0.293762 + xx * (-1.752461 + xx * (5.682633 - xx * 3.582633))));
                    fac = Math.Sqrt((summ2 - 2.0 * Math.Pow(r[k], 2.0) - 2.0 * Math.Pow(r[k - 1], 2.0)) / (1.0 - 2.0 * Math.Pow((a1), 2.0) - 2.0 * Math.Pow((a2), 2.0)));
                    r[k] = a1;
                    r[k - 1] = a2;
                    r[1] = -a1;
                    r[2] = -a2;
                }
                else
                {
                    i1 = 2;
                    fac = Math.Sqrt((summ2 - 2.0 * Math.Pow(r[k], 2.0)) / (1.0 - 2.0 * Math.Pow((a1), 2.0)));
                    r[k] = a1;
                    r[1] = -a1;
                }
                int i2 = k - i1 + 1;
                for (i = i1; i <= i2; i++)
                    r[i] = r[i] / fac;
            }

            double rho = MathDbl.corr(q, r, 1, k, true);
            w = rho * rho;

            //  Evaluate P, Z and V [(1-W)/(median of 1-W)]
            double y = Math.Log(1.0 - w);
            double xq = Math.Log(nx);
            double m = 0.0;
            double s = 1.0;
            if (k == 3)
            {
                double sw = Math.Sqrt(w);
                double ang = 1.5707288 + sw * (-0.2121144 + sw * (0.074261 - 0.0187293 * sw));
                ang = Constant.PI / 2.0 - ang * Math.Sqrt(1.0 - sw);
                double stqr = Math.Asin(Math.Sqrt(0.75));
                p = (6 / Constant.PI) * (ang - stqr);
                int ifault;
                z = -PDF.gauinv(p, out ifault);
                v = (1.0 - w) / (1 - Math.Pow((Math.Sin(Constant.PI / 12.0 + stqr)), 2.0));
            }
            else
            {
                if (k <= 11)
                {
                    double gamma = nx * 0.459 - 2.273;
                    if (y >= gamma)
                    {
                        y = 9.9999;
                        v = Constant.MISSING;
                    }
                    else
                    {
                        y = -Math.Log(-y + gamma);
                        m = 0.544 + nx * (-0.39978 + nx * (0.025054 - nx * 0.0006714));
                        s = Math.Exp(1.3822 + nx * (-0.77857 + nx * (0.062767 - nx * 0.0020322)));
                        v = (1.0 - w) / Math.Exp(gamma - Math.Exp(-m));
                    }
                }
                else
                {
                    m = -1.5861 + xq * (-0.31082 + xq * (-0.083751 + xq * 0.0038915));
                    s = Math.Exp(-0.4803 + xq * (-0.082676 + xq * 0.0030302));
                    v = (1.0 - w) / Math.Exp(m);
                }
                z = (y - m) / s;
                p = PDF.alnorm(-z);
            }

        }


        ///  <summary>
        ///  Shapiro-Francia test for normality
        ///  </summary>
        ///  <param name="x">Vector of observations with zero lower bound</param>
        /// <param name="n"> </param>
        /// <param name="w">W'</param>
        ///  <param name="v">V'</param>
        ///  <param name="z">Normalised statistic</param>
        ///  <param name="p">Significance</param>
        /// <param name="lowerBound"> </param>
        /// <remarks></remarks>
        private static void normality_sf(double[] x, int lowerBound, int n, out double w, out double v, out double z, out double p)
        {
            // set on error exit values first
            w = Constant.MISSING;
            v = Constant.MISSING;
            z = Constant.MISSING;
            p = Constant.MISSING;

            // clean observations
            double[] q = new double[n + 1 ];
            int i;
            int k = 0;
            for (i = lowerBound; i < n + lowerBound; i++)
            {
                if (x[i] != Constant.MISSING)
                {
                    k += 1;
                    q[k] = x[i];
                }
            }
            if (n < 5.0)
            {
                return;
            }

            // ranks
            double[] r = new double[n + 1 ];
            double xf;
            Array.Sort(q, 1, k);
            ExFortran.Rank(q, r, 1, k, 1, out xf);

            // Shapiro-Francia by Patrick Royston
            double nx = Convert.ToDouble(k);
            for (i = 1; i <= k; i++)
            {
                int ifault;
                r[i] = PDF.gauinv((r[i] - 0.375) / (nx + 0.25), out ifault);
                if (ifault != 0)
                {
                    return;
                }
            }

            double h = Math.Log(nx) - 5.0;
            double l = -0.0480157 + h * (0.01971964 - 0.0119065 * h * h);
            double m = -Math.Exp(1.6930674 + h * (0.1441647 + h * (-0.01849276 + h * (0.031074485 + h * 0.0055717663))));
            double f = Math.Exp(-0.510725 + h * (-0.1160364 + h * (-0.006702098 + h * (0.054465944 + h * 0.0087397329))));
            double rho = MathDbl.corr(q, r, 1, k, true);
            w = rho * rho;
            double y = ((Math.Pow((1.0 - w), l)) - 1.0) / l;
            z = (y - m) / f;
            v = (1.0 - w) / (Math.Pow((l * m + 1.0), (1.0 / l)));
            p = PDF.alnorm(-z);

        }


        public static ParameterBag rptTUnpairedSummary(ITemplateHost host, ParameterBag parameters)
        {
            double GAMMA = parameters["gamma"].AsDouble;
            int nx1 = parameters["nx1"].AsInt32;
            double um1 = parameters["um1"].AsDouble;
            double sd1 = parameters["sd1"].AsDouble;
            int nx2 = parameters["nx2"].AsInt32;
            double um2 = parameters["um2"].AsDouble;
            double sd2 = parameters["sd2"].AsDouble;
            int degf = nx1 + nx2 - 2;
            if (nx1 < 2 | sd1 == 0 | nx2 < 2 | sd2 == 0)
            {
                throw new Exception("Insufficient data (must be at least two members in each sample with non-zero standard deviations)");
            }
            double var1 = sd1 * sd1;
            double var2 = sd2 * sd2;
            //  RTF_LoadTemplate("m_unpair.rtf")
            double P0; double cit;
            MathDbl.civ(degf, out cit, GAMMA, out P0);
            double xm1 = um1;
            double xm2 = um2;
            if (um1 < um2)
            {
                double um = um1;
                um1 = um2;
                um2 = um;
            }
            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("title_0", "* sample 1 from summary");
            outputParameters.AddOutput("mean_0", host.RoundU(xm1));
            outputParameters.AddOutput("n0", nx1.ToString());
            outputParameters.AddOutput("title_1", "* sample 2 from summary");
            outputParameters.AddOutput("mean_1", host.RoundU(xm2));
            outputParameters.AddOutput("n1", nx2.ToString());
            // equal variances
            double cv = (((var1 * Convert.ToDouble(nx1 - 1)) + (var2 * Convert.ToDouble(nx2 - 1))) / Convert.ToDouble(nx1 + nx2 - 2));
            double cn = (1 / Convert.ToDouble(nx1)) + (1 / Convert.ToDouble(nx2));
            double cset = Math.Sqrt(cv) * Math.Sqrt(cn);
            double tstat = (um1 - um2) / cset;
            double power = Power.tstpower(1.0 - GAMMA, Math.Abs(um1 - um2), Math.Sqrt(cv), nx1, nx2);
            outputParameters.AddOutput("error", host.RoundU(cset));
            outputParameters.AddOutput("df", degf.ToString());
            outputParameters.AddOutput("t", host.RoundU(tstat));
            double P = PDF.tvalp(Math.Abs(tstat), Convert.ToDouble(degf));
            if (P > 1.0 - P)
            {
                P = 1.0 - P;
            }
            outputParameters.AddOutput("p_1", host.pval(P));
            outputParameters.AddOutput("p_2", host.pval(P * 2.0));
            outputParameters.AddOutput("pc", Formatting.XRound(100 * (1 - P0), 2));
            outputParameters.AddOutput("from", host.RoundU(xm1 - xm2 - (cit * cset)));
            outputParameters.AddOutput("to", host.RoundU(xm1 - xm2 + (cit * cset)));
            outputParameters.AddOutput("pwr", Formatting.pwr(power, 1.0 - GAMMA));
            // unequal variances
            double xn1 = Convert.ToDouble(nx1);
            double xn2 = Convert.ToDouble(nx2);
            cset = Math.Sqrt(var1 / xn1 + var2 / xn2);
            tstat = (um1 - um2) / cset;
            double xdegf = Math.Pow((var1 / xn1 + var2 / xn2), 2.0) / (Math.Pow((var1 / xn1), 2.0) / (xn1 - 1.0) + Math.Pow((var2 / xn2), 2.0) / (xn2 - 1.0));
            outputParameters.AddOutput("error_unequal", host.RoundU(cset));
            outputParameters.AddOutput("df_unequal", host.RoundU(xdegf));
            outputParameters.AddOutput("t_unequal", host.RoundU(tstat));
            P = PDF.tvalp(Math.Abs(tstat), xdegf);
            if (P > 1.0 - P)
            {
                P = 1.0 - P;
            }
            outputParameters.AddOutput("p_1_unequal", host.pval(P));
            outputParameters.AddOutput("p_2_unequal", host.pval(P * 2.0));
            outputParameters.AddOutput("pc_unequal", Formatting.XRound(100 * (1 - P0), 2));
            outputParameters.AddOutput("from_unequal", host.RoundU(xm1 - xm2 - (cit * cset)));
            outputParameters.AddOutput("to_unequal", host.RoundU(xm1 - xm2 + (cit * cset)));
            power = Power.uvttpower(1.0 - GAMMA, Math.Abs(um1 - um2), xn1, xn2, sd1, sd2);
            outputParameters.AddOutput("pwr_unequal", Formatting.pwr(power, 1.0 - GAMMA));
            double f;
            if (Math.Abs(var1) > Math.Abs(var2))
            {
                f = var1 / var2;
                P = PDF.fvalp(f, Convert.ToDouble(nx1 - 1), Convert.ToDouble(nx2 - 1));
            }
            else
            {
                f = var2 / var1;
                P = PDF.fvalp(f, Convert.ToDouble(nx2 - 1), Convert.ToDouble(nx1 - 1));
            }
            if (P < 0.025)
            {
                outputParameters.AddOutput("say1", "TWO SIDED F TEST IS SIGNIFICANT");
                outputParameters.AddOutput("say2", "USE APPROXIMATE t (UNEQUAL VARIANCES) RESULT or ALTERNATIVELY MANN-WHITNEY");
            }
            else
            {
                outputParameters.AddOutput("say1", "Two sided F test is not significant");
                outputParameters.AddOutput("say2", "No need to assume unequal variances");
            }
            return outputParameters;
        }


        public static ParameterBag RptTSingleSummary(ITemplateHost host, ParameterBag parameters)
        {
            double P0; double cit;

            double GAMMA = parameters["gamma"].AsDouble;
            int nx = parameters["nx"].AsInt32;
            double mu = parameters["mu"].AsDouble;
            double sd = parameters["sd1"].AsDouble;
            double mu0 = parameters["mu0"].AsDouble;

            int degf = nx - 1;
            if (nx < 2 || sd == 0)
            {
                throw new Exception("Insufficient data (must be at least two members in the sample with non-zero standard deviation)");
            }
            //  RTF_LoadTemplate("m_single.rtf")
            double se = sd / Math.Sqrt(nx);
            MathDbl.civ(degf, out cit, GAMMA, out P0);
            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("name", "* from summary data");
            outputParameters.AddOutput("sam_mean", host.RoundU(mu));
            outputParameters.AddOutput("pop_mean", host.RoundU(mu0));
            outputParameters.AddOutput("size", nx.ToString());
            outputParameters.AddOutput("sd", host.RoundU(sd));
            outputParameters.AddOutput("pc", Formatting.XRound(100 * (1 - P0), 2));
            outputParameters.AddOutput("for", mu0 == 0 ? "for the mean" : "for mean difference");
            outputParameters.AddOutput("from", host.RoundU((mu - mu0) - (cit * se)));
            outputParameters.AddOutput("to", host.RoundU((mu - mu0) + (cit * se)));
            double t = (mu - mu0) / se;
            degf = nx - 1;
            double P = PDF.tvalp(Math.Abs(t), Convert.ToDouble(degf));
            if (P > 1.0 - P)
            {
                P = 1.0 - P;
            }
            double power = Power.ptpower(1.0 - GAMMA, mu - mu0, sd, Convert.ToDouble(nx));
            outputParameters.AddOutput("df", degf.ToString());
            outputParameters.AddOutput("t", host.RoundU(t));
            outputParameters.AddOutput("p_1", host.pval(P));
            outputParameters.AddOutput("p_2", host.pval(P * 2.0));
            outputParameters.AddOutput("pwr", Formatting.pwr(power, 1.0 - GAMMA));
            return outputParameters;
        }


        public static ParameterBag RptTUnpaired(ITemplateHost host, ParameterBag parameters)
        {
            double P0;
            double cit;
            int bot; int top;

            double[] mean = new double[1 + 1 /* VB to C# conversion */ ];
            double[] ss = new double[1 + 1 /* VB to C# conversion */ ];
            double[] var = new double[1 + 1 /* VB to C# conversion */ ];
            double[] sd = new double[1 + 1 /* VB to C# conversion */ ];
            double[] sem = new double[1 + 1 /* VB to C# conversion */ ];
            int[] tnx = new int[1 + 1 /* VB to C# conversion */];
            double GAMMA = parameters["gamma"].AsDouble;
            DataFrame data = parameters["data"].AsDataFrame;
            //  RTF_LoadTemplate("m_unpair.rtf")
            para(data, mean, ss, var, sd, sem, tnx);
            int degf = tnx[0] + tnx[1] - 2;
            MathDbl.civ(degf, out cit, GAMMA, out P0);
            double um1 = mean[0];
            double um2 = mean[1];
            if (um1 < um2)
            {
                double um = um1;
                um1 = um2;
                um2 = um;
            }
            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("title_0", data.Variables[0].Title);
            outputParameters.AddOutput("mean_0", host.RoundU(mean[0]));
            outputParameters.AddOutput("n0", tnx[0].ToString());
            outputParameters.AddOutput("title_1", data.Variables[1].Title);
            outputParameters.AddOutput("mean_1", host.RoundU(mean[1]));
            outputParameters.AddOutput("n1", tnx[1].ToString());
            // equal variances
            double cv = ((ss[0] + ss[1]) / Convert.ToDouble(tnx[0] + tnx[1] - 2));
            double cn = (1.0 / Convert.ToDouble(tnx[0])) + (1.0 / Convert.ToDouble(tnx[1]));
            double cset = Math.Sqrt(cv * cn);
            double tstat = (um1 - um2) / cset;
            double power = Power.tstpower(1.0 - GAMMA, Math.Abs(um1 - um2), Math.Sqrt(cv), Convert.ToDouble(tnx[0]), Convert.ToDouble(tnx[1]));
            outputParameters.AddOutput("error", host.RoundU(cset));
            outputParameters.AddOutput("df", degf.ToString());
            outputParameters.AddOutput("t", host.RoundU(tstat));
            double P = PDF.tvalp(Math.Abs(tstat), degf);
            if (P > 1.0 - P)
                P = 1.0 - P;
            outputParameters.AddOutput("p_1", host.pval(P));
            outputParameters.AddOutput("p_2", host.pval(P * 2.0));
            outputParameters.AddOutput("pc", Formatting.XRound(100 * (1 - P0), 2));
            outputParameters.AddOutput("from", host.RoundU(mean[0] - mean[1] - (cit * cset)));
            outputParameters.AddOutput("to", host.RoundU(mean[0] - mean[1] + (cit * cset)));
            outputParameters.AddOutput("pwr", Formatting.pwr(power, 1.0 - GAMMA));
            // unequal variances
            double xs1 = ss[0] / Convert.ToDouble(tnx[0] - 1);
            double xs2 = ss[1] / Convert.ToDouble(tnx[1] - 1);
            double xn1 = Convert.ToDouble(tnx[0]);
            double xn2 = Convert.ToDouble(tnx[1]);
            cset = Math.Sqrt(xs1 / xn1 + xs2 / xn2);
            tstat = (um1 - um2) / cset;
            double xdegf = Math.Pow((xs1 / xn1 + xs2 / xn2), 2.0) / (Math.Pow((xs1 / xn1), 2.0) / (xn1 - 1.0) + Math.Pow((xs2 / xn2), 2.0) / (xn2 - 1.0));
            outputParameters.AddOutput("error_unequal", host.RoundU(cset));
            outputParameters.AddOutput("df_unequal", host.RoundU(xdegf));
            outputParameters.AddOutput("t_unequal", host.RoundU(tstat));
            P = PDF.tvalp(Math.Abs(tstat), xdegf);
            if (P > 1.0 - P)
            {
                P = 1.0 - P;
            }
            outputParameters.AddOutput("p_1_unequal", host.pval(P));
            outputParameters.AddOutput("p_2_unequal", host.pval(P * 2.0));
            outputParameters.AddOutput("pc_unequal", Formatting.XRound(100 * (1 - P0), 2));
            outputParameters.AddOutput("from_unequal", host.RoundU(mean[0] - mean[1] - (cit * cset)));
            outputParameters.AddOutput("to_unequal", host.RoundU(mean[0] - mean[1] + (cit * cset)));
            power = Power.uvttpower(1.0 - GAMMA, Math.Abs(um1 - um2), Convert.ToDouble(tnx[0]), Convert.ToDouble(tnx[1]), sd[0], sd[1]);
            outputParameters.AddOutput("pwr_unequal", Formatting.pwr(power, 1.0 - GAMMA));
            if (Math.Abs(var[0]) > Math.Abs(var[1]))
            {
                top = 0;
                bot = 1;
            }
            else
            {
                top = 1;
                bot = 0;
            }
            double f = var[top] / var[bot];
            P = PDF.fvalp(f, Convert.ToDouble(tnx[top] - 1), Convert.ToDouble(tnx[bot] - 1));
            if (P < 0.025)
            {
                outputParameters.AddOutput("say1", "TWO SIDED F TEST IS SIGNIFICANT");
                outputParameters.AddOutput("say2", "USE APPROXIMATE t (UNEQUAL VARIANCES) RESULT or ALTERNATIVELY MANN-WHITNEY");
            }
            else
            {
                outputParameters.AddOutput("say1", "Two sided F test is not significant");
                outputParameters.AddOutput("say2", "No need to assume unequal variances");
            }
            return outputParameters;
        }


        public static ParameterBag RptTSingle(ITemplateHost host, ParameterBag parameters)
        {
            double[] mean = new double[1];
            double[] ss = new double[1];
            double[] var = new double[1];
            double[] sd = new double[1];
            double[] sem = new double[1];
            int[] tnx = new int[1];

            DataFrame Data = parameters["data"].AsDataFrame;
            double mu0 = parameters["population-mean"].AsDouble;
            double GAMMA = parameters["gamma"].AsDouble;

            //  RTF_LoadTemplate("m_single.rtf")
            para(Data, mean, ss, var, sd, sem, tnx);
            int degf = tnx[0] - 1;
            double P0; double cit;
            MathDbl.civ(degf, out cit, GAMMA, out P0);
            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("name", Data.Variables[0].Title);
            outputParameters.AddOutput("sam_mean", host.RoundU(mean[0]));
            outputParameters.AddOutput("pop_mean", host.RoundU(mu0));
            outputParameters.AddOutput("size", tnx[0].ToString());
            outputParameters.AddOutput("sd", host.RoundU(sd[0]));
            outputParameters.AddOutput("pc", Formatting.XRound(100 * (1 - P0), 2));
            outputParameters.AddOutput("for", mu0 == 0 ? "for the mean" : "for mean difference");
            outputParameters.AddOutput("from", host.RoundU((mean[0] - mu0) - (cit * sem[0])));
            outputParameters.AddOutput("to", host.RoundU((mean[0] - mu0) + (cit * sem[0])));
            double t = (mean[0] - mu0) / sem[0];
            degf = tnx[0] - 1;
            double P = PDF.tvalp(Math.Abs(t), Convert.ToDouble(degf));
            if (P > 1.0 - P)
            {
                P = 1.0 - P;
            }
            double power = Power.ptpower(1.0 - GAMMA, mean[0] - mu0, sd[0], Convert.ToDouble(tnx[0]));
            outputParameters.AddOutput("df", degf.ToString());
            outputParameters.AddOutput("t", host.RoundU(t));
            outputParameters.AddOutput("p_1", host.pval(P));
            outputParameters.AddOutput("p_2", host.pval(P * 2.0));
            outputParameters.AddOutput("pwr", Formatting.pwr(power, 1.0 - GAMMA));
            return outputParameters;
        }


        public static ParameterBag RptTPaired(ITemplateHost host, ParameterBag parameters)
        {
            double P0; double cit;
            double var;
            double mean; double sum;
            int N;

            DataFrame Data = parameters["data"].AsDataFrame;
            double GAMMA = parameters["gamma"].AsDouble;
            bool DoAgree = Data.VariableCount > 1 && parameters.ContainsKey("doAgreement") && parameters["doAgreement"].AsBoolean;
            //  RTF_LoadTemplate("m_paired.rtf")

            double[] arr1 = new double[Data.MaxRows + 1 ]; // New array to replace Arr2(0,n)
            DoubleVariable v0 = Data.Variables[0]as DoubleVariable;
            int nx = 0;
            string txc;
            if (Data.VariableCount == 1)
            {
                for (N = 0; N <= v0.Length - 1; N++)
                {
                    if (v0.Data[N] != Constant.MISSING)
                    {
                        nx++;
                        arr1[nx] = v0.Data[N];
                    }
                }
                txc = "differences listed in " + v0.Title;
            }
            else
            {
                DoubleVariable v1 = Data.Variables[1]as DoubleVariable;
                for (N = 0; N <= v0.Length - 1; N++)
                {
                    if (v0.Data[N] != Constant.MISSING & v1.Data[N] != Constant.MISSING)
                    {
                        nx++;
                        arr1[nx] = v0.Data[N] - v1.Data[N];
                    }
                }
                txc = "differences between " + v0.Title + " and " + v1.Title;
            }

            univariate(arr1, nx, out sum, out mean, out var);
            double sd = Math.Sqrt(var);
            double sem = sd / Math.Sqrt(Convert.ToDouble(nx));
            int degf = nx - 1;
            MathDbl.civ(degf, out cit, GAMMA, out P0);
            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("label", txc);
            outputParameters.AddOutput("mean", host.RoundU(mean));
            outputParameters.AddOutput("n", nx.ToString());
            outputParameters.AddOutput("sd", host.RoundU(sd));
            outputParameters.AddOutput("sem", host.RoundU(sem));
            int ifault;
            double z = PDF.gauinv(1.0 - (P0 / 2.0), out ifault);
            double lla = mean - (z * sd);
            double ula = mean + (z * sd);
            outputParameters.AddOutput("pc", Formatting.XRound(100 * (1.0 - P0), 2));
            outputParameters.AddOutput("from", host.RoundU(mean - (cit * sem)));
            outputParameters.AddOutput("to", host.RoundU(mean + (cit * sem)));
            double t = sem != 0.0 ? mean / sem : Constant.MISSING;
            double power = Power.ptpower(1.0 - GAMMA, mean, sd, Convert.ToDouble(nx));
            outputParameters.AddOutput("df", (nx - 1).ToString());
            outputParameters.AddOutput("t", host.RoundU(t));
            double tstat = sem != 0.0 ? mean / sem : Constant.MISSING;
            double P = PDF.tvalp(Math.Abs(tstat), Convert.ToDouble(degf));
            if (P > 1.0 - P)
            {
                P = 1.0 - P;
            }
            outputParameters.AddOutput("tail_1", host.pval(P));
            outputParameters.AddOutput("tail_2", host.pval(P * 2.0));
            outputParameters.AddOutput("pwr", Formatting.pwr(power, 1.0 - GAMMA));
            if (DoAgree)
            {
                IList<ParameterBag> twosampleList = new List<ParameterBag>();
                outputParameters.AddOutput("*twosample", twosampleList);
                ParameterBag twosampleParameters = new ParameterBag();
                twosampleList.Add(twosampleParameters);
                twosampleParameters.AddOutput("from2", host.RoundU(lla));
                twosampleParameters.AddOutput("to2", host.RoundU(ula));
                IList<ParameterBag> chartList = new List<ParameterBag>();
                outputParameters.AddOutput("*chart", chartList);

                DoubleVariable v1 = Data.Variables[1]as DoubleVariable;
                double[] x = new double[v0.Length + 1 ];
                double[] y = new double[v1.Length + 1 ];
                x[0] = Constant.MISSING;
                y[0] = Constant.MISSING;
                nx = 0;
                int j;
                for (j = 0; j <= v0.Length - 1; j++)
                {
                    if (v0.Data[j] != Constant.MISSING & v1.Data[j] != Constant.MISSING)
                    {
                        nx = nx + 1;
                        y[nx] = v0.Data[j] - v1.Data[j];
                        x[nx] = (v0.Data[j] + v1.Data[j]) / 2;
                    }
                }

                using (ChartRenderer ch = new ChartRenderer(ChartDefinition.Empty()))
                {
                    string rtf = ch.PlotTiesAndReturnMetafile(host, x, y, nx, lla, ula, GAMMA, v0.Title, v1.Title, mean);
                    ParameterBag chartParameters = new ParameterBag();
                    chartList.Add(chartParameters);
                    chartParameters.AddOutput("chart", rtf);
                }
            }
            else
            {
                outputParameters.AddOutput("*twosample", null);
                outputParameters.AddOutput("*chart", null);
            }
            return outputParameters;
        }

    }

}
