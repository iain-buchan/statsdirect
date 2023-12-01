using System;

namespace StatsDirect.Charting
{
    /// <remarks>
    /// Immutable.
    /// </remarks>
    public sealed class FontDescriptor
    {
        public string FontFamily { get; }
        // TODO: Remove dependence on Windows-ish flags
        public int Style { get; }
        public float SizeInPoints { get; }

        public FontDescriptor(string fontFamily, int style, float sizeInPoints)
        {
            FontFamily = fontFamily;
            Style = style;
            SizeInPoints = sizeInPoints;
        }

        public override bool Equals(object? obj)
        {
            return obj is FontDescriptor other
                && (null == FontFamily && null == other.FontFamily || (null != FontFamily && FontFamily.Equals(other.FontFamily)))
                && Style == other.Style
                && SizeInPoints == other.SizeInPoints;
        }

        public override int GetHashCode() => HashCode.Combine(FontFamily, Style, SizeInPoints);

        public override string ToString() => $"{FontFamily};{Style};{SizeInPoints}pt";
    }
}
