using StatsDirect.Charting;
using StatsDirect.UI.Properties;
using System;
using System.Text;

namespace StatsDirect.UI
{
    /// <summary>
    /// The interface between ChartPreferences and the preferences system and, if none, the point of truth for loading defaults.
    /// </summary>
    /// <remarks>This makes no attempt to cache reads; it is up to the caller to retain ChartPreferences for efficiency if desired.</remarks>
    public class ChartPreferencesFactory
    {
        public static ChartPreferences GetChartPreferences()
        {
            Settings settings = Settings.Default;

            ChartPreferences chartPreferences = new()
            {
                BoxAxes = settings.BoxAxes,
                BlackAndWhite = settings.BlackAndWhite,
                MarkerTypes = InitMarkerTypes(settings.Markers),

                //  Default fonts, in case there are no preferences
                AxisLabelFont = new FontDescriptor("Calibri", 0, 15),
                AxisTitleFont = new FontDescriptor("Calibri", 1, 15),
                LabelFont = new FontDescriptor("Calibri", 0, 15),
                LegendFont = new FontDescriptor("Calibri", 0, 15),
                TitleFont = new FontDescriptor("Calibri", 1, 22)
            };

            // Update fonts if there are any saved. TODO: This removes all the nuance from the above.
            if (FontDescriptor.TryParse(settings.TitleFont, out FontDescriptor titleFontDescriptor))
            {
                chartPreferences.TitleFont = titleFontDescriptor;
            }
            if (FontDescriptor.TryParse(settings.LabelFont, out FontDescriptor labelFontDescriptor))
            {
                chartPreferences.AxisLabelFont = labelFontDescriptor;
                chartPreferences.AxisTitleFont = labelFontDescriptor;
                chartPreferences.LabelFont = labelFontDescriptor;
                chartPreferences.LegendFont = labelFontDescriptor;
            }
            return chartPreferences;
        }

        ///  <summary>
        ///  Initialise the marker types from persistent storage or (if none) from defaults
        ///  </summary>
        ///  <remarks></remarks>
        private static MarkerType[] InitMarkerTypes(string savedSettings)
        {
            MarkerType[] markerTypes = FirstMarkerTypes();

            // If there aren't any, load our defaults
            if (!string.IsNullOrWhiteSpace(savedSettings))
            {
                // Reconstitute a maximum of n - 1 marker strings as the last one is fixed.  If we can't, retain our defaults.
                string[] markerStrings = savedSettings.Split('|');
                for (int i = 0; i < Math.Min(markerStrings.Length, markerTypes.Length - 1); i++)
                    if (MarkerType.TryParse(markerStrings[i], out MarkerType markerType))
                        markerTypes[i] = markerType;
            }
            return markerTypes;
        }

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

        private static MarkerType[] FirstMarkerTypes() =>
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
                MkMarkerType(MarkerShape.Square, ColorDescriptor.FromArgb(128, 100, 162), DashStyleDescriptor.Dash),
                // fixed style
                FixedMarkerType()
            ];

        private static MarkerType FixedMarkerType()
            => MkMarkerType(MarkerShape.Circle, ColorDescriptor.Black, DashStyleDescriptor.Dash);

        public static void Save(ChartPreferences chartPreferences)
        {
            Settings settings = Settings.Default;
            settings.BlackAndWhite = chartPreferences.BlackAndWhite;
            settings.BoxAxes = chartPreferences.BoxAxes;
            settings.LabelFont = chartPreferences.LabelFont.ToString();
            settings.TitleFont = chartPreferences.TitleFont.ToString();

            StringBuilder savedMarkerTypes = new();
            for (int i = 0; i < 10; i++)
            {
                if (i > 0)
                    savedMarkerTypes.Append('|');
                savedMarkerTypes.Append(chartPreferences.MarkerTypes[i]);
            }
            settings.Markers = savedMarkerTypes.ToString();

            settings.Save();
        }
    }
}
