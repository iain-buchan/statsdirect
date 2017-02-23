using System;
using System.Windows.Forms;
using System.Diagnostics;

namespace StatsDirect.UI
{
    public partial class frmLicense : Form
    {
        private bool userCancelled;
        private bool closedViaButton;
        private readonly UserInfo ui;

        internal frmLicense(UserInfo ui, bool fillFieldsFromExistingData)
        {
            this.ui = ui;
            InitializeComponent();

            if (fillFieldsFromExistingData)
            {
                if (string.IsNullOrEmpty(ui.Name))
                    return;
                if (string.IsNullOrEmpty(ui.Company))
                    return;

                txtEmail.Text = ui.Name.Trim();
                string company = ui.Company;
                if (ui.Name.EndsWith("~~" + ui.Name.Substring(0, 1)))
                    company = company.Substring(0, company.Length - 3);
            }
        }

        public bool UserCancelled
        {
            get { return userCancelled; }
        }

        private void frmLicense_Load(object sender, EventArgs e)
        {
            tipEmail.SetToolTip(txtEmail, "You must either enter the email address that is specified with your licence key or use any email address for a 10 day free trial");
            tipKey.SetToolTip(txtKey, "Enter licence key or leave blank for 10 day free trial");
        }

        private void cmdCancel_Click(object sender, EventArgs e)
        {
            closedViaButton = true;
            userCancelled = true;
            Close();
        }

        private void cmdOK_Click(object sender, EventArgs e)
        {
            bool shouldClose = SetResults();
            userCancelled = false;
            if (shouldClose)
            {
                closedViaButton = true;
                Close();
            }
        }

        private bool SetResults()
        {
            string email = txtEmail.Text.Trim();
            string organisation = string.Empty;
            string key = txtKey.Text.Trim();
            string errorMessage;
            bool retval;
            try
            {
                retval = License.SetResults(email, organisation, key, ui, out errorMessage);
            }
            catch (Exception)
            {
                errorMessage = "An unexpected error occurred while trying to set the licence. Please check you have entered your email address, organisation and key correctly.";
                retval = false;
            }
            if (null != errorMessage)
                SdApplication.SoleInstance.MsgboxX(errorMessage, MessageBoxButtons.OK, MessageBoxIcon.Exclamation, "StatsDirect licence", false);
            return retval;
        }

        private void cmdHelp_Click(object sender, EventArgs e)
        {
            SdApplication.SoleInstance.ShowHelp(this, "50335");
        }

        private void frmLicense_Shown(object sender, EventArgs e)
        {
            txtEmail.Select();
        }

        private void txtEmail_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Return)
                return;

            if (txtEmail.Text.Length > 4)
            {
                e.Handled = true;
                txtKey.Focus();
            }
        }

        private void txtKey_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Return)
                return;

            if (string.IsNullOrEmpty(ui.Expires) || (ui.Expires.Length > 1 && txtKey.Text.Length > 1))
            {
                e.Handled = true;
                cmdOK.PerformClick();
            }
        }

        private void frmLicense_FormClosing(object sender, FormClosingEventArgs e)
        {
            // By default we go round again if the user didn't indicate an explicit cancel and we didn't get the data we need.
            // This is unpleasant on e.g. a Windows shutdown, so we only do this if the close is the user's initiation.
            if (e.CloseReason != CloseReason.UserClosing)
                userCancelled = true;

            // If closedViaButton is not set, we've been closed via some other means such as Alt-F4.  In this case, assume the user wants to cancel.
            // This prevents the form being repeatedly displayed.
            if (!closedViaButton)
                userCancelled = true;
        }

        private void lblWeb_Click(object sender, EventArgs e)
        {
            Process.Start("http://www.statsdirect.com/");
        }
    }
}
