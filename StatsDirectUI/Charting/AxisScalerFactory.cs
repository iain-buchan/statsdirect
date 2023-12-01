using System;
using StatsDirect.Charting.Scales;
using StatsDirect.Templates;

namespace StatsDirect.Charting
{
    public static class AxisScalerFactory
    {
        public static IAxisScaler? AxisScalerFor(ScaleType scaleType) =>
            scaleType switch
            {
                ScaleType.Category => null,// We don't have an axis scaler for a category axis, but it's legitimate to ask us.
                ScaleType.Date => new DateAxisScaler(),
                ScaleType.Linear => new TalbotLinHanrahanAxisScaler(),
                ScaleType.Log10 => new Log10AxisScaler(),
                ScaleType.LogNatural => new LogNaturalAxisScaler(),
                // case ScaleType.NotSet:
                _ => throw new ArgumentOutOfRangeException(nameof(scaleType), scaleType, "AxisScalerFactory doesn't know how to create an AxisScaler for this scale type"),
            };
    }
}
