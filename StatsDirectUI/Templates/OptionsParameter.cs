using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace StatsDirect.Templates
{
    [Serializable]
    public sealed class OptionsOption
    {
        private string name;
        private string label;
        private bool selected;

        [XmlAttribute(AttributeName = "name")]
        public string Name
        {
            get { return name; }
            set { name = value; }
        }

        [XmlText]
        public string Label
        {
            get { return label; }
            set { label = value; }
        }

        [XmlAttribute(AttributeName = "selected")]
        public bool Selected
        {
            get { return selected; }
            set { selected = value; }
        }
    }

    /// <summary>
    /// A parameter allowing selection of one option from a list.
    /// </summary>
    [Serializable]
    public sealed class OptionsParameter: Parameter
    {
        private readonly IList<OptionsOption> options;
        private int columns = 2;

        public OptionsParameter()
        {
            options = new List<OptionsOption>();
        }

        /// <summary>
        /// A hint about the number of columns the UI should use to display options.
        /// </summary>
        /// <value>Defaults to 2</value>
        [XmlElement(ElementName = "columns")]
        public int Columns
        {
            get { return columns; }
            set { columns = value; }
        }

        [XmlArray(ElementName = "options"),
            XmlArrayItem(ElementName = "option")]
        public OptionsOption[] OptionsForXML
        {
            get
            {
                OptionsOption[] optionArray = new OptionsOption[options.Count];
                for (int i = 0; i < options.Count; i++)
                    optionArray[i] = options[i];
                return optionArray;
            }
            set
            {
                foreach (OptionsOption option in value)
                    options.Add(option);
            }
        }

        public IList<OptionsOption> Options
        {
            get { return options; }
        }

        public override ParameterType Type
        {
            get { return ParameterType.Options; }
        }
    }
}
