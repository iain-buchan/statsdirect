using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace StatsDirect.UI.WebReports;

/// <summary>Vector labels for Office's SVG-to-shape importer, without changing report text.</summary>
internal static class OfficeSvg
{
    private const int MaximumBytes = 20_000_000;
    private const int MaximumLabelLength = 4096;
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private static readonly Regex Length = new(@"^([+-]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][+-]?\d+)?)(?:px)?$", RegexOptions.CultureInvariant);

    internal static string ForClipboard(string svg)
    {
        // R's copied SVG already consists of glyph paths. Leave it byte-for-byte
        // intact, along with any SVG that does not have text to convert.
        if (string.IsNullOrEmpty(svg) || (!svg.Contains("<text", StringComparison.Ordinal) && !svg.Contains(":text", StringComparison.Ordinal))
            || Encoding.UTF8.GetByteCount(svg) > MaximumBytes) return svg;
        XDocument document;
        try
        {
            using var reader = XmlReader.Create(new StringReader(svg), new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = MaximumBytes
            });
            document = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
        }
        catch (XmlException) { return svg; }
        XNamespace ns = "http://www.w3.org/2000/svg";
        if (document.Root?.Name != ns + "svg" || document.Descendants(ns + "style").Any()) return svg;

        bool changed = false;
        int characters = svg.Length;
        foreach (var text in document.Descendants(ns + "text").ToArray())
        {
            XElement path = Outline(text, ns);
            if (path == null) continue;
            characters += path.ToString(SaveOptions.DisableFormatting).Length - text.ToString(SaveOptions.DisableFormatting).Length;
            if (characters > MaximumBytes) return svg;
            text.ReplaceWith(path);
            changed = true;
        }
        if (!changed) return svg;
        string result = document.ToString(SaveOptions.DisableFormatting);
        return Encoding.UTF8.GetByteCount(result) <= MaximumBytes ? result : svg;
    }

    private static XElement Outline(XElement text, XNamespace ns)
    {
        // exportableSvg has already resolved native chart spans, anchors and
        // baseline shifts. Do not guess at the layout of other SVG text.
        if (text.HasElements || text.Value.Length > MaximumLabelLength || text.Attribute("style") != null
            || new[] { "dx", "dy", "rotate", "textLength", "lengthAdjust" }.Any(name => text.Attribute(name) != null)
            || !Default(text, "text-anchor", "start")
            || !Default(text, "writing-mode", "horizontal-tb")
            || !Default(text, "dominant-baseline", "auto", "alphabetic")
            || !Default(text, "alignment-baseline", "auto", "baseline")
            || !Default(text, "baseline-shift", "baseline", "0")
            || !Default(text, "letter-spacing", "normal", "0")
            || !Default(text, "word-spacing", "normal", "0")) return null;
        if (!Number(text.Attribute("x")?.Value ?? "0", out float x)
            || !Number(text.Attribute("y")?.Value ?? "0", out float y)
            || !Number(Inherited(text, "font-size"), out float size) || size <= 0 || size > 1024) return null;
        string familyName = Inherited(text, "font-family");
        if (string.IsNullOrWhiteSpace(familyName)) return null;

        FontStyle style = FontStyle.Regular;
        string weight = Inherited(text, "font-weight");
        if (!string.IsNullOrEmpty(weight) && weight != "normal")
        {
            if (weight == "bold") style |= FontStyle.Bold;
            else if (int.TryParse(weight, NumberStyles.Integer, Invariant, out int numeric) && numeric >= 100 && numeric <= 900)
            { if (numeric >= 600) style |= FontStyle.Bold; }
            else return null;
        }
        string slant = Inherited(text, "font-style");
        if (slant == "italic") style |= FontStyle.Italic;
        else if (!string.IsNullOrEmpty(slant) && slant != "normal") return null;

        using var family = Family(familyName);
        if (family == null || !family.IsStyleAvailable(style)) return null;
        string label = text.Value;
        if (Inherited(text, XNamespace.Xml + "space") == "preserve")
        {
            if (label.IndexOfAny(new[] { '\r', '\n', '\t' }) >= 0) return null;
        }
        else label = Regex.Replace(label, @"[\t\r\n ]+", " ").Trim(' ');
        if (label.Length == 0) return null;

        try
        {
            using var drawing = new GraphicsPath();
            using var format = (StringFormat)StringFormat.GenericTypographic.Clone();
            format.FormatFlags |= StringFormatFlags.NoWrap | StringFormatFlags.MeasureTrailingSpaces;
            if (LacksGlyph(label, family, style, size, format)) return null;   // the text stays, for the receiving program's own fallback
            float ascent = size * family.GetCellAscent(style) / family.GetEmHeight(style);
            drawing.AddString(label, family, (int)style, size, new PointF(x, y - ascent), format);
            string data = PathData(drawing);
            if (data == null) return null;
            var result = new XElement(ns + "path",
                text.Attributes().Where(a => !a.IsNamespaceDeclaration && a.Name.LocalName is not
                    ("x" or "y" or "font-family" or "font-size" or "font-weight" or "font-style" or "text-anchor"
                    or "dominant-baseline" or "alignment-baseline" or "baseline-shift" or "letter-spacing" or "word-spacing" or "writing-mode")
                    && a.Name != XNamespace.Xml + "space").Select(a => new XAttribute(a)));
            result.SetAttributeValue("d", data);
            // Glyph outlines are made for the nonzero rule, as every text rasteriser fills them: the counters of O and B stay open,
            // and the overlapping strokes of a font such as Bahnschrift stay solid, where even-odd would cut holes
            result.SetAttributeValue("fill-rule", "nonzero");
            if (result.Attribute("aria-label") == null) result.SetAttributeValue("aria-label", label);
            result.Add(new XElement(ns + "title", label));
            return result;
        }
        catch (ArgumentException) { return null; }
    }

    // A character the font and the fonts linked to it lack is drawn as the font's .notdef box, which AddString does not report:
    // the box is known as the outline of a code point no font assigns, and no supplementary-plane character comes through GDI+
    // font linking at all
    private static bool LacksGlyph(string label, FontFamily family, FontStyle style, float size, StringFormat format)
    {
        if (label.Any(char.IsSurrogate)) return true;
        PointF[] notdef = Glyph("\u0530", family, style, size, format);
        if (notdef.Length == 0) return false;
        foreach (char character in label.Distinct())
        {
            if (char.IsWhiteSpace(character)) continue;
            PointF[] glyph = Glyph(character.ToString(), family, style, size, format);
            if (glyph.Length == notdef.Length && glyph.SequenceEqual(notdef)) return true;
        }
        return false;
    }
    private static PointF[] Glyph(string text, FontFamily family, FontStyle style, float size, StringFormat format)
    {
        using var path = new GraphicsPath();
        path.AddString(text, family, (int)style, size, PointF.Empty, format);
        return path.PointCount == 0 ? Array.Empty<PointF>() : path.PathPoints;
    }

    private static string Inherited(XElement element, XName name) => element.AncestorsAndSelf()
        .Select(e => (string)e.Attribute(name)).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static bool Default(XElement element, string name, params string[] values)
    {
        string value = Inherited(element, name);
        return string.IsNullOrEmpty(value) || values.Contains(value, StringComparer.Ordinal);
    }

    private static bool Number(string value, out float result)
    {
        Match match = Length.Match(value?.Trim() ?? "");
        result = 0;
        return match.Success && float.TryParse(match.Groups[1].Value, NumberStyles.Float, Invariant, out result) && float.IsFinite(result);
    }

    private static FontFamily Family(string list)
    {
        foreach (string item in list.Split(','))
        {
            string name = item.Trim().Trim('\'', '"');
            try
            {
                return name.ToLowerInvariant() switch
                {
                    "sans-serif" or "system-ui" => FontFamily.GenericSansSerif,
                    "serif" => FontFamily.GenericSerif,
                    "monospace" => FontFamily.GenericMonospace,
                    _ => new FontFamily(name)
                };
            }
            catch (ArgumentException) { }
        }
        return null;
    }

    private static string PathData(GraphicsPath path)
    {
        PointF[] points = path.PathPoints;
        byte[] types = path.PathTypes;
        if (points.Length == 0) return null;
        var result = new StringBuilder();
        void Point(int index) => result.Append(points[index].X.ToString("R", Invariant)).Append(' ')
            .Append(points[index].Y.ToString("R", Invariant)).Append(' ');
        for (int i = 0; i < points.Length; i++)
        {
            int type = types[i] & (int)PathPointType.PathTypeMask;
            if (type == (int)PathPointType.Start) { result.Append('M'); Point(i); }
            else if (type == (int)PathPointType.Line) { result.Append('L'); Point(i); }
            else if (type == (int)PathPointType.Bezier3)
            {
                if (i + 2 >= points.Length || (types[i + 1] & 7) != 3 || (types[i + 2] & 7) != 3) return null;
                result.Append('C'); Point(i); Point(i + 1); Point(i + 2); i += 2;
            }
            else return null;
            if ((types[i] & (int)PathPointType.CloseSubpath) != 0) result.Append('Z');
        }
        return result.ToString();
    }
}
