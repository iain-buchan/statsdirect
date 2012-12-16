using System;
using System.Windows.Forms;

namespace StatsDirect.UI
{
    class DrawingControl
    {
        private const int WM_SETREDRAW = 11;

        private static int suspendCounter;

        public static void SuspendDrawing(Control parent)
        {
            if (0 == suspendCounter)
            {
                Message msgSuspendUpdate = Message.Create(parent.Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
                NativeWindow window = NativeWindow.FromHandle(parent.Handle);
                window.DefWndProc(ref msgSuspendUpdate);
            }
            suspendCounter++;
        }

        public static void ResumeDrawing(Control parent)
        {
            if (suspendCounter > 0)
                suspendCounter--;
            if (0 == suspendCounter)
            {
                IntPtr wparam = new IntPtr(1);
                Message msgResumeUpdate = Message.Create(parent.Handle, WM_SETREDRAW, wparam, IntPtr.Zero);
                NativeWindow window = NativeWindow.FromHandle(parent.Handle);
                window.DefWndProc(ref msgResumeUpdate);

                parent.Refresh();
            }
        }
    }
}