using StatsDirect.Numerics;
using StatsDirect.Templates;
using StatsDirect.Utilities;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;

namespace StatsDirect.Charting.Renderer
{
    class RocChartRenderer: AbstractChartRenderer, IChartRenderer
    {
        public RocChartRenderer(ChartDefinition cd, ICanvasFactory canvasFactory)
            : base(cd, canvasFactory)
        {
        }

        ScaleParameters IChartRenderer.GetScaleParameters()
        {
            return new ScaleParameters
            {
                X =
                {
                    AllowedScaleTypes = new[] { ScaleType.Linear },
                    Max = 1,
                    Min = 0
                },
                Y =
                {
                    AllowedScaleTypes = new[] { ScaleType.Linear },
                    Max = 1,
                    Min = 0
                }
            };
        }

        ///  <summary>
        ///  Plot a ROC chart.
        ///  </summary>
        /// <param name="host"></param>
        ParameterBag IChartRenderer.Plot(ITemplateHost host)
        {
            ROCOptions rOptions = ((ROCOptions)(definition.ChartOptions));
            double GAMMA = rOptions.GAMMA;
            if (GAMMA <= 0)
                return null;
            double cit;
            double P0;
            MathDbl.civ(0, out cit, GAMMA, out P0);

            //  Assume data passed as series - X is positive, Y is negative.

            ROCSeriesRecord[] seriesData = new ROCSeriesRecord[definition.XSeries.Count];
            for (int c = 0; c < definition.XSeries.Count; c++)
            {
                seriesData[c] = new ROCSeriesRecord();
                DoubleSeries xs = definition.XSeries[c].AsDoubleSeries;
                DoubleSeries ys = definition.YSeries[c].AsDoubleSeries;
                seriesData[c].pdata = xs.Data;
                seriesData[c].adata = ys.Data;
                seriesData[c].pmn = xs.Sum / Convert.ToDouble(xs.Points);
                seriesData[c].amn = ys.Sum / Convert.ToDouble(ys.Points);
                seriesData[c].min = Math.Min(xs.Min, ys.Min);
                seriesData[c].max = Math.Max(xs.Max, ys.Max);
                // get a sorted list of all data in order to calculate cut points
                seriesData[c].tdata = new double[xs.Points + ys.Points];
                for (int r = 0; r < xs.Points; r++)
                    seriesData[c].tdata[r] = xs.Data[r];
                for (int r = 0; r < ys.Points; r++)
                    seriesData[c].tdata[xs.Points + r] = ys.Data[r];
                Array.Sort(seriesData[c].tdata);
            }

            // Work out how many series there are and extend the plot area as required to hold the legend

            // Measurements and set axes.  These are done on a scratchpad canvas before the proper measurements are set up.
            StartVectorPlot();
            SetFontsAndThicknessesFromOptions(rOptions);
            double smallerExt = Math.Min(xExtCanvas, yExtCanvas);
            xExtCanvas = smallerExt;
            yExtCanvas = smallerExt;
            double legendFontHeight = GetFontHeightInCanvasCoordinates(legendFont);
            EndVectorPlot();

            // By now, all measurements are known.  Set up the plot areas.
            double legendTop = yAxisCanvas - LEGEND_TOP_GAP;
            double markerMidlineOffset = (legendFontHeight - LEGEND_MARKER_SIZE) / 2;
            double legendSpacing = MINIMUM_LEGEND_GAP + Math.Max(LEGEND_MARKER_SIZE, Convert.ToInt32(legendFontHeight));
            double legendBottom = legendTop - (definition.XSeries.Count * legendSpacing);
            if (legendBottom < LOWEST_ALLOWED_LEGEND)
            {
                double extraSpaceRequired = LOWEST_ALLOWED_LEGEND - legendBottom;

                //  Add in the extra space
                imageHeight += (int)Math.Ceiling(extraSpaceRequired);
                yAxisCanvas += extraSpaceRequired;
                legendTop += extraSpaceRequired;
                // legendBottom += extraSpaceRequired; 
            }

            // We've hacked at the axes; don't re-default them.
            StartVectorPlot(false);
            SetFontsAndThicknessesFromOptions(rOptions);
            AssignMarkersToSeries(rOptions);

            DataMinX = 0;
            DataMaxX = 1;
            DataMinY = 0;
            DataMaxY = 1;

            // Draw the scale
            DrawAxesOrEnlargeCanvas(rOptions.Title, new AxisDefinition("1-Specificity", AxisMode.Scale, ScaleType.Linear), new AxisDefinition("Sensitivity", AxisMode.Scale, ScaleType.Linear), true, false);

            // null effect diagonal
            using (Pen tenPenDiagonal = new Pen(ChartPreferences.MarkerTypes[10].LineColor, rOptions.AxisLineThickness))
            {
                DrawLineInCanvasCoordinates(tenPenDiagonal, xAxisCanvas, yAxisCanvas, xAxisCanvas + xExtCanvas, yAxisCanvas + yExtCanvas);
            }

            // get the offsets for the markers
            offx = xAxisCanvas;
            offy = yAxisCanvas;

            bool hideopt = !(rOptions.ShowOptimumCutOff);
            ComparisonValue showopt = rOptions.Showopts;
            ParameterBag results = new ParameterBag();
            IList<ParameterBag> allResults = new List<ParameterBag>();
            results.AddOutput("*datasets", allResults);
            for (int cs = 0; cs < definition.XSeries.Count; cs++)
            {
                ROCSeriesRecord thisData = seriesData[cs];
                DoubleSeries xs = definition.XSeries[cs].AsDoubleSeries;
                DoubleSeries ys = definition.YSeries[cs].AsDoubleSeries;
                double weight = rOptions.Weight;
                if (weight <= 0)
                    weight = 1.0;

                // Draw the legend for each series
                DrawMarkerInCanvasCoordinates(xAxisCanvas + LEGEND_MARKER_SIZE / 2.0, legendTop - (cs * legendSpacing) - markerMidlineOffset, LEGEND_MARKER_SIZE, definition.YSeries[cs].AsDoubleSeries);
                DrawStringLegendL(rOptions.SeriesTitles[cs], xAxisCanvas + 9 + LEGEND_MARKER_SIZE, legendTop - (cs * legendSpacing));

                int a;
                int b;
                int c;
                int d;
                double cutoff;
                double sens;
                if (!hideopt)
                {
                    // work out cutoff for max(weight*sens+spec)
                    double maxss = 0.0;
                    for (int r = 0; r < thisData.tdata.Length; r++)
                    {
                        cutoff = thisData.tdata[r];
                        a = CountValues(showopt, thisData.pdata, cutoff);
                        c = thisData.pdata.Length - a;
                        b = CountValues(showopt, thisData.adata, cutoff);
                        d = thisData.adata.Length - b;
                        sens = Convert.ToDouble(a) / Convert.ToDouble(a + c);
                        double spec = Convert.ToDouble(d) / Convert.ToDouble(b + d);
                        if (weight * sens + spec > maxss)
                        {
                            maxss = weight * sens + spec;
                            thisData.cutoff = cutoff;
                            thisData.a = a;
                            thisData.b = b;
                            thisData.c = c;
                            thisData.d = d;
                            thisData.sens = sens;
                            thisData.spec = spec;
                        }
                    }

                    // Cutoff calculator
                    thisData.comp = showopt;
                    if (rOptions.ShowCutOffCalculator)
                    {
                        string q = "ROC plot for " + rOptions.SeriesTitles[cs];
                        thisData = ShowCutoff(host, thisData, weight, q);
                    }
                    seriesData[cs] = thisData;
                }

                // make first mark
                cutoff = thisData.cutoff;
                a = 0;
                for (int r = 0; r < thisData.pdata.Length; r++)
                    a += CountValues(showopt, thisData.pdata, cutoff);
                c = thisData.pdata.Length - a;
                b = 0;
                for (int r = 0; r < thisData.adata.Length; r++)
                    b += CountValues(showopt, thisData.adata, cutoff);
                d = thisData.adata.Length - b;
                sens = Convert.ToDouble(a) / Convert.ToDouble(a + c);
                double mspec = 1.0 - Convert.ToDouble(d) / Convert.ToDouble(b + d);
                double x1 = offx + mspec * xExtCanvas;
                double y1 = offy + sens * yExtCanvas;

                int stps = thisData.tdata.Length;
                double[] rx = new double[stps];
                double[] ry = new double[stps];

                for (int r = 0; r < stps; r++)
                {
                    cutoff = thisData.tdata[r];
                    a = CountValuesSingleSided(showopt, thisData.pdata, cutoff);
                    c = thisData.pdata.Length - a;
                    b = CountValuesSingleSided(showopt, thisData.adata, cutoff);
                    d = thisData.adata.Length - b;
                    sens = Convert.ToDouble(a) / Convert.ToDouble(a + c);
                    ry[r] = sens;
                    mspec = 1.0 - Convert.ToDouble(d) / Convert.ToDouble(b + d);
                    rx[r] = mspec;
                }

                // Draw markers
                for (int r = 0; r < stps; r++)
                {
                    double x2 = offx + rx[r] * xExtCanvas;
                    double y2 = offy + ry[r] * yExtCanvas;
                    DrawMarkerInCanvasCoordinates(x2, y2, ys.MarkerDetails.MarkerSize, definition.YSeries[cs].AsDoubleSeries);
                }

                // Draw lines between markers
                double last_x2 = x1;
                double last_y2 = y1;
                for (int r = 0; r < stps; r++)
                {
                    double x2 = offx + rx[r] * xExtCanvas;
                    double y2 = offy + ry[r] * yExtCanvas;
                    if (r > 0 && (x2 != last_x2 || y2 != last_y2))
                        DrawLineInCanvasCoordinates(xs.MarkerDetails.LinePen, last_x2, last_y2, x2, y2);
                    last_x2 = x2;
                    last_y2 = y2;
                }

                // Mark cutoff point.  This is reversed if the chart requires reversal.
                double x = 1.0 - thisData.spec;
                double y = thisData.sens;
                if (showopt == ComparisonValue.LT || showopt == ComparisonValue.LE)
                {
                    x = 1.0 - x;
                    y = 1.0 - y;
                }
                DrawMarkerInChartCoordinates(x, y, rOptions.MarkerTypes[definition.XSeries.Count + cs].MarkerSize, rOptions.MarkerTypes[definition.XSeries.Count + cs]);

                thisData.auc = MathDbl.trapezoid_xy_roc(rx, ry, 0, stps);

                if (!hideopt)
                {
                    ParameterBag thisResults = new ParameterBag();
                    allResults.Add(thisResults);
                    // Wilcoxon estimate for AUC
                    // Hanley JA, mcNeil BJ, Radiology 143:29-36
                    //  Note that mwx and mwr are 1-based
                    double[] mwx = new double[thisData.pdata.Length + thisData.adata.Length + 1];
                    Array.Copy(thisData.pdata, 0, mwx, 1, thisData.pdata.Length);
                    Array.Copy(thisData.adata, 0, mwx, 1 + thisData.pdata.Length, thisData.adata.Length);
                    bool fault;
                    double u;
                    double zScrap;
                    double xfScrap;
                    double r1Scrap;
                    double[] mwrScrap;
                    NonParametric.MannWhitneyUTest(mwx, mwx.Length - 1, thisData.pdata.Length, thisData.adata.Length, out mwrScrap, out u, out zScrap, out xfScrap, out r1Scrap, out fault);
                    double theta;
                    double ll;
                    double ul;
                    double sew = 0;
                    if (fault)
                    {
                        theta = Constant.MISSING;
                        ll = Constant.MISSING;
                        ul = Constant.MISSING;
                    }
                    else
                    {
                        // if (thisData.pdata.Length * thisData.adata.Length - u > u)
                        //     u = thisData.pdata.Length * thisData.adata.Length - u;
                        theta = u / (thisData.pdata.Length * thisData.adata.Length);
                        // Q1 = theta / (2# - theta)
                        // Q2 = (2# * (theta ^ 2#)) / (1# + theta)
                        // sew = Sqr((theta * (1# - theta) + CDbl(rowsp(cs) - 1) * (Q1 - theta ^ 2#) + CDbl(rowsa(cs) - 1) * (Q2 - theta# ^ 2#)) / CDbl(rowsp(cs) * rowsa(cs)))
                        sew = DeLongSE(thisData.pdata, thisData.adata, theta);
                        if (sew == Constant.MISSING)
                        {
                            ll = Constant.MISSING;
                            ul = Constant.MISSING;
                        }
                        else
                        {
                            ll = theta - cit * sew;
                            ul = theta + cit * sew;
                        }
                    }
                    // end of Wilcoxon estimate
                    string warn;
                    thisResults.AddOutput("ti", rOptions.SeriesTitles[cs]);
                    thisResults.AddOutput("auc", host.RoundU(thisData.auc));
                    thisResults.AddOutput("theta", host.RoundU(theta));
                    thisResults.AddOutput("se", host.RoundU(sew));
                    thisResults.AddOutput("pc", Formatting.XRound(100.0 * (1.0 - P0), 2));
                    if (ll < 0.0)
                        ll = 0.0;
                    thisResults.AddOutput("ll", host.RoundU(ll));
                    if (ul > 1.0)
                        ul = 1.0;
                    thisResults.AddOutput("ul", host.RoundU(ul));
                    thisResults.AddOutput("cut", host.RoundU(thisData.cutoff));
                    thisResults.AddOutput("a", thisData.a.ToString(CultureInfo.InvariantCulture));
                    thisResults.AddOutput("b", thisData.b.ToString(CultureInfo.InvariantCulture));
                    thisResults.AddOutput("c", thisData.c.ToString(CultureInfo.InvariantCulture));
                    thisResults.AddOutput("d", thisData.d.ToString(CultureInfo.InvariantCulture));
                    // sensitivity CI
                    MathDbl.binci(Convert.ToDouble(thisData.a), Convert.ToDouble(thisData.a + thisData.c), out ll, out ul, GAMMA, out warn);
                    thisResults.AddOutput("senspc", Formatting.XRound(100.0 * (1.0 - P0), 2));
                    thisResults.AddOutput("sens", host.RoundU(thisData.sens));
                    thisResults.AddOutput("sensll", host.RoundU(ll));
                    thisResults.AddOutput("sensul", host.RoundU(ul) + warn);
                    // specificity CI
                    MathDbl.binci(Convert.ToDouble(thisData.d), Convert.ToDouble(thisData.d + thisData.b), out ll, out ul, GAMMA, out warn);
                    thisResults.AddOutput("specpc", Formatting.XRound(100.0 * (1.0 - P0), 2));
                    thisResults.AddOutput("spec", host.RoundU(thisData.spec));
                    thisResults.AddOutput("specll", host.RoundU(ll));
                    thisResults.AddOutput("specul", host.RoundU(ul) + warn);

                    //  BEWARE from this point on: a, b, c, d are integer, but divisions need to deal with floating-point.
                    // prevalence
                    double N = thisData.a + thisData.b + thisData.c + thisData.d;
                    double prevel = Convert.ToDouble(thisData.a + thisData.c) / N;

                    // ppv
                    double ptld;
                    double temp1; double temp2;
                    if (thisData.a + thisData.b > 0)
                    {
                        ptld = thisData.a / Convert.ToDouble(thisData.a + thisData.b);
                        temp1 = ptld * 100.0;
                        temp2 = Convert.ToInt64(ptld * 100.0) - Convert.ToInt64(prevel * 100.0);
                    }
                    else
                    {
                        ptld = Constant.MISSING;
                        temp1 = Constant.MISSING;
                        temp2 = Constant.MISSING;
                    }
                    thisResults.AddOutput("likely", host.RoundU(ptld));
                    // Clopper-Pearson CI
                    double pil; double piu;
                    MathDbl.binci(thisData.a, thisData.a + thisData.b, out pil, out piu, GAMMA, out warn);
                    thisResults.AddOutput("likely_from", host.RoundU(pil));
                    thisResults.AddOutput("likely_to", host.RoundU(piu) + warn);
                    // as percentage
                    thisResults.AddOutput("likely_pc", Formatting.XRound(temp1, 2));
                    if (pil != Constant.MISSING)
                    {
                        pil = 100.0 * pil;
                    }
                    else { pil = Constant.MISSING; }
                    thisResults.AddOutput("likely_from_pc", Formatting.XRound(pil, 2));
                    if (piu != Constant.MISSING)
                    {
                        piu = 100.0 * piu;
                    }
                    else { piu = Constant.MISSING; }
                    thisResults.AddOutput("likely_to_pc", Formatting.XRound(piu, 2));
                    // change
                    thisResults.AddOutput("likely_change", Formatting.XRound(temp2, 2));

                    // npv
                    double ptlng;
                    if (thisData.d + thisData.c > 0)
                    {
                        ptlng = thisData.d / Convert.ToDouble(thisData.d + thisData.c);
                        temp1 = ptlng * 100.0;
                        temp2 = Convert.ToInt32(ptlng * 100.0) - Convert.ToInt32((Convert.ToDouble(thisData.b + thisData.d) / N) * 100.0);
                    }
                    else
                    {
                        ptlng = Constant.MISSING;
                        temp1 = Constant.MISSING;
                        temp2 = Constant.MISSING;
                    }
                    thisResults.AddOutput("likely_negative", host.RoundU(ptlng));
                    // Clopper-Pearson CI
                    MathDbl.binci(thisData.d, thisData.d + thisData.c, out pil, out piu, GAMMA, out warn);
                    thisResults.AddOutput("likely_negative_from", host.RoundU(pil));
                    thisResults.AddOutput("likely_negative_to", host.RoundU(piu) + warn);
                    // as percentage
                    thisResults.AddOutput("likely_negative_pc", Formatting.XRound(temp1, 2));
                    if (pil != Constant.MISSING)
                    {
                        pil = 100.0 * pil;
                    }
                    else { pil = Constant.MISSING; }
                    thisResults.AddOutput("likely_negative_from_pc", Formatting.XRound(pil, 2));
                    if (piu != Constant.MISSING)
                    {
                        piu = 100.0 * piu;
                    }
                    else { piu = Constant.MISSING; }
                    thisResults.AddOutput("likely_negative_to_pc", Formatting.XRound(piu, 2));
                    // change
                    thisResults.AddOutput("likely_negative_change", Formatting.XRound(temp2, 2));

                    // p[dx] despite -ve test
                    double ptlnd;
                    if (thisData.d + thisData.c > 0)
                    {
                        ptlnd = 1.0 - (thisData.d / Convert.ToDouble(thisData.d + thisData.c));
                        temp1 = ptlnd * 100.0;
                        temp2 = Convert.ToInt32(ptlnd * 100.0) - Convert.ToInt32(prevel * 100.0);
                    }
                    else
                    {
                        ptlnd = Constant.MISSING;
                        temp1 = Constant.MISSING;
                        temp2 = Constant.MISSING;
                    }
                    thisResults.AddOutput("likely_despite", host.RoundU(ptlnd));
                    // Clopper-Pearson CI
                    MathDbl.binci(thisData.d, thisData.d + thisData.c, out pil, out piu, GAMMA, out warn);
                    thisResults.AddOutput("likely_despite_from", host.RoundU(Math.Min(1.0 - pil, 1.0 - piu)));
                    thisResults.AddOutput("likely_despite_to", host.RoundU(Math.Max(1.0 - pil, 1.0 - piu)) + warn);
                    // as percentage
                    thisResults.AddOutput("likely_despite_pc", Formatting.XRound(temp1, 2));
                    if (pil != Constant.MISSING)
                    {
                        pil = 100.0 * (1.0 - pil);
                    }
                    else { pil = Constant.MISSING; }
                    if (piu != Constant.MISSING)
                    {
                        piu = 100.0 * (1.0 - piu);
                    }
                    else { piu = Constant.MISSING; }
                    thisResults.AddOutput("likely_despite_from_pc", Formatting.XRound(Math.Min(pil, piu), 2));
                    thisResults.AddOutput("likely_despite_to_pc", Formatting.XRound(Math.Max(pil, piu), 2));
                    // change
                    thisResults.AddOutput("likely_despite_change", Formatting.XRound(temp2, 2));
                }
            }
            EndVectorPlot();
            return results;
        }

        private static int CountValues(ComparisonValue showopt, double[] data, double cutoff)
        {
            int a = 0;
            for (int j = 0; j < data.Length; j++)
            {
                switch (showopt)
                {
                    case ComparisonValue.LT:
                        if (data[j] < cutoff)
                            a++;
                        break;
                    case ComparisonValue.LE:
                        if (data[j] <= cutoff)
                            a++;
                        break;
                    case ComparisonValue.GT:
                        if (data[j] > cutoff)
                            a++;
                        break;
                    default:
                        if (data[j] >= cutoff)
                            a++;
                        break;
                }
            }
            return a;
        }

        private static int CountValuesSingleSided(ComparisonValue showopt, double[] data, double cutoff)
        {
            int a = 0;
            for (int j = 0; j < data.Length; j++)
            {
                switch (showopt)
                {
                    case ComparisonValue.LT:
                    case ComparisonValue.GT:
                        if (data[j] > cutoff)
                            a++;
                        break;
                    default:
                        if (data[j] >= cutoff)
                            a++;
                        break;
                }
            }
            return a;
        }

        private static double DeLongSE(double[] x, double[] y, double auc)
        {
            double[] v10 = new double[x.Length];
            double[] v01 = new double[y.Length];
            for (int i = 0; i < x.Length; i++)
            {
                for (int j = 0; j < y.Length; j++)
                    v10[i] += DeLongPsi(x[i], y[j]);
                v10[i] /= y.Length;
            }
            for (int j = 0; j < y.Length; j++)
            {
                for (int i = 0; i < x.Length; i++)
                    v01[j] += DeLongPsi(x[i], y[j]);
                v01[j] /= x.Length;
            }
            double s10 = 0.0;
            double s01 = 0.0;
            for (int i = 0; i < x.Length; i++)
                s10 += Math.Pow((v10[i] - auc), 2.0);
            s10 /= x.Length - 1;
            for (int j = 0; j < y.Length; j++)
                s01 += Math.Pow((v01[j] - auc), 2.0);
            s01 /= y.Length - 1;
            double var = s10 / x.Length + s01 / y.Length;
            return var < 0.0 ? Constant.MISSING : Math.Sqrt(var);
        }

        private static double DeLongPsi(double x, double y)
        {
            if (y == x)
                return 0.5;
            return (y < x) ? 1.0 : 0.0;
        }

        ///  <summary>
        ///  Cause the host to amend the thisData record in-place with any revisions to the cutoff data.
        ///  </summary>
        ///  <param name="host"></param>
        ///  <param name="thisData"></param>
        ///  <param name="Weight"></param>
        ///  <param name="ti"></param>
        ///  <remarks></remarks>
        private static ROCSeriesRecord ShowCutoff(ITemplateHost host, ROCSeriesRecord thisData, double Weight, string ti)
        {
            ROCCutoff payload = new ROCCutoff { SeriesRecord = thisData, Weight = Weight, Title = ti };
            host.Amend(payload, null);
            return payload.SeriesRecord;
        }
    }
}
