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
            using (OpenFileDialog C_FD = new OpenFileDialog())
            {
                C_FD.ShowHelp = true;
                C_FD.Title = "Import worksheet data";
                C_FD.Filter = "Comma delimited (*.csv)|*.csv|Tab delimited (*.tab)|*.tab|Text file (*.txt)|*.txt|All files (*.*)|*.*";
                C_FD.CheckFileExists = true;

                DialogResult result = C_FD.ShowDialog(SDApplication.SoleInstance.MainWindow);
                if (DialogResult.OK != result)
                {
                    return null;
                }

                // If it's an Excel file, load it instead
                string suffix = Path.GetExtension(C_FD.FileName);
                if (".xls".Equals(suffix) || ".xlsx".Equals(suffix))
                {
                    throw new Exception("Please Open an excel file rather than Importing it.");
                }

                host.StartProgress("Importing data");
                try
                {
                    ParameterBag outputParameters = FileImportAscii(C_FD.FileName);
                    return new StepResult(StepSuccess.Success, outputParameters);
                }
                finally
                {
                    host.FinishProgress();
                }
            }
        }


        public static StepResult FileImportReport(ITemplateHost Host, ParameterBag Parameters)
        {
            // import text to the active report
            using (OpenFileDialog C_FD = new OpenFileDialog())
            {
                C_FD.Title = "Import Text";
                C_FD.Filter = "ASCII Text (*.txt)|*.txt|All files (*.*)|*.*";
                C_FD.CheckFileExists = true;

                DialogResult result = C_FD.ShowDialog(SDApplication.SoleInstance.MainWindow);
                if (DialogResult.OK != result)
                {
                    return null;
                }
                using (StreamReader sr = File.OpenText(C_FD.FileName))
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

                // if the file contains Tabs read as tab-delimited
                // else read as comma delimited
                string delimiter = currentLine.Contains("\t") ? "\t" : ",";
                if (currentLine.Substring(currentLine.Length - 1) != delimiter)
                {
                    currentLine += delimiter;
                }
                DataFrame outputFrame = new DataFrame();
                int row = 0;
                do
                {
                    int lastSplitPosition = 0;
                    for (int col = 0; col <= 256; col++)
                    {
                        int splitPosition = currentLine.IndexOf(delimiter, lastSplitPosition, System.StringComparison.Ordinal);
                        if (splitPosition < 0)
                            break;
                        int splitLength = splitPosition - lastSplitPosition;
                        string rawField = currentLine.Substring(lastSplitPosition, splitLength);
                        // Trim leading and trailing "..." if both are present
                        if (rawField.StartsWith(@"""") && rawField.EndsWith(@""""))
                            rawField = rawField.Substring(1, rawField.Length - 2).Replace("\"\"", "\"");
                        if (outputFrame.VariableCount <= col)
                        {
                            StringVariable v = new StringVariable();
                            outputFrame.Variables.Add(v);
                        }
                        outputFrame.Variables[col].AsStringVariable.set_Data(row, rawField);
                        lastSplitPosition += splitLength + 1;
                    }
                    if (sr.EndOfStream)
                        break;
                    currentLine = sr.ReadLine();
                    if (null == currentLine)
                        break;

                    if (currentLine.Length > 0 && currentLine.Substring(currentLine.Length - 1) != delimiter)
                    {
                        currentLine += delimiter;
                    }
                    row += 1;
                    //  TODO: If Host.UpdateProgress(Loc(ff) / LOF(ff) * 128) Then Exit Do
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
            using (SaveFileDialog C_FD = new SaveFileDialog())
            {
                C_FD.Title = "Export Data";
                C_FD.Filter = "Comma delimited (*.csv)|*.csv|Tab delimited (*.tab)|*.tab";
                DataFrame data = parameters["data"].AsDataFrame;
                string source = data.Name;
                C_FD.FileName = source.Contains(".") ? source.Substring(0, source.Length - 4) + ".csv" : source + ".csv";
                C_FD.OverwritePrompt = true;
                DialogResult result = C_FD.ShowDialog(SDApplication.SoleInstance.MainWindow);
                if (DialogResult.OK == result)
                {
                    bool tabout = C_FD.FileName.Substring(C_FD.FileName.Length - 3).ToLower() == "tab";
                    using (StreamWriter sw = File.CreateText(C_FD.FileName))
                    {
                        host.StartProgress("Exporting Worksheet");

                        //  Titles
                        string a = "";
                        int C1;
                        string buf;
                        for (C1 = 0; C1 <= data.VariableCount - 1; C1++)
                        {
                            StringVariable v = data.Variables[C1].AsStringVariable;
                            buf = v.Title;
                            if (tabout)
                            {
                                a += buf.Trim() + "\t";
                            }
                            else
                            {
                                a += buf.Trim() + ",";
                            }
                        }
                        sw.WriteLine(a.Substring(0, a.Length - 1));

                        //  Data
                        int row = data.MaxRows;
                        int r1;
                        for (r1 = 0; r1 <= data.MaxRows - 1; r1++)
                        {
                            a = "";
                            for (C1 = 0; C1 <= data.VariableCount - 1; C1++)
                            {
                                StringVariable v = data.Variables[C1].AsStringVariable;
                                buf = "";
                                if (v.Length > r1)
                                {
                                    buf = v.Data[r1];
                                }
                                if (tabout)
                                {
                                    a += buf.Trim() + "\t";
                                }
                                else
                                {
                                    a += buf.Trim() + ",";
                                }
                            }
                            sw.WriteLine(a.Substring(0, a.Length - 1));
                            if (host.UpdateProgress(r1 / (double)row))
                            {
                                break;
                            }
                        }
                        host.FinishProgress();
                    }
                }
            }
            return new StepResult(StepSuccess.Success, new ParameterBag());
        }
    }
}
