using System;
using System.IO;
using StatsDirect.Templates;

namespace StatsDirect.Charting
{
    public static class RtfImageRenderer
    {
        public static ParameterBag PlotAndReturnRtf(ITemplateHost host, ChartDefinition cd, out string rtf)
        {
            using (IChartRenderer ch = ChartRendererFactory.ChartRendererFor(cd))
            {
                ParameterBag results = ch.Plot(host);
                rtf = cd.IsAscii ? ch.GetAsciiRtf() : ImageStreamToRtf(ch.GetImageStream(), (int)ch.ImageWidth, (int)ch.ImageHeight);
                return results;
            }
        }

        public static string ImageStreamToRtf(Stream stream, int width, int height)
        {
            try
            {
                return RtfImageConverter.MetastreamToRtf(stream, width, height);
            }
            catch (OutOfMemoryException ex)
            {
                throw new Exception("Couldn't convert a chart to RTF", ex);
            }
        }
    }
}
