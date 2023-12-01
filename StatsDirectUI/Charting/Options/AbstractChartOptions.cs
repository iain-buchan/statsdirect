using System;

namespace StatsDirect.Charting
{
    /// <summary>
    /// Immutable.
    /// </summary>
    [Serializable]
    public abstract class AbstractChartOptions
    {
        public float? AxisLineThickness { get; }
        public bool? ShowLegend { get; }
        public string? Title { get; }
        public bool UseColour { get; }
        public string? XAxisTitle { get; }
        public string? YAxisTitle { get; }

        public IChartPreferences ChartPreferences { get; }

        /// <remarks>
        /// Class is abstract, so it is reasonable to force subclasses to specify all parameters
        /// </remarks>
        public AbstractChartOptions(IChartPreferences chartPreferences, float? axisLineThickness, bool? showLegend, string? title, bool? useColour, string? xAxisTitle, string? yAxisTitle)
        {
            ChartPreferences = chartPreferences;
            AxisLineThickness = axisLineThickness;
            ShowLegend = showLegend;
            Title = title;
            UseColour = useColour ?? ChartPreferences.UseColour;
            XAxisTitle = xAxisTitle;
            YAxisTitle = yAxisTitle;
        }

        /// <summary>
        /// Copy. Used when assembling options from portions, for example in ctlChartOptions.
        /// </summary>
        public AbstractChartOptions(AbstractChartOptions template)
        {
            ChartPreferences = template.ChartPreferences;
            AxisLineThickness = template.AxisLineThickness;
            ShowLegend = template.ShowLegend;
            Title = template.Title;
            UseColour = template.UseColour;
            XAxisTitle = template.XAxisTitle;
            YAxisTitle = template.YAxisTitle;
        }

        /// <summary>
        /// Copy and amend.
        /// </summary>
        /// <remarks>
        /// Class is abstract, so it is reasonable to force subclasses to specify all parameters
        /// </remarks>
        public AbstractChartOptions(AbstractChartOptions template, float? axisLineThickness, bool? showLegend, string? title, bool? useColour, string? xAxisTitle, string? yAxisTitle)
        {
            ChartPreferences = template.ChartPreferences;
            AxisLineThickness = axisLineThickness ?? template.AxisLineThickness;
            ShowLegend = showLegend ?? template.ShowLegend;
            Title = title ?? template.Title;
            UseColour = useColour ?? template.UseColour;
            XAxisTitle = xAxisTitle ?? template.XAxisTitle;
            YAxisTitle = yAxisTitle ?? template.YAxisTitle;
        }

        ///  <summary>
        ///  Given a series index (13 for the 14th series, for example) return the marker that should be used for that series.
        ///  </summary>
        ///  <remarks>This used to be considerably more complex; Peter has simplified.</remarks>
        public static int SeriesNumberToMarkerNumber(int seriesNumber) => seriesNumber % 10;

        public virtual bool UsesAxisLineThickness => true;
        public virtual bool UsesColour => true;
        public virtual bool UsesShowLegend => true;
        public virtual bool UsesXAxisOptions => true;
        public virtual bool UsesYAxisOptions => true;

        public abstract void Accept(IChartOptionVisitor visitor);

        public abstract bool ShowLegendIsRelevant { get; }
    }
}
