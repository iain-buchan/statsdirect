using System;
using System.Xml.Serialization;

namespace StatsDirect.UI
{
    [Serializable,
       XmlType(Namespace = "http://www.statsdirect.com/schemas/Menu.xsd", TypeName = "menu-item"),
       XmlRoot(Namespace = "http://www.statsdirect.com/schemas/Menu.xsd", ElementName = "menu")]
    public class SDMenuItem
    {
        private SDMenuItem[] subItems;
        string label;
        string operation;
        string tooltip;

        [XmlArray(ElementName = "sub-items")]
        public SDMenuItem[] SubItems
        {
            get => subItems;
            set => subItems = value;
        }

        [XmlAttribute(AttributeName="label")]
        public string Label
        {
            get => label;
            set => label = value;
        }

        [XmlAttribute(AttributeName = "operation")]
        public string Operation
        {
            get => operation;
            set => operation = value;
        }

        [XmlAttribute(AttributeName = "tooltip")]
        public string Tooltip
        {
            get => tooltip;
            set => tooltip = value;
        }

        [XmlElement(ElementName = "help")]
        public SDMenuItemHelp Help { get; set; }
    }

    [Serializable,
       XmlType(Namespace = "http://www.statsdirect.com/schemas/Menu.xsd", TypeName = "menu-item-help"),
       XmlRoot(Namespace = "http://www.statsdirect.com/schemas/Menu.xsd", ElementName = "help")]
    public class SDMenuItemHelp
    {
        [XmlAttribute(AttributeName="chm-id")]
        public string ChmId { get; set; }
    }
}
