using System;
using System.Linq;
using System.Windows.Forms;

namespace StatsDirect.UI.HtmlHelp;

/// <summary>Reserves space beside the MDI client without reparenting MDI documents or changing their tabs.</summary>
internal sealed class HelpDockHost : IDisposable
{
    private readonly Form host;
    internal Panel Panel { get; } = new() { Dock = DockStyle.Right, Visible = false, AccessibleName = "Help pane" };
    internal Splitter Divider { get; } = new() { Dock = DockStyle.Right, Visible = false, AccessibleName = "Resize help pane", TabStop = false };
    private int preferredWidth;
    private bool sizing;

    internal HelpDockHost(Form host)
    {
        this.host = host;
        float scale = host.DeviceDpi / 96f;
        preferredWidth = (int)(460 * scale);
        Divider.Width = Math.Max(5, (int)(5 * scale));
        Divider.MinSize = (int)(240 * scale);
        Divider.MinExtra = (int)(240 * scale);
        host.SuspendLayout();
        Control workspace = host.Controls.Cast<Control>().FirstOrDefault(c => c is MdiClient) ?? host.Controls.Cast<Control>().FirstOrDefault(c => c.Dock == DockStyle.Fill);
        host.Controls.Add(Panel);
        host.Controls.Add(Divider);
        // Docking is performed in reverse z-order: menu/ribbon first, then
        // the right pane and its divider, then the remaining workspace.
        host.Controls.SetChildIndex(Panel, 0);
        host.Controls.SetChildIndex(Divider, 0);
        workspace?.BringToFront();
        host.Controls.SetChildIndex(Divider, workspace == null ? 0 : 1);
        host.Controls.SetChildIndex(Panel, workspace == null ? 1 : 2);
        host.ResumeLayout(true);
        host.ClientSizeChanged += HostResized;
        Divider.SplitterMoved += SplitterMoved;
    }

    internal void SetVisible(bool visible)
    {
        host.SuspendLayout();
        if (visible) FitWidth();
        Panel.Visible = visible;
        Divider.Visible = visible;
        if (visible)
        {
            int panelIndex = host.Controls.GetChildIndex(Panel);
            if (host.Controls.GetChildIndex(Divider) > panelIndex)
                host.Controls.SetChildIndex(Divider, panelIndex);
        }
        host.ResumeLayout(true);
    }

    private void HostResized(object sender, EventArgs e) { if (Panel.Visible) FitWidth(); }
    private void SplitterMoved(object sender, SplitterEventArgs e) { if (!sizing) preferredWidth = Panel.Width; }
    private void FitWidth()
    {
        sizing = true;
        try
        {
            int minimum = (int)(240 * host.DeviceDpi / 96f);
            int maximum = Math.Max(100, host.ClientSize.Width - minimum - Divider.Width);
            Panel.Width = Math.Clamp(preferredWidth, Math.Min(minimum, maximum), maximum);
        }
        finally { sizing = false; }
    }

    public void Dispose()
    {
        host.ClientSizeChanged -= HostResized;
        Divider.SplitterMoved -= SplitterMoved;
        Divider.Dispose();
        Panel.Dispose();
    }
}
