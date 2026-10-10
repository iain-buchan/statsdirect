using System.Reflection;
using StatsDirect.Calculator;

internal static partial class Program
{
    static void Menu(CalculatorView view, string text) => view.EditMenu.DropDownItems.Cast<ToolStripMenuItem>().Single(i => i.Text == text).PerformClick();
    static void Shortcut(ExpressionEditor input, Keys keys)
    {
        var message = Message.Create(input.Handle, 0x100, (IntPtr)(keys & Keys.KeyCode), IntPtr.Zero);
        bool handled = (bool)typeof(ExpressionEditor).GetMethod("ProcessCmdKey", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(input, new object[] { message, keys })!;
        Check(handled, "expression editor handles " + keys + " before MDI document shortcuts");
    }
    static async Task EditingChecks(Form host)
    {
        using var calc = new CalculatorService();
        calc.Show(host, () => { });
        var view = calc.View;
        var input = view.Expression;
        Check(host.ActiveMdiChild == null && view.EditMenu.Available, "calculator Edit menu is available without a worksheet or report");
        input.Text = "8"; input.Select(1, 0); input.ClearUndo();
        input.ReplaceSelection(" + 2");
        view.EvaluateCurrent();
        Menu(view, "Undo");
        Check(input.Text == "8" && view.Result.Text == "", "Edit > Undo restores the expression and clears its obsolete result");
        Menu(view, "Redo");
        Check(input.Text == "8 + 2", "Edit > Redo reapplies the expression edit");
        input.Select(input.TextLength, 0); input.ReplaceSelection(" + 3");
        Shortcut(input, Keys.Control | Keys.Z);
        Check(input.Text == "8 + 2", "Ctrl+Z uses the same history as Edit > Undo");
        Shortcut(input, Keys.Control | Keys.Y);
        Check(input.Text == "8 + 2 + 3", "Ctrl+Y redoes the last edit");
        Menu(view, "Undo"); Menu(view, "Undo");
        Check(input.Text == "8", "independent changes undo in chronological order");
        Menu(view, "Redo"); Menu(view, "Redo");
        view.SaveCalculation();
        input.Text = "42"; input.Select(1, 1); input.ClearUndo();
        view.Saved.SelectedIndex = 0;
        Check(input.Text == "42" && input.SelectionStart == 1 && input.SelectionLength == 1, "selecting a saved calculation does not replace or reselect the input");
        view.Recall();
        Check(input.Text == "8 + 2 + 3" && view.Result.Text == "13", "Recall restores the saved expression and result");
        Menu(view, "Undo");
        Check(input.Text == "42" && view.Result.Text == "", "Recall is one undoable replacement and its result cannot become stale");
        Menu(view, "Redo");
        Check(input.Text == "8 + 2 + 3", "Recall can be redone");
        input.Text = "2 * ()"; input.Select(5, 0); input.ClearUndo();
        view.InsertSaved();
        Menu(view, "Undo");
        Check(input.Text == "2 * ()", "Insert is one undoable replacement");
        Menu(view, "Redo");
        Check(input.Text == "2 * (8 + 2 + 3)", "Insert can be redone");
        input.Select(5, 3);
        IntPtr inputHandle = input.Handle;
        await calc.Session.ToggleModeAsync();
        calc.Session.Window.Location = new Point(-16000, -16000);
        Check(input.Handle == inputHandle && input.SelectionStart == 5 && input.SelectionLength == 3, "floating keeps the native editor handle and selection");
        Menu(view, "Undo");
        Check(input.Text == "2 * ()", "floating retains history created while docked");
        await calc.Session.ToggleModeAsync();
        Menu(view, "Redo");
        Check(input.Text == "2 * (8 + 2 + 3)", "redocking retains the redo history");
        Menu(view, "Undo");
        input.Select(5, 0); input.ReplaceSelection("4");
        Check(!input.CanRedo && input.Text == "2 * (4)", "a new edit discards the abandoned redo branch");
        calc.Session.Hide(); calc.Show(host, () => { });
        Menu(view, "Undo");
        Check(input.Text == "2 * ()", "closing and reopening the pane retains edit history");
        view.Result.Text = "result"; view.Result.Focus(); view.Result.SelectAll();
        Menu(view, "Undo");
        Check(input.Text == "2 * ()" && view.Result.Text == "result", "Undo on the read-only result cannot change the expression");
        using var document = new Form { MdiParent = host };
        var field = new TextBox { Dock = DockStyle.Fill }; document.Controls.Add(field); document.Show(); field.Focus();
        Check(!view.TryEdit("undo"), "calculator releases edit commands when focus returns to a document");
    }
}
