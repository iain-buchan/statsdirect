using System.Reflection;
using StatsDirect.UI;

internal static class Program
{
    const BindingFlags Internal = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
    static int checks;
    static object Get(object obj, string name) => obj.GetType().GetProperty(name, Internal)!.GetValue(obj)!;
    static object Field(object obj, string name) => obj.GetType().GetField(name, Internal)!.GetValue(obj)!;
    static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethods(Internal).Single(m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(obj, args)!;
    static void Check(bool value, string label) { if (!value) throw new Exception(label); checks++; Console.WriteLine("PASS " + label); }
    [STAThread]
    static int Main()
    {
        ApplicationConfiguration.Initialize();
        var assembly = typeof(frmMain).Assembly;
        var license = assembly.GetCustomAttributes().Single(a => a.GetType().Name == "SpreadsheetGearLicenseAttribute");
        Assembly.Load("SpreadsheetGear").GetType("SpreadsheetGear.Factory")!.GetMethod("SetSignedLicense")!.Invoke(null, new[] { Get(license, "LicenseString") });
        var appType = assembly.GetType("StatsDirect.UI.SdApplication")!;
        var app = appType.GetProperty("SoleInstance", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        using var main = new frmMain();
        appType.GetProperty("MainWindow", Internal)!.SetValue(app, main);
        // Use the actual main form and commands, but exclude normal startup,
        // user settings writes, IPC and startup/update dialogs from this fixture.
        main.Load -= (EventHandler)typeof(frmMain).GetMethod("frmMain_Load", Internal)!.CreateDelegate(typeof(EventHandler), main);
        main.Shown -= (EventHandler)typeof(frmMain).GetMethod("frmMain_Shown", Internal)!.CreateDelegate(typeof(EventHandler), main);
        main.FormClosing -= (FormClosingEventHandler)typeof(frmMain).GetMethod("frmMain_FormClosing", Internal)!.CreateDelegate(typeof(FormClosingEventHandler), main);
        main.FormClosed -= (FormClosedEventHandler)typeof(frmMain).GetMethod("frmMain_FormClosed", Internal)!.CreateDelegate(typeof(FormClosedEventHandler), main);
        assembly.GetType("StatsDirect.UI.Program")!.GetMethod("ConfigureStartupCommand", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, new object[] { new[] { "-calculator" }, main });
        main.StartPosition = FormStartPosition.Manual; main.Location = new Point(-16000, -16000); main.Size = new Size(1800, 1200); main.ShowInTaskbar = false;
        int exit = 1;
        main.Shown += async (_, _) =>
        {
            try { await Run(main, app); Console.WriteLine($"PASS {checks} production Windows shell checks"); exit = 0; }
            catch (Exception ex) { Console.WriteLine("FAIL " + ex); }
            finally { foreach (var child in main.MdiChildren) child.Dispose(); ((IDisposable)Field(app, "calculator")).Dispose(); main.Close(); }
        };
        Application.Run(main);
        return exit;
    }
    static async Task Run(frmMain main, object app)
    {
        await Task.Delay(100); // run the real startup-command callback after Shown
        Check(Get(app, "CalculatorView") is Control startupView && startupView.ContainsFocus && main.ActiveMdiChild == null && !(bool)Get(main, "ShowOpeningDialog"),
            "legacy -calculator startup opens the integrated pane without a document");
        var grid = (Form)Call(main, "CreateGrid");
        await Task.Delay(150); // let the production post-tab activation timer finish
        var workbook = (SpreadsheetGear.Windows.Forms.WorkbookView)Field(grid, "workbookView");
        T Locked<T>(Func<T> read)
        {
            workbook.GetLock();
            try { return read(); }
            finally { workbook.ReleaseLock(); }
        }
        string selected = Locked(() =>
        {
            workbook.ActiveWorksheet.Cells["A1"].Value = 123;
            workbook.RangeSelection = workbook.ActiveWorksheet.Cells["A1:B2"];
            return workbook.RangeSelection.Address;
        });
        Call(app, "ShowCalculator");
        var view = (Control)Get(app, "CalculatorView");
        var input = (RichTextBox)Get(view, "Expression");
        await Task.Yield();
        Check(input.Focused && main.ActiveMdiChild == grid, "opening Calculator in the actual MDI shell focuses its editor and retains the grid");
        Check(Locked(() => workbook.RangeSelection.Address) == selected, "opening Calculator preserves the actual SpreadsheetGear range");
        input.Text = "2"; input.Select(1, 0); input.ClearUndo(); input.SelectedText = " + 3";
        var menu = (MenuStrip)Field(main, "mnuMain");
        var edit = menu.Items.OfType<ToolStripMenuItem>().Single(i => i.Text.Replace("&", "") == "Edit");
        ToolStripMenuItem Command(string label) => edit.DropDownItems.OfType<ToolStripMenuItem>().Single(i => i.Text.Replace("&", "") == label);
        Command("Undo").PerformClick();
        Check(input.Text == "2" && Locked(() => workbook.ActiveWorksheet.Cells["A1"].Value.ToString()) == "123", "actual worksheet Edit > Undo edits the calculator instead of the worksheet");
        Command("Redo").PerformClick();
        Check(input.Text == "2 + 3", "actual worksheet Edit > Redo redoes calculator input");
        // Exercise the same menu-mode boundary that temporarily removes focus.
        typeof(MenuStrip).GetMethod("OnMenuActivate", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(menu, new object[] { EventArgs.Empty });
        menu.Focus();
        Command("Undo").PerformClick();
        Check(input.Text == "2", "menu activation retains the calculator as the edit target");
        typeof(MenuStrip).GetMethod("OnMenuDeactivate", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(menu, new object[] { EventArgs.Empty });
        await Task.Yield();
        Check(input.Focused, "the Edit command returns focus to the expression");
        input.Select(1, 0); input.SelectedText = " * 4";
        var session = Get(Field(app, "calculator"), "Session");
        await (Task)Call(session, "ToggleModeAsync");
        var floating = (Form)Get(session, "Window"); floating.Location = new Point(-16000, -16000);
        Check(input.Focused, "explicit pop-out focuses the same editor in the actual app");
        await (Task)Call(session, "ToggleModeAsync");
        Command("Undo").PerformClick();
        Check(input.Text == "2" && Locked(() => workbook.RangeSelection.Address) == selected, "redocked menu undo retains history and worksheet selection");
        // Never modify the real clipboard: copy dispatch is tested in the pane
        // harness with an injected callback and with synthetic Office payloads.
        grid.Dispose();
        input.Focus(); input.Select(input.TextLength, 0); input.SelectedText = " + 8";
        var calculatorEdit = (ToolStripDropDownButton)Get(view, "EditMenu");
        calculatorEdit.DropDownItems.OfType<ToolStripMenuItem>().Single(i => i.Text == "Undo").PerformClick();
        Check(input.Text == "2" && main.ActiveMdiChild == null, "calculator Edit menu still works after the last MDI document closes");
    }
}
