using System.Xml.Serialization;

namespace StatsDirect.Templates
{
    public sealed class ScriptStep: Step
    {
        private InputDuringStep requiresInput;

        [XmlAttribute(AttributeName = "language")]
        public string Language { get; set; }

        [XmlAttribute(AttributeName = "entry-point")]
        public string EntryPoint { get; set; }

        [XmlText]
        public string Body { get; set; }

        public override StepType Type
        {
            get { return StepType.Script; }
        }

        public override StepResult ExecuteInternal(ITemplateProcessor processor, ParameterBag parameters, bool isRedo)
        {
            return processor.ExecuteInternal(this, parameters, isRedo);
        }

        [XmlAttribute(AttributeName = "requires-input")]
        public InputDuringStep RequiresInputForXml
        {
            get { return requiresInput; }
            set { requiresInput = value; }
        }

        [XmlIgnore]
        public override InputDuringStep RequiresInput
        {
            get { return requiresInput; }
        }
    }
}
