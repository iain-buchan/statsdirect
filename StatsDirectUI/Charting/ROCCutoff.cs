using StatsDirect.Templates;

namespace StatsDirect.Charting
{
    public class ROCCutoff : IFillable
    {
        public ROCSeriesRecord SeriesRecord;
        public double Weight;
        public string Title;

        public string FillerToUse => "ROCCutoff";
    }
}
