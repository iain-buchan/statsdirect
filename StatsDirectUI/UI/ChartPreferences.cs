using StatsDirect.Charting;
using System.Collections.Generic;

namespace StatsDirect.UI
{
    /// <summary>
    /// The Windows UI's implementation of IChartPreferences, capable of load, save, and amendment.
    /// </summary>
    public class ChartPreferences : IChartPreferences
    {
        public FontDescriptor AxisLabelFont { get; set; }
        public FontDescriptor AxisTitleFont { get; set; }
        public bool BlackAndWhite { get; set; }
        public bool BoxAxes { get; set; }
        public FontDescriptor LabelFont { get; set; }
        public FontDescriptor LegendFont { get; set; }
        public MarkerType[] MarkerTypes { get; set; }
        public bool RequestScaleLimits => false;
        public FontDescriptor SeriesLabelFont => AxisLabelFont;
        bool IChartPreferences.ShouldUseColour => !BlackAndWhite;
        public FontDescriptor TitleFont { get; set; }

        // TODO: Marker stacks are an abomination for histograms and should be removed forthwith.
        private Stack<MarkerType[]> markerTypeStack;

        void IChartPreferences.PushAndCloneMarkerTypes()
        {
            MarkerType[] originalMarkerTypes = MarkerTypes;
            MarkerTypes = new MarkerType[MarkerTypes.Length];
            for (int i = 0; i < MarkerTypes.Length; i++)
                MarkerTypes[i] = originalMarkerTypes[i].Clone();
            if (null == markerTypeStack)
                markerTypeStack = new Stack<MarkerType[]>();
            markerTypeStack.Push(originalMarkerTypes);
        }

        void IChartPreferences.PopMarkerTypes()
        {
            if (null != markerTypeStack && markerTypeStack.Count > 0)
                MarkerTypes = markerTypeStack.Pop();
        }
    }
}
