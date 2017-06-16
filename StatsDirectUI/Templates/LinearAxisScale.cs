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
        /// The number of intervals between major tics. If this is 5, every 5th tic will be a major tic.
        public int IntervalsPerMajorTic { get; private set; }
        /// Where to put the major tics.  Phase 0 gives the first tic as a major, phase 1 gives the second tic as a major, etc..
        public int Phase { get; private set; }

        public double Interval => (MaximumScaleValue - MinimumScaleValue) / Intervals;

        public double FirstMajorTicValue => MinimumScaleValue + Interval * IntervalsPerMajorTic;

        public LinearAxisScale(double minimumDataValue, double maximumDataValue, double minimumScaleValue, double maximumScaleValue, int intervals, int intervalsPerMajorTic, int phase = 0)
        {
            MinimumDataValue = minimumDataValue;
            MaximumDataValue = maximumDataValue;
            MinimumScaleValue = minimumScaleValue;
            MaximumScaleValue = maximumScaleValue;
            Intervals = intervals;
            IntervalsPerMajorTic = intervalsPerMajorTic;
            Phase = phase;
        }

        /// <summary>
        /// Returns a linear list of tics constructed according to the parameters.
        /// </summary>
        /// <param name="min">The value of the first tic</param>
        /// <param name="interval">The interval between minor tics</param>
        /// <returns></returns>
        public IList<Tic> Tics()
        {
            List<Tic> tics = new List<Tic>(Intervals + 1);
            double interval = (MaximumScaleValue - MinimumScaleValue) / Intervals;
            for (int i = 0; i <= Intervals; i++)
                tics.Add(new Tic { Value = MinimumScaleValue + interval * i, TicType = (i - Phase) % IntervalsPerMajorTic == 0 ? TicType.Major : TicType.Minor });
            return tics;
        }

        public override string ToString()
        {
            return string.Format("LinearAxisScale({0}, {2}({3}) * {4}, {1})", MinimumScaleValue, MaximumScaleValue, Intervals, IntervalsPerMajorTic, Interval);
        }

        void IAxisScale.Accept(IAxisScaleVisitor visitor)
        {
            visitor.Visit(this);
        }

        string IAxisScale.ToAxisLabel(Tic tic, string mask)
        {
            return tic.Value.ToString(mask);
        }
    }
}
