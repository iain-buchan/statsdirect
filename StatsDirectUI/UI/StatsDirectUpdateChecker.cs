using System;
using System.Net.Http;
using System.Threading;

namespace StatsDirect.UI
{
    /// <summary>
    /// The check for a newer StatsDirect.  It reads the update manifest on statsdirect.com (<see cref="UpdateOffer"/>): the
    /// version published, with the address, size and SHA-256 of its installer.
    /// <para>Versions before 5.3.17 read the version from the text of the download page ("Current version ...") and fetch the
    /// installer from www.statsdirect.com/download/StatsDirectSetup.exe.  The website keeps both for them.</para>
    /// </summary>
    class StatsDirectUpdateChecker : UpdateChecker
    {
        private static readonly TimeSpan FetchLimit = TimeSpan.FromSeconds(100);
        private volatile UpdateOffer offer;

        /// <summary>The update the last check found, for the download; null unless that check ended with a newer version.</summary>
        internal UpdateOffer Offer => offer;

        public void StartCheck(StatusChangedEventHandler handler)
        {
            offer = null;
            base.StartCheck(UpdateOffer.ManifestUri, handler);
        }

        // The whole fetch is timed, its text included: the client's own timeout ends when the answer's headers arrive.
        protected override string Fetch(HttpClient client, Uri uri)
        {
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(Stopped);
            limit.CancelAfter(FetchLimit);
            return UpdateOffer.ReadManifestAsync(client, uri, limit.Token).GetAwaiter().GetResult();
        }

        protected override void PageDownloaded(string manifest, Exception error, bool cancelled)
        {
            if (cancelled)
                return;
            UpdateCheckOutcome outcome = UpdateOffer.Evaluate(manifest, error, UpdateInstallerCheck.RunningVersion);
            if (outcome.Detail != null)
                Utilities.DiagnosticLog.Write("update check not made: " + outcome.Detail);
            // The offer is kept whole: the download fetches this version's installer and no other.
            offer = outcome.Offer;
            UpdateStatus(true, outcome.Succeeded, outcome.Offer != null, outcome.Message);
        }
    }
}
