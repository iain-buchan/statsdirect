using System;
using System.Drawing;
using System.Windows.Forms;

namespace StatsDirect.UI
{
    public static class WindowsFormsExtensions
    {
        public static void AutoSizeToList(this ComboBox cbo)
        {
            // There's no way of autosizing a combo... so we do it by hand!
            float maximumItemWidth = MaximumItemWidth(cbo);
            int vertScrollBarWidth = cbo.Items.Count > cbo.MaxDropDownItems ? SystemInformation.VerticalScrollBarWidth : 0;
            int currentTextWidth = cbo.DropDownWidth - vertScrollBarWidth;
            // Prefer to grow but not shrink
            int preferredTextWidth = (int)Math.Max(currentTextWidth, maximumItemWidth);
            cbo.DropDownWidth = preferredTextWidth + vertScrollBarWidth;
            cbo.Size = new Size(preferredTextWidth + SystemInformation.VerticalScrollBarWidth, cbo.PreferredHeight); // Surprisingly, it appears the width of the drop-down arrow part of a ComboBox is the same as that of a vertical scrollbar.
        }

        private static float MaximumItemWidth(ComboBox cbo)
        {
            Graphics g = cbo.CreateGraphics();
            Font font = cbo.Font;

            float maxWidth = 0;
            foreach (object item in cbo.Items)
            {
                string s = item.ToString();
                float newWidth = g.MeasureString(s, font).Width;
                if (maxWidth < newWidth)
                    maxWidth = newWidth;
            }
            return maxWidth;
        }
    }
}
