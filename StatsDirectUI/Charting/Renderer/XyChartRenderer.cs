using StatsDirect.Charting.Options;
using StatsDirect.Templates;

namespace StatsDirect.Charting.Renderer
{
    class XyChartRenderer : AbstractXYChartRenderer, IChartRenderer
    {
        public XyChartRenderer(ChartDefinition definition, ICanvasFactory canvasFactory, ISdPreferences sdPreferences)
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

            XyOptions options = (XyOptions)Definition.ChartOptions;
            StartVectorPlot();
            PlotXYInternal(options.X, options.Y, options.XAxisTitle, options.YAxisTitle, options.Title, options.ZPlot, options.MinMaxY, 6, MarkerShape.Circle, false, PenDescriptor.Black, false, ChartAreaShape.Default);
            EndVectorPlot();
            return new ParameterBag();
        }
    }
}
