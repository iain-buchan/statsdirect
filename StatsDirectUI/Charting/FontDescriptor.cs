using System;

namespace StatsDirect.Charting
{
    public sealed class FontDescriptor
    {
        public string FontFamily { get; set; }
        // TODO: Remove dependence on Windows-ish flags
        public int Style { get; set; }
        public float SizeInPoints { get; set; }

        public FontDescriptor(string fontFamily, int style, float sizeInPoints)
        {
            FontFamily = fontFamily;
            Style = style;
            SizeInPoints = sizeInPoints;
        }

        public override bool Equals(object obj)
        {
            return obj is FontDescriptor other
                && (null == FontFamily && null == other.FontFamily || FontFamily.Equals(other.FontFamily))
                && Style == other.Style
                && SizeInPoints == other.SizeInPoints;
        }

        public override int GetHashCode()
        {
            return FontFamily.GetHashCode() ^ Style.GetHashCode() ^ SizeInPoints.GetHashCode();
        }

        public override string ToString()
        {
            return $"{FontFamily};{Style};{SizeInPoints}pt";
        }

        public static bool TryParse(string descriptor, out FontDescriptor fontDescriptor)
        {
            fontDescriptor = null;
            try
            {
                string[] fontStrings = descriptor.Split(';');
                if (fontStrings.Length != 3)
                    return false;
                if (string.IsNullOrWhiteSpace(fontStrings[0]))
                    return false;
                string fontFamily = fontStrings[0];
                // A style of the four FontStyle flags and a size a font can have: a damaged descriptor gives way to the default font
                if (!int.TryParse(fontStrings[1], out int style) || style < 0 || style > 15)
                    return false;
                // ToString persists sizes with "pt"; older descriptors omit it.
                // Accept both so saved user fonts do not silently reset on load.
                string size = fontStrings[2].Trim();
                if (size.EndsWith("pt", StringComparison.OrdinalIgnoreCase))
                    size = size[..^2].TrimEnd();
                if (!float.TryParse(size, out float sizeInPoints) || !float.IsFinite(sizeInPoints) || sizeInPoints <= 0f)
                    return false;
                fontDescriptor = new FontDescriptor(fontFamily, style, sizeInPoints);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
