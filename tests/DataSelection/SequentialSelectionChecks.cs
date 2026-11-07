using System.Globalization;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using StatsDirect.Builtins;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.Templates;
using StatsDirect.UI;
using StatsDirect.Utilities;

// Two separately prompted variables must preserve their common row positions.
// This exercises the real GetCellEqual count/reference boundary, not simulated padding.
internal static class SequentialSelectionChecks
{
    const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    static readonly Type selector = typeof(CellSelection).Assembly.GetType("StatsDirect.UI.GridSelectionProcessor");
    static readonly MethodInfo equal = selector.GetMethod("GetCellEqual", flags);
    static readonly MethodInfo replay = typeof(CellSelection).Assembly.GetType("StatsDirect.UI.frmSpreadsheetGear").GetMethod("ProcessRecordedColumns", flags);
    static int checks, failures;
    static void Check(bool ok, string message) { checks++; if (!ok) failures++; Console.WriteLine($"{(ok ? "ok  " : "FAIL")}  sequential: {message}"); }
    static string Root()
    {
        foreach (string start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
            for (var path = new DirectoryInfo(start); path != null; path = path.Parent)
                if (Directory.Exists(Path.Combine(path.FullName, "StatsDirectUI", "Assets", "Operations"))) return path.FullName;
        throw new DirectoryNotFoundException("Cannot find operation definitions for sequential checks.");
    }
    static FrameParameter Parameter(string operation, string name)
    {
        var document = XDocument.Load(Path.Combine(Root(), "StatsDirectUI", "Assets", "Operations", operation + ".xml"));
        XNamespace ns = "http://www.statsdirect.com/schemas/Operation.xsd";
        var frame = document.Descendants(ns + "frame").Single(f => (string)f.Element(ns + "name") == name);
        var serializer = new XmlSerializer(typeof(FrameParameter), new XmlRootAttribute("frame") { Namespace = ns.NamespaceName });
        using var reader = frame.CreateReader();
        return (FrameParameter)serializer.Deserialize(reader);
    }
    static Dictionary<string, Dictionary<string, double>> Gold()
    {
        var result = new Dictionary<string, Dictionary<string, double>>();
        string path = Path.Combine(AppContext.BaseDirectory, "sequential-selection.tsv");
        if (!File.Exists(path)) path = Path.Combine(Root(), "tests", "DataSelection", "benchmarks", "sequential-selection.tsv");
        foreach (string line in File.ReadLines(path).Skip(1))
        {
            var fields = line.Split('\t');
            if (!result.ContainsKey(fields[0])) result[fields[0]] = new();
            result[fields[0]][fields[1]] = double.Parse(fields[2], CultureInfo.InvariantCulture);
        }
        return result;
    }
    static DataFrame Acquire(MatchedCells cells, (int column, int first, int count) range, FrameParameter parameter, DataFrame reference = null)
    {
        IGrid grid = DispatchProxy.Create<IGrid, MatchedGridProxy>();
        var proxy = (MatchedGridProxy)(object)grid;
        proxy.Cells = cells; proxy.Selection = cells.Selection(new[] { range });
        if (reference == null)
            return MatchedSelectionChecks.Read(proxy.Selection, parameter.SelectionMode, 0, 12);
        var methodParameters = equal.GetParameters();
        object[] args = { reference.MaxRows, parameter.SelectionMode,
            Enum.Parse(methodParameters[2].ParameterType, "GroupIdentifier"), 1, 1, "sequential fixture", null, false,
            Enum.Parse(methodParameters[8].ParameterType, "Wide"), false, false, false, 13, reference.Variables[0].Origin as WorksheetOrigin };
        using var guard = new MatchedSelectionChecks.UnexpectedDialogGuard();
        var result = (DataFrame)equal.Invoke(Activator.CreateInstance(selector, new object[] { grid }), args);
        if (guard.Blocked || (bool)args[10] || (bool)args[11] || proxy.ReadCount != 1)
            throw new InvalidOperationException("A valid sequential fixture unexpectedly warned, cancelled or retried.");
        return result;
    }
    static void Replays(MatchedCells cells, (int column, int first, int count) range, DataFrame frame, string label)
    {
        var oldOrigin = (WorksheetOrigin)frame.Variables[0].Origin;
        var serializer = new XmlSerializer(typeof(WorksheetOrigin));
        using var text = new StringWriter(CultureInfo.InvariantCulture); serializer.Serialize(text, oldOrigin);
        using var reader = new StringReader(text.ToString());
        var origin = (WorksheetOrigin)serializer.Deserialize(reader);
        var selection = cells.Selection(new[] { range });
        var restored = (DataFrame)replay.Invoke(null, new object[] { selection, new List<WorksheetOrigin> { origin }, "sequential replay" });
        Check(origin.Mode == (DataAcquisitionMode)108 && origin.HasTitle == oldOrigin.HasTitle &&
            origin.WorkbookPath == oldOrigin.WorkbookPath && origin.WorksheetName == oldOrigin.WorksheetName,
            label + " persists the aligned mode, header and source in worksheet-origin XML");
        Check(((DoubleVariable)restored.Variables[0]).Data.SequenceEqual(((DoubleVariable)frame.Variables[0]).Data),
            label + " replays the same full observation vector after XML round trip");
    }
    static void Near(double got, double expected, string label) => Check(double.IsFinite(got) &&
        Math.Abs(got - expected) <= 2e-10 * Math.Max(1, Math.Abs(expected)), label + $" ({got:R}; R {expected:R})");
    static void Regression(DataFrame x, DataFrame y, Dictionary<string, double> gold, string label)
    {
        var bag = new ParameterBag(); bag.AddInput("x", x); bag.AddInput("y", y);
        object context = typeof(Regress).GetMethod("GetSimpleLinearRegressionContextWithData", flags).Invoke(null, new object[] { bag });
        context.GetType().GetMethod("CalculateLeastSquaresMethod").Invoke(context, null);
        foreach (var (property, key) in new[] { ("Slope", "slope"), ("YIntercept", "intercept"), ("R", "pearson") })
            Near((double)context.GetType().GetProperty(property).GetValue(context), gold[key], label + " actual regression " + property);
    }
    static void Weighted(DataFrame data, DataFrame weights, Dictionary<string, double> gold, string label, Dictionary<string, double> secondGold = null)
    {
        var bag = new ParameterBag(); bag.AddInput("data", data); bag.AddInput("weights", weights);
        bag.AddInput("gamma", .95); bag.AddInput("report-udca", .1); bag.AddInput("report-udcb", .9); bag.AddInput("report-centile-type", "1");
        foreach (string option in new[] { "valid-data", "missing-data", "sum", "mean", "variance", "sd", "variance-coeff", "sem",
            "u95cl", "l95cl", "geometric-mean", "skewness", "kurtosis", "maximum", "uq", "median", "lq", "iqr", "minimum", "range", "udc" })
            bag.AddInput("report-" + option, option == "mean");
        bag.AddInput("output-to-frame", true);
        var output = Describe.RptWeightedUnivariateSummary(bag).ParameterBag["output"].AsDataFrame;
        Near(((DoubleVariable)output.Variables[1]).Data[0], gold["weightedmean"], label + " actual weighted-summary mean");
        if (secondGold != null)
            Near(((DoubleVariable)output.Variables[1]).Data[1], secondGold["weightedmean"], label + " actual weighted-summary second-column mean");
    }
    static void Case(string operation, string firstName, string secondName, int firstColumn, string name,
        object[] x, object[] y, Dictionary<string, Dictionary<string, double>> gold, int header = 0, int[] hidden = null)
    {
        hidden ??= Array.Empty<int>();
        int secondColumn = 1 - firstColumn;
        object[][] columns = { x, y };
        if (header > 0) columns = columns.Select((c, i) => new[] { header == 3 && i == secondColumn ? null : (object)(i == 0 ? "X data" : "Y data") }.Concat(c).ToArray()).ToArray();
        var cells = new MatchedCells(columns, hidden.Select(i => i + (header > 0 ? 1 : 0)).ToArray());
        int top = header == 2 ? 1 : 0, count = columns[0].Length - top;
        var firstRange = (firstColumn, top, count); var secondRange = (secondColumn, top, count);
        var firstParameter = Parameter(operation, firstName); var secondParameter = Parameter(operation, secondName);
        Check(firstParameter.SelectionMode == (DataAcquisitionMode)108 && secondParameter.SelectionMode == (DataAcquisitionMode)108,
            operation + " explicitly preserves both sequential selections");
        var first = Acquire(cells, firstRange, firstParameter);
        var second = Acquire(cells, secondRange, secondParameter, first);
        var rows = Enumerable.Range(0, x.Length).Except(hidden).ToArray();
        string label = operation + "/" + name + "/header" + header;
        var frames = new DataFrame[2]; frames[firstColumn] = first; frames[secondColumn] = second;
        for (int c = 0; c < 2; c++)
        {
            var wanted = rows.Select(i => MatchedSelectionChecks.Number(c == 0 ? x[i] : y[i])).ToArray();
            var actual = (DoubleVariable)frames[c].Variables[0];
            Check(actual.Length == wanted.Length && actual.Data.SequenceEqual(wanted), label + " preserves every row in variable " + c);
            Replays(cells, c == firstColumn ? firstRange : secondRange, frames[c], label + "/variable" + c);
        }
        if (operation == "SimpleLinearRegression") Regression(frames[0], frames[1], gold[name], label);
        if (operation == "WeightedUnivariateSummary") Weighted(frames[0], frames[1], gold[name], label);
    }
    static void MultipleWeightedReference(Dictionary<string, Dictionary<string, double>> gold)
    {
        object[][] columns = {
            new object[] { "Measure one", null,10.0,20.0,30.0,40.0 },
            new object[] { null,5.0,8.0,null,20.0,25.0 },
            new object[] { "Weights",1.0,2.0,3.0,4.0,null }
        };
        var cells = new MatchedCells(columns);
        var dataParameter = Parameter("WeightedUnivariateSummary", "data");
        var weightsParameter = Parameter("WeightedUnivariateSummary", "weights");
        var selected = cells.Selection(new[] { (0,0,6), (1,0,6) });
        var data = MatchedSelectionChecks.Read(selected, dataParameter.SelectionMode, 0, 12);
        var originalEmptyHeaderOrigin = data.Variables[1].Origin;
        var normalize = selector.GetMethod("NormalizePeerSelectedHeaders", flags);
        normalize.Invoke(null, new object[] { data, cells });
        Check(data.MinRows == 5 && data.MaxRows == 5, "multi-column weighted reference normalizes its selected empty peer header before using MaxRows");
        Check(!ReferenceEquals(originalEmptyHeaderOrigin, data.Variables[1].Origin) &&
            !((WorksheetOrigin)originalEmptyHeaderOrigin).HasTitle && ((WorksheetOrigin)data.Variables[1].Origin).HasTitle,
            "multi-column weighted normalization records its own title decision without mutating the previous origin");
        for (int col = 0; col < 2; col++)
        {
            Check(((DoubleVariable)data.Variables[col]).Data.SequenceEqual(columns[col].Skip(1).Select(MatchedSelectionChecks.Number)),
                "multi-column weighted reference preserves every missing/numeric measurement in column " + col);
            Replays(cells, (col,0,6), new DataFrame(data.Variables[col]), "multi-column weighted reference/column" + col);
        }
        foreach (bool excludeWeightHeader in new[] { false, true })
        {
            var range = (2, excludeWeightHeader ? 1 : 0, excludeWeightHeader ? 5 : 6);
            var weights = Acquire(cells, range, weightsParameter, data);
            Check(((DoubleVariable)weights.Variables[0]).Data.SequenceEqual(columns[2].Skip(1).Select(MatchedSelectionChecks.Number)),
                "multi-column weighted reference keeps weight alignment with header excluded=" + excludeWeightHeader);
            Weighted(data, weights, gold["weighted-edge"], "multi-column weighted report", gold["weighted-second"]);
            Replays(cells, range, weights, "multi-column weights/header-excluded=" + excludeWeightHeader);
        }
    }
    internal static int Run()
    {
        checks = failures = 0;
        try
        {
            var gold = Gold();
            foreach (var (operation, firstName, secondName, firstColumn) in new[] {
                ("SimpleLinearRegression", "y", "x", 1), ("WeightedUnivariateSummary", "data", "weights", 0),
                ("Gini", "data", "weights", 0), ("LOESS", "outcome", "predictors", 1) })
            {
                foreach (int header in new[] { 0, 1, 2, 3 })
                    Case(operation, firstName, secondName, firstColumn, "edge", new object[] { null,1.0,2.0,3.0,4.0 },
                        new object[] { 10.0,40.0,20.0,90.0,null }, gold, header);
                Case(operation, firstName, secondName, firstColumn, "weighted-edge", new object[] { null,10.0,20.0,30.0,40.0 },
                    new object[] { 1.0,2.0,3.0,4.0,null }, gold);
                object[] x = Enumerable.Range(1, 12).Select(i => (object)(double)i).ToArray();
                object[] y = new double[] { 5,3,10,8,9,12,7,19,13,23,17,28 }.Cast<object>().ToArray();
                Case(operation, firstName, secondName, firstColumn, "complete", x, y, gold);
                x = (object[])x.Clone(); y = (object[])y.Clone(); x[0] = x[5] = x[11] = y[3] = null;
                Case(operation, firstName, secondName, firstColumn, "interior", x, y, gold);
                Case(operation, firstName, secondName, firstColumn, "hidden", x, y, gold, hidden: new[] { 1, 8 });
                var blankTailX = x.Concat(new object[5]).ToArray(); var blankTailY = y.Concat(new object[5]).ToArray();
                Case(operation, firstName, secondName, firstColumn, "interior", blankTailX, blankTailY, gold);
            }
            MultipleWeightedReference(gold);
        }
        catch (Exception ex) { Check(false, "unexpected exception: " + ex); }
        Console.WriteLine($"SEQUENTIAL SELECTION: {checks} checks, {failures} failures");
        return failures;
    }
}
