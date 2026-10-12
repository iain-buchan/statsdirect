using System.Reflection;
using System.Windows.Forms;
using StatsDirect.R;
using StatsDirect.UI;

internal static partial class Program
{
    // The script windows Continue in R opens: the main window names each Script 1, Script 2 ... from the numbers of the script windows
    // open, Continue in R puts the analysis after the number, and the tab is named with the window.  The naming is internal to the program,
    // so the check reaches it by reflection, doing what the main window does to open a window and what the report does to show the script.
    private static void CheckScriptWindowNumbers()
    {
        Assembly program = typeof(RScriptWriter).Assembly;
        Type application = program.GetType("StatsDirect.UI.SdApplication");
        object app = application.GetProperty("SoleInstance", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        MethodInfo nextNumber = application.GetMethod("GetScriptWindowNumber", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo addWindow = application.GetMethod("AddWindow", BindingFlags.Instance | BindingFlags.NonPublic);
        Type script = program.GetType("StatsDirect.UI.frmScript");
        MethodInfo showR = script.GetMethod("ShowR", BindingFlags.Instance | BindingFlags.NonPublic);
        PropertyInfo windowOf = typeof(WindowInformation).GetProperty("Window", BindingFlags.Instance | BindingFlags.NonPublic);
        PropertyInfo tabOf = typeof(WindowInformation).GetProperty("TabPage", BindingFlags.Instance | BindingFlags.NonPublic);
        PropertyInfo helpTopic = script.GetProperty("HelpTopic", BindingFlags.Instance | BindingFlags.NonPublic);
        (Form window, TabPage tab) Open(string title, string topic = null, string text = "x <- 1")
        {
            // as frmMain.CreateScriptWindow and SetUpForm open a script window, then as frmReportWebView shows the script in it
            Form window = (Form)Activator.CreateInstance(script);
            window.Text = window.Text + " " + (int)nextNumber.Invoke(app, null);
            TabPage tab = new(window.Text);
            WindowInformation info = new();
            windowOf.SetValue(info, window);
            tabOf.SetValue(info, tab);
            window.Tag = info;
            addWindow.Invoke(app, new object[] { info });
            showR.Invoke(window, new object[] { text, title, topic, true });
            return (window, tab);
        }
        (Form window, TabPage tab) first = Open("Paired t test", "1239");
        (Form window, TabPage tab) second = Open("Unpaired t test");
        // the toolbar: Save, then Cut, Copy and Paste with the Edit menu's icons, then Find, then the R tools
        ToolStrip toolbar = (ToolStrip)script.GetField("ToolStrip1", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(first.window);
        ToolStripButton[] buttons = toolbar.Items.OfType<ToolStripButton>().ToArray();
        Check(string.Join("|", buttons.Take(11).Select(b => b.Text)) == "Save|Undo|Redo|Cut|Copy|Paste|Find|Run|Help|AI assistance|Open in R" && buttons.Take(11).All(b => b.Image != null), "one script toolbar follows the common document order with AI then R+: " + string.Join(", ", buttons.Select(b => b.Text)));
        // the toolbar is drawn as the main window's: the same renderer paints a symbol for each button by its name at the display size, the
        // bitmaps serving only to size the buttons, so the script window's buttons take the names the renderer knows, and it must draw each
        Type renderer = program.GetType("StatsDirect.UI.SharpToolbarRenderer");
        Check(toolbar.Renderer.GetType() == renderer && toolbar.ImageScalingSize == toolbar.LogicalToDeviceUnits(new System.Drawing.Size(20, 20)),
              "the script window's toolbar has the main window's renderer and icon size");
        Check(string.Join("|", buttons.Take(11).Select(b => b.Name)) == "saveToolStripButton|undoToolStripButton|redoToolStripButton|cutToolStripButton|copyToolStripButton|pasteToolStripButton|findToolStripButton|runToolStripButton|helpToolStripButton|aiToolStripButton|rPlusToolStripButton",
              "the eleven primary buttons use the common vector icons");
        var openR=buttons.Single(b=>b.Name=="rPlusToolStripButton");
        Check(openR.DisplayStyle==ToolStripItemDisplayStyle.Image&&openR.Text=="Open in R"&&openR.ToolTipText=="Open in R"&&openR.AccessibleName=="Open in R"&&buttons.Count(b=>b.Text=="Open in R")==1,"R+ is one icon with an accessible Open in R label for its tooltip and overflow");
        MethodInfo draw = renderer.GetMethod("DrawSymbol", BindingFlags.Static | BindingFlags.NonPublic);
        int Ink(ToolStripItem item)   // the pixels the renderer paints for an item, on a white bitmap
        {
            using System.Drawing.Bitmap bitmap = new(32, 32);
            using System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(bitmap);
            g.Clear(System.Drawing.Color.White);
            draw.Invoke(null, new object[] { new ToolStripItemImageRenderEventArgs(g, item, new System.Drawing.Rectangle(0, 0, 32, 32)) });
            int ink = 0;
            for (int i = 0; i < 32; i++) for (int j = 0; j < 32; j++) if (bitmap.GetPixel(i, j).ToArgb() != System.Drawing.Color.White.ToArgb()) ink++;
            return ink;
        }
        string[] inks = buttons.Take(11).Select(b => b.Text + " " + Ink(b)).ToArray();
        Check(buttons.Take(11).All(b => Ink(b) > 40) && Ink(new ToolStripButton("Nothing") { Name = "noSuchButton" }) == 0, "the renderer draws all eleven command symbols: " + string.Join(", ", inks));
        Check(toolbar.CanOverflow && toolbar.OverflowButton.AccessibleName == "More commands" && buttons.Where(b => b.Text is "Clear output" or "Output folder").All(b => b.Overflow == ToolStripItemOverflow.Always), "secondary script actions use accessible More overflow");
        var generation = script.GetField("rGeneration", BindingFlags.Instance | BindingFlags.NonPublic);
        int beforeRun = (int)generation.GetValue(first.window);
        buttons.Single(b=>b.Text=="Run").PerformClick();
        Check((int)generation.GetValue(first.window)==beforeRun+1, "the single labelled Run button starts the existing R workflow");
        Check(first.window.Text == "Script 1: Paired t test" && first.tab.Text == first.window.Text, "the first script window is Script 1 with the analysis after it, and its tab is named with it: " + first.tab.Text);
        Check(second.window.Text == "Script 2: Unpaired t test" && second.tab.Text == second.window.Text, "a second Continue in R opens Script 2: " + second.tab.Text);
        Check((int)nextNumber.Invoke(app, null) == 3, "the next script window would be Script 3");
        // Help: the result's topic when the script continues one; the window's own topic (1401) for a script of no result or an empty window
        (Form window, TabPage tab) empty = Open("Nothing yet", "1239", "");
        Check((string)helpTopic.GetValue(first.window) == "1239" && (string)helpTopic.GetValue(second.window) == "1401" && (string)helpTopic.GetValue(empty.window) == "1401",
              "Help opens the result's topic for its script, and the R script window's own topic for a script of no result or an empty window");
        empty.window.Dispose();
        // what Find selects stays highlighted while its dialog has the focus; printing draws the script, page by page, through the print document
        RichTextBox editor = (RichTextBox)script.GetField("rtbDoc", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(first.window);
        Check(!editor.HideSelection, "the editor keeps its selection visible when another window has the focus, so that what Find selects is highlighted");
        System.Drawing.Printing.PrintDocument document = (System.Drawing.Printing.PrintDocument)script.GetField("PrintDocument1", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(first.window);
        if (document.PrinterSettings.IsValid)
        {
            editor.Text = string.Join("\n", Enumerable.Range(1, 120).Select(i => "line " + i + " of a script long enough for two pages"));
            System.Drawing.Printing.PreviewPrintController preview = new();
            document.PrintController = preview;
            document.Print();
            System.Drawing.Printing.PreviewPageInfo[] pages = preview.GetPreviewPageInfo();
            int Dark(System.Drawing.Image image) { using System.Drawing.Bitmap b = new(image); int n = 0; for (int x = 0; x < b.Width; x += 2) for (int y = 0; y < b.Height; y += 2) if (b.GetPixel(x, y).GetBrightness() < 0.5) n++; return n; }
            Check(pages.Length >= 2 && Dark(pages[0].Image) > 200 && Dark(pages[1].Image) > 200, $"printing draws the script on {pages.Length} pages, each with text on it");
        }
        else Console.WriteLine("      (no printer is set up on this PC, so the printed pages are not checked)");
        // the output pane as an R session: a monospace face, the commands after their prompts in a colour of their own
        RichTextBox pane = (RichTextBox)script.GetField("rtbOutput", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(first.window);
        script.GetMethod("AppendTranscript", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(first.window, new object[] { "> x <- 1\n[1] 1\n" });
        pane.Select(pane.Text.IndexOf("> x <- 1", StringComparison.Ordinal), 1); System.Drawing.Color promptColour = pane.SelectionColor;
        pane.Select(pane.Text.IndexOf("[1] 1", StringComparison.Ordinal), 1); System.Drawing.Color outputColour = pane.SelectionColor;
        Check(pane.Font.Name == "Consolas" && promptColour.ToArgb() == System.Drawing.Color.FromArgb(0, 0, 160).ToArgb() && outputColour.ToArgb() == pane.ForeColor.ToArgb(),
              "the output pane is monospace, with R's prompts and commands in their own colour and R's output in the pane's");
        // Open in R: the script as an R file named after the window, for RStudio when it is installed
        MethodInfo fileName = script.GetMethod("ScriptFileName", BindingFlags.Static | BindingFlags.NonPublic);
        string rstudio = RController.FindRStudio();
        Check((string)fileName.Invoke(null, new object[] { "Script 1: Paired t test" }) == "Script 1_ Paired t test.R" && (string)fileName.Invoke(null, new object[] { "" }) == "script.R"
              && (rstudio == null || File.Exists(rstudio)),
              "the script's file for R is named after the window with the characters a file name cannot hold replaced, and RStudio is found only where it is: " + (rstudio ?? "not installed"));
        // the print preview inside the window, as the report's, in place of a dialog: Print shows it in front of the script, Close returns
        MethodInfo print = script.GetMethod("Print", BindingFlags.Instance | BindingFlags.NonPublic);
        PropertyInfo previewShown = script.GetProperty("PreviewShown", BindingFlags.Instance | BindingFlags.NonPublic);
        print.Invoke(first.window, null);
        Check((bool)previewShown.GetValue(first.window) && first.window.Controls[0] is Panel front && front.Controls.OfType<PrintPreviewControl>().Any(c => c.Document == document),
              "Print shows the preview inside the window, in front of the script, with the script's print document");
        script.GetMethod("HidePreview", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(first.window, null);
        Check(!(bool)previewShown.GetValue(first.window), "Close returns to the script");
        // Find and Replace as a bar at the top of the window, hidden until asked: the search goes on from the selection and round to the start
        PropertyInfo findBarShown = script.GetProperty("FindBarShown", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo findNext = script.GetMethod("FindNext", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(string), typeof(bool) }, null);
        MethodInfo replaceAll = script.GetMethod("ReplaceAll", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(string), typeof(string), typeof(bool) }, null);
        editor.Text = "alpha beta Alpha";
        editor.Select(0, 0);
        bool one = (bool)findNext.Invoke(first.window, new object[] { "alpha", false }); int at1 = editor.SelectionStart;
        bool two = (bool)findNext.Invoke(first.window, new object[] { "alpha", false }); int at2 = editor.SelectionStart;
        bool three = (bool)findNext.Invoke(first.window, new object[] { "alpha", false }); int at3 = editor.SelectionStart;
        bool none = (bool)findNext.Invoke(first.window, new object[] { "gamma", false });
        bool cased = (bool)findNext.Invoke(first.window, new object[] { "Alpha", true }); int at4 = editor.SelectionStart;
        Check(!(bool)findBarShown.GetValue(first.window) && one && at1 == 0 && two && at2 == 11 && three && at3 == 0 && !none && cased && at4 == 11 && editor.SelectionLength == 5,
              "the find bar is hidden until asked, and Find next goes on from the selection, round to the start, matching case only when asked: " + at1 + ", " + at2 + ", " + at3 + ", " + at4);
        int replaced = (int)replaceAll.Invoke(first.window, new object[] { "alpha", "x", false });
        Check(replaced == 2 && editor.Text == "x beta x", "Replace all replaces every match: " + editor.Text);
        // the find bar sits in the docking order after the toolbar and before the script, so it pushes the script down rather than covering it
        Control bar = (Control)script.GetField("findBar", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(first.window);
        Control strip = (Control)script.GetField("ToolStrip1", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(first.window);
        Control body = (Control)script.GetField("splitContainer1", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(first.window);
        int barAt = first.window.Controls.GetChildIndex(bar), stripAt = first.window.Controls.GetChildIndex(strip), bodyAt = first.window.Controls.GetChildIndex(body);
        Check(bar.Dock == DockStyle.Top && bodyAt < barAt && barAt < stripAt, $"the find bar is docked at the top between the toolbar and the script: body {bodyAt}, bar {barAt}, toolbar {stripAt}");
        // the main window's top panel: the gap, the tabs and the toolbar's height only while the toolbar shows
        MethodInfo topPanel = program.GetType("StatsDirect.UI.frmMain").GetMethod("TopPanelHeight", BindingFlags.Static | BindingFlags.NonPublic);
        Check((int)topPanel.Invoke(null, new object[] { 6, 25, 30, true }) == 61 && (int)topPanel.Invoke(null, new object[] { 6, 25, 30, false }) == 31,
              "the panel above the windows gives up the toolbar's height while the toolbar is hidden");
        // Only worksheets use the main toolbar; every script/report owns one bar.
        MethodInfo toolbarShown = program.GetType("StatsDirect.UI.frmMain").GetMethod("ToolbarShownFor", BindingFlags.Static | BindingFlags.NonPublic);
        using Form plain = new();
        Check(!(bool)toolbarShown.Invoke(null, new object[] { first.window }) && !(bool)toolbarShown.Invoke(null, new object[] { plain }) && !(bool)toolbarShown.Invoke(null, new object[] { null }),
              "the main toolbar stays hidden for scripts, non-document windows and no document");
        using ReportStub report = new();
        Check(!(bool)toolbarShown.Invoke(null, new object[] { report }), "a report's own web toolbar has no second native toolbar above it");
        first.window.Dispose();
        second.window.Dispose();
    }

    /// <summary>A report as the main window knows one: the interface the report window implements, with nothing behind it.</summary>
    private sealed class ReportStub : Form, IReport
    {
        public void AppendRenderable(StatsDirect.Templates.IRenderable renderable, int helpContextId, StatsDirect.Templates.Operation operation, object run) { }
        public void EnsureActive() { }
        public string Id() => "stub";
        public bool SelectPane(Pane pane) => false;
        public Pane SelectedPane => null;
    }
}
