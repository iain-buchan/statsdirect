using StatsDirect.Numerics;
using StatsDirect.Templates;
using System.Drawing;

namespace StatsDirect.Charting.Renderer
{
    class LadderChartRenderer : AbstractChartRenderer, IChartRenderer
    {
        public LadderChartRenderer(ChartDefinition definition, ICanvasFactory canvasFactory)
            : base(definition, canvasFactory)
        {
        }

        ScaleParameters IChartRenderer.GetScaleParameters()
        {
            return new ScaleParameters
            {
                X =
                {
                    AllowedScaleTypes = new[] { ScaleType.Category },
                    Max = 0,
                    Min = 0
                },
                Y =
                {
                    AllowedScaleTypes = new[] { ScaleType.Linear },
                    Max = DataMaxY,
                    Min = DataMinY
                }
            };
        }

        ParameterBag IChartRenderer.Plot(ITemplateHost host)
        {
            // Get the plot title
            LadderOptions lOptions = ((LadderOptions)(definition.ChartOptions));
            StartVectorPlot();
            SetFontsAndThicknessesFromOptions(lOptions);
            AssignMarkersToSeries(definition.YSeries, lOptions);

            //  No need to calculate min/max values, as they've already been calculated as the series were added.
            //  We just need to set the neat scale.
            DrawAxesOrEnlargeCanvas(lOptions.Title, new AxisDefinition(null, AxisMode.Series, definition.ScaleParameters.X.ScaleType) { Series = definition.YSeries }, new AxisDefinition(lOptions.YAxisTitle, AxisMode.Scale, definition.ScaleParameters.Y.ScaleType), lOptions.ShouldBoxAxes, false);
            double x1 = xAxisCanvas + (xExtCanvas * 0.25);
            double x2 = xAxisCanvas + (xExtCanvas * 0.75);

            // Plot the points & join the lines
            DoubleSeries s0 = definition.YSeries[0].AsDoubleSeries;
            DoubleSeries s1 = definition.YSeries[1].AsDoubleSeries;
            //  Points
            for (int r = 0; r <= s0.Points - 1; r++)
            {
                if (s0.Data[r] != Constant.MISSING && s1.Data[r] != Constant.MISSING)
                {
                    double y1 = ToCanvasY(s0.Data[r]);
                    double Y2 = ToCanvasY(s1.Data[r]);
                    DrawMarkerInCanvasCoordinates(x1, y1, s0.MarkerDetails.MarkerSize, s0);
                    DrawMarkerInCanvasCoordinates(x2, Y2, s1.MarkerDetails.MarkerSize, s1);
                }
            }
            //  Lines
            MarkerType rungMarkerType = ChartPreferences.MarkerTypes[10];
            if ((lOptions.MarkerTypes != null) && lOptions.MarkerTypes.Count >= 1 && lOptions.MarkerTypes[0] != null)
                rungMarkerType = lOptions.MarkerTypes[0];

            using (Pen rungPen = new Pen(Color.Black, rungMarkerType.Width))
            {
                rungPen.DashStyle = rungMarkerType.LineDashStyle;
                for (int r = 0; r <= s0.Points - 1; r++)
                {
                    if (s0.Data[r] != Constant.MISSING && s1.Data[r] != Constant.MISSING)
                    {
                        double y1 = ToCanvasY(s0.Data[r]);
                        double Y2 = ToCanvasY(s1.Data[r]);
                        DrawLineInCanvasCoordinates(rungPen, x1, y1, x2, Y2);
                    }
                }
            }
            EndVectorPlot();
            return new ParameterBag();
        }
    }
}
