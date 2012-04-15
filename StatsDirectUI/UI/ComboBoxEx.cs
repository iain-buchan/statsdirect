using System.Drawing;
using System.Windows.Forms;

namespace StatsDirect.UI
{
    public partial class ComboBoxEx : ComboBox
    {

        private ImageList imageList;
        public ImageList ImageList
        {
            get { return imageList; }
            set { imageList = value; }
        }

        public ComboBoxEx()
        {
            DrawMode = DrawMode.OwnerDrawFixed;
        }

        protected override void OnDrawItem(DrawItemEventArgs ea)
        {
            ea.DrawBackground();
            ea.DrawFocusRectangle();

            Size imageSize = imageList.ImageSize;
            Rectangle bounds = ea.Bounds;

            Brush foreBrush = new SolidBrush(ea.ForeColor);
            try
            {
                ComboBoxExItem item = (ComboBoxExItem)Items[ea.Index];

                if (item.ImageIndex != -1)
                {
                    imageList.Draw(ea.Graphics, bounds.Left, bounds.Top,
                    item.ImageIndex);
                    ea.Graphics.DrawString(item.Text, ea.Font, foreBrush, bounds.Left + imageSize.Width, bounds.Top);
                }
                else
                {
                    ea.Graphics.DrawString(item.Text, ea.Font, foreBrush, bounds.Left, bounds.Top);
                }
            }
            catch
            {
                ea.Graphics.DrawString(ea.Index != -1 ? Items[ea.Index].ToString() : Text, ea.Font, foreBrush,
                                       bounds.Left, bounds.Top);
            }
            finally
            {
                foreBrush.Dispose();
            }

            base.OnDrawItem(ea);
        }
    }

    class ComboBoxExItem
    {
        private string _text;
        public string Text
        {
            get { return _text; }
            set { _text = value; }
        }

        public int ImageIndex { get; set; }

        public ComboBoxExItem()
            : this("")
        {
        }

        public ComboBoxExItem(string text)
            : this(text, -1)
        {
        }

        public ComboBoxExItem(string text, int imageIndex)
        {
            _text = text;
            ImageIndex = imageIndex;
        }

        public override string ToString()
        {
            return _text;
        }
    }
}
