using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using StatsDirect.R;

namespace StatsDirect.UI;

public partial class frmMain
{
    private void OpenWorksheetR(bool external)
    {
        if (ActiveMdiChild is not frmSpreadsheetGear worksheet || !mnuMain.Enabled || InOperation) return;

        string title = external ? WorksheetRPrompt.ExternalTitle : WorksheetRPrompt.InternalTitle;
        string importFolder = null;
        try
        {
            using var prompt = new WorksheetRPrompt(title, worksheet.DescribeWorksheetRData());
            if (prompt.ShowDialog(this) != DialogResult.OK || ActiveMdiChild != worksheet) return;

            var version = external ? WorksheetRLauncher.FindRGui() : null;
            if (external && version == null) return;

            string script = null;
            if (prompt.SelectedData is WorksheetRData data)
            {
                importFolder = WorksheetRLauncher.CreateImportFolder();
                script = data.WriteImportScript(prompt.UseHeaders, prompt.ImportFormat, prompt.UseFriendlyNames, importFolder);
            }

            if (external)
            {
                WorksheetRLauncher.Launch(version, script, importFolder);
            }
            else
            {
                var window = (frmScript)CreateScriptWindow();
                if (importFolder != null)
                {
                    string sessionFolder = importFolder;
                    window.WorksheetImportFolder = sessionFolder;
                    window.Disposed += (_, _) => WorksheetRLauncher.DeleteImportFolder(sessionFolder);
                }
                window.ShowR(script ?? string.Empty, "R session");
            }

            // The session now owns its import files.
            importFolder = null;
        }
        catch (Exception error)
        {
            SdApplication.SoleInstance.FriendlyError("Could not open the R session", error, false);
        }
        finally
        {
            WorksheetRLauncher.DeleteImportFolder(importFolder);
        }
    }
}

/// <summary>The R import prompt, worded and laid out as on the Mac (statisticalhelp WorksheetR/RULES.md, section 1).</summary>
internal sealed class WorksheetRPrompt : Form
{
    /// <summary>The R and R+ buttons' tooltips, and the prompt's title (rule 1.1).</summary>
    internal const string InternalTitle = "Open in StatsDirect R", ExternalTitle = "Open in external R";
    private static readonly Size PromptSize = new(455, 244);
    private readonly WorksheetRImportOptions options;
    private readonly RadioButton entireSheet = new() { Name = "worksheetRAll", AutoSize = true };
    private readonly RadioButton selectedCells = new() { Name = "worksheetRSelected", AutoSize = true };
    private readonly RadioButton noData = new() { Name = "worksheetRNone", Text = "No data", AutoSize = true, Checked = true };
    private readonly CheckBox headers = new()
    {
        Name = "worksheetRHeaders", Text = "Use the first row as column names", AutoSize = true
    };
    private readonly CheckBox friendlyNames = new()
    {
        Name = "worksheetRFriendlyNames", Text = "Use R-friendly names", AutoSize = true
    };
    private readonly Label scope = new() { Name = "worksheetRScope", AutoSize = true, UseMnemonic = false };
    private readonly ComboBox format = new()
    {
        Name = "worksheetRFormat", AccessibleName = "Import as", DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill
    };
    private readonly Label formatHint = new() { Text = "Missing values are removed separately from each vector.", AutoSize = true };
    // The choices, each in a row of its own sized to it, in a table as large as they need; it sits in a view that scrolls when the
    // dialog is smaller (a small working area, or a large display scale), above the buttons, which keep their own row at the foot.
    // The dialog is as large as its contents need within the working area (OnLoad).
    private readonly TableLayoutPanel layout = new() { ColumnCount = 1, RowCount = 8, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
    private readonly Panel choices = new() { Name = "worksheetRChoices", Dock = DockStyle.Fill, AutoScroll = true };
    private readonly TableLayoutPanel frame = new() { ColumnCount = 1, RowCount = 2, Dock = DockStyle.Fill };
    private readonly TableLayoutPanel formatRow = new() { ColumnCount = 2, RowCount = 1, AutoSize = true, Dock = DockStyle.Fill };
    private readonly FlowLayoutPanel buttons = new() { FlowDirection = FlowDirection.RightToLeft, WrapContents = false, AutoSize = true, Dock = DockStyle.Fill };

    internal bool UseHeaders => headers.Checked;
    internal bool UseFriendlyNames => friendlyNames.Enabled && friendlyNames.Checked;
    internal WorksheetRDataFormat ImportFormat => format.SelectedIndex == 1
        ? WorksheetRDataFormat.VectorList : WorksheetRDataFormat.DataFrame;
    internal WorksheetRData SelectedData => entireSheet.Checked ? options.EntireSheet
        : selectedCells.Checked ? options.SelectedCells : null;

    internal WorksheetRPrompt(string title, WorksheetRImportOptions options)
    {
        this.options = options;
        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        MinimizeBox = false;
        MaximizeBox = false;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = PromptSize;

        // 1.2 to 1.5
        scope.Text = options.EntireSheet == null ? $"The worksheet “{options.SheetName}” has no data to copy."
            : $"Choose the data from “{options.SheetName}” to copy into the new R session as worksheet_data.";
        scope.Margin = new Padding(3, 3, 3, 6);
        entireSheet.Enabled = options.EntireSheet != null;
        entireSheet.Text = EntireLabel(options);
        selectedCells.Enabled = options.SelectedCells != null;
        selectedCells.Text = SelectedLabel(options);

        format.Items.AddRange(["Data frame (keep rows aligned)", "List of vectors (independent samples)"]);
        format.SelectedIndex = 0;
        formatRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        formatRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        formatRow.Controls.Add(new Label { Text = "Import as:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        formatRow.Controls.Add(format, 1, 0);

        var open = new Button { Text = "Open", AutoSize = true, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        buttons.Controls.AddRange(new Control[] { cancel, open });
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        for (int row = 0; row < 8; row++) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(scope, 0, 0);
        layout.Controls.Add(entireSheet, 0, 1);
        layout.Controls.Add(selectedCells, 0, 2);
        layout.Controls.Add(noData, 0, 3);
        // 1.6 and 1.7: how the data is shaped, then how it is named.
        layout.Controls.Add(formatRow, 0, 4);
        layout.Controls.Add(formatHint, 0, 5);
        layout.Controls.Add(headers, 0, 6);
        layout.Controls.Add(friendlyNames, 0, 7);
        choices.Controls.Add(layout);
        frame.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        frame.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        frame.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        frame.Controls.Add(choices, 0, 0);
        frame.Controls.Add(buttons, 0, 1);
        Controls.Add(frame);

        entireSheet.CheckedChanged += (_, _) => UpdateImportOptions();
        selectedCells.CheckedChanged += (_, _) => UpdateImportOptions();
        noData.CheckedChanged += (_, _) => UpdateImportOptions();
        headers.CheckedChanged += (_, _) => UpdateFriendlyNames();
        format.SelectedIndexChanged += (_, _) => UpdateFormatHint();
        UpdateImportOptions();
        AcceptButton = open;
        CancelButton = cancel;
        ActiveControl = noData;
    }

    private void UpdateImportOptions()
    {
        WorksheetRData data = SelectedData;
        headers.Enabled = data is { RowCount: > 1 };
        headers.Checked = headers.Enabled && data.SuggestHeaders;
        UpdateFriendlyNames();
        format.Enabled = data != null;
        UpdateFormatHint();
    }

    private void UpdateFriendlyNames() => friendlyNames.Enabled = headers.Enabled && headers.Checked;

    private void UpdateFormatHint() => formatHint.Visible = format.Enabled && ImportFormat == WorksheetRDataFormat.VectorList;

    /// <summary>The two data choices' labels, as tests/WorksheetRExamples checks them against the shared examples.</summary>
    internal static string EntireLabel(WorksheetRImportOptions options) =>
        options.EntireSheet == null ? "Entire worksheet (no data)" : Describe("Entire worksheet", options.EntireSheet);

    internal static string SelectedLabel(WorksheetRImportOptions options) => options.SelectedCells is WorksheetRData selection
        ? Describe("Selected cells", selection)
        : $"Selected cells ({options.SelectionUnavailableReason ?? WorksheetRImport.NoSelection})";

    // 1.3 and 1.4: "Entire worksheet — A1:C4 (4 rows × 3 columns)", counts in digits with commas whatever the locale.
    private static string Describe(string label, WorksheetRData data) =>
        $"{label} — {data.Address} ({Count(data.RowCount, "row")} × {Count(data.ColumnCount, "column")})";

    private static string Count(int n, string noun) =>
        n.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " " + noun + (n == 1 ? "" : "s");

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        var owner = Owner ?? this;
        var area = Screen.FromControl(owner).WorkingArea;
        Padding = new Padding(owner.LogicalToDeviceUnits(12));
        // As wide as the widest choice needs (a range of several areas can be long), and as tall as the choices need with the hint
        // shown, with the buttons, but no smaller than the usual size, and within the working area: beyond that the choices scroll.
        // The sentence at the top wraps to the width the choices need, or that the dialog has.
        Size minimum = owner.LogicalToDeviceUnits(PromptSize), border = SizeFromClientSize(Size.Empty);
        int widest = new Control[] { entireSheet, selectedCells, noData, formatRow, formatHint, headers, friendlyNames }
            .Max(control => control.GetPreferredSize(Size.Empty).Width + control.Margin.Horizontal) + layout.Padding.Horizontal;
        int width = Math.Min(Math.Max(minimum.Width, Math.Max(widest, buttons.GetPreferredSize(Size.Empty).Width) + Padding.Horizontal),
            area.Width - border.Width);
        scope.MaximumSize = new Size(Math.Max(1, Math.Min(widest, width - Padding.Horizontal) - layout.Padding.Horizontal - scope.Margin.Horizontal), 0);
        bool hintShown = formatHint.Visible;
        formatHint.Visible = true;
        int height = layout.GetPreferredSize(Size.Empty).Height + buttons.GetPreferredSize(Size.Empty).Height + buttons.Margin.Vertical + Padding.Vertical;
        formatHint.Visible = hintShown;
        ClientSize = new Size(width, Math.Min(Math.Max(minimum.Height, height), area.Height - border.Height));
        Location = new Point(
            Math.Clamp(owner.Left + (owner.Width - Width) / 2, area.Left, area.Right - Width),
            Math.Clamp(owner.Top + (owner.Height - Height) / 2, area.Top, area.Bottom - Height));
    }
}
