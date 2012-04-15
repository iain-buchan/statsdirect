using System;

namespace StatsDirect.Utilities
{
    /// <summary>
    /// An exception indicating that a piece of validation failed.
    /// The message should be something that would be understandable if shown to the user who selected the data.
    /// </summary>
    public class ValidationException: Exception
    {
        public ValidationException(string message)
            : base(message)
        {
            // Nothing else required
        }
    }
}
