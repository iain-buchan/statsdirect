using StatsDirect.Templates;

namespace StatsDirect.Charting
{
    public interface IAxisScaler
    {
        IAxisScale Q_Axis(double qMin, double qMinGreaterThanZero, double qMax);
    }
}