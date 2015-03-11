using System;
using System.Windows.Forms;
using System.IO;
using StatsDirect.Calculator;
using StatsDirect.Utilities;
using System.Diagnostics;
using StatsDirect.Configuration;

namespace StatsDirect.UI
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        public static void Main(string[] args)
        {
            // Right at the start, cope with as many variants of chaos as we can.
            CatchMostErrors();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            CheckLicense();

            if (args.Length > 0 && "-calculator".Equals(args[0]))
                StartCalculator();
            else
                StartStatsDirect(args);
        }

        private static void StartCalculator()
        {
            using (frmStatsDirectCalculator mainWindow = new frmStatsDirectCalculator())
            {
                Application.Run(mainWindow);
            }
        }

        private static void StartStatsDirect(string[] args)
        {
            // If we're opening a file on behalf of someone, check whether we should open it in an older StatsDirect process that owns the user interface.  If so, we need do nothing more.
            if (args.Length >= 2)
                if ("FileOpen".Equals(args[0]) && null != args[1])
                    if (IpcSender.TryToTellAnotherStatsDirectToOpen(args[1]))
                        return;

            frmMain mainWindow;
            // As soon as possible, put up a loader
            using (frmLoading loader = new frmLoading())
            {
                loader.Show();
                Application.DoEvents(); // Force display of the show form

                // Prep a background check for new version, if there is one.  This will tidy up after itself.
                int checkForUpdatesInt = SDRegistry.GetDwordSetting("StatsDirect3", "Startup", "CheckForUpdates", true);
                if (checkForUpdatesInt != 0) // If the value is not there, this returns int.MinValue, which is non-zero; we should check in this case.
                    new frmUpdateCheck(true);

                // Preload and parse XML for operations
                Templates.TemplateFactory.LoadOperationsAsync();

                CheckExcelAddIn();
                SetupInitialFiles();

                // Preload a report, to ensure all the report libraries are ready to go.
                PreloadReport();

                // Perform any UI hooks we need to...
                SetupUserInterface();

                // Get ready to show the main window...
                mainWindow = new frmMain();
                SdApplication.SoleInstance.MainWindow = mainWindow;

                // ... and go!
                loader.Hide();
            }

            if (args.Length >= 2)
                if ("FileOpen".Equals(args[0]) && null != args[1])
                    SdApplication.SoleInstance.MainWindow.OpenFile(args[1], true);
            Application.Run(mainWindow);
        }

        private static void PreloadReport()
        {
            using (frmReportRichEditDummy f = new frmReportRichEditDummy())
            {
                int largestVisibleX = int.MinValue;
                int smallestVisibleY = int.MaxValue;
                foreach (Screen screen in Screen.AllScreens)
                {
                    if (screen.Bounds.Right > largestVisibleX)
                        largestVisibleX = screen.Bounds.Right;
                    if (screen.Bounds.Top < smallestVisibleY)
                        smallestVisibleY = screen.Bounds.Top;
                }
                f.Left = largestVisibleX + 10;
                f.Top = smallestVisibleY;
                f.ShowDialog();
                // f will auto-close itself once it's shown itself
            }
        }

        private static void SetupUserInterface()
        {
            ToolStripManager.Renderer = new StatsDirectToolStripRenderer();
        }

        private static void CatchMostErrors()
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            Application.ThreadException += Application_ThreadException;
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                Exception ex = (Exception)e.ExceptionObject;

                CopeWithUnhandledException(ex, false);
            }
            finally
            {
                // Politely ask everything to quit
                Application.Exit();

                // If that returns, force the issue!
                Environment.Exit(-1);
            }
        }

        private static void Application_ThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            try
            {
                CopeWithUnhandledException(e.Exception, false);
            }
            finally
            {
                // Politely ask everything to quit
                Application.Exit();

                // If that returns, force the issue!
                Environment.Exit(-1);
            }
        }

        /// <summary>
        /// Last-ditch attempt to cope with an otherwise-unhandled exception and gain as much information as we can.
        /// </summary>
        /// <param name="ex">The unhandled exception</param>
        /// <param name="canTryToContinue">true if the application might be able to continue (typically because the exception was on a background thread). false if the application cannot continue.</param>
        /// <returns>DialogResult.Abort if the application should close, otherwise something else.</returns>
        private static DialogResult CopeWithUnhandledException(Exception ex, bool canTryToContinue)
        {
            string message = ex.Message + Environment.NewLine + ex.StackTrace;
            using (frmErrorMessage e = new frmErrorMessage(message))
            {
                e.ShowDialog();
            }
            return DialogResult.Abort;
        }

        static void SetupInitialFiles()
        {
            // Copy sample files locally if they don't already exist
            string userStatsDirectFolder = SDConfiguration.MyStatsDirectFolder;
            if (!Directory.Exists(userStatsDirectFolder))
                Directory.CreateDirectory(userStatsDirectFolder);
            string myTestXlsx = SDConfiguration.MyTestFilePath;
            if (File.Exists(myTestXlsx))
            {
                // If we have a newer distribution file, save the user's as an old version (in case they've made any alterations) and then copy over our new one.
                string distTestXlsx = DistTestXlsx();
                if (null != distTestXlsx)
                {
                    DateTime distModified = new FileInfo(distTestXlsx).LastWriteTimeUtc;
                    DateTime mineModified = new FileInfo(myTestXlsx).LastWriteTimeUtc;
                    if (distModified > mineModified)
                    {
                        string extension = Path.GetExtension(myTestXlsx);
                        string prefix = Path.Combine(Path.GetDirectoryName(myTestXlsx), Path.GetFileNameWithoutExtension(myTestXlsx));
                        int nonExistingVersion = 1;
                        while (true)
                        {
                            string probePath = string.Format("{0}_old_{1}{2}", prefix, nonExistingVersion, extension);
                            if (File.Exists(probePath))
                                nonExistingVersion++;
                            else
                            {
                                try
                                {
                                    File.Copy(myTestXlsx, probePath);
                                    if (File.Exists(probePath))
                                    {
                                        new FileInfo(myTestXlsx).IsReadOnly = false;
                                        File.Delete(myTestXlsx);
                                        CopyTestFileTo(myTestXlsx);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    SdApplication.SoleInstance.FriendlyError("Couldn't copy the new StatsDirect test file to your own copy; will try again next time you start StatsDirect", ex, false);
                                }
                                break;
                            }
                        }
                    }
                }
            }
            else
            {
                try
                {
                    CopyTestFileTo(myTestXlsx);
                }
                catch (Exception)
                {
                    // Do nothing
                    // SdApplication.SoleInstance.FriendlyError("Couldn't copy the StatsDirect test file to your own copy; will try again next time you start StatsDirect", ex, false);
                }
            }
        }

        private static void CopyTestFileTo(string myTestXlsx)
        {
            string distTestXlsx = DistTestXlsx();
            if (null == distTestXlsx)
                return;
            File.Copy(distTestXlsx, myTestXlsx, false);
            // Set the copied file read-only
            FileInfo tx = new FileInfo(myTestXlsx);
            tx.IsReadOnly = true;
        }

        /// <summary>
        /// Return the path to the distribution test.xlsx if it is defined and the file exists at that location; otherwise return null.
        /// </summary>
        /// <returns></returns>
        private static string DistTestXlsx()
        {
            string appPath = Path.GetDirectoryName(Application.ExecutablePath);
            if (null == appPath)
                return null;
            string path = Path.Combine(Path.Combine(appPath, "Data"), Properties.Settings.Default.DefaultRecentlyUsedFile);
            if (!File.Exists(path))
                return null;
            return path;
        }

        static void CheckExcelAddIn()
        {
            // Install the registry settings if not already present.
            const string app = "ExcelStatsDirect3Link";
            const string key = "Paths";
            string helpPath = SDRegistry.GetStringSetting(app, key, "Help", false);
            string appPath = Path.GetDirectoryName(Application.ExecutablePath);
            if (null != appPath)
            {
                // Save if changed or nonexistent
                if (!appPath.Equals(helpPath))
                {
                    SDRegistry.SaveSetting(app, key, "Help", appPath);
                    SDRegistry.SaveSetting(app, key, "Data", ""); // Unused but required
                }
            }

            // If this is the first run with Excel in place, ask the user if they want to enable SD Excel integration
            bool entriesInPlace = null != helpPath;
            if (!entriesInPlace)
            {
                bool excelInstalled = ExcelAddInManager.IsExcelInstalled();
                if (!excelInstalled)
                    return;

                if (DialogResult.Yes ==
                    MessageBox.Show(
                        "StatsDirect Excel integration allows you to\n\rstart StatsDirect to process an Excel spreadsheet.\n\r\n\rWould you like to enable this integration?\r\nYou can turn it on and off from the StatsDirect Tools menu.",
                        "StatsDirect", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1))
                {
                    ExcelAddInManager.InstallAddIn();
                }
            }
        }

        static void CheckLicense()
        {
            while (true)
            {
                UserInfo userUi = License.GetUserInfo(false);
                UserInfo machineUi = License.GetUserInfo(true);
                bool userIsPartiallyComplete;
                bool userOk = License.Check(userUi, out userIsPartiallyComplete);
                bool machineIsPartiallyComplete;
                bool machineOk = License.Check(machineUi, out machineIsPartiallyComplete);
                if (userOk || machineOk)
                {
                    SdApplication.SoleInstance.UserInfo = machineOk ? machineUi : userUi;
                    return;
                }

                // If we get here, neither the user nor the machine license are good.  Get the user to start a trial or enter a good key, or exit SD.
                // The user has no ability to enter a machine key, so always use the user key as the basis of this.
                using (frmLicense f = new frmLicense(userUi, userIsPartiallyComplete))
                {
                    f.ShowDialog();
                    if (f.UserCancelled)
                        Environment.Exit(1);
                }
            }
        }
    }
}