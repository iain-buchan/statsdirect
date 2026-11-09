using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using StatsDirect.Configuration;
using StatsDirect.UI.Properties;

internal static partial class Program
{
    const string Variable = "STATSDIRECT_USER_DATA_ROOT";
    static int checks;
    static readonly string StartupExamplePath = SDConfiguration.MyTestFilePath;
    static void Check(bool value, string description)
    {
        if (!value) throw new Exception(description);
        checks++; Console.WriteLine("PASS " + description);
    }
    static string Snapshot(string path) => !File.Exists(path) ? "missing" :
        new FileInfo(path).Length + ":" + File.GetLastWriteTimeUtc(path).Ticks + ":" +
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    static string Run(ProcessStartInfo start, int expectedExitCode = 0)
    {
        start.UseShellExecute = false; start.CreateNoWindow = true;
        start.RedirectStandardOutput = true; start.RedirectStandardError = true;
        using var child = Process.Start(start)!;
        var output = child.StandardOutput.ReadToEndAsync();
        var error = child.StandardError.ReadToEndAsync();
        if (!child.WaitForExit(30000)) { child.Kill(true); throw new Exception("Isolation child timed out"); }
        string stdout = output.GetAwaiter().GetResult(), stderr = error.GetAwaiter().GetResult();
        if (child.ExitCode != expectedExitCode) throw new Exception("Isolation child exited " + child.ExitCode + ", expected " + expectedExitCode + ": " + stderr + stdout);
        return stdout.Trim();
    }
    static ProcessStartInfo Self(params string[] args)
    {
        var start = new ProcessStartInfo(Environment.ProcessPath!);
        if (Path.GetFileNameWithoutExtension(Environment.ProcessPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        foreach (string arg in args) start.ArgumentList.Add(arg);
        return start;
    }
    static int Main(string[] args)
    {
        try
        {
            if (args.Length == 2 && args[0] == "--diagnostic-failure-child")
            {
                string initializerRoot = Environment.GetEnvironmentVariable(Variable)!;
                Environment.SetEnvironmentVariable(Variable, null);
                SDConfiguration.IsolateDiagnosticUserData(new[] { args[1] });
                Settings.Default.DefaultConfidenceInterval = 0.81;
                Settings.Default.Save();
                string blackbox = Path.Combine(SDConfiguration.MyStatsDirectFolder, "Blackbox.txt");
                File.WriteAllText(blackbox, "Synthetic failed diagnostic; exit code 17.\n");
                Console.WriteLine(JsonSerializer.Serialize(new {
                    root = SDConfiguration.MyStatsDirectFolder, initializerRoot,
                    settingsSnapshot = Snapshot(SDConfiguration.SettingsPath), blackboxSnapshot = Snapshot(blackbox)
                }));
                Environment.Exit(17); // The real diagnostic exits must retain their crash context on failure.
            }
            if (args.Length >= 2 && args[0] == "--diagnostic-child")
            {
                Environment.SetEnvironmentVariable(Variable, args.Length == 3 ? args[2] : null);
                SDConfiguration.IsolateDiagnosticUserData(new[] { args[1] });
                Settings.Default.Save();
                Console.WriteLine(JsonSerializer.Serialize(new { root = SDConfiguration.MyStatsDirectFolder, saved = File.Exists(SDConfiguration.SettingsPath) }));
                Environment.Exit(0); // Match both real diagnostic entry points.
            }
            if (args.SequenceEqual(new[] { "--child" }))
            {
                Settings.Default.SelectGroupsByIdentifier = true; Settings.Default.Save();
                Console.WriteLine(JsonSerializer.Serialize(new {
                    root = SDConfiguration.MyStatsDirectFolder,
                    settings = SDConfiguration.SettingsPath,
                    saved = JsonDocument.Parse(File.ReadAllText(SDConfiguration.SettingsPath)).RootElement.GetProperty("selectGroupsByIdentifier").GetBoolean()
                }));
                return 0;
            }
            string normal = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "StatsDirect");
            string normalSettings = Path.Combine(normal, "statsdirect.json"), normalExample = Path.Combine(normal, "test.xlsx");
            string settingsBefore = Snapshot(normalSettings), exampleBefore = Snapshot(normalExample);
            string root = Environment.GetEnvironmentVariable(Variable)!;
            Check(Path.IsPathFullyQualified(root) && root != normal && Directory.Exists(root),
                "shared test initializer sets an isolated absolute profile before Main");
            Check(SDConfiguration.MyStatsDirectFolder == root, "user-data override selects the isolated profile");
            Check(SDConfiguration.SettingsPath == Path.Combine(root, "statsdirect.json"), "preferences use the isolated profile");
            Check(SDConfiguration.MyTestFilePath == Path.Combine(root, "test.xlsx"), "example workbook defaults use the isolated profile");
            Check(StartupExamplePath == Path.Combine(root, "test.xlsx"), "test static initialization sees isolated settings and workbook defaults");
            Check(SDConfiguration.MyStatsDirectRFolder == Path.Combine(root, "R"), "R workspace defaults use the isolated profile");

            Settings.Default.DefaultConfidenceInterval = 0.90; Settings.Default.ShouldKeepData = true; Settings.Default.Save();
            using (var saved = JsonDocument.Parse(File.ReadAllText(SDConfiguration.SettingsPath)))
                Check(saved.RootElement.GetProperty("defaultConfidenceInterval").GetDouble() == 0.90 &&
                    saved.RootElement.GetProperty("shouldKeepData").GetBoolean(), "real Settings.Save writes preferences only in the isolated profile");
            File.WriteAllText(SDConfiguration.MyTestFilePath, "isolated example fixture");
            Directory.CreateDirectory(SDConfiguration.MyStatsDirectRFolder);
            File.WriteAllText(Path.Combine(SDConfiguration.MyStatsDirectRFolder, "probe.R"), "x <- 1\n");

            var inherited = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe"));
            foreach (string arg in new[] { "-NoProfile", "-NonInteractive", "-Command", "[Console]::Write([Environment]::GetEnvironmentVariable('STATSDIRECT_USER_DATA_ROOT'))" })
                inherited.ArgumentList.Add(arg);
            Check(Run(inherited) == root, "ordinary child processes inherit the isolated user-data override");

            using (var child = JsonDocument.Parse(Run(Self("--child"))))
            {
                string childRoot = child.RootElement.GetProperty("root").GetString()!;
                Check(childRoot != root && childRoot != normal && Path.IsPathFullyQualified(childRoot),
                    "a second test executable gets its own profile despite inherited overrides");
                Check(child.RootElement.GetProperty("saved").GetBoolean() &&
                    child.RootElement.GetProperty("settings").GetString() == Path.Combine(childRoot, "statsdirect.json"),
                    "the second test process saves only its own isolated preferences");
                Check(!Directory.Exists(childRoot) && Directory.Exists(root), "successful child cleanup preserves the parent's active profile");
            }
            foreach (string flag in new[] { "-sanity-check", "-test-operations" })
            {
                using var diagnostic = JsonDocument.Parse(Run(Self("--diagnostic-child", flag)));
                string diagnosticRoot = diagnostic.RootElement.GetProperty("root").GetString()!;
                Check(diagnosticRoot != root && diagnosticRoot != normal && Path.IsPathFullyQualified(diagnosticRoot) &&
                    diagnostic.RootElement.GetProperty("saved").GetBoolean(), flag + " automatically isolates its user-data writes");
                Check(!Directory.Exists(diagnosticRoot), flag + " cleans its automatic profile even through Environment.Exit");
                string callerRoot = Path.Combine(root, "caller-" + flag[1..]);
                Directory.CreateDirectory(callerRoot);
                using var caller = JsonDocument.Parse(Run(Self("--diagnostic-child", flag, callerRoot)));
                Check(caller.RootElement.GetProperty("root").GetString() == callerRoot &&
                    File.Exists(Path.Combine(callerRoot, "statsdirect.json")), flag + " honors a caller's profile without deleting it");

                using var failed = JsonDocument.Parse(Run(Self("--diagnostic-failure-child", flag), expectedExitCode: 17));
                string failedRoot = failed.RootElement.GetProperty("root").GetString()!;
                string initializerRoot = failed.RootElement.GetProperty("initializerRoot").GetString()!;
                Check(failedRoot != root && failedRoot != normal && Directory.Exists(failedRoot),
                    flag + " retains its automatic diagnostic profile after a real nonzero child exit");
                Check(Snapshot(Path.Combine(failedRoot, "Blackbox.txt")) == failed.RootElement.GetProperty("blackboxSnapshot").GetString() &&
                    Snapshot(Path.Combine(failedRoot, "statsdirect.json")) == failed.RootElement.GetProperty("settingsSnapshot").GetString(),
                    flag + " retains Blackbox.txt and the saved diagnostic preferences byte for byte");

                // Both failed child profiles belong to this invocation: the shared
                // test initializer's profile and the production diagnostic's own.
                // Validate exact synthetic roots before deleting either one; never
                // enumerate or clean another test process's profile or real data.
                bool OwnProfile(string path, string prefix)
                {
                    string full = Path.GetFullPath(path);
                    string container = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "StatsDirectTests"));
                    string name = Path.GetFileName(full);
                    return path == full && full != root && full != normal &&
                        string.Equals(Path.GetDirectoryName(full), container, StringComparison.OrdinalIgnoreCase) &&
                        name.StartsWith(prefix, StringComparison.Ordinal) && Guid.TryParseExact(name[prefix.Length..], "N", out _) &&
                        Directory.Exists(full) &&
                        (File.GetAttributes(container) & FileAttributes.ReparsePoint) == 0 &&
                        (File.GetAttributes(full) & FileAttributes.ReparsePoint) == 0;
                }
                Check(OwnProfile(failedRoot, "diagnostic-") && OwnProfile(initializerRoot, Assembly.GetExecutingAssembly().GetName().Name + "-"),
                    flag + " failed-profile cleanup is restricted to this child's verified synthetic directories");
                Directory.Delete(failedRoot, true);
                Directory.Delete(initializerRoot, true);
                Check(!Directory.Exists(failedRoot) && !Directory.Exists(initializerRoot) && Directory.Exists(root),
                    flag + " removes only its verified failed fixtures after preservation is proved");
            }
            CheckSavedPreferences(root, normal);
            try
            {
                Environment.SetEnvironmentVariable(Variable, null);
                Check(SDConfiguration.MyStatsDirectFolder == normal &&
                    SDConfiguration.SettingsPath == normalSettings && SDConfiguration.MyTestFilePath == normalExample &&
                    SDConfiguration.MyStatsDirectRFolder == Path.Combine(normal, "R"),
                    "ordinary application paths remain unchanged when the override is unset");
                SDConfiguration.IsolateDiagnosticUserData(Array.Empty<string>());
                SDConfiguration.IsolateDiagnosticUserData(new[] { "-calculator" });
                SDConfiguration.IsolateDiagnosticUserData(new[] { "FileOpen", "example.xlsx" });
                Check(Environment.GetEnvironmentVariable(Variable) == null && SDConfiguration.MyStatsDirectFolder == normal,
                    "ordinary, calculator and file-opening launches do not enable diagnostic isolation");
                Environment.SetEnvironmentVariable(Variable, "relative-user-data");
                bool refused = false;
                try { _ = SDConfiguration.MyStatsDirectFolder; } catch (InvalidOperationException) { refused = true; }
                Check(refused, "invalid relative overrides fail instead of falling back to the user's profile");
            }
            finally { Environment.SetEnvironmentVariable(Variable, root); }
            Check(Snapshot(normalSettings) == settingsBefore, "real preferences retain their hash, length and modification time");
            Check(Snapshot(normalExample) == exampleBefore, "real example workbook retains its hash, length and modification time");
            Console.WriteLine($"PASS {checks} user-data isolation checks");
            return 0;
        }
        catch (Exception ex) { Console.WriteLine("FAIL " + ex); return 1; }
    }
}
