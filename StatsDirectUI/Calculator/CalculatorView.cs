using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using StatsDirect.Builtins;
using StatsDirect.Expressions;
using StatsDirect.Numerics;
using StatsDirect.Utilities;

namespace StatsDirect.Calculator;

/// <summary>A live calculator session, independent of the window that displays it.</summary>
internal sealed class CalculatorView : UserControl
{
    internal TextBox Expression { get; } = new() { Name = "Expression", AccessibleName = "Expression to evaluate", Multiline = true, WordWrap = true, AcceptsReturn = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill };
    internal TextBox Result { get; } = new() { Name = "Result", AccessibleName = "Calculator result", Multiline = true, WordWrap = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill };
    internal ListBox Saved { get; } = new() { Name = "SavedExpressions", AccessibleName = "Saved calculations", Dock = DockStyle.Fill, HorizontalScrollbar = true, IntegralHeight = false };
    internal string LastError { get; private set; }
    internal event Action ModeRequested, CloseRequested;
    private readonly Action help;
    private readonly Action<string> copy;
    private readonly ToolStripButton mode = new("Pop out") { Alignment = ToolStripItemAlignment.Right };
    private readonly Label status = new() { AutoSize = true, ForeColor = Color.DarkRed, Visible = false };
    private readonly TableLayoutPanel columns;
    private readonly Panel content;
    private readonly TableLayoutPanel savedPanel;
    private readonly RowStyle resultRow;
    private bool stacked;
    private bool arranging;

    internal sealed record Calculation(string Expression, string Result)
    {
        public override string ToString() => Expression.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ") + " = " + Result;
    }

    internal CalculatorView(Action help, Action<string> copy = null)
    {
        this.help = help;
        this.copy = copy ?? Clipboard.SetText;
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Arial", 10);
        Dock = DockStyle.Fill;
        var heading = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Dock = DockStyle.Top };
        heading.Items.AddRange([new ToolStripLabel("Calculator"), new ToolStripButton("Close", null, (_, _) => CloseRequested?.Invoke()) { Alignment = ToolStripItemAlignment.Right }, mode]);
        mode.Click += (_, _) => ModeRequested?.Invoke();
        content = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        columns = new TableLayoutPanel { ColumnCount = 2, RowCount = 2, Padding = new Padding(8) };
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        columns.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        columns.RowStyles.Add(new RowStyle(SizeType.Percent, 0));
        var input = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Margin = new Padding(0, 0, 8, 0) };
        input.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        input.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        input.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        input.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        resultRow = new RowStyle(SizeType.Absolute, 42);
        input.RowStyles.Add(resultRow);
        input.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        input.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        input.Controls.Add(new Label { Text = "Expression (Enter calculates; Shift+Enter adds a line):", AutoSize = true }, 0, 0);
        input.Controls.Add(Expression, 0, 1);
        input.Controls.Add(new Label { Text = "Result:", AutoSize = true }, 0, 2);
        input.Controls.Add(Result, 0, 3);
        input.Controls.Add(Commands(("&Calculate", () => EvaluateCurrent()), ("&Save", SaveCalculation), ("Copy result", CopyResult)), 0, 4);
        input.Controls.Add(status, 0, 5);
        input.SizeChanged += (_, _) => status.MaximumSize = new Size(Math.Max(100, input.Width - 12), 0);
        savedPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty };
        savedPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        savedPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        savedPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        savedPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        savedPanel.Controls.Add(new Label { Text = "Saved calculations:", AutoSize = true }, 0, 0);
        savedPanel.Controls.Add(Saved, 0, 1);
        savedPanel.Controls.Add(Commands(("Recall", Recall), ("Insert", InsertSaved), ("Remove", RemoveSaved), ("Copy saved", CopySaved), ("Help", () => help?.Invoke())), 0, 2);
        Saved.DoubleClick += (_, _) => Recall();
        columns.Controls.Add(input, 0, 0);
        columns.Controls.Add(savedPanel, 1, 0);
        content.Controls.Add(columns);
        Controls.Add(content);
        Controls.Add(heading);
        Expression.TextChanged += (_, _) => { Result.Clear(); ShowNotice(null); };
        Expression.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter && !e.Shift)
            {
                e.Handled = e.SuppressKeyPress = true;
                EvaluateCurrent();
            }
        };
        SizeChanged += (_, _) => LayoutColumns();
        content.SizeChanged += (_, _) => LayoutColumns();
    }

    private void LayoutColumns()
    {
        if (columns == null || resultRow == null || arranging) return;
        arranging = true;
        try
        {
            float scale = DeviceDpi / 96f;
            bool narrow = Width < 650 * scale;
            resultRow.Height = Math.Max(42 * scale, Result.Font.Height * 2 + 12 * scale);
            columns.SuspendLayout();
            if (narrow != stacked)
            {
                stacked = narrow;
                columns.ColumnStyles[0].Width = narrow ? 100 : 65;
                columns.ColumnStyles[1].Width = narrow ? 0 : 35;
                columns.RowStyles[0].Height = narrow ? 65 : 100;
                columns.RowStyles[1].Height = narrow ? 35 : 0;
                columns.SetCellPosition(savedPanel, narrow ? new TableLayoutPanelCellPosition(0, 1) : new TableLayoutPanelCellPosition(1, 0));
            }
            // Size the contents explicitly: a scrolling TableLayoutPanel with
            // Fill children can retain its old scroll extent after widening.
            int height = Math.Max(content.ClientSize.Height, (int)((narrow ? 480 : 240) * scale));
            columns.Bounds = new Rectangle(content.AutoScrollPosition, new Size(content.ClientSize.Width, height));
            columns.ResumeLayout(true);
        }
        finally { arranging = false; }
    }
    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        LayoutColumns();
    }
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        // Parent autoscaling may run after the initial SizeChanged event.
        // Reapply dimensions in device pixels once that first scaling is done.
        LayoutColumns();
    }
    private static FlowLayoutPanel Commands(params (string Name, Action Run)[] commands)
    {
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Margin = Padding.Empty };
        foreach (var command in commands)
        {
            var button = new Button { Text = command.Name, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
            button.Click += (_, _) => command.Run();
            panel.Controls.Add(button);
        }
        return panel;
    }
    internal void SetPresentation(bool floating, bool canDock = true)
    {
        mode.Text = floating ? "Dock" : "Pop out";
        mode.Enabled = !floating || canDock;
        mode.ToolTipText = floating && !canDock ? "Calculator will return when the parameter dialog closes." : floating ? "Dock below the workspace" : "Open Calculator in a separate window";
    }
    internal void SetStandalone() => mode.Visible = false;
    internal void FocusExpression() => Expression.Focus();
    internal void ShowNotice(string message) { LastError = message; status.Text = message; status.Visible = !string.IsNullOrEmpty(message); }
    internal bool EvaluateCurrent()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(Expression.Text)) throw new ArgumentException("Please enter an expression first.");
            var calculation = new Calcit(Expression.Text, Array.Empty<DataType>(), false);
            object value = calculation.EvaluateObject<object>(null);
            Result.Text = value is double number && number == Constant.MISSING ? Formatting.ERRR : value?.ToString() ?? "";
            ShowNotice(null);
            return true;
        }
        catch (Exception ex)
        {
            Result.Clear();
            string message = ex.GetBaseException().Message;
            ShowNotice("Couldn't evaluate expression: " + (message.Length > 300 ? message[..300] + "…" : message));
            return false;
        }
        finally { FocusExpression(); }
    }
    internal void SaveCalculation()
    {
        // A failed calculation must not save the previous expression's result.
        if (EvaluateCurrent()) Saved.Items.Add(new Calculation(Expression.Text, Result.Text));
    }
    internal void Recall()
    {
        if (Saved.SelectedItem is not Calculation item) return;
        Expression.Text = item.Expression;
        Result.Text = item.Result;
        FocusExpression();
    }
    internal void RemoveSaved() { if (Saved.SelectedIndex >= 0) Saved.Items.RemoveAt(Saved.SelectedIndex); }
    internal void InsertSaved()
    {
        if (Saved.SelectedItem is not Calculation item) return;
        Expression.SelectedText = item.Expression;
        FocusExpression();
    }
    internal void CopyResult() { if (Result.Text.Length > 0) Copy(Result.Text); }
    internal void CopySaved() { if (Saved.Items.Count > 0) Copy(string.Join(Environment.NewLine, Saved.Items.Cast<Calculation>())); }
    private void Copy(string text)
    {
        try { copy(text); }
        catch (Exception ex) { ShowNotice("Could not copy: " + ex.Message); }
    }
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.F1) { help?.Invoke(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }
}
