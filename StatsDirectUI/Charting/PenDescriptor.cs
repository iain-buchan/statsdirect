namespace StatsDirect.Charting
{
    public class PenDescriptor
    {
        public static PenDescriptor White { get; } = new PenDescriptor(ColorDescriptor.White);
        public static PenDescriptor Black { get; } = new PenDescriptor(ColorDescriptor.Black);

        public ColorDescriptor Color { get; }
        public double LineThickness { get; }
        public DashStyleDescriptor DashStyle { get; set; }
        public CapStyle CapStyle { get; }

        public PenDescriptor(ColorDescriptor color, double lineThickness = 1, CapStyle capStyle = CapStyle.Butt)
        {
            Color = color;
            LineThickness = lineThickness;
            CapStyle = capStyle;
        }
    }
}