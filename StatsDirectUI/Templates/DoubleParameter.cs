using System;
using System.Xml.Serialization;

namespace StatsDirect.Templates
{
    [Serializable]
    public sealed class DoubleParameter: RangeParameter
    {
        private Expression defaultValue;
        private double minimumValue = double.MinValue;
        private double maximumValue = double.MaxValue;

        public DoubleParameter()
        {
            // By default, remember value per operation.  Deserialization can override this.
            Lifetime = ParameterLifetime.SessionForThisOperation;
        }

        public double? DefaultValue(ITemplateProcessor processor, ParameterBag parameters)
        {
            if (null == defaultValue || null == defaultValue.Body)
                return null;
            object o = processor.Evaluate(defaultValue, parameters);
            if (o is int)
                return (double?)(int)o;
            return (double?)o;
        }

        [XmlElement(ElementName = "default-value")]
        public Expression DefaultValueExpression
        {
            get { return defaultValue; }
            set { defaultValue = value; }
        }

        [XmlElement(ElementName = "minimum-value")]
        public double MinimumValue
        {
            get { return minimumValue; }
            set { minimumValue = value; }
        }

        [XmlElement(ElementName = "maximum-value")]
        public double MaximumValue
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
