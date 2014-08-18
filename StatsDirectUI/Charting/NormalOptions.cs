using System;
using System.Collections.Generic;

namespace StatsDirect.Charting
{
    [Serializable]
    public class NormalOptions : GenericOptions
    {
        public enum ScoreMethod
        {
            VanDerWaerden = 1,
            Blom = 2,
            ExpectedNormalOrder = 3,
        }

        public ScoreMethod Method { get; set; }
        ///  <summary>
        ///  If true, show z scores as z * SD + mean, where mean and SD are the mean and standard deviation of the observed/input values and z are the normal scores.
        ///  If false, show z scores as z.
        ///  </summary>
        ///  <remarks></remarks>
        public bool Scaling { get; set; }

        public NormalOptions(bool UseColour)
            : base(UseColour)
        {

            //  A normal plot's marker is derived from the first series
            MarkerTypes = new List<MarkerType>();
            MarkerType markerType = ChartRenderer.MarkerTypes[0].Clone();
            markerType.MarkerSize = 6;
            MarkerTypes.Add(markerType);

            //  A normal plot has a single series with no lines.
            SeriesOptionsDescriptor soleOptions = new SeriesOptionsDescriptor
            {
                SeriesName = "Markers",
                AllowChangeToDashStyle = false,
                AllowChangeToLineThickness = false,
                AllowChangeToLineColour = false,
                MarkerIndex = 0
            };
            SeriesOptions.Add(soleOptions);
        }

        public override ChartOptionType OptionType
        {
            get
            {
                return ChartOptionType.Normal;
            }
        }

        public override bool UsesChartTitle
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

        public override bool UsesBoxAxes
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

        public override bool ShowNormalOptions
        {
            get
            {
                return true;
            }
        }

        public override bool UsesShowLegend
        {
            get
            {
                return false;
            }
        }

        public override bool ShowLegendIsRelevant
        {
            get
            {
                return false;
            }
        }
    }
}
