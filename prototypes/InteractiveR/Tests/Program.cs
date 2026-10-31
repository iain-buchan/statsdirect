using System.Diagnostics;
using System.Text;
using System.Text.Json;
using StatsDirect.InteractiveR;

// These checks use a real installed R, not mocked prompt strings. Every wait has
// a deadline and observes a structured event, rather than assuming a sleep was
// long enough for R to have reached a prompt.
string option(string name, string fallback)
{
    int index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? Path.GetFullPath(args[index + 1]) : fallback;
}
string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
string helper = option("--native-helper", Path.Combine(root, "Native", "bin", "StatsDirect.RConsole.Native.exe"));
string rHome = option("--r-home", @"C:\Program Files\R\R-4.6.1");
string working = option("--working-directory", Path.Combine(Path.GetTempPath(), "statsdirect-interactive-r-tests", Guid.NewGuid().ToString("N")));
Directory.CreateDirectory(working);
if (!File.Exists(helper)) throw new FileNotFoundException("Build the native prototype helper first, or specify --native-helper.", helper);
using var session = new RConsoleSession(helper, rHome, working);
var events = new EventLog();
session.Event += events.Add;
var extraLogs = new Dictionary<string, EventLog>();
var results = new List<object>();
int passed = 0, failed = 0;

async Task check(string name, Func<Task> test)
{
    var watch = Stopwatch.StartNew();
    try
    {
        await test();
        passed++;
        Console.WriteLine($"PASS {name} ({watch.Elapsed.TotalSeconds:F1}s)");
        results.Add(new { name, passed = true, seconds = watch.Elapsed.TotalSeconds });
    }
    catch (Exception error)
    {
        failed++;
        Console.WriteLine($"FAIL {name}: {error.Message}");
        results.Add(new { name, passed = false, seconds = watch.Elapsed.TotalSeconds, error = error.ToString() });
        Console.WriteLine(events.Recent());
        // Stop the failed expression before trying to recover for later checks.
        if (!IsReady(events.Slice(0).LastOrDefault(e => Type(e) == "state")))
            try { int mark = events.Mark; await session.InterruptAsync(); await events.Wait(mark, IsReady, TimeSpan.FromSeconds(5)); } catch { }
    }
}
static void require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
async Task<Run> run(string source, string origin = "console", string status = "ok")
{
    int mark = events.Mark;
    await session.SubmitAsync(source, origin);
    JsonElement finished = await events.Wait(mark, IsFinished);
    require(Text(finished, "status") == status, $"Expected {status}, got {Text(finished, "status")} for {source}");
    await events.Wait(mark, IsReady);
    return new Run(events.Output(mark), finished);
}
async Task<JsonElement> input(int mark) => await events.Wait(mark, e => Type(e) == "inputRequested");
async Task stop(int mark)
{
    await session.InterruptAsync();
    JsonElement finished = await events.Wait(mark, IsFinished);
    require(Text(finished, "status") == "interrupted", "Stop must finish as interrupted, not success/error.");
    await events.Wait(mark, IsReady);
}

await check("start: real interactive R and persistent assignments", async () =>
{
    int mark = events.Mark;
    await session.StartAsync();
    await events.Wait(mark, IsReady, TimeSpan.FromSeconds(30));
    var result = await run("cat('INTERACTIVE=', interactive(), '\\n', sep=''); preserved <- 42; shared <- 10");
    require(result.Output.Contains("INTERACTIVE=TRUE"), "R is not genuinely interactive.");
    result = await run("cat('PRESERVED=', preserved, '\\n', sep='')");
    require(result.Output.Contains("PRESERVED=42"), "An assignment did not survive the next submission.");
});
if (failed != 0) { WriteEvidence(); return; }
await check("readline: real question and UTF-8 answer", async () =>
{
    int mark = events.Mark;
    await session.SubmitAsync("answer <- readline('Your answer (not an R prompt): '); cat('REPLY=', enc2utf8(answer), '\\n', sep='')");
    var request = await input(mark);
    require(Text(request, "prompt").Contains("Your answer"), "The real question was lost.");
    await session.ReplyAsync(Number(request, "requestId"), "café α");
    var finished = await events.Wait(mark, IsFinished);
    require(Text(finished, "status") == "ok", "Answering readline failed.");
    await events.Wait(mark, IsReady);
    require(events.Output(mark).Contains("REPLY=café α"), "The UTF-8 reply did not reach readline intact.");
});
await check("readline Stop: no answer assignment or following side effect", async () =>
{
    await run("if (exists('cancel_answer')) rm(cancel_answer); if (exists('cancel_sentinel')) rm(cancel_sentinel)");
    int mark = events.Mark;
    await session.SubmitAsync("cancel_answer <- readline('STOP THIS QUESTION: '); cancel_sentinel <- TRUE; cat('UNSAFE-AFTER-READLINE')");
    await input(mark);
    await stop(mark);
    require(!events.Output(mark).Contains("UNSAFE-AFTER-READLINE"), "The command continued after Stop.");
    var result = await run("cat('CANCEL=', exists('cancel_answer'), exists('cancel_sentinel'), preserved, '\\n')");
    require(result.Output.Contains("CANCEL= FALSE FALSE 42"), "Stop answered readline, executed following code, or lost existing variables.");
});
await check("menu: numbered selection reaches real R menu", async () =>
{
    int mark = events.Mark;
    await session.SubmitAsync("chosen <- menu(c('First choice','Second choice'), graphics=FALSE); cat('CHOSEN=', chosen, '\\n', sep='')");
    var request = await input(mark);
    await session.ReplyAsync(Number(request, "requestId"), "2");
    await events.Wait(mark, IsFinished);
    await events.Wait(mark, IsReady);
    require(events.Output(mark).Contains("CHOSEN=2"), "The menu answer did not select item 2.");
});
await check("menu Stop: preserves workspace without selecting an item", async () =>
{
    int mark = events.Mark;
    await session.SubmitAsync("menu_cancel <- menu(c('Yes','No'), graphics=FALSE); menu_after <- TRUE");
    await input(mark);
    await stop(mark);
    var result = await run("cat('MENU-CANCEL=', exists('menu_cancel'), exists('menu_after'), preserved, '\\n')");
    require(result.Output.Contains("MENU-CANCEL= FALSE FALSE 42"), "Stop selected a menu item, ran following code or lost variables.");
});
await check("CPU loop Stop: retains earlier assignments", async () =>
{
    int mark = events.Mark;
    await session.SubmitAsync("before_loop <- 123; cat('LOOP-STARTED\\n'); repeat {}; loop_after <- TRUE");
    await events.Wait(mark, e => Type(e) == "output" && Text(e, "text").Contains("LOOP-STARTED"));
    await stop(mark);
    var result = await run("cat('LOOP-CANCEL=', before_loop, preserved, exists('loop_after'), '\\n')");
    require(result.Output.Contains("LOOP-CANCEL= 123 42 FALSE"), "Loop interruption did not preserve expected workspace state.");
});
await check("sleep Stop: returns promptly with existing variables", async () =>
{
    int mark = events.Mark;
    await session.SubmitAsync("cat('SLEEP-STARTED\\n'); Sys.sleep(60); sleep_after <- TRUE");
    await events.Wait(mark, e => Type(e) == "output" && Text(e, "text").Contains("SLEEP-STARTED"));
    var watch = Stopwatch.StartNew();
    await stop(mark);
    require(watch.Elapsed < TimeSpan.FromSeconds(10), "Stop waited for Sys.sleep to finish.");
    var result = await run("cat('SLEEP-CANCEL=', preserved, exists('sleep_after'), '\\n')");
    require(result.Output.Contains("SLEEP-CANCEL= 42 FALSE"), "Sleep interruption lost data or ran the following code.");
});
await check("multiline: continuation input completes the same command", async () =>
{
    int mark = events.Mark;
    await session.SubmitAsync("if (TRUE) {");
    await events.Wait(mark, IsContinuation);
    var request = await input(mark);
    await session.ReplyAsync(Number(request, "requestId"), "multiline_value <- 73; }");
    var finished = await events.Wait(mark, IsFinished);
    require(Text(finished, "status") == "ok", "Completing multiline input failed.");
    await events.Wait(mark, IsReady);
    var result = await run("cat('MULTILINE=', multiline_value, '\\n', sep='')");
    require(result.Output.Contains("MULTILINE=73"), "Continuation input was not evaluated in the workspace.");
});
await check("multiline Stop: discards incomplete expression", async () =>
{
    int mark = events.Mark;
    await session.SubmitAsync("if (TRUE) { multiline_cancel <- TRUE");
    await events.Wait(mark, IsContinuation);
    await stop(mark);
    var result = await run("cat('INCOMPLETE-CANCEL=', exists('multiline_cancel'), preserved, '\\n')");
    require(result.Output.Contains("INCOMPLETE-CANCEL= FALSE 42"), "Stopped incomplete code leaked into the next command.");
});
await check("R error: next command runs exactly once", async () =>
{
    var result = await run("stop('expected-error'); after_error <- TRUE", status: "error");
    require(result.Output.Contains("expected-error"), "The R error message was lost.");
    result = await run("once_count <- if (exists('once_count')) once_count+1 else 1; cat('ONCE=', once_count, exists('after_error'), preserved, '\\n')");
    require(result.Output.Contains("ONCE= 1 FALSE 42"), "The next command was duplicated, failed, or lost prior variables.");
});
await check("prompt Stop: R on.exit cleanup runs during native unwind", async () =>
{
    await run("cleanup_seen <- FALSE; wait_with_cleanup <- function() { on.exit(cleanup_seen <<- TRUE); readline('CLEANUP QUESTION: '); cleanup_after <<- TRUE }");
    int mark = events.Mark;
    await session.SubmitAsync("wait_with_cleanup()");
    await input(mark);
    await stop(mark);
    var result = await run("cat('CLEANUP=', cleanup_seen, exists('cleanup_after'), preserved, '\\n')");
    require(result.Output.Contains("CLEANUP= TRUE FALSE 42"), "The interrupt skipped R cleanup or executed the code after the question.");
});
await check("output: UTF-8, no newline and fake protocol text remain literal", async () =>
{
    var result = await run("cat('café α > + {\"type\":\"ready\"}'); cat('END')");
    require(result.Output.Contains("café α > + {\"type\":\"ready\"}END"), "Output was truncated or interpreted as a prompt/protocol message.");
    // Native/package output must also remain plain text on its separate stream.
    result = await run("system('cmd /c echo NATIVE-OUTPUT'); cat('NATIVE-DONE\\n')");
    require(result.Output.Contains("NATIVE-DONE"), "A native stdout writer damaged the control protocol.");
});
await check("stale input reply: cannot answer a later prompt", async () =>
{
    int mark = events.Mark;
    await session.SubmitAsync("stale_answer <- readline('FIRST REQUEST: ')");
    var first = await input(mark);
    long staleId = Number(first, "requestId");
    await stop(mark);
    mark = events.Mark;
    await session.SubmitAsync("fresh_answer <- readline('SECOND REQUEST: '); cat('FRESH=', fresh_answer, '\\n', sep='')");
    var second = await input(mark);
    require(Number(second, "requestId") != staleId, "Input request IDs were reused in one session.");
    try { await session.ReplyAsync(staleId, "WRONG"); } catch (InvalidOperationException) { }
    await session.ReplyAsync(Number(second, "requestId"), "RIGHT");
    await events.Wait(mark, IsFinished);
    await events.Wait(mark, IsReady);
    require(events.Output(mark).Contains("FRESH=RIGHT") && !events.Output(mark).Contains("FRESH=WRONG"), "A stale input reply answered the new question.");
});
await check("invalid source and answers preserve readiness and the pending question", async () =>
{
    foreach (string invalid in new[] { "x\0y", new string('x', 8 * 1024 * 1024) })
    {
        bool rejected = false;
        try { await session.SubmitAsync(invalid); } catch (ArgumentException) { rejected = true; }
        require(rejected, "Invalid/oversized source was accepted instead of rejected before starting a command.");
    }
    var result = await run("cat('AFTER-INVALID-SOURCE=', preserved, '\\n', sep='')");
    require(result.Output.Contains("AFTER-INVALID-SOURCE=42"), "Invalid source left the session busy or damaged the workspace.");
    int mark = events.Mark;
    await session.SubmitAsync("validated_answer <- readline('VALIDATION QUESTION: '); cat('VALIDATED=', validated_answer, '\\n', sep='')");
    var request = await input(mark);
    long id = Number(request, "requestId");
    foreach (string invalid in new[] { "bad\0answer", "two\nlines", new string('x', 100_000) })
    {
        bool rejected = false;
        try { await session.ReplyAsync(id, invalid); } catch (ArgumentException) { rejected = true; }
        require(rejected, "An invalid/oversized prompt reply was accepted.");
    }
    await session.ReplyAsync(id, "VALID");
    await events.Wait(mark, IsFinished); await events.Wait(mark, IsReady);
    require(events.Output(mark).Contains("VALIDATED=VALID"), "Rejected answers consumed or invalidated the pending input request.");
});
await check("Script and Console use the same workspace", async () =>
{
    await run("script_value <- shared + 6", "script");
    var result = await run("cat('SHARED=', script_value, '\\n', sep='')");
    require(result.Output.Contains("SHARED=16"), "Script and Console are using different R workspaces.");
});
JsonElement firstDefaultPlot = default;
byte[] firstDefaultBytes = [];
await check("default plots: SVG snapshot is displayed", async () =>
{
    int mark = events.Mark;
    await run("plot(1:3); default_device <- unname(dev.cur())");
    var plot = events.Slice(mark).LastOrDefault(e => Type(e) == "plot");
    require(plot.ValueKind == JsonValueKind.Object, "An ordinary plot() did not produce a plot event.");
    require(Text(plot, "mime") == "image/svg+xml", "The Windows default plot is not SVG.");
    string path = Text(plot, "path");
    firstDefaultBytes = File.ReadAllBytes(path);
    string svg = Encoding.UTF8.GetString(firstDefaultBytes);
    require(svg.Contains("<svg") && svg.Contains("<path"), "The ordinary plot snapshot is empty.");
    firstDefaultPlot = plot.Clone();
});
await check("default plots: numerical commands do not repeat the unchanged chart", async () =>
{
    require(firstDefaultPlot.ValueKind == JsonValueKind.Object, "The initial default plot test failed.");
    int mark = events.Mark;
    var result = await run("cat('NON-GRAPHICAL=', mean(1:3), '\\n', sep='')");
    require(result.Output.Contains("NON-GRAPHICAL=2"), "The numerical command did not run.");
    require(!events.Slice(mark).Any(e => Type(e) == "plot"), "A command which did not draw anything repeated the previous chart.");
});
await check("default plots: abline revises the same plot without overwriting its earlier asset", async () =>
{
    require(firstDefaultPlot.ValueKind == JsonValueKind.Object, "The initial default plot test failed.");
    int mark = events.Mark;
    var result = await run("abline(h=2, col='red'); cat('DEFAULT-DEVICE=', identical(unname(dev.cur()), default_device), '\\n', sep='')");
    require(result.Output.Contains("DEFAULT-DEVICE=TRUE"), "The default graphics device did not survive the next command.");
    var plot = events.Slice(mark).LastOrDefault(e => Type(e) == "plot");
    require(plot.ValueKind == JsonValueKind.Object && Text(plot, "plotId") == Text(firstDefaultPlot, "plotId"), "abline produced a new plot instead of updating the current plot.");
    require(Number(plot, "revision") > Number(firstDefaultPlot, "revision"), "The revised plot has no newer revision.");
    require(IsRedSvg(File.ReadAllText(Text(plot, "path"))), "The displayed SVG revision does not contain the red reference line.");
    require(File.ReadAllBytes(Text(firstDefaultPlot, "path")).SequenceEqual(firstDefaultBytes), "A later command overwrote the previous immutable SVG asset.");
});
await check("default plots: two plots in one command retain two separate images", async () =>
{
    var earlierIds = events.Slice(0).Where(e => Type(e) == "plot").Select(e => Text(e, "plotId")).ToHashSet();
    int mark = events.Mark;
    await run("plot(1:4, main='First default plot'); plot(4:1, main='Second default plot')");
    var newPlots = events.Slice(mark).Where(e => Type(e) == "plot" && !earlierIds.Contains(Text(e, "plotId"))).GroupBy(e => Text(e, "plotId")).Select(g => g.Last()).ToArray();
    require(newPlots.Length == 2, $"Expected two new plot IDs, received {newPlots.Length}.");
    require(newPlots.Select(e => Text(e, "path")).Distinct().Count() == 2, "The two new plots share an overwritten asset path.");
    foreach (var plot in newPlots) require(File.ReadAllText(Text(plot, "path")).Contains("<path"), "A retained default plot is blank.");
});
await check("plots: the graphics device survives follow-on commands", async () =>
{
    await run("svg('follow-on.svg'); plot(1:3); plot_device <- unname(dev.cur())");
    var result = await run("abline(h=2, col='red'); cat('SAME-DEVICE=', identical(unname(dev.cur()), plot_device), '\\n', sep=''); dev.off()");
    require(result.Output.Contains("SAME-DEVICE=TRUE"), "The graphics device was closed between commands.");
    string svg = File.ReadAllText(Path.Combine(working, "follow-on.svg"));
    require(svg.Contains("<svg") && IsRedSvg(svg), "The follow-on red reference line was not saved in the SVG.");
    await run("svg('page-%03d.svg'); plot(1:3); plot(3:1); dev.off()");
    require(File.Exists(Path.Combine(working, "page-001.svg")) && File.Exists(Path.Combine(working, "page-002.svg")), "Two plots overwrote one another instead of creating two SVG pages.");
});
await check("restart: new generation, cleared R variables and usable prompt", async () =>
{
    long oldGeneration = events.LatestGeneration;
    int mark = events.Mark;
    await session.RestartAsync();
    await events.Wait(mark, IsReady, TimeSpan.FromSeconds(30));
    require(events.LatestGeneration > oldGeneration, "Restart did not change the session generation.");
    var result = await run("cat('RESTART=', exists('preserved'), exists('script_value'), interactive(), '\\n')");
    require(result.Output.Contains("RESTART= FALSE FALSE TRUE"), "Restart retained the old workspace or lost interactive mode.");
    var afterRestart = events.Slice(mark);
    int firstNewGeneration = Array.FindIndex(afterRestart, e => Number(e, "generation") > oldGeneration);
    require(firstNewGeneration >= 0 && afterRestart.Skip(firstNewGeneration).All(e => Number(e, "generation") > oldGeneration), "Old-generation events escaped after the new session started.");
});
await check("restart during readline: clears variables and rejects the old answer", async () =>
{
    using var isolated = new RConsoleSession(helper, rHome, Path.Combine(working, "restart-question"));
    var log = new EventLog(); isolated.Event += log.Add; extraLogs.Add("restart-question", log);
    await isolated.StartAsync();
    int mark = log.Mark;
    await isolated.SubmitAsync("old_data <- 91; old_answer <- readline('QUESTION BEFORE RESTART: '); old_after <- TRUE");
    var oldRequest = await log.Wait(mark, e => Type(e) == "inputRequested");
    long oldRequestId = Number(oldRequest, "requestId"), oldGeneration = Number(oldRequest, "generation");
    mark = log.Mark;
    await isolated.RestartAsync();
    await log.Wait(mark, e => IsReady(e) && Number(e, "generation") > oldGeneration);
    mark = log.Mark;
    await isolated.SubmitAsync("fresh_answer <- readline('QUESTION AFTER RESTART: '); fresh_after <- TRUE");
    var freshRequest = await log.Wait(mark, e => Type(e) == "inputRequested");
    bool rejected = false;
    try { await isolated.ReplyAsync(oldRequestId, "STALE"); } catch (InvalidOperationException) { rejected = true; }
    require(rejected, $"The adapter accepted an answer belonging to the pre-Restart session (old requestId {oldRequestId}, new requestId {Number(freshRequest, "requestId")}).");
    await isolated.ReplyAsync(Number(freshRequest, "requestId"), "CURRENT");
    await log.Wait(mark, IsFinished); await log.Wait(mark, IsReady);
    mark = log.Mark;
    await isolated.SubmitAsync("cat('RESTART-QUESTION=', exists('old_data'), exists('old_answer'), exists('old_after'), fresh_answer, fresh_after, '\\n')");
    await log.Wait(mark, IsFinished); await log.Wait(mark, IsReady);
    require(log.Output(mark).Contains("RESTART-QUESTION= FALSE FALSE FALSE CURRENT TRUE"), "The restarted question reused old values or did not receive the current answer.");
});
await check("concurrent sessions: variables, prompts and Stop remain independent", async () =>
{
    using var left = new RConsoleSession(helper, rHome, Path.Combine(working, "parallel-left"));
    using var right = new RConsoleSession(helper, rHome, Path.Combine(working, "parallel-right"));
    var leftLog = new EventLog(); var rightLog = new EventLog();
    extraLogs.Add("parallel-left", leftLog); extraLogs.Add("parallel-right", rightLog);
    left.Event += leftLog.Add; right.Event += rightLog.Add;
    await Task.WhenAll(left.StartAsync(), right.StartAsync());
    int leftMark = leftLog.Mark, rightMark = rightLog.Mark;
    await Task.WhenAll(left.SubmitAsync("identity <- 'LEFT'; answer <- readline('LEFT QUESTION: '); after <- TRUE"), right.SubmitAsync("identity <- 'RIGHT'; answer <- readline('RIGHT QUESTION: '); after <- TRUE"));
    var requests = await Task.WhenAll(leftLog.Wait(leftMark, e => Type(e) == "inputRequested"), rightLog.Wait(rightMark, e => Type(e) == "inputRequested"));
    require(Text(requests[0], "prompt").Contains("LEFT") && Text(requests[1], "prompt").Contains("RIGHT"), "A question reached the other session.");
    // Both R processes are waiting: stopping the left must not stop/answer the right.
    await left.InterruptAsync();
    var stopped = await leftLog.Wait(leftMark, IsFinished);
    require(Text(stopped, "status") == "interrupted", "The left prompt did not stop.");
    await leftLog.Wait(leftMark, IsReady);
    await right.ReplyAsync(Number(requests[1], "requestId"), "RIGHT-ANSWER");
    var finished = await rightLog.Wait(rightMark, IsFinished);
    require(Text(finished, "status") == "ok", "Stopping one process interrupted the other process.");
    await rightLog.Wait(rightMark, IsReady);
    leftMark = leftLog.Mark; rightMark = rightLog.Mark;
    await Task.WhenAll(left.SubmitAsync("cat('ISOLATED=', identity, exists('answer'), exists('after'), '\\n')"), right.SubmitAsync("cat('ISOLATED=', identity, answer, after, '\\n')"));
    await Task.WhenAll(leftLog.Wait(leftMark, IsFinished), rightLog.Wait(rightMark, IsFinished));
    await Task.WhenAll(leftLog.Wait(leftMark, IsReady), rightLog.Wait(rightMark, IsReady));
    require(leftLog.Output(leftMark).Contains("ISOLATED= LEFT FALSE FALSE"), "The left workspace acquired the other process's answer or data.");
    require(rightLog.Output(rightMark).Contains("ISOLATED= RIGHT RIGHT-ANSWER TRUE"), "The right workspace was changed by the other process.");
    require(Text(requests[0], "sessionId") != Text(requests[1], "sessionId"), "Concurrent sessions have the same identity.");
});
await check("event envelope: monotonic sequence within each generation", () =>
{
    long generation = -1, sequence = -1;
    foreach (var entry in events.Slice(0))
    {
        require(entry.TryGetProperty("protocolVersion", out var protocol) && protocol.GetInt32() == 1, "Missing/unsupported event protocol version.");
        long currentGeneration = Number(entry, "generation"), currentSequence = Number(entry, "seq");
        require(currentGeneration >= generation, "An event from an earlier generation was delivered late.");
        require(currentGeneration != generation || currentSequence > sequence, "Event sequence is not strictly increasing.");
        generation = currentGeneration; sequence = currentSequence;
    }
    return Task.CompletedTask;
});

WriteEvidence();

void WriteEvidence()
{
    File.WriteAllText(Path.Combine(working, "events.ndjson"), string.Join("\n", events.Slice(0).Select(e => e.GetRawText())), new UTF8Encoding(false));
    foreach (var (name, log) in extraLogs)
        File.WriteAllText(Path.Combine(working, name + ".events.ndjson"), string.Join("\n", log.Slice(0).Select(e => e.GetRawText())), new UTF8Encoding(false));
    File.WriteAllText(Path.Combine(working, "results.json"), JsonSerializer.Serialize(new { passed, failed, helper, rHome, workingDirectory = working, tests = results }, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"{passed} passed, {failed} failed. Evidence: {working}");
    Environment.ExitCode = failed == 0 ? 0 : 1;
}

static string Type(JsonElement e) => Text(e, "type");
static string Text(JsonElement e, string name) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
static long Number(JsonElement e, string name)
{
    if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(name, out var value)) return -1;
    if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number)) return number;
    return name == "requestId" && value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), out number) ? number : -1;
}
static bool IsReady(JsonElement e) => Type(e) == "state" && Text(e, "state") == "ready";
static bool IsContinuation(JsonElement e) => Type(e) == "state" && Text(e, "state") == "continuation";
static bool IsFinished(JsonElement e) => Type(e) == "commandFinished";
static bool IsRedSvg(string svg)
{
    string compact = string.Concat(svg.Where(c => !char.IsWhiteSpace(c)));
    return compact.Contains("rgb(100%,0%,0%)") || compact.Contains("#FF0000", StringComparison.OrdinalIgnoreCase) || compact.Contains("rgb(255,0,0)");
}
record Run(string Output, JsonElement Finished);

sealed class EventLog
{
    readonly object gate = new();
    readonly List<JsonElement> entries = new();
    TaskCompletionSource changed = NewSignal();
    static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    public int Mark { get { lock (gate) return entries.Count; } }
    public long LatestGeneration { get { lock (gate) return entries.Count == 0 ? -1 : entries[^1].GetProperty("generation").GetInt64(); } }
    public void Add(JsonElement entry)
    {
        TaskCompletionSource signal;
        lock (gate) { entries.Add(entry.Clone()); signal = changed; changed = NewSignal(); }
        signal.TrySetResult();
    }
    public JsonElement[] Slice(int start) { lock (gate) return entries.Skip(start).ToArray(); }
    public string Output(int start) => string.Concat(Slice(start).Where(e => e.GetProperty("type").GetString() == "output").Select(e => e.GetProperty("text").GetString()));
    public string Recent() => string.Join("\n", Slice(Math.Max(0, Mark - 14)).Select(e => JsonSerializer.Serialize(e.EnumerateObject().Where(p => p.Name != "preview").ToDictionary(p => p.Name, p => p.Value))));
    public async Task<JsonElement> Wait(int start, Func<JsonElement, bool> predicate, TimeSpan? timeout = null)
    {
        using var deadline = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(15));
        while (true)
        {
            Task next;
            lock (gate)
            {
                for (int index = start; index < entries.Count; index++) if (predicate(entries[index])) return entries[index];
                next = changed.Task;
            }
            try { await next.WaitAsync(deadline.Token); }
            catch (OperationCanceledException) { throw new TimeoutException("Timed out waiting for the expected R event. Recent events:\n" + Recent()); }
        }
    }
}
