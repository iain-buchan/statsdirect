using System.Collections.Generic;

namespace StatsDirect.Charting
{
    internal class ChartPreferencesFactory
    {
        private const int DEFAULT_MARKER_SIZE = 6;
        private const int DEFAULT_LINE_WIDTH = 1;

        /// <summary>
        /// If all else fails and our configuration files have vanished, magic up some really default settings.
        /// </summary>
        public IChartPreferences Defaults() =>
            new ChartPreferencesImpl()
            {
                AxisLabelFont = new FontDescriptor("Calibri", 0, 15),
                AxisTitleFont = new FontDescriptor("Calibri", 1, 15),
                BoxAxes = false,
                LabelFont = new FontDescriptor("Calibri", 0, 15),
                LegendFont = new FontDescriptor("Calibri", 0, 15),
                TitleFont = new FontDescriptor("Calibri", 0, 22),
                MarkerTypes = new List<MarkerType>()
                {
                    NewAbsoluteDefaultMarkerType(MarkerShape.Circle,      new ColorDescriptor(64, 105, 156),  DashStyleDescriptor.Solid),
                    NewAbsoluteDefaultMarkerType(MarkerShape.Square,      new ColorDescriptor(158, 65, 62),   DashStyleDescriptor.Dash),
                    NewAbsoluteDefaultMarkerType(MarkerShape.Triangle,    new ColorDescriptor(127, 154, 72),  DashStyleDescriptor.Dot),
                    NewAbsoluteDefaultMarkerType(MarkerShape.Plus,        new ColorDescriptor(105, 81, 133),  DashStyleDescriptor.DashDot),
                    NewAbsoluteDefaultMarkerType(MarkerShape.Cross,       new ColorDescriptor(60, 141, 163),  DashStyleDescriptor.Solid),
                    NewAbsoluteDefaultMarkerType(MarkerShape.CircleLine,  new ColorDescriptor(204, 123, 56),  DashStyleDescriptor.Dash),
                    NewAbsoluteDefaultMarkerType(MarkerShape.SquareLine,  new ColorDescriptor(79, 129, 189),  DashStyleDescriptor.Dot),
                    NewAbsoluteDefaultMarkerType(MarkerShape.SquareCross, new ColorDescriptor(192, 80, 77),   DashStyleDescriptor.DashDot),
                    NewAbsoluteDefaultMarkerType(MarkerShape.Circle,      new ColorDescriptor(155, 187, 89),  DashStyleDescriptor.Solid),
                    NewAbsoluteDefaultMarkerType(MarkerShape.Square,      new ColorDescriptor(128, 100, 162), DashStyleDescriptor.Dash),
                    NewAbsoluteDefaultMarkerType(MarkerShape.Circle,      ColorDescriptor.Black,              DashStyleDescriptor.Dash) // Fixed style
                }
            };

        private static MarkerType NewAbsoluteDefaultMarkerType(MarkerShape markerShape, ColorDescriptor colorDescriptor, DashStyleDescriptor lineDashStyle) =>
            new(
                false,
                colorDescriptor,
                lineDashStyle,
                colorDescriptor,
                FillStyle.None,
                markerShape,
                DEFAULT_MARKER_SIZE,
                DEFAULT_LINE_WIDTH
            );
    }
}
