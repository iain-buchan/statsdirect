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
    public static class Regress
    {
        ///  <summary>
        ///  The equivalent of the PASS_* variables in SD2, so PASS_X is X in this class
        ///  </summary>
        ///  <remarks></remarks>
        [Serializable]
        private class MultipleLinearRegressionContext
        {
            public double[] ARG;
            public double[] B;
            public double[] Covariance { get; set; }
            public double DEV;
            public double DEVX;
            public int DF;
            public int DFX;
            public bool DoC;
            public double[] DV;
            public int M;
            public double[] FV;
            public double[] H1;
            public double[,] H;
            public string[] Labels { get; set; }
            public double LLX;
            public int N;
            public string outcomeTitle;
            public int P;
            public double[] R;
            public double[,] R2;
            public int RANK;
            public double[] RV;
            public int[] RXI;

            ///  <summary>
            ///  Weights
            ///  </summary>
            public double[] S;

            public double[] SE;
            public double SSREG;
            public double SSY;
            public double[] SV;
            /// <summary>
            /// Trials
            /// </summary>
            public double[] T;

            /// <remarks>1-based when used as predictor titles, 0-based when used for polynomial regression.  Sorry!</remarks>
            public string[] Titles;

            public double TOL;
            //public double[] V1;
            public double[,] V;
            public double[] VIF;
            public string warn;
            public bool WEIGHT;
            public string weightTitle;
            public double[] WT;
            public double[,] X;
            public double[] X1;
            /// <summary>
            /// Events
            /// </summary>
            public double[] Y;
        }

        private static SimpleLinearRegressionContext GetSimpleLinearRegressionContext(ParameterBag parameters)
        {
            // If there's already a cached context, assume it is from a previous operation with the same values and use it.
            if (parameters.ContainsKey("context"))
                return ((SimpleLinearRegressionContext)(parameters["context"].Data));

            // Create a new context holding these X and Y variables
            DataFrame fy = parameters["y"].AsDataFrame;
            DoubleVariable vy = fy.Variables[0]as DoubleVariable;
            DataFrame fx = parameters["x"].AsDataFrame;
            DoubleVariable vx = fx.Variables[0]as DoubleVariable;
            double[][] copiesRemovingMissingRows = Numerics.Utilities.RemoveMissingRows(new double[][] { vy.Data, vx.Data }, 0, vy.Length, 0);
            return new SimpleLinearRegressionContext(copiesRemovingMissingRows[1], copiesRemovingMissingRows[0]);
        }

        private static MultipleLinearRegressionContext GetMultipleLinearRegressionContext(ParameterBag parameters)
        {
            if (parameters.ContainsKey("context"))
                return ((MultipleLinearRegressionContext)(parameters["context"].Data));
            throw new Exception("Expected to find a context parameter and didn't");
        }


        public static ParameterBag RptInterpolateXY(ITemplateHost host, ParameterBag parameters)
        {
            SimpleLinearRegressionContext context = GetSimpleLinearRegressionContext(parameters);
            DataFrame fy = parameters["y"].AsDataFrame;
            DoubleVariable vy = fy.Variables[0]as DoubleVariable;
            DataFrame fx = parameters["x"].AsDataFrame;
            DoubleVariable vx = fx.Variables[0]as DoubleVariable;
            double newx = parameters["newx"].AsDouble;
            double newy = newx * context.Slope + context.YIntercept;
            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("v1", vx.Title + " = " + newx.ToString());
            outputParameters.AddOutput("v2", vy.Title + " = " + host.RoundU(newy));
            return outputParameters;
        }


        public static ParameterBag RptInterpolateYX(ITemplateHost host, ParameterBag parameters)
        {
            SimpleLinearRegressionContext context = GetSimpleLinearRegressionContext(parameters);
            DataFrame fy = parameters["y"].AsDataFrame;
            DoubleVariable vy = fy.Variables[0]as DoubleVariable;
            DataFrame fx = parameters["x"].AsDataFrame;
            DoubleVariable vx = fx.Variables[0]as DoubleVariable;
            double newy = parameters["newy"].AsDouble;
            double newx = (newy - context.YIntercept) / context.Slope;
            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("v1", vy.Title + " = " + newy.ToString());
            outputParameters.AddOutput("v2", vx.Title + " = " + host.RoundU(newx));
            return outputParameters;
        }


        public static ParameterBag RptRanv(ITemplateHost host, ParameterBag parameters)
        {
            SimpleLinearRegressionContext context = GetSimpleLinearRegressionContext(parameters);
            DataFrame fy = parameters["y"].AsDataFrame;
            DoubleVariable vy = fy.Variables[0]as DoubleVariable;
            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("reg_sum", host.RoundU(context.SSREG));
            outputParameters.AddOutput("reg_df", "1");
            outputParameters.AddOutput("reg_mean", host.RoundU(context.SSREG));
            outputParameters.AddOutput("res_sum", host.RoundU(context.SSY - context.SSREG));
            double res_df = vy.Length - 2;
            outputParameters.AddOutput("res_df", res_df.ToString());
            outputParameters.AddOutput("res_mean", host.RoundU((context.SSY - context.SSREG) / res_df));
            outputParameters.AddOutput("tot_sum", host.RoundU(context.SSY));
            double tot_df = vy.Length - 1;
            outputParameters.AddOutput("tot_df", tot_df.ToString());
            double vr = context.SSREG / ((context.SSY - context.SSREG) / res_df);
            outputParameters.AddOutput("f", host.RoundU(vr));
            double prob = PDF.fvalp(vr, 1.0, res_df);
            outputParameters.AddOutput("p", host.pval(prob));
            outputParameters.AddOutput("r", host.RoundU(context.SSREG / context.SSY));
            outputParameters.AddOutput("mse", host.RoundU(Math.Sqrt((context.SSY - context.SSREG) / res_df)));
            return outputParameters;
        }


        public static ParameterBag RptSimpleLinearRegression(ITemplateHost host, ParameterBag parameters)
        {
            DataFrame fy = parameters["y"].AsDataFrame;
            DoubleVariable vy = fy.Variables[0]as DoubleVariable;
            DataFrame fx = parameters["x"].AsDataFrame;
            DoubleVariable vx = fx.Variables[0]as DoubleVariable;

            SimpleLinearRegressionContext context = GetSimpleLinearRegressionContext(parameters);

            double REGGAMMA = parameters["gamma"].AsDouble;
            if (REGGAMMA <= 0.0)
                throw new Exception("GAMMA must be greater than zero");

            context.CalculateLeastSquaresMethod();
            context.CalcRcia(REGGAMMA);
            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("eq_y", vy.Title + " = " + host.RoundU(context.Slope));
            string lnk = context.YIntercept < 0.0 ? " " : " + ";
            outputParameters.AddOutput("eq_x", vx.Title + lnk + host.RoundU(context.YIntercept));
            if (!context.IsPerfectCorrelation)
            {
                IList<ParameterBag> seList = new List<ParameterBag>();
                outputParameters.AddOutput("*se", seList);
                ParameterBag seParameters = new ParameterBag();
                seList.Add(seParameters);
                double SEB = context.SeEst / (context.SDX * Math.Sqrt(context.NX - 1));
                seParameters.AddOutput("slope_err", host.RoundU(SEB));
                seParameters.AddOutput("se_pc", Formatting.XRound(100 * (1.0 - context.P0), 2));
                seParameters.AddOutput("se_from", host.RoundU(context.Slope - (context.CIT * SEB)));
                seParameters.AddOutput("se_to", host.RoundU(context.Slope + (context.CIT * SEB)));
                seParameters.AddOutput("se_r", host.RoundU(context.R));
                seParameters.AddOutput("se_r2", host.RoundU(context.R * context.R));
                if (context.NX > 3)
                {
                    IList<ParameterBag> ciList = new List<ParameterBag>();
                    seParameters.AddOutput("*ci", ciList);
                    ParameterBag ciParameters = new ParameterBag();
                    ciList.Add(ciParameters);
                    double GAMMA = 1.0 - (context.P0 / 2.0);
                    int fault;
                    double rcit = PDF.gauinv(GAMMA, out fault);
                    double fz = 0.5 * Math.Log((1.0 + context.R) / (1.0 - context.R));
                    double fz1 = fz - (rcit / Math.Sqrt(Convert.ToDouble(context.NX - 3)));
                    double fz2 = fz + (rcit / Math.Sqrt(Convert.ToDouble(context.NX - 3)));
                    double con1 = (Math.Exp(2.0 * fz1) - 1.0) / (Math.Exp(2.0 * fz1) + 1.0);
                    double con2 = (Math.Exp(2.0 * fz2) - 1.0) / (Math.Exp(2.0 * fz2) + 1.0);
                    ciParameters.AddOutput("ci_pc", Formatting.XRound(100 * (1.0 - context.P0), 1));
                    ciParameters.AddOutput("ci_from", host.RoundU(con1));
                    ciParameters.AddOutput("ci_to", host.RoundU(con2));
                    // double af = 1; 
                    int df = context.NX - 2;
                    double r = context.R;
                    double st = r * Math.Sqrt(Math.Abs(Convert.ToDouble(df) / (1.0 - (r * r))));
                    ciParameters.AddOutput("df", df.ToString());
                    ciParameters.AddOutput("tdf", host.RoundU(st));
                    double P = PDF.tvalp(Math.Abs(st), Convert.ToDouble(df));
                    if (P > 1.0 - P)
                        P = 1.0 - P;
                    P = 2.0 * P;
                    ciParameters.AddOutput("p", host.pval(P));
                    ciParameters.AddOutput("pwr", Formatting.pwr(Power.rpower(0.0, r, Convert.ToDouble(context.NX), context.P0), context.P0));
                    string x = "Correlation coefficient is ";
                    if (P > 0.05)
                        x += "not ";
                    ciParameters.AddOutput("sig", x + "significantly different from zero");
                }
                else
                {
                    seParameters.AddOutput("*ci", null);
                }
                outputParameters.AddOutput("*notcalc", null);
            }
            else
            {
                // Perfect correlation
                outputParameters.AddOutput("*se", null);
                IList<ParameterBag> notcalcList = new List<ParameterBag>();
                outputParameters.AddOutput("*notcalc", notcalcList);
                ParameterBag notcalcParameters = new ParameterBag();
                notcalcList.Add(notcalcParameters);
                notcalcParameters.AddOutput("r", host.RoundU(context.R));
            }

            outputParameters.Add("context", new FilledParameter(FilledParameterDirection.Input, context));
            return outputParameters;
        }


        public static ParameterBag PlotSimpleLinearRegression(ITemplateHost host, ParameterBag parameters)
        {
            SimpleLinearRegressionContext context = GetSimpleLinearRegressionContext(parameters);
            DataFrame fy = parameters["y"].AsDataFrame;
            DoubleVariable vy = fy.Variables[0]as DoubleVariable;
            DataFrame fx = parameters["x"].AsDataFrame;
            DoubleVariable vx = fx.Variables[0]as DoubleVariable;

            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("mdnValue", context.Slope);
            outputParameters.AddOutput("interceptValue", context.YIntercept);
            outputParameters.AddOutput("xtitle", vx.Title);
            outputParameters.AddOutput("ytitle", vy.Title);
            outputParameters.AddOutput("chartIsFullWidth", true);
            return outputParameters;
        }


        public static ParameterBag PlotResidualsSimple(ITemplateHost host, ParameterBag parameters)
        {
            SimpleLinearRegressionContext context = GetSimpleLinearRegressionContext(parameters);
            DataFrame fy = parameters["y"].AsDataFrame;
            DoubleVariable vy = fy.Variables[0]as DoubleVariable;
            DataFrame fx = parameters["x"].AsDataFrame;
            DoubleVariable vx = fx.Variables[0]as DoubleVariable;

            int nx = vx.Length;
            ParameterBag outputParameters = new ParameterBag();
            double[] z = new double[nx];
            double[] r = new double[nx];
            for (int j = 0; j <= nx - 1; j++)
            {
                z[j] = vx.Data[j] * context.Slope + context.YIntercept;
                r[j] = vy.Data[j] - z[j];
            }
            using (ChartRenderer ch = (ChartRenderer)ChartRendererFactory.ChartRendererFor(ChartDefinition.Empty()))
            {
                outputParameters.AddOutput("residualsVsY", ch.PlotXYAndReturnRtf(host, z, r, "Fitted " + vy.Title, "Residuals (Y - y fit)", "Residuals vs. Fitted Y [linear regression]", true, 0, false));
            }

            for (int j = 0; j <= nx - 1; j++)
            {
                z[j] = vx.Data[j] * context.Slope + context.YIntercept;
                r[j] = vy.Data[j] - z[j];
                z[j] = vx.Data[j];
            }
            using (ChartRenderer ch = (ChartRenderer)ChartRendererFactory.ChartRendererFor(ChartDefinition.Empty()))
            {
                outputParameters.AddOutput("residualsVsPredictor", ch.PlotXYAndReturnRtf(host, z, r, "Predictor: " + vx.Title, "Residuals (Y - y fit)", "Residuals vs. Predictor [linear regression]", true, 0, false));
            }

            double xf;
            ExFortran.Rank(r, z, 0, nx, 0, out xf);
            for (int j = 0; j <= nx - 1; j++)
            {
                //  van der Waerden normal scores, Conover P 396
                int ifault;
                z[j] = PDF.gauinv(z[j] / (Convert.ToDouble(nx) + 1.0), out ifault);
                if (ifault != 0)
                {
                    z[j] = Constant.MISSING;
                }
            }
            using (ChartRenderer ch = (ChartRenderer)ChartRendererFactory.ChartRendererFor(ChartDefinition.Empty()))
            {
                outputParameters.AddOutput("residualsNormalPlot", ch.PlotXYAndReturnRtf(host, r, z, "Residual (Y - y fit)", "van der Waerden normal score", "Normal Plot for Residuals [linear regression]", false, 0, false));
            }
            return outputParameters;
        }

        /// <summary>
        /// Plot Standard Error and 95% confidence interval for simple linear regression
        /// </summary>
        /// <param name="host"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public static ParameterBag PlotSeCi(ITemplateHost host, ParameterBag parameters)
        {
            SimpleLinearRegressionContext context = GetSimpleLinearRegressionContext(parameters);
            DataFrame fy = parameters["y"].AsDataFrame;
            DoubleVariable vy = fy.Variables[0]as DoubleVariable;
            DataFrame fx = parameters["x"].AsDataFrame;
            DoubleVariable vx = fx.Variables[0]as DoubleVariable;

            int nx = vy.Length;
            double REGGAMMA = parameters["reggamma"].AsDouble;
            context.CalcRcia(REGGAMMA);

            ChartDefinition cd = new ChartDefinition();
            cd.AddYSeries(vy.Data, vy.Title);
            cd.AddXSeries(vx.Data, vx.Title);
            ParameterBag outputParameters = new ParameterBag();

            using (ChartRenderer ch = (ChartRenderer)ChartRendererFactory.ChartRendererFor(cd))
            {
                // Ensure lines will fit on chart scale
                double maxpcon = double.MinValue;
                double minpcon = double.MaxValue;
                if (context.PERT != 0)
                {
                    for (double calcx = ch.DataMinX; calcx <= ch.DataMaxX; calcx += (ch.DataMaxX - ch.DataMinX) / 20.0)
                    {
                        double calcy = context.Slope * calcx + context.YIntercept;
                        double sey = Math.Sqrt(context.MS * (1.0 / Convert.ToDouble(nx) + Math.Pow((calcx - (context.SumX / Convert.ToDouble(nx))), 2.0) / context.SSX));
                        double pconu = calcy + (sey * context.PERT);
                        double pconl = calcy - (sey * context.PERT);
                        if (pconu > maxpcon)
                            maxpcon = pconu;
                        if (pconl < minpcon)
                            minpcon = pconl;
                    }
                }
                if (maxpcon > ch.DataMaxY)
                    ch.DataMaxY = maxpcon;
                if (minpcon < ch.DataMinY)
                    ch.DataMinY = minpcon;

                string rtf = ch.PlotLinearRegressionAndMaybeSeCiOrPredictionIntervalAndReturnRtf(host, "SE and " + Formatting.XRound((1.0 - context.P0) * 100, 1) + "% CI for regression estimate", context.Slope, context.YIntercept, true, vx.Title, vy.Title, context.PERT, nx, context.MS, context.SumX, context.SSX, false);
                outputParameters.AddOutput("chart", rtf);
            }
            return outputParameters;
        }


        public static ParameterBag PlotPredictionInterval(ITemplateHost host, ParameterBag parameters)
        {
            SimpleLinearRegressionContext context = GetSimpleLinearRegressionContext(parameters);
            DataFrame fy = parameters["y"].AsDataFrame;
            DoubleVariable vy = fy.Variables[0]as DoubleVariable;
            DataFrame fx = parameters["x"].AsDataFrame;
            DoubleVariable vx = fx.Variables[0]as DoubleVariable;
            double REGGAMMA = parameters["gamma"].AsDouble;
            int nx = vy.Length;
            context.CalcRcia(REGGAMMA);

            ParameterBag outputParameters = new ParameterBag();

            ChartDefinition cd = new ChartDefinition();
            cd.AddYSeries(vy.Data, vy.Title);
            cd.AddXSeries(vx.Data, vx.Title);
            using (ChartRenderer ch = (ChartRenderer)ChartRendererFactory.ChartRendererFor(cd))
            {

                double maxpcon = double.MinValue;
                double minpcon = double.MaxValue;
                if (context.PERT != 0)
                {
                    double calcx;
                    for (calcx = ch.DataMinX; calcx <= ch.DataMaxX; calcx += (ch.DataMaxX - ch.DataMinX) / 20.0)
                    {
                        double calcy = context.Slope * calcx + context.YIntercept;
                        double sey = Math.Sqrt(context.MS * (1.0 + (1.0 / Convert.ToDouble(nx) + Math.Pow((calcx - (context.SumX / Convert.ToDouble(nx))), 2.0) / context.SSX)));
                        double pconu = calcy + (sey * context.PERT);
                        double pconl = calcy - (sey * context.PERT);
                        if (pconu > maxpcon)
                        {
                            maxpcon = pconu;
                        }
                        if (pconl < minpcon)
                        {
                            minpcon = pconl;
                        }
                    }
                }
                if (maxpcon > ch.DataMaxY)
                {
                    ch.DataMaxY = maxpcon;
                }
                if (minpcon < ch.DataMinY)
                {
                    ch.DataMinY = minpcon;
                }

                string rtf = ch.PlotLinearRegressionAndMaybeSeCiOrPredictionIntervalAndReturnRtf(host, Formatting.XRound((1.0 - context.P0) * 100, 1) + "% Prediction Interval", context.Slope, context.YIntercept, true, vx.Title, vy.Title, context.PERT, nx, context.MS, context.SumX, context.SSX, true);

                outputParameters.AddOutput("chart", rtf);
            }

            return outputParameters;
        }


        public static ParameterBag RptCiMeanY(ITemplateHost host, ParameterBag parameters)
        {
            SimpleLinearRegressionContext context = GetSimpleLinearRegressionContext(parameters);
            DataFrame fy = parameters["y"].AsDataFrame;
            DoubleVariable vy = fy.Variables[0]as DoubleVariable;
            DataFrame fx = parameters["x"].AsDataFrame;
            DoubleVariable vx = fx.Variables[0]as DoubleVariable;

            int nx = vy.Length;
            double REGGAMMA = parameters["reggamma"].AsDouble;
            context.CalcRcia(REGGAMMA);
            double XA = parameters["xa"].AsDouble;
            double sey = Math.Sqrt(context.MS * (1.0 / Convert.ToDouble(nx) + Math.Pow((XA - (context.SumX / Convert.ToDouble(nx))), 2.0) / context.SSX));
            double ya = context.Slope * XA + context.YIntercept;
            double pcon = ya + (sey * context.PERT);
            double ncon = ya - (sey * context.PERT);
            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("var_x", vx.Title + " = " + host.RoundU(XA));
            outputParameters.AddOutput("var_y", vy.Title + " = " + host.RoundU(ya));
            outputParameters.AddOutput("ci_y", vy.Title + " = " + host.RoundU(sey));
            outputParameters.AddOutput("pci", Formatting.XRound(100 * (1.0 - context.P0), 1));
            outputParameters.AddOutput("fromi", host.RoundU(ncon));
            outputParameters.AddOutput("toi", host.RoundU(pcon));
            double spred = Math.Sqrt(context.MS * (1.0 + (1.0 / Convert.ToDouble(nx) + Math.Pow((XA - (context.SumX / Convert.ToDouble(nx))), 2.0) / context.SSX)));
            pcon = ya + (spred * context.PERT);
            ncon = ya - (spred * context.PERT);
            outputParameters.AddOutput("s_pred", host.RoundU(spred));
            outputParameters.AddOutput("pcp", Formatting.XRound(100 * (1.0 - context.P0), 1));
            outputParameters.AddOutput("fromp", host.RoundU(ncon));
            outputParameters.AddOutput("top", host.RoundU(pcon));
            return outputParameters;
        }


        public static ParameterBag CalcSimpleLinearRegressionCi(ITemplateHost host, ParameterBag parameters)
        {
            SimpleLinearRegressionContext context = GetSimpleLinearRegressionContext(parameters);
            DataFrame fy = parameters["y"].AsDataFrame;
            DoubleVariable vy = fy.Variables[0]as DoubleVariable;
            DataFrame fx = parameters["x"].AsDataFrame;
            DoubleVariable vx = fx.Variables[0]as DoubleVariable;
            double REGGAMMA = parameters["gamma"].AsDouble;
            int nx = vy.Length;
            context.CalcRcia(REGGAMMA);

            DataFrame outputFrame = new DataFrame();
            DoubleVariable reg = new DoubleVariable { Title = "Reg~" + vy.Title };

            reg.EnsureLength(nx);
            outputFrame.Variables.Add(reg);

            DoubleVariable uci = new DoubleVariable { Title = "UCI~" + vy.Title };

            uci.EnsureLength(nx);
            outputFrame.Variables.Add(uci);

            DoubleVariable lci = new DoubleVariable { Title = "LCI~" + vy.Title };

            lci.EnsureLength(nx);
            outputFrame.Variables.Add(lci);

            for (int j = 0; j < nx; j++)
            {
                double sey = Math.Sqrt(context.MS * (1.0 / Convert.ToDouble(nx) + Math.Pow((vy.Data[j] - (context.SumX / Convert.ToDouble(nx))), 2.0) / context.SSX));
                double ya = context.Slope * vx.Data[j] + context.YIntercept;
                double pcon = ya + (sey * context.PERT);
                double ncon = ya - (sey * context.PERT);
                reg.SetData(j, ya);
                uci.SetData(j, pcon);
                lci.SetData(j, ncon);
            }
            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("data", outputFrame);
            return outputParameters;
        }

        public static ParameterBag RptPrincipalComponentsRegressionCorrelation(ITemplateHost host, ParameterBag parameters)
        {
            DataFrame frame = parameters["data"].AsDataFrame;
            bool correctForReversal = parameters["correctForReversal"].AsBoolean;
            double[,] x;
            double[,] xc;
            double[,] xr;
            double[,] v;
            int N;
            int nx;
            return CalcPrincipal(host, frame, out x, out xc, out xr, out v, 1, out N, out nx, correctForReversal);
        }

        public static ParameterBag RptPrincipalComponentsRegressionCovariance(ITemplateHost host, ParameterBag parameters)
        {
            DataFrame frame = parameters["data"].AsDataFrame;
            bool correctForReversal = parameters["correctForReversal"].AsBoolean;
            double[,] x;
            double[,] xc;
            double[,] xr;
            double[,] v;
            int N;
            int nx;
            return CalcPrincipal(host, frame, out x, out xc, out xr, out v, 2, out N, out nx, correctForReversal);
        }

        ///  <summary>
        ///  
        ///  </summary>
        /// <param name="frame"></param>
        /// <param name="x">Set to...</param>
        ///  <param name="xc">Set to...</param>
        ///  <param name="xr">Set to...</param>
        ///  <param name="v">Set to...</param>
        ///  <param name="irv">Passed in as 1 for correlation ("corral"), 2 for covariance ("covar")</param>
        ///  <param name="N">Set to the number of columns in the input</param>
        ///  <param name="nx">Set to the number of rows in the input</param>
        /// <param name="host"></param>
        /// <param name="correctForReversal"></param>
        /// <remarks></remarks>
        private static ParameterBag CalcPrincipal(ITemplateHost host, DataFrame frame, out double[,] x, out double[,] xc, out double[,] xr, out double[,] v, int irv, out int N, out int nx, bool correctForReversal)
        {
            int j; int i;
            int ifault = 0;
            double[,] u; double[] w; double cum = 0; double dsum = 0;

            N = frame.VariableCount;
            nx = frame.Variables[0].Length;
            x = new double[N + 1, nx + 1];
            bool[] revx = new bool[N + 1 ];
            int inx = 0;
            for (j = 1; j <= nx; j++)
            {
                bool iskip = false;
                for (i = 1; i <= N; i++)
                {
                    if ((frame.Variables[i - 1] as DoubleVariable).Data[j - 1] == Constant.MISSING)
                    {
                        iskip = true;
                    }
                }
                if (!(iskip))
                {
                    inx = inx + 1;
                    for (i = 1; i <= N; i++)
                        x[i, inx] = (frame.Variables[i - 1] as DoubleVariable).Data[j - 1];
                }
            }
            nx = inx;

            // first do the pca to check for scale reversal like Stata alpha command without the asis subcommand
            x_principal(N, ref nx, ref x, out xc, out xr, out u, out w, out v, ref irv, ref ifault);
            bool signrev = false;
            string revlab = string.Empty;
            x_pscore1_corr(N, nx, x, v, irv, revx);
            for (i = 1; i <= N; i++)
            {
                if (!(revx[i]))
                {
                    signrev = true;
                    break;
                }
            }
            if (signrev)
            {
                if (correctForReversal)
                {
                    for (i = 1; i <= N; i++)
                    {
                        if (!(revx[i]))
                        {
                            for (j = 1; j <= nx; j++)
                            {
                                x[i, j] = -x[i, j];
                            }
                            revlab = revlab + frame.Variables[i - 1].Title + "; ";
                        }
                    }
                    if (revlab.Length > 0)
                    {
                        revlab = revlab.Substring(0, revlab.Length - 2);
                        revlab = "Sign was reversed for: " + revlab;
                    }
                }
            }

            // do the pca with the selected covariance or correlation approach
            x_principal(N, ref nx, ref x, out xc, out xr, out u, out w, out v, ref irv, ref ifault);

            ParameterBag outputParameters = new ParameterBag();
            if (ifault == 0)
            {
                for (j = 1; j <= N; j++)
                {
                    dsum = dsum + w[j];
                }
                outputParameters.AddOutput("type", irv == 1 ? "correlation" : "covariance");
                IList<ParameterBag> warnList = null;
                if (revlab.Length > 0)
                {
                    warnList = new List<ParameterBag>();
                    ParameterBag warnParameters = new ParameterBag();
                    warnParameters.AddOutput("warn", revlab);
                    warnList.Add(warnParameters);
                }
                outputParameters.AddOutput("*warn", warnList);
                IList<ParameterBag> tableList = new List<ParameterBag>();
                for (j = 1; j <= N; j++)
                {
                    ParameterBag tableParameters = new ParameterBag();
                    double prop = w[j] / dsum;
                    cum = cum + prop;
                    tableParameters.AddOutput("comp", Formatting.XRound(Convert.ToDouble(j), 0));
                    tableParameters.AddOutput("eigen", host.RoundU(w[j]));
                    tableParameters.AddOutput("prop", Formatting.XRound(prop * 100.0, 2));
                    tableParameters.AddOutput("cum", Formatting.XRound(cum * 100.0, 2));
                    tableList.Add(tableParameters);
                }
                outputParameters.AddOutput("*table", tableList);
            }
            MultipleLinearRegressionContext context = new MultipleLinearRegressionContext { X = x, H = xc, R2 = xr, V = v, M = irv, P = N, N = nx };
            outputParameters.Add("context", new FilledParameter(FilledParameterDirection.Input, context));
            return outputParameters;
        }


        ///  <summary>
        ///  
        ///  </summary>
        ///  <param name="N"></param>
        ///  <param name="nx"></param>
        ///  <param name="x"></param>
        ///  <param name="xc">Set to...</param>
        ///  <param name="xr">Set to...</param>
        ///  <param name="u">Set to...</param>
        ///  <param name="w">Set to...</param>
        ///  <param name="v">Set to...</param>
        ///  <param name="irv"></param>
        ///  <param name="ifault"></param>
        ///  <remarks></remarks>
        private static void x_principal(int N, ref int nx, ref double[,] x, out double[,] xc, out double[,] xr, out double[,] u, out double[] w, out double[,] v, ref int irv, ref int ifault)
        {
            int i; int j;

            xc = new double[N + 1, N + 1];
            xr = new double[N + 1, N + 1];
            u = new double[N + 1, N + 1];
            if (irv == 1)
            {
                for (j = 1; j <= N; j++)
                {
                    for (i = 1; i <= N; i++)
                    {
                        Regress1.X_Comat(out xc[j, i], out xr[j, i], ref x, ref nx, ref j, ref i);
                        u[j, i] = xr[j, i];
                    }
                }
            }
            else
            {
                for (j = 1; j <= N; j++)
                {
                    for (i = 1; i <= N; i++)
                    {
                        Regress1.X_Comat(out xc[j, i], out xr[j, i], ref x, ref nx, ref j, ref i);
                        u[j, i] = xc[j, i];
                    }
                }
            }
            w = new double[N + 1 ];
            v = new double[N + 1, N + 1];
            for (i = 1; i <= N; i++)
            {
                w[i] = 1.0;
            }
            Regress1.X_SVDCP(ref u, ref N, ref N, ref w, ref v, ref ifault);
            double wmax = w[1];
            for (j = 1; j <= N; j++)
            {
                if (wmax < w[j])
                {
                    wmax = w[j];
                }
            }
            double tol = wmax * Constant.EPSNEG;
            for (j = 1; j <= N; j++)
            {
                if (w[j] < tol)
                {
                    w[j] = 0.0;
                }
            }
            Regress1.X_Eigsrt(ref w, ref v, N);
        }


        private static void x_pscore1_corr(int N, int nx, double[,] x, double[,] v, int irv, bool[] negcorr)
        {
            int j;
            int i;
            double[] av = null; double[] sd = null;

            double[] ps = new double[nx + 1];
            double[] xx = new double[nx + 1];
            if (irv == 1)
            {
                av = new double[N + 1];
                sd = new double[N + 1 ];
                for (j = 1; j <= N; j++)
                {
                    Regress1.x_avsd(x, nx, j, out av[j], out sd[j]);
                }
            }
            for (j = 1; j <= nx; j++)
            {
                for (i = 1; i <= 1; i++)
                {
                    ps[j] = 0.0;
                    int k;
                    for (k = 1; k <= N; k++)
                    {
                        if (irv == 1)
                        {
                            //  References to av and sd in the following line are OK, because they are only referenced if irv=1 and are always set in this case.
                            Debug.Assert(null != av && null != sd);
                            ps[j] = ps[j] + v[k, i] * ((x[k, j] - av[k]) / sd[k]);
                        }
                        else
                        {
                            ps[j] = ps[j] + v[k, i] * x[k, j];
                        }
                    }
                }
            }
            for (i = 1; i <= N; i++)
            {
                for (j = 1; j <= nx; j++)
                {
                    xx[j] = x[i, j];
                }
                negcorr[i] = MathDbl.corr(xx, ps, 1, nx, true) < 0.0;
            }
        }




        public static ParameterBag RptMultipleLinearRegression(ITemplateHost host, ParameterBag parameters)
        {
            DataFrame outcomeFrame = parameters["outcome"].AsDataFrame;
            DoubleVariable outcomeVariable = outcomeFrame.Variables[0]as DoubleVariable;
            bool weighted = parameters.ContainsKey("weights") && parameters["weights"] != null;
            DoubleVariable weightsVariable;
            if (weighted)
            {
                DataFrame weightsFrame = parameters["weights"].AsDataFrame;
                weightsVariable = weightsFrame.Variables[0]as DoubleVariable;
            }
            else
            {
                //  Weights aren't in use, use all 1s
                double[] allOnes = new double[outcomeVariable.Length];
                for (int i = 0; i <= allOnes.Length - 1; i++)
                {
                    allOnes[i] = 1;
                }
                weightsVariable = new DoubleVariable(allOnes);
            }
            bool calculateIntercept = parameters["calculateIntercept"].AsBoolean;
            DataFrame predictorsFrame = parameters["predictors"].AsDataFrame;

            //  ReDim PASSX(1, 1) - note 1 for calculate intercept, 0 for not
            //  y() is outcome data, cd(0) is its title
            //  wy() is weights, cd(1) is its title
            //  x(,) is predictors, xd() is titles of predictors
            //  Then in SD2 was transferred to ARR2(0) = y, arr2(1)=wy, arr2(2...) = predictors

            //  calc_multi
            MultipleLinearRegressionContext context = new MultipleLinearRegressionContext();
            int iq;
            // Arr2(0,n)=y data
            // Arr2(1,n)=weight data
            context.N = outcomeVariable.Length;
            int ip = predictorsFrame.VariableCount;
            context.P = ip;
            context.Y = new double[context.N + 1 ];
            context.S = new double[context.N + 1 ];
            context.outcomeTitle = outcomeVariable.Title;
            context.weightTitle = weighted ? weightsVariable.Title : string.Empty;
            if (calculateIntercept)
            {
                // intercept
                context.DoC = true;
                context.P += 1;
                context.X = new double[context.N + 1, context.P + 1];
                iq = 1;
                for (int j = 1; j <= context.N; j++)
                {
                    context.X[j, 1] = 1;
                }
            }
            else
            {
                // no intercept
                context.DoC = false;
                context.X = new double[context.N + 1, context.P + 1];
                iq = 0;
            }
            context.Titles = new string[predictorsFrame.VariableCount + iq + 1 ];
            for (int i = 0; i <= predictorsFrame.VariableCount - 1; i++)
            {
                context.Titles[i + 1 + iq] = predictorsFrame.Variables[i].Title;
            }
            int cnt = 0;
            for (int j = 0; j <= context.N - 1; j++)
            {
                bool OK = outcomeVariable.Data[j] != Constant.MISSING;
                if (weightsVariable.Data[j] == Constant.MISSING)
                {
                    OK = false;
                }
                for (int k = 1; k <= ip; k++)
                {
                    if ((predictorsFrame.Variables[k - 1] as DoubleVariable).Data[j] == Constant.MISSING)
                    {
                        OK = false;
                    }
                }
                if (OK)
                {
                    cnt += 1;
                    context.Y[cnt] = outcomeVariable.Data[j];
                    context.S[cnt] = weightsVariable.Data[j];
                    for (int k = 1; k <= ip; k++)
                    {
                        context.X[cnt, k + iq] = (predictorsFrame.Variables[k - 1] as DoubleVariable).Data[j];
                    }
                }
            }
            if (context.N != cnt)
            {
                host.Warning((context.N - cnt).ToString() + " observations dropped due to missing data." + "\r\n" + "Make sure that observations with missing data are not a subgroup", "Linear Regression");
                context.N = cnt;
            }
            x_glin(context, context.Y, context.S, context.X, out context.SE, out context.B, out context.VIF, ref context.SSREG, ref context.SSY, out context.H, out context.R, out context.FV, ref context.DoC, ref context.N, ref context.P, ref context.M);
            return x_showmr(host, context, context.SE, context.B, true, context.N, context.P, false, context.M);
        }


        private static void x_glin(MultipleLinearRegressionContext context, double[] yd, double[] weight, double[,] xd, out double[] SEB, out double[] bd, out double[] vif, ref double bss, ref double ctss, out double[,] xtxi, out double[] er, out double[] yfit, ref bool DoC, ref int nx, ref int P, ref int ifault)
        {
            int i; int j;
            int iwtcol;
            int incep; int indep; int irank = 0;
            int nrmiss = 0;
            double rdf = 0; double rss = 0;
            double s;
            context.warn = string.Empty;
            int original_p = P;
            SEB = new double[P + 1 ];
            bd = new double[P * P + 1];
            xtxi = new double[P + 1, P + 1];
            er = new double[nx + 1 ];
            yfit = new double[nx + 1 ];
            if (DoC)
            {
                incep = 1;
                indep = P - 1;
            }
            else
            {
                incep = 0;
                indep = P;
            }
            int iwt = 0;
            for (i = 1; i <= nx; i++)
            {
                if (weight[i] != 1.0)
                {
                    iwt = 1;
                }
            }
            double[,] xx = new double[nx + 1, indep + 1 + iwt + 1 ];
            double[,] r = new double[P + 1, P + 1];
            double[] D = new double[P + 1 ];
            double[] xmin = new double[P + 1 ];
            double[] XMax = new double[P + 1 ];
            double[] WK = new double[2 * (P + 1) + 1 ];
            int[] idum = new int[1 + 1 ];
            for (i = 1; i <= nx; i++)
            {
                for (j = 1 + incep; j <= indep + incep; j++)
                {
                    xx[i, j - incep] = xd[i, j];
                }
                if (iwt == 1)
                {
                    xx[i, indep + 1] = weight[i];
                    xx[i, indep + 2] = yd[i];
                }
                else
                {
                    xx[i, indep + 1] = yd[i];
                }
            }
            if (iwt == 0)
            {
                iwtcol = 0;
            }
            else
            {
                iwtcol = indep + 1;
            }
            Regress1.glsqr(0, incep, 0, nx, indep + iwt + 1, xx, -indep, idum, -1, idum, 0, iwtcol, bd, r, D, ref irank, ref rdf, ref rss, ref nrmiss, xmin, XMax, WK, ref ifault);
            if (irank != P & indep > 1)
            {
                int ctr = incep;
                for (j = 1 + incep; j <= P; j++)
                {
                    if (r[j, j] == 0)
                    {
                        context.warn += context.Titles[j] + ", ";
                    }
                    else
                    {
                        ctr = ctr + 1;
                        for (i = 1; i <= nx; i++)
                        {
                            xd[i, ctr] = xx[i, j - incep];
                        }
                        //  Swap the titles
                        string temp = context.Titles[ctr];
                        context.Titles[ctr] = context.Titles[j];
                        context.Titles[j] = temp;
                    }
                }
                if (context.warn.Length > 0)
                {
                    context.warn = context.warn.Substring(0, context.warn.Length - 2) + " dropped from the model due to very high correlation with other variable(s) included.";
                }
                P = irank;
                SEB = new double[P + 1 ];
                bd = new double[P * P + 1];
                xtxi = new double[P + 1, P + 1];
                er = new double[nx + 1];
                yfit = new double[nx + 1];
                if (DoC)
                {
                    incep = 1;
                    indep = P - 1;
                }
                else
                {
                    incep = 0;
                    indep = P;
                }
                r = new double[P + 1, P + 1];
                D = new double[P + 1 ];
                xmin = new double[P + 1];
                XMax = new double[P + 1];
                WK = new double[2 * (P + 1) + 1];
                idum = new int[1 + 1];
                for (i = 1; i <= nx; i++)
                {
                    for (j = 1 + incep; j <= indep + incep; j++)
                    {
                        xx[i, j - incep] = xd[i, j];
                    }
                    if (iwt == 1)
                    {
                        xx[i, indep + 1] = weight[i];
                        xx[i, indep + 2] = yd[i];
                    }
                    else
                    {
                        xx[i, indep + 1] = yd[i];
                    }
                }
                Regress1.glsqr(0, incep, 0, nx, indep + iwt + 1, xx, -indep, idum, -1, idum, 0, iwtcol, bd, r, D, ref irank, ref rdf, ref rss, ref nrmiss, xmin, XMax, WK, ref ifault);
            }
            if (ifault != 0)
            {
                context.warn = "QR solution failed and SVD used, extreme results may be invalid.";
                P = original_p;
                vif = new double[P + 1 ];
                SEB = new double[P + 1 ];
                bd = new double[P + 1 ];
                xtxi = new double[P + 1, P + 1];
                er = new double[nx + 1 ];
                double[] sig = new double[nx + 1 ];
                for (i = 1; i <= P; i++)
                {
                    vif[i] = Constant.MISSING;
                }
                for (i = 1; i <= nx; i++)
                {
                    sig[i] = Math.Sqrt(1.0 / weight[i]);
                }
                x_glin_svd(yd, sig, xd, SEB, bd, bss, ctss, xtxi, er, out yfit, DoC, nx, P, out ifault);
                return;
            }
            double[,] covb = new double[P + 1, P + 1];
            vif = new double[P + 1];
            // variance inflation
            if (incep == 1 & r[1, 1] > 0.0)
            {
                vif[1] = Math.Pow(r[1, 1], 2.0);
            }
            for (j = incep + 1; j <= P; j++)
            {
                if (r[j, j] > 0.0)
                {
                    for (i = incep + 1; i <= j; i++)
                    {
                        if (r[i, i] > 0.0)
                        {
                            vif[j] = vif[j] + Math.Pow(r[i, j], 2.0);
                        }
                    }
                }
            }
            for (j = 1; j <= P; j++)
            {
                if (r[j, j] <= 0.0)
                {
                    vif[j] = Constant.MISSING;
                }
            }
            Regress1.rcovarb(P, r, 1.0, covb, ref ifault);
            for (j = 1; j <= P; j++)
            {
                if (vif[j] != Constant.MISSING)
                {
                    vif[j] = vif[j] * covb[j, j];
                }
            }
            double rms = rss / rdf;
            Regress1.rcovarb(P, r, rms, covb, ref ifault);
            for (i = 1; i <= P; i++)
            {
                SEB[i] = Math.Sqrt(covb[i, i]);
            }
            for (i = 1; i <= P; i++)
            {
                for (j = 1; j <= P; j++)
                {
                    xtxi[i, j] = covb[i, j] / rms;
                }
            }
            for (i = 1; i <= nx; i++)
            {
                s = 0.0;
                for (j = 1; j <= P; j++)
                {
                    s = s + xd[i, j] * bd[j];
                }
                yfit[i] = s;
                er[i] = yd[i] - s;
            }
            bss = 0.0;
            for (i = 1; i <= (P - incep); i++)
            {
                s = 0.0;
                j = incep + i;
                if (r[j, j] > 0.0)
                {
                    int k;
                    for (k = 0; k <= P - j; k++)
                    {
                        s = s + r[j, j + k] * bd[j + k];
                    }
                }
                bss = bss + s * s;
            }
            ctss = bss + rss;
        }


        private static ParameterBag x_showmr(ITemplateHost host, MultipleLinearRegressionContext context, double[] SEB, double[] bd, bool DoC, int nx, int P, bool pol, int errcode)
        {
            int i;

            double rdf = Convert.ToDouble(nx - P);
            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("isPoly", pol);
            if (context.warn.Length > 0 || errcode != 0)
            {
                ParameterBag warnParameters = new ParameterBag();
                warnParameters.AddOutput("warn", context.warn);
                IList<ParameterBag> warnList = new List<ParameterBag>();
                warnList.Add(warnParameters);
                outputParameters.AddOutput("*warn", warnList);
            }
            else
            {
                outputParameters.AddOutput("*warn", null);
            }
            if (errcode != 0)
            {
                outputParameters.AddOutput("*row", null);
            }
            else
            {
                IList<ParameterBag> rowList = new List<ParameterBag>();
                outputParameters.AddOutput("*row", rowList);
                ParameterBag rowParameters = new ParameterBag();
                rowList.Add(rowParameters);
                IList<ParameterBag> colList = new List<ParameterBag>();
                rowParameters.AddOutput("*col", colList);
                for (i = 1; i <= P; i++)
                {
                    ParameterBag colParameters = new ParameterBag();
                    colList.Add(colParameters);
                    double prob = PDF.tvalp(Math.Abs(bd[i] / SEB[i]), Convert.ToDouble(nx - P));
                    if (prob > 1.0 - prob)
                    {
                        prob = 1.0 - prob;
                    }
                    prob = 2.0 * prob;
                    if (DoC)
                    {
                        if (i == 1)
                        {
                            colParameters.AddOutput("label", "Intercept");
                            colParameters.AddOutput("b", "0");
                        }
                        else
                        {
                            colParameters.AddOutput("label", context.Titles[i]);
                            colParameters.AddOutput("b", (i - 1).ToString());
                        }
                    }
                    else
                    {
                        colParameters.AddOutput("label", context.Titles[i]);
                        colParameters.AddOutput("b", i.ToString());
                    }
                    colParameters.AddOutput("val_b", host.RoundU(bd[i]));
                    double t;
                    double rp;
                    if (SEB[i] != 0.0)
                    {
                        t = bd[i] / SEB[i];
                        rp = t / Math.Sqrt(t * t + rdf);
                    }
                    else
                    {
                        t = Constant.MISSING;
                        rp = Constant.MISSING;
                    }
                    if (DoC == false || i > 1)
                    {
                        colParameters.AddOutput("rp", "r = " + host.RoundU(rp));
                    }
                    else
                    {
                        colParameters.AddOutput("rp", string.Empty);
                    }
                    colParameters.AddOutput("t", host.RoundU(t));
                    colParameters.AddOutput("p", host.pval(prob));
                }
                if (!string.IsNullOrEmpty(context.weightTitle))
                    rowParameters.AddOutput("y", context.outcomeTitle + " (weighted by " + context.weightTitle + ")");
                else
                    rowParameters.AddOutput("y", context.outcomeTitle);
                IList<ParameterBag> zList = new List<ParameterBag>();
                rowParameters.AddOutput("*z", zList);
                int j;
                for (j = 1; j <= P; j++)
                {
                    string z;
                    if (j > 1 && bd[j] >= 0.0)
                    {
                        z = " +";
                    }
                    else { z = " "; }
                    z = z + host.RoundU(bd[j]);
                    if (j > 1 || !(DoC))
                    {
                        z = z + " " + context.Titles[j];
                    }
                    ParameterBag zParameters = new ParameterBag();
                    zList.Add(zParameters);
                    zParameters.AddOutput("z", z);
                }
            }
            outputParameters.Add("context", new FilledParameter(FilledParameterDirection.Input, context));
            //  For best subset code
            string[] predictorTitles = new string[P - 2 + 1 ];
            for (i = 2; i <= P; i++)
            {
                predictorTitles[i - 2] = context.Titles[i];
            }
            outputParameters["predictorTitles"] = new FilledParameter(FilledParameterDirection.Input, new DataFrame(new StringVariable(predictorTitles)));
            outputParameters["candidatePredictors"] = new FilledParameter(FilledParameterDirection.Input, x_prep_intermr(context));
            return outputParameters;
        }


        private static void x_glin_svd(double[] yd, double[] sig, double[,] xd, double[] SEB, double[] bd, double bss, double ctss, double[,] xtxi, double[] er, out double[] yfit, bool DoC, int nx, int P, out int ifault)
        {
            int i;
            double wt;

            // SEB = new double[P + 1 ];
            // bd = new double[P + 1 ];
            // xtxi = new double[P + 1, P + 1];
            // er = new double[nx + 1 ];
            yfit = new double[nx + 1 ];
            double[,] ud = new double[nx + 1, P + 1];
            double[,] vd = new double[P + 1, P + 1];
            double[] wd = new double[P + 1];
            double[] cn = new double[P + 1];
            double[] hi = new double[nx + 1 ];
            bss = 0.0;
            ctss = 0.0;
            Regress1.X_SVGO(xd, yd, sig, nx, P, bd, ud, vd, wd, yfit, er, out ifault);
            Regress1.X_SVDVRD(xd, vd, wd, nx, P, xtxi, cn, hi);
            double sy = 0.0;
            double sn = 0.0;
            for (i = 1; i <= nx; i++)
            {
                wt = 1.0 / (sig[i] * sig[i]);
                sn = sn + wt;
                sy = sy + yd[i] * wt;
            }
            double ym = sy / sn;
            ctss = 0.0;
            bss = 0.0;
            if (DoC)
            {
                for (i = 1; i <= nx; i++)
                {
                    wt = 1.0 / (sig[i] * sig[i]);
                    ctss = ctss + (yd[i] * wt - ym) * (yd[i] * wt - ym);
                    bss = bss + (yfit[i] - ym) * (yfit[i] - ym);
                }
            }
            else
            {
                for (i = 1; i <= nx; i++)
                {
                    wt = 1.0 / (sig[i] * sig[i]);
                    ctss = ctss + (yd[i] * wt) * (yd[i] * wt);
                    bss = bss + (yfit[i]) * (yfit[i]);
                }
            }
            double rss = ctss - bss;
            double rdf = Convert.ToDouble((nx - 1) - (P - 1));
            double rms = rss / rdf;
            for (i = 1; i <= P; i++)
            {
                SEB[i] = Math.Sqrt(xtxi[i, i] * rms);
            }
        }


        public static ParameterBag RptMultipleLinearRegressionAnova(ITemplateHost host, ParameterBag parameters)
        {
            MultipleLinearRegressionContext context = GetMultipleLinearRegressionContext(parameters);
            double ci = parameters["ci"].AsDouble;
            double dw;
            string extra;

            double rdf = Convert.ToDouble(context.N - context.P);
            double bdf = context.P == 1 || context.DoC == false ? context.P : context.P - 1;
            double bms = context.SSREG / bdf;
            double rss = context.SSY - context.SSREG;
            double rms = rss / rdf;
            double vr = bms / rms;
            int tdf = context.DoC ? context.N - 1 : context.N;
            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("reg_sum", host.RoundU(context.SSREG));
            outputParameters.AddOutput("reg_df", Math.Floor(bdf).ToString());
            outputParameters.AddOutput("reg_mean", host.RoundU(bms));
            outputParameters.AddOutput("res_sum", host.RoundU(context.SSY - context.SSREG));
            outputParameters.AddOutput("res_df", Math.Floor(rdf).ToString());
            outputParameters.AddOutput("res_mean", host.RoundU(rms));
            outputParameters.AddOutput("tot_sum", host.RoundU(context.SSY));
            outputParameters.AddOutput("tot_df", tdf.ToString());
            outputParameters.AddOutput("mse", host.RoundU(Math.Sqrt(rms)));
            outputParameters.AddOutput("f", host.RoundU(vr));
            double prob = PDF.fvalp(vr, bdf, rdf);
            outputParameters.AddOutput("p", host.pval(prob));
            double r2 = context.SSREG / context.SSY;
            double r = Math.Sqrt(r2);
            if (Math.Floor(bdf) == 1 & context.N > 3)
            {
                if (context.DoC == false)
                {
                    if (context.B[1] < 0)
                    {
                        r = -r;
                    }
                }
                else
                {
                    if (context.B[2] < 0)
                    {
                        r = -r;
                    }
                }
                double P0 = 1 - ci;
                double GAMMA = 1.0 - (P0 / 2.0);
                int ifault;
                double rcit = PDF.gauinv(GAMMA, out ifault);
                double fz = 0.5 * Math.Log((1.0 + r) / (1.0 - r));
                double fz1 = fz - (rcit / Math.Sqrt(Convert.ToDouble(context.N - 3)));
                double fz2 = fz + (rcit / Math.Sqrt(Convert.ToDouble(context.N - 3)));
                double con1 = (Math.Exp(2.0 * fz1) - 1.0) / (Math.Exp(2.0 * fz1) + 1.0);
                double con2 = (Math.Exp(2.0 * fz2) - 1.0) / (Math.Exp(2.0 * fz2) + 1.0);
                extra = "  [" + Formatting.XRound(100 * (1.0 - P0), 1) + "%CI = " + host.RoundU(con1) + " to " + host.RoundU(con2) + "]";
            }
            else
            {
                extra = string.Empty;
            }
            outputParameters.AddOutput("r", host.RoundU(r) + extra);
            outputParameters.AddOutput("r2", host.RoundU(r2 * 100) + "%");
            r2 = 1.0 - (rss / Convert.ToDouble(context.N - context.P)) / (context.SSY / Convert.ToDouble(context.N - 1));
            if (r2 < 0.0)
            {
                r2 = 0.0;
            }
            outputParameters.AddOutput("ra2", host.RoundU(r2 * 100) + "%");
            Regress1.x_dwsd(context.R, context.N, out dw);
            outputParameters.AddOutput("dw", host.RoundU(dw));
            return outputParameters;
        }

        public static ParameterBag RptMultipleLinearRegressionPrediction(ITemplateHost host, ParameterBag parameters)
        {
            MultipleLinearRegressionContext context = GetMultipleLinearRegressionContext(parameters);
            double GAMMA = parameters["ci"].AsDouble;
            DataFrame candidatePredictors = parameters["candidatePredictors"].AsDataFrame;

            int iq; int i;
            double P0; double cit;
            double cl; double pl;

            double[] newx = new double[context.P + 1 ];
            bool lsqmean = true;
            if (context.DoC)
            {
                iq = 1;
                newx[1] = 1.0;
            }
            else
            {
                iq = 0;
            }
            StringVariable valueVariable = candidatePredictors.Variables[1]as StringVariable;
            DoubleVariable oldValueVariable = candidatePredictors.Variables[2]as DoubleVariable;
            for (i = 1 + iq; i <= context.P; i++)
            {
                newx[i] = Parsing.Cdbl_Txt(valueVariable.Data[i - 1 - iq]);
                if (newx[i] != oldValueVariable.Data[i - 1 - iq])
                    lsqmean = false;
            }
            double newy = 0.0;
            for (i = 1; i <= context.P; i++)
                newy += newx[i] * context.B[i];

            MathDbl.civ(context.N - context.P, out cit, GAMMA, out P0);
            ParameterBag outputParameters = new ParameterBag();
            IList<ParameterBag> xList = new List<ParameterBag>();
            outputParameters.AddOutput("*x", xList);
            for (i = 1 + iq; i <= context.P; i++)
            {
                ParameterBag xParameters = new ParameterBag();
                xParameters.AddOutput("x", context.Titles[i - iq] + " = " + host.RoundU(newx[i]));
            }
            string msg = lsqmean ? "  (least squares mean)" : string.Empty;
            outputParameters.AddOutput("y", context.outcomeTitle + " = " + host.RoundU(newy) + msg);
            double rdf = Convert.ToDouble((context.N - 1) - (context.P - 1));
            double rss = context.SSY - context.SSREG;
            double rms = rss / rdf;
            Regress1.x_ciyp(newx, context.H, context.P, rms, cit, out cl, out pl);
            outputParameters.AddOutput("ci_pc", (Formatting.XRound(100 * (1.0 - P0), 1)));
            outputParameters.AddOutput("ci_from", host.RoundU(newy - cl));
            outputParameters.AddOutput("ci_to", host.RoundU(newy + cl));
            outputParameters.AddOutput("pred_pc", Formatting.XRound(100 * (1.0 - P0), 1));
            outputParameters.AddOutput("pred_from", host.RoundU(newy - pl));
            outputParameters.AddOutput("pred_to", host.RoundU(newy + pl));
            return outputParameters;
        }


        public static ParameterBag PlotMultipleLinearRegressionResiduals(ITemplateHost host, ParameterBag parameters)
        {
            MultipleLinearRegressionContext context = GetMultipleLinearRegressionContext(parameters);
            int i; int j;
            double[] r;


            ParameterBag outputParameters = new ParameterBag();
            IList<ParameterBag> chartList = new List<ParameterBag>();
            outputParameters.AddOutput("*chart", chartList);

            using (ChartRenderer ch = (ChartRenderer)ChartRendererFactory.ChartRendererFor(ChartDefinition.Empty()))
            {
                context.R[0] = Constant.MISSING;
                chartList.Add(new ParameterBag("chart", new FilledParameter(FilledParameterDirection.Output, ch.PlotXYAndReturnRtf(host, context.FV, context.R, "Fitted Y (y fit)", "Residual (Y - y fit)", "Residuals vs. Fitted Y [linear regression]", true, 0, false))));
            }

            for (i = 1; i <= context.P; i++)
            {
                if (i > 1 | !(context.DoC))
                {
                    int k = context.DoC ? i - 1 : i;
                    r = new double[context.N + 1];
                    r[0] = Constant.MISSING;
                    for (j = 1; j <= context.N; j++)
                    {
                        r[j] = context.X[j, i];
                    }
                    using (ChartRenderer ch = (ChartRenderer)ChartRendererFactory.ChartRendererFor(ChartDefinition.Empty()))
                    {
                        chartList.Add(new ParameterBag("chart", new FilledParameter(FilledParameterDirection.Output, ch.PlotXYAndReturnRtf(host, r, context.R, "Predictor: " + context.Titles[i], "Residual (Y - y fit)", "Residuals vs. Predictor " + k.ToString() + " [linear regression]", true, 0, false))));
                    }
                }
            }
            r = new double[context.N + 1 ];
            double xf;
            ExFortran.Rank(context.R, r, 1, context.N, 0, out xf);
            for (j = 1; j <= context.N; j++)
            {
                // van der Waerden normal scores, Conover P 396
                int ifault;
                r[j] = PDF.gauinv(r[j] / (Convert.ToDouble(context.N) + 1.0), out ifault);
                if (ifault != 0)
                {
                    r[j] = Constant.MISSING;
                }
            }
            using (ChartRenderer ch = (ChartRenderer)ChartRendererFactory.ChartRendererFor(ChartDefinition.Empty()))
            {
                chartList.Add(new ParameterBag("chart", new FilledParameter(FilledParameterDirection.Output, ch.PlotXYAndReturnRtf(host, context.R, r, "Residual (Y - y fit)", "van der Waerden normal score", "Normal Plot for Residuals (linear regression)", true, 0, false))));
            }
            return outputParameters;
        }


        public static ParameterBag RptMultipleLinearRegressionParameterDetail(ITemplateHost host, ParameterBag parameters)
        {
            MultipleLinearRegressionContext context = GetMultipleLinearRegressionContext(parameters);
            int i; int iq = 0;
            double cit; double P0;

            double GAMMA = parameters["ci"].AsDouble;
            int df = context.N - context.P;
            MathDbl.civ(df, out cit, GAMMA, out P0);
            if (context.DoC)
            {
                iq = 1;
            }
            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("pc", Formatting.XRound(100 * (1.0 - P0), 2));
            IList<ParameterBag> vList = new List<ParameterBag>();
            outputParameters.AddOutput("*v", vList);
            for (i = 1; i <= context.P; i++)
            {
                string Q;
                if (i == 1 && context.DoC)
                {
                    Q = "constant";
                }
                else { Q = context.Titles[i - iq + 1]; }
                ParameterBag vParameters = new ParameterBag();
                vList.Add(vParameters);
                vParameters.AddOutput("label", Q);
                vParameters.AddOutput("coef", host.RoundU(context.B[i]));
                vParameters.AddOutput("err", host.RoundU(context.SE[i]));
                vParameters.AddOutput("from", host.RoundU(context.B[i] - cit * context.SE[i]));
                vParameters.AddOutput("to", host.RoundU(context.B[i] + cit * context.SE[i]));
            }
            bool used_svd = true;
            for (i = 1; i <= context.P; i++)
            {
                if (context.VIF[i] != Constant.MISSING)
                {
                    used_svd = false;
                    break;
                }
            }
            if (!(used_svd))
            {
                double[] vif2 = new double[context.P + 1 ];
                string[] ti = new string[context.P + 1 ];
                double sumv = 0.0;
                for (i = 1; i <= context.P; i++)
                {
                    if (i == 1 && context.DoC)
                    {
                        ti[i] = "_*_";
                    }
                    else
                    {
                        ti[i] = context.Titles[i - iq + 1];
                        sumv = sumv + context.VIF[i];
                    }
                    vif2[i] = context.VIF[i];
                }
                int iv;
                if (context.DoC)
                {
                    iv = context.P - 1;
                }
                else { iv = context.P; }
                double meanv = sumv / Convert.ToDouble(iv);
                // bubble sort
                bool bsorted = false;
                while (!(bsorted))
                {
                    bsorted = true;
                    for (i = context.P - 1; i >= 1; i--)
                    {
                        if (vif2[i + 1] > vif2[i])
                        {
                            bsorted = false;
                            double temp = vif2[i];
                            string tempx = ti[i];
                            vif2[i] = vif2[i + 1];
                            ti[i] = ti[i + 1];
                            vif2[i + 1] = temp;
                            ti[i + 1] = tempx;
                        }
                    }
                }
                // print for independent variables
                IList<ParameterBag> vifList = new List<ParameterBag>();
                outputParameters.AddOutput("*vif", vifList);
                for (i = 1; i <= context.P; i++)
                {
                    if (ti[i] != "_*_")
                    {
                        ParameterBag vifParameters = new ParameterBag();
                        vifList.Add(vifParameters);
                        vifParameters.AddOutput("label", ti[i]);
                        vifParameters.AddOutput("vif", host.RoundU(vif2[i]));
                        vifParameters.AddOutput("x", vif2[i] > 20.0 ? Formatting.ASTERISK : string.Empty);
                        vifParameters.AddOutput("rvif", host.RoundU(1.0 / vif2[i]));
                    }
                }
                outputParameters.AddOutput("meanv", host.RoundU(meanv));
            }
            else
            {
                // not calc if SVD used cos no r matrix
                outputParameters.AddOutput("*vif", null);
                outputParameters.AddOutput("meanv", "not calculated");
            }
            return outputParameters;
        }

        /// <summary>
        /// Renders a 2D array ary[dim1, dim2] into a FilledParameter of list of bags of lists of bags as required by the report renderer.  In the output, dim1 varies faster (inner dimension) and dim2 more slowly (outer dimension).
        /// </summary>
        /// <typeparam name="ArrayType">The type of the array to be rendered</typeparam>
        /// <typeparam name="RenderedType">The type returned by the renderer - allows use of many different renderers</typeparam>
        /// <param name="ary"></param>
        /// <param name="outerLowerBound"></param>
        /// <param name="outerLength"></param>
        /// <param name="innerLowerBound"></param>
        /// <param name="innerLength"></param>
        /// <param name="majorName"></param>
        /// <param name="minorName"></param>
        /// <param name="renderer"></param>
        /// <returns></returns>
        public static List<ParameterBag> ToOutputParameter<ArrayType, RenderedType>(ArrayType[,] ary, int outerLowerBound, int outerLength, int innerLowerBound, int innerLength, string innerName, string valueName, Func<ArrayType, RenderedType> renderer)
        {
            List<ParameterBag> outerList = new List<ParameterBag>();
            for (int i = outerLowerBound; i < outerLowerBound + outerLength; i++)
            {
                ParameterBag outerParameters = new ParameterBag();
                outerList.Add(outerParameters);
                List<ParameterBag> innerList = new List<ParameterBag>();
                outerParameters.AddOutput(innerName, innerList);
                for (int j = innerLowerBound; j < innerLowerBound + innerLength; j++)
                {
                    ParameterBag innerParameters = new ParameterBag();
                    innerList.Add(innerParameters);
                    innerParameters.AddOutput(valueName, renderer(ary[j, i]));
                }
            }
            return outerList;
        }

        public static ParameterBag RptXxi(ITemplateHost host, ParameterBag parameters)
        {
            MultipleLinearRegressionContext context = GetMultipleLinearRegressionContext(parameters);

            double rdf = Convert.ToDouble(context.N - context.P);
            double rss = context.SSY - context.SSREG;
            double rms = rss / rdf;

            DataFrame frame = new DataFrame();
            IList<Variable> pendedVariables = new List<Variable>();
            for (int i = 1; i <= context.P; i++)
            {
                DoubleVariable vxxi = new DoubleVariable();
                frame.Variables.Add(vxxi);
                DoubleVariable vcv = new DoubleVariable();
                pendedVariables.Add(vcv);
                vxxi.Title = "XXi " + i.ToString();
                vxxi.EnsureLength(context.P);
                vcv.Title = "Var-CoVar " + i.ToString();
                vcv.EnsureLength(context.P);
                for (int j = 1; j <= context.P; j++)
                {
                    vxxi.SetData(j - 1, context.H[i, j]);
                    vcv.SetData(j - 1, context.H[i, j] * rms);
                }
            }
            //  Spacer
            frame.Variables.Add(new DoubleVariable());
            //  Add all the cv variables
            foreach (Variable v in pendedVariables)
                frame.Variables.Add(v);

            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("data", frame);

            outputParameters.AddOutput("*xxi", ToOutputParameter(context.H, 1, context.P, 1, context.P, "*col", "x", (v) => host.RoundU(v)));
            outputParameters.AddOutput("*covar", ToOutputParameter(context.H, 1, context.P, 1, context.P, "*col", "x", (v) => host.RoundU(rms * v)));

            return outputParameters;
        }

        public static ParameterBag RptMultipleLinearRegressionResiduals(ITemplateHost host, ParameterBag parameters)
        {
            MultipleLinearRegressionContext context = GetMultipleLinearRegressionContext(parameters);
            double ci = parameters["ci"].AsDouble;
            bool shouldSaveFittedY = parameters["saveFittedY"].AsBoolean;
            bool shouldSaveStudentisedResidual = parameters["saveStudentised"].AsBoolean;
            bool shouldSaveJackknifeResidual = parameters["saveJackknife"].AsBoolean;

            // Check if we need to save the data
            double alpha = 1.0 - ci;
            double hicrit = Math.Min((3 * Convert.ToDouble(context.P)) / Convert.ToDouble(context.N), 0.99);
            double srcrit = PDF.tfromp(alpha / 2.0, Convert.ToDouble(context.N - context.P));
            double jackcrit = PDF.tfromp(alpha / 2.0, Convert.ToDouble(context.N - context.P - 1));
            double cdcrit = PDF.ffromp(Convert.ToDouble(context.N - context.P), Convert.ToDouble(context.P), alpha);
            double dfcrit = 2.0 * Math.Sqrt(Convert.ToDouble(context.P) / Convert.ToDouble(context.N));
            double[] hi = new double[context.N + 1];
            double[] sey = new double[context.N + 1];
            double[] rstd = new double[context.N + 1 ];
            double[] rstudent = new double[context.N + 1];
            double[] cd = new double[context.N + 1];
            double[] dff = new double[context.N + 1 ];
            double rdf = Convert.ToDouble(context.N - context.P);
            //  root mean square is estimate of population variance
            double rms = (context.SSY - context.SSREG) / rdf;
            // double Con = ( rdf - 1.0 ) / rdf; - unused
            for (int i = 1; i <= context.N; i++)
            {
                double wt = context.S[i];
                double xcx = 0.0;
                int j;
                for (j = 1; j <= context.P; j++)
                {
                    double s = 0.0;
                    for (int k = 1; k <= context.P; k++)
                        s += context.H[j, k] * context.X[i, k];
                    xcx += s * context.X[i, j];
                }
                sey[i] = Math.Sqrt(rms * xcx);
                hi[i] = wt * xcx;
                if (wt == 0.0 | hi[i] == 0.0)
                {
                    rstudent[i] = Constant.MISSING;
                    rstd[i] = Constant.MISSING;
                    cd[i] = Constant.MISSING;
                    dff[i] = Constant.MISSING;
                }
                else
                {
                    double varer = (1.0 - hi[i]) / wt;
                    double SI = Math.Sqrt((wt * (context.N - context.P) * rms - Math.Pow((wt * context.R[i]), 2.0) / (1.0 - hi[i])) / (rdf - 1.0));
                    //  jackknife (SAS calls it rstudent) - see Kleinbaum
                    rstudent[i] = (wt * context.R[i]) / (SI * Math.Sqrt(1.0 - hi[i]));
                    //  studentised residual, standardised residual is er(i)/sqr(rms)
                    rstd[i] = context.R[i] / (Math.Sqrt(rms * varer));
                    cd[i] = (rstd[i] * rstd[i] * (hi[i] / (1.0 - hi[i]))) / Convert.ToDouble(context.P);
                    dff[i] = Math.Sqrt(hi[i] / (1.0 - hi[i])) * rstudent[i];
                }
            }
            ParameterBag outputParameters = new ParameterBag();
            IList<ParameterBag> yfitList = new List<ParameterBag>();
            outputParameters.AddOutput("*yfit", yfitList);
            for (int i = 1; i <= context.N; i++)
            {
                ParameterBag yfitParameters = new ParameterBag();
                yfitList.Add(yfitParameters);
                yfitParameters.AddOutput("index", i.ToString());
                yfitParameters.AddOutput("y", host.RoundU(context.Y[i]));
                yfitParameters.AddOutput("fit_y", host.RoundU(context.FV[i]));
                yfitParameters.AddOutput("std_err", host.RoundU(sey[i]));
                yfitParameters.AddOutput("res", host.RoundU(context.R[i]));
            }
            if (shouldSaveFittedY)
            {
                DataFrame yfitFrame = new DataFrame();
                DoubleVariable vYFit = new DoubleVariable(context.N, "Y Fit");
                yfitFrame.Variables.Add(vYFit);
                DoubleVariable vSdYFit = new DoubleVariable(context.N, "SD Y Fit");
                yfitFrame.Variables.Add(vSdYFit);
                DoubleVariable vResidual = new DoubleVariable(context.N, "Residual");
                yfitFrame.Variables.Add(vResidual);
                for (int i = 1; i <= context.N; i++)
                {
                    vYFit.SetData(i - 1, context.FV[i]);
                    vSdYFit.SetData(i - 1, sey[i]);
                    vResidual.SetData(i - 1, context.R[i]);
                }
                outputParameters.AddOutput("yfit", yfitFrame);
            }
            IList<ParameterBag> studentisedList = new List<ParameterBag>();
            outputParameters.AddOutput("*studentised", studentisedList);
            for (int i = 1; i <= context.N; i++)
            {
                ParameterBag studentisedParameters = new ParameterBag();
                studentisedList.Add(studentisedParameters);
                studentisedParameters.AddOutput("index", i.ToString());
                studentisedParameters.AddOutput("index*", Math.Abs(rstd[i]) > srcrit ? Formatting.ASTERISK : string.Empty);
                studentisedParameters.AddOutput("stu", host.RoundU(rstd[i]));
                studentisedParameters.AddOutput("stu*", Math.Abs(hi[i]) > hicrit ? Formatting.ASTERISK : string.Empty);
                studentisedParameters.AddOutput("hi", host.RoundU(hi[i]));
                studentisedParameters.AddOutput("hi*", Math.Abs(cd[i]) > cdcrit ? Formatting.ASTERISK : string.Empty);
                studentisedParameters.AddOutput("cook", host.RoundU(cd[i]));
            }
            if (shouldSaveStudentisedResidual)
            {
                DataFrame studentisedFrame = new DataFrame();
                DoubleVariable vResidual = new DoubleVariable(context.N, "Studentised Residual");
                studentisedFrame.Variables.Add(vResidual);
                DoubleVariable vLeverage = new DoubleVariable(context.N, "Leverage");
                studentisedFrame.Variables.Add(vLeverage);
                DoubleVariable vCook = new DoubleVariable(context.N, "Cook's Distance");
                studentisedFrame.Variables.Add(vCook);
                for (int i = 1; i <= context.N; i++)
                {
                    vResidual.SetData(i - 1, rstd[i]);
                    vLeverage.SetData(i - 1, hi[i]);
                    vCook.SetData(i - 1, cd[i]);
                }
                outputParameters.AddOutput("studentised", studentisedFrame);
            }
            IList<ParameterBag> jackknifeList = new List<ParameterBag>();
            outputParameters.AddOutput("*jackknife", jackknifeList);
            for (int i = 1; i <= context.N; i++)
            {
                ParameterBag jackknifeParameters = new ParameterBag();
                jackknifeList.Add(jackknifeParameters);
                jackknifeParameters.AddOutput("index", i.ToString());
                jackknifeParameters.AddOutput("index*", Math.Abs(rstudent[i]) > jackcrit ? Formatting.ASTERISK : string.Empty);
                jackknifeParameters.AddOutput("jack", host.RoundU(rstudent[i]));
                jackknifeParameters.AddOutput("jack*", Math.Abs(dff[i]) > dfcrit ? Formatting.ASTERISK : string.Empty);
                jackknifeParameters.AddOutput("dfit", host.RoundU(dff[i]));
            }
            if (shouldSaveJackknifeResidual)
            {
                DataFrame jackknifeFrame = new DataFrame();
                DoubleVariable vResidual = new DoubleVariable(context.N, "Jackknife Residual");
                jackknifeFrame.Variables.Add(vResidual);
                DoubleVariable vDFIT = new DoubleVariable(context.N, "DFIT");
                jackknifeFrame.Variables.Add(vDFIT);
                for (int i = 1; i <= context.N; i++)
                {
                    vResidual.SetData(i - 1, rstudent[i]);
                    vDFIT.SetData(i - 1, dff[i]);
                }
                outputParameters.AddOutput("jackknife", jackknifeFrame);
            }
            outputParameters.AddOutput("zhi", host.RoundU(hicrit));
            outputParameters.AddOutput("zcook", host.RoundU(cdcrit));
            outputParameters.AddOutput("zstu", host.RoundU(srcrit));
            outputParameters.AddOutput("zjack", host.RoundU(jackcrit));
            outputParameters.AddOutput("zdfit", host.RoundU(dfcrit));
            return outputParameters;
        }


        public static ParameterBag RptMultipleLinearRegressionBestSubset(ITemplateHost host, ParameterBag parameters)
        {
            MultipleLinearRegressionContext context = GetMultipleLinearRegressionContext(parameters);
            bool[] selectedPredictors = ((bool[])(parameters["selectedPredictors"].Data));
            bool shouldUseMaximumF = "maximumF".Equals(parameters["selector"].AsString);
            int errcode = 0;

            int forced = 0; int C; int kept = 0; int[] keep = null;
            double maxf = 0; double maxr2 = 0;

            double rmsorig = (context.SSY - context.SSREG) / Convert.ToDouble((context.N - 1) - (context.P - 1));
            double mincp = ((context.SSY - context.SSREG) / rmsorig) - Convert.ToDouble(context.N - 2 * (context.P + 1));
            int iq = 0;
            if (context.DoC)
            {
                iq = 1;
            }
            double[] yfit;
            int[] force = new int[context.P + 1 ];
            for (C = 0; C <= selectedPredictors.Length - 1; C++)
            {
                if (selectedPredictors[C])
                {
                    forced = forced + 1;
                    force[forced] = C + 1 + iq;
                }
            }
            if (context.DoC)
            {
                forced = forced + 1;
                // create temp variable for copying values 
                int[] transTemp12 = new int[forced + 1 ];
                Array.Copy(force, transTemp12, Math.Min(force.Length, transTemp12.Length));
                force = transTemp12;
                force[forced] = 1;
            }
            for (int j = 1; j <= context.P; j++)
            {
                int[] preds = new int[j + 1 ];
                for (int i = 1; i <= j; i++)
                {
                    preds[i] = i;
                }
                int at = j;
                do
                {
                    bool oktry = x_forceinc(forced, force, j, preds);
                    if (context.DoC)
                    {
                        if (j == 1)
                        {
                            oktry = false;
                        }
                        else
                        {
                            for (int k = 1; k <= j; k++)
                            {
                                if (preds[k] == 1 & k != 1)
                                {
                                    int temp = preds[k];
                                    preds[k] = preds[1];
                                    preds[1] = temp;
                                }
                            }
                        }
                    }
                    if (oktry)
                    {
                        double[,] tryx = new double[context.N + 1, j + 1];
                        for (int i = 1; i <= context.N; i++)
                        {
                            for (int k = 1; k <= j; k++)
                            {
                                tryx[i, k] = context.X[i, preds[k]];
                            }
                        }
                        x_glin(context, context.Y, context.S, tryx, out context.SE, out context.B, out context.VIF, ref context.SSREG, ref context.SSY, out context.H, out context.R, out yfit, ref context.DoC, ref context.N, ref j, ref errcode);
                        if (errcode == 0)
                        {
                            double rdf = Convert.ToDouble((context.N - 1) - (j - 1));
                            double bdf = j > 1 ? Convert.ToDouble(j - 1) : Convert.ToDouble(j);
                            double bms = context.SSREG / bdf;
                            double rms = (context.SSY - context.SSREG) / rdf;
                            double f = bms / rms;
                            double r2 = context.SSREG / context.SSY;
                            double cp = ((context.SSY - context.SSREG) / rmsorig) - Convert.ToDouble(context.N - 2 * (j + 1));
                            bool isBetter = shouldUseMaximumF ? f > maxf : cp <= mincp;
                            if (isBetter)
                            {
                                maxf = f;
                                mincp = cp;
                                maxr2 = r2;
                                kept = j;
                                keep = new int[kept + 1 ];
                                for (int k = 1; k <= j; k++)
                                {
                                    keep[k] = preds[k];
                                }
                            }
                        }
                        else
                        {
                            throw new TemplateOperationCancelledException();
                        }
                    }
                    for (int L = j; L >= 0; L--)
                    {
                        if (preds[L] < context.P - (j - L))
                        {
                            at = L;
                            break;
                        }
                    }
                    if (at == 0 || j == context.P)
                    {
                        break;
                    }
                    preds[at] = preds[at] + 1;
                    if (at != j)
                    {
                        for (int Q = at + 1; Q <= j; Q++)
                        {
                            preds[Q] = preds[at] + (Q - at);
                        }
                        at = j;
                    }
                }
                while (true);
            }

            ParameterBag outputParameters = new ParameterBag();
            IList<ParameterBag> selectedList = new List<ParameterBag>();
            outputParameters.AddOutput("*selected", selectedList);
            for (int j = 1 + iq; j <= kept; j++)
            {
                ParameterBag selectedParameters = new ParameterBag();
                selectedList.Add(selectedParameters);
                selectedParameters.AddOutput("label", context.Titles[keep[j] - iq + 1]);
            }
            outputParameters.AddOutput("f", host.RoundU(maxf));
            outputParameters.AddOutput("r2", host.RoundU(maxr2));
            outputParameters.AddOutput("cp", host.RoundU(mincp));

            //  Hack the data in the analysis to drop some of the predictors
            context.P = kept;
            double[,] newX = new double[context.N + 1, context.P + 1];
            for (int j = 1; j <= context.N; j++)
            {
                for (int k = 1; k <= context.P; k++)
                {
                    newX[j, k] = context.X[j, keep[k]];
                }
            }
            context.X = newX;
            for (int j = 1 + iq; j <= kept; j++)
            {
                int ix1 = j - iq + 1;
                int ix2 = keep[j] - iq + 1;
                string temp = context.Titles[ix1];
                context.Titles[ix1] = context.Titles[ix2];
                context.Titles[ix2] = temp;
            }
            // create temp variable for copying values 
            string[] transTemp13 = new string[context.P + 1 ];
            Array.Copy(context.Titles, transTemp13, Math.Min(context.Titles.Length, transTemp13.Length));
            context.Titles = transTemp13;
            x_glin(context, context.Y, context.S, context.X, out context.SE, out context.B, out context.VIF, ref context.SSREG, ref context.SSY, out context.H, out context.R, out yfit, ref context.DoC, ref context.N, ref context.P, ref errcode);

            ParameterBag otherParameters = x_showmr(host, context, context.SE, context.B, context.DoC, context.N, context.P, false, errcode);
            foreach (KeyValuePair<string, FilledParameter> pair in otherParameters.Pairs)
                outputParameters[pair.Key] = pair.Value;
            outputParameters.AddOutput("subsetApplied", true);
            return outputParameters;
        }

        private static bool x_forceinc(int forced, int[] force, int j, int[] preds)
        {
            if (forced > 0)
            {
                int cnt = 0;
                for (int i = 1; i <= forced; i++)
                {
                    for (int k = 1; k <= j; k++)
                    {
                        if (force[i] == preds[k])
                        {
                            cnt++;
                        }
                    }
                }
                return cnt == forced;
            }
            return true;
        }


        private static DataFrame x_prep_intermr(MultipleLinearRegressionContext context)
        {
            int iq = context.DoC ? 1 : 0;

            DataFrame frame = new DataFrame();
            StringVariable keyVariable = new StringVariable { Title = "Name" };
            frame.Variables.Add(keyVariable);
            StringVariable valueVariable = new StringVariable { Title = "Value" };
            frame.Variables.Add(valueVariable);
            DoubleVariable oldValueVariable = new DoubleVariable { Title = "Old value" };
            frame.Variables.Add(oldValueVariable);

            for (int j = 1 + iq; j <= context.P; j++)
            {
                double mu = 0.0;
                double a = context.X[1, j];
                double b = Constant.MISSING;
                bool bin = true;
                int i;
                for (i = 1; i <= context.N; i++)
                {
                    double z = context.X[i, j];
                    if (z != Constant.MISSING)
                    {
                        mu = mu + z;
                        if (z != a)
                        {
                            if (z != b)
                            {
                                if (b == Constant.MISSING)
                                {
                                    b = z;
                                }
                                else
                                {
                                    bin = false;
                                }
                            }
                        }
                    }
                }
                if (bin)
                {
                    mu = 0.5;
                }
                else
                {
                    mu = mu / Convert.ToDouble(context.N);
                }
                keyVariable.SetData(j - 1 - iq, context.Titles[j]);
                valueVariable.SetData(j - 1 - iq, mu.ToString());
                oldValueVariable.SetData(j - 1 - iq, Parsing.Cdbl_Txt(mu.ToString()));
            }
            return frame;
        }


        public static ParameterBag RptPrincipalComponentsRegressionCoefficients(ITemplateHost host, ParameterBag parameters)
        {
            MultipleLinearRegressionContext context = GetMultipleLinearRegressionContext(parameters);
            DataFrame frame = parameters["data"].AsDataFrame;
            ParameterBag outputParameters = new ParameterBag();
            IList<ParameterBag> pcList = new List<ParameterBag>();
            outputParameters.AddOutput("*pc", pcList);
            for (int j = 1; j <= context.P; j++)
            {
                ParameterBag pcParameters = new ParameterBag();
                pcList.Add(pcParameters);
                pcParameters.AddOutput("pc", j.ToString());
            }
            IList<ParameterBag> varList = new List<ParameterBag>();
            outputParameters.AddOutput("*var", varList);
            for (int j = 1; j <= context.P; j++)
            {
                ParameterBag varParameters = new ParameterBag();
                varList.Add(varParameters);
                varParameters.AddOutput("lab", frame.Variables[j - 1].Title);
                IList<ParameterBag> resList = new List<ParameterBag>();
                varParameters.AddOutput("*res", resList);
                for (int i = 1; i <= context.P; i++)
                {
                    ParameterBag resParameters = new ParameterBag();
                    resList.Add(resParameters);
                    resParameters.AddOutput("res", host.RoundU(context.V[j, i]));
                }
            }
            return outputParameters;
        }


        public static ParameterBag RptCronbach(ITemplateHost host, ParameterBag parameters)
        {
            MultipleLinearRegressionContext context = GetMultipleLinearRegressionContext(parameters);
            DataFrame frame = parameters["data"].AsDataFrame;
            int k = context.P;
            int N = context.N;
            double[,] x = context.X;
            double[,] r = context.R2;
            double totvar = 0; double alpha; double cl;
            double cco = parameters["ci"].AsDouble;
            if (cco <= 0.0 || cco >= 1.0)
                cco = 0.95;

            double[] qv = new double[k + 1 ];
            for (int i = 1; i <= N; i++)
            {
                x[0, i] = 0.0;
                for (int j = 1; j <= k; j++)
                {
                    x[0, i] = x[0, i] + x[j, i];
                }
            }
            for (int j = 0; j <= k; j++)
            {
                double av;
                Regress1.x_avsd(x, N, j, out av, out qv[j]);
                qv[j] = qv[j] * qv[j];
                if (j > 0)
                {
                    totvar = totvar + qv[j];
                }
            }
            double talpha = (Convert.ToDouble(k) / Convert.ToDouble(k - 1)) * (1.0 - (totvar / qv[0]));
            // Stata technical bulletin 56: SG 144
            double f = PDF.ffromp(Convert.ToDouble(k - 1) * Convert.ToDouble(N - 1), Convert.ToDouble(N - 1), (1.0 - cco));
            if (f == Constant.MISSING)
            {
                cl = Constant.MISSING;
            }
            else
            {
                cl = 1.0 - ((1.0 - talpha) * f);
                if (cl < 0.0 | cl > talpha)
                {
                    cl = 0.0;
                }
            }
            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("raw_t", host.RoundU(talpha));
            outputParameters.AddOutput("pc", Formatting.XRound(cco * 100.0, 1));
            outputParameters.AddOutput("raw_cl", host.RoundU(cl));
            IList<ParameterBag> rawList = new List<ParameterBag>();
            outputParameters.AddOutput("*raw", rawList);
            for (int L = 1; L <= k; L++)
            {
                totvar = 0.0;
                for (int i = 1; i <= N; i++)
                {
                    x[0, i] = 0.0;
                    for (int j = 1; j <= k; j++)
                    {
                        if (j != L)
                        {
                            x[0, i] = x[0, i] + x[j, i];
                        }
                    }
                }
                for (int j = 0; j <= k; j++)
                {
                    if (j != L)
                    {
                        qv[j] = 0.0;
                        double av;
                        Regress1.x_avsd(x, N, j, out av, out qv[j]);
                        qv[j] = qv[j] * qv[j];
                        if (j > 0)
                        {
                            totvar = totvar + qv[j];
                        }
                    }
                }
                if (k > 2)
                {
                    alpha = (Convert.ToDouble(k - 1) / Convert.ToDouble(k - 2)) * (1.0 - (totvar / qv[0]));
                }
                else
                {
                    alpha = Constant.MISSING;
                }
                ParameterBag rawParameters = new ParameterBag();
                rawList.Add(rawParameters);
                rawParameters.AddOutput("lab", frame.Variables[L - 1].Title);
                rawParameters.AddOutput("a", host.RoundU(alpha));
                if (alpha != Constant.MISSING && alpha - talpha > 0.1)
                {
                    rawParameters.AddOutput("x", Formatting.ASTERISK);
                }
                else
                {
                    rawParameters.AddOutput("x", string.Empty);
                }
                rawParameters.AddOutput("a-t", host.RoundU(alpha - talpha));
            }
            // FOR STANDARDIZED DATA (see SAS & SPSS)
            double rtot = 0.0;
            int ctr = 0;
            for (int i = 2; i <= k; i++)
            {
                for (int j = 1; j <= i - 1; j++)
                {
                    rtot = rtot + r[j, i];
                    ctr = ctr + 1;
                }
            }
            double rbar = rtot / Convert.ToDouble(ctr);
            talpha = Convert.ToDouble(k) * rbar / (1.0 + Convert.ToDouble(k - 1) * rbar);
            // Stata technical bulletin 56: SG 144
            f = PDF.ffromp(Convert.ToDouble(k - 1) * Convert.ToDouble(N - 1), Convert.ToDouble(N - 1), (1.0 - cco));
            if (f == Constant.MISSING)
            {
                cl = Constant.MISSING;
            }
            else
            {
                cl = 1.0 - ((1.0 - talpha) * f);
                if (cl < 0.0 | cl > talpha)
                {
                    cl = 0.0;
                }
            }
            outputParameters.AddOutput("standard_t", host.RoundU(talpha));
            outputParameters.AddOutput("standard_cl", host.RoundU(cl));
            IList<ParameterBag> standardList = new List<ParameterBag>();
            outputParameters.AddOutput("*standard", standardList);
            for (int L = 1; L <= k; L++)
            {
                rtot = 0.0;
                ctr = 0;
                for (int i = 2; i <= k; i++)
                {
                    for (int j = 1; j <= i - 1; j++)
                    {
                        if (j != L & i != L)
                        {
                            rtot = rtot + r[j, i];
                            ctr = ctr + 1;
                        }
                    }
                }
                rbar = rtot / Convert.ToDouble(ctr);
                if (k < 2)
                {
                    alpha = Constant.MISSING;
                }
                else
                {
                    alpha = Convert.ToDouble(k - 1) * rbar / (1.0 + Convert.ToDouble(k - 2) * rbar);
                }
                ParameterBag standardParameters = new ParameterBag();
                standardList.Add(standardParameters);
                standardParameters.AddOutput("lab", frame.Variables[L - 1].Title);
                standardParameters.AddOutput("a", host.RoundU(alpha));
                if (alpha != Constant.MISSING && alpha - talpha > 0.1)
                {
                    standardParameters.AddOutput("x", Formatting.ASTERISK);
                }
                else
                {
                    standardParameters.AddOutput("x", string.Empty);
                }
                standardParameters.AddOutput("a-t", host.RoundU(alpha - talpha));
            }
            return outputParameters;
        }


        public static ParameterBag RptPrincipalComponentsRegressionScores(ITemplateHost host, ParameterBag parameters)
        {
            MultipleLinearRegressionContext context = GetMultipleLinearRegressionContext(parameters);
            int N = context.P;
            int nx = context.N;
            double[,] x = context.X;
            double[,] v = context.V;
            double[] av = null; double[] sd = null;

            ParameterBag outputParameters = new ParameterBag();
            IList<ParameterBag> pcList = new List<ParameterBag>();
            outputParameters.AddOutput("*pc", pcList);
            for (int j = 1; j <= N; j++)
            {
                ParameterBag pcParameters = new ParameterBag();
                pcList.Add(pcParameters);
                pcParameters.AddOutput("pc", j.ToString());
            }
            if (context.M == 1)
            {
                av = new double[N + 1];
                sd = new double[N + 1];
                for (int j = 1; j <= N; j++)
                {
                    Regress1.x_avsd(x, nx, j, out av[j], out sd[j]);
                }
            }
            IList<ParameterBag> rowList = new List<ParameterBag>();
            outputParameters.AddOutput("*row", rowList);
            for (int j = 1; j <= nx; j++)
            {
                ParameterBag rowParameters = new ParameterBag();
                rowList.Add(rowParameters);
                rowParameters.AddOutput("row", j.ToString());
                IList<ParameterBag> resList = new List<ParameterBag>();
                rowParameters.AddOutput("*res", resList);
                for (int i = 1; i <= N; i++)
                {
                    double ps = 0.0;
                    for (int k = 1; k <= N; k++)
                    {
                        if (context.M == 1)
                        {
                            Debug.Assert(null != av && null != sd);
                            ps = ps + v[k, i] * ((x[k, j] - av[k]) / sd[k]);
                        }
                        else
                        {
                            ps = ps + v[k, i] * x[k, j];
                        }
                    }
                    ParameterBag resParameters = new ParameterBag();
                    resList.Add(resParameters);
                    resParameters.AddOutput("res", host.RoundU(ps));
                }
            }
            return outputParameters;
        }


        public static ParameterBag RptPrincipalComponentsRegressionMatrix(ITemplateHost host, ParameterBag parameters)
        {
            MultipleLinearRegressionContext context = GetMultipleLinearRegressionContext(parameters);
            DataFrame frame = parameters["data"].AsDataFrame;
            int N = context.P;
            double[,] xc = context.H;
            double[,] XR = context.R2;
            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("type", context.M == 1 ? "Correlation" : "Variance-Covariance");
            IList<ParameterBag> lab1List = new List<ParameterBag>();
            outputParameters.AddOutput("*lab1", lab1List);
            for (int j = 1; j <= N; j++)
            {
                ParameterBag lab1Parameters = new ParameterBag();
                lab1List.Add(lab1Parameters);
                lab1Parameters.AddOutput("lab", frame.Variables[j - 1].Title);
            }
            IList<ParameterBag> lab2List = new List<ParameterBag>();
            outputParameters.AddOutput("*lab2", lab2List);
            for (int j = 1; j <= N; j++)
            {
                ParameterBag lab2Parameters = new ParameterBag();
                lab2List.Add(lab2Parameters);
                lab2Parameters.AddOutput("lab", frame.Variables[j - 1].Title);
                IList<ParameterBag> resList = new List<ParameterBag>();
                lab2Parameters.AddOutput("*res", resList);
                for (int i = 1; i <= N; i++)
                {
                    ParameterBag resParameters = new ParameterBag();
                    resList.Add(resParameters);
                    resParameters.AddOutput("res", host.RoundU(context.M == 1 ? XR[j, i] : xc[j, i]));
                }
            }
            return outputParameters;
        }


        public static ParameterBag RptLinearizedEstimates(ITemplateHost host, ParameterBag parameters)
        {
            DataFrame fY = parameters["y"].AsDataFrame;
            DoubleVariable vY = fY.Variables[0]as DoubleVariable;
            DataFrame fX = parameters["x"].AsDataFrame;
            DoubleVariable vX = fX.Variables[0]as DoubleVariable;
            int model = 0;
            if (parameters.ContainsKey("model"))
                model = Parsing.Cint_Txt(parameters["model"].AsString);

            int nx = vY.Length;
            double[][] copiesRemovingMissingRows = Numerics.Utilities.RemoveMissingRows(new double[][] { vY.Data, vX.Data }, 0, nx, 0);
            SimpleLinearRegressionContext context = new SimpleLinearRegressionContext(copiesRemovingMissingRows[1], copiesRemovingMissingRows[0]);
            ParameterBag outputParameters = new ParameterBag();
            switch (model)
            {
                case 0:
                    context.ApplyToY(Math.Log);
                    outputParameters.AddOutput("modelDescription", "(Exponential)  Y = a * exp(b * x)");
                    context.CalculateLeastSquaresMethod();
                    context.A = Math.Exp(context.YIntercept);
                    context.G = context.Slope;
                    break;
                case 1:
                    context.ApplyToY(Math.Log);
                    context.ApplyToX(Math.Log);
                    outputParameters.AddOutput("modelDescription", "(Geometric / Power)  Y = a * x^b");
                    context.CalculateLeastSquaresMethod();
                    context.A = Math.Exp(context.YIntercept);
                    context.G = context.Slope;
                    break;
                case 2:
                    context.ApplyToY(a => 1.0 / a);
                    context.ApplyToX(a => 1.0 / a);
                    outputParameters.AddOutput("modelDescription", "(Hyperbolic)  Y = x / (a + b * x)");
                    context.CalculateLeastSquaresMethod();
                    context.A = context.Slope;
                    context.G = context.YIntercept;
                    break;
                default:
                    throw new Exception("Unknown model");
            }

            outputParameters.AddOutput("lab_y", vY.Title);
            outputParameters.AddOutput("lab_x", vX.Title);
            outputParameters.AddOutput("a", host.RoundU(context.A));
            outputParameters.AddOutput("b", host.RoundU(context.G));
            outputParameters.AddOutput("r", host.RoundU(context.R));
            outputParameters.AddOutput("r2", host.RoundU(context.R * context.R));
            outputParameters.AddOutput("ste", host.RoundU(context.SeEst));
            outputParameters.Add("context", new FilledParameter(FilledParameterDirection.Input, context));
            return outputParameters;
        }


        public static ParameterBag RptLinearizedEstimateInterpolation(ITemplateHost host, ParameterBag parameters)
        {
            DataFrame fY = parameters["y"].AsDataFrame;
            DoubleVariable vY = fY.Variables[0]as DoubleVariable;
            DataFrame fX = parameters["x"].AsDataFrame;
            DoubleVariable vX = fX.Variables[0]as DoubleVariable;
            SimpleLinearRegressionContext context = GetSimpleLinearRegressionContext(parameters);
            int model = 0;
            if (parameters.ContainsKey("model"))
            {
                model = Parsing.Cint_Txt(parameters["model"].AsString);
            }
            double newx = parameters["newx"].AsDouble;
            double newy = 0;
            switch (model)
            {
                case 0:
                    newy = Math.Exp(context.YIntercept) * Math.Exp(context.Slope * newx);
                    break;
                case 1:
                    newy = Math.Exp(context.YIntercept) * Math.Pow(newx, context.Slope);
                    break;
                case 2:
                    double denom = context.Slope + newx * context.YIntercept;
                    if (denom == 0.0)
                    {
                        denom = 0.0000001;
                    }
                    newy = newx / denom;
                    break;
            }

            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("lab_x", vX.Title);
            outputParameters.AddOutput("res_x", host.RoundU(newx));
            outputParameters.AddOutput("lab_y", vY.Title);
            outputParameters.AddOutput("res_y", host.RoundU(newy));
            return outputParameters;
        }


        public static ParameterBag RptLinearizedEstimatePlot(ITemplateHost host, ParameterBag parameters)
        {
            DataFrame fY = parameters["y"].AsDataFrame;
            DoubleVariable vY = fY.Variables[0]as DoubleVariable;
            DataFrame fX = parameters["x"].AsDataFrame;
            DoubleVariable vX = fX.Variables[0]as DoubleVariable;
            SimpleLinearRegressionContext context = GetSimpleLinearRegressionContext(parameters);
            int model = 0;
            if (parameters.ContainsKey("model"))
            {
                model = Parsing.Cint_Txt(parameters["model"].AsString);
            }
            ChartDefinition cd = new ChartDefinition();
            cd.AddYSeries(vY.Data, vY.Title);
            cd.AddXSeries(vX.Data, vX.Title);
            AgreementOptions aOptions = new AgreementOptions(host.Preferences.ShouldUseColour) { mxd = vY.Data, av = vX.Data };
            cd.ChartOptions = aOptions;
            ParameterBag outputParameters = new ParameterBag();
            using (ChartRenderer ch = (ChartRenderer)ChartRendererFactory.ChartRendererFor(cd))
            {
                string rtf = ch.PlotLinearizedEstimationAndReturnRtf(host, string.Empty, model, context.A, context.G, vX.Title, vY.Title);
                outputParameters.AddOutput("chart", rtf);
            }
            return outputParameters;
        }


        public static ParameterBag RptPolynomialRegression(ITemplateHost host, ParameterBag parameters)
        {
            DataFrame fY = parameters["y"].AsDataFrame;
            DoubleVariable vY = fY.Variables[0]as DoubleVariable;
            // DataFrame fX = parameters[ "x" ].AsDataFrame; unused
            // DoubleVariable vX = fX.Variables[ 0 ]as DoubleVariable; unused
            int P = Parsing.Cint_Txt(parameters["degree"].AsString) + 1;
            MultipleLinearRegressionContext context = new MultipleLinearRegressionContext { N = vY.Length, P = P, DoC = true };
            CalcPoly(parameters, context);
            bool DoC = true;
            x_glin(context, context.Y, context.S, context.X, out context.SE, out context.B, out context.VIF, ref context.SSREG, ref context.SSY, out context.H, out context.R, out context.FV, ref DoC, ref context.N, ref context.P, ref context.M);
            return x_showmr(host, context, context.SE, context.B, true, context.N, context.P, true, context.M);
        }


        ///  <summary>
        ///  Fill in py, weight, px and titles given X and Y data, P and N.
        ///  </summary>
        /// <param name="parameters"></param>
        ///  <param name="context"></param>
        ///  <remarks></remarks>
        private static void CalcPoly(ParameterBag parameters, MultipleLinearRegressionContext context)
        {
            DataFrame fY = parameters["y"].AsDataFrame;
            DoubleVariable vY = fY.Variables[0]as DoubleVariable;
            DataFrame fX = parameters["x"].AsDataFrame;
            DoubleVariable vX = fX.Variables[0]as DoubleVariable;
            //  Sort X and Y in increasing order of X
            Array.Sort(vX.Data, vY.Data);
            int deg = context.P - 1;
            context.Y = new double[context.N + 1 ];
            context.S = new double[context.N + 1 ];
            context.X = new double[context.N + 1, context.P + 1];
            context.Titles = new string[context.N + 1 ];
            context.Titles[0] = vY.Title;
            context.Titles[1] = vX.Title;
            for (int j = 1; j <= context.N; j++)
            {
                context.X[j, 1] = 1.0;
            }
            for (int j = 1; j <= deg; j++)
            {
                for (int i = 1; i <= context.N; i++)
                {
                    context.X[i, j + 1] = Math.Pow(vX.Data[i - 1], Convert.ToDouble(j));
                }
            }
            if (context.P > deg)
            {
                context.Titles[2] = context.Titles[1];
                context.Titles[1] = context.Titles[0];
                for (int j = 3; j <= context.P; j++)
                {
                    context.Titles[j] = context.Titles[2] + "^" + (j - 1).ToString();
                }
            }
            else
            {
                for (int j = 2; j <= deg; j++)
                {
                    context.Titles[j] = context.Titles[1] + "^" + j.ToString();
                }
            }
            for (int j = 1; j <= context.N; j++)
            {
                context.Y[j] = vY.Data[j - 1];
                context.S[j] = 1.0;
            }
        }


        public static ParameterBag RptPolynomialRegressionInterpolation(ITemplateHost host, ParameterBag parameters)
        {
            MultipleLinearRegressionContext context = ((MultipleLinearRegressionContext)(parameters["context"].Data));
            double[,] xtxi = context.H;
            double[] bd = context.B;
            double rss = context.SSY - context.SSREG;
            int nx = context.N;
            int P = context.P;
            double[] newx = new double[P + 1 ];
            newx[1] = 1.0;
            double nwx = parameters["newx"].AsDouble;
            if (P > 1)
            {
                for (int N = 2; N <= P; N++)
                {
                    newx[N] = Math.Pow(nwx, Convert.ToDouble(N - 1));
                }
            }
            double newy = 0;
            for (int N = 1; N <= P; N++)
            {
                newy = newy + (newx[N] * bd[N]);
            }
            double GAMMA = parameters["gamma"].AsDouble;
            double P0; double cit;
            MathDbl.civ(nx - P, out cit, GAMMA, out P0);
            ParameterBag outputParameters = new ParameterBag();
            IList<ParameterBag> tableList = new List<ParameterBag>();
            outputParameters.AddOutput("*table", tableList);
            for (int N = 1; N <= P; N++)
            {
                string Q = N == 1 ? "Intercept" : context.Titles[N - 1];
                ParameterBag tableParameters = new ParameterBag();
                tableList.Add(tableParameters);
                tableParameters.AddOutput("lab", Q);
                tableParameters.AddOutput("res", host.RoundU(newx[N]));
            }
            outputParameters.AddOutput("y_lab", context.Titles[0]);
            outputParameters.AddOutput("y_res", host.RoundU(newy));
            double rdf = Convert.ToDouble((nx - 1) - (P - 1));
            double rms = rss / rdf;
            double cl; double pl;
            Regress1.x_ciyp(newx, xtxi, P, rms, cit, out cl, out pl);
            outputParameters.AddOutput("pc", Formatting.XRound(100 * (1.0 - P0), 1));
            outputParameters.AddOutput("from_conf", host.RoundU(newy - cl));
            outputParameters.AddOutput("to_conf", host.RoundU(newy + cl));
            outputParameters.AddOutput("from_pred", host.RoundU(newy - pl));
            outputParameters.AddOutput("to_pred", host.RoundU(newy + pl));
            return outputParameters;
        }


        public static ParameterBag RptPolynomialRegressionPlot(ITemplateHost host, ParameterBag parameters)
        {
            MultipleLinearRegressionContext context = ((MultipleLinearRegressionContext)(parameters["context"].Data));
            ParameterBag outputParameters = new ParameterBag();
            IList<ParameterBag> chartList = new List<ParameterBag>();
            outputParameters.AddOutput("*chart", chartList);
            for (int mode = 0; mode <= 2; mode++)
            {
                ParameterBag chartParameters = new ParameterBag();
                chartList.Add(chartParameters);
                chartParameters.AddOutput("chart", PlotPoly(host, parameters, mode, context.H, context.B, context.SSREG - context.SSY, context.N, context.P));
            }
            return outputParameters;
        }


        private static string PlotPoly(ITemplateHost host, ParameterBag parameters, int mode, double[,] xtxi, double[] bd, double rss, int nx, int P)
        {
            DataFrame fY = parameters["y"].AsDataFrame;
            DoubleVariable vY = fY.Variables[0]as DoubleVariable;
            DataFrame fX = parameters["x"].AsDataFrame;
            DoubleVariable vX = fX.Variables[0]as DoubleVariable;
            double GAMMA = parameters["gamma"].AsDouble;
            double P0; double cit;
            MathDbl.civ(nx - P, out cit, GAMMA, out P0);
            string title = string.Empty;
            // Select the title
            switch (mode)
            {
                case 0:
                    title = string.Empty;
                    break;
                case 1:
                    title = (Formatting.XRound((1.0 - P0) * 100, 1) + "% CI for the regression estimate");
                    break;
                case 2:
                    title = (Formatting.XRound((1.0 - P0) * 100, 1) + "% Prediction Interval");
                    break;
            }

            AgreementOptions aOptions = new AgreementOptions(host.Preferences.ShouldUseColour) { mxd = vY.Data, av = vX.Data };
            ChartDefinition cd = new ChartDefinition {ChartOptions = aOptions};
            cd.AddYSeries(vY.Data, vY.Title);
            cd.AddXSeries(vX.Data, vX.Title);
            using (ChartRenderer ch = (ChartRenderer)ChartRendererFactory.ChartRendererFor(cd))
            {
                return ch.PlotPolynomialRegressionAndReturnRtf(host, title, mode, xtxi, bd, rss, nx, P, GAMMA, vX.Title, vY.Title);
            }
        }


        public static ParameterBag RptAreaUnderCurve(ITemplateHost host, ParameterBag parameters)
        {
            MultipleLinearRegressionContext context = ((MultipleLinearRegressionContext)(parameters["context"].Data));
            double[] bd = context.B;
            int nx = context.N;
            int P = context.P;
            DataFrame fY = parameters["y"].AsDataFrame;
            DoubleVariable vY = fY.Variables[0]as DoubleVariable;
            DataFrame fX = parameters["x"].AsDataFrame;
            DoubleVariable vX = fX.Variables[0]as DoubleVariable;

            ParameterBag outputParameters = new ParameterBag();
            double auc = 0;
            outputParameters.AddOutput("poly_auc",
                                       x_qromb(vX.Data[0], vX.Data[vX.Length - 1], ref auc, bd, P)
                                           ? host.RoundU(auc)
                                           : Formatting.ERRR);
            double aucg = x_giabaldi(nx, vY.Data, vX.Data);
            outputParameters.AddOutput("trap_auc", host.RoundU(aucg));
            return outputParameters;
        }


        private static bool x_qromb(double a, double b, ref double ss, double[] bd, int P)
        {
            bool x_qrombReturn = false;
            double ds = 0;
            const double eps = 100.0 * Constant.EPSNEG;
            const int jmax = 16;
            const int jmaxp = jmax + 1;
            const int k = 5;
            const int km = k - 1;
            double[] h = new double[jmaxp + 1 ];
            double[] s = new double[jmaxp + 1 ];
            h[1] = 1.0;
            int j;
            for (j = 1; j <= jmax; j++)
            {
                Regress1.trapzd(a, b, ref s[j], j, bd, P);
                if (j >= k)
                {
                    int ifault = 0;
                    Regress1.polint(h, s, j - km, k, 0.0, out ss, ref ds, ref ifault);
                    if (ifault != 0)
                    {
                        return false;
                    }
                    if (Math.Abs(ds) <= eps * Math.Abs(ss))
                    {
                        return true;
                    }
                }
                s[j + 1] = s[j];
                h[j + 1] = 0.25 * h[j];
            }
            if (j < jmax)
            {
                x_qrombReturn = true;
            }
            return x_qrombReturn;
        }


        private static double x_giabaldi(int nx, double[] y, double[] x)
        {
            double ss = 0.0;
            for (int i = 1; i <= nx - 1; i++)
            {
                ss += (((y[i] + y[i - 1]) / 2.0) * Math.Abs(x[i] - x[i - 1]));
            }
            return ss;
        }


        public static ParameterBag RptPolynomialRegressionConfidence(ITemplateHost host, ParameterBag parameters)
        {
            MultipleLinearRegressionContext context = ((MultipleLinearRegressionContext)(parameters["context"].Data));
            int nx = context.N;
            int P = context.P;
            double[,] xtxi = context.H;
            double[] yfit = context.FV;
            double[,] x = context.X;
            double rss = context.SSY - context.SSREG;
            double rdf = Convert.ToDouble((nx - 1) - (P - 1));
            double rms = rss / rdf;
            double GAMMA = parameters["gamma"].AsDouble;
            double cit; double P0;
            MathDbl.civ(nx - P, out cit, GAMMA, out P0);
            DoubleVariable vYFit = new DoubleVariable(nx, "Fitted Y");
            DoubleVariable vsey = new DoubleVariable(nx, "SE of Y fit");
            DoubleVariable vcl = new DoubleVariable(nx, Formatting.XRound(100.0 * (1.0 - P0), 1) + "% Conf Limit");
            DoubleVariable vpl = new DoubleVariable(nx, Formatting.XRound(100.0 * (1.0 - P0), 1) + "% Pred Limit");
            for (int k = 1; k <= nx; k++)
            {
                double xcx = 0;
                double s;
                for (int i = 1; i <= P; i++)
                {
                    s = 0;
                    for (int j = 1; j <= P; j++)
                    {
                        s += xtxi[i, j] * x[k, j];
                    }
                    xcx += s * x[k, i];
                }
                double sey = Math.Sqrt(rms * xcx);
                double cl = cit * sey;
                s = Math.Sqrt(rms * (1.0 + xcx));
                double pl = cit * s;
                vYFit.SetData(k - 1, yfit[k]);
                vsey.SetData(k - 1, sey);
                vcl.SetData(k - 1, cl);
                vpl.SetData(k - 1, pl);
            }
            DataFrame outputFrame = new DataFrame();
            outputFrame.Variables.Add(vYFit);
            outputFrame.Variables.Add(vsey);
            outputFrame.Variables.Add(vcl);
            outputFrame.Variables.Add(vpl);
            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("results", outputFrame);
            return outputParameters;
        }


        public static ParameterBag RptPolynomialRegressionBackInterpolation(ITemplateHost host, ParameterBag parameters)
        {
            MultipleLinearRegressionContext context = ((MultipleLinearRegressionContext)(parameters["context"].Data));
            double[] bd = context.B;
            int nx = context.N;
            int P = context.P;
            double[] yfit = context.FV;
            // DataFrame fY = parameters[ "y" ].AsDataFrame; unused
            // DoubleVariable vY = fY.Variables[ 0 ]as DoubleVariable; unused
            DataFrame fX = parameters["x"].AsDataFrame;
            DoubleVariable vX = fX.Variables[0]as DoubleVariable;
            long cnt = 0; bool fault = false;
            double lastdif = 0;
            double y = parameters["newy"].AsDouble;
            double YMax = yfit[1];
            double YMin = yfit[1];
            double XMax = vX.Data[0];
            double xmin = XMax;
            for (int j = 1; j <= nx; j++)
            {
                if (yfit[j] > YMax)
                {
                    YMax = yfit[j];
                }
                if (yfit[j] < YMin)
                {
                    YMin = yfit[j];
                }
                double x = vX.Data[j - 1];
                if (x > XMax)
                {
                    XMax = x;
                }
                if (x < xmin)
                {
                    XMax = x;
                }
            }
            if (y > YMax | y < YMin)
            {
                host.Error("Y must lie within the fitted curve (" + host.RoundU(YMin) + " to " + host.RoundU(YMax) + ")", "Polynomial Interpolation");
                throw new TemplateOperationCancelledException();
            }
            double INC = (XMax - xmin) / 10.0;
            double yinc = (YMax - YMin) / 10.0;
            double gotx = xmin - INC;
            do
            {
                do
                {
                    cnt = cnt + 1;
                    if (cnt > 10000)
                    {
                        fault = true;
                        break;
                    }
                    gotx = gotx + INC;
                    double goty = Regress1.polyfunc(gotx, bd, P);
                    double dif = Math.Abs(goty - y);
                    if (dif == lastdif)
                    {
                        break;
                    }
                    lastdif = dif;
                    if (dif < Math.Abs(yinc * 10))
                    {
                        yinc = yinc / 10.0;
                        gotx = gotx - INC;
                        break;
                    }
                }
                while (true);
                INC = INC / 10.0;
                if (INC < Constant.EPSNEG * 10.0)
                {
                    break;
                }
                if (fault)
                {
                    break;
                }
            }
            while (true);
            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("y", host.RoundU(Regress1.polyfunc(gotx, bd, P)));
            outputParameters.AddOutput("x", host.RoundU(gotx));
            if (fault)
            {
                host.Error("Error in calculation", "Polynomial Interpolation");
            }
            return outputParameters;
        }


        public static ParameterBag RptLogisticRegression(ITemplateHost host, ParameterBag parameters)
        {
            int rows;
            int tot_obs;
            double[] tt; double[] tr; double[] tw;

            double accuracy = Parsing.Cdbl_Txt(parameters["accuracy"].AsString);
            if (accuracy > 0.01)
                accuracy = 0.01;
            bool grouped = "grouped".Equals(parameters["grouping"].AsString);
            bool shouldCalculateIntercept = parameters["intercept"].AsBoolean;
            bool hasWeights = parameters["weights"].AsBoolean;

            DoubleVariable responseVariable;
            if (grouped)
            {
                DataFrame totalFrame = parameters["total"].AsDataFrame;
                DoubleVariable totalVariable = totalFrame.Variables[0]as DoubleVariable;
                // Store the total Data
                rows = totalVariable.Length;
                tt = new double[rows + 1];
                tr = new double[rows + 1];
                tw = new double[rows + 1];
                tot_obs = 0;
                for (int c = 1; c <= rows; c++)
                {
                    tt[c] = totalVariable.Data[c - 1];
                    if (tt[c] != Constant.MISSING) tot_obs = tot_obs + Convert.ToInt32(tt[c]);
                }
                DataFrame responseFrame = parameters["response"].AsDataFrame;
                responseVariable = responseFrame.Variables[0]as DoubleVariable;
                // Store the response data
                for (int c = 1; c <= rows; c++)
                    tr[c] = responseVariable.Data[c - 1];
            }
            else
            {
                DataFrame responseFrame = parameters["response"].AsDataFrame;
                responseVariable = responseFrame.Variables[0]as DoubleVariable;
                rows = responseVariable.Length;
                tt = new double[rows + 1];
                tr = new double[rows + 1];
                tw = new double[rows + 1];
                // Store the response data
                for (int c = 1; c <= rows; c++)
                {
                    tr[c] = responseVariable.Data[c - 1];
                    if (tr[c] > 1.0 && tr[c] != Constant.MISSING)
                    {
                        host.Error("Response data must be either 0 (not responded) or 1 (responded), if you want to use grouped response data then please select this option at the start", "Logistic Regression");
                        throw new TemplateOperationCancelledException();
                    }
                }
                // Total observations = rows
                tot_obs = rows;
                // set denominator/total as 1
                for (int c = 1; c <= rows; c++)
                    tt[c] = 1.0;
            }
            if (hasWeights)
            {
                DataFrame weightsFrame = parameters["weights"].AsDataFrame;
                DoubleVariable weightsVariable = weightsFrame.Variables[0]as DoubleVariable;
                // Store the weight Data
                for (int c = 1; c <= rows; c++)
                    tw[c] = weightsVariable.Data[c - 1];
            }
            else
            {
                for (int c = 1; c <= rows; c++)
                    tw[c] = 1.0;
            }
            DataFrame predictorsFrame = parameters["predictors"].AsDataFrame;
            // Store the predictors
            int prd = predictorsFrame.VariableCount;
            double[,] pt = new double[prd, rows + 1];
            for (int c = 0; c < prd; c++)
            {
                DoubleVariable v = predictorsFrame.Variables[c]as DoubleVariable;
                double[] data = v.Data;
                int r;
                for (r = 1; r <= rows; r++)
                    pt[c, r] = data[r - 1];
            }
            // check predictors for categorical data not yet dummied
            if (prd + 1 >= tot_obs)
            {
                host.Error("You must have more observations than parameters", "Logistic Regression");
                throw new TemplateOperationCancelledException();
            }

            // stack entries with duplicate covariate patterns
            // IEB July 2009: don't stack missing observations in the response as the subsequent dropper won't work
            int cutrows = 0;
            for (int i = 1; i < rows; i++)
            {
                for (int j = i + 1; j <= rows; j++)
                {
                    if (tw[j] == 1.0 && tr[j] != Constant.MISSING)
                    {
                        bool snap = true;
                        for (int k = 0; k < prd; k++)
                        {
                            if (pt[k, j] != pt[k, i])
                            {
                                snap = false;
                                break;
                            }
                        }
                        if (snap)
                        {
                            tt[i] += tt[j];
                            tr[i] += tr[j];
                            tw[j] = Constant.MISSING;
                            cutrows += 1;
                        }
                    }
                }
            }
            //  At this point we have merged rows where there are duplicate covariate patterns, and all other rows have MISSING in the weights.  There are rows - cutrows valid rows in the arrays, but they could be anywhere!
            int newrows = rows - cutrows;
            //  Copy down the remaining observations
            //  0 = tt = total observations
            //  1 = tr = responses
            //  2 = tw = weights
            //  3+ = pt = predictors
            int targetRow = 1;
            for (int sourceRow = 1; sourceRow <= rows; sourceRow++)
            {
                if (tw[sourceRow] != Constant.MISSING)
                {
                    //  This row is valid; if necessary, copy it down to our current target row
                    if (sourceRow != targetRow)
                    {
                        tt[targetRow] = tt[sourceRow];
                        tr[targetRow] = tr[sourceRow];
                        tw[targetRow] = tw[sourceRow];
                        for (int pred = 0; pred < prd; pred++)
                            pt[pred, targetRow] = pt[pred, sourceRow];
                    }
                    targetRow += 1;
                }
            }
            Debug.Assert(targetRow == newrows + 1);
            //  At this point, tt, tr, tw and pt contain valid data from row 1 to row newrows inclusive

            bool use_weights = false; int rank = 0; int df = 0;
            double deviance; double devx; double llx;
            const int maxit = 200;
            int fault;
            int records = newrows;
            int[] rxi = new int[1 + 1];
            int predictors = predictorsFrame.VariableCount;
            int p = predictors;
            bool mean = shouldCalculateIntercept;
            if (mean)
                p++;

            double tol = accuracy;
            string[] labels = new string[p + 1 ];
            labels[0] = responseVariable.Title.Trim();
            for (int j = 1; j <= predictors; j++)
                labels[j] = predictorsFrame.Variables[j - 1].Title.Trim();

            // Copy tt to t, tr yo y, tw to wt, pt to x.  Check for missing data; if any is present for a row, do not copy the row.
            int cnt = 0;
            double[] t = new double[records + 1];
            double[] y = new double[records + 1];
            double[] wt = new double[records + 1];
            double[,] x = new double[records + 1, p + 1];
            for (int j = 1; j <= records; j++)
            {
                bool ok = tt[j] != Constant.MISSING && tr[j] != Constant.MISSING;
                if (use_weights && tw[j] == Constant.MISSING)
                    ok = false;
                for (int k = 0; k < predictors; k++)
                {
                    if (pt[k, j] == Constant.MISSING)
                    {
                        ok = false;
                        break;
                    }
                }
                if (ok)
                {
                    cnt++;
                    t[cnt] = tt[j];
                    y[cnt] = tr[j];
                    if (y[cnt] == 0.0)
                        y[cnt] = Constant.EPSNEG;
                    if (y[cnt] == t[cnt])
                        y[cnt] = y[cnt] - Constant.EPSNEG;
                    if (use_weights)
                        wt[cnt] = tw[j];
                    else
                        wt[cnt] = 1.0;
                    for (int k = 1; k <= predictors; k++)
                        x[cnt, k] = pt[k - 1, j];
                }
            }
            if (records != cnt)
            {
                x_dropper(host, records - cnt, "Logistic regression");
                records = cnt;
            }
            //  get intercept deviance - drop predictors
            double[,] x2 = new double[records + 1, 1 + 1];
            for (int j = 1; j <= records; j++)
            {
                x2[j, 0] = 1;
                x2[j, 1] = 1;
            }
            bool[] select_x = new bool[1 + 1 ];
            select_x[1] = true;
            double[] beta = new double[1 + 1 ];
            double[] se_beta = new double[records + 1 ];
            double[] covariance = new double[1 + 1];
            double[] fit = new double[records + 1 ];
            double[] residual = new double[records + 1];
            double[] leverage = new double[records + 1];
            double[] offset = new double[records + 1 ];
            string dropped = string.Empty;
            string err_msg = string.Empty;
            Regress1.X_Logistic_Regression(false, false, ref use_weights, records, x2, 1, select_x, 1, y, t, wt, out deviance, ref df, beta, ref rank, se_beta, covariance, tol, maxit, fit, residual, leverage, offset, out fault, ref dropped, ref err_msg);
            int idfx = df;
            if (mean == false)
            {
                llx = Constant.MISSING;
                devx = Constant.MISSING;
            }
            else
            {
                llx = x_loglik_l(use_weights, records, wt, y, t, fit);
                devx = deviance;
            }

            //  calculate full model
            select_x = new bool[p + 1];
            for (int j = 1; j <= p; j++)
                select_x[j] = true;

            beta = new double[p + 1];
            se_beta = new double[records + 1];
            covariance = new double[((int)(Math.Floor((double)p * (p + 1) / 2))) + 1];
            fit = new double[records + 1];
            residual = new double[records + 1];
            leverage = new double[records + 1];
            offset = new double[records + 1];
            dropped = string.Empty;
            err_msg = string.Empty;
            Regress1.X_Logistic_Regression(mean, false, ref use_weights, records, x, predictors, select_x, p, y, t, wt, out deviance, ref df, beta, ref rank, se_beta, covariance, tol, maxit, fit, residual, leverage, offset, out fault, ref dropped, ref err_msg);
            if (fault != 0 && fault != 3)
            {
                host.Error(err_msg, "Logistic Regression");
                throw new TemplateOperationCancelledException();
            }
            ParameterBag outputParameters = new ParameterBag();
            string warn;
            if (fault == 3 && err_msg.Length > 0)
                warn = Formatting.WRNCOLON + err_msg;
            else
                warn = string.Empty;
            if (rank != p)
            {
                if (warn.Length > 0)
                    warn += Formatting.RTFCRLF;
                warn += Formatting.WRNCOLON + "result not of full rank, there is more than one solution for the model." + Formatting.RTFCRLF + "Look for correlated predictor variables that you might drop: the mutliple linear regression function does this automatically.";
            }
            if (df <= 0)
            {
                if (warn.Length > 0)
                    warn += Formatting.RTFCRLF;
                warn += Formatting.WRNCOLON + "saturated model (all degrees of freedom used, can't assess goodness of fit)";
            }
            if (dropped.Length > 0)
            {
                if (warn.Length > 0)
                    warn += Formatting.RTFCRLF + dropped;
                else
                    warn = dropped;
            }
            IList<ParameterBag> warnList = new List<ParameterBag>();
            outputParameters.AddOutput("*warn", warnList);
            if (warn.Length > 0)
            {
                ParameterBag warnParameters = new ParameterBag();
                warnList.Add(warnParameters);
                warnParameters.AddOutput("warn", warn);
            }
            outputParameters.AddOutput("dev", host.RoundU(deviance));
            outputParameters.AddOutput("df", df.ToString());
            double prob = PDF.chivalp(deviance, Convert.ToDouble(df));
            outputParameters.AddOutput("p", host.pval(prob));
            warn = prob < 0.05 ? Formatting.ASTERISK : string.Empty;
            outputParameters.AddOutput("w", warn);
            double x2dev = devx - deviance;
            if (x2dev < 0.0)
                x2dev = Constant.MISSING;

            outputParameters.AddOutput("x2", host.RoundU(x2dev));
            outputParameters.AddOutput("df_lr", (idfx - df).ToString());
            outputParameters.AddOutput("p_lr",
                idfx > 0
                    ? host.pval(PDF.chivalp(x2dev, Convert.ToDouble(idfx - df)))
                    : Formatting.ASTERISK);

            //IEB Dec 14 change from reporting coefficients to reporting odds ratios
            double cit;
            double p0;
            double gamma = parameters["gamma"].AsDouble;
            MathDbl.civ(0, out cit, gamma, out p0);
            outputParameters.AddOutput("pc", Formatting.XRound(100 * (1.0 - p0), 2));
            int iq = 0;
            if (mean)
            {
                iq = 1;
            }
            IList<ParameterBag> varList = new List<ParameterBag>();
            outputParameters.AddOutput("*var", varList);
            for (int i = 1; i <= p; i++)
            {
                if (se_beta[i] == 0)
                    prob = Constant.MISSING;
                else
                    prob = 2.0 * (1.0 - PDF.alnorm(Math.Abs(beta[i] / se_beta[i])));
                ParameterBag varParameters = new ParameterBag();
                varList.Add(varParameters);
                string q = i == 1 && mean ? "(intercept)" : labels[i - iq];
                varParameters.AddOutput("par", q);
                if (i > 1 || mean == false)
                {
                    double odr = Formatting.SafeExp(beta[i]);
                    double lci = Formatting.SafeExp(beta[i] - se_beta[i] * cit);
                    double uci = Formatting.SafeExp(beta[i] + se_beta[i] * cit);
                    varParameters.AddOutput("or", host.RoundU(odr));
                    varParameters.AddOutput("ci", "("+ host.RoundU(lci) + "  to  " + host.RoundU(uci) + ")");
                }
                else
                {
                    varParameters.AddOutput("or", "n/a");
                    varParameters.AddOutput("ci", string.Empty);
                }
                double z;
                if (se_beta[i] == 0.0)
                {
                    z = Constant.MISSING;
                    varParameters.AddOutput("z", host.RoundU(z));
                    varParameters.AddOutput("p_var", "* error: drop this variable *");
                }
                else
                {
                    z = beta[i] / se_beta[i];
                    varParameters.AddOutput("z", host.RoundU(z));
                    varParameters.AddOutput("p_var", host.pval(prob));
                }
            }

            /*
            IList<ParameterBag> varList = new List<ParameterBag>();
            outputParameters.AddOutput("*var", varList);
            for (int i = 1; i <= p; i++)
            {
                if (se_beta[i] == 0)
                    prob = Constant.MISSING;
                else
                    prob = 2.0 * (1.0 - PDF.alnorm(Math.Abs(beta[i] / se_beta[i])));
                ParameterBag varParameters = new ParameterBag();
                varList.Add(varParameters);
                if (mean)
                {
                    varParameters.AddOutput("lab", i == 1 ? "Intercept" : label[i - 1]);
                    varParameters.AddOutput("idx", (i - 1).ToString());
                }
                else
                {
                    varParameters.AddOutput("lab", label[i]);
                    varParameters.AddOutput("idx", i.ToString());
                }
                varParameters.AddOutput("res", host.RoundU(beta[i]));
                double z;
                if (se_beta[i] == 0.0)
                {
                    z = Constant.MISSING;
                    varParameters.AddOutput("z", host.RoundU(z));
                    varParameters.AddOutput("p_var", "* error: drop this variable *");
                }
                else
                {
                    z = beta[i] / se_beta[i];
                    varParameters.AddOutput("z", host.RoundU(z));
                    varParameters.AddOutput("p_var", host.pval(prob));
                }
            }
            */

            string tx = "logit ";
            if (labels[0].Length > 0)
                tx += labels[0];
            else
                tx += "Y";
            tx += " = ";
            for (int j = 1; j <= p; j++)
            {
                if (j > 1 && beta[j] >= 0.0)
                    tx += "+";
                tx += host.RoundU(beta[j]);
                string Q = mean ? (j > 1 ? labels[j - 1] : " ") : labels[j];
                if (Q.Length == 0)
                    tx += " X" + j.ToString();
                else 
                    tx += " " + Q + " ";
            }
            outputParameters.AddOutput("logit", tx);
            MultipleLinearRegressionContext context = new MultipleLinearRegressionContext();
            outputParameters.Add("context", new FilledParameter(FilledParameterDirection.Input, context));
            context.SE = se_beta;
            context.B = beta;
            context.T = t;
            context.X = x;
            context.Y = y;
            context.FV = fit;
            context.R = residual;
            context.H1 = leverage;
            context.N = records;
            context.DoC = mean;
            context.Labels = labels;
            context.P = p;
            context.Covariance = covariance;
            context.WT = wt;
            context.RXI = rxi;
            context.WEIGHT = use_weights;
            context.RANK = rank;
            context.DF = df;
            context.TOL = tol;
            context.M = predictors;
            context.DEV = deviance;
            context.DEVX = devx;
            context.LLX = llx;
            context.DFX = idfx;
            outputParameters["candidatePredictors"] = new FilledParameter(FilledParameterDirection.Input, x_prep_interlr(context));
            return outputParameters;
        }

        private static double x_loglik_p(bool weight, int N, double[] wt, double[] y, double[] fvl)
        {
            double ll = 0;
            int i;

            for (i = 1; i <= N; i++)
            {
                double ww = weight ? wt[i] : 1.0;
                ll += ww * y[i] * Math.Log(fvl[i]) - ww * fvl[i] - ww * PDF.alogam(y[i] + 1.0);
            }
            return ll;
        }

        private static double x_loglik_l(bool weight, int N, double[] wt, double[] y, double[] t, double[] fvl)
        {
            double ll = 0;
            for (int i = 1; i <= N; i++)
            {
                double PP = fvl[i] / t[i];
                double ww = weight ? wt[i] : 1.0;
                if (PP != 0.0)
                {
                    ll = ww * ll + y[i] * Math.Log(PP) + ww * (t[i] - y[i]) * Math.Log(1.0 - PP);
                }
            }
            return ll;
        }

        private static void x_dropper(ITemplateHost host, long Q, string caption)
        {
            host.Warning(Q.ToString() + " observations dropped due to missing data." + "\r\n" + "Make sure that observations with missing data are not a subgroup.", caption);
        }

        public static ParameterBag RptLogisticRegressionFit(ITemplateHost host, ParameterBag parameters)
        {
            MultipleLinearRegressionContext context = ((MultipleLinearRegressionContext)(parameters["context"].Data));

            double[] t = context.T;
            double[] y = context.Y;
            double[] fvl = context.FV;
            double[] dr = context.R;
            double[] hi = context.H1;
            // double[] var = context.V1; 
            int nx = context.N;
            bool DoC = context.DoC;
            string[] labels = context.Labels;
            int P = context.P;
            double[] covariance = context.Covariance;
            double[] wt = context.WT;
            bool weight = context.WEIGHT;
            double[,] x = context.X;

            double[] ry = new double[nx + 1];
            double[] fit = new double[nx + 1];
            double[] PP = new double[nx + 1];
            double[] ww = new double[nx + 1];
            double[] pxi = new double[nx + 1];
            double[] xis = new double[nx + 1];
            double[] cbar = new double[nx + 1];
            double[] c = new double[nx + 1];
            double[] d = new double[nx + 1];
            double[] dc = new double[nx + 1];
            for (int i = 1; i <= nx; i++)
            {
                // Deviance
                ry[i] = y[i] <= Constant.EPSNEG ? 0.0 : y[i];
                fit[i] = t[i] != 0.0 ? fvl[i] / t[i] : Constant.MISSING;

                // Pearson
                PP[i] = fvl[i] / t[i];
                ww[i] = weight ? wt[i] : 1.0;
                pxi[i] = (PP[i] != 0.0) ? ((y[i] - t[i] * PP[i]) * Math.Sqrt(ww[i])) / Math.Sqrt(t[i] * PP[i] * (1.0 - PP[i])) : Constant.MISSING;
                xis[i] = (1.0 - hi[i] > 0.0 && pxi[i] != Constant.MISSING) ? pxi[i] / Math.Sqrt(1.0 - hi[i]) : Constant.MISSING;

                // Delta beta et al.  IEB July 2009
                if (PP[i] * (1.0 - PP[i]) != 0.0)
                {
                    double xi = ((y[i] - t[i] * PP[i]) * Math.Sqrt(ww[i])) / Math.Sqrt(t[i] * PP[i] * (1.0 - PP[i]));
                    c[i] = (Math.Pow(xi, 2.0) * hi[i]) / Math.Pow((1.0 - hi[i]), 2.0);
                    cbar[i] = (Math.Pow(xi, 2.0) * hi[i]) / (1.0 - hi[i]);
                    d[i] = Math.Pow(dr[i], 2.0) + cbar[i];
                    dc[i] = cbar[i] / hi[i];
                }
                else
                {
                    c[i] = Constant.MISSING;
                    cbar[i] = Constant.MISSING;
                    d[i] = Constant.MISSING;
                    dc[i] = Constant.MISSING;
                }
            }

            ParameterBag outputParameters = new ParameterBag();

            // Output is individual if the user selected individual rows in the original regression *and* chose to preserve them in this call
            bool isGrouped = !("individual".Equals(parameters["grouping"].AsString) && "individual".Equals(parameters["row_type"].AsString));

            // Make up all variables for a possible future output of a frame containing all or part of these.
            // DO NOT change this order without looking at MakeLogisticRegressionFitRow and GridLogisticRegressionFit, which assume these indices.
            int variableLength = isGrouped ? nx : parameters["predictors"].AsDataFrame.MaxRows;
            DataFrame outputFrame = new DataFrame();
            outputFrame.Variables.Add(new DoubleVariable(variableLength, "Trials"));
            outputFrame.Variables.Add(new DoubleVariable(variableLength, "Events"));
            outputFrame.Variables.Add(new DoubleVariable(variableLength, "Event Probability"));
            outputFrame.Variables.Add(new DoubleVariable(variableLength, "Deviance Residual"));
            outputFrame.Variables.Add(new DoubleVariable(variableLength, "Pearson Residual"));
            outputFrame.Variables.Add(new DoubleVariable(variableLength, "Leverage"));
            outputFrame.Variables.Add(new DoubleVariable(variableLength, "Std Pearson Residual"));
            outputFrame.Variables.Add(new DoubleVariable(variableLength, "Delta Beta"));
            outputFrame.Variables.Add(new DoubleVariable(variableLength, "Std Delta Beta"));
            outputFrame.Variables.Add(new DoubleVariable(variableLength, "Delta Deviance"));
            outputFrame.Variables.Add(new DoubleVariable(variableLength, "Delta Chi-Square"));
            outputParameters.AddOutput("fitsDump", outputFrame);

            if (isGrouped)
            {
                List<ParameterBag> groupedList = new List<ParameterBag>();
                outputParameters.AddOutput("*grouped", groupedList);
                ParameterBag groupedParameters = new ParameterBag();
                groupedList.Add(groupedParameters);

                List<ParameterBag> predictorsList = new List<ParameterBag>();
                groupedParameters.AddOutput("*predictors", predictorsList);
                for (int i = 1; i <= labels.Length - 2; i++)
                {
                    ParameterBag predictorsParameters = new ParameterBag();
                    predictorsList.Add(predictorsParameters);
                    predictorsParameters.AddOutput("lab", labels[i]);
                }
                List<ParameterBag> predictorValuesList = new List<ParameterBag>();
                groupedParameters.AddOutput("*predictorValues", predictorValuesList);
                for (int i = 1; i <= nx; i++)
                {
                    ParameterBag bag = MakeLogisticRegressionFitRow(host, t, y, dr, hi, labels, x, ry, fit, pxi, xis, cbar, c, d, dc, i, true, outputFrame, i - 1);
                    predictorValuesList.Add(bag);
                }
            }
            else
            {
                List<ParameterBag> individualList = new List<ParameterBag>();
                outputParameters.AddOutput("*individual", individualList);
                ParameterBag individualParameters = new ParameterBag();
                individualList.Add(individualParameters);

                List<ParameterBag> predictorValuesList = new List<ParameterBag>();
                individualParameters.AddOutput("*predictorValues", predictorValuesList);

                double[] responses = (parameters["response"].AsDataFrame.Variables[0] as DoubleVariable).Data;
                DataFrame predictorsFrame = parameters["predictors"].AsDataFrame;
                // Process each (known individual) predictor
                int vars = predictorsFrame.VariableCount;
                int rows = predictorsFrame.MaxRows;
                for (int predictorRow = 0; predictorRow < rows; predictorRow++)
                {
                    // Gather the predictor values for this row
                    double[] thisRow = new double[vars];
                    bool atLeastOneMissing = false;
                    for (int v = 0; v < vars; v++)
                    {
                        double value = (predictorsFrame.Variables[v] as DoubleVariable).Data[predictorRow];
                        if (value == Constant.MISSING)
                        {
                            atLeastOneMissing = true;
                            break;
                        }
                        thisRow[v] = value;
                    }
                    // If we have missing data, this row will never have a group so there's no point looking
                    ParameterBag bag = null;
                    if (atLeastOneMissing)
                    {
                        bag = MakeLogisticRegressionFitRow(host, null, null, null, null, null, null, null, null, null, null, null, null, null, null, 0, false, outputFrame, predictorRow);
                    }
                    else
                    {
                        // Find that predictor pattern in our grouped data; once found, emit the matching values
                        bool snapped = false;
                        for (int i = 1; i <= nx; i++)
                        {
                            for (int xindex = 0; xindex <= nx; xindex++)
                            {
                                bool snap = true;
                                for (int predIndex = 0; predIndex < vars; predIndex++)
                                {
                                    if (x[i, predIndex + 1] != thisRow[predIndex])
                                    {
                                        snap = false;
                                        break;
                                    }
                                }
                                if (snap)
                                {
                                    bag = MakeLogisticRegressionFitRow(host, t, y, dr, hi, labels, x, ry, fit, pxi, xis, cbar, c, d, dc, i, false, outputFrame, predictorRow);
                                    snapped = true;
                                    break;
                                }
                            }
                        }
                        if (!snapped)
                            throw new Exception("Should never happen: Couldn't find an individual predictor pattern in the processed groups.");
                    }
                    bag.AddOutput("resbool", host.RoundU(responses[predictorRow]));
                    bag.AddOutput("resnum", (predictorRow + 1).ToString());
                    predictorValuesList.Add(bag);
                }
            }

            // Covariance
            IList<ParameterBag> covarList = new List<ParameterBag>();
            outputParameters.AddOutput("*covar", covarList);
            for (int i = 1; i <= P; i++)
            {
                for (int j = i; j <= P; j++)
                {
                    ParameterBag covarParameters = new ParameterBag();
                    covarList.Add(covarParameters);
                    string x1 = qlbli(labels, i, DoC);
                    string x2 = qlbli(labels, j, DoC);
                    covarParameters.AddOutput("lab", x1 + " vs. " + x2);
                    covarParameters.AddOutput("cov", host.RoundU(covariance[((int)(Math.Floor((double)j * (j - 1) / 2 + i)))]));
                }
            }
            return outputParameters;
        }

        private static ParameterBag MakeLogisticRegressionFitRow(ITemplateHost host, double[] t, double[] y, double[] dr, double[] hi, string[] label, double[,] x, double[] ry, double[] fit, double[] pxi, double[] xis, double[] cbar, double[] c, double[] d, double[] dc, int arrayOffset, bool includePredictors, DataFrame outputFrame, int outputRow)
        {
            ParameterBag predictorValuesParameters = new ParameterBag();
            predictorValuesParameters.AddOutput("idx", arrayOffset);
            predictorValuesParameters.AddOutput("sub", null == t ? Formatting.ASTERISK : host.RoundU(t[arrayOffset]));
            predictorValuesParameters.AddOutput("res", null == ry ? Formatting.ASTERISK : host.RoundU(ry[arrayOffset]));
            predictorValuesParameters.AddOutput("fit", null == fit ? Formatting.ASTERISK : host.RoundU(fit[arrayOffset]));
            predictorValuesParameters.AddOutput("dev", null == dr ? Formatting.ASTERISK : host.RoundU(dr[arrayOffset]));

            predictorValuesParameters.AddOutput("pres", null == pxi ? Formatting.ASTERISK : host.RoundU(pxi[arrayOffset]));
            predictorValuesParameters.AddOutput("lev", null == hi ? Formatting.ASTERISK : host.RoundU(hi[arrayOffset]));
            predictorValuesParameters.AddOutput("spres", null == xis ? Formatting.ASTERISK : host.RoundU(xis[arrayOffset]));

            predictorValuesParameters.AddOutput("deltac", null == cbar ? Formatting.ASTERISK : host.RoundU(cbar[arrayOffset]));
            predictorValuesParameters.AddOutput("deltabar", null == c ? Formatting.ASTERISK : host.RoundU(c[arrayOffset]));
            predictorValuesParameters.AddOutput("deltadev", null == d ? Formatting.ASTERISK : host.RoundU(d[arrayOffset]));
            predictorValuesParameters.AddOutput("deltachi", null == dc ? Formatting.ASTERISK : host.RoundU(dc[arrayOffset]));

            (outputFrame.Variables[0] as DoubleVariable).Data[outputRow] = null == t ? Constant.MISSING : t[arrayOffset]; // Trials
            (outputFrame.Variables[1] as DoubleVariable).Data[outputRow] = null == y ? Constant.MISSING : y[arrayOffset]; // Events
            (outputFrame.Variables[2] as DoubleVariable).Data[outputRow] = null == fit ? Constant.MISSING : fit[arrayOffset]; // Event Probability
            (outputFrame.Variables[3] as DoubleVariable).Data[outputRow] = null == dr ? Constant.MISSING : dr[arrayOffset]; // Deviance Residual
            (outputFrame.Variables[4] as DoubleVariable).Data[outputRow] = null == pxi ? Constant.MISSING : pxi[arrayOffset]; // Pearson Residual
            (outputFrame.Variables[5] as DoubleVariable).Data[outputRow] = null == hi ? Constant.MISSING : hi[arrayOffset]; // Leverage
            (outputFrame.Variables[6] as DoubleVariable).Data[outputRow] = null == xis ? Constant.MISSING : xis[arrayOffset]; // Std Pearson Residual
            (outputFrame.Variables[7] as DoubleVariable).Data[outputRow] = null == cbar ? Constant.MISSING : cbar[arrayOffset]; // Delta Beta
            (outputFrame.Variables[8] as DoubleVariable).Data[outputRow] = null == c ? Constant.MISSING : c[arrayOffset]; // Std Delta Beta
            (outputFrame.Variables[9] as DoubleVariable).Data[outputRow] = null == d ? Constant.MISSING : d[arrayOffset]; // Delta Deviance
            (outputFrame.Variables[10] as DoubleVariable).Data[outputRow] = null == dc ? Constant.MISSING : dc[arrayOffset]; // Delta Chi-Square

            if (includePredictors)
            {
                List<ParameterBag> predictorsList2 = new List<ParameterBag>();
                predictorValuesParameters.AddOutput("*pred", predictorsList2);
                for (int j = 1; j < label.Length - 1; j++)
                {
                    ParameterBag predictorsParameters = new ParameterBag();
                    predictorsList2.Add(predictorsParameters);
                    predictorsParameters.AddOutput("val", x[arrayOffset, j].ToString());
                }
            }

            return predictorValuesParameters;
        }

        public static ParameterBag GridLogisticRegressionFit(ITemplateHost host, ParameterBag parameters)
        {
            bool doTrials = parameters["trials"].AsBoolean;
            bool doEvents = parameters["events"].AsBoolean;
            bool doEventProbability = parameters["eventProbability"].AsBoolean;
            bool doDevianceResidual = parameters["devianceResidual"].AsBoolean;
            bool doPearsonResidual = parameters["pearsonResidual"].AsBoolean;
            bool doLeverage = parameters["leverage"].AsBoolean;
            bool doStdPearsonResidual = parameters["stdPearsonResidual"].AsBoolean;
            bool doDeltaBeta = parameters["deltaBeta"].AsBoolean;
            bool doStdDeltaBeta = parameters["stdDeltaBeta"].AsBoolean;
            bool doDeltaDeviance = parameters["deltaDeviance"].AsBoolean;
            bool doDeltaChiSquare = parameters["deltaChiSquare"].AsBoolean;
            DataFrame fitsDump = parameters["fitsDump"].AsDataFrame;
            ParameterBag outputParameters = new ParameterBag();
            if (doTrials || doEvents || doEventProbability || doDevianceResidual || doPearsonResidual || doLeverage || doStdPearsonResidual || doDeltaBeta || doStdDeltaBeta || doDeltaDeviance || doDeltaChiSquare)
            {
                // Retrieve all the variables (it's fast!) then only include the ones we need
                DoubleVariable trialsVariable = fitsDump.Variables[0]as DoubleVariable;
                DoubleVariable eventsVariable = fitsDump.Variables[1]as DoubleVariable;
                DoubleVariable eventProbabilityVariable = fitsDump.Variables[2]as DoubleVariable;
                DoubleVariable devianceResidualVariable = fitsDump.Variables[3]as DoubleVariable;
                DoubleVariable pearsonResidualVariable = fitsDump.Variables[4]as DoubleVariable;
                DoubleVariable leverageVariable = fitsDump.Variables[5]as DoubleVariable;
                DoubleVariable stdPearsonResidualVariable = fitsDump.Variables[6]as DoubleVariable;
                DoubleVariable deltaBetaVariable = fitsDump.Variables[7]as DoubleVariable;
                DoubleVariable stdDeltaBetaVariable = fitsDump.Variables[8]as DoubleVariable;
                DoubleVariable deltaDevianceVariable = fitsDump.Variables[9]as DoubleVariable;
                DoubleVariable deltaChiSquareVariable = fitsDump.Variables[10]as DoubleVariable;

                DataFrame resultsFrame = new DataFrame();
                if (doTrials)
                    resultsFrame.Variables.Add(trialsVariable);
                if (doEvents)
                    resultsFrame.Variables.Add(eventsVariable);
                if (doEventProbability)
                    resultsFrame.Variables.Add(eventProbabilityVariable);
                if (doDevianceResidual)
                    resultsFrame.Variables.Add(devianceResidualVariable);
                if (doPearsonResidual)
                    resultsFrame.Variables.Add(pearsonResidualVariable);
                if (doLeverage)
                    resultsFrame.Variables.Add(leverageVariable);
                if (doStdPearsonResidual)
                    resultsFrame.Variables.Add(stdPearsonResidualVariable);
                if (doDeltaBeta)
                    resultsFrame.Variables.Add(deltaBetaVariable);
                if (doStdDeltaBeta)
                    resultsFrame.Variables.Add(stdDeltaBetaVariable);
                if (doDeltaDeviance)
                    resultsFrame.Variables.Add(deltaDevianceVariable);
                if (doDeltaChiSquare)
                    resultsFrame.Variables.Add(deltaChiSquareVariable);
                outputParameters.AddOutput("results", resultsFrame);
            }
            return outputParameters;
        }

        private static string qlbli(string[] label, int i, bool DoC)
        {
            string x;

            if (DoC)
                x = i == 1 ? "Intercept" : label[i - 1];
            else
                x = label[i];
            if (x.Length == 0)
            {
                x = "b" + i.ToString();
            }
            return x;
        }


        public static ParameterBag PlotLogisticRegressionDiagnostics(ITemplateHost host, ParameterBag parameters)
        {
            MultipleLinearRegressionContext context = ((MultipleLinearRegressionContext)(parameters["context"].Data));
            double[] t = context.T;
            double[] y = context.Y;
            double[] fv = context.FV;
            double[] dr = context.R;
            double[] hi = context.H1;
            int nx = context.N;
            double[] wt = context.WT;
            bool weight = context.WEIGHT;

            if (context.DF <= 0)
            {
                host.Error("Plots are not relevant for a saturated model.", "Logistic Regression");
                throw new TemplateOperationCancelledException();
            }

            double[] yy1 = new double[nx + 1 ];
            double[] yy2 = new double[nx + 1 ];
            double[] yy3 = new double[nx + 1];
            double[] yy4 = new double[nx + 1];
            double[] xx = new double[nx + 1];
            // diagnostic plots
            const string ep = "Event Probability (pi)";
            const string lv = "Leverage (Hi)";
            const string db = "Delta Beta";
            const string dbs = "Delta Beta Std";
            const string dd = "Delta Deviance";
            const string dx = "Delta Chi-square";
            for (int i = 1; i <= nx; i++)
            {
                double PP = fv[i] / t[i];
                double ww = weight ? wt[i] : 1.0;
                // IEB July 2009
                double c;
                double cbar;
                double d;
                double dc;
                if (PP * (1.0 - PP) != 0.0)
                {
                    double xi = ((y[i] - t[i] * PP) * Math.Sqrt(ww)) / Math.Sqrt(t[i] * PP * (1.0 - PP));
                    c = (Math.Pow(xi, 2.0) * hi[i]) / Math.Pow((1.0 - hi[i]), 2.0);
                    cbar = (Math.Pow(xi, 2.0) * hi[i]) / (1.0 - hi[i]);
                    d = Math.Pow(dr[i], 2.0) + cbar;
                    dc = cbar / hi[i];
                }
                else
                {
                    c = Constant.MISSING;
                    cbar = Constant.MISSING;
                    d = Constant.MISSING;
                    dc = Constant.MISSING;
                }
                yy1[i] = c;
                yy2[i] = cbar;
                yy3[i] = d;
                yy4[i] = dc;
                xx[i] = PP;
            }
            ParameterBag outputParameters = new ParameterBag();
            List<ParameterBag> chartsList = new List<ParameterBag>();
            outputParameters.AddOutput("*chart", chartsList);
            //  Ensure the zeroth element isn't plotted
            xx[0] = Constant.MISSING;
            hi[0] = Constant.MISSING;

            //  delta beta vs. proportion
            using (ChartRenderer ch = (ChartRenderer)ChartRendererFactory.ChartRendererFor(ChartDefinition.Empty()))
            {
                string chart = ch.PlotXYAndReturnRtf(host, xx, yy1, ep, db, db + " vs. " + ep, false, 0, false);
                chartsList.Add(new ParameterBag("chart", new FilledParameter(FilledParameterDirection.Output, chart)));
            }
            //  std delta beta vs. proportion
            using (ChartRenderer ch = (ChartRenderer)ChartRendererFactory.ChartRendererFor(ChartDefinition.Empty()))
            {
                string chart = ch.PlotXYAndReturnRtf(host, xx, yy2, ep, dbs, dbs + " vs. " + ep, false, 0, false);
                chartsList.Add(new ParameterBag("chart", new FilledParameter(FilledParameterDirection.Output, chart)));
            }
            //  delta deviance vs. proportion
            using (ChartRenderer ch = (ChartRenderer)ChartRendererFactory.ChartRendererFor(ChartDefinition.Empty()))
            {
                string chart = ch.PlotXYAndReturnRtf(host, xx, yy3, ep, dd, dd + " vs. " + ep, false, 0, false);
                chartsList.Add(new ParameterBag("chart", new FilledParameter(FilledParameterDirection.Output, chart)));
            }
            //  delta x2 vs. proportion
            using (ChartRenderer ch = (ChartRenderer)ChartRendererFactory.ChartRendererFor(ChartDefinition.Empty()))
            {
                string chart = ch.PlotXYZAndReturnRtf(host, xx, yy4, yy1, ep, dx, dx + " (delta beta as marker size) vs. " + ep, false, 0);
                chartsList.Add(new ParameterBag("chart", new FilledParameter(FilledParameterDirection.Output, chart)));
            }
            //  delta beta vs. hi
            using (ChartRenderer ch = (ChartRenderer)ChartRendererFactory.ChartRendererFor(ChartDefinition.Empty()))
            {
                string chart = ch.PlotXYAndReturnRtf(host, hi, yy1, lv, db, db + " vs. " + lv, false, 0, false);
                chartsList.Add(new ParameterBag("chart", new FilledParameter(FilledParameterDirection.Output, chart)));
            }
            //  delta beta std vs. hi
            using (ChartRenderer ch = (ChartRenderer)ChartRendererFactory.ChartRendererFor(ChartDefinition.Empty()))
            {
                string chart = ch.PlotXYAndReturnRtf(host, hi, yy2, lv, dbs, dbs + " vs. " + lv, false, 0, false);
                chartsList.Add(new ParameterBag("chart", new FilledParameter(FilledParameterDirection.Output, chart)));
            }
            //  delta deviance vs. hi
            using (ChartRenderer ch = (ChartRenderer)ChartRendererFactory.ChartRendererFor(ChartDefinition.Empty()))
            {
                string chart = ch.PlotXYAndReturnRtf(host, hi, yy3, lv, dd, dd + " vs. " + lv, false, 0, false);
                chartsList.Add(new ParameterBag("chart", new FilledParameter(FilledParameterDirection.Output, chart)));
            }
            //  delta x2 vs. hi
            using (ChartRenderer ch = (ChartRenderer)ChartRendererFactory.ChartRendererFor(ChartDefinition.Empty()))
            {
                string chart = ch.PlotXYAndReturnRtf(host, hi, yy4, lv, dx, dx + " vs. " + lv, false, 0, false);
                chartsList.Add(new ParameterBag("chart", new FilledParameter(FilledParameterDirection.Output, chart)));
            }
            return outputParameters;
        }


        private struct Tri
        {
            public double d;
            public double r;
            public double s;
        }


        private class TriByDAscending : IComparer<Tri>
        {
            private static int Compare(Tri x, Tri y)
            {
                if (x.d > y.d)
                    return 1;
                if (x.d == y.d)
                    return 0;
                return -1;
            }

            // interface methods implemented by Compare
            int IComparer<Tri>.Compare(Tri x, Tri y)
            {
                return Compare(x, y);
            }
        }


        public static ParameterBag RptPoissonRegressionModel(ITemplateHost host, ParameterBag parameters)
        {
            MultipleLinearRegressionContext context = ((MultipleLinearRegressionContext)(parameters["context"].Data));
            double[] b = context.B;
            double[] se = context.SE;
            int P = context.P;
            string[] labels = context.Labels;
            bool DoC = context.DoC;
            double dev = context.DEV;
            int irank = context.RANK;
            int idf = context.DF;
            double tol = context.TOL;
            int M = context.M;
            int N = context.N;
            double[] fvl = context.FV;
            double[] y = context.Y;
            bool Weight = context.WEIGHT;
            double[] wt = context.WT;
            double devx = context.DEVX;
            double llx = context.LLX;
            int idfx = context.DFX;
            int i; int iq = 0;
            double ll = 0; double x2 = 0;
            double r2; double r2i; double sp;
            double s_x2dev; double s_x2; double s_dev;
            double prob = 0;
            string Q;

            if (idf > 0)
            {
                x2 = 0.0;
                for (i = 1; i <= N; i++)
                {
                    double ww = Weight ? wt[i] : 1.0;
                    x2 = x2 + (Math.Pow(((y[i] - fvl[i]) * Math.Sqrt(ww)), 2.0)) / Math.Max(fvl[i], Constant.EPSNEG);
                }
                ll = x_loglik_p(Weight, N, wt, y, fvl);
            }

            ParameterBag outputParameters = new ParameterBag();
            if (DoC)
            {
                iq = 1;
            }
            outputParameters.AddOutput("acc", host.RoundU(tol));
            outputParameters.AddOutput("ll", host.RoundU(ll));
            outputParameters.AddOutput("dev", host.RoundU(dev));
            outputParameters.AddOutput("df", idf.ToString());
            outputParameters.AddOutput("rank", irank.ToString());
            outputParameters.AddOutput("aka", host.RoundU(dev + 2 * (1 + M)));
            outputParameters.AddOutput("sch", host.RoundU(dev + (2 + M) * Math.Log(N)));
            outputParameters.AddOutput("devx", host.RoundU(devx));
            double x2dev = devx - dev;
            if (x2dev < 0.0)
            {
                x2dev = Constant.MISSING;
                r2 = Constant.MISSING;
            }
            else
            {
                r2 = x2dev / devx;
            }
            if (llx == Constant.MISSING)
            {
                r2i = Constant.MISSING;
            }
            else
            {
                r2i = 1.0 - ll / llx;
            }
            if (idf > 0 & x2 != 0.0)
            {
                sp = x2 / Convert.ToDouble(N - P);
                s_x2dev = x2dev / sp;
                s_x2 = x2 / sp;
                s_dev = dev / sp;
            }
            else
            {
                sp = Constant.MISSING;
                s_x2dev = Constant.MISSING;
                s_x2 = Constant.MISSING;
                s_dev = Constant.MISSING;
            }
            outputParameters.AddOutput("x2dev", host.RoundU(x2dev));
            outputParameters.AddOutput("x2df", (idfx - idf).ToString());
            if (idf > 0)
            {
                outputParameters.AddOutput("x2p", host.pval(PDF.chivalp(x2dev, Convert.ToDouble(idfx - idf))));
            }
            else
            {
                outputParameters.AddOutput("x2p", "* saturated model: goodness of fit not valid");
                x2dev = Constant.MISSING;
                x2 = Constant.MISSING;
                dev = Constant.MISSING;
            }
            outputParameters.AddOutput("r2", host.RoundU(r2));
            outputParameters.AddOutput("r2i", host.RoundU(r2i));
            outputParameters.AddOutput("x2_pearson", host.RoundU(x2));
            outputParameters.AddOutput("idf", idf.ToString());
            outputParameters.AddOutput("p_pearson", host.pval(PDF.chivalp(x2, Convert.ToDouble(idf))));
            outputParameters.AddOutput("dev_gf", host.RoundU(dev));
            outputParameters.AddOutput("p_dev", host.pval(PDF.chivalp(dev, Convert.ToDouble(idf))));
            // scaled for overdispersion
            outputParameters.AddOutput("sp", host.RoundU(sp));
            outputParameters.AddOutput("x2dev_scaled", host.RoundU(s_x2dev));
            outputParameters.AddOutput("x2p_scaled", host.pval(PDF.chivalp(s_x2dev, Convert.ToDouble(idfx - idf))));
            outputParameters.AddOutput("x2_scaled", host.RoundU(s_x2));
            outputParameters.AddOutput("p_p_scaled", host.pval(PDF.chivalp(s_x2, Convert.ToDouble(idf))));
            outputParameters.AddOutput("dev_scaled", host.RoundU(s_dev));
            outputParameters.AddOutput("p_dev_scaled", host.pval(PDF.chivalp(s_dev, Convert.ToDouble(idf))));

            IList<ParameterBag> unscaledList = new List<ParameterBag>();
            outputParameters.AddOutput("*unscaled", unscaledList);
            for (i = 1; i <= P; i++)
            {
                ParameterBag unscaledParameters = new ParameterBag();
                unscaledList.Add(unscaledParameters);
                if (i == 1 && DoC)
                {
                    Q = "Constant";
                }
                else { Q = labels[i - iq]; }
                unscaledParameters.AddOutput("par", Q);
                unscaledParameters.AddOutput("coef", host.RoundU(b[i]));
                unscaledParameters.AddOutput("err", host.RoundU(se[i]));
            }

            IList<ParameterBag> scaledList = new List<ParameterBag>();
            outputParameters.AddOutput("*scaled", scaledList);
            for (i = 1; i <= P; i++)
            {
                ParameterBag scaledParameters = new ParameterBag();
                scaledList.Add(scaledParameters);
                if (i == 1 && DoC)
                {
                    Q = "Constant";
                }
                else { Q = labels[i - iq]; }
                double sse = sp == Constant.MISSING ? Constant.MISSING : Math.Sqrt(se[i] * se[i] * sp);
                double z;
                if (sse == 0.0 | sse == Constant.MISSING)
                {
                    z = Constant.MISSING;
                }
                else
                {
                    z = b[i] / sse;
                    prob = 2.0 * (1.0 - PDF.alnorm(Math.Abs(b[i]) / sse));
                }
                scaledParameters.AddOutput("par", Q);
                scaledParameters.AddOutput("err", host.RoundU(sse));
                scaledParameters.AddOutput("z", host.RoundU(z));
                scaledParameters.AddOutput("p", host.pval(prob));
            }
            return outputParameters;
        }


        public static ParameterBag RptLogisticRegressionModel(ITemplateHost host, ParameterBag parameters)
        {
            MultipleLinearRegressionContext context = ((MultipleLinearRegressionContext)(parameters["context"].Data));
            double[] b = context.B;
            double[] se = context.SE;
            int P = context.P;
            string[] labels = context.Labels;
            bool DoC = context.DoC;
            double dev = context.DEV;
            int irank = context.RANK;
            int idf = context.DF;
            double tol = context.TOL;
            int M = context.M;
            int N = context.N;
            double[] fvl = context.FV;
            double[] t = context.T;
            double[] y = context.Y;
            bool weight = context.WEIGHT;
            double[] wt = context.WT;
            double devx = context.DEVX;
            double llx = context.LLX;
            int idfx = context.DFX;

            int hldf = 0; int i;
            int iq = 0;
            double x2 = 0; double ll = 0;
            double chat = 0;
            double r2; double r2i;
            if (idf > 0)
            {
                x2 = 0.0;
                int ntot = 0;
                double PP;
                for (i = 1; i <= N; i++)
                {
                    PP = fvl[i] / t[i];
                    double ww = weight ? wt[i] : 1;
                    if (PP != 0.0)
                    {
                        x2 = x2 + (Math.Pow(((y[i] - t[i] * PP) * Math.Sqrt(ww)), 2.0)) / (t[i] * PP * (1.0 - PP));
                    }
                    ntot += Convert.ToInt32(t[i]);
                }
                ll = x_loglik_l(weight, N, wt, y, t, fvl);
                double[] OBS = new double[10 + 1];
                double[] tot = new double[10 + 1];
                double[] mpi = new double[10 + 1 ];
                Tri[] z = new Tri[N + 1 ];
                for (i = 1; i <= N; i++)
                {
                    PP = fvl[i] / t[i];
                    z[i].d = PP;
                    z[i].r = y[i];
                    z[i].s = t[i];
                }
                Array.Sort(z, 1, N, new TriByDAscending());
                int ctr = 1;
                double ndiv = Convert.ToDouble(ntot) / 10.0;
                double ncut = Math.Floor(ndiv);
                double ncum = 0;
                for (i = 1; i <= N; i++)
                {
                    if (ncum >= ncut & ctr <= 10)
                    {
                        ctr = ctr + 1;
                        double transTemp8 = Math.Max(ncum + ndiv, ctr * ndiv);
                        ncut = Math.Floor(transTemp8);
                    }
                    tot[ctr] = tot[ctr] + z[i].s;
                    ncum = ncum + z[i].s;
                    OBS[ctr] = OBS[ctr] + z[i].r;
                    mpi[ctr] = mpi[ctr] + z[i].d * z[i].s;
                    if (ncum >= ntot)
                    {
                        break;
                    }
                }
                hldf = ctr - 2;
                chat = 0.0;
                for (i = 1; i <= ctr; i++)
                {
                    if (tot[i] > 0.0 & mpi[i] > 0.0)
                    {
                        double numer = (OBS[i] - mpi[i]) * (OBS[i] - mpi[i]);
                        mpi[i] = mpi[i] / tot[i];
                        chat = chat + numer / (tot[i] * mpi[i] * (1.0 - mpi[i]));
                    }
                }
            }
            if (DoC)
            {
                iq = 1;
            }
            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("acc", host.RoundU(tol));
            outputParameters.AddOutput("ll", host.RoundU(ll));
            outputParameters.AddOutput("dev", host.RoundU(dev));
            outputParameters.AddOutput("idf", idf.ToString());
            outputParameters.AddOutput("rank", irank.ToString());
            outputParameters.AddOutput("aka", host.RoundU(dev + 2 * (1 + M)));
            outputParameters.AddOutput("sch", host.RoundU(dev + (2 + M) * Math.Log(N)));
            outputParameters.AddOutput("devx", host.RoundU(devx));
            double x2dev = devx - dev;
            if (devx == Constant.MISSING)
            {
                x2dev = Constant.MISSING;
                r2 = Constant.MISSING;
            }
            else
            {
                r2 = x2dev / devx;
            }
            if (llx == Constant.MISSING)
            {
                r2i = Constant.MISSING;
            }
            else
            {
                r2i = 1.0 - ll / llx;
            }
            outputParameters.AddOutput("x2dev", host.RoundU(x2dev));
            outputParameters.AddOutput("x2df", (idfx - idf).ToString());
            outputParameters.AddOutput("x2p",
                                       idfx > 0
                                           ? host.pval(PDF.chivalp(x2dev, Convert.ToDouble(idfx - idf)))
                                           : Formatting.ASTERISK);
            outputParameters.AddOutput("r2", host.RoundU(r2));
            outputParameters.AddOutput("r2i", host.RoundU(r2i));
            outputParameters.AddOutput("x2", host.RoundU(x2));
            if (idf <= 0)
            {
                x2 = Constant.MISSING;
                dev = Constant.MISSING;
                chat = Constant.MISSING;
                outputParameters.AddOutput("p", "* saturated model: can't assess goodness of fit");
            }
            else
            {
                outputParameters.AddOutput("p", host.pval(PDF.chivalp(x2, Convert.ToDouble(idf))));
            }
            outputParameters.AddOutput("dev_good", host.RoundU(dev));
            outputParameters.AddOutput("df_good", idf.ToString());
            outputParameters.AddOutput("p_good", host.pval(PDF.chivalp(dev, Convert.ToDouble(idf))));
            outputParameters.AddOutput("hl", host.RoundU(chat));
            outputParameters.AddOutput("df_hl", hldf.ToString());
            double hlp = hldf < 1 ? Constant.MISSING : PDF.chivalp(chat, Convert.ToDouble(hldf));
            outputParameters.AddOutput("p_hl", host.pval(hlp));
            List<ParameterBag> parametersList = new List<ParameterBag>();
            outputParameters.AddOutput("*parameters", parametersList);
            double prob;
            for (i = 1; i <= P; i++)
            {
                if (se[i] == 0)
                    prob = Constant.MISSING;
                else
                    prob = 2.0 * (1.0 - PDF.alnorm(Math.Abs(b[i] / se[i])));
                ParameterBag parametersParameters = new ParameterBag();
                parametersList.Add(parametersParameters);
                string Q = i == 1 && DoC ? "(intercept)" : labels[i - iq];
                parametersParameters.AddOutput("par", Q);
                parametersParameters.AddOutput("coef", host.RoundU(b[i]));
                parametersParameters.AddOutput("err", host.RoundU(se[i]));
                double z;
                if (se[i] == 0.0)
                {
                    z = Constant.MISSING;
                    parametersParameters.AddOutput("z", host.RoundU(z));
                    parametersParameters.AddOutput("p_var", "* error: drop this variable *");
                }
                else
                {
                    z = b[i] / se[i];
                    parametersParameters.AddOutput("z", host.RoundU(z));
                    parametersParameters.AddOutput("p_var", host.pval(prob));
                }
            }
            return outputParameters;
        }


        public static ParameterBag RptPoissonRegressionIrr(ITemplateHost host, ParameterBag parameters)
        {
            MultipleLinearRegressionContext context = ((MultipleLinearRegressionContext)(parameters["context"].Data));
            double[,] x = context.X;
            double[] b = context.B;
            double[] se = context.SE;
            int p = context.P;
            int nx = context.N;
            string[] labels = context.Labels;
            bool DoC = context.DoC;
            double GAMMA = parameters["gamma"].AsDouble;
            double cit; double P0;
            MathDbl.civ(0, out cit, GAMMA, out P0);
            ParameterBag outputParameters = new ParameterBag();

            //  Whole study
            IList<ParameterBag> popList = new List<ParameterBag>();
            outputParameters.AddOutput("*pop", popList);
            ParameterBag popParameters = new ParameterBag();
            popList.Add(popParameters);
            popParameters.AddOutput("pop", "whole study (baseline relative risk)");
            popParameters.AddOutput("pc", Formatting.XRound(100 * (1.0 - P0), 2));
            IList<ParameterBag> parList = new List<ParameterBag>();
            popParameters.AddOutput("*par", parList);
            int iq = DoC ? 1 : 0;
            for (int i = 1; i <= p; i++)
            {
                if (i != 1 || !DoC)
                {
                    ParameterBag parParameters = new ParameterBag();
                    parList.Add(parParameters);
                    parParameters.AddOutput("par", labels[i - iq]);
                    parParameters.AddOutput("est", host.RoundU(b[i]));
                    double rr = Formatting.SafeExp(b[i]);
                    double lci;
                    double uci;
                    if (rr != Constant.MISSING)
                    {
                        lci = Formatting.SafeExp(b[i] - se[i] * cit);
                        uci = Formatting.SafeExp(b[i] + se[i] * cit);
                    }
                    else
                    {
                        lci = Constant.MISSING;
                        uci = Constant.MISSING;
                    }
                    parParameters.AddOutput("irr", host.RoundU(rr));
                    parParameters.AddOutput("ci", host.RoundU(lci) + "  to  " + host.RoundU(uci));
                }
            }

            // relative to dichotomous covariates
            if (parameters["hasDichotomousCovariates"].AsBoolean)
            {
                double[] nsel = (double[])(parameters["dichotomousCovariates"].AsDataFrame.Variables[1] as DoubleVariable).Data;
                bool[] cov = (bool[])parameters["cov"].Data;
                int selectedIndex = 0;
                for (int i = 0; i <= cov.GetUpperBound(0); i++)
                {
                    if (cov[i])
                    {
                        selectedIndex = i;
                        break;
                    }
                }
                int l = (int)nsel[selectedIndex];
                for (int j = 2; j >= 1; j--)
                {
                    double bx = Formatting.SafeExp(b[l]);
                    if (bx != Constant.MISSING)
                    {
                        popParameters = new ParameterBag();
                        popList.Add(popParameters);
                        popParameters.AddOutput("pop", labels[l - iq] + " = " + (j - 1).ToString());
                        popParameters.AddOutput("pc", Formatting.XRound(100 * (1.0 - P0), 2));
                        parList = new List<ParameterBag>();
                        popParameters.AddOutput("*par", parList);
                        if (j == 1)
                            bx = 1.0 / bx;

                        for (int i = 1; i <= p; i++)
                        {
                            if ((i != 1 || !DoC) && i != l)
                            {
                                ParameterBag parParameters = new ParameterBag();
                                parList.Add(parParameters);
                                parParameters.AddOutput("par", labels[i - iq]);
                                parParameters.AddOutput("est", host.RoundU(b[i]));
                                double rr = Formatting.SafeExp(b[i]) * bx;
                                double lci;
                                double uci;
                                if (rr != Constant.MISSING)
                                {
                                    lci = Formatting.SafeExp(b[i] * bx - se[i] * cit * bx);
                                    uci = Formatting.SafeExp(b[i] * bx + se[i] * cit * bx);
                                }
                                else
                                {
                                    lci = Constant.MISSING;
                                    uci = Constant.MISSING;
                                }
                                parParameters.AddOutput("irr", host.RoundU(rr));
                                parParameters.AddOutput("ci", host.RoundU(lci) + "  to  " + host.RoundU(uci));
                            }
                        }
                    }
                }
            }
            return outputParameters;
        }

        public static ParameterBag OpPoissonRegressionIrrMakeDichotomousCovariates(ITemplateHost host, ParameterBag parameters)
        {
            MultipleLinearRegressionContext context = ((MultipleLinearRegressionContext)(parameters["context"].Data));
            double[,] x = context.X;
            int p = context.P;
            int nx = context.N;

            ParameterBag outputParameters = new ParameterBag();

            int iq = context.DoC ? 1 : 0;

            // Locate dichotomous covariates so that the user can be asked which one they want
            List<double> nsel = new List<double>();
            List<string> okLabels = new List<string>();
            for (int l = 1; l <= p; l++)
            {
                if (l > 1 || !context.DoC)
                {
                    bool ok = true;
                    int k = context.DoC ? l - 1 : l;
                    for (int j = 1; j <= nx; j++)
                    {
                        if (x[j, k] != 0.0 && x[j, k] != 1.0)
                        {
                            ok = false;
                            break;
                        }
                    }
                    if (ok)
                    {
                        okLabels.Add(context.Labels[l - iq]);
                        nsel.Add(l);
                    }
                }
            }

            // We need to ask if there's at least one dichotomous covariate and at least two coefficients other than the intercept
            outputParameters.AddOutput("hasDichotomousCovariates", okLabels.Count >= 1 && p - iq > 1);
            StringVariable names = new StringVariable(okLabels.ToArray(), "labels");
            DoubleVariable offsets = new DoubleVariable(nsel.ToArray(), "offsets");
            DataFrame dcFrame = new DataFrame();
            dcFrame.Variables.Add(names);
            dcFrame.Variables.Add(offsets);
            outputParameters.AddOutput("dichotomousCovariates", dcFrame);
            return outputParameters;
        }

        public static ParameterBag RptLogisticRegressionModelSelection(ITemplateHost host, ParameterBag parameters)
        {
            MultipleLinearRegressionContext context = ((MultipleLinearRegressionContext)(parameters["context"].Data));
            bool mean = context.DoC;
            int n = context.N;
            string[] labels = context.Labels;
            double[] y = context.Y;
            double[] t = context.T;
            double[] wt = context.WT;
            bool use_weights = context.WEIGHT;
            double[,] x = context.X;
            int p = context.P;
            int m = context.M;
            double tol = context.TOL;
            string dropped = string.Empty;
            string err_msg = string.Empty;
            //intercept deviance and degrees of freedom can be used from original fit
            int dfx = context.DFX;
            double devx = context.DEVX;

            ParameterBag outputParameters = new ParameterBag();
            List<ParameterBag> parametersList = new List<ParameterBag>();
            outputParameters.AddOutput("*parameters", parametersList);

            //first show the full model
            bool[] selectX = new bool[p + 1];
            for (int j = 1; j <= p; j++)
                selectX[j] = true;

            double[] b = new double[p + 1];
            double[] se = new double[n + 1];
            double[] cov = new double[((int)(Math.Floor((double)p * (p + 1) / 2))) + 1];
            double[] fv = new double[n + 1];
            double[] dr = new double[n + 1];
            double[] h = new double[n + 1];
            double[] offst = new double[n + 1];
            double dev;
            int df = 0;
            int rank = 0;
            bool iweight = true;
            int fault;
            dropped = string.Empty;
            err_msg = string.Empty;
            host.StartProgress("Checking significance with all predictors", false);
            Regress1.X_Logistic_Regression(mean, false, ref iweight, n, x, m, selectX, p, y, t, wt, out dev, ref df, b, ref rank, se, cov, tol, 50, fv, dr, h, offst, out fault, ref dropped, ref err_msg);
            LR_ModelSelectionOutput(host, parametersList, fault, labels, b, se, mean, dev, devx, p, m, df, dfx, err_msg, selectX);
            host.FinishProgress();

            // Now add predictors one at time: select the predictor that gives max Akaike information to the model on each addition, building up to the full model again
            host.StartProgress("Selecting most informative predictors. Small models are tested first. Cancel will give interim results.", true);
            bool[] previousSelection = new bool[p + 1]; // All blank initially; no previous selections.
            int parms = mean ? 2 : 1;
            double estimatedRegressionsToRun = m * (m + 1) / 2.0 + m;
            int regressionsRun = 0;
            while (true)
            {
                selectX = new bool[p + 1];
                Array.Copy(previousSelection, selectX, p + 1);
                int bestPredictorIndexSoFar = 0;
                double minAkaikeInformationSoFar = double.MaxValue;
                bool abandon = false;
                for (int candidate = 1; candidate <= m; candidate++)
                {
                    // If we've already processed this one, don't do so again
                    if (previousSelection[candidate])
                        continue;

                    // Check whether it's better than our best so far this run; if so, note the fact.
                    // Lower AIC values are better - the value represents the information *lost* if this model is chosen.
                    selectX[candidate] = true;
                    Regress1.X_Logistic_Regression(mean, false, ref iweight, n, x, m, selectX, parms, y, t, wt, out dev, ref df, b, ref rank, se, cov, tol, 50, fv, dr, h, offst, out fault, ref dropped, ref err_msg);
                    if (host.UpdateProgress((++regressionsRun) / estimatedRegressionsToRun))
                    {
                        abandon = true;
                        break;
                    }
                    double aic = dev + 2 * (1 + m);
                    if (aic < minAkaikeInformationSoFar)
                    {
                        bestPredictorIndexSoFar = candidate;
                        minAkaikeInformationSoFar = aic;
                    }
                    selectX[candidate] = false;
                }

                // Have we added all predictors (or has the user given up)?
                if (bestPredictorIndexSoFar <= 0 || abandon)
                    break;

                // Re-do the regression with that predictor selected along with any others we may have from previous iterations
                selectX[bestPredictorIndexSoFar] = true;
                Regress1.X_Logistic_Regression(mean, false, ref iweight, n, x, m, selectX, parms, y, t, wt, out dev, ref df, b, ref rank, se, cov, tol, 50, fv, dr, h, offst, out fault, ref dropped, ref err_msg);
                LR_ModelSelectionOutput(host, parametersList, fault, labels, b, se, mean, dev, devx, p, m, df, dfx, err_msg, selectX);
                if (host.UpdateProgress((++regressionsRun) / estimatedRegressionsToRun))
                    break;

                // Go round again, remembering the predictor we've chosen this time
                previousSelection = selectX;
                parms++;
            }
            host.FinishProgress();

            return outputParameters;
        }

        private static void LR_ModelSelectionOutput(ITemplateHost host, List<ParameterBag> parametersList, int fault,string[] label, double[] b, double[] se, bool mean, double dev, double devx, int p, int m, int df, int dfx, string err_msg, bool[] selectX)
        {
            ParameterBag parametersParameters = new ParameterBag();
            parametersList.Add(parametersParameters);
            if (fault != 0)
            {
                parametersParameters.AddOutput("model", err_msg);
                return;
            }

            string tx = "logit ";
            tx += (label[0].Length > 0) ? label[0] : "Y";
            tx += " = ";
            int significantCoefficients = 0;
            int totalCoefficients = 0;
            for (int j = 1; j <= p; j++)
            {
                bool isIntercept = mean && (j == 1);
                // Only process this part of the model if it is selected
                if (!isIntercept)
                {
                    int selectXIndex = mean ? j - 1 : j;
                    if (!selectX[selectXIndex])
                        continue;
                }

                if ((!isIntercept) && b[j] >= 0.0)
                    tx += "+";
                tx += host.RoundU(b[j]);
                bool isSignificant;
                tx += SignificanceString(b, se, out isSignificant, j);
                if (isSignificant)
                    significantCoefficients++;
                totalCoefficients++;
                string q = mean ? (j > 1 ? label[j - 1] : " ") : label[j];
                if (q.Length == 0)
                    tx += " X" + j.ToString();
                else
                    tx += " " + q + " ";
            }

            parametersParameters.AddOutput("model", tx);
            parametersParameters.AddOutput("aic", host.RoundU(dev + 2 * (1 + m)));
            double x2dev = devx - dev;
            double r2;
            if (devx == Constant.MISSING)
            {
                x2dev = Constant.MISSING;
                r2 = Constant.MISSING;
            }
            else
            {
                r2 = x2dev / devx;
            }
            parametersParameters.AddOutput("r2", host.RoundU(r2));
            parametersParameters.AddOutput("scoef", significantCoefficients.ToString());
            parametersParameters.AddOutput("ncoef", totalCoefficients.ToString());
            parametersParameters.AddOutput("x2dev", host.RoundU(x2dev));
            parametersParameters.AddOutput("p_dev",
                                        dfx > 0
                                            ? host.pval(PDF.chivalp(x2dev, dfx - df))
                                            : Formatting.ASTERISK);
        }

        private static string SignificanceString(double[] b, double[] se, out bool isSignificant, int j)
        {
            isSignificant = false;
            if (se[j] == 0.0)
                return "~N/A";
            double prob = 2.0 * (1.0 - PDF.alnorm(Math.Abs(b[j] / se[j])));
            if (prob < 0.05)
            {
                isSignificant = true;
                return string.Empty;
            }
            else if (prob >= 0.05 && prob < 0.2)
                return "~?NS";
            else
                return "~NS";
        }


        public static ParameterBag RptLogisticRegressionClassification(ITemplateHost host, ParameterBag parameters)
        {
            MultipleLinearRegressionContext context = ((MultipleLinearRegressionContext)(parameters["context"].Data));
            int N = context.N;
            double[] fvl = context.FV;
            double[] t = context.T;
            double[] y = context.Y;

            double zc; double zl;
            double sns; double spe; double ppv; double nnv; double lrpos; double thetal; double thetau; double lrneg;
            int tp; int fn; int fp; int tn; int i;

            ParameterBag outputParameters = new ParameterBag();
            double co = parameters["cutoff"].AsDouble;
            if (co <= 0.0 || co >= 1.0)
            {
                co = 0.5;
            }
            double GAMMA = parameters["gamma"].AsDouble;
            MathDbl.civ(0, out zc, GAMMA, out zl);
            x_lclass(t, fvl, y, N, out tp, out fp, out fn, out tn, co);
            outputParameters.AddOutput("tp", tp.ToString());
            outputParameters.AddOutput("fp", fp.ToString());
            outputParameters.AddOutput("totc", (tp + fp).ToString());
            outputParameters.AddOutput("fn", fn.ToString());
            outputParameters.AddOutput("tn", tn.ToString());
            outputParameters.AddOutput("totnoc", (tn + fn).ToString());
            outputParameters.AddOutput("tote", (tp + fn).ToString());
            outputParameters.AddOutput("totnoe", (fp + tn).ToString());
            outputParameters.AddOutput("co", Formatting.XRound(co, 2));
            double a = Convert.ToDouble(tp);
            double b = Convert.ToDouble(fp);
            double C = Convert.ToDouble(fn);
            double D = Convert.ToDouble(tn);
            if (tp + fn > 0)
            {
                sns = 100.0 * a / (a + C);
            }
            else { sns = Constant.MISSING; }
            outputParameters.AddOutput("sens", Formatting.XRound(sns, 2));
            if (tn + fp > 0)
            {
                spe = 100.0 * D / (b + D);
            }
            else { spe = Constant.MISSING; }
            outputParameters.AddOutput("spec", Formatting.XRound(spe, 2));
            if (tp + fp > 0)
            {
                ppv = 100.0 * a / (a + b);
            }
            else { ppv = Constant.MISSING; }
            outputParameters.AddOutput("+ve", Formatting.XRound(ppv, 2));
            if ((tn + fn) > 0)
            {
                nnv = 100.0 * (1.0 - D / (D + C));
            }
            else { nnv = Constant.MISSING; }
            outputParameters.AddOutput("npv", Formatting.XRound(100.0 - nnv, 2));
            outputParameters.AddOutput("-ve", Formatting.XRound(nnv, 2));
            outputParameters.AddOutput("cc", Formatting.XRound(100.0 * (a + D) / (a + b + C + D), 2));
            outputParameters.AddOutput("pc", Formatting.XRound(100.0 * (1.0 - zl), 2));
            if (tp + fn > 0 & fp + tn > 0 & fp > 0 & tp > 0)
            {
                lrpos = (a / (a + C)) / (b / (b + D));
                x_lrci(b, a, b + D, a + C, zc, out thetal, out thetau);
            }
            else
            {
                lrpos = Constant.MISSING;
                thetal = Constant.MISSING;
                thetau = Constant.MISSING;
            }
            outputParameters.AddOutput("lr+", host.RoundU(lrpos));
            outputParameters.AddOutput("from+", host.RoundU(thetal));
            outputParameters.AddOutput("to+", host.RoundU(thetau));
            if (tp + fn > 0 & fp + tn > 0 & tn > 0 & fn > 0)
            {
                lrneg = (C / (a + C)) / (D / (D + b));
                x_lrci(D, C, b + D, a + C, zc, out thetal, out thetau);
            }
            else
            {
                lrneg = Constant.MISSING;
                thetal = Constant.MISSING;
                thetau = Constant.MISSING;
            }
            outputParameters.AddOutput("lr-", host.RoundU(lrneg));
            outputParameters.AddOutput("from-", host.RoundU(thetal));
            outputParameters.AddOutput("to-", host.RoundU(thetau));
            double XMax = 0.0;
            co = 0.0;
            double cmax = co;
            const int stps = 100;
            double[] ry = new double[stps + 1 ];
            double[] rx = new double[stps + 1 ];
            for (i = 1; i <= stps; i++)
            {
                co = co + 0.01;
                x_lclass(t, fvl, y, N, out tp, out fp, out fn, out tn, co);
                if (tp + fn > 0 && tn + fp > 0)
                {
                    double sens = Convert.ToDouble(tp) / Convert.ToDouble(tp + fn);
                    double sec = Convert.ToDouble(tn) / Convert.ToDouble(tn + fp);
                    ry[i] = sens;
                    rx[i] = 1.0 - sec;
                    if (sens + sec > XMax)
                    {
                        XMax = sens + sec;
                        cmax = co;
                    }
                }
            }
            double auc = MathDbl.trapezoid_xy_roc(rx, ry, 1, stps);
            outputParameters.AddOutput("cmax", Formatting.XRound(cmax, 3));
            outputParameters.AddOutput("area", host.RoundU(auc));
            using (ChartRenderer ch = (ChartRenderer)ChartRendererFactory.ChartRendererFor(ChartDefinition.Empty()))
            {
                ch.SetBox0To1();
                outputParameters.AddOutput("chart", ch.PlotXYAndReturnRtf(host, rx, ry, "1-specificity", "sensitivity", string.Empty, false, DataMinMax.XPreset_YPreset, false));
            }
            return outputParameters;
        }

        public static ParameterBag RptLogisticRegressionBootstrap(ITemplateHost host, ParameterBag parameters)
        {
            MultipleLinearRegressionContext context = ((MultipleLinearRegressionContext)(parameters["context"].Data));
            double[] se;
            double[] b = context.B;
            double[] t = context.T;
            double[,] x = context.X;
            double[] y = context.Y;
            double[] fv;
            double[] dr;
            double[] h;
            int n = context.N;
            bool mean = context.DoC;
            string[] labels = context.Labels;
            int p = context.P;
            double[] cov;
            double[] wt = context.WT;
            bool use_weights = context.WEIGHT;
            int rank = context.RANK;
            int df = context.DF;
            double tol = context.TOL;
            int predictors = context.M;
            double dev = context.DEV;

            int j; int i;
            int iq = 0;
            double cit; double P0; double[] offst;
            string dropped = string.Empty;
            string err_msg = string.Empty;

            double GAMMA = parameters["gamma"].AsDouble;
            MathDbl.civ(0, out cit, GAMMA, out P0);
            bool[] isx = new bool[p + 1];
            double[] ob = new double[p + 1];
            for (j = 1; j <= p; j++)
            {
                isx[j] = true;
                ob[j] = Formatting.SafeExp(b[j]);
            }
            int gtot = 0;
            for (j = 1; j <= n; j++)
            {
                gtot += Convert.ToInt32(t[j]);
            }
            int boots = parameters["boots"].AsInt32;
            host.StartProgress("Bootstrapping " + boots.ToString() + " iterations", true);
            double[,] qo = new double[p + 1, boots + 1];
            double[] theta = new double[p + 1];
            double[] ql = new double[p + 1];
            double[] qu = new double[p + 1];
            int booted = 0;
            MersenneTwister rng = new MersenneTwister();
            for (i = 1; i <= boots; i++)
            {
                if (host.UpdateProgress(i / (double)boots))
                    break;

                double[] rndy = new double[n + 1];
                double[] rndt = new double[n + 1];
                double[] rndwt = new double[n + 1];
                for (j = 1; j <= gtot; j++)
                {
                    int pick = Convert.ToInt32((gtot - 1) * rng.NextDouble()) + 1;
                    int pivot = 0;
                    int k;
                    for (k = 1; k <= n; k++)
                    {
                        pivot += Convert.ToInt32(t[k]);
                        if (pick <= pivot)
                        {
                            rndt[k]++;
                            if (rng.NextDouble() <= (Convert.ToInt64(y[k]) / (double)Convert.ToInt64(t[k])))
                                rndy[k]++;
                            break;
                        }
                    }
                }
                for (j = 1; j <= n; j++)
                {
                    if (rndy[j] == 0.0)
                    {
                        rndy[j] = Constant.EPSNEG;
                    }
                    if (rndy[j] == rndt[j])
                    {
                        rndy[j] = rndy[j] - Constant.EPSNEG;
                    }
                    if (rndt[j] == 0.0)
                    {
                        rndwt[j] = 0.0;
                    }
                    else
                    {
                        rndwt[j] = wt[j];
                    }
                }
                b = new double[p + 1];
                se = new double[n + 1];
                cov = new double[((int)(Math.Floor((double)p * (p + 1) / 2))) + 1];
                fv = new double[n + 1];
                dr = new double[n + 1];
                h = new double[n + 1];
                offst = new double[n + 1 ];
                bool iweight = true;
                int fault;
                dropped = string.Empty;
                err_msg = string.Empty;
                Regress1.X_Logistic_Regression(mean, false, ref iweight, n, x, predictors, isx, p, rndy, rndt, rndwt, out dev, ref df, b, ref rank, se, cov, tol, 50, fv, dr, h, offst, out fault, ref dropped, ref err_msg);
                if (fault == 0)
                {
                    booted++;
                    for (j = 1; j <= p; j++)
                    {
                        qo[j, booted] = Formatting.SafeExp(b[j]);
                        if (qo[j, booted] < 1000.0 * ob[j])
                            theta[j] += qo[j, booted];
                    }
                }
            }
            host.FinishProgress();

            // In the case of the bootstrap not running to its full iterations because the user cancelled, present results as far as it's got (#669)
            boots = i - 1;

            ParameterBag outputParameters = new ParameterBag();

            for (j = 1; j <= p; j++)
            {
                theta[j] = theta[j] / Convert.ToDouble(booted);
            }
            for (j = 1; j <= p; j++)
            {
                double[] qq = new double[booted + 1 ];
                int ctr = 0;
                for (i = 1; i <= booted; i++)
                {
                    qq[i] = qo[j, i];
                    if (qq[i] <= ob[j])
                    {
                        ctr = ctr + 1;
                    }
                }
                Array.Sort(qq, 1, booted);
                double z0 = ctr / (double)booted;
                int ifault;
                z0 = PDF.gauinv(z0, out ifault);
                double P1 = PDF.alnorm(2.0 * z0 - cit);
                double P2 = PDF.alnorm(2.0 * z0 + cit);
                ql[j] = qq[Convert.ToInt32(Convert.ToDouble(booted - 1) * P1) + 1];
                qu[j] = qq[Convert.ToInt32(Convert.ToDouble(booted - 1) * P2) + 1];
            }
            if (boots == booted)
                outputParameters.AddOutput("boots", booted.ToString());
            else
                outputParameters.AddOutput("boots", booted.ToString() + ", warning: " + (boots - booted).ToString() + " re-samples were dropped because they caused error in the regression");
            outputParameters.AddOutput("pc", Formatting.XRound(100 * (1.0 - P0), 2));
            if (mean)
                iq = 1;
            List<ParameterBag> parametersList = new List<ParameterBag>();
            outputParameters.AddOutput("*parameters", parametersList);
            for (i = 1; i <= p; i++)
            {
                ParameterBag parametersParameters = new ParameterBag();
                parametersList.Add(parametersParameters);
                string Q = i == 1 && mean ? "Constant" : labels[i - iq];
                parametersParameters.AddOutput("par", Q);
                parametersParameters.AddOutput("obs", host.RoundU(ob[i]));
                if (i > 1 | mean == false)
                {
                    parametersParameters.AddOutput("bias", host.RoundU(theta[i] - ob[i]));
                    parametersParameters.AddOutput("ci", host.RoundU(ql[i]) + "  to  " + host.RoundU(qu[i]));
                }
                else
                {
                    parametersParameters.AddOutput("bias", string.Empty);
                    parametersParameters.AddOutput("ci", string.Empty);
                }
            }
            //  recalculate full model
            b = new double[p + 1];
            se = new double[n + 1];
            cov = new double[((int)(Math.Floor((double)p * (p + 1) / 2))) + 1];
            fv = new double[n + 1 ];
            dr = new double[n + 1 ];
            h = new double[n + 1];
            offst = new double[n + 1 ];
            dropped = string.Empty;
            err_msg = string.Empty;
            int scrapFault;
            Regress1.X_Logistic_Regression(mean, false, ref use_weights, n, x, predictors, isx, p, y, t, wt, out dev, ref df, b, ref rank, se, cov, tol, 50, fv, dr, h, offst, out scrapFault, ref dropped, ref err_msg);
            return outputParameters;
        }


        public static ParameterBag RptLogisticRegressionPrediction(ITemplateHost host, ParameterBag parameters)
        {
            MultipleLinearRegressionContext context = ((MultipleLinearRegressionContext)(parameters["context"].Data));
            double[] b = context.B;
            int P = context.P;
            string[] labels = context.Labels;
            double[] covariance = context.Covariance;
            bool DoC = context.DoC;

            int iq; int i; int j;
            double P0; double cit;
            double lcl; double ucl;


            ParameterBag outputParameters = new ParameterBag();
            double[] newx = new double[P + 1 ];
            bool lsqmean = true;
            if (DoC)
            {
                iq = 1;
                newx[1] = 1.0;
            }
            else
            {
                iq = 0;
            }
            DataFrame candidatePredictors = parameters["candidatePredictors"].AsDataFrame;
            //  Predictors are guaranteed to be in the same order as the labels
            StringVariable valueVariable = candidatePredictors.Variables[1]as StringVariable;
            DoubleVariable oldValueVariable = candidatePredictors.Variables[2]as DoubleVariable;
            for (i = 1; i <= P - iq; i++)
            {
                newx[i + 1] = Parsing.Cdbl_Txt(valueVariable.Data[i - 1]);
                if (newx[i + 1] != oldValueVariable.Data[i - 1])
                {
                    lsqmean = false;
                }
            }
            double newy = 0.0;
            for (i = 1; i <= P; i++)
            {
                newy = newy + (newx[i] * b[i]);
            }
            double GAMMA = parameters["gamma"].AsDouble;
            MathDbl.civ(0, out cit, GAMMA, out P0);
            List<ParameterBag> predictorsList = new List<ParameterBag>();
            outputParameters.AddOutput("*predictors", predictorsList);
            for (i = 1; i <= P - iq; i++)
            {
                ParameterBag predictorsParameters = new ParameterBag();
                predictorsList.Add(predictorsParameters);
                predictorsParameters.AddOutput("x", labels[i] + " = " + host.RoundU(newx[i + iq]));
            }
            string msg = lsqmean ? "  (regression mean)" : string.Empty;
            //  sd of Y from covariance matrix as sqr(xVx')
            double sey = 0.0;
            for (j = 1; j <= P; j++)
            {
                double s = 0.0;
                for (i = 1; i <= P; i++)
                {
                    // unpack the upper triangular packed form of the covariance matrix
                    int ii;
                    int jj;
                    if (j >= i)
                    {
                        jj = j;
                        ii = i;
                    }
                    else
                    {
                        jj = i;
                        ii = j;
                    }
                    s = s + covariance[((int)(Math.Floor((double)jj * (jj - 1) / 2 + ii)))] * newx[i];
                }
                sey = sey + s * newx[j];
            }
            if (sey >= 0.0)
            {
                sey = Math.Sqrt(sey);
                double cl = cit * sey;
                lcl = newy - cl;
                ucl = newy + cl;
                //  transform to outcome probability scale
                newy = pfromlogit(newy);
                lcl = pfromlogit(lcl);
                ucl = pfromlogit(ucl);
            }
            else
            {
                newy = pfromlogit(newy);
                lcl = Constant.MISSING;
                ucl = Constant.MISSING;
            }
            outputParameters.AddOutput("y", "Response proportion = " + host.RoundU(newy) + msg);
            outputParameters.AddOutput("pc", (Formatting.XRound(100 * (1.0 - P0), 1)));
            outputParameters.AddOutput("from", host.RoundU(lcl));
            outputParameters.AddOutput("to", host.RoundU(ucl));
            return outputParameters;
        }


        private static void x_lrci(double fp, double tp, double column2total, double column1total, double zc, out double thetal, out double thetau)
        {
            double lastz = 0;

            double x0 = fp;
            double x1 = tp;
            double n0 = column2total;
            if (n0 == x0)
            {
                n0 = n0 + 0.5;
            }
            double n1 = column1total;
            if (n1 == x1)
            {
                n1 = n1 + 0.5;
            }
            double uhat = (1.0 / (x1 + 0.5)) + (1.0 / (x0 + 0.5)) - (1.0 / (n0 + 0.5)) - (1.0 / (n1 + 0.5));
            double N = n0 + n1;
            double logthetahat = Math.Log((x1 + 0.5) / (n1 + 0.5)) - Math.Log((x0 + 0.5) / (n0 + 0.5));
            thetau = Math.Exp(logthetahat) * Math.Exp(zc * Math.Sqrt(uhat));
            thetal = Math.Exp(logthetahat) * Math.Exp(-zc * Math.Sqrt(uhat));
            for (int i = 1; i <= 2; i++)
            {
                double za2 = zc;
                double temptheta1 = i == 1 ? thetau : thetal;
                double temptheta2 = 0.9 * temptheta1;
                double a;
                double b;
                double c;
                double ztemp1 = x_lrz(temptheta1, out a, out b, out c, N, n0, n1, x0, x1);
                double diff1 = Math.Abs(za2 - Math.Abs(ztemp1));
                double ztemp2 = x_lrz(temptheta2, out a, out b, out c, N, n0, n1, x0, x1);
                double diff2 = Math.Abs(za2 - Math.Abs(ztemp2));
                double theta1;
                double theta0;
                double z1;
                double z0;
                double zcritical;
                x_lrdiff(diff1, diff2, out theta1, out theta0, temptheta1, temptheta2, out z1, out z0, ztemp1, ztemp2, out zcritical);
                int cnt = 0;
                double theta2;
                do
                {
                    if (i == 1)
                    {
                        za2 = -zc;
                    }
                    theta2 = Math.Exp(Math.Log(theta0) + ((za2 - z0) / (z1 - z0)) * Math.Log(theta1 / theta0));
                    temptheta1 = theta1;
                    temptheta2 = theta2;
                    ztemp2 = x_lrz(temptheta2, out a, out b, out c, N, n0, n1, x0, x1);
                    ztemp1 = z1;
                    diff1 = Math.Abs(za2 - ztemp1);
                    diff2 = Math.Abs(za2 - ztemp2);
                    x_lrdiff(diff1, diff2, out theta1, out theta0, temptheta1, temptheta2, out z1, out z0, ztemp1, ztemp2, out zcritical);
                    cnt = cnt + 1;
                    if (cnt > 5000)
                    {
                        break;
                    }
                    if (zcritical == lastz && zcritical < 0.001)
                    {
                        break;
                    }
                    lastz = zcritical;
                }
                while (!(zcritical < 0.00000001));
                if (i == 1)
                {
                    thetau = theta2;
                }
                else { thetal = theta2; }
            }
        }


        private static double x_lrptilde(double theta, double a, double b, double C)
        {
            double pest1 = (-b + Math.Sqrt(b * b - 4.0 * a * C)) / 2.0 / a;
            double pest2 = (-b - Math.Sqrt(b * b - 4.0 * a * C)) / 2.0 / a;
            if (pest1 > 1.0 || pest1 < 0.0)
                return pest2;
            if (pest2 > 1.0 || pest2 < 0.0)
                return pest1;
            if ((pest1 * theta) > 1.0 || (pest1 * theta) < 0.0)
                return pest2;
            return pest1;
        }


        private static double x_lrz(double thetahat, out double a, out double b, out double c, double N, double n0, double n1, double x0, double x1)
        {

            a = N * thetahat;
            b = -((x0 + n1) * thetahat + x1 + n0);
            c = x0 + x1;
            double p0tilde = x_lrptilde(thetahat, a, b, c);
            double p1tilde = p0tilde * thetahat;
            double q0tilde = 1.0 - p0tilde;
            double q1tilde = 1.0 - p1tilde;
            double utilde = (q0tilde / (n0 * p0tilde)) + (q1tilde / (n1 * p1tilde));
            double vtilde = 1.0 / utilde;
            return (((x1) - (n1 * p1tilde)) / q1tilde) / Math.Sqrt(vtilde);
        }


        private static double pfromlogit(double x)
        {
            double y = Formatting.SafeExp(-x);
            y = y != Constant.MISSING && y != 1.0 ? 1.0 / (1.0 + y) : Constant.MISSING;
            return y;
        }


        private static void x_lclass(double[] t, double[] fvl, double[] y, int N, out int tp, out int fp, out int fn, out int tn, double co)
        {
            // updated 15 Aug 2001
            tp = 0;
            fn = 0;
            fp = 0;
            tn = 0;
            //  a=true positives TP, b=false positives FP, c=false negatives FN, d=true negatives TN
            //  sens = a/(a+c), spec = d/(b+d)
            for (int j = 1; j <= N; j++)
            {
                double P = fvl[j] / t[j];
                int obsTot = Convert.ToInt32(t[j]);
                int obsPos = Convert.ToInt32(y[j]);
                int obsNeg = obsTot - obsPos;
                if (P >= co)
                {
                    tp = tp + obsPos;
                    fp = fp + obsNeg;
                }
                else
                {
                    tn = tn + obsNeg;
                    fn = fn + obsPos;
                }
            }
        }


        private static void x_lrdiff(double diff1, double diff2, out double theta1, out double theta0, double temptheta1, double temptheta2, out double z1, out double z0, double ztemp1, double ztemp2, out double zcritical)
        {
            if (diff1 < diff2)
            {
                theta1 = temptheta1;
                theta0 = temptheta2;
                z1 = ztemp1;
                z0 = ztemp2;
                zcritical = diff1;
            }
            else
            {
                theta0 = temptheta1;
                theta1 = temptheta2;
                z0 = ztemp1;
                z1 = ztemp2;
                zcritical = diff2;
            }
        }


        private static DataFrame x_prep_interlr(MultipleLinearRegressionContext context)
        {
            int iq = context.DoC ? 1 : 0;

            DataFrame frame = new DataFrame();
            StringVariable keyVariable = new StringVariable { Title = "Name" };
            frame.Variables.Add(keyVariable);
            StringVariable valueVariable = new StringVariable { Title = "Value" };
            frame.Variables.Add(valueVariable);
            DoubleVariable oldValueVariable = new DoubleVariable { Title = "Old value" };
            frame.Variables.Add(oldValueVariable);

            for (int j = 1; j <= context.P - iq; j++)
            {
                double mu = 0.0;
                double a = context.X[1, j];
                double b = Constant.MISSING;
                bool bin = true;
                for (int i = 1; i <= context.N; i++)
                {
                    double z = context.X[i, j];
                    if (z != Constant.MISSING)
                    {
                        mu = mu + z;
                        if (z != a)
                        {
                            if (z != b)
                            {
                                if (b == Constant.MISSING)
                                {
                                    b = z;
                                }
                                else
                                {
                                    bin = false;
                                }
                            }
                        }
                    }
                }
                if (bin)
                {
                    string t = context.Labels[j];
                    int px = t.IndexOf("(", StringComparison.Ordinal) + 1;
                    if (px == 0)
                    {
                        mu = 0.5;
                    }
                    else
                    {
                        //  Deal with multiple predictors with the same name - stored as name(...
                        int k = 1;
                        t = t.Substring(0, px - 1);
                        for (int i = 1; i < context.Labels.Length; i++)
                        {
                            if (null != context.Labels[i] && context.Labels[i].Contains(t))
                            {
                                k++;
                            }
                        }
                        mu = 1.0 / Convert.ToDouble(k);
                    }
                }
                else
                {
                    mu = mu / Convert.ToDouble(context.N);
                }
                keyVariable.SetData(j - 1, context.Labels[j]);
                valueVariable.SetData(j - 1, mu.ToString());
                oldValueVariable.SetData(j - 1, Parsing.Cdbl_Txt(mu.ToString()));
            }
            return frame;
        }

        public static ParameterBag RptPoissonRegression(ITemplateHost host, ParameterBag parameters)
        {
            //  Dim PASSX(4, 1) As Double ' (1, 1) = calc intercept (1 = yes); (2, 1) = accuracy; (3, 1) = weights (1 = yes); (4, 1) = ptime (1 = yes)
            double tol = Parsing.Cdbl_Txt(parameters["accuracy"].AsString);
            if (tol > 0.01)
                tol = 0.01;

            bool ptime = "true".Equals(parameters["has-exposure"].AsString);
            bool weighted = parameters["weights"].AsBoolean;
            bool intercept = parameters["intercept"].AsBoolean;

            DataFrame responseFrame = parameters["response"].AsDataFrame;
            DoubleVariable responseVariable = responseFrame.Variables[0]as DoubleVariable;
            int rows = responseVariable.Length;
            double[] y = new double[rows + 1];
            double[] t = new double[rows + 1];
            double[] weight = new double[rows + 1];
            for (int c = 1; c <= rows; c++)
                y[c] = responseVariable.Data[c - 1];

            if (ptime)
            {
                DataFrame exposureFrame = parameters["exposure"].AsDataFrame;
                DoubleVariable exposureVariable = exposureFrame.Variables[0]as DoubleVariable;
                for (int c = 1; c <= rows; c++)
                    t[c] = exposureVariable.Data[c - 1];
            }

            if (weighted)
            {
                DataFrame weightFrame = parameters["weight"].AsDataFrame;
                DoubleVariable weightVariable = weightFrame.Variables[0]as DoubleVariable;
                for (int c = 1; c <= rows; c++)
                    weight[c] = weightVariable.Data[c - 1];
            }

            DataFrame predictorsFrame = parameters["predictors"].AsDataFrame;
            // Store the predictors
            int prd = predictorsFrame.VariableCount;
            double[,] x = new double[rows + 1, prd + 1];
            for (int c = 1; c <= prd; c++)
            {
                DoubleVariable v = predictorsFrame.Variables[c - 1]as DoubleVariable;
                for (int r = 1; r <= rows; r++)
                {
                    x[r, c] = v.Data[r - 1];
                    if (x[r, c] == Constant.MISSING)
                        weight[r] = Constant.MISSING;
                }
            }

            // is n<p
            if (prd + 1 >= rows)
            {
                host.Error("You must have more observations than parameters", "Poisson regression");
                throw new TemplateOperationCancelledException();
            }
            //  Copy down the remaining observations
            int targetRow = 1;
            for (int sourceRow = 1; sourceRow <= rows; sourceRow++)
            {
                bool OK = y[sourceRow] != Constant.MISSING;
                if (t[sourceRow] == Constant.MISSING)
                    OK = false;
                if (weighted && weight[sourceRow] == Constant.MISSING)
                    OK = false;
                if (ptime && t[sourceRow] <= 0.0)
                    OK = false;
                for (int pred = 1; pred <= prd; pred++)
                {
                    if (x[sourceRow, pred] == Constant.MISSING)
                        OK = false;
                }
                if (OK)
                {
                    //  This row is valid; if necessary, copy it down to our current target row
                    if (sourceRow != targetRow)
                    {
                        y[targetRow] = y[sourceRow];
                        if (ptime)
                            t[targetRow] = t[sourceRow];
                        else
                            t[targetRow] = 1.0;
                        if (weighted)
                            weight[targetRow] = weight[sourceRow];
                        for (int pred = 1; pred <= prd; pred++)
                            x[targetRow, pred] = x[sourceRow, pred];
                    }
                    targetRow++;
                }
            }
            //  At this point, y, pt, wt and x contain valid data from row 1 to row targetrow - 1 inclusive
            // int ctr = 0; 
            string warn;
            const int maxit = 200;
            int rank = 0; int df = 0;
            double deviance = 0; double devx; double llx;
            int fault;
            int records = rows;
            int[] rxi = new int[1 + 1 ];
            int predictors = prd;
            int p = predictors;
            bool mean = intercept;
            if (mean)
                p++;
            string[] labels = new string[p + 1 ];
            labels[0] = responseVariable.Title;
            for (int j = 1; j <= prd; j++)
                labels[j] = predictorsFrame.Variables[j - 1].Title;
            if (records != targetRow - 1)
            {
                x_dropper(host, records - (targetRow - 1), "Poisson regression");
                records = targetRow - 1;
            }
            bool use_offset = ptime;
            //  get intercept deviance - drop predictors
            double[,] x2 = new double[records + 1, 1 + 1];
            for (int j = 1; j <= records; j++)
            {
                x2[j, 0] = 1;
                x2[j, 1] = 1;
            }
            bool[] selectX = new bool[1 + 1];
            selectX[1] = true;
            double[] beta = new double[1 + 1];
            double[] se_beta = new double[records + 1];
            double[] covariance = new double[1 + 1];
            double[] fit = new double[records + 1];
            double[] residual = new double[records + 1 ];
            double[] leverage = new double[records + 1];
            double[] offset = new double[records + 1 ];
            //  use offset of log(exposure) if exposure specified
            if (use_offset)
            {
                for (int j = 1; j <= records; j++)
                    offset[j] = Math.Log(t[j]);
            }
            string dropped = string.Empty;
            string err_msg = string.Empty;
            Regress1.X_Poisson_Regression(false, use_offset, ref weighted, records, x2, 1, selectX, 1, y, t, weight, ref deviance, ref df, beta, ref rank, se_beta, covariance, tol, maxit, fit, residual, leverage, offset, out fault, ref dropped, ref err_msg);
            int idfx = df;
            if (!mean)
            {
                llx = Constant.MISSING;
                devx = Constant.MISSING;
            }
            else
            {
                llx = x_loglik_p(weighted, records, weight, y, fit);
                devx = deviance;
            }
            //  calculate full model
            selectX = new bool[p + 1];
            for (int j = 1; j <= p; j++)
                selectX[j] = true;
            beta = new double[p + 1];
            se_beta = new double[records + 1];
            covariance = new double[((int)(Math.Floor((double)p * (p + 1) / 2))) + 1];
            fit = new double[records + 1 ];
            residual = new double[records + 1];
            leverage = new double[records + 1];
            Regress1.X_Poisson_Regression(mean, use_offset, ref weighted, records, x, predictors, selectX, p, y, t, weight, ref deviance, ref df, beta, ref rank, se_beta, covariance, tol, maxit, fit, residual, leverage, offset, out fault, ref dropped, ref err_msg);
            if (fault != 0 && fault != 3)
            {
                host.Error(err_msg, "Poisson regression");
                throw new TemplateOperationCancelledException();
            }
            ParameterBag outputParameters = new ParameterBag();
            if (fault == 3 && err_msg.Length> 0)
                warn = Formatting.WRNCOLON + err_msg;
            else
                warn = string.Empty;
            if (rank != p)
            {
                if (warn.Length > 0)
                    warn += Formatting.RTFCRLF;
                warn += Formatting.WRNCOLON + "result not of full rank, there is more than one solution for the model." + Formatting.RTFCRLF + "Look for correlated predictor variables that you might drop: the mutliple linear regression function does this automatically.";
            }
            if (df <= 0)
            {
                if (warn.Length > 0)
                    warn = warn + Formatting.RTFCRLF;
                warn += Formatting.WRNCOLON + "saturated model (all degrees of freedom used, can't assess goodness of fit)";
            }
            if (dropped.Length > 0)
            {
                if (warn.Length > 0)
                    warn += Formatting.RTFCRLF + dropped;
                else
                    warn = dropped;
            }
            if (warn.Length > 0)
            {
                IList<ParameterBag> warnList = new List<ParameterBag>();
                outputParameters.AddOutput("*warn", warnList);
                ParameterBag warnParameters = new ParameterBag();
                warnList.Add(warnParameters);
                warnParameters.AddOutput("warn", warn);
            }
            else
            {
                outputParameters.AddOutput("*warn", null);
            }

            double GAMMA = parameters["gamma"].AsDouble;
            double cit; double P0;
            MathDbl.civ(0, out cit, GAMMA, out P0);
            outputParameters.AddOutput("pc", Formatting.XRound(100 * (1.0 - P0), 2));

            outputParameters.AddOutput("dev", host.RoundU(deviance));
            outputParameters.AddOutput("df_dev", df.ToString());
            double prob = PDF.chivalp(deviance, Convert.ToDouble(df));
            outputParameters.AddOutput("p_dev", host.pval(prob));
            warn = prob < 0.05 ? Formatting.ASTERISK : string.Empty;
            outputParameters.AddOutput("w", warn);
            double x2dev = devx - deviance;
            if (x2dev < 0.0)
                x2dev = Constant.MISSING;
            outputParameters.AddOutput("x2", host.RoundU(x2dev));
            outputParameters.AddOutput("df_x2", (idfx - df).ToString());
            outputParameters.AddOutput("p_x2",
                                       df > 0
                                           ? host.pval(PDF.chivalp(x2dev, Convert.ToDouble(idfx - df)))
                                           : Formatting.ASTERISK);
            IList<ParameterBag> predList = new List<ParameterBag>();
            outputParameters.AddOutput("*pred", predList);
            for (int i = 1; i <= p; i++)
            {
                if (se_beta[i] == 0)
                    prob = Constant.MISSING;
                else
                    prob = 2.0 * (1.0 - PDF.alnorm(Math.Abs(beta[i] / se_beta[i])));
                ParameterBag predParameters = new ParameterBag();
                predList.Add(predParameters);
                if (mean)
                {
                    predParameters.AddOutput("lab", i == 1 ? "Intercept" : labels[i - 1]);
                    predParameters.AddOutput("idx", (i - 1).ToString());
                }
                else
                {
                    predParameters.AddOutput("lab", labels[i]);
                    predParameters.AddOutput("idx", i.ToString());
                }
                predParameters.AddOutput("res", host.RoundU(beta[i]));
                double z;
                if (se_beta[i] == 0.0)
                {
                    z = Constant.MISSING;
                    predParameters.AddOutput("z", host.RoundU(z));
                    predParameters.AddOutput("p", "* error: drop this variable *");
                }
                else
                {
                    z = beta[i] / se_beta[i];
                    predParameters.AddOutput("z", host.RoundU(z));
                    predParameters.AddOutput("p", host.pval(prob));
                }

                double rr = Formatting.SafeExp(beta[i]);
                double lci;
                double uci;
                if (rr != Constant.MISSING)
                {
                    lci = Formatting.SafeExp(beta[i] - se_beta[i] * cit);
                    uci = Formatting.SafeExp(beta[i] + se_beta[i] * cit);
                }
                else
                {
                    lci = Constant.MISSING;
                    uci = Constant.MISSING;
                }
                predParameters.AddOutput("irr", host.RoundU(rr));
                predParameters.AddOutput("ci", host.RoundU(lci) + "  to  " + host.RoundU(uci));
            }
            string tx = "log ";
            if (labels[0].Length > 0)
                tx += labels[0];
            else 
                tx += "Y";
            if (use_offset)
            {
                if (labels[1].Length > 0)
                    tx += " [offset log(" + labels[1] + ")]";
                else
                    tx = tx + " [offset log(exposure)]";
            }
            tx += " = ";
            for (int j = 1; j <= p; j++)
            {
                if (j > 1 & beta[j] >= 0.0)
                    tx += "+";
                tx += host.RoundU(beta[j]);
                string Q = mean ? (j > 1 ? labels[j - 1] : " ") : labels[j];
                tx += Q.Length == 0 ? " X" + j.ToString() : " " + Q + " ";
            }
            outputParameters.AddOutput("logit", tx);

            MultipleLinearRegressionContext context = new MultipleLinearRegressionContext
            {
                B = beta,
                Covariance = covariance,
                DEV = deviance,
                DEVX = devx,
                DF = df,
                DFX = idfx,
                DoC = mean,
                M = predictors,
                FV = fit,
                H1 = leverage,
                Labels = labels,
                LLX = llx,
                N = records,
                P = p,
                R = residual,
                RANK = rank,
                RXI = rxi,
                SE = se_beta,
                T = t,
                TOL = tol,
                WEIGHT = weighted,
                WT = weight,
                X = x,
                Y = y
            };
            outputParameters.Add("context", new FilledParameter(FilledParameterDirection.Input, context));
            return outputParameters;
        }

        public static ParameterBag RptPoissonRegressionFit(ITemplateHost host, ParameterBag parameters)
        {
            MultipleLinearRegressionContext context = ((MultipleLinearRegressionContext)(parameters["context"].Data));
            double[] t = context.T;
            double[] y = context.Y;
            double[] fvl = context.FV;
            double[] dr = context.R;
            double[] hi = context.H1;
            int nx = context.N;
            bool DoC = context.DoC;
            string[] labels = context.Labels;
            int P = context.P;
            double[] covariance = context.Covariance;
            double[] wt = context.WT;
            bool Weight = context.WEIGHT;
            double[,] x = context.X;

            double ww;

            ParameterBag outputParameters = new ParameterBag();
            List<ParameterBag> predictorsList = new List<ParameterBag>();
            outputParameters.AddOutput("*predictors", predictorsList);
            for (int i = 1; i <= labels.Length - 2; i++)
            {
                ParameterBag predictorsParameters = new ParameterBag();
                predictorsList.Add(predictorsParameters);
                predictorsParameters.AddOutput("lab", labels[i]);
            }
            List<ParameterBag> predictorValuesList = new List<ParameterBag>();
            outputParameters.AddOutput("*predictorValues", predictorValuesList);
            for (int j = 1; j <= nx; j++)
            {
                ParameterBag predictorValuesParameters = new ParameterBag();
                predictorValuesList.Add(predictorValuesParameters);
                predictorValuesParameters.AddOutput("idx", j);
                List<ParameterBag> predictorsList2 = new List<ParameterBag>();
                predictorValuesParameters.AddOutput("*pred", predictorsList2);
                for (int i = 1; i <= labels.Length - 2; i++)
                {
                    ParameterBag predictorsParameters = new ParameterBag();
                    predictorsList2.Add(predictorsParameters);
                    predictorsParameters.AddOutput("val", x[j, i].ToString());
                }
            }

            IList<ParameterBag> eventsList = new List<ParameterBag>();
            outputParameters.AddOutput("*events", eventsList);
            for (int i = 1; i <= nx; i++)
            {
                ParameterBag eventsParameters = new ParameterBag();
                eventsList.Add(eventsParameters);
                eventsParameters.AddOutput("idx", i.ToString());
                eventsParameters.AddOutput("obs", host.RoundU(y[i]));
                eventsParameters.AddOutput("fit", host.RoundU(fvl[i]));
                double ry = t[i] != 0.0 ? y[i] / t[i] : Constant.MISSING;
                eventsParameters.AddOutput("iro", host.RoundU(ry));
                ry = t[i] != 0.0 ? fvl[i] / t[i] : Constant.MISSING;
                eventsParameters.AddOutput("ire", host.RoundU(ry));
            }

            IList<ParameterBag> residualsList = new List<ParameterBag>();
            outputParameters.AddOutput("*residuals", residualsList);
            for (int i = 1; i <= nx; i++)
            {
                ParameterBag residualsParameters = new ParameterBag();
                residualsList.Add(residualsParameters);
                residualsParameters.AddOutput("idx", i.ToString());
                residualsParameters.AddOutput("yr", host.RoundU(y[i] - fvl[i]));
                ww = Weight ? wt[i] : 1.0;
                double ftr;
                if (fvl[i] <= 0.0)
                {
                    ftr = Constant.MISSING;
                }
                else { ftr = Math.Sqrt(y[i]) * Math.Sqrt(ww) + Math.Sqrt(y[i] + 1.0) * Math.Sqrt(ww) - Math.Sqrt(4.0 * fvl[i] + 1.0) * Math.Sqrt(ww); }
                residualsParameters.AddOutput("ftr", host.RoundU(ftr));
                residualsParameters.AddOutput("dev", host.RoundU(dr[i]));
            }

            IList<ParameterBag> pearsonList = new List<ParameterBag>();
            outputParameters.AddOutput("*pearson", pearsonList);
            for (int i = 1; i <= nx; i++)
            {
                ParameterBag pearsonParameters = new ParameterBag();
                pearsonList.Add(pearsonParameters);
                pearsonParameters.AddOutput("idx", i.ToString());
                ww = Weight ? wt[i] : 1;
                double xi = fvl[i] == 0.0 ? Constant.MISSING : (Math.Pow((y[i] - fvl[i]), 2.0)) / fvl[i];
                pearsonParameters.AddOutput("res", host.RoundU(xi));
                pearsonParameters.AddOutput("lev", host.RoundU(hi[i]));
                // IEB July 2009
                double xis;
                if (fvl[i] == 0.0)
                {
                    // xi = Constant.MISSING; never used
                    xis = Constant.MISSING;
                }
                else
                {
                    xi = ((y[i] - fvl[i]) * Math.Sqrt(ww)) / Math.Sqrt(fvl[i]);
                    if (1.0 - hi[i] > 0.0)
                    {
                        xis = xi / Math.Sqrt(1.0 - hi[i]);
                    }
                    else { xis = Constant.MISSING; }
                }
                pearsonParameters.AddOutput("sres", host.RoundU(xis));
            }

            IList<ParameterBag> covarList = new List<ParameterBag>();
            outputParameters.AddOutput("*covar", covarList);
            for (int i = 1; i <= P; i++)
            {
                for (int j = i; j <= P; j++)
                {
                    ParameterBag covarParameters = new ParameterBag();
                    covarList.Add(covarParameters);
                    string x1 = qlbli(labels, i, DoC);
                    string x2 = qlbli(labels, j, DoC);
                    covarParameters.AddOutput("lab", x1 + " vs. " + x2);
                    covarParameters.AddOutput("cov", host.RoundU(covariance[(((int)(Math.Floor((double)j * (j - 1) / 2 + i))))]));
                }
            }
            return outputParameters;
        }


        public static ParameterBag GridPoissonRegressionFit(ITemplateHost host, ParameterBag parameters)
        {
            MultipleLinearRegressionContext context = ((MultipleLinearRegressionContext)(parameters["context"].Data));
            double[] t = context.T;
            double[] y = context.Y;
            double[] fvl = context.FV;
            double[] dr = context.R;
            double[] hi = context.H1;
            int nx = context.N;
            double[] wt = context.WT;
            bool Weight = context.WEIGHT;
            bool expectedEvents = parameters["expectedEvents"].AsBoolean;
            bool expectedIncidence = parameters["expectedIncidence"].AsBoolean;
            bool residualEvents = parameters["residualEvents"].AsBoolean;
            bool freemanTukeyResidual = parameters["freemanTukeyResidual"].AsBoolean;
            bool devianceResidual = parameters["devianceResidual"].AsBoolean;
            bool pearsonResidual = parameters["pearsonResidual"].AsBoolean;
            bool leverage = parameters["leverage"].AsBoolean;
            bool stdPearsonResidual = parameters["stdPearsonResidual"].AsBoolean;
            ParameterBag outputParameters = new ParameterBag();
            DataFrame resultsFrame = new DataFrame();
            if (expectedEvents || expectedIncidence || residualEvents || freemanTukeyResidual || devianceResidual || pearsonResidual || leverage || stdPearsonResidual)
            {
                //  Make up all the variables (it's fast!) then only include the ones we need
                DoubleVariable expectedEventsVariable = new DoubleVariable(nx, "Expected events");
                DoubleVariable expectedIncidenceVariable = new DoubleVariable(nx, "Expected incidence");
                DoubleVariable residualEventsVariable = new DoubleVariable(nx, "Residual events");
                DoubleVariable freemanTukeyResidualVariable = new DoubleVariable(nx, "Freeman-Tukey residual");
                DoubleVariable devianceResidualVariable = new DoubleVariable(nx, "Deviance Residual");
                DoubleVariable pearsonResidualVariable = new DoubleVariable(nx, "Pearson Residual");
                DoubleVariable leverageVariable = new DoubleVariable(nx, "Leverage");
                DoubleVariable stdPearsonResidualVariable = new DoubleVariable(nx, "Std Pearson Residual");
                for (int i = 1; i <= nx; i++)
                {
                    // Check for each save option
                    if (expectedEvents)
                    { // Events

                        expectedEventsVariable.SetData(i - 1, fvl[i]);
                    }
                    if (expectedIncidence)
                    {
                        // Incidence

                        double ry = t[i] != 0.0 ? fvl[i] / t[i] : Constant.MISSING;
                        expectedIncidenceVariable.SetData(i - 1, ry);
                    }
                    if (residualEvents)
                    { // Residual

                        residualEventsVariable.SetData(i - 1, y[i] - fvl[i]);
                    }
                    double ww;
                    if (freemanTukeyResidual)
                    { // Freeman-Tukey

                        ww = Weight ? wt[i] : 1.0;
                        double ftr = fvl[i] <= 0.0
                                         ? Constant.MISSING
                                         : Math.Sqrt(y[i]) * Math.Sqrt(ww) + Math.Sqrt(y[i] + 1.0) * Math.Sqrt(ww) -
                                           Math.Sqrt(4.0 * fvl[i] + 1.0) * Math.Sqrt(ww);
                        freemanTukeyResidualVariable.SetData(i - 1, ftr);
                    }
                    if (devianceResidual)
                    { // Deviance residuals

                        devianceResidualVariable.SetData(i - 1, dr[i]);
                    }
                    ww = Weight ? wt[i] : 1.0;
                    double xi = fvl[i] == 0.0 ? Constant.MISSING : (Math.Pow((y[i] - fvl[i]), 2.0)) / fvl[i];
                    if (pearsonResidual)
                    { // Pearson chi-square residuals

                        pearsonResidualVariable.SetData(i - 1, xi);
                    }
                    if (leverage)
                    { // Leverage HI

                        leverageVariable.SetData(i - 1, hi[i]);
                    }
                    if (stdPearsonResidual)
                    { // Standardised Pearson residual

                        xi = ((y[i] - fvl[i]) * Math.Sqrt(ww)) / Math.Sqrt(fvl[i]);
                        double xis = 1.0 - hi[i] > 0.0 ? xi / Math.Sqrt(1.0 - hi[i]) : Constant.MISSING;
                        stdPearsonResidualVariable.SetData(i - 1, xis);
                    }
                }
                if (expectedEvents)
                {
                    resultsFrame.Variables.Add(expectedEventsVariable);
                }
                if (expectedIncidence)
                {
                    resultsFrame.Variables.Add(expectedIncidenceVariable);
                }
                if (residualEvents)
                {
                    resultsFrame.Variables.Add(residualEventsVariable);
                }
                if (freemanTukeyResidual)
                {
                    resultsFrame.Variables.Add(freemanTukeyResidualVariable);
                }
                if (devianceResidual)
                { // Deviance residuals

                    resultsFrame.Variables.Add(devianceResidualVariable);
                }
                if (pearsonResidual)
                { // Pearson residuals

                    resultsFrame.Variables.Add(pearsonResidualVariable);
                }
                if (leverage)
                { // Leverage HI

                    resultsFrame.Variables.Add(leverageVariable);
                }
                if (stdPearsonResidual)
                { // Standardised Pearson residual

                    resultsFrame.Variables.Add(stdPearsonResidualVariable);
                }
            }
            outputParameters.AddOutput("results", resultsFrame);
            return outputParameters;
        }


        public static ParameterBag RptPoissonRegressionResiduals(ITemplateHost host, ParameterBag parameters)
        {
            MultipleLinearRegressionContext context = ((MultipleLinearRegressionContext)(parameters["context"].Data));
            int nx = context.N;
            double[] yfit = context.FV;
            double[] dr = context.R;
            double[,] xd = context.X;
            string[] labels = context.Labels;
            int P = context.P;
            bool Intercept = context.DoC;

            ParameterBag outputParameters = new ParameterBag();
            IList<ParameterBag> chartList = new List<ParameterBag>();
            outputParameters.AddOutput("*chart", chartList);

            double[] r = new double[nx + 1 ];
            r[0] = Constant.MISSING; //  Avoid drawing a point at (0,0)
            for (int j = 1; j <= nx; j++)
            {
                r[j] = Math.Abs(dr[j]);
            }
            using (ChartRenderer ch = (ChartRenderer)ChartRendererFactory.ChartRendererFor(ChartDefinition.Empty()))
            {
                ParameterBag chartParameters = new ParameterBag();
                chartList.Add(chartParameters);
                chartParameters.AddOutput("chart", ch.PlotXYAndReturnRtf(host, yfit, r, "Fitted Y", "Abs(Deviance residual)", "Absolute Residuals vs. Fitted Y [Poisson regression]", false, 0, false));
            }
            for (int i = 1; i <= P; i++)
            {
                if (i > 1 || !Intercept)
                {
                    bool OK = false;
                    int k = Intercept ? i - 1 : i;
                    r = new double[nx + 1 ];
                    for (int j = 1; j <= nx; j++)
                    {
                        r[j] = xd[j, k];
                        if (!OK)
                            OK = r[j] != 1.0 && r[j] != 0.0 && r[j] != -1.0;
                    }
                    if (OK)
                    {
                        using (ChartRenderer ch = (ChartRenderer)ChartRendererFactory.ChartRendererFor(ChartDefinition.Empty()))
                        {
                            ParameterBag chartParameters = new ParameterBag();
                            chartList.Add(chartParameters);
                            chartParameters.AddOutput("chart", ch.PlotXYAndReturnRtf(host, r, dr, "Predictor: " + labels[k], "Deviance residual", "Residuals vs. Predictor " + k.ToString() + " [Poisson regression]", true, 0, false));
                        }
                    }
                }
            }
            return outputParameters;
        }

        public enum ProbitModel
        {
            Probit = 1,
            Logit = 2,
        }

        public static ParameterBag RptProbit(ITemplateHost host, ParameterBag parameters)
        {
            return RptProbitOrLogit(host, parameters, ProbitModel.Probit);
        }

        public static ParameterBag RptLogit(ITemplateHost host, ParameterBag parameters)
        {
            return RptProbitOrLogit(host, parameters, ProbitModel.Logit);
        }

        public static ParameterBag ProbitOrLogitDataHasControls(ITemplateHost host, ParameterBag parameters)
        {
            DataFrame doseFrame = parameters["dose"].AsDataFrame;
            DoubleVariable doseVariable = doseFrame.Variables[0]as DoubleVariable;

            DataFrame subjectsFrame = parameters["subjects"].AsDataFrame;
            DoubleVariable subjectsVariable = subjectsFrame.Variables[0]as DoubleVariable;

            DataFrame respondersFrame = parameters["responders"].AsDataFrame;
            DoubleVariable respondersVariable = respondersFrame.Variables[0]as DoubleVariable;

            ParameterBag outputParameters = new ParameterBag();
            for (int n = 0; n < doseVariable.Length; n++)
            {
                if (doseVariable.Data[n] == 0.0 && subjectsVariable.Data[n] != Constant.MISSING && respondersVariable.Data[n] != Constant.MISSING)
                {
                    outputParameters.AddInput("dataHasControls", true);
                    outputParameters.AddInput("nc", Convert.ToInt32(subjectsVariable.Data[n]));
                    outputParameters.AddInput("nrc", Convert.ToInt32(respondersVariable.Data[n]));
                    break;
                }
            }
            return outputParameters;
        }

        private static ParameterBag RptProbitOrLogit(ITemplateHost host, ParameterBag parameters, ProbitModel model)
        {
            DataFrame doseFrame = parameters["dose"].AsDataFrame;
            DoubleVariable doseVariable = doseFrame.Variables[0]as DoubleVariable;
            ColumnData[] cd = new ColumnData[3];
            cd[0] = new ColumnData { Title = doseVariable.Title };

            int rows = doseVariable.Length;
            double[] dv = new double[rows + 1];
            for (int row = 1; row <= rows; row++)
                dv[row] = doseVariable.Data[row - 1];

            DataFrame subjectsFrame = parameters["subjects"].AsDataFrame;
            DoubleVariable subjectsVariable = subjectsFrame.Variables[0]as DoubleVariable;
            cd[1] = new ColumnData { Title = subjectsVariable.Title };
            double[] sv = new double[rows + 1];
            for (int row = 1; row <= rows; row++)
                sv[row] = subjectsVariable.Data[row - 1];

            DataFrame respondersFrame = parameters["responders"].AsDataFrame;
            DoubleVariable respondersVariable = respondersFrame.Variables[0]as DoubleVariable;
            cd[2] = new ColumnData { Title = respondersVariable.Title };
            // Store the Responders Data
            double[] rv = new double[rows + 1];
            for (int row = 1; row <= rows; row++)
                rv[row] = respondersVariable.Data[row - 1];

            double ici = parameters["conf"].AsDouble;
            bool clog = parameters["calc-log10-doses"].AsBoolean;

            int ifa = 0;
            int laps = 0;
            int nc = 0; int nrc = 0; int icount = 0; int ifault; int ifault2 = 0;
            double t; double seh = 0; double se = 0;
            double a = 0; double b = 0; double varb = 0; double sw = 0; double S1 = 0; double S2 = 0; double s3 = 0; double cse = 0; double cseh = 0;
            double I1 = 0; double XM = 0;
            double C2 = 0;
            double dose50 = 0; double doseq = 0; double nohetllm = 0;
            double nohetulm = 0; double hetllm = 0; double hetulm = 0; double nohetllq = 0; double nohetulq = 0; double hetllq = 0; double hetulq = 0; double TM = 0; double ym = 0; double del = 0; double s4 = 0; double s6 = 0;

            ParameterBag outputParameters = new ParameterBag();
            int nx = 0;

            int k = rows;
            double C1 = 0.0;
            double[] d = new double[k + 1];
            double[] s = new double[k + 1];
            double[] r = new double[k + 1];
            for (int n = 1; n <= k; n++)
            {
                if (dv[n] != Constant.MISSING && sv[n] != Constant.MISSING && rv[n] != Constant.MISSING)
                {
                    if (dv[n] == 0 && C1 == 0.0)
                    {
                        nc = Convert.ToInt32(sv[n]);
                        nrc = Convert.ToInt32(rv[n]);
                        C1 = -1.0;
                    }
                    else
                    {
                        nx++;
                        d[nx] = dv[n];
                        s[nx] = sv[n];
                        r[nx] = rv[n];
                    }
                }
            }
            k = nx;
            // create temp variable for copying values 
            double[] transTemp14 = new double[k + 1];
            Array.Copy(d, transTemp14, Math.Min(d.Length, transTemp14.Length));
            d = transTemp14;
            // create temp variable for copying values 
            double[] transTemp15 = new double[k + 1 ];
            Array.Copy(s, transTemp15, Math.Min(s.Length, transTemp15.Length));
            s = transTemp15;
            // create temp variable for copying values 
            double[] transTemp16 = new double[k + 1];
            Array.Copy(r, transTemp16, Math.Min(r.Length, transTemp16.Length));
            r = transTemp16;
            if (C1 == 0.0)
            {
                nc = parameters["nc"].AsInt32;
                if (nc > 0)
                {
                    nrc = parameters["nrc"].AsInt32;
                    C1 = -1.0;
                }
            }
            double qld = parameters["qld"].AsDouble;
            if (qld >= 100.0 || qld <= 0.0)
                qld = 90.0;

            double[] P = new double[k + 1];
            double[] w = new double[k + 1];
            double[] y = new double[k + 1];
            double[] pob = new double[k + 1];
            double[] x = new double[k + 1];
            x_sortbydose(d, s, r, k);
            double c = 0;
            x_probits(model, k, ref C1, ref c, ref nc, nrc, clog, qld, d, s, r, P, w, y, pob, x, ref a, ref b, ref laps, ref S1, ref S2, ref s3, ref s4, ref s6, ref del, ref TM, ref XM, ref ym, ref sw, ref icount, out ifault);
            if (ifault == 0)
                x_qdcl(model, k, out C2, ici, clog, qld, ref dose50, ref doseq, a, b, ref nohetllm, ref nohetulm, ref hetllm, ref hetulm, ref nohetllq, ref nohetulq, ref hetllq, ref hetulq, ref c, out se, ref cse, out seh, ref cseh, ref I1, ref S1, ref S2, ref s3, s4, s6, del, TM, XM, sw, out varb, icount, out ifault2);
            outputParameters.AddOutput("title", model == ProbitModel.Probit ? "probit sigmoid curve" : "logit sigmoid curve");
            x_profolt(ifault, host);
            if (ifault != 0)
                throw new TemplateOperationCancelledException();

            outputParameters.AddOutput("a", host.RoundU(a));
            outputParameters.AddOutput("b", host.RoundU(b));
            x_profolt(ifault2, host);
            if (ifault != 0)
            {
                throw new TemplateOperationCancelledException();
            }
            double hetp = PDF.chivalp(c, I1);
            outputParameters.AddOutput("mxd", host.RoundU(dose50));
            if (hetp < 0.05)
            {
                outputParameters.AddOutput("het", "(Heterogeneity)");
                outputParameters.AddOutput("from", host.RoundU(hetllm));
                outputParameters.AddOutput("to", host.RoundU(hetulm));
            }
            else
            {
                outputParameters.AddOutput("het", "(No Heterogeneity)");
                outputParameters.AddOutput("from", host.RoundU(nohetllm));
                outputParameters.AddOutput("to", host.RoundU(nohetulm));
            }
            outputParameters.AddOutput("centile", host.RoundU(qld));
            outputParameters.AddOutput("doseq", host.RoundU(doseq));
            if (hetp < 0.05)
            {
                outputParameters.AddOutput("het_cent", "(Heterogeneity)");
                outputParameters.AddOutput("from_cent", host.RoundU(hetllq));
                outputParameters.AddOutput("to_cent", host.RoundU(hetulq));
            }
            else
            {
                outputParameters.AddOutput("het_cent", "(No Heterogeneity)");
                outputParameters.AddOutput("from_cent", host.RoundU(nohetllq));
                outputParameters.AddOutput("to_cent", host.RoundU(nohetulq));
            }
            outputParameters.AddOutput("dev", host.RoundU(c));
            outputParameters.AddOutput("df", I1.ToString());
            outputParameters.AddOutput("p", host.pval(hetp));
            if (hetp < 0.05)
                t = b / seh;
            else
                t = b / se;
            outputParameters.AddOutput("t_slope", host.RoundU(t));
            outputParameters.AddOutput("df_slope", I1.ToString());
            double tp = PDF.tvalp(t, I1);
            if (tp > 1.0 - tp)
                tp = 1.0 - tp;
            tp = 2.0 * tp;
            outputParameters.AddOutput("p_slope", host.pval(tp));
            if (tp > 0.05)
            {
                IList<ParameterBag> warnList = new List<ParameterBag>();
                warnList.Add(new ParameterBag());
                outputParameters.AddOutput("*warn", warnList);
            }
            else
            {
                outputParameters.AddOutput("*warn", null);
            }
            MultipleLinearRegressionContext context = new MultipleLinearRegressionContext { ARG = new double[18 + 1] };

            context.ARG[1] = a;
            context.ARG[2] = b;
            context.ARG[3] = t;
            context.ARG[4] = varb;
            context.ARG[5] = sw;
            context.ARG[6] = S1;
            context.ARG[7] = S2;
            context.ARG[8] = s3;
            context.ARG[9] = seh;
            context.ARG[10] = se;
            context.ARG[11] = cse;
            context.ARG[12] = cseh;
            context.ARG[13] = hetp;
            context.ARG[14] = I1;
            context.ARG[15] = XM;
            context.ARG[16] = ici;
            context.ARG[17] = C1;
            context.ARG[18] = C2;
            context.M = (int)model;
            context.X1 = d;
            context.Y = s;
            context.R = r;
            context.H1 = P;
            context.N = ifa;
            context.DoC = clog;
            context.P = laps;
            context.DF = k;
            context.DV = dv;
            context.SV = sv;
            context.RV = rv;
            context.Labels = new string[3];
            context.Labels[0] = doseVariable.Title;
            context.Labels[1] = subjectsVariable.Title;
            context.Labels[2] = respondersVariable.Title;
            outputParameters.Add("context", new FilledParameter(FilledParameterDirection.Input, context));
            return outputParameters;
        }


        private static void x_sortbydose(double[] d, double[] s, double[] r, int k)
        {
            Tri[] swp = new Tri[k + 1];
            for (int i = 1; i <= k; i++)
            {
                swp[i].d = d[i];
                swp[i].s = s[i];
                swp[i].r = r[i];
            }
            Array.Sort(swp, 1, k, new TriByDAscending());
            for (int i = 1; i <= k; i++)
            {
                d[i] = swp[i].d;
                s[i] = swp[i].s;
                r[i] = swp[i].r;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="model"></param>
        /// <param name="k"></param>
        /// <param name="C1">Experimental value of natural mortality</param>
        /// <param name="C"></param>
        /// <param name="nc"></param>
        /// <param name="nrc"></param>
        /// <param name="clog"></param>
        /// <param name="qld"></param>
        /// <param name="D"></param>
        /// <param name="s"></param>
        /// <param name="r"></param>
        /// <param name="P"></param>
        /// <param name="w"></param>
        /// <param name="y"></param>
        /// <param name="pob"></param>
        /// <param name="x"></param>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="laps"></param>
        /// <param name="S1"></param>
        /// <param name="S2"></param>
        /// <param name="s3"></param>
        /// <param name="s4"></param>
        /// <param name="s6"></param>
        /// <param name="del"></param>
        /// <param name="TM"></param>
        /// <param name="XM"></param>
        /// <param name="ym"></param>
        /// <param name="sw"></param>
        /// <param name="icount"></param>
        /// <param name="ifault"></param>
        public static void x_probits(ProbitModel model, int k, ref double C1, ref double C, ref int nc, int nrc, bool clog, double qld, double[] D, double[] s, double[] r, double[] P, double[] w, double[] y, double[] pob, double[] x, ref double a, ref double b, ref int laps, ref double S1, ref double S2, ref double s3, ref double s4, ref double s6, ref double del, ref double TM, ref double XM, ref double ym, ref double sw, ref int icount, out int ifault)
        {
            int i;
            double PP;
            double s1l; double s2l; double s3l; double s4l = 0; double s6l = 0; double dell = 0;
            double s5 = 0;
            double swl;
            double dsq;

            double[] Y2 = new double[k + 1];
            double[] t2 = new double[k + 1];
            //  ***  CALCULATE EXPERIMENTAL VALUE OF NATURAL MORTALITY (C1) AND RESET CPOOL
            double rc = nrc;
            double cpool = -1.0;
            if (C1 < 0.0)
            {
                C1 = rc / Convert.ToDouble(nc);
                cpool = C1;
            }
            if (C1 < 0.0)
            {
                ifault = 1;
                return;
            }
            double dc = 0.0;
            C = C1;
            for (i = 1; i <= k; i++)
            {
                x[i] = D[i];
            }
            //  ***  CHECK THAT NUMBER OF DOSE LEVELS >2
            ifault = 2;
            if (k <= 2)
            {
                return;
            }
            //  ***  CHECK DOSE LEVELS ARE ALL POSITIVE
            ifault = 3;
            for (i = 1; i <= k; i++)
            {
                if (x[i] < 0.0)
                {
                    return;
                }
            }
            //  ***  IF LOG CONVERSION REQUIRED , CHECK DOSE LEVELS > 0 THEN CONVERT TO
            //  ***  COMMON LOGS
            if (clog)
            {
                for (i = 1; i <= k; i++)
                {
                    if (x[i] > 0.0)
                    {
                        x[i] = Math.Log(x[i]) / Math.Log(10.0);
                    }
                    else
                    {
                        ifault = 4;
                        return;
                    }
                }
            }
            //  ***  CHECK FOR RESPONDERS > SUBJECTS, AND SUBJECTS < 1,  AND CALCULATE
            //  ***  PROPORTIONS RESPONDING
            for (i = 1; i <= k; i++)
            {
                if (s[i] > 0.0)
                {
                    if ((s[i] - r[i]) < 0.0)
                    {
                        ifault = 1;
                        return;
                    }
                    P[i] = r[i] / s[i];
                }
            }
            //  ***  CHECK THAT NO PROPORTIONS RESPONDING (P) ARE LOWER THAN OR EQUAL
            //  ***  TO THE EXPERIMENTAL VALUE OF NATURAL MORTALITY (CPOOL) (IF THERE
            //  ***  IS NO CONTROL DATA CPOOL WILL BE -1)
            int j = 0;
            for (i = 1; i <= k; i++)
            {
                if (P[i] - cpool <= 0.0)
                {
                    j = i;
                }
            }
            if (j > 0)
            {
                for (i = 1; i <= j; i++)
                {
                    nc = nc + Convert.ToInt32(s[i]);
                    rc = rc + r[i];
                }
                C1 = rc / Convert.ToDouble(nc);
                C = C1;
                ifault = 6;
                if (k - j <= 2)
                {
                    return;
                }
            }
            //  ***  CALCULATE INITIAL ESTIMATE OF A AND B FOR THE PROBIT EQUTION
            //  ***  CALCULATE OBSERVED PROBITS (POB), USING P ADJUSTED BY THE
            //  ***  EXPERIMENTAL VALUE OF NATURAL MORTALITY AND ADDING OR SUBTRACTING
            //  ***  -.5 IF PROPORTION RESPONDING IS 0.0 OR 1.0
            //  ***  CALCULATE EXPECTED PROBITS (Y) USING THESE INITIAL ESTIMATES
            double sm = 0.0;
            double sumxz = 0.0;
            double sumx = 0.0;
            double sumz = 0.0;
            double sumxx = 0.0;
            for (i = j + 1; i <= k; i++)
            {
                sm = sm + 1.0;
                PP = (P[i] - C) / (1.0 - C);
                if (PP <= 0.0)
                {
                    PP = ((0.5 / s[i]) - C) / (1.0 - C);
                }
                else
                {
                    if (PP >= 1.0)
                    {
                        PP = (((r[i] - 0.5) / s[i]) - C) / (1.0 - C);
                    }
                }
                int ifa;
                double z = model == ProbitModel.Probit ? PDF.gauinv(PP, out ifa) : 0.5 * Math.Log(PP / (1.0 - PP));
                pob[i] = z;
                sumxz = sumxz + z * x[i];
                sumx = sumx + x[i];
                sumz = sumz + z;
                sumxx = sumxx + x[i] * x[i];
            }
            XM = sumx / sm;
            double zm = sumz / sm;
            b = (sumxz - XM * sumz) / (sumxx - XM * sumx);
            a = zm - b * XM;
            for (i = 1; i <= k; i++)
            {
                y[i] = a + b * x[i];
            }
            //  ***  CALCULATE THE EXPECTED PROBITS ITERATIVELY  *******************
            //  ***  CALCULATE WEIGHTS(W) AND AUXILIARY VARIATE(T)
            do
            {
                icount = 0;
                for (i = 1; i <= k; i++)
                {
                    double v = y[i];
                    double dd;
                    double Q;
                    if (model == ProbitModel.Probit)
                    {
                        PP = PDF.alnorm(v);
                        Q = 1.0 - PP;
                        dd = 0.398942280401433 * Math.Exp(-v * v / 2.0);
                    }
                    else
                    {
                        PP = Math.Exp(v * 2.0) / (1.0 + Math.Exp(v * 2.0));
                        Q = 1.0 - PP;
                        dd = 2.0 * Q * PP;
                    }
                    if (Q <= 0.0 | Q >= 1.0)
                    {
                        icount = icount + 1;
                        w[i] = 0.0;
                        t2[i] = 0.0;
                    }
                    else
                    {
                        w[i] = dd * dd / (Q * (PP + C / (1.0 - C)));
                        t2[i] = Q / dd;
                    }
                    //  ***  CALCULATE WORKING PROBITS (Y2)
                    double ap = (P[i] - C) / (1.0 - C);
                    if (y[i] <= 0.0)
                    {
                        if (dd == 0.0)
                        {
                            Y2[i] = y[i] - PP + ap;
                        }
                        else
                        {
                            Y2[i] = y[i] - PP / dd + ap / dd;
                        }
                    }
                    else
                    {
                        if (dd == 0.0)
                        {
                            Y2[i] = y[i] + Q - (1.0 - ap);
                        }
                        else
                        {
                            Y2[i] = y[i] + Q / dd - (1.0 - ap) / dd;
                        }
                    }
                }
                //  ***  CHECK THAT THER ARE NOT TOO MANY DOSE LEVELS WITH 0 WEIGHTS
                ifault = 7;
                if (k - icount < 2)
                {
                    return;
                }
                //  ***  CALCULATE VALUES OF COEFFICIENTS in EQUATIONS (S1 TO S7)
                //  ***  FINNEY, PROBIT ANALYSIS (1971) PAGE 130
                double swxx = 0.0;
                double swx = 0.0;
                swl = 0.0;
                double swxy = 0.0;
                double swy = 0.0;
                double swyy = 0.0;
                double swxt = 0.0;
                double swtt = 0.0;
                double swt = 0.0;
                double swty = 0.0;
                for (i = 1; i <= k; i++)
                {
                    swxt = swxt + s[i] * w[i] * x[i] * t2[i];
                    swt = swt + s[i] * w[i] * t2[i];
                    swtt = swtt + s[i] * w[i] * t2[i] * t2[i];
                    swty = swty + s[i] * w[i] * t2[i] * Y2[i];
                    swxx = swxx + s[i] * w[i] * x[i] * x[i];
                    swx = swx + s[i] * w[i] * x[i];
                    swl = swl + s[i] * w[i];
                    swxy = swxy + s[i] * w[i] * x[i] * Y2[i];
                    swy = swy + s[i] * w[i] * Y2[i];
                    swyy = swyy + s[i] * w[i] * Y2[i] * Y2[i];
                }
                s1l = swxx - (swx * swx) / swl;
                s2l = swxy - (swx * swy) / swl;
                s3l = swyy - (swy * swy) / swl;
                if (C > 0)
                {
                    s4l = swtt - (swt * swt) / swl + Convert.ToDouble(nc) * (1.0 - C) / C;
                    s5 = swty - (swt * swy) / swl + Convert.ToDouble(nc) * (C1 - C) / C;
                    s6l = swxt - (swx * swt) / swl;
                    double s7 = s6l * s6l;
                    dell = s1l * s4l - s7;
                }
                ym = swy / swl;
                XM = swx / swl;
                TM = swt / swl;
                //  ***  CALCULATE NEW A, B AND DC USING COEFFICIENTS
                ifault = 7;
                if (s1l == 0)
                {
                    return;
                }
                b = s2l / s1l;
                if (C > 0.0)
                {
                    b = (s4l * s2l - s6l * s5) / dell;
                    dc = (1.0 - C) * (s5 * s1l - s6l * s2l) / dell;
                }
                a = ym - b * XM - (dc * TM) / (1.0 - C);
                //  ***  CALCULATE NEW EXPECTED PROBITS (STORE in T TEMPORARILY) AND
                //  ***  ADD CHANGE in NATURAL MORTALITY TO ESTIMATE. COMPARE NEW EXPECTED
                //  ***  PROBITS WITH OLD AND IF CHANGE > 1D-9 RETURN TO BEGINING OF
                //  ***  ITERATIVE CYCLE
                dsq = 0.0;
                for (i = 1; i <= k; i++)
                {
                    double t = a + b * x[i];
                    dsq = dsq + (y[i] - t) * (y[i] - t);
                    y[i] = t;
                }
                C = C + dc;
                laps = laps + 1;
                if (laps > 300)
                {
                    ifault = 10;
                    return;
                }
            }
            while (!(dsq < Constant.EPSILON));
            //  ***  CHECK THAT THERE ARE NOT TOO MANY DOSE LEVELS WITH 0 WEIGHTS
            ifault = 7;
            if (k - icount <= 2)
            {
                return;
            }
            //  ***  ADJUST P BY THE FINAL ESTIMATE OF NATURAL MORTALITY
            for (i = 1; i <= k; i++)
            {
                P[i] = (P[i] - C) / (1.0 - C);
            }
            //  ***  FINAL ESTIMATE OF NATURAL MORTALITY
            if (C > 0.0)
            {
            }
            S1 = s1l;
            S2 = s2l;
            s3 = s3l;
            s4 = s4l;
            s6 = s6l;
            sw = swl;
            del = dell;
            ifault = 0;
        }
        
        public static void x_profolt(int ifault, ITemplateHost host)
        {
            if (ifault != 0)
            {
                string msg;
                switch (ifault)
                {
                    case 1:
                        msg = "Responders > Subjects";
                        break;
                    case 2:
                        msg = "Dose levels < 2";
                        break;
                    case 3:
                        msg = "Negative dose level";
                        break;
                    case 4:
                        msg = "> 1 Dose level <= 0";
                        break;
                    case 6:
                        msg = "Too few dose levels after adjusting for controls";
                        break;
                    case 7:
                        msg = "Too many dose levels with zero weights";
                        break;
                    case 8:
                    case 9:
                        msg = "Slope insignificant, can not calculate confidence limits";
                        break;
                    case 10:
                        msg = "Convergent solution not reached within 300 cycles";
                        break;
                    default:
                        msg = "Unknown error";
                        break;
                }

                host.Error(msg, "Fault in Probit Analysis");
            }
        }


        private static void x_qdcl(ProbitModel model, int k, out double C2, double ici, bool clog, double qld, ref double dose50, ref double doseq, double a, double b, ref double nohetllm, ref double nohetulm, ref double hetllm, ref double hetulm, ref double nohetllq, ref double nohetulq, ref double hetllq, ref double hetulq, ref double C, out double se, ref double cse, out double seh, ref double cseh, ref double I1, ref double S1, ref double S2, ref double s3, double s4, double s6, double del, double TM, double XM, double sw, out double varb, int icount, out int ifault)
        {
            double covar = 0;
            double vardcb = 0;
            double g = 0;

            int ibit = 0;
            //  ***  PUT C2 = C, NATURAL MORTALITY. (C BECOMES THE CHI-SQUARED VALUE)
            C2 = C;
            C = s3 - b * S2;
            //  ***  CALCULATE THE VARIANCE OF B (VARB),(THE FORMULA IS DIFFERENT IF
            //  ***  THERE IS CONTROL DATA). CALCULATE THE DEGREES OF FREEDOM (I).
            if (C2 <= 0.0)
            {
                varb = 1.0 / S1;
            }
            else
            {
                varb = s4 / del;
            }
            int i = k - 2 - icount;
            //  ***  WRITE CHI-SQUARE VALUE, AND STANDARD ERROR OF B FOR HETEROGENEITY
            //  ***  AND NO HETEROGENEITY
            se = Math.Sqrt(varb);
            seh = se * Math.Sqrt(C / Convert.ToDouble(i));
            //  ***  IF THERE ARE CONTROL DATA CALCULATE AND PRINT THE STANDARD ERROR
            //  ***  OF C FOR NO HETEROGENEITY AND HETEROGENEITY
            if (C2 > 0.0)
            {
                cse = (1.0 - C2) * Math.Sqrt(S1 / del);
                cseh = cse * Math.Sqrt(C / Convert.ToDouble(i));
            }
            //  ***  CALCULATE  AND PRINT THE VALUES FOR LETHAL DOSES 50 OR 90 (Z).
            //  ***  CONVERT IT IF LOGS HAVE BEEN USED. (IBIT RECORDS WHETHER LETHAL
            //  ***  DOSE 50 OR LETHAL DOSE Q IS BEING CALCULATED)
            do
            {
                int ifa;
                double qldz;
                if (ibit == 1)
                {
                    if (model == ProbitModel.Probit)
                    {
                        qldz = PDF.gauinv(qld / 100.0, out ifa);
                    }
                    else
                    {
                        qldz = qld / 100.0;
                        qldz = 0.5 * Math.Log(qldz / (1.0 - qldz));
                    }
                }
                else
                {
                    qldz = 0.0;
                }
                double pdose = (qldz - a) / b;
                double dose = clog ? Math.Exp(pdose * Math.Log(10.0)) : pdose;
                //  ***  FOR BOTH LETHAL DOSES CALCULATE THE CONFIDENCE LIMITS FOR
                //  ***  NO HETEROGENEITY AND HETEROGENEITY (HET RECORDS WHETHER NO
                //  ***  HETEROGENEITY OR HETEROGENEITY BEING PERFORMED) ***************
                //  ***  RECORD in ICL AND PRINT WHICH CONFIDENCE LIMITS ARE BEING
                //  ***  CALCULATED AND PUT THE NORMAL DEVIATE FOR THOSE LIMITS in T.
                ifa = 0;
                double t = PDF.gauinv(ici + ((1.0 - ici) / 2.0), out ifa);
                int het = 1;
                double vx = pdose - XM;
                varb = 1.0 / S1;
                //  ***  IF THERE IS CONTROL DATA THE FORMULA FOR CONFIDENCE LIMITS IS
                //  ***  MORE COMPLICATED, CALCULATE SOME EXTRA TERMS HERE
                if (C2 > 0.0)
                {
                    varb = s4 / del;
                    vardcb = S1 / del;
                    covar = -s6 / del;
                }
                //  ***  IF HETEROGENEITY CONFIDENCE LIMITS BEING CALCULATED, MULTIPLY
                //  ***  VALUES in EQUATION BY THE CHI-SQUARE VALUE DIVIDED BY THE DEGREES
                //  ***  OF FREEDOM AND PUT T VALUE FOR CONFIDENCE LIMITS INT.
                do
                {
                    if (het == 2)
                    {
                        varb = varb * C / Convert.ToDouble(i);
                        I1 = Convert.ToDouble(i);
                        t = PDF.tfromp((1.0 - ici) / 2.0, I1);
                    }
                    else
                    {
                        g = t * t * varb / (b * b);
                    }
                    //  ***  TEST FOR INSIGNIFICANCE OF SLOPE, IF IT IS PRESENT PRINT MESSAGE
                    //  ***  AND DISCONTINUE CALCULATIONS FOR THIS LETHAL DOSE
                    ifault = 9;
                    if (g - 1.0 > 0.0)
                    {
                        return;
                    }
                    //  ***  CALCULATE MORE TERMS in THE EQUATIONS FOR CONFIDENCE LIMITS
                    double gi = 1.0 - g;
                    double pf1 = pdose + g * vx / gi;
                    double pf2 = t / (Math.Abs(b) * gi);
                    double pf3 = gi / sw + varb * vx * vx;
                    if (het == 2)
                    {
                        pf3 = (gi * C) / (sw * i) + varb * vx * vx;
                    }
                    if (C2 > 0.0)
                    {
                        if (het == 2)
                        {
                            vardcb = vardcb * C / i;
                            covar = covar * C / i;
                        }
                        pf1 = pf1 - g * TM * covar / (varb * gi);
                        double pf3a = 1.0 / sw + TM * TM * vardcb;
                        if (het == 2)
                        {
                            pf3a = C / (sw * i) + TM * TM * vardcb;
                        }
                        pf3 = pf3a - 2 * vx * TM * covar + vx * vx * varb - g * (pf3a - TM * TM * covar * covar / varb);
                    }
                    ifault = 8;
                    if (pf3 < 0.0)
                    {
                        return;
                    }
                    double pf4 = pf2 * Math.Sqrt(pf3);
                    double ans1 = pf1 + pf4;
                    double ans2 = pf1 - pf4;
                    //  ***  IF LOG CONVERSION HAS BEEN USED CONVERT THE VALUES OF THE
                    //  ***  CONFIDENCE LIMITS
                    if (clog)
                    {
                        ans1 = Math.Exp(ans1 * Math.Log(10.0));
                        ans2 = Math.Exp(ans2 * Math.Log(10.0));
                    }
                    //  ***  SET THE CONFIDENCE LIMITS FOR NO HETEROGENEITY. PUT HET=2
                    //  ***  THEN GO BACK AND CALCULATE CONFIDENCE LIMITS FOR HETEROGENEITY
                    if (ibit == 0)
                    {
                        if (het == 1)
                        {
                            het = 2;
                            nohetllm = ans2;
                            nohetulm = ans1;
                        }
                        else
                        {
                            hetllm = ans2;
                            hetulm = ans1;
                            break;
                        }
                    }
                    else
                    {
                        if (het == 1)
                        {
                            het = 2;
                            nohetllq = ans2;
                            nohetulq = ans1;
                        }
                        else
                        {
                            hetllq = ans2;
                            hetulq = ans1;
                            break;
                        }
                    }
                }
                while (true);
                //  ***  IF LETHAL DOSE 50 HAS JUST BEEN CALCULATED THEN GO BACK AND
                //  ***  DO LETHAL DOSE 90
                if (ibit == 0)
                {
                    dose50 = dose;
                    ibit = 1;
                }
                else
                {
                    doseq = dose;
                    break;
                }
            }
            while (true);
            ifault = 0;
        }


        public static ParameterBag PlotProbit(ITemplateHost host, ParameterBag parameters)
        {
            MultipleLinearRegressionContext context = ((MultipleLinearRegressionContext)(parameters["context"].Data));
            int Model = context.M;
            double[] x = context.X1;
            double[] y = context.H1;
            double a = context.ARG[1];
            double b = context.ARG[2];
            double t;
            double sw = context.ARG[5];
            double S1 = context.ARG[6];
            double ici = context.ARG[16];
            bool clog = context.DoC;
            if (context.ARG[13] < 0.05)
            {
                t = PDF.tfromp(ici + ((1.0 - ici) / 2.0), context.ARG[14]);
            }
            else
            {
                int ifa;
                t = PDF.gauinv(ici + ((1.0 - ici) / 2.0), out ifa);
            }

            string YAxisTitle = context.Labels[2] + " / " + context.Labels[1];
            string XAxisTitle = context.Labels[0];
            y[0] = Constant.MISSING; //  Force no point at (0,0)
            x[0] = Constant.MISSING;
            ChartDefinition cd = new ChartDefinition();
            cd.AddYSeries(y, YAxisTitle);
            cd.AddXSeries(x, XAxisTitle);
            cd.ScaleParameters.X.ScaleType = clog ? ScaleType.Log10 : ScaleType.Linear;

            ParameterBag outputParameters = new ParameterBag();

            using (ChartRenderer ch = (ChartRenderer)ChartRendererFactory.ChartRendererFor(cd))
            {
                string rtf = ch.PlotLogitAndReturnRtf(host, "Proportional Response with " + Formatting.XRound(ici * 100, 1) + "% CI", Model, t, sw, S1, a, b, XAxisTitle, YAxisTitle);
                outputParameters.AddOutput("chart", rtf);
            }
            return outputParameters;
        }


        public static ParameterBag RptProbitInterpolateX(ITemplateHost host, ParameterBag parameters)
        {
            MultipleLinearRegressionContext context = ((MultipleLinearRegressionContext)(parameters["context"].Data));
            int Model = context.M;
            double a = context.ARG[1];
            double b = context.ARG[2];
            bool clog = context.DoC;
            double C2 = context.ARG[18];
            string[] Label = context.Labels;

            // Interpolate X value
            double xval = parameters["newx"].AsDouble;
            if (xval < 0)
            {
                throw new TemplateOperationCancelledException();
            }

            ParameterBag outputParameters = new ParameterBag();
            if (xval == 0.0)
            {
                xval = 1.0E-30;
            }
            if (clog)
            {
                xval = Math.Log(xval) / Math.Log(10.0);
            }
            double yval = a + b * xval;
            outputParameters.AddOutput("x_lab", Label[0]);
            outputParameters.AddOutput("x", host.RoundU(xval));
            if (Model == 1)
            {
                yval = PDF.alnorm(yval);
            }
            else
            {
                yval = Math.Exp(yval * 2.0) / (1.0 + Math.Exp(yval * 2.0));
            }
            outputParameters.AddOutput("resp", host.RoundU(yval));
            if (C2 > 0)
            {
                yval = C2 + yval * (1.0 - C2);
                outputParameters.AddOutput("mort", "Incorporating natural mortality, proportional response = " + yval.ToString());
            }
            else
            {
                outputParameters.AddOutput("mort", string.Empty);
            }
            return outputParameters;
        }


        public static ParameterBag RptProbitInterpolateY(ITemplateHost host, ParameterBag parameters)
        {
            MultipleLinearRegressionContext context = ((MultipleLinearRegressionContext)(parameters["context"].Data));
            int Model = context.M;
            double a = context.ARG[1];
            double b = context.ARG[2];
            bool clog = context.DoC;
            double C2 = context.ARG[18];
            string[] Label = context.Labels;
            double qdose;

            // Interpolate Y value
            double yval = parameters["newy"].AsDouble;
            if (yval <= 0 || yval >= 1)
            {
                throw new TemplateOperationCancelledException();
            }

            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("resp", host.RoundU(yval));
            outputParameters.AddOutput("mort", C2 > 0 ? "Not considering natural mortality." : string.Empty);
            if (Model == 1)
            {
                int ifa;
                qdose = PDF.gauinv(yval, out ifa);
            }
            else
            {
                qdose = 0.5 * Math.Log(yval / (1.0 - yval));
            }
            qdose = (qdose - a) / b;
            if (clog)
            {
                qdose = Math.Exp(qdose * Math.Log(10.0));
            }
            outputParameters.AddOutput("x_lab", Label[0]);
            outputParameters.AddOutput("x", host.RoundU(qdose));
            return outputParameters;
        }


        public static ParameterBag RptProbitMore(ITemplateHost host, ParameterBag parameters)
        {
            MultipleLinearRegressionContext context = ((MultipleLinearRegressionContext)(parameters["context"].Data));
            int Model = context.M;
            double a = context.ARG[1];
            double b = context.ARG[2];
            double varb = context.ARG[4];
            // double sw = context.ARG[ 5 ]; 
            double S1 = context.ARG[6];
            double S2 = context.ARG[7];
            double S3 = context.ARG[8];
            double seh = context.ARG[9];
            double se = context.ARG[10];
            double cse = context.ARG[11];
            double cseh = context.ARG[12];
            double hetp = context.ARG[13];
            // double ici = context.ARG[ 16 ]; 
            bool clog = context.DoC;
            // int nx = context.DF; 
            double C1 = context.ARG[17];
            double C2 = context.ARG[18];
            int laps = context.P;
            int k = context.DF;
            // string[] Label = context.Label; 
            double[] dv = context.DV;
            double[] sv = context.SV;
            double[] rv = context.RV;
            int i;

            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("itr", laps.ToString());
            outputParameters.AddOutput("sxx", host.RoundU(S1));
            outputParameters.AddOutput("sxy", host.RoundU(S2));
            outputParameters.AddOutput("syy", host.RoundU(S3));
            outputParameters.AddOutput("var_b", host.RoundU(varb));
            if (hetp < 0.05)
            {
                outputParameters.AddOutput("het", "with heterogeneity");
                outputParameters.AddOutput("seb", host.RoundU(seh));
            }
            else
            {
                outputParameters.AddOutput("het", "without heterogeneity");
                outputParameters.AddOutput("seb", host.RoundU(se));
            }
            if (C1 != 0)
            {
                IList<ParameterBag> naturalList = new List<ParameterBag>();
                outputParameters.AddOutput("*natural", naturalList);
                ParameterBag naturalParameters = new ParameterBag();
                naturalList.Add(naturalParameters);
                naturalParameters.AddOutput("c", host.RoundU(C2));
                if (hetp < 0.05)
                {
                    naturalParameters.AddOutput("het_c", "with heterogeneity");
                    naturalParameters.AddOutput("seb_c", host.RoundU(cseh));
                }
                else
                {
                    naturalParameters.AddOutput("het_c", "without heterogeneity");
                    naturalParameters.AddOutput("seb_c", host.RoundU(cse));
                }
            }
            else
            {
                outputParameters.AddOutput("*natural", null);
            }
            IList<ParameterBag> obsList = new List<ParameterBag>();
            outputParameters.AddOutput("*obs", obsList);
            for (i = 1; i <= k; i++)
            {
                double xval = dv[i];
                if (xval == 0)
                    xval = 1.0E-30;
                if (clog)
                    xval = Math.Log(xval) / Math.Log(10.0);
                double yval = a + b * xval;
                // int ifa = 0; 
                if (Model == 1)
                    yval = PDF.alnorm(yval);
                else
                    yval = Math.Exp(yval * 2.0) / (1.0 + Math.Exp(yval * 2.0));
                yval = C2 + yval * (1.0 - C2);
                double ex = yval * sv[i];
                ParameterBag obsParameters = new ParameterBag();
                obsList.Add(obsParameters);
                obsParameters.AddOutput("obs", i.ToString());
                obsParameters.AddOutput("sub", host.RoundU(sv[i]));
                obsParameters.AddOutput("res", host.RoundU(rv[i]));
                obsParameters.AddOutput("exp", host.RoundU(ex));
                obsParameters.AddOutput("dev", host.RoundU(rv[i] - ex));
            }
            return outputParameters;
        }
    }

}
