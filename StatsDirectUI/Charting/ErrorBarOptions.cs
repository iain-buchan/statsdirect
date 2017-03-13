using System;
using System.Collections.Generic;

namespace StatsDirect.Charting
{
    [Serializable]
    public class ErrorBarOptions : GenericOptions
    {
        public bool PlotMarkers { get; set; }
        public bool JoinMarkersWithLines { get; set; }
        public List<MultiDoubleSeries> Series { get; set; }
        public bool ShouldCheckForOffsets { get; set; }

        public ErrorBarOptions(bool useColour)
            : base(useColour)
        {
            PlotMarkers = true;
            ShouldCheckForOffsets = true;
        }

        public void SetMarkers()
        {
            MarkerTypes = new List<MarkerType>();
            for (int i = 0; i < Series.Count; i++)
            {
                int mkr = SeriesNumberToMarkerNumber(i);
                MarkerType markerType = ChartPreferences.MarkerTypes[mkr].Clone();
                markerType.MarkerSize = 6;
                MarkerTypes.Add(markerType);

                //  An error plot has series with possible lines.
                SeriesOptionsDescriptor soleOptions = new SeriesOptionsDescriptor
                {
                    SeriesName = Series[i].Title,
                    AllowChangeToDashStyle = true,
                    AllowChangeToLineThickness = true,
                    MarkerIndex = i
                };
                SeriesOptions.Add(soleOptions);
            }
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

        public override bool UsesBoxAxes
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

        public override bool ShowErrorBarOptions
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
                return Series.Count > 1;
            }
        }

        public override bool UsesLegendFontDescriptor
        {
            get
            {
                return true;
            }
        }

        public override void Accept(IChartOptionVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}
