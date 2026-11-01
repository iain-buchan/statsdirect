using StatsDirect.UI;
using System.Text.Json;

internal static partial class Program
{
    /// <summary>With the console's minimum raised above any installed R (the STATSDIRECT_R_CONSOLE_MINIMUM hook), the window runs each
    /// command through the installed R's Rscript: the notice says so, the status line says each command runs in a fresh R, output and charts
    /// arrive through the same page, and objects do not persist from one command to the next.</summary>
    static async Task CheckRscriptFallback(frmMain main)
    {
        Environment.SetEnvironmentVariable("STATSDIRECT_R_CONSOLE_MINIMUM", "99.0");
        try
        {
            var host = await NewConsole(main);
            await host.Ready();
            await InvokeAsync(host.View, "StartAsync");
            await Wait(() => host.State == "ready", "The window did not become ready through the Rscript fallback.", 30000);
            await host.WaitBrowserReady();
            Check(host.Session.GetType().Name == "RScriptSession" && host.Events.Any(e => Kind(e) == "notice" && Text(e, "text").Contains("Rscript", StringComparison.Ordinal)),
                "with no installed R the console takes, the window runs through Rscript and its first notice says so");
            Check(await StatusLine(host) == FreshStatus, "the status line of the fallback says each command runs in a fresh R, not that the workspace is retained");
            await host.Run("fallbackObject <- 1; cat('FALLBACK_FIRST=', exists('fallbackObject'), '\\n', sep='')", "FALLBACK_FIRST=TRUE");
            await host.Run("cat('FALLBACK_SECOND=', exists('fallbackObject'), '\\n', sep='')", "FALLBACK_SECOND=FALSE");
            Check(true, "each fallback command runs in a fresh R, so an object of one command is gone in the next");
            var log = await host.Run("message('FALLBACK_MESSAGE'); plot(1:3)");
            Check(log.Any(e => Kind(e) == "output" && Text(e, "stream") == "stderr" && Text(e, "text").Contains("FALLBACK_MESSAGE", StringComparison.Ordinal)),
                "a fallback command's message arrives as stderr output, which the page colours as R's console does");
            Check(log.Any(e => Kind(e) == "plot"), "a fallback command's chart becomes a chart card");
            await host.Run("stop('FALLBACK_ERROR')", "", "console", "error");
            Check(true, "an R error in a fallback command finishes the command as an error and the window stays ready");
            // Restart from the fallback chooses the session afresh: the page must follow the replacement session, whose generation must rise above the old
            await host.Restart();
            await host.Run("cat('FALLBACK_RESTARTED=1\n')", "FALLBACK_RESTARTED=1");
            Check(host.Session.GetType().Name == "RScriptSession", "Restart from the fallback starts a fresh session the page follows, and with no console R it is the fallback again");
            Check(await StatusLine(host) == FreshStatus, "after Restart the status line still says each command runs in a fresh R");
            // The page sent again after display recovery carries the session's capabilities as the first did
            await InvokeAsync(host.View, "RecoverDisplayAsync");
            await host.WaitBrowserReady();
            Check(await StatusLine(host) == FreshStatus, "after display recovery the status line still says each command runs in a fresh R");
            Set(host.Document, "Dirty", false); host.Document.Dispose();
        }
        finally { Environment.SetEnvironmentVariable("STATSDIRECT_R_CONSOLE_MINIMUM", null); }
    }

    /// <summary>A worker file Windows cannot run (text in its place here) is not a missing worker: as when the worker cannot start with the R,
    /// the window falls back to Rscript with the notice that the console could not start, and it is usable.</summary>
    static async Task CheckUnrunnableWorkerFallback(frmMain main)
    {
        // The workers of the other windows are running from the file, so it is renamed aside, which Windows allows of a running program, not written
        string worker = Path.Combine(AppContext.BaseDirectory, "StatsDirect.RConsole.Native.exe"), keep = worker + ".keep";
        File.Move(worker, keep, true);
        try
        {
            File.WriteAllText(worker, "not a program");
            var host = await NewConsole(main);
            await host.Ready();
            await InvokeAsync(host.View, "StartAsync");
            await Wait(() => host.State == "ready", "The window did not become ready through the Rscript fallback with a worker Windows cannot run.", 30000);
            await host.WaitBrowserReady();
            Check(host.Session.GetType().Name == "RScriptSession" && host.Events.Any(e => Kind(e) == "notice" && Text(e, "text").Contains("could not start", StringComparison.Ordinal)),
                "a worker file Windows cannot run sends the window to Rscript with the notice that the console could not start");
            await host.Run("cat('UNRUNNABLE_WORKER=1\\n')", "UNRUNNABLE_WORKER=1");
            Check(await StatusLine(host) == FreshStatus, "the window with the unrunnable worker runs commands through Rscript and says each runs in a fresh R");
            Set(host.Document, "Dirty", false); host.Document.Dispose();
        }
        finally { File.Delete(worker); File.Move(keep, worker); }
    }

    const string FreshStatus = "Ready · each command runs in a fresh R";
    /// <summary>The page's status line, after the frames in which it is drawn.</summary>
    static async Task<string> StatusLine(ConsoleHost host)
    {
        await host.TwoFrames();
        return JsonSerializer.Deserialize<string>(await host.JS("document.getElementById('state').textContent")) ?? "";
    }
}
