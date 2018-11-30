using System.Drawing;

namespace StatsDirect.Charting
{
    public class PenDescriptor
    {
        public static PenDescriptor White { get; } = new PenDescriptor(Color.White);
        public static PenDescriptor Black { get; } = new PenDescriptor(Color.Black);

        public Color Color { get; set; }
        public double LineThickness { get; set; }
        public System.Drawing.Drawing2D.DashStyle DashStyle { get; set; }

        public PenDescriptor(Color color)
            : this(color, 1)
        {
        }

        public PenDescriptor(Color color, double lineThickness)
        {
            Color = color;
            LineThickness = lineThickness;
        }
    }
}