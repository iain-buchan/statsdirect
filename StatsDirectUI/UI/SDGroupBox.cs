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
                s.Height = Bounds.Height - DisplayRectangle.Height + Controls[0].PreferredSize.Height;
            }
            return s;
        }
    }
}
