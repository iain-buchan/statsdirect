using System;
using System.Drawing;

namespace Layout
{
    abstract public class AxisLabeler
    {
        // input to the optimization routines.
        public class Options
        {
            public AxisDirection Direction { get; set; }
            public int FontSize { get; set; }
            public Range DataRange { get; set; }
            public Range VisibleRange { get; set; }
            public RectangleF Screen { get; set; }
            public Func<string, decimal, Axis, RectangleF> ComputeLabelRect { get; set; }

            public Axis DefaultAxis()
            {
                return new Axis
                {
                    FontSize = FontSize,
                    Direction = Direction,
                    VisibleRange = VisibleRange
                };
            }
        }

        public abstract Axis generate(Options options, double m);
    }
}
