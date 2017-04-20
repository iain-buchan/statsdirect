using System;
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
        private Metafile metafile;
        private Graphics metafileGraphics;
        private Stream outputStream;
        private double width;
        private double height;

        public void Dispose()
        {
            if (null != metafileGraphics)
            {
                metafileGraphics.Dispose();
                metafileGraphics = null;
            }
            if (null != metafile)
            {
                metafile.Dispose();
                metafile = null;
            }
            if (null != outputStream)
            {
                outputStream.Dispose();
                outputStream = null;
            }
        }

        public EmfCanvas(double width, double height)
        {
            this.width = width;
            this.height = height;
            SetupGraphics();
        }

        private void SetupGraphics()
        {
            outputStream = new MemoryStream();
            //  Create temporary graphics object for metafile creation and get handle to its device context.
            using (Bitmap b = new Bitmap(1, 1, PixelFormat.Format32bppArgb))
            {
                using (Graphics newGraphics = Graphics.FromImage(b))
                {
                    //  Create metafile object to do the recording.
                    IntPtr hdc = newGraphics.GetHdc();
                    metafile = new Metafile(outputStream, hdc, new RectangleF(0, 0, (float)width, (float)height), MetafileFrameUnit.Pixel, EmfType.EmfPlusDual);
                    newGraphics.ReleaseHdc(hdc);

                    MetafileHeader header = metafile.GetMetafileHeader();
                    metafileGraphics = Graphics.FromImage(metafile);
                    metafileGraphics.SmoothingMode = SmoothingMode.AntiAlias;
                }
            }
        }

        public void DrawString(string s, Font font, Brush brush, double x, double y, StringFormat txtFormat)
        {
            metafileGraphics.DrawString(s, font, brush, Convert.ToSingle(x), Convert.ToSingle(height - y), txtFormat);
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
        public SizeF DrawStringAtAngle(string s, Font font, Brush brush, double x, double y, StringFormat txtFormat, LabelDirection direction)
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
            float angle = DirectionToAngle(direction);
            metafileGraphics.TranslateTransform(Convert.ToSingle(x), Convert.ToSingle(height - y));
            metafileGraphics.RotateTransform(angle);
            metafileGraphics.DrawString(s, font, brush, 0, 0, txtFormat);
            // Undo the transform
            metafileGraphics.RotateTransform(0f - angle);
            metafileGraphics.TranslateTransform(0f - Convert.ToSingle(x), 0f - Convert.ToSingle(height - y));

            SizeF uprightSize = metafileGraphics.MeasureString(s, font);
            SizeF boundingSize = ToBoundingSize(uprightSize, direction);
            return boundingSize;
        }

        private static float DirectionToAngle(LabelDirection direction)
        {
            switch (direction)
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

        public static SizeF ToBoundingSize(SizeF uprightSize, LabelDirection direction)
        {
            switch (direction)
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

        public SizeF MeasureString(string s, Font font)
        {
            return metafileGraphics.MeasureString(s, font);
        }

        public Stream DetachAndReturnImageStream()
        {
            if (null != metafileGraphics)
            {
                metafileGraphics.Dispose();
                metafileGraphics = null;
            }
            if (null != metafile)
            {
                metafile.Dispose();
                metafile = null;
            }
            if (null == outputStream)
                return null;
            Stream temp = outputStream;
            outputStream = null;
            temp.Position = 0;
            return temp;
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
        public void DrawSquare(Pen p, double x, double y, double size, bool fill)
        {
            double size2 = size / 2;
            PointF[] pt = new PointF[5];
            pt[0].X = Convert.ToSingle(x - size2);
            pt[0].Y = Convert.ToSingle(height - (y - size2));
            pt[1].X = Convert.ToSingle(x - size2);
            pt[1].Y = Convert.ToSingle(height - (y + size2));
            pt[2].X = Convert.ToSingle(x + size2);
            pt[2].Y = Convert.ToSingle(height - (y + size2));
            pt[3].X = Convert.ToSingle(x + size2);
            pt[3].Y = Convert.ToSingle(height - (y - size2));
            pt[4].X = Convert.ToSingle(x - size2);
            pt[4].Y = Convert.ToSingle(height - (y - size2));
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
        public void DrawDiamond(Pen p, double x, double y, double size, bool fill)
        {
            double size2 = size / 2;
            PointF[] pt = new PointF[5];
            pt[0].X = Convert.ToSingle(x - size2);
            pt[0].Y = Convert.ToSingle(height - y);
            pt[1].X = Convert.ToSingle(x);
            pt[1].Y = Convert.ToSingle(height - (y - size2));
            pt[2].X = Convert.ToSingle(x + size2);
            pt[2].Y = Convert.ToSingle(height - y);
            pt[3].X = Convert.ToSingle(x);
            pt[3].Y = Convert.ToSingle(height - (y + size2));
            pt[4].X = Convert.ToSingle(x - size2);
            pt[4].Y = Convert.ToSingle(height - y);
            DrawAndOrFillPolygon(p, fill, pt);
        }

        private void DrawAndOrFillPolygon(Pen p, bool fill, PointF[] pt)
        {
            if (fill)
            {
                using (Brush b = new SolidBrush(p.Color))
                {
                    metafileGraphics.FillPolygon(b, pt);
                }
            }
            // Draw the diamond
            metafileGraphics.DrawPolygon(p, pt);
        }

        public void DrawMarker(double x, double y, double size, MarkerShape shape, bool isFilled, Pen p)
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
                    PointF[] points = { new PointF(Convert.ToSingle(x - size), Convert.ToSingle(height - (y - size))), new PointF(Convert.ToSingle(x), Convert.ToSingle(height - (y + size))), new PointF(Convert.ToSingle(x + size), Convert.ToSingle(height - (y - size))) };
                    if (isFilled)
                    {
                        using (Brush b = new SolidBrush(p.Color))
                        {
                            metafileGraphics.FillPolygon(b, points);
                        }
                    }
                    else
                    {
                        metafileGraphics.DrawPolygon(p, points);
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
                    throw new ArgumentException("Don't know how to draw style's shape", nameof(shape));
            }
        }

        private void FillEllipse(Brush b, double x, double y, double w, double h)
        {
            metafileGraphics.FillEllipse(b, Convert.ToInt32(Convert.ToSingle(x)), Convert.ToInt32(Convert.ToSingle(height - y)), Convert.ToInt32(Convert.ToSingle(w)), Convert.ToInt32(Convert.ToSingle(h)));
        }

        private void DrawEllipse(Pen p, double x, double y, double w, double h)
        {
            metafileGraphics.DrawEllipse(p, Convert.ToSingle(x), Convert.ToSingle(height - y), Convert.ToSingle(w), Convert.ToSingle(h));
        }

        public void FillRectangle(Brush b, double x, double y, double w, double h)
        {
            metafileGraphics.FillRectangle(b, Convert.ToSingle(x), Convert.ToSingle(height - y), Convert.ToSingle(w), Convert.ToSingle(h));
        }

        public void DrawRectangle(Pen p, double x, double y, double w, double h)
        {
            metafileGraphics.DrawRectangle(p, Convert.ToSingle(x), Convert.ToSingle(height - y), Convert.ToSingle(w), Convert.ToSingle(h));
        }

        public void DrawLine(Pen p, double x1, double y1, double x2, double y2)
        {
            metafileGraphics.DrawLine(p, Convert.ToSingle(Math.Round(x1, 0)), Convert.ToSingle(Math.Round(height - y1, 0)), Convert.ToSingle(Math.Round(x2, 0)), Convert.ToSingle(Math.Round(height - y2, 0)));
        }

        public double GetFontHeight(Font f)
        {
            return f.GetHeight(metafileGraphics);
        }
    }
}
