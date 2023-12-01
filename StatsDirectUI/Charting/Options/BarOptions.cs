using System.Collections.Generic;

namespace StatsDirect.Charting.Options
{
    public class BarOptions : AbstractGenericOptions
        , IAxisLabelFontOptions
        , IAxisTitleFontOptions
        , IBarOptions
        , IBoxAxesOptions
        , IChartTitleOptions
        , IMarkerTypes
        , IXAxisTitleOptions
        , IYAxisTitleOptions
    {
        private const double DEFAULT_MAX_BAR_WIDTH = 0.5;
        private const bool DEFAULT_ROTATE_WHEN_STACKED = true;
        private const bool DEFAULT_STACKED = false;
        private const bool DEFAULT_STACKED_100_PERCENT = false;

        public FillStyle? ForcedFillStyle { get; }
        public bool? ForcedIsFilled { get; }
        public IReadOnlyList<MarkerType> MarkerTypes { get; }
        public double MaxBarWidth { get; }
        public bool RotateWhenStacked { get; }
        public IReadOnlyList<SeriesOptionsDescriptor> SeriesOptions { get; }
        public override bool ShowLegendIsRelevant { get; }
        public bool Stacked { get; }
        public bool Stacked100Percent { get; }

        public BarOptions(IChartPreferences chartPreferences,
            IReadOnlyList<MarkerType> markerTypes,
            IReadOnlyList<ISeries> seriesToUse,
            FontDescriptor? axisLabelFontDescriptor = default,
            float? axisLineThickness = default,
            FontDescriptor? axisTitleFontDescriptor = default,
            FontDescriptor? legendFontDescriptor = default,
            double? maxBarWidth = default,
            ChartOrientation? orientation = default,
            bool? rotateWhenStacked = default,
            IReadOnlyList<string?>? seriesTitles = default,
            bool? shouldAutoscale = default,
            bool? shouldBoxAxes = default,
            bool? showLegend = default,
            bool? stacked = default,
            bool? stacked100Percent = default,
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
            MarkerTypes = markerTypes;
            MaxBarWidth = maxBarWidth ?? DEFAULT_MAX_BAR_WIDTH;
            RotateWhenStacked = rotateWhenStacked ?? DEFAULT_ROTATE_WHEN_STACKED;
            Stacked = stacked ?? DEFAULT_STACKED;
            Stacked100Percent = stacked100Percent ?? DEFAULT_STACKED_100_PERCENT;

            // Markers will be calculated automatically as required (though we need to force fills); we just need to set up the option descriptors.
            // ShouldForceIsFilled = True
            // ForcedIsFilled = True
            // ShouldForceFillStyle = True
            // ForcedFillStyle = FillStyle.None
            List<SeriesOptionsDescriptor> seriesOptions = new();
            for (int i = 0; i < seriesToUse.Count; i++)
            {
                SeriesOptionsDescriptor soleOptions = new()
                {
                    SeriesName = seriesToUse[i].Title,
                    AllowChangeToMarkerSize = false,
                    AllowChangeToMarkerType = false,
                    AllowChangeToDashStyle = true,
                    AllowChangeToLineThickness = true,
                    AllowChangeToFill = true,
                    MarkerIndex = i
                };
                seriesOptions.Add(soleOptions);
            }
            SeriesOptions = seriesOptions;
            ShowLegendIsRelevant = Stacked || seriesToUse.Count > 1;
        }

        public BarOptions(
            AbstractGenericOptions genericOptions,
            IReadOnlyList<MarkerType> markerTypes,
            List<SeriesOptionsDescriptor> seriesOptions,
            double? maxBarWidth = default,
            bool? rotateWhenStacked = default,
            bool? stacked = default,
            bool? stacked100Percent = default)
            : base(genericOptions)
        {
            MarkerTypes = markerTypes;
            MaxBarWidth = maxBarWidth ?? DEFAULT_MAX_BAR_WIDTH;
            RotateWhenStacked = rotateWhenStacked ?? DEFAULT_ROTATE_WHEN_STACKED;
            Stacked = stacked ?? DEFAULT_STACKED;
            SeriesOptions = seriesOptions;
            ShowLegendIsRelevant = Stacked || seriesOptions.Count > 1;
            Stacked100Percent = stacked100Percent ?? DEFAULT_STACKED_100_PERCENT;
        }

        public override bool IsNaturalOrientation => Orientation == ChartOrientation.Vertical;
        public override string OrientationLabel => "Bar orientation";
        public override bool UsesAutoscale => true;
        public override bool UsesOrientation => true;

        public override void Accept(IChartOptionVisitor visitor) => visitor.Visit(this);
    }
}
