using System;
using System.Drawing;
using StatsDirect.Numerics;
using StatsDirect.Templates;

namespace StatsDirect.Charting.Renderer
{
    class ScatterChartRenderer: AbstractChartRenderer, IChartRenderer
    {
        public ScatterChartRenderer(ChartDefinition cd, ICanvasFactory canvasFactory)
            : base (cd, canvasFactory)
        {
        }

        ScaleParameters IChartRenderer.GetScaleParameters()
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

        ParameterBag IChartRenderer.Plot(ITemplateHost host)
        {
            const int LEGEND_MARKER_X = 12;
            const int LEGEND_MARKER_Y_OFFSET = 22;
            const int LEGEND_TEXT_X = 24;

            ScatterXYOptions sOptions = (ScatterXYOptions)Definition.ChartOptions;
            bool shouldDrawMarkers = sOptions.PlotMarkers;
            bool joinMarkersWithLines = sOptions.JoinMarkersWithLines;

            if (!IsAscii)
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
                    if (Definition.XSeries.Count > 1)
                    {
                        foreach (Series s in Definition.XSeries)
                        {
                            double w = LegendWidthInCanvasCoordinates(s.Title) + MINIMUM_X_WHITESPACE;
                            if (w > xtra + XAxisCanvas)
                                xtra = w - XAxisCanvas;
                        }
                    }
                }

                AxisScales axisScales = DrawAxesOrEnlargeCanvas(Definition.ChartOptions.Title, new AxisDefinition(sOptions.XAxisTitle, AxisMode.Scale, Definition.ScaleParameters.X.ScaleType), new AxisDefinition(sOptions.YAxisTitle, AxisMode.Scale, Definition.ScaleParameters.Y.ScaleType) { ExtraSpaceBeforeAxisStarts = xtra }, BoxAxes, false);

                float size2 = LabelFont.Size * 2;
                //  If there are multiple series, draw the legends
                if (showLegend && Definition.XSeries.Count > 1)
                {
                    int i = 1;
                    foreach (Series s in Definition.XSeries)
                    {
                        if (s.Title.Length > 0)
                        {
                            DrawMarkerInCanvasCoordinates(LEGEND_MARKER_X, YAxisCanvas + YExtCanvas - LEGEND_MARKER_Y_OFFSET - size2 * i, LEGEND_MARKER_SIZE, Definition.YSeries[i - 1].AsDoubleSeries);
                            DrawStringLegendL(s.Title, LEGEND_TEXT_X, YAxisCanvas + YExtCanvas - 10 - size2 * i);
                        }
                        i += 1;
                    }
                }

                // plot points
                for (int c = 0; c < Definition.XSeries.Count; c++)
                {
                    DoubleSeries xs = Definition.XSeries[c].AsDoubleSeries;
                    DoubleSeries ys = Definition.YSeries[c].AsDoubleSeries;
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
                        if (float.IsInfinity(xys[r].X) || float.IsInfinity(xys[r].Y)
                            || float.IsNaN(xys[r].X) || float.IsNaN(xys[r].Y))
                        {
                            xys[r].X = -1;
                            xys[r].Y = -1;
                        }
                    }
                    DrawMarkerSeriesInCanvasCoordinates(xys, ys.MarkerDetails.MarkerSize, ys.MarkerDetails.MarkerShape, ys.MarkerDetails.IsMarkerFilled, ys.MarkerDetails.MarkerPen, ys.MarkerDetails.LinePen, joinMarkersWithLines, shouldDrawMarkers);
                }
                MaybeDrawMarkerLines(axisScales);
                EndVectorPlot();
            }
            else
            {
                int y = Definition.ScaleParameters.Y.AxisScale.Tics().Count - 1;
                ASCII_InitPlot(5 + y); // 5 = Title, top axis title, bottom axis, bottom scale, bottom axis title

                // Draw the scale
                DefaultAxes();
                AxisScales axisScales = DrawAxesOrEnlargeCanvas(Definition.ChartOptions.Title, new AxisDefinition(sOptions.XAxisTitle, AxisMode.Scale, Definition.ScaleParameters.X.ScaleType), new AxisDefinition(sOptions.YAxisTitle, AxisMode.Scale, Definition.ScaleParameters.Y.ScaleType), false, false);
                SetStandardAsciiScaling(y, axisScales);

                // Draw the title text
                int L = sOptions.YAxisTitle.Length;
                int Q = L < 14 ? 14 - L : 2;
                WriteAsciiYX(ShTx.GetUpperBound(0) - 1, Q, sOptions.YAxisTitle);
                L = sOptions.XAxisTitle.Length;
                WriteAsciiYX(0, 76 - L, sOptions.XAxisTitle);

                // Work through the columns
                for (int c = 0; c <= Definition.XSeries.Count - 1; c++)
                {
                    DoubleSeries xs = Definition.XSeries[c].AsDoubleSeries;
                    DoubleSeries ys = Definition.YSeries[c].AsDoubleSeries;
                    double[] xdat = xs.Data;
                    double[] ydat = ys.Data;
                    // Work through the rows
                    for (int r = 0; r <= xdat.Length - 1; r++)
                    {
                        if (xdat[r] != Constant.MISSING && ydat[r] != Constant.MISSING)
                        {
                            int x1 = Convert.ToInt32(OffX + Convert.ToInt32(xdat[r] / DivX * 60));
                            int y1 = Convert.ToInt32(OffY + Convert.ToInt32(ydat[r] / DivY * y));
                            ASCII_PlotPoint(x1, y1);
                        }
                    }
                }
            }
            //  End If
            return new ParameterBag();
        }
    }
}
