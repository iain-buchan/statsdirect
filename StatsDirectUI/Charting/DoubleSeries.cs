using System;
using StatsDirect.Numerics;

namespace StatsDirect.Charting
{
    public class DoubleSeries : ISeries
    {
        public double[] Data { get; }

        public MarkerType MarkerType { get; set; }

        private bool hasSum;
        private double sum;
        private bool hasStdDev;
        private double stdDev;
        private double min;
        private double minGreaterThanZero;
        private double max;
        private bool hasMinMax;

        public DoubleSeries(double[] data, string? title = default)
        {
            Data = data;
            Title = title;
            // TODO: We need a better way of handling unknown marker types. This should not be here.
            MarkerType = MarkerType.Default;
        }

        /// <summary>
        /// TODO: CORRECTNESS: What if there are missing values?  How many points do we have, and how does that affect the StdDev?
        /// </summary>
        public int Points => Data.Length;

        public double Sum
        {
            get
            {
                if (!hasSum)
                {
                    double s = 0.0;
                    for (int i = 0; i < Data.Length; i++)
                        if (Data[i] != Constant.MISSING)
                            s += Data[i];
                    sum = s;
                    hasSum = true;
                }
                return sum;
            }
        }

        public double StdDev
        {
            get
            {
                if (!hasStdDev)
                {
                    double avg = Sum / Points;
                    double ep = 0.0; double var = 0.0;
                    for (int i = 0; i < Data.Length; i++)
                    {
                        if (Data[i] != Constant.MISSING)
                        {
                            double s = Data[i] - avg;
                            ep += s;
                            var += s * s;
                        }
                    }
                    var = (var - Math.Pow(ep, 2.0) / Points) / (Points - 1);
                    stdDev = Math.Sqrt(var);
                    hasStdDev = true;
                }
                return stdDev;
            }
        }

        public double Min
        {
            get
            {
                if (!hasMinMax)
                    CalcMinMax();
                return min;
            }
        }

        public double MinGreaterThanZero
        {
            get
            {
                if (!hasMinMax)
                    CalcMinMax();
                return minGreaterThanZero;
            }
        }

        public double Max
        {
            get
            {
                if (!hasMinMax)
                    CalcMinMax();
                return max;
            }
        }

        public string? Title { get; }

        private void CalcMinMax()
        {
            double mn = double.MaxValue;
            double mg0 = double.MaxValue;
            double mx = double.MinValue;
            for (int i = 0; i < Data.Length; i++)
            {
                double v = Data[i];
                if (v != Constant.MISSING)
                {
                    if (v < mn)
                        mn = v;
                    if (v > 0 && v < mg0)
                        mg0 = v;
                    if (v > mx)
                        mx = v;
                }
            }
            min = mn;
            minGreaterThanZero = mg0;
            max = mx;
            hasMinMax = true;
        }
    }
}
