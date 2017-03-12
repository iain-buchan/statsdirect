using System;
using System.Collections.Generic;

namespace StatsDirect.Charting
{
    [Serializable]
    public class ROCOptions : GenericOptions
    {

        public bool ShowCutOffCalculator;
        public bool ShowOptimumCutOff;
        public double GAMMA;
        public double Weight;
        public ComparisonValue Showopts;
        private readonly bool showLegendIsRelevant;

        public ROCOptions(bool useColour, IList<Series> seriesToUse) : base(useColour)
        {

            //  The ROC plot uses two series per ROC series.  Series 1 is the markers, series 2 is the optimum cut-off marker.
            //  All the "normal" series are set up first, then all the "optimum cut-off" series.
            MarkerTypes = new List<MarkerType>();
            for (int markerIndex = 0; markerIndex <= seriesToUse.Count - 1; markerIndex++)
            {
                Series series = seriesToUse[markerIndex];
                int mkr = SeriesNumberToMarkerNumber(markerIndex);
                MarkerType markerType = Renderer.AbstractChartRenderer.MarkerTypes[mkr].Clone();
                markerType.MarkerSize = 6;
                MarkerTypes.Add(markerType);

                //  Can change the shape, size and filled/unfilled for series
                SeriesOptionsDescriptor descriptor = new SeriesOptionsDescriptor
                {
                    SeriesName = series.Title,
                    AllowChangeToDashStyle = false,
                    AllowChangeToLineThickness = false,
                    MarkerIndex = MarkerTypes.Count - 1
                };
                SeriesOptions.Add(descriptor);
            }

            //  Now the cut-offs
            for (int markerIndex = 0; markerIndex <= seriesToUse.Count - 1; markerIndex++)
            {
                Series series = seriesToUse[markerIndex];
                int mkr = SeriesNumberToMarkerNumber(markerIndex);
                MarkerType markerType = Renderer.AbstractChartRenderer.MarkerTypes[mkr].Clone();
                //  Increase the size of the optimum cut-off indicators by default
                markerType.MarkerSize = 12;
                MarkerTypes.Add(markerType);

                //  Can change the shape, size and filled/unfilled for series
                SeriesOptionsDescriptor descriptor = new SeriesOptionsDescriptor
                {
                    SeriesName = series.Title + " optimum cut-off",
                    AllowChangeToDashStyle = false,
                    AllowChangeToLineThickness = false,
                    MarkerIndex = MarkerTypes.Count - 1
                };
                SeriesOptions.Add(descriptor);
            }

            showLegendIsRelevant = seriesToUse.Count > 2; //  2 series per ROC series
        }

        public override ChartOptionType OptionType
        {
            get
            {
                return ChartOptionType.ROC;
            }
        }

        public override bool UsesChartTitle
        {
            get
            {
                return true;
            }
        }

        public override bool UsesSeriesLabels
        {
            get
            {
                return true;
            }
        }

        public override bool UsesAxisLabelFontDescriptor
        {
            get
            {
                return true;
            }
        }

        public override bool UsesAxisTitleFontDescriptor
        {
            get
            {
                return true;
            }
        }

        public override bool ShowRocOptions
        {
            get
            {
                return true;
            }
        }

        public override bool UsesLegendFontDescriptor
        {
            get
            {
                return true;
            }
        }

        public override bool ShowLegendIsRelevant
        {
            get
            {
                return showLegendIsRelevant;
            }
        }

    }
}
