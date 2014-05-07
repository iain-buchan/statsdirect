using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using StatsDirect.Templates;

namespace StatsDirect.Charting
{
    /// <summary>
    /// A chart drawing surface that is backed with an Enhanced Metafile.
    /// </summary>
    class EmfCanvas : IDisposable
    {
        private Metafile metaFile;
        private Graphics canvas;
        private Stream cachedOutputStream;

        public void Dispose()
        {
            if (null != canvas)
            {
                canvas.Dispose();
                canvas = null;
            }
            if ((metaFile != null))
            {
                metaFile.Dispose();
                metaFile = null;
            }
            cachedOutputStream = null;
        }

        public EmfCanvas(double width, double height)
        {
            cachedOutputStream = new MemoryStream();
            SetupGraphics((float)width, (float)height);
        }

        private void SetupGraphics(float width, float height)
        {
            if (cachedOutputStream != null)
            {
                //  Create temporary graphics object for metafile creation and get handle to its device context.
                using (Bitmap b = new Bitmap(1, 1, PixelFormat.Format32bppArgb))
                {
                    b.SetResolution(96.0f, 96.0f);
                    using (Graphics newGraphics = Graphics.FromImage(b))
                    {
                        IntPtr hdc = newGraphics.GetHdc();
                        cachedOutputStream.Position = 0; //  Just in case we're resetting an earlier metafile output
                        //  Create metafile object to do the recording.
                        metaFile = new Metafile(cachedOutputStream, hdc, new RectangleF(0, 0, width, height), MetafileFrameUnit.Pixel, EmfType.EmfPlusDual);
                        //  Create graphics object as our interface to the recording metaFile.
                        canvas = Graphics.FromImage(metaFile);
                        canvas.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                        //  Release handle to scratch device context.
                        newGraphics.ReleaseHdc(hdc);
                    }
                }
            }
        }

        public void DrawString(string s, Font font, Brush brush, double x, double y, StringFormat txtFormat, ChartRenderer chartRenderer)
        {
            canvas.DrawString(s, font, brush, Convert.ToSingle(x), Convert.ToSingle(chartRenderer.MetafileHeight - y), txtFormat);
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
        /// <param name="chartRenderer"></param>
        /// <remarks></remarks>
        public SizeF DrawStringAtAngle(string s, Font font, Brush brush, double x, double y, StringFormat txtFormat, LabelDirection direction, ChartRenderer chartRenderer)
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
            canvas.TranslateTransform(Convert.ToSingle(x), Convert.ToSingle(chartRenderer.MetafileHeight - y));
            canvas.RotateTransform(angle);
            canvas.DrawString(s, font, brush, 0, 0, txtFormat);
            canvas.ResetTransform();
            SizeF uprightSize = canvas.MeasureString(s, font);
            SizeF boundingSize = ToBoundingSize(uprightSize, direction);
            return boundingSize;
        }

        public float DirectionToAngle(LabelDirection Direction)
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

        public void DrawVerticalAxisLabel(string text, StringAlignment alignment, double x, double y, ChartRenderer chartRenderer)
        {
            using (StringFormat txtFormat = new StringFormat())
            {
                txtFormat.Alignment = alignment; // StringAlignment.Near;
                canvas.TranslateTransform(Convert.ToSingle(x), Convert.ToSingle(chartRenderer.MetafileHeight - y));
                canvas.RotateTransform(-90.0F);
                canvas.DrawString(text, chartRenderer.AxisLabelFont, Brushes.Black, 0, 0, txtFormat);
                canvas.ResetTransform();
            }
        }

        internal SizeF MeasureString(string s, Font axisLabelFont)
        {
            return canvas.MeasureString(s, axisLabelFont);
        }

        ///  <summary>
        ///  Draw a square of side size, centred on (x, y)
        ///  </summary>
        ///  <param name="p"></param>
        ///  <param name="x"></param>
        ///  <param name="y"></param>
        ///  <param name="size"></param>
        ///  <param name="fill"></param>
        ///  <remarks></remarks>
        public void DrawSquare(Pen p, double x, double y, double size, bool fill, ChartRenderer chartRenderer)
        {
            double size2 = size / 2;
            PointF[] pt = new PointF[5];
            pt[0].X = Convert.ToSingle(x - size2);
            pt[0].Y = Convert.ToSingle(chartRenderer.MetafileHeight - (y - size2));
            pt[1].X = Convert.ToSingle(x - size2);
            pt[1].Y = Convert.ToSingle(chartRenderer.MetafileHeight - (y + size2));
            pt[2].X = Convert.ToSingle(x + size2);
            pt[2].Y = Convert.ToSingle(chartRenderer.MetafileHeight - (y + size2));
            pt[3].X = Convert.ToSingle(x + size2);
            pt[3].Y = Convert.ToSingle(chartRenderer.MetafileHeight - (y - size2));
            pt[4].X = Convert.ToSingle(x - size2);
            pt[4].Y = Convert.ToSingle(chartRenderer.MetafileHeight - (y - size2));
            if (fill)
            {
                canvas.FillPolygon(chartRenderer.BlackBrush, pt);
            }
            canvas.DrawPolygon(p, pt);
        }

        ///  <summary>
        ///  Draw a diamond of diameter size, centred on (x, y)
        ///  </summary>
        ///  <param name="p"></param>
        ///  <param name="x"></param>
        ///  <param name="y"></param>
        ///  <param name="size"></param>
        ///  <param name="fill"></param>
        ///  <remarks></remarks>
        public void DrawDiamond(Pen p, double x, double y, double size, bool fill, ChartRenderer chartRenderer)
        {
            double size2 = size / 2;
            PointF[] pt = new PointF[5];
            pt[0].X = Convert.ToSingle(x - size2);
            pt[0].Y = Convert.ToSingle(chartRenderer.MetafileHeight - y);
            pt[1].X = Convert.ToSingle(x);
            pt[1].Y = Convert.ToSingle(chartRenderer.MetafileHeight - (y - size2));
            pt[2].X = Convert.ToSingle(x + size2);
            pt[2].Y = Convert.ToSingle(chartRenderer.MetafileHeight - y);
            pt[3].X = Convert.ToSingle(x);
            pt[3].Y = Convert.ToSingle(chartRenderer.MetafileHeight - (y + size2));
            pt[4].X = Convert.ToSingle(x - size2);
            pt[4].Y = Convert.ToSingle(chartRenderer.MetafileHeight - y);
            if (fill)
            {
                using (Brush b = new SolidBrush(p.Color))
                {
                    canvas.FillPolygon(b, pt);
                }
            }
            // Draw the diamond
            canvas.DrawPolygon(p, pt);
        }

        public void DrawMarker(double x, double y, double size, MarkerShape shape, bool isFilled, Pen p, ChartRenderer chartRenderer)
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
                            chartRenderer.EmfCanvas.FillEllipse(b, x - size, y + size, size2, size2, chartRenderer);
                        }
                    }
                    else
                    {
                        chartRenderer.EmfCanvas.DrawEllipse(p, x - size, y + size, size2, size2, chartRenderer);
                    }
                }
                    break;
                case MarkerShape.Square:
                {
                    if (isFilled)
                    {
                        using (Brush b = new SolidBrush(p.Color))
                        {
                            chartRenderer.EmfCanvas.FillRectangle(b, x - size, y + size, size2, size2, chartRenderer);
                        }
                    }
                    else
                    {
                        chartRenderer.EmfCanvas.DrawRectangle(p, x - size, y + size, size2, size2, chartRenderer);
                    }
                }
                    break;
                case MarkerShape.Triangle:
                {
                    PointF[] points = { new PointF(Convert.ToSingle(x - size), Convert.ToSingle(chartRenderer.MetafileHeight2 - (y - size))), new PointF(Convert.ToSingle(x), Convert.ToSingle(chartRenderer.MetafileHeight2 - (y + size))), new PointF(Convert.ToSingle(x + size), Convert.ToSingle(chartRenderer.MetafileHeight2 - (y - size))) };
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
                    chartRenderer.EmfCanvas.DrawLine(p, x - size, y, x + size, y, chartRenderer);
                    chartRenderer.EmfCanvas.DrawLine(p, x, y - size, x, y + size, chartRenderer);
                    break;
                case MarkerShape.Cross:
                    //  Same filled or unfilled
                    chartRenderer.EmfCanvas.DrawLine(p, x - size, y - size, x + size, y + size, chartRenderer);
                    chartRenderer.EmfCanvas.DrawLine(p, x - size, y + size, x + size, y - size, chartRenderer);
                    break;
                case MarkerShape.CircleLine:
                {
                    if (isFilled)
                    {
                        using (Brush b = new SolidBrush(p.Color))
                        {
                            chartRenderer.EmfCanvas.FillEllipse(b, x - size, y + size, size2, size2, chartRenderer);
                        }
                        chartRenderer.EmfCanvas.DrawLine(Pens.White, x, y - size, x, y + size, chartRenderer);
                    }
                    else
                    {
                        chartRenderer.EmfCanvas.DrawEllipse(p, x - size, y + size, size2, size2, chartRenderer);
                        chartRenderer.EmfCanvas.DrawLine(p, x, y - size, x, y + size, chartRenderer);
                    }
                }
                    break;
                case MarkerShape.SquareLine:
                    if (isFilled)
                    {
                        using (Brush b = new SolidBrush(p.Color))
                        {
                            chartRenderer.EmfCanvas.FillRectangle(b, x - size, y + size, size2, size2, chartRenderer);
                        }
                        chartRenderer.EmfCanvas.DrawLine(Pens.White, x - size, y + size, x + size, y - size, chartRenderer);
                    }
                    else
                    {
                        chartRenderer.EmfCanvas.DrawRectangle(p, x - size, y + size, size2, size2, chartRenderer);
                        chartRenderer.EmfCanvas.DrawLine(p, x - size, y + size, x + size, y - size, chartRenderer);
                    }
                    break;
                case MarkerShape.SquareCross:
                    if (isFilled)
                    {
                        using (Brush b = new SolidBrush(p.Color))
                        {
                            chartRenderer.EmfCanvas.FillRectangle(b, x - size, y + size, size2, size2, chartRenderer);
                        }
                        chartRenderer.EmfCanvas.DrawLine(Pens.White, x - size, y - size, x + size, y + size, chartRenderer);
                        chartRenderer.EmfCanvas.DrawLine(Pens.White, x - size, y + size, x + size, y - size, chartRenderer);
                    }
                    else
                    {
                        chartRenderer.EmfCanvas.DrawRectangle(p, x - size, y + size, size2, size2, chartRenderer);
                        chartRenderer.EmfCanvas.DrawLine(p, x - size, y - size, x + size, y + size, chartRenderer);
                        chartRenderer.EmfCanvas.DrawLine(p, x - size, y + size, x + size, y - size, chartRenderer);
                    }
                    break;
                case MarkerShape.Diamond:
                    DrawDiamond(p, x, y, size2, isFilled, chartRenderer);
                    break;
                default:
                    throw new ArgumentException("Don't know how to draw style's shape", "shape");
            }

        }

        public void FillEllipse(Brush b, double x, double y, double width, double height, ChartRenderer chartRenderer)
        {
            canvas.FillEllipse(b, Convert.ToInt32(Convert.ToSingle(x)), Convert.ToInt32(Convert.ToSingle(chartRenderer.MetafileHeight - y)), Convert.ToInt32(Convert.ToSingle(width)), Convert.ToInt32(Convert.ToSingle(height)));
        }

        public void DrawEllipse(Pen p, double x, double y, double width, double height, ChartRenderer chartRenderer)
        {
            canvas.DrawEllipse(p, Convert.ToSingle(x), Convert.ToSingle(chartRenderer.MetafileHeight - y), Convert.ToSingle(width), Convert.ToSingle(height));
        }

        public void FillRectangle(Brush b, double x, double y, double width, double height, ChartRenderer chartRenderer)
        {
            canvas.FillRectangle(b, Convert.ToSingle(x), Convert.ToSingle(chartRenderer.MetafileHeight - y), Convert.ToSingle(width), Convert.ToSingle(height));
        }

        public void DrawRectangle(Pen p, double x, double y, double width, double height, ChartRenderer chartRenderer)
        {
            canvas.DrawRectangle(p, Convert.ToSingle(x), Convert.ToSingle(chartRenderer.MetafileHeight - y), Convert.ToSingle(width), Convert.ToSingle(height));
        }

        public void DrawLine(Pen p, double x1, double y1, double x2, double y2, ChartRenderer chartRenderer)
        {
            canvas.DrawLine(p, Convert.ToSingle(Math.Round(x1, 0)), Convert.ToSingle(Math.Round(chartRenderer.MetafileHeight - y1, 0)), Convert.ToSingle(Math.Round(x2, 0)), Convert.ToSingle(Math.Round(chartRenderer.MetafileHeight - y2, 0)));
        }

        public void DrawRotatedTitle(string title, double x, double y, ChartRenderer chartRenderer)
        {
            if (!(string.IsNullOrEmpty(title)))
            {
                using (StringFormat txtFormat = new StringFormat())
                {
                    txtFormat.Alignment = StringAlignment.Center;
                    canvas.TranslateTransform(Convert.ToSingle(x), Convert.ToSingle(y));
                    canvas.RotateTransform(-90.0F);
                    canvas.DrawString(title, chartRenderer.AxisTitleFont, Brushes.Black, 0, 0, txtFormat);
                    canvas.ResetTransform();
                }
            }
        }

        public void DrawRotatedLabel(double xPos, double yPos, float angle, string title, StringFormat txtFormat, ChartRenderer chartRenderer)
        {
            canvas.TranslateTransform(Convert.ToSingle(xPos), Convert.ToSingle(yPos));
            canvas.RotateTransform(angle);
            canvas.DrawString(title, chartRenderer.AxisLabelFont1, chartRenderer.AxisBrush, 0, 0, txtFormat);
            canvas.ResetTransform();
        }

        internal double GetFontHeight(Font f)
        {
            return f.GetHeight(canvas);
        }
    }
}
