using System.Reflection;
using System.Runtime.InteropServices;
using StatsDirect.Templates;
using StatsDirect.UI;

internal static partial class Program
{
    // The Further analysis drop-down after an analysis (a support report on 5.2.6: after a multiple linear regression the drop-down
    // could not be opened by a click or by Alt+Down, while the simple and logistic regressions' could; the drop-down was laid out below
    // the visible edge of a top bar that had shrunk to the default panel, which is short while a report is active, and only a follow-on
    // that starts on its own, as the simple regression's interpolation does, re-sized the bar).  A report is created and made ready
    // first, as the runs' report, so that it is activated as the program's is.  Each regression is then run through the main window
    // exactly as the menu runs it: its options are accepted in the top bar with Run, and its data are selected on a worksheet of the
    // suite's own and accepted with OK.  The drop-down is examined in the state the user sees: whether the top bar is tall enough to
    // show it, the control under its arrow, the mouse capture, and whether it opens to a property, to a click on its arrow, and to F4.
    // A follow-on that asks for input straight after its parent is examined while it asks, then returned from, so that the suite cannot
    // hang on a prompt.  Last, every operation with follow-ons is swept: with the report active and the bar shrunk to the default
    // panel, suggesting its follow-ons must show the drop-down within the bar.
    [DllImport("user32.dll")] static extern IntPtr GetCapture();
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    const int WM_KEYDOWN = 0x100, WM_KEYUP = 0x101, WM_LBUTTONDOWN = 0x201, WM_LBUTTONUP = 0x202, VK_F4 = 0x73;

    static async Task CheckFurtherAnalysis(frmMain main, Form grid)
    {
        var combo = (ComboBox)Field(main, "cboOperation");
        var childrenBefore = main.MdiChildren.ToList();
        var mine = (Form)Call(main, "CreateGrid");   // a worksheet of the check's own: columns A and B predictors, C the outcome
        await Task.Delay(150);
        var workbook = (SpreadsheetGear.Windows.Forms.WorkbookView)Field(mine, "workbookView");
        double[] x1 = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }, x2 = { 3, 1, 4, 1, 5, 9, 2, 6, 5, 3, 5, 8 }, y = { 2.9, 5.2, 7.4, 8.1, 11.3, 14.2, 13.8, 17.1, 18.6, 19.9, 23.2, 25.1 };
        workbook.GetLock();
        try
        {
            var sheet = workbook.ActiveWorksheet;
            for (int r = 0; r < 12; r++) { sheet.Cells[r, 0].Value = x1[r]; sheet.Cells[r, 1].Value = x2[r]; sheet.Cells[r, 2].Value = y[r]; }
        }
        finally { workbook.ReleaseLock(); }
        var report = (Form)Call(main, "CreateReport");   // the runs' report, ready before they append to it
        await ((Task)Get(Field(report, "view"), "Ready")).WaitAsync(TimeSpan.FromSeconds(45));
        try
        {
            // the ranges each regression's selections take, in the order its data are asked for
            foreach ((string name, string[] ranges) in new[] { ("SimpleLinearRegression", new[] { "C1:C12", "A1:A12" }), ("MultipleLinearRegression", new[] { "C1:C12", "A1:B12" }) })
            {
                Check(TemplateFactory.Operations.TryGetValue(name, out Operation operation), name + " is a known operation");
                mine.Activate(); await Task.Delay(150);   // the worksheet in front, as when an analysis is chosen from the menu
                bool probed = false; (bool shown, bool opens) result = default;
                async Task Probe(string label)
                {
                    if (probed) return;
                    probed = true;
                    result = (shown: Describe(main, label, operation), opens: await Opens(main, combo));
                }
                (bool completed, Exception failure, int requests) = await RunAsTheMenuDoes(main, workbook, operation, new Queue<string>(ranges), () => Probe(name + " while its follow-on asks for input"));
                Console.WriteLine($"      {name}: {requests} request(s) answered; run {(completed ? "finished" : "did not finish")}" + (failure == null ? "" : "; threw " + failure));
                await Probe(name + " after the run");
                Check(completed && failure == null && result.shown, name + "'s follow-ons fill the Further analysis drop-down, shown within the top bar under its own arrow");
                Check(result.opens, "the Further analysis drop-down opens after " + name + " to a property, to a click on its arrow and to F4");
                Call(main, "CloseCurrentOperation");
                await Task.Delay(50);
            }
            report.Activate(); await Task.Delay(150);   // a report in front, as after a run: the main toolbar hides and the default panel is short
            Check(main.ActiveMdiChild == report && !((Control)Field(main, "toolStrip")).Visible, "a report is in front and the main toolbar is hidden, so the default panel is at its short height");
            await SweepFollowOnSuggestions(main);
        }
        finally
        {
            foreach (var child in main.MdiChildren.Except(childrenBefore).ToList()) child.Dispose();   // the check's worksheet and report
            await Task.Delay(50);
            grid.Activate();
        }
    }

    /// <summary>Every operation with follow-ons: with a report in front and the bar shrunk to the default panel, as after a run, suggesting
    /// its follow-ons must show the drop-down within the bar.  The operations whose first follow-on waits for Run are listed: those are
    /// the ones whose drop-down a user met the fault with, as a follow-on that starts on its own re-sizes the bar.</summary>
    static async Task SweepFollowOnSuggestions(frmMain main)
    {
        var combo = (ComboBox)Field(main, "cboOperation");
        var panel = (Control)Field(main, "pnlSelectOperation");
        var top = (Control)Field(main, "pnlTop");
        var startsOnItsOwn = typeof(frmMain).GetMethod("ShouldRunOperationOnSelection", BindingFlags.NonPublic | BindingFlags.Static)!;
        var withFollowOns = TemplateFactory.Operations.Values.Where(o => o.SuggestedOperations.Count > 0).OrderBy(o => o.Name, StringComparer.Ordinal).ToList();
        var clipped = new List<string>(); var waiting = new List<string>(); var starting = new List<string>();
        int shrunkBar = -1;
        foreach (Operation operation in withFollowOns)
        {
            Call(main, "CloseCurrentOperation");   // the bar at the default panel's height
            await Task.Delay(20);
            if (shrunkBar < 0) shrunkBar = top.Height;
            var items = operation.SuggestedOperations.Where(s => TemplateFactory.Operations.ContainsKey(s.Name))
                .Select(s => new SDListItem(TemplateFactory.Operations[s.Name].FriendlyName ?? s.Name, s.Name)).ToList();
            panel.Visible = true;   // as the suggestion after a run shows it
            Call(main, "SuggestOperations", items, new ParameterBag(), operation, SuggestionTime.BeforeOperation);
            await Task.Delay(20);
            Rectangle bar = top.RectangleToScreen(top.ClientRectangle), box = combo.RectangleToScreen(combo.ClientRectangle);
            if (!bar.Contains(box) || combo.Items.Count != items.Count)
                clipped.Add($"{operation.Name} (bar {bar.Height} tall, drop-down ends {box.Bottom - bar.Top} below its top, items {combo.Items.Count} of {items.Count})");
            // the first follow-on, as the run selects it: does it start on its own, or wait for Run with the drop-down shown?
            if (TemplateFactory.Operations.TryGetValue(operation.SuggestedOperations[0].Name, out Operation first))
            {
                var known = new ParameterBag();   // the parent's own parameters, as a run leaves them
                foreach (Step step in operation.Steps)
                    if (step.GetType().GetProperty("Parameters")?.GetValue(step) is IEnumerable<Parameter> parameters)
                        foreach (Parameter p in parameters) if (p.Name != null && !known.ContainsKey(p.Name)) known.AddInput(p.Name, 0.0);
                string verdict;
                try { verdict = (bool)startsOnItsOwn.Invoke(null, new object[] { first, known })! ? "starts" : "waits"; }
                catch (Exception ex) { verdict = "undecided: " + (ex.InnerException ?? ex).Message; }
                (verdict == "starts" ? starting : waiting).Add(operation.Name + " -> " + first.Name + (verdict.StartsWith("undecided") ? " (" + verdict + ")" : ""));
            }
        }
        Call(main, "CloseCurrentOperation");
        await Task.Delay(20);
        Console.WriteLine($"      {withFollowOns.Count} operations suggest follow-ons; the bar shrunk to {shrunkBar} with the toolbar {(((Control)Field(main, "toolStrip")).Visible ? "shown" : "hidden")}; "
            + $"the first follow-on starts on its own for {starting.Count} and waits for Run for {waiting.Count}:");
        Console.WriteLine("      waits for Run: " + string.Join(", ", waiting));
        if (clipped.Count > 0) Console.WriteLine("      drop-down clipped for " + clipped.Count + ": " + string.Join("; ", clipped.Take(6)) + (clipped.Count > 6 ? "; ..." : ""));
        Check(clipped.Count == 0, $"with a report in front and the bar shrunk to the default panel, the follow-on suggestion of each of the {withFollowOns.Count} operations with follow-ons shows the drop-down within the bar");
    }

    /// <summary>The deepest control under the drop-down's arrow: the drop-down itself unless something lies over it or the bar clips it.</summary>
    static Control ControlAtTheArrow(frmMain main, ComboBox combo)
    {
        Point arrow = combo.PointToScreen(new Point(combo.Width - 8, combo.Height / 2));
        Control hit = main;
        while (true)
        {
            Control child = hit.GetChildAtPoint(hit.PointToClient(arrow), GetChildAtPointSkip.Invisible);
            if (child == null) return hit;
            hit = child;
        }
    }

    static bool Describe(frmMain main, string label, Operation operation)
    {
        var combo = (ComboBox)Field(main, "cboOperation");
        var panel = (Control)Field(main, "pnlSelectOperation");
        var instructions = (Control)Field(main, "pnlFollowOnInstructions");
        var variables = (Control)Field(main, "pnlVariables");
        var operations = (Control)Field(main, "pnlOperations");
        var top = (Control)Field(main, "pnlTop");
        var left = (Control)Field(main, "tlpSelectOperation");
        var toolbar = (Control)Field(main, "toolStrip");
        var standard = (Control)Field(main, "pnlDefault");
        Control hit = ControlAtTheArrow(main, combo);
        Rectangle bar = top.RectangleToScreen(top.ClientRectangle), box = combo.RectangleToScreen(combo.ClientRectangle);
        bool withinTheBar = bar.Contains(box);
        IntPtr capture = GetCapture();
        Control captor = capture == IntPtr.Zero ? null : Control.FromChildHandle(capture);
        Console.WriteLine($"      {label}: items {combo.Items.Count} of {operation.SuggestedOperations.Count}, selected {combo.SelectedIndex}, panel visible {panel.Visible}, operations panel visible {operations.Visible}, "
            + $"drop-down visible {combo.Visible} enabled {combo.Enabled} can focus {combo.CanFocus} width {combo.Width}, top bar {top.Height} tall for content {left.Height} (drop-down within the bar: {withinTheBar}; "
            + $"default panel {standard.Height}, toolbar {(toolbar.Visible ? "shown" : "hidden")}, active window {main.ActiveMdiChild?.GetType().Name ?? "none"}), "
            + $"instructions visible {instructions.Visible}, variables visible {variables.Visible}, under the arrow: {hit.Name}, "
            + $"capture: {(capture == IntPtr.Zero ? "none" : captor?.Name ?? capture.ToString())}, inputting {Field(main, "inputtingData")}, selecting {Field(main, "selectingData")}, "
            + $"in operation {Get(main, "InOperation")}, open forms {Application.OpenForms.Count}, main enabled {main.Enabled}, title '{main.Text}'");
        return combo.Items.Count == operation.SuggestedOperations.Count && panel.Visible && operations.Visible && combo.Visible && combo.Enabled && withinTheBar && hit == combo
            && !toolbar.Visible && main.ActiveMdiChild?.GetType().Name == "frmReportWebView";   // the state the fault needs: a report in front, the toolbar hidden
    }

    /// <summary>Whether the drop-down opens to its property, to a click on its arrow, and to F4, each list closed again.</summary>
    static async Task<bool> Opens(frmMain main, ComboBox combo)
    {
        combo.Focus(); await Task.Delay(50);   // the thread's focus, without taking the foreground: none of the openings needs it
        combo.DroppedDown = true; await Task.Delay(150);
        bool byProperty = combo.DroppedDown;
        combo.DroppedDown = false; await Task.Delay(100);
        int x = combo.Width - 8, y = combo.Height / 2;
        IntPtr at = (IntPtr)((y << 16) | (x & 0xFFFF));
        PostMessage(combo.Handle, WM_LBUTTONDOWN, (IntPtr)1, at); PostMessage(combo.Handle, WM_LBUTTONUP, IntPtr.Zero, at);
        await Task.Delay(200);
        bool byClick = combo.DroppedDown;
        combo.DroppedDown = false; await Task.Delay(100);
        PostMessage(combo.Handle, WM_KEYDOWN, (IntPtr)VK_F4, IntPtr.Zero); PostMessage(combo.Handle, WM_KEYUP, (IntPtr)VK_F4, IntPtr.Zero);
        await Task.Delay(200);
        bool byKey = combo.DroppedDown;
        combo.DroppedDown = false; await Task.Delay(50);
        Console.WriteLine($"      opens: to the property {byProperty}, to a click on its arrow {byClick}, to F4 {byKey}");
        return byProperty && byClick && byKey;
    }

    static string Describe(Control root)   // the visible controls of a panel, for a request's log line
    {
        var parts = new List<string>();
        void Walk(Control c) { foreach (Control child in c.Controls) { if (child.Visible && child is not Panel and not TableLayoutPanel) parts.Add(child.GetType().Name + (string.IsNullOrEmpty(child.Text) ? "" : " '" + child.Text.Replace("\r\n", " ").Replace("\n", " ") + "'")); Walk(child); } }
        Walk(root);
        return string.Join(", ", parts);
    }

    /// <summary>Runs an operation through the main window as the menu does, answering its requests: options in the top bar are accepted
    /// with Run, each selection takes the next range of the queue and is accepted with OK, and a message in the top bar takes its first
    /// button.  A request made by a follow-on (the window's title no longer names the operation) runs the callback once and is left with
    /// Return, so that the suite cannot hang.</summary>
    static async Task<(bool completed, Exception failure, int requests)> RunAsTheMenuDoes(frmMain main, SpreadsheetGear.Windows.Forms.WorkbookView workbook, Operation operation, Queue<string> ranges, Func<Task> whileFollowOnAsks)
    {
        bool done = false; Exception failure = null; int requests = 0;
        main.BeginInvoke(() =>
        {
            try { Call(main, "DoOperationOnceOrUntilCancelled", operation, null); }
            catch (Exception ex) { failure = ex is TargetInvocationException thrown ? thrown.InnerException : ex; }
            finally { done = true; }
        });
        var run = (Button)Field(main, "cmdCalculate");
        var ok = (Button)Field(main, "cmdOK");
        var close = (Button)Field(main, "cmdClose");
        var cancel = (Button)Field(main, "cmdCancel");
        var message = (Label)Field(main, "lblSelectionMessage");
        var modalText = (Label)Field(main, "lblModalMessageText");
        var modalFirst = (Button)Field(main, "cmdModalMessage1");
        var user = (Control)Field(main, "pnlUser");
        string title = "StatsDirect: " + operation;
        int idle = 0;
        for (int i = 0; i < 600 && !done; i++)
        {
            await Task.Delay(50);
            if (done) break;
            bool inputting = (bool)Field(main, "inputtingData"), selecting = (bool)Field(main, "selectingData"), modal = (bool)Field(main, "waitingForModalMessage");
            if (!inputting && !selecting && !modal)
            {
                if (++idle % 40 == 0) Console.WriteLine($"      ... waiting: panel {Field(main, "currentPanelType")}, title '{main.Text}', in operation {Get(main, "InOperation")}, top bar: {Describe(user)}");
                continue;
            }
            idle = 0;
            requests++;
            if (requests > 8) { Console.WriteLine("      too many requests; the operation is left"); (modal ? modalFirst : selecting ? cancel : close).PerformClick(); continue; }
            if (modal)
            {
                Console.WriteLine($"      message in the top bar: '{modalText.Text.Replace("\r\n", " ")}' -> its first button, '{modalFirst.Text}'");
                modalFirst.PerformClick();
            }
            else if (!main.Text.StartsWith(title, StringComparison.Ordinal))   // the title carries the active document's caption after the operation's name
            {   // a follow-on asks: the state the user sees after the run
                Console.WriteLine($"      '{main.Text}' asks for {(selecting ? "a selection: " + message.Text : "input in the top bar: " + Describe(user))}; probed, then Return");
                await whileFollowOnAsks();
                (selecting ? cancel : close).PerformClick();
            }
            else if (selecting)
            {
                string range = ranges.Count > 0 ? ranges.Dequeue() : null;
                Console.WriteLine($"      selection asked: '{message.Text}' -> {range ?? "nothing left to select; Cancel"}");
                if (range == null) { cancel.PerformClick(); continue; }
                workbook.GetLock();
                try { workbook.RangeSelection = workbook.ActiveWorksheet.Cells[range]; }
                finally { workbook.ReleaseLock(); }
                await Task.Delay(50);
                ok.PerformClick();
            }
            else
            {
                Console.WriteLine($"      options asked in the top bar: {Describe(user)}; Run");
                run.PerformClick();
            }
            await Task.Delay(100);
        }
        return (done, failure, requests);
    }
}
