#nullable enable

using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace StatsDirect.UI
{
    delegate void StatusChangedEventHandler(object? sender, UpdateCheckerEventArgs e);

    /// <summary>
    /// Abstract superclass for things that check a URL for updated versions. This abstracts out DNS resolution and a HTTP GET, and calls DownloadStringCompleted when done.
    /// The check is designed not to slow down a UI, so runs asynchronously once started (via StartCheck), and therefore callbacks may be run on a background thread.
    /// Clients or subclasses may hand in a status changed handler, which will get called on significant changes; this can for example be used to enable/disable buttons or update textual status.
    /// </summary>
    abstract class UpdateChecker : IDisposable
    {
        private readonly HttpClient httpClient = new();
        private event StatusChangedEventHandler? statusChanged;

        protected async Task StartCheck(Uri uri, StatusChangedEventHandler handler)
        {
            string dnsDomain = uri.DnsSafeHost;
            if (null != handler)
                statusChanged += handler;
            try
            {
                IPAddress[]? addresses = await Dns.GetHostAddressesAsync(dnsDomain);
                if (null == addresses)
                {
                    UpdateStatus(true, false, false, "Could not resolve " + dnsDomain + "; are you connected to a network?");
                    return;
                }
                UpdateStatus(false, false, false, "Contacting " + dnsDomain + "...");

                HttpResponseMessage responseMessage = await httpClient.GetAsync(uri);
                await GetCompleted(responseMessage);
            }
            catch (SocketException)
            {
                UpdateStatus(true, false, false, "Could not resolve " + dnsDomain + "; are you connected to a network?");
            }
        }

        public void StopCheck()
        {
            httpClient?.CancelPendingRequests();
        }

        protected void UpdateStatus(bool isFinal, bool successful, bool newerVersionAvailable, string message)
        {
            statusChanged?.Invoke(this, new UpdateCheckerEventArgs { IsFinal = isFinal, Succeeded = successful, NewerVersionAvailable = newerVersionAvailable, Message = message });
        }

        /// <summary>
        /// Invoked when the HTTP request completes or times out.  Subclasses should implement to do what they need with the result.
        /// </summary>
        protected abstract Task GetCompleted(HttpResponseMessage response);

        private bool disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                    httpClient?.Dispose();

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
    }
}
