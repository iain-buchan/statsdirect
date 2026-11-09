using StatsDirect.Charting;
using StatsDirect.Configuration;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace StatsDirect.UI.Properties
{
    internal sealed class Settings
    {
        /// <summary>
        /// The number of recently used files the File menu lists.
        /// </summary>
        internal const int MAX_RECENT_FILES = 7;
        private const long MAX_SETTINGS_FILE_LENGTH = 1 << 20; // hundreds of times the size of any preferences StatsDirect saves
        private const int RECENT_FILE_CHECK_MILLISECONDS = 1000;
        private const int TITLE_BAR_HEIGHT = 20; // as the main window's own check of its title bar assumes

        public static Settings Default { get; private set; } = new();
        public int MainTop { get; set; }
        public int MainLeft { get; set; }
        public int MainWidth { get; set; }
        public int MainHeight { get; set; }
        public FormWindowState MainWindowState { get; set; }

        [JsonIgnore]
        public string MenuFileName { get; set; } = "menu.xml";
        public bool MetaPlotCI { get; set; } = true;
        public int MetaPlotMethod { get; set; } = 1;
        public int PDecimalPlaces { get; set; } = 4;
        public double MetaCC { get; set; } = -9;
        public bool MetaExact { get; set; } = true;
        public bool DelayContinuityCorrection { get; set; } = false;
        public bool CanDefaultConfidenceInterval { get; set; } = true;
        public double DefaultConfidenceInterval { get; set; } = 0.95;
        public IReadOnlyList<string> RecentFileList { get; set; }
        public bool SelectGroupsByIdentifier { get; set; } = false;

        [JsonIgnore]
        public string DataDirectory => "Data";

        [JsonIgnore]
        public string DefaultRecentlyUsedFile => "test.xlsx";
        public bool ShouldKeepData { get; set; } = false;
        public int DisplayDecimalPlaces { get; set; } = 6;
        public bool ShouldUseColour { get; set; } = true;
        public string DefaultWorkbookFont { get; set; } = "Calibri;0;11";

        [JsonIgnore]
        public string TemplateDirectory => "Template";

        [JsonIgnore]
        public string OperationsDirectory => "Operations";

        [JsonIgnore]
        public string UserOperationsDirectory => "UserOperations";
        public int CalculatorTop { get; set; } = 0;
        public int CalculatorLeft { get; set; } = 0;
        public int CalculatorWidth { get; set; } = 0;
        public int CalculatorHeight { get; set; } = 0;
        public bool CalculatorMaximized { get; set; } = false;
        public string Markers { get; set; } = string.Empty;
        public string TitleFont { get; set; } = string.Empty;
        public string LabelFont { get; set; } = string.Empty;
        public bool BoxAxes { get; set; } = false;
        public bool BlackAndWhite { get; set; } = false;
        public bool RequestScaleLimits { get; set; } = false;
        public bool UseScientificNotationForSmallPValues { get; set; } = false;

        [JsonIgnore]
        public bool WasLoaded { get; set;} = false;

        public void Save()
        {
            JsonSerializerOptions options = new()
            {
                DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            Directory.CreateDirectory(SDConfiguration.MyStatsDirectFolder); // A profile without the folder yet, as on a test runner, gets it here rather than failing.
            using (FileStream settingsStream = File.Create(SDConfiguration.SettingsPath))
            {
                JsonSerializer.Serialize(settingsStream, this, options);
            }
        }

        /// <summary>
        /// Replaces Default with the preferences the last session saved. The program calls this once, as it starts and before anything
        /// reads a preference; nothing else reads the file, so the diagnostic modes and the tests see only the defaults.
        /// </summary>
        internal static void LoadSaved()
        {
            try
            {
                Default = Load(SDConfiguration.SettingsPath, Screen.AllScreens.Select(screen => screen.WorkingArea).ToList());
            }
            catch (Exception)
            {
                // Preferences that cannot be read never stop StatsDirect starting: it starts with the defaults, as it always has
            }
        }

        /// <summary>
        /// The preferences saved in a file, read without writing anything. Each keeps its default unless the file holds a value of its
        /// type that StatsDirect can use, so a missing, unreadable or damaged file gives the defaults, a file from an older version gives
        /// what it holds, and a value that cannot be used costs only itself. The main window's bounds are kept only if the window can be
        /// seen and moved on the working areas of the screens there are now, and recent files only if they are still there.
        /// </summary>
        internal static Settings Load(string path, IReadOnlyList<Rectangle> workingAreas)
        {
            Settings settings = new();
            JsonDocument document;
            try
            {
                using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                if (stream.Length > MAX_SETTINGS_FILE_LENGTH)
                    return settings;
                document = JsonDocument.Parse(stream, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException or SecurityException)
            {
                return settings;
            }
            using (document)
            {
                if (JsonValueKind.Object == document.RootElement.ValueKind)
                {
                    settings.Read(document.RootElement, workingAreas);
                    settings.WasLoaded = true;
                }
            }
            return settings;
        }

        private void Read(JsonElement saved, IReadOnlyList<Rectangle> workingAreas)
        {
            // The main window where it was, if it can still be reached there; otherwise it opens where a first start opens it
            Rectangle main = new(ReadInteger(saved, nameof(MainLeft), MainLeft), ReadInteger(saved, nameof(MainTop), MainTop),
                ReadInteger(saved, nameof(MainWidth), MainWidth), ReadInteger(saved, nameof(MainHeight), MainHeight));
            if (IsUsableWindowRectangle(main, workingAreas))
            {
                MainTop = main.Top;
                MainLeft = main.Left;
                MainWidth = main.Width;
                MainHeight = main.Height;
            }
            int windowState = ReadInteger(saved, nameof(MainWindowState), (int)MainWindowState);
            if (Enum.IsDefined(typeof(FormWindowState), windowState))
                MainWindowState = (FormWindowState)windowState;

            // The analysis and meta-analysis options, within the choices their dialogs offer
            MetaPlotCI = ReadBoolean(saved, nameof(MetaPlotCI), MetaPlotCI);
            MetaPlotMethod = ReadInteger(saved, nameof(MetaPlotMethod), MetaPlotMethod, 1, 7); // the seven vertical axes of the bias plot
            PDecimalPlaces = ReadInteger(saved, nameof(PDecimalPlaces), PDecimalPlaces, 3, 7);
            MetaCC = ReadNumber(saved, nameof(MetaCC), MetaCC, cc => -9.0 == cc || cc > 0.0 && cc < 1.0); // the treatment arm correction, or a constant
            MetaExact = ReadBoolean(saved, nameof(MetaExact), MetaExact);
            DelayContinuityCorrection = ReadBoolean(saved, nameof(DelayContinuityCorrection), DelayContinuityCorrection);
            CanDefaultConfidenceInterval = ReadBoolean(saved, nameof(CanDefaultConfidenceInterval), CanDefaultConfidenceInterval);
            DefaultConfidenceInterval = ReadNumber(saved, nameof(DefaultConfidenceInterval), DefaultConfidenceInterval, level => level > 0.0 && level < 1.0);
            RecentFileList = RecentFiles(saved) ?? RecentFileList;
            SelectGroupsByIdentifier = ReadBoolean(saved, nameof(SelectGroupsByIdentifier), SelectGroupsByIdentifier);
            ShouldKeepData = ReadBoolean(saved, nameof(ShouldKeepData), ShouldKeepData);
            DisplayDecimalPlaces = ReadInteger(saved, nameof(DisplayDecimalPlaces), DisplayDecimalPlaces, 2, 12);
            ShouldUseColour = ReadBoolean(saved, nameof(ShouldUseColour), ShouldUseColour);
            UseScientificNotationForSmallPValues = ReadBoolean(saved, nameof(UseScientificNotationForSmallPValues), UseScientificNotationForSmallPValues);

            // The worksheet font and the chart options, in the forms their readers take
            DefaultWorkbookFont = ReadText(saved, nameof(DefaultWorkbookFont), DefaultWorkbookFont, IsUsableWorkbookFont);
            Markers = ReadText(saved, nameof(Markers), Markers, markers => ChartPreferences.TryParseMarkerTypes(markers, out _));
            TitleFont = ReadText(saved, nameof(TitleFont), TitleFont, font => FontDescriptor.TryParse(font, out _));
            LabelFont = ReadText(saved, nameof(LabelFont), LabelFont, font => FontDescriptor.TryParse(font, out _));
            BoxAxes = ReadBoolean(saved, nameof(BoxAxes), BoxAxes);
            BlackAndWhite = ReadBoolean(saved, nameof(BlackAndWhite), BlackAndWhite);
            RequestScaleLimits = ReadBoolean(saved, nameof(RequestScaleLimits), RequestScaleLimits);

            // Kept as they were saved, although nothing reads them now
            CalculatorTop = ReadInteger(saved, nameof(CalculatorTop), CalculatorTop);
            CalculatorLeft = ReadInteger(saved, nameof(CalculatorLeft), CalculatorLeft);
            CalculatorWidth = ReadInteger(saved, nameof(CalculatorWidth), CalculatorWidth);
            CalculatorHeight = ReadInteger(saved, nameof(CalculatorHeight), CalculatorHeight);
            CalculatorMaximized = ReadBoolean(saved, nameof(CalculatorMaximized), CalculatorMaximized);
        }

        /// <summary>
        /// Notes the main window as StatsDirect closes, so that it opens next time where it was, on the same screen, and maximised if it
        /// was: its bounds when it is normal, and otherwise the bounds it returns to (RestoreBounds), which Windows Forms keeps for a
        /// minimised window as for a maximised one. A window closed while minimised, as from its taskbar button, opens as it was shown
        /// before it was minimised, maximised or normal.
        /// </summary>
        internal void NoteMainWindow(FormWindowState state, Rectangle bounds, Rectangle restoreBounds, FormWindowState stateBeforeMinimising)
        {
            Rectangle normalBounds = FormWindowState.Normal == state ? bounds : restoreBounds;
            MainTop = normalBounds.Top;
            MainLeft = normalBounds.Left;
            MainWidth = normalBounds.Width;
            MainHeight = normalBounds.Height;
            MainWindowState = FormWindowState.Minimized != state ? state
                : FormWindowState.Maximized == stateBeforeMinimising ? FormWindowState.Maximized : FormWindowState.Normal;
        }

        /// <summary>
        /// True if a window with these bounds can be seen and moved on screens with these working areas: it has a size, at least half of it
        /// is on them, and enough of its title bar is on one to drag it by, as the main window's own check asks. A monitor since
        /// disconnected, or a smaller remote desktop, can leave a saved window out of reach.
        /// </summary>
        internal static bool IsUsableWindowRectangle(Rectangle window, IReadOnlyList<Rectangle> workingAreas)
        {
            if (window.Width <= 0 || window.Height <= 0 || (long)window.X + window.Width > int.MaxValue || (long)window.Y + window.Height > int.MaxValue)
                return false;
            long area = (long)window.Width * window.Height;
            long visible = 0;
            bool titleBarIsVisible = false;
            Rectangle titleBar = new(window.X, window.Y, window.Width, Math.Min(TITLE_BAR_HEIGHT, window.Height));
            foreach (Rectangle workingArea in workingAreas)
            {
                Rectangle onScreen = Rectangle.Intersect(window, workingArea);
                visible += (long)onScreen.Width * onScreen.Height;
                Rectangle visibleTitleBar = Rectangle.Intersect(titleBar, workingArea);
                titleBarIsVisible |= visibleTitleBar.Height > 10 && visibleTitleBar.Width > 40;
            }
            return titleBarIsVisible && 2 * Math.Min(visible, area) >= area;
        }

        /// <summary>
        /// The saved recent files that are still there: the latest mention of each fully qualified path, the most recent last as
        /// NoteRecentFile keeps them, and no more than the File menu lists. A file whose disk does not answer in time, such as one on a
        /// server that cannot be reached, is kept, so the start is not held up; opening it then, if it has gone, takes it off the list.
        /// </summary>
        private static IReadOnlyList<string> RecentFiles(JsonElement saved)
        {
            if (Value(saved, nameof(RecentFileList)) is not { ValueKind: JsonValueKind.Array } list)
                return null;
            JsonElement[] entries = list.EnumerateArray().ToArray();
            List<string> latestFirst = new();
            HashSet<string> seen = new(StringComparer.Ordinal); // the comparison NoteRecentFile uses
            for (int i = entries.Length - 1; i >= 0 && latestFirst.Count < MAX_RECENT_FILES; i--)
                if (JsonValueKind.String == entries[i].ValueKind && entries[i].GetString() is string path
                    && !string.IsNullOrWhiteSpace(path) && Path.IsPathFullyQualified(path) && seen.Add(path))
                    latestFirst.Add(path);
            Task<bool>[] exists = latestFirst.Select(path => Task.Run(() => File.Exists(path))).ToArray();
            Task.WaitAll(exists, RECENT_FILE_CHECK_MILLISECONDS);
            List<string> recentFiles = latestFirst.Where((_, i) => !exists[i].IsCompleted || exists[i].Result).ToList();
            recentFiles.Reverse();
            return recentFiles;
        }

        /// <summary>
        /// True if a worksheet can take the font: family;style;size as frmSpreadsheetGear reads it (Utilities.FontFromSaveString), with a
        /// style of the four FontStyle flags and a size from 1 to 409 points, the sizes Excel allows in the workbooks StatsDirect saves.
        /// </summary>
        private static bool IsUsableWorkbookFont(string font)
        {
            string[] parts = font.Split(';');
            return 3 == parts.Length && !string.IsNullOrWhiteSpace(parts[0])
                && int.TryParse(parts[1], out int style) && style >= 0 && style <= 15
                && float.TryParse(parts[2], out float size) && size >= 1f && size <= 409f;
        }

        // A saved value by the name Save gives the property, if the file has it
        private static JsonElement? Value(JsonElement saved, string propertyName)
            => saved.TryGetProperty(JsonNamingPolicy.CamelCase.ConvertName(propertyName), out JsonElement value) ? value : null;

        private static bool ReadBoolean(JsonElement saved, string propertyName, bool defaultValue)
            => Value(saved, propertyName) is { ValueKind: JsonValueKind.True or JsonValueKind.False } value ? value.GetBoolean() : defaultValue;

        private static int ReadInteger(JsonElement saved, string propertyName, int defaultValue, int minimum = int.MinValue, int maximum = int.MaxValue)
            => Value(saved, propertyName) is { ValueKind: JsonValueKind.Number } value && value.TryGetInt32(out int number)
                && number >= minimum && number <= maximum ? number : defaultValue;

        private static double ReadNumber(JsonElement saved, string propertyName, double defaultValue, Func<double, bool> isUsable)
            => Value(saved, propertyName) is { ValueKind: JsonValueKind.Number } value && value.TryGetDouble(out double number)
                && double.IsFinite(number) && isUsable(number) ? number : defaultValue;

        private static string ReadText(JsonElement saved, string propertyName, string defaultValue, Func<string, bool> isUsable)
            => Value(saved, propertyName) is { ValueKind: JsonValueKind.String } value && value.GetString() is string text
                && isUsable(text) ? text : defaultValue;
    }
}
