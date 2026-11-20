using System;
using System.Drawing;
using System.Windows.Forms;

namespace StatsDirect.UI;

/// <summary>The native document bars share the report toolbar's order, icon scale and overflow convention.</summary>
internal static class DocumentToolbar
{
    private static readonly Bitmap placeholder = new(20, 20);

    internal static ToolStripButton Button(string text, string icon, EventHandler click, string shortcut = null, bool labelled = false)
    {
        var button = new ToolStripButton(text, placeholder, click);
        SetButtonAppearance(button, text, icon, shortcut, labelled);
        return button;
    }

    internal static ToolStripDropDownButton DropDownButton(string text, string icon, ToolStripDropDown dropDown)
    {
        var button = new ToolStripDropDownButton(text, placeholder)
        {
            DropDown = dropDown,
            DropDownDirection = ToolStripDropDownDirection.BelowRight,
            ShowDropDownArrow = false
        };
        SetButtonAppearance(button, text, icon, null, true);
        return button;
    }

    private static void SetButtonAppearance(ToolStripItem button, string text, string icon, string shortcut, bool labelled)
    {
        button.Name = icon + "ToolStripButton";
        button.AccessibleName = text;
        button.DisplayStyle = labelled ? ToolStripItemDisplayStyle.ImageAndText : ToolStripItemDisplayStyle.Image;
        button.ToolTipText = shortcut == null ? text : text + " (" + shortcut + ")";
        button.AutoSize = true;
        button.Padding = new Padding(4);
        button.Overflow = ToolStripItemOverflow.AsNeeded;
    }

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
