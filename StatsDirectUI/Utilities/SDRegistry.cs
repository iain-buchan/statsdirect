using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
        /// <param name="valueName"></param>
        /// <param name="value"></param>
        /// <returns>true if the save succeeded, false if not</returns>
        internal static bool SaveSetting(string app, string key, string valueName, string value)
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

                                    keyKey.SetValue(valueName, value, RegistryValueKind.String);
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
        /// <param name="valueName"></param>
        /// <param name="getFromMachine"></param>
        /// <returns></returns>
        [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
        internal static string GetStringSetting(string app, string key, string valueName, bool getFromMachine)
        {
            try
            {
                using (RegistryKey root = getFromMachine ? Registry.LocalMachine : Registry.CurrentUser,
                    softwareKey = root.OpenSubKey("Software"),
                    vbKey = softwareKey.OpenSubKey("VB and VBA Program Settings"),
                    appKey = vbKey.OpenSubKey(app),
                    keyKey = appKey.OpenSubKey(key))
                {
                    object val = keyKey.GetValue(valueName);
                    return (string)val;
                }
            }
            catch (Exception)
            {
                return null;
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
        /// <param name="valueName"></param>
        /// <param name="getFromMachine"></param>
        /// <returns></returns>
        [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
        internal static int GetDwordSetting(string app, string key, string valueName, bool getFromMachine)
        {
            try
            {
                using (RegistryKey root = getFromMachine ? Registry.LocalMachine : Registry.CurrentUser,
                    softwareKey = root.OpenSubKey("Software"),
                    vbKey = softwareKey.OpenSubKey("VB and VBA Program Settings"),
                    appKey = vbKey.OpenSubKey(app),
                    keyKey = appKey.OpenSubKey(key))
                {
                    object val = keyKey.GetValue(valueName);
                    return (int)val;
                }
            }
            catch (Exception)
            {
                return int.MinValue;
            }
        }

    }
}
