using System;
using System.Xml.Serialization;

namespace StatsDirect.Templates
{
    [Serializable]
    public sealed class DateParameter: RangeParameter
    {
        private Expression defaultValue;

        /// <summary>
        /// The default value for this parameter, or null for no default.
        /// </summary>
        public DateTime DefaultValue(ITemplateProcessor processor, ParameterBag parameters)
        {
            return (DateTime)processor.Evaluate(defaultValue, parameters);
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

        public override void Accept(IParameterVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}
