using System;
using System.Xml.Serialization;

namespace StatsDirect.Templates
{
    public enum ValidationMode
    {
        NotSet = 0,
        Pooling = 1,
        Square = 2,
        SquareBins = 3,
        CheckForNonDummiedCategories = 4,
        Boolean = 5,
        Positive = 6,
        ZeroToOneExclusive = 7,
        TwoBinsAndNoMissingData = 8
    }

    /// <summary>
    /// The kind of parameter stored here, to avoid typeof() checks.
    /// </summary>
    public enum ParameterType
    {
        Boolean,
        ConfidenceInterval,
        /// <summary>
        /// Extension point for projects that wish to use Parameters for custom purposes.
        /// Those projects will have to find some other way of disambiguating the custom parameter!
        /// </summary>
        Custom,
        Date,
        Double,
        Double2By2,
        Double2By2ByK,
        EditGrid,
        Grid,
        Grid2D,
        GroupedCovariance,
        Integer,
        MultipleOptions,
        Option,
        Options,
        PickFromList,
        PickVariables,
        Special,
        String
    }

    /// <summary>
    /// For how long should the value of a parameter be preserved?
    /// </summary>
    public enum ParameterLifetime
    {
        /// <summary>
        /// The parameter is not preserved past the end of this operation
        /// </summary>
        Operation,
        /// <summary>
        /// The parameter is preserved for the session, but only for use within this operation
        /// </summary>
        SessionForThisOperation,
        /// <summary>
        /// The parameter is preserved for the session, for all operations that use the same named parameter.  Beware - the type is not checked!
        /// </summary>
        SessionForAllOperations
    }

    [Serializable,
       XmlInclude(typeof(BooleanParameter)),
       XmlInclude(typeof(ConfidenceIntervalParameter)),
       XmlInclude(typeof(DateParameter)),
       XmlInclude(typeof(DoubleParameter)),
       XmlInclude(typeof(Double2By2Parameter)),
       XmlInclude(typeof(Double2By2ByKParameter)),
       XmlInclude(typeof(EditGridParameter)),
       XmlInclude(typeof(GridParameter)),
       XmlInclude(typeof(GridParameter2D)),
       XmlInclude(typeof(GroupedCovarianceParameter)),
       XmlInclude(typeof(IntegerParameter)),
       XmlInclude(typeof(MultipleOptionsParameter)),
       XmlInclude(typeof(OptionParameter)),
       XmlInclude(typeof(OptionsParameter)),
       XmlInclude(typeof(PickFromListParameter)),
       XmlInclude(typeof(PickVariablesParameter)),
       XmlInclude(typeof(SpecialParameter)),
       XmlInclude(typeof(StringParameter))]
    public abstract class Parameter
    {
        private string name;
        private Expression prompt;
        private ValidationMode validationMode;
        private string validationFailMessage;
        private string requiresParameter;
        private Operation operation;
        private bool mustRequest;
        private string cancelSkipsParameter;
        private Expression acquireIfTrue;
        private Expression rubric;
        private ParameterLifetime lifetime;
        private bool promptPrecedesParameter;
        private string title;

        public bool AcquireIfTrue(ITemplateProcessor processor, ParameterBag parameters)
        {
            if (null == acquireIfTrue || null == acquireIfTrue.Body)
                return true;
            return (bool)processor.Evaluate(acquireIfTrue, parameters);
        }

        public string Prompt(ITemplateProcessor processor, ParameterBag parameters)
        {
            if (null == prompt || null == prompt.Body)
                return null;
            return (string)processor.Evaluate(prompt, parameters);
        }

        public string Rubric(ITemplateProcessor processor, ParameterBag parameters)
        {
            if (null == rubric || null == rubric.Body)
                return null;
            return (string)processor.Evaluate(rubric, parameters);
        }

        [XmlIgnore]
        public Operation Operation
        {
            get { return operation; }
            set { operation = value; }
        }

        [XmlElement(ElementName = "lifetime")]
        public ParameterLifetime Lifetime
        {
            get { return lifetime; }
            set { lifetime = value; }
        }

        [XmlElement(ElementName="title")]
        public string Title
        {
            get { return title; }
            set { title = value; }
        }

        /// <summary>
        /// If true, this parameter must be requested from the user (if there is a user) even if it is already present as an input parameter.
        /// The new parameter must overwrite the old one.
        /// This is generally used in post-hoc processing to ensure that repeated operations are handled correctly.
        /// </summary>
        [XmlElement(ElementName = "must-request")]
        public bool MustRequest
        {
            get { return mustRequest; }
            set { mustRequest = value; }
        }

        [XmlIgnore]
        public bool HasPrompt
        {
            get { return null != prompt && null != prompt.Body; }
        }

        [XmlIgnore]
        public bool HasAcquireIfTrue
        {
            get { return null != acquireIfTrue && null != acquireIfTrue.Body; }
        }

        [XmlElement(ElementName = "prompt")]
        public Expression PromptExpression
        {
            get { return prompt; }
            set { prompt = value; }
        }

        [XmlElement(ElementName = "rubric")]
        public Expression RubricExpression
        {
            get { return rubric; }
            set { rubric = value; }
        }

        [XmlElement(ElementName = "acquire-if-true")]
        public Expression AcquireIfTrueExpression
        {
            get { return acquireIfTrue; }
            set
            {
                acquireIfTrue = value;
            }
        }

        /// <summary>
        /// The name of another parameter which must be present and non-blank in the parameters collection for this parameter to be requested.
        /// </summary>
        [XmlElement(ElementName = "requires-parameter")]
        public string RequiresParameter
        {
            get { return requiresParameter; }
            set { requiresParameter = value; }
        }

        /// <summary>
        /// The name by which the parameter will be known within the parameters collection.
        /// </summary>
        [XmlElement(ElementName = "name")]
        public string Name
        {
            get { return name; }
            set { name = value; }
        }

        /// <summary>
        /// The mode in which the data should be loaded into the frame
        /// </summary>
        [XmlElement(ElementName = "validator")]
        public ValidationMode ValidationMode
        {
            get { return validationMode; }
            set { validationMode = value; }
        }

        /// <summary>
        /// The error message that should be shown if there is a validator and it fails.
        /// If this is not set, the default error message should be used.
        /// </summary>
        [XmlElement(ElementName = "validation-fail-message")]
        public string ValidationFailMessage
        {
            get { return validationFailMessage; }
            set { validationFailMessage = value; }
        }

        /// <summary>
        /// If non-null, UIs should fill a null value if the user cancels entry of the value (and should display the value of the parameter as a prompt on any interface they may present).
        /// If null, UIs should throw a user cancelled exception if the user cancels entry of the value.
        /// </summary>
        [XmlElement(ElementName = "cancel-skips-parameter")]
        public string CancelSkipsParameter
        {
            get { return cancelSkipsParameter; }
            set { cancelSkipsParameter = value; }
        }

        /// <summary>
        /// If true, UIs should render the prompt before the input for the parameter.
        /// If false (default), UIs should render the prompt after the parameter.
        /// </summary>
        [XmlElement(ElementName = "prompt-precedes-parameter")]
        public bool PromptPrecedesParameter
        {
            get { return promptPrecedesParameter; }
            set { promptPrecedesParameter = value; }
        }

        public virtual bool RequiresGrid
        {
            get { return false; }
        }

        public abstract ParameterType Type
        {
            get;
        }
    }
}
