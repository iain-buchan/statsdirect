using System;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using StatsDirect.Templates;

namespace StatsDirect.Charting
{
    /// <summary>
    /// A chart drawing surface that is backed with an Enhanced Metafile.
    /// </summary>
    class EmfCanvas : IStatsDirectCanvas
    {
        private readonly Metafile metafile;
        private readonly Graphics metafileGraphics;
        private readonly Stream outputStream;

        public double Width { get; }

        public double Height { get; }

        public EmfCanvas(double width, double height)
        {
            Width = width;
            Height = height;

            outputStream = new MemoryStream();
            //  Create temporary graphics object for metafile creation and get handle to its device context.
            using Bitmap b = new(1, 1, PixelFormat.Format32bppArgb);
            using Graphics newGraphics = Graphics.FromImage(b);
            //  Create metafile object to do the recording.
            IntPtr hdc = newGraphics.GetHdc();
            metafile = new Metafile(outputStream, hdc, new RectangleF(0, 0, (float)Width, (float)Height), MetafileFrameUnit.Pixel, EmfType.EmfPlusDual);
            newGraphics.ReleaseHdc(hdc);

            metafileGraphics = Graphics.FromImage(metafile);
            metafileGraphics.SmoothingMode = SmoothingMode.AntiAlias;
        }

        public void DrawString(string? s, FontDescriptor font, BrushDescriptor? b, double x, double y, StringFormat txtFormat)
        {
            if (TryGetBrush(b, out Brush? brush) && FontCache.TryGetFont(font, out Font? f))
                metafileGraphics.DrawString(s, f, brush, Convert.ToSingle(x), Convert.ToSingle(Height - y), txtFormat);
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
        public void DrawStringAtAngle(string? s, FontDescriptor font, BrushDescriptor? b, double x, double y, StringFormat txtFormat, LabelDirection direction)
        {
            if (string.IsNullOrWhiteSpace(s))
                return;

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
            if (b is not null)
            {
                if (TryGetBrush(b, out Brush? brush) && FontCache.TryGetFont(font, out Font? f))
                {
                    float angle = DirectionToAngle(direction);
                    metafileGraphics.TranslateTransform(Convert.ToSingle(x), Convert.ToSingle(Height - y));
                    metafileGraphics.RotateTransform(angle);
                    metafileGraphics.DrawString(s, f, brush, 0, 0, txtFormat);
                    // Undo the transform
                    metafileGraphics.RotateTransform(0f - angle);
                    metafileGraphics.TranslateTransform(0f - Convert.ToSingle(x), 0f - Convert.ToSingle(Height - y));
                }
            }
        }

        public SizeD MeasureStringAtAngle(string s, FontDescriptor font, LabelDirection direction)
        {
            if (!FontCache.TryGetFont(font, out Font? f))
                return SizeD.Empty;
            SizeF uprightSize = metafileGraphics.MeasureString(s, f);
            return ToBoundingSize(uprightSize, direction);
        }

        private static float DirectionToAngle(LabelDirection direction) =>
            direction switch
            {
                LabelDirection.Across => 0.0F,
                LabelDirection.Up => -90.0F,
                LabelDirection.Down => 90.0F,
                LabelDirection.SlopeUp => -45.0F,
                LabelDirection.SlopeDown => 45.0F,
                _ => 0,
            };

        public static SizeD ToBoundingSize(SizeF uprightSize, LabelDirection direction)
        {
            switch (direction)
            {
                case LabelDirection.Across:
                    return new SizeD(uprightSize.Width, uprightSize.Height);
                case LabelDirection.Up:
                case LabelDirection.Down:
                    return new SizeD(uprightSize.Height, uprightSize.Width);
                case LabelDirection.SlopeDown:
                case LabelDirection.SlopeUp:
                    float diagonal = Convert.ToSingle((uprightSize.Width + uprightSize.Height) * Math.Sin(Math.PI / 4.0));
                    return new SizeD(diagonal, diagonal);
            }

            return SizeD.Empty;
        }

        public SizeD MeasureString(string s, FontDescriptor font)
        {
            if (!FontCache.TryGetFont(font, out Font? f))
                return SizeD.Empty;
            SizeF sizeF = metafileGraphics.MeasureString(s, f);
            return new SizeD(sizeF.Width, sizeF.Height);
        }

        public Stream DetachAndReturnImageStream()
        {
            metafileGraphics.Dispose();
            metafile.Dispose();
            outputStream.Position = 0;
            return outputStream;
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
            PointF[] pt = new PointF[5];
            pt[0].X = Convert.ToSingle(x - size2);
            pt[0].Y = Convert.ToSingle(Height - (y - size2));
            pt[1].X = Convert.ToSingle(x - size2);
            pt[1].Y = Convert.ToSingle(Height - (y + size2));
            pt[2].X = Convert.ToSingle(x + size2);
            pt[2].Y = Convert.ToSingle(Height - (y + size2));
            pt[3].X = Convert.ToSingle(x + size2);
            pt[3].Y = Convert.ToSingle(Height - (y - size2));
            pt[4].X = Convert.ToSingle(x - size2);
            pt[4].Y = Convert.ToSingle(Height - (y - size2));
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
            PointF[] pt = new PointF[5];
            pt[0].X = Convert.ToSingle(x - size2);
            pt[0].Y = Convert.ToSingle(Height - y);
            pt[1].X = Convert.ToSingle(x);
            pt[1].Y = Convert.ToSingle(Height - (y - size2));
            pt[2].X = Convert.ToSingle(x + size2);
            pt[2].Y = Convert.ToSingle(Height - y);
            pt[3].X = Convert.ToSingle(x);
            pt[3].Y = Convert.ToSingle(Height - (y + size2));
            pt[4].X = Convert.ToSingle(x - size2);
            pt[4].Y = Convert.ToSingle(Height - y);
            DrawAndOrFillPolygon(p, fill, pt);
        }

        private void DrawAndOrFillPolygon(PenDescriptor p, bool fill, PointF[] pt)
        {
            if (fill)
            {
                using Brush b = new SolidBrush(ToColor(p.Color));
                metafileGraphics.FillPolygon(b, pt);
            }
            // Draw the diamond
            metafileGraphics.DrawPolygon(GetPen(p), pt);
        }

        public void DrawMarker(double x, double y, double size, MarkerShape shape, bool isFilled, PenDescriptor p)
        {
            double size2 = size * 2;
            BrushDescriptor? b = isFilled ? new BrushDescriptor(p.Color) : null;
            PenDescriptor pd = isFilled ? PenDescriptor.White : p;

            switch (shape)
            {
                case MarkerShape.Circle:
                    if (isFilled)
                        FillEllipse(new BrushDescriptor(p.Color), x - size, y + size, size2, size2);
                    else
                        DrawEllipse(p, x - size, y + size, size2, size2);
                    break;
                case MarkerShape.Square:
                    DrawRectangle(p, b, x - size, y + size, size2, size2);
                    break;
                case MarkerShape.Triangle:
                    {
                        PointF[] points =
                        {
                            new PointF(Convert.ToSingle(x - size), Convert.ToSingle(Height - (y - size))),
                            new PointF(Convert.ToSingle(x), Convert.ToSingle(Height - (y + size))),
                            new PointF(Convert.ToSingle(x + size), Convert.ToSingle(Height - (y - size)))
                        };
                        if (b is not null)
                        {
                            if (TryGetBrush(b, out Brush? brush))
                                metafileGraphics.FillPolygon(brush, points);
                        }
                        else
                            metafileGraphics.DrawPolygon(GetPen(p), points);
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
                    if (isFilled)
                        FillEllipse(new BrushDescriptor(p.Color), x - size, y + size, size2, size2);
                    else
                        DrawEllipse(p, x - size, y + size, size2, size2);
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

        private void FillEllipse(BrushDescriptor b, double x, double y, double w, double h)
        {
            if (TryGetBrush(b, out Brush? brush))
                metafileGraphics.FillEllipse(brush, Convert.ToInt32(Convert.ToSingle(x)), Convert.ToInt32(Convert.ToSingle(Height - y)), Convert.ToInt32(Convert.ToSingle(w)), Convert.ToInt32(Convert.ToSingle(h)));
        }

        private void DrawEllipse(PenDescriptor p, double x, double y, double w, double h)
        {
            metafileGraphics.DrawEllipse(GetPen(p), Convert.ToSingle(x), Convert.ToSingle(Height - y), Convert.ToSingle(w), Convert.ToSingle(h));
        }

        public void DrawRectangle(PenDescriptor? p, BrushDescriptor? b, double x, double y, double w, double h)
        {
            if (TryGetBrush(b, out Brush? brush))
                metafileGraphics.FillRectangle(brush, Convert.ToSingle(x), Convert.ToSingle(Height - y), Convert.ToSingle(w), Convert.ToSingle(h));
            if (p is not null)
                metafileGraphics.DrawRectangle(GetPen(p), Convert.ToSingle(x), Convert.ToSingle(Height - y), Convert.ToSingle(w), Convert.ToSingle(h));
        }

        private static Pen GetPen(PenDescriptor p)
        {
            // TODO: Cache
            return new Pen(ToColor(p.Color), (float)p.LineThickness) { DashStyle = ToDashStyle(p.DashStyle) };
        }

        private static DashStyle ToDashStyle(DashStyleDescriptor dashStyle) =>
            dashStyle switch
            {
                DashStyleDescriptor.Solid => DashStyle.Solid,
                DashStyleDescriptor.Dash => DashStyle.Dash,
                DashStyleDescriptor.Dot => DashStyle.Dot,
                DashStyleDescriptor.DashDot => DashStyle.DashDot,
                DashStyleDescriptor.DashDotDot => DashStyle.DashDotDot,
                _ => DashStyle.Custom,
            };

        public void DrawLine(PenDescriptor p, double x1, double y1, double x2, double y2)
        {
            metafileGraphics.DrawLine(GetPen(p), Convert.ToSingle(Math.Round(x1, 0)), Convert.ToSingle(Math.Round(Height - y1, 0)), Convert.ToSingle(Math.Round(x2, 0)), Convert.ToSingle(Math.Round(Height - y2, 0)));
        }

        #region IDisposable Support
        private bool disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    metafile?.Dispose();
                    metafileGraphics?.Dispose();
                    outputStream?.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
        #endregion

        private bool TryGetBrush(BrushDescriptor? b, [NotNullWhen(true)] out Brush? brush)
        {
            if (b is null)
            {
                brush = null;
                return false;
            }
            // TODO: Cache
            brush = ToBrush(b);
            return brush is not null;
        }

        /// <summary>
        /// Return a Brush if there is a fill style, or null if there is no need for one as the fill style is None.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">if the FillStyle is unknown</exception>
        private static Brush? ToBrush(BrushDescriptor b) =>
            b.FillStyle switch
            {
                FillStyle.None => null,
                FillStyle.Crosshatch => new HatchBrush(HatchStyle.DiagonalCross, ToColor(b.Color), Color.White),
                FillStyle.BackwardDiagonal => new HatchBrush(HatchStyle.BackwardDiagonal, ToColor(b.Color), Color.White),
                FillStyle.ForwardDiagonal => new HatchBrush(HatchStyle.ForwardDiagonal, ToColor(b.Color), Color.White),
                FillStyle.Solid => new SolidBrush(ToColor(b.Color)),
                _ => throw new ArgumentOutOfRangeException(nameof(b), b.FillStyle, "FillStyle Values between 0 and 4 accepted"),
            };

        private static Color ToColor(ColorDescriptor color) => Color.FromArgb(color.R, color.G, color.B);
    }
}
