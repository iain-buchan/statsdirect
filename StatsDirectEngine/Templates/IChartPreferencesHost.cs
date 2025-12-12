using StatsDirect.Charting;

namespace StatsDirect.Templates
{
    /// <summary>
    /// Data needed by charting that can only be known by the host.
    /// </summary>
    public interface IChartPreferencesHost
    {
        IChartPreferences ChartPreferences { get; }
    }
}
