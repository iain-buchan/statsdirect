using System;
using System.IO;
using System.Reflection;

namespace StatsDirect.Configuration
{
    public static class SDConfiguration
    {
        private const string DATA_DIRECTORY = "Data";
        private const string EXAMPLE_FILE_NAME = "test.xlsx";
        private const string HELP_FILE_NAME = "statsdirect.chm";
        private const string MENU_FILE_NAME = "menu.xml";
        private const string OPERATIONS_DIRECTORY = "Operations";
        private const string R_FOLDER_NAME = "R";
        private const string SETTINGS_FILE_NAME = "statsdirect.json";
        private const string STATSDIRECT_FOLDER_NAME = "StatsDirect";
        private const string TEMPLATE_DIRECTORY = "Template";
        private const string USER_OPERATIONS_DIRECTORY = "UserOperations";

        private static string InstallationRelative(string lastPartOfPath) => Path.Combine(InstallationDirectory, lastPartOfPath);
        private static string InstallationRelative(params string[] lastPartsOfPath) => Path.Combine([InstallationDirectory, .. lastPartsOfPath]);

        public static string DistributionExampleFilePath => InstallationRelative(DATA_DIRECTORY, EXAMPLE_FILE_NAME);
        public static string HelpFilePath => InstallationRelative(HELP_FILE_NAME);
        public static string InstallationDirectory => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        public static string MenuPath => InstallationRelative(MENU_FILE_NAME);
        public static string MyStatsDirectFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), STATSDIRECT_FOLDER_NAME);
        public static string MyStatsDirectRFolder => Path.Combine(MyStatsDirectFolder, R_FOLDER_NAME);
        public static string MyExampleFilePath => Path.Combine(MyStatsDirectFolder, EXAMPLE_FILE_NAME);
        public static string OperationsPath => InstallationRelative(OPERATIONS_DIRECTORY);
        public static string SettingsPath => Path.Combine(MyStatsDirectFolder, SETTINGS_FILE_NAME);
        public static string TemplatePath => InstallationRelative(TEMPLATE_DIRECTORY);
        public static string UserOperationsPath => InstallationRelative(USER_OPERATIONS_DIRECTORY);
    }
}
