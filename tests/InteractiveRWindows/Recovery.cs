using Microsoft.Web.WebView2.Core;
using System.Diagnostics;
using System.Text.Json;

internal static partial class Program
{
    static async Task CheckClearOutput(ConsoleHost host)
    {
        var before = await host.Snapshot();
        string source = Text(before, "script"), history = before.GetProperty("history").GetRawText();
        await InvokeAsync(host.View, "ClearOutputAsync");
        await host.TwoFrames();
        var after = await host.Snapshot();
        Check(after.GetProperty("transcript").GetArrayLength() == 0 && Text(after, "script") == source && after.GetProperty("history").GetRawText() == history,
            "Clear output removes the transcript while preserving Script and command history");
        await host.Run("cat('AFTER_CLEAR=',saved,'\\n',sep='')", "AFTER_CLEAR=42");
        Check(true, "Clear output retains the live R variables");
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
