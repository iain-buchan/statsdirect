using StatsDirect.Builtins;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.Templates;
using StatsDirect.Utilities;
using System;
using System.Collections.Generic;

namespace StatsDirect.Charting
{
    /// <summary>
    /// This has two purposes (TODO: one too many): know where to get the correct implementation of IChartRenderer for any given definition, and act as a one-stop shop for static calls that were previously in ChartRenderer itself to render charts that are used within the codebase.
    /// </summary>
    public static class ChartRendererFactory
    {
        public static string PlotBiasMAAndReturnRtf(ITemplateHost host, double[] x, double[] yy, double[] yw, int rows, string xtxt, double[] cl, double[] cu, double cco, double cit, double rmh, Transformation xform, bool diagonal)
        {
            using (ChartRenderer ch = (ChartRenderer)ChartRendererFor(new ChartDefinition()))
            {
                ch.Plot_Bias_MA(host, x, yy, yw, rows, xtxt, cl, cu, cco, cit, rmh, xform, diagonal);
                return Render(ch);
            }
        }

        public static string PlotCorrelationAndReturnRtf(int k, string[] title, double[] odr, double[] odrl, double[] odru, double[] gn, int[] pg, string cap, string qid, Transformation xform, bool isDifference)
        {
            using (ChartRenderer ch = (ChartRenderer)ChartRendererFor(new ChartDefinition()))
            {
                ch.PlotCorrelation(k, title, odr, odrl, odru, gn, pg, cap, qid, xform, isDifference);
                return Render(ch);
            }
        }

        public static string PlotCox2AndReturnRtf(int[] gn, int igroups, double[] xp, double[] yp, ColumnData[] cdat1, int groupid)
        {
            ChartDefinition cd = new ChartDefinition();
            cd.AddXSeries(xp, null);
            cd.AddYSeries(yp, null);
            using (ChartRenderer ch = (ChartRenderer)ChartRendererFor(cd))
            {
                ch.PlotCox2(gn, igroups, xp, yp, cdat1, groupid);
                return Render(ch);
            }
        }

        public static string PlotEffectAndReturnRtf(ITemplateHost host, int k, double[] cn, double[] En, string[] title, double rmh, double ll, double ul, double cco, double[] odr, double[] odrl, double[] odru, string cap, int pbias, string qid)
        {
            using (ChartRenderer ch = (ChartRenderer)ChartRendererFor(new ChartDefinition()))
            {
                ch.PlotEffect(host, k, cn, En, title, rmh, ll, ul, cco, odr, odrl, odru, cap, pbias, qid);
                return Render(ch);
            }
        }

        public static string PlotLAbbeAndReturnRtf(int k, double[,] o, double rmh)
        {
            using (ChartRenderer ch = (ChartRenderer)ChartRendererFor(new ChartDefinition()))
            {
                ch.PlotLAbbe(k, o, rmh);
                return Render(ch);
            }
        }

        public static string PlotLinearizedEstimationAndReturnRtf(double[] xData, double[] yData, string title, int model, double a, double b, string XAxisTitle, string YAxisTitle, bool shouldUseColour)
        {
            ChartDefinition cd = new ChartDefinition();
            cd.AddYSeries(yData, YAxisTitle);
            cd.AddXSeries(xData, XAxisTitle);
            AgreementOptions aOptions = new AgreementOptions(shouldUseColour) { mxd = yData, av = xData };
            cd.ChartOptions = aOptions;
            using (ChartRenderer ch = (ChartRenderer)ChartRendererFor(cd))
            {
                ch.PlotLinearizedEstimation(title, model, a, b, XAxisTitle, YAxisTitle);
                return Render(ch);
            }
        }

        public static string PlotLinearRegressionAndMaybeSeCiOrPredictionIntervalAndReturnRtf(double[] xData, double[] yData, string title, double slope, double intercept, bool fullWidth, string xAxisTitle, string yAxisTitle, double PERT, int nx, double MS, double SUMX, double SSX, bool isPredictionInterval)
        {
            ChartDefinition cd = new ChartDefinition();
            cd.AddYSeries(yData, yAxisTitle);
            cd.AddXSeries(xData, xAxisTitle);
            using (ChartRenderer ch = (ChartRenderer)ChartRendererFor(cd))
            {
                double maxpcon = double.MinValue;
                double minpcon = double.MaxValue;
                if (PERT != 0)
                {
                    double calcx;
                    for (calcx = ch.DataMinX; calcx <= ch.DataMaxX; calcx += (ch.DataMaxX - ch.DataMinX) / 20.0)
                    {
                        double calcy = slope * calcx + intercept;
                        double sey = Math.Sqrt(MS * (1.0 + (1.0 / Convert.ToDouble(nx) + Math.Pow((calcx - (SUMX / Convert.ToDouble(nx))), 2.0) / SSX)));
                        double pconu = calcy + (sey * PERT);
                        double pconl = calcy - (sey * PERT);
                        if (pconu > maxpcon)
                            maxpcon = pconu;
                        if (pconl < minpcon)
                            minpcon = pconl;
                    }
                }
                if (maxpcon > ch.DataMaxY)
                    ch.DataMaxY = maxpcon;
                if (minpcon < ch.DataMinY)
                    ch.DataMinY = minpcon;

                ch.PlotLinearRegressionAndMaybeSeCiOrPredictionInterval(title, slope, intercept, fullWidth, xAxisTitle, yAxisTitle, PERT, nx, MS, SUMX, SSX, isPredictionInterval);
                return Render(ch);
            }
        }

        public static string PlotLogitAndReturnRtf(double[] x, double[] y, string title, int model, double t, double sw, double s1, double a, double b, string xAxisTitle, string yAxisTitle, bool useLogScale)
        {
            ChartDefinition cd = new ChartDefinition();
            cd.AddYSeries(y, yAxisTitle);
            cd.AddXSeries(x, xAxisTitle);
            cd.ScaleParameters.X.ScaleType = useLogScale ? ScaleType.Log10 : ScaleType.Linear;
            using (ChartRenderer ch = (ChartRenderer)ChartRendererFor(cd))
            {
                ch.PlotLogit(title, model, t, sw, s1, a, b, xAxisTitle, yAxisTitle);
                return Render(ch);
            }
        }

        public static string PlotMHAndReturnRtf(int k, double[,] o, double[] odw, string[] title, double rmh, double ll, double ul, double cco, double[] odr, double[] odrl, double[] odru, bool[] lerr, bool[] uerr, string cap, int pbias, string qid, out bool ifault)
        {
            using (ChartRenderer ch = (ChartRenderer)ChartRendererFor(new ChartDefinition()))
            {
                ch.Plot_MH(k, o, odw, title, rmh, ll, ul, cco, odr, odrl, odru, lerr, uerr, cap, pbias, qid, out ifault);
                return Render(ch);
            }
        }

        public static string PlotMHRDAndReturnRtf(int k, double[,] o, double[] odw, string[] title, double rmh, double ll, double ul, double cco, double[] odr, double[] odrl, double[] odru, bool[] lerr, bool[] uerr, string cap, int pbias, string qid, out bool ifault)
        {
            using (ChartRenderer ch = (ChartRenderer)ChartRendererFor(new ChartDefinition()))
            {
                ch.Plot_MHRD(k, odw, title, rmh, ll, ul, cco, odr, odrl, odru, lerr, uerr, cap, pbias, qid, out ifault);
                return Render(ch);
            }
        }

        public static string PlotNormalAndReturnRtf(double[] y, string title, bool shouldUseColour)
        {
            NormalOptions nOptions = new NormalOptions(shouldUseColour) { ShouldScaleZ = true, Method = NormalOptions.ScoreMethod.Blom };
            ChartDefinition cd = new ChartDefinition { ChartOptions = nOptions };
            cd.XSeries.Add(new DoubleSeries(y, title));

            using (ChartRenderer ch = (ChartRenderer)ChartRendererFor(cd))
            {
                ch.PlotNormal(y);
                return Render(ch);
            }
        }

        public static string PlotPolynomialRegressionAndReturnRtf(double[] xData, double[] yData, string title, int mode, double[,] xtxi, double[] bd, double rss, int nx, int P, double gamma, string xAxisTitle, string yAxisTitle, bool shouldUseColour)
        {
            AgreementOptions aOptions = new AgreementOptions(shouldUseColour) { mxd = yData, av = xData };
            ChartDefinition cd = new ChartDefinition { ChartOptions = aOptions };
            cd.AddYSeries(yData, yAxisTitle);
            cd.AddXSeries(xData, xAxisTitle);
            using (ChartRenderer ch = (ChartRenderer)ChartRendererFor(cd))
            {
                ch.PlotPolynomialRegression(title, mode, xtxi, bd, rss, nx, P, gamma, xAxisTitle, yAxisTitle);
                return Render(ch);
            }
        }

        public static string PlotTiesAndReturnMetafile(double[] x, double[] y, int nx, double lla, double ula, double GAMMA, string v0Title, string v1Title, double mean)
        {
            using (ChartRenderer ch = (ChartRenderer)ChartRendererFor(new ChartDefinition()))
            {
                ch.PlotTies(x, y, nx, lla, ula, GAMMA, v0Title, v1Title, mean);
                return Render(ch);
            }
        }

        public static string PlotXYAndReturnRtf(double[] x, double[] y, string xtxt, string ytxt, string title, bool zPlot, DataMinMax minMaxY, bool useCalculatedScalesEvenWithDefinition)
        {
            using (ChartRenderer ch = (ChartRenderer)ChartRendererFor(new ChartDefinition()))
            {
                ch.PlotXY(x, y, xtxt, ytxt, title, zPlot, minMaxY, useCalculatedScalesEvenWithDefinition);
                return Render(ch);
            }
        }

        public static string PlotXY0To1AndReturnRtf(double[] x, double[] y, string xtxt, string ytxt, string title, bool zPlot, DataMinMax minMaxY, bool useCalculatedScalesEvenWithDefinition)
        {
            using (ChartRenderer ch = (ChartRenderer)ChartRendererFor(new ChartDefinition()))
            {
                ch.PlotXY0To1(x, y, xtxt, ytxt, title, zPlot, minMaxY, useCalculatedScalesEvenWithDefinition);
                return Render(ch);
            }
        }

        public static string PlotXYRAndReturnRtf(double[,] x, double[,,] y, int ng, int[] gn, int[,] nr, double[] b, double[] a, string xtxt, string ytxt, string title, string[] bnam, MinMax minMax)
        {
            using (ChartRenderer ch = (ChartRenderer)ChartRendererFor(new ChartDefinition()))
            {
                ch.DataMinX = minMax.MinX;
                ch.DataMaxX = minMax.MaxX;
                ch.DataMinY = minMax.MinY;
                ch.DataMaxY = minMax.MaxY;
                ch.PlotXYR(x, y, ng, gn, nr, b, a, xtxt, ytxt, title, bnam);
                return Render(ch);
            }
        }

        public static string PlotXYZAndReturnRtf(double[] x, double[] y, double[] z, string xtxt, string ytxt, string title, bool zPlot, DataMinMax minMaxY)
        {
            using (ChartRenderer ch = (ChartRenderer)ChartRendererFor(new ChartDefinition()))
            {
                // TODO: Shouldn't need to reference Pens here.
                ch.PlotXYZ(x, y, z, 1, x.Length - 1, xtxt, ytxt, title, zPlot, minMaxY, MarkerShape.Circle, false, System.Drawing.Pens.Black, null);
                return Render(ch);
            }
        }

        public static string SurvivalOrHazardPlot(CoxP[] z, int iobs, int istrata, ChartRenderer.CoxPlotMode plotMode, int igroups, int groupid, bool grouped, bool stratified, double[,,] ARR3, ColumnData[] cdat1, bool use_tic, bool use_marker, int[] gn)
        {
            using (ChartRenderer ch = (ChartRenderer)ChartRendererFor(new ChartDefinition()))
            {
                ch.PlotCox1(z, iobs, istrata, plotMode, igroups, groupid, grouped, stratified, ARR3, cdat1, use_tic, use_marker, gn);
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
                                nx = nx + 1;
                                x[nx, k] = stime[j, k];
                                y[nx, k] = s[j, k];
                                break;
                            case 2:
                                if (h[j, k] != Constant.MISSING)
                                {
                                    nx = nx + 1;
                                    x[nx, k] = stime[j, k];
                                    y[nx, k] = h[j, k];
                                }
                                break;
                            case 3:
                                if (h[j, k] != Constant.MISSING & stime[j, k] > 0 & h[j, k] > 0)
                                {
                                    nx = nx + 1;
                                    x[nx, k] = Math.Log(stime[j, k]);
                                    y[nx, k] = Math.Log(h[j, k]);
                                }
                                break;
                            case 4:
                                int fault;
                                double Q = PDF.gauinv(s[j, k], out fault);
                                if (fault == 0 & stime[j, k] > 0)
                                {
                                    nx = nx + 1;
                                    x[nx, k] = Math.Log(stime[j, k]);
                                    y[nx, k] = Q;
                                }
                                break;
                            case 5:
                                if (h[j, k] != Constant.MISSING & stime[j, k] != 0)
                                {
                                    nx = nx + 1;
                                    x[nx, k] = stime[j, k];
                                    y[nx, k] = h[j, k] / stime[j, k];
                                }
                                break;
                        }

                    }
                    cnx[k] = nx;
                }
                // Plot the results
                using (ChartRenderer ch = (ChartRenderer)ChartRendererFor(new ChartDefinition()))
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
                case ChartType.Bar:
                case ChartType.StackedBar:
                case ChartType.StackedBar100Percent:
                    return new BarChartRenderer(chartDefinition);
                case ChartType.BoxWhisker:
                    return new BoxWhiskerChartRenderer(chartDefinition);
                case ChartType.Forest:
                    return new ForestChartRenderer(chartDefinition);
                case ChartType.Ladder:
                    return new LadderChartRenderer(chartDefinition);
                case ChartType.Pyramid:
                    return new PyramidChartRenderer(chartDefinition);
                case ChartType.Spread:
                    return new SpreadChartRenderer(chartDefinition);
                case ChartType.Survival:
                    return new SurvivalChartRenderer(chartDefinition);
                default:
                    return new ChartRenderer(chartDefinition);
            }
        }

        private static string Render(IChartRenderer ch)
        {
            return RtfImageRenderer.ImageStreamToRtf(ch.GetImageStream(), ch.ImageWidth, ch.ImageHeight);
        }
    }
}
