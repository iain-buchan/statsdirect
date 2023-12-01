using System;
using System.Collections.Generic;
using StatsDirect.Utilities;

namespace StatsDirect.Charting.Options
{
    public partial class BoxWhiskerOptions : AbstractGenericOptions
        , IAxisLabelFontOptions
        , IAxisTitleFontOptions
        , IBoxAxesOptions
        , IBoxWhiskerOptions
        , IChartTitleOptions
        , IIsAsciiOptions
        , ISeriesTitlesOptions
        , IXAxisTitleOptions
    {
        public double Cco { get; }
        public bool IsAscii { get; }
        public bool MarkMeanAndMedian { get; }
        public BoxWhiskerMethod Method { get; }
        public bool UseInnerFence { get; }
        public bool UseOuterFence { get; }

        public BoxWhiskerOptions(IChartPreferences chartPreferences,
            FontDescriptor? axisLabelFontDescriptor = default,
            float? axisLineThickness = default,
            FontDescriptor? axisTitleFontDescriptor = default,
            double? cco = default,
            bool? isAscii = default,
            FontDescriptor? legendFontDescriptor = default,
            bool? markMeanAndMedian = default,
            BoxWhiskerMethod? method = default,
            ChartOrientation? orientation = default,
            IReadOnlyList<string?>? seriesTitles = default,
            bool? shouldAutoscale = default,
            bool? shouldBoxAxes = default,
            bool? showLegend = default,
            string? title = default,
            FontDescriptor? titleFontDescriptor = default,
            bool? useColour = default,
            bool? useInnerFence = default,
            bool? useOuterFence = default,
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
                  xAxisTitle ?? DefaultXAxisTitle(cco ?? 0.95, markMeanAndMedian ?? false, method ?? BoxWhiskerMethod.MedianQuartilesRange, useInnerFence ?? false, useOuterFence ?? false),
                  yAxisTitle)
        {
            Cco = cco ?? 0.95;
            IsAscii = isAscii ?? false;
            MarkMeanAndMedian = markMeanAndMedian ?? false;
            Method = method ?? BoxWhiskerMethod.MedianQuartilesRange;
            UseInnerFence = useInnerFence ?? false;
            UseOuterFence = useOuterFence ?? false;
        }

        public BoxWhiskerOptions(
            AbstractGenericOptions genericOptions,
            double cco = default,
            bool isAscii = default,
            bool markMeanAndMedian = default,
            BoxWhiskerMethod method = default,
            bool useInnerFence = default,
            bool useOuterFence = default)
            : base(genericOptions)
        {
            Cco = cco;
            IsAscii = isAscii;
            MarkMeanAndMedian = markMeanAndMedian;
            Method = method;
            UseInnerFence = useInnerFence;
            UseOuterFence = useOuterFence;
        }

        private static string DefaultXAxisTitle(double cco, bool markMeanAndMedian, BoxWhiskerMethod method, bool useInnerFence, bool useOuterFence)
        {
            string xAxisTitle;
            //  This used to try to be cleverer, but it turns out that formatting for each combination is almost essential to allow variation.
            switch (method)
            {
                case BoxWhiskerMethod.MedianQuartilesRange:
                    xAxisTitle = useInnerFence
                        ? useOuterFence ? "min < LQ < median%MEAN% > UQ > max, fences (1.5 & 3.0 IQR)" : "min < LQ < median%MEAN% > UQ > max, fence (1.5 IQR)"
                        : useOuterFence ? "min < LQ < median%MEAN% > UQ > max, fence (3.0 IQR)" : "min < LQ < median%MEAN% > UQ > max";
                    break;
                case BoxWhiskerMethod.MeanStandardDeviationRange:
                    xAxisTitle = useInnerFence
                        ? useOuterFence ? "min < 1 SD < mean%MEDIAN% > 1 SD > max, fences (1.96 SD, 2.58 SD)" : "min < 1 SD < mean%MEDIAN% > 1 SD > max, fence (1.96 SD)"
                        : useOuterFence ? "min < 1 SD < mean%MEDIAN% > 1 SD > max, fence (2.58 SD)" : "min < 1 SD < mean%MEDIAN% > 1 SD > max";
                    break;
                case BoxWhiskerMethod.MeanStandardErrorRange:
                    xAxisTitle = useInnerFence
                        ? useOuterFence ? "min < 1 SE < mean%MEDIAN% > 1 SE > max, fences (1.96 SD, 2.58 SD)" : "min < 1 SE < mean%MEDIAN% > 1 SE > max, fence (1.96 SD)"
                        : useOuterFence ? "min < 1 SE < mean%MEDIAN% > 1 SE > max, fence (2.58 SD)" : "min < 1 SE < mean%MEDIAN% > 1 SE > max";
                    break;
                case BoxWhiskerMethod.MeanConfidenceIntervalRange:
                    string ci = $"{Formatting.XRound(cco * 100.0, 1)}% confidence interval";
                    xAxisTitle = useInnerFence
                        ? useOuterFence
                            ? $"min < mean%MEDIAN% ? {ci} > max, fences (1.96 SD, 2.58 SD)"
                            : $"min < mean%MEDIAN% ? {ci} > max, fence (1.96 SD)"
                        : useOuterFence
                            ? $"min < mean%MEDIAN% ? {ci} > max, fence (2.58 SD)"
                            : $"min < mean%MEDIAN% ? {ci} > max";
                    break;
                case BoxWhiskerMethod.SevenNumberSummary:
                    xAxisTitle = "min < [ 2nd < 9th < [ LQ < median%MEAN% > UQ | > 91st > 98th ] > max";
                    break;
                case BoxWhiskerMethod.BowleySummary:
                    xAxisTitle = "[ min < 10th < | LQ < median%MEAN% > UQ | > 90th > max ]";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(method), method.ToString());
            }

            if (markMeanAndMedian)
            {
                xAxisTitle = xAxisTitle.Replace("%MEAN%", " & mean(x)");
                xAxisTitle = xAxisTitle.Replace("%MEDIAN%", " & median(x)");
            }
            else
            {
                xAxisTitle = xAxisTitle.Replace("%MEAN%", string.Empty);
                xAxisTitle = xAxisTitle.Replace("%MEDIAN%", string.Empty);
            }

            return xAxisTitle;
        }

        public override bool UsesColour => false;
        public override bool UsesOrientation => true;
        public override bool UsesShowLegend => false;
        public override bool UsesTitleFontDescriptor => true;
        public override bool ShowLegendIsRelevant => false;

        public override bool IsNaturalOrientation => Orientation == ChartOrientation.Horizontal;

        public override void Accept(IChartOptionVisitor visitor) => visitor.Visit(this);
    }
}
