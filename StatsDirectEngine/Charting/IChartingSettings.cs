namespace StatsDirect.Charting
{
    /// <summary>
    /// Settings and user preferences required for charting that should be provided by the host.
    /// TODO: How does this interact with ChartPreferences?  How should it interact?
    /// </summary>
    public interface IChartingSettings
    {
        bool BlackAndWhite { get; }
        bool BoxAxes { get; }
        string LabelFont { get; }
        string Markers { get; }
        bool RequestScaleLimits { get; }
        string TitleFont { get; }

        void Save();
    }
}
