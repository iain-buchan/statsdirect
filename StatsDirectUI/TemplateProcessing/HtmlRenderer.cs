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
        private /* TODO: IPreferences*/ ITemplateHost Host { get; }
        private StringBuilder builder;

        public HtmlRenderer(/* TODO: IPreferences*/ ITemplateHost host)
        {
            Host = host;
        }

        public string Render(IRenderable renderable)
        {
            builder = new StringBuilder();
            renderable.Accept(this);
            string s = builder.ToString();
            builder = null;
            return s;
        }

        void IRenderableVisitor.Visit(ReportTemplateAndParameters victim)
        {
            ReportRenderer renderer = GetReportRenderer(victim.Template.MimeType);
            builder.Append(renderer.Render(Host, victim.Template.Content, victim.Parameters));
        }

        void IRenderableVisitor.Visit(ChartDefinition victim)
        {
            HtmlImageRenderer.PlotAndReturnHtml(Host, victim, out string html);
            builder.Append(html);
        }

        void IRenderableVisitor.Visit(ReportPicture victim)
        {
            builder.Append("<img alt=\"R chart\" width=\"").Append(victim.Width)
                .Append("\" height=\"").Append(victim.Height)
                .Append("\" src=\"data:image/png;base64,").Append(Convert.ToBase64String(victim.Png))
                .Append("\" />");
        }

        private ReportRenderer GetReportRenderer(string mimeType)
        {
            if ("application/x-statsdirect-creole".Equals(mimeType))
                return new CreoleHtmlReportRenderer();
            throw new ArgumentOutOfRangeException(nameof(mimeType), mimeType, "Unknown MIME type when trying to obtain a report renderer. Is this report in a format that StatsDirect can render?");
        }
    }
}
