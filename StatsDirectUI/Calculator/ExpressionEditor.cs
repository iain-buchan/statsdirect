using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace StatsDirect.Calculator;

/// <summary>Plain text calculator input with Windows' persistent, multilevel undo.</summary>
internal sealed class ExpressionEditor : RichTextBox
{
    internal event Action<string> EditFailed;
    internal ExpressionEditor()
    {
        DetectUrls = false;
        EnableAutoDragDrop = false;
        // Only text editing shortcuts belong in an expression, never rich-text formatting.
        ShortcutsEnabled = false;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);

    internal void ReplaceSelection(string text)
    {
        // Keep Recall/Insert/paste separate from the preceding/following typing.
        SendMessage(Handle, 0x458 /* EM_STOPGROUPTYPING */, IntPtr.Zero, IntPtr.Zero);
        SelectedText = text;
        SendMessage(Handle, 0x458, IntPtr.Zero, IntPtr.Zero);
    }

    internal void PasteText()
    {
        try
        {
            if (Clipboard.ContainsText()) ReplaceSelection(Clipboard.GetText());
        }
        catch (ExternalException ex)
        {
            // Another application can temporarily own the clipboard. A failed
            // keyboard/context-menu paste must not reach the fatal app handler.
            EditFailed?.Invoke("Could not paste: " + ex.Message);
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x302 /* WM_PASTE */) { PasteText(); return; }
        base.WndProc(ref m);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        switch (keyData)
        {
            case Keys.Control | Keys.Z: Undo(); return true;
            case Keys.Control | Keys.Y:
            case Keys.Control | Keys.Shift | Keys.Z: Redo(); return true;
            case Keys.Control | Keys.A: SelectAll(); return true;
            case Keys.Control | Keys.C:
            case Keys.Control | Keys.Insert: Copy(); return true;
            case Keys.Control | Keys.X:
            case Keys.Shift | Keys.Delete: Cut(); return true;
            case Keys.Control | Keys.V:
            case Keys.Shift | Keys.Insert: PasteText(); return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }
}
