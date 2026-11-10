using System.Globalization;
using System.Reflection;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.Templates;
using StatsDirect.UI;
using StatsDirect.Utilities;

// Invoke the real long-layout acquisition methods, with cell reads backed by a
// deterministic worksheet. No Windows form or user workbook is involved.
internal static class GroupedRepeatedSelectionChecks
{
    static readonly Type acquisition = typeof(CellSelection).Assembly.GetType("StatsDirect.UI.GridSelectionProcessor");
    static readonly MethodInfo read = typeof(CellSelection).Assembly.GetType("StatsDirect.UI.CellArrayProcessor").GetMethod("ProcessCellArray", BindingFlags.Public | BindingFlags.Static);
    static readonly MethodInfo gidx2 = acquisition.GetMethod("Gidx2", BindingFlags.NonPublic | BindingFlags.Instance);
    static readonly MethodInfo gidx3 = acquisition.GetMethod("Gidx3", BindingFlags.NonPublic | BindingFlags.Instance);
    static int checks, failures;

    sealed class Grid : IGrid, IGetCells
    {
        readonly object[][] columns;
        readonly HashSet<int> hidden;
        readonly Queue<int> selections = new(new[] { 0, 1, 2 });
        internal readonly List<DataAcquisitionMode> Modes = new();
        internal Grid(object[][] columns, int[] hidden = null) { this.columns = columns; this.hidden = new(hidden ?? Array.Empty<int>()); }
        object Cell(int r, int c) => r >= 0 && r < columns[c].Length ? columns[c][r] : null;
        int[] Rows(int first, int last) => Enumerable.Range(first, Math.Max(0, last - first + 1)).Where(r => !hidden.Contains(r)).ToArray();
        public string GetCellText(int r, int c) => Convert.ToString(Cell(r, c), CultureInfo.InvariantCulture) ?? "";
        public double GetCellValue(int r, int c) => Cell(r, c) is double d ? d : Cell(r, c) is int n ? n : Constant.MISSING;
        public string GetColumnTitle(int c) => new[] { "Treatment", "Block", "Outcome" }[c];
        public bool IsFormattedLikeATitle(int c, int r) => false;
        public (string[], int) GetCellTexts(int c, int first, int last)
        {
            var rows = Rows(first, last); var values = Enumerable.Repeat("", last - first + 1).ToArray();
            for (int i = 0; i < rows.Length; i++) values[i] = GetCellText(rows[i], c);
            return (values, rows.Length);
        }
        public (double[], int) GetCellValues(int c, int first, int last)
        { var rows = Rows(first, last); return (rows.Select(r => GetCellValue(r, c)).ToArray(), rows.Length); }
        public (object[,], int) GetCellObjects(int c, int first, int last)
        {
            var rows = Rows(first, last); var values = new object[last - first + 1, 1];
            for (int i = 0; i < rows.Length; i++) values[i, 0] = Cell(rows[i], c);
            return (values, rows.Length);
        }
        public (string[], int) GetCellFormulae(int c, int first, int last) => GetCellTexts(c, first, last);
        public (DateTime[], int) GetCellDateValues(int c, int first, int last) => (new DateTime[last - first + 1], Rows(first, last).Length);
        public DataFrame GetCellArray(int hint, DataAcquisitionMode mode, int min, int max, string prompt, string cancel, bool pivot, bool batching, out bool userCancelled, out bool wasPivoted, int originGroup, WorksheetOrigin rowReference = null, IReadOnlyList<bool> codingRows = null)
        {
            userCancelled = wasPivoted = false;
            if (selections.Count == 0) throw new InvalidOperationException("Unexpected repeated selection: " + prompt);
            int c = selections.Dequeue(); Modes.Add(mode);
            var selection = new CellSelection { LongestRowCount = columns[c].Length };
            selection.ColumnSelections.Add(new CellColumnSelection(this) { ColumnIndex = c, RowIndex = 0, RowCount = columns[c].Length, WorkbookPath = WorkbookPath, WorksheetName = ActiveWorksheetName });
            return (DataFrame)read.Invoke(null, new object[] { selection, mode, hint, false, false, originGroup, "long-layout fixture" });
        }
        public void ClearSelection() { }
        public bool Dirty => false;
        public string WorkbookPath => "long-layout.xlsx";
        public string ActiveWorksheetName => "Data";
        public Area UsedArea => throw new NotSupportedException();
        public StatsDirect.UI.Range Selection { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public Pane SelectedPane => null;
        public void EnsureActive() { }
        public string Id() => "long-layout";
        public bool SelectPane(Pane pane) => throw new NotSupportedException();
        public object[,] GetValues(int top, int left, int bottom, int right) => throw new NotSupportedException();
        public bool HasSelection() => true;
        public void SetValues(int top, int left, object[,] values) => throw new NotSupportedException();
        public void WriteDataFrame(DataFrame frame, bool isFormulae, string missingIndicator, RelativePosition writePosition) => throw new NotSupportedException();
        public void Refill(List<IVariable> variable) => throw new NotSupportedException();
        public DataFrame ReadColumns(IList<WorksheetOrigin> origins) => throw new NotSupportedException();
    }

    static void Check(bool ok, string message) { checks++; if (!ok) failures++; Console.WriteLine($"{(ok ? "ok  " : "FAIL")}  {message}"); }
    static object[][] Data(bool repeats = false) => new[]
    {
        new object[] { "A", "A", "A", "A", "B", "B", "B", "B" },
        repeats ? new object[] { "x", "x", "y", "y", "x", "x", "y", "y" } : new object[] { "b1", "b2", "b3", "b4", "b1", "b2", "b3", "b4" },
        new object[] { 11.0, 12.0, 13.0, 14.0, 21.0, 22.0, 23.0, 24.0 }
    };
    static object[][] Tail(object[][] data) => data.Select(c => c.Concat(new object[7]).ToArray()).ToArray();
    static object[][] Header(object[][] data, bool emptyOutcome) => data.Select((c, i) => new[] { i == 2 && emptyOutcome ? null : (object)new[] { "Treatment", "Block", "Outcome" }[i] }.Concat(c).ToArray()).ToArray();
    static int[] Kept(object[][] data, int[] hidden) => Enumerable.Range(0, data[0].Length).Except(hidden ?? Array.Empty<int>()).Where(r => data[0][r] is string a && (a == "A" || a == "B") && data[1][r] is string b && (b.StartsWith("b") || b == "x" || b == "y")).ToArray();
    static double Numeric(object x) => x is double d ? d : Constant.MISSING;
    static void Modes(Grid grid, string name) => Check(grid.Modes.SequenceEqual(new[] { DataAcquisitionMode.CategoryCombineAllColumns, DataAcquisitionMode.CategoryCombineAllColumns, (DataAcquisitionMode)108 }), name + ": both identifier readers and numeric outcome use aligned acquisition");
    static void TwoWay(string name, object[][] data, int[] hidden = null, int extraEmptyRows = 0)
    {
        var grid = new Grid(data, hidden); var instance = Activator.CreateInstance(acquisition, new object[] { grid });
        var frame = (DataFrame)gidx2.Invoke(instance, new object[] { 2, 2, false, false, 19 });
        var rows = Kept(data, hidden); var groups = rows.Select(r => (string)data[0][r]).Distinct().ToArray();
        // Block identities remain global even when that row's treatment is absent.
        var blocks = Enumerable.Range(0, data[1].Length).Except(hidden ?? Array.Empty<int>()).Select(r => data[1][r]).OfType<string>().Where(b => b.StartsWith("b")).Distinct().ToArray();
        Check(frame.VariableCount == groups.Length, name + ": both treatment columns survive");
        for (int g = 0; g < groups.Length; g++)
        {
            var expected = blocks.Select(block => rows.Where(r => (string)data[0][r] == groups[g] && (string)data[1][r] == block).Select(r => Numeric(data[2][r])).DefaultIfEmpty(Constant.MISSING).Last()).Concat(Enumerable.Repeat(Constant.MISSING, extraEmptyRows)).ToArray();
            var actual = (DoubleVariable)frame.Variables[g];
            Check(actual.Length == expected.Length && actual.Data.Take(actual.Length).SequenceEqual(expected), name + ": treatment " + groups[g] + " keeps each observed and missing outcome in its correct block");
        }
        Modes(grid, name);
    }
    static void Repeated(string name, object[][] data, DataAcquisitionMode2D mode, int[] hidden = null)
    {
        var grid = new Grid(data, hidden); var instance = Activator.CreateInstance(acquisition, new object[] { grid });
        var frame = (DataFrame2D)gidx3.Invoke(instance, new object[] { 2, 2, 0, null, mode, false, false, 19 });
        var rows = Kept(data, hidden); var groups = rows.Select(r => (string)data[0][r]).Distinct().ToArray(); var blocks = rows.Select(r => (string)data[1][r]).Distinct().ToArray();
        int longest = groups.SelectMany(group => blocks.Select(block => rows.Count(r => (string)data[0][r] == group && (string)data[1][r] == block))).Max();
        for (int g = 0; g < groups.Length; g++) for (int b = 0; b < blocks.Length; b++)
        {
            var expected = rows.Where(r => (string)data[0][r] == groups[g] && (string)data[1][r] == blocks[b]).Select(r => Numeric(data[2][r])).ToList();
            if (mode == DataAcquisitionMode2D.BlockThenGroup) while (expected.Count < longest) expected.Add(Constant.MISSING);
            var actual = (DoubleVariable)(mode == DataAcquisitionMode2D.GroupThenBlock ? frame.Variables[g][b] : frame.Variables[b][g]);
            Check(actual != null && actual.Length == expected.Count && actual.Data.Take(actual.Length).SequenceEqual(expected), name + ": " + mode + " / " + groups[g] + "/" + blocks[b] + " preserves replicate positions");
        }
        Modes(grid, name + " / " + mode);
    }
    static void RejectUnlabelled(string name, int column)
    {
        var data = Data(true); data[column][2] = null;
        var grid = new Grid(data); var instance = Activator.CreateInstance(acquisition, new object[] { grid });
        bool refused = false;
        try { gidx3.Invoke(instance, new object[] { 2, 2, 0, null, DataAcquisitionMode2D.GroupThenBlock, false, false, 19 }); }
        catch (TargetInvocationException ex) { refused = ex.InnerException is TemplateOperationCancelledException && ex.InnerException.Message.Contains("Row 3"); }
        Check(refused, name + ": an observed value without an identifier is refused with its selected row number");
    }
    internal static int Run()
    {
        checks = failures = 0;
        TwoWay("long two-way complete", Data());
        var edges = Data(); edges[2][0] = edges[2][7] = null; TwoWay("long two-way missing edge outcomes", edges);
        TwoWay("long two-way empty used-range tail", Tail(Data()));
        TwoWay("long two-way hidden complete block", Data(), new[] { 1, 5 });
        var blanks = Data(); foreach (int r in new[] { 1, 5 }) for (int c = 0; c < 3; c++) blanks[c][r] = null;
        TwoWay("long two-way wholly missing block", blanks);
        var differentBlanks = Data(); foreach (int r in new[] { 1, 6 }) for (int c = 0; c < 3; c++) differentBlanks[c][r] = null;
        TwoWay("long two-way incomplete different blocks", differentBlanks);
        var absentBlocks = Data(); foreach (int r in new[] { 1, 5 }) absentBlocks[1][r] = absentBlocks[2][r] = null;
        // The retained treatment counts still allow four rows; the absent block
        // must be skipped before indexing, and the unused final row stays missing.
        TwoWay("long two-way missing block ID beside missing outcome", absentBlocks, extraEmptyRows: 1);
        var absentTreatments = Data(); foreach (int r in new[] { 1, 5 }) absentTreatments[0][r] = absentTreatments[2][r] = null;
        TwoWay("long two-way missing treatment ID beside missing outcome", absentTreatments);
        TwoWay("long two-way text headers", Header(Data(), false));
        TwoWay("long two-way empty outcome header", Header(Data(), true));
        foreach (var mode in new[] { DataAcquisitionMode2D.GroupThenBlock, DataAcquisitionMode2D.BlockThenGroup })
        {
            Repeated("long repeated complete", Data(true), mode);
            var missing = Data(true); missing[2][0] = missing[2][7] = null; Repeated("long repeated missing edge outcomes", missing, mode);
            Repeated("long repeated empty used-range tail", Tail(Data(true)), mode);
            Repeated("long repeated hidden observations", Data(true), mode, new[] { 1, 5 });
            var empty = Data(true); foreach (int r in new[] { 1, 5 }) for (int c = 0; c < 3; c++) empty[c][r] = null;
            Repeated("long repeated wholly missing observations", empty, mode);
            foreach (int id in new[] { 0, 1 })
            {
                var partial = Data(true); foreach (int r in new[] { 1, 5 }) partial[id][r] = partial[2][r] = null;
                Repeated("long repeated missing identifier " + id + " beside missing outcome", partial, mode);
            }
            Repeated("long repeated text headers", Header(Data(true), false), mode);
            Repeated("long repeated empty outcome header", Header(Data(true), true), mode);
        }
        RejectUnlabelled("long repeated missing treatment ID", 0);
        RejectUnlabelled("long repeated missing block ID", 1);
        Console.WriteLine($"Long grouped/repeated selection: {checks - failures}/{checks} checks passed");
        return failures;
    }
}
