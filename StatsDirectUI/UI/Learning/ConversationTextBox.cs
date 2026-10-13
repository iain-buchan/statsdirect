using System;
using System.Drawing;
using System.Windows.Forms;

namespace StatsDirect.UI.Learning;

/// <summary>Updates streamed replies without replacing the history or interrupting reading/copying.</summary>
internal sealed class ConversationTextBox : RichTextBox
{
    private const int WM_SETREDRAW = 0x000B, EM_GETFIRSTVISIBLELINE = 0x00CE, EM_LINESCROLL = 0x00B6;
    private int FirstVisibleLine => (int)NativeMethods.SendMessage(Handle, EM_GETFIRSTVISIBLELINE, IntPtr.Zero, IntPtr.Zero);
    /// <summary>Whether the end of the conversation is in view: the reader is following the reply, so the box scrolls with it.</summary>
    private bool EndInView()
    {
        if (TextLength == 0) return true;
        Point end = GetPositionFromCharIndex(TextLength - 1);
        return end.Y >= 0 && end.Y < ClientSize.Height;
    }
    internal void UpdateConversation(string text, bool resetPosition)
    {
        string previous = Text;
        if (previous == text && !resetPosition) return;
        if (!IsHandleCreated) { Text = text; return; }
        int start = SelectionStart, length = SelectionLength, firstLine = FirstVisibleLine;
        bool follow = resetPosition || (length == 0 && EndInView());
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
                Select(TextLength, 0);   // the scroll to the end comes once redrawing is back on: the control ignores it while redrawing is off
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
        // Following the reply: scroll so that its end is in view, by as much as this chunk added, rather than letting the text fill
        // below the visible lines until something else moves the view.
        if (follow) ScrollToCaret();
    }
}
