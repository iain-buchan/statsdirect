using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace StatsDirect.UI.Learning;

/// <summary>Updates streamed replies without replacing the history or interrupting reading/copying.</summary>
internal sealed class ConversationTextBox : RichTextBox
{
    private const int WM_SETREDRAW = 0x000B, EM_GETFIRSTVISIBLELINE = 0x00CE, EM_LINESCROLL = 0x00B6;
    [StructLayout(LayoutKind.Sequential)]
    private struct ScrollInfo
    {
        internal uint Size, Mask;
        internal int Minimum, Maximum;
        internal uint Page;
        internal int Position, TrackPosition;
    }
    [DllImport("user32.dll")]
    private static extern bool GetScrollInfo(IntPtr handle, int bar, ref ScrollInfo info);

    private int FirstVisibleLine => (int)NativeMethods.SendMessage(Handle, EM_GETFIRSTVISIBLELINE, IntPtr.Zero, IntPtr.Zero);
    private bool AtBottom()
    {
        var info = new ScrollInfo { Size = (uint)Marshal.SizeOf<ScrollInfo>(), Mask = 7 /* range, page, position */ };
        return !GetScrollInfo(Handle, 1 /* vertical */, ref info) || (long)info.Position + info.Page >= info.Maximum;
    }
    internal void UpdateConversation(string text, bool resetPosition)
    {
        string previous = Text;
        if (previous == text && !resetPosition) return;
        if (!IsHandleCreated) { Text = text; return; }
        int start = SelectionStart, length = SelectionLength, firstLine = FirstVisibleLine;
        bool follow = resetPosition || (length == 0 && AtBottom());
        // Usually only a suffix arrives. A final response may correct its streamed tail;
        // replace only that changed portion so earlier text and its selection survive.
        int prefix = 0;
        if (!resetPosition)
            while (prefix < previous.Length && prefix < text.Length && previous[prefix] == text[prefix]) prefix++;
        bool redraw = Visible;
        if (redraw) NativeMethods.SendMessage(Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
        try
        {
            if (resetPosition || prefix == text.Length)
                // A read-only RichTextBox ignores deletion via empty SelectedText.
                // Resetting a conversation or trimming a tail must still work.
                Text = text;
            else
            {
                Select(prefix, previous.Length - prefix);
                SelectedText = text.Substring(prefix);
            }
            ClearUndo();
            if (follow)
            {
                Select(TextLength, 0);
                ScrollToCaret();
            }
            else
            {
                Select(Math.Min(start, TextLength), Math.Min(length, Math.Max(0, TextLength - start)));
                // Use line positions rather than EM_GETSCROLLPOS's 16-bit pixel values,
                // which overflow with long conversations at high display scaling.
                NativeMethods.SendMessage(Handle, EM_LINESCROLL, IntPtr.Zero, (IntPtr)(firstLine - FirstVisibleLine));
            }
        }
        finally
        {
            if (redraw)
            {
                NativeMethods.SendMessage(Handle, WM_SETREDRAW, (IntPtr)1, IntPtr.Zero);
                Invalidate();
            }
        }
    }
}
