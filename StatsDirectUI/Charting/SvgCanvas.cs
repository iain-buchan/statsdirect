using StatsDirect.Templates;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace StatsDirect.Charting
{
    /// <summary>
    /// A chart drawing surface that is backed with a Scalable Vector Graphics file.
    /// </summary>
    partial class SvgCanvas : IStatsDirectCanvas
    {
        // Assume 72 points per inch (common in computing, inaccurate in print), 96 pixels per inch
        const float PIXELS_PER_INCH = 96.0f;
        const float POINTS_PER_INCH = 72.0f;
        const float PIXELS_PER_POINT = PIXELS_PER_INCH / POINTS_PER_INCH;

        const float SMALLEST_PIXEL_SIZE = 5;
        const float LARGEST_PIXEL_SIZE = 100;

        public double Width { get; }
        public double Height { get; }

        private XNamespace SvgNamespace { get; }
        private XNamespace XlinkNamespace { get; }
        private XElement root;

        public SvgCanvas(double width, double height)
        {
            Width = width;
            Height = height;
            SvgNamespace = "http://www.w3.org/2000/svg";
            XlinkNamespace = "http://www.w3.org/1999/xlink";
            root = new XElement(SvgNamespace + "svg",
                // new XAttribute("xmlns", SvgNamespace.NamespaceName),
                new XAttribute("width", "6in"),
                new XAttribute("viewBox", $"0 0 {width} {height}")
                );
        }

        public void DrawString(string s, FontDescriptor font, BrushDescriptor brush, double x, double y, StringFormat txtFormat)
        {
            root.Add(new XElement(SvgNamespace + "text",
                new XAttribute("x", x),
                new XAttribute("y", Height - y),
                new XElement(SvgNamespace + "tspan",
                    s,
                    new XAttribute("dy", ToDy(txtFormat)),
                    new XAttribute("style", $"{ToCss(font)}{ToCss(brush)}{ToCss(txtFormat)}")
                )
            ));
        }

        private string ToDy(StringFormat txtFormat)
        {
            switch (txtFormat.LineAlignment)
            {
                case StringAlignment.Near:
                    return "0.8em";
                case StringAlignment.Center:
                    return "0.4em";
                case StringAlignment.Far:
                    return "0";
                default:
                    throw new ArgumentOutOfRangeException(nameof(txtFormat), txtFormat.LineAlignment, "Unknown line alignment");
            }
        }

        private object ToCss(FontDescriptor font)
        {
            return $"font-family:{font.FontFamily};font-size:{font.SizeInPoints * PIXELS_PER_POINT}px;";
        }

        private string ToCss(StringFormat txtFormat)
        {
            string textAnchor;
            switch (txtFormat.Alignment)
            {
                case StringAlignment.Near:
                    textAnchor = "start";
                    break;
                case StringAlignment.Center:
                    textAnchor = "middle";
                    break;
                case StringAlignment.Far:
                    textAnchor = "end";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(txtFormat), txtFormat.Alignment, "Unknown alignment");
            }
            return $"text-anchor:{textAnchor};";
        }

        ///  <summary>
        ///  Cases:
        ///  Left-justify: (x,y) is centre of left-hand edge of text.
        ///  Right-justify: (x,y) is centre of right-hand edge of text.
        ///  </summary>
        ///  <param name="s"></param>
        ///  <param name="font"></param>
        ///  <param name="brush"></param>
        ///  <param name="x"></param>
        ///  <param name="y"></param>
        ///  <param name="txtFormat"></param>
        ///  <param name="direction"></param>
        ///  <returns>The bounding size of s drawn in direction with txtFormat</returns>
        /// <remarks></remarks>
        public void DrawStringAtAngle(string s, FontDescriptor font, BrushDescriptor brush, double x, double y, StringFormat txtFormat, LabelDirection direction)
        {
            //  Work out how to fiddle the text alignment
            if (txtFormat.LineAlignment == StringAlignment.Center && txtFormat.Alignment == StringAlignment.Far)
            {
                //  Middle-right: Vertical text needs fiddling, otherwise we're OK.
                if (direction == LabelDirection.Up)
                {
                    txtFormat = (StringFormat)txtFormat.Clone();
                    txtFormat.LineAlignment = StringAlignment.Far;
                    txtFormat.Alignment = StringAlignment.Center;
                }
                else if (direction == LabelDirection.Down)
                {
                    txtFormat = (StringFormat)txtFormat.Clone();
                    txtFormat.LineAlignment = StringAlignment.Near;
                    txtFormat.Alignment = StringAlignment.Center;
                }
            }
            else if (txtFormat.LineAlignment == StringAlignment.Near && txtFormat.Alignment == StringAlignment.Center)
            {
                //  Top-centre: Anything other than across needs fiddling.
                if (direction == LabelDirection.Down || direction == LabelDirection.SlopeDown)
                {
                    txtFormat = (StringFormat)txtFormat.Clone();
                    txtFormat.LineAlignment = StringAlignment.Center;
                    txtFormat.Alignment = StringAlignment.Near;
                }
                else if (direction == LabelDirection.SlopeUp || direction == LabelDirection.Up)
                {
                    txtFormat = (StringFormat)txtFormat.Clone();
                    txtFormat.LineAlignment = StringAlignment.Center;
                    txtFormat.Alignment = StringAlignment.Far;
                }
            }
            double angle = DirectionToAngle(direction);
            root.Add(new XElement(SvgNamespace + "g",
                new XAttribute("transform", $"translate({x},{Height - y})"),
                new XElement(SvgNamespace + "text",
                    new XAttribute("x", 0),
                    new XAttribute("y", 0),
                    new XAttribute("transform", $"rotate({angle})"),
                    new XElement(SvgNamespace + "tspan",
                        s,
                        new XAttribute("dy", ToDy(txtFormat)),
                        new XAttribute("style", $"{ToCss(font)}{ToCss(brush)}{ToCss(txtFormat)}")
                    )
                )
            ));
        }

        public SizeD MeasureStringAtAngle(string s, FontDescriptor font, LabelDirection direction)
        {
            return ToBoundingSize(MeasureString(s, font), direction);
        }

        private static double DirectionToAngle(LabelDirection direction)
        {
            switch (direction)
            {
                case LabelDirection.Across:
                    return 0;
                case LabelDirection.Up:
                    return -90;
                case LabelDirection.Down:
                    return 90;
                case LabelDirection.SlopeUp:
                    return -45;
                case LabelDirection.SlopeDown:
                    return 45;
            }

            return 0;
        }

        public static SizeD ToBoundingSize(SizeD uprightSize, LabelDirection direction)
        {
            switch (direction)
            {
                case LabelDirection.Across:
                    return uprightSize;
                case LabelDirection.Up:
                case LabelDirection.Down:
                    return new SizeD(uprightSize.Height, uprightSize.Width);
                case LabelDirection.SlopeDown:
                case LabelDirection.SlopeUp:
                    double diagonal = (uprightSize.Width + uprightSize.Height) * Math.Sin(Math.PI / 4.0);
                    return new SizeD(diagonal, diagonal);
            }

            return SizeD.Empty;
        }

        public SizeD MeasureString(string s, FontDescriptor font)
        {
            Size sz = MeasureText(s, font);
            return new SizeD(sz.Width, sz.Height);
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
        public void DrawSquare(PenDescriptor p, double x, double y, double size, bool fill)
        {
            double size2 = size / 2;
            PointD[] pt = new PointD[] {
                new PointD(x - size2, Height - (y - size2)),
                new PointD(x - size2, Height - (y + size2)),
                new PointD(x + size2, Height - (y + size2)),
                new PointD(x + size2, Height - (y - size2))
            };
            DrawAndOrFillPolygon(p, fill, pt);
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
        public void DrawDiamond(PenDescriptor p, double x, double y, double size, bool fill)
        {
            double size2 = size / 2;
            PointD[] pt = new PointD[]
            {
                new PointD(x - size2, Height - y),
                new PointD(x, Height - (y - size2)),
                new PointD(x + size2, Height - y),
                new PointD(x, Height - (y + size2))
            };
            DrawAndOrFillPolygon(p, fill, pt);
        }

        private void DrawAndOrFillPolygon(PenDescriptor p, bool fill, IList<PointD> pt)
        {
            root.Add(new XElement(SvgNamespace + "polygon",
                new XAttribute("points", ToPointsString(pt)),
                new XAttribute("style", $"{ToCss(p)}{ToCssFill(fill)}")
                ));
        }

        private string ToPointsString(IList<PointD> points)
        {
            return string.Join(" ", points.Select(p => $"{p.X},{p.Y}").ToArray());
        }

        public void DrawMarker(double x, double y, double size, MarkerShape shape, bool isFilled, PenDescriptor p)
        {
            double size2 = size * 2;
            BrushDescriptor b = isFilled ? new BrushDescriptor(p.Color) : null;
            PenDescriptor pd = isFilled ? PenDescriptor.White : p;

            switch (shape)
            {
                case MarkerShape.Circle:
                    DrawEllipse(p, b, x, y, size2, size2);
                    break;
                case MarkerShape.Square:
                    DrawRectangle(p, b, x - size, y + size, size2, size2);
                    break;
                case MarkerShape.Triangle:
                    DrawAndOrFillPolygon(p, isFilled,
                        new PointD[] {
                        new PointD(x - size, Height - (y - size)),
                        new PointD(x, Height - (y + size)),
                        new PointD(x + size, Height - (y - size))
                        });
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
                    DrawEllipse(p, b, x, y, size2, size2);
                    DrawLine(pd, x, y - size, x, y + size);
                    break;
                case MarkerShape.SquareLine:
                    DrawRectangle(p, b, x - size, y + size, size2, size2);
                    DrawLine(pd, x - size, y + size, x + size, y - size);
                    break;
                case MarkerShape.SquareCross:
                    DrawRectangle(p, b, x - size, y + size, size2, size2);
                    DrawLine(pd, x - size, y - size, x + size, y + size);
                    DrawLine(pd, x - size, y + size, x + size, y - size);
                    break;
                case MarkerShape.Diamond:
                    DrawDiamond(p, x, y, size2, isFilled);
                    break;
                case MarkerShape.SurvivalTic:
                    DrawLine(p, x - size, y - size, x + size, y - size);
                    DrawLine(p, x + size, y - size, x + size, y + size);
                    break;
                default:
                    throw new ArgumentException("Don't know how to draw style's shape", nameof(shape));
            }
        }

        private void DrawEllipse(PenDescriptor p, BrushDescriptor b, double x, double y, double w, double h)
        {
            root.Add(new XElement(SvgNamespace + "ellipse",
                new XAttribute("cx", x),
                new XAttribute("cy", Height - y),
                new XAttribute("rx", w / 2),
                new XAttribute("ry", h / 2),
                new XAttribute("style", ToCss(p, b))
                ));
        }

        public void DrawRectangle(PenDescriptor p, BrushDescriptor b, double x, double y, double w, double h)
        {
            root.Add(new XElement(SvgNamespace + "rect",
                new XAttribute("x", x),
                new XAttribute("y", Height - y),
                new XAttribute("width", w),
                new XAttribute("height", h),
                new XAttribute("style", $"{ToCss(p, b)}")
                ));
        }

        public void DrawLine(PenDescriptor p, double x1, double y1, double x2, double y2)
        {
            root.Add(new XElement(SvgNamespace + "line",
                new XAttribute("x1", x1),
                new XAttribute("y1", Height - y1),
                new XAttribute("x2", x2),
                new XAttribute("y2", Height - y2),
                new XAttribute("style", $"{ToCss(p)}")
                ));
        }

        private static string ToCss(PenDescriptor p)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("stroke:{0};", ToCss(p.Color));
            if (!(p.CapStyle == CapStyle.Butt))
                sb.AppendFormat("stroke-linecap:{0};", ToCss(p.CapStyle));
            return sb.ToString();
        }

        private static object ToCss(CapStyle capStyle)
        {
            switch (capStyle)
            {
                case CapStyle.Butt:
                    return "butt";
                case CapStyle.Round:
                    return "round";
                case CapStyle.Square:
                    return "square";
                default:
                    throw new ArgumentOutOfRangeException(nameof(capStyle), capStyle, "Only Butt, Round, Square known.");
            }
        }

        private static string ToCss(BrushDescriptor b)
        {
            return $"fill:{ToCss(b.Color)};";
        }

        private static string ToCss(PenDescriptor p, BrushDescriptor b)
        {
            if (null == b)
                return ToCss(p) + "fill:none;";
            return ToCss(p) + $"fill:{ToCss(b.Color)};";
        }

        private object ToCssFill(bool fill)
        {
            return fill ? string.Empty : "fill:none;";
        }

        private static string ToCss(ColorDescriptor c)
        {
            return "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");
        }

        public double GetFontHeight(FontDescriptor f)
        {
            return f.SizeInPoints * PIXELS_PER_POINT;
        }

        #region IDisposable Support
        private bool disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        public Stream DetachAndReturnImageStream()
        {
            MemoryStream ms = new MemoryStream();
            StreamWriter sw = new StreamWriter(ms, Encoding.UTF8);
            sw.Write(root.ToString());
            sw.Flush();
            ms.Position = 0;
            return ms;
        }
        #endregion

        private static Size MeasureText(string s, FontDescriptor f)
        {
            // HACK: Truly horrible layer-smashing, but the only way to get a reasonable size for a string.
            return System.Windows.Forms.TextRenderer.MeasureText(s, FontCache.Font(f), default(Size), System.Windows.Forms.TextFormatFlags.NoPadding);
        }
    }
}
