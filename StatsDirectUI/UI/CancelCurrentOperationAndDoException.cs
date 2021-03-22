using System;
using StatsDirect.Templates;
using StatsDirect.Utilities;

namespace StatsDirect.UI
{
    [Serializable]
    public class CancelCurrentOperationAndDoException : TemplateExecutionHandlesMeSpeciallyException
    {
        public Operation Operation { get; }
        public ParameterBag InputParameters { get; }

        public CancelCurrentOperationAndDoException(Operation operation, ParameterBag inputParameters)
        {
            Operation = operation;
            InputParameters = inputParameters;
        }
    }
}