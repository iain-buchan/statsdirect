using StatsDirect.Utilities;

using System;
using System.Collections.Generic;
namespace StatsDirect.Numerics
{
    public class Summary
    {
        public int ValidData { get; set; }
        public int MissingData { get; set; }
        public double Sum { get; set; }
        public double Mean { get; set; }
        public double Variance { get; set; }
        public double Sd { get; set; }
        public double Sem { get; set; }
        public double MeanLCL { get; set; }
        public double MeanUCL { get; set; }
        public double ConfidenceLevel { get; set; }
        public double GeometricMean { get; set; }
        public double Skewness { get; set; }
        public double Kurtosis { get; set; }
        public double VarianceCoefficient { get; set; }

        public double Maximum { get; set; }
        public double UpperQuartile { get; set; }
        public double Median { get; set; }
        public double LowerQuartile { get; set; }
        public double Minimum { get; set; }
        public double UserCentileL { get; set; }
        public double UserCentileU { get; set; }
        public double Range { get; set; }
        public double WeightedSum { get; set; }
        public double SumOfWeights { get; set; }

        public string UserCentileLCaption { get; set; }
        public string UserCentileUCaption { get; set; }
        public string CLCaption { get; set; }
        public string Title { get; set; }

        public int CentileType;

        private struct VarAndWt
        {
            public double Data;
            public double wt;
        }

        private class VarAndWtByData : IComparer<VarAndWt>
        {
            private int Compare(VarAndWt x, VarAndWt y)
            {
                //  First check TM
                if (x.Data > y.Data)
                    return 1;
                if (x.Data < y.Data)
                    return -1;

                //  If we get here, there are no meaningful differences
                return 0;
            }
            // interface methods implemented by Compare
            int IComparer<VarAndWt>.Compare(VarAndWt x, VarAndWt y)
            {
                return Compare(x, y);
            }

        }

        ///  <summary>
        ///  Univariate summary statistics with optional analytical weights
        ///  </summary>
        ///  <param name="x"></param>
        ///  <param name="v"></param>
        ///  <param name="start"></param>
        ///  <param name="finish"></param>
        ///  <param name="userCL"></param>
        ///  <param name="userCentL"></param>
        ///  <param name="userCentU"></param>
        ///  <param name="nvSum"></param>
        /// <param name="xs"> </param>
        /// <returns></returns>
        ///  <remarks>see Gleason JR. Univariate summaries with boxplots. Stata Technical Bulletin sg67, 1997 and sg67.1, 1999.</remarks>
        private bool FullSummary(double[] x, double[] v, int start, int finish, double userCL, double userCentL, double userCentU, double nvSum, out VarAndWt[] xs)
        {
            // preparatory counting and feeder arrays
            bool doUserCentL;
            if (userCentL > 0.0 && userCentL < 100.0)
            {
                doUserCentL = true;
                UserCentileLCaption = "Centile " + userCentL.ToString();
            }
            else
            {
                doUserCentL = false;
                UserCentileLCaption = "";
            }
            bool doUserCentU;
            if (userCentU > 0.0 && userCentU < 100.0)
            {
                doUserCentU = true;
                UserCentileUCaption = "Centile " + userCentU.ToString();
            }
            else
            {
                doUserCentU = false;
                UserCentileUCaption = "";
            }
            ValidData = finish - start + 1;
            xs = new VarAndWt[ValidData + 1];
            double[] xo = new double[ValidData + 1];
            double[] w = new double[ValidData + 1];
            ValidData = 0;
            double sumv = 0.0;
            int k = 0;
            for (int i = start; i <= finish; i++)
            {
                if (x[i] != Constant.MISSING & v[i] != Constant.MISSING)
                {
                    k++;
                    xs[k].Data = x[i];
                    xo[k] = x[i];
                    sumv += v[i];
                    ValidData++;
                }
            }
            MissingData = (finish - start) - ValidData + 1;
            double nnx = Convert.ToDouble(ValidData);

            // set up normalised analytical weights
            WeightedSum = 0.0;
            SumOfWeights = 0.0;
            if (sumv == 0)
                ValidData = 0;
            else
            {
                double nsumv = (nvSum != Constant.MISSING) ? nvSum : nnx / sumv;
                for (int i = 1; i <= ValidData; i++)
                {
                    SumOfWeights += v[i];
                    w[i] = v[i] * nsumv;
                    xs[i].wt = w[i];
                    WeightedSum += w[i];
                }
            }
            // confidence interval prep
            if (userCL <= 0.0 || userCL >= 1.0)
                userCL = 0.95;
            double P = (1.0 - userCL) / 2.0;
            if (P > 1.0 - P)
                P = 1.0 - P;
            double cit = PDF.tfromp(P, ValidData - 1);
            CLCaption = " " + Formatting.XRound(userCL * 100, 1) + "% CL";

            if (ValidData > 1)
            {
                // nonparametric summary

                Array.Sort(xs, 1, ValidData, new VarAndWtByData());

                // get quantiles
                Minimum = GetCentile(xs, ValidData, 0);
                LowerQuartile = GetCentile(xs, ValidData, 0.25);
                Median = GetCentile(xs, ValidData, 0.5);
                UpperQuartile = GetCentile(xs, ValidData, 0.75);
                Maximum = GetCentile(xs, ValidData, 1);
                Range = Maximum - Minimum;
                UserCentileL = doUserCentL ? GetCentile(xs, ValidData, userCentL / 100.0) : Constant.MISSING;
                UserCentileU = doUserCentU ? GetCentile(xs, ValidData, userCentU / 100.0) : Constant.MISSING;

                // parametric univariate summary

                // basic sums
                Sum = 0.0;
                double slog = 0.0;
                double sumsqdev = 0.0;
                bool gmok = false;
                for (int i = 1; i <= ValidData; i++)
                {
                    Sum += xo[i] * w[i];
                    if (xo[i] * w[i] > 0.0)
                        slog += Math.Log(xo[i] * w[i]);
                    else
                        gmok = true;
                }
                Mean = Sum / nnx;

                // deviations from the mean
                for (int i = 1; i <= ValidData; i++)
                {
                    if (Math.Abs(sumsqdev) > 1.0E+300)
                    {
                        sumsqdev = Constant.MISSING;
                        break;
                    }
                    sumsqdev += (xo[i] - Mean) * (xo[i] - Mean) * w[i];
                }
                if (sumsqdev == Constant.MISSING)
                    Variance = Constant.MISSING;
                else
                    Variance = sumsqdev / (ValidData - 1);
                Sd = Variance < 0.0 ? Constant.MISSING : Math.Sqrt(Variance);
                if (ValidData <= 0 || Sd == Constant.MISSING)
                {
                    Sem = Constant.MISSING;
                    MeanLCL = Constant.MISSING;
                    MeanUCL = Constant.MISSING;
                }
                else
                {
                    Sem = Sd / Math.Sqrt(nnx);
                    double bit = cit * Sd / Math.Sqrt(nnx);
                    MeanLCL = Mean - bit;
                    MeanUCL = Mean + bit;
                }
                GeometricMean = gmok == false ? Math.Exp(slog / nnx) : Constant.MISSING;
                if (Sd != Constant.MISSING & Mean != Constant.MISSING & Mean != 0.0)
                {
                    VarianceCoefficient = Sd / Mean;
                }
                else
                {
                    VarianceCoefficient = Constant.MISSING;
                }

                // moments
                if (Variance != Constant.MISSING & Variance != 0 & ValidData > 3)
                {
                    double m2 = 0.0;
                    double m3 = 0.0;
                    double m4 = 0.0;
                    bool toobig = false;
                    for (int i = 1; i <= ValidData; i++)
                    {
                        double xd = xo[i] - Mean;
                        m2 += (Math.Pow(xd, 2.0)) * w[i];
                        m3 += (Math.Pow(xd, 3.0)) * w[i];
                        m4 += (Math.Pow(xd, 4.0)) * w[i];
                        if (m4 > 1.0E+300)
                        {
                            toobig = true;
                            break;
                        }
                    }
                    if (toobig)
                    {
                        Skewness = Constant.MISSING;
                        Kurtosis = Constant.MISSING;
                    }
                    else
                    {
                        m2 = m2 / nnx;
                        m3 = m3 / nnx;
                        m4 = m4 / nnx;
                        // Numerically consistent with R but not Stata
                        Skewness = m3 * Math.Pow(m2, (-1.5));
                        Kurtosis = m4 * Math.Pow(m2, (-2.0));
                    }
                }
                else
                {
                    Skewness = Constant.MISSING;
                    Kurtosis = Constant.MISSING;
                }
                return true;

            }
            if (ValidData == 1)
            {
                Skewness = Constant.MISSING;
                Kurtosis = Constant.MISSING;
                LowerQuartile = Constant.MISSING;
                UpperQuartile = Constant.MISSING;
                UserCentileL = Constant.MISSING;
                UserCentileU = Constant.MISSING;
                GeometricMean = Constant.MISSING;
                Median = Constant.MISSING;
                Variance = Constant.MISSING;
                Maximum = xo[1];
                Minimum = xo[1];
                Sum = xo[1];
                Sd = Constant.MISSING;
                Sem = Constant.MISSING;
                MeanLCL = Constant.MISSING;
                MeanUCL = Constant.MISSING;
                return true;
            }
            Skewness = Constant.MISSING;
            Kurtosis = Constant.MISSING;
            LowerQuartile = Constant.MISSING;
            UpperQuartile = Constant.MISSING;
            UserCentileL = Constant.MISSING;
            UserCentileU = Constant.MISSING;
            GeometricMean = Constant.MISSING;
            Median = Constant.MISSING;
            Mean = Constant.MISSING;
            Variance = Constant.MISSING;
            Maximum = Constant.MISSING;
            Minimum = Constant.MISSING;
            Sum = Constant.MISSING;
            Sd = Constant.MISSING;
            Sem = Constant.MISSING;
            VarianceCoefficient = Constant.MISSING;
            MeanLCL = Constant.MISSING;
            MeanUCL = Constant.MISSING;
            Range = Constant.MISSING;
            return false;
        }

        ///  <summary>
        ///  
        ///  </summary>
        ///  <param name="x">x() is assumed to be an array from 1 to n with all elements valid</param>
        ///  <param name="N"></param>
        ///  <param name="centile"></param>
        ///  <returns></returns>
        ///  <remarks>see Gleason JR. Univariate summaries with boxplots. Stata Technical Bulletin sg67, 1997 and sg67.1, 1999.</remarks>
        private double GetCentile(VarAndWt[] x, int N, double centile)
        {
            double index;
            double lastcumsum = 0;

            if (centile < 0.0 | centile > 1.0)
            {
                return Constant.MISSING;
            }
            if (centile == 0.0)
            {
                return x[1].Data;
            }
            if (centile == 1.0)
            {
                return x[N].Data;
            }
            if (CentileType == 2)
            {
                index = Math.Floor(centile * (N + 1));
                double h = centile * (N + 1) - index;
                int bottom = index < 1 ? 1 : Convert.ToInt32(index);
                int top;
                if (index + 1 > N)
                {
                    top = N;
                }
                else { top = Convert.ToInt32(index) + 1; }
                return (1.0 - h) * x[bottom].Data + h * x[top].Data;
            }

            index = centile * Convert.ToDouble(N);
            double cumsum = 0.0;
            int i;
            for (i = 1; i <= N; i++)
            {
                cumsum = cumsum + x[i].wt;
                if (cumsum > index)
                    break;
                lastcumsum = cumsum;
            }
            if (i > N)
            {
                i = N;
            }
            if (lastcumsum == index)
                return (x[i - 1].Data + x[i].Data) / 2.0;
            return x[i].Data;
        }

        public bool WeightedSummaryFromXK(int k, double[,] x, int rows, string ti, double userCL, double userCentL, double userCentU, double[,] wt, string wti, double nvSum)
        {
            Title = ti + " (weight: " + wti + ")";
            double[] z = new double[rows + 1 /* VB to C# conversion */ ];
            double[] v = new double[rows + 1 /* VB to C# conversion */ ];
            for (int i = 1; i <= rows; i++)
            {
                z[i] = x[k, i];
                v[i] = wt[k, i];
            }
            CentileType = 1;
            VarAndWt[] xsrt;
            return FullSummary(z, v, 1, rows, userCL, userCentL, userCentU, nvSum, out xsrt);
        }

        public bool FullSummaryFromXSort(double[] x, out double[] xSorted, int rows, string ti, double userCL, double userCentL, double userCentU, int centileDef)
        {
            int i;

            Title = ti;
            double[] v = new double[rows + 1 /* for VB to C# conversion */ ];
            for (i = 1; i <= rows; i++)
                v[i] = 1.0;
            CentileType = centileDef;
            VarAndWt[] xs;
            bool fullSummaryFromXSortReturn = FullSummary(x, v, 1, rows, userCL, userCentL, userCentU, Constant.MISSING, out xs);
            xSorted = new double[rows + 1];
            for (i = 1; i <= rows; i++)
                xSorted[i] = xs[i].Data;
            return fullSummaryFromXSortReturn;
        }

        public bool FullSummaryFromX(double[] x, int rows, string ti, double UserCL, double UserCentL, double UserCentU, int CentileDef)
        {
            Title = ti;
            double[] v = new double[rows + 1 /* VB to C# conversion */ ];
            for (int i = 1; i <= rows; i++)
                v[i] = 1.0;
            CentileType = CentileDef;
            VarAndWt[] xs;
            bool fullSummaryFromXReturn = FullSummary(x, v, 1, rows, UserCL, UserCentL, UserCentU, Constant.MISSING, out xs);
            return fullSummaryFromXReturn;
        }

        public bool FullSummaryFromXK(int k, double[,] x, int rows, string ti, double userCL, double userCentL, double userCentU, int centileDef)
        {
            Title = ti;
            double[] z = new double[rows + 1 /* VB to C# conversion */ ];
            double[] v = new double[rows + 1 /* VB to C# conversion */ ];
            for (int i = 1; i <= rows; i++)
            {
                z[i] = x[k, i];
                v[i] = 1.0;
            }
            CentileType = centileDef;
            VarAndWt[] xs;
            bool fullSummaryFromXKReturn = FullSummary(z, v, 1, rows, userCL, userCentL, userCentU, Constant.MISSING, out xs);
            return fullSummaryFromXKReturn;
        }

    }


}
