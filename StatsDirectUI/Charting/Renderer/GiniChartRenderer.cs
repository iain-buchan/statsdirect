using StatsDirect.Templates;
using System.Drawing;

namespace StatsDirect.Charting.Renderer
{
    class GiniChartRenderer: AbstractChartRenderer, IChartRenderer
    {
        public GiniChartRenderer(ChartDefinition definition)
            : base(definition)
        {
        }

        ScaleParameters IChartRenderer.GetScaleParameters()
        {
            return new ScaleParameters
            {
                X =
                    {
                        AllowedScaleTypes = new[] { ScaleType.Linear },
                        Max = 1,
                        Min = 0
                    },
                Y =
                    {
                        AllowedScaleTypes = new[] { ScaleType.Linear },
                        Max = 1,
                        Min = 0
                    }
            };
        }

        ParameterBag IChartRenderer.Plot(ITemplateHost host)
        {
            GiniOptions gOptions = ((GiniOptions)(definition.ChartOptions));
            DoubleSeries xs0 = definition.XSeries[0].AsDoubleSeries;
            DoubleSeries ys0 = definition.YSeries[0].AsDoubleSeries;
            StartVectorPlot();

            DataMinX = 0.0;
            DataMaxX = 1.0;
            DataMinY = 0.0;
            DataMaxY = 1.0;

            DrawAxesOrEnlargeCanvas(gOptions.Title.Trim(), new AxisDefinition(gOptions.XAxisTitle, AxisMode.Scale, definition.ScaleParameters.X.ScaleType), new AxisDefinition(gOptions.YAxisTitle, AxisMode.Scale, definition.ScaleParameters.Y.ScaleType), true, false);

            // Draw equality line
            DrawLineInChartCoordinates(grRed, 0, 0, 1, 1);

            // Draw Lorenz polygon
            using (Pen greenPen = new Pen(grGreen))
            {
                double lastX = offx;
                double lastY = offy;
                for (int j = 0; j < xs0.Points; j++)
                {
                    double x = ToCanvasX(xs0.Data[j]);
                    double y = ToCanvasY(ys0.Data[j]);
                    DrawLineInCanvasCoordinates(greenPen, lastX, lastY, x, y);
                    lastX = x;
                    lastY = y;
                }
            }
            EndVectorPlot();
            return new ParameterBag();
        }
    }
}
