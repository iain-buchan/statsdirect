using StatsDirect.UI;

internal static partial class Program
{
    /// <summary>With the console's minimum raised above any installed R (the STATSDIRECT_R_CONSOLE_MINIMUM hook), the window runs each
    /// command through the installed R's Rscript: the notice says so, output and charts arrive through the same page, and objects do not
    /// persist from one command to the next.</summary>
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
            Set(host.Document, "Dirty", false); host.Document.Dispose();
        }
        finally { Environment.SetEnvironmentVariable("STATSDIRECT_R_CONSOLE_MINIMUM", null); }
    }
}
