using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace StatsDirect.UI
{
    public partial class frmOpening : Form
    {
        // .Net doesn't do double-clicks on option buttons, so we have to do it ourselves
        private DateTime lastNewWorkbookClick = DateTime.MinValue;
        private DateTime lastNewReportClick = DateTime.MinValue;

        public frmOpening()
        {
            InitializeComponent();
        }

        private void cmdOk_Click(object sender, EventArgs e)
        {
            try
            {
                if (rdoNewWorkbook.Checked)
                    CreateNewWorkbook();
                else if (rdoNewReport.Checked)
                    CreateNewReport();
                else
                    OpenFromList();
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Couldn't create or open file", ex, false);
            }
        }

        private void cmdCancel_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void frmOpening_Shown(object sender, EventArgs e)
        {
            LoadRecentFiles();
            lstRecent.Focus();
            if (lstRecent.Items.Count > 0)
                lstRecent.SelectedIndex = 0;
        }

        private void cmdNewWorkbook_Click(object sender, EventArgs e)
        {
            try
            {
                CreateNewWorkbook();
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Couldn't create workbook", ex, false);
            }
        }

        private void cmdNewReport_Click(object sender, EventArgs e)
        {
            try
            {
                CreateNewReport();
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Couldn't create report", ex, false);
            }
        }

        private void cmdBrowse_Click(object sender, EventArgs e)
        {
            try
            {
                BrowseForFile();
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Couldn't find or open file", ex, false);
            }
        }

        private void cmdBrowseImage_Click(object sender, EventArgs e)
        {
            try
            {
                BrowseForFile();
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Couldn't find or open file", ex, false);
            }
        }

        private void lstRecent_DoubleClick(object sender, EventArgs e)
        {
            try
            {
                OpenFromList();
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Couldn't open file", ex, false);
            }
        }

        private void lstRecent_Enter(object sender, EventArgs e)
        {
            rdoNewReport.Checked = false;
            rdoNewWorkbook.Checked = false;
        }

        private void lstRecent_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ('\n' == e.KeyChar)
            {
                try
                {
                    OpenFromList();
                }
                catch (Exception ex)
                {
                    SdApplication.SoleInstance.FriendlyError("Couldn't open file", ex, false);
                }
                e.Handled = true;
            }
            else if (27 == e.KeyChar)
            {
                Close();
                e.Handled = true;
            }
        }

        private void LoadRecentFiles()
        {
            IList<string> recentFiles = SdApplication.SoleInstance.RecentFiles;
            foreach (string path in recentFiles)
                lstRecent.Items.Add(path);
        }

        private void OpenFromList()
        {
            Close();
            SdApplication.SoleInstance.OpenFile((string)lstRecent.SelectedItem, true);
        }

        private void BrowseForFile()
        {
            Visible = false;
            if (SdApplication.SoleInstance.OpenFile())
                Close();
            else
                Visible = true;
        }

        private void CreateNewReport()
        {
            Close();
            SdApplication.SoleInstance.CreateReport();
        }

        private void CreateNewWorkbook()
        {
            Close();
            SdApplication.SoleInstance.CreateGrid();
        }

        private void rdoNewWorkbook_Click(object sender, EventArgs e)
        {
            const int doubleClickMilliseconds = 250; // TODO: Get the double-click time from the user's preferences
            DateTime now = DateTime.Now;
            if (lastNewWorkbookClick.AddMilliseconds(doubleClickMilliseconds) > now)
            {
                try
                {
                    CreateNewWorkbook();
                }
                catch (Exception ex)
                {
                    SdApplication.SoleInstance.FriendlyError("Couldn't create workbook", ex, false);
                }
            }
            else
            {
                lastNewWorkbookClick = now;
            }
        }

        private void rdoNewReport_Click(object sender, EventArgs e)
        {
            const int doubleClickMilliseconds = 250; // TODO: Get the double-click time from the user's preferences
            DateTime now = DateTime.Now;
            if (lastNewReportClick.AddMilliseconds(doubleClickMilliseconds) > now)
            {
                try
                {
                    CreateNewReport();
                }
                catch (Exception ex)
                {
                    SdApplication.SoleInstance.FriendlyError("Couldn't create report", ex, false);
                }
            }
            else
            {
                lastNewReportClick = now;
            }
        }
    }
}
