using Layout;
using StatsDirect.Charting.Scales;
using StatsDirect.Numerics;
using StatsDirect.Templates;
using StatsDirect.Utilities;
using System;
using System.Drawing;

namespace StatsDirect.Charting.Renderer
{
    class MHChartRenderer : AbstractChartRenderer, IChartRenderer
    {
        public MHChartRenderer(ChartDefinition definition, ICanvasFactory canvasFactory)
            : base(definition, canvasFactory)
        {
        }

        ScaleParameters IChartRenderer.GetScaleParameters()
        {
            return new ScaleParameters
            {
                X = new AxisScaleParameters { ScaleType = ScaleType.Log10 },
                Y = new AxisScaleParameters { ScaleType = ScaleType.Linear }
            };
        }

        ParameterBag IChartRenderer.Plot(ITemplateHost host, bool isForReturnedParametersOnly)
        {
            if (isForReturnedParametersOnly)
                return new ParameterBag();

            MHOptions options = (MHOptions)Definition.ChartOptions;
            ScaleHeight(options.k);

            double[] odw = options.odw;
            double[] odr = options.odr;
            double[] odrl = options.odrl;
            double[] odru = options.odru;

            double[] gw = new double[options.k + 1];
            double ormax = double.NegativeInfinity;
            double ormin = double.PositiveInfinity;
            double orumax = double.NegativeInfinity;
            double orlmin = double.PositiveInfinity;
            double maxGw = double.NegativeInfinity;
            double absmin = double.PositiveInfinity;
            for (int i = 1; i <= options.k; i++)
            {
                if (odw[i] != Constant.MISSING)
                {
                    if (odw[i] > maxGw)
                        maxGw = odw[i];
                    gw[i] = odw[i];
                }
                if (odr[i] != Constant.MISSING && include_table(options.o, i) && !double.IsInfinity(odr[i]))
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

            if (DataMaxX < options.rmh)
                DataMaxX = options.rmh;
            if (DataMaxX < options.ul && options.ul != Constant.MISSING && !double.IsInfinity(options.ul))
                DataMaxX = options.ul;
            if (DataMaxX < orumax && orumax != Constant.MISSING && !double.IsInfinity(orumax))
                DataMaxX = orumax;

            StartVectorPlot();
            double rgap = 0;
            double xtra = 0;
            // allow room for right hand labels of effect and CI
            double w = LegendWidthInCanvasCoordinates(ComboTi(options.cap)) + 30;
            if (w > xtra + XAxisCanvas)
                xtra = w - XAxisCanvas - AxisBigTick;
            for (int i = 1; i <= options.k; i++)
            {
                if (odr[i] != Constant.MISSING && !double.IsInfinity(odr[i]))
                {
                    w = LegendWidthInCanvasCoordinates(options.title[i]) + 30;
                    if (w > xtra + XAxisCanvas)
                        xtra = w - XAxisCanvas - AxisBigTick;
                    w = LegendWidthInCanvasCoordinates(Formatting.RoundMeta(odr[i], absmin) + " (" + Formatting.RoundMeta(odrl[i], absmin) + ", " + Formatting.RoundMeta(odru[i], absmin) + ")");
                    if (w > rgap)
                        rgap = w;
                }
            }
            AxisScales axisScales = LayoutChartAndDrawAxes(options.cap,
                new AxisDefinition(options.qid + " (" + Formatting.XRound(options.cco * 100, 1) + "% confidence interval" + ")", AxisMode.Scale, Definition.ScaleParameters.X.ScaleType) { ExtraSpaceBeforeAxisStarts = xtra, ExtraSpaceAfterAxisEnds = rgap },
                new AxisDefinition(null, AxisMode.None, ScaleType.Category),
                false, false);
            axisScales.Y = new CategoryAxisScale(options.k + options.pbias);
            DivY = options.k + options.pbias;
            OffY = YAxisCanvas;

            MarkerType studyMarkerType = new MarkerType
            {
                MarkerColor = ColorDescriptor.Gray,
                LineColor = ColorDescriptor.Black,
                IsMarkerFilled = true,
                MarkerShape = MarkerShape.Square,
                LineDashStyle = DashStyleDescriptor.Solid,
                Width = 1
            };
            MarkerType pooledMarkerType = new MarkerType
            {
                MarkerColor = ColorDescriptor.Gray,
                LineColor = ColorDescriptor.Black,
                IsMarkerFilled = true,
                MarkerShape = MarkerShape.Diamond,
                LineDashStyle = DashStyleDescriptor.Solid,
                Width = 1
            };

            PenDescriptor ciPen = GetLinePen(studyMarkerType, true);
            PenDescriptor dotPen = GetMarkerPen(ChartPreferences.MarkerTypes[10]);
            PenDescriptor pooledCiPen = GetLinePen(pooledMarkerType, true);
            PenDescriptor pooledEffectPen = GetLinePen(ChartPreferences.MarkerTypes[10], false);
            int r = 0;
            double txh = LabelHeightInCanvasCoordinates(options.title[1]);
            double realamin = axisScales.X.MinimumScaleValue;
            double realamax = axisScales.X.MaximumScaleValue;
            double yc = 0;
            for (int i = options.k; i >= 1; i--)
            {
                r++;
                double yctr = (r + options.pbias - 0.5) / DivY * YExtCanvas;
                double ytop = (r + options.pbias) / DivY * YExtCanvas;
                double y2 = (ytop - yctr) / 1.5;
                double y3 = (ytop - yctr) / 4;
                yc = OffY + yctr;
                double yt = OffY + yctr + y2;
                double yb = OffY + yctr - y2;
                if (odr[i] != Constant.MISSING && !double.IsInfinity(odr[i]) && include_table(options.o, i))
                {
                    double xm;
                    if (odr[i] <= 0 || odr[i] < realamin)
                        xm = XAxisCanvas;
                    else
                        xm = ToCanvasX(odr[i]);
                    double xl;
                    if (odrl[i] <= 0 || odrl[i] < realamin || odrl[i] == Constant.MISSING || double.IsInfinity(odrl[i]))
                        xl = XAxisCanvas;
                    else
                        xl = ToCanvasX(odrl[i]);
                    double xr;
                    if (double.IsInfinity(odru[i]) || odru[i] == Constant.MISSING || double.IsInfinity(odru[i]))
                        xr = ToCanvasX(realamax);
                    else
                        xr = odru[i] <= 0 ? OffX : ToCanvasX(odru[i]);

                    // Weight blob.  Draw this first so that the line appears in front of it in the case of short lines (#994).
                    // #688: Make blob size proportional to sqrt(1/variance) rather than 1/variance
                    double blobSize = (5 + Math.Abs(yt - yb) * Math.Sqrt(gw[i] / maxGw)) * 0.7;
                    DrawMarkerInCanvasCoordinates(xm, yc, blobSize / 2, studyMarkerType);

                    // CI line
                    DrawLineInCanvasCoordinates(ciPen, xl, yc, xr, yc);
                    // Arrow ends if not plottable
                    if (odrl[i] <= 0 || options.lerr[i] || odrl[i] < orlmin || odrl[i] == Constant.MISSING || double.IsInfinity(odrl[i]))
                    {
                        DrawLineInCanvasCoordinates(ciPen, xl + y3, yc + y3, xl, yc);
                        DrawLineInCanvasCoordinates(ciPen, xl, yc, xl + y3, yc - y3);
                    }
                    if (options.uerr[i] || double.IsInfinity(odru[i]) || odru[i] == Constant.MISSING)
                    {
                        DrawLineInCanvasCoordinates(ciPen, xr - y3, yc + y3, xr, yc);
                        DrawLineInCanvasCoordinates(ciPen, xr, yc, xr - y3, yc - y3);
                    }
                    // Centre mark.  Draw this last so that it appears in front of the line.  Always black.
                    DrawMarkerInCanvasCoordinates(xm, yc, 2, MarkerShape.Circle, true, dotPen);

                    DrawStringLabel(options.title[i], XAxisCanvas - 15, yc + txh / 2, StringAlignment.Far);
                    DrawStringLabel(Formatting.RoundMeta(odr[i], absmin) + " (" + Formatting.RoundMeta(odrl[i], absmin) + ", " + Formatting.RoundMeta(odru[i], absmin) + ")", XAxisCanvas + XExtCanvas + 10, yc + txh / 2, StringAlignment.Near);
                }
                else
                {
                    DrawStringLabel(options.title[i], XAxisCanvas - 15, yc + txh / 2, StringAlignment.Far);
                    DrawStringLabel("* (excluded)", XAxisCanvas + XExtCanvas + 10, yc + txh / 2, StringAlignment.Near);
                }
            }

            // zero effect marker
            switch (Definition.ScaleParameters.X.ScaleType)
            {
                case ScaleType.Linear:
                    if (axisScales.X.MinimumScaleValue <= 0 && axisScales.X.MaximumScaleValue >= 0)
                        DrawLineInChartCoordinates(GrBlack, 0, axisScales.Y.MinimumScaleValue, 0, axisScales.Y.MaximumScaleValue);
                    break;
                case ScaleType.Log10:
                case ScaleType.LogNatural:
                    if (axisScales.X.MinimumScaleValue <= 1 && axisScales.X.MaximumScaleValue >= 1)
                        DrawLineInChartCoordinates(GrBlack, 1, axisScales.Y.MinimumScaleValue, 1, axisScales.Y.MaximumScaleValue);
                    break;
            }

            if (options.pbias == 1)
            {
                // pooled marker
                double saveYc = yc;
                double yctr = 0.5 / DivY * YExtCanvas;
                double ytop = 1 / DivY * YExtCanvas;
                double y2 = (ytop - yctr) / 1.5;
                yc = OffY + yctr;
                double yt = OffY + yctr + y2;
                // yb = offy + yctr - Y2; 
                DrawMarkerInCanvasCoordinates(ToCanvasX(options.rmh), yc, y2, pooledMarkerType);
                DrawLineInCanvasCoordinates(pooledCiPen, ToCanvasX(options.ul), yc, ToCanvasX(options.ll), yc);
                // pooled effect marker
                DrawLineInCanvasCoordinates(pooledEffectPen, ToCanvasX(options.rmh), saveYc, ToCanvasX(options.rmh), yt);
                // pool label
                DrawStringLabel(ComboTi(options.cap), XAxisCanvas - 15, yc + txh / 2, StringAlignment.Far);
                DrawStringLabel(Formatting.RoundMeta(options.rmh, absmin) + " (" + Formatting.RoundMeta(options.ll, absmin) + ", " + Formatting.RoundMeta(options.ul, absmin) + ")", XAxisCanvas + XExtCanvas + 10, yc + txh / 2, StringAlignment.Near);
                // xaxis label
            }

            EndVectorPlot();
            return new ParameterBag();
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
            return !(o[i, 1] == 0.0 && o[i, 2] == 0.0 || o[i, 3] == 0.0 && o[i, 4] == 0.0);
        }
    }
}
