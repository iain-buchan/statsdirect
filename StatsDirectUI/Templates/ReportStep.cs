using System;
using System.Xml.Serialization;
using StatsDirect.TemplateProcessing;

namespace StatsDirect.Templates
{
    [Serializable, XmlType(Namespace = "http://www.statsdirect.com/schemas/Operation.xsd", TypeName = "report-template")]
    public sealed class ReportStep: Step
    {
        [XmlAttribute(AttributeName="filename")]
        public string FileName { get; set; }

        public override StepType Type
        {
            get { return StepType.Report; }
        }

        [XmlText]
        public string Text { get; set; }

        [XmlAttribute(AttributeName="type")]
        public string MimeType { get; set; }

        /// <summary>
        /// Fill in the substitutions to this template.
        /// </summary>
        /// <param name="substitutions"></param>
        /// <returns>The template including all substitutions this has made from the substitutions.</returns>
        public string Substitute(ITemplateHost host, ParameterBag substitutions)
        {
            ReportRenderer renderer = GetRenderer();
            return renderer.Render(host, substitutions);
        }

        private ReportRenderer GetRenderer()
        {
            if (null == MimeType || "text/rtf".Equals(MimeType))
                return new RtfReportRenderer { Template = GetContent() };
            if ("application/x-statsdirect-creole".Equals(MimeType))
                return new CreoleReportRenderer { Template = GetContent() };
            throw new Exception("Unknown MIME type \"" + MimeType + "\" in report");
        }

        private string GetContent()
        {
            if (null != Text)
                return Text;
            return ReportRenderer.GetContent(FileName);
        }

        public override StepResult ExecuteInternal(ITemplateProcessor processor, ParameterBag parameters, bool isRedo)
        {
            return processor.ExecuteInternal(this, parameters, isRedo);
        }

        /// <summary>
        /// Doesn't need to be told where to put the output as that's now UI state rather than requested on demand.
        /// </summary>
        public override InputDuringStep RequiresInputGiven(ParameterBag parameters)
        {
            return InputDuringStep.Never;
        }
    }
}
