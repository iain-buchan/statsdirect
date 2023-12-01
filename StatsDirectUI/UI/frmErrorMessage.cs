using System;
using System.Windows.Forms;

namespace StatsDirect.UI
{
    /// <summary>
    /// The last-ditch form that is shown when StatsDirect encounters an error it cannot otherwise handle.
    /// </summary>
    public partial class frmErrorMessage : Form
    {
        public frmErrorMessage(string errorMessage)
        {
            InitializeComponent();
            txtError.Text = errorMessage;
        }

        private void cmdClose_Click(object? sender, EventArgs e)
        {
            Close();
        }
    }
}
