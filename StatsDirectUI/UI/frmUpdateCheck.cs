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
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            SdApplication.SoleInstance.ShowOrQueueDialog(this, null);
        }

        private void cmdClose_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}
