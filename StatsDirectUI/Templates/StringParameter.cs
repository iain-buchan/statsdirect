using System.Xml.Serialization;

namespace StatsDirect.Templates
{
    public sealed class StringParameter: RangeParameter
    {
        private Expression defaultValue;

        /*
        public StringParameter()
        {
            // By default, remember value per operation.  Deserialization can override this.
            // Lifetime = ParameterLifetime.SessionForThisOperation;
        }
         */

        /// <summary>
        /// The default value for this parameter, or null for no default.
        /// </summary>
        public string DefaultValue(ITemplateProcessor processor, ParameterBag parameters)
        {
            return processor.Evaluate(defaultValue, parameters).ToString();
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
            set { defaultValue = value; }
        }

        /// <summary>
        /// The maximum length for this parameter, or 0 for no maximum.
        /// </summary>
        [XmlElement(ElementName = "maximum-length")]
        public int MaxLength { get; set; }

        public override ParameterType Type
        {
            get { return ParameterType.String; }
        }
    }
}
