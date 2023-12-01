using System;
using System.Collections.Generic;

namespace StatsDirect.Charting.Options
{
    [Serializable]
    public class GiniOptions : AbstractGenericOptions, IChartTitleOptions, IXAxisTitleOptions, IYAxisTitleOptions
    {
        public GiniOptions(IChartPreferences chartPreferences,
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

        public override bool ShowLegendIsRelevant => false;

        public override void Accept(IChartOptionVisitor visitor) => visitor.Visit(this);
    }
}
