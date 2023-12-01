using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace StatsDirect.Charting.Options
{
    [Serializable]
    public class ControlOptions : AbstractGenericOptions
        , IAxisLabelFontOptions
        , IAxisTitleFontOptions
        , IBoxAxesOptions
        , IChartTitleOptions
        , ILegendFontOptions
        , IXAxisTitleOptions
        , IYAxisTitleOptions
    {
        const int DEFAULT_RIGHT_HAND_DECIMAL_PLACES = 3; // TODO: Should this be centralised somewhere, or is per-chart-type the best place for local options?

        public bool UseMean { get; }
        public bool Use1SD { get; }
        public bool Use2SD { get; }
        public bool Use3SD { get; }
        public MeanAndStandardDeviation? UserSpecifiedMeanAndStandardDeviation { get; }
        public ControlAndWarningLimits ControlAndWarningLimits { get; }
        /// <summary>
        /// If null, use all to calculate control values.
        /// </summary>
        public int? ObservationsToUse { get; }
        public int RightHandDecimalPlaces { get; }

        public ControlOptions(
            IChartPreferences chartPreferences,
            FontDescriptor? axisLabelFontDescriptor = default,
            float? axisLineThickness = default,
            FontDescriptor? axisTitleFontDescriptor = default,
            ControlAndWarningLimits? controlAndWarningLimits = null,
            FontDescriptor? legendFontDescriptor = default,
            int? observationsToUse = default,
            ChartOrientation? orientation = default,
            int? rightHandDecimalPlaces = default,
            IReadOnlyList<string?>? seriesTitles = default,
            bool? shouldAutoscale = default,
            bool? shouldBoxAxes = default,
            bool? showLegend = default,
            string? title = default,
            FontDescriptor? titleFontDescriptor = default,
            bool use1Sd = false,
            bool use2Sd = false,
            bool use3Sd = false,
            bool? useColour = default,
            bool useMean = false,
            MeanAndStandardDeviation? userSpecifiedMeanAndStandardDeviation = null,
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
            ControlAndWarningLimits = controlAndWarningLimits ?? ControlAndWarningLimits.Empty;
            ObservationsToUse = observationsToUse;
            RightHandDecimalPlaces = rightHandDecimalPlaces ?? DEFAULT_RIGHT_HAND_DECIMAL_PLACES;
            Use1SD = use1Sd;
            Use2SD = use2Sd;
            Use3SD = use3Sd;
            UseMean = useMean;
            UserSpecifiedMeanAndStandardDeviation = userSpecifiedMeanAndStandardDeviation;
        }

        public ControlOptions(
            AbstractGenericOptions genericOptions,
            ControlAndWarningLimits? controlAndWarningLimits = null,
            int? observationsToUse = default,
            int? rightHandDecimalPlaces = default,
            bool use1Sd = false,
            bool use2Sd = false,
            bool use3Sd = false,
            bool useMean = false,
            MeanAndStandardDeviation? userSpecifiedMeanAndStandardDeviation = null)
            : base(genericOptions)
        {
            ControlAndWarningLimits = controlAndWarningLimits ?? ControlAndWarningLimits.Empty;
            ObservationsToUse = observationsToUse;
            RightHandDecimalPlaces = rightHandDecimalPlaces ?? DEFAULT_RIGHT_HAND_DECIMAL_PLACES;
            Use1SD = use1Sd;
            Use2SD = use2Sd;
            Use3SD = use3Sd;
            UseMean = useMean;
            UserSpecifiedMeanAndStandardDeviation = userSpecifiedMeanAndStandardDeviation;
        }

        [MemberNotNullWhen(true, "ControlAndWarningLimits")]
        public bool HasUserSpecifiedLimits => ControlAndWarningLimits is not null && ControlAndWarningLimits.HasLimits;

        public override bool ShowLegendIsRelevant => false;
        public override bool UsesAutoscale => true;

        public override string LegendFontLabel => "Control Label";

        public override void Accept(IChartOptionVisitor visitor) => visitor.Visit(this);
    }

    public class MeanAndStandardDeviation
    {
        public double? Mean { get; }
        public double? StandardDeviation { get; }

        public MeanAndStandardDeviation(double? mean, double? standardDeviation)
        {
            Mean = mean;
            StandardDeviation = standardDeviation;
        }
    }

    public class ControlAndWarningLimits
    {
        public static ControlAndWarningLimits Empty { get; } = new ControlAndWarningLimits();

        public double? LowerWarningLimit { get; }
        public double? UpperWarningLimit { get; }
        public double? LowerControlLimit { get; }
        public double? UpperControlLimit { get; }

        public ControlAndWarningLimits(double? lowerWarningLimit = default, double? upperWarningLimit = default, double? lowerControlLimit = default, double? upperControlLimit = default)
        {
            LowerWarningLimit = lowerWarningLimit;
            UpperWarningLimit = upperWarningLimit;
            LowerControlLimit = lowerControlLimit;
            UpperControlLimit = upperControlLimit;

            // Force correction of limits if all are present
            if (HasLimits)
            {
                if (LowerControlLimit > UpperControlLimit)
                    (UpperControlLimit, LowerControlLimit) = (LowerControlLimit, UpperControlLimit);
                if (LowerWarningLimit > UpperWarningLimit)
                    (UpperWarningLimit, LowerWarningLimit) = (LowerWarningLimit, UpperWarningLimit);
                if (LowerControlLimit > LowerWarningLimit)
                    (LowerWarningLimit, LowerControlLimit) = (LowerControlLimit, LowerWarningLimit);
                if (UpperWarningLimit > UpperControlLimit)
                    (UpperWarningLimit, UpperControlLimit) = (UpperControlLimit, UpperWarningLimit);
            }
        }

        public bool HasLimits =>
            LowerControlLimit.HasValue
            && LowerWarningLimit.HasValue
            && UpperControlLimit.HasValue
            && UpperWarningLimit.HasValue;
    }
}
