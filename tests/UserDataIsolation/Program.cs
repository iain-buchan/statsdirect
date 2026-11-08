using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using StatsDirect.Configuration;
using StatsDirect.UI.Properties;

internal static class Program
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
    static string Run(ProcessStartInfo start)
    {
        start.UseShellExecute = false; start.CreateNoWindow = true;
        start.RedirectStandardOutput = true; start.RedirectStandardError = true;
        using var child = Process.Start(start)!;
        var output = child.StandardOutput.ReadToEndAsync();
        var error = child.StandardError.ReadToEndAsync();
        if (!child.WaitForExit(30000)) { child.Kill(true); throw new Exception("Isolation child timed out"); }
        string stdout = output.GetAwaiter().GetResult(), stderr = error.GetAwaiter().GetResult();
        if (child.ExitCode != 0) throw new Exception("Isolation child failed: " + stderr + stdout);
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
            }
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
