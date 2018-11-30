using System.Drawing;

namespace StatsDirect.Charting
{
    public class BrushDescriptor
    {
        public static BrushDescriptor Black { get; } = new BrushDescriptor(Color.Black);

        public Color Color { get; }
        public FillStyle FillStyle { get; set; } = FillStyle.Solid;

        public BrushDescriptor(Color color)
        {
            Color = color;
        }

    }
}