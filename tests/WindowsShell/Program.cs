using System.Reflection;
using StatsDirect.UI;

internal static partial class Program
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
        await CheckWorkbookTabs(main, grid, workbook);
        await CheckDocumentToolbars(main, grid, workbook);
        await CheckScriptCharts(main, grid);
        await CheckTutorEncoding();
        Call(app, "ShowCalculator");
        var view = (Control)Get(app, "CalculatorView");
        var input = (RichTextBox)Get(view, "Expression");
        await Task.Yield();
        Check(input.Focused && main.ActiveMdiChild == grid, "opening Calculator in the actual MDI shell focuses its editor and retains the grid");
        Check(Locked(() => workbook.RangeSelection.Address) == selected, "opening Calculator preserves the actual SpreadsheetGear range");
        input.Text = "2"; input.Select(1, 0); input.ClearUndo(); input.SelectedText = " + 3";
        await Task.Delay(30);
        var mainToolbar = (ToolStrip)Field(main,"toolStrip");
        mainToolbar.Items["undoToolStripButton"].PerformClick();
        Check(input.Text=="2", "toolbar Undo respects calculator focus rather than changing the underlying worksheet");
        await Task.Delay(30);
        mainToolbar.Items["redoToolStripButton"].PerformClick();
        Check(input.Text=="2 + 3", "toolbar Redo respects calculator focus");
        var menu = (MenuStrip)Field(main, "mnuMain");
        // Help: Example workbook in its own section before About StatsDirect, which stays last
        var helpItems = menu.Items.OfType<ToolStripMenuItem>().Single(i => i.Text.Replace("&", "") == "Help").DropDownItems;
        int exampleAt = helpItems.IndexOfKey("exampleWorkbookMenuItem"), aboutAt = helpItems.IndexOfKey("aboutsStatsDirectToolStripMenuItem");
        Check(aboutAt == helpItems.Count - 1 && exampleAt < helpItems.IndexOfKey("checkForUpdatesToolStripMenuItem") && helpItems.IndexOfKey("checkForUpdatesToolStripMenuItem") < aboutAt && helpItems[aboutAt - 1] is ToolStripSeparator,
              "Help ends with Example workbook, Check for Updates and About StatsDirect last");
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

    // The ChatGPT component speaks JSON-RPC over its standard streams in UTF-8: what it writes must be read as UTF-8, else a minus
    // sign or a Greek letter in a reply arrives as three Latin characters.  A stand-in component, a PowerShell script answering the same
    // requests with raw UTF-8 bytes, stands where the real one would, through the real tutor class: the account's email and a streamed
    // reply must come back intact.
    static async Task CheckTutorEncoding()
    {
        var type = typeof(frmMain).Assembly.GetType("StatsDirect.UI.Learning.ChatGPTTutor")!;
        // A child's streams default to the console's code page, which is UTF-8 under this runner but a one-byte page when the program
        // starts from Explorer: make it one-byte here, so that only an explicit UTF-8 can pass.
        System.Text.Encoding consoleOut = Console.OutputEncoding, consoleIn = Console.InputEncoding;
        try { Console.OutputEncoding = System.Text.Encoding.Latin1; Console.InputEncoding = System.Text.Encoding.Latin1; } catch (IOException) { }
        string bin = Path.GetDirectoryName(typeof(frmMain).Assembly.Location)!, resources = Path.Combine(bin, "Tutor");
        Check(File.Exists(Path.Combine(resources, "config.toml")) && File.Exists(Path.Combine(resources, "script-instructions.txt")), "the tutor's configuration and instructions are beside the program");
        string root = Path.Combine(Path.GetTempPath(), "StatsDirect-shell-tutor-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        string mock = Path.Combine(root, "mock.ps1");
        File.WriteAllText(mock, string.Join("\n", new[] {
            "[Console]::InputEncoding=[Text.Encoding]::UTF8",
            "[Console]::OutputEncoding=[Text.Encoding]::UTF8",
            "$minus=[string][char]0x2212; $beta=[string][char]0x03B2",
            "while(($l=[Console]::In.ReadLine()) -ne $null){",
            "  $m=$l|ConvertFrom-Json",
            "  if($m.method -eq 'account/read'){ [Console]::Out.WriteLine('{\"id\":'+$m.id+',\"result\":{\"account\":{\"type\":\"chatgpt\",\"email\":\"'+$minus+$beta+'@example.org\",\"planType\":\"plus\"}}}') }",
            "  elseif($m.method -eq 'thread/start'){ [Console]::Out.WriteLine('{\"id\":'+$m.id+',\"result\":{\"thread\":{\"id\":\"t1\"},\"model\":\"mock\"}}') }",
            "  elseif($m.method -eq 'turn/start'){",
            "    [Console]::Out.WriteLine('{\"id\":'+$m.id+',\"result\":{\"turn\":{\"id\":\"u1\"}}}')",
            "    [Console]::Out.WriteLine('{\"method\":\"item/agentMessage/delta\",\"params\":{\"threadId\":\"t1\",\"turnId\":\"u1\",\"itemId\":\"i1\",\"delta\":\"Limits of agreement: '+$minus+'10.9 to 123.1 '+$beta+'\"}}')",
            "    [Console]::Out.WriteLine('{\"method\":\"turn/completed\",\"params\":{\"threadId\":\"t1\",\"turn\":{\"id\":\"u1\",\"status\":\"completed\"}}}')",
            "  }",
            "  elseif($m.id -ne $null){ [Console]::Out.WriteLine('{\"id\":'+$m.id+',\"result\":{}}') }",
            "}" }));
        string powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
        var tutor = (IDisposable)Activator.CreateInstance(type, Internal, null, new object[] { powershell, Path.Combine(root, "account"), resources, new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", mock } }, null)!;
        try
        {
            await (Task)type.GetMethod("RefreshAccountAsync", Internal)!.Invoke(tutor, null)!;
            Check((string)Get(tutor, "Email") == "\u2212\u03B2@example.org", "the account's email is read from the component as UTF-8");
            var messages = new System.Text.Json.Nodes.JsonArray(new System.Text.Json.Nodes.JsonObject { ["role"] = "user", ["text"] = "What are the limits of agreement?" });
            Func<string, System.Text.Json.Nodes.JsonObject, Task<System.Text.Json.Nodes.JsonNode>> noTools = (_, _) => Task.FromResult<System.Text.Json.Nodes.JsonNode>(new System.Text.Json.Nodes.JsonObject());
            var task = (Task)type.GetMethod("ConverseAsync", Internal)!.Invoke(tutor, new object[] { "{}", messages, new System.Text.Json.Nodes.JsonArray(), true, (Action<string>)(_ => { }), noTools, CancellationToken.None, false })!;
            await task;
            object reply = task.GetType().GetProperty("Result")!.GetValue(task)!;
            Check((string)Get(reply, "Text") == "Limits of agreement: \u221210.9 to 123.1 \u03B2", "a streamed reply with a minus sign and a Greek letter arrives intact");
        }
        finally
        {
            tutor.Dispose();
            try { Console.OutputEncoding = consoleOut; Console.InputEncoding = consoleIn; } catch (IOException) { }
            try { Directory.Delete(root, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
    static async Task CheckDocumentToolbars(frmMain main, Form grid, SpreadsheetGear.Windows.Forms.WorkbookView workbook)
    {
        var toolbar = (ToolStrip)Field(main, "toolStrip");
        Check(toolbar.Visible && string.Join("|", toolbar.Items.OfType<ToolStripButton>().Select(b => b.Text)) == "Save|Undo|Redo|Cut|Copy|Paste|Find|Help",
            "worksheet has one native toolbar in the shared document order");
        CaptureToolbar(toolbar, "grid-toolbar.png");
        Check(((Control)Field(grid,"formulaBar")).Visible && toolbar.Items.ContainsKey("cboActiveReport"), "formula entry and Results to selection remain available");
        workbook.Focus();
        Call(grid,"EditFillDown");
        await Task.Delay(30);
        Check(toolbar.Items["undoToolStripButton"].Enabled, "worksheet Undo is enabled after a recorded cell edit");
        toolbar.Items["undoToolStripButton"].PerformClick();
        await Task.Delay(30);
        workbook.GetLock();
        try { Check(workbook.ActiveWorksheet.Cells["A2"].Value == null, "toolbar Undo restores the worksheet cell"); }
        finally { workbook.ReleaseLock(); }
        Check(toolbar.Items["redoToolStripButton"].Enabled, "worksheet Redo reflects its real history");
        toolbar.Items["redoToolStripButton"].PerformClick();
        workbook.GetLock();
        try { Check(workbook.ActiveWorksheet.Cells["A2"].Value?.ToString() == "123", "toolbar Redo replays the worksheet edit"); }
        finally { workbook.ReleaseLock(); }
        using var script = (Form)Call(main,"CreateScriptWindow");
        Call(script,"ShowR","x <- 1","Toolbar check",null,true);
        await Task.Delay(180);
        var scriptToolbar = (ToolStrip)Field(script,"ToolStrip1");
        Check(!toolbar.Visible && scriptToolbar.Visible, "switching to a script leaves exactly its own toolbar visible");
        Check(!((Control)Field(script,"pnlActions")).Visible && scriptToolbar.Items.ContainsKey("runToolStripButton"), "script has one Run control and no lower command row");
        var editor = (RichTextBox)Field(script,"rtbDoc");
        editor.Text="original"; editor.ClearUndo(); editor.Select(editor.TextLength,0); editor.SelectedText=" edited";
        await Task.Delay(30);
        scriptToolbar.Items["undoToolStripButton"].PerformClick();
        Check(editor.Text=="original", "script toolbar Undo uses native editor history");
        await Task.Delay(30);
        scriptToolbar.Items["redoToolStripButton"].PerformClick();
        Check(editor.Text=="original edited", "script toolbar Redo restores the edit");
        string saved = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "StatsDirect-toolbar-" + Guid.NewGuid().ToString("N") + ".R");
        try
        {
            script.GetType().GetField("currentFile", Internal)!.SetValue(script,saved);
            scriptToolbar.Items["saveToolStripButton"].PerformClick();
            Check(System.IO.File.ReadAllText(saved)=="original edited", "one-click script Save writes the current editor contents");
        }
        finally { if(System.IO.File.Exists(saved)) System.IO.File.Delete(saved); }
        CaptureToolbar(scriptToolbar, "script-toolbar.png");
        scriptToolbar.Items["findToolStripButton"].PerformClick();
        Check((bool)Get(script,"FindBarShown"), "script toolbar Find opens the existing find bar");
        var oldWidth = main.Width;
        var oldMinimum = main.MinimumSize;
        main.MinimumSize = Size.Empty;
        main.Width = main.LogicalToDeviceUnits(290);
        await Task.Delay(60);
        Check(scriptToolbar.OverflowButton.Visible && scriptToolbar.OverflowButton.AccessibleName=="More commands" && scriptToolbar.Items.Cast<ToolStripItem>().Any(i=>i.Placement==ToolStripItemPlacement.Overflow), "narrow script uses accessible native More overflow");
        editor.Select(0,8);
        scriptToolbar.OverflowButton.ShowDropDown();
        await Task.Delay(30);
        var language = (ComboBox)Field(script,"cboLanguage");
        Check(language.Visible && language.AccessibleName=="Script language" && editor.SelectionLength==8, "More exposes the existing language selector without losing the script selection");
        scriptToolbar.OverflowButton.HideDropDown();
        var output = (Control)Field(script,"outputPane");
        await (Task)Get(output,"Ready");
        Call(output,"Clear"); Call(output,"AppendText","Results to copy");
        await (Task)Call(output,"SelectAllAsync"); output.Focus();
        await Task.Delay(200);
        scriptToolbar.OverflowButton.ShowDropDown();
        await Task.Delay(30);
        Check(ReferenceEquals(Field(script,"editTarget"),output) && scriptToolbar.Items["copyToolStripButton"].Enabled && await (Task<string>)Call(output,"SelectedTextAsync")=="Results to copy", "Copy from output retains its selection and target when More takes focus");
        scriptToolbar.OverflowButton.HideDropDown();
        main.Width = oldWidth; main.MinimumSize = oldMinimum;
        script.Dispose(); grid.Activate();
        await Task.Delay(180);
        Check(toolbar.Visible, "returning to data restores its toolbar");
    }

    static void CaptureToolbar(ToolStrip toolbar, string file)
    {
        string output = Environment.GetEnvironmentVariable("STATSDIRECT_TOOLBAR_ARTIFACTS");
        if(string.IsNullOrEmpty(output)) return;
        System.IO.Directory.CreateDirectory(output);
        using Bitmap bitmap = new(toolbar.Width,toolbar.Height);
        toolbar.DrawToBitmap(bitmap,new Rectangle(Point.Empty,toolbar.Size));
        bitmap.Save(System.IO.Path.Combine(output,file));
    }
}
