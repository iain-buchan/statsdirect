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
        Form report = null, rWindow = null;
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
            report.Close();
            for (int i = 0; i < 100 && !report.IsDisposed; i++)
                await Task.Delay(50);

            // An R window opened just before Close and update, whose console is still starting: being clean, it answers at once, so the
            // main window's first asking ends with its second asking queued.  The update and its installer wait for that second asking.
            asked.Clear();
            installer = await FakeInstaller();
            path = (string)Get(installer, "FilePath");
            rWindow = (Form)Call(main, "CreateScriptWindow");
            Call(rWindow, "ShowR", string.Empty, "R session", null, false);
            Call(app, "CloseForUpgrade", installer);
            bool keptForRetry = (bool)Get(app, "ClosingForUpgrade") && File.Exists(path) && (bool)Get(main, "CloseIsPending");
            for (int i = 0; i < 300 && (asked.Count < 2 || (bool)Get(app, "ClosingForUpgrade")); i++)
                await Task.Delay(50);
            Check(keptForRetry, "an R window that answers at once keeps the update and its installer for the main window's queued second asking");
            Check(asked.Count == 2 && asked[0] == (true, true) && asked[1] == (true, true),
                "the queued second asking, after an R window that answered at once, still has the update and its installer: " + string.Join(", ", asked));
            Check(!(bool)Get(app, "ClosingForUpgrade") && !File.Exists(path) && !main.IsDisposed && !(bool)Get(main, "CloseIsPending"),
                "when that second asking is refused, the update is released and its installer removed");
        }
        finally
        {
            main.FormClosing -= refuse;
            main.FormClosing -= closing;
            foreach (var window in new[] { report, rWindow })
                if (window != null && !window.IsDisposed)
                {
                    window.Close();
                    for (int i = 0; i < 100 && !window.IsDisposed; i++)
                        await Task.Delay(50);
                }
        }
    }

    // A close of the main window that a later window refuses leaves an earlier, clean workbook where it was: visible, in the main
    // window, with its tab and its record.  (Before 5.3.16 the workbook took itself away when asked, and the refusal could not bring
    // it back.)
    static async Task CheckRefusedCloseKeepsWindows(frmMain main, object app)
    {
        FormClosingEventHandler refuse = (_, e) => e.Cancel = true;   // the fixture's main window never really closes
        main.FormClosing += refuse;
        var workbook = (Form)Call(main, "CreateGrid");
        bool workbookAsked = false;
        FormClosingEventHandler noteAsked = (_, e) => workbookAsked |= e.CloseReason == CloseReason.MdiFormClosing;
        workbook.FormClosing += noteAsked;
        var refusing = new Form { MdiParent = main, Text = "Refuses to close" };
        FormClosingEventHandler refuseChild = (_, e) => { if (e.CloseReason == CloseReason.MdiFormClosing) e.Cancel = true; };
        refusing.FormClosing += refuseChild;
        refusing.Show();
        try
        {
            await Task.Delay(200);
            main.Close();
            await Task.Delay(500);
            var tabs = (TabControl)Field(main, "tabWindows");
            object information = workbook.Tag;
            bool inPlace = !workbook.IsDisposed && workbook.Visible && workbook.MdiParent == main && information != null
                && Get(information, "TabPage") is TabPage tab && tabs.TabPages.Contains(tab)
                && ((System.Collections.IEnumerable)Get(app, "Windows")).Cast<object>().Contains(information);
            Check(workbookAsked && inPlace,
                $"a close of the main window refused by a later window leaves an earlier clean workbook visible, in place, with its tab (asked: {workbookAsked}, in place: {inPlace})");
        }
        finally
        {
            main.FormClosing -= refuse;
            workbook.FormClosing -= noteAsked;
            refusing.FormClosing -= refuseChild;
            refusing.Close();
            workbook.Close();
            for (int i = 0; i < 100 && !(workbook.IsDisposed && refusing.IsDisposed); i++)
                await Task.Delay(50);
        }
    }

    // A downloaded installer as the updater holds one, made from a few bytes of a pretend setup file: it is never started here.
    static async Task<object> FakeInstaller()
    {
        var type = typeof(frmMain).Assembly.GetType("StatsDirect.UI.UpdateInstaller")!;
        var download = type.GetMethod("DownloadAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
        // The update that the pretend setup file answers: an offer of its own size and SHA-256.
        object offer = typeof(frmMain).Assembly.GetType("StatsDirect.UI.UpdateOffer")!.GetMethod("Create", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, new object[] { new Version(999, 0, 0), (long)PretendSetup.Bytes.Length, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(PretendSetup.Bytes)), null })!;
        using var client = new HttpClient(new PretendSetup());
        var task = (Task)download.Invoke(null, new object[] { client, offer, null, CancellationToken.None, (Action<string>)(_ => { }), null })!;
        await task;
        return task.GetType().GetProperty("Result")!.GetValue(task)!;
    }

    sealed class PretendSetup : HttpMessageHandler
    {
        internal static readonly byte[] Bytes = { (byte)'M', (byte)'Z', 0, 0 };

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Bytes), RequestMessage = request });
    }
}
