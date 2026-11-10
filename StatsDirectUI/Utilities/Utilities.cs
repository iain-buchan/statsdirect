using StatsDirect.Charting;
using System;
using System.Drawing;
using System.Globalization;
using System.Xml.Serialization;

namespace StatsDirect.Utilities
{
    [XmlRoot(Namespace = "http://www.statsdirect.com/schemas/Scrap.xsd", ElementName = "Utilities-Utilities")] // To prevent Sgen complaining about serializing two classes with the same name.
    public sealed class Utilities
    {
        public static void Swap(ref double x, ref double y)
        {
            double t = x;
            x = y;
            y = t;
        }

        public static void Swap(ref int x, ref int y)
        {
            int t = x;
            x = y;
            y = t;
        }

        /// <summary>
        /// Returns a font matching the descriptor appropriate for using in the UI.
        /// </summary>
        public static Font FontFromSaveString(string descriptor)
        {
            string[] fontStrings = descriptor.Split(';');
            string familyName = fontStrings[0];
            FontStyle style = (FontStyle)Parsing.Cint_Txt(fontStrings[1]);
            // The size as either culture wrote it: 5.3.9 and earlier saved it in the culture of the day
            if (!FontDescriptor.TryParseSize(fontStrings[2], out float emSize))
                throw new FormatException("The font size '" + fontStrings[2] + "' is not a number.");
            return new Font(familyName, emSize, style);
        }

        public static string SaveStringFromFont(Font f)
        {
            return f.FontFamily.Name + ";" + (int)f.Style + ";" + f.Size.ToString(CultureInfo.InvariantCulture);
        }
    }
}
