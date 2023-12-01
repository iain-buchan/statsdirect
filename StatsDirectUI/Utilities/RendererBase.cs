using StatsDirect.Templates;

namespace StatsDirect.Utilities
{
    /// <summary>
    /// This is in a poor location - it's used by builtins, charting and template processing, but it depends on ISdPreferences.
    /// TODO: Move ISdPreferences to Utilities?  Create a Common?
    /// </summary>
    public class RendererBase
    {
        protected ISdPreferences SdPreferences { get; }

        protected RendererBase(ISdPreferences sdPreferences)
        {
            SdPreferences = sdPreferences;
        }

        /// <summary>
        /// Returns a display value of Amount, rounded to DisplayDecimalPlaces if sensible.
        /// </summary>
        /// <returns></returns>
        protected string RoundU(double amount) => Formatting.XRound(amount, SdPreferences.DisplayDecimalPlaces);

        protected string Pval(double p) => Formatting.pval(p, SdPreferences.PDecimalPlaces, SdPreferences.UseScientificNotationForSmallPValues);

        protected string PvalHalf(double p) => Formatting.pval_half(p, SdPreferences.PDecimalPlaces, SdPreferences.UseScientificNotationForSmallPValues);
    }
}
