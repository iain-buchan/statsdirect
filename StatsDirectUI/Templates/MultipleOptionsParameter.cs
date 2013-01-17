using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace StatsDirect.Templates
{
    [Serializable]
    public sealed class OptionsSelect
    {
        private string name;
        private string defaultValue;
        private string prompt;
        private IList<OptionOption> options;

        public OptionsSelect()
        {
            options = new List<OptionOption>();
        }

        [XmlAttribute(AttributeName = "name")]
        public string Name
        {
            get { return name; }
            set { name = value; }
        }

        [XmlAttribute(AttributeName = "default-value")]
        public string DefaultValue
        {
            get { return defaultValue; }
            set { defaultValue = value; }
        }

        [XmlAttribute(AttributeName = "prompt")]
        public string Prompt
        {
            get { return prompt; }
            set { prompt = value; }
        }

        [XmlElement(ElementName = "option")]
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

        [XmlIgnore]
        public IList<OptionOption> Options
        {
            get { return options; }
            set { options = value; }
        }

    }

    /// <summary>
    /// A parameter allowing complex selections.  TODO: Remove me
    /// </summary>
    [Serializable]
    public sealed class MultipleOptionsParameter: Parameter
    {
        private readonly IList<OptionsOption> options;
        private readonly IList<OptionsSelect> selects;

        public MultipleOptionsParameter()
        {
            options = new List<OptionsOption>();
            selects = new List<OptionsSelect>();
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

        [XmlArray(ElementName = "selects"),
            XmlArrayItem(ElementName = "combo")]
        public OptionsSelect[] SelectsForXML
        {
            get
            {
                OptionsSelect[] selectArray = new OptionsSelect[selects.Count];
                for (int i = 0; i < selects.Count; i++)
                    selectArray[i] = selects[i];
                return selectArray;
            }
            set
            {
                foreach (OptionsSelect select in value)
                    selects.Add(select);
            }
        }

        [XmlIgnore]
        public IList<OptionsOption> Options
        {
            get { return options; }
        }

        [XmlIgnore]
        public IList<OptionsSelect> Selects
        {
            get { return selects; }
        }

        public override ParameterType Type
        {
            get { return ParameterType.MultipleOptions; }
        }
    }
}
