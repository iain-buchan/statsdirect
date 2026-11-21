using System.Net;
using System.Reflection;
using StatsDirect.UI;

internal static partial class Program
{
    // Close and update.  A report cancels the main window's first asking to put its question, and the main window closes on a second
    // asking once the questions are answered.  The downloaded installer has to be there for that second asking (StatsDirect starts it
    // when the main window has closed), and it has to be removed when a close is refused.  The fixture's main window must never really
    // close, so a handler added after the main window's own closing handler refuses every asking and records what it saw.
    static async Task CheckCloseForUpdate(frmMain main, object app)
    {
        var closing = (FormClosingEventHandler)typeof(frmMain).GetMethod("frmMain_FormClosing", Internal)!.CreateDelegate(typeof(FormClosingEventHandler), main);
        var asked = new List<(bool arranged, bool installerThere)>();
        string path = null;
        FormClosingEventHandler refuse = (_, e) => { asked.Add(((bool)Get(app, "ClosingForUpgrade"), path != null && File.Exists(path))); e.Cancel = true; };
        main.FormClosing += closing;
        main.FormClosing += refuse;
        Form report = null;
        try
        {
            // No report or R window: the close is refused at once, and the update is released with its installer.
            object installer = await FakeInstaller();
            path = (string)Get(installer, "FilePath");
            Call(app, "CloseForUpgrade", installer);
            Check(asked.Count == 1 && asked[0] == (true, true) && !(bool)Get(app, "ClosingForUpgrade") && !File.Exists(path) && !main.IsDisposed,
                "a close for an update that is refused at once releases the update and removes its installer");

            // A report open: the first asking waits for the report, and the update and its installer are kept for the second asking.
            asked.Clear();
            report = (Form)Call(main, "CreateReport");
            await Task.Delay(300);
            installer = await FakeInstaller();
            path = (string)Get(installer, "FilePath");
            Call(app, "CloseForUpgrade", installer);
            bool keptWhileWaiting = (bool)Get(app, "ClosingForUpgrade") && File.Exists(path) && (bool)Get(main, "CloseIsPending");
            for (int i = 0; i < 300 && (asked.Count < 2 || (bool)Get(app, "ClosingForUpgrade")); i++)
                await Task.Delay(50);
            Check(keptWhileWaiting, "while a report puts its question, the update stays arranged and its installer is kept");
            Check(asked.Count == 2 && asked[0] == (true, true) && asked[1] == (true, true),
                "the main window's second asking, after the report's question, still has the update and its installer: " + string.Join(", ", asked));
            Check(!(bool)Get(app, "ClosingForUpgrade") && !File.Exists(path) && !main.IsDisposed && !(bool)Get(main, "CloseIsPending"),
                "when that second asking is refused, the update is released and its installer removed");
        }
        finally
        {
            main.FormClosing -= refuse;
            main.FormClosing -= closing;
            if (report != null && !report.IsDisposed)
            {
                report.Close();
                for (int i = 0; i < 100 && !report.IsDisposed; i++)
                    await Task.Delay(50);
            }
        }
    }

    // A downloaded installer as the updater holds one, made from a few bytes of a pretend setup file: it is never started here.
    static async Task<object> FakeInstaller()
    {
        var type = typeof(frmMain).Assembly.GetType("StatsDirect.UI.UpdateInstaller")!;
        var download = type.GetMethod("DownloadAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
        using var client = new HttpClient(new PretendSetup());
        var task = (Task)download.Invoke(null, new object[] { client, null, CancellationToken.None, (Action<string>)(_ => { }) })!;
        await task;
        return task.GetType().GetProperty("Result")!.GetValue(task)!;
    }

    sealed class PretendSetup : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[] { (byte)'M', (byte)'Z', 0, 0 }), RequestMessage = request });
    }
}
