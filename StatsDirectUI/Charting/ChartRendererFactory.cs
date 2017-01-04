using StatsDirect.Builtins;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.Utilities;
using System;
using System.Collections.Generic;

namespace StatsDirect.Charting
{
    public static class ChartRendererFactory
    {
        public static string SurvivalOrHazardPlot(CoxP[] z, int iobs, int istrata, ChartRenderer.CoxPlotMode plotMode, int igroups, int groupid, bool grouped, bool stratified, double[,,] ARR3, ColumnData[] cdat1, bool use_tic, bool use_marker, int[] gn)
        {
            using (ChartRenderer ch = (ChartRenderer)ChartRendererFor(new ChartDefinition()))
            {
                ch.PlotCox1(z, iobs, istrata, plotMode, igroups, groupid, grouped, stratified, ARR3, cdat1, use_tic, use_marker, gn);
                return RtfImageRenderer.ImageStreamToRtf(ch.GetImageStream(), ch.ImageWidth, ch.ImageHeight);
            }
        }

        public static string PlotEffectAndReturnRtf(Templates.ITemplateHost host, int k, double[] cn, double[] En, string[] title, double rmh, double ll, double ul, double cco, double[] odr, double[] odrl, double[] odru, string cap, int pbias, string qid)
        {
            using (ChartRenderer ch = (ChartRenderer)ChartRendererFor(new ChartDefinition()))
            {
                ch.PlotEffect(host, k, cn, En, title, rmh, ll, ul, cco, odr, odrl, odru, cap, pbias, qid);
                return RtfImageRenderer.ImageStreamToRtf(ch.GetImageStream(), ch.ImageWidth, ch.ImageHeight);
            }
        }

        public static string PlotLAbbeAndReturnRtf(int k, double[,] o, double rmh)
        {
            using (ChartRenderer ch = (ChartRenderer)ChartRendererFor(new ChartDefinition()))
            {
                ch.PlotLAbbe(k, o, rmh);
                return RtfImageRenderer.ImageStreamToRtf(ch.GetImageStream(), ch.ImageWidth, ch.ImageHeight);
            }
        }

        public static string PlotMHAndReturnRtf(int k, double[,] o, double[] odw, string[] title, double rmh, double ll, double ul, double cco, double[] odr, double[] odrl, double[] odru, bool[] lerr, bool[] uerr, string cap, int pbias, string qid, out bool ifault)
        {
            using (ChartRenderer ch = (ChartRenderer)ChartRendererFor(new ChartDefinition()))
            {
                ch.Plot_MH(k, o, odw, title, rmh, ll, ul, cco, odr, odrl, odru, lerr, uerr, cap, pbias, qid, out ifault);
                return RtfImageRenderer.ImageStreamToRtf(ch.GetImageStream(), ch.ImageWidth, ch.ImageHeight);
            }
        }

        public static string PlotMHRDAndReturnRtf(int k, double[,] o, double[] odw, string[] title, double rmh, double ll, double ul, double cco, double[] odr, double[] odrl, double[] odru, bool[] lerr, bool[] uerr, string cap, int pbias, string qid, out bool ifault)
        {
            using (ChartRenderer ch = (ChartRenderer)ChartRendererFor(new ChartDefinition()))
            {
                ch.Plot_MHRD(k, odw, title, rmh, ll, ul, cco, odr, odrl, odru, lerr, uerr, cap, pbias, qid, out ifault);
                return RtfImageRenderer.ImageStreamToRtf(ch.GetImageStream(), ch.ImageWidth, ch.ImageHeight);
            }
        }

        public static string PlotBiasMAAndReturnRtf(Templates.ITemplateHost host, double[] x, double[] yy, double[] yw, int rows, string xtxt, double[] cl, double[] cu, double cco, double cit, double rmh, Transformation xform, bool diagonal)
        {
            using (ChartRenderer ch = (ChartRenderer)ChartRendererFor(new ChartDefinition()))
            {
                ch.Plot_Bias_MA(host, x, yy, yw, rows, xtxt, cl, cu, cco, cit, rmh, xform, diagonal);
                return RtfImageRenderer.ImageStreamToRtf(ch.GetImageStream(), ch.ImageWidth, ch.ImageHeight);
            }
        }

        public static string PlotCorrelationAndReturnRtf(int k, string[] title, double[] odr, double[] odrl, double[] odru, double[] gn, int[] pg, string cap, string qid, Transformation xform, bool isDifference)
        {
            using (ChartRenderer ch = (ChartRenderer)ChartRendererFor(new ChartDefinition()))
            {
                ch.PlotCorrelation(k, title, odr, odrl, odru, gn, pg, cap, qid, xform, isDifference);
                return RtfImageRenderer.ImageStreamToRtf(ch.GetImageStream(), ch.ImageWidth, ch.ImageHeight);
            }
        }
        public static string PlotTiesAndReturnMetafile(double[] x, double[] y, int nx, double lla, double ula, double GAMMA, string v0Title, string v1Title, double mean)
        {
            using (ChartRenderer ch = (ChartRenderer)ChartRendererFor(new ChartDefinition()))
            {
                ch.PlotTies(x, y, nx, lla, ula, GAMMA, v0Title, v1Title, mean);
                return RtfImageRenderer.ImageStreamToRtf(ch.GetImageStream(), ch.ImageWidth, ch.ImageHeight);
            }
        }

        public static string PlotXYAndReturnRtf(double[] x, double[] y, string xtxt, string ytxt, string title, bool zPlot, DataMinMax minMaxY, bool useCalculatedScalesEvenWithDefinition)
        {
            using (ChartRenderer ch = (ChartRenderer)ChartRendererFor(new ChartDefinition()))
            {
                ch.PlotXY(x, y, xtxt, ytxt, title, zPlot, minMaxY, useCalculatedScalesEvenWithDefinition);
                return RtfImageRenderer.ImageStreamToRtf(ch.GetImageStream(), ch.ImageWidth, ch.ImageHeight);
            }
        }

        public static string PlotXY0To1AndReturnRtf(double[] x, double[] y, string xtxt, string ytxt, string title, bool zPlot, DataMinMax minMaxY, bool useCalculatedScalesEvenWithDefinition)
        {
            using (ChartRenderer ch = (ChartRenderer)ChartRendererFor(new ChartDefinition()))
            {
                ch.PlotXY0To1(x, y, xtxt, ytxt, title, zPlot, minMaxY, useCalculatedScalesEvenWithDefinition);
                return RtfImageRenderer.ImageStreamToRtf(ch.GetImageStream(), ch.ImageWidth, ch.ImageHeight);
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
                return RtfImageRenderer.ImageStreamToRtf(ch.GetImageStream(), ch.ImageWidth, ch.ImageHeight);
            }
        }


        public static string PlotXYZAndReturnRtf(double[] x, double[] y, double[] z, string xtxt, string ytxt, string title, bool zPlot, DataMinMax minMaxY)
        {
            using (ChartRenderer ch = (ChartRenderer)ChartRendererFor(new ChartDefinition()))
            {
                // TODO: Shouldn't need to reference Pens here.
                ch.PlotXYZ(x, y, z, 1, x.Length - 1, xtxt, ytxt, title, zPlot, minMaxY, MarkerShape.Circle, false, System.Drawing.Pens.Black, null);
                return RtfImageRenderer.ImageStreamToRtf(ch.GetImageStream(), ch.ImageWidth, ch.ImageHeight);
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
                    outputImages.Add(RtfImageRenderer.ImageStreamToRtf(ch.GetImageStream(), ch.ImageWidth, ch.ImageHeight));
                }
            }
            return outputImages;
        }

        public static IChartRenderer ChartRendererFor(ChartDefinition chartDefinition)
        {
            switch (chartDefinition.ChartType)
            {
                case Templates.ChartType.BoxWhisker:
                    return new BoxWhiskerChartRenderer(chartDefinition);
                default:
                    return new ChartRenderer(chartDefinition);
            }
        }

    }
}
