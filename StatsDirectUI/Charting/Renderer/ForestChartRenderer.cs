using System;
using System.Drawing;
using StatsDirect.Numerics;
using StatsDirect.Templates;
using StatsDirect.Utilities;

namespace StatsDirect.Charting.Renderer
{
    class ForestChartRenderer: AbstractChartRenderer, IChartRenderer
    {
        public ForestChartRenderer(ChartDefinition definition, ICanvasFactory canvasFactory)
            : base(definition, canvasFactory)
        {
        }

        ScaleParameters IChartRenderer.GetScaleParameters()
        {
            ForestOptions fOptions = (ForestOptions)Definition.ChartOptions;
            DoubleArraysAndBooleans copiesRemovingMissingRows = Numerics.Utilities.RemoveMissingRows(new[] { fOptions.OddsRatios, fOptions.OddsRatioLcis, fOptions.OddsRatioUcis }, 0, fOptions.k, 0);
            double[] odr = copiesRemovingMissingRows.ArraysWithMissingRowsRemoved[0];
            double[] odrl = copiesRemovingMissingRows.ArraysWithMissingRowsRemoved[1];
            double[] odru = copiesRemovingMissingRows.ArraysWithMissingRowsRemoved[2];
            int k = odr.Length;

            DataMaxX = double.MinValue;
            DataMinX = double.MaxValue;
            DataMinGreaterThanZeroX = double.MaxValue;

            for (int i = 0; i < k; i++)
            {
                if (odr[i] != Constant.MISSING && !double.IsInfinity(odr[i]))
                {
                    // Odds ratio
                    if (odr[i] > DataMaxX)
                        DataMaxX = odr[i];
                    if (odr[i] < DataMinX)
                        DataMinX = odr[i];
                    if (odr[i] > 0 && odr[i] < DataMinGreaterThanZeroX)
                        DataMinGreaterThanZeroX = odr[i];

                    // Swap LCI and UCI if the user's entered them the wrong way round
                    if (odrl[i] > odru[i])
                    {
                        double tmp = odrl[i];
                        odrl[i] = odru[i];
                        odru[i] = tmp;
                    }
                    if (odrl[i] != Constant.MISSING && !double.IsInfinity(odrl[i]))
                    {
                        if (odrl[i] > DataMaxX)
                            DataMaxX = odrl[i];
                        if (odrl[i] < DataMinX)
                            DataMinX = odrl[i];
                        if (odrl[i] > 0 && odrl[i] < DataMinGreaterThanZeroX)
                            DataMinGreaterThanZeroX = odrl[i];
                    }
                    if (odru[i] != Constant.MISSING && !double.IsInfinity(odru[i]))
                    {
                        if (odru[i] > DataMaxX)
                            DataMaxX = odru[i];
                        if (odru[i] < DataMinX)
                            DataMinX = odru[i];
                        if (odru[i] > 0 && odru[i] < DataMinGreaterThanZeroX)
                            DataMinGreaterThanZeroX = odru[i];
                    }
                }
            }

            bool shouldDrawLine = DataMinX <= 0 || null != Definition && Definition.HasScaleParameters && Definition.ScaleParameters.X.MarkerLineValue.HasValue;
            double lineX = null != Definition && Definition.HasScaleParameters && Definition.ScaleParameters.X.MarkerLineValue.HasValue ? Definition.ScaleParameters.X.MarkerLineValue.Value : 0;
            if (shouldDrawLine)
            {
                if (DataMaxX < lineX)
                    DataMaxX = lineX;
                if (DataMinX > lineX)
                    DataMinX = lineX;
            }

            return new ScaleParameters
            {
                X = { AllowedScaleTypes = new[] { ScaleType.Linear, /* ScaleType.LogNatural, */ ScaleType.Log10 }, Min = DataMinX, MinGreaterThanZero = DataMinGreaterThanZeroX, Max = DataMaxX },
                Y = { AllowedScaleTypes = new[] { ScaleType.Category } }
            };
        }

        ParameterBag IChartRenderer.Plot(ITemplateHost host)
        {
            int pbias = 0;

            ForestOptions fOptions = (ForestOptions)Definition.ChartOptions;
            MarkerType studyMarkerType = fOptions.MarkerTypes[0];
            MarkerType pooledMarkerType = fOptions.MarkerTypes[1];

            DoubleArraysAndBooleans copiesRemovingMissingRows = Numerics.Utilities.RemoveMissingRows(new[] { fOptions.OddsRatios, fOptions.OddsRatioLcis, fOptions.OddsRatioUcis, fOptions.gn, fOptions.pg }, 0, fOptions.k, 0);
            double[] odr = copiesRemovingMissingRows.ArraysWithMissingRowsRemoved[0];
            double[] odrl = copiesRemovingMissingRows.ArraysWithMissingRowsRemoved[1];
            double[] odru = copiesRemovingMissingRows.ArraysWithMissingRowsRemoved[2];
            double[] gn = copiesRemovingMissingRows.ArraysWithMissingRowsRemoved[3];
            double[] pg = copiesRemovingMissingRows.ArraysWithMissingRowsRemoved[4];
            int k = odr.Length;

            string[] title = Numerics.Utilities.CopyValidRows(fOptions.Titles, copiesRemovingMissingRows.ValidRowsInOriginal, 0, fOptions.k, 0, k);

            ScaleHeight(k);

            int kok = 0;
            DataMaxX = double.NegativeInfinity;
            DataMinX = double.PositiveInfinity;
            DataMinGreaterThanZeroX = double.PositiveInfinity;
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
                    if (odr[i] > 0 && odr[i] < DataMinGreaterThanZeroX)
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
            DataMinGreaterThanZeroX = DataMinX;

            int decimalPlaces = fOptions.EffectSizeAndIntervalDecimalPlaces;

            // Determine whether to draw a vertical line and, if so, where; ensure it is within our scale.
            bool shouldDrawLine = DataMinX <= 0 || null != Definition && Definition.HasScaleParameters && Definition.ScaleParameters.X.MarkerLineValue.HasValue;
            double lineX = null != Definition && Definition.HasScaleParameters && Definition.ScaleParameters.X.MarkerLineValue.HasValue ? Definition.ScaleParameters.X.MarkerLineValue.Value : 0;
            if (shouldDrawLine)
            {
                if (DataMaxX < lineX)
                    DataMaxX = lineX;
                if (DataMinX > lineX)
                    DataMinX = lineX;
                if (null != Definition && Definition.HasScaleParameters)
                {
                    if (Definition.ScaleParameters.X.Max < lineX)
                        Definition.ScaleParameters.X.Max = lineX;
                    if (Definition.ScaleParameters.X.Min > lineX)
                        Definition.ScaleParameters.X.Min = lineX;
                }
            }

            StartVectorPlot(fOptions);

            double rgap = 0;
            double xtra = 0;
            //  Allow room for right hand labels of effect and CI
            for (int i = 0; i < k; i++)
            {
                if (odr[i] != Constant.MISSING && !double.IsInfinity(odr[i]))
                {
                    float titleWidth = LegendWidthInCanvasCoordinates(title[i]);
                    if (titleWidth > xtra)
                        xtra = titleWidth;
                    string rhs = Formatting.RoundMeta(odr[i], absmin, decimalPlaces) + " (" + Formatting.RoundMeta(odrl[i], absmin, decimalPlaces) + ", " + Formatting.RoundMeta(odru[i], absmin, decimalPlaces) + ")";
                    float rhsWidth = LegendWidthInCanvasCoordinates(rhs);
                    if (rhsWidth > rgap)
                        rgap = rhsWidth;
                }
            }
            AxisScales axisScales = LayoutChartAndDrawAxes(fOptions.Title,
                new AxisDefinition(fOptions.XAxisTitle, AxisMode.Scale, Definition.ScaleParameters.X.ScaleType) { ExtraSpaceBeforeAxisStarts = xtra, ExtraSpaceAfterAxisEnds = rgap },
                new AxisDefinition(null, AxisMode.None, ScaleType.NotSet),
                false, false);
            axisScales.Y = new CategoryAxisScale(k + pbias);
            DivY = kok + pbias;
            OffY = YAxisCanvas;

            int r = 0;

            using (Pen effectTenPen = GetLinePen(ChartPreferences.MarkerTypes[10], false),
                ciPen = GetLinePen(studyMarkerType, true),
                dotPen = GetMarkerPen(ChartPreferences.MarkerTypes[10]),
                pooledCiPen = GetLinePen(pooledMarkerType, true))
            {
                double yt = 0;
                for (int i = k - 1; i >= 0; --i)
                {
                    if (odr[i] != Constant.MISSING && !double.IsInfinity(odr[i]))
                    {
                        r++;
                        double yctr = ToCanvasHeight(r + pbias - 0.5);
                        double ytop = ToCanvasHeight(r + pbias);
                        double xm = ToCanvasX(Math.Max(odr[i], axisScales.X.MinimumScaleValue));
                        double xl = ToCanvasX(Math.Max(odrl[i], axisScales.X.MinimumScaleValue));
                        double xr = ToCanvasX(odru[i]);
                        double y2 = (ytop - yctr) / 1.5;
                        double y3 = (ytop - yctr) / 4;
                        double yc = OffY + yctr;
                        yt = OffY + yctr + y2;
                        double yb = OffY + yctr - y2;
                        if (pg == null || pg[i] == 0)
                        {
                            // Weight blob.  Draw this first so that the line appears in front of it in the case of short lines (#994).
                            // #688: Make blob size proportional to sqrt(1/variance) rather than 1/variance
                            double blobSize = (5 + Math.Abs(yt - yb) * Math.Sqrt(gn[i] / max_gn)) * 0.7;
                            DrawMarkerInCanvasCoordinates(xm, yc, blobSize / 2, studyMarkerType);

                            // CI line
                            DrawLineInCanvasCoordinates(ciPen, xl, yc, xr, yc);
                            // Arrow ends if not plottable
                            if (odrl[i] < axisScales.X.MinimumScaleValue || odrl[i] == Constant.MISSING || double.IsInfinity(odrl[i]))
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
                        AxisDrawStringAtAngleRM(title[i], XAxisCanvas - 15, yc, Definition.ScaleParameters.Y.LabelDirection);
                        DrawStringLabel(Formatting.RoundMeta(odr[i], absmin, decimalPlaces) + " (" + Formatting.RoundMeta(odrl[i], absmin, decimalPlaces) + ", " + Formatting.RoundMeta(odru[i], absmin, decimalPlaces) + ")", XAxisCanvas + XExtCanvas + 10, yc, StringAlignment.Near, StringAlignment.Center);
                    }
                }

                if (shouldDrawLine)
                {
                    // no effect line, which is effectively part of the axis so uses the axis pen
                    DrawLineInCanvasCoordinates(AxisPen, ToCanvasX(lineX), yt, ToCanvasX(lineX), ToCanvasY(axisScales.Y.MinimumScaleValue));
                }
            }

            EndVectorPlot();
            return new ParameterBag();
        }
    }
}
