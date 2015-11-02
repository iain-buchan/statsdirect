using System.Collections.Generic;
using System.Xml.Serialization;

namespace StatsDirect.Templates
{
    [XmlType(Namespace="http://www.statsdirect.com/schemas/Operation.xsd", TypeName="test")]
    public class TestStep: Step
    {
        private readonly IList<Step> trueSteps;
        private readonly IList<Step> falseSteps;
        private Expression condition;

        public TestStep()
        {
            trueSteps = new List<Step>();
            falseSteps = new List<Step>();
        }

        // Remember to change Operation::StepsForXml and TestStep::FalseStepsForXml if you change this list
        [XmlArray(ElementName = "iftrue"),
            XmlArrayItem(ElementName = "builtin", Type = typeof(BuiltinStep)),
            XmlArrayItem(ElementName = "chart", Type = typeof(ChartStep)),
            XmlArrayItem(ElementName = "iteration", Type = typeof(IterationStep)),
            XmlArrayItem(ElementName = "output-frame", Type = typeof(OutputFrameStep)),
            XmlArrayItem(ElementName = "parameters", Type = typeof(ParametersStep)),
            XmlArrayItem(ElementName = "report", Type = typeof(ReportStep)),
            XmlArrayItem(ElementName = "script", Type = typeof(ScriptStep)),
            XmlArrayItem(ElementName = "test", Type = typeof(TestStep))
        ]
        public Step[] TrueStepsForXml
        {
            get
            {
                Step[] stepArray = new Step[trueSteps.Count];
                for (int i = 0; i < trueSteps.Count; i++)
                    stepArray[i] = trueSteps[i];
                return stepArray;
            }
            set
            {
                foreach (Step step in value)
                    trueSteps.Add(step);
            }
        }

        // Remember to change Operation::StepsForXml and TestStep::TrueStepsForXml if you change this list
        [XmlArray(ElementName = "iffalse"),
            XmlArrayItem(ElementName = "builtin", Type = typeof(BuiltinStep)),
            XmlArrayItem(ElementName = "chart", Type = typeof(ChartStep)),
            XmlArrayItem(ElementName = "iteration", Type = typeof(IterationStep)),
            XmlArrayItem(ElementName = "output-frame", Type = typeof(OutputFrameStep)),
            XmlArrayItem(ElementName = "parameters", Type = typeof(ParametersStep)),
            XmlArrayItem(ElementName = "report", Type = typeof(ReportStep)),
            XmlArrayItem(ElementName = "script", Type = typeof(ScriptStep)),
            XmlArrayItem(ElementName = "test", Type = typeof(TestStep))
        ]
        public Step[] FalseStepsForXml
        {
            get
            {
                Step[] stepArray = new Step[falseSteps.Count];
                for (int i = 0; i < falseSteps.Count; i++)
                    stepArray[i] = falseSteps[i];
                return stepArray;
            }
            set
            {
                foreach (Step step in value)
                    falseSteps.Add(step);
            }
        }

        [XmlElement(ElementName = "condition")]
        public Expression Condition
        {
            get { return condition; }
            set
            {
                condition = value;
            }
        }

        /// <summary>
        /// The steps that will be run if the condition evaluates to true.
        /// </summary>
        public IList<Step> TrueSteps
        {
            get { return trueSteps; }
        }

        /// <summary>
        /// The steps that will be run if the condition evaluates to false.
        /// </summary>
        public IList<Step> FalseSteps
        {
            get { return falseSteps; }
        }

        public override StepType Type
        {
            get { return StepType.Test; }
        }

        public override ParameterBag ExecuteInternal(ITemplateProcessor processor, ParameterBag parameters, bool isRedo)
        {
            return processor.ExecuteInternal(this, parameters, isRedo);
        }

        public override bool RequiresGrid
        {
            get
            {
                foreach (Step step in trueSteps)
                {
                    if (step.RequiresGrid)
                        return true;
                }
                foreach (Step step in falseSteps)
                {
                    if (step.RequiresGrid)
                        return true;
                }
                return false;
            }
        }

        public override InputDuringStep RequiresInputGiven(ParameterBag parameters)
        {
            InputDuringStep trueRequirement = GetInputRequirement(trueSteps, parameters);
            InputDuringStep falseRequirement = GetInputRequirement(falseSteps, parameters);

            // If the requirements are the same, that's the overall requirement
            if (trueRequirement == falseRequirement)
                return trueRequirement;

            // Otherwise, they're different.  In all such cases, it's a resounding maybe!
            // (Yes/sometimes, yes/no, sometimes/no and the reverses of these)
            return InputDuringStep.Sometimes;
        }

        public override bool IsOrContains(Step candidate)
        {
            if (base.IsOrContains(candidate))
                return true;

            foreach (Step s in trueSteps)
            {
                if (s.IsOrContains(candidate))
                    return true;
            }
            foreach (Step s in falseSteps)
            {
                if (s.IsOrContains(candidate))
                    return true;
            }
            return false;
        }

        public override HasInput ShouldRequestTargetAfter(Step stepToFind, StepType stepType, bool found, out Step stepFound)
        {
            // If we're actually looking for this step (unlikely!) then we've found it.
            if (this == stepToFind)
            {
                // TODO: Should we evaluate both arms at this point and return something saner?
                stepFound = null;
                return HasInput.NoAndTypeNotFound;
            }

            HasInput trueSide = ShouldRequestTargetAfter(stepToFind, trueSteps, stepType, found, out stepFound);
            if (trueSide != HasInput.NoAndTypeNotFound)
                return trueSide;
            HasInput falseSide = ShouldRequestTargetAfter(stepToFind, falseSteps, stepType, found, out stepFound);
            return falseSide;
        }

        internal override void NoteOperation(Operation operation)
        {
            base.NoteOperation(operation);
            foreach (Step s in trueSteps)
            {
                s.NoteOperation(operation);
            }
            foreach (Step s in falseSteps)
            {
                s.NoteOperation(operation);
            }
        }
    }
}
