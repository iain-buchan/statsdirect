using System.Drawing;

namespace StatsDirect.Charting
{
    /// <summary>
    /// TODO: Better name.  Aggregates the different options for drawing a marker.
    /// </summary>
    public class MarkerDetails
    {
        internal Pen MarkerPen { get; set; }
        internal Pen LinePen { get; set; }
        internal MarkerShape MarkerShape { get; set; }
        internal bool IsMarkerFilled { get; set; }
        internal double MarkerSize { get; set; }
    }
}
