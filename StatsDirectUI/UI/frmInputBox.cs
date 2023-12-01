using System;
using System.Windows.Forms;

namespace StatsDirect.UI
{
    public sealed partial class frmInputBox : Form
    {
        private bool userCancelled;

        private ISdApplication SdApplication { get; }

        internal frmInputBox(string prompt, string caption, string defaultValue, ISdApplication sdApplication)
        {
            SdApplication = sdApplication;
            InitializeComponent();
            Text = caption;
            lblPrompt.Text = prompt;
            txtInput.Text = defaultValue;
        }

        private void cmdCancel_Click(object? sender, EventArgs e)
        {
            userCancelled = true;
            Close();
        }

        public bool UserCancelled => userCancelled;

        public string Value => txtInput.Text;

        private void cmdOk_Click(object? sender, EventArgs e)
        {
            Close();
        }

        private void frmInputBox_HelpRequested(object? sender, HelpEventArgs hlpevent)
        {
            SdApplication.ShowCurrentHelp();
        }
    }
}
