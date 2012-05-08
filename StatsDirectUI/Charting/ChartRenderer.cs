using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Security.Permissions;
using System.Drawing;

using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.Templates;
using StatsDirect.Utilities;

namespace StatsDirect.Charting
{
    ///  <summary>
    ///  Converts a chart definition into an ASCII or metafile rendering of that definition.
    ///  </summary>
    public class ChartRenderer
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

        private Font AxisLabelFont;
        private Font AxisTitleFont;
        private float AxisLineThickness;
        private Pen AxisPen;
        private Brush AxisBrush;
        private const double AxisLittleTick = 4;
        private const double AxisBigTick = 7;
        private Font TitleFont;
        private Font LegendFont;
        private bool BoxAxes = defaultBoxAxes;

        private Font LabelFont;

        private Graphics Canvas;
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
        private double XInt;
        private double YInt;

        private double XAxisCanvas;
        private double XExtCanvas;
        private int XDiv;
        private double YAxisCanvas;
        private double YExtCanvas;
        private int YDiv;

        private double divx;
        private double offx;
        private double divy;
        private double offy;

        /// <summary>How many minor tics per major tic on the axis?</summary>
        private int MinorTicsPerMajorTic;
        private double ScaleYAxis = 1.0;
        private double ScaleXAxis = 1.0;
        private const double DEFAULT_METAH = 800;
        private const double DEFAULT_METAW = 1132;
        private const double DEFAULT_Y_GAP = 80;
        private double MetaH = DEFAULT_METAH;
        private double MetaW = DEFAULT_METAW;
        private const double SCALE_FACTOR = 1.4;
        private const int LABEL_TO_AXIS_LABEL_GAP = 20;

        //  Box and Whisker constants
        private const double BOXWHISKER_WHISKER_END_LENGTH = 9;
        private const double BOXWHISKER_OUTLIER_RADIUS = 4;
        private const double BOXWHISKER_BOX_FRACTION_OF_SPACE = 0.667;
        private const double BOXWHISKER_WHISKER_FRACTION_OF_BOX = 0.333;

        private string[] SH_TX;

        private static bool AreSharedValuesInitialised;
        private Brush BlackBrush;

        private const int ASCII_Ytxt = 3;
        private const int ASCII_XTxt = 15;

        ///  <summary>
        ///  A few methods take a colour, not a pen.  This caches the most recent pen used by those methods, so that it can be re-used rather than regenerated each time.
        ///  </summary>
        private Pen mostRecentPen;

        private static MarkerType[] _MarkerTypes;

        // TRANSMISSINGCOMMENT: Property DefaultRequestScaleLimits
        public static bool DefaultRequestScaleLimits
        {
            get
            {
                return false;
            }
        }

        // TRANSMISSINGCOMMENT: Property DefaultBoxAxes
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

        // TRANSMISSINGCOMMENT: Property DefaultAxisLabelFont
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

        // TRANSMISSINGCOMMENT: Property DefaultSeriesLabelFont
        public static Font DefaultSeriesLabelFont
        {
            get
            {
                return DefaultAxisLabelFont;
            }
        }

        // TRANSMISSINGCOMMENT: Property DefaultAxisTitleFont
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

        // TRANSMISSINGCOMMENT: Property DefaultLabelFont
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

        // TRANSMISSINGCOMMENT: Property DefaultLegendFont
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

        // TRANSMISSINGCOMMENT: Property DefaultTitleFont
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

        // TRANSMISSINGCOMMENT: Property MarkerTypes
        public static MarkerType[] MarkerTypes
        {
            get
            {
                if (!(AreSharedValuesInitialised))
                {
                    InitSharedValues();
                }
                return _MarkerTypes;
            }
        }

        // TRANSMISSINGCOMMENT: Method InitSharedValues
        private static void InitSharedValues()
        {
            AreSharedValuesInitialised = true; //  Set early to prevent recursively trying to initialise properties when saving them
            InitMarkerTypes();
            InitFonts();
            InitFlags();
        }


        // TRANSMISSINGCOMMENT: Property DataMinX
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

        // TRANSMISSINGCOMMENT: Property DataMaxX
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

        // TRANSMISSINGCOMMENT: Property DataMinY
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

        // TRANSMISSINGCOMMENT: Property DataMaxY
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
            if (Definition != null)
            {
                dataMinX = Definition.DataMinX;
                dataMaxX = Definition.DataMaxX;
                dataMinY = Definition.DataMinY;
                dataMaxY = Definition.DataMaxY;
            }
        }

        // TRANSMISSINGCOMMENT: Method GetScaleParameters
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


        ///  <summary>
        ///  Plot a chart.
        ///  </summary>
        ///  <param name="OutputStream"></param>
        ///  <param name="host"></param>
        ///  <returns>Any output parameters created as side-effects of the plotting</returns>
        ///  <remarks>Postcondition: Another plot can be called on the same chart object and give the same results.  This is required for previewing.</remarks>
        public ParameterBag Plot(Stream OutputStream, ITemplateHost host)
        {
            switch (definition.ChartType)
            {
                case ChartType.AgreementPair:
                    return PlotAgreementPair(OutputStream);
                case ChartType.Bar:
                case ChartType.StackedBar:
                case ChartType.StackedBar100Percent:
                    return PlotBar(OutputStream);
                case ChartType.BoxWhisker:
                    return PlotBoxWhisker(OutputStream);
                case ChartType.Control:
                    return PlotControl(OutputStream);
                case ChartType.ErrorBar:
                    return PlotErrorBar(OutputStream);
                case ChartType.Forest:
                    return PlotForest(OutputStream);
                case ChartType.Gini:
                    return PlotGini(OutputStream);
                case ChartType.Histogram:
                    return PlotHistogram(OutputStream);
                case ChartType.Ladder:
                    return PlotLadder(OutputStream);
                case ChartType.LineXY:
                    return PlotScatter(OutputStream, true);
                case ChartType.LinearRegression:
                    return PlotLinearRegression(OutputStream);
                case ChartType.Normal:
                    return PlotNormal(OutputStream, host);
                case ChartType.Pyramid:
                    return PlotPyramid(OutputStream);
                case ChartType.ROC:
                    return PlotROC(OutputStream, host);
                case ChartType.ScatterXY:
                    return PlotScatter(OutputStream, false);
                case ChartType.Spread:
                    return PlotSpread(OutputStream, host);
                case ChartType.Survival:
                    return PlotSurvival(OutputStream);
                default:
                    throw new NotImplementedException("That chart type is not yet implemented");
            }

        }


        // TRANSMISSINGCOMMENT: Method InitFlags
        private static void InitFlags()
        {
            defaultBoxAxes = Settings1.Default.BoxAxes;
            defaultAllBlack = Settings1.Default.BlackAndWhite;
        }


        // TRANSMISSINGCOMMENT: Method InitFonts
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


        // TRANSMISSINGCOMMENT: Method FontFromSaveString
        public static Font FontFromSaveString(string Descriptor)
        {
            string[] fontStrings = Descriptor.Split(';');
            string fontString = fontStrings[0];
            FontStyle style = ((FontStyle)(int.Parse(fontStrings[1])));
            float sz = float.Parse(fontStrings[2]);
            return new Font(fontString, sz, style);
        }


        // TRANSMISSINGCOMMENT: Method SaveStringFromFont
        public static string SaveStringFromFont(Font f)
        {
            return f.FontFamily.Name + ";" + (Convert.ToInt32(f.Style)).ToString() + ";" + f.Size.ToString();
        }


        // TRANSMISSINGCOMMENT: Method InitFirstFonts
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
            _MarkerTypes = new MarkerType[11];
            for (int i = _MarkerTypes.GetLowerBound(0); i <= _MarkerTypes.GetUpperBound(0); i++)
            {
                _MarkerTypes[i] = new MarkerType();
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
                    _MarkerTypes[i].Color = col;
                    _MarkerTypes[i].IsFilled = isFilled;
                    _MarkerTypes[i].MarkerSize = markerSize;
                    _MarkerTypes[i].Shape = shape;
                    _MarkerTypes[i].Style = style;
                    _MarkerTypes[i].Width = width;
                }

                // fixed style
                _MarkerTypes[10].Shape = MarkerShape.Circle;
                _MarkerTypes[10].Color = Color.Black;
                _MarkerTypes[10].Width = 1;
                _MarkerTypes[10].Style = System.Drawing.Drawing2D.DashStyle.Dash;
                _MarkerTypes[10].IsFilled = false;
                _MarkerTypes[10].MarkerSize = 6;
            }
        }


        // TRANSMISSINGCOMMENT: Method SaveFlags
        public static void SaveFlags()
        {
            Settings1.Default.BlackAndWhite = defaultAllBlack;
            Settings1.Default.BoxAxes = defaultBoxAxes;

            SaveSettings(Settings1.Default);
        }


        // TRANSMISSINGCOMMENT: Method SaveFonts
        public static void SaveFonts()
        {
            Settings1.Default.LabelFont = SaveStringFromFont(DefaultLabelFont);
            Settings1.Default.TitleFont = SaveStringFromFont(DefaultTitleFont);

            SaveSettings(Settings1.Default);
        }


        // TRANSMISSINGCOMMENT: Method SaveMarkerTypes
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
                savedSettings.Append(Convert.ToInt32(_MarkerTypes[i].Shape).ToString());
                savedSettings.Append(";");

                //  Colour
                Color col = _MarkerTypes[i].Color;
                savedSettings.Append(col.R.ToString());
                savedSettings.Append(",");
                savedSettings.Append(col.G.ToString());
                savedSettings.Append(",");
                savedSettings.Append(col.B.ToString());
                savedSettings.Append(";");
                savedSettings.Append(_MarkerTypes[i].Width.ToString());
                savedSettings.Append(";");

                //  Line style
                savedSettings.Append(Convert.ToInt32(_MarkerTypes[i].Style).ToString());
                savedSettings.Append(";");

                //  Filled (1/0)
                savedSettings.Append(_MarkerTypes[i].IsFilled ? "1" : "0");
                savedSettings.Append(";");

                //  Marker size
                savedSettings.Append(_MarkerTypes[i].MarkerSize.ToString());
            }
            Settings1.Default.Markers = savedSettings.ToString();
            SaveSettings(Settings1.Default);
        }


        // TRANSMISSINGCOMMENT: Method InitFirstMarkerTypes
        private static void InitFirstMarkerTypes()
        {
            _MarkerTypes[0].Shape = MarkerShape.Circle;
            _MarkerTypes[0].Color = Color.FromArgb(64, 105, 156); // Color.Red
            _MarkerTypes[0].Style = System.Drawing.Drawing2D.DashStyle.Solid;

            _MarkerTypes[1].Shape = MarkerShape.Square;
            _MarkerTypes[1].Color = Color.FromArgb(158, 65, 62); // Color.Green
            _MarkerTypes[1].Style = System.Drawing.Drawing2D.DashStyle.Dash;

            _MarkerTypes[2].Shape = MarkerShape.Triangle;
            _MarkerTypes[2].Color = Color.FromArgb(127, 154, 72); // Color.Blue
            _MarkerTypes[2].Style = System.Drawing.Drawing2D.DashStyle.Dot;

            _MarkerTypes[3].Shape = MarkerShape.Plus;
            _MarkerTypes[3].Color = Color.FromArgb(105, 81, 133); // Color.Black
            _MarkerTypes[3].Style = System.Drawing.Drawing2D.DashStyle.DashDot;

            _MarkerTypes[4].Shape = MarkerShape.Cross;
            _MarkerTypes[4].Color = Color.FromArgb(60, 141, 163); // Color.Cyan
            _MarkerTypes[4].Style = System.Drawing.Drawing2D.DashStyle.Solid;

            _MarkerTypes[5].Shape = MarkerShape.CircleLine;
            _MarkerTypes[5].Color = Color.FromArgb(204, 123, 56); // Color.Magenta
            _MarkerTypes[5].Style = System.Drawing.Drawing2D.DashStyle.Dash;

            _MarkerTypes[6].Shape = MarkerShape.SquareLine;
            _MarkerTypes[6].Color = Color.FromArgb(79, 129, 189); // Color.Yellow
            _MarkerTypes[6].Style = System.Drawing.Drawing2D.DashStyle.Dot;

            _MarkerTypes[7].Shape = MarkerShape.SquareCross;
            _MarkerTypes[7].Color = Color.FromArgb(192, 80, 77); // Color.Red
            _MarkerTypes[7].Style = System.Drawing.Drawing2D.DashStyle.DashDot;

            _MarkerTypes[8].Shape = MarkerShape.Circle;
            _MarkerTypes[8].Color = Color.FromArgb(155, 187, 89); // Color.Green
            _MarkerTypes[8].Style = System.Drawing.Drawing2D.DashStyle.Solid;

            _MarkerTypes[9].Shape = MarkerShape.Square;
            _MarkerTypes[9].Color = Color.FromArgb(128, 100, 162); //  Color.Cyan
            _MarkerTypes[9].Style = System.Drawing.Drawing2D.DashStyle.Dash;

            // fixed style
            _MarkerTypes[10].Shape = MarkerShape.Circle;
            _MarkerTypes[10].Color = Color.Black;
            _MarkerTypes[10].Style = System.Drawing.Drawing2D.DashStyle.Dash;

            foreach (MarkerType mt in _MarkerTypes)
            {
                mt.Width = 1;
                mt.MarkerSize = 6;
            }
            SaveMarkerTypes();
        }


        // TRANSMISSINGCOMMENT: Method SaveSettings
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
            XAxisCanvas = MetaW / 7.55;
            YAxisCanvas = Math.Min(MetaH / 8, DEFAULT_Y_GAP);
            XExtCanvas = MetaW / 1.25 * ScaleXAxis;
            YExtCanvas = MetaH - Math.Min(MetaH / 4, 2 * DEFAULT_Y_GAP * ScaleYAxis);
        }


        ///  <summary>
        ///  Prepare to plot a metafile chart to the specified stream.
        ///  </summary>
        ///  <remarks></remarks>
        public void StartMetafile(Stream OutputStream, bool ShouldDefaultAxes)
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
            AxisLabelFont = DefaultAxisLabelFont;
            AxisPen = new Pen(grAxis, 1);
            AxisTitleFont = DefaultAxisTitleFont;
            AxisBrush = new SolidBrush(Color.Black);
            LabelFont = DefaultLabelFont;
            LegendFont = DefaultLegendFont;
            TitleFont = DefaultTitleFont;
            BlackBrush = new SolidBrush(Color.Black);

            cachedOutputStream = OutputStream;
            SetupGraphics();
        }

        // TRANSWARNING: Automatically generated because of optional parameter(s) 
        // TRANSMISSINGCOMMENT: Method StartMetafile
        public void StartMetafile(Stream OutputStream)
        {
            StartMetafile(OutputStream, true);
        }


        // TRANSMISSINGCOMMENT: Method SetupGraphics
        private void SetupGraphics()
        {
            if (cachedOutputStream != null)
            {
                //  Create temporary graphics object for metafile creation and get handle to its device context.
                //  Dim newGraphics As Graphics = Graphics.FromImage(New Bitmap(CInt(MetaW), CInt(MetaH), Imaging.PixelFormat.Format32bppArgb))
                Graphics newGraphics = Graphics.FromImage(new Bitmap(1, 1, System.Drawing.Imaging.PixelFormat.Format32bppArgb));
                IntPtr hdc = newGraphics.GetHdc();
                //  Create metafile object to record.
                cachedOutputStream.Position = 0; //  Just in case we're resetting an earlier metafile output
                metaFile = new System.Drawing.Imaging.Metafile(cachedOutputStream, hdc, new RectangleF(0, 0, Convert.ToSingle(MetaW), Convert.ToSingle(MetaH)), System.Drawing.Imaging.MetafileFrameUnit.Pixel, System.Drawing.Imaging.EmfType.EmfPlusDual);
                //  Create graphics object to record metaFile.
                Canvas = Graphics.FromImage(metaFile);
                Canvas.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                //  Release handle to scratch device context.
                newGraphics.ReleaseHdc(hdc);
                //  Dispose of scratch graphics object.
                newGraphics.Dispose();
            }
        }


        ///  <summary>
        ///  Stop plotting a metafile chart and release resources.
        ///  </summary>
        ///  <remarks></remarks>
        public void EndMetafile()
        {
            //  Series are kept in case of redoing a preview.

            if ((Canvas != null))
            {
                Canvas.Dispose();
                Canvas = null;
            }
            if ((metaFile != null))
            {
                metaFile.Dispose();
                metaFile = null;
            }
            if ((AxisLabelFont != null))
            {
                if (AxisLabelFont != DefaultAxisLabelFont)
                {
                    AxisLabelFont.Dispose();
                }
                AxisLabelFont = null;
            }
            if ((AxisTitleFont != null))
            {
                if (AxisTitleFont != DefaultAxisTitleFont)
                {
                    AxisTitleFont.Dispose();
                }
                AxisTitleFont = null;
            }
            if ((AxisPen != null))
            {
                AxisPen.Dispose();
                AxisPen = null;
            }
            if ((AxisBrush != null))
            {
                AxisBrush.Dispose();
                AxisBrush = null;
            }
            if ((TitleFont != null))
            {
                if (TitleFont != DefaultTitleFont)
                {
                    TitleFont.Dispose();
                }
                TitleFont = null;
            }
            if ((LegendFont != null))
            {
                if (LegendFont != DefaultLegendFont)
                {
                    LegendFont.Dispose();
                }
                LegendFont = null;
            }
            if ((LabelFont != null))
            {
                if (LabelFont != DefaultLabelFont)
                {
                    LabelFont.Dispose();
                }
                LabelFont = null;
            }
            if ((BlackBrush != null))
            {
                BlackBrush.Dispose();
                BlackBrush = null;
            }
            if ((mostRecentPen != null))
            {
                mostRecentPen.Dispose();
                mostRecentPen = null;
            }
            cachedOutputStream = null;
        }


        // TRANSMISSINGCOMMENT: Method DrawTitle
        private void DrawTitle(string Title)
        {
            StringFormat TxtFormat = new StringFormat { Alignment = StringAlignment.Center };

            DrawString(Title, TitleFont, Brushes.Black, (XExtCanvas / 2) + XAxisCanvas, YAxisCanvas + YExtCanvas + 60 - TitleFont.Height * 0.5F, TxtFormat);
            TxtFormat.Dispose();
        }


        // TRANSMISSINGCOMMENT: Method DrawString
        private void DrawString(string s, Font font, Brush brush, double x, double y, StringFormat TxtFormat)
        {
            Canvas.DrawString(s, font, brush, Convert.ToSingle(x), Convert.ToSingle(MetaH - y), TxtFormat);
        }


        // TRANSMISSINGCOMMENT: Method DirectionToAngle
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
        ///  <param name="TxtFormat"></param>
        ///  <param name="Direction"></param>
        ///  <remarks></remarks>
        private SizeF DrawStringAtAngle(string s, Font font, Brush brush, double x, double y, StringFormat TxtFormat, LabelDirection Direction)
        {
            //  Work out how to fiddle the text alignment
            if (TxtFormat.LineAlignment == StringAlignment.Center && TxtFormat.Alignment == StringAlignment.Far)
            {
                //  Middle-right: Vertical text needs fiddling, otherwise we're OK.
                if (Direction == LabelDirection.Up)
                {
                    TxtFormat = ((StringFormat)(TxtFormat.Clone()));
                    TxtFormat.LineAlignment = StringAlignment.Far;
                    TxtFormat.Alignment = StringAlignment.Center;
                }
                else if (Direction == LabelDirection.Down)
                {
                    TxtFormat = ((StringFormat)(TxtFormat.Clone()));
                    TxtFormat.LineAlignment = StringAlignment.Near;
                    TxtFormat.Alignment = StringAlignment.Center;
                }
            }
            else if (TxtFormat.LineAlignment == StringAlignment.Near && TxtFormat.Alignment == StringAlignment.Center)
            {
                //  Top-centre: Anything other than across needs fiddling.
                if (Direction == LabelDirection.Down || Direction == LabelDirection.SlopeDown)
                {
                    TxtFormat = ((StringFormat)(TxtFormat.Clone()));
                    TxtFormat.LineAlignment = StringAlignment.Center;
                    TxtFormat.Alignment = StringAlignment.Near;
                }
                else if (Direction == LabelDirection.SlopeUp || Direction == LabelDirection.Up)
                {
                    TxtFormat = ((StringFormat)(TxtFormat.Clone()));
                    TxtFormat.LineAlignment = StringAlignment.Center;
                    TxtFormat.Alignment = StringAlignment.Far;
                }
            }
            float angle = DirectionToAngle(Direction);
            Canvas.TranslateTransform(Convert.ToSingle(x), Convert.ToSingle(MetaH - y));
            Canvas.RotateTransform(angle);
            Canvas.DrawString(s, font, brush, 0, 0, TxtFormat);
            Canvas.ResetTransform();
            SizeF uprightSize = Canvas.MeasureString(s, font);
            SizeF boundingSize = ToBoundingSize(uprightSize, Direction);
            return boundingSize;
        }


        // TRANSMISSINGCOMMENT: Method ToBoundingSize
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


        // TRANSMISSINGCOMMENT: Method DrawXAxisTitle
        public void DrawXAxisTitle(string Title)
        {
            if (!(string.IsNullOrEmpty(Title)))
                DrawXAxisTitle(Title, AxisLabelFont.Height);
        }


        // TRANSMISSINGCOMMENT: Method DrawXAxisTitle
        public void DrawXAxisTitle(string Title, double GapForAxisLabels)
        {
            if (!(string.IsNullOrEmpty(Title)))
            {
                StringFormat TxtFormat = new StringFormat { Alignment = StringAlignment.Center };

                DrawString(Title, AxisTitleFont, Brushes.Black, (XExtCanvas / 2) + XAxisCanvas, YAxisCanvas - AxisBigTick - GapForAxisLabels - AxisTitleFont.Height * 0.5 - LABEL_TO_AXIS_LABEL_GAP, TxtFormat);
                TxtFormat.Dispose();
            }
        }


        // TRANSMISSINGCOMMENT: Method DrawVerticalAxisLabel
        private void DrawVerticalAxisLabel(string Text, StringAlignment Alignment, double X, double Y)
        {
            StringFormat TxtFormat = new StringFormat { Alignment = Alignment };

            TxtFormat.Alignment = StringAlignment.Near;
            Canvas.TranslateTransform(Convert.ToSingle(X), Convert.ToSingle(MetaH - Y));
            Canvas.RotateTransform(-90.0F);
            Canvas.DrawString(Text, AxisLabelFont, Brushes.Black, 0, 0, TxtFormat);
            Canvas.ResetTransform();
            TxtFormat.Dispose();
        }


        // TRANSMISSINGCOMMENT: Method DrawYAxisTitle
        public void DrawYAxisTitle(string Title, double yShift)
        {
            if (!(string.IsNullOrEmpty(Title)))
            {
                StringFormat TxtFormat = new StringFormat { Alignment = StringAlignment.Center };

                Canvas.TranslateTransform(Convert.ToSingle(XAxisCanvas - AxisLabelFont.Height - AxisBigTick - yShift - LABEL_TO_AXIS_LABEL_GAP), Convert.ToSingle(MetaH - (YExtCanvas / 2 + YAxisCanvas)));
                Canvas.RotateTransform(-90.0F);
                Canvas.DrawString(Title, AxisTitleFont, Brushes.Black, 0, 0, TxtFormat);
                Canvas.ResetTransform();
                TxtFormat.Dispose();
            }
        }


        ///  <summary>
        ///  Draw the axes and chart title.
        ///  </summary>
        ///  <param name="Title">The title of the chart</param>
        ///  <param name="X">The axis definition for the X-axis</param>
        ///  <param name="Y">The axis definition for the Y-axis</param>
        /// <param name="ShouldBoxAxes"></param>
        /// <param name="ShouldDefaultAxes">If true, DefaultAxes() is called before the axes are drawn.</param>
        /// <param name="UseCalculatedScalesEvenWithDefinition"></param>
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
        private void DrawAxes(string Title, Axis X, Axis Y, bool ShouldBoxAxes, bool ShouldDefaultAxes, bool UseCalculatedScalesEvenWithDefinition)
        {
            if (!(IsAscii))
            {
                if (ShouldDefaultAxes)
                {
                    DefaultAxes();
                }
                XAxisCanvas += Y.ExtraSpace;
                XExtCanvas -= Y.ExtraSpace;
                YAxisCanvas += X.ExtraSpace;
                YExtCanvas -= X.ExtraSpace;

                // Draw the title.  Don't draw the axis titles until we know how much we might have to move them.
                DrawTitle(Title);

                PushContainer();
                // Draw the axes
                switch (X.Mode)
                {
                    case AxisMode.LineOnly:
                    case AxisMode.Scale:
                    case AxisMode.Series:
                    case AxisMode.ReverseScale:
                        AxisDrawline(XAxisCanvas, YAxisCanvas, XAxisCanvas + XExtCanvas, YAxisCanvas);
                        break;
                }

                switch (Y.Mode)
                {
                    case AxisMode.LineOnly:
                    case AxisMode.Scale:
                    case AxisMode.Series:
                    case AxisMode.ReverseScale:
                        AxisDrawline(XAxisCanvas, YAxisCanvas + YExtCanvas, XAxisCanvas, YAxisCanvas);
                        break;
                }

                if (ShouldBoxAxes)
                {
                    // Boxed in, assume both drawn
                    AxisDrawline(XAxisCanvas + XExtCanvas, YAxisCanvas + YExtCanvas, XAxisCanvas, YAxisCanvas + YExtCanvas);
                    AxisDrawline(XAxisCanvas + XExtCanvas, YAxisCanvas + YExtCanvas, XAxisCanvas + XExtCanvas, YAxisCanvas);
                }
            }
            else
            { //  Is Ascii

                // Draw the Title
                int s = 40 - (Title.Length / 2);
                WriteAsciiYX(SH_TX.GetUpperBound(0), s, Title);
            }

            double xShift = X.AxisTitleOffset;
            double yShift = Y.AxisTitleOffset;
            switch (X.Mode)
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
                    xShift = DrawXScale(true, X.ScaleType, UseCalculatedScalesEvenWithDefinition);
                    break;
                case AxisMode.ScaleWithoutLabels:
                    xShift = DrawXScale(false, X.ScaleType, UseCalculatedScalesEvenWithDefinition);
                    break;
                case AxisMode.Series:
                    xShift = DrawXSeries();
                    break;
            }

            switch (Y.Mode)
            {
                case AxisMode.LineOnly:
                    //  Do nothing
                    break;
                case AxisMode.None:
                    //  Do nothing
                    break;
                case AxisMode.ReverseScale:
                    yShift = DrawYScale(true, Y.ScaleType, UseCalculatedScalesEvenWithDefinition);
                    break;
                case AxisMode.Scale:
                    yShift = DrawYScale(false, Y.ScaleType, UseCalculatedScalesEvenWithDefinition);
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
                DrawXAxisTitle(X.Title, xShift);
                DrawYAxisTitle(Y.Title, yShift);
                PopContainer();
            }

        }


        // TRANSMISSINGCOMMENT: Method DrawXScale
        private double DrawXScale(bool DrawLabels, ScaleType ScaleType, bool UseCalculatedScalesEvenWithDefinition)
        {
            double aint = 0, amin = 0;
            string msk = "";
            double labelHeight = 0;

            if (IsAscii || DrawLabels)
            {
                // find a neat axis division
                Q_AxisOrFromDefinition(ref dataMinX, ref dataMaxX, out XDiv, ref amin, ref aint, out MinorTicsPerMajorTic, false, ScaleType, UseCalculatedScalesEvenWithDefinition);
                // set the X axis min and max values to fit the scale
                XInt = aint;
                axisXMin = amin;
                axisXMax = amin + (aint * XDiv);
                // set a string mask that will fit OK
                msk = AxisMaskOrFromDefinition(aint, amin, XDiv, MinorTicsPerMajorTic, false, ScaleType, UseCalculatedScalesEvenWithDefinition);
            }
            else
            {
                //  Not ASCII, not drawing our own labels
                XDiv = 20;
                MinorTicsPerMajorTic = 5;
                XInt = (dataMaxX - dataMinX) / Convert.ToDouble(XDiv);
                axisXMin = dataMinX;
                axisXMax = dataMaxX;
            }

            if (!(IsAscii))
            {
                LabelDirection direction = LabelDirection.Across;
                bool hasGridLines = false;
                System.Drawing.Drawing2D.DashStyle gridLineDashStyle = System.Drawing.Drawing2D.DashStyle.Solid;
                Pen gridLinePen = null;
                if (HasScaleParameters && definition.ScaleParameters.X != null)
                {
                    direction = definition.ScaleParameters.X.LabelDirection;
                    hasGridLines = definition.ScaleParameters.X.HasGridLines;
                    gridLineDashStyle = definition.ScaleParameters.X.GridLineDashStyle;
                }
                if (hasGridLines)
                {
                    gridLinePen = new Pen(AxisPen.Color, 1) { DashStyle = gridLineDashStyle };

                }
                for (int x = 0; x <= XDiv; x++)
                {
                    double x1 = x / (double)XDiv * XExtCanvas + XAxisCanvas;
                    if (x % MinorTicsPerMajorTic != 0)
                    {
                        //  Minor tic
                        AxisDrawline(x1, YAxisCanvas - AxisLittleTick, x1, YAxisCanvas);
                        if (hasGridLines && !(DrawLabels))
                        {
                            DrawLine(gridLinePen, x1, YAxisCanvas, x1, YAxisCanvas + YExtCanvas);
                        }
                    }
                    else
                    {
                        //  Major tic - may or may not be labelled
                        if (DrawLabels)
                        {
                            double value = axisXMin + x * aint;
                            //  Un-transform value for non-linear scales
                            switch (ScaleType)
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
                            labelHeight = Math.Max(AxisDrawStringAtAngleCT(lab, x1, YAxisCanvas - AxisBigTick, direction).Height, Convert.ToSingle(labelHeight));
                            AxisDrawline(x1, YAxisCanvas - AxisBigTick, x1, YAxisCanvas);
                        }
                        else
                        {
                            AxisDrawline(x1, YAxisCanvas - AxisLittleTick, x1, YAxisCanvas);
                        }
                        if (hasGridLines)
                        {
                            DrawLine(gridLinePen, x1, YAxisCanvas, x1, YAxisCanvas + YExtCanvas);
                        }
                    }
                }
                if (gridLinePen != null)
                {
                    gridLinePen.Dispose();
                }
            }
            else
            {
                //  ASCII - always linear for now.  TODO: Log
                SH_TX[ASCII_Ytxt - 1] = String.Empty.PadLeft(13) + "/" + new string('-', 61);
                for (int x = 0; x <= XDiv; x++)
                {
                    if (x % MinorTicsPerMajorTic == 0)
                    {
                        string lab = (axisXMin + (x * aint)).ToString(msk);
                        int l = lab.Length;
                        labelHeight = Math.Max(Convert.ToInt32(labelHeight), l);
                        int s = Convert.ToInt32((x * (60 / XDiv)) + 15);
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


        // TRANSMISSINGCOMMENT: Method Q_AxisOrFromDefinition
        private void Q_AxisOrFromDefinition(ref double qmin, ref double qmax, out int div, ref double zmin, ref double zint, out int MinorTicsPerMajorTic, bool IsY, ScaleType ScaleType, bool UseCalculatedScalesEvenWithDefinition)
        {
            if ((definition != null) && definition.HasScaleParameters && !(UseCalculatedScalesEvenWithDefinition))
            {
                //  Use the values in our scale parameters
                AxisScaleParameters asp = IsY ? definition.ScaleParameters.Y : definition.ScaleParameters.X;
                if ((asp != null) && asp.HasAxisScale)
                {
                    qmin = asp.QMin;
                    qmax = asp.QMax;
                    div = asp.Div;
                    zmin = asp.ZMin;
                    zint = asp.ZInt;
                    MinorTicsPerMajorTic = asp.MinorTicsPerMajorTic;
                    return;
                }
            }
            //  If we get here, there was no prior definition - calculate it ourselves.
            AxisScaler.Q_Axis(ref qmin, ref qmax, out div, ref zmin, ref zint, out MinorTicsPerMajorTic, ScaleType);
        }


        // TRANSMISSINGCOMMENT: Method AxisMaskOrFromDefinition
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


        // TRANSMISSINGCOMMENT: Method DrawYScale
        private double DrawYScale(bool reverse, ScaleType scaleType, bool useCalculatedScalesEvenWithDefinition)
        {
            const double AXIS_LABEL_OFFSET_FROM_BIG_TICK = 8;

            // find a neat axis division
            double aint = 0, amin = 0;
            Q_AxisOrFromDefinition(ref dataMinY, ref dataMaxY, out YDiv, ref amin, ref aint, out MinorTicsPerMajorTic, true, scaleType, useCalculatedScalesEvenWithDefinition);

            // set the Y axis min and max values to fit the scale
            YInt = aint;
            axisYMin = amin;
            axisYMax = amin + (aint * YDiv);

            // set a string mask that will fit OK
            string msk = AxisMaskOrFromDefinition(aint, amin, YDiv, MinorTicsPerMajorTic, true, scaleType, useCalculatedScalesEvenWithDefinition);
            LabelDirection direction = LabelDirection.Across;
            bool hasGridLines = false;
            System.Drawing.Drawing2D.DashStyle gridLineDashStyle = System.Drawing.Drawing2D.DashStyle.Solid;
            Pen gridLinePen = null;
            if (HasScaleParameters && definition.ScaleParameters.Y != null)
            {
                direction = definition.ScaleParameters.Y.LabelDirection;
                hasGridLines = definition.ScaleParameters.Y.HasGridLines;
                gridLineDashStyle = definition.ScaleParameters.Y.GridLineDashStyle;
            }
            if (hasGridLines)
                gridLinePen = new Pen(AxisPen.Color, 1) { DashStyle = gridLineDashStyle };

            double maxWidth = 0;
            if (!(IsAscii))
            {
                for (int y = 0; y <= YDiv; y++)
                {
                    double y1 = (y / (double)YDiv * YExtCanvas) + YAxisCanvas;
                    if (y % MinorTicsPerMajorTic != 0)
                    {
                        //  Minor tic
                        AxisDrawline(XAxisCanvas - AxisLittleTick, y1, XAxisCanvas, y1);
                    }
                    else
                    {
                        //  Major tic
                        double value;
                        if (reverse)
                        {
                            value = axisYMin + (Convert.ToDouble(YDiv - y) * aint);
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
                        maxWidth = Math.Max(Convert.ToSingle(maxWidth), AxisDrawStringAtAngleRM(lab, XAxisCanvas - (AxisBigTick + AXIS_LABEL_OFFSET_FROM_BIG_TICK), y1, direction).Width);
                        AxisDrawline(XAxisCanvas - AxisBigTick, y1, XAxisCanvas, y1);
                        if (hasGridLines)
                        {
                            DrawLine(gridLinePen, XAxisCanvas, y1, XAxisCanvas + XExtCanvas, y1);
                        }
                    }
                }
                if (gridLinePen != null)
                {
                    gridLinePen.Dispose();
                }
            }
            else
            {
                for (int y = 0; y <= YDiv; y++)
                {
                    if (y % MinorTicsPerMajorTic == 0)
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
                StringFormat TxtFormat = new StringFormat { Alignment = StringAlignment.Far };

                LabelDirection direction = LabelDirection.Across;
                bool hasGridLines = false;
                System.Drawing.Drawing2D.DashStyle gridLineDashStyle = System.Drawing.Drawing2D.DashStyle.Solid;
                Pen gridLinePen = null;
                if (HasScaleParameters && definition.ScaleParameters.Y != null)
                {
                    direction = definition.ScaleParameters.Y.LabelDirection;
                    hasGridLines = definition.ScaleParameters.Y.HasGridLines;
                    gridLineDashStyle = definition.ScaleParameters.Y.GridLineDashStyle;
                }
                if (hasGridLines)
                {
                    gridLinePen = new Pen(AxisPen.Color, 1) { DashStyle = gridLineDashStyle };

                }
                double yoff = AxisLabelFont.Height / 2.0;
                double count = Convert.ToDouble(definition.YSeries.Count);
                for (int y = 0; y <= definition.YSeries.Count - 1; y++)
                {
                    double yctr = YAxisCanvas + YExtCanvas - ((y + 0.5) / count * YExtCanvas);
                    double ytic = YAxisCanvas + YExtCanvas - (y / count * YExtCanvas);
                    //  TODO: There's an error here where MetaH is not default.  The string is not offset by MetaH, leading to the strings being drawn in an unexpected order.
                    //  However, as all the charts in here accommodate that order, this hasn't been fixed!  PJC 2009/12/22
                    float angle = DirectionToAngle(direction);
                    Canvas.TranslateTransform(Convert.ToSingle(XAxisCanvas - (AxisBigTick + 3)), Convert.ToSingle(yctr - yoff));
                    Canvas.RotateTransform(angle);
                    Canvas.DrawString(definition.YSeries[y].Title, AxisLabelFont, AxisBrush, 0, 0, TxtFormat);
                    Canvas.ResetTransform();
                    SizeF uprightSize = Canvas.MeasureString(definition.YSeries[y].Title, AxisLabelFont);
                    SizeF boundingSize = ToBoundingSize(uprightSize, direction);
                    maxWidth = Math.Max(Convert.ToSingle(maxWidth), boundingSize.Width);
                    AxisDrawline(XAxisCanvas - AxisBigTick, ytic, XAxisCanvas, ytic);
                    if (hasGridLines)
                    {
                        DrawLine(gridLinePen, XAxisCanvas, ytic, XAxisCanvas + XExtCanvas, ytic);
                    }
                }
                if (gridLinePen != null)
                {
                    gridLinePen.Dispose();
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
                    {
                        q = 1;
                    }
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
        private double DrawYSeries(IList<string> Labels)
        {
            double maxWidth = 0;
            StringFormat txtFormat = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };


            LabelDirection direction = LabelDirection.Across;
            bool hasGridLines = false;
            System.Drawing.Drawing2D.DashStyle gridLineDashStyle = System.Drawing.Drawing2D.DashStyle.Solid;
            Pen gridLinePen = null;
            if (HasScaleParameters && definition.ScaleParameters.Y != null)
            {
                direction = definition.ScaleParameters.Y.LabelDirection;
                hasGridLines = definition.ScaleParameters.Y.HasGridLines;
                gridLineDashStyle = definition.ScaleParameters.Y.GridLineDashStyle;
            }
            if (hasGridLines)
            {
                gridLinePen = new Pen(AxisPen.Color, 1) { DashStyle = gridLineDashStyle };
            }
            double count = Labels.Count;
            for (int y = 0; y <= Labels.Count - 1; y++)
            {
                double yctr = YAxisCanvas + YExtCanvas - ((y + 0.5) / count * YExtCanvas);
                double ytic = YAxisCanvas + YExtCanvas - (y / count * YExtCanvas);
                maxWidth = Math.Max(Convert.ToSingle(maxWidth), DrawStringAtAngle(Labels[y], AxisLabelFont, AxisBrush, XAxisCanvas - (AxisBigTick + 3), yctr, txtFormat, direction).Width);
                AxisDrawline(XAxisCanvas - AxisBigTick, ytic, XAxisCanvas, ytic);
                if (hasGridLines)
                {
                    DrawLine(gridLinePen, XAxisCanvas, ytic, XAxisCanvas + XExtCanvas, ytic);
                }
            }
            if (gridLinePen != null)
            {
                gridLinePen.Dispose();
            }
            return maxWidth;
        }


        ///  <summary>
        ///  Draw the X axis as a series
        ///  </summary>
        private double DrawXSeries()
        {
            double maxHeight = 0;
            if (!(IsAscii))
            {
                StringFormat txtFormat = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near };


                LabelDirection direction = LabelDirection.Across;
                bool hasGridLines = false;
                System.Drawing.Drawing2D.DashStyle gridLineDashStyle = System.Drawing.Drawing2D.DashStyle.Solid;
                Pen gridLinePen = null;
                if (HasScaleParameters && definition.ScaleParameters.X != null)
                {
                    direction = definition.ScaleParameters.X.LabelDirection;
                    hasGridLines = definition.ScaleParameters.X.HasGridLines;
                    gridLineDashStyle = definition.ScaleParameters.X.GridLineDashStyle;
                }
                if (hasGridLines)
                {
                    gridLinePen = new Pen(AxisPen.Color, 1) { DashStyle = gridLineDashStyle };

                }
                double count = definition.XSeries.Count;
                for (int x = 0; x <= definition.XSeries.Count - 1; x++)
                {
                    double xctr = XAxisCanvas + (x + 0.5) / count * XExtCanvas;
                    double xtic = XAxisCanvas + Convert.ToDouble(x + 1) / count * XExtCanvas;
                    maxHeight = Math.Max(Convert.ToSingle(maxHeight), DrawStringAtAngle(definition.XSeries[x].Title, AxisLabelFont, AxisBrush, xctr, YAxisCanvas - AxisBigTick, txtFormat, direction).Height);
                    AxisDrawline(xtic, YAxisCanvas - AxisBigTick, xtic, YAxisCanvas);
                    if (hasGridLines)
                    {
                        DrawLine(gridLinePen, xtic, YAxisCanvas, xtic, YAxisCanvas + YExtCanvas);
                    }
                }
                if (gridLinePen != null)
                {
                    gridLinePen.Dispose();
                }
            }
            return maxHeight;
        }


        ///  <summary>
        ///  Draw the X axis as a series
        ///  </summary>
        private double DrawXSeries(IList<string> Labels)
        {
            double maxHeight = 0;
            if (!(IsAscii))
            {
                StringFormat txtFormat = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near };


                LabelDirection direction = LabelDirection.Across;
                bool hasGridLines = false;
                System.Drawing.Drawing2D.DashStyle gridLineDashStyle = System.Drawing.Drawing2D.DashStyle.Solid;
                Pen gridLinePen = null;
                if (HasScaleParameters && definition.ScaleParameters.X != null)
                {
                    direction = definition.ScaleParameters.X.LabelDirection;
                    hasGridLines = definition.ScaleParameters.X.HasGridLines;
                    gridLineDashStyle = definition.ScaleParameters.X.GridLineDashStyle;
                }
                if (hasGridLines)
                {
                    gridLinePen = new Pen(AxisPen.Color, 1) { DashStyle = gridLineDashStyle };

                }
                double count = Labels.Count;
                for (int x = 0; x <= Labels.Count - 1; x++)
                {
                    double xctr = XAxisCanvas + (x + 0.5) / count * XExtCanvas;
                    double xtic = XAxisCanvas + Convert.ToDouble(x + 1) / count * XExtCanvas;
                    maxHeight = Math.Max(Convert.ToSingle(maxHeight), DrawStringAtAngle(Labels[x], AxisLabelFont, AxisBrush, xctr, YAxisCanvas - AxisBigTick, txtFormat, direction).Height);
                    AxisDrawline(xtic, YAxisCanvas - AxisBigTick, xtic, YAxisCanvas);
                    if (hasGridLines)
                    {
                        DrawLine(gridLinePen, xtic, YAxisCanvas, xtic, YAxisCanvas + YExtCanvas);
                    }
                }
                if (gridLinePen != null)
                {
                    gridLinePen.Dispose();
                }
            }
            return maxHeight;
        }


        // TRANSMISSINGCOMMENT: Method AxisDrawline
        private void AxisDrawline(double X1, double Y1, double X2, double Y2)
        {
            DrawLine(AxisPen, X1, Y1, X2, Y2);
        }


        // TRANSMISSINGCOMMENT: Method DrawLine
        private void DrawLine(Pen P, double X1, double Y1, double X2, double Y2)
        {
            Canvas.DrawLine(P, Convert.ToSingle(Math.Round(X1, 0)), Convert.ToSingle(Math.Round(MetaH - Y1, 0)), Convert.ToSingle(Math.Round(X2, 0)), Convert.ToSingle(Math.Round(MetaH - Y2, 0)));
        }


        // TRANSNOTUSED: Private Method AxisDrawStringR

        //		private void AxisDrawStringR( string Txt, double X1, double Y1 ) 
        //		{ 
        //			// Draw axis text aligned to the right
        //			StringFormat AlignTxt = new StringFormat(); 
        //			AlignTxt.Alignment = StringAlignment.Far; 
        //			DrawString( Txt, AxisLabelFont, AxisBrush, X1, Y1, AlignTxt ); 
        //		} 
        //

        // TRANSMISSINGCOMMENT: Method AxisDrawStringAtAngleRM
        private SizeF AxisDrawStringAtAngleRM(string Txt, double X1, double Y1, LabelDirection Direction)
        {
            // Draw axis text aligned to the right
            StringFormat AlignTxt = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };


            return DrawStringAtAngle(Txt, AxisLabelFont, AxisBrush, X1, Y1, AlignTxt, Direction);
        }


        ///  <summary>
        ///  Draw axis text aligned to the centre
        ///  </summary>
        ///  <param name="Txt"></param>
        ///  <param name="X1"></param>
        ///  <param name="Y1"></param>
        /// <param name="Direction"></param>
        private SizeF AxisDrawStringAtAngleCT(string Txt, double X1, double Y1, LabelDirection Direction)
        {
            StringFormat AlignTxt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near };


            return DrawStringAtAngle(Txt, AxisLabelFont, AxisBrush, X1, Y1, AlignTxt, Direction);
        }


        ///  <summary>
        ///  Draw axis text aligned to the centre
        ///  </summary>
        ///  <param name="Txt"></param>
        ///  <param name="X1"></param>
        ///  <param name="Y1"></param>
        private void AxisDrawStringC(string Txt, double X1, double Y1)
        {
            StringFormat AlignTxt = new StringFormat { Alignment = StringAlignment.Center };

            DrawString(Txt, AxisLabelFont, AxisBrush, X1, Y1, AlignTxt);
        }


        // TRANSNOTUSED: Private Method AxisDrawStringL

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
        private void DrawStringLegendL(string Txt, double X, double Y)
        {
            DrawStringLegend(Txt, X, Y, StringAlignment.Near);
        }


        ///  <summary>
        ///  Draw legend text aligned to the left
        ///  </summary>
        ///  <remarks></remarks>
        // TRANSNOTUSED: Private Method DrawStringSeriesLabelL

        //		private void DrawStringSeriesLabelL( string Txt, double X, double Y ) 
        //		{ 
        //			DrawStringSeriesLabel( Txt, X, Y, StringAlignment.Near ); 
        //		} 
        //

        ///  <summary>
        ///  Draw legend text
        ///  </summary>
        ///  <remarks></remarks>
        private void DrawStringLegend(string Txt, double X, double Y, StringAlignment Alignment)
        {
            StringFormat AlignTxt = new StringFormat { Alignment = Alignment };

            DrawString(Txt, LegendFont, AxisBrush, X, Y, AlignTxt);
        }


        ///  <summary>
        ///  Draw legend text
        ///  </summary>
        ///  <remarks></remarks>
        // TRANSNOTUSED: Private Method DrawStringSeriesLabel

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
        private void DrawStringLabel(string Txt, double X, double Y, StringAlignment Alignment)
        {
            StringFormat AlignTxt = new StringFormat { Alignment = Alignment };

            DrawString(Txt, LabelFont, AxisBrush, X, Y, AlignTxt);
        }


        // TRANSMISSINGCOMMENT: Method DrawStringLabel
        private void DrawStringLabel(string Txt, double X, double Y, StringAlignment Alignment, StringAlignment LineAlignment)
        {
            StringFormat AlignTxt = new StringFormat { Alignment = Alignment, LineAlignment = LineAlignment };


            DrawString(Txt, LabelFont, AxisBrush, X, Y, AlignTxt);
        }


        // TRANSMISSINGCOMMENT: Method FillEllipse
        private void FillEllipse(Brush b, double x, double y, double width, double height)
        {
            Canvas.FillEllipse(b, Convert.ToInt32(Convert.ToSingle(x)), Convert.ToInt32(Convert.ToSingle(MetaH - y)), Convert.ToInt32(Convert.ToSingle(width)), Convert.ToInt32(Convert.ToSingle(height)));
        }


        // TRANSMISSINGCOMMENT: Method DrawEllipse
        private void DrawEllipse(Pen p, double x, double y, double width, double height)
        {
            Canvas.DrawEllipse(p, Convert.ToSingle(x), Convert.ToSingle(MetaH - y), Convert.ToSingle(width), Convert.ToSingle(height));
        }


        // TRANSMISSINGCOMMENT: Method FillRectangle
        private void FillRectangle(Brush b, double x, double y, double width, double height)
        {
            Canvas.FillRectangle(b, Convert.ToSingle(x), Convert.ToSingle(MetaH - y), Convert.ToSingle(width), Convert.ToSingle(height));
        }


        // TRANSMISSINGCOMMENT: Method DrawRectangle
        private void DrawRectangle(Pen p, double x, double y, double width, double height)
        {
            Canvas.DrawRectangle(p, Convert.ToSingle(x), Convert.ToSingle(MetaH - y), Convert.ToSingle(width), Convert.ToSingle(height));
        }


        // TRANSMISSINGCOMMENT: Method DrawMarker
        private void DrawMarker(double X, double Y, double Size, MarkerShape Shape, bool IsFilled, Pen p)
        {
            double size2 = Size * 2;

            switch (Shape)
            {
                case MarkerShape.Circle:
                    {
                        if (IsFilled)
                        {
                            Brush b = new SolidBrush(p.Color);
                            FillEllipse(b, X - Size, Y + Size, size2, size2);
                            b.Dispose();
                        }
                        else
                        {
                            DrawEllipse(p, X - Size, Y + Size, size2, size2);
                        }
                    } break;
                case MarkerShape.Square:
                    {
                        if (IsFilled)
                        {
                            Brush b = new SolidBrush(p.Color);
                            FillRectangle(b, X - Size, Y + Size, size2, size2);
                            b.Dispose();
                        }
                        else
                        {
                            DrawRectangle(p, X - Size, Y + Size, size2, size2);
                        }
                    } break;
                case MarkerShape.Triangle:
                    {
                        PointF[] points = { new PointF(Convert.ToSingle(X - Size), Convert.ToSingle(MetaH - (Y - Size))), new PointF(Convert.ToSingle(X), Convert.ToSingle(MetaH - (Y + Size))), new PointF(Convert.ToSingle(X + Size), Convert.ToSingle(MetaH - (Y - Size))) };
                        if (IsFilled)
                        {
                            Brush b = new SolidBrush(p.Color);
                            Canvas.FillPolygon(b, points);
                            b.Dispose();
                        }
                        else
                        {
                            Canvas.DrawPolygon(p, points);
                            // Canvas.DrawLine(p, X - Size, MetaH - (Y - Size), X, MetaH - (Y + Size))
                            // Canvas.DrawLine(p, X, MetaH - (Y + Size), X + Size, MetaH - (Y - Size))
                            // Canvas.DrawLine(p, X + Size, MetaH - (Y - Size), X - Size, MetaH - (Y - Size))
                        }
                    } break;
                case MarkerShape.Plus:
                    //  Same filled or unfilled
                    DrawLine(p, X - Size, Y, X + Size, Y);
                    DrawLine(p, X, Y - Size, X, Y + Size);
                    break;
                case MarkerShape.Cross:
                    //  Same filled or unfilled
                    DrawLine(p, X - Size, Y - Size, X + Size, Y + Size);
                    DrawLine(p, X - Size, Y + Size, X + Size, Y - Size);
                    break;
                case MarkerShape.CircleLine:
                    {
                        if (IsFilled)
                        {
                            Brush b = new SolidBrush(p.Color);
                            FillEllipse(b, X - Size, Y + Size, size2, size2);
                            b.Dispose();
                            DrawLine(Pens.White, X, Y - Size, X, Y + Size);
                        }
                        else
                        {
                            DrawEllipse(p, X - Size, Y + Size, size2, size2);
                            DrawLine(p, X, Y - Size, X, Y + Size);
                        }
                    } break;
                case MarkerShape.SquareLine:
                    if (IsFilled)
                    {
                        Brush b = new SolidBrush(p.Color);
                        FillRectangle(b, X - Size, Y + Size, size2, size2);
                        b.Dispose();
                        DrawLine(Pens.White, X - Size, Y + Size, X + Size, Y - Size);
                    }
                    else
                    {
                        DrawRectangle(p, X - Size, Y + Size, size2, size2);
                        DrawLine(p, X - Size, Y + Size, X + Size, Y - Size);
                    }
                    break;
                case MarkerShape.SquareCross:
                    if (IsFilled)
                    {
                        Brush b = new SolidBrush(p.Color);
                        FillRectangle(b, X - Size, Y + Size, size2, size2);
                        b.Dispose();
                        DrawLine(Pens.White, X - Size, Y - Size, X + Size, Y + Size);
                        DrawLine(Pens.White, X - Size, Y + Size, X + Size, Y - Size);
                    }
                    else
                    {
                        DrawRectangle(p, X - Size, Y + Size, size2, size2);
                        DrawLine(p, X - Size, Y - Size, X + Size, Y + Size);
                        DrawLine(p, X - Size, Y + Size, X + Size, Y - Size);
                    }
                    break;
                case MarkerShape.Diamond:
                    DrawDiamond(p, X, Y, size2, IsFilled);
                    break;
                default:
                    throw new ArgumentException("Don't know how to draw style's shape", "Shape");
            }

        }


        // TRANSMISSINGCOMMENT: Method DrawMarker
        private void DrawMarker(double X, double Y, double Size, DoubleSeries Series)
        {
            DrawMarker(X, Y, Size, Series.Shape, Series.IsFilled, Series.UnstyledPen);
        }


        // TRANSMISSINGCOMMENT: Method DrawMarker
        private void DrawMarker(double X, double Y, double Size, MarkerType mType)
        {
            Pen p = GetPen(mType, true);
            DrawMarker(X, Y, Size, mType.Shape, mType.IsFilled, p);
            p.Dispose();
        }


        // TRANSMISSINGCOMMENT: Method SetStandardScaling
        private void SetStandardScaling()
        {
            divx = axisXMax - axisXMin;
            offx = -(axisXMin / divx * XExtCanvas) + XAxisCanvas;
            divy = axisYMax - axisYMin;
            offy = -(axisYMin / divy * YExtCanvas) + YAxisCanvas;
        }


        // TRANSMISSINGCOMMENT: Method SetStandardAsciiScaling
        private void SetStandardAsciiScaling()
        {
            divx = axisXMax - axisXMin;
            offx = Convert.ToInt32(-(axisXMin / divx * 60) + 15);
            divy = axisYMax - axisYMin;
            offy = Convert.ToInt32(-(axisYMin / divy * YDiv) + ASCII_Ytxt);
        }


        // TRANSMISSINGCOMMENT: Method GetScatterScaleParameters
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


        // TRANSMISSINGCOMMENT: Method PlotScatter
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
                        if (w > xtra + XAxisCanvas)
                        {
                            xtra = w - XAxisCanvas;
                        }
                    }
                }

                DrawAxes(definition.ChartOptions.Title, new Axis(sOptions.XAxisTitle, AxisMode.Scale, 0, definition.ScaleParameters.X.ScaleType), new Axis(sOptions.YAxisTitle, AxisMode.Scale, xtra, definition.ScaleParameters.Y.ScaleType), BoxAxes, true, false);

                float size2 = LabelFont.Size * 2;
                //  If there are multiple series, draw the legends
                if (definition.XSeries.Count > 1)
                {
                    int i = 1;
                    foreach (Series s in definition.XSeries)
                    {
                        if (s.Title.Length > 0)
                        {
                            DrawMarker(LEGEND_MARKER_X, YAxisCanvas + YExtCanvas - LEGEND_MARKER_Y_OFFSET - (size2 * i), LEGEND_MARKER_SIZE, definition.YSeries[i - 1].AsDoubleSeries);
                            DrawStringLegendL(s.Title, LEGEND_TEXT_X, YAxisCanvas + YExtCanvas - 10 - (size2 * i));
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
                WriteAsciiYX(SH_TX.GetUpperBound(0) - 1, Q, sOptions.YAxisTitle);
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
                            int y1 = Convert.ToInt32(offy + Convert.ToInt32(ydat[r] / divy * YDiv));
                            ASCII_PlotPoint(x1, y1);
                        }
                    }
                }
            }
            //  End If
            return new ParameterBag();
        }


        // TRANSMISSINGCOMMENT: Method GetLinearRegressionScaleParameters
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


        // TRANSMISSINGCOMMENT: Method PlotLinearRegression
        private ParameterBag PlotLinearRegression(Stream OutputStream)
        {
            const int MINIMUM_X_WHITESPACE = 70;
            const int MARKER_SIZE = 6;

            LinearRegressionOptions lrOptions = ((LinearRegressionOptions)(definition.ChartOptions));
            double slope = lrOptions.Slope;
            double intercept = lrOptions.Intercept;
            bool fullWidth = lrOptions.FullWidth;

            // Plot a metafile version
            StartMetafile(OutputStream, true);
            AssignMarkersToSeries();
            // Draw the scale
            DefaultAxes();
            //  What extra space do we need before the X axis?
            double xtra = 0;
            if (definition.XSeries.Count > 1)
            {
                foreach (Series s in definition.XSeries)
                {
                    double w = Canvas.MeasureString(s.Title, LegendFont).Width + MINIMUM_X_WHITESPACE;
                    if (w > xtra + XAxisCanvas)
                    {
                        xtra = w - XAxisCanvas;
                    }
                }
            }

            DrawAxes(definition.ChartOptions.Title, new Axis(lrOptions.XAxisTitle, AxisMode.Scale, 0, definition.ScaleParameters.X.ScaleType), new Axis(lrOptions.YAxisTitle, AxisMode.Scale, xtra, definition.ScaleParameters.Y.ScaleType), BoxAxes, true, false);

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

            double xstep = XInt / 2;

            // Plot regression
            Pen p = new Pen(grGreen, 2);
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
                if (calcx > lowerX && y1 > YAxisCanvas && x1 > XAxisCanvas && y1 < YAxisCanvas + YExtCanvas)
                {
                    DrawLine(p, x1, y1, oldx, oldy);
                }
                oldx = x1;
                oldy = y1;
            }
            p.Dispose();

            MaybeDrawMarkerLines();
            EndMetafile();
            return new ParameterBag();
        }


        // TRANSMISSINGCOMMENT: Method PlotLinearRegressionInternal
        public void PlotLinearRegressionInternal(ITemplateHost host, string title, double slope, double intercept, bool fullWidth, string XAxisTitle, string YAxisTitle)
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
                    double w = Canvas.MeasureString(s.Title, LegendFont).Width + MINIMUM_X_WHITESPACE;
                    if (w > xtra + XAxisCanvas)
                    {
                        xtra = w - XAxisCanvas;
                    }
                }
            }

            DrawAxes(title, new Axis(XAxisTitle, AxisMode.Scale, 0, ScaleType.Linear), new Axis(YAxisTitle, AxisMode.Scale, xtra, ScaleType.Linear), BoxAxes, true, false);

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

            double xstep = XInt / 2;

            // Plot regression
            Pen p = new Pen(grGreen, 2);
            double oldx = 0;
            double oldy = 0;
            if (fullWidth)
            {
                for (double calcx = axisXMin; calcx <= axisXMax; calcx += xstep)
                {
                    double calcy = slope * calcx + intercept;
                    double x1 = ToCanvasX(calcx);
                    double y1 = ToCanvasY(calcy);
                    if (calcx > axisXMin && y1 > YAxisCanvas && x1 > XAxisCanvas && y1 < YAxisCanvas + YExtCanvas)
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
                    if (calcx > DataMinX && y1 > YAxisCanvas && x1 > XAxisCanvas && y1 < YAxisCanvas + YExtCanvas)
                    {
                        DrawLine(p, x1, y1, oldx, oldy);
                    }
                    oldx = x1;
                    oldy = y1;
                }
            }
            p.Dispose();
        }


        // TRANSMISSINGCOMMENT: Method PlotCox2Internal
        public void PlotCox2Internal(int[] gn, int igroups, double[] xp, double[] yp, ColumnData[] cdat1, int groupid)
        {
            const int MINIMUM_X_WHITESPACE = 70;

            DefaultAxes();

            double xtra = 0;
            if (definition.XSeries.Count > 1)
            {
                foreach (Series s in definition.XSeries)
                {
                    double w = Canvas.MeasureString(s.Title, LegendFont).Width + MINIMUM_X_WHITESPACE;
                    if (w > xtra + XAxisCanvas)
                    {
                        xtra = w - XAxisCanvas;
                    }
                }
            }

            //  TODO: Log and log-log axes here
            DrawAxes("Log-log plot (parallel groups if hazards proportional)", new Axis("log(Time)", AxisMode.Scale, 0, ScaleType.Linear), new Axis("-log(-log(Survival))", AxisMode.Scale, xtra, ScaleType.Linear), false, true, false);

            // Draw the legends
            Pen p = new Pen(grBlack, 1);
            float size2 = LabelFont.Size * 2;
            for (int i = 1; i <= igroups; i++)
            {
                DrawMarker(12, YAxisCanvas + YExtCanvas - 22 - (size2 * i), 6, ((MarkerShape)(i)), false, p);
                string transTemp11 = cdat1[groupid].Title;
                int transTemp12 = Math.Min(20, cdat1[groupid].Title.Length);
                DrawStringLegendL(  /* TRANSINFO: .NET Equivalent of Microsoft.VisualBasic NameSpace */ transTemp11.Substring(0, transTemp12) + "=" + cdat1[groupid].Groups[i - 1].Label, 24, YAxisCanvas + YExtCanvas - 10 - (size2 * i));
            }

            // get the offsets for the Markers
            SetStandardScaling();

            // plot points
            int istart = 0;
            for (int k = 1; k <= 2; k++)
            {
                PointF[] xys = new PointF[gn[k] + 1 /* for VB to C# conversion */ ];
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
                DrawMarkerSeries(xys, 6, ((MarkerShape)(k)), false, p, p, false, true);
                istart += gn[k];
            }
            p.Dispose();
        }


        // TRANSMISSINGCOMMENT: Method PlotCox1Internal
        public void PlotCox1Internal(ITemplateHost host, string title, coxp[] z, int iobs, bool stratified, bool grouped, int istrata, int igroups, ColumnData[] cdat1, int groupid, bool use_marker, bool use_tic, double[, ,] ARR3, int j3, string XAxisTitle, string YAxisTitle, ref int[] gn)
        {
            const int MINIMUM_X_WHITESPACE = 70;

            Pen p;

            AssignMarkersToSeries();
            // allow more room for legend labels if required
            DefaultAxes();
            double xtra = 0;
            if (stratified)
            {
                for (int i = 1; i <= istrata; i++)
                {
                    double w = Canvas.MeasureString("Stratum " + i.ToString(), LegendFont).Width + MINIMUM_X_WHITESPACE;
                    if (w > xtra + XAxisCanvas)
                    {
                        xtra = w - XAxisCanvas - 5;
                    }
                }
            }
            if (grouped)
            {
                for (int i = 0; i <= igroups - 1; i++)
                {
                    string transTemp14 = cdat1[groupid].Title;
                    int transTemp15 = Math.Min(20, cdat1[groupid].Title.Length);
                    double w = Canvas.MeasureString(  /* TRANSINFO: .NET Equivalent of Microsoft.VisualBasic NameSpace */ transTemp14.Substring(0, transTemp15) + "=" + cdat1[groupid].Groups[i].Label, LegendFont).Width + MINIMUM_X_WHITESPACE;
                    if (w > xtra + XAxisCanvas)
                    {
                        xtra = w - XAxisCanvas - 5;
                    }
                }
            }

            // Draw the axes
            DrawAxes(title, new Axis(XAxisTitle, AxisMode.Scale, 0, definition.ScaleParameters.X.ScaleType), new Axis(YAxisTitle, AxisMode.Scale, xtra, definition.ScaleParameters.Y.ScaleType), false, true, false);

            //  Get the offsets and scale multipliers for the markers
            SetStandardScaling();

            // draw legend
            double size2 = LabelFont.Size * 2;
            if (grouped)
            {
                for (int k = 1; k <= igroups; k++)
                {
                    string transTemp17 = cdat1[groupid].Title;
                    int transTemp18 = Math.Min(20, cdat1[groupid].Title.Length);
                    string vq =  /* TRANSINFO: .NET Equivalent of Microsoft.VisualBasic NameSpace */ transTemp17.Substring(0, transTemp18) + "=" + cdat1[groupid].Groups[k - 1].Label;
                    if (!(use_marker))
                    {
                        p = GetPen(_MarkerTypes[(k - 1) % 9], true);
                        DrawLine(p, 10, YAxisCanvas + YExtCanvas - 18 - (size2 * k), 20, YAxisCanvas + YExtCanvas - 18 - (size2 * k));
                        DrawLine(p, 20, YAxisCanvas + YExtCanvas - 18 - (size2 * k), 20, YAxisCanvas + YExtCanvas - 28 - (size2 * k));
                        p.Dispose();
                    }
                    else
                    {
                        DrawMarker(12, YAxisCanvas + YExtCanvas - 22 - (size2 * k), 6, _MarkerTypes[(k - 1) % 9]);
                    }
                    DrawStringLegendL(vq, 24, YAxisCanvas + YExtCanvas - 10 - (size2 * k));
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
                        p = GetPen(_MarkerTypes[(k - 1) % 9], true);
                        DrawLine(p, 10, YAxisCanvas + YExtCanvas - 18 - (size2 * k), 20, YAxisCanvas + YExtCanvas - 18 - (size2 * k));
                        DrawLine(p, 20, YAxisCanvas + YExtCanvas - 18 - (size2 * k), 20, YAxisCanvas + YExtCanvas - 28 - (size2 * k));
                        p.Dispose();
                    }
                    else
                    {
                        DrawMarker(12, YAxisCanvas + YExtCanvas - 22 - (size2 * k), 6, _MarkerTypes[(k - 1) % 9]);
                    }
                    DrawStringLegendL(vq, 24, YAxisCanvas + YExtCanvas - 10 - (size2 * k));
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

            MarkerType mt = _MarkerTypes[igp % 9];
            p = GetPen(mt, true);
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
                        mt = _MarkerTypes[igp % 9];
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
                        mt = _MarkerTypes[igp % 9];
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


        // TRANSMISSINGCOMMENT: Method PlotLinearizedEstimationInternal
        public void PlotLinearizedEstimationInternal(ITemplateHost host, string title, int model, double a, double b, string XAxisTitle, string YAxisTitle)
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
                    double w = Canvas.MeasureString(s.Title, LegendFont).Width + MINIMUM_X_WHITESPACE;
                    if (w > xtra + XAxisCanvas)
                    {
                        xtra = w - XAxisCanvas;
                    }
                }
            }

            DrawAxes(title, new Axis(XAxisTitle, AxisMode.Scale, 0, definition.ScaleParameters.X.ScaleType), new Axis(YAxisTitle, AxisMode.Scale, xtra, definition.ScaleParameters.Y.ScaleType), BoxAxes, true, false);

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

            double xstep = XInt / 2;

            // Plot regression
            Pen p = new Pen(grBlack, 2);
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
                if (calcx > axisXMin && y1 > YAxisCanvas && x1 > XAxisCanvas && y1 < YAxisCanvas + YExtCanvas)
                {
                    DrawLine(p, x1, y1, oldx, oldy);
                }
                oldx = x1;
                oldy = y1;
            }
            p.Dispose();
        }


        // TRANSMISSINGCOMMENT: Method PlotPolynomialRegressionInternal
        public void PlotPolynomialRegressionInternal(ITemplateHost host, string title, int mode, double[,] xtxi, double[] bd, double rss, int nx, int P, double GAMMA, string XAxisTitle, string YAxisTitle)
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
                    double w = Canvas.MeasureString(ser.Title, LegendFont).Width + MINIMUM_X_WHITESPACE;
                    if (w > xtra + XAxisCanvas)
                    {
                        xtra = w - XAxisCanvas;
                    }
                }
            }

            DrawAxes(title, new Axis(XAxisTitle, AxisMode.Scale, 0, definition.ScaleParameters.X.ScaleType), new Axis(YAxisTitle, AxisMode.Scale, xtra, definition.ScaleParameters.Y.ScaleType), BoxAxes, true, false);

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
            MathDbl.civ(nx - P, out cit, GAMMA, out P0);
            double rdf = Convert.ToDouble((nx - 1) - (P - 1));
            double rms = rss / rdf;
            double[] px = new double[P];
            px[1] = 1.0;
            double xstep = XInt / 2;

            // This routine has changed from the original
            // It is more efficient in drawing - but bigger in code
            // Draw the base line
            Pen greenPen = new Pen(grGreen, 1);
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
                if (calcx > axisXMin && y1 >= YAxisCanvas && x1 >= XAxisCanvas && y1 < YAxisCanvas + YExtCanvas && oldx >= XAxisCanvas && oldy >= YAxisCanvas && oldy < YAxisCanvas + YExtCanvas)
                {
                    DrawLine(greenPen, x1, y1, oldx, oldy);
                }
                oldx = x1;
                oldy = y1;
            }
            greenPen.Dispose();
            if (mode > 0)
            {
                // Draw -Lines
                Pen blackPen = new Pen(grBlack, 1);
                oldx = 0;
                oldy = 0;
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
                    if (calcx > axisXMin && y1 >= YAxisCanvas && x1 >= XAxisCanvas && y1 < YAxisCanvas + YExtCanvas && oldx >= XAxisCanvas && oldy >= YAxisCanvas && oldy < YAxisCanvas + YExtCanvas)
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
                    if (calcx > axisXMin && y1 >= YAxisCanvas && x1 >= XAxisCanvas && y1 < YAxisCanvas + YExtCanvas && oldx >= XAxisCanvas && oldy >= YAxisCanvas && oldy < YAxisCanvas + YExtCanvas)
                    {
                        DrawLine(blackPen, x1, y1, oldx, oldy);
                    }
                    oldx = x1;
                    oldy = y1;
                }
                blackPen.Dispose();
            }
        }


        ///  <remarks>Jul 09: updated to put log models on a log x axis scale</remarks>
        public void PlotLogitInternal(ITemplateHost host, string title, int Model, bool clog, double t, double sw, double S1, double a, double b, string XAxisTitle, string YAxisTitle)
        {
            const int MARKER_SIZE = 6;

            DoubleSeries xs = definition.XSeries[0].AsDoubleSeries;
            DoubleSeries ys = definition.YSeries[0].AsDoubleSeries;
            double[] xdat = xs.Data;
            double[] ydat = ys.Data;
            //  If necessary, move to a log x axis scale.
            //  This allocates a new array if required, as the old one comes directly from storage and is not otherwise copied, so overwriting is dangerous.
            if (clog)
            {
                double[] linxdat = xdat;
                double[] logxdat = new double[linxdat.GetUpperBound(0) + 1 /* for VB to C# conversion */ ];
                for (int i = linxdat.GetLowerBound(0); i <= linxdat.GetUpperBound(0); i++)
                {
                    if (linxdat[i] == Constant.MISSING)
                    {
                        logxdat[i] = Constant.MISSING;
                    }
                    else if (linxdat[i] == 0.0)
                    {
                        logxdat[i] = 0.0;
                    }
                    else
                    {
                        logxdat[i] = Math.Log(linxdat[i]) / Math.Log(10.0);
                    }
                }
                xs.Data = logxdat;
                xdat = logxdat;
                GetMinMaxArray(xdat, out dataMinX, out dataMaxX);
            }

            double cl = 0;
            int nx = 0;
            foreach (double v in xdat)
            {
                if (v != Constant.MISSING)
                {
                    cl = cl + v;
                    nx = nx + 1;
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
            AxisMode xMode = clog ? AxisMode.None : AxisMode.Scale;
            DrawAxes(title, new Axis(XAxisTitle, xMode, 0, definition.ScaleParameters.X.ScaleType), new Axis(YAxisTitle, AxisMode.Scale, 0, definition.ScaleParameters.Y.ScaleType), false, true, false);

            double zmin = 0;
            double zint = 0;
            if (clog)
            {
                //  TODO: Use a proper log scale
                AxisScaler.Q_Axis(ref dataMinX, ref dataMaxX, out XDiv, ref zmin, ref zint, out MinorTicsPerMajorTic, ScaleType.Linear);
                // set the X axis min and max values to fit the scale
                XInt = zint;
                axisXMin = zmin;
                axisXMax = zmin + (zint * XDiv);
            }

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

            double xstep = XInt / 2;

            // This routine has changed from the original
            // It is more efficient in drawing - but bigger in code
            Pen greenPen = new Pen(grGreen, 1);
            double oldx = 0;
            double oldy = 0;
            for (double calcx = axisXMin; calcx <= axisXMax; calcx += xstep)
            {
                // cl = t * Math.Sqrt( 1.0 / sw + Math.Pow( ( calcx - XM ), 2.0 ) / S1 ); 
                double calcy = a + b * calcx;
                if (Model == 1)
                {
                    calcy = PDF.alnorm(calcy);
                }
                else
                {
                    calcy = Math.Exp(calcy * 2.0) / (1.0 + Math.Exp(calcy * 2.0));
                }
                double x1 = ToCanvasX(calcx);
                double y1 = ToCanvasY(calcy);
                if (calcx > axisXMin && y1 >= YAxisCanvas && x1 >= XAxisCanvas && y1 < YAxisCanvas + YExtCanvas && oldx >= XAxisCanvas && oldy >= YAxisCanvas && oldy < YAxisCanvas + YExtCanvas)
                {
                    DrawLine(greenPen, x1, y1, oldx, oldy);
                }
                oldx = x1;
                oldy = y1;
            }
            greenPen.Dispose();

            // Draw upper curve
            Pen magentaPen = new Pen(grMagenta, 1);
            oldx = 0;
            oldy = 0;
            for (double calcx = axisXMin; calcx <= axisXMax; calcx += xstep)
            {
                cl = t * Math.Sqrt(1.0 / sw + Math.Pow((calcx - XM), 2.0) / S1);
                double calcy = a + b * calcx;
                double cly = calcy + cl;
                if (Model == 1)
                {
                    cly = PDF.alnorm(cly);
                }
                else
                {
                    cly = Math.Exp(cly * 2.0) / (1.0 + Math.Exp(cly * 2.0));
                }
                double x1 = ToCanvasX(calcx);
                double y1 = ToCanvasY(cly);
                if (calcx > axisXMin && y1 >= YAxisCanvas && x1 >= XAxisCanvas && y1 < YAxisCanvas + YExtCanvas && oldx >= XAxisCanvas && oldy >= YAxisCanvas && oldy < YAxisCanvas + YExtCanvas)
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
                cl = t * Math.Sqrt(1.0 / sw + Math.Pow((calcx - XM), 2.0) / S1);
                double calcy = a + b * calcx;
                double cly = calcy - cl;
                if (Model == 1)
                {
                    cly = PDF.alnorm(cly);
                }
                else
                {
                    cly = Math.Exp(cly * 2.0) / (1.0 + Math.Exp(cly * 2.0));
                }
                double x1 = ToCanvasX(calcx);
                double y1 = ToCanvasY(cly);
                if (calcx > axisXMin && y1 >= YAxisCanvas && x1 >= XAxisCanvas && y1 < YAxisCanvas + YExtCanvas && oldx >= XAxisCanvas && oldy >= YAxisCanvas && oldy < YAxisCanvas + YExtCanvas)
                {
                    DrawLine(magentaPen, x1, y1, oldx, oldy);
                }
                oldx = x1;
                oldy = y1;
            }
            magentaPen.Dispose();

            // Add a log scale if relevant
            if (clog)
            {
                // find a neat axis division on natural scale
                axisXMin = double.MaxValue;
                axisXMax = double.MinValue;
                for (int r = 0; r <= Math.Min(xs.Data.Length, ys.Data.Length) - 1; r++)
                {
                    if (xdat[r] != Constant.MISSING & ydat[r] != Constant.MISSING)
                    {
                        if (xdat[r] > axisXMax)
                        {
                            axisXMax = xdat[r];
                        }
                        if (xdat[r] < axisXMin)
                        {
                            axisXMin = xdat[r];
                        }
                    }
                }
                Pen tenPen = GetPen(MarkerTypes[10], true);
                int zdiv;
                //  TODO: Use a proper log scale
                AxisScaler.Q_Axis(ref axisXMin, ref axisXMax, out zdiv, ref zmin, ref zint, out MinorTicsPerMajorTic, ScaleType.Linear);
                string msk = GetAxisMask(zint, zmin, zdiv, MinorTicsPerMajorTic);
                int C = ((int)(Math.Floor(Math.Exp(axisXMax * Math.Log(10)) / zint)));
                if (C > zdiv)
                {
                    zdiv = C;
                }
                double x1 = 0;
                for (C = 0; C <= zdiv; C++)
                {
                    double calcx = zmin + Convert.ToDouble(C) * zint;
                    if (calcx > 0.0)
                    {
                        calcx = Math.Log(calcx) / Math.Log(10.0);
                        if (calcx >= DataMinX)
                        {
                            x1 = ToCanvasX(calcx);
                            if (C % MinorTicsPerMajorTic != 0)
                            {
                                DrawLine(tenPen, x1, YAxisCanvas - 7, x1, YAxisCanvas);
                            }
                            else
                            {
                                string tx = (zmin + Convert.ToDouble(C) * zint).ToString(msk);
                                DrawStringLabel(tx, x1, YAxisCanvas - 12, StringAlignment.Center);
                                DrawLine(tenPen, x1, YAxisCanvas - 12, x1, YAxisCanvas);
                            }
                        }
                    }
                }
                if (x1 < XAxisCanvas + XExtCanvas)
                {
                    x1 = XAxisCanvas + XExtCanvas;
                }
                DrawLine(tenPen, XAxisCanvas, YAxisCanvas, x1, YAxisCanvas);
                tenPen.Dispose();
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
            SH_TX = new string[lines];
            for (int c = 0; c <= SH_TX.GetUpperBound(0); c++)
            {
                SH_TX[c] = String.Empty.PadLeft(85);
            }
        }


        // TRANSMISSINGCOMMENT: Method ASCII_PlotPoint
        private void ASCII_PlotPoint(int x, int y)
        {
            // Check if a point has already been plotted
            switch (SH_TX[y][x])
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
                    WriteAsciiYX(y, x, (char)(SH_TX[y][x] + 1));
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
                SetSeriesFromMarkerTypeAndOptions(ds, _MarkerTypes[mkr], null);
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
                    SetSeriesFromMarkerTypeAndOptions(ds, _MarkerTypes[mkr], opts);
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
                ScaleYAxis = 1 + (k - 10) / 20;
                if (ScaleYAxis > 5)
                {
                    ScaleYAxis = 5;
                }
                MetaH = ScaleYAxis * DEFAULT_METAH;
            }
            else
            {
                ScaleYAxis = 1;
                MetaH = DEFAULT_METAH;
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
                AxisLabelFont = FontFromSaveString(bwOptions.AxisLabelFontDescriptor);
            }
            if (!(string.IsNullOrEmpty(bwOptions.AxisFontDescriptor)))
            {
                AxisTitleFont = FontFromSaveString(bwOptions.AxisFontDescriptor);
            }
            if (!(string.IsNullOrEmpty(bwOptions.TitleFontDescriptor)))
            {
                TitleFont = FontFromSaveString(bwOptions.TitleFontDescriptor);
            }

            // Draw the scale
            DefaultAxes();
            AssignMarkersToSeries();
            double xtra = 0;
            foreach (Series s in SeriesToUse)
            {
                double w = Canvas.MeasureString(s.Title, AxisLabelFont).Width + 20;
                if (w > xtra + XAxisCanvas)
                {
                    xtra = w - XAxisCanvas;
                }
            }
            DrawAxes(definition.ChartOptions.Title, new Axis(axisTitle, AxisMode.Scale, 0, definition.ScaleParameters.X.ScaleType), new Axis(null, AxisMode.Series, xtra, definition.ScaleParameters.Y.ScaleType), false, true, false);

            divx = axisXMax - axisXMin;
            offx = -(axisXMin / divx * XExtCanvas) + XAxisCanvas;
            divy = SeriesToUse.Count;
            offy = -(0 / divy * YExtCanvas) + YAxisCanvas;

            Pen blackPen = GetPen(_MarkerTypes[10], true);
            Pen dottedBlackPen = GetPen(_MarkerTypes[10], true);
            dottedBlackPen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dot;

            // work through the columns
            for (int c = 0; c <= SeriesToUse.Count - 1; c++)
            {
                DoubleSeries s = SeriesToUse[c].AsDoubleSeries;
                double centre = 0; double boxL = 0; double boxR = 0;
                double innerFenceL = 0; double innerFenceR = 0;
                double outerFenceL = 0; double outerFenceR = 0;
                double otherMark = 0;
                bool centreIsMedian = false;
                PlotBoxWhiskerCalc(s, bwOptions.Method, P, ref centre, ref boxL, ref boxR, ref innerFenceL, ref innerFenceR, bwOptions.UseInnerFence, ref outerFenceL, ref outerFenceR, bwOptions.UseOuterFence, ref otherMark, ref centreIsMedian);

                // Plot graphic
                double yctr = (c + 0.5) / divy * YExtCanvas;
                double ytop = (c + 1) / divy * YExtCanvas;
                double centreX = ToCanvasX(centre);

                double halfBoxHeight = (ytop - yctr) * BOXWHISKER_BOX_FRACTION_OF_SPACE;
                double yc = offy + yctr;
                double yt = yc + halfBoxHeight;
                double yb = yc - halfBoxHeight;

                double boxLX = ToCanvasX(boxL);
                double boxRX = ToCanvasX(boxR);

                // Draw marker, centre line and box
                PushContainer();

                PushContainer();
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
                PopContainer();

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
                    PushContainer();
                    DrawLine(blackPen, minWhiskerLX, yt, minWhiskerLX, yb);
                    if (shouldDrawOuterBracketL)
                    {
                        DrawLine(blackPen, minWhiskerLX + BOXWHISKER_WHISKER_END_LENGTH, yt, minWhiskerLX, yt);
                        DrawLine(blackPen, minWhiskerLX, yb, minWhiskerLX + BOXWHISKER_WHISKER_END_LENGTH, yb);
                    }
                    PopContainer();
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
                    PushContainer();
                    DrawLine(blackPen, maxWhiskerRX, yt, maxWhiskerRX, yb);
                    if (shouldDrawOuterBracketR)
                    {
                        DrawLine(blackPen, maxWhiskerRX - BOXWHISKER_WHISKER_END_LENGTH, yt, maxWhiskerRX, yt);
                        DrawLine(blackPen, maxWhiskerRX, yb, maxWhiskerRX - BOXWHISKER_WHISKER_END_LENGTH, yb);
                    }
                    PopContainer();
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
                PopContainer();
            }

            blackPen.Dispose();
            dottedBlackPen.Dispose();
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
                ScaleXAxis = 1 + (k - 10) / 20;
                if (ScaleXAxis > 5)
                {
                    ScaleXAxis = 5;
                }
                MetaW = ScaleXAxis * DEFAULT_METAW;
            }
            else
            {
                ScaleXAxis = 1;
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
                AxisLabelFont = FontFromSaveString(bwOptions.AxisLabelFontDescriptor);
            }
            if (!(string.IsNullOrEmpty(bwOptions.AxisFontDescriptor)))
            {
                AxisTitleFont = FontFromSaveString(bwOptions.AxisFontDescriptor);
            }
            if (!(string.IsNullOrEmpty(bwOptions.TitleFontDescriptor)))
            {
                TitleFont = FontFromSaveString(bwOptions.TitleFontDescriptor);
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
            Q_AxisOrFromDefinition(ref min, ref max, out div, ref zmin, ref zint, out MinorTicsPerMajorTic, true, definition.ScaleParameters.Y.ScaleType, false);
            string msk = AxisMaskOrFromDefinition(zint, zmin, div, MinorTicsPerMajorTic, true, definition.ScaleParameters.Y.ScaleType, false);

            float xtra = 0;
            float w = Canvas.MeasureString(min.ToString(msk), AxisLabelFont).Width;
            //  Allow 20 units for axes; if we need more, offset the axis
            if (w - 20 > xtra)
            {
                xtra = w - 20;
            }
            w = Canvas.MeasureString(max.ToString(msk), AxisLabelFont).Width;
            if (w - 20 > xtra)
            {
                xtra = w - 20;
            }
            //  Offset the axis label

            DrawAxes(definition.ChartOptions.Title, new Axis(null, AxisMode.Series, 0, definition.ScaleParameters.X.ScaleType), new Axis(axisTitle, AxisMode.Scale, xtra, definition.ScaleParameters.Y.ScaleType), false, true, false);

            divy = axisYMax - axisYMin;
            offy = -(axisYMin / divy * YExtCanvas) + YAxisCanvas;
            divx = SeriesToUse.Count;
            offx = -(0 / divx * XExtCanvas) + XAxisCanvas;

            Pen blackPen = GetPen(_MarkerTypes[10], true);
            Pen dottedBlackPen = GetPen(_MarkerTypes[10], true);
            dottedBlackPen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dot;

            // work through the columns
            for (int c = 0; c < SeriesToUse.Count; c++)
            {
                DoubleSeries s = SeriesToUse[c].AsDoubleSeries;
                double centre = 0; double boxB = 0; double boxT = 0;
                double innerFenceB = 0; double innerFenceT = 0;
                double outerFenceB = 0; double outerFenceT = 0;
                double otherMark = 0;
                bool CentreIsMedian = false;
                PlotBoxWhiskerCalc(s, bwOptions.Method, P, ref centre, ref boxB, ref boxT, ref innerFenceB, ref innerFenceT, bwOptions.UseInnerFence, ref outerFenceB, ref outerFenceT, bwOptions.UseOuterFence, ref otherMark, ref CentreIsMedian);

                // Plot graphic
                double xctr = (c + 0.5) / divx * XExtCanvas;
                double xright = (c + 1) / divx * XExtCanvas;
                double centreY = ToCanvasY(centre);

                double halfBoxWidth = (xright - xctr) * BOXWHISKER_BOX_FRACTION_OF_SPACE;
                double xc = offx + xctr;
                double xr = xc + halfBoxWidth;
                double xl = xc - halfBoxWidth;

                double boxBY = ToCanvasY(boxB);
                double boxTY = ToCanvasY(boxT);

                // Draw marker, centre line and box
                PushContainer();

                PushContainer();

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
                PopContainer();

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
                    PushContainer();
                    DrawLine(blackPen, xr, minWhiskerBY, xl, minWhiskerBY);
                    if (shouldDrawOuterBracketB)
                    {
                        DrawLine(blackPen, xr, minWhiskerBY + BOXWHISKER_WHISKER_END_LENGTH, xr, minWhiskerBY);
                        DrawLine(blackPen, xl, minWhiskerBY, xl, minWhiskerBY + BOXWHISKER_WHISKER_END_LENGTH);
                    }
                    PopContainer();
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
                    PushContainer();
                    DrawLine(blackPen, xr, maxWhiskerTY, xl, maxWhiskerTY);
                    if (shouldDrawOuterBracketT)
                    {
                        DrawLine(blackPen, xr, maxWhiskerTY - BOXWHISKER_WHISKER_END_LENGTH, xr, maxWhiskerTY);
                        DrawLine(blackPen, xl, maxWhiskerTY, xl, maxWhiskerTY - BOXWHISKER_WHISKER_END_LENGTH);
                    }
                    PopContainer();
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
                PopContainer();
            }

            blackPen.Dispose();
            dottedBlackPen.Dispose();
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

            if (SH_TX[0].Length > bwOptions.XAxisTitle.Length)
            {
                WriteAsciiYX(0, 45 - bwOptions.XAxisTitle.Length / 2, bwOptions.XAxisTitle);
            }
            else
            {
                //  Axis title is larger than the chart, so replace the entire first string
                SH_TX[0] = bwOptions.XAxisTitle;
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
            MemoryStream scratchStream = new MemoryStream();
            StartMetafile(scratchStream, true);
            SetFontsAndThicknessesFromOptions(bOptions);
            DefaultAxes();
            double legendFontHeight = LegendFont.GetHeight(Canvas);
            EndMetafile();
            scratchStream.Dispose();

            //  By now, all measurements are known.  Set up the plot areas.
            double legendTop = YAxisCanvas - LEGEND_TOP_GAP;
            double legendRowHeight = Math.Max(LEGEND_MARKER_SIZE, Convert.ToInt32(legendFontHeight));
            double legendSpacing = MINIMUM_LEGEND_GAP + legendRowHeight;
            if (bOptions.ShowLegend && bOptions.ShowLegendIsRelevant)
            {
                double legendBottom = legendTop - (seriesToUse.Count * legendSpacing);
                if (legendBottom < LOWEST_ALLOWED_LEGEND)
                {
                    double extraSpaceRequired = LOWEST_ALLOWED_LEGEND - legendBottom;

                    //  Add in the extra space
                    MetaH += extraSpaceRequired;
                    YAxisCanvas += extraSpaceRequired;
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
                offx = -(axisXMin / divx * XExtCanvas) + XAxisCanvas;
                divy = ((DoubleSeries)(seriesToUse[0])).Points;
                offy = -(0 / divy * YExtCanvas) + YAxisCanvas;

                double eachAreaHeight = YExtCanvas / divy;
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
                                    double barW = dataW / divx * XExtCanvas;
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
                            FillRectangle(barBrush, XAxisCanvas, legendTop - (c * legendSpacing), legendRowHeight, legendRowHeight);
                        }
                        if (!(definition.ChartOptions.UseColour))
                        {
                            DrawRectangle(barPen, XAxisCanvas, legendTop - (c * legendSpacing), legendRowHeight, legendRowHeight);
                        }
                        DrawStringLegendL(definition.XSeries[c].Title, XAxisCanvas + 9 + legendRowHeight, legendTop - (c * legendSpacing));
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
                Q_AxisOrFromDefinition(ref min, ref max, out div, ref zmin, ref zint, out MinorTicsPerMajorTic, true, definition.ScaleParameters.Y.ScaleType, false);
                string msk = AxisMaskOrFromDefinition(zint, zmin, div, MinorTicsPerMajorTic, true, definition.ScaleParameters.Y.ScaleType, false);

                double xtra = 0;
                double w = Canvas.MeasureString(min.ToString(msk), AxisLabelFont).Width;
                //  Allow 20 units for axes; if we need more, offset the axis
                if (w - 20 > xtra)
                {
                    xtra = w - 20;
                }
                w = Canvas.MeasureString(max.ToString(msk), AxisLabelFont).Width;
                if (w - 20 > xtra)
                {
                    xtra = w - 20;
                }
                //  Offset the axis label

                DrawAxes(definition.ChartOptions.Title, new Axis(null, AxisMode.Series, xtra, definition.ScaleParameters.X.ScaleType), new Axis(axisTitle, AxisMode.Scale, 0, definition.ScaleParameters.Y.ScaleType), bOptions.ShouldBoxAxes, false, false);
                DrawXSeries(bOptions.SeriesTitles);

                divy = axisYMax - axisYMin;
                offy = -(axisYMin / divy * YExtCanvas) + YAxisCanvas;
                divx = ((DoubleSeries)(seriesToUse[0])).Points;
                offx = -(0 / divx * XExtCanvas) + XAxisCanvas;

                double eachAreaWidth = XExtCanvas / divx;
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
                                    double barH = dataH / divy * YExtCanvas;
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
                            DrawRectangle(barPen, XAxisCanvas, legendTop - (c * legendSpacing), legendRowHeight, legendRowHeight);
                        }
                        else
                        {
                            FillRectangle(barBrush, XAxisCanvas, legendTop - (c * legendSpacing), legendRowHeight, legendRowHeight);
                        }
                        DrawStringLegendL(definition.YSeries[c].Title, XAxisCanvas + 9 + legendRowHeight, legendTop - (c * legendSpacing));
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
                        Pen tenPen = new Pen(grBlack, 1);
                        DrawLine(tenPen, x, YAxisCanvas, x, YAxisCanvas + YExtCanvas);
                        tenPen.Dispose();
                    }
                    if (definition.ScaleParameters.Y.HasMarkerLine)
                    {
                        double y = ToCanvasY(definition.ScaleParameters.Y.MarkerLineValue);
                        Pen tenPen = new Pen(grBlack, 1);
                        DrawLine(tenPen, XAxisCanvas, y, XAxisCanvas + XExtCanvas, y);
                        tenPen.Dispose();
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
            pt[0].Y = Convert.ToSingle(MetaH - y);
            pt[1].X = Convert.ToSingle(x);
            pt[1].Y = Convert.ToSingle(MetaH - (y - size2));
            pt[2].X = Convert.ToSingle(x + size2);
            pt[2].Y = Convert.ToSingle(MetaH - y);
            pt[3].X = Convert.ToSingle(x);
            pt[3].Y = Convert.ToSingle(MetaH - (y + size2));
            pt[4].X = Convert.ToSingle(x - size2);
            pt[4].Y = Convert.ToSingle(MetaH - y);
            if (Fill)
            {
                Brush b = new SolidBrush(p.Color);
                Canvas.FillPolygon(b, pt);
                b.Dispose();
            }
            // Draw the diamond
            Canvas.DrawPolygon(p, pt);
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
            pt[0].Y = Convert.ToSingle(MetaH - (y - size2));
            pt[1].X = Convert.ToSingle(x - size2);
            pt[1].Y = Convert.ToSingle(MetaH - (y + size2));
            pt[2].X = Convert.ToSingle(x + size2);
            pt[2].Y = Convert.ToSingle(MetaH - (y + size2));
            pt[3].X = Convert.ToSingle(x + size2);
            pt[3].Y = Convert.ToSingle(MetaH - (y - size2));
            pt[4].X = Convert.ToSingle(x - size2);
            pt[4].Y = Convert.ToSingle(MetaH - (y - size2));
            if (Fill)
            {
                Canvas.FillPolygon(BlackBrush, pt);
            }
            Canvas.DrawPolygon(p, pt);
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
                    MetaH = MetaH * SeriesToUse.Count;

                    StartMetafile(OutputStream, true);

                    originalMarkerTypes = _MarkerTypes;
                    _MarkerTypes = new MarkerType[originalMarkerTypes.Length];
                    for (int i = 0; i <= originalMarkerTypes.Length - 1; i++)
                    {
                        _MarkerTypes[i] = originalMarkerTypes[i].Clone();
                        _MarkerTypes[i].Width = histOptions.LineWidth;
                    }
                    AssignMarkersToSeries(SeriesToUse);

                    if (!(string.IsNullOrEmpty(histOptions.AxisFontDescriptor)))
                    {
                        AxisLabelFont = FontFromSaveString(histOptions.AxisFontDescriptor);
                    }
                    if (!(string.IsNullOrEmpty(histOptions.AxisTitleFontDescriptor)))
                    {
                        AxisTitleFont = FontFromSaveString(histOptions.AxisTitleFontDescriptor);
                    }
                    if (!(string.IsNullOrEmpty(histOptions.TitleFontDescriptor)))
                    {
                        TitleFont = FontFromSaveString(histOptions.TitleFontDescriptor);
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
                        double heightPerChart = MetaH / SeriesToUse.Count; //  Should end up as the old MetaH
                        double thisChartTop = MetaH - (iter * heightPerChart);
                        double thisChartBottom = thisChartTop - heightPerChart;

                        //  No longer the default Y axis!
                        DefaultAxes();
                        YAxisCanvas = thisChartBottom + Math.Min(Math.Floor(MetaH / 8), DEFAULT_Y_GAP);
                        YExtCanvas = heightPerChart - Math.Min(heightPerChart / 4, 2 * DEFAULT_Y_GAP) * ScaleYAxis;

                        //  Ensure the normal curve doesn't fall off the top of the Y axis
                        if (overlayNormalCurve)
                        {
                            double mxy = PlotCurveMax(zmin, zint, mp - 1, s);
                            if (mxy > dataMaxY)
                            {
                                dataMaxY = mxy;
                            }
                        }

                        divx = XExtCanvas / Math.Max(mp, 1);
                        offx = 0; //  was CInt(divx * 0.1) but bars are now full-width
                        divy = axisYMax - axisYMin;
                        offy = -(axisYMin / divy * YExtCanvas) + YAxisCanvas;
                        double barx = divx; //  was CInt(divx * 0.9) but bars are now full-width
                        double ctrx = divx / 2.0;
                        SizeF legendSize = Canvas.MeasureString(midpt[mp].ToString(), LegendFont);
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
                            double x1 = XAxisCanvas + divx * (C - 1) + offx;
                            double x2 = x1 + barx;
                            double value = showRelativeFrequencies ? size[C] / (double)s.Points : size[C];
                            double y1 = YAxisCanvas + (value / axisYMax * YExtCanvas);
                            double y2 = YAxisCanvas;
                            DrawRectangle(s.UnstyledPen, x1, y1, x2 - x1, y1 - y2);

                            // Now the mid-point label in the centre of the bar
                            x1 += ctrx;
                            double yoff = AxisLabelFont.SizeInPoints;
                            if (labelsAreLong)
                            {
                                if (labelIsLow)
                                {
                                    yoff = AxisLabelFont.SizeInPoints * 2.5;
                                }
                                labelIsLow = !(labelIsLow);
                            }
                            AxisDrawStringC(midpt[C].ToString(msk), x1, YAxisCanvas - yoff);
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
                        SH_TX[2] = "     " + SH_TX[2].Substring(0, 85);
                        SH_TX[1] = "     " + SH_TX[1].Substring(0, 85);
                        SH_TX[0] = "     " + SH_TX[0].Substring(0, 85);

                        //  Save this plot
                        for (int i = SH_TX.Length - 1; i >= 0; i--)
                        {
                            savedLines.Insert(0, SH_TX[i]);
                        }
                        //  Separator
                        savedLines.Insert(0, "");
                    }
                }

                if (!(IsAscii))
                {
                    MaybeDrawMarkerLines();
                    EndMetafile();

                    MetaH = DEFAULT_METAH;
                }
                else
                {
                    //  Fill in the output in its expected place from our saved place
                    SH_TX = new string[savedLines.Count];
                    for (int i = 0; i <= savedLines.Count - 1; i++)
                    {
                        SH_TX[i] = savedLines[i];
                    }
                }

                return new ParameterBag();
            }
            finally
            {
                if (originalMarkerTypes != null)
                {
                    //  TODO: Resource leak on pens?
                    _MarkerTypes = originalMarkerTypes;
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
        public Image PlotXYAndReturnImage(ITemplateHost host, double[] x, double[] y, string xtxt, string ytxt, string title, bool zPlot, int minMaxY, bool useCalculatedScalesEvenWithDefinition)
        {
            Stream metaStream = new MemoryStream();
            StartMetafile(metaStream, true);
            PlotXY(x, y, xtxt, ytxt, title, zPlot, minMaxY, 6, MarkerShape.Circle, false, Pens.Black, useCalculatedScalesEvenWithDefinition);
            EndMetafile();
            metaStream.Position = 0;
            return Image.FromStream(metaStream);
        }


        // TRANSMISSINGCOMMENT: Method PlotXYZAndReturnImage
        public Image PlotXYZAndReturnImage(double[] x, double[] y, double[] z, string xtxt, string ytxt, string title, bool zPlot, int minMaxY)
        {
            Stream metaStream = new MemoryStream();
            StartMetafile(metaStream, true);
            PlotXYZ(x, y, z, 1, x.Length - 1, xtxt, ytxt, title, zPlot, minMaxY, MarkerShape.Circle, false, Pens.Black, null);
            EndMetafile();
            metaStream.Position = 0;
            return Image.FromStream(metaStream);
        }


        ///  <summary>
        ///  Draw a horizontal line at the specified offset from the Y origin.
        ///  </summary>
        ///  <param name="y">The offset, in device units</param>
        ///  <remarks></remarks>
        private void DrawQCanvas(double y)
        {
            Pen greenPen = new Pen(grGreen, 2);
            DrawLine(greenPen, XAxisCanvas, y, XAxisCanvas + XExtCanvas, y);
            greenPen.Dispose();
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
            int div = Convert.ToInt32(XExtCanvas / count / 5);
            count *= div;
            zint = zint / Convert.ToDouble(div);

            //  Move the cursor to the start
            double y = bins * Math.Exp(-0.5 * Math.Pow(((sumx - xbar) / sdv), 2.0)) * proportionScaler;
            double yold = ToCanvasY(y);
            double xold = XAxisCanvas;

            for (int C = 1; C <= count; C++)
            {
                sumx += zint;
                y = bins * Math.Exp(-0.5 * Math.Pow(((sumx - xbar) / sdv), 2.0)) * proportionScaler;
                double y1 = ToCanvasY(y);
                double x1 = XAxisCanvas + (C / (double)count * XExtCanvas);
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
            int div = Convert.ToInt32(XExtCanvas / count / 5);
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
                ScaleYAxis = 1 + (k - 10) / 20;
                if (ScaleYAxis > 5)
                {
                    ScaleYAxis = 5;
                }
                MetaH = ScaleYAxis * DEFAULT_METAH;
            }
            else
            {
                ScaleYAxis = 1;
                MetaH = DEFAULT_METAH;
            }

            GetMinMaxSort(SeriesToUse, out dataMinX, out dataMaxX);

            DefaultAxes();

            StartMetafile(OutputStream, true);
            SetFontsAndThicknessesFromOptions(sOptions);
            AssignMarkersToSeries(sOptions);
            foreach (Series s in SeriesToUse)
            {
                double w = LegendWidth(s.Title) + 20;
                if (w > xtra + XAxisCanvas)
                {
                    xtra = w - XAxisCanvas;
                }
            }

            // Draw the scale
            DrawAxes(sOptions.Title, new Axis(sOptions.XAxisTitle, AxisMode.Scale, 0, definition.ScaleParameters.X.ScaleType), new Axis(null, AxisMode.Series, xtra, definition.ScaleParameters.Y.ScaleType), sOptions.ShouldBoxAxes, true, false);

            // get the offsets for the Markers
            divx = axisXMax - axisXMin;
            offx = -(axisXMin / divx * XExtCanvas) + XAxisCanvas;
            divy = SeriesToUse.Count;
            offy = YAxisCanvas;
            double ygap = YExtCanvas / divy;

            //  Work out what markers to use
            MarkerType mt = MarkerTypes[10]; //  Default
            double diam = mt.MarkerSize;
            if ((sOptions.MarkerTypes != null) && sOptions.MarkerTypes.Count > 0)
            {
                diam = sOptions.MarkerTypes[0].MarkerSize;
                mt = sOptions.MarkerTypes[0];
            }

            double INC = 2 * diam;
            double xxwid = divx / (XExtCanvas / INC);
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
                ScaleXAxis = 1 + (k - 10) / 20;
                if (ScaleXAxis > 5)
                {
                    ScaleXAxis = 5;
                }
                MetaW = ScaleXAxis * DEFAULT_METAW;
            }
            else
            {
                ScaleXAxis = 1;
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
            offy = -(axisYMin / divy * YExtCanvas) + YAxisCanvas;
            divx = seriesToUse.Count;
            offx = XAxisCanvas;
            double xgap = XExtCanvas / divx;

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
            double yywid = divy / (YExtCanvas / INC);
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
            MemoryStream scratchStream = new MemoryStream();
            StartMetafile(scratchStream, true);
            SetFontsAndThicknessesFromOptions(rOptions);
            DefaultAxes();
            double smallerExt = Math.Min(XExtCanvas, YExtCanvas);
            XExtCanvas = smallerExt;
            YExtCanvas = smallerExt;
            double legendFontHeight = LegendFont.GetHeight(Canvas);
            EndMetafile();
            scratchStream.Dispose();

            //  By now, all measurements are known.  Set up the plot areas.
            double legendTop = YAxisCanvas - LEGEND_TOP_GAP;
            double markerMidlineOffset = (legendFontHeight - LEGEND_MARKER_SIZE) / 2;
            double legendSpacing = MINIMUM_LEGEND_GAP + Math.Max(LEGEND_MARKER_SIZE, Convert.ToInt32(legendFontHeight));
            double legendBottom = legendTop - (definition.XSeries.Count * legendSpacing);
            if (legendBottom < LOWEST_ALLOWED_LEGEND)
            {
                double extraSpaceRequired = LOWEST_ALLOWED_LEGEND - legendBottom;

                //  Add in the extra space
                MetaH += extraSpaceRequired;
                YAxisCanvas += extraSpaceRequired;
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
            Pen tenPenDiagonal = new Pen(_MarkerTypes[10].Color, rOptions.AxisLineThickness);
            DrawLine(tenPenDiagonal, XAxisCanvas, YAxisCanvas, XAxisCanvas + XExtCanvas, YAxisCanvas + YExtCanvas);
            tenPenDiagonal.Dispose();

            // get the offsets for the Markers
            offx = XAxisCanvas;
            offy = YAxisCanvas;

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
                DrawMarker(XAxisCanvas + LEGEND_MARKER_SIZE / 2.0, legendTop - (cs * legendSpacing) - markerMidlineOffset, LEGEND_MARKER_SIZE, definition.YSeries[cs].AsDoubleSeries);
                DrawStringLegendL(rOptions.SeriesTitles[cs], XAxisCanvas + 9 + LEGEND_MARKER_SIZE, legendTop - (cs * legendSpacing));

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
                double x1 = offx + mspec * XExtCanvas;
                double y1 = offy + sens * YExtCanvas;

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
                    x2 = offx + rx[r] * XExtCanvas;
                    Y2 = offy + ry[r] * YExtCanvas;
                    DrawMarker(x2, Y2, ys.MarkerSize, definition.YSeries[cs].AsDoubleSeries);
                }

                double last_x2 = x1;
                double last_y2 = y1;
                for (int r = 0; r <= stps - 1; r++)
                {
                    x2 = offx + rx[r] * XExtCanvas;
                    Y2 = offy + ry[r] * YExtCanvas;
                    if (r > 0 & (x2 != last_x2 | Y2 != last_y2))
                    {
                        DrawLine(xs.StyledPen, last_x2, last_y2, x2, Y2);
                    }
                    last_x2 = x2;
                    last_y2 = Y2;
                }

                // mark cutoff point
                x2 = offx + (1.0 - thisData.spec) * XExtCanvas;
                Y2 = offy + thisData.sens * YExtCanvas;
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
            Plot_Normal(host, y);
            EndMetafile();
            return new ParameterBag();
        }


        ///  <summary>
        ///  Plot normal scores for a single variable in XSeries.
        ///  </summary>
        ///  <remarks></remarks>
        public void Plot_Normal(ITemplateHost host, double[] y)
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
                // Dim tenPen As Pen = GetPen(MarkerTypes(10), True)
                DrawLine(AxisPen, XAxisCanvas, YAxisCanvas, XAxisCanvas + XExtCanvas, YAxisCanvas + YExtCanvas);
                // tenPen.Dispose()
            }
        }


        // TRANSMISSINGCOMMENT: Method GetPyramidScaleParameters
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
                ScaleYAxis = 1 + (nmale - 10) / 20;
                if (ScaleYAxis > 5)
                {
                    ScaleYAxis = 5;
                }
                MetaH = ScaleYAxis * DEFAULT_METAH;
            }
            else
            {
                ScaleYAxis = 1;
                MetaH = DEFAULT_METAH;
            }

            StartMetafile(OutputStream, true);

            SetFontsAndThicknessesFromOptions(pOptions);

            //  Init_Axes()
            double xtra = 0;
            for (int i = 0; i <= nmale - 1; i++)
            {
                double w = AxisLabelWidth(title[i]) + MINIMUM_X_WHITESPACE;
                if (w > xtra + XAxisCanvas)
                {
                    xtra = w - XAxisCanvas - 5;
                }
            }

            XAxisCanvas = XAxisCanvas + xtra;
            XExtCanvas = XExtCanvas - xtra;

            StringFormat CenterFormat = new StringFormat { Alignment = StringAlignment.Center };

            DrawString(pOptions.Title, TitleFont, Brushes.Black, (XExtCanvas / 2) + XAxisCanvas, YExtCanvas + 180, CenterFormat);

            StringFormat RightFormat = new StringFormat { Alignment = StringAlignment.Far };

            double ystep = YExtCanvas / nmale;
            if (title[0].Length > 0)
            {
                double txh = AxisLabelHeight(title[0]);
                for (int i = 0; i <= nmale - 1; i++)
                {
                    double yc = YAxisCanvas + (nmale - i) * ystep - ystep / 2;
                    DrawString(title[i], AxisLabelFont, Brushes.Black, XAxisCanvas - 15, yc + txh / 2, RightFormat);
                }
            }

            double xstep = XExtCanvas / 2;
            double xc = XAxisCanvas + xstep;
            Pen blackPen = GetPen(_MarkerTypes[10], true);
            for (int i = 0; i <= nmale - 1; i++)
            {
                double yt = YAxisCanvas + (nmale - i) * ystep;
                double yb = YAxisCanvas + (nmale - i - 1) * ystep;
                double xl = XAxisCanvas + xstep - (male[i] / ScaleMax) * xstep;
                double xr = XAxisCanvas + xstep + (female[i] / ScaleMax) * xstep;
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

            StringFormat LeftFormat = new StringFormat { Alignment = StringAlignment.Near };


            if (mode == 1)
            {
                DrawLine(blackPen, xc, YAxisCanvas, XAxisCanvas + xstep, YAxisCanvas + nmale * ystep);
                DrawString("male", AxisLabelFont, Brushes.Black, (XExtCanvas / 4) + XAxisCanvas, YAxisCanvas - 12, LeftFormat);
                DrawString("female", AxisLabelFont, Brushes.Black, (XExtCanvas / 4) + (XExtCanvas / 2) + XAxisCanvas, YAxisCanvas - 12, LeftFormat);
            }

            DrawString("Scale maximum = " + ScaleMax.ToString(), AxisLabelFont, Brushes.Black, 40, YAxisCanvas - 40, LeftFormat);

            EndMetafile();
            ScaleYAxis = 1;
            MetaH = DEFAULT_METAH;
            return new ParameterBag();
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


        // TRANSMISSINGCOMMENT: Method PlotXYR
        public void PlotXYR(double[,] x, double[, ,] y, int ng, int[] gn, int[,] nr, double[] b, double[] a, string xtxt, string ytxt, string title, string[] bnam)
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
                double w = Canvas.MeasureString(bnam[g], LegendFont).Width + 55;
                if (w > xtra + XAxisCanvas)
                {
                    xtra = w - XAxisCanvas;
                }
            }
            DrawAxes(title, new Axis(xtxt, AxisMode.Scale, 0, ScaleType.Linear), new Axis(ytxt, AxisMode.Scale, xtra, ScaleType.Linear), false, true, false);

            double size2 = LabelFont.Size * 2;

            // Draw the legends
            if (ng > 1)
            {
                for (g = 1; g <= ng; g++)
                {
                    string transTemp30 = bnam[g];
                    if (  /* TRANSINFO: .NET Equivalent of Microsoft.VisualBasic NameSpace */ transTemp30.Length > 0)
                    {
                        mkr = ChartOptions.SeriesNumberToMarkerNumber(g - 1);
                        DrawMarker(LEGEND_MARKER_X, YAxisCanvas + YExtCanvas - LEGEND_MARKER_Y_OFFSET - (size2 * g), LEGEND_MARKER_SIZE, _MarkerTypes[mkr]); //  TODO: Broken?
                        DrawStringLegendL(bnam[g], LEGEND_TEXT_X, YAxisCanvas + YExtCanvas - 10 - (size2 * g));
                    }
                }
            }

            // get the offsets for the Graph
            SetStandardScaling();

            // Plot the points
            for (g = 1; g <= ng; g++)
            {
                mkr = ChartOptions.SeriesNumberToMarkerNumber(g - 1);
                MarkerType t = _MarkerTypes[mkr];
                Pen p = GetPen(t, true);
                double minx = double.MaxValue;
                double maxx = double.MinValue;
                double miny = double.MaxValue;
                double maxy = double.MinValue;
                int r;
                double x1;
                double y1;
                for (r = 1; r <= gn[g]; r++)
                {
                    PointF[] xys = new PointF[nr[g, r] - 1 + 1 /* for VB to C# conversion */ ];
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
                p.Dispose();
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
                Pen blackPen = GetPen(_MarkerTypes[10], true);
                DrawLine(blackPen, XAxisCanvas, YAxisCanvas, XAxisCanvas + XExtCanvas, YAxisCanvas + YExtCanvas);
                // pooled event rate
                Pen blackFXPen = GetPen(_MarkerTypes[10], false);
                double x1; double y1;
                if (rmh >= 1)
                {
                    y1 = YAxisCanvas + YExtCanvas;
                    x1 = ToCanvasX(axisYMax / rmh);
                }
                else
                {
                    x1 = XAxisCanvas + XExtCanvas;
                    y1 = ToCanvasY(rmh * axisXMax);
                }
                DrawLine(blackFXPen, XAxisCanvas, YAxisCanvas, x1, y1);
                blackPen.Dispose();
                blackFXPen.Dispose();
            }
        }

        ///  <summary>
        ///  
        ///  </summary>
        ///  <param name="k">The number of elements in o(,)</param>
        ///  <param name="o">A 1-based array of values</param>
        ///  <param name="rmh"></param>
        ///  <remarks></remarks>
        public void PlotLAbbe(int k, double[,] o, double rmh)
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
            PlotXYZ(x, y, w, 1, k, "control percent", "experimental percent", "L'Abbe plot (symbol size represents sample size)", false, 0, _MarkerTypes[0].Shape, _MarkerTypes[0].IsFilled, GetPen(_MarkerTypes[0], true), rmh);
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
            offy = -(axisYMin / divy * YExtCanvas) + YAxisCanvas;
            double x1 = XAxisCanvas + (XExtCanvas * 0.25);
            double x2 = XAxisCanvas + (XExtCanvas * 0.75);

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
            MarkerType rungMarkerType = _MarkerTypes[10];
            if ((lOptions.MarkerTypes != null) && lOptions.MarkerTypes.Count >= 1 && lOptions.MarkerTypes[0] != null)
            {
                rungMarkerType = lOptions.MarkerTypes[0];
            }
            Pen rungPen = new Pen(Color.Black, rungMarkerType.Width) { DashStyle = rungMarkerType.Style };

            for (int r = 0; r <= s0.Points - 1; r++)
            {
                if (s0.Data[r] != Constant.MISSING && s1.Data[r] != Constant.MISSING)
                {
                    double y1 = ToCanvasY(s0.Data[r]);
                    double Y2 = ToCanvasY(s1.Data[r]);
                    DrawLine(rungPen, x1, y1, x2, Y2);
                }
            }
            rungPen.Dispose();
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
                XExtCanvas -= RHS_LABEL_GAP + LegendWidth(Math.Round(ymean + ysd * 3.0, cOptions.RightHandDecimalPlaces) + " (+3 SD)");
            }
            if (cOptions.UseDates)
            {
                float vshift = AxisLabelWidth(new DateTime(1899, 12, 30, 0, 0, 0).AddDays(xdat[0]).ToString("d")) + 30;
                YAxisCanvas += vshift;
                YExtCanvas -= vshift;
            }

            double xtra = 0;
            double w = TitleWidth(cOptions.YAxisTitle) + 30;
            if (w > xtra + XAxisCanvas)
            {
                xtra = w - XAxisCanvas;
            }

            XAxisCanvas = XAxisCanvas + xtra;
            XExtCanvas = XExtCanvas - xtra;

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
                        y1 = YAxisCanvas - AxisLabelWidth(tx) - AxisBigTick - 3;
                        DrawVerticalAxisLabel(tx, StringAlignment.Near, x1 - AxisLabelHeight(tx) / 2, y1);
                    }
                }
            }

            int rhDp = cOptions.RightHandDecimalPlaces;

            Pen blackPen = new Pen(grBlack);
            if (cOptions.HasUserSpecifiedLimits)
            {

                // user specified control and warning lines
                x1 = XAxisCanvas + XExtCanvas;
                y1 = ToCanvasY((cOptions.UpperWarningLimit));
                DrawLine(blackPen, XAxisCanvas, y1, x1, y1);
                string tx = Math.Round(cOptions.UpperWarningLimit, rhDp) + " (warn)";
                DrawStringLegendL(tx, x1 + RHS_LABEL_GAP, y1 + LegendHeight(tx) / 2);
                y1 = ToCanvasY(cOptions.LowerWarningLimit);
                DrawLine(blackPen, XAxisCanvas, y1, x1, y1);
                tx = Math.Round(cOptions.LowerWarningLimit, rhDp) + " (warn)";
                DrawStringLegendL(tx, x1 + RHS_LABEL_GAP, y1 + LegendHeight(tx) / 2);
                Pen redPen = new Pen(grRed);
                y1 = ToCanvasY(cOptions.UpperControlLimit);
                DrawLine(redPen, XAxisCanvas, y1, x1, y1);
                tx = Math.Round(cOptions.UpperControlLimit, rhDp) + " (ctrl)";
                DrawStringLegendL(tx, x1 + RHS_LABEL_GAP, y1 + LegendHeight(tx) / 2);
                y1 = ToCanvasY(ymean - ysd * 3.0);
                DrawLine(redPen, XAxisCanvas, y1, x1, y1);
                tx = Math.Round(cOptions.LowerControlLimit, rhDp) + " (ctrl)";
                DrawStringLegendL(tx, x1 + RHS_LABEL_GAP, y1 + LegendHeight(tx) / 2);
                DrawStringLegendL("External:", x1 + RHS_LABEL_GAP, YAxisCanvas + YExtCanvas);
                redPen.Dispose();
            }
            else
            {
                // draw control lines
                if (cOptions.UseMean)
                {
                    x1 = XAxisCanvas + XExtCanvas;
                    y1 = ToCanvasY(ymean);
                    DrawLine(blackPen, XAxisCanvas, y1, x1, y1);
                    string tx = Math.Round(ymean, rhDp) + " (mean)";
                    DrawStringLegendL(tx, x1 + RHS_LABEL_GAP, y1 + LegendHeight(tx) / 2);
                    if (restricted)
                    {
                        DrawStringLegendL("On first " + kobs.ToString() + " points:", x1 + RHS_LABEL_GAP, YAxisCanvas + YExtCanvas);
                    }
                    else if (external)
                    {
                        DrawStringLegendL("External:", x1 + RHS_LABEL_GAP, YAxisCanvas + YExtCanvas);
                    }
                }

                if (ysd != Constant.MISSING)
                {
                    string tx;
                    if (cOptions.Use1SD)
                    {
                        Pen greenPen = new Pen(grGreen);
                        x1 = XAxisCanvas + XExtCanvas;
                        y1 = ToCanvasY(ymean + ysd);
                        DrawLine(greenPen, XAxisCanvas, y1, x1, y1);
                        tx = Math.Round(ymean + ysd, rhDp) + " (+1 SD)";
                        DrawStringLegendL(tx, x1 + RHS_LABEL_GAP, y1 + LegendHeight(tx) / 2);
                        y1 = ToCanvasY(ymean - ysd);
                        DrawLine(greenPen, XAxisCanvas, y1, x1, y1);
                        tx = Math.Round(ymean - ysd, rhDp) + " (-1 SD)";
                        DrawStringLegendL(tx, x1 + RHS_LABEL_GAP, y1 + LegendHeight(tx) / 2);
                        greenPen.Dispose();
                    }

                    if (cOptions.Use2SD)
                    {
                        x1 = XAxisCanvas + XExtCanvas;
                        y1 = ToCanvasY(ymean + ysd * 2.0);
                        DrawLine(blackPen, XAxisCanvas, y1, x1, y1);
                        tx = Math.Round(ymean + ysd * 2.0, rhDp) + " (+2 SD)";
                        DrawStringLegendL(tx, x1 + RHS_LABEL_GAP, y1 + LegendHeight(tx) / 2);
                        y1 = ToCanvasY(ymean - ysd * 2.0);
                        DrawLine(blackPen, XAxisCanvas, y1, x1, y1);
                        tx = Math.Round(ymean - ysd * 2.0, rhDp) + " (-2 SD)";
                        DrawStringLegendL(tx, x1 + RHS_LABEL_GAP, y1 + LegendHeight(tx) / 2);
                    }

                    if (cOptions.Use3SD)
                    {
                        Pen redPen = new Pen(grRed);
                        x1 = XAxisCanvas + XExtCanvas;
                        y1 = ToCanvasY(ymean + ysd * 3.0);
                        DrawLine(redPen, XAxisCanvas, y1, x1, y1);
                        tx = Math.Round(ymean + ysd * 3.0, rhDp) + " (+3 SD)";
                        DrawStringLegendL(tx, x1 + RHS_LABEL_GAP, y1 + LegendHeight(tx) / 2);
                        y1 = ToCanvasY(ymean - ysd * 3.0);
                        DrawLine(redPen, XAxisCanvas, y1, x1, y1);
                        tx = Math.Round(ymean - ysd * 3.0, rhDp) + " (-3 SD)";
                        DrawStringLegendL(tx, x1 + RHS_LABEL_GAP, y1 + LegendHeight(tx) / 2);
                        redPen.Dispose();
                    }
                }
            }
            if (blackPen != null)
            {
                blackPen.Dispose();
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
            MemoryStream scratchStream = new MemoryStream();
            StartMetafile(scratchStream, true);
            SetFontsAndThicknessesFromOptions(eOptions);
            DefaultAxes();
            double legendFontHeight = LegendFont.GetHeight(Canvas);
            EndMetafile();
            scratchStream.Dispose();

            //  By now, all measurements are known.  Set up the plot areas.
            double legendTop = YAxisCanvas - LEGEND_TOP_GAP;
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
                    MetaH += extraSpaceRequired;
                    YAxisCanvas += extraSpaceRequired;
                    legendTop += extraSpaceRequired;
                    // legendBottom += extraSpaceRequired; 
                }
            }

            StartMetafile(OutputStream, false);
            SetFontsAndThicknessesFromOptions(eOptions);
            AssignMarkersToSeries(eOptions);

            // Draw the scale
            DrawAxes(eOptions.Title, new Axis(eOptions.XAxisTitle, AxisMode.Scale, 0, definition.ScaleParameters.X.ScaleType), new Axis(eOptions.YAxisTitle, AxisMode.Scale, 0, definition.ScaleParameters.Y.ScaleType), BoxAxes, false, false);

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
                Pen p = GetPen(eOptions.MarkerTypes[C], true);
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
                    Pen pStyled = GetPen(eOptions.MarkerTypes[C], false);
                    for (int r = 0; r <= yvar.Length - 1; r++)
                    {
                        x1 = ToCanvasX(xvar.Data[r]);
                        y1 = ToCanvasY(yvar.Data[r]);
                        DrawLine(pStyled, x1, y1, x2, y2);
                        x2 = x1;
                        y2 = y1;
                    }
                    pStyled.Dispose();
                }

                //  Legend
                if (eOptions.ShowLegend && eOptions.ShowLegendIsRelevant)
                {
                    double y = legendTop - (C * legendSpacing);
                    DrawMarker(XAxisCanvas + LEGEND_MARKER_SIZE / 2.0, y - legendFontHeight / 2.0, LEGEND_MARKER_SIZE, eOptions.MarkerTypes[C]);
                    DrawStringLegendL(eOptions.SeriesTitles[C], XAxisCanvas + LEGEND_MARKER_SIZE * 2, y);
                }

                p.Dispose();
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
                ScaleYAxis = 1 + (k - 10) / 20;
                if (ScaleYAxis > 5)
                {
                    ScaleYAxis = 5;
                }
                MetaH = ScaleYAxis * DEFAULT_METAH;
            }
            else
            {
                ScaleYAxis = 1;
                MetaH = DEFAULT_METAH;
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
                    if (w > xtra + XAxisCanvas)
                    {
                        xtra = w - XAxisCanvas - 5;
                    }
                    w = LegendWidth(Formatting.RoundMeta(odr[i], absmin, decimalPlaces) + " (" + Formatting.RoundMeta(odrl[i], absmin, decimalPlaces) + ", " + Formatting.RoundMeta(odru[i], absmin, decimalPlaces) + ")");
                    if (w > rgap)
                    {
                        rgap = w;
                    }
                }
            }
            w = TitleWidth(combo_ti(fOptions.Title)) + 30;
            if (w > xtra + XAxisCanvas)
            {
                xtra = w - XAxisCanvas - 5;
            }
            XExtCanvas = 940 - rgap;

            if (isLogScale)
            {
                //  TODO: Use a proper log scale
                AxisScaler.Q_Axis(ref dataMinX, ref dataMaxX, out XDiv, ref amin, ref aint, out MinorTicsPerMajorTic, ScaleType.Linear);
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
            offx = -(DataMinX / divx * XExtCanvas) + XAxisCanvas;
            divy = kok + pbias;
            offy = YAxisCanvas;

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
                        DrawStringLabel(Lab, xm, YAxisCanvas - 12, StringAlignment.Center);
                        DrawLine(AxisPen, xm, YAxisCanvas - 12, xm, YAxisCanvas);
                    }
                }
            }

            Pen tenPen = GetPen(_MarkerTypes[10], true);
            Pen effectTenPen = GetPen(_MarkerTypes[10], false);
            Pen ciTenPen = new Pen(_MarkerTypes[10].Color, fOptions.StudyCiLineThickness);

            double rmh = -99;
            int r = 0;
            double botlim = isLogScale ? realamin : double.MinValue;
            for (int i = k - 1; i >= 0; i--)
            {
                if (odr[i] != Constant.MISSING)
                {
                    r = r + 1;
                    double yctr = (r + pbias - 0.5) / divy * YExtCanvas;
                    double ytop = (r + pbias) / divy * YExtCanvas;
                    xm = odr[i] < botlim ? XAxisCanvas : ToCanvasX(isLogScale ? Math.Log(odr[i]) : odr[i]);
                    double XL = odrl[i] < botlim ? XAxisCanvas : ToCanvasX(isLogScale ? Math.Log(odrl[i]) : odrl[i]);
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
                    AxisDrawStringAtAngleRM(title[i], XAxisCanvas - 15, yc, definition.ScaleParameters.Y.LabelDirection);
                    DrawStringLabel(Formatting.RoundMeta(odr[i], absmin, decimalPlaces) + " (" + Formatting.RoundMeta(odrl[i], absmin, decimalPlaces) + ", " + Formatting.RoundMeta(odru[i], absmin, decimalPlaces) + ")", XAxisCanvas + XExtCanvas + 10, yc, StringAlignment.Near, StringAlignment.Center);
                }
            }

            if (DataMinX <= 0)
            {
                // no effect marker
                xm = ToCanvasX(isLogScale ? Math.Log(1) : 0);
                DrawLine(tenPen, xm, yt, xm, YAxisCanvas);
            }
            tenPen.Dispose();

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
            MemoryStream scratchStream = new MemoryStream();
            StartMetafile(scratchStream, true);
            SetFontsAndThicknessesFromOptions(sOptions);
            DefaultAxes();
            double smallerExt = Math.Min(XExtCanvas, YExtCanvas);
            XExtCanvas = smallerExt;
            YExtCanvas = smallerExt;
            double legendFontHeight = LegendFont.GetHeight(Canvas);
            EndMetafile();
            scratchStream.Dispose();

            //  By now, all measurements are known.  Set up the plot areas.
            double legendTop = YAxisCanvas - LEGEND_TOP_GAP;
            double markerMidlineOffset = (legendFontHeight - LEGEND_MARKER_SIZE) / 2;
            double legendSpacing = MINIMUM_LEGEND_GAP + Math.Max(LEGEND_MARKER_SIZE, Convert.ToInt32(legendFontHeight));
            double legendBottom = legendTop - (definition.XSeries.Count * legendSpacing);
            if (sOptions.ShowLegend && legendBottom < LOWEST_ALLOWED_LEGEND)
            {
                double extraSpaceRequired = LOWEST_ALLOWED_LEGEND - legendBottom;

                //  Add in the extra space
                MetaH += extraSpaceRequired;
                YAxisCanvas += extraSpaceRequired;
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
            offx = -(axisXMin / divx * XExtCanvas) + XAxisCanvas;
            divy = cols + 1;
            offy = YAxisCanvas;

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
                Pen p = GetPen(mType, true);

                //  If necessary, draw the marker legend
                if (sOptions.ShowLegend)
                {
                    if (sOptions.SeriesTitles[C].Length > 0)
                    {
                        double markerX = XAxisCanvas + LEGEND_MARKER_SIZE / 2.0;
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
                        DrawStringLegendL(sOptions.SeriesTitles[C], XAxisCanvas + 9 + LEGEND_MARKER_SIZE, legendTop - (C * legendSpacing));
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
                                Pen ciPen = new Pen(ciPenColour, ciMarkerType.Width) { DashStyle = ciMarkerType.Style };


                                double y2l = ToCanvasY(ydat_l[r]);
                                double y2u = ToCanvasY(ydat_u[r]);
                                DrawLine(ciPen, x2, y2l, x2, y2u);
                                ciPen.Dispose();
                            }
                        }
                        // x1 = x2; 
                    }
                }

                p.Dispose();

            }
            EndMetafile();
            return new ParameterBag();
        }


        // TRANSMISSINGCOMMENT: Method GetGiniScaleParameters
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


        // TRANSMISSINGCOMMENT: Method PlotGini
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
            Pen redPen = new Pen(grRed);
            double x1 = ToCanvasX(0);
            double y1 = ToCanvasY(0);
            double x2 = ToCanvasX(1.0);
            double y2 = ToCanvasY(1.0);
            DrawLine(redPen, x1, y1, x2, y2);
            redPen.Dispose();

            // Draw Lorenz polygon
            Pen greenPen = new Pen(grGreen);
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
            greenPen.Dispose();
            EndMetafile();
            return new ParameterBag();
        }


        // TRANSMISSINGCOMMENT: Method Plot_Bias_MA
        public void Plot_Bias_MA(Stream OutputStream, IChartHost Host, double[] x, double[] yy, double[] yw, int rows, string xtxt, double[] cl, double[] cu, double cco, double cit, double rmh, Transformation xform, bool diagonal)
        {
            string ytx = null;
            double[] y;
            string title;
            bool reverse = false; bool use_ci = false;
            int plotMethod;
            get_ma_ordinate(Host, out y, yy, yw, cl, cu, ref cco, rows, out title, ref ytx, xtxt, out plotMethod, xform, ref reverse, ref use_ci);

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
            AxisScaler.Q_Axis(ref dataMinY, ref dataMaxY, out YDiv, ref amin, ref aint, out MinorTicsPerMajorTic, ScaleType.Linear);
            double ymn = amin;
            double ymx = amin + (aint * YDiv);
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

            StartMetafile(OutputStream, true);
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
                    DrawMarker(x1, y1, 6, _MarkerTypes[0]);
                }
            }

            double xnow; double ynow;
            Pen blackPen = new Pen(grBlack, 1);
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
            blackPen.Dispose();

            if (diagonal)
            {
                Pen tenPen = GetPen(_MarkerTypes[10], true);
                DrawLine(tenPen, XAxisCanvas, YAxisCanvas, XAxisCanvas + XExtCanvas, YAxisCanvas + YExtCanvas);
                tenPen.Dispose();
            }
            EndMetafile();
        }


        // TRANSMISSINGCOMMENT: Method Plot_Ties
        public void PlotTies(IChartHost Host, double[] x, double[] y, int nx, double lla, double ula, double GAMMA, string v0Title, string v1Title, double mean)
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
            int size2 = LabelFont.Height * 2;
            DrawStringLegend("mean difference ? " + Formatting.XRound(GAMMA * 100.0, 2) + "% limits of agreement", XAxisCanvas + XExtCanvas, YAxisCanvas + YExtCanvas + size2, StringAlignment.Far);

            // get the offsets for the Markers
            SetStandardScaling();

            // Draw the limits
            double x1 = XAxisCanvas + XExtCanvas;
            double y1 = ToCanvasY(ula);
            Pen redPen = new Pen(grRed, 1);
            Pen blackPen = new Pen(grBlack, 1);
            DrawLine(redPen, XAxisCanvas, y1, x1, y1);
            y1 = ToCanvasY(lla);
            DrawLine(redPen, XAxisCanvas, y1, x1, y1);
            y1 = ToCanvasY(mean);
            DrawLine(blackPen, XAxisCanvas, y1, x1, y1);
            // Work through the rows
            for (int r = 1; r <= nx; r++)
            {
                x1 = ToCanvasX(x[r]);
                y1 = ToCanvasY(y[r]);
                DrawMarker(x1, y1, 6, _MarkerTypes[0]);
            }
            redPen.Dispose();
            blackPen.Dispose();
        }


        // TRANSMISSINGCOMMENT: Method get_y1
        private double get_y1(double ynow, bool reverse)
        {
            return reverse ? YExtCanvas + YAxisCanvas + YAxisCanvas - ToCanvasY(ynow) : ToCanvasY(ynow);
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
            Pen p = GetPen(_MarkerTypes[0], true);
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
                PlotXY(aOptions.av, aOptions.mxd, xtxt, ytxt, "Agreement Plot (" + Formatting.XRound(100 * (1 - aOptions.P0), 2) + "% limits of agreement)", false, -1, _MarkerTypes[0].MarkerSize, _MarkerTypes[0].Shape, _MarkerTypes[0].IsFilled, p, false);
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
                PlotXY(aOptions.av, aOptions.mxd, xtxt, ytxt, "Agreement Plot", false, -1, _MarkerTypes[0].MarkerSize, _MarkerTypes[0].Shape, _MarkerTypes[0].IsFilled, p, false);
            }
            p.Dispose();
            // Get the offsets
            SetStandardScaling();

            // Plot mean
            Pen greenPen = new Pen(grGreen, 2);
            double y1 = ToCanvasY(aOptions.mean);
            DrawLine(greenPen, XAxisCanvas, y1, XAxisCanvas + XExtCanvas, y1);
            greenPen.Dispose();
            if (aOptions.HasLimits)
            {
                Pen blackPen = new Pen(grBlack, 1);
                // Plot upper limit
                y1 = ToCanvasY(aOptions.ula);
                DrawLine(greenPen, XAxisCanvas, y1, XAxisCanvas + XExtCanvas, y1);
                // Plot lower limit
                y1 = ToCanvasY(aOptions.lla);
                DrawLine(greenPen, XAxisCanvas, y1, XAxisCanvas + XExtCanvas, y1);
                blackPen.Dispose();
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
            return Canvas.MeasureString(s, AxisLabelFont).Width;
        }


        // TRANSMISSINGCOMMENT: Method AxisLabelHeight
        private float AxisLabelHeight(string s)
        {
            return Canvas.MeasureString(s, AxisLabelFont).Height;
        }


        // TRANSMISSINGCOMMENT: Method LabelHeight
        private float LabelHeight(string s)
        {
            return Canvas.MeasureString(s, LabelFont).Height;
        }


        // TRANSMISSINGCOMMENT: Method LegendWidth
        private float LegendWidth(string s)
        {
            return Canvas.MeasureString(s, LegendFont).Width;
        }


        // TRANSMISSINGCOMMENT: Method LegendHeight
        private float LegendHeight(string s)
        {
            return Canvas.MeasureString(s, LegendFont).Height;
        }


        // TRANSMISSINGCOMMENT: Method TitleWidth
        private float TitleWidth(string s)
        {
            return Canvas.MeasureString(s, TitleFont).Width;
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
            return offx + (transformedX / divx * XExtCanvas);
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
            return offy + (transformedY / divy * YExtCanvas);
        }


        // TRANSMISSINGCOMMENT: Method DrawLineInChartCoordinates
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


        // TRANSMISSINGCOMMENT: Method x_plgraph
        public IList<Image> x_plgraph(double[,] h, double[,] s, double[,] stime, int[,] dead, int groups, int[] cnx, string[] glab, bool tic, bool marker)
        {
            double x1 = 0; double y1 = 0;
            int j3;

            IList<Image> outputImages = new List<Image>();
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
                ChartRenderer ch = new ChartRenderer(ChartDefinition.Empty());
                Stream metaStream = new MemoryStream();
                ch.StartMetafile(metaStream, true);
                ch.DataMaxX = double.MinValue;
                ch.DataMaxY = double.MinValue;
                ch.DataMinX = double.MaxValue;
                ch.DataMinY = double.MaxValue;
                for (k = 1; k <= groups; k++)
                {
                    for (j = 1; j <= cnx[k]; j++)
                    {
                        if (x[j, k] > ch.DataMaxX)
                        {
                            ch.DataMaxX = x[j, k];
                        }
                        if (x[j, k] < ch.DataMinX)
                        {
                            ch.DataMinX = x[j, k];
                        }
                        if (y[j, k] > ch.DataMaxY)
                        {
                            ch.DataMaxY = y[j, k];
                        }
                        if (y[j, k] < ch.DataMinY)
                        {
                            ch.DataMinY = y[j, k];
                        }
                    }
                }
                if (j3 == 1)
                {
                    ch.DataMaxY = 1;
                    ch.DataMinY = 0;
                }
                // Draw the axes
                ch.DrawAxes(vt, new Axis(vx, AxisMode.Scale, 0, ScaleType.Linear), new Axis(vy, AxisMode.Scale, 0, ScaleType.Linear), BoxAxes, true, false);
                // get the offsets for the Graph
                ch.SetStandardScaling();

                // Plot the legends
                int size2 = ch.LabelFont.Height * 2;
                Pen p;
                if (groups > 1)
                {
                    for (k = 1; k <= groups; k++)
                    {
                        string vq = glab[k];
                        if (marker)
                        {
                            ch.DrawMarker(12, ch.YAxisCanvas + ch.YExtCanvas - 22 - (size2 * k), 6, _MarkerTypes[(k - 1) % 9]);
                        }
                        else
                        {
                            p = GetPen(_MarkerTypes[(k - 1) % 9], true);
                            ch.DrawLine(p, 10, ch.YAxisCanvas + ch.YExtCanvas - 18 - (size2 * k), 20, ch.YAxisCanvas + ch.YExtCanvas - 18 - (size2 * k));
                            ch.DrawLine(p, 20, ch.YAxisCanvas + ch.YExtCanvas - 18 - (size2 * k), 20, ch.YAxisCanvas + ch.YExtCanvas - 28 - (size2 * k));
                            p.Dispose();
                        }
                        ch.DrawStringLegendL(vq, 24, ch.YAxisCanvas + ch.YExtCanvas - 10 - (size2 * k));
                    }
                }
                for (k = 1; k <= groups; k++)
                {
                    p = GetPen(_MarkerTypes[(k - 1) % 9], true);
                    switch (j3)
                    {
                        case 1:
                            x1 = ch.ToCanvasX(ch.axisXMin);
                            y1 = ch.ToCanvasY(1.0);
                            break;
                        case 2:
                            x1 = ch.ToCanvasX(ch.axisXMin);
                            y1 = ch.ToCanvasY(0);
                            break;
                        case 3:
                            x1 = ch.ToCanvasX(x[1, k]);
                            y1 = ch.ToCanvasY(y[1, k]);
                            break;
                        case 4:
                            x1 = ch.ToCanvasX(x[1, k]);
                            y1 = ch.ToCanvasY(y[1, k]);
                            break;
                        case 5:
                            x1 = ch.ToCanvasX(x[1, k]);
                            y1 = ch.ToCanvasY(y[1, k]);
                            break;
                    }

                    for (j = 1; j <= cnx[k]; j++)
                    {
                        double x2 = ch.ToCanvasX(x[j, k]);
                        double Y2 = ch.ToCanvasY(y[j, k]);
                        // Draw the markers
                        // If Y2 <> Y1 Then Draw_Marker X2, Y2, 6, (k - 1) Mod 9
                        // changed to tic mark at censor points March 01
                        if (dead[j, k] == 0 && tic)
                        {
                            ch.DrawLine(p, x2, Y2, x2, Y2 + 7);
                        }
                        if (dead[j, k] != 0 && marker)
                        {
                            ch.DrawMarker(x2, Y2, 6, _MarkerTypes[(k - 1) % 9]);
                        }
                        // Then the lines
                        ch.DrawLine(p, x1, y1, x2, y1);
                        ch.DrawLine(p, x2, y1, x2, Y2);
                        x1 = x2;
                        y1 = Y2;
                    }
                    p.Dispose();
                }
                ch.EndMetafile();
                metaStream.Position = 0;
                outputImages.Add(Image.FromStream(metaStream));
            }
            return outputImages;
        }


        // TRANSMISSINGCOMMENT: Method Plot_MH
        public void Plot_MH(Stream OutputStream, int k, double[,] o, double[] odw, string[] title, double rmh, double ll, double ul, double cco, double[] odr, double[] odrl, double[] odru, bool[] lerr, bool[] uerr, string cap, int pbias, string qid, out bool ifault, object xlabel)
        {
            double w;
            double ytop; double xl; double xr; double Y2; double yc = 0; double yt = 0;
            int i; double XM; double yctr;

            if (k > 10)
            {
                ScaleYAxis = 1 + (k - 10) / 20;
                if (ScaleYAxis > 5)
                {
                    ScaleYAxis = 5;
                }
                MetaH = ScaleYAxis * 800;
            }
            else
            {
                ScaleYAxis = 1;
                MetaH = 800;
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

            StartMetafile(OutputStream, true);
            DefaultAxes();
            double rgap = 0;
            double xtra = 0;
            // allow room for right hand labels of effect and CI
            for (i = 1; i <= k; i++)
            {
                if (odr[i] != Constant.MISSING)
                {
                    w = Canvas.MeasureString(title[i], LegendFont).Width + 30;
                    if (w > xtra + XAxisCanvas)
                    {
                        xtra = w - XAxisCanvas - 5;
                    }
                    w = Canvas.MeasureString(Formatting.RoundMeta(odr[i], absmin) + " (" + Formatting.RoundMeta(odrl[i], absmin) + ", " + Formatting.RoundMeta(odru[i], absmin) + ")", LegendFont).Width;
                    if (w > rgap)
                    {
                        rgap = w;
                    }
                }
            }
            w = Canvas.MeasureString(combo_ti(cap), LegendFont).Width + 30;
            if (w > xtra + XAxisCanvas)
            {
                xtra = w - XAxisCanvas - 5;
            }
            XExtCanvas = 940 - rgap;
            DrawAxes(cap, new Axis(null, AxisMode.LineOnly, 0, ScaleType.Linear), new Axis(null, AxisMode.None, xtra, ScaleType.Linear), false, false, false);

            divx = DataMaxX - DataMinX;
            offx = -(DataMinX / divx * XExtCanvas) + XAxisCanvas;
            divy = k + pbias;
            offy = YAxisCanvas;

            Pen tenPenTrue = GetPen(_MarkerTypes[10], true);
            Pen tenPenFalse = GetPen(_MarkerTypes[10], false);
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
                    DrawStringLabel(Lab, XM, YAxisCanvas - 12, StringAlignment.Center);
                    DrawLine(tenPenTrue, XM, YAxisCanvas - 12, XM, YAxisCanvas);
                }
            }

            int r = 0;
            double txh = Canvas.MeasureString(title[1], LabelFont).Height;
            for (i = k; i >= 1; i--)
            {
                r = r + 1;
                yctr = (r + pbias - 0.5) / divy * YExtCanvas;
                ytop = (r + pbias) / divy * YExtCanvas;
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
                        XM = XAxisCanvas;
                    }
                    else
                    {
                        XM = ToCanvasX(Math.Log(odr[i]));
                    }
                    if (odrl[i] <= 0 | odrl[i] < realamin | odrl[i] == Constant.MISSING)
                    {
                        xl = XAxisCanvas;
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
                    DrawStringLabel(title[i], XAxisCanvas - 15, yc + txh / 2, StringAlignment.Far);
                    DrawStringLabel(Formatting.RoundMeta(odr[i], absmin) + " (" + Formatting.RoundMeta(odrl[i], absmin) + ", " + Formatting.RoundMeta(odru[i], absmin) + ")", XAxisCanvas + XExtCanvas + 10, yc + txh / 2, StringAlignment.Near);
                }
                else
                {
                    DrawStringLabel(title[i], XAxisCanvas - 15, yc + txh / 2, StringAlignment.Far);
                    DrawStringLabel("* (excluded)", XAxisCanvas + XExtCanvas + 10, yc + txh / 2, StringAlignment.Near);
                }
            }

            if (DataMinX <= 0)
            {
                // zero effect marker
                XM = ToCanvasX(Math.Log(1));
                DrawLine(tenPenTrue, XM, yt, XM, YAxisCanvas);
            }

            if (pbias == 1)
            {
                // pooled diamond
                double save_yc = yc;
                yctr = 0.5 / divy * YExtCanvas;
                ytop = 1 / divy * YExtCanvas;
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
                DrawStringLabel(combo_ti(cap), XAxisCanvas - 15, yc + txh / 2, StringAlignment.Far);
                DrawStringLabel(Formatting.RoundMeta(rmh, absmin) + " (" + Formatting.RoundMeta(ll, absmin) + ", " + Formatting.RoundMeta(ul, absmin) + ")", XAxisCanvas + XExtCanvas + 10, yc + txh / 2, StringAlignment.Near);
                // xaxis label
                DrawStringLabel(qid + " (" + Formatting.XRound(cco * 100, 1) + "% confidence interval" + ")", XAxisCanvas + XExtCanvas / 2, 50, StringAlignment.Center);
            }
            tenPenFalse.Dispose();
            tenPenTrue.Dispose();

            EndMetafile();

            ifault = false;
        }


        // TRANSMISSINGCOMMENT: Method Plot_MHRD
        public void Plot_MHRD(ITemplateHost host, int k, double[,] o, double[] odw, string[] title, double rmh, double ll, double ul, double cco, double[] odr, double[] odrl, double[] odru, bool[] lerr, bool[] uerr, string cap, int pbias, string qid, out bool ifault)
        {
            double aint = 0; double amin = 0;
            double w;
            double ytop; double XL; double XR; double Y2; double yc = 0; double yt = 0;
            int i; double XM; double yctr;

            if (k > 10)
            {
                ScaleYAxis = 1 + (k - 10) / 20;
                if (ScaleYAxis > 5)
                {
                    ScaleYAxis = 5;
                }
                MetaH = ScaleYAxis * 800;
            }
            else
            {
                ScaleYAxis = 1;
                MetaH = 800;
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

            AxisScaler.Q_Axis(ref dataMinX, ref dataMaxX, out XDiv, ref amin, ref aint, out MinorTicsPerMajorTic, ScaleType.Linear);
            DataMinX = amin;
            DataMaxX = amin + XDiv * aint;

            DefaultAxes();
            double rgap = 0;
            double xtra = 0;
            // allow room for right hand labels of effect and CI
            for (i = 1; i <= k; i++)
            {
                if (odr[i] != Constant.MISSING)
                {
                    w = Canvas.MeasureString(title[i], LabelFont).Width + 30;
                    if (w > xtra + XAxisCanvas)
                    {
                        xtra = w - XAxisCanvas - 5;
                    }
                    w = Canvas.MeasureString(Formatting.RoundMeta(odr[i], absmin) + " (" + Formatting.RoundMeta(odrl[i], absmin) + ", " + Formatting.RoundMeta(odru[i], absmin) + ")", LabelFont).Width;
                    if (w > rgap)
                    {
                        rgap = w;
                    }
                }
            }
            w = Canvas.MeasureString(combo_ti(cap), LabelFont).Width + 30;
            if (w > xtra + XAxisCanvas)
            {
                xtra = w - XAxisCanvas - 5;
            }
            XExtCanvas = 940 - rgap;
            DrawAxes(cap, new Axis(null, AxisMode.LineOnly, 0, ScaleType.Linear), new Axis(null, AxisMode.None, xtra, ScaleType.Linear), false, false, false);

            divx = DataMaxX - DataMinX;
            offx = -(DataMinX / divx * XExtCanvas) + XAxisCanvas;
            divy = k + pbias;
            offy = YAxisCanvas;

            Pen tenPenTrue = GetPen(_MarkerTypes[10], true);
            Pen tenPenFalse = GetPen(_MarkerTypes[10], false);
            string msk = GetAxisMask(aint, amin, XDiv, MinorTicsPerMajorTic);
            for (i = 0; i <= XDiv; i++)
            {
                XM = ToCanvasX(amin + aint * i);
                if ((i % MinorTicsPerMajorTic) != 0)
                {
                    DrawLine(tenPenTrue, XM, YAxisCanvas - 7, XM, YAxisCanvas);
                }
                else
                {
                    string Lab = (amin + i * aint).ToString(msk);
                    if (double.Parse(Lab) != 0)
                    {
                        DrawStringLabel(Lab, XM, YAxisCanvas - 12, StringAlignment.Center);
                        DrawLine(tenPenTrue, XM, YAxisCanvas - 12, XM, YAxisCanvas);
                    }
                }
            }

            int r = 0;
            double txh = Canvas.MeasureString(title[1], LabelFont).Height;
            for (i = k; i >= 1; i--)
            {
                r = r + 1;
                yctr = (r + pbias - 0.5) / divy * YExtCanvas;
                ytop = (r + pbias) / divy * YExtCanvas;
                Y2 = (ytop - yctr) / 1.5;
                yc = offy + yctr;
                yt = offy + yctr + Y2;
                double yb = offy + yctr - Y2;
                // ytop = yctr + ( ytop - yctr ) * 0.1 + ( ytop - yctr ) * 0.9 * ( gw[ i ] / max_gw ); 
                // ytop = yctr + ( ytop - yctr ) * 0.8; 
                if (odr[i] != Constant.MISSING)
                {
                    XM = ToCanvasX(odr[i]);
                    XL = odrl[i] == Constant.MISSING ? XAxisCanvas : ToCanvasX(odrl[i]);
                    if (odru[i] == double.PositiveInfinity | odru[i] == Constant.MISSING)
                    {
                        XR = XAxisCanvas + XExtCanvas;
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
                    DrawStringLabel(title[i], XAxisCanvas - 15, yc + txh / 2, StringAlignment.Far);
                    DrawStringLabel(Formatting.RoundMeta(odr[i], absmin) + " (" + Formatting.RoundMeta(odrl[i], absmin) + ", " + Formatting.RoundMeta(odru[i], absmin) + ")", XAxisCanvas + XExtCanvas + 10, yc + txh / 2, StringAlignment.Near);
                }
                else
                {
                    DrawStringLabel(title[i], XAxisCanvas - 15, yc + txh / 2, StringAlignment.Far);
                    DrawStringLabel("* (excluded)", XAxisCanvas + XExtCanvas + 10, yc + txh / 2, StringAlignment.Near);
                }
            }

            if (DataMinX <= 0)
            {
                //  no effect marker
                XM = offx;
                DrawLine(tenPenTrue, XM, yt, XM, YAxisCanvas - 12);
                DrawStringLabel("  0  ", XM, YAxisCanvas - 12, StringAlignment.Center);
            }

            if (pbias == 1)
            {
                double save_yc = yc;
                yctr = 0.5 / divy * YExtCanvas;
                ytop = 1 / divy * YExtCanvas;
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
                DrawStringLabel(combo_ti(cap), XAxisCanvas - 15, yc + txh / 2, StringAlignment.Far);
                DrawStringLabel(Formatting.RoundMeta(rmh, absmin) + " (" + Formatting.RoundMeta(ll, absmin) + ", " + Formatting.RoundMeta(ul, absmin) + ")", XAxisCanvas + XExtCanvas + 10, yc + txh / 2, StringAlignment.Near);
                // x axis text
                DrawStringLabel(qid + " (" + Formatting.XRound(cco * 100, 1) + "% confidence interval" + ")", XAxisCanvas + XExtCanvas / 2, 50, StringAlignment.Center);
            }
            tenPenTrue.Dispose();
            tenPenFalse.Dispose();

            ifault = false;
        }


        // TRANSMISSINGCOMMENT: Method Plot_Effect
        public void PlotEffect(ITemplateHost host, int k, double[] cn, double[] En, string[] title, double rmh, double ll, double ul, double cco, double[] odr, double[] odrl, double[] odru, string cap, int pbias, string qid)
        {
            double aint = 0; double amin = 0;
            double xtra = 0;
            int i; double xm; double yctr;
            double ytop; double xl; double xr; double y2; double yc = 0; double yt = 0;
            string lab;

            if (k > 10)
            {
                ScaleYAxis = 1 + (k - 10) / 20;
                if (ScaleYAxis > 5)
                {
                    ScaleYAxis = 5;
                }
                MetaH = ScaleYAxis * 800;
            }
            else
            {
                ScaleYAxis = 1;
                MetaH = 800;
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

            AxisScaler.Q_Axis(ref dataMinX, ref dataMaxX, out XDiv, ref amin, ref aint, out MinorTicsPerMajorTic, ScaleType.Linear);
            DataMinX = amin;
            DataMaxX = amin + XDiv * aint;

            DefaultAxes();
            for (i = 1; i <= k; i++)
            {
                if (odr[i] != Constant.MISSING)
                {
                    double w = Canvas.MeasureString(title[i], TitleFont).Width + 30;
                    if (w > xtra + XAxisCanvas)
                    {
                        xtra = w - XAxisCanvas - 5;
                    }
                }
            }
            DrawAxes(cap, new Axis(null, AxisMode.LineOnly, 0, ScaleType.NotSet), new Axis(null, AxisMode.None, xtra, ScaleType.NotSet), false, true, false);

            divx = DataMaxX - DataMinX;
            offx = -(DataMinX / divx * XExtCanvas) + XAxisCanvas;
            divy = kok + pbias;
            offy = YAxisCanvas;

            Pen tenPenTrue = GetPen(_MarkerTypes[10], true);
            string msk = GetAxisMask(aint, amin, XDiv, MinorTicsPerMajorTic);
            for (i = 0; i <= XDiv; i++)
            {
                xm = ToCanvasX(amin + aint * i);
                if ((i % MinorTicsPerMajorTic) != 0)
                {
                    DrawLine(tenPenTrue, xm, YAxisCanvas - 7, xm, YAxisCanvas);
                }
                else
                {
                    lab = (amin + i * aint).ToString(msk);
                    if (double.Parse(lab) != 0)
                    {
                        DrawStringLabel(lab, xm, YAxisCanvas - 12, StringAlignment.Center);
                        DrawLine(tenPenTrue, xm, YAxisCanvas - 12, xm, YAxisCanvas);
                    }
                }
            }

            int r = 0;
            double txh = Canvas.MeasureString(title[1], LabelFont).Height;
            for (i = k; i >= 1; i--)
            {
                if (odr[i] != Constant.MISSING)
                {
                    r = r + 1;
                    yctr = (r + pbias - 0.5) / divy * YExtCanvas;
                    ytop = (r + pbias) / divy * YExtCanvas;
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
                    DrawStringLabel(title[i], XAxisCanvas - 15, yc + txh / 2, StringAlignment.Far);
                }
            }

            if (DataMinX <= 0)
            {
                xm = offx;
                DrawLine(tenPenTrue, xm, yt, xm, YAxisCanvas - 12);
                DrawStringLabel("  0  ", xm, YAxisCanvas - 12, StringAlignment.Center);
            }

            if (pbias == 1)
            {
                double save_yc = yc;
                yctr = 0.5 / divy * YExtCanvas;
                ytop = 1 / divy * YExtCanvas;
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
                Pen tenPenFalse = GetPen(_MarkerTypes[10], false);
                DrawLine(tenPenFalse, xm, save_yc, xm, yt);
                tenPenFalse.Dispose();
                lab = "pooled " + qid + " = " + host.RoundU(rmh) + "  (" + Formatting.XRound(cco * 100, 1) + "% CI = " + host.RoundU(ll) + " to " + host.RoundU(ul) + ")";
                string xlab = cap.IndexOf("fixed", StringComparison.Ordinal) + 1 != 0 ? "" : "DL ";
                //  If hSS <> -99 Then Lab = xlab & Lab
                lab = xlab + lab;
                DrawStringLabel(lab, XAxisCanvas + XExtCanvas / 2, 50, StringAlignment.Center);
            }
            tenPenTrue.Dispose();
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
        public void Plot_CP(ITemplateHost host, int k, string[] title, double[] odr, double[] odrl, double[] odru, double[] gn, int[] pg, string cap, string qid, Transformation xform)
        {
            if (k > 10)
            {
                ScaleYAxis = 1 + (k - 10) / 20;
                if (ScaleYAxis > 5)
                {
                    ScaleYAxis = 5;
                }
                MetaH = ScaleYAxis * 800;
            }
            else
            {
                ScaleYAxis = 1;
                MetaH = 800;
            }
            SetupGraphics();

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
                    w = Canvas.MeasureString(title[i], LabelFont).Width + 30;
                    if (w > xtra + XAxisCanvas)
                    {
                        xtra = w - XAxisCanvas - 5;
                    }
                    w = Canvas.MeasureString(Formatting.RoundMeta(odr[i], absmin) + " (" + Formatting.RoundMeta(odrl[i], absmin) + ", " + Formatting.RoundMeta(odru[i], absmin) + ")", LabelFont).Width;
                    if (w > rgap)
                    {
                        rgap = w;
                    }
                }
            }
            w = Canvas.MeasureString(combo_ti(cap), LabelFont).Width + 30;
            if (w > xtra + XAxisCanvas)
            {
                xtra = w - XAxisCanvas - 5;
            }
            XExtCanvas = 940 - rgap;

            double realamin = 0;
            double realamax = 0;

            int tics = 0; double[] tic = null;
            switch (xform)
            {
                case Transformation.Log:
                    //  TODO: Use a proper log scale
                    double amin = 0;
                    double aint = 0;
                    AxisScaler.Q_Axis(ref dataMinX, ref dataMaxX, out XDiv, ref amin, ref aint, out MinorTicsPerMajorTic, ScaleType.Linear);
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
                        DataMinX = 0.0;
                        DataMaxX = 1.0;
                    }
                    DrawAxes(cap, new Axis(null, AxisMode.Scale, 0, ScaleType.Linear), new Axis(null, AxisMode.None, xtra, ScaleType.NotSet), false, false, false);
                    DataMinX = axisXMin;
                    DataMaxX = axisXMax;

                    break;
            }


            divx = DataMaxX - DataMinX;
            offx = -(DataMinX / divx * XExtCanvas) + XAxisCanvas;
            divy = kok;
            offy = YAxisCanvas;

            Pen tenPenTrue = GetPen(_MarkerTypes[10], true);
            Pen tenPenFalse = GetPen(_MarkerTypes[10], false);
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
                        DrawStringLabel(lab, XM, YAxisCanvas - 12, StringAlignment.Center);
                        DrawLine(tenPenTrue, XM, YAxisCanvas - 12, XM, YAxisCanvas);
                    }
                }
            }

            double rmh = -99;
            int r = 0;
            double txh = Canvas.MeasureString(title[1], LabelFont).Height;
            double botlim = xform == Transformation.Log ? realamin : double.NegativeInfinity;
            double yt = 0;
            for (int i = k; i >= 1; i--)
            {
                if (odr[i] != Constant.MISSING)
                {
                    r = r + 1;
                    double yctr = (r - 0.5) / divy * YExtCanvas;
                    double ytop = r / divy * YExtCanvas;
                    double XM = 0;
                    if (odr[i] < botlim)
                    {
                        XM = XAxisCanvas;
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
                        XL = XAxisCanvas;
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
                                XL = ToCanvasX(odrl[i]);
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
                            XR = ToCanvasX(odru[i]);
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
                    DrawStringLabel(title[i], XAxisCanvas - 15, yc + txh / 2, StringAlignment.Far);
                    DrawStringLabel(Formatting.RoundMeta(odr[i], absmin) + " (" + Formatting.RoundMeta(odrl[i], absmin) + ", " + Formatting.RoundMeta(odru[i], absmin) + ")", XAxisCanvas + XExtCanvas + 10, yc + txh / 2, StringAlignment.Near);
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

                DrawLine(tenPenTrue, XM, yt, XM, YAxisCanvas);
            }

            if (rmh != -99)
            {
                string buf = qid;
                DrawStringLabel(buf, XAxisCanvas + XExtCanvas / 2, 50, StringAlignment.Center);
            }
            tenPenTrue.Dispose();
            tenPenFalse.Dispose();
        }


        // TRANSMISSINGCOMMENT: Method plot_pert
        public void plot_pert(double PERT, double Slope, double YIntercept, int nx, double MS, double SUMX, double SSX, bool plotBothLines)
        {
            double xstep = (axisXMax - axisXMin) / 20.0;

            if (PERT != 0)
            {
                double lastx1 = 0;
                double lasty1 = 0;

                bool first = true;
                for (double calcx = axisXMin; calcx <= axisXMax; calcx += xstep)
                {
                    double calcy = Slope * calcx + YIntercept;
                    double sey = Math.Sqrt(MS * (1.0 + (1.0 / Convert.ToDouble(nx) + Math.Pow((calcx - (SUMX / Convert.ToDouble(nx))), 2.0) / SSX)));
                    double pcon = calcy + (sey * PERT);
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
                    double calcy = Slope * calcx + YIntercept;
                    double sey = Math.Sqrt(MS * (1.0 + (1.0 / Convert.ToDouble(nx) + Math.Pow((calcx - (SUMX / Convert.ToDouble(nx))), 2.0) / SSX)));
                    double ncon = calcy - (sey * PERT);
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
                    double calcy = Slope * calcx + YIntercept;
                    double sey = Math.Sqrt(MS * (1.0 + (1.0 / Convert.ToDouble(nx) + Math.Pow((calcx - (SUMX / Convert.ToDouble(nx))), 2.0) / SSX)));
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
                    double calcy = Slope * calcx + YIntercept;
                    double sey = Math.Sqrt(MS * (1.0 + (1.0 / Convert.ToDouble(nx) + Math.Pow((calcx - (SUMX / Convert.ToDouble(nx))), 2.0) / SSX)));
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


        // TRANSMISSINGCOMMENT: Property AsAsciiRTF
        public string AsAsciiRTF
        {
            get
            {
                if (!(IsAscii))
                {
                    throw new Exception("Trying to get ASCII string for a non-ASCII chart");
                }
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                for (int i = SH_TX.GetUpperBound(0); i >= SH_TX.GetLowerBound(0); i--)
                {
                    sb.Append(SH_TX[i]);
                    sb.Append(Formatting.RTFCRLF);
                }
                return sb.ToString();
            }
        }

        private void WriteAsciiYX(int y, int x, string text)
        {
            SH_TX[y] = ReplaceAt(SH_TX[y], x, text);
        }

        private void WriteAsciiYX(int y, int x, char c)
        {
            SH_TX[y] = ReplaceAt(SH_TX[y], x, c);
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
            BoxAxes = true;
        }


        // TRANSMISSINGCOMMENT: Method SetFontsAndThicknessesFromOptions
        private void SetFontsAndThicknessesFromOptions(GenericOptions o)
        {
            if (o.UsesAxisLabelFontDescriptor && !(string.IsNullOrEmpty(o.AxisLabelFontDescriptor)))
            {
                AxisLabelFont = FontFromSaveString(o.AxisLabelFontDescriptor);
            }
            if (o.UsesAxisTitleFontDescriptor && !(string.IsNullOrEmpty(o.AxisTitleFontDescriptor)))
            {
                AxisTitleFont = FontFromSaveString(o.AxisTitleFontDescriptor);
            }
            if (o.UsesLegendFontDescriptor && !(string.IsNullOrEmpty(o.LegendFontDescriptor)))
            {
                LegendFont = FontFromSaveString(o.LegendFontDescriptor);
            }
            if (o.UsesTitleFontDescriptor && !(string.IsNullOrEmpty(o.TitleFontDescriptor)))
            {
                TitleFont = FontFromSaveString(o.TitleFontDescriptor);
            }

            if (o.UsesAxisLineThickness)
            {
                AxisLineThickness = o.AxisLineThickness;
                Color c = Color.Black;
                if (AxisPen != null)
                {
                    c = AxisPen.Color;
                    AxisPen.Dispose();
                }
                AxisPen = new Pen(c, AxisLineThickness);
            }
        }


        private Stack<System.Drawing.Drawing2D.GraphicsContainer> ContainerStack;

        // TRANSMISSINGCOMMENT: Method PushContainer
        private void PushContainer()
        {
            if (ContainerStack == null)
            {
                ContainerStack = new Stack<System.Drawing.Drawing2D.GraphicsContainer>();
            }
            ContainerStack.Push(Canvas.BeginContainer());
        }


        // TRANSMISSINGCOMMENT: Method PopContainer
        private void PopContainer()
        {
            if (ContainerStack != null)
            {
                Canvas.EndContainer(ContainerStack.Pop());
            }
        }


        // TRANSMISSINGCOMMENT: Method GetPen
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
    }
}
