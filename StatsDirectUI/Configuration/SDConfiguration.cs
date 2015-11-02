using System;
using System.IO;
using System.Diagnostics;
using StatsDirect.UI.Properties;
using System.Reflection;

namespace StatsDirect.Configuration
{
    public class SDConfiguration
    {
        private const string STATSDIRECT_FOLDER_NAME = "StatsDirect";
        private const string R_FOLDER_NAME = "R";
        public const string PERSISTENT_VALUE_FILE_NAME = "session.ser";
        private const string HELP_FILE_NAME = "statsdirect.chm";

        public static string HelpFilePath
        {
            get { return Path.Combine(InstallationDirectory, HELP_FILE_NAME); }
        }

        public static string TemplatePath
        {
            get { return Path.Combine(InstallationDirectory, Settings.Default.TemplateDirectory); }
        }

        public static string MyStatsDirectFolder
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), STATSDIRECT_FOLDER_NAME); }
        }

        public static string MyStatsDirectRFolder
        {
            get { return Path.Combine(MyStatsDirectFolder, R_FOLDER_NAME); }
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
                // string exeName = Process.GetCurrentProcess().MainModule.FileName;
                string exeName = Assembly.GetExecutingAssembly().Location;
                return Path.GetDirectoryName(exeName);
            }
        }
    }
}
