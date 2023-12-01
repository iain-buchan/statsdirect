using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace StatsDirect.UI
{
    internal partial class ctlUpdateStatsDirectCheck : UserControl
    {
        private StatsDirectUpdateChecker? statsDirectChecker;
        public event EventHandler? NewerVersionAvailable;

        private ISdApplication SdApplication { get; }

        public ctlUpdateStatsDirectCheck(ISdApplication sdApplication)
        {
            SdApplication = sdApplication;
            InitializeComponent();
        }

        private void button1_Click(object? sender, EventArgs e)
        {
            StopCheck();
            try
            {
                SdApplication.CloseAndUpdate();
            }
            catch (Exception ex)
            {
                SdApplication.FriendlyError("Couldn't launch Web browser to fetch StatsDirect update", ex, false);
            }
        }

        public void StopCheck()
        {
            statsDirectChecker?.StopCheck();
        }

        public void StartCheck()
        {
            UseWaitCursor = true;
            statsDirectChecker = new StatsDirectUpdateChecker();
            // Fire and forget!
            statsDirectChecker.StartCheck(UpdateStatsDirectStatus);
        }

        private void UpdateStatsDirectStatus(object? sender, UpdateCheckerEventArgs e)
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

        private void lblWhatsNew_Click(object? sender, EventArgs e)
        {
            try
            {
                Process.Start("http://www.statsdirect.com/Revisions.aspx");
            }
            catch (Exception ex)
            {
                SdApplication.FriendlyError("Couldn't launch Web browser to view revisions", ex, false);
            }
        }
    }
}
