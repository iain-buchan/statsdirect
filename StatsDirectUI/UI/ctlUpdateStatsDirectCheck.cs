using System;
using System.Diagnostics;
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
            // Let the modal update-check dialog finish before opening the download dialog.
            // Its always-on-top window would otherwise cover the next window and save prompts.
            FindForm().DialogResult = DialogResult.OK;
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
            if (e.IsFinal)
                Utilities.DiagnosticLog.Write("checker status: final " + e.IsFinal + ", newer " + e.NewerVersionAvailable + ", InvokeRequired " + lblStatsDirectStatus.InvokeRequired + ", handle " + IsHandleCreated + ": " + e.Message);
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
            try
            {
                Process.Start(new ProcessStartInfo("http://www.statsdirect.com/Revisions.aspx") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Couldn't launch Web browser to view revisions", ex, false);
            }
        }
    }
}
