using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace StatsDirect.UI
{
    public partial class frmUpdateCheck : Form
    {
        private WebClient client;
        public frmUpdateCheck()
        {
            InitializeComponent();
            Application.UseWaitCursor = true;
        }

        private void cmdClose_Click(object sender, EventArgs e)
        {
            if (null != client)
                client.CancelAsync();
            Close();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (null != client)
                client.CancelAsync();
            Close();
            SdApplication.SoleInstance.CloseAndUpdate();
        }

        private void frmUpdateCheck_Shown(object sender, EventArgs e)
        {
            StartCheck();
        }

        private void StartCheck()
        {
            Application.UseWaitCursor = true;
            // Just using DownloadStringAsync can block on DNS resolution; so perform async DNS resolution for www.statsdirect.com
            Dns.BeginGetHostAddresses("www.statsdirect.com", DnsCompleted, null);
        }

        private void DnsCompleted(IAsyncResult ar)
        {
            if (!ar.IsCompleted)
            {
                UpdateStatus("Could not resolve www.statsdirect.com; are you connected to a network?");
                return;
            }
            try
            {
                var addresses = Dns.EndGetHostAddresses(ar);
                client = new WebClient();
                client.DownloadStringCompleted += client_DownloadStringCompleted;
                client.DownloadStringAsync(new Uri(string.Format("http://{0}/update.aspx", addresses[0])));
            }
            catch (SocketException)
            {
                UpdateStatus("Could not resolve www.statsdirect.com; are you connected to a network?");
            }
        }

        private void UpdateStatus(string status)
        {
            Application.UseWaitCursor = false;
            if (lblStatus.InvokeRequired)
            {
                lblStatus.Invoke(new MethodInvoker(delegate { lblStatus.Text = status; }));
            }
            else
            {
                lblStatus.Text = status;
            }
        }

        private void client_DownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            Application.UseWaitCursor = false;
            const string prefix = "Current version";
            if (e.Cancelled)
                return;
            if (null != e.Error)
            {
                UpdateStatus("Could not check for updates. Please check your Internet connection.");
                return;
            }

            // Success - look for the version
            string downloadedPage = e.Result;
            int latestVersionPos = downloadedPage.IndexOf(prefix, StringComparison.Ordinal);
            if (latestVersionPos < 0)
            {
                UpdateStatus("Could not locate version on update page. Please check manually at www.statsdirect.com/update.aspx");
                return;
            }
            string latestVersionLine = downloadedPage.Substring(latestVersionPos + prefix.Length);
            int versionsEndPos = latestVersionLine.IndexOf("<br");
            if (versionsEndPos < 0)
            {
                UpdateStatus("Could not locate version on update page. Please check manually at www.statsdirect.com/update.aspx");
                return;
            }
            latestVersionLine = latestVersionLine.Substring(0, versionsEndPos - 1);
            MajorMinorPoint availableVersion = GetVersion3(latestVersionLine);
            if (null == availableVersion || !availableVersion.IsValid)
            {
                UpdateStatus("Could not locate a version of StatsDirect 3 on update page. Please check manually at www.statsdirect.com/update.aspx");
                return;
            }
            MajorMinorPoint installedVersion = new MajorMinorPoint(Application.ProductVersion);
            // isNewer = true; // useful for testing without updating the web site!
            if (installedVersion < availableVersion)
            {
                lblStatus.Text = "You are presently running version " + installedVersion + ". Version " + availableVersion.ToString() +
                                    " is available. Would you like to close StatsDirect and install the new version?";
                cmdClose.Text = "Cancel";
                button1.Visible = true;
                lblWhatsNew.Visible = true;
            }
            else
            {
                lblStatus.Text = "You are presently running version " + installedVersion +
                                    ". You have the latest version of StatsDirect.";
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
