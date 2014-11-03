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
            checker = new RUpdateChecker();
            checker.StartCheck(UpdateStatus);
        }

        private void UpdateStatus(object sender, UpdateCheckerEventArgs e)
        {
            if (e.IsFinal)
                Application.UseWaitCursor = false;
            if (lblStatus.InvokeRequired)
            {
                lblStatus.Invoke(new MethodInvoker(delegate { lblStatus.Text = e.Message; cmdDownloadR.Visible = e.IsFinal && e.NewerVersionAvailable; }));
            }
            else
            {
                lblStatus.Text = e.Message;
                cmdDownloadR.Visible = e.IsFinal && e.NewerVersionAvailable;
            }
            if (e.IsFinal && e.NewerVersionAvailable && null != NewerVersionAvailable)
                NewerVersionAvailable.Invoke(this, EventArgs.Empty);
        }

        private void cmdDownloadR_Click(object sender, EventArgs e)
        {
            Process.Start("http://cran.r-project.org/bin/windows/base/release.htm");
            ((Form)this.TopLevelControl).Close(); // See #1031; no point leaving the form here.
        }
    }
}
