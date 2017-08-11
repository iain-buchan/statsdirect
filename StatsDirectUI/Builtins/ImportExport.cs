using System;
using System.IO;
using System.Windows.Forms;

using StatsDirect.Data;
using StatsDirect.Templates;
using StatsDirect.UI;
using System.Globalization;
using System.Collections.Generic;
using StatsDirect.CsvParser;

namespace StatsDirect.Builtins
{
    public static class ImportExport
    {
        public static ParameterBag FileImportWorksheet(ITemplateHost host, ParameterBag parameters)
        {
            // import data to the active worksheet
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.ShowHelp = true;
                openFileDialog.Title = "Import worksheet data";
                openFileDialog.Filter = "Comma delimited (*.csv)|*.csv|Tab delimited (*.tab)|*.tab|Text file (*.txt)|*.txt|All files (*.*)|*.*";
                openFileDialog.CheckFileExists = true;

                DialogResult result = openFileDialog.ShowDialog(SdApplication.SoleInstance.MainWindow);
                if (DialogResult.OK != result)
                    return null;

                // If it's an Excel file, load it instead
                string suffix = Path.GetExtension(openFileDialog.FileName);
                if (".xls".Equals(suffix) || ".xlsx".Equals(suffix))
                    throw new Exception("Please Open an excel file rather than Importing it.");

                host.StartProgress("Importing data", false);
                try
                {
                    ParameterBag outputParameters = FileImportAscii(openFileDialog.FileName);
                    return outputParameters;
                }
                finally
                {
                    host.FinishProgress();
                }
            }
        }


        public static ParameterBag FileImportReport()
        {
            // import text to the active report
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Title = "Import Text";
                openFileDialog.Filter = "ASCII Text (*.txt)|*.txt|All files (*.*)|*.*";
                openFileDialog.CheckFileExists = true;

                DialogResult result = openFileDialog.ShowDialog(SdApplication.SoleInstance.MainWindow);
                if (DialogResult.OK != result)
                {
                    return null;
                }
                using (StreamReader sr = File.OpenText(openFileDialog.FileName))
                {
                    string fileContents = sr.ReadToEnd();
                    ParameterBag outputParameters = new ParameterBag();
                    outputParameters.AddOutput("rtf", fileContents);
                    return outputParameters;
                }
            }
        }

        private static ParameterBag FileImportAscii(string path)
        {
            // If the file contains Tabs read as tab-delimited; otherwise, read as Excel comma-delimited.
            bool isTabDelimited = SniffForTabs(path);

            using (StreamReader sr = File.OpenText(path))
            {
                DataFrame outputFrame = isTabDelimited ? ImportTabSeparated(sr) : ImportCommaSeparated(sr);
                ParameterBag outputParameters = new ParameterBag();
                outputParameters.AddOutput("output", outputFrame);
                return outputParameters;
            }
        }

        private static bool SniffForTabs(string path)
        {
            using (StreamReader sr = File.OpenText(path))
            {
                string firstLine = sr.ReadLine();
                if (null == firstLine)
                    return false;

                return firstLine.Contains("\t");
            }
        }

        private static DataFrame ImportTabSeparated(StreamReader sr)
        {
            string currentLine = sr.ReadLine();
            if (null == currentLine)
                return null;

            string delimiter = "\t";
            if (currentLine.Substring(currentLine.Length - 1) != delimiter)
                currentLine += delimiter;
            DataFrame outputFrame = new DataFrame();
            int row = 0;
            do
            {
                int lastSplitPosition = 0;
                for (int col = 0; col <= 256; col++)
                {
                    int splitPosition = currentLine.IndexOf(delimiter, lastSplitPosition, StringComparison.Ordinal);
                    if (splitPosition < 0)
                        break;
                    int splitLength = splitPosition - lastSplitPosition;
                    string rawField = currentLine.Substring(lastSplitPosition, splitLength);
                    // Trim leading and trailing "..." if both are present
                    if (rawField.StartsWith(@"""") && rawField.EndsWith(@""""))
                        rawField = rawField.Substring(1, rawField.Length - 2).Replace("\"\"", "\"");
                    if (outputFrame.VariableCount <= col)
                        outputFrame.Variables.Add(new StringVariable());
                    (outputFrame.Variables[col] as StringVariable).SetData(row, rawField);
                    lastSplitPosition += splitLength + 1;
                }
                if (sr.EndOfStream)
                    break;
                currentLine = sr.ReadLine();
                if (null == currentLine)
                    break;

                if (currentLine.Length > 0 && currentLine.Substring(currentLine.Length - 1) != delimiter)
                    currentLine += delimiter;
                row += 1;
            } while (true);
            return outputFrame;
        }

        private static DataFrame ImportCommaSeparated(StreamReader sr)
        {
            List<List<string>> rows = CsvReader.Read(sr);

            int longestRowCount = 0;
            foreach (List<string> row in rows)
                if (row.Count > longestRowCount)
                    longestRowCount = row.Count;

            DataFrame outputFrame = new DataFrame();
            for (int colIndex = 0; colIndex < longestRowCount; colIndex++)
                outputFrame.Variables.Add(new StringVariable(rows.Count, null));

            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                List<string> row = rows[rowIndex];
                for (int colIndex = 0; colIndex < row.Count; colIndex++)
                    (outputFrame.Variables[colIndex] as StringVariable).SetData(rowIndex, row[colIndex]);
            }
            return outputFrame;
        }

        public static ParameterBag FileExportWorksheet(ITemplateHost host, ParameterBag parameters)
        {
            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Title = "Export Data";
                saveFileDialog.Filter = "Comma delimited (*.csv)|*.csv|Tab delimited (*.tab)|*.tab";
                DataFrame data = parameters["data"].AsDataFrame;
                string source = data.Name;
                saveFileDialog.FileName = source.Contains(".") ? source.Substring(0, source.Length - 4) + ".csv" : source + ".csv";
                saveFileDialog.OverwritePrompt = true;
                DialogResult result = saveFileDialog.ShowDialog(SdApplication.SoleInstance.MainWindow);
                if (DialogResult.OK == result)
                {
                    bool useTabDelimiter = saveFileDialog.FileName.Substring(saveFileDialog.FileName.Length - 3).ToLower(CultureInfo.InvariantCulture) == "tab";
                    using (StreamWriter sw = File.CreateText(saveFileDialog.FileName))
                    {
                        host.StartProgress("Exporting Worksheet", true);

                        //  Titles
                        string delimiter = useTabDelimiter ? "\t" : ",";
                        bool first = true;
                        for (int c = 0; c < data.VariableCount; c++)
                        {
                            StringVariable v = data.Variables[c] as StringVariable;
                            if (first)
                                first = false;
                            else
                                sw.Write(delimiter);
                            sw.Write(ToCsvCell(v.Title));
                        }
                        sw.WriteLine();

                        //  Data
                        int rows = data.MaxRows;
                        for (int r = 0; r < data.MaxRows; r++)
                        {
                            first = true;
                            for (int c = 0; c < data.VariableCount; c++)
                            {
                                StringVariable v = data.Variables[c] as StringVariable;
                                if (first)
                                    first = false;
                                else
                                    sw.Write(delimiter);
                                string buf = v.Length > r ? v.Data[r] : string.Empty;
                                sw.Write(ToCsvCell(buf));
                            }
                            sw.WriteLine();
                            if (host.UpdateProgress(r / (double)rows))
                                break;
                        }
                        host.FinishProgress();
                    }
                }
            }
            return new ParameterBag();
        }

        /// <param name="contents"></param>
        /// <returns>A representation of contents in Excel-CSV format, i.e. quoted and with quotes double-quoted if there are CSV metacharacters (comma, double-quote, newline) in the contents.</returns>
        private static string ToCsvCell(string contents)
        {
            string s = contents.Trim();
            if (s.Contains("\"") || s.Contains(",") || s.Contains(Environment.NewLine))
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            else
                return s;
        }
    }
}
