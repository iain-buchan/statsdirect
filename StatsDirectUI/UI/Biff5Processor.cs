using Microsoft.Win32;
using OpenMcdf;
using SpreadsheetGear;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace StatsDirect.UI
{
    public static class Biff5Processor
    {
        public enum BiffFormat
        {
            SomethingElse,
            Biff5Or7,
            Biff8
        }

        /// <summary>
        /// Returns Biff5or7 if the stream is an Excel Biff5 stream, Biff8 if it's in Biff8, SomethingElse otherwise.
        /// </summary>
        /// <param name="s">The stream to be tested.  This is read as part of the process, so remember to reset to start before doing anything else with it.</param>
        /// <returns>Biff5or7 if the stream is an Excel Biff5 stream, Biff8 if it's in Biff8, SomethingElse otherwise</returns>
        public static BiffFormat SniffFormat(Stream s)
        {
            try
            {
                using CompoundFile contents = new(s);
                if (!contents.RootStorage.TryGetStream("Book", out CFStream bookStream)
                    && !contents.RootStorage.TryGetStream("Workbook", out bookStream))
                {
                    // Nothing named Book or Workbook; not an Excel workbook.
                    return BiffFormat.SomethingElse;
                }

                // By now, bookStream is known to be non-null.  Test the first few bytes (no need to read the whole lot) to see whether this is an Excel Biff5 workbook.
                if (bookStream.Size < 6)
                    return BiffFormat.SomethingElse;
                byte[] bytes = new byte[6];
                bookStream.Read(bytes, 0, 6);
                // Excel BIFF, BOF record, at least BIFF5 starts with 0x0809 (little-endian).  Version is at offset 4: 0x0500 (again little-endian) for BIFF5/BIFF7, 0x0600 for BIFF8.
                if (bytes.Length < 6 || bytes[0] != 9 || bytes[1] != 8)
                    return BiffFormat.SomethingElse;
                if (bytes[4] == 0 && bytes[5] == 5)
                    return BiffFormat.Biff5Or7;
                if (bytes[4] == 0 && bytes[5] == 6)
                    return BiffFormat.Biff8;
                return BiffFormat.SomethingElse;
            }
            catch (CFFileFormatException)
            {
                // Not a valid OLE format
                return BiffFormat.SomethingElse;
            }
        }

        /// <summary>
        /// Take a Biff5 stream; return a Biff8 stream.  Uses temporary files, not thread-safe, not even process-safe.
        /// TODO: Should create temp files for the conversion.
        /// </summary>
        /// <param name="biff5Stream">A stream from which a Biff5 file can be read</param>
        /// <returns>A MemoryStream to which the corresponding Biff8 file has been written</returns>
        /// <remarks>Precondition</remarks>
        public static MemoryStream ConvertBiff5Or7ToBiff8(Stream biff5Stream)
        {
            // Write the stream to a temp file, convert it using Excelcnv.exe, then read the output file back in as a stream and stick it back on the clipboard.
            string tempPath = Path.GetTempPath();
            string biff5FileName = Path.Combine(tempPath, "Biff5or7.xls");
            using (FileStream biff5FileStream = File.OpenWrite(biff5FileName))
            {
                biff5Stream.CopyTo(biff5FileStream);
            }

            string xlsxTempFileName = Path.Combine(tempPath, "xlsx.xlsx");
            string excelCnvPath = FindExcelCnv();
            if (null == excelCnvPath)
                throw new Exception("Cannot find an installed Excel converter. Please ensure Microsoft Office is installed, and ensure the Office format conversion is ticked in the installer.");
            string arguments = string.Join(" ", "-oice", Quote(biff5FileName), Quote(xlsxTempFileName));
            ProcessStartInfo psi = new() { WorkingDirectory = tempPath, FileName = excelCnvPath, Arguments = arguments };
            using (Process excelcnvProcess = Process.Start(psi))
            {
                excelcnvProcess.WaitForExit();
                if (excelcnvProcess.ExitCode != 0)
                    throw new Exception("Conversion of BIFF5 file failed; excelcnv.exe exited with a non-zero exit code. Please try pasting your data into Excel and see whether that succeeds.");
            }
            File.Delete(biff5FileName);

            MemoryStream biff8Stream;
            using (FileStream xlsxFileStream = File.OpenRead(xlsxTempFileName))
            {
                IWorkbook workbook = Factory.GetWorkbookSet().Workbooks.OpenFromStream(xlsxFileStream);
                biff8Stream = new MemoryStream();
                workbook.SaveToStream(biff8Stream, FileFormat.Excel8);
            }
            File.Delete(xlsxTempFileName);
            return biff8Stream;
        }

        /// <returns>The path to an installed excelcnv.exe, or null if none can be found</returns>
        private static string FindExcelCnv()
        {
            // Find Excel.exe and assume excelcnv.exe is in the same directory.
            // Excel.exe can be located by getting its CLSID from the version-independent registry entry HKCR\Excel.Application\CLSID and then looking up that CLSID in HKCR\CLSID\<CLSID>
            string excelClsId = GetStringValueOrNull(Registry.ClassesRoot, new[] { "Excel.Application", "CLSID" }, null);
            if (null == excelClsId)
            {
                // Excel not installed, registry not readable, or similar awkwardness.  Give up.
                return null;
            }

            string localServerCommandLine = GetStringValueOrNull(Registry.ClassesRoot, new[] { "CLSID", excelClsId, "LocalServer" }, null)
                ?? GetStringValueOrNull(Registry.ClassesRoot, new[] { "CLSID", excelClsId, "LocalServer32" }, null);
            if (null == localServerCommandLine)
            {
                // Excel not installed, registry not readable, or similar awkwardness.  Give up.
                return null;
            }

            // Just get the executable path, stripping off any command-line switches at the end.
            int firstSlash = localServerCommandLine.IndexOf('/');
            string excelPath = firstSlash >= 0 ? localServerCommandLine.Substring(0, firstSlash) : localServerCommandLine;
            // If we've stripped command-line switches, the path may have a trailing space.
            excelPath = excelPath.TrimEnd();
            // Strip quotes if present
            if (excelPath.StartsWith("\"") && excelPath.EndsWith("\""))
                excelPath = excelPath.Substring(1, excelPath.Length - 2);
            string officePath = Path.GetDirectoryName(excelPath);
            string excelCnvPath = Path.Combine(officePath, "excelcnv.exe");
            return File.Exists(excelCnvPath) ? excelCnvPath : null;
        }

        private static string Quote(string path)
        {
            return "\"" + path + "\"";
        }

        private static string GetStringValueOrNull(RegistryKey root, string[] subkeyNames, string valueName)
        {
            RegistryKey here = root;
            Stack<IDisposable> toDispose = new();
            try
            {
                foreach (string subkeyName in subkeyNames)
                {
                    try
                    {
                        here = here.OpenSubKey(subkeyName);
                        if (null != here)
                            toDispose.Push(here);
                    }
                    catch (Exception)
                    {
                        here = null;
                        break;
                    }
                }
                if (null == here)
                    return null;
                try
                {
                    return (string)here.GetValue(valueName);
                }
                catch (Exception)
                {
                    return null;
                }
            }
            finally
            {
                while (toDispose.Count > 0)
                    toDispose.Pop().Dispose();
            }
        }
    }
}
