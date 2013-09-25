using System;
using System.IO;
using System.Diagnostics;
using StatsDirect.UI.Properties;

namespace StatsDirect.Configuration
{
    public class SDConfiguration
    {
        private const string STATSDIRECT_FOLDER_NAME = "StatsDirect";
        public const string PERSISTENT_VALUE_FILE_NAME = "session.ser";
        private const string HELP_FILE_NAME = "statsdirect.chm";
        private const string TEMPLATE_DIRECTORY_NAME = "Template";

        public static string HelpFilePath
        {
            get { return Path.Combine(InstallationDirectory, HELP_FILE_NAME); }
        }

        public static string TemplatePath
        {
            get { return Path.Combine(InstallationDirectory, TEMPLATE_DIRECTORY_NAME); }
        }

        public static string MyStatsDirectFolder
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), STATSDIRECT_FOLDER_NAME); }
        }

        public static string MyTestFilePath
        {
            get
            {
                string mySdPath = MyStatsDirectFolder;
                string defaultRecentlyUsedFile = Settings.Default.DefaultRecentlyUsedFile;
                return Path.Combine(mySdPath, defaultRecentlyUsedFile);
            }
        }

        public static string InstallationDirectory
        {
            get
            {
                string exeName = Process.GetCurrentProcess().MainModule.FileName;
                return Path.GetDirectoryName(exeName);
            }
        }
    }
}
