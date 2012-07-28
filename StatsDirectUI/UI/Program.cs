using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.IO;
using StatsDirect.Calculator;
using StatsDirect.Utilities;

namespace StatsDirect.UI
{
    static class Program
    {
        // private const string sd_ini = "StatsDirect.ini";
        private const string sd_xls = "StatsDirect.xls";
        private const string test_xlsx = "test.xlsx";
        private const string STATSDIRECT_FOLDER_NAME = "StatsDirect";

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
            frmStatsDirectCalculator mainWindow = new frmStatsDirectCalculator();
            Application.Run(mainWindow);
            mainWindow.Dispose();
        }

        private static void StartStatsDirect(string[] args)
        {
            frmMain mainWindow = null;
            // As soon as possible, put up a loader
            using (frmLoading loader = new frmLoading())
            {
                loader.Show();
                Application.DoEvents(); // Force display of the show form

                CheckExcelAddIn();
                SetupInitialFiles();

                // Preload a report, to ensure all the report libraries are ready to go.
                PreloadReport();

                // Preload and parse XML for operations
                IDictionary<string, Templates.Operation> scrap = Templates.TemplateFactory.Operations;
                IList<Templates.Operation> userScrap = Templates.TemplateFactory.UserOperations;

                // Perform any UI hooks we need to...
                SetupUserInterface();

                // Get ready to show the main window...
                mainWindow = new frmMain();
                SDApplication.SoleInstance.MainWindow = mainWindow;

                // ... and go!
                loader.Hide();
            }

            if (args.Length >= 2)
                if ("FileOpen".Equals(args[0]) && null != args[1])
                    SDApplication.SoleInstance.MainWindow.OpenFile(args[1]);
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
            string myDocuments = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string mySDFolder = Path.Combine(myDocuments, STATSDIRECT_FOLDER_NAME);
            if (!Directory.Exists(mySDFolder))
            {
                Directory.CreateDirectory(mySDFolder);
            }
            if (!Directory.Exists(mySDFolder))
            {
                mySDFolder = appPath;
            }
            // first ini override of userdir - copy over test.xlsx and statsdirect.xls
            if (!mySDFolder.Equals(appPath))
            {
                if (!File.Exists(Path.Combine(mySDFolder, sd_xls)))
                {
                    if (File.Exists(Path.Combine(appPath, sd_xls)))
                        File.Copy(Path.Combine(appPath, sd_xls), Path.Combine(mySDFolder, sd_xls), false);
                }
                if (!File.Exists(Path.Combine(mySDFolder, test_xlsx)))
                {
                    if (File.Exists(Path.Combine(Path.Combine(appPath, "Data"), test_xlsx)))
                        File.Copy(Path.Combine(Path.Combine(appPath, "Data"), test_xlsx), Path.Combine(mySDFolder, test_xlsx), false);
                }
            }
        }

        static void CheckExcelAddIn()
        {
            // Install the registry settings if not already present.
            const string app = "ExcelStatsDirectLink";
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
                        SDApplication.SoleInstance.UserInfo = ui;
                        break;
                    }
                }
            }
        }
    }
}