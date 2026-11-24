using StatsDirect.UI;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

internal static partial class Program
{
    static async Task CheckHeavyOutput(frmMain main)
    {
        const string minimum = "STATSDIRECT_R_CONSOLE_MINIMUM";
        string? previousMinimum = Environment.GetEnvironmentVariable(minimum);
        try
        {
            // Run the reported Rscript case first, then exercise the persistent worker.
            Environment.SetEnvironmentVariable(minimum, "99.0");
            await CheckHeavyOutput(main, "Rscript fallback", persistent: false);
            Environment.SetEnvironmentVariable(minimum, null);
            await CheckHeavyOutput(main, "persistent worker", persistent: true);
        }
        finally
        {
            Environment.SetEnvironmentVariable(minimum, previousMinimum);
        }
    }

    static async Task CheckHeavyOutput(frmMain main, string mode, bool persistent)
    {
        const int lines = 20_000, transcriptLimit = 200_000;
        const string source = "for (i in 1:20000) cat('PROBE_HEAVY', i, '\\n')";
        const string finalLine = "PROBE_HEAVY 20000 \n";
        string expected = string.Concat(Enumerable.Range(1, lines)
            .Select(i => "PROBE_HEAVY " + i.ToString(CultureInfo.InvariantCulture) + " \n"));
        string retained = expected[^transcriptLimit..];
        var host = await NewConsole(main);
        await host.Start().WaitAsync(TimeSpan.FromSeconds(45));
        RememberWorker(host);
        Check((bool)Get(host.Session, "PersistentWorkspace")! == persistent, mode + " is active for the 20,000-line test");
        await BrowserProbe("""
            window.__heavyOutputMessages = 0;
            window.__heavyOutputEvents = 0;
            window.chrome.webview.addEventListener('message', ({data}) => {
              ++window.__heavyOutputMessages;
              for (const item of Array.isArray(data) ? data : [data]) {
                if (item?.type === 'output') ++window.__heavyOutputEvents;
              }
            });
            """);

        var elapsed = Stopwatch.StartNew();
        int mark = await host.Submit(source, "console").WaitAsync(TimeSpan.FromSeconds(12));
        JsonElement page;
        do
        {
            page = await BrowserProbe($$"""
                (() => {
                  const view = document.getElementById('transcript');
                  const stdout = [...view.querySelectorAll('.output-entry.stdout')].at(-1);
                  return {
                    ready: !document.getElementById('console-input').disabled && document.getElementById('prompt').textContent === '> ',
                    renderedLastLine: !!stdout?.textContent.endsWith({{JsonSerializer.Serialize(finalLine)}}),
                    messages: window.__heavyOutputMessages,
                    events: window.__heavyOutputEvents
                  };
                })()
                """);
            if (page.GetProperty("ready").GetBoolean() && page.GetProperty("renderedLastLine").GetBoolean() && host.State == "ready") break;
            if (elapsed.Elapsed > TimeSpan.FromSeconds(45))
                throw new TimeoutException(mode + " did not render the final R output and ready prompt. Native state: " + host.State);
            await Task.Delay(100);
        } while (true);
        int messages = page.GetProperty("messages").GetInt32(), outputEvents = page.GetProperty("events").GetInt32();
        Console.WriteLine($"INFO {mode}: rendered {lines:N0} lines of R output in {elapsed.Elapsed.TotalSeconds:F2} s "
            + $"({outputEvents:N0} output events, {messages:N0} web messages)");
        var events = host.Since(mark);
        string emitted = string.Concat(events.Where(e => Kind(e) == "output" && Text(e, "stream") == "stdout").Select(e => Text(e, "text")));
        Check(emitted == expected && events.Any(e => Kind(e) == "commandFinished" && Text(e, "status") == "ok"),
            mode + " executes all 20,000 numbered lines in order and finishes normally");

        page = await BrowserProbe("""
            (() => {
              const view = document.getElementById('transcript');
              return {
                output: [...view.querySelectorAll('.output-entry.stdout')].map(row => row.textContent).join(''),
                trimmed: view.querySelector('.notice')?.textContent.startsWith('Earlier output is omitted') === true,
                consoleVisible: !document.getElementById('console-panel').hidden
              };
            })()
            """);
        Check(Text(page, "output") == retained && page.GetProperty("trimmed").GetBoolean() && page.GetProperty("consoleVisible").GetBoolean(),
            mode + " shows the exact retained 200,000-character tail, including the final line and the trimming notice");
        Check(messages > 0 && messages <= outputEvents / 2,
            mode + " averages at least two output events per web message during the burst");
        await host.Run("cat('AFTER_HEAVY_READY\\n')", "AFTER_HEAVY_READY").WaitAsync(TimeSpan.FromSeconds(30));
        await host.TwoFrames().WaitAsync(TimeSpan.FromSeconds(12));
        page = await BrowserProbe("""
            document.getElementById('transcript').textContent.includes('AFTER_HEAVY_READY\n') &&
            !document.getElementById('console-input').disabled && document.getElementById('prompt').textContent === '> '
            """);
        Check(page.ValueKind == JsonValueKind.True, mode + " still accepts another prompt command and displays its output");
        Set(host.Document, "Dirty", false);
        host.Document.Dispose();

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
