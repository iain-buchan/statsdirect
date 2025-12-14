using StatsDirect.Charting;
using StatsDirect.Configuration;
using StatsDirect.Templates;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Forms;

namespace StatsDirect.UI
{
    internal sealed class Settings : IChartPreferences, IPreferences
    {
        private readonly static JsonSerializerOptions options = new()
        {
            DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private static Settings defaultSettings = null;
        /// <summary>
        /// Singleton for the statically-created Setting that is either initialised or loaded.
        /// </summary>
        public static Settings Default => defaultSettings ??= CreateOrLoadSettings();

        private static Settings CreateOrLoadSettings()
        {
            Settings candidate = MaybeLoad();
            if (candidate is null)
                return InstallationDefaults;
            candidate.WasLoaded = true;
            return candidate;
        }

        /// <summary>
        /// Guaranteed a new copy each time, with no load of any other data on top.
        /// </summary>
        public static Settings InstallationDefaults => new();

        // Windows UI properties
        public int CalculatorTop { get; set; } = 0;
        public int CalculatorLeft { get; set; } = 0;
        public int CalculatorWidth { get; set; } = 0;
        public int CalculatorHeight { get; set; } = 0;
        public bool CalculatorMaximized { get; set; } = false;
        public string DefaultWorkbookFont { get; set; } = "Calibri;0;11";
        public int MainTop { get; set; } = 0;
        public int MainLeft { get; set; } = 0;
        public int MainWidth { get; set; } = 0;
        public int MainHeight { get; set; } = 0;
        public FormWindowState MainWindowState { get; set; }
        public IReadOnlyList<string> RecentFileList { get; set; }
        public IReadOnlyList<string> ToolsNames { get; set; } = ["Calculator", "Notepad"];
        public IReadOnlyList<string> ToolsPrograms { get; set; } = ["%STATSDIRECT%\\StatsDirect.exe -calculator", "notepad.exe"];

        // IPreferences properties
        public bool CanDefaultConfidenceInterval { get; set; } = true;
        public double DefaultConfidenceInterval { get; set; } = 0.95;
        public bool DelayContinuityCorrection { get; set; } = false;
        public int DisplayDecimalPlaces { get; set; } = 6;
        public double MetaCC { get; set; } = -9;
        public bool MetaExact { get; set; } = true;
        public bool MetaPlotCI { get; set; } = true;
        public int MetaPlotMethod { get; set; } = 1;
        public int PDecimalPlaces { get; set; } = 4;
        public bool SelectGroupsByIdentifier { get; set; } = false;
        public bool UseScientificNotationForSmallPValues { get; set; } = false;

        // IChartPreferences properties
        public FontDescriptor AxisLabelFont { get; set; } = new FontDescriptor("Calibri", 0, 15);
        public FontDescriptor AxisTitleFont { get; set; } = new FontDescriptor("Calibri", 1, 15);
        public bool BlackAndWhite { get; set; } = false;
        public bool BoxAxes { get; set; } = false;
        [JsonIgnore]
        public MarkerType FixedMarkerType { get; } = MkFixedMarkerType();
        public FontDescriptor LabelFont { get; set; } = new FontDescriptor("Calibri", 0, 15);
        public FontDescriptor LegendFont { get; set; } = new FontDescriptor("Calibri", 0, 15);
        public IReadOnlyList<MarkerType> MarkerTypes { get; set; } = FirstMarkerTypes();
        public bool RequestScaleLimits { get; set; } = false;
        public FontDescriptor TitleFont { get; set; } = new FontDescriptor("Calibri", 1, 22);

        [JsonIgnore]
        public bool WasLoaded { get; set;} = false;

        // TODO: These constants probably should not be part of Settings.
        string IPreferences.DECP_CHAR => System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
        int IPreferences.MaxRows => 64000;
        string IPreferences.Numeric_Thousands_Separator => System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberGroupSeparator;

        /// <summary>
        /// Load and return a Settings object from the saved state if one exists and is readable.  If not, return null.
        /// </summary>
        /// <returns>A Settings object created from the saved state if one exists and is readable, or null.</returns>
        private static Settings MaybeLoad()
        {
#if !WATCH_EXCEPTIONS
            try
            {
#endif
                using FileStream settingsStream = File.OpenRead(SDConfiguration.SettingsPath);
                return JsonSerializer.Deserialize<Settings>(settingsStream, options);
#if !WATCH_EXCEPTIONS
            }
            catch (Exception _)
            {
                // TODO: Log
                return null;
            }
#endif
        }

        public void Save()
        {
            using FileStream settingsStream = File.Create(SDConfiguration.SettingsPath);
            JsonSerializer.Serialize(settingsStream, this, options);
        }

        // TODO: Marker stacks are an abomination for histograms and should be removed forthwith.
        private Stack<IReadOnlyList<MarkerType>> markerTypeStack;

        void IChartPreferences.PushAndCloneMarkerTypes()
        {
            IReadOnlyList<MarkerType> originalMarkerTypes = MarkerTypes;
            var markerTypes = new MarkerType[MarkerTypes.Count];
            for (int i = 0; i < MarkerTypes.Count; i++)
                markerTypes[i] = originalMarkerTypes[i].Clone();
            MarkerTypes = markerTypes;

            markerTypeStack ??= new Stack<IReadOnlyList<MarkerType>>();
            markerTypeStack.Push(originalMarkerTypes);
        }

        void IChartPreferences.PopMarkerTypes() => markerTypeStack?.TryPop(out IReadOnlyList<MarkerType> _);

        private static MarkerType MkMarkerType(MarkerShape markerShape, ColorDescriptor colorDescriptor, DashStyleDescriptor dashStyle) =>
            new()
            {
                MarkerShape = markerShape,
                MarkerColor = colorDescriptor,
                LineColor = colorDescriptor,
                LineDashStyle = dashStyle,
                Width = 1,
                MarkerSize = 6,
                IsMarkerFilled = false,
                MarkerFillStyle = FillStyle.None
            };

        private static IReadOnlyList<MarkerType> FirstMarkerTypes() =>
            [
                MkMarkerType(MarkerShape.Circle, ColorDescriptor.FromArgb(64, 105, 156), DashStyleDescriptor.Solid),
                MkMarkerType(MarkerShape.Square, ColorDescriptor.FromArgb(158, 65, 62), DashStyleDescriptor.Dash),
                MkMarkerType(MarkerShape.Triangle, ColorDescriptor.FromArgb(127, 154, 72), DashStyleDescriptor.Dot),
                MkMarkerType(MarkerShape.Plus, ColorDescriptor.FromArgb(105, 81, 133), DashStyleDescriptor.DashDot),
                MkMarkerType(MarkerShape.Cross, ColorDescriptor.FromArgb(60, 141, 163), DashStyleDescriptor.Solid),
                MkMarkerType(MarkerShape.CircleLine, ColorDescriptor.FromArgb(204, 123, 56), DashStyleDescriptor.Dash),
                MkMarkerType(MarkerShape.SquareLine, ColorDescriptor.FromArgb(79, 129, 189), DashStyleDescriptor.Dot),
                MkMarkerType(MarkerShape.SquareCross, ColorDescriptor.FromArgb(192, 80, 77), DashStyleDescriptor.DashDot),
                MkMarkerType(MarkerShape.Circle, ColorDescriptor.FromArgb(155, 187, 89), DashStyleDescriptor.Solid),
                MkMarkerType(MarkerShape.Square, ColorDescriptor.FromArgb(128, 100, 162), DashStyleDescriptor.Dash)
            ];

        private static MarkerType MkFixedMarkerType()
            => MkMarkerType(MarkerShape.Circle, ColorDescriptor.Black, DashStyleDescriptor.Dash);
    }
}
