using System.Reflection;
using StatsDirect.Calculator;
using StatsDirect.UI.HtmlHelp;

internal static partial class Program
{
    static int checks;
    static string output;
    static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; Console.WriteLine("PASS " + message); }
    [STAThread]
    static int Main(string[] args)
    {
        output = Path.GetFullPath(args.FirstOrDefault() ?? Path.Combine(AppContext.BaseDirectory, "artifacts"));
        Directory.CreateDirectory(output);
        if (args.Contains("--96dpi"))
        {
            Application.SetHighDpiMode(HighDpiMode.DpiUnaware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
        }
        else ApplicationConfiguration.Initialize();
        using var host = new Form { IsMdiContainer = true, Width = 1600, Height = 1100, StartPosition = FormStartPosition.Manual, Location = new Point(-16000, -16000), ShowInTaskbar = false };
        int exit = 1;
        host.Shown += async (_, _) =>
        {
            try { await Run(host); Console.WriteLine($"PASS {checks} calculator checks"); exit = 0; }
            catch (Exception ex) { Console.WriteLine("FAIL " + ex); }
            finally { host.Close(); }
        };
        Application.Run(host);
        return exit;
    }
    static async Task Run(Form host)
    {
        float scale = host.DeviceDpi / 96f;
        host.Size = new Size((int)(1400 * scale), (int)(900 * scale));
        var ribbon = new Panel { Dock = DockStyle.Top, Height = 60 };
        host.Controls.Add(ribbon);
        using var sheet = new Form { MdiParent = host, WindowState = FormWindowState.Maximized };
        var selection = new TextBox { Text = "selected worksheet range", Dock = DockStyle.Top };
        sheet.Controls.Add(selection);
        sheet.Show();
        selection.Select(2, 7);
        var mdi = host.Controls.OfType<MdiClient>().Single();
        Size full = mdi.Size;
        using var help = new HelpService();
        using var calc = new CalculatorService();
        Check(calc.Session == null && calc.View == null && !host.Controls.OfType<TabControl>().Any(), "calculator is absent until explicitly opened; no permanent tab");
        string app = @"C:\Program Files\StatsDirect\StatsDirect.exe";
        foreach (string command in new[] { @"%STATSDIRECT%\StatsDirect.exe -calculator", '"' + app + '"' + " -calculator", app + " -calculator", "StatsDirect.exe -calculator" })
            Check(CalculatorService.IsBuiltInCommand(command, app), "recognises saved Tools command: " + command);
        Check(!CalculatorService.IsBuiltInCommand("calc.exe", app) && !CalculatorService.IsBuiltInCommand(@"C:\Other\StatsDirect.exe -calculator", app) && !CalculatorService.IsBuiltInCommand(app + " -calculator other", app), "custom tools and other installations retain their external launch path");
        calc.Show(host, () => help.Show(host, "1020", Path.Combine(AppContext.BaseDirectory, "Help")));
        var view = calc.View;
        var session = calc.Session;
        Check(session.IsDocked && session.DockHost.Panel.Dock == DockStyle.Bottom && mdi.Height < full.Height && mdi.Width == full.Width, "Calculator docks below the workspace without taking worksheet width");
        Check(host.ActiveMdiChild == sheet && selection.SelectionStart == 2 && selection.SelectionLength == 7 && view.Expression.Focused, "opening Calculator focuses input and preserves active worksheet and range");
        Check(ribbon.Width == host.ClientSize.Width && mdi.Bottom <= session.DockHost.Divider.Top, "calculator and divider leave the ribbon and worksheet unobscured");
        calc.Show(host, () => { });
        Check(ReferenceEquals(view, calc.View) && ReferenceEquals(session, calc.Session), "repeated Tools command reuses one calculator session");
        Check(view.Expression.Multiline && view.Expression.WordWrap && view.Expression.ScrollBars == RichTextBoxScrollBars.Vertical, "input wraps with vertical scrolling");
        double dockedLines = view.Expression.ClientSize.Height / (double)view.Expression.Font.Height;
        Check(dockedLines >= 2.5 && dockedLines <= 4.5, $"default docked input shows about three lines at {view.DeviceDpi} DPI ({dockedLines:F2})");
        var viewport = view.Controls.OfType<Panel>().Single();
        Check(!viewport.HorizontalScroll.Visible && !viewport.VerticalScroll.Visible, "default dock shows every control without scrolling the whole pane");
        using (var bitmap = new Bitmap(view.Width, view.Height)) { view.DrawToBitmap(bitmap, view.ClientRectangle); bitmap.Save(Path.Combine(output, "calculator-docked.png")); }
        Check(view.RectangleToScreen(view.ClientRectangle).Contains(view.Result.RectangleToScreen(view.Result.ClientRectangle)), "result remains visible in the initial dock at the current DPI");
        string longExpression = string.Join(" + ", Enumerable.Repeat("1", 180));
        view.Expression.Text = longExpression;
        Check(view.Expression.GetLineFromCharIndex(longExpression.Length - 1) > 0 && view.Expression.Text == longExpression, "long expressions visually wrap without inserting characters");
        Check(view.EvaluateCurrent() && view.Result.Text == "180", "wrapped expression uses the existing calculator engine");
        view.Expression.Text = "(2 + 3)\r\n* 4";
        Check(view.EvaluateCurrent() && view.Result.Text == "20", "pasted line breaks are parsed as whitespace");
        view.SaveCalculation();
        Check(view.Saved.Items.Count == 1, "successful expression and result can be saved");
        view.Expression.Text = "2 + 8";
        var enter = new KeyEventArgs(Keys.Enter);
        typeof(Control).GetMethod("OnKeyDown", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(view.Expression, new object[] { enter });
        Check(enter.SuppressKeyPress && view.Result.Text == "10" && view.Expression.Text == "2 + 8", "Enter calculates without inserting a newline");
        var shiftEnter = new KeyEventArgs(Keys.Shift | Keys.Enter);
        typeof(Control).GetMethod("OnKeyDown", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(view.Expression, new object[] { shiftEnter });
        Check(!shiftEnter.SuppressKeyPress, "Shift+Enter is left to the multiline editor");
        view.Expression.Text = "1 +";
        view.SaveCalculation();
        Check(view.LastError != null && view.Result.Text == "" && view.Saved.Items.Count == 1, "invalid expressions show an inline error and cannot save stale results");
        view.Saved.SelectedIndex = 0;
        view.Recall();
        Check(view.Expression.Text == "(2 + 3)\n* 4" && view.Result.Text == "20", "recall restores original multiline expression and result (native LF line endings)");
        view.Expression.Select(1, 5);
        await session.ToggleModeAsync();
        Check(!session.IsDocked && session.Window != null && ReferenceEquals(view.Parent, session.Window) && mdi.Size == full, "Pop out moves the same live calculator and restores the workspace");
        session.Window.Location = new Point(-16000, -16000);
        double floatingLines = view.Expression.ClientSize.Height / (double)view.Expression.Font.Height;
        Check(floatingLines >= 2.5 && floatingLines <= 4.5, $"default floating input shows about three lines at {view.DeviceDpi} DPI ({floatingLines:F2})");
        int compactWindowHeight = session.Window.Height, compactInputHeight = view.Expression.Height;
        session.Window.Height += (int)(100 * scale);
        Check(view.Expression.Height > compactInputHeight, "enlarging the floating window gives the input more visible lines");
        session.Window.Height = compactWindowHeight;
        Check(!viewport.HorizontalScroll.Visible && !viewport.VerticalScroll.Visible, "compact floating layout does not acquire unnecessary scrollbars after resizing");
        Check(view.RectangleToScreen(view.ClientRectangle).Contains(view.Result.RectangleToScreen(view.Result.ClientRectangle)), "result remains visible in the compact floating window");
        using (var bitmap = new Bitmap(view.Width, view.Height)) { view.DrawToBitmap(bitmap, view.ClientRectangle); bitmap.Save(Path.Combine(output, "calculator-floating.png")); }
        Check(view.Expression.SelectionStart == 1 && view.Expression.SelectionLength == 5 && view.Saved.Items.Count == 1 && view.Result.Text == "20", "pop-out preserves caret selection, result and saved calculations");
        session.Window.Width = (int)(550 * scale);
        Check(!viewport.HorizontalScroll.Visible, "narrow floating calculator keeps every control reachable with vertical scrolling only");
        view.Expression.Text = longExpression;
        Check(view.Expression.GetLineFromCharIndex(longExpression.Length - 1) > 0 && view.Expression.Text == longExpression, "narrow floating input continues to wrap without changing the expression");
        using (var bitmap = new Bitmap(view.Width, view.Height)) { view.DrawToBitmap(bitmap, view.ClientRectangle); bitmap.Save(Path.Combine(output, "calculator-narrow.png")); }
        await session.ToggleModeAsync();
        Check(session.IsDocked && view.Saved.Items.Count == 1 && view.Expression.Text == longExpression, "Dock preserves all calculator state");
        int height = session.DockHost.Panel.Height;
        int inputHeight = view.Expression.Height;
        session.DockHost.Divider.SplitPosition = height + 50;
        Check(session.DockHost.Panel.Height == height + 50 && mdi.Bottom <= session.DockHost.Divider.Top, "divider resizes calculator height and worksheet together");
        Check(view.Expression.Height > inputHeight, "enlarging the dock gives the input more visible lines");
        Size normal = host.Size;
        host.Height = (int)(450 * scale);
        Check(session.DockHost.Panel.Height >= session.DockHost.Divider.MinSize && mdi.Height >= session.DockHost.Divider.MinExtra, "small windows keep splitter limits within the available space");
        host.Size = normal;
        Check(session.DockHost.Panel.Height == height + 50, "restoring window size restores the preferred pane height");
        session.Hide();
        Check(!session.IsVisible && mdi.Size == full && !host.Controls.OfType<TabControl>().Any(), "Close removes the calculator and its divider without leaving a tab");
        calc.Show(host, () => { });
        Check(session.IsDocked && session.DockHost.Panel.Height == height + 50 && ReferenceEquals(view, calc.View) && view.Saved.Items.Count == 1, "reopening retains dock size and saved session");
        await session.ToggleModeAsync();
        session.Window.Close();
        Check(!session.IsVisible && !view.IsDisposed, "floating X hides rather than disposes the calculator");
        calc.Show(host, () => { });
        Check(session.Window != null, "reopening remembers floating mode");
        session.Window.Location = new Point(-16000, -16000);
        await session.ToggleModeAsync();
        help.Show(host, "1020", Path.Combine(AppContext.BaseDirectory, "Help"));
        await help.View.Ready.WaitAsync(TimeSpan.FromSeconds(30));
        Check(help.IsDocked && session.IsDocked && session.DockHost.Panel.Right <= help.DockHost.Divider.Left && mdi.Bottom <= session.DockHost.Divider.Top, "Calculator and Help dock together without overlap or tabs");
        help.Hide();
        session.Hide();
        help.Show(host, "1020", Path.Combine(AppContext.BaseDirectory, "Help"));
        calc.Show(host, () => { });
        Check(session.DockHost.Panel.Right <= help.DockHost.Divider.Left && mdi.Bottom <= session.DockHost.Divider.Top, "layout is consistent when tools open in the reverse order");
        await Task.Delay(100); // Unwind WebView2 callbacks before starting a modal loop.
        using var modal = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-16000, -16000), ShowInTaskbar = false };
        var parameter = new TextBox { Dock = DockStyle.Top };
        modal.Controls.Add(parameter);
        Exception modalError = null;
        modal.Shown += async (_, _) =>
        {
            try
            {
                parameter.Focus();
                await Task.Delay(150);
                Check(session.IsTemporary && help.IsTemporary && session.Window?.Owner == modal && help.Window?.Owner == modal, "both tools temporarily float alongside modal parameter dialogs");
                Check(parameter.Focused, "automatic floating leaves keyboard focus in the parameter dialog");
                view.Expression.Text = "6 * 7";
                Check(view.EvaluateCurrent() && view.Result.Text == "42", "calculator remains usable during a modal dialog");
            }
            catch (Exception ex) { modalError = ex; }
            finally { modal.Close(); }
        };
        modal.ShowDialog(host);
        if (modalError != null) throw modalError;
        await Task.Delay(150);
        Check(session.IsDocked && help.IsDocked && view.Result.Text == "42" && view.Saved.Items.Count == 1, "dialog closure redocks both tools with calculator state intact");
        string copied = null;
        using var clipboardView = new CalculatorView(() => { }, text => copied = text);
        clipboardView.Expression.Text = "\"two words\"";
        Check(clipboardView.EvaluateCurrent() && clipboardView.Result.Text == "two words", "calculator preserves spaces inside string expressions");
        clipboardView.CopyResult();
        Check(copied == "two words", "Copy result copies the unmodified result");
        clipboardView.Expression.Text = "2\t+\r\n3";
        clipboardView.SaveCalculation();
        clipboardView.Saved.SelectedIndex = 0;
        clipboardView.Recall();
        clipboardView.CopySaved();
        Check(clipboardView.Expression.Text == "2\t+\n3" && copied.EndsWith(" = 5"), "saved multiline/tabbed expressions retain the editor text and copy readable results");
        clipboardView.Expression.Text = "2 * ()";
        clipboardView.Expression.Select(5, 0);
        clipboardView.InsertSaved();
        Check(clipboardView.Expression.Text == "2 * (2\t+\n3)" && clipboardView.EvaluateCurrent() && clipboardView.Result.Text == "10", "saved expressions can be inserted at the caret in a new calculation");
        clipboardView.RemoveSaved();
        Check(clipboardView.Saved.Items.Count == 0, "saved calculations can be removed");
        help.Dispose();
        calc.Dispose();
        Check(view.IsDisposed && mdi.Size == full, "disposing the services releases both tools and restores the workspace");
        sheet.Close();
        await EditingChecks(host);
    }
}
