using StatsDirect.Configuration;

namespace StatsDirect.Templates
{
    /// <summary>
    /// A place to hold a bag of preference values, to be used throughout the system.
    /// </summary>
    public interface ISdPreferences: IPersistentPreferences
    {
        bool CanDefaultConfidenceInterval { get; set; }
        string DECP_CHAR { get; }
        double DefaultConfidenceInterval { get; set; }
        bool DelayContinuityCorrection { get; set; }
        int DisplayDecimalPlaces { get; set; }
        /// <summary>
        /// The largest number of rows this host is willing to output.
        /// </summary>
        int MaxRows { get; }
        double MetaCC { get; set; }
        bool MetaExact { get; set; }
        bool MetaPlotCI { get; set; }
        int MetaPlotMethod { get; set; }
        string Numeric_Thousands_Separator { get; }
        int PDecimalPlaces { get; set; }
        /// <summary>
        /// If true, scientific notation should be used for small P value display.  If false, P < 0.*1 will be shown.
        /// </summary>
        bool UseScientificNotationForSmallPValues { get; set; }
    }
}
