using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace StatsDirect.UI
{
    internal readonly record struct UpdateDownloadProgress(long BytesReceived, long? TotalBytes);

    /// <summary>A completed installer download, kept until the installer has started.</summary>
    internal sealed class UpdateInstaller : IDisposable
    {
        private const int MaximumRedirects = 10;
        private const string NotSecure = "The installer download did not use a secure connection.";
        private const string WrongSize = "The file at the installer's address is not the size published for this update, so it has not been opened.";

        private readonly string directory;
        private bool started;

        private UpdateInstaller(string directory)
        {
            this.directory = directory;
            FilePath = Path.Combine(directory, UpdateOffer.InstallerName);
        }

        internal string FilePath { get; }

        /// <summary>
        /// The client the updater downloads with.  It follows no redirect itself: <see cref="DownloadAsync"/> follows each one,
        /// so that every address on the way is seen.  The whole transfer is timed by the caller's cancellation token.
        /// </summary>
        internal static HttpClient CreateClient() =>
            new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = Timeout.InfiniteTimeSpan };

        /// <summary>
        /// Downloads the installer of an offer and checks it.  The file kept is exactly the one the offer describes: of its
        /// size and its SHA-256, fetched from its address over https all the way, signed by StatsDirect Limited, and the
        /// version offered.  Anything else throws, and nothing is kept.
        /// </summary>
        /// <param name="verify">The check made on the completed file before it is offered for starting; the updater's own
        /// check (<see cref="UpdateInstallerCheck.VerifyForRunningProgram"/>) when null.  Only tests of the transfer pass another.</param>
        internal static async Task<UpdateInstaller> DownloadAsync(HttpClient client, UpdateOffer offer,
            IProgress<UpdateDownloadProgress> progress, CancellationToken cancellationToken, Action<string> verify = null)
        {
            ArgumentNullException.ThrowIfNull(offer);
            var installer = new UpdateInstaller(Directory.CreateTempSubdirectory("StatsDirect-update-").FullName);
            try
            {
                using HttpResponseMessage response = await GetAsync(client, offer.Url, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                // A stated length that is not the offer's is refused before anything is fetched.
                if (response.Content.Headers.ContentLength is long stated && stated != offer.Size)
                    throw new IOException(WrongSize);

                long received = 0;
                progress?.Report(new UpdateDownloadProgress(0, offer.Size));
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                await using (Stream source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
                await using (var destination = new FileStream(installer.FilePath + ".partial", FileMode.CreateNew,
                    FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    byte[] buffer = new byte[81920];
                    var lastProgress = Stopwatch.StartNew();
                    int count;
                    while ((count = await source.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) != 0)
                    {
                        received += count;
                        // Nothing longer than the offer is written, whatever length was stated.
                        if (received > offer.Size)
                            throw new IOException(WrongSize);
                        hash.AppendData(buffer, 0, count);
                        await destination.WriteAsync(buffer.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
                        if (lastProgress.ElapsedMilliseconds >= 100)
                        {
                            progress?.Report(new UpdateDownloadProgress(received, offer.Size));
                            lastProgress.Restart();
                        }
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (received != offer.Size)
                    throw new IOException("The installer download was incomplete. Please try again.");
                // The bytes received are the bytes published: the SHA-256 is taken as they arrive, and it is the offer's.
                if (!string.Equals(Convert.ToHexStringLower(hash.GetHashAndReset()), offer.Sha256, StringComparison.Ordinal))
                    throw new IOException("The file downloaded is not the installer published for this update, so it has not been opened.");

                // Catch an HTML error page returned with HTTP 200 before offering it to the shell.
                // Windows performs the executable's trust checks when it is launched.
                using (var file = File.OpenRead(installer.FilePath + ".partial"))
                    if (file.ReadByte() != 'M' || file.ReadByte() != 'Z')
                        throw new IOException("The download was not a Windows installer. Please try again.");

                File.Move(installer.FilePath + ".partial", installer.FilePath);
                // Nothing is offered for starting unless it is ours: signed by StatsDirect Limited, a newer StatsDirect than the
                // one running, and the version offered.  A file that fails is removed with its folder, as any failed download is.
                verify ??= path => UpdateInstallerCheck.VerifyForRunningProgram(path, offer.Version);
                await Task.Run(() => verify(installer.FilePath), cancellationToken).ConfigureAwait(false);
                // Preserve the Internet origin, as a browser download would, so the normal Windows
                // downloaded-file security checks still apply when ShellExecute opens the EXE. A temp folder that cannot
                // hold the mark (not NTFS) does not stop the update.
                try
                {
                    await File.WriteAllTextAsync(installer.FilePath + ":Zone.Identifier",
                        "[ZoneTransfer]\r\nZoneId=3\r\nHostUrl=" + offer.Url.AbsoluteUri + "\r\n",
                        Encoding.ASCII, cancellationToken).ConfigureAwait(false);
                }
                catch (IOException ex)
                {
                    Utilities.DiagnosticLog.Write("Installer zone mark not written: " + ex.Message);
                }
                catch (UnauthorizedAccessException ex)
                {
                    Utilities.DiagnosticLog.Write("Installer zone mark not written: " + ex.Message);
                }
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new UpdateDownloadProgress(received, offer.Size));
                return installer;
            }
            catch
            {
                installer.Dispose();
                throw;
            }
        }

        // The offer's address is GitHub's lasting one for the release's file, which GitHub answers with a redirect to where it
        // holds the file today.  Each redirect is followed here, not by the client, so that every address on the way is seen to
        // be https.  The hosts on the way are not fixed: GitHub has moved its files between hosts before, and an updater that
        // knew only the old ones could no longer update itself.  What is kept is decided by the offer's size and SHA-256 and by
        // the signature, whichever host sent it.  The caller disposes the answer returned.
        private static async Task<HttpResponseMessage> GetAsync(HttpClient client, Uri address, CancellationToken cancellationToken)
        {
            for (int redirects = 0; ; redirects++)
            {
                if (address.Scheme != Uri.UriSchemeHttps)
                    throw new IOException(NotSecure);
                HttpResponseMessage response = await client.GetAsync(address, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                bool answer = false;
                try
                {
                    // A client that follows redirects itself reports the address it ended on.
                    Uri reached = response.RequestMessage?.RequestUri ?? address;
                    if (reached.Scheme != Uri.UriSchemeHttps)
                        throw new IOException(NotSecure);
                    if ((int)response.StatusCode is not (301 or 302 or 303 or 307 or 308))
                    {
                        answer = true;
                        return response;
                    }
                    if (response.Headers.Location is not Uri location)
                        throw new IOException("The installer download was redirected without an address.");
                    if (redirects >= MaximumRedirects)
                        throw new IOException("The installer download was redirected too many times.");
                    address = location.IsAbsoluteUri ? location : new Uri(reached, location);
                }
                finally
                {
                    if (!answer)
                        response.Dispose();
                }
            }
        }

        /// <summary>
        /// Removes the downloads of earlier updates from the temp folder (a download is kept for the installer it started):
        /// best effort, a folder whose file is still in use is left.
        /// </summary>
        internal static void DeleteOldDownloads()
        {
            try
            {
                foreach (string oldDirectory in Directory.GetDirectories(Path.GetTempPath(), "StatsDirect-update-*"))
                {
                    try
                    {
                        Directory.Delete(oldDirectory, true);
                    }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        internal void Start()
        {
            // Open the local EXE, not its URL: no browser is left covering the installer.
            using Process process = Process.Start(new ProcessStartInfo(FilePath)
            {
                UseShellExecute = true,
                WorkingDirectory = directory,
                WindowStyle = ProcessWindowStyle.Normal
            });
            started = true;
        }

        public void Dispose()
        {
            // The installer may still need its EXE after ShellExecute returns. Leave successful
            // downloads in the user's temp folder; remove only our own files on failure/cancellation.
            if (started)
                return;
            try
            {
                File.Delete(FilePath + ".partial");
                File.Delete(FilePath);
                Directory.Delete(directory);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
