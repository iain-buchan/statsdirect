using System.Collections.Generic;

namespace StatsDirect.Charting
{
    public interface IChartPreferences
    {
        FontDescriptor AxisLabelFont { get; set; }
        float AxisLineThickness { get; }
        FontDescriptor AxisTitleFont { get; set; }
        bool BoxAxes { get; set; }
        FontDescriptor LabelFont { get; set; }
        FontDescriptor LegendFont { get; set; }
        bool RequestScaleLimits { get; }
        FontDescriptor SeriesLabelFont { get; }
        FontDescriptor TitleFont { get; set; }
        IList<MarkerType> MarkerTypes { get; set; }
        bool UseColour { get; set; }

        public void Save();

        public void PushAndCloneMarkerTypes();
        public void PopMarkerTypes();
    }
}
