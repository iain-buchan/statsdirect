using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace StatsDirect.Templates
{
    public enum HasInput
    {
        Yes,
        NoAndTypeFound,
        NoAndTypeNotFound
    }

    [Serializable,
       XmlType(Namespace="http://www.statsdirect.com/schemas/Operation.xsd", TypeName="operation"),
       XmlRoot(Namespace = "http://www.statsdirect.com/schemas/Operation.xsd", ElementName = "operation")]
    public class Operation : IMightRequireInput
    {
        private IList<Step> steps;
        private IList<string> prerequisiteOperationNames;
        private List<SuggestedOperation> suggestedOperations;
        private List<CustomValidator> customValidators;

        public Operation()
        {
            steps = new List<Step>();
            prerequisiteOperationNames = new List<string>();
            suggestedOperations = new List<SuggestedOperation>();
            customValidators = new List<CustomValidator>();
        }

        public void FixAfterLoading()
        {
            foreach (Step s in steps)
                s.NoteOperation(this);
        }

        [XmlElement(ElementName="help")]
        public HelpContext HelpContext { get; set; }

        [XmlElement(ElementName = "name")]
        public string Name { get; set; }

        /// <summary>
        /// The friendly names of this operation.  TODO: Add language to this.
        /// </summary>
        [XmlElement(ElementName = "friendly-name")]
        public string FriendlyName { get; set; }

        [XmlArray(ElementName = "custom-validators"),
        XmlArrayItem(ElementName = "validator", Type = typeof(CustomValidator))]
        public CustomValidator[] CustomValidatorsForXml
        {
            get
            {
                return customValidators.ToArray();
            }
            set
            {
                if (null != value)
                {
                    foreach (CustomValidator cv in value)
                        customValidators.Add(cv);
                }
            }
        }

        [XmlIgnore]
        public IList<CustomValidator> CustomValidators
        {
            get { return customValidators; }
        }

        [XmlArray(ElementName = "suggested-operations"),
        XmlArrayItem(ElementName="suggested-operation", Type=typeof(SuggestedOperation))]
        public SuggestedOperation[] SuggestedOperationsForXml
        {
            get
            {
                return suggestedOperations.ToArray();
            }
            set
            {
                if (null != value)
                {
                    foreach (SuggestedOperation su in value)
                        suggestedOperations.Add(su);
                }
            }
        }

        [XmlElement(ElementName = "prerequisite-operation")]
        public string[] PrerequisiteOperationsForXml
        {
            get
            {
                return ListToStringArray(prerequisiteOperationNames);
            }
            set
            {
                if (null != value)
                {
                    foreach (string name in value)
                        prerequisiteOperationNames.Add(name);
                }
            }
        }

        /// <summary>
        /// The operation names where at least one must be performed before this operation becomes useful.
        /// </summary>
        [XmlIgnore]
        public IList<string> PrerequisiteOperationNames
        {
            get { return prerequisiteOperationNames; }
        }

        /// <summary>
        /// The suggested operation names that might be useful to perform after this operation.
        /// </summary>
        [XmlIgnore]
        public IList<SuggestedOperation> SuggestedOperations
        {
            get { return suggestedOperations; }
        }

        // Remember to change TestStep::TrueStepsForXml and TestStep::FalseStepsForXml if you change this list
        [XmlArray(ElementName = "steps"),
            XmlArrayItem(ElementName = "builtin", Type = typeof(BuiltinStep)),
            XmlArrayItem(ElementName = "chart", Type = typeof(ChartStep)),
            XmlArrayItem(ElementName = "iteration", Type = typeof(IterationStep)),
            XmlArrayItem(ElementName = "output-frame", Type = typeof(OutputFrameStep)),
            XmlArrayItem(ElementName = "parameters", Type = typeof(ParametersStep)),
            XmlArrayItem(ElementName = "report", Type = typeof(ReportStep)),
            XmlArrayItem(ElementName = "script", Type = typeof(ScriptStep)),
            XmlArrayItem(ElementName = "test", Type = typeof(TestStep))
        ]
        public Step[] StepsForXml
        {
            get
            {
                Step[] stepArray = new Step[steps.Count];
                for (int i = 0; i < steps.Count; i++)
                    stepArray[i] = steps[i];
                return stepArray;
            }
            set
            {
                foreach (Step step in value)
                    steps.Add(step);
            }
        }

        /// <summary>
        /// The steps that will be executed after all the parameters have been input, in the order in which they will be executed.
        /// </summary>
        public IList<Step> Steps
        {
            get { return steps; }
        }

        private string[] ListToStringArray(IList<string> names)
        {
            string[] nameArray = new string[names.Count];
            for (int i = 0; i < names.Count; i++)
                nameArray[i] = names[i];
            return nameArray;
        }

        /// <summary>
        /// Returns true iff one or more of the operation's suggestions is itself.
        /// </summary>
        [XmlIgnore]
        public bool SuggestsSelf
        {
            get
            {
                foreach (SuggestedOperation su in suggestedOperations)
                {
                    if (Name.Equals(su.Name))
                        return true;
                }
                return false;
            }
        }

        [XmlIgnore]
        public bool RequiresGrid
        {
            get
            {
                foreach (Step step in steps)
                {
                    if (step.RequiresGrid)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        public bool HasPrerequisites
        {
            get { return null != prerequisiteOperationNames && prerequisiteOperationNames.Count > 0; }
        }

        /// <summary>
        /// Returns true if there is at least one step of the requested type and nothing between step and that typed step will ever ask for input.
        /// </summary>
        /// <param name="stepToFind"></param>
        /// <param name="stepType"></param>
        /// <param name="stepFound"></param>
        /// <returns></returns>
        public HasInput ShouldRequestTargetAfter(Step stepToFind, Step.StepType stepType, out Step stepFound)
        {
            return Step.ShouldRequestTargetAfter(stepToFind, steps, stepType, false, out stepFound);
        }

        public InputDuringStep RequiresInputGiven(ParameterBag parameters)
        {
            return Step.GetInputRequirement(steps, parameters);
        }

        public IList<SuggestedOperation> AvailableSuggestedOperations(ITemplateProcessor processor, ParameterBag parameters)
        {
            List<SuggestedOperation> availableSuggestedOperations = new List<SuggestedOperation>();
            foreach (SuggestedOperation su in suggestedOperations)
            {
                if (null == su.SuggestIf || (bool)processor.Evaluate(su.SuggestIf, parameters))
                {
                    availableSuggestedOperations.Add(su);
                }
            }
            return availableSuggestedOperations;
        }

        public override string ToString()
        {
            return FriendlyName ?? Name ?? "(Unnamed operation)";
        }
    }
}
