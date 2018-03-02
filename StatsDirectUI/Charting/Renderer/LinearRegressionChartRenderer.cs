using System;
using System.Drawing;
using StatsDirect.Charting.Scales;
using StatsDirect.Numerics;
using StatsDirect.Templates;

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

            LinearRegressionOptions lrOptions = (LinearRegressionOptions)Definition.ChartOptions;
            double slope = lrOptions.Slope;
            double intercept = lrOptions.Intercept;
            bool fullWidth = lrOptions.FullWidth;

            // Plot a metafile version
            StartVectorPlot();
            AssignMarkersToSeries();
            AxisScales axisScales = LayoutChartAndDrawAxes(Definition.ChartOptions.Title,
                new AxisDefinition(lrOptions.XAxisTitle, AxisMode.Scale, Definition.ScaleParameters.X.ScaleType),
                new AxisDefinition(lrOptions.YAxisTitle, AxisMode.Scale, Definition.ScaleParameters.Y.ScaleType),
                ChartPreferences.DefaultBoxAxes, false);

            // plot points
            DoubleSeries xs = Definition.XSeries[0].AsDoubleSeries;
            DoubleSeries ys = Definition.YSeries[0].AsDoubleSeries;
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
            DrawMarkerSeriesInCanvasCoordinates(xys, MARKER_SIZE, ys.MarkerType, false, true);

            // Plot regression
            double oldx = Constant.MISSING;
            double oldy = Constant.MISSING;
            double xstep = (axisScales.X.MaximumScaleValue - axisScales.X.MinimumScaleValue) / axisScales.X.Tics().Count / 2.0;
            for (double calcx = fullWidth ? axisScales.X.MinimumScaleValue : axisScales.X.MinimumDataValue; calcx <= (fullWidth ? axisScales.X.MaximumScaleValue : axisScales.X.MaximumDataValue); calcx += xstep)
            {
                double calcy = slope * calcx + intercept;
                MaybeDrawLineInChartCoordinates(axisScales, GrGreen, calcx, calcy, oldx, oldy);
                oldx = calcx;
                oldy = calcy;
            }

            MaybeDrawMarkerLines(axisScales);
            EndVectorPlot();
            return new ParameterBag();
        }

        internal void PlotLinearRegressionAndMaybeSeCiOrPredictionInterval(string title, double slope, double intercept, bool fullWidth, string xAxisTitle, string yAxisTitle, double pert, int nx, double ms, double sumx, double ssx, bool isPredictionInterval, double dataMinY, double dataMaxY)
        {
            DataMinY = dataMinY;
            DataMaxY = dataMaxY;
            StartVectorPlot();
            AxisScales axisScales = PlotLinearRegressionInternal(title, slope, intercept, fullWidth, xAxisTitle, yAxisTitle);
            if (pert != 0)
                PlotSeCiOrPredictionInterval(pert, slope, intercept, nx, ms, sumx, ssx, isPredictionInterval, axisScales);
            EndVectorPlot();
        }

        private AxisScales PlotLinearRegressionInternal(string title, double slope, double intercept, bool fullWidth, string xAxisTitle, string yAxisTitle)
        {
            const int MARKER_SIZE = 6;

            AssignMarkersToSeries();
            //  What extra space do we need before the X axis?
            double xtra = 0;
            if (Definition.XSeries.Count > 1)
            {
                foreach (Series s in Definition.XSeries)
                {
                    double w = LegendWidthInCanvasCoordinates(s.Title);
                    if (w > xtra)
                        xtra = w;
                }
            }
            AxisScales axisScales = LayoutChartAndDrawAxes(title,
                new AxisDefinition(xAxisTitle, AxisMode.Scale, ScaleType.Linear) { ExtraSpaceBeforeAxisStarts = xtra },
                new AxisDefinition(yAxisTitle, AxisMode.Scale, ScaleType.Linear),
                ChartPreferences.DefaultBoxAxes, false);

            // plot points
            DoubleSeries xs = Definition.XSeries[0].AsDoubleSeries;
            DoubleSeries ys = Definition.YSeries[0].AsDoubleSeries;
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
            DrawMarkerSeriesInCanvasCoordinates(xys, MARKER_SIZE, ys.MarkerType, false, true);

            double xstep = (axisScales.X.MaximumScaleValue - axisScales.X.MinimumScaleValue) / axisScales.X.Tics().Count / 2.0;

            // Plot regression
            double oldx = Constant.MISSING;
            double oldy = Constant.MISSING;
            for (double calcx = fullWidth ? axisScales.X.MinimumScaleValue : axisScales.X.MinimumDataValue; calcx <= (fullWidth ? axisScales.X.MaximumScaleValue : axisScales.X.MaximumDataValue); calcx += xstep)
            {
                double calcy = slope * calcx + intercept;
                MaybeDrawLineInChartCoordinates(axisScales, GrGreen, calcx, calcy, oldx, oldy);
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
                            MaybeDrawLineInChartCoordinates(axisScales, GrBlack, lastX1P, lastY1P, x1P, y1P);
                            MaybeDrawLineInChartCoordinates(axisScales, GrBlack, lastX1N, lastY1N, x1N, y1N);
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
                            MaybeDrawLineInChartCoordinates(axisScales, GrBlack, lastX1P, lastY1P, x1P, y1P);
                            MaybeDrawLineInChartCoordinates(axisScales, GrBlack, lastX1N, lastY1N, x1N, y1N);
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
                            MaybeDrawLineInChartCoordinates(axisScales, GrMagenta, lastX1P, lastY1P, x1P, y1P);
                            MaybeDrawLineInChartCoordinates(axisScales, GrMagenta, lastX1N, lastY1N, x1N, y1N);
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
