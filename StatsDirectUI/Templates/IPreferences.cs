using System.Collections.Generic;

namespace StatsDirect.Templates
{
    /// <summary>
    /// An interface for retrieving preferences that the engine needs to know about.
    /// </summary>
    public interface IPreferences
    {
        string RoundU(double amount);

        /// <summary>
        /// Format a probability, using the default number of decimal places
        /// </summary>
        string pval(double p);

        string pval_half(double p);

        bool MetaPlotCI { get; }

        int MetaPlotMethod { get; }

        /// <summary>
        /// A location to store parameters that will persist as long as the host does and are kept per-operation.
        /// </summary>
        IDictionary<string, ParameterBag> SessionParametersPerOperation { get; }

        /// <summary>
        /// A location to store parameters that will persist as long as the host does and are common across all operations that use the same name for their parameters.
        /// </summary>
        ParameterBag SessionParametersAcrossOperations { get; }

        /// <summary>
        /// The current set of preferences.
        /// Hosts MAY provide a way to change these through the interface, and SHOULD persist them between invocations.
        /// </summary>
        SDPreferences Preferences
        {
            get;
        }

        IDictionary<string, object> Session
        {
            get;
        }
    }
}
