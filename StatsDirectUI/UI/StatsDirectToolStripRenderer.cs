using System;
using System.Windows.Forms;
using System.Drawing;

namespace StatsDirect.UI
{
    class StatsDirectToolStripRenderer : ToolStripProfessionalRenderer, IDisposable
    {
        private Brush backgroundBrush;

        protected override void OnRenderToolStripBackground(
            ToolStripRenderEventArgs e)
        {
            base.OnRenderToolStripBackground(e);

            // This late initialization is a workaround. The gradient
            // depends on the bounds of the GridStrip control. The bounds 
            // are dependent on the layout engine, which hasn't fully
            // performed layout by the time the Initialize method runs.
            if (null == backgroundBrush)
            {
                backgroundBrush = new SolidBrush(SystemColors.Control);
            }

            // Paint the GridStrip control's background.
            e.Graphics.FillRectangle(
                backgroundBrush,
                e.AffectedBounds);
        }

        protected override void OnRenderImageMargin(
            ToolStripRenderEventArgs e)
        {
            base.OnRenderImageMargin(e);

            // This late initialization is a workaround. The gradient
            // depends on the bounds of the GridStrip control. The bounds 
            // are dependent on the layout engine, which hasn't fully
            // performed layout by the time the Initialize method runs.
            if (null == backgroundBrush)
            {
                backgroundBrush = new SolidBrush(SystemColors.Control);
            }

            // Paint the GridStrip control's background.
            e.Graphics.FillRectangle(
                backgroundBrush,
                e.AffectedBounds);
        }
        /*
        protected override void OnRenderMenuItemBackground(
            ToolStripItemRenderEventArgs e)
        {
            base.OnRenderMenuItemBackground(e);

            if (null == this.menuBackgroundBrush)
            {
                this.menuBackgroundBrush = new SolidBrush(SystemColors.MenuHighlight);
            }

            if (e.Item.Selected)
            {
                e.Graphics.FillRectangle(menuBackgroundBrush, e.Item.ContentRectangle);
            }
            else
            {
                e.Graphics.FillRectangle(backgroundBrush, e.Item.ContentRectangle);
            }
        }
         */

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public virtual void Dispose(bool disposeManaged)
        {
            if (null != backgroundBrush)
            {
                backgroundBrush.Dispose();
                backgroundBrush = null;
            }
        }
    }
}
