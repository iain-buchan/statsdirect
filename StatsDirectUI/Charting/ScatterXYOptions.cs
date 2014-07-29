using System;
using System.Collections.Generic;

namespace StatsDirect.Charting
{
    [Serializable]
    public class ScatterXYOptions : GenericOptions
    {

        public bool PlotMarkers { get; set; }
        public bool IsAscii { get; set; }
        private readonly bool showLegendIsRelevant;
        public bool JoinMarkersWithLines { get; set; }

        public ScatterXYOptions(bool useColour, IList<Series> xSeries, bool useLines)
            : base(useColour)
        {
            JoinMarkersWithLines = useLines;
            PlotMarkers = true;

            MarkerTypes = new List<MarkerType>();
            for (int i = 0; i <= xSeries.Count - 1; i++)
            {
                int mkr = SeriesNumberToMarkerNumber(i);
                MarkerType markerType = ChartRenderer.MarkerTypes[mkr].Clone();
                markerType.MarkerSize = 6;
                MarkerTypes.Add(markerType);

                //  A scatter plot has series with no lines.
                SeriesOptionsDescriptor soleOptions = new SeriesOptionsDescriptor
                                                                               {
                                                                                   SeriesName = xSeries[i].Title,
                                                                                   AllowChangeToDashStyle = useLines,
                                                                                   AllowChangeToLineColour = useLines,
                                                                                   AllowChangeToLineThickness = useLines,
                                                                                   MarkerIndex = i
                                                                               };
                SeriesOptions.Add(soleOptions);
            }
            showLegendIsRelevant = xSeries.Count > 1;
        }

        public override bool UsesChartTitle
        {
            get
            {
                return true;
            }
        }

        public override bool UsesXAxisTitle
        {
            get
            {
                return true;
            }
        }

        public override bool UsesYAxisTitle
        {
            get
            {
                return true;
            }
        }

        public override bool UsesAutoscale
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

        public override bool UsesSeriesLabels
        {
            get
            {
                return true;
            }
        }

        public override ChartOptionType OptionType
        {
            get
            {
                return ChartOptionType.ScatterXY;
            }
        }

        public override bool ShowScatterXYOptions
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
