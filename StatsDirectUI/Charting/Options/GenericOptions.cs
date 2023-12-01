using System;
using System.Collections.Generic;

namespace StatsDirect.Charting.Options
{
    /// <summary>
    /// Immutable and without inits.  This is intentional, to force copies where different parts of the system may be relying on e.g. array contents and orders.
    /// </summary>
    [Serializable]
    public class GenericOptions : AbstractGenericOptions
    {
        public GenericOptions(IChartPreferences chartPreferences,
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
        }

        public GenericOptions(GenericOptions genericOptions,
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
            : base(genericOptions,
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
        }

        public override bool ShowLegendIsRelevant => false;

        public override void Accept(IChartOptionVisitor visitor) => visitor.Visit(this);
    }
}
