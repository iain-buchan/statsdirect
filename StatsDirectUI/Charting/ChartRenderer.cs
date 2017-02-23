using System;
using System.Collections.Generic;
using System.Globalization;
using System.Drawing;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.Templates;
using StatsDirect.Utilities;
using StatsDirect.Builtins;

namespace StatsDirect.Charting
{
    ///  <summary>
    ///  Converts a chart definition into an ASCII or metafile rendering of that definition.
    ///  </summary>
    public class ChartRenderer : AbstractChartRenderer
    {
        public ChartRenderer(ChartDefinition definition)
            : base(definition)
        {
        }

        public override ScaleParameters GetScaleParameters()
        {
            switch (definition.ChartType)
            {
                case ChartType.AgreementPair:
                    return GetAgreementPairScaleParameters();
                case ChartType.Bar:
                case ChartType.StackedBar:
                case ChartType.StackedBar100Percent:
                case ChartType.BoxWhisker:
                case ChartType.Control:
                case ChartType.Histogram:
                    throw new NotImplementedException();
                case ChartType.ErrorBar:
                    return GetErrorBarScaleParameters();
                case ChartType.Forest:
                    throw new NotImplementedException();
                case ChartType.Gini:
                    return GetGiniScaleParameters();
                case ChartType.Ladder:
                    throw new NotImplementedException();
                case ChartType.LineXY:
                    return GetScatterScaleParameters();
                case ChartType.LinearRegression:
                    return GetLinearRegressionScaleParameters();
                case ChartType.Normal:
                    return GetNormalScaleParameters();
                case ChartType.Pyramid:
                case ChartType.ROC:
                    return GetRocScaleParameters();
                case ChartType.ScatterXY:
                    return GetScatterScaleParameters();
                case ChartType.Spread:
                case ChartType.Survival:
                    throw new NotImplementedException();
                default:
                    throw new Exception("Unknown chart type");
            }

        }

        ///  <summary>
        ///  Plot a chart.
        ///  </summary>
        ///  <returns>Any output parameters created as side-effects of the plotting</returns>
        ///  <remarks>Postcondition: Another plot can be called on the same chart object and give the same results.  This is required for previewing.</remarks>
        public override ParameterBag Plot(ITemplateHost host)
        {
            switch (definition.ChartType)
            {
                case ChartType.AgreementPair:
                    return PlotAgreementPair();
                case ChartType.Bar:
                case ChartType.StackedBar:
                case ChartType.StackedBar100Percent:
                case ChartType.BoxWhisker:
                case ChartType.Control:
                case ChartType.Histogram:
                    throw new NotImplementedException();
                case ChartType.ErrorBar:
                    return PlotErrorBar();
                case ChartType.Forest:
                    throw new NotImplementedException();
                case ChartType.Gini:
                    return PlotGini();
                case ChartType.Ladder:
                    throw new NotImplementedException();
                case ChartType.LineXY:
                case ChartType.ScatterXY:
                    return PlotScatter(host);
                case ChartType.LinearRegression:
                    return PlotLinearRegression();
                case ChartType.Normal:
                    return PlotNormal();
                case ChartType.Pyramid:
                    throw new NotImplementedException();
                case ChartType.ROC:
                    return PlotROC(host);
                case ChartType.Spread:
                case ChartType.Survival:
                    throw new NotImplementedException();
                default:
                    throw new Exception("Unknown chart type");
            }

        }



        private ScaleParameters GetScatterScaleParameters()
        {
            return new ScaleParameters
            {
                X =
                {
                    AllowedScaleTypes = new[] { ScaleType.Linear, ScaleType.Log10, ScaleType.LogNatural },
                    Max = DataMaxX,
                    MinGreaterThanZero = DataMinGreaterThanZeroX,
                    Min = DataMinX
                },
                Y =
                {
                    AllowedScaleTypes = new[] { ScaleType.Linear, ScaleType.Log10, ScaleType.LogNatural },
                    Max = DataMaxY,
                    MinGreaterThanZero = DataMinGreaterThanZeroY,
                    Min = DataMinY
                }
            };
        }

        private ParameterBag PlotScatter(ITemplateHost host)
        {
            const int LEGEND_MARKER_X = 12;
            const int LEGEND_MARKER_Y_OFFSET = 22;
            const int LEGEND_TEXT_X = 24;

            ScatterXYOptions sOptions = ((ScatterXYOptions)(definition.ChartOptions));
            bool shouldDrawMarkers = sOptions.PlotMarkers;
            bool joinMarkersWithLines = sOptions.JoinMarkersWithLines;

            if (!(IsAscii))
            {
                // Plot a metafile version
                StartVectorPlot();
                SetFontsAndThicknessesFromOptions(sOptions);
                AssignMarkersToSeries(sOptions);
                //  What extra space do we need before the X axis?
                bool showLegend = sOptions.ShowLegend;
                double xtra = 0;
                if (showLegend)
                {
                    if (definition.XSeries.Count > 1)
                    {
                        foreach (Series s in definition.XSeries)
                        {
                            double w = LegendWidthInCanvasCoordinates(s.Title) + MINIMUM_X_WHITESPACE;
                            if (w > xtra + xAxisCanvas)
                                xtra = w - xAxisCanvas;
                        }
                    }
                }

                DrawAxesOrEnlargeCanvas(definition.ChartOptions.Title, new AxisDefinition(sOptions.XAxisTitle, AxisMode.Scale, definition.ScaleParameters.X.ScaleType), new AxisDefinition(sOptions.YAxisTitle, AxisMode.Scale, definition.ScaleParameters.Y.ScaleType) { ExtraSpaceBeforeAxisStarts = xtra }, boxAxes, false);

                float size2 = labelFont.Size * 2;
                //  If there are multiple series, draw the legends
                if (showLegend && definition.XSeries.Count > 1)
                {
                    int i = 1;
                    foreach (Series s in definition.XSeries)
                    {
                        if (s.Title.Length > 0)
                        {
                            DrawMarkerInCanvasCoordinates(LEGEND_MARKER_X, yAxisCanvas + yExtCanvas - LEGEND_MARKER_Y_OFFSET - (size2 * i), LEGEND_MARKER_SIZE, definition.YSeries[i - 1].AsDoubleSeries);
                            DrawStringLegendL(s.Title, LEGEND_TEXT_X, yAxisCanvas + yExtCanvas - 10 - (size2 * i));
                        }
                        i += 1;
                    }
                }

                // plot points
                for (int c = 0; c < definition.XSeries.Count; c++)
                {
                    DoubleSeries xs = definition.XSeries[c].AsDoubleSeries;
                    DoubleSeries ys = definition.YSeries[c].AsDoubleSeries;
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
                        if (Single.IsInfinity(xys[r].X) || Single.IsInfinity(xys[r].Y)
                            || Single.IsNaN(xys[r].X) || Single.IsNaN(xys[r].Y))
                        {
                            xys[r].X = -1;
                            xys[r].Y = -1;
                        }
                    }
                    DrawMarkerSeriesInCanvasCoordinates(xys, ys.MarkerDetails.MarkerSize, ys.MarkerDetails.MarkerShape, ys.MarkerDetails.IsMarkerFilled, ys.MarkerDetails.MarkerPen, ys.MarkerDetails.LinePen, joinMarkersWithLines, shouldDrawMarkers);
                }
                MaybeDrawMarkerLines();
                EndVectorPlot();
            }
            else
            {
                int y = definition.ScaleParameters.Y.AxisScale.Tics().Count - 1;
                ASCII_InitPlot(5 + y); // 5 = Title, top axis title, bottom axis, bottom scale, bottom axis title

                // Draw the scale
                DefaultAxes();
                AxisScales axisScales = DrawAxesOrEnlargeCanvas(definition.ChartOptions.Title, new AxisDefinition(sOptions.XAxisTitle, AxisMode.Scale, definition.ScaleParameters.X.ScaleType), new AxisDefinition(sOptions.YAxisTitle, AxisMode.Scale, definition.ScaleParameters.Y.ScaleType), false, false);
                SetStandardAsciiScaling(y, axisScales);

                // Draw the title text
                int L = sOptions.YAxisTitle.Length;
                int Q = L < 14 ? 14 - L : 2;
                WriteAsciiYX(shTx.GetUpperBound(0) - 1, Q, sOptions.YAxisTitle);
                L = sOptions.XAxisTitle.Length;
                WriteAsciiYX(0, 76 - L, sOptions.XAxisTitle);

                // Work through the columns
                for (int c = 0; c <= definition.XSeries.Count - 1; c++)
                {
                    DoubleSeries xs = definition.XSeries[c].AsDoubleSeries;
                    DoubleSeries ys = definition.YSeries[c].AsDoubleSeries;
                    double[] xdat = xs.Data;
                    double[] ydat = ys.Data;
                    // Work through the rows
                    for (int r = 0; r <= xdat.Length - 1; r++)
                    {
                        if (xdat[r] != Constant.MISSING && ydat[r] != Constant.MISSING)
                        {
                            int x1 = Convert.ToInt32(offx + Convert.ToInt32(xdat[r] / divx * 60));
                            int y1 = Convert.ToInt32(offy + Convert.ToInt32(ydat[r] / divy * y));
                            ASCII_PlotPoint(x1, y1);
                        }
                    }
                }
            }
            //  End If
            return new ParameterBag();
        }

        private ScaleParameters GetLinearRegressionScaleParameters()
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

        private ParameterBag PlotLinearRegression()
        {
            const int MARKER_SIZE = 6;

            LinearRegressionOptions lrOptions = ((LinearRegressionOptions)(definition.ChartOptions));
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
                if (calcx >= axisScales.X.MinimumScaleValue && calcy >= axisScales.Y.MinimumScaleValue && calcx <= axisScales.X.MaximumScaleValue && calcy <= axisScales.Y.MaximumScaleValue)
                    DrawLineInChartCoordinates(grGreen, calcx, calcy, oldx, oldy);
                oldx = calcx;
                oldy = calcy;
            }

            MaybeDrawMarkerLines();
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

        public enum CoxPlotMode
        {
            Survival = 1,
            Hazard = 2
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
                        using (Pen legendPen = GetLinePen(MarkerTypes[(k - 1) % 9], true))
                        {
                            DrawLineInCanvasCoordinates(legendPen, 10, yAxisCanvas + yExtCanvas - 18 - (size2 * k), 20, yAxisCanvas + yExtCanvas - 18 - (size2 * k));
                            DrawLineInCanvasCoordinates(legendPen, 20, yAxisCanvas + yExtCanvas - 18 - (size2 * k), 20, yAxisCanvas + yExtCanvas - 28 - (size2 * k));
                        }
                    }
                    else
                    {
                        DrawMarkerInCanvasCoordinates(12, yAxisCanvas + yExtCanvas - 22 - (size2 * k), 6, MarkerTypes[(k - 1) % 9]);
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
                        using (Pen legendPen = GetLinePen(MarkerTypes[(k - 1) % 9], true))
                        {
                            DrawLineInCanvasCoordinates(legendPen, 10, yAxisCanvas + yExtCanvas - 18 - (size2 * k), 20, yAxisCanvas + yExtCanvas - 18 - (size2 * k));
                            DrawLineInCanvasCoordinates(legendPen, 20, yAxisCanvas + yExtCanvas - 18 - (size2 * k), 20, yAxisCanvas + yExtCanvas - 28 - (size2 * k));
                        }
                    }
                    else
                    {
                        DrawMarkerInCanvasCoordinates(12, yAxisCanvas + yExtCanvas - 22 - (size2 * k), 6, MarkerTypes[(k - 1) % 9]);
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

            MarkerType mt = MarkerTypes[igp % 9];
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
                    mt = MarkerTypes[igp % 9];
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
                if (ix1 != ix2 | iy1 != iy2)
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

            AxisScales axisScales = DrawAxesOrEnlargeCanvas(title, new AxisDefinition(xAxisTitle, AxisMode.Scale, definition.ScaleParameters.X.ScaleType), new AxisDefinition(yAxisTitle, AxisMode.Scale, definition.ScaleParameters.Y.ScaleType) { ExtraSpaceBeforeAxisStarts = xtra }, boxAxes, false);

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
                double oldx = 0;
                double oldy = 0;
                for (double calcx = axisScales.X.MinimumScaleValue; calcx <= axisScales.X.MaximumScaleValue; calcx += xstep)
                {
                    double calcy = 0;
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
                    }

                    double x1 = ToCanvasX(calcx);
                    double y1 = ToCanvasY(calcy);
                    if (calcx > axisScales.X.MinimumScaleValue && y1 > yAxisCanvas && x1 > xAxisCanvas && y1 < yAxisCanvas + yExtCanvas)
                    {
                        DrawLineInCanvasCoordinates(p, x1, y1, oldx, oldy);
                    }
                    oldx = x1;
                    oldy = y1;
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

            AxisScales axisScales = DrawAxesOrEnlargeCanvas(title, new AxisDefinition(xAxisTitle, AxisMode.Scale, definition.ScaleParameters.X.ScaleType), new AxisDefinition(yAxisTitle, AxisMode.Scale, definition.ScaleParameters.Y.ScaleType) { ExtraSpaceBeforeAxisStarts = xtra }, boxAxes, false);

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
                    double oldx = Constant.MISSING;
                    double oldy = Constant.MISSING;
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
                    double oldx = Constant.MISSING;
                    double oldy = Constant.MISSING;
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
            double[] x = new double[xdat.Length];
            for (int i = 0; i < xdat.Length; i++)
                if (xdat[i] > 0 && x[i] != Constant.MISSING)
                    x[i] = Math.Log10(xdat[i]);
                else
                    x[i] = xdat[i];

            double cl = 0;
            int nx = 0;
            foreach (double v in x)
            {
                if (v != Constant.MISSING)
                {
                    cl += v;
                    nx++;
                }
            }
            double xm = cl / Convert.ToDouble(nx);

            StartVectorPlot();
            AssignMarkersToSeries();
            if (DataMaxY - DataMinY > 0.25)
            {
                DataMaxY = 1;
                DataMinY = 0;
            }
            AxisScales axisScales = DrawAxesOrEnlargeCanvas(title, new AxisDefinition(xAxisTitle, AxisMode.Scale, definition.ScaleParameters.X.ScaleType), new AxisDefinition(yAxisTitle, AxisMode.Scale, definition.ScaleParameters.Y.ScaleType), false, false);

            // plot points
            PointF[] xys = new PointF[Math.Min(xdat.Length, ydat.Length)];
            for (int r = 0; r <= Math.Min(xdat.Length, ydat.Length) - 1; r++)
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

            // Aim for 100 steps across the chart - anything coarser gives terrible resolution for tight curves (e.g. log10 of the sample data)
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
                // TODO: Better names for Transform and InverseTransform.
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


        /// <summary>
        /// Detect and return minimum and maximum values in the array.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="scaleType">THe scale that will use the data.  For log scales, values of 0 or less are ignored.</param>
        /// <param name="min">Filled in with the global minimum value</param>
        /// <param name="max">Filled in with the global maximum value</param>
        /// <remarks>STYLE: Wouldn't this be better as a function returning some kind of data structure?</remarks>
        private void GetMinMaxArray(double[] data, ScaleType scaleType, out double min, out double max)
        {
            bool ignoreZeroOrLess = scaleType == ScaleType.LogNatural || scaleType == ScaleType.Log10;
            min = double.MaxValue;
            max = double.MinValue;
            for (int i = data.GetLowerBound(0); i <= data.GetUpperBound(0); i++)
            {
                if (data[i] != Constant.MISSING && !(ignoreZeroOrLess && data[i] <= 0))
                {
                    if (data[i] < min)
                        min = data[i];
                    if (data[i] > max)
                        max = data[i];
                }
            }
        }

        /// <summary>
        /// Detect and return minimum, minimum greater than zero and maximum values in the array.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="min">Filled in with the global minimum value</param>
        /// <param name="minGreaterThanZero">Filled in with the global minimum value that is greater than zero.</param>
        /// <param name="max">Filled in with the global maximum value</param>
        /// <remarks>STYLE: Wouldn't this be better as a function returning some kind of data structure?</remarks>
        private void GetMinMaxArray(double[] data, out double min, out double max, out double minGreaterThanZero)
        {
            min = double.MaxValue;
            minGreaterThanZero = double.MaxValue;
            max = double.MinValue;
            for (int i = data.GetLowerBound(0); i <= data.GetUpperBound(0); i++)
            {
                if (data[i] != Constant.MISSING)
                {
                    if (data[i] < min)
                        min = data[i];
                    if ((data[i] > 0) && (data[i] < minGreaterThanZero))
                        minGreaterThanZero = data[i];
                    if (data[i] > max)
                        max = data[i];
                }
            }
        }

        /// <summary>
        ///  Plots an XY chart assuming an existing vector plot is open. This allows callers to use this then add other features to the chart before it is completed.
        /// </summary>
        /// <param name="x">The X co-ordinates of the points to plot.  Zero-based or 1-based.</param>
        /// <param name="y">The Y co-ordinates of the points to plot.  Zero-based or 1-based, same length as x.</param>
        /// <param name="xtxt">The X-axis title</param>
        /// <param name="ytxt">The y-axis title</param>
        /// <param name="title">The chart title</param>
        /// <param name="zPlot">If true, draw a line at the smallest Y value</param>
        /// <param name="minMaxY"></param>
        /// <param name="markerSize"></param>
        /// <param name="Shape"></param>
        /// <param name="isFilled"></param>
        /// <param name="p"></param>
        /// <param name="useCalculatedScalesEvenWithDefinition"></param>
        /// <remarks></remarks>
        private void PlotXYInternal(double[] x, double[] y, string xtxt, string ytxt, string title, bool zPlot, DataMinMax minMaxY, double markerSize, MarkerShape shape, bool isFilled, Pen p, bool useCalculatedScalesEvenWithDefinition, double presetXMin = 0, double presetXMax = 0, double presetYMin = 0, double presetYMax = 0)
        {
            ScaleType scaleTypeX = ScaleType.Linear;
            ScaleType scaleTypeY = ScaleType.Linear;
            if (HasScaleParameters)
            {
                scaleTypeX = definition.ScaleParameters.X.ScaleType;
                scaleTypeY = definition.ScaleParameters.Y.ScaleType;
            }

            //  If required, get the Min and Max for the data
            //  This is safe because we're using this function to plot our data.
            double axisXMin;
            double axisXMinGreaterThanZero;
            double axisXMax;
            double axisYMin;
            double axisYMinGreaterThanZero;
            double axisYMax;
            switch (minMaxY)
            {
                case DataMinMax.XPreset_YPreset:
                    axisXMin = presetXMin;
                    axisXMinGreaterThanZero = presetXMin;
                    axisXMax = presetXMax;
                    axisYMin = presetYMin;
                    axisYMinGreaterThanZero = presetYMin;
                    axisYMax = presetYMax;
                    break;
                case DataMinMax.XUseScaleParameters_YUseScaleParameters:
                    //  Take data from scale parameters
                    axisYMax = definition.ScaleParameters.Y.Max;
                    axisYMinGreaterThanZero = definition.ScaleParameters.Y.MinGreaterThanZero;
                    axisYMin = definition.ScaleParameters.Y.Min;
                    axisXMax = definition.ScaleParameters.X.Max;
                    axisXMinGreaterThanZero = definition.ScaleParameters.X.MinGreaterThanZero;
                    axisXMin = definition.ScaleParameters.X.Min;
                    break;
                case DataMinMax.XY_CalcTogether:
                    // X and Y must have the same scale
                    GetMinMaxArray(x, out axisXMin, out axisXMax, out axisXMinGreaterThanZero);
                    GetMinMaxArray(y, out axisYMin, out axisYMax, out axisYMinGreaterThanZero);
                    axisYMin = axisXMin = Math.Min(axisYMin, axisXMin);
                    axisYMinGreaterThanZero = axisXMinGreaterThanZero = Math.Min(axisYMinGreaterThanZero, axisXMinGreaterThanZero);
                    axisYMax = axisXMax = Math.Max(axisYMax, axisXMax);
                    break;
                case DataMinMax.XCalc_YCalc:
                    GetMinMaxArray(x, out axisXMin, out axisXMax, out axisXMinGreaterThanZero);
                    GetMinMaxArray(y, out axisYMin, out axisYMax, out axisYMinGreaterThanZero);
                    break;
                case DataMinMax.XCalc_YPreset:
                    GetMinMaxArray(x, out axisXMin, out axisXMax, out axisXMinGreaterThanZero);
                    axisYMin = presetYMin;
                    axisYMinGreaterThanZero = presetYMin;
                    axisYMax = presetYMax;
                    break;
                default:
                    throw new ArgumentOutOfRangeException("MinMaxY", minMaxY, "Don't know how to plot using the given minMaxY");
            }

            DataMinY = axisYMin;
            DataMinGreaterThanZeroY = axisYMinGreaterThanZero;
            DataMaxY = axisYMax;
            DataMinX = axisXMin;
            DataMinGreaterThanZeroX = axisXMinGreaterThanZero;
            DataMaxX = axisXMax;

            DrawAxesOrEnlargeCanvas(title, new AxisDefinition(xtxt, AxisMode.Scale, scaleTypeX), new AxisDefinition(ytxt, AxisMode.Scale, scaleTypeY), false, useCalculatedScalesEvenWithDefinition);

            if (zPlot)
                DrawQCanvas(offy);

            // Plot the points
            int rows = x.Length;
            int xOffset = x.GetLowerBound(0);
            int yOffset = y.GetLowerBound(0);
            PointF[] xys = new PointF[rows];
            for (int r = 0; r < rows; r++)
            {
                if (x[r + xOffset] != Constant.MISSING && y[r + yOffset] != Constant.MISSING)
                {
                    xys[r].X = Convert.ToSingle(ToCanvasX(x[r + xOffset]));
                    xys[r].Y = Convert.ToSingle(ToCanvasY(y[r + yOffset]));
                }
                else
                {
                    //  Missing.  Any value less than zero is ignored by DrawMarkerSeries.
                    xys[r].X = -1;
                    xys[r].Y = -1;
                }
            }
            DrawMarkerSeriesInCanvasCoordinates(xys, markerSize, shape, isFilled, p, p, false, true);
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

        ///  <summary>
        ///  Draw a horizontal line at the specified offset from the Y origin.
        ///  </summary>
        ///  <param name="y">The offset, in device units</param>
        ///  <remarks></remarks>
        private void DrawQCanvas(double y)
        {
            using (Pen greenPen = new Pen(grGreen, 2))
            {
                DrawLineInCanvasCoordinates(greenPen, xAxisCanvas, y, xAxisCanvas + xExtCanvas, y);
            }
        }

        private ScaleParameters GetRocScaleParameters()
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

        ///  <summary>
        ///  Plot a ROC chart.
        ///  </summary>
        /// <param name="host"></param>
        public ParameterBag PlotROC(ITemplateHost host)
        {
            ROCOptions rOptions = ((ROCOptions)(definition.ChartOptions));
            double GAMMA = rOptions.GAMMA;
            if (GAMMA <= 0)
                return null;
            double cit;
            double P0;
            MathDbl.civ(0, out cit, GAMMA, out P0);

            //  Assume data passed as series - X is positive, Y is negative.

            ROCSeriesRecord[] seriesData = new ROCSeriesRecord[definition.XSeries.Count];
            for (int c = 0; c < definition.XSeries.Count; c++)
            {
                seriesData[c] = new ROCSeriesRecord();
                DoubleSeries xs = definition.XSeries[c].AsDoubleSeries;
                DoubleSeries ys = definition.YSeries[c].AsDoubleSeries;
                seriesData[c].pdata = xs.Data;
                seriesData[c].adata = ys.Data;
                seriesData[c].pmn = xs.Sum / Convert.ToDouble(xs.Points);
                seriesData[c].amn = ys.Sum / Convert.ToDouble(ys.Points);
                seriesData[c].min = Math.Min(xs.Min, ys.Min);
                seriesData[c].max = Math.Max(xs.Max, ys.Max);
                // get a sorted list of all data in order to calculate cut points
                seriesData[c].tdata = new double[xs.Points + ys.Points];
                for (int r = 0; r < xs.Points; r++)
                    seriesData[c].tdata[r] = xs.Data[r];
                for (int r = 0; r < ys.Points; r++)
                    seriesData[c].tdata[xs.Points + r] = ys.Data[r];
                Array.Sort(seriesData[c].tdata);
            }

            // Work out how many series there are and extend the plot area as required to hold the legend

            // Measurements and set axes.  These are done on a scratchpad canvas before the proper measurements are set up.
            StartVectorPlot();
            SetFontsAndThicknessesFromOptions(rOptions);
            double smallerExt = Math.Min(xExtCanvas, yExtCanvas);
            xExtCanvas = smallerExt;
            yExtCanvas = smallerExt;
            double legendFontHeight = GetFontHeightInCanvasCoordinates(legendFont);
            EndVectorPlot();

            // By now, all measurements are known.  Set up the plot areas.
            double legendTop = yAxisCanvas - LEGEND_TOP_GAP;
            double markerMidlineOffset = (legendFontHeight - LEGEND_MARKER_SIZE) / 2;
            double legendSpacing = MINIMUM_LEGEND_GAP + Math.Max(LEGEND_MARKER_SIZE, Convert.ToInt32(legendFontHeight));
            double legendBottom = legendTop - (definition.XSeries.Count * legendSpacing);
            if (legendBottom < LOWEST_ALLOWED_LEGEND)
            {
                double extraSpaceRequired = LOWEST_ALLOWED_LEGEND - legendBottom;

                //  Add in the extra space
                imageHeight += (int)Math.Ceiling(extraSpaceRequired);
                yAxisCanvas += extraSpaceRequired;
                legendTop += extraSpaceRequired;
                // legendBottom += extraSpaceRequired; 
            }

            // We've hacked at the axes; don't re-default them.
            StartVectorPlot(false);
            SetFontsAndThicknessesFromOptions(rOptions);
            AssignMarkersToSeries(rOptions);

            DataMinX = 0;
            DataMaxX = 1;
            DataMinY = 0;
            DataMaxY = 1;

            // Draw the scale
            DrawAxesOrEnlargeCanvas(rOptions.Title, new AxisDefinition("1-Specificity", AxisMode.Scale, ScaleType.Linear), new AxisDefinition("Sensitivity", AxisMode.Scale, ScaleType.Linear), true, false);

            // null effect diagonal
            using (Pen tenPenDiagonal = new Pen(MarkerTypes[10].LineColor, rOptions.AxisLineThickness))
            {
                DrawLineInCanvasCoordinates(tenPenDiagonal, xAxisCanvas, yAxisCanvas, xAxisCanvas + xExtCanvas, yAxisCanvas + yExtCanvas);
            }

            // get the offsets for the markers
            offx = xAxisCanvas;
            offy = yAxisCanvas;

            bool hideopt = !(rOptions.ShowOptimumCutOff);
            ComparisonValue showopt = rOptions.Showopts;
            ParameterBag results = new ParameterBag();
            IList<ParameterBag> allResults = new List<ParameterBag>();
            results.AddOutput("*datasets", allResults);
            for (int cs = 0; cs < definition.XSeries.Count; cs++)
            {
                ROCSeriesRecord thisData = seriesData[cs];
                DoubleSeries xs = definition.XSeries[cs].AsDoubleSeries;
                DoubleSeries ys = definition.YSeries[cs].AsDoubleSeries;
                double weight = rOptions.Weight;
                if (weight <= 0)
                    weight = 1.0;

                // Draw the legend for each series
                DrawMarkerInCanvasCoordinates(xAxisCanvas + LEGEND_MARKER_SIZE / 2.0, legendTop - (cs * legendSpacing) - markerMidlineOffset, LEGEND_MARKER_SIZE, definition.YSeries[cs].AsDoubleSeries);
                DrawStringLegendL(rOptions.SeriesTitles[cs], xAxisCanvas + 9 + LEGEND_MARKER_SIZE, legendTop - (cs * legendSpacing));

                int a;
                int b;
                int c;
                int d;
                double cutoff;
                double sens;
                if (!hideopt)
                {
                    // work out cutoff for max(weight*sens+spec)
                    double maxss = 0.0;
                    for (int r = 0; r < thisData.tdata.Length; r++)
                    {
                        cutoff = thisData.tdata[r];
                        a = CountValues(showopt, thisData.pdata, cutoff);
                        c = thisData.pdata.Length - a;
                        b = CountValues(showopt, thisData.adata, cutoff);
                        d = thisData.adata.Length - b;
                        sens = Convert.ToDouble(a) / Convert.ToDouble(a + c);
                        double spec = Convert.ToDouble(d) / Convert.ToDouble(b + d);
                        if (weight * sens + spec > maxss)
                        {
                            maxss = weight * sens + spec;
                            thisData.cutoff = cutoff;
                            thisData.a = a;
                            thisData.b = b;
                            thisData.c = c;
                            thisData.d = d;
                            thisData.sens = sens;
                            thisData.spec = spec;
                        }
                    }

                    // Cutoff calculator
                    thisData.comp = showopt;
                    if (rOptions.ShowCutOffCalculator)
                    {
                        string q = "ROC plot for " + rOptions.SeriesTitles[cs];
                        thisData = ShowCutoff(host, thisData, weight, q);
                    }
                    seriesData[cs] = thisData;
                }

                // make first mark
                cutoff = thisData.cutoff;
                a = 0;
                for (int r = 0; r < thisData.pdata.Length; r++)
                    a += CountValues(showopt, thisData.pdata, cutoff);
                c = thisData.pdata.Length - a;
                b = 0;
                for (int r = 0; r < thisData.adata.Length; r++)
                    b += CountValues(showopt, thisData.adata, cutoff);
                d = thisData.adata.Length - b;
                sens = Convert.ToDouble(a) / Convert.ToDouble(a + c);
                double mspec = 1.0 - Convert.ToDouble(d) / Convert.ToDouble(b + d);
                double x1 = offx + mspec * xExtCanvas;
                double y1 = offy + sens * yExtCanvas;

                int stps = thisData.tdata.Length;
                double[] rx = new double[stps];
                double[] ry = new double[stps];

                for (int r = 0; r < stps; r++)
                {
                    cutoff = thisData.tdata[r];
                    a = CountValuesSingleSided(showopt, thisData.pdata, cutoff);
                    c = thisData.pdata.Length - a;
                    b = CountValuesSingleSided(showopt, thisData.adata, cutoff);
                    d = thisData.adata.Length - b;
                    sens = Convert.ToDouble(a) / Convert.ToDouble(a + c);
                    ry[r] = sens;
                    mspec = 1.0 - Convert.ToDouble(d) / Convert.ToDouble(b + d);
                    rx[r] = mspec;
                }

                // Draw markers
                for (int r = 0; r < stps; r++)
                {
                    double x2 = offx + rx[r] * xExtCanvas;
                    double y2 = offy + ry[r] * yExtCanvas;
                    DrawMarkerInCanvasCoordinates(x2, y2, ys.MarkerDetails.MarkerSize, definition.YSeries[cs].AsDoubleSeries);
                }

                // Draw lines between markers
                double last_x2 = x1;
                double last_y2 = y1;
                for (int r = 0; r < stps; r++)
                {
                    double x2 = offx + rx[r] * xExtCanvas;
                    double y2 = offy + ry[r] * yExtCanvas;
                    if (r > 0 && (x2 != last_x2 || y2 != last_y2))
                        DrawLineInCanvasCoordinates(xs.MarkerDetails.LinePen, last_x2, last_y2, x2, y2);
                    last_x2 = x2;
                    last_y2 = y2;
                }

                // Mark cutoff point.  This is reversed if the chart requires reversal.
                double x = 1.0 - thisData.spec;
                double y = thisData.sens;
                if (showopt == ComparisonValue.LT || showopt == ComparisonValue.LE)
                {
                    x = 1.0 - x;
                    y = 1.0 - y;
                }
                DrawMarkerInChartCoordinates(x, y, rOptions.MarkerTypes[definition.XSeries.Count + cs].MarkerSize, rOptions.MarkerTypes[definition.XSeries.Count + cs]);

                thisData.auc = MathDbl.trapezoid_xy_roc(rx, ry, 0, stps);

                if (!hideopt)
                {
                    ParameterBag thisResults = new ParameterBag();
                    allResults.Add(thisResults);
                    // Wilcoxon estimate for AUC
                    // Hanley JA, mcNeil BJ, Radiology 143:29-36
                    //  Note that mwx and mwr are 1-based
                    double[] mwx = new double[thisData.pdata.Length + thisData.adata.Length + 1];
                    Array.Copy(thisData.pdata, 0, mwx, 1, thisData.pdata.Length);
                    Array.Copy(thisData.adata, 0, mwx, 1 + thisData.pdata.Length, thisData.adata.Length);
                    bool fault;
                    double u;
                    double zScrap;
                    double xfScrap;
                    double r1Scrap;
                    double[] mwrScrap;
                    NonParametric.MannWhitneyUTest(mwx, mwx.Length - 1, thisData.pdata.Length, thisData.adata.Length, out mwrScrap, out u, out zScrap, out xfScrap, out r1Scrap, out fault);
                    double theta;
                    double ll;
                    double ul;
                    double sew = 0;
                    if (fault)
                    {
                        theta = Constant.MISSING;
                        ll = Constant.MISSING;
                        ul = Constant.MISSING;
                    }
                    else
                    {
                        // if (thisData.pdata.Length * thisData.adata.Length - u > u)
                        //     u = thisData.pdata.Length * thisData.adata.Length - u;
                        theta = u / (thisData.pdata.Length * thisData.adata.Length);
                        // Q1 = theta / (2# - theta)
                        // Q2 = (2# * (theta ^ 2#)) / (1# + theta)
                        // sew = Sqr((theta * (1# - theta) + CDbl(rowsp(cs) - 1) * (Q1 - theta ^ 2#) + CDbl(rowsa(cs) - 1) * (Q2 - theta# ^ 2#)) / CDbl(rowsp(cs) * rowsa(cs)))
                        sew = DeLongSE(thisData.pdata, thisData.adata, theta);
                        if (sew == Constant.MISSING)
                        {
                            ll = Constant.MISSING;
                            ul = Constant.MISSING;
                        }
                        else
                        {
                            ll = theta - cit * sew;
                            ul = theta + cit * sew;
                        }
                    }
                    // end of Wilcoxon estimate
                    string warn;
                    thisResults.AddOutput("ti", rOptions.SeriesTitles[cs]);
                    thisResults.AddOutput("auc", host.RoundU(thisData.auc));
                    thisResults.AddOutput("theta", host.RoundU(theta));
                    thisResults.AddOutput("se", host.RoundU(sew));
                    thisResults.AddOutput("pc", Formatting.XRound(100.0 * (1.0 - P0), 2));
                    if (ll < 0.0)
                        ll = 0.0;
                    thisResults.AddOutput("ll", host.RoundU(ll));
                    if (ul > 1.0)
                        ul = 1.0;
                    thisResults.AddOutput("ul", host.RoundU(ul));
                    thisResults.AddOutput("cut", host.RoundU(thisData.cutoff));
                    thisResults.AddOutput("a", thisData.a.ToString(CultureInfo.InvariantCulture));
                    thisResults.AddOutput("b", thisData.b.ToString(CultureInfo.InvariantCulture));
                    thisResults.AddOutput("c", thisData.c.ToString(CultureInfo.InvariantCulture));
                    thisResults.AddOutput("d", thisData.d.ToString(CultureInfo.InvariantCulture));
                    // sensitivity CI
                    MathDbl.binci(Convert.ToDouble(thisData.a), Convert.ToDouble(thisData.a + thisData.c), out ll, out ul, GAMMA, out warn);
                    thisResults.AddOutput("senspc", Formatting.XRound(100.0 * (1.0 - P0), 2));
                    thisResults.AddOutput("sens", host.RoundU(thisData.sens));
                    thisResults.AddOutput("sensll", host.RoundU(ll));
                    thisResults.AddOutput("sensul", host.RoundU(ul) + warn);
                    // specificity CI
                    MathDbl.binci(Convert.ToDouble(thisData.d), Convert.ToDouble(thisData.d + thisData.b), out ll, out ul, GAMMA, out warn);
                    thisResults.AddOutput("specpc", Formatting.XRound(100.0 * (1.0 - P0), 2));
                    thisResults.AddOutput("spec", host.RoundU(thisData.spec));
                    thisResults.AddOutput("specll", host.RoundU(ll));
                    thisResults.AddOutput("specul", host.RoundU(ul) + warn);

                    //  BEWARE from this point on: a, b, c, d are integer, but divisions need to deal with floating-point.
                    // prevalence
                    double N = thisData.a + thisData.b + thisData.c + thisData.d;
                    double prevel = Convert.ToDouble(thisData.a + thisData.c) / N;

                    // ppv
                    double ptld;
                    double temp1; double temp2;
                    if (thisData.a + thisData.b > 0)
                    {
                        ptld = thisData.a / Convert.ToDouble(thisData.a + thisData.b);
                        temp1 = ptld * 100.0;
                        temp2 = Convert.ToInt64(ptld * 100.0) - Convert.ToInt64(prevel * 100.0);
                    }
                    else
                    {
                        ptld = Constant.MISSING;
                        temp1 = Constant.MISSING;
                        temp2 = Constant.MISSING;
                    }
                    thisResults.AddOutput("likely", host.RoundU(ptld));
                    // Clopper-Pearson CI
                    double pil; double piu;
                    MathDbl.binci(thisData.a, thisData.a + thisData.b, out pil, out piu, GAMMA, out warn);
                    thisResults.AddOutput("likely_from", host.RoundU(pil));
                    thisResults.AddOutput("likely_to", host.RoundU(piu) + warn);
                    // as percentage
                    thisResults.AddOutput("likely_pc", Formatting.XRound(temp1, 2));
                    if (pil != Constant.MISSING)
                    {
                        pil = 100.0 * pil;
                    }
                    else { pil = Constant.MISSING; }
                    thisResults.AddOutput("likely_from_pc", Formatting.XRound(pil, 2));
                    if (piu != Constant.MISSING)
                    {
                        piu = 100.0 * piu;
                    }
                    else { piu = Constant.MISSING; }
                    thisResults.AddOutput("likely_to_pc", Formatting.XRound(piu, 2));
                    // change
                    thisResults.AddOutput("likely_change", Formatting.XRound(temp2, 2));

                    // npv
                    double ptlng;
                    if (thisData.d + thisData.c > 0)
                    {
                        ptlng = thisData.d / Convert.ToDouble(thisData.d + thisData.c);
                        temp1 = ptlng * 100.0;
                        temp2 = Convert.ToInt32(ptlng * 100.0) - Convert.ToInt32((Convert.ToDouble(thisData.b + thisData.d) / N) * 100.0);
                    }
                    else
                    {
                        ptlng = Constant.MISSING;
                        temp1 = Constant.MISSING;
                        temp2 = Constant.MISSING;
                    }
                    thisResults.AddOutput("likely_negative", host.RoundU(ptlng));
                    // Clopper-Pearson CI
                    MathDbl.binci(thisData.d, thisData.d + thisData.c, out pil, out piu, GAMMA, out warn);
                    thisResults.AddOutput("likely_negative_from", host.RoundU(pil));
                    thisResults.AddOutput("likely_negative_to", host.RoundU(piu) + warn);
                    // as percentage
                    thisResults.AddOutput("likely_negative_pc", Formatting.XRound(temp1, 2));
                    if (pil != Constant.MISSING)
                    {
                        pil = 100.0 * pil;
                    }
                    else { pil = Constant.MISSING; }
                    thisResults.AddOutput("likely_negative_from_pc", Formatting.XRound(pil, 2));
                    if (piu != Constant.MISSING)
                    {
                        piu = 100.0 * piu;
                    }
                    else { piu = Constant.MISSING; }
                    thisResults.AddOutput("likely_negative_to_pc", Formatting.XRound(piu, 2));
                    // change
                    thisResults.AddOutput("likely_negative_change", Formatting.XRound(temp2, 2));

                    // p[dx] despite -ve test
                    double ptlnd;
                    if (thisData.d + thisData.c > 0)
                    {
                        ptlnd = 1.0 - (thisData.d / Convert.ToDouble(thisData.d + thisData.c));
                        temp1 = ptlnd * 100.0;
                        temp2 = Convert.ToInt32(ptlnd * 100.0) - Convert.ToInt32(prevel * 100.0);
                    }
                    else
                    {
                        ptlnd = Constant.MISSING;
                        temp1 = Constant.MISSING;
                        temp2 = Constant.MISSING;
                    }
                    thisResults.AddOutput("likely_despite", host.RoundU(ptlnd));
                    // Clopper-Pearson CI
                    MathDbl.binci(thisData.d, thisData.d + thisData.c, out pil, out piu, GAMMA, out warn);
                    thisResults.AddOutput("likely_despite_from", host.RoundU(Math.Min(1.0 - pil, 1.0 - piu)));
                    thisResults.AddOutput("likely_despite_to", host.RoundU(Math.Max(1.0 - pil, 1.0 - piu)) + warn);
                    // as percentage
                    thisResults.AddOutput("likely_despite_pc", Formatting.XRound(temp1, 2));
                    if (pil != Constant.MISSING)
                    {
                        pil = 100.0 * (1.0 - pil);
                    }
                    else { pil = Constant.MISSING; }
                    if (piu != Constant.MISSING)
                    {
                        piu = 100.0 * (1.0 - piu);
                    }
                    else { piu = Constant.MISSING; }
                    thisResults.AddOutput("likely_despite_from_pc", Formatting.XRound(Math.Min(pil, piu), 2));
                    thisResults.AddOutput("likely_despite_to_pc", Formatting.XRound(Math.Max(pil, piu), 2));
                    // change
                    thisResults.AddOutput("likely_despite_change", Formatting.XRound(temp2, 2));
                }
            }
            EndVectorPlot();
            return results;
        }

        private static int CountValues(ComparisonValue showopt, double[] data, double cutoff)
        {
            int a = 0;
            for (int j = 0; j < data.Length; j++)
            {
                switch (showopt)
                {
                    case ComparisonValue.LT:
                        if (data[j] < cutoff)
                            a++;
                        break;
                    case ComparisonValue.LE:
                        if (data[j] <= cutoff)
                            a++;
                        break;
                    case ComparisonValue.GT:
                        if (data[j] > cutoff)
                            a++;
                        break;
                    default:
                        if (data[j] >= cutoff)
                            a++;
                        break;
                }
            }
            return a;
        }

        private static int CountValuesSingleSided(ComparisonValue showopt, double[] data, double cutoff)
        {
            int a = 0;
            for (int j = 0; j < data.Length; j++)
            {
                switch (showopt)
                {
                    case ComparisonValue.LT:
                    case ComparisonValue.GT:
                        if (data[j] > cutoff)
                            a++;
                        break;
                    default:
                        if (data[j] >= cutoff)
                            a++;
                        break;
                }
            }
            return a;
        }

        private ScaleParameters GetNormalScaleParameters()
        {
            NormalOptions nOptions = ((NormalOptions)(definition.ChartOptions));
            NormalOptions.ScoreMethod method = nOptions.Method;

            DoubleSeries xs0 = definition.XSeries[0].AsDoubleSeries;
            int rows = xs0.Points;

            double[] y = new double[rows];
            for (int j = 0; j <= rows - 1; j++)
            {
                y[j] = xs0.Data[j];
            }

            double[] x = new double[rows];
            double transTemp68;
            ExFortran.Rank(y, x, 0, rows, 0, out transTemp68);

            if (method == NormalOptions.ScoreMethod.ExpectedNormalOrder)
            {
                if (rows > 4000)
                {
                    method = NormalOptions.ScoreMethod.VanDerWaerden;
                }
            }

            int nn = xs0.Points;
            for (int j = 0; j <= rows - 1; j++)
            {
                // X(j) = GAUINV(((2 * X(j)) - 1) / (2 * nn), 0)
                int ifault;
                switch (method)
                {
                    case NormalOptions.ScoreMethod.VanDerWaerden:
                        //  van der Waerden, Conover P 396
                        x[j] = PDF.gauinv(x[j] / (Convert.ToDouble(nn) + 1.0), out ifault);
                        if (ifault != 0)
                        {
                            x[j] = Constant.MISSING;
                        }
                        break;
                    case NormalOptions.ScoreMethod.Blom:
                        //  Blom - Altman p143
                        x[j] = PDF.gauinv(x[j] / (Convert.ToDouble(nn) + 1.0), out ifault);
                        if (ifault != 0)
                        {
                            x[j] = Constant.MISSING;
                        }
                        break;
                    default:
                        //  expected normal order
                        x[j] = PDF.expnos(Convert.ToInt32(x[j]), nn);
                        break;
                }
            }

            double xMin;
            double xMax;
            GetMinMaxArray(x, ScaleType.Linear, out xMin, out xMax);
            double yMin;
            double yMax;
            GetMinMaxArray(y, ScaleType.Linear, out yMin, out yMax);
            return new ScaleParameters
            {
                X = { AllowedScaleTypes = new[] { ScaleType.Linear }, ScaleType = ScaleType.Linear, Min = xMin, Max = xMax },
                Y = { AllowedScaleTypes = new[] { ScaleType.Linear }, ScaleType = ScaleType.Linear, Min = yMin, Max = yMax }
            };
        }


        ///  <summary>
        ///  Plot normal scores for a single variable in XSeries.
        ///  </summary>
        ///  <returns></returns>
        ///  <remarks></remarks>
        private ParameterBag PlotNormal()
        {
            return PlotNormal(definition.XSeries[0].AsDoubleSeries.Data);
        }

        ///  <summary>
        ///  Plot normal scores for a single variable in XSeries.
        ///  </summary>
        ///  <remarks></remarks>
        internal ParameterBag PlotNormal(double[] y)
        {
            NormalOptions nOptions = ((NormalOptions)(definition.ChartOptions));
            NormalOptions.ScoreMethod method = nOptions.Method;
            bool shouldScaleZ = nOptions.ShouldScaleZ;

            int rows = y.Length;

            double sy = 0;
            for (int j = 0; j < rows; j++)
                sy += y[j];
            double ybar = sy / rows;
            double ssy = 0;
            for (int j = 0; j < rows; j++)
            {
                double d = y[j] - ybar;
                ssy += d * d;
            }
            double vary = ssy / rows;
            double sdy = Math.Sqrt(vary);

            double[] x = new double[rows];
            double scrap;
            ExFortran.Rank(y, x, 0, rows, 0, out scrap);

            if (method == NormalOptions.ScoreMethod.ExpectedNormalOrder)
                if (rows > 4000)
                    method = NormalOptions.ScoreMethod.VanDerWaerden;

            // Set label
            string lab;
            if (shouldScaleZ)
                lab = "Normal (" + definition.XSeries[0].Title + ")";
            else
            {
                switch (method)
                {
                    case NormalOptions.ScoreMethod.VanDerWaerden:
                        lab = "Normal scores (van der Waerden)";
                        break;
                    case NormalOptions.ScoreMethod.Blom:
                        lab = "Normal scores (Blom)";
                        break;
                    default:
                        lab = "Expected normal order scores";
                        break;
                }
            }

            int nn = y.Length;
            for (int j = 0; j < rows; j++)
            {
                switch (method)
                {
                    case NormalOptions.ScoreMethod.VanDerWaerden:
                        {
                            //  van der Waerden, Conover P 396
                            int ifault;
                            x[j] = PDF.gauinv(x[j] / (nn + 1.0), out ifault);
                            if (ifault != 0)
                                x[j] = Constant.MISSING;
                            break;
                        }
                    case NormalOptions.ScoreMethod.Blom:
                        {
                            //  Blom - Altman p143
                            int ifault;
                            x[j] = PDF.gauinv(x[j] / (nn + 1.0), out ifault);
                            if (ifault != 0)
                                x[j] = Constant.MISSING;
                            break;
                        }
                    default:
                        //  expected normal order
                        x[j] = PDF.expnos(Convert.ToInt32(x[j]), nn);
                        break;
                }
                if (shouldScaleZ && x[j] != Constant.MISSING)
                    x[j] = x[j] * sdy + ybar;
            }

            StartVectorPlot();
            SetFontsAndThicknessesFromOptions(nOptions);
            AssignMarkersToSeries(definition.XSeries, nOptions);

            DataMinMax Select_MinMaxY = DataMinMax.XCalc_YCalc;
            if (shouldScaleZ)
                Select_MinMaxY = DataMinMax.XY_CalcTogether;
            MarkerType mt = MarkerTypes[0];
            if (null != nOptions && null != nOptions.MarkerTypes && nOptions.MarkerTypes.Count >= 1)
                mt = nOptions.MarkerTypes[0];
            PlotXYInternal(x, y, lab, "Observed (" + definition.XSeries[0].Title + ")", nOptions.Title, false, Select_MinMaxY, mt.MarkerSize, mt.MarkerShape, mt.IsMarkerFilled, GetMarkerPen(mt), true);
            if (shouldScaleZ)
                DrawLineInCanvasCoordinates(axisPen, xAxisCanvas, yAxisCanvas, xAxisCanvas + xExtCanvas, yAxisCanvas + yExtCanvas);
            EndVectorPlot();

            // Regression results
            SimpleLinearRegressionContext context = new SimpleLinearRegressionContext(x, y);
            context.CalculateLeastSquaresMethod();
            return new ParameterBag("context", new FilledParameter(FilledParameterDirection.Output, context));
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
                        DrawMarkerInCanvasCoordinates(LEGEND_MARKER_X, yAxisCanvas + yExtCanvas - LEGEND_MARKER_Y_OFFSET - (size2 * g), LEGEND_MARKER_SIZE, MarkerTypes[mkr]); //  TODO: Broken?
                        DrawStringLegendL(bnam[g], LEGEND_TEXT_X, yAxisCanvas + yExtCanvas - 10 - (size2 * g));
                    }
                }
            }

            // Plot the points
            for (int g = 1; g <= ng; g++)
            {
                int mkr = ChartOptions.SeriesNumberToMarkerNumber(g - 1);
                MarkerType t = MarkerTypes[mkr];
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
                            x1 = ToCanvasX(((calcy - a[g]) / b[g]));
                    }
                    else if (calcy > maxy)
                    {
                        calcy = maxy;
                        if (b[g] != 0.0)
                            x1 = ToCanvasX(((calcy - a[g]) / b[g]));
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
                    double y2 = ToCanvasY(calcy);
                    DrawLineInCanvasCoordinates(p, x1, y1, x2, y2);
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
                    GetMinMaxArray(x, definition.ScaleParameters.X.ScaleType, out DataMinX, out DataMaxX);
                    if (minMaxY == DataMinMax.XCalc_YCalc)
                        GetMinMaxArray(y, definition.ScaleParameters.Y.ScaleType, out DataMinY, out DataMaxY);
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
                using (Pen blackFXPen = GetLinePen(MarkerTypes[10], false))
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
            PlotXYZ(x, y, w, 1, k, "control percent", "experimental percent", "L'Abbe plot (symbol size represents sample size)", false, 0, MarkerTypes[0], rmh);
        }

        private ScaleParameters GetErrorBarScaleParameters()
        {
            ErrorBarOptions eOptions = ((ErrorBarOptions)(definition.ChartOptions));

            // Setup the Min & Max Values
            DataMinX = double.MaxValue;
            DataMaxX = double.MinValue;
            DataMinY = double.MaxValue;
            DataMaxY = double.MinValue;

            foreach (MultiDoubleSeries s in eOptions.Series)
            {
                // Get the overall Min & Max Values for Y
                for (int i = 0; i <= 2; i++)
                {
                    if (DataMinY > s.MinY(i))
                        DataMinY = s.MinY(i);
                    if (DataMaxY < s.MaxY(i))
                        DataMaxY = s.MaxY(i);
                }

                // Get the overall Min & Max Values for X
                if (DataMinX > s.MinX)
                    DataMinX = s.MinX;
                if (DataMaxX < s.MaxX)
                    DataMaxX = s.MaxX;

            }

            return new ScaleParameters
            {
                X =
                    {
                        ScaleType = ScaleType.Linear,
                        AllowedScaleTypes = new[] { ScaleType.Linear },
                        Max = DataMaxX,
                        Min = DataMinX
                    },
                Y =
                    {
                        ScaleType = ScaleType.Linear,
                        AllowedScaleTypes = new[] { ScaleType.Linear },
                        Max = DataMaxY,
                        Min = DataMinY
                    }
            };
        }

        /// <summary>
        /// Series setup: Y[0] = centre value, Y[1] = lower error bar value, Y[2] = upper error bar value.
        /// </summary>
        /// <returns></returns>
        private ParameterBag PlotErrorBar()
        {
            ErrorBarOptions eOptions = ((ErrorBarOptions)(definition.ChartOptions));
            bool shouldCheckForOffsets = eOptions.ShouldCheckForOffsets;
            int seriesCount = eOptions.Series.Count;

            // Setup the Min & Max Values
            DataMinX = double.MaxValue;
            DataMaxX = double.MinValue;
            DataMinY = double.MaxValue;
            DataMaxY = double.MinValue;

            foreach (MultiDoubleSeries s in eOptions.Series)
            {
                // Get the overall Min & Max Values for Y values, including upper and lower error bars
                for (int i = 0; i <= 2; i++)
                {
                    if (DataMinY > s.MinY(i))
                        DataMinY = s.MinY(i);
                    if (DataMaxY < s.MaxY(i))
                        DataMaxY = s.MaxY(i);
                }

                // Get the overall Min & Max Values for X
                if (DataMinX > s.MinX)
                    DataMinX = s.MinX;
                if (DataMaxX < s.MaxX)
                    DataMaxX = s.MaxX;
            }

            //  If there's a legend, work out how many series there are and extend the plot area as required to hold the legend

            //  Measurements and set axes.  These are done on a scratchpad canvas before the proper measurements are set up.
            StartVectorPlot();
            SetFontsAndThicknessesFromOptions(eOptions);
            double legendFontHeight = GetFontHeightInCanvasCoordinates(legendFont);
            EndVectorPlot();

            //  By now, all measurements are known.  Set up the plot areas.
            double legendTop = yAxisCanvas - LEGEND_TOP_GAP;
            double legendRowHeight = Math.Max(LEGEND_MARKER_SIZE, Convert.ToInt32(legendFontHeight));
            double legendSpacing = MINIMUM_LEGEND_GAP + legendRowHeight;
            if (eOptions.ShowLegend && eOptions.ShowLegendIsRelevant)
            {
                //  Dim markerMidlineOffset As Double = (legendFontHeight - LEGEND_MARKER_SIZE) / 2
                double legendBottom = legendTop - (seriesCount * legendSpacing);
                if (legendBottom < LOWEST_ALLOWED_LEGEND)
                {
                    double extraSpaceRequired = LOWEST_ALLOWED_LEGEND - legendBottom;

                    //  Add in the extra space
                    imageHeight += (int)Math.Ceiling(extraSpaceRequired);
                    yAxisCanvas += extraSpaceRequired;
                    legendTop += extraSpaceRequired;
                    // legendBottom += extraSpaceRequired; 
                }
            }

            StartVectorPlot(false);
            SetFontsAndThicknessesFromOptions(eOptions);
            AssignMarkersToSeries(eOptions);

            // Draw the scale
            DrawAxesOrEnlargeCanvas(eOptions.Title, new AxisDefinition(eOptions.XAxisTitle, AxisMode.Scale, definition.ScaleParameters.X.ScaleType), new AxisDefinition(eOptions.YAxisTitle, AxisMode.Scale, definition.ScaleParameters.Y.ScaleType), boxAxes, false);

            // #1079: Prevent overdrawing of error bars by offsetting bars that would otherwise overlap.
            Dictionary<int, List<MultiDoublePoint>> alreadyUsed = new Dictionary<int, List<MultiDoublePoint>>();
            double aboutALineWidth = divx / xExtCanvas;

            // Work through the series
            for (int seriesIndex = 0; seriesIndex < eOptions.Series.Count; seriesIndex++)
            {
                MultiDoubleSeries s = eOptions.Series[seriesIndex];
                List<MultiDoublePoint> safesBySeries = new List<MultiDoublePoint>();
                int length = s.Data.Length;

                // Draw the error bars first so we don't interfere with connection lines
                using (Pen p = GetMarkerPen(eOptions.MarkerTypes[seriesIndex]))
                {
                    foreach (MultiDoublePoint pt in s.Data)
                    {
                        // Check for overlaps with any existing error bar.  If none, save this one; if there is one, offset by the line width and try again.
                        MultiDoublePoint safePoint = pt;
                        if (shouldCheckForOffsets)
                        {
                            while (true)
                            {
                                int roughX = (int)Math.Round(ToCanvasX(safePoint.X));
                                List<MultiDoublePoint> barsAtRoughX;
                                if (!alreadyUsed.TryGetValue(roughX, out barsAtRoughX))
                                {
                                    // this one's the first point at this X; known safe.  Record it and move on.
                                    alreadyUsed.Add(roughX, new List<MultiDoublePoint> { safePoint });
                                    break;
                                }

                                // If we get here, there's at least one bar at this rough X.  Check for overlaps; if they exist, offset this by 1 and try that instead.
                                bool atLeastOneOverlap = false;
                                foreach (MultiDoublePoint existingBar in barsAtRoughX)
                                {
                                    if (safePoint.get_Y(1) < existingBar.get_Y(2) && safePoint.get_Y(2) > existingBar.get_Y(1))
                                    {
                                        atLeastOneOverlap = true;
                                        safePoint = safePoint.Clone();
                                        safePoint.X += aboutALineWidth;
                                        break;
                                    }
                                }
                                if (!atLeastOneOverlap)
                                {
                                    // We've found somewhere to put this point.  Record it and move on.
                                    alreadyUsed[roughX].Add(safePoint);
                                    break;
                                }
                            }
                        }
                        safesBySeries.Add(safePoint);

                        double x = ToCanvasX(safePoint.X);
                        double yl = ToCanvasY(safePoint.get_Y(1));
                        double yu = ToCanvasY(safePoint.get_Y(2));
                        // Draw the endlines
                        DrawLineInCanvasCoordinates(p, x - 10, yl, x + 10, yl);
                        DrawLineInCanvasCoordinates(p, x - 10, yu, x + 10, yu);
                        // Draw the bar
                        DrawLineInCanvasCoordinates(p, x, yl, x, yu);
                    }
                }

                //  Plot the markers
                if (eOptions.PlotMarkers)
                {
                    foreach (MultiDoublePoint pt in safesBySeries)
                    {
                        double x1 = ToCanvasX(pt.X);
                        double y1 = ToCanvasY(pt.get_Y(0));
                        DrawMarkerInCanvasCoordinates(x1, y1, eOptions.MarkerTypes[seriesIndex].MarkerSize, eOptions.MarkerTypes[seriesIndex]);
                    }
                }

                if (eOptions.JoinMarkersWithLines)
                {
                    using (Pen pStyled = GetLinePen(eOptions.MarkerTypes[seriesIndex], false))
                    {
                        // set the initial values of x2,y2 to x1,y1
                        double x2 = ToCanvasX(s.Data[0].X);
                        double y2 = ToCanvasY(s.Data[0].get_Y(0));

                        foreach (MultiDoublePoint pt in safesBySeries)
                        {
                            double x1 = ToCanvasX(pt.X);
                            double y1 = ToCanvasY(pt.get_Y(0));
                            DrawLineInCanvasCoordinates(pStyled, x1, y1, x2, y2);
                            x2 = x1;
                            y2 = y1;
                        }
                    }
                }

                //  Legend
                if (eOptions.ShowLegend && eOptions.ShowLegendIsRelevant)
                {
                    double legendY = legendTop - (seriesIndex * legendSpacing);
                    DrawMarkerInCanvasCoordinates(xAxisCanvas + LEGEND_MARKER_SIZE / 2.0, legendY - legendFontHeight / 2.0, LEGEND_MARKER_SIZE, eOptions.MarkerTypes[seriesIndex]);
                    DrawStringLegendL(eOptions.SeriesTitles[seriesIndex], xAxisCanvas + LEGEND_MARKER_SIZE * 2, legendY);
                }
            }
            EndVectorPlot();
            return new ParameterBag();
        }

        private static ScaleParameters GetGiniScaleParameters()
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

        private ParameterBag PlotGini()
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
            using (Pen redPen = new Pen(grRed))
            {
                double x1 = ToCanvasX(0);
                double y1 = ToCanvasY(0);
                double x2 = ToCanvasX(1.0);
                double y2 = ToCanvasY(1.0);
                DrawLineInCanvasCoordinates(redPen, x1, y1, x2, y2);
            }

            // Draw Lorenz polygon
            using (Pen greenPen = new Pen(grGreen))
            {
                double lastX = offx;
                double lastY = offy;
                for (int j = 0; j <= xs0.Points - 1; j++)
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
            GetMinMaxArray(xx, ScaleType.Linear, out DataMinX, out DataMaxX);
            GetMinMaxArray(y, ScaleType.Linear, out DataMinY, out DataMaxY);

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
                    DrawMarkerInChartCoordinates(xx[r], y[r], 6, MarkerTypes[0]);

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
                        DrawMarkerInCanvasCoordinates(x1, y1, 6, MarkerTypes[0]);
                    }
                }
            }
            EndVectorPlot();
        }

        private ScaleParameters GetAgreementPairScaleParameters()
        {
            double avMin = 0;
            double avMax = 0;
            double mxdMin = 0;
            double mxdMax = 0;
            if (!(definition == null || definition.ChartOptions == null))
            {
                AgreementOptions aOptions = (AgreementOptions)definition.ChartOptions;
                GetMinMaxArray(aOptions.mxd, ScaleType.Linear, out mxdMin, out mxdMax);
                if (aOptions.HasLimits)
                {
                    if (aOptions.lla < mxdMin)
                        mxdMin = aOptions.lla;
                    if (aOptions.ula > mxdMax)
                        mxdMax = aOptions.ula;
                }
                GetMinMaxArray(aOptions.av, ScaleType.Linear, out avMin, out avMax);
            }

            return new ScaleParameters
            {
                X =
                {
                    AllowedScaleTypes = new[] { ScaleType.Linear },
                    Max = avMax,
                    Min = avMin
                },
                Y =
                {
                    AllowedScaleTypes = new[] { ScaleType.Linear },
                    Max = mxdMax,
                    Min = mxdMin
                }
            };
        }

        private ParameterBag PlotAgreementPair()
        {
            AgreementOptions aOptions = (AgreementOptions)definition.ChartOptions;
            StartVectorPlot();
            double mxdMin;
            double mxdMax;
            GetMinMaxArray(aOptions.mxd, definition.ScaleParameters.Y.ScaleType, out mxdMin, out mxdMax);
            using (Pen p = GetMarkerPen(MarkerTypes[0]))
            {
                if (aOptions.HasLimits)
                {
                    if (aOptions.lla < mxdMin)
                        mxdMin = aOptions.lla;
                    if (aOptions.ula > mxdMax)
                        mxdMax = aOptions.ula;
                    string xtxt = definition.ChartOptions.XAxisTitle;
                    if (string.IsNullOrEmpty(xtxt))
                        xtxt = "mean";
                    string ytxt = definition.ChartOptions.YAxisTitle;
                    if (string.IsNullOrEmpty(ytxt))
                        ytxt = "difference";
                    PlotXYInternal(aOptions.av, aOptions.mxd, xtxt, ytxt, "Agreement Plot (" + Formatting.XRound(100 * (1 - aOptions.P0), 2) + "% limits of agreement)", false, DataMinMax.XCalc_YPreset, MarkerTypes[0].MarkerSize, MarkerTypes[0].MarkerShape, MarkerTypes[0].IsMarkerFilled, p, false, 0, 0, mxdMin, mxdMax);
                }
                else
                {
                    string xtxt = definition.ChartOptions.XAxisTitle;
                    if (string.IsNullOrEmpty(xtxt))
                        xtxt = "mean";
                    string ytxt = definition.ChartOptions.YAxisTitle;
                    if (string.IsNullOrEmpty(ytxt))
                        ytxt = "maximum difference";
                    PlotXYInternal(aOptions.av, aOptions.mxd, xtxt, ytxt, "Agreement Plot", false, DataMinMax.XCalc_YPreset, MarkerTypes[0].MarkerSize, MarkerTypes[0].MarkerShape, MarkerTypes[0].IsMarkerFilled, p, false);
                }
            }

            // Plot mean
            using (Pen greenPen = new Pen(grGreen, 2))
            {
                double y1 = ToCanvasY(aOptions.mean);
                DrawLineInCanvasCoordinates(greenPen, xAxisCanvas, y1, xAxisCanvas + xExtCanvas, y1);
                if (aOptions.HasLimits)
                {
                    // Plot upper limit
                    y1 = ToCanvasY(aOptions.ula);
                    DrawLineInCanvasCoordinates(greenPen, xAxisCanvas, y1, xAxisCanvas + xExtCanvas, y1);
                    // Plot lower limit
                    y1 = ToCanvasY(aOptions.lla);
                    DrawLineInCanvasCoordinates(greenPen, xAxisCanvas, y1, xAxisCanvas + xExtCanvas, y1);
                }
            }
            EndVectorPlot();
            return new ParameterBag();
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

        ///  <summary>
        ///  Cause the host to amend the thisData record in-place with any revisions to the cutoff data.
        ///  </summary>
        ///  <param name="host"></param>
        ///  <param name="thisData"></param>
        ///  <param name="Weight"></param>
        ///  <param name="ti"></param>
        ///  <remarks></remarks>
        private static ROCSeriesRecord ShowCutoff(ITemplateHost host, ROCSeriesRecord thisData, double Weight, string ti)
        {
            ROCCutoff payload = new ROCCutoff { SeriesRecord = thisData, Weight = Weight, Title = ti };
            host.Amend(payload, null);
            return payload.SeriesRecord;
        }

        private static double DeLongPsi(double x, double y)
        {
            if (y == x)
                return 0.5;
            return (y < x) ? 1.0 : 0.0;
        }

        private static double DeLongSE(double[] x, double[] y, double auc)
        {
            double[] v10 = new double[x.Length];
            double[] v01 = new double[y.Length];
            for (int i = 0; i < x.Length; i++)
            {
                for (int j = 0; j < y.Length; j++)
                    v10[i] += DeLongPsi(x[i], y[j]);
                v10[i] /= y.Length;
            }
            for (int j = 0; j < y.Length; j++)
            {
                for (int i = 0; i < x.Length; i++)
                    v01[j] += DeLongPsi(x[i], y[j]);
                v01[j] /= x.Length;
            }
            double s10 = 0.0;
            double s01 = 0.0;
            for (int i = 0; i < x.Length; i++)
                s10 += Math.Pow((v10[i] - auc), 2.0);
            s10 /= x.Length - 1;
            for (int j = 0; j < y.Length; j++)
                s01 += Math.Pow((v01[j] - auc), 2.0);
            s01 /= y.Length - 1;
            double var = s10 / x.Length + s01 / y.Length;
            return var < 0.0 ? Constant.MISSING : Math.Sqrt(var);
        }

        private static void Swap(ref double x, ref double y)
        {
            double temp = x;
            x = y;
            y = temp;
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
                        DrawMarkerInCanvasCoordinates(12, yAxisCanvas + yExtCanvas - 22 - (size2 * k), 6, MarkerTypes[(k - 1) % 9]);
                    }
                    else
                    {
                        using (Pen p = GetMarkerPen(MarkerTypes[(k - 1) % 9]))
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
                MarkerType mt = MarkerTypes[(k - 1) % 9];
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

        internal void Plot_MH(int k, double[,] o, double[] odw, string[] title, double rmh, double ll, double ul, double cco, double[] odr, double[] odrl, double[] odru, bool[] lerr, bool[] uerr, string cap, int pbias, string qid, out bool ifault)
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
                dotPen = GetMarkerPen(MarkerTypes[10]),
                pooledCiPen = GetLinePen(pooledMarkerType, true),
                tenPenTrue = GetMarkerPen(MarkerTypes[10]),
                pooledEffectPen = GetLinePen(MarkerTypes[10], false))
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
                    double xm = ToCanvasX(rmh);
                    double xl = ToCanvasX(ll);
                    double xr = ToCanvasX(ul);
                    double y2 = (ytop - yctr) / 1.5;
                    yc = offy + yctr;
                    yt = offy + yctr + y2;
                    // yb = offy + yctr - Y2; 
                    DrawMarkerInCanvasCoordinates(xm, yc, y2, pooledMarkerType);
                    DrawLineInCanvasCoordinates(pooledCiPen, xr, yc, xl, yc);
                    // pooled effect marker
                    DrawLineInCanvasCoordinates(pooledEffectPen, xm, save_yc, xm, yt);
                    // pool label
                    DrawStringLabel(combo_ti(cap), xAxisCanvas - 15, yc + txh / 2, StringAlignment.Far);
                    DrawStringLabel(Formatting.RoundMeta(rmh, absmin) + " (" + Formatting.RoundMeta(ll, absmin) + ", " + Formatting.RoundMeta(ul, absmin) + ")", xAxisCanvas + xExtCanvas + 10, yc + txh / 2, StringAlignment.Near);
                    // xaxis label
                }
            }

            EndVectorPlot();

            ifault = false;
        }

        internal void Plot_MHRD(int k, double[] odw, string[] title, double rmh, double ll, double ul, double cco, double[] odr, double[] odrl, double[] odru, bool[] lerr, bool[] uerr, string cap, int pbias, string qid, out bool fault)
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

            using (Pen tenPenTrue = GetLinePen(MarkerTypes[10], true),
                dotPen = GetMarkerPen(MarkerTypes[10]),
                tenPenFalse = GetLinePen(MarkerTypes[10], false))
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
                        double XM = ToCanvasX(odr[i]);
                        double xl = odrl[i] == Constant.MISSING ? xAxisCanvas : ToCanvasX(odrl[i]);
                        double xr;
                        if (odru[i] == double.PositiveInfinity || odru[i] == Constant.MISSING)
                            xr = xAxisCanvas + xExtCanvas;
                        else
                            xr = ToCanvasX(odru[i]);
                        // Weight blob.  Draw this first so that the line appears in front of it in the case of short lines (#994).
                        // #688: Make blob size proportional to sqrt(1/variance) rather than 1/variance
                        double blobSize = (5 + Math.Abs(yt - yb) * (Math.Sqrt(gw[i] / max_gw))) * 0.7;
                        DrawMarkerInCanvasCoordinates(XM, yc, blobSize / 2, studyMarkerType);

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
                        DrawMarkerInCanvasCoordinates(XM, yc, 2, MarkerShape.Circle, true, dotPen);

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
                    double XM = ToCanvasX(rmh);
                    double xl = ToCanvasX(ll);
                    double xr = ToCanvasX(ul);
                    double y2 = (ytop - yctr) / 1.5;
                    yc = offy + yctr;
                    yt = offy + yctr + y2;
                    // yb = offy + yctr - Y2; 
                    DrawDiamondInCanvasCoordinates(tenPenTrue, XM, yc, y2 * 2, false);
                    DrawLineInCanvasCoordinates(tenPenTrue, xr, yc, xl, yc);
                    // pooled effect marker
                    DrawLineInCanvasCoordinates(tenPenFalse, XM, save_yc, XM, yt);
                    // pool label
                    DrawStringLabel(combo_ti(cap), xAxisCanvas - 15, yc + txh / 2, StringAlignment.Far);
                    DrawStringLabel(Formatting.RoundMeta(rmh, absMin) + " (" + Formatting.RoundMeta(ll, absMin) + ", " + Formatting.RoundMeta(ul, absMin) + ")", xAxisCanvas + xExtCanvas + 10, yc + txh / 2, StringAlignment.Near);
                    // x axis text
                }
            }

            fault = false;
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

            using (Pen linePen = GetLinePen(MarkerTypes[10], true))
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
                    using (Pen pooledEffectPen = GetLinePen(MarkerTypes[10], false))
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

        public enum CorrelationRowType
        {
            Pooled = -1,
            Study = 0,
            Subgroup = 1
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

            using (Pen markerPen = GetMarkerPen(MarkerTypes[10]),
                linePen = GetLinePen(MarkerTypes[10], true),
                pooledEffectPen = GetLinePen(MarkerTypes[10], false),
                dotPen = GetMarkerPen(MarkerTypes[10]))
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
                        double pcon = calcy + (sey * pert);
                        double x1P = calcx;
                        double y1P = pcon;
                        double ncon = calcy - (sey * pert);
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
                        double pcon = calcy + (sey * pert);
                        double x1P = calcx;
                        double y1P = pcon;
                        double ncon = calcy - (sey * pert);
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
                        double sey = Math.Sqrt(ms * ((1.0 / nx + Math.Pow((calcx - (sumx / nx)), 2.0) / ssx)));
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
