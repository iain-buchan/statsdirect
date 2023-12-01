using System;
using System.Collections.Generic;

namespace StatsDirect.Charting.Options
{
    [Serializable]
    public class ROCOptions : AbstractGenericOptions
        , IAxisLabelFontOptions
        , IAxisTitleFontOptions
        , IChartTitleOptions
        , ILegendFontOptions
        , IMarkerTypes
        , ISeriesTitlesOptions
    {
        public Comparison Comparison { get; }
        public FillStyle? ForcedFillStyle { get; }
        public bool? ForcedIsFilled { get; }
        public double Gamma { get; }
        public IReadOnlyList<SeriesOptionsDescriptor> SeriesOptions { get; }
        public bool ShowCutOffCalculator { get; }
        public bool ShowOptimumCutOff { get; }
        public double Weight { get; }

        private readonly bool showLegendIsRelevant;

        public ROCOptions(IChartPreferences chartPreferences,
            IReadOnlyList<ISeries> seriesToUse,
            Comparison comparison,
            double gamma,
            bool showCutOffCalculator,
            bool showOptimumCutOff,
            double weight,
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
            Comparison = comparison;
            Gamma = gamma;
            ShowCutOffCalculator = showCutOffCalculator;
            ShowOptimumCutOff = showOptimumCutOff;
            Weight = weight;

            //  The ROC plot uses two series per ROC series.  Series 1 is the markers, series 2 is the optimum cut-off marker.
            //  All the "normal" series are set up first, then all the "optimum cut-off" series.
            MarkerType[] markerTypes = new MarkerType[seriesToUse.Count * 2];
            SeriesOptionsDescriptor[] seriesOptions = new SeriesOptionsDescriptor[seriesToUse.Count * 2];
            for (int markerIndex = 0; markerIndex < seriesToUse.Count; markerIndex++)
            {
                markerTypes[markerIndex] = new(ChartPreferences.MarkerTypes[SeriesNumberToMarkerNumber(markerIndex)], markerSize: 6);

                // Can change the shape, size and filled/unfilled for series
                ISeries series = seriesToUse[markerIndex];
                SeriesOptionsDescriptor descriptor = new()
                {
                    SeriesName = series.Title,
                    AllowChangeToDashStyle = false,
                    AllowChangeToLineThickness = false,
                    MarkerIndex = markerIndex
                };
                seriesOptions[markerIndex] = descriptor;
            }

            //  Now the cut-offs
            for (int markerIndex = 0; markerIndex < seriesToUse.Count; markerIndex++)
            {
                int offset = markerIndex + seriesToUse.Count;
                //  Increase the size of the optimum cut-off indicators by default
                markerTypes[offset] = new MarkerType(markerTypes[markerIndex], markerSize: 12);

                //  Can change the shape, size and filled/unfilled for series
                ISeries series = seriesToUse[markerIndex];
                SeriesOptionsDescriptor descriptor = new()
                {
                    SeriesName = $"{series.Title} optimum cut-off",
                    AllowChangeToDashStyle = false,
                    AllowChangeToLineThickness = false,
                    MarkerIndex = offset
                };
                seriesOptions[offset] = descriptor;
            }

            MarkerTypes = markerTypes;
            SeriesOptions = seriesOptions;
            showLegendIsRelevant = seriesToUse.Count > 2; //  2 series per ROC series
        }

        public override bool ShowLegendIsRelevant => showLegendIsRelevant;

        public IReadOnlyList<MarkerType> MarkerTypes { get; }

        public override void Accept(IChartOptionVisitor visitor) => visitor.Visit(this);
    }
}
