using System.Xml.Linq;
using StatsDirect.Charting;
using StatsDirect.Templates;

internal static partial class Program
{
    private static void CheckBoxWhiskerAxes()
    {
        Type factoryType = typeof(ChartDefinition).Assembly.GetType("StatsDirect.Charting.SvgCanvasFactory", true);
        ICanvasFactory factory = (ICanvasFactory)Activator.CreateInstance(factoryType, true);
        XElement Draw(ChartOrientation orientation, bool boxed)
        {
            BoxWhiskerOptions options = new()
            {
                Orientation = orientation, ShouldBoxAxes = boxed, Title = "Selected box axes",
                XAxisTitle = "Measurements", SeriesTitles = new[] { "Selected column" },
                Cco = 0.95, UseInnerFence = true, UseOuterFence = true, AxisLineThickness = 1
            };
            ChartDefinition definition = new() { ChartType = ChartType.BoxWhisker, ChartOptions = options };
            DoubleSeries series = new(new[] { 1.0, 2, 3, 4, 5, 7, 20 }, "Selected column");
            if (orientation == ChartOrientation.Horizontal) definition.AddYSeries(series);
            else definition.AddXSeries(series);
            // The chart dialog supplies a completed value scale before the renderer draws either orientation.
            definition.ScaleParameters.X.ScaleType = ScaleType.Linear;
            definition.ScaleParameters.X.AxisScale = new LinearAxisScale(1, 20, 0, 20, 4);
            using IChartRenderer renderer = ChartRendererFactory.ChartRendererFor(definition, factory);
            renderer.Plot(null, false);
            using Stream image = renderer.Canvas.DetachAndReturnImageStream();
            return XElement.Load(image);
        }

        foreach (ChartOrientation orientation in Enum.GetValues<ChartOrientation>())
        {
            XElement ordinary = Draw(orientation, false), boxed = Draw(orientation, true);
            List<XElement> original = ordinary.Elements().ToList(), withBox = boxed.Elements().ToList(), added = new();
            int matched = 0;
            foreach (XElement element in withBox)
                if (matched < original.Count && XNode.DeepEquals(element, original[matched])) matched++;
                else added.Add(element);
            Check(matched == original.Count && withBox.Count == original.Count + 2 && added.Count == 2
                  && added.All(e => e.Name.LocalName == "line")
                  && ordinary.Attributes().Select(a => a.ToString()).SequenceEqual(boxed.Attributes().Select(a => a.ToString())),
                "the native " + orientation.ToString().ToLowerInvariant() + " box plot honours Box axes by adding only the two closing axis lines");
        }
    }
}
