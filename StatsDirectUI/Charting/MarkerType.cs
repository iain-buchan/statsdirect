using System;

namespace StatsDirect.Charting
{
    /// <remarks>
    /// Immutable.
    /// </remarks>
    [Serializable]
    public class MarkerType
    {
        private static readonly ColorDescriptor DEFAULT_LINE_COLOR = ColorDescriptor.Black;
        private static readonly ColorDescriptor DEFAULT_MARKER_COLOR = ColorDescriptor.Black;
        private const double DEFAULT_MARKER_SIZE = 6;
        private const float DEFAULT_WIDTH = 1.0f;

        ///  <summary>
        ///  If true, the marker shape is filled; if false, it is hollow.
        ///  </summary>
        public bool IsMarkerFilled { get; }
        public ColorDescriptor LineColor { get; }
        /// <summary>
        /// The dash style used for lines (markers always use solid lines)
        /// </summary>
        public DashStyleDescriptor LineDashStyle { get; }
        public ColorDescriptor MarkerColor { get; }
        public FillStyle MarkerFillStyle { get; }
        public MarkerShape MarkerShape { get; }
        ///  <summary>
        ///  Sizes are defined in co-ordinate sizes.  If the canvas gets larger, marker sizes get relatively smaller.
        ///  </summary>
        public double MarkerSize { get; }
        public float Width { get; }

        public static MarkerType Default { get; } = new MarkerType(false, ColorDescriptor.Black, DashStyleDescriptor.Solid, ColorDescriptor.Black, FillStyle.None, MarkerShape.Circle, 6, 1);

        public MarkerType(
            bool isMarkerFilled = default,
            ColorDescriptor? lineColor = default,
            DashStyleDescriptor lineDashStyle = default,
            ColorDescriptor? markerColor = default,
            FillStyle markerFillStyle = default,
            MarkerShape markerShape = default,
            double markerSize = DEFAULT_MARKER_SIZE,
            float width = DEFAULT_WIDTH)
        {
            IsMarkerFilled = isMarkerFilled;
            LineColor = lineColor ?? DEFAULT_LINE_COLOR;
            LineDashStyle = lineDashStyle;
            MarkerColor = markerColor ?? DEFAULT_MARKER_COLOR;
            MarkerFillStyle = markerFillStyle;
            MarkerShape = markerShape;
            MarkerSize = markerSize;
            Width = width;
        }

        public MarkerType(
            MarkerType from,
            bool? isMarkerFilled = default,
            ColorDescriptor? lineColor = default,
            DashStyleDescriptor? lineDashStyle = default,
            ColorDescriptor? markerColor = default,
            FillStyle? markerFillStyle = default,
            MarkerShape? markerShape = default,
            double? markerSize = default,
            float? width = default)
        {
            IsMarkerFilled = isMarkerFilled ?? from.IsMarkerFilled;
            LineColor = lineColor ?? from.LineColor;
            LineDashStyle = lineDashStyle ?? from.LineDashStyle;
            MarkerColor = markerColor ?? from.MarkerColor;
            MarkerFillStyle = markerFillStyle ?? from.MarkerFillStyle;
            MarkerShape = markerShape ?? from.MarkerShape;
            MarkerSize = markerSize ?? from.MarkerSize;
            Width = width ?? from.Width;
        }
    }
}
