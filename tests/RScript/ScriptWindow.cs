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
        (Form window, TabPage tab) Open(string title)
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
            showR.Invoke(window, new object[] { "x <- 1", title });
            return (window, tab);
        }
        (Form window, TabPage tab) first = Open("Paired t test");
        (Form window, TabPage tab) second = Open("Unpaired t test");
        // the toolbar: Save, then Cut, Copy and Paste with the Edit menu's icons, then Find, then the R tools
        ToolStrip toolbar = (ToolStrip)script.GetField("ToolStrip1", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(first.window);
        ToolStripButton[] buttons = toolbar.Items.OfType<ToolStripButton>().ToArray();
        Check(string.Join("|", buttons.Take(5).Select(b => b.Text)) == "Save|Cut|Copy|Paste|Find" && buttons.Take(5).All(b => b.Image != null), "the toolbar has Cut, Copy and Paste between Save and Find, each with an icon: " + string.Join(", ", buttons.Select(b => b.Text)));
        // Save, Cut, Copy and Paste wear the main window's toolbar icons, so that the script window matches the other windows
        System.ComponentModel.ComponentResourceManager main = new(program.GetType("StatsDirect.UI.frmMain"));
        bool SamePixels(System.Drawing.Image x, System.Drawing.Image y)
        {
            using System.Drawing.Bitmap a = new(x), b = new(y);
            if (a.Size != b.Size) return false;
            for (int i = 0; i < a.Width; i++) for (int j = 0; j < a.Height; j++) if (a.GetPixel(i, j) != b.GetPixel(i, j)) return false;
            return true;
        }
        List<string> differ = new();
        foreach ((string text, string resource) in new[] { ("Save", "saveToolStripButton.Image"), ("Cut", "cutToolStripButton.Image"), ("Copy", "copyToolStripButton.Image"), ("Paste", "pasteToolStripButton.Image") })
        {
            System.Drawing.Image ours = buttons.First(b => b.Text == text).Image, theirs = (System.Drawing.Image)main.GetObject(resource);
            if (theirs == null || !SamePixels(ours, theirs)) differ.Add($"{text} ({ours.Width}x{ours.Height} against {(theirs == null ? "no resource" : theirs.Width + "x" + theirs.Height)})");
        }
        if (differ.Count > 0) Console.WriteLine("      not the main window's icon: " + string.Join(", ", differ));
        Check(differ.Count == 0, "Save, Cut, Copy and Paste on the script window's toolbar are the main window's icons");
        Check(first.window.Text == "Script 1: Paired t test" && first.tab.Text == first.window.Text, "the first script window is Script 1 with the analysis after it, and its tab is named with it: " + first.tab.Text);
        Check(second.window.Text == "Script 2: Unpaired t test" && second.tab.Text == second.window.Text, "a second Continue in R opens Script 2: " + second.tab.Text);
        Check((int)nextNumber.Invoke(app, null) == 3, "the next script window would be Script 3");
        first.window.Dispose();
        second.window.Dispose();
    }
}
