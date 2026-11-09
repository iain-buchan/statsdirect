using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using StatsDirect.Builtins;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.R;
using StatsDirect.TemplateProcessing;
using StatsDirect.Templates;
using StatsDirect.UI;
using StatsDirect.Utilities;

internal static partial class Program
{
    // The Continue in R recipes of the 5.3.9 review: scripts that stopped where the report completed, replayed another
    // analysis, or did not take the report's confidence fallback, and the native Royston K2 and Cox hazard-ratio limits.
    // Each case acquires its columns from worksheet cells through GridSelectionProcessor.FillFrameParameter (and the
    // CellArrayProcessor under it), runs the operation through the template processor, writes the script from the record
    // the report step left, runs it in R within a time limit, and compares R's figures with the native output bag.
    private static readonly string[] Recipes539Parts = { "gof" };
    private static void CheckRecipes539(string folder, string only = null)
    {
        if (only != null && !Recipes539Parts.Contains(only)) throw new ArgumentException("Unknown 5.3.9 recipe check: " + only);
        RRecipes.Folder = Metadata("Recipes");
        Directory.CreateDirectory(folder);
        if (only is null or "gof") CheckGoodnessOfFit539(Path.Combine(folder, "gof"));
        if (Failed539.Count > 0) throw new Exception("FAIL " + Failed539.Count + " of the 5.3.9 recipe cases: " + string.Join(" | ", Failed539));
    }

    // Normally the first failure stops the run, as in every other check. With STATSDIRECT_RECIPES539_CONTINUE=1, used to
    // show what fails before a fix, a failing case is noted with R's last words and the other cases still run.
    private static readonly List<string> Failed539 = new();
    private static void Case539(string label, Action body)
    {
        if (Environment.GetEnvironmentVariable("STATSDIRECT_RECIPES539_CONTINUE") != "1") { body(); return; }
        try { body(); }
        catch (Exception ex)
        {
            string[] lines = ex.Message.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries);
            string said = lines[0] + (lines.Length > 1 ? " ... " + string.Join(" / ", lines.Skip(Math.Max(1, lines.Length - 2))) : "");
            Failed539.Add(label + ": " + said);
            Console.WriteLine("FAILED CASE " + label + ": " + said);
        }
    }

    // A column of worksheet cells: its title in the first row, then its values.
    private static object[] Cells539(string title, IEnumerable<double> values) => new object[] { title }.Concat(values.Select(v => (object)v)).ToArray();

    // The answer to a frame prompt: the columns acquired from the cells as the grid acquires a selection,
    // through GridSelectionProcessor.FillFrameParameter with the frame's own mode, lengths and alignment.
    private static Func<ITemplateProcessor, Parameter, ParameterBag, ITemplateHost, ParameterBag> Select539(RecordCells cells, int rows, params int[] columns) =>
        (processor, parameter, context, host) =>
        {
            IGrid grid = DispatchProxy.Create<IGrid, AlignedGrid>();
            var proxy = (AlignedGrid)(object)grid; proxy.Cells = cells; proxy.Host = host;
            proxy.Selection = new CellSelection { LongestRowCount = rows };
            foreach (int c in columns)
                proxy.Selection.ColumnSelections.Add(new CellColumnSelection(cells) { ColumnIndex = c, RowIndex = 0, RowCount = rows, WorkbookPath = Workbook, WorksheetName = "Recipes 5.3.9" });
            Type type = typeof(CellSelection).Assembly.GetType("StatsDirect.UI.GridSelectionProcessor");
            try { return (ParameterBag)type.GetMethod("FillFrameParameter", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(Activator.CreateInstance(type, grid), new object[] { parameter, processor, context }); }
            catch (TargetInvocationException ex) { throw new InvalidOperationException(ex.GetBaseException().ToString(), ex); }
        };

    private static double Num539(ParameterBag bag, string key)
    {
        object value = bag[key].AsObject;
        if (value is string text) return double.Parse(Regex.Match(text, @"[-+]?(?:\d+\.?\d*|\.\d+)(?:[Ee][-+]?\d+)?").Value, inv);
        return Convert.ToDouble(value, inv);
    }
    private static double Found539(ParameterBag bag, string key) => Num539(DeferredBags538(bag).First(b => b.ContainsKey(key) && b[key]?.AsObject != null), key);
    private static IList<ParameterBag> Rows539(ParameterBag bag, string key) => (IList<ParameterBag>)bag[key].AsObject;
    private static string Vector539(IEnumerable<double> values) => RScriptWriter.Vector(values.Select(v => v == Constant.MISSING || double.IsNaN(v) ? null : (object)v).ToArray());
    private static string Equal539(string expression, IEnumerable<double> expected, double tolerance = 1e-9) =>
        "stopifnot(isTRUE(all.equal(unname(c(" + expression + ")), " + Vector539(expected) + ", tolerance = " + tolerance.ToString("R", inv) + ", check.attributes = FALSE)))\n";
    private static double[] Printed539(string text, string pattern) =>
        Regex.Match(text, pattern, RegexOptions.Multiline) is { Success: true } m ? m.Groups.Cast<System.Text.RegularExpressions.Group>().Skip(1).Select(g => double.Parse(g.Value, inv)).ToArray() : Array.Empty<double>();
    private const string Figure539 = @"(-?[0-9]+(?:\.[0-9]+)?(?:[eE][-+]?[0-9]+)?)";

    // The script of the run's record, run in R within the time limit with the assertions after it.
    private static string Replay539(OperationRun run, string assertion, string folder, Func<FramePointer, FrameValues> read = null, string setup = "", int timeout = 120)
    {
        RScript script = RScriptWriter.Write(Record(run), null, read);
        string label = Path.GetFileName(folder);
        Check(script.HasRecipe && !script.DataUnavailable && !script.Text.Contains("has changed in the workbook"), label + ": the record keeps or rereads the selected data and has its recipe");
        string output = DeferredR538(setup + script.Text + "\n" + assertion + "\ncat('RECIPES_539_OK\\n')\n", folder, timeout);
        Check(output.Contains("RECIPES_539_OK"), label + ": R reproduces the native report");
        return output;
    }

    // The run's record as saved before 5.2.3, which did not name the root analysis of a follow-on.
    private static JsonElement Legacy539(OperationRun run)
    {
        JsonObject record = JsonNode.Parse(run.Record).AsObject();
        record.Remove("rootOperation");
        return JsonSerializer.SerializeToElement(record);
    }

    // An ordering of 1..n with the given number of inversions (greedy Lehmer code).
    private static int[] Permutation539(int n, long inversions)
    {
        List<int> available = Enumerable.Range(1, n).ToList(); int[] values = new int[n];
        for (int j = 0; j < n; j++) { int k = (int)Math.Min(inversions, n - 1 - j); values[j] = available[k]; available.RemoveAt(k); inversions -= k; }
        return values;
    }

    // 1. Expected counts (or percentages, or proportions that do not add up to 1) are scaled to the observed total by the
    //    report; chisq.test needs rescale.p = TRUE to do the same.
    private static void CheckGoodnessOfFit539(string folder)
    {
        void Case(string label, double[] observed, double[] expected, bool simulate = false) => Case539("gof " + label, () =>
        {
            string[] names = observed.Select((_, i) => "Group " + (char)('A' + i)).ToArray();
            var cells = new RecordCells(Cells539("Observed", observed), Cells539("Expected", expected), new object[] { "Category" }.Concat(names).ToArray());
            int rows = observed.Length + 1;
            var (run, output) = Run("ChiSquareGoodnessOfFit", new() { ["observed"] = Select539(cells, rows, 0), ["expected"] = Select539(cells, rows, 1), ["names"] = Select539(cells, rows, 2) });
            ParameterBag bag = output.ParameterBag;
            string assertion = Equal539("sum(observed), test$parameter", new[] { Num539(bag, "n"), Num539(bag, "df") }) +
                Equal539("test$expected", Rows539(bag, "*frequencies").Select(r => Num539(r, "e"))) +
                (Num539(bag, "chi2") == 0 ? "stopifnot(abs(test$statistic) < 1e-12, abs(test$p.value - 1) < 1e-12)\n" : Equal539("test$statistic, test$p.value", new[] { Num539(bag, "chi2"), Num539(bag, "p") })) +
                "stopifnot(identical(names(observed), " + RScriptWriter.Vector(names) + "))\n";
            Replay539(run, assertion, Path.Combine(folder, label));
            if (!simulate) return;
            var (follow, _) = Run("ChiGFSimulateExactP", new() { ["iterations"] = 2000, ["seed"] = 11, ["ci"] = .99 }, bag);
            Replay539(follow, assertion, Path.Combine(folder, label + "-simulated"));
        });
        Case("counts-equal-total", new double[] { 10, 20, 30, 25, 15 }, new double[] { 10, 20, 30, 25, 15 }, simulate: true);
        Case("counts-other-total", new double[] { 12, 18, 33, 22, 15 }, new double[] { 20, 40, 60, 50, 30 });
        Case("percentages", new double[] { 67, 83, 29, 8 }, new double[] { 44, 45, 8, 3 });
        Case("proportions-below-one", new double[] { 9, 21, 28, 30, 12 }, new double[] { .1, .2, .3, .25, .1 });
        Case("proportions-help", new double[] { 67, 83, 29, 8 }, new double[] { .44, .45, .08, .03 });
    }

}
