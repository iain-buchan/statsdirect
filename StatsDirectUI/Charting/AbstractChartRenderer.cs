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
        protected double xInt;
        protected double yInt;
        /// <summary>
        /// The X-position in canvas co-ordinates of the left-hand end of the chart's X-axis
        /// </summary>
        protected double xAxisCanvas;
        /// <summary>
        /// The length in canvas co-ordinates of the chart's X-axis
        /// </summary>
        protected double xExtCanvas;
        protected int xDiv;
        /// <summary>
        /// The Y-position in canvas co-ordinates of the bottom of the chart's Y-axis
        /// </summary>
        protected double yAxisCanvas;
        protected double yExtCanvas;
        protected int yDiv;

        protected double divx;
        protected double offx;
        protected double divy;
        protected double offy;

        /// <summary>How many minor tics per major tic on the axis?</summary>
        protected int minorTicsPerMajorTic;
        protected double scaleYAxis = 1.0;
        protected double scaleXAxis = 1.0;
        protected const int DEFAULT_METAFILE_HEIGHT = 800;
        protected const int DEFAULT_METAFILE_WIDTH = 1132;
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
                DefaultAxes(0);
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
        protected void DefaultAxes(double extraHeightRequiredAtBottom)
        {
            //  xaxis also needs to be reset in routines with legends
            xAxisCanvas = imageWidth / 7.55;
            yAxisCanvas = Math.Min(imageHeight / 8, DEFAULT_Y_GAP) + extraHeightRequiredAtBottom;
            xExtCanvas = imageWidth / 1.25 * scaleXAxis;
            yExtCanvas = imageHeight - Math.Min(imageHeight / 4, 2 * DEFAULT_Y_GAP * scaleYAxis) - extraHeightRequiredAtBottom;
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

        public bool DrawYAxisTitle(string title, double gapForAxisLabels)
        {
            // If the title would not fit on the current canvas, return false
            bool titleHasText = !(string.IsNullOrEmpty(title));
            double rightOfYAxisTitle = xAxisCanvas - AXIS_BIG_TICK - gapForAxisLabels - LABEL_TO_AXIS_LABEL_GAP;
            double leftOfYAxisTitle = rightOfYAxisTitle - (titleHasText ? (axisTitleFont.Height * 0.5) : 0);
            double middleOfYAxisTitle = (leftOfYAxisTitle + rightOfYAxisTitle) / 2.0;
            if (leftOfYAxisTitle < 0)
                return false;

            if (titleHasText)
            {
                using (StringFormat txtFormat = new StringFormat())
                {
                    txtFormat.Alignment = StringAlignment.Center;
                    txtFormat.LineAlignment = StringAlignment.Center;
                    statsDirectCanvas.DrawStringAtAngle(title, axisTitleFont, Brushes.Black, middleOfYAxisTitle, (yExtCanvas / 2.0) + yAxisCanvas, txtFormat, LabelDirection.Up);
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
        protected void DrawAxesOrEnlargeCanvas(string title, Axis x, Axis y, bool shouldBoxAxes, bool useCalculatedScalesEvenWithDefinition)
        {
            double extraWidthRequired;
            double extraHeightRequired;
            if (!DrawAxesOrFail(title, x, y, shouldBoxAxes, useCalculatedScalesEvenWithDefinition, out extraHeightRequired, out extraWidthRequired))
            {
                extraWidthRequired = Math.Ceiling(extraWidthRequired);
                imageWidth += (int)Math.Ceiling(extraWidthRequired);
                extraHeightRequired = Math.Ceiling(extraHeightRequired);
                imageHeight += (int)Math.Ceiling(extraHeightRequired);
                statsDirectCanvas.Dispose();
                statsDirectCanvas = new EmfCanvas(imageWidth, imageHeight);
                DefaultAxes(extraHeightRequired);
                if (!DrawAxesOrFail(title, x, y, shouldBoxAxes, useCalculatedScalesEvenWithDefinition, out extraHeightRequired, out extraWidthRequired))
                    throw new Exception("Even after trying to enlarge the canvas, I don't have enough space for the chart.");
            }
        }

        protected bool DrawAxesOrFail(string title, Axis x, Axis y, bool shouldBoxAxes, bool useCalculatedScalesEvenWithDefinition, out double extraHeightRequired, out double extraWidthRequired)
        {
            const int ALREADY_ALLOWED_WIDTH = 30;
            const int ALREADY_ALLOWED_HEIGHT = 30;

            if (!IsAscii)
            {
                xAxisCanvas += y.ExtraSpace;
                xExtCanvas -= y.ExtraSpace;

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

            double xHeight = 0;
            double yWidth = 0;
            switch (x.Mode)
            {
                case AxisMode.LineOnly:
                    //  Do nothing
                    break;
                case AxisMode.None:
                    //  Do nothing
                    break;
                case AxisMode.ReverseScale:
                    throw new ArgumentException("A reversed X scale is not currently supported");
                case AxisMode.Scale:
                case AxisMode.ScaleWithoutLabels:
                    xHeight = DrawXScale((x.Mode & AxisMode.Labels) == AxisMode.Labels, x.ScaleType, useCalculatedScalesEvenWithDefinition);
                    break;
                case AxisMode.Series:
                    if (null != x.Series)
                        xHeight = DrawXSeries(x.Series);
                    else if (null != x.Labels)
                        xHeight = DrawXSeries(x.Labels);
                    break;
            }

            switch (y.Mode)
            {
                case AxisMode.LineOnly:
                    //  Do nothing
                    break;
                case AxisMode.None:
                    //  Do nothing
                    break;
                case AxisMode.ReverseScale:
                    yWidth = DrawYScale(true, y.ScaleType, useCalculatedScalesEvenWithDefinition);
                    break;
                case AxisMode.Scale:
                    yWidth = DrawYScale(false, y.ScaleType, useCalculatedScalesEvenWithDefinition);
                    break;
                case AxisMode.ScaleWithoutLabels:
                    throw new ArgumentException("A Y scale without labels is not currently supported");
                case AxisMode.Series:
                    if (null != y.Series)
                        yWidth = DrawYSeries(y.Series);
                    else if (null != y.Labels)
                        yWidth = DrawYSeries(y.Labels);
                    break;
            }

            if (IsAscii)
                SetStandardAsciiScaling();
            else
            {
                if (x.Mode == AxisMode.Series && null != x.Series)
                {
                    divx = x.Series.Count;
                    offx = xAxisCanvas;
                }
                else
                {
                    divx = axisXMax - axisXMin;
                    offx = -(axisXMin / divx * xExtCanvas) + xAxisCanvas;
                }
                if (y.Mode == AxisMode.Series && null != y.Series)
                {
                    divy = y.Series.Count;
                    offy = yAxisCanvas;
                }
                else
                {
                    divy = axisYMax - axisYMin;
                    offy = -(axisYMin / divy * yExtCanvas) + yAxisCanvas;
                }

                //  We now know by how much we might have to shift the titles.  If we have to, restart our drawing process.
                bool succeeded = true;
                double xShiftFromAxis = Math.Max(xHeight + x.ExtraSpace - ALREADY_ALLOWED_HEIGHT, 0);
                if (DrawXAxisTitle(x.Title, Math.Max(xShiftFromAxis, x.AxisTitleOffset)))
                    extraHeightRequired = 0;
                else
                {
                    // The x axis title, or the bottom of the labels, or the legend, would fall off the bottom of the current canvas.  We need a new canvas with a better size.
                    extraHeightRequired = xShiftFromAxis;
                    succeeded = false;
                }

                double yShiftFromAxis = Math.Max(yWidth + y.ExtraSpace - ALREADY_ALLOWED_WIDTH, 0);
                if (DrawYAxisTitle(y.Title, Math.Max(yShiftFromAxis, y.AxisTitleOffset)))
                    extraWidthRequired = 0;
                else
                {
                    // The x axis title, or the bottom of the labels, or the legend, would fall off the bottom of the current canvas.  We need a new canvas with a better size.
                    extraWidthRequired = yShiftFromAxis;
                    succeeded = false;
                }
                if (!succeeded)
                    return false;
            }

            // Draw the chart title now that we know it's safe to do so.
            if (!IsAscii)
                DrawTitle(title);
            extraWidthRequired = 0;
            extraHeightRequired = 0;
            return true;
        }

        protected double DrawXScale(bool drawLabels, ScaleType scaleType, bool useCalculatedScalesEvenWithDefinition)
        {
            double aint = 0;
            string msk = string.Empty;
            double labelHeight = 0;

            if (IsAscii || drawLabels)
            {
                // find a neat axis division
                double amin;
                Q_AxisOrFromDefinition(ref DataMinX, DataMinGreaterThanZeroX, ref DataMaxX, out xDiv, out amin, out aint, out minorTicsPerMajorTic, false, scaleType, useCalculatedScalesEvenWithDefinition);
                // set the X axis min and max values to fit the scale
                xInt = aint;
                axisXMin = amin;
                axisXMax = amin + (aint * xDiv);
                divx = axisXMax - axisXMin;
                offx = -(axisXMin / divx * xExtCanvas) + xAxisCanvas;
                // set a string mask that will fit OK
                msk = AxisMaskOrFromDefinition(aint, amin, xDiv, minorTicsPerMajorTic, false, scaleType, useCalculatedScalesEvenWithDefinition);
            }
            else
            {
                //  Not ASCII, not drawing our own labels
                xDiv = 20;
                minorTicsPerMajorTic = 5;
                xInt = (DataMaxX - DataMinX) / xDiv;
                axisXMin = DataMinX;
                axisXMax = DataMaxX;
            }

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
                    for (int x = 0; x <= xDiv; x++)
                    {
                        double x1 = x / (double)xDiv * xExtCanvas + xAxisCanvas;
                        if (x % minorTicsPerMajorTic != 0)
                        {
                            //  Minor tic
                            switch (scaleType)
                            {
                                case ScaleType.Log10:
                                    // Assume 3 minors per major
                                    double baseValue = axisXMin + (x - (x % minorTicsPerMajorTic)) * aint;
                                    double value = Math.Pow(10, baseValue);
                                    if (x % minorTicsPerMajorTic == 1)
                                        x1 = ToCanvasX(2.0 * value, scaleType);
                                    else if (x % minorTicsPerMajorTic == 2)
                                        x1 = ToCanvasX(5.0 * value, scaleType);
                                    AxisDrawline(x1, yAxisCanvas - AXIS_LITTLE_TICK, x1, yAxisCanvas);
                                    if (hasGridLines && !(drawLabels))
                                        statsDirectCanvas.DrawLine(gridLinePen, x1, yAxisCanvas, x1, yAxisCanvas + yExtCanvas);
                                    break;
                                default:
                                    AxisDrawline(x1, yAxisCanvas - AXIS_LITTLE_TICK, x1, yAxisCanvas);
                                    if (hasGridLines && !(drawLabels))
                                        statsDirectCanvas.DrawLine(gridLinePen, x1, yAxisCanvas, x1, yAxisCanvas + yExtCanvas);
                                    break;
                            }
                        }
                        else
                        {
                            //  Major tic - may or may not be labelled
                            if (drawLabels)
                            {
                                double value = axisXMin + x * aint;
                                //  Un-transform value for non-linear scales
                                switch (scaleType)
                                {
                                    case ScaleType.Log10:
                                        value = Math.Pow(10, value);
                                        break;
                                    case ScaleType.LogNatural:
                                        //  We use powers of 2 for the labels, not powers of e
                                        value = Math.Pow(2, value);
                                        break;
                                        // Else do nothing - other scales are linear
                                }

                                string lab = value.ToString(msk);
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
                    }
                }
            }
            else
            {
                //  ASCII - always linear for now.  TODO: Log
                shTx[ASCII_Ytxt - 1] = string.Empty.PadLeft(13) + "/" + new string('-', 61);
                for (int x = 0; x <= xDiv; x++)
                {
                    if (x % minorTicsPerMajorTic == 0)
                    {
                        string lab = (axisXMin + (x * aint)).ToString(msk);
                        int l = lab.Length;
                        labelHeight = Math.Max(Convert.ToInt32(labelHeight), l);
                        int s = Convert.ToInt32((x * (60 / xDiv)) + 15);
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
            return labelHeight;
        }

        protected void Q_AxisOrFromDefinition(ref double qmin, double qMinGreaterThanZero, ref double qmax, out int div, out double zmin, out double zint, out int minorTicsPerMajorTic, bool isY, ScaleType scaleType, bool useCalculatedScalesEvenWithDefinition)
        {
            if ((definition != null) && definition.HasScaleParameters && !useCalculatedScalesEvenWithDefinition)
            {
                //  Use the values in our scale parameters
                AxisScaleParameters asp = isY ? definition.ScaleParameters.Y : definition.ScaleParameters.X;
                if ((asp != null) && asp.HasAxisScale)
                {
                    qmin = asp.QMin;
                    qmax = asp.QMax;
                    div = asp.Div;
                    zmin = asp.ZMin;
                    zint = asp.ZInt;
                    minorTicsPerMajorTic = asp.MinorTicsPerMajorTic;
                    return;
                }
            }
            //  If we get here, there was no prior definition - calculate it ourselves.
            AxisScaler.Q_Axis(ref qmin, qMinGreaterThanZero, ref qmax, out div, out zmin, out zint, out minorTicsPerMajorTic, scaleType);
        }

        protected string AxisMaskOrFromDefinition(double stepp, double znmin, int nstep, int sp, bool IsY, ScaleType ScaleType, bool UseCalculatedScalesEvenWithDefinition)
        {
            if ((definition != null) && definition.HasScaleParameters && !(UseCalculatedScalesEvenWithDefinition))
            {
                //  Use the values in our scale parameters
                AxisScaleParameters asp = IsY ? definition.ScaleParameters.Y : definition.ScaleParameters.X;
                if ((asp != null) && asp.HasAxisScale)
                    return asp.Mask;
            }
            //  If we get here, there was no prior definition - calculate it ourselves.
            return AxisScaler.AxisMask(stepp, znmin, nstep, sp, ScaleType);
        }

        protected double DrawYScale(bool reverse, ScaleType scaleType, bool useCalculatedScalesEvenWithDefinition)
        {
            const double AXIS_LABEL_OFFSET_FROM_BIG_TICK = 8;

            // find a neat axis division
            double aint, amin;
            Q_AxisOrFromDefinition(ref DataMinY, DataMinGreaterThanZeroY, ref DataMaxY, out yDiv, out amin, out aint, out minorTicsPerMajorTic, true, scaleType, useCalculatedScalesEvenWithDefinition);

            // set the Y axis min and max values to fit the scale
            yInt = aint;
            axisYMin = amin;
            axisYMax = amin + (aint * yDiv);
            divy = axisYMax - axisYMin;
            offy = -(axisYMin / divy * yExtCanvas) + yAxisCanvas;

            // set a string mask that will fit OK
            string msk = AxisMaskOrFromDefinition(aint, amin, yDiv, minorTicsPerMajorTic, true, scaleType, useCalculatedScalesEvenWithDefinition);
            LabelDirection direction = LabelDirection.Across;
            bool hasGridLines = false;
            System.Drawing.Drawing2D.DashStyle gridLineDashStyle = System.Drawing.Drawing2D.DashStyle.Solid;
            if (HasScaleParameters && definition.ScaleParameters.Y != null)
            {
                direction = definition.ScaleParameters.Y.LabelDirection;
                hasGridLines = definition.ScaleParameters.Y.HasGridLines;
                gridLineDashStyle = definition.ScaleParameters.Y.GridLineDashStyle;
            }

            double maxWidth = 0;
            if (!(IsAscii))
            {
                using (Pen gridLinePen = new Pen(axisPen.Color, 1))
                {
                    gridLinePen.DashStyle = gridLineDashStyle;
                    for (int y = 0; y <= yDiv; y++)
                    {
                        double y1 = (y / (double)yDiv * yExtCanvas) + yAxisCanvas;
                        if (y % minorTicsPerMajorTic != 0)
                        {
                            //  Minor tic
                            AxisDrawline(xAxisCanvas - AXIS_LITTLE_TICK, y1, xAxisCanvas, y1);
                        }
                        else
                        {
                            //  Major tic
                            double value;
                            if (reverse)
                                value = axisYMin + ((yDiv - y) * aint);
                            else
                                value = axisYMin + (y * aint);

                            //  Un-transform value for non-linear scales
                            switch (scaleType)
                            {
                                case ScaleType.Log10:
                                    value = Math.Pow(10, value);
                                    break;
                                case ScaleType.LogNatural:
                                    //  We use powers of 2 for the labels, not powers of e
                                    value = Math.Pow(2, value);
                                    break;
                                    // Else do nothing - other scales are linear
                            }


                            string lab = value.ToString(msk);
                            maxWidth = Math.Max(Convert.ToSingle(maxWidth), AxisDrawStringAtAngleRM(lab, xAxisCanvas - (AXIS_BIG_TICK + AXIS_LABEL_OFFSET_FROM_BIG_TICK), y1, direction).Width);
                            AxisDrawline(xAxisCanvas - AXIS_BIG_TICK, y1, xAxisCanvas, y1);
                            if (hasGridLines)
                                statsDirectCanvas.DrawLine(gridLinePen, xAxisCanvas, y1, xAxisCanvas + xExtCanvas, y1);
                        }
                    }
                }
            }
            else
            {
                for (int y = 0; y <= yDiv; y++)
                {
                    if (y % minorTicsPerMajorTic == 0)
                    {
                        string lab = (axisYMin + (y * aint)).ToString(msk);
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
            return maxWidth;
        }


        ///  <summary>
        ///  Draw the Y axis as a series
        ///  </summary>
        ///  <remarks>Labels are drawn centred between ticks</remarks>
        ///  <returns>The extra distance occupied by the labels</returns>
        protected double DrawYSeries(IList<Series> series)
        {
            if (null == series)
                return 0;
            return DrawYSeries(series.Select(s => s.Title).ToList());
        }

        ///  <summary>
        ///  Draw the Y axis as a series
        ///  </summary>
        ///  <remarks>Labels are drawn centred between tics</remarks>
        protected double DrawYSeries(IList<string> labels)
        {
            double maxWidth = 0;
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
                            maxWidth = Math.Max(maxWidth, statsDirectCanvas.DrawStringAtAngle(labels[y], axisLabelFont, axisBrush, xAxisCanvas - (AXIS_BIG_TICK + 3), yctr, txtFormat, direction).Width);
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

            return maxWidth;
        }

        ///  <summary>
        ///  Draw the X axis as a series
        ///  </summary>
        protected double DrawXSeries(IList<Series> series)
        {
            if (null == series)
                return 0;
            return DrawXSeries(series.Select(s => s.Title).ToList());
        }

        ///  <summary>
        ///  Draw the X axis as a series
        ///  </summary>
        protected double DrawXSeries(IList<string> labels)
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
            return maxHeight;
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

        protected void SetStandardAsciiScaling()
        {
            divx = axisXMax - axisXMin;
            offx = SafeToInt32(-(axisXMin / divx * 60) + 15);
            divy = axisYMax - axisYMin;
            offy = SafeToInt32(-(axisYMin / divy * yDiv) + ASCII_Ytxt);
        }

        protected double SafeToInt32(double d)
        {
            if (double.IsNaN(d) || d < int.MinValue || d > Int32.MaxValue)
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

        protected double ToCanvasX(double chartX)
        {
            ScaleType scaleType = ScaleType.Linear;
            if (HasScaleParameters && definition.ScaleParameters.X != null)
                scaleType = definition.ScaleParameters.X.ScaleType;
            return ToCanvasX(chartX, scaleType);
        }

        protected double ToCanvasX(double chartX, ScaleType scaleType)
        {
            double transformed = Transform(chartX, scaleType);
            return offx + (transformed / divx * xExtCanvas);
        }

        protected double TransformX(double chartX)
        {
            if ((!HasScaleParameters || definition.ScaleParameters.X == null))
                return chartX;
            return Transform(chartX, definition.ScaleParameters.X.ScaleType);
        }

        protected double InverseTransformX(double canvasX)
        {
            if ((!HasScaleParameters || definition.ScaleParameters.X == null))
                return canvasX;
            return InverseTransform(canvasX, definition.ScaleParameters.X.ScaleType);
        }

        protected double ToCanvasY(double chartY)
        {
            ScaleType scaleType = ScaleType.Linear;
            if (HasScaleParameters && definition.ScaleParameters.Y != null)
                scaleType = definition.ScaleParameters.Y.ScaleType;
            return ToCanvasY(chartY, scaleType);
        }

        protected double ToCanvasY(double chartY, ScaleType scaleType)
        {
            double transformed = Transform(chartY, scaleType);
            return offy + (transformed / divy * yExtCanvas);
        }

        protected double TransformY(double chartY)
        {
            if ((!HasScaleParameters || definition.ScaleParameters.Y == null))
                return chartY;
            return Transform(chartY, definition.ScaleParameters.Y.ScaleType);
        }

        protected double InverseTransformY(float canvasY)
        {
            if ((!HasScaleParameters || definition.ScaleParameters.Y == null))
                return canvasY;
            return InverseTransform(canvasY, definition.ScaleParameters.Y.ScaleType);
        }

        [Obsolete("Ideally subclasses would never need to use canvas co-ordinates")]
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

        public void DrawLineInChartCoordinates(Color Color, double x1, double y1, double x2, double y2)
        {
            if (mostRecentPen == null || !(mostRecentPen.Color.Equals(Color)))
            {
                if (null != mostRecentPen)
                    mostRecentPen.Dispose();
                mostRecentPen = new Pen(Color);
            }
            double dx1 = ToCanvasX(x1);
            double dy1 = ToCanvasY(y1);
            double dx2 = ToCanvasX(x2);
            double dy2 = ToCanvasY(y2);
            statsDirectCanvas.DrawLine(mostRecentPen, dx1, dy1, dx2, dy2);
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

        protected string EndVectorPlotAndReturnRtf()
        {
            EndVectorPlot();
            return RtfImageRenderer.ImageStreamToRtf(statsDirectCanvas.DetachAndReturnImageStream(), imageWidth, imageHeight);
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

        public abstract ParameterBag Plot(ITemplateHost host);
        public abstract ScaleParameters GetScaleParameters();
    }
}