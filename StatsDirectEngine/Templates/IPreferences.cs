using StatsDirect.Utilities;

namespace StatsDirect.Templates
{
    /// <summary>
    /// A place to hold a bag of preference values, to be used throughout the system.
    /// TODO: Make read-only and change the operation to Amend these into a pure UI-side call.
    /// </summary>
    public interface IPreferences
        : ICanBeSaved
    {
        bool CanDefaultConfidenceInterval { get; set; }
        double DefaultConfidenceInterval { get; set; }
        bool DelayContinuityCorrection { get; set; }
        int DisplayDecimalPlaces { get; set; }
        double MetaCC { get; set; }
        bool MetaExact { get; set; }
        bool MetaPlotCI { get; set; }
        int MetaPlotMethod { get; set; }
        int PDecimalPlaces { get; set; }
        /// <summary>
        /// If false, group selectors are by variable.
        /// If true, group selectors are by indicator.
        /// </summary>
        bool SelectGroupsByIdentifier { get; set; }
        /// <summary>
        /// If true, scientific notation should be used for small P value display.  If false, P < 0.*1 will be shown.
        /// </summary>
        bool UseScientificNotationForSmallPValues { get; set; }

        string DECP_CHAR { get; }
        /// <summary>
        /// The largest number of rows this host is willing to output.
        /// </summary>
        int MaxRows { get; }
        string Numeric_Thousands_Separator { get; }
    }
}
