using System;

namespace StatsDirect.Utilities
{
    /// <summary>
    /// An exception indicating that something (probably the user) cancelled an operation.
    /// Used to unwind the stack and close everything in the event of the user cancelling part-way through an operation.
    /// The reason it's in Utilities is because charting also uses this, which would otherwise lead to a circular dependency between templates and charting.
    /// </summary>
    [Serializable]
    public class TemplateOperationCancelledException: NotAnErrorException
    {
    }
}
