// What the checks of the Agreement menu share: the counting of checks, and distribution functions worked out here from series and continued
// fractions, so that nothing of the program's own arithmetic is relied upon.
using System.Numerics;
using System.Reflection;
using StatsDirect.Data;
using StatsDirect.Templates;

internal sealed class NoProgress : IProgressBarHost, IProgressBar
{
    public IProgressBar StartProgress(string operationDescription, bool provideProgress, bool display = true) => this;
    public void Finish() { }
    public bool Update(double fractionComplete) => false;
    public void Dispose() { }
}

// the preferences that the reports ask for: six decimal places for a probability, and the default of everything else
public class PreferencesProxy : DispatchProxy
{
    protected override object Invoke(MethodInfo method, object[] arguments)
    {
        if (method.Name == "get_PDecimalPlaces" || method.Name == "get_DisplayDecimalPlaces") return 6;
        Type type = method.ReturnType;
        return type == typeof(void) ? null : type.IsValueType ? Activator.CreateInstance(type) : null;
    }
}

internal sealed class Plain : IPreferences
{
    public string RoundU(double amount) => amount.ToString("G10");
    public string pval(double p) => p.ToString("G6");
    public string pval_half(double p) => p.ToString("G6");
    public SDPreferences Preferences { get; } = DispatchProxy.Create<SDPreferences, PreferencesProxy>();
}

internal static partial class Program
{
    internal const double M = double.MinValue;
    internal static int failures, checks;

    internal static void Check(string what, double worst, double tolerance, bool print = false)
    {
        checks++;
        bool ok = worst <= tolerance;
        if (!ok) failures++;
        if (!ok || print) Console.WriteLine($"{(ok ? "ok  " : "FAIL")}  {what}: worst difference {worst:E2} (tolerance {tolerance:E0})");
    }

    internal static void Say(bool ok, string what, bool print = false)
    {
        checks++;
        if (!ok) failures++;
        if (!ok || print) Console.WriteLine($"{(ok ? "ok  " : "FAIL")}  {what}");
    }

    internal static string Message(Exception ex)
    {
        Exception inner = ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex;
        string where = inner is NullReferenceException or IndexOutOfRangeException ? " " + (inner.StackTrace ?? "").Split('\n').FirstOrDefault()?.Trim() : "";
        return inner.GetType().Name + ": " + inner.Message + where;
    }

    internal static List<ParameterBag> Rows(ParameterBag bag, string block) =>
        bag[block].AsObject == null ? new List<ParameterBag>() : ((System.Collections.IEnumerable)bag[block].AsObject).Cast<ParameterBag>().ToList();

    internal static double Relative(double a, double b) => double.IsNaN(a) || a == M ? double.PositiveInfinity : Math.Abs(a - b) / Math.Max(1e-300, Math.Max(Math.Abs(b), 1));

    // ---------------------------------------------------------------- distributions
    internal static double LogGamma(double x)
    {
        // Lanczos approximation (g = 7, 9 terms), with the reflection formula below 0.5
        if (x < 0.5) return Math.Log(Math.PI / Math.Abs(Math.Sin(Math.PI * x))) - LogGamma(1 - x);
        double[] c = { 0.99999999999980993, 676.5203681218851, -1259.1392167224028, 771.32342877765313, -176.61502916214059, 12.507343278686905, -0.13857109526572012, 9.9843695780195716e-6, 1.5056327351493116e-7 };
        x -= 1;
        double a = c[0], t = x + 7.5;
        for (int i = 1; i < 9; i++) a += c[i] / (x + i);
        return 0.5 * Math.Log(2 * Math.PI) + (x + 0.5) * Math.Log(t) - t + Math.Log(a);
    }

    // the regularised incomplete gamma function P(a, x): the series below a + 1 and the continued fraction above
    internal static double GammaLower(double a, double x)
    {
        if (x <= 0) return 0;
        if (x < a + 1)
        {
            double sum = 1 / a, term = sum;
            for (int n = 1; n < 20000000; n++) { term *= x / (a + n); sum += term; if (Math.Abs(term) < Math.Abs(sum) * 1e-17) break; }
            return sum * Math.Exp(-x + a * Math.Log(x) - LogGamma(a));
        }
        return 1 - GammaUpper(a, x);
    }

    internal static double GammaUpper(double a, double x)
    {
        if (x <= 0) return 1;
        if (x < a + 1) return 1 - GammaLower(a, x);
        double b = x + 1 - a, c = 1e300, d = 1 / b, h = d;
        for (int i = 1; i < 20000000; i++)
        {
            double an = -i * (i - a);
            b += 2;
            d = an * d + b; if (Math.Abs(d) < 1e-300) d = 1e-300;
            c = b + an / c; if (Math.Abs(c) < 1e-300) c = 1e-300;
            d = 1 / d;
            double delta = d * c;
            h *= delta;
            if (Math.Abs(delta - 1) < 1e-17) break;
        }
        return Math.Exp(-x + a * Math.Log(x) - LogGamma(a)) * h;
    }

    internal static double ChiSquareUpper(double x, double df) => GammaUpper(df / 2, x / 2);

    internal static double ChiSquareQuantile(double p, double df)
    {
        double low = 0, high = 1;
        while (1 - ChiSquareUpper(high, df) < p) high *= 2;
        for (int i = 0; i < 300; i++) { double mid = (low + high) / 2; if (1 - ChiSquareUpper(mid, df) < p) low = mid; else high = mid; }
        return (low + high) / 2;
    }

    // the regularised incomplete beta function I_x(a, b), by its continued fraction
    internal static double Beta(double x, double a, double b)
    {
        if (x <= 0) return 0;
        if (x >= 1) return 1;
        double front = Math.Exp(LogGamma(a + b) - LogGamma(a) - LogGamma(b) + a * Math.Log(x) + b * Math.Log(1 - x));
        if (x > (a + 1) / (a + b + 2)) return 1 - Beta(1 - x, b, a);
        double c = 1, d = 1 - (a + b) * x / (a + 1);
        if (Math.Abs(d) < 1e-300) d = 1e-300;
        d = 1 / d;
        double h = d;
        for (int m = 1; m < 100000; m++)
        {
            double aa = m * (b - m) * x / ((a + 2 * m - 1) * (a + 2 * m));
            d = 1 + aa * d; if (Math.Abs(d) < 1e-300) d = 1e-300;
            c = 1 + aa / c; if (Math.Abs(c) < 1e-300) c = 1e-300;
            d = 1 / d; h *= d * c;
            aa = -(a + m) * (a + b + m) * x / ((a + 2 * m) * (a + 2 * m + 1));
            d = 1 + aa * d; if (Math.Abs(d) < 1e-300) d = 1e-300;
            c = 1 + aa / c; if (Math.Abs(c) < 1e-300) c = 1e-300;
            d = 1 / d;
            double delta = d * c;
            h *= delta;
            if (Math.Abs(delta - 1) < 1e-17) break;
        }
        return front * h / a;
    }

    // the upper tail of F on df1 and df2 degrees of freedom, and the value with a given upper tail
    internal static double FUpper(double f, double df1, double df2) => Beta(df2 / (df2 + df1 * f), df2 / 2, df1 / 2);

    internal static double FWithUpperTail(double p, double df1, double df2)
    {
        double low = 0, high = 1;
        while (FUpper(high, df1, df2) > p) high *= 2;
        for (int i = 0; i < 300; i++) { double mid = (low + high) / 2; if (FUpper(mid, df1, df2) > p) low = mid; else high = mid; }
        return (low + high) / 2;
    }

    internal static double NormalUpper(double z)
    {
        double half = GammaUpper(0.5, z * z / 2) / 2;
        return z >= 0 ? half : 1 - half;
    }

    internal static double NormalQuantile(double p)
    {
        double low = -40, high = 40;
        for (int i = 0; i < 300; i++) { double mid = (low + high) / 2; if (1 - NormalUpper(mid) < p) low = mid; else high = mid; }
        return (low + high) / 2;
    }

    // P(S >= s) for Kendall's score of n pairs without ties, from the numbers of orderings with each number of inversions
    internal static double KendallUpper(int n, double s)
    {
        BigInteger[] counts = { BigInteger.One };
        for (int m = 2; m <= n; m++)
        {
            BigInteger[] next = new BigInteger[counts.Length + m - 1];
            for (int k = 0; k < counts.Length; k++)
                for (int j = 0; j < m; j++) next[k + j] += counts[k];
            counts = next;
        }
        int pairs = n * (n - 1) / 2;
        BigInteger total = BigInteger.Zero, upper = BigInteger.Zero;
        for (int k = 0; k <= pairs; k++)
        {
            total += counts[k];
            if (pairs - 2 * k >= s) upper += counts[k];
        }
        return (double)upper / (double)total;
    }

    internal static ClassifierVariable Classifier(string title, string[] labels)
    {
        // as the program makes one: the groups numbered from 0 in the order in which their labels are first met; an empty cell (null here) is
        // given the label of a missing value, which has a group of its own, and the value of a missing number
        const string missing = "* (missing)";
        ClassifierVariable variable = new() { Title = title };
        List<string> met = new();
        double[] data = new double[labels.Length];
        for (int i = 0; i < labels.Length; i++)
        {
            string label = labels[i] ?? missing;
            int at = met.IndexOf(label);
            if (at < 0) { met.Add(label); at = met.Count - 1; }
            data[i] = label == missing ? M : at;
        }
        variable.Data = data;
        for (int g = 0; g < met.Count; g++) variable.Groups.Add(new Group(met[g], g) { NBin = labels.Count(l => (l ?? missing) == met[g]) });
        return variable;
    }

    private static int Main(string[] args)
    {
        Intraclass();
        Kappa();
        Universal();
        Console.WriteLine();
        Console.WriteLine(failures == 0 ? $"ALL {checks} CHECKS PASS" : $"{failures} OF {checks} CHECKS FAILED");
        return failures == 0 ? 0 : 1;
    }
}
