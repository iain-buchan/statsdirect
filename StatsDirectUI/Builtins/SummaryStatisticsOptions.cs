using StatsDirect.Templates;

namespace StatsDirect.Builtins
{
    public class SummaryStatisticsOptions : IFillable
    {
        public string Text;

        public static string FillerToUse
        {
            get
            {
                return "SummaryStatistics";
            }
        } // interface properties implemented by FillerToUse
        string IFillable.FillerToUse
        {
            get
            {
                return FillerToUse;
            }
        }

    }
}