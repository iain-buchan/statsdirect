using System.Globalization;
using System.Reflection;
using System.Text.Json;
using StatsDirect.Builtins;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.Templates;
using StatsDirect.UI;
using StatsDirect.Utilities;

// The real mixed predictor reader and dummy transformation, with a deterministic
// host supplying the same reference-category choice as the selection dialog.
internal static class MixedSelectionChecks
{
    internal const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    static readonly Type processor = typeof(CellSelection).Assembly.GetType("StatsDirect.UI.CellArrayProcessor");
    internal static readonly MethodInfo ReadMixed = processor.GetMethod("ProcessCellArrayNumericCodingPreserveRows", Flags);
    static readonly Type selector = typeof(CellSelection).Assembly.GetType("StatsDirect.UI.GridSelectionProcessor");
    static readonly MethodInfo getEqual = selector.GetMethod("GetCellEqual", Flags);
    static readonly MethodInfo replay = typeof(CellSelection).Assembly.GetType("StatsDirect.UI.frmSpreadsheetGear").GetMethod("ProcessRecordedColumns", Flags);
    static int checks, failures;
    static void Check(bool ok, string message) { checks++; if (!ok) failures++; Console.WriteLine($"{(ok ? "ok  " : "FAIL")}  mixed predictors: {message}"); }
    static FrameParameter Parameter(string operation, string name) => (FrameParameter)typeof(SequentialSelectionChecks)
        .GetMethod("Parameter", Flags).Invoke(null, new object[] { operation, name });
    static Dictionary<string, Dictionary<string, double>> Gold()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "mixed-selection.tsv");
        if (!File.Exists(path)) path = Path.Combine(Environment.CurrentDirectory, "tests", "DataSelection", "benchmarks", "mixed-selection.tsv");
        var result = new Dictionary<string, Dictionary<string, double>>();
        foreach (string line in File.ReadLines(path).Skip(1))
        {
            string[] f = line.Split('\t'); if (!result.ContainsKey(f[0])) result[f[0]] = new();
            result[f[0]][f[1]] = double.Parse(f[2], CultureInfo.InvariantCulture);
        }
        return result;
    }
    static void Near(double got, double expected, string label) => Check(double.IsFinite(got) &&
        Math.Abs(got - expected) <= 2e-10 * Math.Max(1, Math.Abs(expected)), label + $" ({got:R}; R {expected:R})");
    static (ITemplateHost host, MixedCodingHost options) Host(bool continuous = false)
    {
        var host = DispatchProxy.Create<ITemplateHost, MixedCodingHost>();
        var options = (MixedCodingHost)(object)host; options.Continuous = continuous;
        return (host, options);
    }
    static DataFrame Acquire(MatchedCells cells, (int column, int first, int count)[] ranges, DataFrame outcome, ITemplateHost host)
    {
        var parameter = Parameter("MultipleLinearRegression", "predictors");
        IGrid grid = DispatchProxy.Create<IGrid, MixedGridProxy>();
        var proxy = (MixedGridProxy)(object)grid; proxy.Cells = cells; proxy.Selection = cells.Selection(ranges); proxy.Host = host;
        var argsInfo = getEqual.GetParameters();
        object[] args = { outcome.MaxRows, parameter.SelectionMode, Enum.Parse(argsInfo[2].ParameterType, "GroupIdentifier"), 1, ranges.Length,
            "mixed predictor fixture", null, false, Enum.Parse(argsInfo[8].ParameterType, "Wide"), false, false, false, 21, outcome.Variables[0].Origin };
        using var guard = new MatchedSelectionChecks.UnexpectedDialogGuard();
        var result = (DataFrame)getEqual.Invoke(Activator.CreateInstance(selector, new object[] { grid }), args);
        if (guard.Blocked || proxy.ReadCount != 1 || (bool)args[10] || (bool)args[11])
            throw new InvalidOperationException("Valid mixed acquisition warned, retried, cancelled or pivoted.");
        return result;
    }
    static void Fit(DataFrame outcome, DataFrame predictors, Dictionary<string, double> gold, string label)
    {
        var input = new ParameterBag(); input.AddInput("outcome", outcome); input.AddInput("predictors", predictors); input.AddInput("calculateIntercept", true);
        var context = Regress.RptMultipleLinearRegression(input).ParameterBag["context"].AsObject;
        var coefficients = (double[])context.GetType().GetProperty("B").GetValue(context);
        Near((int)context.GetType().GetProperty("N").GetValue(context), gold["n"], label + " actual multiple-regression complete-case count");
        for (int i = 0; i <= predictors.VariableCount; i++)
            Near(coefficients[i + 1], gold["b" + i], label + " actual multiple-regression coefficient " + i);
    }
    static void ReplayNumeric(MatchedCells cells, (int column, int first, int count) range, IVariable variable, string label)
    {
        var origin = variable.Origin as WorksheetOrigin;
        Check(origin != null && origin.Mode == (DataAcquisitionMode)108, label + " records the continuous decision as numeric108");
        var reread = (DataFrame)replay.Invoke(null, new object[] { cells.Selection(new[] { range }), new List<WorksheetOrigin> { origin }, label });
        Check(((DoubleVariable)reread.Variables[0]).Data.SequenceEqual(((DoubleVariable)variable).Data),
            label + " replays numeric values without repeating a categorical choice");
    }
    static void Snapshot(DataFrame frame, bool entered, string label)
    {
        var bag = new ParameterBag(); bag.AddInput("data", frame);
        object snapshot = typeof(RunRecord).GetMethod("Capture", Flags).Invoke(null, new object[] { bag["data"], "frame", false, true });
        var json = (JsonElement)snapshot.GetType().GetField("Content", Flags).GetValue(snapshot);
        Check(json.GetProperty("kind").GetString() == (entered ? "entered" : "frame"), label + " retains the correct recorded-data contract");
        if (!entered) { Check(json.GetProperty("mode").GetString() == "NumericPreserveRows", label + " persists numeric108 for replay"); return; }
        var columns = json.GetProperty("columns").EnumerateArray().ToArray();
        Check(columns.Length == frame.VariableCount, label + " records every expanded/mixed predictor column");
        for (int c = 0; c < columns.Length; c++)
        {
            var actual = ((DoubleVariable)frame.Variables[c]).Data;
            var held = columns[c].GetProperty("values").EnumerateArray().Select(v => v.ValueKind == JsonValueKind.Null ? Constant.MISSING : v.GetDouble()).ToArray();
            Check(held.SequenceEqual(actual), label + " snapshots all original missing/value positions in predictor " + c);
        }
    }
    static void Model(string name, object[][] raw, Dictionary<string, Dictionary<string, double>> gold,
        bool headers, bool excludeHeaders = false, int[] hidden = null, bool blankNumericHeader = false, bool hideHeader = false)
    {
        hidden ??= Array.Empty<int>();
        object[][] columns = headers ? raw.Select((values, i) => new[] {
            blankNumericHeader && i == 0 ? null : (object)(i == raw.Length - 1 ? "Outcome" : "Predictor " + i)
        }.Concat(values).ToArray()).ToArray() : raw;
        var cells = new MatchedCells(columns, hidden.Select(i => i + (headers ? 1 : 0)).Concat(hideHeader ? new[] { 0 } : Array.Empty<int>()).ToArray());
        int first = headers && excludeHeaders ? 1 : 0, count = columns[0].Length - first;
        var outcomeParameter = Parameter("MultipleLinearRegression", "outcome");
        var outcome = MatchedSelectionChecks.Read(cells.Selection(new[] { (columns.Length - 1, first, count) }), outcomeParameter.SelectionMode, 0, 20);
        var choice = Host();
        var ranges = Enumerable.Range(0, columns.Length - 1).Select(c => (c, first, count)).ToArray();
        var predictors = Acquire(cells, ranges, outcome, choice.host);
        var rows = Enumerable.Range(0, raw[0].Length).Except(hidden).ToArray();
        string label = name + "/headers" + headers + "/excluded" + excludeHeaders + "/blank" + blankNumericHeader + "/hidden-header" + hideHeader;
        bool text = raw.Take(raw.Length - 1).Any(c => c.Any(v => v is string s && s is "A" or "B" or "C"));
        Check(predictors.Variables.All(v => v.Length == rows.Length), label + " retains the full common selected row count");
        Check(choice.options.Requests.Count == (text ? 1 : 0), label + " asks only for actual categorical coding");
        int output = 0;
        for (int c = 0; c < raw.Length - 1; c++)
        {
            bool categorical = raw[c].Any(v => v is string s && s is "A" or "B" or "C");
            if (!categorical)
            {
                var actual = (DoubleVariable)predictors.Variables[output++];
                Check(actual.Data.SequenceEqual(rows.Select(r => MatchedSelectionChecks.Number(raw[c][r]))), label + " keeps numeric predictor " + c + " aligned");
                ReplayNumeric(cells, ranges[c], actual, label + "/numeric" + c);
            }
            else
            {
                var levels = rows.Select(r => raw[c][r]).OfType<string>().Where(s => s is "A" or "B" or "C").Distinct().Order().Skip(1).ToArray();
                foreach (string level in levels)
                {
                    var actual = (DoubleVariable)predictors.Variables[output++];
                    var expected = rows.Select(r => raw[c][r] is string s && s is "A" or "B" or "C" ? s == level ? 1.0 : 0.0 : Constant.MISSING);
                    Check(actual.Data.SequenceEqual(expected), label + " preserves missing categorical rows in dummy " + level);
                    Check(actual.Origin == null, label + " leaves transformed dummy values as recorded snapshots");
                }
                Check(!choice.options.Requests[0].AllowUserToTreatAsContinuous, label + " does not offer continuous numbers for textual labels");
            }
        }
        Check(output == predictors.VariableCount, label + " returns exactly the required predictor columns");
        Fit(outcome, predictors, gold[name], label);
        Snapshot(predictors, text, label);
    }
    static void NumericChoices()
    {
        var repeated = new object[] { 1.0,2.0,3.0,null,1.0,2.0,3.0,1.0,2.0,3.0,null };
        var cells = new MatchedCells(new[] { repeated });
        var choice = Host(true);
        var continuous = (DataFrame)ReadMixed.Invoke(null, new object[] { cells.Selection(new[] { (0,0,repeated.Length) }), repeated.Length, false, false, 1, choice.host, null });
        Check(choice.options.Requests.Count == 1 && choice.options.Requests[0].AllowUserToTreatAsContinuous, "repeated numeric categories retain the user's continuous option");
        Check(((DoubleVariable)continuous.Variables[0]).Data.SequenceEqual(repeated.Select(MatchedSelectionChecks.Number)), "continuous numeric choice preserves every original missing position");
        ReplayNumeric(cells, (0,0,repeated.Length), continuous.Variables[0], "explicit continuous choice");
        Snapshot(continuous, false, "explicit continuous choice");
        choice = Host();
        var coded = (DataFrame)ReadMixed.Invoke(null, new object[] { cells.Selection(new[] { (0,0,repeated.Length) }), repeated.Length, false, false, 1, choice.host, null });
        Check(coded.VariableCount == 2 && coded.Variables.All(v => ((DoubleVariable)v).Data[3] == Constant.MISSING && ((DoubleVariable)v).Data[10] == Constant.MISSING),
            "numeric category dummy choice propagates missing rows to every dummy");
        Snapshot(coded, true, "explicit numeric dummy choice");
        foreach (object[] values in new[] {
            new object[] { null,1.0,2.0,3.0,4.0,null,null,null },
            new object[] { null,0.0,1.0,0.0,1.0,null },
            new object[] { null,1.25,2.5,3.75,null },
            new object[] { null,null,null } })
        {
            var numericCells = new MatchedCells(new[] { values });
            using var guard = new MatchedSelectionChecks.UnexpectedDialogGuard();
            var frame = MatchedSelectionChecks.Read(numericCells.Selection(new[] { (0,0,values.Length) }), (DataAcquisitionMode)109, values.Length, 1);
            Check(!guard.Blocked && frame.VariableCount == 1 && ((DoubleVariable)frame.Variables[0]).Data.SequenceEqual(values.Select(MatchedSelectionChecks.Number)),
                "distinct, binary, continuous and wholly missing typed columns do not acquire artificial categories");
        }
        var legacy = new MatchedCells(new[] { new object[] { "Header","A","B",null,"B","A" } });
        var categories = (ClassifierVariable)MatchedSelectionChecks.Read(legacy.Selection(new[] { (0,0,6) }), DataAcquisitionMode.NumericCodingTextToCategories, 5, 1).Variables[0];
        Check(categories.Data[2] == Constant.MISSING, "legacy shared classifier keeps a missing position rather than category zero");
        var dummy = Sheet.ToDummyVariables(Host().host, categories, true);
        Check(((DoubleVariable)dummy.Variables[0]).Data[2] == Constant.MISSING, "shared dummy transformation propagates a marked missing category");
    }
    static void UnprovenHeader()
    {
        var columns = new[] { new object[] { null,null,"A","B","A","B","C" } };
        var cells = new MatchedCells(columns);
        foreach (var reference in new WorksheetOrigin[] { null,
            new("matched-fixture.xlsx", "OtherSheet", 1, 0, 7, (DataAcquisitionMode)108, true, false, 1),
            new("matched-fixture.xlsx", "Sheet1", 1, 2, 7, (DataAcquisitionMode)108, true, false, 1) })
        {
            var choice = Host();
            var frame = (DataFrame)ReadMixed.Invoke(null, new object[] { cells.Selection(new[] { (0,0,7) }), 6, false, false, 1, choice.host, reference });
            Check(frame.Variables.All(v => v.Length == 7 && ((DoubleVariable)v).Data[0] == Constant.MISSING && ((DoubleVariable)v).Data[1] == Constant.MISSING),
                "an extra leading missing row is not treated as a categorical header without matching source/row evidence");
        }
    }
    internal static int Run()
    {
        checks = failures = 0;
        try
        {
            foreach (string operation in new[] { "MultipleLinearRegression", "LogisticRegression", "PoissonRegression", "CoxRegression", "ConditionalLogisticRegression" })
                Check(Parameter(operation, "predictors").SelectionMode == (DataAcquisitionMode)109, operation + " opts into aligned mixed predictors without changing the legacy XML mode");
            Check(Parameter("CoxRegression", "strata").SelectionMode == DataAcquisitionMode.CategoryCombineAllColumns &&
                Parameter("ConditionalLogisticRegression", "stratum").SelectionMode == DataAcquisitionMode.CategoryCombineAllColumns,
                "single-column Cox and conditional strata preserve blank positions");
            var gold = Gold();
            var numeric = new[] { new object[] { null,1.0,2.0,3.0,4.0 }, new object[] { 10.0,40.0,20.0,90.0,null } };
            foreach (bool headers in new[] { false, true })
                Model("numeric-edge", numeric, gold, headers);
            Model("numeric-edge", numeric, gold, true, true);
            Model("numeric-edge", numeric, gold, true, blankNumericHeader: true);
            Model("numeric-edge", numeric, gold, true, hideHeader: true);
            var binary = new[] { new object[] { "A","B",null,"B","A" }, new object[] { 1.0,2.0,20.0,2.0,1.0 } };
            Model("categorical-binary", binary, gold, true);
            Model("categorical-binary", binary, gold, true, true);
            Model("categorical-binary", binary, gold, true, blankNumericHeader: true);
            var firstMissing = new[] { new object[] { null,"A","B","A","B","C" }, new object[] { 999.0,3.0,8.0,4.0,6.0,11.0 } };
            Model("categorical-firstna", firstMissing, gold, true, blankNumericHeader: true);
            Model("categorical-firstna", firstMissing, gold, false);
            Model("categorical-firstna", firstMissing, gold, true, blankNumericHeader: true, hideHeader: true);
            var categorical = new object[] { "A","B","C","A",null,"B","C","A","B" };
            var y = new object[] { 3.0,7.0,12.0,5.0,999.0,8.0,14.0,4.0,6.0 };
            Model("categorical-three", new[] { categorical,y }, gold, true);
            foreach (object missing in new object[] { "*",".","NA","N/A","NaN","NULL","MISSING","* (missing)" })
            {
                var marker = (object[])categorical.Clone(); marker[4] = missing;
                Model("categorical-three", new[] { marker,y }, gold, true);
            }
            var z = new object[] { 1.1,2.5,1.7,3.3,4.1,6.2,5.3,7.4,8.6 };
            Model("mixed-design", new[] { z,categorical,y }, gold, true);
            Model("mixed-design", new[] { z,categorical,y }, gold, true, blankNumericHeader: true);
            Model("mixed-hidden", new[] { z,categorical,y }, gold, true, hidden: new[] { 1 });
            NumericChoices();
            UnprovenHeader();
        }
        catch (Exception ex) { Check(false, "unexpected exception: " + ex); }
        Console.WriteLine($"MIXED SELECTION: {checks} checks, {failures} failures");
        return failures;
    }
}

public class MixedGridProxy : MatchedGridProxy
{
    internal ITemplateHost Host;
    protected override object Invoke(MethodInfo method, object[] arguments)
    {
        if (method.Name == "GetCellArray" && (int)(DataAcquisitionMode)arguments[1] == 109)
        {
            ReadCount++; arguments[8] = arguments[9] = false;
            return MixedSelectionChecks.ReadMixed.Invoke(null, new object[] { Selection, (int)arguments[0], false, false, (int)arguments[10], Host, arguments[11] });
        }
        return base.Invoke(method, arguments);
    }
}

public class MixedCodingHost : DispatchProxy
{
    internal bool Continuous;
    internal readonly List<DummyOptions> Requests = new();
    protected override object Invoke(MethodInfo method, object[] arguments)
    {
        if (method.Name == "Amend")
        {
            var options = (DummyOptions)arguments[0]; Requests.Add(options);
            options.JDrop = 0; options.TreatAsContinuous = Continuous;
            return new ParameterBag();
        }
        throw new InvalidOperationException("Unexpected mixed predictor host call: " + method.Name);
    }
}
