namespace StatsDirect.Charting
{
    public static class ChartRendererFactory
    {
        public static string PlotMHAndReturnRtf(int k, double[,] o, double[] odw, string[] title, double rmh, double ll, double ul, double cco, double[] odr, double[] odrl, double[] odru, bool[] lerr, bool[] uerr, string cap, int pbias, string qid, out bool ifault)
        {
            using (ChartRenderer ch = (ChartRenderer)ChartRendererFor(ChartDefinition.Empty()))
            {
                return ch.PlotMHAndReturnRtf(k, o, odw, title, rmh, ll, ul, cco, odr, odrl, odru, lerr, uerr, cap, pbias, qid, out ifault);
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
