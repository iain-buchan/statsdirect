using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using Layout;
using StatsDirect.Numerics;
using StatsDirect.Templates;
using StatsDirect.Utilities;

namespace StatsDirect.Charting.Renderer
{
    public abstract class AbstractChartRenderer : IDisposable
    {
        protected readonly double LOG2 = Math.Log(2.0);
        protected const int LEGEND_LEFT_GAP = 24;
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

        protected ChartDefinition Definition { get; set; }

        private IStatsDirectCanvas statsDirectCanvas;

        protected Font AxisLabelFont { get; private set; }
        protected Font AxisTitleFont { get; private set; }
        protected float AxisLineThickness { get; private set; }
        protected Pen AxisPen { get; private set; }
        private Brush axisBrush;
        private const double AXIS_LITTLE_TICK = 4;
        protected const double AXIS_BIG_TICK = 7;
        private Font TitleFont { get; set; }
        private Font LegendFont { get; set; }
        protected bool BoxAxes { get; } = ChartPreferences.DefaultBoxAxes;

        private Font LabelFont { get; set; }

        private bool isXAxisReversed;
        private bool isYAxisReversed;
        /// <summary>
        /// The X-position in canvas co-ordinates of the left-hand end of the chart's X-axis
        /// </summary>
        protected double XAxisCanvas { get; set; }
        /// <summary>
        /// The length in canvas co-ordinates of the chart's X-axis
        /// </summary>
        protected double XExtCanvas { get; set; }
        /// <summary>
        /// The Y-position in canvas co-ordinates of the bottom of the chart's Y-axis
        /// </summary>
        protected double YAxisCanvas { get; set; }
        protected double YExtCanvas { get; set; }

        protected double DivX { get; set; }
        protected double OffX { get; set; }
        protected double DivY { get; set; }
        protected double OffY { get; set; }

        private const int DEFAULT_METAFILE_HEIGHT = 800;
        private const int DEFAULT_METAFILE_WIDTH = 1132;
        protected const double DEFAULT_X_GAP = 80;
        protected const double DEFAULT_Y_GAP = 80;
        protected int imageHeight = DEFAULT_METAFILE_HEIGHT;
        protected int imageWidth = DEFAULT_METAFILE_WIDTH;
        protected const int LABEL_TO_AXIS_LABEL_GAP = 3;

        protected string[] TextCanvas { get; set; }

        protected const int ASCII_YTxt = 3;
        protected const int ASCII_XTxt = 15;
        private const int ASCII_XExt = 60;


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

            Definition = definition;
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
        protected static Range GetMinMaxSort(List<Series> seriesToUse)
        {
            double min = double.MaxValue;
            double max = double.MinValue;
            foreach (Series s in seriesToUse)
            {
                DoubleSeries ds = (DoubleSeries)s;
                Array.Sort(ds.Data);
                if (ds.Data[0] < min)
                    min = ds.Data[0];
                if (ds.Data[ds.Data.Length - 1] > max)
                    max = ds.Data[ds.Data.Length - 1];
            }
            return new Range(min, max);
        }

        ///  <summary>
        ///  Prepare to plot a vector chart to the specified stream.
        ///  </summary>
        ///  <remarks></remarks>
        protected void StartVectorPlot(GenericOptions options = null, Legend legend = null)
        {
            if (!ChartPreferences.AreSharedValuesInitialised)
                ChartPreferences.InitSharedValues();

            //  Drawing objects
            if (!ReconstituteFonts())
            {
                ChartPreferences.InitFirstFonts();
                if (!ReconstituteFonts())
                    throw new Exception("Cannot find the fonts that StatsDirect uses for charting. If Calibri is not installed on your system, you can download it from https://www.microsoft.com/typography/fonts/font.aspx?FMID=1710");
            }

            AxisPen = new Pen(GrAxis, 1);
            axisBrush = new SolidBrush(Color.Black);

            // If we have any non-default options, set them now, so that we know what size e.g. fonts are when we're calculating e.g. legends.
            if (null != options)
                SetFontsAndThicknessesFromOptions(options);

            int extraWidth = 0;
            int extraHeight = 0;

            if (null != legend)
            {
                // Temporary canvas for sizing strings for legends
                statsDirectCanvas = canvasFactory.Create(1, 1);
                switch (legend.Position)
                {
                    case LegendPosition.Bottom:
                        extraHeight += (int)Math.Ceiling(ChartPartSizer.Size(this, legend).Height);
                        break;
                    case LegendPosition.Left:
                        extraWidth += (int)Math.Ceiling(ChartPartSizer.Size(this, legend).Width);
                        break;
                    default:
                        throw new NotImplementedException("Only Left and Bottom legend locations are known");
                }
                statsDirectCanvas.Dispose();
                statsDirectCanvas = null;
            }

            statsDirectCanvas = canvasFactory.Create(imageWidth + extraWidth, imageHeight + extraHeight);
        }

        ///  <summary>
        ///  Set up some appropriate default axes, allowing room for axis labels and for other elements that might be on the canvas.
        ///  </summary>
        ///  <remarks>Precondition: Chart is graphical, not ASCII</remarks>
        protected void DefaultAxes(Margin margin, Size axisLabelAndTicSpace)
        {
            XAxisCanvas = DEFAULT_X_GAP + (margin?.Left ?? 0) + axisLabelAndTicSpace.Width;
            YAxisCanvas = DEFAULT_Y_GAP + (margin?.Bottom ?? 0) + axisLabelAndTicSpace.Height;
            XExtCanvas = ImageWidth - XAxisCanvas - (margin?.Right ?? 0) - DEFAULT_X_GAP;
            YExtCanvas = ImageHeight - YAxisCanvas - DEFAULT_Y_GAP - (margin?.Top ?? 0);
        }

        protected void DefaultAsciiAxes()
        {
            XAxisCanvas = ASCII_XTxt;
            XExtCanvas = ASCII_XExt;
            YAxisCanvas = 2; // Leave a line for the X-axis title, and another for the scale.  The axis will be plotted on line 2.
            YExtCanvas = TextCanvas.Length - 5;
        }

        private bool ReconstituteFonts()
        {
            AxisLabelFont = ChartPreferences.FontFromSaveString(ChartPreferences.DefaultAxisLabelFont);
            AxisTitleFont = ChartPreferences.FontFromSaveString(ChartPreferences.DefaultAxisTitleFont);
            LabelFont = ChartPreferences.FontFromSaveString(ChartPreferences.DefaultLabelFont);
            LegendFont = ChartPreferences.FontFromSaveString(ChartPreferences.DefaultLegendFont);
            TitleFont = ChartPreferences.FontFromSaveString(ChartPreferences.DefaultTitleFont);
            return null != AxisLabelFont && null != AxisTitleFont && null != LabelFont && null != LegendFont && null != TitleFont;
        }

        ///  <summary>
        ///  Stop plotting a vector chart and release resources.
        ///  </summary>
        ///  <remarks></remarks>
        protected void EndVectorPlot()
        {
            //  Series are kept in case of redoing a preview.  TODO: Is this appropriate?  Isn't a new renderer used each time?

            if (null != AxisPen)
            {
                AxisPen.Dispose();
                AxisPen = null;
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
            if (!string.IsNullOrEmpty(title))
                DrawStringInCanvasCoordinates(title, TitleFont, Brushes.Black, XExtCanvas / 2 + XAxisCanvas, YAxisCanvas + YExtCanvas + 60, StringAlignment.Center, StringAlignment.Near);
        }

        public void DrawXAxisTitle(string title, double gapForAxisLabels)
        {
            if (!string.IsNullOrEmpty(title))
                DrawStringInCanvasCoordinates(title, AxisTitleFont, Brushes.Black, XExtCanvas / 2 + XAxisCanvas, YAxisCanvas - gapForAxisLabels - LABEL_TO_AXIS_LABEL_GAP, StringAlignment.Center, StringAlignment.Near);
        }

        public void DrawYAxisTitle(string title, double axisWidth)
        {
            double rightOfYAxisTitle = XAxisCanvas - axisWidth - LABEL_TO_AXIS_LABEL_GAP;

            if (!string.IsNullOrEmpty(title))
            {
                using (StringFormat txtFormat = new StringFormat())
                {
                    txtFormat.Alignment = StringAlignment.Center;
                    txtFormat.LineAlignment = StringAlignment.Far;
                    statsDirectCanvas.DrawStringAtAngle(title, AxisTitleFont, Brushes.Black, rightOfYAxisTitle, YExtCanvas / 2.0 + YAxisCanvas, txtFormat, LabelDirection.Up);
                }
            }
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
        /// <param name="legend"></param>
        /// <param name="isSquarePlotArea">If true, the plot area is sized to square (the larger axis length will be reduced to the size of the smaller). If false, the usual rectangular plot area will be used.</param>
        protected AxisScales LayoutChartAndDrawAxes(string title, AxisDefinition x, AxisDefinition y, bool shouldBoxAxes, bool useCalculatedScalesEvenWithDefinition, Legend legend = null, bool isSquarePlotArea = false)
        {
            SizeF legendSize = default(SizeF);
            if (null != legend)
                legendSize = ChartPartSizer.Size(this, legend);

            Margin plotAreaMargins = new Margin
            {
                Bottom = y.ExtraSpaceBeforeAxisStarts + (null != legend && legend.Position == LegendPosition.Bottom ? legendSize.Height : 0),
                Left = x.ExtraSpaceBeforeAxisStarts + (null != legend && legend.Position == LegendPosition.Left ? legendSize.Width : 0),
                Right = x.ExtraSpaceAfterAxisEnds,
                Top = y.ExtraSpaceAfterAxisEnds
            };

            Size extraSizeForAxes = CalculateAxisSizes(title, x, y, useCalculatedScalesEvenWithDefinition);

            if (IsAscii)
                DefaultAsciiAxes();
            else
                DefaultAxes(plotAreaMargins, extraSizeForAxes);
            if (isSquarePlotArea)
            {
                double smallerExt = Math.Min(XExtCanvas, YExtCanvas);
                XExtCanvas = smallerExt;
                YExtCanvas = smallerExt;
            }
            AxisScales ass = DrawAxes(title, x, y, shouldBoxAxes, useCalculatedScalesEvenWithDefinition, extraSizeForAxes);
            return ass;
        }

        protected AxisScales DrawAxes(string title, AxisDefinition x, AxisDefinition y, bool shouldBoxAxes, bool useCalculatedScalesEvenWithDefinition, Size extraSizeForAxes)
        {
            if (IsAscii)
            {
                if ((x.Mode & AxisMode.Line) == AxisMode.Line)
                    WriteAsciiYX((int)YAxisCanvas, (int)XAxisCanvas, new string('-', (int)XExtCanvas));

                if ((y.Mode & AxisMode.Line) == AxisMode.Line)
                {
                    for (int row = (int)YAxisCanvas + 1; row <= (int)YAxisCanvas + (int)YExtCanvas; row++)
                        WriteAsciiYX(row, (int)XAxisCanvas - 1, "|");
                }
                if ((x.Mode & AxisMode.Line) == AxisMode.Line && (y.Mode & AxisMode.Line) == AxisMode.Line)
                    WriteAsciiYX((int)YAxisCanvas, (int)XAxisCanvas - 1, "/");
            }
            else
            {
                // Draw the axis lines
                if ((x.Mode & AxisMode.Line) == AxisMode.Line)
                    AxisDrawline(XAxisCanvas, YAxisCanvas, XAxisCanvas + XExtCanvas, YAxisCanvas);

                if ((y.Mode & AxisMode.Line) == AxisMode.Line)
                    AxisDrawline(XAxisCanvas, YAxisCanvas + YExtCanvas, XAxisCanvas, YAxisCanvas);

                if (shouldBoxAxes)
                {
                    if ((x.Mode & AxisMode.Line) == AxisMode.Line)
                        AxisDrawline(XAxisCanvas + XExtCanvas, YAxisCanvas + YExtCanvas, XAxisCanvas, YAxisCanvas + YExtCanvas);
                    if ((y.Mode & AxisMode.Line) == AxisMode.Line)
                        AxisDrawline(XAxisCanvas + XExtCanvas, YAxisCanvas + YExtCanvas, XAxisCanvas + XExtCanvas, YAxisCanvas);
                }
            }

            IAxisScale xAxisScale;
            isXAxisReversed = x.Reverse;
            isYAxisReversed = y.Reverse;
            switch (x.Mode)
            {
                case AxisMode.LineOnly:
                case AxisMode.None:
                    //  Do nothing
                    xAxisScale = null;
                    DivX = 1;
                    OffX = XAxisCanvas;
                    break;
                case AxisMode.ReverseScale:
                case AxisMode.Scale:
                case AxisMode.ScaleWithoutLabels:
                    xAxisScale = DrawXScale((x.Mode & AxisMode.Labels) == AxisMode.Labels, x.ScaleType, useCalculatedScalesEvenWithDefinition);
                    // DrawXScale sets divx and offx
                    break;
                case AxisMode.Series:
                    if (null != x.Series)
                    {
                        xAxisScale = DrawXSeries(x.Series.Select(s => s.Title).ToList());
                        DivX = x.Series.Count;
                        OffX = XAxisCanvas;
                    }
                    else if (null != x.Labels)
                    {
                        xAxisScale = DrawXSeries(x.Labels);
                        DivX = x.Labels.Count;
                        OffX = XAxisCanvas;
                    }
                    else
                        xAxisScale = null;
                    break;
                default:
                    throw new NotImplementedException("Unknown X axis scale mode");
            }

            IAxisScale yAxisScale;
            switch (y.Mode)
            {
                case AxisMode.LineOnly:
                case AxisMode.None:
                    //  Do nothing
                    yAxisScale = null;
                    DivY = 1;
                    OffY = YAxisCanvas;
                    break;
                case AxisMode.ReverseScale:
                case AxisMode.Scale:
                case AxisMode.ScaleWithoutLabels:
                    yAxisScale = DrawYScale((x.Mode & AxisMode.Labels) == AxisMode.Labels, y.ScaleType, useCalculatedScalesEvenWithDefinition);
                    break;
                case AxisMode.Series:
                    if (null != y.Series)
                    {
                        yAxisScale = DrawYSeries(y.Series.Select(s => s.Title).ToList());
                        DivY = y.Series.Count;
                        OffY = YAxisCanvas;
                    }
                    else if (null != y.Labels)
                    {
                        yAxisScale = DrawYSeries(y.Labels);
                        DivY = y.Labels.Count;
                        OffY = YAxisCanvas;
                    }
                    else
                        yAxisScale = null;
                    break;
                default:
                    throw new NotImplementedException("Unknown X axis scale mode");
            }

            AxisScales axisScales = new AxisScales { X = xAxisScale, Y = yAxisScale };

            if (!IsAscii)
            {
                DrawXAxisTitle(x.Title, extraSizeForAxes.Height);
                DrawYAxisTitle(y.Title, extraSizeForAxes.Width);
            }

            // Draw the chart title now that we know it's safe to do so.
            if (IsAscii)
            {
                int s = 40 - title.Length / 2;
                WriteAsciiYX(TextCanvas.GetUpperBound(0), s, title);
            }
            else
                DrawTitle(title);
            return axisScales;
        }

        protected Size CalculateAxisSizes(string title, AxisDefinition x, AxisDefinition y, bool useCalculatedScalesEvenWithDefinition)
        {
            double xHeight;
            isXAxisReversed = x.Reverse;
            isYAxisReversed = y.Reverse;
            // ReSharper disable once SwitchStatementMissingSomeCases
            switch (x.Mode)
            {
                case AxisMode.LineOnly:
                case AxisMode.None:
                    xHeight = 0;
                    break;
                case AxisMode.ReverseScale:
                case AxisMode.Scale:
                case AxisMode.ScaleWithoutLabels:
                    xHeight = CalculateXScaleHeight((x.Mode & AxisMode.Labels) == AxisMode.Labels, x.ScaleType, useCalculatedScalesEvenWithDefinition);
                    break;
                case AxisMode.Series:
                    if (null != x.Series)
                        xHeight = CalculateXSeriesHeight(x.Series.Select(s => s.Title).ToList());
                    else if (null != x.Labels)
                        xHeight = CalculateXSeriesHeight(x.Labels);
                    else
                        xHeight = 0;
                    break;
                default:
                    throw new NotImplementedException("Unknown X axis scale mode");
            }

            double yWidth;
            // ReSharper disable once SwitchStatementMissingSomeCases
            switch (y.Mode)
            {
                case AxisMode.LineOnly:
                case AxisMode.None:
                    //  Do nothing
                    yWidth = 0;
                    break;
                case AxisMode.ReverseScale:
                case AxisMode.Scale:
                case AxisMode.ScaleWithoutLabels:
                    yWidth = CalculateYScaleWidth((x.Mode & AxisMode.Labels) == AxisMode.Labels, y.ScaleType, useCalculatedScalesEvenWithDefinition);
                    break;
                case AxisMode.Series:
                    if (null != y.Series)
                        yWidth = CalculateYSeriesWidth(y.Series.Select(s => s.Title).ToList());
                    else if (null != y.Labels)
                        yWidth = CalculateYSeriesWidth(y.Labels);
                    else
                        yWidth = 0;
                    break;
                default:
                    throw new NotImplementedException("Unknown X axis scale mode");
            }

            return new Size((int)yWidth, (int)xHeight);
        }

        private IAxisScale DrawXScale(bool drawLabels, ScaleType scaleType, bool useCalculatedScalesEvenWithDefinition)
        {
            IAxisScale xAxisScale;
            if (IsAscii || drawLabels)
                xAxisScale = Q_AxisOrFromDefinition(DataMinX, DataMinGreaterThanZeroX, DataMaxX, false, scaleType, useCalculatedScalesEvenWithDefinition);
            else
            {
                //  Not ASCII, not drawing our own labels, so just set up 20 divisions
                xAxisScale = new LinearAxisScale(DataMinX, DataMaxX, DataMinX, DataMaxX, 5);
            }

            DataMinX = xAxisScale.MinimumDataValue;
            DataMaxX = xAxisScale.MaximumDataValue;
            DivX = Transform(xAxisScale.MaximumScaleValue, scaleType) - Transform(xAxisScale.MinimumScaleValue, scaleType);
            OffX = -(Transform(xAxisScale.MinimumScaleValue, scaleType) / DivX * XExtCanvas) + XAxisCanvas;

            if (!IsAscii)
            {
                LabelDirection direction = LabelDirection.Across;
                bool hasGridLines = false;
                DashStyle gridLineDashStyle = DashStyle.Solid;
                if (HasScaleParameters && Definition.ScaleParameters.X != null)
                {
                    direction = Definition.ScaleParameters.X.LabelDirection;
                    hasGridLines = Definition.ScaleParameters.X.HasGridLines;
                    gridLineDashStyle = Definition.ScaleParameters.X.GridLineDashStyle;
                }
                using (Pen gridLinePen = new Pen(AxisPen.Color, 1))
                {
                    gridLinePen.DashStyle = gridLineDashStyle;
                    foreach (Tic tic in xAxisScale.Tics())
                    {
                        double x1 = ToCanvasX(tic.Value);
                        if (drawLabels)
                        {
                            AxisDrawStringAtAngleCT(tic.Label, x1, YAxisCanvas - AXIS_BIG_TICK, direction);
                            AxisDrawline(x1, YAxisCanvas - AXIS_BIG_TICK, x1, YAxisCanvas);
                        }
                        else
                        {
                            AxisDrawline(x1, YAxisCanvas - AXIS_LITTLE_TICK, x1, YAxisCanvas);
                        }
                        if (hasGridLines)
                            statsDirectCanvas.DrawLine(gridLinePen, x1, YAxisCanvas, x1, YAxisCanvas + YExtCanvas);
                    }
                }
            }
            else
            {
                //  ASCII - always linear for now.  TODO: Log
                foreach (Tic tic in xAxisScale.Tics())
                {
                    string lab = tic.Label;
                    int s = ToAsciiX(tic.Value);
                    int s2 = s - (lab[0] == '-' ? 1 : 0);
                    WriteAsciiYX(ASCII_YTxt - 2, s2, lab);
                    WriteAsciiYX(ASCII_YTxt - 1, s, "+");
                }
            }
            return xAxisScale;
        }

        private double CalculateXScaleHeight(bool drawLabels, ScaleType scaleType, bool useCalculatedScalesEvenWithDefinition)
        {
            IAxisScale xAxisScale;
            if (IsAscii || drawLabels)
                xAxisScale = Q_AxisOrFromDefinition(DataMinX, DataMinGreaterThanZeroX, DataMaxX, false, scaleType, useCalculatedScalesEvenWithDefinition);
            else
            {
                //  Not ASCII, not drawing our own labels, so just set up 20 divisions
                xAxisScale = new LinearAxisScale(DataMinX, DataMaxX, DataMinX, DataMaxX, 5);
            }

            DataMinX = xAxisScale.MinimumDataValue;
            DataMaxX = xAxisScale.MaximumDataValue;
            // set a string mask that will fit OK

            float labelHeight = 0;
            if (!IsAscii)
            {
                LabelDirection direction = LabelDirection.Across;
                if (HasScaleParameters && Definition.ScaleParameters.X != null)
                    direction = Definition.ScaleParameters.X.LabelDirection;
                foreach (Tic tic in xAxisScale.Tics())
                {
                    //  Major tic - may or may not be labelled
                    if (drawLabels)
                        labelHeight = Math.Max(AxisMeasureStringAtAngle(tic.Label, direction).Height, labelHeight);
                }
            }
            else
            {
                //  ASCII
                foreach (Tic tic in xAxisScale.Tics())
                    labelHeight = Math.Max(labelHeight, tic.Label.Length);
            }
            return labelHeight + AXIS_BIG_TICK;
        }

        private IAxisScale Q_AxisOrFromDefinition(double qmin, double qMinGreaterThanZero, double qmax, bool isY, ScaleType scaleType, bool useCalculatedScalesEvenWithDefinition)
        {
            if (Definition != null && Definition.HasScaleParameters && !useCalculatedScalesEvenWithDefinition)
            {
                //  Use the values in our scale parameters
                AxisScaleParameters asp = isY ? Definition.ScaleParameters.Y : Definition.ScaleParameters.X;
                if (asp?.AxisScale != null)
                    return asp.AxisScale;
            }
            //  If we get here, there was no prior definition - calculate it ourselves.
            return AxisScalerFactory.AxisScalerFor(scaleType).Q_Axis(qmin, qMinGreaterThanZero, qmax, isY, false);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="drawLabels"></param>
        /// <param name="scaleType"></param>
        /// <param name="useCalculatedScalesEvenWithDefinition"></param>
        /// <returns>The width of the axis, ticks, gap to labels, and labels</returns>
        private IAxisScale DrawYScale(bool drawLabels, ScaleType scaleType, bool useCalculatedScalesEvenWithDefinition)
        {
            const double AXIS_LABEL_OFFSET_FROM_BIG_TICK = 8;

            // find a neat axis division
            IAxisScale yAxisScale = Q_AxisOrFromDefinition(DataMinY, DataMinGreaterThanZeroY, DataMaxY, true, scaleType, useCalculatedScalesEvenWithDefinition);
            DataMinY = yAxisScale.MinimumDataValue;
            DataMaxY = yAxisScale.MaximumDataValue;
            DivY = Transform(yAxisScale.MaximumScaleValue, scaleType) - Transform(yAxisScale.MinimumScaleValue, scaleType);
            OffY = -(Transform(yAxisScale.MinimumScaleValue, scaleType) / DivY * YExtCanvas) + YAxisCanvas;

            // set a string mask that will fit OK
            LabelDirection direction = LabelDirection.Across;
            bool hasGridLines = false;
            DashStyle gridLineDashStyle = DashStyle.Solid;
            if (HasScaleParameters && Definition.ScaleParameters.Y != null)
            {
                direction = Definition.ScaleParameters.Y.LabelDirection;
                hasGridLines = Definition.ScaleParameters.Y.HasGridLines;
                gridLineDashStyle = Definition.ScaleParameters.Y.GridLineDashStyle;
            }

            if (!IsAscii)
            {
                using (Pen gridLinePen = new Pen(AxisPen.Color, 1))
                {
                    gridLinePen.DashStyle = gridLineDashStyle;
                    foreach (Tic tic in yAxisScale.Tics())
                    {
                        double y1 = ToCanvasY(tic.Value);
                        if (drawLabels)
                        {
                            AxisDrawStringAtAngleRM(tic.Label, XAxisCanvas - (AXIS_BIG_TICK + AXIS_LABEL_OFFSET_FROM_BIG_TICK), y1, direction);
                            AxisDrawline(XAxisCanvas - AXIS_BIG_TICK, y1, XAxisCanvas, y1);
                        }
                        else
                        {
                            AxisDrawline(XAxisCanvas - AXIS_LITTLE_TICK, y1, XAxisCanvas, y1);
                        }
                        if (hasGridLines)
                            statsDirectCanvas.DrawLine(gridLinePen, XAxisCanvas, y1, XAxisCanvas + XExtCanvas, y1);
                    }
                }
            }
            else
            {
                // ASCII
                foreach (Tic tic in yAxisScale.Tics())
                {
                    int y = ToAsciiY(tic.Value);
                    string lab = tic.Label;
                    WriteAsciiYX(y, (int)XAxisCanvas - 1 - lab.Length, lab);
                    WriteAsciiYX(y, (int)XAxisCanvas - 1, "+");
                }
            }
            return yAxisScale;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="drawLabels"></param>
        /// <param name="scaleType"></param>
        /// <param name="useCalculatedScalesEvenWithDefinition"></param>
        /// <returns>The width of the axis, ticks, gap to labels, and labels</returns>
        private double CalculateYScaleWidth(bool drawLabels, ScaleType scaleType, bool useCalculatedScalesEvenWithDefinition)
        {
            const double AXIS_LABEL_OFFSET_FROM_BIG_TICK = 8;

            // find a neat axis division
            IAxisScale yAxisScale = Q_AxisOrFromDefinition(DataMinY, DataMinGreaterThanZeroY, DataMaxY, true, scaleType, useCalculatedScalesEvenWithDefinition);
            DataMinY = yAxisScale.MinimumDataValue;
            DataMaxY = yAxisScale.MaximumDataValue;

            // set a string mask that will fit OK
            LabelDirection direction = LabelDirection.Across;
            if (HasScaleParameters && Definition.ScaleParameters.Y != null)
                direction = Definition.ScaleParameters.Y.LabelDirection;

            double maxLabelWidth = 0;
            if (!IsAscii)
            {
                foreach (Tic tic in yAxisScale.Tics())
                {
                    if (drawLabels)
                    {
                        maxLabelWidth = Math.Max(maxLabelWidth, AxisMeasureStringAtAngle(tic.Label, direction).Width);
                    }
                }
            }
            return maxLabelWidth + AXIS_BIG_TICK + AXIS_LABEL_OFFSET_FROM_BIG_TICK;
        }

        ///  <summary>
        ///  Draw the Y axis as a series
        ///  </summary>
        /// <returns>The width of the axis, ticks, gap to labels, and labels</returns>
        ///  <remarks>Labels are drawn centred between tics</remarks>
        private IAxisScale DrawYSeries(IList<string> labels)
        {
            const int AXIS_LABEL_OFFSET_FROM_TICK = 3;

            if (!IsAscii)
            {
                // Vector
                using (StringFormat txtFormat = new StringFormat())
                {
                    txtFormat.Alignment = StringAlignment.Far;
                    txtFormat.LineAlignment = StringAlignment.Center;
                    LabelDirection direction = LabelDirection.Across;
                    bool hasGridLines = false;
                    DashStyle gridLineDashStyle = DashStyle.Solid;
                    if (HasScaleParameters && Definition.ScaleParameters.Y != null)
                    {
                        direction = Definition.ScaleParameters.Y.LabelDirection;
                        hasGridLines = Definition.ScaleParameters.Y.HasGridLines;
                        gridLineDashStyle = Definition.ScaleParameters.Y.GridLineDashStyle;
                    }
                    using (Pen gridLinePen = new Pen(AxisPen.Color, 1))
                    {
                        gridLinePen.DashStyle = gridLineDashStyle;
                        double count = labels.Count;
                        for (int y = 0; y < labels.Count; y++)
                        {
                            double yctr = YAxisCanvas + YExtCanvas - (y + 0.5) / count * YExtCanvas;
                            double ytic = YAxisCanvas + YExtCanvas - y / count * YExtCanvas;
                            statsDirectCanvas.DrawStringAtAngle(labels[y], AxisLabelFont, axisBrush, XAxisCanvas - (AXIS_BIG_TICK + AXIS_LABEL_OFFSET_FROM_TICK), yctr, txtFormat, direction);
                            AxisDrawline(XAxisCanvas - AXIS_BIG_TICK, ytic, XAxisCanvas, ytic);
                            if (hasGridLines)
                                statsDirectCanvas.DrawLine(gridLinePen, XAxisCanvas, ytic, XAxisCanvas + XExtCanvas, ytic);
                        }
                    }
                }
            }
            else
            {
                //  ASCII
                for (int y = 0; y < labels.Count; y++)
                {
                    int y2 = 3 + y * 2;
                    int l = labels[y].Length;
                    int q = 13 - l;
                    if (l >= 13)
                        q = 1;
                    WriteAsciiYX(y2, q, labels[y].Substring(0, Math.Min(l, 13)));
                    WriteAsciiYX(y2, 14, "|");
                    WriteAsciiYX(y2 + 1, 14, "+");
                }
            }

            return new CategoryAxisScale(labels.Count);
        }

        /// <returns>The width of the axis, ticks, gap to labels, and labels</returns>
        private double CalculateYSeriesWidth(IList<string> labels)
        {
            const int AXIS_LABEL_OFFSET_FROM_TICK = 3;

            double maxLabelWidth = 0;
            if (!IsAscii)
            {
                // Vector
                LabelDirection direction = LabelDirection.Across;
                if (HasScaleParameters && Definition.ScaleParameters.Y != null)
                    direction = Definition.ScaleParameters.Y.LabelDirection;
                for (int y = 0; y < labels.Count; y++)
                    maxLabelWidth = Math.Max(maxLabelWidth, statsDirectCanvas.MeasureStringAtAngle(labels[y], AxisLabelFont, direction).Width);
            }

            return maxLabelWidth + AXIS_BIG_TICK + AXIS_LABEL_OFFSET_FROM_TICK;
        }

        ///  <summary>
        ///  Draw the X axis as a series
        ///  </summary>
        private IAxisScale DrawXSeries(IList<string> labels)
        {
            if (!IsAscii)
            {
                using (StringFormat txtFormat = new StringFormat())
                {
                    txtFormat.Alignment = StringAlignment.Center;
                    txtFormat.LineAlignment = StringAlignment.Near;
                    LabelDirection direction = LabelDirection.Across;
                    bool hasGridLines = false;
                    DashStyle gridLineDashStyle = DashStyle.Solid;
                    if (HasScaleParameters && Definition.ScaleParameters.X != null)
                    {
                        direction = Definition.ScaleParameters.X.LabelDirection;
                        hasGridLines = Definition.ScaleParameters.X.HasGridLines;
                        gridLineDashStyle = Definition.ScaleParameters.X.GridLineDashStyle;
                    }
                    using (Pen gridLinePen = new Pen(AxisPen.Color, 1))
                    {
                        gridLinePen.DashStyle = gridLineDashStyle;
                        double count = labels.Count;
                        for (int x = 0; x < labels.Count; x++)
                        {
                            double xctr = XAxisCanvas + (x + 0.5) / count * XExtCanvas;
                            double xtic = XAxisCanvas + (x + 1.0) / count * XExtCanvas;
                            statsDirectCanvas.DrawStringAtAngle(labels[x], AxisLabelFont, axisBrush, xctr, YAxisCanvas - AXIS_BIG_TICK, txtFormat, direction);
                            AxisDrawline(xtic, YAxisCanvas - AXIS_BIG_TICK, xtic, YAxisCanvas);
                            if (hasGridLines)
                                statsDirectCanvas.DrawLine(gridLinePen, xtic, YAxisCanvas, xtic, YAxisCanvas + YExtCanvas);
                        }
                    }
                }
            }
            return new CategoryAxisScale(labels.Count);
        }


        private double CalculateXSeriesHeight(IList<string> labels)
        {
            double maxHeight = 0;
            if (!IsAscii)
            {
                LabelDirection direction = LabelDirection.Across;
                if (HasScaleParameters && Definition.ScaleParameters.X != null)
                    direction = Definition.ScaleParameters.X.LabelDirection;
                for (int x = 0; x < labels.Count; x++)
                    maxHeight = Math.Max(maxHeight, statsDirectCanvas.MeasureStringAtAngle(labels[x], AxisLabelFont, direction).Height);
            }
            return maxHeight;
        }

        protected void AxisDrawline(double x1, double y1, double x2, double y2)
        {
            statsDirectCanvas.DrawLine(AxisPen, x1, y1, x2, y2);
        }

        /// <summary>
        /// Draw axis text aligned to the right
        /// </summary>
        protected void AxisDrawStringAtAngleRM(string txt, double x1, double y1, LabelDirection direction)
        {
            using (StringFormat alignTxt = new StringFormat())
            {
                alignTxt.Alignment = StringAlignment.Far;
                alignTxt.LineAlignment = StringAlignment.Center;
                statsDirectCanvas.DrawStringAtAngle(txt, AxisLabelFont, axisBrush, x1, y1, alignTxt, direction);
            }
        }

        /// <summary>
        /// Draw axis text aligned to the right
        /// </summary>
        protected SizeF AxisMeasureStringAtAngle(string txt, LabelDirection direction)
        {
            using (StringFormat alignTxt = new StringFormat())
            {
                alignTxt.Alignment = StringAlignment.Far;
                alignTxt.LineAlignment = StringAlignment.Center;
                return statsDirectCanvas.MeasureStringAtAngle(txt, AxisLabelFont, direction);
            }
        }

        ///  <summary>
        ///  Draw axis text aligned to the centre
        ///  </summary>
        protected void AxisDrawStringAtAngleCT(string txt, double x1, double y1, LabelDirection direction)
        {
            using (StringFormat alignTxt = new StringFormat())
            {
                alignTxt.Alignment = StringAlignment.Center;
                alignTxt.LineAlignment = StringAlignment.Near;
                statsDirectCanvas.DrawStringAtAngle(txt, AxisLabelFont, axisBrush, x1, y1, alignTxt, direction);
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
        ///  Draw legend text aligned to the left horizontally, middle vertically
        ///  </summary>
        ///  <remarks></remarks>
        protected void DrawStringLegendLC(string txt, double x, double y)
        {
            DrawStringLabel(txt, x, y, StringAlignment.Near, StringAlignment.Center);
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
                statsDirectCanvas.DrawString(txt, LegendFont, axisBrush, x, y, alignTxt);
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
                statsDirectCanvas.DrawString(txt, LabelFont, axisBrush, x, y, alignTxt);
            }
        }

        protected void DrawStringLabel(string txt, double x, double y, StringAlignment alignment, StringAlignment lineAlignment)
        {
            using (StringFormat alignTxt = new StringFormat())
            {
                alignTxt.Alignment = alignment;
                alignTxt.LineAlignment = lineAlignment;
                statsDirectCanvas.DrawString(txt, LabelFont, axisBrush, x, y, alignTxt);
            }
        }

        protected void DrawStringInCanvasCoordinates(string txt, Font f, Brush b, double x, double y, StringAlignment alignment, StringAlignment lineAlignment)
        {
            using (StringFormat alignTxt = new StringFormat())
            {
                alignTxt.Alignment = alignment;
                alignTxt.LineAlignment = lineAlignment;
                statsDirectCanvas.DrawString(txt, f, b, x, y, alignTxt);
            }
        }

#if WARN_OBSOLETES
        [Obsolete("Ideally subclasses would never need to use canvas co-ordinates")]
#endif
        protected void DrawMarkerInCanvasCoordinates(double x, double y, double size, DoubleSeries series)
        {
            statsDirectCanvas.DrawMarker(x, y, size, series.MarkerType.MarkerShape, series.MarkerType.IsMarkerFilled, GetMarkerPen(series.MarkerType));
        }

#if WARN_OBSOLETES
        [Obsolete("Ideally subclasses would never need to use canvas co-ordinates")]
#endif
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
#if WARN_OBSOLETES
        [Obsolete("Ideally subclasses would never need to use canvas co-ordinates")]
#endif
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
                        DrawLineInCanvasCoordinates(linePen, xy.X, xy.Y, oldXy.X, oldXy.Y);
                    oldXy = xy;
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

        ///  <summary>
        ///  Draw the set of markers whose centre device co-ordinates are in xys.
        ///  </summary>
#if WARN_OBSOLETES
        [Obsolete("Ideally subclasses would never need to use canvas co-ordinates")]
#endif
        protected void DrawMarkerSeriesInCanvasCoordinates(PointF[] xys, int size, MarkerType mt, bool joinMarkersWithLines, bool drawMarkers)
        {
            // sort by x, then by y
            Array.Sort(xys, new SortXThenY());

            PointF oldXy;
            if (joinMarkersWithLines)
            {
                // Plot joining lines
                // Set initial values so that the first line won't be drawn
                oldXy = xys[0];
                using (Pen linePen = GetLinePen(mt, false))
                {
                    foreach (PointF xy in xys)
                    {
                        if (xy.X >= 0 && xy.Y >= 0 && oldXy.X >= 0 && oldXy.Y >= 0 && (xy.X != oldXy.X || xy.Y != oldXy.Y))
                            DrawLineInCanvasCoordinates(linePen, xy.X, xy.Y, oldXy.X, oldXy.Y);
                        oldXy = xy;
                    }
                }
            }

            if (drawMarkers)
            {
                using (Pen markerPen = GetMarkerPen(mt))
                {
                    oldXy = new PointF(-1, -1);
                    foreach (PointF xy in xys)
                    {
                        if (xy.X != oldXy.X || xy.Y != oldXy.Y)
                        {
                            if (xy.X >= 0 && xy.Y >= 0)
                                DrawMarkerInCanvasCoordinates(xy.X, xy.Y, size, mt.MarkerShape, mt.IsMarkerFilled, markerPen);
                            oldXy = xy;
                        }
                    }
                }
            }
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
                    return (float)chartValue;
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
            if (HasScaleParameters && Definition.ScaleParameters.X != null)
                scaleType = Definition.ScaleParameters.X.ScaleType;
            return ToCanvasWidth(chartX, scaleType);
        }

        protected double ToCanvasWidth(double chartX, ScaleType scaleType)
        {
            return Transform(chartX, scaleType) / DivX * XExtCanvas;
        }

        protected double ToCanvasX(double chartX)
        {
            ScaleType scaleType = ScaleType.Linear;
            if (HasScaleParameters && Definition.ScaleParameters.X != null)
                scaleType = Definition.ScaleParameters.X.ScaleType;
            return ToCanvasX(chartX, scaleType);
        }

        protected double ToCanvasX(double chartX, ScaleType scaleType)
        {
            double width = ToCanvasWidth(chartX, scaleType);
            if (isXAxisReversed)
                return XExtCanvas + XAxisCanvas + XAxisCanvas - (OffX + width);
            return OffX + width;
        }

        protected double FromCanvasWidth(double canvasWidth)
        {
            ScaleType scaleType = ScaleType.Linear;
            if (HasScaleParameters && Definition.ScaleParameters.X != null)
                scaleType = Definition.ScaleParameters.X.ScaleType;
            return FromCanvasWidth(canvasWidth, scaleType);
        }

        protected double FromCanvasWidth(double canvasWidth, ScaleType scaleType)
        {
            double rawChartWidth = canvasWidth * DivX / XExtCanvas;
            return InverseTransform(rawChartWidth, scaleType);
        }

        protected double InverseTransformX(double canvasX)
        {
            if (!HasScaleParameters || Definition.ScaleParameters.X == null)
                return canvasX;
            return InverseTransform(canvasX, Definition.ScaleParameters.X.ScaleType);
        }

        protected double ToCanvasHeight(double chartY)
        {
            ScaleType scaleType = ScaleType.Linear;
            if (HasScaleParameters && Definition.ScaleParameters.Y != null)
                scaleType = Definition.ScaleParameters.Y.ScaleType;
            return ToCanvasHeight(chartY, scaleType);
        }

        protected double ToCanvasHeight(double chartY, ScaleType scaleType)
        {
            double transformed = Transform(chartY, scaleType);
            return transformed / DivY * YExtCanvas;
        }

        protected double ToCanvasY(double chartY)
        {
            ScaleType scaleType = ScaleType.Linear;
            if (HasScaleParameters && Definition.ScaleParameters.Y != null)
                scaleType = Definition.ScaleParameters.Y.ScaleType;
            return ToCanvasY(chartY, scaleType);
        }

        protected double ToCanvasY(double chartY, ScaleType scaleType)
        {
            double height = ToCanvasHeight(chartY, scaleType);
            if (isYAxisReversed)
                return YExtCanvas + YAxisCanvas + YAxisCanvas - (OffY + height);
            return OffY + height;
        }

        protected double FromCanvasHeight(double canvasHeight)
        {
            ScaleType scaleType = ScaleType.Linear;
            if (HasScaleParameters && Definition.ScaleParameters.Y != null)
                scaleType = Definition.ScaleParameters.Y.ScaleType;
            return FromCanvasHeight(canvasHeight, scaleType);
        }

        protected double FromCanvasHeight(double canvasHeight, ScaleType scaleType)
        {
            double rawChartHeight = canvasHeight * DivY / YExtCanvas;
            return InverseTransform(rawChartHeight, scaleType);
        }

#if WARN_OBSOLETES
        [Obsolete("TODO: Get pyramid to use an x scale without tics and draw this in that way")]
#endif
        protected void DrawStringInCanvasCoordinates(string s, Font font, Brush brush, double x, double y, StringFormat txtFormat)
        {
            statsDirectCanvas.DrawString(s, font, brush, x, y, txtFormat);
        }

        ///  <summary>
        ///  Cases:
        ///  Left-justify: (x,y) is centre of left-hand edge of text.
        ///  Right-justify: (x,y) is centre of right-hand edge of text.
        ///  </summary>
#if WARN_OBSOLETES
        [Obsolete("Ideally subclasses would never need to use canvas co-ordinates")]
#endif
        protected void DrawStringAtAngleInCanvasCoordinates(string s, Font font, Brush brush, double x, double y, StringFormat txtFormat, LabelDirection direction)
        {
            statsDirectCanvas.DrawStringAtAngle(s, font, brush, x, y, txtFormat, direction);
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
#if WARN_OBSOLETES
        [Obsolete("Ideally subclasses would never need to use canvas co-ordinates")]
#endif
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
#if WARN_OBSOLETES
        [Obsolete("Ideally subclasses would never need to use canvas co-ordinates")]
#endif
        protected void DrawDiamondInCanvasCoordinates(Pen p, double x, double y, double size, bool fill)
        {
            statsDirectCanvas.DrawDiamond(p, x, y, size, fill);
        }

#if WARN_OBSOLETES
        [Obsolete("Ideally subclasses would never need to use canvas co-ordinates")]
#endif
        protected void DrawLineInCanvasCoordinates(Pen p, double x1, double y1, double x2, double y2)
        {
            statsDirectCanvas.DrawLine(p, x1, y1, x2, y2);
        }

#if WARN_OBSOLETES
        [Obsolete("Ideally subclasses would never need to use canvas co-ordinates")]
#endif
        protected void DrawMarkerInCanvasCoordinates(double x, double y, double size, MarkerShape shape, bool isFilled, Pen p)
        {
            statsDirectCanvas.DrawMarker(x, y, size, shape, isFilled, p);
        }

        protected void DrawMarkerInChartCoordinates(double x, double y, double size, MarkerShape shape, bool isFilled, Pen p)
        {
            statsDirectCanvas.DrawMarker(ToCanvasX(x), ToCanvasY(y), size, shape, isFilled, p);
        }

#if WARN_OBSOLETES
        [Obsolete("Ideally subclasses would never need to use canvas co-ordinates")]
#endif
        protected void FillRectangleInCanvasCoordinates(Brush b, double x, double y, double w, double h)
        {
            statsDirectCanvas.FillRectangle(b, x, y, w, h);
        }

#if WARN_OBSOLETES
        [Obsolete("Ideally subclasses would never need to use canvas co-ordinates")]
#endif
        protected void DrawRectangleInCanvasCoordinates(Pen p, double x, double y, double w, double h)
        {
            statsDirectCanvas.DrawRectangle(p, x, y, w, h);
        }

#if WARN_OBSOLETES
        [Obsolete("Ideally subclasses would never need to use canvas co-ordinates")]
#endif
        protected SizeF MeasureStringInCanvasCoordinates(string s, Font font)
        {
            return statsDirectCanvas.MeasureString(s, font);
        }

#if WARN_OBSOLETES
        [Obsolete("Ideally subclasses would never need to use canvas co-ordinates")]
#endif
        protected double LegendFontHeightInCanvasCoordinates()
        {
            return statsDirectCanvas.GetFontHeight(LegendFont);
        }

        private Pen GetSameOrDifferentPen(Color color)
        {
            if (mostRecentPen == null || !mostRecentPen.Color.Equals(color))
            {
                mostRecentPen?.Dispose();
                mostRecentPen = new Pen(color);
            }
            return mostRecentPen;
        }

        protected bool IsInsidePlotArea(AxisScales axisScales, double x, double y)
        {
            return y >= axisScales.Y.MinimumScaleValue && y <= axisScales.Y.MaximumScaleValue
                && x >= axisScales.X.MinimumScaleValue && x <= axisScales.X.MaximumScaleValue;
        }

        protected bool AreInsidePlotArea(AxisScales axisScales, double x1, double y1, double x2, double y2)
        {
            return IsInsidePlotArea(axisScales, x1, y1) && IsInsidePlotArea(axisScales, x2, y2);
        }

        protected void MaybeDrawLineInChartCoordinates(AxisScales axisScales, Color color, double x1, double y1, double x2, double y2)
        {
            if (AreInsidePlotArea(axisScales, x1, y1, x2, y2))
                DrawLineInChartCoordinates(GetSameOrDifferentPen(color), x1, y1, x2, y2);
        }

        protected void MaybeDrawLineInChartCoordinates(AxisScales axisScales, Pen p, double x1, double y1, double x2, double y2)
        {
            if (AreInsidePlotArea(axisScales, x1, y1, x2, y2))
                DrawLineInChartCoordinates(p, x1, y1, x2, y2);
        }

        protected void DrawLineInChartCoordinates(Color color, double x1, double y1, double x2, double y2)
        {
            DrawLineInChartCoordinates(GetSameOrDifferentPen(color), x1, y1, x2, y2);
        }

        protected void DrawLineInChartCoordinates(Pen p, double x1, double y1, double x2, double y2)
        {
            statsDirectCanvas.DrawLine(p, ToCanvasX(x1), ToCanvasY(y1), ToCanvasX(x2), ToCanvasY(y2));
        }

        protected void DrawRectangleInChartCoordinates(Color color, double left, double top, double width, double height)
        {
            statsDirectCanvas.DrawRectangle(GetSameOrDifferentPen(color), ToCanvasX(left), ToCanvasY(top), ToCanvasWidth(width), ToCanvasHeight(height));
        }

        protected bool HasScaleParameters => Definition != null && Definition.HasScaleParameters;

        protected bool HasChartOptions => Definition?.ChartOptions != null;

        protected bool ShouldUseColour
        {
            get
            {
                bool useColour = !ChartPreferences.DefaultAllBlack;
                if (HasChartOptions)
                    useColour = Definition.ChartOptions.UseColour;
                return useColour;
            }
        }

        public Pen GetMarkerPen(MarkerType mt)
        {
            return new Pen(ShouldUseColour ? mt.MarkerColor : GrBlack, mt.Width);
        }

        /// <summary>
        /// Return a new Pen of the given type. It is up to the caller to dispose of this.
        /// </summary>
        public Pen GetLinePen(MarkerType mt, bool ignoreStyle)
        {
            Pen p = new Pen(ShouldUseColour ? mt.LineColor : GrBlack, mt.Width);
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
        public static Color GrBlack => Color.Black;

        ///  <summary>
        ///  A colour to be used for drawing lines that are nominally green.
        ///  </summary>
        ///  <value></value>
        ///  <returns></returns>
        ///  <remarks></remarks>
        protected Color GrGreen => ShouldUseColour ? Color.Green : Color.Black;

        ///  <summary>
        ///  A colour to be used for drawing lines that are nominally magenta.
        ///  </summary>
        ///  <value></value>
        ///  <returns></returns>
        ///  <remarks></remarks>
        public Color GrMagenta => ShouldUseColour ? Color.Magenta : Color.Black;

        ///  <summary>
        ///  A colour to be used for drawing lines that are nominally red.
        ///  </summary>
        ///  <value></value>
        ///  <returns></returns>
        ///  <remarks></remarks>
        protected Color GrRed => ShouldUseColour ? Color.Red : Color.Black;

        ///  <summary>
        ///  A colour to be used for drawing axis lines
        ///  </summary>
        ///  <value></value>
        ///  <returns></returns>
        ///  <remarks></remarks>
        private Color GrAxis => ShouldUseColour ? Color.FromArgb(134, 134, 134) : Color.Black;

#if WARN_OBSOLETES
        [Obsolete("RTF should not be used in ChartRenderer")]
#endif
        public string GetAsciiRTF()
        {
            if (!IsAscii)
                throw new InvalidOperationException("Trying to get ASCII string for a non-ASCII chart");

            StringBuilder sb = new StringBuilder();
            for (int i = TextCanvas.GetUpperBound(0); i >= TextCanvas.GetLowerBound(0); i--)
            {
                sb.Append(TextCanvas[i]);
                sb.Append(Formatting.RTFCRLF);
            }
            return sb.ToString();
        }

        protected void WriteAsciiYX(int y, int x, string text)
        {
#if DEBUG
            if (y < 0 || y >= TextCanvas.Length)
                throw new Exception("y is out of the renderer's range");
#endif
            TextCanvas[y] = ReplaceAt(TextCanvas[y], x, text);
        }

        protected void WriteAsciiYX(int y, int x, char c)
        {
            TextCanvas[y] = ReplaceAt(TextCanvas[y], x, c);
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
            if (Definition.XSeries.Count > 0)
                AssignMarkersToSeries(Definition.XSeries);
            if (Definition.YSeries.Count > 0)
                AssignMarkersToSeries(Definition.YSeries);
        }

        protected void AssignMarkersToSeries(GenericOptions opts)
        {
            if (Definition.XSeries.Count > 0)
                AssignMarkersToSeries(Definition.XSeries, opts);
            if (Definition.YSeries.Count > 0)
                AssignMarkersToSeries(Definition.YSeries, opts);
        }

        protected void AssignMarkersToSeries(List<Series> s)
        {
            for (int i = 0; i < s.Count; i++)
            {
                DoubleSeries ds = (DoubleSeries)s[i];
                int mkr = ChartOptions.SeriesNumberToMarkerNumber(i);
                SetSeriesFromMarkerTypeAndOptions(ds, ChartPreferences.MarkerTypes[mkr], null);
            }
        }

        private static void SetSeriesFromMarkerTypeAndOptions(DoubleSeries ds, MarkerType mt, GenericOptions o)
        {
            ds.MarkerType = mt.Clone();
            if (o != null)
                ds.MarkerType.IsMarkerFilled = o.ShouldForceIsFilled ? o.ForcedIsFilled : mt.IsMarkerFilled;
        }

        protected void AssignMarkersToSeries(List<Series> s, GenericOptions opts)
        {
            if (opts?.MarkerTypes == null || opts.MarkerTypes.Count < 1)
            {
                for (int i = 0; i < s.Count; i++)
                {
                    DoubleSeries ds = (DoubleSeries)s[i];
                    int mkr = ChartOptions.SeriesNumberToMarkerNumber(i);
                    SetSeriesFromMarkerTypeAndOptions(ds, ChartPreferences.MarkerTypes[mkr], opts);
                }
            }
            else
            {
                for (int i = 0; i < s.Count; i++)
                {
                    if (s[i] is DoubleSeries ds)
                    {
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
            if (null != AxisPen)
            {
                AxisPen.Dispose();
                AxisPen = null;
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
            if (null != AxisLabelFont)
            {
                AxisLabelFont.Dispose();
                AxisLabelFont = null;
            }
            if (null != AxisTitleFont)
            {
                AxisTitleFont.Dispose();
                AxisTitleFont = null;
            }
            if (null != LabelFont)
            {
                LabelFont.Dispose();
                LabelFont = null;
            }
            if (null != LegendFont)
            {
                LegendFont.Dispose();
                LegendFont = null;
            }
            if (null != TitleFont)
            {
                TitleFont.Dispose();
                TitleFont = null;
            }
        }

        public double ImageWidth => statsDirectCanvas.Width;
        public double ImageHeight => statsDirectCanvas.Height;

        ///  <summary>
        ///  Initialise everything required for an ASCII plot of the required number of lines, notably including the SH_TX array.
        ///  </summary>
        ///  <param name="lines">The number of lines of text in the ASCII plot</param>
        protected void StartAsciiPlot(int lines)
        {
            // Set up the plot area - this is in ASCII co-ordinates, i.e. characters.
            int width = ASCII_XExt + ASCII_XTxt + 10;
            TextCanvas = new string[lines + 1];
            for (int c = 0; c <= TextCanvas.GetUpperBound(0); c++)
                TextCanvas[c] = new string(' ', width);
        }

        protected void EndAsciiPlot()
        {
            // Do nothing
        }

        protected void ASCII_PlotPointInChartCoordinates(double x, double y)
        {
            int chartX = ToAsciiX(x);
            int chartY = ToAsciiY(y);
            // Check if a point has already been plotted
            switch (TextCanvas[chartY][chartX])
            {
                case ' ':
                    WriteAsciiYX(chartY, chartX, "*");
                    break;
                case '*':
                    WriteAsciiYX(chartY, chartX, "2");
                    break;
                case '9':
                    WriteAsciiYX(chartY, chartX, "X");
                    break;
                case '|':
                case '+':
                case '-':
                    return;
                default:
                    // Must be numeric; add 1
                    WriteAsciiYX(chartY, chartX, (char)(TextCanvas[chartY][chartX] + 1));
                    break;
            }
        }

        /// <summary>
        /// Marker lines are single values on the X or Y axis that the user has requested to be drawn.
        /// </summary>
        protected void MaybeDrawMarkerLines(AxisScales axisScales)
        {
            if (Definition?.ScaleParameters == null)
                return;
            if (Definition.ScaleParameters.X.MarkerLineValue.HasValue)
            {
                double x = Definition.ScaleParameters.X.MarkerLineValue.Value;
                DrawLineInChartCoordinates(GrBlack, x, axisScales.Y.MinimumScaleValue, x, axisScales.Y.MaximumScaleValue);
            }
            if (Definition.ScaleParameters.Y.MarkerLineValue.HasValue)
            {
                double y = Definition.ScaleParameters.Y.MarkerLineValue.Value;
                DrawLineInChartCoordinates(GrBlack, axisScales.X.MinimumScaleValue, y, axisScales.X.MaximumScaleValue, y);
            }
        }

        private void SetFontsAndThicknessesFromOptions(GenericOptions o)
        {
            if (o.UsesAxisLabelFontDescriptor && !string.IsNullOrEmpty(o.AxisLabelFontDescriptor))
                AxisLabelFont = ChartPreferences.FontFromSaveString(o.AxisLabelFontDescriptor);
            if (o.UsesAxisTitleFontDescriptor && !string.IsNullOrEmpty(o.AxisTitleFontDescriptor))
                AxisTitleFont = ChartPreferences.FontFromSaveString(o.AxisTitleFontDescriptor);
            if (o.UsesLegendFontDescriptor && !string.IsNullOrEmpty(o.LegendFontDescriptor))
                LegendFont = ChartPreferences.FontFromSaveString(o.LegendFontDescriptor);
            if (o.UsesTitleFontDescriptor && !string.IsNullOrEmpty(o.TitleFontDescriptor))
                TitleFont = ChartPreferences.FontFromSaveString(o.TitleFontDescriptor);

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
                c = GrBlack;
                f = mt.MarkerFillStyle;
            }

            Brush b = null;
            switch (f)
            {
                case FillStyle.None:
                    //  Do nothing
                    break;
                case FillStyle.Crosshatch:
                    b = new HatchBrush(HatchStyle.DiagonalCross, c, Color.White);
                    break;
                case FillStyle.BackwardDiagonal:
                    b = new HatchBrush(HatchStyle.BackwardDiagonal, c, Color.White);
                    break;
                case FillStyle.ForwardDiagonal:
                    b = new HatchBrush(HatchStyle.ForwardDiagonal, c, Color.White);
                    break;
                case FillStyle.Solid:
                    b = new SolidBrush(c);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mt), f, "FillStyle Values between 0 and 4 accepted");
            }

            return b;
        }

        protected static string MakeTitle(string useIfAvailable, string defaultTitle)
        {
            if (string.IsNullOrWhiteSpace(useIfAvailable))
                return defaultTitle;

            if (useIfAvailable.Length > MAX_LABEL_LENGTH)
                return useIfAvailable.Substring(0, MAX_LABEL_LENGTH);
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

#if WARN_OBSOLETES
        [Obsolete("Ideally subclasses would never need to use canvas co-ordinates")]
#endif
        protected float AxisLabelWidthInCanvasCoordinates(string s)
        {
            return MeasureStringInCanvasCoordinates(s, AxisLabelFont).Width;
        }

#if WARN_OBSOLETES
        [Obsolete("Ideally subclasses would never need to use canvas co-ordinates")]
#endif
        protected float AxisLabelHeightInCanvasCoordinates(string s)
        {
            return MeasureStringInCanvasCoordinates(s, AxisLabelFont).Height;
        }

#if WARN_OBSOLETES
        [Obsolete("Ideally subclasses would never need to use canvas co-ordinates")]
#endif
        public float LabelWidthInCanvasCoordinates(string s)
        {
            return MeasureStringInCanvasCoordinates(s, LabelFont).Width;
        }

#if WARN_OBSOLETES
        [Obsolete("Ideally subclasses would never need to use canvas co-ordinates")]
#endif
        public float LabelHeightInCanvasCoordinates(string s)
        {
            return MeasureStringInCanvasCoordinates(s, LabelFont).Height;
        }

#if WARN_OBSOLETES
        [Obsolete("Ideally subclasses would never need to use canvas co-ordinates")]
#endif
        public float LegendWidthInCanvasCoordinates(string s)
        {
            return MeasureStringInCanvasCoordinates(s, LegendFont).Width;
        }

#if WARN_OBSOLETES
        [Obsolete("Ideally subclasses would never need to use canvas co-ordinates")]
#endif
        public float LegendHeightInCanvasCoordinates(string s)
        {
            return MeasureStringInCanvasCoordinates(s, LegendFont).Height;
        }

#if WARN_OBSOLETES
        [Obsolete("Ideally subclasses would never need to use canvas co-ordinates")]
#endif
        protected float TitleWidthInCanvasCoordinates(string s)
        {
            return MeasureStringInCanvasCoordinates(s, TitleFont).Width;
        }

        protected int ToAsciiX(double value)
        {
            return Convert.ToInt32(OffX + value / DivX * XExtCanvas);
        }

        protected int ToAsciiY(double value)
        {
            return Convert.ToInt32(OffY + value / DivY * YExtCanvas);
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
                    if (data[i] > 0 && data[i] < minGreaterThanZero)
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
        protected Range GetMinMaxArray(double[] data, ScaleType scaleType)
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
            return new Range(min, max);
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
        /// <param name="shape"></param>
        /// <param name="isFilled"></param>
        /// <param name="p"></param>
        /// <param name="useCalculatedScalesEvenWithDefinition"></param>
        /// <param name="presetXMin"></param>
        /// <param name="presetXMax"></param>
        /// <param name="presetYMin"></param>
        /// <param name="presetYMax"></param>
        /// <remarks></remarks>
        protected AxisScales PlotXYInternal(double[] x, double[] y, string xtxt, string ytxt, string title, bool zPlot, DataMinMax minMaxY, double markerSize, MarkerShape shape, bool isFilled, Pen p, bool useCalculatedScalesEvenWithDefinition, double presetXMin = 0, double presetXMax = 0, double presetYMin = 0, double presetYMax = 0)
        {
            ScaleType scaleTypeX = ScaleType.Linear;
            ScaleType scaleTypeY = ScaleType.Linear;
            if (HasScaleParameters)
            {
                scaleTypeX = Definition.ScaleParameters.X.ScaleType;
                scaleTypeY = Definition.ScaleParameters.Y.ScaleType;
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
                    axisYMax = Definition.ScaleParameters.Y.Max;
                    axisYMinGreaterThanZero = Definition.ScaleParameters.Y.MinGreaterThanZero;
                    axisYMin = Definition.ScaleParameters.Y.Min;
                    axisXMax = Definition.ScaleParameters.X.Max;
                    axisXMinGreaterThanZero = Definition.ScaleParameters.X.MinGreaterThanZero;
                    axisXMin = Definition.ScaleParameters.X.Min;
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
                    throw new ArgumentOutOfRangeException(nameof(minMaxY), minMaxY, "Don't know how to plot using the given minMaxY");
            }

            DataMinY = axisYMin;
            DataMinGreaterThanZeroY = axisYMinGreaterThanZero;
            DataMaxY = axisYMax;
            DataMinX = axisXMin;
            DataMinGreaterThanZeroX = axisXMinGreaterThanZero;
            DataMaxX = axisXMax;

            AxisScales axisScales = LayoutChartAndDrawAxes(title,
                new AxisDefinition(xtxt, AxisMode.Scale, scaleTypeX),
                new AxisDefinition(ytxt, AxisMode.Scale, scaleTypeY),
                false, useCalculatedScalesEvenWithDefinition);

            if (zPlot)
                DrawQCanvas(OffY);

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

            return axisScales;
        }

        ///  <summary>
        ///  Draw a horizontal line at the specified offset from the Y origin.
        ///  </summary>
        ///  <param name="y">The offset, in device units</param>
        ///  <remarks></remarks>
        protected void DrawQCanvas(double y)
        {
            using (Pen greenPen = new Pen(GrGreen, 2))
            {
                DrawLineInCanvasCoordinates(greenPen, XAxisCanvas, y, XAxisCanvas + XExtCanvas, y);
            }
        }

        /// <summary>
        /// Find an appropriate height for a chart with k series, between 1 and 5 times the nominal height.
        /// </summary>
        protected void ScaleHeight(int k)
        {
            if (k > 10)
            {
                double scaleYAxis = 1 + (k - 10) / 20.0;
                if (scaleYAxis > 5)
                    scaleYAxis = 5;
                imageHeight = (int)Math.Ceiling(scaleYAxis * DEFAULT_METAFILE_HEIGHT);
            }
        }

        protected void ScaleWidth(int k)
        {
            if (k > 10)
            {
                double scaleXAxis = 1 + (k - 10) / 20.0;
                if (scaleXAxis > 5)
                    scaleXAxis = 5;
                imageWidth = (int)Math.Ceiling(scaleXAxis * DEFAULT_METAFILE_WIDTH);
            }
        }

        protected void DrawLegend(Legend legend)
        {
            const int INTER_ROW_GAP = 6;
            const int MARKER_TO_LEGEND_GAP = 12;
            const int BORDER_WIDTH = 0;

            double left;
            double top;
            switch (legend.Position)
            {
                case LegendPosition.Bottom:
                    left = XAxisCanvas;
                    top = YAxisCanvas - LEGEND_TOP_GAP;
                    break;
                case LegendPosition.Left:
                    left = LEGEND_LEFT_GAP;
                    top = YAxisCanvas + YExtCanvas;
                    break;
                default:
                    throw new Exception("Only Left and Bottom legend positions known");
            }

            int i = 0;
            foreach (LegendEntry entry in legend.LegendEntries)
            {
                double legendFontHeight = LegendHeightInCanvasCoordinates("M");
                double rowHeight = Math.Max(LEGEND_MARKER_SIZE, legendFontHeight);
                double rowCentre = top - rowHeight * (i + 0.5) - INTER_ROW_GAP * i;
                DrawMarkerInCanvasCoordinates(left + BORDER_WIDTH + LEGEND_MARKER_SIZE / 2.0, rowCentre, LEGEND_MARKER_SIZE, entry.MarkerType);
                DrawStringLegendLC(entry.Label, left + BORDER_WIDTH + LEGEND_MARKER_SIZE + MARKER_TO_LEGEND_GAP, rowCentre);
                i++;
            }
        }

        protected bool IsAscii => null != Definition && Definition.IsAscii;

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