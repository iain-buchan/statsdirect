using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace StatsDirect.Setup
{
    /// <summary>
    /// Retires the Excel link add-in (a StatsDirect menu in Excel that sent a copy of the open workbook to StatsDirect), judged by its file names only:
    /// its copies in Excel's start-up and add-in folders, its start-up registrations and its entry in Excel's add-in list, and StatsDirect's own settings
    /// for it.  Never starts or closes Excel, and touches nothing else of Excel's.  Shared by the installer (every local user profile whose registry is
    /// loaded, and the shared Office folders, as the system account) and StatsDirect at start-up (the current user, which also covers the profiles the
    /// installer could not open and a redirected AppData).  Nothing records completion: a file locked during an upgrade is retried at a later start-up,
    /// and a start-up with nothing to retire, the usual case, changes and logs nothing.
    /// </summary>
    internal static class ExcelAddInRetirer
    {
        internal static readonly string[] FileNames =
        {
            "StatsDirectExcelLink.xla", "StatsDirectExcelLink.xlam",
            "StatsDirect3ExcelLink.xla", "StatsDirect3ExcelLink.xlam",
            "StatsDirect4ExcelLink.xla", "StatsDirect4ExcelLink.xlam"
        };

        /// <summary>StatsDirect's own settings for the add-in (where it found StatsDirect.exe and the help), written at every start-up by StatsDirect 3 to 5.0.8.</summary>
        private const string SettingsParent = @"Software\VB and VBA Program Settings";
        private static readonly string[] SettingsKeys = { "ExcelStatsDirect3Link", "ExcelStatsDirect4Link" };

        private static readonly Regex OpenName = new(@"^OPEN(?<number>[0-9]{1,6})?$", RegexOptions.IgnoreCase);
        private static readonly Regex OpenCommand = new(@"^\s*(?:/R\s+)?(?:""(?<path>[^""]+)""|(?<path>[^""]+?))\s*$", RegexOptions.IgnoreCase);

        internal static void RunCurrentUser()
        {
            Run(false, null, log => RetireUser(Registry.CurrentUser,
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), false, ExcelIsRunning, log));
        }

        internal static void RunMachine(bool whatIf, Action<string> echo)
        {
            Run(whatIf, echo, log =>
            {
                if (ExcelIsRunning())
                {
                    log("Excel is running; add-in retirement deferred until a later StatsDirect launch with Excel closed.");
                    return;
                }

                // Do not load other users' registry hives. Their own StatsDirect launch handles them, including
                // roaming/redirected AppData. Do not remove an offline user's files while their startup registrations
                // cannot be cleaned: Excel could otherwise report a missing add-in before StatsDirect is next run.
                using RegistryKey machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                using RegistryKey profiles = machine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList");
                if (profiles != null)
                    foreach (string sid in profiles.GetSubKeyNames())
                    {
                        // Exclude service accounts; include domain/local and Azure AD user profiles.
                        if (!sid.StartsWith("S-1-5-21-", StringComparison.Ordinal) && !sid.StartsWith("S-1-12-1-", StringComparison.Ordinal))
                            continue;
                        Attempt(() =>
                        {
                            using RegistryKey profile = profiles.OpenSubKey(sid);
                            string path = profile?.GetValue("ProfileImagePath") as string;
                            if (string.IsNullOrWhiteSpace(path))
                                return;
                            path = Environment.ExpandEnvironmentVariables(path);
                            if (!SafeLocalPath(path))
                                return;
                            using RegistryKey user = Registry.Users.OpenSubKey(sid, !whatIf);
                            RetireUser(user, Path.Combine(path, @"AppData\Roaming"), whatIf, ExcelIsRunning, log,
                                expandUserPaths: false);
                        }, log);
                    }

                // Older manual installations could use Excel's shared Library or XLSTART directory.
                foreach (string programFiles in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) }.Distinct(StringComparer.OrdinalIgnoreCase))
                    if (!string.IsNullOrEmpty(programFiles))
                        for (int version = 9; version <= 16; version++)
                            foreach (string prefix in new[] { "", "root" })
                                foreach (string leaf in new[] { "XLSTART", "Library" })
                                    RetireDirectory(Path.Combine(programFiles, "Microsoft Office", prefix, "Office" + version, leaf), whatIf, log);
            });
        }

        private static void Run(bool whatIf, Action<string> echo, Action<Action<string>> retire)
        {
            void Log(string message)
            {
                echo?.Invoke(message);
                if (!whatIf)
                    try
                    {
                        File.AppendAllText(Path.Combine(Path.GetTempPath(), "StatsDirect-retire-excel-addin.log"),
                            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine);
                    }
                    catch (Exception) { /* A diagnostic only; retirement must not prevent startup or installation. */ }
            }
            Attempt(() => retire(Log), Log);
        }

        private static bool ExcelIsRunning()
        {
            try
            {
                Process[] processes = Process.GetProcessesByName("EXCEL");
                bool running = processes.Length != 0;
                foreach (Process process in processes)
                    process.Dispose();
                return running;
            }
            catch (Exception)
            {
                return true; // If the check is unavailable, leave Excel alone.
            }
        }

        // The registry root and AppData path can be stand-ins in regression tests. Only named add-ins are removed.
        internal static void RetireUser(RegistryKey user, string appData, bool whatIf, Func<bool> excelIsRunning,
            Action<string> log, bool expandUserPaths = true)
        {
            if (user == null)
            {
                log("User registry is not loaded; add-in retirement deferred until that user starts StatsDirect.");
                return;
            }

            // Nothing to retire is the usual case at every start-up: find that out without changing or logging anything.
            bool anything = false;
            Retire(user, appData, true, _ => anything = true, expandUserPaths);
            if (!anything)
                return;

            if (excelIsRunning())
            {
                log("Excel is running; add-in retirement deferred.");
                return;
            }

            Retire(user, appData, whatIf, log, expandUserPaths);
        }

        private static void Retire(RegistryKey user, string appData, bool whatIf, Action<string> log, bool expandUserPaths)
        {
            var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                Path.Combine(appData, @"Microsoft\Excel\XLSTART"), Path.Combine(appData, @"Microsoft\AddIns")
            };
            bool registrationsCleaned = true;

            foreach (string officePath in new[] { @"Software\Microsoft\Office", @"Software\Wow6432Node\Microsoft\Office" })
            {
                bool officeCleaned = Attempt(() =>
                {
                    using RegistryKey office = user.OpenSubKey(officePath, !whatIf);
                    if (office == null)
                        return;
                    foreach (string version in office.GetSubKeyNames())
                    {
                        if (!Regex.IsMatch(version, @"^\d+\.\d+$"))
                            continue;
                        registrationsCleaned &= Attempt(() =>
                        {
                            using RegistryKey options = office.OpenSubKey(version + @"\Excel\Options", !whatIf);
                            if (options != null)
                            {
                                // Excel's optional alternate startup folder is also automatically loaded.
                                string alternate = options.GetValue("AltStartup", null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
                                if (!string.IsNullOrWhiteSpace(alternate))
                                {
                                    if (expandUserPaths)
                                        alternate = Environment.ExpandEnvironmentVariables(alternate);
                                    directories.Add(alternate);
                                }
                                RetireOpenValues(options, whatIf, log);
                            }
                            using RegistryKey manager = office.OpenSubKey(version + @"\Excel\Add-in Manager", !whatIf);
                            if (manager != null)
                                foreach (string name in manager.GetValueNames())
                                    if (IsAddInPath(name))
                                    {
                                        log((whatIf ? "Would unregister " : "Unregistering ") + name);
                                        if (!whatIf)
                                            manager.DeleteValue(name, false);
                                    }
                        }, log);
                    }
                }, log);
                registrationsCleaned &= officeCleaned;
            }

            if (registrationsCleaned)
                foreach (string directory in directories)
                    RetireDirectory(directory, whatIf, log);
            else
                log("Add-in files left in place because some startup registrations could not be checked.");

            // StatsDirect's own settings for the add-in: nothing else reads them, and nothing writes them any more.
            Attempt(() =>
            {
                using RegistryKey parent = user.OpenSubKey(SettingsParent, !whatIf);
                if (parent == null)
                    return;
                foreach (string name in SettingsKeys)
                {
                    bool present;
                    using (RegistryKey settings = parent.OpenSubKey(name))
                        present = settings != null;
                    if (!present)
                        continue;
                    log((whatIf ? "Would remove " : "Removing ") + "StatsDirect's settings for the add-in, " + parent.Name + @"\" + name);
                    if (!whatIf)
                        parent.DeleteSubKeyTree(name, false);
                }
            }, log);
        }

        internal static bool IsAddInPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;
            string fileName = Path.GetFileName(path.Trim());
            return FileNames.Contains(fileName, StringComparer.OrdinalIgnoreCase);
        }

        internal static bool IsAddInCommand(string command)
        {
            if (command == null)
                return false;
            Match match = OpenCommand.Match(command);
            return match.Success && IsAddInPath(match.Groups["path"].Value);
        }

        private static void RetireOpenValues(RegistryKey options, bool whatIf, Action<string> log)
        {
            var original = options.GetValueNames().Where(name => OpenName.IsMatch(name))
                .OrderBy(name => name.Length == 4 ? -1 : int.Parse(name.Substring(4)))
                .Select(name => (Name: name, Value: options.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames),
                    Kind: options.GetValueKind(name))).ToList();
            var kept = original.Where(item => !IsAddInCommand(item.Value as string)).ToList();
            if (kept.Count == original.Count)
                return;
            log((whatIf ? "Would remove " : "Removing ") + (original.Count - kept.Count) + " StatsDirect Excel startup registration(s) from " + options.Name);
            if (whatIf)
                return;

            // Excel reads consecutive OPEN, OPEN1, ... entries. Write the survivors before removing surplus
            // values, preserving their data and types. Restore the snapshot if an individual write fails.
            string Name(int index) => index == 0 ? "OPEN" : "OPEN" + index;
            try
            {
                for (int i = 0; i < kept.Count; i++)
                    options.SetValue(Name(i), kept[i].Value, kept[i].Kind);
                var newNames = Enumerable.Range(0, kept.Count).Select(Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var item in original)
                    if (!newNames.Contains(item.Name))
                        options.DeleteValue(item.Name, false);
            }
            catch
            {
                foreach (var item in original)
                    options.SetValue(item.Name, item.Value, item.Kind);
                var oldNames = original.Select(item => item.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < kept.Count; i++)
                    if (!oldNames.Contains(Name(i)))
                        options.DeleteValue(Name(i), false);
                throw;
            }
        }

        internal static bool SafeLocalPath(string path)
        {
            // Do not follow a user-controlled junction/symlink from the elevated installer, or contact a network share.
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || path.StartsWith(@"\\", StringComparison.Ordinal))
                return false;
            try
            {
                string full = Path.GetFullPath(path);
                for (string part = full; !string.IsNullOrEmpty(part); part = Path.GetDirectoryName(part))
                    if ((File.Exists(part) || Directory.Exists(part)) && (File.GetAttributes(part) & FileAttributes.ReparsePoint) != 0)
                        return false;
                return true;
            }
            catch (Exception) { return false; }
        }

        internal static void RetireDirectory(string directory, bool whatIf, Action<string> log)
        {
            if (!SafeLocalPath(directory) || !Directory.Exists(directory))
                return;
            foreach (string fileName in FileNames)
            {
                string path = Path.Combine(directory, fileName);
                Attempt(() =>
                {
                    if (!SafeLocalPath(path) || !File.Exists(path))
                        return;
                    log((whatIf ? "Would remove " : "Removing ") + path);
                    if (!whatIf)
                        File.Delete(path);
                }, log);
            }
        }

        private static bool Attempt(Action action, Action<string> log)
        {
            try { action(); return true; }
            catch (Exception ex) { log("Retirement deferred: " + ex.Message); return false; }
        }
    }
}
