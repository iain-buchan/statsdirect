using StatsDirect.UI;
using System.Diagnostics;
using System.Text.Json;

internal static partial class Program
{
    static async Task CheckClosingAndSaving(frmMain main)
    {
        string path = Path.Combine(artifacts, "close-source.R");
        const string original = "# original saved source\ncloseVariable <- 7\n";
        File.WriteAllText(path, original, new System.Text.UTF8Encoding(false));
        byte[] originalBytes = File.ReadAllBytes(path);
        Check((bool)Call(main, "OpenFile", path, false)! && main.ActiveMdiChild is frmScript,
            "close/save fixture opens an owned R file through the actual dispatcher");
        var host = new ConsoleHost(main.ActiveMdiChild!); hosts.Add(host);
        await host.Ready();
        Set(host.View, "WorkingDirectory", Path.Combine(artifacts, "working-" + hosts.Count));
        await host.Start(); RememberWorker(host);
        await host.Run("closeVariable <- 7; cat('CLOSE_READY\\n')", "CLOSE_READY");
        await SetBrowserSource(host, "# unsaved browser edit café α\ncloseVariable <- 9\n");
        int worker = ((Process)Get(host.Session, "process")!).Id;

        var cancelled = await AnswerClose(main, host, "Cancel");
        Check(cancelled.Asked && cancelled.Question.Contains("changes that have not been saved", StringComparison.Ordinal)
            && !host.Document.IsDisposed && host.Document.Visible && (bool)Get(host.Document, "Dirty")!,
            "Cancel in the real unsaved-R close banner keeps the script window and unsaved state");
        Check(File.ReadAllBytes(path).SequenceEqual(originalBytes) && IsAlive(worker),
            "cancelling close preserves the prior R file bytes and its native worker");
        await host.Run("cat('CLOSE_CANCEL_RETAINED=',closeVariable,'\\n',sep='')", "CLOSE_CANCEL_RETAINED=7");
        Check(true, "cancelling close preserves the R workspace variables");

        const string failedSource = "# latest browser edit before a failed save\ncloseVariable <- 10\n";
        await SetBrowserSource(host, failedSource);
        var previousAttributes = File.GetAttributes(path);
        try
        {
            File.SetAttributes(path, previousAttributes | FileAttributes.ReadOnly);
            var failed = await AnswerClose(main, host, "Yes");
            Check(failed.Asked && !host.Document.IsDisposed && host.Document.Visible && (bool)Get(host.Document, "Dirty")!
                && !(bool)Get(host.Document, "SafeToClose")!,
                "a failed save from the close banner leaves the R window open and unsaved");
            Check(File.ReadAllBytes(path).SequenceEqual(originalBytes)
                && Text(await host.Snapshot(), "script") == failedSource
                && ((string)Get(host.Document, "LearningScript")!).Replace("\r", "") == failedSource,
                "a failed close/save preserves prior file bytes and the latest browser source");
            Check(IsAlive(worker) && !Directory.GetFiles(artifacts, "close-source.R.*.tmp").Any(),
                "a failed close/save keeps its native worker and removes the temporary save file");
        }
        finally { File.SetAttributes(path, previousAttributes); }

        const string finalSource = "# final browser edit saved on close café β\ncloseVariable <- 11\n";
        await SetBrowserSource(host, finalSource);
        var saved = await AnswerClose(main, host, "Yes");
        await Wait(() => host.Document.IsDisposed, "The R document did not close after successfully saving.");
        Check(saved.Asked && File.ReadAllText(path).Replace("\r", "") == finalSource,
            "Yes in the real close banner saves the latest browser source before closing");
        await Wait(() => !IsAlive(worker), "The saved-and-closed R window left its native worker running.", 10000);
        Check(true, "closing after a successful R save terminates the owned native worker");

        var blank = await NewConsole(main);
        Check(Get(blank.View, "Session") == null, "the blank close fixture has not started R");
        var closedBlank = await AnswerClose(main, blank, "Cancel");
        await Wait(() => blank.Document.IsDisposed, "A blank unused R window did not close.");
        Check(!closedBlank.Asked, "an unused blank R session closes without a save question or starting R");
    }

    static async Task SetBrowserSource(ConsoleHost host, string source)
    {
        await host.JS($$"""
            (()=>{window.StatsDirectRConsole.setMode('script');const field=document.getElementById('script-source');field.value={{JsonSerializer.Serialize(source)}};field.dispatchEvent(new Event('input',{bubbles:true}))})()
            """);
        await Wait(() => ((string)Get(host.Document, "LearningScript")!).Replace("\r", "") == source && (bool)Get(host.Document, "Dirty")!,
            "The edited browser source did not reach the unsaved document model.");
    }

    static async Task<(bool Asked, string Question)> AnswerClose(frmMain main, ConsoleHost host, string response)
    {
        bool answered = false; string question = ""; Exception? failure = null;
        using var timer = new System.Windows.Forms.Timer { Interval = 20 };
        timer.Tick += (_, _) =>
        {
            try
            {
                if (answered || !(bool)Get(main, "waitingForModalMessage")!) return;
                question = ((Label)Get(main, "lblModalMessageText")!).Text;
                var expected = response == "Yes" ? "cmdModalMessage1" : "cmdModalMessage3";
                var button = (Button)Get(main, expected)!;
                if ((button.Text ?? "").Replace("&", "") != response) throw new InvalidOperationException("Unexpected R close-banner button: " + button.Text);
                answered = true;
                button.PerformClick();
            }
            catch (Exception error) { failure = error; timer.Stop(); }
        };
        timer.Start();
        host.Document.Close();
        await Wait(() => failure != null || host.Document.IsDisposed || answered && Get(host.Document, "PendingInteractiveClose") == null,
            "The R close request did not finish after answering its in-application save banner.", 15000);
        timer.Stop();
        if (failure != null) throw failure;
        return (answered, question);
    }

    static bool IsAlive(int id)
    {
        try { using var process = Process.GetProcessById(id); return !process.HasExited; }
        catch (ArgumentException) { return false; }
    }
}
