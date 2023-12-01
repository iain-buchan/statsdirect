using StatsDirect.Charting.Options;
using StatsDirect.Templates;

namespace StatsDirect.Charting.Renderer
{
    class LinearRegressionChartRenderer : AbstractLinearRegressionChartRenderer, IChartRenderer
    {
        public LinearRegressionChartRenderer(ChartDefinition cd, ICanvasFactory canvasFactory, ISdPreferences sdPreferences)
            : base(cd, canvasFactory, sdPreferences)
        {
        }

        ScaleParameters IChartRenderer.GetScaleParameters()
        {
            return new ScaleParameters(
                new(new[] { ScaleType.Linear }) {
                    Max = DataMaxX,
                    Min = DataMinX
                },
                new(new[] { ScaleType.Linear }) {
                    Max = DataMaxY,
                    Min = DataMinY
                }
            );
        }

        ParameterBag IChartRenderer.Plot(bool isForReturnedParametersOnly)
        {
            if (isForReturnedParametersOnly)
                return new ParameterBag();

            LinearRegressionOptions options = (LinearRegressionOptions)Definition.ChartOptions;
            StartVectorPlot();
            PlotLinearRegression(options.Title, options.Slope, options.Intercept, options.FullWidth, options.XAxisTitle, options.YAxisTitle);
            EndVectorPlot();
            return new ParameterBag();
        }
    }
}
