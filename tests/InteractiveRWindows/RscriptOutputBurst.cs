using StatsDirect.R.Interactive;
using StatsDirect.UI;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

internal static partial class Program
{
    static async Task CheckRscriptOutputBurst(frmMain main)
    {
        const int pairs = 40_000, retention = 200_000, lineLength = 13;
        const string minimum = "STATSDIRECT_R_CONSOLE_MINIMUM";
        string? previousMinimum = Environment.GetEnvironmentVariable(minimum);
        Environment.SetEnvironmentVariable(minimum, "99.0");
        try
        {
            var host = await NewConsole(main, recordEvents: false);
            await host.Start().WaitAsync(TimeSpan.FromSeconds(45));
            Check(host.Session is RScriptSession, "the output backlog fixture uses Rscript");
            var session = (RScriptSession)host.Session;
            string expected = ExpectedOutput();
            int received = 0, outputEvents = 0;
            bool ordered = true;
            string? commandStatus = null;
            var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            void Observe(JsonElement item)
            {
                if (Kind(item) == "output")
                {
                    string chunk = Text(item, "text");
                    string stream = (received / lineLength) % 2 == 0 ? "stdout" : "stderr";
                    ordered &= chunk.Length <= lineLength - received % lineLength
                        && Text(item, "stream") == stream
                        && expected.AsSpan(Math.Min(received, expected.Length)).StartsWith(chunk, StringComparison.Ordinal);
                    received += chunk.Length;
                    outputEvents++;
                }
                else if (Kind(item) == "commandFinished") commandStatus = Text(item, "status");
                else if (Kind(item) == "state" && Text(item, "state") == "ready" && commandStatus != null) ready.TrySetResult();
            }

            await host.JS("""
                window.__fallbackBurstControl = [];
                window.chrome.webview.addEventListener('message', ({data}) => {
                  for (const event of Array.isArray(data) ? data : [data]) {
                    if (['commandAccepted', 'inputEcho', 'plot', 'commandFinished'].includes(event.type)) {
                      window.__fallbackBurstControl.push(event.type);
                    } else if (event.type === 'state') window.__fallbackBurstControl.push(event.state);
                  }
                });
                """);
            var elapsed = Stopwatch.StartNew();
            const string source = "for (i in 1:40000) { cat(sprintf('SD_OUT_%05d\\n', i)); message(sprintf('SD_ERR_%05d', i)) }; plot(1:3)";
            session.Event += Observe;
            int queuedEvents, queuedCharacters;
            try
            {
                await session.SubmitAsync(source).WaitAsync(TimeSpan.FromSeconds(15));
                // Pause the fixture's UI pump until the background producer has finished.
                // This gives a reproducible worst-case queue independent of machine speed.
                if (!ready.Task.Wait(TimeSpan.FromSeconds(30)))
                    throw new TimeoutException("Rscript did not finish the synthetic output burst.");
                var queue = (ICollection)Get(host.View, "sessionEvents")!;
                lock (queue)
                {
                    queuedEvents = queue.Count;
                    queuedCharacters = queue.Cast<object>().Sum(item => (int)Get(item, "TextLength")!);
                }
            }
            finally { session.Event -= Observe; }

            double rFinished = elapsed.Elapsed.TotalSeconds;
            JsonElement page = await WaitForRenderedOutput("SD_ERR_40000\n");
            string visible = Text(page, "text");
            string native = (string)Get(host.View, "Transcript")!;
            Console.WriteLine($"INFO Rscript {pairs:N0} pairs: {outputEvents:N0} producer events, {queuedEvents:N0} queued events and {queuedCharacters:N0} queued characters when R finished, page-ready lag {elapsed.Elapsed.TotalSeconds - rFinished:F2}s after resuming the fixture UI");
            Check(commandStatus == "ok" && ordered && received == expected.Length,
                "Rscript delivers every alternating stdout and stderr fragment in exact order");
            Check(queuedCharacters is > 0 and <= retention && queuedEvents < outputEvents,
                "Rscript bounds the queued text while the UI cannot drain");
            Check(native == expected[^retention..], "the native console keeps the exact most recent Rscript text");
            Check(visible.EndsWith("SD_OUT_40000\nSD_ERR_40000\n", StringComparison.Ordinal)
                && expected.EndsWith(visible, StringComparison.Ordinal)
                && page.GetProperty("trimmed").GetBoolean(),
                "the browser shows the ordered Rscript tail and its output omission notice");
            string[] controls = page.GetProperty("controls").EnumerateArray().Select(eventName => eventName.GetString()!)
                .SkipWhile(eventName => eventName != "commandAccepted").ToArray();
            Check(controls.SequenceEqual(new[] { "commandAccepted", "inputEcho", "busy", "plot", "commandFinished", "ready" })
                && page.GetProperty("chart").GetBoolean(),
                "Rscript queue trimming preserves the command, chart and state events in order");

            await InvokeAsync(host.View, "RecoverDisplayAsync").WaitAsync(TimeSpan.FromSeconds(45));
            page = await WaitForRenderedOutput("SD_ERR_40000\n");
            Check(page.GetProperty("trimmed").GetBoolean() && expected.EndsWith(Text(page, "text"), StringComparison.Ordinal),
                "reloading the display keeps the Rscript tail and its omission notice");

            const string nextOutput = "AFTER_RSCRIPT_BURST\n";
            await host.Submit("cat('AFTER_RSCRIPT_BURST\\n')").WaitAsync(TimeSpan.FromSeconds(15));
            page = await WaitForRenderedOutput(nextOutput);
            visible = Text(page, "text");
            Check(visible.EndsWith("SD_ERR_40000\n" + nextOutput, StringComparison.Ordinal),
                "another Rscript command retains the end of the preceding burst");

            await InvokeAsync(host.View, "ClearOutputAsync").WaitAsync(TimeSpan.FromSeconds(20));
            var snapshot = await host.Snapshot();
            Check(snapshot.GetProperty("transcript").GetArrayLength() == 0 && !snapshot.GetProperty("trimmed").GetBoolean(),
                "Clear output resets the Rscript transcript and omission notice");
            Set(host.Document, "Dirty", false);
            host.Document.Dispose();

            async Task<JsonElement> WaitForRenderedOutput(string marker)
            {
                var wait = Stopwatch.StartNew();
                while (wait.Elapsed < TimeSpan.FromSeconds(30))
                {
                    var state = JsonSerializer.Deserialize<JsonElement>(await host.JS($$"""
                        (() => {
                          const view = document.getElementById('transcript');
                          const text = [...view.querySelectorAll('.output-entry')].map(row => row.textContent).join('');
                          return {
                            ready: !document.getElementById('console-input').disabled && document.getElementById('prompt').textContent === '> '
                              && text.endsWith({{JsonSerializer.Serialize(marker)}}),
                            text, trimmed: view.querySelector('.notice')?.textContent.startsWith('Earlier output is omitted') === true,
                            controls: window.__fallbackBurstControl ?? [], chart: !!view.querySelector('figure.plot')
                          };
                        })()
                        """).WaitAsync(TimeSpan.FromSeconds(8)));
                    if (state.GetProperty("ready").GetBoolean() && host.State == "ready") return state;
                    await Task.Delay(50);
                }
                throw new TimeoutException("The Rscript console did not render the expected output and ready prompt.");
            }
        }
        finally { Environment.SetEnvironmentVariable(minimum, previousMinimum); }

        static string ExpectedOutput()
        {
            var output = new StringBuilder(pairs * 2 * lineLength);
            for (int i = 1; i <= pairs; i++)
            {
                string suffix = i.ToString("D5", CultureInfo.InvariantCulture);
                output.Append("SD_OUT_").Append(suffix).Append('\n');
                output.Append("SD_ERR_").Append(suffix).Append('\n');
            }
            return output.ToString();
        }
    }
}
