using SpreadsheetGear;
using System;
using System.IO;
using System.Windows.Forms;

namespace StatsDirect.UI
{
    public static class ClipboardChecker
    {
        const string CLIPBOARD_BIFF8_FORMAT = "Biff8";

        /// <summary>
        /// Access (at least 2010 and 2013) still writes a Biff5 formatted byte stream (Excel up to Excel 95!) to the Clipboard.
        /// SSG can only read Biff8 from the clipboard (neither Biff5 nor the more modern Office 2007+ formats can be read).
        /// This looks through data in the Clipboard for a Biff5 stream.  If - and only if - it finds one, it converts the stream to Biff8 and places that (only) on the Clipboard.
        /// The intention is that this should be used when SSG detects a Paste, before the data is in fact pasted, to do the appropriate conversion.
        /// 
        /// The detection code has no external dependencies.
        /// The conversion code relies on Excel being installed, and being able to find a copy of excelcnv.exe in the same directory as Excel.exe.
        /// This is true of modern Office versions, but may not be true of all.  The code attempts to throw reasonable exceptions if it can't do the conversion.
        /// </summary>
        /// <param name="convert">If false, detects whether there is a Biff5 stream on the Clipboard but does not attempt to convert.  If true, detects and converts.</param>
        /// <returns>true if there is was Biff5 stream on the Clipboard when this was called, false if not.</returns>
        public static bool ConvertClipboardWithBiff5ToBiff8(bool convert)
        {
            IDataObject currentContents = Clipboard.GetDataObject();
            if (null == currentContents)
                return false;
            string[] formats = currentContents.GetFormats(false);
            if (null == formats)
                return false;
            string biff5Format = null;
            string biff8Format = null;
            foreach (string format in formats)
            {
                Stream s = GetFreshDataStream(currentContents, format);
                if (null != s)
                {
                    switch (Biff5Processor.SniffFormat(s))
                    {
                        case Biff5Processor.BiffFormat.Biff5Or7:
                            biff5Format = format;
                            break;
                        case Biff5Processor.BiffFormat.Biff8:
                            if (SsgCanRead(GetFreshDataStream(currentContents, format)))
                                biff8Format = format;
                            break;
                        default:
                            // do nothing
                            break;
                    }
                }
            }

            // If converting, we need to hack at the clipboard if there's Biff5 anywhere on it.  If we can simply substitute an existing Biff8 stream, great.  If not, convert.
            if (null != biff5Format && null == biff8Format && convert)
            {
                MemoryStream biff8Stream = null;
                if (null != biff8Format)
                    biff8Stream = GetFreshDataStream(currentContents, biff8Format);
                else 
                    biff8Stream = Biff5Processor.ConvertBiff5Or7ToBiff8(GetFreshDataStream(currentContents, biff5Format));
                Clipboard.SetData(CLIPBOARD_BIFF8_FORMAT, biff8Stream);
            }
            return null != biff5Format;
        }

        private static bool SsgCanRead(Stream s)
        {
            try
            {
                Factory.GetWorkbookSet().Workbooks.OpenFromStream(s);
            }
            catch (InvalidOperationException)
            {
                // Bad format
                return false;
            }
            return true;
        }

        private static MemoryStream GetFreshDataStream(IDataObject dataObject, string format)
        {
            object data = dataObject.GetData(format);
            return data as MemoryStream;
        }
    }
}
