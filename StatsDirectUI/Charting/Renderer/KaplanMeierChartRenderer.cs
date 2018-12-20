using StatsDirect.Charting.Scales;
using StatsDirect.Templates;
using System;

namespace StatsDirect.Charting.Renderer
{
    class KaplanMeierChartRenderer : AbstractChartRenderer, IChartRenderer
    {
        public KaplanMeierChartRenderer(ChartDefinition definition, ICanvasFactory canvasFactory)
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

            KaplanMeierOptions options = (KaplanMeierOptions)Definition.ChartOptions;
            Legend legend = null;
            if (options.groups > 1)
            {
                legend = new Legend { Position = LegendPosition.Left };
                for (int k = 1; k <= options.groups; k++)
                {
                    MarkerType mt = ChartPreferences.MarkerTypes[(k - 1) % 9].Clone();
                    if (!options.marker)
                    {
                        mt.MarkerShape = MarkerShape.SurvivalTic;
                        mt.IsMarkerFilled = false;
                    }
                    legend.LegendEntries.Add(new LegendEntry { Label = options.glab[k], MarkerType = mt });
                }
            }

            StartVectorPlot(null, legend);
            DataMaxX = double.MinValue;
            DataMaxY = double.MinValue;
            DataMinX = double.MaxValue;
            DataMinY = double.MaxValue;
            for (int k = 1; k <= options.groups; k++)
            {
                for (int j = 1; j <= options.cnx[k]; j++)
                {
                    if (options.x[j, k] > DataMaxX)
                        DataMaxX = options.x[j, k];
                    if (options.x[j, k] < DataMinX)
                        DataMinX = options.x[j, k];
                    if (options.y[j, k] > DataMaxY)
                        DataMaxY = options.y[j, k];
                    if (options.y[j, k] < DataMinY)
                        DataMinY = options.y[j, k];
                }
            }
            if (options.plotMode == KaplanMeierPlotMode.Survival)
            {
                DataMaxY = 1;
                DataMinY = 0;
            }
            AxisScales axisScales = LayoutChartAndDrawAxes(options.title,
                new AxisDefinition(options.xAxisTitle, AxisMode.Scale, ScaleType.Linear),
                new AxisDefinition(options.yAxisTitle, AxisMode.Scale, ScaleType.Linear),
                false, false,
                legend);

            for (int k = 1; k <= options.groups; k++)
            {
                MarkerType mt = ChartPreferences.MarkerTypes[(k - 1) % 9];
                double x1; double y1;
                switch (options.plotMode)
                {
                    case KaplanMeierPlotMode.Survival:
                        x1 = axisScales.X.MinimumScaleValue;
                        y1 = 1.0;
                        break;
                    case KaplanMeierPlotMode.Hazard:
                        x1 = axisScales.X.MinimumScaleValue;
                        y1 = 0;
                        break;
                    case KaplanMeierPlotMode.LogHazard:
                    case KaplanMeierPlotMode.LognormalSurvival:
                    case KaplanMeierPlotMode.HazardRate:
                        x1 = options.x[1, k];
                        y1 = options.y[1, k];
                        break;
                    default:
                        throw new Exception("Unknown plot mode");
                }

                for (int j = 1; j <= options.cnx[k]; j++)
                {
                    double x2 = options.x[j, k];
                    double y2 = options.y[j, k];
                    // Draw the markers
                    // changed to tic mark at censor points March 01
                    if (options.dead[j, k] == 0 && options.tic)
                        DrawLineInChartCoordinates(mt.LineColor, x2, y2, x2, y2 + FromCanvasHeight(7));
                    if (options.dead[j, k] != 0 && options.marker)
                        DrawMarkerInChartCoordinates(x2, y2, 6, mt);
                    // Then the lines
                    DrawLineInChartCoordinates(mt.LineColor, x1, y1, x2, y1);
                    DrawLineInChartCoordinates(mt.LineColor, x2, y1, x2, y2);
                    x1 = x2;
                    y1 = y2;
                }
            }
            if (null != legend)
                DrawLegend(legend);
            EndVectorPlot();
            return new ParameterBag();
        }
    }
}
