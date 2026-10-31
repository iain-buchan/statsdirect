using System.Diagnostics;
using System.Text.Json;

namespace StatsDirect.InteractiveR;

internal sealed partial class MainForm
{
    private bool smokeStarted;

    // R execution uses the actual WebView message bridge and native R session.
    // Host presentation events also exercise clicking while the page updates.
    private async Task RunUiSmokeAsync()
    {
        var checks = new List<object>();
        string? failure = null;
        string? screenshot = null;
        JsonElement? browserMetrics = null;
        object? recipeRegression = null;
        var elapsed = Stopwatch.StartNew();
        try
        {
            async Task Wait(Func<bool> condition, string description, int timeoutMs = 12000)
            {
                var wait = Stopwatch.StartNew();
                while (!condition())
                {
                    if (wait.ElapsedMilliseconds > timeoutMs) throw new TimeoutException(description);
                    await Task.Delay(40);
                }
            }
            async Task TwoFrames()
            {
                await browser.CoreWebView2.ExecuteScriptAsync("window.__statsDirectSmokeFrames=false;requestAnimationFrame(()=>requestAnimationFrame(()=>{window.__statsDirectSmokeFrames=true}))");
                var wait = Stopwatch.StartNew();
                while (await browser.CoreWebView2.ExecuteScriptAsync("window.__statsDirectSmokeFrames===true") != "true")
                {
                    if (wait.ElapsedMilliseconds > 12000) throw new TimeoutException("The console did not finish two animation frames");
                    await Task.Delay(20);
                }
            }
            bool HasOutput(string marker) => string.Concat(history.Where(item => item.TryGetProperty("type", out var type) && type.GetString() == "output")
                .Select(item => item.TryGetProperty("text", out var text) ? text.GetString() ?? "" : "")).Contains(marker, StringComparison.Ordinal);
            bool HasPrompt(string prompt) => history.Any(item => item.TryGetProperty("type", out var type) && type.GetString() == "inputRequested" &&
                item.TryGetProperty("prompt", out var text) && (text.GetString() ?? "").Contains(prompt, StringComparison.Ordinal));
            async Task Command(string source, string marker, string origin = "console")
            {
                await RunThroughUi(source, origin);
                await Wait(() => nativeState == "ready" && HasOutput(marker), "R command did not produce " + marker);
            }
            async Task RunThroughUi(string source, string origin = "console")
            {
                var wait = Stopwatch.StartNew();
                while (true)
                {
                    string? browserState = JsonSerializer.Deserialize<string>(await browser.CoreWebView2.ExecuteScriptAsync("window.StatsDirectRConsole.snapshot().state"));
                    if (browserState is "ready" or "input" or "continuation") break;
                    if (wait.ElapsedMilliseconds > 12000) throw new TimeoutException("The web console did not become ready for input: " + browserState);
                    await Task.Delay(40);
                }
                await browser.CoreWebView2.ExecuteScriptAsync($$"""
                    (() => {
                      window.StatsDirectRConsole.setMode({{JsonSerializer.Serialize(origin == "script" ? "script" : "console")}});
                      const field = document.getElementById({{JsonSerializer.Serialize(origin == "script" ? "script-source" : "console-input")}});
                      field.value = {{JsonSerializer.Serialize(source)}};
                      field.dispatchEvent(new Event('input', {bubbles:true}));
                      document.getElementById('run').click();
                    })()
                    """);
            }
            long RequestId()
            {
                var request = history.Last(item => item.TryGetProperty("type", out var type) && type.GetString() == "inputRequested").GetProperty("requestId");
                return request.ValueKind == JsonValueKind.String ? long.Parse(request.GetString()!, System.Globalization.CultureInfo.InvariantCulture) : request.GetInt64();
            }
            async Task Check(string name, bool passed)
            {
                checks.Add(new { name, passed });
                if (!passed) throw new InvalidOperationException(name);
                await Task.Delay(40); // Let the final web event reach the page before reading its DOM.
            }
            await Wait(() => nativeState == "ready", "R did not become ready");
            string snapshot = await browser.CoreWebView2.ExecuteScriptAsync("JSON.stringify(window.StatsDirectRConsole.snapshot())");
            await Check("shared console bridge is loaded", snapshot.Length > 5 && snapshot != "null");
            await Command("saved <- 42; cat('UI_SESSION=', saved, '\\n', sep='')", "UI_SESSION=42");
            await Check("console submission reaches native R and appears in the page", JsonSerializer.Deserialize<string>(await browser.CoreWebView2.ExecuteScriptAsync("document.body.innerText"))!.Contains("UI_SESSION=42"));

            await RunThroughUi("answer <- readline('UI_ASK> '); cat('UI_ANSWER=', answer, '\\n', sep='')");
            await Wait(() => nativeState == "input" && HasPrompt("UI_ASK>"), "readline did not request input");
            long oldRequest = RequestId();
            await RunThroughUi("genuine answer");
            await Wait(() => nativeState == "ready" && HasOutput("UI_ANSWER=genuine answer"), "readline did not receive its answer");
            await Check("genuine readline prompt receives its separate answer", true);
            await Check("prompt answers are excluded from the source script and command history", await browser.CoreWebView2.ExecuteScriptAsync("(()=>{const s=window.StatsDirectRConsole.snapshot(); return !s.script.includes('genuine answer') && !s.history.includes('genuine answer')})()") == "true");

            await RunThroughUi("retryAnswer <- readline('UI_RETRY> '); cat('UI_RETRY_ANSWER=',retryAnswer,'\\n',sep='')");
            await Wait(() => nativeState == "input" && HasPrompt("UI_RETRY>"), "retry test did not request input");
            int limit = history.Last(item => item.TryGetProperty("type", out var type) && type.GetString() == "inputRequested").GetProperty("maxBytes").GetInt32();
            await RunThroughUi(new string('x', limit + 1));
            await RunThroughUi("retry accepted");
            await Wait(() => nativeState == "ready" && HasOutput("UI_RETRY_ANSWER=retry accepted"), "a rejected answer disabled its prompt");
            await Check("an oversized answer is rejected and the same prompt accepts a valid retry", true);

            await RunThroughUi("beforeStop <- 'retained'; cat('UI_LOOP_STARTED\\n'); repeat {}");
            await Wait(() => nativeState == "busy" && HasOutput("UI_LOOP_STARTED"), "loop did not start");
            await browser.CoreWebView2.ExecuteScriptAsync("document.getElementById('stop').click()");
            await Wait(() => nativeState == "ready", "loop did not interrupt");
            await Command("cat('UI_PRESERVED=', saved == 42 && beforeStop == 'retained', '\\n', sep='')", "UI_PRESERVED=TRUE");
            await Check("Stop returns to prompt and preserves pre-existing variables and completed assignments", true);

            await RunThroughUi("cancelAnswer <- readline('UI_CANCEL> '); cancelledFollowOn <- TRUE");
            await Wait(() => nativeState == "input" && HasPrompt("UI_CANCEL>"), "cancel test did not request input");
            await browser.CoreWebView2.ExecuteScriptAsync("document.getElementById('stop').click()");
            await Wait(() => nativeState == "ready", "readline did not interrupt");
            await Command("cat('UI_CANCELLED=', !exists('cancelAnswer') && !exists('cancelledFollowOn') && saved == 42, '\\n', sep='')", "UI_CANCELLED=TRUE");
            await Check("Stop cancels readline without supplying an answer or running follow-on code", true);

            await RunThroughUi("if (TRUE) {");
            await Wait(() => nativeState == "continuation", "incomplete expression did not request continuation");
            await RunThroughUi("multi <- saved + 1\n}");
            await Wait(() => nativeState == "ready", "multiline expression did not finish");
            await Command("cat('UI_MULTI=', multi, '\\n', sep='')", "UI_MULTI=43");
            await Check("multiline continuation uses inputReply and preserves the workspace", true);

            await Command("scriptValue <- saved + multi; cat('UI_SCRIPT=',scriptValue,'\\n',sep='')", "UI_SCRIPT=85", "script");
            await Check("Script submission and Console share the same R variables", true);

            await RunThroughUi("invalid\0source");
            await Command("cat('UI_VALID_AFTER_REJECTION\\n')", "UI_VALID_AFTER_REJECTION");
            await Check("invalid source is rejected without leaving the console disabled", true);

            await Command("cat('<img id=ui-injected onerror=alert(1)>', '\\n'); cat('UI_SAFE_OUTPUT\\n')", "UI_SAFE_OUTPUT");
            await Check("R text is rendered literally, without HTML execution", await browser.CoreWebView2.ExecuteScriptAsync("document.getElementById('ui-injected')===null") == "true");
            await Check("command history recalls a command and restores the current draft", await browser.CoreWebView2.ExecuteScriptAsync("(()=>{window.StatsDirectRConsole.setMode('console');const i=document.getElementById('console-input');i.value='draft';i.setSelectionRange(5,5);i.dispatchEvent(new Event('input',{bubbles:true}));i.dispatchEvent(new KeyboardEvent('keydown',{key:'ArrowUp',bubbles:true,cancelable:true}));const previous=i.value===window.StatsDirectRConsole.snapshot().history.at(-1);i.dispatchEvent(new KeyboardEvent('keydown',{key:'ArrowDown',bubbles:true,cancelable:true}));return previous&&i.value==='draft'})()") == "true");

            await Command("plot(1:3); cat('UI_PLOT_FIRST\\n')", "UI_PLOT_FIRST");
            var firstImageWait = Stopwatch.StartNew();
            while (await browser.CoreWebView2.ExecuteScriptAsync("document.images.length>=1 && document.images[0].complete && document.images[0].naturalWidth>0") != "true")
            {
                if (firstImageWait.ElapsedMilliseconds > 12000) throw new TimeoutException("The first SVG preview did not load");
                await Task.Delay(40);
            }
            string firstPreview = JsonSerializer.Deserialize<string>(await browser.CoreWebView2.ExecuteScriptAsync("document.images[0]?.getAttribute('src')")) ?? "";
            await Command("abline(h=2); cat('UI_PLOT_SECOND\\n')", "UI_PLOT_SECOND");
            var imageWait = Stopwatch.StartNew();
            while (await browser.CoreWebView2.ExecuteScriptAsync("document.images.length>=2 && [...document.images].every(i=>i.complete&&i.naturalWidth>0)") != "true")
            {
                if (imageWait.ElapsedMilliseconds > 12000) throw new TimeoutException("SVG plot previews did not load");
                await Task.Delay(40);
            }
            await Check("SVG plot revisions display without overwriting the earlier plot", !string.IsNullOrEmpty(firstPreview) && await browser.CoreWebView2.ExecuteScriptAsync("document.images[0].getAttribute('src')===" + JsonSerializer.Serialize(firstPreview)) == "true");
            var firstPlot = history.First(item => item.TryGetProperty("type", out var type) && type.GetString() == "plot");
            var lastPlot = history.Last(item => item.TryGetProperty("type", out var type) && type.GetString() == "plot");
            var resolved = FindPlot(firstPlot.GetProperty("plotId").GetString()!, firstPlot.GetProperty("revision").GetInt64());
            await Check("earlier plot actions resolve the original revision's SVG asset", resolved.GetProperty("path").GetString() == firstPlot.GetProperty("path").GetString() && resolved.GetProperty("path").GetString() != lastPlot.GetProperty("path").GetString());

            // Use the actual Continue-in-R recipe as one Script submission. Its
            // plot/abline/abline/mtext calls must not become four partial charts.
            int recipeStart = history.Count;
            int imagesBeforeRecipe = JsonSerializer.Deserialize<int>(await browser.CoreWebView2.ExecuteScriptAsync("document.images.length"));
            string recipe = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "TPaired.R"));
            await RunThroughUi(recipe, "script");
            await Wait(() => nativeState == "ready" && history.Skip(recipeStart).Any(item => item.TryGetProperty("type", out var type) && type.GetString() == "commandFinished"), "the paired-t recipe did not finish");
            var recipeEvents = history.Skip(recipeStart).ToArray();
            bool EventType(JsonElement item, string name) => item.TryGetProperty("type", out var type) && type.GetString() == name;
            int recipePlotCount = recipeEvents.Count(item => EventType(item, "plot"));
            string recipeOutput = string.Concat(recipeEvents.Where(item => EventType(item, "output")).Select(item => item.GetProperty("text").GetString()));
            int firstRecipePlot = Array.FindIndex(recipeEvents, item => EventType(item, "plot"));
            int lastRecipeOutput = Array.FindLastIndex(recipeEvents, item => EventType(item, "output"));
            bool numericOutputBeforePlot = recipeOutput.Contains("Mean of differences =", StringComparison.Ordinal) && recipeOutput.Contains("95% Limits of agreement =", StringComparison.Ordinal) && firstRecipePlot > lastRecipeOutput;
            recipeRegression = new { fixture = "tests/RScript/examples/TPaired.R", completedPlotEvents = recipePlotCount, numericOutputBeforePlot, firstRecipePlot, lastRecipeOutput, output = recipeOutput, eventTypes = recipeEvents.Select(item => item.GetProperty("type").GetString()).ToArray() };
            await Check("the full paired-t R recipe emits one completed chart after its numerical output", recipePlotCount == 1 && numericOutputBeforePlot);
            var recipeImageWait = Stopwatch.StartNew();
            int imagesAfterRecipe;
            while (true)
            {
                imagesAfterRecipe = JsonSerializer.Deserialize<int>(await browser.CoreWebView2.ExecuteScriptAsync("document.images.length"));
                if (imagesAfterRecipe >= imagesBeforeRecipe + 1 && await browser.CoreWebView2.ExecuteScriptAsync("[...document.images].every(i=>i.complete&&i.naturalWidth>0)") == "true") break;
                if (recipeImageWait.ElapsedMilliseconds > 12000) throw new TimeoutException("The paired-t recipe's SVG preview did not load");
                await Task.Delay(40);
            }
            recipeRegression = new { fixture = "tests/RScript/examples/TPaired.R", completedPlotEvents = recipePlotCount, imagesAdded = imagesAfterRecipe - imagesBeforeRecipe, numericOutputBeforePlot };
            await Check("the paired-t Script run adds exactly one real WebView chart image", imagesAfterRecipe == imagesBeforeRecipe + 1);

            // Retain the real controls: .click() on a newly looked-up node would
            // miss a replacement between physical mousedown and mouseup.
            try
            {
                await browser.CoreWebView2.ExecuteScriptAsync("""
                    (() => {
                      const figure = document.querySelector('.plot');
                      const buttons = [...figure.querySelectorAll('.plot-actions button')];
                      const open = buttons.find(button => button.textContent.startsWith('Open'));
                      const copy = buttons.find(button => button.textContent === 'Copy chart');
                      if (!open || !copy) throw new Error('The actual chart controls were not found');
                      const bridge = window.chrome.webview, original = bridge.postMessage;
                      const regression = window.__statsDirectPlotClickRegression = {open, copy, original, actions:[]};
                      const intercept = message => {
                        if (message.action === 'openPlot' || message.action === 'copyPlot') regression.actions.push(message);
                        else original.call(bridge, message);
                      };
                      bridge.postMessage = intercept;
                      if (bridge.postMessage !== intercept) throw new Error('The chart-action test interception could not be installed');
                      open.focus(); window.getSelection().removeAllRanges();
                      document.dispatchEvent(new Event('selectionchange'));
                    })()
                    """);
                await TwoFrames();
                await Check("focusing a chart action preserves the same Open and Copy DOM controls", await browser.CoreWebView2.ExecuteScriptAsync("(()=>{const r=window.__statsDirectPlotClickRegression,b=[...document.querySelector('.plot').querySelectorAll('.plot-actions button')];return r.open.isConnected&&r.copy.isConnected&&b.includes(r.open)&&b.includes(r.copy)})()") == "true");
                await browser.CoreWebView2.ExecuteScriptAsync("""
                    (() => {
                      const r = window.__statsDirectPlotClickRegression;
                      r.open.click();
                      r.copy.dispatchEvent(new PointerEvent('pointerdown', {bubbles:true,pointerId:17,pointerType:'mouse',isPrimary:true,button:0,buttons:1}));
                      r.copy.dispatchEvent(new MouseEvent('mousedown', {bubbles:true,button:0,buttons:1}));
                      r.copy.focus(); window.getSelection().removeAllRanges();
                      document.dispatchEvent(new Event('selectionchange'));
                    })()
                    """);
                session.PublishState();
                session.PublishNotice("Chart-control regression: output arrived while the pointer was down.");
                await TwoFrames();
                await Check("state and transcript updates do not replace a chart button while its pointer is down", await browser.CoreWebView2.ExecuteScriptAsync("(()=>{const r=window.__statsDirectPlotClickRegression;return r.copy.isConnected&&[...document.querySelector('.plot').querySelectorAll('.plot-actions button')].includes(r.copy)})()") == "true");
                await browser.CoreWebView2.ExecuteScriptAsync("""
                    (() => {
                      const r = window.__statsDirectPlotClickRegression;
                      r.copy.dispatchEvent(new PointerEvent('pointerup', {bubbles:true,pointerId:17,pointerType:'mouse',isPrimary:true,button:0,buttons:0}));
                      r.copy.dispatchEvent(new MouseEvent('mouseup', {bubbles:true,button:0,buttons:0}));
                      r.copy.click();
                    })()
                    """);
                await TwoFrames();
                using var actions = JsonDocument.Parse(await browser.CoreWebView2.ExecuteScriptAsync("window.__statsDirectPlotClickRegression.actions"));
                string expectedPlot = firstPlot.GetProperty("plotId").GetString()!;
                long expectedRevision = firstPlot.GetProperty("revision").GetInt64();
                bool exactActions = actions.RootElement.GetArrayLength() == 2 && actions.RootElement[0].GetProperty("action").GetString() == "openPlot" && actions.RootElement[1].GetProperty("action").GetString() == "copyPlot" &&
                    actions.RootElement.EnumerateArray().All(action => action.GetProperty("plotId").GetString() == expectedPlot && action.GetProperty("revision").GetInt64() == expectedRevision);
                await Check("the original Open and Copy controls still dispatch their original plot revision after pointer release", exactActions);
            }
            finally
            {
                await browser.CoreWebView2.ExecuteScriptAsync("(()=>{const r=window.__statsDirectPlotClickRegression;if(r?.original)window.chrome.webview.postMessage=r.original;delete window.__statsDirectPlotClickRegression;delete window.__statsDirectSmokeFrames})()");
            }
            screenshot = Path.ChangeExtension(Path.GetFullPath(options.UiSmokeResult!), ".png");
            browserMetrics = JsonSerializer.Deserialize<JsonElement>(await browser.CoreWebView2.ExecuteScriptAsync("({width:innerWidth,height:innerHeight,devicePixelRatio,codeFont:getComputedStyle(document.getElementById('console-input')).fontSize})"));
            await browser.CoreWebView2.ExecuteScriptAsync("(()=>{const pane=document.getElementById('transcript');pane.scrollTop=pane.scrollHeight})()");
            await Task.Delay(100);
            await using (var image = File.Create(screenshot)) await browser.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png, image);
            string preservedSource = await BrowserScriptAsync();
            long previousGeneration = generation; string? previousSession = sessionId;
            await browser.CoreWebView2.ExecuteScriptAsync("document.getElementById('restart').click()");
            await Wait(() => generation > previousGeneration && nativeState == "ready", "restart did not create a ready generation");
            await Check("Restart preserves the accumulated R source script", await BrowserScriptAsync() == preservedSource);
            await browser.CoreWebView2.ExecuteScriptAsync("window.chrome.webview.postMessage(" + JsonSerializer.Serialize(new { channel = "statsdirect-r-console", action = "inputReply", sessionId = previousSession, generation = previousGeneration, requestId = oldRequest, text = "stale_answer <- TRUE" }) + ")");
            await Command("cat('UI_RESTART=', !exists('saved') && !exists('stale_answer'), '\\n', sep='')", "UI_RESTART=TRUE");
            await Check("Restart clears variables and rejects replies from the previous generation", true);
        }
        catch (Exception error) { failure = error.ToString(); }
        finally
        {
            string resultPath = Path.GetFullPath(options.UiSmokeResult!);
            Directory.CreateDirectory(Path.GetDirectoryName(resultPath)!);
            await File.WriteAllTextAsync(resultPath, JsonSerializer.Serialize(new { passed = failure is null, elapsedSeconds = elapsed.Elapsed.TotalSeconds, checks, failure, recipeRegression, screenshot, browserMetrics, hostDpi = DeviceDpi, browserZoom = browser.ZoomFactor, workingDirectory = options.WorkingDirectory }, new JsonSerializerOptions { WriteIndented = true }));
            Environment.ExitCode = failure is null ? 0 : 1;
            Close();
        }
    }
}
