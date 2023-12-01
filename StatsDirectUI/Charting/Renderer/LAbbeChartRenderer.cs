using StatsDirect.Charting.Options;
using StatsDirect.Templates;

namespace StatsDirect.Charting.Renderer
{
    class LAbbeChartRenderer : AbstractXYZChartRenderer, IChartRenderer
    {
        public LAbbeChartRenderer(ChartDefinition definition, ICanvasFactory canvasFactory, ISdPreferences sdPreferences)
            : base(definition, canvasFactory, sdPreferences)
        {
        }

        ScaleParameters IChartRenderer.GetScaleParameters()
        {
            return new
            (
                new AxisScaleParameters { ScaleType = ScaleType.Linear },
                new AxisScaleParameters { ScaleType = ScaleType.Linear }
            );
        }

        ParameterBag IChartRenderer.Plot(bool isForReturnedParametersOnly)
        {
            if (isForReturnedParametersOnly)
                return new ParameterBag();

            LAbbeOptions options = (LAbbeOptions)Definition.ChartOptions;
            double[] y = new double[options.K + 1];
            double[] x = new double[options.K + 1];
            double[] w = new double[options.K + 1];
            for (int i = 1; i <= options.K; i++)
            {
                y[i] = options.O[i, 1] + options.O[i, 3] == 0
                    ? 0
                    : options.O[i, 1] / (options.O[i, 1] + options.O[i, 3]);
                x[i] = options.O[i, 2] + options.O[i, 4] == 0 
                    ? 0
                    : options.O[i, 2] / (options.O[i, 2] + options.O[i, 4]);
                w[i] = options.O[i, 1] + options.O[i, 2] + options.O[i, 3] + options.O[i, 4];
            }
            PlotXYZ(x, y, w, 1, options.K, "control percent", "experimental percent", "L'Abbe plot (symbol size represents sample size)", false, 0, ChartPreferences.MarkerTypes[0], options.Rmh);
            return new ParameterBag();
        }
    }
}
