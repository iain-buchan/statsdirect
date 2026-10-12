using StatsDirect.UI.Learning;
using StatsDirect.Utilities;
using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace StatsDirect.UI;

internal partial class frmSpreadsheetGear
{
    internal int LearningVersion { get; private set; }
    internal JsonObject LearningInfo() => workbookView.WithLock(() =>
    {
        var book = workbookView.ActiveWorkbook;
        return new JsonObject { ["activeSheet"] = workbookView.ActiveWorksheet.Index,
            ["sheets"] = new JsonArray(book.Worksheets.Cast<SpreadsheetGear.IWorksheet>().Select(s => (JsonNode)new JsonObject { ["index"] = s.Index, ["name"] = s.Name }).ToArray()),
            ["selection"] = workbookView.RangeSelection.Address,
            ["usedRange"] = workbookView.ActiveWorksheet.UsedRange.Address,
            ["version"] = LearningVersion };
    });
    internal JsonObject LearningRead(JsonObject options) => workbookView.WithLock(() =>
    {
        var book = workbookView.ActiveWorkbook;
        int sheetIndex = options.I("sheetIndex", workbookView.ActiveWorksheet.Index);
        if (sheetIndex < 0 || sheetIndex >= book.Worksheets.Count) throw new ArgumentException("Choose an available worksheet.");
        var sheet = book.Worksheets[sheetIndex]; var used = sheet.UsedRange;
        var selected = workbookView.ActiveWorksheet == sheet ? workbookView.RangeSelection : used;
        if (selected.Areas.Count != 1 && options["columns"] == null) throw new ArgumentException("Select a rectangular range, or specify the columns explicitly.");
        int left = selected.Column, width = selected.ColumnCount;
        if (width == 16384) { left = used.Column; width = used.ColumnCount; }
        int[] columns = options["columns"] is JsonArray array ? array.Select(v => v.GetValue<int>()).ToArray() : Enumerable.Range(left, Math.Min(width, 33)).ToArray();
        if (columns.Length is < 1 or > 32 || columns.Distinct().Count() != columns.Length || columns.Any(c => c < 0 || c >= 16384)) throw new ArgumentException("Request 1–32 distinct worksheet columns.");
        int first = options.I("firstRow", selected.Row + 1);
        int last = options.I("lastRow", selected.RowCount == 1048576 ? used.Row + used.RowCount : selected.Row + selected.RowCount);
        int count = last - first + 1;
        if (first < 1 || last > 1048576 || count <= 0 || (long)count * columns.Length > 4000) throw new ArgumentException("Choose a smaller range: at most 4,000 cells per read. No cells have been sampled or cropped.");
        bool errors = false; var data = new JsonArray();
        foreach (int column in columns)
        {
            var values = new JsonArray(); var texts = new JsonArray();
            for (int row = first - 1; row < last; row++)
            {
                var cell = sheet.Cells[row, column];
                if (cell.ValueType == SpreadsheetGear.ValueType.Error) errors = true;
                object value = cell.Value;
                if (value is double number && !double.IsFinite(number)) { value = null; errors = true; }
                values.Add(value == null ? null : JsonSerializer.SerializeToNode(value));
                texts.Add(cell.Text);
            }
            // Titles are descriptive metadata only: row 1 is not silently discarded from the requested range.
            string title = sheet.Cells[0, column].Value is string heading ? heading.Limit(200) : "Column " + (column + 1);
            data.Add(new JsonObject { ["index"] = column, ["title"] = title, ["values"] = values, ["text"] = texts });
        }
        return new JsonObject { ["columns"] = data, ["rowCount"] = count, ["firstRow"] = first, ["lastRow"] = last, ["sheetIndex"] = sheetIndex, ["sheet"] = sheet.Name, ["workbook"] = Text, ["range"] = sheet.Cells[first - 1, columns.Min(), last - 1, columns.Max()].Address,
            ["complete"] = true, ["hasErrors"] = errors, ["hasStaleFormulas"] = workbookView.ActiveWorkbookSet.Calculation != SpreadsheetGear.Calculation.Automatic,
            ["headerRow"] = false, ["note"] = "Rows are literal worksheet rows; select firstRow 2 to exclude row-1 headings where appropriate." };
    });
    internal void LoadLearningExample(JsonArray columns) => workbookView.WithLock(() =>
    {
        var sheet = workbookView.ActiveWorksheet; sheet.Name = "Fictional data"; int rows = 0;
        for (int c = 0; c < columns.Count; c++)
        {
            sheet.Cells[0, c].Value = columns[c].S("title", "Variable"); var values = columns[c].A("values"); rows = Math.Max(rows, values.Count);
            for (int r = 0; r < values.Count; r++) sheet.Cells[r + 1, c].Value = JsonSerializer.Deserialize<double>(values[r].ToJsonString());
        }
        if (columns.Count > 0) workbookView.RangeSelection = sheet.Cells[0, 0, rows, columns.Count - 1];
        Dirty = false; LearningVersion++;
    });
}
