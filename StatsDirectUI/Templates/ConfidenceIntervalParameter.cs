using System;
using System.Xml.Serialization;

namespace StatsDirect.Templates
{
    [Serializable]
    public sealed class ConfidenceIntervalParameter: Parameter
    {
        public ConfidenceIntervalParameter()
        {
            MinimumSuggestedValue = 0.9;
            MaximumSuggestedValue = 0.99;
            SuggestedStep = 0.01;
            CanDefault = true;
            CanUseStandard = true;
        }

        [XmlElement(ElementName = "default-value")]
        public double DefaultValue { get; set; }

        [XmlElement(ElementName = "minimum-suggested-value")]
        public double MinimumSuggestedValue { get; set; }

        [XmlElement(ElementName = "maximum-suggested-value")]
        public double MaximumSuggestedValue { get; set; }

        [XmlElement(ElementName = "suggested-step")]
        public double SuggestedStep { get; set; }

        [XmlElement(ElementName = "can-default")]
        public bool CanDefault { get; set; }

        /// <summary>
        /// If true, the CI parameter can use the standard input area below the buttons.
        /// If false, it should be in the parameter flow.
        /// </summary>
        [XmlElement(ElementName = "can-use-standard")]
        public bool CanUseStandard { get; set; }

        public override ParameterType Type
        {
            get { return ParameterType.ConfidenceInterval; }
        }

        public override InputDuringStep RequiresInputGiven(ParameterBag parameters)
        {
            return (MustRequest || null != Name && null != parameters && !parameters.ContainsKey(Name)) ? InputDuringStep.Always : InputDuringStep.Never;
        }
    }
}
