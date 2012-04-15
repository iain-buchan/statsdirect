using StatsDirect.Templates;

namespace StatsDirect.Charting
{
    // TRANSMISSINGCOMMENT: Class ROCCutoff
    public class ROCCutoff : IFillable
    {
        public ROCSeriesRecord SeriesRecord;
        public double Weight;
        public string Title;

        // TRANSMISSINGCOMMENT: Property FillerToUse
        public string FillerToUse
        {
            get
            {
                return "ROCCutoff";
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
