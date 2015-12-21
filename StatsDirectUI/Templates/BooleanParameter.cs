using System;
using System.Xml.Serialization;

namespace StatsDirect.Templates
{
    [Serializable]
    public sealed class BooleanParameter: Parameter, IDefaultParameter<bool>
    {
        private Expression defaultValue;

        public BooleanParameter()
        {
            // By default, remember value per operation.  Deserialization can override this.
            // Lifetime = ParameterLifetime.SessionForThisOperation;
        }

        public bool? DefaultValue(ITemplateProcessor processor, ParameterBag parameters)
        {
            if (null == defaultValue || null == defaultValue.Body)
                return null;
            object o = processor.Evaluate(defaultValue, parameters);
            if (o is bool)
                return (bool)o;
            return (bool?)o;
        }

        /// <summary>
        /// true iff the parameter defines a default.
        /// </summary>
        public bool HasDefaultValue
        {
            get { return null != defaultValue; }
        }

        [XmlElement(ElementName = "default-value")]
        public Expression DefaultValueExpression
        {
            get { return defaultValue; }
            set { defaultValue = value; }
        }

        public override ParameterType Type
        {
            get { return ParameterType.Boolean; }
        }

        public override InputDuringStep RequiresInputGiven(ParameterBag parameters)
        {
            return (MustRequest || null != Name && null != parameters && !parameters.ContainsKey(Name)) ? InputDuringStep.Always : InputDuringStep.Never;
        }
    }
}
