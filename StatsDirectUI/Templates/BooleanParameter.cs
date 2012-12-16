using System;
using System.Xml.Serialization;

namespace StatsDirect.Templates
{
    [Serializable]
    public sealed class BooleanParameter: Parameter
    {
        private Expression defaultValue;

        public BooleanParameter()
        {
            // By default, remember value per operation.  Deserialization can override this.
            // Lifetime = ParameterLifetime.SessionForThisOperation;
        }

        [XmlElement(ElementName = "default-value")]
        public Expression DefaultValue
        {
            get { return defaultValue; }
            set { defaultValue = value; }
        }

        public override ParameterType Type
        {
            get { return ParameterType.Boolean; }
        }
    }
}
