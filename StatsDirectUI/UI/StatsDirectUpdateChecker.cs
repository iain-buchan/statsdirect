using System;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace StatsDirect.UI
{
    class StatsDirectUpdateChecker : UpdateChecker
    {
        public async Task StartCheck(StatusChangedEventHandler handler)
        {
            await base.StartCheck(new Uri("http://www.statsdirect.com/update.aspx"), handler);
        }

        protected override async Task GetCompleted(HttpResponseMessage httpResponseMessage)
        {
            const string prefix = "Current version";
            if (!httpResponseMessage.IsSuccessStatusCode)
            {
                UpdateStatus(true, false, false, "Could not check for StatsDirect updates. Please check your Internet connection.");
                return;
            }
            // Success - look for the version
            string downloadedPage = await httpResponseMessage.Content.ReadAsStringAsync();
            int latestVersionPos = downloadedPage.IndexOf(prefix, StringComparison.Ordinal);
            if (latestVersionPos < 0)
            {
                UpdateStatus(true, false, false, "Could not locate StatsDirect version on update page. Please check manually at www.statsdirect.com/update.aspx");
                return;
            }
            string latestVersionLine = downloadedPage[(latestVersionPos + prefix.Length)..];
            int versionsEndPos = latestVersionLine.IndexOf("<br", StringComparison.InvariantCulture);
            if (versionsEndPos < 0)
            {
                UpdateStatus(true, false, false, "Could not locate StatsDirect version on update page. Please check manually at www.statsdirect.com/update.aspx");
                return;
            }
            latestVersionLine = latestVersionLine[..(versionsEndPos - 1)];
            MajorMinorPoint availableVersion = GetVersion3(latestVersionLine);
            if (null == availableVersion || !availableVersion.IsValid)
            {
                UpdateStatus(true, false, false, "Could not locate a version of StatsDirect 3 on update page. Please check manually at www.statsdirect.com/update.aspx");
                return;
            }
            MajorMinorPoint installedVersion = new(Application.ProductVersion);
            // isNewer = true; // useful for testing without updating the web site!
            if (installedVersion < availableVersion)
            {
                UpdateStatus(true, true, true, $"You are presently running StatsDirect version {installedVersion}. Version {availableVersion} is available. Would you like to close StatsDirect and install the new version?");
            }
            else
            {
                UpdateStatus(true, true, false, $"You are presently running StatsDirect version {installedVersion}. You have the latest version of StatsDirect.");
            }
        }

        private static MajorMinorPoint GetVersion3(string latestVersionLine)
        {
            Regex versionSpotter = new("[0-9]+\\.[0-9]+\\.[0-9]+");
            MatchCollection matches = versionSpotter.Matches(latestVersionLine);
            if (matches.Count == 0)
                return null;
            foreach (Match m in matches)
            {
                MajorMinorPoint version = new(m.Value);
                if (version.IsValid && version.Major == 3)
                    return version;
            }
            return null;
        }
    }
}
