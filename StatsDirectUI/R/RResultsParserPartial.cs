using StatsDirect.Templates;
using System.Collections.Generic;
using System.Drawing;
using System;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace StatsDirect.R
{
    partial class RResultsParser
    {
        private object PathToChart(string path)
        {
            // the scripts of the program draw with R's svg device; another format is read as a raster and embedded as a PNG
            if (".svg".Equals(Path.GetExtension(path), StringComparison.OrdinalIgnoreCase))
                return VectorPicture(File.ReadAllText(path));
            using Image image = Image.FromFile(path);
            using var output = new MemoryStream();
            image.Save(output, ImageFormat.Png);
            return new ReportPicture(output.ToArray(), image.Width, image.Height);
        }

        /// <summary>
        /// The picture of an SVG file written by R: the markup from its svg element on (R writes an XML declaration before it), and the
        /// size in pixels from the element's width and height, which R gives in points (72 to the inch, against 96 pixels), or failing
        /// those from its viewBox.
        /// </summary>
        internal static ReportVectorPicture VectorPicture(string text)
        {
            int start = text.IndexOf("<svg", StringComparison.OrdinalIgnoreCase);
            if (start < 0)
                throw new System.IO.InvalidDataException("The chart file has no svg element.");
            string svg = text.Substring(start).TrimEnd();
            int end = svg.IndexOf('>');
            string element = end < 0 ? svg : svg.Substring(0, end);
            int width = Pixels(element, "width");
            int height = Pixels(element, "height");
            if (width <= 0 || height <= 0)
            {
                Match box = Regex.Match(element, "viewBox=[\"']\\s*[-\\d.]+[\\s,]+[-\\d.]+[\\s,]+([\\d.]+)[\\s,]+([\\d.]+)");
                if (box.Success)
                {
                    width = (int)Math.Round(double.Parse(box.Groups[1].Value, CultureInfo.InvariantCulture));
                    height = (int)Math.Round(double.Parse(box.Groups[2].Value, CultureInfo.InvariantCulture));
                }
            }
            if (width <= 0 || height <= 0)
                throw new System.IO.InvalidDataException("The chart file's svg element has no size.");
            return new ReportVectorPicture(svg, width, height);
        }

        /// <summary>
        /// The pixels of a length attribute of the svg element (pt, in, cm, mm or px; px when there is no unit), or 0 when it is not there.
        /// </summary>
        private static int Pixels(string element, string attribute)
        {
            // R's cairo device quotes with double quotes, svglite with single ones; either may write decimals (432.00pt)
            Match m = Regex.Match(element, "\\b" + attribute + "=([\"'])\\s*([\\d.]+)\\s*(pt|px|in|cm|mm)?\\s*\\1");
            if (!m.Success)
                return 0;
            double value = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            double perInch = m.Groups[3].Value switch { "pt" => 72.0, "in" => 1.0, "cm" => 2.54, "mm" => 25.4, _ => 96.0 };
            return (int)Math.Round(value * 96.0 / perInch);
        }

        private static string PathToName(string path)
        {
            return Path.GetFileNameWithoutExtension(path);
        }

        private static string ToStringBody(string rawParsedString)
        {
            return rawParsedString.Substring(1, rawParsedString.Length - 2).Replace("\\\"", "\"");
        }

        private static Dictionary<string, object> CoalesceNamesAndValues(List<string> names, List<object> values, List<string> titles)
        {
            Dictionary<string, object> coalesced = new();
            for (int i = 0; i < names.Count; i++)
            {
                string name = names[i];
                object value = values[i];
                if (value is List<object>)
                {
                    string title = null != titles && titles.Count > i ? titles[i] : names[i];
                    coalesced[name] = new TitleAndValue { Title = title, Value = value };
                }
                else
                {
                    coalesced[name] = value;
                }
            }
            return coalesced;
        }
    }

    public class TitleAndValue
    {
        public string Title;
        public object Value;
    }
}
