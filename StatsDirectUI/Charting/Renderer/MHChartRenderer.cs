using Layout;
using StatsDirect.Charting.Scales;
using StatsDirect.Numerics;
using StatsDirect.Templates;
using StatsDirect.Utilities;
using System;
using System.Drawing;

namespace StatsDirect.Charting.Renderer
{
    class MHChartRenderer : AbstractForestishChartRenderer, IChartRenderer
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
            double absMin = double.PositiveInfinity;
            for (int i = 1; i <= options.k; i++)
            {
                if (odw[i] != Constant.MISSING)
                {
                    if (odw[i] > maxGw)
                        maxGw = odw[i];
                    gw[i] = odw[i];
                }
                if (odr[i] != Constant.MISSING && IncludeTable(options, i) && !double.IsInfinity(odr[i]))
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
                    if (Math.Abs(odr[i]) < absMin && odr[i] != 0.0)
                        absMin = Math.Abs(odr[i]);
                    if (Math.Abs(odrl[i]) < absMin && odrl[i] != 0.0 && odrl[i] != Constant.MISSING && !double.IsInfinity(odrl[i]))
                        absMin = Math.Abs(odrl[i]);
                    if (Math.Abs(odru[i]) < absMin && odru[i] != 0.0 && odru[i] != Constant.MISSING && !double.IsInfinity(odru[i]))
                        absMin = Math.Abs(odru[i]);
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
                    w = LegendWidthInCanvasCoordinates(RangeLabel(odr[i], odrl[i], odru[i], absMin));
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

            PenDescriptor ciPen = GetLinePen(studyMarkerType, true);
            PenDescriptor dotPen = GetMarkerPen(ChartPreferences.MarkerTypes[10]);
            int r = 0;
            double yc = 0;
            for (int i = options.k; i >= 1; i--)
            {
                r++;
                yc = (r + options.pbias - 0.5);
                if (odr[i] != Constant.MISSING && !double.IsInfinity(odr[i]) && IncludeTable(options.o, i))
                {
                    double xm = odr[i] <= 0 || odr[i] < axisScales.X.MinimumScaleValue
                        ? axisScales.X.MinimumScaleValue
                        : odr[i];
                    double xl = odrl[i] <= 0 || odrl[i] < axisScales.X.MinimumScaleValue || odrl[i] == Constant.MISSING || double.IsInfinity(odrl[i])
                        ? axisScales.X.MinimumScaleValue
                        : odrl[i];
                    double xr = double.IsInfinity(odru[i]) || odru[i] == Constant.MISSING || double.IsInfinity(odru[i])
                        ? axisScales.X.MaximumScaleValue
                        : odru[i] <= axisScales.X.MinimumScaleValue
                            ? axisScales.X.MinimumScaleValue
                            : odru[i];

                    // Weight blob.  Draw this first so that the line appears in front of it in the case of short lines (#994).
                    // #688: Make blob size proportional to sqrt(1/variance) rather than 1/variance
                    double blobSize = (5 + ToCanvasHeight(featureHeight * Math.Sqrt(gw[i] / maxGw))) * 0.7;
                    DrawMarkerInChartCoordinates(xm, yc, blobSize / 2, studyMarkerType);

                    // CI line
                    DrawLineInChartCoordinates(ciPen, xl, yc, xr, yc);

                    // Arrow ends if not plottable
                    if (odrl[i] <= 0 || options.lerr[i] || odrl[i] < orlmin || odrl[i] == Constant.MISSING || double.IsInfinity(odrl[i]))
                    {
                        DrawLineInCanvasCoordinates(ciPen, ToCanvasX(xl) + ToCanvasHeight(arrowWidth), ToCanvasY(yc) + ToCanvasHeight(arrowWidth), ToCanvasX(xl), ToCanvasY(yc));
                        DrawLineInCanvasCoordinates(ciPen, ToCanvasX(xl), ToCanvasY(yc), ToCanvasX(xl) + ToCanvasHeight(arrowWidth), ToCanvasY(yc) - ToCanvasHeight(arrowWidth));
                    }
                    if (options.uerr[i] || double.IsInfinity(odru[i]) || odru[i] == Constant.MISSING)
                    {
                        DrawLineInCanvasCoordinates(ciPen, ToCanvasX(xr) - ToCanvasHeight(arrowWidth), ToCanvasY(yc) + ToCanvasHeight(arrowWidth), ToCanvasX(xr), ToCanvasY(yc));
                        DrawLineInCanvasCoordinates(ciPen, ToCanvasX(xr), ToCanvasY(yc), ToCanvasX(xr) - ToCanvasHeight(arrowWidth), ToCanvasY(yc) - ToCanvasHeight(arrowWidth));
                    }
                    // Centre mark.  Draw this last so that it appears in front of the line.  Always black.
                    DrawMarkerInChartCoordinates(xm, yc, 2, MarkerShape.Circle, true, dotPen);

                    DrawStringLabel(options.title[i], XAxisCanvas - 15, ToCanvasY(yc), StringAlignment.Far, StringAlignment.Center);
                    DrawRangeLabelInChartCoordinates(odr[i], odrl[i], odru[i], absMin, yc);
                }
                else
                {
                    DrawStringLabel(options.title[i], XAxisCanvas - 15, ToCanvasY(yc), StringAlignment.Far, StringAlignment.Center);
                    DrawStringLabel("* (excluded)", XAxisCanvas + XExtCanvas + 10, ToCanvasY(yc), StringAlignment.Near, StringAlignment.Center);
                }
            }

            PlotNoEffectMarker(axisScales);

            if (options.pbias == 1)
                PlotPooledMarker(options.rmh, options.ll, options.ul, absMin, yc, options.cap);

            EndVectorPlot();
            return new ParameterBag();
        }

        protected override bool IncludeTable(MHOptions options, int i) => IncludeTable(options.o, i);
        private static bool IncludeTable(double[,] o, int i) => !(o[i, 1] == 0.0 && o[i, 2] == 0.0 || o[i, 3] == 0.0 && o[i, 4] == 0.0);
    }
}
