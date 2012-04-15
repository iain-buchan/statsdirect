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
        private bool combineWherePossible = true;

        public ParametersStep()
        {
            parameters = new List<Parameter>();
        }

        [XmlAttribute(AttributeName = "combine-where-possible")]
        public bool CombineWherePossible
        {
            get { return combineWherePossible; }
            set { combineWherePossible = true; }
        }

        [XmlArray(ElementName="parameters"),
          XmlArrayItem(ElementName = "boolean", Type = typeof(BooleanParameter)),
          XmlArrayItem(ElementName = "confidence-interval", Type = typeof(ConfidenceIntervalParameter)),
          XmlArrayItem(ElementName = "date", Type = typeof(DateParameter)),
          XmlArrayItem(ElementName = "double", Type = typeof(DoubleParameter)),
          XmlArrayItem(ElementName = "double-2-by-2", Type = typeof(Double2By2Parameter)),
          XmlArrayItem(ElementName = "double-2-by-2-by-k", Type = typeof(Double2By2ByKParameter)),
          XmlArrayItem(ElementName = "edit-grid", Type = typeof(EditGridParameter)),
          XmlArrayItem(ElementName = "frame", Type = typeof(GridParameter)),
          XmlArrayItem(ElementName = "frame2d", Type = typeof(GridParameter2D)),
          XmlArrayItem(ElementName = "grouped-covariance", Type = typeof(GroupedCovarianceParameter)),
          XmlArrayItem(ElementName = "integer", Type = typeof(IntegerParameter)),
          XmlArrayItem(ElementName = "multiple-options", Type = typeof(MultipleOptionsParameter)),
          XmlArrayItem(ElementName = "option", Type = typeof(OptionParameter)),
          XmlArrayItem(ElementName = "options", Type = typeof(OptionsParameter)),
          XmlArrayItem(ElementName = "pick-from-list", Type = typeof(PickFromListParameter)),
          XmlArrayItem(ElementName = "pick-variables", Type = typeof(PickVariablesParameter)),
          XmlArrayItem(ElementName = "special", Type = typeof(SpecialParameter)),
          XmlArrayItem(ElementName = "string", Type = typeof(StringParameter))]
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

        public override StepResult ExecuteInternal(ITemplateProcessor processor, ParameterBag parms, bool isRedo)
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

        public override InputDuringStep RequiresInput
        {
            get { return InputDuringStep.Always; }
        }
    }
}
