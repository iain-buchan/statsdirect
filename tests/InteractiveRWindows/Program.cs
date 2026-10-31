using StatsDirect.UI;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

internal static partial class Program
{
    static int checks, unhandled;
    static readonly List<object> evidence = new();
    static readonly List<ConsoleHost> hosts = new();
    static readonly HashSet<int> workerIds = new();
    static readonly string artifacts = Path.GetFullPath(Environment.GetEnvironmentVariable("STATSDIRECT_INTERACTIVE_R_ARTIFACTS") ?? Path.Combine(Path.GetTempPath(), "StatsDirect-integrated-R-" + Guid.NewGuid().ToString("N")));
    static void Check(bool passed, string description)
    {
        evidence.Add(new { description, passed });
        if (!passed) throw new InvalidOperationException(description);
        checks++;
        Console.WriteLine("PASS " + description);
    }
    [STAThread]
    static int Main(string[] args)
    {
        Directory.CreateDirectory(artifacts);
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => { unhandled++; Console.WriteLine("UNHANDLED " + e.Exception); };
        var assembly = typeof(frmMain).Assembly;
        var license = assembly.GetCustomAttributes().Single(a => a.GetType().Name == "SpreadsheetGearLicenseAttribute");
        Assembly.Load("SpreadsheetGear").GetType("SpreadsheetGear.Factory")!.GetMethod("SetSignedLicense")!.Invoke(null, new[] { Get(license, "LicenseString") });
        var appType = assembly.GetType("StatsDirect.UI.SdApplication")!;
        var app = appType.GetProperty("SoleInstance", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        using var main = new frmMain();
        appType.GetProperty("MainWindow", Members)!.SetValue(app, main);
        // Exercise the real document host without startup, settings writes, IPC or update dialogs.
        foreach (var pair in new[] { ("Load", "frmMain_Load"), ("Shown", "frmMain_Shown"), ("FormClosing", "frmMain_FormClosing"), ("FormClosed", "frmMain_FormClosed") })
        {
            var evt = typeof(frmMain).GetEvent(pair.Item1)!;
            evt.RemoveEventHandler(main, typeof(frmMain).GetMethod(pair.Item2, Members)!.CreateDelegate(evt.EventHandlerType!, main));
        }
        main.Location = new Point(-16000, -16000); main.StartPosition = FormStartPosition.Manual;
        main.Size = new Size(2200, 1500); main.ShowInTaskbar = false;
        if (args.Contains("--manual", StringComparer.Ordinal)) return RunManual(main);
        int result = 1; string? failure = null;
        main.Shown += async (_, _) =>
        {
            try
            {
                await Run(main);
                Check(unhandled == 0, "no exception escapes to the Windows UI thread");
                Console.WriteLine($"PASS {checks} integrated Windows interactive R checks");
                result = 0;
            }
            catch (Exception error) { failure = error.ToString(); Console.WriteLine("FAIL " + failure); }
            finally
            {
                foreach (var host in hosts) await SaveEvidence(host);
                foreach (var child in main.MdiChildren) { child.GetType().GetProperty("Dirty", Members)!.SetValue(child, false); child.Dispose(); }
                main.Close();
            }
        };
        Application.Run(main);
        foreach (int worker in workerIds)
        {
            bool alive;
            try { using var process = Process.GetProcessById(worker); alive = !process.WaitForExit(5000); }
            catch (ArgumentException) { alive = false; }
            evidence.Add(new { description = "window disposal terminates owned native R worker " + worker, passed = !alive });
            if (alive) { result = 1; Console.WriteLine("FAIL owned R worker remains alive: " + worker); }
        }
        File.WriteAllText(Path.Combine(artifacts, "results.json"), JsonSerializer.Serialize(new { passed = result == 0 && unhandled == 0, checks, unhandled, failure, evidence, workerIds }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("Evidence: " + artifacts);
        return unhandled > 0 ? 1 : result;
    }
    static int RunManual(frmMain main)
    {
        main.Text = "StatsDirect — R console acceptance";
        main.StartPosition = FormStartPosition.CenterScreen;
        main.Size = new Size(2200, 1500); main.ShowInTaskbar = true;
        int result = 0;
        main.Shown += async (_, _) =>
        {
            try
            {
                var host = await NewConsole(main);
                // This visible fixture deliberately closes without the document's
                // unsaved-source prompt or the real main window's preference writes.
                host.Document.FormClosing -= (FormClosingEventHandler)host.Document.GetType()
                    .GetMethod("frmScript_FormClosing", Members)!.CreateDelegate(typeof(FormClosingEventHandler), host.Document);
                host.Document.FormClosing += (_, _) => Set(host.Document, "Dirty", false);
                main.Text = "StatsDirect — R console acceptance";
                Console.WriteLine("Manual acceptance ready. Close this window when finished; released StatsDirect and preferences are untouched.");
                Console.WriteLine("Owned R output directory: " + Get(host.View, "WorkingDirectory"));
            }
            catch (Exception error) { result = 1; Console.WriteLine("FAIL manual acceptance setup: " + error); main.Close(); }
        };
        try { Application.Run(main); }
        finally { foreach (var child in main.MdiChildren) child.Dispose(); }
        return unhandled > 0 ? 1 : result;
    }
    static async Task<ConsoleHost> NewConsole(frmMain main)
    {
        var file = (ToolStripMenuItem)Get(main, "fileToolStripMenuItem")!;
        var before = main.MdiChildren.ToHashSet();
        file.DropDownItems["newScriptToolStripMenuItem"]!.PerformClick();
        await Wait(() => main.MdiChildren.Any(c => !before.Contains(c)), "File > New R session did not create a document.");
        var created = main.MdiChildren.Where(c => !before.Contains(c)).ToArray();
        Check(created.Length == 1 && created[0] is frmScript && main.ActiveMdiChild == created[0], "File > New R session opens the actual R script MDI window");
        var host = new ConsoleHost(created.Single()); hosts.Add(host);
        await host.Ready();
        Set(host.View, "WorkingDirectory", Path.Combine(artifacts, "working-" + hosts.Count));
        return host;
    }
    static void RememberWorker(ConsoleHost host)
    {
        if (Get(host.Session, "process") is Process worker) workerIds.Add(worker.Id);
    }
    static async Task SaveEvidence(ConsoleHost host)
    {
        try
        {
            int index = hosts.IndexOf(host) + 1;
            File.WriteAllText(Path.Combine(artifacts, $"console-{index}-events.json"), JsonSerializer.Serialize(host.Events.Select(e => e.EnumerateObject().Where(p => p.Name != "preview").ToDictionary(p => p.Name, p => p.Value)), new JsonSerializerOptions { WriteIndented = true }));
            if (!host.Document.IsDisposed && host.Browser.CoreWebView2 != null)
            {
                await using var stream = File.Create(Path.Combine(artifacts, $"console-{index}.png"));
                await host.Browser.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png, stream);
            }
        }
        catch (Exception error) { Console.WriteLine("Evidence note: " + error.Message); }
    }
    static async Task Run(frmMain main)
    {
        var host = await NewConsole(main);
        Check((string)Get(host.Document, "LearningScript")! == "" && !(bool)Get(host.Document, "LearningRunning")! && (string)Get(host.Document, "LearningOutput")! == "",
            "a new R session has blank source and runs no user code automatically");
        Check(Get(host.View, "Session") == null, "opening a blank R session does not launch R before first use");
        await host.Start(); RememberWorker(host);
        await CheckCommandsAndPrompts(host);
        await CheckStopping(host);
        await CheckScriptAndFiles(main, host);
        await CheckPlots(main, host);
        await CheckDisplayRecovery(host);
        await CheckDomAndWheel(host);
        await CheckClearOutput(host);
        await CheckRestartAndIsolation(main, host);
        await CheckClosingAndSaving(main);
    }
}
