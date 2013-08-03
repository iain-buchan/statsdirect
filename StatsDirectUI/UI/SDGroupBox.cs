using System;
using System.Windows.Forms;
using System.Drawing;

namespace StatsDirect.UI
{
    class SDGroupBox : GroupBox
    {
        public override Size GetPreferredSize(Size proposedSize)
        {
            Size s = base.GetPreferredSize(proposedSize);
            if (AutoSize)
            {
                s.Height = Math.Max(s.Height, Bounds.Height - DisplayRectangle.Height + (Controls.Count > 0 ? Controls[0].PreferredSize.Height : 0));
            }
            return s;
        }
    }
}
