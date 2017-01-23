using StatsDirect.Numerics;
using StatsDirect.Templates;
using StatsDirect.Utilities;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace StatsDirect.Charting
{
    class ForestChartRenderer: AbstractChartRenderer
    {
        public ForestChartRenderer(ChartDefinition definition)
            : base(definition)
        {
        }

        public override ScaleParameters GetScaleParameters()
        {
            ForestOptions fOptions = ((ForestOptions)(definition.ChartOptions));
            int k = fOptions.k;
            double[] odr = fOptions.OddsRatios;
            double[] odrl = fOptions.OddsRatioLcis;
            double[] odru = fOptions.OddsRatioUcis;

            DataMaxX = double.MinValue;
            DataMinX = double.MaxValue;
            DataMinGreaterThanZeroX = double.MaxValue;

            for (int i = 0; i < k; i++)
            {
                if (odr[i] != Constant.MISSING && !double.IsInfinity(odr[i]))
                {
                    if (odr[i] > DataMaxX)
                        DataMaxX = odr[i];
                    if (odr[i] < DataMinX)
                        DataMinX = odr[i];
                    if (odr[i] < DataMinGreaterThanZeroX)
                        DataMinGreaterThanZeroX = odr[i];
                    if (odrl[i] > odru[i])
                    {
                        double tmp = odrl[i];
                        odrl[i] = odru[i];
                        odru[i] = tmp;
                    }
                    if (odrl[i] < DataMinX && odrl[i] > 0 && odrl[i] != Constant.MISSING && !double.IsInfinity(odrl[i]))
                        DataMinX = odrl[i];
                    if (odru[i] > DataMaxX && odru[i] != Constant.MISSING && !double.IsInfinity(odru[i]))
                        DataMaxX = odru[i];
                }
            }

            bool shouldDrawLine = DataMinX <= 0 || (null != definition && definition.HasScaleParameters && definition.ScaleParameters.X.MarkerLineValue.HasValue);
            double lineX = (null != definition && definition.HasScaleParameters && definition.ScaleParameters.X.MarkerLineValue.HasValue) ? definition.ScaleParameters.X.MarkerLineValue.Value : 0;
            if (shouldDrawLine)
            {
                if (DataMaxX < lineX)
                    DataMaxX = lineX;
                if (DataMinX > lineX)
                    DataMinX = lineX;
            }

            return new ScaleParameters
            {
                X = { AllowedScaleTypes = new[] { ScaleType.Linear, ScaleType.LogNatural }, Min = DataMinX, MinGreaterThanZero = DataMinGreaterThanZeroX, Max = DataMaxX },
                Y = { AllowedScaleTypes = new[] { ScaleType.Category } }
            };
        }

        public override ParameterBag Plot(ITemplateHost host)
        {
            int pbias = 0;

            ForestOptions fOptions = ((ForestOptions)(definition.ChartOptions));
            MarkerType studyMarkerType = fOptions.MarkerTypes[0];
            MarkerType pooledMarkerType = fOptions.MarkerTypes[1];
            double[] gn = fOptions.gn;
            int k = fOptions.k;
            double[] odr = fOptions.OddsRatios;
            double[] odrl = fOptions.OddsRatioLcis;
            double[] odru = fOptions.OddsRatioUcis;
            double[] pg = fOptions.pg;
            string[] title = fOptions.Titles;

            if (k > 10)
            {
                double scaleYAxis = 1.0 + (k - 10.0) / 20.0;
                if (scaleYAxis > 5)
                    scaleYAxis = 5;
                imageHeight = (int)Math.Ceiling(scaleYAxis * DEFAULT_METAFILE_HEIGHT);
            }

            int kok = 0;
            DataMaxX = double.NegativeInfinity;
            DataMinX = double.PositiveInfinity;
            double max_gn = double.NegativeInfinity;

            for (int i = 0; i < k; i++)
            {
                if (pg == null || pg[i] == 0)
                {
                    if (gn[i] != Constant.MISSING && !double.IsInfinity(gn[i]) && gn[i] > max_gn)
                        max_gn = gn[i];
                }
                if (odr[i] != Constant.MISSING && !double.IsInfinity(odr[i]))
                {
                    kok++;
                    if (odr[i] > DataMaxX)
                        DataMaxX = odr[i];
                    if (odr[i] < DataMinX && odr[i] > 0)
                        DataMinX = odr[i];
                    if (odrl[i] > odru[i])
                    {
                        double tmp = odrl[i];
                        odrl[i] = odru[i];
                        odru[i] = tmp;
                    }
                    if (odrl[i] < DataMinX && odrl[i] > 0 && odrl[i] != Constant.MISSING && !double.IsInfinity(odrl[i]))
                        DataMinX = odrl[i];
                    if (odru[i] > DataMaxX && odru[i] != Constant.MISSING && !double.IsInfinity(odru[i]))
                        DataMaxX = odru[i];
                }
            }

            double absmin = double.PositiveInfinity;
            for (int i = 0; i < k; i++)
            {
                if (Math.Abs(odr[i]) < absmin && odr[i] != 0.0 && odr[i] != Constant.MISSING && !double.IsInfinity(odr[i]))
                    absmin = Math.Abs(odr[i]);
                if (Math.Abs(odrl[i]) < absmin && odrl[i] != 0.0 && odrl[i] != Constant.MISSING && !double.IsInfinity(odrl[i]))
                    absmin = Math.Abs(odrl[i]);
                if (Math.Abs(odru[i]) < absmin && odru[i] != 0.0 && odru[i] != Constant.MISSING && !double.IsInfinity(odru[i]))
                    absmin = Math.Abs(odru[i]);
            }

            int decimalPlaces = fOptions.EffectSizeAndIntervalDecimalPlaces;

            ScaleType xlogscale = definition.ScaleParameters.X.ScaleType;
            bool isLogScale = (xlogscale == ScaleType.Log10 || xlogscale == ScaleType.LogNatural);

            // Determine whether to draw a vertical line and, if so, where; ensure it is within our scale.
            bool shouldDrawLine = DataMinX <= 0 || (null != definition && definition.HasScaleParameters && definition.ScaleParameters.X.MarkerLineValue.HasValue);
            double lineX = (null != definition && definition.HasScaleParameters && definition.ScaleParameters.X.MarkerLineValue.HasValue) ? definition.ScaleParameters.X.MarkerLineValue.Value : 0;
            if (shouldDrawLine)
            {
                if (DataMaxX < lineX)
                    DataMaxX = lineX;
                if (DataMinX > lineX)
                    DataMinX = lineX;
                if (null != definition && definition.HasScaleParameters)
                {
                    if (definition.ScaleParameters.X.Max < lineX)
                        definition.ScaleParameters.X.Max = lineX;
                    if (definition.ScaleParameters.X.Min > lineX)
                        definition.ScaleParameters.X.Min = lineX;
                }
            }

            int tics = 0;
            double[] tic = null;
            double realamin = 0;
            double realamax = 0;
            if (isLogScale)
            {
                tics = 1;
                tic = new double[tics + 1];
                CreateRatioLogScale(out tics, ref tic, ref DataMinX, ref DataMaxX, out realamin, out realamax);
            }

            StartVectorPlot();

            SetFontsAndThicknessesFromOptions(fOptions);

            double rgap = 0;
            double xtra = 0;
            //  Allow room for right hand labels of effect and CI
            for (int i = 0; i < k; i++)
            {
                if (odr[i] != Constant.MISSING && !double.IsInfinity(odr[i]))
                {
                    float titleWidth = TitleWidthInCanvasCoordinates(title[i]) + 30;
                    if (titleWidth > xtra + xAxisCanvas)
                        xtra = titleWidth - xAxisCanvas - 5;
                    string rhs = Formatting.RoundMeta(odr[i], absmin, decimalPlaces) + " (" + Formatting.RoundMeta(odrl[i], absmin, decimalPlaces) + ", " + Formatting.RoundMeta(odru[i], absmin, decimalPlaces) + ")";
                    float rhsWidth = LegendWidthInCanvasCoordinates(rhs);
                    if (rhsWidth > rgap)
                        rgap = rhsWidth;
                }
            }
            float w = TitleWidthInCanvasCoordinates(combo_ti(fOptions.Title)) + 30;
            if (w > xtra + xAxisCanvas)
                xtra = w - xAxisCanvas - 5;
            if (isLogScale)
                DrawAxesOrEnlargeCanvas(fOptions.Title, new Axis(fOptions.XAxisTitle, AxisMode.LineOnly, ScaleType.Linear) { ExtraSpaceAfterAxisEnds = rgap }, new Axis(null, AxisMode.None, xtra, ScaleType.Linear), false, false);
            else
                DrawAxesOrEnlargeCanvas(fOptions.Title, new Axis(fOptions.XAxisTitle, AxisMode.Scale, ScaleType.NotSet) { ExtraSpaceAfterAxisEnds = rgap }, new Axis(null, AxisMode.None, xtra, ScaleType.NotSet), false, false);

            divx = DataMaxX - DataMinX;
            offx = -(DataMinX / divx * xExtCanvas) + xAxisCanvas;
            divy = kok + pbias;
            offy = yAxisCanvas;

            if (isLogScale)
            {
                double lastXM = 0;
                for (int i = 1; i <= tics; i++)
                {
                    //  The use of tic is safe, as this code is only run if logscale, which is where tic is set above.
                    if (tic[i] >= realamin && tic[i] <= realamax)
                    {
                        // force ToCanvas to use log on a linear canvas because scatter plot etc. uses different scaling: TODO
                        double xm = ToCanvasX(Math.Log(tic[i]), ScaleType.Linear);
                        string lab = tic[i].ToString("G");
                        if (lastXM == 0 || MeasureStringInCanvasCoordinates(lab, axisLabelFont).Width < xm - lastXM)
                        {
                            DrawStringLabel(lab, xm, yAxisCanvas - 12, StringAlignment.Center);
                            DrawLineInCanvasCoordinates(axisPen, xm, yAxisCanvas - 12, xm, yAxisCanvas);
                            lastXM = xm;
                        }
                    }
                }
            }

            int r = 0;
            double botlim = isLogScale ? realamin : double.MinValue;

            using (Pen effectTenPen = GetLinePen(SharedMarkerTypes[10], false),
                ciPen = GetLinePen(studyMarkerType, true),
                dotPen = GetMarkerPen(SharedMarkerTypes[10]),
                pooledCiPen = GetLinePen(pooledMarkerType, true))
            {
                double yt = 0;
                for (int i = k - 1; i >= 0; i--)
                {
                    if (odr[i] != Constant.MISSING && !double.IsInfinity(odr[i]))
                    {
                        r++;
                        double yctr = (r + pbias - 0.5) / divy * yExtCanvas;
                        double ytop = (r + pbias) / divy * yExtCanvas;
                        double xm = odr[i] < botlim ? xAxisCanvas : ToCanvasX(isLogScale ? Math.Log(odr[i]) : odr[i], ScaleType.Linear);
                        double xl = odrl[i] < botlim ? xAxisCanvas : ToCanvasX(isLogScale ? Math.Log(odrl[i]) : odrl[i], ScaleType.Linear);
                        double xr = ToCanvasX(isLogScale ? Math.Log(odru[i]) : odru[i], ScaleType.Linear);
                        double y2 = (ytop - yctr) / 1.5;
                        double y3 = (ytop - yctr) / 4;
                        double yc = offy + yctr;
                        yt = offy + yctr + y2;
                        double yb = offy + yctr - y2;
                        if (pg == null || pg[i] == 0)
                        {
                            // Weight blob.  Draw this first so that the line appears in front of it in the case of short lines (#994).
                            // #688: Make blob size proportional to sqrt(1/variance) rather than 1/variance
                            double blobSize = (5 + Math.Abs(yt - yb) * (Math.Sqrt(gn[i] / max_gn))) * 0.7;
                            DrawMarkerInCanvasCoordinates(xm, yc, blobSize / 2, studyMarkerType);

                            // CI line
                            DrawLineInCanvasCoordinates(ciPen, xl, yc, xr, yc);
                            // Arrow ends if not plottable
                            if ((odrl[i] <= 0 & isLogScale) || odrl[i] == Constant.MISSING || double.IsInfinity(odrl[i]))
                            {
                                DrawLineInCanvasCoordinates(ciPen, xl + y3, yc + y3, xl, yc);
                                DrawLineInCanvasCoordinates(ciPen, xl, yc, xl + y3, yc - y3);
                            }
                            if (odru[i] == Constant.MISSING || double.IsInfinity(odru[i]))
                            {
                                DrawLineInCanvasCoordinates(ciPen, xr - y3, yc + y3, xr, yc);
                                DrawLineInCanvasCoordinates(ciPen, xr, yc, xr - y3, yc - y3);
                            }

                            // Centre mark.  If drawn, draw this last so that it appears in front of the line.  Always black.
                            if (fOptions.MarkCentres)
                                DrawMarkerInCanvasCoordinates(xm, yc, 2, MarkerShape.Circle, true, dotPen);
                        }
                        else
                        {
                            // Pooled effect
                            DrawMarkerInCanvasCoordinates(xm, yc, y2, pooledMarkerType);
                            DrawLineInCanvasCoordinates(pooledCiPen, xr, yc, xl, yc);
                            if (pg[i] < 0)
                            {
                                // pooled effect marker
                                DrawLineInCanvasCoordinates(effectTenPen, xm, yt, xm, ToCanvasY(k + pbias - 0.5));
                            }

                        }
                        AxisDrawStringAtAngleRM(title[i], xAxisCanvas - 15, yc, definition.ScaleParameters.Y.LabelDirection);
                        DrawStringLabel(Formatting.RoundMeta(odr[i], absmin, decimalPlaces) + " (" + Formatting.RoundMeta(odrl[i], absmin, decimalPlaces) + ", " + Formatting.RoundMeta(odru[i], absmin, decimalPlaces) + ")", xAxisCanvas + xExtCanvas + 10, yc, StringAlignment.Near, StringAlignment.Center);
                    }
                }

                if (shouldDrawLine)
                {
                    // no effect line, which is effectively part of the axis so uses the axis pen
                    double xm = ToCanvasX(lineX, ScaleType.Linear);
                    DrawLineInCanvasCoordinates(axisPen, xm, yt, xm, yAxisCanvas);
                }
            }

            EndVectorPlot();
            return new ParameterBag();
        }
    }
}
