using StatsDirect.Templates;

namespace StatsDirect.Charting.Renderer
{
    class XyChartRenderer : AbstractXYChartRenderer, IChartRenderer
    {
        public XyChartRenderer(ChartDefinition definition, ICanvasFactory canvasFactory)
            : base(definition, canvasFactory)
        {
        }

        ScaleParameters IChartRenderer.GetScaleParameters()
        {
            return new ScaleParameters
            {
                X = new AxisScaleParameters { ScaleType = ScaleType.Linear },
                Y = new AxisScaleParameters { ScaleType = ScaleType.Linear }
            };
        }

        ParameterBag IChartRenderer.Plot(ITemplateHost host, bool isForReturnedParametersOnly)
        {
            if (isForReturnedParametersOnly)
                return new ParameterBag();

            XyOptions options = (XyOptions)Definition.ChartOptions;
            StartVectorPlot();
            PlotXYInternal(options.x, options.y, options.xtxt, options.ytxt, options.title, options.zPlot, options.minMaxY, 6, MarkerShape.Circle, false, PenDescriptor.Black, false, ChartAreaShape.Default);
            EndVectorPlot();
            return new ParameterBag();
        }
    }
}
