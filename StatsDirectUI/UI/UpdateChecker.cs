using StatsDirect.R;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace StatsDirect.UI
{
    delegate void StatusChangedEventHandler(object sender, UpdateCheckerEventArgs e);

    abstract class UpdateChecker
    {
        private Uri Uri { get; set; }
        private string DnsDomain { get; set; }
        private WebClient webClient;
        private event StatusChangedEventHandler statusChanged;

        protected void StartCheck(Uri uri, StatusChangedEventHandler handler)
        {
            Uri = uri;
            DnsDomain = uri.DnsSafeHost;
            if (null != handler)
                statusChanged += handler;
            // Just using DownloadStringAsync can block on DNS resolution; so perform async DNS resolution for cran.r-project.org
            Dns.BeginGetHostAddresses(DnsDomain, DnsCompleted, null);
        }

        public void StopCheck()
        {
            if (null != webClient)
                webClient.CancelAsync();
        }

        private void DnsCompleted(IAsyncResult ar)
        {
            if (!ar.IsCompleted)
            {
                UpdateStatus(true, false, false, "Could not resolve " + DnsDomain + "; are you connected to a network?");
                return;
            }
            try
            {
                var addresses = Dns.EndGetHostAddresses(ar);
                UpdateStatus(false, false, false, "Contacting " + DnsDomain + "...");

                // Uri numericUri = new UriBuilder(Uri.Scheme, addresses[0].ToString(), Uri.Port, Uri.AbsolutePath, Uri.Query).Uri; // Can't use this as R, at least, needs host headers intact.
                webClient = new WebClient();
                webClient.DownloadStringCompleted += DownloadStringCompleted;
                webClient.DownloadStringAsync(Uri);
            }
            catch (SocketException)
            {
                UpdateStatus(true, false, false, "Could not resolve " + DnsDomain + "; are you connected to a network?");
            }
        }

        protected void UpdateStatus(bool isFinal, bool successful, bool newerVersionAvailable, string message)
        {
            if (null != statusChanged)
                statusChanged.Invoke(this, new UpdateCheckerEventArgs { IsFinal = isFinal, Succeeded = successful, NewerVersionAvailable = newerVersionAvailable, Message = message });
        }

        protected abstract void DownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e);
    }
}
