using System.Globalization;
using System.Reflection;
using StatsDirect.Builtins;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.Templates;
using StatsDirect.Utilities;

// Pairwise differences are Cartesian: each observed X minus each observed Y.
// Missing observations are removed independently, not as matched pairs.
internal static class PairwiseDifferencesChecks
{
    static int checks, failures;
    static readonly double missing = Constant.MISSING;
    static void Check(bool ok, string message)
    {
        checks++; if (!ok) failures++;
        Console.WriteLine($"{(ok ? "ok  " : "FAIL")}  Cartesian differences: {message}");
    }
    static ParameterBag Inputs(double[] x, double[] y)
    {
        var input = new ParameterBag();
        input.AddInput("x", new DataFrame(new DoubleVariable(x, "X")));
        input.AddInput("y", new DataFrame(new DoubleVariable(y, "Y")));
        return input;
    }
    static IPreferences Host(int maxRows = 1048576)
    {
        var host = DispatchProxy.Create<IPreferences, PairwisePreferencesHost>();
        ((PairwisePreferencesHost)(object)host).MaxRows = maxRows;
        return host;
    }
    static DoubleVariable Slopes(double[] x, double[] y, int maxRows = 1048576)
    {
        var input = Inputs(x, y); input.AddInput("calculate-slope", false);
        return (DoubleVariable)Sheet.ShtPairSlopes(Host(maxRows), input).ParameterBag["output"].AsDataFrame.Variables.Single();
    }
    static void Case(string label, double[] x, double[] y, double[] expected)
    {
        try
        {
            var output = Sheet.ShtPairDifferences(Host(), Inputs(x, y)).ParameterBag["output"].AsDataFrame;
            var result = (DoubleVariable)output.Variables.Single();
            Check(result.Title == "Differences (X - Y)" && result.Length == expected.Length
                && result.Data.SequenceEqual(expected), label + " returns exactly every X−Y combination: "
                + string.Join(",", result.Data.Select(v => v.ToString("R", CultureInfo.InvariantCulture))));
        }
        catch (Exception error) { Check(false, label + " raised " + error); }
    }
    internal static int Run()
    {
        checks = failures = 0;
        // These vectors are also the base-R result as.vector(t(outer(x,y,"-")))
        // after omitting each vector's NAs independently. Their selected lengths
        // are equal even when their observed sample sizes differ.
        Case("2 observed X by 3 observed Y with interior missing X", new[] { 1.0, missing, 3 }, new[] { 10.0, 20, 30 },
            new[] { -9.0, -19, -29, -7, -17, -27 });
        Case("3 observed X by 2 observed Y with interior missing Y", new[] { 1.0, 2, 3 }, new[] { 10.0, missing, 30 },
            new[] { -9.0, -29, -8, -28, -7, -27 });
        Case("unequal raw input lengths", new[] { 1.0, 3 }, new[] { 10.0, 20, 30 },
            new[] { -9.0, -19, -29, -7, -17, -27 });
        Case("equal complete inputs retain their existing order", new[] { 1.0, 2 }, new[] { 10.0, 30 },
            new[] { -9.0, -29, -8, -28 });
        Case("independent missing positions", new[] { missing, 1.0, 3, missing }, new[] { 10.0, missing, 30, 40 },
            new[] { -9.0, -29, -39, -7, -27, -37 });
        Case("an empty independent sample yields no combinations", new[] { missing, missing, missing }, new[] { 10.0, 20, 30 },
            Array.Empty<double>());
        try
        {
            Sheet.ShtPairDifferences(Host(5), Inputs(new[] { 1.0, 2, 3 }, new[] { 10.0, missing, 30 }));
            Check(false, "the worksheet row limit uses the true 3×2 product");
        }
        catch (TemplateOperationCancelledException) { Check(true, "the worksheet row limit uses the true 3×2 product"); }
        catch (Exception error) { Check(false, "the worksheet row limit raised the wrong error: " + error); }
        try
        {
            // The product exceeds Int32.MaxValue. Reject before narrowing the
            // count or trying to allocate/enumerate billions of differences.
            var many = Enumerable.Repeat(1.0, 50000).ToArray();
            Sheet.ShtPairDifferences(Host(int.MaxValue), Inputs(many, many));
            Check(false, "an overflowing Cartesian product is refused before allocation");
        }
        catch (TemplateOperationCancelledException) { Check(true, "an overflowing Cartesian product is refused before allocation"); }
        catch (Exception error) { Check(false, "an overflowing Cartesian product raised the wrong error: " + error); }

        // Nearby shared-method branches must retain their numerical results.
        var means = new ParameterBag(); means.AddInput("y", new DataFrame(new DoubleVariable(new[] { 1.0, 3, 5 }, "Y")));
        var meanValues = (DoubleVariable)Sheet.ShtPairMeans(Host(), means).ParameterBag["output"].AsDataFrame.Variables.Single();
        Check(meanValues.Data.SequenceEqual(new[] { 1.0, 2, 3, 3, 4, 5 }), "pairwise means are unchanged");
        var slopes = Inputs(new[] { 1.0, 2, 3 }, new[] { 2.0, 4, 7 }); slopes.AddInput("calculate-slope", false);
        var slopeValues = (DoubleVariable)Sheet.ShtPairSlopes(Host(), slopes).ParameterBag["output"].AsDataFrame.Variables.Single();
        Check(slopeValues.Data.SequenceEqual(new[] { 2.0, 2.5, 3 }), "matched pairwise slopes are unchanged");
        var tied = Slopes(new[] { 1.0, 1, 1, 1, 2 }, new[] { 10.0, 20, 30, 40, 50 });
        Check(tied.Length == 4 && tied.Data.SequenceEqual(new[] { 40.0, 30, 20, 10 }), "tied-X slopes contain four actual values and no phantom zero rows");
        var constant = Slopes(new[] { 1.0, 1, 1, 1, 1 }, new[] { 10.0, 20, 30, 40, 50 });
        Check(constant.Length == 0, "no eligible slopes yields an empty output");
        Check(Slopes(new[] { 1.0, 2 }, new[] { 3.0, 7 }).Data.SequenceEqual(new[] { 4.0 }), "two points yield their one eligible slope");
        try
        {
            Slopes(new[] { 1.0, 2, 3 }, new[] { 2.0, 4, 7 }, 2);
            Check(false, "the row limit refuses three actual slopes when only two fit");
        }
        catch (TemplateOperationCancelledException) { Check(true, "the row limit refuses three actual slopes when only two fit"); }
        catch (Exception error) { Check(false, "the slope row limit raised the wrong error: " + error); }
        try
        {
            var manyMeans = new ParameterBag();
            manyMeans.AddInput("y", new DataFrame(new DoubleVariable(Enumerable.Repeat(1.0, 50000).ToArray(), "Y")));
            Sheet.ShtPairMeans(Host(), manyMeans);
            Check(false, "means capacity is checked before its integer product can overflow or allocate output");
        }
        catch (TemplateOperationCancelledException) { Check(true, "means capacity is checked before its integer product can overflow or allocate output"); }
        catch (Exception error) { Check(false, "overflowing means capacity raised the wrong error: " + error); }
        try
        {
            var noSlope = Inputs(Enumerable.Repeat(1.0, 10).ToArray(), Enumerable.Range(1, 10).Select(i => (double)i).ToArray());
            noSlope.AddInput("calculate-slope", true); noSlope.AddInput("gamma", .95);
            var result = Sheet.ShtPairSlopes(Host(), noSlope).ParameterBag["output"].AsDataFrame;
            Check(false, "a median slope cannot be reported with no eligible slopes; returned " + result.Variables.Single().Title);
        }
        catch (TemplateOperationCancelledException error) { Check(error.Message.Contains("different X values", StringComparison.Ordinal), "a median slope cannot be reported with no eligible slopes, with a clear explanation"); }
        catch (Exception error) { Check(false, "no eligible median slope raised the wrong error: " + error); }
        Console.WriteLine($"CARTESIAN DIFFERENCES: {checks} checks, {failures} failures");
        return failures;
    }
}

public class PairwisePreferencesHost : DispatchProxy
{
    internal int MaxRows;
    protected override object Invoke(MethodInfo method, object[] arguments)
    {
        if (method.Name == "RoundU") return ((double)arguments[0]).ToString("R", CultureInfo.InvariantCulture);
        if (method.Name == "get_Preferences")
        {
            var preferences = DispatchProxy.Create<SDPreferences, PairwiseRowLimit>();
            ((PairwiseRowLimit)(object)preferences).MaxRows = MaxRows;
            return preferences;
        }
        throw new InvalidOperationException("Unexpected pairwise preferences call: " + method.Name);
    }
}
public class PairwiseRowLimit : DispatchProxy
{
    internal int MaxRows;
    protected override object Invoke(MethodInfo method, object[] arguments)
        => method.Name == "get_MaxRows" ? MaxRows : throw new InvalidOperationException("Unexpected pairwise preference: " + method.Name);
}
