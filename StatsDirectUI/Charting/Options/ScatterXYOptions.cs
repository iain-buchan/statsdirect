using System;
using System.Collections.Generic;

namespace StatsDirect.Charting.Options
{
    /// <remarks>
    /// Immutable
    /// </remarks>
    [Serializable]
    public class ScatterXYOptions : AbstractGenericOptions
        , IAxisLabelFontOptions
        , IAxisTitleFontOptions
        , IChartTitleOptions
        , IMarkerTypes
        , ISeriesTitlesOptions
        , IXAxisTitleOptions
        , IYAxisTitleOptions
    {
        public FillStyle? ForcedFillStyle { get; }
        public bool? ForcedIsFilled { get; }
        public bool IsAscii { get; }
        public bool JoinMarkersWithLines { get; }
        public IReadOnlyList<MarkerType> MarkerTypes { get; }
        public bool PlotMarkers { get; }
        public IReadOnlyList<SeriesOptionsDescriptor> SeriesOptions { get; }

        private readonly bool showLegendIsRelevant;

        /// <param name="markerTypes">If null, calculated internally from chart preferences; override by passing in existing marker types if desired</param>
        public ScatterXYOptions(IChartPreferences chartPreferences,
            IReadOnlyList<ISeries> xSeries,
            FontDescriptor? axisLabelFontDescriptor = default,
            float? axisLineThickness = default,
            FontDescriptor? axisTitleFontDescriptor = default,
            bool? isAscii = default,
            bool? joinMarkersWithLines = default,
            FontDescriptor? legendFontDescriptor = default,
            IReadOnlyList<MarkerType>? markerTypes = default,
            ChartOrientation? orientation = default,
            bool? plotMarkers = default,
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
            IsAscii = isAscii ?? false;
            JoinMarkersWithLines = joinMarkersWithLines ?? false;
            PlotMarkers = plotMarkers ?? true;

            if (markerTypes is not null)
                MarkerTypes = markerTypes;
            else
            {
                List<MarkerType> defaultMarkerTypes = new();
                for (int i = 0; i < xSeries.Count; i++)
                    defaultMarkerTypes.Add(new(chartPreferences.MarkerTypes[SeriesNumberToMarkerNumber(i)], markerSize: 6));
                MarkerTypes = defaultMarkerTypes;
            }

            List<SeriesOptionsDescriptor> seriesOptionsDescriptors = new();
            for (int i = 0; i < xSeries.Count; i++)
            {
                //  A scatter plot has series with no lines.
                SeriesOptionsDescriptor soleOptions = new()
                {
                    SeriesName = xSeries[i].Title,
                    AllowChangeToDashStyle = JoinMarkersWithLines,
                    AllowChangeToLineColour = JoinMarkersWithLines,
                    AllowChangeToLineThickness = JoinMarkersWithLines,
                    MarkerIndex = i
                };
                seriesOptionsDescriptors.Add(soleOptions);
            }
            SeriesOptions = seriesOptionsDescriptors;
            showLegendIsRelevant = xSeries.Count > 1;
        }

        public override bool UsesAutoscale => true;

        public override bool ShowLegendIsRelevant => showLegendIsRelevant;

        public override void Accept(IChartOptionVisitor visitor) => visitor.Visit(this);
    }
}
