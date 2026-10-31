using StatsDirect.UI;

internal static partial class Program
{
    static IEnumerable<ToolStripItem> MenuDescendants(ToolStripItemCollection items)
    {
        foreach (ToolStripItem item in items)
        {
            yield return item;
            if (item is ToolStripMenuItem menu)
                foreach (var child in MenuDescendants(menu.DropDownItems)) yield return child;
        }
    }

    static async Task CheckMenuSimplification(frmMain main, object app)
    {
        // The fixture excludes normal Load to avoid changing saved settings.
        // Load the real operation menus explicitly, including their command handlers.
        Call(main, "AddTemplates");
        var menu = (MenuStrip)Field(main, "mnuMain");
        Check(menu.Items.OfType<ToolStripMenuItem>().Where(item => item.Available)
            .Select(item => item.Text.Replace("&", ""))
            .SequenceEqual(new[] { "File", "Data", "Analysis", "Graphics", "Window", "Help" }),
            "operation menus retain File, Data, Analysis, Graphics, Window, Help order without Tools");
        var descendants = MenuDescendants(menu.Items).ToArray();
        Check(!descendants.Any(item => new[] { "Tools", "Notepad", "R", "Setup Tools..." }
            .Contains(item.Text.Replace("&", ""))),
            "no Tools menu or external Notepad, R GUI or Setup Tools launchers remain");
        Check(descendants.Count(item => item.Name == "checkForUpdatesToolStripMenuItem") == 1,
            "update checking has one menu entry, retained under Help");
        var analysis = menu.Items.OfType<ToolStripMenuItem>().Single(item => item.Text.Replace("&", "") == "Analysis");
        var calculator = (ToolStripMenuItem)analysis.DropDownItems["calculatorMenuItem"]!;
        int at = analysis.DropDownItems.IndexOf(calculator);
        Check(at > 0 && analysis.DropDownItems[at - 1] is ToolStripSeparator
            && at + 2 == analysis.DropDownItems.Count - 1 && analysis.DropDownItems[at + 1] is ToolStripSeparator
            && analysis.DropDownItems[at + 2].Text.Replace("&", "") == "Options",
            "Calculator has its own Analysis section immediately before the final Options section");
        Check(main.ActiveMdiChild == null && analysis.Enabled && calculator.Enabled,
            "Analysis > Calculator is available without an open worksheet or document");
        await CheckCalculatorMenuForDocument(main, app, null, "no document");
    }

    static async Task CheckCalculatorMenuForDocument(frmMain main, object app, Form document, string context)
    {
        var menu = (MenuStrip)Field(main, "mnuMain");
        var analysis = menu.Items.OfType<ToolStripMenuItem>().Single(item => item.Text.Replace("&", "") == "Analysis");
        var calculator = (ToolStripMenuItem)analysis.DropDownItems["calculatorMenuItem"]!;
        var service = Field(app, "calculator");
        var session = Get(service, "Session");
        var view = (Control)Get(app, "CalculatorView");
        var input = (RichTextBox)Get(view, "Expression");
        input.Text = "7 + 4"; input.Select(2, 3);
        Check((bool)Call(view, "EvaluateCurrent"), "calculator evaluates before the " + context + " menu check");
        Call(session, "Hide");
        document?.Activate();
        await Task.Delay(100);
        Check(main.ActiveMdiChild == document && analysis.Enabled && calculator.Enabled,
            "Analysis > Calculator remains available with " + context);
        var documents = main.MdiChildren.ToHashSet();
        var panel = Field(main, "currentPanelType");
        calculator.PerformClick();
        var until = DateTime.UtcNow.AddSeconds(5);
        while (!(bool)Get(session, "IsVisible") && DateTime.UtcNow < until) await Task.Delay(30);
        Check((bool)Get(session, "IsVisible") && ReferenceEquals(Get(app, "CalculatorView"), view)
            && ReferenceEquals(Get(service, "Session"), session) && input.Focused,
            "Analysis > Calculator reopens the same live pane and focuses input with " + context);
        Check(input.Text == "7 + 4" && input.SelectionStart == 2 && input.SelectionLength == 3
            && ((TextBox)Get(view, "Result")).Text == "11",
            "menu reopening preserves the calculator expression, selection and result with " + context);
        Check(main.ActiveMdiChild == document && documents.SetEquals(main.MdiChildren)
            && Equals(Field(main, "currentPanelType"), panel) && !(bool)Get(main, "InOperation"),
            "Calculator preserves the active document and starts no analysis or new document with " + context);
    }
}
