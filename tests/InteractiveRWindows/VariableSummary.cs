using StatsDirect.Data;
using StatsDirect.Templates;
using StatsDirect.UI;

internal static partial class Program
{
    static async Task CheckVariableSummary(frmMain main)
    {
        var operationField = typeof(frmMain).GetField("mostRecentOperation", Members)!;
        object? previousOperation = operationField.GetValue(main), previousPanel = Get(main, "currentPanelType");
        var panel = (Panel)Get(main, "pnlVariables")!;
        var label = (Label)Get(main, "lblSelectedVariables")!;
        var user = (Panel)Get(main, "pnlUser")!;
        var confidence = (Panel)Get(main, "pnlConfidenceInterval")!;
        var ci = (ComboBox)Get(main, "cboConfidenceInterval")!;
        var instructions = (Panel)Get(main, "pnlFollowOnInstructions")!;
        string oldText = label.Text, oldDescription = label.AccessibleDescription ?? "", oldCi = ci.Text;
        bool oldPanelVisible = panel.Visible, oldConfidence = confidence.Visible, oldInstructions = instructions.Visible;
        bool hadTable = (bool)Call(main, "HasUserInputTable")!;
        var table = hadTable ? (TableLayoutPanel)Call(main, "GetUserInputTable")! : (TableLayoutPanel)Call(main, "CreateUserInputTable")!;
        if (!hadTable) user.Controls.Add(table);
        var options = (TableLayoutPanel)table.Controls[0];
        var option = new CheckBox { AutoSize = true, Text = "Keep chart settings", Checked = true };
        options.Controls.Add(option);
        try
        {
            var operation = new Operation { Name = "summary-test" };
            var parameters = new ParametersStep(); parameters.Parameters.Add(new FrameParameter { Name = "data", Title = "Samples" });
            operation.Steps.Add(parameters); operationField.SetValue(main, operation);
            var frame = new DataFrame();
            frame.Variables.Add(new DoubleVariable(new[] { 1.0, 2.0 }, "PEFR Before"));
            frame.Variables.Add(new DoubleVariable(new[] { 3.0, 4.0 }, "PEFR After"));
            var context = new ParameterBag(); context.AddInput("data", frame);
            Call(main, "MaybeShowVariables", context);
            var panelType = typeof(frmMain).GetNestedType("PanelType", System.Reflection.BindingFlags.NonPublic)!;
            confidence.Visible = instructions.Visible = true; ci.Text = "95";
            Call(main, "ShowPanel", Enum.Parse(panelType, "Operations"), true);
            main.PerformLayout(); table.PerformLayout(); await Task.Yield();
            Check(panel.Visible && label.Text.StartsWith("Selected variables (2)", StringComparison.Ordinal)
                && label.Text.Contains("Samples", StringComparison.Ordinal) && label.Text.Contains("PEFR Before", StringComparison.Ordinal)
                && label.Text.Contains("PEFR After", StringComparison.Ordinal),
                "selected variable names and count are visible above operation options without hovering over an icon");
            Check(typeof(frmMain).GetField("cmdVariables", Members) == null && typeof(frmMain).GetField("tipVariables", Members) == null
                && panel.Controls.Count == 1 && ReferenceEquals(panel.Controls[0], label),
                "the selected-variable summary replaces the trolley button and its tooltip");
            Check(label.AccessibleName == "Selected variables" && label.AccessibleDescription == label.Text && !label.UseMnemonic,
                "selected names are accessible as text and ampersands are shown literally");

            var many = new DataFrame();
            for (int i = 0; i < 30; i++) many.Variables.Add(new DoubleVariable(new[] { (double)i }, "A & B " + i + " " + new string('w', 300)));
            var manyContext = new ParameterBag(); manyContext.AddInput("data", many);
            Call(main, "MaybeShowVariables", manyContext); main.PerformLayout(); panel.PerformLayout();
            await Wait(() => panel.VerticalScroll.Visible, "The long variable summary did not offer vertical scrolling.");
            Check(label.Text.Contains("Selected variables (30)", StringComparison.Ordinal) && label.Text.Contains("A & B", StringComparison.Ordinal)
                && label.Width <= panel.ClientSize.Width + 1 && label.PreferredHeight > panel.Height,
                "many long variable names wrap within the summary width and retain literal ampersands");
            Check(panel.Height <= label.Font.Height * 6 + label.Padding.Vertical && panel.VerticalScroll.Visible && !panel.HorizontalScroll.Visible,
                "the long summary is bounded to six font lines with vertical rather than horizontal scrolling");
            var operations = (Panel)Get(main, "pnlOperations")!;
            int bottom = operations.RectangleToScreen(operations.ClientRectangle).Bottom;
            Check(confidence.Visible && ci.Text == "95" && option.Visible && option.Checked && instructions.Visible
                && confidence.RectangleToScreen(confidence.ClientRectangle).Bottom <= bottom
                && option.RectangleToScreen(option.ClientRectangle).Bottom <= bottom
                && instructions.RectangleToScreen(instructions.ClientRectangle).Bottom <= bottom,
                "the variable summary leaves confidence, options and follow-on guidance visible and unchanged");
            Call(main, "MaybeShowVariables", new ParameterBag());
            Check(!panel.Visible && label.Text == "", "an empty input context hides the selected-variable summary");
        }
        finally
        {
            options.Controls.Remove(option); option.Dispose();
            if (!hadTable) { user.Controls.Remove(table); table.Dispose(); }
            operationField.SetValue(main, previousOperation);
            label.Text = oldText; label.AccessibleDescription = oldDescription; panel.Visible = oldPanelVisible;
            confidence.Visible = oldConfidence; instructions.Visible = oldInstructions; ci.Text = oldCi;
            if (previousPanel != null) Call(main, "ShowPanel", previousPanel, false);
        }
    }
}
