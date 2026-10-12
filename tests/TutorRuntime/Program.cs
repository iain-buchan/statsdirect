using StatsDirect.UI.Learning;
using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using System.Reflection;

internal static class Program
{
    private static int checks;
    private static readonly string root = Path.Combine(Path.GetTempPath(), "StatsDirect-runtime-checks-" + Guid.NewGuid().ToString("N"));
    private static string Repo => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
    private static void Check(bool yes, string label) { if (!yes) throw new Exception(label); checks++; Console.WriteLine("PASS " + label); }
    private static async Task Reject<T>(Func<Task> action, string label) where T : Exception
    { try { await action(); } catch (T) { Check(true, label); return; } throw new Exception("Expected " + typeof(T).Name + ": " + label); }
    private static object Field(object obj, string name) => obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(obj)!;
    private static async Task Wait(Func<bool> done) { using var timeout = new CancellationTokenSource(5000); while (!done()) await Task.Delay(10, timeout.Token); }

    [STAThread] private static int Main()
    {
        ApplicationConfiguration.Initialize(); Directory.CreateDirectory(root);
        using var owner = new Form { Location = new(-16000, -16000), StartPosition = FormStartPosition.Manual, ShowInTaskbar = false };
        int exit = 1;
        owner.Shown += async (_, _) =>
        {
            try { await RunAsync(owner); Console.WriteLine($"PASS {checks} first-use runtime checks"); exit = 0; }
            catch (Exception ex) { Console.WriteLine("FAIL " + ex); }
            finally { owner.Close(); }
        };
        Application.Run(owner);
        // Only this test's unique direct child of Temp is removed.
        if (Path.GetDirectoryName(root) == Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar) && Path.GetFileName(root).StartsWith("StatsDirect-runtime-checks-")) Directory.Delete(root, true);
        return exit;
    }
    private static async Task RunAsync(Form owner)
    {
        string notices = Path.Combine(Repo, "StatsDirectUI/Assets/Licenses/OpenAI");
        var runtime = new TutorRuntimeInstallation(Path.Combine(root, "runtime"), notices);
        Check(!runtime.IsInstalled && !await runtime.IsValidAsync(default) && !Directory.Exists(Path.GetDirectoryName(runtime.DirectoryPath)), "checking availability performs no installation or directory creation");
        foreach (string bad in new[] { "http://github.com/file", "https://github.com.evil.invalid/file", "https://github.com:444/file", "https://user@github.com/file" })
            Check(!TutorRuntimeInstallation.AllowedDownloadUri(new(bad)), "reject untrusted download URL " + bad);
        using (var redirect = new HttpClient(new Handler(_ => { var r = new HttpResponseMessage(HttpStatusCode.Redirect); r.Headers.Location = new Uri("https://example.invalid/evil"); return r; })))
            await Reject<IOException>(() => runtime.InstallAsync(redirect, null, default), "untrusted redirect is rejected before download");
        using (var unavailable = new HttpClient(new Handler(_ => new(HttpStatusCode.ServiceUnavailable))))
            await Reject<IOException>(() => runtime.InstallAsync(unavailable, null, default), "HTTP error remains retryable");
        using (var truncated = new HttpClient(new Handler(_ => new(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) })))
            await Reject<IOException>(() => runtime.InstallAsync(truncated, null, default), "wrong content length never installs");
        using (var shortBody = new HttpClient(new Handler(_ => { var r = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) }; r.Content.Headers.ContentLength = TutorRuntimeInstallation.DownloadBytes; return r; })))
            await Reject<IOException>(() => runtime.InstallAsync(shortBody, null, default), "truncated response body never installs");
        Check(!runtime.IsInstalled && !Directory.EnumerateDirectories(Path.GetDirectoryName(runtime.DirectoryPath)!, ".install-*").Any(), "failed attempts clean partial files and expose no installed runtime");
        using (var held = new FileStream(Path.Combine(Path.GetDirectoryName(runtime.DirectoryPath)!, "install.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        using (var stop = new CancellationTokenSource(60))
        using (var never = new HttpClient(new Handler(_ => throw new Exception("No download while waiting for another installer"))))
            await Reject<OperationCanceledException>(() => runtime.InstallAsync(never, null, stop.Token), "cross-process installation lock supports cancellation");
        string wrong = Path.Combine(root, "wrong.gz"); await File.WriteAllTextAsync(wrong, "not the runtime");
        await Reject<IOException>(() => TutorRuntimeInstallation.VerifyHashAsync(wrong, TutorRuntimeInstallation.Sha256, default), "substituted archive fails pinned hash");
        await BadArchiveAsync("../escape.exe", TarEntryType.RegularFile);
        await BadArchiveAsync("bin/codex-app-server.exe", TarEntryType.SymbolicLink);
        await BadArchiveAsync("unexpected.exe", TarEntryType.RegularFile);
        await BadArchiveAsync("bin/codex-app-server.exe", TarEntryType.RegularFile);
        await BadArchiveAsync("codex-package.json", TarEntryType.RegularFile, true);
        using (var cancelled = new CancellationTokenSource())
        {
            var progress = new InlineProgress(p => { if (p.Stage.StartsWith("Downloading")) cancelled.Cancel(); });
            using var client = CachedClient();
            await Reject<OperationCanceledException>(() => runtime.InstallAsync(client, progress, cancelled.Token), "cancellation during download prevents promotion");
        }
        Check(!runtime.IsInstalled && !Directory.EnumerateDirectories(Path.GetDirectoryName(runtime.DirectoryPath)!, ".install-*").Any(), "cancelled download cleans staging and remains retryable");
        string sentinel = Path.Combine(root, "credentials-untouched.txt"); await File.WriteAllTextAsync(sentinel, "unchanged");
        var stages = new List<string>();
        using (var client = CachedClient()) await Task.Run(() => runtime.InstallAsync(client, new InlineProgress(p => stages.Add(p.Stage)), default));
        Check(runtime.IsInstalled && await runtime.IsValidAsync(default), "official archive installs with all pinned file hashes and Windows publisher verification");
        Check(stages.Any(s => s.StartsWith("Downloading")) && stages.Contains("Installing ChatGPT component…") && stages.Contains("Checking OpenAI publisher…"), "download, extraction and verification report distinct progress stages");
        using (var noNetwork = new HttpClient(new Handler(_ => throw new Exception("Existing runtime must not download"))))
            await runtime.InstallAsync(noNetwork, null, default);
        Check(true, "repeat first-use setup reuses a verified runtime without network");
        string manifest = Path.Combine(runtime.DirectoryPath, "codex-package.json");
        byte[] damaged = await File.ReadAllBytesAsync(manifest); damaged[0] ^= 1; await File.WriteAllBytesAsync(manifest, damaged);
        Check(runtime.IsInstalled && !await runtime.IsValidAsync(default), "same-size corruption is detected before process startup");
        using (var client = CachedClient()) await Task.Run(() => runtime.InstallAsync(client, null, default));
        Check(await runtime.IsValidAsync(default) && await File.ReadAllTextAsync(sentinel) == "unchanged", "retry repairs corrupt runtime without touching separate account data");
        var second = new TutorRuntimeInstallation(Path.Combine(root, "second"), notices);
        using (var cancelled = new CancellationTokenSource())
        using (var client = CachedClient())
            await Reject<OperationCanceledException>(() => Task.Run(() => second.InstallAsync(client, new InlineProgress(p => { if (p.Stage.StartsWith("Installing")) cancelled.Cancel(); }), cancelled.Token)), "cancellation after download also prevents installation");
        Check(!second.IsInstalled, "cancelled extraction has no completed marker");
        await TestDialogAsync(owner);
        if (Environment.GetEnvironmentVariable("STATSDIRECT_TEST_RUNTIME_LIVE") == "1")
        {
            var live = new TutorRuntimeInstallation(Path.Combine(root, "live"), notices);
            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromMinutes(10) };
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(15));
            await Task.Run(() => live.InstallAsync(client, new InlineProgress(p => { if (p.Received == 0) Console.WriteLine("LIVE " + p.Stage); }), timeout.Token));
            Check(live.IsInstalled && await live.IsValidAsync(default), "real GitHub redirect/download/install completes with pinned bytes and publisher");
        }
    }
    private static HttpClient CachedClient() => new(new Handler(_ => new(HttpStatusCode.OK) { Content = new StreamContent(File.OpenRead(Path.Combine(Repo, "DownloadedBinaries/TutorCache/codex-app-server-package-x86_64-pc-windows-msvc.tar.gz"))) }));
    private static async Task BadArchiveAsync(string name, TarEntryType type, bool duplicate = false)
    {
        string archive = Path.Combine(root, Guid.NewGuid() + ".gz"), dest = Path.Combine(root, Guid.NewGuid().ToString()); Directory.CreateDirectory(dest);
        await using (var file = File.Create(archive))
        await using (var gzip = new GZipStream(file, CompressionMode.Compress))
        await using (var writer = new TarWriter(gzip))
        {
            for (int i = 0; i < (duplicate ? 2 : 1); i++)
            {
                var entry = new PaxTarEntry(type, name);
                if (type == TarEntryType.SymbolicLink) entry.LinkName = "../../escape";
                else entry.DataStream = new MemoryStream(new byte[name == "codex-package.json" ? 237 : 1]);
                await writer.WriteEntryAsync(entry);
            }
        }
        await Reject<IOException>(() => TutorRuntimeInstallation.ExtractAsync(archive, dest, default), "reject archive entry: " + name + " / " + type + (duplicate ? " duplicate" : ""));
        Check(!File.Exists(Path.Combine(root, "escape.exe")), "archive rejection cannot write outside staging");
    }
    private static async Task TestDialogAsync(Form owner)
    {
        int attempts = 0;
        using var dialog = new frmInstallTutor(async (progress, token) => { progress.Report(new("Downloading ChatGPT component", 42000000, 100000000)); if (++attempts == 1) await Task.Delay(30000, token); });
        dialog.StartPosition = FormStartPosition.Manual; dialog.Location = new(-16000, -16000); dialog.ShowInTaskbar = false; dialog.Show(owner);
        await Wait(() => ((ProgressBar)Field(dialog, "progress")).Value == 42);
        Check(dialog.Visible && ((Label)Field(dialog, "status")).Text.Contains("Downloading"), "first use starts download and shows progress immediately");
        string artifacts = Environment.GetEnvironmentVariable("STATSDIRECT_RUNTIME_ARTIFACTS");
        if (artifacts != null) { Directory.CreateDirectory(artifacts); using var bitmap = new Bitmap(dialog.Width, dialog.Height); dialog.DrawToBitmap(bitmap, new Rectangle(Point.Empty,dialog.Size)); bitmap.Save(Path.Combine(artifacts,"ai-setup-progress.png")); }
        var cancelBounds = dialog.RectangleToClient(((Button)Field(dialog,"cancel")).RectangleToScreen(((Button)Field(dialog,"cancel")).ClientRectangle));
        Check(dialog.ClientRectangle.Contains(cancelBounds), "cancel remains visible without scrolling at the current display DPI");
        ((Button)Field(dialog, "cancel")).PerformClick(); await Wait(() => ((Button)Field(dialog, "retry")).Visible);
        Check(dialog.Visible && dialog.DialogResult != DialogResult.OK && ((Label)Field(dialog, "status")).Text.Contains("cancelled"), "cancel keeps Retry available without claiming success");
        ((Button)Field(dialog, "retry")).PerformClick(); await Wait(() => dialog.DialogResult == DialogResult.OK);
        Check(attempts == 2, "retry completes setup and returns success once");
        using var failure = new frmInstallTutor((_, _) => throw new IOException("Network unavailable"));
        failure.StartPosition = FormStartPosition.Manual; failure.Location = new(-16000, -16000); failure.ShowInTaskbar = false; failure.Show(owner);
        await Wait(() => ((Button)Field(failure, "retry")).Visible);
        Check(((Label)Field(failure, "status")).Text.Contains("Network unavailable") && ((Button)Field(failure, "retry")).Visible, "setup error displays reason and a retry action");
        ((Button)Field(failure, "cancel")).PerformClick(); Check(!failure.Visible, "Not now dismisses failed setup");
        bool completed = false;
        using var close = new frmInstallTutor(async (_, token) => { try { await Task.Delay(30000, token); } finally { await Task.Delay(30); completed = true; } });
        close.StartPosition = FormStartPosition.Manual; close.Location = new(-16000, -16000); close.ShowInTaskbar = false; close.Show(owner); await Wait(() => Field(close,"operation") != null); close.Close();
        await Wait(() => !close.Visible); Check(completed && close.DialogResult != DialogResult.OK, "closing waits for cancellation cleanup and never resumes sign-in");
        using var modal = new frmInstallTutor(async (_, token) => await Task.Delay(20, token));
        modal.StartPosition = FormStartPosition.Manual; modal.Location = new(-16000, -16000); modal.ShowInTaskbar = false;
        Check(modal.ShowDialog(owner) == DialogResult.OK, "modal caller resumes only after successful setup");
    }
    private sealed class InlineProgress(Action<TutorInstallProgress> action) : IProgress<TutorInstallProgress> { public void Report(TutorInstallProgress value) => action(value); }
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> reply) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) { token.ThrowIfCancellationRequested(); var result = reply(request); result.RequestMessage = request; return Task.FromResult(result); } }
}
