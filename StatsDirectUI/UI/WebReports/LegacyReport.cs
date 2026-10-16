using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using RtfPipe;
using RtfPipe.Tokens;

namespace StatsDirect.UI.WebReports;

/// <summary>Convert legacy reports to the same sanitized HTML/SVG import path as the Mac host.</summary>
internal static class LegacyReport
{
    // RtfPipe initializes its default Windows-1252 encoding on first use.
    // Register here so opening a report never depends on a prior CSV/XLSB import.
    static LegacyReport() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    internal sealed record PictureData(string marker, string format, string data, double width, double height);
    internal sealed record Document(string html, List<PictureData> pictures);

    internal static Document Read(string path)
    {
        if (new FileInfo(path).Length > 30_000_000) throw new InvalidDataException("This report exceeds the 30 MB report limit.");
        byte[] bytes = File.ReadAllBytes(path);
        Validate(bytes);
        var pictures = new List<PictureData>();
        using var input = new MemoryStream(bytes, false);
        string html = Rtf.ToHtml(input, new RtfHtmlSettings
        {
            ImageUriGetter = picture =>
            {
                if (pictures.Count >= 200 || picture.Bytes.Length > 15_000_000) throw new InvalidDataException("This report contains too much picture data.");
                string format = picture.Type switch { EmfBlip => "emf", WmMetafile => "wmf", PngBlip => "png", JpegBlip => "jpeg", _ => "unsupported" };
                string marker = "STATSDIRECT_PICTURE_" + Guid.NewGuid().ToString("N");
                pictures.Add(new(marker, format, Convert.ToBase64String(picture.Bytes), picture.Width.ToPx(), picture.Height.ToPx()));
                return marker;
            }
        });
        foreach (var picture in pictures)
            html = Regex.Replace(html, "<img\\b[^>]*\\bsrc=[\"']" + picture.marker + "[\"'][^>]*>", picture.marker, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
        return new(html, pictures);
    }

    private static void Validate(byte[] bytes)
    {
        if (bytes.Length < 6 || Encoding.ASCII.GetString(bytes, 0, 5) != "{\\rtf") throw new InvalidDataException("This is not an RTF report.");
        int depth = 0;
        for (int i = 0; i < bytes.Length; i++)
        {
            int b = bytes[i];
            if (b == '{' && ++depth > 128) throw new InvalidDataException("This RTF report is too deeply nested.");
            if (b == '}' && --depth < 0) throw new InvalidDataException("This RTF report has unbalanced groups.");
            if (b != '\\') continue;
            if (++i >= bytes.Length) break;
            if (bytes[i] is (byte)'\\' or (byte)'{' or (byte)'}') continue;
            int start = i;
            while (i < bytes.Length && bytes[i] is >= (byte)'a' and <= (byte)'z') i++;
            string word = Encoding.ASCII.GetString(bytes, start, i - start);
            if (word != "bin") { i--; continue; }
            int number = i;
            while (i < bytes.Length && bytes[i] is >= (byte)'0' and <= (byte)'9') i++;
            if (!int.TryParse(Encoding.ASCII.GetString(bytes, number, i-number), out int length) || length < 0 || length > bytes.Length-i)
                throw new InvalidDataException("This RTF report has invalid binary picture data.");
            if (i < bytes.Length && bytes[i] == ' ') i++;
            if (length > bytes.Length-i) throw new InvalidDataException("This RTF report has truncated binary picture data.");
            i += length-1;
        }
        if (depth != 0) throw new InvalidDataException("This RTF report has unbalanced groups.");
    }
}
