using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace StatsDirect.UI
{
    // Draw the toolbar symbols of the main window and the script window at their
    // actual display size. The original resource images still supply layout and
    // designer previews, but are never stretched to paint these buttons on a
    // high-DPI display.
    internal sealed class SharpToolbarRenderer : ToolStripSystemRenderer
    {
        protected override void OnRenderOverflowButtonBackground(ToolStripItemRenderEventArgs e)
        {
            var bounds = new Rectangle(Point.Empty, e.Item.Size);
            using var background = new SolidBrush(e.Item.Selected ? SystemColors.ControlLight : SystemColors.Control);
            e.Graphics.FillRectangle(background, bounds);
            var state = e.Graphics.Save();
            try
            {
                float size = Math.Min(bounds.Width - 4, bounds.Height - 4);
                e.Graphics.TranslateTransform((bounds.Width - size) / 2, (bounds.Height - size) / 2);
                e.Graphics.ScaleTransform(size / 24, size / 24);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var pen = new Pen(SystemColors.ControlText, 1.8f);
                foreach (int x in new[] { 4, 12, 20 }) e.Graphics.DrawEllipse(pen, x - 1, 11, 2, 2);
            }
            finally { e.Graphics.Restore(state); }
        }

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
                case "findToolStripButton":
                case "rToolStripButton":
                case "rPlusToolStripButton":
                case "aiToolStripButton":
                case "undoToolStripButton":
                case "redoToolStripButton":
                case "runToolStripButton":
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
                    case "undoToolStripButton":
                    case "redoToolStripButton":
                        if (e.Item.Name == "redoToolStripButton") { g.TranslateTransform(24, 0); g.ScaleTransform(-1, 1); }
                        Lines(new(8, 4), new(3, 9), new(8, 14));
                        using (var turn = new GraphicsPath())
                        {
                            turn.AddLine(3, 9, 14, 9);
                            turn.AddBezier(14, 9, 23, 9, 23, 21, 14, 21);
                            g.DrawPath(pen, turn);
                        }
                        break;
                    case "runToolStripButton":
                        Lines(new(7, 3), new(21, 12), new(7, 21), new(7, 3));
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
                    case "findToolStripButton":
                        g.DrawEllipse(pen, 3, 3, 12, 12);
                        Line(14, 14, 21, 21);
                        break;
                    case "rToolStripButton":
                        // the letter R: its stem, its bowl and its leg
                        Lines(new(7, 21), new(7, 3), new(13, 3));
                        using (var bowl = new GraphicsPath())
                        {
                            bowl.AddBezier(13, 3, 19.5f, 3, 19.5f, 12, 13, 12);
                            g.DrawPath(pen, bowl);
                        }
                        Line(7, 12, 13, 12);
                        Line(12.5f, 12, 18.5f, 21);
                        break;
                    case "rPlusToolStripButton":
                        Lines(new(3, 20), new(3, 4), new(8, 4));
                        using (var bowl = new GraphicsPath())
                        {
                            bowl.AddBezier(8, 4, 15, 4, 15, 12, 8, 12);
                            g.DrawPath(pen, bowl);
                        }
                        Line(3, 12, 8, 12); Line(8, 12, 14, 20);
                        Line(16, 12, 23, 12); Line(19.5f, 8.5f, 19.5f, 15.5f);
                        break;
                    case "aiToolStripButton":
                        Lines(new(2, 20), new(8, 4), new(14, 20)); Line(4.25f, 14, 11.75f, 14);
                        Line(18, 4, 22, 4); Line(20, 4, 20, 20); Line(18, 20, 22, 20);
                        break;
                }
            }
            finally { g.Restore(state); }
        }
    }
}
