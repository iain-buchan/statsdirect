using System;
using System.Xml.Serialization;

namespace StatsDirect.Templates
{
    [Serializable]
    public sealed class BuiltinStep: Step
    {
        private string functionName;

        [XmlAttribute(AttributeName = "function-name")]
        public string FunctionName
        {
            get { return functionName; }
            set { functionName = value; }
        }

        public override StepType Type
        {
            get { return StepType.Builtin; }
        }

        public override StepResult ExecuteInternal(ITemplateProcessor processor, ParameterBag parameters, bool isRedo)
        {
            return processor.ExecuteInternal(this, parameters, isRedo);
        }

        public override InputDuringStep RequiresInputGiven(ParameterBag parameters)
        {
            return BuiltinRegistry.SoleInstance.Builtin(functionName).RequiresInputGiven(parameters);
        }
    }
}
