using System.IO; 
using System.Windows.Forms;

using StatsDirect.Data;
using StatsDirect.Templates;

namespace StatsDirect.Builtins
{
    public class ImportExport  
    { 
        public static StepResult FileImportWorksheet( ITemplateHost host, ParameterBag parameters ) 
        { 
            // import data to the active worksheet
            OpenFileDialog C_FD = new OpenFileDialog
                                      {
                                          ShowHelp = true,
                                          Title = "Import worksheet data",
                                          Filter =
                                              "Comma delimited (*.csv)|*.csv|Tab delimited (*.tab)|*.tab|Text file (*.txt)|*.txt|All files (*.*)|*.*",
                                          CheckFileExists = true
                                      };

            //  TODO: C_FD.helpid = 11200
            DialogResult result = C_FD.ShowDialog(); 
            if ( DialogResult.OK != result ) 
            { 
                return null; 
            } 
            
            host.StartProgress( "Importing data" );
            ParameterBag outputParameters = FileImportAscii(C_FD.FileName);
            host.FinishProgress(); 
            return new StepResult( StepSuccess.Success, outputParameters ); 
        } 
        
        
        public static StepResult FileImportReport( ITemplateHost Host, ParameterBag Parameters ) 
        { 
            // import text to the active report
            //  TODO: C_FD.helpid = 21100
            OpenFileDialog C_FD = new OpenFileDialog
                                      {
                                          Title = "Import Text",
                                          Filter = "ASCII Text (*.txt)|*.txt|All files (*.*)|*.*",
                                          CheckFileExists = true
                                      };

            DialogResult result = C_FD.ShowDialog(); 
            if ( DialogResult.OK != result ) 
            { 
                return null; 
            } 
            StreamReader sr = File.OpenText( C_FD.FileName ); 
            string fileContents = sr.ReadToEnd(); 
            sr.Close(); 
            ParameterBag outputParameters = new ParameterBag(); 
            outputParameters.AddOutput( "rtf", fileContents ); 
            return new StepResult( StepSuccess.Success, outputParameters ); 
        } 
        
        private static ParameterBag FileImportAscii(string path ) 
        {
            StreamReader sr = File.OpenText( path );
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

                    if (currentLine.Substring(currentLine.Length - 1) != delimiter)
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
        
        
        public static StepResult FileExportWorksheet( ITemplateHost host, ParameterBag parameters ) 
        {
            SaveFileDialog C_FD = new SaveFileDialog
                                      {
                                          Title = "Export Data",
                                          Filter = "Comma delimited (*.csv)|*.csv|Tab delimited (*.tab)|*.tab"
                                      };
            DataFrame data = parameters[ "data" ].AsDataFrame; 
            string source = data.Name; 
            if ( source.Contains( "." ) ) 
            { 
                C_FD.FileName = source.Substring( 0, source.Length - 4) + ".csv"; 
            } 
            else 
            { 
                C_FD.FileName = source + ".csv"; 
            } 
            C_FD.OverwritePrompt = true; 
            DialogResult result = C_FD.ShowDialog(); 
            if ( DialogResult.OK == result ) 
            {
                bool tabout = C_FD.FileName.Substring(C_FD.FileName.Length - 3).ToLower() == "tab"; 
                StreamWriter sw = File.CreateText( C_FD.FileName ); 
                host.StartProgress( "Exporting Worksheet" ); 
                
                //  Titles
                string a = "";
                int C1;
                string buf;
                for ( C1=0; C1 <= data.VariableCount - 1; C1++ ) 
                { 
                    StringVariable v = data.Variables[ C1 ].AsStringVariable; 
                    buf = v.Title; 
                    if ( tabout ) 
                    { 
                        a += buf.Trim() + "\t"; 
                    } 
                    else 
                    { 
                        a += buf.Trim() + ","; 
                    } 
                } 
                sw.WriteLine( a.Substring( 0, a.Length - 1 ) ); 
                
                //  Data
                int row = data.MaxRows;
                int r1;
                for ( r1=0; r1 <= data.MaxRows - 1; r1++ ) 
                { 
                    a = ""; 
                    for ( C1=0; C1 <= data.VariableCount - 1; C1++ ) 
                    { 
                        StringVariable v = data.Variables[ C1 ].AsStringVariable; 
                        buf = ""; 
                        if ( v.Length > r1 ) 
                        { 
                            buf = v.Data[ r1 ]; 
                        } 
                        if ( tabout ) 
                        { 
                            a += buf.Trim() + "\t"; 
                        } 
                        else 
                        { 
                            a += buf.Trim() + ","; 
                        } 
                    } 
                    sw.WriteLine( a.Substring( 0, a.Length - 1 ) ); 
                    if ( host.UpdateProgress( r1 / (double)row ) )
                    { 
                        break;
                    } 
                } 
                host.FinishProgress(); 
                sw.Close(); 
            } 
            return new StepResult( StepSuccess.Success, new ParameterBag() ); 
        } 
    } 
} 
