using System;
using System.IO;
using StatsDirect.UI.Properties;
using System.Reflection;

namespace StatsDirect.Configuration
{
    public static class SDConfiguration
    {
        private const string STATSDIRECT_FOLDER_NAME = "StatsDirect";
        private const string R_FOLDER_NAME = "R";
        public const string PERSISTENT_VALUE_FILE_NAME = "session.ser";
        private const string SETTINGS_FILE_NAME = "statsdirect.json";
        private const string USER_DATA_ROOT_VARIABLE = "STATSDIRECT_USER_DATA_ROOT";

        public static string HelpDirectory => Path.Combine(InstallationDirectory, "Help");

        public static string TemplatePath => Path.Combine(InstallationDirectory, Settings.Default.TemplateDirectory);

        // A process-local override lets test hosts isolate every user-data write,
        // including preferences, the example workbook and R child processes.
        // Ordinary launches retain the user's Documents/StatsDirect directory.
        public static string MyStatsDirectFolder
        {
            get
            {
                string root = Environment.GetEnvironmentVariable(USER_DATA_ROOT_VARIABLE);
                if (string.IsNullOrEmpty(root))
                    return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), STATSDIRECT_FOLDER_NAME);
                if (!Path.IsPathFullyQualified(root))
                    throw new InvalidOperationException("STATSDIRECT_USER_DATA_ROOT must name an absolute user-data directory.");
                return Path.GetFullPath(root);
            }
        }

        public static string MyStatsDirectRFolder => Path.Combine(MyStatsDirectFolder, R_FOLDER_NAME);

        public static string MyTestFilePath => Path.Combine(MyStatsDirectFolder, Settings.Default.DefaultRecentlyUsedFile);

        public static string InstallationDirectory => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        public static string SettingsPath => Path.Combine(MyStatsDirectFolder, SETTINGS_FILE_NAME);

        internal static void IsolateDiagnosticUserData(string[] args)
        {
            if (args.Length == 0 || args[0] is not ("-sanity-check" or "-test-operations") ||
                !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(USER_DATA_ROOT_VARIABLE)))
                return;
            string root = Path.Combine(Path.GetTempPath(), "StatsDirectTests", "diagnostic-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            Environment.SetEnvironmentVariable(USER_DATA_ROOT_VARIABLE, root);
            // Both diagnostics call Environment.Exit, which bypasses finally/using.
            // Only this automatically-created profile is ours to delete. Keep the
            // override set during shutdown so late writers stay off the real profile.
            AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            {
                try { Directory.Delete(root, true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            };
        }
    }
}
