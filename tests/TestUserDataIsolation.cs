using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

internal static class TestUserDataIsolation
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        // This container intentionally does not match the R suite's statsdirect-*
        // cleanup pattern. Concurrent test executables must keep separate profiles.
        string container = Path.Combine(Path.GetTempPath(), "StatsDirectTests");
        string root = Path.Combine(container, Assembly.GetExecutingAssembly().GetName().Name + "-" + Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("STATSDIRECT_USER_DATA_ROOT", root);
        Directory.CreateDirectory(root);
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            if (Environment.ExitCode != 0) return; // Preserve failed-run diagnostics.
            try { Directory.Delete(root, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        };

        // A superseded application DLL may ignore the override. Refuse it before
        // Main can initialize settings, rather than silently using the real profile.
        const string configurationType = "StatsDirect.Configuration.SDConfiguration";
        Assembly tests = Assembly.GetExecutingAssembly();
        Type configuration = tests.GetType(configurationType);
        if (configuration == null)
            foreach (AssemblyName reference in tests.GetReferencedAssemblies())
                if (reference.Name == "StatsDirect")
                {
                    configuration = Assembly.Load(reference).GetType(configurationType, true);
                    break;
                }
        if (configuration != null && !string.Equals(
            (string)configuration.GetProperty("MyStatsDirectFolder").GetValue(null), root,
            StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("StatsDirect did not honor STATSDIRECT_USER_DATA_ROOT. Rebuild the application before running this test suite.");
    }
}
