using StatsDirect.Charting;
using System;
using System.Collections.Generic;

namespace StatsDirect.Templates
{
    public class LinearAxisScale: ILinearAxisScale
    {
        public double MinimumDataValue { get; private set; }
        public double MaximumDataValue { get; private set; }
        public double MinimumScaleValue { get; private set; }
        public double MaximumScaleValue { get; private set; }
        /// The number of intervals between tics (one less than the number of tics).  20 intervals = 21 tics - one extra at the end.
        public int Intervals { get; private set; }
        public double Interval => (MaximumScaleValue - MinimumScaleValue) / Intervals;

        public double FirstMajorTicValue => MinimumScaleValue + Interval;

        public LinearAxisScale(double minimumDataValue, double maximumDataValue, double minimumScaleValue, double maximumScaleValue, int intervals)
        {
            MinimumDataValue = minimumDataValue;
            MaximumDataValue = maximumDataValue;
            MinimumScaleValue = minimumScaleValue;
            MaximumScaleValue = maximumScaleValue;
            Intervals = intervals;
        }

        /// <summary>
        /// Returns a linear list of tics constructed according to the parameters.
        /// </summary>
        /// <param name="min">The value of the first tic</param>
        /// <param name="interval">The interval between minor tics</param>
        /// <returns></returns>
        public IList<Tic> Tics()
        {
            string msk = LinearAxisMasker.AxisMask(this);
            List<Tic> tics = new List<Tic>(Intervals + 1);
            double interval = (MaximumScaleValue - MinimumScaleValue) / Intervals;
            for (int i = 0; i <= Intervals; i++)
            {
                double value = MinimumScaleValue + interval * i;
                tics.Add(new Tic(value, value.ToString(msk)));
            }
            return tics;
        }

        public override string ToString()
        {
            return string.Format("LinearAxisScale({0}, {2} * {3}, {1})", MinimumScaleValue, MaximumScaleValue, Intervals, Interval);
        }

        void IAxisScale.Accept(IAxisScaleVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}
