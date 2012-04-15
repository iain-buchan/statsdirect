using System;
using System.Xml.Serialization;

namespace StatsDirect.Templates
{
    [Serializable]
    public sealed class DoubleParameter: RangeParameter
    {
        private double defaultValue = double.NaN;
        private double minimumValue = double.MinValue;
        private double maximumValue = double.MaxValue;

        public DoubleParameter()
        {
            // By default, remember value per operation.  Deserialization can override this.
            Lifetime = ParameterLifetime.SessionForThisOperation;
        }

        [XmlElement(ElementName = "default-value")]
        public double DefaultValue
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
