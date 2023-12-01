using System;

namespace StatsDirect.Charting.Options
{
    [Serializable]
    public class LinearRegressionOptions : AbstractChartOptions
    {
        public bool FullWidth { get; }
        public double Intercept { get; }
        public double Slope { get; }

        public LinearRegressionOptions(IChartPreferences chartPreferences,
            double intercept,
            double slope,
            float? axisLineThickness = default,
            bool? fullWidth = default,
            bool? showLegend = default,
            string? title = default,
            bool? useColour = default,
            string? xAxisTitle = default,
            string? yAxisTitle = default)
            : base(chartPreferences,
                  axisLineThickness,
                  showLegend,
                  title,
                  useColour,
                  xAxisTitle,
                  yAxisTitle)
        {
            FullWidth = fullWidth ?? false;
            Intercept = intercept;
            Slope = slope;
        }

        public override bool ShowLegendIsRelevant => false;

        public override void Accept(IChartOptionVisitor visitor) => visitor.Visit(this);
    }
}
