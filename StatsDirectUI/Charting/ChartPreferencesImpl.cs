using System.Collections.Generic;

namespace StatsDirect.Charting
{
    internal class ChartPreferencesImpl : IChartPreferences
    {
        public FontDescriptor AxisLabelFont { get; set; }
        public float AxisLineThickness { get; } = 1;
        public FontDescriptor AxisTitleFont { get; set; }
        public bool BoxAxes { get; set; }
        public FontDescriptor LabelFont { get; set; }
        public FontDescriptor LegendFont { get; set; }
        /// <summary>
        /// Default marker types; shared between renderers.
        /// </summary>
        public IList<MarkerType> MarkerTypes { get; set; }
        public bool RequestScaleLimits => false;
        public FontDescriptor SeriesLabelFont => ((IChartPreferences)this).AxisLabelFont;
        public FontDescriptor TitleFont { get; set; }
        public bool UseColour { get; set; }


        // TODO: HACK: Marker stacks are an abomination for histograms and should be removed forthwith.
        private Stack<IList<MarkerType>>? markerTypeStack;

        public ChartPreferencesImpl()
        {
        }

        public void PushAndCloneMarkerTypes()
        {
            IList<MarkerType> originalMarkerTypes = MarkerTypes;
            MarkerTypes = new List<MarkerType>(originalMarkerTypes);
            markerTypeStack ??= new Stack<IList<MarkerType>>();
            markerTypeStack.Push(originalMarkerTypes);
        }

        public void PopMarkerTypes()
        {
            if (markerTypeStack is not null && markerTypeStack.Count > 0)
                MarkerTypes = markerTypeStack.Pop();
        }

        void IChartPreferences.Save()
        {
            throw new System.NotImplementedException();
        }
    }
}
