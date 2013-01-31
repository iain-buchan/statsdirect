using System;
using System.Windows.Forms;
using StatsDirect.Utilities;

namespace StatsDirect.UI
{
    public partial class frmLicense : Form
    {
        private bool userCancelled;
        private readonly UserInfo ui;

        public frmLicense(UserInfo ui)
        {
            this.ui = ui;
            InitializeComponent();
        }

        public frmLicense(UserInfo UI, string name, string organisation)
            : this(UI)
        {
            if (string.IsNullOrEmpty(name))
                return;
            if (string.IsNullOrEmpty(organisation))
                return;

            txtEmail.Text = name.Trim();
            if (name.EndsWith("~~" + name.Substring(0, 1)))
            {
                organisation = organisation.Substring(0, organisation.Length - 3);
            }
            txtOrganisation.Text = organisation.Trim();
        }

        public bool UserCancelled
        {
            get { return userCancelled; }
        }

        private void frmLicense_Load(object sender, EventArgs e)
        {
            tipEmail.SetToolTip(txtEmail, "You must either enter the email address that is specified with your licence key or use any email address for a 10 day free trial");
            tipOrganisation.SetToolTip(txtOrganisation, "Enter the name of your organisation or leave blank");
            tipKey.SetToolTip(txtKey, "Enter licence key or leave blank for 10 day free trial");
        }

        private void cmdCancel_Click(object sender, EventArgs e)
        {
            userCancelled = true;
            Close();
        }

        private void cmdOK_Click(object sender, EventArgs e)
        {
            bool shouldClose = SetResults();
            userCancelled = false;
            if (shouldClose)
                Close();
        }

        static int count;

        private bool SetResults()
        {
            count++;

            if (count >= 4)
            {
                // four wrong attempts with key
                SDApplication.SoleInstance.MsgboxX("You have entered an invalid licence key four times\n\r\n\rIt is illegal to use this software without a valid licence.\n\r\n\rFor trial use, do not enter a licence key.", MessageBoxButtons.OK, MessageBoxIcon.Exclamation, "StatsDirect licence unlock attempts", false);
                return true;
            }
            if (txtEmail.Text.Trim().Length < 5)
            {
                // no name
                SDApplication.SoleInstance.MsgboxX("You must enter a valid email address of at least five characters.", MessageBoxButtons.OK, MessageBoxIcon.Exclamation, "StatsDirect User Email Address", false);
                return false;
            }
            // email/username present
            DateTime expiry;
            switch (License.KeyMatch(ui, txtEmail.Text, txtOrganisation.Text, txtKey.Text, out expiry))
            {
                case 1:
                    // key matches name
                    if (expiry < DateTime.Today)
                    {

                        SDApplication.SoleInstance.MsgboxX("The licence key used has expired.\n\r\n\rSee www.statsdirect.com for more information.", MessageBoxButtons.OK, MessageBoxIcon.Exclamation, "StatsDirect Licence", false);
                    }
                    else
                    {
                        string a = txtEmail.Text.Trim();
                        a = License.XorString(a, License.REG_KEY_KEY);
                        SDRegistry.SaveSetting(License.REG_APP_NAME, License.REG_LIC, License.REG_UI_NAME, License.StrToNum(a));
                        a = txtOrganisation.Text.Trim();
                        a = new string(' ', 4) + a + "~~" + txtEmail.Text.Substring(0, 1);
                        a = License.XorString(a, License.REG_KEY_KEY);
                        SDRegistry.SaveSetting(License.REG_APP_NAME, License.REG_LIC, License.REG_UI_COMPANY, License.StrToNum(a));
                        a = expiry.ToString();
                        a = License.XorString(a, License.REG_KEY_KEY);
                        SDRegistry.SaveSetting(License.REG_APP_NAME, License.REG_LIC, License.REG_UI_EXPIRES, License.StrToNum(a));
                        return true;
                    }
                    break;
                case 2:
                    // no key/new trial
                    {
                        string a = txtEmail.Text.Trim();
                        a = License.XorString(a, License.REG_KEY_KEY);
                        SDRegistry.SaveSetting(License.REG_APP_NAME, License.REG_LIC, License.REG_UI_NAME, License.StrToNum(a));
                        a = txtOrganisation.Text.Trim();
                        a = new string(' ', 4) + a;
                        a = License.XorString(a, License.REG_KEY_KEY);
                        SDRegistry.SaveSetting(License.REG_APP_NAME, License.REG_LIC, License.REG_UI_COMPANY, License.StrToNum(a));
                        a = DateTime.Today.AddDays(10).ToString();
                        a = License.XorString(a, License.REG_KEY_KEY);
                        SDRegistry.SaveSetting(License.REG_APP_NAME, License.REG_LIC, License.REG_UI_EXPIRES, License.StrToNum(a));
                        return true;
                    }
                case 3:
                    if (DateTime.Parse(ui.Expires) < DateTime.Today)
                    {
                        SDApplication.SoleInstance.MsgboxX("Your StatsDirect licence has expired.\n\r\n\rSee www.statsdirect.com for more information.", MessageBoxButtons.OK, MessageBoxIcon.Exclamation, "StatsDirect Licence", false);
                    }
                    else
                    {
                        SDApplication.SoleInstance.MsgboxX("Your StatsDirect licence expires on " + ui.Expires + ".\n\r\n\rSee http://www.statsdirect.com for more information.", MessageBoxButtons.OK, MessageBoxIcon.Exclamation, "StatsDirect Licence", false);
                    }
                    break;
                default:
                    // wrong key and/or name, or no key and expired trial
                    SDApplication.SoleInstance.MsgboxX("You must enter the email address and key exactly as specified in your licence.\n\r\n\rPlease try to copy from your confirmation of purchase email and paste into the relevant boxes here.", MessageBoxButtons.OK, MessageBoxIcon.Exclamation, "StatsDirect licence key", false);
                    break;
            }
            // If we get here, there was an error of some kind
            return false;
        }

        private void cmdHelp_Click(object sender, EventArgs e)
        {
            SDApplication.SoleInstance.ShowHelp(this, "50335");
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
                txtOrganisation.Focus();
            }
        }

        private void txtOrganisation_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Return)
                return;

            e.Handled = true;
            txtKey.Focus();
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
    }
}
