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
            get { return pic.Image; }
            set
            {
                pic.Image = value;
                aspectRatio = value.Width / ((double)value.Height);
            }
        }

        private void frmImagePreview_Resize(object sender, EventArgs e)
        {
            pic.Height = (int)(pic.Width / aspectRatio);
        }
    }
}
