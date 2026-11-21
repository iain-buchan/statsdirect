using SpreadsheetGear;
using StatsDirect.R;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace StatsDirect.UI;

/// <summary>
/// Worksheet data sent to R by the worksheet toolbar's R and R+ buttons, by the rules StatsDirect for Windows and for Mac share:
/// statisticalhelp WorksheetR/RULES.md (rule numbers below are its), tested against WorksheetR/examples.json by
/// tests/WorksheetRExamples.  The data travels in a CSV file that the script reads with utils::read.csv, one R type per column.
/// </summary>
internal static class WorksheetRImport
{
    internal const string NoSelection = "no selection";
    internal const string OneRectangle = "select one rectangle, or whole columns or rows";
    internal const string NoDataInSelection = "no data in the selection";

    // ---------------------------------------------------------------------------------------------------------------- 2. Which cells

    /// <summary>The prompt's data choices for the sheet in view and its selection (rules 1.3 to 1.5 and section 2).</summary>
    internal static WorksheetRImportOptions Describe(IWorksheet sheet, IRange selection, Func<Func<string>, string> withLock)
    {
        // Rule 3.11: formula cells send their current values.  A workbook that calculates manually can hold results older than the
        // cells they come from, so it is calculated first, as F9 does.
        var calculation = sheet.Workbook.WorkbookSet;
        if (calculation.Calculation != Calculation.Automatic) calculation.Calculate();
        WorksheetRSource entire = Entire(sheet);
        var (selected, reason) = Selected(sheet, selection, entire);
        return new WorksheetRImportOptions(sheet.Name, entire == null ? null : Data(entire, withLock),
            selected == null ? null : Data(selected, withLock), reason);
    }

    private static WorksheetRData Data(WorksheetRSource source, Func<Func<string>, string> withLock) =>
        new(source.LabelAreas, source.Rows.Length, source.Columns.Length, SuggestHeadings(source),
            (headers, format, friendly, folder) => withLock(() => WriteScript(source, headers, format, friendly, folder)));

    /// <summary>Rule 2.1: the smallest rectangle holding every non-blank cell, or null when there is none.</summary>
    internal static WorksheetRSource Entire(IWorksheet sheet)
    {
        var bounds = Bounds(sheet.UsedRange);
        return bounds is not var (top, bottom, left, right) ? null
            : new WorksheetRSource(sheet, Span(top, bottom), Span(left, right));
    }

    /// <summary>Rules 2.2 to 2.6 and C6, C7: the selected cells, or why they cannot be sent.</summary>
    internal static (WorksheetRSource Source, string Reason) Selected(IWorksheet sheet, IRange selection, WorksheetRSource entire)
    {
        if (selection == null || selection.AreaCount == 0) return (null, NoSelection);
        var cells = sheet.Cells;
        var areas = Enumerable.Range(0, selection.AreaCount).Select(i => selection.Areas[i]).ToList();
        bool WholeColumns(IRange area) => area.RowCount == cells.RowCount;
        bool WholeRows(IRange area) => area.ColumnCount == cells.ColumnCount;
        var used = sheet.UsedRange;

        if (areas.Any(area => WholeColumns(area) && WholeRows(area)))   // 2.6 select all
            return entire == null ? (null, NoDataInSelection) : (entire, null);

        if (areas.All(WholeColumns))   // 2.3 and C7: the selected columns, from the first to the last non-blank row of those columns
        {
            int[] columns = areas.SelectMany(area => Enumerable.Range(area.Column, area.ColumnCount)).Distinct().Order().ToArray();
            if (columns.Length == cells.ColumnCount)   // 2.6: every column is every cell, however it was selected
                return entire == null ? (null, NoDataInSelection) : (entire, null);
            int first = int.MaxValue, last = -1;
            foreach (var (from, to) in WorksheetRSource.Runs(columns))
                if (Bounds(cells[used.Row, from, used.Row + used.RowCount - 1, to]) is var (top, bottom, _, _))
                {
                    first = Math.Min(first, top);
                    last = Math.Max(last, bottom);
                }
            return last < 0 ? (null, NoDataInSelection) : (new WorksheetRSource(sheet, Span(first, last), columns), null);
        }

        if (areas.All(WholeRows))   // C6: from the first to the last selected row holding data, across that data's columns
        {
            int[] rows = areas.SelectMany(area => Enumerable.Range(area.Row, area.RowCount)).Distinct().Order().ToArray();
            if (rows.Length == cells.RowCount)   // 2.6: every row is every cell, however it was selected
                return entire == null ? (null, NoDataInSelection) : (entire, null);
            int top = int.MaxValue, bottom = -1, left = int.MaxValue, right = -1;
            foreach (var (from, to) in WorksheetRSource.Runs(rows))
                if (Bounds(cells[from, used.Column, to, used.Column + used.ColumnCount - 1]) is var (t, b, l, r))
                {
                    top = Math.Min(top, t); bottom = Math.Max(bottom, b);
                    left = Math.Min(left, l); right = Math.Max(right, r);
                }
            return bottom < 0 ? (null, NoDataInSelection)
                : (new WorksheetRSource(sheet, rows.Where(row => row >= top && row <= bottom).ToArray(), Span(left, right)), null);
        }

        if (areas.Count != 1) return (null, OneRectangle);   // C7
        var rectangle = areas[0];
        // Only the rectangle's part within the used range can hold data; the rectangle itself is what is sent.
        int firstRow = Math.Max(rectangle.Row, used.Row), lastRow = Math.Min(rectangle.Row + rectangle.RowCount, used.Row + used.RowCount) - 1;
        int firstColumn = Math.Max(rectangle.Column, used.Column), lastColumn = Math.Min(rectangle.Column + rectangle.ColumnCount, used.Column + used.ColumnCount) - 1;
        bool holdsData = firstRow <= lastRow && firstColumn <= lastColumn && Bounds(cells[firstRow, firstColumn, lastRow, lastColumn]) != null;
        return !holdsData ? (null, NoDataInSelection)   // 2.2
            : (new WorksheetRSource(sheet, Span(rectangle.Row, rectangle.Row + rectangle.RowCount - 1),
                Span(rectangle.Column, rectangle.Column + rectangle.ColumnCount - 1)), null);
    }

    private static int[] Span(int first, int last) => Enumerable.Range(first, last - first + 1).ToArray();

    /// <summary>The rows and columns of the non-blank cells within a range: a constant or a formula, text of white space included.</summary>
    private static (int Top, int Bottom, int Left, int Right)? Bounds(IRange range)
    {
        if (!range.Worksheet.AutoFilterMode) return FoundBounds(range);
        // While an AutoFilter is on, Find passes over every hidden row (whether the filter or the user hid it), and the columns it gives
        // can leave out a column whose cells are all in shown rows that are not one block (the independent review of 0b9c2282).  So the
        // bounds come from the import's own copy of the sheet (TryCopyBounds).  Should no copy serve, the visible rows' bounds from Find
        // are taken, with the hidden rows added: few hidden cells are read one by one; otherwise each hidden row's first and last cell
        // with contents come from the row's ends, as Ctrl+arrow finds them, which pass over hidden columns, so those columns' cells are
        // read one by one.
        if (TryCopyBounds(range, out var copied)) return copied;
        var bounds = FoundBounds(range);
        var cells = range.Worksheet.Cells;
        int last = range.Row + range.RowCount - 1, first = range.Column, right = range.Column + range.ColumnCount - 1;
        var runs = new List<(int From, int To)>();
        long hiddenRows = 0;
        for (int row = range.Row; row <= last; row++)
        {
            if (!cells[row, 0].EntireRow.Hidden) continue;
            int end = row;
            while (end < last && cells[end + 1, 0].EntireRow.Hidden) end++;
            runs.Add((row, end));
            hiddenRows += end - row + 1;
            row = end;
        }
        void Add(int top, int bottom, int left, int rightmost) => bounds = bounds is var (t, b, l, r)
            ? (Math.Min(t, top), Math.Max(b, bottom), Math.Min(l, left), Math.Max(r, rightmost)) : (top, bottom, left, rightmost);
        if (hiddenRows * range.ColumnCount <= 1_000_000)
        {
            foreach (var (from, to) in runs)
                if (ScannedBounds(cells[from, first, to, right]) is var (top, bottom, left, rightmost))
                    Add(top, bottom, left, rightmost);
            return bounds;
        }
        int[] hiddenColumns = Enumerable.Range(first, range.ColumnCount).Where(column => cells[0, column].EntireColumn.Hidden).ToArray();
        var values = (SpreadsheetGear.Advanced.Cells.IValues)range.Worksheet;   // a cell's value without a range for each cell
        foreach (var (from, to) in runs)
            for (int row = from; row <= to; row++)
            {
                int? lo = RowEnd(cells, row, first, right, fromRight: false), hi = lo == null ? null : RowEnd(cells, row, first, right, fromRight: true);
                foreach (int column in hiddenColumns)
                    if (values[row, column] != null)
                    {
                        lo = Math.Min(lo ?? column, column);
                        hi = Math.Max(hi ?? column, column);
                    }
                if (lo is int l && hi is int h) Add(row, row, l, h);
            }
        return bounds;
    }

    // The first (or last) cell with contents of a row within columns first to last, as Ctrl+Left and Ctrl+Right find it from that end:
    // one step from an empty end cell, which passes over hidden columns.  Null when there is none in those columns.
    private static int? RowEnd(IRange cells, int row, int first, int last, bool fromRight)
    {
        var end = cells[row, fromRight ? last : first];
        if (end.Value != null) return end.Column;
        var next = fromRight ? end.EndLeft : end.EndRight;
        return next.Value != null && next.Column >= first && next.Column <= last ? next.Column : null;
    }

    // The bounds within the range from the import's own copy of the sheet, in a workbook of its own that calculates nothing; the copy
    // takes milliseconds, however large the sheet, and the sheet itself is left as it is, protection included.  The copy's filter is
    // turned off, a copy of a sheet protected without a password allowing the program's own changes (ProtectionMode), and Find searches
    // it.  A password keeps the copy's filter on: then its cells are read one by one where they are no more than 50 million, and
    // otherwise the copy alone is saved and the cells it lists are read (TrySavedBounds).  False when none of these serves.
    private static bool TryCopyBounds(IRange range, out (int Top, int Bottom, int Left, int Right)? bounds)
    {
        bounds = null;
        int top = range.Row, bottom = range.Row + range.RowCount - 1, left = range.Column, right = range.Column + range.ColumnCount - 1;
        var book = Factory.GetWorkbook(CultureInfo.InvariantCulture);
        try
        {
            book.WorkbookSet.Calculation = Calculation.Manual;
            var copy = (IWorksheet)range.Worksheet.CopyAfter(book.Worksheets[0]);
            try
            {
                if (copy.ProtectContents) copy.ProtectionMode = true;   // refused, with InvalidOperationException, for a password
                copy.AutoFilterMode = false;
            }
            catch (InvalidOperationException) { }
            if (!copy.AutoFilterMode)
            {
                bounds = FoundBounds(copy.Cells[top, left, bottom, right]);
                return true;
            }
            if ((long)range.RowCount * range.ColumnCount <= 50_000_000)
            {
                bounds = ValueBounds((SpreadsheetGear.Advanced.Cells.IValues)copy, top, bottom, left, right);
                return true;
            }
            book.Worksheets[0].Delete();   // the copy alone, so that the saved workbook lists its cells and no others
            return TrySavedBounds(book, top, bottom, left, right, out bounds);
        }
        catch (Exception e) when (e is not OutOfMemoryException) { return false; }
        finally { book.Close(); }
    }

    // The bounds of the cells with a value or a formula, read one by one.
    private static (int, int, int, int)? ValueBounds(SpreadsheetGear.Advanced.Cells.IValues values, int top, int bottom, int left, int right)
    {
        int first = int.MaxValue, last = -1, leftmost = int.MaxValue, rightmost = -1;
        for (int row = top; row <= bottom; row++)
            for (int column = left; column <= right; column++)
                if (values[row, column] != null)
                {
                    first = Math.Min(first, row); last = Math.Max(last, row);
                    leftmost = Math.Min(leftmost, column); rightmost = Math.Max(rightmost, column);
                }
        return last < 0 ? null : (first, last, leftmost, rightmost);
    }

    // The bounds, within rows firstRow to lastRow and columns firstColumn to lastColumn, of the cells a workbook of one sheet lists when
    // saved: each cell with a value, a formula or inline text.  Saving lists only the cells the sheet holds, protected or not, so this
    // costs as much as the sheet's contents, however many rows and columns are hidden.  SpreadsheetGear leaves out a row's or a cell's
    // reference where it follows the one before, as the file format allows.  False when the workbook cannot be saved or read so.
    private static bool TrySavedBounds(IWorkbook book, int firstRow, int lastRow, int firstColumn, int lastColumn, out (int Top, int Bottom, int Left, int Right)? bounds)
    {
        bounds = null;
        int top = int.MaxValue, bottom = -1, left = int.MaxValue, right = -1;
        try
        {
            byte[] saved = book.SaveToMemory(FileFormat.OpenXMLWorkbook);
            using var zip = new System.IO.Compression.ZipArchive(new MemoryStream(saved), System.IO.Compression.ZipArchiveMode.Read);
            var parts = zip.Entries.Where(entry => entry.FullName.StartsWith("xl/worksheets/", StringComparison.Ordinal)
                && entry.FullName.EndsWith(".xml", StringComparison.Ordinal) && !entry.FullName.Contains("/_rels/", StringComparison.Ordinal)).ToArray();
            if (parts.Length != 1) return false;
            using var reader = System.Xml.XmlReader.Create(parts[0].Open(), new System.Xml.XmlReaderSettings { IgnoreWhitespace = true, DtdProcessing = System.Xml.DtdProcessing.Prohibit });
            int row = -1, column = -1;
            bool inCell = false, content = false, inData = false;
            while (reader.Read())
            {
                if (reader.NodeType == System.Xml.XmlNodeType.Element)
                {
                    switch (reader.LocalName)
                    {
                        case "sheetData": inData = true; break;
                        case "row" when inData:
                            row = reader.GetAttribute("r") is string r ? int.Parse(r, NumberStyles.None, CultureInfo.InvariantCulture) - 1 : row + 1;
                            column = -1;
                            break;
                        case "c" when inData:
                            column = reader.GetAttribute("r") is string reference ? CellColumn(reference) : column + 1;
                            if (column < 0) return false;
                            inCell = !reader.IsEmptyElement;
                            content = false;
                            break;
                        case "v" or "f" or "is" when inCell: content = true; break;
                    }
                }
                else if (reader.NodeType == System.Xml.XmlNodeType.EndElement)
                {
                    if (reader.LocalName == "sheetData") break;
                    if (reader.LocalName == "c" && inCell)
                    {
                        inCell = false;
                        if (content && row >= firstRow && row <= lastRow && column >= firstColumn && column <= lastColumn)
                        {
                            top = Math.Min(top, row); bottom = Math.Max(bottom, row);
                            left = Math.Min(left, column); right = Math.Max(right, column);
                        }
                    }
                }
            }
        }
        catch (Exception e) when (e is not OutOfMemoryException) { return false; }
        bounds = bottom < 0 ? null : (top, bottom, left, right);
        return true;
    }

    // The column of a cell reference such as AB5, from 0; -1 when it is not one.
    private static int CellColumn(string reference)
    {
        int column = 0, i = 0;
        while (i < reference.Length && reference[i] is >= 'A' and <= 'Z')
            column = column * 26 + (reference[i++] - 'A' + 1);
        return i == 0 || i == reference.Length || column > 16_384 ? -1 : column - 1;
    }

    // The bounds Find gives: every non-blank cell, except in rows hidden while an AutoFilter is on.
    private static (int Top, int Bottom, int Left, int Right)? FoundBounds(IRange range)
    {
        var topLeft = range[0, 0];
        var bottomRight = range[range.RowCount - 1, range.ColumnCount - 1];
        IRange Find(IRange after, SearchOrder order, SearchDirection direction) =>
            range.Find("*", after, FindLookIn.Formulas, LookAt.Part, order, direction, false);
        var first = Find(bottomRight, SearchOrder.ByRows, SearchDirection.Next);
        if (first == null) return null;
        return (first.Row, Find(topLeft, SearchOrder.ByRows, SearchDirection.Previous).Row,
            Find(bottomRight, SearchOrder.ByColumns, SearchDirection.Next).Column,
            Find(topLeft, SearchOrder.ByColumns, SearchDirection.Previous).Column);
    }

    private static (int, int, int, int)? ScannedBounds(IRange range)
    {
        int top = int.MaxValue, left = int.MaxValue, bottom = -1, right = -1;
        var cells = range.Worksheet.Cells;
        int columns = range.ColumnCount, batchRows = Math.Max(1, 16_384 / columns);
        for (int start = 0; start < range.RowCount; start += batchRows)
        {
            int rows = Math.Min(batchRows, range.RowCount - start), rowOffset = range.Row + start;
            object values = cells[rowOffset, range.Column, rowOffset + rows - 1, range.Column + columns - 1].Value;
            for (int row = 0; row < rows; row++)
                for (int column = 0; column < columns; column++)
                    if ((values is object[,] block ? block[row, column] : values) != null)
                    {
                        top = Math.Min(top, rowOffset + row); bottom = Math.Max(bottom, rowOffset + row);
                        left = Math.Min(left, range.Column + column); right = Math.Max(right, range.Column + column);
                    }
        }
        return bottom < 0 ? null : (top, bottom, left, right);
    }

    // ------------------------------------------------------------------------------------------------------- 3. Values in each cell

    internal enum Kind { Missing, Word, Number, NumericText, InfinityText, Boolean, BooleanText, Date, DateTime, TimeOfDay, Duration, Text }

    /// <summary>A cell as rule 3 sees it: its kind, its value for R, and the text it shows in a character column.</summary>
    internal readonly record struct Cell(Kind Kind, double Number = 0, bool Boolean = false, string Text = null);

    // 3.2: missing in any column, written exactly so; and the words that count in any case among numbers, Booleans or dates.
    private static readonly HashSet<string> ExactMarkers = new(StringComparer.Ordinal) { "*", "* (missing)", ".", "NA", "N/A", "NaN", "NULL", "MISSING" };
    private static readonly HashSet<string> AnyCaseWords = new(StringComparer.OrdinalIgnoreCase) { "NA", "N/A", "NaN", "NULL", "MISSING" };
    // 3.4: numeric text, ASCII digits only; C9: a leading zero keeps text as text.
    private static readonly Regex NumericPattern = new(@"^[+-]?([0-9]+(\.[0-9]*)?|\.[0-9]+)([eE][+-]?[0-9]+)?$", RegexOptions.CultureInvariant);
    private static readonly Regex LeadingZero = new(@"^[+-]?0[0-9]", RegexOptions.CultureInvariant);

    /// <summary>Unicode White_Space (rule 3.1): what String.Trim() removes; U+200B and U+FEFF are not white space.</summary>
    internal static bool IsWhiteSpace(char c) => c is >= '\u0009' and <= '\u000D' or ' ' or '\u0085' or '\u00A0' or '\u1680'
        or >= '\u2000' and <= '\u200A' or '\u2028' or '\u2029' or '\u202F' or '\u205F' or '\u3000';

    internal static string Trim(string text)
    {
        int start = 0, end = text.Length;
        while (start < end && IsWhiteSpace(text[start])) start++;
        while (end > start && IsWhiteSpace(text[end - 1])) end--;
        return text[start..end];
    }

    /// <summary>The kind of one stored cell.  A double with a date or time format is a date, time of day or duration (3.9).</summary>
    internal static Cell Classify(object value, Func<NumberFormatType> formatType, Func<string> format, bool date1904, string address)
    {
        switch (value)
        {
            case null:
            case ValueError:   // C4
                return new Cell(Kind.Missing);
            case bool boolean:
                return new Cell(Kind.Boolean, Boolean: boolean);
            case string text:
                return ClassifyText(text, address);
            case double number:
                if (!double.IsFinite(number)) return new Cell(Kind.Missing);
                // Rule 3.9: the section of the number format that applies to the value decides how it is read, by that section's own
                // type: the spreadsheet's type of a format of several sections is its first section's.
                string pattern = format();
                string section = ActiveSection(pattern, number);
                NumberFormatType type = ReferenceEquals(section, pattern) ? formatType() : SectionType(section);
                return type is NumberFormatType.Date or NumberFormatType.Time or NumberFormatType.DateTime
                    ? ClassifyDate(number, type, section, date1904)
                    : new Cell(Kind.Number, number);
            default:
                double converted = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                return double.IsFinite(converted) ? new Cell(Kind.Number, converted) : new Cell(Kind.Missing);
        }
    }

    private static Cell ClassifyText(string raw, string address)
    {
        if (raw.Contains('\0'))   // 3.10
            throw new InvalidDataException($"Cell {address} contains a null character, which R cannot hold. Remove it, then open the R import again.");
        string text = WithoutLoneSurrogates(raw);
        string trimmed = Trim(text);
        if (trimmed.Length == 0 || ExactMarkers.Contains(trimmed)) return new Cell(Kind.Missing);
        if (AnyCaseWords.Contains(trimmed)) return new Cell(Kind.Word, Text: text);
        if (IsNumericText(trimmed, out double number)) return new Cell(Kind.NumericText, number, Text: text);
        switch (trimmed.ToUpperInvariant())
        {
            case "INF" or "INFINITY": return new Cell(Kind.InfinityText, double.PositiveInfinity, Text: text);   // 3.3
            case "-INF" or "-INFINITY": return new Cell(Kind.InfinityText, double.NegativeInfinity, Text: text);
            case "TRUE": return new Cell(Kind.BooleanText, Boolean: true, Text: text);   // 3.5
            case "FALSE": return new Cell(Kind.BooleanText, Boolean: false, Text: text);
        }
        return new Cell(Kind.Text, Text: text);
    }

    /// <summary>Rule 3.4 and C9: trimmed text that reads as a finite number, with no leading zero.</summary>
    internal static bool IsNumericText(string trimmed, out double number)
    {
        number = 0;
        return NumericPattern.IsMatch(trimmed) && !LeadingZero.IsMatch(trimmed)
            && double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out number) && double.IsFinite(number);
    }

    /// <summary>A lone surrogate arrives as U+FFFD (rule 3.10): it has no UTF-8 form.</summary>
    internal static string WithoutLoneSurrogates(string text)
    {
        StringBuilder fixedText = null;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            bool paired = char.IsHighSurrogate(c) ? i + 1 < text.Length && char.IsLowSurrogate(text[i + 1])
                : !char.IsLowSurrogate(c) || i > 0 && char.IsHighSurrogate(text[i - 1]);
            if (paired && fixedText == null) continue;
            fixedText ??= new StringBuilder(text, 0, i, text.Length);
            fixedText.Append(paired ? c : '\uFFFD');
        }
        return fixedText?.ToString() ?? text;
    }

    private const long DayMilliseconds = 86_400_000;

    /// <summary>Rule 3.9: dates and date-times by value, times of day and durations by format, all rounded to the millisecond.</summary>
    private static Cell ClassifyDate(double serial, NumberFormatType type, string format, bool date1904)
    {
        // 9999-12-31 is the last day of either date system.  A value beyond it, a duration as long included, is a plain number.
        long lastDay = date1904 ? 2_957_003 : 2_958_465;
        if (!(Math.Abs(serial) <= lastDay + 1)) return new Cell(Kind.Number, serial);
        long ms = (long)Math.Round(serial * DayMilliseconds, MidpointRounding.AwayFromZero);
        if (IsElapsed(ActiveSection(format, serial)))
            return Math.Abs(serial) > lastDay ? new Cell(Kind.Number, serial) : new Cell(Kind.Duration, Text: DurationText(ms));
        if (type == NumberFormatType.Time && serial >= 0 && serial < 1) return new Cell(Kind.TimeOfDay, Text: ClockText(ms));
        long day = Math.DivRem(ms, DayMilliseconds, out long ofDay);
        if (ofDay < 0) { day--; ofDay += DayMilliseconds; }
        if (!date1904 && day == 60) return new Cell(Kind.Missing);   // 1900-02-29, which never was
        // Before the date system's first day (1900-01-01 is serial 1; 1904-01-01 is serial 0), or after 9999-12-31.  Serial 0 of the
        // 1900 system, which Excel shows as 1900-01-00, is a number.
        if (day < (date1904 ? 0 : 1) || day > lastDay) return new Cell(Kind.Number, serial);
        DateTime date = date1904 ? new DateTime(1904, 1, 1).AddDays(day)
            : day < 60 ? new DateTime(1899, 12, 31).AddDays(day) : new DateTime(1899, 12, 30).AddDays(day);
        string iso = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return ofDay == 0
            ? new Cell(Kind.Date, (date - new DateTime(1970, 1, 1)).Days, Text: iso)
            : new Cell(Kind.DateTime, Text: iso + " " + ClockText(ofDay));
    }

    // An elapsed-time format holds [h], [mm], [ss] and the like outside quotes and escapes: a bracket of one of the letters h, m
    // or s alone, repeated.  Other brackets ([Red], [Magenta], [$-409], [>=100]) are colours, locales and conditions.
    internal static bool IsElapsed(string format)
    {
        if (string.IsNullOrEmpty(format)) return false;
        bool quoted = false;
        for (int i = 0; i < format.Length; i++)
        {
            char c = format[i];
            if (c == '"') quoted = !quoted;
            else if (!quoted && c is '\\' or '_' or '*') i++;   // an escaped, spacing or fill character
            else if (!quoted && c == '[')
            {
                int close = format.IndexOf(']', i + 1);
                if (close < 0) return false;
                string code = format[(i + 1)..close].ToLowerInvariant();
                if (code.Length > 0 && code[0] is 'h' or 'm' or 's' && code.All(letter => letter == code[0])) return true;
                i = close;
            }
        }
        return false;
    }

    // A condition's limit is a number as the spreadsheet writes one: 10, -1.5, .5, 5., 1E2, 1.5e-3.
    private static readonly Regex Condition = new(@"^(<=|>=|<>|<|>|=)\s*([+-]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[eE][+-]?[0-9]+)?)$", RegexOptions.CultureInvariant);

    // A format's sections, and each section's condition ([<1], [>=100]) where it has one: both found outside quotes and escapes, so
    // "[>10]" in quotes is text.  The character after _ (a space as wide as it) or * (a fill) is that character, so "_;" is no
    // separator.  Parsed once for each format.
    private sealed record Sections(string[] Parts, (string Comparison, double Limit)?[] Conditions);
    private static readonly Dictionary<string, Sections> parsedFormats = new(StringComparer.Ordinal);

    private static Sections Parse(string format)
    {
        lock (parsedFormats)
        {
            if (parsedFormats.TryGetValue(format, out var known)) return known;
            var parts = new List<string>();
            var conditions = new List<(string, double)?>();
            (string, double)? condition = null;
            bool quoted = false;
            int start = 0;
            for (int i = 0; i < format.Length; i++)
            {
                char c = format[i];
                if (c == '"') quoted = !quoted;
                else if (quoted) continue;
                else if (c is '\\' or '_' or '*') i++;
                else if (c == '[')
                {
                    int close = format.IndexOf(']', i + 1);
                    if (close < 0) break;
                    var match = Condition.Match(format[(i + 1)..close].Trim());
                    if (match.Success && condition == null)
                        condition = (match.Groups[1].Value, double.Parse(match.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture));
                    i = close;
                }
                else if (c == ';')
                {
                    parts.Add(format[start..i]);
                    conditions.Add(condition);
                    condition = null;
                    start = i + 1;
                }
            }
            parts.Add(format[start..]);
            conditions.Add(condition);
            var sections = new Sections(parts.ToArray(), conditions.ToArray());
            if (parsedFormats.Count < 10_000) parsedFormats[format] = sections;
            return sections;
        }
    }

    /// <summary>
    /// Rule 3.9: the section of a number format that applies to the value, as the spreadsheet chooses it.  Sections are separated by ;
    /// outside quotes, escapes and brackets.  Without a condition in the first two sections: one section is for every value; of two,
    /// the first is for zero and positive values and the second for negative ones; of three or more, the first is for positive, the
    /// second for negative and the third for zero values.  With a condition ([&lt;1], [&gt;=100]) in the first or second section, each of
    /// those two sections is for the values its condition holds for, or, without one, the first for positive values and the second for
    /// negative ones: the first applies where it can, then the second, then the third; of only two sections, the second applies
    /// wherever the first does not.  A format of one section is returned as it is.
    /// </summary>
    internal static string ActiveSection(string format, double value)
    {
        if (string.IsNullOrEmpty(format) || format.IndexOf(';') < 0) return format;
        var sections = Parse(format);
        var parts = sections.Parts;
        if (parts.Length == 1) return format;
        var (first, second) = (sections.Conditions[0], sections.Conditions[1]);
        if (first == null && second == null)
            return parts.Length == 2 ? (value >= 0 ? parts[0] : parts[1]) : value > 0 ? parts[0] : value < 0 ? parts[1] : parts[2];
        if (first is var (c1, l1) ? Holds(c1, value, l1) : value > 0) return parts[0];
        if (parts.Length == 2) return parts[1];
        return (second is var (c2, l2) ? Holds(c2, value, l2) : value < 0) ? parts[1] : parts[2];
    }

    private static bool Holds(string comparison, double value, double limit) => comparison switch
    {
        "<" => value < limit, "<=" => value <= limit, ">" => value > limit, ">=" => value >= limit, "=" => value == limit, _ => value != limit,
    };

    // The spreadsheet's own type of one section, from a cell of a workbook of the import's own that holds nothing else.
    private static readonly Dictionary<string, NumberFormatType> sectionTypes = new(StringComparer.Ordinal);
    private static IRange typeCell;

    internal static NumberFormatType SectionType(string section)
    {
        lock (sectionTypes)
        {
            if (sectionTypes.TryGetValue(section, out var known)) return known;
            typeCell ??= Factory.GetWorkbook(CultureInfo.InvariantCulture).Worksheets[0].Cells[0, 0];
            NumberFormatType type;
            try
            {
                typeCell.NumberFormat = section;
                type = typeCell.NumberFormatType;
            }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException) { type = NumberFormatType.General; }
            if (sectionTypes.Count < 10_000) sectionTypes[section] = type;
            return type;
        }
    }

    // HH:MM:SS, hours not wrapped, with a point and one to three digits only when there is a fraction.
    private static string ClockText(long ms) =>
        (ms / 3_600_000).ToString("00", CultureInfo.InvariantCulture)
        + ":" + (ms / 60_000 % 60).ToString("00", CultureInfo.InvariantCulture)
        + ":" + (ms / 1000 % 60).ToString("00", CultureInfo.InvariantCulture) + Fraction(ms % 1000);

    private static string DurationText(long ms) => (ms < 0 ? "-" : "") + ClockText(Math.Abs(ms));

    private static string Fraction(long ms) => ms == 0 ? "" : "." + ms.ToString("000", CultureInfo.InvariantCulture).TrimEnd('0');

    // ---------------------------------------------------------------------------------------------------- 3.6 The type of a column

    internal enum ColumnType { Numeric, Logical, Date, DateTimeText, Character, AllMissing }

    /// <summary>
    /// Rules 3.2 to 3.6, from how many cells of each kind a column has.  Words and Inf count only beside at least one anchor
    /// value (a number, Boolean or date), and all such words in the column are set aside together.
    /// </summary>
    internal static ColumnType TypeOf(int[] counts)
    {
        int Count(params Kind[] wanted) => wanted.Sum(kind => counts[(int)kind]);
        int present = counts.Sum() - counts[(int)Kind.Missing], words = Count(Kind.Word), others = present - words;
        if (present == 0) return ColumnType.AllMissing;
        int numbers = Count(Kind.Number, Kind.NumericText), infinite = Count(Kind.InfinityText);
        if (numbers > 0 && others == numbers + infinite) return ColumnType.Numeric;
        int booleans = Count(Kind.Boolean, Kind.BooleanText);
        if (booleans > 0 && others == booleans) return ColumnType.Logical;
        // Dates and date-times anchor the words; times of day and durations do not (agreed with the Mac, 21 November 2026).
        int dates = Count(Kind.Date), dateTimes = Count(Kind.DateTime);
        if (dates > 0 && others == dates) return ColumnType.Date;
        if (dateTimes > 0 && others == dates + dateTimes) return ColumnType.DateTimeText;
        return ColumnType.Character;
    }

    /// <summary>A missing value, or a word that counts as missing in this column.</summary>
    private static bool IsNA(Cell cell, ColumnType type) => cell.Kind == Kind.Missing
        || cell.Kind == Kind.Word && type is ColumnType.Numeric or ColumnType.Logical or ColumnType.Date or ColumnType.DateTimeText;

    /// <summary>Rule 3.7: the text of a cell in a character column, and the text of a heading.</summary>
    internal static string Text(Cell cell, bool dateTimeColumn = false) => cell.Kind switch
    {
        Kind.Number => JavaScriptNumber(cell.Number),
        Kind.Boolean => cell.Boolean ? "TRUE" : "FALSE",
        Kind.Date => dateTimeColumn ? cell.Text + " 00:00:00" : cell.Text,
        _ => cell.Text ?? ""
    };

    // ------------------------------------------------------------------------------------------------------------- Numbers for R

    /// <summary>
    /// Rule 3.8: the shortest decimal when it has at most 15 digits, no exponent and at most four digits after the point;
    /// otherwise R's hexadecimal form, 0x1.(13 hex digits)p(exp), and a subnormal as 0x0.(13 hex digits)p-1022.  Every R reads
    /// these back exactly; the shortest decimal alone is misread about once in 2,000 values by Windows R.
    /// </summary>
    internal static string RNumber(double value)
    {
        if (double.IsPositiveInfinity(value)) return "Inf";
        if (double.IsNegativeInfinity(value)) return "-Inf";
        if (value == 0) return "0";
        var (negative, digits, point) = ShortestDigits(value);
        int count = digits.Length;
        string plain = null;
        if (point >= count && point <= 15) plain = digits + new string('0', point - count);
        else if (point > 0 && point < count && count <= 15 && count - point <= 4) plain = digits[..point] + "." + digits[point..];
        else if (point <= 0 && count - point <= 4 && count <= 15) plain = "0." + new string('0', -point) + digits;
        if (plain != null) return negative ? "-" + plain : plain;
        long bits = BitConverter.DoubleToInt64Bits(value);
        int exponent = (int)((bits >> 52) & 0x7FF);
        string mantissa = (bits & 0xF_FFFF_FFFF_FFFF).ToString("x13", CultureInfo.InvariantCulture);
        string hex = exponent == 0 ? "0x0." + mantissa + "p-1022"
            : "0x1." + mantissa + "p" + (exponent - 1023 >= 0 ? "+" : "") + (exponent - 1023).ToString(CultureInfo.InvariantCulture);
        return negative ? "-" + hex : hex;
    }

    /// <summary>The shortest digits naming a nonzero double: value = 0.digits × 10^point, digits with no leading or trailing zero.</summary>
    private static (bool Negative, string Digits, int Point) ShortestDigits(double value)
    {
        string r = value.ToString("R", CultureInfo.InvariantCulture);
        bool negative = r[0] == '-';
        if (negative) r = r[1..];
        int e = r.IndexOfAny(['E', 'e']), exponent = 0;
        if (e >= 0) { exponent = int.Parse(r[(e + 1)..], CultureInfo.InvariantCulture); r = r[..e]; }
        int dot = r.IndexOf('.');
        string digits = dot < 0 ? r : r.Remove(dot, 1);
        int point = (dot < 0 ? r.Length : dot) + exponent;
        int lead = 0;
        while (lead < digits.Length - 1 && digits[lead] == '0') { lead++; point--; }
        digits = digits[lead..].TrimEnd('0');
        return (negative, digits.Length == 0 ? "0" : digits, point);
    }

    /// <summary>JavaScript's String(x) (ECMAScript Number::toString), as the Mac grid writes a number: rule 3.7.</summary>
    internal static string JavaScriptNumber(double value)
    {
        if (double.IsNaN(value)) return "NaN";
        if (double.IsInfinity(value)) return value > 0 ? "Infinity" : "-Infinity";
        if (value == 0) return "0";
        var (negative, digits, n) = ShortestDigits(value);
        int k = digits.Length;
        string body;
        if (k <= n && n <= 21) body = digits + new string('0', n - k);
        else if (0 < n && n <= 21) body = digits[..n] + "." + digits[n..];
        else if (-6 < n && n <= 0) body = "0." + new string('0', -n) + digits;
        else
        {
            int exponent = n - 1;
            body = (k == 1 ? digits : digits[..1] + "." + digits[1..]) + "e" + (exponent < 0 ? "-" : "+")
                + Math.Abs(exponent).ToString(CultureInfo.InvariantCulture);
        }
        return negative ? "-" + body : body;
    }

    // --------------------------------------------------------------------------------------------------- 4. Headings and names

    /// <summary>Rule 4.1: the first row is suggested as headings.</summary>
    internal static bool SuggestHeadings(WorksheetRSource source)
    {
        if (source.Rows.Length < 2) return false;
        bool heading = false;
        var cells = source.Sheet.Cells;
        foreach (int column in source.Columns)
        {
            object value = cells[source.Rows[0], column].Value;
            if (value == null) continue;
            if (value is not string text) return false;
            string trimmed = Trim(text);
            if (trimmed.Length == 0) continue;
            if (!trimmed.EnumerateRunes().Any(Rune.IsLetter) || ExactMarkers.Contains(trimmed) || IsNumericText(trimmed, out _)
                || trimmed.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("FALSE", StringComparison.OrdinalIgnoreCase))
                return false;
            heading = true;
        }
        return heading;
    }

    /// <summary>A worksheet column's letters: A, B, ... Z, AA, ... (C5).</summary>
    internal static string ColumnName(int column)
    {
        var name = new StringBuilder();
        for (int n = column + 1; n > 0; n = (n - 1) / 26)
            name.Insert(0, (char)('A' + (n - 1) % 26));
        return name.ToString();
    }

    // ---------------------------------------------------------------------------------------------------------------- The script

    private const string MissingValue = "%NA";

    /// <summary>Writes the CSV into the folder and returns the R code that reads it into worksheet_data (sections 3 to 5).</summary>
    internal static string WriteScript(WorksheetRSource source, bool headers, WorksheetRDataFormat format, bool friendlyNames, string folder)
    {
        var sheet = source.Sheet;
        bool date1904 = sheet.Workbook.Date1904;
        int[] columns = source.Columns;
        int[] rows = headers ? source.Rows[1..] : source.Rows;
        bool anyDates = false;

        // 4.2 to 4.5.  The headings come first in sheet order, so a null character there is the one named.
        string[] names = new string[columns.Length];
        for (int i = 0; i < columns.Length; i++)
        {
            names[i] = ColumnName(columns[i]);   // C5
            if (!headers) continue;
            IRange heading = sheet.Cells[source.Rows[0], columns[i]];
            switch (heading.Value)
            {
                case null or ValueError:
                    break;   // 4.3
                case string text:
                    if (text.Contains('\0')) ClassifyText(text, Address(heading.Row, heading.Column));   // refused, naming the cell
                    if (Trim(text).Length > 0) names[i] = WithoutLoneSurrogates(text);   // 4.4: kept exactly
                    break;
                default:
                    var cell = Read(heading, date1904);   // 4.5: a number, Boolean or date as its rule 3.7 text
                    anyDates |= IsDateKind(cell.Kind);
                    if (Text(cell).Length > 0) names[i] = Text(cell);
                    break;
            }
        }

        // First pass: how many cells of each kind each column has, which decides its type.
        var counts = columns.Select(_ => new int[Enum.GetValues<Kind>().Length]).ToArray();
        ForEachCell(sheet, rows, columns, date1904, (column, cell) =>
        {
            counts[column][(int)cell.Kind]++;
            anyDates |= IsDateKind(cell.Kind);
        });
        ColumnType[] types = counts.Select(TypeOf).ToArray();

        var script = new StringBuilder()
            .Append("# Data copied from the worksheet \u201C").Append(CommentText(sheet.Name))
            .Append("\u201D, ").Append(source.AllAreas).Append(".\n")
            .Append("# Blank cells, missing-value markers and spreadsheet errors are NA.\n");
        if (anyDates) script.Append("# Dates without a time of day are R Date values; other dates and times are text.\n");

        string classes = RStrings(types.Select(type => type switch
        {
            ColumnType.Numeric or ColumnType.Date => "numeric",
            ColumnType.Logical or ColumnType.AllMissing => "logical",
            _ => "character"
        }));
        if (rows.Length == 0)
            script.Append("worksheet_data <- structure(list(").Append(string.Join(", ", columns.Select(_ => "logical(0)")))
                .Append("), names = ").Append(RStrings(names)).Append(", row.names = integer(0), class = \"data.frame\")\n");
        else
        {
            // Second pass: the CSV, one R type per column.
            string path = Path.GetFullPath(Path.Combine(folder, WorksheetRData.CsvFileName));
            Directory.CreateDirectory(folder);
            bool escaped = false;
            using (var writer = new StreamWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write), new UTF8Encoding(false, true)))
            {
                int written = 0;
                ForEachCell(sheet, rows, columns, date1904, (column, cell) =>
                {
                    if (column > 0) writer.Write(',');
                    ColumnType type = types[column];
                    if (IsNA(cell, type)) writer.Write(MissingValue);
                    else if (type is ColumnType.Numeric or ColumnType.Date) writer.Write(RNumber(cell.Number));
                    else if (type == ColumnType.Logical) writer.Write(cell.Boolean ? "TRUE" : "FALSE");
                    else
                    {
                        string text = Text(cell, type == ColumnType.DateTimeText);
                        if (text.Contains('%') || text.Contains('\r') || text.Contains('\u001A'))
                        {
                            // The CSV reader changes line endings and stops at Ctrl-Z; percent signs mark the escapes.
                            text = text.Replace("%", "%25").Replace("\r", "%0D").Replace("\u001A", "%1A");
                            escaped = true;
                        }
                        writer.Write('"');
                        writer.Write(text.Replace("\"", "\"\""));
                        writer.Write('"');
                    }
                    if (++written == columns.Length) { writer.WriteLine(); written = 0; }
                });
            }
            script.Append("worksheet_data <- ").Append(ReaderOperand(path)).Append(",\n")
                .Append("  header = FALSE, colClasses = ").Append(classes).Append(", na.strings = \"").Append(MissingValue).Append("\",\n")
                .Append("  encoding = \"UTF-8\", check.names = FALSE, blank.lines.skip = FALSE)\n");
            if (escaped)
                script.Append("worksheet_data[] <- lapply(worksheet_data, function(column) {\n")
                    .Append("  if (!is.character(column)) return(column)\n")
                    .Append("  gsub(\"%25\", \"%\", gsub(\"%1A\", \"\\032\", gsub(\"%0D\", \"\\r\", column, fixed = TRUE), fixed = TRUE), fixed = TRUE)\n")
                    .Append("})\n");
            script.Append("names(worksheet_data) <- ").Append(RStrings(names)).Append('\n');
            for (int i = 0; i < types.Length; i++)
                if (types[i] == ColumnType.Date)
                    script.Append("class(worksheet_data[[").Append(i + 1).Append("]]) <- \"Date\"\n");
        }
        if (headers && friendlyNames)
            script.Append("names(worksheet_data) <- make.unique(make.names(gsub(\"[[:space:]]+\", \"_\", trimws(names(worksheet_data)))), sep = \"_\")\n");
        if (format == WorksheetRDataFormat.VectorList)
            script.Append("# Missing values are removed separately from each vector.\n")
                .Append("worksheet_data <- lapply(worksheet_data, function(column) column[!is.na(column)])\n");
        return script.Append("utils::str(worksheet_data)\n").ToString();
    }

    private static bool IsDateKind(Kind kind) => kind is Kind.Date or Kind.DateTime or Kind.TimeOfDay or Kind.Duration;

    private static Cell Read(IRange cell, bool date1904) =>
        Classify(cell.Value, () => cell.NumberFormatType, () => cell.NumberFormat, date1904, Address(cell.Row, cell.Column));

    private static string Address(int row, int column) => ColumnName(column) + (row + 1).ToString(CultureInfo.InvariantCulture);

    /// <summary>Each cell in sheet order (rows, then columns), read in bounded blocks, with each column's number format read once a block.</summary>
    private static void ForEachCell(IWorksheet sheet, int[] rows, int[] columns, bool date1904, Action<int, Cell> visit)
    {
        var cells = sheet.Cells;
        var runs = WorksheetRSource.Runs(columns).ToArray();
        int batchRows = Math.Max(1, 16_384 / Math.Max(1, columns.Length));
        foreach (var (firstRow, lastRow) in WorksheetRSource.Runs(rows))
            for (int start = firstRow; start <= lastRow; start += batchRows)
            {
                int end = Math.Min(lastRow, start + batchRows - 1);
                var blocks = runs.Select(run => cells[start, run.First, end, run.Last]).ToArray();
                var values = blocks.Select(block => block.Value).ToArray();
                // A block whose cells share one number format answers for all of them; a mixed block is asked cell by cell.
                // (A mixed block reports its first cell's format, so NumberFormatDefined is what tells the two apart.)
                var uniform = blocks.Select(block => block.NumberFormatDefined).ToArray();
                var types = blocks.Select(block => block.NumberFormatType).ToArray();
                var formats = blocks.Select(block => block.NumberFormat).ToArray();
                for (int row = start; row <= end; row++)
                {
                    int index = 0;
                    for (int r = 0; r < runs.Length; r++)
                        for (int column = runs[r].First; column <= runs[r].Last; column++, index++)
                        {
                            object value = values[r] is object[,] block ? block[row - start, column - runs[r].First] : values[r];
                            int at = r, cellRow = row, cellColumn = column;
                            visit(index, Classify(value,
                                () => uniform[at] ? types[at] : cells[cellRow, cellColumn].NumberFormatType,
                                () => uniform[at] ? formats[at] : cells[cellRow, cellColumn].NumberFormat, date1904, Address(row, column)));
                        }
                }
            }
    }

    private static string RStrings(IEnumerable<string> values) => "c(" + string.Join(", ", values.Select(RString)) + ")";

    // R refuses a string literal holding escapes once its text reaches 10,000 characters, so a longer value is written as
    // paste0() of pieces of at most 2,000 characters each (rule 4.4: very long headings are kept exactly).
    private const int LiteralPiece = 2_000;

    internal static string RString(string value)
    {
        if (value.Length <= LiteralPiece) return QuoteR(value);
        var pieces = new List<string>();
        for (int start = 0; start < value.Length;)
        {
            int length = Math.Min(LiteralPiece, value.Length - start);
            if (start + length < value.Length && char.IsHighSurrogate(value[start + length - 1])) length--;   // keep pairs together
            pieces.Add(QuoteR(value.Substring(start, length)));
            start += length;
        }
        return "paste0(" + string.Join(", ", pieces) + ")";
    }

    // A comment line: each control character (C0, DEL and C1, CR and LF among them) as a space.
    internal static string CommentText(string text) =>
        string.Concat(text.Select(c => c < ' ' || c >= '\u007F' && c <= '\u009F' ? ' ' : c));

    internal static string QuotePath(string path) => QuoteR(path.Replace('\\', '/'));

    /// <summary>The R code by which an import script reads its CSV file: the one place the script names its data file.  A saved
    /// script's data file is moved by changing this code alone, so that comments and the user's own code stay as they are.</summary>
    internal static string ReaderOperand(string path) => "utils::read.csv(file(" + QuotePath(path) + ", encoding = \"native.enc\")";

    /// <summary>
    /// The string literals of R code, quoted in " or ', with their values as R reads them: not those in comments, backquoted names, raw
    /// strings, or special operators such as %in% and the user's own, whose names may hold quotes, # or a path (Codex's fifth review).
    /// A literal with an escape R does not read has no value.
    /// </summary>
    internal static List<(int Start, int Length, char Quote, string Value)> StringsInCode(string source)
    {
        var found = new List<(int, int, char, string)>();
        int n = source.Length;
        bool NamePart(int k) => k >= 0 && (char.IsLetterOrDigit(source[k]) || source[k] is '.' or '_');
        for (int i = 0; i < n;)
        {
            char c = source[i];
            if (c == '#')
            {
                int end = source.IndexOf('\n', i);
                i = end < 0 ? n : end + 1;
            }
            else if (c == '%')
            {
                // A special operator is one token up to the next % on the line, whatever it holds.
                int close = source.IndexOf('%', i + 1), end = source.IndexOf('\n', i + 1);
                i = close > 0 && (end < 0 || close < end) ? close + 1 : i + 1;
            }
            else if (c is 'r' or 'R' && i + 1 < n && source[i + 1] is '"' or '\'' && !NamePart(i - 1) && RawStringEnd(source, i) is int after)
                i = after;
            else if (c is '"' or '\'' or '`')
            {
                int j = i + 1;
                while (j < n && source[j] != c) j += source[j] == '\\' ? 2 : 1;
                int close = Math.Min(n, j);
                if (c != '`' && close < n) found.Add((i, close + 1 - i, c, DecodeR(source, i + 1, close)));
                i = close + 1;
            }
            else i++;
        }
        return found;
    }

    // An R string literal's value from its text between the quotes, its escapes read as R reads them.  Null for a literal R refuses: an
    // unknown escape, a code point beyond Unicode or a surrogate, or Unicode escapes mixed with byte escapes (\x or octal).  A byte
    // escape beyond ASCII has no value here either, since its character depends on the encoding (Codex's seventh review).
    private static string DecodeR(string source, int from, int to)
    {
        var value = new StringBuilder(to - from);
        bool unicode = false, bytes = false;
        for (int i = from; i < to; i++)
        {
            char c = source[i];
            if (c != '\\') { value.Append(c); continue; }
            if (++i >= to) return null;
            char e = source[i];
            switch (e)
            {
                case 'n': value.Append('\n'); break;
                case 'r': value.Append('\r'); break;
                case 't': value.Append('\t'); break;
                case 'a': value.Append('\a'); break;
                case 'b': value.Append('\b'); break;
                case 'f': value.Append('\f'); break;
                case 'v': value.Append('\v'); break;
                case '\\' or '"' or '\'' or '`': value.Append(e); break;
                case >= '0' and <= '7':
                {
                    int code = 0, digits = 0;
                    while (digits < 3 && i < to && source[i] is >= '0' and <= '7') { code = code * 8 + (source[i] - '0'); i++; digits++; }
                    i--;
                    if (code >= 0x80) return null;
                    bytes = true;
                    value.Append((char)code);
                    break;
                }
                case 'x' or 'u' or 'U':
                {
                    int most = e == 'x' ? 2 : e == 'u' ? 4 : 8;
                    bool braced = e != 'x' && i + 1 < to && source[i + 1] == '{';
                    if (braced) i++;
                    long code = 0;
                    int digits = 0;
                    while (digits < most && i + 1 < to && Uri.IsHexDigit(source[i + 1])) { code = code * 16 + Uri.FromHex(source[i + 1]); i++; digits++; }
                    if (digits == 0 || braced && !(i + 1 < to && source[++i] == '}')) return null;
                    if (e == 'x')
                    {
                        if (code >= 0x80) return null;
                        bytes = true;
                        value.Append((char)code);
                    }
                    else
                    {
                        if (code > 0x10FFFF || code is >= 0xD800 and <= 0xDFFF) return null;
                        unicode = true;
                        value.Append(char.ConvertFromUtf32((int)code));
                    }
                    break;
                }
                default: return null;
            }
        }
        return unicode && bytes ? null : value.ToString();
    }

    // The end of an R raw string that starts at i, r"(...)" with brackets ( [ or { and any number of dashes, or null when it is not one.
    private static int? RawStringEnd(string source, int i)
    {
        char quote = source[i + 1];
        int j = i + 2, dashes = 0;
        while (j < source.Length && source[j] == '-') { j++; dashes++; }
        if (j >= source.Length || source[j] is not ('(' or '[' or '{')) return null;
        char close = source[j] switch { '(' => ')', '[' => ']', _ => '}' };
        string closing = close + new string('-', dashes) + quote;
        int end = source.IndexOf(closing, j + 1, StringComparison.Ordinal);
        return end < 0 ? source.Length : end + closing.Length;
    }

    // Whether a string's value is a path: the same file, case and the direction of its separators aside.
    private static bool IsPath(string value, string path) =>
        value != null && string.Equals(value.Replace('\\', '/'), path.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether R code names a file by its path in a string literal (<see cref="StringsInCode"/>), however the user's code reads
    /// or writes it: what the window's own copy of its data is named by.</summary>
    internal static bool NamesInCode(string source, string path) => StringsInCode(source).Any(s => IsPath(s.Value, path));

    private const string ReaderStart = "utils::read.csv(file(";
    private static readonly string[] ReaderEnds = { ", encoding = \"native.enc\")", ", encoding = 'native.enc')" };

    /// <summary>
    /// Whether R code reads a file through the import's own reader, utils::read.csv(file(&lt;path&gt;, encoding = "native.enc"), the path in
    /// either quote and the reader's name a whole token (not notutils::read.csv, Codex's seventh review).  A script that names a file of
    /// that name in any other way, as an output written with write.csv for one, does not hold it by this (Codex's sixth review).
    /// </summary>
    internal static bool ReadsAsImport(string source, string path) => StringsInCode(source).Any(s => IsPath(s.Value, path)
        && s.Start >= ReaderStart.Length && string.CompareOrdinal(source, s.Start - ReaderStart.Length, ReaderStart, 0, ReaderStart.Length) == 0
        && !IsNamePart(source, s.Start - ReaderStart.Length - 1)
        && ReaderEnds.Any(end => s.Start + s.Length + end.Length <= source.Length && string.CompareOrdinal(source, s.Start + s.Length, end, 0, end.Length) == 0));

    private static bool IsNamePart(string source, int k) => k >= 0 && (char.IsLetterOrDigit(source[k]) || source[k] is '.' or '_');

    /// <summary>The first line of a script StatsDirect saved with its data file, which names that file beside it: the pair's own record,
    /// so that the script still holds its data file when the user has changed the import's reader (Codex's seventh review).</summary>
    internal const string DataFileMarker = "# StatsDirect worksheet data: ";

    // The data file a script's marker names, or null when it has none.
    private static string MarkedDataFile(string source)
    {
        foreach (string line in source.Split('\n'))
            if (line.StartsWith(DataFileMarker, StringComparison.Ordinal)) return line[DataFileMarker.Length..].TrimEnd('\r');
        return null;
    }

    /// <summary>
    /// Whether a script holds the data file beside it, which it then reads from a copy of its own: it reads the file through the import's
    /// own reader (<see cref="ReadsAsImport"/>, the record of scripts saved before the marker), or its marker, written when StatsDirect
    /// saved the pair, names the file and a string literal in its code still names it (<see cref="NamesInCode"/>).  An ordinary
    /// script that names a file of that name holds nothing.
    /// </summary>
    internal static bool HoldsDataFile(string source, string dataFile) => ReadsAsImport(source, dataFile)
        || string.Equals(MarkedDataFile(source), Path.GetFileName(dataFile), StringComparison.OrdinalIgnoreCase) && NamesInCode(source, dataFile);

    /// <summary>The script without its marker lines: what the editor shows, since StatsDirect writes the marker with each save.</summary>
    internal static string Unmarked(string source)
    {
        if (!source.Contains(DataFileMarker, StringComparison.Ordinal)) return source;
        var lines = source.Split('\n');
        return string.Join("\n", lines.Where(line => !line.StartsWith(DataFileMarker, StringComparison.Ordinal)));
    }

    /// <summary>The script with one marker, naming its data file, as its first line, or after a #! line: written with the pair.</summary>
    internal static string Marked(string source, string dataFileName)
    {
        string unmarked = Unmarked(source), newline = unmarked.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        string marker = DataFileMarker + dataFileName + newline;
        if (!unmarked.StartsWith("#!", StringComparison.Ordinal)) return marker + unmarked;
        int end = unmarked.IndexOf('\n');
        return end < 0 ? unmarked + newline + marker : unmarked[..(end + 1)] + marker + unmarked[(end + 1)..];
    }

    /// <summary>R code with each string literal that names one path (<see cref="StringsInCode"/>) naming another, in the literal's own
    /// quote.</summary>
    internal static string RenameInCode(string source, string path, string replacement)
    {
        var at = StringsInCode(source).Where(s => IsPath(s.Value, path)).ToList();
        if (at.Count == 0) return source;
        string doubled = QuotePath(replacement), single = "'" + doubled[1..^1].Replace("'", "\\'") + "'";
        var result = new StringBuilder(source.Length + at.Count * doubled.Length);
        int last = 0;
        foreach (var (start, length, quote, _) in at)
        {
            result.Append(source, last, start - last).Append(quote == '\'' ? single : doubled);
            last = start + length;
        }
        return result.Append(source, last, source.Length - last).ToString();
    }

    /// <summary>An R string literal, ASCII only: every other character as a \U escape, bidi controls included (rule 3.10).</summary>
    internal static string QuoteR(string value)
    {
        var quoted = new StringBuilder();
        foreach (Rune character in RScriptWriter.Quote(value).EnumerateRunes())
        {
            if (character.IsAscii) quoted.Append((char)character.Value);
            else quoted.Append("\\U").Append(character.Value.ToString("x8", CultureInfo.InvariantCulture));
        }
        return quoted.ToString();
    }
}

/// <summary>The cells an import sends: rows and columns of one worksheet, each ascending and in sheet order (RULES.md section 2).</summary>
internal sealed class WorksheetRSource
{
    internal WorksheetRSource(IWorksheet sheet, int[] rows, int[] columns)
    {
        Sheet = sheet;
        Rows = rows;
        Columns = columns;
    }

    internal IWorksheet Sheet { get; }
    internal int[] Rows { get; }
    internal int[] Columns { get; }

    /// <summary>The cells in A1 notation, every area (rule 1.4), as the script's comment gives them.</summary>
    internal string AllAreas => string.Join(",", Areas());

    /// <summary>The cells as the prompt shows them: at most three areas, then ",…".</summary>
    internal string LabelAreas
    {
        get
        {
            var areas = Areas().ToList();
            return areas.Count <= 3 ? string.Join(",", areas) : string.Join(",", areas.Take(3)) + ",\u2026";
        }
    }

    private IEnumerable<string> Areas()
    {
        foreach (var (top, bottom) in Runs(Rows))
            foreach (var (left, right) in Runs(Columns))
                yield return top == bottom && left == right ? Cell(top, left) : Cell(top, left) + ":" + Cell(bottom, right);
    }

    private static string Cell(int row, int column) => WorksheetRImport.ColumnName(column) + (row + 1).ToString(CultureInfo.InvariantCulture);

    /// <summary>The runs of consecutive numbers in an ascending list.</summary>
    internal static IEnumerable<(int First, int Last)> Runs(int[] sorted)
    {
        for (int i = 0; i < sorted.Length;)
        {
            int j = i;
            while (j + 1 < sorted.Length && sorted[j + 1] == sorted[j] + 1) j++;
            yield return (sorted[i], sorted[j]);
            i = j + 1;
        }
    }
}
