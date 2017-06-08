using System.Collections.Generic;

namespace StatsDirect.Templates
{
    public class DateAxisScale: IAxisScale
    {
        public double MinimumDataValue { get; }
        public double MaximumDataValue { get; }
        public double MinimumScaleValue { get; }
        public double MaximumScaleValue { get; }

        public DateAxisScale(double minimumDataValue, double maximumDataValue, double minimumScaleValue, double maximumScaleValue)
        {
            MinimumDataValue = minimumDataValue;
            MaximumDataValue = maximumDataValue;
            MinimumScaleValue = minimumScaleValue;
            MaximumScaleValue = maximumScaleValue;
        }

        public IList<Tic> Tics()
        {
            return new List<Tic>();
        }

        public override string ToString()
        {
            return $"DateAxisScale({MinimumScaleValue}, {MaximumScaleValue})";
        }
    }
}
