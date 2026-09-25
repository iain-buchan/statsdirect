using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace StatsDirect.UI
{
    internal readonly record struct UpdateDownloadProgress(long BytesReceived, long? TotalBytes);

    /// <summary>A completed installer download, kept until the installer has started.</summary>
    internal sealed class UpdateInstaller : IDisposable
    {
        internal static readonly Uri DownloadUri = new("https://www.statsdirect.com/download/StatsDirectSetup.exe");

        private readonly string directory;
        private bool started;

        private UpdateInstaller(string directory)
        {
            this.directory = directory;
            FilePath = Path.Combine(directory, "StatsDirectSetup.exe");
        }

        internal string FilePath { get; }

        internal static async Task<UpdateInstaller> DownloadAsync(HttpClient client,
            IProgress<UpdateDownloadProgress> progress, CancellationToken cancellationToken)
        {
            var installer = new UpdateInstaller(Directory.CreateTempSubdirectory("StatsDirect-update-").FullName);
            try
            {
                using HttpResponseMessage response = await client.GetAsync(DownloadUri,
                    HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                // Do not accept a redirect that downgrades the official HTTPS download.
                if (response.RequestMessage?.RequestUri?.Scheme != Uri.UriSchemeHttps)
                    throw new IOException("The installer download did not use a secure connection.");

                long? total = response.Content.Headers.ContentLength;
                long received = 0;
                progress?.Report(new UpdateDownloadProgress(0, total));
                await using (Stream source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
                await using (var destination = new FileStream(installer.FilePath + ".partial", FileMode.CreateNew,
                    FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    byte[] buffer = new byte[81920];
                    var lastProgress = Stopwatch.StartNew();
                    int count;
                    while ((count = await source.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) != 0)
                    {
                        await destination.WriteAsync(buffer.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
                        received += count;
                        if (lastProgress.ElapsedMilliseconds >= 100)
                        {
                            progress?.Report(new UpdateDownloadProgress(received, total));
                            lastProgress.Restart();
                        }
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (received < 2 || (total.HasValue && received != total.Value))
                    throw new IOException("The installer download was incomplete. Please try again.");

                // Catch an HTML error page returned with HTTP 200 before offering it to the shell.
                // Windows performs the executable's trust checks when it is launched.
                using (var file = File.OpenRead(installer.FilePath + ".partial"))
                    if (file.ReadByte() != 'M' || file.ReadByte() != 'Z')
                        throw new IOException("The download was not a Windows installer. Please try again.");

                File.Move(installer.FilePath + ".partial", installer.FilePath);
                // Preserve the Internet origin, as a browser download would, so the normal Windows
                // downloaded-file security checks still apply when ShellExecute opens the EXE. A temp folder that cannot
                // hold the mark (not NTFS) does not stop the update.
                try
                {
                    await File.WriteAllTextAsync(installer.FilePath + ":Zone.Identifier",
                        "[ZoneTransfer]\r\nZoneId=3\r\nHostUrl=" + DownloadUri.AbsoluteUri + "\r\n",
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
                progress?.Report(new UpdateDownloadProgress(received, total));
                return installer;
            }
            catch
            {
                installer.Dispose();
                throw;
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
