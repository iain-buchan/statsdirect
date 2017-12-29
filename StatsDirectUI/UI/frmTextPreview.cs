using System;
using System.Windows.Forms;

namespace StatsDirect.UI
{
    public partial class frmTextPreview : Form
    {
        public frmTextPreview()
        {
            InitializeComponent();
        }

        private void cmdClose_Click(object sender, EventArgs e)
        {
            Close();
        }

        public string Rtf
        {
            get => rtb.Rtf;
            set => rtb.Rtf = value;
        }
    }
}
