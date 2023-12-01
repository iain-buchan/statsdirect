using StatsDirect.Charting.Options;
using StatsDirect.Templates;

namespace StatsDirect.Charting.Renderer
{
    class XyzChartRenderer : AbstractXYZChartRenderer, IChartRenderer
    {
        public XyzChartRenderer(ChartDefinition definition, ICanvasFactory canvasFactory, ISdPreferences sdPreferences)
            : base(definition, canvasFactory, sdPreferences)
        {
        }

        ScaleParameters IChartRenderer.GetScaleParameters()
        {
            return new ScaleParameters(
                new AxisScaleParameters { ScaleType = ScaleType.Linear },
                new AxisScaleParameters { ScaleType = ScaleType.Linear }
            );
        }

        ParameterBag IChartRenderer.Plot(bool isForReturnedParametersOnly)
        {
            if (isForReturnedParametersOnly)
                return new ParameterBag();

            XyzOptions options = (XyzOptions)Definition.ChartOptions;
            PlotXYZ(options.x, options.y, options.z, 1, options.x.Length - 1, options.XAxisTitle, options.YAxisTitle, options.Title, options.zPlot, options.minMaxY, new MarkerType { MarkerShape = MarkerShape.Circle, IsMarkerFilled = false, MarkerColor = ColorDescriptor.Black });
            return new ParameterBag();
        }
    }
}
