using System;
using System.Collections.Generic;
using StatsDirect.Builtins;
using StatsDirect.Charting.Renderer;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.Templates;
using StatsDirect.Utilities;

namespace StatsDirect.Charting
{
    /// <summary>
    /// This has two purposes (TODO: one too many): know where to get the correct implementation of IChartRenderer for any given definition, and act as a one-stop shop for static calls that were previously in ChartRenderer itself to render charts that are used within the codebase.
    /// </summary>
    public static class ChartRendererFactory
    {
        // Configuration as to what canvas we're using, and hence the kind of image that will result from a chart being plotted.
        private static readonly ICanvasFactory CANVAS_FACTORY = new EmfCanvasFactory();

        public static string PlotBiasMAAndReturnRtf(ITemplateHost host, double[] x, double[] yy, double[] yw, int rows, string xtxt, double[] cl, double[] cu, double cco, double cit, double rmh, Transformation xform, bool diagonal)
        {
            ChartDefinition cd = new ChartDefinition { ScaleParameters = CreateScaleParameters(ScaleType.Linear, ScaleType.Linear) };
            using (ChartRenderer ch = new ChartRenderer(cd, CANVAS_FACTORY))
            {
                ch.Plot_Bias_MA(host, x, yy, yw, rows, xtxt, cl, cu, cco, cit, rmh, xform, diagonal);
                return Render(ch);
            }
        }

        public static string PlotCorrelationAndReturnRtf(int k, string[] title, double[] odr, double[] odrl, double[] odru, double[] gn, CorrelationRowType[] pg, string cap, string qid, Transformation xform, bool isDifference)
        {
            ChartDefinition cd = new ChartDefinition { ScaleParameters = CreateScaleParameters(Transformation.Log == xform ? ScaleType.Log10 : ScaleType.Linear, ScaleType.Linear) };
            using (ChartRenderer ch = new ChartRenderer(cd, CANVAS_FACTORY))
            {
                ch.PlotCorrelation(k, title, odr, odrl, odru, gn, pg, cap, qid, xform, isDifference);
                return Render(ch);
            }
        }

        public static string PlotCox2AndReturnRtf(int[] gn, int igroups, double[] xp, double[] yp, ColumnData[] cdat1, int groupid)
        {
            ChartDefinition cd = new ChartDefinition { ScaleParameters = CreateScaleParameters(ScaleType.Linear, ScaleType.Linear) };
            cd.AddXSeries(xp, null);
            cd.AddYSeries(yp, null);
            using (ChartRenderer ch = new ChartRenderer(cd, CANVAS_FACTORY))
            {
                ch.PlotCox2(gn, igroups, xp, yp, cdat1, groupid);
                return Render(ch);
            }
        }

        public static string PlotEffectAndReturnRtf(ITemplateHost host, int k, double[] cn, double[] en, string[] title, double rmh, double ll, double ul, double cco, double[] odr, double[] odrl, double[] odru, string cap, int pbias, string qid)
        {
            ChartDefinition cd = new ChartDefinition { ScaleParameters = CreateScaleParameters(ScaleType.Linear, ScaleType.Linear) };
            using (ChartRenderer ch = new ChartRenderer(cd, CANVAS_FACTORY))
            {
                ch.PlotEffect(host, k, cn, en, title, rmh, ll, ul, cco, odr, odrl, odru, cap, pbias, qid);
                return Render(ch);
            }
        }

        public static string PlotLAbbeAndReturnRtf(int k, double[,] o, double rmh)
        {
            using (ChartRenderer ch = new ChartRenderer(new ChartDefinition { ScaleParameters = CreateScaleParameters(ScaleType.Linear, ScaleType.Linear) }, CANVAS_FACTORY))
            {
                ch.PlotLAbbe(k, o, rmh);
                return Render(ch);
            }
        }

        public static string PlotLinearizedEstimationAndReturnRtf(double[] xData, double[] yData, string title, int model, double a, double b, string xAxisTitle, string yAxisTitle, bool shouldUseColour)
        {
            ChartDefinition cd = new ChartDefinition { ScaleParameters = CreateScaleParameters(ScaleType.Linear, ScaleType.Linear) };
            cd.AddYSeries(yData, yAxisTitle);
            cd.AddXSeries(xData, xAxisTitle);
            AgreementOptions aOptions = new AgreementOptions(shouldUseColour) { mxd = yData, av = xData };
            cd.ChartOptions = aOptions;
            using (ChartRenderer ch = new ChartRenderer(cd, CANVAS_FACTORY))
            {
                ch.PlotLinearizedEstimation(title, model, a, b, xAxisTitle, yAxisTitle);
                return Render(ch);
            }
        }

        public static string PlotLinearRegressionAndMaybeSeCiOrPredictionIntervalAndReturnRtf(double[] xData, double[] yData, string title, double slope, double intercept, bool fullWidth, string xAxisTitle, string yAxisTitle, double pert, int nx, double ms, double sumx, double ssx, bool isPredictionInterval)
        {
            ChartDefinition cd = new ChartDefinition {ChartType = ChartType.LinearRegression};
            DoubleSeries ys = new DoubleSeries(yData, yAxisTitle);
            cd.AddYSeriesAt(ys, 0);
            DoubleSeries xs = new DoubleSeries(xData, xAxisTitle);
            cd.AddXSeriesAt(xs, 0);
            using (LinearRegressionChartRenderer ch = (LinearRegressionChartRenderer)ChartRendererFor(cd))
            {
                double maxpcon = double.MinValue;
                double minpcon = double.MaxValue;
                if (pert != 0)
                {
                    double calcx;
                    for (calcx = xs.Min; calcx <= xs.Max; calcx += (xs.Max - xs.Min) / 20.0)
                    {
                        double calcy = slope * calcx + intercept;
                        double sey = Math.Sqrt(ms * (1.0 + (1.0 / Convert.ToDouble(nx) + Math.Pow(calcx - sumx / Convert.ToDouble(nx), 2.0) / ssx)));
                        double pconu = calcy + sey * pert;
                        double pconl = calcy - sey * pert;
                        if (pconu > maxpcon)
                            maxpcon = pconu;
                        if (pconl < minpcon)
                            minpcon = pconl;
                    }
                }

                ch.PlotLinearRegressionAndMaybeSeCiOrPredictionInterval(title, slope, intercept, fullWidth, xAxisTitle, yAxisTitle, pert, nx, ms, sumx, ssx, isPredictionInterval, Math.Min(ys.Min, minpcon), Math.Max(ys.Max, maxpcon));
                return Render(ch);
            }
        }

        public static string PlotLogitAndReturnRtf(double[] x, double[] y, string title, int model, double t, double sw, double s1, double a, double b, string xAxisTitle, string yAxisTitle, bool useLogScale)
        {
            ChartDefinition cd = new ChartDefinition { ScaleParameters = CreateScaleParameters(useLogScale ? ScaleType.Log10 : ScaleType.Linear, ScaleType.Linear) };
            cd.AddYSeries(y, yAxisTitle);
            cd.AddXSeries(x, xAxisTitle);
            using (ChartRenderer ch = new ChartRenderer(cd, CANVAS_FACTORY))
            {
                ch.PlotLogit(title, model, t, sw, s1, a, b, xAxisTitle, yAxisTitle, useLogScale);
                return Render(ch);
            }
        }

        public static string PlotMHAndReturnRtf(int k, double[,] o, double[] odw, string[] title, double rmh, double ll, double ul, double cco, double[] odr, double[] odrl, double[] odru, bool[] lerr, bool[] uerr, string cap, int pbias, string qid)
        {
            ChartDefinition cd = new ChartDefinition { ScaleParameters = CreateScaleParameters(ScaleType.Log10, ScaleType.Linear) };
            using (ChartRenderer ch = new ChartRenderer(cd, CANVAS_FACTORY))
            {
                ch.Plot_MH(k, o, odw, title, rmh, ll, ul, cco, odr, odrl, odru, lerr, uerr, cap, pbias, qid);
                return Render(ch);
            }
        }

        public static string PlotMHRDAndReturnRtf(int k, double[,] o, double[] odw, string[] title, double rmh, double ll, double ul, double cco, double[] odr, double[] odrl, double[] odru, bool[] lerr, bool[] uerr, string cap, int pbias, string qid)
        {
            using (ChartRenderer ch = new ChartRenderer(new ChartDefinition { ScaleParameters = CreateScaleParameters(ScaleType.Linear, ScaleType.Linear) }, CANVAS_FACTORY))
            {
                ch.Plot_MHRiskDifference(k, odw, title, rmh, ll, ul, cco, odr, odrl, odru, lerr, uerr, cap, pbias, qid);
                return Render(ch);
            }
        }

        public static string PlotNormalAndReturnRtf(double[] y, string title, bool shouldUseColour)
        {
            NormalOptions nOptions = new NormalOptions(shouldUseColour) { ShouldScaleZ = true, Method = NormalOptions.ScoreMethod.Blom };
            ChartDefinition cd = new ChartDefinition { ChartOptions = nOptions, ChartType = ChartType.Normal };
            cd.XSeries.Add(new DoubleSeries(y, title));

            using (NormalChartRenderer ch = (NormalChartRenderer)ChartRendererFor(cd))
            {
                ch.PlotNormal(y);
                return Render(ch);
            }
        }

        public static string PlotPolynomialRegressionAndReturnRtf(double[] xData, double[] yData, string title, int mode, double[,] xtxi, double[] bd, double rss, int nx, int p, double gamma, string xAxisTitle, string yAxisTitle, bool shouldUseColour)
        {
            AgreementOptions aOptions = new AgreementOptions(shouldUseColour) { mxd = yData, av = xData };
            ChartDefinition cd = new ChartDefinition { ChartOptions = aOptions, ScaleParameters = CreateScaleParameters(ScaleType.Linear, ScaleType.Linear) };
            cd.AddYSeries(yData, yAxisTitle);
            cd.AddXSeries(xData, xAxisTitle);
            using (ChartRenderer ch = new ChartRenderer(cd, CANVAS_FACTORY))
            {
                ch.PlotPolynomialRegression(title, mode, xtxi, bd, rss, nx, p, gamma, xAxisTitle, yAxisTitle);
                return Render(ch);
            }
        }

        public static string PlotTiesAndReturnRtf(double[] x, double[] y, int nx, double lla, double ula, double gamma, string v0Title, string v1Title, double mean)
        {
            using (ChartRenderer ch = new ChartRenderer(new ChartDefinition { ScaleParameters = CreateScaleParameters(ScaleType.Linear, ScaleType.Linear) }, CANVAS_FACTORY))
            {
                ch.PlotTies(x, y, nx, lla, ula, gamma, v0Title, v1Title, mean);
                return Render(ch);
            }
        }

        public static string PlotXYAndReturnRtf(double[] x, double[] y, string xtxt, string ytxt, string title, bool zPlot, DataMinMax minMaxY, bool useCalculatedScalesEvenWithDefinition)
        {
            using (ChartRenderer ch = new ChartRenderer(new ChartDefinition { ScaleParameters = CreateScaleParameters(ScaleType.Linear, ScaleType.Linear) }, CANVAS_FACTORY))
            {
                ch.PlotXY(x, y, xtxt, ytxt, title, zPlot, minMaxY, useCalculatedScalesEvenWithDefinition, ChartAreaShape.Default);
                return Render(ch);
            }
        }

        public static string PlotXY0To1AndReturnRtf(double[] x, double[] y, string xtxt, string ytxt, string title, bool zPlot, DataMinMax minMaxY, bool useCalculatedScalesEvenWithDefinition)
        {
            using (ChartRenderer ch = new ChartRenderer(new ChartDefinition { ScaleParameters = CreateScaleParameters(ScaleType.Linear, ScaleType.Linear) }, CANVAS_FACTORY))
            {
                ch.PlotXY0To1(x, y, xtxt, ytxt, title, zPlot, minMaxY, useCalculatedScalesEvenWithDefinition, ChartAreaShape.Default);
                return Render(ch);
            }
        }

        public static string PlotXYRAndReturnRtf(double[,] x, double[,,] y, int ng, int[] gn, int[,] nr, double[] b, double[] a, string xtxt, string ytxt, string title, string[] bnam, MinMax minMax)
        {
            using (ChartRenderer ch = new ChartRenderer(new ChartDefinition { ScaleParameters = CreateScaleParameters(ScaleType.Linear, ScaleType.Linear) }, CANVAS_FACTORY))
            {
                ch.PlotXYR(x, y, ng, gn, nr, b, a, xtxt, ytxt, title, bnam, minMax.MinX, minMax.MaxX, minMax.MinY, minMax.MaxY);
                return Render(ch);
            }
        }

        public static string PlotXYZAndReturnRtf(double[] x, double[] y, double[] z, string xtxt, string ytxt, string title, bool zPlot, DataMinMax minMaxY)
        {
            using (ChartRenderer ch = new ChartRenderer(new ChartDefinition { ScaleParameters = CreateScaleParameters(ScaleType.Linear, ScaleType.Linear) }, CANVAS_FACTORY))
            {
                ch.PlotXYZ(x, y, z, 1, x.Length - 1, xtxt, ytxt, title, zPlot, minMaxY, new MarkerType { MarkerShape = MarkerShape.Circle, IsMarkerFilled = false, MarkerColor = AbstractChartRenderer.GrBlack });
                return Render(ch);
            }
        }

        public static string SurvivalOrHazardPlot(CoxP[] z, int iobs, int istrata, CoxPlotMode plotMode, int igroups, int groupid, bool grouped, bool stratified, double[,,] arr3, ColumnData[] cdat1, bool useTic, bool useMarker, int[] gn)
        {
            using (ChartRenderer ch = new ChartRenderer(new ChartDefinition { ScaleParameters = CreateScaleParameters(ScaleType.Linear, ScaleType.Linear) }, CANVAS_FACTORY))
            {
                ch.PlotCox1(z, iobs, istrata, plotMode, igroups, groupid, grouped, stratified, arr3, cdat1, useTic, useMarker, gn);
                return Render(ch);
            }
        }

        public static IList<string> x_plgraph(double[,] h, double[,] s, double[,] stime, int[,] dead, int groups, int[] cnx, string[] glab, bool tic, bool marker)
        {
            IList<string> outputImages = new List<string>();
            int gx = stime.GetUpperBound(0);
            double[,] x = new double[gx + 1, groups + 1];
            double[,] y = new double[gx + 1, groups + 1];
            for (int plotMode = 1; plotMode <= 5; plotMode++)
            {
                string xAxisTitle;
                string yAxisTitle;
                string title;
                switch (plotMode)
                {
                    case 1:
                        xAxisTitle = "Times";
                        yAxisTitle = "Survivor";
                        title = "Survival Plot (PL estimates)";
                        break;
                    case 2:
                        xAxisTitle = "Times";
                        yAxisTitle = "Hazard";
                        title = "Hazard Plot";
                        break;
                    case 3:
                        xAxisTitle = "Log Times";
                        yAxisTitle = "Log Hazard";
                        title = "Log Hazard Plot";
                        break;
                    case 4:
                        xAxisTitle = "Log Times";
                        yAxisTitle = "Z (Survivor)";
                        title = "Lognormal Survival Plot";
                        break;
                    case 5:
                        xAxisTitle = "Times";
                        yAxisTitle = "Hazard / Time";
                        title = "Hazard Rate Plot";
                        break;
                    default:
                        throw new Exception("Unexpected j3");
                }

                for (int k = 1; k <= groups; k++)
                {
                    int nx = 0;
                    for (int j = 1; j <= cnx[k]; j++)
                    {
                        switch (plotMode)
                        {
                            case 1:
                                nx++;
                                x[nx, k] = stime[j, k];
                                y[nx, k] = s[j, k];
                                break;
                            case 2:
                                if (h[j, k] != Constant.MISSING)
                                {
                                    nx++;
                                    x[nx, k] = stime[j, k];
                                    y[nx, k] = h[j, k];
                                }
                                break;
                            case 3:
                                if (h[j, k] != Constant.MISSING && stime[j, k] > 0 & h[j, k] > 0)
                                {
                                    nx++;
                                    x[nx, k] = Math.Log(stime[j, k]);
                                    y[nx, k] = Math.Log(h[j, k]);
                                }
                                break;
                            case 4:
                                double q = PDF.gauinv(s[j, k], out int fault);
                                if (fault == 0 && stime[j, k] > 0)
                                {
                                    nx++;
                                    x[nx, k] = Math.Log(stime[j, k]);
                                    y[nx, k] = q;
                                }
                                break;
                            case 5:
                                if (h[j, k] != Constant.MISSING && stime[j, k] != 0)
                                {
                                    nx++;
                                    x[nx, k] = stime[j, k];
                                    y[nx, k] = h[j, k] / stime[j, k];
                                }
                                break;
                        }
                    }
                    cnx[k] = nx;
                }
                // Plot the results
                using (ChartRenderer ch = new ChartRenderer(new ChartDefinition { ScaleParameters = CreateScaleParameters(ScaleType.Linear, ScaleType.Linear) }, CANVAS_FACTORY))
                {
                    ch.x_plGraphInternal(dead, groups, cnx, glab, tic, marker, x, y, plotMode, xAxisTitle, yAxisTitle, title);
                    outputImages.Add(Render(ch));
                }
            }
            return outputImages;
        }

        public static IChartRenderer ChartRendererFor(ChartDefinition chartDefinition)
        {
            switch (chartDefinition.ChartType)
            {
                case ChartType.AgreementPair:
                    return new AgreementPairChartRenderer(chartDefinition, CANVAS_FACTORY);
                case ChartType.Bar:
                    return new BarChartRenderer(chartDefinition, CANVAS_FACTORY);
                case ChartType.BoxWhisker:
                    return new BoxWhiskerChartRenderer(chartDefinition, CANVAS_FACTORY);
                case ChartType.Control:
                    return new ControlChartRenderer(chartDefinition, CANVAS_FACTORY);
                case ChartType.ErrorBar:
                    return new ErrorBarChartRenderer(chartDefinition, CANVAS_FACTORY);
                case ChartType.Forest:
                    return new ForestChartRenderer(chartDefinition, CANVAS_FACTORY);
                case ChartType.Gini:
                    return new GiniChartRenderer(chartDefinition, CANVAS_FACTORY);
                case ChartType.Histogram:
                    return new HistogramChartRenderer(chartDefinition, CANVAS_FACTORY);
                case ChartType.Ladder:
                    return new LadderChartRenderer(chartDefinition, CANVAS_FACTORY);
                case ChartType.LinearRegression:
                    return new LinearRegressionChartRenderer(chartDefinition, CANVAS_FACTORY);
                case ChartType.LineXY:
                    return new ScatterChartRenderer(chartDefinition, CANVAS_FACTORY);
                case ChartType.Normal:
                    return new NormalChartRenderer(chartDefinition, CANVAS_FACTORY);
                case ChartType.Pyramid:
                    return new PyramidChartRenderer(chartDefinition, CANVAS_FACTORY);
                case ChartType.ROC:
                    return new RocChartRenderer(chartDefinition, CANVAS_FACTORY);
                case ChartType.ScatterXY:
                    return new ScatterChartRenderer(chartDefinition, CANVAS_FACTORY);
                case ChartType.Spread:
                    return new SpreadChartRenderer(chartDefinition, CANVAS_FACTORY);
                case ChartType.StackedBar:
                case ChartType.StackedBar100Percent:
                    return new BarChartRenderer(chartDefinition, CANVAS_FACTORY);
                case ChartType.Survival:
                    return new SurvivalChartRenderer(chartDefinition, CANVAS_FACTORY);
                default:
                    return new NotSetChartRenderer(chartDefinition, CANVAS_FACTORY);
            }
        }

        private static string Render(IChartRenderer ch)
        {
            return RtfImageRenderer.ImageStreamToRtf(ch.GetImageStream(), (int)ch.ImageWidth, (int)ch.ImageHeight);
        }

        private static string Render(ChartRenderer ch)
        {
            return RtfImageRenderer.ImageStreamToRtf(ch.GetImageStream(), (int)ch.ImageWidth, (int)ch.ImageHeight);
        }

        private static ScaleParameters CreateScaleParameters(ScaleType xScaleType, ScaleType yScaleType)
        {
            return new ScaleParameters
            {
                X = new AxisScaleParameters { ScaleType = xScaleType },
                Y = new AxisScaleParameters { ScaleType = yScaleType }
            };
        }
    }
}
