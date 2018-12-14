using Layout;
using StatsDirect.Charting.Scales;
using StatsDirect.Numerics;
using StatsDirect.Templates;
using StatsDirect.Utilities;
using System;
using System.Drawing;

namespace StatsDirect.Charting.Renderer
{
    class MHRDChartRenderer : AbstractChartRenderer, IChartRenderer
    {
        public MHRDChartRenderer(ChartDefinition definition, ICanvasFactory canvasFactory)
            : base(definition, canvasFactory)
        {
        }

        ScaleParameters IChartRenderer.GetScaleParameters()
        {
            return new ScaleParameters
            {
                X = new AxisScaleParameters { ScaleType = ScaleType.Linear },
                Y = new AxisScaleParameters { ScaleType = ScaleType.Linear }
            };
        }

        ParameterBag IChartRenderer.Plot(ITemplateHost host, bool isForReturnedParametersOnly)
        {
            if (isForReturnedParametersOnly)
                return new ParameterBag();

            MHOptions options = (MHOptions)Definition.ChartOptions;
            double[] odw = options.odw;
            double[] odr = options.odr;
            double[] odrl = options.odrl;
            double[] odru = options.odru;

            ScaleHeight(options.k);

            StartVectorPlot();

            double[] gw = new double[options.k + 1];
            double orMax = double.NegativeInfinity;
            double orMin = double.PositiveInfinity;
            double oruMax = double.NegativeInfinity;
            double orlMin = double.PositiveInfinity;
            double maxGw = double.NegativeInfinity;
            double absMin = double.PositiveInfinity;
            for (int i = 1; i <= options.k; i++)
            {
                if (odw[i] != Constant.MISSING)
                {
                    if (odw[i] > maxGw)
                        maxGw = odw[i];
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
            if (DataMaxX < options.rmh)
                DataMaxX = options.rmh;
            if (DataMaxX < options.ul && options.ul != Constant.MISSING)
                DataMaxX = options.ul;
            if (DataMaxX < oruMax && oruMax != Constant.MISSING)
                DataMaxX = oruMax;
            if (DataMinX > options.rmh)
                DataMinX = options.rmh;
            if (DataMinX > options.ll && options.ll != Constant.MISSING)
                DataMinX = options.ll;
            if (DataMinX > orlMin && orlMin != Constant.MISSING)
                DataMinX = orlMin;

            ILinearAxisScale axisScale = (ILinearAxisScale)AxisScalerFactory.AxisScalerFor(ScaleType.Linear).QAxis(DataMinX, 0, DataMaxX, false, false);
            DataMinX = axisScale.MinimumScaleValue;
            DataMaxX = axisScale.MaximumScaleValue;

            double rgap = 0;
            double xtra = 0;
            // allow room for right hand labels of effect and CI
            for (int i = 1; i <= options.k; i++)
            {
                if (odr[i] != Constant.MISSING)
                {
                    double w = LabelWidthInCanvasCoordinates(options.title[i]) + 30;
                    if (w > xtra + XAxisCanvas)
                        xtra = w - XAxisCanvas - 5;
                    w = LabelWidthInCanvasCoordinates(Formatting.RoundMeta(odr[i], absMin) + " (" + Formatting.RoundMeta(odrl[i], absMin) + ", " + Formatting.RoundMeta(odru[i], absMin) + ")");
                    if (w > rgap)
                        rgap = w;
                }
            }
            AxisScales axisScales = LayoutChartAndDrawAxes(options.cap,
                new AxisDefinition(options.qid + " (" + Formatting.XRound(options.cco * 100, 1) + "% confidence interval" + ")", AxisMode.Scale, ScaleType.Linear) { ExtraSpaceBeforeAxisStarts = xtra, ExtraSpaceAfterAxisEnds = rgap },
                new AxisDefinition(null, AxisMode.None, ScaleType.Linear),
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

            PenDescriptor tenPenTrue = GetLinePen(ChartPreferences.MarkerTypes[10], true);
            PenDescriptor dotPen = GetMarkerPen(ChartPreferences.MarkerTypes[10]);
            PenDescriptor tenPenFalse = GetLinePen(ChartPreferences.MarkerTypes[10], false);
            int r = 0;
            double yc = 0;
            double yt = 0;
            double txh = LabelHeightInCanvasCoordinates(options.title[1]);
            for (int i = options.k; i >= 1; i--)
            {
                r++;
                double yctr = (r + options.pbias - 0.5) / DivY * YExtCanvas;
                double ytop = (r + options.pbias) / DivY * YExtCanvas;
                double y2 = (ytop - yctr) / 1.5;
                yc = OffY + yctr;
                yt = OffY + yctr + y2;
                double yb = OffY + yctr - y2;
                if (odr[i] != Constant.MISSING)
                {
                    double xl = odrl[i] == Constant.MISSING ? XAxisCanvas : ToCanvasX(odrl[i]);
                    double xr;
                    if (odru[i] == double.PositiveInfinity || odru[i] == Constant.MISSING)
                        xr = XAxisCanvas + XExtCanvas;
                    else
                        xr = ToCanvasX(odru[i]);
                    // Weight blob.  Draw this first so that the line appears in front of it in the case of short lines (#994).
                    // #688: Make blob size proportional to sqrt(1/variance) rather than 1/variance
                    double blobSize = (5 + Math.Abs(yt - yb) * Math.Sqrt(gw[i] / maxGw)) * 0.7;
                    DrawMarkerInCanvasCoordinates(ToCanvasX(odr[i]), yc, blobSize / 2, studyMarkerType);

                    // CI line
                    DrawLineInCanvasCoordinates(tenPenTrue, xl, yc, xr, yc);

                    // Arrow ends if not plottable
                    if (options.lerr[i])
                    {
                        DrawLineInCanvasCoordinates(tenPenTrue, xl + y2, yc + y2, xl, yc);
                        DrawLineInCanvasCoordinates(tenPenTrue, xl, yc, xl + y2, yc - y2);
                    }
                    if (options.uerr[i])
                    {
                        DrawLineInCanvasCoordinates(tenPenTrue, xr - y2, yc + y2, xr, yc);
                        DrawLineInCanvasCoordinates(tenPenTrue, xr, yc, xr - y2, yb - y2);
                    }
                    // Centre mark.  Draw this last so that it appears in front of the line.  Always black.
                    DrawMarkerInCanvasCoordinates(ToCanvasX(odr[i]), yc, 2, MarkerShape.Circle, true, dotPen);

                    DrawStringLabel(options.title[i], XAxisCanvas - 15, yc + txh / 2, StringAlignment.Far);
                    DrawStringLabel(Formatting.RoundMeta(odr[i], absMin) + " (" + Formatting.RoundMeta(odrl[i], absMin) + ", " + Formatting.RoundMeta(odru[i], absMin) + ")", XAxisCanvas + XExtCanvas + 10, yc + txh / 2, StringAlignment.Near);
                }
                else
                {
                    DrawStringLabel(options.title[i], XAxisCanvas - 15, yc + txh / 2, StringAlignment.Far);
                    DrawStringLabel("* (excluded)", XAxisCanvas + XExtCanvas + 10, yc + txh / 2, StringAlignment.Near);
                }
            }

            if (axisScales.X.MinimumScaleValue <= 0 && axisScales.X.MaximumScaleValue >= 0)
            {
                //  no effect marker
                DrawLineInCanvasCoordinates(tenPenTrue, OffX, yt, OffX, YAxisCanvas - 12);
            }

            if (options.pbias == 1)
            {
                double saveYc = yc;
                double yctr = 0.5 / DivY * YExtCanvas;
                double ytop = 1 / DivY * YExtCanvas;
                double y2 = (ytop - yctr) / 1.5;
                yc = OffY + yctr;
                yt = OffY + yctr + y2;
                DrawDiamondInCanvasCoordinates(tenPenTrue, ToCanvasX(options.rmh), yc, y2 * 2, false);
                DrawLineInCanvasCoordinates(tenPenTrue, ToCanvasX(options.ul), yc, ToCanvasX(options.ll), yc);
                // pooled effect marker
                DrawLineInCanvasCoordinates(tenPenFalse, ToCanvasX(options.rmh), saveYc, ToCanvasX(options.rmh), yt);
                // pool label
                DrawStringLabel(ComboTi(options.cap), XAxisCanvas - 15, yc + txh / 2, StringAlignment.Far);
                DrawStringLabel(Formatting.RoundMeta(options.rmh, absMin) + " (" + Formatting.RoundMeta(options.ll, absMin) + ", " + Formatting.RoundMeta(options.ul, absMin) + ")", XAxisCanvas + XExtCanvas + 10, yc + txh / 2, StringAlignment.Near);
                // x axis text
            }

            EndVectorPlot();
            return new ParameterBag();
        }
    }
}
