using System;
using System.Xml.Serialization;

namespace StatsDirect.Templates
{
    [Serializable]
    public sealed class DoubleParameter: RangeParameter, IDefaultParameter<double>
    {
        private Expression defaultValue;
        private Expression minimumValue;
        private Expression maximumValue;

        public DoubleParameter()
        {
            // By default, remember value per operation.  Deserialization can override this.
            // Lifetime = ParameterLifetime.SessionForThisOperation;
        }

        public double? DefaultValue(ITemplateProcessor processor, ParameterBag parameters)
        {
            if (null == defaultValue || null == defaultValue.Body)
                return null;
            object o = processor.Evaluate(defaultValue, parameters);
            if (o is int)
                return (int)o;
            return (double?)o;
        }

        /// <summary>
        /// true iff the parameter defines a default.
        /// </summary>
        public bool HasDefaultValue
        {
            get { return null != defaultValue; }
        }

        public double MinimumValue(ITemplateProcessor processor, ParameterBag parameters)
        {
            if (null == minimumValue || null == minimumValue.Body)
                return double.MinValue;
            object o = processor.Evaluate(minimumValue, parameters);
            if (o is int)
                return (int)o;
            return (double)o;
        }

        public double MaximumValue(ITemplateProcessor processor, ParameterBag parameters)
        {
            if (null == maximumValue || null == maximumValue.Body)
                return double.MaxValue;
            object o = processor.Evaluate(maximumValue, parameters);
            if (o is int)
                return (int)o;
            return (double)o;
        }

        [XmlElement(ElementName = "default-value")]
        public Expression DefaultValueExpression
        {
            get { return defaultValue; }
            set { defaultValue = value; }
        }

        [XmlElement(ElementName = "minimum-value")]
        public Expression MinimumValueExpression
        {
            get { return minimumValue; }
            set { minimumValue = value; }
        }

        [XmlElement(ElementName = "maximum-value")]
        public Expression MaximumValueExpression
        {
            get { return maximumValue; }
            set { maximumValue = value; }
        }

        public override ParameterType Type
        {
            get { return ParameterType.Double; }
        }
    }
}
