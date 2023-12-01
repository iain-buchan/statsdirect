using StatsDirect.Charting;
using StatsDirect.Templates;
using System;
using System.Text;

namespace StatsDirect.TemplateProcessing
{
    /// <summary>
    /// Something that turns an IRenderable into a piece of HTML.
    /// </summary>
    class HtmlRenderer : IRenderableVisitor
    {
        private readonly StringBuilder builder;

        private IChartRendererFactory ChartRendererFactory { get; }
        private ISdPreferences SdPreferences { get; }

        public HtmlRenderer(IChartRendererFactory chartRendererFactory, ISdPreferences sdPreferences)
        {
            builder = new StringBuilder();
            ChartRendererFactory = chartRendererFactory;
            SdPreferences = sdPreferences;
        }

        public string Render(IRenderable renderable)
        {
            renderable.Accept(this);
            string s = builder.ToString();
            builder.Clear();
            return s;
        }

        void IRenderableVisitor.Visit(ReportTemplateAndParameters victim)
        {
            ReportRenderer renderer = GetReportRenderer(victim.Template.MimeType);
            builder.Append(renderer.Render(victim.Template.Content, victim.Parameters));
        }

        void IRenderableVisitor.Visit(ChartDefinition victim)
        {
            new HtmlImageRenderer(ChartRendererFactory).PlotAndReturnHtml(victim, out string html);
            builder.Append(html);
        }

        private ReportRenderer GetReportRenderer(string mimeType)
        {
            if ("application/x-statsdirect-creole".Equals(mimeType))
                return new CreoleHtmlReportRenderer(ChartRendererFactory, SdPreferences);
            throw new ArgumentOutOfRangeException(nameof(mimeType), mimeType, "Unknown MIME type when trying to obtain a report renderer. Is this report in a format that StatsDirect can render?");
        }
    }
}
