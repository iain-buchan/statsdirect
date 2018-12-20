using System;
using System.Drawing;
using StatsDirect.Charting.Scales;
using StatsDirect.Numerics;

namespace StatsDirect.Charting.Renderer
{
    ///  <summary>
    ///  Converts a chart definition into an ASCII or metafile rendering of that definition.
    ///  </summary>
    internal class ChartRenderer : AbstractChartRenderer
    {
        public ChartRenderer(ChartDefinition definition, ICanvasFactory canvasFactory)
            : base(definition, canvasFactory)
        {
        }

        internal void PlotLinearizedEstimation(string title, int model, double a, double b, string xAxisTitle, string yAxisTitle)
        {
            const int MARKER_SIZE = 6;

            StartVectorPlot();
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
                new AxisDefinition(xAxisTitle, AxisMode.Scale, Definition.ScaleParameters.X.ScaleType) { ExtraSpaceBeforeAxisStarts = xtra },
                new AxisDefinition(yAxisTitle, AxisMode.Scale, Definition.ScaleParameters.Y.ScaleType),
                ChartPreferences.DefaultBoxAxes, false);

            // plot points
            DoubleSeries xs = Definition.XSeries[0].AsDoubleSeries;
            DoubleSeries ys = Definition.YSeries[0].AsDoubleSeries;
            double[] xdat = xs.Data;
            double[] ydat = ys.Data;
            PointF[] xys = new PointF[xs.Data.Length];
            for (int r = 0; r <= xs.Data.Length - 1; r++)
            {
                if (xdat[r] != Constant.MISSING & ydat[r] != Constant.MISSING)
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

            double xstep = (axisScales.X.MaximumScaleValue - axisScales.X.MinimumScaleValue) / axisScales.X.Tics().Count / 2;

            // Plot regression
            PenDescriptor p = new PenDescriptor(GrBlack, 2);
            double oldx = Constant.MISSING;
            double oldy = Constant.MISSING;
            for (double calcx = axisScales.X.MinimumScaleValue; calcx <= axisScales.X.MaximumScaleValue; calcx += xstep)
            {
                double calcy;
                switch (model)
                {
                    case 0:
                        calcy = a * Math.Exp(b * calcx);
                        break;
                    case 1:
                        calcy = a * Math.Pow(calcx, b);
                        break;
                    case 2:
                        double denom = a + calcx * b;
                        if (denom == 0.0)
                            denom = 0.0000001;
                        calcy = calcx / denom;
                        break;
                    default:
                        throw new Exception("Unknown mode");
                }

                if (oldx >= axisScales.X.MinimumScaleValue && oldx <= axisScales.X.MaximumScaleValue
                    && calcy >= axisScales.Y.MinimumScaleValue && calcy <= axisScales.Y.MaximumScaleValue
                    && oldy >= axisScales.Y.MinimumScaleValue && oldy <= axisScales.Y.MaximumScaleValue)
                    DrawLineInChartCoordinates(p, calcx, calcy, oldx, oldy);
                oldx = calcx;
                oldy = calcy;
            }
            EndVectorPlot();
        }

        internal void PlotPolynomialRegression(string title, int mode, double[,] xtxi, double[] bd, double rss, int nx, int p, double gamma, string xAxisTitle, string yAxisTitle)
        {
            const int MARKER_SIZE = 6;

            StartVectorPlot();
            AssignMarkersToSeries();
            //  What extra space do we need before the X axis?
            double xtra = 0;
            if (Definition.XSeries.Count > 1)
            {
                foreach (Series ser in Definition.XSeries)
                {
                    double w = LegendWidthInCanvasCoordinates(ser.Title);
                    if (w > xtra + XAxisCanvas)
                        xtra = w - XAxisCanvas;
                }
            }

            AxisScales axisScales = LayoutChartAndDrawAxes(title,
                new AxisDefinition(xAxisTitle, AxisMode.Scale, Definition.ScaleParameters.X.ScaleType) { ExtraSpaceBeforeAxisStarts = xtra },
                new AxisDefinition(yAxisTitle, AxisMode.Scale, Definition.ScaleParameters.Y.ScaleType),
                ChartPreferences.DefaultBoxAxes, false);

            // plot points
            DoubleSeries xs = Definition.XSeries[0].AsDoubleSeries;
            DoubleSeries ys = Definition.YSeries[0].AsDoubleSeries;
            double[] xdat = xs.Data;
            double[] ydat = ys.Data;
            PointF[] xys = new PointF[xs.Data.Length];
            for (int r = 0; r < xs.Data.Length; r++)
            {
                if (xdat[r] != Constant.MISSING & ydat[r] != Constant.MISSING)
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

            MathDbl.civ(nx - p, out double cit, gamma, out double _);
            double rdf = Convert.ToDouble(nx - 1 - (p - 1));
            double rms = rss / rdf;
            double[] px = new double[p + 1];
            px[1] = 1.0;
            double xstep = (axisScales.X.MaximumScaleValue - axisScales.X.MinimumScaleValue) / axisScales.X.Tics().Count / 2;

            // This routine has changed from the original
            // It is more efficient in drawing - but bigger in code
            // Draw the base line
            {
                double oldx = Constant.MISSING;
                double oldy = Constant.MISSING;
                for (double calcx = axisScales.X.MinimumScaleValue; calcx <= axisScales.X.MaximumScaleValue; calcx += xstep)
                {
                    double calcy = 0;
                    for (int jj = 1; jj <= p; jj++)
                        calcy += bd[jj] * Math.Pow(calcx, Convert.ToDouble(jj - 1));
                    double x1 = calcx;
                    double y1 = calcy;
                    MaybeDrawLineInChartCoordinates(axisScales, GrGreen, x1, y1, oldx, oldy);
                    oldx = x1;
                    oldy = y1;
                }
            }
            if (mode > 0)
            {
                double oldx = Constant.MISSING;
                double oldy = Constant.MISSING;
                // Draw -Lines
                for (double calcx = axisScales.X.MinimumScaleValue; calcx <= axisScales.X.MaximumScaleValue; calcx += xstep)
                {
                    double calcy = 0;
                    for (int jj = 1; jj <= p; jj++)
                        calcy += bd[jj] * Math.Pow(calcx, jj - 1);
                    px[1] = 1.0;
                    for (int k = 2; k <= p; k++)
                        px[k] = Math.Pow(calcx, k - 1);
                    double xcx = 0;
                    for (int i = 1; i <= p; i++)
                    {
                        double s = 0;
                        for (int k = 1; k <= p; k++)
                            s += xtxi[i, k] * px[k];
                        xcx += s * px[i];
                    }
                    double sey = Math.Sqrt(mode == 1 ? Math.Abs(rms * xcx) : Math.Abs(rms * (1.0 + xcx)));
                    double cl = cit * sey;
                    double x1 = calcx;
                    double y1 = calcy - cl;
                    MaybeDrawLineInChartCoordinates(axisScales, GrBlack, x1, y1, oldx, oldy);
                    oldx = x1;
                    oldy = y1;
                }
                // Draw +Lines
                oldx = Constant.MISSING;
                oldy = Constant.MISSING;
                for (double calcx = axisScales.X.MinimumScaleValue; calcx <= axisScales.X.MaximumScaleValue; calcx += xstep)
                {
                    double calcy = 0;
                    for (int jj = 1; jj <= p; jj++)
                        calcy += bd[jj] * Math.Pow(calcx, jj - 1);
                    px[1] = 1.0;
                    for (int k = 2; k <= p; k++)
                        px[k] = Math.Pow(calcx, k - 1);
                    double xcx = 0;
                    for (int i = 1; i <= p; i++)
                    {
                        double s = 0;
                        for (int k = 1; k <= p; k++)
                            s += xtxi[i, k] * px[k];
                        xcx += s * px[i];
                    }
                    double sey = Math.Sqrt(mode == 1 ? Math.Abs(rms * xcx) : Math.Abs(rms * (1.0 + xcx)));
                    double cl = cit * sey;
                    double x1 = calcx;
                    double y1 = calcy + cl;
                    MaybeDrawLineInChartCoordinates(axisScales, GrBlack, x1, y1, oldx, oldy);
                    oldx = x1;
                    oldy = y1;
                }
            }
            EndVectorPlot();
        }

        ///  <remarks>Jul 09: updated to put log models on a log x axis scale</remarks>
        internal void PlotLogit(string title, int model, double t, double sw, double s1, double a, double b, string xAxisTitle, string yAxisTitle, bool modelIsLog10)
        {
            const int MARKER_SIZE = 6;

            DoubleSeries xs = Definition.XSeries[0].AsDoubleSeries;
            DoubleSeries ys = Definition.YSeries[0].AsDoubleSeries;
            double[] xdat = xs.Data;
            double[] ydat = ys.Data;

            double cl = 0;
            int nx = 0;
            foreach (double v in xdat)
            {
                if (v != Constant.MISSING)
                {
                    cl += modelIsLog10 ? Math.Log10(v) : v;
                    nx++;
                }
            }
            double xm = cl / nx;

            StartVectorPlot();
            AssignMarkersToSeries();
            if (DataMaxY - DataMinY > 0.25)
            {
                DataMaxY = 1;
                DataMinY = 0;
            }
            AxisScales axisScales = LayoutChartAndDrawAxes(title,
                new AxisDefinition(xAxisTitle, AxisMode.Scale, Definition.ScaleParameters.X.ScaleType),
                new AxisDefinition(yAxisTitle, AxisMode.Scale, Definition.ScaleParameters.Y.ScaleType),
                false, false);

            // plot points
            PointF[] xys = new PointF[Math.Min(xdat.Length, ydat.Length)];
            for (int r = 0; r < Math.Min(xdat.Length, ydat.Length); r++)
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

            // Aim for 100 steps across the chart - anything coarser gives terrible resolution for tight curves (e.g. log10 of the sample data).
            double xstepCanvas = (ToCanvasWidth(axisScales.X.MaximumScaleValue) - ToCanvasWidth(axisScales.X.MinimumScaleValue)) / 100.0;

            // Draw central curve
            double oldx = Constant.MISSING;
            double oldy = Constant.MISSING;
            for (double xCanvas = ToCanvasWidth(axisScales.X.MinimumScaleValue); xCanvas <= ToCanvasWidth(axisScales.X.MaximumScaleValue); xCanvas += xstepCanvas)
            {
                double x = FromCanvasWidth(xCanvas);
                double calcX = modelIsLog10 ? Math.Log10(x) : x;
                double calcY = a + b * calcX;
                calcY = Remodel(model, calcY);
                MaybeDrawLineInChartCoordinates(axisScales, GrGreen, x, calcY, oldx, oldy);
                oldx = x;
                oldy = calcY;
            }

            // Draw upper and lower curves
            oldx = Constant.MISSING;
            oldy = Constant.MISSING;
            for (double calcxCanvas = ToCanvasWidth(axisScales.X.MinimumScaleValue); calcxCanvas <= ToCanvasWidth(axisScales.X.MaximumScaleValue); calcxCanvas += xstepCanvas)
            {
                double x = FromCanvasWidth(calcxCanvas);
                double calcX = modelIsLog10 ? Math.Log10(x) : x;
                cl = t * Math.Sqrt(1.0 / sw + Math.Pow(calcX - xm, 2.0) / s1);
                double calcY = a + b * calcX + cl;
                calcY = Remodel(model, calcY);
                MaybeDrawLineInChartCoordinates(axisScales, GrMagenta, x, calcY, oldx, oldy);
                oldx = x;
                oldy = calcY;
            }
            // Draw lower curve
            oldx = Constant.MISSING;
            oldy = Constant.MISSING;
            for (double calcxCanvas = ToCanvasWidth(axisScales.X.MinimumScaleValue); calcxCanvas <= ToCanvasWidth(axisScales.X.MaximumScaleValue); calcxCanvas += xstepCanvas)
            {
                double x = FromCanvasWidth(calcxCanvas);
                double calcX = modelIsLog10 ? Math.Log10(x) : x;
                cl = t * Math.Sqrt(1.0 / sw + Math.Pow(calcX - xm, 2.0) / s1);
                double calcY = a + b * calcX - cl;
                calcY = Remodel(model, calcY);
                MaybeDrawLineInChartCoordinates(axisScales, GrMagenta, x, calcY, oldx, oldy);
                oldx = x;
                oldy = calcY;
            }
            EndVectorPlot();
        }

        private static double Remodel(int model, double calcY)
        {
            if (model == 1)
                return PDF.alnorm(calcY);
            return Math.Exp(calcY * 2.0) / (1.0 + Math.Exp(calcY * 2.0));
        }
    }
}
