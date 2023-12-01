using System;
using System.Collections.Generic;

namespace StatsDirect.Charting.Options
{
    /// <summary>
    /// Immutable and without inits.  This is intentional, to force copies where different parts of the system may be relying on e.g. array contents and orders.
    /// </summary>
    [Serializable]
    public abstract class AbstractGenericOptions : AbstractChartOptions
    {
        public FontDescriptor AxisLabelFontDescriptor { get; }
        public FontDescriptor AxisTitleFontDescriptor { get; }
        public FontDescriptor LegendFontDescriptor { get; }
        public ChartOrientation Orientation { get; }
        public IReadOnlyList<string?>? SeriesTitles { get; }
        public bool ShouldAutoscale { get; }
        public bool ShouldBoxAxes { get; }
        public FontDescriptor TitleFontDescriptor { get; }

        /* Convenience snippets for subclasses
        public XOptions(IChartPreferences chartPreferences,
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

        public XOptions(XOptions genericOptions,
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
         */

        public AbstractGenericOptions(IChartPreferences chartPreferences,
            FontDescriptor? axisLabelFontDescriptor,
            float? axisLineThickness,
            FontDescriptor? axisTitleFontDescriptor,
            FontDescriptor? legendFontDescriptor,
            ChartOrientation? orientation,
            IReadOnlyList<string?>? seriesTitles,
            bool? shouldAutoscale,
            bool? shouldBoxAxes,
            bool? showLegend,
            string? title,
            FontDescriptor? titleFontDescriptor,
            bool? useColour,
            string? xAxisTitle,
            string? yAxisTitle)
            : base(chartPreferences, axisLineThickness, showLegend, title, useColour, xAxisTitle, yAxisTitle)
        {
            AxisLabelFontDescriptor = axisLabelFontDescriptor ?? chartPreferences.AxisLabelFont;
            AxisTitleFontDescriptor = axisTitleFontDescriptor ?? chartPreferences.AxisTitleFont;
            LegendFontDescriptor = legendFontDescriptor ?? chartPreferences.LegendFont;
            Orientation = orientation ?? chartPreferences.Orientation;
            SeriesTitles = seriesTitles;
            ShouldAutoscale = shouldAutoscale ?? chartPreferences.Autoscale;
            ShouldBoxAxes = shouldBoxAxes ?? chartPreferences.BoxAxes;
            TitleFontDescriptor = titleFontDescriptor ?? chartPreferences.TitleFont;
        }

        protected AbstractGenericOptions(AbstractChartOptions chartOptions,
            FontDescriptor? axisLabelFontDescriptor,
            FontDescriptor? axisTitleFontDescriptor,
            FontDescriptor? legendFontDescriptor,
            ChartOrientation? orientation,
            IReadOnlyList<string?>? seriesTitles,
            bool? shouldAutoscale,
            bool? shouldBoxAxes,
            FontDescriptor? titleFontDescriptor)
            : base(chartOptions)
        {
            IChartPreferences chartPreferences = chartOptions.ChartPreferences;
            AxisLabelFontDescriptor = axisLabelFontDescriptor ?? chartPreferences.AxisLabelFont;
            AxisTitleFontDescriptor = axisTitleFontDescriptor ?? chartPreferences.AxisTitleFont;
            LegendFontDescriptor = legendFontDescriptor ?? chartPreferences.LegendFont;
            Orientation = orientation ?? chartPreferences.Orientation;
            SeriesTitles = seriesTitles;
            ShouldAutoscale = shouldAutoscale ?? chartPreferences.Autoscale;
            ShouldBoxAxes = shouldBoxAxes ?? chartPreferences.BoxAxes;
            TitleFontDescriptor = titleFontDescriptor ?? chartPreferences.TitleFont;
        }

        public AbstractGenericOptions(AbstractGenericOptions genericOptions)
            : base(genericOptions)
        {
            AxisLabelFontDescriptor = genericOptions.AxisLabelFontDescriptor;
            AxisTitleFontDescriptor = genericOptions.AxisTitleFontDescriptor;
            LegendFontDescriptor = genericOptions.LegendFontDescriptor;
            Orientation = genericOptions.Orientation;
            SeriesTitles = genericOptions.SeriesTitles;
            ShouldAutoscale = genericOptions.ShouldAutoscale;
            ShouldBoxAxes = genericOptions.ShouldBoxAxes;
            TitleFontDescriptor = genericOptions.TitleFontDescriptor;
        }

        protected AbstractGenericOptions(AbstractGenericOptions genericOptions,
            FontDescriptor? axisLabelFontDescriptor,
            float? axisLineThickness,
            FontDescriptor? axisTitleFontDescriptor,
            FontDescriptor? legendFontDescriptor,
            ChartOrientation? orientation,
            IReadOnlyList<string?>? seriesTitles,
            bool? shouldAutoscale,
            bool? shouldBoxAxes,
            bool? showLegend,
            string? title,
            FontDescriptor? titleFontDescriptor,
            bool? useColour,
            string? xAxisTitle,
            string? yAxisTitle)
            : base(genericOptions, axisLineThickness, showLegend, title, useColour, xAxisTitle, yAxisTitle)
        {
            AxisLabelFontDescriptor = axisLabelFontDescriptor ?? genericOptions.AxisLabelFontDescriptor;
            AxisTitleFontDescriptor = axisTitleFontDescriptor ?? genericOptions.AxisTitleFontDescriptor;
            LegendFontDescriptor = legendFontDescriptor ?? genericOptions.LegendFontDescriptor;
            Orientation = orientation ?? genericOptions.Orientation;
            SeriesTitles = seriesTitles ?? genericOptions.SeriesTitles;
            ShouldAutoscale = shouldAutoscale ?? genericOptions.ShouldAutoscale;
            ShouldBoxAxes = shouldBoxAxes ?? genericOptions.ShouldBoxAxes;
            TitleFontDescriptor = titleFontDescriptor ?? genericOptions.TitleFontDescriptor;
        }

        public virtual bool UsesAutoscale => false;
        public virtual string LegendFontLabel => "Legend";
        public virtual string OrientationLabel => "Orientation";

        ///  <summary>
        ///  True if the chart can only be drawn in one orientation or if the orientation matches its preferred orientation.
        ///  False if the chart will be drawn in a non-preferred orientation.
        ///  </summary>
        public virtual bool IsNaturalOrientation => true;
        public virtual bool UsesTitleFontDescriptor => true;
        public virtual bool UsesOrientation => false;
    }
}
