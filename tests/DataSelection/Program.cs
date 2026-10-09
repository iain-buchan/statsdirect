using System.Reflection;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.UI;
using StatsDirect.Utilities;

// Checks of the selection of data from a worksheet by group identifiers (StatsDirectUI/UI/CellArrayProcessor.cs), driven by a
// stand-in for the grid: the identifier columns are combined into one classifier of the rows (CategoryCombineAllColumns), and
// a row without an identifier keeps its place as a missing identifier, so that the identifiers are as many as the rows selected
// and the data selected beside them stay beside their rows.  Run with: dotnet run -c Release
static class Program
{
    private static int failures = 0;

    private static void Say(bool ok, string what)
    {
        Console.WriteLine($"{(ok ? "ok  " : "FAIL")}  {what}");
        if (!ok)
            failures++;
    }

    // A grid of one column of texts: the processor asks it for the column's title and the texts of its cells
    private sealed class FakeGrid : IGetCells
    {
        private readonly string title;
        private readonly string[] texts;
        public FakeGrid(string title, string[] texts) { this.title = title; this.texts = texts; }
        public string GetColumnTitle(int column) => title;
        public string GetCellText(int gridRow, int gridColumn) => texts[gridRow];
        public (string[], int) GetCellTexts(int column, int firstRow, int lastRow) => (texts[firstRow..(lastRow + 1)], 0);
        public bool IsFormattedLikeATitle(int column, int row) => false;
        public (object[,], int) GetCellObjects(int column, int firstRow, int lastRow)
        {
            object[,] objects = new object[lastRow - firstRow + 1, 1];
            for (int r = firstRow; r <= lastRow; r++)
                objects[r - firstRow, 0] = texts[r].Length == 0 ? null : texts[r];
            return (objects, 0);
        }
        public (string[], int) GetCellFormulae(int column, int firstRow, int lastRow) => (texts[firstRow..(lastRow + 1)], 0);
        public (DateTime[], int) GetCellDateValues(int column, int firstRow, int lastRow) => (new DateTime[lastRow - firstRow + 1], 0);
        public double GetCellValue(int gridRow, int gridColumn) => double.NaN;
        public (double[], int) GetCellValues(int column, int firstRow, int lastRow) => (Enumerable.Repeat(double.NaN, lastRow - firstRow + 1).ToArray(), 0);
    }

    // The classifier that the processor makes of the identifier columns given (each a column of cell texts, "" for a blank cell)
    private static ClassifierVariable Combined(params string[][] columns)
    {
        int rows = columns[0].Length;
        CellSelection selection = new() { LongestRowCount = rows };
        string[,] hold = new string[rows, columns.Length];
        for (int c = 0; c < columns.Length; c++)
        {
            selection.ColumnSelections.Add(new CellColumnSelection(new FakeGrid("ids" + (c + 1), columns[c])) { ColumnIndex = c, RowIndex = 0, RowCount = rows, WorkbookPath = "", WorksheetName = "Sheet1" });
            for (int r = 0; r < rows; r++)
                hold[r, c] = columns[c][r];
        }
        Type processor = typeof(CellSelection).Assembly.GetType("StatsDirect.UI.CellArrayProcessor");
        MethodInfo method = processor.GetMethod("ProcessCellArrayCategoryCombineAllColumns", BindingFlags.NonPublic | BindingFlags.Static);
        DataFrame frame = (DataFrame)method.Invoke(null, new object[] { selection, DataAcquisitionMode.CategoryCombineAllColumns, 0, hold, 0 });
        return (ClassifierVariable)frame.Variables[0];
    }

    private static string Shown(ClassifierVariable v) =>
        "length " + v.Length + ", ids " + string.Join(",", v.Data.Take(v.Length).Select(d => d == Constant.MISSING ? "missing" : ((int)d).ToString())) +
        ", groups " + string.Join(" ", v.Groups.Select(g => g.Label + ":" + g.NBin));

    private static void Check(string what, ClassifierVariable v, int length, double[] ids, string groups)
    {
        bool ok = v.Length == length && v.Data.Take(v.Length).SequenceEqual(ids) && string.Join(" ", v.Groups.Select(g => g.Label + ":" + g.NBin)) == groups;
        Say(ok, what + ": " + Shown(v) + (ok ? "" : $" (expected length {length}, groups {groups})"));
    }

    // The rule that the selections by identifier share (GridSelectionProcessor.RowWithoutIdentifier): the first row, counted from
    // 1 among the rows selected, that has a value but no identifier, or 0 when there is none
    private static void CheckRow(string what, double[] ids, double[] values, int expected)
    {
        MethodInfo method = typeof(CellSelection).Assembly.GetType("StatsDirect.UI.GridSelectionProcessor")?.GetMethod("RowWithoutIdentifier", BindingFlags.NonPublic | BindingFlags.Static);
        if (method == null)
        {
            Say(false, what + ": the program has no RowWithoutIdentifier");
            return;
        }
        int row = (int)method.Invoke(null, new object[] { ids, values, ids.Length });
        Say(row == expected, what + ": row " + row + (row == expected ? "" : $" (expected {expected})"));
    }

    private static int Main()
    {
        double m = Constant.MISSING;
        Console.WriteLine("Group identifiers combined into one classifier of the rows");
        Check("two groups, no blanks", Combined(new[] { "a", "a", "b", "b", "b" }), 5, new[] { 0, 0, 1, 1, 1.0 }, "a:2 b:3");
        Check("a blank identifier keeps its row", Combined(new[] { "a", "a", "", "b", "b", "b" }), 6, new[] { 0, 0, m, 1, 1, 1 }, "a:2 b:3");
        Check("an asterisk is a blank identifier", Combined(new[] { "a", "*", "b" }), 3, new[] { 0, m, 1 }, "a:1 b:1");
        Check("the label of a missing value is a missing identifier, not a group", Combined(new[] { "a", "* (missing)", "b", "a" }), 4, new[] { 0, m, 1, 0 }, "a:2 b:1");
        Check("two identifier columns: a blank in either makes the row's identifier missing", Combined(new[] { "a", "a", "", "b" }, new[] { "x", "y", "x", "" }), 4, new[] { 0, 1, m, m }, "a, x:1 a, y:1");
        Check("blank rows at the end keep the length of the range", Combined(new[] { "a", "b", "", "" }), 4, new[] { 0, 1, m, m }, "a:1 b:1");
        Console.WriteLine("A value beside a blank identifier, refused by its row");
        CheckRow("identifiers a,a,blank,b,b,b against 10,11,12,20,21,22: the third row", new[] { 0, 0, m, 1, 1, 1 }, new[] { 10, 11, 12, 20, 21, 22.0 }, 3);
        CheckRow("a blank identifier beside a missing value is no fault", new[] { 0, 0, m, 1, 1, 1 }, new[] { 10, 11, m, 20, 21, 22 }, 0);
        CheckRow("no blank identifier", new[] { 0, 0, 1, 1.0 }, new[] { 10, 11, 20, 21.0 }, 0);
        CheckRow("the first row with a value and no identifier is named, not the first blank identifier", new[] { m, 0, m, 1 }, new[] { m, 11, 20, 21 }, 3);
        CheckRow("a value with no identifier in the last row", new[] { 0, 1, m }, new[] { 10, 20, 30.0 }, 3);
        Console.WriteLine(failures == 0 ? "ALL CHECKS PASS" : $"{failures} CHECKS FAILED");
        return failures == 0 ? 0 : 1;
    }
}
