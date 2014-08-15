using System;
using System.Drawing;
using System.IO;
using StatsDirect.Templates;

namespace StatsDirect.Charting
{
    /// <summary>
    /// A chart drawing surface that is appropriate for ChartRenderer to draw on.  (0, 0) is at the bottom-left of the canvas.
    /// </summary>
    internal interface IStatsDirectCanvas : IDisposable
    {
        void DrawString(string s, Font font, Brush brush, double x, double y, StringFormat txtFormat);

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
        SizeF DrawStringAtAngle(string s, Font font, Brush brush, double x, double y, StringFormat txtFormat, LabelDirection direction);

        ///  <summary>
        ///  Draw a square of side size, centred on (x, y).
        ///  </summary>
        ///  <param name="p">The pen with which to draw the outline and, if filled, from which to take the fill colour.</param>
        ///  <param name="x"></param>
        ///  <param name="y"></param>
        ///  <param name="size"></param>
        ///  <param name="fill">If true, fill the square; if false, merely draw the outline.</param>
        ///  <remarks></remarks>
        void DrawSquare(Pen p, double x, double y, double size, bool fill);

        ///  <summary>
        ///  Draw a diamond of diameter size, centred on (x, y)
        ///  </summary>
        ///  <param name="p">The pen with which to draw the outline and, if filled, from which to take the fill colour.</param>
        ///  <param name="x"></param>
        ///  <param name="y"></param>
        ///  <param name="size"></param>
        ///  <param name="fill">If true, fill the square; if false, merely draw the outline.</param>
        /// <remarks></remarks>
        void DrawDiamond(Pen p, double x, double y, double size, bool fill);

        void DrawMarker(double x, double y, double size, MarkerShape shape, bool isFilled, Pen p);
        void FillRectangle(Brush b, double x, double y, double w, double h);
        void DrawRectangle(Pen p, double x, double y, double w, double h);
        void DrawLine(Pen p, double x1, double y1, double x2, double y2);
        double GetFontHeight(Font f);
        SizeF MeasureString(string s, Font font);

        /// <summary>
        /// 
        /// </summary>
        /// <returns>A Stream which is live and, if read from its current point to its end, gives an Image.  Note that this detaches the Stream from the SDCanvas to prevent its disposal, so only call this once per SDCanvas!</returns>
        Stream DetachAndReturnImageStream();
    }
}