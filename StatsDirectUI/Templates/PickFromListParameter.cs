using System;
using System.Xml.Serialization;

namespace StatsDirect.Templates
{
    /// <summary>
    /// A parameter allowing selection of multiple items from a variable in a data frame.
    /// </summary>
    [Serializable]
    public sealed class PickFromListParameter: Parameter
    {
        private bool allowMultiple;
        private bool includeNoneEntry;
        private string source;

        /// <summary>
        /// If true, multiple items may be selected from the list.
        /// If false, one item may be selected.
        /// </summary>
        [XmlElement(ElementName = "allow-multiple")]
        public bool AllowMultiple
        {
            get { return allowMultiple; }
            set { allowMultiple = value; }
        }

        /// <summary>
        /// If true, there's a "none" entry at the top of the list.
        /// If false, only the list entries are present.
        /// </summary>
        [XmlElement(ElementName = "include-none-entry")]
        public bool IncludeNoneEntry
        {
            get { return includeNoneEntry; }
            set { includeNoneEntry = value; }
        }

        /// <summary>
        /// The name of the frame whose first StringVariable will be used to provide labels for the selection.
        /// </summary>
        [XmlElement(ElementName = "source")]
        public string Source
        {
            get { return source; }
            set { source = value; }
        }

        public override ParameterType Type
        {
            get { return ParameterType.PickFromList; }
        }
    }
}
