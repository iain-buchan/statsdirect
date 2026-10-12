using System;
using System.Drawing;
using System.Windows.Forms;

namespace StatsDirect.UI;

/// <summary>The native document bars share the report toolbar's order, icon scale and overflow convention.</summary>
internal static class DocumentToolbar
{
    private static readonly Bitmap placeholder = new(20, 20);

    internal static ToolStripButton Button(string text, string icon, EventHandler click, string shortcut = null, bool labelled = false) =>
        new(text, placeholder, click)
        {
            Name = icon + "ToolStripButton",
            AccessibleName = text,
            DisplayStyle = labelled ? ToolStripItemDisplayStyle.ImageAndText : ToolStripItemDisplayStyle.Image,
            ToolTipText = shortcut == null ? text : text + " (" + shortcut + ")",
            AutoSize = true,
            Padding = new Padding(4),
            Overflow = ToolStripItemOverflow.AsNeeded
        };

    internal static void Configure(ToolStrip bar, Action update)
    {
        bar.Renderer = new SharpToolbarRenderer();
        bar.GripStyle = ToolStripGripStyle.Hidden;
        bar.CanOverflow = true;
        bar.LayoutStyle = ToolStripLayoutStyle.HorizontalStackWithOverflow;
        bar.AccessibleName = "Document commands";
        bar.OverflowButton.Text = "More";
        bar.OverflowButton.DisplayStyle = ToolStripItemDisplayStyle.Text;
        bar.OverflowButton.AccessibleName = "More commands";
        bar.OverflowButton.ToolTipText = "More commands";
        void SizeIcons() => bar.ImageScalingSize = bar.LogicalToDeviceUnits(new Size(20, 20));
        SizeIcons();
        bar.DpiChangedAfterParent += (_, _) => SizeIcons();
        EventHandler idle = (_, _) => { if (!bar.IsDisposed && bar.Visible) update(); };
        Application.Idle += idle;
        bar.Disposed += (_, _) => Application.Idle -= idle;
        bar.OverflowButton.DropDownOpening += (_, _) => update();
        update();
    }
}
