using System.Net;
using System.Net.Http;
using StatsDirect.UI;

internal static class Program
{
    private static int passed;
    // The transfer checks use made-up bytes, which no publisher signed; they pass this in place of the updater's own check.
    private static readonly Action<string> NoCheck = _ => { };

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            if (args.Contains("--download-only"))
            {
                using var client = new HttpClient();
                using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
                using UpdateInstaller installer = UpdateInstaller.DownloadAsync(client, null, timeout.Token).GetAwaiter().GetResult();
                Console.WriteLine($"Downloaded {new FileInfo(installer.FilePath).Length:N0} bytes from {UpdateInstaller.DownloadUri}. It is signed by {UpdateInstallerCheck.Signer(installer.FilePath)} and passed the updater's own check. File has Internet-zone metadata. No installer launched; test download removed on exit.");
                return 0;
            }
            int verify = Array.IndexOf(args, "--verify");
            if (verify >= 0)
            {
                // The release check: would the updater of the version given start this installer?
                string path = Path.GetFullPath(args[verify + 1]);
                int runningAt = Array.IndexOf(args, "--running");
                Version running = runningAt >= 0 ? Version.Parse(args[runningAt + 1]) : new Version(0, 0, 0);
                UpdateInstallerCheck.Verify(path, running);
                Console.WriteLine($"PASS: {path} is signed by {UpdateInstallerCheck.Signer(path)} and is a newer StatsDirect than {running}: the updater would start it. Nothing was launched.");
                return 0;
            }
            if (args.Contains("--preview"))
            {
                ApplicationConfiguration.Initialize();
                using var preview = new frmDownloadUpdate(async (progress, token) =>
                {
                    for (int i = 0; ; i = (i + 1) % 100)
                    {
                        progress.Report(new UpdateDownloadProgress(i * 1048576L, 100 * 1048576L));
                        await Task.Delay(1000, token);
                    }
                });
                preview.ShowDialog();
                return 0;
            }

            RunDownloads().GetAwaiter().GetResult();
            RunInstallerChecks();
            ApplicationConfiguration.Initialize();
            RunDialogs();
            Console.WriteLine($"PASS: {passed} update download and dialog checks. No installer was launched.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static async Task RunDownloads()
    {
        byte[] bytes = new byte[200000];
        new Random(42).NextBytes(bytes);
        bytes[0] = (byte)'M'; bytes[1] = (byte)'Z';
        string savedPath;
        var progress = new RecordedProgress();
        using (var client = Client(new ByteArrayContent(bytes)))
        using (UpdateInstaller installer = await UpdateInstaller.DownloadAsync(client, progress, CancellationToken.None, NoCheck))
        {
            savedPath = installer.FilePath;
            Check(File.ReadAllBytes(savedPath).SequenceEqual(bytes), "Complete binary preserved byte for byte");
            Check(!File.Exists(savedPath + ".partial"), "Only a completed installer is offered for launch");
            string zone = File.ReadAllText(savedPath + ":Zone.Identifier");
            Check(zone.Contains("ZoneId=3\r\n") && zone.Contains("HostUrl=" + UpdateInstaller.DownloadUri.AbsoluteUri),
                "Downloaded file retains Internet zone and source");
            Check(progress.Values.First().BytesReceived == 0 && progress.Values.Last() == new UpdateDownloadProgress(bytes.Length, bytes.Length),
                "Progress includes the start and completed content length");
        }
        Check(!Directory.Exists(Path.GetDirectoryName(savedPath)), "Unlaunched download removed when a save/close is cancelled");

        using (var client = Client(new TestContent(new NonSeekableStream(bytes))))
        using (UpdateInstaller installer = await UpdateInstaller.DownloadAsync(client, null, CancellationToken.None, NoCheck))
            Check(File.ReadAllBytes(installer.FilePath).SequenceEqual(bytes), "Download works without Content-Length");

        // With the updater's own check, a complete download that StatsDirect Limited did not sign is not kept.
        await Reject("Complete download not signed by StatsDirect Limited", Client(new ByteArrayContent(bytes)));
        await Reject("HTTP failure", Client(new StringContent("Not found"), HttpStatusCode.NotFound));
        await Reject("HTML returned with status 200", Client(new StringContent("<html>Temporarily unavailable</html>")));
        await Reject("Empty response", Client(new ByteArrayContent([])));
        await Reject("One-byte response", Client(new ByteArrayContent([(byte)'M'])));
        var truncated = new ByteArrayContent(bytes); truncated.Headers.ContentLength = bytes.Length + 500;
        await Reject("Truncated response", Client(truncated));
        var excess = new ByteArrayContent(bytes); excess.Headers.ContentLength = bytes.Length - 500;
        await Reject("Incorrect content length", Client(excess));
        await Reject("Insecure redirect rejected", Client(new ByteArrayContent(bytes), redirect: new Uri("http://www.statsdirect.com/download/StatsDirectSetup.exe")));
        await Reject("Read failure after partial transfer", Client(new TestContent(new FailingStream(bytes))));

        using var alreadyCancelled = new CancellationTokenSource();
        alreadyCancelled.Cancel();
        await Reject("Cancellation before download", Client(new ByteArrayContent(bytes)), alreadyCancelled.Token);

        using var duringRead = new CancellationTokenSource();
        duringRead.CancelAfter(150);
        await Reject("Cancellation during response body", Client(new TestContent(new WaitingStream())), duringRead.Token);

        using var afterRead = new CancellationTokenSource();
        var cancelOnEof = new TestContent(new CancelAtEndStream(bytes, afterRead));
        await Reject("Cancellation at end of response", Client(cancelOnEof), afterRead.Token);
    }

    private static void RunDialogs()
    {
        using (var dialog = new frmDownloadUpdate(async (progress, token) =>
        {
            using var client = Client(new ByteArrayContent([(byte)'M', (byte)'Z', 0, 0]));
            return await UpdateInstaller.DownloadAsync(client, progress, token, NoCheck);
        }))
        {
            dialog.Shown += (_, _) =>
            {
                Control layout = dialog.Controls[0];
                Check(layout.Controls.Cast<Control>().All(c => layout.ClientRectangle.Contains(c.Bounds)),
                    $"Progress text and Cancel button fit the dialog at {dialog.DeviceDpi} DPI");
            };
            Check(dialog.ShowDialog() == DialogResult.OK && dialog.Installer != null, "Dialog returns a completed download");
            using UpdateInstaller installer = dialog.Installer;
        }

        bool cancelled = false;
        using (var dialog = new frmDownloadUpdate(async (progress, token) =>
        {
            try { await Task.Delay(Timeout.Infinite, token); }
            catch (OperationCanceledException) { cancelled = true; throw; }
            return null;
        }))
        using (var timer = new System.Windows.Forms.Timer { Interval = 150 })
        {
            timer.Tick += (_, _) => { timer.Stop(); dialog.Close(); };
            timer.Start();
            Check(dialog.ShowDialog() == DialogResult.Cancel && dialog.Installer == null && cancelled,
                "Closing the dialog cancels and waits for the transfer");
        }
    }

    // The updater's check itself, on real files: one with no signature, one validly signed by another publisher and, where
    // this machine has an installed StatsDirect signed by StatsDirect Limited, one of ours.
    private static void RunInstallerChecks()
    {
        string own = Environment.ProcessPath;
        Check(UpdateInstallerCheck.Signer(own) == null && Refused(own, new Version(0, 0, 0), "not signed by StatsDirect Limited"), "A file with no signature is refused");
        string dotnet = Path.GetFullPath(Path.Combine(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", "..", "dotnet.exe"));
        if (File.Exists(dotnet) && UpdateInstallerCheck.Signer(dotnet) is string other)
            Check(other != UpdateInstallerCheck.Publisher && Refused(dotnet, new Version(0, 0, 0), "not signed by StatsDirect Limited"), $"A file validly signed by another publisher ({other}) is refused");
        else
            Console.WriteLine("SKIP: no validly signed dotnet.exe beside the runtime to stand for another publisher");
        string installed = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "StatsDirect", "StatsDirect.exe");
        if (File.Exists(installed) && UpdateInstallerCheck.Signer(installed) == UpdateInstallerCheck.Publisher)
        {
            UpdateInstallerCheck.Verify(installed, new Version(0, 0, 0));
            passed++;
            Console.WriteLine("PASS: A StatsDirect file signed by StatsDirect Limited and newer than the version running is accepted");
            Check(Refused(installed, new Version(999, 0, 0), "not a newer version of StatsDirect"), "A signed StatsDirect file that is not newer than the version running is refused");
        }
        else
            Console.WriteLine("SKIP: no installed StatsDirect signed by StatsDirect Limited on this machine; a release is checked with --verify");
    }

    private static bool Refused(string path, Version running, string reason)
    {
        try { UpdateInstallerCheck.Verify(path, running); return false; }
        catch (IOException ex) { return ex.Message.Contains(reason); }
    }

    private static async Task Reject(string name, HttpClient client, CancellationToken token = default)
    {
        var before = Directory.GetDirectories(Path.GetTempPath(), "StatsDirect-update-*").ToHashSet();
        bool rejected = false;
        using (client)
        {
            try { using UpdateInstaller unexpected = await UpdateInstaller.DownloadAsync(client, null, token); }
            catch (Exception ex) when (ex is HttpRequestException or IOException or OperationCanceledException) { rejected = true; }
        }
        Check(rejected && !Directory.GetDirectories(Path.GetTempPath(), "StatsDirect-update-*").Except(before).Any(),
            name + ": no installer or partial files retained");
    }

    private static HttpClient Client(HttpContent content, HttpStatusCode status = HttpStatusCode.OK, Uri redirect = null) =>
        new(new ResponseHandler(content, status, redirect));

    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception("FAIL: " + name);
        passed++;
        Console.WriteLine("PASS: " + name);
    }

    private sealed class ResponseHandler(HttpContent content, HttpStatusCode status, Uri redirect) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri != UpdateInstaller.DownloadUri)
                throw new Exception("Unexpected download endpoint");
            cancellationToken.ThrowIfCancellationRequested();
            if (redirect != null) request.RequestUri = redirect;
            return Task.FromResult(new HttpResponseMessage(status) { Content = content, RequestMessage = request });
        }
    }

    private sealed class RecordedProgress : IProgress<UpdateDownloadProgress>
    {
        internal readonly List<UpdateDownloadProgress> Values = [];
        public void Report(UpdateDownloadProgress value) => Values.Add(value);
    }

    private sealed class TestContent(Stream source) : HttpContent
    {
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext context) => source.CopyToAsync(stream);
        protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult(source);
        protected override void Dispose(bool disposing) { if (disposing) source.Dispose(); base.Dispose(disposing); }
    }

    private class NonSeekableStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override bool CanSeek => false;
    }

    private sealed class FailingStream(byte[] bytes) : NonSeekableStream(bytes)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            Position > 0 ? ValueTask.FromException<int>(new IOException("Simulated connection loss")) : base.ReadAsync(buffer, cancellationToken);
    }

    private sealed class WaitingStream() : NonSeekableStream([])
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }
    }

    private sealed class CancelAtEndStream(byte[] bytes, CancellationTokenSource cancellation) : NonSeekableStream(bytes)
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int count = await base.ReadAsync(buffer, cancellationToken);
            if (count == 0) cancellation.Cancel();
            return count;
        }
    }
}
