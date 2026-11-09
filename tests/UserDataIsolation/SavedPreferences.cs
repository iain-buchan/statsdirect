using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using StatsDirect.Charting;
using StatsDirect.Configuration;
using StatsDirect.UI.Properties;

// Saved preferences, checked only in this run's isolated profile: their readers, the loader the program's start-up runs, and the main
// window's placement. The real preferences file is never read or written: every path is checked against the profile before it is used.
internal static partial class Program
{
    static string preferencesProfile, realProfile;

    // A path in this run's own isolated profile, or a stop before anything is touched
    static string Inside(string path)
    {
        string full = Path.GetFullPath(path);
        if (!full.StartsWith(preferencesProfile + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            full.Equals(realProfile, StringComparison.OrdinalIgnoreCase) || full.StartsWith(realProfile + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("A saved preferences check reached outside its isolated profile: " + full);
        return full;
    }

    static void CheckSavedPreferences(string root, string normal)
    {
        preferencesProfile = root;
        realProfile = normal;
        Check(Inside(SDConfiguration.SettingsPath) == Path.Combine(root, "statsdirect.json"), "the saved preferences checks use only the isolated profile");
        CheckSavedChartPreferences();
    }

    // Series markers as SaveMarkerTypes writes them: shape;red,green,blue;width;line style;filled;size, ten of them separated by |
    static string Marker(int shape, string colour, string width, int style, string filled = "0", string size = "6") => $"{shape};{colour};{width};{style};{filled};{size}";
    static readonly string[] CustomMarkers = Enumerable.Range(0, 10).Select(i => Marker(i % 8 + 1, $"{10 * i},{20 + i},{255 - i}",
        (i % 4 + 1).ToString(CultureInfo.InvariantCulture), i % 5, i % 2 == 0 ? "1" : "0", (5 + i).ToString(CultureInfo.InvariantCulture))).ToArray();
    static readonly string CustomMarkerSettings = string.Join("|", CustomMarkers);
    static readonly string OldMarkerSettings = string.Join("|", Enumerable.Repeat("1;64,105,156;1;0", 10)); // saved before the filled flag and size
    static string ReplaceMarker(int index, string marker) => string.Join("|", CustomMarkers.Select((m, i) => i == index ? marker : m));
    static readonly string[] UnusableMarkerSettings =
    {
        string.Join("|", CustomMarkers.Take(9)), ReplaceMarker(3, Marker(0, "1,2,3", "1", 0)), ReplaceMarker(3, Marker(9, "1,2,3", "1", 0)),
        ReplaceMarker(3, Marker(10, "1,2,3", "1", 0)), ReplaceMarker(3, Marker(1, "256,0,0", "1", 0)), ReplaceMarker(3, Marker(1, "-1,0,0", "1", 0)),
        ReplaceMarker(3, Marker(1, "1,2", "1", 0)), ReplaceMarker(3, Marker(1, "1,2,3,4", "1", 0)), ReplaceMarker(3, Marker(1, "a,b,c", "1", 0)),
        ReplaceMarker(3, Marker(1, "1,2,3", "0", 0)), ReplaceMarker(3, Marker(1, "1,2,3", "5", 0)), ReplaceMarker(3, Marker(1, "1,2,3", "NaN", 0)),
        ReplaceMarker(3, Marker(1, "1,2,3", "1,5", 0)), ReplaceMarker(3, Marker(1, "1,2,3", "1", 5)), ReplaceMarker(3, Marker(1, "1,2,3", "1", -1)),
        ReplaceMarker(3, "1;1,2,3;1"), ReplaceMarker(3, ""), ReplaceMarker(3, "one;1,2,3;1;0"), "|||||||||", "x"
    };

    // The chart defaults take saved markers and fonts as saved; a damaged one gives way to the default instead of stopping a chart
    static void CheckSavedChartPreferences()
    {
        Settings.Default.Markers = "";
        Settings.Default.TitleFont = "";
        Settings.Default.LabelFont = "";
        ChartPreferences.InitSharedValues();
        string defaultMarkers = Settings.Default.Markers; // the markers a first chart saves
        MarkerType[] defaultMarkerTypes = ChartPreferences.MarkerTypes.Select(m => m.Clone()).ToArray();
        static bool SameMarker(MarkerType a, MarkerType b) => a.MarkerShape == b.MarkerShape && a.MarkerColor.Equals(b.MarkerColor) && a.LineColor.Equals(b.LineColor) &&
            a.Width == b.Width && a.LineDashStyle == b.LineDashStyle && a.IsMarkerFilled == b.IsMarkerFilled && a.MarkerSize == b.MarkerSize;
        List<string> stopped = new();
        foreach (string markers in UnusableMarkerSettings)
        {
            Settings.Default.Markers = markers;
            try
            {
                ChartPreferences.InitSharedValues();
                if (ChartPreferences.TryParseMarkerTypes(markers, out _) || Settings.Default.Markers != defaultMarkers ||
                    !ChartPreferences.MarkerTypes.Zip(defaultMarkerTypes).All(pair => SameMarker(pair.First, pair.Second)))
                    stopped.Add(markers);
            }
            catch (Exception ex) { stopped.Add(markers + " (" + ex.GetType().Name + ")"); }
        }
        Check(defaultMarkers.Split('|').Length == 10 && stopped.Count == 0, $"each of {UnusableMarkerSettings.Length} damaged marker settings gives the default markers to charts" +
            (stopped.Count == 0 ? "" : ": " + string.Join("; ", stopped)));
        Settings.Default.Markers = CustomMarkerSettings;
        ChartPreferences.InitSharedValues();
        MarkerType[] read = ChartPreferences.MarkerTypes;
        Check(Enumerable.Range(0, 10).All(i => (int)read[i].MarkerShape == i % 8 + 1 && read[i].MarkerColor.Equals(ColorDescriptor.FromArgb(10 * i, 20 + i, 255 - i)) &&
                read[i].LineColor.Equals(read[i].MarkerColor) && read[i].Width == i % 4 + 1 && (int)read[i].LineDashStyle == i % 5 &&
                read[i].IsMarkerFilled == (i % 2 == 0) && read[i].MarkerSize == 5 + i) &&
            read[10].MarkerShape == MarkerShape.Circle && read[10].LineDashStyle == DashStyleDescriptor.Dash,
            "saved series markers reach the chart defaults as saved, beside the fixed eleventh marker");
        Check(read.Take(10).All(m => (int)m.MarkerShape - 1 is >= 0 and <= 7 && (int)m.Width - 1 is >= 0 and <= 3 && (int)m.LineDashStyle is >= 0 and <= 4),
            "every marker read can be shown in the graphics options");
        ChartPreferences.SaveMarkerTypes(); // into the isolated profile
        Check(Settings.Default.Markers == CustomMarkerSettings, "markers read from preferences are saved again unchanged");
        Check(ChartPreferences.TryParseMarkerTypes(OldMarkerSettings, out MarkerType[] old) && old.All(m => !m.IsMarkerFilled && m.MarkerSize == 6),
            "markers saved before the filled flag and size existed are still read, hollow and of size 6");
        CultureInfo culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            Check(ChartPreferences.TryParseMarkerTypes(ReplaceMarker(0, Marker(1, "1,2,3", "1.5", 0)), out MarkerType[] german) && german[0].Width == 1.5f,
                "marker widths are read in the invariant culture they are saved in, where the decimal separator is a comma too");
        }
        finally { CultureInfo.CurrentCulture = culture; }
        Settings.Default.TitleFont = "Georgia;1;20pt";
        Settings.Default.LabelFont = "Georgia;0;12pt";
        ChartPreferences.InitSharedValues();
        Check(ChartPreferences.DefaultTitleFont.Equals(new FontDescriptor("Georgia", 1, 20)) && ChartPreferences.DefaultAxisLabelFont.Equals(new FontDescriptor("Georgia", 0, 12)) &&
            ChartPreferences.DefaultLegendFont.Equals(new FontDescriptor("Georgia", 0, 12)), "saved chart fonts reach the chart defaults");
        List<string> fontsKept = new();
        foreach (string font in new[] { "Arial;1;NaNpt", "Arial;1;-4pt", "Arial;1;0pt", "Arial;1;Infinitypt", "Arial;64;12pt", "Arial;-1;12pt" })
        {
            Settings.Default.TitleFont = font;
            ChartPreferences.InitSharedValues();
            if (!ChartPreferences.DefaultTitleFont.Equals(new FontDescriptor("Arial", 1, 22)))
                fontsKept.Add(font);
        }
        Check(fontsKept.Count == 0, "a damaged chart font gives way to the default fonts" + (fontsKept.Count == 0 ? "" : ": kept " + string.Join("; ", fontsKept)));
    }
}
