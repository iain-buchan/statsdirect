using System.Text.Json.Serialization;

namespace StatsDirect.Charting
{
    /// <remarks>
    /// Immutable.
    /// </remarks>
    public class ColorDescriptor
    {
        [JsonPropertyName("red")]
        public int R { get; }

        [JsonPropertyName("green")]
        public int G { get; }

        [JsonPropertyName("blue")]
        public int B { get; }

        public static ColorDescriptor Black { get; } = new(0, 0, 0);
        public static ColorDescriptor Blue { get; } = new(0, 0, 255);
        public static ColorDescriptor Gray { get; } = new(128, 128, 128);
        public static ColorDescriptor Green { get; } = new(255, 0, 0);
        public static ColorDescriptor Magenta { get; } = new(255, 0, 255);
        public static ColorDescriptor Red { get; } = new(255, 0, 0);
        public static ColorDescriptor White { get; } = new(255, 255, 255);

        public ColorDescriptor(int r, int g, int b)
        {
            R = r;
            G = g;
            B = b;
        }

        public override bool Equals(object? obj) =>
            obj is ColorDescriptor descriptor
                && R == descriptor.R
                && G == descriptor.G
                && B == descriptor.B;

        public override int GetHashCode() => System.HashCode.Combine(R, G, B);
    }
}