using System;
using System.Collections;
using System.Configuration;
using System.Windows.Forms;
using System.Collections.Specialized;
using System.Xml;
using System.IO;

using StatsDirect.Configuration;
using System.Xml.XPath;

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
                if (!string.IsNullOrWhiteSpace(Application.ProductName))
                    return Application.ProductName;
                FileInfo fi = new(Application.ExecutablePath);
                return fi.Name.Substring(0, fi.Name.Length - fi.Extension.Length);
            }
            set
            {
                // Do nothing
            }
        }

        protected virtual string GetAppSettingsFilename()
        {
            // Used to determine the filename to store the settings
            return ApplicationName + ".settings";
        }

        protected string SettingsFileName => Path.Combine(SDConfiguration.MyStatsDirectFolder, GetAppSettingsFilename());

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
            SettingsPropertyValueCollection values = new();

            // Iterate through the settings to be retrieved
            foreach (SettingsProperty setting in props)
            {
                string sv = GetValue(setting, out bool _);
                SettingsPropertyValue value = new(setting) {IsDirty = false, SerializedValue = sv};
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

                        XmlNode nodeRoot = m_SettingsXML.CreateNode(XmlNodeType.Element, SETTINGSROOT, string.Empty);
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
                return setting.DefaultValue?.ToString();
            }
        }

        private void SetValue(SettingsPropertyValue propVal)
        {
            if (null == propVal?.SerializedValue)
                return;
            if (null == propVal.Name)
                return;

            XmlElement settingNode;

            // Determine if the setting is roaming.
            // If roaming then the value is stored as an element under the root
            // Otherwise it is stored under a machine name node 
            try
            {
                if (IsRoaming(propVal.Property))
                    settingNode = (XmlElement)SettingsXML.SelectSingleNode(SETTINGSROOT + "/" + propVal.Name);
                else
                    settingNode = (XmlElement)SettingsXML.SelectSingleNode(SETTINGSROOT + "/" + SystemInformation.ComputerName + "/" + propVal.Name);
            }
            catch (XPathException)
            {
                settingNode = null;
            }

            // Check to see if the node exists, if so then set its new value
            if (null != settingNode)
            {
                settingNode.InnerText = propVal.SerializedValue.ToString();
            }
            else
            {
                XmlNode parentNode;

                if (IsRoaming(propVal.Property))
                {
                    // Store the value as an element of the Settings Root Node
                    parentNode = SettingsXML.SelectSingleNode(SETTINGSROOT);
                }
                else
                {
                    // It's machine specific, store as an element of the machine name node,
                    // creating a new machine name node if one doesnt exist.
                    XmlElement machineNode;
                    try
                    {
                        machineNode = (XmlElement)SettingsXML.SelectSingleNode(SETTINGSROOT + "/" + SystemInformation.ComputerName);
                    }
                    catch (XPathException)
                    {
                        machineNode = null;
                    }

                    if (null == machineNode)
                    {
                        machineNode = SettingsXML.CreateElement(SystemInformation.ComputerName);
                        SettingsXML.SelectSingleNode(SETTINGSROOT).AppendChild(machineNode);
                    }

                    parentNode = machineNode;
                }

                settingNode = SettingsXML.CreateElement(propVal.Name);
                settingNode.InnerText = propVal.SerializedValue.ToString();
                parentNode?.AppendChild(settingNode);
            }
        }

        private static bool IsRoaming(SettingsProperty prop)
        {
            // Determine if the setting is marked as Roaming
            foreach (DictionaryEntry d in prop.Attributes)
            {
                Attribute a = (Attribute)d.Value;
                if (a is SettingsManageabilityAttribute sma)
                    return sma.Manageability == SettingsManageability.Roaming;
            }
            return false;
        }
    }
}
