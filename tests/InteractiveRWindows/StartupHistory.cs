using StatsDirect.UI;
using System.Text.Json;

internal static partial class Program
{
    static async Task CheckStartupHistory(frmMain main)
    {
        var host = await NewConsole(main);
        const string source = "# Startup café β\n"
            + "startupHistoryRuns <- if (exists('startupHistoryRuns', inherits=FALSE)) startupHistoryRuns + 1L else 1L\n"
            + "startupValues <- c(\n  4, 5,\n  6\n)\n"
            + "cat('STARTUP_HISTORY=',startupHistoryRuns,':',sum(startupValues),'\\n',sep='')\n";
        Call(host.Document, "ShowR", source, "Startup history check", null, false);
        await host.TwoFrames();
        var loaded = await host.Snapshot();
        Check(Get(host.View, "Session") == null && loaded.GetProperty("history").GetArrayLength() == 0
            && Text(loaded, "script") == source,
            "loading startup source without running it does not add unexecuted code to command recall");

        int mark = host.Mark;
        Call(host.Document, "ShowR", source, "Startup history check", null, true);
        await host.Finished(mark);
        RememberWorker(host);
        await host.TwoFrames();
        var started = await host.Snapshot();
        Check(host.Output(mark).Contains("STARTUP_HISTORY=1:15", StringComparison.Ordinal)
            && started.GetProperty("history").EnumerateArray().Count(item => item.GetString() == source) == 1
            && Text(started, "script") == source,
            "the actual startup run recalls its complete multiline source once without duplicating Script");

        const string draft = "# unfinished console draft";
        int beforeRecall = host.Mark;
        await host.JS($$"""
            (()=>{
              window.StatsDirectRConsole.setMode('console');
              const input=document.getElementById('console-input'); input.value={{JsonSerializer.Serialize(draft)}};
              input.dispatchEvent(new Event('input',{bubbles:true})); input.setSelectionRange(input.value.length,input.value.length);
              input.dispatchEvent(new KeyboardEvent('keydown',{key:'ArrowUp',bubbles:true,cancelable:true}));
            })()
            """);
        string recalled = JsonSerializer.Deserialize<string>(await host.JS("document.getElementById('console-input').value"))!;
        Check(recalled == source && !host.Since(beforeRecall).Any(item => Kind(item) == "commandAccepted")
            && Text(await host.Snapshot(), "script") == source,
            "Up recalls the exact startup batch without executing code or duplicating the saved script");
        await host.JS("document.getElementById('console-input').dispatchEvent(new KeyboardEvent('keydown',{key:'ArrowDown',bubbles:true,cancelable:true}))");
        Check(JsonSerializer.Deserialize<string>(await host.JS("document.getElementById('console-input').value")) == draft,
            "Down after startup recall restores the user's unfinished console draft");

        int beforeRecovery = host.Mark;
        await InvokeAsync(host.View, "RecoverDisplayAsync");
        await host.WaitBrowserReady();
        await host.TwoFrames();
        var recovered = await host.Snapshot();
        bool restored = recovered.GetProperty("history").EnumerateArray().Count(item => item.GetString() == source) == 1
            && Text(recovered, "script") == source
            && !host.Since(beforeRecovery).Any(item => Kind(item) == "commandAccepted");
        await host.Run("cat('STARTUP_STILL_ONCE=',startupHistoryRuns,'\\n',sep='')", "STARTUP_STILL_ONCE=1");
        Check(restored, "display recovery restores startup recall without running the startup batch again");
    }
}
