using System.Text.Json;
using Microsoft.Web.WebView2.WinForms;

internal static partial class Program
{
    static object RConsole(Form script) => Get(script, "InteractiveConsole");
    static WebView2 RBrowser(Form script) => (WebView2)Get(RConsole(script), "Browser");
    static JsonElement[] REvents(Form script) => ((IEnumerable<JsonElement>)Get(RConsole(script), "History")).ToArray();
    static JsonElement[] RPlots(Form script) => REvents(script).Where(e => e.GetProperty("type").GetString() == "plot").ToArray();
    static int RFinished(Form script) => REvents(script).Count(e => e.GetProperty("type").GetString() == "commandFinished");

    static async Task PrepareRConsole(Form script, string folder)
    {
        Directory.CreateDirectory(folder);
        Call(script, "ShowR", "", "Interactive chart checks", null, false);
        var view = RConsole(script);
        view.GetType().GetProperty("WorkingDirectory", Internal)!.SetValue(view, folder);
        await ((Task)Get(view, "Ready")).WaitAsync(TimeSpan.FromSeconds(45));
    }

    static async Task CompleteRCommand(Form script, int before, bool expectedError = false)
    {
        var started = DateTime.UtcNow;
        var until = DateTime.UtcNow.AddSeconds(120);
        while ((RFinished(script) <= before || (string)Get(RConsole(script), "NativeState") != "ready") && DateTime.UtcNow < until)
        {
            string state = (string)Get(RConsole(script), "NativeState");
            if (state is "stopped" or "input" or "continuation")
            {
                await DiagnoseRCommand(script, before, "Unexpected R state: " + state);
                Check(false, "the chart script unexpectedly stopped or requested console input");
            }
            var session = Get(RConsole(script), "Session");
            if (session != null && DateTime.UtcNow - started > TimeSpan.FromSeconds(5))
            {
                var process = session.GetType().GetField("process", Internal)?.GetValue(session) as System.Diagnostics.Process;
                bool failedStartup;
                try { failedStartup = process == null || process.HasExited; }
                catch (InvalidOperationException) { failedStartup = true; }
                if (failedStartup)
                {
                    await DiagnoseRCommand(script, before, "The R worker did not start or has exited.");
                    Check(false, "the R chart worker starts and remains connected until command completion");
                }
            }
            await Task.Delay(30);
        }
        var finished = REvents(script).LastOrDefault(e => e.GetProperty("type").GetString() == "commandFinished");
        string status = finished.ValueKind == JsonValueKind.Object ? finished.GetProperty("status").GetString() : null;
        if (RFinished(script) <= before || (string)Get(RConsole(script), "NativeState") != "ready" || status != (expectedError ? "error" : "ok"))
            await DiagnoseRCommand(script, before, "Command completion timeout or unexpected status: " + status);
        Check(RFinished(script) > before && (string)Get(RConsole(script), "NativeState") == "ready" && status == (expectedError ? "error" : "ok"),
            expectedError ? "the persistent R session reports the script error and returns to its prompt" : "the real R script completes and returns to its persistent prompt");
        var paintUntil = DateTime.UtcNow.AddSeconds(5);
        int expected = RPlots(script).Length;
        while (DateTime.UtcNow < paintUntil)
        {
            string count = await RBrowser(script).CoreWebView2.ExecuteScriptAsync("document.querySelectorAll('figure.plot').length");
            if (count == expected.ToString(System.Globalization.CultureInfo.InvariantCulture)) return;
            await Task.Delay(30);
        }
        Check(false, "the integrated R chart cards finish rendering");
    }

    static async Task DiagnoseRCommand(Form script, int before, string reason)
    {
        var view = RConsole(script);
        var session = Get(view, "Session");
        object ReadField(object value, string name) => value?.GetType().GetField(name, Internal)?.GetValue(value);
        var process = ReadField(session, "process") as System.Diagnostics.Process;
        object processState;
        try { processState = process == null ? null : new { process.Id, process.HasExited }; }
        catch (InvalidOperationException) { processState = "disposed"; }
        string snapshot;
        try { snapshot = await RBrowser(script).CoreWebView2.ExecuteScriptAsync("window.StatsDirectRConsole?.snapshot()").WaitAsync(TimeSpan.FromSeconds(5)); }
        catch (Exception error) { snapshot = "Could not read browser: " + error.Message; }
        var captured = new
        {
            reason, before, finished = RFinished(script), nativeState = Get(view, "NativeState"),
            script = Get(script, "LearningScript"), hiddenEditor = ((RichTextBox)Field(script, "rtbDoc")).Text,
            transcript = Get(view, "Transcript"),
            session = new { state = ReadField(session, "state"), prompt = ReadField(session, "currentPrompt"), request = ReadField(session, "requestId"), active = ReadField(session, "active"), processState },
            events = REvents(script), snapshot
        };
        string directory = Environment.GetEnvironmentVariable("STATSDIRECT_SHELL_ARTIFACTS") ?? Path.GetTempPath();
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "R-command-diagnostic-" + Guid.NewGuid().ToString("N") + ".json");
        string json = JsonSerializer.Serialize(captured, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json);
        Console.WriteLine("R DIAGNOSTIC " + path + "\n" + json[..Math.Min(json.Length, 18000)]);
    }

    static async Task DeleteRFixtureDirectory(string folder, string prefix)
    {
        string target = Path.GetFullPath(folder);
        string relative = Path.GetRelativePath(Path.GetFullPath(Path.GetTempPath()), target);
        if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || !Path.GetFileName(target).StartsWith(prefix, StringComparison.Ordinal))
            throw new InvalidOperationException("The R fixture cleanup target is outside its owned temporary directory.");
        for (int attempt = 0; Directory.Exists(target); attempt++)
            try { Directory.Delete(target, true); }
            catch (IOException) when (attempt < 50) { await Task.Delay(30); }
    }
}
