using System.Globalization;
using System.Reflection;
using StatsDirect.Builtins;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.Templates;

// Exercise the actual report's preprocessing and fit together: an incomplete
// denominator must not poison a complete row with the same covariate pattern.
internal static class LogisticStackingChecks
{
    static int checks, failures;
    static readonly double missing = Constant.MISSING;
    static readonly double[] baseX = { 0, 1, 2, 3, 4 };
    static readonly double[] baseResponse = { 5, 8, 11, 14, 16 };
    static readonly double[] baseTotal = { 20, 20, 20, 20, 20 };
    static readonly double[] prior = { 1, 2, 1, 3, 1 };
    static void Check(bool ok, string message)
    {
        checks++; if (!ok) failures++;
        Console.WriteLine($"{(ok ? "ok  " : "FAIL")}  logistic stacking: {message}");
    }
    static Dictionary<string, Dictionary<string, double>> Gold()
    {
        string file = Path.Combine(AppContext.BaseDirectory, "logistic-stacking.tsv");
        if (!File.Exists(file))
            foreach (string start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
            {
                for (var directory = new DirectoryInfo(start); directory != null; directory = directory.Parent)
                {
                    string candidate = Path.Combine(directory.FullName, "tests", "DataSelection", "benchmarks", "logistic-stacking.tsv");
                    if (File.Exists(candidate)) { file = candidate; break; }
                }
                if (File.Exists(file)) break;
            }
        var result = new Dictionary<string, Dictionary<string, double>>();
        foreach (string line in File.ReadLines(file).Skip(1))
        {
            string[] cells = line.Split('\t');
            if (!result.ContainsKey(cells[0])) result[cells[0]] = new();
            result[cells[0]][cells[1]] = double.Parse(cells[2], CultureInfo.InvariantCulture);
        }
        return result;
    }
    static ParameterBag Fit(double[] x, double[] response, double[] total, double[] weights = null, bool individual = false)
    {
        var input = new ParameterBag();
        input.AddInput("accuracy", "0.0000000001"); input.AddInput("gamma", .95);
        input.AddInput("grouping", individual ? "individual" : "grouped");
        input.AddInput("intercept", true); input.AddInput("weights", weights != null);
        input.AddInput("predictors", new DataFrame(new DoubleVariable(x, "X")));
        input.AddInput("response", new DataFrame(new DoubleVariable(response, "Response")));
        if (!individual) input.AddInput("total", new DataFrame(new DoubleVariable(total, "Total")));
        if (weights != null) input.AddInput("weight", new DataFrame(new DoubleVariable(weights, "Prior weight")));
        return Regress.RptLogisticRegression(DispatchProxy.Create<IFormatting, MatchedReportHost>(), input).ParameterBag;
    }
    static void Case(string label, string goldName, Dictionary<string, Dictionary<string, double>> gold,
        double[] x, double[] response, double[] total, double[] weights = null, int dropped = 0, bool individual = false)
    {
        try
        {
            ParameterBag output = Fit(x, response, total, weights, individual);
            object context = output["context"].AsObject;
            T Property<T>(string name) => (T)context.GetType().GetProperty(name).GetValue(context);
            double[] beta = Property<double[]>("B"), se = Property<double[]>("Se");
            var actual = new Dictionary<string, double> { ["intercept"] = beta[1], ["slope"] = beta[2],
                ["se_intercept"] = se[1], ["se_slope"] = se[2], ["dev"] = output["dev"].AsDouble,
                ["df"] = output["df"].AsDouble, ["patterns"] = Property<int>("N") };
            foreach (var expected in gold[goldName])
            {
                double value = actual[expected.Key];
                Check(double.IsFinite(value) && Math.Abs(value - expected.Value) <= 2e-8 * Math.Max(1e-4, Math.Abs(expected.Value)),
                    $"{label}: {expected.Key} agrees with complete-case R glm ({value:R}; R {expected.Value:R})");
            }
            var warning = output["*warn"].AsParameterBagList.Select(p => p["warn"].AsString).ToArray();
            Check(dropped == 0 ? warning.All(w => !w.Contains("observations dropped due to missing data", StringComparison.Ordinal))
                : warning.Any(w => w.StartsWith(dropped + " observations dropped due to missing data", StringComparison.Ordinal)),
                label + ": the missing-data warning counts incomplete input rows rather than discarding valid duplicates");
        }
        catch (Exception error) { Check(false, label + " raised " + error); }
    }
    static double[] Insert(double[] values, double value, bool before) => before ? new[] { value }.Concat(values).ToArray() : values.Concat(new[] { value }).ToArray();
    internal static int Run()
    {
        checks = failures = 0;
        try
        {
            var gold = Gold();
            Case("complete-case control", "base", gold, baseX, baseResponse, baseTotal);
            foreach (bool before in new[] { true, false })
            {
                string position = before ? "before" : "after";
                Case("missing total " + position + " equal-pattern valid row", "base", gold,
                    Insert(baseX, 0, before), Insert(baseResponse, 3, before), Insert(baseTotal, missing, before), dropped: 1);
                Case("missing total " + position + " weighted equal-pattern valid row", "weighted", gold,
                    Insert(baseX, 0, before), Insert(baseResponse, 3, before), Insert(baseTotal, missing, before), Insert(prior, 1, before), dropped: 1);
                Case("missing response " + position + " valid row", "base", gold,
                    Insert(baseX, 0, before), Insert(baseResponse, missing, before), Insert(baseTotal, 20, before), dropped: 1);
                Case("missing weight " + position + " valid row", "weighted", gold,
                    Insert(baseX, 0, before), Insert(baseResponse, 3, before), Insert(baseTotal, 20, before), Insert(prior, missing, before), dropped: 1);
            }
            Case("unstacked incomplete-row control", "base", gold,
                Insert(baseX, -1, true), Insert(baseResponse, 3, true), Insert(baseTotal, missing, true), dropped: 1);
            double[] duplicateX = baseX.Concat(new[] { 0.0 }).ToArray();
            double[] duplicateResponse = baseResponse.Concat(new[] { 2.0 }).ToArray();
            double[] duplicateTotal = baseTotal.Concat(new[] { 10.0 }).ToArray();
            Case("valid equal-pattern rows still stack", "valid_duplicate", gold, duplicateX, duplicateResponse, duplicateTotal);
            Case("incomplete row cannot poison two legitimate stacked rows", "valid_duplicate", gold,
                Insert(duplicateX, 0, true), Insert(duplicateResponse, 3, true), Insert(duplicateTotal, missing, true), dropped: 1);
            Case("two incomplete rows cannot consume the valid pattern", "base", gold,
                Insert(Insert(baseX, 0, true), 0, false), Insert(Insert(baseResponse, 3, true), 3, false),
                Insert(Insert(baseTotal, missing, true), missing, false), dropped: 2);
            Case("individual binary responses retain their existing stacking", "individual", gold,
                new[] { 0.0,0,0,1,1,1,2,2,2,2 }, new[] { 0.0,0,1,1,0,1,1,1,0,1 }, null, individual: true);
        }
        catch (Exception error) { Check(false, "unexpected exception: " + error); }
        Console.WriteLine($"LOGISTIC STACKING: {checks} checks, {failures} failures");
        return failures;
    }
}
