using StatsDirect.Numerics;
using StatsDirect.Templates;
using System;
using System.Drawing;

namespace StatsDirect.Charting.Renderer
{
    internal class ControlChartRenderer : AbstractChartRenderer, IChartRenderer
    {
        const int RHS_LABEL_GAP = 7;

        public ControlChartRenderer(ChartDefinition definition, ICanvasFactory canvasFactory, ISdPreferences sdPreferences)
            : base(definition, canvasFactory, sdPreferences)
        {
        }

        ScaleParameters IChartRenderer.GetScaleParameters()
        {
            DoubleSeries ys0 = (DoubleSeries)Definition.YSeries[0];
            DoubleSeries xs0 = (DoubleSeries)Definition.XSeries[0];
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

            MathDbl.MeanSD(ydat, ref rows, out double ymean, out double ysd);

            ControlOptions cOptions = (ControlOptions)Definition.ChartOptions;
            int kobs = cOptions.ObservationsToUse ?? rows;
            if (cOptions.UserSpecifiedMeanAndStandardDeviation is not null)
            {
                ymean = cOptions.UserSpecifiedMeanAndStandardDeviation.Mean ?? ymean;
                ysd = cOptions.UserSpecifiedMeanAndStandardDeviation.StandardDeviation ?? ysd;
            }

            if (cOptions.HasUserSpecifiedLimits)
            {
                if (cOptions.ControlAndWarningLimits.LowerControlLimit.HasValue && DataMinY > cOptions.ControlAndWarningLimits.LowerControlLimit.Value)
                    DataMinY = cOptions.ControlAndWarningLimits.LowerControlLimit.Value;
                if (cOptions.ControlAndWarningLimits.UpperControlLimit.HasValue && DataMaxY < cOptions.ControlAndWarningLimits.UpperControlLimit.Value)
                    DataMaxY = cOptions.ControlAndWarningLimits.UpperControlLimit.Value;
            }
            else
            {
                if (kobs != rows)
                {
                    MathDbl.MeanSD(ydat, ref kobs, out ymean, out ysd);
                }
                if (ysd != Constant.MISSING)
                {
                    if (DataMinY > ymean - ysd * 3.0)
                        DataMinY = ymean - ysd * 3.0;
                    if (DataMaxY < ymean + ysd * 3.0)
                        DataMaxY = ymean + ysd * 3.0;
                }
            }

            return new ScaleParameters(
                new()
                {
                    ScaleType = looksLikeDates ? ScaleType.Date : ScaleType.Linear,
                    AllowedScaleTypes = new[] { ScaleType.Linear, ScaleType.Date },
                    Max = DataMaxX,
                    Min = DataMinX
                },
                new()
                {
                    AllowedScaleTypes = new[] { ScaleType.Linear },
                    Max = DataMaxY,
                    Min = DataMinY
                }
            );
        }

        ///  <summary>
        ///  Plot a control chart.  Expects one X series and one Y series.
        ///  </summary>
        ParameterBag IChartRenderer.Plot(bool isForReturnedParametersOnly)
        {
            if (isForReturnedParametersOnly)
                return new ParameterBag();

            DoubleSeries ys0 = (DoubleSeries)Definition.YSeries[0];
            DoubleSeries xs0 = (DoubleSeries)Definition.XSeries[0];
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

            MathDbl.MeanSD(ydat, ref rows, out double ymean, out double ysd);

            ControlOptions cOptions = (ControlOptions)Definition.ChartOptions;
            bool useDates = Definition.HasScaleParameters && Definition.ScaleParameters.X.ScaleType == ScaleType.Date;
            double oldymean = ymean;
            double oldysd = ysd;
            int kobs = cOptions.ObservationsToUse ?? rows;
            if (cOptions.UserSpecifiedMeanAndStandardDeviation is not null)
            {
                ymean = cOptions.UserSpecifiedMeanAndStandardDeviation.Mean ?? ymean;
                ysd = cOptions.UserSpecifiedMeanAndStandardDeviation.StandardDeviation ?? ysd;
            }

            bool restricted = false; bool external = false;
            if (cOptions.HasUserSpecifiedLimits)
            {
                external = true;
                if (cOptions.ControlAndWarningLimits.LowerControlLimit.HasValue && DataMinY > cOptions.ControlAndWarningLimits.LowerControlLimit.Value)
                    DataMinY = cOptions.ControlAndWarningLimits.LowerControlLimit.Value;
                if (cOptions.ControlAndWarningLimits.UpperControlLimit.HasValue && DataMaxY < cOptions.ControlAndWarningLimits.UpperControlLimit.Value)
                    DataMaxY = cOptions.ControlAndWarningLimits.UpperControlLimit.Value;
            }
            else
            {
                if (kobs != rows)
                {
                    MathDbl.MeanSD(ydat, ref kobs, out ymean, out ysd);
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

            StartVectorPlot(cOptions);
            //  NB we use the Legend font as the Control Label font

            // Draw the scale
            AssignMarkersToSeries();

            // adjust drawing window for right hand labels and vertical date labels
            double rgap = 0;
            if (cOptions.UseMean || cOptions.Use1SD || cOptions.Use2SD || cOptions.Use3SD)
                rgap = RHS_LABEL_GAP + LegendWidthInCanvasCoordinates(Math.Round(ymean + ysd * 3.0, cOptions.RightHandDecimalPlaces) + " (+3 SD)");
            if (useDates)
            {
                double vshift = AxisLabelWidthInCanvasCoordinates(new DateTime(1899, 12, 30, 0, 0, 0).AddDays(xdat[0]).ToString("d")) + 30;
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
            LayoutChartAndDrawAxes(cOptions.Title,
                new AxisDefinition(cOptions.XAxisTitle, AxisMode.Scale, Definition.ScaleParameters.X.ScaleType) { ExtraSpaceAfterAxisEnds = rgap },
                new AxisDefinition(cOptions.YAxisTitle, AxisMode.Scale, Definition.ScaleParameters.Y.ScaleType),
                cOptions.ShouldBoxAxes, false);

            // plot points
            PointF[] xys = new PointF[rows];
            for (int r = 0; r < rows; r++)
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

            PenDescriptor markerPen = GetMarkerPen(xs0.MarkerType);
            PenDescriptor linePen = GetLinePen(xs0.MarkerType, true);
            DrawMarkerSeriesInCanvasCoordinates(xys, 6, xs0.MarkerType.MarkerShape, xs0.MarkerType.IsMarkerFilled, markerPen, linePen, true, false);

            PenDescriptor blackPen = new(GrBlack);
            if (cOptions.HasUserSpecifiedLimits)
            {
                ControlAndWarningLimits controlAndWarningLimits = cOptions.ControlAndWarningLimits;
                // user specified control and warning lines
                MaybeDrawControlLine(blackPen, controlAndWarningLimits.UpperWarningLimit, "warn");
                MaybeDrawControlLine(blackPen, controlAndWarningLimits.LowerWarningLimit, "warn");
                PenDescriptor redPen = new(GrRed);
                MaybeDrawControlLine(redPen, controlAndWarningLimits.UpperControlLimit, "ctrl");
                MaybeDrawControlLine(redPen, controlAndWarningLimits.LowerControlLimit, "ctrl");
                double noteX = XAxisCanvas + XExtCanvas + RHS_LABEL_GAP;
                double noteY = YAxisCanvas + YExtCanvas;
                DrawStringLegendL("External:", noteX, noteY);
            }
            else
            {
                // draw control lines
                if (cOptions.UseMean)
                {
                    double noteX = XAxisCanvas + XExtCanvas + RHS_LABEL_GAP;
                    double noteY = YAxisCanvas + YExtCanvas;
                    DrawControlLine(blackPen, ymean, "mean");
                    if (restricted)
                        DrawStringLegendL($"On first {kobs} points:", noteX, noteY);
                    else if (external)
                        DrawStringLegendL("External:", noteX, noteY);
                }

                if (ysd != Constant.MISSING)
                {
                    if (cOptions.Use1SD)
                    {
                        PenDescriptor greenPen = new(GrGreen);
                        DrawControlLine(greenPen, ymean + ysd, "+1 SD");
                        DrawControlLine(greenPen, ymean - ysd, "-1 SD");
                    }

                    if (cOptions.Use2SD)
                    {
                        DrawControlLine(blackPen, ymean + ysd * 2.0, "+2 SD");
                        DrawControlLine(blackPen, ymean - ysd * 2.0, "-2 SD");
                    }

                    if (cOptions.Use3SD)
                    {
                        PenDescriptor redPen = new(GrRed);
                        DrawControlLine(redPen, ymean + ysd * 3.0, "+3 SD");
                        DrawControlLine(redPen, ymean - ysd * 3.0, "-3 SD");
                    }
                }
            }

            EndVectorPlot();
            return new ParameterBag();
        }

        private void MaybeDrawControlLine(PenDescriptor pen, double? value, string suffix)
        {
            if (!value.HasValue)
                return;
            DrawControlLine(pen, value.Value, suffix);
        }

        private void DrawControlLine(PenDescriptor pen, double value, string suffix)
        {
            ControlOptions cOptions = (ControlOptions)Definition.ChartOptions;
            double x1 = XAxisCanvas + XExtCanvas;
            double y1 = ToCanvasY(value);
            DrawLineInCanvasCoordinates(pen, XAxisCanvas, y1, x1, y1);
            DrawStringLegendLC($"{Math.Round(value, cOptions.RightHandDecimalPlaces)} ({suffix})", x1 + RHS_LABEL_GAP, y1);
        }
    }
}
