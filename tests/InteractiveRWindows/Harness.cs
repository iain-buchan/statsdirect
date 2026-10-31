using Microsoft.Web.WebView2.WinForms;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

internal static partial class Program
{
    const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static object? Get(object obj, string name) => obj.GetType().GetProperty(name, Members)?.GetValue(obj)
        ?? obj.GetType().GetField(name, Members)?.GetValue(obj);
    static void Set(object obj, string name, object? value) => obj.GetType().GetProperty(name, Members)!.SetValue(obj, value);
    static object? Call(object obj, string name, params object?[] args)
    {
        try { return obj.GetType().GetMethods(Members).Single(m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(obj, args); }
        catch (TargetInvocationException error) { throw error.InnerException ?? error; }
    }
    static async Task InvokeAsync(object obj, string name, params object?[] args) => await (Task)Call(obj, name, args)!;
    static async Task Wait(Func<bool> condition, string why, int milliseconds = 15000)
    {
        var watch = Stopwatch.StartNew();
        while (!condition())
        {
            if (watch.ElapsedMilliseconds > milliseconds) throw new TimeoutException(why);
            await Task.Delay(30);
        }
    }
    static string Text(JsonElement entry, string key) => entry.ValueKind == JsonValueKind.Object && entry.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
    static string Kind(JsonElement entry) => Text(entry, "type");
    static long Number(JsonElement entry, string key)
    {
        if (!entry.TryGetProperty(key, out var value)) return -1;
        return value.ValueKind == JsonValueKind.String ? long.Parse(value.GetString()!, System.Globalization.CultureInfo.InvariantCulture) : value.GetInt64();
    }

    sealed class ConsoleHost(Form document)
    {
        readonly object eventGate = new();
        readonly List<JsonElement> journal = new();
        object? subscribedSession;
        public Form Document { get; } = document;
        public object View => Get(Document, "InteractiveConsole") ?? throw new InvalidOperationException("R script window has no integrated interactive console.");
        public object Session => Get(View, "Session") ?? throw new InvalidOperationException("The R session has not started.");
        public WebView2 Browser => (WebView2)Get(View, "Browser")!;
        public string State => (string)Get(View, "NativeState")!;
        public JsonElement[] Events
        {
            get
            {
                AttachEventJournal();
                lock (eventGate) return journal.Select(e => e.Clone()).ToArray();
            }
        }
        void AttachEventJournal()
        {
            var session = Get(View, "Session");
            if (ReferenceEquals(subscribedSession, session)) return;
            lock (eventGate)
                journal.AddRange(((IEnumerable<JsonElement>)Get(View, "History")!).Select(e => e.Clone()));
            if (session == null) return;
            subscribedSession = session;
            session.GetType().GetEvent("Event")!.AddEventHandler(session, (Action<JsonElement>)(entry =>
            {
                lock (eventGate) journal.Add(entry.Clone());
            }));
        }
        public int Mark => Events.Length;
        public JsonElement[] Since(int mark) => Events.Skip(mark).ToArray();
        public string Output(int mark = 0) => string.Concat(Since(mark).Where(e => Kind(e) == "output").Select(e => Text(e, "text")));
        public Task Ready() => ((Task)Get(View, "Ready")!).WaitAsync(TimeSpan.FromSeconds(45));
        public async Task<string> JS(string source) => await Browser.CoreWebView2.ExecuteScriptAsync(source);
        public async Task<JsonElement> Snapshot() => JsonSerializer.Deserialize<JsonElement>(await JS("window.StatsDirectRConsole.snapshot()"));
        public async Task Start()
        {
            await Ready();
            await InvokeAsync(View, "StartAsync");
            AttachEventJournal();
            await Wait(() => State == "ready", "The integrated R session did not become ready.", 30000);
            await WaitBrowserReady();
        }
        public async Task WaitBrowserReady()
        {
            var watch = Stopwatch.StartNew();
            while (true)
            {
                var state = Text(await Snapshot(), "state");
                if (state is "ready" or "input" or "continuation") return;
                if (watch.ElapsedMilliseconds > 15000) throw new TimeoutException("Browser console did not accept input: " + state);
                await Task.Delay(30);
            }
        }
        public async Task<int> Submit(string source, string origin = "console")
        {
            await WaitBrowserReady();
            int mark = Mark;
            await JS($$"""
                (() => {
                  window.StatsDirectRConsole.setMode({{JsonSerializer.Serialize(origin == "script" ? "script" : "console")}});
                  const field = document.getElementById({{JsonSerializer.Serialize(origin == "script" ? "script-source" : "console-input")}});
                  field.value = {{JsonSerializer.Serialize(source)}};
                  field.dispatchEvent(new Event('input', {bubbles:true}));
                  document.getElementById('run').click();
                })()
                """);
            return mark;
        }
        public async Task<JsonElement[]> Run(string source, string marker = "", string origin = "console", string status = "ok")
        {
            int mark = await Submit(source, origin);
            await Finished(mark, status);
            if (marker.Length > 0) await Wait(() => Output(mark).Contains(marker, StringComparison.Ordinal), "R did not print " + marker);
            return Since(mark);
        }
        public async Task Finished(int mark, string status = "ok")
        {
            await Wait(() => Since(mark).Any(e => Kind(e) == "commandFinished") && State == "ready", "R command did not finish: " + Recent(), 25000);
            var finished = Since(mark).Last(e => Kind(e) == "commandFinished");
            if (Text(finished, "status") != status) throw new InvalidOperationException("Expected command status " + status + ": " + Recent());
            await WaitBrowserReady();
        }
        public async Task<JsonElement> Prompt(int mark, string text, string state = "input")
        {
            await Wait(() => State == state && Since(mark).Any(e => Kind(e) == "inputRequested" && Text(e, "prompt").Contains(text, StringComparison.Ordinal)), "R did not ask " + text + ": " + Recent());
            await WaitBrowserReady();
            return Since(mark).Last(e => Kind(e) == "inputRequested");
        }
        public async Task Stop(int mark)
        {
            await JS("document.getElementById('stop').click()");
            await Finished(mark, "interrupted");
        }
        public async Task Restart()
        {
            long old = Number(await Snapshot(), "generation");
            await JS("document.getElementById('restart').click()");
            await Wait(() => Events.Any(e => Kind(e) == "state" && Text(e, "state") == "ready" && Number(e, "generation") > old) && State == "ready", "Restart did not produce a new ready generation.", 30000);
            var watch = Stopwatch.StartNew();
            while (true)
            {
                var snapshot = await Snapshot();
                if (Number(snapshot, "generation") > old && Text(snapshot, "state") == "ready") return;
                if (watch.ElapsedMilliseconds > 15000) throw new TimeoutException("The browser did not enter the restarted R generation.");
                await Task.Delay(30);
            }
        }
        public string Recent() => string.Join("\n", Events.TakeLast(12).Select(e => JsonSerializer.Serialize(e.EnumerateObject().Where(p => p.Name != "preview").ToDictionary(p => p.Name, p => p.Value))));
        public async Task TwoFrames()
        {
            await JS("window.__rWindowsFrames=false;requestAnimationFrame(()=>requestAnimationFrame(()=>window.__rWindowsFrames=true))");
            var watch = Stopwatch.StartNew();
            while (await JS("window.__rWindowsFrames===true") != "true")
            {
                if (watch.ElapsedMilliseconds > 12000) throw new TimeoutException("The R console did not complete two animation frames.");
                await Task.Delay(20);
            }
        }
    }
}
