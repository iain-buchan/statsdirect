using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace StatsDirect.Setup
{
    /// <summary>
    /// Retires the setup registrations of StatsDirect 4 that cannot remove themselves, or must not be allowed to.  The installer runs this, as the system account,
    /// once the new program is in place (custom action RetireOldSetups in SDInstaller).
    /// </summary>
    /// <remarks>
    /// When a StatsDirect setup (a WiX Burn bundle) replaces an older one, the installer removes the old program and Burn then runs the old setup, from the package
    /// cache, to uninstall and unregister itself.  The version 4 setups treat the .NET runtime as something they uninstall, and need their own copy of Microsoft's
    /// runtime installer to do it.  That copy is kept in a package cache folder named after the file's hash, so every 4.0.0 to 4.0.4 setup shared one folder, and the
    /// first upgrade between them deleted it.  From then on the surviving setup fails ("Failed to resolve source"), so it stays in Windows' list of installed apps
    /// beside the new version, and Uninstall there asks for the original StatsDirectSetup.exe.  Nothing in a new setup can make the old one succeed; what it can do
    /// is what the old setup would have done for itself, apart from uninstalling the runtime: delete its cached files and its registration.  Burn then finds no old
    /// setup to run, which it treats as a failure of something optional and carries on.
    ///
    /// A version 4 setup that still has its runtime installer is left to Burn as before, with one exception: a 4.0.5 build that carries the .NET 10 runtime would
    /// uninstall the runtime that version 5 has just decided is present and needs, so it is retired here too.
    ///
    /// Everything is judged from names, so that nothing is deleted that is not provably StatsDirect's: registrations carrying StatsDirect's bundle upgrade code and
    /// version 4, package cache folders named exactly by the identifiers found in them, and dependency keys named "StatsDirect".  A setup is left alone if its
    /// program is still installed, if its cached files are not where Burn normally keeps them, or if anything about its folder is unusual.  The shared runtime
    /// installers in the package cache are never deleted, because another product's setup may share them.
    ///
    /// This is a program of its own, not part of StatsDirect.exe, for the installer's sake: a console program cannot put up the .NET host's "You must install .NET"
    /// dialog, which nobody could answer where the installer runs it, and a new file name cannot turn out to be an older build that does not know the errand and
    /// starts the whole application instead (files of a running StatsDirect are only replaced at the next restart).
    /// </remarks>
    public static class OldSetupRetirer
    {
        /// <summary>The UpgradeCode of SDBootstrapper/Bundle.wxs, unchanged since version 3.</summary>
        private const string BUNDLE_UPGRADE_CODE = "{E52FA35E-33B1-45D2-B004-CFC5CC7C7EF5}";
        private const string UNINSTALL_KEY = @"Microsoft\Windows\CurrentVersion\Uninstall";
        private const string DEPENDENCIES_KEY = @"Classes\Installer\Dependencies";
        private const string PRODUCT_NAME = "StatsDirect";
        /// <summary>Microsoft's runtime installers are over 50 MB; the stub that Microsoft's own setup caches under the same file name is under 1 MB.</summary>
        private const long SMALLEST_RUNTIME_INSTALLER = 10 * 1024 * 1024;

        private const string GUID = @"\{[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}\}";
        private static readonly Regex guidPattern = new("^" + GUID + "$");
        /// <summary>Burn's dependency key for an MSI package: the product code, then (from WiX 4) "_v" and the version.  Its package cache folder is the same without the underscore.</summary>
        private static readonly Regex msiProviderPattern = new("^(" + GUID + @")(_(v[0-9]+(\.[0-9]+){1,3}))?$");

        /// <summary>
        /// Entry point for the installer.  Never throws and never shows anything: it runs as the system account during installation, where nobody could answer.
        /// </summary>
        /// <param name="whatIf">Report what would be retired, and delete nothing.</param>
        /// <param name="echo">Receives each line of the report as well as the log file, or null.</param>
        public static void Run(bool whatIf, Action<string> echo)
        {
            List<string> lines = new();
            void Log(string line)
            {
                lines.Add(line);
                echo?.Invoke(line);
            }

            try
            {
                string packageCache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Package Cache");
                List<RegistryKey> softwareKeys = new();
                try
                {
                    foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
                    {
                        using RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                        RegistryKey software = hklm.OpenSubKey("SOFTWARE", !whatIf);
                        if (null != software)
                            softwareKeys.Add(software);
                    }
                    Retire(softwareKeys, packageCache, whatIf, Log);
                }
                finally
                {
                    foreach (RegistryKey software in softwareKeys)
                        software.Dispose();
                }
            }
            catch (Exception ex)
            {
                Log("Stopped: " + ex);
            }

            try
            {
                // The system account's temporary folder is C:\Windows\SystemTemp on Windows 11 and C:\Windows\Temp before that.
                string logPath = Path.Combine(Path.GetTempPath(), "StatsDirect-retire-old-setups.log");
                File.AppendAllLines(logPath, lines.Select(line => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + line));
            }
            catch (Exception)
            {
                // The log is a courtesy.
            }
        }

        /// <summary>
        /// Retire the version 4 setups registered under the given keys.
        /// </summary>
        /// <param name="softwareKeys">HKEY_LOCAL_MACHINE\SOFTWARE, once for each registry view and writable unless <paramref name="whatIf"/> (or stand-ins with the same layout, for testing).</param>
        /// <param name="packageCacheFolder">Burn's per-machine package cache, normally C:\ProgramData\Package Cache.</param>
        /// <param name="whatIf">Decide and report, but delete nothing.</param>
        /// <param name="log">Receives one line for each decision and each deletion.</param>
        /// <returns>The identifiers of the setups retired (or, with <paramref name="whatIf"/>, that would be).</returns>
        public static IList<string> Retire(IEnumerable<RegistryKey> softwareKeys, string packageCacheFolder, bool whatIf, Action<string> log)
        {
            List<string> retired = new();
            List<RegistryKey> views = softwareKeys.ToList();
            foreach (RegistryKey software in views)
            {
                using RegistryKey uninstall = software.OpenSubKey(UNINSTALL_KEY, !whatIf);
                if (null == uninstall)
                    continue;

                foreach (string bundleId in uninstall.GetSubKeyNames())
                {
                    if (!guidPattern.IsMatch(bundleId))
                        continue;

                    // One setup that cannot be read or retired must not stop the others being dealt with.
                    try
                    {
                        if (RetireIfNecessary(bundleId, uninstall, views, packageCacheFolder, whatIf, log))
                            retired.Add(bundleId);
                    }
                    catch (Exception ex)
                    {
                        log($"Left {bundleId} as it is: {ex.Message}");
                    }
                }
            }
            if (0 == retired.Count)
                log("No old setup needed retiring.");
            return retired;
        }

        private static bool RetireIfNecessary(string bundleId, RegistryKey uninstall, IList<RegistryKey> views, string packageCacheFolder, bool whatIf, Action<string> log)
        {
            Version version;
            string registeredCachePath;
            using (RegistryKey registration = uninstall.OpenSubKey(bundleId))
            {
                if (null == registration || !IsStatsDirectSetup(registration))
                    return false;
                if (!Version.TryParse(registration.GetValue("BundleVersion") as string, out version))
                    return false;
                registeredCachePath = registration.GetValue("BundleCachePath") as string;
            }
            // Version 3 setups keep no runtime installer and remove themselves, and so do released version 5 setups, which never uninstall the runtime.  (Test builds of
            // 5.0.0 made before 18 September 2026 behave as version 4 did, but cannot be told from the released build here; they were never given out, and have to be
            // uninstalled by hand.)
            if (4 != version.Major)
                return false;

            string reason = ReasonToRetire(version, packageCacheFolder);
            if (null == reason)
            {
                log($"Setup {bundleId} (version {version}) has its runtime installer and is left to remove itself.");
                return false;
            }

            // Burn will run the setup program named in the registration.  If that is not where this code is about to delete, the registration must stay.
            string setupFolder = Path.Combine(packageCacheFolder, bundleId);
            if (!IsInFolder(registeredCachePath, setupFolder))
            {
                log($"Left setup {bundleId} (version {version}) registered: its cached program is at \"{registeredCachePath}\", not in {setupFolder}.");
                return false;
            }

            // The installer has removed every older StatsDirect by the time it runs this.  Run in any other circumstances, do not strand a program without the entry
            // from which to uninstall it.
            List<string> providers = ProvidersDependedOn(bundleId, views);
            foreach (string provider in providers)
            {
                Match msiProvider = msiProviderPattern.Match(provider);
                if (msiProvider.Success && !provider.Equals(bundleId, StringComparison.OrdinalIgnoreCase) && IsProductInstalled(msiProvider.Groups[1].Value, views))
                {
                    log($"Left setup {bundleId} (version {version}) registered: its program {msiProvider.Groups[1].Value} is still installed.");
                    return false;
                }
            }

            log($"{(whatIf ? "Would retire" : "Retiring")} setup {bundleId} (version {version}): {reason}.");
            if (whatIf)
                return true;

            // The cached setup goes first.  If its program cannot be deleted Burn will still run it, and a setup that runs and fails writes its registration again;
            // better then to leave the registration alone.  (Anything else left in the folder does no harm.)
            if (!DeleteCacheFolder(setupFolder, log) && Directory.Exists(setupFolder) && Directory.GetFiles(setupFolder, "*.exe").Length > 0)
            {
                log($"Left setup {bundleId} registered, because its cached program could not be deleted.");
                return false;
            }

            // Burn's reference counting: the setup's own provider key, and its name among the dependents of each package it installed.  HKLM\SOFTWARE\Classes is
            // one key seen from both registry views, so the second view usually finds nothing left.
            foreach (RegistryKey software in views)
            {
                foreach (string provider in providers)
                {
                    try
                    {
                        ReleaseProvider(provider, bundleId, software, packageCacheFolder, log);
                    }
                    catch (Exception ex)
                    {
                        log($"Left dependency key {provider}: {ex.Message}");
                    }
                }
            }

            // The registration goes last, so that a run cut short can be finished by another: this is what the setup is found by.
            uninstall.DeleteSubKeyTree(bundleId, false);
            log("Deleted its entry in the list of installed apps.");
            return true;
        }

        private static bool IsStatsDirectSetup(RegistryKey registration)
        {
            object upgradeCodes = registration.GetValue("BundleUpgradeCode");
            IEnumerable<string> codes = upgradeCodes as string[] ?? (upgradeCodes is string one ? new[] { one } : Array.Empty<string>());
            return codes.Any(code => BUNDLE_UPGRADE_CODE.Equals(code, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Why this setup must be retired here, or null if it can be left to uninstall itself.
        /// </summary>
        private static string ReasonToRetire(Version version, string packageCacheFolder)
        {
            // What each release carried, from SDBootstrapper/Bundle.wxs at tags 4.0.4, 4.0.5-net8 and 4.0.5-net10 and the commits for 4.0.0 to 4.0.3.
            bool before405 = version < new Version(4, 0, 5);
            if (!before405 && IsRuntimeInstallerCached(packageCacheFolder, "windowsdesktop-runtime-10.0.0-win-x64.exe"))
                return "it carries the .NET 10 runtime and would uninstall the runtime that this version needs";

            string runtimeInstaller = before405 ? "windowsdesktop-runtime-6.0.29-win-x64.exe" : "windowsdesktop-runtime-8.0.22-win-x64.exe";
            if (!IsRuntimeInstallerCached(packageCacheFolder, runtimeInstaller))
                return $"its copy of {runtimeInstaller} has gone from the package cache, so it cannot remove itself";

            return null;
        }

        private static bool IsRuntimeInstallerCached(string packageCacheFolder, string fileName)
        {
            if (!Directory.Exists(packageCacheFolder))
                return false;
            foreach (string folder in Directory.GetDirectories(packageCacheFolder))
            {
                // Folders named by a GUID belong to an installed setup or product (Microsoft's own setup stub has the same file name as its installer); payloads are in folders named by hash.
                if (Path.GetFileName(folder).StartsWith("{", StringComparison.Ordinal))
                    continue;
                FileInfo candidate = new(Path.Combine(folder, fileName));
                if (candidate.Exists && candidate.Length >= SMALLEST_RUNTIME_INSTALLER)
                    return true;
            }
            return false;
        }

        private static bool IsInFolder(string filePath, string folder)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return false;
            try
            {
                string parent = Path.GetDirectoryName(Path.GetFullPath(filePath));
                return string.Equals(parent, Path.GetFullPath(folder), StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// The dependency keys that concern this setup: its own, and every one that lists it as a dependent.
        /// </summary>
        private static List<string> ProvidersDependedOn(string bundleId, IList<RegistryKey> views)
        {
            HashSet<string> providers = new(StringComparer.OrdinalIgnoreCase);
            foreach (RegistryKey software in views)
            {
                using RegistryKey dependencies = software.OpenSubKey(DEPENDENCIES_KEY);
                if (null == dependencies)
                    continue;
                foreach (string providerName in dependencies.GetSubKeyNames())
                {
                    if (providerName.Equals(bundleId, StringComparison.OrdinalIgnoreCase))
                    {
                        providers.Add(providerName);
                        continue;
                    }
                    try
                    {
                        using RegistryKey dependents = dependencies.OpenSubKey(providerName + @"\Dependents");
                        if (null != dependents && dependents.GetSubKeyNames().Contains(bundleId, StringComparer.OrdinalIgnoreCase))
                            providers.Add(providerName);
                    }
                    catch (Exception)
                    {
                        // Another product's key that cannot be read is not ours.
                    }
                }
            }
            return providers.ToList();
        }

        private static bool IsProductInstalled(string productCode, IList<RegistryKey> views)
        {
            foreach (RegistryKey software in views)
            {
                using RegistryKey product = software.OpenSubKey(UNINSTALL_KEY + @"\" + productCode);
                if (null != product)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Take the setup's name off a dependency key, and delete the key, with the cached .msi it stands for, if it is StatsDirect's and nothing else depends on it.
        /// </summary>
        private static void ReleaseProvider(string providerName, string bundleId, RegistryKey software, string packageCacheFolder, Action<string> log)
        {
            using RegistryKey dependencies = software.OpenSubKey(DEPENDENCIES_KEY, true);
            if (null == dependencies)
                return;

            bool isOurs;
            bool hasOtherDependents;
            using (RegistryKey provider = dependencies.OpenSubKey(providerName, true))
            {
                if (null == provider)
                    return;
                isOurs = PRODUCT_NAME.Equals(provider.GetValue("DisplayName") as string, StringComparison.OrdinalIgnoreCase);
                using RegistryKey dependents = provider.OpenSubKey("Dependents", true);
                if (null != dependents)
                {
                    string entry = dependents.GetSubKeyNames().FirstOrDefault(name => name.Equals(bundleId, StringComparison.OrdinalIgnoreCase));
                    if (null != entry)
                        dependents.DeleteSubKeyTree(entry, false);
                }
                hasOtherDependents = null != dependents && dependents.SubKeyCount > 0;
            }
            if (!isOurs || hasOtherDependents)
                return;

            dependencies.DeleteSubKeyTree(providerName, false);
            log($"Deleted dependency key {providerName}.");

            // The old program's cached .msi.  The program itself has gone (checked above), so nothing can need it.
            Match msiProvider = msiProviderPattern.Match(providerName);
            if (msiProvider.Success && msiProvider.Groups[3].Success)
                DeleteCacheFolder(Path.Combine(packageCacheFolder, msiProvider.Groups[1].Value + msiProvider.Groups[3].Value), log);
        }

        /// <summary>
        /// Delete a folder of the package cache that holds nothing but ordinary files.
        /// </summary>
        /// <returns>true if the folder is not there afterwards.</returns>
        private static bool DeleteCacheFolder(string folder, Action<string> log)
        {
            try
            {
                if (!Directory.Exists(folder))
                    return true;
                // A junction or symbolic link would have this code deleting files somewhere else.
                if (File.GetAttributes(folder).HasFlag(FileAttributes.ReparsePoint) || Directory.GetDirectories(folder).Length > 0)
                {
                    log($"Left {folder}: it is not laid out as a package cache folder.");
                    return false;
                }
                string[] files = Directory.GetFiles(folder);
                if (files.Any(file => File.GetAttributes(file).HasFlag(FileAttributes.ReparsePoint)))
                {
                    log($"Left {folder}: it holds a link.");
                    return false;
                }
                // Programs first, so that a failure part of the way through leaves no setup that Burn could run.
                foreach (string file in files.OrderBy(name => name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? 0 : 1))
                {
                    // Burn marks what it caches read-only.
                    FileAttributes attributes = File.GetAttributes(file);
                    if (attributes.HasFlag(FileAttributes.ReadOnly))
                        File.SetAttributes(file, attributes & ~FileAttributes.ReadOnly);
                    File.Delete(file);
                }
                Directory.Delete(folder);
                log($"Deleted {folder}.");
                return true;
            }
            catch (Exception ex)
            {
                log($"Could not delete all of {folder}: {ex.Message}");
                return false;
            }
        }
    }
}
