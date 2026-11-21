using System;

namespace StatsDirect.UI;

internal partial class frmSpreadsheetGear
{
    /// <summary>The R import prompt's data choices for the worksheet in view and its selection (WorksheetRImport).</summary>
    internal WorksheetRImportOptions DescribeWorksheetRData() => workbookView.WithLock(() =>
        WorksheetRImport.Describe(workbookView.ActiveWorksheet, workbookView.RangeSelection, write => workbookView.WithLock(write)));
}

internal sealed record WorksheetRImportOptions(string SheetName, WorksheetRData EntireSheet,
    WorksheetRData SelectedCells, string SelectionUnavailableReason = null);

internal enum WorksheetRDataFormat { DataFrame, VectorList }

/// <summary>
/// One data choice of the R import prompt: its cells in A1 notation as the prompt shows them, its size, whether its first row is
/// suggested as headings, and the writer of its import (the CSV in a folder, and the R code that reads it).
/// </summary>
internal sealed record WorksheetRData(string Address, int RowCount, int ColumnCount,
    bool SuggestHeaders, Func<bool, WorksheetRDataFormat, bool, string, string> WriteImportScript)
{
    internal const string CsvFileName = "worksheet-data.csv";

    internal static string QuotePath(string path) => WorksheetRImport.QuotePath(path);

    internal static string ReaderOperand(string path) => WorksheetRImport.ReaderOperand(path);
}
