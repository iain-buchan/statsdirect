using System;
using System.Collections.Generic;
using System.Linq;

namespace StatsDirect.Templates
{
    public class Log10AxisScale: IAxisScale
    {
        public double MinimumDataValue { get; private set; }
        public double MaximumDataValue { get; private set; }
        public double MinimumScaleValue { get { return Math.Pow(10, MinimumPower) * MinimumScaleTicMultiplier; } }
        public double MaximumScaleValue { get { return Math.Pow(10, MaximumPower - 1) * MaximumScaleTicMultiplier; } }
        private int MinimumPower { get; set; }
        private int MinimumScaleTicMultiplier { get; set; }
        private int MaximumPower { get; set; }
        private int MaximumScaleTicMultiplier { get; set; }
        private IList<int> MinorTicMultipliers { get; set; }

        public Log10AxisScale(double minimumDataValue, double maximumDataValue, int minimumPower, int minimumScaleTicMultiplier, int maximumPower, int maximumScaleTicMultiplier, IList<int> minorTicMultipliers)
        {
            MinimumDataValue = minimumDataValue;
            MaximumDataValue = maximumDataValue;
            MinimumPower = minimumPower;
            MinimumScaleTicMultiplier = minimumScaleTicMultiplier;
            MaximumPower = maximumPower;
            MaximumScaleTicMultiplier = maximumScaleTicMultiplier;
            MinorTicMultipliers = minorTicMultipliers;
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
                if (basePower >= MinimumScaleValue && basePower <= MaximumScaleValue)
                    tics.Add(new Tic { TicType = TicType.Major, Value = basePower });
                foreach (double multiplier in MinorTicMultipliers)
                {
                    double ticValue = basePower * multiplier;
                    if (ticValue >= MinimumScaleValue && ticValue <= MaximumScaleValue)
                        tics.Add(new Tic { TicType = TicType.Major, Value = ticValue });
                }
            }
            double lastMajorTicValue = Math.Pow(10, MaximumPower);
            if (lastMajorTicValue >= MinimumScaleValue && lastMajorTicValue <= MaximumScaleValue)
                tics.Add(new Tic { TicType = TicType.Major, Value = lastMajorTicValue });
            return tics;
        }
    }
}
