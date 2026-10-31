using StatsDirect.UI;
using System.Diagnostics;
using System.Text.Json;

internal static partial class Program
{
    static async Task CheckRestartAndIsolation(frmMain main, ConsoleHost host)
    {
        int mark = await host.Submit("oldAnswer <- readline('OLD_GENERATION> '); oldFollowOn <- TRUE");
        var oldRequest = await host.Prompt(mark, "OLD_GENERATION>");
        var oldSnapshot = await host.Snapshot();
        await host.Stop(mark);
        string source = Text(await host.Snapshot(), "script");
        await host.Restart(); RememberWorker(host);
        Check(Text(await host.Snapshot(), "script") == source, "Restart clears R state while preserving the accumulated source script");
        await host.JS("window.chrome.webview.postMessage(" + JsonSerializer.Serialize(new
        {
            channel = "statsdirect-r-console", action = "inputReply", sessionId = Text(oldSnapshot, "sessionId"),
            generation = Number(oldSnapshot, "generation"), requestId = Number(oldRequest, "requestId").ToString(), text = "staleAssignment <- TRUE"
        }) + ")");
        await host.Run("cat('RESTART_CLEAR=',!exists('saved')&&!exists('oldAnswer')&&!exists('oldFollowOn')&&!exists('staleAssignment'),'\\n',sep='')", "RESTART_CLEAR=TRUE");
        Check(true, "Restart creates a clean R workspace and rejects a browser reply from the previous generation");

        mark = await host.Submit("currentAnswer <- readline('CURRENT_GENERATION> '); cat('CURRENT_REPLY=',currentAnswer,'\\n',sep='')");
        var currentRequest = await host.Prompt(mark, "CURRENT_GENERATION>");
        var snapshot = await host.Snapshot();
        Check(Number(currentRequest, "requestId") != Number(oldRequest, "requestId"), "prompt tokens remain distinct across Restart");
        await host.JS("window.chrome.webview.postMessage(" + JsonSerializer.Serialize(new
        {
            channel = "statsdirect-r-console", action = "inputReply", sessionId = Text(snapshot, "sessionId"),
            generation = Number(snapshot, "generation"), requestId = Number(oldRequest, "requestId").ToString(), text = "wrong stale answer"
        }) + ")");
        await host.TwoFrames();
        Check(host.State == "input" && !host.Output(mark).Contains("CURRENT_REPLY=wrong stale answer", StringComparison.Ordinal),
            "a stale token in the current generation cannot consume a new R question");
        await host.Submit("correct current answer"); await host.Finished(mark);
        Check(host.Output(mark).Contains("CURRENT_REPLY=correct current answer", StringComparison.Ordinal), "the current question remains answerable after rejecting a stale token");

        var second = await NewConsole(main); await second.Start(); RememberWorker(second);
        await host.Run("scope <- 11; cat('FIRST_SCOPE=',scope,'\\n',sep='')", "FIRST_SCOPE=11");
        await second.Run("scope <- 99; cat('SECOND_SCOPE=',scope,'\\n',sep='')", "SECOND_SCOPE=99");
        await host.Run("cat('FIRST_STILL=',scope,'\\n',sep='')", "FIRST_STILL=11");
        Check(true, "separate R script windows keep independent persistent workspaces");
        int busy = await second.Submit("cat('CLOSE_BUSY_STARTED\\n');repeat {}");
        await Wait(() => second.State == "busy" && second.Output(busy).Contains("CLOSE_BUSY_STARTED", StringComparison.Ordinal), "The close-during-run fixture did not start.");
        int pid = ((Process)Get(second.Session, "process")!).Id;
        Set(second.Document, "Dirty", false); second.Document.Dispose();
        await Wait(() =>
        {
            try { using var child = Process.GetProcessById(pid); return child.HasExited; }
            catch (ArgumentException) { return true; }
        }, "Disposing an R script window left its busy native worker running.", 10000);
        Check(true, "closing the actual R script window terminates its busy owned worker");
    }
}
