using StatsDirect.Templates;

namespace StatsDirect.Charting
{
    public interface IAxisScaler
    {
        IAxisScale Q_Axis(double minimumDataValue, double minimumDataValueGreaterThanZero, double maximumDataValue, bool isYAxis, bool useDataValuesAsScaleValues);
    }
}