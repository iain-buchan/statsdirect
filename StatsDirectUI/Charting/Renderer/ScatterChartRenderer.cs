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
            ScatterXYOptions sOptions = (ScatterXYOptions)Definition.ChartOptions;
            bool shouldDrawMarkers = sOptions.PlotMarkers;
            bool joinMarkersWithLines = sOptions.JoinMarkersWithLines;

            bool showLegend = sOptions.ShowLegend && Definition.XSeries.Count > 1;
            Legend legend = null;
            if (showLegend)
            {
                legend = new Legend() { Position = LegendPosition.Left };
                foreach (Series s in Definition.XSeries)
                    legend.LegendEntries.Add(new LegendEntry() { MarkerType = s.AsDoubleSeries.MarkerType, Label = s.Title });
            }

            if (!IsAscii)
            {
                // Plot a metafile version
                StartVectorPlot(sOptions, legend);
                AssignMarkersToSeries(sOptions);
                AxisScales axisScales = LayoutChartAndDrawAxes(Definition.ChartOptions.Title,
                    new AxisDefinition(sOptions.XAxisTitle, AxisMode.Scale, Definition.ScaleParameters.X.ScaleType),
                    new AxisDefinition(sOptions.YAxisTitle, AxisMode.Scale, Definition.ScaleParameters.Y.ScaleType),
                    BoxAxes, false,
                    legend);

                if (showLegend)
                    DrawLegend(legend);

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
                    using (Pen markerPen = GetMarkerPen(ys.MarkerType), linePen = GetLinePen(ys.MarkerType, false))
                    {
                        DrawMarkerSeriesInCanvasCoordinates(xys, ys.MarkerType.MarkerSize, ys.MarkerType.MarkerShape, ys.MarkerType.IsMarkerFilled, markerPen, linePen, joinMarkersWithLines, shouldDrawMarkers);
                    }
                }
                MaybeDrawMarkerLines(axisScales);
                EndVectorPlot();
            }
            else
            {
                int y = Definition.ScaleParameters.Y.AxisScale.Tics().Count - 1;
                ASCII_InitPlot(5 + y); // 5 = Title, top axis title, bottom axis, bottom scale, bottom axis title

                // Draw the scale
                DefaultAxes(null, default(Size));
                AxisScales axisScales = LayoutChartAndDrawAxes(Definition.ChartOptions.Title, new AxisDefinition(sOptions.XAxisTitle, AxisMode.Scale, Definition.ScaleParameters.X.ScaleType), new AxisDefinition(sOptions.YAxisTitle, AxisMode.Scale, Definition.ScaleParameters.Y.ScaleType), false, false);
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
