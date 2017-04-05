using StatsDirect.Numerics;
using StatsDirect.Templates;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace StatsDirect.Charting.Renderer
{
    class BarChartRenderer: AbstractChartRenderer, IChartRenderer
    {
        public BarChartRenderer(ChartDefinition definition, ICanvasFactory canvasFactory)
            : base(definition, canvasFactory)
        {
        }

        ScaleParameters IChartRenderer.GetScaleParameters()
        {
            BarOptions bOptions = ((BarOptions)(definition.ChartOptions));

            // No false origins
            DataMinY = 0;

            //  Stacked and 100% stacked charts require different scaling
            if (bOptions.Stacked)
            {
                if (bOptions.Stacked100Percent)
                {
                    DataMaxY = 100;
                }
                else
                {
                    double largestSoFar = 0;

                    for (int offset = 0; offset <= definition.YSeries[0].AsDoubleSeries.Points - 1; offset++)
                    {
                        double thisTotal = 0;
                        foreach (DoubleSeries s in definition.YSeries)
                            if (s.Data[offset] != Constant.MISSING)
                                thisTotal += s.Data[offset];
                        if (thisTotal > largestSoFar)
                            largestSoFar = thisTotal;
                    }
                    //  Ensure there's always *some* size to the axis
                    if (largestSoFar == 0)
                        largestSoFar = 1;
                    DataMaxY = largestSoFar;
                }
            }

            // Label orientation: As standard, there are 80 characters across.
            const int maxLabelChars = 80;
            int longestTitle = 0;
            foreach (string title in bOptions.SeriesTitles)
                if (title.Length > longestTitle)
                    longestTitle = title.Length;
            LabelDirection preferredLabelDirection = LabelDirection.Across;
            if (longestTitle * bOptions.SeriesTitles.Length > maxLabelChars)
                preferredLabelDirection = LabelDirection.Up;

            return new ScaleParameters
            {
                X =
                {
                    AllowedScaleTypes = new[] { ScaleType.Category },
                    Max = 0,
                    Min = 0,
                    LabelDirection = preferredLabelDirection
                },
                Y =
                {
                    AllowedScaleTypes = new[] { ScaleType.Linear },
                    Max = DataMaxY,
                    Min = DataMinY
                }
            };
        }


        ///  <summary>
        ///  Plot a bar, stacked bar or 100% stacked bar chart.
        ///  </summary>
        ///  <remarks></remarks>
        ParameterBag IChartRenderer.Plot(ITemplateHost host)
        {
            definition = definition.Clone();

            IList<Series> seriesToUse = definition.YSeries;
            BarOptions bOptions = ((BarOptions)(definition.ChartOptions));

            string xAxisTitle = bOptions.XAxisTitle;
            string yAxisTitle = bOptions.YAxisTitle;

            // If we've been asked to flip rows and columns, do so
            if (bOptions.Stacked && bOptions.RotateWhenStacked)
            {
                string[] oldSeriesTitles = bOptions.SeriesTitles;

                // The new series titles are the old series names
                string[] newSeriesTitles = new string[seriesToUse.Count];
                for (int i = 0; i < seriesToUse.Count; i++)
                    newSeriesTitles[i] = seriesToUse[i].Title;

                // One new series for each old title
                List<Series> newSeriesToUse = new List<Series>(oldSeriesTitles.Length);
                foreach (string t in oldSeriesTitles)
                {
                    Series s = new DoubleSeries(new double[seriesToUse.Count], t);
                    newSeriesToUse.Add(s);
                }

                // Rotate the data
                for (int oldSeries = 0; oldSeries < seriesToUse.Count; oldSeries++)
                    for (int oldRow = 0; oldRow < oldSeriesTitles.Length; oldRow++)
                        newSeriesToUse[oldRow].AsDoubleSeries.Data[oldSeries] = seriesToUse[oldSeries].AsDoubleSeries.Data[oldRow];

                // Assign
                bOptions.SeriesTitles = newSeriesTitles;
                definition.YSeries = newSeriesToUse;
                seriesToUse = newSeriesToUse;

                // Ensure we have enough markers
                bOptions.SetMarkers(seriesToUse);
                bOptions.MarkerTypes = MarkersFromDescriptors(bOptions.SeriesOptions, bOptions.ShouldForceIsFilled,
                                                              bOptions.ForcedIsFilled, bOptions.ShouldForceFillStyle,
                                                              bOptions.ForcedFillStyle);
            }

            //  Sort out the axes for different chart types
            if (bOptions.Stacked)
            {
                if (bOptions.Stacked100Percent)
                {
                    //  Y axis scales 0-100
                    DataMinY = 0;
                    DataMaxY = 100;
                }
                else
                {
                    //  Add up the bars and scale to that maximum
                    double largestSetOfBars = 0;
                    for (int barIndex = 0; barIndex <= seriesToUse[0].AsDoubleSeries.Data.Length - 1; barIndex++)
                    {
                        //  Missing data leads to missing bars
                        double totalOfAllBars = 0;
                        for (int seriesIndex = 0; seriesIndex <= seriesToUse.Count - 1; seriesIndex++)
                        {
                            double seriesValue = seriesToUse[seriesIndex].AsDoubleSeries.Data[barIndex];
                            if (seriesValue != Constant.MISSING)
                                totalOfAllBars += seriesValue;
                        }
                        largestSetOfBars = Math.Max(largestSetOfBars, totalOfAllBars);
                    }
                    DataMaxY = largestSetOfBars;
                }
            }

            //  If there's a legend, work out how many series there are and extend the plot area as required to hold the legend
            bool shouldDrawLegend = bOptions.Stacked || (bOptions.ShowLegend && bOptions.ShowLegendIsRelevant);

            //  Measurements and set axes.  These are done on a scratchpad canvas before the proper measurements are set up.
            StartVectorPlot();
            SetFontsAndThicknessesFromOptions(bOptions);
            double legendFontHeight = GetFontHeightInCanvasCoordinates(legendFont);
            EndVectorPlot();

            //  By now, all measurements are known.  Set up the plot areas.
            double legendTop = yAxisCanvas - LEGEND_TOP_GAP;
            double legendRowHeight = Math.Max(LEGEND_MARKER_SIZE, Convert.ToInt32(legendFontHeight));
            double legendSpacing = MINIMUM_LEGEND_GAP + legendRowHeight;
            double legendSpaceRequired = 0;
            if (shouldDrawLegend)
            {
                double legendBottom = legendTop - (seriesToUse.Count * legendSpacing);
                if (legendBottom < LOWEST_ALLOWED_LEGEND)
                {
                    legendSpaceRequired = LOWEST_ALLOWED_LEGEND - legendBottom;
                    legendTop += legendSpaceRequired;
                    legendBottom += legendSpaceRequired;
                }
            }

            //  Plot
            if (bOptions.Orientation == ChartOrientation.Horizontal)
            {
                //  Flip the series, and hence the min/max values
                definition = definition.Clone();
                List<Series> tempSeries = definition.XSeries;
                definition.XSeries = definition.YSeries;
                definition.YSeries = tempSeries;
                AxisScaleParameters tempAxisScaleParameters = definition.ScaleParameters.X;
                definition.ScaleParameters.X = definition.ScaleParameters.Y;
                definition.ScaleParameters.Y = tempAxisScaleParameters;
                DataMinX = DataMinY;
                DataMaxX = DataMaxY;
                DataMinY = 0;
                DataMaxY = 0;
                string temp = yAxisTitle;
                yAxisTitle = xAxisTitle;
                xAxisTitle = temp;

                StartVectorPlot(false);
                SetFontsAndThicknessesFromOptions(bOptions);

                // Draw the scale
                AssignMarkersToSeries(bOptions);

                double xtra = 0;
                foreach (string s in bOptions.SeriesTitles)
                {
                    double w = AxisLabelWidthInCanvasCoordinates(s);
                    if (w > xtra)
                        xtra = w;
                }
                xtra = Math.Max(0, Convert.ToInt32(xtra - 20));

                AxisScales axisScales = DrawAxesOrEnlargeCanvas(definition.ChartOptions.Title,
                    new AxisDefinition(xAxisTitle, AxisMode.Scale, definition.ScaleParameters.X.ScaleType) { ExtraSpaceBeforeAxisStarts = legendSpaceRequired },
                    new AxisDefinition(yAxisTitle, AxisMode.Series, definition.ScaleParameters.Y.ScaleType) { ExtraSpaceBeforeAxisStarts = xtra, Labels = bOptions.SeriesTitles },
                    bOptions.ShouldBoxAxes, false);
                divy = ((DoubleSeries)(seriesToUse[0])).Points;
                offy = -(0 / divy * yExtCanvas) + yAxisCanvas;

                double eachAreaHeight = yExtCanvas / divy;
                double eachBarHeightFraction;
                double eachBarHeight;
                double totalBarHeightFraction;
                if (bOptions.Stacked)
                {
                    eachBarHeightFraction = Math.Min(1.0, Math.Max(bOptions.MaxBarWidth, 0.01));
                    eachBarHeight = eachAreaHeight * eachBarHeightFraction;
                    totalBarHeightFraction = eachBarHeightFraction;
                }
                else
                {
                    eachBarHeightFraction = Math.Min(1.0 / seriesToUse.Count, Math.Max(bOptions.MaxBarWidth, 0.01));
                    eachBarHeight = eachAreaHeight * eachBarHeightFraction;
                    totalBarHeightFraction = eachBarHeightFraction * seriesToUse.Count;
                }
                double eachSideWhiteSpaceHeightFraction = (1.0 - totalBarHeightFraction) / 2.0;
                double eachSideWhiteSpaceHeight = eachSideWhiteSpaceHeightFraction * eachAreaHeight;

                //  Work through the columns - this plots each series in turn, rather than all the bars in increasing Y-order.  It's easier on pen/brush resources but requires a little more calculation.
                for (int c = 0; c <= seriesToUse.Count - 1; c++)
                {
                    DoubleSeries s = seriesToUse[c].AsDoubleSeries;
                    MarkerType mt = bOptions.MarkerTypes[c];
                    double bottomOffsetInArea;
                    if (bOptions.Stacked)
                        bottomOffsetInArea = eachBarHeight + eachSideWhiteSpaceHeight;
                    else
                        bottomOffsetInArea = (seriesToUse.Count - c) * eachBarHeight + eachSideWhiteSpaceHeight;

                    Pen barPen = s.MarkerDetails.LinePen;
                    Brush barBrush = MarkerTypeToBrush(mt);

                    for (int barIndex = 0; barIndex <= s.Data.Length - 1; barIndex++)
                    {
                        double thisData = s.Data[barIndex];
                        //  Missing data leads to missing bars
                        if (thisData != Constant.MISSING)
                        {
                            double totalBelowThisBar = 0;
                            double totalOfAllBars = 0;
                            //  Data exists.  For stacked and 100% stacked bars, we now need to position and scale the bar.
                            if (bOptions.Stacked)
                            {
                                for (int probeIndex = 0; probeIndex <= seriesToUse.Count - 1; probeIndex++)
                                {
                                    double probeValue = seriesToUse[probeIndex].AsDoubleSeries.Data[barIndex];
                                    if (probeValue != Constant.MISSING)
                                    {
                                        if (probeIndex < c)
                                            totalBelowThisBar += probeValue;
                                        totalOfAllBars += probeValue;
                                    }
                                }
                                //  By now: totalOfAllBars contains the total for all bars; totalBelowThisBar contains the total of bars that have already been drawn; thisData contains our own bar length
                                if (bOptions.Stacked100Percent)
                                {
                                    if (totalOfAllBars <= 0)
                                        thisData = Constant.MISSING;
                                    else
                                    {
                                        //  Scale to percent
                                        thisData = thisData / totalOfAllBars * 100.0;
                                        totalBelowThisBar = totalBelowThisBar / totalOfAllBars * 100.0;
                                    }
                                }
                                //  By now, totalBelowThisBar contains the sum of all the values below this bar scaled appropriately for 100% scaling if required.
                                //  thisData also contains appropriately scaled data.
                            }

                            //  If we should, draw this bar
                            if (thisData != Constant.MISSING)
                            {
                                //  Prevent portions of bars being drawn below the X axis
                                double dataW;
                                double dataLowX;
                                //  Dim dataHighX As Double = totalBelowThisBar + thisData
                                if (totalBelowThisBar < axisScales.X.MinimumScaleValue)
                                {
                                    dataW = thisData + totalBelowThisBar - axisScales.X.MinimumScaleValue;
                                    dataLowX = axisScales.X.MinimumScaleValue;
                                }
                                else
                                {
                                    dataW = thisData;
                                    dataLowX = totalBelowThisBar;
                                }

                                if (dataW > 0)
                                {
                                    double areaYOffset = (s.Data.Length - 1 - barIndex) * eachAreaHeight;
                                    double barH = eachBarHeight;
                                    double barW = ToCanvasWidth(dataW);
                                    double barY = offy + areaYOffset + bottomOffsetInArea;
                                    double barX = ToCanvasX(dataLowX);
                                    if (barBrush != null)
                                    {
                                        FillRectangleInCanvasCoordinates(barBrush, barX, barY, barW, barH);
                                    }
                                    if (!(definition.ChartOptions.UseColour))
                                    {
                                        DrawRectangleInCanvasCoordinates(barPen, barX, barY, barW, barH);
                                    }
                                }
                            }
                        }
                    }

                    //  Legend
                    if (shouldDrawLegend)
                    {
                        if (barBrush != null)
                            FillRectangleInCanvasCoordinates(barBrush, xAxisCanvas, legendTop - (c * legendSpacing), legendRowHeight, legendRowHeight);
                        if (!(definition.ChartOptions.UseColour))
                            DrawRectangleInCanvasCoordinates(barPen, xAxisCanvas, legendTop - (c * legendSpacing), legendRowHeight, legendRowHeight);
                        DrawStringLegendL(definition.XSeries[c].Title, xAxisCanvas + 9 + legendRowHeight, legendTop - (c * legendSpacing));
                    }

                    if (barBrush != null)
                        barBrush.Dispose();
                }
                MaybeDrawMarkerLines(axisScales);
            }
            else
            {
                //  Not horizontal, so vertical

                StartVectorPlot(false);
                SetFontsAndThicknessesFromOptions(bOptions);
                AssignMarkersToSeries(bOptions);

                //  Get overall minima and maxima
                double min = 0; // We don't do false origins, so axis minimum cannot be greater than zero
                double minGreaterThanZero = double.MaxValue;
                double max = double.MinValue;
                foreach (DoubleSeries s in seriesToUse)
                {
                    min = Math.Min(min, s.Min);
                    minGreaterThanZero = Math.Min(minGreaterThanZero, s.MinGreaterThanZero);
                    max = Math.Max(max, s.Max);
                }
                DataMinY = min; // HACK!  TODO: We really need to fix up the references to min, DataMin and so on.

                AxisScales axisScales = DrawAxesOrEnlargeCanvas(definition.ChartOptions.Title, new AxisDefinition(xAxisTitle, AxisMode.Series, definition.ScaleParameters.X.ScaleType) { ExtraSpaceBeforeAxisStarts = legendSpaceRequired, Labels = bOptions.SeriesTitles }, new AxisDefinition(yAxisTitle, AxisMode.Scale, definition.ScaleParameters.Y.ScaleType), bOptions.ShouldBoxAxes, false);
                divx = ((DoubleSeries)(seriesToUse[0])).Points;
                offx = -(0 / divx * xExtCanvas) + xAxisCanvas;

                double eachAreaWidth = xExtCanvas / divx;
                double eachBarWidthFraction;
                double eachBarWidth;
                double totalBarWidthFraction;
                if (bOptions.Stacked)
                {
                    eachBarWidthFraction = Math.Min(1.0, Math.Max(bOptions.MaxBarWidth, 0.01));
                    eachBarWidth = eachAreaWidth * eachBarWidthFraction;
                    totalBarWidthFraction = eachBarWidthFraction;
                }
                else
                {
                    eachBarWidthFraction = Math.Min(1.0 / seriesToUse.Count, Math.Max(bOptions.MaxBarWidth, 0.01));
                    eachBarWidth = eachAreaWidth * eachBarWidthFraction;
                    totalBarWidthFraction = eachBarWidthFraction * seriesToUse.Count;
                }
                double eachSideWhiteSpaceWidthFraction = (1.0 - totalBarWidthFraction) / 2.0;
                double eachSideWhiteSpaceWidth = eachSideWhiteSpaceWidthFraction * eachAreaWidth;

                // work through the columns - this plots each series in turn, rather than all the bars in increasing X-order
                for (int c = 0; c <= seriesToUse.Count - 1; c++)
                {
                    DoubleSeries s = seriesToUse[c].AsDoubleSeries;
                    MarkerType mt = bOptions.MarkerTypes[c];
                    double leftOffsetInArea;
                    if (bOptions.Stacked)
                    {
                        leftOffsetInArea = eachSideWhiteSpaceWidth;
                    }
                    else
                    {
                        leftOffsetInArea = c * eachBarWidth + eachSideWhiteSpaceWidth;
                    }

                    Pen barPen = s.MarkerDetails.LinePen;
                    Brush barBrush = MarkerTypeToBrush(mt);

                    for (int barIndex = 0; barIndex <= s.Data.Length - 1; barIndex++)
                    {
                        double thisData = s.Data[barIndex];
                        //  Missing data leads to missing bars
                        if (thisData != Constant.MISSING)
                        {
                            double totalBelowThisBar = 0;
                            //  Data exists.  For stacked and 100% stacked bars, we now need to position and scale the bar.
                            if (bOptions.Stacked)
                            {
                                double totalOfAllBars = 0;
                                for (int probeIndex = 0; probeIndex <= seriesToUse.Count - 1; probeIndex++)
                                {
                                    double probeValue = seriesToUse[probeIndex].AsDoubleSeries.Data[barIndex];
                                    if (probeValue != Constant.MISSING)
                                    {
                                        if (probeIndex < c)
                                        {
                                            totalBelowThisBar += probeValue;
                                        }
                                        totalOfAllBars += probeValue;
                                    }
                                }
                                //  By now: totalOfAllBars contains the total for all bars; totalBelowThisBar contains the total of bars that have already been drawn; thisData contains our own bar length
                                if (bOptions.Stacked100Percent)
                                {
                                    if (totalOfAllBars <= 0)
                                    {
                                        thisData = Constant.MISSING;
                                    }
                                    else
                                    {
                                        //  Scale to percent
                                        thisData = thisData / totalOfAllBars * 100.0;
                                        totalBelowThisBar = totalBelowThisBar / totalOfAllBars * 100.0;
                                    }
                                }
                                //  By now, totalBelowThisBar contains the sum of all the values below this bar scaled appropriately for 100% scaling if required.
                                //  thisData also contains appropriately scaled data.
                            }

                            //  If we should, draw this bar
                            if (thisData != Constant.MISSING)
                            {
                                //  Prevent portions of bars being drawn below the X axis
                                double dataH;
                                double dataLowY;
                                //  Dim dataHighX As Double = totalBelowThisBar + thisData
                                if (totalBelowThisBar < axisScales.Y.MinimumScaleValue)
                                {
                                    dataH = thisData + totalBelowThisBar - axisScales.Y.MinimumScaleValue;
                                    dataLowY = axisScales.Y.MinimumScaleValue;
                                }
                                else
                                {
                                    dataH = thisData;
                                    dataLowY = totalBelowThisBar;
                                }

                                if (dataH > 0)
                                {
                                    double areaXOffset = barIndex * eachAreaWidth;
                                    double barW = eachBarWidth;
                                    double barH = dataH / divy * yExtCanvas;
                                    double barX = offx + areaXOffset + leftOffsetInArea;
                                    double barY = ToCanvasY(dataLowY + dataH);

                                    if (barBrush != null)
                                        FillRectangleInCanvasCoordinates(barBrush, barX, barY, barW, barH);
                                    if (!(definition.ChartOptions.UseColour))
                                        DrawRectangleInCanvasCoordinates(barPen, barX, barY, barW, barH);
                                }
                            }
                        }
                    }

                    //  Legend
                    if (shouldDrawLegend)
                    {
                        if (barBrush == null)
                            DrawRectangleInCanvasCoordinates(barPen, xAxisCanvas, legendTop - (c * legendSpacing), legendRowHeight, legendRowHeight);
                        else
                            FillRectangleInCanvasCoordinates(barBrush, xAxisCanvas, legendTop - (c * legendSpacing), legendRowHeight, legendRowHeight);
                        DrawStringLegendL(definition.YSeries[c].Title, xAxisCanvas + 9 + legendRowHeight, legendTop - (c * legendSpacing));
                    }

                    if (barBrush != null)
                        barBrush.Dispose();
                }
                MaybeDrawMarkerLines(axisScales);
            }
            EndVectorPlot();
            return new ParameterBag();
        }
    }
}
