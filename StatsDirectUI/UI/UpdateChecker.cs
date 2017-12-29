using System;
using System.Net;
using System.Net.Sockets;

namespace StatsDirect.UI
{
    delegate void StatusChangedEventHandler(object sender, UpdateCheckerEventArgs e);

    /// <summary>
    /// Abstract superclass for things that check a URL for updated versions. This abstracts out DNS resolution and a HTTP GET, and calls DownloadStringCompleted when done.
    /// The check is designed not to slow down a UI, so runs asynchronously once started (via StartCheck), and therefore callbacks may be run on a background thread.
    /// Clients or subclasses may hand in a status changed handler, which will get called on significant changes; this can for example be used to enable/disable buttons or update textual status.
    /// </summary>
    abstract class UpdateChecker : IDisposable
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
            // Just using DownloadStringAsync can block on DNS resolution; so perform async DNS resolution.
            Dns.BeginGetHostAddresses(DnsDomain, DnsCompleted, null);
        }

        public void StopCheck()
        {
            if (null != webClient)
                webClient.CancelAsync();
        }

        /// <summary>
        /// Invoked when DNS resolution completes or times out.
        /// </summary>
        private void DnsCompleted(IAsyncResult ar)
        {
            if (!ar.IsCompleted)
            {
                UpdateStatus(true, false, false, "Could not resolve " + DnsDomain + "; are you connected to a network?");
                return;
            }
            try
            {
                Dns.EndGetHostAddresses(ar);
                UpdateStatus(false, false, false, "Contacting " + DnsDomain + "...");

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

        /// <summary>
        /// Invoked when the HTTP request completes or times out.  Subclasses should implement to do what they need with the result.
        /// </summary>
        protected abstract void DownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e);

        private bool disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    webClient?.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
    }
}
