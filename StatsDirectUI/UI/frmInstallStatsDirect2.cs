using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace StatsDirect.UI
{
    public partial class frmInstallStatsDirect2 : Form
    {
        private bool userThinksStatsDirect2IsInstalled;

        public frmInstallStatsDirect2(bool isUpgrade)
        {
            InitializeComponent();
            string reason = isUpgrade ? "Your installed version of StatsDirect is older than 2.8.0 and needs upgrading to support this operation." : "You have no version of StatsDirect 2 installed.";
            string installOrUpgrade = isUpgrade ? "upgrade" : "install";
            string InstallOrUpgrade = isUpgrade ? "Upgrade" : "Install";
            string installedOrUpgraded = isUpgrade ? "upgraded" : "installed";
            lblRubric.Text = string.Format(lblRubric.Text, reason, installOrUpgrade, installedOrUpgraded);
            Text = string.Format(Text, InstallOrUpgrade);
        }

        private void lnkInstallR_LinkClicked(object? sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("http://www.statsdirect.com/download/setup.exe");
        }

        private void cmdDoNotInstall_Click(object? sender, EventArgs e)
        {
            Close();
        }

        private void cmdInstalled_Click(object? sender, EventArgs e)
        {
            userThinksStatsDirect2IsInstalled = true;
            Close();
        }

        public bool UserThinksStatsDirect2IsInstalled => userThinksStatsDirect2IsInstalled;
    }
}
