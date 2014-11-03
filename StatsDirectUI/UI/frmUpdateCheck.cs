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
            else
            {
                Application.UseWaitCursor = true;
            }
            ctlUpdateRCheck1.StartCheck();
            ctlUpdateStatsDirectCheck1.StartCheck();
        }

        void ctlUpdateStatsDirectCheck1_NewerVersionAvailable(object sender, EventArgs e)
        {
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            ShowDialog();
        }

        private void cmdClose_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}
