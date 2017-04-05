using System.Collections.Generic;

namespace StatsDirect.Charting
{
    /// <summary>
    /// A sequence of items to be drawn at some point on a chart as a legend.
    /// </summary>
    public class Legend
    {
        public IList<LegendEntry> LegendEntries { get; private set; }

        public Legend()
        {
            LegendEntries = new List<LegendEntry>();
        }
    }

    /// <summary>
    /// One item in a Legend.
    /// </summary>
    public class LegendEntry
    {
        public MarkerType MarkerType { get; set; }
        public string Label { get; set; }
    }
}
