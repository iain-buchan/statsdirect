using System;
using System.IO;
using System.Windows.Forms;
using System.Collections.Specialized;
using System.Xml;

namespace StatsDirect.UI
{
    public partial class frmSetupTools : Form
    {
        /// <summary>
        /// Checking add-in status is very expensive, so we cache it.
        /// </summary>
        private bool addInWasEnabledAtLoad;

        public frmSetupTools()
        {
            InitializeComponent();
            LoadData();
        }

        private void cmdCancel_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void cmdOK_Click(object sender, EventArgs e)
        {
            try
            {
                SaveData();
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Couldn't save tool data", ex, false);
            }
            Close();
        }

        private void LoadData()
        {
            StringCollection names = Properties.Settings.Default.ToolsNames;
            StringCollection paths = Properties.Settings.Default.ToolsPrograms;
            for (int i = 0; i < names.Count; i++)
                grid.Rows.Add(names[i], paths[i]);
        }

        private void DefaultData()
        {
            string xNames = (string)Properties.Settings.Default.Properties["ToolsNames"].DefaultValue;
            string xPaths = (string)Properties.Settings.Default.Properties["ToolsPrograms"].DefaultValue;
            StringCollection names = ParseXmlToStringCollection(xNames);
            StringCollection paths = ParseXmlToStringCollection(xPaths);
            grid.Rows.Clear();
            for (int i = 0; i < names.Count; i++)
                grid.Rows.Add(names[i], paths[i]);
        }

        private static StringCollection ParseXmlToStringCollection(string rawXml)
        {
            XmlDocument doc = new();
            using (StringReader sr = new(rawXml))
            {
                doc.Load(sr);
            }
            XmlNodeList elements = doc.GetElementsByTagName("string");
            StringCollection coll = new();
            foreach (XmlNode element in elements)
                coll.Add(element.InnerText);
            return coll;
        }

        private void SaveData()
        {
            StringCollection names = new();
            StringCollection paths = new();
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (null != row.Cells[0].Value && null != row.Cells[1].Value)
                {
                    string name = (string)row.Cells[0].Value;
                    string path = (string)row.Cells[1].Value;
                    if (name.Length > 0 && path.Length > 0)
                    {
                        names.Add(name);
                        paths.Add(path);
                    }
                }
            }
            Properties.Settings.Default.ToolsNames = names;
            Properties.Settings.Default.ToolsPrograms = paths;
            Properties.Settings.Default.Save();

            // Get rid of any old add-in that might still be hanging around
            ExcelAddInManager.UninstallOldAddIn();
            if (addInWasEnabledAtLoad && rdoExcelOff.Checked)
                ExcelAddInManager.UninstallAddIn();
            else if (rdoExcelOn.Checked && !addInWasEnabledAtLoad)
                ExcelAddInManager.InstallAddIn();
        }

        private void frmSetupTools_Load(object sender, EventArgs e)
        {
            addInWasEnabledAtLoad = ExcelAddInManager.IsAddInInstalled();
            rdoExcelOn.Checked = addInWasEnabledAtLoad;
            rdoExcelOff.Checked = !addInWasEnabledAtLoad;
        }

        private void cmdDefaultTools_Click(object sender, EventArgs e)
        {
            try
            {
                DefaultData();
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Couldn't reset tool list to defaults", ex, false);
            }
        }
    }
}
