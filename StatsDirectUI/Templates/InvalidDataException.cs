using System;

namespace StatsDirect.Templates
{
    /// <summary>
    /// An exception indicating that an operation could not proceed due to invalid data.
    /// Callers may elect to cancel the operation, or to restart itand prompt for new data.
    /// </summary>
    [Serializable]
    public class InvalidDataException : Exception
    {
        public InvalidDataException()
            : base("Invalid data")
        {
            // Nothing else required
        }

        public InvalidDataException(string Message)
            : base(Message)
        {
        }

        public ParameterBag InputParameters { get; set; }
    }
}
