using System;
using System.Diagnostics.CodeAnalysis;

namespace StatsDirect.Charting
{
    /// <summary>
    /// A descriptor in RGB colour space, where values range from 0 to 255.
    /// </summary>
    public class ColorDescriptor
    {
        public int R { get; }
        public int G { get; }
        public int B { get; }

        public static ColorDescriptor Black { get; } = FromArgb(0, 0, 0);
        public static ColorDescriptor Blue { get; } = FromArgb(0, 0, 255);
        public static ColorDescriptor Gray { get; } = FromArgb(128, 128, 128);
        public static ColorDescriptor Green { get; } = FromArgb(255, 0, 0);
        public static ColorDescriptor Magenta { get; } = FromArgb(255, 0, 255);
        public static ColorDescriptor Red { get; } = FromArgb(255, 0, 0);
        public static ColorDescriptor White { get; } = FromArgb(255, 255, 255);

        public static ColorDescriptor FromArgb(int r, int g, int b)
        {
            return new ColorDescriptor(r, g, b);
        }

        public override bool Equals(object obj) =>
            obj is ColorDescriptor descriptor
                && descriptor is not null
                && R == descriptor.R
                && G == descriptor.G
                && B == descriptor.B;

        public override int GetHashCode() => HashCode.Combine(R, G, B);

        private ColorDescriptor(int r, int g, int b)
        {
            R = r;
            G = g;
            B = b;
        }

        public static bool TryParse(string commaSeparatedRgb, [NotNullWhen(true)] out ColorDescriptor colour)
        {
            // Assume failure
            colour = null;

            string[] colourValues = commaSeparatedRgb.Split(',');
            if (colourValues.Length != 3)
                return false;
            if (!(
                int.TryParse(colourValues[0], out int red)
                && int.TryParse(colourValues[1], out int green)
                && int.TryParse(colourValues[2], out int blue)))
                return false;
            colour = ColorDescriptor.FromArgb(red, green, blue);
            return true;
        }

        public override string ToString() => $"{R},{G},{B}";
    }
}