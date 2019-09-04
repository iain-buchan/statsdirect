using System.Xml.Serialization;

namespace StatsDirect.Templates
{
    public class OperationTestInputParameter
    {
        [XmlAttribute(AttributeName ="name")]
        public string Name { get; set; }

        [XmlText]
        public string Value { get; set; }
    }
}