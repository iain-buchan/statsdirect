using System;
using System.Collections.Generic;

namespace StatsDirect.Charting
{
    [Serializable]
    public class SpreadOptions : GenericOptions
    {

        public SpreadOptions(bool UseColour) : base(UseColour)
        {

            MarkerTypes = new List<MarkerType>();
            int mkr = SeriesNumberToMarkerNumber(0);
            MarkerType markerType = Renderer.AbstractChartRenderer.MarkerTypes[mkr].Clone();
            markerType.MarkerSize = 6;
            MarkerTypes.Add(markerType);

            //  A spread plot has a single series with no lines.
            SeriesOptionsDescriptor soleOptions = new SeriesOptionsDescriptor
            {
                SeriesName = "Markers",
                AllowChangeToDashStyle = false,
                AllowChangeToLineThickness = false,
                MarkerIndex = 0
            };
            SeriesOptions.Add(soleOptions);
        }

        public override bool UsesAutoscale
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
                return false;
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

        public override bool UsesOrientation
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
                return false;
            }
        }

        public override bool IsNaturalOrientation
        {
            get
            {
                return Orientation == ChartOrientation.Horizontal;
            }
        }

        public override void Accept(IChartOptionVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}
