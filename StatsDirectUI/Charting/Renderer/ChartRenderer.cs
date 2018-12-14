using System;
using System.Drawing;
using Layout;
using StatsDirect.Charting.Scales;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.Templates;
using StatsDirect.Utilities;

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

        internal void PlotCox2(int[] gn, int igroups, double[] xp, double[] yp, ColumnData[] cdat1, int groupid)
        {
            Legend legend = new Legend();
            for (int i = 1; i <= igroups; i++)
            {
                MarkerType mt = new MarkerType { MarkerShape = (MarkerShape)i, MarkerColor = GrBlack, MarkerSize = 6 };
                string vq = cdat1[groupid].Title.Substring(0, Math.Min(20, cdat1[groupid].Title.Length)) + "=" + cdat1[groupid].Groups[i - 1].Label;
                legend.LegendEntries.Add(new LegendEntry { Label = vq, MarkerType = mt });
            }

            StartVectorPlot(null, legend);

            //  TODO: Log and log-log axes here
            LayoutChartAndDrawAxes("Log-log plot (parallel groups if hazards proportional)",
                new AxisDefinition("log(Time)", AxisMode.Scale, ScaleType.Linear),
                new AxisDefinition("-log(-log(Survival))", AxisMode.Scale, ScaleType.Linear),
                false, false,
                legend);

            PenDescriptor p = new PenDescriptor(GrBlack, 1);
            int istart = 0;
            for (int k = 1; k <= 2; k++)
            {
                PointF[] xys = new PointF[gn[k] + 1];
                xys[0].X = -1;
                xys[0].Y = -1;
                for (int i = 1; i <= gn[k]; i++)
                {
                    if (xp[istart + i] != Constant.MISSING && yp[istart + i] != Constant.MISSING)
                    {
                        xys[i].X = (float)ToCanvasX(xp[istart + i]);
                        xys[i].Y = (float)ToCanvasY(yp[istart + i]);
                    }
                    else
                    {
                        xys[i].X = -1;
                        xys[i].Y = -1;
                    }
                }
                DrawMarkerSeriesInCanvasCoordinates(xys, 6, (MarkerShape)k, false, p, p, true, true);
                istart += gn[k];
            }
            DrawLegend(legend);
            EndVectorPlot();
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

        public void PlotXY(double[] x, double[] y, string xtxt, string ytxt, string title, bool zPlot, DataMinMax minMaxY, bool useCalculatedScalesEvenWithDefinition, ChartAreaShape chartAreaShape)
        {
            StartVectorPlot();
            PlotXYInternal(x, y, xtxt, ytxt, title, zPlot, minMaxY, 6, MarkerShape.Circle, false, PenDescriptor.Black, useCalculatedScalesEvenWithDefinition, chartAreaShape);
            EndVectorPlot();
        }

        public void PlotXY0To1(double[] x, double[] y, string xtxt, string ytxt, string title, bool zPlot, DataMinMax minMaxY, bool useCalculatedScalesEvenWithDefinition, ChartAreaShape chartAreaShape)
        {
            StartVectorPlot();
            PlotXYInternal(x, y, xtxt, ytxt, title, zPlot, minMaxY, 6, MarkerShape.Circle, false, PenDescriptor.Black, useCalculatedScalesEvenWithDefinition, chartAreaShape, 0, 1, 0, 1);
            EndVectorPlot();
        }

        internal void PlotXYR(double[,] x, double[,,] y, int ng, int[] gn, int[,] nr, double[] b, double[] a, string xtxt, string ytxt, string title, string[] bnam, double dataMinX, double dataMaxX, double dataMinY, double dataMaxY)
        {
            DataMinX = dataMinX;
            DataMaxX = dataMaxX;
            DataMinY = dataMinY;
            DataMaxY = dataMaxY;

            Legend legend = null;
            if (ng > 1)
            {
                legend = new Legend();
                for (int g = 1; g <= ng; g++)
                {
                    MarkerType mt = ChartPreferences.MarkerTypes[ChartOptions.SeriesNumberToMarkerNumber(g - 1)];
                    legend.LegendEntries.Add(new LegendEntry { Label = bnam[g], MarkerType = mt });
                }
            }

            StartVectorPlot(null, legend);
            LayoutChartAndDrawAxes(title,
                new AxisDefinition(xtxt, AxisMode.Scale, ScaleType.Linear),
                new AxisDefinition(ytxt, AxisMode.Scale, ScaleType.Linear),
                false, false,
                legend);

            // Plot the points
            for (int g = 1; g <= ng; g++)
            {
                int mkr = ChartOptions.SeriesNumberToMarkerNumber(g - 1);
                MarkerType t = ChartPreferences.MarkerTypes[mkr];
                PenDescriptor p = GetMarkerPen(t);
                    double minx = double.MaxValue;
                    double maxx = double.MinValue;
                    double miny = double.MaxValue;
                    double maxy = double.MinValue;
                    double x1;
                    double y1;
                    for (int r = 1; r <= gn[g]; r++)
                    {
                        PointF[] xys = new PointF[nr[g, r]];
                        for (int k = 1; k <= nr[g, r]; k++)
                        {
                            if (x[g, r] != Constant.MISSING)
                            {
                                x1 = ToCanvasX(x[g, r]);
                                if (x[g, r] > maxx)
                                    maxx = x[g, r];
                                if (x[g, r] < minx)
                                    minx = x[g, r];
                                if (y[g, r, k] != Constant.MISSING)
                                {
                                    y1 = ToCanvasY(y[g, r, k]);
                                    if (y[g, r, k] > maxy)
                                        maxy = y[g, r, k];
                                    if (y[g, r, k] < miny)
                                        miny = y[g, r, k];
                                    xys[k - 1].X = (float)x1;
                                    xys[k - 1].Y = (float)y1;
                                }
                                else
                                {
                                    xys[k - 1].X = -1;
                                    xys[k - 1].Y = -1;
                                }
                            }
                            else
                            {
                                xys[k - 1].X = -1;
                                xys[k - 1].Y = -1;
                            }
                        }
                        DrawMarkerSeriesInCanvasCoordinates(xys, 6, t.MarkerShape, t.IsMarkerFilled, p, p, false, true);
                    }
                    x1 = ToCanvasX(minx);
                    double calcy = a[g] + b[g] * minx;
                    if (calcy < miny)
                    {
                        calcy = miny;
                        if (b[g] != 0.0)
                            x1 = ToCanvasX((calcy - a[g]) / b[g]);
                    }
                    else if (calcy > maxy)
                    {
                        calcy = maxy;
                        if (b[g] != 0.0)
                            x1 = ToCanvasX((calcy - a[g]) / b[g]);
                    }
                    y1 = ToCanvasY(calcy);
                    double x2 = ToCanvasX(maxx);
                    calcy = a[g] + b[g] * maxx;
                    if (calcy < miny)
                    {
                        calcy = miny;
                        if (b[g] != 0.0)
                            x2 = ToCanvasX((calcy - a[g]) / b[g]);
                    }
                    else if (calcy > maxy)
                    {
                        calcy = maxy;
                        if (b[g] != 0.0)
                            x2 = ToCanvasX((calcy - a[g]) / b[g]);
                    }
                    DrawLineInCanvasCoordinates(p, x1, y1, x2, ToCanvasY(calcy));
            }
            DrawLegend(legend);
            EndVectorPlot();
        }

        internal void PlotXYZ(double[] x, double[] y, double[] z, int lowerBound, int rows, string xtxt, string ytxt, string title, bool zPlot, DataMinMax minMaxY, MarkerType markerType, double? labbePool = default(double?))
        {
            StartVectorPlot();
            double rmh = 0;
            bool isLAabbe = labbePool.HasValue;
            if (isLAabbe)
            {
                rmh = labbePool.Value;
                DataMaxX = 100;
                DataMaxY = 100;
                DataMinX = 0;
                DataMinY = 0;
            }
            else
            {
                // Get the Min and Max for the data
                if (minMaxY != DataMinMax.XPreset_YPreset)
                {
                    Range dataRangeX = GetMinMaxArray(x, Definition.ScaleParameters.X.ScaleType);
                    DataMinX = dataRangeX.Min;
                    DataMaxX = dataRangeX.Max;
                    if (minMaxY == DataMinMax.XCalc_YCalc)
                    {
                        Range dataRangeY = GetMinMaxArray(y, Definition.ScaleParameters.Y.ScaleType);
                        DataMinY = dataRangeY.Min;
                        DataMaxY = dataRangeY.Max;
                    }
                }
            }
            AxisScales axisScales = LayoutChartAndDrawAxes(title,
                new AxisDefinition(xtxt, AxisMode.Scale, ScaleType.Linear),
                new AxisDefinition(ytxt, AxisMode.Scale, ScaleType.Linear),
                isLAabbe, false);

            if (zPlot)
                DrawQCanvas(OffY);

            // Plot the points
            double maxz = double.MinValue;
            double sumz = 0;
            int[] scalez = new int[rows + 1];
            for (int r = lowerBound; r < rows + lowerBound; r++)
            {
                sumz += z[r];
                if (z[r] > maxz)
                    maxz = z[r];
            }
            double meanz = sumz / rows;
            double maxdev = maxz / meanz;
            if (maxdev > 5.0)
                meanz = meanz * maxdev / 5.0;
            for (int r = lowerBound; r < rows + lowerBound; r++)
            {
                scalez[r] = Convert.ToInt32(6.0 * z[r] / meanz);
                if (scalez[r] < 3)
                    scalez[r] = 3;
            }

            if (isLAabbe)
            {
                for (int r = lowerBound; r < rows + lowerBound; r++)
                {
                    //  IEB Jul 09
                    if (x[r] != Constant.MISSING && y[r] != Constant.MISSING)
                        DrawMarkerInChartCoordinates(100.0 * x[r], 100.0 * y[r], scalez[r], markerType);
                }
            }
            else
            {
                for (int r = lowerBound; r < rows + lowerBound; r++)
                {
                    //  IEB Jul 09
                    if (x[r] != Constant.MISSING && y[r] != Constant.MISSING)
                        DrawMarkerInChartCoordinates(x[r], y[r], scalez[r], markerType);
                }
            }

            // LAbbe plot specific null and pooled effect lines
            if (isLAabbe)
            {
                // null effect diagonal
                DrawLineInChartCoordinates(GrBlack, axisScales.X.MinimumScaleValue, axisScales.Y.MinimumScaleValue, axisScales.X.MaximumScaleValue, axisScales.Y.MaximumScaleValue);
                // pooled event rate
                double x1;
                double y1;
                if (rmh >= 1)
                {
                    y1 = ToCanvasY(axisScales.Y.MaximumScaleValue);
                    x1 = ToCanvasX(axisScales.Y.MaximumScaleValue / rmh);
                }
                else
                {
                    x1 = ToCanvasX(axisScales.X.MaximumScaleValue);
                    y1 = ToCanvasY(rmh * axisScales.X.MaximumScaleValue);
                }
                DrawLineInCanvasCoordinates(GetLinePen(ChartPreferences.MarkerTypes[10], false), ToCanvasX(axisScales.X.MinimumScaleValue), ToCanvasY(axisScales.Y.MinimumScaleValue), x1, y1);
            }
            EndVectorPlot();
        }

        ///  <summary>
        ///  
        ///  </summary>
        ///  <param name="k">The number of elements in o(,)</param>
        ///  <param name="o">A 1-based array of values</param>
        ///  <param name="rmh"></param>
        ///  <remarks></remarks>
        internal void PlotLAbbe(int k, double[,] o, double rmh)
        {
            double[] y = new double[k + 1];
            double[] x = new double[k + 1];
            double[] w = new double[k + 1];
            for (int i = 1; i <= k; i++)
            {
                if (o[i, 1] + o[i, 3] == 0)
                    y[i] = 0;
                else
                    y[i] = o[i, 1] / (o[i, 1] + o[i, 3]);
                if (o[i, 2] + o[i, 4] == 0)
                    x[i] = 0;
                else
                    x[i] = o[i, 2] / (o[i, 2] + o[i, 4]);
                w[i] = o[i, 1] + o[i, 2] + o[i, 3] + o[i, 4];
            }
            PlotXYZ(x, y, w, 1, k, "control percent", "experimental percent", "L'Abbe plot (symbol size represents sample size)", false, 0, ChartPreferences.MarkerTypes[0], rmh);
        }

        internal void x_plGraphInternal(int[,] dead, int groups, int[] cnx, string[] glab, bool tic, bool marker, double[,] x, double[,] y, int plotMode, string xAxisTitle, string yAxisTitle, string title)
        {
            Legend legend = null;
            if (groups > 1)
            {
                legend = new Legend { Position = LegendPosition.Left };
                for (int k = 1; k <= groups; k++)
                {
                    MarkerType mt = ChartPreferences.MarkerTypes[(k - 1) % 9].Clone();
                    if (!marker)
                    {
                        mt.MarkerShape = MarkerShape.SurvivalTic;
                        mt.IsMarkerFilled = false;
                    }
                    legend.LegendEntries.Add(new LegendEntry { Label = glab[k], MarkerType = mt });
                }
            }

            StartVectorPlot(null, legend);
            DataMaxX = double.MinValue;
            DataMaxY = double.MinValue;
            DataMinX = double.MaxValue;
            DataMinY = double.MaxValue;
            for (int k = 1; k <= groups; k++)
            {
                for (int j = 1; j <= cnx[k]; j++)
                {
                    if (x[j, k] > DataMaxX)
                        DataMaxX = x[j, k];
                    if (x[j, k] < DataMinX)
                        DataMinX = x[j, k];
                    if (y[j, k] > DataMaxY)
                        DataMaxY = y[j, k];
                    if (y[j, k] < DataMinY)
                        DataMinY = y[j, k];
                }
            }
            if (plotMode == 1)
            {
                DataMaxY = 1;
                DataMinY = 0;
            }
            AxisScales axisScales = LayoutChartAndDrawAxes(title,
                new AxisDefinition(xAxisTitle, AxisMode.Scale, ScaleType.Linear),
                new AxisDefinition(yAxisTitle, AxisMode.Scale, ScaleType.Linear),
                false, false,
                legend);

            for (int k = 1; k <= groups; k++)
            {
                MarkerType mt = ChartPreferences.MarkerTypes[(k - 1) % 9];
                double x1; double y1;
                switch (plotMode)
                {
                    case 1:
                        x1 = axisScales.X.MinimumScaleValue;
                        y1 = 1.0;
                        break;
                    case 2:
                        x1 = axisScales.X.MinimumScaleValue;
                        y1 = 0;
                        break;
                    case 3:
                        x1 = x[1, k];
                        y1 = y[1, k];
                        break;
                    case 4:
                        x1 = x[1, k];
                        y1 = y[1, k];
                        break;
                    case 5:
                        x1 = x[1, k];
                        y1 = y[1, k];
                        break;
                    default:
                        throw new Exception("Unknown plot mode");
                }

                for (int j = 1; j <= cnx[k]; j++)
                {
                    double x2 = x[j, k];
                    double y2 = y[j, k];
                    // Draw the markers
                    // If Y2 <> Y1 Then Draw_Marker X2, Y2, 6, (k - 1) Mod 9
                    // changed to tic mark at censor points March 01
                    if (dead[j, k] == 0 && tic)
                        DrawLineInChartCoordinates(mt.LineColor, x2, y2, x2, y2 + FromCanvasHeight(7));
                    if (dead[j, k] != 0 && marker)
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
        }

        internal void PlotEffect(ITemplateHost host, int k, double[] cn, double[] en, string[] title, double rmh, double ll, double ul, double cco, double[] odr, double[] odrl, double[] odru, string cap, int pbias, string qid)
        {
            ScaleHeight(k);

            StartVectorPlot();

            double[] gn = new double[k + 1];
            int kok = 0;
            double ormax = double.NegativeInfinity;
            double ormin = double.PositiveInfinity;
            double orumax = double.NegativeInfinity;
            double orlmin = double.PositiveInfinity;
            double maxGn = double.NegativeInfinity;
            for (int i = 1; i <= k; i++)
            {
                gn[i] = cn[i] + en[i];
                if (gn[i] > maxGn)
                    maxGn = gn[i];
                if (odr[i] != Constant.MISSING)
                {
                    kok = kok + 1;
                    if (odr[i] > ormax)
                        ormax = odr[i];
                    if (odr[i] < ormin)
                        ormin = odr[i];
                    if (odrl[i] < orlmin)
                        orlmin = odrl[i];
                    if (odru[i] > orumax)
                        orumax = odru[i];
                }
            }

            DataMaxX = ormax;
            DataMinX = ormin;
            if (DataMaxX < rmh)
                DataMaxX = rmh;
            if (DataMaxX < ul && ul != Constant.MISSING)
                DataMaxX = ul;
            if (DataMaxX < orumax && orumax != Constant.MISSING)
                DataMaxX = orumax;
            if (DataMinX > rmh)
                DataMinX = rmh;
            if (DataMinX > ll && ll != Constant.MISSING)
                DataMinX = ll;
            if (DataMinX > orlmin && orlmin != Constant.MISSING)
                DataMinX = orlmin;

            IAxisScale xAxisScale = (ILinearAxisScale)AxisScalerFactory.AxisScalerFor(ScaleType.Linear).QAxis(DataMinX, 0, DataMaxX, false, false);
            DataMinX = xAxisScale.MinimumScaleValue;
            DataMaxX = xAxisScale.MaximumScaleValue;

            double xtra = 0;
            for (int i = 1; i <= k; i++)
            {
                if (odr[i] != Constant.MISSING)
                {
                    double w = LabelWidthInCanvasCoordinates(title[i]) + 30;
                    if (w > xtra + XAxisCanvas)
                        xtra = w - XAxisCanvas - 5;
                }
            }
            AxisScales axisScales = LayoutChartAndDrawAxes(cap,
                new AxisDefinition(null, AxisMode.Scale, ScaleType.Linear) { ExtraSpaceBeforeAxisStarts = xtra },
                new AxisDefinition(null, AxisMode.None, ScaleType.NotSet),
                false, false);
            axisScales.Y = new CategoryAxisScale(kok + pbias);
            DivY = kok + pbias;
            OffY = YAxisCanvas;

            PenDescriptor linePen = GetLinePen(ChartPreferences.MarkerTypes[10], true);
                double yc = 0;
                double yt = 0;
                int r = 0;
                double txh = LabelHeightInCanvasCoordinates(title[1]);
                for (int i = k; i >= 1; i--)
                {
                    if (odr[i] != Constant.MISSING)
                    {
                        r++;
                        double yctr = (r + pbias - 0.5) / DivY * YExtCanvas;
                        double ytop = (r + pbias) / DivY * YExtCanvas;
                        double xm = ToCanvasX(odr[i]);
                        double xl = ToCanvasX(odrl[i]);
                        double xr = ToCanvasX(odru[i]);
                        double y2 = (ytop - yctr) / 1.5;
                        yc = OffY + yctr;
                        yt = OffY + yctr + y2;
                        double yb = OffY + yctr - y2;
                        // CI line
                        DrawLineInCanvasCoordinates(linePen, xl, yc, xr, yc);
                        // Weight blob
                        DrawSquareInCanvasCoordinates(linePen, xm, yc, (5 + Math.Abs(yt - yb) * (gn[i] / maxGn)) * 0.7, true);
                        DrawStringLabel(title[i], XAxisCanvas - 15, yc + txh / 2, StringAlignment.Far);
                    }
                }

                if (DataMinX <= 0)
                {
                    double xm = OffX;
                    DrawLineInCanvasCoordinates(linePen, xm, yt, xm, YAxisCanvas - 12);
                }

                if (pbias == 1)
                {
                    double saveYc = yc;
                    double yctr = ToCanvasHeight(0.5);
                    double diamondHalfSize = yctr / 1.5;
                    yc = ToCanvasY(0.5);
                    yt = OffY + yctr + diamondHalfSize;
                    DrawDiamondInCanvasCoordinates(linePen, ToCanvasX(rmh), yc, diamondHalfSize * 2, false);
                    DrawLineInCanvasCoordinates(linePen, ToCanvasX(ul), yc, ToCanvasX(ll), yc);
                // pooled effect marker
                PenDescriptor pooledEffectPen = GetLinePen(ChartPreferences.MarkerTypes[10], false);
                        DrawLineInCanvasCoordinates(pooledEffectPen, ToCanvasX(rmh), saveYc, ToCanvasX(rmh), yt);
                    string lab = "pooled " + qid + " = " + host.RoundU(rmh) + "  (" + Formatting.XRound(cco * 100, 1) + "% CI = " + host.RoundU(ll) + " to " + host.RoundU(ul) + ")";
                    string xlab = cap.IndexOf("fixed", StringComparison.Ordinal) + 1 != 0 ? string.Empty : "DL ";
                    lab = xlab + lab;
                    DrawStringLabel(lab, XAxisCanvas + XExtCanvas / 2.0, 50, StringAlignment.Center);
                }
            EndVectorPlot();
        }
    }
}
