using System;
using System.Windows.Forms;

namespace StatsDirect.UI
{
    public partial class frmErrorMessage : Form
    {
        public frmErrorMessage(string errorMessage)
        {
            InitializeComponent();
            txtError.Text = errorMessage;
        }

        private void cmdClose_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}
