using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace StatsDirect.UI.WebReports;

internal static class ReportClipboard
{
    internal const string FragmentFormat = "StatsDirect.ReportFragment.HTML.v1";
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
