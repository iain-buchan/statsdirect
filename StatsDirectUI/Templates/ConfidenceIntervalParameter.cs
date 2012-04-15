using System;
using System.Xml.Serialization;

namespace StatsDirect.Templates
{
    [Serializable]
    public sealed class ConfidenceIntervalParameter: Parameter
    {
        private double defaultValue;
        private bool canDefault = true;
        private double minimumSuggestedValue = 0.9;
        private double maximumSuggestedValue = 0.99;
        private double suggestedStep = 0.01;

        [XmlElement(ElementName = "default-value")]
        public double DefaultValue
        {
            get { return defaultValue; }
            set { defaultValue = value; }
        }

        [XmlElement(ElementName = "minimum-suggested-value")]
        public double MinimumSuggestedValue
        {
            get { return minimumSuggestedValue; }
            set { minimumSuggestedValue = value; }
        }

        [XmlElement(ElementName = "maximum-suggested-value")]
        public double MaximumSuggestedValue
        {
            get { return maximumSuggestedValue; }
            set { maximumSuggestedValue = value; }
        }

        [XmlElement(ElementName = "suggested-step")]
        public double SuggestedStep
        {
            get { return suggestedStep; }
            set { suggestedStep = value; }
        }

        [XmlElement(ElementName = "can-default")]
        public bool CanDefault
        {
            get { return canDefault; }
            set { canDefault = value; }
        }

        public override ParameterType Type
        {
            get { return ParameterType.ConfidenceInterval; }
        }
    }
}
