using Microsoft.Win32;
using StatsDirect.Setup;

internal static class Program
{
    private static int passed;

    private static int Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "StatsDirect-addin-test-" + Guid.NewGuid().ToString("N"));
        string registryPath = @"Software\StatsDirect\Tests\ExcelAddInRetirement-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(root);
        try
        {
            using RegistryKey user = Registry.CurrentUser.CreateSubKey(registryPath);
            var log = new List<string>();
            string appData = Path.Combine(root, "AppData");
            string startup = Path.Combine(appData, @"Microsoft\Excel\XLSTART");
            string addins = Path.Combine(appData, @"Microsoft\AddIns");
            string alternate = Path.Combine(root, "Alternate startup");
            foreach (string directory in new[] { startup, addins, alternate })
            {
                Directory.CreateDirectory(directory);
                foreach (string name in ExcelAddInRetirer.FileNames)
                    File.WriteAllText(Path.Combine(directory, name), "old StatsDirect add-in");
                foreach (string name in new[] { "Other.xlam", "PERSONAL.XLSB", "Results.xlsx", "StatsDirect4ExcelLink.xlsm", "MyStatsDirect4ExcelLink.xlam" })
                    File.WriteAllText(Path.Combine(directory, name), "keep this file");
            }
            using RegistryKey options = user.CreateSubKey(@"Software\Microsoft\Office\16.0\Excel\Options");
            using RegistryKey manager = user.CreateSubKey(@"Software\Microsoft\Office\16.0\Excel\Add-in Manager");
            options.SetValue("AltStartup", alternate);
            options.SetValue("OPEN", "/R \"C:\\Old StatsDirect\\StatsDirect4ExcelLink.xlam\"");
            options.SetValue("OPEN1", "/R \"C:\\Another vendor\\Other.xlam\"");
            options.SetValue("OPEN2", "/r \"C:\\Old StatsDirect\\StatsDirect3ExcelLink.xla\"");
            options.SetValue("OPEN3", "%APPDATA%\\Company\\Keep.xlam", RegistryValueKind.ExpandString);
            options.SetValue("OPEN4", "PERSONAL.XLSB");
            options.SetValue("OPENBackup", "do not change");
            options.SetValue("SomeOtherSetting", 42, RegistryValueKind.DWord);
            string registered = @"C:\Custom folder\StatsDirect4ExcelLink.xlam";
            manager.SetValue(registered, "");
            manager.SetValue(@"C:\Custom folder\Other.xlam", "");
            using (RegistryKey settings = user.CreateSubKey(@"Software\VB and VBA Program Settings\ExcelStatsDirect4Link\Paths"))
                settings.SetValue("Help", @"C:\Program Files\StatsDirect");
            using (RegistryKey otherSettings = user.CreateSubKey(@"Software\VB and VBA Program Settings\OtherProgram\Paths"))
                otherSettings.SetValue("Help", "keep");

            ExcelAddInRetirer.RetireUser(user, appData, false, () => true, log.Add);
            Check(File.Exists(Path.Combine(startup, "StatsDirect4ExcelLink.xlam")) && options.GetValue("OPEN2") != null,
                "Running Excel defers both file and registration changes");
            ExcelAddInRetirer.RetireUser(user, appData, true, () => false, log.Add);
            Check(File.Exists(Path.Combine(startup, "StatsDirect4ExcelLink.xlam")) && manager.GetValue(registered) != null,
                "Dry run leaves files and registry unchanged");

            ExcelAddInRetirer.RetireUser(user, appData, false, () => false, log.Add);
            foreach (string directory in new[] { startup, addins, alternate })
            {
                Check(ExcelAddInRetirer.FileNames.All(name => !File.Exists(Path.Combine(directory, name))),
                    "All known legacy add-ins removed from " + Path.GetFileName(directory));
                Check(Directory.GetFiles(directory).Length == 5 && Directory.GetFiles(directory).All(path => File.ReadAllText(path) == "keep this file"),
                    "Other add-ins, workbooks and similarly named files preserved in " + Path.GetFileName(directory));
            }
            Check((string)options.GetValue("OPEN") == "/R \"C:\\Another vendor\\Other.xlam\"", "Next unrelated add-in moved to OPEN");
            Check((string)options.GetValue("OPEN1", null, RegistryValueOptions.DoNotExpandEnvironmentNames) == "%APPDATA%\\Company\\Keep.xlam" &&
                options.GetValueKind("OPEN1") == RegistryValueKind.ExpandString, "Unrelated expandable startup value preserved verbatim with its type");
            Check((string)options.GetValue("OPEN2") == "PERSONAL.XLSB" && options.GetValue("OPEN3") == null && options.GetValue("OPEN4") == null,
                "Startup sequence has no gaps and no duplicates");
            Check((string)options.GetValue("OPENBackup") == "do not change" && (int)options.GetValue("SomeOtherSetting") == 42,
                "Other Excel settings preserved");
            Check(manager.GetValue(registered) == null && manager.GetValue(@"C:\Custom folder\Other.xlam") != null,
                "Only legacy add-in manager registration removed");
            Check(user.OpenSubKey(@"Software\VB and VBA Program Settings\ExcelStatsDirect4Link") == null && user.OpenSubKey(@"Software\VB and VBA Program Settings\OtherProgram\Paths") != null,
                "StatsDirect's own settings for the add-in removed, another program's kept");
            int before = log.Count;
            ExcelAddInRetirer.RetireUser(user, appData, false, () => false, log.Add);
            Check(log.Count == before, "A repeated completed retirement changes nothing");
            ExcelAddInRetirer.RetireUser(user, appData, false, () => true, log.Add);
            Check(log.Count == before, "Nothing to retire: nothing logged, even while Excel is running");

            foreach (string office in new[] { @"Software\Microsoft\Office\12.0", @"Software\Wow6432Node\Microsoft\Office\16.0" })
            {
                using RegistryKey other = user.CreateSubKey(office + @"\Excel\Options");
                other.SetValue("OPEN", @"/R C:\Old\StatsDirectExcelLink.xla");
                ExcelAddInRetirer.RetireUser(user, appData, false, () => false, log.Add);
                Check(other.GetValue("OPEN") == null, "Handles older Office or alternate registry view: " + office);
            }

            string lockedPath = Path.Combine(startup, "StatsDirect4ExcelLink.xlam");
            File.WriteAllText(lockedPath, "locked");
            using (var locked = new FileStream(lockedPath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                ExcelAddInRetirer.RetireUser(user, appData, false, () => false, log.Add);
                Check(File.Exists(lockedPath) && log.Last().StartsWith("Retirement deferred:"), "Locked file left for retry without throwing");
            }
            ExcelAddInRetirer.RetireUser(user, appData, false, () => false, log.Add);
            Check(!File.Exists(lockedPath), "Previously locked file removed on later launch");

            string offline = Path.Combine(root, "OfflineProfile");
            string offlineStartup = Path.Combine(offline, @"Microsoft\Excel\XLSTART");
            Directory.CreateDirectory(offlineStartup);
            File.WriteAllText(Path.Combine(offlineStartup, "StatsDirect3ExcelLink.xlam"), "old");
            ExcelAddInRetirer.RetireUser(null, offline, false, () => false, log.Add, false);
            Check(Directory.GetFiles(offlineStartup).Length == 1, "Offline profile deferred to avoid leaving registrations pointing to missing files");
            ExcelAddInRetirer.RetireUser(user, offline, false, () => false, log.Add);
            Check(Directory.GetFiles(offlineStartup).Length == 0, "Deferred profile cleaned when its registry becomes available");

            string redirected = Path.Combine(root, "Redirected AppData");
            Directory.CreateDirectory(Path.Combine(redirected, @"Microsoft\Excel\XLSTART"));
            string redirectedFile = Path.Combine(redirected, @"Microsoft\Excel\XLSTART\StatsDirect4ExcelLink.xlam");
            File.WriteAllText(redirectedFile, "old");
            ExcelAddInRetirer.RetireUser(user, redirected, false, () => false, log.Add);
            Check(!File.Exists(redirectedFile), "Current-user cleanup uses supplied redirected AppData");

            string target = Path.Combine(root, "LinkTarget");
            Directory.CreateDirectory(target);
            string targetFile = Path.Combine(target, "StatsDirect4ExcelLink.xlam");
            File.WriteAllText(targetFile, "keep through link");
            string link = Path.Combine(root, "LinkedStartup");
            CreateJunction(link, target);
            ExcelAddInRetirer.RetireDirectory(link, false, log.Add);
            Check(File.Exists(targetFile), "Directory reparse point is not followed");
            Check(!ExcelAddInRetirer.SafeLocalPath(Path.Combine(link, "StatsDirect4ExcelLink.xlam")), "A file reached through a reparse point is also rejected");
            Directory.Delete(link); // Remove only the symlink, never its target.

            foreach (string path in new[] { "relative", @"\\server\share\XLSTART", @"\\?\C:\XLSTART", "" })
                Check(!ExcelAddInRetirer.SafeLocalPath(path), "Rejects non-local or relative path: " + path);
            foreach (string name in ExcelAddInRetirer.FileNames)
                Check(ExcelAddInRetirer.IsAddInCommand("/R \"C:\\With spaces\\" + name.ToUpperInvariant() + "\""), "Recognises exact legacy name case-insensitively: " + name);
            foreach (string command in new[] { "Other.xlam", "StatsDirect4ExcelLink.xlsm", "MyStatsDirect4ExcelLink.xlam", "StatsDirect4ExcelLink.xlam.backup",
                "\"C:\\StatsDirect4ExcelLink.xlam\" extra", "\"C:\\StatsDirect4ExcelLink.xlam\\Other.xlam\"", "" })
                Check(!ExcelAddInRetirer.IsAddInCommand(command), "Preserves unrelated command: " + command);

            Console.WriteLine($"PASS: {passed} retirement checks. Only isolated test files and registry keys were changed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(registryPath, false);
            // This unique, fixed-prefix test folder is the only recursive deletion in the tests.
            string resolved = Path.GetFullPath(root);
            if (Path.GetDirectoryName(resolved).Equals(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)
                && Path.GetFileName(resolved).StartsWith("StatsDirect-addin-test-", StringComparison.Ordinal))
                Directory.Delete(resolved, true);
        }
    }

    private static void Check(bool condition, string description)
    {
        if (!condition)
            throw new Exception("FAIL: " + description);
        Console.WriteLine("PASS: " + description);
        passed++;
    }

    private static void CreateJunction(string link, string target)
    {
        // Junctions exercise the reparse-point protection without requiring symbolic-link privileges.
        var start = new System.Diagnostics.ProcessStartInfo("powershell.exe") { UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-NonInteractive");
        start.ArgumentList.Add("-Command");
        start.ArgumentList.Add("$ErrorActionPreference='Stop'; New-Item -ItemType Junction -Path '" + link.Replace("'", "''") +
            "' -Target '" + target.Replace("'", "''") + "' | Out-Null");
        using var process = System.Diagnostics.Process.Start(start);
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new Exception("Could not create the test junction.");
    }
}
