using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;

namespace StatsDirect.UI.WebReports;

internal static class ReportClipboard
{
    internal const string FragmentFormat = "StatsDirect.ReportFragment.HTML.v1";
    internal const string SvgFormat = "image/svg+xml";

    // Keep the image resources alive until Windows has materialised every
    // clipboard format. Rich HTML and the private vector fragment stay intact.
    internal static void Publish(DataObject contents, string svg = null, string png = null)
    {
        const int maximumBytes = 20_000_000;
        const string pngPrefix = "data:image/png;base64,";
        if (png != null && (!png.StartsWith(pngPrefix, StringComparison.Ordinal) || png.Length > 27_000_000))
            throw new InvalidDataException("The selected picture could not be copied as a PNG smaller than 20 MB.");
        byte[] bytes = png == null ? null : Convert.FromBase64String(png[pngPrefix.Length..]);
        byte[] vector = svg == null ? null : Encoding.UTF8.GetBytes(svg);
        if ((bytes?.Length ?? 0) > maximumBytes || (vector?.Length ?? 0) > maximumBytes)
            throw new InvalidDataException("The selected picture is too large to copy.");
        using var raster = bytes == null ? null : new MemoryStream(bytes, writable: false);
        using var bitmap = raster == null ? null : new Bitmap(raster);
        using var pngData = bytes == null ? null : new MemoryStream(bytes, writable: false);
        using var svgData = vector == null ? null : new MemoryStream(vector, writable: false);
        if (bitmap != null)
        {
            if (bitmap.RawFormat.Guid != ImageFormat.Png.Guid) throw new InvalidDataException("The clipboard PNG picture is invalid.");
            contents.SetData("PNG", false, pngData);
            contents.SetData(DataFormats.Bitmap, true, bitmap);
        }
        if (svgData != null) contents.SetData(SvgFormat, false, svgData);
        Clipboard.SetDataObject(contents, true, 5, 100);
    }

    // Embed native clipboard pictures. SVG stays vector; the report's existing
    // fragment sanitizer removes active content and gives drawing IDs a fresh scope.
    internal static string ImageHtml(IDataObject contents)
    {
        const int maximumBytes = 20_000_000; // Base64 stays within the report's 30 MB paste limit.
        static string SvgText(byte[] bytes)
        {
            using var reader = new StreamReader(new MemoryStream(bytes, writable: false), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            return reader.ReadToEnd();
        }
        foreach (string format in new[] { SvgFormat, "image/x-svg+xml", "SVG" })
        {
            object data = contents?.GetData(format, false);
            string svg = data switch
            {
                string value => value,
                MemoryStream stream when stream.Length <= maximumBytes => SvgText(stream.ToArray()),
                byte[] bytes when bytes.Length <= maximumBytes => SvgText(bytes),
                MemoryStream or byte[] => throw new InvalidDataException("The clipboard picture is too large."),
                _ => null
            };
            if (string.IsNullOrWhiteSpace(svg)) continue;
            if (Encoding.UTF8.GetByteCount(svg) > maximumBytes) throw new InvalidDataException("The clipboard picture is too large.");
            using var source = new StringReader(svg.Trim('\uFEFF', '\0'));
            using var reader = XmlReader.Create(source, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null, MaxCharactersInDocument = maximumBytes });
            XElement root = XElement.Load(reader);
            if (root.Name.LocalName != "svg" || root.Name.NamespaceName is not ("" or "http://www.w3.org/2000/svg"))
                throw new InvalidDataException("The clipboard picture has no SVG element.");
            return root.ToString(SaveOptions.DisableFormatting);
        }
        byte[] png = contents?.GetData("PNG", false) switch
        {
            MemoryStream stream when stream.Length <= maximumBytes => stream.ToArray(),
            byte[] bytes when bytes.Length <= maximumBytes => bytes,
            MemoryStream or byte[] => throw new InvalidDataException("The clipboard picture is too large."),
            _ => null
        };
        if (png == null)
        {
            Image bitmap = contents?.GetData(DataFormats.Bitmap) as Image ?? contents?.GetData(typeof(Bitmap)) as Image;
            if (bitmap != null)
            {
                using var buffer = new MemoryStream();
                bitmap.Save(buffer, ImageFormat.Png);
                if (buffer.Length > maximumBytes) throw new InvalidDataException("The clipboard picture is too large.");
                png = buffer.ToArray();
            }
        }
        if (png == null) return null;
        using (var buffer = new MemoryStream(png, writable: false))
        using (Image decoded = Image.FromStream(buffer, useEmbeddedColorManagement: false, validateImageData: true))
        {
            if (decoded.RawFormat.Guid != ImageFormat.Png.Guid)
                throw new InvalidDataException("The clipboard PNG picture is invalid.");
        }
        return "<img alt=\"Pasted image\" src=\"data:image/png;base64," + Convert.ToBase64String(png) + "\">";
    }
    // CF_HTML offsets count UTF-8 BYTES, not .NET UTF-16 characters.
    internal static string EncodeHtml(string html)
    {
        const string start = "<!--StartFragment-->", end = "<!--EndFragment-->";
        if (!html.Contains(start, StringComparison.Ordinal)) html = "<html><body>" + start + html + end + "</body></html>";
        const string template = "Version:1.0\r\nStartHTML:{0:D10}\r\nEndHTML:{1:D10}\r\nStartFragment:{2:D10}\r\nEndFragment:{3:D10}\r\n";
        int header = string.Format(CultureInfo.InvariantCulture, template, 0, 0, 0, 0).Length;
        int from = html.IndexOf(start, StringComparison.Ordinal) + start.Length;
        int to = html.IndexOf(end, from, StringComparison.Ordinal);
        if (to < from) throw new FormatException("Invalid HTML clipboard fragment.");
        return string.Format(CultureInfo.InvariantCulture, template, header, header + Encoding.UTF8.GetByteCount(html),
            header + Encoding.UTF8.GetByteCount(html[..from]), header + Encoding.UTF8.GetByteCount(html[..to])) + html;
    }

    internal static string DecodeHtml(string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        int Offset(string name)
        {
            Match m = Regex.Match(value, "(?:^|\\n)" + name + @":\s*(\d+)", RegexOptions.CultureInvariant);
            return m.Success && int.TryParse(m.Groups[1].Value, out int n) ? n : -1;
        }
        // Retain the document head and its styles when provided by Word/Excel.
        int start = Offset("StartHTML"), end = Offset("EndHTML");
        if (start >= 0 && end > start && end <= bytes.Length) return Encoding.UTF8.GetString(bytes, start, end - start);
        start = Offset("StartFragment"); end = Offset("EndFragment");
        if (start >= 0 && end >= start && end <= bytes.Length) return Encoding.UTF8.GetString(bytes, start, end - start);
        int opening = value.IndexOf('<');
        return opening >= 0 ? value[opening..] : value;
    }
}
