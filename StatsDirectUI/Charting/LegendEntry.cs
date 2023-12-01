namespace StatsDirect.Charting
{
    /// <summary>
    /// One item in a Legend.
    /// </summary>
    /// <remarks>Immutable</remarks>
    public class LegendEntry
    {
        public MarkerType MarkerType { get; }
        public string Label { get; }

        public LegendEntry(MarkerType markerType, string label)
        {
            MarkerType = markerType;
            Label = label;
        }
    }
}
