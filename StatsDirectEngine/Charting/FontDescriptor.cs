using System;

namespace StatsDirect.Charting
{
    /// <remarks>Immutable.</remarks>
    public sealed class FontDescriptor(string fontFamily, int style, float sizeInPoints)
    {
        public string FontFamily { get; } = fontFamily;
        // TODO: Remove dependence on Windows-ish flags
        public int Style { get; } = style;
        public float SizeInPoints { get; } = sizeInPoints;

        public override bool Equals(object obj)
        {
            return obj is FontDescriptor other
                && (null == FontFamily && null == other.FontFamily || FontFamily.Equals(other.FontFamily))
                && Style == other.Style
                && SizeInPoints == other.SizeInPoints;
        }

        public override int GetHashCode() => HashCode.Combine(FontFamily, Style, SizeInPoints);

        public override string ToString()
        {
            return $"{FontFamily};{Style};{SizeInPoints}pt";
        }

        public static bool TryParse(string descriptor, out FontDescriptor fontDescriptor)
        {
            fontDescriptor = null;
            string[] fontStrings = descriptor.Split(';');
            if (fontStrings.Length != 3)
                return false;
            if (string.IsNullOrWhiteSpace(fontStrings[0]))
                return false;
            string fontFamily = fontStrings[0];
            if (!int.TryParse(fontStrings[1], out int style))
                return false;
            if (!float.TryParse(fontStrings[2], out float sizeInPoints))
                return false;
            fontDescriptor = new FontDescriptor(fontFamily, style, sizeInPoints);
            return true;
        }
    }
}
