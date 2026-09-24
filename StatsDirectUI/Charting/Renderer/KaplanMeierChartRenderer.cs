using StatsDirect.Charting.Scales;
using StatsDirect.Templates;
using System;
using System.Globalization;

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

        ParameterBag IChartRenderer.Plot(/* TODO: IPreferences*/ ITemplateHost _, bool isForReturnedParametersOnly)
        {
            if (isForReturnedParametersOnly)
                return new ParameterBag();

            KaplanMeierOptions options = (KaplanMeierOptions)Definition.ChartOptions;
            Legend legend = null;
            if (options.Groups > 1)
            {
                legend = new Legend { Position = LegendPosition.Left };
                for (int k = 1; k <= options.Groups; k++)
                {
                    MarkerType mt = ChartPreferences.MarkerTypes[(k - 1) % 9].Clone();
                    if (!options.UseMarkers)
                    {
                        mt.MarkerShape = MarkerShape.SurvivalTic;
                        mt.IsMarkerFilled = false;
                    }
                    legend.LegendEntries.Add(new LegendEntry { Label = options.GroupLabels[k], MarkerType = mt });
                }
            }

            StartVectorPlot(null, legend);
            DataMaxX = double.MinValue;
            DataMaxY = double.MinValue;
            DataMinX = double.MaxValue;
            DataMinY = double.MaxValue;
            for (int k = 1; k <= options.Groups; k++)
            {
                for (int j = 1; j <= options.cnx[k]; j++)
                {
                    if (options.X[j, k] > DataMaxX)
                        DataMaxX = options.X[j, k];
                    if (options.X[j, k] < DataMinX)
                        DataMinX = options.X[j, k];
                    if (options.Y[j, k] > DataMaxY)
                        DataMaxY = options.Y[j, k];
                    if (options.Y[j, k] < DataMinY)
                        DataMinY = options.Y[j, k];
                }
            }
            if (options.PlotMode == KaplanMeierPlotMode.Survival)
            {
                DataMaxY = 1;
                DataMinY = 0;
            }
            //  No point at all (the hazard of a group whose only subject died, or the log hazard of a group with no death) overflowed the axis layout.
            if (DataMaxX < DataMinX || DataMaxY < DataMinY)
                throw new Exception("No points to plot: the " + options.YAxisTitle.ToLower(CultureInfo.CurrentCulture) + " values are not defined for these data");
            //  A group with a single death has one hazard value throughout (equal but for the last bits of -log S), and one or two subjects can give a
            //  single time: no axis can be made from a range of zero, so the range is widened by a tenth of the value (by 1 for a value of 0) and the
            //  chart drawn.
            if (IsZeroRange(DataMinX, DataMaxX))
            {
                double widening = DataMaxX == 0 ? 1 : 0.1 * Math.Abs(DataMaxX);
                DataMinX -= widening;
                DataMaxX += widening;
            }
            if (IsZeroRange(DataMinY, DataMaxY))
            {
                double widening = DataMaxY == 0 ? 1 : 0.1 * Math.Abs(DataMaxY);
                DataMinY -= widening;
                DataMaxY += widening;
            }
            AxisScales axisScales = LayoutChartAndDrawAxes(options.Title,
                new AxisDefinition(options.XAxisTitle, AxisMode.Scale, ScaleType.Linear),
                new AxisDefinition(options.YAxisTitle, AxisMode.Scale, ScaleType.Linear),
                false, false,
                legend);

            for (int k = 1; k <= options.Groups; k++)
            {
                MarkerType mt = ChartPreferences.MarkerTypes[(k - 1) % 9];
                double x1; double y1;
                switch (options.PlotMode)
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
                        x1 = options.X[1, k];
                        y1 = options.Y[1, k];
                        break;
                    default:
                        throw new Exception("Unknown plot mode");
                }

                for (int j = 1; j <= options.cnx[k]; j++)
                {
                    double x2 = options.X[j, k];
                    double y2 = options.Y[j, k];
                    // Draw the markers
                    // changed to tic mark at censor points March 01
                    if (options.Dead[j, k] == 0 && options.DrawTics)
                        DrawLineInChartCoordinates(mt.LineColor, x2, y2, x2, y2 + FromCanvasHeight(7));
                    if (options.Dead[j, k] != 0 && options.UseMarkers)
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

        /// <summary>
        /// True when the range is zero, or so small beside the values' magnitude that no axis can be made from it.
        /// </summary>
        private static bool IsZeroRange(double minimum, double maximum)
        {
            return maximum - minimum <= 1e-9 * Math.Max(Math.Abs(minimum), Math.Abs(maximum));
        }
    }
}
