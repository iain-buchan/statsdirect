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
            showR.Invoke(window, new object[] { text, title, topic });
            return (window, tab);
        }
        (Form window, TabPage tab) first = Open("Paired t test", "1239");
        (Form window, TabPage tab) second = Open("Unpaired t test");
        // the toolbar: Save, then Cut, Copy and Paste with the Edit menu's icons, then Find, then the R tools
        ToolStrip toolbar = (ToolStrip)script.GetField("ToolStrip1", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(first.window);
        ToolStripButton[] buttons = toolbar.Items.OfType<ToolStripButton>().ToArray();
        Check(string.Join("|", buttons.Take(9).Select(b => b.Text)) == "New script|Open|Save|Print|Cut|Copy|Paste|Find|Help" && buttons.Take(9).All(b => b.Image != null), "the toolbar is in the main window's order, New, Open, Save, Print, Cut, Copy, Paste, then Find and Help, each with an icon: " + string.Join(", ", buttons.Select(b => b.Text)));
        // the toolbar is drawn as the main window's: the same renderer paints a symbol for each button by its name at the display size, the
        // bitmaps serving only to size the buttons, so the script window's buttons take the names the renderer knows, and it must draw each
        Type renderer = program.GetType("StatsDirect.UI.SharpToolbarRenderer");
        Check(toolbar.Renderer.GetType() == renderer && toolbar.ImageScalingSize == toolbar.LogicalToDeviceUnits(new System.Drawing.Size(20, 20)),
              "the script window's toolbar has the main window's renderer and icon size");
        Check(string.Join("|", buttons.Take(9).Select(b => b.Name)) == "newToolStripButton|openToolStripButton|saveToolStripButton|printToolStripButton|cutToolStripButton|copyToolStripButton|pasteToolStripButton|findToolStripButton|helpToolStripButton",
              "the nine buttons are named as the renderer draws them");
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
        string[] inks = buttons.Take(9).Select(b => b.Text + " " + Ink(b)).ToArray();
        Check(buttons.Take(9).All(b => Ink(b) > 40) && Ink(new ToolStripButton("Nothing") { Name = "noSuchButton" }) == 0, "the renderer draws a symbol for each of the nine, and none for a name it does not know: " + string.Join(", ", inks));
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
        // the main window's toolbar is the report's and the worksheet's: it goes while a script window, which has its own, is active
        MethodInfo toolbarShown = program.GetType("StatsDirect.UI.frmMain").GetMethod("ToolbarShownFor", BindingFlags.Static | BindingFlags.NonPublic);
        using Form plain = new();
        Check(!(bool)toolbarShown.Invoke(null, new object[] { first.window }) && (bool)toolbarShown.Invoke(null, new object[] { plain }) && (bool)toolbarShown.Invoke(null, new object[] { null }),
              "the main toolbar hides while a script window is active and shows for any other window, or none");
        first.window.Dispose();
        second.window.Dispose();
    }
}
