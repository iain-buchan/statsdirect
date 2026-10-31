using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace StatsDirect.InteractiveR;

/// <summary>Prototype-only adapter. R runs in a native child; neither the WebView nor this process loads R.dll.</summary>
public sealed class RConsoleSession : IDisposable
{
    private readonly string helper, rHome, workingDirectory;
    private readonly object sync = new();
    private readonly SemaphoreSlim writes = new(1, 1);
    private readonly SemaphoreSlim lifecycle = new(1, 1);
    private readonly string sessionId = Guid.NewGuid().ToString("N");
    private Process? process;
    private long generation, seq;
    private uint nextCommand;
    private long? requestId;
    private long nextInputToken;
    private uint nativeRequestId;
    private int requestMaxBytes = 4094;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private string state = "stopped", requestKind = "input";
    private string currentPrompt = "";
    private Command? active;
    private bool disposed, configured;
    private TaskCompletionSource? started;
    private string plotIndex = "";
    private long plotIndexLines;

    private sealed record Command(uint Id, string Source, string Origin, string? Internal = null)
    {
        public string? Status { get; set; }
        public Command? Parent { get; init; }
    }

    public event Action<JsonElement>? Event;

    public void PublishNotice(string text)
    {
        long current; lock (sync) current = generation;
        Emit(current, "notice", new() { ["text"] = text });
    }

    public void PublishState()
    {
        lock (sync)
        {
            string visibleState = state == "stopping" ? "interrupting" : state;
            Emit(generation, "state", new() { ["state"] = visibleState, ["prompt"] = currentPrompt });
            if (requestId is long id)
                Emit(generation, "inputRequested", new() { ["requestId"] = id.ToString(System.Globalization.CultureInfo.InvariantCulture), ["prompt"] = currentPrompt, ["kind"] = requestKind, ["addToHistory"] = requestKind == "continuation", ["maxBytes"] = requestMaxBytes });
        }
    }

    public RConsoleSession(string nativeHelper, string rHome, string workingDirectory)
    {
        helper = Path.GetFullPath(nativeHelper);
        this.rHome = Path.GetFullPath(rHome);
        this.workingDirectory = Path.GetFullPath(workingDirectory);
    }

    public async Task StartAsync()
    {
        await lifecycle.WaitAsync();
        try { await StartCoreAsync(); }
        finally { lifecycle.Release(); }
    }

    private async Task StartCoreAsync()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (process is { HasExited: false }) return;
        if (!File.Exists(helper)) throw new FileNotFoundException("Build the native R console helper first.", helper);
        Directory.CreateDirectory(workingDirectory);
        long current;
        lock (sync)
        {
            current = ++generation; seq = 0; requestId = null; active = null;
            configured = false; state = "busy"; plotIndexLines = 0;
            plotIndex = Path.Combine(workingDirectory, $"console-plots-{sessionId}-{current}.tsv");
            started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        Emit(current, "session", new() { ["script"] = "", ["capabilities"] = new { plotMimes = new[] { "image/svg+xml" }, consoleInput = true, rawStdin = false, help = true, ai = false, openPlot = true, copyPlot = true } });
        Emit(current, "state", new() { ["state"] = "busy", ["prompt"] = "Starting R…" });
        var info = new ProcessStartInfo(helper)
        {
            WorkingDirectory = workingDirectory, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
        };
        info.ArgumentList.Add("--r-home"); info.ArgumentList.Add(rHome);
        info.ArgumentList.Add("--working-directory"); info.ArgumentList.Add(workingDirectory);
        // R's Windows UTF-8 locale must be selected by R, not inherited as an unsupported Unix locale.
        info.Environment.Remove("LC_ALL"); info.Environment.Remove("LC_CTYPE");
        var child = new Process { StartInfo = info };
        bool launched = false;
        try
        {
            launched = child.Start();
            if (!launched) throw new IOException("The R console helper could not start.");
            process = child;
            _ = ReadEventsAsync(child, current);
            _ = ReadRawOutputAsync(child, current);
            await started!.Task.WaitAsync(TimeSpan.FromSeconds(25));
        }
        catch
        {
            if (launched && !child.HasExited) child.Kill(entireProcessTree: true);
            child.Dispose(); process = null;
            throw;
        }
    }

    public async Task SubmitAsync(string source, string origin = "console")
    {
        ArgumentNullException.ThrowIfNull(source);
        ValidateText(source, 8 * 1024 * 1024 - 5);
        Command command;
        long current;
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (state != "ready" || active != null) throw new InvalidOperationException("R is busy or is waiting for an answer. Finish or stop that command first.");
            current = generation;
            command = active = new(++nextCommand, source, origin);
            state = "busy"; requestId = null;
        }
        Emit(current, "commandAccepted", new() { ["commandId"] = command.Id, ["origin"] = origin });
        Emit(current, "inputEcho", new() { ["commandId"] = command.Id, ["text"] = source, ["prompt"] = "> ", ["origin"] = origin });
        Emit(current, "state", new() { ["state"] = "busy", ["prompt"] = "" });
        try { await SendAsync(1, command.Id, source, current); }
        catch (Exception error) { TransportFailed(current, error); throw; }
    }

    public async Task ReplyAsync(long requestId, string text)
    {
        long current;
        uint nativeId;
        string kind;
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (this.requestId != requestId || state is not ("input" or "continuation"))
                throw new InvalidOperationException("That input request has expired. Use the current R prompt.");
            ValidateText(text, requestKind == "continuation" ? 8 * 1024 * 1024 - 5 : requestMaxBytes);
            if (requestKind != "continuation" && (text.Contains('\n') || text.Contains('\r')))
                throw new ArgumentException("An answer to this R prompt must be one line.", nameof(text));
            nativeId = nativeRequestId; kind = requestKind;
            current = generation; this.requestId = null; state = "busy";
        }
        Emit(current, "inputEcho", new() { ["text"] = text, ["prompt"] = kind == "continuation" ? "+ " : "", ["origin"] = kind == "continuation" ? "console" : "input" });
        Emit(current, "state", new() { ["state"] = "busy", ["prompt"] = "" });
        try { await SendAsync(2, nativeId, text, current); }
        catch (Exception error) { TransportFailed(current, error); throw; }
    }

    public async Task InterruptAsync()
    {
        long current;
        lock (sync)
        {
            if (state is "ready" or "stopped" || active == null) return;
            current = generation; state = "stopping"; requestId = null;
        }
        // No empty line or EOF is delivered to R. The helper wakes its callback and unwinds on the R thread.
        Emit(current, "state", new() { ["state"] = "busy", ["prompt"] = "Stopping…" });
        try { await SendAsync(3, null, null, current); }
        catch (Exception error) { TransportFailed(current, error); throw; }
    }

    public async Task RestartAsync()
    {
        await lifecycle.WaitAsync();
        try { await StopCoreAsync(); await StartCoreAsync(); }
        finally { lifecycle.Release(); }
    }

    private async Task SendAsync(byte opcode, uint? id, string? text, long current)
    {
        byte[] source = text == null ? [] : Utf8.GetBytes(text);
        int payload = 1 + (id.HasValue ? 4 : 0) + source.Length;
        if (payload > 8 * 1024 * 1024) throw new ArgumentException("The console command exceeds the prototype's 8 MB limit.");
        byte[] frame = new byte[4 + payload];
        BinaryPrimitives.WriteUInt32LittleEndian(frame, (uint)payload); frame[4] = opcode;
        if (id.HasValue) BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(5), id.Value);
        source.CopyTo(frame.AsSpan(id.HasValue ? 9 : 5));
        await writes.WaitAsync();
        try
        {
            Process? child;
            lock (sync) { if (current != generation || disposed) return; child = process; }
            if (child == null || child.HasExited) throw new IOException("The R session has stopped. Restart it to continue.");
            await child.StandardInput.BaseStream.WriteAsync(frame);
            await child.StandardInput.BaseStream.FlushAsync();
        }
        finally { writes.Release(); }
    }

    private async Task ReadEventsAsync(Process child, long current)
    {
        try
        {
            while (await child.StandardOutput.ReadLineAsync() is string line)
            {
                if (line.Length == 0) continue;
                using var json = JsonDocument.Parse(line);
                await HandleAsync(json.RootElement, current);
            }
            await child.WaitForExitAsync();
            lock (sync)
            {
                if (generation != current) return;
                state = "stopped"; requestId = null;
                started?.TrySetException(new IOException($"The native R helper exited ({child.ExitCode})."));
            }
            Emit(current, "exited", new() { ["exitCode"] = child.ExitCode });
            Emit(current, "state", new() { ["state"] = "stopped", ["prompt"] = "Restart R to continue." });
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            lock (sync) { if (current != generation || disposed) return; started?.TrySetException(error); state = "stopped"; }
            Emit(current, "notice", new() { ["text"] = "R console connection failed: " + error.Message });
            Emit(current, "exited", new() { ["error"] = error.Message });
        }
    }

    private async Task ReadRawOutputAsync(Process child, long current)
    {
        char[] buffer = new char[4096];
        try
        {
            int count;
            while ((count = await child.StandardError.ReadAsync(buffer)) > 0)
                Emit(current, "output", new() { ["stream"] = "stderr", ["text"] = new string(buffer, 0, count), ["native"] = true });
        }
        catch (Exception error) when (error is IOException or ObjectDisposedException or InvalidOperationException) { }
    }

    private async Task HandleAsync(JsonElement item, long current)
    {
        lock (sync) if (current != generation || disposed) return;
        string type = item.GetProperty("type").GetString() ?? "";
        string Text(string name, string fallback = "") => item.TryGetProperty(name, out var value) ? value.GetString() ?? fallback : fallback;
        switch (type)
        {
            case "ready":
                Emit(current, "notice", new() { ["text"] = "R " + Text("rVersion") + " — interactive prototype; the released application is unchanged." });
                break;
            case "output":
                Command? outputCommand; lock (sync) outputCommand = active;
                if (outputCommand?.Internal == null)
                    Emit(current, "output", new() { ["stream"] = Text("stream", "stdout"), ["text"] = Text("text"), ["commandId"] = outputCommand?.Id });
                break;
            case "busy": break; // Busy(false) can precede a nested ReadConsole, so it is not completion.
            case "completed":
                lock (sync) { if (current != generation || disposed) return; if (active != null) active.Status = Text("status", "error"); requestId = null; }
                break;
            case "error": Emit(current, "notice", new() { ["text"] = Text("message") }); break;
            case "prompt":
                string kind = Text("kind");
                if (kind == "top") { await AtTopPromptAsync(current, Text("text", "> ")); break; }
                long id;
                string inputState = kind == "continuation" ? "continuation" : "input";
                lock (sync)
                {
                    if (current != generation || disposed) return;
                    id = ++nextInputToken; requestId = id;
                    nativeRequestId = item.GetProperty("requestId").GetUInt32();
                    requestMaxBytes = item.TryGetProperty("maxBytes", out var max) ? max.GetInt32() : 4094;
                    requestKind = kind; state = inputState; currentPrompt = Text("text");
                }
                Emit(current, "state", new() { ["state"] = inputState, ["prompt"] = Text("text") });
                Emit(current, "inputRequested", new() { ["requestId"] = id.ToString(System.Globalization.CultureInfo.InvariantCulture), ["prompt"] = Text("text"), ["kind"] = kind, ["addToHistory"] = kind == "continuation", ["maxBytes"] = requestMaxBytes });
                break;
        }
    }

    private async Task AtTopPromptAsync(long current, string prompt)
    {
        Command? previous;
        bool needsSetup;
        lock (sync) { if (current != generation || disposed) return; previous = active; requestId = null; needsSetup = !configured; }
        if (needsSetup)
        {
            if (previous?.Internal == "setup")
            {
                if (previous.Status != "ok") throw new IOException("R console graphics setup failed.");
                lock (sync) { if (current != generation || disposed) return; configured = true; }
            }
            else
            {
                await InternalAsync(Bootstrap(), "setup", null, current);
                return;
            }
        }
        else if (previous is { Internal: null, Status: not null })
        {
            await InternalAsync("invisible(try(.StatsDirectConsole$snapshot(), silent=TRUE))", "snapshot", previous, current);
            return;
        }
        if (previous?.Internal == "snapshot")
        {
            await ReadPlotsAsync(current);
            previous = previous.Parent;
        }
        lock (sync) { if (current != generation || disposed) return; active = null; state = "ready"; currentPrompt = prompt; }
        if (previous is { Internal: null })
            Emit(current, "commandFinished", new() { ["commandId"] = previous.Id, ["status"] = previous.Status ?? "error" });
        Emit(current, "state", new() { ["state"] = "ready", ["prompt"] = prompt });
        lock (sync) { if (current == generation && !disposed) started?.TrySetResult(); }
    }

    private async Task InternalAsync(string source, string purpose, Command? parent, long current)
    {
        Command command;
        lock (sync) { if (current != generation || disposed) return; command = active = new(++nextCommand, source, "internal", purpose) { Parent = parent }; state = "busy"; }
        await SendAsync(1, command.Id, source, current);
    }

    private string Bootstrap()
    {
        string Q(string value) => JsonSerializer.Serialize(value.Replace('\\', '/'));
        string folder = Q(workingDirectory), index = Q(plotIndex), prefix = Q($"console-{sessionId}-{generation}");
        // A small display-list snapshot keeps the active device open: abline()/title() still update the current plot.
        // Hooks capture a completed page before plot.new/grid.newpage starts the next one.
        return $$"""
        local({
          s <- new.env(parent=baseenv()); s$serial <- 0L; s$revision <- 0L; s$capturing <- FALSE
          s$lastPlot <- NULL; s$lastSerial <- -1L
          s$folder <- {{folder}}; s$index <- {{index}}; s$prefix <- {{prefix}}
          s$snapshot <- function() {
            if (s$capturing || grDevices::dev.cur() == 1L || s$serial == 0L) return(invisible(NULL))
            p <- grDevices::recordPlot(); previous <- grDevices::dev.cur()
            # Explicit file devices without a display list remain file outputs, not blank console previews.
            if (is.null(p[[1L]])) return(invisible(NULL))
            if (identical(s$serial, s$lastSerial) && identical(p, s$lastPlot)) return(invisible(NULL))
            s$revision <- s$revision + 1L
            path <- file.path(s$folder, sprintf('%s-plot-%d-rev-%d.svg', s$prefix, s$serial, s$revision))
            s$capturing <- TRUE; snapshotDevice <- NA_integer_
            on.exit({if(snapshotDevice %in% grDevices::dev.list()) grDevices::dev.off(snapshotDevice); if(previous %in% grDevices::dev.list()) grDevices::dev.set(previous); s$capturing <- FALSE}, add=TRUE)
            grDevices::svg(path); snapshotDevice <- grDevices::dev.cur()
            grDevices::replayPlot(p); grDevices::dev.off(snapshotDevice); grDevices::dev.set(previous)
            cat(s$serial, s$revision, path, sep='\t', file=s$index, append=TRUE); cat('\n', file=s$index, append=TRUE)
            s$lastPlot <- p; s$lastSerial <- s$serial
            invisible(NULL)
          }
          begin <- function() if(!s$capturing) s$serial <- s$serial + 1L
          before <- function() if(!s$capturing) s$snapshot()
          setHook('before.plot.new', before, 'append'); setHook('plot.new', begin, 'append')
          setHook('before.grid.newpage', before, 'append'); setHook('grid.newpage', begin, 'append')
          n <- 0L
          device <- function() {
            n <<- n + 1L
            args <- mget(as.character(names(as.list(match.call())[-1L])), envir=environment(), inherits=FALSE)
            if(!'filename' %in% names(args)) args$filename <- file.path(s$folder, sprintf('%s-device-%d-%%03d.svg', s$prefix, n))
            do.call(grDevices::svg, args); grDevices::dev.control(displaylist='enable'); invisible(NULL)
          }
          formals(device) <- formals(grDevices::svg); options(device=device)
          assign('.StatsDirectConsole', s, envir=.GlobalEnv)
        })
        """;
    }

    private async Task ReadPlotsAsync(long current)
    {
        string index; long consumed;
        lock (sync) { if (current != generation || disposed) return; index = plotIndex; consumed = plotIndexLines; }
        if (!File.Exists(index)) return;
        string[] lines = await File.ReadAllLinesAsync(index);
        lock (sync) if (current != generation || disposed) return;
        foreach (string line in lines.Skip((int)consumed))
        {
            string[] parts = line.Split('\t');
            if (parts.Length != 3 || !long.TryParse(parts[0], out long serial) || !long.TryParse(parts[1], out long revision)) continue;
            string path = Path.GetFullPath(parts[2]);
            if (!path.StartsWith(workingDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !File.Exists(path)) continue;
            byte[] bytes = await File.ReadAllBytesAsync(path);
            if (bytes.Length > 20 * 1024 * 1024) { Emit(current, "notice", new() { ["text"] = "A plot preview exceeds the prototype's 20 MB limit; its SVG is saved in the session folder." }); continue; }
            Emit(current, "plot", new() { ["plotId"] = $"{sessionId}-{current}-{serial}", ["revision"] = revision, ["mime"] = "image/svg+xml", ["preview"] = "data:image/svg+xml;base64," + Convert.ToBase64String(bytes), ["uri"] = new Uri(path).AbsoluteUri, ["path"] = path, ["label"] = "R plot " + serial });
        }
        lock (sync) { if (current == generation && !disposed) plotIndexLines = lines.Length; }
    }

    private static void ValidateText(string text, int maxBytes)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Contains('\0')) throw new ArgumentException("R console input cannot contain a NUL character.", nameof(text));
        if (Utf8.GetByteCount(text) > maxBytes) throw new ArgumentException($"This R input exceeds the {maxBytes:N0}-byte limit. Shorten it and try again.", nameof(text));
    }

    private void TransportFailed(long current, Exception error)
    {
        lock (sync) { if (current != generation || disposed) return; state = "stopped"; active = null; requestId = null; }
        Emit(current, "notice", new() { ["text"] = "The R connection failed. Restart R to continue. " + error.Message });
        Emit(current, "state", new() { ["state"] = "stopped", ["prompt"] = "Restart R to continue." });
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

    private async Task StopCoreAsync()
    {
        Process? child; long current;
        lock (sync) { child = process; current = generation; requestId = null; }
        if (child == null) return;
        try
        {
            if (!child.HasExited)
            {
                await SendAsync(4, null, null, current);
                try { await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(2)); }
                catch (TimeoutException) { child.Kill(entireProcessTree: true); await child.WaitForExitAsync(); }
            }
        }
        catch (Exception error) when (error is IOException or InvalidOperationException) { }
        finally { child.Dispose(); process = null; }
    }

    public void Dispose()
    {
        if (disposed) return;
        lock (sync) { disposed = true; generation++; requestId = null; }
        // Closing/restarting is the explicit destructive fallback; Stop above never kills a persistent workspace.
        try { if (process is { HasExited: false }) process.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) { }
        process?.Dispose(); process = null;
    }
}
