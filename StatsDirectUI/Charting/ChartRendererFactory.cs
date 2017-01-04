using StatsDirect.Builtins;
using StatsDirect.Data;
using StatsDirect.Utilities;

namespace StatsDirect.Charting
{
    public static class ChartRendererFactory
    {
        public static string SurvivalOrHazardPlot(CoxP[] z, int iobs, int istrata, ChartRenderer.CoxPlotMode plotMode, int igroups, int groupid, bool grouped, bool stratified, double[,,] ARR3, ColumnData[] cdat1, bool use_tic, bool use_marker, int[] gn)
        {
            using (ChartRenderer ch = (ChartRenderer)ChartRendererFor(ChartDefinition.Empty()))
            {
                ch.PlotCox1(z, iobs, istrata, plotMode, igroups, groupid, grouped, stratified, ARR3, cdat1, use_tic, use_marker, gn);
                return RtfImageRenderer.ImageStreamToRtf(ch.GetImageStream(), ch.ImageWidth, ch.ImageHeight);
            }
        }

        public static string PlotEffectAndReturnRtf(Templates.ITemplateHost host, int k, double[] cn, double[] En, string[] title, double rmh, double ll, double ul, double cco, double[] odr, double[] odrl, double[] odru, string cap, int pbias, string qid)
        {
            using (ChartRenderer ch = (ChartRenderer)ChartRendererFor(ChartDefinition.Empty()))
            {
                ch.PlotEffect(host, k, cn, En, title, rmh, ll, ul, cco, odr, odrl, odru, cap, pbias, qid);
                return RtfImageRenderer.ImageStreamToRtf(ch.GetImageStream(), ch.ImageWidth, ch.ImageHeight);
            }
        }

        public static string PlotLAbbeAndReturnRtf(int k, double[,] o, double rmh)
        {
            using (ChartRenderer ch = (ChartRenderer)ChartRendererFor(ChartDefinition.Empty()))
            {
                ch.PlotLAbbe(k, o, rmh);
                return RtfImageRenderer.ImageStreamToRtf(ch.GetImageStream(), ch.ImageWidth, ch.ImageHeight);
            }
        }

        public static string PlotMHAndReturnRtf(int k, double[,] o, double[] odw, string[] title, double rmh, double ll, double ul, double cco, double[] odr, double[] odrl, double[] odru, bool[] lerr, bool[] uerr, string cap, int pbias, string qid, out bool ifault)
        {
            using (ChartRenderer ch = (ChartRenderer)ChartRendererFor(ChartDefinition.Empty()))
            {
                ch.Plot_MH(k, o, odw, title, rmh, ll, ul, cco, odr, odrl, odru, lerr, uerr, cap, pbias, qid, out ifault);
                return RtfImageRenderer.ImageStreamToRtf(ch.GetImageStream(), ch.ImageWidth, ch.ImageHeight);
            }
        }

        public static string PlotMHRDAndReturnRtf(int k, double[,] o, double[] odw, string[] title, double rmh, double ll, double ul, double cco, double[] odr, double[] odrl, double[] odru, bool[] lerr, bool[] uerr, string cap, int pbias, string qid, out bool ifault)
        {
            using (ChartRenderer ch = (ChartRenderer)ChartRendererFor(ChartDefinition.Empty()))
            {
                ch.Plot_MHRD(k, odw, title, rmh, ll, ul, cco, odr, odrl, odru, lerr, uerr, cap, pbias, qid, out ifault);
                return RtfImageRenderer.ImageStreamToRtf(ch.GetImageStream(), ch.ImageWidth, ch.ImageHeight);
            }
        }

        public static string PlotBiasMAAndReturnRtf(Templates.ITemplateHost host, double[] x, double[] yy, double[] yw, int rows, string xtxt, double[] cl, double[] cu, double cco, double cit, double rmh, Transformation xform, bool diagonal)
        {
            using (ChartRenderer ch = (ChartRenderer)ChartRendererFor(ChartDefinition.Empty()))
            {
                ch.Plot_Bias_MA(host, x, yy, yw, rows, xtxt, cl, cu, cco, cit, rmh, xform, diagonal);
                return RtfImageRenderer.ImageStreamToRtf(ch.GetImageStream(), ch.ImageWidth, ch.ImageHeight);
            }
        }

        public static string PlotCorrelationAndReturnRtf(int k, string[] title, double[] odr, double[] odrl, double[] odru, double[] gn, int[] pg, string cap, string qid, Transformation xform, bool isDifference)
        {
            using (ChartRenderer ch = (ChartRenderer)ChartRendererFor(ChartDefinition.Empty()))
            {
                ch.PlotCorrelation(k, title, odr, odrl, odru, gn, pg, cap, qid, xform, isDifference);
                return RtfImageRenderer.ImageStreamToRtf(ch.GetImageStream(), ch.ImageWidth, ch.ImageHeight);
            }
        }
        public static string PlotTiesAndReturnMetafile(double[] x, double[] y, int nx, double lla, double ula, double GAMMA, string v0Title, string v1Title, double mean)
        {
            using (ChartRenderer ch = (ChartRenderer)ChartRendererFor(ChartDefinition.Empty()))
            {
                ch.PlotTies(x, y, nx, lla, ula, GAMMA, v0Title, v1Title, mean);
                return RtfImageRenderer.ImageStreamToRtf(ch.GetImageStream(), ch.ImageWidth, ch.ImageHeight);
            }
        }

        public static string PlotXYAndReturnRtf(double[] x, double[] y, string xtxt, string ytxt, string title, bool zPlot, DataMinMax minMaxY, bool useCalculatedScalesEvenWithDefinition)
        {
            using (ChartRenderer ch = (ChartRenderer)ChartRendererFor(ChartDefinition.Empty()))
            {
                ch.PlotXY(x, y, xtxt, ytxt, title, zPlot, minMaxY, useCalculatedScalesEvenWithDefinition);
                return RtfImageRenderer.ImageStreamToRtf(ch.GetImageStream(), ch.ImageWidth, ch.ImageHeight);
            }
        }

        public static string PlotXY0To1AndReturnRtf(double[] x, double[] y, string xtxt, string ytxt, string title, bool zPlot, DataMinMax minMaxY, bool useCalculatedScalesEvenWithDefinition)
        {
            using (ChartRenderer ch = (ChartRenderer)ChartRendererFor(ChartDefinition.Empty()))
            {
                ch.PlotXY0To1(x, y, xtxt, ytxt, title, zPlot, minMaxY, useCalculatedScalesEvenWithDefinition);
                return RtfImageRenderer.ImageStreamToRtf(ch.GetImageStream(), ch.ImageWidth, ch.ImageHeight);
            }
        }

        public static string PlotXYRAndReturnRtf(double[,] x, double[,,] y, int ng, int[] gn, int[,] nr, double[] b, double[] a, string xtxt, string ytxt, string title, string[] bnam, MinMax minMax)
        {
            using (ChartRenderer ch = (ChartRenderer)ChartRendererFor(ChartDefinition.Empty()))
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
            using (ChartRenderer ch = (ChartRenderer)ChartRendererFor(ChartDefinition.Empty()))
            {
                // TODO: Shouldn't need to reference Pens here.
                ch.PlotXYZ(x, y, z, 1, x.Length - 1, xtxt, ytxt, title, zPlot, minMaxY, MarkerShape.Circle, false, System.Drawing.Pens.Black, null);
                return RtfImageRenderer.ImageStreamToRtf(ch.GetImageStream(), ch.ImageWidth, ch.ImageHeight);
            }
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
