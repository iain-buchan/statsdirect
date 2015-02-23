using StatsDirect.R;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace StatsDirect.UI
{
    public partial class ctlUpdateRCheck : UserControl
    {
        private RUpdateChecker checker;
        public event EventHandler NewerVersionAvailable;

        public ctlUpdateRCheck()
        {
            InitializeComponent();
        }

        public void StopCheck()
        {
            if (null != checker)
                checker.StopCheck();
        }

        public void StartCheck()
        {
            UseWaitCursor = true;
            checker = new RUpdateChecker();
            checker.StartCheck(UpdateStatus);
        }

        private void UpdateStatus(object sender, UpdateCheckerEventArgs e)
        {
            if (lblStatus.InvokeRequired)
                lblStatus.Invoke(new MethodInvoker(delegate { FixupUi(e); }));
            else
                FixupUi(e);
            if (e.IsFinal && e.NewerVersionAvailable && null != NewerVersionAvailable)
                NewerVersionAvailable.Invoke(this, EventArgs.Empty);
        }

        private void FixupUi(UpdateCheckerEventArgs e)
        {
            lblStatus.Text = e.Message;
            cmdDownloadR.Visible = e.IsFinal && e.NewerVersionAvailable;
            if (e.IsFinal)
                UseWaitCursor = false;
        }

        private void cmdDownloadR_Click(object sender, EventArgs e)
        {
            try
            {
                Process.Start("http://cran.r-project.org/bin/windows/base/release.htm");
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Couldn't launch Web browser to fetch R update", ex, false);
            }
            ((Form)this.TopLevelControl).Close(); // See #1031; no point leaving the form here.
        }
    }
}
