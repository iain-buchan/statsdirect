using System;
using System.Drawing;
using System.Windows.Forms;

namespace StatsDirect.UI.HtmlHelp;

/// <summary>The floating frame owns no browser state; the service moves the shared view in and out.</summary>
internal sealed class HelpWindow : Form
{
    internal event Action CloseRequested;
    internal bool Retiring;
    protected override bool ShowWithoutActivation => true;

    internal HelpWindow()
    {
        Text = "StatsDirect Help";
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        Size = new Size(900, 780);
        MinimumSize = new Size(360, 300);
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!Retiring && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            CloseRequested?.Invoke();
        }
        base.OnFormClosing(e);
    }
}
