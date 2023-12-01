using StatsDirect.R;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace StatsDirect.UI
{
    class RUpdateChecker : UpdateChecker
    {
        public async Task StartCheck(StatusChangedEventHandler handler)
        {
            await base.StartCheck(new Uri("http://cran.r-project.org/bin/windows/base/release.htm"), handler);
        }

        protected override async Task GetCompleted(HttpResponseMessage httpResponseMessage)
        {
            const string prefix = "URL=R-";
            if (!httpResponseMessage.IsSuccessStatusCode)
            {
                UpdateStatus(true, false, false, "Could not check for R updates. Please check your Internet connection.");
                return;
            }
            // Success - look for the version
            string downloadedPage = await httpResponseMessage.Content.ReadAsStringAsync();
            int latestVersionPos = downloadedPage.IndexOf(prefix, StringComparison.Ordinal);
            if (latestVersionPos < 0)
            {
                UpdateStatus(true, false, false, "Could not locate R version on download page. Please check manually at http://cran.r-project.org");
                return;
            }
            string latestVersionLine = downloadedPage[(latestVersionPos + prefix.Length)..];
            int versionsEndPos = latestVersionLine.IndexOf("-win", StringComparison.InvariantCulture);
            if (versionsEndPos < 0)
            {
                UpdateStatus(true, false, false, "Could not locate R version on download page. Please check manually at http://cran.r-project.org");
                return;
            }
            latestVersionLine = latestVersionLine[..versionsEndPos];

            MajorMinorPoint downloadableVersion = new(latestVersionLine);
            bool downloadableIsNewer = true;
            MajorMinorPoint latestInstalledVersion = null;
            foreach (RVersion version in RController.CheckR())
            {
                MajorMinorPoint installedVersion = new(version.VersionString);
                if (!(downloadableVersion > installedVersion))
                    downloadableIsNewer = false;
                if (null == latestInstalledVersion || installedVersion > latestInstalledVersion)
                    latestInstalledVersion = installedVersion;
            }
            if (downloadableIsNewer)
            {
                string currentR = null == latestInstalledVersion ? "You do not have R installed." : "You are presently running R version " + latestInstalledVersion + ".";
                currentR += " Version " + latestVersionLine + " is available. Would you like to download it?";
                UpdateStatus(true, true, true, currentR);
            }
            else
            {
                UpdateStatus(true, true, false, "You are presently running the latest R version.");
            }
        }
    }
}
