using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
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
            const string prefix = "Current version ";
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
            string latestVersion = downloadedPage.Substring(latestVersionPos + prefix.Length);
            int versionEndPos = latestVersion.IndexOf(' ');
            if (versionEndPos < 0)
            {
                UpdateStatus("Could not locate version on update page. Please check manually at www.statsdirect.com/update.aspx");
                return;
            }
            latestVersion = latestVersion.Substring(0, versionEndPos - 1);
            string currentVersion = Application.ProductVersion;
            int lastDotPos = currentVersion.LastIndexOf('.');
            if (lastDotPos > 0)
                currentVersion = currentVersion.Substring(0, lastDotPos);
            // Check versions
            bool isNewer = false;
            string[] splitCurrentVersion = currentVersion.Split('.');
            string[] splitLatestVersion = latestVersion.Split('.');
            for (int i = 0; i < Math.Min(splitCurrentVersion.Length, splitLatestVersion.Length); i++ )
            {
                int currentValue;
                int latestValue;
                if (!(int.TryParse(splitCurrentVersion[i], out currentValue) && int.TryParse(splitLatestVersion[i], out latestValue)))
                {
                    UpdateStatus("Could not locate version on update page. Please check manually at www.statsdirect.com/update.aspx");
                    return;
                }
                if (latestValue > currentValue)
                {
                    isNewer = true;
                    break;
                }
                if (latestValue < currentValue)
                {
                    // We're newer
                    break;
                }
                // Check the next part - the numbers are neck-and-neck to here.  If there are no more values, we're equal to the latest version, so we'll exit the loop with isNewer = false.
            }
            // isNewer = true; // useful for testing without updating the web site!
            if (isNewer)
            {
                lblStatus.Text = "You are presently running version " + currentVersion + ". Version " + latestVersion +
                                    " is available. Would you like to close StatsDirect and install the new version?";
                cmdClose.Text = "Cancel";
                button1.Visible = true;
                lblWhatsNew.Visible = true;
            }
            else
            {
                lblStatus.Text = "You are presently running version " + currentVersion +
                                    ". You have the latest version of StatsDirect.";
            }
        }

        private void lblWhatsNew_Click(object sender, EventArgs e)
        {
            Process.Start("http://www.statsdirect.com/Revisions.aspx");
        }
    }
}
