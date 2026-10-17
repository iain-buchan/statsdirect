using System.Globalization;
using System.Xml.Linq;
using StatsDirect.Charting;
using StatsDirect.Templates;

internal static partial class Program
{
    private static void CheckNativeAxisLabels()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            foreach (string name in new[] { "en-GB", "de-DE" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
                foreach ((double low, double high) in new[]
                {
                    (201.0, 402.0), (0.0, 2.0), (-3.2, 2.1),
                    (0.000125, 0.00055), (1e-20, 6e-20), (225.5, 228.0)
                })
                foreach (bool vertical in new[] { false, true })
                {
                    var scale = (ILinearAxisScale)AxisScalerFactory.AxisScalerFor(ScaleType.Linear)
                        .QAxis(low, low > 0 ? low : 0.1, high, vertical, false);
                    var ticks = scale.Tics();
                    string separator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
                    Check(ticks.Count > 1 && ticks.All(t =>
                        !System.Text.RegularExpressions.Regex.IsMatch(t.Label,
                            System.Text.RegularExpressions.Regex.Escape(separator) + "0+$") &&
                        double.TryParse(t.Label, NumberStyles.Float, CultureInfo.CurrentCulture, out double parsed) &&
                        Math.Abs(parsed - t.Value) <= 2e-15 * Math.Max(Math.Abs(t.Value), 1e-300)),
                        $"automatic {(vertical ? "vertical" : "horizontal")} axis {low:R}..{high:R} in {name} keeps its values without redundant decimal zeros");
                }
            }

            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-GB");
            BoxWhiskerOptions options = new()
            {
                Orientation = ChartOrientation.Horizontal, Title = "PEFR measurements",
                XAxisTitle = "PEFR", SeriesTitles = new[] { "PEFR Before", "PEFR After" },
                Cco = 0.95, UseInnerFence = true, UseOuterFence = true, AxisLineThickness = 1
            };
            ChartDefinition definition = new() { ChartType = ChartType.BoxWhisker, ChartOptions = options };
            definition.AddYSeries(new DoubleSeries(new double[] { 312, 242, 340, 388, 296, 254, 391, 402, 290 }, "PEFR Before"));
            definition.AddYSeries(new DoubleSeries(new double[] { 300, 201, 232, 312, 220, 256, 328, 330, 231 }, "PEFR After"));
            definition.ScaleParameters.X.ScaleType = ScaleType.Linear;
            definition.ScaleParameters.X.AxisScale = AxisScalerFactory.AxisScalerFor(ScaleType.Linear)
                .QAxis(201, 201, 402, false, false);
            Type type = typeof(ChartDefinition).Assembly.GetType("StatsDirect.Charting.SvgCanvasFactory", true);
            using IChartRenderer renderer = ChartRendererFactory.ChartRendererFor(definition,
                (ICanvasFactory)Activator.CreateInstance(type, true));
            renderer.Plot(null, false);
            using Stream image = renderer.Canvas.DetachAndReturnImageStream();
            XElement svg = XElement.Load(image);
            string[] text = svg.Descendants().Where(e => e.Name.LocalName == "text").Select(e => e.Value).ToArray();
            Check(text.Contains("200") && text.Contains("400") && !text.Contains("200.0") && !text.Contains("400.0"),
                "the rendered example-workbook PEFR box plot labels whole ticks as 200 through 400");
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }
}
