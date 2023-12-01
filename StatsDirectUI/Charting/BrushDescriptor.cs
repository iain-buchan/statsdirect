using System.Text.Json.Serialization;

namespace StatsDirect.Charting
{
    public class BrushDescriptor
    {
        public static BrushDescriptor SolidBlack { get; } = new BrushDescriptor(ColorDescriptor.Black);

        public ColorDescriptor Color { get; }

        [JsonPropertyName("fill")]
        public FillStyle FillStyle { get; }

        public BrushDescriptor(ColorDescriptor color, FillStyle fillStyle = FillStyle.Solid)
        {
            Color = color;
            FillStyle = fillStyle;
        }
    }
}