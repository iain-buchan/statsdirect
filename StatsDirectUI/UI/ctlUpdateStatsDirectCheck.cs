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
    public partial class ctlUpdateStatsDirectCheck : UserControl
    {
        private StatsDirectUpdateChecker statsDirectChecker;
        public event EventHandler NewerVersionAvailable;

        public ctlUpdateStatsDirectCheck()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            StopCheck();
            SdApplication.SoleInstance.CloseAndUpdate();
        }

        public void StopCheck()
        {
            if (null != statsDirectChecker)
                statsDirectChecker.StopCheck();
        }

        public void StartCheck()
        {
            UseWaitCursor = true;
            statsDirectChecker = new StatsDirectUpdateChecker();
            statsDirectChecker.StartCheck(UpdateStatsDirectStatus);
        }

        private void UpdateStatsDirectStatus(object sender, UpdateCheckerEventArgs e)
        {
            if (lblStatsDirectStatus.InvokeRequired)
                lblStatsDirectStatus.Invoke(new MethodInvoker(delegate { FixupUi(e); }));
            else
                FixupUi(e);
            if (e.IsFinal && e.NewerVersionAvailable && null != NewerVersionAvailable)
                NewerVersionAvailable.Invoke(this, EventArgs.Empty);
        }

        private void FixupUi(UpdateCheckerEventArgs e)
        {
            lblStatsDirectStatus.Text = e.Message;
            if (e.IsFinal)
            {
                cmdUpdateStatsDirect.Visible = e.NewerVersionAvailable;
                lblWhatsNew.Visible = e.NewerVersionAvailable;
                UseWaitCursor = false;
            }
        }

        private void lblWhatsNew_Click(object sender, EventArgs e)
        {
            Process.Start("http://www.statsdirect.com/Revisions.aspx");
        }
    }
}
