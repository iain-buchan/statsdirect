using StatsDirect.Templates;

namespace StatsDirect.Charting
{
    public class DateAxisScaler: IAxisScaler
    {
        public IAxisScale Q_Axis(double minimumDataValue, double _, double maximumDataValue, bool isYAxis, bool useDataValuesAsScaleValues)
        {
            return new DateAxisScale(minimumDataValue, maximumDataValue, minimumDataValue, maximumDataValue);
        }
    }
}
