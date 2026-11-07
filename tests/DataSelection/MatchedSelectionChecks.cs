using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Serialization;
using StatsDirect.Builtins;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.Templates;
using StatsDirect.UI;
using StatsDirect.Utilities;

// Exercise the ordinary Wide acquisition path, not a copy of its padding logic.
// Row comparisons precede the numerical checks: a shifted pair must never pass
// just because a statistic happens to be invariant under that particular shift.
internal static class MatchedSelectionChecks
{
    const DataAcquisitionMode aligned = (DataAcquisitionMode)108;
    static readonly Type processor = typeof(CellSelection).Assembly.GetType("StatsDirect.UI.CellArrayProcessor");
    static readonly Type selectionProcessor = typeof(CellSelection).Assembly.GetType("StatsDirect.UI.GridSelectionProcessor");
    static readonly Type sheet = typeof(CellSelection).Assembly.GetType("StatsDirect.UI.frmSpreadsheetGear");
    static readonly MethodInfo numeric = sheet.GetMethod("ToCellValue", BindingFlags.Public | BindingFlags.Static);
    static readonly MethodInfo read = processor.GetMethod("ProcessCellArray", BindingFlags.Public | BindingFlags.Static);
    static readonly MethodInfo getEqual = selectionProcessor.GetMethod("GetCellEqual", BindingFlags.NonPublic | BindingFlags.Instance);
    static int checks, failures;

    static void Check(bool ok, string message)
    {
        checks++; if (!ok) failures++;
        Console.WriteLine($"{(ok ? "ok  " : "FAIL")}  matched columns: {message}");
    }
    internal static double RawNumber(object value) => (double)numeric.Invoke(null, new[] { value });
    internal static double Number(object value)
    {
        double x = RawNumber(value);
        return x == Constant.MISSING * 10D || double.IsNaN(x) ? Constant.MISSING : x;
    }
    internal static DataFrame Read(CellSelection selection, DataAcquisitionMode mode, int hint, int originGroup)
        => (DataFrame)read.Invoke(null, new object[] { selection, mode, hint, false, false, originGroup, "matched fixture" });

    static string SourceRoot()
    {
        foreach (string start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
            for (var directory = new DirectoryInfo(start); directory != null; directory = directory.Parent)
                if (Directory.Exists(Path.Combine(directory.FullName, "StatsDirectUI", "Assets", "Operations"))) return directory.FullName;
        throw new DirectoryNotFoundException("Cannot locate source operation definitions for matched-column checks.");
    }
    static Dictionary<string, Dictionary<string, double>> Gold()
    {
        string file = Path.Combine(AppContext.BaseDirectory, "matched-selection.tsv");
        if (!File.Exists(file)) file = Path.Combine(SourceRoot(), "tests", "DataSelection", "benchmarks", "matched-selection.tsv");
        var result = new Dictionary<string, Dictionary<string, double>>();
        foreach (string line in File.ReadLines(file).Skip(1))
        {
            string[] fields = line.Split('\t');
            if (!result.ContainsKey(fields[0])) result[fields[0]] = new();
            result[fields[0]][fields[1]] = double.Parse(fields[2], CultureInfo.InvariantCulture);
        }
        return result;
    }
    static void Near(double actual, double expected, string message)
        => Check(double.IsFinite(actual) && Math.Abs(actual - expected) <= 2e-10 * Math.Max(1e-100, Math.Abs(expected)),
            $"{message} ({actual:R}; R {expected:R})");

    static void Reports(string name, DataFrame frame, Dictionary<string, Dictionary<string, double>> gold)
    {
        var input = new ParameterBag(); input.AddInput("data", frame); input.AddInput("gamma", .95);
        var paired = Parametric.RptTPaired(input).ParameterBag;
        foreach (string key in new[] { "n", "mean", "sd", "sem", "df", "t", "tail_2", "from", "to" })
            Near(paired[key].AsDouble, gold[name][key], name + " paired t " + key);

        var signed = Nonparametric.RptWilcoxon(DispatchProxy.Create<ITemplateHost, MatchedReportHost>(), input).ParameterBag;
        Near(signed["non_0"].AsDouble, gold[name]["signed_nonzero"], name + " signed-rank observed pairs");
        Near(signed["p_2"].AsDouble, gold[name]["signed_p"], name + " exact signed-rank two-sided P");
        var spearman = Nonparametric.RptSpearman(input).ParameterBag;
        Near(spearman["obs"].AsDouble, gold[name]["n"], name + " Spearman observed pairs");
        Near(spearman["rho"].AsDouble, gold[name]["spearman"], name + " Spearman rho");

        // This is the report's own pair-filtering boundary, followed by its actual
        // least-squares calculation. No chart or report window is constructed.
        var regression = new ParameterBag();
        regression.AddInput("x", new DataFrame(frame.Variables[0]));
        regression.AddInput("y", new DataFrame(frame.Variables[1]));
        var create = typeof(Regress).GetMethod("GetSimpleLinearRegressionContextWithData", BindingFlags.NonPublic | BindingFlags.Static);
        object context = create.Invoke(null, new object[] { regression });
        context.GetType().GetMethod("CalculateLeastSquaresMethod").Invoke(context, null);
        Near((double)context.GetType().GetProperty("R").GetValue(context), gold[name]["pearson"], name + " Pearson r");
    }

    static DataFrame Acquire(MatchedCells cells, (int column, int first, int count)[] ranges, DataAcquisitionMode mode = aligned)
    {
        IGrid grid = DispatchProxy.Create<IGrid, MatchedGridProxy>();
        var proxy = (MatchedGridProxy)(object)grid;
        proxy.Cells = cells; proxy.Selection = cells.Selection(ranges);
        object instance = Activator.CreateInstance(selectionProcessor, new object[] { grid });
        var parameters = getEqual.GetParameters();
        object[] arguments = { 0, mode, Enum.Parse(parameters[2].ParameterType, "GroupIdentifier"), 1, ranges.Length,
            "matched fixture", null, false, Enum.Parse(parameters[8].ParameterType, "Wide"), false, false, false, 7, null };
        using var guard = new UnexpectedDialogGuard();
        DataFrame result = (DataFrame)getEqual.Invoke(instance, arguments);
        if (guard.Blocked) throw new InvalidOperationException("The valid matched-column fixture unexpectedly attempted to display a selection warning.");
        if ((bool)arguments[10] || (bool)arguments[11] || proxy.ReadCount != 1)
            throw new InvalidOperationException("The valid matched-column fixture cancelled, pivoted or retried acquisition.");
        return result;
    }

    static void Case(string name, object[] x, object[] y, Dictionary<string, Dictionary<string, double>> gold,
        bool headers = false, bool excludeHeaders = false, bool blankSecondHeader = false, int[] hiddenDataRows = null)
    {
        hiddenDataRows ??= Array.Empty<int>();
        object[][] columns = headers ? new[] {
            new object[] { "Before" }.Concat(x).ToArray(), new object[] { blankSecondHeader ? null : "After" }.Concat(y).ToArray()
        } : new[] { x, y };
        int[] hidden = hiddenDataRows.Select(r => r + (headers ? 1 : 0)).ToArray();
        var cells = new MatchedCells(columns, hidden);
        int first = headers && excludeHeaders ? 1 : 0;
        int count = columns[0].Length - first;
        var ranges = new[] { (0, first, count), (1, first, count) };
        DataFrame frame = Acquire(cells, ranges);
        int[] rows = Enumerable.Range(0, x.Length).Except(hiddenDataRows).ToArray();
        string label = name + (headers ? excludeHeaders ? " headers excluded" : blankSecondHeader ? " empty selected header" : " headers selected" : " no headers");
        Check(frame.VariableCount == 2 && frame.MinRows == rows.Length && frame.MaxRows == rows.Length, label + " retains both equal selected ranges");
        for (int c = 0; c < 2; c++)
        {
            object[] values = c == 0 ? x : y;
            var variable = (DoubleVariable)frame.Variables[c];
            Check(variable.Data.Take(variable.Length).SequenceEqual(rows.Select(r => Number(values[r]))), label + $" preserves every row in column {c + 1}");
            var origin = (WorksheetOrigin)variable.Origin;
            Check(origin.Mode == aligned && origin.HasTitle == (headers && !excludeHeaders) && origin.OriginGroup == 7,
                label + $" records row-preserving mode and actual header state for column {c + 1}");
        }
        var origins = frame.Variables.Select(v => (WorksheetOrigin)v.Origin).ToList();
        var replay = sheet.GetMethod("ProcessRecordedColumns", BindingFlags.NonPublic | BindingFlags.Static);
        DataFrame recorded = (DataFrame)replay.Invoke(null, new object[] { cells.Selection(ranges), origins, "matched replay" });
        Check(recorded.VariableCount == 2 && recorded.Name == "matched replay" && recorded.Variables.Select((v, i) =>
            v.Length == frame.Variables[i].Length && ((DoubleVariable)v).Data.Take(v.Length).SequenceEqual(((DoubleVariable)frame.Variables[i]).Data.Take(v.Length))).All(v => v),
            label + " replays both complete vectors in their original order");
        Reports(name, frame, gold);
    }

    static List<FrameParameter> Frames(string name)
    {
        using var reader = File.OpenText(Path.Combine(SourceRoot(), "StatsDirectUI", "Assets", "Operations", name + ".xml"));
        var operation = (Operation)new XmlSerializer(typeof(Operation)).Deserialize(reader);
        return operation.Steps.OfType<ParametersStep>().SelectMany(s => s.Parameters).OfType<FrameParameter>().ToList();
    }
    static void OperationBoundaries()
    {
        string[] optedIn = { "TPaired", "Wilcoxon", "Agreement", "Spearman", "Kendall", "CochranQ", "Cronbach",
            "Friedman", "FriedmanMulti", "FriedmanSimulateExactP", "PCRCoefficients", "PCRCorrelation", "PCRCovariance",
            "PCRCovarianceNoFollowOns", "PCRMatrix", "PCRScores", "TwoWay", "LadderPlot" };
        foreach (string name in optedIn)
        {
            List<FrameParameter> frames = Frames(name);
            Check(frames.Any(f => f.PreserveRowAlignment) && frames.Where(f => f.PreserveRowAlignment).All(f =>
                f.DataAcquisitionMode == DataAcquisitionMode.NumericReplaceMissing && f.SelectionMode == aligned && f.ColumnsAreSameLength),
                name + " deserializes its explicit matched-row opt-in without changing the legacy XML mode");
        }
        foreach (string name in new[] { "TUnpaired", "BarPlot", "ExactChiRbyC", "ExactChiRbyCScreen", "ControlPlot", "LadderPowers", "PyramidPlot" })
            Check(Frames(name).All(f => !f.PreserveRowAlignment && f.SelectionMode == f.DataAcquisitionMode), name + " retains its existing selection mode");

        // The opt-in does not reinterpret ordinary independent columns or table
        // selections. These are positive reads of their real serialized modes.
        object[] x = Enumerable.Range(1, 10).Cast<object>().ToArray();
        object[] shortY = Enumerable.Range(1, 7).Cast<object>().Concat(new object[3]).ToArray();
        var independent = new MatchedCells(new[] { x, shortY });
        var unpairedMode = Frames("TUnpaired").First(f => f.DataAcquisitionMode == DataAcquisitionMode.NumericReplaceMissing).SelectionMode;
        DataFrame unequal = Read(independent.Selection(new[] { (0, 0, 10), (1, 0, 10) }), unpairedMode, 0, 7);
        Check(unequal.VariableCount == 2 && unequal.Variables[0].Length == 10 && unequal.Variables[1].Length == 7,
            "unpaired independent columns still allow different sample sizes");
        foreach (string name in new[] { "BarPlot", "ExactChiRbyC" })
        {
            var fixture = new MatchedCells(new[] { x.Concat(new object[2]).ToArray(), x.Concat(new object[2]).ToArray() });
            var mode = Frames(name).First(f => f.DataAcquisitionMode == DataAcquisitionMode.NumericReplaceMissing).SelectionMode;
            DataFrame frame = Acquire(fixture, new[] { (0, 0, 12), (1, 0, 12) }, mode);
            Check(frame.VariableCount == 2 && frame.MinRows == 10 && frame.MaxRows == 10,
                name + " still trims common unused worksheet tails through its legacy mode");
        }
    }

    public static int Run()
    {
        try
        {
            OperationBoundaries();
            var gold = Gold();
            object[] x = Enumerable.Range(1, 100).Select(i => (object)(double)i).ToArray();
            object[] y = Enumerable.Range(1, 100).Select(i => (object)(double)(2 * i + i % 3)).ToArray();
            Case("complete", x, y, gold);
            Case("complete", x, y, gold, headers: true);
            Case("complete", x, y, gold, headers: true, excludeHeaders: true);
            Case("complete", x, y, gold, headers: true, blankSecondHeader: true);
            var leading = (object[])x.Clone(); leading[0] = null;
            Case("leading", leading, y, gold);
            Case("leading", leading, y, gold, headers: true);
            Case("leading", leading, y, gold, headers: true, excludeHeaders: true);
            Case("leading", leading, y, gold, headers: true, blankSecondHeader: true);
            var trailing = (object[])x.Clone(); trailing[99] = null;
            Case("trailing", trailing, y, gold, headers: true);
            var interior = (object[])x.Clone(); interior[49] = null;
            Case("interior", interior, y, gold, headers: true);
            var both = (object[])x.Clone(); both[0] = both[99] = null;
            var other = (object[])y.Clone(); other[49] = null;
            Case("both_ends", both, other, gold, headers: true);
            Case("hidden", x, y, gold, hiddenDataRows: new[] { 0, 9, 49, 99 });
            Case("hidden", x, y, gold, headers: true, blankSecondHeader: true, hiddenDataRows: new[] { 0, 9, 49, 99 });
            var irregular = Enumerable.Range(1, 100).Select(i => (object)(2 * i + 25 * Math.Sin(i))).ToArray();
            var irregularX = (object[])x.Clone(); irregularX[0] = irregularX[49] = null;
            Case("nonmonotone", irregularX, irregular, gold, headers: true);
        }
        catch (Exception error)
        {
            while (error is TargetInvocationException && error.InnerException != null) error = error.InnerException;
            Check(false, "unexpected exception: " + error);
        }
        Console.WriteLine($"MATCHED SELECTION: {checks} checks, {failures} failures");
        return failures;
    }

    // A regression must fail the test instead of waiting for a human to dismiss a
    // warning. This CBT hook is installed ONLY on this test's own calling thread;
    // it cannot see or close Iain's windows or windows in other application threads.
    sealed class UnexpectedDialogGuard : IDisposable
    {
        delegate IntPtr Hook(int code, IntPtr window, IntPtr details);
        readonly Hook callback;
        readonly IntPtr handle;
        public bool Blocked { get; private set; }
        public UnexpectedDialogGuard()
        {
            callback = (code, window, details) =>
            {
                if (code == 3)
                {
                    var name = new StringBuilder(64); GetClassNameW(window, name, name.Capacity);
                    if (name.ToString() == "#32770") { Blocked = true; return (IntPtr)1; }
                }
                return CallNextHookEx(IntPtr.Zero, code, window, details);
            };
            handle = SetWindowsHookExW(5, callback, IntPtr.Zero, GetCurrentThreadId());
            if (handle == IntPtr.Zero) throw new InvalidOperationException("Cannot install the test-thread dialog guard: " + Marshal.GetLastWin32Error());
        }
        public void Dispose() { UnhookWindowsHookEx(handle); GC.KeepAlive(callback); }
        [DllImport("user32.dll", SetLastError = true)] static extern IntPtr SetWindowsHookExW(int id, Hook hook, IntPtr module, uint thread);
        [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr window, IntPtr details);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassNameW(IntPtr window, StringBuilder name, int length);
        [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    }
}

internal sealed class MatchedCells : IGetCells
{
    readonly object[][] columns;
    readonly HashSet<int> hidden;
    internal MatchedCells(object[][] columns, params int[] hidden) { this.columns = columns; this.hidden = new(hidden); }
    object Cell(int row, int column) => row >= 0 && row < columns[column].Length ? columns[column][row] : null;
    int[] Rows(int first, int last) => Enumerable.Range(first, Math.Max(0, last - first + 1)).Where(r => !hidden.Contains(r)).ToArray();
    internal CellSelection Selection((int column, int first, int count)[] ranges)
    {
        var selection = new CellSelection { LongestRowCount = ranges.Max(r => r.count) };
        foreach (var range in ranges) selection.ColumnSelections.Add(new CellColumnSelection(this) {
            ColumnIndex = range.column, RowIndex = range.first, RowCount = range.count,
            WorkbookPath = "matched-fixture.xlsx", WorksheetName = "Sheet1"
        });
        return selection;
    }
    public string GetColumnTitle(int column) => column == 0 ? "Before" : "After";
    public bool IsFormattedLikeATitle(int column, int row) => false;
    public string GetCellText(int row, int column) => Convert.ToString(Cell(row, column), CultureInfo.InvariantCulture) ?? "";
    public double GetCellValue(int row, int column) => MatchedSelectionChecks.RawNumber(Cell(row, column));
    public (string[], int) GetCellTexts(int column, int first, int last)
    { var rows = Rows(first, last); return (rows.Select(r => GetCellText(r, column)).ToArray(), rows.Length); }
    public (double[], int) GetCellValues(int column, int first, int last)
    { var rows = Rows(first, last); return (rows.Select(r => GetCellValue(r, column)).ToArray(), rows.Length); }
    public (object[,], int) GetCellObjects(int column, int first, int last)
    {
        var rows = Rows(first, last); var cells = new object[rows.Length, 1];
        for (int i = 0; i < rows.Length; i++) cells[i, 0] = Cell(rows[i], column);
        return (cells, rows.Length);
    }
    public (string[], int) GetCellFormulae(int column, int first, int last) => GetCellTexts(column, first, last);
    public (DateTime[], int) GetCellDateValues(int column, int first, int last)
    { var rows = Rows(first, last); return (new DateTime[rows.Length], rows.Length); }
}

// DispatchProxy's generated subclass needs a public, non-sealed base type.
public class MatchedGridProxy : DispatchProxy, IGetCells
{
    internal MatchedCells Cells;
    internal CellSelection Selection;
    internal int ReadCount;
    protected override object Invoke(MethodInfo method, object[] arguments)
    {
        if (method.Name == "GetCellArray")
        {
            ReadCount++; arguments[8] = arguments[9] = false;
            return MatchedSelectionChecks.Read(Selection, (DataAcquisitionMode)arguments[1], (int)arguments[0], (int)arguments[10]);
        }
        throw new InvalidOperationException("Unexpected IGrid call in matched-column acquisition: " + method.Name);
    }
    public string GetColumnTitle(int column) => Cells.GetColumnTitle(column);
    public bool IsFormattedLikeATitle(int column, int row) => Cells.IsFormattedLikeATitle(column, row);
    public string GetCellText(int row, int column) => Cells.GetCellText(row, column);
    public double GetCellValue(int row, int column) => Cells.GetCellValue(row, column);
    public (string[], int) GetCellTexts(int column, int first, int last) => Cells.GetCellTexts(column, first, last);
    public (double[], int) GetCellValues(int column, int first, int last) => Cells.GetCellValues(column, first, last);
    public (object[,], int) GetCellObjects(int column, int first, int last) => Cells.GetCellObjects(column, first, last);
    public (string[], int) GetCellFormulae(int column, int first, int last) => Cells.GetCellFormulae(column, first, last);
    public (DateTime[], int) GetCellDateValues(int column, int first, int last) => Cells.GetCellDateValues(column, first, last);
}

public class MatchedReportHost : DispatchProxy
{
    sealed class NoProgress : IProgressBar, IProgressBarHost
    {
        public IProgressBar StartProgress(string operationDescription, bool provideProgress, bool display = true) => this;
        public bool Update(double fractionComplete) => false;
        public void Finish() { }
        public void Dispose() { }
    }
    static readonly NoProgress progress = new();
    protected override object Invoke(MethodInfo method, object[] arguments)
    {
        if (method.Name == "StartProgress") return progress;
        if (method.Name is "RoundU" or "pval" or "pval_half")
            return Convert.ToDouble(arguments[0], CultureInfo.InvariantCulture).ToString("R", CultureInfo.InvariantCulture);
        if (method.Name is "Error" or "Warning")
            throw new InvalidOperationException("Unexpected report " + method.Name + ": " + string.Join(" / ", arguments));
        Type type = method.ReturnType;
        return type == typeof(void) ? null : type.IsValueType ? Activator.CreateInstance(type) : null;
    }
}
