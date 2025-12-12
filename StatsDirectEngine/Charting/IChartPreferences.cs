namespace StatsDirect.Charting
{
    public interface IChartPreferences
    {
        FontDescriptor AxisLabelFont { get; }
        FontDescriptor AxisTitleFont { get; }
        bool BoxAxes { get; }
        FontDescriptor LabelFont { get; }
        FontDescriptor LegendFont { get; }
        bool RequestScaleLimits { get; }
        FontDescriptor SeriesLabelFont { get; }
        FontDescriptor TitleFont { get; }
        MarkerType[] MarkerTypes { get; }
        bool ShouldUseColour { get; }

        void PopMarkerTypes(); // TODO: Should not be here!
        void PushAndCloneMarkerTypes(); // TODO: Should not be here!
    }
}