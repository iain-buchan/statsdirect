using System.Xml.Serialization;

namespace StatsDirect.Templates
{
    public sealed class ScriptStep: Step
    {
        private InputDuringStep requiresInput;
        private bool requiresGrid;

        [XmlAttribute(AttributeName = "language")]
        public string Language { get; set; }

        [XmlAttribute(AttributeName = "requires-grid")]
        public bool RequiresGridForXml 
        {
            get { return requiresGrid; }
            set { requiresGrid = value; }
        }

        [XmlAttribute(AttributeName = "entry-point")]
        public string EntryPoint { get; set; }

        [XmlText]
        public string Body { get; set; }

        public override ParameterBag ExecuteInternal(ITemplateProcessor processor, ParameterBag parameters, bool isRedo)
        {
            return processor.ExecuteInternal(this, parameters, isRedo);
        }

        [XmlAttribute(AttributeName = "requires-input")]
        public InputDuringStep RequiresInputForXml
        {
            get { return requiresInput; }
            set { requiresInput = value; }
        }

        public override InputDuringStep RequiresInputGiven(ParameterBag parameters)
        {
            return requiresInput;
        }

        [XmlIgnore]
        public override bool RequiresGrid => requiresGrid;

        public override void Accept(IStepVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}
