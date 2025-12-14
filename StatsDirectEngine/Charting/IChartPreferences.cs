using System.Collections.Generic;

namespace StatsDirect.Charting
{
    public interface IChartPreferences
    {
        FontDescriptor AxisLabelFont { get; }
        FontDescriptor AxisTitleFont { get; }
        bool BlackAndWhite { get; }
        bool BoxAxes { get; }
        FontDescriptor LabelFont { get; }
        FontDescriptor LegendFont { get; }
        bool RequestScaleLimits { get; }
        FontDescriptor TitleFont { get; }
        IReadOnlyList<MarkerType> MarkerTypes { get; }
        /// <remarks>The old "marker type 10", now moved out of the array and hence potentially allowing for expansion of the marker types.</remarks>
        MarkerType FixedMarkerType { get; }

        void PopMarkerTypes(); // TODO: Should not be here!
        void PushAndCloneMarkerTypes(); // TODO: Should not be here!
    }
}