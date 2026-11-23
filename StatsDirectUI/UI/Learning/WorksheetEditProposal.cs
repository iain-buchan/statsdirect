using SpreadsheetGear;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace StatsDirect.UI.Learning;

internal sealed record WorksheetEditTarget(frmSpreadsheetGear Grid, IWorkbook Workbook, IWorksheet Worksheet, string SheetName, int Version)
{
    internal WorksheetEditProposal Propose(JsonObject arguments) => Grid.StageWorksheetEdit(this, WorksheetEditRequest.Parse(arguments));
}

internal sealed record WorksheetEditRequest(string Address, int Top, int Left, int Bottom, int Right, object[,] Values, string Description)
{
    private const int MaximumCells = 4000, MaximumCellText = 2048, MaximumTotalText = 128000;
    private static readonly Regex RangePattern = new(@"\A([A-Z]{1,3})([1-9][0-9]{0,6})(?::([A-Z]{1,3})([1-9][0-9]{0,6}))?\z",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    internal static WorksheetEditRequest Parse(JsonObject arguments)
    {
        if (arguments == null || arguments.Any(a => a.Key is not ("range" or "description" or "values"))
            || arguments["range"] is not JsonValue range || !range.TryGetValue(out string address) || address.Length > 24)
            throw new ArgumentException("Specify a cell or rectangular range on the current worksheet, such as B2:C8.");

        address = address.Trim().ToUpperInvariant();
        var match = RangePattern.Match(address);
        if (!match.Success)
            throw new ArgumentException("Specify a range on the current worksheet in A1 notation, without a sheet name.");

        int top = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) - 1;
        int left = Column(match.Groups[1].Value);
        int bottom = match.Groups[4].Success ? int.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture) - 1 : top;
        int right = match.Groups[3].Success ? Column(match.Groups[3].Value) : left;
        if (bottom < top || right < left || bottom >= 1048576 || right >= 16384)
            throw new ArgumentException("The suggested range is outside the worksheet or its endpoints are reversed.");

        int height = bottom - top + 1, width = right - left + 1;
        if ((long)height * width > MaximumCells)
            throw new ArgumentException("An edit proposal can cover up to 4,000 cells. Ask for a smaller range.");
        if (arguments["values"] is not JsonArray rows || rows.Count != height)
            throw new ArgumentException("The number of proposed rows must match the range.");

        var values = new object[height, width];
        int textLength = 0;
        for (int row = 0; row < height; row++)
        {
            if (rows[row] is not JsonArray cells || cells.Count != width)
                throw new ArgumentException("Every proposed row must have the same number of columns as the range.");
            for (int column = 0; column < width; column++)
            {
                values[row, column] = ReadLiteral(cells[column]);
                if (values[row, column] is string text) textLength += text.Length;
                if (textLength > MaximumTotalText)
                    throw new ArgumentException("The proposed text is too long. Ask for a smaller edit.");
            }
        }

        string description = "";
        if (arguments["description"] is JsonNode descriptionNode)
        {
            if (descriptionNode is not JsonValue summary || !summary.TryGetValue(out string proposedDescription) || proposedDescription.Length > 200)
                throw new ArgumentException("An edit description may contain up to 200 characters.");
            description = proposedDescription.Trim();
        }
        return new WorksheetEditRequest(address, top, left, bottom, right, values, description);
    }

    private static int Column(string label)
    {
        int result = 0;
        foreach (char letter in label) result = result * 26 + letter - 'A' + 1;
        return result - 1;
    }

    private static object ReadLiteral(JsonNode node)
    {
        if (node == null) return null;
        if (node is JsonValue value)
        {
            if (value.TryGetValue(out bool flag)) return flag;
            if (value.TryGetValue(out string text))
            {
                if (text.Length > MaximumCellText || text.Contains('\0'))
                    throw new ArgumentException("A proposed cell may contain up to 2,048 characters of text and no NUL characters.");
                return text;
            }
            if (value.GetValueKind() == JsonValueKind.Number &&
                double.TryParse(value.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double number) && double.IsFinite(number))
                return number;
        }
        throw new ArgumentException("Use only literal numbers, text, true, false or null for blank cells.");
    }
}

internal sealed record WorksheetCellEdit(int Row, int Column, string Address, object Before, string PreviousFormula,
    object After, string NumberFormat, string FormattedBefore, string FormattedAfter)
{
    internal bool ReplacesExisting => PreviousFormula != null || Before != null;
    internal string BeforeText => PreviousFormula == null
        ? Display(Before, FormattedBefore)
        : Display(Before, FormattedBefore) + " (formula " + PreviousFormula + ")";
    internal string AfterText => Display(After, FormattedAfter);

    private static string Display(object value, string formatted)
    {
        if (value == null) return "(blank)";
        if (string.IsNullOrEmpty(formatted) && value is not "") return "(hidden by cell format)";
        if (value is not string) return formatted;
        return "\"" + (formatted ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"")
            .Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t") + "\"";
    }
}

internal sealed class WorksheetEditProposal
{
    internal WorksheetEditTarget Target { get; }
    internal WorksheetEditRequest Request { get; }
    internal IReadOnlyList<WorksheetCellEdit> PreviewCells { get; }
    internal string WorkbookName { get; }
    internal string Description => Request.Description;
    internal string RangeAddress => Request.Address;
    internal string WorksheetName => Target.SheetName;
    internal int ChangedCellCount => PreviewCells.Count;
    internal bool OverwritesExistingCells => PreviewCells.Any(cell => cell.ReplacesExisting);

    internal WorksheetEditProposal(WorksheetEditTarget target, WorksheetEditRequest request, string workbookName, List<WorksheetCellEdit> cells)
    {
        Target = target;
        Request = request;
        WorkbookName = workbookName;
        PreviewCells = cells.AsReadOnly();
    }

    internal void Validate() => Target.Grid.ValidateWorksheetEdit(this);
    internal void Apply() => Target.Grid.ApplyWorksheetEdit(this);

    internal bool ReviewAndApply(IWin32Window owner, Func<bool> canApply = null)
    {
        CheckApproval(canApply);
        Validate();
        using var dialog = new WorksheetEditReview(this);
        if (dialog.ShowDialog(owner) != DialogResult.OK) return false;
        CheckApproval(canApply);
        Apply();
        return true;
    }

    private static void CheckApproval(Func<bool> canApply)
    {
        if (canApply != null && !canApply()) throw new InvalidOperationException("The worksheet edit is no longer available in this conversation.");
    }

    private sealed class WorksheetEditReview : Form
    {
        private readonly int changedCells;

        internal WorksheetEditReview(WorksheetEditProposal proposal)
        {
            changedCells = proposal.ChangedCellCount;
            Name = "worksheetEditReview";
            Text = "Review worksheet edits";
            StartPosition = FormStartPosition.Manual;
            AutoScaleDimensions = new SizeF(96, 96);
            AutoScaleMode = AutoScaleMode.Dpi;
            ShowInTaskbar = false;
            MinimizeBox = false;

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(12) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(layout);
            var target = new Label
            {
                AutoSize = true, Dock = DockStyle.Fill, UseMnemonic = false,
                Text = $"{proposal.WorksheetName}!{proposal.RangeAddress} in {proposal.WorkbookName}"
            };
            layout.Controls.Add(target, 0, 0);
            int count = proposal.ChangedCellCount;
            int replaced = proposal.PreviewCells.Count(cell => cell.ReplacesExisting);
            var details = new Label
            {
                AutoSize = true, Dock = DockStyle.Fill, UseMnemonic = false, Margin = new Padding(3, 4, 3, 8),
                Text = $"{count} {(count == 1 ? "cell will change" : "cells will change")}."
                    + (replaced == 0 ? "" : $" Existing content will be replaced or cleared in {replaced} {(replaced == 1 ? "cell" : "cells")}.")
            };
            layout.Controls.Add(details, 0, 1);

            var grid = new DataGridView
            {
                Name = "worksheetEditPreview", AccessibleName = "Before and after worksheet values",
                Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
                RowHeadersVisible = false, AllowUserToResizeRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = System.Drawing.SystemColors.Window, GridColor = System.Drawing.SystemColors.ControlDark,
                BorderStyle = BorderStyle.FixedSingle,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false
            };
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Cell", FillWeight = 20, MinimumWidth = 65, SortMode = DataGridViewColumnSortMode.NotSortable });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Before", FillWeight = 70, SortMode = DataGridViewColumnSortMode.NotSortable });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "After", FillWeight = 70, SortMode = DataGridViewColumnSortMode.NotSortable });
            foreach (var cell in proposal.PreviewCells)
            {
                int row = grid.Rows.Add(cell.Address, cell.BeforeText, cell.AfterText);
                grid.Rows[row].Cells[1].ToolTipText = cell.BeforeText;
                grid.Rows[row].Cells[2].ToolTipText = cell.AfterText;
            }
            layout.Controls.Add(grid, 0, 2);
            var buttons = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var cancel = new Button { Name = "cancelWorksheetEdit", Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
            var apply = new Button { Name = "applyWorksheetEdit", Text = "Apply edits", AutoSize = true, DialogResult = DialogResult.OK };
            buttons.Controls.Add(new Label { Text = "Undo restores the previous cells.", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            buttons.Controls.Add(apply, 1, 0);
            buttons.Controls.Add(cancel, 2, 0);
            layout.Controls.Add(buttons, 0, 3);
            CancelButton = cancel;
            Shown += (_, _) => { grid.ClearSelection(); cancel.Focus(); };
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Form host = Owner ?? this;
            Rectangle area = Screen.FromControl(host).WorkingArea;
            Size border = SizeFromClientSize(Size.Empty);
            // A few edits fit in a compact dialog. Keep a scrollable preview for larger changes.
            int preferredHeight = 185 + Math.Clamp(changedCells, 3, 11) * 21;
            Size preferred = host.LogicalToDeviceUnits(new Size(700, preferredHeight));
            Size minimum = host.LogicalToDeviceUnits(new Size(535, 240));
            MinimumSize = new Size(Math.Min(minimum.Width, area.Width), Math.Min(minimum.Height, area.Height));
            ClientSize = new Size(Math.Min(preferred.Width, area.Width - border.Width), Math.Min(preferred.Height, area.Height - border.Height));
            Location = new Point(
                Math.Clamp(host.Left + (host.Width - Width) / 2, area.Left, area.Right - Width),
                Math.Clamp(host.Top + (host.Height - Height) / 2, area.Top, area.Bottom - Height));
        }
    }
}
