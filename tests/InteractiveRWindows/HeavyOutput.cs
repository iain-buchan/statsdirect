using Microsoft.Web.WebView2.WinForms;
using StatsDirect.UI;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

internal static partial class Program
{
    static async Task CheckHeavyOutput(frmMain main, int lines = 20_000)
    {
        const string minimum = "STATSDIRECT_R_CONSOLE_MINIMUM";
        string? previousMinimum = Environment.GetEnvironmentVariable(minimum);
        try
        {
            Environment.SetEnvironmentVariable(minimum, "99.0");
            await CheckHeavyOutput(main, "Rscript fallback", persistent: false, lines);
            Environment.SetEnvironmentVariable(minimum, null);
            await CheckHeavyOutput(main, "persistent worker", persistent: true, lines);
        }
        finally
        {
            Environment.SetEnvironmentVariable(minimum, previousMinimum);
        }
    }

    static async Task CheckHeavyOutput(frmMain main, string mode, bool persistent, int lines)
    {
        const int transcriptLimit = 200_000;
        const string followUpSource = "cat('AFTER_HEAVY_NEXT\\n')";
        const string followUpOutput = "AFTER_HEAVY_NEXT\n";
        string lineCount = lines.ToString(CultureInfo.InvariantCulture);
        string source = $"for (i in 1:{lineCount}) cat('PROBE_HEAVY', i, '\\n')";
        string finalLine = $"PROBE_HEAVY {lineCount} \n";
        string expected = string.Concat(Enumerable.Range(1, lines)
            .Select(i => "PROBE_HEAVY " + i.ToString(CultureInfo.InvariantCulture) + " \n"));
        string retained = expected[^transcriptLimit..];
        string retainedAfterFollowUp = retained[^(transcriptLimit - followUpSource.Length - followUpOutput.Length)..];

        var report = (Form)Call(main, "CreateReport")!;
        var reportView = Get(report, "LearningReport")!;
        await ((Task)Get(reportView, "Ready")!).WaitAsync(TimeSpan.FromSeconds(45));
        var reportBrowser = (WebView2)Get(reportView, "Browser")!;
        var script = (Form)Call(Get(main, "Learning")!, "OpenScript", "", "Heavy R output", false, true)!;
        var host = new ConsoleHost(script, recordEvents: false);
        hosts.Add(host);
        Set(host.View, "WorkingDirectory", Path.Combine(artifacts, "working-" + hosts.Count));
        await host.Start().WaitAsync(TimeSpan.FromSeconds(45));
        RememberWorker(host);
        object session = host.Session;
        Check((bool)Get(session, "PersistentWorkspace")! == persistent, $"{mode} is active for the {lines:N0}-line test");
        await BrowserProbe("""
            window.__heavyOutputMessages = 0;
            window.__heavyOutputEvents = 0;
            window.chrome.webview.addEventListener('message', ({data}) => {
              const events = Array.isArray(data) ? data : [data];
              const outputCount = events.filter(item => item?.type === 'output' && item.stream === 'stdout').length;
              if (outputCount) ++window.__heavyOutputMessages;
              window.__heavyOutputEvents += outputCount;
            });
            """);

        // Compare at the producer as events arrive, without journalling a million
        // fragments or introducing a full-transcript copy onto the measured UI thread.
        var elapsed = new Stopwatch();
        var completion = new TaskCompletionSource<(string Status, TimeSpan At)>(TaskCreationOptions.RunContinuationsAsynchronously);
        int emittedCharacters = 0, outputEvents = 0;
        bool matchesExpected = true;
        Action<JsonElement> observe = item =>
        {
            if (Kind(item) == "output" && Text(item, "stream") == "stdout")
            {
                string value = Text(item, "text");
                matchesExpected &= expected.AsSpan(emittedCharacters).StartsWith(value, StringComparison.Ordinal);
                emittedCharacters = Math.Min(expected.Length, emittedCharacters + value.Length);
                outputEvents++;
            }
            else if (Kind(item) == "commandFinished")
                completion.TrySetResult((Text(item, "status"), elapsed.Elapsed));
        };

        TimeSpan maximumUiGap = TimeSpan.Zero, lastUiTick = TimeSpan.Zero;
        using var uiTimer = new System.Windows.Forms.Timer { Interval = 15 };
        uiTimer.Tick += (_, _) =>
        {
            TimeSpan now = elapsed.Elapsed;
            if (now - lastUiTick > maximumUiGap) maximumUiGap = now - lastUiTick;
            lastUiTick = now;
        };
        var sessionEvent = session.GetType().GetEvent("Event")!;
        JsonElement page;
        TimeSpan pageReady;
        sessionEvent.AddEventHandler(session, observe);
        try
        {
            elapsed.Start();
            uiTimer.Start();
            await host.Submit(source).WaitAsync(TimeSpan.FromSeconds(12));
            page = await WaitForReadyOutput(finalLine, lines > 20_000 ? 150 : 45);
            pageReady = elapsed.Elapsed;
            if (pageReady - lastUiTick > maximumUiGap) maximumUiGap = pageReady - lastUiTick;
        }
        finally
        {
            uiTimer.Stop();
            sessionEvent.RemoveEventHandler(session, observe);
        }
        var finished = await completion.Task.WaitAsync(TimeSpan.FromSeconds(2));
        int messages = page.GetProperty("messages").GetInt32(), browserOutputEvents = page.GetProperty("events").GetInt32();
        Console.WriteLine($"INFO {mode}: {lines:N0} lines. R finished in {finished.At.TotalSeconds:F2} s, page ready in {pageReady.TotalSeconds:F2} s, "
            + $"display lag {(pageReady - finished.At).TotalSeconds:F2} s, longest UI tick gap {maximumUiGap.TotalMilliseconds:F0} ms "
            + $"({outputEvents:N0} R output events, {browserOutputEvents:N0} browser output events in {messages:N0} messages)");
        Check(matchesExpected && emittedCharacters == expected.Length && finished.Status == "ok",
            $"{mode} executes all {lines:N0} numbered lines in order and finishes normally");
        Check(messages > 0 && messages <= outputEvents / 2,
            mode + " combines multiple R output events per web message during the burst");
        if (persistent)
            Check(browserOutputEvents > 0 && browserOutputEvents <= outputEvents / 2,
                mode + " coalesces at least half of the R output fragments before sending them to the page");

        const string transcriptState = """
            (() => {
              const view = document.getElementById('transcript');
              return {
                output: [...view.querySelectorAll('.output-entry.stdout')].map(row => row.textContent).join(''),
                trimmed: view.querySelector('.notice')?.textContent.startsWith('Earlier output is omitted') === true,
                consoleVisible: !document.getElementById('console-panel').hidden
              };
            })()
            """;
        page = await BrowserProbe(transcriptState);
        Check(Text(page, "output") == retained && page.GetProperty("trimmed").GetBoolean() && page.GetProperty("consoleVisible").GetBoolean(),
            mode + " shows the exact retained 200,000-character tail, including the final line and the trimming notice");

        await host.Submit(followUpSource).WaitAsync(TimeSpan.FromSeconds(12));
        await WaitForReadyOutput(followUpOutput, 30);
        page = await BrowserProbe(transcriptState);
        Check(Text(page, "output") == retainedAfterFollowUp + followUpOutput,
            mode + " keeps the preceding output tail after another prompt command");

        await host.TwoFrames().WaitAsync(TimeSpan.FromSeconds(12));
        var added = await BrowserProbe("""
            (() => {
              const button = document.getElementById('add-output-to-report');
              if (!button || button.hidden || button.disabled) return false;
              button.click();
              return true;
            })()
            """);
        Check(added.ValueKind == JsonValueKind.True, mode + " can add the completed console output to a report");

        string reportText;
        var reportWait = Stopwatch.StartNew();
        do
        {
            reportText = JsonSerializer.Deserialize<string>(await reportBrowser.CoreWebView2.ExecuteScriptAsync(
                "document.querySelector('#results pre')?.textContent ?? ''").WaitAsync(TimeSpan.FromSeconds(8))) ?? "";
            if (reportText.Length > 0 || reportWait.Elapsed > TimeSpan.FromSeconds(20)) break;
            await Task.Delay(100);
        } while (true);
        Check(reportText == (retainedAfterFollowUp + followUpOutput).TrimEnd(),
            mode + " adds the retained text from both commands to the selected report");

        Set(report, "Dirty", false);
        report.Dispose();
        if (persistent && lines > 20_000) await CheckStopDuringHeavyOutput();
        Set(host.Document, "Dirty", false);
        host.Document.Dispose();

        async Task CheckStopDuringHeavyOutput()
        {
            host.Document.Activate();
            var stopped = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            Action<JsonElement> observeStop = item =>
            {
                if (Kind(item) == "commandFinished") stopped.TrySetResult(Text(item, "status"));
            };
            sessionEvent.AddEventHandler(session, observeStop);
            try
            {
                await host.Submit("repeat cat('PROBE_STOP', '\\n')").WaitAsync(TimeSpan.FromSeconds(12));
                var wait = Stopwatch.StartNew();
                while (true)
                {
                    var clicked = await BrowserProbe("""
                        (() => {
                          const stop = document.getElementById('stop');
                          const output = [...document.querySelectorAll('#transcript .output-entry.stdout')].at(-1);
                          if (stop.disabled || !output?.textContent.includes('PROBE_STOP ')) return false;
                          stop.click();
                          return true;
                        })()
                        """);
                    if (clicked.GetBoolean()) break;
                    if (wait.Elapsed > TimeSpan.FromSeconds(15))
                        throw new TimeoutException("R's ongoing output and Stop did not become available together.");
                    await Task.Delay(100);
                }
                Check(await stopped.Task.WaitAsync(TimeSpan.FromSeconds(20)) == "interrupted",
                    "Stop interrupts the persistent worker while it is printing sustained output");
            }
            finally
            {
                sessionEvent.RemoveEventHandler(session, observeStop);
            }

            await host.Submit("cat('AFTER_STOP_READY\\n')").WaitAsync(TimeSpan.FromSeconds(30));
            await WaitForReadyOutput("AFTER_STOP_READY\n", 30);
            Check(host.State == "ready", "the persistent console accepts another command after stopping sustained output");
        }

        async Task<JsonElement> WaitForReadyOutput(string lastOutput, int seconds)
        {
            var wait = Stopwatch.StartNew();
            while (true)
            {
                var status = await BrowserProbe($$"""
                    (() => {
                      const stdout = [...document.querySelectorAll('#transcript .output-entry.stdout')].at(-1);
                      return {
                        ready: !document.getElementById('console-input').disabled && document.getElementById('prompt').textContent === '> ',
                        renderedLastLine: !!stdout?.textContent.endsWith({{JsonSerializer.Serialize(lastOutput)}}),
                        messages: window.__heavyOutputMessages,
                        events: window.__heavyOutputEvents
                      };
                    })()
                    """);
                if (status.GetProperty("ready").GetBoolean() && status.GetProperty("renderedLastLine").GetBoolean() && host.State == "ready")
                    return status;
                if (wait.Elapsed > TimeSpan.FromSeconds(seconds))
                    throw new TimeoutException(mode + " did not render the final R output and ready prompt. Native state: " + host.State);
                await Task.Delay(100);
            }
        }

        async Task<JsonElement> BrowserProbe(string javascript)
        {
            try
            {
                return JsonSerializer.Deserialize<JsonElement>(await host.JS(javascript).WaitAsync(TimeSpan.FromSeconds(8)));
            }
            catch (TimeoutException error)
            {
                throw new TimeoutException(mode + " console page stopped answering. Native state: " + host.State, error);
            }
        }
    }
}
