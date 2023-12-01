using System.Windows.Forms;
using StatsDirect.Templates;
using StatsDirect.Utilities;

namespace StatsDirect.UI
{
    /// <summary>
    /// A control that understands key StatsDirect concepts like formatting
    /// </summary>
    internal class StatsDirectUserControl : UserControl
    {
        protected ISdPreferences SdPreferences { get; }

        protected StatsDirectUserControl(ISdPreferences sdPreferences)
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
