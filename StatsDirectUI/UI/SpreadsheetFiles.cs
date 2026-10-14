using ExcelDataReader;
using SpreadsheetGear;
using SpreadsheetGear.Advanced.Cells;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace StatsDirect.UI;

internal static class SpreadsheetFiles
{
    internal static bool CanOpen(string extension) => extension is ".xls" or ".xlsx" or ".xlsm" or ".xlsb" or ".xlt" or ".xltx" or ".xltm" or ".csv" or ".tsv" or ".tab";
    internal static bool Imported(string extension) => extension is ".csv" or ".tsv" or ".tab" or ".xlsb" or ".xlt" or ".xltx" or ".xltm";
    internal static IWorkbook Open(IWorkbookSet set, string path)
    {
        string extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension is not (".csv" or ".tsv" or ".tab" or ".xlsb")) return set.Workbooks.Open(path);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var input = File.OpenRead(path);
        bool csv = extension != ".xlsb";
        using var reader = csv ? ExcelReaderFactory.CreateCsvReader(input, new ExcelReaderConfiguration
        { AutodetectSeparators = extension is ".tab" or ".tsv" ? ['\t'] : [',', ';'], TrimWhiteSpace = false,
            FallbackEncoding = Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.ANSICodePage, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback) }) : ExcelReaderFactory.CreateReader(input);
        var book = set.Workbooks.Add();
        try
        {
            int sheetIndex = 0;
            do
            {
                if (reader.FieldCount > 16384) throw new InvalidDataException("The table exceeds Excel's column limit.");
                var sheet = sheetIndex++ == 0 ? book.Worksheets[0] : book.Worksheets.Add();
                sheet.Name = SheetName(reader.Name ?? Path.GetFileNameWithoutExtension(path), book, sheet);
                int row = 0;
                while (reader.Read())
                {
                    if (row >= 1048576) throw new InvalidDataException("The table exceeds Excel's row limit.");
                    for (int c = 0; c < reader.FieldCount; c++) SetLiteral(sheet, row, c, csv ? CsvValue(reader.GetString(c)) : reader.GetValue(c));
                    row++;
                }
            } while (reader.NextResult());
            return book;
        }
        catch { book.Close(); throw; }
    }
    // IRange.Value parses strings as Excel input. The fast typed API preserves
    // literal identifiers and leading '=' without interpreting them as formulas.
    internal static void SetLiteral(IWorksheet sheet, int row, int column, object value)
    {
        var cells = (IValues)sheet;
        switch (value)
        {
            case null: break;
            case string text: cells.SetText(row, column, text); break;
            case bool boolean: cells.SetLogical(row, column, boolean); break;
            case DateTime date: cells.SetNumber(row, column, date.ToOADate()); sheet.Cells[row,column].NumberFormat = "yyyy-mm-dd hh:mm:ss"; break;
            default: cells.SetNumber(row, column, Convert.ToDouble(value, CultureInfo.InvariantCulture)); break;
        }
    }
    private static object CsvValue(string value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        if (Regex.IsMatch(value, @"^[+-]?\d{16,}$")) return value; // Excel's numeric precision cannot preserve long identifiers.
        // Keep identifiers, surrounding whitespace and formula-like text literal.
        if (value == value.Trim() && !Regex.IsMatch(value, @"^[+-]?0\d") && Regex.IsMatch(value, @"^[+-]?(?:\d+\.?\d*|\.\d+)(?:[eE][+-]?\d+)?$") &&
            double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) && double.IsFinite(number)) return number;
        return value;
    }
    internal static string SheetName(string name, IWorkbook book, IWorksheet current)
    {
        name = Regex.Replace(name, @"[\[\]:*?/\\]", "_").Trim('\'');
        if (name.Length == 0) name = "Data";
        name = name[..Math.Min(name.Length, 31)]; string stem = name; int n = 1;
        while (book.Worksheets.Cast<IWorksheet>().Any(s => s != current && s.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        { string suffix = " " + (++n); name = stem[..Math.Min(stem.Length, 31-suffix.Length)] + suffix; }
        return name;
    }
}
