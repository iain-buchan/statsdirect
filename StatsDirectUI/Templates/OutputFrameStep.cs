using System;
using System.Xml.Serialization;
using StatsDirect.Utilities;

namespace StatsDirect.Templates
{
    /// <summary>
    /// Extract the named data frame from the input parameters and cause the host to display it
    /// </summary>
    [Serializable, XmlType(Namespace = "http://www.statsdirect.com/schemas/Operation.xsd", TypeName = "output-frame")]
    public sealed class OutputFrameStep: Step
    {
        public OutputFrameStep()
        {
            MissingIndicator = Formatting.ASTERISK;
        }

        /// <summary>
        /// The name of the parameter containing the frame to be output
        /// </summary>
        [XmlAttribute(AttributeName="frame-name")]
        public string ParameterName { get; set; }

        [XmlAttribute(AttributeName="keep-selection")]
        public bool KeepSelection { get; set; }

        [XmlAttribute(AttributeName = "formulae")]
        public bool IsFormulae { get; set; }

        [XmlAttribute(AttributeName = "missing-indicator")]
        public string MissingIndicator { get; set; }

        /// <summary>
        /// If true, prefer inserting before the selection.  If false (default), prefer inserting after the selection.
        /// </summary>
        [XmlAttribute(AttributeName="prefer-in-place-insertion")]
        public bool PreferInPlaceInsertion { get; set; }

        public override StepType Type
        {
            get { return StepType.OutputFrame; }
        }

        public override ParameterBag ExecuteInternal(ITemplateProcessor processor, ParameterBag parameters, bool isRedo)
        {
            return processor.ExecuteInternal(this, parameters, isRedo);
        }

        /// <summary>
        /// Requires input if the output location has not been selected.
        /// </summary>
        public override InputDuringStep RequiresInputGiven(ParameterBag parameters)
        {
            return InputDuringStep.Sometimes;
        }
    }
}
