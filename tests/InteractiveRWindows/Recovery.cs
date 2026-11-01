using Microsoft.Web.WebView2.Core;
using System.Diagnostics;
using System.Text.Json;

internal static partial class Program
{
    static async Task CheckClearOutput(ConsoleHost host)
    {
        int answered = await host.Submit("clearAnswered <- readline('CLEAR_ANSWER> ')");
        await host.Prompt(answered, "CLEAR_ANSWER>");
        await host.Submit("kept");
        await host.Finished(answered);
        int cancelled = await host.Submit("clearCancelled <- readline('CLEAR_CANCEL> ')");
        await host.Prompt(cancelled, "CLEAR_CANCEL>");
        await host.Stop(cancelled);

        const string unexecuted = "\n# unexecuted recovery edit\nnotExecutedAfterClear <- 999\n";
        await host.JS($$"""
            (()=>{
              window.StatsDirectRConsole.setMode('script');
              const field=document.getElementById('script-source');
              field.value += {{JsonSerializer.Serialize(unexecuted)}};
              field.dispatchEvent(new Event('input',{bubbles:true}));
              window.StatsDirectRConsole.setMode('console');
            })()
            """);
        await host.TwoFrames();

        var before = await host.Snapshot();
        string source = Text(before, "script"), history = before.GetProperty("history").GetRawText();
        Check(before.GetProperty("transcript").EnumerateArray().Count(e => Text(e, "kind") == "question") >= 2,
            "Clear output fixture contains both answered and Stop-cancelled questions");
        Check(!before.GetProperty("history").EnumerateArray().Any(e => e.GetString()!.Contains("notExecutedAfterClear", StringComparison.Ordinal)
                || e.GetString() == "kept"),
            "unexecuted Script edits and question answers are absent from command recall");
        await InvokeAsync(host.View, "ClearOutputAsync");
        await host.TwoFrames();
        var after = await host.Snapshot();
        Check(after.GetProperty("transcript").GetArrayLength() == 0 && Text(after, "script") == source && after.GetProperty("history").GetRawText() == history,
            "Clear output removes the transcript while preserving Script and command history");
        Check(((IEnumerable<JsonElement>)Get(host.View, "History")!).All(e => Kind(e) != "inputRequested"),
            "Clear output removes completed and cancelled questions from native replay history");
        int recovery = host.Mark;
        for (int reload = 0; reload < 2; reload++)
        {
            await Reload("ready");
            after = await host.Snapshot();
            Check(after.GetProperty("transcript").GetArrayLength() == 0 && Text(after, "script") == source
                && after.GetProperty("history").GetRawText() == history
                && await host.JS("document.querySelectorAll('.question-entry').length===0&&document.getElementById('question-announcement').textContent===''") == "true",
                "cleared questions stay cleared and exact command recall survives renderer recovery " + (reload + 1));
        }
        const string draft = "# unfinished console draft";
        string lastCommand = before.GetProperty("history").EnumerateArray().Last().GetString()!;
        await host.JS($$"""
            (()=>{
              const field=document.getElementById('console-input');field.value={{JsonSerializer.Serialize(draft)}};
              field.dispatchEvent(new Event('input',{bubbles:true}));field.setSelectionRange(field.value.length,field.value.length);
              field.dispatchEvent(new KeyboardEvent('keydown',{key:'ArrowUp',bubbles:true,cancelable:true}));
            })()
            """);
        Check(JsonSerializer.Deserialize<string>(await host.JS("document.getElementById('console-input').value")) == lastCommand,
            "Up recalls the original submitted command after clearing and repeated display recovery");
        await host.JS("document.getElementById('console-input').dispatchEvent(new KeyboardEvent('keydown',{key:'ArrowDown',bubbles:true,cancelable:true}))");
        Check(JsonSerializer.Deserialize<string>(await host.JS("document.getElementById('console-input').value")) == draft,
            "Down restores the unfinished draft after recalled commands");
        Check(host.Since(recovery).All(e => Kind(e) is not ("commandAccepted" or "inputEcho")),
            "recovering a cleared display does not resubmit commands or replay answers into R");
        await host.Run("cat('AFTER_CLEAR=',saved==42 && identical(clearAnswered,'kept') && !exists('clearCancelled') && !exists('notExecutedAfterClear'),'\\n',sep='')", "AFTER_CLEAR=TRUE");
        Check(true, "Clear output and repeated recovery retain live variables and the cancelled assignment stays absent");

        int pending = await host.Submit("clearPending <- readline('CLEAR_PENDING> '); cat('CLEAR_PENDING_REPLY=',clearPending,'\\n',sep='')");
        string requestId = Text(await host.Prompt(pending, "CLEAR_PENDING>"), "requestId");
        var pendingSnapshot = await host.Snapshot();
        source = Text(pendingSnapshot, "script"); history = pendingSnapshot.GetProperty("history").GetRawText();
        await InvokeAsync(host.View, "ClearOutputAsync");
        await host.TwoFrames();
        await CheckPending("clearing output");
        await Reload("input");
        await CheckPending("recovering the cleared display");
        await host.Submit("café β");
        await host.Finished(pending);
        Check(host.Output(pending).Contains("CLEAR_PENDING_REPLY=café β", StringComparison.Ordinal),
            "the original pending request accepts one Unicode answer after Clear output and recovery");

        await InvokeAsync(host.View, "ClearOutputAsync");
        await Reload("ready");
        var finalSnapshot = await host.Snapshot();
        Check(finalSnapshot.GetProperty("transcript").GetArrayLength() == 0
            && finalSnapshot.GetProperty("history").GetRawText() == history,
            "an answered question stays cleared after recovery while commands remain in recall and its Unicode reply stays out");

        async Task Reload(string state)
        {
            await InvokeAsync(host.View, "RecoverDisplayAsync");
            var watch = Stopwatch.StartNew();
            while (Text(await host.Snapshot(), "state") != state)
            {
                if (watch.ElapsedMilliseconds > 15000) throw new TimeoutException("The cleared display did not recover its " + state + " state.");
                await Task.Delay(30);
            }
            await host.TwoFrames();
        }
        async Task CheckPending(string action)
        {
            var snapshot = await host.Snapshot();
            var questions = snapshot.GetProperty("transcript").EnumerateArray().Where(e => Text(e, "kind") == "question").ToArray();
            Check(Text(snapshot, "state") == "input" && Text(snapshot, "requestId") == requestId && Text(snapshot, "script") == source
                && snapshot.GetProperty("history").GetRawText() == history
                && questions.Length == 1 && Text(questions[0], "requestId") == requestId
                && await host.JS("document.querySelectorAll('.question-entry').length===1") == "true",
                action + " preserves exactly the live question and its original reply token without duplicating Script");
        }
    }
    static async Task CheckDisplayRecovery(ConsoleHost host)
    {
        await host.Run("recoveryExecutions <- if(exists('recoveryExecutions')) recoveryExecutions+1 else 1; cat('RECOVERY_ONCE=',recoveryExecutions,'\\n',sep='')", "RECOVERY_ONCE=1");
        string script = Text(await host.Snapshot(), "script");
        int images = JsonSerializer.Deserialize<int>(await host.JS("document.images.length"));
        int mark = host.Mark;
        var loaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Navigation(object? sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            if (args.IsSuccess) loaded.TrySetResult();
            else loaded.TrySetException(new InvalidOperationException("R console recovery navigation failed: " + args.WebErrorStatus));
        }
        host.Browser.CoreWebView2.NavigationCompleted += Navigation;
        try
        {
            await InvokeAsync(host.View, "RecoverDisplayAsync");
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(30));
            var watch = Stopwatch.StartNew();
            while (await host.JS("typeof window.StatsDirectRConsole==='object'&&window.StatsDirectRConsole.snapshot().state==='ready'") != "true")
            {
                if (watch.ElapsedMilliseconds > 15000) throw new TimeoutException("The recovered console did not restore its ready model.");
                await Task.Delay(30);
            }
            await host.TwoFrames();
            Check(Text(await host.Snapshot(), "script") == script && (string)Get(host.Document, "LearningScript")! == script,
                "reloading the console renderer preserves Script exactly without duplicating replayed commands");
            Check(host.Since(mark).All(e => Kind(e) != "commandAccepted")
                && JsonSerializer.Deserialize<int>(await host.JS("document.images.length")) == images,
                "renderer recovery restores visible charts without resubmitting R code");
            await host.Run("cat('RECOVERY_STILL_ONCE=',recoveryExecutions,'\\n',sep='')", "RECOVERY_STILL_ONCE=1");
            Check(true, "renderer recovery retains the native R workspace and does not repeat earlier side effects");
        }
        finally { host.Browser.CoreWebView2.NavigationCompleted -= Navigation; }
    }
}
