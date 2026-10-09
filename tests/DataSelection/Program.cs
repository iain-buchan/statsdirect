using System.Reflection;
using StatsDirect.Builtins;
using StatsDirect.Data;
using StatsDirect.Templates;
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

    // The rows of the long layout placed in their groups for the analysis of covariance (GridSelectionProcessor.PlaceByGroup): x and the
    // replicates of y at each level of each group, the replicates present at each level, the levels of each group and the sums of x
    private static readonly MethodInfo placeByGroup = typeof(CellSelection).Assembly.GetType("StatsDirect.UI.GridSelectionProcessor")?.GetMethod("PlaceByGroup", BindingFlags.NonPublic | BindingFlags.Static);

    private static (double[,] x, double[,,] y, int[,] ny, int[] levels, double[] sums) Placed(double[] ids, double[] groups, int maxRows, double[] x, double[][] y)
    {
        object[] args = { ids, groups, groups.Length - 1, ids.Length, maxRows, x, y, null, null, null, null, null };
        placeByGroup.Invoke(null, args);
        return ((double[,])args[7], (double[,,])args[8], (int[,])args[9], (int[])args[10], (double[])args[11]);
    }

    // The analysis of covariance of groups placed so, as the operation runs it
    private static ParameterBag Covariance(double[,] x, double[,,] y, int[,] ny, int[] levels, double[] sums, int maxRows, int maxReplicates)
    {
        int k = levels.Length - 1;
        ColumnData[] cx = new ColumnData[k + 1];
        for (int g = 1; g <= k; g++)
            cx[g] = new ColumnData { Title = "group " + g, Rows = levels[g], Sum = sums[g] };
        GroupedCovarianceData gcd = new()
        {
            a = new double[k + 1], b = new double[k + 1], bnam = new string[k + 1], cx = cx, GAMMA = 0.95, k = k, maxr = maxRows, maxreps = maxReplicates,
            minMax = new MinMax(), nxi = levels, ny = ny, rssx = new double[k + 1], xlab = "x", xmean = new double[k + 1], xt = x, y = y, ymean = new double[k + 1]
        };
        ParameterBag bag = new();
        bag.AddInput("gcd", gcd);
        bag.AddInput("mx0-prompted", 2.6);
        return RegressRpt.RptGroupedCovariance(bag).ParameterBag;
    }

    private static void CheckFigure(string what, double got, double expected)
    {
        bool ok = Math.Abs(got - expected) <= 1e-9 * Math.Max(1.0, Math.Abs(expected));
        Say(ok, what + ": " + got + (ok ? "" : $" (expected {expected})"));
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
        Console.WriteLine("Analysis of covariance by identifier: the rows placed in their groups with missing values left out");
        if (placeByGroup == null)
            Say(false, "the program has no PlaceByGroup");
        else
        {
            // x 1, 2, 3 against y 2, 3, 5 in group a and x 2, 3, 4 against y 3, 6, 8 in group b, in rows; the y of the second row blank
            double[] ids = { 0, 0, 0, 1, 1, 1 };
            double[] groups = { 0, 0, 1 };
            var p = Placed(ids, groups, 3, new[] { 1, 2, 3, 2, 3, 4.0 }, new[] { new[] { 2, m, 5, 3, 6, 8 } });
            Say(p.levels[1] == 3 && p.levels[2] == 3 && p.ny[1, 1] == 1 && p.ny[1, 2] == 0 && p.ny[1, 3] == 1 && p.ny[2, 2] == 1 && p.x[1, 2] == 2 && p.y[1, 1, 1] == 2 && p.y[1, 3, 1] == 5 && p.y[2, 3, 1] == 8 && p.sums[1] == 6 && p.sums[2] == 9,
                "a blank replicate of y is left out of its level: group a keeps three levels, the second with no replicate");
            var q = Placed(ids, groups, 3, new[] { 1, 2, m, 2, 3, 4 }, new[] { new[] { 2, 3, 5, 3, 6, 8.0 } });
            Say(q.levels[1] == 2 && q.levels[2] == 3 && q.x[1, 1] == 1 && q.x[1, 2] == 2 && q.ny[1, 2] == 1 && q.y[1, 2, 1] == 3 && q.sums[1] == 3,
                "a row whose x is blank is left out of its group: group a keeps two levels");
            var two = Placed(ids, groups, 3, new[] { 1, 2, 3, 2, 3, 4.0 }, new[] { new[] { 2, m, 5, 3, 6, 8 }, new[] { m, 4, m, 3, 7, 9 } });
            Say(two.ny[1, 1] == 1 && two.ny[1, 2] == 1 && two.ny[1, 3] == 1 && two.ny[2, 1] == 2 && two.y[1, 2, 1] == 4 && two.y[2, 1, 2] == 3,
                "with two replicate columns the replicates present are counted at each level");
            // the figures from the definition, by hand: group a has (1, 2) and (3, 5), group b (2, 3), (3, 6) and (4, 8); the sums of squares of
            // x, of y and of their products about the group means are 2, 9/2, 3 and 2, 38/3, 5, so the slopes are 1.5 and 2.5, the common
            // slope 8/4 = 2 with its sum of squares 64/4 = 16, the separate slopes account for 9/2 + 25/2 = 17 and so for 1 beyond it, and
            // (9/2 - 9/2) + (38/3 - 25/2) = 1/6 is left on 5 - 4 = 1 degree of freedom
            ParameterBag o = Covariance(p.x, p.y, p.ny, p.levels, p.sums, 3, 1);
            CheckFigure("common slope: sum of squares", o["com_ssq"].AsDouble, 16);
            CheckFigure("between slopes: sum of squares", o["bet_ssq"].AsDouble, 1);
            CheckFigure("residual sum of squares", o["res_ssq"].AsDouble, 1.0 / 6);
            CheckFigure("residual degrees of freedom", o["res_df"].AsDouble, 1);
            ParameterBag slope = ((System.Collections.IEnumerable)o["*slope"].AsObject).Cast<ParameterBag>().First();
            CheckFigure("slope of group a", slope["res1"].AsDouble, 1.5);
            CheckFigure("slope of group b", slope["res2"].AsDouble, 2.5);
            // the same figures, number for number, as the layout in separate columns gives, built by hand as that selection builds it: the
            // x series of each group, and at each level the replicates of y that are present (none at the second level of group a)
            double[,] cx = { { 0, 0, 0, 0 }, { 0, 1, 2, 3 }, { 0, 2, 3, 4 } };
            double[,,] cy = new double[3, 4, 2];
            cy[1, 1, 1] = 2; cy[1, 3, 1] = 5; cy[2, 1, 1] = 3; cy[2, 2, 1] = 6; cy[2, 3, 1] = 8;
            int[,] cny = { { 0, 0, 0, 0 }, { 0, 1, 0, 1 }, { 0, 1, 1, 1 } };
            ParameterBag c = Covariance(cx, cy, cny, new[] { 0, 3, 3 }, new[] { 0, 6, 9.0 }, 3, 1);
            string[] keys = { "com_ssq", "com_vr", "com_p", "bet_ssq", "bet_vr", "bet_p", "res_ssq", "res_msq", "grp_ssq", "uc_bet_yy", "uc_bet_xy", "uc_bet_xx", "uc_with_yy", "uc_with_xy", "uc_with_xx", "uc_tot_yy", "uc_tot_xy", "uc_tot_xx" };
            string differ = string.Join(" ", keys.Where(key => o[key].AsDouble != c[key].AsDouble));
            Say(differ.Length == 0, "the long layout and the layout in separate columns give the same figures" + (differ.Length == 0 ? " (" + keys.Length + " compared)" : ": differ at " + differ));
        }
        Console.WriteLine(failures == 0 ? "ALL CHECKS PASS" : $"{failures} CHECKS FAILED");
        return failures == 0 ? 0 : 1;
    }
}
