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
            var data = worksheet.DescribeWorksheetRData(out string unavailableReason);
            using var prompt = new WorksheetRPrompt(title, data, unavailableReason);
            var choice = prompt.ShowDialog(this);
            if (choice is not (DialogResult.Yes or DialogResult.No) || ActiveMdiChild != worksheet) return;

            var version = external ? WorksheetRLauncher.FindRGui() : null;
            if (external && version == null) return;

            string script = null;
            if (choice == DialogResult.Yes && data != null)
            {
                importFolder = WorksheetRLauncher.CreateImportFolder();
                script = data.WriteImportScript(prompt.UseHeaders, importFolder);
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
    private readonly CheckBox headers = new()
    {
        Name = "worksheetRHeaders", Text = "Use the first row as column names", AutoSize = true, Enabled = false
    };
    private readonly Label scope = new()
    {
        Name = "worksheetRScope", AutoSize = true
    };

    internal bool UseHeaders => headers.Checked;

    internal WorksheetRPrompt(string title, WorksheetRData data, string unavailableReason)
    {
        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        MinimizeBox = false;
        MaximizeBox = false;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(470, 143);
        Padding = new Padding(12);

        if (data == null)
        {
            scope.Text = unavailableReason;
        }
        else
        {
            scope.Text = $"Copy {data.Address.Replace("$", "")} on “{data.SheetName}” ({data.RowCount:N0} rows × {data.ColumnCount:N0} columns) into the new R session as worksheet_data?";
            headers.Enabled = data.RowCount > 1;
            headers.Checked = headers.Enabled && data.SuggestHeaders;
        }

        var copy = new Button { Text = "Copy data and open", AutoSize = true, DialogResult = DialogResult.Yes, Enabled = data != null };
        var open = new Button { Text = "Open without data", AutoSize = true, DialogResult = DialogResult.No };
        var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, WrapContents = false, AutoSize = true, Dock = DockStyle.Fill };
        buttons.Controls.AddRange(new Control[] { cancel, open, copy });
        var layout = new TableLayoutPanel { ColumnCount = 1, RowCount = 3, Dock = DockStyle.Fill };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        scope.Dock = DockStyle.Fill;
        scope.Margin = new Padding(3, 3, 3, 6);
        layout.Controls.Add(scope, 0, 0);
        layout.Controls.Add(headers, 0, 1);
        headers.Visible = data != null;
        layout.Controls.Add(buttons, 0, 2);
        Controls.Add(layout);
        AcceptButton = open;
        CancelButton = cancel;
        ActiveControl = open;
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        var owner = Owner ?? this;
        var area = Screen.FromControl(owner).WorkingArea;
        Padding = new Padding(owner.LogicalToDeviceUnits(12));
        Size desired = SizeFromClientSize(owner.LogicalToDeviceUnits(new Size(440, 124)));
        Size = new Size(Math.Min(desired.Width, area.Width), Math.Min(desired.Height, area.Height));
        scope.MaximumSize = new Size(Math.Max(1, ClientSize.Width - Padding.Horizontal - scope.Margin.Horizontal), 0);
        Location = new Point(
            Math.Clamp(owner.Left + (owner.Width - Width) / 2, area.Left, area.Right - Width),
            Math.Clamp(owner.Top + (owner.Height - Height) / 2, area.Top, area.Bottom - Height));
    }
}
