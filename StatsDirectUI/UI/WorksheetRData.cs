using SpreadsheetGear;
using StatsDirect.R;
using StatsDirect.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace StatsDirect.UI;

internal partial class frmSpreadsheetGear
{
    internal WorksheetRImportOptions DescribeWorksheetRData() => workbookView.WithLock(() =>
    {
        var sheet = workbookView.ActiveWorksheet;
        var cells = sheet.Cells;
        var used = WorksheetDataRange(sheet.UsedRange);
        if (used == null)
            return new WorksheetRImportOptions(sheet.Name, null, null);

        var entireSheet = Describe(used);
        var selected = workbookView.RangeSelection;
        if (selected.AreaCount != 1)
            return new WorksheetRImportOptions(sheet.Name, entireSheet, null, "select one rectangular range");

        bool wholeColumns = selected.RowCount == cells.RowCount;
        bool wholeRows = selected.ColumnCount == cells.ColumnCount;
        if (wholeColumns && wholeRows)
            return new WorksheetRImportOptions(sheet.Name, entireSheet, entireSheet);

        int top = selected.Row;
        int bottom = selected.Row + selected.RowCount - 1;
        int left = wholeRows ? used.Column : selected.Column;
        int right = wholeRows ? used.Column + used.ColumnCount - 1 : selected.Column + selected.ColumnCount - 1;
        if (wholeColumns)
        {
            // Search the chosen columns within the data bounds, then keep the chosen columns in the import.
            int firstColumn = Math.Max(left, used.Column);
            int lastColumn = Math.Min(right, used.Column + used.ColumnCount - 1);
            var data = firstColumn <= lastColumn
                ? WorksheetDataRange(cells[used.Row, firstColumn, used.Row + used.RowCount - 1, lastColumn])
                : null;
            if (data == null)
                return new WorksheetRImportOptions(sheet.Name, entireSheet, null, "no data in these columns");
            top = data.Row;
            bottom = data.Row + data.RowCount - 1;
        }

        return new WorksheetRImportOptions(sheet.Name, entireSheet, Describe(cells[top, left, bottom, right]));

        WorksheetRData Describe(IRange range)
        {
            return new WorksheetRData(range.Address, range.RowCount, range.ColumnCount, HasColumnNames(range),
                (useHeaders, format, useFriendlyNames, folder) =>
                    workbookView.WithLock(() => WorksheetRData.WriteScript(range, useHeaders, format, useFriendlyNames, folder)));
        }
    });

    private static bool HasColumnNames(IRange range)
    {
        if (range.RowCount < 2) return false;

        bool hasName = false;
        for (int column = 0; column < range.ColumnCount; column++)
        {
            object value = range[0, column].Value;
            if (value == null || value is string blank && string.IsNullOrWhiteSpace(blank)) continue;

            // Numeric-looking text values can be data copied from another application.
            if (value is not string text || !text.Any(char.IsLetter) || bool.TryParse(text, out _) ||
                double.TryParse(text, out _) || double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                return false;
            hasName = true;
        }
        return hasName;
    }

    private static IRange WorksheetDataRange(IRange used)
    {
        var sheet = used.Worksheet;
        if (sheet.AutoFilterMode) return FilteredWorksheetDataRange(used);

        // UsedRange can extend past the data because of formatting.
        var topLeft = used[0, 0];
        var bottomRight = used[used.RowCount - 1, used.ColumnCount - 1];
        IRange Find(IRange after, SearchOrder order, SearchDirection direction) =>
            used.Find("*", after, FindLookIn.Formulas, LookAt.Part, order, direction, false);

        var first = Find(bottomRight, SearchOrder.ByRows, SearchDirection.Next);
        if (first == null) return null;
        int lastRow = Find(topLeft, SearchOrder.ByRows, SearchDirection.Previous).Row;
        int firstColumn = Find(bottomRight, SearchOrder.ByColumns, SearchDirection.Next).Column;
        int lastColumn = Find(topLeft, SearchOrder.ByColumns, SearchDirection.Previous).Column;
        return sheet.Cells[first.Row, firstColumn, lastRow, lastColumn];
    }

    private static IRange FilteredWorksheetDataRange(IRange used)
    {
        // Find skips filtered-out rows. Read bounded blocks so the import includes those rows.
        int firstRow = int.MaxValue, firstColumn = int.MaxValue, lastRow = -1, lastColumn = -1;
        var cells = used.Worksheet.Cells;
        int columns = used.ColumnCount;
        int batchRows = Math.Max(1, 16_384 / columns);
        for (int start = 0; start < used.RowCount; start += batchRows)
        {
            int rows = Math.Min(batchRows, used.RowCount - start);
            int rowOffset = used.Row + start;
            var values = cells[rowOffset, used.Column, rowOffset + rows - 1, used.Column + columns - 1].Value;
            if (values is object[,] block)
            {
                for (int row = 0; row < rows; row++)
                    for (int column = 0; column < columns; column++)
                        if (block[row, column] != null) Include(rowOffset + row, used.Column + column);
            }
            else if (values != null) Include(rowOffset, used.Column);
        }

        return lastRow < 0 ? null : cells[firstRow, firstColumn, lastRow, lastColumn];

        void Include(int row, int column)
        {
            firstRow = Math.Min(firstRow, row);
            firstColumn = Math.Min(firstColumn, column);
            lastRow = Math.Max(lastRow, row);
            lastColumn = Math.Max(lastColumn, column);
        }
    }
}

internal sealed record WorksheetRImportOptions(string SheetName, WorksheetRData EntireSheet,
    WorksheetRData SelectedCells, string SelectionUnavailableReason = null);

internal enum WorksheetRDataFormat { DataFrame, VectorList }

internal sealed record WorksheetRData(string Address, int RowCount, int ColumnCount,
    bool SuggestHeaders, Func<bool, WorksheetRDataFormat, bool, string, string> WriteImportScript)
{
    internal const string CsvFileName = "worksheet-data.csv";
    private const string MissingValue = "%NA";

    private enum ColumnType { Unknown, Numeric, Logical, Text }

    internal static string WriteScript(IRange range, bool headers, WorksheetRDataFormat format, bool useFriendlyNames, string folder)
    {
        var sheet = range.Worksheet;
        int rows = range.RowCount - (headers ? 1 : 0);
        int columns = range.ColumnCount;
        int firstRow = range.Row + (headers ? 1 : 0);
        string[] names = Enumerable.Range(0, columns).Select(column =>
        {
            object heading = headers ? sheet.Cells[range.Row, range.Column + column].Value : null;
            string title = heading is not ValueError ? CellText(heading) : "";
            return string.IsNullOrWhiteSpace(title) ? "V" + (column + 1).ToString(CultureInfo.InvariantCulture) : title;
        }).ToArray();

        string namesArgument = RStrings(names);
        string nameConversion = headers && useFriendlyNames
            ? "# Turn headings into unique R names that can be used without backticks.\n"
                + "names(worksheet_data) <- make.unique(\n"
                + "  make.names(gsub(\"[[:space:]]+\", \"_\", trimws(names(worksheet_data)))), sep = \"_\")\n"
            : "";
        string vectorConversion = format == WorksheetRDataFormat.VectorList
            ? "# Remove missing values separately for each independent sample.\n"
                + "worksheet_data <- lapply(worksheet_data, function(column) column[!is.na(column)])\n"
            : "";
        if (rows == 0)
            return "# The selected range contains column headings only.\n" +
                $"worksheet_data <- stats::setNames(as.data.frame(matrix(nrow = 0, ncol = {columns})), {namesArgument})\n" +
                nameConversion + vectorConversion;

        string path = Path.GetFullPath(Path.Combine(folder, CsvFileName));
        Directory.CreateDirectory(folder);
        var types = new ColumnType[columns];
        bool escapedText = false;
        using (var writer = new StreamWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write), new UTF8Encoding(false, true)))
        {
            // Read rectangular batches to keep memory bounded for tall and wide selections.
            int batchRows = Math.Max(1, 16_384 / columns);
            for (int start = 0; start < rows; start += batchRows)
            {
                int count = Math.Min(batchRows, rows - start);
                object batch = sheet.Cells[firstRow + start, range.Column, firstRow + start + count - 1, range.Column + columns - 1].Value;
                var values = batch as object[,];
                for (int row = 0; row < count; row++)
                {
                    for (int column = 0; column < columns; column++)
                    {
                        if (column > 0) writer.Write(',');
                        object value = values == null ? batch : values[row, column];
                        ColumnType type = value switch
                        {
                            null or ValueError => ColumnType.Unknown,
                            double number => double.IsFinite(number) ? ColumnType.Numeric : ColumnType.Unknown,
                            bool => ColumnType.Logical,
                            _ => ColumnType.Text
                        };
                        if (type == ColumnType.Unknown)
                        {
                            writer.Write(MissingValue);
                            continue;
                        }

                        types[column] = types[column] == ColumnType.Unknown || types[column] == type ? type : ColumnType.Text;
                        string valueText = CellText(value);
                        if (type == ColumnType.Text)
                        {
                            if (valueText.Contains('%') || valueText.Contains('\r'))
                            {
                                // Preserve literal percent signs and CRs (the R CSV reader normalizes line endings).
                                valueText = valueText.Replace("%", "%25").Replace("\r", "%0D");
                                escapedText = true;
                            }
                            writer.Write('"');
                            writer.Write(valueText.Replace("\"", "\"\""));
                            writer.Write('"');
                        }
                        else writer.Write(valueText);
                    }
                    writer.WriteLine();
                }
            }
        }

        string typesArgument = RStrings(types.Select(type => type switch
        {
            ColumnType.Text => "character",
            ColumnType.Logical => "logical",
            _ => "numeric"
        }));
        var script = new StringBuilder()
            .AppendLine("# Import a snapshot of the worksheet. Keep the CSV to run this script again.")
            .AppendLine("# Blank cells and spreadsheet errors are NA. Dates are worksheet serial numbers.")
            .AppendLine($"worksheet_data <- utils::read.csv({QuotePath(path)},")
            .AppendLine($"  header = FALSE, col.names = {namesArgument},")
            .AppendLine($"  colClasses = {typesArgument}, na.strings = {RScriptWriter.Quote(MissingValue)},")
            .AppendLine("  encoding = \"UTF-8\", check.names = FALSE, blank.lines.skip = FALSE)");
        if (escapedText)
        {
            script.AppendLine("# Restore percent signs and carriage returns in worksheet text.")
                .AppendLine("worksheet_data[] <- lapply(worksheet_data, function(column) {")
                .AppendLine("  if (!is.character(column)) return(column)")
                .AppendLine("  gsub(\"%25\", \"%\", gsub(\"%0D\", \"\\r\", column, fixed = TRUE), fixed = TRUE)")
                .AppendLine("})");
        }
        return script.Append(nameConversion).Append(vectorConversion).ToString();
    }

    private static string CellText(object value)
    {
        string text = value switch
        {
            double number => number.ToString("R", CultureInfo.InvariantCulture),
            bool boolean => boolean ? "TRUE" : "FALSE",
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""
        };
        if (text.Contains('\0'))
            throw new InvalidDataException("The selected worksheet contains a NUL character, which R cannot store in a text value.");
        return text;
    }

    private static string RStrings(IEnumerable<string> values) => "c(" + string.Join(", ", values.Select(QuoteR)) + ")";

    internal static string QuotePath(string path) => QuoteR(path.Replace('\\', '/'));

    private static string QuoteR(string value)
    {
        // Keep the script source ASCII so R can parse it in non-UTF-8 Windows locales.
        var quoted = new StringBuilder();
        foreach (Rune character in RScriptWriter.Quote(value).EnumerateRunes())
        {
            if (character.IsAscii) quoted.Append((char)character.Value);
            else quoted.Append("\\U").Append(character.Value.ToString("x8", CultureInfo.InvariantCulture));
        }
        return quoted.ToString();
    }
}
