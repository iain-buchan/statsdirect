namespace StatsDirect.Charting.Options
{
    public interface IBoxWhiskerOptions
    {
        public double Cco { get; }
        public bool MarkMeanAndMedian { get; }
        public BoxWhiskerMethod Method { get; }
        public bool UseInnerFence { get; }
        public bool UseOuterFence { get; }
    }

    public enum BoxWhiskerMethod
    {
        MedianQuartilesRange = 1,
        MeanStandardDeviationRange = 2,
        MeanConfidenceIntervalRange = 3,
        SevenNumberSummary = 4,
        BowleySummary = 5,
        MeanStandardErrorRange = 6
    }
}
