#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace StatsDirect.R.Interactive;

/// <summary>The console's fallback for an installed R the native worker cannot use (R before 4.2, a 32-bit-only R) or when the worker fails
/// to start: each submission runs as a fresh R process through Rscript.exe, with the same events as the interactive session, so the page
/// shows it the same way.  Objects do not persist between commands, and R cannot ask questions.</summary>
public sealed class RScriptSession : IRSession
{
    private readonly string rscriptPath, rVersion, workingDirectory, reason;
    private readonly Encoding outputEncoding;
    private readonly object sync = new();
    private readonly string sessionId = Guid.NewGuid().ToString("N");
    private long generation, seq, nextFilePlot;
    private uint nextCommand;
    private string state = "stopped", currentPrompt = "";
    private Run? active;
    private bool disposed;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static readonly Lazy<bool> codePages = new(() => { Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); return true; });

    private sealed record Run(uint Id)
    {
        public Process? Process { get; set; }
        public bool Killed { get; set; }
        public TaskCompletionSource Finished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public event Action<JsonElement>? Event;

    public bool PersistentWorkspace => false;

    public string RDescription => $"R {rVersion} through Rscript";

    /// <param name="rscriptPath">The Rscript.exe of the installed R.</param>
    /// <param name="rVersion">Its version, such as "4.1.3": R before 4.2 writes in the native code page on Windows, so it decides the decoding of R's output.</param>
    /// <param name="workingDirectory">The user's R folder, where the commands run and their files land.</param>
    /// <param name="reason">Why the console runs this way, told to the user in the opening notice.</param>
    public RScriptSession(string rscriptPath, string rVersion, string workingDirectory, string reason)
    {
        ArgumentNullException.ThrowIfNull(rVersion);
        this.rscriptPath = Path.GetFullPath(rscriptPath);
        this.rVersion = rVersion;
        this.workingDirectory = Path.GetFullPath(workingDirectory);
        this.reason = reason;
        outputEncoding = OutputEncoding(rVersion);
    }

    public void PublishNotice(string text)
    {
        long current; lock (sync) current = generation;
        Emit(current, "notice", new() { ["text"] = text });
    }

    public void PublishState()
    {
        lock (sync) Emit(generation, "state", new() { ["state"] = state, ["prompt"] = currentPrompt });
    }

    public async Task StartAsync()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        // The window starts its session before every submission; a started runner has nothing to do, as a running worker has nothing to do
        bool started; lock (sync) started = generation > 0;
        if (started) return;
        await OpenAsync(null);
    }

    /// <summary>A new generation: a running command is stopped first, then the session, the explanation of running through Rscript, any word
    /// of a restart and the ready state are announced.  The generation comes from the counter every session shares, so that this session's
    /// events outrank on the page those of any session it replaces.</summary>
    private async Task OpenAsync(string? restarted)
    {
        if (!File.Exists(rscriptPath)) throw new FileNotFoundException("Rscript.exe was not found where R was installed.", rscriptPath);
        long current;
        for (;;)
        {
            await StopCoreAsync();
            lock (sync)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                // A command accepted between the stopped run's end and here would run on unseen under the new generation: it is stopped too.
                if (active != null) continue;
                current = generation = RSessionGenerations.Next(); seq = 0; state = "ready"; currentPrompt = "> ";
                break;
            }
        }
        Directory.CreateDirectory(workingDirectory);
        Emit(current, "session", new() { ["script"] = "", ["capabilities"] = new { plotMimes = new[] { "image/svg+xml", "image/png", "application/pdf" }, consoleInput = true, rawStdin = false, help = true, ai = false, openPlot = true, copyPlot = true, persistentWorkspace = false } });
        Emit(current, "notice", new() { ["text"] = $"R {rVersion} runs each command as a fresh R session through Rscript, so objects do not persist between commands. " + reason });
        if (restarted != null) Emit(current, "notice", new() { ["text"] = restarted });
        Emit(current, "state", new() { ["state"] = "ready", ["prompt"] = "> " });
    }

    public async Task SubmitAsync(string source, string origin = "console")
    {
        ArgumentNullException.ThrowIfNull(source);
        ValidateText(source, 8 * 1024 * 1024 - 5);
        Run run;
        long current;
        IReadOnlyDictionary<string, ROutputFiles.FileStamp> before;
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (state != "ready" || active != null) throw new InvalidOperationException("R is busy or is waiting for an answer. Finish or stop that command first.");
            current = generation;
            run = active = new(++nextCommand);
            state = "busy"; currentPrompt = "";
            before = ROutputFiles.Snapshot(workingDirectory);
        }
        Emit(current, "commandAccepted", new() { ["commandId"] = run.Id, ["origin"] = origin });
        Emit(current, "inputEcho", new() { ["commandId"] = run.Id, ["text"] = source, ["prompt"] = "> ", ["origin"] = origin });
        Emit(current, "state", new() { ["state"] = "busy", ["prompt"] = "" });
        string script = Path.Combine(workingDirectory, $"console-{sessionId}-{current}-run-{run.Id}.r");
        Process? child = null;
        try
        {
            Directory.CreateDirectory(workingDirectory);
            // R reads the file as UTF-8 (--encoding below) and takes LF line ends; without a BOM, which R would read as text.
            await File.WriteAllTextAsync(script, Head(current, run.Id) + source.Replace("\r\n", "\n").Replace('\r', '\n') + "\n", Utf8);
            var info = new ProcessStartInfo(rscriptPath)
            {
                WorkingDirectory = workingDirectory, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = outputEncoding, StandardErrorEncoding = outputEncoding
            };
            info.ArgumentList.Add("--vanilla"); info.ArgumentList.Add("--encoding=UTF-8"); info.ArgumentList.Add(script);
            // R's Windows UTF-8 locale must be selected by R, not inherited as an unsupported Unix locale.
            info.Environment.Remove("LC_ALL"); info.Environment.Remove("LC_CTYPE");
            //  Rscript.exe is not DPI-aware, so on a scaled display Windows tells it the screen is 96 dpi while the metafile it records is framed against the
            //  display's true resolution: at 225% R draws its chart into the top left 40% of the picture and leaves the rest blank.  This compatibility layer
            //  makes Windows give R the real figures, so the drawing fills its frame.  Any layers already set for this process are kept.
            const string compatLayerVariable = "__COMPAT_LAYER";
            const string highDpiAwareLayer = "HighDpiAware";
            //  (TryGetValue, because the indexer throws when the variable is not set, which is the normal case.)
            info.Environment.TryGetValue(compatLayerVariable, out string? existingLayers);
            if (string.IsNullOrWhiteSpace(existingLayers))
                info.Environment[compatLayerVariable] = highDpiAwareLayer;
            else if (existingLayers.IndexOf(highDpiAwareLayer, StringComparison.OrdinalIgnoreCase) < 0)
                info.Environment[compatLayerVariable] = existingLayers + " " + highDpiAwareLayer;
            child = new Process { StartInfo = info };
            if (!child.Start()) throw new IOException("Rscript could not start.");
            bool killNow;
            lock (sync) { run.Process = child; killNow = run.Killed; }
            // A Stop that came while R was starting still ends this run.
            if (killNow) Kill(child);
            // Pumping R's output and reading its chart files must not capture the WinForms synchronization context of the caller.
            _ = Task.Run(() => RunAsync(run, child, current, before, script));
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            // Whatever stopped R starting, the command ends here as an R error ends one: the page must not show R busy for ever, and the
            // notice says why once, so the caller has nothing to report again.
            child?.Dispose();
            try { File.Delete(script); } catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException) { }
            lock (sync) { if (current == generation && !disposed && active == run) { active = null; state = "ready"; currentPrompt = "> "; } }
            Emit(current, "notice", new() { ["text"] = "R could not run through Rscript. " + error.Message });
            Emit(current, "commandFinished", new() { ["commandId"] = run.Id, ["status"] = "error" });
            Emit(current, "state", new() { ["state"] = "ready", ["prompt"] = "> " });
            run.Finished.TrySetResult();
        }
    }

    public Task ReplyAsync(long requestId, string text)
        => throw new InvalidOperationException("R running through Rscript cannot ask questions; the interactive console needs 64-bit R 4.2 or later.");

    public Task InterruptAsync()
    {
        Run? run; long current;
        lock (sync)
        {
            run = active;
            if (run == null || state != "busy") return Task.CompletedTask;
            current = generation; run.Killed = true; state = "interrupting"; currentPrompt = "Stopping…";
        }
        // As PublishState shows this state: Stop stays disabled until R has gone; the exit path then finishes the command as interrupted.
        Emit(current, "state", new() { ["state"] = "interrupting", ["prompt"] = "Stopping…" });
        Kill(run.Process);
        return Task.CompletedTask;
    }

    public async Task RestartAsync()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        // The page leaves its restarting state only for a session event, as the console's Restart gives it, so Restart opens a new generation
        // here too: a fresh transcript that says why R runs this way, and that no workspace needed clearing.
        bool unstarted; lock (sync) unstarted = generation == 0;
        await OpenAsync(unstarted ? null : "Each command already runs in a fresh R; there is no workspace to clear.");
    }

    /// <summary>Ends a running command, if there is one, and waits for its R to go, so that the window can close.</summary>
    public Task StopAsync() => StopCoreAsync();

    private async Task StopCoreAsync()
    {
        Run? run;
        lock (sync)
        {
            run = active;
            if (run == null) return;
            run.Killed = true;
            if (state == "busy") { state = "interrupting"; currentPrompt = "Stopping…"; }
        }
        Kill(run.Process);
        await run.Finished.Task;
    }

    private async Task RunAsync(Run run, Process child, long current, IReadOnlyDictionary<string, ROutputFiles.FileStamp> before, string script)
    {
        // Process.Dispose leaves readers that were read directly open until they are finalized, so they are disposed here by name.
        StreamReader? output = null, errors = null;
        try
        {
            output = child.StandardOutput; errors = child.StandardError;
            // Both streams flow to the page as R writes them, so a long command shows its progress and a message its place among the output.
            Task stdout = PumpAsync(output, "stdout", run.Id, current), stderr = PumpAsync(errors, "stderr", run.Id, current);
            await child.WaitForExitAsync();
            await Task.WhenAll(stdout, stderr);
            int exitCode = child.ExitCode;
            bool killed; lock (sync) killed = run.Killed;
            // Only a Stop that landed makes the command interrupted, and a killed R exits with -1: a Stop arriving after R ended on its own, in
            // the moment before this is read, leaves the result R gave.
            string status = killed && exitCode == -1 ? "interrupted" : exitCode == 0 ? "ok" : "error";
            if (status == "error") Emit(current, "notice", new() { ["text"] = $"(R stopped with exit code {exitCode})" });
            await ReadDevicePlotsAsync(run.Id, current);
            foreach (var (type, fields) in await ROutputFiles.ChangedAsync(workingDirectory, before, sessionId, current, () => Interlocked.Increment(ref nextFilePlot), () => { lock (sync) return current == generation && !disposed; }))
                Emit(current, type, fields);
            try { File.Delete(script); } catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
            lock (sync) { if (current == generation && !disposed && active == run) { active = null; state = "ready"; currentPrompt = "> "; } }
            Emit(current, "commandFinished", new() { ["commandId"] = run.Id, ["status"] = status });
            Emit(current, "state", new() { ["state"] = "ready", ["prompt"] = "> " });
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            lock (sync) { if (current == generation && !disposed && active == run) { active = null; state = "ready"; currentPrompt = "> "; } }
            Emit(current, "notice", new() { ["text"] = "R's output could not be read. " + error.Message });
            Emit(current, "commandFinished", new() { ["commandId"] = run.Id, ["status"] = "error" });
            Emit(current, "state", new() { ["state"] = "ready", ["prompt"] = "> " });
        }
        finally { output?.Dispose(); errors?.Dispose(); child.Dispose(); run.Finished.TrySetResult(); }
    }

    private async Task PumpAsync(StreamReader reader, string stream, uint commandId, long current)
    {
        char[] buffer = new char[4096];
        var text = new StringBuilder();
        bool pendingReturn = false;
        try
        {
            int count;
            while ((count = await reader.ReadAsync(buffer)) > 0)
            {
                // R writes Windows line ends to the pipe; the page takes LF alone, as the worker's output has it.  A CR ending a chunk
                // waits for the next chunk to show whether an LF follows it.
                text.Clear();
                if (pendingReturn) { text.Append('\r'); pendingReturn = false; }
                text.Append(buffer, 0, count);
                if (text[text.Length - 1] == '\r') { pendingReturn = true; text.Length--; }
                text.Replace("\r\n", "\n");
                if (text.Length > 0) Emit(current, "output", new() { ["stream"] = stream, ["text"] = text.ToString(), ["commandId"] = commandId });
            }
            if (pendingReturn) Emit(current, "output", new() { ["stream"] = stream, ["text"] = "\n", ["commandId"] = commandId });
        }
        catch (Exception error) when (error is IOException or ObjectDisposedException or InvalidOperationException) { }
    }

    /// <summary>The run's automatic charts, console-&lt;session&gt;-&lt;generation&gt;-run-&lt;n&gt;-device-&lt;d&gt;-&lt;page&gt;.svg, as cards in device then
    /// page order.  R closed every device as it ended, so the files are complete; a page never drawn on is left as an empty file and is not shown.</summary>
    private async Task ReadDevicePlotsAsync(uint commandId, long current)
    {
        string prefix = $"console-{sessionId}-{current}-run-{commandId}-device-";
        var pages = new List<(int Device, int Page, string Path)>();
        try
        {
            foreach (string path in Directory.EnumerateFiles(workingDirectory, prefix + "*.svg"))
            {
                var match = Regex.Match(Path.GetFileName(path), @"^" + Regex.Escape(prefix) + @"(\d+)-(\d+)\.svg$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                if (match.Success) pages.Add((int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture), path));
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { return; }
        int shown = 0;
        foreach (var (device, page, path) in pages.OrderBy(p => p.Device).ThenBy(p => p.Page))
        {
            lock (sync) if (current != generation || disposed) return;
            try
            {
                long length = new FileInfo(path).Length;
                if (length == 0) continue;
                string? preview = null;
                if (length <= 20 * 1024 * 1024)
                    preview = "data:image/svg+xml;base64," + Convert.ToBase64String(await File.ReadAllBytesAsync(path));
                else Emit(current, "notice", new() { ["text"] = "This chart exceeds the preview limit. Use Open chart to view its original SVG." });
                shown++;
                Emit(current, "plot", new() { ["plotId"] = $"{sessionId}-{current}-run-{commandId}-device-{device}-{page}", ["revision"] = 1, ["mime"] = "image/svg+xml", ["preview"] = preview, ["uri"] = new Uri(path).AbsoluteUri, ["path"] = path, ["label"] = "R plot " + shown });
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                Emit(current, "notice", new() { ["text"] = "A chart of this command could not be read: " + error.Message });
            }
        }
    }

    /// <summary>The head of each run's script: the program's package library first on the search path, as the runner and the console have it, and
    /// ordinary plots to SVG files in the working folder, one per page, that become this command's cards.  Nothing is left in the user's workspace.</summary>
    private string Head(long current, uint commandId)
    {
        string Q(string value) => JsonSerializer.Serialize(value.Replace('\\', '/'));
        string folder = Q(workingDirectory), pattern = Q($"console-{sessionId}-{current}-run-{commandId}-device-%d-%%03d.svg");
        // dev.new() keeps only arguments named in the device's formals, so expose svg's formals instead of just (...).
        // Forward only supplied arguments: svg itself resolves its defaults, including those depending on other arguments.
        return $$"""
        local({
          # Preserve the package library used by the earlier StatsDirect R script runner.
          libdir <- file.path({{folder}}, 'Lib')
          dir.create(libdir, recursive=TRUE, showWarnings=FALSE)
          .libPaths(c(libdir, .libPaths()))
          n <- 0L
          device <- function() {
            n <<- n + 1L
            args <- mget(as.character(names(as.list(match.call())[-1L])), envir=environment(), inherits=FALSE)
            if (!'filename' %in% names(args)) args$filename <- file.path({{folder}}, sprintf({{pattern}}, n))
            do.call(grDevices::svg, args)
            invisible(NULL)
          }
          formals(device) <- formals(grDevices::svg)
          options(device=device)
        }, envir=new.env(parent=baseenv()))

        """;
    }

    /// <summary>R from 4.2 writes UTF-8 on Windows; earlier R writes in the system's ANSI code page, which .NET knows only once its provider is registered.</summary>
    private static Encoding OutputEncoding(string rVersion)
    {
        var match = Regex.Match(rVersion, @"\d+(?:\.\d+){1,3}");
        if (!match.Success || !Version.TryParse(match.Value, out var version)) return Encoding.UTF8;
        if (new Version(version.Major, version.Minor, Math.Max(version.Build, 0)) >= new Version(4, 2, 0)) return Encoding.UTF8;
        _ = codePages.Value;
        try { return Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.ANSICodePage); }
        catch (Exception error) when (error is ArgumentException or NotSupportedException) { return Encoding.Default; }
    }

    private static void Kill(Process? child)
    {
        if (child == null) return;
        try { child.Kill(entireProcessTree: true); }
        catch (Exception error) when (error is InvalidOperationException or Win32Exception or AggregateException or NotSupportedException) { }
    }

    private static void ValidateText(string text, int maxBytes)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Contains('\0')) throw new ArgumentException("R console input cannot contain a NUL character.", nameof(text));
        if (Utf8.GetByteCount(text) > maxBytes) throw new ArgumentException($"This R input exceeds the {maxBytes:N0}-byte limit. Shorten it and try again.", nameof(text));
    }

    private void Emit(long current, string type, Dictionary<string, object?>? fields = null)
    {
        lock (sync)
        {
            if (current != generation || disposed) return;
            fields ??= new(); fields["protocolVersion"] = 1; fields["type"] = type;
            fields["sessionId"] = sessionId; fields["generation"] = current;
            fields["seq"] = type == "session" ? 0 : ++seq;
            Event?.Invoke(JsonSerializer.SerializeToElement(fields));
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        Run? run;
        lock (sync) { run = active; disposed = true; generation++; if (run != null) run.Killed = true; }
        // A command still running is ended, and given a moment to finish, its R gone and its files read, so that the copies it was making are
        // removed with the rest rather than written after the sweep.  Its run sets Finished however it ends.
        if (run != null)
        {
            Kill(run.Process);
            run.Finished.Task.Wait(2000);
        }
        RemoveSessionFiles();
    }

    /// <summary>The session's own files in the R folder, the run scripts, device pages, file copies and previews: written for its cards, and of
    /// no use once the window is closed.  The chart windows hold their own copies.</summary>
    private void RemoveSessionFiles()
    {
        try
        {
            foreach (string path in Directory.EnumerateFiles(workingDirectory, $"console-{sessionId}-*"))
                try { File.Delete(path); } catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
    }
}
