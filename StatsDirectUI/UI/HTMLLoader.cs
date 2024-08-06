using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using System.Linq;
using System;
using System.Web;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Policy;
using DevExpress.Data.Svg;

namespace StatsDirect.UI
{
    class HTMLLOADER
    {
        public static Dictionary<string, string[]> urlData = new Dictionary<string, string[]>();

        private static int CellReferenceToIndex(Cell cell)
        {
            int index = 0;
            string reference = cell.CellReference.ToString().ToUpper();
            foreach (char ch in reference)
            {
                if (Char.IsLetter(ch))
                {
                    int value = (int)ch - (int)'A';
                    index = (index == 0) ? value : ((index + 1) * 26) + value;
                }
                else
                {
                    return index;
                }
            }
            return index;
        }
        public void LoadXLSX(string folder)
        {
            // Open a SpreadsheetDocument based on a file path.
//            string filePath = "E:\\work\\StatsDirect\\git\\statsdirect4\\StatsDirectUI\\Assets\\SD Help ID to File Name Mapping.xlsx";
            string filePath = folder + "\\SD Help ID to File Name Mapping.xlsx";
            // Open the spreadsheet document for read-only access.
            try
            {
                using (SpreadsheetDocument document = SpreadsheetDocument.Open(filePath, false))
                {
                    // Retrieve a reference to the workbook part.
                    WorkbookPart workbookPart = document.WorkbookPart;

                    foreach (Sheet sheet in workbookPart.Workbook.Sheets)
                    {
                        WorksheetPart worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id);
                        Worksheet worksheet = worksheetPart.Worksheet;

                        //                    var sheetData = new List<List<string>>();
                        var sheetData = new List<string[]>();
                        foreach (Row row in worksheet.Descendants<Row>())
                        {
                            try
                            {
                                string[] tempRow = new string[4];
                                for (int i = 0; i < row.Descendants<Cell>().Count(); i++)
                                {
                                    Cell cell = row.Descendants<Cell>().ElementAt(i);
                                    int actualCellIndex = CellReferenceToIndex(cell);
                                    tempRow[actualCellIndex] = GetCellValue(document, cell);
                                }
                                urlData[tempRow[3]] = tempRow;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(ex.Message);
                            }
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message); 
            }

        }

        // Method to get the cell value.
        private static string GetCellValue(SpreadsheetDocument document, Cell cell)
        {
            if (cell.CellValue == null)
            {
                return null;
            }

            string value = cell.CellValue.Text;

            // Check the data type of the cell.
            if (cell.DataType != null && cell.DataType.Value == CellValues.SharedString)
            {
                SharedStringTablePart stringTable = document.WorkbookPart.SharedStringTablePart;
                value = stringTable.SharedStringTable.ChildElements[Int32.Parse(value)].InnerText;
            }

            return value;
        }

        public void OpenURL( string folder, string activeTopic)
        {
            // activeTopic
            try
            {
                string filePath = "file:///" + folder + "\\WebHelp\\default.htm";
                string startingFolder = folder + "\\WebHelp\\";

                string[] pages = urlData[activeTopic];
                char[] charsToTrim = { '/' };

                /////////////////////////////////////////////
                string directHelpLocation = folder + "\\WebHelp\\" + pages[2].TrimStart(charsToTrim);
                Uri directHelpUri = new Uri(directHelpLocation);

                /////////////////////////////////////////////

                Process p = Process.Start(new ProcessStartInfo
                {
                    FileName = directHelpUri.ToString(),
                    UseShellExecute = true,
                    WorkingDirectory = startingFolder,
                });
            }
            catch (System.ComponentModel.Win32Exception e)
            {
                // Handle the case where the browser couldn't be opened
                Console.WriteLine("An error occurred: " + e.Message);
            }
        }
    }

}
