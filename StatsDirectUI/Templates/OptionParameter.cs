using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace StatsDirect.Templates
{
    [Serializable]
    public sealed class OptionOption
    {
        private string value;
        private string label;

        [XmlAttribute(AttributeName = "value")]
        public string Value
        {
            get { return value; }
            set { this.value = value; }
        }

        [XmlText]
        public string Label
        {
            get { return label; }
            set { label = value; }
        }

        public override string ToString()
        {
            return label;
        }
    }

    public enum OptionFormatType
    {
        Radio = 0,
        Dropdown = 1
    };

    /// <summary>
    /// A parameter allowing selection of one option from a list.
    /// </summary>
    [Serializable]
    public sealed class OptionParameter: Parameter
    {
        private readonly IList<OptionOption> options;
        private Expression defaultValue;
        private int columns = 2;
        private OptionFormatType optionFormatType = OptionFormatType.Radio;

        public OptionParameter()
        {
            // By default, remember value per operation.  Deserialization can override this.
            // Lifetime = ParameterLifetime.SessionForThisOperation;
            options = new List<OptionOption>();
        }

        /// <summary>
        /// A hint about the number of columns the UI should use to display options.
        /// </summary>
        /// <value>Defaults to 2</value>
        [XmlElement(ElementName="columns")]
        public int Columns
        {
            get { return columns; }
            set { columns = value; }
        }

        [XmlElement(ElementName="format-type")]
        public OptionFormatType OptionFormatType
        {
            get { return optionFormatType; }
            set { optionFormatType = value; }
        }

        [XmlArray(ElementName="options"),
            XmlArrayItem(ElementName = "option")]
        public OptionOption[] OptionsForXML
        {
            get
            {
                OptionOption[] optionArray = new OptionOption[options.Count];
                for (int i = 0; i < options.Count; i++)
                    optionArray[i] = options[i];
                return optionArray;
            }
            set
            {
                foreach (OptionOption option in value)
                    options.Add(option);
            }
        }

        [XmlElement(ElementName = "default-value")]
        public Expression DefaultValue
        {
            get { return defaultValue; }
            set { defaultValue = value; }
        }

        [XmlIgnore]
        public IList<OptionOption> Options
        {
            get { return options; }
        }

        public override ParameterType Type
        {
            get { return ParameterType.Option; }
        }
    }
}
