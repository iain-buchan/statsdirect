using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace StatsDirect.UI
{
    /// <summary>
    /// An extended combo box that displays images as well as / instead of text and is capable of drawing disabled items.
    /// </summary>
    public partial class ComboBoxEx : ComboBox
    {
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public ImageList ImageList { get; set; }

        public ComboBoxEx()
        {
            DrawMode = DrawMode.OwnerDrawFixed;
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            e.DrawBackground();

            Size imageSize = null != ImageList ? ImageList.ImageSize : Size.Empty;
            Rectangle bounds = e.Bounds;

            using Brush foreBrush = new SolidBrush(e.ForeColor);
            if (e.Index >= 0 && Items[e.Index] is ComboBoxExItem)
            {
                ComboBoxExItem item = (ComboBoxExItem)Items[e.Index];

                int textOffset = 0;
                if (item.ImageIndex != -1)
                {
                    ImageList.Draw(e.Graphics, bounds.Left, bounds.Top, item.ImageIndex);
                    textOffset = imageSize.Width;
                }
                if (null != item.Text)
                {
                    if (item.Enabled)
                    {
                        e.Graphics.DrawString(item.Text, e.Font, foreBrush, bounds.Left + textOffset, bounds.Top);
                    }
                    else
                    {
                        using Font f = new(e.Font, FontStyle.Strikeout);
                        e.Graphics.DrawString(item.Text, f, SystemBrushes.GrayText, bounds.Left + textOffset, bounds.Top);
                    }
                }
            }
            else
            {
                // Mimic the usual ComboBox behaviour of allowing any item and rendering it to a string for drawing.
                e.Graphics.DrawString(e.Index != -1 ? Items[e.Index].ToString() : Text, e.Font, foreBrush, bounds.Left, bounds.Top);
            }

            // e.DrawFocusRectangle();

            // base.OnDrawItem(e);
        }

        protected override void OnSelectedIndexChanged(EventArgs e)
        {
            EnsureEnabledSelection();
            base.OnSelectedIndexChanged(e);
        }

        /// <summary>
        /// If the selected item is disabled, deselect it (combo box) or try to select an enabled one (list box).
        /// </summary>
        public void EnsureEnabledSelection()
        {
            if (SelectedIndex >= 0 && Items[SelectedIndex] is ComboBoxExItem && !((ComboBoxExItem)Items[SelectedIndex]).Enabled)
            {
                if (DropDownStyle == ComboBoxStyle.DropDownList)
                {
                    // List box, so we must select something - choose the lowest available item.  If we have no enabled items, choose [0] and accept we've selected a disabled one.
                    bool found = false;
                    for (int probe = 0; probe < Items.Count; probe++)
                    {
                        if (!(Items[probe] is ComboBoxExItem && !((ComboBoxExItem)Items[probe]).Enabled))
                        {
                            SelectedIndex = probe;
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                        SelectedIndex = 0;
                }
                else
                {
                    // Combo box - blank the selection
                    SelectedIndex = -1;
                }
            }
        }
    }

    class ComboBoxExItem
    {
        public string Text { get; set; }

        public int ImageIndex { get; set; }

        public bool Enabled { get; set; } = true;

        public object Tag { get; set; }

        public ComboBoxExItem()
            : this(string.Empty)
        {
        }

        public ComboBoxExItem(string text)
            : this(text, -1)
        {
        }

        public ComboBoxExItem(string text, int imageIndex)
        {
            Text = text;
            ImageIndex = imageIndex;
        }

        public override string ToString()
        {
            return Text;
        }
    }
}
