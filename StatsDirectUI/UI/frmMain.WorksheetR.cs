using System;
using System.Drawing;
using System.Windows.Forms;
using StatsDirect.R;

namespace StatsDirect.UI;

public partial class frmMain
{
    private void OpenWorksheetR(bool external)
    {
        if (ActiveMdiChild is not frmSpreadsheetGear worksheet || !mnuMain.Enabled || InOperation) return;

        string title = external ? "Open external R session" : "Open R session";
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

internal sealed class WorksheetRPrompt : Form
{
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

        scope.Text = options.EntireSheet == null ? $"The worksheet “{options.SheetName}” has no data to copy."
            : $"Copy data from “{options.SheetName}” into the new R session as worksheet_data?";
        scope.Margin = new Padding(3, 3, 3, 6);
        entireSheet.Enabled = options.EntireSheet != null;
        entireSheet.Text = options.EntireSheet == null ? "Entire worksheet" : Describe("Entire worksheet", options.EntireSheet);
        selectedCells.Enabled = options.SelectedCells != null;
        selectedCells.Text = options.SelectedCells is WorksheetRData selection
            ? Describe("Selected cells", selection)
            : options.SelectionUnavailableReason is string reason ? $"Selected cells ({reason})" : "Selected cells";

        format.Items.AddRange(["Data frame (keep rows aligned)", "List of vectors (independent samples)"]);
        format.SelectedIndex = 0;
        var formatRow = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, AutoSize = true, Dock = DockStyle.Fill };
        formatRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        formatRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        formatRow.Controls.Add(new Label { Text = "Import as", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        formatRow.Controls.Add(format, 1, 0);

        var open = new Button { Text = "Open", AutoSize = true, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, WrapContents = false, AutoSize = true, Dock = DockStyle.Fill };
        buttons.Controls.AddRange(new Control[] { cancel, open });
        var layout = new TableLayoutPanel { ColumnCount = 1, RowCount = 9, Dock = DockStyle.Fill };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int row = 0; row < 7; row++) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(scope, 0, 0);
        layout.Controls.Add(entireSheet, 0, 1);
        layout.Controls.Add(selectedCells, 0, 2);
        layout.Controls.Add(noData, 0, 3);
        layout.Controls.Add(headers, 0, 4);
        layout.Controls.Add(friendlyNames, 0, 5);
        layout.Controls.Add(formatRow, 0, 6);
        layout.Controls.Add(formatHint, 0, 7);
        layout.Controls.Add(buttons, 0, 8);
        Controls.Add(layout);

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

    private static string Describe(string label, WorksheetRData data) =>
        $"{label} ({data.Address.Replace("$", "")}, {data.RowCount:N0} × {data.ColumnCount:N0})";

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        var owner = Owner ?? this;
        var area = Screen.FromControl(owner).WorkingArea;
        Padding = new Padding(owner.LogicalToDeviceUnits(12));
        Size desired = SizeFromClientSize(owner.LogicalToDeviceUnits(PromptSize));
        Size = new Size(Math.Min(desired.Width, area.Width), Math.Min(desired.Height, area.Height));
        scope.MaximumSize = new Size(Math.Max(1, ClientSize.Width - Padding.Horizontal - scope.Margin.Horizontal), 0);
        Location = new Point(
            Math.Clamp(owner.Left + (owner.Width - Width) / 2, area.Left, area.Right - Width),
            Math.Clamp(owner.Top + (owner.Height - Height) / 2, area.Top, area.Bottom - Height));
    }
}
