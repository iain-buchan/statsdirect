using System;
using System.Drawing;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.Templates;
using StatsDirect.Utilities;

namespace StatsDirect.Charting.Renderer
{
    ///  <summary>
    ///  Converts a chart definition into an ASCII or metafile rendering of that definition.
    ///  </summary>
    public class ChartRenderer : AbstractChartRenderer
    {
        public ChartRenderer(ChartDefinition definition, ICanvasFactory canvasFactory)
            : base(definition, canvasFactory)
        {
        }

        internal void PlotCox2(int[] gn, int igroups, double[] xp, double[] yp, ColumnData[] cdat1, int groupid)
        {
            StartVectorPlot();
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

            //  TODO: Log and log-log axes here
            DrawAxesOrEnlargeCanvas("Log-log plot (parallel groups if hazards proportional)", new AxisDefinition("log(Time)", AxisMode.Scale, ScaleType.Linear), new AxisDefinition("-log(-log(Survival))", AxisMode.Scale, ScaleType.Linear) { ExtraSpaceBeforeAxisStarts = xtra }, false, false);

            // Draw the legends
            using (Pen p = new Pen(grBlack, 1))
            {
                float size2 = labelFont.Size * 2;
                for (int i = 1; i <= igroups; i++)
                {
                    DrawMarkerInCanvasCoordinates(12, yAxisCanvas + yExtCanvas - 22 - (size2 * i), 6, ((MarkerShape)(i)), false, p);
                    DrawStringLegendL(cdat1[groupid].Title.Substring(0, Math.Min(20, cdat1[groupid].Title.Length)) + "=" + cdat1[groupid].Groups[i - 1].Label, 24, yAxisCanvas + yExtCanvas - 10 - (size2 * i));
                }

                // plot points
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
            }
            EndVectorPlot();
        }

        internal void PlotCox1(CoxP[] z, int iobs, int istrata, CoxPlotMode plotMode, int igroups, int groupid, bool grouped, bool stratified, double[,,] ARR3, ColumnData[] cdat1, bool use_tic, bool use_marker, int[] gn)
        {
            DataMaxX = double.MinValue;
            DataMaxY = double.MinValue;
            DataMinX = double.MaxValue;
            DataMinY = double.MaxValue;
            const string tim = "Time";
            string xAxisSuffix = grouped ? "(individual)" : "(baseline)";
            string title;
            string xAxisTitle;
            string yAxisTitle;
            switch (plotMode)
            {
                case CoxPlotMode.Survival:
                    xAxisTitle = tim;
                    yAxisTitle = "Survival Probability " + xAxisSuffix;
                    title = "Survival Plot (Cox regression)";
                    DataMaxY = 1;
                    DataMinY = 0;
                    for (int i = 1; i <= iobs; i++)
                    {
                        if (z[i].Time > DataMaxX)
                            DataMaxX = z[i].Time;
                        if (z[i].Time < DataMinX)
                            DataMinX = z[i].Time;
                    }
                    break;
                case CoxPlotMode.Hazard:
                    xAxisTitle = tim;
                    yAxisTitle = "Cumulative Hazard " + xAxisSuffix;
                    title = "Hazard Plot (Cox regression)";
                    for (int i = 1; i <= iobs; i++)
                    {
                        if (z[i].Time > DataMaxX)
                            DataMaxX = z[i].Time;
                        if (z[i].Time < DataMinX)
                            DataMinX = z[i].Time;
                        double haz;
                        if (grouped)
                        {
                            haz = Math.Pow(z[i].S, Math.Exp(Convert.ToDouble(z[i].Id) * ARR3[1, groupid, 1]));
                            haz = haz > 0.0 ? -Math.Log(haz) : Constant.MISSING;
                        }
                        else
                            haz = z[i].H;
                        if (haz != Constant.MISSING & haz > DataMaxY & z[i].Censor != 0)
                            DataMaxY = haz;
                        if (haz != Constant.MISSING & haz < DataMinY & z[i].Censor != 0)
                            DataMinY = haz;
                    }
                    break;
                default:
                    throw new Exception("Unexpected case");
            }

            StartVectorPlot();
            AssignMarkersToSeries();
            double xtra = 0;
            if (stratified)
            {
                for (int i = 1; i <= istrata; i++)
                {
                    double w = MeasureStringInCanvasCoordinates("Stratum " + i, legendFont).Width + MINIMUM_X_WHITESPACE;
                    if (w > xtra + xAxisCanvas)
                        xtra = w - xAxisCanvas - 5;
                }
            }
            if (grouped)
            {
                for (int i = 0; i < igroups; i++)
                {
                    double w = MeasureStringInCanvasCoordinates(cdat1[groupid].Title.Substring(0, Math.Min(20, cdat1[groupid].Title.Length)) + "=" + cdat1[groupid].Groups[i].Label, legendFont).Width + MINIMUM_X_WHITESPACE;
                    if (w > xtra + xAxisCanvas)
                        xtra = w - xAxisCanvas - 5;
                }
            }

            // Draw the axes
            AxisScales axisScales = DrawAxesOrEnlargeCanvas(title, new AxisDefinition(xAxisTitle, AxisMode.Scale, definition.ScaleParameters.X.ScaleType), new AxisDefinition(yAxisTitle, AxisMode.Scale, definition.ScaleParameters.Y.ScaleType) { ExtraSpaceBeforeAxisStarts = xtra }, false, false);

            // draw legend
            double size2 = labelFont.Size * 2;
            if (grouped)
            {
                for (int k = 1; k <= igroups; k++)
                {
                    string vq = cdat1[groupid].Title.Substring(0, Math.Min(20, cdat1[groupid].Title.Length)) + "=" + cdat1[groupid].Groups[k - 1].Label;
                    if (!use_marker)
                    {
                        using (Pen legendPen = GetLinePen(ChartPreferences.MarkerTypes[(k - 1) % 9], true))
                        {
                            DrawLineInCanvasCoordinates(legendPen, 10, yAxisCanvas + yExtCanvas - 18 - (size2 * k), 20, yAxisCanvas + yExtCanvas - 18 - (size2 * k));
                            DrawLineInCanvasCoordinates(legendPen, 20, yAxisCanvas + yExtCanvas - 18 - (size2 * k), 20, yAxisCanvas + yExtCanvas - 28 - (size2 * k));
                        }
                    }
                    else
                    {
                        DrawMarkerInCanvasCoordinates(12, yAxisCanvas + yExtCanvas - 22 - (size2 * k), 6, ChartPreferences.MarkerTypes[(k - 1) % 9]);
                    }
                    DrawStringLegendL(vq, 24, yAxisCanvas + yExtCanvas - 10 - (size2 * k));
                }
            }
            //  Legend
            if (stratified)
            {
                for (int k = 1; k <= istrata; k++)
                {
                    string vq = "Stratum " + k;
                    if (!(use_marker))
                    {
                        using (Pen legendPen = GetLinePen(ChartPreferences.MarkerTypes[(k - 1) % 9], true))
                        {
                            DrawLineInCanvasCoordinates(legendPen, 10, yAxisCanvas + yExtCanvas - 18 - (size2 * k), 20, yAxisCanvas + yExtCanvas - 18 - (size2 * k));
                            DrawLineInCanvasCoordinates(legendPen, 20, yAxisCanvas + yExtCanvas - 18 - (size2 * k), 20, yAxisCanvas + yExtCanvas - 28 - (size2 * k));
                        }
                    }
                    else
                    {
                        DrawMarkerInCanvasCoordinates(12, yAxisCanvas + yExtCanvas - 22 - (size2 * k), 6, ChartPreferences.MarkerTypes[(k - 1) % 9]);
                    }
                    DrawStringLegendL(vq, 24, yAxisCanvas + yExtCanvas - 10 - (size2 * k));
                }
            }

            // starting positions
            double ix0 = 0;
            double iy0 = 0;
            switch (plotMode)
            {
                case CoxPlotMode.Survival:
                    ix0 = ToCanvasX(axisScales.X.MinimumScaleValue);
                    iy0 = ToCanvasY(1.0);
                    break;
                case CoxPlotMode.Hazard:
                    ix0 = ToCanvasX(axisScales.X.MinimumScaleValue);
                    iy0 = ToCanvasY(0.0);
                    break;
            }

            double ix1 = ix0;
            double iy1 = iy0;
            int igp = 0;

            MarkerType mt = ChartPreferences.MarkerTypes[igp % 9];
            Pen markerPen = GetMarkerPen(mt);
            Pen linePen = GetLinePen(mt, true);
            MarkerShape shape = mt.MarkerShape;
            bool isFilled = mt.IsMarkerFilled;

            for (int i = 1; i <= iobs; i++)
            {
                if (i > 1 && (grouped ? (z[i].Id != z[i - 1].Id) : (z[i].Stratum != z[i - 1].Stratum)))
                {
                    igp = igp + 1;
                    ix1 = ix0;
                    iy1 = iy0;
                    markerPen.Dispose();
                    linePen.Dispose();
                    mt = ChartPreferences.MarkerTypes[igp % 9];
                    markerPen = GetMarkerPen(mt);
                    linePen = GetLinePen(mt, true);
                    shape = mt.MarkerShape;
                    isFilled = mt.IsMarkerFilled;
                }

                double ix2 = ToCanvasX(z[i].Time);
                double iy2 = 0;

                // get survivor or hazard function if an event occured
                switch (plotMode)
                {
                    case CoxPlotMode.Survival:
                        double surv = grouped ? Math.Pow(z[i].S, Math.Exp(Convert.ToDouble(z[i].Id) * ARR3[1, groupid, 1])) : z[i].S;
                        iy2 = ToCanvasY(surv);
                        gn[igp + 1] = gn[igp + 1] + 1;
                        break;
                    case CoxPlotMode.Hazard:
                        double haz;
                        if (grouped)
                        {
                            haz = Math.Pow(z[i].S, Math.Exp(Convert.ToDouble(z[i].Id) * ARR3[1, groupid, 1]));
                            haz = haz > 0.0 ? -Math.Log(haz) : Constant.MISSING;
                        }
                        else
                        {
                            haz = z[i].H;
                        }
                        if (haz != Constant.MISSING)
                            iy2 = ToCanvasY(haz);
                        break;
                }


                if (z[i].Censor == 0)
                    iy2 = iy1;

                if (z[i].Censor == 0 && use_tic)
                {
                    // Draw tic if censored
                    if (ix1 != ix2 || iy1 != iy2)
                        DrawLineInCanvasCoordinates(linePen, ix2, iy2, ix2, iy2 + 7);
                }

                // Draw the markers
                if (iy2 != iy1 && use_marker)
                    DrawMarkerInCanvasCoordinates(ix2, iy2, 6, shape, isFilled, markerPen);

                // Then the lines
                if (ix1 != ix2 || iy1 != iy2)
                {
                    DrawLineInCanvasCoordinates(linePen, ix1, iy1, ix2, iy1);
                    DrawLineInCanvasCoordinates(linePen, ix2, iy1, ix2, iy2);
                }
                ix1 = ix2;
                iy1 = iy2;

            }
            markerPen.Dispose();
            linePen.Dispose();
            EndVectorPlot();
        }

        internal void PlotLinearizedEstimation(string title, int model, double a, double b, string xAxisTitle, string yAxisTitle)
        {
            const int MARKER_SIZE = 6;

            StartVectorPlot();
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

            AxisScales axisScales = DrawAxesOrEnlargeCanvas(title,
                new AxisDefinition(xAxisTitle, AxisMode.Scale, definition.ScaleParameters.X.ScaleType),
                new AxisDefinition(yAxisTitle, AxisMode.Scale, definition.ScaleParameters.Y.ScaleType) { ExtraSpaceBeforeAxisStarts = xtra },
                boxAxes, false);

            // plot points
            DoubleSeries xs = definition.XSeries[0].AsDoubleSeries;
            DoubleSeries ys = definition.YSeries[0].AsDoubleSeries;
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
            DrawMarkerSeriesInCanvasCoordinates(xys, MARKER_SIZE, ys.MarkerDetails.MarkerShape, ys.MarkerDetails.IsMarkerFilled, ys.MarkerDetails.MarkerPen, ys.MarkerDetails.LinePen, false, true);

            double xstep = (axisScales.X.MaximumScaleValue - axisScales.X.MinimumScaleValue) / axisScales.X.Tics().Count / 2;

            // Plot regression
            using (Pen p = new Pen(grBlack, 2))
            {
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
            }
            EndVectorPlot();
        }

        internal void PlotPolynomialRegression(string title, int mode, double[,] xtxi, double[] bd, double rss, int nx, int P, double gamma, string xAxisTitle, string yAxisTitle)
        {
            const int MARKER_SIZE = 6;

            StartVectorPlot();
            AssignMarkersToSeries();
            //  What extra space do we need before the X axis?
            double xtra = 0;
            if (definition.XSeries.Count > 1)
            {
                foreach (Series ser in definition.XSeries)
                {
                    double w = MeasureStringInCanvasCoordinates(ser.Title, legendFont).Width + MINIMUM_X_WHITESPACE;
                    if (w > xtra + xAxisCanvas)
                        xtra = w - xAxisCanvas;
                }
            }

            AxisScales axisScales = DrawAxesOrEnlargeCanvas(title,
                new AxisDefinition(xAxisTitle, AxisMode.Scale, definition.ScaleParameters.X.ScaleType),
                new AxisDefinition(yAxisTitle, AxisMode.Scale, definition.ScaleParameters.Y.ScaleType) { ExtraSpaceBeforeAxisStarts = xtra },
                boxAxes, false);

            // plot points
            DoubleSeries xs = definition.XSeries[0].AsDoubleSeries;
            DoubleSeries ys = definition.YSeries[0].AsDoubleSeries;
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
            DrawMarkerSeriesInCanvasCoordinates(xys, MARKER_SIZE, ys.MarkerDetails.MarkerShape, ys.MarkerDetails.IsMarkerFilled, ys.MarkerDetails.MarkerPen, ys.MarkerDetails.LinePen, false, true);

            double P0;
            double cit;
            MathDbl.civ(nx - P, out cit, gamma, out P0);
            double rdf = Convert.ToDouble((nx - 1) - (P - 1));
            double rms = rss / rdf;
            double[] px = new double[P + 1];
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
                    for (int jj = 1; jj <= P; jj++)
                        calcy += bd[jj] * Math.Pow(calcx, Convert.ToDouble(jj - 1));
                    double x1 = calcx;
                    double y1 = calcy;
                    if (calcx > axisScales.X.MinimumScaleValue
                        && y1 >= axisScales.Y.MinimumScaleValue && y1 <= axisScales.Y.MaximumScaleValue
                        && oldx != Constant.MISSING
                        && oldy >= axisScales.Y.MinimumScaleValue && oldy <= axisScales.Y.MaximumScaleValue)
                        DrawLineInChartCoordinates(grGreen, x1, y1, oldx, oldy);
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
                    for (int jj = 1; jj <= P; jj++)
                        calcy += bd[jj] * Math.Pow(calcx, jj - 1);
                    px[1] = 1.0;
                    for (int k = 2; k <= P; k++)
                        px[k] = Math.Pow(calcx, k - 1);
                    double xcx = 0;
                    for (int i = 1; i <= P; i++)
                    {
                        double s = 0;
                        for (int k = 1; k <= P; k++)
                            s += xtxi[i, k] * px[k];
                        xcx += s * px[i];
                    }
                    double sey;
                    if (mode == 1)
                        sey = Math.Sqrt(Math.Abs(rms * xcx));
                    else
                        sey = Math.Sqrt(Math.Abs(rms * (1.0 + xcx)));
                    double cl = cit * sey;
                    double x1 = calcx;
                    double y1 = calcy - cl;
                    if (calcx > axisScales.X.MinimumScaleValue
                        && y1 >= axisScales.Y.MinimumScaleValue && y1 <= axisScales.Y.MaximumScaleValue
                        && oldx != Constant.MISSING
                        && oldy >= axisScales.Y.MinimumScaleValue && oldy <= axisScales.Y.MaximumScaleValue)
                        DrawLineInChartCoordinates(grBlack, x1, y1, oldx, oldy);
                    oldx = x1;
                    oldy = y1;
                }
                // Draw +Lines
                oldx = Constant.MISSING;
                oldy = Constant.MISSING;
                for (double calcx = axisScales.X.MinimumScaleValue; calcx <= axisScales.X.MaximumScaleValue; calcx += xstep)
                {
                    double calcy = 0;
                    for (int jj = 1; jj <= P; jj++)
                        calcy += bd[jj] * Math.Pow(calcx, jj - 1);
                    px[1] = 1.0;
                    for (int k = 2; k <= P; k++)
                        px[k] = Math.Pow(calcx, k - 1);
                    double xcx = 0;
                    for (int i = 1; i <= P; i++)
                    {
                        double s = 0;
                        for (int k = 1; k <= P; k++)
                            s += xtxi[i, k] * px[k];
                        xcx += s * px[i];
                    }
                    double sey;
                    if (mode == 1)
                        sey = Math.Sqrt(Math.Abs(rms * xcx));
                    else
                        sey = Math.Sqrt(Math.Abs(rms * (1.0 + xcx)));
                    double cl = cit * sey;
                    double x1 = calcx;
                    double y1 = calcy + cl;
                    if (calcx > axisScales.X.MinimumScaleValue
                        && y1 >= axisScales.Y.MinimumScaleValue && y1 <= axisScales.Y.MaximumScaleValue
                        && oldx != Constant.MISSING
                        && oldy >= axisScales.Y.MinimumScaleValue && oldy <= axisScales.Y.MaximumScaleValue)
                        DrawLineInChartCoordinates(grBlack, x1, y1, oldx, oldy);
                    oldx = x1;
                    oldy = y1;
                }
            }
            EndVectorPlot();
        }

        ///  <remarks>Jul 09: updated to put log models on a log x axis scale</remarks>
        internal void PlotLogit(string title, int model, double t, double sw, double s1, double a, double b, string xAxisTitle, string yAxisTitle)
        {
            const int MARKER_SIZE = 6;

            DoubleSeries xs = definition.XSeries[0].AsDoubleSeries;
            DoubleSeries ys = definition.YSeries[0].AsDoubleSeries;
            double[] xdat = xs.Data;
            double[] ydat = ys.Data;

            double cl = 0;
            int nx = 0;
            foreach (double v in xdat)
            {
                if (v != Constant.MISSING)
                {
                    // TODO: Why is it correct to take log10(x) here?  It matches the old code to move to a log10 axis, but...?
                    cl += v <= 0 ? v : Math.Log10(v);
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
            AxisScales axisScales = DrawAxesOrEnlargeCanvas(title,
                new AxisDefinition(xAxisTitle, AxisMode.Scale, definition.ScaleParameters.X.ScaleType),
                new AxisDefinition(yAxisTitle, AxisMode.Scale, definition.ScaleParameters.Y.ScaleType),
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
            DrawMarkerSeriesInCanvasCoordinates(xys, MARKER_SIZE, ys.MarkerDetails.MarkerShape, ys.MarkerDetails.IsMarkerFilled, ys.MarkerDetails.MarkerPen, ys.MarkerDetails.LinePen, false, true);

            // Aim for 100 steps across the chart - anything coarser gives terrible resolution for tight curves (e.g. log10 of the sample data).
            // TODO: How to handle this on a log X scale?
            double xstep = (axisScales.X.MaximumScaleValue - axisScales.X.MinimumScaleValue) / 100.0;

            // This routine has changed from the original
            // It is more efficient in drawing - but bigger in code

            // Draw central curve
            using (Pen greenPen = new Pen(grGreen, 1))
            {
                double oldx = Constant.MISSING;
                double oldy = 0;
                // Transformations on this get messy: axisXMin and axisXMax are in transformed non-canvas units (e.g. log10); originalX is therefore the original value.
                // However a and b are based on the transformed non-canvas unit!
                // So we need to keep both around.
                for (double calcx = axisScales.X.MinimumScaleValue; calcx <= axisScales.X.MaximumScaleValue; calcx += xstep)
                {
                    double originalX = InverseTransformX(calcx);
                    double calcY = a + b * calcx;
                    if (model == 1)
                        calcY = PDF.alnorm(calcY);
                    else
                        calcY = Math.Exp(calcY * 2.0) / (1.0 + Math.Exp(calcY * 2.0));
                    double x1 = ToCanvasX(originalX);
                    double y1 = ToCanvasY(calcY);
                    if (y1 >= yAxisCanvas && y1 <= yAxisCanvas + yExtCanvas && oldx != Constant.MISSING && oldy >= yAxisCanvas && oldy <= yAxisCanvas + yExtCanvas)
                        DrawLineInCanvasCoordinates(greenPen, x1, y1, oldx, oldy);
                    oldx = x1;
                    oldy = y1;
                }
            }

            // Draw upper curve - TODO: Repeat fix
            using (Pen magentaPen = new Pen(grMagenta, 1))
            {
                double oldx = 0;
                double oldy = 0;
                for (double calcx = axisScales.X.MinimumScaleValue; calcx <= axisScales.X.MaximumScaleValue; calcx += xstep)
                {
                    double originalX = InverseTransformX(calcx);
                    cl = t * Math.Sqrt(1.0 / sw + Math.Pow((calcx - xm), 2.0) / s1);
                    double calcy = a + b * calcx;
                    double cly = calcy + cl;
                    if (model == 1)
                        cly = PDF.alnorm(cly);
                    else
                        cly = Math.Exp(cly * 2.0) / (1.0 + Math.Exp(cly * 2.0));
                    double x1 = ToCanvasX(originalX);
                    double y1 = ToCanvasY(cly);
                    if (y1 >= yAxisCanvas && y1 < yAxisCanvas + yExtCanvas && oldx != Constant.MISSING && oldy >= yAxisCanvas && oldy < yAxisCanvas + yExtCanvas)
                        DrawLineInCanvasCoordinates(magentaPen, x1, y1, oldx, oldy);
                    oldx = x1;
                    oldy = y1;
                }
                // Draw lower curve - TODO: repeat fix
                oldx = 0;
                oldy = 0;
                for (double calcx = axisScales.X.MinimumScaleValue; calcx <= axisScales.X.MaximumScaleValue; calcx += xstep)
                {
                    double originalX = InverseTransformX(calcx);
                    cl = t * Math.Sqrt(1.0 / sw + Math.Pow((calcx - xm), 2.0) / s1);
                    double calcy = a + b * calcx;
                    double cly = calcy - cl;
                    if (model == 1)
                        cly = PDF.alnorm(cly);
                    else
                        cly = Math.Exp(cly * 2.0) / (1.0 + Math.Exp(cly * 2.0));
                    double x1 = ToCanvasX(originalX);
                    double y1 = ToCanvasY(cly);
                    if (y1 >= yAxisCanvas && y1 < yAxisCanvas + yExtCanvas && oldx != Constant.MISSING && oldy >= yAxisCanvas && oldy < yAxisCanvas + yExtCanvas)
                        DrawLineInCanvasCoordinates(magentaPen, x1, y1, oldx, oldy);
                    oldx = x1;
                    oldy = y1;
                }
            }
            EndVectorPlot();
        }


        public void PlotXY(double[] x, double[] y, string xtxt, string ytxt, string title, bool zPlot, DataMinMax minMaxY, bool useCalculatedScalesEvenWithDefinition)
        {
            StartVectorPlot();
            PlotXYInternal(x, y, xtxt, ytxt, title, zPlot, minMaxY, 6, MarkerShape.Circle, false, Pens.Black, useCalculatedScalesEvenWithDefinition);
            EndVectorPlot();
        }

        public void PlotXY0To1(double[] x, double[] y, string xtxt, string ytxt, string title, bool zPlot, DataMinMax minMaxY, bool useCalculatedScalesEvenWithDefinition)
        {
            StartVectorPlot();
            PlotXYInternal(x, y, xtxt, ytxt, title, zPlot, minMaxY, 6, MarkerShape.Circle, false, Pens.Black, useCalculatedScalesEvenWithDefinition, 0, 1, 0, 1);
            EndVectorPlot();
        }

        private ScaleParameters GetDefaultScaleParameters()
        {
            return new ScaleParameters
            {
                X = { AllowedScaleTypes = new[] { ScaleType.Linear } },
                Y = { AllowedScaleTypes = new[] { ScaleType.Linear } },
            };
        }

        internal void PlotXYR(double[,] x, double[,,] y, int ng, int[] gn, int[,] nr, double[] b, double[] a, string xtxt, string ytxt, string title, string[] bnam, double dataMinX, double dataMaxX, double dataMinY, double dataMaxY)
        {
            const int LEGEND_MARKER_X = 12;
            const int LEGEND_MARKER_Y_OFFSET = 15;
            const int LEGEND_TEXT_X = 24;

            DataMinX = dataMinX;
            DataMaxX = dataMaxX;
            DataMinY = dataMinY;
            DataMaxY = dataMaxY;

            StartVectorPlot();
            // Draw the scale
            double xtra = 0;
            for (int g = 1; g <= ng; g++)
            {
                double w = MeasureStringInCanvasCoordinates(bnam[g], legendFont).Width + 55;
                if (w > xtra + xAxisCanvas)
                    xtra = w - xAxisCanvas;
            }
            DrawAxesOrEnlargeCanvas(title, new AxisDefinition(xtxt, AxisMode.Scale, ScaleType.Linear), new AxisDefinition(ytxt, AxisMode.Scale, ScaleType.Linear) { ExtraSpaceBeforeAxisStarts = xtra }, false, false);

            double size2 = labelFont.Size * 2;

            // Draw the legends
            if (ng > 1)
            {
                for (int g = 1; g <= ng; g++)
                {
                    if (bnam[g].Length > 0)
                    {
                        int mkr = ChartOptions.SeriesNumberToMarkerNumber(g - 1);
                        DrawMarkerInCanvasCoordinates(LEGEND_MARKER_X, yAxisCanvas + yExtCanvas - LEGEND_MARKER_Y_OFFSET - (size2 * g), LEGEND_MARKER_SIZE, ChartPreferences.MarkerTypes[mkr]); //  TODO: Broken?
                        DrawStringLegendL(bnam[g], LEGEND_TEXT_X, yAxisCanvas + yExtCanvas - 10 - (size2 * g));
                    }
                }
            }

            // Plot the points
            for (int g = 1; g <= ng; g++)
            {
                int mkr = ChartOptions.SeriesNumberToMarkerNumber(g - 1);
                MarkerType t = ChartPreferences.MarkerTypes[mkr];
                using (Pen p = GetMarkerPen(t))
                {
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
                    double calcy = (a[g] + b[g] * minx);
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
                    calcy = (a[g] + b[g] * maxx);
                    if (calcy < miny)
                    {
                        calcy = miny;
                        if (b[g] != 0.0)
                            x2 = ToCanvasX(((calcy - a[g]) / b[g]));
                    }
                    else if (calcy > maxy)
                    {
                        calcy = maxy;
                        if (b[g] != 0.0)
                            x2 = ToCanvasX(((calcy - a[g]) / b[g]));
                    }
                    DrawLineInCanvasCoordinates(p, x1, y1, x2, ToCanvasY(calcy));
                }
            }
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
                    Layout.Range dataRangeX = GetMinMaxArray(x, definition.ScaleParameters.X.ScaleType);
                    DataMinX = dataRangeX.Min;
                    DataMaxX = dataRangeX.Max;
                    if (minMaxY == DataMinMax.XCalc_YCalc)
                    {
                        Layout.Range dataRangeY = GetMinMaxArray(y, definition.ScaleParameters.Y.ScaleType);
                        DataMinY = dataRangeY.Min;
                        DataMaxY = dataRangeY.Max;
                    }
                }
            }
            AxisScales axisScales = DrawAxesOrEnlargeCanvas(title, new AxisDefinition(xtxt, AxisMode.Scale, ScaleType.Linear), new AxisDefinition(ytxt, AxisMode.Scale, ScaleType.Linear), isLAabbe, false);

            if (zPlot)
                DrawQCanvas(offy);

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
                DrawLineInChartCoordinates(grBlack, axisScales.X.MinimumScaleValue, axisScales.Y.MinimumScaleValue, axisScales.X.MaximumScaleValue, axisScales.Y.MaximumScaleValue);
                // pooled event rate
                using (Pen blackFXPen = GetLinePen(ChartPreferences.MarkerTypes[10], false))
                {
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
                    DrawLineInCanvasCoordinates(blackFXPen, ToCanvasX(axisScales.X.MinimumScaleValue), ToCanvasY(axisScales.Y.MinimumScaleValue), x1, y1);
                }
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

        internal void Plot_Bias_MA(ITemplateHost host, double[] x, double[] yy, double[] yw, int rows, string xtxt, double[] cl, double[] cu, double cco, double cit, double rmh, Transformation xform, bool diagonal)
        {
            string ytxt = null;
            double[] y;
            string title;
            bool reverse; bool use_ci = false;
            int plotMethod;
            get_ma_ordinate(host, out y, yy, yw, cl, cu, ref cco, rows, out title, out ytxt, xtxt, out plotMethod, xform, out reverse, ref use_ci);

            double[] xx = new double[rows + 1];
            xx[0] = Constant.MISSING;
            switch (xform)
            {
                case Transformation.Log:
                    for (int r = 1; r <= rows; r++)
                    {
                        if (x[r] > 0.0 && x[r] != Constant.MISSING)
                            xx[r] = Math.Log(x[r]);
                        else
                            xx[r] = Constant.MISSING;
                    }
                    break;
                case Transformation.Z:
                    for (int r = 1; r <= rows; r++)
                    {
                        if (x[r] != Constant.MISSING)
                            xx[r] = MathDbl.rtoz(x[r]);
                        else
                            xx[r] = Constant.MISSING;
                    }
                    break;
                case Transformation.None:
                    for (int r = 1; r <= rows; r++)
                        xx[r] = x[r];
                    break;
            }


            // get the Min and Max for the data
            Layout.Range dataRangeX = GetMinMaxArray(xx, ScaleType.Linear);
            DataMinX = dataRangeX.Min;
            DataMaxX = dataRangeX.Max;
            Layout.Range dataRangeY = GetMinMaxArray(y, ScaleType.Linear);
            DataMinY = dataRangeY.Min;
            DataMaxY = dataRangeY.Max;

            // get complete funnel by extending x axis so the funnel does not cut the y axis
            double pool;
            switch (xform)
            {
                case Transformation.Log:
                    pool = Math.Log(rmh);
                    break;
                case Transformation.Z:
                    pool = MathDbl.rtoz(rmh);
                    break;
                case Transformation.None:
                    pool = rmh;
                    break;
                default:
                    throw new ArgumentOutOfRangeException("xform", xform, "Unexpected transform: only Log, None, Z known");
            }

            ILinearAxisScale axisScale = (ILinearAxisScale)AxisScalerFactory.AxisScalerFor(ScaleType.Linear).Q_Axis(DataMinY, 0, DataMaxY, true);
            DataMinY = axisScale.MinimumDataValue;
            DataMaxY = axisScale.MaximumDataValue;
            double ymn = axisScale.MinimumScaleValue;
            double ymx = axisScale.MaximumScaleValue;
            double mini = Math.Min(axisScale.Interval, DataMinY);
            if (use_ci)
            {
                double se = ma_plot_se(plotMethod == 2 ? ymn : ymx, mini, plotMethod);
                if (DataMaxX < pool + se * cit)
                    DataMaxX = pool + se * cit;
                if (DataMinX > pool - se * cit)
                    DataMinX = pool - se * cit;
            }

            switch (xform)
            {
                case Transformation.Log:
                    xtxt = "Log(" + xtxt + ")";
                    break;
                case Transformation.Z:
                    xtxt = "Fisher Z(" + xtxt + ")";
                    break;
                case Transformation.None:
                    //  Do nothing
                    break;
            }

            StartVectorPlot();
            // Peto plots are boxed
            AxisScales axisScales = DrawAxesOrEnlargeCanvas(title, new AxisDefinition(xtxt, AxisMode.Scale, ScaleType.Linear), new AxisDefinition(ytxt, reverse ? AxisMode.ReverseScale : AxisMode.Scale, ScaleType.Linear), !reverse && diagonal, false);

            // plot the points
            for (int r = 1; r <= rows; r++)
                if (xx[r] != Constant.MISSING && y[r] != Constant.MISSING)
                    DrawMarkerInChartCoordinates(xx[r], y[r], 6, ChartPreferences.MarkerTypes[0]);

            using (Pen blackPen = new Pen(grBlack, 1))
            {
                if (!diagonal)
                {
                    // mark pooled value
                    DrawLineInChartCoordinates(grBlack, pool, axisScales.Y.MinimumScaleValue, pool, axisScales.Y.MaximumScaleValue);
                }

                if ((plotMethod == 1 || plotMethod == 2 || plotMethod == 7) && !diagonal && use_ci)
                {
                    // plot confidence interval
                    int incs = plotMethod == 1 ? 1 : 300;
                    double yinc = (axisScales.Y.MaximumScaleValue - axisScales.Y.MinimumScaleValue) / incs;
                    if (plotMethod == 2)
                    {
                        double ynow = axisScales.Y.MaximumScaleValue;
                        double xnow = pool + ma_plot_se(ynow, mini, plotMethod) * cit;
                        double y1 = ynow;
                        double x1 = xnow;
                        for (int r = 1; r <= incs; r++)
                        {
                            ynow -= yinc;
                            xnow = pool + ma_plot_se(ynow, mini, plotMethod) * cit;
                            if (xnow >= axisScales.X.MinimumScaleValue && xnow <= axisScales.X.MaximumScaleValue)
                            {
                                DrawLineInChartCoordinates(grBlack, x1, y1, xnow, ynow);
                                y1 = ynow;
                                x1 = xnow;
                            }
                        }
                        ynow = axisScales.Y.MaximumScaleValue;
                        xnow = pool - ma_plot_se(ynow, mini, plotMethod) * cit;
                        y1 = ynow;
                        x1 = xnow;
                        for (int r = 1; r <= incs; r++)
                        {
                            ynow -= yinc;
                            xnow = pool - ma_plot_se(ynow, mini, plotMethod) * cit;
                            if (xnow >= axisScales.X.MinimumScaleValue && xnow <= axisScales.X.MaximumScaleValue)
                            {
                                DrawLineInChartCoordinates(grBlack, x1, y1, xnow, ynow);
                                y1 = ynow;
                                x1 = xnow;
                            }
                        }
                    }
                    else
                    {
                        double ynow = axisScales.Y.MinimumScaleValue;
                        double xnow = pool;
                        double y1 = ynow;
                        double x1 = xnow;
                        for (int r = 1; r <= incs; r++)
                        {
                            ynow += yinc;
                            xnow = pool + ma_plot_se(ynow, mini, plotMethod) * cit;
                            if (xnow >= axisScales.X.MinimumScaleValue && xnow <= axisScales.X.MaximumScaleValue)
                            {
                                DrawLineInChartCoordinates(grBlack, x1, y1, xnow, ynow);
                                y1 = ynow;
                                x1 = xnow;
                            }
                        }
                        ynow = axisScales.Y.MinimumScaleValue;
                        xnow = pool;
                        y1 = ynow;
                        x1 = xnow;
                        for (int r = 1; r <= incs; r++)
                        {
                            ynow += yinc;
                            xnow = pool - ma_plot_se(ynow, mini, plotMethod) * cit;
                            if (xnow >= axisScales.X.MinimumScaleValue && xnow <= axisScales.X.MaximumScaleValue)
                            {
                                DrawLineInChartCoordinates(grBlack, x1, y1, xnow, ynow);
                                y1 = ynow;
                                x1 = xnow;
                            }
                        }
                    }
                }
            }

            if (diagonal)
                DrawLineInChartCoordinates(grBlack, axisScales.X.MinimumScaleValue, axisScales.Y.MinimumScaleValue, axisScales.X.MaximumScaleValue, axisScales.Y.MaximumScaleValue);
            EndVectorPlot();
        }

        internal void PlotTies(double[] x, double[] y, int nx, double lla, double ula, double GAMMA, string v0Title, string v1Title, double mean)
        {
            DataMinX = x[1];
            DataMaxX = x[1];
            DataMinY = y[1];
            DataMaxY = y[1];
            for (int j = 2; j <= nx; j++)
            {
                if (x[j] > DataMaxX)
                    DataMaxX = x[j];
                if (y[j] > DataMaxY)
                    DataMaxY = y[j];
                if (x[j] < DataMinX)
                    DataMinX = x[j];
                if (y[j] < DataMinY)
                    DataMinY = y[j];
            }
            if (lla < DataMinY)
                DataMinY = lla;
            if (ula > DataMaxY)
                DataMaxY = ula;
            // Draw the scale
            string xtxt = "Mean ((" + v0Title + " + " + v1Title + ") / 2)";
            string ytxt = "Difference (" + v0Title + " - " + v1Title + ")";
            StartVectorPlot();
            DrawAxesOrEnlargeCanvas(string.Empty, new AxisDefinition(xtxt, AxisMode.Scale, ScaleType.Linear), new AxisDefinition(ytxt, AxisMode.Scale, ScaleType.Linear), false, false);

            // Draw the titles
            int size2 = labelFont.Height * 2;
            DrawStringLegend("mean difference \u00B1 " + Formatting.XRound(GAMMA * 100.0, 2) + "% limits of agreement", xAxisCanvas + xExtCanvas, yAxisCanvas + yExtCanvas + size2, StringAlignment.Far);

            // Draw the limits
            double x1 = xAxisCanvas + xExtCanvas;
            double y1 = ToCanvasY(ula);
            using (Pen redPen = new Pen(grRed, 1))
            {
                using (Pen blackPen = new Pen(grBlack, 1))
                {
                    DrawLineInCanvasCoordinates(redPen, xAxisCanvas, y1, x1, y1);
                    y1 = ToCanvasY(lla);
                    DrawLineInCanvasCoordinates(redPen, xAxisCanvas, y1, x1, y1);
                    y1 = ToCanvasY(mean);
                    DrawLineInCanvasCoordinates(blackPen, xAxisCanvas, y1, x1, y1);
                    // Work through the rows
                    for (int r = 1; r <= nx; r++)
                    {
                        x1 = ToCanvasX(x[r]);
                        y1 = ToCanvasY(y[r]);
                        DrawMarkerInCanvasCoordinates(x1, y1, 6, ChartPreferences.MarkerTypes[0]);
                    }
                }
            }
            EndVectorPlot();
        }

        private void get_ma_ordinate(ITemplateHost host, out double[] y, double[] yy, double[] yw, double[] cl, double[] cu, ref double cco, int rows, out string title, out string ytx, string xtxt, out int plot_method, Transformation xform, out bool reverse, ref bool use_ci)
        {
            y = new double[rows + 1];
            y[0] = Constant.MISSING;
            if (xtxt == "Peto weights")
            {
                ytx = "Observed-Expected";
                title = "Peto O-E vs. V plot";
                plot_method = 2;
                for (int r = 1; r <= rows; r++)
                {
                    if (yw[r] == 0.0 || yw[r] == Constant.MISSING)
                        y[r] = Constant.MISSING;
                    else
                        y[r] = 1.0 / yw[r];
                }
                reverse = false;
                return;
            }

            bool usept = xtxt.Contains("Incidence");
            use_ci = host.MetaPlotCI;

            if (cco <= 0.0)
                cco = 0.95;
            double cit = PDF.gauinv(1.0 - ((1.0 - cco) / 2.0));

            plot_method = host.MetaPlotMethod;

            switch (plot_method)
            {
                case 1:
                    reverse = true;
                    ytx = "Standard error";
                    switch (xform)
                    {
                        case Transformation.Log:
                            for (int r = 1; r <= rows; r++)
                            {
                                if (cu[r] == Constant.MISSING || cl[r] == Constant.MISSING || cu[r] <= 0.0 || cl[r] <= 0.0)
                                    y[r] = Constant.MISSING;
                                else
                                    y[r] = ((Math.Log(cu[r]) - Math.Log(cl[r])) / 2.0) / cit;
                            }
                            break;
                        case Transformation.Z:
                            for (int r = 1; r <= rows; r++)
                            {
                                if (cu[r] == Constant.MISSING || cl[r] == Constant.MISSING)
                                    y[r] = Constant.MISSING;
                                else
                                    y[r] = ((MathDbl.rtoz(cu[r]) - MathDbl.rtoz(cl[r])) / 2.0) / cit;
                            }
                            break;
                        case Transformation.None:
                            for (int r = 1; r <= rows; r++)
                            {
                                if (cu[r] == Constant.MISSING || cl[r] == Constant.MISSING)
                                    y[r] = Constant.MISSING;
                                else
                                    y[r] = ((cu[r] - cl[r]) / 2.0) / cit;
                            }
                            break;
                    }

                    break;
                case 2:
                    reverse = false;
                    ytx = "Precision";
                    switch (xform)
                    {
                        case Transformation.Log:
                            for (int r = 1; r <= rows; r++)
                            {
                                if (cu[r] == Constant.MISSING || cl[r] == Constant.MISSING || cu[r] <= 0.0 || cl[r] <= 0.0)
                                    y[r] = Constant.MISSING;
                                else
                                    y[r] = ((Math.Log(cu[r]) - Math.Log(cl[r])) / 2.0) / cit;
                                if (y[r] != 0.0 & y[r] != Constant.MISSING)
                                    y[r] = 1.0 / y[r];
                                else
                                    y[r] = Constant.MISSING;
                            }
                            break;
                        case Transformation.Z:
                            for (int r = 1; r <= rows; r++)
                            {
                                if (cu[r] == Constant.MISSING | cl[r] == Constant.MISSING)
                                    y[r] = Constant.MISSING;
                                else
                                    y[r] = ((MathDbl.rtoz(cu[r]) - MathDbl.rtoz(cl[r])) / 2.0) / cit;
                                if (y[r] != 0.0 & y[r] != Constant.MISSING)
                                    y[r] = 1.0 / y[r];
                                else
                                    y[r] = Constant.MISSING;
                            }
                            break;
                        case Transformation.None:
                            for (int r = 1; r <= rows; r++)
                            {
                                if (cu[r] == Constant.MISSING | cl[r] == Constant.MISSING)
                                    y[r] = Constant.MISSING;
                                else
                                    y[r] = ((cu[r] - cl[r]) / 2.0) / cit;
                                if (y[r] != 0.0 & y[r] != Constant.MISSING)
                                    y[r] = 1.0 / y[r];
                                else
                                    y[r] = Constant.MISSING;
                            }
                            break;
                    }

                    break;
                case 3:
                    reverse = true;
                    ytx = "1/Sample size";
                    for (int r = 1; r <= rows; r++)
                    {
                        if (yy[r] == 0.0)
                            y[r] = Constant.MISSING;
                        else
                            y[r] = 1.0 / yy[r];
                    }
                    break;
                case 4:
                    reverse = false;
                    ytx = "Sample size";
                    for (int r = 1; r <= rows; r++)
                        y[r] = yy[r];
                    break;
                case 5:
                    reverse = true;
                    ytx = "1/Log(sample size)";
                    for (int r = 1; r <= rows; r++)
                    {
                        if (yy[r] <= 0.0 | yy[r] == 1.0)
                            y[r] = Constant.MISSING;
                        else
                            y[r] = 1.0 / Math.Log10(yy[r]);
                    }
                    break;
                case 6:
                    reverse = false;
                    ytx = "Log(sample size)";
                    for (int r = 1; r <= rows; r++)
                    {
                        if (yy[r] <= 0.0)
                            y[r] = Constant.MISSING;
                        else
                            y[r] = Math.Log10(yy[r]);
                    }
                    break;
                case 7:
                    reverse = true;
                    ytx = "1/MH weight";
                    for (int r = 1; r <= rows; r++)
                    {
                        if (yw[r] == 0.0)
                            y[r] = Constant.MISSING;
                        else
                            y[r] = 1.0 / yw[r];
                    }
                    break;
                default:
                    throw new NotImplementedException("Unknown meta plot method");
            }

            if (usept)
                ytx = ytx.Replace("sample size", "person-time");
            title = "Bias assessment plot";
        }

        private static double ma_plot_se(double y, double z, int plot_method)
        {
            switch (plot_method)
            {
                case 1:
                    return y;
                case 2:
                    return y == 0.0 ? 1.0 / z : 1.0 / y;
                case 7:
                    return y < 0.0 ? 0.0 : Math.Sqrt(y);
                default:
                    return 0;
            }
        }

        internal void x_plGraphInternal(int[,] dead, int groups, int[] cnx, string[] glab, bool tic, bool marker, double[,] x, double[,] y, int plotMode, string xAxisTitle, string yAxisTitle, string title)
        {
            StartVectorPlot();
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
            AxisScales axisScales = DrawAxesOrEnlargeCanvas(title, new AxisDefinition(xAxisTitle, AxisMode.Scale, ScaleType.Linear), new AxisDefinition(yAxisTitle, AxisMode.Scale, ScaleType.Linear), false, false);

            // Plot the legends
            int size2 = labelFont.Height * 2;
            if (groups > 1)
            {
                for (int k = 1; k <= groups; k++)
                {
                    string vq = glab[k];
                    if (marker)
                    {
                        DrawMarkerInCanvasCoordinates(12, yAxisCanvas + yExtCanvas - 22 - (size2 * k), 6, ChartPreferences.MarkerTypes[(k - 1) % 9]);
                    }
                    else
                    {
                        using (Pen p = GetMarkerPen(ChartPreferences.MarkerTypes[(k - 1) % 9]))
                        {
                            DrawLineInCanvasCoordinates(p, 10, yAxisCanvas + yExtCanvas - 18 - (size2 * k), 20, yAxisCanvas + yExtCanvas - 18 - (size2 * k));
                            DrawLineInCanvasCoordinates(p, 20, yAxisCanvas + yExtCanvas - 18 - (size2 * k), 20, yAxisCanvas + yExtCanvas - 28 - (size2 * k));
                        }
                    }
                    DrawStringLegendL(vq, 24, yAxisCanvas + yExtCanvas - 10 - (size2 * k));
                }
            }
            for (int k = 1; k <= groups; k++)
            {
                MarkerType mt = ChartPreferences.MarkerTypes[(k - 1) % 9];
                using (Pen p = GetMarkerPen(mt))
                {
                    double x1; double y1;
                    switch (plotMode)
                    {
                        case 1:
                            x1 = (axisScales.X.MinimumScaleValue);
                            y1 = (1.0);
                            break;
                        case 2:
                            x1 = (axisScales.X.MinimumScaleValue);
                            y1 = (0);
                            break;
                        case 3:
                            x1 = (x[1, k]);
                            y1 = (y[1, k]);
                            break;
                        case 4:
                            x1 = (x[1, k]);
                            y1 = (y[1, k]);
                            break;
                        case 5:
                            x1 = (x[1, k]);
                            y1 = (y[1, k]);
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
            }
            EndVectorPlot();
        }

        internal void Plot_MH(int k, double[,] o, double[] odw, string[] title, double rmh, double ll, double ul, double cco, double[] odr, double[] odrl, double[] odru, bool[] lerr, bool[] uerr, string cap, int pbias, string qid)
        {
            if (k > 10)
            {
                double scaleYAxis = 1 + (k - 10) / 20.0;
                if (scaleYAxis > 5)
                    scaleYAxis = 5;
                imageHeight = (int)Math.Ceiling(scaleYAxis * DEFAULT_METAFILE_HEIGHT);
            }

            double[] gw = new double[k + 1];
            double ormax = double.NegativeInfinity;
            double ormin = double.PositiveInfinity;
            double orumax = double.NegativeInfinity;
            double orlmin = double.PositiveInfinity;
            double max_gw = double.NegativeInfinity;
            double absmin = double.PositiveInfinity;
            for (int i = 1; i <= k; i++)
            {
                if (odw[i] != Constant.MISSING)
                {
                    if (odw[i] > max_gw)
                        max_gw = odw[i];
                    gw[i] = odw[i];
                }
                if (odr[i] != Constant.MISSING && include_table(o, i) && !double.IsInfinity(odr[i]))
                {
                    if (odr[i] > ormax)
                        ormax = odr[i];
                    if (odru[i] > orumax && odru[i] != Constant.MISSING && !double.IsInfinity(odru[i]))
                        orumax = odru[i];
                    if (odru[i] < orlmin && odru[i] > 0 && odru[i] != Constant.MISSING && !double.IsInfinity(odru[i]))
                        orlmin = odru[i];
                    if (odr[i] > 0)
                    {
                        if (odr[i] < ormin)
                            ormin = odr[i];
                        if (odrl[i] < orlmin && odrl[i] > 0 && odrl[i] != Constant.MISSING && !double.IsInfinity(odrl[i]))
                            orlmin = odrl[i];
                    }
                    if (Math.Abs(odr[i]) < absmin && odr[i] != 0.0)
                        absmin = Math.Abs(odr[i]);
                    if (Math.Abs(odrl[i]) < absmin && odrl[i] != 0.0 && odrl[i] != Constant.MISSING && !double.IsInfinity(odrl[i]))
                        absmin = Math.Abs(odrl[i]);
                    if (Math.Abs(odru[i]) < absmin && odru[i] != 0.0 && odru[i] != Constant.MISSING && !double.IsInfinity(odru[i]))
                        absmin = Math.Abs(odru[i]);
                }
            }

            DataMinX = orlmin;
            DataMinGreaterThanZeroX = orlmin;
            DataMaxX = ormax;

            if (DataMaxX < rmh)
                DataMaxX = rmh;
            if (DataMaxX < ul && ul != Constant.MISSING && !double.IsInfinity(ul))
                DataMaxX = ul;
            if (DataMaxX < orumax && orumax != Constant.MISSING && !double.IsInfinity(orumax))
                DataMaxX = orumax;

            StartVectorPlot();
            double rgap = 0;
            double xtra = 0;
            // allow room for right hand labels of effect and CI
            double w = MeasureStringInCanvasCoordinates(combo_ti(cap), legendFont).Width + 30;
            if (w > xtra + xAxisCanvas)
                xtra = w - xAxisCanvas - AXIS_BIG_TICK;
            for (int i = 1; i <= k; i++)
            {
                if (odr[i] != Constant.MISSING && !double.IsInfinity(odr[i]))
                {
                    w = MeasureStringInCanvasCoordinates(title[i], legendFont).Width + 30;
                    if (w > xtra + xAxisCanvas)
                        xtra = w - xAxisCanvas - AXIS_BIG_TICK;
                    w = MeasureStringInCanvasCoordinates(Formatting.RoundMeta(odr[i], absmin) + " (" + Formatting.RoundMeta(odrl[i], absmin) + ", " + Formatting.RoundMeta(odru[i], absmin) + ")", legendFont).Width;
                    if (w > rgap)
                        rgap = w;
                }
            }
            AxisScales axisScales = DrawAxesOrEnlargeCanvas(cap, new AxisDefinition(qid + " (" + Formatting.XRound(cco * 100, 1) + "% confidence interval" + ")", AxisMode.Scale, definition.ScaleParameters.X.ScaleType) { ExtraSpaceAfterAxisEnds = rgap }, new AxisDefinition(null, AxisMode.None, ScaleType.Category) { ExtraSpaceBeforeAxisStarts = xtra }, false, false);
            axisScales.Y = new CategoryAxisScale(k + pbias);
            divy = k + pbias;
            offy = yAxisCanvas;

            MarkerType studyMarkerType = new MarkerType
            {
                MarkerColor = Color.Gray,
                LineColor = Color.Black,
                IsMarkerFilled = true,
                MarkerShape = MarkerShape.Square,
                LineDashStyle = System.Drawing.Drawing2D.DashStyle.Solid,
                Width = 1
            };
            MarkerType pooledMarkerType = new MarkerType
            {
                MarkerColor = Color.Gray,
                LineColor = Color.Black,
                IsMarkerFilled = true,
                MarkerShape = MarkerShape.Diamond,
                LineDashStyle = System.Drawing.Drawing2D.DashStyle.Solid,
                Width = 1
            };

            using (Pen ciPen = GetLinePen(studyMarkerType, true),
                dotPen = GetMarkerPen(ChartPreferences.MarkerTypes[10]),
                pooledCiPen = GetLinePen(pooledMarkerType, true),
                tenPenTrue = GetMarkerPen(ChartPreferences.MarkerTypes[10]),
                pooledEffectPen = GetLinePen(ChartPreferences.MarkerTypes[10], false))
            {
                int r = 0;
                double txh = MeasureStringInCanvasCoordinates(title[1], labelFont).Height;
                double realamin = axisScales.X.MinimumScaleValue;
                double realamax = axisScales.X.MaximumScaleValue;
                double yc = 0;
                double yt = 0;
                for (int i = k; i >= 1; i--)
                {
                    r++;
                    double yctr = (r + pbias - 0.5) / divy * yExtCanvas;
                    double ytop = (r + pbias) / divy * yExtCanvas;
                    double y2 = (ytop - yctr) / 1.5;
                    double y3 = (ytop - yctr) / 4;
                    yc = offy + yctr;
                    yt = offy + yctr + y2;
                    double yb = offy + yctr - y2;
                    if (odr[i] != Constant.MISSING && !double.IsInfinity(odr[i]) && include_table(o, i))
                    {
                        double xm;
                        if (odr[i] <= 0 || odr[i] < realamin)
                            xm = xAxisCanvas;
                        else
                            xm = ToCanvasX(odr[i]);
                        double xl;
                        if (odrl[i] <= 0 || odrl[i] < realamin || odrl[i] == Constant.MISSING || double.IsInfinity(odrl[i]))
                            xl = xAxisCanvas;
                        else
                            xl = ToCanvasX(odrl[i]);
                        double xr;
                        if (double.IsInfinity(odru[i]) || odru[i] == Constant.MISSING || double.IsInfinity(odru[i]))
                            xr = ToCanvasX(realamax);
                        else
                            xr = odru[i] <= 0 ? offx : ToCanvasX(odru[i]);

                        // Weight blob.  Draw this first so that the line appears in front of it in the case of short lines (#994).
                        // #688: Make blob size proportional to sqrt(1/variance) rather than 1/variance
                        double blobSize = (5 + Math.Abs(yt - yb) * (Math.Sqrt(gw[i] / max_gw))) * 0.7;
                        DrawMarkerInCanvasCoordinates(xm, yc, blobSize / 2, studyMarkerType);

                        // CI line
                        DrawLineInCanvasCoordinates(ciPen, xl, yc, xr, yc);
                        // Arrow ends if not plottable
                        if (odrl[i] <= 0 || lerr[i] || odrl[i] < orlmin || odrl[i] == Constant.MISSING || double.IsInfinity(odrl[i]))
                        {
                            DrawLineInCanvasCoordinates(ciPen, xl + y3, yc + y3, xl, yc);
                            DrawLineInCanvasCoordinates(ciPen, xl, yc, xl + y3, yc - y3);
                        }
                        if (uerr[i] || double.IsInfinity(odru[i]) || odru[i] == Constant.MISSING)
                        {
                            DrawLineInCanvasCoordinates(ciPen, xr - y3, yc + y3, xr, yc);
                            DrawLineInCanvasCoordinates(ciPen, xr, yc, xr - y3, yc - y3);
                        }
                        // Centre mark.  Draw this last so that it appears in front of the line.  Always black.
                        DrawMarkerInCanvasCoordinates(xm, yc, 2, MarkerShape.Circle, true, dotPen);

                        DrawStringLabel(title[i], xAxisCanvas - 15, yc + txh / 2, StringAlignment.Far);
                        DrawStringLabel(Formatting.RoundMeta(odr[i], absmin) + " (" + Formatting.RoundMeta(odrl[i], absmin) + ", " + Formatting.RoundMeta(odru[i], absmin) + ")", xAxisCanvas + xExtCanvas + 10, yc + txh / 2, StringAlignment.Near);
                    }
                    else
                    {
                        DrawStringLabel(title[i], xAxisCanvas - 15, yc + txh / 2, StringAlignment.Far);
                        DrawStringLabel("* (excluded)", xAxisCanvas + xExtCanvas + 10, yc + txh / 2, StringAlignment.Near);
                    }
                }

                // zero effect marker
                switch (definition.ScaleParameters.X.ScaleType)
                {
                    case ScaleType.Linear:
                        if (axisScales.X.MinimumScaleValue <= 0 && axisScales.X.MaximumScaleValue >= 0)
                            DrawLineInChartCoordinates(grBlack, 0, axisScales.Y.MinimumScaleValue, 0, axisScales.Y.MaximumScaleValue);
                        break;
                    case ScaleType.Log10:
                    case ScaleType.LogNatural:
                        if (axisScales.X.MinimumScaleValue <= 1 && axisScales.X.MaximumScaleValue >= 1)
                            DrawLineInChartCoordinates(grBlack, 1, axisScales.Y.MinimumScaleValue, 1, axisScales.Y.MaximumScaleValue);
                        break;
                }

                if (pbias == 1)
                {
                    // pooled marker
                    double save_yc = yc;
                    double yctr = 0.5 / divy * yExtCanvas;
                    double ytop = 1 / divy * yExtCanvas;
                    double y2 = (ytop - yctr) / 1.5;
                    yc = offy + yctr;
                    yt = offy + yctr + y2;
                    // yb = offy + yctr - Y2; 
                    DrawMarkerInCanvasCoordinates(ToCanvasX(rmh), yc, y2, pooledMarkerType);
                    DrawLineInCanvasCoordinates(pooledCiPen, ToCanvasX(ul), yc, ToCanvasX(ll), yc);
                    // pooled effect marker
                    DrawLineInCanvasCoordinates(pooledEffectPen, ToCanvasX(rmh), save_yc, ToCanvasX(rmh), yt);
                    // pool label
                    DrawStringLabel(combo_ti(cap), xAxisCanvas - 15, yc + txh / 2, StringAlignment.Far);
                    DrawStringLabel(Formatting.RoundMeta(rmh, absmin) + " (" + Formatting.RoundMeta(ll, absmin) + ", " + Formatting.RoundMeta(ul, absmin) + ")", xAxisCanvas + xExtCanvas + 10, yc + txh / 2, StringAlignment.Near);
                    // xaxis label
                }
            }

            EndVectorPlot();
        }

        internal void Plot_MHRiskDifference(int k, double[] odw, string[] title, double rmh, double ll, double ul, double cco, double[] odr, double[] odrl, double[] odru, bool[] lerr, bool[] uerr, string cap, int pbias, string qid)
        {
            if (k > 10)
            {
                double scaleYAxis = 1 + (k - 10) / 20.0;
                if (scaleYAxis > 5)
                    scaleYAxis = 5;
                imageHeight = (int)Math.Ceiling(scaleYAxis * DEFAULT_METAFILE_HEIGHT);
            }

            StartVectorPlot();

            double[] gw = new double[k + 1];
            double orMax = double.NegativeInfinity;
            double orMin = double.PositiveInfinity;
            double oruMax = double.NegativeInfinity;
            double orlMin = double.PositiveInfinity;
            double max_gw = double.NegativeInfinity;
            double absMin = double.PositiveInfinity;
            for (int i = 1; i <= k; i++)
            {
                if (odw[i] != Constant.MISSING)
                {
                    if (odw[i] > max_gw)
                        max_gw = odw[i];
                    gw[i] = odw[i];
                }
                if (odr[i] != Constant.MISSING)
                {
                    if (odr[i] > orMax)
                        orMax = odr[i];
                    if (odr[i] < orMin)
                        orMin = odr[i];
                    if (odrl[i] < orlMin && odrl[i] != Constant.MISSING)
                        orlMin = odrl[i];
                    if (odru[i] > oruMax && odru[i] != Constant.MISSING)
                        oruMax = odru[i];
                    if (Math.Abs(odr[i]) < absMin && odr[i] != 0.0)
                        absMin = Math.Abs(odr[i]);
                    if (Math.Abs(odrl[i]) < absMin && odrl[i] != 0.0 && odrl[i] != Constant.MISSING)
                        absMin = Math.Abs(odrl[i]);
                    if (Math.Abs(odru[i]) < absMin && odru[i] != 0.0 && odru[i] != Constant.MISSING)
                        absMin = Math.Abs(odru[i]);
                }
            }

            DataMaxX = orMax;
            DataMinX = orMin;
            if (DataMaxX < rmh)
                DataMaxX = rmh;
            if (DataMaxX < ul && ul != Constant.MISSING)
                DataMaxX = ul;
            if (DataMaxX < oruMax && oruMax != Constant.MISSING)
                DataMaxX = oruMax;
            if (DataMinX > rmh)
                DataMinX = rmh;
            if (DataMinX > ll && ll != Constant.MISSING)
                DataMinX = ll;
            if (DataMinX > orlMin && orlMin != Constant.MISSING)
                DataMinX = orlMin;

            ILinearAxisScale axisScale = (ILinearAxisScale)AxisScalerFactory.AxisScalerFor(ScaleType.Linear).Q_Axis(DataMinX, 0, DataMaxX, false);
            DataMinX = axisScale.MinimumScaleValue;
            DataMaxX = axisScale.MaximumScaleValue;

            double rgap = 0;
            double xtra = 0;
            // allow room for right hand labels of effect and CI
            float w = MeasureStringInCanvasCoordinates(combo_ti(cap), labelFont).Width + 30;
            if (w > xtra + xAxisCanvas)
                xtra = w - xAxisCanvas - 5;
            for (int i = 1; i <= k; i++)
            {
                if (odr[i] != Constant.MISSING)
                {
                    w = MeasureStringInCanvasCoordinates(title[i], labelFont).Width + 30;
                    if (w > xtra + xAxisCanvas)
                        xtra = w - xAxisCanvas - 5;
                    w = MeasureStringInCanvasCoordinates(Formatting.RoundMeta(odr[i], absMin) + " (" + Formatting.RoundMeta(odrl[i], absMin) + ", " + Formatting.RoundMeta(odru[i], absMin) + ")", labelFont).Width;
                    if (w > rgap)
                        rgap = w;
                }
            }
            AxisScales axisScales = DrawAxesOrEnlargeCanvas(cap, new AxisDefinition(qid + " (" + Formatting.XRound(cco * 100, 1) + "% confidence interval" + ")", AxisMode.Scale, ScaleType.Linear) { ExtraSpaceAfterAxisEnds = rgap }, new AxisDefinition(null, AxisMode.None, ScaleType.Linear) { ExtraSpaceBeforeAxisStarts = xtra }, false, false);
            axisScales.Y = new CategoryAxisScale(k + pbias);
            divy = k + pbias;
            offy = yAxisCanvas;

            MarkerType studyMarkerType = new MarkerType
            {
                MarkerColor = Color.Gray,
                LineColor = Color.Black,
                IsMarkerFilled = true,
                MarkerShape = MarkerShape.Square,
                LineDashStyle = System.Drawing.Drawing2D.DashStyle.Solid,
                Width = 1
            };
            MarkerType pooledMarkerType = new MarkerType
            {
                MarkerColor = Color.Gray,
                LineColor = Color.Black,
                IsMarkerFilled = true,
                MarkerShape = MarkerShape.Diamond,
                LineDashStyle = System.Drawing.Drawing2D.DashStyle.Solid,
                Width = 1
            };

            using (Pen tenPenTrue = GetLinePen(ChartPreferences.MarkerTypes[10], true),
                dotPen = GetMarkerPen(ChartPreferences.MarkerTypes[10]),
                tenPenFalse = GetLinePen(ChartPreferences.MarkerTypes[10], false))
            {
                int r = 0;
                double yc = 0;
                double yt = 0;
                double txh = MeasureStringInCanvasCoordinates(title[1], labelFont).Height;
                for (int i = k; i >= 1; i--)
                {
                    r++;
                    double yctr = (r + pbias - 0.5) / divy * yExtCanvas;
                    double ytop = (r + pbias) / divy * yExtCanvas;
                    double y2 = (ytop - yctr) / 1.5;
                    yc = offy + yctr;
                    yt = offy + yctr + y2;
                    double yb = offy + yctr - y2;
                    if (odr[i] != Constant.MISSING)
                    {
                        double xl = odrl[i] == Constant.MISSING ? xAxisCanvas : ToCanvasX(odrl[i]);
                        double xr;
                        if (odru[i] == double.PositiveInfinity || odru[i] == Constant.MISSING)
                            xr = xAxisCanvas + xExtCanvas;
                        else
                            xr = ToCanvasX(odru[i]);
                        // Weight blob.  Draw this first so that the line appears in front of it in the case of short lines (#994).
                        // #688: Make blob size proportional to sqrt(1/variance) rather than 1/variance
                        double blobSize = (5 + Math.Abs(yt - yb) * (Math.Sqrt(gw[i] / max_gw))) * 0.7;
                        DrawMarkerInCanvasCoordinates(ToCanvasX(odr[i]), yc, blobSize / 2, studyMarkerType);

                        // CI line
                        DrawLineInCanvasCoordinates(tenPenTrue, xl, yc, xr, yc);

                        // Arrow ends if not plottable
                        if (lerr[i])
                        {
                            DrawLineInCanvasCoordinates(tenPenTrue, xl + y2, yc + y2, xl, yc);
                            DrawLineInCanvasCoordinates(tenPenTrue, xl, yc, xl + y2, yc - y2);
                        }
                        if (uerr[i])
                        {
                            DrawLineInCanvasCoordinates(tenPenTrue, xr - y2, yc + y2, xr, yc);
                            DrawLineInCanvasCoordinates(tenPenTrue, xr, yc, xr - y2, yb - y2);
                        }
                        // Centre mark.  Draw this last so that it appears in front of the line.  Always black.
                        DrawMarkerInCanvasCoordinates(ToCanvasX(odr[i]), yc, 2, MarkerShape.Circle, true, dotPen);

                        DrawStringLabel(title[i], xAxisCanvas - 15, yc + txh / 2, StringAlignment.Far);
                        DrawStringLabel(Formatting.RoundMeta(odr[i], absMin) + " (" + Formatting.RoundMeta(odrl[i], absMin) + ", " + Formatting.RoundMeta(odru[i], absMin) + ")", xAxisCanvas + xExtCanvas + 10, yc + txh / 2, StringAlignment.Near);
                    }
                    else
                    {
                        DrawStringLabel(title[i], xAxisCanvas - 15, yc + txh / 2, StringAlignment.Far);
                        DrawStringLabel("* (excluded)", xAxisCanvas + xExtCanvas + 10, yc + txh / 2, StringAlignment.Near);
                    }
                }

                if (axisScales.X.MinimumScaleValue <= 0 && axisScales.X.MaximumScaleValue >= 0)
                {
                    //  no effect marker
                    DrawLineInCanvasCoordinates(tenPenTrue, offx, yt, offx, yAxisCanvas - 12);
                }

                if (pbias == 1)
                {
                    double save_yc = yc;
                    double yctr = 0.5 / divy * yExtCanvas;
                    double ytop = 1 / divy * yExtCanvas;
                    double y2 = (ytop - yctr) / 1.5;
                    yc = offy + yctr;
                    yt = offy + yctr + y2;
                    DrawDiamondInCanvasCoordinates(tenPenTrue, ToCanvasX(rmh), yc, y2 * 2, false);
                    DrawLineInCanvasCoordinates(tenPenTrue, ToCanvasX(ul), yc, ToCanvasX(ll), yc);
                    // pooled effect marker
                    DrawLineInCanvasCoordinates(tenPenFalse, ToCanvasX(rmh), save_yc, ToCanvasX(rmh), yt);
                    // pool label
                    DrawStringLabel(combo_ti(cap), xAxisCanvas - 15, yc + txh / 2, StringAlignment.Far);
                    DrawStringLabel(Formatting.RoundMeta(rmh, absMin) + " (" + Formatting.RoundMeta(ll, absMin) + ", " + Formatting.RoundMeta(ul, absMin) + ")", xAxisCanvas + xExtCanvas + 10, yc + txh / 2, StringAlignment.Near);
                    // x axis text
                }
            }

            EndVectorPlot();
        }

        internal void PlotEffect(ITemplateHost host, int k, double[] cn, double[] En, string[] title, double rmh, double ll, double ul, double cco, double[] odr, double[] odrl, double[] odru, string cap, int pbias, string qid)
        {
            if (k > 10)
            {
                double scaleYAxis = 1 + (k - 10) / 20.0;
                if (scaleYAxis > 5)
                    scaleYAxis = 5;
                imageHeight = (int)Math.Ceiling(scaleYAxis * DEFAULT_METAFILE_HEIGHT);
            }

            StartVectorPlot();

            double[] gn = new double[k + 1];
            int kok = 0;
            double ormax = double.NegativeInfinity;
            double ormin = double.PositiveInfinity;
            double orumax = double.NegativeInfinity;
            double orlmin = double.PositiveInfinity;
            double max_gn = double.NegativeInfinity;
            for (int i = 1; i <= k; i++)
            {
                gn[i] = cn[i] + En[i];
                if (gn[i] > max_gn)
                    max_gn = gn[i];
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

            IAxisScale xAxisScale = (ILinearAxisScale)AxisScalerFactory.AxisScalerFor(ScaleType.Linear).Q_Axis(DataMinX, 0, DataMaxX, false);
            DataMinX = xAxisScale.MinimumScaleValue;
            DataMaxX = xAxisScale.MaximumScaleValue;

            double xtra = 0;
            for (int i = 1; i <= k; i++)
            {
                if (odr[i] != Constant.MISSING)
                {
                    double w = MeasureStringInCanvasCoordinates(title[i], titleFont).Width + 30;
                    if (w > xtra + xAxisCanvas)
                        xtra = w - xAxisCanvas - 5;
                }
            }
            AxisScales axisScales = DrawAxesOrEnlargeCanvas(cap, new AxisDefinition(null, AxisMode.Scale, ScaleType.Linear), new AxisDefinition(null, AxisMode.None, ScaleType.NotSet) { ExtraSpaceBeforeAxisStarts = xtra }, false, false);
            axisScales.Y = new CategoryAxisScale(kok + pbias);
            divy = kok + pbias;
            offy = yAxisCanvas;

            using (Pen linePen = GetLinePen(ChartPreferences.MarkerTypes[10], true))
            {
                double yc = 0;
                double yt = 0;
                int r = 0;
                double txh = MeasureStringInCanvasCoordinates(title[1], labelFont).Height;
                for (int i = k; i >= 1; i--)
                {
                    if (odr[i] != Constant.MISSING)
                    {
                        r++;
                        double yctr = (r + pbias - 0.5) / divy * yExtCanvas;
                        double ytop = (r + pbias) / divy * yExtCanvas;
                        double xm = ToCanvasX(odr[i]);
                        double xl = ToCanvasX(odrl[i]);
                        double xr = ToCanvasX(odru[i]);
                        double y2 = (ytop - yctr) / 1.5;
                        yc = offy + yctr;
                        yt = offy + yctr + y2;
                        double yb = offy + yctr - y2;
                        // ytop = yctr + ( ytop - yctr ) * 0.1 + ( ytop - yctr ) * 0.9 * ( gn[ i ] / max_gn ); 
                        // ytop = yctr + ( ytop - yctr ) * 0.8; 
                        // CI line
                        DrawLineInCanvasCoordinates(linePen, xl, yc, xr, yc);
                        // Weight blob
                        DrawSquareInCanvasCoordinates(linePen, xm, yc, (5 + Math.Abs(yt - yb) * (gn[i] / max_gn)) * 0.7, true);
                        DrawStringLabel(title[i], xAxisCanvas - 15, yc + txh / 2, StringAlignment.Far);
                    }
                }

                if (DataMinX <= 0)
                {
                    double xm = offx;
                    DrawLineInCanvasCoordinates(linePen, xm, yt, xm, yAxisCanvas - 12);
                }

                if (pbias == 1)
                {
                    double save_yc = yc;
                    double yctr = 0.5 / divy * yExtCanvas;
                    double ytop = 1 / divy * yExtCanvas;
                    double xm = ToCanvasX(rmh);
                    double xl = ToCanvasX(ll);
                    double xr = ToCanvasX(ul);
                    double y2 = (ytop - yctr) / 1.5;
                    yc = offy + yctr;
                    yt = offy + yctr + y2;
                    // yb = offy + yctr - y2; 
                    DrawDiamondInCanvasCoordinates(linePen, xm, yc, y2 * 2, false);
                    DrawLineInCanvasCoordinates(linePen, xr, yc, xl, yc);
                    // pooled effect marker
                    using (Pen pooledEffectPen = GetLinePen(ChartPreferences.MarkerTypes[10], false))
                    {
                        DrawLineInCanvasCoordinates(pooledEffectPen, xm, save_yc, xm, yt);
                    }
                    string lab = "pooled " + qid + " = " + host.RoundU(rmh) + "  (" + Formatting.XRound(cco * 100, 1) + "% CI = " + host.RoundU(ll) + " to " + host.RoundU(ul) + ")";
                    string xlab = cap.IndexOf("fixed", StringComparison.Ordinal) + 1 != 0 ? string.Empty : "DL ";
                    //  If hSS <> -99 Then Lab = xlab & Lab
                    lab = xlab + lab;
                    DrawStringLabel(lab, xAxisCanvas + xExtCanvas / 2, 50, StringAlignment.Center);
                }
            }
            EndVectorPlot();
        }

        internal void PlotCorrelation(int k, string[] title, double[] odr, double[] odrl, double[] odru, double[] gn, CorrelationRowType[] pg, string cap, string qid, Transformation xform, bool isDifference)
        {
            if (k > 10)
            {
                double scaleYAxis = 1 + (k - 10) / 20.0;
                if (scaleYAxis > 5)
                    scaleYAxis = 5;
                imageHeight = (int)Math.Ceiling(scaleYAxis * DEFAULT_METAFILE_HEIGHT);
            }

            StartVectorPlot();

            int kok = 0;
            double ormax = double.NegativeInfinity;
            double ormin = double.PositiveInfinity;
            double orumax = double.NegativeInfinity;
            double orlmin = double.PositiveInfinity;
            double max_gn = double.NegativeInfinity;

            switch (xform)
            {
                case Transformation.Log:
                    {
                        for (int i = 1; i <= k; i++)
                        {
                            if (pg[i] == CorrelationRowType.Study)
                            {
                                if (gn[i] != Constant.MISSING && gn[i] > max_gn)
                                    max_gn = gn[i];
                            }
                            if (odr[i] != Constant.MISSING && !double.IsInfinity(odr[i]))
                            {
                                kok += 1;
                                if (odr[i] > ormax)
                                    ormax = odr[i];
                                if (odr[i] < ormin && odr[i] > 0)
                                    ormin = odr[i];
                                if (odrl[i] > odru[i])
                                {
                                    double tmp = odrl[i];
                                    odrl[i] = odru[i];
                                    odru[i] = tmp;
                                }
                                if (odrl[i] < orlmin && odrl[i] > 0 && !double.IsInfinity(odrl[i]) && odrl[i] != Constant.MISSING)
                                    orlmin = odrl[i];
                                if (odru[i] > orumax && !double.IsInfinity(odru[i]) && odru[i] != Constant.MISSING)
                                    orumax = odru[i];
                            }
                        }
                    }
                    break;
                case Transformation.Z:
                    {
                        for (int i = 1; i <= k; i++)
                        {
                            if (pg[i] == CorrelationRowType.Study)
                            {
                                if (gn[i] != Constant.MISSING & gn[i] > max_gn)
                                    max_gn = gn[i];
                            }
                            if (odr[i] != Constant.MISSING)
                            {
                                kok += 1;
                                if (odr[i] > ormax)
                                    ormax = odr[i];
                                if (odr[i] < ormin && odr[i] > 0)
                                    ormin = odr[i];
                                if (odrl[i] > odru[i])
                                {
                                    double tmp = odrl[i];
                                    odrl[i] = odru[i];
                                    odru[i] = tmp;
                                }
                                if (odrl[i] < orlmin && odrl[i] > 0)
                                    orlmin = odrl[i];
                                if (odru[i] > orumax)
                                    orumax = odru[i];
                            }
                        }
                    }
                    break;
                case Transformation.None:
                    for (int i = 1; i <= k; i++)
                    {
                        if (pg[i] == CorrelationRowType.Study)
                        {
                            if (gn[i] != Constant.MISSING && gn[i] > max_gn)
                                max_gn = gn[i];
                        }
                        if (odr[i] != Constant.MISSING)
                        {
                            kok += 1;
                            if (odr[i] > ormax)
                                ormax = odr[i];
                            if (odr[i] < ormin)
                                ormin = odr[i];
                            if (odrl[i] > odru[i])
                            {
                                double tmp = odrl[i];
                                odrl[i] = odru[i];
                                odru[i] = tmp;
                            }
                            if (odrl[i] < orlmin)
                                orlmin = odrl[i];
                            if (odru[i] > orumax)
                                orumax = odru[i];
                        }
                    }
                    break;
            }

            double absmin = Constant.MISSING;
            for (int i = 1; i <= k; i++)
            {
                if (Math.Abs(odr[i]) < absmin & odr[i] != 0.0)
                    absmin = Math.Abs(odr[i]);
                if (Math.Abs(odrl[i]) < absmin & odrl[i] != 0.0)
                    absmin = Math.Abs(odrl[i]);
                if (Math.Abs(odru[i]) < absmin & odru[i] != 0.0)
                    absmin = Math.Abs(odru[i]);
            }

            DataMaxX = ormax;
            if (DataMaxX < orumax && orumax != Constant.MISSING)
                DataMaxX = orumax;
            DataMinX = ormin;
            if (DataMinX > orlmin && orlmin != Constant.MISSING)
                DataMinX = orlmin;
            DataMinGreaterThanZeroX = DataMinX;

            double rgap = 0;
            double xtra = 0;
            // allow room for right hand labels of effect and CI
            double w;
            for (int i = 1; i <= k; i++)
            {
                if (odr[i] != Constant.MISSING)
                {
                    w = MeasureStringInCanvasCoordinates(title[i], labelFont).Width + 30;
                    if (w > xtra + xAxisCanvas)
                        xtra = w - xAxisCanvas - 5;
                    w = MeasureStringInCanvasCoordinates(Formatting.RoundMeta(odr[i], absmin) + " (" + Formatting.RoundMeta(odrl[i], absmin) + ", " + Formatting.RoundMeta(odru[i], absmin) + ")", labelFont).Width;
                    if (w > rgap)
                        rgap = w;
                }
            }
            w = MeasureStringInCanvasCoordinates(combo_ti(cap), labelFont).Width + 30;
            if (w > xtra + xAxisCanvas)
                xtra = w - xAxisCanvas - 5;

            AxisScales axisScales;
            switch (xform)
            {
                case Transformation.Log:
                    axisScales = DrawAxesOrEnlargeCanvas(cap, new AxisDefinition(null, AxisMode.Scale, ScaleType.Log10) { ExtraSpaceAfterAxisEnds = rgap }, new AxisDefinition(null, AxisMode.None, ScaleType.Linear) { ExtraSpaceBeforeAxisStarts = xtra }, false, false);
                    break;
                default:
                    {
                        if (cap.IndexOf("Correlation (", StringComparison.Ordinal) >= 0)
                        {
                            DataMinX = DataMinX >= 0.0 ? 0.0 : -1.0;
                            DataMaxX = DataMaxX <= 0.0 ? 0.0 : 1.0;
                        }
                        axisScales = DrawAxesOrEnlargeCanvas(cap, new AxisDefinition(null, AxisMode.Scale, ScaleType.Linear) { ExtraSpaceAfterAxisEnds = rgap }, new AxisDefinition(null, AxisMode.None, ScaleType.NotSet) { ExtraSpaceBeforeAxisStarts = xtra }, false, false);
                        DataMinX = axisScales.X.MinimumScaleValue;
                        DataMaxX = axisScales.X.MaximumScaleValue;
                    }
                    break;
            }

            axisScales.Y = new CategoryAxisScale(kok);
            divy = kok;
            offy = yAxisCanvas;

            MarkerType studyMarkerType = new MarkerType
            {
                MarkerColor = Color.Gray,
                LineColor = Color.Black,
                IsMarkerFilled = true,
                MarkerShape = MarkerShape.Square,
                LineDashStyle = System.Drawing.Drawing2D.DashStyle.Solid,
                Width = 1
            };
            MarkerType pooledMarkerType = new MarkerType
            {
                MarkerColor = Color.Gray,
                LineColor = Color.Black,
                IsMarkerFilled = true,
                MarkerShape = MarkerShape.Diamond,
                LineDashStyle = System.Drawing.Drawing2D.DashStyle.Solid,
                Width = 1
            };

            using (Pen markerPen = GetMarkerPen(ChartPreferences.MarkerTypes[10]),
                linePen = GetLinePen(ChartPreferences.MarkerTypes[10], true),
                pooledEffectPen = GetLinePen(ChartPreferences.MarkerTypes[10], false),
                dotPen = GetMarkerPen(ChartPreferences.MarkerTypes[10]))
            {
                double rmh = -99;
                int r = 0;
                double txh = MeasureStringInCanvasCoordinates(title[1], labelFont).Height;
                double botlim = double.NegativeInfinity;
                double yt = 0;
                for (int i = k; i >= 1; i--)
                {
                    if (odr[i] != Constant.MISSING)
                    {
                        r++;
                        double yctr = (r - 0.5) / divy * yExtCanvas;
                        double ytop = r / divy * yExtCanvas;
                        double xm = 0;
                        if (odr[i] < botlim)
                        {
                            xm = xAxisCanvas;
                        }
                        else
                        {
                            switch (xform)
                            {
                                case Transformation.Z:
                                    xm = ToCanvasX(MathDbl.rtoz(odr[i]));
                                    break;
                                case Transformation.None:
                                case Transformation.Log:
                                    xm = ToCanvasX(odr[i]);
                                    break;
                            }

                        }
                        double xl = 0;
                        if (odrl[i] < botlim)
                        {
                            xl = xAxisCanvas;
                        }
                        else
                        {
                            switch (xform)
                            {
                                case Transformation.Log:
                                    xl = ToCanvasX(odrl[i]);
                                    break;
                                case Transformation.Z:
                                    xl = ToCanvasX(MathDbl.rtoz(odrl[i]));
                                    break;
                                case Transformation.None:
                                    xl = ToCanvasX(Math.Max(odrl[i], isDifference ? double.MinValue : -1));
                                    break;
                            }

                        }
                        double xr = 0;
                        switch (xform)
                        {
                            case Transformation.Log:
                                xr = ToCanvasX(odru[i]);
                                break;
                            case Transformation.Z:
                                xr = ToCanvasX(MathDbl.rtoz(odru[i]));
                                break;
                            case Transformation.None:
                                xr = ToCanvasX(Math.Min(odru[i], isDifference ? double.MaxValue : 1));
                                break;
                        }

                        double y2 = (ytop - yctr) / 1.5;
                        double yc = offy + yctr;
                        yt = offy + yctr + y2;
                        double yb = offy + yctr - y2;
                        if (pg[i] == CorrelationRowType.Study)
                        {
                            // Weight blob.  Draw this first so that the line appears in front of it in the case of short lines (#994).
                            // #688: Make blob size proportional to sqrt(1/variance) rather than 1/variance
                            double blobSize = (5 + Math.Abs(yt - yb) * (Math.Sqrt(gn[i] / max_gn))) * 0.7;
                            DrawMarkerInCanvasCoordinates(xm, yc, blobSize / 2, studyMarkerType);
                            // CI line
                            DrawLineInCanvasCoordinates(linePen, xl, yc, xr, yc);
                            // Arrow ends if not plottable
                            if ((odrl[i] <= 0 && xform == Transformation.Log) || odrl[i] == Constant.MISSING)
                            {
                                DrawLineInCanvasCoordinates(linePen, xl + y2, yc + y2, xl, yc);
                                DrawLineInCanvasCoordinates(linePen, xl, yc, xl + y2, yc - y2);
                            }
                            if (odru[i] == Constant.MISSING)
                            {
                                DrawLineInCanvasCoordinates(linePen, xr - y2, yc + y2, xr, yc);
                                DrawLineInCanvasCoordinates(linePen, xr, yc, xr - y2, yb - y2);
                            }
                            // Centre mark.  Draw this last so that it appears in front of the line.  Always black.
                            DrawMarkerInCanvasCoordinates(xm, yc, 2, MarkerShape.Circle, true, dotPen);

                        }
                        else
                        {
                            DrawMarkerInCanvasCoordinates(xm, yc, y2, pooledMarkerType);
                            DrawLineInCanvasCoordinates(linePen, xr, yc, xl, yc);
                            if (pg[i] == CorrelationRowType.Pooled)
                            {
                                rmh = odr[i];
                                // pooled effect marker
                                DrawLineInCanvasCoordinates(pooledEffectPen, xm, yt, xm, ToCanvasY(k - 0.5));
                            }
                        }
                        DrawStringLabel(title[i], xAxisCanvas - 15, yc + txh / 2, StringAlignment.Far);
                        DrawStringLabel(Formatting.RoundMeta(odr[i], absmin) + " (" + Formatting.RoundMeta(odrl[i], absmin) + ", " + Formatting.RoundMeta(odru[i], absmin) + ")", xAxisCanvas + xExtCanvas + 10, yc + txh / 2, StringAlignment.Near);
                    }
                }

                double noEffectPosition = 0;
                switch (xform)
                {
                    case Transformation.Z:
                        // Don't care
                        break;
                    case Transformation.Log:
                        noEffectPosition = 1;
                        break;
                    case Transformation.None:
                        noEffectPosition = 0;
                        break;
                }
                if (DataMinX <= noEffectPosition && xform != Transformation.Z)
                {
                    // no effect marker
                    double xm;
                    switch (xform)
                    {
                        case Transformation.Z:
                            throw new Exception("Shouldn't be plotting no effect marker with a correlation plot");
                        default:
                            xm = ToCanvasX(noEffectPosition);
                            break;
                    }
                    DrawLineInCanvasCoordinates(linePen, xm, yt, xm, yAxisCanvas);
                }

                if (rmh != -99)
                {
                    string buf = qid;
                    DrawStringLabel(buf, xAxisCanvas + xExtCanvas / 2, 50, StringAlignment.Center);
                }
            }

            EndVectorPlot();
        }

        ///  <summary>
        ///  Copied from meta due to mutual dependency issues
        ///  </summary>
        ///  <param name="o"></param>
        ///  <param name="i"></param>
        ///  <returns></returns>
        ///  <remarks></remarks>
        private static bool include_table(double[,] o, int i)
        {
            return !((o[i, 1] == 0.0 && o[i, 2] == 0.0) || (o[i, 3] == 0.0 && o[i, 4] == 0.0));
        }
    }
}
