using System;
using System.Xml.Serialization;

namespace StatsDirect.Templates
{
    [Serializable]
    public sealed class OptionOption
    {
        [XmlAttribute(AttributeName = "value")]
        public string Value { get; set; }

        [XmlText]
        public string Label { get; set; }

        public override string ToString()
        {
            return Label;
        }
    }
}
