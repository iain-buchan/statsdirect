using StatsDirect.UI.WebReports;

namespace StatsDirect.UI;

internal partial class frmSpreadsheetGear
{
    internal void LoadNotebookTables(ReportNotebookImport.Table[] tables, string title)
    {
        workbookView.WithLock(() =>
        {
            var book = workbookView.ActiveWorkbook;
            while (book.Worksheets.Count > 1) book.Worksheets[book.Worksheets.Count - 1].Delete();
            for (int t = 0; t < tables.Length; t++)
            {
                var table = tables[t];
                var sheet = t == 0 ? book.Worksheets[0] : book.Worksheets.Add();
                sheet.Name = SpreadsheetFiles.SheetName(table.Title, book, sheet);
                for (int c = 0; c < table.Columns.Length; c++)
                {
                    SpreadsheetFiles.SetLiteral(sheet, 0, c, table.Columns[c].Title);
                    for (int r = 0; r < table.Columns[c].Values.Length; r++)
                        SpreadsheetFiles.SetLiteral(sheet, r + 1, c, table.Columns[c].Values[r]);
                }
                if (table.Columns.Length > 0) sheet.Cells[0, 0, 0, table.Columns.Length - 1].Font.Bold = true;
            }
            book.Worksheets[0].Select();
        });
        Path = null;
        SetUnsavedName(title + " data");
        Dirty = true;
    }
}
