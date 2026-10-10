using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace StatsDirect.UI
{
    // Draw the main toolbar symbols at their actual display size. The original
    // resource images still supply layout/designer previews, but are never
    // stretched to paint these buttons on a high-DPI display.
    internal sealed class SharpToolbarRenderer : ToolStripSystemRenderer
    {
        protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e)
        {
            switch (e.Item.Name)
            {
                case "newToolStripButton":
                case "openToolStripButton":
                case "saveToolStripButton":
                case "printToolStripButton":
                case "cutToolStripButton":
                case "copyToolStripButton":
                case "pasteToolStripButton":
                case "helpToolStripButton":
                    DrawSymbol(e);
                    return;
                default:
                    base.OnRenderItemImage(e);
                    return;
            }
        }

        private static void DrawSymbol(ToolStripItemImageRenderEventArgs e)
        {
            Rectangle bounds = e.ImageRectangle;
            if (bounds.Width <= 0 || bounds.Height <= 0) return;
            Graphics g = e.Graphics;
            GraphicsState state = g.Save();
            try
            {
                g.TranslateTransform(bounds.X, bounds.Y);
                g.ScaleTransform(bounds.Width / 24f, bounds.Height / 24f);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using var pen = new Pen(e.Item.Enabled ? SystemColors.ControlText : SystemColors.GrayText, 1.8f)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round,
                    LineJoin = LineJoin.Round
                };
                void Line(float x1, float y1, float x2, float y2) => g.DrawLine(pen, x1, y1, x2, y2);
                void Lines(params PointF[] points) => g.DrawLines(pen, points);
                void Box(float x, float y, float width, float height) => g.DrawRectangle(pen, x, y, width, height);
                switch (e.Item.Name)
                {
                    case "newToolStripButton":
                        Lines(new(14, 3), new(5, 3), new(5, 21), new(19, 21), new(19, 8), new(14, 3), new(14, 8), new(19, 8));
                        Line(8, 13, 16, 13); Line(8, 17, 14, 17);
                        break;
                    case "openToolStripButton":
                        Lines(new(3, 18), new(3, 5), new(9, 5), new(11, 8), new(20, 8), new(20, 11));
                        Lines(new(3, 20), new(6, 11), new(22, 11), new(19, 20), new(3, 20));
                        break;
                    case "saveToolStripButton":
                        Lines(new(4, 3), new(17, 3), new(21, 7), new(21, 21), new(3, 21), new(3, 3), new(4, 3));
                        Lines(new(7, 3), new(7, 9), new(16, 9), new(16, 3));
                        Lines(new(7, 21), new(7, 14), new(17, 14), new(17, 21));
                        break;
                    case "printToolStripButton":
                        Lines(new(7, 8), new(7, 3), new(17, 3), new(17, 8));
                        Lines(new(6, 17), new(3, 17), new(3, 8), new(21, 8), new(21, 17), new(18, 17));
                        Box(7, 14, 10, 7); Line(17, 11, 18, 11);
                        break;
                    case "cutToolStripButton":
                        g.DrawEllipse(pen, 3, 3, 6, 6); g.DrawEllipse(pen, 3, 15, 6, 6);
                        Line(8, 8, 20, 20); Line(8, 16, 20, 4);
                        break;
                    case "copyToolStripButton":
                        Lines(new(7, 16), new(3, 16), new(3, 3), new(15, 3), new(15, 7));
                        Box(8, 8, 13, 13);
                        break;
                    case "pasteToolStripButton":
                        Lines(new(8, 5), new(4, 5), new(4, 21), new(20, 21), new(20, 5), new(16, 5));
                        Box(8, 3, 8, 4); Line(8, 12, 16, 12); Line(8, 16, 14, 16);
                        break;
                    case "helpToolStripButton":
                        g.DrawEllipse(pen, 2.5f, 2.5f, 19, 19);
                        using (var question = new GraphicsPath())
                        {
                            question.AddBezier(8.5f, 8, 8.5f, 4.5f, 16, 4.5f, 16, 8.5f);
                            question.AddBezier(16, 8.5f, 16, 11, 12, 11, 12, 14);
                            g.DrawPath(pen, question);
                        }
                        Line(12, 17, 12, 17.2f);
                        break;
                }
            }
            finally { g.Restore(state); }
        }
    }
}
