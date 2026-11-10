using System;
using System.Globalization;

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
            // In the invariant culture: a size written in the culture of the day was misread wherever the decimal separator differs
            return $"{FontFamily};{Style};{SizeInPoints.ToString(CultureInfo.InvariantCulture)}pt";
        }

        /// <summary>
        /// Reads a saved font size as either culture wrote it. 5.3.9 and earlier wrote sizes in the culture of the day, so that ten and a
        /// half points are "10,5" in a file saved where the decimal separator is a comma and "10.5" elsewhere. No size was ever written
        /// with digit grouping, so a comma or a point between digits is always the decimal separator: "10,5" is never 105.
        /// </summary>
        public static bool TryParseSize(string text, out float size)
        {
            text = text.Trim().Replace(NumberFormatInfo.CurrentInfo.NumberDecimalSeparator, ".").Replace(',', '.').Replace('\u066B', '.');
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out size);
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
                if (!TryParseSize(size, out float sizeInPoints) || !float.IsFinite(sizeInPoints) || sizeInPoints <= 0f)
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
