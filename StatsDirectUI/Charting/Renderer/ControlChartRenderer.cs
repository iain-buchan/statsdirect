using System;
using System.Drawing;
using StatsDirect.Numerics;
using StatsDirect.Templates;

namespace StatsDirect.Charting.Renderer
{
    class ControlChartRenderer: AbstractChartRenderer, IChartRenderer
    {
        public ControlChartRenderer(ChartDefinition definition, ICanvasFactory canvasFactory)
            : base(definition, canvasFactory)
        {
        }

        ScaleParameters IChartRenderer.GetScaleParameters()
        {
            DoubleSeries ys0 = Definition.YSeries[0].AsDoubleSeries;
            DoubleSeries xs0 = Definition.XSeries[0].AsDoubleSeries;
            int rows = xs0.Points;
            double[] xdat = new double[rows + 1];
            double[] ydat = new double[rows + 1];

            int ctr = 0;
            double[] ySeriesData = ys0.Data;
            double[] xSeriesData = xs0.Data;
            for (int r = 0; r < rows; r++)
            {
                if (ySeriesData[r] != Constant.MISSING && xSeriesData[r] != Constant.MISSING)
                {
                    xdat[ctr] = xSeriesData[r];
                    ydat[ctr] = ySeriesData[r];
                    ctr++;
                }
            }
            rows = ctr;

            MathDbl.meansd(ydat, 0, ref rows, out double ymean, out double ysd);

            ControlOptions cOptions = (ControlOptions)Definition.ChartOptions;
            int kobs = cOptions.ObservationsToUse;
            if (cOptions.HasUserSpecifiedMeanAndSD)
            {
                ymean = cOptions.UserSpecifiedMean;
                ysd = cOptions.UserSpecifiedSD;
            }

            cOptions.HasUserSpecifiedLimits = cOptions.LowerWarningLimit != Constant.MISSING && cOptions.UpperWarningLimit != Constant.MISSING && cOptions.LowerControlLimit != Constant.MISSING && cOptions.UpperControlLimit != Constant.MISSING;
            if (cOptions.HasUserSpecifiedLimits)
            {
                if (cOptions.LowerControlLimit > cOptions.UpperControlLimit)
                {
                    double temp = cOptions.LowerControlLimit;
                    cOptions.LowerControlLimit = cOptions.UpperControlLimit;
                    cOptions.UpperControlLimit = temp;
                }
                if (cOptions.LowerWarningLimit > cOptions.UpperWarningLimit)
                {
                    double temp = cOptions.LowerWarningLimit;
                    cOptions.LowerWarningLimit = cOptions.UpperWarningLimit;
                    cOptions.UpperWarningLimit = temp;
                }
                if (cOptions.LowerControlLimit > cOptions.LowerWarningLimit)
                {
                    double temp = cOptions.LowerControlLimit;
                    cOptions.LowerControlLimit = cOptions.LowerWarningLimit;
                    cOptions.LowerWarningLimit = temp;
                }
                if (cOptions.UpperWarningLimit > cOptions.UpperControlLimit)
                {
                    double temp = cOptions.UpperControlLimit;
                    cOptions.UpperControlLimit = cOptions.UpperWarningLimit;
                    cOptions.UpperWarningLimit = temp;
                }
                if (DataMinY > cOptions.LowerControlLimit)
                    DataMinY = cOptions.LowerControlLimit;
                if (DataMaxY < cOptions.UpperControlLimit)
                    DataMaxY = cOptions.UpperControlLimit;
            }
            else
            {
                if (kobs != rows)
                {
                    MathDbl.meansd(ydat, 0, ref kobs, out ymean, out ysd);
                    // restricted = true; 
                }
                if (ysd != Constant.MISSING)
                {
                    if (DataMinY > ymean - ysd * 3.0)
                        DataMinY = ymean - ysd * 3.0;
                    if (DataMaxY < ymean + ysd * 3.0)
                        DataMaxY = ymean + ysd * 3.0;
                }
            }

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

        ///  <summary>
        ///  Do a control plot.  Expects one X series and one Y series.
        ///  </summary>
        /// <returns>True if the plot succeeds, False otherwise.</returns>
        ParameterBag IChartRenderer.Plot(ITemplateHost host)
        {
            DoubleSeries ys0 = Definition.YSeries[0].AsDoubleSeries;
            DoubleSeries xs0 = Definition.XSeries[0].AsDoubleSeries;
            int rows = xs0.Points;
            double[] xdat = new double[rows + 1];
            double[] ydat = new double[rows + 1];

            int ctr = 0;
            bool looksLikeDates = true;
            double[] ySeriesData = ys0.Data;
            double[] xSeriesData = xs0.Data;
            for (int r = 0; r < rows; r++)
            {
                if (ySeriesData[r] != Constant.MISSING && xSeriesData[r] != Constant.MISSING)
                {
                    xdat[ctr] = xSeriesData[r];
                    ydat[ctr] = ySeriesData[r];
                    if (xdat[ctr] < 20000)
                        looksLikeDates = false;
                    ctr++;
                }
            }
            rows = ctr;

            MathDbl.meansd(ydat, 0, ref rows, out double ymean, out double ysd);

            ControlOptions cOptions = (ControlOptions)Definition.ChartOptions;
            cOptions.UseDates = looksLikeDates;
            double oldymean = ymean;
            double oldysd = ysd;
            int kobs = cOptions.ObservationsToUse;
            if (cOptions.HasUserSpecifiedMeanAndSD)
            {
                ymean = cOptions.UserSpecifiedMean;
                ysd = cOptions.UserSpecifiedSD;
            }

            bool restricted = false; bool external = false;
            cOptions.HasUserSpecifiedLimits = cOptions.LowerWarningLimit != Constant.MISSING & cOptions.UpperWarningLimit != Constant.MISSING & cOptions.LowerControlLimit != Constant.MISSING & cOptions.UpperControlLimit != Constant.MISSING;
            if (cOptions.HasUserSpecifiedLimits)
            {
                external = true;
                if (cOptions.LowerControlLimit > cOptions.UpperControlLimit)
                {
                    double temp = cOptions.LowerControlLimit;
                    cOptions.LowerControlLimit = cOptions.UpperControlLimit;
                    cOptions.UpperControlLimit = temp;
                }
                if (cOptions.LowerWarningLimit > cOptions.UpperWarningLimit)
                {
                    double temp = cOptions.LowerWarningLimit;
                    cOptions.LowerWarningLimit = cOptions.UpperWarningLimit;
                    cOptions.UpperWarningLimit = temp;
                }
                if (cOptions.LowerControlLimit > cOptions.LowerWarningLimit)
                {
                    double temp = cOptions.LowerControlLimit;
                    cOptions.LowerControlLimit = cOptions.LowerWarningLimit;
                    cOptions.LowerWarningLimit = temp;
                }
                if (cOptions.UpperWarningLimit > cOptions.UpperControlLimit)
                {
                    double temp = cOptions.UpperControlLimit;
                    cOptions.UpperControlLimit = cOptions.UpperWarningLimit;
                    cOptions.UpperWarningLimit = temp;
                }
                if (DataMinY > cOptions.LowerControlLimit)
                    DataMinY = cOptions.LowerControlLimit;
                if (DataMaxY < cOptions.UpperControlLimit)
                    DataMaxY = cOptions.UpperControlLimit;
            }
            else
            {
                if (kobs != rows)
                {
                    MathDbl.meansd(ydat, 0, ref kobs, out ymean, out ysd);
                    restricted = true;
                }
                else
                {
                    // restricted = false; 
                    external = oldymean != ymean || oldysd != ysd;
                }
                if (ysd != Constant.MISSING)
                {
                    if (DataMinY > ymean - ysd * 3.0)
                        DataMinY = ymean - ysd * 3.0;
                    if (DataMaxY < ymean + ysd * 3.0)
                        DataMaxY = ymean + ysd * 3.0;
                }
            }

            const int RHS_LABEL_GAP = 7;

            StartVectorPlot();

            //  Fonts
            SetFontsAndThicknessesFromOptions(cOptions);
            //  NB we use the Legend font as the Control Label font!

            // Draw the scale
            AssignMarkersToSeries();

            // adjust drawing window for right hand labels and vertical date labels
            double rgap = 0;
            if (cOptions.UseMean || cOptions.Use1SD || cOptions.Use2SD || cOptions.Use3SD)
                rgap = RHS_LABEL_GAP + LegendWidthInCanvasCoordinates(Math.Round(ymean + ysd * 3.0, cOptions.RightHandDecimalPlaces) + " (+3 SD)");
            if (cOptions.UseDates)
            {
                float vshift = AxisLabelWidthInCanvasCoordinates(new DateTime(1899, 12, 30, 0, 0, 0).AddDays(xdat[0]).ToString("d")) + 30;
                YAxisCanvas += vshift;
                YExtCanvas -= vshift;
            }

            double xtra = 0;
            double w = TitleWidthInCanvasCoordinates(cOptions.YAxisTitle) + 30;
            if (w > xtra + XAxisCanvas)
                xtra = w - XAxisCanvas;

            XAxisCanvas += xtra;
            XExtCanvas -= xtra;

            // draw the axes
            double xspace = 0;
            AxisMode xmode = AxisMode.Scale;
            if (cOptions.UseDates)
            {
                xspace = AxisLabelWidthInCanvasCoordinates(new DateTime(1900, 1, 1, 0, 0, 0).ToString("d"));
                xmode = AxisMode.ScaleWithoutLabels;
            }
            DrawAxesOrEnlargeCanvas(cOptions.Title,
                new AxisDefinition(cOptions.XAxisTitle, xmode, Definition.ScaleParameters.X.ScaleType) { ExtraSpaceBeforeAxisStarts = xspace, ExtraSpaceAfterAxisEnds = rgap },
                new AxisDefinition(cOptions.YAxisTitle, AxisMode.Scale, Definition.ScaleParameters.Y.ScaleType),
                cOptions.ShouldBoxAxes, false);

            double x1; double y1; double last_x1 = 0;

            // set then initial values of x2,y2 to x1,y1
            ToCanvasX(xdat[1]);
            ToCanvasY(ydat[1]);
            // plot points
            PointF[] xys = new PointF[rows];
            for (int r = 0; r <= rows - 1; r++)
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
            DrawMarkerSeriesInCanvasCoordinates(xys, 6, xs0.MarkerDetails.MarkerShape, xs0.MarkerDetails.IsMarkerFilled, xs0.MarkerDetails.MarkerPen, xs0.MarkerDetails.LinePen, true, false);

            // draw vertical date markers
            if (cOptions.UseDates)
            {
                //  If labels overlap, scale down font
                ctr = 0;
                double scaler = 1.0;
                do
                {
                    bool ok = true;
                    for (int r = 0; r < rows; r++)
                    {
                        if (xdat[r] != Constant.MISSING & ydat[r] != Constant.MISSING)
                        {
                            x1 = ToCanvasX(xdat[r]);
                            // y1 = YAxisCanvas - 14; 
                            string tx = new DateTime(1899, 12, 30, 0, 0, 0).AddDays(xdat[r]).ToString("d");
                            if (Math.Abs(x1 - last_x1) < AxisLabelHeightInCanvasCoordinates(tx))
                            {
                                ok = false;
                                break;
                            }
                            last_x1 = x1;
                        }
                    }
                    if (ok || ctr > 15)
                        break;
                    // At least one label overlaps; try a smaller scale font and go again
                    scaler = scaler * 0.9;
                    ctr++;
                } while (true);
                //  TODO: Scale font to scaler if needed
                using (StringFormat txtFormat = new StringFormat())
                {
                    txtFormat.Alignment = StringAlignment.Near;

                    for (int r = 0; r < rows; r++)
                    {
                        if (xdat[r] != Constant.MISSING && ydat[r] != Constant.MISSING)
                        {
                            x1 = ToCanvasX(xdat[r]);
                            string tx = new DateTime(1899, 12, 30, 0, 0, 0).AddDays(xdat[r]).ToString("d");
                            y1 = YAxisCanvas - AxisLabelWidthInCanvasCoordinates(tx) - AXIS_BIG_TICK - 3;
                            DrawStringAtAngleInCanvasCoordinates(tx, AxisLabelFont, Brushes.Black, x1 - AxisLabelHeightInCanvasCoordinates(tx) / 2, y1, txtFormat, LabelDirection.Up);
                        }
                    }
                }
            }

            int rhDp = cOptions.RightHandDecimalPlaces;

            using (Pen blackPen = new Pen(GrBlack))
            {
                if (cOptions.HasUserSpecifiedLimits)
                {
                    // user specified control and warning lines
                    x1 = XAxisCanvas + XExtCanvas;
                    y1 = ToCanvasY(cOptions.UpperWarningLimit);
                    DrawLineInCanvasCoordinates(blackPen, XAxisCanvas, y1, x1, y1);
                    string tx = Math.Round(cOptions.UpperWarningLimit, rhDp) + " (warn)";
                    DrawStringLegendL(tx, x1 + RHS_LABEL_GAP, y1 + LegendHeightInCanvasCoordinates(tx) / 2);
                    y1 = ToCanvasY(cOptions.LowerWarningLimit);
                    DrawLineInCanvasCoordinates(blackPen, XAxisCanvas, y1, x1, y1);
                    tx = Math.Round(cOptions.LowerWarningLimit, rhDp) + " (warn)";
                    DrawStringLegendL(tx, x1 + RHS_LABEL_GAP, y1 + LegendHeightInCanvasCoordinates(tx) / 2);
                    using (Pen redPen = new Pen(GrRed))
                    {
                        y1 = ToCanvasY(cOptions.UpperControlLimit);
                        DrawLineInCanvasCoordinates(redPen, XAxisCanvas, y1, x1, y1);
                        tx = Math.Round(cOptions.UpperControlLimit, rhDp) + " (ctrl)";
                        DrawStringLegendL(tx, x1 + RHS_LABEL_GAP, y1 + LegendHeightInCanvasCoordinates(tx) / 2);
                        y1 = ToCanvasY(ymean - ysd * 3.0);
                        DrawLineInCanvasCoordinates(redPen, XAxisCanvas, y1, x1, y1);
                        tx = Math.Round(cOptions.LowerControlLimit, rhDp) + " (ctrl)";
                        DrawStringLegendL(tx, x1 + RHS_LABEL_GAP, y1 + LegendHeightInCanvasCoordinates(tx) / 2);
                        DrawStringLegendL("External:", x1 + RHS_LABEL_GAP, YAxisCanvas + YExtCanvas);
                    }
                }
                else
                {
                    // draw control lines
                    if (cOptions.UseMean)
                    {
                        x1 = XAxisCanvas + XExtCanvas;
                        y1 = ToCanvasY(ymean);
                        DrawLineInCanvasCoordinates(blackPen, XAxisCanvas, y1, x1, y1);
                        string tx = Math.Round(ymean, rhDp) + " (mean)";
                        DrawStringLegendL(tx, x1 + RHS_LABEL_GAP, y1 + LegendHeightInCanvasCoordinates(tx) / 2);
                        if (restricted)
                            DrawStringLegendL("On first " + kobs + " points:", x1 + RHS_LABEL_GAP, YAxisCanvas + YExtCanvas);
                        else if (external)
                            DrawStringLegendL("External:", x1 + RHS_LABEL_GAP, YAxisCanvas + YExtCanvas);
                    }

                    if (ysd != Constant.MISSING)
                    {
                        string tx;
                        if (cOptions.Use1SD)
                        {
                            using (Pen greenPen = new Pen(GrGreen))
                            {
                                x1 = XAxisCanvas + XExtCanvas;
                                y1 = ToCanvasY(ymean + ysd);
                                DrawLineInCanvasCoordinates(greenPen, XAxisCanvas, y1, x1, y1);
                                tx = Math.Round(ymean + ysd, rhDp) + " (+1 SD)";
                                DrawStringLegendL(tx, x1 + RHS_LABEL_GAP, y1 + LegendHeightInCanvasCoordinates(tx) / 2);
                                y1 = ToCanvasY(ymean - ysd);
                                DrawLineInCanvasCoordinates(greenPen, XAxisCanvas, y1, x1, y1);
                                tx = Math.Round(ymean - ysd, rhDp) + " (-1 SD)";
                                DrawStringLegendL(tx, x1 + RHS_LABEL_GAP, y1 + LegendHeightInCanvasCoordinates(tx) / 2);
                            }
                        }

                        if (cOptions.Use2SD)
                        {
                            x1 = XAxisCanvas + XExtCanvas;
                            y1 = ToCanvasY(ymean + ysd * 2.0);
                            DrawLineInCanvasCoordinates(blackPen, XAxisCanvas, y1, x1, y1);
                            tx = Math.Round(ymean + ysd * 2.0, rhDp) + " (+2 SD)";
                            DrawStringLegendL(tx, x1 + RHS_LABEL_GAP, y1 + LegendHeightInCanvasCoordinates(tx) / 2);
                            y1 = ToCanvasY(ymean - ysd * 2.0);
                            DrawLineInCanvasCoordinates(blackPen, XAxisCanvas, y1, x1, y1);
                            tx = Math.Round(ymean - ysd * 2.0, rhDp) + " (-2 SD)";
                            DrawStringLegendL(tx, x1 + RHS_LABEL_GAP, y1 + LegendHeightInCanvasCoordinates(tx) / 2);
                        }

                        if (cOptions.Use3SD)
                        {
                            using (Pen redPen = new Pen(GrRed))
                            {
                                x1 = XAxisCanvas + XExtCanvas;
                                y1 = ToCanvasY(ymean + ysd * 3.0);
                                DrawLineInCanvasCoordinates(redPen, XAxisCanvas, y1, x1, y1);
                                tx = Math.Round(ymean + ysd * 3.0, rhDp) + " (+3 SD)";
                                DrawStringLegendL(tx, x1 + RHS_LABEL_GAP, y1 + LegendHeightInCanvasCoordinates(tx) / 2);
                                y1 = ToCanvasY(ymean - ysd * 3.0);
                                DrawLineInCanvasCoordinates(redPen, XAxisCanvas, y1, x1, y1);
                                tx = Math.Round(ymean - ysd * 3.0, rhDp) + " (-3 SD)";
                                DrawStringLegendL(tx, x1 + RHS_LABEL_GAP, y1 + LegendHeightInCanvasCoordinates(tx) / 2);
                            }
                        }
                    }
                }
            }

            EndVectorPlot();
            return new ParameterBag();
        }
    }
}
