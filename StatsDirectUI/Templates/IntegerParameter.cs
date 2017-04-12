using System;
using System.Xml.Serialization;

namespace StatsDirect.Templates
{
    [Serializable]
    public sealed class IntegerParameter: RangeParameter, IDefaultParameter<int>
    {
        private int minimumValue = Int32.MinValue;
        private int maximumValue = Int32.MaxValue;

        public IntegerParameter()
        {
            // By default, remember value per operation.  Deserialization can override this.
            // Lifetime = ParameterLifetime.SessionForThisOperation;
        }

        /// <summary>
        /// The default value for this parameter, or null for no default.
        /// </summary>
        public int? DefaultValue(ITemplateProcessor processor, ParameterBag parameters)
        {
            if (null == DefaultValueExpression || null == DefaultValueExpression.Body)
                return null;
            object o = processor.Evaluate(DefaultValueExpression, parameters);
            return (int?)o;
        }

        /// <summary>
        /// true iff the parameter defines a default.
        /// </summary>
        public bool HasDefaultValue => null != DefaultValueExpression;

        /// <summary>
        /// The default value for this parameter, or null for no default.
        /// </summary>
        [XmlElement(ElementName = "default-value")]
        public Expression DefaultValueExpression { get; set; }

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

        public override void Accept(IParameterVisitor visitor)
        {
            visitor.Visit(this);
        }

        public override ParameterBag AllDefaults(ITemplateProcessor processor, ParameterBag context)
        {
            int? defaultValue = DefaultValue(processor, context);
            if (defaultValue.HasValue)
                return new ParameterBag(Name, new FilledParameter(FilledParameterDirection.Default, defaultValue.Value));
            return new ParameterBag();
        }
    }
}
