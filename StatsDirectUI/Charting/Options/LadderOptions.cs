using System;
using System.Collections.Generic;

namespace StatsDirect.Charting.Options
{
    [Serializable]
    public class LadderOptions : AbstractGenericOptions
        , IAxisLabelFontOptions
        , IAxisTitleFontOptions
        , IBoxAxesOptions
        , IChartTitleOptions
        , IMarkerTypes
        , ISeriesTitlesOptions
        , IYAxisTitleOptions
    {
        public FillStyle? ForcedFillStyle { get; }
        public bool? ForcedIsFilled { get; }
        public IReadOnlyList<MarkerType> MarkerTypes { get; }
        public IReadOnlyList<SeriesOptionsDescriptor> SeriesOptions { get; }

        public LadderOptions(IChartPreferences chartPreferences,
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
            //  A ladder plot's left and right markers are derived from the first two series.
            MarkerTypes = new MarkerType[]
            {
                new MarkerType(ChartPreferences.MarkerTypes[0], markerSize: 6),
                new MarkerType(ChartPreferences.MarkerTypes[1], markerSize: 6)
            };

            //  A ladder plot has a left-hand and a right-hand series, connected by a line.
            //  The line uses the left-hand marker's line type and thickness
            SeriesOptionsDescriptor leftHandOptions = new()
            {
                SeriesName = "Left hand markers",
                AllowChangeToDashStyle = false,
                AllowChangeToLineThickness = false,
                AllowChangeToLineColour = false,
                MarkerIndex = 0
            };
            SeriesOptionsDescriptor ladderRungOptions = new()
            {
                SeriesName = "Ladder rungs",
                AllowChangeToMarkerColour = false,
                AllowChangeToMarkerSize = false,
                AllowChangeToMarkerType = false,
                MarkerIndex = 0
            };
            SeriesOptionsDescriptor rightHandOptions = new()
            {
                SeriesName = "Right hand markers",
                AllowChangeToDashStyle = false,
                AllowChangeToLineThickness = false,
                AllowChangeToLineColour = false,
                MarkerIndex = 1
            };
            SeriesOptions = new[]
            {
                leftHandOptions,
                ladderRungOptions,
                rightHandOptions
            };
        }

        public override bool UsesAutoscale => true;
        public override bool ShowLegendIsRelevant => false;

        public override void Accept(IChartOptionVisitor visitor) => visitor.Visit(this);
    }
}
