using System;
using StatsDirect.Templates;

namespace StatsDirect.Charting
{
    public static class AxisScalerFactory
    {
        public static IAxisScaler AxisScalerFor(ScaleType scaleType)
        {
            switch (scaleType)
            {
                case ScaleType.Category:
                    throw new NotImplementedException("Axis scales are only for scale axes - AxisScalerFactory should never be asked for a scaler for a category axis");
                case ScaleType.Date: // TODO: Eventually dates should use a different scaler as e.g. weeks, months might be useful intervals.
                case ScaleType.Linear:
                    return new TalbotLinHanrahanAxisScaler();
                case ScaleType.Log10:
                    return new Log10AxisScaler();
                case ScaleType.LogNatural:
                    return new LogNaturalAxisScaler();
                case ScaleType.NotSet:
                default:
                    throw new ArgumentOutOfRangeException(nameof(scaleType), scaleType, "AxisScalerFactory doesn't know how to create an AxisScaler for this scale type");
            }
        }
    }
}
