using System;
using System.Collections.Generic;

namespace StatsDirect.Charting.Options
{
    [Serializable]
    public class ForestOptions : ForestishOptions
        , IAxisLabelFontOptions
        , IAxisTitleFontOptions
        , IChartTitleOptions
        , IXAxisTitleOptions
    {
        private const double DEFAULT_CCO = 0.95;

        public IReadOnlyList<double>? Pg { get; } // Should really be integer or bool but assigning a double variable is as efficient
        public int EffectSizeAndIntervalDecimalPlaces { get; }

        public ForestOptions(IChartPreferences chartPreferences,
            int effectSizeAndIntervalDecimalPlaces,
            IReadOnlyList<double> groupSizes,
            int k,
            IReadOnlyList<double> oddsRatioLcis,
            IReadOnlyList<double> oddsRatios,
            IReadOnlyList<double> oddsRatioUcis,
            IReadOnlyList<double>? pg,
            bool shouldAutoscale,
            string? title,
            IReadOnlyList<string> titles,
            string? xAxisTitle,
            double cco = DEFAULT_CCO,
            bool? markCentres = default,
            IReadOnlyList<MarkerType>? markerTypes = default,
            IReadOnlyList<SeriesOptionsDescriptor>? seriesOptions = default)
            : base(chartPreferences, cco, groupSizes, k, markCentres, markerTypes, oddsRatioLcis, oddsRatios, oddsRatioUcis, seriesOptions, shouldAutoscale, title, titles, xAxisTitle)
        {
            EffectSizeAndIntervalDecimalPlaces = effectSizeAndIntervalDecimalPlaces;
            Pg = pg;
        }

        public override void Accept(IChartOptionVisitor visitor) => visitor.Visit(this);
    }
}
