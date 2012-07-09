using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace StatsDirect.Templates
{
    public class HelpTip
    {
        [XmlAttribute(AttributeName = "lang")]
        public string Lang { get; set; }

        [XmlText]
        public string Text { get; set; }
    }
}
