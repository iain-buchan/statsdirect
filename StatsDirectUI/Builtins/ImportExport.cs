using System;
using System.IO;
using System.Windows.Forms;

using StatsDirect.Data;
using StatsDirect.Templates;
using StatsDirect.UI;

namespace StatsDirect.Builtins
{
    public class ImportExport
    {
        public static StepResult FileImportWorksheet(ITemplateHost host, ParameterBag parameters)
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
                    return new StepResult(StepSuccess.Success, outputParameters);
                }
                finally
                {
                    host.FinishProgress();
                }
            }
        }


        public static StepResult FileImportReport(ITemplateHost host, ParameterBag parameters)
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
                    return new StepResult(StepSuccess.Success, outputParameters);
                }
            }
        }

        private static ParameterBag FileImportAscii(string path)
        {
            StreamReader sr = File.OpenText(path);
            try
            {
                string currentLine = sr.ReadLine();
                if (null == currentLine)
                    return null;

                // If the file contains Tabs read as tab-delimited; otherwise, read as comma-delimited.
                string delimiter = currentLine.Contains("\t") ? "\t" : ",";
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
                        outputFrame.Variables[col].AsStringVariable.SetData(row, rawField);
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
                ParameterBag outputParameters = new ParameterBag();
                outputParameters.AddOutput("output", outputFrame);
                return outputParameters;
            }
            finally
            {
                sr.Close();
            }
        }


        public static StepResult FileExportWorksheet(ITemplateHost host, ParameterBag parameters)
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
                    bool useTabDelimiter = saveFileDialog.FileName.Substring(saveFileDialog.FileName.Length - 3).ToLower() == "tab";
                    using (StreamWriter sw = File.CreateText(saveFileDialog.FileName))
                    {
                        host.StartProgress("Exporting Worksheet", true);

                        //  Titles
                        string delimiter = useTabDelimiter ? "\t" : ",";
                        bool first = true;
                        for (int c = 0; c < data.VariableCount; c++)
                        {
                            StringVariable v = data.Variables[c].AsStringVariable;
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
                                StringVariable v = data.Variables[c].AsStringVariable;
                                if (first)
                                    first = false;
                                else
                                    sw.Write(delimiter);
                                string buf = (v.Length > r) ? v.Data[r] : "";
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
            return new StepResult(StepSuccess.Success, new ParameterBag());
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
