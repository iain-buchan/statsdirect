using System.Reflection;
using StatsDirect.UI;

internal static partial class Program
{
    static void DetachMainWindowLifecycle(frmMain main)
    {
        main.Load -= (EventHandler)typeof(frmMain).GetMethod("frmMain_Load", Internal)!.CreateDelegate(typeof(EventHandler), main);
        main.Shown -= (EventHandler)typeof(frmMain).GetMethod("frmMain_Shown", Internal)!.CreateDelegate(typeof(EventHandler), main);
        main.FormClosing -= (FormClosingEventHandler)typeof(frmMain).GetMethod("frmMain_FormClosing", Internal)!.CreateDelegate(typeof(FormClosingEventHandler), main);
        main.FormClosed -= (FormClosedEventHandler)typeof(frmMain).GetMethod("frmMain_FormClosed", Internal)!.CreateDelegate(typeof(FormClosedEventHandler), main);
    }

    static void CheckMainWindowPlacement(frmMain fixture, object app)
    {
        var settingsType = typeof(frmMain).Assembly.GetType("StatsDirect.UI.Properties.Settings", throwOnError: true)!;
        var defaultSettings = settingsType.GetProperty("Default", BindingFlags.Static | BindingFlags.Public)!;
        var originalSettings = defaultSettings.GetValue(null);
        var settings = Activator.CreateInstance(settingsType, nonPublic: true)!;
        var work = Screen.FromControl(fixture).WorkingArea;
        var saved = new Rectangle(work.Left + 20, work.Top + 20,
            Math.Max(work.Width - 40, fixture.MinimumSize.Width), Math.Max(work.Height - 40, fixture.MinimumSize.Height));
        void SetPreference(string name, object value) => settingsType.GetProperty(name, Internal)!.SetValue(settings, value);
        SetPreference("MainLeft", saved.Left);
        SetPreference("MainTop", saved.Top);
        SetPreference("MainWidth", saved.Width);
        SetPreference("MainHeight", saved.Height);

        try
        {
            defaultSettings.SetValue(null, settings);
            SetPreference("MainWindowState", FormWindowState.Maximized);
            using (var maximized = (frmMain)Call(app, "CreateMainWindow"))
                Check(!maximized.IsHandleCreated && !maximized.Visible && maximized.StartPosition == FormStartPosition.Manual
                    && maximized.WindowState == FormWindowState.Maximized && maximized.RestoreBounds == saved,
                    "main window restores the saved maximized state and its normal bounds before creating a handle");

            SetPreference("MainWindowState", FormWindowState.Normal);
            using var normal = (frmMain)Call(app, "CreateMainWindow");
            Check(!normal.IsHandleCreated && !normal.Visible && normal.StartPosition == FormStartPosition.Manual
                && normal.WindowState == FormWindowState.Normal && normal.Bounds == saved,
                "main window restores its saved normal size and position before creating a handle");

            // Leave the native Close path and override active. The fixture needs no IPC, preference writes or process exit.
            DetachMainWindowLifecycle(normal);
            normal.Location = fixture.Location;
            normal.ShowInTaskbar = false;
            normal.Show();
            var beforeClose = normal.Bounds;
            bool cancel = true, visibleDuringDecision = false, hiddenBeforeTeardown = false;
            normal.FormClosing += (_, e) => { visibleDuringDecision = normal.Visible; e.Cancel = cancel; };
            normal.FormClosed += (_, _) => hiddenBeforeTeardown = !normal.Visible && normal.Bounds == beforeClose;
            normal.Close();
            Check(!normal.IsDisposed && visibleDuringDecision && normal.Visible && normal.Bounds == beforeClose,
                "the main window remains visible at the same size when closing is canceled");

            cancel = false;
            Call(normal, "OnFormClosing", new FormClosingEventArgs(CloseReason.WindowsShutDown, false));
            Check(normal.Visible && !normal.IsDisposed, "a Windows logoff query leaves the main window visible");
            normal.Close();
            Check(visibleDuringDecision && hiddenBeforeTeardown && normal.IsDisposed,
                "an accepted ordinary close hides the main window at its full size before disposal");
        }
        finally
        {
            defaultSettings.SetValue(null, originalSettings);
            app.GetType().GetProperty("MainWindow", Internal)!.SetValue(app, fixture);
            fixture.Activate();
        }
    }
}
