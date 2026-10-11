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
        Check(string.Join("|", buttons.Take(5).Select(b => b.Text)) == "Save|Cut|Copy|Paste|Find" && buttons.Take(5).All(b => b.Image != null), "the toolbar has Cut, Copy and Paste between Save and Find, each with an icon: " + string.Join(", ", buttons.Select(b => b.Text)));
        // the toolbar is drawn as the main window's: the same renderer paints a symbol for each button by its name at the display size, the
        // bitmaps serving only to size the buttons, so the script window's buttons take the names the renderer knows, and it must draw each
        Type renderer = program.GetType("StatsDirect.UI.SharpToolbarRenderer");
        Check(toolbar.Renderer.GetType() == renderer && toolbar.ImageScalingSize == toolbar.LogicalToDeviceUnits(new System.Drawing.Size(20, 20)),
              "the script window's toolbar has the main window's renderer and icon size");
        Check(string.Join("|", buttons.Take(5).Select(b => b.Name)) == "saveToolStripButton|cutToolStripButton|copyToolStripButton|pasteToolStripButton|findToolStripButton",
              "Save, Cut, Copy, Paste and Find are named as the renderer draws them");
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
        string[] inks = buttons.Take(5).Select(b => b.Text + " " + Ink(b)).ToArray();
        Check(buttons.Take(5).All(b => Ink(b) > 40) && Ink(new ToolStripButton("Nothing") { Name = "noSuchButton" }) == 0, "the renderer draws a symbol for each of the five, and none for a name it does not know: " + string.Join(", ", inks));
        Check(first.window.Text == "Script 1: Paired t test" && first.tab.Text == first.window.Text, "the first script window is Script 1 with the analysis after it, and its tab is named with it: " + first.tab.Text);
        Check(second.window.Text == "Script 2: Unpaired t test" && second.tab.Text == second.window.Text, "a second Continue in R opens Script 2: " + second.tab.Text);
        Check((int)nextNumber.Invoke(app, null) == 3, "the next script window would be Script 3");
        // Help: the result's topic when the script continues one; the window's own topic (1401) for a script of no result or an empty window
        (Form window, TabPage tab) empty = Open("Nothing yet", "1239", "");
        Check((string)helpTopic.GetValue(first.window) == "1239" && (string)helpTopic.GetValue(second.window) == "1401" && (string)helpTopic.GetValue(empty.window) == "1401",
              "Help opens the result's topic for its script, and the R script window's own topic for a script of no result or an empty window");
        empty.window.Dispose();
        first.window.Dispose();
        second.window.Dispose();
    }
}
