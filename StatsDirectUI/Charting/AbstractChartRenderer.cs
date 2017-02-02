using StatsDirect.Templates;
using StatsDirect.UI.Properties;
using StatsDirect.Utilities;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace StatsDirect.Charting
{
    public abstract class AbstractChartRenderer : IChartRenderer, IDisposable
    {
        const float PIXELS_PER_INCH = 96.0f;
        const float POINTS_PER_INCH = 72.0f;
        const float PIXELS_PER_POINT = PIXELS_PER_INCH / POINTS_PER_INCH;
        protected readonly double LOG2 = Math.Log(2.0);
        protected const int LEGEND_TOP_GAP = 70;
        protected const int LEGEND_MARKER_SIZE = 6;
        protected const int MINIMUM_LEGEND_GAP = 6;
        protected const int LOWEST_ALLOWED_LEGEND = 30;
        protected const int MAX_LABEL_LENGTH = 50;
        protected const int MINIMUM_X_WHITESPACE = 70;

        //  PUBLIC VARIABLES - users can set these
        ///  <summary>
        ///  If true, a textual representation of the chart is plotted.  If false (default) a metafile is plotted.
        ///  </summary>
        public bool IsAscii { get; set; } = false;


        //  PRIVATE VARIABLES - callers should be unable to touch anything below here
        public double DataMinX; //TODO: { get; set; }
        public double DataMinGreaterThanZeroX { get; set; }
        public double DataMaxX; //TODO: { get; set; }
        public double DataMinY; //TODO: { get; set; }
        public double DataMinGreaterThanZeroY { get; set; }
        public double DataMaxY; //TODO: { get; set; }

        protected ChartDefinition definition;

        private IStatsDirectCanvas statsDirectCanvas;

        private static string defaultAxisLabelFont;
        private static string defaultAxisTitleFont;
        private static string defaultTitleFont;
        private static string defaultLegendFont;
        private static string defaultLabelFont;

        protected static bool defaultBoxAxes;

        protected static bool defaultAllBlack;

        protected Font axisLabelFont;
        protected Font axisTitleFont;
        protected float axisLineThickness;
        protected Pen axisPen;
        protected Brush axisBrush;
        protected const double AXIS_LITTLE_TICK = 4;
        protected const double AXIS_BIG_TICK = 7;
        protected Font titleFont;
        protected Font legendFont;
        protected bool boxAxes = defaultBoxAxes;

        protected Font labelFont;


        ///  <summary>
        ///  The minimum value for the X-axis that will eventually be drawn (the neat value)
        ///  </summary>
        protected double axisXMin;
        protected double axisXMinGreaterThanZero;
        ///  <summary>
        ///  The maximum value for the X-axis that will eventually be drawn (the neat value)
        ///  </summary>
        protected double axisXMax;
        ///  <summary>
        ///  The minimum value for the Y-axis that will eventually be drawn (the neat value)
        ///  </summary>
        protected double axisYMin;
        protected double axisYMinGreaterThanZero;
        ///  <summary>The maximum value for the Y-axis that will eventually be drawn (the neat value)</summary>
        protected double axisYMax;
        /// <summary>
        /// The X-position in canvas co-ordinates of the left-hand end of the chart's X-axis
        /// </summary>
        protected double xAxisCanvas;
        /// <summary>
        /// The length in canvas co-ordinates of the chart's X-axis
        /// </summary>
        protected double xExtCanvas;
        /// <summary>
        /// The Y-position in canvas co-ordinates of the bottom of the chart's Y-axis
        /// </summary>
        protected double yAxisCanvas;
        protected double yExtCanvas;

        protected double divx;
        protected double offx;
        protected double divy;
        protected double offy;

        protected const int DEFAULT_METAFILE_HEIGHT = 800;
        protected const int DEFAULT_METAFILE_WIDTH = 1132;
        protected const double DEFAULT_X_GAP = 80;
        protected const double DEFAULT_Y_GAP = 80;
        protected int imageHeight = DEFAULT_METAFILE_HEIGHT;
        protected int imageWidth = DEFAULT_METAFILE_WIDTH;
        protected const int LABEL_TO_AXIS_LABEL_GAP = 20;

        protected string[] shTx;

        private static bool AreSharedValuesInitialised;

        protected const int ASCII_Ytxt = 3;
        protected const int ASCII_XTxt = 15;

        ///  <summary>
        ///  A few methods take a colour, not a pen.  This caches the most recent pen used by those methods, so that it can be re-used rather than regenerated each time.
        ///  </summary>
        protected Pen mostRecentPen;
        /// <summary>
        /// Default marker types; shared between renderers.
        /// </summary>
        protected static MarkerType[] SharedMarkerTypes;

        protected AbstractChartRenderer(ChartDefinition Definition)
        {
            DataMinX = double.MaxValue;
            DataMaxX = -double.MaxValue;
            DataMinY = double.MaxValue;
            DataMaxY = -double.MaxValue;

            definition = Definition;
            if (Definition == null)
                return;
            DataMinX = Definition.DataMinX;
            DataMinGreaterThanZeroX = Definition.DataMinGreaterThanZeroX;
            DataMaxX = Definition.DataMaxX;
            DataMinY = Definition.DataMinY;
            DataMinGreaterThanZeroY = Definition.DataMinGreaterThanZeroY;
            DataMaxY = Definition.DataMaxY;
        }

        public static bool DefaultRequestScaleLimits
        {
            get { return false; }
        }

        public static bool DefaultBoxAxes
        {
            get
            {
                if (!(AreSharedValuesInitialised))
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
                if (!(AreSharedValuesInitialised))
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
                if (!(AreSharedValuesInitialised))
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
                if (!(AreSharedValuesInitialised))
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
                if (!(AreSharedValuesInitialised))
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
                if (!(AreSharedValuesInitialised))
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
                if (!(AreSharedValuesInitialised))
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
                if (!(AreSharedValuesInitialised))
                    InitSharedValues();
                return SharedMarkerTypes;
            }
        }

        private static void InitSharedValues()
        {
            AreSharedValuesInitialised = true; //  Set early to prevent recursively trying to initialise properties when saving them
            InitMarkerTypes();
            InitFonts();
            InitFlags();
        }

        ///  <summary>
        ///  Sort the data for each series into ascending order.
        ///  Note and return the global minimum and maximum values.
        ///  </summary>
        /// <param name="seriesToUse"></param>
        /// <param name="min">Filled in with the global minimum value</param>
        ///  <param name="max">Filled in with the global maximum value</param>
        ///  <remarks>STYLE: Wouldn't this be better as a function returning some kind of data structure?</remarks>
        protected void GetMinMaxSort(List<Series> seriesToUse, out double min, out double max)
        {
            min = double.MaxValue;
            max = double.MinValue;
            foreach (DoubleSeries s in seriesToUse)
            {
                Array.Sort(s.Data);
                if (s.Data[0] < min)
                {
                    min = s.Data[0];
                }
                if (s.Data[s.Data.Length - 1] > max)
                {
                    max = s.Data[s.Data.Length - 1];
                }
            }
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
        private static void InitFirstFonts()
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
            SharedMarkerTypes = new MarkerType[11];
            for (int i = SharedMarkerTypes.GetLowerBound(0); i <= SharedMarkerTypes.GetUpperBound(0); i++)
            {
                SharedMarkerTypes[i] = new MarkerType();
            }

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
                    SharedMarkerTypes[i].MarkerColor = col;
                    SharedMarkerTypes[i].LineColor = col;
                    SharedMarkerTypes[i].IsMarkerFilled = isFilled;
                    SharedMarkerTypes[i].MarkerSize = markerSize;
                    SharedMarkerTypes[i].MarkerShape = shape;
                    SharedMarkerTypes[i].LineDashStyle = style;
                    SharedMarkerTypes[i].Width = width;
                }

                // fixed style
                SharedMarkerTypes[10].MarkerShape = MarkerShape.Circle;
                SharedMarkerTypes[10].MarkerColor = Color.Black;
                SharedMarkerTypes[10].LineColor = Color.Black;
                SharedMarkerTypes[10].Width = 1;
                SharedMarkerTypes[10].LineDashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                SharedMarkerTypes[10].IsMarkerFilled = false;
                SharedMarkerTypes[10].MarkerSize = 6;
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
                savedSettings.Append(Convert.ToInt32(SharedMarkerTypes[i].MarkerShape).ToString(CultureInfo.InvariantCulture));
                savedSettings.Append(";");

                // Colour.  TODO: Line colour.
                Color col = SharedMarkerTypes[i].MarkerColor;
                savedSettings.Append(col.R.ToString(CultureInfo.InvariantCulture));
                savedSettings.Append(",");
                savedSettings.Append(col.G.ToString(CultureInfo.InvariantCulture));
                savedSettings.Append(",");
                savedSettings.Append(col.B.ToString(CultureInfo.InvariantCulture));
                savedSettings.Append(";");
                savedSettings.Append(SharedMarkerTypes[i].Width.ToString(CultureInfo.InvariantCulture));
                savedSettings.Append(";");

                //  Line style
                savedSettings.Append(Convert.ToInt32(SharedMarkerTypes[i].LineDashStyle).ToString(CultureInfo.InvariantCulture));
                savedSettings.Append(";");

                //  Filled (1/0)
                savedSettings.Append(SharedMarkerTypes[i].IsMarkerFilled ? "1" : "0");
                savedSettings.Append(";");

                //  Marker size
                savedSettings.Append(SharedMarkerTypes[i].MarkerSize.ToString(CultureInfo.InvariantCulture));
            }
            Settings.Default.Markers = savedSettings.ToString();
            SaveSettings(Settings.Default);
        }

        private static void InitFirstMarkerTypes()
        {
            SharedMarkerTypes[0].MarkerShape = MarkerShape.Circle;
            SharedMarkerTypes[0].MarkerColor = Color.FromArgb(64, 105, 156);
            SharedMarkerTypes[0].LineColor = Color.FromArgb(64, 105, 156);
            SharedMarkerTypes[0].LineDashStyle = System.Drawing.Drawing2D.DashStyle.Solid;

            SharedMarkerTypes[1].MarkerShape = MarkerShape.Square;
            SharedMarkerTypes[1].MarkerColor = Color.FromArgb(158, 65, 62);
            SharedMarkerTypes[1].LineColor = Color.FromArgb(158, 65, 62);
            SharedMarkerTypes[1].LineDashStyle = System.Drawing.Drawing2D.DashStyle.Dash;

            SharedMarkerTypes[2].MarkerShape = MarkerShape.Triangle;
            SharedMarkerTypes[2].MarkerColor = Color.FromArgb(127, 154, 72);
            SharedMarkerTypes[2].LineColor = Color.FromArgb(127, 154, 72);
            SharedMarkerTypes[2].LineDashStyle = System.Drawing.Drawing2D.DashStyle.Dot;

            SharedMarkerTypes[3].MarkerShape = MarkerShape.Plus;
            SharedMarkerTypes[3].MarkerColor = Color.FromArgb(105, 81, 133);
            SharedMarkerTypes[3].LineColor = Color.FromArgb(105, 81, 133);
            SharedMarkerTypes[3].LineDashStyle = System.Drawing.Drawing2D.DashStyle.DashDot;

            SharedMarkerTypes[4].MarkerShape = MarkerShape.Cross;
            SharedMarkerTypes[4].MarkerColor = Color.FromArgb(60, 141, 163);
            SharedMarkerTypes[4].LineColor = Color.FromArgb(60, 141, 163);
            SharedMarkerTypes[4].LineDashStyle = System.Drawing.Drawing2D.DashStyle.Solid;

            SharedMarkerTypes[5].MarkerShape = MarkerShape.CircleLine;
            SharedMarkerTypes[5].MarkerColor = Color.FromArgb(204, 123, 56);
            SharedMarkerTypes[5].LineColor = Color.FromArgb(204, 123, 56);
            SharedMarkerTypes[5].LineDashStyle = System.Drawing.Drawing2D.DashStyle.Dash;

            SharedMarkerTypes[6].MarkerShape = MarkerShape.SquareLine;
            SharedMarkerTypes[6].MarkerColor = Color.FromArgb(79, 129, 189);
            SharedMarkerTypes[6].LineColor = Color.FromArgb(79, 129, 189);
            SharedMarkerTypes[6].LineDashStyle = System.Drawing.Drawing2D.DashStyle.Dot;

            SharedMarkerTypes[7].MarkerShape = MarkerShape.SquareCross;
            SharedMarkerTypes[7].MarkerColor = Color.FromArgb(192, 80, 77);
            SharedMarkerTypes[7].LineColor = Color.FromArgb(192, 80, 77);
            SharedMarkerTypes[7].LineDashStyle = System.Drawing.Drawing2D.DashStyle.DashDot;

            SharedMarkerTypes[8].MarkerShape = MarkerShape.Circle;
            SharedMarkerTypes[8].MarkerColor = Color.FromArgb(155, 187, 89);
            SharedMarkerTypes[8].LineColor = Color.FromArgb(155, 187, 89);
            SharedMarkerTypes[8].LineDashStyle = System.Drawing.Drawing2D.DashStyle.Solid;

            SharedMarkerTypes[9].MarkerShape = MarkerShape.Square;
            SharedMarkerTypes[9].MarkerColor = Color.FromArgb(128, 100, 162);
            SharedMarkerTypes[9].LineColor = Color.FromArgb(128, 100, 162);
            SharedMarkerTypes[9].LineDashStyle = System.Drawing.Drawing2D.DashStyle.Dash;

            // fixed style
            SharedMarkerTypes[10].MarkerShape = MarkerShape.Circle;
            SharedMarkerTypes[10].MarkerColor = Color.Black;
            SharedMarkerTypes[10].LineColor = Color.Black;
            SharedMarkerTypes[10].LineDashStyle = System.Drawing.Drawing2D.DashStyle.Dash;

            foreach (MarkerType mt in SharedMarkerTypes)
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

        ///  <summary>
        ///  Prepare to plot a vector chart to the specified stream.
        ///  </summary>
        ///  <remarks></remarks>
        protected void StartVectorPlot(bool shouldDefaultAxes = true)
        {
            // Initialise scaling and resources
            IsAscii = false;
            if (shouldDefaultAxes)
                DefaultAxes();
            if (!(AreSharedValuesInitialised))
                InitSharedValues();

            //  Drawing objects
            if (!ReconstituteFonts())
            {
                InitFirstFonts();
                if (!ReconstituteFonts())
                    throw new Exception("Cannot find the fonts that StatsDirect uses for charting. If Calibri is not installed on your system, you can download it from https://www.microsoft.com/typography/fonts/font.aspx?FMID=1710");
            }

            axisPen = new Pen(grAxis, 1);
            axisBrush = new SolidBrush(Color.Black);

            statsDirectCanvas = new EmfCanvas(imageWidth, imageHeight);
        }

        ///  <summary>
        ///  Set up some appropriate default axes.
        ///  </summary>
        ///  <remarks>
        ///  The X axis uses 80% of the width and is offset by a few percent to the right
        ///  The Y axis is centred and uses 75% of the height
        ///  </remarks>
        protected void DefaultAxes(double extraHeightRequiredAtBottom = 0, double extraWidthRequiredAtLeft = 0, double extraWidthRequiredAtRight = 0)
        {
            //  xaxis also needs to be reset in routines with legends
            xAxisCanvas = DEFAULT_X_GAP + extraWidthRequiredAtLeft;
            yAxisCanvas = DEFAULT_Y_GAP + extraHeightRequiredAtBottom;
            xExtCanvas = imageWidth - xAxisCanvas - extraWidthRequiredAtRight - DEFAULT_X_GAP;
            yExtCanvas = imageHeight - yAxisCanvas - DEFAULT_Y_GAP;
        }

        private bool ReconstituteFonts()
        {
            axisLabelFont = FontFromSaveString(DefaultAxisLabelFont);
            axisTitleFont = FontFromSaveString(DefaultAxisTitleFont);
            labelFont = FontFromSaveString(DefaultLabelFont);
            legendFont = FontFromSaveString(DefaultLegendFont);
            titleFont = FontFromSaveString(DefaultTitleFont);
            return null != axisLabelFont && null != axisTitleFont && null != labelFont && null != legendFont && null != titleFont;
        }

        ///  <summary>
        ///  Stop plotting a vector chart and release resources.
        ///  </summary>
        ///  <remarks></remarks>
        protected void EndVectorPlot()
        {
            //  Series are kept in case of redoing a preview.  TODO: Is this appropriate?  Isn't a new renderer used each time?

            if (null != axisPen)
            {
                axisPen.Dispose();
                axisPen = null;
            }
            if (null != axisBrush)
            {
                axisBrush.Dispose();
                axisBrush = null;
            }
            if (null != mostRecentPen)
            {
                mostRecentPen.Dispose();
                mostRecentPen = null;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns>A Stream which is live and, if read from its current point to its end, gives an Image.  Note that this detaches the Stream from the internal canvas to prevent its disposal, so only call this once per plot!</returns>
        public Stream GetImageStream()
        {
            return statsDirectCanvas.DetachAndReturnImageStream();
        }

        protected void DrawTitle(string title)
        {
            using (StringFormat txtFormat = new StringFormat())
            {
                txtFormat.Alignment = StringAlignment.Center;
                statsDirectCanvas.DrawString(title, titleFont, Brushes.Black, (xExtCanvas / 2) + xAxisCanvas, yAxisCanvas + yExtCanvas + 60 - titleFont.Height * 0.25F, txtFormat);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="title"></param>
        /// <param name="gapForAxisLabels"></param>
        /// <returns>true if the title would fit on the current canvas, false if the canvas needs to be extended</returns>
        public bool DrawXAxisTitle(string title, double gapForAxisLabels)
        {
            // If the title would not fit on the current canvas, return false
            bool titleHasText = !(string.IsNullOrEmpty(title));
            double topOfXAxisTitle = yAxisCanvas - AXIS_BIG_TICK - gapForAxisLabels - LABEL_TO_AXIS_LABEL_GAP;
            double bottomOfXAxisTitle = topOfXAxisTitle - (titleHasText ? (axisTitleFont.Height * 0.5) : 0);
            double middleOfXAxisTitle = (bottomOfXAxisTitle + topOfXAxisTitle) / 2.0;
            if (bottomOfXAxisTitle < 0)
                return false;

            if (titleHasText)
            {
                using (StringFormat txtFormat = new StringFormat())
                {
                    txtFormat.Alignment = StringAlignment.Center;
                    statsDirectCanvas.DrawString(title, axisTitleFont, Brushes.Black, (xExtCanvas / 2) + xAxisCanvas, middleOfXAxisTitle, txtFormat);
                }
            }
            return true;
        }

        public bool DrawYAxisTitle(string title, double axisLabelWidth)
        {
            // If the title would not fit on the current canvas, return false
            bool titleHasText = !(string.IsNullOrEmpty(title));
            double rightOfYAxisTitle = xAxisCanvas - AXIS_BIG_TICK - axisLabelWidth - LABEL_TO_AXIS_LABEL_GAP;
            double leftOfYAxisTitle = rightOfYAxisTitle - (titleHasText ? (axisTitleFont.Height) : 0);
            if (leftOfYAxisTitle < 0)
                return false;

            if (titleHasText)
            {
                using (StringFormat txtFormat = new StringFormat())
                {
                    txtFormat.Alignment = StringAlignment.Center;
                    txtFormat.LineAlignment = StringAlignment.Far;
                    statsDirectCanvas.DrawStringAtAngle(title, axisTitleFont, Brushes.Black, rightOfYAxisTitle, (yExtCanvas / 2.0) + yAxisCanvas, txtFormat, LabelDirection.Up);
                }
            }
            return true;
        }

        /// <summary>
        /// Draw the axes and chart title.  This must be the first drawing operation called.
        /// This is allowed to shift xAxis/xExt and yAxis/yExt around to make space.
        /// </summary>
        /// <param name="title">The title of the chart</param>
        /// <param name="x">The axis definition for the X-axis</param>
        /// <param name="y">The axis definition for the Y-axis</param>
        /// <param name="shouldBoxAxes"></param>
        /// <param name="useCalculatedScalesEvenWithDefinition"></param>
        protected AxisScales DrawAxesOrEnlargeCanvas(string title, AxisDefinition x, AxisDefinition y, bool shouldBoxAxes, bool useCalculatedScalesEvenWithDefinition)
        {
            DefaultAxes(0, 0, x.ExtraSpaceAfterAxisEnds);
            AxisScalesAndExtraSize ases = DrawAxesOrFail(title, x, y, shouldBoxAxes, useCalculatedScalesEvenWithDefinition);
            if (ases.ExtraSize.Width > 0 || ases.ExtraSize.Height > 0)
            {
                imageWidth += ases.ExtraSize.Width;
                imageHeight += ases.ExtraSize.Height;
                statsDirectCanvas.Dispose();
                statsDirectCanvas = new EmfCanvas(imageWidth, imageHeight);
                DefaultAxes(ases.ExtraSize.Height, ases.ExtraSize.Width, x.ExtraSpaceAfterAxisEnds);
                ases = DrawAxesOrFail(title, x, y, shouldBoxAxes, useCalculatedScalesEvenWithDefinition);
                if (ases.ExtraSize.Width > 0 || ases.ExtraSize.Height > 0)
                    throw new Exception("Even after trying to enlarge the canvas, I don't have enough space for the chart.");
            }
            return ases.AxisScales;
        }

        protected AxisScalesAndExtraSize DrawAxesOrFail(string title, AxisDefinition x, AxisDefinition y, bool shouldBoxAxes, bool useCalculatedScalesEvenWithDefinition)
        {
            Size extraSizeRequired = new Size();
            if (!IsAscii)
            {
                // Draw the axes
                if ((x.Mode & AxisMode.Line) == AxisMode.Line)
                    AxisDrawline(xAxisCanvas, yAxisCanvas, xAxisCanvas + xExtCanvas, yAxisCanvas);

                if ((y.Mode & AxisMode.Line) == AxisMode.Line)
                    AxisDrawline(xAxisCanvas, yAxisCanvas + yExtCanvas, xAxisCanvas, yAxisCanvas);

                if (shouldBoxAxes)
                {
                    if ((x.Mode & AxisMode.Line) == AxisMode.Line)
                        AxisDrawline(xAxisCanvas + xExtCanvas, yAxisCanvas + yExtCanvas, xAxisCanvas, yAxisCanvas + yExtCanvas);
                    if ((y.Mode & AxisMode.Line) == AxisMode.Line)
                        AxisDrawline(xAxisCanvas + xExtCanvas, yAxisCanvas + yExtCanvas, xAxisCanvas + xExtCanvas, yAxisCanvas);
                }
            }
            else
            { //  Is Ascii
                // Draw the Title
                int s = 40 - (title.Length / 2);
                WriteAsciiYX(shTx.GetUpperBound(0), s, title);
            }

            AxisScaleAndSize xAss;
            switch (x.Mode)
            {
                case AxisMode.LineOnly:
                case AxisMode.None:
                    //  Do nothing
                    xAss = new AxisScaleAndSize();
                    divx = axisXMax - axisXMin;
                    offx = -(axisXMin / divx * xExtCanvas) + xAxisCanvas;
                    break;
                case AxisMode.ReverseScale:
                    throw new ArgumentException("A reversed X scale is not currently supported");
                case AxisMode.Scale:
                case AxisMode.ScaleWithoutLabels:
                    xAss = DrawXScale((x.Mode & AxisMode.Labels) == AxisMode.Labels, x.ScaleType, useCalculatedScalesEvenWithDefinition);
                    // DrawXScale sets divx and offx
                    break;
                case AxisMode.Series:
                    if (null != x.Series)
                        xAss = DrawXSeries(x.Series.Select(s => s.Title).ToList());
                    else if (null != x.Labels)
                        xAss = DrawXSeries(x.Labels);
                    else
                        xAss = new AxisScaleAndSize();
                    if (null != x.Series)
                    {
                        divx = x.Series.Count;
                        offx = xAxisCanvas;
                    }
                    break;
                default:
                    throw new NotImplementedException("Unknown X axis scale mode");
            }

            AxisScaleAndSize yAss;
            switch (y.Mode)
            {
                case AxisMode.LineOnly:
                case AxisMode.None:
                    //  Do nothing
                    yAss = new AxisScaleAndSize();
                    divy = axisYMax - axisYMin;
                    offy = -(axisYMin / divy * yExtCanvas) + yAxisCanvas;
                    break;
                case AxisMode.ReverseScale:
                    yAss = DrawYScale(true, y.ScaleType, useCalculatedScalesEvenWithDefinition);
                    break;
                case AxisMode.Scale:
                    yAss = DrawYScale(false, y.ScaleType, useCalculatedScalesEvenWithDefinition);
                    break;
                case AxisMode.ScaleWithoutLabels:
                    throw new ArgumentException("A Y scale without labels is not currently supported");
                case AxisMode.Series:
                    if (null != y.Series)
                        yAss = DrawYSeries(y.Series.Select(s => s.Title).ToList());
                    else if (null != y.Labels)
                        yAss = DrawYSeries(y.Labels);
                    else
                        yAss = new AxisScaleAndSize();
                    if (null != y.Series)
                    {
                        divy = y.Series.Count;
                        offy = yAxisCanvas;
                    }
                    break;
                default:
                    throw new NotImplementedException("Unknown X axis scale mode");
            }

            if (IsAscii)
                SetStandardAsciiScaling(yAss.AxisScale.Tics().Count);
            else
            {
                //  We now know by how much we might have to shift the titles.  If we have to, restart our drawing process.
                double xShiftFromAxis = Math.Max(xAss.Size + x.ExtraSpaceBeforeAxisStarts, 0);
                if (!DrawXAxisTitle(x.Title, xShiftFromAxis))
                {
                    // The x axis title, or the bottom of the labels, or the legend, would fall off the bottom of the current canvas.  We need a new canvas with a better size.
                    extraSizeRequired.Height = (int)Math.Ceiling(xShiftFromAxis);
                }

                double yShiftFromAxis = Math.Max(yAss.Size + y.ExtraSpaceBeforeAxisStarts, 0);
                if (!DrawYAxisTitle(y.Title, yShiftFromAxis))
                {
                    // The y axis title, or the left of the labels, would fall off the left of the current canvas.  We need a new canvas with a better size.
                    extraSizeRequired.Width = (int)Math.Ceiling(yShiftFromAxis);
                }
            }

            // Draw the chart title now that we know it's safe to do so.
            if (!IsAscii)
                DrawTitle(title);
            return new AxisScalesAndExtraSize { AxisScales = new AxisScales { X = xAss.AxisScale, Y = yAss.AxisScale}, ExtraSize = extraSizeRequired };
        }

        protected AxisScaleAndSize DrawXScale(bool drawLabels, ScaleType scaleType, bool useCalculatedScalesEvenWithDefinition)
        {
            IAxisScale axisScale;
            if (IsAscii || drawLabels)
                axisScale = Q_AxisOrFromDefinition(DataMinX, DataMinGreaterThanZeroX, DataMaxX, false, scaleType, useCalculatedScalesEvenWithDefinition);
            else
            {
                //  Not ASCII, not drawing our own labels, so just set up 20 divisions
                axisScale = new LinearAxisScale(DataMinX, DataMaxX, DataMinX, DataMaxX, 20, 5);
            }

            DataMinX = axisScale.MinimumDataValue;
            DataMaxX = axisScale.MaximumDataValue;
            axisXMin = axisScale.MinimumScaleValue;
            axisXMax = axisScale.MaximumScaleValue;
            divx = Transform(axisXMax, scaleType) - Transform(axisXMin, scaleType);
            offx = -(Transform(axisXMin, scaleType) / divx * xExtCanvas) + xAxisCanvas;
            // set a string mask that will fit OK
            string msk = AxisMaskOrFromDefinition(axisScale, false, useCalculatedScalesEvenWithDefinition);

            float labelHeight = 0;
            if (!IsAscii)
            {
                LabelDirection direction = LabelDirection.Across;
                bool hasGridLines = false;
                System.Drawing.Drawing2D.DashStyle gridLineDashStyle = System.Drawing.Drawing2D.DashStyle.Solid;
                if (HasScaleParameters && definition.ScaleParameters.X != null)
                {
                    direction = definition.ScaleParameters.X.LabelDirection;
                    hasGridLines = definition.ScaleParameters.X.HasGridLines;
                    gridLineDashStyle = definition.ScaleParameters.X.GridLineDashStyle;
                }
                using (Pen gridLinePen = new Pen(axisPen.Color, 1))
                {
                    gridLinePen.DashStyle = gridLineDashStyle;
                    foreach (Tic tic in axisScale.Tics())
                    {
                        double x1 = ToCanvasX(tic.Value);
                        switch (tic.TicType)
                        {
                            case TicType.Minor:
                                {
                                    AxisDrawline(x1, yAxisCanvas - AXIS_LITTLE_TICK, x1, yAxisCanvas);
                                    if (hasGridLines && !drawLabels)
                                        statsDirectCanvas.DrawLine(gridLinePen, x1, yAxisCanvas, x1, yAxisCanvas + yExtCanvas);
                                }
                                break;
                            case TicType.Major:
                                //  Major tic - may or may not be labelled
                                {
                                    if (drawLabels)
                                    {
                                        string lab = tic.Value.ToString(msk);
                                        labelHeight = Math.Max(AxisDrawStringAtAngleCT(lab, x1, yAxisCanvas - AXIS_BIG_TICK, direction).Height, Convert.ToSingle(labelHeight));
                                        AxisDrawline(x1, yAxisCanvas - AXIS_BIG_TICK, x1, yAxisCanvas);
                                    }
                                    else
                                    {
                                        AxisDrawline(x1, yAxisCanvas - AXIS_LITTLE_TICK, x1, yAxisCanvas);
                                    }
                                    if (hasGridLines)
                                        statsDirectCanvas.DrawLine(gridLinePen, x1, yAxisCanvas, x1, yAxisCanvas + yExtCanvas);
                                }
                                break;
                        }
                    }
                }
            }
            else
            {
                //  ASCII - always linear for now.  TODO: Log
                LinearAxisScale linearAxisScale = (LinearAxisScale)axisScale;
                shTx[ASCII_Ytxt - 1] = string.Empty.PadLeft(13) + "/" + new string('-', 61);
                for (int x = 0; x <= linearAxisScale.Intervals; x++)
                {
                    if ((x - linearAxisScale.Phase) % linearAxisScale.IntervalsPerMajorTic == 0)
                    {
                        string lab = (axisXMin + (x * linearAxisScale.Interval)).ToString(msk);
                        int l = lab.Length;
                        labelHeight = Math.Max(Convert.ToInt32(labelHeight), l);
                        int s = Convert.ToInt32((x * (60 / linearAxisScale.Intervals)) + 15);
                        int s2;
                        if (lab.Substring(0, 1) == "-")
                            s2 = s - 1;
                        else
                            s2 = s;
                        WriteAsciiYX(ASCII_Ytxt - 2, s2, lab);
                        WriteAsciiYX(ASCII_Ytxt - 1, s, "+");
                    }
                }
            }
            return new AxisScaleAndSize { AxisScale = axisScale, Size = labelHeight };
        }

        protected IAxisScale Q_AxisOrFromDefinition(double qmin, double qMinGreaterThanZero, double qmax, bool isY, ScaleType scaleType, bool useCalculatedScalesEvenWithDefinition)
        {
            if ((definition != null) && definition.HasScaleParameters && !useCalculatedScalesEvenWithDefinition)
            {
                //  Use the values in our scale parameters
                AxisScaleParameters asp = isY ? definition.ScaleParameters.Y : definition.ScaleParameters.X;
                if ((null != asp) && null != asp.AxisScale)
                    return asp.AxisScale;
            }
            //  If we get here, there was no prior definition - calculate it ourselves.
            return AxisScalerFactory.AxisScalerFor(scaleType).Q_Axis(qmin, qMinGreaterThanZero, qmax);
        }

        protected string AxisMaskOrFromDefinition(IAxisScale axisScale, /* double stepp, double znmin, int nstep, int sp, */ bool isY, /* ScaleType scaleType, */ bool UseCalculatedScalesEvenWithDefinition)
        {
            if ((definition != null) && definition.HasScaleParameters && !UseCalculatedScalesEvenWithDefinition)
            {
                //  Use the values in our scale parameters
                AxisScaleParameters asp = isY ? definition.ScaleParameters.Y : definition.ScaleParameters.X;
                if ((null != asp) && null != asp.Mask)
                    return asp.Mask;
            }
            //  If we get here, there was no prior definition - calculate it ourselves.
            return AxisMasker.AxisMask(axisScale);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="reverse"></param>
        /// <param name="scaleType"></param>
        /// <param name="useCalculatedScalesEvenWithDefinition"></param>
        /// <returns>The width of the axis, ticks, gap to labels, and labels</returns>
        protected AxisScaleAndSize DrawYScale(bool reverse, ScaleType scaleType, bool useCalculatedScalesEvenWithDefinition)
        {
            const double AXIS_LABEL_OFFSET_FROM_BIG_TICK = 8;

            // find a neat axis division
            IAxisScale axisScale = Q_AxisOrFromDefinition(DataMinY, DataMinGreaterThanZeroY, DataMaxY, true, scaleType, useCalculatedScalesEvenWithDefinition);
            DataMinY = axisScale.MinimumDataValue;
            DataMaxY = axisScale.MaximumDataValue;
            axisYMin = axisScale.MinimumScaleValue;
            axisYMax = axisScale.MaximumScaleValue;
            divy = Transform(axisYMax, scaleType) - Transform(axisYMin, scaleType);
            offy = -(Transform(axisYMin, scaleType) / divy * yExtCanvas) + yAxisCanvas;

            // set a string mask that will fit OK
            string msk = AxisMaskOrFromDefinition(axisScale, true, useCalculatedScalesEvenWithDefinition);
            LabelDirection direction = LabelDirection.Across;
            bool hasGridLines = false;
            System.Drawing.Drawing2D.DashStyle gridLineDashStyle = System.Drawing.Drawing2D.DashStyle.Solid;
            if (HasScaleParameters && definition.ScaleParameters.Y != null)
            {
                direction = definition.ScaleParameters.Y.LabelDirection;
                hasGridLines = definition.ScaleParameters.Y.HasGridLines;
                gridLineDashStyle = definition.ScaleParameters.Y.GridLineDashStyle;
            }

            double maxLabelWidth = 0;
            if (!IsAscii)
            {
                using (Pen gridLinePen = new Pen(axisPen.Color, 1))
                {
                    gridLinePen.DashStyle = gridLineDashStyle;
                    foreach (Tic tic in axisScale.Tics())
                    {
                        double y1 = ToCanvasY(tic.Value, reverse);
                        switch (tic.TicType)
                        {
                            case TicType.Minor:
                                AxisDrawline(xAxisCanvas - AXIS_LITTLE_TICK, y1, xAxisCanvas, y1);
                                break;
                            case TicType.Major:
                                string lab = tic.Value.ToString(msk);
                                maxLabelWidth = Math.Max(Convert.ToSingle(maxLabelWidth), AxisDrawStringAtAngleRM(lab, xAxisCanvas - (AXIS_BIG_TICK + AXIS_LABEL_OFFSET_FROM_BIG_TICK), y1, direction).Width);
                                AxisDrawline(xAxisCanvas - AXIS_BIG_TICK, y1, xAxisCanvas, y1);
                                if (hasGridLines)
                                    statsDirectCanvas.DrawLine(gridLinePen, xAxisCanvas, y1, xAxisCanvas + xExtCanvas, y1);
                                break;
                            default:
                                throw new NotImplementedException("Unknown tic type in DrawYScale");
                        }
                    }
                }
            }
            else
            {
                // ASCII charts only work with linear scales
                LinearAxisScale linearAxisScale = (LinearAxisScale)axisScale;
                for (int y = 0; y <= linearAxisScale.Intervals; y++)
                {
                    if ((y - linearAxisScale.Phase) % linearAxisScale.IntervalsPerMajorTic == 0)
                    {
                        string lab = (axisYMin + (y * linearAxisScale.Interval)).ToString(msk);
                        int l = lab.Length;
                        WriteAsciiYX(y + ASCII_Ytxt, 14 - l, lab);
                        WriteAsciiYX(y + ASCII_Ytxt, 14, "+");
                    }
                    else
                    {
                        WriteAsciiYX(y + ASCII_Ytxt, 14, "|");
                    }
                }
            }
            return new AxisScaleAndSize { AxisScale = axisScale, Size = maxLabelWidth + AXIS_BIG_TICK + AXIS_LABEL_OFFSET_FROM_BIG_TICK };
        }

        ///  <summary>
        ///  Draw the Y axis as a series
        ///  </summary>
        /// <returns>The width of the axis, ticks, gap to labels, and labels</returns>
        ///  <remarks>Labels are drawn centred between tics</remarks>
        protected AxisScaleAndSize DrawYSeries(IList<string> labels)
        {
            const int AXIS_LABEL_OFFSET_FROM_TICK = 3;

            double maxLabelWidth = 0;
            if (!IsAscii)
            {
                // Vector
                using (StringFormat txtFormat = new StringFormat())
                {
                    txtFormat.Alignment = StringAlignment.Far;
                    txtFormat.LineAlignment = StringAlignment.Center;
                    LabelDirection direction = LabelDirection.Across;
                    bool hasGridLines = false;
                    System.Drawing.Drawing2D.DashStyle gridLineDashStyle = System.Drawing.Drawing2D.DashStyle.Solid;
                    if (HasScaleParameters && definition.ScaleParameters.Y != null)
                    {
                        direction = definition.ScaleParameters.Y.LabelDirection;
                        hasGridLines = definition.ScaleParameters.Y.HasGridLines;
                        gridLineDashStyle = definition.ScaleParameters.Y.GridLineDashStyle;
                    }
                    using (Pen gridLinePen = new Pen(axisPen.Color, 1))
                    {
                        gridLinePen.DashStyle = gridLineDashStyle;
                        double count = labels.Count;
                        for (int y = 0; y < labels.Count; y++)
                        {
                            double yctr = yAxisCanvas + yExtCanvas - ((y + 0.5) / count * yExtCanvas);
                            double ytic = yAxisCanvas + yExtCanvas - (y / count * yExtCanvas);
                            maxLabelWidth = Math.Max(maxLabelWidth, statsDirectCanvas.DrawStringAtAngle(labels[y], axisLabelFont, axisBrush, xAxisCanvas - (AXIS_BIG_TICK + AXIS_LABEL_OFFSET_FROM_TICK), yctr, txtFormat, direction).Width);
                            AxisDrawline(xAxisCanvas - AXIS_BIG_TICK, ytic, xAxisCanvas, ytic);
                            if (hasGridLines)
                                statsDirectCanvas.DrawLine(gridLinePen, xAxisCanvas, ytic, xAxisCanvas + xExtCanvas, ytic);
                        }
                    }
                }
            }
            else
            {
                //  ASCII
                for (int y = 0; y < labels.Count; y++)
                {
                    int Y2 = 3 + y * 2;
                    int L = labels[y].Length;
                    int q = 13 - L;
                    if (L >= 13)
                        q = 1;
                    WriteAsciiYX(Y2, q, labels[y].Substring(0, Math.Min(L, 13)));
                    WriteAsciiYX(Y2, 14, "|");
                    WriteAsciiYX(Y2 + 1, 14, "+");
                }
            }

            return new AxisScaleAndSize { Size = maxLabelWidth + AXIS_BIG_TICK + AXIS_LABEL_OFFSET_FROM_TICK };
        }

        ///  <summary>
        ///  Draw the X axis as a series
        ///  </summary>
        protected AxisScaleAndSize DrawXSeries(IList<string> labels)
        {
            double maxHeight = 0;
            if (!IsAscii)
            {
                using (StringFormat txtFormat = new StringFormat())
                {
                    txtFormat.Alignment = StringAlignment.Center;
                    txtFormat.LineAlignment = StringAlignment.Near;
                    LabelDirection direction = LabelDirection.Across;
                    bool hasGridLines = false;
                    System.Drawing.Drawing2D.DashStyle gridLineDashStyle = System.Drawing.Drawing2D.DashStyle.Solid;
                    if (HasScaleParameters && definition.ScaleParameters.X != null)
                    {
                        direction = definition.ScaleParameters.X.LabelDirection;
                        hasGridLines = definition.ScaleParameters.X.HasGridLines;
                        gridLineDashStyle = definition.ScaleParameters.X.GridLineDashStyle;
                    }
                    using (Pen gridLinePen = new Pen(axisPen.Color, 1))
                    {
                        gridLinePen.DashStyle = gridLineDashStyle;
                        double count = labels.Count;
                        for (int x = 0; x < labels.Count; x++)
                        {
                            double xctr = xAxisCanvas + (x + 0.5) / count * xExtCanvas;
                            double xtic = xAxisCanvas + (x + 1.0) / count * xExtCanvas;
                            maxHeight = Math.Max(maxHeight, statsDirectCanvas.DrawStringAtAngle(labels[x], axisLabelFont, axisBrush, xctr, yAxisCanvas - AXIS_BIG_TICK, txtFormat, direction).Height);
                            AxisDrawline(xtic, yAxisCanvas - AXIS_BIG_TICK, xtic, yAxisCanvas);
                            if (hasGridLines)
                                statsDirectCanvas.DrawLine(gridLinePen, xtic, yAxisCanvas, xtic, yAxisCanvas + yExtCanvas);
                        }
                    }
                }
            }
            return new AxisScaleAndSize { Size = maxHeight };
        }

        protected void AxisDrawline(double x1, double y1, double x2, double y2)
        {
            statsDirectCanvas.DrawLine(axisPen, x1, y1, x2, y2);
        }

        /// <summary>
        /// Draw axis text aligned to the right
        /// </summary>
        /// <param name="txt"></param>
        /// <param name="x1"></param>
        /// <param name="y1"></param>
        /// <param name="direction"></param>
        /// <returns></returns>
        protected SizeF AxisDrawStringAtAngleRM(string txt, double x1, double y1, LabelDirection direction)
        {
            using (StringFormat alignTxt = new StringFormat())
            {
                alignTxt.Alignment = StringAlignment.Far;
                alignTxt.LineAlignment = StringAlignment.Center;
                return statsDirectCanvas.DrawStringAtAngle(txt, axisLabelFont, axisBrush, x1, y1, alignTxt, direction);
            }
        }

        ///  <summary>
        ///  Draw axis text aligned to the centre
        ///  </summary>
        ///  <param name="txt"></param>
        ///  <param name="x1"></param>
        ///  <param name="y1"></param>
        /// <param name="direction"></param>
        protected SizeF AxisDrawStringAtAngleCT(string txt, double x1, double y1, LabelDirection direction)
        {
            using (StringFormat alignTxt = new StringFormat())
            {
                alignTxt.Alignment = StringAlignment.Center;
                alignTxt.LineAlignment = StringAlignment.Near;
                return statsDirectCanvas.DrawStringAtAngle(txt, axisLabelFont, axisBrush, x1, y1, alignTxt, direction);
            }
        }

        ///  <summary>
        ///  Draw axis text aligned to the centre
        ///  </summary>
        ///  <param name="txt"></param>
        ///  <param name="x1"></param>
        ///  <param name="y1"></param>
        protected void AxisDrawStringC(string txt, double x1, double y1)
        {
            using (StringFormat alignTxt = new StringFormat())
            {
                alignTxt.Alignment = StringAlignment.Center;
                statsDirectCanvas.DrawString(txt, axisLabelFont, axisBrush, x1, y1, alignTxt);
            }
        }

        ///  <summary>
        ///  Draw legend text aligned to the left
        ///  </summary>
        ///  <remarks></remarks>
        protected void DrawStringLegendL(string txt, double x, double y)
        {
            DrawStringLegend(txt, x, y, StringAlignment.Near);
        }

        ///  <summary>
        ///  Draw legend text
        ///  </summary>
        ///  <remarks></remarks>
        protected void DrawStringLegend(string txt, double x, double y, StringAlignment alignment)
        {
            using (StringFormat alignTxt = new StringFormat())
            {
                alignTxt.Alignment = alignment;
                statsDirectCanvas.DrawString(txt, legendFont, axisBrush, x, y, alignTxt);
            }
        }

        ///  <summary>
        ///  Draw label text
        ///  </summary>
        ///  <remarks></remarks>
        protected void DrawStringLabel(string txt, double x, double y, StringAlignment alignment)
        {
            using (StringFormat alignTxt = new StringFormat())
            {
                alignTxt.Alignment = alignment;
                statsDirectCanvas.DrawString(txt, labelFont, axisBrush, x, y, alignTxt);
            }
        }

        protected void DrawStringLabel(string txt, double x, double y, StringAlignment alignment, StringAlignment lineAlignment)
        {
            using (StringFormat alignTxt = new StringFormat())
            {
                alignTxt.Alignment = alignment;
                alignTxt.LineAlignment = lineAlignment;
                statsDirectCanvas.DrawString(txt, labelFont, axisBrush, x, y, alignTxt);
            }
        }

        [Obsolete("Ideally subclasses would never need to use canvas co-ordinates")]
        protected void DrawMarkerInCanvasCoordinates(double x, double y, double size, DoubleSeries series)
        {
            statsDirectCanvas.DrawMarker(x, y, size, series.MarkerDetails.MarkerShape, series.MarkerDetails.IsMarkerFilled, series.MarkerDetails.MarkerPen);
        }

        [Obsolete("Ideally subclasses would never need to use canvas co-ordinates")]
        protected void DrawMarkerInCanvasCoordinates(double x, double y, double size, MarkerType mType)
        {
            using (Pen p = GetMarkerPen(mType))
            {
                statsDirectCanvas.DrawMarker(x, y, size, mType.MarkerShape, mType.IsMarkerFilled, p);
            }
        }

        protected void DrawMarkerInChartCoordinates(double x, double y, double size, MarkerType mType)
        {
            using (Pen p = GetMarkerPen(mType))
            {
                statsDirectCanvas.DrawMarker(ToCanvasX(x), ToCanvasY(y), size, mType.MarkerShape, mType.IsMarkerFilled, p);
            }
        }

        protected void SetStandardAsciiScaling(int yDivisions)
        {
            divx = axisXMax - axisXMin;
            offx = SafeToInt32(-(axisXMin / divx * 60) + 15);
            divy = axisYMax - axisYMin;
            offy = SafeToInt32(-(axisYMin / divy * yDivisions) + ASCII_Ytxt);
        }

        protected double SafeToInt32(double d)
        {
            if (double.IsNaN(d) || d < int.MinValue || d > int.MaxValue)
                return 0;
            return Convert.ToInt32(d);
        }

        /// <summary>
        /// Convert a value from chart co-ordinates to canvas co-ordinates
        /// </summary>
        /// <param name="chartValue"></param>
        /// <param name="scaleType"></param>
        /// <returns></returns>
        protected float Transform(double chartValue, ScaleType scaleType)
        {
            switch (scaleType)
            {
                case ScaleType.Log10:
                    return chartValue > 0 ? (float)Math.Log10(chartValue) : 0;
                case ScaleType.LogNatural:
                    return chartValue > 0 ? (float)(Math.Log(chartValue) / LOG2) : 0;
                default:
                    return (Single)chartValue;
            }
        }

        protected double InverseTransform(double canvasValue, ScaleType scaleType)
        {
            switch (scaleType)
            {
                case ScaleType.Log10:
                    return Math.Pow(10, canvasValue);
                case ScaleType.LogNatural:
                    return Math.Pow(Math.E, canvasValue * LOG2);
                default:
                    return canvasValue;
            }
        }

        protected double ToCanvasWidth(double chartX)
        {
            ScaleType scaleType = ScaleType.Linear;
            if (HasScaleParameters && definition.ScaleParameters.X != null)
                scaleType = definition.ScaleParameters.X.ScaleType;
            return ToCanvasWidth(chartX, scaleType);
        }

        protected double ToCanvasWidth(double chartX, ScaleType scaleType)
        {
            return Transform(chartX, scaleType) / divx * xExtCanvas;
        }

        protected double ToCanvasX(double chartX)
        {
            ScaleType scaleType = ScaleType.Linear;
            if (HasScaleParameters && definition.ScaleParameters.X != null)
                scaleType = definition.ScaleParameters.X.ScaleType;
            return ToCanvasX(chartX, scaleType);
        }

        protected double ToCanvasX(double chartX, ScaleType scaleType)
        {
            return offx + ToCanvasWidth(chartX, scaleType);
        }

        protected double FromCanvasWidth(double canvasWidth)
        {
            ScaleType scaleType = ScaleType.Linear;
            if (HasScaleParameters && definition.ScaleParameters.X != null)
                scaleType = definition.ScaleParameters.X.ScaleType;
            return FromCanvasWidth(canvasWidth, scaleType);
        }

        protected double FromCanvasWidth(double canvasWidth, ScaleType scaleType)
        {
            double rawChartWidth = canvasWidth * divx / xExtCanvas;
            return InverseTransform(rawChartWidth, scaleType);
        }

        protected double InverseTransformX(double canvasX)
        {
            if ((!HasScaleParameters || definition.ScaleParameters.X == null))
                return canvasX;
            return InverseTransform(canvasX, definition.ScaleParameters.X.ScaleType);
        }

        protected double ToCanvasHeight(double chartY)
        {
            ScaleType scaleType = ScaleType.Linear;
            if (HasScaleParameters && definition.ScaleParameters.Y != null)
                scaleType = definition.ScaleParameters.Y.ScaleType;
            return ToCanvasHeight(chartY, scaleType);
        }

        protected double ToCanvasHeight(double chartY, ScaleType scaleType)
        {
            double transformed = Transform(chartY, scaleType);
            return transformed / divy * yExtCanvas;
        }

        protected double ToCanvasY(double chartY, bool reverse = false)
        {
            ScaleType scaleType = ScaleType.Linear;
            if (HasScaleParameters && definition.ScaleParameters.Y != null)
                scaleType = definition.ScaleParameters.Y.ScaleType;
            return ToCanvasY(chartY, reverse, scaleType);
        }

        protected double ToCanvasY(double chartY, bool reverse, ScaleType scaleType)
        {
            double height = ToCanvasHeight(chartY, scaleType);
            if (reverse)
                height = yExtCanvas - height;
            return offy + height;
        }

        protected double FromCanvasHeight(double canvasHeight)
        {
            ScaleType scaleType = ScaleType.Linear;
            if (HasScaleParameters && definition.ScaleParameters.Y != null)
                scaleType = definition.ScaleParameters.Y.ScaleType;
            return FromCanvasHeight(canvasHeight, scaleType);
        }

        protected double FromCanvasHeight(double canvasHeight, ScaleType scaleType)
        {
            double rawChartHeight = canvasHeight * divy / yExtCanvas;
            return InverseTransform(rawChartHeight, scaleType);
        }

        [Obsolete("TODO: Get pyramid to use an x scale without tics and draw this in that way")]
        protected void DrawStringInCanvasCoordinates(string s, Font font, Brush brush, double x, double y, StringFormat txtFormat)
        {
            statsDirectCanvas.DrawString(s, font, brush, x, y, txtFormat);
        }

        ///  <summary>
        ///  Cases:
        ///  Left-justify: (x,y) is centre of left-hand edge of text.
        ///  Right-justify: (x,y) is centre of right-hand edge of text.
        ///  </summary>
        ///  <param name="s"></param>
        ///  <param name="font"></param>
        ///  <param name="brush"></param>
        ///  <param name="x">The </param>
        ///  <param name="y"></param>
        ///  <param name="txtFormat"></param>
        ///  <param name="direction"></param>
        ///  <returns>The bounding size of s drawn in direction with txtFormat</returns>
        /// <remarks></remarks>
        [Obsolete("Ideally subclasses would never need to use canvas co-ordinates")]
        protected SizeF DrawStringAtAngleInCanvasCoordinates(string s, Font font, Brush brush, double x, double y, StringFormat txtFormat, LabelDirection direction)
        {
            return statsDirectCanvas.DrawStringAtAngle(s, font, brush, x, y, txtFormat, direction);
        }

        ///  <summary>
        ///  Draw a square of side size, centred on (x, y).
        ///  </summary>
        ///  <param name="p">The pen with which to draw the outline and, if filled, from which to take the fill colour.</param>
        ///  <param name="x"></param>
        ///  <param name="y"></param>
        ///  <param name="size"></param>
        ///  <param name="fill">If true, fill the square; if false, merely draw the outline.</param>
        ///  <remarks></remarks>
        [Obsolete("Ideally subclasses would never need to use canvas co-ordinates")]
        protected void DrawSquareInCanvasCoordinates(Pen p, double x, double y, double size, bool fill)
        {
            statsDirectCanvas.DrawSquare(p, x, y, size, fill);
        }

        ///  <summary>
        ///  Draw a diamond of diameter size, centred on (x, y)
        ///  </summary>
        ///  <param name="p">The pen with which to draw the outline and, if filled, from which to take the fill colour.</param>
        ///  <param name="x"></param>
        ///  <param name="y"></param>
        ///  <param name="size"></param>
        ///  <param name="fill">If true, fill the square; if false, merely draw the outline.</param>
        /// <remarks></remarks>
        [Obsolete("Ideally subclasses would never need to use canvas co-ordinates")]
        protected void DrawDiamondInCanvasCoordinates(Pen p, double x, double y, double size, bool fill)
        {
            statsDirectCanvas.DrawDiamond(p, x, y, size, fill);
        }

        [Obsolete("Ideally subclasses would never need to use canvas co-ordinates")]
        protected void DrawLineInCanvasCoordinates(Pen p, double x1, double y1, double x2, double y2)
        {
            statsDirectCanvas.DrawLine(p, x1, y1, x2, y2);
        }

        [Obsolete("Ideally subclasses would never need to use canvas co-ordinates")]
        protected void DrawMarkerInCanvasCoordinates(double x, double y, double size, MarkerShape shape, bool isFilled, Pen p)
        {
            statsDirectCanvas.DrawMarker(x, y, size, shape, isFilled, p);
        }

        [Obsolete("Ideally subclasses would never need to use canvas co-ordinates")]
        protected void FillRectangleInCanvasCoordinates(Brush b, double x, double y, double w, double h)
        {
            statsDirectCanvas.FillRectangle(b, x, y, w, h);
        }

        [Obsolete("Ideally subclasses would never need to use canvas co-ordinates")]
        protected void DrawRectangleInCanvasCoordinates(Pen p, double x, double y, double w, double h)
        {
            statsDirectCanvas.DrawRectangle(p, x, y, w, h);
        }

        [Obsolete("Ideally subclasses would never need to use canvas co-ordinates")]
        protected SizeF MeasureStringInCanvasCoordinates(string s, Font font)
        {
            return statsDirectCanvas.MeasureString(s, font);
        }

        [Obsolete("Ideally subclasses would never need to use canvas co-ordinates")]
        protected double GetFontHeightInCanvasCoordinates(Font f)
        {
            return statsDirectCanvas.GetFontHeight(f);
        }

        private Pen GetSameOrDifferentPen(Color color)
        {
            if (mostRecentPen == null || !(mostRecentPen.Color.Equals(color)))
            {
                if (null != mostRecentPen)
                    mostRecentPen.Dispose();
                mostRecentPen = new Pen(color);
            }
            return mostRecentPen;
        }
        public void DrawLineInChartCoordinates(Color color, double x1, double y1, double x2, double y2)
        {
            double dx1 = ToCanvasX(x1);
            double dy1 = ToCanvasY(y1);
            double dx2 = ToCanvasX(x2);
            double dy2 = ToCanvasY(y2);
            statsDirectCanvas.DrawLine(GetSameOrDifferentPen(color), dx1, dy1, dx2, dy2);
        }

        protected void DrawRectangleInChartCoordinates(Color color, double left, double top, double width, double height)
        {
            statsDirectCanvas.DrawRectangle(GetSameOrDifferentPen(color), ToCanvasX(left), ToCanvasY(top), ToCanvasWidth(width), ToCanvasHeight(height));
        }

        protected bool HasScaleParameters
        {
            get
            {
                return (definition != null) && definition.HasScaleParameters;
            }
        }

        protected bool HasChartOptions
        {
            get
            {
                return (definition != null) && (definition.ChartOptions != null);
            }
        }

        protected bool ShouldUseColour
        {
            get
            {
                bool useColour = !DefaultAllBlack;
                if (HasChartOptions)
                    useColour = definition.ChartOptions.UseColour;
                return useColour;
            }
        }

        public Pen GetMarkerPen(MarkerType mt)
        {
            return new Pen(ShouldUseColour ? mt.MarkerColor : grBlack, mt.Width);
        }

        /// <summary>
        /// Return a new Pen of the given type. It is up to the caller to dispose of this.
        /// </summary>
        public Pen GetLinePen(MarkerType mt, bool ignoreStyle)
        {
            Pen p = new Pen(ShouldUseColour ? mt.LineColor : grBlack, mt.Width);
            if (!ignoreStyle)
                p.DashStyle = mt.LineDashStyle;
            return p;
        }

        ///  <summary>
        ///  A colour to be used for drawing lines that are nominally black.
        ///  </summary>
        ///  <value></value>
        ///  <returns></returns>
        ///  <remarks></remarks>
        public static Color grBlack
        {
            get
            {
                return Color.Black;
            }
        }

        ///  <summary>
        ///  A colour to be used for drawing lines that are nominally green.
        ///  </summary>
        ///  <value></value>
        ///  <returns></returns>
        ///  <remarks></remarks>
        protected Color grGreen
        {
            get { return ShouldUseColour ? Color.Green : Color.Black; }
        }

        ///  <summary>
        ///  A colour to be used for drawing lines that are nominally magenta.
        ///  </summary>
        ///  <value></value>
        ///  <returns></returns>
        ///  <remarks></remarks>
        public Color grMagenta
        {
            get { return ShouldUseColour ? Color.Magenta : Color.Black; }
        }

        ///  <summary>
        ///  A colour to be used for drawing lines that are nominally red.
        ///  </summary>
        ///  <value></value>
        ///  <returns></returns>
        ///  <remarks></remarks>
        protected Color grRed
        {
            get { return ShouldUseColour ? Color.Red : Color.Black; }
        }

        ///  <summary>
        ///  A colour to be used for drawing axis lines
        ///  </summary>
        ///  <value></value>
        ///  <returns></returns>
        ///  <remarks></remarks>
        private Color grAxis
        {
            get { return ShouldUseColour ? Color.FromArgb(134, 134, 134) : Color.Black; }
        }

        [Obsolete("RTF should not be used in ChartRenderer")]
        public string GetAsciiRTF()
        {
            if (!IsAscii)
                throw new InvalidOperationException("Trying to get ASCII string for a non-ASCII chart");

            StringBuilder sb = new StringBuilder();
            for (int i = shTx.GetUpperBound(0); i >= shTx.GetLowerBound(0); i--)
            {
                sb.Append(shTx[i]);
                sb.Append(Formatting.RTFCRLF);
            }
            return sb.ToString();
        }

        protected void WriteAsciiYX(int y, int x, string text)
        {
#if DEBUG
            if (y < 0 || y >= shTx.Length)
                throw new Exception("y is out of the renderer's range");
#endif
            shTx[y] = ReplaceAt(shTx[y], x, text);
        }

        protected void WriteAsciiYX(int y, int x, char c)
        {
            shTx[y] = ReplaceAt(shTx[y], x, c);
        }

        private static string ReplaceAt(string buffer, int x, string text)
        {
            int l = buffer.Length;
            int tl = text.Length;
            return buffer.Substring(0, Math.Min(l, x)) + new string(' ', Math.Max(0, x - l)) + text + (x + tl >= l ? string.Empty : buffer.Substring(x + tl));
        }

        private static string ReplaceAt(string buffer, int x, char c)
        {
            return buffer.Substring(0, x) + c + buffer.Substring(x + 1);
        }

        public void SetBox0To1()
        {
            axisXMax = 1.0;
            axisYMax = 1.0;
            axisXMin = 0.0;
            axisYMin = 0.0;
            boxAxes = true;
        }

        public void AssignMarkersToSeries()
        {
            if (definition.XSeries.Count > 0)
                AssignMarkersToSeries(definition.XSeries);
            if (definition.YSeries.Count > 0)
                AssignMarkersToSeries(definition.YSeries);
        }

        protected void AssignMarkersToSeries(GenericOptions opts)
        {
            if (definition.XSeries.Count > 0)
                AssignMarkersToSeries(definition.XSeries, opts);
            if (definition.YSeries.Count > 0)
                AssignMarkersToSeries(definition.YSeries, opts);
        }

        protected void AssignMarkersToSeries(List<Series> s)
        {
            for (int i = 0; i <= s.Count - 1; i++)
            {
                DoubleSeries ds = ((DoubleSeries)(s[i]));
                int mkr = ChartOptions.SeriesNumberToMarkerNumber(i);
                SetSeriesFromMarkerTypeAndOptions(ds, SharedMarkerTypes[mkr], null);
            }
        }

        private void SetSeriesFromMarkerTypeAndOptions(DoubleSeries ds, MarkerType mt, GenericOptions o)
        {
            if (o != null)
                ds.MarkerDetails.IsMarkerFilled = o.ShouldForceIsFilled ? o.ForcedIsFilled : mt.IsMarkerFilled;
            ds.MarkerDetails.MarkerPen = GetMarkerPen(mt);
            //  Dash styles are only used in monochrome plots; if colour, ignore.
            ds.MarkerDetails.LinePen = GetLinePen(mt, ShouldUseColour);
            ds.MarkerDetails.MarkerShape = mt.MarkerShape;
            ds.MarkerDetails.MarkerSize = mt.MarkerSize;
        }

        protected void AssignMarkersToSeries(List<Series> s, GenericOptions opts)
        {
            if (opts == null || opts.MarkerTypes == null || opts.MarkerTypes.Count < 1)
            {
                for (int i = 0; i < s.Count; i++)
                {
                    DoubleSeries ds = ((DoubleSeries)(s[i]));
                    int mkr = ChartOptions.SeriesNumberToMarkerNumber(i);
                    SetSeriesFromMarkerTypeAndOptions(ds, SharedMarkerTypes[mkr], opts);
                }
            }
            else
            {
                for (int i = 0; i < s.Count; i++)
                {
                    if (s[i] is DoubleSeries)
                    {
                        DoubleSeries ds = ((DoubleSeries)(s[i]));
                        int mkr = i % opts.MarkerTypes.Count;
                        SetSeriesFromMarkerTypeAndOptions(ds, opts.MarkerTypes[mkr], opts);
                    }
                }
            }
        }


        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (null != axisBrush)
            {
                axisBrush.Dispose();
                axisBrush = null;
            }
            if (null != axisPen)
            {
                axisPen.Dispose();
                axisPen = null;
            }
            if (null != statsDirectCanvas)
            {
                statsDirectCanvas.Dispose();
                statsDirectCanvas = null;
            }
            if (null != mostRecentPen)
            {
                mostRecentPen.Dispose();
                mostRecentPen = null;
            }
            if (null != axisLabelFont)
            {
                axisLabelFont.Dispose();
                axisLabelFont = null;
            }
            if (null != axisTitleFont)
            {
                axisTitleFont.Dispose();
                axisTitleFont = null;
            }
            if (null != labelFont)
            {
                labelFont.Dispose();
                labelFont = null;
            }
            if (null != legendFont)
            {
                legendFont.Dispose();
                legendFont = null;
            }
            if (null != titleFont)
            {
                titleFont.Dispose();
                titleFont = null;
            }
        }

        public int ImageWidth { get { return imageWidth; } }
        public int ImageHeight { get { return imageHeight; } }

        ///  <summary>
        ///  Initialise everything required for an ASCII plot of the required number of lines, notably including the SH_TX array.
        ///  </summary>
        ///  <param name="lines">The number of lines of text in the ASCII plot</param>
        protected void ASCII_InitPlot(int lines)
        {
            shTx = new string[lines + 1];
            for (int c = 0; c <= shTx.GetUpperBound(0); c++)
            {
                shTx[c] = string.Empty.PadLeft(85);
            }
        }

        protected void ASCII_PlotPoint(int x, int y)
        {
            // Check if a point has already been plotted
            switch (shTx[y][x])
            {
                case ' ':
                    WriteAsciiYX(y, x, "*");
                    break;
                case '*':
                    WriteAsciiYX(y, x, "2");
                    break;
                case '9':
                    WriteAsciiYX(y, x, "X");
                    break;
                case '|':
                case '+':
                case '-':
                    return;
                default:
                    // Must be numeric; add 1
                    WriteAsciiYX(y, x, (char)(shTx[y][x] + 1));
                    break;
            }
        }

        /// <summary>
        /// Marker lines are single values on the X or Y axis that the user has requested to be drawn.
        /// </summary>
        protected void MaybeDrawMarkerLines()
        {
            if (null == definition)
                return;
            if (null == definition.ScaleParameters)
                return;
            if (definition.ScaleParameters.X.MarkerLineValue.HasValue)
            {
                double x = ToCanvasX(definition.ScaleParameters.X.MarkerLineValue.Value);
                using (Pen tenPen = new Pen(grBlack, 1))
                {
                    DrawLineInCanvasCoordinates(tenPen, x, yAxisCanvas, x, yAxisCanvas + yExtCanvas);
                }
            }
            if (definition.ScaleParameters.Y.MarkerLineValue.HasValue)
            {
                double y = ToCanvasY(definition.ScaleParameters.Y.MarkerLineValue.Value);
                using (Pen tenPen = new Pen(grBlack, 1))
                {
                    DrawLineInCanvasCoordinates(tenPen, xAxisCanvas, y, xAxisCanvas + xExtCanvas, y);
                }
            }
        }

        protected void SetFontsAndThicknessesFromOptions(GenericOptions o)
        {
            if (o.UsesAxisLabelFontDescriptor && !(string.IsNullOrEmpty(o.AxisLabelFontDescriptor)))
                axisLabelFont = FontFromSaveString(o.AxisLabelFontDescriptor);
            if (o.UsesAxisTitleFontDescriptor && !(string.IsNullOrEmpty(o.AxisTitleFontDescriptor)))
                axisTitleFont = FontFromSaveString(o.AxisTitleFontDescriptor);
            if (o.UsesLegendFontDescriptor && !(string.IsNullOrEmpty(o.LegendFontDescriptor)))
                legendFont = FontFromSaveString(o.LegendFontDescriptor);
            if (o.UsesTitleFontDescriptor && !(string.IsNullOrEmpty(o.TitleFontDescriptor)))
                titleFont = FontFromSaveString(o.TitleFontDescriptor);

            if (o.UsesAxisLineThickness)
            {
                axisLineThickness = o.AxisLineThickness;
                Color c = Color.Black;
                if (axisPen != null)
                {
                    c = axisPen.Color;
                    axisPen.Dispose();
                }
                axisPen = new Pen(c, axisLineThickness);
            }
        }

        public static IList<MarkerType> MarkersFromDescriptors(IList<SeriesOptionsDescriptor> seriesOptionsDescriptors, bool shouldForceIsFilled, bool forcedIsFilled, bool shouldForceFillStyle, FillStyle forcedFillStyle)
        {
            IList<MarkerType> markerTypes = new List<MarkerType>(seriesOptionsDescriptors.Count);
            foreach (SeriesOptionsDescriptor t in seriesOptionsDescriptors)
            {
                int markerIndex = t.MarkerIndex;
                int mkr = ChartOptions.SeriesNumberToMarkerNumber(markerIndex);

                MarkerType clone = MarkerTypes[mkr].Clone();
                if (shouldForceIsFilled)
                    clone.IsMarkerFilled = forcedIsFilled;
                if (shouldForceFillStyle)
                    clone.MarkerFillStyle = forcedFillStyle;
                markerTypes.Add(clone);
            }
            return markerTypes;
        }

        protected Brush MarkerTypeToBrush(MarkerType mt)
        {
            Color c;
            FillStyle f;
            if (ShouldUseColour)
            {
                c = mt.MarkerColor;
                f = FillStyle.Solid;
            }
            else
            {
                c = grBlack;
                f = mt.MarkerFillStyle;
            }

            Brush b = null;
            switch (f)
            {
                case FillStyle.None:
                    //  Do nothing
                    break;
                case FillStyle.Crosshatch:
                    b = new System.Drawing.Drawing2D.HatchBrush(System.Drawing.Drawing2D.HatchStyle.DiagonalCross, c, Color.White);
                    break;
                case FillStyle.BackwardDiagonal:
                    b = new System.Drawing.Drawing2D.HatchBrush(System.Drawing.Drawing2D.HatchStyle.BackwardDiagonal, c, Color.White);
                    break;
                case FillStyle.ForwardDiagonal:
                    b = new System.Drawing.Drawing2D.HatchBrush(System.Drawing.Drawing2D.HatchStyle.ForwardDiagonal, c, Color.White);
                    break;
                case FillStyle.Solid:
                    b = new SolidBrush(c);
                    break;
                default:
                    throw new ArgumentOutOfRangeException("mt", f, "FillStyle Values between 0 and 4 accepted");
            }

            return b;
        }

        protected static string MakeTitle(string useIfAvailable, string defaultTitle)
        {
            if (string.IsNullOrWhiteSpace(useIfAvailable))
                return defaultTitle;

            if (useIfAvailable.Length > MAX_LABEL_LENGTH)
                return useIfAvailable.Substring(0, MAX_LABEL_LENGTH);
            else
                return useIfAvailable;
        }

        public static string combo_ti(string cap)
        {
            string x = "combined";
            if (cap.Contains("fixed effects"))
                x += " [fixed]";
            else if (cap.Contains("random effects"))
                x += " [random]";
            return x;
        }

        /// <summary>
        /// Calculate a ratio scale with tics in each decade at 1, 2, 3, 5.
        /// </summary>
        /// <param name="tics"></param>
        /// <param name="tic"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <param name="scalemin"></param>
        /// <param name="scalemax"></param>
        protected static void CreateRatioLogScale(out int tics, out double[] tic, ref double min, ref double max, out double scalemin, out double scalemax)
        {
            double top = max, bot = min;
            // #1323: Detect an exact power of 10 and prevent it from fouling up the algorithm
            if (min == Math.Pow(10, Math.Floor(Math.Log10(min))))
                bot = 0.99 * bot;

            if (max <= 0)
                top = 100000000;
            if (min <= 0)
                bot = 0.00000001;

            double tmp = bot;
            int i = 1;
            double z;
            do
            {
                z = Math.Pow(10, Math.Floor(Math.Log10(tmp)));
                tmp = z;
                if (tmp >= bot) i++;
                if (tmp >= top) break;
                tmp += z;
                if (tmp >= bot) i++;
                if (tmp >= top) break;
                tmp += z;
                if (tmp >= bot) i++;
                if (tmp >= top) break;
                tmp += z * 2;
                if (tmp >= bot) i++;
                if (tmp >= top) break;
                tmp += z * 5;
            } while (tmp < top * 2);

            tics = i;
            tic = new double[tics + 1];

            tmp = bot;
            i = 1;
            do
            {
                z = Math.Pow(10, Math.Floor(Math.Log10(tmp)));
                tmp = z;
                if (tmp >= bot) i++;
                tic[i] = tmp;
                if (tmp >= top) break;
                tmp += z;
                if (tmp >= bot) i++;
                tic[i] = tmp;
                if (tmp >= top) break;
                tmp += z;
                if (tmp >= bot) i++;
                tic[i] = tmp;
                if (tmp >= top) break;
                tmp += z * 2;
                if (tmp >= bot) i++;
                tic[i] = tmp;
                if (tmp >= top) break;
                // TODO: Does this give a higher max than our scale points if we multiply tmp here and then exit (yes), and does it matter if it does (dunno)?
                tmp += z * 5;
            } while (tmp < top * 2);

            scalemin = tic[1];
            scalemax = tmp;
            min = scalemin;
            max = scalemax;
        }

        [Obsolete("Ideally subclasses would never need to use canvas co-ordinates")]
        protected float AxisLabelWidthInCanvasCoordinates(string s)
        {
            return MeasureStringInCanvasCoordinates(s, axisLabelFont).Width;
        }

        [Obsolete("Ideally subclasses would never need to use canvas co-ordinates")]
        protected float AxisLabelHeightInCanvasCoordinates(string s)
        {
            return MeasureStringInCanvasCoordinates(s, axisLabelFont).Height;
        }

        [Obsolete("Ideally subclasses would never need to use canvas co-ordinates")]
        protected float LegendWidthInCanvasCoordinates(string s)
        {
            return MeasureStringInCanvasCoordinates(s, legendFont).Width;
        }

        [Obsolete("Ideally subclasses would never need to use canvas co-ordinates")]
        protected float LegendHeightInCanvasCoordinates(string s)
        {
            return MeasureStringInCanvasCoordinates(s, legendFont).Height;
        }

        [Obsolete("Ideally subclasses would never need to use canvas co-ordinates")]
        protected float TitleWidthInCanvasCoordinates(string s)
        {
            return MeasureStringInCanvasCoordinates(s, titleFont).Width;
        }

        protected int ToAsciiX(double value)
        {
            return Convert.ToInt32(offx + value / divx * 60);
        }

        public abstract ParameterBag Plot(ITemplateHost host);
        public abstract ScaleParameters GetScaleParameters();

        protected class AxisScaleAndSize
        {
            public IAxisScale AxisScale { get; set; }
            public double Size { get; set; }
        }

        protected class AxisScalesAndExtraSize
        {
            public AxisScales AxisScales { get; set; }
            public Size ExtraSize { get; set; }
        }
    }
}