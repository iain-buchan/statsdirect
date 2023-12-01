using SpreadsheetGear.Charts;
using System;
using System.Collections.Generic;
using static StatsDirect.Charting.Options.NormalOptions;

namespace StatsDirect.Charting.Options
{
    [Serializable]
    public class SpreadOptions : AbstractGenericOptions
        , IAxisLabelFontOptions
        , IAxisTitleFontOptions
        , IBoxAxesOptions
        , IChartTitleOptions
        , IMarkerTypes
        , ISeriesTitlesOptions
        , IXAxisTitleOptions
    {
        public FillStyle? ForcedFillStyle { get; }
        public bool? ForcedIsFilled { get; }
        public IReadOnlyList<MarkerType> MarkerTypes { get; }
        public IReadOnlyList<SeriesOptionsDescriptor> SeriesOptions { get; }

        public SpreadOptions(IChartPreferences chartPreferences,
            FontDescriptor? axisLabelFontDescriptor = default,
            float? axisLineThickness = default,
            FontDescriptor? axisTitleFontDescriptor = default,
            FontDescriptor? legendFontDescriptor = default,
            ChartOrientation? orientation = default,
            IReadOnlyList<string?>? seriesTitles = default,
            bool? shouldAutoscale = default,
            bool? shouldBoxAxes = default,
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

            MarkerTypes = new MarkerType[]
            {
                new(ChartPreferences.MarkerTypes[SeriesNumberToMarkerNumber(0)], markerSize: 6)
            };

            //  A spread plot has a single series with no lines.
            SeriesOptions = new SeriesOptionsDescriptor[]
            {
                new()
                {
                    SeriesName = "Markers",
                    AllowChangeToDashStyle = false,
                    AllowChangeToLineThickness = false,
                    MarkerIndex = 0
                }
            };
        }

        public override bool UsesAutoscale => true;
        public override bool UsesOrientation => true;
        public override bool ShowLegendIsRelevant => false;

        public override bool IsNaturalOrientation => Orientation == ChartOrientation.Horizontal;

        public override void Accept(IChartOptionVisitor visitor) => visitor.Visit(this);
    }
}
