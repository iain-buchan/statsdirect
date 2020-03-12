using Layout;
using StatsDirect.Charting.Scales;
using StatsDirect.Numerics;
using StatsDirect.Templates;
using StatsDirect.Utilities;
using System;
using System.Drawing;
using System.Linq;

namespace StatsDirect.Charting.Renderer
{
    class MHRDChartRenderer : AbstractForestishChartRenderer, IChartRenderer
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
            ScaleHeight(options.k);

            double[] odw = options.odw;
            double[] odr = options.odr;
            double[] odrl = options.odrl;
            double[] odru = options.odru;

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

            DataMaxX = new[] { orMax, options.rmh, options.ul, oruMax }.MaxIgnoringMissingAndInfinities();
            DataMinX = new[] { orMin, options.rmh, options.ll, orlMin }.MinIgnoringMissingAndInfinities();

            StartVectorPlot();

            ILinearAxisScale axisScale = (ILinearAxisScale)AxisScalerFactory.AxisScalerFor(ScaleType.Linear).QAxis(DataMinX, 0, DataMaxX, false, false);
            DataMinX = axisScale.MinimumScaleValue;
            DataMaxX = axisScale.MaximumScaleValue;

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
                new AxisDefinition(null, AxisMode.None, ScaleType.Linear),
                false, false);
            axisScales.Y = new CategoryAxisScale(options.k + options.pbias);
            DivY = options.k + options.pbias;
            OffY = YAxisCanvas;

            PenDescriptor ciPen = GetLinePen(studyMarkerType, true);
            PenDescriptor dotPen = GetMarkerPen(ChartPreferences.MarkerTypes[10]);
            int r = 0;
            double yc = 0;
            double txh = LabelHeightInCanvasCoordinates(options.title[1]);
            double rowHeight = YExtCanvas / DivY;
            double featureHeight = rowHeight * 2 / 3;
            double arrowWidth = rowHeight / 8;
            for (int i = options.k; i >= 1; i--)
            {
                r++;
                double yctr = (r + options.pbias - 0.5) / DivY * YExtCanvas;
                yc = OffY + yctr;
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
                    double blobSize = (5 + featureHeight * Math.Sqrt(gw[i] / maxGw)) * 0.7;
                    DrawMarkerInCanvasCoordinates(ToCanvasX(odr[i]), yc, blobSize / 2, studyMarkerType);

                    // CI line
                    DrawLineInCanvasCoordinates(ciPen, xl, yc, xr, yc);

                    // Arrow ends if not plottable
                    if (options.lerr[i])
                    {
                        DrawLineInCanvasCoordinates(ciPen, xl + arrowWidth, yc + arrowWidth, xl, yc);
                        DrawLineInCanvasCoordinates(ciPen, xl, yc, xl + arrowWidth, yc - arrowWidth);
                    }
                    if (options.uerr[i])
                    {
                        DrawLineInCanvasCoordinates(ciPen, xr - arrowWidth, yc + arrowWidth, xr, yc);
                        DrawLineInCanvasCoordinates(ciPen, xr, yc, xr - arrowWidth, yc - arrowWidth);
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

            PlotNoEffectMarker(axisScales);

            if (options.pbias == 1)
                PlotPooledMarker(options.rmh, options.ll, options.ul, absMin, yc, options.cap);

            EndVectorPlot();
            return new ParameterBag();
        }
        protected override bool IncludeTable(MHOptions options, int i) => true;
    }
}
