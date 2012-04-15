using System;
using System.Collections.Generic;
using Microsoft.Win32;

namespace StatsDirect.Utilities
{
    public class SDRegistry
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="app"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="newValue"></param>
        /// <returns>true if the save succeeded, false if not</returns>
        internal static bool SaveSetting(string app, string key, string value, string newValue)
        {
            try
            {
                using (RegistryKey hkcu = Registry.CurrentUser)
                {
                    using (RegistryKey softwareKey = hkcu.OpenSubKey("Software", true))
                    {
                        if (null == softwareKey)
                            return false;

                        // Create VB/VBA key if not present
                        if (!new List<string>(softwareKey.GetSubKeyNames()).Contains("VB and VBA Program Settings"))
                        {
                            RegistryKey scrap = softwareKey.CreateSubKey("VB and VBA Program Settings");
                            if (null == scrap)
                                return false;
                            scrap.Close();
                        }
                        using (RegistryKey vbKey = softwareKey.OpenSubKey("VB and VBA Program Settings", true))
                        {
                            if (null == vbKey)
                                return false;

                            // We may or may not have our own subkey now
                            if (!new List<string>(vbKey.GetSubKeyNames()).Contains(app))
                            {
                                RegistryKey scrap = vbKey.CreateSubKey(app);
                                if (null == scrap)
                                    return false;
                                scrap.Close();
                            }
                            using (RegistryKey appKey = vbKey.OpenSubKey(app, true))
                            {
                                if (null == appKey)
                                    return false;

                                if (!new List<string>(appKey.GetSubKeyNames()).Contains(key))
                                {
                                    RegistryKey scrap = appKey.CreateSubKey(key);
                                    if (null == scrap)
                                        return false;
                                    scrap.Close();
                                }
                                using (RegistryKey keyKey = appKey.OpenSubKey(key, true))
                                {
                                    if (null == keyKey)
                                        return false;

                                    keyKey.SetValue(value, newValue, RegistryValueKind.String);
                                }
                                return true;
                            }
                        }
                    }
                }
            }
            catch (Exception /* ex */)
            {
                // SDApplication.SoleInstance.msgbox_x("Couldn't write registry key.", MessageBoxButtons.OK, MessageBoxIcon.Error, "StatsDirect", false);
                return false;
            }
        }

        internal static string GetSetting(string app, string key, string value)
        {
            try
            {
                using (RegistryKey hkcu = Registry.CurrentUser)
                {
                    using (RegistryKey softwareKey = hkcu.OpenSubKey("Software"))
                    {
                        if (null == softwareKey)
                            return null;
                        using (RegistryKey vbKey = softwareKey.OpenSubKey("VB and VBA Program Settings"))
                        {
                            if (null == vbKey)
                                return null;
                            using (RegistryKey appKey = vbKey.OpenSubKey(app))
                            {
                                if (null == appKey)
                                    return null;
                                using (RegistryKey keyKey = appKey.OpenSubKey(key))
                                {
                                    if (null == keyKey)
                                        return null;
                                    object val = keyKey.GetValue(value);
                                    return (string)val;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception /* ex */)
            {
                try
                {
                    // Error when using HKCU; probably not found.  Is it in HKLM?
                    using (RegistryKey hklm = Registry.LocalMachine)
                    {
                        using (RegistryKey softwareKey = hklm.OpenSubKey("Software"))
                        {
                            if (null == softwareKey)
                                return null;
                            using (RegistryKey vbKey = softwareKey.OpenSubKey("VB and VBA Program Settings"))
                            {
                                if (null == vbKey)
                                    return null;
                                using (RegistryKey appKey = vbKey.OpenSubKey(app))
                                {
                                    if (null == appKey)
                                        return null;
                                    using (RegistryKey keyKey = appKey.OpenSubKey(key))
                                    {
                                        if (null == keyKey)
                                            return null;
                                        object val = keyKey.GetValue(value);
                                        return (string)val;
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception /* exInner */)
                {
                    return null;
                }
            }
        }

    }
}
