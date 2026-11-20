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
    internal WorksheetRData DescribeWorksheetRData(out string unavailableReason)
    {
        var (data, reason) = workbookView.WithLock<(WorksheetRData, string)>(() =>
        {
            var sheet = workbookView.ActiveWorksheet;
            var selected = workbookView.RangeSelection;
            if (selected.AreaCount != 1)
                return (null, "Select a single rectangular range to copy worksheet data into R.");

            var used = sheet.UsedRange;
            if (used.RowCount == 1 && used.ColumnCount == 1 && used.Value == null)
                return (null, "The active worksheet has no data to copy into R.");

            if (selected.RowCount == 1 && selected.ColumnCount == 1)
                selected = used;

            // Whole-column and whole-row selections are bounded by the sheet's used range on that axis.
            int top = selected.RowCount == 1_048_576 ? used.Row : selected.Row;
            int bottom = selected.RowCount == 1_048_576 ? used.Row + used.RowCount - 1 : selected.Row + selected.RowCount - 1;
            int left = selected.ColumnCount == 16_384 ? used.Column : selected.Column;
            int right = selected.ColumnCount == 16_384 ? used.Column + used.ColumnCount - 1 : selected.Column + selected.ColumnCount - 1;
            var range = sheet.Cells[top, left, bottom, right];
            bool suggestHeaders = top == used.Row && bottom > top && Enumerable.Range(left, right - left + 1)
                .All(column => sheet.Cells[top, column].Value is string text && !string.IsNullOrWhiteSpace(text));
            return (new WorksheetRData(sheet.Name, range.Address, range.RowCount, range.ColumnCount, suggestHeaders,
                (headers, folder) => workbookView.WithLock(() => WorksheetRData.WriteScript(range, headers, folder))), null);
        });
        unavailableReason = reason;
        return data;
    }
}

internal sealed record WorksheetRData(string SheetName, string Address, int RowCount, int ColumnCount,
    bool SuggestHeaders, Func<bool, string, string> WriteImportScript)
{
    internal const string CsvFileName = "worksheet-data.csv";
    private const string MissingValue = "%NA";

    private enum ColumnType { Unknown, Numeric, Logical, Text }

    internal static string WriteScript(IRange range, bool headers, string folder)
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
        if (rows == 0)
            return "# The selected range contains column headings only.\n" +
                $"worksheet_data <- stats::setNames(as.data.frame(matrix(nrow = 0, ncol = {columns})), {namesArgument})\n";

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
        return script.ToString();
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
