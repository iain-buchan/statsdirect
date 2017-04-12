using StatsDirect.Numerics;
using StatsDirect.Templates;
using System;
using System.Drawing;

namespace StatsDirect.Charting.Renderer
{
    class SurvivalChartRenderer: AbstractChartRenderer, IChartRenderer
    {
        public SurvivalChartRenderer(ChartDefinition definition, ICanvasFactory canvasFactory)
            : base(definition, canvasFactory)
        {
        }

        ScaleParameters IChartRenderer.GetScaleParameters()
        {
            SurvivalOptions sOptions = (SurvivalOptions)definition.ChartOptions;

            // Setup the Min & Max Values
            DataMinX = double.MaxValue;
            DataMaxX = double.MinValue;
            DataMinY = double.MaxValue;
            DataMaxY = double.MinValue;
            foreach (SurvivalOptions.SurvivalSeries ss in sOptions.Series)
            {
                foreach (double d in ss.XDat)
                {
                    if (d != Constant.MISSING)
                    {
                        if (d < DataMinX)
                            DataMinX = d;
                        if (d > DataMaxX)
                            DataMaxX = d;
                    }
                }
                foreach (double d in ss.YDat)
                {
                    if (d != Constant.MISSING)
                    {
                        if (d < DataMinY)
                            DataMinY = d;
                        if (d > DataMaxY)
                            DataMaxY = d;
                    }
                }
            }

            // #641: User can change survival plot maximum within reason - it can be set between actual DataMaxY and 1.0
            double candidateMaxY = definition.HasScaleParameters ? definition.ScaleParameters.Y.Max : 1.0;
            if (candidateMaxY < DataMaxY)
                candidateMaxY = DataMaxY;
            if (candidateMaxY > 1.0)
                candidateMaxY = 1.0;
            DataMaxY = candidateMaxY;
            return new ScaleParameters
            {
                X =
                    {
                        AllowedScaleTypes = new[] { ScaleType.Linear },
                        Min = DataMinX,
                        Max = DataMaxX
                    },
                Y =
                    {
                        AllowedScaleTypes = new[] { ScaleType.Linear },
                        Max = DataMaxY,
                        Min = 0
                    }
            };
        }

        ParameterBag IChartRenderer.Plot(ITemplateHost host)
        {
            SurvivalOptions sOptions = (SurvivalOptions)definition.ChartOptions;

            // Setup the Min & Max Values
            DataMinX = double.MaxValue;
            DataMaxX = double.MinValue;
            DataMinY = double.MaxValue;
            DataMaxY = double.MinValue;
            bool doCi = true;
            foreach (SurvivalOptions.SurvivalSeries ss in sOptions.Series)
            {
                //  If any series doesn't have both confidence intervals, we don't plot them at all.
                if (ss.YDatL == null || ss.YDatU == null)
                {
                    doCi = false;
                }
                foreach (double d in ss.XDat)
                {
                    if (d != Constant.MISSING)
                    {
                        if (d < DataMinX)
                            DataMinX = d;
                        if (d > DataMaxX)
                            DataMaxX = d;
                    }
                }
                foreach (double d in ss.YDat)
                {
                    if (d != Constant.MISSING)
                    {
                        if (d < DataMinY)
                            DataMinY = d;
                        if (d > DataMaxY)
                            DataMaxY = d;
                    }
                }
            }
            DataMinY = 0;
            // #641: User can change survival plot maximum within reason - it can be set between actual DataMaxY and 1.0
            double candidateMaxY = definition.HasScaleParameters ? definition.ScaleParameters.Y.Max : 1.0;
            if (candidateMaxY < DataMaxY)
                candidateMaxY = DataMaxY;
            if (candidateMaxY > 1.0)
                candidateMaxY = 1.0;
            DataMaxY = candidateMaxY;

            bool use_marker = sOptions.ShowEventMarkers;
            bool use_tic = sOptions.ShowCensorshipTics;

            //  If there is a legend, work out how many series there are and extend the plot area as required to hold the legend

            //  Measurements and set axes.  These are done on a scratchpad canvas before the proper measurements are set up.
            StartVectorPlot();
            SetFontsAndThicknessesFromOptions(sOptions);
            double legendFontHeight = GetFontHeightInCanvasCoordinates(legendFont);
            EndVectorPlot();

            //  By now, all measurements are known.  Set up the plot areas.
            double legendTop = yAxisCanvas - LEGEND_TOP_GAP;
            double markerMidlineOffset = (legendFontHeight - LEGEND_MARKER_SIZE) / 2;
            double legendSpacing = MINIMUM_LEGEND_GAP + Math.Max(LEGEND_MARKER_SIZE, Convert.ToInt32(legendFontHeight));
            double legendBottom = legendTop - sOptions.Series.Count * legendSpacing;
            double xtra = 0;
#if LEGEND_AT_BOTTOM
            if (sOptions.ShowLegend && legendBottom < LOWEST_ALLOWED_LEGEND)
            {
                double extraSpaceRequired = LOWEST_ALLOWED_LEGEND - legendBottom;

                //  Add in the extra space
                metafileHeight += extraSpaceRequired;
                yAxisCanvas += extraSpaceRequired;
                legendTop += extraSpaceRequired;
                // legendBottom += extraSpaceRequired; 
            }
#else
            for (int c = 0; c < sOptions.Series.Count; c++)
            {
                double w = LegendWidthInCanvasCoordinates(MakeTitle(sOptions.SeriesTitles[c], null)) + MINIMUM_X_WHITESPACE;
                if (w > xtra + xAxisCanvas)
                    xtra = w - xAxisCanvas;
            }
#endif

            StartVectorPlot(false);
            SetFontsAndThicknessesFromOptions(sOptions);
            AssignMarkersToSeries(sOptions);

            AxisScales axisScales = DrawAxesOrEnlargeCanvas(sOptions.Title, new AxisDefinition("Times", AxisMode.Scale, definition.ScaleParameters.X.ScaleType), new AxisDefinition(sOptions.YAxisTitle, AxisMode.Scale, definition.ScaleParameters.Y.ScaleType) { ExtraSpaceBeforeAxisStarts = xtra }, false, false);
            //divy = cols + 1;
            divy = 1;
            offy = yAxisCanvas;

            // Work through the columns
            //  The CI marker type is always the last one in the list
            MarkerType ciMarkerType = sOptions.MarkerTypes[sOptions.MarkerTypes.Count - 1];
            for (int c = 0; c <= sOptions.Series.Count - 1; c++)
            {
                double[] ydat = sOptions.Series[c].YDat;
                double[] xdat = sOptions.Series[c].XDat;
                int[] cdat = sOptions.Series[c].CDat;
                double[] ydat_l = sOptions.Series[c].YDatL;
                double[] ydat_u = sOptions.Series[c].YDatU;

                MarkerType mType = sOptions.MarkerTypes[c];
                using (Pen p = GetMarkerPen(mType))
                {
                    //  If necessary, draw the marker legend
                    if (sOptions.ShowLegend)
                    {
                        if (sOptions.SeriesTitles[c].Length > 0)
                        {
#if LEGEND_AT_BOTTOM
                            double markerX = xAxisCanvas + LEGEND_MARKER_SIZE / 2.0;
                            double markerY = legendTop - (c * legendSpacing) - markerMidlineOffset;
#else
                            double markerX = 9 + LEGEND_MARKER_SIZE / 2.0;
                            double markerY = yAxisCanvas + yExtCanvas - 10 - c * legendSpacing - markerMidlineOffset;
#endif
                            if (use_marker)
                            {
                                DrawMarkerInCanvasCoordinates(markerX, markerY, LEGEND_MARKER_SIZE, mType);
                            }
                            else
                            {
                                const double cornerOffset = LEGEND_MARKER_SIZE / 2.0;
                                DrawLineInCanvasCoordinates(p, markerX - cornerOffset, markerY - cornerOffset, markerX + cornerOffset, markerY - cornerOffset);
                                DrawLineInCanvasCoordinates(p, markerX + cornerOffset, markerY - cornerOffset, markerX + cornerOffset, markerY + cornerOffset);
                            }
                            DrawStringLegendL(MakeTitle(sOptions.SeriesTitles[c], null), markerX + LEGEND_MARKER_SIZE * 1.5, markerY + markerMidlineOffset);
                        }
                    }

                    double x1 = ToCanvasX(axisScales.X.MinimumScaleValue);
                    double y1 = ToCanvasY(1.0);
                    double x2 = 0;
                    double y2 = 0;
                    for (int r = ydat.GetLowerBound(0); r <= ydat.GetUpperBound(0); r++)
                    {
                        if (ydat[r] != Constant.MISSING && xdat[r] != Constant.MISSING && cdat[r] != -1)
                        {
                            x2 = ToCanvasX(xdat[r]);
                            y2 = ToCanvasY(ydat[r]);
                            if (use_marker && cdat[r] > 0)
                                DrawMarkerInCanvasCoordinates(x2, y2, mType.MarkerSize, mType);
                            // Draw tic if censored
                            if (cdat[r] == 0 && use_tic)
                                DrawLineInCanvasCoordinates(p, x2, y2, x2, y2 + 7);
                            // Then the lines
                            DrawLineInCanvasCoordinates(p, x1, y1, x2, y1);
                            DrawLineInCanvasCoordinates(p, x2, y1, x2, y2);
                        }
                        x1 = x2;
                        y1 = y2;
                    }

                    //  overlay confidence intervals
                    if (doCi)
                    {
                        // x1 = ToCanvasX( AxisXMin ); 
                        for (int r = ydat.GetLowerBound(0); r <= ydat.GetUpperBound(0); r++)
                        {
                            if (ydat[r] != Constant.MISSING & ydat_l[r] != Constant.MISSING & ydat_u[r] != Constant.MISSING & xdat[r] != Constant.MISSING & cdat[r] != -1)
                            {
                                x2 = ToCanvasX(xdat[r]);
                                // Confidence interval
                                if (cdat[r] > 0)
                                {
                                    Color ciPenColour = sOptions.UseSeriesColourForConfidenceIntervals ? p.Color : ciMarkerType.LineColor;
                                    using (Pen ciPen = new Pen(ciPenColour, ciMarkerType.Width) { DashStyle = ciMarkerType.LineDashStyle })
                                    {
                                        double y2l = ToCanvasY(ydat_l[r]);
                                        double y2u = ToCanvasY(ydat_u[r]);
                                        DrawLineInCanvasCoordinates(ciPen, x2, y2l, x2, y2u);
                                    }
                                }
                            }
                            // x1 = x2; 
                        }
                    }
                }
            }
            EndVectorPlot();
            return new ParameterBag();
        }
    }
}
