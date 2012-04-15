using System;
using System.Xml.Serialization;

namespace StatsDirect.Templates
{
    [Serializable]
    public sealed class PickVariablesParameter: Parameter
    {
        private bool preSelectVariables = true;
        private int minimumVariables = 1;
        private int maximumVariables = Int32.MaxValue;
        private string parameterName;

        [XmlElement(ElementName = "preselect-variables")]
        public bool PreSelectVariables
        {
            get { return preSelectVariables; }
            set { preSelectVariables = value; }
        }

        [XmlElement(ElementName = "minimum-variables")]
        public int MinimumVariables
        {
            get { return minimumVariables; }
            set { minimumVariables = value; }
        }

        [XmlElement(ElementName = "maximum-variables")]
        public int MaximumVariables
        {
            get { return maximumVariables; }
            set { maximumVariables = value; }
        }

        [XmlElement(ElementName = "parameter-name")]
        public string ParameterName
        {
            get { return parameterName; }
            set { parameterName = value; }
        }

        public override ParameterType Type
        {
            get { return ParameterType.PickVariables; }
        }
    }
}
