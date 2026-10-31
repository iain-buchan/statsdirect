using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace StatsDirect.R;

internal readonly record struct RDownloadProgress(long Received, long? Total);

/// <summary>The reviewed official installer. Update the URL and hash together after checking CRAN and Authenticode.</summary>
internal sealed class RInstallation : IDisposable
{
    internal const string Version = "4.6.2";
    internal const string Series = "4.6";   // the series the R console's worker takes: a new series needs the worker rebuilt and this pin moved
    internal static readonly Uri DownloadUri = new("https://cran.r-project.org/bin/windows/base/R-4.6.2-win.exe");
    internal const string Sha256 = "b0b0053baa1963b6d8a28c9642d50107ef9ce2f41c8b9a90e952846c8918b38a";
    internal const long MaximumBytes = 150 * 1024 * 1024;
    private readonly string folder = Directory.CreateTempSubdirectory("StatsDirect-R-").FullName;
    private bool launched;
    internal string FilePath => Path.Combine(folder, "R-" + Version + "-win.exe");

    internal async Task DownloadAsync(HttpClient client, IProgress<RDownloadProgress> progress, CancellationToken token)
    {
        using var response = await client.GetAsync(DownloadUri, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.OK || response.RequestMessage?.RequestUri != DownloadUri)
            throw new IOException("CRAN did not return the expected R installer. Please retry or use the official R website.");
        long? total = response.Content.Headers.ContentLength;
        if (total is <= 0 or > MaximumBytes) throw new IOException("The R installer has an unexpected size.");
        long received = 0;
        try
        {
            await using (var input = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false))
            await using (var output = new FileStream(FilePath + ".partial", FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            {
                byte[] buffer = new byte[81920];
                var throttle = Stopwatch.StartNew();
                int count;
                while ((count = await input.ReadAsync(buffer, token).ConfigureAwait(false)) != 0)
                {
                    received += count;
                    if (received > MaximumBytes) throw new IOException("The R download is larger than expected.");
                    await output.WriteAsync(buffer.AsMemory(0, count), token).ConfigureAwait(false);
                    if (throttle.ElapsedMilliseconds >= 100)
                    {
                        progress?.Report(new(received, total));
                        throttle.Restart();
                    }
                }
            }
            if (received == 0 || total.HasValue && total.Value != received) throw new IOException("The R download was incomplete. Please retry.");
            await VerifyHashAsync(FilePath + ".partial", Sha256, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            VerifySignature(FilePath + ".partial");
            token.ThrowIfCancellationRequested();
            File.Move(FilePath + ".partial", FilePath);
            try { await File.WriteAllTextAsync(FilePath + ":Zone.Identifier", "[ZoneTransfer]\r\nZoneId=3\r\nHostUrl=" + DownloadUri + "\r\n", token).ConfigureAwait(false); }
            catch (IOException) { }
            progress?.Report(new(received, total));
        }
        catch { File.Delete(FilePath + ".partial"); throw; }
    }

    internal static async Task VerifyHashAsync(string path, string expected, CancellationToken token)
    {
        await using var file = File.OpenRead(path);
        if (file.Length == 0 || file.Length > MaximumBytes) throw new IOException("The R download has an unexpected size.");
        string actual = Convert.ToHexString(await SHA256.HashDataAsync(file, token).ConfigureAwait(false));
        if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
            throw new IOException("The R download failed its integrity check. It has not been opened. Please retry.");
    }

    internal static void VerifySignature(string path)
    {
        var file = new TrustFile { Size = (uint)Marshal.SizeOf<TrustFile>(), Path = path };
        IntPtr memory = Marshal.AllocHGlobal(Marshal.SizeOf<TrustFile>());
        try
        {
            Marshal.StructureToPtr(file, memory, false);
            var data = new TrustData { Size = (uint)Marshal.SizeOf<TrustData>(), Ui = 2, UnionChoice = 1, File = memory, Revocation = 1, Flags = 0x80 };
            Guid action = new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
            int result = WinVerifyTrust(new IntPtr(-1), ref action, ref data);
            // CertificateLoader does not expose Authenticode extraction; trust itself is checked by WinVerifyTrust above.
#pragma warning disable SYSLIB0057
            using var certificate = new X509Certificate2(X509Certificate.CreateFromSignedFile(path));
#pragma warning restore SYSLIB0057
            if (result != 0 || certificate.GetNameInfo(X509NameType.SimpleName, false) != "Martyn Plummer")
                throw new IOException("The R installer's publisher signature could not be verified. It has not been opened.");
        }
        finally { Marshal.DestroyStructure<TrustFile>(memory); Marshal.FreeHGlobal(memory); }
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct TrustFile { internal uint Size; [MarshalAs(UnmanagedType.LPWStr)] internal string Path; internal IntPtr Handle, Subject; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct TrustData { internal uint Size; internal IntPtr Policy, Client; internal uint Ui, Revocation, UnionChoice; internal IntPtr File; internal uint State; internal IntPtr StateData, Url; internal uint Flags, Context; internal IntPtr SignatureSettings; }
    [DllImport("wintrust.dll", ExactSpelling = true)]
    private static extern int WinVerifyTrust(IntPtr window, ref Guid action, ref TrustData data);

    internal Process Start()
    {
        VerifySignature(FilePath);
        var process = Process.Start(new ProcessStartInfo(FilePath) { UseShellExecute = true, WorkingDirectory = folder });
        launched = true;
        return process;
    }

    internal static Task<bool> ReadyAsync(CancellationToken token) => ReadyAsync(RController.PreferredRVersion(), token);

    /// <summary>Whether this installed R answers; false for none.  The console asks for the R of its worker's series, the runner for the newest.</summary>
    internal static async Task<bool> ReadyAsync(RVersion version, CancellationToken token)
    {
        if (version == null) return false;
        var start = new ProcessStartInfo(Path.Combine(version.BinPath, "Rscript.exe"))
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("--vanilla"); start.ArgumentList.Add("-e"); start.ArgumentList.Add("cat('STATSDIRECT_R_READY')");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            using var process = Process.Start(start);
            try
            {
                var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
                var errors = process.StandardError.ReadToEndAsync(timeout.Token);
                await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
                await errors;
                return process.ExitCode == 0 && (await output).Contains("STATSDIRECT_R_READY");
            }
            finally { if (!process.HasExited) process.Kill(true); }
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { return false; }
        catch (System.ComponentModel.Win32Exception) { return false; }
    }

    public void Dispose()
    {
        if (launched) return;
        try { File.Delete(FilePath + ".partial"); File.Delete(FilePath); Directory.Delete(folder); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
