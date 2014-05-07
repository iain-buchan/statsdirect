using System;

using StatsDirect.Numerics;
using System.Drawing;

namespace StatsDirect.Charting
{
    public class DoubleSeries : Series
    {
        public double[] Data { get; set; }

        //  Similar to markers
        internal Pen MarkerPen { get; set; }
        internal Pen LinePen { get; set; }
        internal MarkerShape MarkerShape { get; set; }
        internal bool IsMarkerFilled { get; set; }
        internal double MarkerSize { get; set; }

        private bool hasSum;
        private double sum;
        private bool hasStdDev;
        private double stdDev;
        private double min;
        private double max;
        private bool hasMinMax;

        public DoubleSeries()
        {
            //  Do nothing; this is only here because we also have a custom constructor
        }

        public DoubleSeries(double[] data, string title)
        {
            Data = data;
            Title = title;
        }

        public int Points
        {
            get
            {
                return Data.Length;
            }
        }

        public double Sum
        {
            get
            {
                if (!(hasSum))
                {
                    double s = 0.0;
                    for (int i = Data.GetLowerBound(0); i <= Data.GetUpperBound(0); i++)
                    {
                        if (Data[i] != Constant.MISSING)
                        {
                            s += Data[i];
                        }
                    }
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
                if (!(hasStdDev))
                {

                    double avg = Sum / Convert.ToDouble(Points);
                    double ep = 0.0; double var = 0.0;
                    for (int C = Data.GetLowerBound(0); C <= Data.GetUpperBound(0); C++)
                    {
                        double s = Data[C] - avg;
                        ep += s;
                        var += s * s;
                    }
                    var = (var - Math.Pow(ep, 2.0) / Convert.ToDouble(Points)) / Convert.ToDouble(Points - 1);
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
                if (!(hasMinMax))
                {
                    CalcMinMax();
                }
                return min;
            }
        }

        public double Max
        {
            get
            {
                if (!(hasMinMax))
                {
                    CalcMinMax();
                }
                return max;
            }
        }

        private void CalcMinMax()
        {
            double mn = double.MaxValue;
            double mx = double.MinValue;
            for (int i = Data.GetLowerBound(0); i <= Data.GetUpperBound(0); i++)
            {
                if (Data[i] != Constant.MISSING)
                {
                    if (Data[i] < mn)
                    {
                        mn = Data[i];
                    }
                    if (Data[i] > mx)
                    {
                        mx = Data[i];
                    }
                }
            }
            min = mn;
            max = mx;
            hasMinMax = true;
        }


        public override DoubleSeries AsDoubleSeries
        {
            get
            {
                return this;
            }
        }
    }
}
