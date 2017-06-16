using System;
using System.Collections.Generic;

namespace StatsDirect.Templates
{
    public class NewLinearAxisScale: ILinearAxisScale
    {
        public double MinimumDataValue { get; private set; }
        public double MaximumDataValue { get; private set; }
        public double MinimumScaleValue { get; private set; }
        public double MaximumScaleValue { get; private set; }
        /// The number of intervals between major tics. If this is 5, every 5th tic will be a major tic.
        public int IntervalsPerMajorTic { get; private set; }
        /// Where to put the major tics.  Phase 0 gives the first tic as a major, phase 1 gives the second tic as a major, etc..
        public int Phase { get; private set; }

        public double Interval { get; set; }

        public double FirstMajorTicValue => MinimumScaleValue + Interval * IntervalsPerMajorTic;

        public NewLinearAxisScale(double minimumDataValue, double maximumDataValue, double minimumScaleValue, double maximumScaleValue, double interval, int intervalsPerMajorTic, int phase = 0)
        {
            MinimumDataValue = minimumDataValue;
            MaximumDataValue = maximumDataValue;
            MinimumScaleValue = minimumScaleValue;
            MaximumScaleValue = maximumScaleValue;
            Interval = interval;
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
            List<Tic> tics = new List<Tic>();
            for (int i = 0; MinimumScaleValue + Interval * i <= MaximumScaleValue; i++)
                tics.Add(new Tic { Value = MinimumScaleValue + Interval * i, TicType = 0 == i || (i - Phase) % IntervalsPerMajorTic == 0 ? TicType.Major : TicType.Minor }); // Note that the first tic is always a major so that users can always see the minimum value.
            return tics;
        }

        public override string ToString()
        {
            return string.Format("NewLinearAxisScale({0}, ({2}) * {3}, {1})", MinimumScaleValue, MaximumScaleValue, IntervalsPerMajorTic, Interval);
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
