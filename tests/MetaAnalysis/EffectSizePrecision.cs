using System.Reflection;
using StatsDirect.Builtins;
using StatsDirect.Numerics;
using StatsDirect.Templates;

internal static partial class Program
{
    private static void EffectSizePrecision()
    {
        Console.WriteLine("Effect-size limits and Hedges correction at large parameters");
        string folder = Path.Combine(AppContext.BaseDirectory, "benchmarks");
        var interval = typeof(Meta).GetMethod("Ginterval", BindingFlags.NonPublic | BindingFlags.Static);
        double largestCdfError = 0, largestLimitError = 0;
        foreach (string line in File.ReadLines(Path.Combine(folder, "noncentral-t-cdf.txt")).Skip(1))
        {
            double[] r = line.Split('\t').Select(Number).ToArray();
            double actual = ExFortran.pnct(r[1], (int)r[0], r[2], out int fault);
            largestCdfError = Math.Max(largestCdfError, Math.Abs(actual - r[3]));
            Say(fault == 0 && Math.Abs(actual - r[3]) < 2e-11,
                $"noncentral t: df={r[0]}, t={r[1]}, delta={r[2]}: {actual:R}, integral {r[3]:R}");
        }
        foreach (string line in File.ReadLines(Path.Combine(folder, "noncentral-t-limits.txt")).Skip(1))
        {
            double[] r = line.Split('\t').Select(Number).ToArray();
            foreach (int sign in new[] { 1, -1 })
            {
                object[] args = { sign * r[1], (int)r[0], 1.0, r[2], 0.0, 0.0 };
                interval.Invoke(null, args);
                double lo = (double)args[4], hi = (double)args[5];
                double expectedLo = sign == 1 ? r[3] : -r[4], expectedHi = sign == 1 ? r[4] : -r[3];
                largestLimitError = Math.Max(largestLimitError, Math.Max(Math.Abs(lo - expectedLo), Math.Abs(hi - expectedHi)));
                Say(Math.Abs(lo - expectedLo) < 2e-11 && Math.Abs(hi - expectedHi) < 2e-11 && lo < hi,
                    $"noncentrality limits: df={r[0]}, t={sign * r[1]}, alpha={r[2]}: [{lo:R}, {hi:R}]; integral [{expectedLo:R}, {expectedHi:R}]");
            }
        }
        Console.WriteLine($"Largest absolute differences from independent integration: CDF {largestCdfError:G3}; noncentrality limits {largestLimitError:G3}");

        foreach (double t in new[] { -3.0, 3.0 })
        {
            double actual = ExFortran.pnct(t, 1, 0, out int fault);
            Say(fault == 0 && Math.Abs(actual - (.5 + Math.Atan(t) / Math.PI)) < 2e-15, "central t with one df agrees with the Cauchy closed form");
        }

        foreach (var (t, df, delta) in new (double, int, double)[] {
            (double.NaN, 10, 1), (M, 10, 1), (1, 0, 1), (1, 10, double.NaN), (1, 10, double.PositiveInfinity), (1, 10, M) })
        {
            double actual = ExFortran.pnct(t, df, delta, out int fault);
            Say(fault != 0 && actual == M, "invalid noncentral-t input reports failure");
        }
        foreach (double p in new[] { 0.0, 1, double.NaN })
        {
            var solve = typeof(Meta).GetMethod("NoncentralityOfT", BindingFlags.NonPublic | BindingFlags.Static);
            double actual = (double)solve.Invoke(null, new object[] { 40.0, 10000, p });
            Say(actual == M, "invalid noncentrality target reports failure");
        }

        Set(settings[0]);
        ParameterBag Effect(double n, double mean)
        {
            ParameterBag b = new();
            b.AddInput("gamma", .95); b.AddInput("type", "d");
            b.AddInput("en", Column("n1", new[] { n })); b.AddInput("cn", Column("n2", new[] { n }));
            b.AddInput("em", Column("mean1", new[] { mean })); b.AddInput("cm", Column("mean2", new[] { 0.0 }));
            b.AddInput("es", Column("sd1", new[] { 1.0 })); b.AddInput("cs", Column("sd2", new[] { 1.0 }));
            return Rows(Meta.RptEffect(new Host(), b).ParameterBag, "*exact")[0];
        }
        foreach (int sign in new[] { 1, -1 })
        {
            var report = Effect(5001, sign);
            double lo = report["lci"].AsDouble, hi = report["uci"].AsDouble;
            double expectedLo = sign == 1 ? .958404270242589 : -1.04155099343302;
            double expectedHi = sign == 1 ? 1.04155099343302 : -.958404270242589;
            Say(Math.Abs(lo - expectedLo) < 2e-11 && Math.Abs(hi - expectedHi) < 2e-11,
                $"full effect-size report, n1=n2=5001, g={sign}: [{lo:R}, {hi:R}]");
        }
        // Independent 65-digit log-gamma/Stirling calculations (not R's subtraction of two large lgamma values).
        foreach (var (df, expected) in new (int, double)[] {
            (2, .564189583547756287), (20, .961944533740748727), (198, .996206532385184516), (200, .996244522479126554),
            (1000, .999249781179716368), (10000, .999924997812429690), (100000, .999992499978124930),
            (1000000, .999999249999781250), (100000000, .999999992499999978), (1000000000, .999999999250000000) })
        {
            // g=0 keeps this test of J independent of the large-noncentrality integral.
            double actual = Effect(df / 2.0 + 1, 0)["gj"].AsDouble;
            Say(actual < 1 && Math.Abs(actual - expected) < 4e-15, $"Hedges J, df={df}: {actual:R}; high precision {expected:R}");
        }
    }
}
