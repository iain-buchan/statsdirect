using StatsDirect.Numerics;
using StatsDirect.Templates;
using System;
using System.Drawing;

namespace StatsDirect.Charting.Renderer
{
    class LinearRegressionChartRenderer : AbstractChartRenderer, IChartRenderer
    {
        public LinearRegressionChartRenderer(ChartDefinition cd, ICanvasFactory canvasFactory)
            : base(cd, canvasFactory)
        {
        }

        ScaleParameters IChartRenderer.GetScaleParameters()
        {
            return new ScaleParameters
            {
                X =
                    {
                        AllowedScaleTypes = new[] { ScaleType.Linear },
                        Max = DataMaxX,
                        Min = DataMinX
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
            const int MARKER_SIZE = 6;

            LinearRegressionOptions lrOptions = (LinearRegressionOptions)definition.ChartOptions;
            double slope = lrOptions.Slope;
            double intercept = lrOptions.Intercept;
            bool fullWidth = lrOptions.FullWidth;

            // Plot a metafile version
            StartVectorPlot();
            AssignMarkersToSeries();
            AxisScales axisScales = DrawAxesOrEnlargeCanvas(definition.ChartOptions.Title, new AxisDefinition(lrOptions.XAxisTitle, AxisMode.Scale, definition.ScaleParameters.X.ScaleType), new AxisDefinition(lrOptions.YAxisTitle, AxisMode.Scale, definition.ScaleParameters.Y.ScaleType), boxAxes, false);

            // plot points
            DoubleSeries xs = definition.XSeries[0].AsDoubleSeries;
            DoubleSeries ys = definition.YSeries[0].AsDoubleSeries;
            double[] xdat = xs.Data;
            double[] ydat = ys.Data;
            PointF[] xys = new PointF[xs.Data.Length];
            for (int r = 0; r < xs.Data.Length; r++)
            {
                if (xdat[r] != Constant.MISSING && ydat[r] != Constant.MISSING)
                {
                    xys[r].X = Convert.ToSingle(ToCanvasX(xdat[r]));
                    xys[r].Y = Convert.ToSingle(ToCanvasY(ydat[r]));
                }
                else
                {
                    xys[r].X = -1;
                    xys[r].Y = -1;
                }
            }
            DrawMarkerSeriesInCanvasCoordinates(xys, MARKER_SIZE, ys.MarkerDetails.MarkerShape, ys.MarkerDetails.IsMarkerFilled, ys.MarkerDetails.MarkerPen, ys.MarkerDetails.LinePen, false, true);

            // Plot regression
            double oldx = Constant.MISSING;
            double oldy = Constant.MISSING;
            double xstep = (axisScales.X.MaximumScaleValue - axisScales.X.MinimumScaleValue) / axisScales.X.Tics().Count / 2.0;
            for (double calcx = fullWidth ? axisScales.X.MinimumScaleValue : axisScales.X.MinimumDataValue; calcx <= (fullWidth ? axisScales.X.MaximumScaleValue : axisScales.X.MaximumDataValue); calcx += xstep)
            {
                double calcy = slope * calcx + intercept;
                if (calcx >= axisScales.X.MinimumScaleValue && calcy >= axisScales.Y.MinimumScaleValue
                    && calcx <= axisScales.X.MaximumScaleValue && calcy <= axisScales.Y.MaximumScaleValue
                    && oldx >= axisScales.X.MinimumScaleValue && oldy >= axisScales.Y.MinimumScaleValue
                    && oldx <= axisScales.X.MaximumScaleValue && oldy <= axisScales.Y.MaximumScaleValue)
                    DrawLineInChartCoordinates(grGreen, calcx, calcy, oldx, oldy);
                oldx = calcx;
                oldy = calcy;
            }

            MaybeDrawMarkerLines(axisScales);
            EndVectorPlot();
            return new ParameterBag();
        }

        internal void PlotLinearRegressionAndMaybeSeCiOrPredictionInterval(string title, double slope, double intercept, bool fullWidth, string xAxisTitle, string yAxisTitle, double PERT, int nx, double MS, double SUMX, double SSX, bool isPredictionInterval, double dataMinY, double dataMaxY)
        {
            DataMinY = dataMinY;
            DataMaxY = dataMaxY;
            StartVectorPlot();
            AxisScales axisScales = PlotLinearRegressionInternal(title, slope, intercept, fullWidth, xAxisTitle, yAxisTitle);
            if (PERT != 0)
                PlotSeCiOrPredictionInterval(PERT, slope, intercept, nx, MS, SUMX, SSX, isPredictionInterval, axisScales);
            EndVectorPlot();
        }

        private AxisScales PlotLinearRegressionInternal(string title, double slope, double intercept, bool fullWidth, string xAxisTitle, string yAxisTitle)
        {
            const int MARKER_SIZE = 6;

            AssignMarkersToSeries();
            //  What extra space do we need before the X axis?
            double xtra = 0;
            if (definition.XSeries.Count > 1)
            {
                foreach (Series s in definition.XSeries)
                {
                    double w = MeasureStringInCanvasCoordinates(s.Title, legendFont).Width + MINIMUM_X_WHITESPACE;
                    if (w > xtra + xAxisCanvas)
                        xtra = w - xAxisCanvas;
                }
            }
            AxisScales axisScales = DrawAxesOrEnlargeCanvas(title, new AxisDefinition(xAxisTitle, AxisMode.Scale, ScaleType.Linear), new AxisDefinition(yAxisTitle, AxisMode.Scale, ScaleType.Linear) { ExtraSpaceBeforeAxisStarts = xtra }, boxAxes, false);

            // plot points
            DoubleSeries xs = definition.XSeries[0].AsDoubleSeries;
            DoubleSeries ys = definition.YSeries[0].AsDoubleSeries;
            double[] xdat = xs.Data;
            double[] ydat = ys.Data;
            PointF[] xys = new PointF[xs.Data.Length];
            for (int r = 0; r < xs.Data.Length; r++)
            {
                if (xdat[r] != Constant.MISSING && ydat[r] != Constant.MISSING)
                {
                    xys[r].X = Convert.ToSingle(ToCanvasX(xdat[r]));
                    xys[r].Y = Convert.ToSingle(ToCanvasY(ydat[r]));
                }
                else
                {
                    xys[r].X = -1;
                    xys[r].Y = -1;
                }
            }
            DrawMarkerSeriesInCanvasCoordinates(xys, MARKER_SIZE, ys.MarkerDetails.MarkerShape, ys.MarkerDetails.IsMarkerFilled, ys.MarkerDetails.MarkerPen, ys.MarkerDetails.LinePen, false, true);

            double xstep = (axisScales.X.MaximumScaleValue - axisScales.X.MinimumScaleValue) / axisScales.X.Tics().Count / 2.0;

            // Plot regression
            double oldx = Constant.MISSING;
            double oldy = Constant.MISSING;
            for (double calcx = fullWidth ? axisScales.X.MinimumScaleValue : axisScales.X.MinimumDataValue; calcx <= (fullWidth ? axisScales.X.MaximumScaleValue : axisScales.X.MaximumDataValue); calcx += xstep)
            {
                double calcy = slope * calcx + intercept;
                if (calcx >= axisScales.X.MinimumScaleValue && calcy >= axisScales.Y.MinimumScaleValue && calcx <= axisScales.X.MaximumScaleValue && calcy <= axisScales.Y.MaximumScaleValue)
                    DrawLineInChartCoordinates(grGreen, calcx, calcy, oldx, oldy);
                oldx = calcx;
                oldy = calcy;
            }
            return axisScales;
        }

        private void PlotSeCiOrPredictionInterval(double pert, double slope, double yIntercept, int nx, double ms, double sumx, double ssx, bool isPredictionInterval, AxisScales axisScales)
        {
            double xstep = (axisScales.X.MaximumScaleValue - axisScales.X.MinimumScaleValue) / 20.0;

            if (isPredictionInterval)
            {
                if (pert != 0)
                {
                    double lastX1P = 0;
                    double lastY1P = 0;
                    double lastX1N = 0;
                    double lastY1N = 0;

                    bool first = true;
                    for (double calcx = axisScales.X.MinimumScaleValue; calcx <= axisScales.X.MaximumScaleValue; calcx += xstep)
                    {
                        double calcy = slope * calcx + yIntercept;
                        double sey = Math.Sqrt(ms * (1.0 + 1.0 / nx + Math.Pow(calcx - sumx / nx, 2.0) / ssx));
                        double pcon = calcy + sey * pert;
                        double x1P = calcx;
                        double y1P = pcon;
                        double ncon = calcy - sey * pert;
                        double x1N = calcx;
                        double y1N = ncon;
                        if (first)
                            first = false;
                        else
                        {
                            if (lastY1P >= axisScales.Y.MinimumScaleValue && lastY1P <= axisScales.Y.MaximumScaleValue && y1P > axisScales.Y.MinimumScaleValue && y1P < axisScales.Y.MaximumScaleValue)
                                DrawLineInChartCoordinates(grBlack, lastX1P, lastY1P, x1P, y1P);
                            if (lastY1N >= axisScales.Y.MinimumScaleValue && lastY1N <= axisScales.Y.MaximumScaleValue && y1N > axisScales.Y.MinimumScaleValue && y1N < axisScales.Y.MaximumScaleValue)
                                DrawLineInChartCoordinates(grBlack, lastX1N, lastY1N, x1N, y1N);
                        }
                        lastY1P = y1P;
                        lastX1P = x1P;
                        lastY1N = y1N;
                        lastX1N = x1N;
                    }
                }
            }
            else
            {
                if (pert != 0)
                {
                    double lastX1P = 0;
                    double lastY1P = 0;
                    double lastX1N = 0;
                    double lastY1N = 0;

                    bool first = true;
                    for (double calcx = axisScales.X.MinimumScaleValue; calcx <= axisScales.X.MaximumScaleValue; calcx += xstep)
                    {
                        double calcy = slope * calcx + yIntercept;
                        double sey = Math.Sqrt(ms * (1.0 / nx + Math.Pow(calcx - sumx / nx, 2.0) / ssx));
                        double pcon = calcy + sey * pert;
                        double x1P = calcx;
                        double y1P = pcon;
                        double ncon = calcy - sey * pert;
                        double x1N = calcx;
                        double y1N = ncon;
                        if (first)
                            first = false;
                        else
                        {
                            if (lastY1P >= axisScales.Y.MinimumScaleValue && lastY1P <= axisScales.Y.MaximumScaleValue && y1P > axisScales.Y.MinimumScaleValue && y1P < axisScales.Y.MaximumScaleValue)
                                DrawLineInChartCoordinates(grBlack, lastX1P, lastY1P, x1P, y1P);
                            if (lastY1N >= axisScales.Y.MinimumScaleValue && lastY1N <= axisScales.Y.MaximumScaleValue && y1N > axisScales.Y.MinimumScaleValue && y1N < axisScales.Y.MaximumScaleValue)
                                DrawLineInChartCoordinates(grBlack, lastX1N, lastY1N, x1N, y1N);
                        }
                        lastY1P = y1P;
                        lastX1P = x1P;
                        lastY1N = y1N;
                        lastX1N = x1N;
                    }

                    first = true;
                    for (double calcx = axisScales.X.MinimumScaleValue; calcx <= axisScales.X.MaximumScaleValue; calcx += xstep)
                    {
                        double calcy = slope * calcx + yIntercept;
                        double sey = Math.Sqrt(ms * (1.0 / nx + Math.Pow(calcx - sumx / nx, 2.0) / ssx));
                        double pcon = calcy + sey;
                        double x1P = calcx;
                        double y1P = pcon;
                        double ncon = calcy - sey;
                        double x1N = calcx;
                        double y1N = ncon;
                        if (first)
                            first = false;
                        else
                        {
                            if (lastY1P >= axisScales.Y.MinimumScaleValue && lastY1P <= axisScales.Y.MaximumScaleValue && y1P > axisScales.Y.MinimumScaleValue && y1P < axisScales.Y.MaximumScaleValue)
                                DrawLineInChartCoordinates(grMagenta, lastX1P, lastY1P, x1P, y1P);
                            if (lastY1N >= axisScales.Y.MinimumScaleValue && lastY1N <= axisScales.Y.MaximumScaleValue && y1N > axisScales.Y.MinimumScaleValue && y1N < axisScales.Y.MaximumScaleValue)
                                DrawLineInChartCoordinates(grMagenta, lastX1N, lastY1N, x1N, y1N);
                        }
                        lastY1P = y1P;
                        lastX1P = x1P;
                        lastY1N = y1N;
                        lastX1N = x1N;
                    }
                }
            }
        }
    }
}
