using Layout;
using StatsDirect.Charting.Scales;
using StatsDirect.Numerics;
using StatsDirect.Templates;
using System;

namespace StatsDirect.Charting.Renderer
{
    internal abstract class AbstractXYZChartRenderer : AbstractChartRenderer
    {
        protected AbstractXYZChartRenderer(ChartDefinition definition, ICanvasFactory canvasFactory)
            : base(definition, canvasFactory)
        {
        }

        /// <param name="labbePool">For a L'Abbe plot, the pooled odds ratio or relative risk whose locus is drawn as the dashed line</param>
        /// <param name="labbePoolIsOddsRatio">True when labbePool is an odds ratio, false when it is a relative risk</param>
        protected void PlotXYZ(double[] x, double[] y, double[] z, int lowerBound, int rows, string xtxt, string ytxt, string title, bool zPlot, DataMinMax minMaxY, MarkerType markerType, double? labbePool = default, bool labbePoolIsOddsRatio = false)
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
                    Layout.Range dataRangeX = GetMinMaxArray(x, Definition.ScaleParameters.X.ScaleType);
                    DataMinX = dataRangeX.Min;
                    DataMaxX = dataRangeX.Max;
                    if (minMaxY == DataMinMax.XCalc_YCalc)
                    {
                        Layout.Range dataRangeY = GetMinMaxArray(y, Definition.ScaleParameters.Y.ScaleType);
                        DataMinY = dataRangeY.Min;
                        DataMaxY = dataRangeY.Max;
                    }
                }
            }
            AxisScales axisScales = LayoutChartAndDrawAxes(title,
                new AxisDefinition(xtxt, AxisMode.Scale, ScaleType.Linear),
                new AxisDefinition(ytxt, AxisMode.Scale, ScaleType.Linear),
                isLAabbe, false);

            if (zPlot)
                DrawQCanvas(OffY);

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
                DrawLineInChartCoordinates(GrBlack, axisScales.X.MinimumScaleValue, axisScales.Y.MinimumScaleValue, axisScales.X.MaximumScaleValue, axisScales.Y.MaximumScaleValue);
                // pooled effect
                PenDescriptor pooledPen = GetLinePen(ChartPreferences.MarkerTypes[10], false);
                if (labbePoolIsOddsRatio)
                {
                    // The locus of a constant odds ratio is the curve y = rmh x / (1 - x + rmh x), with the rates as fractions; it used to be drawn as the
                    // line through the origin of slope rmh, which is the locus of a constant relative risk.
                    const int segments = 100;
                    double xMin = axisScales.X.MinimumScaleValue;
                    double xMax = axisScales.X.MaximumScaleValue;
                    double xPrevious = xMin;
                    double yPrevious = OddsRatioLocus(xMin, rmh);
                    for (int segment = 1; segment <= segments; segment++)
                    {
                        double xNext = xMin + (xMax - xMin) * segment / segments;
                        double yNext = OddsRatioLocus(xNext, rmh);
                        MaybeDrawLineInChartCoordinates(axisScales, pooledPen, xPrevious, yPrevious, xNext, yNext);
                        xPrevious = xNext;
                        yPrevious = yNext;
                    }
                }
                else
                {
                    // The locus of a constant relative risk: the line through the origin of slope rmh
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
                    DrawLineInCanvasCoordinates(pooledPen, ToCanvasX(axisScales.X.MinimumScaleValue), ToCanvasY(axisScales.Y.MinimumScaleValue), x1, y1);
                }
            }
            EndVectorPlot();
        }

        /// <summary>
        /// The experimental event rate, in percent, at which a study with control event rate controlPercent has the odds ratio oddsRatio.
        /// </summary>
        private static double OddsRatioLocus(double controlPercent, double oddsRatio)
        {
            double p = controlPercent / 100.0;
            return 100.0 * oddsRatio * p / (1.0 - p + oddsRatio * p);
        }
    }
}
