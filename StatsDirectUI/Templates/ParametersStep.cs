using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace StatsDirect.Templates
{
    [Serializable,
       XmlType(Namespace="http://www.statsdirect.com/schemas/Operation.xsd", TypeName="settings")]
    public class ParametersStep: Step
    {
        private readonly IList<Parameter> parameters;

        public ParametersStep()
        {
            parameters = new List<Parameter>();
        }

        [XmlElement(ElementName = "boolean", Type = typeof(BooleanParameter))]
        [XmlElement(ElementName = "confidence-interval", Type = typeof(ConfidenceIntervalParameter))]
        [XmlElement(ElementName = "date", Type = typeof(DateParameter))]
        [XmlElement(ElementName = "double", Type = typeof(DoubleParameter))]
        [XmlElement(ElementName = "double-2-by-2", Type = typeof(Double2By2Parameter))]
        [XmlElement(ElementName = "double-2-by-2-by-k", Type = typeof(Double2By2ByKParameter))]
        [XmlElement(ElementName = "edit-grid", Type = typeof(EditGridParameter))]
        [XmlElement(ElementName = "frame", Type = typeof(GridParameter))]
        [XmlElement(ElementName = "frame2d", Type = typeof(GridParameter2D))]
        [XmlElement(ElementName = "grouped-covariance", Type = typeof(GroupedCovarianceParameter))]
        [XmlElement(ElementName = "integer", Type = typeof(IntegerParameter))]
        [XmlElement(ElementName = "option", Type = typeof(OptionParameter))]
        [XmlElement(ElementName = "options", Type = typeof(OptionsParameter))]
        [XmlElement(ElementName = "pick-from-list", Type = typeof(PickFromListParameter))]
        [XmlElement(ElementName = "pick-variables", Type = typeof(PickVariablesParameter))]
        [XmlElement(ElementName = "special", Type = typeof(SpecialParameter))]
        [XmlElement(ElementName = "string", Type = typeof(StringParameter))]
        public Parameter[] ParametersForXml
        {
            get
            {
                Parameter[] parameterArray = new Parameter[parameters.Count];
                for (int i = 0; i < parameters.Count; i++)
                    parameterArray[i] = parameters[i];
                return parameterArray;
            }
            set
            {
                foreach (Parameter parameter in value)
                    parameters.Add(parameter);
            }
        }

        /// <summary>
        /// The parameters that will be requested before the operation steps are processed, in the order in which they will be requested.
        /// </summary>
        public IList<Parameter> Parameters
        {
            get { return parameters; }
        }

        public override StepType Type
        {
            get { return StepType.Parameters; }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="processor"></param>
        /// <param name="parms"></param>
        /// <param name="isRedo"></param>
        /// <returns></returns>
        /// <remarks>Note that this may return parameters with key->null; it is up to the caller to deal with this.</remarks>
        public override ParameterBag ExecuteInternal(ITemplateProcessor processor, ParameterBag parms, bool isRedo)
        {
            return processor.ExecuteInternal(this, parms, isRedo);
        }

        public override void PrepareInternal(ITemplateProcessor processor, ParameterBag parms)
        {
            processor.PrepareInternal(this, parms);
        }

        internal override void NoteOperation(Operation operation)
        {
            base.NoteOperation(operation);
            foreach (Parameter parameter in parameters)
                parameter.Operation = operation;
        }

        public override bool RequiresGrid
        {
            get
            {
                foreach (Parameter parameter in parameters)
                {
                    if (parameter.RequiresGrid)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        public override InputDuringStep RequiresInputGiven(ParameterBag bag)
        {
            // Any always means we always need to request; otherwise, any sometimes means we sometimes need to request; otherwise, we never need to request.
            bool atLeastOneSometimes = false;
            foreach (Parameter parameter in parameters)
                switch (parameter.RequiresInputGiven(bag))
                {
                    case InputDuringStep.Always:
                        return InputDuringStep.Always;
                    case InputDuringStep.Sometimes:
                        atLeastOneSometimes = true;
                        break;
                    default:
                        // Do nothing
                        break;
                }
            return atLeastOneSometimes ? InputDuringStep.Sometimes : InputDuringStep.Never; 
        }
    }
}
