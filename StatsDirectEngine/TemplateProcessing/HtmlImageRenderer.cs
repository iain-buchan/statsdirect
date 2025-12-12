using System;
using System.IO;
using System.Text;
using StatsDirect.Charting;
using StatsDirect.Templates;

namespace StatsDirect.TemplateProcessing
{
    public static class HtmlImageRenderer
    {
        private static readonly ICanvasFactory CANVAS_FACTORY = new SvgCanvasFactory();

        public static ParameterBag PlotAndReturnHtml(/* TODO: IPreferences*/ ITemplateHost host, ChartDefinition cd, out string html)
        {
            using IChartRenderer ch = ChartRendererFactory.ChartRendererFor(cd, CANVAS_FACTORY);
            ParameterBag results = ch.Plot(host, false);
            if (cd.IsAscii)
                html = ch.GetAscii().Replace(Environment.NewLine, "<br />");
            else
                html = new StreamReader(ch.Canvas.DetachAndReturnImageStream(), Encoding.UTF8).ReadToEnd();
            return results;
        }
    }
}
