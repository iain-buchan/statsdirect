using StatsDirect.R;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace StatsDirect.UI
{
    public partial class frmUpdateCheck : Form
    {
        private WebClient statsDirectClient;
        private WebClient rClient;

        public frmUpdateCheck()
        {
            InitializeComponent();
            Application.UseWaitCursor = true;
        }

        private void cmdClose_Click(object sender, EventArgs e)
        {
            if (null != statsDirectClient)
                statsDirectClient.CancelAsync();
            Close();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (null != statsDirectClient)
                statsDirectClient.CancelAsync();
            Close();
            SdApplication.SoleInstance.CloseAndUpdate();
        }

        private void frmUpdateCheck_Shown(object sender, EventArgs e)
        {
            StartStatsDirectCheck();
            StartRCheck();
        }

        private void StartStatsDirectCheck()
        {
            Application.UseWaitCursor = true;
            // Just using DownloadStringAsync can block on DNS resolution; so perform async DNS resolution for www.statsdirect.com
            Dns.BeginGetHostAddresses("www.statsdirect.com", StatsDirectDnsCompleted, null);
        }

        private void StartRCheck()
        {
            Application.UseWaitCursor = true;
            // Just using DownloadStringAsync can block on DNS resolution; so perform async DNS resolution for cran.r-project.org
            Dns.BeginGetHostAddresses("cran.r-project.org", RDnsCompleted, null);
        }

        private void StatsDirectDnsCompleted(IAsyncResult ar)
        {
            if (!ar.IsCompleted)
            {
                UpdateStatsDirectStatus("Could not resolve www.statsdirect.com; are you connected to a network?");
                return;
            }
            try
            {
                var addresses = Dns.EndGetHostAddresses(ar);
                UpdateStatsDirectStatus("Contacting www.statsdirect.com...");
                statsDirectClient = new WebClient();
                statsDirectClient.DownloadStringCompleted += DownloadStatsDirectStringCompleted;
                statsDirectClient.DownloadStringAsync(new Uri(string.Format("http://{0}/update.aspx", addresses[0])));
            }
            catch (SocketException)
            {
                UpdateStatsDirectStatus("Could not resolve www.statsdirect.com; are you connected to a network?");
            }
        }

        private void RDnsCompleted(IAsyncResult ar)
        {
            if (!ar.IsCompleted)
            {
                UpdateRStatus("Could not resolve cran.r-project.org; are you connected to a network?");
                return;
            }
            try
            {
                var addresses = Dns.EndGetHostAddresses(ar);
                UpdateRStatus("Contacting cran.r-project.org...");
                rClient = new WebClient();
                rClient.DownloadStringCompleted += DownloadRStringCompleted;
                rClient.DownloadStringAsync(new Uri("http://cran.r-project.org/bin/windows/base/release.htm"));
            }
            catch (SocketException)
            {
                UpdateStatsDirectStatus("Could not resolve www.statsdirect.com; are you connected to a network?");
            }
        }

        private void UpdateStatsDirectStatus(string status)
        {
            Application.UseWaitCursor = false;
            if (lblStatsDirectStatus.InvokeRequired)
            {
                lblStatsDirectStatus.Invoke(new MethodInvoker(delegate { lblStatsDirectStatus.Text = status; }));
            }
            else
            {
                lblStatsDirectStatus.Text = status;
            }
        }

        private void UpdateRStatus(string status)
        {
            Application.UseWaitCursor = false;
            if (lblRStatus.InvokeRequired)
            {
                lblRStatus.Invoke(new MethodInvoker(delegate { lblRStatus.Text = status; }));
            }
            else
            {
                lblRStatus.Text = status;
            }
        }

        private void DownloadStatsDirectStringCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            Application.UseWaitCursor = false;
            const string prefix = "Current version";
            if (e.Cancelled)
                return;
            if (null != e.Error)
            {
                UpdateStatsDirectStatus("Could not check for StatsDirect updates. Please check your Internet connection.");
                return;
            }

            // Success - look for the version
            string downloadedPage = e.Result;
            int latestVersionPos = downloadedPage.IndexOf(prefix, StringComparison.Ordinal);
            if (latestVersionPos < 0)
            {
                UpdateStatsDirectStatus("Could not locate StatsDirect version on update page. Please check manually at www.statsdirect.com/update.aspx");
                return;
            }
            string latestVersionLine = downloadedPage.Substring(latestVersionPos + prefix.Length);
            int versionsEndPos = latestVersionLine.IndexOf("<br");
            if (versionsEndPos < 0)
            {
                UpdateStatsDirectStatus("Could not locate StatsDirect version on update page. Please check manually at www.statsdirect.com/update.aspx");
                return;
            }
            latestVersionLine = latestVersionLine.Substring(0, versionsEndPos - 1);
            MajorMinorPoint availableVersion = GetVersion3(latestVersionLine);
            if (null == availableVersion || !availableVersion.IsValid)
            {
                UpdateStatsDirectStatus("Could not locate a version of StatsDirect 3 on update page. Please check manually at www.statsdirect.com/update.aspx");
                return;
            }
            MajorMinorPoint installedVersion = new MajorMinorPoint(Application.ProductVersion);
            // isNewer = true; // useful for testing without updating the web site!
            if (installedVersion < availableVersion)
            {
                lblStatsDirectStatus.Text = "You are presently running StatsDirect version " + installedVersion + ". Version " + availableVersion.ToString() +
                                    " is available. Would you like to close StatsDirect and install the new version?";
                cmdUpdateStatsDirect.Visible = true;
                lblWhatsNew.Visible = true;
            }
            else
            {
                lblStatsDirectStatus.Text = "You are presently running StatsDirect version " + installedVersion +
                                    ". You have the latest version of StatsDirect.";
            }
        }

        private void DownloadRStringCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            Application.UseWaitCursor = false;
            const string prefix = "URL=R-";
            if (e.Cancelled)
                return;
            if (null != e.Error)
            {
                UpdateRStatus("Could not check for R updates. Please check your Internet connection.");
                return;
            }
            // Success - look for the version
            string downloadedPage = e.Result;
            int latestVersionPos = downloadedPage.IndexOf(prefix, StringComparison.Ordinal);
            if (latestVersionPos < 0)
            {
                UpdateRStatus("Could not locate R version on download page. Please check manually at http://cran.r-project.org");
                return;
            }
            string latestVersionLine = downloadedPage.Substring(latestVersionPos + prefix.Length);
            int versionsEndPos = latestVersionLine.IndexOf("-win");
            if (versionsEndPos < 0)
            {
                UpdateRStatus("Could not locate R version on download page. Please check manually at http://cran.r-project.org");
                return;
            }
            latestVersionLine = latestVersionLine.Substring(0, versionsEndPos);

            MajorMinorPoint downloadableVersion = new MajorMinorPoint(latestVersionLine);
            bool downloadableIsNewer = true;
            MajorMinorPoint latestInstalledVersion = null;
            foreach (RVersion version in RController.CheckR())
            {
                MajorMinorPoint installedVersion = new MajorMinorPoint(version.Version);
                if (!(downloadableVersion > installedVersion))
                    downloadableIsNewer = false;
                if (null == latestInstalledVersion || installedVersion > latestInstalledVersion)
                    latestInstalledVersion = installedVersion;
            }
            if (downloadableIsNewer)
            {
                string currentR = null == latestInstalledVersion ? "You do not have R installed." : "You are presently running R version " + latestInstalledVersion.ToString() + ".";
                lblRStatus.Text = currentR + " Version " + latestVersionLine + " is available. Would you like to download it?";
                cmdDownloadR.Visible = true;
            }
            else
            {
                lblRStatus.Text = "You are presently running the latest R version.";
            }
        }

        private MajorMinorPoint GetVersion3(string latestVersionLine)
        {
            Regex versionSpotter = new Regex("[0-9]+\\.[0-9]+\\.[0-9]+");
            MatchCollection matches = versionSpotter.Matches(latestVersionLine);
            if (null == matches || matches.Count == 0)
                return null;
            foreach (Match m in matches)
            {
                MajorMinorPoint version = new MajorMinorPoint(m.Value);
                if (version.IsValid && version.Major == 3)
                    return version;
            }
            return null;
        }

        private void lblWhatsNew_Click(object sender, EventArgs e)
        {
            Process.Start("http://www.statsdirect.com/Revisions.aspx");
        }

        private void cmdDownloadR_Click(object sender, EventArgs e)
        {
            Process.Start("http://cran.r-project.org/bin/windows/base/release.htm");
            Close(); // See #1031; no point leaving the form here.
        }
    }

    class MajorMinorPoint
    {
        public int Major { get; set; }
        public int Minor { get; set; }
        public int Point { get; set; }
        public bool IsValid { get; set; }

        public MajorMinorPoint()
        {
        }

        public MajorMinorPoint(string s)
        {
            int major;
            int minor;
            int point;
            string[] parts = s.Split('.');
            if (parts.Length >= 3 && int.TryParse(parts[0], out major) && int.TryParse(parts[1], out minor) && int.TryParse(parts[2], out point))
            {
                Major = major;
                Minor = minor;
                Point = point;
                IsValid = true;
            }
        }

        public override string ToString()
        {
            return IsValid ? (Major.ToString() + "." + Minor.ToString() + "." + Point.ToString()) : "(invalid)";
        }

        public static bool operator <(MajorMinorPoint lhs, MajorMinorPoint rhs)
        {
            if (!(lhs.IsValid && rhs.IsValid))
                return false;
            return lhs.Major != rhs.Major
                ? lhs.Major < rhs.Major
                : lhs.Minor != rhs.Minor
                    ? lhs.Minor < rhs.Minor
                    : lhs.Point < rhs.Point;
        }

        public static bool operator >(MajorMinorPoint lhs, MajorMinorPoint rhs)
        {
            if (!(lhs.IsValid && rhs.IsValid))
                return false;
            return lhs.Major != rhs.Major
                ? lhs.Major > rhs.Major
                : lhs.Minor != rhs.Minor
                    ? lhs.Minor > rhs.Minor
                    : lhs.Point > rhs.Point;
        }
    }
}
