using StatsDirect.Numerics;
using StatsDirect.Templates;
using StatsDirect.Utilities;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;

namespace StatsDirect.Charting.Renderer
{
    public abstract class AbstractChartRenderer : IDisposable
    {
        protected readonly double LOG2 = Math.Log(2.0);
        protected const int LEGEND_TOP_GAP = 70;
        protected const int LEGEND_MARKER_SIZE = 6;
        protected const int MINIMUM_LEGEND_GAP = 6;
        protected const int LOWEST_ALLOWED_LEGEND = 30;
        protected const int MAX_LABEL_LENGTH = 50;
        protected const int MINIMUM_X_WHITESPACE = 70;

        protected double DataMinX { get; set; }
        protected double DataMinGreaterThanZeroX { get; set; }
        protected double DataMaxX { get; set; }
        protected double DataMinY { get; set; }
        protected double DataMinGreaterThanZeroY { get; set; }
        protected double DataMaxY { get; set; }

        protected ChartDefinition definition { get; set; }

        private IStatsDirectCanvas statsDirectCanvas;

        protected Font axisLabelFont { get; private set; }
        protected Font axisTitleFont { get; private set; }
        protected float axisLineThickness { get; private set; }
        protected Pen axisPen { get; private set; }
        private Brush axisBrush;
        private const double AXIS_LITTLE_TICK = 4;
        protected const double AXIS_BIG_TICK = 7;
        protected Font titleFont { get; private set; }
        protected Font legendFont { get; private set; }
        protected bool boxAxes { get; private set; } = ChartPreferences.DefaultBoxAxes;

        protected Font labelFont { get; private set; }

        private bool isXAxisReversed;
        private bool isYAxisReversed;
        /// <summary>
        /// The X-position in canvas co-ordinates of the left-hand end of the chart's X-axis
        /// </summary>
        protected double xAxisCanvas { get; set; }
        /// <summary>
        /// The length in canvas co-ordinates of the chart's X-axis
        /// </summary>
        protected double xExtCanvas { get; set; }
        /// <summary>
        /// The Y-position in canvas co-ordinates of the bottom of the chart's Y-axis
        /// </summary>
        protected double yAxisCanvas { get; set; }
        protected double yExtCanvas { get; set; }

        protected double divx { get; set; }
        protected double offx { get; set; }
        protected double divy { get; set; }
        protected double offy { get; set; }

        protected const int DEFAULT_METAFILE_HEIGHT = 800;
        protected const int DEFAULT_METAFILE_WIDTH = 1132;
        protected const double DEFAULT_X_GAP = 80;
        protected const double DEFAULT_Y_GAP = 80;
        protected int imageHeight = DEFAULT_METAFILE_HEIGHT;
        protected int imageWidth = DEFAULT_METAFILE_WIDTH;
        protected const int LABEL_TO_AXIS_LABEL_GAP = 15;

        protected string[] shTx { get; set; }

        protected const int ASCII_Ytxt = 3;
        protected const int ASCII_XTxt = 15;

        ///  <summary>
        ///  Several methods take a colour, not a pen.  This caches the most recent pen used by those methods, so that it can be re-used rather than regenerated each time.
        ///  </summary>
        private Pen mostRecentPen;

        private readonly ICanvasFactory canvasFactory;

        protected AbstractChartRenderer(ChartDefinition definition, ICanvasFactory canvasFactory)
        {
            DataMinX = double.MaxValue;
            DataMaxX = -double.MaxValue;
            DataMinY = double.MaxValue;
            DataMaxY = -double.MaxValue;

            this.definition = definition;
            this.canvasFactory = canvasFactory;
            if (definition == null)
                return;
            DataMinX = definition.DataMinX;
            DataMinGreaterThanZeroX = definition.DataMinGreaterThanZeroX;
            DataMaxX = definition.DataMaxX;
            DataMinY = definition.DataMinY;
            DataMinGreaterThanZeroY = definition.DataMinGreaterThanZeroY;
            DataMaxY = definition.DataMaxY;
        }

        ///  <summary>
        ///  Sort the data for each series into ascending order.
        ///  Note and return the global minimum and maximum values.
        ///  </summary>
        /// <param name="seriesToUse"></param>
        protected static Layout.Range GetMinMaxSort(List<Series> seriesToUse)
        {
            double min = double.MaxValue;
            double max = double.MinValue;
            foreach (DoubleSeries s in seriesToUse)
            {
                Array.Sort(s.Data);
                if (s.Data[0] < min)
                    min = s.Data[0];
                if (s.Data[s.Data.Length - 1] > max)
                    max = s.Data[s.Data.Length - 1];
            }
            return new Layout.Range(min, max);
        }

        ///  <summary>
        ///  Prepare to plot a vector chart to the specified stream.
        ///  </summary>
        ///  <remarks></remarks>
        protected void StartVectorPlot(bool shouldDefaultAxes = true)
        {
            // Initialise scaling and resources
            if (shouldDefaultAxes)
                DefaultAxes();
            if (!ChartPreferences.AreSharedValuesInitialised)
                ChartPreferences.InitSharedValues();

            //  Drawing objects
            if (!ReconstituteFonts())
            {
                ChartPreferences.InitFirstFonts();
                if (!ReconstituteFonts())
                    throw new Exception("Cannot find the fonts that StatsDirect uses for charting. If Calibri is not installed on your system, you can download it from https://www.microsoft.com/typography/fonts/font.aspx?FMID=1710");
            }

            axisPen = new Pen(grAxis, 1);
            axisBrush = new SolidBrush(Color.Black);

            statsDirectCanvas = CreateCanvas(imageWidth, imageHeight);
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
            axisLabelFont = ChartPreferences.FontFromSaveString(ChartPreferences.DefaultAxisLabelFont);
            axisTitleFont = ChartPreferences.FontFromSaveString(ChartPreferences.DefaultAxisTitleFont);
            labelFont = ChartPreferences.FontFromSaveString(ChartPreferences.DefaultLabelFont);
            legendFont = ChartPreferences.FontFromSaveString(ChartPreferences.DefaultLegendFont);
            titleFont = ChartPreferences.FontFromSaveString(ChartPreferences.DefaultTitleFont);
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
            double topOfXAxisTitle = yAxisCanvas - gapForAxisLabels - LABEL_TO_AXIS_LABEL_GAP;
            double bottomOfXAxisTitle = topOfXAxisTitle - (titleHasText ? axisTitleFont.Height / 2 : 0);
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
            // TODO: Fix this so that the user can spec their own scales again!
            useCalculatedScalesEvenWithDefinition = true;

            DefaultAxes(0, 0, x.ExtraSpaceAfterAxisEnds);
            AxisScalesAndExtraSize ases = DrawAxesOrFail(title, x, y, shouldBoxAxes, useCalculatedScalesEvenWithDefinition);
            if (ases.ExtraSize.Width > 0 || ases.ExtraSize.Height > 0)
            {
                imageWidth += ases.ExtraSize.Width;
                imageHeight += ases.ExtraSize.Height;
                statsDirectCanvas.Dispose();
                statsDirectCanvas = CreateCanvas(imageWidth, imageHeight);
                DefaultAxes(ases.ExtraSize.Height, ases.ExtraSize.Width, x.ExtraSpaceAfterAxisEnds);
                ases = DrawAxesOrFail(title, x, y, shouldBoxAxes, useCalculatedScalesEvenWithDefinition);
                if (ases.ExtraSize.Width > 0 || ases.ExtraSize.Height > 0)
                    throw new Exception("Even after trying to enlarge the canvas, I don't have enough space for the chart.");
            }
            return ases.AxisScales;
        }

        private IStatsDirectCanvas CreateCanvas(int width, int height)
        {
            return canvasFactory.Create(width, height);
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
            isXAxisReversed = x.Reverse;
            isYAxisReversed = y.Reverse;
            switch (x.Mode)
            {
                case AxisMode.LineOnly:
                case AxisMode.None:
                    //  Do nothing
                    xAss = new AxisScaleAndSize();
                    divx = 1;
                    offx = xAxisCanvas;
                    break;
                case AxisMode.ReverseScale:
                case AxisMode.Scale:
                case AxisMode.ScaleWithoutLabels:
                    xAss = DrawXScale((x.Mode & AxisMode.Labels) == AxisMode.Labels, x.ScaleType, useCalculatedScalesEvenWithDefinition);
                    // DrawXScale sets divx and offx
                    break;
                case AxisMode.Series:
                    if (null != x.Series)
                    {
                        xAss = DrawXSeries(x.Series.Select(s => s.Title).ToList());
                        divx = x.Series.Count;
                        offx = xAxisCanvas;
                    }
                    else if (null != x.Labels)
                    {
                        xAss = DrawXSeries(x.Labels);
                        divx = x.Labels.Count;
                        offx = xAxisCanvas;
                    }
                    else
                        xAss = new AxisScaleAndSize();
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
                    divy = 1;
                    offy = yAxisCanvas;
                    break;
                case AxisMode.ReverseScale:
                case AxisMode.Scale:
                case AxisMode.ScaleWithoutLabels:
                    yAss = DrawYScale((x.Mode & AxisMode.Labels) == AxisMode.Labels, y.ScaleType, useCalculatedScalesEvenWithDefinition);
                    break;
                case AxisMode.Series:
                    if (null != y.Series)
                    {
                        yAss = DrawYSeries(y.Series.Select(s => s.Title).ToList());
                        divy = y.Series.Count;
                        offy = yAxisCanvas;
                    }
                    else if (null != y.Labels)
                    {
                        yAss = DrawYSeries(y.Labels);
                        divy = y.Labels.Count;
                        offy = yAxisCanvas;
                    }
                    else
                        yAss = new AxisScaleAndSize();
                    break;
                default:
                    throw new NotImplementedException("Unknown X axis scale mode");
            }

            AxisScales axisScales = new AxisScales { X = xAss.AxisScale, Y = yAss.AxisScale };

            if (IsAscii)
                SetStandardAsciiScaling(yAss.AxisScale.Tics().Count, axisScales);
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
            return new AxisScalesAndExtraSize { AxisScales = axisScales, ExtraSize = extraSizeRequired };
        }

        private AxisScaleAndSize DrawXScale(bool drawLabels, ScaleType scaleType, bool useCalculatedScalesEvenWithDefinition)
        {
            IAxisScale xAxisScale;
            if (IsAscii || drawLabels)
                xAxisScale = Q_AxisOrFromDefinition(DataMinX, DataMinGreaterThanZeroX, DataMaxX, false, scaleType, useCalculatedScalesEvenWithDefinition);
            else
            {
                //  Not ASCII, not drawing our own labels, so just set up 20 divisions
                xAxisScale = new LinearAxisScale(DataMinX, DataMaxX, DataMinX, DataMaxX, 20, 5);
            }

            DataMinX = xAxisScale.MinimumDataValue;
            DataMaxX = xAxisScale.MaximumDataValue;
            divx = Transform(xAxisScale.MaximumScaleValue, scaleType) - Transform(xAxisScale.MinimumScaleValue, scaleType);
            offx = -(Transform(xAxisScale.MinimumScaleValue, scaleType) / divx * xExtCanvas) + xAxisCanvas;
            // set a string mask that will fit OK
            string msk = AxisMaskOrFromDefinition(xAxisScale, false, useCalculatedScalesEvenWithDefinition);

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
                    foreach (Tic tic in xAxisScale.Tics())
                    {
                        double x1 = ToCanvasX(tic.Value);
                        switch (tic.TicType)
                        {
                            case TicType.Minor:
                                AxisDrawline(x1, yAxisCanvas - AXIS_LITTLE_TICK, x1, yAxisCanvas);
                                if (hasGridLines && !drawLabels)
                                    statsDirectCanvas.DrawLine(gridLinePen, x1, yAxisCanvas, x1, yAxisCanvas + yExtCanvas);
                                break;
                            case TicType.Major:
                                //  Major tic - may or may not be labelled
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
                                break;
                        }
                    }
                }
            }
            else
            {
                //  ASCII - always linear for now.  TODO: Log
                ILinearAxisScale linearAxisScale = (ILinearAxisScale)xAxisScale;
                shTx[ASCII_Ytxt - 1] = string.Empty.PadLeft(13) + "/" + new string('-', 61);
                int intervals = xAxisScale.Tics().Count - 1;
                for (int x = 0; x <= intervals; x++)
                {
                    if ((x - linearAxisScale.Phase) % linearAxisScale.IntervalsPerMajorTic == 0)
                    {
                        string lab = (xAxisScale.MinimumScaleValue + (x * linearAxisScale.Interval)).ToString(msk);
                        int l = lab.Length;
                        labelHeight = Math.Max(Convert.ToInt32(labelHeight), l);
                        int s = Convert.ToInt32((x * (60 / intervals)) + 15);
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
            return new AxisScaleAndSize { AxisScale = xAxisScale, Size = labelHeight + AXIS_BIG_TICK };
        }

        private IAxisScale Q_AxisOrFromDefinition(double qmin, double qMinGreaterThanZero, double qmax, bool isY, ScaleType scaleType, bool useCalculatedScalesEvenWithDefinition)
        {
            if ((definition != null) && definition.HasScaleParameters && !useCalculatedScalesEvenWithDefinition)
            {
                //  Use the values in our scale parameters
                AxisScaleParameters asp = isY ? definition.ScaleParameters.Y : definition.ScaleParameters.X;
                if ((null != asp) && null != asp.AxisScale)
                    return asp.AxisScale;
            }
            //  If we get here, there was no prior definition - calculate it ourselves.
            return AxisScalerFactory.AxisScalerFor(scaleType).Q_Axis(qmin, qMinGreaterThanZero, qmax, isY);
        }

        private string AxisMaskOrFromDefinition(IAxisScale axisScale, /* double stepp, double znmin, int nstep, int sp, */ bool isY, /* ScaleType scaleType, */ bool UseCalculatedScalesEvenWithDefinition)
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
        private AxisScaleAndSize DrawYScale(bool drawLabels, ScaleType scaleType, bool useCalculatedScalesEvenWithDefinition)
        {
            const double AXIS_LABEL_OFFSET_FROM_BIG_TICK = 8;

            // find a neat axis division
            IAxisScale yAxisScale = Q_AxisOrFromDefinition(DataMinY, DataMinGreaterThanZeroY, DataMaxY, true, scaleType, useCalculatedScalesEvenWithDefinition);
            DataMinY = yAxisScale.MinimumDataValue;
            DataMaxY = yAxisScale.MaximumDataValue;
            divy = Transform(yAxisScale.MaximumScaleValue, scaleType) - Transform(yAxisScale.MinimumScaleValue, scaleType);
            offy = -(Transform(yAxisScale.MinimumScaleValue, scaleType) / divy * yExtCanvas) + yAxisCanvas;

            // set a string mask that will fit OK
            string msk = AxisMaskOrFromDefinition(yAxisScale, true, useCalculatedScalesEvenWithDefinition);
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
                    foreach (Tic tic in yAxisScale.Tics())
                    {
                        double y1 = ToCanvasY(tic.Value);
                        switch (tic.TicType)
                        {
                            case TicType.Minor:
                                AxisDrawline(xAxisCanvas - AXIS_LITTLE_TICK, y1, xAxisCanvas, y1);
                                if (hasGridLines && !drawLabels)
                                    statsDirectCanvas.DrawLine(gridLinePen, xAxisCanvas, y1, xAxisCanvas + xExtCanvas, y1);
                                break;
                            case TicType.Major:
                                //  Major tic - may or may not be labelled
                                if (drawLabels)
                                {
                                    string lab = tic.Value.ToString(msk);
                                    maxLabelWidth = Math.Max(Convert.ToSingle(maxLabelWidth), AxisDrawStringAtAngleRM(lab, xAxisCanvas - (AXIS_BIG_TICK + AXIS_LABEL_OFFSET_FROM_BIG_TICK), y1, direction).Width);
                                    AxisDrawline(xAxisCanvas - AXIS_BIG_TICK, y1, xAxisCanvas, y1);
                                }
                                else
                                {
                                    AxisDrawline(xAxisCanvas - AXIS_LITTLE_TICK, y1, xAxisCanvas, y1);
                                }
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
                ILinearAxisScale linearAxisScale = (ILinearAxisScale)yAxisScale;
                int intervals = yAxisScale.Tics().Count - 1;
                for (int y = 0; y <= intervals; y++)
                {
                    if ((y - linearAxisScale.Phase) % linearAxisScale.IntervalsPerMajorTic == 0)
                    {
                        string lab = (yAxisScale.MinimumScaleValue + (y * linearAxisScale.Interval)).ToString(msk);
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
            return new AxisScaleAndSize { AxisScale = yAxisScale, Size = maxLabelWidth + AXIS_BIG_TICK + AXIS_LABEL_OFFSET_FROM_BIG_TICK };
        }

        ///  <summary>
        ///  Draw the Y axis as a series
        ///  </summary>
        /// <returns>The width of the axis, ticks, gap to labels, and labels</returns>
        ///  <remarks>Labels are drawn centred between tics</remarks>
        private AxisScaleAndSize DrawYSeries(IList<string> labels)
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

            return new AxisScaleAndSize { AxisScale = new CategoryAxisScale(labels.Count), Size = maxLabelWidth + AXIS_BIG_TICK + AXIS_LABEL_OFFSET_FROM_TICK };
        }

        ///  <summary>
        ///  Draw the X axis as a series
        ///  </summary>
        private AxisScaleAndSize DrawXSeries(IList<string> labels)
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
            return new AxisScaleAndSize { AxisScale = new CategoryAxisScale(labels.Count), Size = maxHeight };
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

        ///  <summary>
        ///  Draw the set of markers whose centre device co-ordinates are in xys.
        ///  </summary>
        [Obsolete("Ideally subclasses would never need to use canvas co-ordinates")]
        protected void DrawMarkerSeriesInCanvasCoordinates(PointF[] xys, double size, MarkerShape shape, bool isFilled, Pen markerPen, Pen linePen, bool joinMarkersWithLines, bool drawMarkers)
        {
            // sort by x, then by y
            Array.Sort(xys, new SortXThenY());

            PointF oldXy;
            if (joinMarkersWithLines)
            {
                // Plot joining lines
                // Set initial values so that the first line won't be drawn
                oldXy = xys[0];
                foreach (PointF xy in xys)
                {
                    if (xy.X >= 0 && xy.Y >= 0 && oldXy.X >= 0 && oldXy.Y >= 0 && (xy.X != oldXy.X || xy.Y != oldXy.Y))
                    {
                        DrawLineInCanvasCoordinates(linePen, xy.X, xy.Y, oldXy.X, oldXy.Y);
                        oldXy = xy;
                    }
                }
            }

            if (drawMarkers)
            {
                oldXy = new PointF(-1, -1);
                foreach (PointF xy in xys)
                {
                    if (xy.X != oldXy.X || xy.Y != oldXy.Y)
                    {
                        if (xy.X >= 0 && xy.Y >= 0)
                            DrawMarkerInCanvasCoordinates(xy.X, xy.Y, size, shape, isFilled, markerPen);
                        oldXy = xy;
                    }
                }
            }
        }

        protected void SetStandardAsciiScaling(int yDivisions, AxisScales axisScales)
        {
            divx = axisScales.X.MaximumScaleValue - axisScales.X.MinimumScaleValue;
            offx = SafeToInt32(-(axisScales.X.MinimumScaleValue / divx * 60) + 15);
            divy = axisScales.Y.MaximumScaleValue - axisScales.Y.MinimumScaleValue;
            offy = SafeToInt32(-(axisScales.Y.MinimumScaleValue / divy * yDivisions) + ASCII_Ytxt);
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
            double width = ToCanvasWidth(chartX, scaleType);
            if (isXAxisReversed)
                return xExtCanvas + xAxisCanvas + xAxisCanvas - (offx + width);
            return offx + width;
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

        protected double ToCanvasY(double chartY)
        {
            ScaleType scaleType = ScaleType.Linear;
            if (HasScaleParameters && definition.ScaleParameters.Y != null)
                scaleType = definition.ScaleParameters.Y.ScaleType;
            return ToCanvasY(chartY, scaleType);
        }

        protected double ToCanvasY(double chartY, ScaleType scaleType)
        {
            double height = ToCanvasHeight(chartY, scaleType);
            if (isYAxisReversed)
                return yExtCanvas + yAxisCanvas + yAxisCanvas - (offy + height);
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

        protected void DrawMarkerInChartCoordinates(double x, double y, double size, MarkerShape shape, bool isFilled, Pen p)
        {
            statsDirectCanvas.DrawMarker(ToCanvasX(x), ToCanvasY(y), size, shape, isFilled, p);
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
                bool useColour = !ChartPreferences.DefaultAllBlack;
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
                SetSeriesFromMarkerTypeAndOptions(ds, ChartPreferences.MarkerTypes[mkr], null);
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
                    SetSeriesFromMarkerTypeAndOptions(ds, ChartPreferences.MarkerTypes[mkr], opts);
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
                axisLabelFont = ChartPreferences.FontFromSaveString(o.AxisLabelFontDescriptor);
            if (o.UsesAxisTitleFontDescriptor && !(string.IsNullOrEmpty(o.AxisTitleFontDescriptor)))
                axisTitleFont = ChartPreferences.FontFromSaveString(o.AxisTitleFontDescriptor);
            if (o.UsesLegendFontDescriptor && !(string.IsNullOrEmpty(o.LegendFontDescriptor)))
                legendFont = ChartPreferences.FontFromSaveString(o.LegendFontDescriptor);
            if (o.UsesTitleFontDescriptor && !(string.IsNullOrEmpty(o.TitleFontDescriptor)))
                titleFont = ChartPreferences.FontFromSaveString(o.TitleFontDescriptor);

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

                MarkerType clone = ChartPreferences.MarkerTypes[mkr].Clone();
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

        /// <summary>
        /// Detect and return minimum, minimum greater than zero and maximum values in the array.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="min">Filled in with the global minimum value</param>
        /// <param name="minGreaterThanZero">Filled in with the global minimum value that is greater than zero.</param>
        /// <param name="max">Filled in with the global maximum value</param>
        /// <remarks>STYLE: Wouldn't this be better as a function returning some kind of data structure?</remarks>
        protected void GetMinMaxArray(double[] data, out double min, out double max, out double minGreaterThanZero)
        {
            min = double.MaxValue;
            minGreaterThanZero = double.MaxValue;
            max = double.MinValue;
            for (int i = data.GetLowerBound(0); i <= data.GetUpperBound(0); i++)
            {
                if (data[i] != Constant.MISSING)
                {
                    if (data[i] < min)
                        min = data[i];
                    if ((data[i] > 0) && (data[i] < minGreaterThanZero))
                        minGreaterThanZero = data[i];
                    if (data[i] > max)
                        max = data[i];
                }
            }
        }

        /// <summary>
        /// Detect and return minimum and maximum values in the array.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="scaleType">The scale that will use the data.  For log scales, values of 0 or less are ignored.</param>
        /// <param name="min">Filled in with the global minimum value</param>
        /// <param name="max">Filled in with the global maximum value</param>
        /// <remarks>STYLE: Wouldn't this be better as a function returning some kind of data structure?</remarks>
        protected Layout.Range GetMinMaxArray(double[] data, ScaleType scaleType)
        {
            bool ignoreZeroOrLess = scaleType == ScaleType.LogNatural || scaleType == ScaleType.Log10;
            double min = double.MaxValue;
            double max = double.MinValue;
            for (int i = data.GetLowerBound(0); i <= data.GetUpperBound(0); i++)
            {
                if (data[i] != Constant.MISSING && !(ignoreZeroOrLess && data[i] <= 0))
                {
                    if (data[i] < min)
                        min = data[i];
                    if (data[i] > max)
                        max = data[i];
                }
            }
            return new Layout.Range(min, max);
        }

        /// <summary>
        ///  Plots an XY chart assuming an existing vector plot is open. This allows callers to use this then add other features to the chart before it is completed.
        /// </summary>
        /// <param name="x">The X co-ordinates of the points to plot.  Zero-based or 1-based.</param>
        /// <param name="y">The Y co-ordinates of the points to plot.  Zero-based or 1-based, same length as x.</param>
        /// <param name="xtxt">The X-axis title</param>
        /// <param name="ytxt">The y-axis title</param>
        /// <param name="title">The chart title</param>
        /// <param name="zPlot">If true, draw a line at the smallest Y value</param>
        /// <param name="minMaxY"></param>
        /// <param name="markerSize"></param>
        /// <param name="Shape"></param>
        /// <param name="isFilled"></param>
        /// <param name="p"></param>
        /// <param name="useCalculatedScalesEvenWithDefinition"></param>
        /// <remarks></remarks>
        protected void PlotXYInternal(double[] x, double[] y, string xtxt, string ytxt, string title, bool zPlot, DataMinMax minMaxY, double markerSize, MarkerShape shape, bool isFilled, Pen p, bool useCalculatedScalesEvenWithDefinition, double presetXMin = 0, double presetXMax = 0, double presetYMin = 0, double presetYMax = 0)
        {
            ScaleType scaleTypeX = ScaleType.Linear;
            ScaleType scaleTypeY = ScaleType.Linear;
            if (HasScaleParameters)
            {
                scaleTypeX = definition.ScaleParameters.X.ScaleType;
                scaleTypeY = definition.ScaleParameters.Y.ScaleType;
            }

            //  If required, get the Min and Max for the data
            //  This is safe because we're using this function to plot our data.
            double axisXMin;
            double axisXMinGreaterThanZero;
            double axisXMax;
            double axisYMin;
            double axisYMinGreaterThanZero;
            double axisYMax;
            switch (minMaxY)
            {
                case DataMinMax.XPreset_YPreset:
                    axisXMin = presetXMin;
                    axisXMinGreaterThanZero = presetXMin;
                    axisXMax = presetXMax;
                    axisYMin = presetYMin;
                    axisYMinGreaterThanZero = presetYMin;
                    axisYMax = presetYMax;
                    break;
                case DataMinMax.XUseScaleParameters_YUseScaleParameters:
                    //  Take data from scale parameters
                    axisYMax = definition.ScaleParameters.Y.Max;
                    axisYMinGreaterThanZero = definition.ScaleParameters.Y.MinGreaterThanZero;
                    axisYMin = definition.ScaleParameters.Y.Min;
                    axisXMax = definition.ScaleParameters.X.Max;
                    axisXMinGreaterThanZero = definition.ScaleParameters.X.MinGreaterThanZero;
                    axisXMin = definition.ScaleParameters.X.Min;
                    break;
                case DataMinMax.XY_CalcTogether:
                    // X and Y must have the same scale
                    GetMinMaxArray(x, out axisXMin, out axisXMax, out axisXMinGreaterThanZero);
                    GetMinMaxArray(y, out axisYMin, out axisYMax, out axisYMinGreaterThanZero);
                    axisYMin = axisXMin = Math.Min(axisYMin, axisXMin);
                    axisYMinGreaterThanZero = axisXMinGreaterThanZero = Math.Min(axisYMinGreaterThanZero, axisXMinGreaterThanZero);
                    axisYMax = axisXMax = Math.Max(axisYMax, axisXMax);
                    break;
                case DataMinMax.XCalc_YCalc:
                    GetMinMaxArray(x, out axisXMin, out axisXMax, out axisXMinGreaterThanZero);
                    GetMinMaxArray(y, out axisYMin, out axisYMax, out axisYMinGreaterThanZero);
                    break;
                case DataMinMax.XCalc_YPreset:
                    GetMinMaxArray(x, out axisXMin, out axisXMax, out axisXMinGreaterThanZero);
                    axisYMin = presetYMin;
                    axisYMinGreaterThanZero = presetYMin;
                    axisYMax = presetYMax;
                    break;
                default:
                    throw new ArgumentOutOfRangeException("MinMaxY", minMaxY, "Don't know how to plot using the given minMaxY");
            }

            DataMinY = axisYMin;
            DataMinGreaterThanZeroY = axisYMinGreaterThanZero;
            DataMaxY = axisYMax;
            DataMinX = axisXMin;
            DataMinGreaterThanZeroX = axisXMinGreaterThanZero;
            DataMaxX = axisXMax;

            DrawAxesOrEnlargeCanvas(title, new AxisDefinition(xtxt, AxisMode.Scale, scaleTypeX), new AxisDefinition(ytxt, AxisMode.Scale, scaleTypeY), false, useCalculatedScalesEvenWithDefinition);

            if (zPlot)
                DrawQCanvas(offy);

            // Plot the points
            int rows = x.Length;
            int xOffset = x.GetLowerBound(0);
            int yOffset = y.GetLowerBound(0);
            PointF[] xys = new PointF[rows];
            for (int r = 0; r < rows; r++)
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
            DrawMarkerSeriesInCanvasCoordinates(xys, markerSize, shape, isFilled, p, p, false, true);
        }

        ///  <summary>
        ///  Draw a horizontal line at the specified offset from the Y origin.
        ///  </summary>
        ///  <param name="y">The offset, in device units</param>
        ///  <remarks></remarks>
        protected void DrawQCanvas(double y)
        {
            using (Pen greenPen = new Pen(grGreen, 2))
            {
                DrawLineInCanvasCoordinates(greenPen, xAxisCanvas, y, xAxisCanvas + xExtCanvas, y);
            }
        }

        protected bool IsAscii { get { return null != definition && definition.IsAscii; } }

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
    }
}