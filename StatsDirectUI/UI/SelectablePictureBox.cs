using System;
using System.ComponentModel;
using System.Windows.Forms;
using System.Drawing;

namespace StatsDirect.UI
{
    class SelectablePictureBox : PictureBox
    {
        public delegate void SelectedChangedEventHandler(object sender, EventArgs e);

        private bool selected;

        public event SelectedChangedEventHandler SelectedChanged;

        [Description("Is the PictureBox selected?")]
        public bool Selected
        {
            get => selected;
            set
            {
                bool oldSelected = selected;
                selected = value;
                if (oldSelected != selected)
                {
                    Invalidate();
                    OnSelectedChanged(EventArgs.Empty);
                }
            }
        }

        protected override void OnPaint(PaintEventArgs pe)
        {
            ControlPaint.DrawBorder(pe.Graphics, pe.ClipRectangle, selected ? Color.DarkBlue : BackColor,
                                    ButtonBorderStyle.Solid);
            base.OnPaint(pe);
        }

        protected virtual void OnSelectedChanged(EventArgs e)
        {
            if (null != SelectedChanged)
                SelectedChanged(this, e);
        }

        protected override void OnClick(EventArgs e)
        {
            Selected = true;
 	        base.OnClick(e);
        }
    }
}
