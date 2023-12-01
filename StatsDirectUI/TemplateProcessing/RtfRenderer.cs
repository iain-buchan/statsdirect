using StatsDirect.Charting;
using StatsDirect.Templates;
using System;
using System.Text;

namespace StatsDirect.TemplateProcessing
{
    /// <summary>
    /// Something that turns an IRenderable into a piece of RTF.
    /// </summary>
    class RtfRenderer : IRenderableVisitor
    {
        private IChartRendererFactory ChartRendererFactory { get; }
        private ISdPreferences SdPreferences { get; }

        private readonly StringBuilder builder;

        public RtfRenderer(IChartRendererFactory chartRendererFactory, ISdPreferences sdPreferences)
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
            new RtfImageRenderer(ChartRendererFactory).PlotAndReturnRtf(victim, out string rtf);
            builder.Append(rtf);
        }

        private ReportRenderer GetReportRenderer(string mimeType)
        {
            if ("application/x-statsdirect-creole".Equals(mimeType))
                return new PrincipledCreoleRtfReportRenderer(SdPreferences);
            throw new ArgumentOutOfRangeException(nameof(mimeType), mimeType, "Unknown MIME type when trying to obtain a report renderer. Is this report in a format that StatsDirect can render?");
        }
    }
}
