using System.Globalization;
using System.Reflection;
using StatsDirect.Builtins;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.Templates;
using StatsDirect.UI;
using StatsDirect.Utilities;

// Exercise the actual cell readers and group builder together, including the
// worksheet-origin replay boundary. No application window or user workbook is used.
internal static class RowAlignedSelectionChecks
{
    static int failures, checks;
    static readonly Type processor = typeof(CellSelection).Assembly.GetType("StatsDirect.UI.CellArrayProcessor");
    static readonly Type groups = typeof(CellSelection).Assembly.GetType("StatsDirect.UI.GridSelectionProcessor");
    static readonly Type sheet = typeof(CellSelection).Assembly.GetType("StatsDirect.UI.frmSpreadsheetGear");
    static readonly MethodInfo cellValue = sheet.GetMethod("ToCellValue", BindingFlags.Public | BindingFlags.Static);
    static readonly MethodInfo read = processor.GetMethod("ProcessCellArray", BindingFlags.Public | BindingFlags.Static);
    static readonly MethodInfo split = groups.GetMethod("BuildGroupsByIdentifier", BindingFlags.NonPublic | BindingFlags.Static);
    static readonly MethodInfo unplaced = groups.GetMethod("RowWithoutIdentifier", BindingFlags.NonPublic | BindingFlags.Static);
    static readonly MethodInfo normalizeHeader = groups.GetMethod("NormalizeSelectedGroupHeaders", BindingFlags.NonPublic | BindingFlags.Static);
    const DataAcquisitionMode aligned = (DataAcquisitionMode)108;

    sealed class Grid : IGetCells
    {
        readonly object[][] columns; readonly HashSet<int> hidden;
        public Grid(object[][] columns, params int[] hidden) { this.columns = columns; this.hidden = new(hidden); }
        object Cell(int r, int c) => r >= 0 && r < columns[c].Length ? columns[c][r] : null;
        int[] Rows(int first, int last) => Enumerable.Range(first, Math.Max(0, last - first + 1)).Where(r => !hidden.Contains(r)).ToArray();
        public string GetColumnTitle(int c) => c == 0 ? "Group" : "Value";
        public bool IsFormattedLikeATitle(int c, int r) => false;
        public string GetCellText(int r, int c) => Convert.ToString(Cell(r, c), CultureInfo.InvariantCulture) ?? "";
        public double GetCellValue(int r, int c) => Numeric(Cell(r, c));
        public (string[], int) GetCellTexts(int c, int first, int last)
        {
            var rows = Rows(first, last); var result = new string[Math.Max(0, last - first + 1)];
            for (int i = 0; i < rows.Length; i++) result[i] = GetCellText(rows[i], c);
            return (result, rows.Length);
        }
        public (double[], int) GetCellValues(int c, int first, int last)
        {
            var rows = Rows(first, last); return (rows.Select(r => GetCellValue(r, c)).ToArray(), rows.Length);
        }
        public (object[,], int) GetCellObjects(int c, int first, int last)
        {
            var rows = Rows(first, last); var result = new object[Math.Max(0, last - first + 1), 1];
            for (int i = 0; i < rows.Length; i++) result[i, 0] = Cell(rows[i], c);
            return (result, rows.Length);
        }
        public (string[], int) GetCellFormulae(int c, int first, int last) => GetCellTexts(c, first, last);
        public (DateTime[], int) GetCellDateValues(int c, int first, int last) { var rows = Rows(first, last); return (new DateTime[rows.Length], rows.Length); }
    }

    static void Check(bool ok, string message) { checks++; if (!ok) failures++; Console.WriteLine($"{(ok ? "ok  " : "FAIL")}  {message}"); }
    static CellColumnSelection Column(Grid grid, int col, int first, int count, string sheet = "Sheet1") => new(grid)
    { ColumnIndex = col, RowIndex = first, RowCount = count, WorkbookPath = "selection-fixture.xlsx", WorksheetName = sheet };
    static DataFrame Read(Grid grid, int col, int first, int count, DataAcquisitionMode mode, int hint = 0, bool refill = false, bool title = false, string sheet = "Sheet1")
    {
        var selection = new CellSelection { LongestRowCount = count }; selection.ColumnSelections.Add(Column(grid, col, first, count, sheet));
        return (DataFrame)read.Invoke(null, new object[] { selection, mode, hint, refill, title, 7, "selection-fixture" });
    }
    static (DataFrame frame, string empty) Split(ClassifierVariable ids, DoubleVariable values, DataAcquisitionMode mode)
    {
        object[] arguments = { ids, values, mode, null }; var result = (DataFrame)split.Invoke(null, arguments);
        return (result, (string)arguments[3]);
    }
    static object[] Ids() => Enumerable.Range(0, 200).Select(i => (object)(i < 90 ? "A" : "B")).ToArray();
    static object[] Values() => Enumerable.Range(1, 200).Select(i => (object)(100 + (i * 17 % 41) / 5.0 + (i > 90 ? 1.25 : 0))).ToArray();
    static double Numeric(object value) => (double)cellValue.Invoke(null, new[] { value });
    static double[] Numbers(object[] values, IEnumerable<int> rows) => rows.Select(i => Numeric(values[i])).Select(x => x == Constant.MISSING * 10 ? Constant.MISSING : x).ToArray();
    static Dictionary<string, Dictionary<string, double>> Gold()
    {
        var result = new Dictionary<string, Dictionary<string, double>>();
        foreach (string line in File.ReadLines(Path.Combine(AppContext.BaseDirectory, "row-alignment.tsv")).Skip(1))
        { var f = line.Split('\t'); if (!result.ContainsKey(f[0])) result[f[0]] = new(); result[f[0]][f[1]] = double.Parse(f[2], CultureInfo.InvariantCulture); }
        return result;
    }
    static void ReportMatchesR(string name, DataFrame frame, Dictionary<string, Dictionary<string, double>> gold, bool paired = false)
    {
        var input = new ParameterBag(); input.AddInput("data", frame); input.AddInput("gamma", 0.95);
        var output = (paired ? Parametric.RptTPaired(input) : Parametric.RptTUnpaired(input)).ParameterBag;
        foreach (var expected in gold[name])
        {
            double actual = output[expected.Key].AsDouble;
            Check(double.IsFinite(actual) && Math.Abs(actual - expected.Value) <= 1e-10 * Math.Max(1e-3, Math.Abs(expected.Value)),
                $"{name}: {expected.Key} agrees with R ({actual:R})");
        }
    }
    static void Case(string name, object[] ids, object[] values, Dictionary<string, Dictionary<string, double>> gold, int[] hidden = null, bool header = false, int first = 0)
    {
        hidden ??= Array.Empty<int>();
        var grid = new Grid(new[] { ids, values }, hidden); int count = ids.Length - first;
        var idFrame = Read(grid, 0, first, count, DataAcquisitionMode.CategoryCombineAllColumns);
        var labels = (ClassifierVariable)idFrame.Variables[0];
        var numericFrame = Read(grid, 1, first, count, aligned, labels.Length);
        normalizeHeader.Invoke(null, new object[] { numericFrame, labels.Length, labels.Origin, grid });
        var numeric = (DoubleVariable)numericFrame.Variables[0];
        var rows = Enumerable.Range(first + (header ? 1 : 0), count - (header ? 1 : 0)).Except(hidden).ToArray();
        Check(labels.Length == rows.Length && numeric.Length == rows.Length, $"{name}: identifiers and outcomes retain the same visible selected rows");
        Check(numeric.Data.Take(numeric.Length).SequenceEqual(Numbers(values, rows)), $"{name}: every numeric cell keeps its selected position, including missing values");
        Check((int)unplaced.Invoke(null, new object[] { labels.Data, numeric.Data, labels.Length }) == 0, $"{name}: no observation loses its identifier");
        var origin = (WorksheetOrigin)numeric.Origin;
        var replay = (DoubleVariable)Read(grid, 1, first, count, aligned, 0, true, origin.HasTitle).Variables[0];
        Check(replay.Length == numeric.Length && replay.Data.Take(replay.Length).SequenceEqual(numeric.Data.Take(numeric.Length)), $"{name}: recorded origin replay preserves the full data vector");
        foreach (var mode in new[] { DataAcquisitionMode.NumericReplaceMissing, DataAcquisitionMode.NumericSkipMissing })
        {
            var grouped = Split(labels, numeric, mode);
            Check(grouped.empty == null && grouped.frame.VariableCount == 2, $"{name}: the production group builder accepts both samples ({mode})");
            ReportMatchesR(name, grouped.frame, gold);
        }
    }
    public static int Run()
    {
        if (split == null || !Enum.IsDefined(aligned)) { Check(false, "row-preserving grouped selection is implemented"); return failures; }
        var gold = Gold();
        Case("complete", Ids(), Values(), gold);
        var last = Values(); last[199] = null; Case("trailing", Ids(), last, gold);
        var first = Values(); first[0] = null; Case("leading", Ids(), first, gold);
        var middle = Values(); middle[89] = null; middle[90] = null; Case("interior", Ids(), middle, gold);
        var tailIds = Ids(); var tailValues = Values(); for (int i = 130; i < 200; i++) { tailIds[i] = null; tailValues[i] = null; }
        Case("tail", tailIds, tailValues, gold);
        Case("hidden", Ids(), Values(), gold, new[] { 0, 50, 150, 199 });
        var headerIds = new object[] { "Group" }.Concat(Ids()).ToArray();
        var headerValues = new object[] { "Measurement" }.Concat(Values()).ToArray();
        Case("complete", headerIds, headerValues, gold, header: true);
        var headerMissing = (object[])headerValues.Clone(); headerMissing[1] = null; headerMissing[200] = null;
        Case("header_missing", headerIds, headerMissing, gold, header: true);
        Case("complete", headerIds, headerValues, gold, first: 1);
        var blankHeaderValues = new object[] { null }.Concat(Values()).ToArray();
        Case("complete", headerIds, blankHeaderValues, gold, header: true);
        Case("hidden", headerIds, blankHeaderValues, gold, new[] { 1, 51, 151, 200 }, header: true);
        var blankHeaderMissing = (object[])blankHeaderValues.Clone(); blankHeaderMissing[1] = null; blankHeaderMissing[200] = null;
        Case("header_missing", headerIds, blankHeaderMissing, gold, header: true);

        var blankHeaderGrid = new Grid(new[] { headerIds, blankHeaderValues });
        var headerExcludedIds = (ClassifierVariable)Read(blankHeaderGrid, 0, 1, 200, DataAcquisitionMode.CategoryCombineAllColumns).Variables[0];
        var blankHeaderFrame = Read(blankHeaderGrid, 1, 0, 201, aligned, 200);
        var originalBlankOrigin = (WorksheetOrigin)blankHeaderFrame.Variables[0].Origin;
        normalizeHeader.Invoke(null, new object[] { blankHeaderFrame, 200, headerExcludedIds.Origin, blankHeaderGrid });
        Check(blankHeaderFrame.MaxRows == 200 && ((WorksheetOrigin)blankHeaderFrame.Variables[0].Origin).HasTitle && !originalBlankOrigin.HasTitle,
            "positive row context supports an empty selected outcome header even when identifiers exclude theirs, without mutating the old origin");
        ReportMatchesR("complete", Split(headerExcludedIds, (DoubleVariable)blankHeaderFrame.Variables[0], DataAcquisitionMode.NumericReplaceMissing).frame, gold);

        var referenceHeader = (WorksheetOrigin)Read(blankHeaderGrid, 0, 0, 201, DataAcquisitionMode.CategoryCombineAllColumns).Variables[0].Origin;
        foreach (string scenario in new[] { "unknown book", "different sheet", "different end", "no visible-header reader", "hidden physical header", "numeric prefix" })
        {
            var candidateGrid = scenario == "numeric prefix"
                ? new Grid(new[] { headerIds, new object[] { 42.0 }.Concat(Values()).ToArray() }) : blankHeaderGrid;
            var candidate = Read(candidateGrid, 1, 0, 201, aligned, 200);
            var origin = (WorksheetOrigin)candidate.Variables[0].Origin;
            var reference = new WorksheetOrigin(referenceHeader.WorkbookPath, referenceHeader.WorksheetName, referenceHeader.Column,
                referenceHeader.TopRow, referenceHeader.Rows, referenceHeader.Mode, referenceHeader.HasTitle, false, 7);
            if (scenario == "unknown book") { origin.WorkbookPath = null; reference.WorkbookPath = null; }
            if (scenario == "different sheet") origin.WorksheetName = "Other sheet";
            if (scenario == "different end") origin.Rows++;
            IGetCells visibleReader = scenario == "no visible-header reader" ? null : scenario == "hidden physical header"
                ? new Grid(new[] { headerIds, blankHeaderValues }, 0) : candidateGrid;
            normalizeHeader.Invoke(null, new object[] { candidate, 200, reference, visibleReader });
            Check(candidate.MaxRows == 201 && !origin.HasTitle, $"an extra row is not silently removed with {scenario}");
        }

        var grid = new Grid(new[] { Ids(), new object[5].Concat(Values()).ToArray() });
        var ids = (ClassifierVariable)Read(grid, 0, 0, 200, DataAcquisitionMode.CategoryCombineAllColumns).Variables[0];
        var positional = (DoubleVariable)Read(grid, 1, 5, 200, aligned, 200, sheet: "Other sheet").Variables[0];
        var positionalGroups = Split(ids, positional, DataAcquisitionMode.NumericReplaceMissing);
        Check(positionalGroups.empty == null, "explicit equal-length positional selections can use different starting rows and sheets");
        ReportMatchesR("complete", positionalGroups.frame, gold);
        var shorter = (DoubleVariable)Read(grid, 1, 5, 199, aligned, 200).Variables[0];
        Check(shorter.Length == 199, "a genuinely shorter selection is never padded to the requested length");
        try { Split(ids, shorter, DataAcquisitionMode.NumericReplaceMissing); Check(false, "mismatched lengths must be refused"); }
        catch (TargetInvocationException e) when (e.InnerException is ArgumentException) { Check(true, "mismatched lengths are refused before grouping"); }

        var allMissing = new Grid(new[] { Ids(), new object[200] });
        var emptyValues = (DoubleVariable)Read(allMissing, 1, 0, 200, aligned, 200).Variables[0];
        Check(Split(ids, emptyValues, DataAcquisitionMode.NumericReplaceMissing).empty == "A", "a group with no numeric observations is refused by name");
        var missingId = Ids(); missingId[50] = null; var bad = new Grid(new[] { missingId, Values() });
        var badIds = (ClassifierVariable)Read(bad, 0, 0, 200, DataAcquisitionMode.CategoryCombineAllColumns).Variables[0];
        var badValues = (DoubleVariable)Read(bad, 1, 0, 200, aligned, 200).Variables[0];
        Check((int)unplaced.Invoke(null, new object[] { badIds.Data, badValues.Data, badIds.Length }) == 51, "a value without a group still reports its correct row");
        var hiddenAll = new Grid(new[] { Ids(), Values() }, Enumerable.Range(0, 200).ToArray());
        var noIds = (ClassifierVariable)Read(hiddenAll, 0, 0, 200, DataAcquisitionMode.CategoryCombineAllColumns).Variables[0];
        Check(noIds.Length == 0 && noIds.GroupCount == 0, "a fully filtered selection produces no synthetic identifier rows");

        var covarianceGuard = groups.GetMethod("GroupWithoutUsableCovarianceRows", BindingFlags.NonPublic | BindingFlags.Static);
        var completeGrid = new Grid(new[] { Ids(), Values() });
        var completeValues = (DoubleVariable)Read(completeGrid, 1, 0, 200, aligned, 200).Variables[0];
        var replicates = new DataFrame(completeValues);
        Check(covarianceGuard.Invoke(null, new object[] { ids, completeValues, replicates }) == null, "grouped covariance accepts complete aligned X/Y rows");
        var absentX = Values(); Array.Fill(absentX, null, 0, 90);
        var absentXGrid = new Grid(new[] { Ids(), absentX });
        var absentXValues = (DoubleVariable)Read(absentXGrid, 1, 0, 200, aligned, 200).Variables[0];
        Check((string)covarianceGuard.Invoke(null, new object[] { ids, absentXValues, replicates }) == "A", "grouped covariance refuses a group without observed X");
        Check((string)covarianceGuard.Invoke(null, new object[] { ids, completeValues, new DataFrame(absentXValues) }) == "A", "grouped covariance refuses a group without any observed Y replicate");
        var complementaryX = Enumerable.Repeat<object>(null, 200).ToArray(); complementaryX[0] = 1.0;
        var complementaryY = Values(); Array.Fill(complementaryY, null, 0, 90); complementaryY[1] = 2.0;
        var complement = new Grid(new[] { Ids(), complementaryX, complementaryY });
        var x = (DoubleVariable)Read(complement, 1, 0, 200, aligned, 200).Variables[0];
        var y = (DoubleVariable)Read(complement, 2, 0, 200, aligned, 200).Variables[0];
        Check((string)covarianceGuard.Invoke(null, new object[] { ids, x, new DataFrame(y) }) == "A", "grouped covariance requires X and Y on the same observation, not different missing rows");

        foreach (var marker in new[] { "*", ".", "MISSING", "* (missing)", "NA", "N/A", "NaN", "NULL" })
        {
            var marked = Values(); marked[0] = marker; var markerGrid = new Grid(new[] { Ids(), marked });
            var value = (DoubleVariable)Read(markerGrid, 1, 0, 200, aligned, 199).Variables[0];
            Check(value.Length == 200 && value.Data[0] == Constant.MISSING && !((WorksheetOrigin)value.Origin).HasTitle,
                $"explicit missing marker '{marker}' is not silently consumed as an extra title row");
        }

        // Both outcomes belong to the same 200 observations, but only one selected range includes its title.
        var mixed = new Grid(new[] { headerIds, headerValues, headerValues });
        var a = Read(mixed, 1, 0, 201, aligned, 200); var b = Read(mixed, 2, 1, 200, aligned, 200);
        var origins = new List<WorksheetOrigin> { (WorksheetOrigin)a.Variables[0].Origin, (WorksheetOrigin)b.Variables[0].Origin };
        var selection = new CellSelection { LongestRowCount = 201 };
        selection.ColumnSelections.Add(Column(mixed, 1, 0, 201)); selection.ColumnSelections.Add(Column(mixed, 2, 1, 200));
        // The real ReadColumns host creates these descriptors before recording the workbook/sheet.
        foreach (var column in selection.ColumnSelections) { column.WorkbookPath = null; column.WorksheetName = null; }
        var reader = sheet.GetMethod("ProcessRecordedColumns", BindingFlags.NonPublic | BindingFlags.Static);
        var recorded = (DataFrame)reader.Invoke(null, new object[] { selection, origins, "mixed replay" });
        Check(origins[0].HasTitle && !origins[1].HasTitle && recorded.MinRows == 200 && recorded.MaxRows == 200, "multi-column replay honors each recorded selected header independently");
        Check(recorded.Variables.Cast<DoubleVariable>().All(v => v.Data.Take(v.Length).SequenceEqual(Numbers(Values(), Enumerable.Range(0, 200)))), "multi-column replay preserves every value and variable order");
        Check(recorded.Name == "mixed replay" && recorded.Variables.Select((v, i) =>
            v.Origin is WorksheetOrigin origin && origin.WorkbookPath == origins[i].WorkbookPath
                && origin.WorksheetName == origins[i].WorksheetName && origin.Column == origins[i].Column
                && origin.TopRow == origins[i].TopRow && origin.Rows == origins[i].Rows
                && origin.Mode == aligned && origin.HasTitle == origins[i].HasTitle && origin.OriginGroup == origins[i].OriginGroup).All(x => x),
            "the real replay boundary retains workbook, sheet, range, mode, title and origin-group metadata");
        var nowHiddenHeader = new Grid(new[] { headerIds, headerValues }, 0);
        var hiddenHeaderReplay = (DoubleVariable)Read(nowHiddenHeader, 1, 0, 201, aligned, 0, true, true).Variables[0];
        Check(hiddenHeaderReplay.Length == 200 && hiddenHeaderReplay.Data.Take(200).SequenceEqual(Numbers(Values(), Enumerable.Range(0, 200))), "replay does not consume the first visible observation when its recorded header is hidden");

        var legacy = new Grid(new[] { Values(), Values().Take(180).Concat(new object[20]).ToArray() });
        Check(Read(legacy, 0, 0, 200, DataAcquisitionMode.NumericReplaceMissing).MaxRows == 200 && Read(legacy, 1, 0, 200, DataAcquisitionMode.NumericReplaceMissing).MaxRows == 180,
            "ordinary separate-column acquisition still permits unequal sample sizes");

        var pairIds = Enumerable.Range(0, 200).Select(i => (object)(i < 100 ? "A" : "B")).ToArray();
        var pairValues = Values(); pairValues[0] = null; pairValues[5] = null; pairValues[101] = null;
        var pairGrid = new Grid(new[] { pairIds, pairValues });
        var pairLabels = (ClassifierVariable)Read(pairGrid, 0, 0, 200, DataAcquisitionMode.CategoryCombineAllColumns).Variables[0];
        var pairNumbers = (DoubleVariable)Read(pairGrid, 1, 0, 200, aligned, 200).Variables[0];
        var paired = Split(pairLabels, pairNumbers, DataAcquisitionMode.NumericReplaceMissing);
        Check(paired.frame.MinRows == 100 && paired.frame.MaxRows == 100 && ((DoubleVariable)paired.frame.Variables[0]).Data[0] == Constant.MISSING,
            "paired groups retain missing positions rather than closing gaps independently");
        ReportMatchesR("paired", paired.frame, gold, true);

        var threeIds = Enumerable.Range(0, 200).Select(i => (object)(i < 60 ? "A" : i < 140 ? "B" : "C")).ToArray();
        var threeValues = Values(); threeValues[0] = null; threeValues[80] = null; threeValues[199] = null;
        var threeGrid = new Grid(new[] { threeIds, threeValues });
        var threeLabels = (ClassifierVariable)Read(threeGrid, 0, 0, 200, DataAcquisitionMode.CategoryCombineAllColumns).Variables[0];
        var threeNumbers = (DoubleVariable)Read(threeGrid, 1, 0, 200, aligned, 200).Variables[0];
        foreach (var mode in new[] { DataAcquisitionMode.NumericReplaceMissing, DataAcquisitionMode.NumericSkipMissing })
        {
            var three = Split(threeLabels, threeNumbers, mode);
            Check(three.empty == null && three.frame.VariableCount == 3, $"three unequal groups are retained for ANOVA/Kruskal-Wallis ({mode})");
            int[] starts = { 0, 60, 140 }, lengths = { 60, 80, 60 };
            for (int group = 0; group < 3; group++)
            {
                var expected = Numbers(threeValues, Enumerable.Range(starts[group], lengths[group])).AsEnumerable();
                if (mode == DataAcquisitionMode.NumericSkipMissing) expected = expected.Where(v => v != Constant.MISSING);
                var actual = (DoubleVariable)three.frame.Variables[group];
                Check(actual.Data.Take(actual.Length).SequenceEqual(expected), $"group {group + 1} preserves exactly its observations ({mode})");
            }
        }
        Console.WriteLine($"ROW ALIGNMENT: {checks} checks, {failures} failures");
        return failures;
    }
}
