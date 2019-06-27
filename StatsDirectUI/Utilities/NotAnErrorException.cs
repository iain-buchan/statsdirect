using System;

namespace StatsDirect.Utilities
{
    [Serializable]
    public abstract class NotAnErrorException: Exception
    {
        protected NotAnErrorException(string message)
            : base(message)
        {
        }

        protected NotAnErrorException()
        {
        }
    }
}