using System;
using System.Drawing;

namespace Layout
{
    public class AxisLayout
    {
        public static double AxisDensity = 1.0 / 150;
        public static double AxisFontSize = 12.0;

        public AxisLabeler.Options options;

        public AxisLayout(bool isYAxis, Range dataRange, Range visibleRange, Func<string, decimal, Axis, RectangleF> ComputeLabelRect, RectangleF screen)
        {
            options = new AxisLabeler.Options
            {
                Direction = isYAxis ? AxisDirection.Vertical : AxisDirection.Horizontal,
                DataRange = dataRange,
                VisibleRange = visibleRange,
                FontSize = (int)AxisFontSize,
                ComputeLabelRect = ComputeLabelRect,
                Screen = screen
            };
        }

        public Axis layoutAxis(Graphics g)
        {
            AxisLabeler labeler = new ExtendedAxisLabeler(g);
            return labeler.generate(options, AxisDensity);
        }
    }
}
