using StatsDirect.UI;
using System.Text.Json;

internal static partial class Program
{
    static async Task CheckCommandsAndPrompts(ConsoleHost host)
    {
        await host.Run("saved <- 42; cat('INTERACTIVE=', interactive(), '; UTF8=', enc2utf8('café α'), '\\n', sep='')", "INTERACTIVE=TRUE; UTF8=café α");
        Check(host.Output().Contains("INTERACTIVE=TRUE; UTF8=café α", StringComparison.Ordinal), "the actual R script window runs genuine interactive R with UTF-8 output");
        await host.Run("cat('KEPT=',saved,'\\n',sep='')", "KEPT=42");
        Check(true, "assignments survive between Console submissions");
        await host.Run("stop('expected integrated test error')", "expected integrated test error", status: "error");
        var afterError = await host.Run("afterError <- saved + 1; cat('AFTER_ERROR=',afterError,'\\n',sep='')", "AFTER_ERROR=43");
        Check(afterError.Count(e => Kind(e) == "commandAccepted") == 1 && afterError.Count(e => Kind(e) == "commandFinished") == 1,
            "an R error leaves the workspace usable and the following command executes exactly once");

        int mark = await host.Submit("answer <- readline('WINDOWS_ANSWER> '); cat('ANSWER=',enc2utf8(answer),'\\n',sep='')");
        await host.Prompt(mark, "WINDOWS_ANSWER>");
        await host.TwoFrames();
        Check(await host.JS("""(()=>{const q=[...document.querySelectorAll('.question-entry')].map(e=>e.textContent);const a=document.getElementById('question-announcement').textContent;const s=document.getElementById('state').textContent;return q.some(t=>t.includes('WINDOWS_ANSWER>'))&&/^R question [0-9]+: WINDOWS_ANSWER> $/.test(a)&&s==='R is waiting for your answer'})()""") == "true",
            "while R waits, the question is a transcript line of its own, the hidden region announces it by number, and the status line does not carry it");
        await host.Submit("reply café β");
        await host.Finished(mark);
        Check(host.Output(mark).Contains("ANSWER=reply café β", StringComparison.Ordinal), "real readline receives the separate Unicode answer in the integrated window");
        Check(await host.JS("(()=>{const s=window.StatsDirectRConsole.snapshot();return !s.script.includes('reply café β')&&!s.history.includes('reply café β')})()") == "true",
            "prompt replies are excluded from the executable script and command history");
        await host.TwoFrames();
        Check(await host.JS("""(()=>{const rows=[...document.querySelectorAll('.input-entry.reply')],last=rows.at(-1);return !!last&&last.querySelector('.echo-prompt').textContent===''&&last.textContent.includes('reply café β')&&document.getElementById('question-announcement').textContent===''})()""") == "true",
            "after the answer, the reply is a line of its own under the question, and the announcement is cleared");

        mark = await host.Submit("selected <- menu(c('First','Second'),graphics=FALSE); cat('MENU=',selected,'\\n',sep='')");
        await host.Prompt(mark, "Selection:");
        await host.Submit("2");
        await host.Finished(mark);
        Check(host.Output(mark).Contains("MENU=2", StringComparison.Ordinal), "the integrated window answers an actual numbered R menu");

        mark = await host.Submit("retry <- readline('RETRY> '); cat('RETRIED=',retry,'\\n',sep='')");
        var request = await host.Prompt(mark, "RETRY>");
        int maxBytes = request.GetProperty("maxBytes").GetInt32();
        await host.Submit(new string('x', maxBytes + 1));
        await host.WaitBrowserReady();
        await host.Submit("accepted after rejection");
        await host.Finished(mark);
        Check(host.Output(mark).Contains("RETRIED=accepted after rejection", StringComparison.Ordinal), "an oversized prompt answer is rejected without consuming or disabling the real question");

        mark = await host.Submit("if (TRUE) {");
        await host.Prompt(mark, "+", "continuation");
        await host.Submit("multiline <- saved + 2\n}");
        await host.Finished(mark);
        await host.Run("cat('MULTILINE=',multiline,'\\n',sep='')", "MULTILINE=44");
        Check(true, "an incomplete expression accepts real multiline continuation and preserves variables");

        await host.Run("cat('<img id=integrated-injection onerror=alert(1)>\\n'); cat('LITERAL\\n')", "LITERAL");
        Check(await host.JS("document.getElementById('integrated-injection')===null") == "true", "R output is literal text and cannot inject executable HTML into the console");
        await host.Submit("invalid\0source");
        await host.Run("cat('AFTER_REJECTED_SOURCE\\n')", "AFTER_REJECTED_SOURCE");
        Check(true, "rejected source leaves the integrated input ready for a valid command");
    }

    static async Task CheckStopping(ConsoleHost host)
    {
        int mark = await host.Submit("beforeStop <- 'retained'; cat('CPU_STARTED\\n'); repeat {}");
        await Wait(() => host.State == "busy" && host.Output(mark).Contains("CPU_STARTED"), "The CPU-loop fixture did not start.");
        await host.Stop(mark);
        await host.Run("cat('CPU_KEPT=',saved==42 && beforeStop=='retained','\\n',sep='')", "CPU_KEPT=TRUE");
        Check(true, "Stop unwinds a busy CPU loop and preserves completed assignments and earlier variables");

        mark = await host.Submit("sleepBefore <- saved; cat('SLEEP_STARTED\\n'); Sys.sleep(60); sleepAfter <- TRUE");
        await Wait(() => host.State == "busy" && host.Output(mark).Contains("SLEEP_STARTED"), "The sleeping R fixture did not start.");
        await host.Stop(mark);
        await host.Run("cat('SLEEP_KEPT=',sleepBefore==42 && !exists('sleepAfter'),'\\n',sep='')", "SLEEP_KEPT=TRUE");
        Check(true, "Stop interrupts Sys.sleep without executing the remaining source");

        foreach (var fixture in new[]
        {
            (Source: "cancelAnswer <- readline('CANCEL_INPUT> '); cancelledAfter <- TRUE", Prompt: "CANCEL_INPUT>", State: "input", Check: "!exists('cancelAnswer') && !exists('cancelledAfter')"),
            (Source: "cancelMenu <- menu(c('Yes','No'),graphics=FALSE); menuAfter <- TRUE", Prompt: "Selection:", State: "input", Check: "!exists('cancelMenu') && !exists('menuAfter')"),
            (Source: "incomplete <- {", Prompt: "+", State: "continuation", Check: "!exists('incomplete')")
        })
        {
            mark = await host.Submit(fixture.Source);
            await host.Prompt(mark, fixture.Prompt, fixture.State);
            await host.Stop(mark);
            await host.Run("cat('CANCEL_SAFE='," + fixture.Check + " && saved==42,'\\n',sep='')", "CANCEL_SAFE=TRUE");
            Check(true, "Stop cancels " + fixture.State + " without supplying an empty answer, assigning a result or running follow-on source");
            if (fixture.State == "input")
            {
                await host.TwoFrames();
                Check(await host.JS("[...document.querySelectorAll('.question-entry')].some(e=>e.textContent.includes('" + fixture.Prompt + "'))") == "true",
                    "the question Stop cancelled stays in the transcript as its own line");
            }
        }

        mark = await host.Submit("cleanup <- FALSE; local({on.exit(cleanup <<- TRUE); cat('CLEANUP_STARTED\\n'); repeat {}})");
        await Wait(() => host.State == "busy" && host.Output(mark).Contains("CLEANUP_STARTED"), "The R cleanup fixture did not start.");
        await host.Stop(mark);
        await host.Run("cat('CLEANUP=',cleanup,'\\n',sep='')", "CLEANUP=TRUE");
        Check(true, "Stop unwinds the real R stack and runs on.exit cleanup");
    }

    static async Task CheckScriptAndFiles(frmMain main, ConsoleHost host)
    {
        await host.Run("fromScript <- saved + multiline; cat('SCRIPT_SHARED=',fromScript,'\\n',sep='')", "SCRIPT_SHARED=86", "script");
        await host.Run("cat('CONSOLE_SHARED=',fromScript,'\\n',sep='')", "CONSOLE_SHARED=86");
        Check(true, "Script and Console use the same persistent R workspace");
        Check(((string)Get(host.Document, "LearningScript")!).Contains("fromScript <-", StringComparison.Ordinal), "the integrated Script text is synchronized with the existing AI and save model");

        const string selectedCode = "selectionValue <- saved + 10; cat('SELECTION=',selectionValue,'\\n',sep='')";
        string allCode = "outsideSelection <- TRUE\n" + selectedCode + "\nstop('unselected source must not run')";
        int mark = host.Mark;
        await host.JS($$"""
            (()=>{
              window.StatsDirectRConsole.setMode('script');
              const field=document.getElementById('script-source');
              field.value={{JsonSerializer.Serialize(allCode)}};field.dispatchEvent(new Event('input',{bubbles:true}));
              field.setSelectionRange({{"outsideSelection <- TRUE\n".Length}},{{"outsideSelection <- TRUE\n".Length + selectedCode.Length}});
              field.dispatchEvent(new Event('select',{bubbles:true}));document.getElementById('run').click();
            })()
            """);
        await host.Finished(mark);
        await host.Run("cat('SELECTION_SAFE=',selectionValue==52 && !exists('outsideSelection'),'\\n',sep='')", "SELECTION_SAFE=TRUE");
        Check(true, "Run selection executes only the selected Script code in the same workspace");

        string path = Path.Combine(artifacts, "opened-utf8.R");
        const string original = "# café α\nfileOnly <- 123\n";
        File.WriteAllText(path, original, new System.Text.UTF8Encoding(false));
        Check((bool)Call(main, "OpenFile", path, false)! && main.ActiveMdiChild is frmScript, "File > Open dispatches an R file to the actual R script window");
        var opened = new ConsoleHost(main.ActiveMdiChild!); hosts.Add(opened); await opened.Ready();
        Set(opened.View, "WorkingDirectory", Path.Combine(artifacts, "working-" + hosts.Count));
        Check(((string)Get(opened.Document, "LearningScript")!).Replace("\r", "") == original
            && !(bool)Get(opened.Document, "LearningRunning")! && Get(opened.View, "Session") == null,
            "opening an R file preserves its Unicode source without automatically running it");
        const string changed = "# changed café β\nfileOnly <- 456\n";
        await opened.JS($$"""
            (()=>{window.StatsDirectRConsole.setMode('script');const s=document.getElementById('script-source');s.value={{JsonSerializer.Serialize(changed)}};s.dispatchEvent(new Event('input',{bubbles:true}))})()
            """);
        await Wait(() => ((string)Get(opened.Document, "LearningScript")!).Replace("\r", "") == changed, "The browser Script change did not reach the document save model.");
        Check((bool)Get(opened.Document, "Dirty")!, "editing Script marks the actual document as unsaved");
        main.MainMenuStrip!.Items.OfType<ToolStripMenuItem>().Single(i => (i.Text ?? "").Replace("&", "") == "File")
            .DropDownItems.OfType<ToolStripMenuItem>().Single(i => (i.Text ?? "").Replace("&", "") == "Save").PerformClick();
        await Wait(() => File.ReadAllText(path).Replace("\r", "") == changed && !(bool)Get(opened.Document, "Dirty")!, "File Save did not finish saving the edited R source.");
        Check(File.ReadAllText(path).Replace("\r", "") == changed && !(bool)Get(opened.Document, "Dirty")!, "ordinary File Save writes the edited Unicode R script and clears the saved state");
        string afterSave = File.ReadAllText(path);
        await opened.Start(); RememberWorker(opened);
        await opened.Run("cat('OPENED_NOT_RUN=',!exists('fileOnly'),'\\n',sep='')", "OPENED_NOT_RUN=TRUE");
        Check(File.ReadAllText(path) == afterSave, "running Console code does not rewrite the saved R file");
        host.Document.Activate();
    }
}
