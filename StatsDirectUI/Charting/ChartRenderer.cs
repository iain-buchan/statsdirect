using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Security.Permissions;
using System.Drawing;
using System.Windows.Forms;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.Templates;
using StatsDirect.Utilities;

namespace StatsDirect.Charting
{
    ///  <summary>
    ///  Converts a chart definition into an ASCII or metafile rendering of that definition.
    ///  </summary>
    public class ChartRenderer : IDisposable
    {
        private readonly double LOG2 = Math.Log(2.0);
        private const int LEGEND_TOP_GAP = 70;
        private const int LEGEND_MARKER_SIZE = 6;
        private const int MINIMUM_LEGEND_GAP = 6;
        private const int LOWEST_ALLOWED_LEGEND = 30;

        ///  <summary>
        ///  Sometimes we need to draw line charts (for example) with their points sorted.
        ///  This comparer sorts PointFs by increasing X, then by increasing Y.
        ///  </summary>
        private class SortXThenY : IComparer<PointF>
        {
            int IComparer<PointF>.Compare(PointF x, PointF y)
            {
                float xDiff = x.X - y.X;
                return xDiff == 0 ? Math.Sign(x.Y - y.Y) : Math.Sign(xDiff);
            }
        }


        //  PUBLIC VARIABLES - users can set these

        ///  <summary>
        ///  If true, a textual representation of the chart is plotted.  If false (default) a metafile is plotted.
        ///  </summary>
        public bool IsAscii;

        //  PRIVATE VARIABLES - callers should be unable to touch anything below here

        private static Font defaultAxisLabelFont;
        private static Font defaultAxisTitleFont;
        private static Font defaultTitleFont;
        private static Font defaultLegendFont;
        private static Font defaultLabelFont;

        private static bool defaultBoxAxes;

        private static bool defaultAllBlack;

        private ChartDefinition definition;

        private Font axisLabelFont;
        private Font axisTitleFont;
        private float axisLineThickness;
        private Pen axisPen;
        private Brush axisBrush;
        private const double AXIS_LITTLE_TICK = 4;
        private const double AXIS_BIG_TICK = 7;
        private Font titleFont;
        private Font legendFont;
        private bool boxAxes = defaultBoxAxes;

        private Font labelFont;

        private Graphics canvas;
        private System.Drawing.Imaging.Metafile metaFile;
        private Stream cachedOutputStream;

        private double dataMinX = double.MaxValue;
        private double dataMaxX = -double.MaxValue;
        private double dataMinY = double.MaxValue;
        private double dataMaxY = -double.MaxValue;
        ///  <summary>
        ///  The minimum value for the X-axis that will eventually be drawn (the neat value)
        ///  </summary>
        private double axisXMin;
        ///  <summary>
        ///  The maximum value for the X-axis that will eventually be drawn (the neat value)
        ///  </summary>
        private double axisXMax;
        ///  <summary>
        ///  The minimum value for the Y-axis that will eventually be drawn (the neat value)
        ///  </summary>
        private double axisYMin;
        ///  <summary>
        ///  The maximum value for the Y-axis that will eventually be drawn (the neat value)
        ///  </summary>
        private double axisYMax;
        private double xInt;
        private double yInt;

        private double xAxisCanvas;
        private double xExtCanvas;
        private int xDiv;
        private double yAxisCanvas;
        private double yExtCanvas;
        private int yDiv;

        private double divx;
        private double offx;
        private double divy;
        private double offy;

        /// <summary>How many minor tics per major tic on the axis?</summary>
        private int minorTicsPerMajorTic;
        private double scaleYAxis = 1.0;
        private double scaleXAxis = 1.0;
        private const double DEFAULT_METAH = 800;
        private const double DEFAULT_METAW = 1132;
        private const double DEFAULT_Y_GAP = 80;
        private double metaH = DEFAULT_METAH;
        private double MetaW = DEFAULT_METAW;
        private const double SCALE_FACTOR = 1.4;
        private const int LABEL_TO_AXIS_LABEL_GAP = 20;

        //  Box and Whisker constants
        private const double BOXWHISKER_WHISKER_END_LENGTH = 9;
        private const double BOXWHISKER_OUTLIER_RADIUS = 4;
        private const double BOXWHISKER_BOX_FRACTION_OF_SPACE = 0.667;
        private const double BOXWHISKER_WHISKER_FRACTION_OF_BOX = 0.333;

        private string[] shTx;

        private static bool AreSharedValuesInitialised;
        private Brush blackBrush;

        private const int ASCII_Ytxt = 3;
        private const int ASCII_XTxt = 15;

        ///  <summary>
        ///  A few methods take a colour, not a pen.  This caches the most recent pen used by those methods, so that it can be re-used rather than regenerated each time.
        ///  </summary>
        private Pen mostRecentPen;

        private static MarkerType[] _markerTypes;

        public static bool DefaultRequestScaleLimits
        {
            get
            {
                return false;
            }
        }

        public static bool DefaultBoxAxes
        {
            get
            {
                if (!(AreSharedValuesInitialised))
                {
                    InitSharedValues();
                }
                return defaultBoxAxes;
            }
            set
            {
                defaultBoxAxes = value;
            }
        }

        public static Font DefaultAxisLabelFont
        {
            get
            {
                if (!(AreSharedValuesInitialised))
                {
                    InitSharedValues();
                }
                return defaultAxisLabelFont;
            }
            set
            {
                defaultAxisLabelFont = value;
            }
        }

        public static Font DefaultSeriesLabelFont
        {
            get
            {
                return DefaultAxisLabelFont;
            }
        }

        public static Font DefaultAxisTitleFont
        {
            get
            {
                if (!(AreSharedValuesInitialised))
                {
                    InitSharedValues();
                }
                return defaultAxisTitleFont;
            }
            set
            {
                defaultAxisTitleFont = value;
            }
        }

        public static Font DefaultLabelFont
        {
            get
            {
                if (!(AreSharedValuesInitialised))
                {
                    InitSharedValues();
                }
                return defaultLabelFont;
            }
            set
            {
                defaultLabelFont = value;
            }
        }

        public static Font DefaultLegendFont
        {
            get
            {
                if (!(AreSharedValuesInitialised))
                {
                    InitSharedValues();
                }
                return defaultLegendFont;
            }
            set
            {
                defaultLegendFont = value;
            }
        }

        public static Font DefaultTitleFont
        {
            get
            {
                if (!(AreSharedValuesInitialised))
                {
                    InitSharedValues();
                }
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
                {
                    InitSharedValues();
                }
                return _markerTypes;
            }
        }

        private static void InitSharedValues()
        {
            AreSharedValuesInitialised = true; //  Set early to prevent recursively trying to initialise properties when saving them
            InitMarkerTypes();
            InitFonts();
            InitFlags();
        }

        public double DataMinX
        {
            get
            {
                return dataMinX;
            }
            set
            {
                dataMinX = value;
            }
        }

        public double DataMaxX
        {
            get
            {
                return dataMaxX;
            }
            set
            {
                dataMaxX = value;
            }
        }

        public double DataMinY
        {
            get
            {
                return dataMinY;
            }
            set
            {
                dataMinY = value;
            }
        }

        public double DataMaxY
        {
            get
            {
                return dataMaxY;
            }
            set
            {
                dataMaxY = value;
            }
        }

        public ChartRenderer(ChartDefinition Definition)
        {
            definition = Definition;
            if (Definition == null)
                return;
            dataMinX = Definition.DataMinX;
            dataMaxX = Definition.DataMaxX;
            dataMinY = Definition.DataMinY;
            dataMaxY = Definition.DataMaxY;
        }

        public ScaleParameters GetScaleParameters()
        {
            switch (definition.ChartType)
            {
                case ChartType.AgreementPair:
                    return GetAgreementPairScaleParameters();
                case ChartType.Bar:
                case ChartType.StackedBar:
                case ChartType.StackedBar100Percent:
                    return GetBarScaleParameters();
                case ChartType.BoxWhisker:
                    return GetBoxWhiskerScaleParameters();
                case ChartType.Control:
                    return GetControlScaleParameters();
                case ChartType.ErrorBar:
                    return GetErrorBarScaleParameters();
                case ChartType.Forest:
                    return GetForestScaleParameters();
                case ChartType.Gini:
                    return GetGiniScaleParameters();
                case ChartType.Histogram:
                    return GetHistogramScaleParameters();
                case ChartType.Ladder:
                    return GetLadderScaleParameters();
                case ChartType.LineXY:
                    return GetScatterScaleParameters();
                case ChartType.LinearRegression:
                    return GetLinearRegressionScaleParameters();
                case ChartType.Normal:
                    return GetNormalScaleParameters();
                case ChartType.Pyramid:
                    return GetPyramidScaleParameters();
                case ChartType.ROC:
                    return GetRocScaleParameters();
                case ChartType.ScatterXY:
                    return GetScatterScaleParameters();
                case ChartType.Spread:
                    return GetSpreadScaleParameters();
                case ChartType.Survival:
                    return GetSurvivalScaleParameters();
                default:
                    throw new NotImplementedException("That chart type is not yet implemented");
            }

        }

        public ParameterBag PlotAndReturnRtf(ITemplateHost host, out string rtf)
        {
            using (MemoryStream metaStream = new MemoryStream())
            {
                ParameterBag results = Plot(metaStream, host);
                rtf = host.ImageStreamToRtf(metaStream);
                return results;
            }
        }

        ///  <summary>
        ///  Plot a chart.
        ///  </summary>
        ///  <returns>Any output parameters created as side-effects of the plotting</returns>
        ///  <remarks>Postcondition: Another plot can be called on the same chart object and give the same results.  This is required for previewing.</remarks>
        public ParameterBag Plot(Stream outputStream, ITemplateHost host)
        {
            switch (definition.ChartType)
            {
                case ChartType.AgreementPair:
                    return PlotAgreementPair(outputStream);
                case ChartType.Bar:
                case ChartType.StackedBar:
                case ChartType.StackedBar100Percent:
                    return PlotBar(outputStream);
                case ChartType.BoxWhisker:
                    return PlotBoxWhisker(outputStream);
                case ChartType.Control:
                    return PlotControl(outputStream);
                case ChartType.ErrorBar:
                    return PlotErrorBar(outputStream);
                case ChartType.Forest:
                    return PlotForest(outputStream);
                case ChartType.Gini:
                    return PlotGini(outputStream);
                case ChartType.Histogram:
                    return PlotHistogram(outputStream);
                case ChartType.Ladder:
                    return PlotLadder(outputStream);
                case ChartType.LineXY:
                    return PlotScatter(outputStream, true);
                case ChartType.LinearRegression:
                    return PlotLinearRegression(outputStream);
                case ChartType.Normal:
                    return PlotNormal(outputStream, host);
                case ChartType.Pyramid:
                    return PlotPyramid(outputStream);
                case ChartType.ROC:
                    return PlotROC(outputStream, host);
                case ChartType.ScatterXY:
                    return PlotScatter(outputStream, false);
                case ChartType.Spread:
                    return PlotSpread(outputStream, host);
                case ChartType.Survival:
                    return PlotSurvival(outputStream);
                default:
                    throw new NotImplementedException("That chart type is not yet implemented");
            }

        }

        private static void InitFlags()
        {
            defaultBoxAxes = Settings1.Default.BoxAxes;
            defaultAllBlack = Settings1.Default.BlackAndWhite;
        }

        private static void InitFonts()
        {
            //  Title
            string savedTitleFont = Settings1.Default.TitleFont;

            if (savedTitleFont == null || savedTitleFont.Length < 3)
            {
                InitFirstFonts();
            }
            else
            {
                DefaultTitleFont = FontFromSaveString(savedTitleFont);
                string savedLabelFont = Settings1.Default.LabelFont;
                DefaultAxisLabelFont = FontFromSaveString(savedLabelFont);
                DefaultAxisTitleFont = FontFromSaveString(savedLabelFont);
                DefaultLabelFont = FontFromSaveString(savedLabelFont);
                DefaultLegendFont = FontFromSaveString(savedLabelFont);
            }
        }

        public static Font FontFromSaveString(string Descriptor)
        {
            string[] fontStrings = Descriptor.Split(';');
            string fontString = fontStrings[0];
            FontStyle style = ((FontStyle)(int.Parse(fontStrings[1])));
            float sz = float.Parse(fontStrings[2]);
            return new Font(fontString, sz, style);
        }

        public static string SaveStringFromFont(Font f)
        {
            return f.FontFamily.Name + ";" + (Convert.ToInt32(f.Style)).ToString() + ";" + f.Size.ToString();
        }

        private static void InitFirstFonts()
        {
            //  Default fonts, in case there are no preferences
            DefaultAxisLabelFont = new Font("Calibri", Convert.ToSingle(11 * SCALE_FACTOR), FontStyle.Regular);
            DefaultAxisTitleFont = new Font("Calibri", Convert.ToSingle(11 * SCALE_FACTOR), FontStyle.Bold);
            DefaultLabelFont = new Font("Calibri", Convert.ToSingle(11 * SCALE_FACTOR), FontStyle.Regular);
            DefaultLegendFont = new Font("Calibri", Convert.ToSingle(11 * SCALE_FACTOR), FontStyle.Regular);
            DefaultTitleFont = new Font("Calibri", Convert.ToSingle(16 * SCALE_FACTOR), FontStyle.Bold);
            SaveFonts();
        }

        ///  <summary>
        ///  Initialise the marker types from persistent storage or (if none) from defaults
        ///  </summary>
        ///  <remarks></remarks>
        private static void InitMarkerTypes()
        {
            _markerTypes = new MarkerType[11];
            for (int i = _markerTypes.GetLowerBound(0); i <= _markerTypes.GetUpperBound(0); i++)
            {
                _markerTypes[i] = new MarkerType();
            }

            string savedSettings = Settings1.Default.Markers;

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
                    {
                        isFilled = "1".Equals(parameterStrings[4]);
                    }
                    //  Marker size
                    int markerSize = 0;
                    if (parameterStrings.Length > 5)
                    {
                        int.TryParse(parameterStrings[5], out markerSize);
                    }
                    if (markerSize <= 0)
                    {
                        markerSize = 6;
                    }
                    _markerTypes[i].Color = col;
                    _markerTypes[i].IsFilled = isFilled;
                    _markerTypes[i].MarkerSize = markerSize;
                    _markerTypes[i].Shape = shape;
                    _markerTypes[i].Style = style;
                    _markerTypes[i].Width = width;
                }

                // fixed style
                _markerTypes[10].Shape = MarkerShape.Circle;
                _markerTypes[10].Color = Color.Black;
                _markerTypes[10].Width = 1;
                _markerTypes[10].Style = System.Drawing.Drawing2D.DashStyle.Dash;
                _markerTypes[10].IsFilled = false;
                _markerTypes[10].MarkerSize = 6;
            }
        }

        public static void SaveFlags()
        {
            Settings1.Default.BlackAndWhite = defaultAllBlack;
            Settings1.Default.BoxAxes = defaultBoxAxes;

            SaveSettings(Settings1.Default);
        }

        public static void SaveFonts()
        {
            Settings1.Default.LabelFont = SaveStringFromFont(DefaultLabelFont);
            Settings1.Default.TitleFont = SaveStringFromFont(DefaultTitleFont);

            SaveSettings(Settings1.Default);
        }

        public static void SaveMarkerTypes()
        {
            System.Text.StringBuilder savedSettings = new System.Text.StringBuilder();
            for (int i = 0; i <= 9; i++)
            {
                if (i > 0)
                {
                    savedSettings.Append("|");
                }
                //  Shape
                savedSettings.Append(Convert.ToInt32(_markerTypes[i].Shape).ToString());
                savedSettings.Append(";");

                //  Colour
                Color col = _markerTypes[i].Color;
                savedSettings.Append(col.R.ToString());
                savedSettings.Append(",");
                savedSettings.Append(col.G.ToString());
                savedSettings.Append(",");
                savedSettings.Append(col.B.ToString());
                savedSettings.Append(";");
                savedSettings.Append(_markerTypes[i].Width.ToString());
                savedSettings.Append(";");

                //  Line style
                savedSettings.Append(Convert.ToInt32(_markerTypes[i].Style).ToString());
                savedSettings.Append(";");

                //  Filled (1/0)
                savedSettings.Append(_markerTypes[i].IsFilled ? "1" : "0");
                savedSettings.Append(";");

                //  Marker size
                savedSettings.Append(_markerTypes[i].MarkerSize.ToString());
            }
            Settings1.Default.Markers = savedSettings.ToString();
            SaveSettings(Settings1.Default);
        }

        private static void InitFirstMarkerTypes()
        {
            _markerTypes[0].Shape = MarkerShape.Circle;
            _markerTypes[0].Color = Color.FromArgb(64, 105, 156); // Color.Red
            _markerTypes[0].Style = System.Drawing.Drawing2D.DashStyle.Solid;

            _markerTypes[1].Shape = MarkerShape.Square;
            _markerTypes[1].Color = Color.FromArgb(158, 65, 62); // Color.Green
            _markerTypes[1].Style = System.Drawing.Drawing2D.DashStyle.Dash;

            _markerTypes[2].Shape = MarkerShape.Triangle;
            _markerTypes[2].Color = Color.FromArgb(127, 154, 72); // Color.Blue
            _markerTypes[2].Style = System.Drawing.Drawing2D.DashStyle.Dot;

            _markerTypes[3].Shape = MarkerShape.Plus;
            _markerTypes[3].Color = Color.FromArgb(105, 81, 133); // Color.Black
            _markerTypes[3].Style = System.Drawing.Drawing2D.DashStyle.DashDot;

            _markerTypes[4].Shape = MarkerShape.Cross;
            _markerTypes[4].Color = Color.FromArgb(60, 141, 163); // Color.Cyan
            _markerTypes[4].Style = System.Drawing.Drawing2D.DashStyle.Solid;

            _markerTypes[5].Shape = MarkerShape.CircleLine;
            _markerTypes[5].Color = Color.FromArgb(204, 123, 56); // Color.Magenta
            _markerTypes[5].Style = System.Drawing.Drawing2D.DashStyle.Dash;

            _markerTypes[6].Shape = MarkerShape.SquareLine;
            _markerTypes[6].Color = Color.FromArgb(79, 129, 189); // Color.Yellow
            _markerTypes[6].Style = System.Drawing.Drawing2D.DashStyle.Dot;

            _markerTypes[7].Shape = MarkerShape.SquareCross;
            _markerTypes[7].Color = Color.FromArgb(192, 80, 77); // Color.Red
            _markerTypes[7].Style = System.Drawing.Drawing2D.DashStyle.DashDot;

            _markerTypes[8].Shape = MarkerShape.Circle;
            _markerTypes[8].Color = Color.FromArgb(155, 187, 89); // Color.Green
            _markerTypes[8].Style = System.Drawing.Drawing2D.DashStyle.Solid;

            _markerTypes[9].Shape = MarkerShape.Square;
            _markerTypes[9].Color = Color.FromArgb(128, 100, 162); //  Color.Cyan
            _markerTypes[9].Style = System.Drawing.Drawing2D.DashStyle.Dash;

            // fixed style
            _markerTypes[10].Shape = MarkerShape.Circle;
            _markerTypes[10].Color = Color.Black;
            _markerTypes[10].Style = System.Drawing.Drawing2D.DashStyle.Dash;

            foreach (MarkerType mt in _markerTypes)
            {
                mt.Width = 1;
                mt.MarkerSize = 6;
            }
            SaveMarkerTypes();
        }

        private static void SaveSettings(Settings1 s)
        {
            IsolatedStorageFilePermission p = new IsolatedStorageFilePermission(PermissionState.Unrestricted);
            try
            {
                p.Assert();
                s.Save();
            }
            finally
            {
                CodeAccessPermission.RevertAssert();
            }
        }

        ///  <summary>
        ///  Set up some appropriate default axes.
        ///  </summary>
        ///  <remarks>
        ///  The X axis uses 80% of the width and is offset by a few percent to the right
        ///  The Y axis is centred and uses 75% of the height
        ///  </remarks>
        private void DefaultAxes()
        {
            //  xaxis also needs to be reset in routines with legends
            xAxisCanvas = MetaW / 7.55;
            yAxisCanvas = Math.Min(metaH / 8, DEFAULT_Y_GAP);
            xExtCanvas = MetaW / 1.25 * scaleXAxis;
            yExtCanvas = metaH - Math.Min(metaH / 4, 2 * DEFAULT_Y_GAP * scaleYAxis);
        }

        ///  <summary>
        ///  Prepare to plot a metafile chart to the specified stream.
        ///  </summary>
        ///  <remarks></remarks>
        private void StartMetafile(Stream OutputStream, bool ShouldDefaultAxes)
        {
            // Initialise scaling and resources
            IsAscii = false;
            if (ShouldDefaultAxes)
            {
                DefaultAxes();
            }
            if (!(AreSharedValuesInitialised))
            {
                InitSharedValues();
            }

            //  Drawing objects
            axisLabelFont = DefaultAxisLabelFont;
            axisPen = new Pen(grAxis, 1);
            axisTitleFont = DefaultAxisTitleFont;
            axisBrush = new SolidBrush(Color.Black);
            labelFont = DefaultLabelFont;
            legendFont = DefaultLegendFont;
            titleFont = DefaultTitleFont;
            blackBrush = new SolidBrush(Color.Black);

            cachedOutputStream = OutputStream;
            SetupGraphics();
        }

        private void StartMetafile(Stream OutputStream)
        {
            StartMetafile(OutputStream, true);
        }

        private void SetupGraphics()
        {
            if (cachedOutputStream != null)
            {
                //  Create temporary graphics object for metafile creation and get handle to its device context.
                //  Dim newGraphics As Graphics = Graphics.FromImage(New Bitmap(CInt(MetaW), CInt(MetaH), Imaging.PixelFormat.Format32bppArgb))
                using (Bitmap b = new Bitmap(1, 1, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                {
                    using (Graphics newGraphics = Graphics.FromImage(b))
                    {
                        IntPtr hdc = newGraphics.GetHdc();
                        //  Create metafile object to record.
                        cachedOutputStream.Position = 0; //  Just in case we're resetting an earlier metafile output
                        metaFile = new System.Drawing.Imaging.Metafile(cachedOutputStream, hdc, new RectangleF(0, 0, Convert.ToSingle(MetaW), Convert.ToSingle(metaH)), System.Drawing.Imaging.MetafileFrameUnit.Pixel, System.Drawing.Imaging.EmfType.EmfPlusDual);
                        //  Create graphics object to record metaFile.
                        canvas = Graphics.FromImage(metaFile);
                        canvas.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                        //  Release handle to scratch device context.
                        newGraphics.ReleaseHdc(hdc);
                        //  Dispose of scratch graphics object.
                    }
                }
            }
        }

        ///  <summary>
        ///  Stop plotting a metafile chart and release resources.
        ///  </summary>
        ///  <remarks></remarks>
        private void EndMetafile()
        {
            //  Series are kept in case of redoing a preview.

            if ((canvas != null))
            {
                canvas.Dispose();
                canvas = null;
            }
            if ((metaFile != null))
            {
                metaFile.Dispose();
                metaFile = null;
            }
            if ((axisLabelFont != null))
            {
                if (axisLabelFont != DefaultAxisLabelFont)
                {
                    axisLabelFont.Dispose();
                }
                axisLabelFont = null;
            }
            if ((axisTitleFont != null))
            {
                if (axisTitleFont != DefaultAxisTitleFont)
                {
                    axisTitleFont.Dispose();
                }
                axisTitleFont = null;
            }
            if ((axisPen != null))
            {
                axisPen.Dispose();
                axisPen = null;
            }
            if ((axisBrush != null))
            {
                axisBrush.Dispose();
                axisBrush = null;
            }
            if ((titleFont != null))
            {
                if (titleFont != DefaultTitleFont)
                {
                    titleFont.Dispose();
                }
                titleFont = null;
            }
            if ((legendFont != null))
            {
                if (legendFont != DefaultLegendFont)
                {
                    legendFont.Dispose();
                }
                legendFont = null;
            }
            if ((labelFont != null))
            {
                if (labelFont != DefaultLabelFont)
                {
                    labelFont.Dispose();
                }
                labelFont = null;
            }
            if ((blackBrush != null))
            {
                blackBrush.Dispose();
                blackBrush = null;
            }
            if ((mostRecentPen != null))
            {
                mostRecentPen.Dispose();
                mostRecentPen = null;
            }
            cachedOutputStream = null;
        }

        private void DrawTitle(string title)
        {
            using (StringFormat txtFormat = new StringFormat())
            {
                txtFormat.Alignment = StringAlignment.Center;
                DrawString(title, titleFont, Brushes.Black, (xExtCanvas / 2) + xAxisCanvas, yAxisCanvas + yExtCanvas + 60 - titleFont.Height * 0.5F, txtFormat);
            }
        }

        private void DrawString(string s, Font font, Brush brush, double x, double y, StringFormat txtFormat)
        {
            canvas.DrawString(s, font, brush, Convert.ToSingle(x), Convert.ToSingle(metaH - y), txtFormat);
        }

        private float DirectionToAngle(LabelDirection Direction)
        {
            switch (Direction)
            {
                case LabelDirection.Across:
                    return 0.0F;
                case LabelDirection.Up:
                    return -90.0F;
                case LabelDirection.Down:
                    return 90.0F;
                case LabelDirection.SlopeUp:
                    return -45.0F;
                case LabelDirection.SlopeDown:
                    return 45.0F;
            }

            return 0;
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
        ///  <remarks></remarks>
        private SizeF DrawStringAtAngle(string s, Font font, Brush brush, double x, double y, StringFormat txtFormat, LabelDirection direction)
        {
            //  Work out how to fiddle the text alignment
            if (txtFormat.LineAlignment == StringAlignment.Center && txtFormat.Alignment == StringAlignment.Far)
            {
                //  Middle-right: Vertical text needs fiddling, otherwise we're OK.
                if (direction == LabelDirection.Up)
                {
                    txtFormat = ((StringFormat)(txtFormat.Clone()));
                    txtFormat.LineAlignment = StringAlignment.Far;
                    txtFormat.Alignment = StringAlignment.Center;
                }
                else if (direction == LabelDirection.Down)
                {
                    txtFormat = ((StringFormat)(txtFormat.Clone()));
                    txtFormat.LineAlignment = StringAlignment.Near;
                    txtFormat.Alignment = StringAlignment.Center;
                }
            }
            else if (txtFormat.LineAlignment == StringAlignment.Near && txtFormat.Alignment == StringAlignment.Center)
            {
                //  Top-centre: Anything other than across needs fiddling.
                if (direction == LabelDirection.Down || direction == LabelDirection.SlopeDown)
                {
                    txtFormat = ((StringFormat)(txtFormat.Clone()));
                    txtFormat.LineAlignment = StringAlignment.Center;
                    txtFormat.Alignment = StringAlignment.Near;
                }
                else if (direction == LabelDirection.SlopeUp || direction == LabelDirection.Up)
                {
                    txtFormat = ((StringFormat)(txtFormat.Clone()));
                    txtFormat.LineAlignment = StringAlignment.Center;
                    txtFormat.Alignment = StringAlignment.Far;
                }
            }
            float angle = DirectionToAngle(direction);
            canvas.TranslateTransform(Convert.ToSingle(x), Convert.ToSingle(metaH - y));
            canvas.RotateTransform(angle);
            canvas.DrawString(s, font, brush, 0, 0, txtFormat);
            canvas.ResetTransform();
            SizeF uprightSize = canvas.MeasureString(s, font);
            SizeF boundingSize = ToBoundingSize(uprightSize, direction);
            return boundingSize;
        }

        public SizeF ToBoundingSize(SizeF uprightSize, LabelDirection Direction)
        {
            switch (Direction)
            {
                case LabelDirection.Across:
                    return uprightSize;
                case LabelDirection.Up:
                case LabelDirection.Down:
                    return new SizeF(uprightSize.Height, uprightSize.Width);
                case LabelDirection.SlopeDown:
                case LabelDirection.SlopeUp:
                    float diagonal = Convert.ToSingle((uprightSize.Width + uprightSize.Height) * Math.Sin(Math.PI / 4.0));
                    return new SizeF(diagonal, diagonal);
            }

            return new SizeF();
        }

        public void DrawXAxisTitle(string title)
        {
            if (!(string.IsNullOrEmpty(title)))
                DrawXAxisTitle(title, axisLabelFont.Height);
        }

        public void DrawXAxisTitle(string title, double gapForAxisLabels)
        {
            if (!(string.IsNullOrEmpty(title)))
            {
                using (StringFormat txtFormat = new StringFormat())
                {
                    txtFormat.Alignment = StringAlignment.Center;
                    DrawString(title, axisTitleFont, Brushes.Black, (xExtCanvas / 2) + xAxisCanvas, yAxisCanvas - AXIS_BIG_TICK - gapForAxisLabels - axisTitleFont.Height * 0.5 - LABEL_TO_AXIS_LABEL_GAP, txtFormat);
                }
            }
        }

        private void DrawVerticalAxisLabel(string text, StringAlignment alignment, double x, double y)
        {
            using (StringFormat txtFormat = new StringFormat())
            {
                txtFormat.Alignment = alignment; // StringAlignment.Near;
                canvas.TranslateTransform(Convert.ToSingle(x), Convert.ToSingle(metaH - y));
                canvas.RotateTransform(-90.0F);
                canvas.DrawString(text, axisLabelFont, Brushes.Black, 0, 0, txtFormat);
                canvas.ResetTransform();
            }
        }

        public void DrawYAxisTitle(string title, double yShift)
        {
            if (!(string.IsNullOrEmpty(title)))
            {
                using (StringFormat txtFormat = new StringFormat())
                {
                    txtFormat.Alignment = StringAlignment.Center;
                    canvas.TranslateTransform(Convert.ToSingle(xAxisCanvas - axisLabelFont.Height - AXIS_BIG_TICK - yShift - LABEL_TO_AXIS_LABEL_GAP), Convert.ToSingle(metaH - (yExtCanvas / 2 + yAxisCanvas)));
                    canvas.RotateTransform(-90.0F);
                    canvas.DrawString(title, axisTitleFont, Brushes.Black, 0, 0, txtFormat);
                    canvas.ResetTransform();
                }
            }
        }

        ///  <summary>
        ///  Draw the axes and chart title.
        ///  </summary>
        ///  <param name="title">The title of the chart</param>
        ///  <param name="x">The axis definition for the X-axis</param>
        ///  <param name="y">The axis definition for the Y-axis</param>
        /// <param name="shouldBoxAxes"></param>
        /// <param name="shouldDefaultAxes">If true, DefaultAxes() is called before the axes are drawn.</param>
        /// <param name="useCalculatedScalesEvenWithDefinition"></param>
        /// <remarks>Axis modes used to be:
        ///  1: x=scale, y=series, no box
        ///  2: y=scale, x=series, no box
        ///  3: x=scale, y=scale, no box
        ///  4: x=scale, y=blank
        ///  5: y=scale, x=blank
        ///  6: x=line, y=blank
        ///  7: x=scale, y=scale, boxed
        ///  8: x=scale, y=series, boxed
        ///  9: y=scale, x=series, boxed
        ///  23: x=scale, y=reverse-scale</remarks>
        private void DrawAxes(string title, Axis x, Axis y, bool shouldBoxAxes, bool shouldDefaultAxes, bool useCalculatedScalesEvenWithDefinition)
        {
            const int ALREADY_ALLOWED_HEIGHT = 30;

            if (!(IsAscii))
            {
                if (shouldDefaultAxes)
                {
                    DefaultAxes();
                }
                xAxisCanvas += y.ExtraSpace;
                xExtCanvas -= y.ExtraSpace;
                yAxisCanvas += x.ExtraSpace;
                yExtCanvas -= x.ExtraSpace;

                // Draw the title.  Don't draw the axis titles until we know how much we might have to move them.
                DrawTitle(title);

                // Draw the axes
                switch (x.Mode)
                {
                    case AxisMode.LineOnly:
                    case AxisMode.Scale:
                    case AxisMode.Series:
                    case AxisMode.ReverseScale:
                        AxisDrawline(xAxisCanvas, yAxisCanvas, xAxisCanvas + xExtCanvas, yAxisCanvas);
                        break;
                }

                switch (y.Mode)
                {
                    case AxisMode.LineOnly:
                    case AxisMode.Scale:
                    case AxisMode.Series:
                    case AxisMode.ReverseScale:
                        AxisDrawline(xAxisCanvas, yAxisCanvas + yExtCanvas, xAxisCanvas, yAxisCanvas);
                        break;
                }

                if (shouldBoxAxes)
                {
                    // Boxed in, assume both drawn
                    AxisDrawline(xAxisCanvas + xExtCanvas, yAxisCanvas + yExtCanvas, xAxisCanvas, yAxisCanvas + yExtCanvas);
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
            double yShift = y.AxisTitleOffset;
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
                    xHeight = DrawXScale(true, x.ScaleType, useCalculatedScalesEvenWithDefinition);
                    break;
                case AxisMode.ScaleWithoutLabels:
                    xHeight = DrawXScale(false, x.ScaleType, useCalculatedScalesEvenWithDefinition);
                    break;
                case AxisMode.Series:
                    xHeight = DrawXSeries();
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
                    yShift = DrawYScale(true, y.ScaleType, useCalculatedScalesEvenWithDefinition);
                    break;
                case AxisMode.Scale:
                    yShift = DrawYScale(false, y.ScaleType, useCalculatedScalesEvenWithDefinition);
                    break;
                case AxisMode.ScaleWithoutLabels:
                    throw new ArgumentException("A Y scale without labels is not currently supported");
                case AxisMode.Series:
                    yShift = DrawYSeries();
                    break;
            }


            //  We now know by how much we might have to shift the titles
            if (!(IsAscii))
            {
                double xShiftFromAxis = Math.Max(xHeight - ALREADY_ALLOWED_HEIGHT, 0);
                DrawXAxisTitle(x.Title, Math.Max(xShiftFromAxis, x.AxisTitleOffset));
                DrawYAxisTitle(y.Title, yShift);
            }

        }

        private double DrawXScale(bool drawLabels, ScaleType scaleType, bool useCalculatedScalesEvenWithDefinition)
        {
            double aint = 0, amin = 0;
            string msk = "";
            double labelHeight = 0;

            if (IsAscii || drawLabels)
            {
                // find a neat axis division
                Q_AxisOrFromDefinition(ref dataMinX, ref dataMaxX, out xDiv, ref amin, ref aint, out minorTicsPerMajorTic, false, scaleType, useCalculatedScalesEvenWithDefinition);
                // set the X axis min and max values to fit the scale
                xInt = aint;
                axisXMin = amin;
                axisXMax = amin + (aint * xDiv);
                // set a string mask that will fit OK
                msk = AxisMaskOrFromDefinition(aint, amin, xDiv, minorTicsPerMajorTic, false, scaleType, useCalculatedScalesEvenWithDefinition);
            }
            else
            {
                //  Not ASCII, not drawing our own labels
                xDiv = 20;
                minorTicsPerMajorTic = 5;
                xInt = (dataMaxX - dataMinX) / Convert.ToDouble(xDiv);
                axisXMin = dataMinX;
                axisXMax = dataMaxX;
            }

            if (!(IsAscii))
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
                        double x1 = x / (double) xDiv * xExtCanvas + xAxisCanvas;
                        if (x % minorTicsPerMajorTic != 0)
                        {
                            //  Minor tic
                            AxisDrawline(x1, yAxisCanvas - AXIS_LITTLE_TICK, x1, yAxisCanvas);
                            if (hasGridLines && !(drawLabels))
                            {
                                DrawLine(gridLinePen, x1, yAxisCanvas, x1, yAxisCanvas + yExtCanvas);
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
                            {
                                DrawLine(gridLinePen, x1, yAxisCanvas, x1, yAxisCanvas + yExtCanvas);
                            }
                        }
                    }
                }
            }
            else
            {
                //  ASCII - always linear for now.  TODO: Log
                shTx[ASCII_Ytxt - 1] = String.Empty.PadLeft(13) + "/" + new string('-', 61);
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
                        {
                            s2 = s - 1;
                        }
                        else { s2 = s; }
                        WriteAsciiYX(ASCII_Ytxt - 2, s2, lab);
                        WriteAsciiYX(ASCII_Ytxt - 1, s, "+");
                    }
                }
            }
            return labelHeight;
        }

        private void Q_AxisOrFromDefinition(ref double qmin, ref double qmax, out int div, ref double zmin, ref double zint, out int minorTicsPerMajorTic, bool isY, ScaleType scaleType, bool useCalculatedScalesEvenWithDefinition)
        {
            if ((definition != null) && definition.HasScaleParameters && !(useCalculatedScalesEvenWithDefinition))
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
            AxisScaler.Q_Axis(ref qmin, ref qmax, out div, ref zmin, ref zint, out minorTicsPerMajorTic, scaleType);
        }

        private string AxisMaskOrFromDefinition(double stepp, double znmin, int nstep, int sp, bool IsY, ScaleType ScaleType, bool UseCalculatedScalesEvenWithDefinition)
        {
            if ((definition != null) && definition.HasScaleParameters && !(UseCalculatedScalesEvenWithDefinition))
            {
                //  Use the values in our scale parameters
                AxisScaleParameters asp = IsY ? definition.ScaleParameters.Y : definition.ScaleParameters.X;
                if ((asp != null) && asp.HasAxisScale)
                {
                    return asp.Mask;
                }
            }
            //  If we get here, there was no prior definition - calculate it ourselves.
            return AxisScaler.AxisMask(stepp, znmin, nstep, sp, ScaleType);
        }

        private double DrawYScale(bool reverse, ScaleType scaleType, bool useCalculatedScalesEvenWithDefinition)
        {
            const double AXIS_LABEL_OFFSET_FROM_BIG_TICK = 8;

            // find a neat axis division
            double aint = 0, amin = 0;
            Q_AxisOrFromDefinition(ref dataMinY, ref dataMaxY, out yDiv, ref amin, ref aint, out minorTicsPerMajorTic, true, scaleType, useCalculatedScalesEvenWithDefinition);

            // set the Y axis min and max values to fit the scale
            yInt = aint;
            axisYMin = amin;
            axisYMax = amin + (aint * yDiv);

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
                        double y1 = (y / (double) yDiv * yExtCanvas) + yAxisCanvas;
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
                            {
                                value = axisYMin + (Convert.ToDouble(yDiv - y) * aint);
                            }
                            else
                            {
                                value = axisYMin + (Convert.ToDouble(y) * aint);
                            }
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
                            {
                                DrawLine(gridLinePen, xAxisCanvas, y1, xAxisCanvas + xExtCanvas, y1);
                            }
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
        private double DrawYSeries()
        {
            double maxWidth = 0;
            if (!(IsAscii))
            {
                //  Vector
                using (StringFormat txtFormat = new StringFormat())
                {
                    txtFormat.Alignment = StringAlignment.Far;
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

                        double yoff = axisLabelFont.Height / 2.0;
                        double count = Convert.ToDouble(definition.YSeries.Count);
                        for (int y = 0; y <= definition.YSeries.Count - 1; y++)
                        {
                            double yctr = yAxisCanvas + yExtCanvas - ((y + 0.5) / count * yExtCanvas);
                            double ytic = yAxisCanvas + yExtCanvas - (y / count * yExtCanvas);
                            //  TODO: There's an error here where MetaH is not default.  The string is not offset by MetaH, leading to the strings being drawn in an unexpected order.
                            //  However, as all the charts in here accommodate that order, this hasn't been fixed!  PJC 2009/12/22
                            float angle = DirectionToAngle(direction);
                            canvas.TranslateTransform(Convert.ToSingle(xAxisCanvas - (AXIS_BIG_TICK + 3)), Convert.ToSingle(yctr - yoff));
                            canvas.RotateTransform(angle);
                            canvas.DrawString(definition.YSeries[y].Title, axisLabelFont, axisBrush, 0, 0, txtFormat);
                            canvas.ResetTransform();
                            SizeF uprightSize = canvas.MeasureString(definition.YSeries[y].Title, axisLabelFont);
                            SizeF boundingSize = ToBoundingSize(uprightSize, direction);
                            maxWidth = Math.Max(Convert.ToSingle(maxWidth), boundingSize.Width);
                            AxisDrawline(xAxisCanvas - AXIS_BIG_TICK, ytic, xAxisCanvas, ytic);
                            if (hasGridLines)
                            {
                                DrawLine(gridLinePen, xAxisCanvas, ytic, xAxisCanvas + xExtCanvas, ytic);
                            }
                        }
                    }
                }
            }
            else
            {
                //  ASCII
                for (int y = 0; y <= definition.YSeries.Count - 1; y++)
                {
                    int Y2 = 3 + y * 2;
                    int L = definition.YSeries[y].Title.Length;
                    int q = 13 - L;
                    if (L >= 13)
                        q = 1;
                    WriteAsciiYX(Y2, q, definition.YSeries[y].Title.Substring(0, 13));
                    WriteAsciiYX(Y2, 14, "|");
                    WriteAsciiYX(Y2 + 1, 14, "+");
                }
            }
            return maxWidth;
        }

        ///  <summary>
        ///  Draw the Y axis as a series
        ///  </summary>
        ///  <remarks>Labels are drawn centred between tics</remarks>
        private double DrawYSeries(IList<string> labels)
        {
            double maxWidth = 0;
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
                    for (int y = 0; y <= labels.Count - 1; y++)
                    {
                        double yctr = yAxisCanvas + yExtCanvas - ((y + 0.5) / count * yExtCanvas);
                        double ytic = yAxisCanvas + yExtCanvas - (y / count * yExtCanvas);
                        maxWidth = Math.Max(Convert.ToSingle(maxWidth), DrawStringAtAngle(labels[y], axisLabelFont, axisBrush, xAxisCanvas - (AXIS_BIG_TICK + 3), yctr, txtFormat, direction).Width);
                        AxisDrawline(xAxisCanvas - AXIS_BIG_TICK, ytic, xAxisCanvas, ytic);
                        if (hasGridLines)
                        {
                            DrawLine(gridLinePen, xAxisCanvas, ytic, xAxisCanvas + xExtCanvas, ytic);
                        }
                    }
                }
                return maxWidth;
            }
        }

        ///  <summary>
        ///  Draw the X axis as a series
        ///  </summary>
        private double DrawXSeries()
        {
            double maxHeight = 0;
            if (!(IsAscii))
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
                        double count = definition.XSeries.Count;
                        for (int x = 0; x <= definition.XSeries.Count - 1; x++)
                        {
                            double xctr = xAxisCanvas + (x + 0.5) / count * xExtCanvas;
                            double xtic = xAxisCanvas + Convert.ToDouble(x + 1) / count * xExtCanvas;
                            maxHeight = Math.Max(Convert.ToSingle(maxHeight), DrawStringAtAngle(definition.XSeries[x].Title, axisLabelFont, axisBrush, xctr, yAxisCanvas - AXIS_BIG_TICK, txtFormat, direction).Height);
                            AxisDrawline(xtic, yAxisCanvas - AXIS_BIG_TICK, xtic, yAxisCanvas);
                            if (hasGridLines)
                            {
                                DrawLine(gridLinePen, xtic, yAxisCanvas, xtic, yAxisCanvas + yExtCanvas);
                            }
                        }
                    }
                }
            }
            return maxHeight;
        }

        ///  <summary>
        ///  Draw the X axis as a series
        ///  </summary>
        private double DrawXSeries(IList<string> labels)
        {
            double maxHeight = 0;
            if (!(IsAscii))
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
                        for (int x = 0; x <= labels.Count - 1; x++)
                        {
                            double xctr = xAxisCanvas + (x + 0.5) / count * xExtCanvas;
                            double xtic = xAxisCanvas + Convert.ToDouble(x + 1) / count * xExtCanvas;
                            maxHeight = Math.Max(Convert.ToSingle(maxHeight), DrawStringAtAngle(labels[x], axisLabelFont, axisBrush, xctr, yAxisCanvas - AXIS_BIG_TICK, txtFormat, direction).Height);
                            AxisDrawline(xtic, yAxisCanvas - AXIS_BIG_TICK, xtic, yAxisCanvas);
                            if (hasGridLines)
                                DrawLine(gridLinePen, xtic, yAxisCanvas, xtic, yAxisCanvas + yExtCanvas);
                        }
                    }
                }
            }
            return maxHeight;
        }

        private void AxisDrawline(double x1, double y1, double x2, double y2)
        {
            DrawLine(axisPen, x1, y1, x2, y2);
        }

        private void DrawLine(Pen p, double x1, double y1, double x2, double y2)
        {
            canvas.DrawLine(p, Convert.ToSingle(Math.Round(x1, 0)), Convert.ToSingle(Math.Round(metaH - y1, 0)), Convert.ToSingle(Math.Round(x2, 0)), Convert.ToSingle(Math.Round(metaH - y2, 0)));
        }

        //		private void AxisDrawStringR( string Txt, double X1, double Y1 ) 
        //		{ 
        //			// Draw axis text aligned to the right
        //			StringFormat AlignTxt = new StringFormat(); 
        //			AlignTxt.Alignment = StringAlignment.Far; 
        //			DrawString( Txt, AxisLabelFont, AxisBrush, X1, Y1, AlignTxt ); 
        //		} 
        //

        /// <summary>
        /// Draw axis text aligned to the right
        /// </summary>
        /// <param name="txt"></param>
        /// <param name="x1"></param>
        /// <param name="y1"></param>
        /// <param name="direction"></param>
        /// <returns></returns>
        private SizeF AxisDrawStringAtAngleRM(string txt, double x1, double y1, LabelDirection direction)
        {
            using (StringFormat alignTxt = new StringFormat())
            {
                alignTxt.Alignment = StringAlignment.Far;
                alignTxt.LineAlignment = StringAlignment.Center;
                return DrawStringAtAngle(txt, axisLabelFont, axisBrush, x1, y1, alignTxt, direction);
            }
        }

        ///  <summary>
        ///  Draw axis text aligned to the centre
        ///  </summary>
        ///  <param name="txt"></param>
        ///  <param name="x1"></param>
        ///  <param name="y1"></param>
        /// <param name="direction"></param>
        private SizeF AxisDrawStringAtAngleCT(string txt, double x1, double y1, LabelDirection direction)
        {
            using (StringFormat alignTxt = new StringFormat())
            {
                alignTxt.Alignment = StringAlignment.Center;
                alignTxt.LineAlignment = StringAlignment.Near;
                return DrawStringAtAngle(txt, axisLabelFont, axisBrush, x1, y1, alignTxt, direction);
            }
        }

        ///  <summary>
        ///  Draw axis text aligned to the centre
        ///  </summary>
        ///  <param name="txt"></param>
        ///  <param name="x1"></param>
        ///  <param name="y1"></param>
        private void AxisDrawStringC(string txt, double x1, double y1)
        {
            using (StringFormat alignTxt = new StringFormat())
            {
                alignTxt.Alignment = StringAlignment.Center;
                DrawString(txt, axisLabelFont, axisBrush, x1, y1, alignTxt);
            }
        }

        //		private void AxisDrawStringL( string Txt, double X1, double Y1 ) 
        //		{ 
        //			// Draw axis text aligned to the left
        //			StringFormat AlignTxt = new StringFormat(); 
        //			AlignTxt.Alignment = StringAlignment.Near; 
        //			DrawString( Txt, AxisLabelFont, AxisBrush, X1, Y1, AlignTxt ); 
        //		} 
        //

        ///  <summary>
        ///  Draw legend text aligned to the left
        ///  </summary>
        ///  <remarks></remarks>
        private void DrawStringLegendL(string txt, double x, double y)
        {
            DrawStringLegend(txt, x, y, StringAlignment.Near);
        }

        ///  <summary>
        ///  Draw legend text aligned to the left
        ///  </summary>
        ///  <remarks></remarks>
        //		private void DrawStringSeriesLabelL( string Txt, double X, double Y ) 
        //		{ 
        //			DrawStringSeriesLabel( Txt, X, Y, StringAlignment.Near ); 
        //		} 
        //

        ///  <summary>
        ///  Draw legend text
        ///  </summary>
        ///  <remarks></remarks>
        private void DrawStringLegend(string txt, double x, double y, StringAlignment alignment)
        {
            using (StringFormat alignTxt = new StringFormat())
            {
                alignTxt.Alignment = alignment;
                DrawString(txt, legendFont, axisBrush, x, y, alignTxt);
            }
        }


        ///  <summary>
        ///  Draw legend text
        ///  </summary>
        ///  <remarks></remarks>
        //		private void DrawStringSeriesLabel( string Txt, double X, double Y, StringAlignment Alignment ) 
        //		{ 
        //			StringFormat AlignTxt = new StringFormat(); 
        //			AlignTxt.Alignment = Alignment; 
        //			DrawString( Txt, SeriesLabelFont, AxisBrush, X, Y, AlignTxt ); 
        //		} 
        //

        ///  <summary>
        ///  Draw label text
        ///  </summary>
        ///  <remarks></remarks>
        private void DrawStringLabel(string txt, double x, double y, StringAlignment alignment)
        {
            using (StringFormat alignTxt = new StringFormat())
            {
                alignTxt.Alignment = alignment;
                DrawString(txt, labelFont, axisBrush, x, y, alignTxt);
            }
        }

        private void DrawStringLabel(string txt, double x, double y, StringAlignment alignment, StringAlignment lineAlignment)
        {
            using (StringFormat alignTxt = new StringFormat())
            {
                alignTxt.Alignment = alignment;
                alignTxt.LineAlignment = lineAlignment;
                DrawString(txt, labelFont, axisBrush, x, y, alignTxt);
            }
        }

        private void FillEllipse(Brush b, double x, double y, double width, double height)
        {
            canvas.FillEllipse(b, Convert.ToInt32(Convert.ToSingle(x)), Convert.ToInt32(Convert.ToSingle(metaH - y)), Convert.ToInt32(Convert.ToSingle(width)), Convert.ToInt32(Convert.ToSingle(height)));
        }

        private void DrawEllipse(Pen p, double x, double y, double width, double height)
        {
            canvas.DrawEllipse(p, Convert.ToSingle(x), Convert.ToSingle(metaH - y), Convert.ToSingle(width), Convert.ToSingle(height));
        }

        private void FillRectangle(Brush b, double x, double y, double width, double height)
        {
            canvas.FillRectangle(b, Convert.ToSingle(x), Convert.ToSingle(metaH - y), Convert.ToSingle(width), Convert.ToSingle(height));
        }

        private void DrawRectangle(Pen p, double x, double y, double width, double height)
        {
            canvas.DrawRectangle(p, Convert.ToSingle(x), Convert.ToSingle(metaH - y), Convert.ToSingle(width), Convert.ToSingle(height));
        }

        private void DrawMarker(double x, double y, double size, MarkerShape shape, bool isFilled, Pen p)
        {
            double size2 = size * 2;

            switch (shape)
            {
                case MarkerShape.Circle:
                    {
                        if (isFilled)
                        {
                            using (Brush b = new SolidBrush(p.Color))
                            {
                                FillEllipse(b, x - size, y + size, size2, size2);
                            }
                        }
                        else
                        {
                            DrawEllipse(p, x - size, y + size, size2, size2);
                        }
                    }
                    break;
                case MarkerShape.Square:
                    {
                        if (isFilled)
                        {
                            using (Brush b = new SolidBrush(p.Color))
                            {
                                FillRectangle(b, x - size, y + size, size2, size2);
                            }
                        }
                        else
                        {
                            DrawRectangle(p, x - size, y + size, size2, size2);
                        }
                    }
                    break;
                case MarkerShape.Triangle:
                    {
                        PointF[] points = { new PointF(Convert.ToSingle(x - size), Convert.ToSingle(metaH - (y - size))), new PointF(Convert.ToSingle(x), Convert.ToSingle(metaH - (y + size))), new PointF(Convert.ToSingle(x + size), Convert.ToSingle(metaH - (y - size))) };
                        if (isFilled)
                        {
                            using (Brush b = new SolidBrush(p.Color))
                            {
                                canvas.FillPolygon(b, points);
                            }
                        }
                        else
                        {
                            canvas.DrawPolygon(p, points);
                        }
                    }
                    break;
                case MarkerShape.Plus:
                    //  Same filled or unfilled
                    DrawLine(p, x - size, y, x + size, y);
                    DrawLine(p, x, y - size, x, y + size);
                    break;
                case MarkerShape.Cross:
                    //  Same filled or unfilled
                    DrawLine(p, x - size, y - size, x + size, y + size);
                    DrawLine(p, x - size, y + size, x + size, y - size);
                    break;
                case MarkerShape.CircleLine:
                    {
                        if (isFilled)
                        {
                            using (Brush b = new SolidBrush(p.Color))
                            {
                                FillEllipse(b, x - size, y + size, size2, size2);
                            }
                            DrawLine(Pens.White, x, y - size, x, y + size);
                        }
                        else
                        {
                            DrawEllipse(p, x - size, y + size, size2, size2);
                            DrawLine(p, x, y - size, x, y + size);
                        }
                    }
                    break;
                case MarkerShape.SquareLine:
                    if (isFilled)
                    {
                        using (Brush b = new SolidBrush(p.Color))
                        {
                            FillRectangle(b, x - size, y + size, size2, size2);
                        }
                        DrawLine(Pens.White, x - size, y + size, x + size, y - size);
                    }
                    else
                    {
                        DrawRectangle(p, x - size, y + size, size2, size2);
                        DrawLine(p, x - size, y + size, x + size, y - size);
                    }
                    break;
                case MarkerShape.SquareCross:
                    if (isFilled)
                    {
                        using (Brush b = new SolidBrush(p.Color))
                        {
                            FillRectangle(b, x - size, y + size, size2, size2);
                        }
                        DrawLine(Pens.White, x - size, y - size, x + size, y + size);
                        DrawLine(Pens.White, x - size, y + size, x + size, y - size);
                    }
                    else
                    {
                        DrawRectangle(p, x - size, y + size, size2, size2);
                        DrawLine(p, x - size, y - size, x + size, y + size);
                        DrawLine(p, x - size, y + size, x + size, y - size);
                    }
                    break;
                case MarkerShape.Diamond:
                    DrawDiamond(p, x, y, size2, isFilled);
                    break;
                default:
                    throw new ArgumentException("Don't know how to draw style's shape", "shape");
            }

        }

        private void DrawMarker(double x, double y, double size, DoubleSeries series)
        {
            DrawMarker(x, y, size, series.Shape, series.IsFilled, series.UnstyledPen);
        }

        private void DrawMarker(double x, double y, double size, MarkerType mType)
        {
            using (Pen p = GetPen(mType, true))
            {
                DrawMarker(x, y, size, mType.Shape, mType.IsFilled, p);
            }
        }

        private void SetStandardScaling()
        {
            divx = axisXMax - axisXMin;
            offx = -(axisXMin / divx * xExtCanvas) + xAxisCanvas;
            divy = axisYMax - axisYMin;
            offy = -(axisYMin / divy * yExtCanvas) + yAxisCanvas;
        }

        private void SetStandardAsciiScaling()
        {
            divx = axisXMax - axisXMin;
            offx = Convert.ToInt32(-(axisXMin / divx * 60) + 15);
            divy = axisYMax - axisYMin;
            offy = Convert.ToInt32(-(axisYMin / divy * yDiv) + ASCII_Ytxt);
        }

        private ScaleParameters GetScatterScaleParameters()
        {
            return new ScaleParameters
                                     {
                                         X =
                                             {
                                                 AllowedScaleTypes =
                                                     new[] { ScaleType.Linear, ScaleType.Log10, ScaleType.LogNatural },
                                                 ShouldCheck = true,
                                                 Max = DataMaxX,
                                                 Min = DataMinX
                                             },
                                         Y =
                                             {
                                                 AllowedScaleTypes =
                                                     new[] { ScaleType.Linear, ScaleType.Log10, ScaleType.LogNatural },
                                                 ShouldCheck = true,
                                                 Max = DataMaxY,
                                                 Min = DataMinY
                                             }
                                     };
        }

        private ParameterBag PlotScatter(Stream outputStream, bool joinMarkersWithLines)
        {
            const int LEGEND_MARKER_X = 12;
            const int LEGEND_MARKER_Y_OFFSET = 14; //  22
            const int LEGEND_TEXT_X = 24;
            const int MINIMUM_X_WHITESPACE = 70;

            ScatterXYOptions sOptions = ((ScatterXYOptions)(definition.ChartOptions));
            bool ShouldDrawMarkers = sOptions.PlotMarkers;

            if (!(IsAscii))
            {
                // Plot a metafile version
                StartMetafile(outputStream, true);
                SetFontsAndThicknessesFromOptions(sOptions);
                AssignMarkersToSeries(sOptions);
                // Draw the scale
                DefaultAxes();
                //  What extra space do we need before the X axis?
                double xtra = 0;
                if (definition.XSeries.Count > 1)
                {
                    foreach (Series s in definition.XSeries)
                    {
                        double w = LegendWidth(s.Title) + MINIMUM_X_WHITESPACE;
                        if (w > xtra + xAxisCanvas)
                        {
                            xtra = w - xAxisCanvas;
                        }
                    }
                }

                DrawAxes(definition.ChartOptions.Title, new Axis(sOptions.XAxisTitle, AxisMode.Scale, 0, definition.ScaleParameters.X.ScaleType), new Axis(sOptions.YAxisTitle, AxisMode.Scale, xtra, definition.ScaleParameters.Y.ScaleType), boxAxes, true, false);

                float size2 = labelFont.Size * 2;
                //  If there are multiple series, draw the legends
                if (definition.XSeries.Count > 1)
                {
                    int i = 1;
                    foreach (Series s in definition.XSeries)
                    {
                        if (s.Title.Length > 0)
                        {
                            DrawMarker(LEGEND_MARKER_X, yAxisCanvas + yExtCanvas - LEGEND_MARKER_Y_OFFSET - (size2 * i), LEGEND_MARKER_SIZE, definition.YSeries[i - 1].AsDoubleSeries);
                            DrawStringLegendL(s.Title, LEGEND_TEXT_X, yAxisCanvas + yExtCanvas - 10 - (size2 * i));
                        }
                        i += 1;
                    }
                }

                //  Get the offsets and scale multipliers for the markers
                SetStandardScaling();

                // plot points
                for (int c = 0; c <= definition.XSeries.Count - 1; c++)
                {
                    DoubleSeries xs = definition.XSeries[c].AsDoubleSeries;
                    DoubleSeries ys = definition.YSeries[c].AsDoubleSeries;
                    double[] xdat = xs.Data;
                    double[] ydat = ys.Data;
                    PointF[] xys = new PointF[xs.Data.Length - 1 + 1 /* for VB to C# conversion */ ];
                    for (int r = 0; r <= xs.Data.Length - 1; r++)
                    {
                        if (xdat[r] != Constant.MISSING & ydat[r] != Constant.MISSING)
                        {
                            xys[r].X = Convert.ToSingle(ToCanvasX(xdat[r]));
                            xys[r].Y = Convert.ToSingle(ToCanvasY(ydat[r]));
                        }
                        else
                        {
                            xys[r].X = -1;
                            xys[r].Y = -1;
                        }
                    }
                    DrawMarkerSeries(xys, ys.MarkerSize, ys.Shape, ys.IsFilled, ys.UnstyledPen, ys.StyledPen, joinMarkersWithLines, ShouldDrawMarkers);
                }
                MaybeDrawMarkerLines();
                EndMetafile();
            }
            else
            {
                ASCII_InitPlot(25);

                // Draw the scale
                DrawAxes(definition.ChartOptions.Title, new Axis(sOptions.XAxisTitle, AxisMode.Scale, 0, definition.ScaleParameters.X.ScaleType), new Axis(sOptions.YAxisTitle, AxisMode.Scale, 0, definition.ScaleParameters.Y.ScaleType), false, true, false);

                // Draw the title text
                int L = sOptions.YAxisTitle.Length;
                int Q = L < 14 ? 14 - L : 2;
                WriteAsciiYX(shTx.GetUpperBound(0) - 1, Q, sOptions.YAxisTitle);
                L = sOptions.XAxisTitle.Length;
                WriteAsciiYX(0, 76 - L, sOptions.XAxisTitle);

                // Get the offsets for the markers
                SetStandardAsciiScaling();

                // Work through the columns
                for (int c = 0; c <= definition.XSeries.Count - 1; c++)
                {
                    DoubleSeries xs = definition.XSeries[c].AsDoubleSeries;
                    DoubleSeries ys = definition.YSeries[c].AsDoubleSeries;
                    double[] xdat = xs.Data;
                    double[] ydat = ys.Data;
                    // Work through the rows
                    for (int r = 0; r <= xdat.Length - 1; r++)
                    {
                        if (xdat[r] != Constant.MISSING && ydat[r] != Constant.MISSING)
                        {
                            int x1 = Convert.ToInt32(offx + Convert.ToInt32(xdat[r] / divx * 60));
                            int y1 = Convert.ToInt32(offy + Convert.ToInt32(ydat[r] / divy * yDiv));
                            ASCII_PlotPoint(x1, y1);
                        }
                    }
                }
            }
            //  End If
            return new ParameterBag();
        }

        private ScaleParameters GetLinearRegressionScaleParameters()
        {
            return new ScaleParameters
                                     {
                                         X =
                                             {
                                                 AllowedScaleTypes = new[] { ScaleType.Linear },
                                                 ShouldCheck = true,
                                                 Max = DataMaxX,
                                                 Min = DataMinX
                                             },
                                         Y =
                                             {
                                                 AllowedScaleTypes = new[] { ScaleType.Linear },
                                                 ShouldCheck = true,
                                                 Max = DataMaxY,
                                                 Min = DataMinY
                                             }
                                     };
        }

        private ParameterBag PlotLinearRegression(Stream outputStream)
        {
            const int MINIMUM_X_WHITESPACE = 70;
            const int MARKER_SIZE = 6;

            LinearRegressionOptions lrOptions = ((LinearRegressionOptions)(definition.ChartOptions));
            double slope = lrOptions.Slope;
            double intercept = lrOptions.Intercept;
            bool fullWidth = lrOptions.FullWidth;

            // Plot a metafile version
            StartMetafile(outputStream, true);
            AssignMarkersToSeries();
            // Draw the scale
            DefaultAxes();
            //  What extra space do we need before the X axis?
            double xtra = 0;
            if (definition.XSeries.Count > 1)
            {
                foreach (Series s in definition.XSeries)
                {
                    double w = canvas.MeasureString(s.Title, legendFont).Width + MINIMUM_X_WHITESPACE;
                    if (w > xtra + xAxisCanvas)
                    {
                        xtra = w - xAxisCanvas;
                    }
                }
            }

            DrawAxes(definition.ChartOptions.Title, new Axis(lrOptions.XAxisTitle, AxisMode.Scale, 0, definition.ScaleParameters.X.ScaleType), new Axis(lrOptions.YAxisTitle, AxisMode.Scale, xtra, definition.ScaleParameters.Y.ScaleType), boxAxes, true, false);

            //  Get the offsets and scale multipliers for the markers
            SetStandardScaling();

            // plot points
            DoubleSeries xs = definition.XSeries[0].AsDoubleSeries;
            DoubleSeries ys = definition.YSeries[0].AsDoubleSeries;
            double[] xdat = xs.Data;
            double[] ydat = ys.Data;
            PointF[] xys = new PointF[xs.Data.Length - 1 + 1 /* for VB to C# conversion */ ];
            for (int r = 0; r <= xs.Data.Length - 1; r++)
            {
                if (xdat[r] != Constant.MISSING & ydat[r] != Constant.MISSING)
                {
                    xys[r].X = Convert.ToSingle(ToCanvasX(xdat[r]));
                    xys[r].Y = Convert.ToSingle(ToCanvasY(ydat[r]));
                }
                else
                {
                    xys[r].X = -1;
                    xys[r].Y = -1;
                }
            }
            DrawMarkerSeries(xys, MARKER_SIZE, ys.Shape, ys.IsFilled, ys.UnstyledPen, ys.StyledPen, false, true);

            double xstep = xInt / 2;

            // Plot regression
            using (Pen p = new Pen(grGreen, 2))
            {
                double oldx = 0;
                double oldy = 0;

                double lowerX;
                double upperX;
                if (fullWidth)
                {
                    lowerX = axisXMin;
                    upperX = axisXMax;
                }
                else
                {
                    lowerX = DataMinX;
                    upperX = DataMaxX;
                }
                for (double calcx = lowerX; calcx <= upperX; calcx += xstep)
                {
                    double calcy = slope * calcx + intercept;
                    double x1 = ToCanvasX(calcx);
                    double y1 = ToCanvasY(calcy);
                    if (calcx > lowerX && y1 > yAxisCanvas && x1 > xAxisCanvas && y1 < yAxisCanvas + yExtCanvas)
                    {
                        DrawLine(p, x1, y1, oldx, oldy);
                    }
                    oldx = x1;
                    oldy = y1;
                }
            }

            MaybeDrawMarkerLines();
            EndMetafile();
            return new ParameterBag();
        }

        public string PlotLinearRegressionAndMaybePertAndReturnRtf(ITemplateHost host, string title, double slope, double intercept, bool fullWidth, string xAxisTitle, string yAxisTitle, double PERT, int nx, double MS, double SUMX, double SSX, bool plotBothLines)
        {
            using (MemoryStream metaStream = new MemoryStream())
            {
                StartMetafile(metaStream);
                PlotLinearRegressionInternal(title, slope, intercept, fullWidth, xAxisTitle, yAxisTitle);
                if (PERT != 0)
                    PlotPert(PERT, slope, intercept, nx, MS, SUMX, SSX, plotBothLines);
                EndMetafile();
                return host.ImageStreamToRtf(metaStream);
            }
        }

        private void PlotLinearRegressionInternal(string title, double slope, double intercept, bool fullWidth, string xAxisTitle, string yAxisTitle)
        {
            const int MINIMUM_X_WHITESPACE = 70;
            const int MARKER_SIZE = 6;

            AssignMarkersToSeries();
            // Draw the scale
            DefaultAxes();
            //  What extra space do we need before the X axis?
            double xtra = 0;
            if (definition.XSeries.Count > 1)
            {
                foreach (Series s in definition.XSeries)
                {
                    double w = canvas.MeasureString(s.Title, legendFont).Width + MINIMUM_X_WHITESPACE;
                    if (w > xtra + xAxisCanvas)
                    {
                        xtra = w - xAxisCanvas;
                    }
                }
            }

            DrawAxes(title, new Axis(xAxisTitle, AxisMode.Scale, 0, ScaleType.Linear), new Axis(yAxisTitle, AxisMode.Scale, xtra, ScaleType.Linear), boxAxes, true, false);

            //  Get the offsets and scale multipliers for the markers
            SetStandardScaling();

            // plot points
            DoubleSeries xs = definition.XSeries[0].AsDoubleSeries;
            DoubleSeries ys = definition.YSeries[0].AsDoubleSeries;
            double[] xdat = xs.Data;
            double[] ydat = ys.Data;
            PointF[] xys = new PointF[xs.Data.Length - 1 + 1 /* for VB to C# conversion */ ];
            for (int r = 0; r <= xs.Data.Length - 1; r++)
            {
                if (xdat[r] != Constant.MISSING & ydat[r] != Constant.MISSING)
                {
                    xys[r].X = Convert.ToSingle(ToCanvasX(xdat[r]));
                    xys[r].Y = Convert.ToSingle(ToCanvasY(ydat[r]));
                }
                else
                {
                    xys[r].X = -1;
                    xys[r].Y = -1;
                }
            }
            DrawMarkerSeries(xys, MARKER_SIZE, ys.Shape, ys.IsFilled, ys.UnstyledPen, ys.StyledPen, false, true);

            double xstep = xInt / 2;

            // Plot regression
            using (Pen p = new Pen(grGreen, 2))
            {
                double oldx = 0;
                double oldy = 0;
                if (fullWidth)
                {
                    for (double calcx = axisXMin; calcx <= axisXMax; calcx += xstep)
                    {
                        double calcy = slope * calcx + intercept;
                        double x1 = ToCanvasX(calcx);
                        double y1 = ToCanvasY(calcy);
                        if (calcx > axisXMin && y1 > yAxisCanvas && x1 > xAxisCanvas && y1 < yAxisCanvas + yExtCanvas)
                        {
                            DrawLine(p, x1, y1, oldx, oldy);
                        }
                        oldx = x1;
                        oldy = y1;
                    }
                }
                else
                {
                    for (double calcx = DataMinX; calcx <= DataMaxX; calcx += xstep)
                    {
                        double calcy = slope * calcx + intercept;
                        double x1 = ToCanvasX(calcx);
                        double y1 = ToCanvasY(calcy);
                        if (calcx > DataMinX && y1 > yAxisCanvas && x1 > xAxisCanvas && y1 < yAxisCanvas + yExtCanvas)
                        {
                            DrawLine(p, x1, y1, oldx, oldy);
                        }
                        oldx = x1;
                        oldy = y1;
                    }
                }
            }
        }

        public string PlotCox2AndReturnRtf(ITemplateHost host, int[] gn, int igroups, double[] xp, double[] yp, ColumnData[] cdat1, int groupid)
        {
            using (MemoryStream metaStream = new MemoryStream())
            {
                StartMetafile(metaStream);
                PlotCox2Internal(gn, igroups, xp, yp, cdat1, groupid);
                EndMetafile();
                return host.ImageStreamToRtf(metaStream);
            }
        }

        private void PlotCox2Internal(int[] gn, int igroups, double[] xp, double[] yp, ColumnData[] cdat1, int groupid)
        {
            const int MINIMUM_X_WHITESPACE = 70;

            DefaultAxes();

            double xtra = 0;
            if (definition.XSeries.Count > 1)
            {
                foreach (Series s in definition.XSeries)
                {
                    double w = canvas.MeasureString(s.Title, legendFont).Width + MINIMUM_X_WHITESPACE;
                    if (w > xtra + xAxisCanvas)
                    {
                        xtra = w - xAxisCanvas;
                    }
                }
            }

            //  TODO: Log and log-log axes here
            DrawAxes("Log-log plot (parallel groups if hazards proportional)", new Axis("log(Time)", AxisMode.Scale, 0, ScaleType.Linear), new Axis("-log(-log(Survival))", AxisMode.Scale, xtra, ScaleType.Linear), false, true, false);

            // Draw the legends
            using (Pen p = new Pen(grBlack, 1))
            {
                float size2 = labelFont.Size * 2;
                for (int i = 1; i <= igroups; i++)
                {
                    DrawMarker(12, yAxisCanvas + yExtCanvas - 22 - (size2 * i), 6, ((MarkerShape) (i)), false, p);
                    string transTemp11 = cdat1[groupid].Title;
                    int transTemp12 = Math.Min(20, cdat1[groupid].Title.Length);
                    DrawStringLegendL( /* TRANSINFO: .NET Equivalent of Microsoft.VisualBasic NameSpace */ transTemp11.Substring(0, transTemp12) + "=" + cdat1[groupid].Groups[i - 1].Label, 24, yAxisCanvas + yExtCanvas - 10 - (size2 * i));
                }

                // get the offsets for the Markers
                SetStandardScaling();

                // plot points
                int istart = 0;
                for (int k = 1; k <= 2; k++)
                {
                    PointF[] xys = new PointF[gn[k] + 1 /* for VB to C# conversion */];
                    for (int i = 1; i <= gn[k]; i++)
                    {
                        if (xp[istart + i] != Constant.MISSING & yp[istart + i] != Constant.MISSING)
                        {
                            xys[i].X = Convert.ToSingle(ToCanvasX(xp[istart + i]));
                            xys[i].Y = Convert.ToSingle(ToCanvasY(yp[istart + i]));
                        }
                        else
                        {
                            xys[i].X = -1;
                            xys[i].Y = -1;
                        }
                    }
                    DrawMarkerSeries(xys, 6, ((MarkerShape) (k)), false, p, p, false, true);
                    istart += gn[k];
                }
            }
        }

        public string PlotCox1AndReturnRtf(ITemplateHost host, string title, coxp[] z, int iobs, bool stratified, bool grouped, int istrata, int igroups, ColumnData[] cdat1, int groupid, bool use_marker, bool use_tic, double[, ,] ARR3, int j3, string xAxisTitle, string yAxisTitle, ref int[] gn)
        {
            using (MemoryStream metaStream = new MemoryStream())
            {
                StartMetafile(metaStream);
                PlotCox1Internal(title, z, iobs, stratified, grouped, istrata, igroups, cdat1, groupid, use_marker, use_tic, ARR3, j3, xAxisTitle, yAxisTitle, ref gn);
                EndMetafile();
                return host.ImageStreamToRtf(metaStream);
            }
        }

        private void PlotCox1Internal(string title, coxp[] z, int iobs, bool stratified, bool grouped, int istrata, int igroups, ColumnData[] cdat1, int groupid, bool use_marker, bool use_tic, double[, ,] ARR3, int j3, string xAxisTitle, string yAxisTitle, ref int[] gn)
        {
            const int MINIMUM_X_WHITESPACE = 70;

            AssignMarkersToSeries();
            // allow more room for legend labels if required
            DefaultAxes();
            double xtra = 0;
            if (stratified)
            {
                for (int i = 1; i <= istrata; i++)
                {
                    double w = canvas.MeasureString("Stratum " + i.ToString(), legendFont).Width + MINIMUM_X_WHITESPACE;
                    if (w > xtra + xAxisCanvas)
                    {
                        xtra = w - xAxisCanvas - 5;
                    }
                }
            }
            if (grouped)
            {
                for (int i = 0; i <= igroups - 1; i++)
                {
                    string transTemp14 = cdat1[groupid].Title;
                    int transTemp15 = Math.Min(20, cdat1[groupid].Title.Length);
                    double w = canvas.MeasureString(  /* TRANSINFO: .NET Equivalent of Microsoft.VisualBasic NameSpace */ transTemp14.Substring(0, transTemp15) + "=" + cdat1[groupid].Groups[i].Label, legendFont).Width + MINIMUM_X_WHITESPACE;
                    if (w > xtra + xAxisCanvas)
                    {
                        xtra = w - xAxisCanvas - 5;
                    }
                }
            }

            // Draw the axes
            DrawAxes(title, new Axis(xAxisTitle, AxisMode.Scale, 0, definition.ScaleParameters.X.ScaleType), new Axis(yAxisTitle, AxisMode.Scale, xtra, definition.ScaleParameters.Y.ScaleType), false, true, false);

            //  Get the offsets and scale multipliers for the markers
            SetStandardScaling();

            // draw legend
            double size2 = labelFont.Size * 2;
            if (grouped)
            {
                for (int k = 1; k <= igroups; k++)
                {
                    string transTemp17 = cdat1[groupid].Title;
                    int transTemp18 = Math.Min(20, cdat1[groupid].Title.Length);
                    string vq =  /* TRANSINFO: .NET Equivalent of Microsoft.VisualBasic NameSpace */ transTemp17.Substring(0, transTemp18) + "=" + cdat1[groupid].Groups[k - 1].Label;
                    if (!(use_marker))
                    {
                        using (Pen pp = GetPen(_markerTypes[(k - 1) % 9], true))
                        {
                            DrawLine(pp, 10, yAxisCanvas + yExtCanvas - 18 - (size2 * k), 20, yAxisCanvas + yExtCanvas - 18 - (size2 * k));
                            DrawLine(pp, 20, yAxisCanvas + yExtCanvas - 18 - (size2 * k), 20, yAxisCanvas + yExtCanvas - 28 - (size2 * k));
                        }
                    }
                    else
                    {
                        DrawMarker(12, yAxisCanvas + yExtCanvas - 22 - (size2 * k), 6, _markerTypes[(k - 1) % 9]);
                    }
                    DrawStringLegendL(vq, 24, yAxisCanvas + yExtCanvas - 10 - (size2 * k));
                }
            }
            //  Legend
            if (stratified)
            {
                for (int k = 1; k <= istrata; k++)
                {
                    string vq = "Stratum " + k.ToString();
                    if (!(use_marker))
                    {
                        using (Pen pp = GetPen(_markerTypes[(k - 1) % 9], true))
                        {
                            DrawLine(pp, 10, yAxisCanvas + yExtCanvas - 18 - (size2 * k), 20, yAxisCanvas + yExtCanvas - 18 - (size2 * k));
                            DrawLine(pp, 20, yAxisCanvas + yExtCanvas - 18 - (size2 * k), 20, yAxisCanvas + yExtCanvas - 28 - (size2 * k));
                        }
                    }
                    else
                    {
                        DrawMarker(12, yAxisCanvas + yExtCanvas - 22 - (size2 * k), 6, _markerTypes[(k - 1) % 9]);
                    }
                    DrawStringLegendL(vq, 24, yAxisCanvas + yExtCanvas - 10 - (size2 * k));
                }
            }

            // starting positions
            double ix0 = 0;
            double iy0 = 0;
            switch (j3)
            {
                case 1:
                    ix0 = ToCanvasX(axisXMin);
                    iy0 = ToCanvasY(1.0);
                    break;
                case 2:
                    ix0 = ToCanvasX(axisXMin);
                    iy0 = ToCanvasY(0.0);
                    break;
            }

            double ix1 = ix0;
            double iy1 = iy0;
            int igp = 0;

            MarkerType mt = _markerTypes[igp % 9];
            Pen p = GetPen(mt, true);
            MarkerShape shape = mt.Shape;
            bool isFilled = mt.IsFilled;

            for (int i = 1; i <= iobs; i++)
            {

                if (grouped)
                {
                    if (i > 1 & z[i].id != z[i - 1].id)
                    {
                        igp = igp + 1;
                        ix1 = ix0;
                        iy1 = iy0;
                        p.Dispose();
                        mt = _markerTypes[igp % 9];
                        p = GetPen(mt, true);
                        shape = mt.Shape;
                        isFilled = mt.IsFilled;
                    }
                }
                else
                {
                    if (i > 1 & z[i].strat != z[i - 1].strat)
                    {
                        igp = igp + 1;
                        ix1 = ix0;
                        iy1 = iy0;
                        p.Dispose();
                        mt = _markerTypes[igp % 9];
                        p = GetPen(mt, true);
                        shape = mt.Shape;
                        isFilled = mt.IsFilled;
                    }
                }

                double ix2 = ToCanvasX(z[i].TM);
                double iy2 = 0;

                // get survivor or hazard function if an event occured
                switch (j3)
                {
                    case 1:
                        double surv = grouped ? Math.Pow(z[i].s, Math.Exp(Convert.ToDouble(z[i].id) * ARR3[1, groupid, 1])) : z[i].s;
                        iy2 = ToCanvasY(surv);
                        gn[igp + 1] = gn[igp + 1] + 1;
                        break;
                    case 2:
                        double haz;
                        if (grouped)
                        {
                            haz = Math.Pow(z[i].s, Math.Exp(Convert.ToDouble(z[i].id) * ARR3[1, groupid, 1]));
                            haz = haz > 0.0 ? -Math.Log(haz) : Constant.MISSING;
                        }
                        else
                        {
                            haz = z[i].h;
                        }
                        if (haz != Constant.MISSING)
                        {
                            iy2 = ToCanvasY(haz);
                        }
                        break;
                }


                if (z[i].cens == 0)
                {
                    iy2 = iy1;
                }

                if (z[i].cens == 0 && use_tic)
                {
                    // Draw tic if censored
                    if (ix1 != ix2 | iy1 != iy2)
                    {
                        DrawLine(p, ix2, iy2, ix2, iy2 + 7);
                    }
                }

                // Draw the markers
                if (iy2 != iy1 & use_marker)
                {
                    DrawMarker(ix2, iy2, 6, shape, isFilled, p);
                }

                // Then the lines
                if (ix1 != ix2 | iy1 != iy2)
                {
                    DrawLine(p, ix1, iy1, ix2, iy1);
                    DrawLine(p, ix2, iy1, ix2, iy2);
                }
                ix1 = ix2;
                iy1 = iy2;

            }
            p.Dispose();
        }

        public string PlotLinearizedEstimationAndReturnRtf(ITemplateHost host, string title, int model, double a, double b, string XAxisTitle, string YAxisTitle)
        {
            using (MemoryStream metaStream = new MemoryStream())
            {
                StartMetafile(metaStream);
                PlotLinearizedEstimationInternal(title, model, a, b, XAxisTitle, YAxisTitle);
                EndMetafile();
                return host.ImageStreamToRtf(metaStream);
            }
        }

        private void PlotLinearizedEstimationInternal(string title, int model, double a, double b, string XAxisTitle, string YAxisTitle)
        {
            const int MINIMUM_X_WHITESPACE = 70;
            const int MARKER_SIZE = 6;

            AssignMarkersToSeries();
            // Draw the scale
            DefaultAxes();
            //  What extra space do we need before the X axis?
            double xtra = 0;
            if (definition.XSeries.Count > 1)
            {
                foreach (Series s in definition.XSeries)
                {
                    double w = canvas.MeasureString(s.Title, legendFont).Width + MINIMUM_X_WHITESPACE;
                    if (w > xtra + xAxisCanvas)
                    {
                        xtra = w - xAxisCanvas;
                    }
                }
            }

            DrawAxes(title, new Axis(XAxisTitle, AxisMode.Scale, 0, definition.ScaleParameters.X.ScaleType), new Axis(YAxisTitle, AxisMode.Scale, xtra, definition.ScaleParameters.Y.ScaleType), boxAxes, true, false);

            //  Get the offsets and scale multipliers for the markers
            SetStandardScaling();

            // plot points
            DoubleSeries xs = definition.XSeries[0].AsDoubleSeries;
            DoubleSeries ys = definition.YSeries[0].AsDoubleSeries;
            double[] xdat = xs.Data;
            double[] ydat = ys.Data;
            PointF[] xys = new PointF[xs.Data.Length - 1 + 1 /* for VB to C# conversion */ ];
            for (int r = 0; r <= xs.Data.Length - 1; r++)
            {
                if (xdat[r] != Constant.MISSING & ydat[r] != Constant.MISSING)
                {
                    xys[r].X = Convert.ToSingle(ToCanvasX(xdat[r]));
                    xys[r].Y = Convert.ToSingle(ToCanvasY(ydat[r]));
                }
                else
                {
                    xys[r].X = -1;
                    xys[r].Y = -1;
                }
            }
            DrawMarkerSeries(xys, MARKER_SIZE, ys.Shape, ys.IsFilled, ys.UnstyledPen, ys.StyledPen, false, true);

            double xstep = xInt / 2;

            // Plot regression
            using (Pen p = new Pen(grBlack, 2))
            {
                double oldx = 0;
                double oldy = 0;
                for (double calcx = axisXMin; calcx <= axisXMax; calcx += xstep)
                {
                    double calcy = 0;
                    switch (model)
                    {
                        case 0:
                            calcy = a * Math.Exp(b * calcx);
                            break;
                        case 1:
                            calcy = a * Math.Pow(calcx, b);
                            break;
                        case 2:
                            double denom = a + calcx * b;
                            if (denom == 0.0)
                            {
                                denom = 0.0000001;
                            }
                            calcy = calcx / denom;
                            break;
                    }

                    double x1 = ToCanvasX(calcx);
                    double y1 = ToCanvasY(calcy);
                    if (calcx > axisXMin && y1 > yAxisCanvas && x1 > xAxisCanvas && y1 < yAxisCanvas + yExtCanvas)
                    {
                        DrawLine(p, x1, y1, oldx, oldy);
                    }
                    oldx = x1;
                    oldy = y1;
                }
            }
        }

        public string PlotPolynomialRegressionAndReturnRtf(ITemplateHost host, string title, int mode, double[,] xtxi, double[] bd, double rss, int nx, int P, double gamma, string xAxisTitle, string yAxisTitle)
        {
            using (MemoryStream metaStream = new MemoryStream())
            {
                StartMetafile(metaStream);
                PlotPolynomialRegressionInternal(title, mode, xtxi, bd, rss, nx, P, gamma, xAxisTitle, yAxisTitle);
                EndMetafile();
                return host.ImageStreamToRtf(metaStream);
            }
        }

        private void PlotPolynomialRegressionInternal(string title, int mode, double[,] xtxi, double[] bd, double rss, int nx, int P, double gamma, string xAxisTitle, string yAxisTitle)
        {
            const int MINIMUM_X_WHITESPACE = 70;
            const int MARKER_SIZE = 6;

            AssignMarkersToSeries();
            // Draw the scale
            DefaultAxes();
            //  What extra space do we need before the X axis?
            double xtra = 0;
            if (definition.XSeries.Count > 1)
            {
                foreach (Series ser in definition.XSeries)
                {
                    double w = canvas.MeasureString(ser.Title, legendFont).Width + MINIMUM_X_WHITESPACE;
                    if (w > xtra + xAxisCanvas)
                    {
                        xtra = w - xAxisCanvas;
                    }
                }
            }

            DrawAxes(title, new Axis(xAxisTitle, AxisMode.Scale, 0, definition.ScaleParameters.X.ScaleType), new Axis(yAxisTitle, AxisMode.Scale, xtra, definition.ScaleParameters.Y.ScaleType), boxAxes, true, false);

            //  Get the offsets and scale multipliers for the markers
            SetStandardScaling();

            // plot points
            DoubleSeries xs = definition.XSeries[0].AsDoubleSeries;
            DoubleSeries ys = definition.YSeries[0].AsDoubleSeries;
            double[] xdat = xs.Data;
            double[] ydat = ys.Data;
            PointF[] xys = new PointF[xs.Data.Length - 1 + 1 /* for VB to C# conversion */ ];
            for (int r = 0; r < xs.Data.Length; r++)
            {
                if (xdat[r] != Constant.MISSING & ydat[r] != Constant.MISSING)
                {
                    xys[r].X = Convert.ToSingle(ToCanvasX(xdat[r]));
                    xys[r].Y = Convert.ToSingle(ToCanvasY(ydat[r]));
                }
                else
                {
                    xys[r].X = -1;
                    xys[r].Y = -1;
                }
            }
            DrawMarkerSeries(xys, MARKER_SIZE, ys.Shape, ys.IsFilled, ys.UnstyledPen, ys.StyledPen, false, true);

            double P0;
            double cit;
            MathDbl.civ(nx - P, out cit, gamma, out P0);
            double rdf = Convert.ToDouble((nx - 1) - (P - 1));
            double rms = rss / rdf;
            double[] px = new double[P];
            px[1] = 1.0;
            double xstep = xInt / 2;

            // This routine has changed from the original
            // It is more efficient in drawing - but bigger in code
            // Draw the base line
            using (Pen greenPen = new Pen(grGreen, 1))
            {
                double oldx = 0;
                double oldy = 0;
                for (double calcx = axisXMin; calcx <= axisXMax; calcx += xstep)
                {
                    double calcy = 0;
                    for (int jj = 1; jj <= P; jj++)
                    {
                        calcy = calcy + bd[jj] * Math.Pow(calcx, Convert.ToDouble(jj - 1));
                    }
                    double x1 = ToCanvasX(calcx);
                    double y1 = ToCanvasY(calcy);
                    if (calcx > axisXMin && y1 >= yAxisCanvas && x1 >= xAxisCanvas && y1 < yAxisCanvas + yExtCanvas && oldx >= xAxisCanvas && oldy >= yAxisCanvas && oldy < yAxisCanvas + yExtCanvas)
                    {
                        DrawLine(greenPen, x1, y1, oldx, oldy);
                    }
                    oldx = x1;
                    oldy = y1;
                }
            }
            if (mode > 0)
            {
                // Draw -Lines
                using (Pen blackPen = new Pen(grBlack, 1))
                {
                    double oldx = 0;
                    double oldy = 0;
                    double sey;
                    double cl;
                    double xcx;
                    double s;
                    for (double calcx = axisXMin; calcx <= axisXMax; calcx += xstep)
                    {
                        double calcy = 0;
                        for (int jj = 1; jj <= P; jj++)
                        {
                            calcy = calcy + bd[jj] * Math.Pow(calcx, Convert.ToDouble(jj - 1));
                        }
                        px[1] = 1.0;
                        for (int k = 2; k <= P; k++)
                        {
                            px[k] = Math.Pow(calcx, Convert.ToDouble(k - 1));
                        }
                        xcx = 0.0;
                        for (int i = 1; i <= P; i++)
                        {
                            s = 0;
                            for (int k = 1; k <= P; k++)
                            {
                                s = s + xtxi[i, k] * px[k];
                            }
                            xcx = xcx + s * px[i];
                        }
                        if (mode == 1)
                        {
                            sey = Math.Sqrt(Math.Abs(rms * xcx));
                            cl = cit * sey;
                        }
                        else
                        {
                            sey = Math.Sqrt(Math.Abs(rms * (1.0 + xcx)));
                            cl = cit * sey;
                        }
                        double x1 = ToCanvasX(calcx);
                        double y1 = ToCanvasY(calcy - cl);
                        if (calcx > axisXMin && y1 >= yAxisCanvas && x1 >= xAxisCanvas && y1 < yAxisCanvas + yExtCanvas && oldx >= xAxisCanvas && oldy >= yAxisCanvas && oldy < yAxisCanvas + yExtCanvas)
                        {
                            DrawLine(blackPen, x1, y1, oldx, oldy);
                        }
                        oldx = x1;
                        oldy = y1;
                    }
                    // Draw +Lines
                    oldx = 0;
                    oldy = 0;
                    for (double calcx = axisXMin; calcx <= axisXMax; calcx += xstep)
                    {
                        double calcy = 0;
                        for (int jj = 1; jj <= P; jj++)
                        {
                            calcy = calcy + bd[jj] * Math.Pow(calcx, Convert.ToDouble(jj - 1));
                        }
                        px[1] = 1.0;
                        for (int k = 2; k <= P; k++)
                        {
                            px[k] = Math.Pow(calcx, Convert.ToDouble(k - 1));
                        }
                        xcx = 0;
                        for (int i = 1; i <= P; i++)
                        {
                            s = 0;
                            for (int k = 1; k <= P; k++)
                            {
                                s = s + xtxi[i, k] * px[k];
                            }
                            xcx = xcx + s * px[i];
                        }
                        if (mode == 1)
                        {
                            sey = Math.Sqrt(Math.Abs(rms * xcx));
                            cl = cit * sey;
                        }
                        else
                        {
                            sey = Math.Sqrt(Math.Abs(rms * (1.0 + xcx)));
                            cl = cit * sey;
                        }
                        double x1 = ToCanvasX(calcx);
                        double y1 = ToCanvasY(calcy + cl);
                        if (calcx > axisXMin && y1 >= yAxisCanvas && x1 >= xAxisCanvas && y1 < yAxisCanvas + yExtCanvas && oldx >= xAxisCanvas && oldy >= yAxisCanvas && oldy < yAxisCanvas + yExtCanvas)
                        {
                            DrawLine(blackPen, x1, y1, oldx, oldy);
                        }
                        oldx = x1;
                        oldy = y1;
                    }
                }
            }
        }

        public string PlotLogitAndReturnRtf(ITemplateHost host, string title, int model, double t, double sw, double s1, double a, double b, string xAxisTitle, string yAxisTitle)
        {
            using (MemoryStream metaStream = new MemoryStream())
            {
                StartMetafile(metaStream);
                PlotLogitInternal(title, model, t, sw, s1, a, b, xAxisTitle, yAxisTitle);
                EndMetafile();
                return host.ImageStreamToRtf(metaStream);
            }
        }

        ///  <remarks>Jul 09: updated to put log models on a log x axis scale</remarks>
        private void PlotLogitInternal(string title, int model, double t, double sw, double s1, double a, double b, string xAxisTitle, string yAxisTitle)
        {
            const int MARKER_SIZE = 6;

            DoubleSeries xs = definition.XSeries[0].AsDoubleSeries;
            DoubleSeries ys = definition.YSeries[0].AsDoubleSeries;
            double[] xdat = xs.Data;
            double[] ydat = ys.Data;

            double cl = 0;
            int nx = 0;
            foreach (double v in xdat)
            {
                if (v != Constant.MISSING)
                {
                    cl += v;
                    nx++;
                }
            }
            double XM = cl / Convert.ToDouble(nx);

            AssignMarkersToSeries();
            // Draw the scale
            DefaultAxes();
            if (DataMaxY - DataMinY > 0.25)
            {
                DataMaxY = 1;
                DataMinY = 0;
            }
            DrawAxes(title, new Axis(xAxisTitle, AxisMode.Scale, 0, definition.ScaleParameters.X.ScaleType), new Axis(yAxisTitle, AxisMode.Scale, 0, definition.ScaleParameters.Y.ScaleType), false, true, false);

            //  Get the offsets and scale multipliers for the markers
            SetStandardScaling();

            // plot points
            PointF[] xys = new PointF[Math.Min(xdat.Length, ydat.Length) - 1 + 1 /* for VB to C# conversion */ ];
            for (int r = 0; r <= Math.Min(xdat.Length, ydat.Length) - 1; r++)
            {
                if (xdat[r] != Constant.MISSING & ydat[r] != Constant.MISSING)
                {
                    xys[r].X = Convert.ToSingle(ToCanvasX(xdat[r]));
                    xys[r].Y = Convert.ToSingle(ToCanvasY(ydat[r]));
                }
                else
                {
                    xys[r].X = -1;
                    xys[r].Y = -1;
                }
            }
            DrawMarkerSeries(xys, MARKER_SIZE, ys.Shape, ys.IsFilled, ys.UnstyledPen, ys.StyledPen, false, true);

            double xstep = xInt / 2;

            // This routine has changed from the original
            // It is more efficient in drawing - but bigger in code
            using (Pen greenPen = new Pen(grGreen, 1))
            {
                double oldx = 0;
                double oldy = 0;
                for (double calcx = axisXMin; calcx <= axisXMax; calcx += xstep)
                {
                    // cl = t * Math.Sqrt( 1.0 / sw + Math.Pow( ( calcx - XM ), 2.0 ) / S1 ); 
                    double calcy = a + b * calcx;
                    if (model == 1)
                    {
                        calcy = PDF.alnorm(calcy);
                    }
                    else
                    {
                        calcy = Math.Exp(calcy * 2.0) / (1.0 + Math.Exp(calcy * 2.0));
                    }
                    double x1 = ToCanvasX(calcx);
                    double y1 = ToCanvasY(calcy);
                    if (calcx > axisXMin && y1 >= yAxisCanvas && x1 >= xAxisCanvas && y1 < yAxisCanvas + yExtCanvas && oldx >= xAxisCanvas && oldy >= yAxisCanvas && oldy < yAxisCanvas + yExtCanvas)
                    {
                        DrawLine(greenPen, x1, y1, oldx, oldy);
                    }
                    oldx = x1;
                    oldy = y1;
                }
            }

            // Draw upper curve
            using (Pen magentaPen = new Pen(grMagenta, 1))
            {
                double oldx = 0;
                double oldy = 0;
                for (double calcx = axisXMin; calcx <= axisXMax; calcx += xstep)
                {
                    cl = t * Math.Sqrt(1.0 / sw + Math.Pow((calcx - XM), 2.0) / s1);
                    double calcy = a + b * calcx;
                    double cly = calcy + cl;
                    if (model == 1)
                    {
                        cly = PDF.alnorm(cly);
                    }
                    else
                    {
                        cly = Math.Exp(cly * 2.0) / (1.0 + Math.Exp(cly * 2.0));
                    }
                    double x1 = ToCanvasX(calcx);
                    double y1 = ToCanvasY(cly);
                    if (calcx > axisXMin && y1 >= yAxisCanvas && x1 >= xAxisCanvas && y1 < yAxisCanvas + yExtCanvas && oldx >= xAxisCanvas && oldy >= yAxisCanvas && oldy < yAxisCanvas + yExtCanvas)
                    {
                        DrawLine(magentaPen, x1, y1, oldx, oldy);
                    }
                    oldx = x1;
                    oldy = y1;
                }
                // Draw lower curve
                oldx = 0;
                oldy = 0;
                for (double calcx = axisXMin; calcx <= axisXMax; calcx += xstep)
                {
                    cl = t * Math.Sqrt(1.0 / sw + Math.Pow((calcx - XM), 2.0) / s1);
                    double calcy = a + b * calcx;
                    double cly = calcy - cl;
                    if (model == 1)
                    {
                        cly = PDF.alnorm(cly);
                    }
                    else
                    {
                        cly = Math.Exp(cly * 2.0) / (1.0 + Math.Exp(cly * 2.0));
                    }
                    double x1 = ToCanvasX(calcx);
                    double y1 = ToCanvasY(cly);
                    if (calcx > axisXMin && y1 >= yAxisCanvas && x1 >= xAxisCanvas && y1 < yAxisCanvas + yExtCanvas && oldx >= xAxisCanvas && oldy >= yAxisCanvas && oldy < yAxisCanvas + yExtCanvas)
                    {
                        DrawLine(magentaPen, x1, y1, oldx, oldy);
                    }
                    oldx = x1;
                    oldy = y1;
                }
            }
        }


        ///  <summary>
        ///  Draw the set of markers whose centre device co-ordinates are in xys.
        ///  </summary>
        private void DrawMarkerSeries(PointF[] xys, double Size, MarkerShape Shape, bool IsFilled, Pen UnstyledPen, Pen StyledPen, bool JoinMarkersWithLines, bool DrawMarkers)
        {
            // sort by each of x and y
            Array.Sort(xys, new SortXThenY());

            PointF oldXy;
            if (JoinMarkersWithLines)
            {
                // plot joining lines
                //  Set initial values so that the first line won't be drawn
                oldXy = xys[0];
                foreach (PointF xy in xys)
                {
                    if (xy.X >= 0 & xy.Y >= 0)
                    {
                        if (xy.X != oldXy.X || xy.Y != oldXy.Y)
                        {
                            DrawLine(StyledPen, xy.X, xy.Y, oldXy.X, oldXy.Y);
                        }
                        oldXy = xy;
                    }
                }
            }

            if (DrawMarkers)
            {
                oldXy = new PointF(-1, -1);
                foreach (PointF xy in xys)
                {
                    // ok to compare with element below cos zero based array
                    if (xy.X != oldXy.X || xy.Y != oldXy.Y)
                    {
                        if (xy.X >= 0 & xy.Y >= 0)
                        {
                            DrawMarker(xy.X, xy.Y, Size, Shape, IsFilled, UnstyledPen);
                        }
                        oldXy = xy;
                    }
                }
            }
        }


        ///  <summary>
        ///  Initialise everything required for an ASCII plot of the required number of lines, notably including the SH_TX array.
        ///  </summary>
        ///  <param name="lines">The number of lines of text in the ASCII plot</param>
        ///  <remarks></remarks>
        private void ASCII_InitPlot(int lines)
        {
            shTx = new string[lines];
            for (int c = 0; c <= shTx.GetUpperBound(0); c++)
            {
                shTx[c] = String.Empty.PadLeft(85);
            }
        }


        // TRANSMISSINGCOMMENT: Method ASCII_PlotPoint
        private void ASCII_PlotPoint(int x, int y)
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


        // TRANSMISSINGCOMMENT: Method AssignMarkersToSeries
        private void AssignMarkersToSeries()
        {
            if (definition.XSeries.Count > 0)
            {
                AssignMarkersToSeries(definition.XSeries);
            }
            if (definition.YSeries.Count > 0)
            {
                AssignMarkersToSeries(definition.YSeries);
            }
        }


        // TRANSMISSINGCOMMENT: Method AssignMarkersToSeries
        private void AssignMarkersToSeries(GenericOptions opts)
        {
            if (definition.XSeries.Count > 0)
            {
                AssignMarkersToSeries(definition.XSeries, opts);
            }
            if (definition.YSeries.Count > 0)
            {
                AssignMarkersToSeries(definition.YSeries, opts);
            }
        }


        private void AssignMarkersToSeries(List<Series> s)
        {
            for (int i = 0; i <= s.Count - 1; i++)
            {
                DoubleSeries ds = ((DoubleSeries)(s[i]));
                int mkr = ChartOptions.SeriesNumberToMarkerNumber(i);
                SetSeriesFromMarkerTypeAndOptions(ds, _markerTypes[mkr], null);
            }
        }



        private void SetSeriesFromMarkerTypeAndOptions(DoubleSeries ds, MarkerType mt, GenericOptions o)
        {
            if ((o != null))
            {
                ds.IsFilled = o.ShouldForceIsFilled ? o.ForcedIsFilled : mt.IsFilled;
                ds.FillStyle = o.ShouldForceFillStyle ? o.ForcedFillStyle : mt.FillStyle;
            }
            else
            {
                ds.Style = mt.Style;
                ds.FillStyle = mt.FillStyle;
            }
            ds.UnstyledPen = GetPen(mt, true);
            //  Dash styles are only used in monochrome plots; if colour, ignore.
            ds.StyledPen = GetPen(mt, HasChartOptions && definition.ChartOptions.UseColour);
            ds.Shape = mt.Shape;
            ds.MarkerSize = mt.MarkerSize;
            ds.Style = mt.Style;
        }


        private void AssignMarkersToSeries(List<Series> s, GenericOptions opts)
        {
            if (opts == null || opts.MarkerTypes == null || opts.MarkerTypes.Count < 1)
            {
                for (int i = 0; i <= s.Count - 1; i++)
                {
                    DoubleSeries ds = ((DoubleSeries)(s[i]));
                    int mkr = ChartOptions.SeriesNumberToMarkerNumber(i);
                    SetSeriesFromMarkerTypeAndOptions(ds, _markerTypes[mkr], opts);
                }
            }
            else
            {
                for (int i = 0; i <= s.Count - 1; i++)
                {
                    if ((s[i]) is DoubleSeries)
                    {
                        DoubleSeries ds = ((DoubleSeries)(s[i]));
                        int mkr = i % opts.MarkerTypes.Count;
                        SetSeriesFromMarkerTypeAndOptions(ds, opts.MarkerTypes[mkr], opts);
                    }
                }
            }
        }


        // TRANSMISSINGCOMMENT: Method GetBoxWhiskerScaleParameters
        private ScaleParameters GetBoxWhiskerScaleParameters()
        {
            //  If only X series have been passed in, we're vertical.  If only Y, we're horizontal.  If both or neither, we can't plot.
            if (definition.XSeries.Count == 0 && definition.YSeries.Count == 0)
            {
                throw new ArgumentException("Must have at least one series to plot a box+whisker plot");
            }
            if (definition.XSeries.Count > 0 && definition.YSeries.Count > 0)
            {
                throw new ArgumentException("Cannot plot a box+whisker plot with both X and Y series");
            }
            List<Series> SeriesToUse = definition.YSeries.Count > 0 ? definition.YSeries : definition.XSeries;

            // sort the array and get the min, max values
            GetMinMaxSort(SeriesToUse, out dataMinX, out dataMaxX);

            ScaleParameters sp = new ScaleParameters
                                     {
                                         X = { AllowedScaleTypes = new[] { ScaleType.Linear } },
                                         Y = { AllowedScaleTypes = new[] { ScaleType.Category } }
                                     };
            sp.X.ShouldCheck = true;
            sp.Y.ShouldCheck = false;
            sp.Y.Max = 0;
            sp.Y.Min = 0;
            sp.X.Min = dataMinX;
            sp.X.Max = dataMaxX;
            return sp;
        }


        ///  <summary>
        ///  Plot a box and whisker chart.
        ///  </summary>
        ///  <remarks></remarks>
        private ParameterBag PlotBoxWhisker(Stream outputStream)
        {
            //  If only X series have been passed in, we're vertical.  If only Y, we're horizontal.  If both or neither, we can't plot.
            if (definition.XSeries.Count == 0 && definition.YSeries.Count == 0)
            {
                throw new ArgumentException("Must have at least one series to plot a box+whisker plot");
            }
            if (definition.XSeries.Count > 0 && definition.YSeries.Count > 0)
            {
                throw new ArgumentException("Cannot plot a box+whisker plot with both X and Y series");
            }
            List<Series> SeriesToUse = definition.YSeries.Count > 0 ? definition.YSeries : definition.XSeries;

            BoxWhiskerOptions bwOptions = ((BoxWhiskerOptions)(definition.ChartOptions));

            if (IsAscii)
                return PlotBoxWhiskerAscii(SeriesToUse);
            if (bwOptions.Orientation == ChartOrientation.Horizontal)
                return PlotBoxWhiskerHorizontal(outputStream, SeriesToUse);
            return PlotBoxWhiskerVertical(outputStream, SeriesToUse);
        }


        // TRANSMISSINGCOMMENT: Method PlotBoxWhiskerHorizontal
        private ParameterBag PlotBoxWhiskerHorizontal(Stream OutputStream, List<Series> SeriesToUse)
        {

            int k = SeriesToUse.Count + 1;
            if (k > 10)
            {
                scaleYAxis = 1 + (k - 10) / 20;
                if (scaleYAxis > 5)
                {
                    scaleYAxis = 5;
                }
                metaH = scaleYAxis * DEFAULT_METAH;
            }
            else
            {
                scaleYAxis = 1;
                metaH = DEFAULT_METAH;
            }

            // sort the array and get the min, max values
            GetMinMaxSort(SeriesToUse, out dataMinX, out dataMaxX);

            BoxWhiskerOptions bwOptions = ((BoxWhiskerOptions)(definition.ChartOptions));

            double P = (1.0 - bwOptions.Cco) / 2.0;
            if (P > 1.0 - P)
                P = 1.0 - P;

            string axisTitle = bwOptions.XAxisTitle;

            // Plot a Metafile version
            StartMetafile(OutputStream, true);

            //  Fonts
            if (!(string.IsNullOrEmpty(bwOptions.AxisLabelFontDescriptor)))
            {
                axisLabelFont = FontFromSaveString(bwOptions.AxisLabelFontDescriptor);
            }
            if (!(string.IsNullOrEmpty(bwOptions.AxisFontDescriptor)))
            {
                axisTitleFont = FontFromSaveString(bwOptions.AxisFontDescriptor);
            }
            if (!(string.IsNullOrEmpty(bwOptions.TitleFontDescriptor)))
            {
                titleFont = FontFromSaveString(bwOptions.TitleFontDescriptor);
            }

            // Draw the scale
            DefaultAxes();
            AssignMarkersToSeries();
            double xtra = 0;
            foreach (Series s in SeriesToUse)
            {
                double w = canvas.MeasureString(s.Title, axisLabelFont).Width + 20;
                if (w > xtra + xAxisCanvas)
                {
                    xtra = w - xAxisCanvas;
                }
            }
            DrawAxes(definition.ChartOptions.Title, new Axis(axisTitle, AxisMode.Scale, 0, definition.ScaleParameters.X.ScaleType), new Axis(null, AxisMode.Series, xtra, definition.ScaleParameters.Y.ScaleType), false, true, false);

            divx = axisXMax - axisXMin;
            offx = -(axisXMin / divx * xExtCanvas) + xAxisCanvas;
            divy = SeriesToUse.Count;
            offy = -(0 / divy * yExtCanvas) + yAxisCanvas;

            using (Pen blackPen = GetPen(_markerTypes[10], true))
            {
                using (Pen dottedBlackPen = GetPen(_markerTypes[10], true))
                {
                    dottedBlackPen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dot;

                    // work through the columns
                    for (int c = 0; c <= SeriesToUse.Count - 1; c++)
                    {
                        DoubleSeries s = SeriesToUse[c].AsDoubleSeries;
                        double centre = 0;
                        double boxL = 0;
                        double boxR = 0;
                        double innerFenceL = 0;
                        double innerFenceR = 0;
                        double outerFenceL = 0;
                        double outerFenceR = 0;
                        double otherMark = 0;
                        bool centreIsMedian = false;
                        PlotBoxWhiskerCalc(s, bwOptions.Method, P, ref centre, ref boxL, ref boxR, ref innerFenceL, ref innerFenceR, bwOptions.UseInnerFence, ref outerFenceL, ref outerFenceR, bwOptions.UseOuterFence, ref otherMark, ref centreIsMedian);

                        // Plot graphic
                        double yctr = (c + 0.5) / divy * yExtCanvas;
                        double ytop = (c + 1) / divy * yExtCanvas;
                        double centreX = ToCanvasX(centre);

                        double halfBoxHeight = (ytop - yctr) * BOXWHISKER_BOX_FRACTION_OF_SPACE;
                        double yc = offy + yctr;
                        double yt = yc + halfBoxHeight;
                        double yb = yc - halfBoxHeight;

                        double boxLX = ToCanvasX(boxL);
                        double boxRX = ToCanvasX(boxR);

                        // Draw marker, centre line and box
                        DrawRectangle(blackPen, boxLX, yt, boxRX - boxLX, yt - yb); //  Box
                        if (bwOptions.MarkMeanAndMedian)
                        {
                            //  Other mark
                            double otherMarkX = ToCanvasX(otherMark);
                            MarkerShape otherMarkShape;
                            double otherMarkScaleFactor;
                            if (centreIsMedian)
                            {
                                //  Other marker is mean (x)
                                otherMarkShape = MarkerShape.Cross;
                                otherMarkScaleFactor = 0.5;
                            }
                            else
                            {
                                //  Other marker is median (x)
                                otherMarkShape = MarkerShape.Cross;
                                otherMarkScaleFactor = 0.5;
                            }
                            DrawMarker(otherMarkX, yc, 21 * otherMarkScaleFactor, otherMarkShape, false, blackPen);
                        }
                        DrawLine(blackPen, centreX, yt, centreX, yb); //  Centre line

                        //  Draw whiskers, fences etc.
                        double halfWhiskerHeight = halfBoxHeight * BOXWHISKER_WHISKER_FRACTION_OF_BOX;
                        yt = yc + halfWhiskerHeight;
                        yb = yc - halfWhiskerHeight;

                        //  Left-hand fences
                        bool gatedInnerL = bwOptions.Method != BoxWhiskerOptions.BoxWhiskerMethod.SevenNumberSummary && bwOptions.Method != BoxWhiskerOptions.BoxWhiskerMethod.BowleySummary && s.Data[0] < innerFenceL && innerFenceL < boxL;
                        bool gatedOuterL = s.Data[0] < outerFenceL && outerFenceL < boxL;

                        // double outerFenceLX = ToCanvasX(gatedOuterL ? outerFenceL : s.Data[ 0 ]);

                        //  Draw inner fence
                        //  The inner fence goes to the first one of:
                        //  - The inner fence for seven number and Bowley plots;
                        //  - The inner fence if both inner and outer fences are selected and there's at least one outlier beyond it;
                        //  - Not drawn otherwise.
                        bool shouldDrawInnerFenceL = false;
                        double innerFenceLX = 0;
                        if (bwOptions.Method == BoxWhiskerOptions.BoxWhiskerMethod.SevenNumberSummary || bwOptions.Method == BoxWhiskerOptions.BoxWhiskerMethod.BowleySummary)
                        {
                            shouldDrawInnerFenceL = true;
                            innerFenceLX = ToCanvasX(innerFenceL);
                        }
                        if (shouldDrawInnerFenceL)
                        {
                            Pen innerPen;
                            if (bwOptions.UseOuterFence || bwOptions.Method == BoxWhiskerOptions.BoxWhiskerMethod.SevenNumberSummary || bwOptions.Method == BoxWhiskerOptions.BoxWhiskerMethod.BowleySummary)
                            {
                                innerPen = dottedBlackPen;
                            }
                            else
                            {
                                innerPen = blackPen;
                            }
                            DrawLine(innerPen, innerFenceLX, yt, innerFenceLX, yb);
                        }

                        //  Draw min whisker
                        //  The min whisker goes to the first one of:
                        //  - The outer fence for seven number and Bowley plots;
                        //  - The first data point inside the inner fence if inner fence is selected;
                        //  - The first data point inside the outer fence if outer fence is selected;
                        //  - The min data point otherwise.
                        double minWhiskerL = 0;
                        if (bwOptions.Method == BoxWhiskerOptions.BoxWhiskerMethod.SevenNumberSummary || bwOptions.Method == BoxWhiskerOptions.BoxWhiskerMethod.BowleySummary)
                        {
                            minWhiskerL = outerFenceL;
                        }
                        else if (bwOptions.UseInnerFence)
                        {
                            for (int i = 0; i <= s.Data.Length - 1; i++)
                            {
                                if (s.Data[i] >= innerFenceL)
                                {
                                    minWhiskerL = s.Data[i];
                                    break; /* TRANSWARNING: check that break is in correct scope */
                                }
                            }
                        }
                        else if (bwOptions.UseOuterFence)
                        {
                            for (int i = 0; i <= s.Data.Length - 1; i++)
                            {
                                if (s.Data[i] >= outerFenceL)
                                {
                                    minWhiskerL = s.Data[i];
                                    break; /* TRANSWARNING: check that break is in correct scope */
                                }
                            }
                        }
                        else
                        {
                            minWhiskerL = s.Data[0];
                        }
                        double minWhiskerLX = ToCanvasX(minWhiskerL);

                        //  Draw min whisker to outer limit
                        DrawLine(blackPen, minWhiskerLX, yc, boxLX, yc);

                        //  Draw outer marker
                        // ReSharper disable ConvertToConstant.Local
                        bool shouldDrawOuterFenceL = true;
                        // ReSharper restore ConvertToConstant.Local
                        // If (gatedInnerL OrElse gatedOuterL) _
                        //     AndAlso Not (bwOptions.Method = BoxWhiskerOptions.BoxWhiskerMethod.SevenNumberSummary OrElse bwOptions.Method = BoxWhiskerOptions.BoxWhiskerMethod.BowleySummary) Then 'bwOptions.UseInnerFence AndAlso (Not bwOptions.UseOuterFence) AndAlso gatedInnerL Then
                        //  At least one inner outlier, and we're not using the outer fence.  The inner fence will have been drawn; we should not draw this as well.
                        // shouldDrawOuterFenceL = False
                        // End If
                        bool shouldDrawOuterBracketL = shouldDrawOuterFenceL && !((bwOptions.Method == BoxWhiskerOptions.BoxWhiskerMethod.SevenNumberSummary || bwOptions.Method == BoxWhiskerOptions.BoxWhiskerMethod.BowleySummary)) && !((gatedOuterL || gatedInnerL));
                        if (shouldDrawOuterFenceL)
                        {
                            DrawLine(blackPen, minWhiskerLX, yt, minWhiskerLX, yb);
                            if (shouldDrawOuterBracketL)
                            {
                                DrawLine(blackPen, minWhiskerLX + BOXWHISKER_WHISKER_END_LENGTH, yt, minWhiskerLX, yt);
                                DrawLine(blackPen, minWhiskerLX, yb, minWhiskerLX + BOXWHISKER_WHISKER_END_LENGTH, yb);
                            }
                        }

                        //  Min outliers - below outer fence
                        const double outlierRadius = BOXWHISKER_OUTLIER_RADIUS;
                        if (gatedInnerL)
                        {
                            for (int r = 0; r <= s.Data.Length - 1; r++)
                            {
                                if (s.Data[r] < innerFenceL && (s.Data[r] >= outerFenceL || !(gatedOuterL)))
                                {
                                    double x1 = ToCanvasX(s.Data[r]);
                                    DrawMarker(x1, yc, 2 * outlierRadius, MarkerShape.Circle, false, blackPen);
                                }
                            }
                        }
                        if (gatedOuterL)
                        {
                            for (int r = 0; r <= s.Data.Length - 1; r++)
                            {
                                if (s.Data[r] < outerFenceL)
                                {
                                    double x1 = ToCanvasX(s.Data[r]);
                                    DrawMarker(x1, yc, 2 * outlierRadius, MarkerShape.Circle, true, blackPen);
                                }
                            }
                        }

                        //  Right-hand fences
                        bool gatedInnerR = bwOptions.Method != BoxWhiskerOptions.BoxWhiskerMethod.SevenNumberSummary && bwOptions.Method != BoxWhiskerOptions.BoxWhiskerMethod.BowleySummary && s.Data[s.Data.Length - 1] > innerFenceR && innerFenceR > boxR;
                        bool gatedOuterR = s.Data[s.Data.Length - 1] > outerFenceR && outerFenceR > boxR;

                        // double outerFenceRX = ToCanvasX(gatedOuterR ? outerFenceR : s.Data[ s.Data.Length - 1 ]);

                        //  Draw inner fence
                        //  The inner fence goes to the first one of:
                        //  - The inner fence for seven number and Bowley plots;
                        //  - The inner fence if both inner and outer fences are selected and there's at least one outlier betond it;
                        //  - Not drawn otherwise.
                        bool shouldDrawInnerFenceR = false;
                        double innerFenceRX = 0;
                        if (bwOptions.Method == BoxWhiskerOptions.BoxWhiskerMethod.SevenNumberSummary || bwOptions.Method == BoxWhiskerOptions.BoxWhiskerMethod.BowleySummary)
                        {
                            shouldDrawInnerFenceR = true;
                            innerFenceRX = ToCanvasX(innerFenceR);
                            // ElseIf bwOptions.UseInnerFence AndAlso gatedInnerR Then
                            //     shouldDrawInnerFenceR = True
                            //     innerFenceRX = ToCanvasX(innerFenceR)
                        }
                        if (shouldDrawInnerFenceR)
                        {
                            Pen innerPen;
                            if (bwOptions.UseOuterFence || bwOptions.Method == BoxWhiskerOptions.BoxWhiskerMethod.SevenNumberSummary || bwOptions.Method == BoxWhiskerOptions.BoxWhiskerMethod.BowleySummary)
                            {
                                innerPen = dottedBlackPen;
                            }
                            else
                            {
                                innerPen = blackPen;
                            }
                            DrawLine(innerPen, innerFenceRX, yt, innerFenceRX, yb);
                        }

                        //  Draw max whisker
                        //  The max whisker goes to the first one of:
                        //  - The outer fence for seven number and Bowley plots;
                        //  - The last data point below the inner fence if inner fence is selected;
                        //  - The last data point below the outer fence if outer fence is selected;
                        //  - The max data point otherwise.
                        double maxWhiskerR = 0;
                        if (bwOptions.Method == BoxWhiskerOptions.BoxWhiskerMethod.SevenNumberSummary || bwOptions.Method == BoxWhiskerOptions.BoxWhiskerMethod.BowleySummary)
                        {
                            maxWhiskerR = outerFenceR;
                        }
                        else if (bwOptions.UseInnerFence)
                        {
                            for (int i = s.Data.Length - 1; i >= 0; i--)
                            {
                                if (s.Data[i] <= innerFenceR)
                                {
                                    maxWhiskerR = s.Data[i];
                                    break; /* TRANSWARNING: check that break is in correct scope */
                                }
                            }
                        }
                        else if (bwOptions.UseOuterFence)
                        {
                            for (int i = s.Data.Length - 1; i >= 0; i--)
                            {
                                if (s.Data[i] <= outerFenceR)
                                {
                                    maxWhiskerR = s.Data[i];
                                    break; /* TRANSWARNING: check that break is in correct scope */
                                }
                            }
                        }
                        else
                        {
                            maxWhiskerR = s.Data[s.Data.Length - 1];
                        }
                        double maxWhiskerRX = ToCanvasX(maxWhiskerR);
                        DrawLine(blackPen, maxWhiskerRX, yc, boxRX, yc);

                        //  Outer fence
                        // ReSharper disable ConvertToConstant.Local
                        bool shouldDrawOuterFenceR = true;
                        // ReSharper restore ConvertToConstant.Local
                        // If (gatedInnerR OrElse gatedOuterR) _
                        //     AndAlso Not (bwOptions.Method = BoxWhiskerOptions.BoxWhiskerMethod.SevenNumberSummary OrElse bwOptions.Method = BoxWhiskerOptions.BoxWhiskerMethod.BowleySummary) Then
                        // If bwOptions.UseInnerFence AndAlso (Not bwOptions.UseOuterFence) AndAlso gatedInnerR Then
                        //  At least one inner outlier, and we're not using the outer fence.  The inner fence will have been drawn; we should not draw this as well.
                        // shouldDrawOuterFenceR = False
                        // End If
                        bool shouldDrawOuterBracketR = shouldDrawOuterFenceR && !((bwOptions.Method == BoxWhiskerOptions.BoxWhiskerMethod.SevenNumberSummary || bwOptions.Method == BoxWhiskerOptions.BoxWhiskerMethod.BowleySummary)) && !((gatedOuterR || gatedInnerR));
                        if (shouldDrawOuterFenceR)
                        {
                            DrawLine(blackPen, maxWhiskerRX, yt, maxWhiskerRX, yb);
                            if (shouldDrawOuterBracketR)
                            {
                                DrawLine(blackPen, maxWhiskerRX - BOXWHISKER_WHISKER_END_LENGTH, yt, maxWhiskerRX, yt);
                                DrawLine(blackPen, maxWhiskerRX, yb, maxWhiskerRX - BOXWHISKER_WHISKER_END_LENGTH, yb);
                            }
                        }

                        //  Max outliers
                        if (gatedInnerR)
                        {
                            for (int r = 0; r <= s.Data.Length - 1; r++)
                            {
                                if (s.Data[r] > innerFenceR && (s.Data[r] <= outerFenceR || !(gatedOuterR)))
                                {
                                    double x1 = ToCanvasX(s.Data[r]);
                                    DrawMarker(x1, yc, 2 * outlierRadius, MarkerShape.Circle, false, blackPen);
                                }
                            }
                        }
                        if (gatedOuterR)
                        {
                            for (int r = 0; r <= s.Data.Length - 1; r++)
                            {
                                if (s.Data[r] > outerFenceR)
                                {
                                    double x1 = ToCanvasX(s.Data[r]);
                                    DrawMarker(x1, yc, 2 * outlierRadius, MarkerShape.Circle, true, blackPen);
                                }
                            }
                        }
                    }

                }
            }
            MaybeDrawMarkerLines();
            EndMetafile();
            return new ParameterBag();
        }


        // TRANSMISSINGCOMMENT: Method PlotBoxWhiskerVertical
        private ParameterBag PlotBoxWhiskerVertical(Stream OutputStream, List<Series> SeriesToUse)
        {

            int k = SeriesToUse.Count + 1;
            if (k > 10)
            {
                scaleXAxis = 1 + (k - 10) / 20;
                if (scaleXAxis > 5)
                {
                    scaleXAxis = 5;
                }
                MetaW = scaleXAxis * DEFAULT_METAW;
            }
            else
            {
                scaleXAxis = 1;
                MetaW = DEFAULT_METAW;
            }

            // sort the array and get the min, max values
            GetMinMaxSort(SeriesToUse, out dataMinX, out dataMaxX);

            BoxWhiskerOptions bwOptions = ((BoxWhiskerOptions)(definition.ChartOptions));

            double P = (1.0 - bwOptions.Cco) / 2.0;
            if (P > 1.0 - P)
                P = 1.0 - P;

            string axisTitle = bwOptions.XAxisTitle;

            // Plot a Metafile version
            StartMetafile(OutputStream, true);

            //  Fonts
            if (!(string.IsNullOrEmpty(bwOptions.AxisLabelFontDescriptor)))
            {
                axisLabelFont = FontFromSaveString(bwOptions.AxisLabelFontDescriptor);
            }
            if (!(string.IsNullOrEmpty(bwOptions.AxisFontDescriptor)))
            {
                axisTitleFont = FontFromSaveString(bwOptions.AxisFontDescriptor);
            }
            if (!(string.IsNullOrEmpty(bwOptions.TitleFontDescriptor)))
            {
                titleFont = FontFromSaveString(bwOptions.TitleFontDescriptor);
            }

            // Draw the scale
            DefaultAxes();
            AssignMarkersToSeries();
            //  Not horizontal, so vertical

            //  Swap over the X and Y axis definitions, as we've flipped the drawing
            definition = definition.Clone(); //  Make sure the swaps are safe!
            AxisScaleParameters temp = definition.ScaleParameters.X;
            definition.ScaleParameters.X = definition.ScaleParameters.Y;
            definition.ScaleParameters.Y = temp;

            //  Ensure the X and Y series are where we need them to be for drawing axes
            List<Series> tempSeries = definition.YSeries;
            definition.YSeries = definition.XSeries;
            definition.XSeries = tempSeries;

            //  Get overall minima and maxima
            double min = double.MaxValue;
            double max = double.MinValue;
            foreach (DoubleSeries s in SeriesToUse)
            {
                min = Math.Min(min, s.Min);
                max = Math.Max(max, s.Max);
            }

            int div;
            double zmin = 0;
            double zint = 0;
            Q_AxisOrFromDefinition(ref min, ref max, out div, ref zmin, ref zint, out minorTicsPerMajorTic, true, definition.ScaleParameters.Y.ScaleType, false);
            string msk = AxisMaskOrFromDefinition(zint, zmin, div, minorTicsPerMajorTic, true, definition.ScaleParameters.Y.ScaleType, false);

            float xtra = 0;
            float w = canvas.MeasureString(min.ToString(msk), axisLabelFont).Width;
            //  Allow 20 units for axes; if we need more, offset the axis
            if (w - 20 > xtra)
            {
                xtra = w - 20;
            }
            w = canvas.MeasureString(max.ToString(msk), axisLabelFont).Width;
            if (w - 20 > xtra)
            {
                xtra = w - 20;
            }
            //  Offset the axis label

            DrawAxes(definition.ChartOptions.Title, new Axis(null, AxisMode.Series, 0, definition.ScaleParameters.X.ScaleType), new Axis(axisTitle, AxisMode.Scale, xtra, definition.ScaleParameters.Y.ScaleType), false, true, false);

            divy = axisYMax - axisYMin;
            offy = -(axisYMin / divy * yExtCanvas) + yAxisCanvas;
            divx = SeriesToUse.Count;
            offx = -(0 / divx * xExtCanvas) + xAxisCanvas;

            using (Pen blackPen = GetPen(_markerTypes[10], true))
            {
                using (Pen dottedBlackPen = GetPen(_markerTypes[10], true))
                {
                    dottedBlackPen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dot;

                    // work through the columns
                    for (int c = 0; c < SeriesToUse.Count; c++)
                    {
                        DoubleSeries s = SeriesToUse[c].AsDoubleSeries;
                        double centre = 0;
                        double boxB = 0;
                        double boxT = 0;
                        double innerFenceB = 0;
                        double innerFenceT = 0;
                        double outerFenceB = 0;
                        double outerFenceT = 0;
                        double otherMark = 0;
                        bool CentreIsMedian = false;
                        PlotBoxWhiskerCalc(s, bwOptions.Method, P, ref centre, ref boxB, ref boxT, ref innerFenceB, ref innerFenceT, bwOptions.UseInnerFence, ref outerFenceB, ref outerFenceT, bwOptions.UseOuterFence, ref otherMark, ref CentreIsMedian);

                        // Plot graphic
                        double xctr = (c + 0.5) / divx * xExtCanvas;
                        double xright = (c + 1) / divx * xExtCanvas;
                        double centreY = ToCanvasY(centre);

                        double halfBoxWidth = (xright - xctr) * BOXWHISKER_BOX_FRACTION_OF_SPACE;
                        double xc = offx + xctr;
                        double xr = xc + halfBoxWidth;
                        double xl = xc - halfBoxWidth;

                        double boxBY = ToCanvasY(boxB);
                        double boxTY = ToCanvasY(boxT);

                        // Draw marker, centre line and box
                        DrawRectangle(blackPen, xl, boxTY, xr - xl, boxTY - boxBY); //  Box
                        if (bwOptions.MarkMeanAndMedian)
                        {
                            //  Other mark
                            double otherMarky = ToCanvasY(otherMark);
                            const double otherMarkScaleFactor = 0.5;
                            const MarkerShape otherMarkShape = MarkerShape.Cross;
                            DrawMarker(xc, otherMarky, 21 * otherMarkScaleFactor, otherMarkShape, false, blackPen);
                        }
                        DrawLine(blackPen, xr, centreY, xl, centreY); //  Centre line

                        //  Draw whiskers, fences etc.
                        double halfWhiskerWidth = halfBoxWidth * BOXWHISKER_WHISKER_FRACTION_OF_BOX;
                        xr = xc + halfWhiskerWidth;
                        xl = xc - halfWhiskerWidth;

                        //  Left-hand fences
                        bool gatedInnerB = bwOptions.Method != BoxWhiskerOptions.BoxWhiskerMethod.SevenNumberSummary && bwOptions.Method != BoxWhiskerOptions.BoxWhiskerMethod.BowleySummary && s.Data[0] < innerFenceB && innerFenceB < boxB;
                        bool gatedOuterB = s.Data[0] < outerFenceB && outerFenceB < boxB;

                        // double outerFenceBY = ToCanvasY(gatedOuterB ? outerFenceB : s.Data[ 0 ]);

                        //  Draw inner fence
                        //  The inner fence goes to the first one of:
                        //  - The inner fence for seven number and Bowley plots;
                        //  - The inner fence if both inner and outer fences are selected and there's at least one outlier beyond it;
                        //  - Not drawn otherwise.
                        bool shouldDrawInnerFenceB = false;
                        double innerFenceBY = 0;
                        if (bwOptions.Method == BoxWhiskerOptions.BoxWhiskerMethod.SevenNumberSummary || bwOptions.Method == BoxWhiskerOptions.BoxWhiskerMethod.BowleySummary)
                        {
                            shouldDrawInnerFenceB = true;
                            innerFenceBY = ToCanvasY(innerFenceB);
                        }
                        if (shouldDrawInnerFenceB)
                        {
                            Pen innerPen;
                            if (bwOptions.UseOuterFence || bwOptions.Method == BoxWhiskerOptions.BoxWhiskerMethod.SevenNumberSummary || bwOptions.Method == BoxWhiskerOptions.BoxWhiskerMethod.BowleySummary)
                            {
                                innerPen = dottedBlackPen;
                            }
                            else
                            {
                                innerPen = blackPen;
                            }
                            DrawLine(innerPen, xr, innerFenceBY, xl, innerFenceBY);
                        }

                        //  Draw min whisker
                        //  The min whisker goes to the first one of:
                        //  - The outer fence for seven number and Bowley plots;
                        //  - The first data point inside the inner fence if inner fence is selected;
                        //  - The first data point inside the outer fence if outer fence is selected;
                        //  - The min data point otherwise.
                        double minWhiskerB = 0;
                        if (bwOptions.Method == BoxWhiskerOptions.BoxWhiskerMethod.SevenNumberSummary || bwOptions.Method == BoxWhiskerOptions.BoxWhiskerMethod.BowleySummary)
                        {
                            minWhiskerB = outerFenceB;
                        }
                        else if (bwOptions.UseInnerFence)
                        {
                            for (int i = 0; i <= s.Data.Length - 1; i++)
                            {
                                if (s.Data[i] >= innerFenceB)
                                {
                                    minWhiskerB = s.Data[i];
                                    break; /* TRANSWARNING: check that break is in correct scope */
                                }
                            }
                        }
                        else if (bwOptions.UseOuterFence)
                        {
                            for (int i = 0; i <= s.Data.Length - 1; i++)
                            {
                                if (s.Data[i] >= outerFenceB)
                                {
                                    minWhiskerB = s.Data[i];
                                    break; /* TRANSWARNING: check that break is in correct scope */
                                }
                            }
                        }
                        else
                        {
                            minWhiskerB = s.Data[0];
                        }
                        double minWhiskerBY = ToCanvasY(minWhiskerB);

                        //  Draw min whisker to outer limit
                        DrawLine(blackPen, xc, minWhiskerBY, xc, boxBY);

                        //  Draw outer marker
                        const bool shouldDrawOuterFenceB = true;
                        // If (gatedInnerB OrElse gatedOuterB) _
                        //     AndAlso Not (bwOptions.Method = BoxWhiskerOptions.BoxWhiskerMethod.SevenNumberSummary OrElse bwOptions.Method = BoxWhiskerOptions.BoxWhiskerMethod.BowleySummary) Then 'bwOptions.UseInnerFence AndAlso (Not bwOptions.UseOuterFence) AndAlso gatedInnerL Then
                        //  At least one inner outlier, and we're not using the outer fence.  The inner fence will have been drawn; we should not draw this as well.
                        // shouldDrawOuterFenceB = False
                        // End If
                        // ReSharper disable RedundantLogicalConditionalExpressionOperand
                        bool shouldDrawOuterBracketB = shouldDrawOuterFenceB && !((bwOptions.Method == BoxWhiskerOptions.BoxWhiskerMethod.SevenNumberSummary || bwOptions.Method == BoxWhiskerOptions.BoxWhiskerMethod.BowleySummary)) && !((gatedOuterB || gatedInnerB));
                        // ReSharper restore RedundantLogicalConditionalExpressionOperand
                        if (shouldDrawOuterFenceB)
                        {
                            DrawLine(blackPen, xr, minWhiskerBY, xl, minWhiskerBY);
                            if (shouldDrawOuterBracketB)
                            {
                                DrawLine(blackPen, xr, minWhiskerBY + BOXWHISKER_WHISKER_END_LENGTH, xr, minWhiskerBY);
                                DrawLine(blackPen, xl, minWhiskerBY, xl, minWhiskerBY + BOXWHISKER_WHISKER_END_LENGTH);
                            }
                        }

                        //  Min outliers - below outer fence
                        if (gatedInnerB)
                        {
                            for (int r = 0; r <= s.Data.Length - 1; r++)
                            {
                                if (s.Data[r] < innerFenceB && (s.Data[r] >= outerFenceB || !(gatedOuterB)))
                                {
                                    double y1 = ToCanvasY(s.Data[r]);
                                    DrawMarker(xc, y1, 2 * BOXWHISKER_OUTLIER_RADIUS, MarkerShape.Circle, false, blackPen);
                                }
                            }
                        }
                        if (gatedOuterB)
                        {
                            for (int r = 0; r <= s.Data.Length - 1; r++)
                            {
                                if (s.Data[r] < outerFenceB)
                                {
                                    double y1 = ToCanvasY(s.Data[r]);
                                    DrawMarker(xc, y1, 2 * BOXWHISKER_OUTLIER_RADIUS, MarkerShape.Circle, true, blackPen);
                                }
                            }
                        }

                        //  Right-hand fences
                        bool gatedInnerT = bwOptions.Method != BoxWhiskerOptions.BoxWhiskerMethod.SevenNumberSummary && bwOptions.Method != BoxWhiskerOptions.BoxWhiskerMethod.BowleySummary && s.Data[s.Data.Length - 1] > innerFenceT && innerFenceT > boxT;
                        bool gatedOuterT = s.Data[s.Data.Length - 1] > outerFenceT && outerFenceT > boxT;

                        // double outerFenceTY = ToCanvasY(gatedOuterT ? outerFenceT : s.Data[ s.Data.Length - 1 ]);

                        //  Draw inner fence
                        //  The inner fence goes to the first one of:
                        //  - The inner fence for seven number and Bowley plots;
                        //  - The inner fence if both inner and outer fences are selected and there's at least one outlier betond it;
                        //  - Not drawn otherwise.
                        bool shouldDrawInnerFenceT = false;
                        double innerFenceTY = 0;
                        if (bwOptions.Method == BoxWhiskerOptions.BoxWhiskerMethod.SevenNumberSummary || bwOptions.Method == BoxWhiskerOptions.BoxWhiskerMethod.BowleySummary)
                        {
                            shouldDrawInnerFenceT = true;
                            innerFenceTY = ToCanvasY(innerFenceT);
                        }
                        if (shouldDrawInnerFenceT)
                        {
                            Pen innerPen;
                            if (bwOptions.UseOuterFence || bwOptions.Method == BoxWhiskerOptions.BoxWhiskerMethod.SevenNumberSummary || bwOptions.Method == BoxWhiskerOptions.BoxWhiskerMethod.BowleySummary)
                            {
                                innerPen = dottedBlackPen;
                            }
                            else
                            {
                                innerPen = blackPen;
                            }
                            DrawLine(innerPen, xr, innerFenceTY, xl, innerFenceTY);
                        }

                        //  Draw max whisker
                        //  The max whisker goes to the first one of:
                        //  - The outer fence for seven number and Bowley plots;
                        //  - The last data point below the inner fence if inner fence is selected;
                        //  - The last data point below the outer fence if outer fence is selected;
                        //  - The max data point otherwise.
                        double maxWhiskerT = 0;
                        if (bwOptions.Method == BoxWhiskerOptions.BoxWhiskerMethod.SevenNumberSummary || bwOptions.Method == BoxWhiskerOptions.BoxWhiskerMethod.BowleySummary)
                        {
                            maxWhiskerT = outerFenceT;
                        }
                        else if (bwOptions.UseInnerFence)
                        {
                            for (int i = s.Data.Length - 1; i >= 0; i--)
                            {
                                if (s.Data[i] <= innerFenceT)
                                {
                                    maxWhiskerT = s.Data[i];
                                    break; /* TRANSWARNING: check that break is in correct scope */
                                }
                            }
                        }
                        else if (bwOptions.UseOuterFence)
                        {
                            for (int i = s.Data.Length - 1; i >= 0; i--)
                            {
                                if (s.Data[i] <= outerFenceT)
                                {
                                    maxWhiskerT = s.Data[i];
                                    break; /* TRANSWARNING: check that break is in correct scope */
                                }
                            }
                        }
                        else
                        {
                            maxWhiskerT = s.Data[s.Data.Length - 1];
                        }
                        double maxWhiskerTY = ToCanvasY(maxWhiskerT);
                        DrawLine(blackPen, xc, maxWhiskerTY, xc, boxTY);

                        //  Outer fence
                        const bool shouldDrawOuterFenceT = true;
                        // If (gatedInnerT OrElse gatedOuterT) _
                        //     AndAlso Not (bwOptions.Method = BoxWhiskerOptions.BoxWhiskerMethod.SevenNumberSummary OrElse bwOptions.Method = BoxWhiskerOptions.BoxWhiskerMethod.BowleySummary) Then
                        // If bwOptions.UseInnerFence AndAlso (Not bwOptions.UseOuterFence) AndAlso gatedInnerR Then
                        //  At least one inner outlier, and we're not using the outer fence.  The inner fence will have been drawn; we should not draw this as well.
                        // shouldDrawOuterFenceT = False
                        //  End If
                        // ReSharper disable RedundantLogicalConditionalExpressionOperand
                        bool shouldDrawOuterBracketT = shouldDrawOuterFenceT && !((bwOptions.Method == BoxWhiskerOptions.BoxWhiskerMethod.SevenNumberSummary || bwOptions.Method == BoxWhiskerOptions.BoxWhiskerMethod.BowleySummary)) && !((gatedOuterT || gatedInnerT));
                        // ReSharper restore RedundantLogicalConditionalExpressionOperand
                        if (shouldDrawOuterFenceT)
                        {
                            DrawLine(blackPen, xr, maxWhiskerTY, xl, maxWhiskerTY);
                            if (shouldDrawOuterBracketT)
                            {
                                DrawLine(blackPen, xr, maxWhiskerTY - BOXWHISKER_WHISKER_END_LENGTH, xr, maxWhiskerTY);
                                DrawLine(blackPen, xl, maxWhiskerTY, xl, maxWhiskerTY - BOXWHISKER_WHISKER_END_LENGTH);
                            }
                        }

                        //  Max outliers
                        if (gatedInnerT)
                        {
                            for (int r = 0; r <= s.Data.Length - 1; r++)
                            {
                                if (s.Data[r] > innerFenceT && (s.Data[r] <= outerFenceT || !(gatedOuterT)))
                                {
                                    double y1 = ToCanvasY(s.Data[r]);
                                    DrawMarker(xc, y1, 2 * BOXWHISKER_OUTLIER_RADIUS, MarkerShape.Circle, false, blackPen);
                                }
                            }
                        }
                        if (gatedOuterT)
                        {
                            for (int r = 0; r <= s.Data.Length - 1; r++)
                            {
                                if (s.Data[r] > outerFenceT)
                                {
                                    double y1 = ToCanvasY(s.Data[r]);
                                    DrawMarker(xc, y1, 2 * BOXWHISKER_OUTLIER_RADIUS, MarkerShape.Circle, true, blackPen);
                                }
                            }
                        }
                    }

                }
            }
            MaybeDrawMarkerLines();
            EndMetafile();
            return new ParameterBag();
        }


        // TRANSMISSINGCOMMENT: Method PlotBoxWhiskerAscii
        private ParameterBag PlotBoxWhiskerAscii(List<Series> SeriesToUse)
        {
            // sort the array and get the min, max values
            GetMinMaxSort(SeriesToUse, out dataMinX, out dataMaxX);

            BoxWhiskerOptions bwOptions = ((BoxWhiskerOptions)(definition.ChartOptions));

            double P = (1.0 - bwOptions.Cco) / 2.0;
            if (P > 1.0 - P)
                P = 1.0 - P;

            ASCII_InitPlot(SeriesToUse.Count * 2 + 4);

            // Draw the scale
            DrawAxes(definition.ChartOptions.Title + "\r\n", new Axis(bwOptions.XAxisTitle, AxisMode.Scale, 0, definition.ScaleParameters.X.ScaleType), new Axis(null, AxisMode.Series, 0, definition.ScaleParameters.X.ScaleType), false, true, false);

            if (shTx[0].Length > bwOptions.XAxisTitle.Length)
            {
                WriteAsciiYX(0, 45 - bwOptions.XAxisTitle.Length / 2, bwOptions.XAxisTitle);
            }
            else
            {
                //  Axis title is larger than the chart, so replace the entire first string
                shTx[0] = bwOptions.XAxisTitle;
            }

            divx = axisXMax - axisXMin;
            offx = Convert.ToInt32(-(axisXMin / divx * 60) + 16);
            divy = SeriesToUse.Count + 1;
            offy = Convert.ToInt32(-(0 / divy * 20) + ASCII_Ytxt);

            // work through the columns
            for (int c = 0; c <= SeriesToUse.Count - 1; c++)
            {
                DoubleSeries s = SeriesToUse[c].AsDoubleSeries;
                double mdn = 0; double Q1 = 0; double Q3 = 0;
                double innerFenceL = 0; double innerFenceR = 0;
                double outerFenceL = 0; double outerFenceR = 0;
                double otherMark = 0;
                bool centreIsMedian = false;
                PlotBoxWhiskerCalc(s, bwOptions.Method, P, ref mdn, ref Q1, ref Q3, ref innerFenceL, ref innerFenceR, bwOptions.UseInnerFence, ref outerFenceL, ref outerFenceR, bwOptions.UseOuterFence, ref otherMark, ref centreIsMedian);

                bool gatedl;
                int XL;
                if (s.Data[0] < outerFenceL & outerFenceL < Q1)
                {
                    XL = Convert.ToInt32(offx + outerFenceL / divx * 60);
                    gatedl = true;
                }
                else
                {
                    XL = Convert.ToInt32(offx + s.Data[0] / divx * 60);
                    gatedl = false;
                }

                bool gatedr;
                int XR;
                if (s.Data[s.Data.Length - 1] > outerFenceR & outerFenceR > Q3)
                {
                    XR = Convert.ToInt32(offx + outerFenceR / divx * 60);
                    gatedr = true;
                }
                else
                {
                    XR = Convert.ToInt32(offx + s.Data[s.Data.Length - 1] / divx * 60);
                    gatedr = false;
                }

                int XM = Convert.ToInt32(offx + (mdn / divx * 60));

                int LQ = Convert.ToInt32(offx + (Q1 / divx * 60));
                int UQ = Convert.ToInt32(offx + (Q3 / divx * 60));

                // Plot it
                int Y2 = 3 + c * 2;
                WriteAsciiYX(Y2, LQ, new string('.', UQ - LQ));
                WriteAsciiYX(Y2, XM, "*");

                int L;
                if (gatedl)
                {
                    L = LQ - XL;
                    if (L < 2)
                    {
                        L = 2;
                    }
                    WriteAsciiYX(Y2, XL, "|" + new string('-', L - 2) + "[");
                    for (int r = 0; r <= s.Data.Length - 1; r++)
                    {
                        if (s.Data[r] < outerFenceL)
                        {
                            int x1 = Convert.ToInt32(offx + s.Data[r] / divx * 60);
                            WriteAsciiYX(Y2, x1, ".");
                        }
                    }
                }
                else
                {
                    L = LQ - XL;
                    if (L < 2)
                    {
                        L = 2;
                    }
                    WriteAsciiYX(Y2, XL, ">" + new string('-', L - 2) + "[");
                }

                if (gatedr)
                {
                    L = XR - UQ;
                    if (L < 2)
                    {
                        L = 2;
                    }
                    WriteAsciiYX(Y2, UQ, "]" + new string('-', L - 2) + "|");
                    for (int r = 0; r <= s.Data.Length - 1; r++)
                    {
                        if (s.Data[r] > outerFenceR)
                        {
                            int x1 = Convert.ToInt32(offx + s.Data[r] / divx * 60);
                            WriteAsciiYX(Y2, x1, ".");
                        }
                    }
                }
                else
                {
                    L = XR - UQ;
                    if (L < 2)
                    {
                        L = 2;
                    }
                    WriteAsciiYX(Y2, UQ, "]" + new string('-', L - 2) + "<");
                }

            }
            return new ParameterBag();
        }


        // TRANSMISSINGCOMMENT: Method GetBarScaleParameters
        private ScaleParameters GetBarScaleParameters()
        {
            BarOptions bOptions = ((BarOptions)(definition.ChartOptions));

            //  Stacked and 100% stacked charts require different scaling
            if (bOptions.Stacked)
            {
                if (bOptions.Stacked100Percent)
                {
                    DataMinY = 0;
                    DataMaxY = 100;
                    axisYMin = 0;
                    axisYMax = 100;
                }
                else
                {
                    double largestSoFar = 0;

                    for (int offset = 0; offset <= definition.YSeries[0].AsDoubleSeries.Points - 1; offset++)
                    {
                        double thisTotal = 0;
                        foreach (DoubleSeries s in definition.YSeries)
                        {
                            if (s.Data[offset] != Constant.MISSING)
                            {
                                thisTotal += s.Data[offset];
                            }
                        }
                        if (thisTotal > largestSoFar)
                        {
                            largestSoFar = thisTotal;
                        }
                    }
                    //  Ensure there's always *some* size to the axis
                    if (largestSoFar == 0)
                    {
                        largestSoFar = 1;
                    }
                    DataMinY = 0;
                    DataMaxY = largestSoFar;
                    axisYMin = 0;
                    axisYMax = largestSoFar;
                }
            }

            // Label orientation: As standard, there are 80 characters across.
            const int maxLabelChars = 80;
            int longestTitle = 0;
            foreach (string title in bOptions.SeriesTitles)
                if (title.Length > longestTitle)
                    longestTitle = title.Length;
            LabelDirection preferredLabelDirection = LabelDirection.Across;
            if (longestTitle * bOptions.SeriesTitles.Length > maxLabelChars)
                preferredLabelDirection = LabelDirection.Up;

            return new ScaleParameters
                                     {
                                         X =
                                             {
                                                 AllowedScaleTypes = new[] { ScaleType.Category },
                                                 ShouldCheck = false,
                                                 Max = 0,
                                                 Min = 0,
                                                 LabelDirection = preferredLabelDirection
                                             },
                                         Y =
                                             {
                                                 AllowedScaleTypes = new[] { ScaleType.Linear },
                                                 ShouldCheck = true,
                                                 Max = DataMaxY,
                                                 Min = DataMinY
                                             }
                                     };
        }


        ///  <summary>
        ///  Plot a bar, stacked bar or 100% stacked bar chart.
        ///  </summary>
        ///  <remarks></remarks>
        private ParameterBag PlotBar(Stream outputStream)
        {
            definition = definition.Clone();

            IList<Series> seriesToUse = definition.YSeries;
            BarOptions bOptions = ((BarOptions)(definition.ChartOptions));

            string axisTitle = bOptions.YAxisTitle;

            // If we've been asked to flip rows and columns, do so
            if (bOptions.Stacked && bOptions.RotateWhenStacked)
            {
                string[] oldSeriesTitles = bOptions.SeriesTitles;

                // The new series titles are the old series names
                string[] newSeriesTitles = new string[seriesToUse.Count];
                for (int i = 0; i < seriesToUse.Count; i++)
                    newSeriesTitles[i] = seriesToUse[i].Title;

                // One new series for each old title
                List<Series> newSeriesToUse = new List<Series>(oldSeriesTitles.Length);
                foreach (string t in oldSeriesTitles)
                {
                    Series s = new DoubleSeries(new double[seriesToUse.Count], t);
                    newSeriesToUse.Add(s);
                }

                // Rotate the data
                for (int oldSeries = 0; oldSeries < seriesToUse.Count; oldSeries++)
                    for (int oldRow = 0; oldRow < oldSeriesTitles.Length; oldRow++)
                        newSeriesToUse[oldRow].AsDoubleSeries.Data[oldSeries] = seriesToUse[oldSeries].AsDoubleSeries.Data[oldRow];

                // Assign
                bOptions.SeriesTitles = newSeriesTitles;
                definition.YSeries = newSeriesToUse;
                seriesToUse = newSeriesToUse;

                // Ensure we have enough markers
                bOptions.SetMarkers(seriesToUse);
                bOptions.MarkerTypes = MarkersFromDescriptors(bOptions.SeriesOptions, bOptions.ShouldForceIsFilled,
                                                              bOptions.ForcedIsFilled, bOptions.ShouldForceFillStyle,
                                                              bOptions.ForcedFillStyle);
            }

            //  Sort out the axes for different chart types
            if (bOptions.Stacked)
            {
                if (bOptions.Stacked100Percent)
                {
                    //  Y axis scales 0-100
                    axisYMin = 0;
                    axisYMax = 100;
                }
                else
                {
                    //  Add up the bars and scale to that maximum
                    double largestSetOfBars = 0;
                    for (int barIndex = 0; barIndex <= seriesToUse[0].AsDoubleSeries.Data.Length - 1; barIndex++)
                    {
                        //  Missing data leads to missing bars
                        double totalOfAllBars = 0;
                        for (int seriesIndex = 0; seriesIndex <= seriesToUse.Count - 1; seriesIndex++)
                        {
                            double seriesValue = seriesToUse[seriesIndex].AsDoubleSeries.Data[barIndex];
                            if (seriesValue != Constant.MISSING)
                            {
                                totalOfAllBars += seriesValue;
                            }
                        }
                        largestSetOfBars = Math.Max(largestSetOfBars, totalOfAllBars);
                    }
                    DataMaxY = largestSetOfBars;
                }
            }

            //  If there's a legend, work out how many series there are and extend the plot area as required to hold the legend

            //  Measurements and set axes.  These are done on a scratchpad canvas before the proper measurements are set up.
            double legendFontHeight;
            using (MemoryStream scratchStream = new MemoryStream())
            {
                StartMetafile(scratchStream, true);
                SetFontsAndThicknessesFromOptions(bOptions);
                DefaultAxes();
                legendFontHeight = legendFont.GetHeight(canvas);
                EndMetafile();
            }

            //  By now, all measurements are known.  Set up the plot areas.
            double legendTop = yAxisCanvas - LEGEND_TOP_GAP;
            double legendRowHeight = Math.Max(LEGEND_MARKER_SIZE, Convert.ToInt32(legendFontHeight));
            double legendSpacing = MINIMUM_LEGEND_GAP + legendRowHeight;
            if (bOptions.ShowLegend && bOptions.ShowLegendIsRelevant)
            {
                double legendBottom = legendTop - (seriesToUse.Count * legendSpacing);
                if (legendBottom < LOWEST_ALLOWED_LEGEND)
                {
                    double extraSpaceRequired = LOWEST_ALLOWED_LEGEND - legendBottom;

                    //  Add in the extra space
                    metaH += extraSpaceRequired;
                    yAxisCanvas += extraSpaceRequired;
                    legendTop += extraSpaceRequired;
                    // legendBottom += extraSpaceRequired; 
                }
            }

            //  Plot
            if (bOptions.Orientation == ChartOrientation.Horizontal)
            {

                //  Flip the series, and hence the min/max values
                definition = definition.Clone();
                List<Series> tempSeries = definition.XSeries;
                definition.XSeries = definition.YSeries;
                definition.YSeries = tempSeries;
                AxisScaleParameters tempAxisScaleParameters = definition.ScaleParameters.X;
                definition.ScaleParameters.X = definition.ScaleParameters.Y;
                definition.ScaleParameters.Y = tempAxisScaleParameters;
                DataMinX = DataMinY;
                DataMaxX = DataMaxY;
                axisXMin = axisYMin;
                axisXMax = axisYMax;
                axisYMin = 0;
                axisYMax = 0;
                DataMinY = 0;
                DataMaxY = 0;

                StartMetafile(outputStream, false);
                SetFontsAndThicknessesFromOptions(bOptions);

                // Draw the scale
                AssignMarkersToSeries(bOptions);

                double xtra = 0;
                foreach (string s in bOptions.SeriesTitles)
                {
                    double w = AxisLabelWidth(s);
                    if (w > xtra)
                        xtra = w;
                }
                xtra = Math.Max(0, Convert.ToInt32(xtra - 20));

                DrawAxes(definition.ChartOptions.Title, new Axis(axisTitle, AxisMode.Scale, 0, definition.ScaleParameters.X.ScaleType), new Axis(null, AxisMode.Series, xtra, definition.ScaleParameters.Y.ScaleType), bOptions.ShouldBoxAxes, false, false);
                DrawYSeries(bOptions.SeriesTitles);

                divx = axisXMax - axisXMin;
                offx = -(axisXMin / divx * xExtCanvas) + xAxisCanvas;
                divy = ((DoubleSeries)(seriesToUse[0])).Points;
                offy = -(0 / divy * yExtCanvas) + yAxisCanvas;

                double eachAreaHeight = yExtCanvas / divy;
                double eachBarHeightFraction;
                double eachBarHeight;
                double totalBarHeightFraction;
                if (bOptions.Stacked)
                {
                    eachBarHeightFraction = Math.Min(1.0, Math.Max(bOptions.MaxBarWidth, 0.01));
                    eachBarHeight = eachAreaHeight * eachBarHeightFraction;
                    totalBarHeightFraction = eachBarHeightFraction;
                }
                else
                {
                    eachBarHeightFraction = Math.Min(1.0 / seriesToUse.Count, Math.Max(bOptions.MaxBarWidth, 0.01));
                    eachBarHeight = eachAreaHeight * eachBarHeightFraction;
                    totalBarHeightFraction = eachBarHeightFraction * seriesToUse.Count;
                }
                double eachSideWhiteSpaceHeightFraction = (1.0 - totalBarHeightFraction) / 2.0;
                double eachSideWhiteSpaceHeight = eachSideWhiteSpaceHeightFraction * eachAreaHeight;

                //  Work through the columns - this plots each series in turn, rather than all the bars in increasing Y-order.  It's easier on pen/brush resources but requires a little more calculation.
                for (int c = 0; c <= seriesToUse.Count - 1; c++)
                {
                    DoubleSeries s = seriesToUse[c].AsDoubleSeries;
                    MarkerType mt = bOptions.MarkerTypes[c];
                    double bottomOffsetInArea;
                    if (bOptions.Stacked)
                    {
                        bottomOffsetInArea = eachBarHeight + eachSideWhiteSpaceHeight;
                    }
                    else
                    {
                        bottomOffsetInArea = (seriesToUse.Count - c) * eachBarHeight + eachSideWhiteSpaceHeight;
                    }

                    Pen barPen = s.StyledPen;
                    Brush barBrush = MarkerTypeToBrush(mt);

                    for (int barIndex = 0; barIndex <= s.Data.Length - 1; barIndex++)
                    {
                        double thisData = s.Data[barIndex];
                        //  Missing data leads to missing bars
                        if (thisData != Constant.MISSING)
                        {
                            double totalBelowThisBar = 0;
                            double totalOfAllBars = 0;
                            //  Data exists.  For stacked and 100% stacked bars, we now need to position and scale the bar.
                            if (bOptions.Stacked)
                            {
                                for (int probeIndex = 0; probeIndex <= seriesToUse.Count - 1; probeIndex++)
                                {
                                    double probeValue = seriesToUse[probeIndex].AsDoubleSeries.Data[barIndex];
                                    if (probeValue != Constant.MISSING)
                                    {
                                        if (probeIndex < c)
                                        {
                                            totalBelowThisBar += probeValue;
                                        }
                                        totalOfAllBars += probeValue;
                                    }
                                }
                                //  By now: totalOfAllBars contains the total for all bars; totalBelowThisBar contains the total of bars that have already been drawn; thisData contains our own bar length
                                if (bOptions.Stacked100Percent)
                                {
                                    if (totalOfAllBars <= 0)
                                    {
                                        thisData = Constant.MISSING;
                                    }
                                    else
                                    {
                                        //  Scale to percent
                                        thisData = thisData / totalOfAllBars * 100.0;
                                        totalBelowThisBar = totalBelowThisBar / totalOfAllBars * 100.0;
                                    }
                                }
                                //  By now, totalBelowThisBar contains the sum of all the values below this bar scaled appropriately for 100% scaling if required.
                                //  thisData also contains appropriately scaled data.
                            }

                            //  If we should, draw this bar
                            if (thisData != Constant.MISSING)
                            {
                                //  Prevent portions of bars being drawn below the X axis
                                double dataW;
                                double dataLowX;
                                //  Dim dataHighX As Double = totalBelowThisBar + thisData
                                if (totalBelowThisBar < axisXMin)
                                {
                                    dataW = thisData + totalBelowThisBar - axisXMin;
                                    dataLowX = axisXMin;
                                }
                                else
                                {
                                    dataW = thisData;
                                    dataLowX = totalBelowThisBar;
                                }

                                if (dataW > 0)
                                {
                                    double areaYOffset = (s.Data.Length - 1 - barIndex) * eachAreaHeight;
                                    double barH = eachBarHeight;
                                    double barW = dataW / divx * xExtCanvas;
                                    double barY = offy + areaYOffset + bottomOffsetInArea;
                                    double barX = ToCanvasX(dataLowX);
                                    if (barBrush != null)
                                    {
                                        FillRectangle(barBrush, barX, barY, barW, barH);
                                    }
                                    if (!(definition.ChartOptions.UseColour))
                                    {
                                        DrawRectangle(barPen, barX, barY, barW, barH);
                                    }
                                }
                            }
                        }
                    }

                    //  Legend
                    if (bOptions.ShowLegend && bOptions.ShowLegendIsRelevant)
                    {
                        if (barBrush != null)
                        {
                            FillRectangle(barBrush, xAxisCanvas, legendTop - (c * legendSpacing), legendRowHeight, legendRowHeight);
                        }
                        if (!(definition.ChartOptions.UseColour))
                        {
                            DrawRectangle(barPen, xAxisCanvas, legendTop - (c * legendSpacing), legendRowHeight, legendRowHeight);
                        }
                        DrawStringLegendL(definition.XSeries[c].Title, xAxisCanvas + 9 + legendRowHeight, legendTop - (c * legendSpacing));
                    }

                    if (barBrush != null)
                    {
                        barBrush.Dispose();
                    }
                }
            }
            else
            {
                //  Not horizontal, so vertical

                StartMetafile(outputStream, false);
                SetFontsAndThicknessesFromOptions(bOptions);

                // Draw the scale
                AssignMarkersToSeries(bOptions);

                //  Get overall minima and maxima
                double min = double.MaxValue;
                double max = double.MinValue;
                foreach (DoubleSeries s in seriesToUse)
                {
                    min = Math.Min(min, s.Min);
                    max = Math.Max(max, s.Max);
                }

                int div;
                double zmin = 0;
                double zint = 0;
                Q_AxisOrFromDefinition(ref min, ref max, out div, ref zmin, ref zint, out minorTicsPerMajorTic, true, definition.ScaleParameters.Y.ScaleType, false);
                string msk = AxisMaskOrFromDefinition(zint, zmin, div, minorTicsPerMajorTic, true, definition.ScaleParameters.Y.ScaleType, false);

                double xtra = 0;
                double w = canvas.MeasureString(min.ToString(msk), axisLabelFont).Width;
                //  Allow 20 units for axes; if we need more, offset the axis
                if (w - 20 > xtra)
                {
                    xtra = w - 20;
                }
                w = canvas.MeasureString(max.ToString(msk), axisLabelFont).Width;
                if (w - 20 > xtra)
                {
                    xtra = w - 20;
                }
                //  Offset the axis label

                DrawAxes(definition.ChartOptions.Title, new Axis(null, AxisMode.Series, xtra, definition.ScaleParameters.X.ScaleType), new Axis(axisTitle, AxisMode.Scale, 0, definition.ScaleParameters.Y.ScaleType), bOptions.ShouldBoxAxes, false, false);
                DrawXSeries(bOptions.SeriesTitles);

                divy = axisYMax - axisYMin;
                offy = -(axisYMin / divy * yExtCanvas) + yAxisCanvas;
                divx = ((DoubleSeries)(seriesToUse[0])).Points;
                offx = -(0 / divx * xExtCanvas) + xAxisCanvas;

                double eachAreaWidth = xExtCanvas / divx;
                double eachBarWidthFraction;
                double eachBarWidth;
                double totalBarWidthFraction;
                if (bOptions.Stacked)
                {
                    eachBarWidthFraction = Math.Min(1.0, Math.Max(bOptions.MaxBarWidth, 0.01));
                    eachBarWidth = eachAreaWidth * eachBarWidthFraction;
                    totalBarWidthFraction = eachBarWidthFraction;
                }
                else
                {
                    eachBarWidthFraction = Math.Min(1.0 / seriesToUse.Count, Math.Max(bOptions.MaxBarWidth, 0.01));
                    eachBarWidth = eachAreaWidth * eachBarWidthFraction;
                    totalBarWidthFraction = eachBarWidthFraction * seriesToUse.Count;
                }
                double eachSideWhiteSpaceWidthFraction = (1.0 - totalBarWidthFraction) / 2.0;
                double eachSideWhiteSpaceWidth = eachSideWhiteSpaceWidthFraction * eachAreaWidth;

                // work through the columns - this plots each series in turn, rather than all the bars in increasing X-order
                for (int c = 0; c <= seriesToUse.Count - 1; c++)
                {
                    DoubleSeries s = seriesToUse[c].AsDoubleSeries;
                    MarkerType mt = bOptions.MarkerTypes[c];
                    double leftOffsetInArea;
                    if (bOptions.Stacked)
                    {
                        leftOffsetInArea = eachSideWhiteSpaceWidth;
                    }
                    else
                    {
                        leftOffsetInArea = c * eachBarWidth + eachSideWhiteSpaceWidth;
                    }

                    Pen barPen = s.StyledPen;
                    Brush barBrush = MarkerTypeToBrush(mt);

                    for (int barIndex = 0; barIndex <= s.Data.Length - 1; barIndex++)
                    {
                        double thisData = s.Data[barIndex];
                        //  Missing data leads to missing bars
                        if (thisData != Constant.MISSING)
                        {
                            double totalBelowThisBar = 0;
                            //  Data exists.  For stacked and 100% stacked bars, we now need to position and scale the bar.
                            if (bOptions.Stacked)
                            {
                                double totalOfAllBars = 0;
                                for (int probeIndex = 0; probeIndex <= seriesToUse.Count - 1; probeIndex++)
                                {
                                    double probeValue = seriesToUse[probeIndex].AsDoubleSeries.Data[barIndex];
                                    if (probeValue != Constant.MISSING)
                                    {
                                        if (probeIndex < c)
                                        {
                                            totalBelowThisBar += probeValue;
                                        }
                                        totalOfAllBars += probeValue;
                                    }
                                }
                                //  By now: totalOfAllBars contains the total for all bars; totalBelowThisBar contains the total of bars that have already been drawn; thisData contains our own bar length
                                if (bOptions.Stacked100Percent)
                                {
                                    if (totalOfAllBars <= 0)
                                    {
                                        thisData = Constant.MISSING;
                                    }
                                    else
                                    {
                                        //  Scale to percent
                                        thisData = thisData / totalOfAllBars * 100.0;
                                        totalBelowThisBar = totalBelowThisBar / totalOfAllBars * 100.0;
                                    }
                                }
                                //  By now, totalBelowThisBar contains the sum of all the values below this bar scaled appropriately for 100% scaling if required.
                                //  thisData also contains appropriately scaled data.
                            }

                            //  If we should, draw this bar
                            if (thisData != Constant.MISSING)
                            {
                                //  Prevent portions of bars being drawn below the X axis
                                double dataH;
                                double dataLowY;
                                //  Dim dataHighX As Double = totalBelowThisBar + thisData
                                if (totalBelowThisBar < axisYMin)
                                {
                                    dataH = thisData + totalBelowThisBar - axisYMin;
                                    dataLowY = axisYMin;
                                }
                                else
                                {
                                    dataH = thisData;
                                    dataLowY = totalBelowThisBar;
                                }

                                if (dataH > 0)
                                {
                                    double areaXOffset = barIndex * eachAreaWidth;
                                    double barW = eachBarWidth;
                                    double barH = dataH / divy * yExtCanvas;
                                    double barX = offx + areaXOffset + leftOffsetInArea;
                                    double barY = ToCanvasY(dataLowY + dataH);

                                    if (barBrush != null)
                                    {
                                        FillRectangle(barBrush, barX, barY, barW, barH);
                                    }
                                    if (!(definition.ChartOptions.UseColour))
                                    {
                                        DrawRectangle(barPen, barX, barY, barW, barH);
                                    }
                                }
                            }
                        }
                    }

                    //  Legend
                    if (bOptions.ShowLegend && bOptions.ShowLegendIsRelevant)
                    {
                        if (barBrush == null)
                        {
                            DrawRectangle(barPen, xAxisCanvas, legendTop - (c * legendSpacing), legendRowHeight, legendRowHeight);
                        }
                        else
                        {
                            FillRectangle(barBrush, xAxisCanvas, legendTop - (c * legendSpacing), legendRowHeight, legendRowHeight);
                        }
                        DrawStringLegendL(definition.YSeries[c].Title, xAxisCanvas + 9 + legendRowHeight, legendTop - (c * legendSpacing));
                    }

                    if (barBrush != null)
                    {
                        barBrush.Dispose();
                    }
                }
            }
            MaybeDrawMarkerLines();
            EndMetafile();
            return new ParameterBag();
        }


        // TRANSMISSINGCOMMENT: Method MaybeDrawMarkerLines
        private void MaybeDrawMarkerLines()
        {
            if (definition != null)
            {
                if (definition.ScaleParameters != null)
                {
                    if (definition.ScaleParameters.X.HasMarkerLine)
                    {
                        double x = ToCanvasX(definition.ScaleParameters.X.MarkerLineValue);
                        using (Pen tenPen = new Pen(grBlack, 1))
                        {
                            DrawLine(tenPen, x, yAxisCanvas, x, yAxisCanvas + yExtCanvas);
                        }
                    }
                    if (definition.ScaleParameters.Y.HasMarkerLine)
                    {
                        double y = ToCanvasY(definition.ScaleParameters.Y.MarkerLineValue);
                        using (Pen tenPen = new Pen(grBlack, 1))
                        {
                            DrawLine(tenPen, xAxisCanvas, y, xAxisCanvas + xExtCanvas, y);
                        }
                    }
                }
            }
        }


        ///  <summary>
        ///  Draw a diamond of diameter size, centred on (x, y)
        ///  </summary>
        ///  <param name="p"></param>
        ///  <param name="x"></param>
        ///  <param name="y"></param>
        ///  <param name="Size"></param>
        ///  <param name="Fill"></param>
        ///  <remarks></remarks>
        public void DrawDiamond(Pen p, double x, double y, double Size, bool Fill)
        {
            double size2 = Size / 2;
            PointF[] pt = new PointF[5];
            pt[0].X = Convert.ToSingle(x - size2);
            pt[0].Y = Convert.ToSingle(metaH - y);
            pt[1].X = Convert.ToSingle(x);
            pt[1].Y = Convert.ToSingle(metaH - (y - size2));
            pt[2].X = Convert.ToSingle(x + size2);
            pt[2].Y = Convert.ToSingle(metaH - y);
            pt[3].X = Convert.ToSingle(x);
            pt[3].Y = Convert.ToSingle(metaH - (y + size2));
            pt[4].X = Convert.ToSingle(x - size2);
            pt[4].Y = Convert.ToSingle(metaH - y);
            if (Fill)
            {
                using (Brush b = new SolidBrush(p.Color))
                {
                    canvas.FillPolygon(b, pt);
                }
            }
            // Draw the diamond
            canvas.DrawPolygon(p, pt);
        }


        ///  <summary>
        ///  Draw a square of side size, centred on (x, y)
        ///  </summary>
        ///  <param name="p"></param>
        ///  <param name="x"></param>
        ///  <param name="y"></param>
        ///  <param name="size"></param>
        ///  <param name="Fill"></param>
        ///  <remarks></remarks>
        public void DrawSquare(Pen p, double x, double y, double size, bool Fill)
        {
            double size2 = size / 2;
            PointF[] pt = new PointF[5];
            pt[0].X = Convert.ToSingle(x - size2);
            pt[0].Y = Convert.ToSingle(metaH - (y - size2));
            pt[1].X = Convert.ToSingle(x - size2);
            pt[1].Y = Convert.ToSingle(metaH - (y + size2));
            pt[2].X = Convert.ToSingle(x + size2);
            pt[2].Y = Convert.ToSingle(metaH - (y + size2));
            pt[3].X = Convert.ToSingle(x + size2);
            pt[3].Y = Convert.ToSingle(metaH - (y - size2));
            pt[4].X = Convert.ToSingle(x - size2);
            pt[4].Y = Convert.ToSingle(metaH - (y - size2));
            if (Fill)
            {
                canvas.FillPolygon(blackBrush, pt);
            }
            canvas.DrawPolygon(p, pt);
        }


        ///  <summary>
        ///  Sort the data for each series (actually presently only Y series, as that's all this is ever needed for) into ascending order.
        ///  Note and return the global minimum and maximum values.
        ///  </summary>
        /// <param name="SeriesToUse"></param>
        /// <param name="Min">Filled in with the global minimum value</param>
        ///  <param name="Max">Filled in with the global maximum value</param>
        ///  <remarks>STYLE: Wouldn't this be better as a function returning some kind of data structure?</remarks>
        public void GetMinMaxSort(List<Series> SeriesToUse, out double Min, out double Max)
        {
            Min = double.MaxValue;
            Max = double.MinValue;
            foreach (DoubleSeries s in SeriesToUse)
            {
                Array.Sort(s.Data);
                if (s.Data[0] < Min)
                {
                    Min = s.Data[0];
                }
                if (s.Data[s.Data.Length - 1] > Max)
                {
                    Max = s.Data[s.Data.Length - 1];
                }
            }
        }


        ///  <summary>
        ///  Detect and return minimum and maximum values in the array.
        ///  </summary>
        /// <param name="data"></param>
        /// <param name="Min">Filled in with the global minimum value</param>
        ///  <param name="Max">Filled in with the global maximum value</param>
        ///  <remarks>STYLE: Wouldn't this be better as a function returning some kind of data structure?</remarks>
        private void GetMinMaxArray(double[] data, out double Min, out double Max)
        {
            Min = double.MaxValue;
            Max = double.MinValue;
            for (int i = data.GetLowerBound(0); i <= data.GetUpperBound(0); i++)
            {
                if (data[i] != Constant.MISSING)
                {
                    if (data[i] < Min)
                    {
                        Min = data[i];
                    }
                    if (data[i] > Max)
                    {
                        Max = data[i];
                    }
                }
            }
        }


        ///  <summary>
        ///  Get the nth centile (divided by 100,  so 0.25 for lower quartile etc) from the given series containing a sorted 0-based array of data
        ///  </summary>
        ///  <returns></returns>
        ///  <remarks></remarks>
        private double Quantile(DoubleSeries s, double n)
        {
            int count = s.Data.Length;
            double imdn = n * count;
            if (imdn < 0)
            {
                imdn = 0.0;
            }
            if (imdn > s.Data.Length - 1)
            {
                imdn = s.Data.Length - 1;
            }
            if (imdn -  /* TRANSINFO: .NET Equivalent of Microsoft.VisualBasic NameSpace */ Math.Floor(imdn) == 0.0)
            {
                return s.Data[Convert.ToInt32(imdn)];
            }
            return s.Data[((int)(Math.Floor(imdn)))] + (s.Data[((int)(Math.Floor(imdn))) + 1] - s.Data[((int)(Math.Floor(imdn)))]) * (imdn - Math.Floor(imdn));
        }

        ///  <summary>
        ///  
        ///  </summary>
        ///  <param name="s">The series to use for calculation</param>
        ///  <param name="method">The calculation method</param>
        ///  <param name="P">For Mean, CI, Range: the CI to calculate</param>
        ///  <param name="Centre">Returns the median value</param>
        ///  <param name="BoxL">Returns the lower quertile</param>
        ///  <param name="BoxR">Returns the upper quartile</param>
        ///  <param name="InnerFenceL">The lower inner fence value if used, 9th centile for seven number, or 10th centile for Bowley</param>
        ///  <param name="InnerFenceR">The upper inner fence value if used, 91st centile for seven number, or 90th centile for Bowley</param>
        ///  <param name="useInnerFence">True to calculate inner fences</param>
        ///  <param name="OuterFenceL">The lower outer fence value if used, 2nd centile for seven number, min otherwise</param>
        ///  <param name="OuterFenceR">The upper outer fence value if used, 98th centile for seven number, max otherwise</param>
        ///  <param name="useOuterFence">True to calculate outer fences</param>
        ///  <param name="OtherCentre">Another centre that might be appropriate to plot.  Mean if centre is median, and vice versa.</param>
        /// <param name="CentreIsMedian"></param>
        private void PlotBoxWhiskerCalc(DoubleSeries s, BoxWhiskerOptions.BoxWhiskerMethod method, double P, ref double Centre, ref double BoxL, ref double BoxR, ref double InnerFenceL, ref double InnerFenceR, bool useInnerFence, ref double OuterFenceL, ref double OuterFenceR, bool useOuterFence, ref double OtherCentre, ref bool CentreIsMedian)
        {

            int count = s.Data.Length;
            switch (method)
            {
                case BoxWhiskerOptions.BoxWhiskerMethod.MedianQuartilesRange:
                case BoxWhiskerOptions.BoxWhiskerMethod.SevenNumberSummary:
                case BoxWhiskerOptions.BoxWhiskerMethod.BowleySummary:
                    {

                        //  Median
                        Centre = Quantile(s, 0.5);
                        //  Lower quartile
                        BoxL = Quantile(s, 0.25);
                        //  Upper quartile
                        BoxR = Quantile(s, 0.75);

                        //  Inner fence
                        switch (method)
                        {
                            case BoxWhiskerOptions.BoxWhiskerMethod.MedianQuartilesRange:
                                double interQuartileRange = Math.Abs(BoxR - BoxL);
                                if (useInnerFence)
                                {
                                    InnerFenceL = BoxL - 1.5 * interQuartileRange;
                                    InnerFenceR = BoxR + 1.5 * interQuartileRange;
                                }
                                else
                                {
                                    //  Get out of the way!
                                    InnerFenceL = BoxL;
                                    InnerFenceR = BoxR;
                                }
                                break;
                            case BoxWhiskerOptions.BoxWhiskerMethod.SevenNumberSummary:
                                InnerFenceL = Quantile(s, 0.09);
                                InnerFenceR = Quantile(s, 0.91);
                                break;
                            case BoxWhiskerOptions.BoxWhiskerMethod.BowleySummary:
                                InnerFenceL = Quantile(s, 0.1);
                                InnerFenceR = Quantile(s, 0.9);
                                break;
                        }

                        //  Fences never extend beyond the data
                        if (InnerFenceL < s.Data[0])
                        {
                            InnerFenceL = s.Data[0];
                        }
                        if (InnerFenceR > s.Data[s.Data.Length - 1])
                        {
                            InnerFenceR = s.Data[s.Data.Length - 1];
                        }

                        //  Outer fence
                        switch (method)
                        {
                            case BoxWhiskerOptions.BoxWhiskerMethod.MedianQuartilesRange:
                                if (useOuterFence)
                                {
                                    double interQuartileRange = Math.Abs(BoxR - BoxL);
                                    OuterFenceL = BoxL - 3.0 * interQuartileRange;
                                    OuterFenceR = BoxR + 3.0 * interQuartileRange;
                                }
                                else
                                {
                                    //  Min/max
                                    OuterFenceL = s.Data[0];
                                    OuterFenceR = s.Data[s.Points - 1];
                                }
                                break;
                            case BoxWhiskerOptions.BoxWhiskerMethod.SevenNumberSummary:
                                OuterFenceL = Quantile(s, 0.02);
                                OuterFenceR = Quantile(s, 0.98);
                                break;
                            case BoxWhiskerOptions.BoxWhiskerMethod.BowleySummary:
                                //  Min/max
                                OuterFenceL = s.Data[0];
                                OuterFenceR = s.Data[s.Points - 1];
                                break;
                        }

                        //  Fences never extend beyond the data
                        if (OuterFenceL < s.Data[0])
                        {
                            OuterFenceL = s.Data[0];
                        }
                        if (OuterFenceR > s.Data[s.Data.Length - 1])
                        {
                            OuterFenceR = s.Data[s.Data.Length - 1];
                        }

                        CentreIsMedian = true;
                        //  Other centre is the mean
                        double sum = 0;
                        for (int N = 0; N <= count - 1; N++)
                        {
                            sum += s.Data[N];
                        }
                        OtherCentre = sum / Convert.ToDouble(count);

                    } break;
                case BoxWhiskerOptions.BoxWhiskerMethod.MeanStandardDeviationRange:
                case BoxWhiskerOptions.BoxWhiskerMethod.MeanConfidenceIntervalRange:
                    {

                        double sum = 0.0;
                        // double sumsq = 0.0; 
                        double sumsqdev = 0.0;

                        for (int N = 0; N <= count - 1; N++)
                        {
                            sum += s.Data[N];
                            // sumsq += s.Data[ N ] * s.Data[ N ]; 
                        }

                        double mean = sum / Convert.ToDouble(count);
                        // double ss = sumsq - ( ( sum * sum ) / Convert.ToDouble( count ) ); 

                        for (int N = 0; N <= count - 1; N++)
                        {
                            if (Math.Abs(sumsqdev) > 1.0E+300)
                            {
                                sumsqdev = Constant.MISSING;
                                break; /* TRANSWARNING: check that break is in correct scope */
                            }
                            double dev = s.Data[N] - mean;
                            sumsqdev += dev * dev;
                        }

                        Centre = mean;
                        CentreIsMedian = false;
                        OtherCentre = Quantile(s, 0.5); //  Median
                        if (sumsqdev == Constant.MISSING)
                        {
                            //  Everything collapses
                            BoxL = Centre;
                            BoxR = Centre;
                            if (useInnerFence)
                            {
                                InnerFenceL = Centre;
                                InnerFenceR = Centre;
                            }
                            if (useOuterFence)
                            {
                                OuterFenceL = Centre;
                                OuterFenceR = Centre;
                            }
                        }
                        else
                        {
                            double variance = sumsqdev / Convert.ToDouble(count - 1);
                            double standardDeviation = Math.Sqrt(variance);

                            if (method == BoxWhiskerOptions.BoxWhiskerMethod.MeanStandardDeviationRange)
                            {
                                BoxL = mean - standardDeviation;
                                BoxR = mean + standardDeviation;
                            }
                            else
                            {
                                double cit = PDF.tfromp(P, Convert.ToDouble(count - 1));
                                double bit = cit * standardDeviation / Math.Sqrt(count);
                                BoxL = mean - bit;
                                BoxR = mean + bit;
                            }

                            if (useInnerFence)
                            {
                                //  95% CI
                                int transTemp75;
                                double innerSdFactor = PDF.gauinv(0.975, out transTemp75);
                                InnerFenceL = mean - innerSdFactor * standardDeviation;
                                InnerFenceR = mean + innerSdFactor * standardDeviation;
                            }
                            if (useOuterFence)
                            {
                                //  99% CI
                                int transTemp74;
                                double outerSdFactor = PDF.gauinv(0.995, out transTemp74);
                                OuterFenceL = mean - outerSdFactor * standardDeviation;
                                OuterFenceR = mean + outerSdFactor * standardDeviation;
                            }
                        }

                    } break;
                default:
                    throw new ArgumentOutOfRangeException("method", method.ToString());
            }


        }


        // TRANSMISSINGCOMMENT: Method GetHistogramScaleParameters
        private ScaleParameters GetHistogramScaleParameters()
        {
            HistogramOptions histOptions = ((HistogramOptions)(definition.ChartOptions));
            bool ShowRelativeFrequencies = histOptions.ShowRelativeFrequencies;
            List<Series> SeriesToUse = definition.YSeries;

            //  Ensure the data is sorted
            GetMinMaxSort(SeriesToUse, out dataMinX, out dataMaxX);

            //  We're looking over multiple histograms and getting a merged view
            DataMinY = 0.0;
            DataMaxY = 0.0;

            for (int iter = 0; iter <= SeriesToUse.Count - 1; iter++)
            {
                DoubleSeries s = SeriesToUse[iter].AsDoubleSeries;
                HistogramSeriesOptions so = histOptions.HistoSeriesOptions[iter];

                int mp = so.Bins;
                double zint = so.MidPointInterval;
                double zmin = so.MinimumBinMidPoint;
                int[] size = new int[mp + 1 + 1 /* for VB to C# conversion */ ];
                double[] midpt = new double[mp + 1 + 1 /* for VB to C# conversion */ ];

                //  Set up our axis bounds for the X axis - we do this ourselves and don't allow the neatening code to amend it.
                axisXMin = zmin;
                axisXMax = zmin + (zint * mp - 1);

                //  Find the number of values in each bin, and hence the size of the histogram's y axis.
                //  This works because the bins are always of equal width - if they weren't, we'd have to scale by the width
                double seriesMaxY = 0;
                int c2 = 0;
                for (int C = 1; C <= mp; C++)
                {
                    double high = zmin + (zint * Convert.ToDouble(C - 1)) + zint / 2.0;
                    //  Count the number of samples in this bin
                    int c1;
                    for (c1 = c2; c1 <= s.Points - 1; c1++)
                    {
                        if (s.Data[c1] > high)
                        {
                            break; /* TRANSWARNING: check that break is in correct scope */
                        }
                    }
                    size[C] = c1 - c2;
                    midpt[C] = zmin + (zint * Convert.ToDouble(C - 1));
                    if (size[C] > seriesMaxY)
                    {
                        seriesMaxY = size[C];
                    }
                    c2 = c1;
                }
                if (ShowRelativeFrequencies)
                {
                    seriesMaxY = seriesMaxY / s.Points;
                }
                if (seriesMaxY > DataMaxY)
                {
                    DataMaxY = seriesMaxY;
                }
            }
            ScaleParameters sp = new ScaleParameters
                                     {
                                         X = { AllowedScaleTypes = new[] { ScaleType.Linear } },
                                         Y = { AllowedScaleTypes = new[] { ScaleType.Linear } }
                                     };
            sp.X.ShouldCheck = false;
            sp.Y.ShouldCheck = true;
            sp.X.Max = DataMaxX;
            sp.X.Min = DataMinX;
            sp.Y.Max = DataMaxY;
            sp.Y.Min = DataMinY;
            return sp;
        }


        // TRANSMISSINGCOMMENT: Method PlotHistogram
        private ParameterBag PlotHistogram(Stream OutputStream)
        {
            HistogramOptions histOptions = ((HistogramOptions)(definition.ChartOptions));
            bool showRelativeFrequencies = histOptions.ShowRelativeFrequencies;
            List<Series> SeriesToUse = definition.YSeries;
            MarkerType[] originalMarkerTypes = null;
            //  A space to save drawn ASCII plots until required
            List<string> savedLines = null;

            //  Ensure the data is sorted
            GetMinMaxSort(SeriesToUse, out dataMinX, out dataMaxX);

            // get settings for plot
            bool overlayNormalCurve = histOptions.OverlayNormalCurve;

            try
            {
                if (!(IsAscii))
                {
                    //  If there's more than one series, they're to be plotted separately.  Each plot is the same height as the original.
                    metaH = metaH * SeriesToUse.Count;

                    StartMetafile(OutputStream, true);

                    originalMarkerTypes = _markerTypes;
                    _markerTypes = new MarkerType[originalMarkerTypes.Length];
                    for (int i = 0; i <= originalMarkerTypes.Length - 1; i++)
                    {
                        _markerTypes[i] = originalMarkerTypes[i].Clone();
                        _markerTypes[i].Width = histOptions.LineWidth;
                    }
                    AssignMarkersToSeries(SeriesToUse);

                    if (!(string.IsNullOrEmpty(histOptions.AxisFontDescriptor)))
                    {
                        axisLabelFont = FontFromSaveString(histOptions.AxisFontDescriptor);
                    }
                    if (!(string.IsNullOrEmpty(histOptions.AxisTitleFontDescriptor)))
                    {
                        axisTitleFont = FontFromSaveString(histOptions.AxisTitleFontDescriptor);
                    }
                    if (!(string.IsNullOrEmpty(histOptions.TitleFontDescriptor)))
                    {
                        titleFont = FontFromSaveString(histOptions.TitleFontDescriptor);
                    }
                }
                else
                {
                    savedLines = new List<string>();
                }

                for (int iter = 0; iter < SeriesToUse.Count; iter++)
                {
                    DoubleSeries s = SeriesToUse[iter].AsDoubleSeries;
                    HistogramSeriesOptions so = histOptions.HistoSeriesOptions[iter];
                    string title = so.ChartTitle;

                    int mp = so.Bins;
                    double zint = so.MidPointInterval;
                    double zmin = so.MinimumBinMidPoint;
                    int[] size = new int[mp + 1 + 1 /* for VB to C# conversion */ ];
                    double[] midpt = new double[mp + 1 + 1 /* for VB to C# conversion */ ];
                    string msk = GetAxisMask(zint, zmin, mp, 1);

                    //  Set up our axis bounds for the X axis - we do this ourselves and don't allow the neatening code to amend it.
                    axisXMin = zmin;
                    axisXMax = zmin + (zint * mp - 1);

                    //  Find the number of values in each bin, and hence the size of the histogram's y axis.
                    //  This works because the bins are always of equal width - if they weren't, we'd have to scale by the width
                    DataMinY = 0.0;
                    DataMaxY = 0.0;
                    int C2 = 0;
                    double seriesMaxY = 0;
                    for (int C = 1; C <= mp; C++)
                    {
                        double high = zmin + (zint * Convert.ToDouble(C - 1)) + zint / 2.0;
                        int c1;
                        for (c1 = C2; c1 <= s.Points - 1; c1++)
                        {
                            if (s.Data[c1] > high)
                            {
                                break; /* TRANSWARNING: check that break is in correct scope */
                            }
                        }
                        size[C] = c1 - C2;
                        midpt[C] = zmin + (zint * Convert.ToDouble(C - 1));
                        if (size[C] > seriesMaxY)
                        {
                            seriesMaxY = size[C];
                        }
                        C2 = c1;
                        if (showRelativeFrequencies)
                        {
                            seriesMaxY = seriesMaxY / s.Points;
                        }
                        if (seriesMaxY > DataMaxY)
                        {
                            DataMaxY = seriesMaxY;
                        }
                    }

                    if (!(IsAscii))
                    {
                        // Plot a Metafile version
                        double heightPerChart = metaH / SeriesToUse.Count; //  Should end up as the old MetaH
                        double thisChartTop = metaH - (iter * heightPerChart);
                        double thisChartBottom = thisChartTop - heightPerChart;

                        //  No longer the default Y axis!
                        DefaultAxes();
                        yAxisCanvas = thisChartBottom + Math.Min(Math.Floor(metaH / 8), DEFAULT_Y_GAP);
                        yExtCanvas = heightPerChart - Math.Min(heightPerChart / 4, 2 * DEFAULT_Y_GAP) * scaleYAxis;

                        //  Ensure the normal curve doesn't fall off the top of the Y axis
                        if (overlayNormalCurve)
                        {
                            double mxy = PlotCurveMax(zmin, zint, mp - 1, s);
                            if (mxy > dataMaxY)
                            {
                                dataMaxY = mxy;
                            }
                        }

                        divx = xExtCanvas / Math.Max(mp, 1);
                        offx = 0; //  was CInt(divx * 0.1) but bars are now full-width
                        divy = axisYMax - axisYMin;
                        offy = -(axisYMin / divy * yExtCanvas) + yAxisCanvas;
                        double barx = divx; //  was CInt(divx * 0.9) but bars are now full-width
                        double ctrx = divx / 2.0;
                        SizeF legendSize = canvas.MeasureString(midpt[mp].ToString(), legendFont);
                        bool labelsAreLong = legendSize.Width > (barx * 0.75);

                        // Draw the scale
                        double ytra = legendSize.Height;
                        if (labelsAreLong)
                        {
                            ytra += legendSize.Height * 1.5;
                        }
                        Axis xAxis = new Axis(histOptions.HistoSeriesOptions[iter].XAxisTitle, AxisMode.LineOnly, 0, definition.ScaleParameters.X.ScaleType);
                        if (labelsAreLong)
                        {
                            xAxis.AxisTitleOffset = legendSize.Height * 1.5;
                        }
                        DrawAxes(title, xAxis, new Axis(histOptions.HistoSeriesOptions[iter].YAxisTitle, AxisMode.Scale, ytra, definition.ScaleParameters.Y.ScaleType), false, false, false);

                        // Plot each bar
                        bool labelIsLow = false;
                        for (int C = 1; C <= mp; C++)
                        {

                            // plot a bar at an absolute position (maxX / 20)
                            double x1 = xAxisCanvas + divx * (C - 1) + offx;
                            double x2 = x1 + barx;
                            double value = showRelativeFrequencies ? size[C] / (double)s.Points : size[C];
                            double y1 = yAxisCanvas + (value / axisYMax * yExtCanvas);
                            double y2 = yAxisCanvas;
                            DrawRectangle(s.UnstyledPen, x1, y1, x2 - x1, y1 - y2);

                            // Now the mid-point label in the centre of the bar
                            x1 += ctrx;
                            double yoff = axisLabelFont.SizeInPoints;
                            if (labelsAreLong)
                            {
                                if (labelIsLow)
                                {
                                    yoff = axisLabelFont.SizeInPoints * 2.5;
                                }
                                labelIsLow = !(labelIsLow);
                            }
                            AxisDrawStringC(midpt[C].ToString(msk), x1, yAxisCanvas - yoff);
                        }


                        //  ZInt was calculated at Mp*2
                        if (overlayNormalCurve)
                        {
                            double proportionScaler;
                            if (showRelativeFrequencies)
                            {
                                proportionScaler = 1.0 / s.Points;
                            }
                            else
                            {
                                proportionScaler = 1.0;
                            }
                            PlotCurve(zmin, zint, mp - 1, s, proportionScaler);
                        }
                    }
                    else
                    {
                        // Plot one ASCII histogram per series.  The cheat is to plot each one, save it, and concatenate at the end!
                        ASCII_InitPlot(mp + 4);
                        // Draw the scale

                        // the ASCII version is plotted sideways
                        // so save the current X value
                        // get the values needed to plot the X axis
                        axisXMin = 0;
                        DataMinX = 0;
                        DataMaxX = DataMaxY;
                        axisXMax = DataMaxY;

                        DrawAxes(title, new Axis(null, AxisMode.Scale, 0, definition.ScaleParameters.X.ScaleType), new Axis(null, AxisMode.None, 0, definition.ScaleParameters.Y.ScaleType), false, true, false);
                        int C;
                        for (C = 1; C <= mp; C++)
                        {
                            int L = Convert.ToInt32(size[C] / axisXMax * 60);
                            WriteAsciiYX(C + ASCII_Ytxt - 1, ASCII_XTxt + 5, new string('=', L));
                            if (size[C] > 0 & L == 0)
                            {
                                WriteAsciiYX(C + ASCII_Ytxt - 1, ASCII_XTxt + 5, ":");
                            }

                            string buf = midpt[C].ToString(msk) + "|";
                            L = buf.Length;
                            WriteAsciiYX(C + ASCII_Ytxt - 1, ASCII_XTxt - L + 5, buf);

                            buf = size[C].ToString();
                            WriteAsciiYX(C + ASCII_Ytxt - 1, 1, buf);
                        }

                        WriteAsciiYX(0, ASCII_XTxt, histOptions.HistoSeriesOptions[iter].XAxisTitle);

                        //  At this point, C is one past the number of series in the chart
                        WriteAsciiYX(C + ASCII_Ytxt - 1, 16, "Mid-points");
                        WriteAsciiYX(C + ASCII_Ytxt - 1, 1, "Counts");
                        shTx[2] = "     " + shTx[2].Substring(0, 85);
                        shTx[1] = "     " + shTx[1].Substring(0, 85);
                        shTx[0] = "     " + shTx[0].Substring(0, 85);

                        //  Save this plot
                        for (int i = shTx.Length - 1; i >= 0; i--)
                        {
                            savedLines.Insert(0, shTx[i]);
                        }
                        //  Separator
                        savedLines.Insert(0, "");
                    }
                }

                if (!(IsAscii))
                {
                    MaybeDrawMarkerLines();
                    EndMetafile();

                    metaH = DEFAULT_METAH;
                }
                else
                {
                    //  Fill in the output in its expected place from our saved place
                    shTx = new string[savedLines.Count];
                    for (int i = 0; i <= savedLines.Count - 1; i++)
                    {
                        shTx[i] = savedLines[i];
                    }
                }

                return new ParameterBag();
            }
            finally
            {
                if (originalMarkerTypes != null)
                {
                    //  TODO: Resource leak on pens?
                    _markerTypes = originalMarkerTypes;
                }
            }
        }


        ///  <summary>
        ///  
        ///  </summary>
        /// <param name="x">The X co-ordinates of the points to plot.  Zero-based or 1-based.</param>
        ///  <param name="y">The Y co-ordinates of the points to plot.  Zero-based or 1-based, same length as x.</param>
        ///  <param name="xtxt">The X-axis title</param>
        ///  <param name="ytxt">The y-axis title</param>
        ///  <param name="title">The chart title</param>
        ///  <param name="ZPlot">If true, draw a line at the smallest Y value</param>
        ///  <param name="MinMaxY">If -99, use preset min/max X and Y values.  If 0, calculate X and Y min/max values.  Otherwise use preset Y and calculate X min/max values.</param>
        /// <param name="MarkerSize"></param>
        /// <param name="Shape"></param>
        /// <param name="IsFilled"></param>
        /// <param name="p"></param>
        /// <param name="UseCalculatedScalesEvenWithDefinition"></param>
        /// <remarks></remarks>
        private void PlotXY(double[] x, double[] y, string xtxt, string ytxt, string title, bool ZPlot, int MinMaxY, double MarkerSize, MarkerShape Shape, bool IsFilled, Pen p, bool UseCalculatedScalesEvenWithDefinition)
        {
            //  If required, get the Min and Max for the data
            //  This is safe because we're using this function to plot our data.
            switch (MinMaxY)
            {
                case -99:
                    //  do nothing
                    break;
                case -98:
                    //  Take data from scale parameters
                    axisYMax = definition.ScaleParameters.Y.Max;
                    axisYMin = definition.ScaleParameters.Y.Min;
                    axisXMax = definition.ScaleParameters.X.Max;
                    axisXMin = definition.ScaleParameters.X.Min;
                    break;
                case -97:
                    // X and Y must have the same scale
                    GetMinMaxArray(x, out axisXMin, out axisXMax);
                    GetMinMaxArray(y, out axisYMin, out axisYMax);
                    axisXMin = Math.Min(axisYMin, axisXMin);
                    axisYMin = Math.Min(axisYMin, axisXMin);
                    axisXMax = Math.Max(axisYMax, axisXMax);
                    axisYMax = Math.Max(axisYMax, axisXMax);
                    break;
                default:
                    GetMinMaxArray(x, out axisXMin, out axisXMax);
                    if (MinMaxY == 0)
                    {
                        GetMinMaxArray(y, out axisYMin, out axisYMax);
                    }
                    break;
            }

            DataMinY = axisYMin;
            DataMaxY = axisYMax;
            DataMinX = axisXMin;
            DataMaxX = axisXMax;

            ScaleType scaleTypeX = ScaleType.Linear;
            ScaleType scaleTypeY = ScaleType.Linear;
            if (HasScaleParameters)
            {
                scaleTypeX = definition.ScaleParameters.X.ScaleType;
                scaleTypeY = definition.ScaleParameters.Y.ScaleType;
            }
            DrawAxes(title, new Axis(xtxt, AxisMode.Scale, 0, scaleTypeX), new Axis(ytxt, AxisMode.Scale, 0, scaleTypeY), false, true, UseCalculatedScalesEvenWithDefinition);

            // get the offsets for the Graph
            SetStandardScaling();

            if (ZPlot)
            {
                DrawQCanvas(offy);
            }

            // Plot the points
            int rows = x.Length;
            int xOffset = x.GetLowerBound(0);
            int yOffset = y.GetLowerBound(0);
            PointF[] xys = new PointF[rows - 1 + 1 /* for VB to C# conversion */ ];
            for (int r = 0; r <= rows - 1; r++)
            {
                if (x[r + xOffset] != Constant.MISSING && y[r + yOffset] != Constant.MISSING)
                {
                    xys[r].X = Convert.ToSingle(ToCanvasX(x[r + xOffset]));
                    xys[r].Y = Convert.ToSingle(ToCanvasY(y[r + yOffset]));
                }
                else
                {
                    //  Missing.  Any value less than zero is ignored by DrawMarkerSeries.
                    xys[r].X = -1;
                    xys[r].Y = -1;
                }
            }
            DrawMarkerSeries(xys, MarkerSize, Shape, IsFilled, p, p, false, true);
        }


        // TRANSMISSINGCOMMENT: Method PlotXYAndReturnImage
        public string PlotXYAndReturnRtf(ITemplateHost host, double[] x, double[] y, string xtxt, string ytxt, string title, bool zPlot, int minMaxY, bool useCalculatedScalesEvenWithDefinition)
        {
            using (MemoryStream metaStream = new MemoryStream())
            {
                StartMetafile(metaStream, true);
                PlotXY(x, y, xtxt, ytxt, title, zPlot, minMaxY, 6, MarkerShape.Circle, false, Pens.Black, useCalculatedScalesEvenWithDefinition);
                EndMetafile();
                return host.ImageStreamToRtf(metaStream);
            }
        }


        // TRANSMISSINGCOMMENT: Method PlotXYZAndReturnImage
        public string PlotXYZAndReturnRtf(ITemplateHost host, double[] x, double[] y, double[] z, string xtxt, string ytxt, string title, bool zPlot, int minMaxY)
        {
            using (MemoryStream metaStream = new MemoryStream())
            {
                StartMetafile(metaStream, true);
                PlotXYZ(x, y, z, 1, x.Length - 1, xtxt, ytxt, title, zPlot, minMaxY, MarkerShape.Circle, false, Pens.Black, null);
                EndMetafile();
                return host.ImageStreamToRtf(metaStream);
            }
        }


        ///  <summary>
        ///  Draw a horizontal line at the specified offset from the Y origin.
        ///  </summary>
        ///  <param name="y">The offset, in device units</param>
        ///  <remarks></remarks>
        private void DrawQCanvas(double y)
        {
            using (Pen greenPen = new Pen(grGreen, 2))
            {
                DrawLine(greenPen, xAxisCanvas, y, xAxisCanvas + xExtCanvas, y);
            }
        }


        ///  <summary>
        ///  A colour to be used for drawing lines that are nominally black.
        ///  </summary>
        ///  <value></value>
        ///  <returns></returns>
        ///  <remarks></remarks>
        public Color grBlack
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
        private Color grGreen
        {
            get { return HasChartOptions && definition.ChartOptions.UseColour ? Color.Green : Color.Black; }
        }

        ///  <summary>
        ///  A colour to be used for drawing lines that are nominally magenta.
        ///  </summary>
        ///  <value></value>
        ///  <returns></returns>
        ///  <remarks></remarks>
        public Color grMagenta
        {
            get { return HasChartOptions && definition.ChartOptions.UseColour ? Color.Magenta : Color.Black; }
        }

        ///  <summary>
        ///  A colour to be used for drawing lines that are nominally red.
        ///  </summary>
        ///  <value></value>
        ///  <returns></returns>
        ///  <remarks></remarks>
        private Color grRed
        {
            get { return HasChartOptions && definition.ChartOptions.UseColour ? Color.Red : Color.Black; }
        }

        ///  <summary>
        ///  A colour to be used for drawing axis lines
        ///  </summary>
        ///  <value></value>
        ///  <returns></returns>
        ///  <remarks></remarks>
        private Color grAxis
        {
            get
            {
                return HasChartOptions && definition.ChartOptions.UseColour
                           ? Color.FromArgb(134, 134, 134)
                           : Color.Black;
            }
        }


        // TRANSMISSINGCOMMENT: Method PlotCurve
        private void PlotCurve(double qzmin, double qzint, int qcount, DoubleSeries s, double proportionScaler)
        {
            double zmin = qzmin;
            double zint = qzint;
            int count = qcount;

            // Setup the plotting variables
            double xbar = s.Sum / s.Points;
            double sdv = s.StdDev;
            double sumx = zmin;
            double bins = zint * s.Points * (1.0 / (sdv * Math.Sqrt(2.0 * Math.PI)));

            //  Multiply the count to give more steps
            int div = Convert.ToInt32(xExtCanvas / count / 5);
            count *= div;
            zint = zint / Convert.ToDouble(div);

            //  Move the cursor to the start
            double y = bins * Math.Exp(-0.5 * Math.Pow(((sumx - xbar) / sdv), 2.0)) * proportionScaler;
            double yold = ToCanvasY(y);
            double xold = xAxisCanvas;

            for (int C = 1; C <= count; C++)
            {
                sumx += zint;
                y = bins * Math.Exp(-0.5 * Math.Pow(((sumx - xbar) / sdv), 2.0)) * proportionScaler;
                double y1 = ToCanvasY(y);
                double x1 = xAxisCanvas + (C / (double)count * xExtCanvas);
                DrawLine(s.UnstyledPen, xold, yold, x1, y1);
                xold = x1;
                yold = y1;
            }
        }


        ///  <summary>
        ///  Returns the maximum value of a normal curve from the specified series and bin values.
        ///  </summary>
        ///  <param name="qzmin">The smallest midpoint</param>
        ///  <param name="qzint">The midpoint interval</param>
        ///  <param name="qcount">The number of bins</param>
        ///  <param name="s">The series whose data is to be used for the calculation</param>
        ///  <remarks></remarks>
        private double PlotCurveMax(double qzmin, double qzint, int qcount, DoubleSeries s)
        {
            double zmin = qzmin;
            double zint = qzint;
            int count = qcount;

            // calc max for normal plot background
            //  TODO: Init_Axes() - do we need to?

            // Setup the plotting variables
            double xbar = s.Sum / s.Points;
            double sdv = s.StdDev;
            double sumx = zmin;
            double bins = zint * s.Points * (1.0 / (sdv * Math.Sqrt(2.0 * Math.PI)));

            //  Multiply the count to give more steps
            int div = Convert.ToInt32(xExtCanvas / count / 5);
            count *= div;
            zint = zint / Convert.ToDouble(div);

            //  Move the cursor to the start
            double max = bins * Math.Exp(-0.5 * Math.Pow(((sumx - xbar) / sdv), 2.0));

            for (int c = 1; c <= count; c++)
            {
                sumx += zint;
                double y = bins * Math.Exp(-0.5 * Math.Pow(((sumx - xbar) / sdv), 2.0));
                if (y > max)
                {
                    max = y;
                }
            }
            return max;
        }


        // TRANSMISSINGCOMMENT: Method GetAxisMask
        private static string GetAxisMask(double stepp, double znmin, int nstep, int sp)
        {
            int dp;

            if (stepp > 0.000001)
            {
                string Q = (stepp * sp).ToString();
                string Q2 = Math.Abs(znmin).ToString();
                int xp = Q.IndexOf(SDGlobalStub.DECP_CHAR, StringComparison.Ordinal) + 1;
                dp = xp == 0 ? 0 : Q.Length - xp;
                int xp2 = Q2.IndexOf(SDGlobalStub.DECP_CHAR, StringComparison.Ordinal) + 1;
                int dp2 = xp2 == 0 ? 0 : Q2.Length - xp2;
                if (dp2 > dp & xp2 != 0)
                {
                    dp = dp2;
                }
                else
                {
                    if (xp == 0)
                    {
                        dp = 0;
                    }
                }
            }
            else
            {
                dp = -1;
            }
            int maxc = 1;
            double x = Math.Abs(znmin) + Math.Abs(nstep * stepp);
            if (x > 0.0)
            {
                maxc = maxc + Convert.ToInt32(Math.Abs(Math.Floor(Math.Log(x) / Math.Log(10))));
            }
            if (znmin < 0)
            {
                maxc = maxc + 1;
            }
            if (maxc > 6)
            {
                dp = -1;
            }
            string msk = "";
            if (dp > 0)
            {
                msk = new string('#', maxc - 1) + "0." + new string('0', dp);
            }
            else if (dp == 0)
            {
                msk = new string('#', maxc - 1) + "0";
            }
            if (msk.Length > 9 || dp < 0)
            {
                msk = "Scientific";
            }
            return msk;
        }


        // TRANSMISSINGCOMMENT: Method GetSpreadScaleParameters
        private ScaleParameters GetSpreadScaleParameters()
        {
            List<Series> seriesToUse = definition.YSeries.Count > 0 ? definition.YSeries : definition.XSeries;

            ScaleParameters sp = new ScaleParameters
                                     {
                                         X = { AllowedScaleTypes = new[] { ScaleType.Linear } },
                                         Y = { AllowedScaleTypes = new[] { ScaleType.NotSet } }
                                     };
            sp.X.ShouldCheck = true;
            sp.Y.ShouldCheck = false;
            double transTemp72;
            double transTemp73;
            GetMinMaxSort(seriesToUse, out transTemp72, out transTemp73);
            sp.X.Min = transTemp72;
            sp.X.Max = transTemp73;
            sp.Y.Max = 0;
            sp.Y.Min = 0;
            return sp;
        }


        // TRANSMISSINGCOMMENT: Method PlotSpread
        private ParameterBag PlotSpread(Stream outputStream, ITemplateHost host)
        {
            SpreadOptions sOptions = ((SpreadOptions)(definition.ChartOptions));
            if (sOptions.Orientation == ChartOrientation.Horizontal)
            {
                return PlotSpreadHorizontal(outputStream);
            }
            if (definition.XSeries.Count == 0 && definition.YSeries.Count > 0)
            {
                definition = definition.Clone();
                List<Series> temp = definition.XSeries;
                definition.XSeries = definition.YSeries;
                definition.YSeries = temp;
                AxisScaleParameters tempAxisScaleParameters = definition.ScaleParameters.X;
                definition.ScaleParameters.X = definition.ScaleParameters.Y;
                definition.ScaleParameters.Y = tempAxisScaleParameters;
            }
            return PlotSpreadVertical(outputStream);
        }


        // TRANSMISSINGCOMMENT: Method PlotSpreadHorizontal
        private ParameterBag PlotSpreadHorizontal(Stream OutputStream)
        {
            double xtra = 0;

            SpreadOptions sOptions = ((SpreadOptions)(definition.ChartOptions));
            List<Series> SeriesToUse = definition.YSeries.Count > 0 ? definition.YSeries : definition.XSeries;

            int k = SeriesToUse.Count;
            if (k > 10)
            {
                scaleYAxis = 1 + (k - 10) / 20;
                if (scaleYAxis > 5)
                {
                    scaleYAxis = 5;
                }
                metaH = scaleYAxis * DEFAULT_METAH;
            }
            else
            {
                scaleYAxis = 1;
                metaH = DEFAULT_METAH;
            }

            GetMinMaxSort(SeriesToUse, out dataMinX, out dataMaxX);

            DefaultAxes();

            StartMetafile(OutputStream, true);
            SetFontsAndThicknessesFromOptions(sOptions);
            AssignMarkersToSeries(sOptions);
            foreach (Series s in SeriesToUse)
            {
                double w = LegendWidth(s.Title) + 20;
                if (w > xtra + xAxisCanvas)
                {
                    xtra = w - xAxisCanvas;
                }
            }

            // Draw the scale
            DrawAxes(sOptions.Title, new Axis(sOptions.XAxisTitle, AxisMode.Scale, 0, definition.ScaleParameters.X.ScaleType), new Axis(null, AxisMode.Series, xtra, definition.ScaleParameters.Y.ScaleType), sOptions.ShouldBoxAxes, true, false);

            // get the offsets for the Markers
            divx = axisXMax - axisXMin;
            offx = -(axisXMin / divx * xExtCanvas) + xAxisCanvas;
            divy = SeriesToUse.Count;
            offy = yAxisCanvas;
            double ygap = yExtCanvas / divy;

            //  Work out what markers to use
            MarkerType mt = MarkerTypes[10]; //  Default
            double diam = mt.MarkerSize;
            if ((sOptions.MarkerTypes != null) && sOptions.MarkerTypes.Count > 0)
            {
                diam = sOptions.MarkerTypes[0].MarkerSize;
                mt = sOptions.MarkerTypes[0];
            }

            double INC = 2 * diam;
            double xxwid = divx / (xExtCanvas / INC);
            ygap -= diam * 2;

            for (int c = 0; c <= SeriesToUse.Count - 1; c++)
            {
                DoubleSeries s = SeriesToUse[c].AsDoubleSeries;
                int maxcount = 0;
                int r;
                for (r = 0; r <= s.Points - 1; r++)
                {
                    double v1 = s.Data[r];
                    int r1;
                    for (r1 = r + 1; r1 <= s.Points - 1; r1++)
                    {
                        if (Math.Abs(v1 - s.Data[r1]) > xxwid)
                        {
                            break; /* TRANSWARNING: check that break is in correct scope */
                        }
                    }
                    int count = r1 - r;
                    if (count > maxcount)
                    {
                        maxcount = count;
                    }
                }

                double scl;
                if (maxcount * INC > ygap)
                {
                    scl = ygap / (maxcount * INC);
                }
                else { scl = 1.0; }
                double yctr = ToCanvasY(c + 0.5);

                r = 0;
                while (r < s.Points)
                {
                    double v1 = s.Data[r];
                    int r1;
                    for (r1 = r + 1; r1 <= s.Points - 1; r1++)
                    {
                        if (Math.Abs(v1 - s.Data[r1]) > xxwid)
                        {
                            break; /* TRANSWARNING: check that break is in correct scope */
                        }
                    }
                    // Plot r1-r markers
                    int count = r1 - r;
                    double y1 = yctr - scl * ((count * diam) + diam);
                    double x1 = ToCanvasX(v1);
                    double lasty1 = 0;
                    for (int i = 1; i <= count; i++)
                    {
                        y1 += (INC * scl);
                        if (Math.Abs(lasty1 - y1) > 2)
                        {
                            DrawMarker(x1, y1, mt.MarkerSize, mt);
                            lasty1 = y1;
                        }
                    }
                    r = r1;
                }
            }
            EndMetafile();
            return new ParameterBag();
        }

        private ParameterBag PlotSpreadVertical(Stream outputStream)
        {
            SpreadOptions sOptions = ((SpreadOptions)(definition.ChartOptions));
            List<Series> seriesToUse = definition.YSeries.Count > 0 ? definition.YSeries : definition.XSeries;

            int k = seriesToUse.Count;
            if (k > 10)
            {
                scaleXAxis = 1 + (k - 10) / 20;
                if (scaleXAxis > 5)
                {
                    scaleXAxis = 5;
                }
                MetaW = scaleXAxis * DEFAULT_METAW;
            }
            else
            {
                scaleXAxis = 1;
                MetaW = DEFAULT_METAW;
            }

            GetMinMaxSort(seriesToUse, out dataMinY, out dataMaxY);

            DefaultAxes();

            StartMetafile(outputStream, true);
            SetFontsAndThicknessesFromOptions(sOptions);
            AssignMarkersToSeries(sOptions);

            // Draw the scale
            //  TODO: Should we be using the X axis title for something that will be shown vertically?
            DrawAxes(sOptions.Title, new Axis(null, AxisMode.Series, 0, definition.ScaleParameters.X.ScaleType), new Axis(sOptions.XAxisTitle, AxisMode.Scale, 0, definition.ScaleParameters.Y.ScaleType), sOptions.ShouldBoxAxes, true, false);

            // get the offsets for the Markers
            divy = axisYMax - axisYMin;
            offy = -(axisYMin / divy * yExtCanvas) + yAxisCanvas;
            divx = seriesToUse.Count;
            offx = xAxisCanvas;
            double xgap = xExtCanvas / divx;

            //  Work out what markers to use
            MarkerType mt = MarkerTypes[10]; //  Default
            double diam = mt.MarkerSize;
            if ((sOptions.MarkerTypes != null) && sOptions.MarkerTypes.Count > 0)
            {
                diam = sOptions.MarkerTypes[0].MarkerSize;
                mt = sOptions.MarkerTypes[0];
            }

            // double diamy = diam - 1; 
            double INC = 2 * diam;
            double yywid = divy / (yExtCanvas / INC);
            xgap -= diam * 2;

            for (int C = 0; C <= seriesToUse.Count - 1; C++)
            {
                DoubleSeries s = seriesToUse[C].AsDoubleSeries;
                int maxcount = 0;
                int r;
                for (r = 0; r <= s.Points - 1; r++)
                {
                    double v1 = s.Data[r];
                    int r1;
                    for (r1 = r + 1; r1 <= s.Points - 1; r1++)
                    {
                        if (Math.Abs(v1 - s.Data[r1]) > yywid)
                        {
                            break; /* TRANSWARNING: check that break is in correct scope */
                        }
                    }
                    int count = r1 - r;
                    if (count > maxcount)
                    {
                        maxcount = count;
                    }
                }

                double scl;
                if (maxcount * INC > xgap)
                {
                    scl = xgap / (maxcount * INC);
                }
                else { scl = 1.0; }
                double xctr = ToCanvasX(C + 0.5);

                r = 0;
                while (r < s.Points)
                {
                    double v1 = s.Data[r];
                    int r1;
                    for (r1 = r + 1; r1 <= s.Points - 1; r1++)
                    {
                        if (Math.Abs(v1 - s.Data[r1]) > yywid)
                        {
                            break; /* TRANSWARNING: check that break is in correct scope */
                        }
                    }
                    // Plot r1-r markers
                    int count = r1 - r;
                    double x1 = xctr - scl * ((count * diam) + diam);
                    double y1 = ToCanvasY(v1);
                    double lastx1 = 0;
                    for (int i = 1; i <= count; i++)
                    {
                        x1 += (INC * scl);
                        if (Math.Abs(lastx1 - x1) > 2)
                        {
                            DrawMarker(x1, y1, mt.MarkerSize, mt);
                            lastx1 = x1;
                        }
                    }
                    r = r1;
                }

            }
            EndMetafile();
            return new ParameterBag();
        }


        // TRANSMISSINGCOMMENT: Method GetRocScaleParameters
        private ScaleParameters GetRocScaleParameters()
        {
            return new ScaleParameters
                                     {
                                         X =
                                             {
                                                 AllowedScaleTypes = new[] { ScaleType.Linear },
                                                 ShouldCheck = false,
                                                 Max = 1,
                                                 Min = 0
                                             },
                                         Y =
                                             {
                                                 AllowedScaleTypes = new[] { ScaleType.Linear },
                                                 ShouldCheck = false,
                                                 Max = 1,
                                                 Min = 0
                                             }
                                     };
        }


        ///  <summary>
        ///  Plot a ROC chart to the specified stream
        ///  </summary>
        ///  <param name="OutputStream"></param>
        /// <param name="host"></param>
        public ParameterBag PlotROC(Stream OutputStream, ITemplateHost host)
        {
            ROCOptions rOptions = ((ROCOptions)(definition.ChartOptions));
            double GAMMA = rOptions.GAMMA;
            if (GAMMA <= 0)
            {
                return null;
            }
            double cit;
            double P0;
            MathDbl.civ(0, out cit, GAMMA, out P0);

            //  Assume data passed as series - X is present, Y is absent.

            ROCSeriesRecord[] seriesData = new ROCSeriesRecord[definition.XSeries.Count + 1 /* for VB to C# conversion */ ];
            for (int C = 0; C <= definition.XSeries.Count - 1; C++)
            {
                seriesData[C] = new ROCSeriesRecord();
                DoubleSeries xs = definition.XSeries[C].AsDoubleSeries;
                DoubleSeries ys = definition.YSeries[C].AsDoubleSeries;
                ROCSeriesRecord transTemp78 = seriesData[C];
                transTemp78.pdata = xs.Data;
                transTemp78.adata = ys.Data;
                transTemp78.pmn = xs.Sum / Convert.ToDouble(xs.Points);
                transTemp78.amn = ys.Sum / Convert.ToDouble(ys.Points);
                transTemp78.min = Math.Min(xs.Min, ys.Min);
                transTemp78.max = Math.Max(xs.Max, ys.Max);
                // get a sorted list of all data in order to calculate cut points
                transTemp78.tdata = new double[xs.Points + ys.Points];
                for (int r = 0; r < xs.Points; r++)
                {
                    transTemp78.tdata[r] = xs.Data[r];
                }
                for (int r = 0; r < ys.Points; r++)
                {
                    transTemp78.tdata[xs.Points + r] = ys.Data[r];
                }
                Array.Sort(transTemp78.tdata);

            }

            //  Work out how many series there are and extend the plot area as required to hold the legend

            //  Measurements and set axes.  These are done on a scratchpad canvas before the proper measurements are set up.
            double legendFontHeight;
            using (MemoryStream scratchStream = new MemoryStream())
            {
                StartMetafile(scratchStream, true);
                SetFontsAndThicknessesFromOptions(rOptions);
                DefaultAxes();
                double smallerExt = Math.Min(xExtCanvas, yExtCanvas);
                xExtCanvas = smallerExt;
                yExtCanvas = smallerExt;
                legendFontHeight = legendFont.GetHeight(canvas);
                EndMetafile();
            }

            //  By now, all measurements are known.  Set up the plot areas.
            double legendTop = yAxisCanvas - LEGEND_TOP_GAP;
            double markerMidlineOffset = (legendFontHeight - LEGEND_MARKER_SIZE) / 2;
            double legendSpacing = MINIMUM_LEGEND_GAP + Math.Max(LEGEND_MARKER_SIZE, Convert.ToInt32(legendFontHeight));
            double legendBottom = legendTop - (definition.XSeries.Count * legendSpacing);
            if (legendBottom < LOWEST_ALLOWED_LEGEND)
            {
                double extraSpaceRequired = LOWEST_ALLOWED_LEGEND - legendBottom;

                //  Add in the extra space
                metaH += extraSpaceRequired;
                yAxisCanvas += extraSpaceRequired;
                legendTop += extraSpaceRequired;
                // legendBottom += extraSpaceRequired; 
            }

            StartMetafile(OutputStream, false);
            SetFontsAndThicknessesFromOptions(rOptions);
            AssignMarkersToSeries(rOptions);

            DataMinX = 0;
            DataMaxX = 1;
            DataMinY = 0;
            DataMaxY = 1;

            // Draw the scale
            DrawAxes(rOptions.Title, new Axis("1-Specificity", AxisMode.Scale, 0, ScaleType.Linear), new Axis("Sensitivity", AxisMode.Scale, 0, ScaleType.Linear), true, false, false); //  We've hacked at the axes; don't re-default them.

            // null effect diagonal
            using (Pen tenPenDiagonal = new Pen(_markerTypes[10].Color, rOptions.AxisLineThickness))
            {
                DrawLine(tenPenDiagonal, xAxisCanvas, yAxisCanvas, xAxisCanvas + xExtCanvas, yAxisCanvas + yExtCanvas);
            }

            // get the offsets for the Markers
            offx = xAxisCanvas;
            offy = yAxisCanvas;

            bool hideopt = !(rOptions.ShowOptimumCutOff);
            ComparisonValue showopt = rOptions.Showopts;
            ParameterBag results = new ParameterBag();
            IList<ParameterBag> allResults = new List<ParameterBag>();
            results.AddOutput("*", allResults);
            for (int cs = 0; cs <= definition.XSeries.Count - 1; cs++)
            {
                ROCSeriesRecord thisData = seriesData[cs];
                DoubleSeries xs = definition.XSeries[cs].AsDoubleSeries;
                DoubleSeries ys = definition.YSeries[cs].AsDoubleSeries;
                //  TODO: Move this to the options screen
                // If thisData.pmn > thisData.amn Then
                // descriptor.CheckBoxes(0).Checked = True
                // showopt = ComparisonValue.GE
                // Else
                // descriptor.CheckBoxes(2).Checked = True
                // showopt = ComparisonValue.LE
                // End If
                double Weight = rOptions.Weight;
                if (Weight <= 0)
                {
                    Weight = 1.0;
                }

                // Draw the legend for each series
                DrawMarker(xAxisCanvas + LEGEND_MARKER_SIZE / 2.0, legendTop - (cs * legendSpacing) - markerMidlineOffset, LEGEND_MARKER_SIZE, definition.YSeries[cs].AsDoubleSeries);
                DrawStringLegendL(rOptions.SeriesTitles[cs], xAxisCanvas + 9 + LEGEND_MARKER_SIZE, legendTop - (cs * legendSpacing));

                int a;
                int b;
                int C;
                int D;
                double CUTOFF;
                double sens;
                if (!(hideopt))
                {
                    // work out cutoff for max(weight*sens+spec)
                    double maxss = 0.0;
                    for (int r = 0; r <= thisData.tdata.Length - 1; r++)
                    {
                        a = 0;
                        b = 0;
                        CUTOFF = thisData.tdata[r];
                        for (int j = 0; j <= thisData.pdata.Length - 1; j++)
                        {
                            switch (showopt)
                            {
                                case ComparisonValue.LT:
                                    if (thisData.pdata[j] < CUTOFF)
                                    {
                                        a = a + 1;
                                    }
                                    break;
                                case ComparisonValue.LE:
                                    if (thisData.pdata[j] <= CUTOFF)
                                    {
                                        a = a + 1;
                                    }
                                    break;
                                case ComparisonValue.GT:
                                    if (thisData.pdata[j] > CUTOFF)
                                    {
                                        a = a + 1;
                                    }
                                    break;
                                default:
                                    if (thisData.pdata[j] >= CUTOFF)
                                    {
                                        a = a + 1;
                                    }
                                    break;
                            }

                        }
                        C = thisData.pdata.Length - a;
                        for (int j = 0; j <= thisData.adata.Length - 1; j++)
                        {
                            switch (showopt)
                            {
                                case ComparisonValue.LT:
                                    if (thisData.adata[j] < CUTOFF)
                                    {
                                        b = b + 1;
                                    }
                                    break;
                                case ComparisonValue.LE:
                                    if (thisData.adata[j] <= CUTOFF)
                                    {
                                        b = b + 1;
                                    }
                                    break;
                                case ComparisonValue.GT:
                                    if (thisData.adata[j] > CUTOFF)
                                    {
                                        b = b + 1;
                                    }
                                    break;
                                default:
                                    if (thisData.adata[j] >= CUTOFF)
                                    {
                                        b = b + 1;
                                    }
                                    break;
                            }

                        }
                        D = thisData.adata.Length - b;
                        sens = Convert.ToDouble(a) / Convert.ToDouble(a + C);
                        double spec = Convert.ToDouble(D) / Convert.ToDouble(b + D);
                        if (Weight * sens + spec > maxss)
                        {
                            maxss = Weight * sens + spec;
                            thisData.cutoff = CUTOFF;
                            thisData.a = a;
                            thisData.b = b;
                            thisData.c = C;
                            thisData.d = D;
                            thisData.sens = sens;
                            thisData.spec = spec;
                        }
                    }

                    // Cutoff calculator
                    thisData.comp = showopt;
                    if (rOptions.ShowCutOffCalculator)
                    {
                        string q = "ROC plot for " + rOptions.SeriesTitles[cs];
                        thisData = ShowCutoff(host, thisData, Weight, q);
                    }
                    seriesData[cs] = thisData;

                }

                // make first mark
                a = 0;
                b = 0;
                CUTOFF = thisData.cutoff;
                for (int r = 0; r <= thisData.pdata.Length - 1; r++)
                {
                    switch (showopt)
                    {
                        case ComparisonValue.LT:
                            if (thisData.pdata[r] < CUTOFF)
                            {
                                a = a + 1;
                            }
                            break;
                        case ComparisonValue.LE:
                            if (thisData.pdata[r] <= CUTOFF)
                            {
                                a = a + 1;
                            }
                            break;
                        case ComparisonValue.GT:
                            if (thisData.pdata[r] > CUTOFF)
                            {
                                a = a + 1;
                            }
                            break;
                        default:
                            if (thisData.pdata[r] >= CUTOFF)
                            {
                                a = a + 1;
                            }
                            break;
                    }

                }
                C = thisData.pdata.Length - a;
                for (int r = 0; r <= thisData.adata.Length - 1; r++)
                {
                    switch (showopt)
                    {
                        case ComparisonValue.LT:
                            if (thisData.adata[r] < CUTOFF)
                            {
                                b = b + 1;
                            }
                            break;
                        case ComparisonValue.LE:
                            if (thisData.adata[r] <= CUTOFF)
                            {
                                b = b + 1;
                            }
                            break;
                        case ComparisonValue.GT:
                            if (thisData.adata[r] > CUTOFF)
                            {
                                b = b + 1;
                            }
                            break;
                        default:
                            if (thisData.adata[r] >= CUTOFF)
                            {
                                b = b + 1;
                            }
                            break;
                    }

                }
                D = thisData.adata.Length - b;
                sens = Convert.ToDouble(a) / Convert.ToDouble(a + C);
                double mspec = 1.0 - Convert.ToDouble(D) / Convert.ToDouble(b + D);
                double x1 = offx + mspec * xExtCanvas;
                double y1 = offy + sens * yExtCanvas;

                int stps = thisData.tdata.Length;
                double[] rx = new double[stps + 1 /* for VB to C# conversion */ ];
                double[] ry = new double[stps + 1 /* for VB to C# conversion */ ];

                for (int r = 0; r <= stps - 1; r++)
                {
                    a = 0;
                    b = 0;
                    CUTOFF = thisData.tdata[r];
                    for (int j = 0; j <= thisData.pdata.Length - 1; j++)
                    {
                        switch (showopt)
                        {
                            case ComparisonValue.LT:
                                if (thisData.pdata[j] < CUTOFF)
                                {
                                    a = a + 1;
                                }
                                break;
                            case ComparisonValue.LE:
                                if (thisData.pdata[j] <= CUTOFF)
                                {
                                    a = a + 1;
                                }
                                break;
                            case ComparisonValue.GT:
                                if (thisData.pdata[j] > CUTOFF)
                                {
                                    a = a + 1;
                                }
                                break;
                            default:
                                if (thisData.pdata[j] >= CUTOFF)
                                {
                                    a = a + 1;
                                }
                                break;
                        }

                    }
                    C = thisData.pdata.Length - a;
                    for (int j = 0; j <= thisData.adata.Length - 1; j++)
                    {
                        switch (showopt)
                        {
                            case ComparisonValue.LT:
                                if (thisData.adata[j] < CUTOFF)
                                {
                                    b = b + 1;
                                }
                                break;
                            case ComparisonValue.LE:
                                if (thisData.adata[j] <= CUTOFF)
                                {
                                    b = b + 1;
                                }
                                break;
                            case ComparisonValue.GT:
                                if (thisData.adata[j] > CUTOFF)
                                {
                                    b = b + 1;
                                }
                                break;
                            default:
                                if (thisData.adata[j] >= CUTOFF)
                                {
                                    b = b + 1;
                                }
                                break;
                        }

                    }
                    D = thisData.adata.Length - b;
                    sens = Convert.ToDouble(a) / Convert.ToDouble(a + C);
                    ry[r] = sens;
                    mspec = 1.0 - Convert.ToDouble(D) / Convert.ToDouble(b + D);
                    rx[r] = mspec;
                }

                double x2;
                double Y2;
                for (int r = 0; r <= stps - 1; r++)
                {
                    x2 = offx + rx[r] * xExtCanvas;
                    Y2 = offy + ry[r] * yExtCanvas;
                    DrawMarker(x2, Y2, ys.MarkerSize, definition.YSeries[cs].AsDoubleSeries);
                }

                double last_x2 = x1;
                double last_y2 = y1;
                for (int r = 0; r <= stps - 1; r++)
                {
                    x2 = offx + rx[r] * xExtCanvas;
                    Y2 = offy + ry[r] * yExtCanvas;
                    if (r > 0 & (x2 != last_x2 | Y2 != last_y2))
                    {
                        DrawLine(xs.StyledPen, last_x2, last_y2, x2, Y2);
                    }
                    last_x2 = x2;
                    last_y2 = Y2;
                }

                // mark cutoff point
                x2 = offx + (1.0 - thisData.spec) * xExtCanvas;
                Y2 = offy + thisData.sens * yExtCanvas;
                DrawMarker(x2, Y2, rOptions.MarkerTypes[definition.XSeries.Count + cs].MarkerSize, rOptions.MarkerTypes[definition.XSeries.Count + cs]);

                thisData.auc = MathDbl.trapezoid_xy_roc(rx, ry, 0, stps);

                if (!(hideopt))
                {
                    ParameterBag thisResults = new ParameterBag();
                    allResults.Add(thisResults);
                    // Wilcoxon estimate for AUC
                    // Hanley JA, mcNeil BJ, Radiology 143:29-36
                    //  Note that mwx and mwr are 1-based!
                    double[] mwx = new double[thisData.pdata.Length + thisData.adata.Length + 1 /* for VB to C# conversion */ ];
                    double[] mwr = new double[thisData.pdata.Length + thisData.adata.Length + 1 /* for VB to C# conversion */ ];
                    for (int j = 0; j <= thisData.pdata.Length - 1; j++)
                    {
                        mwx[j + 1] = thisData.pdata[j];
                    }
                    for (int j = 0; j <= thisData.adata.Length - 1; j++)
                    {
                        mwx[thisData.pdata.Length + j + 1] = thisData.adata[j];
                    }
                    bool fault;
                    double u = 0;
                    double transTemp69 = 0;
                    double transTemp70 = 0;
                    double transTemp71 = 0;
                    NonParametric.x_mwut(ref mwx, mwx.Length - 1, thisData.pdata.Length, thisData.adata.Length, mwr, ref u, ref transTemp69, ref transTemp70, ref transTemp71, out fault);
                    double theta;
                    double ll;
                    double ul;
                    double sew = 0;
                    if (fault)
                    {
                        theta = Constant.MISSING;
                        ll = Constant.MISSING;
                        ul = Constant.MISSING;
                    }
                    else
                    {
                        if (Convert.ToDouble(thisData.pdata.Length * thisData.adata.Length) - u > u)
                        {
                            u = Convert.ToDouble(thisData.pdata.Length * thisData.adata.Length) - u;
                        }
                        theta = u / Convert.ToDouble(thisData.pdata.Length * thisData.adata.Length);
                        // Q1 = theta / (2# - theta)
                        // Q2 = (2# * (theta ^ 2#)) / (1# + theta)
                        // sew = Sqr((theta * (1# - theta) + CDbl(rowsp(cs) - 1) * (Q1 - theta ^ 2#) + CDbl(rowsa(cs) - 1) * (Q2 - theta# ^ 2#)) / CDbl(rowsp(cs) * rowsa(cs)))
                        sew = DeLongSE(thisData.pdata, thisData.adata, theta);
                        if (sew == Constant.MISSING)
                        {
                            ll = Constant.MISSING;
                            ul = Constant.MISSING;
                        }
                        else
                        {
                            ll = theta - cit * sew;
                            ul = theta + cit * sew;
                        }
                    }
                    // end of Wilcoxon extimate
                    string warn;
                    thisResults.AddOutput("ti", rOptions.SeriesTitles[cs]);
                    thisResults.AddOutput("auc", host.RoundU(thisData.auc));
                    thisResults.AddOutput("theta", host.RoundU(theta));
                    thisResults.AddOutput("se", host.RoundU(sew));
                    thisResults.AddOutput("pc", Formatting.XRound(100.0 * (1.0 - P0), 2));
                    if (ll < 0.0)
                    {
                        ll = 0.0;
                    }
                    thisResults.AddOutput("ll", host.RoundU(ll));
                    if (ul > 1.0)
                    {
                        ul = 1.0;
                    }
                    thisResults.AddOutput("ul", host.RoundU(ul));
                    thisResults.AddOutput("cut", host.RoundU(thisData.cutoff));
                    thisResults.AddOutput("a", thisData.a.ToString());
                    thisResults.AddOutput("b", thisData.b.ToString());
                    thisResults.AddOutput("c", thisData.c.ToString());
                    thisResults.AddOutput("d", thisData.d.ToString());
                    // sensitivity CI
                    MathDbl.binci(Convert.ToDouble(thisData.a), Convert.ToDouble(thisData.a + thisData.c), out ll, out ul, GAMMA, out warn);
                    thisResults.AddOutput("senspc", Formatting.XRound(100.0 * (1.0 - P0), 2));
                    thisResults.AddOutput("sens", host.RoundU(thisData.sens));
                    thisResults.AddOutput("sensll", host.RoundU(ll));
                    thisResults.AddOutput("sensul", host.RoundU(ul) + warn);
                    // specificity CI
                    MathDbl.binci(Convert.ToDouble(thisData.d), Convert.ToDouble(thisData.d + thisData.b), out ll, out ul, GAMMA, out warn);
                    thisResults.AddOutput("specpc", Formatting.XRound(100.0 * (1.0 - P0), 2));
                    thisResults.AddOutput("spec", host.RoundU(thisData.spec));
                    thisResults.AddOutput("specll", host.RoundU(ll));
                    thisResults.AddOutput("specul", host.RoundU(ul) + warn);

                    //  BEWARE from this point on: a, b, c, d are integer, but divisions need to deal with floating-point.
                    // prevalence
                    double N = thisData.a + thisData.b + thisData.c + thisData.d;
                    double prevel = Convert.ToDouble(thisData.a + thisData.c) / N;

                    // ppv
                    double ptld;
                    double temp1; double temp2;
                    if (thisData.a + thisData.b > 0)
                    {
                        ptld = thisData.a / Convert.ToDouble(thisData.a + thisData.b);
                        temp1 = ptld * 100.0;
                        temp2 = Convert.ToInt64(ptld * 100.0) - Convert.ToInt64(prevel * 100.0);
                    }
                    else
                    {
                        ptld = Constant.MISSING;
                        temp1 = Constant.MISSING;
                        temp2 = Constant.MISSING;
                    }
                    thisResults.AddOutput("likely", host.RoundU(ptld));
                    // Clopper-Pearson CI
                    double pil; double piu;
                    MathDbl.binci(thisData.a, thisData.a + thisData.b, out pil, out piu, GAMMA, out warn);
                    thisResults.AddOutput("likely_from", host.RoundU(pil));
                    thisResults.AddOutput("likely_to", host.RoundU(piu) + warn);
                    // as percentage
                    thisResults.AddOutput("likely_pc", Formatting.XRound(temp1, 2));
                    if (pil != Constant.MISSING)
                    {
                        pil = 100.0 * pil;
                    }
                    else { pil = Constant.MISSING; }
                    thisResults.AddOutput("likely_from_pc", Formatting.XRound(pil, 2));
                    if (piu != Constant.MISSING)
                    {
                        piu = 100.0 * piu;
                    }
                    else { piu = Constant.MISSING; }
                    thisResults.AddOutput("likely_to_pc", Formatting.XRound(piu, 2));
                    // change
                    thisResults.AddOutput("likely_change", Formatting.XRound(temp2, 2));

                    // npv
                    double ptlng;
                    if (thisData.d + thisData.c > 0)
                    {
                        ptlng = thisData.d / Convert.ToDouble(thisData.d + thisData.c);
                        temp1 = ptlng * 100.0;
                        temp2 = Convert.ToInt32(ptlng * 100.0) - Convert.ToInt32((Convert.ToDouble(thisData.b + thisData.d) / N) * 100.0);
                    }
                    else
                    {
                        ptlng = Constant.MISSING;
                        temp1 = Constant.MISSING;
                        temp2 = Constant.MISSING;
                    }
                    thisResults.AddOutput("likely_negative", host.RoundU(ptlng));
                    // Clopper-Pearson CI
                    MathDbl.binci(thisData.d, thisData.d + thisData.c, out pil, out piu, GAMMA, out warn);
                    thisResults.AddOutput("likely_negative_from", host.RoundU(pil));
                    thisResults.AddOutput("likely_negative_to", host.RoundU(piu) + warn);
                    // as percentage
                    thisResults.AddOutput("likely_negative_pc", Formatting.XRound(temp1, 2));
                    if (pil != Constant.MISSING)
                    {
                        pil = 100.0 * pil;
                    }
                    else { pil = Constant.MISSING; }
                    thisResults.AddOutput("likely_negative_from_pc", Formatting.XRound(pil, 2));
                    if (piu != Constant.MISSING)
                    {
                        piu = 100.0 * piu;
                    }
                    else { piu = Constant.MISSING; }
                    thisResults.AddOutput("likely_negative_to_pc", Formatting.XRound(piu, 2));
                    // change
                    thisResults.AddOutput("likely_negative_change", Formatting.XRound(temp2, 2));

                    // p[dx] despite -ve test
                    double ptlnd;
                    if (thisData.d + thisData.c > 0)
                    {
                        ptlnd = 1.0 - (thisData.d / Convert.ToDouble(thisData.d + thisData.c));
                        temp1 = ptlnd * 100.0;
                        temp2 = Convert.ToInt32(ptlnd * 100.0) - Convert.ToInt32(prevel * 100.0);
                    }
                    else
                    {
                        ptlnd = Constant.MISSING;
                        temp1 = Constant.MISSING;
                        temp2 = Constant.MISSING;
                    }
                    thisResults.AddOutput("likely_despite", host.RoundU(ptlnd));
                    // Clopper-Pearson CI
                    MathDbl.binci(thisData.d, thisData.d + thisData.c, out pil, out piu, GAMMA, out warn);
                    thisResults.AddOutput("likely_despite_from", host.RoundU(Math.Min(1.0 - pil, 1.0 - piu)));
                    thisResults.AddOutput("likely_despite_to", host.RoundU(Math.Max(1.0 - pil, 1.0 - piu)) + warn);
                    // as percentage
                    thisResults.AddOutput("likely_despite_pc", Formatting.XRound(temp1, 2));
                    if (pil != Constant.MISSING)
                    {
                        pil = 100.0 * (1.0 - pil);
                    }
                    else { pil = Constant.MISSING; }
                    if (piu != Constant.MISSING)
                    {
                        piu = 100.0 * (1.0 - piu);
                    }
                    else { piu = Constant.MISSING; }
                    thisResults.AddOutput("likely_despite_from_pc", Formatting.XRound(Math.Min(pil, piu), 2));
                    thisResults.AddOutput("likely_despite_to_pc", Formatting.XRound(Math.Max(pil, piu), 2));
                    // change
                    thisResults.AddOutput("likely_despite_change", Formatting.XRound(temp2, 2));
                }
            }
            EndMetafile();
            return results;
        }


        // TRANSMISSINGCOMMENT: Method GetNormalScaleParameters
        private ScaleParameters GetNormalScaleParameters()
        {
            NormalOptions nOptions = ((NormalOptions)(definition.ChartOptions));
            NormalOptions.ScoreMethod method = nOptions.Method;

            DoubleSeries xs0 = definition.XSeries[0].AsDoubleSeries;
            int rows = xs0.Points;

            double[] y = new double[rows - 1 + 1 /* for VB to C# conversion */ ];
            for (int j = 0; j <= rows - 1; j++)
            {
                y[j] = xs0.Data[j];
            }

            double[] x = new double[rows - 1 + 1 /* for VB to C# conversion */ ];
            double transTemp68;
            ExFortran.Rank(y, x, 0, rows, 0, out transTemp68);

            if (method == NormalOptions.ScoreMethod.ExpectedNormalOrder)
            {
                if (rows > 4000)
                {
                    method = NormalOptions.ScoreMethod.VanDerWaerden;
                }
            }

            int nn = xs0.Points;
            for (int j = 0; j <= rows - 1; j++)
            {
                // X(j) = GAUINV(((2 * X(j)) - 1) / (2 * nn), 0)
                int ifault;
                switch (method)
                {
                    case NormalOptions.ScoreMethod.VanDerWaerden:
                        //  van der Waerden, Conover P 396
                        x[j] = PDF.gauinv(x[j] / (Convert.ToDouble(nn) + 1.0), out ifault);
                        if (ifault != 0)
                        {
                            x[j] = Constant.MISSING;
                        }
                        break;
                    case NormalOptions.ScoreMethod.Blom:
                        //  Blom - Altman p143
                        x[j] = PDF.gauinv(x[j] / (Convert.ToDouble(nn) + 1.0), out ifault);
                        if (ifault != 0)
                        {
                            x[j] = Constant.MISSING;
                        }
                        break;
                    default:
                        //  expected normal order
                        x[j] = PDF.expnos(Convert.ToInt32(x[j]), nn);
                        break;
                }
            }

            ScaleParameters sp = new ScaleParameters
                                     {
                                         X = { AllowedScaleTypes = new[] { ScaleType.Linear }, ShouldCheck = true },
                                         Y = { AllowedScaleTypes = new[] { ScaleType.Linear }, ShouldCheck = true }
                                     };


            double transTemp66;
            double transTemp67;
            GetMinMaxArray(x, out transTemp66, out transTemp67);
            sp.Y.Min = transTemp66;
            sp.Y.Max = transTemp67;
            double transTemp64;
            double transTemp65;
            GetMinMaxArray(y, out transTemp64, out transTemp65);
            sp.X.Min = transTemp64;
            sp.X.Max = transTemp65;
            return sp;
        }


        ///  <summary>
        ///  Plot normal scores for a single variable in XSeries.
        ///  </summary>
        ///  <returns></returns>
        ///  <remarks></remarks>
        private ParameterBag PlotNormal(Stream outputStream, ITemplateHost host)
        {
            DoubleSeries xs0 = definition.XSeries[0].AsDoubleSeries;
            int rows = xs0.Points;

            double[] y = new double[rows - 1 + 1 /* for VB to C# conversion */ ];
            for (int j = 0; j <= rows - 1; j++)
            {
                y[j] = xs0.Data[j];
            }

            StartMetafile(outputStream, true);
            Plot_Normal(y);
            EndMetafile();
            return new ParameterBag();
        }

        public string PlotNormalAndReturnRtf(ITemplateHost host, double[] y)
        {
            using (MemoryStream metaStream = new MemoryStream())
            {
                StartMetafile(metaStream);
                Plot_Normal(y);
                EndMetafile();
                return host.ImageStreamToRtf(metaStream);
            }
        }

        ///  <summary>
        ///  Plot normal scores for a single variable in XSeries.
        ///  </summary>
        ///  <remarks></remarks>
        private void Plot_Normal(double[] y)
        {
            NormalOptions nOptions = ((NormalOptions)(definition.ChartOptions));
            NormalOptions.ScoreMethod method = nOptions.Method;
            bool scaling = nOptions.Scaling;

            int rows = y.Length;

            double sy = 0;
            for (int j = 0; j <= rows - 1; j++)
            {
                sy += y[j];
            }
            double ybar = sy / rows;
            double ssy = 0;
            for (int j = 0; j <= rows - 1; j++)
            {
                double d = y[j] - ybar;
                ssy += d * d;
            }
            double vary = ssy / rows;
            double sdy = Math.Sqrt(vary);


            double[] x = new double[rows - 1 + 1 /* for VB to C# conversion */ ];
            double transTemp63;
            ExFortran.Rank(y, x, 0, rows, 0, out transTemp63);

            if (method == NormalOptions.ScoreMethod.ExpectedNormalOrder)
            {
                if (rows > 4000)
                {
                    method = NormalOptions.ScoreMethod.VanDerWaerden;
                }
            }
            string Lab;
            if (scaling)
            {
                Lab = "Normal (" + definition.XSeries[0].Title + ")";
            }
            else
            {
                switch (method)
                {
                    case NormalOptions.ScoreMethod.VanDerWaerden:
                        Lab = "Normal scores (van der Waerden)";
                        break;
                    case NormalOptions.ScoreMethod.Blom:
                        Lab = "Normal scores (Blom)";
                        break;
                    default:
                        Lab = "Expected normal order scores";
                        break;
                }

            }

            int nn = y.Length;
            for (int j = 0; j <= rows - 1; j++)
            {
                int ifault;
                switch (method)
                {
                    case NormalOptions.ScoreMethod.VanDerWaerden:
                        //  van der Waerden, Conover P 396
                        x[j] = PDF.gauinv(x[j] / (Convert.ToDouble(nn) + 1.0), out ifault);
                        if (ifault != 0)
                        {
                            x[j] = Constant.MISSING;
                        }
                        else
                        {
                            if (scaling)
                            {
                                x[j] = x[j] * sdy + ybar;
                            }
                        }
                        break;
                    case NormalOptions.ScoreMethod.Blom:
                        //  Blom - Altman p143
                        x[j] = PDF.gauinv(x[j] / (Convert.ToDouble(nn) + 1.0), out ifault);
                        if (ifault != 0)
                        {
                            x[j] = Constant.MISSING;
                        }
                        else
                        {
                            if (scaling)
                            {
                                x[j] = x[j] * sdy + ybar;
                            }
                        }
                        break;
                    default:
                        //  expected normal order
                        x[j] = PDF.expnos(Convert.ToInt32(x[j]), nn);
                        if (scaling)
                        {
                            x[j] = x[j] * sdy + ybar;
                        }
                        break;
                }
            }

            SetFontsAndThicknessesFromOptions(nOptions);
            AssignMarkersToSeries(definition.XSeries, nOptions);

            int Select_MinMaxY = 0;
            if (scaling)
            {
                Select_MinMaxY = -97;
            }
            PlotXY(x, y, Lab, "Observed (" + definition.XSeries[0].Title + ")", nOptions.Title, false, Select_MinMaxY, MarkerTypes[0].MarkerSize, MarkerTypes[0].Shape, MarkerTypes[0].IsFilled, GetPen(MarkerTypes[0], true), true);
            if (scaling)
            {
                DrawLine(axisPen, xAxisCanvas, yAxisCanvas, xAxisCanvas + xExtCanvas, yAxisCanvas + yExtCanvas);
            }
        }

        private ScaleParameters GetPyramidScaleParameters()
        {
            PyramidOptions pOptions = ((PyramidOptions)(definition.ChartOptions));

            DataFrame maleFrame = pOptions.MaleFrame;
            DoubleVariable males = maleFrame.Variables[0].AsDoubleVariable;
            double maxmale = males.Max;

            double maxfemale;
            if (pOptions.FemaleFrame != null)
            {
                //  Separate male and female values
                DataFrame femaleFrame = pOptions.FemaleFrame;
                DoubleVariable females = femaleFrame.Variables[0].AsDoubleVariable;
                maxfemale = females.Max;
            }
            else
            {
                //  Combined male/female values - assume an even split
                maxmale = maxmale / 2.0;
                maxfemale = maxmale;
            }

            double tmax = maxfemale > maxmale ? maxfemale : maxmale;

            return new ScaleParameters
                                     {
                                         X =
                                             {
                                                 AllowedScaleTypes = new[] { ScaleType.Linear },
                                                 ShouldCheck = true,
                                                 Max = tmax,
                                                 Min = 0
                                             },
                                         Y =
                                             {
                                                 AllowedScaleTypes = new[] { ScaleType.Category },
                                                 ShouldCheck = false,
                                                 Max = 0,
                                                 Min = 0
                                             }
                                     };
        }


        // TRANSMISSINGCOMMENT: Method PlotPyramid
        private ParameterBag PlotPyramid(Stream OutputStream)
        {

            const int MINIMUM_X_WHITESPACE = 30;

            PyramidOptions pOptions = ((PyramidOptions)(definition.ChartOptions));

            DataFrame maleFrame = pOptions.MaleFrame;
            DoubleVariable males = maleFrame.Variables[0].AsDoubleVariable;
            int nmale = males.Length;
            double maxmale = males.Max;

            int nfemale = 0;
            double[] female;
            double[] male;
            double maxfemale = 0;
            int mode;
            if (pOptions.FemaleFrame != null)
            {
                //  Separate male and female values
                DataFrame femaleFrame = pOptions.FemaleFrame;
                DoubleVariable females = femaleFrame.Variables[0].AsDoubleVariable;
                female = new double[nmale - 1 + 1 /* for VB to C# conversion */ ];
                male = new double[nmale - 1 + 1 /* for VB to C# conversion */ ];
                maxfemale = females.Max;

                for (int r = 0; r <= nmale - 1; r++)
                {
                    if (females.Data[r] != Constant.MISSING && males.Data[r] != Constant.MISSING)
                    {
                        female[nfemale] = females.Data[r];
                        male[nfemale] = males.Data[r];
                        nfemale += 1;
                    }
                }
                nmale = nfemale;
                mode = 1;
            }
            else
            {
                //  Combined male/female values - assume an even split
                female = new double[nmale - 1 + 1 /* for VB to C# conversion */];
                male = new double[nmale - 1 + 1 /* for VB to C# conversion */];
                for (int r = 0; r <= nmale - 1; r++)
                {
                    if (males.Data[r] != Constant.MISSING)
                    {
                        female[nfemale] = males.Data[r] / 2;
                        male[nfemale] = males.Data[r] / 2;
                        nfemale += 1;
                    }
                }
                nmale = nfemale;
                mode = 2;
            }

            string[] title = new string[nmale + 1 /* for VB to C# conversion */ ];
            if (pOptions.LabelFrame != null)
            {
                StringVariable labels = pOptions.LabelFrame.Variables[0].AsStringVariable;
                int i;
                for (i = labels.Length - 1; i >= 0; i--)
                {
                    if ((labels.Data[i] != null) && labels.Data[i].Length > 0)
                    {
                        break; /* TRANSWARNING: check that break is in correct scope */
                    }
                }
                int lastrow = i;
                if (lastrow == nmale - 1)
                {
                    for (i = 0; i <= lastrow; i++)
                    {
                        if ((labels.Data[i] != null) && labels.Data[i].Length > 0)
                        {
                            title[i] = labels.Data[i];
                            if (title[i].Length > 50)
                            {
                                title[i] = title[i].Substring(0, 50);
                            }
                        }
                        else
                        {
                            title[i] = "group " + (i + 1).ToString();
                        }
                    }
                }
            }

            double tmax = maxfemale > maxmale ? maxfemale : maxmale;
            double tmx = pOptions.ScaleMaximum;

            double ScaleMax = tmx;
            if (ScaleMax < tmax)
            {
                ScaleMax = tmax;
            }

            Brush maleBrush = null;
            if (pOptions.MarkerTypes.Count >= 1)
            {
                maleBrush = MarkerTypeToBrush(pOptions.MarkerTypes[0]);
            }
            Brush femaleBrush = null;
            if (pOptions.MarkerTypes.Count >= 2)
            {
                femaleBrush = MarkerTypeToBrush(pOptions.MarkerTypes[1]);
            }

            if (nmale > 10)
            {
                scaleYAxis = 1 + (nmale - 10) / 20;
                if (scaleYAxis > 5)
                {
                    scaleYAxis = 5;
                }
                metaH = scaleYAxis * DEFAULT_METAH;
            }
            else
            {
                scaleYAxis = 1;
                metaH = DEFAULT_METAH;
            }

            StartMetafile(OutputStream, true);

            SetFontsAndThicknessesFromOptions(pOptions);

            //  Init_Axes()
            double xtra = 0;
            for (int i = 0; i <= nmale - 1; i++)
            {
                double w = AxisLabelWidth(title[i]) + MINIMUM_X_WHITESPACE;
                if (w > xtra + xAxisCanvas)
                {
                    xtra = w - xAxisCanvas - 5;
                }
            }

            xAxisCanvas = xAxisCanvas + xtra;
            xExtCanvas = xExtCanvas - xtra;

            using (StringFormat centerFormat = new StringFormat())
            {
                centerFormat.Alignment = StringAlignment.Center;
                DrawString(pOptions.Title, titleFont, Brushes.Black, (xExtCanvas / 2) + xAxisCanvas, yExtCanvas + 180, centerFormat);
            }

            using (StringFormat rightFormat = new StringFormat())
            {
                rightFormat.Alignment = StringAlignment.Far;
                double ystep = yExtCanvas / nmale;
                if (title[0].Length > 0)
                {
                    double txh = AxisLabelHeight(title[0]);
                    for (int i = 0; i <= nmale - 1; i++)
                    {
                        double yc = yAxisCanvas + (nmale - i) * ystep - ystep / 2;
                        DrawString(title[i], axisLabelFont, Brushes.Black, xAxisCanvas - 15, yc + txh / 2, rightFormat);
                    }
                }

                double xstep = xExtCanvas / 2;
                double xc = xAxisCanvas + xstep;
                using (Pen blackPen = GetPen(_markerTypes[10], true))
                {
                    for (int i = 0; i <= nmale - 1; i++)
                    {
                        double yt = yAxisCanvas + (nmale - i) * ystep;
                        double yb = yAxisCanvas + (nmale - i - 1) * ystep;
                        double xl = xAxisCanvas + xstep - (male[i] / ScaleMax) * xstep;
                        double xr = xAxisCanvas + xstep + (female[i] / ScaleMax) * xstep;
                        if (mode == 1)
                        {
                            //  Male/female
                            if (maleBrush != null)
                            {
                                FillRectangle(maleBrush, xl, yt, xc - xl, yt - yb);
                            }
                            if (femaleBrush != null)
                            {
                                FillRectangle(femaleBrush, xc, yt, xr - xc, yt - yb);
                            }
                        }
                        else
                        {
                            //  Just the one
                            if (maleBrush != null)
                            {
                                FillRectangle(maleBrush, xl, yt, xr - xl, yt - yb);
                            }
                        }
                        DrawRectangle(blackPen, xl, yt, xr - xl, yt - yb);
                    }
                    if (maleBrush != null)
                    {
                        maleBrush.Dispose();
                    }
                    if (femaleBrush != null)
                    {
                        femaleBrush.Dispose();
                    }

                    using (StringFormat leftFormat = new StringFormat())
                    {
                        leftFormat.Alignment = StringAlignment.Near;

                        if (mode == 1)
                        {
                            DrawLine(blackPen, xc, yAxisCanvas, xAxisCanvas + xstep, yAxisCanvas + nmale * ystep);
                            DrawString("male", axisLabelFont, Brushes.Black, (xExtCanvas / 4) + xAxisCanvas, yAxisCanvas - 12, leftFormat);
                            DrawString("female", axisLabelFont, Brushes.Black, (xExtCanvas / 4) + (xExtCanvas / 2) + xAxisCanvas, yAxisCanvas - 12, leftFormat);
                        }

                        DrawString("Scale maximum = " + ScaleMax.ToString(), axisLabelFont, Brushes.Black, 40, yAxisCanvas - 40, leftFormat);

                        EndMetafile();
                        scaleYAxis = 1;
                        metaH = DEFAULT_METAH;
                        return new ParameterBag();
                    }
                }
            }
        }


        // TRANSMISSINGCOMMENT: Method MarkerTypeToBrush
        private Brush MarkerTypeToBrush(MarkerType mt)
        {
            Color c;
            FillStyle f;
            if (HasChartOptions && definition.ChartOptions.UseColour)
            {
                c = mt.Color;
                f = FillStyle.Solid;
            }
            else
            {
                c = grBlack;
                f = mt.FillStyle;
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

        public string PlotXYRAndReturnRtf(ITemplateHost host, double[,] x, double[, ,] y, int ng, int[] gn, int[,] nr, double[] b, double[] a, string xtxt, string ytxt, string title, string[] bnam)
        {
            using (MemoryStream metaStream = new MemoryStream())
            {
                StartMetafile(metaStream, true);
                PlotXYR(x, y, ng, gn, nr, b, a, xtxt, ytxt, title, bnam);
                EndMetafile();
                return host.ImageStreamToRtf(metaStream);
            }
        }

        private void PlotXYR(double[,] x, double[, ,] y, int ng, int[] gn, int[,] nr, double[] b, double[] a, string xtxt, string ytxt, string title, string[] bnam)
        {
            const int LEGEND_MARKER_X = 12;
            const int LEGEND_MARKER_Y_OFFSET = 15;
            const int LEGEND_TEXT_X = 24;

            int g;
            int mkr;

            // Draw the scale
            DefaultAxes();
            double xtra = 0;
            for (g = 1; g <= ng; g++)
            {
                double w = canvas.MeasureString(bnam[g], legendFont).Width + 55;
                if (w > xtra + xAxisCanvas)
                {
                    xtra = w - xAxisCanvas;
                }
            }
            DrawAxes(title, new Axis(xtxt, AxisMode.Scale, 0, ScaleType.Linear), new Axis(ytxt, AxisMode.Scale, xtra, ScaleType.Linear), false, true, false);

            double size2 = labelFont.Size * 2;

            // Draw the legends
            if (ng > 1)
            {
                for (g = 1; g <= ng; g++)
                {
                    string transTemp30 = bnam[g];
                    if (  /* TRANSINFO: .NET Equivalent of Microsoft.VisualBasic NameSpace */ transTemp30.Length > 0)
                    {
                        mkr = ChartOptions.SeriesNumberToMarkerNumber(g - 1);
                        DrawMarker(LEGEND_MARKER_X, yAxisCanvas + yExtCanvas - LEGEND_MARKER_Y_OFFSET - (size2 * g), LEGEND_MARKER_SIZE, _markerTypes[mkr]); //  TODO: Broken?
                        DrawStringLegendL(bnam[g], LEGEND_TEXT_X, yAxisCanvas + yExtCanvas - 10 - (size2 * g));
                    }
                }
            }

            // get the offsets for the Graph
            SetStandardScaling();

            // Plot the points
            for (g = 1; g <= ng; g++)
            {
                mkr = ChartOptions.SeriesNumberToMarkerNumber(g - 1);
                MarkerType t = _markerTypes[mkr];
                using (Pen p = GetPen(t, true))
                {
                    double minx = double.MaxValue;
                    double maxx = double.MinValue;
                    double miny = double.MaxValue;
                    double maxy = double.MinValue;
                    int r;
                    double x1;
                    double y1;
                    for (r = 1; r <= gn[g]; r++)
                    {
                        PointF[] xys = new PointF[nr[g, r] - 1 + 1 /* for VB to C# conversion */];
                        int k;
                        for (k = 1; k <= nr[g, r]; k++)
                        {
                            if (x[g, r] != Constant.MISSING)
                            {
                                x1 = ToCanvasX(x[g, r]);
                                if (x[g, r] > maxx)
                                {
                                    maxx = x[g, r];
                                }
                                if (x[g, r] < minx)
                                {
                                    minx = x[g, r];
                                }
                                if (y[g, r, k] != Constant.MISSING)
                                {
                                    y1 = ToCanvasY(y[g, r, k]);
                                    if (y[g, r, k] > maxy)
                                    {
                                        maxy = y[g, r, k];
                                    }
                                    if (y[g, r, k] < miny)
                                    {
                                        miny = y[g, r, k];
                                    }
                                    xys[k - 1].X = Convert.ToSingle(x1);
                                    xys[k - 1].Y = Convert.ToSingle(y1);
                                }
                                else
                                {
                                    xys[k - 1].X = -1;
                                    xys[k - 1].Y = -1;
                                }
                            }
                            else
                            {
                                xys[k - 1].X = -1;
                                xys[k - 1].Y = -1;
                            }
                        }
                        DrawMarkerSeries(xys, 6, t.Shape, t.IsFilled, p, p, false, true);
                    }
                    x1 = ToCanvasX(minx);
                    double calcy = (a[g] + b[g] * minx);
                    if (calcy < miny)
                    {
                        calcy = miny;
                        if (b[g] != 0.0)
                        {
                            x1 = ToCanvasX(((calcy - a[g]) / b[g]));
                        }
                    }
                    else if (calcy > maxy)
                    {
                        calcy = maxy;
                        if (b[g] != 0.0)
                        {
                            x1 = ToCanvasX(((calcy - a[g]) / b[g]));
                        }
                    }
                    y1 = ToCanvasY(calcy);
                    double x2 = ToCanvasX(maxx);
                    calcy = (a[g] + b[g] * maxx);
                    if (calcy < miny)
                    {
                        calcy = miny;
                        if (b[g] != 0.0)
                        {
                            x2 = ToCanvasX(((calcy - a[g]) / b[g]));
                        }
                    }
                    else if (calcy > maxy)
                    {
                        calcy = maxy;
                        if (b[g] != 0.0)
                        {
                            x2 = ToCanvasX(((calcy - a[g]) / b[g]));
                        }
                    }
                    double y2 = ToCanvasY(calcy);
                    DrawLine(p, x1, y1, x2, y2);
                }
            }
        }


        // TRANSMISSINGCOMMENT: Method PlotXYZ
        private void PlotXYZ(double[] x, double[] y, double[] z, int LowerBound, int rows, string xtxt, string ytxt, string title, bool ZPlot, int MinMaxY, MarkerShape Shape, bool IsFilled, Pen p, object labbepool)
        {
            double rmh = 0;
            bool labbe = false;
            if (labbepool != null)
            {
                rmh = Convert.ToDouble(labbepool);
                labbe = true;
                DataMaxX = 100;
                DataMaxY = 100;
                DataMinX = 0;
                DataMinY = 0;
                DrawAxes(title, new Axis(xtxt, AxisMode.Scale, 0, ScaleType.Linear), new Axis(ytxt, AxisMode.Scale, 0, ScaleType.Linear), true, true, false);
            }
            else
            {
                // Get the Min and Max for the data
                if (MinMaxY != -99)
                {
                    GetMinMaxArray(x, out dataMinX, out dataMaxX);
                    if (MinMaxY == 0)
                    {
                        GetMinMaxArray(y, out dataMinY, out dataMaxY);
                    }
                }
                DrawAxes(title, new Axis(xtxt, AxisMode.Scale, 0, ScaleType.Linear), new Axis(ytxt, AxisMode.Scale, 0, ScaleType.Linear), false, true, false);
            }

            // get the offsets for the Graph
            SetStandardScaling();

            if (ZPlot)
            {
                DrawQCanvas(offy);
            }

            // Plot the points
            double maxz = double.MinValue;
            double sumz = Convert.ToDouble(0M);
            int[] scalez = new int[rows + 1 /* for VB to C# conversion */ ];
            for (int r = LowerBound; r <= rows + LowerBound - 1; r++)
            {
                sumz = sumz + z[r];
                if (z[r] > maxz)
                {
                    maxz = z[r];
                }
            }
            double meanz = sumz / Convert.ToDouble(rows);
            double maxdev = maxz / meanz;
            if (maxdev > 5.0)
            {
                meanz = meanz * maxdev / 5.0;
            }
            for (int r = LowerBound; r <= rows + LowerBound - 1; r++)
            {
                scalez[r] = Convert.ToInt32(6.0 * z[r] / meanz);
                if (scalez[r] < 3)
                {
                    scalez[r] = 3;
                }
            }

            if (labbe)
            {
                for (int r = LowerBound; r <= rows + LowerBound - 1; r++)
                {
                    //  IEB Jul 09
                    if (x[r] != Constant.MISSING && y[r] != Constant.MISSING)
                    {
                        double x1 = ToCanvasX(100.0 * x[r]);
                        double y1 = ToCanvasY(100.0 * y[r]);
                        DrawMarker(x1, y1, scalez[r], Shape, IsFilled, p);
                    }
                }
            }
            else
            {
                for (int r = LowerBound; r <= rows + LowerBound - 1; r++)
                {
                    //  IEB Jul 09
                    if (x[r] != Constant.MISSING && y[r] != Constant.MISSING)
                    {
                        double x1 = ToCanvasX(x[r]);
                        double y1 = ToCanvasY(y[r]);
                        DrawMarker(x1, y1, scalez[r], Shape, IsFilled, p);
                    }
                }
            }

            // LAbbe plot specific null and pooled effect lines
            if (labbe)
            {
                // null effect diagonal
                using (Pen blackPen = GetPen(_markerTypes[10], true))
                {
                    DrawLine(blackPen, xAxisCanvas, yAxisCanvas, xAxisCanvas + xExtCanvas, yAxisCanvas + yExtCanvas);
                    // pooled event rate
                    using (Pen blackFXPen = GetPen(_markerTypes[10], false))
                    {
                        double x1;
                        double y1;
                        if (rmh >= 1)
                        {
                            y1 = yAxisCanvas + yExtCanvas;
                            x1 = ToCanvasX(axisYMax / rmh);
                        }
                        else
                        {
                            x1 = xAxisCanvas + xExtCanvas;
                            y1 = ToCanvasY(rmh * axisXMax);
                        }
                        DrawLine(blackFXPen, xAxisCanvas, yAxisCanvas, x1, y1);
                    }
                }
            }
        }

        public string PlotLAbbeAndReturnRtf(ITemplateHost host, int k, double[,] o, double rmh)
        {
            using (MemoryStream metaStream = new MemoryStream())
            {
                StartMetafile(metaStream);
                PlotLAbbe(k, o, rmh);
                EndMetafile();
                return host.ImageStreamToRtf(metaStream);
            }
        }

        ///  <summary>
        ///  
        ///  </summary>
        ///  <param name="k">The number of elements in o(,)</param>
        ///  <param name="o">A 1-based array of values</param>
        ///  <param name="rmh"></param>
        ///  <remarks></remarks>
        private void PlotLAbbe(int k, double[,] o, double rmh)
        {
            double[] y = new double[k + 1 /* for VB to C# conversion */ ];
            double[] x = new double[k + 1 /* for VB to C# conversion */ ];
            double[] w = new double[k + 1 /* for VB to C# conversion */ ];
            for (int i = 1; i <= k; i++)
            {
                y[i] = o[i, 1] / (o[i, 1] + o[i, 3]);
                x[i] = o[i, 2] / (o[i, 2] + o[i, 4]);
                w[i] = o[i, 1] + o[i, 2] + o[i, 3] + o[i, 4];
            }
            PlotXYZ(x, y, w, 1, k, "control percent", "experimental percent", "L'Abbe plot (symbol size represents sample size)", false, 0, _markerTypes[0].Shape, _markerTypes[0].IsFilled, GetPen(_markerTypes[0], true), rmh);
        }


        // TRANSMISSINGCOMMENT: Method GetLadderScaleParameters
        private ScaleParameters GetLadderScaleParameters()
        {
            return new ScaleParameters
                                     {
                                         X =
                                             {
                                                 AllowedScaleTypes = new[] { ScaleType.Category },
                                                 ShouldCheck = false,
                                                 Max = 0,
                                                 Min = 0
                                             },
                                         Y =
                                             {
                                                 AllowedScaleTypes = new[] { ScaleType.Linear },
                                                 ShouldCheck = true,
                                                 Max = DataMaxY,
                                                 Min = DataMinY
                                             }
                                     };
        }


        // TRANSMISSINGCOMMENT: Method PlotLadder
        private ParameterBag PlotLadder(Stream OutputStream)
        {
            // Get the plot title
            LadderOptions lOptions = ((LadderOptions)(definition.ChartOptions));
            StartMetafile(OutputStream, true);
            SetFontsAndThicknessesFromOptions(lOptions);
            AssignMarkersToSeries(definition.YSeries, lOptions);

            //  No need to calculate min/max values, as they've already been calculated as the series were added.
            //  We just need to set the neat scale.
            // Draw the scale - this includes frigging the series so that the X series is auto-drawn.
            definition = definition.Clone();
            definition.XSeries = definition.YSeries;
            DrawAxes(lOptions.Title, new Axis(null, AxisMode.Series, 0, definition.ScaleParameters.X.ScaleType), new Axis(lOptions.YAxisTitle, AxisMode.Scale, 0, definition.ScaleParameters.Y.ScaleType), lOptions.ShouldBoxAxes, true, false);

            divy = axisYMax - axisYMin;
            offy = -(axisYMin / divy * yExtCanvas) + yAxisCanvas;
            double x1 = xAxisCanvas + (xExtCanvas * 0.25);
            double x2 = xAxisCanvas + (xExtCanvas * 0.75);

            // Plot the points & join the lines
            DoubleSeries s0 = definition.YSeries[0].AsDoubleSeries;
            DoubleSeries s1 = definition.YSeries[1].AsDoubleSeries;
            //  Points
            for (int r = 0; r <= s0.Points - 1; r++)
            {
                if (s0.Data[r] != Constant.MISSING && s1.Data[r] != Constant.MISSING)
                {
                    double y1 = ToCanvasY(s0.Data[r]);
                    double Y2 = ToCanvasY(s1.Data[r]);
                    DrawMarker(x1, y1, s0.MarkerSize, s0);
                    DrawMarker(x2, Y2, s1.MarkerSize, s1);
                }
            }
            //  Lines
            MarkerType rungMarkerType = _markerTypes[10];
            if ((lOptions.MarkerTypes != null) && lOptions.MarkerTypes.Count >= 1 && lOptions.MarkerTypes[0] != null)
            {
                rungMarkerType = lOptions.MarkerTypes[0];
            }
            using (Pen rungPen = new Pen(Color.Black, rungMarkerType.Width))
            {
                rungPen.DashStyle = rungMarkerType.Style;
                for (int r = 0; r <= s0.Points - 1; r++)
                {
                    if (s0.Data[r] != Constant.MISSING && s1.Data[r] != Constant.MISSING)
                    {
                        double y1 = ToCanvasY(s0.Data[r]);
                        double Y2 = ToCanvasY(s1.Data[r]);
                        DrawLine(rungPen, x1, y1, x2, Y2);
                    }
                }
            }
            EndMetafile();
            return new ParameterBag();
        }


        // TRANSMISSINGCOMMENT: Method GetControlScaleParameters
        private ScaleParameters GetControlScaleParameters()
        {
            DoubleSeries ys0 = definition.YSeries[0].AsDoubleSeries;
            DoubleSeries xs0 = definition.XSeries[0].AsDoubleSeries;
            int rows = xs0.Points;
            double[] xdat = new double[rows + 1 /* for VB to C# conversion */ ];
            double[] ydat = new double[rows + 1 /* for VB to C# conversion */ ];

            int ctr = 0;
            double[] ySeriesData = ys0.Data;
            double[] xSeriesData = xs0.Data;
            for (int r = 0; r <= rows - 1; r++)
            {
                if (ySeriesData[r] != Constant.MISSING & xSeriesData[r] != Constant.MISSING)
                {
                    xdat[ctr] = xSeriesData[r];
                    ydat[ctr] = ySeriesData[r];
                    ctr = ctr + 1;
                }
            }
            rows = ctr;

            double ymean; double ysd;
            MathDbl.meansd(ydat, 0, ref rows, out ymean, out ysd);

            ControlOptions cOptions = ((ControlOptions)(definition.ChartOptions));
            int kobs = cOptions.ObservationsToUse;
            if (cOptions.HasUserSpecifiedMeanAndSD)
            {
                ymean = cOptions.UserSpecifiedMean;
                ysd = cOptions.UserSpecifiedSD;
            }

            // bool restricted;
            cOptions.HasUserSpecifiedLimits = cOptions.LowerWarningLimit != Constant.MISSING & cOptions.UpperWarningLimit != Constant.MISSING & cOptions.LowerControlLimit != Constant.MISSING & cOptions.UpperControlLimit != Constant.MISSING;
            if (cOptions.HasUserSpecifiedLimits)
            {
                double transTemp61 = cOptions.LowerControlLimit;
                double transTemp62 = cOptions.UpperControlLimit;
                if (cOptions.LowerControlLimit > cOptions.UpperControlLimit)
                {
                    Swap(ref transTemp61, ref transTemp62);
                }
                double transTemp59 = cOptions.LowerWarningLimit;
                double transTemp60 = cOptions.UpperWarningLimit;
                if (cOptions.LowerWarningLimit > cOptions.UpperWarningLimit)
                {
                    Swap(ref transTemp59, ref transTemp60);
                }
                double transTemp57 = cOptions.LowerControlLimit;
                double transTemp58 = cOptions.LowerWarningLimit;
                if (cOptions.LowerControlLimit > cOptions.LowerWarningLimit)
                {
                    Swap(ref transTemp57, ref transTemp58);
                }
                double transTemp55 = cOptions.UpperControlLimit;
                double transTemp56 = cOptions.UpperWarningLimit;
                if (cOptions.UpperWarningLimit > cOptions.UpperControlLimit)
                {
                    Swap(ref transTemp55, ref transTemp56);
                }
                if (DataMinY > cOptions.LowerControlLimit)
                {
                    DataMinY = cOptions.LowerControlLimit;
                }
                if (DataMaxY < cOptions.UpperControlLimit)
                {
                    DataMaxY = cOptions.UpperControlLimit;
                }
            }
            else
            {
                if (kobs != rows)
                {
                    MathDbl.meansd(ydat, 0, ref kobs, out ymean, out ysd);
                    // restricted = true; 
                }
                if (ysd != Constant.MISSING)
                {
                    if (DataMinY > ymean - ysd * 3.0)
                    {
                        DataMinY = ymean - ysd * 3.0;
                    }
                    if (DataMaxY < ymean + ysd * 3.0)
                    {
                        DataMaxY = ymean + ysd * 3.0;
                    }
                }
            }

            return new ScaleParameters
                                     {
                                         X =
                                             {
                                                 AllowedScaleTypes = new[] { ScaleType.Linear },
                                                 ShouldCheck = true,
                                                 Max = DataMaxX,
                                                 Min = DataMinX
                                             },
                                         Y =
                                             {
                                                 AllowedScaleTypes = new[] { ScaleType.Linear },
                                                 ShouldCheck = true,
                                                 Max = DataMaxY,
                                                 Min = DataMinY
                                             }
                                     };
        }


        ///  <summary>
        ///  Do a control plot.  Expects one X series and one Y series.
        ///  </summary>
        ///  <param name="OutputStream"></param>
        /// <returns>True if the plot succeeds, False otherwise.</returns>
        private ParameterBag PlotControl(Stream OutputStream)
        {
            DoubleSeries ys0 = definition.YSeries[0].AsDoubleSeries;
            DoubleSeries xs0 = definition.XSeries[0].AsDoubleSeries;
            int rows = xs0.Points;
            double[] xdat = new double[rows + 1 /* for VB to C# conversion */ ];
            double[] ydat = new double[rows + 1 /* for VB to C# conversion */ ];

            int ctr = 0;
            bool looksLikeDates = true;
            double[] ySeriesData = ys0.Data;
            double[] xSeriesData = xs0.Data;
            for (int r = 0; r <= rows - 1; r++)
            {
                if (ySeriesData[r] != Constant.MISSING & xSeriesData[r] != Constant.MISSING)
                {
                    xdat[ctr] = xSeriesData[r];
                    ydat[ctr] = ySeriesData[r];
                    if (xdat[ctr] < 20000)
                    {
                        looksLikeDates = false;
                    }
                    ctr = ctr + 1;
                }
            }
            rows = ctr;

            double ymean; double ysd;
            MathDbl.meansd(ydat, 0, ref rows, out ymean, out ysd);

            ControlOptions cOptions = ((ControlOptions)(definition.ChartOptions));
            cOptions.UseDates = looksLikeDates;
            double oldymean = ymean;
            double oldysd = ysd;
            int kobs = cOptions.ObservationsToUse;
            if (cOptions.HasUserSpecifiedMeanAndSD)
            {
                ymean = cOptions.UserSpecifiedMean;
                ysd = cOptions.UserSpecifiedSD;
            }

            bool restricted = false; bool external = false;
            cOptions.HasUserSpecifiedLimits = cOptions.LowerWarningLimit != Constant.MISSING & cOptions.UpperWarningLimit != Constant.MISSING & cOptions.LowerControlLimit != Constant.MISSING & cOptions.UpperControlLimit != Constant.MISSING;
            if (cOptions.HasUserSpecifiedLimits)
            {
                external = true;
                double transTemp53 = cOptions.LowerControlLimit;
                double transTemp54 = cOptions.UpperControlLimit;
                if (cOptions.LowerControlLimit > cOptions.UpperControlLimit)
                {
                    Swap(ref transTemp53, ref transTemp54);
                }
                double transTemp51 = cOptions.LowerWarningLimit;
                double transTemp52 = cOptions.UpperWarningLimit;
                if (cOptions.LowerWarningLimit > cOptions.UpperWarningLimit)
                {
                    Swap(ref transTemp51, ref transTemp52);
                }
                double transTemp49 = cOptions.LowerControlLimit;
                double transTemp50 = cOptions.LowerWarningLimit;
                if (cOptions.LowerControlLimit > cOptions.LowerWarningLimit)
                {
                    Swap(ref transTemp49, ref transTemp50);
                }
                double transTemp47 = cOptions.UpperControlLimit;
                double transTemp48 = cOptions.UpperWarningLimit;
                if (cOptions.UpperWarningLimit > cOptions.UpperControlLimit)
                {
                    Swap(ref transTemp47, ref transTemp48);
                }
                if (DataMinY > cOptions.LowerControlLimit)
                {
                    DataMinY = cOptions.LowerControlLimit;
                }
                if (DataMaxY < cOptions.UpperControlLimit)
                {
                    DataMaxY = cOptions.UpperControlLimit;
                }
            }
            else
            {
                if (kobs != rows)
                {
                    MathDbl.meansd(ydat, 0, ref kobs, out ymean, out ysd);
                    restricted = true;
                }
                else
                {
                    // restricted = false; 
                    external = (oldymean != ymean | oldysd != ysd);
                }
                if (ysd != Constant.MISSING)
                {
                    if (DataMinY > ymean - ysd * 3.0)
                    {
                        DataMinY = ymean - ysd * 3.0;
                    }
                    if (DataMaxY < ymean + ysd * 3.0)
                    {
                        DataMaxY = ymean + ysd * 3.0;
                    }
                }
            }

            const int RHS_LABEL_GAP = 7;

            StartMetafile(OutputStream, true);

            //  Fonts
            SetFontsAndThicknessesFromOptions(cOptions);
            //  NB we use the Legend font as the Control Label font!

            // Draw the scale
            DefaultAxes();
            AssignMarkersToSeries();

            // adjust drawing window for right hand labels and vertical date labels
            if (cOptions.UseMean | cOptions.Use1SD | cOptions.Use2SD | cOptions.Use3SD)
            {
                xExtCanvas -= RHS_LABEL_GAP + LegendWidth(Math.Round(ymean + ysd * 3.0, cOptions.RightHandDecimalPlaces) + " (+3 SD)");
            }
            if (cOptions.UseDates)
            {
                float vshift = AxisLabelWidth(new DateTime(1899, 12, 30, 0, 0, 0).AddDays(xdat[0]).ToString("d")) + 30;
                yAxisCanvas += vshift;
                yExtCanvas -= vshift;
            }

            double xtra = 0;
            double w = TitleWidth(cOptions.YAxisTitle) + 30;
            if (w > xtra + xAxisCanvas)
            {
                xtra = w - xAxisCanvas;
            }

            xAxisCanvas = xAxisCanvas + xtra;
            xExtCanvas = xExtCanvas - xtra;

            // draw the axes
            double xspace = 0;
            AxisMode xmode = AxisMode.Scale;
            if (cOptions.UseDates)
            {
                xspace = AxisLabelWidth(new DateTime(1900, 1, 1, 0, 0, 0).ToString("d"));
                xmode = AxisMode.ScaleWithoutLabels;
            }
            DrawAxes(cOptions.Title, new Axis(cOptions.XAxisTitle, xmode, xspace, definition.ScaleParameters.X.ScaleType), new Axis(cOptions.YAxisTitle, AxisMode.Scale, 0, definition.ScaleParameters.Y.ScaleType), cOptions.ShouldBoxAxes, false, false);

            // get the offsets for the Markers
            SetStandardScaling();

            double x1; double y1; double last_x1 = 0;

            // set then initial values of x2,y2 to x1,y1
            ToCanvasX(xdat[1]);
            ToCanvasY(ydat[1]);
            // plot points
            PointF[] xys = new PointF[rows - 1 + 1 /* for VB to C# conversion */ ];
            for (int r = 0; r <= rows - 1; r++)
            {
                if (xdat[r] != Constant.MISSING & ydat[r] != Constant.MISSING)
                {
                    xys[r].X = Convert.ToSingle(ToCanvasX(xdat[r]));
                    xys[r].Y = Convert.ToSingle(ToCanvasY(ydat[r]));
                }
                else
                {
                    xys[r].X = -1;
                    xys[r].Y = -1;
                }
            }
            DrawMarkerSeries(xys, 6, xs0.Shape, xs0.IsFilled, xs0.UnstyledPen, xs0.StyledPen, true, false);

            // draw vertical date markers
            if (cOptions.UseDates)
            {
                //  If labels overlap, scale down font
                bool OK = false; //  TODO: Is this ever set to true, or is this loop screwy?
                ctr = 0;
                double scaler = 1.0;
                do
                {
                    for (int r = 0; r <= rows - 1; r++)
                    {
                        if (xdat[r] != Constant.MISSING & ydat[r] != Constant.MISSING)
                        {
                            x1 = ToCanvasX(xdat[r]);
                            // y1 = YAxisCanvas - 14; 
                            string tx = new DateTime(1899, 12, 30, 0, 0, 0).AddDays(xdat[r]).ToString("d");
                            if (Math.Abs(x1 - last_x1) < AxisLabelHeight(tx))
                            {
                                OK = false;
                                scaler = scaler * 0.9;
                                break; /* TRANSWARNING: check that break is in correct scope */
                            }
                            last_x1 = x1;
                        }
                    }
                    if (OK | ctr > 15)
                    {
                        break; /* TRANSWARNING: check that break is in correct scope */
                    }
                    ctr = ctr + 1;
                }
                while (true);
                //  TODO: Scale font to scaler if needed
                for (int r = 0; r <= rows - 1; r++)
                {
                    if (xdat[r] != Constant.MISSING & ydat[r] != Constant.MISSING)
                    {
                        x1 = ToCanvasX(xdat[r]);
                        string tx = new DateTime(1899, 12, 30, 0, 0, 0).AddDays(xdat[r]).ToString("d");
                        y1 = yAxisCanvas - AxisLabelWidth(tx) - AXIS_BIG_TICK - 3;
                        DrawVerticalAxisLabel(tx, StringAlignment.Near, x1 - AxisLabelHeight(tx) / 2, y1);
                    }
                }
            }

            int rhDp = cOptions.RightHandDecimalPlaces;

            using (Pen blackPen = new Pen(grBlack))
            {
                if (cOptions.HasUserSpecifiedLimits)
                {

                    // user specified control and warning lines
                    x1 = xAxisCanvas + xExtCanvas;
                    y1 = ToCanvasY((cOptions.UpperWarningLimit));
                    DrawLine(blackPen, xAxisCanvas, y1, x1, y1);
                    string tx = Math.Round(cOptions.UpperWarningLimit, rhDp) + " (warn)";
                    DrawStringLegendL(tx, x1 + RHS_LABEL_GAP, y1 + LegendHeight(tx) / 2);
                    y1 = ToCanvasY(cOptions.LowerWarningLimit);
                    DrawLine(blackPen, xAxisCanvas, y1, x1, y1);
                    tx = Math.Round(cOptions.LowerWarningLimit, rhDp) + " (warn)";
                    DrawStringLegendL(tx, x1 + RHS_LABEL_GAP, y1 + LegendHeight(tx) / 2);
                    using (Pen redPen = new Pen(grRed))
                    {
                        y1 = ToCanvasY(cOptions.UpperControlLimit);
                        DrawLine(redPen, xAxisCanvas, y1, x1, y1);
                        tx = Math.Round(cOptions.UpperControlLimit, rhDp) + " (ctrl)";
                        DrawStringLegendL(tx, x1 + RHS_LABEL_GAP, y1 + LegendHeight(tx) / 2);
                        y1 = ToCanvasY(ymean - ysd * 3.0);
                        DrawLine(redPen, xAxisCanvas, y1, x1, y1);
                        tx = Math.Round(cOptions.LowerControlLimit, rhDp) + " (ctrl)";
                        DrawStringLegendL(tx, x1 + RHS_LABEL_GAP, y1 + LegendHeight(tx) / 2);
                        DrawStringLegendL("External:", x1 + RHS_LABEL_GAP, yAxisCanvas + yExtCanvas);
                    }
                }
                else
                {
                    // draw control lines
                    if (cOptions.UseMean)
                    {
                        x1 = xAxisCanvas + xExtCanvas;
                        y1 = ToCanvasY(ymean);
                        DrawLine(blackPen, xAxisCanvas, y1, x1, y1);
                        string tx = Math.Round(ymean, rhDp) + " (mean)";
                        DrawStringLegendL(tx, x1 + RHS_LABEL_GAP, y1 + LegendHeight(tx) / 2);
                        if (restricted)
                        {
                            DrawStringLegendL("On first " + kobs.ToString() + " points:", x1 + RHS_LABEL_GAP, yAxisCanvas + yExtCanvas);
                        }
                        else if (external)
                        {
                            DrawStringLegendL("External:", x1 + RHS_LABEL_GAP, yAxisCanvas + yExtCanvas);
                        }
                    }

                    if (ysd != Constant.MISSING)
                    {
                        string tx;
                        if (cOptions.Use1SD)
                        {
                            using (Pen greenPen = new Pen(grGreen))
                            {
                                x1 = xAxisCanvas + xExtCanvas;
                                y1 = ToCanvasY(ymean + ysd);
                                DrawLine(greenPen, xAxisCanvas, y1, x1, y1);
                                tx = Math.Round(ymean + ysd, rhDp) + " (+1 SD)";
                                DrawStringLegendL(tx, x1 + RHS_LABEL_GAP, y1 + LegendHeight(tx) / 2);
                                y1 = ToCanvasY(ymean - ysd);
                                DrawLine(greenPen, xAxisCanvas, y1, x1, y1);
                                tx = Math.Round(ymean - ysd, rhDp) + " (-1 SD)";
                                DrawStringLegendL(tx, x1 + RHS_LABEL_GAP, y1 + LegendHeight(tx) / 2);
                            }
                        }

                        if (cOptions.Use2SD)
                        {
                            x1 = xAxisCanvas + xExtCanvas;
                            y1 = ToCanvasY(ymean + ysd * 2.0);
                            DrawLine(blackPen, xAxisCanvas, y1, x1, y1);
                            tx = Math.Round(ymean + ysd * 2.0, rhDp) + " (+2 SD)";
                            DrawStringLegendL(tx, x1 + RHS_LABEL_GAP, y1 + LegendHeight(tx) / 2);
                            y1 = ToCanvasY(ymean - ysd * 2.0);
                            DrawLine(blackPen, xAxisCanvas, y1, x1, y1);
                            tx = Math.Round(ymean - ysd * 2.0, rhDp) + " (-2 SD)";
                            DrawStringLegendL(tx, x1 + RHS_LABEL_GAP, y1 + LegendHeight(tx) / 2);
                        }

                        if (cOptions.Use3SD)
                        {
                            using (Pen redPen = new Pen(grRed))
                            {
                                x1 = xAxisCanvas + xExtCanvas;
                                y1 = ToCanvasY(ymean + ysd * 3.0);
                                DrawLine(redPen, xAxisCanvas, y1, x1, y1);
                                tx = Math.Round(ymean + ysd * 3.0, rhDp) + " (+3 SD)";
                                DrawStringLegendL(tx, x1 + RHS_LABEL_GAP, y1 + LegendHeight(tx) / 2);
                                y1 = ToCanvasY(ymean - ysd * 3.0);
                                DrawLine(redPen, xAxisCanvas, y1, x1, y1);
                                tx = Math.Round(ymean - ysd * 3.0, rhDp) + " (-3 SD)";
                                DrawStringLegendL(tx, x1 + RHS_LABEL_GAP, y1 + LegendHeight(tx) / 2);
                            }
                        }
                    }
                }
            }

            EndMetafile();
            return new ParameterBag();
        }


        // TRANSMISSINGCOMMENT: Method GetErrorBarScaleParameters
        private ScaleParameters GetErrorBarScaleParameters()
        {
            ErrorBarOptions eOptions = ((ErrorBarOptions)(definition.ChartOptions));
            int cols = eOptions.ydat.VariableCount;

            // Setup the Min & Max Values
            DataMinX = double.MaxValue;
            DataMaxX = double.MinValue;
            DataMinY = double.MaxValue;
            DataMaxY = double.MinValue;

            for (int C = 0; C <= cols - 1; C++)
            {
                DoubleVariable yvar = eOptions.ydat.Variables[C].AsDoubleVariable;
                DoubleVariable xvar = eOptions.xdat.Variables[C].AsDoubleVariable;
                DoubleVariable yvarl = eOptions.ydatl.Variables[C].AsDoubleVariable;
                DoubleVariable yvaru = eOptions.ydatu.Variables[C].AsDoubleVariable;

                // Get the overall Min & Max Values for Y
                if (DataMinY > yvar.Min)
                {
                    DataMinY = yvar.Min;
                }
                if (DataMaxY < yvar.Max)
                {
                    DataMaxY = yvar.Max;
                }

                // Get the overall Min & Max Values for X
                if (DataMinX > xvar.Min)
                {
                    DataMinX = xvar.Min;
                }
                if (DataMaxX < xvar.Max)
                {
                    DataMaxX = xvar.Max;
                }

                // Get error bar data (upper bar)
                // Check the overall Min & Max Values
                if (yvaru.Min < DataMinY)
                {
                    DataMinY = yvaru.Min;
                }
                if (yvaru.Max > DataMaxY)
                {
                    DataMaxY = yvaru.Max;
                }

                // Get error bar data (lower bar)
                // Check the overall Min & Max Values
                if (yvarl.Min < DataMinY)
                {
                    DataMinY = yvarl.Min;
                }
                if (yvarl.Max > DataMaxY)
                {
                    DataMaxY = yvarl.Max;
                }
            }

            ScaleParameters sp = new ScaleParameters
                                     {
                                         X =
                                             {
                                                 AllowedScaleTypes = new[] { ScaleType.Linear },
                                                 ShouldCheck = true,
                                                 Max = DataMaxX,
                                                 Min = DataMinX
                                             },
                                         Y =
                                             {
                                                 AllowedScaleTypes = new[] { ScaleType.Linear },
                                                 ShouldCheck = true,
                                                 Max = DataMaxY,
                                                 Min = DataMinY
                                             }
                                     };




            return sp;
        }


        // TRANSMISSINGCOMMENT: Method PlotErrorBar
        private ParameterBag PlotErrorBar(Stream OutputStream)
        {
            ErrorBarOptions eOptions = ((ErrorBarOptions)(definition.ChartOptions));
            int cols = eOptions.ydat.VariableCount;

            // Setup the Min & Max Values
            DataMinX = double.MaxValue;
            DataMaxX = double.MinValue;
            DataMinY = double.MaxValue;
            DataMaxY = double.MinValue;

            for (int C = 0; C <= cols - 1; C++)
            {
                DoubleVariable yvar = eOptions.ydat.Variables[C].AsDoubleVariable;
                DoubleVariable xvar = eOptions.xdat.Variables[C].AsDoubleVariable;
                DoubleVariable yvarl = eOptions.ydatl.Variables[C].AsDoubleVariable;
                DoubleVariable yvaru = eOptions.ydatu.Variables[C].AsDoubleVariable;

                // Get the overall Min & Max Values for Y
                if (DataMinY > yvar.Min)
                {
                    DataMinY = yvar.Min;
                }
                if (DataMaxY < yvar.Max)
                {
                    DataMaxY = yvar.Max;
                }

                // Get the overall Min & Max Values for X
                if (DataMinX > xvar.Min)
                {
                    DataMinX = xvar.Min;
                }
                if (DataMaxX < xvar.Max)
                {
                    DataMaxX = xvar.Max;
                }

                // Get error bar data (upper bar)
                // Check the overall Min & Max Values
                if (yvaru.Min < DataMinY)
                {
                    DataMinY = yvaru.Min;
                }
                if (yvaru.Max > DataMaxY)
                {
                    DataMaxY = yvaru.Max;
                }

                // Get error bar data (lower bar)
                // Check the overall Min & Max Values
                if (yvarl.Min < DataMinY)
                {
                    DataMinY = yvarl.Min;
                }
                if (yvarl.Max > DataMaxY)
                {
                    DataMaxY = yvarl.Max;
                }
            }

            //  If there's a legend, work out how many series there are and extend the plot area as required to hold the legend

            //  Measurements and set axes.  These are done on a scratchpad canvas before the proper measurements are set up.
            double legendFontHeight;
            using (MemoryStream scratchStream = new MemoryStream())
            {
                StartMetafile(scratchStream, true);
                SetFontsAndThicknessesFromOptions(eOptions);
                DefaultAxes();
                legendFontHeight = legendFont.GetHeight(canvas);
                EndMetafile();
            }

            //  By now, all measurements are known.  Set up the plot areas.
            double legendTop = yAxisCanvas - LEGEND_TOP_GAP;
            double legendRowHeight = Math.Max(LEGEND_MARKER_SIZE, Convert.ToInt32(legendFontHeight));
            double legendSpacing = MINIMUM_LEGEND_GAP + legendRowHeight;
            if (eOptions.ShowLegend && eOptions.ShowLegendIsRelevant)
            {
                //  Dim markerMidlineOffset As Double = (legendFontHeight - LEGEND_MARKER_SIZE) / 2
                double legendBottom = legendTop - (cols * legendSpacing);
                if (legendBottom < LOWEST_ALLOWED_LEGEND)
                {
                    double extraSpaceRequired = LOWEST_ALLOWED_LEGEND - legendBottom;

                    //  Add in the extra space
                    metaH += extraSpaceRequired;
                    yAxisCanvas += extraSpaceRequired;
                    legendTop += extraSpaceRequired;
                    // legendBottom += extraSpaceRequired; 
                }
            }

            StartMetafile(OutputStream, false);
            SetFontsAndThicknessesFromOptions(eOptions);
            AssignMarkersToSeries(eOptions);

            // Draw the scale
            DrawAxes(eOptions.Title, new Axis(eOptions.XAxisTitle, AxisMode.Scale, 0, definition.ScaleParameters.X.ScaleType), new Axis(eOptions.YAxisTitle, AxisMode.Scale, 0, definition.ScaleParameters.Y.ScaleType), boxAxes, false, false);

            // get the offsets for the Markers
            SetStandardScaling();

            // Work through the columns
            for (int C = 0; C <= cols - 1; C++)
            {
                DoubleVariable yvar = eOptions.ydat.Variables[C].AsDoubleVariable;
                DoubleVariable xvar = eOptions.xdat.Variables[C].AsDoubleVariable;
                DoubleVariable yvarl = eOptions.ydatl.Variables[C].AsDoubleVariable;
                DoubleVariable yvaru = eOptions.ydatu.Variables[C].AsDoubleVariable;

                double x1;
                double y1;
                double x2 = 0;
                double y2 = 0;

                //  Draw the error bars first so we don't interfere with connection lines
                using (Pen p = GetPen(eOptions.MarkerTypes[C], true))
                {
                    for (int r = 0; r <= xvar.Data.Length - 1; r++)
                    {
                        x1 = ToCanvasX(xvar.Data[r]);
                        y1 = ToCanvasY(yvaru.Data[r]);
                        y2 = ToCanvasY(yvarl.Data[r]);
                        // Draw the endlines
                        DrawLine(p, x1 - 10, y1, x1 + 10, y1);
                        DrawLine(p, x1 - 10, y2, x1 + 10, y2);
                        // Draw the bar
                        DrawLine(p, x1, y1, x1, y2);
                    }

                    if (eOptions.JoinMarkersWithLines)
                    {
                        // set the initial values of x2,y2 to x1,y1
                        x2 = ToCanvasX(xvar.Data[0]);
                        y2 = ToCanvasY(yvar.Data[0]);
                    }

                    //  Work through the rows plotting the markers
                    if (eOptions.PlotMarkers)
                    {
                        for (int r = 0; r <= yvar.Length - 1; r++)
                        {
                            x1 = ToCanvasX(xvar.Data[r]);
                            y1 = ToCanvasY(yvar.Data[r]);
                            DrawMarker(x1, y1, eOptions.MarkerTypes[C].MarkerSize, eOptions.MarkerTypes[C]);
                        }
                    }

                    if (eOptions.JoinMarkersWithLines)
                    {
                        using (Pen pStyled = GetPen(eOptions.MarkerTypes[C], false))
                        {
                            for (int r = 0; r <= yvar.Length - 1; r++)
                            {
                                x1 = ToCanvasX(xvar.Data[r]);
                                y1 = ToCanvasY(yvar.Data[r]);
                                DrawLine(pStyled, x1, y1, x2, y2);
                                x2 = x1;
                                y2 = y1;
                            }
                        }
                    }

                    //  Legend
                    if (eOptions.ShowLegend && eOptions.ShowLegendIsRelevant)
                    {
                        double y = legendTop - (C * legendSpacing);
                        DrawMarker(xAxisCanvas + LEGEND_MARKER_SIZE / 2.0, y - legendFontHeight / 2.0, LEGEND_MARKER_SIZE, eOptions.MarkerTypes[C]);
                        DrawStringLegendL(eOptions.SeriesTitles[C], xAxisCanvas + LEGEND_MARKER_SIZE * 2, y);
                    }
                }
            }
            EndMetafile();
            return new ParameterBag();
        }


        // TRANSMISSINGCOMMENT: Method GetForestScaleParameters
        private ScaleParameters GetForestScaleParameters()
        {
            ForestOptions fOptions = ((ForestOptions)(definition.ChartOptions));
            double[] gn = fOptions.gn;
            int k = fOptions.k;
            double[] odr = fOptions.odr;
            double[] odrl = fOptions.odrl;
            double[] odru = fOptions.odru;
            double[] pg = fOptions.pg;

            int kok = 0;
            double ormax = double.MinValue;
            double ormin = double.MaxValue;
            double orumax = double.MinValue;
            double orlmin = double.MaxValue;
            double max_gn = double.MinValue;

            for (int i = 0; i <= k - 1; i++)
            {
                if (pg == null || pg[i] == 0)
                {
                    if (gn[i] != Constant.MISSING & gn[i] > max_gn)
                    {
                        max_gn = gn[i];
                    }
                }
                if (odr[i] != Constant.MISSING)
                {
                    kok = kok + 1;
                    if (odr[i] > ormax)
                    {
                        ormax = odr[i];
                    }
                    if (odr[i] < ormin)
                    {
                        ormin = odr[i];
                    }
                    if (odrl[i] > odru[i])
                    {
                        double tmp = odrl[i];
                        odrl[i] = odru[i];
                        odru[i] = tmp;
                    }
                    if (odrl[i] < orlmin)
                    {
                        orlmin = odrl[i];
                    }
                    if (odru[i] > orumax)
                    {
                        orumax = odru[i];
                    }
                }
            }

            double absmin = double.MaxValue;
            for (int i = 0; i <= k - 1; i++)
            {
                if (Math.Abs(odr[i]) < absmin && odr[i] != 0.0)
                {
                    absmin = Math.Abs(odr[i]);
                }
                if (Math.Abs(odrl[i]) < absmin && odrl[i] != 0.0)
                {
                    absmin = Math.Abs(odrl[i]);
                }
                if (Math.Abs(odru[i]) < absmin && odru[i] != 0.0)
                {
                    absmin = Math.Abs(odru[i]);
                }
            }

            DataMaxX = ormax;
            if (DataMaxX < orumax && orumax != Constant.MISSING)
            {
                dataMaxX = orumax;
            }
            DataMinX = ormin;
            if (DataMinX > orlmin && orlmin != Constant.MISSING)
            {
                dataMinX = orlmin;
            }

            ScaleParameters sp = new ScaleParameters
                                     {
                                         X = { AllowedScaleTypes = new[] { ScaleType.Linear, ScaleType.Log10 } },
                                         Y = { AllowedScaleTypes = new[] { ScaleType.Category } }
                                     };
            sp.X.ShouldCheck = true;
            sp.Y.ShouldCheck = false;
            sp.X.Max = DataMaxX;
            sp.X.Min = DataMinX;
            return sp;
        }


        // TRANSMISSINGCOMMENT: Method PlotForest
        private ParameterBag PlotForest(Stream outputStream)
        {
            double aint = 0; double amin = 0;
            double tmp; double realamin = 0; double[] tic = null; double realamax = 0;
            int tics = 0; int pbias = 0; double xm;
            double yt = 0;

            ForestOptions fOptions = ((ForestOptions)(definition.ChartOptions));
            MarkerType studyMarkerType = fOptions.MarkerTypes[0];
            MarkerType pooledMarkerType = fOptions.MarkerTypes[1];
            double[] gn = fOptions.gn;
            int k = fOptions.k;
            double[] odr = fOptions.odr;
            double[] odrl = fOptions.odrl;
            double[] odru = fOptions.odru;
            double[] pg = fOptions.pg;
            string[] title = fOptions.titles;
            ScaleType xlogscale = definition.ScaleParameters.X.ScaleType;

            if (k > 10)
            {
                scaleYAxis = 1 + (k - 10) / 20;
                if (scaleYAxis > 5)
                {
                    scaleYAxis = 5;
                }
                metaH = scaleYAxis * DEFAULT_METAH;
            }
            else
            {
                scaleYAxis = 1;
                metaH = DEFAULT_METAH;
            }

            bool isLogScale = (xlogscale == ScaleType.Log10);

            StartMetafile(outputStream, true);

            SetFontsAndThicknessesFromOptions(fOptions);

            int kok = 0;
            double ormax = double.MinValue;
            double ormin = double.MaxValue;
            double orumax = double.MinValue;
            double orlmin = double.MaxValue;
            double max_gn = double.MinValue;

            if (isLogScale)
            {
                for (int i = 0; i <= k - 1; i++)
                {
                    if (pg == null || pg[i] == 0)
                    {
                        if (gn[i] != Constant.MISSING & gn[i] > max_gn)
                        {
                            max_gn = gn[i];
                        }
                    }
                    if (odr[i] != Constant.MISSING)
                    {
                        kok = kok + 1;
                        if (odr[i] > ormax)
                        {
                            ormax = odr[i];
                        }
                        if (odr[i] < ormin & odr[i] > 0)
                        {
                            ormin = odr[i];
                        }
                        if (odrl[i] > odru[i])
                        {
                            tmp = odrl[i];
                            odrl[i] = odru[i];
                            odru[i] = tmp;
                        }
                        if (odrl[i] < orlmin & odrl[i] > 0)
                        {
                            orlmin = odrl[i];
                        }
                        if (odru[i] > orumax)
                        {
                            orumax = odru[i];
                        }
                    }
                }
            }
            else
            {
                for (int i = 0; i <= k - 1; i++)
                {
                    if (pg == null || pg[i] == 0)
                    {
                        if (gn[i] != Constant.MISSING & gn[i] > max_gn)
                        {
                            max_gn = gn[i];
                        }
                    }
                    if (odr[i] != Constant.MISSING)
                    {
                        kok = kok + 1;
                        if (odr[i] > ormax)
                        {
                            ormax = odr[i];
                        }
                        if (odr[i] < ormin)
                        {
                            ormin = odr[i];
                        }
                        if (odrl[i] > odru[i])
                        {
                            tmp = odrl[i];
                            odrl[i] = odru[i];
                            odru[i] = tmp;
                        }
                        if (odrl[i] < orlmin)
                        {
                            orlmin = odrl[i];
                        }
                        if (odru[i] > orumax)
                        {
                            orumax = odru[i];
                        }
                    }
                }
            }

            double absmin = double.MaxValue;
            for (int i = 0; i <= k - 1; i++)
            {
                if (Math.Abs(odr[i]) < absmin && odr[i] != 0.0)
                {
                    absmin = Math.Abs(odr[i]);
                }
                if (Math.Abs(odrl[i]) < absmin && odrl[i] != 0.0)
                {
                    absmin = Math.Abs(odrl[i]);
                }
                if (Math.Abs(odru[i]) < absmin && odru[i] != 0.0)
                {
                    absmin = Math.Abs(odru[i]);
                }
            }

            int decimalPlaces = fOptions.EffectSizeAndIntervalDecimalPlaces;

            DataMaxX = ormax;
            if (DataMaxX < orumax && orumax != Constant.MISSING)
            {
                dataMaxX = orumax;
            }
            DataMinX = ormin;
            if (DataMinX > orlmin && orlmin != Constant.MISSING)
            {
                dataMinX = orlmin;
            }

            DefaultAxes();
            double rgap = 0;
            double xtra = 0;
            //  Allow room for right hand labels of effect and CI
            double w;
            for (int i = 0; i <= k - 1; i++)
            {
                if (odr[i] != Constant.MISSING)
                {
                    w = TitleWidth(title[i]) + 30;
                    if (w > xtra + xAxisCanvas)
                    {
                        xtra = w - xAxisCanvas - 5;
                    }
                    w = LegendWidth(Formatting.RoundMeta(odr[i], absmin, decimalPlaces) + " (" + Formatting.RoundMeta(odrl[i], absmin, decimalPlaces) + ", " + Formatting.RoundMeta(odru[i], absmin, decimalPlaces) + ")");
                    if (w > rgap)
                    {
                        rgap = w;
                    }
                }
            }
            w = TitleWidth(combo_ti(fOptions.Title)) + 30;
            if (w > xtra + xAxisCanvas)
            {
                xtra = w - xAxisCanvas - 5;
            }
            xExtCanvas = 940 - rgap;

            if (isLogScale)
            {
                //  TODO: Use a proper log scale
                AxisScaler.Q_Axis(ref dataMinX, ref dataMaxX, out xDiv, ref amin, ref aint, out minorTicsPerMajorTic, ScaleType.Linear);
                tics = 15;
                tic = new double[tics + 1 /* for VB to C# conversion */ ];
                tic[1] = 0.00000001;
                tic[2] = 0.00001;
                tic[3] = 0.001;
                tic[4] = 0.01;
                tic[5] = 0.1;
                tic[6] = 0.2;
                tic[7] = 0.5;
                tic[8] = 1;
                tic[9] = 2;
                tic[10] = 5;
                tic[11] = 10;
                tic[12] = 100;
                tic[13] = 1000;
                tic[14] = 100000;
                tic[15] = 100000000;
                realamin = DataMinX;
                for (int i = 2; i <= tics; i++)
                {
                    if (tic[i] > DataMinX)
                    {
                        realamin = tic[i - 1];
                        break;
                    }
                }
                realamax = DataMaxX;
                for (int i = tics - 1; i >= 1; i--)
                {
                    if (tic[i] < DataMaxX)
                    {
                        realamax = tic[i + 1];
                        break;
                    }
                }
                DataMinX = Math.Log(realamin);
                DataMaxX = Math.Log(realamax);
                DrawAxes(fOptions.Title, new Axis(null, AxisMode.LineOnly, 0, ScaleType.Linear), new Axis(null, AxisMode.None, xtra, ScaleType.Linear), false, false, false);
            }
            else
            {
                DrawAxes(fOptions.Title, new Axis(null, AxisMode.Scale, 0, ScaleType.NotSet), new Axis(null, AxisMode.None, xtra, ScaleType.NotSet), false, false, false);
                DataMinX = axisXMin;
                DataMaxX = axisXMax;
            }

            divx = DataMaxX - DataMinX;
            offx = -(DataMinX / divx * xExtCanvas) + xAxisCanvas;
            divy = kok + pbias;
            offy = yAxisCanvas;

            if (isLogScale)
            {
                for (int i = 1; i <= tics; i++)
                {
                    //  The use of tic is safe, as this code is only run if logscale, which is where tic is set above.
                    if (tic[i] >= realamin & tic[i] <= realamax)
                    {
                        xm = ToCanvasX(Math.Log(tic[i]));
                        string Lab;
                        if (tic[i] > 1000 | tic[i] < 0.001)
                        {
                            Lab = tic[i].ToString("E");
                        }
                        else
                        {
                            Lab = tic[i].ToString();
                        }
                        DrawStringLabel(Lab, xm, yAxisCanvas - 12, StringAlignment.Center);
                        DrawLine(axisPen, xm, yAxisCanvas - 12, xm, yAxisCanvas);
                    }
                }
            }

            double rmh = -99;
            int r = 0;
            double botlim = isLogScale ? realamin : double.MinValue;

            using (Pen tenPen = GetPen(_markerTypes[10], true))
            {
                using (Pen effectTenPen = GetPen(_markerTypes[10], false))
                {
                    using (Pen ciTenPen = new Pen(_markerTypes[10].Color, fOptions.StudyCiLineThickness))
                    {

                        for (int i = k - 1; i >= 0; i--)
                        {
                            if (odr[i] != Constant.MISSING)
                            {
                                r = r + 1;
                                double yctr = (r + pbias - 0.5) / divy * yExtCanvas;
                                double ytop = (r + pbias) / divy * yExtCanvas;
                                xm = odr[i] < botlim ? xAxisCanvas : ToCanvasX(isLogScale ? Math.Log(odr[i]) : odr[i]);
                                double XL = odrl[i] < botlim ? xAxisCanvas : ToCanvasX(isLogScale ? Math.Log(odrl[i]) : odrl[i]);
                                double XR = ToCanvasX(isLogScale ? Math.Log(odru[i]) : odru[i]);
                                double Y2 = (ytop - yctr) / 1.5;
                                double yc = offy + yctr;
                                yt = offy + yctr + Y2;
                                double yb = offy + yctr - Y2;
                                //if ( gn[ i ] == Constant.MISSING ) 
                                //{ 
                                //    // ytop = yctr + ( ytop - yctr ) * 0.1 + ( ytop - yctr ) * 0.7; 
                                //} 
                                //else 
                                //{ 
                                //    // ytop = yctr + ( ytop - yctr ) * 0.1 + ( ytop - yctr ) * 0.9 * ( gn[ i ] / max_gn ); 
                                //} 
                                //// ytop = yctr + ( ytop - yctr ) * 0.8; 
                                if (pg == null || pg[i] == 0)
                                {
                                    // CI line
                                    DrawLine(ciTenPen, XL, yc, XR, yc);
                                    // Weight blob
                                    double blobSize = (5 + Math.Abs(yt - yb) * (gn[i] / max_gn)) * 0.7;
                                    DrawMarker(xm, yc, blobSize / 2, studyMarkerType);
                                    // Arrow ends if not plottable
                                    if ((odrl[i] <= 0 & isLogScale) | odrl[i] == Constant.MISSING)
                                    {
                                        DrawLine(tenPen, XL + Y2, yc + Y2, XL, yc);
                                        DrawLine(tenPen, XL, yc, XL + Y2, yc - Y2);
                                    }
                                    if (odru[i] == Constant.MISSING)
                                    {
                                        DrawLine(tenPen, XR - Y2, yc + Y2, XR, yc);
                                        DrawLine(tenPen, XR, yc, XR - Y2, yb - Y2);
                                    }

                                }
                                else
                                {
                                    DrawMarker(xm, yc, Y2, pooledMarkerType);
                                    DrawLine(ciTenPen, XR, yc, XL, yc);
                                    if (pg[i] < 0)
                                    {
                                        rmh = odr[i];
                                        // pooled effect marker
                                        DrawLine(effectTenPen, xm, yt, xm, ToCanvasY(k + pbias - 0.5));
                                    }

                                }
                                AxisDrawStringAtAngleRM(title[i], xAxisCanvas - 15, yc, definition.ScaleParameters.Y.LabelDirection);
                                DrawStringLabel(Formatting.RoundMeta(odr[i], absmin, decimalPlaces) + " (" + Formatting.RoundMeta(odrl[i], absmin, decimalPlaces) + ", " + Formatting.RoundMeta(odru[i], absmin, decimalPlaces) + ")", xAxisCanvas + xExtCanvas + 10, yc, StringAlignment.Near, StringAlignment.Center);
                            }
                        }

                        if (DataMinX <= 0)
                        {
                            // no effect marker
                            xm = ToCanvasX(isLogScale ? Math.Log(1) : 0);
                            DrawLine(tenPen, xm, yt, xm, yAxisCanvas);
                        }
                    }
                }
            }

            if (rmh != -99)
            {
                DrawXAxisTitle(fOptions.XAxisTitle);
            }

            EndMetafile();
            return new ParameterBag();
        }


        // TRANSMISSINGCOMMENT: Method GetSurvivalScaleParameters
        private ScaleParameters GetSurvivalScaleParameters()
        {
            SurvivalOptions sOptions = ((SurvivalOptions)(definition.ChartOptions));

            // Setup the Min & Max Values
            DataMinX = double.MaxValue;
            DataMaxX = double.MinValue;
            foreach (SurvivalOptions.SurvivalSeries ss in sOptions.Series)
            {
                foreach (double d in ss.XDat)
                {
                    if (d != Constant.MISSING)
                    {
                        if (d < dataMinX)
                        {
                            dataMinX = d;
                        }
                        if (d > dataMaxX)
                        {
                            dataMaxX = d;
                        }
                    }
                }
            }

            ScaleParameters sp = new ScaleParameters
                                     {
                                         X =
                                             {
                                                 AllowedScaleTypes = new[] { ScaleType.Linear },
                                                 ShouldCheck = true,
                                                 Min = DataMinX,
                                                 Max = DataMaxX
                                             },
                                         Y =
                                             {
                                                 AllowedScaleTypes = new[] { ScaleType.Linear },
                                                 ShouldCheck = false,
                                                 Max = 1,
                                                 Min = 0
                                             }
                                     };
            return sp;
        }


        // TRANSMISSINGCOMMENT: Method PlotSurvival
        private ParameterBag PlotSurvival(Stream OutputStream)
        {
            SurvivalOptions sOptions = ((SurvivalOptions)(definition.ChartOptions));

            int cols = sOptions.Series.Count;

            // Setup the Min & Max Values
            DataMinX = double.MaxValue;
            DataMaxX = double.MinValue;
            bool doCi = true;
            foreach (SurvivalOptions.SurvivalSeries ss in sOptions.Series)
            {
                //  If any series doesn't have both confidence intervals, we don't plot them at all.
                if (ss.YDatL == null || ss.YDatU == null)
                {
                    doCi = false;
                }
                foreach (double d in ss.XDat)
                {
                    if (d != Constant.MISSING)
                    {
                        if (d < dataMinX)
                        {
                            dataMinX = d;
                        }
                        if (d > dataMaxX)
                        {
                            dataMaxX = d;
                        }
                    }
                }
            }
            DataMinY = 0;
            DataMaxY = 1;

            bool use_marker = sOptions.ShowEventMarkers;
            bool use_tic = sOptions.ShowCensorshipTics;

            //  If there is a legend, work out how many series there are and extend the plot area as required to hold the legend

            //  Measurements and set axes.  These are done on a scratchpad canvas before the proper measurements are set up.
            double legendFontHeight;
            using (MemoryStream scratchStream = new MemoryStream())
            {
                StartMetafile(scratchStream, true);
                SetFontsAndThicknessesFromOptions(sOptions);
                DefaultAxes();
                double smallerExt = Math.Min(xExtCanvas, yExtCanvas);
                xExtCanvas = smallerExt;
                yExtCanvas = smallerExt;
                legendFontHeight = legendFont.GetHeight(canvas);
                EndMetafile();
            }

            //  By now, all measurements are known.  Set up the plot areas.
            double legendTop = yAxisCanvas - LEGEND_TOP_GAP;
            double markerMidlineOffset = (legendFontHeight - LEGEND_MARKER_SIZE) / 2;
            double legendSpacing = MINIMUM_LEGEND_GAP + Math.Max(LEGEND_MARKER_SIZE, Convert.ToInt32(legendFontHeight));
            double legendBottom = legendTop - (definition.XSeries.Count * legendSpacing);
            if (sOptions.ShowLegend && legendBottom < LOWEST_ALLOWED_LEGEND)
            {
                double extraSpaceRequired = LOWEST_ALLOWED_LEGEND - legendBottom;

                //  Add in the extra space
                metaH += extraSpaceRequired;
                yAxisCanvas += extraSpaceRequired;
                legendTop += extraSpaceRequired;
                // legendBottom += extraSpaceRequired; 
            }

            StartMetafile(OutputStream, false);
            SetFontsAndThicknessesFromOptions(sOptions);
            AssignMarkersToSeries(sOptions);

            // Start at 1
            DataMaxY = 1;
            DataMinY = 0;

            DrawAxes(sOptions.Title, new Axis("Times", AxisMode.Scale, 0, definition.ScaleParameters.X.ScaleType), new Axis(sOptions.YAxisTitle, AxisMode.Scale, 0, definition.ScaleParameters.Y.ScaleType), false, true, false);

            // Draw the lines & markers
            divx = axisXMax - axisXMin;
            offx = -(axisXMin / divx * xExtCanvas) + xAxisCanvas;
            divy = cols + 1;
            offy = yAxisCanvas;

            // Work through the columns
            //  Dim tenPen As Pen = _MarkerTypes(10).GetPen(False)
            //  The CI marker type is always the last one in the list
            MarkerType ciMarkerType = sOptions.MarkerTypes[sOptions.MarkerTypes.Count - 1];
            for (int C = 0; C <= sOptions.Series.Count - 1; C++)
            {
                double[] ydat = sOptions.Series[C].YDat;
                double[] xdat = sOptions.Series[C].XDat;
                int[] cdat = sOptions.Series[C].CDat;
                double[] ydat_l = sOptions.Series[C].YDatL;
                double[] ydat_u = sOptions.Series[C].YDatU;

                MarkerType mType = sOptions.MarkerTypes[C];
                using (Pen p = GetPen(mType, true))
                {
                    //  If necessary, draw the marker legend
                    if (sOptions.ShowLegend)
                    {
                        if (sOptions.SeriesTitles[C].Length > 0)
                        {
                            double markerX = xAxisCanvas + LEGEND_MARKER_SIZE / 2.0;
                            double markerY = legendTop - (C * legendSpacing) - markerMidlineOffset;
                            if (use_marker)
                            {
                                DrawMarker(markerX, markerY, LEGEND_MARKER_SIZE, mType);
                                //  DrawMarker(12, YAxis + YExt - 32 - (size2 * C), 6, mType)
                            }
                            else
                            {
                                const double cornerOffset = LEGEND_MARKER_SIZE / 2.0;
                                DrawLine(p, markerX - cornerOffset, markerY - cornerOffset, markerX + cornerOffset, markerY - cornerOffset);
                                DrawLine(p, markerX + cornerOffset, markerY - cornerOffset, markerX + cornerOffset, markerY + cornerOffset);
                            }
                            DrawStringLegendL(sOptions.SeriesTitles[C], xAxisCanvas + 9 + LEGEND_MARKER_SIZE, legendTop - (C * legendSpacing));
                            //  DrawStringLegendL(sOptions.SeriesTitles(C), 24, YAxis + YExt - 10 - (size2 * (C + 1)))
                        }
                    }

                    double x1 = ToCanvasX(axisXMin);
                    double y1 = ToCanvasY(1.0);
                    double x2 = 0;
                    double y2 = 0;
                    for (int r = ydat.GetLowerBound(0); r <= ydat.GetUpperBound(0); r++)
                    {
                        if (ydat[r] != Constant.MISSING & xdat[r] != Constant.MISSING & cdat[r] != -1)
                        {
                            x2 = ToCanvasX(xdat[r]);
                            y2 = ToCanvasY(ydat[r]);
                            if (use_marker & cdat[r] > 0)
                            {
                                DrawMarker(x2, y2, mType.MarkerSize, mType);
                            }
                            // Draw tic if censored
                            if (cdat[r] == 0 & use_tic)
                            {
                                DrawLine(p, x2, y2, x2, y2 + 7);
                            }
                            // Then the lines
                            DrawLine(p, x1, y1, x2, y1);
                            DrawLine(p, x2, y1, x2, y2);
                        }
                        x1 = x2;
                        y1 = y2;
                    }

                    //  overlay confidence intervals
                    if (doCi)
                    {
                        // x1 = ToCanvasX( AxisXMin ); 
                        for (int r = ydat.GetLowerBound(0); r <= ydat.GetUpperBound(0); r++)
                        {
                            if (ydat[r] != Constant.MISSING & ydat_l[r] != Constant.MISSING & ydat_u[r] != Constant.MISSING & xdat[r] != Constant.MISSING & cdat[r] != -1)
                            {
                                x2 = ToCanvasX(xdat[r]);
                                // Confidence interval
                                if (cdat[r] > 0)
                                {
                                    Color ciPenColour = sOptions.UseSeriesColourForConfidenceIntervals ? p.Color : ciMarkerType.Color;
                                    using (Pen ciPen = new Pen(ciPenColour, ciMarkerType.Width) {DashStyle = ciMarkerType.Style})
                                    {
                                        double y2l = ToCanvasY(ydat_l[r]);
                                        double y2u = ToCanvasY(ydat_u[r]);
                                        DrawLine(ciPen, x2, y2l, x2, y2u);
                                    }
                                }
                            }
                            // x1 = x2; 
                        }
                    }
                }
            }
            EndMetafile();
            return new ParameterBag();
        }

        private ScaleParameters GetGiniScaleParameters()
        {
            return new ScaleParameters
                                     {
                                         X =
                                             {
                                                 AllowedScaleTypes = new[] { ScaleType.Linear },
                                                 ShouldCheck = false,
                                                 Max = 1,
                                                 Min = 0
                                             },
                                         Y =
                                             {
                                                 AllowedScaleTypes = new[] { ScaleType.Linear },
                                                 ShouldCheck = false,
                                                 Max = 1,
                                                 Min = 0
                                             }
                                     };
        }

        private ParameterBag PlotGini(Stream outputStream)
        {
            GiniOptions gOptions = ((GiniOptions)(definition.ChartOptions));
            DoubleSeries xs0 = definition.XSeries[0].AsDoubleSeries;
            DoubleSeries ys0 = definition.YSeries[0].AsDoubleSeries;
            StartMetafile(outputStream, true);

            DataMinX = 0.0;
            DataMaxX = 1.0;
            DataMinY = 0.0;
            DataMaxY = 1.0;

            // Draw the scale
            DrawAxes(gOptions.Title.Trim(), new Axis(gOptions.XAxisTitle, AxisMode.Scale, 0, definition.ScaleParameters.X.ScaleType), new Axis(gOptions.YAxisTitle, AxisMode.Scale, 0, definition.ScaleParameters.Y.ScaleType), true, true, false);

            // get the offsets for the Markers
            SetStandardScaling();

            // Draw equality line
            using (Pen redPen = new Pen(grRed))
            {
                double x1 = ToCanvasX(0);
                double y1 = ToCanvasY(0);
                double x2 = ToCanvasX(1.0);
                double y2 = ToCanvasY(1.0);
                DrawLine(redPen, x1, y1, x2, y2);
            }

            // Draw Lorenz polygon
            using (Pen greenPen = new Pen(grGreen))
            {
                double lastX = offx;
                double lastY = offy;
                for (int j = 0; j <= xs0.Points - 1; j++)
                {
                    double x = ToCanvasX(xs0.Data[j]);
                    double y = ToCanvasY(ys0.Data[j]);
                    DrawLine(greenPen, lastX, lastY, x, y);
                    lastX = x;
                    lastY = y;
                }
            }
            EndMetafile();
            return new ParameterBag();
        }

        public string PlotBiasMAAndReturnRtf(ITemplateHost host, double[] x, double[] yy, double[] yw, int rows, string xtxt, double[] cl, double[] cu, double cco, double cit, double rmh, Transformation xform, bool diagonal)
        {
            using (MemoryStream metaStream = new MemoryStream())
            {
                Plot_Bias_MA(metaStream, host, x, yy, yw, rows, xtxt, cl, cu, cco, cit, rmh, xform, diagonal);
                return host.ImageStreamToRtf(metaStream);
            }
        }

        private void Plot_Bias_MA(Stream outputStream, IChartHost host, double[] x, double[] yy, double[] yw, int rows, string xtxt, double[] cl, double[] cu, double cco, double cit, double rmh, Transformation xform, bool diagonal)
        {
            string ytx = null;
            double[] y;
            string title;
            bool reverse = false; bool use_ci = false;
            int plotMethod;
            get_ma_ordinate(host, out y, yy, yw, cl, cu, ref cco, rows, out title, ref ytx, xtxt, out plotMethod, xform, ref reverse, ref use_ci);

            double[] xx = new double[rows + 1 /* for VB to C# conversion */ ];
            xx[0] = Constant.MISSING;
            switch (xform)
            {
                case Transformation.Log:
                    for (int r = 1; r <= rows; r++)
                    {
                        if (x[r] > 0.0 & x[r] != Constant.MISSING)
                        {
                            xx[r] = Math.Log(x[r]);
                        }
                        else { xx[r] = Constant.MISSING; }
                    }
                    break;
                case Transformation.Z:
                    for (int r = 1; r <= rows; r++)
                    {
                        if (x[r] != Constant.MISSING)
                        {
                            xx[r] = MathDbl.rtoz(x[r]);
                        }
                        else { xx[r] = Constant.MISSING; }
                    }
                    break;
                case Transformation.None:
                    for (int r = 1; r <= rows; r++)
                    {
                        xx[r] = x[r];
                    }
                    break;
            }


            // get the Min and Max for the data
            GetMinMaxArray(xx, out dataMinX, out dataMaxX);
            GetMinMaxArray(y, out dataMinY, out dataMaxY);

            // get complete funnel by extending x axis so the funnel does not cut the y axis
            double pool = 0;
            switch (xform)
            {
                case Transformation.Log:
                    pool = Math.Log(rmh);
                    break;
                case Transformation.Z:
                    pool = MathDbl.rtoz(rmh);
                    break;
                case Transformation.None:
                    pool = rmh;
                    break;
            }

            double amin = 0;
            double aint = 0;
            AxisScaler.Q_Axis(ref dataMinY, ref dataMaxY, out yDiv, ref amin, ref aint, out minorTicsPerMajorTic, ScaleType.Linear);
            double ymn = amin;
            double ymx = amin + (aint * yDiv);
            double mini = Math.Min(aint, DataMinY);
            if (use_ci)
            {
                if (plotMethod == 2)
                {
                    if (DataMaxX < pool + ma_plot_se(ymn, mini, plotMethod) * cit)
                    {
                        DataMaxX = pool + ma_plot_se(ymn, mini, plotMethod) * cit;
                    }
                    if (DataMinX > pool - ma_plot_se(ymn, mini, plotMethod) * cit)
                    {
                        DataMinX = pool - ma_plot_se(ymn, mini, plotMethod) * cit;
                    }
                }
                else
                {
                    if (DataMaxX < pool + ma_plot_se(ymx, mini, plotMethod) * cit)
                    {
                        DataMaxX = pool + ma_plot_se(ymx, mini, plotMethod) * cit;
                    }
                    if (DataMinX > pool - ma_plot_se(ymx, mini, plotMethod) * cit)
                    {
                        DataMinX = pool - ma_plot_se(ymx, mini, plotMethod) * cit;
                    }
                }
            }

            switch (xform)
            {
                case Transformation.Log:
                    xtxt = "Log(" + xtxt + ")";
                    break;
                case Transformation.Z:
                    xtxt = "Fisher Z(" + xtxt + ")";
                    break;
                case Transformation.None:
                    //  Do nothing
                    break;
            }

            StartMetafile(outputStream, true);
            if (reverse)
            {
                DrawAxes(title, new Axis(xtxt, AxisMode.Scale, 0, ScaleType.Linear), new Axis(ytx, AxisMode.ReverseScale, 0, ScaleType.Linear), false, true, false);
            }
            else
            {
                // Peto plots are boxed
                DrawAxes(title, new Axis(xtxt, AxisMode.Scale, 0, ScaleType.Linear), new Axis(ytx, AxisMode.Scale, 0, ScaleType.Linear), diagonal, true, false);
            }

            // get the offsets for the Graph
            SetStandardScaling();

            // plot the points
            double x1; double y1;
            for (int r = 1; r <= rows; r++)
            {
                if (xx[r] != Constant.MISSING & y[r] != Constant.MISSING)
                {
                    x1 = ToCanvasX(xx[r]);
                    y1 = get_y1(y[r], reverse);
                    DrawMarker(x1, y1, 6, _markerTypes[0]);
                }
            }

            double xnow; double ynow;
            using (Pen blackPen = new Pen(grBlack, 1))
            {
                if (!(diagonal))
                {
                    // mark pooled value
                    ynow = axisYMin;
                    xnow = pool;
                    y1 = get_y1(ynow, reverse);
                    x1 = ToCanvasX(xnow);
                    ynow = axisYMax;
                    double y2 = get_y1(ynow, reverse);
                    double x2 = ToCanvasX(xnow);
                    DrawLine(blackPen, x1, y1, x2, y2);
                }

                if ((plotMethod == 1 || plotMethod == 2 || plotMethod == 7) && !(diagonal) && use_ci)
                {
                    // plot confidence interval
                    int incs = plotMethod == 1 ? 1 : 300;
                    double yinc = divy / Convert.ToDouble(incs);
                    if (plotMethod == 2)
                    {
                        ynow = axisYMax;
                        xnow = pool + ma_plot_se(ynow, mini, plotMethod) * cit;
                        y1 = get_y1(ynow, reverse);
                        x1 = ToCanvasX(xnow);
                        for (int r = 1; r <= incs; r++)
                        {
                            ynow = ynow - yinc;
                            xnow = pool + ma_plot_se(ynow, mini, plotMethod) * cit;
                            if (xnow >= axisXMin & xnow <= axisXMax)
                            {
                                double y2 = get_y1(ynow, reverse);
                                double x2 = ToCanvasX(xnow);
                                DrawLine(blackPen, x1, y1, x2, y2);
                                y1 = y2;
                                x1 = x2;
                            }
                        }
                        ynow = axisYMax;
                        xnow = pool - ma_plot_se(ynow, mini, plotMethod) * cit;
                        y1 = get_y1(ynow, reverse);
                        x1 = ToCanvasX(xnow);
                        for (int r = 1; r <= incs; r++)
                        {
                            ynow = ynow - yinc;
                            xnow = pool - ma_plot_se(ynow, mini, plotMethod) * cit;
                            if (xnow >= axisXMin & xnow <= axisXMax)
                            {
                                double y2 = get_y1(ynow, reverse);
                                double x2 = ToCanvasX(xnow);
                                DrawLine(blackPen, x1, y1, x2, y2);
                                y1 = y2;
                                x1 = x2;
                            }
                        }
                    }
                    else
                    {
                        ynow = axisYMin;
                        xnow = pool;
                        y1 = get_y1(ynow, reverse);
                        x1 = ToCanvasX(xnow);
                        for (int r = 1; r <= incs; r++)
                        {
                            ynow = ynow + yinc;
                            xnow = pool + ma_plot_se(ynow, mini, plotMethod) * cit;
                            if (xnow >= axisXMin & xnow <= axisXMax)
                            {
                                double y2 = get_y1(ynow, reverse);
                                double x2 = ToCanvasX(xnow);
                                DrawLine(blackPen, x1, y1, x2, y2);
                                y1 = y2;
                                x1 = x2;
                            }
                        }
                        ynow = axisYMin;
                        xnow = pool;
                        y1 = get_y1(ynow, reverse);
                        x1 = ToCanvasX(xnow);
                        for (int r = 1; r <= incs; r++)
                        {
                            ynow = ynow + yinc;
                            xnow = pool - ma_plot_se(ynow, mini, plotMethod) * cit;
                            if (xnow >= axisXMin & xnow <= axisXMax)
                            {
                                double y2 = get_y1(ynow, reverse);
                                double x2 = ToCanvasX(xnow);
                                DrawLine(blackPen, x1, y1, x2, y2);
                                y1 = y2;
                                x1 = x2;
                            }
                        }
                    }
                }
            }

            if (diagonal)
            {
                using (Pen tenPen = GetPen(_markerTypes[10], true))
                {
                    DrawLine(tenPen, xAxisCanvas, yAxisCanvas, xAxisCanvas + xExtCanvas, yAxisCanvas + yExtCanvas);
                }
            }
            EndMetafile();
        }

        public string PlotTiesAndReturnMetafile(ITemplateHost host, double[] x, double[] y, int nx, double lla, double ula, double GAMMA, string v0Title, string v1Title, double mean)
        {
            using (MemoryStream metaStream = new MemoryStream())
            {
                StartMetafile(metaStream);
                PlotTies(host, x, y, nx, lla, ula, GAMMA, v0Title, v1Title, mean);
                EndMetafile();
                return host.ImageStreamToRtf(metaStream);
            }
        }

        private void PlotTies(IChartHost Host, double[] x, double[] y, int nx, double lla, double ula, double GAMMA, string v0Title, string v1Title, double mean)
        {
            DataMinX = x[1];
            DataMaxX = x[1];
            DataMinY = y[1];
            DataMaxY = y[1];
            for (int j = 2; j <= nx; j++)
            {
                if (x[j] > DataMaxX)
                {
                    DataMaxX = x[j];
                }
                if (y[j] > DataMaxY)
                {
                    DataMaxY = y[j];
                }
                if (x[j] < DataMinX)
                {
                    DataMinX = x[j];
                }
                if (y[j] < DataMinY)
                {
                    DataMinY = y[j];
                }
            }
            if (lla < DataMinY)
            {
                DataMinY = lla;
            }
            if (ula > DataMaxY)
            {
                DataMaxY = ula;
            }
            // Draw the scale
            string xtxt = "Mean ((" + v0Title + " + " + v1Title + ") / 2)";
            string ytxt = "Difference (" + v0Title + " - " + v1Title + ")";
            DrawAxes("", new Axis(xtxt, AxisMode.Scale, 0, ScaleType.Linear), new Axis(ytxt, AxisMode.Scale, 0, ScaleType.Linear), false, true, false);
            // Draw the titles
            int size2 = labelFont.Height * 2;
            DrawStringLegend("mean difference ? " + Formatting.XRound(GAMMA * 100.0, 2) + "% limits of agreement", xAxisCanvas + xExtCanvas, yAxisCanvas + yExtCanvas + size2, StringAlignment.Far);

            // get the offsets for the Markers
            SetStandardScaling();

            // Draw the limits
            double x1 = xAxisCanvas + xExtCanvas;
            double y1 = ToCanvasY(ula);
            using (Pen redPen = new Pen(grRed, 1))
            {
                using (Pen blackPen = new Pen(grBlack, 1))
                {
                    DrawLine(redPen, xAxisCanvas, y1, x1, y1);
                    y1 = ToCanvasY(lla);
                    DrawLine(redPen, xAxisCanvas, y1, x1, y1);
                    y1 = ToCanvasY(mean);
                    DrawLine(blackPen, xAxisCanvas, y1, x1, y1);
                    // Work through the rows
                    for (int r = 1; r <= nx; r++)
                    {
                        x1 = ToCanvasX(x[r]);
                        y1 = ToCanvasY(y[r]);
                        DrawMarker(x1, y1, 6, _markerTypes[0]);
                    }
                }
            }
        }


        // TRANSMISSINGCOMMENT: Method get_y1
        private double get_y1(double ynow, bool reverse)
        {
            return reverse ? yExtCanvas + yAxisCanvas + yAxisCanvas - ToCanvasY(ynow) : ToCanvasY(ynow);
        }


        // TRANSMISSINGCOMMENT: Method GetAgreementPairScaleParameters
        private ScaleParameters GetAgreementPairScaleParameters()
        {
            if (!((definition == null || definition.ChartOptions == null)))
            {
                AgreementOptions aOptions = ((AgreementOptions)(definition.ChartOptions));
                GetMinMaxArray(aOptions.mxd, out axisYMin, out axisYMax);
                if (aOptions.HasLimits)
                {
                    if (aOptions.lla < axisYMax)
                    {
                        axisYMin = aOptions.lla;
                    }
                    if (aOptions.ula > axisYMax)
                    {
                        axisYMax = aOptions.ula;
                    }
                }
                GetMinMaxArray(aOptions.av, out axisXMin, out axisXMax);
            }

            return new ScaleParameters
                                     {
                                         X =
                                             {
                                                 AllowedScaleTypes = new[] { ScaleType.Linear },
                                                 ShouldCheck = true,
                                                 Max = axisXMax,
                                                 Min = axisXMin
                                             },
                                         Y =
                                             {
                                                 AllowedScaleTypes = new[] { ScaleType.Linear },
                                                 ShouldCheck = true,
                                                 Max = axisYMax,
                                                 Min = axisYMin
                                             }
                                     };
        }


        // TRANSMISSINGCOMMENT: Method PlotAgreementPair
        private ParameterBag PlotAgreementPair(Stream outputStream)
        {
            AgreementOptions aOptions = ((AgreementOptions)(definition.ChartOptions));
            StartMetafile(outputStream, true);
            GetMinMaxArray(aOptions.mxd, out axisYMin, out axisYMax);
            using (Pen p = GetPen(_markerTypes[0], true))
            {
                if (aOptions.HasLimits)
                {
                    if (aOptions.lla < axisYMax)
                    {
                        axisYMin = aOptions.lla;
                    }
                    if (aOptions.ula > axisYMax)
                    {
                        axisYMax = aOptions.ula;
                    }
                    string xtxt = definition.ChartOptions.XAxisTitle;
                    if (string.IsNullOrEmpty(xtxt))
                    {
                        xtxt = "mean";
                    }
                    string ytxt = definition.ChartOptions.YAxisTitle;
                    if (string.IsNullOrEmpty(ytxt))
                    {
                        ytxt = "difference";
                    }
                    PlotXY(aOptions.av, aOptions.mxd, xtxt, ytxt, "Agreement Plot (" + Formatting.XRound(100 * (1 - aOptions.P0), 2) + "% limits of agreement)", false, -1, _markerTypes[0].MarkerSize, _markerTypes[0].Shape, _markerTypes[0].IsFilled, p, false);
                }
                else
                {
                    string xtxt = definition.ChartOptions.XAxisTitle;
                    if (string.IsNullOrEmpty(xtxt))
                    {
                        xtxt = "mean";
                    }
                    string ytxt = definition.ChartOptions.YAxisTitle;
                    if (string.IsNullOrEmpty(ytxt))
                    {
                        ytxt = "maximum difference";
                    }
                    PlotXY(aOptions.av, aOptions.mxd, xtxt, ytxt, "Agreement Plot", false, -1, _markerTypes[0].MarkerSize, _markerTypes[0].Shape, _markerTypes[0].IsFilled, p, false);
                }
            }
            // Get the offsets
            SetStandardScaling();

            // Plot mean
            using (Pen greenPen = new Pen(grGreen, 2))
            {
                double y1 = ToCanvasY(aOptions.mean);
                DrawLine(greenPen, xAxisCanvas, y1, xAxisCanvas + xExtCanvas, y1);
                if (aOptions.HasLimits)
                {
                    using (Pen blackPen = new Pen(grBlack, 1))
                    {
                        // Plot upper limit
                        y1 = ToCanvasY(aOptions.ula);
                        DrawLine(greenPen, xAxisCanvas, y1, xAxisCanvas + xExtCanvas, y1);
                        // Plot lower limit
                        y1 = ToCanvasY(aOptions.lla);
                        DrawLine(greenPen, xAxisCanvas, y1, xAxisCanvas + xExtCanvas, y1);
                    }
                }
            }
            EndMetafile();
            return new ParameterBag();
        }


        private void get_ma_ordinate(IChartHost host, out double[] y, double[] yy, double[] yw, double[] cl, double[] cu, ref double cco, int rows, out string title, ref string ytx, string xtxt, out int plot_method, Transformation xform, ref bool reverse, ref bool use_ci)
        {
            y = new double[rows + 1 /* for VB to C# conversion */ ];
            y[0] = Constant.MISSING;
            if (xtxt == "Peto weights")
            {
                ytx = "Observed-Expected";
                title = "Peto O-E vs. V plot";
                plot_method = 2;
                for (int r = 1; r <= rows; r++)
                {
                    if (yw[r] == 0.0 || yw[r] == Constant.MISSING)
                    {
                        y[r] = Constant.MISSING;
                    }
                    else { y[r] = 1.0 / yw[r]; }
                }
                return;
            }

            bool usept = xtxt.Contains("Incidence");
            use_ci = host.MetaPlotCI;

            double cit;
            if (cco > 0.0)
            {
                int transTemp42;
                cit = PDF.gauinv(1.0 - ((1.0 - cco) / 2.0), out transTemp42);
            }
            else
            {
                cco = 0.95;
                int transTemp41;
                cit = PDF.gauinv(0.975, out transTemp41);
            }

            plot_method = host.MetaPlotMethod;

            switch (plot_method)
            {
                case 1:
                    reverse = true;
                    ytx = "Standard error";
                    switch (xform)
                    {
                        case Transformation.Log:
                            for (int r = 1; r <= rows; r++)
                            {
                                if (cu[r] == Constant.MISSING || cl[r] == Constant.MISSING || cu[r] <= 0.0 || cl[r] <= 0.0)
                                {
                                    y[r] = Constant.MISSING;
                                }
                                else { y[r] = ((Math.Log(cu[r]) - Math.Log(cl[r])) / 2.0) / cit; }
                            }
                            break;
                        case Transformation.Z:
                            for (int r = 1; r <= rows; r++)
                            {
                                if (cu[r] == Constant.MISSING || cl[r] == Constant.MISSING)
                                {
                                    y[r] = Constant.MISSING;
                                }
                                else { y[r] = ((MathDbl.rtoz(cu[r]) - MathDbl.rtoz(cl[r])) / 2.0) / cit; }
                            }
                            break;
                        case Transformation.None:
                            for (int r = 1; r <= rows; r++)
                            {
                                if (cu[r] == Constant.MISSING || cl[r] == Constant.MISSING)
                                {
                                    y[r] = Constant.MISSING;
                                }
                                else { y[r] = ((cu[r] - cl[r]) / 2.0) / cit; }
                            }
                            break;
                    }

                    break;
                case 2:
                    reverse = false;
                    ytx = "Precision";
                    switch (xform)
                    {
                        case Transformation.Log:
                            for (int r = 1; r <= rows; r++)
                            {
                                if (cu[r] == Constant.MISSING || cl[r] == Constant.MISSING || cu[r] <= 0.0 || cl[r] <= 0.0)
                                {
                                    y[r] = Constant.MISSING;
                                }
                                else { y[r] = ((Math.Log(cu[r]) - Math.Log(cl[r])) / 2.0) / cit; }
                                if (y[r] != 0.0 & y[r] != Constant.MISSING)
                                {
                                    y[r] = 1.0 / y[r];
                                }
                                else { y[r] = Constant.MISSING; }
                            }
                            break;
                        case Transformation.Z:
                            for (int r = 1; r <= rows; r++)
                            {
                                if (cu[r] == Constant.MISSING | cl[r] == Constant.MISSING)
                                {
                                    y[r] = Constant.MISSING;
                                }
                                else { y[r] = ((MathDbl.rtoz(cu[r]) - MathDbl.rtoz(cl[r])) / 2.0) / cit; }
                                if (y[r] != 0.0 & y[r] != Constant.MISSING)
                                {
                                    y[r] = 1.0 / y[r];
                                }
                                else { y[r] = Constant.MISSING; }
                            }
                            break;
                        case Transformation.None:
                            for (int r = 1; r <= rows; r++)
                            {
                                if (cu[r] == Constant.MISSING | cl[r] == Constant.MISSING)
                                {
                                    y[r] = Constant.MISSING;
                                }
                                else { y[r] = ((cu[r] - cl[r]) / 2.0) / cit; }
                                if (y[r] != 0.0 & y[r] != Constant.MISSING)
                                {
                                    y[r] = 1.0 / y[r];
                                }
                                else { y[r] = Constant.MISSING; }
                            }
                            break;
                    }

                    break;
                case 3:
                    reverse = true;
                    ytx = "1/Sample size";
                    for (int r = 1; r <= rows; r++)
                    {
                        if (yy[r] == 0.0)
                        {
                            y[r] = Constant.MISSING;
                        }
                        else { y[r] = 1.0 / yy[r]; }
                    }
                    break;
                case 4:
                    reverse = false;
                    ytx = "Sample size";
                    for (int r = 1; r <= rows; r++)
                    {
                        y[r] = yy[r];
                    }
                    break;
                case 5:
                    reverse = true;
                    ytx = "1/Log(sample size)";
                    for (int r = 1; r <= rows; r++)
                    {
                        if (yy[r] <= 0.0 | yy[r] == 1.0)
                        {
                            y[r] = Constant.MISSING;
                        }
                        else { y[r] = 1.0 / (Math.Log(yy[r]) / Math.Log(10.0)); }
                    }
                    break;
                case 6:
                    reverse = false;
                    ytx = "Log(sample size)";
                    for (int r = 1; r <= rows; r++)
                    {
                        if (yy[r] <= 0.0)
                        {
                            y[r] = Constant.MISSING;
                        }
                        else { y[r] = Math.Log(yy[r]) / Math.Log(10.0); }
                    }
                    break;
                case 7:
                    reverse = true;
                    ytx = "1/MH weight";
                    for (int r = 1; r <= rows; r++)
                    {
                        if (yw[r] == 0.0)
                        {
                            y[r] = Constant.MISSING;
                        }
                        else { y[r] = 1.0 / yw[r]; }
                    }
                    break;
            }

            if (usept)
            {
                ytx = ytx.Replace("sample size", "person-time");
            }
            title = "Bias assessment plot";
        }


        // TRANSMISSINGCOMMENT: Method ma_plot_se
        private double ma_plot_se(double y, double z, int plot_method)
        {

            switch (plot_method)
            {
                case 1:
                    return y;
                case 2:
                    return y == 0.0 ? 1.0 / z : 1.0 / y;
                case 7:
                    return y < 0.0 ? 0.0 : Math.Sqrt(y);
            }

            return 0;
        }




        ///  <summary>
        ///  Cause the host to amend the thisData record in-place with any revisions to the cutoff data.
        ///  </summary>
        ///  <param name="host"></param>
        ///  <param name="thisData"></param>
        ///  <param name="Weight"></param>
        ///  <param name="ti"></param>
        ///  <remarks></remarks>
        private ROCSeriesRecord ShowCutoff(ITemplateHost host, ROCSeriesRecord thisData, double Weight, string ti)
        {
            ROCCutoff payload = new ROCCutoff { SeriesRecord = thisData, Weight = Weight, Title = ti };
            host.Amend(payload, null);
            return payload.SeriesRecord;
        }


        // TRANSMISSINGCOMMENT: Method DeLongPsi
        private double DeLongPsi(double x, double y)
        {
            if (y < x)
                return 1.0;
            if (y == x)
                return 0.5;
            return 0.0;
        }


        // TRANSMISSINGCOMMENT: Method DeLongSE
        private double DeLongSE(double[] x, double[] y, double auc)
        {

            double[] v10 = new double[x.Length + 1 /* for VB to C# conversion */ ];
            double[] v01 = new double[y.Length + 1 /* for VB to C# conversion */ ];
            for (int i = 0; i <= x.Length - 1; i++)
            {
                for (int j = 0; j <= y.Length - 1; j++)
                {
                    v10[i] = v10[i] + DeLongPsi(x[i], y[j]);
                }
                v10[i] = v10[i] / Convert.ToDouble(y.Length);
            }
            for (int j = 0; j <= y.Length - 1; j++)
            {
                for (int i = 0; i <= x.Length - 1; i++)
                {
                    v01[j] = v01[j] + DeLongPsi(x[i], y[j]);
                }
                v01[j] = v01[j] / Convert.ToDouble(x.Length);
            }
            double s10 = 0.0;
            double s01 = 0.0;
            for (int i = 0; i <= x.Length - 1; i++)
            {
                s10 = s10 + Math.Pow((v10[i] - auc), 2.0);
            }
            s10 = s10 / Convert.ToDouble(x.Length - 1);
            for (int j = 0; j <= y.Length - 1; j++)
            {
                s01 = s01 + Math.Pow((v01[j] - auc), 2.0);
            }
            s01 = s01 / Convert.ToDouble(y.Length - 1);
            double var = s10 / Convert.ToDouble(x.Length) + s01 / Convert.ToDouble(y.Length);
            return var < 0.0 ? Constant.MISSING : Math.Sqrt(var);
        }


        // TRANSMISSINGCOMMENT: Method Swap
        private void Swap(ref double x, ref double y)
        {
            double temp = x;
            x = y;
            y = temp;
        }


        // TRANSMISSINGCOMMENT: Method AxisLabelWidth
        private float AxisLabelWidth(string s)
        {
            return canvas.MeasureString(s, axisLabelFont).Width;
        }


        // TRANSMISSINGCOMMENT: Method AxisLabelHeight
        private float AxisLabelHeight(string s)
        {
            return canvas.MeasureString(s, axisLabelFont).Height;
        }


        // TRANSMISSINGCOMMENT: Method LabelHeight
        private float LabelHeight(string s)
        {
            return canvas.MeasureString(s, labelFont).Height;
        }


        // TRANSMISSINGCOMMENT: Method LegendWidth
        private float LegendWidth(string s)
        {
            return canvas.MeasureString(s, legendFont).Width;
        }


        // TRANSMISSINGCOMMENT: Method LegendHeight
        private float LegendHeight(string s)
        {
            return canvas.MeasureString(s, legendFont).Height;
        }


        // TRANSMISSINGCOMMENT: Method TitleWidth
        private float TitleWidth(string s)
        {
            return canvas.MeasureString(s, titleFont).Width;
        }


        // TRANSMISSINGCOMMENT: Method combo_ti
        public static string combo_ti(string cap)
        {

            string x = "combined";
            if (cap.Contains("fixed effects"))
            {
                x += " [fixed]";
            }
            else
            {
                if (cap.Contains("random effects"))
                {
                    x += " [random]";
                }
            }
            return x;
        }


        // TRANSMISSINGCOMMENT: Method ToCanvasX
        private double ToCanvasX(double ChartX)
        {
            double transformedX = ChartX;
            if (HasScaleParameters && definition.ScaleParameters.X != null)
            {
                switch (definition.ScaleParameters.X.ScaleType)
                {
                    case ScaleType.Log10:
                        transformedX = Math.Log10(ChartX);
                        break;
                    case ScaleType.LogNatural:
                        transformedX = Math.Log(ChartX) / LOG2;
                        break;
                    default:
                        transformedX = ChartX;
                        break;
                }

            }
            return offx + (transformedX / divx * xExtCanvas);
        }


        // TRANSMISSINGCOMMENT: Method ToCanvasY
        private double ToCanvasY(double ChartY)
        {
            double transformedY = ChartY;
            if (HasScaleParameters && definition.ScaleParameters.Y != null)
            {
                switch (definition.ScaleParameters.Y.ScaleType)
                {
                    case ScaleType.Log10:
                        transformedY = Math.Log10(ChartY);
                        break;
                    case ScaleType.LogNatural:
                        transformedY = Math.Log(ChartY) / LOG2;
                        break;
                    default:
                        transformedY = ChartY;
                        break;
                }

            }
            return offy + (transformedY / divy * yExtCanvas);
        }

        public void DrawLineInChartCoordinates(Color Color, double x1, double y1, double x2, double y2)
        {
            if (mostRecentPen == null || !(mostRecentPen.Color.Equals(Color)))
            {
                if (mostRecentPen != null)
                {
                    mostRecentPen.Dispose();
                }
                mostRecentPen = new Pen(Color);
            }
            double dx1 = ToCanvasX(x1);
            double dy1 = ToCanvasY(y1);
            double dx2 = ToCanvasX(x2);
            double dy2 = ToCanvasY(y2);
            DrawLine(mostRecentPen, dx1, dy1, dx2, dy2);
        }

        public IList<string> x_plgraph(ITemplateHost host, double[,] h, double[,] s, double[,] stime, int[,] dead, int groups, int[] cnx, string[] glab, bool tic, bool marker)
        {
            double x1 = 0; double y1 = 0;
            int j3;

            IList<string> outputImages = new List<string>();
            const string tim = "Times";
            const string ltim = "Log Times";
            int gx = stime.GetUpperBound(0);
            double[,] x = new double[gx + 1 /* for VB to C# conversion */, groups + 1 /* for VB to C# conversion */];
            double[,] y = new double[gx + 1 /* for VB to C# conversion */, groups + 1 /* for VB to C# conversion */];
            for (j3 = 1; j3 <= 5; j3++)
            {
                string vx;
                string vy;
                string vt;
                switch (j3)
                {
                    case 1:
                        vx = tim;
                        vy = "Survivor";
                        vt = "Survival Plot (PL estimates)";
                        break;
                    case 2:
                        vx = tim;
                        vy = "Hazard";
                        vt = "Hazard Plot";
                        break;
                    case 3:
                        vx = ltim;
                        vy = "Log Hazard";
                        vt = "Log Hazard Plot";
                        break;
                    case 4:
                        vx = ltim;
                        vy = "Z (Survivor)";
                        vt = "Lognormal Survival Plot";
                        break;
                    case 5:
                        vx = tim;
                        vy = "Hazard / Time";
                        vt = "Hazard Rate Plot";
                        break;
                    default:
                        throw new Exception("Unexpected j3");
                }

                int k;
                int j;
                for (k = 1; k <= groups; k++)
                {
                    int nx = 0;
                    for (j = 1; j <= cnx[k]; j++)
                    {
                        switch (j3)
                        {
                            case 1:
                                nx = nx + 1;
                                x[nx, k] = stime[j, k];
                                y[nx, k] = s[j, k];
                                break;
                            case 2:
                                if (h[j, k] != Constant.MISSING)
                                {
                                    nx = nx + 1;
                                    x[nx, k] = stime[j, k];
                                    y[nx, k] = h[j, k];
                                }
                                break;
                            case 3:
                                if (h[j, k] != Constant.MISSING & stime[j, k] > 0 & h[j, k] > 0)
                                {
                                    nx = nx + 1;
                                    x[nx, k] = Math.Log(stime[j, k]);
                                    y[nx, k] = Math.Log(h[j, k]);
                                }
                                break;
                            case 4:
                                int fault;
                                double Q = PDF.gauinv(s[j, k], out fault);
                                if (fault == 0 & stime[j, k] > 0)
                                {
                                    nx = nx + 1;
                                    x[nx, k] = Math.Log(stime[j, k]);
                                    y[nx, k] = Q;
                                }
                                break;
                            case 5:
                                if (h[j, k] != Constant.MISSING & stime[j, k] != 0)
                                {
                                    nx = nx + 1;
                                    x[nx, k] = stime[j, k];
                                    y[nx, k] = h[j, k] / stime[j, k];
                                }
                                break;
                        }

                    }
                    cnx[k] = nx;
                }
                // Plot the results
                using (MemoryStream metaStream = new MemoryStream())
                {
                    StartMetafile(metaStream, true);
                    DataMaxX = double.MinValue;
                    DataMaxY = double.MinValue;
                    DataMinX = double.MaxValue;
                    DataMinY = double.MaxValue;
                    for (k = 1; k <= groups; k++)
                    {
                        for (j = 1; j <= cnx[k]; j++)
                        {
                            if (x[j, k] > DataMaxX)
                                DataMaxX = x[j, k];
                            if (x[j, k] < DataMinX)
                                DataMinX = x[j, k];
                            if (y[j, k] > DataMaxY)
                                DataMaxY = y[j, k];
                            if (y[j, k] < DataMinY)
                                DataMinY = y[j, k];
                        }
                    }
                    if (j3 == 1)
                    {
                        DataMaxY = 1;
                        DataMinY = 0;
                    }
                    // Draw the axes
                    DrawAxes(vt, new Axis(vx, AxisMode.Scale, 0, ScaleType.Linear), new Axis(vy, AxisMode.Scale, 0, ScaleType.Linear), boxAxes, true, false);
                    // get the offsets for the Graph
                    SetStandardScaling();

                    // Plot the legends
                    int size2 = labelFont.Height * 2;
                    if (groups > 1)
                    {
                        for (k = 1; k <= groups; k++)
                        {
                            string vq = glab[k];
                            if (marker)
                            {
                                DrawMarker(12, yAxisCanvas + yExtCanvas - 22 - (size2 * k), 6, _markerTypes[(k - 1) % 9]);
                            }
                            else
                            {
                                using (Pen p = GetPen(_markerTypes[(k - 1) % 9], true))
                                {
                                    DrawLine(p, 10, yAxisCanvas + yExtCanvas - 18 - (size2 * k), 20, yAxisCanvas + yExtCanvas - 18 - (size2 * k));
                                    DrawLine(p, 20, yAxisCanvas + yExtCanvas - 18 - (size2 * k), 20, yAxisCanvas + yExtCanvas - 28 - (size2 * k));
                                }
                            }
                            DrawStringLegendL(vq, 24, yAxisCanvas + yExtCanvas - 10 - (size2 * k));
                        }
                    }
                    for (k = 1; k <= groups; k++)
                    {
                        using (Pen p = GetPen(_markerTypes[(k - 1) % 9], true))
                        {
                            switch (j3)
                            {
                                case 1:
                                    x1 = ToCanvasX(axisXMin);
                                    y1 = ToCanvasY(1.0);
                                    break;
                                case 2:
                                    x1 = ToCanvasX(axisXMin);
                                    y1 = ToCanvasY(0);
                                    break;
                                case 3:
                                    x1 = ToCanvasX(x[1, k]);
                                    y1 = ToCanvasY(y[1, k]);
                                    break;
                                case 4:
                                    x1 = ToCanvasX(x[1, k]);
                                    y1 = ToCanvasY(y[1, k]);
                                    break;
                                case 5:
                                    x1 = ToCanvasX(x[1, k]);
                                    y1 = ToCanvasY(y[1, k]);
                                    break;
                            }

                            for (j = 1; j <= cnx[k]; j++)
                            {
                                double x2 = ToCanvasX(x[j, k]);
                                double Y2 = ToCanvasY(y[j, k]);
                                // Draw the markers
                                // If Y2 <> Y1 Then Draw_Marker X2, Y2, 6, (k - 1) Mod 9
                                // changed to tic mark at censor points March 01
                                if (dead[j, k] == 0 && tic)
                                {
                                    DrawLine(p, x2, Y2, x2, Y2 + 7);
                                }
                                if (dead[j, k] != 0 && marker)
                                {
                                    DrawMarker(x2, Y2, 6, _markerTypes[(k - 1) % 9]);
                                }
                                // Then the lines
                                DrawLine(p, x1, y1, x2, y1);
                                DrawLine(p, x2, y1, x2, Y2);
                                x1 = x2;
                                y1 = Y2;
                            }
                        }
                    }
                    EndMetafile();
                    outputImages.Add(host.ImageStreamToRtf(metaStream));
                }
            }
            return outputImages;
        }

        public string PlotMHAndReturnRtf(ITemplateHost host, int k, double[,] o, double[] odw, string[] title, double rmh, double ll, double ul, double cco, double[] odr, double[] odrl, double[] odru, bool[] lerr, bool[] uerr, string cap, int pbias, string qid, out bool ifault, object xlabel)
        {
            using (MemoryStream metaStream = new MemoryStream())
            {
                StartMetafile(metaStream);
                Plot_MH(metaStream, k, o, odw, title, rmh, ll, ul, cco, odr, odrl, odru, lerr, uerr, cap, pbias, qid, out ifault, xlabel);
                EndMetafile();
                return host.ImageStreamToRtf(metaStream);
            }
        }

        private void Plot_MH(Stream outputStream, int k, double[,] o, double[] odw, string[] title, double rmh, double ll, double ul, double cco, double[] odr, double[] odrl, double[] odru, bool[] lerr, bool[] uerr, string cap, int pbias, string qid, out bool ifault, object xlabel)
        {
            double w;
            double ytop; double xl; double xr; double Y2; double yc = 0; double yt = 0;
            int i; double XM; double yctr;

            if (k > 10)
            {
                scaleYAxis = 1 + (k - 10) / 20;
                if (scaleYAxis > 5)
                {
                    scaleYAxis = 5;
                }
                metaH = scaleYAxis * 800;
            }
            else
            {
                scaleYAxis = 1;
                metaH = 800;
            }
            double[] gw = new double[k + 1 /* for VB to C# conversion */ ];
            double ormax = double.NegativeInfinity;
            double ormin = double.PositiveInfinity;
            double orumax = double.NegativeInfinity;
            double orlmin = double.PositiveInfinity;
            double max_gw = double.NegativeInfinity;
            double absmin = double.PositiveInfinity;
            for (i = 1; i <= k; i++)
            {
                // double a = o[ i, 1 ]; 
                // double b = o[ i, 2 ]; 
                // double C = o[ i, 3 ]; 
                // double D = o[ i, 4 ]; 
                // double N = a + b + C + D; 
                if (odw[i] != Constant.MISSING)
                {
                    if (odw[i] > max_gw)
                    {
                        max_gw = odw[i];
                    }
                    gw[i] = odw[i];
                }
                if (odr[i] != Constant.MISSING && include_table(o, i))
                {
                    if (odr[i] > ormax)
                    {
                        ormax = odr[i];
                    }
                    if (odru[i] > orumax && odru[i] != Constant.MISSING)
                    {
                        orumax = odru[i];
                    }
                    if (odru[i] < orlmin && odru[i] > 0 && odru[i] != Constant.MISSING)
                    {
                        orlmin = odru[i];
                    }
                    if (odr[i] > 0)
                    {
                        if (odr[i] < ormin)
                        {
                            ormin = odr[i];
                        }
                        if (odrl[i] < orlmin && odrl[i] > 0 && odrl[i] != Constant.MISSING)
                        {
                            orlmin = odrl[i];
                        }
                    }
                    if (Math.Abs(odr[i]) < absmin && odr[i] != 0.0)
                    {
                        absmin = Math.Abs(odr[i]);
                    }
                    if (Math.Abs(odrl[i]) < absmin && odrl[i] != 0.0 && odrl[i] != Constant.MISSING)
                    {
                        absmin = Math.Abs(odrl[i]);
                    }
                    if (Math.Abs(odru[i]) < absmin && odru[i] != 0.0 && odru[i] != Constant.MISSING)
                    {
                        absmin = Math.Abs(odru[i]);
                    }
                }
            }

            DataMaxX = ormax;
            DataMinX = orlmin;
            if (DataMaxX < rmh)
            {
                DataMaxX = rmh;
            }
            if (DataMaxX < ul && ul != Constant.MISSING)
            {
                DataMaxX = ul;
            }
            if (DataMaxX < orumax && orumax != Constant.MISSING)
            {
                DataMaxX = orumax;
            }

            const int tics = 15;
            double[] tic = new double[tics + 1 /* for VB to C# conversion */ ];
            tic[1] = 0.00000001;
            tic[2] = 0.00001;
            tic[3] = 0.001;
            tic[4] = 0.01;
            tic[5] = 0.1;
            tic[6] = 0.2;
            tic[7] = 0.5;
            tic[8] = 1;
            tic[9] = 2;
            tic[10] = 5;
            tic[11] = 10;
            tic[12] = 100;
            tic[13] = 1000;
            tic[14] = 100000;
            tic[15] = 100000000;

            double realamin = DataMinX;
            for (i = 2; i <= tics; i++)
            {
                if (tic[i] > DataMinX)
                {
                    realamin = tic[i - 1];
                    break; /* TRANSWARNING: check that break is in correct scope */
                }
            }
            double realamax = DataMaxX;
            for (i = tics - 1; i >= 1; i--)
            {
                if (tic[i] < DataMaxX)
                {
                    realamax = tic[i + 1];
                    break; /* TRANSWARNING: check that break is in correct scope */
                }
            }
            DataMinX = Math.Log(realamin);
            DataMaxX = Math.Log(realamax);

            StartMetafile(outputStream, true);
            DefaultAxes();
            double rgap = 0;
            double xtra = 0;
            // allow room for right hand labels of effect and CI
            for (i = 1; i <= k; i++)
            {
                if (odr[i] != Constant.MISSING)
                {
                    w = canvas.MeasureString(title[i], legendFont).Width + 30;
                    if (w > xtra + xAxisCanvas)
                    {
                        xtra = w - xAxisCanvas - 5;
                    }
                    w = canvas.MeasureString(Formatting.RoundMeta(odr[i], absmin) + " (" + Formatting.RoundMeta(odrl[i], absmin) + ", " + Formatting.RoundMeta(odru[i], absmin) + ")", legendFont).Width;
                    if (w > rgap)
                    {
                        rgap = w;
                    }
                }
            }
            w = canvas.MeasureString(combo_ti(cap), legendFont).Width + 30;
            if (w > xtra + xAxisCanvas)
            {
                xtra = w - xAxisCanvas - 5;
            }
            xExtCanvas = 940 - rgap;
            DrawAxes(cap, new Axis(null, AxisMode.LineOnly, 0, ScaleType.Linear), new Axis(null, AxisMode.None, xtra, ScaleType.Linear), false, false, false);

            divx = DataMaxX - DataMinX;
            offx = -(DataMinX / divx * xExtCanvas) + xAxisCanvas;
            divy = k + pbias;
            offy = yAxisCanvas;

            using (Pen tenPenTrue = GetPen(_markerTypes[10], true))
            {
                using (Pen tenPenFalse = GetPen(_markerTypes[10], false))
                {
                    for (i = 1; i <= tics; i++)
                    {
                        if (tic[i] >= realamin & tic[i] <= realamax)
                        {
                            XM = ToCanvasX(Math.Log(tic[i]));
                            string Lab;
                            if (tic[i] > 1000 | tic[i] < 0.001)
                            {
                                Lab = tic[i].ToString("E");
                            }
                            else
                            {
                                Lab = tic[i].ToString();
                            }
                            DrawStringLabel(Lab, XM, yAxisCanvas - 12, StringAlignment.Center);
                            DrawLine(tenPenTrue, XM, yAxisCanvas - 12, XM, yAxisCanvas);
                        }
                    }

                    int r = 0;
                    double txh = canvas.MeasureString(title[1], labelFont).Height;
                    for (i = k; i >= 1; i--)
                    {
                        r = r + 1;
                        yctr = (r + pbias - 0.5) / divy * yExtCanvas;
                        ytop = (r + pbias) / divy * yExtCanvas;
                        Y2 = (ytop - yctr) / 1.5;
                        yc = offy + yctr;
                        yt = offy + yctr + Y2;
                        double yb = offy + yctr - Y2;
                        // ytop = yctr + ( ytop - yctr ) * 0.1 + ( ytop - yctr ) * 0.9 * ( gw[ i ] / max_gw ); 
                        // ytop = yctr + ( ytop - yctr ) * 0.8; 
                        if (odr[i] != Constant.MISSING & include_table(o, i))
                        {
                            if (odr[i] <= 0 | odr[i] < realamin)
                            {
                                XM = xAxisCanvas;
                            }
                            else
                            {
                                XM = ToCanvasX(Math.Log(odr[i]));
                            }
                            if (odrl[i] <= 0 | odrl[i] < realamin | odrl[i] == Constant.MISSING)
                            {
                                xl = xAxisCanvas;
                            }
                            else
                            {
                                xl = ToCanvasX(Math.Log(odrl[i]));
                            }
                            if (double.IsInfinity(odru[i]) | odru[i] == Constant.MISSING)
                            {
                                xr = ToCanvasX(Math.Log(realamax));
                            }
                            else
                            {
                                xr = odru[i] <= 0 ? offx : ToCanvasX(Math.Log(odru[i]));
                            }
                            // CI line
                            DrawLine(tenPenTrue, xl, yc, xr, yc);
                            // Weight blob
                            DrawSquare(tenPenTrue, XM, yc, (5 + Math.Abs(yt - yb) * (gw[i] / max_gw)) * 0.7, true);
                            // Arrow ends if not plottable
                            if (odrl[i] < 0 | lerr[i] | odrl[i] < orlmin | odrl[i] == Constant.MISSING)
                            {
                                DrawLine(tenPenTrue, xl + Y2, yc + Y2, xl, yc);
                                DrawLine(tenPenTrue, xl, yc, xl + Y2, yc - Y2);
                            }
                            if (uerr[i] | double.IsInfinity(odru[i]) | odru[i] == Constant.MISSING)
                            {
                                DrawLine(tenPenTrue, xr - Y2, yc + Y2, xr, yc);
                                DrawLine(tenPenTrue, xr, yc, xr - Y2, yb - Y2);
                            }
                            DrawStringLabel(title[i], xAxisCanvas - 15, yc + txh / 2, StringAlignment.Far);
                            DrawStringLabel(Formatting.RoundMeta(odr[i], absmin) + " (" + Formatting.RoundMeta(odrl[i], absmin) + ", " + Formatting.RoundMeta(odru[i], absmin) + ")", xAxisCanvas + xExtCanvas + 10, yc + txh / 2, StringAlignment.Near);
                        }
                        else
                        {
                            DrawStringLabel(title[i], xAxisCanvas - 15, yc + txh / 2, StringAlignment.Far);
                            DrawStringLabel("* (excluded)", xAxisCanvas + xExtCanvas + 10, yc + txh / 2, StringAlignment.Near);
                        }
                    }

                    if (DataMinX <= 0)
                    {
                        // zero effect marker
                        XM = ToCanvasX(Math.Log(1));
                        DrawLine(tenPenTrue, XM, yt, XM, yAxisCanvas);
                    }

                    if (pbias == 1)
                    {
                        // pooled diamond
                        double save_yc = yc;
                        yctr = 0.5 / divy * yExtCanvas;
                        ytop = 1 / divy * yExtCanvas;
                        XM = ToCanvasX(Math.Log(rmh));
                        xl = ToCanvasX(Math.Log(ll));
                        xr = ToCanvasX(Math.Log(ul));
                        Y2 = (ytop - yctr) / 1.5;
                        yc = offy + yctr;
                        yt = offy + yctr + Y2;
                        // yb = offy + yctr - Y2; 
                        DrawDiamond(tenPenTrue, XM, yc, Y2 * 2, false);
                        DrawLine(tenPenTrue, xr, yc, xl, yc);
                        // pooled effect marker
                        DrawLine(tenPenFalse, XM, save_yc, XM, yt);
                        // pool label
                        DrawStringLabel(combo_ti(cap), xAxisCanvas - 15, yc + txh / 2, StringAlignment.Far);
                        DrawStringLabel(Formatting.RoundMeta(rmh, absmin) + " (" + Formatting.RoundMeta(ll, absmin) + ", " + Formatting.RoundMeta(ul, absmin) + ")", xAxisCanvas + xExtCanvas + 10, yc + txh / 2, StringAlignment.Near);
                        // xaxis label
                        DrawStringLabel(qid + " (" + Formatting.XRound(cco * 100, 1) + "% confidence interval" + ")", xAxisCanvas + xExtCanvas / 2, 50, StringAlignment.Center);
                    }
                }
            }

            EndMetafile();

            ifault = false;
        }

        public string PlotMHRDAndReturnRtf(ITemplateHost host, int k, double[,] o, double[] odw, string[] title, double rmh, double ll, double ul, double cco, double[] odr, double[] odrl, double[] odru, bool[] lerr, bool[] uerr, string cap, int pbias, string qid, out bool ifault)
        {
            using (MemoryStream metaStream = new MemoryStream())
            {
                StartMetafile(metaStream);
                Plot_MHRD(host, k, o, odw, title, rmh, ll, ul, cco, odr, odrl, odru, lerr, uerr, cap, pbias, qid, out ifault);
                EndMetafile();
                return host.ImageStreamToRtf(metaStream);
            }
        }

        private void Plot_MHRD(ITemplateHost host, int k, double[,] o, double[] odw, string[] title, double rmh, double ll, double ul, double cco, double[] odr, double[] odrl, double[] odru, bool[] lerr, bool[] uerr, string cap, int pbias, string qid, out bool ifault)
        {
            double aint = 0; double amin = 0;
            double w;
            double ytop; double XL; double XR; double Y2; double yc = 0; double yt = 0;
            int i; double XM; double yctr;

            if (k > 10)
            {
                scaleYAxis = 1 + (k - 10) / 20;
                if (scaleYAxis > 5)
                {
                    scaleYAxis = 5;
                }
                metaH = scaleYAxis * 800;
            }
            else
            {
                scaleYAxis = 1;
                metaH = 800;
            }
            double[] gw = new double[k + 1 /* for VB to C# conversion */ ];
            double ormax = double.NegativeInfinity;
            double ormin = double.PositiveInfinity;
            double orumax = double.NegativeInfinity;
            double orlmin = double.PositiveInfinity;
            double max_gw = double.NegativeInfinity;
            double absmin = double.PositiveInfinity;
            for (i = 1; i <= k; i++)
            {
                // double a = o[ i, 1 ]; 
                // double b = o[ i, 2 ]; 
                // double C = o[ i, 3 ]; 
                // double D = o[ i, 4 ]; 
                // double N = a + b + C + D; 
                if (odw[i] != Constant.MISSING)
                {
                    if (odw[i] > max_gw)
                    {
                        max_gw = odw[i];
                    }
                    gw[i] = odw[i];
                }
                if (odr[i] != Constant.MISSING)
                {
                    if (odr[i] > ormax)
                    {
                        ormax = odr[i];
                    }
                    if (odr[i] < ormin)
                    {
                        ormin = odr[i];
                    }
                    if (odrl[i] < orlmin & odrl[i] != Constant.MISSING)
                    {
                        orlmin = odrl[i];
                    }
                    if (odru[i] > orumax & odru[i] != Constant.MISSING)
                    {
                        orumax = odru[i];
                    }
                    if (Math.Abs(odr[i]) < absmin & odr[i] != 0.0)
                    {
                        absmin = Math.Abs(odr[i]);
                    }
                    if (Math.Abs(odrl[i]) < absmin & odrl[i] != 0.0 & odrl[i] != Constant.MISSING)
                    {
                        absmin = Math.Abs(odrl[i]);
                    }
                    if (Math.Abs(odru[i]) < absmin & odru[i] != 0.0 & odru[i] != Constant.MISSING)
                    {
                        absmin = Math.Abs(odru[i]);
                    }
                }
            }

            DataMaxX = ormax;
            DataMinX = ormin;
            if (DataMaxX < rmh)
            {
                DataMaxX = rmh;
            }
            if (DataMaxX < ul & ul != Constant.MISSING)
            {
                DataMaxX = ul;
            }
            if (DataMaxX < orumax & orumax != Constant.MISSING)
            {
                DataMaxX = orumax;
            }
            if (DataMinX > rmh)
            {
                DataMinX = rmh;
            }
            if (DataMinX > ll & ll != Constant.MISSING)
            {
                DataMinX = ll;
            }
            if (DataMinX > orlmin & orlmin != Constant.MISSING)
            {
                DataMinX = orlmin;
            }

            AxisScaler.Q_Axis(ref dataMinX, ref dataMaxX, out xDiv, ref amin, ref aint, out minorTicsPerMajorTic, ScaleType.Linear);
            DataMinX = amin;
            DataMaxX = amin + xDiv * aint;

            DefaultAxes();
            double rgap = 0;
            double xtra = 0;
            // allow room for right hand labels of effect and CI
            for (i = 1; i <= k; i++)
            {
                if (odr[i] != Constant.MISSING)
                {
                    w = canvas.MeasureString(title[i], labelFont).Width + 30;
                    if (w > xtra + xAxisCanvas)
                    {
                        xtra = w - xAxisCanvas - 5;
                    }
                    w = canvas.MeasureString(Formatting.RoundMeta(odr[i], absmin) + " (" + Formatting.RoundMeta(odrl[i], absmin) + ", " + Formatting.RoundMeta(odru[i], absmin) + ")", labelFont).Width;
                    if (w > rgap)
                    {
                        rgap = w;
                    }
                }
            }
            w = canvas.MeasureString(combo_ti(cap), labelFont).Width + 30;
            if (w > xtra + xAxisCanvas)
            {
                xtra = w - xAxisCanvas - 5;
            }
            xExtCanvas = 940 - rgap;
            DrawAxes(cap, new Axis(null, AxisMode.LineOnly, 0, ScaleType.Linear), new Axis(null, AxisMode.None, xtra, ScaleType.Linear), false, false, false);

            divx = DataMaxX - DataMinX;
            offx = -(DataMinX / divx * xExtCanvas) + xAxisCanvas;
            divy = k + pbias;
            offy = yAxisCanvas;

            using (Pen tenPenTrue = GetPen(_markerTypes[10], true))
            {
                using (Pen tenPenFalse = GetPen(_markerTypes[10], false))
                {
                    string msk = GetAxisMask(aint, amin, xDiv, minorTicsPerMajorTic);
                    for (i = 0; i <= xDiv; i++)
                    {
                        XM = ToCanvasX(amin + aint * i);
                        if ((i % minorTicsPerMajorTic) != 0)
                        {
                            DrawLine(tenPenTrue, XM, yAxisCanvas - 7, XM, yAxisCanvas);
                        }
                        else
                        {
                            string Lab = (amin + i * aint).ToString(msk);
                            if (double.Parse(Lab) != 0)
                            {
                                DrawStringLabel(Lab, XM, yAxisCanvas - 12, StringAlignment.Center);
                                DrawLine(tenPenTrue, XM, yAxisCanvas - 12, XM, yAxisCanvas);
                            }
                        }
                    }

                    int r = 0;
                    double txh = canvas.MeasureString(title[1], labelFont).Height;
                    for (i = k; i >= 1; i--)
                    {
                        r = r + 1;
                        yctr = (r + pbias - 0.5) / divy * yExtCanvas;
                        ytop = (r + pbias) / divy * yExtCanvas;
                        Y2 = (ytop - yctr) / 1.5;
                        yc = offy + yctr;
                        yt = offy + yctr + Y2;
                        double yb = offy + yctr - Y2;
                        // ytop = yctr + ( ytop - yctr ) * 0.1 + ( ytop - yctr ) * 0.9 * ( gw[ i ] / max_gw ); 
                        // ytop = yctr + ( ytop - yctr ) * 0.8; 
                        if (odr[i] != Constant.MISSING)
                        {
                            XM = ToCanvasX(odr[i]);
                            XL = odrl[i] == Constant.MISSING ? xAxisCanvas : ToCanvasX(odrl[i]);
                            if (odru[i] == double.PositiveInfinity | odru[i] == Constant.MISSING)
                            {
                                XR = xAxisCanvas + xExtCanvas;
                            }
                            else
                            {
                                XR = ToCanvasX(odru[i]);
                            }
                            // CI line
                            DrawLine(tenPenTrue, XL, yc, XR, yc);
                            // Weight blob
                            DrawSquare(tenPenTrue, XM, yc, (5 + Math.Abs(yt - yb) * (gw[i] / max_gw)) * 0.7, true);
                            // Arrow ends if not plottable
                            if (lerr[i])
                            {
                                DrawLine(tenPenTrue, XL + Y2, yc + Y2, XL, yc);
                                DrawLine(tenPenTrue, XL, yc, XL + Y2, yc - Y2);
                            }
                            if (uerr[i])
                            {
                                DrawLine(tenPenTrue, XR - Y2, yc + Y2, XR, yc);
                                DrawLine(tenPenTrue, XR, yc, XR - Y2, yb - Y2);
                            }
                            DrawStringLabel(title[i], xAxisCanvas - 15, yc + txh / 2, StringAlignment.Far);
                            DrawStringLabel(Formatting.RoundMeta(odr[i], absmin) + " (" + Formatting.RoundMeta(odrl[i], absmin) + ", " + Formatting.RoundMeta(odru[i], absmin) + ")", xAxisCanvas + xExtCanvas + 10, yc + txh / 2, StringAlignment.Near);
                        }
                        else
                        {
                            DrawStringLabel(title[i], xAxisCanvas - 15, yc + txh / 2, StringAlignment.Far);
                            DrawStringLabel("* (excluded)", xAxisCanvas + xExtCanvas + 10, yc + txh / 2, StringAlignment.Near);
                        }
                    }

                    if (DataMinX <= 0)
                    {
                        //  no effect marker
                        XM = offx;
                        DrawLine(tenPenTrue, XM, yt, XM, yAxisCanvas - 12);
                        DrawStringLabel("  0  ", XM, yAxisCanvas - 12, StringAlignment.Center);
                    }

                    if (pbias == 1)
                    {
                        double save_yc = yc;
                        yctr = 0.5 / divy * yExtCanvas;
                        ytop = 1 / divy * yExtCanvas;
                        XM = ToCanvasX(rmh);
                        XL = ToCanvasX(ll);
                        XR = ToCanvasX(ul);
                        Y2 = (ytop - yctr) / 1.5;
                        yc = offy + yctr;
                        yt = offy + yctr + Y2;
                        // yb = offy + yctr - Y2; 
                        DrawDiamond(tenPenTrue, XM, yc, Y2 * 2, false);
                        DrawLine(tenPenTrue, XR, yc, XL, yc);
                        // pooled effect marker
                        DrawLine(tenPenFalse, XM, save_yc, XM, yt);
                        // pool label
                        DrawStringLabel(combo_ti(cap), xAxisCanvas - 15, yc + txh / 2, StringAlignment.Far);
                        DrawStringLabel(Formatting.RoundMeta(rmh, absmin) + " (" + Formatting.RoundMeta(ll, absmin) + ", " + Formatting.RoundMeta(ul, absmin) + ")", xAxisCanvas + xExtCanvas + 10, yc + txh / 2, StringAlignment.Near);
                        // x axis text
                        DrawStringLabel(qid + " (" + Formatting.XRound(cco * 100, 1) + "% confidence interval" + ")", xAxisCanvas + xExtCanvas / 2, 50, StringAlignment.Center);
                    }
                }
            }

            ifault = false;
        }

        public string PlotEffectAndReturnRtf(ITemplateHost host, int k, double[] cn, double[] En, string[] title, double rmh, double ll, double ul, double cco, double[] odr, double[] odrl, double[] odru, string cap, int pbias, string qid)
        {
            using (MemoryStream metaStream = new MemoryStream())
            {
                StartMetafile(metaStream);
                PlotEffect(host, k, cn, En, title, rmh, ll, ul, cco, odr, odrl, odru, cap, pbias, qid);
                EndMetafile();
                return host.ImageStreamToRtf(metaStream);
            }
        }

        private void PlotEffect(ITemplateHost host, int k, double[] cn, double[] En, string[] title, double rmh, double ll, double ul, double cco, double[] odr, double[] odrl, double[] odru, string cap, int pbias, string qid)
        {
            double aint = 0; double amin = 0;
            double xtra = 0;
            int i; double xm; double yctr;
            double ytop; double xl; double xr; double y2; double yc = 0; double yt = 0;
            string lab;

            if (k > 10)
            {
                scaleYAxis = 1 + (k - 10) / 20;
                if (scaleYAxis > 5)
                {
                    scaleYAxis = 5;
                }
                metaH = scaleYAxis * 800;
            }
            else
            {
                scaleYAxis = 1;
                metaH = 800;
            }
            double[] gn = new double[k + 1 /* for VB to C# conversion */ ];
            int kok = 0;
            double ormax = double.NegativeInfinity;
            double ormin = double.PositiveInfinity;
            double orumax = double.NegativeInfinity;
            double orlmin = double.PositiveInfinity;
            double max_gn = double.NegativeInfinity;
            for (i = 1; i <= k; i++)
            {
                gn[i] = cn[i] + En[i];
                if (gn[i] > max_gn)
                {
                    max_gn = gn[i];
                }
                if (odr[i] != Constant.MISSING)
                {
                    kok = kok + 1;
                    if (odr[i] > ormax)
                    {
                        ormax = odr[i];
                    }
                    if (odr[i] < ormin)
                    {
                        ormin = odr[i];
                    }
                    if (odrl[i] < orlmin)
                    {
                        orlmin = odrl[i];
                    }
                    if (odru[i] > orumax)
                    {
                        orumax = odru[i];
                    }
                }
            }

            DataMaxX = ormax;
            DataMinX = ormin;
            if (DataMaxX < rmh)
            {
                DataMaxX = rmh;
            }
            if (DataMaxX < ul && ul != Constant.MISSING)
            {
                DataMaxX = ul;
            }
            if (DataMaxX < orumax && orumax != Constant.MISSING)
            {
                DataMaxX = orumax;
            }
            if (DataMinX > rmh)
            {
                DataMinX = rmh;
            }
            if (DataMinX > ll && ll != Constant.MISSING)
            {
                DataMinX = ll;
            }
            if (DataMinX > orlmin && orlmin != Constant.MISSING)
            {
                DataMinX = orlmin;
            }

            AxisScaler.Q_Axis(ref dataMinX, ref dataMaxX, out xDiv, ref amin, ref aint, out minorTicsPerMajorTic, ScaleType.Linear);
            DataMinX = amin;
            DataMaxX = amin + xDiv * aint;

            DefaultAxes();
            for (i = 1; i <= k; i++)
            {
                if (odr[i] != Constant.MISSING)
                {
                    double w = canvas.MeasureString(title[i], titleFont).Width + 30;
                    if (w > xtra + xAxisCanvas)
                    {
                        xtra = w - xAxisCanvas - 5;
                    }
                }
            }
            DrawAxes(cap, new Axis(null, AxisMode.LineOnly, 0, ScaleType.NotSet), new Axis(null, AxisMode.None, xtra, ScaleType.NotSet), false, true, false);

            divx = DataMaxX - DataMinX;
            offx = -(DataMinX / divx * xExtCanvas) + xAxisCanvas;
            divy = kok + pbias;
            offy = yAxisCanvas;

            using (Pen tenPenTrue = GetPen(_markerTypes[10], true))
            {
                string msk = GetAxisMask(aint, amin, xDiv, minorTicsPerMajorTic);
                for (i = 0; i <= xDiv; i++)
                {
                    xm = ToCanvasX(amin + aint * i);
                    if ((i % minorTicsPerMajorTic) != 0)
                    {
                        DrawLine(tenPenTrue, xm, yAxisCanvas - 7, xm, yAxisCanvas);
                    }
                    else
                    {
                        lab = (amin + i * aint).ToString(msk);
                        if (double.Parse(lab) != 0)
                        {
                            DrawStringLabel(lab, xm, yAxisCanvas - 12, StringAlignment.Center);
                            DrawLine(tenPenTrue, xm, yAxisCanvas - 12, xm, yAxisCanvas);
                        }
                    }
                }

                int r = 0;
                double txh = canvas.MeasureString(title[1], labelFont).Height;
                for (i = k; i >= 1; i--)
                {
                    if (odr[i] != Constant.MISSING)
                    {
                        r = r + 1;
                        yctr = (r + pbias - 0.5) / divy * yExtCanvas;
                        ytop = (r + pbias) / divy * yExtCanvas;
                        xm = ToCanvasX(odr[i]);
                        xl = ToCanvasX(odrl[i]);
                        xr = ToCanvasX(odru[i]);
                        y2 = (ytop - yctr) / 1.5;
                        yc = offy + yctr;
                        yt = offy + yctr + y2;
                        double yb = offy + yctr - y2;
                        // ytop = yctr + ( ytop - yctr ) * 0.1 + ( ytop - yctr ) * 0.9 * ( gn[ i ] / max_gn ); 
                        // ytop = yctr + ( ytop - yctr ) * 0.8; 
                        // CI line
                        DrawLine(tenPenTrue, xl, yc, xr, yc);
                        // Weight blob
                        DrawSquare(tenPenTrue, xm, yc, (5 + Math.Abs(yt - yb) * (gn[i] / max_gn)) * 0.7, true);
                        DrawStringLabel(title[i], xAxisCanvas - 15, yc + txh / 2, StringAlignment.Far);
                    }
                }

                if (DataMinX <= 0)
                {
                    xm = offx;
                    DrawLine(tenPenTrue, xm, yt, xm, yAxisCanvas - 12);
                    DrawStringLabel("  0  ", xm, yAxisCanvas - 12, StringAlignment.Center);
                }

                if (pbias == 1)
                {
                    double save_yc = yc;
                    yctr = 0.5 / divy * yExtCanvas;
                    ytop = 1 / divy * yExtCanvas;
                    xm = ToCanvasX(rmh);
                    xl = ToCanvasX(ll);
                    xr = ToCanvasX(ul);
                    y2 = (ytop - yctr) / 1.5;
                    yc = offy + yctr;
                    yt = offy + yctr + y2;
                    // yb = offy + yctr - y2; 
                    DrawDiamond(tenPenTrue, xm, yc, y2 * 2, false);
                    DrawLine(tenPenTrue, xr, yc, xl, yc);
                    // pooled effect marker
                    using (Pen tenPenFalse = GetPen(_markerTypes[10], false))
                    {
                        DrawLine(tenPenFalse, xm, save_yc, xm, yt);
                    }
                    lab = "pooled " + qid + " = " + host.RoundU(rmh) + "  (" + Formatting.XRound(cco * 100, 1) + "% CI = " + host.RoundU(ll) + " to " + host.RoundU(ul) + ")";
                    string xlab = cap.IndexOf("fixed", StringComparison.Ordinal) + 1 != 0 ? "" : "DL ";
                    //  If hSS <> -99 Then Lab = xlab & Lab
                    lab = xlab + lab;
                    DrawStringLabel(lab, xAxisCanvas + xExtCanvas / 2, 50, StringAlignment.Center);
                }
            }
        }

        public string PlotCPAndReturnRtf(ITemplateHost host, int k, string[] title, double[] odr, double[] odrl, double[] odru, double[] gn, int[] pg, string cap, string qid, Transformation xform)
        {
            using (MemoryStream metaStream = new MemoryStream())
            {
                StartMetafile(metaStream);
                Plot_CP(host, k + 1, title, odr, odrl, odru, gn, pg, cap, qid, xform);
                EndMetafile();
                return host.ImageStreamToRtf(metaStream);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="host"></param>
        /// <param name="k"></param>
        /// <param name="title"></param>
        /// <param name="odr"></param>
        /// <param name="odrl"></param>
        /// <param name="odru"></param>
        /// <param name="gn"></param>
        /// <param name="pg">0, 1 or -1</param>
        /// <param name="cap"></param>
        /// <param name="qid"></param>
        /// <param name="xform"></param>
        private void Plot_CP(ITemplateHost host, int k, string[] title, double[] odr, double[] odrl, double[] odru, double[] gn, int[] pg, string cap, string qid, Transformation xform)
        {
            if (k > 10)
            {
                scaleYAxis = 1 + (k - 10) / 20;
                if (scaleYAxis > 5)
                {
                    scaleYAxis = 5;
                }
                metaH = scaleYAxis * 800;
            }
            else
            {
                scaleYAxis = 1;
                metaH = 800;
            }

            int kok = 0;
            double ormax = double.NegativeInfinity;
            double ormin = double.PositiveInfinity;
            double orumax = double.NegativeInfinity;
            double orlmin = double.PositiveInfinity;
            double max_gn = double.NegativeInfinity;

            switch (xform)
            {
                case Transformation.Log:
                    {
                        for (int i = 1; i <= k; i++)
                        {
                            if (pg[i] == 0)
                            {
                                if (gn[i] != Constant.MISSING & gn[i] > max_gn)
                                {
                                    max_gn = gn[i];
                                }
                            }
                            if (odr[i] != Constant.MISSING)
                            {
                                kok += 1;
                                if (odr[i] > ormax)
                                {
                                    ormax = odr[i];
                                }
                                if (odr[i] < ormin & odr[i] > 0)
                                {
                                    ormin = odr[i];
                                }
                                if (odrl[i] > odru[i])
                                {
                                    double tmp = odrl[i];
                                    odrl[i] = odru[i];
                                    odru[i] = tmp;
                                }
                                if (odrl[i] < orlmin & odrl[i] > 0)
                                {
                                    orlmin = odrl[i];
                                }
                                if (odru[i] > orumax)
                                {
                                    orumax = odru[i];
                                }
                            }
                        }
                    } break;
                case Transformation.Z:
                    {
                        for (int i = 1; i <= k; i++)
                        {
                            if (pg[i] == 0)
                            {
                                if (gn[i] != Constant.MISSING & gn[i] > max_gn)
                                {
                                    max_gn = gn[i];
                                }
                            }
                            if (odr[i] != Constant.MISSING)
                            {
                                kok += 1;
                                if (odr[i] > ormax)
                                {
                                    ormax = odr[i];
                                }
                                if (odr[i] < ormin && odr[i] > 0)
                                {
                                    ormin = odr[i];
                                }
                                if (odrl[i] > odru[i])
                                {
                                    double tmp = odrl[i];
                                    odrl[i] = odru[i];
                                    odru[i] = tmp;
                                }
                                if (odrl[i] < orlmin && odrl[i] > 0)
                                {
                                    orlmin = odrl[i];
                                }
                                if (odru[i] > orumax)
                                {
                                    orumax = odru[i];
                                }
                            }
                        }
                    } break;
                case Transformation.None:
                    for (int i = 1; i <= k; i++)
                    {
                        if (pg[i] == 0)
                        {
                            if (gn[i] != Constant.MISSING & gn[i] > max_gn)
                            {
                                max_gn = gn[i];
                            }
                        }
                        if (odr[i] != Constant.MISSING)
                        {
                            kok += 1;
                            if (odr[i] > ormax)
                            {
                                ormax = odr[i];
                            }
                            if (odr[i] < ormin)
                            {
                                ormin = odr[i];
                            }
                            if (odrl[i] > odru[i])
                            {
                                double tmp = odrl[i];
                                odrl[i] = odru[i];
                                odru[i] = tmp;
                            }
                            if (odrl[i] < orlmin)
                            {
                                orlmin = odrl[i];
                            }
                            if (odru[i] > orumax)
                            {
                                orumax = odru[i];
                            }
                        }
                    }
                    break;
            }

            double absmin = Constant.MISSING;
            for (int i = 1; i <= k; i++)
            {
                if (Math.Abs(odr[i]) < absmin & odr[i] != 0.0)
                {
                    absmin = Math.Abs(odr[i]);
                }
                if (Math.Abs(odrl[i]) < absmin & odrl[i] != 0.0)
                {
                    absmin = Math.Abs(odrl[i]);
                }
                if (Math.Abs(odru[i]) < absmin & odru[i] != 0.0)
                {
                    absmin = Math.Abs(odru[i]);
                }
            }

            DataMaxX = ormax;
            if (DataMaxX < orumax & orumax != Constant.MISSING)
            {
                DataMaxX = orumax;
            }
            DataMinX = ormin;
            if (DataMinX > orlmin & orlmin != Constant.MISSING)
            {
                DataMinX = orlmin;
            }

            DefaultAxes();
            double rgap = 0;
            double xtra = 0;
            // allow room for right hand labels of effect and CI
            double w;
            for (int i = 1; i <= k; i++)
            {
                if (odr[i] != Constant.MISSING)
                {
                    w = canvas.MeasureString(title[i], labelFont).Width + 30;
                    if (w > xtra + xAxisCanvas)
                    {
                        xtra = w - xAxisCanvas - 5;
                    }
                    w = canvas.MeasureString(Formatting.RoundMeta(odr[i], absmin) + " (" + Formatting.RoundMeta(odrl[i], absmin) + ", " + Formatting.RoundMeta(odru[i], absmin) + ")", labelFont).Width;
                    if (w > rgap)
                    {
                        rgap = w;
                    }
                }
            }
            w = canvas.MeasureString(combo_ti(cap), labelFont).Width + 30;
            if (w > xtra + xAxisCanvas)
            {
                xtra = w - xAxisCanvas - 5;
            }
            xExtCanvas = 940 - rgap;

            double realamin = 0;
            double realamax = 0;

            int tics = 0; double[] tic = null;
            double amin = 0;
            double aint = 0;
            AxisScaler.Q_Axis(ref dataMinX, ref dataMaxX, out xDiv, ref amin, ref aint, out minorTicsPerMajorTic, ScaleType.Linear);
            switch (xform)
            {
                case Transformation.Log:
                    //  TODO: Use a proper log scale
                    tics = 15;
                    tic = new double[tics + 1 /* VB to C# conversion */ ];
                    tic[1] = 0.00000001;
                    tic[2] = 0.00001;
                    tic[3] = 0.001;
                    tic[4] = 0.01;
                    tic[5] = 0.1;
                    tic[6] = 0.2;
                    tic[7] = 0.5;
                    tic[8] = 1;
                    tic[9] = 2;
                    tic[10] = 5;
                    tic[11] = 10;
                    tic[12] = 100;
                    tic[13] = 1000;
                    tic[14] = 100000;
                    tic[15] = 100000000;
                    realamin = DataMinX;
                    for (int i = 2; i <= tics; i++)
                    {
                        if (tic[i] > DataMinX)
                        {
                            realamin = tic[i - 1];
                            break;
                        }
                    }
                    realamax = DataMaxX;
                    for (int i = tics - 1; i >= 1; i--)
                    {
                        if (tic[i] < DataMaxX)
                        {
                            realamax = tic[i + 1];
                            break;
                        }
                    }
                    DataMinX = Math.Log(realamin);
                    DataMaxX = Math.Log(realamax);
                    DrawAxes(cap, new Axis(null, AxisMode.LineOnly, 0, ScaleType.NotSet), new Axis(null, AxisMode.None, xtra, ScaleType.NotSet), false, false, false);

                    break;
                default:
                    if (cap.IndexOf("Correlation (", StringComparison.Ordinal) >= 0)
                    {
                        DataMinX = DataMinX >= 0.0 ? 0.0 : -1.0;
                        DataMaxX = DataMaxX <= 0.0 ? 0.0 : 1.0;
                    }
                    DrawAxes(cap, new Axis(null, AxisMode.Scale, 0, ScaleType.Linear), new Axis(null, AxisMode.None, xtra, ScaleType.NotSet), false, false, false);
                    DataMinX = axisXMin;
                    DataMaxX = axisXMax;

                    break;
            }


            divx = DataMaxX - DataMinX;
            offx = -(DataMinX / divx * xExtCanvas) + xAxisCanvas;
            divy = kok;
            offy = yAxisCanvas;

            using (Pen tenPenTrue = GetPen(_markerTypes[10], true))
            {
                using (Pen tenPenFalse = GetPen(_markerTypes[10], false))
                {
                    if (xform == Transformation.Log)
                    {
                        if (null == tic)
                            throw new Exception("Expected tics to be populated for log scale");
                        for (int i = 1; i <= tics; i++)
                        {
                            if (tic[i] >= realamin && tic[i] <= realamax)
                            {
                                double XM = ToCanvasX(Math.Log(tic[i]));
                                string lab;
                                if (tic[i] > 1000 | tic[i] < 0.001)
                                {
                                    lab = tic[i].ToString("E");
                                }
                                else
                                {
                                    lab = tic[i].ToString();
                                }
                                DrawStringLabel(lab, XM, yAxisCanvas - 12, StringAlignment.Center);
                                DrawLine(tenPenTrue, XM, yAxisCanvas - 12, XM, yAxisCanvas);
                            }
                        }
                    }

                    double rmh = -99;
                    int r = 0;
                    double txh = canvas.MeasureString(title[1], labelFont).Height;
                    double botlim = xform == Transformation.Log ? realamin : double.NegativeInfinity;
                    double yt = 0;
                    for (int i = k; i >= 1; i--)
                    {
                        if (odr[i] != Constant.MISSING)
                        {
                            r = r + 1;
                            double yctr = (r - 0.5) / divy * yExtCanvas;
                            double ytop = r / divy * yExtCanvas;
                            double XM = 0;
                            if (odr[i] < botlim)
                            {
                                XM = xAxisCanvas;
                            }
                            else
                            {
                                switch (xform)
                                {
                                    case Transformation.Log:
                                        XM = ToCanvasX(Math.Log(odr[i]));
                                        break;
                                    case Transformation.Z:
                                        XM = ToCanvasX(MathDbl.rtoz(odr[i]));
                                        break;
                                    case Transformation.None:
                                        XM = ToCanvasX(odr[i]);
                                        break;
                                }

                            }
                            double XL = 0;
                            if (odrl[i] < botlim)
                            {
                                XL = xAxisCanvas;
                            }
                            else
                            {
                                switch (xform)
                                {
                                    case Transformation.Log:
                                        XL = ToCanvasX(Math.Log(odrl[i]));
                                        break;
                                    case Transformation.Z:
                                        XL = ToCanvasX(MathDbl.rtoz(odrl[i]));
                                        break;
                                    case Transformation.None:
                                        XL = ToCanvasX(Math.Max(odrl[i], -1));
                                        break;
                                }

                            }
                            double XR = 0;
                            switch (xform)
                            {
                                case Transformation.Log:
                                    XR = ToCanvasX(Math.Log(odru[i]));
                                    break;
                                case Transformation.Z:
                                    XR = ToCanvasX(MathDbl.rtoz(odru[i]));
                                    break;
                                case Transformation.None:
                                    XR = ToCanvasX(Math.Min(odru[i], 1));
                                    break;
                            }

                            double Y2 = (ytop - yctr) / 1.5;
                            double yc = offy + yctr;
                            yt = offy + yctr + Y2;
                            double yb = offy + yctr - Y2;
                            //if ( gn[ i ] == Constant.MISSING ) 
                            //{ 
                            //    ytop = yctr + ( ytop - yctr ) * 0.1 + ( ytop - yctr ) * 0.7; 
                            //} 
                            //else 
                            //{ 
                            //    ytop = yctr + ( ytop - yctr ) * 0.1 + ( ytop - yctr ) * 0.9 * ( gn[ i ] / max_gn ); 
                            //} 
                            //ytop = yctr + ( ytop - yctr ) * 0.8; 
                            if (pg[i] == 0)
                            {
                                // CI line
                                DrawLine(tenPenTrue, XL, yc, XR, yc);
                                // Weight blob
                                DrawSquare(tenPenTrue, XM, yc, (5 + Math.Abs(yt - yb) * (gn[i] / max_gn)) * 0.7, true);
                                // Arrow ends if not plottable
                                if ((odrl[i] <= 0 & xform == Transformation.Log) | odrl[i] == Constant.MISSING)
                                {
                                    DrawLine(tenPenTrue, XL + Y2, yc + Y2, XL, yc);
                                    DrawLine(tenPenTrue, XL, yc, XL + Y2, yc - Y2);
                                }
                                if (odru[i] == Constant.MISSING)
                                {
                                    DrawLine(tenPenTrue, XR - Y2, yc + Y2, XR, yc);
                                    DrawLine(tenPenTrue, XR, yc, XR - Y2, yb - Y2);
                                }

                            }
                            else
                            {
                                DrawDiamond(tenPenTrue, XM, yc, Y2 * 2, false);
                                DrawLine(tenPenTrue, XR, yc, XL, yc);
                                if (pg[i] < 0)
                                {
                                    // double ll = odrl[ i ]; 
                                    // double ul = odru[ i ]; 
                                    rmh = odr[i];
                                    // pooled effect marker
                                    DrawLine(tenPenFalse, XM, yt, XM, ToCanvasY(k - 0.5));
                                    // re-set pen so get solid line back in case next is not pooled
                                }

                            }
                            DrawStringLabel(title[i], xAxisCanvas - 15, yc + txh / 2, StringAlignment.Far);
                            DrawStringLabel(Formatting.RoundMeta(odr[i], absmin) + " (" + Formatting.RoundMeta(odrl[i], absmin) + ", " + Formatting.RoundMeta(odru[i], absmin) + ")", xAxisCanvas + xExtCanvas + 10, yc + txh / 2, StringAlignment.Near);
                        }
                    }

                    if (DataMinX <= 0 && xform != Transformation.Z)
                    {
                        // no effect marker
                        double XM = 0;
                        switch (xform)
                        {
                            case Transformation.Log:
                                XM = ToCanvasX(Math.Log(1));
                                break;
                            case Transformation.Z:
                                throw new Exception("Shoudn't be plotting no effect marker with a correlation plot");
                            case Transformation.None:
                                XM = ToCanvasX(0);
                                break;
                        }

                        DrawLine(tenPenTrue, XM, yt, XM, yAxisCanvas);
                    }

                    if (rmh != -99)
                    {
                        string buf = qid;
                        DrawStringLabel(buf, xAxisCanvas + xExtCanvas / 2, 50, StringAlignment.Center);
                    }
                }
            }
        }

        private void PlotPert(double pert, double slope, double yIntercept, int nx, double ms, double sumx, double ssx, bool plotBothLines)
        {
            double xstep = (axisXMax - axisXMin) / 20.0;

            if (pert != 0)
            {
                double lastx1 = 0;
                double lasty1 = 0;

                bool first = true;
                for (double calcx = axisXMin; calcx <= axisXMax; calcx += xstep)
                {
                    double calcy = slope * calcx + yIntercept;
                    double sey = Math.Sqrt(ms * (1.0 + (1.0 / Convert.ToDouble(nx) + Math.Pow((calcx - (sumx / Convert.ToDouble(nx))), 2.0) / ssx)));
                    double pcon = calcy + (sey * pert);
                    double x1 = calcx;
                    double y1 = pcon;
                    if (first)
                    {
                        first = false;
                    }
                    else
                    {
                        if (lasty1 >= axisYMin && lasty1 <= axisYMax && y1 > axisYMin && y1 < axisYMax)
                        {
                            DrawLineInChartCoordinates(grBlack, lastx1, lasty1, x1, y1);
                        }
                    }
                    lasty1 = y1;
                    lastx1 = x1;
                }
                first = true;
                for (double calcx = axisXMin; calcx <= axisXMax; calcx += xstep)
                {
                    double calcy = slope * calcx + yIntercept;
                    double sey = Math.Sqrt(ms * (1.0 + (1.0 / Convert.ToDouble(nx) + Math.Pow((calcx - (sumx / Convert.ToDouble(nx))), 2.0) / ssx)));
                    double ncon = calcy - (sey * pert);
                    double x1 = calcx;
                    double y1 = ncon;
                    if (first)
                    {
                        first = false;
                    }
                    else
                    {
                        if (lasty1 >= axisYMin && lasty1 <= axisYMax && y1 > axisYMin && y1 < axisYMax)
                        {
                            DrawLineInChartCoordinates(grBlack, lastx1, lasty1, x1, y1);
                        }
                    }
                    lasty1 = y1;
                    lastx1 = x1;
                }

                first = true;
                for (double calcx = axisXMin; calcx <= axisXMax; calcx += xstep)
                {
                    double calcy = slope * calcx + yIntercept;
                    double sey = Math.Sqrt(ms * (1.0 + (1.0 / Convert.ToDouble(nx) + Math.Pow((calcx - (sumx / Convert.ToDouble(nx))), 2.0) / ssx)));
                    double pcon = calcy + (sey);
                    double x1 = calcx;
                    double y1 = pcon;
                    if (first)
                    {
                        first = false;
                    }
                    else
                    {
                        if (lasty1 >= axisYMin && lasty1 <= axisYMax && y1 > axisYMin && y1 < axisYMax)
                        {
                            DrawLineInChartCoordinates(grMagenta, lastx1, lasty1, x1, y1);
                        }
                    }
                    lasty1 = y1;
                    lastx1 = x1;
                }
                first = true;
                for (double calcx = axisXMin; calcx <= axisXMax; calcx += xstep)
                {
                    double calcy = slope * calcx + yIntercept;
                    double sey = Math.Sqrt(ms * (1.0 + (1.0 / Convert.ToDouble(nx) + Math.Pow((calcx - (sumx / Convert.ToDouble(nx))), 2.0) / ssx)));
                    double ncon = calcy - (sey);
                    double x1 = calcx;
                    double y1 = ncon;
                    if (first)
                    {
                        first = false;
                    }
                    else
                    {
                        if (lasty1 >= axisYMin && lasty1 <= axisYMax && y1 > axisYMin && y1 < axisYMax)
                        {
                            DrawLineInChartCoordinates(grMagenta, lastx1, lasty1, x1, y1);
                        }
                    }
                    lasty1 = y1;
                    lastx1 = x1;
                }
            }
        }

        ///  <summary>
        ///  Copied from meta due to mutual dependency issues
        ///  </summary>
        ///  <param name="o"></param>
        ///  <param name="i"></param>
        ///  <returns></returns>
        ///  <remarks></remarks>
        private bool include_table(double[,] o, int i)
        {
            return !((o[i, 1] == 0.0 & o[i, 2] == 0.0) | (o[i, 3] == 0.0 & o[i, 4] == 0.0));
        }

        public string AsAsciiRTF
        {
            get
            {
                if (!(IsAscii))
                {
                    throw new InvalidOperationException("Trying to get ASCII string for a non-ASCII chart");
                }
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                for (int i = shTx.GetUpperBound(0); i >= shTx.GetLowerBound(0); i--)
                {
                    sb.Append(shTx[i]);
                    sb.Append(Formatting.RTFCRLF);
                }
                return sb.ToString();
            }
        }

        private void WriteAsciiYX(int y, int x, string text)
        {
            shTx[y] = ReplaceAt(shTx[y], x, text);
        }

        private void WriteAsciiYX(int y, int x, char c)
        {
            shTx[y] = ReplaceAt(shTx[y], x, c);
        }

        private string ReplaceAt(string buffer, int x, string text)
        {
            return buffer.Substring(0, 0) + text + buffer.Substring(x + text.Length);
        }

        private string ReplaceAt(string buffer, int x, char c)
        {
            return buffer.Substring(0, 0) + c + buffer.Substring(x + 1);
        }


        // TRANSMISSINGCOMMENT: Method SetBox0To1
        public void SetBox0To1()
        {
            axisXMax = 1.0;
            axisYMax = 1.0;
            axisXMin = 0.0;
            axisYMin = 0.0;
            boxAxes = true;
        }

        private void SetFontsAndThicknessesFromOptions(GenericOptions o)
        {
            if (o.UsesAxisLabelFontDescriptor && !(string.IsNullOrEmpty(o.AxisLabelFontDescriptor)))
            {
                axisLabelFont = FontFromSaveString(o.AxisLabelFontDescriptor);
            }
            if (o.UsesAxisTitleFontDescriptor && !(string.IsNullOrEmpty(o.AxisTitleFontDescriptor)))
            {
                axisTitleFont = FontFromSaveString(o.AxisTitleFontDescriptor);
            }
            if (o.UsesLegendFontDescriptor && !(string.IsNullOrEmpty(o.LegendFontDescriptor)))
            {
                legendFont = FontFromSaveString(o.LegendFontDescriptor);
            }
            if (o.UsesTitleFontDescriptor && !(string.IsNullOrEmpty(o.TitleFontDescriptor)))
            {
                titleFont = FontFromSaveString(o.TitleFontDescriptor);
            }

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

        public Pen GetPen(MarkerType mt, bool IgnoreStyle)
        {
            Pen p;
            if (HasChartOptions && definition.ChartOptions.UseColour)
            {
                p = new Pen(mt.Color, mt.Width);
            }
            else
            {
                p = new Pen(grBlack, mt.Width);
            }
            if (!(IgnoreStyle))
            {
                p.DashStyle = mt.Style;
            }
            return p;
        }

        private bool HasScaleParameters
        {
            get
            {
                return (definition != null) && definition.HasScaleParameters;
            }
        }

        private bool HasChartOptions
        {
            get
            {
                return (definition != null) && (definition.ChartOptions != null);
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
                    clone.IsFilled = forcedIsFilled;
                if (shouldForceFillStyle)
                    clone.FillStyle = forcedFillStyle;
                markerTypes.Add(clone);
            }
            return markerTypes;
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
            if (null != blackBrush)
            {
                blackBrush.Dispose();
                blackBrush = null;
            }
            if (null != metaFile)
            {
                metaFile.Dispose();
                metaFile = null;
            }
            if (null != mostRecentPen)
            {
                mostRecentPen.Dispose();
                mostRecentPen = null;
            }
        }
    }
}
