using System;
using System.Collections.Generic;

namespace StatsDirect.Charting
{
    [Serializable]
    public class ErrorBarOptions : GenericOptions
    {
        public bool PlotMarkers { get; set; }
        public Data.DataFrame ydat { get; set; }
        public Data.DataFrame xdat { get; set; }
        public Data.DataFrame ydatl { get; set; }
        public Data.DataFrame ydatu { get; set; }
        public bool JoinMarkersWithLines { get; set; }

        public ErrorBarOptions(bool useColour)
            : base(useColour)
        {
            PlotMarkers = true;
        }

        public void SetMarkers()
        {
            MarkerTypes = new List<MarkerType>();
            for (int i = 0; i < ydat.VariableCount; i++)
            {
                int mkr = SeriesNumberToMarkerNumber(i);
                MarkerType markerType = ChartRenderer.MarkerTypes[mkr].Clone();
                markerType.MarkerSize = 6;
                MarkerTypes.Add(markerType);

                //  An error plot has series with possible lines.
                SeriesOptionsDescriptor soleOptions = new SeriesOptionsDescriptor
                {
                    SeriesName = ydat.Variables[i].Title,
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

        public override ChartOptionType OptionType
        {
            get
            {
                return ChartOptionType.ErrorBars;
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
                return ydat.VariableCount > 1;
            }
        }

        public override bool UsesLegendFontDescriptor
        {
            get
            {
                return true;
            }
        }
    }
}
