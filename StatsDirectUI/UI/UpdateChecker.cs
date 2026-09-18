using ABI.System;
using System;
using System.Net;
using System.Net.Sockets;
using System.Net.Http;

namespace StatsDirect.UI
{
    delegate void StatusChangedEventHandler(object sender, UpdateCheckerEventArgs e);

    /// <summary>
    /// Abstract superclass for things that check a URL for updated versions. This abstracts out DNS resolution and a HTTP GET, and calls DownloadStringCompleted when done.
    /// The check is designed not to slow down a UI, so runs asynchronously once started (via StartCheck), and therefore callbacks may be run on a background thread.
    /// Clients or subclasses may hand in a status changed handler, which will get called on significant changes; this can for example be used to enable/disable buttons or update textual status.
    /// </summary>
    abstract class UpdateChecker : System.IDisposable
    {
        private System.Uri Uri { get; set; }
        private string DnsDomain { get; set; }
        HttpClient httpClient;
        private event StatusChangedEventHandler statusChanged;

        protected void StartCheck(System.Uri uri, StatusChangedEventHandler handler)
        {
            Uri = uri;
            DnsDomain = uri.DnsSafeHost;
            if (null != handler)
                statusChanged += handler;
            // Just using DownloadStringAsync can block on DNS resolution; so perform async DNS resolution.
            Dns.BeginGetHostAddresses(DnsDomain, DnsCompleted, null);
        }

        private volatile bool stopRequested;

        public void StopCheck()
        {
            stopRequested = true;
            if (null != httpClient)
                httpClient.CancelPendingRequests();
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

                httpClient = new HttpClient();
                string page = null;
                System.Exception error = null;
                bool cancelled = false;
                try
                {
                    page = httpClient.GetStringAsync(Uri).GetAwaiter().GetResult();
                }
                catch (System.OperationCanceledException ex)
                {
                    //  StopCheck cancels the pending request; a timeout arrives as the same exception and is an error, not a cancellation
                    cancelled = stopRequested;
                    if (!cancelled)
                        error = ex;
                }
                catch (System.Exception ex)
                {
                    error = ex;
                }

                //  The page used to be handed to a private method that only wrote it to the console, so the subclasses never saw it and no check ever finished.
                try
                {
                    PageDownloaded(page, error, cancelled);
                }
                catch (System.Exception ex)
                {
                    //  We are on a thread pool thread: an exception escaping from here would end the program
                    UpdateStatus(true, false, false, "Could not check for updates: " + ex.Message);
                }
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
        /// Invoked when the HTTP request has completed, failed or been cancelled.  Subclasses should implement to do what they need with the page.
        /// </summary>
        /// <param name="page">The downloaded page, or null if the download failed or was cancelled</param>
        /// <param name="error">Why the download failed, or null if it did not</param>
        /// <param name="cancelled">True if StopCheck cancelled the download</param>
        protected abstract void PageDownloaded(string page, System.Exception error, bool cancelled);

        private bool disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    httpClient?.Dispose();
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
