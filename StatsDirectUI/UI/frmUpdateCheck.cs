using System;
using System.Windows.Forms;

namespace StatsDirect.UI
{
    public partial class frmUpdateCheck : Form
    {
        private bool IsBackgroundChecker { get; set; }
        public frmUpdateCheck(bool isBackgroundChecker)
        {
            InitializeComponent();

            if (isBackgroundChecker)
            {
                ctlUpdateStatsDirectCheck1.NewerVersionAvailable += ctlUpdateStatsDirectCheck1_NewerVersionAvailable;
                ctlUpdateRCheck1.Visible = false;
            }
            ctlUpdateRCheck1.StartCheck();
            ctlUpdateStatsDirectCheck1.StartCheck();
        }

        void ctlUpdateStatsDirectCheck1_NewerVersionAvailable(object sender, EventArgs e)
        {
            // This arrives on a thread-pool thread, usually within a second of start-up, while the main window is still being built
            // on the UI thread. The dialog is shown once the main window is on the screen and pumping messages; showing it from
            // here put a form on the wrong thread.
            StartPosition = FormStartPosition.CenterScreen;
            SdApplication.SoleInstance.ShowWhenMainWindowShown(this);
        }

        private void cmdClose_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}
