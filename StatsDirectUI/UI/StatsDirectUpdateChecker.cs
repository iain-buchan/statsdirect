using System;
using System.Net;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace StatsDirect.UI
{
    class StatsDirectUpdateChecker : UpdateChecker
    {
        public void StartCheck(StatusChangedEventHandler handler)
        {
            base.StartCheck(new Uri("http://www.statsdirect.com/download.aspx"), handler);
        }

        protected override void DownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            const string prefix = "Current version";
            if (e.Cancelled)
                return;
            if (null != e.Error)
            {
                UpdateStatus(true, false, false, "Could not check for StatsDirect updates. Please check your Internet connection.");
                return;
            }

            // Success - look for the version
            string downloadedPage = e.Result;
            int latestVersionPos = downloadedPage.IndexOf(prefix, StringComparison.Ordinal);
            if (latestVersionPos < 0)
            {
                UpdateStatus(true, false, false, "Could not locate StatsDirect version on update page. Please check manually at www.statsdirect.com/download.aspx");
                return;
            }
            string latestVersionLine = downloadedPage.Substring(latestVersionPos + prefix.Length);
            int versionsEndPos = latestVersionLine.IndexOf("<br", StringComparison.InvariantCulture);
            if (versionsEndPos < 0)
            {
                UpdateStatus(true, false, false, "Could not locate StatsDirect version on update page. Please check manually at www.statsdirect.com/download.aspx");
                return;
            }
            latestVersionLine = latestVersionLine.Substring(0, versionsEndPos);
            MajorMinorPoint installedVersion = new MajorMinorPoint(Application.ProductVersion);
            MajorMinorPoint availableVersion = GetListedVersion(latestVersionLine, installedVersion);
            if (null == availableVersion || !availableVersion.IsValid)
            {
                UpdateStatus(true, false, false, "Could not locate a version of StatsDirect on update page. Please check manually at www.statsdirect.com/download.aspx");
                return;
            }
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

        /// <summary>
        /// Find the version the update page lists as current.
        /// </summary>
        /// <param name="latestVersionLine">The text following "Current version" on the update page</param>
        /// <param name="installedVersion">The version that is running</param>
        /// <returns>The listed version, or null if none could be found</returns>
        /// <remarks>
        /// The page reads "Current version 4.0.4 (24th June 2024) for Microsoft Windows...", so the version wanted is the one that immediately follows the prefix, whatever its major version.
        /// Only a colon, an equals sign, "is" or a "v" may come between them: anything looser could pick up another number of the same shape, such as a .Net runtime version.
        /// Failing that, look further along the line, but only for a version with the same major version as the one running.
        /// </remarks>
        private static MajorMinorPoint GetListedVersion(string latestVersionLine, MajorMinorPoint installedVersion)
        {
            Match immediatelyFollowing = Regex.Match(latestVersionLine, "^\\s*(?:[:=]|is)?\\s*v?([0-9]+\\.[0-9]+\\.[0-9]+)");
            if (immediatelyFollowing.Success)
            {
                MajorMinorPoint version = new MajorMinorPoint(immediatelyFollowing.Groups[1].Value);
                if (version.IsValid)
                    return version;
            }
            Regex versionSpotter = new Regex("[0-9]+\\.[0-9]+\\.[0-9]+");
            foreach (Match m in versionSpotter.Matches(latestVersionLine))
            {
                MajorMinorPoint version = new MajorMinorPoint(m.Value);
                if (version.IsValid && installedVersion.IsValid && version.Major == installedVersion.Major)
                    return version;
            }
            return null;
        }
    }
}
