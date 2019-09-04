using System.Xml.Serialization;

namespace StatsDirect.Templates
{
    public class OperationTestOutputParameter
    {
        [XmlAttribute(AttributeName = "name")]
        public string Name { get; set; }

        [XmlAttribute(AttributeName = "missing")]
        public bool ShouldBeMissing { get; set; }

        [XmlText]
        public string Value { get; set; }
    }
}