using System;
using System.IO;
using System.Text;
using StatsDirect.Charting;
using StatsDirect.Templates;

namespace StatsDirect.TemplateProcessing
{
    public class HtmlImageRenderer
    {
        private static readonly ICanvasFactory CANVAS_FACTORY = new SvgCanvasFactory();

        private IChartRendererFactory ChartRendererFactory { get; }

        public HtmlImageRenderer(IChartRendererFactory chartRendererFactory)
        {
            ChartRendererFactory = chartRendererFactory;
        }

        public ParameterBag PlotAndReturnHtml(ChartDefinition cd, out string html)
        {
            using IChartRenderer ch = ChartRendererFactory.ChartRendererFor(cd, CANVAS_FACTORY);
            ParameterBag results = ch.Plot(false);
            if (cd.IsAscii)
                html = ch.GetAscii().Replace(Environment.NewLine, "<br />");
            else
                html = new StreamReader(ch.DetachAndReturnImageStream(), Encoding.UTF8).ReadToEnd();
            return results;
        }
    }
}
