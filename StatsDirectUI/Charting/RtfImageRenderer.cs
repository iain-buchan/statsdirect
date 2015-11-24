using StatsDirect.Templates;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace StatsDirect.Charting
{
    public static class RtfImageRenderer
    {
        public static ParameterBag PlotAndReturnRtf(ITemplateHost host, ChartRenderer renderer, out string rtf)
        {
            ParameterBag results = renderer.Plot(host);
            rtf = ImageStreamToRtf(renderer.GetImageStream(), (int)renderer.ImageWidth, (int)renderer.ImageHeight);
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
