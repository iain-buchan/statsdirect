using System;
using System.Collections.Generic;

namespace StatsDirect.Templates
{
    public class Log2AxisScale: IAxisScale
    {
        public double MinimumDataValue { get; private set; }
        public double MaximumDataValue { get; private set; }
        public double MinimumScaleValue => Math.Pow(2, MinimumPower);
        public double MaximumScaleValue => Math.Pow(2, MaximumPower);

        /// The number of intervals between tics (one less than the number of tics).  20 intervals = 21 tics - one extra at the end.
        private int MinimumPower { get; set; }
        private int MaximumPower { get; set; }

        public Log2AxisScale(double minimumDataValue, double maximumDataValue, int minimumPower, int maximumPower)
        {
            MinimumDataValue = minimumDataValue;
            MaximumDataValue = maximumDataValue;
            MinimumPower = minimumPower;
            MaximumPower = maximumPower;
        }

        /// <summary>
        /// Returns a linear list of tics constructed according to the parameters.
        /// </summary>
        public IList<Tic> Tics()
        {
            List<Tic> tics = new List<Tic>();
            for (int power = MinimumPower; power <= MaximumPower; power++)
                tics.Add(new Tic { TicType = TicType.Major, Value = Math.Pow(2, power) });
            return tics;
        }
    }
}
