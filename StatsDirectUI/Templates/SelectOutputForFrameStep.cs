using System;
using System.Xml.Serialization;

namespace StatsDirect.Templates
{
    /// <summary>
    /// Extract the named data frame from the input parameters and cause the host to display it
    /// </summary>
    [Serializable, XmlType(Namespace = "http://www.statsdirect.com/schemas/Operation.xsd", TypeName = "select-output-for-frame")]
    public sealed class SelectOutputForFrameStep: Step
    {
        public override StepType Type
        {
            get { return StepType.SelectOutputForFrame; }
        }

        public override StepResult ExecuteInternal(ITemplateProcessor processor, ParameterBag parameters, bool isRedo)
        {
            return processor.ExecuteInternal(this, parameters, isRedo);
        }

        public override InputDuringStep RequiresInput
        {
            get { return InputDuringStep.Always; }
        }
    }
}
