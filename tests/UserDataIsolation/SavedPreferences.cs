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
        CheckPreferencesLoader();
        CheckMainWindowPlacement();
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
    static readonly Rectangle[] Laptop = { new(0, 0, 1920, 1040) }; // the working area of a 1920 by 1080 screen above its taskbar
    static string Json(string text) => JsonSerializer.Serialize(text);

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

    // The loader: missing, damaged and older files, each preference's own validation, the window's bounds, recent files, and no writes
    static void CheckPreferencesLoader()
    {
        string root = preferencesProfile;
        string profileSettings = Inside(SDConfiguration.SettingsPath);
        string folder = Inside(Path.Combine(root, "preferences-load"));
        Directory.CreateDirectory(folder);
        string path = Inside(Path.Combine(folder, "statsdirect.json"));
        Rectangle[] withLeftMonitor = { new(0, 0, 1920, 1040), new(-1920, 0, 1920, 1040) };
        Rectangle[] withRightMonitor = { new(0, 0, 1920, 1040), new(1920, 0, 2560, 1400) };

        PropertyInfo[] persisted = typeof(Settings).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite && p.GetCustomAttribute<JsonIgnoreAttribute>() == null).ToArray();
        bool Same(object a, object b) => a is IEnumerable<string> x && b is IEnumerable<string> y ? x.SequenceEqual(y) : Equals(a, b);
        string Differences(Settings a, Settings b) => string.Join(", ", persisted.Where(p => !Same(p.GetValue(a), p.GetValue(b))).Select(p => p.Name));
        bool IsDefault(Settings settings) => Differences(settings, new Settings()) == "";
        string FolderSnapshot(string directory) => string.Join("\n", Directory.EnumerateFileSystemEntries(directory, "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal).Select(entry => entry + ":" + (File.Exists(entry)
                ? new FileInfo(entry).Length + ":" + File.GetLastWriteTimeUtc(entry).Ticks + ":" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(entry)))
                : "folder")));
        int loads = 0, writes = 0;
        Settings LoadText(string json, IReadOnlyList<Rectangle> screens = null)
        {
            File.WriteAllText(path, json);
            string before = FolderSnapshot(root); // the whole profile, its own statsdirect.json too
            Settings loaded = Settings.Load(path, screens ?? Laptop);
            loads++;
            if (FolderSnapshot(root) != before) writes++;
            return loaded;
        }

        // A missing folder or file gives every default, and loading creates neither
        string absentFolder = Inside(Path.Combine(root, "preferences-absent"));
        Settings fromAbsentFolder = Settings.Load(Inside(Path.Combine(absentFolder, "statsdirect.json")), Laptop);
        Check(IsDefault(fromAbsentFolder) && !fromAbsentFolder.WasLoaded && !Directory.Exists(absentFolder), "a missing preferences folder gives every default and is not created");
        string emptyFolder = FolderSnapshot(folder);
        Settings fromAbsentFile = Settings.Load(path, Laptop);
        Check(IsDefault(fromAbsentFile) && !fromAbsentFile.WasLoaded && !File.Exists(path) && FolderSnapshot(folder) == emptyFolder,
            "a missing preferences file gives every default and is not created");

        // A damaged or unreadable file gives every default
        string[] damaged = { "", "   ", "{", "{\"pDecimalPlaces\": 6, \"displayDec", "not JSON", "\u0001\u0002\u0003", "{\"pDecimalPlaces\": 6}}",
            "[{\"pDecimalPlaces\": 6}]", "\"pDecimalPlaces\"", "6", "null", "true" };
        Check(damaged.All(json => LoadText(json) is { WasLoaded: false } loaded && IsDefault(loaded)), $"each of {damaged.Length} damaged or non-object preferences files gives every default");
        File.WriteAllText(path, "{\"pDecimalPlaces\": 6}", Encoding.Unicode);
        Check(IsDefault(Settings.Load(path, Laptop)), "a preferences file in another encoding gives every default");
        File.WriteAllText(path, "{\"pDecimalPlaces\": 6}");
        using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            Check(IsDefault(Settings.Load(path, Laptop)), "a preferences file another program holds locked gives every default");
        using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
            Check(Settings.Load(path, Laptop).PDecimalPlaces == 6, "a preferences file another program has open for writing is still read");
        Check(IsDefault(LoadText("{\"pDecimalPlaces\": 6" + new string(' ', (1 << 20) + 1) + "}")) &&
            LoadText("{\"pDecimalPlaces\": 6" + new string(' ', 4096) + "}").PDecimalPlaces == 6, "a preferences file of more than 1 MiB is not read; one of ordinary size is");
        Check(LoadText("{\n  // edited by hand\n  \"pDecimalPlaces\": 6,\n  \"shouldKeepData\": true,\n}") is { WasLoaded: true, PDecimalPlaces: 6, ShouldKeepData: true },
            "a comment and a trailing comma in a hand-edited file are tolerated");

        // A file from an older version: the preferences it holds, the retired Tools programs ignored, the defaults of later preferences.
        // Closed maximised, StatsDirect 5.3 saved no bounds with the state; the state is kept without them.
        Settings fromOlder = LoadText("{\"mainTop\":0,\"mainLeft\":0,\"mainWidth\":0,\"mainHeight\":0,\"mainWindowState\":2,\"metaPlotCI\":true," +
            "\"metaPlotMethod\":3,\"pDecimalPlaces\":5,\"metaCC\":0.5,\"metaExact\":false,\"delayContinuityCorrection\":true," +
            "\"canDefaultConfidenceInterval\":true,\"defaultConfidenceInterval\":0.9,\"recentFileList\":null,\"selectGroupsByIdentifier\":true," +
            "\"toolsNames\":[\"Calculator\",\"Notepad\",\"Wordpad\"],\"toolsPrograms\":[\"%STATSDIRECT%\\\\StatsDirect.exe -calculator\",\"notepad.exe\",\"wordpad.exe\"]," +
            "\"shouldKeepData\":false,\"displayDecimalPlaces\":8,\"shouldUseColour\":true,\"defaultWorkbookFont\":\"Calibri;0;11\",\"calculatorTop\":0," +
            "\"calculatorLeft\":0,\"calculatorWidth\":0,\"calculatorHeight\":0,\"calculatorMaximized\":false,\"markers\":\"\",\"titleFont\":\"\"," +
            "\"labelFont\":\"\",\"boxAxes\":true,\"blackAndWhite\":false}");
        Check(fromOlder is { WasLoaded: true, MainWindowState: FormWindowState.Maximized, MainWidth: 0, MetaPlotMethod: 3, PDecimalPlaces: 5, MetaCC: 0.5,
                MetaExact: false, DelayContinuityCorrection: true, DefaultConfidenceInterval: 0.9, SelectGroupsByIdentifier: true, DisplayDecimalPlaces: 8,
                BoxAxes: true, RecentFileList: null, UseScientificNotationForSmallPValues: false, RequestScaleLimits: false },
            "an older file gives the preferences it holds, ignores the retired Tools programs and keeps the defaults of later preferences");

        // Every preference Save writes is read back: a value of each, unlike its default, survives the real Save and a load
        string[] files = Enumerable.Range(1, 9).Select(i => Inside(Path.Combine(folder, $"recent-{i}.xlsx"))).ToArray();
        foreach (string file in files)
            File.WriteAllText(file, "recent file fixture");
        Settings chosen = new()
        {
            MainTop = 100, MainLeft = 120, MainWidth = 1200, MainHeight = 800, MainWindowState = FormWindowState.Maximized,
            MetaPlotCI = false, MetaPlotMethod = 5, PDecimalPlaces = 6, MetaCC = 0.01, MetaExact = false, DelayContinuityCorrection = true,
            CanDefaultConfidenceInterval = false, DefaultConfidenceInterval = 0.9, RecentFileList = new[] { files[0], files[1] },
            SelectGroupsByIdentifier = true, ShouldKeepData = true, DisplayDecimalPlaces = 9, ShouldUseColour = false, DefaultWorkbookFont = "Cambria;2;12",
            CalculatorTop = 10, CalculatorLeft = 20, CalculatorWidth = 300, CalculatorHeight = 400, CalculatorMaximized = true,
            Markers = CustomMarkerSettings, TitleFont = "Georgia;1;20pt", LabelFont = "Georgia;0;12pt",
            BoxAxes = true, BlackAndWhite = true, RequestScaleLimits = true, UseScientificNotationForSmallPValues = true
        };
        Check(persisted.Length == 31 && Differences(chosen, new Settings()).Split(", ").Length == persisted.Length,
            $"the round-trip fixture changes every one of the {persisted.Length} saved preferences");
        chosen.Save(); // the program's own writer, into the isolated profile
        Settings reread = Settings.Load(profileSettings, Laptop);
        string lost = Differences(reread, chosen);
        Check(reread.WasLoaded && lost == "", "every saved preference survives Save and a load" + (lost == "" ? "" : ": lost " + lost));

        // Each preference keeps its default when its value cannot be used, and that costs only itself: a usable neighbour still loads
        (string json, Func<Settings, bool> kept)[] unusable = new (string, Func<Settings, bool>)[]
        {
            ("\"pDecimalPlaces\": 2", s => s.PDecimalPlaces == 4), ("\"pDecimalPlaces\": 8", s => s.PDecimalPlaces == 4),
            ("\"pDecimalPlaces\": -1", s => s.PDecimalPlaces == 4), ("\"pDecimalPlaces\": 6.5", s => s.PDecimalPlaces == 4),
            ("\"pDecimalPlaces\": \"6\"", s => s.PDecimalPlaces == 4), ("\"pDecimalPlaces\": 1e12", s => s.PDecimalPlaces == 4), ("\"pDecimalPlaces\": null", s => s.PDecimalPlaces == 4),
            ("\"displayDecimalPlaces\": 1", s => s.DisplayDecimalPlaces == 6), ("\"displayDecimalPlaces\": 13", s => s.DisplayDecimalPlaces == 6),
            ("\"displayDecimalPlaces\": 100000000", s => s.DisplayDecimalPlaces == 6),
            ("\"metaPlotMethod\": 0", s => s.MetaPlotMethod == 1), ("\"metaPlotMethod\": 8", s => s.MetaPlotMethod == 1),
            ("\"metaCC\": 0", s => s.MetaCC == -9), ("\"metaCC\": 1", s => s.MetaCC == -9), ("\"metaCC\": 5", s => s.MetaCC == -9),
            ("\"metaCC\": -1", s => s.MetaCC == -9), ("\"metaCC\": \"0.5\"", s => s.MetaCC == -9), ("\"metaCC\": 1e400", s => s.MetaCC == -9),
            ("\"defaultConfidenceInterval\": 0", s => s.DefaultConfidenceInterval == 0.95), ("\"defaultConfidenceInterval\": 1", s => s.DefaultConfidenceInterval == 0.95),
            ("\"defaultConfidenceInterval\": 95", s => s.DefaultConfidenceInterval == 0.95), ("\"defaultConfidenceInterval\": -0.5", s => s.DefaultConfidenceInterval == 0.95),
            ("\"defaultConfidenceInterval\": true", s => s.DefaultConfidenceInterval == 0.95), ("\"defaultConfidenceInterval\": 1e400", s => s.DefaultConfidenceInterval == 0.95),
            ("\"mainWindowState\": 3", s => s.MainWindowState == FormWindowState.Normal), ("\"mainWindowState\": -1", s => s.MainWindowState == FormWindowState.Normal),
            ("\"mainWindowState\": \"Maximized\"", s => s.MainWindowState == FormWindowState.Normal),
            ("\"metaExact\": \"false\"", s => s.MetaExact), ("\"metaExact\": 0", s => s.MetaExact), ("\"metaExact\": null", s => s.MetaExact),
            ("\"selectGroupsByIdentifier\": 1", s => !s.SelectGroupsByIdentifier), ("\"useScientificNotationForSmallPValues\": \"true\"", s => !s.UseScientificNotationForSmallPValues),
            ("\"defaultWorkbookFont\": \"Calibri;0;abc\"", s => s.DefaultWorkbookFont == "Calibri;0;11"), ("\"defaultWorkbookFont\": \"Calibri;0;0\"", s => s.DefaultWorkbookFont == "Calibri;0;11"),
            ("\"defaultWorkbookFont\": \"Calibri;0;500\"", s => s.DefaultWorkbookFont == "Calibri;0;11"), ("\"defaultWorkbookFont\": \"Calibri;0;11pt\"", s => s.DefaultWorkbookFont == "Calibri;0;11"),
            ("\"defaultWorkbookFont\": \";0;11\"", s => s.DefaultWorkbookFont == "Calibri;0;11"), ("\"defaultWorkbookFont\": \"Calibri;16;11\"", s => s.DefaultWorkbookFont == "Calibri;0;11"),
            ("\"defaultWorkbookFont\": \"Calibri;0\"", s => s.DefaultWorkbookFont == "Calibri;0;11"), ("\"defaultWorkbookFont\": \"Calibri;0;11;12\"", s => s.DefaultWorkbookFont == "Calibri;0;11"),
            ("\"defaultWorkbookFont\": \"Calibri;0;NaN\"", s => s.DefaultWorkbookFont == "Calibri;0;11"), ("\"defaultWorkbookFont\": 42", s => s.DefaultWorkbookFont == "Calibri;0;11"),
            ("\"defaultWorkbookFont\": null", s => s.DefaultWorkbookFont == "Calibri;0;11"),
            ("\"titleFont\": \"Arial;0;-3pt\"", s => s.TitleFont == ""), ("\"titleFont\": \"Arial;99;12pt\"", s => s.TitleFont == ""),
            ("\"titleFont\": \"Arial;1;NaNpt\"", s => s.TitleFont == ""), ("\"titleFont\": \"Arial;1;Infinitypt\"", s => s.TitleFont == ""),
            ("\"titleFont\": \"Arial;1;0pt\"", s => s.TitleFont == ""), ("\"titleFont\": \" ;1;12pt\"", s => s.TitleFont == ""),
            ("\"titleFont\": \"Arial;1\"", s => s.TitleFont == ""), ("\"titleFont\": 12", s => s.TitleFont == ""), ("\"labelFont\": \"Arial;0;NaNpt\"", s => s.LabelFont == ""),
            ("\"recentFileList\": " + Json(files[0]), s => s.RecentFileList == null), ("\"recentFileList\": {}", s => s.RecentFileList == null),
            ("\"calculatorTop\": \"10\"", s => s.CalculatorTop == 0)
        }.Concat(UnusableMarkerSettings.Select(markers => ("\"markers\": " + Json(markers), (Func<Settings, bool>)(s => s.Markers == "")))).ToArray();
        List<string> notKept = new();
        foreach ((string json, Func<Settings, bool> kept) in unusable)
            if (LoadText("{" + json + ", \"shouldKeepData\": true}") is not { WasLoaded: true, ShouldKeepData: true } loaded || !kept(loaded))
                notKept.Add(json);
        Check(notKept.Count == 0, $"each of {unusable.Length} unusable values keeps its preference's default while a usable neighbour loads" +
            (notKept.Count == 0 ? "" : ": " + string.Join("; ", notKept)));
        (string json, Func<Settings, bool> taken)[] usable =
        {
            ("\"pDecimalPlaces\": 3", s => s.PDecimalPlaces == 3), ("\"pDecimalPlaces\": 7", s => s.PDecimalPlaces == 7),
            ("\"displayDecimalPlaces\": 2", s => s.DisplayDecimalPlaces == 2), ("\"displayDecimalPlaces\": 12", s => s.DisplayDecimalPlaces == 12),
            ("\"metaPlotMethod\": 7", s => s.MetaPlotMethod == 7), ("\"metaCC\": 0.5", s => s.MetaCC == 0.5), ("\"metaCC\": 0.01", s => s.MetaCC == 0.01),
            ("\"defaultConfidenceInterval\": 0.8", s => s.DefaultConfidenceInterval == 0.8), ("\"defaultConfidenceInterval\": 0.99", s => s.DefaultConfidenceInterval == 0.99),
            ("\"mainWindowState\": 1", s => s.MainWindowState == FormWindowState.Minimized), // the main window opens a minimised one normal
            ("\"defaultWorkbookFont\": \"Calibri;1;409\"", s => s.DefaultWorkbookFont == "Calibri;1;409"),
            ("\"defaultWorkbookFont\": \"Cambria;15;1\"", s => s.DefaultWorkbookFont == "Cambria;15;1"),
            ("\"titleFont\": \"Arial;15;8pt\"", s => s.TitleFont == "Arial;15;8pt"), ("\"titleFont\": \"Arial;0;15\"", s => s.TitleFont == "Arial;0;15"),
            ("\"markers\": " + Json(OldMarkerSettings), s => s.Markers == OldMarkerSettings), ("\"markers\": " + Json(CustomMarkerSettings), s => s.Markers == CustomMarkerSettings)
        };
        List<string> notTaken = new();
        foreach ((string json, Func<Settings, bool> taken) in usable)
            if (LoadText("{" + json + ", \"shouldKeepData\": true}") is not { WasLoaded: true, ShouldKeepData: true } loaded || !taken(loaded))
                notTaken.Add(json);
        Check(notTaken.Count == 0, $"each of {usable.Length} usable values at the edges of its preference's choices is taken" +
            (notTaken.Count == 0 ? "" : ": " + string.Join("; ", notTaken)));

        // The main window's bounds against the screens there are now
        (Rectangle window, Rectangle[] screens, bool usable, string place)[] windows =
        {
            (new(100, 100, 1200, 800), Laptop, true, "on the screen"),
            (new(-7, 0, 974, 1047), Laptop, true, "snapped to half the screen, with its invisible borders"),
            (new(1200, 100, 1400, 800), Laptop, true, "just over half on the screen"),
            (new(1300, 100, 1400, 800), Laptop, false, "less than half on the screen"),
            (new(2100, 100, 1200, 800), Laptop, false, "on a monitor since disconnected"),
            (new(2100, 100, 1200, 800), withRightMonitor, true, "on a second monitor still there"),
            (new(-1800, 100, 1200, 800), withLeftMonitor, true, "on a monitor to the left"),
            (new(-1800, 100, 1200, 800), Laptop, false, "on a monitor to the left since disconnected"),
            (new(100, -400, 1200, 800), Laptop, false, "with its title bar above the screen"),
            (new(100, 1030, 1200, 20), Laptop, false, "with its title bar behind the taskbar"),
            (new(0, 0, 3000, 1800), Laptop, false, "larger than the screen, saved on a bigger one"),
            (new(100, 100, 0, 800), Laptop, false, "without a width"),
            (new(100, 100, 1200, -5), Laptop, false, "with a negative height"),
            (new(100, 100, 40, 10), Laptop, false, "too small to show its title bar"),
            (new(int.MaxValue - 10, 0, 1000, 800), Laptop, false, "beyond the last coordinate across"),
            (new(0, int.MaxValue - 5, 800, 600), Laptop, false, "beyond the last coordinate down"),
            (new(int.MinValue, 0, int.MaxValue, 800), Laptop, false, "of a damaged size"),
            (new(100, 100, 1200, 800), Array.Empty<Rectangle>(), false, "with no screen")
        };
        List<string> misjudged = windows.Where(w => Settings.IsUsableWindowRectangle(w.window, w.screens) != w.usable).Select(w => w.place).ToList();
        Check(misjudged.Count == 0, $"each of {windows.Length} saved window rectangles is judged against the screens: seen and moved there, or not" +
            (misjudged.Count == 0 ? "" : ": misjudged " + string.Join("; ", misjudged)));
        static string Bounds(int left, int top, int width, int height, int state = 0) =>
            $"\"mainLeft\": {left}, \"mainTop\": {top}, \"mainWidth\": {width}, \"mainHeight\": {height}, \"mainWindowState\": {state}, \"pDecimalPlaces\": 6";
        Check(LoadText("{" + Bounds(100, 100, 1200, 800) + "}") is { MainLeft: 100, MainTop: 100, MainWidth: 1200, MainHeight: 800 }, "a window saved on the screen opens where it was");
        Check(LoadText("{" + Bounds(2100, 100, 1200, 800, 2) + "}") is { MainLeft: 0, MainTop: 0, MainWidth: 0, MainHeight: 0, MainWindowState: FormWindowState.Maximized, PDecimalPlaces: 6 },
            "a window saved on a monitor since disconnected opens where a first start opens it, keeping its state and the other preferences");
        Check(LoadText("{" + Bounds(2100, 100, 1200, 800) + "}", withRightMonitor) is { MainLeft: 2100, MainTop: 100, MainWidth: 1200, MainHeight: 800 },
            "a window saved on a second monitor opens there while it is attached");
        Check(LoadText("{\"mainTop\": 100, \"mainLeft\": 100}") is { MainTop: 0, MainLeft: 0, MainWidth: 0, MainHeight: 0 }, "a window position saved without a size is not used");

        // Recent files: the latest mention of each fully qualified file still there, the most recent last, no more than the File menu lists
        string deleted = Inside(Path.Combine(folder, "deleted.xlsx"));
        string mixed = string.Join(", ", Json(files[0]), Json(deleted), Json("relative.xlsx"), "\"\"", "\"  \"", "null", "42", "[\"nested\"]",
            Json(files[1]), Json(files[0]), Json(files[2]));
        Check(LoadText("{\"recentFileList\": [" + mixed + "]}").RecentFileList.SequenceEqual(new[] { files[1], files[0], files[2] }),
            "recent files keep the latest mention of each fully qualified file still there, the most recent last");
        Check(LoadText("{\"recentFileList\": [" + string.Join(", ", files.Select(Json)) + "]}").RecentFileList.SequenceEqual(files.Skip(2)),
            $"no more than the {Settings.MAX_RECENT_FILES} most recent files are kept");
        Check(loads > 100 && writes == 0, $"none of the {loads} loads of good, damaged and partial files wrote to the profile");

        // The start-up's own load: the profile's preferences replace the defaults only when it runs, on this computer's screens, without a write
        Rectangle work = Screen.PrimaryScreen!.WorkingArea;
        Rectangle window = new(work.Left + work.Width / 8, work.Top + work.Height / 8, work.Width / 2, work.Height / 2);
        File.WriteAllText(profileSettings, "{" + $"\"mainLeft\": {window.Left}, \"mainTop\": {window.Top}, \"mainWidth\": {window.Width}, \"mainHeight\": {window.Height}, " +
            "\"pDecimalPlaces\": 6, \"displayDecimalPlaces\": 9, \"defaultConfidenceInterval\": 0.9, \"selectGroupsByIdentifier\": true, " +
            "\"markers\": " + Json(CustomMarkerSettings) + ", \"titleFont\": \"Georgia;1;20pt\", \"labelFont\": \"Georgia;0;12pt\", \"recentFileList\": [" + Json(files[3]) + "]}");
        Check(Settings.Default is { WasLoaded: false, PDecimalPlaces: 4, DisplayDecimalPlaces: 6 }, "saved preferences are not read back until the start-up loads them");
        string beforeStart = FolderSnapshot(root);
        Settings.LoadSaved();
        Check(Settings.Default is { WasLoaded: true, PDecimalPlaces: 6, DisplayDecimalPlaces: 9, DefaultConfidenceInterval: 0.9, SelectGroupsByIdentifier: true } &&
            Settings.Default.MainLeft == window.Left && Settings.Default.MainTop == window.Top && Settings.Default.MainWidth == window.Width &&
            Settings.Default.MainHeight == window.Height && Settings.Default.RecentFileList.SequenceEqual(new[] { files[3] }) && FolderSnapshot(root) == beforeStart,
            "the start-up load replaces the defaults with the profile's preferences, on this computer's screens, and writes nothing");
        ChartPreferences.InitSharedValues();
        Check(ChartPreferences.DefaultTitleFont.Equals(new FontDescriptor("Georgia", 1, 20)) && (int)ChartPreferences.MarkerTypes[9].MarkerShape == 2 &&
            ChartPreferences.MarkerTypes[9].MarkerSize == 14, "preferences the start-up loads reach the chart defaults");
        File.Delete(profileSettings);
        Settings.LoadSaved();
        Check(Settings.Default is { WasLoaded: false } && IsDefault(Settings.Default) && !File.Exists(profileSettings),
            "with no saved preferences the start-up keeps every default and creates no file");

        // Only the ordinary start-up loads preferences, before it reads one: the diagnostic modes and these checks' host keep the defaults
        DirectoryInfo source = new(AppContext.BaseDirectory);
        while (source != null && !File.Exists(Path.Combine(source.FullName, "StatsDirectUI", "UI", "Program.cs")))
            source = source.Parent;
        if (source == null)
            throw new DirectoryNotFoundException("Cannot locate the StatsDirectUI sources for the start-up check.");
        string program = File.ReadAllText(Path.Combine(source.FullName, "StatsDirectUI", "UI", "Program.cs"));
        int start = program.IndexOf("private static void StartStatsDirect(", StringComparison.Ordinal);
        int load = program.IndexOf("Settings.LoadSaved();", StringComparison.Ordinal);
        int firstRead = start < 0 ? -1 : program.IndexOf("Settings.Default.", start, StringComparison.Ordinal);
        string[] loaders = Directory.EnumerateFiles(Path.Combine(source.FullName, "StatsDirectUI"), "*.cs", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(source.FullName, file))
            .Where(file => !file.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj") &&
                File.ReadAllText(Path.Combine(source.FullName, file)).Contains("LoadSaved("))
            .Order(StringComparer.Ordinal).ToArray();
        Check(start >= 0 && load > start && load < firstRead && program.IndexOf("Settings.LoadSaved();", load + 1, StringComparison.Ordinal) < 0 &&
            loaders.SequenceEqual(new[] { Path.Combine("StatsDirectUI", "UI", "Program.cs"), Path.Combine("StatsDirectUI", "UI", "Settings.cs") }),
            "only the ordinary start-up loads saved preferences, before it reads one; the diagnostic modes keep the defaults");
    }

    // Closing notes a normal window's bounds, and otherwise the bounds it returns to; a minimised window as it was shown before it was minimised
    static void CheckMainWindowPlacement()
    {
        Settings noted = new();
        noted.NoteMainWindow(FormWindowState.Normal, new Rectangle(200, 150, 1000, 700), new Rectangle(1, 2, 3, 4), FormWindowState.Normal);
        bool notedNormal = noted is { MainLeft: 200, MainTop: 150, MainWidth: 1000, MainHeight: 700, MainWindowState: FormWindowState.Normal };
        noted.NoteMainWindow(FormWindowState.Maximized, new Rectangle(-8, -8, 1936, 1056), new Rectangle(300, 200, 900, 600), FormWindowState.Maximized);
        bool notedMaximised = noted is { MainLeft: 300, MainTop: 200, MainWidth: 900, MainHeight: 600, MainWindowState: FormWindowState.Maximized };
        Check(notedNormal && notedMaximised, "closing notes a normal window's bounds and a maximised one's normal bounds");
        // Closed while minimised, as from its taskbar button: Windows Forms keeps the bounds it returns to, and the window opens as it was shown before
        Rectangle minimised = new(-32000, -32000, 160, 28);
        noted.NoteMainWindow(FormWindowState.Minimized, minimised, new Rectangle(400, 250, 800, 500), FormWindowState.Maximized);
        bool fromMaximised = noted is { MainLeft: 400, MainTop: 250, MainWidth: 800, MainHeight: 500, MainWindowState: FormWindowState.Maximized };
        noted.NoteMainWindow(FormWindowState.Minimized, minimised, new Rectangle(500, 300, 700, 400), FormWindowState.Normal);
        bool fromNormal = noted is { MainLeft: 500, MainTop: 300, MainWidth: 700, MainHeight: 400, MainWindowState: FormWindowState.Normal };
        Check(fromMaximised && fromNormal, "closing a minimised window notes the bounds it returns to and the state it was shown in before it was minimised");
        noted.NoteMainWindow(FormWindowState.Maximized, new Rectangle(-8, -8, 1936, 1056), new Rectangle(300, 200, 900, 600), FormWindowState.Maximized);
        noted.Save(); // into the isolated profile
        Check(Settings.Load(Inside(SDConfiguration.SettingsPath), Laptop) is { MainWindowState: FormWindowState.Maximized, MainLeft: 300, MainTop: 200, MainWidth: 900, MainHeight: 600 },
            "a window closed maximised reopens maximised over the bounds it returns to");
        noted.NoteMainWindow(FormWindowState.Minimized, minimised, new Rectangle(400, 250, 800, 500), FormWindowState.Maximized);
        noted.Save(); // into the isolated profile
        Check(Settings.Load(Inside(SDConfiguration.SettingsPath), Laptop) is { MainWindowState: FormWindowState.Maximized, MainLeft: 400, MainTop: 250, MainWidth: 800, MainHeight: 500 },
            "a window maximised, minimised and closed from its taskbar button reopens maximised over the bounds it returns to");
    }
}
