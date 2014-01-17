using System;
using System.Collections.Generic;
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

            Stopwatch sw = Stopwatch.StartNew();
            frmMain mainWindow;
            // As soon as possible, put up a loader
            using (frmLoading loader = new frmLoading())
            {
                loader.Show();
                Application.DoEvents(); // Force display of the show form
                Debug.WriteLine("After loader show: {0}", sw.ElapsedMilliseconds);

                // Preload and parse XML for operations
                Templates.TemplateFactory.LoadOperationsAsync();
                Debug.WriteLine("After async operations start: {0}", sw.ElapsedMilliseconds);

                CheckExcelAddIn();
                Debug.WriteLine("After CheckExcelAddIn: {0}", sw.ElapsedMilliseconds);
                SetupInitialFiles();
                Debug.WriteLine("After SetupInitialFiles: {0}", sw.ElapsedMilliseconds);

                // Preload a report, to ensure all the report libraries are ready to go.
                PreloadReport();
                Debug.WriteLine("PreloadReport: {0}", sw.ElapsedMilliseconds);

                // Perform any UI hooks we need to...
                SetupUserInterface();

                // Get ready to show the main window...
                mainWindow = new frmMain();
                SdApplication.SoleInstance.MainWindow = mainWindow;

                // ... and go!
                loader.Hide();
                Debug.WriteLine("End: {0}", sw.ElapsedMilliseconds);
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

        private static void CurrentDomain_UnhandledException (object sender, UnhandledExceptionEventArgs e)
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

        private static void Application_ThreadException (object sender, System.Threading.ThreadExceptionEventArgs e)
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
            string appPath = Path.GetDirectoryName(Application.ExecutablePath);
            if (null == appPath)
                return;

            // Copy sample files locally if they don't already exist
            string userStatsDirectFolder = SDConfiguration.MyStatsDirectFolder;
            if (!Directory.Exists(userStatsDirectFolder))
                Directory.CreateDirectory(userStatsDirectFolder);
            string myTestXlsx = SDConfiguration.MyTestFilePath;
            if (!File.Exists(myTestXlsx))
            {
                string distTestXlsx = Path.Combine(Path.Combine(appPath, "Data"), Properties.Settings.Default.DefaultRecentlyUsedFile);
                if (File.Exists(distTestXlsx))
                {
                    File.Copy(distTestXlsx, myTestXlsx, false);
                    // Set the copied file read-only
                    new FileInfo(myTestXlsx).IsReadOnly = true;
                }
            }
        }

        static void CheckExcelAddIn()
        {
            // Install the registry settings if not already present.
            const string app = "ExcelStatsDirect3Link";
            const string key = "Paths";
            string helpPath = SDRegistry.GetSetting(app, key, "Help");
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
                UserInfo ui = License.GetUserInfo();
                DateTime scrapDate;
                if (string.IsNullOrEmpty(ui.Name) || ui.Name.Length < 2 || !DateTime.TryParse(ui.Expires, out scrapDate))
                {
                    using (frmLicense f = new frmLicense(ui))
                    {
                        f.ShowDialog();
                        if (f.UserCancelled)
                            Environment.Exit(1);
                    }
                }
                else
                {
                    int D = DateTime.Parse(ui.Expires).Subtract(DateTime.Today).Days;
                    if (D <= 0 || (ui.Trial && D > 11))
                    {
                        string lk = License.RegDateLock.ToString();
                        ui.Expires = lk;
                        lk = License.XorString(lk, License.REG_KEY_KEY);
                        SDRegistry.SaveSetting(License.REG_APP_NAME, License.REG_LIC, License.REG_UI_EXPIRES, License.StrToNum(lk));
                        using (frmLicense f = new frmLicense(ui, ui.Name, ui.Company))
                        {
                            f.ShowDialog();
                            if (f.UserCancelled)
                                Environment.Exit(1);
                        }
                    }
                    else
                    {
                        if (ui.Expires.Equals(new DateTime(2011, 11, 11).ToString()))
                        {
                            // convert old style perpetual licence to new fixed term
                            string lk = new DateTime(2004, 6, 6).ToString();
                            ui.Expires = lk;
                            lk = License.XorString(lk, License.REG_KEY_KEY);
                            SDRegistry.SaveSetting(License.REG_APP_NAME, License.REG_LIC, License.REG_UI_EXPIRES, License.StrToNum(lk));
                        }
                        SdApplication.SoleInstance.UserInfo = ui;
                        break;
                    }
                }
            }
        }
    }
}