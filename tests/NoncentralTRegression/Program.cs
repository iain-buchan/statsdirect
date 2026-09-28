using System.Globalization;
using System.Reflection;
using StatsDirect.Builtins;
using StatsDirect.Expressions;
using StatsDirect.Numerics;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
int checks = 0, failures = 0, cases = 0;
double largestCdfError = 0, largestRelativeTailError = 0;
var monotonic = new Dictionary<(int df, double delta), List<(double t, double p)>>();
void Check(bool ok, string message)
{
    checks++;
    if (!ok) { failures++; if (failures <= 30) Console.WriteLine("FAIL " + message); }
}
bool Close(double actual, double expected, double tolerance = 2e-10) =>
    double.IsFinite(actual) && Math.Abs(actual - expected) <= tolerance * Math.Max(1, Math.Abs(expected));
void Probability(double actual, double expected, string context)
{
    largestCdfError = Math.Max(largestCdfError, Math.Abs(actual - expected));
    Check(Close(actual, expected, 2e-11), context + $": {actual:R}; independent {expected:R}");
    if (expected > 1e-280 && expected < 1e-8)
    {
        double relative = Math.Abs(actual / expected - 1);
        largestRelativeTailError = Math.Max(largestRelativeTailError, relative);
        Check(relative < 2e-8, context + $": relative small-tail error {relative:G5}");
    }
}
var power = typeof(Power).GetMethod("nctpower", BindingFlags.NonPublic | BindingFlags.Static, null,
    new[] { typeof(double), typeof(double), typeof(int) }, null);
var interval = typeof(Meta).GetMethod("Ginterval", BindingFlags.NonPublic | BindingFlags.Static);
foreach (string line in File.ReadLines(Path.Combine(AppContext.BaseDirectory, "references.tsv")).Skip(1))
{
    string[] fields = line.Split('\t');
    string kind = fields[0];
    double x = double.Parse(fields[1]), delta = double.Parse(fields[3]), expected = double.Parse(fields[4]), other = double.Parse(fields[5]);
    int df = (int)double.Parse(fields[2]);
    string context = $"{kind}, x={x:R}, df={df}, delta={delta:R}";
    cases++;
    if (kind == "cdf")
    {
        double actual = ExFortran.pnct(x, df, delta, out int fault);
        Check(fault == 0 && actual >= 0 && actual <= 1, context + ": valid probability");
        Probability(actual, expected, context);
        // These exercise the calculator entry points, including upper tails too small to form as 1-CDF.
        Probability(SDMath.Pt(x, df, delta, false, false), other, context + ", calculator upper tail");
        if (other > 1e-280 && other < 1e-8)
            Check(Math.Abs(SDMath.Pt(x, df, delta, false, true) - Math.Log(other)) < 2e-8, context + ", log upper tail");
        double reflected = ExFortran.pnct(-x, df, -delta, out int reflectedFault);
        Check(reflectedFault == 0 && Math.Abs(actual + reflected - 1) < 2e-12, context + ", reflection symmetry");
        var key = (df, delta);
        if (!monotonic.ContainsKey(key)) monotonic[key] = new();
        monotonic[key].Add((x, actual));
    }
    else if (kind == "quantile")
    {
        double actual = ExFortran.tnct(x, df, delta, out int fault);
        Check(fault == 0 && Close(actual, expected), context + $": {actual:R}; independent root {expected:R}");
        Check(Close(SDMath.Qt(x, df, delta, true, false), expected), context + ", calculator lower quantile");
        Check(Close(SDMath.Qt(x, df, -delta, false, false), -expected), context + ", calculator upper quantile");
        if (x < .5)
            Check(Close(SDMath.Qt(Math.Log(x), df, -delta, false, true), -expected), context + ", calculator log upper quantile");
    }
    else if (kind == "power")
    {
        double actual = (double)power.Invoke(null, new object[] { x, delta, df });
        Probability(actual, expected, context);
    }
    else
    {
        object[] intervalArgs = { x, df, 1.0, delta, 0.0, 0.0 };
        interval.Invoke(null, intervalArgs);
        Check(Close((double)intervalArgs[4], expected) && Close((double)intervalArgs[5], other), context + ", effect-size limits");
    }
}
foreach (var group in monotonic)
{
    var values = group.Value.OrderBy(v => v.t).ToArray();
    for (int i = 1; i < values.Length; i++)
        Check(values[i].p + 2e-12 >= values[i-1].p, $"CDF monotonicity: df={group.Key.df}, delta={group.Key.delta}, t={values[i].t}");
}
foreach (int df in new[] { 1, 2, 1000, 1001, int.MaxValue })
{
    Check(ExFortran.pnct(double.NegativeInfinity, df, 40, out int f1) == 0 && f1 == 0, "negative infinite t");
    Check(ExFortran.pnct(double.PositiveInfinity, df, 40, out int f2) == 1 && f2 == 0, "positive infinite t");
}
Console.WriteLine($"{cases} reference cases; {checks} checks; {failures} failures.");
Console.WriteLine($"Largest probability difference {largestCdfError:G4}; largest relative difference in tails between 1e-280 and 1e-8: {largestRelativeTailError:G4}.");
return failures == 0 ? 0 : 1;
