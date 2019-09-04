using System.Collections.Generic;
using System.Xml.Serialization;

namespace StatsDirect.Templates
{
    public class OperationTest
    {
        private readonly List<OperationTestInputParameter> inputs;
        private readonly List<OperationTestOutputParameter> outputs;

        [XmlAttribute("reason")]
        public string Reason { get; set; }

        public OperationTest()
        {
            inputs = new List<OperationTestInputParameter>();
            outputs = new List<OperationTestOutputParameter>();
        }

        [XmlArray(ElementName = "inputs"),
        XmlArrayItem(ElementName = "parameter", Type = typeof(OperationTestInputParameter))]
        public OperationTestInputParameter[] InputsForXml
        {
            get => inputs.ToArray();
            set
            {
                inputs.Clear();
                if (null != value)
                    foreach (OperationTestInputParameter input in value)
                        inputs.Add(input);
            }
        }

        [XmlIgnore]
        public IList<OperationTestInputParameter> Inputs => inputs;

        [XmlArray(ElementName = "outputs"),
        XmlArrayItem(ElementName = "output", Type = typeof(OperationTestOutputParameter))]
        public OperationTestOutputParameter[] OutputsForXml
        {
            get => outputs.ToArray();
            set
            {
                outputs.Clear();
                if (null != value)
                    foreach (OperationTestOutputParameter output in value)
                        outputs.Add(output);
            }
        }

        [XmlIgnore]
        public IList<OperationTestOutputParameter> Outputs => outputs;
    }
}