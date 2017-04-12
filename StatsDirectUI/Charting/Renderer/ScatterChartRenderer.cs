using StatsDirect.Numerics;
using StatsDirect.Templates;
using System;
using System.Drawing;

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

            ScatterXYOptions sOptions = (ScatterXYOptions)definition.ChartOptions;
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
                    if (definition.XSeries.Count > 1)
                    {
                        foreach (Series s in definition.XSeries)
                        {
                            double w = LegendWidthInCanvasCoordinates(s.Title) + MINIMUM_X_WHITESPACE;
                            if (w > xtra + xAxisCanvas)
                                xtra = w - xAxisCanvas;
                        }
                    }
                }

                AxisScales axisScales = DrawAxesOrEnlargeCanvas(definition.ChartOptions.Title, new AxisDefinition(sOptions.XAxisTitle, AxisMode.Scale, definition.ScaleParameters.X.ScaleType), new AxisDefinition(sOptions.YAxisTitle, AxisMode.Scale, definition.ScaleParameters.Y.ScaleType) { ExtraSpaceBeforeAxisStarts = xtra }, boxAxes, false);

                float size2 = labelFont.Size * 2;
                //  If there are multiple series, draw the legends
                if (showLegend && definition.XSeries.Count > 1)
                {
                    int i = 1;
                    foreach (Series s in definition.XSeries)
                    {
                        if (s.Title.Length > 0)
                        {
                            DrawMarkerInCanvasCoordinates(LEGEND_MARKER_X, yAxisCanvas + yExtCanvas - LEGEND_MARKER_Y_OFFSET - size2 * i, LEGEND_MARKER_SIZE, definition.YSeries[i - 1].AsDoubleSeries);
                            DrawStringLegendL(s.Title, LEGEND_TEXT_X, yAxisCanvas + yExtCanvas - 10 - size2 * i);
                        }
                        i += 1;
                    }
                }

                // plot points
                for (int c = 0; c < definition.XSeries.Count; c++)
                {
                    DoubleSeries xs = definition.XSeries[c].AsDoubleSeries;
                    DoubleSeries ys = definition.YSeries[c].AsDoubleSeries;
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
                        if (Single.IsInfinity(xys[r].X) || Single.IsInfinity(xys[r].Y)
                            || Single.IsNaN(xys[r].X) || Single.IsNaN(xys[r].Y))
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
                int y = definition.ScaleParameters.Y.AxisScale.Tics().Count - 1;
                ASCII_InitPlot(5 + y); // 5 = Title, top axis title, bottom axis, bottom scale, bottom axis title

                // Draw the scale
                DefaultAxes();
                AxisScales axisScales = DrawAxesOrEnlargeCanvas(definition.ChartOptions.Title, new AxisDefinition(sOptions.XAxisTitle, AxisMode.Scale, definition.ScaleParameters.X.ScaleType), new AxisDefinition(sOptions.YAxisTitle, AxisMode.Scale, definition.ScaleParameters.Y.ScaleType), false, false);
                SetStandardAsciiScaling(y, axisScales);

                // Draw the title text
                int L = sOptions.YAxisTitle.Length;
                int Q = L < 14 ? 14 - L : 2;
                WriteAsciiYX(shTx.GetUpperBound(0) - 1, Q, sOptions.YAxisTitle);
                L = sOptions.XAxisTitle.Length;
                WriteAsciiYX(0, 76 - L, sOptions.XAxisTitle);

                // Work through the columns
                for (int c = 0; c <= definition.XSeries.Count - 1; c++)
                {
                    DoubleSeries xs = definition.XSeries[c].AsDoubleSeries;
                    DoubleSeries ys = definition.YSeries[c].AsDoubleSeries;
                    double[] xdat = xs.Data;
                    double[] ydat = ys.Data;
                    // Work through the rows
                    for (int r = 0; r <= xdat.Length - 1; r++)
                    {
                        if (xdat[r] != Constant.MISSING && ydat[r] != Constant.MISSING)
                        {
                            int x1 = Convert.ToInt32(offx + Convert.ToInt32(xdat[r] / divx * 60));
                            int y1 = Convert.ToInt32(offy + Convert.ToInt32(ydat[r] / divy * y));
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
