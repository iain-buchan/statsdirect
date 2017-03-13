using StatsDirect.UI.Properties;
using System;
using System.Drawing;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using StatsDirect.Utilities;

namespace StatsDirect.Charting
{
    static class ChartPreferences
    {
        const float PIXELS_PER_INCH = 96.0f;
        const float POINTS_PER_INCH = 72.0f;
        const float PIXELS_PER_POINT = PIXELS_PER_INCH / POINTS_PER_INCH;

        public static bool AreSharedValuesInitialised { get; private set; }

        private static string defaultAxisLabelFont;
        private static string defaultAxisTitleFont;
        private static string defaultTitleFont;
        private static string defaultLegendFont;
        private static string defaultLabelFont;

        private static bool defaultBoxAxes;

        private static bool defaultAllBlack;

        /// <summary>
        /// Default marker types; shared between renderers.
        /// </summary>
        private static MarkerType[] sharedMarkerTypes;
        // TODO: Marker stacks are an abomination for histograms and should be removed forthwith.
        private static Stack<MarkerType[]> markerTypeStack;

        public static bool DefaultRequestScaleLimits
        {
            get { return false; }
        }

        public static bool DefaultBoxAxes
        {
            get
            {
                if (!AreSharedValuesInitialised)
                    InitSharedValues();
                return defaultBoxAxes;
            }
            set
            {
                defaultBoxAxes = value;
            }
        }

        public static bool DefaultAllBlack
        {
            get
            {
                if (!AreSharedValuesInitialised)
                    InitSharedValues();
                return defaultAllBlack;
            }
            set
            {
                defaultAllBlack = value;
            }
        }

        public static string DefaultAxisLabelFont
        {
            get
            {
                if (!AreSharedValuesInitialised)
                    InitSharedValues();
                return defaultAxisLabelFont;
            }
            set
            {
                defaultAxisLabelFont = value;
            }
        }

        public static string DefaultSeriesLabelFont
        {
            get { return DefaultAxisLabelFont; }
        }

        public static string DefaultAxisTitleFont
        {
            get
            {
                if (!AreSharedValuesInitialised)
                    InitSharedValues();
                return defaultAxisTitleFont;
            }
            set
            {
                defaultAxisTitleFont = value;
            }
        }

        public static string DefaultLabelFont
        {
            get
            {
                if (!AreSharedValuesInitialised)
                    InitSharedValues();
                return defaultLabelFont;
            }
            set
            {
                defaultLabelFont = value;
            }
        }

        public static string DefaultLegendFont
        {
            get
            {
                if (!AreSharedValuesInitialised)
                    InitSharedValues();
                return defaultLegendFont;
            }
            set
            {
                defaultLegendFont = value;
            }
        }

        public static string DefaultTitleFont
        {
            get
            {
                if (!AreSharedValuesInitialised)
                    InitSharedValues();
                return defaultTitleFont;
            }
            set
            {
                defaultTitleFont = value;
            }
        }

        public static MarkerType[] MarkerTypes
        {
            get
            {
                if (!AreSharedValuesInitialised)
                    InitSharedValues();
                return sharedMarkerTypes;
            }
        }

        public static void InitSharedValues()
        {
            AreSharedValuesInitialised = true; //  Set early to prevent recursively trying to initialise properties when saving them
            InitMarkerTypes();
            InitFonts();
            InitFlags();
        }

        internal static void InitFirstFonts()
        {
            //  Default fonts, in case there are no preferences
            DefaultAxisLabelFont = "Calibri;0;15";
            DefaultAxisTitleFont = "Calibri;1;15";
            DefaultLabelFont = "Calibri;0;15";
            DefaultLegendFont = "Calibri;0;15";
            DefaultTitleFont = "Calibri;1;22";
            SaveFonts();
        }

        ///  <summary>
        ///  Initialise the marker types from persistent storage or (if none) from defaults
        ///  </summary>
        ///  <remarks></remarks>
        private static void InitMarkerTypes()
        {
            sharedMarkerTypes = new MarkerType[11];
            for (int i = sharedMarkerTypes.GetLowerBound(0); i <= sharedMarkerTypes.GetUpperBound(0); i++)
                sharedMarkerTypes[i] = new MarkerType();

            string savedSettings = Settings.Default.Markers;

            if (savedSettings == null || savedSettings.Length < 10)
            {
                InitFirstMarkerTypes();
            }
            else
            {
                string[] markerStrings = savedSettings.Split('|');
                for (int i = 0; i <= 9; i++)
                {
                    string[] parameterStrings = markerStrings[i].Split(';');
                    //  Shape
                    MarkerShape shape = ((MarkerShape)(int.Parse(parameterStrings[0])));
                    //  Colour
                    string[] colourValues = parameterStrings[1].Split(',');
                    Color col = Color.FromArgb(255, int.Parse(colourValues[0]), int.Parse(colourValues[1]), int.Parse(colourValues[2]));
                    //  Width
                    float width = float.Parse(parameterStrings[2]);
                    //  Style
                    System.Drawing.Drawing2D.DashStyle style = ((System.Drawing.Drawing2D.DashStyle)(int.Parse(parameterStrings[3])));
                    //  Filled (1 = yes, missing or 0 = no)
                    bool isFilled = false;
                    if (parameterStrings.Length > 4)
                        isFilled = "1".Equals(parameterStrings[4]);
                    //  Marker size
                    int markerSize = 0;
                    if (parameterStrings.Length > 5)
                        int.TryParse(parameterStrings[5], out markerSize);
                    if (markerSize <= 0)
                        markerSize = 6;
                    sharedMarkerTypes[i].MarkerColor = col;
                    sharedMarkerTypes[i].LineColor = col;
                    sharedMarkerTypes[i].IsMarkerFilled = isFilled;
                    sharedMarkerTypes[i].MarkerSize = markerSize;
                    sharedMarkerTypes[i].MarkerShape = shape;
                    sharedMarkerTypes[i].LineDashStyle = style;
                    sharedMarkerTypes[i].Width = width;
                }

                // fixed style
                sharedMarkerTypes[10].MarkerShape = MarkerShape.Circle;
                sharedMarkerTypes[10].MarkerColor = Color.Black;
                sharedMarkerTypes[10].LineColor = Color.Black;
                sharedMarkerTypes[10].Width = 1;
                sharedMarkerTypes[10].LineDashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                sharedMarkerTypes[10].IsMarkerFilled = false;
                sharedMarkerTypes[10].MarkerSize = 6;
            }
        }

        public static void SaveFlags()
        {
            Settings.Default.BlackAndWhite = defaultAllBlack;
            Settings.Default.BoxAxes = defaultBoxAxes;

            SaveSettings(Settings.Default);
        }

        public static void SaveFonts()
        {
            Settings.Default.LabelFont = DefaultLabelFont;
            Settings.Default.TitleFont = DefaultTitleFont;

            SaveSettings(Settings.Default);
        }

        public static void SaveMarkerTypes()
        {
            StringBuilder savedSettings = new StringBuilder();
            for (int i = 0; i <= 9; i++)
            {
                if (i > 0)
                {
                    savedSettings.Append("|");
                }
                //  Shape
                savedSettings.Append(Convert.ToInt32(sharedMarkerTypes[i].MarkerShape).ToString(CultureInfo.InvariantCulture));
                savedSettings.Append(";");

                // Colour.  TODO: Line colour.
                Color col = sharedMarkerTypes[i].MarkerColor;
                savedSettings.Append(col.R.ToString(CultureInfo.InvariantCulture));
                savedSettings.Append(",");
                savedSettings.Append(col.G.ToString(CultureInfo.InvariantCulture));
                savedSettings.Append(",");
                savedSettings.Append(col.B.ToString(CultureInfo.InvariantCulture));
                savedSettings.Append(";");
                savedSettings.Append(sharedMarkerTypes[i].Width.ToString(CultureInfo.InvariantCulture));
                savedSettings.Append(";");

                //  Line style
                savedSettings.Append(Convert.ToInt32(sharedMarkerTypes[i].LineDashStyle).ToString(CultureInfo.InvariantCulture));
                savedSettings.Append(";");

                //  Filled (1/0)
                savedSettings.Append(sharedMarkerTypes[i].IsMarkerFilled ? "1" : "0");
                savedSettings.Append(";");

                //  Marker size
                savedSettings.Append(sharedMarkerTypes[i].MarkerSize.ToString(CultureInfo.InvariantCulture));
            }
            Settings.Default.Markers = savedSettings.ToString();
            SaveSettings(Settings.Default);
        }

        private static void InitFirstMarkerTypes()
        {
            sharedMarkerTypes[0].MarkerShape = MarkerShape.Circle;
            sharedMarkerTypes[0].MarkerColor = Color.FromArgb(64, 105, 156);
            sharedMarkerTypes[0].LineColor = Color.FromArgb(64, 105, 156);
            sharedMarkerTypes[0].LineDashStyle = System.Drawing.Drawing2D.DashStyle.Solid;

            sharedMarkerTypes[1].MarkerShape = MarkerShape.Square;
            sharedMarkerTypes[1].MarkerColor = Color.FromArgb(158, 65, 62);
            sharedMarkerTypes[1].LineColor = Color.FromArgb(158, 65, 62);
            sharedMarkerTypes[1].LineDashStyle = System.Drawing.Drawing2D.DashStyle.Dash;

            sharedMarkerTypes[2].MarkerShape = MarkerShape.Triangle;
            sharedMarkerTypes[2].MarkerColor = Color.FromArgb(127, 154, 72);
            sharedMarkerTypes[2].LineColor = Color.FromArgb(127, 154, 72);
            sharedMarkerTypes[2].LineDashStyle = System.Drawing.Drawing2D.DashStyle.Dot;

            sharedMarkerTypes[3].MarkerShape = MarkerShape.Plus;
            sharedMarkerTypes[3].MarkerColor = Color.FromArgb(105, 81, 133);
            sharedMarkerTypes[3].LineColor = Color.FromArgb(105, 81, 133);
            sharedMarkerTypes[3].LineDashStyle = System.Drawing.Drawing2D.DashStyle.DashDot;

            sharedMarkerTypes[4].MarkerShape = MarkerShape.Cross;
            sharedMarkerTypes[4].MarkerColor = Color.FromArgb(60, 141, 163);
            sharedMarkerTypes[4].LineColor = Color.FromArgb(60, 141, 163);
            sharedMarkerTypes[4].LineDashStyle = System.Drawing.Drawing2D.DashStyle.Solid;

            sharedMarkerTypes[5].MarkerShape = MarkerShape.CircleLine;
            sharedMarkerTypes[5].MarkerColor = Color.FromArgb(204, 123, 56);
            sharedMarkerTypes[5].LineColor = Color.FromArgb(204, 123, 56);
            sharedMarkerTypes[5].LineDashStyle = System.Drawing.Drawing2D.DashStyle.Dash;

            sharedMarkerTypes[6].MarkerShape = MarkerShape.SquareLine;
            sharedMarkerTypes[6].MarkerColor = Color.FromArgb(79, 129, 189);
            sharedMarkerTypes[6].LineColor = Color.FromArgb(79, 129, 189);
            sharedMarkerTypes[6].LineDashStyle = System.Drawing.Drawing2D.DashStyle.Dot;

            sharedMarkerTypes[7].MarkerShape = MarkerShape.SquareCross;
            sharedMarkerTypes[7].MarkerColor = Color.FromArgb(192, 80, 77);
            sharedMarkerTypes[7].LineColor = Color.FromArgb(192, 80, 77);
            sharedMarkerTypes[7].LineDashStyle = System.Drawing.Drawing2D.DashStyle.DashDot;

            sharedMarkerTypes[8].MarkerShape = MarkerShape.Circle;
            sharedMarkerTypes[8].MarkerColor = Color.FromArgb(155, 187, 89);
            sharedMarkerTypes[8].LineColor = Color.FromArgb(155, 187, 89);
            sharedMarkerTypes[8].LineDashStyle = System.Drawing.Drawing2D.DashStyle.Solid;

            sharedMarkerTypes[9].MarkerShape = MarkerShape.Square;
            sharedMarkerTypes[9].MarkerColor = Color.FromArgb(128, 100, 162);
            sharedMarkerTypes[9].LineColor = Color.FromArgb(128, 100, 162);
            sharedMarkerTypes[9].LineDashStyle = System.Drawing.Drawing2D.DashStyle.Dash;

            // fixed style
            sharedMarkerTypes[10].MarkerShape = MarkerShape.Circle;
            sharedMarkerTypes[10].MarkerColor = Color.Black;
            sharedMarkerTypes[10].LineColor = Color.Black;
            sharedMarkerTypes[10].LineDashStyle = System.Drawing.Drawing2D.DashStyle.Dash;

            foreach (MarkerType mt in sharedMarkerTypes)
            {
                mt.Width = 1;
                mt.MarkerSize = 6;
            }
            SaveMarkerTypes();
        }

        private static void InitFlags()
        {
            defaultBoxAxes = Settings.Default.BoxAxes;
            defaultAllBlack = Settings.Default.BlackAndWhite;
        }

        private static void InitFonts()
        {
            //  Title
            string savedTitleFont = Settings.Default.TitleFont;

            if (savedTitleFont == null || !CanParseSaveString(savedTitleFont))
            {
                InitFirstFonts();
            }
            else
            {
                DefaultTitleFont = savedTitleFont;
                string savedLabelFont = Settings.Default.LabelFont;
                DefaultAxisLabelFont = savedLabelFont;
                DefaultAxisTitleFont = savedLabelFont;
                DefaultLabelFont = savedLabelFont;
                DefaultLegendFont = savedLabelFont;
            }
        }

        private static void SaveSettings(Settings s)
        {
            s.Save();
        }

        public static bool CanParseSaveString(string descriptor)
        {
            try
            {
                string[] fontStrings = descriptor.Split(';');
                if (fontStrings.Length != 3)
                    return false;
                int scrapInt;
                if (!int.TryParse(fontStrings[1], out scrapInt))
                    return false;
                float scrapFloat;
                return float.TryParse(fontStrings[2], out scrapFloat);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static string SaveStringFromFont(Font f)
        {
            float emSize;
            switch (f.Unit)
            {
                case GraphicsUnit.Pixel:
                    emSize = f.Size / PIXELS_PER_POINT;
                    break;
                case GraphicsUnit.Point:
                    emSize = f.Size;
                    break;
                default:
                    throw new Exception("Cannot save font - unknown conversion from unit " + f.Unit.ToString());
            }
            return f.FontFamily.Name + ";" + (Convert.ToInt32(f.Style)) + ";" + emSize;
        }

        /// <summary>
        /// Returns a font matching the descriptor appropriate for drawing on a metafile, or null if no font can be derived from the descriptor.  #830: To prevent scaling issues, assume the metafile is drawn at 96dpi.
        /// </summary>
        public static Font FontFromSaveString(string descriptor)
        {
            if (string.IsNullOrWhiteSpace(descriptor))
                return null;
            string[] fontStrings = descriptor.Split(';');
            if (fontStrings.Length != 3)
                return null;
            string familyName = fontStrings[0];
            FontStyle style = ((FontStyle)(Parsing.Cint_Txt(fontStrings[1])));
            float emSize = float.Parse(fontStrings[2]);
            float pixelSize = emSize * PIXELS_PER_POINT;
            try
            {
                return new Font(familyName, pixelSize, style, GraphicsUnit.Pixel);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static void PushAndCloneMarkerTypes()
        {
            MarkerType[] originalMarkerTypes = sharedMarkerTypes;
            sharedMarkerTypes = new MarkerType[sharedMarkerTypes.Length];
            for (int i = 0; i < sharedMarkerTypes.Length; i++)
                sharedMarkerTypes[i] = originalMarkerTypes[i].Clone();
            if (null == markerTypeStack)
                markerTypeStack = new Stack<MarkerType[]>();
            markerTypeStack.Push(originalMarkerTypes);
        }

        public static void PopMarkerTypes()
        {
            if (null != markerTypeStack && markerTypeStack.Count > 0)
                sharedMarkerTypes = markerTypeStack.Pop();
        }
    }
}
