using System;
using System.Windows.Forms;

namespace StatsDirect.UI
{
    internal partial class frmUpdateCheck : Form
    {
        private bool IsBackgroundChecker { get; set; }

        private ISdApplication SdApplication { get; }

        public frmUpdateCheck(bool isBackgroundChecker, ISdApplication sdApplication)
        {
            SdApplication = sdApplication;
            InitializeComponent();

            if (isBackgroundChecker)
            {
                ctlUpdateStatsDirectCheck1.NewerVersionAvailable += ctlUpdateStatsDirectCheck1_NewerVersionAvailable;
                ctlUpdateRCheck1.Visible = false;
            }
            ctlUpdateRCheck1.StartCheck();
            ctlUpdateStatsDirectCheck1.StartCheck();
        }

        void ctlUpdateStatsDirectCheck1_NewerVersionAvailable(object? sender, EventArgs e)
        {
            StartPosition = FormStartPosition.CenterScreen;
            SdApplication.ShowOrQueueDialog(this, null);
        }

        private void cmdClose_Click(object? sender, EventArgs e)
        {
            Close();
        }
    }
}
