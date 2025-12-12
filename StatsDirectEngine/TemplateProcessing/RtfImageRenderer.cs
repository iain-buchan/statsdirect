using System;
using System.IO;
using StatsDirect.Charting;
using StatsDirect.Templates;
using StatsDirect.Utilities;

namespace StatsDirect.TemplateProcessing
{
    public static class RtfImageRenderer
    {
        public static ICanvasFactory CANVAS_FACTORY = new EmfCanvasFactory();

        public static ParameterBag PlotAndReturnRtf(/* TODO: IPreferences*/ ITemplateHost host, ChartDefinition cd, out string rtf)
        {
            using IChartRenderer ch = ChartRendererFactory.ChartRendererFor(cd, CANVAS_FACTORY);
            ParameterBag results = ch.Plot(host, false);
            if (cd.IsAscii)
                rtf = ch.GetAscii().Replace(Environment.NewLine, Formatting.RTFCRLF);
            else
            {
                EmfCanvas emfCanvas = (EmfCanvas)ch.Canvas;
                rtf = ImageStreamToRtf(emfCanvas.DetachAndReturnImageStream(), (int)emfCanvas.Width, (int)emfCanvas.Height);
            }
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
