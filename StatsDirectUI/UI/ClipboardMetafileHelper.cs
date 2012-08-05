using System;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace StatsDirect.UI
{
    /// <summary>
    /// Unpleasant win32-based hack for getting metafiles onto the clipboard - the .Net clipboard format for metafiles is broken as at 2011-04.
    /// </summary>
    /// <remarks>From http://support.microsoft.com/kb/323530 </remarks>
    class ClipboardMetafileHelper
    {
        private static class NativeMethods
        {
            [DllImport("user32.dll")]
            public static extern bool OpenClipboard(IntPtr hWndNewOwner);

            [DllImport("user32.dll")]
            public static extern bool EmptyClipboard();

            [DllImport("user32.dll")]
            public static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

            [DllImport("user32.dll")]
            public static extern bool CloseClipboard();

            [DllImport("gdi32.dll")]
            public static extern IntPtr CopyEnhMetaFile(IntPtr hemfSrc, IntPtr hNULL);

            [DllImport("gdi32.dll")]
            public static extern bool DeleteEnhMetaFile(IntPtr hemf);
        }

        // Metafile mf is set to a state that is not valid inside this function.
        static public bool PutEnhMetafileOnClipboard(IntPtr hWnd, Metafile mf)
        {
            bool bResult = false;
            IntPtr hEMF = mf.GetHenhmetafile();
            if (!hEMF.Equals(new IntPtr(0)))
            {
                IntPtr hEMF2 = NativeMethods.CopyEnhMetaFile(hEMF, new IntPtr(0));
                if (!hEMF2.Equals(new IntPtr(0)))
                {
                    if (NativeMethods.OpenClipboard(hWnd))
                    {
                        if (NativeMethods.EmptyClipboard())
                        {
                            IntPtr hRes = NativeMethods.SetClipboardData(14 /*CF_ENHMETAFILE*/, hEMF2);
                            bResult = hRes.Equals(hEMF2);
                            NativeMethods.CloseClipboard();
                        }
                    }
                }
                NativeMethods.DeleteEnhMetaFile(hEMF);
            }
            return bResult;
        }
    }
}
