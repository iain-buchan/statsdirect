using System;
using System.IO;
using System.Reflection;

namespace StatsDirect.Configuration
{
    /// <summary>
    /// Things that shouldn't change about a StatsDirect installation. This is a static class that can be referenced from anywhere.
    /// </summary>
    public static class SDConfiguration
    {
        private const string BLANK_REPORT_TEMPLATE_PATH = "blank.rtf";
        private const string DataDirectory = "Data";
        private const string DEFAULT_RECENTLY_USED_FILE_NAME = "test.xlsx";
        private const string HELP_FILE_NAME = "statsdirect.chm";
        private const string MENU_FILE_NAME = "menu.xml";
        private const string OPERATIONS_DIRECTORY_NAME = "Operations";
        private const string PERSISTENT_VALUE_FILE_NAME = "session.ser";
        private const string R_FOLDER_NAME = "R";
        private const string STATSDIRECT_FOLDER_NAME = "StatsDirect";
        private const string TEMPLATE_DIRECTORY_NAME = "Template";

        public static string HelpFilePath => Path.Combine(InstallationPath, HELP_FILE_NAME);
        public static string InstallationPath => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        public static string InstallationTestFilePath => Path.Combine(InstallationPath, DEFAULT_RECENTLY_USED_FILE_NAME);
        public static string MenuFilePath => Path.Combine(InstallationPath, MENU_FILE_NAME);
        public static string MyStatsDirectFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), STATSDIRECT_FOLDER_NAME);
        public static string MyStatsDirectRFolder => Path.Combine(MyStatsDirectFolder, R_FOLDER_NAME);
        public static string MyTestFilePath => Path.Combine(MyStatsDirectFolder, DEFAULT_RECENTLY_USED_FILE_NAME);
        public static string OperationsPath => Path.Combine(InstallationPath, OPERATIONS_DIRECTORY_NAME);
        public static string PersistentValueFilePath => Path.Combine(MyStatsDirectFolder, PERSISTENT_VALUE_FILE_NAME);
        public static string TemplatePathForNewReports => Path.Combine(SDConfiguration.TemplatePath, BLANK_REPORT_TEMPLATE_PATH);
        public static string TemplatePath => Path.Combine(InstallationPath, TEMPLATE_DIRECTORY_NAME);
    }
}
