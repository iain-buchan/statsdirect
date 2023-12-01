using System;
using System.Collections.Generic;

namespace StatsDirect.Charting.Options
{
    [Serializable]
    public class ErrorBarOptions : AbstractGenericOptions
        , IAxisLabelFontOptions
        , IAxisTitleFontOptions
        , IBoxAxesOptions
        , IChartTitleOptions
        , ILegendFontOptions
        , IMarkerTypes
        , ISeriesTitlesOptions
        , IXAxisTitleOptions
        , IYAxisTitleOptions
    {
        public FillStyle? ForcedFillStyle { get; }
        public bool? ForcedIsFilled { get; }
        public bool JoinMarkersWithLines { get; }
        public IReadOnlyList<MarkerType> MarkerTypes { get; }
        public bool PlotMarkers { get; }
        public IReadOnlyList<MultiDoubleSeries> Series { get; }
        public IReadOnlyList<SeriesOptionsDescriptor> SeriesOptions { get; }
        public bool ShouldCheckForOffsets { get; }

        public ErrorBarOptions(IChartPreferences chartPreferences,
            IReadOnlyList<MultiDoubleSeries> series,
            FontDescriptor? axisLabelFontDescriptor = default,
            float? axisLineThickness = default,
            FontDescriptor? axisTitleFontDescriptor = default,
            bool? joinMarkersWithLines = default,
            FontDescriptor? legendFontDescriptor = default,
            ChartOrientation? orientation = default,
            bool? plotMarkers = default,
            IReadOnlyList<string?>? seriesTitles = default,
            bool? shouldAutoscale = default,
            bool? shouldBoxAxes = default,
            bool? shouldCheckForOffsets = default,
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
            JoinMarkersWithLines = joinMarkersWithLines ?? false;
            PlotMarkers = plotMarkers ?? true;
            Series = series;
            ShouldCheckForOffsets = shouldCheckForOffsets ?? true;

            // Set markers
            MarkerType[] markerTypes = new MarkerType[Series.Count];
            SeriesOptionsDescriptor[] seriesOptions = new SeriesOptionsDescriptor[Series.Count];
            for (int i = 0; i < Series.Count; i++)
            {
                markerTypes[i] = new(
                    ChartPreferences.MarkerTypes[SeriesNumberToMarkerNumber(i)],
                    markerSize: 6
                );

                //  An error plot has series with possible lines.
                seriesOptions[i] = new()
                {
                    SeriesName = Series[i].Title,
                    AllowChangeToDashStyle = true,
                    AllowChangeToLineThickness = true,
                    MarkerIndex = i
                };
            }
            MarkerTypes = markerTypes;
            SeriesOptions = seriesOptions;
        }

        public override bool ShowLegendIsRelevant => Series.Count > 1;
        public override bool UsesAutoscale => true;

        public override void Accept(IChartOptionVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}
