using StatsDirect.Templates;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace StatsDirect.Charting.Renderer
{
    class ErrorBarChartRenderer: AbstractChartRenderer, IChartRenderer
    {
        public ErrorBarChartRenderer(ChartDefinition cd, ICanvasFactory canvasFactory)
            : base(cd, canvasFactory)
        {
        }

        ScaleParameters IChartRenderer.GetScaleParameters()
        {
            ErrorBarOptions eOptions = (ErrorBarOptions)definition.ChartOptions;

            // Setup the Min & Max Values
            DataMinX = double.MaxValue;
            DataMaxX = double.MinValue;
            DataMinY = double.MaxValue;
            DataMaxY = double.MinValue;

            foreach (MultiDoubleSeries s in eOptions.Series)
            {
                // Get the overall Min & Max Values for Y
                for (int i = 0; i <= 2; i++)
                {
                    if (DataMinY > s.MinY(i))
                        DataMinY = s.MinY(i);
                    if (DataMaxY < s.MaxY(i))
                        DataMaxY = s.MaxY(i);
                }

                // Get the overall Min & Max Values for X
                if (DataMinX > s.MinX)
                    DataMinX = s.MinX;
                if (DataMaxX < s.MaxX)
                    DataMaxX = s.MaxX;

            }

            return new ScaleParameters
            {
                X =
                    {
                        ScaleType = ScaleType.Linear,
                        AllowedScaleTypes = new[] { ScaleType.Linear },
                        Max = DataMaxX,
                        Min = DataMinX
                    },
                Y =
                    {
                        ScaleType = ScaleType.Linear,
                        AllowedScaleTypes = new[] { ScaleType.Linear },
                        Max = DataMaxY,
                        Min = DataMinY
                    }
            };
        }

        /// <summary>
        /// Series setup: Y[0] = centre value, Y[1] = lower error bar value, Y[2] = upper error bar value.
        /// </summary>
        /// <returns></returns>
        ParameterBag IChartRenderer.Plot(ITemplateHost host)
        {
            ErrorBarOptions eOptions = (ErrorBarOptions)definition.ChartOptions;
            bool shouldCheckForOffsets = eOptions.ShouldCheckForOffsets;
            int seriesCount = eOptions.Series.Count;

            // Setup the Min & Max Values
            DataMinX = double.MaxValue;
            DataMaxX = double.MinValue;
            DataMinY = double.MaxValue;
            DataMaxY = double.MinValue;

            foreach (MultiDoubleSeries s in eOptions.Series)
            {
                // Get the overall Min & Max Values for Y values, including upper and lower error bars
                for (int i = 0; i <= 2; i++)
                {
                    if (DataMinY > s.MinY(i))
                        DataMinY = s.MinY(i);
                    if (DataMaxY < s.MaxY(i))
                        DataMaxY = s.MaxY(i);
                }

                // Get the overall Min & Max Values for X
                if (DataMinX > s.MinX)
                    DataMinX = s.MinX;
                if (DataMaxX < s.MaxX)
                    DataMaxX = s.MaxX;
            }

            //  If there's a legend, work out how many series there are and extend the plot area as required to hold the legend

            //  Measurements and set axes.  These are done on a scratchpad canvas before the proper measurements are set up.
            StartVectorPlot();
            SetFontsAndThicknessesFromOptions(eOptions);
            double legendFontHeight = GetFontHeightInCanvasCoordinates(legendFont);
            EndVectorPlot();

            //  By now, all measurements are known.  Set up the plot areas.
            double legendTop = yAxisCanvas - LEGEND_TOP_GAP;
            double legendRowHeight = Math.Max(LEGEND_MARKER_SIZE, Convert.ToInt32(legendFontHeight));
            double legendSpacing = MINIMUM_LEGEND_GAP + legendRowHeight;
            if (eOptions.ShowLegend && eOptions.ShowLegendIsRelevant)
            {
                //  Dim markerMidlineOffset As Double = (legendFontHeight - LEGEND_MARKER_SIZE) / 2
                double legendBottom = legendTop - seriesCount * legendSpacing;
                if (legendBottom < LOWEST_ALLOWED_LEGEND)
                {
                    double extraSpaceRequired = LOWEST_ALLOWED_LEGEND - legendBottom;

                    //  Add in the extra space
                    imageHeight += (int)Math.Ceiling(extraSpaceRequired);
                    yAxisCanvas += extraSpaceRequired;
                    legendTop += extraSpaceRequired;
                    // legendBottom += extraSpaceRequired; 
                }
            }

            StartVectorPlot(false);
            SetFontsAndThicknessesFromOptions(eOptions);
            AssignMarkersToSeries(eOptions);

            // Draw the scale
            DrawAxesOrEnlargeCanvas(eOptions.Title, new AxisDefinition(eOptions.XAxisTitle, AxisMode.Scale, definition.ScaleParameters.X.ScaleType), new AxisDefinition(eOptions.YAxisTitle, AxisMode.Scale, definition.ScaleParameters.Y.ScaleType), boxAxes, false);

            // #1079: Prevent overdrawing of error bars by offsetting bars that would otherwise overlap.
            Dictionary<int, List<MultiDoublePoint>> alreadyUsed = new Dictionary<int, List<MultiDoublePoint>>();
            double aboutALineWidth = divx / xExtCanvas;

            // Work through the series
            for (int seriesIndex = 0; seriesIndex < eOptions.Series.Count; seriesIndex++)
            {
                MultiDoubleSeries s = eOptions.Series[seriesIndex];
                List<MultiDoublePoint> safesBySeries = new List<MultiDoublePoint>();
                int length = s.Data.Length;

                // Draw the error bars first so we don't interfere with connection lines
                using (Pen p = GetMarkerPen(eOptions.MarkerTypes[seriesIndex]))
                {
                    foreach (MultiDoublePoint pt in s.Data)
                    {
                        // Check for overlaps with any existing error bar.  If none, save this one; if there is one, offset by the line width and try again.
                        MultiDoublePoint safePoint = pt;
                        if (shouldCheckForOffsets)
                        {
                            while (true)
                            {
                                int roughX = (int)Math.Round(ToCanvasX(safePoint.X));
                                List<MultiDoublePoint> barsAtRoughX;
                                if (!alreadyUsed.TryGetValue(roughX, out barsAtRoughX))
                                {
                                    // this one's the first point at this X; known safe.  Record it and move on.
                                    alreadyUsed.Add(roughX, new List<MultiDoublePoint> { safePoint });
                                    break;
                                }

                                // If we get here, there's at least one bar at this rough X.  Check for overlaps; if they exist, offset this by 1 and try that instead.
                                bool atLeastOneOverlap = false;
                                foreach (MultiDoublePoint existingBar in barsAtRoughX)
                                {
                                    if (safePoint.get_Y(1) < existingBar.get_Y(2) && safePoint.get_Y(2) > existingBar.get_Y(1))
                                    {
                                        atLeastOneOverlap = true;
                                        safePoint = safePoint.Clone();
                                        safePoint.X += aboutALineWidth;
                                        break;
                                    }
                                }
                                if (!atLeastOneOverlap)
                                {
                                    // We've found somewhere to put this point.  Record it and move on.
                                    alreadyUsed[roughX].Add(safePoint);
                                    break;
                                }
                            }
                        }
                        safesBySeries.Add(safePoint);

                        double x = ToCanvasX(safePoint.X);
                        double yl = ToCanvasY(safePoint.get_Y(1));
                        double yu = ToCanvasY(safePoint.get_Y(2));
                        // Draw the endlines
                        DrawLineInCanvasCoordinates(p, x - 10, yl, x + 10, yl);
                        DrawLineInCanvasCoordinates(p, x - 10, yu, x + 10, yu);
                        // Draw the bar
                        DrawLineInCanvasCoordinates(p, x, yl, x, yu);
                    }
                }

                //  Plot the markers
                if (eOptions.PlotMarkers)
                {
                    foreach (MultiDoublePoint pt in safesBySeries)
                    {
                        double x1 = ToCanvasX(pt.X);
                        double y1 = ToCanvasY(pt.get_Y(0));
                        DrawMarkerInCanvasCoordinates(x1, y1, eOptions.MarkerTypes[seriesIndex].MarkerSize, eOptions.MarkerTypes[seriesIndex]);
                    }
                }

                if (eOptions.JoinMarkersWithLines)
                {
                    using (Pen pStyled = GetLinePen(eOptions.MarkerTypes[seriesIndex], false))
                    {
                        // set the initial values of x2,y2 to x1,y1
                        double x2 = ToCanvasX(s.Data[0].X);
                        double y2 = ToCanvasY(s.Data[0].get_Y(0));

                        foreach (MultiDoublePoint pt in safesBySeries)
                        {
                            double x1 = ToCanvasX(pt.X);
                            double y1 = ToCanvasY(pt.get_Y(0));
                            DrawLineInCanvasCoordinates(pStyled, x1, y1, x2, y2);
                            x2 = x1;
                            y2 = y1;
                        }
                    }
                }

                //  Legend
                if (eOptions.ShowLegend && eOptions.ShowLegendIsRelevant)
                {
                    double legendY = legendTop - seriesIndex * legendSpacing;
                    DrawMarkerInCanvasCoordinates(xAxisCanvas + LEGEND_MARKER_SIZE / 2.0, legendY - legendFontHeight / 2.0, LEGEND_MARKER_SIZE, eOptions.MarkerTypes[seriesIndex]);
                    DrawStringLegendL(eOptions.SeriesTitles[seriesIndex], xAxisCanvas + LEGEND_MARKER_SIZE * 2, legendY);
                }
            }
            EndVectorPlot();
            return new ParameterBag();
        }
    }
}
