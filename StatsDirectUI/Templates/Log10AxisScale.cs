using System;
using System.Collections.Generic;
using System.Linq;

namespace StatsDirect.Templates
{
    public class Log10AxisScale: IAxisScale
    {
        public double MinimumDataValue { get; private set; }
        public double MaximumDataValue { get; private set; }
        public double MinimumScaleValue { get { return Math.Pow(10, MinimumPower); } }
        public double MaximumScaleValue { get { return Math.Pow(10, MaximumPower); } }
        /// The number of intervals between tics (one less than the number of tics).  20 intervals = 21 tics - one extra at the end.
        private int MinimumPower { get; set; }
        private int MaximumPower { get; set; }
        private double[] MinorTicMultipliers { get; set; }

        public Log10AxisScale(double minimumDataValue, double maximumDataValue, int minimumPower, int maximumPower, IList<double> minorTicMultipliers)
        {
            MinimumDataValue = minimumDataValue;
            MaximumDataValue = maximumDataValue;
            MinimumPower = minimumPower;
            MaximumPower = maximumPower;
            MinorTicMultipliers = minorTicMultipliers.ToArray();
        }

        /// <summary>
        /// Returns a linear list of tics constructed according to the parameters.
        /// </summary>
        public IList<Tic> Tics()
        {
            List<Tic> tics = new List<Tic>();
            for (int power = MinimumPower; power < MaximumPower; power++)
            {
                double basePower = Math.Pow(10, power);
                tics.Add(new Tic { TicType = TicType.Major, Value = basePower });
                foreach (double multiplier in MinorTicMultipliers)
                    tics.Add(new Tic { TicType = TicType.Minor, Value = basePower * multiplier });
            }
            tics.Add(new Tic { TicType = TicType.Major, Value = Math.Pow(10, MaximumPower) });
            return tics;
        }
    }
}
