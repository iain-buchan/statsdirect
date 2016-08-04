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
        private int columns = 2;
        private OptionFormatType optionFormatType = OptionFormatType.Radio;

        public OptionParameter()
        {
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
        public Expression DefaultValueExpression { get; set; }

        public string DefaultValue(ITemplateProcessor processor, ParameterBag parameters)
        {
            if (null == DefaultValueExpression || null == DefaultValueExpression.Body)
                return null;
            object o = processor.Evaluate(DefaultValueExpression, parameters);
            return (string)o;
        }

        /// <summary>
        /// true iff the parameter defines a default.
        /// </summary>
        public bool HasDefaultValue
        {
            get { return null != DefaultValueExpression; }
        }

        [XmlIgnore]
        public IList<OptionOption> Options
        {
            get { return options; }
        }
        public override InputDuringStep RequiresInputGiven(ParameterBag parameters)
        {
            return (MustRequest || null != Name && null != parameters && !parameters.ContainsKey(Name)) ? InputDuringStep.Always : InputDuringStep.Never;
        }

        public override void Accept(IParameterVisitor visitor)
        {
            visitor.Visit(this);
        }

        public override ParameterBag AllDefaults(ITemplateProcessor processor, ParameterBag context)
        {
            return new ParameterBag(Name, new FilledParameter(FilledParameterDirection.Input, DefaultValue(processor, context)));
        }
    }
}
