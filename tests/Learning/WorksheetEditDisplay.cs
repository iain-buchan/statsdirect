using SpreadsheetGear;
using SpreadsheetGear.Windows.Forms;
using StatsDirect.UI;
using StatsDirect.UI.Learning;
using System.Text.Json.Nodes;

internal static partial class Program
{
    private static void TestWorksheetEditDisplay(frmMain main)
    {
        var previous = main.ActiveMdiChild;
        string path = Path.Combine(root, "worksheet-edit-display-1904.xlsx");
        var blank = frmSpreadsheetGear.CreateWorksheetEditPreviewWorkbook(Factory.GetWorkbookSet(), date1904: true);
        try { blank.SaveAs(path, FileFormat.OpenXMLWorkbook); }
        finally { blank.Close(); }

        var grid = (frmSpreadsheetGear)main.CreateGrid(path, true, "Worksheet edit display check");
        var view = (WorkbookView)Field(grid, "workbookView");
        var firstDate = new DateTime(2026, 11, 23);
        try
        {
            double serial = WithWorksheet(view, sheet =>
            {
                Check(sheet.Workbook.Date1904, "the test opens a real workbook that uses the 1904 date system");
                double value = (firstDate - new DateTime(1904, 1, 1)).TotalDays;
                sheet.Cells["A1"].NumberFormat = "yyyy-mm-dd";
                sheet.Cells["A1"].Value = value;
                sheet.Cells["A2"].NumberFormat = "hh:mm";
                sheet.Cells["A2"].Value = 0.375;
                sheet.Cells["A3:A4"].NumberFormat = "0%";
                sheet.Cells["A3"].Value = 0.25;
                sheet.Cells["A4"].Formula = "=1/4";
                sheet.Cells["A5"].Value = "original text";
                sheet.Cells["A6"].NumberFormat = ";;;";
                sheet.Cells["A6"].Value = 7d;
                return value;
            });

            int version = grid.LearningVersion;
            bool dirty = grid.Dirty;
            bool canUndo = grid.CanUndo;
            var proposal = grid.CaptureWorksheetEditTarget().Propose(new JsonObject
            {
                ["range"] = "A1:A6",
                ["values"] = new JsonArray(
                    new JsonArray(serial + 1), new JsonArray(0.5), new JsonArray(0.75),
                    new JsonArray(0.5), new JsonArray("=2+2"), new JsonArray(new JsonNode[] { null }))
            });
            var edits = proposal.PreviewCells;
            Check(edits.Select(cell => cell.Address).SequenceEqual(["A1", "A2", "A3", "A4", "A5", "A6"]),
                "edit preview uses plain A1 addresses");
            Check(edits[0].BeforeText == "2026-11-23" && edits[0].AfterText == "2026-11-24",
                "edit preview formats dates before and after using the source workbook's 1904 date system");
            Check(edits[1].BeforeText == "09:00" && edits[1].AfterText == "12:00",
                "edit preview formats both time values using the existing time format");
            Check(edits[2].BeforeText == "25%" && edits[2].AfterText == "75%",
                "edit preview formats both percentages rather than showing their fractions");
            Check(edits[3].BeforeText == "25% (formula =1/4)" && edits[3].AfterText == "50%",
                "edit preview identifies the formatted calculation and its original formula before replacement");
            Check(edits[4].BeforeText == "\"original text\"" && edits[4].AfterText == "\"=2+2\"",
                "edit preview distinguishes formula-like literal text from a worksheet formula");
            Check(edits[5].BeforeText == "(hidden by cell format)" && edits[5].AfterText == "(blank)",
                "edit preview distinguishes existing content hidden by a format from a cleared cell");
            Check(grid.LearningVersion == version && grid.Dirty == dirty && grid.CanUndo == canUndo &&
                WithWorksheet(view, sheet => sheet.Cells["A1"].Text == "2026-11-23" && sheet.Cells["A4"].Formula == "=1/4"),
                "formatting the preview leaves the worksheet values, version and undo history unchanged");

            proposal.Apply();
            Check(WithWorksheet(view, sheet => sheet.Cells["A1"].Text == edits[0].AfterText &&
                sheet.Cells["A2"].Text == edits[1].AfterText && sheet.Cells["A3"].Text == edits[2].AfterText &&
                sheet.Cells["A4"].Text == edits[3].AfterText && !sheet.Cells["A4"].HasFormula &&
                (string)sheet.Cells["A5"].Value == "=2+2" && !sheet.Cells["A5"].HasFormula && sheet.Cells["A6"].Value == null),
                "actual worksheet displays match the approved formatted preview");
            grid.EditUndo();
            Check(WithWorksheet(view, sheet => sheet.Cells["A1"].Text == "2026-11-23" &&
                sheet.Cells["A4"].Formula == "=1/4" && sheet.Cells["A4"].Text == "25%"),
                "Undo restores the original date and formatted formula");
        }
        finally
        {
            CloseTestDocument(grid);
            previous?.Activate();
        }
    }
}
