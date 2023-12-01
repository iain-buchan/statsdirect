using System;
using System.IO;
using System.Windows.Forms;
using System.Collections.Specialized;
using System.Xml;
using System.Collections.Generic;

namespace StatsDirect.UI
{
    internal partial class frmSetupTools : Form
    {
        /// <summary>
        /// Checking add-in status is very expensive, so we cache it.
        /// </summary>
        private bool addInWasEnabledAtLoad;

        private ISdApplication SdApplication { get; }
        private IUiPreferences UiPreferences { get; }

        public frmSetupTools(ISdApplication sdApplication, IUiPreferences uiPreferences)
        {
            SdApplication = sdApplication;
            UiPreferences = uiPreferences;
            InitializeComponent();
            LoadData();
        }

        private void cmdCancel_Click(object? sender, EventArgs e)
        {
            Close();
        }

        private void cmdOK_Click(object? sender, EventArgs e)
        {
            try
            {
                SaveData();
            }
            catch (Exception ex)
            {
                SdApplication.FriendlyError("Couldn't save tool data", ex, false);
            }
            Close();
        }

        private void LoadData()
        {
            foreach (ToolDescriptor toolDescriptor in UiPreferences.Tools)
                grid.Rows.Add(toolDescriptor.Label, toolDescriptor.Program);
        }

        private void DefaultData()
        {
            grid.Rows.Clear();
            foreach (ToolDescriptor toolDescriptor in UiPreferences.Tools)
                grid.Rows.Add(toolDescriptor.Label, toolDescriptor.Program);
        }

        private void SaveData()
        {
            List<ToolDescriptor> tools = new();
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.Cells[0].Value is not null && row.Cells[1].Value is not null)
                {
                    string label = (string)row.Cells[0].Value;
                    string program = (string)row.Cells[1].Value;
                    if (label.Length > 0 && program.Length > 0)
                        tools.Add(new ToolDescriptor(label, program));
                }
            }
            UiPreferences.Tools = tools;
            UiPreferences.Save();

            // Get rid of any old add-in that might still be hanging around
            ExcelAddInManager.UninstallOldAddIn();
            if (addInWasEnabledAtLoad && rdoExcelOff.Checked)
                ExcelAddInManager.UninstallAddIn();
            else if (rdoExcelOn.Checked && !addInWasEnabledAtLoad)
                ExcelAddInManager.InstallAddIn();
        }

        private void frmSetupTools_Load(object? sender, EventArgs e)
        {
            addInWasEnabledAtLoad = ExcelAddInManager.IsAddInInstalled();
            rdoExcelOn.Checked = addInWasEnabledAtLoad;
            rdoExcelOff.Checked = !addInWasEnabledAtLoad;
        }

        private void cmdDefaultTools_Click(object? sender, EventArgs e)
        {
            try
            {
                DefaultData();
            }
            catch (Exception ex)
            {
                SdApplication.FriendlyError("Couldn't reset tool list to defaults", ex, false);
            }
        }
    }
}
