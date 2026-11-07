using System.Reflection;
using System.Xml.Serialization;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.TemplateProcessing;
using StatsDirect.Templates;
using StatsDirect.Utilities;

// Run the transformation script shipped in ApplyFunction.xml. The only stand-in
// is the worksheet: its selected cells still pass through actual GetCellEqual.
// Expected vectors below are explicit arithmetic results, including their rows.
internal static class ApplyFunctionSelectionChecks
{
    static int checks, failures;
    static readonly double missing = Constant.MISSING;
    static void Check(bool ok, string message)
    {
        checks++; if (!ok) failures++;
        Console.WriteLine($"{(ok ? "ok  " : "FAIL")}  apply function: {message}");
    }
    static Operation Load()
    {
        foreach (string start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
            for (var directory = new DirectoryInfo(start); directory != null; directory = directory.Parent)
            {
                string file = Path.Combine(directory.FullName, "StatsDirectUI", "Assets", "Operations", "ApplyFunction.xml");
                if (!File.Exists(file)) continue;
                using var reader = File.OpenText(file);
                return (Operation)new XmlSerializer(typeof(Operation)).Deserialize(reader);
            }
        throw new FileNotFoundException("Cannot locate the actual ApplyFunction operation definition.");
    }
    static void Case(Operation operation, string label, object[][] columns, double[] expected,
        string expression = "X1 - X2", bool headers = false, bool excludeHeaders = false, int[] hidden = null)
    {
        var definition = operation.Steps.OfType<ParametersStep>().SelectMany(s => s.Parameters).OfType<FrameParameter>().Single();
        var cells = new MatchedCells(columns, hidden ?? Array.Empty<int>());
        int first = headers && excludeHeaders ? 1 : 0;
        int count = columns[0].Length - first;
        DataFrame frame = MatchedSelectionChecks.Acquire(cells, new[] { (0, first, count), (1, first, count) }, definition.SelectionMode);
        Check(frame.VariableCount == 2 && frame.MinRows == expected.Length && frame.MaxRows == expected.Length,
            label + " acquires the intended number of matched rows without padding or retries");
        foreach (IVariable variable in frame.Variables)
        {
            var origin = (WorksheetOrigin)variable.Origin;
            Check(origin.Mode == (DataAcquisitionMode)108 && origin.HasTitle == (headers && !excludeHeaders),
                label + " records the actual header and row-preserving mode for " + variable.Title);
        }

        var input = new ParameterBag(); input.AddInput("data", frame);
        input.AddInput("expression", expression); input.AddInput("new_column_name", "Matched result");
        ScriptStep calculate = operation.Steps.OfType<ScriptStep>().Last();
        IScriptEngine engine = new ScriptEngine();
        var output = (ParameterBag)engine.Run(calculate.Language, calculate.Body, ScriptType.Function,
            DispatchProxy.Create<ITemplateHost, MatchedReportHost>(), input, null, null);
        DataFrame result = output["output"].AsDataFrame;
        var values = (DoubleVariable)result.Variables.Single();
        Check(values.Title == "Matched result" && values.Length == expected.Length
            && values.Data.Take(values.Length).SequenceEqual(expected),
            label + " executes the real VB transformation on the correct row pairs");
    }
    static object[][] Values() => new[]
    {
        new object[] { 1.0, 2.0, 3.0, 4.0, 5.0, 6.0 },
        new object[] { 10.0, 7.0, 20.0, 9.0, 40.0, 11.0 }
    };
    static object[][] Header(object[][] data, bool blankSecond = false) => new[]
    {
        new object[] { "First" }.Concat(data[0]).ToArray(),
        new object[] { blankSecond ? null : "Second" }.Concat(data[1]).ToArray()
    };
    internal static int Run()
    {
        checks = failures = 0;
        try
        {
            Operation operation = Load();
            var parameter = operation.Steps.OfType<ParametersStep>().SelectMany(s => s.Parameters).OfType<FrameParameter>().Single();
            Check(parameter.PreserveRowAlignment && parameter.DataAcquisitionMode == DataAcquisitionMode.NumericReplaceMissing
                && parameter.SelectionMode == (DataAcquisitionMode)108, "the actual operation explicitly opts into matched rows");
            double[] complete = { -9, -5, -17, -5, -35, -5 };
            Case(operation, "complete", Values(), complete);
            Case(operation, "selected headers", Header(Values()), complete, headers: true);
            Case(operation, "excluded headers", Header(Values()), complete, headers: true, excludeHeaders: true);
            Case(operation, "empty selected second header", Header(Values(), true), complete, headers: true);
            var leading = Values(); leading[0][0] = null;
            Case(operation, "leading missing first value", leading, new[] { missing, -5, -17, -5, -35, -5 });
            Case(operation, "header and leading missing first value", Header(leading), new[] { missing, -5, -17, -5, -35, -5 }, headers: true);
            var second = Values(); second[1][0] = null;
            Case(operation, "leading missing second value", Header(second, true), new[] { missing, -5, -17, -5, -35, -5 }, headers: true);
            var gaps = Values(); gaps[0][0] = null; gaps[1][2] = "NA"; gaps[0][5] = "*";
            Case(operation, "missing values in different rows and columns", Header(gaps), new[] { missing, -5, missing, -5, -35, missing }, headers: true);
            Case(operation, "hidden first and interior rows", Values(), new[] { -5.0, -17, -5, -5 }, hidden: new[] { 0, 4 });
            Case(operation, "selected header and hidden observations", Header(Values(), true), new[] { -5.0, -17, -5, -5 }, headers: true, hidden: new[] { 1, 5 });
            var tails = Values().Select(c => c.Concat(new object[3]).ToArray()).ToArray();
            Case(operation, "common empty selected tail", tails, new[] { -9.0, -5, -17, -5, -35, -5, missing, missing, missing });
            Case(operation, "second arithmetic expression", Values(), new[] { 21.0, 16, 43, 22, 85, 28 }, expression: "X1 + 2 * X2");
        }
        catch (Exception error)
        {
            while (error is TargetInvocationException && error.InnerException != null) error = error.InnerException;
            Check(false, "unexpected exception: " + error);
        }
        Console.WriteLine($"APPLY FUNCTION SELECTION: {checks} checks, {failures} failures");
        return failures;
    }
}
