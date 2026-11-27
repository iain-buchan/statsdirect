using SpreadsheetGear.Windows.Forms;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.Templates;
using StatsDirect.UI;
using StatsDirect.Utilities;
using System.Globalization;

internal static partial class Program
{
    private static void CheckWorksheetLocaleNumberAcquisition()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        using var scratchGrid = new frmSpreadsheetGear();
        var workbook = (WorkbookView)Field(scratchGrid, "workbookView");
        IGetCells cells = scratchGrid;
        try
        {
            var cases = new[]
            {
                (Culture: "en-GB", Decimal: "1.5", Grouped: "1,000", Scientific: "-1,234,567.5e-2",
                    Bad: "1,5", OtherBad: new[] { "12,34.5", "1,,000", "1.5,2", "1e1,5", "1234,567" }),
                (Culture: "en-US", Decimal: "1.5", Grouped: "1,000", Scientific: "-1,234,567.5e-2",
                    Bad: "1,5", OtherBad: new[] { "1,000,", "1,2345", "1.5.2", "1 00.5" }),
                (Culture: "de-DE", Decimal: "1,000", Grouped: "1.000", Scientific: "-1.234.567,5e-2",
                    Bad: "1.5", OtherBad: new[] { "1.23,4", "1..000", "1,5.2", "1e1.5" }),
                (Culture: "fr-FR", Decimal: "1,5", Grouped: "1\u202f000", Scientific: "-1\u202f234\u202f567,5e-2",
                    Bad: "1 5", OtherBad: new[] { "1 23,4", "1.5", "1\u202f00", "1,5\u202f2" })
            };
            foreach (var example in cases)
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(example.Culture);
                Check(WorksheetNumber.Read(example.Decimal, 0, 0) == (example.Culture == "de-DE" ? 1.0 : 1.5)
                    && WorksheetNumber.Read(example.Grouped, 0, 0) == 1000
                    && WorksheetNumber.Read(example.Scientific, 0, 0) == -12345.675,
                    example.Culture + " preserves local decimals, valid grouping and grouped scientific notation");
                foreach (string bad in example.OtherBad)
                    Check(InvalidWorksheetNumberAt(() => WorksheetNumber.Read(bad, 24, 27), "AB25"),
                        $"{example.Culture} reports AB25 for invalid number separators in {bad}");
                WithWorksheet(workbook, sheet =>
                {
                    sheet.Cells["H20:H25"].NumberFormat = "@";
                    sheet.Cells["H20"].Value = "Measurement";
                    sheet.Cells["H21"].Value = example.Grouped;
                    sheet.Cells["H22"].Value = example.Bad;
                    sheet.Cells["H22"].EntireRow.Hidden = true;
                    sheet.Cells["H23"].Value = example.Bad;
                    sheet.Cells["H24"].Value = "*";
                    sheet.Cells["H25"].Value = 1.5;
                    return true;
                });
                Check(cells.GetCellText(22, 7) == example.Bad
                    && InvalidWorksheetNumberAt(() => cells.GetCellValues(7, 19, 24), "H23"),
                    example.Culture + " reports the visible cell's physical address after a hidden row");
                foreach (DataAcquisitionMode mode in new[] { DataAcquisitionMode.NumericSkipMissing, DataAcquisitionMode.NumericReplaceMissing, DataAcquisitionMode.NumericPreserveRows })
                {
                    // H23 on its own looks like a title to the permissive text fallback.
                    Check(InvalidWorksheetNumberAt(() => Acquire(cells, mode, 22, 3), "H23"),
                        $"{example.Culture} {mode} flags a malformed first value instead of treating it as a title");
                }
                var labels = (ClassifierVariable)Acquire(cells, DataAcquisitionMode.CategoryCombineAllColumns, 19, 6).Variables[0];
                Check(labels.Groups.Any(group => group.Label == example.Bad),
                    example.Culture + " keeps the same text as a literal label in categorical selection");
                Check(InvalidWorksheetNumberAt(() => Acquire(cells, DataAcquisitionMode.NumericCodingTextToCategories, 19, 6), "H23"),
                    example.Culture + " flags a misplaced separator in an otherwise numeric predictor");
                Check(InvalidWorksheetNumberAt(() => Acquire(cells, DataAcquisitionMode.NumericCodingTextToDummiesPreserveRows, 19, 6), "H23"),
                    example.Culture + " checks the original address after removing a matched predictor's heading");
                var masked = CellArrayProcessor.ProcessCellArrayNumericCodingPreserveRows(
                    Selection(cells, 19, 6), 0, false, false, 0, codingRows: [true, false, false, true]);
                Check(masked.Variables[0] is DoubleVariable predictor && predictor.Data[1] == Constant.MISSING,
                    example.Culture + " does not flag the predictor on a row excluded by its missing response");
                WithWorksheet(workbook, sheet =>
                {
                    sheet.Cells["H21"].Value = "None";
                    return true;
                });
                var autoLabels = (ClassifierVariable)Acquire(cells, DataAcquisitionMode.NumericCodingTextToCategories, 19, 6).Variables[0];
                Check(autoLabels.Groups.Any(group => group.Label == example.Bad),
                    example.Culture + " keeps the original label in an automatically classified text column");
                WithWorksheet(workbook, sheet =>
                {
                    sheet.Cells["H21"].Value = example.Grouped;
                    sheet.Cells["H23"].Value = example.Decimal;
                    return true;
                });
                double[] values = ((DoubleVariable)Acquire(cells, DataAcquisitionMode.NumericPreserveRows, 19, 6).Variables[0]).Data;
                Check(values.SequenceEqual(new[] { 1000.0, example.Culture == "de-DE" ? 1.0 : 1.5, Constant.MISSING, 1.5 }),
                    example.Culture + " acquires corrected text, a true number and a missing marker without shifting visible rows");
                WithWorksheet(workbook, sheet =>
                {
                    sheet.Cells["H20"].Value = example.Bad;
                    return true;
                });
                var replay = CellArrayProcessor.ProcessCellArray(Selection(cells, 19, 6), DataAcquisitionMode.NumericPreserveRows, 0, true, true, 0, "Locale numbers");
                Check(((DoubleVariable)replay.Variables[0]).Data.SequenceEqual(values),
                    example.Culture + " honours the stored heading when rereading a recorded selection");
            }

            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Check(WorksheetNumber.Read("1 000,5", 0, 0) == 1000.5
                && WorksheetNumber.Read(null, 0, 0) == Constant.MISSING
                && WorksheetNumber.Read("MISSING", 0, 0) == Constant.MISSING * 10D
                && WorksheetNumber.Read(".", 0, 0) == Constant.MISSING * 10D
                && WorksheetNumber.Read("Group 1, North", 0, 0) == Constant.MISSING,
                "the numeric reader preserves the French space alias, missing markers and ordinary text handling");

            using var inlineGrid = new frmSpreadsheetGear();
            var inline = (WorkbookView)Field(inlineGrid, "workbookView");
            WithWorksheet(inline, sheet =>
            {
                sheet.Cells["E9"].NumberFormat = "@";
                sheet.Cells["E9"].Value = "1 23,4";
                return true;
            });
            var frameParameter = new FrameParameter { Name = "frame" };
            var output = new ParameterBag();
            var extractor = new InlineParameterValueExtractor { Control = inline, OutputParameters = output, DoValidation = false };
            extractor.Visit(frameParameter);
            Check(output.ContainsKey("frame"), "an unfinished inline number does not raise a dialog during live settings evaluation");
            extractor.DoValidation = true;
            Check(InvalidWorksheetNumberAt(() => extractor.Visit(frameParameter), "E9"),
                "an inline table names an invalid cell relative to the worksheet rather than the table");
            using var special = new TableLayoutPanel();
            special.Controls.Add(inline, 1, 1);
            extractor.Control = special;
            foreach (string type in new[] { "chi-2-column", "raters-2d" })
                Check(InvalidWorksheetNumberAt(() => extractor.Visit(new SpecialParameter { Name = "counts", SpecialType = type }), "E9"),
                    $"the {type} inline table names the actual invalid cell");
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }

        static DataFrame Acquire(IGetCells grid, DataAcquisitionMode mode, int row, int count) =>
            CellArrayProcessor.ProcessCellArray(Selection(grid, row, count), mode, 0, false, false, 0, "Locale numbers");

        static CellSelection Selection(IGetCells grid, int row, int count)
        {
            var selection = new CellSelection { LongestRowCount = count };
            selection.ColumnSelections.Add(new CellColumnSelection(grid) { ColumnIndex = 7, RowIndex = row, RowCount = count });
            return selection;
        }
    }

    private static bool InvalidWorksheetNumberAt(Action read, string address)
    {
        try { read(); }
        catch (InvalidOperationException error)
        {
            return error.Message.StartsWith("Cell " + address + " has a misplaced number separator.", StringComparison.Ordinal);
        }
        return false;
    }
}
