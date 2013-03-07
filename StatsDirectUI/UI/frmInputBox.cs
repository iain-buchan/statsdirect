using System;
using System.Windows.Forms;

namespace StatsDirect.UI
{
    public sealed partial class frmInputBox : Form
    {
        private bool userCancelled;

        public frmInputBox(string prompt, string caption, string defaultValue)
        {
            InitializeComponent();
            Text = caption;
            lblPrompt.Text = prompt;
            txtInput.Text = defaultValue;
        }

        private void cmdCancel_Click(object sender, EventArgs e)
        {
            userCancelled = true;
            Close();
        }

        public bool UserCancelled
        {
            get { return userCancelled; }
        }

        public string Value
        {
            get { return txtInput.Text; }
        }

        private void cmdOk_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void frmInputBox_HelpRequested(object sender, HelpEventArgs hlpevent)
        {
            SdApplication.SoleInstance.ShowCurrentHelp();
        }
    }
}
