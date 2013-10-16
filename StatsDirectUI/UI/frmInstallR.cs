using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace StatsDirect.UI
{
    public partial class frmInstallR : Form
    {
        private bool userThinksRIsInstalled;

        public frmInstallR()
        {
            InitializeComponent();
        }

        private void lnkInstallR_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("http://cran.r-project.org/bin/windows/base/release.htm");
        }

        private void lnkMoreInformation_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("http://www.r-project.org/");
        }

        private void cmdDoNotInstall_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void cmdInstalled_Click(object sender, EventArgs e)
        {
            userThinksRIsInstalled = true;
            Close();
        }

        public bool UserThinksRIsInstalled { get { return userThinksRIsInstalled; } }
    }
}
