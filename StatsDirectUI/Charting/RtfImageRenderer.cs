using StatsDirect.Templates;
using System;
using System.IO;

namespace StatsDirect.Charting
{
    public static class RtfImageRenderer
    {
        public static ParameterBag PlotAndReturnRtf(ITemplateHost host, IChartRenderer ch, out string rtf)
        {
            ParameterBag results = ch.Plot(host);
            if (ch.IsAscii)
                rtf = ch.GetAsciiRTF();
            else
                rtf = ImageStreamToRtf(ch.GetImageStream(), ch.ImageWidth, ch.ImageHeight);
            return results;
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
