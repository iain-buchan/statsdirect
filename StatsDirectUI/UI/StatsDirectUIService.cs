using System;
using System.Windows.Forms;
using System.IO;
using StatsDirect.Utilities;
using StatsDirect.Configuration;
using Microsoft.Extensions.Hosting;
using System.Threading.Tasks;
using System.Threading;

namespace StatsDirect.UI
{
    class StatsDirectUIService: IHostedService
    {
        private ISdApplication SdApplication { get; }
        private IUiPreferences UiPreferences { get; }
        /// <summary>
        /// A convenient place to pass through any file we wish to open.
        /// </summary>
        private string? FileToOpen { get; }

        public StatsDirectUIService(string? fileToOpen, ISdApplication sdApplication, IUiPreferences uiPreferences)
        {
            FileToOpen = fileToOpen;
            SdApplication = sdApplication;
            UiPreferences = uiPreferences;
        }

        Task IHostedService.StartAsync(CancellationToken cancellationToken)
        {
            // Graphics start-up
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // As soon as possible, put up a loader - #1224 on the screen on which frmMain will open.
            Screen launchScreen = FindLaunchScreen(UiPreferences.MainDimensions);
            using (frmLoading loader = new())
            {
                loader.Top = launchScreen.Bounds.Top + (launchScreen.Bounds.Height - loader.Height) / 2;
                loader.Left = launchScreen.Bounds.Left + (launchScreen.Bounds.Width - loader.Width) / 2;
                loader.Show();
                Application.DoEvents(); // Force display of the show form

                // Prepare a background check for new version, if there is one.  This will tidy up after itself.
                int checkForUpdatesInt = SDRegistry.GetDwordSetting("StatsDirect3", "Startup", "CheckForUpdates", true);
                if (checkForUpdatesInt != 0) // If the value is not there, this returns int.MinValue, which is non-zero; we should check in this case.
                    new frmUpdateCheck(true, SdApplication);

                // Preload and parse XML for operations
                Templates.TemplateFactory.LoadOperationsAsync();

                ExcelAddInManager.Check();
                SetupInitialFiles();

                // Preload a report, to ensure all the report libraries are ready to go.
                if (!SdApplication.IsRunningOnMono)
                    PreloadReport();

                // Perform any UI hooks we need to...
                SetupUserInterface();

                // ... and go!
                loader.Hide();
            }

            if (null != FileToOpen)
                SdApplication.OpenFile(FileToOpen, true);
            SdApplication.Run();

            return Task.CompletedTask;
        }

        private static Screen FindLaunchScreen(WindowDimensions windowDimensions)
        {
            double mainMidY = windowDimensions.Top + windowDimensions.Height / 2.0;
            double mainMidX = windowDimensions.Left + windowDimensions.Width / 2.0;
            Screen bestSoFar = Screen.PrimaryScreen;
            double smallestSquaredDistanceSoFar = double.MaxValue;
            foreach (Screen candidate in Screen.AllScreens)
            {
                double screenCentreY = candidate.Bounds.Top + candidate.Bounds.Height / 2.0;
                double screenCentreX = candidate.Bounds.Left + candidate.Bounds.Width / 2.0;
                double squaredDistanceBetweenCentres = (mainMidX - screenCentreX) * (mainMidX - screenCentreX) + (mainMidY - screenCentreY) * (mainMidY - screenCentreY);
                if (squaredDistanceBetweenCentres < smallestSquaredDistanceSoFar || squaredDistanceBetweenCentres == smallestSquaredDistanceSoFar && candidate.Primary)
                {
                    bestSoFar = candidate;
                    smallestSquaredDistanceSoFar = squaredDistanceBetweenCentres;
                }
            }

            return bestSoFar;
        }

        /// <summary>
        /// Cause the report libraries to pre-load, for perceived faster first report startup.
        /// </summary>
        /// <remarks>Requires frmReportRichEditDummy to close itself once it has shown itself.</remarks>
        private static void PreloadReport()
        {
            using frmReportRichEditDummy f = new();
            // Ensure the form loads off the visible area on any screen - it'll load just to the right of the furthest-right screen.
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

        private static void SetupUserInterface()
        {
            ToolStripManager.Renderer = new StatsDirectToolStripRenderer();
        }

        void SetupInitialFiles()
        {
            // Copy sample files locally if they don't already exist
            string userStatsDirectFolder = SDConfiguration.MyStatsDirectFolder;
            if (!Directory.Exists(userStatsDirectFolder))
            {
                try
                {
                    Directory.CreateDirectory(userStatsDirectFolder);
                }
                catch (Exception ex)
                {
                    SdApplication.FriendlyError($"Couldn't create {userStatsDirectFolder}; will try again next time you start StatsDirect. The example file (test.xlsx) will not be available.", ex, false);
                    return;
                }
            }
            string myTestXlsx = SDConfiguration.MyTestFilePath;
            if (File.Exists(myTestXlsx))
            {
                // If we have a newer distribution file, save the user's as an old version (in case they've made any alterations) and then copy over our new one.
                string? distTestXlsx = InstallationTestXlsx();
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
                            string probePath = $"{prefix}_old_{nonExistingVersion}{extension}";
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
                                    SdApplication.FriendlyError("Couldn't copy the new StatsDirect test file to your own copy; will try again next time you start StatsDirect", ex, false);
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
                    // SdApplication.FriendlyError("Couldn't copy the StatsDirect test file to your own copy; will try again next time you start StatsDirect", ex, false);
                }
            }
        }

        private static void CopyTestFileTo(string myTestXlsx)
        {
            string? distTestXlsx = InstallationTestXlsx();
            if (distTestXlsx is null)
                return;
            File.Copy(distTestXlsx, myTestXlsx, false);
            // Set the copied file read-only.  TODO: Does this work?!
            FileInfo tx = new(myTestXlsx) {IsReadOnly = true};
        }

        /// <summary>
        /// Return the path to the distribution test.xlsx if it is defined and the file exists at that location; otherwise return null.
        /// </summary>
        /// <returns></returns>
        private static string? InstallationTestXlsx()
        {
            string path = SDConfiguration.InstallationTestFilePath;
            return File.Exists(path)
                ? path
                : null;
        }

        Task IHostedService.StopAsync(CancellationToken cancellationToken)
        {
            // Do nothing
            return Task.CompletedTask;
        }
    }
}