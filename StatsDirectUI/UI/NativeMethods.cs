#define ALLOW_OPTIONAL_UNMANAGED_CODE

using System;
using System.Runtime.InteropServices;

namespace StatsDirect.UI
{
#if ALLOW_OPTIONAL_UNMANAGED_CODE
    internal static class NativeMethods
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        internal static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        internal const int WM_MDINEXT = 0x224;
    }
#endif
}
