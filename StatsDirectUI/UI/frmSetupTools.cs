using System;
using System.IO;
using System.Windows.Forms;
using System.Collections.Specialized;
using System.Xml;
using System.Collections.Generic;
using StatsDirect.UI.Properties;

namespace StatsDirect.UI
{
    public partial class frmSetupTools : Form
    {
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
            IReadOnlyList<string> names = Properties.Settings.Default.ToolsNames;
            IReadOnlyList<string> paths = Properties.Settings.Default.ToolsPrograms;
            for (int i = 0; i < names.Count; i++)
                grid.Rows.Add(names[i], paths[i]);
        }

        private void DefaultData()
        {
            Settings installationDefaults = Settings.InstallationDefaults;
            grid.Rows.Clear();
            for (int i = 0; i < installationDefaults.ToolsNames.Count; i++)
                grid.Rows.Add(installationDefaults.ToolsNames[i], installationDefaults.ToolsPrograms[i]);
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
            List<string> names = new();
            List<string> paths = new();
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
