using System;
using StatsDirect.Templates;

namespace StatsDirect.UI
{
    [Serializable]
    public class CancelCurrentOperationAndDoException : Exception
    {
        private readonly Operation operation;
        private readonly ParameterBag inputParameters;

        public CancelCurrentOperationAndDoException(Operation operation, ParameterBag inputParameters)
        {
            this.operation = operation;
            this.inputParameters = inputParameters;
        }

        public Operation Operation
        {
            get { return operation; }
        }

        public ParameterBag InputParameters
        {
            get { return inputParameters; }
        }
    }
}