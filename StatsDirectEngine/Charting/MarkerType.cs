using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace StatsDirect.Charting
{
    [Serializable]
    public class MarkerType
    {
        public MarkerShape MarkerShape { get; set; }
        public ColorDescriptor MarkerColor { get; set; }
        public ColorDescriptor LineColor { get; set; }
        /// <summary>
        /// Widths are in canvas coordinates.
        /// </summary>
        public float Width { get; set; }

        /// <summary>
        /// The dash style used for lines (markers always use solid lines)
        /// </summary>
        public DashStyleDescriptor LineDashStyle { get; set; }

        ///  <summary>
        ///  If true, the marker shape is filled; if false, it is hollow.
        ///  </summary>
        public bool IsMarkerFilled { get; set; }

        ///  <summary>
        ///  Sizes are defined in co-ordinate sizes.  If the canvas gets larger, marker sizes get relatively smaller.
        ///  </summary>
        public double MarkerSize { get; set; }
        public FillStyle MarkerFillStyle { get; set; }

        public MarkerType Clone() =>
            new()
            {
                MarkerColor = MarkerColor,
                LineColor = LineColor,
                MarkerShape = MarkerShape,
                LineDashStyle = LineDashStyle,
                Width = Width,
                IsMarkerFilled = IsMarkerFilled,
                MarkerSize = MarkerSize,
                MarkerFillStyle = MarkerFillStyle
            };

        /// <summary>
        /// Expected format: shape;colour;style;isFilled;size, where isFilled is 1 for true, 0 for false. Example: 0;255,0,0;0;0;6.
        /// </summary>
        public static bool TryParse(string markerString, [NotNullWhen(true)] out MarkerType markerType)
        {
            // Default to failing
            markerType = null;

            string[] parameterStrings = markerString.Split(';');
            if (parameterStrings.Length < 4)
                return false;

            //  Shape
            if (!int.TryParse(parameterStrings[0], out int markerShapeIndex))
                return false;
            MarkerShape shape = (MarkerShape)markerShapeIndex;

            //  Colour
            if (!ColorDescriptor.TryParse(parameterStrings[1], out ColorDescriptor col))
                return false;

            //  Width
            if (!float.TryParse(parameterStrings[2], out float width))
                return false;

            //  Style
            if (!int.TryParse(parameterStrings[3], out int dashStyleIndex))
                return false;
            DashStyleDescriptor dashStyle = (DashStyleDescriptor)dashStyleIndex;

            //  Filled (1 = yes, missing or 0 = no)
            bool isFilled = false;
            if (parameterStrings.Length > 4)
                isFilled = "1".Equals(parameterStrings[4]);

            //  Marker size
            int markerSize = 0;
            if (parameterStrings.Length > 5)
                int.TryParse(parameterStrings[5], out markerSize);
            if (markerSize <= 0)
                markerSize = 6;

            markerType = new()
            {
                MarkerColor = col,
                LineColor = col,
                IsMarkerFilled = isFilled,
                MarkerSize = markerSize,
                MarkerShape = shape,
                LineDashStyle = dashStyle,
                Width = width
            };
            return true;
        }

        public override string ToString() =>
            string.Create(CultureInfo.InvariantCulture, $"""{MarkerShape};{MarkerColor};{LineDashStyle};{(IsMarkerFilled ? "1" : "0")};{MarkerSize}""");
    }
}
