using StatsDirect.Data;
using StatsDirect.TemplateProcessing;
using StatsDirect.Templates;
using StatsDirect.UI;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

// Checks of the writing of an operation's output frames to a worksheet by the real document host: the sheet the user picks is written
// to even when the operation's steps keep the selection, as the fits and residuals of a multiple linear regression do; the frames after
// the first follow it; a pick of a new sheet makes one sheet; and without a pick a step that keeps the selection still writes after the
// selection of the active grid.
internal static class Program
{
    const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static int checks, unhandled;
    static readonly List<object> evidence = new();

    static void Check(bool passed, string description)
    {
        evidence.Add(new { description, passed });
        if (!passed) throw new InvalidOperationException(description);
        checks++;
        Console.WriteLine("PASS " + description);
    }

    static object? Get(object obj, string name) => obj.GetType().GetProperty(name, Members)?.GetValue(obj)
        ?? obj.GetType().GetField(name, Members)?.GetValue(obj);

    [STAThread]
    static int Main()
    {
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
        // The real document host without start-up, settings writes, IPC or update dialogs.
        foreach (var pair in new[] { ("Load", "frmMain_Load"), ("Shown", "frmMain_Shown"), ("FormClosing", "frmMain_FormClosing"), ("FormClosed", "frmMain_FormClosed") })
        {
            var evt = typeof(frmMain).GetEvent(pair.Item1)!;
            evt.RemoveEventHandler(main, typeof(frmMain).GetMethod(pair.Item2, Members)!.CreateDelegate(evt.EventHandlerType!, main));
        }
        main.Location = new Point(-16000, -16000); main.StartPosition = FormStartPosition.Manual;
        main.Size = new Size(1600, 1000); main.ShowInTaskbar = false;
        int result = 1; string? failure = null;
        main.Shown += (_, _) =>
        {
            try
            {
                Run(main, app);
                Check(unhandled == 0, "no exception escapes to the Windows UI thread");
                Console.WriteLine($"PASS {checks} worksheet output checks");
                result = 0;
            }
            catch (Exception error) { failure = error.ToString(); Console.WriteLine("FAIL " + failure); }
            finally
            {
                foreach (var child in main.MdiChildren) { child.GetType().GetProperty("Dirty", Members)?.SetValue(child, false); child.Dispose(); }
                main.Close();
            }
        };
        Application.Run(main);
        string? artifacts = Environment.GetEnvironmentVariable("STATSDIRECT_WORKSHEET_OUTPUT_ARTIFACTS");
        if (!string.IsNullOrEmpty(artifacts))
        {
            Directory.CreateDirectory(artifacts);
            File.WriteAllText(Path.Combine(artifacts, "results.json"), JsonSerializer.Serialize(new { passed = result == 0 && unhandled == 0, checks, unhandled, failure, evidence }, new JsonSerializerOptions { WriteIndented = true }));
        }
        return unhandled > 0 ? 1 : result;
    }

    static void Run(frmMain main, object app)
    {
        IUserInterface host = (IUserInterface)app;
        var createGrid = typeof(frmMain).GetMethod("CreateGrid", Members, new[] { typeof(string) })!;
        var data = (IGrid)createGrid.Invoke(main, new object?[] { "Example data" })!;
        data.SetValues(0, 0, new object[,] { { "Y", "X" }, { 1.0, 2.0 }, { 2.0, 3.0 }, { 4.0, 3.0 } });
        var target = (IGrid)createGrid.Invoke(main, new object?[] { "Data 1" })!;
        // The grid of the data is the active one, as it is when the follow-on of a regression is run; the pick is of the other grid.
        data.EnsureActive(); Application.DoEvents();
        Check(ReferenceEquals(ActiveGrid(app), data), "the grid of the data is the active grid before the output");
        var panes = Panes(target);
        Check(panes.Count == 2 && "Sheet1".Equals(panes[0].Tag) && panes[1].Tag == null, "the other grid offers its one sheet and a new sheet");
        var pick = Pick(panes[0], RelativePosition.LastColumn);

        // The first frame of a step that keeps the selection, with the other grid's sheet picked.
        host.OutputFrame(Frame("Y Fit", "SD Y Fit", "Residual"), true, false, "*", pick, RelativePosition.AfterSelection);
        Application.DoEvents();
        Check(Titles(target, 0, 2) == "Y Fit|SD Y Fit|Residual", "the first frame of a step that keeps the selection goes to the picked sheet of the other grid");
        Check(Titles(data, 0, 1) == "Y|X" && data.UsedArea.Columns == 2, "the active grid of the data is untouched by the pick");
        Check(ReferenceEquals(ActiveGrid(app), target), "the picked grid is the active grid after the first frame");

        // The frames after the first, with the same pick.
        host.OutputFrame(Frame("Studentised", "Leverage", "Cook"), true, false, "*", pick, RelativePosition.AfterSelection);
        host.OutputFrame(Frame("Jackknife", "DFIT"), true, false, "*", pick, RelativePosition.AfterSelection);
        Application.DoEvents();
        Check(Titles(target, 3, 7) == "Studentised|Leverage|Cook|Jackknife|DFIT", "the frames after the first follow it on the picked sheet");
        Check(Panes(target).Count == 2, "the frames after the first add no sheet");
        Check(data.UsedArea.Columns == 2, "the grid of the data stays untouched");

        // A pick of a new sheet: one sheet for the operation's frames.
        data.EnsureActive(); Application.DoEvents();
        var fresh = Pick(panes[1], RelativePosition.LastColumn);
        host.OutputFrame(Frame("Fit"), true, false, "*", fresh, RelativePosition.AfterSelection);
        host.OutputFrame(Frame("SE"), true, false, "*", fresh, RelativePosition.AfterSelection);
        Application.DoEvents();
        Check(Panes(target).Count == 3, "a pick of a new sheet makes one sheet for an operation's frames");
        Check(target.ActiveWorksheetName != "Sheet1" && Titles(target, 0, 1) == "Fit|SE", "both frames are on the new sheet, the second after the first");

        // Without a pick, a step that keeps the selection writes after the selection of the active grid, as it did.
        data.EnsureActive(); Application.DoEvents();
        data.Selection = new StatsDirect.UI.Range(new[] { new Area(data, 0, 0, 3, 0) });
        host.OutputFrame(Frame("After"), true, false, "*", null, RelativePosition.AfterSelection);
        Application.DoEvents();
        Check(Titles(data, 0, 2) == "Y|After|X", "without a pick, a step that keeps the selection writes after the selection in the active grid");

        // A pick made afresh, equal to the first, is taken again though the step keeps the selection: a later run of the operation.
        var again = Pick(panes[0], RelativePosition.LastColumn);
        Check(again.Equals(pick) && !ReferenceEquals(again, pick), "the pick made afresh equals the first without being it");
        host.OutputFrame(Frame("Fit 2"), true, false, "*", again, RelativePosition.AfterSelection);
        Application.DoEvents();
        Check(target.ActiveWorksheetName == "Sheet1" && Titles(target, 8, 8) == "Fit 2", "a pick made afresh is taken again for a step that keeps the selection");
        Check(Titles(data, 0, 2) == "Y|After|X" && data.UsedArea.Columns == 3, "the grid of the data is untouched by the later pick");

        // The workbook font of the preferences, as a new grid takes it. 5.3.9 and earlier wrote and read its size in the culture of the
        // day: the 10,5 points saved where the decimal separator is a comma were 105 points where it is a point.
        object settings = typeof(frmMain).Assembly.GetType("StatsDirect.UI.Properties.Settings")!.GetProperty("Default", BindingFlags.Static | BindingFlags.Public)!.GetValue(null)!;
        PropertyInfo workbookFont = settings.GetType().GetProperty("DefaultWorkbookFont", Members)!;
        object? fontBefore = workbookFont.GetValue(settings);
        CultureInfo culture = CultureInfo.CurrentCulture;
        List<string> sizes = new();
        string written;
        try
        {
            foreach ((string reader, string saved) in new[] { ("en-GB", "Arial;0;10,5"), ("de-DE", "Arial;0;10.5"), ("de-DE", "Arial;0;10,5"), ("en-GB", "Arial;0;10.5") })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(reader);
                workbookFont.SetValue(settings, saved);
                sizes.Add(DefaultFontSize((IGrid)createGrid.Invoke(main, new object?[] { "Font " + sizes.Count })!).ToString(CultureInfo.InvariantCulture));
            }
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            using Font chosen = new("Arial", 10.5f);
            written = StatsDirect.Utilities.Utilities.SaveStringFromFont(chosen);
        }
        finally { CultureInfo.CurrentCulture = culture; workbookFont.SetValue(settings, fontBefore); }
        Application.DoEvents();
        Check(string.Join(" ", sizes) == "10.5 10.5 10.5 10.5",
            "a new grid takes the saved workbook font at its 10.5 points, whether the size was saved with a comma or with a point: " + string.Join(" ", sizes));
        Check(written == "Arial;0;10.5", "a workbook font chosen where the decimal separator is a comma is saved with its size in the invariant culture: " + written);

        // The "Precision of estimates:" prompt of logistic and of Poisson regression, with a precision typed into its box and Run pressed.
        // Up to 5.3.9 the definitions of the two limited the box to 12 characters: a typed 0.00000000001 was cut at "0.0000000000", which
        // is read as a precision of 0.
        const string precision = "0.00000000001";
        string[] taken = new[] { "LogisticRegression", "PoissonRegression" }.Select(operation => operation + " " + Typed(main, app, operation, "accuracy", precision)).ToArray();
        Check(taken.All(answer => answer.EndsWith(" " + precision + " = 1E-11")),
            "the precision prompts of logistic and of Poisson regression take a typed " + precision + " in full: " + string.Join(", ", taken));
    }

    [DllImport("user32.dll")]
    static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

    // What the program takes from a text prompt of an operation when a text is typed into its box and Run is pressed, with the number
    // the regressions read from it. The prompt is prepared from the operation's definition and is read by the real main window, and the
    // characters reach the box as key presses do: the limit of a text box applies to what is typed, not to a text that is set.
    static string Typed(frmMain main, object app, string operation, string name, string text)
    {
        Parameter parameter = TemplateFactory.Operations[operation].Steps.OfType<ParametersStep>().SelectMany(step => step.Parameters).Single(asked => asked.Name == name);
        ITemplateHost host = (ITemplateHost)app;
        bool typed = false;
        DateTime patience = DateTime.UtcNow.AddSeconds(20);
        using System.Windows.Forms.Timer typist = new() { Interval = 50 };
        typist.Tick += (_, _) =>
        {
            TextBox? box = Descendants(main).OfType<TextBox>().FirstOrDefault(control => ReferenceEquals(control.Tag, parameter) && control.Visible && control.IsHandleCreated);
            if (box != null && !typed)
            {
                typed = true;
                box.SelectAll(); // over the default, as a user who tabs to the box types
                foreach (char character in text) SendMessage(box.Handle, 0x0102 /* WM_CHAR */, (IntPtr)character, IntPtr.Zero);
                ((Button)Get(main, "cmdCalculate")!).PerformClick();
            }
            else if (DateTime.UtcNow > patience)
            {
                // Return, so that a prompt that was not answered cannot hold the checks
                typist.Stop();
                typeof(frmMain).GetMethod("cmdClose_Click", Members)!.Invoke(main, new object?[] { null, EventArgs.Empty });
            }
        };
        typist.Start();
        ParameterBag? filled;
        try
        {
            filled = (ParameterBag?)typeof(frmMain).GetMethod("FillAndValidateCombinedParameters", Members)!
                .Invoke(main, new object[] { host, new TemplateProcessor(host), new ParameterBag(), new List<Parameter> { parameter } });
        }
        finally { typist.Stop(); }
        if (filled == null || !filled.ContainsKey(name)) return typed ? "(typed, but Run gave no answer)" : "(the prompt's box was not shown)";
        string answer = filled[name].AsString;
        return answer + " = " + StatsDirect.Utilities.Parsing.Cdbl_Txt(answer).ToString("R", CultureInfo.InvariantCulture);
    }

    static IEnumerable<Control> Descendants(Control control) => control.Controls.Cast<Control>().SelectMany(child => Descendants(child).Prepend(child));

    // The default font size of the grid's workbook set, read under the workbook set's lock as the program reads it
    static double DefaultFontSize(IGrid grid)
    {
        object view = Get(grid, "workbookView")!;
        view.GetType().GetMethod("GetLock", Type.EmptyTypes)!.Invoke(view, null);
        try
        {
            object set = view.GetType().GetProperty("ActiveWorkbookSet")!.GetValue(view)!;
            return Convert.ToDouble(set.GetType().GetInterface("SpreadsheetGear.IWorkbookSet")!.GetProperty("DefaultFontSize")!.GetValue(set), CultureInfo.InvariantCulture);
        }
        finally { view.GetType().GetMethod("ReleaseLock", Type.EmptyTypes)!.Invoke(view, null); }
    }

    static object? ActiveGrid(object app) => Get(Get(app, "ActiveGrid")!, "Window");
    static IList<Pane> Panes(IGrid grid) => (IList<Pane>)Get(grid, "AvailablePanes")!;
    static PaneAndPosition Pick(Pane pane, RelativePosition position) =>
        (PaneAndPosition)Activator.CreateInstance(typeof(PaneAndPosition), Members, null, new object[] { pane, position }, null)!;

    static DataFrame Frame(params string[] titles)
    {
        DataFrame frame = new();
        foreach (string title in titles)
        {
            DoubleVariable variable = new(3, title);
            for (int i = 0; i < 3; i++) variable.SetData(i, i + 0.5);
            frame.Variables.Add(variable);
        }
        return frame;
    }

    static string Titles(IGrid grid, int left, int right)
    {
        object[,] values = grid.GetValues(0, left, 0, right);
        return string.Join("|", Enumerable.Range(0, values.GetLength(1)).Select(c => values[0, c]?.ToString() ?? ""));
    }
}
