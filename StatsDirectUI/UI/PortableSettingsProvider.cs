using System;
using System.Collections;
using System.Configuration;
using System.Windows.Forms;
using System.Collections.Specialized;
using System.Xml;
using System.IO;

using StatsDirect.Configuration;

namespace StatsDirect.UI
{
    public class PortableSettingsProvider : SettingsProvider
    {
        const string SETTINGSROOT = "Settings"; // XML Root Node

        public override void Initialize(string name, NameValueCollection col)
        {
            base.Initialize(ApplicationName, col);
        }

        public override string ApplicationName
        {
            get
            {
                if (Application.ProductName.Trim().Length > 0)
                {
                    return Application.ProductName;
                }
                FileInfo fi = new FileInfo(Application.ExecutablePath);
                return fi.Name.Substring(0, fi.Name.Length - fi.Extension.Length);
            }
            set
            {
                // Do nothing
            }
        }

        protected virtual string GetAppSettingsPath()
        {
            // Used to determine where to store the settings
            FileInfo fi = new FileInfo(Application.ExecutablePath);
            return fi.DirectoryName;
        }

        protected virtual string GetAppSettingsFilename()
        {
            // Used to determine the filename to store the settings
            return ApplicationName + ".settings";
        }

        protected string SettingsFileName
        {
            get { return Path.Combine(SDConfiguration.MyStatsDirectFolder, GetAppSettingsFilename()); }
            // get { return Path.Combine(GetAppSettingsPath(), GetAppSettingsFilename()); }
        }

        public override void SetPropertyValues(SettingsContext context, SettingsPropertyValueCollection propvals)
        {
            // Iterate through the settings to be stored
            // Only dirty settings are included in propvals, and only ones relevant to this provider
            foreach (SettingsPropertyValue propval in propvals)
                SetValue(propval);

            try
            {
                SettingsXML.Save(SettingsFileName);
            }
            catch (Exception)
            {
                // Ignore if can't save, device been ejected
            }
        }

        public override SettingsPropertyValueCollection GetPropertyValues(SettingsContext context, SettingsPropertyCollection props)
        {
            // Create new collection of values
            SettingsPropertyValueCollection values = new SettingsPropertyValueCollection();

            // Iterate through the settings to be retrieved
            foreach (SettingsProperty setting in props)
            {
                bool usingDefault;
                string sv = GetValue(setting, out usingDefault);
                SettingsPropertyValue value = new SettingsPropertyValue(setting) {IsDirty = false, SerializedValue = sv};
                values.Add(value);
            }
            return values;
        }

        private XmlDocument m_SettingsXML;

        private XmlDocument SettingsXML
        {
            get
            {
                // If we don't hold an xml document, try opening one.  
                // If it doesnt exist then create a new one ready.
                if (null == m_SettingsXML)
                {
                    m_SettingsXML = new XmlDocument();

                    try
                    {
                        m_SettingsXML.Load(SettingsFileName);
                    }
                    catch (Exception)
                    {
                        // Create new document
                        XmlDeclaration dec = m_SettingsXML.CreateXmlDeclaration("1.0", "utf-8", string.Empty);
                        m_SettingsXML.AppendChild(dec);

                        XmlNode nodeRoot = m_SettingsXML.CreateNode(XmlNodeType.Element, SETTINGSROOT, "");
                        m_SettingsXML.AppendChild(nodeRoot);
                    }
                }

                return m_SettingsXML;
            }
        }

        private string GetValue(SettingsProperty setting, out bool usingDefault)
        {
            try
            {
                usingDefault = false;
                if (IsRoaming(setting))
                    return SettingsXML.SelectSingleNode(SETTINGSROOT + "/" + setting.Name).InnerText;
                return SettingsXML.SelectSingleNode(SETTINGSROOT + "/" + SystemInformation.ComputerName + "/" + setting.Name).InnerText;
            }
            catch (Exception)
            {
                usingDefault = true;
                return null != setting.DefaultValue ? setting.DefaultValue.ToString() : null;
            }
        }

        private void SetValue(SettingsPropertyValue propVal)
        {
            if (null == propVal.SerializedValue)
                return;

            XmlElement SettingNode;

            // Determine if the setting is roaming.
            // If roaming then the value is stored as an element under the root
            // Otherwise it is stored under a machine name node 
            try
            {
                if (IsRoaming(propVal.Property))
                    SettingNode = (XmlElement)(SettingsXML.SelectSingleNode(SETTINGSROOT + "/" + propVal.Name));
                else
                    SettingNode = (XmlElement)(SettingsXML.SelectSingleNode(SETTINGSROOT + "/" + SystemInformation.ComputerName + "/" + propVal.Name));
            }
            catch (Exception)
            {
                SettingNode = null;
            }

            // Check to see if the node exists, if so then set its new value
            if (null != SettingNode)
            {
                SettingNode.InnerText = propVal.SerializedValue.ToString();
            }
            else
            {
                if (IsRoaming(propVal.Property))
                {
                    // Store the value as an element of the Settings Root Node
                    SettingNode = SettingsXML.CreateElement(propVal.Name);
                    SettingNode.InnerText = propVal.SerializedValue.ToString();
                    SettingsXML.SelectSingleNode(SETTINGSROOT).AppendChild(SettingNode);
                }
                else
                {
                    // Its machine specific, store as an element of the machine name node,
                    // creating a new machine name node if one doesnt exist.
                    XmlElement MachineNode;
                    try
                    {
                        MachineNode = (XmlElement)(SettingsXML.SelectSingleNode(SETTINGSROOT + "/" + SystemInformation.ComputerName));
                    }
                    catch (Exception)
                    {
                        MachineNode = SettingsXML.CreateElement(SystemInformation.ComputerName);
                        SettingsXML.SelectSingleNode(SETTINGSROOT).AppendChild(MachineNode);
                    }

                    if (null == MachineNode)
                    {
                        MachineNode = SettingsXML.CreateElement(SystemInformation.ComputerName);
                        SettingsXML.SelectSingleNode(SETTINGSROOT).AppendChild(MachineNode);
                    }

                    SettingNode = SettingsXML.CreateElement(propVal.Name);
                    SettingNode.InnerText = propVal.SerializedValue.ToString();
                    MachineNode.AppendChild(SettingNode);
                }
            }
        }

        private bool IsRoaming(SettingsProperty prop)
        {
            // Determine if the setting is marked as Roaming
            foreach (DictionaryEntry d in prop.Attributes)
            {
                Attribute a = (Attribute)d.Value;
                if (a is SettingsManageabilityAttribute)
                {
                    SettingsManageabilityAttribute sma = (SettingsManageabilityAttribute)a;
                    return sma.Manageability == SettingsManageability.Roaming;
                }
            }
            return false;
        }
    }
}
