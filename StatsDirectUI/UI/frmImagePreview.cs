using System;
using System.Drawing;
using System.Windows.Forms;

namespace StatsDirect.UI
{
    public partial class frmImagePreview : Form
    {
        private double aspectRatio;

        public frmImagePreview()
        {
            InitializeComponent();
            aspectRatio = 1.0;
        }

        private void cmdClose_Click(object sender, EventArgs e)
        {
            Close();
        }

        public Image Image
        {
            get => pic.Image;
            set
            {
                pic.Image = value;
                aspectRatio = value.Width / (double)value.Height;
                SetHeight();
            }
        }

        private void frmImagePreview_Resize(object sender, EventArgs e)
        {
            SetHeight();
        }

        private void SetHeight()
        {
            pic.Height = (int) (pic.Width / aspectRatio);
        }
    }
}
