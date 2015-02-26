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

        /// <summary>
        /// Emulates the old VB6 GetSetting call, right down to looking in \Software\VB and VBA Program Settings.
        /// First try to look in HKCU; if that fails, look in HKLM; if that also fails, the setting can't be there.
        /// This makes no attempt to return early if it can't find a subkey in HKCU, as there's a nasty edge case when some settings are in HKCU and some in HKLM.
        /// Instead, it triggers the exception in the registry code and tries HKLM instead.
        /// </summary>
        /// <param name="app"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        internal static string GetSetting(string app, string key, string value)
        {
            try
            {
                using (RegistryKey hkcu = Registry.CurrentUser)
                {
                    using (RegistryKey softwareKey = hkcu.OpenSubKey("Software"))
                    {
                        using (RegistryKey vbKey = softwareKey.OpenSubKey("VB and VBA Program Settings"))
                        {
                            using (RegistryKey appKey = vbKey.OpenSubKey(app))
                            {
                                using (RegistryKey keyKey = appKey.OpenSubKey(key))
                                {
                                    object val = keyKey.GetValue(value);
                                    return (string)val;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                try
                {
                    // Error when using HKCU; probably not found.  Is it in HKLM?
                    using (RegistryKey hklm = Registry.LocalMachine)
                    {
                        using (RegistryKey softwareKey = hklm.OpenSubKey("Software"))
                        {
                            using (RegistryKey vbKey = softwareKey.OpenSubKey("VB and VBA Program Settings"))
                            {
                                using (RegistryKey appKey = vbKey.OpenSubKey(app))
                                {
                                    using (RegistryKey keyKey = appKey.OpenSubKey(key))
                                    {
                                        object val = keyKey.GetValue(value);
                                        return (string)val;
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    // Can't find in either location.
                    return null;
                }
            }
        }

    }
}
