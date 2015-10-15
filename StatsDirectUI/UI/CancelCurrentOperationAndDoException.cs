using System;
using StatsDirect.Templates;
using StatsDirect.Utilities;

namespace StatsDirect.UI
{
    [Serializable]
    public class CancelCurrentOperationAndDoException : NotAnErrorException
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