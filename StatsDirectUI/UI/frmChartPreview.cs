using StatsDirect.Configuration;
using StatsDirect.UI.WebReports;
using System;
using System.IO;
using System.Windows.Forms;

namespace StatsDirect.UI;

/// <summary>Preview the same HTML/SVG that will appear in the report.</summary>
internal sealed class frmChartPreview : Form
{
    internal frmChartPreview(string html)
    {
        Text = "Chart preview";
        Width = 1000;
        Height = 750;
        StartPosition = FormStartPosition.CenterParent;
        var view = new ReportView(Path.Combine(SDConfiguration.InstallationDirectory, "ReportEditor")) { Dock = DockStyle.Fill };
        var status = new Label { Dock = DockStyle.Bottom, Height = 28, Text = "Loading chart…" };
        var close = new Button { Text = "Close", Dock = DockStyle.Bottom, DialogResult = DialogResult.Cancel };
        Controls.Add(view);
        Controls.Add(status);
        Controls.Add(close);
        CancelButton = close;
        view.Notice += message => status.Text = message;
        Shown += async (_, _) =>
        {
            try
            {
                await view.AppendAsync(html, "Chart preview", "", 0);
                await view.CallAsync("preview");
                status.Text = "";
            }
            catch (Exception ex) { status.Text = ex.Message; }
        };
    }
}
