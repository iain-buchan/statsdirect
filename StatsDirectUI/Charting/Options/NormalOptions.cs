using System;
using System.Collections.Generic;

namespace StatsDirect.Charting.Options
{
    [Serializable]
    public class NormalOptions : AbstractGenericOptions
        , IAxisLabelFontOptions
        , IAxisTitleFontOptions
        , IBoxAxesOptions
        , IChartTitleOptions
        , IMarkerTypes
    {
        public enum ScoreMethod
        {
            VanDerWaerden = 1,
            Blom = 2,
            ExpectedNormalOrder = 3
        }

        public FillStyle? ForcedFillStyle { get; }
        public bool? ForcedIsFilled { get; }
        public IReadOnlyList<MarkerType> MarkerTypes { get; }
        public ScoreMethod Method { get; }
        public IReadOnlyList<SeriesOptionsDescriptor> SeriesOptions { get; }
        ///  <summary>
        ///  If true, show z scores as z * SD + mean, where mean and SD are the mean and standard deviation of the observed/input values and z are the normal scores.
        ///  If false, show z scores as z.
        ///  </summary>
        ///  <remarks></remarks>
        public bool ShouldScaleZ { get; }

        public NormalOptions(IChartPreferences chartPreferences,
            ScoreMethod method,
            FontDescriptor? axisLabelFontDescriptor = default,
            float? axisLineThickness = default,
            FontDescriptor? axisTitleFontDescriptor = default,
            FontDescriptor? legendFontDescriptor = default,
            ChartOrientation? orientation = default,
            IReadOnlyList<string?>? seriesTitles = default,
            bool? shouldAutoscale = default,
            bool? shouldBoxAxes = default,
            bool? shouldScaleZ = default,
            bool? showLegend = default,
            string? title = default,
            FontDescriptor? titleFontDescriptor = default,
            bool? useColour = default,
            string? xAxisTitle = default,
            string? yAxisTitle = default)
            : base(chartPreferences,
                  axisLabelFontDescriptor,
                  axisLineThickness,
                  axisTitleFontDescriptor,
                  legendFontDescriptor,
                  orientation,
                  seriesTitles,
                  shouldAutoscale,
                  shouldBoxAxes,
                  showLegend,
                  title,
                  titleFontDescriptor,
                  useColour,
                  xAxisTitle,
                  yAxisTitle)
        {
            Method = method;
            ShouldScaleZ = shouldScaleZ ?? false;

            //  A normal plot's marker is derived from the first series
            MarkerTypes = new MarkerType[]
            {
                new(ChartPreferences.MarkerTypes[0], markerSize: 6)
            };

            //  A normal plot has a single series with no lines.
            SeriesOptions = new SeriesOptionsDescriptor[]
            {
                new()
                {
                    SeriesName = "Markers",
                    AllowChangeToDashStyle = false,
                    AllowChangeToLineThickness = false,
                    AllowChangeToLineColour = false,
                    MarkerIndex = 0
                }
            };
        }

        public override bool UsesAutoscale => true;
        public override bool UsesShowLegend => false;
        public override bool ShowLegendIsRelevant => false;

        public override void Accept(IChartOptionVisitor visitor) => visitor.Visit(this);
    }
}
