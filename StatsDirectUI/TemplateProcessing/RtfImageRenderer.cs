using System;
using System.IO;
using StatsDirect.Charting;
using StatsDirect.Templates;
using StatsDirect.Utilities;

namespace StatsDirect.TemplateProcessing
{
    public class RtfImageRenderer
    {
        private static ICanvasFactory CANVAS_FACTORY = new EmfCanvasFactory();

        private IChartRendererFactory ChartRendererFactory { get; }

        public RtfImageRenderer(IChartRendererFactory chartRendererFactory)
        {
            ChartRendererFactory = chartRendererFactory;
        }

        public ParameterBag PlotAndReturnRtf(ChartDefinition cd, out string rtf)
        {
            using IChartRenderer ch = ChartRendererFactory.ChartRendererFor(cd, CANVAS_FACTORY);
            ParameterBag results = ch.Plot(false);
            rtf = cd.IsAscii
                ? ch.GetAscii().Replace(Environment.NewLine, Formatting.RTFCRLF)
                : ImageStreamToRtf(ch.DetachAndReturnImageStream(), (int)ch.Width, (int)ch.Height);
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
