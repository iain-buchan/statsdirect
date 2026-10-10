using System;
using System.Xml.Serialization;

namespace StatsDirect.Templates
{
    [Serializable]
    public class HelpContext
    {
        // Historical XML name retained for operation/template compatibility.
        // The numeric ID now resolves through HTML5 help's Data/Alias.xml.
        [XmlAttribute(AttributeName="chm-id")]
        public int ChmId { get; set; }

        [XmlAttribute(AttributeName = "url")]
        public string Url { get; set; }
    }
}
