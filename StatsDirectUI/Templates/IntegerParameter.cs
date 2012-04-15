using System;
using System.Xml.Serialization;

namespace StatsDirect.Templates
{
    [Serializable]
    public sealed class IntegerParameter: RangeParameter
    {
        private Expression defaultValue;
        private int minimumValue = Int32.MinValue;
        private int maximumValue = Int32.MaxValue;

        public IntegerParameter()
        {
            // By default, remember value per operation.  Deserialization can override this.
            Lifetime = ParameterLifetime.SessionForThisOperation;
        }

        /// <summary>
        /// The default value for this parameter, or null for no default.
        /// </summary>
        public int DefaultValue(ITemplateProcessor processor, ParameterBag parameters)
        {
            return (int)processor.Evaluate(defaultValue, parameters);
        }

        /// <summary>
        /// true iff the parameter defines a default.
        /// </summary>
        public bool HasDefaultValue
        {
            get { return null != defaultValue; }
        }

        /// <summary>
        /// The default value for this parameter, or null for no default.
        /// </summary>
        [XmlElement(ElementName = "default-value")]
        public Expression DefaultValueExpression
        {
            get { return defaultValue; }
            set
            {
                defaultValue = value;
            }
        }

        [XmlElement(ElementName = "minimum-value")]
        public int MinimumValue
        {
            get { return minimumValue; }
            set { minimumValue = value; }
        }

        [XmlElement(ElementName = "maximum-value")]
        public int MaximumValue
        {
            get { return maximumValue; }
            set { maximumValue = value; }
        }

        public override ParameterType Type
        {
            get { return ParameterType.Integer; }
        }
    }
}
