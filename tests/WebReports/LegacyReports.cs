using StatsDirect.UI.WebReports;
using System.Net;
using System.Text;

internal static partial class Program
{
    // Run first in a fresh process. CSV/XLSB import registers legacy encodings
    // globally and would otherwise hide a missing registration in the RTF path.
    private static async Task CheckLegacyReports()
    {
        string western = Path.Combine(output, "legacy-western.rtf");
        File.WriteAllText(western, @"{\rtf1\ansi\ansicpg1252\deff0{\fonttbl{\f0\fcharset0 Arial;}}\f0\fs20 Caf\'e9 \'96 \'a3 95% CI \u945?}", Encoding.ASCII);
        var first = LegacyReport.Read(western);
        Check(WebUtility.HtmlDecode(first.html).Contains("Café – £ 95% CI α"), "RTF opens first in a fresh process and preserves Windows-1252 and Unicode text");
        await view.OpenLegacyAsync(first, "Legacy western report");
        Check(await True("document.getElementById('results').innerText.includes('Café – £ 95% CI α')"), "legacy report text reaches the editable browser report");

        string cyrillic = Path.Combine(output, "legacy-cyrillic.rtf");
        File.WriteAllText(cyrillic, @"{\rtf1\ansi\ansicpg1251\deff0{\fonttbl{\f0\fcharset204 Arial;}}\f0 \'cf\'f0\'e8\'e2\'e5\'f2}", Encoding.ASCII);
        Check(WebUtility.HtmlDecode(LegacyReport.Read(cyrillic).html).Contains("Привет"), "RTF retains text in another declared Windows code page");

        foreach (string file in new[] { "layout.rtf", "legacy-charts.rtf", "standard-wmf.rtf", "raster-picture.rtf", "binary-picture.rtf", "damaged-picture.rtf" })
        {
            await view.OpenLegacyAsync(LegacyReport.Read(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Reports", file)), file);
            Check(await True("!document.getElementById('results').innerText.includes('STATSDIRECT_PICTURE_') && !document.querySelector('#results script,#results iframe')"), $"RTF {file} imports safely without unresolved picture markers");
            if (file == "layout.rtf")
                Check(await True("!!document.querySelector('#results table') && document.getElementById('results').innerText.includes('Trial A')"), "legacy report tables and values survive import");
            if (file is "legacy-charts.rtf" or "standard-wmf.rtf" or "binary-picture.rtf")
                Check(await True("!!document.querySelector('#results svg')"), $"RTF {file} converts its legacy chart to SVG");
            if (file == "raster-picture.rtf")
                Check(await True("!!document.querySelector('#results img')"), "legacy raster picture survives import");
            if (file == "damaged-picture.rtf")
                Check(await True("document.getElementById('results').innerText.includes('could not be converted')"), "damaged legacy chart leaves an explicit placeholder in the readable report");
        }
        await view.CallAsync("load", new { entries = Array.Empty<object>() });
    }
}
