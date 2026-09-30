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
        public double SD { get; set; }
        public double SEM { get; set; }
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
        public double InterquartileRange { get; set; }
        public double Minimum { get; set; }
        public double UserCentileL { get; set; }
        public double UserCentileU { get; set; }
        public double Range { get; set; }
        public double WeightedSum { get; set; }
        public double SumOfWeights { get; set; }

        public string UserCentileLCaption { get; set; }
        public string UserCentileUCaption { get; set; }
        public string Title { get; set; }

        public int CentileType;

        public struct VarAndWeight
        {
            public double Data;
            public double Weight;
        }

        private class VarAndWeightByData : IComparer<VarAndWeight>
        {
            private int Compare(VarAndWeight x, VarAndWeight y)
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
            int IComparer<VarAndWeight>.Compare(VarAndWeight x, VarAndWeight y)
            {
                return Compare(x, y);
            }

        }

        ///  <summary>
        ///  Univariate summary statistics with optional analytical weights
        ///  </summary>
        ///  <param name="x"></param>
        ///  <param name="v"></param>
        ///  <param name="start">Index of the first valid row in x and v</param>
        ///  <param name="rows">Number of valid rows</param>
        ///  <param name="userCL"></param>
        ///  <param name="userCentL"></param>
        ///  <param name="userCentU"></param>
        ///  <param name="nvSum"></param>
        ///  <param name="xs">1-based array of VarAndWeight</param>
        ///  <returns></returns>
        ///  <remarks>see Gleason JR. Univariate summaries with boxplots. Stata Technical Bulletin sg67, 1997 and sg67.1, 1999.</remarks>
        public bool FullSummary(double[] x, double[] v, int start, int rows, double userCL, double userCentL, double userCentU, double nvSum, out VarAndWeight[] xs)
        {
            // preparatory counting and feeder arrays
            bool doUserCentL;
            // a centile of 0 is the minimum and one of 100 the maximum; the name has the percentage without the figures that
            // its product with 100 has beyond what was entered (7.000000000000001 for 7)
            if (userCentL >= 0.0 && userCentL <= 100.0)
            {
                doUserCentL = true;
                UserCentileLCaption = "Centile " + Formatting.XRound(userCentL, 6);
            }
            else
            {
                doUserCentL = false;
                UserCentileLCaption = string.Empty;
            }
            bool doUserCentU;
            if (userCentU >= 0.0 && userCentU <= 100.0)
            {
                doUserCentU = true;
                UserCentileUCaption = "Centile " + Formatting.XRound(userCentU, 6);
            }
            else
            {
                doUserCentU = false;
                UserCentileUCaption = string.Empty;
            }
            ValidData = rows;
            xs = new VarAndWeight[ValidData + 1]; // 1-based
            double[] xo = new double[ValidData + 1]; // 1-based
            double[] w = new double[ValidData + 1]; // 1-based
            ValidData = 0;
            double sumv = 0.0;
            int k = 0;
            for (int i = start; i < start + rows; i++)
            {
                if (x[i] != Constant.MISSING && v[i] != Constant.MISSING && !double.IsNaN(x[i]) && !double.IsNaN(v[i]))
                {
                    k++;
                    xs[k].Data = x[i];
                    xo[k] = x[i];
                    // keep each weight with its observation: rows dropped above must not shift the weights against the data
                    w[k] = v[i];
                    sumv += v[i];
                    ValidData++;
                }
            }
            MissingData = rows - ValidData;
            double nnx = Convert.ToDouble(ValidData);

            // set up normalised analytical weights
            WeightedSum = 0.0;
            SumOfWeights = 0.0;
            if (sumv == 0)
                ValidData = 0;
            else
            {
                double nsumv = nvSum != Constant.MISSING ? nvSum : nnx / sumv;
                for (int i = 1; i <= ValidData; i++)
                {
                    SumOfWeights += w[i];
                    double wt = w[i] * nsumv;
                    w[i] = wt;
                    xs[i].Weight = wt;
                    WeightedSum += wt;
                }
            }
            // confidence interval prep
            if (userCL <= 0.0 || userCL >= 1.0)
                userCL = 0.95;
            double P = (1.0 - userCL) / 2.0;
            if (P > 1.0 - P)
                P = 1.0 - P;
            double cit = PDF.tfromp(P, ValidData - 1);

            if (ValidData > 1)
            {
                // nonparametric summary

                Array.Sort(xs, 1, ValidData, new VarAndWeightByData());

                // get quantiles
                Minimum = GetCentile(xs, ValidData, 0);
                LowerQuartile = GetCentile(xs, ValidData, 0.25);
                Median = GetCentile(xs, ValidData, 0.5);
                UpperQuartile = GetCentile(xs, ValidData, 0.75);
                Maximum = GetCentile(xs, ValidData, 1);
                Range = Maximum - Minimum;
                InterquartileRange = UpperQuartile - LowerQuartile;
                UserCentileL = doUserCentL ? GetCentile(xs, ValidData, userCentL / 100.0) : Constant.MISSING;
                UserCentileU = doUserCentU ? GetCentile(xs, ValidData, userCentU / 100.0) : Constant.MISSING;

                // parametric univariate summary

                // basic sums
                Sum = 0.0;
                double slog = 0.0;
                bool geometricMeanOk = true;
                for (int i = 1; i <= ValidData; i++)
                {
                    Sum += xo[i] * w[i];
                    // weighted mean of the logs: the weight multiplies log(x), it does not go inside the logarithm
                    if (xo[i] > 0.0 && w[i] >= 0.0)
                        slog += w[i] * Math.Log(xo[i]);
                    else
                        geometricMeanOk = false;
                }
                Mean = Sum / nnx;
                // the sum is rounded as it is formed, and the mean with it: the mean of the differences from the mean is what
                // the rounding has left, and is added to it
                double left = 0.0;
                for (int i = 1; i <= ValidData; i++)
                    left += (xo[i] - Mean) * w[i];
                if (double.IsFinite(Mean + left / nnx))
                    Mean += left / nnx;
                // the mean now has all the figures that a number holds; what is then left of the mean of the differences is
                // taken off the differences themselves
                double rest = 0.0;
                for (int i = 1; i <= ValidData; i++)
                    rest += (xo[i] - Mean) * w[i];
                rest = double.IsFinite(rest / nnx) ? rest / nnx : 0.0;

                // deviations from the mean, in units of the power of two at or below the greatest of them: their powers then
                // neither pass the greatest number that there is nor are lost below the least, and to divide by a power of
                // two changes no figure
                double most = 0.0;
                for (int i = 1; i <= ValidData; i++)
                    most = Math.Max(most, Math.Abs(xo[i] - Mean - rest));
                double unit = most > 0.0 && double.IsFinite(most) ? Math.ScaleB(1.0, Math.ILogB(most)) : 1.0;
                double sumsqdev = 0.0;
                for (int i = 1; i <= ValidData; i++)
                {
                    double xd = (xo[i] - Mean - rest) / unit;
                    sumsqdev += xd * xd * w[i];
                }
                SD = double.IsFinite(sumsqdev) && sumsqdev >= 0.0 ? Math.Sqrt(sumsqdev / (ValidData - 1)) * unit : Constant.MISSING;
                // a variance beyond the greatest number that there is has no figure, though its root has
                Variance = SD == Constant.MISSING ? Constant.MISSING : sumsqdev / (ValidData - 1) * unit * unit;
                if (!double.IsFinite(Variance))
                    Variance = Constant.MISSING;
                if (ValidData <= 0 || SD == Constant.MISSING)
                {
                    SEM = Constant.MISSING;
                    MeanLCL = Constant.MISSING;
                    MeanUCL = Constant.MISSING;
                }
                else
                {
                    SEM = SD / Math.Sqrt(nnx);
                    double bit = cit * SD / Math.Sqrt(nnx);
                    MeanLCL = Mean - bit;
                    MeanUCL = Mean + bit;
                }
                GeometricMean = geometricMeanOk ? Math.Exp(slog / nnx) : Constant.MISSING;
                if (SD != Constant.MISSING && Mean != Constant.MISSING && Mean != 0.0)
                {
                    VarianceCoefficient = SD / Mean;
                }
                else
                {
                    VarianceCoefficient = Constant.MISSING;
                }

                // moments
                if (SD != Constant.MISSING & sumsqdev != 0 & ValidData > 3)
                {
                    double m2 = 0.0;
                    double m3 = 0.0;
                    double m4 = 0.0;
                    for (int i = 1; i <= ValidData; i++)
                    {
                        double xd = (xo[i] - Mean - rest) / unit;
                        m2 += Math.Pow(xd, 2.0) * w[i];
                        m3 += Math.Pow(xd, 3.0) * w[i];
                        m4 += Math.Pow(xd, 4.0) * w[i];
                    }
                    m2 /= nnx;
                    m3 /= nnx;
                    m4 /= nnx;
                    // Numerically consistent with R but not Stata
                    Skewness = m3 * Math.Pow(m2, -1.5);
                    Kurtosis = m4 * Math.Pow(m2, -2.0);
                }
                else
                {
                    Skewness = Constant.MISSING;
                    Kurtosis = Constant.MISSING;
                }
                return true;

            }

            // If we get here, there's no more than one row of valid data.
            if (ValidData == 1)
            {
                // one value is its own median, quartiles and centiles, and its own geometric mean if it is above 0; it has
                // no variance, and nothing that is made of the variance
                Skewness = Constant.MISSING;
                Kurtosis = Constant.MISSING;
                LowerQuartile = xo[1];
                InterquartileRange = 0.0;
                UpperQuartile = xo[1];
                UserCentileL = doUserCentL ? xo[1] : Constant.MISSING;
                UserCentileU = doUserCentU ? xo[1] : Constant.MISSING;
                GeometricMean = xo[1] > 0.0 ? xo[1] : Constant.MISSING;
                Median = xo[1];
                Variance = Constant.MISSING;
                Maximum = xo[1];
                Minimum = xo[1];
                Range = 0.0;
                Sum = xo[1];
                // these three were left holding whatever the object held before: 0, or the previous variable's values
                Mean = xo[1];
                VarianceCoefficient = Constant.MISSING;
                SD = Constant.MISSING;
                SEM = Constant.MISSING;
                MeanLCL = Constant.MISSING;
                MeanUCL = Constant.MISSING;
                return true;
            }

            // If we get here, there's no valid data.
            Skewness = Constant.MISSING;
            Kurtosis = Constant.MISSING;
            LowerQuartile = Constant.MISSING;
            InterquartileRange = Constant.MISSING;
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
            SD = Constant.MISSING;
            SEM = Constant.MISSING;
            VarianceCoefficient = Constant.MISSING;
            MeanLCL = Constant.MISSING;
            MeanUCL = Constant.MISSING;
            Range = Constant.MISSING;
            return false;
        }

        ///  <summary>
        ///  
        ///  </summary>
        ///  <param name="x">An array from 1 to n with all elements valid</param>
        ///  <param name="n"></param>
        ///  <param name="centile"></param>
        ///  <returns></returns>
        ///  <remarks>see Gleason JR. Univariate summaries with boxplots. Stata Technical Bulletin sg67, 1997 and sg67.1, 1999.</remarks>
        private double GetCentile(VarAndWeight[] x, int n, double centile)
        {
            double index;
            double lastcumsum = 0;

            if (centile < 0.0 || centile > 1.0)
                return Constant.MISSING;
            if (centile == 0.0)
                return x[1].Data;
            if (centile == 1.0)
                return x[n].Data;
            if (CentileType == 2)
            {
                index = Math.Floor(centile * (n + 1));
                double h = centile * (n + 1) - index;
                int bottom = index < 1 ? 1 : Convert.ToInt32(index);
                int top = index + 1 > n ? n : Convert.ToInt32(index) + 1;
                return (1.0 - h) * x[bottom].Data + h * x[top].Data;
            }

            index = centile * n;
            // Normalised weights carry rounding error (two weights of 49 become 0.99999999999999989 each), so whether the
            // cumulative weight has reached the index exactly, which is when two neighbours are averaged, is tested within a
            // tolerance. With unit weights the median and quartiles are unchanged (0.25 n, 0.5 n and 0.75 n are exact). A user
            // defined centile changes only where centile * n is a whole number that floating point misses: 0.29 * 100 is
            // 28.999999999999996 and 0.07 * 100 is 7.000000000000001, so the 29th and 7th centiles of 100 values were not
            // averaged although the 10th was; now all three are, as the definition says.
            double tol = 1.0E-9 * n;
            double cumsum = 0.0;
            int i;
            for (i = 1; i <= n; i++)
            {
                cumsum += x[i].Weight;
                if (cumsum > index + tol)
                    break;
                lastcumsum = cumsum;
            }
            if (i > n)
                i = n;
            if (i > 1 && Math.Abs(lastcumsum - index) <= tol)
                return (x[i - 1].Data + x[i].Data) / 2.0;
            return x[i].Data;
        }

        public bool WeightedSummaryFromXK(int k, double[,] x, int rows, string ti, double userCL, double userCentL, double userCentU, double[,] wt, string wti, double nvSum)
        {
            Title = ti + " (weight: " + wti + ")";
            double[] z = new double[rows];
            double[] v = new double[rows];
            for (int i = 1; i <= rows; i++)
            {
                z[i - 1] = x[k, i];
                v[i - 1] = wt[k, i];
            }
            CentileType = 1;
            return FullSummary(z, v, 0, rows, userCL, userCentL, userCentU, nvSum, out VarAndWeight[] _);
        }

        public bool FullSummaryFromXSort(double[] x, out double[] xSorted, int rows, string ti, double userCL, double userCentL, double userCentU, int centileDef)
        {
            int i;

            Title = ti;
            double[] v = new double[rows + 1 ];
            for (i = 1; i <= rows; i++)
                v[i] = 1.0;
            CentileType = centileDef;
            bool fullSummaryFromXSortReturn = FullSummary(x, v, 1, rows, userCL, userCentL, userCentU, Constant.MISSING, out VarAndWeight[] xs);
            xSorted = new double[rows + 1];
            for (i = 1; i <= rows; i++)
                xSorted[i] = xs[i].Data;
            return fullSummaryFromXSortReturn;
        }

        public bool FullSummaryFromX(double[] x, int rows, string ti, double UserCL, double UserCentL, double UserCentU, int CentileDef)
        {
            Title = ti;
            double[] v = new double[rows];
            for (int i = 0; i < rows; i++)
                v[i] = 1.0;
            CentileType = CentileDef;
            return FullSummary(x, v, 0, rows, UserCL, UserCentL, UserCentU, Constant.MISSING, out VarAndWeight[] _);
        }

        public bool FullSummaryFromXK(int k, double[,] x, int rows, string ti, double userCL, double userCentL, double userCentU, int centileDef)
        {
            Title = ti;
            double[] z = new double[rows];
            double[] v = new double[rows];
            for (int i = 1; i <= rows; i++)
            {
                z[i - 1] = x[k, i];
                v[i - 1] = 1.0;
            }
            CentileType = centileDef;
            return FullSummary(z, v, 0, rows, userCL, userCentL, userCentU, Constant.MISSING, out VarAndWeight[] _);
        }

    }


}
