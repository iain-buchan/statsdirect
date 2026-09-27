// The universal agreement measure (Agreement.cs): delta is the mean distance between two observers' measurements of the same object, and R is 1
// less delta over its mean when each observer's measurements are dealt among the objects at random.  The mean, variance and skewness of delta
// that the program gives by formula are checked here against every such dealing listed, the P value against the gamma distribution, the
// simulated P against the listed dealings, and the calculator that compares two values of R against the reports that its figures come from.
using System.Reflection;
using StatsDirect.Builtins;
using StatsDirect.Data;
using StatsDirect.Templates;

internal static partial class Program
{
    private static List<int[]> Orderings(int n)
    {
        List<int[]> all = new();
        void Place(int[] done, int at, bool[] used)
        {
            if (at == n) { all.Add((int[])done.Clone()); return; }
            for (int v = 0; v < n; v++)
                if (!used[v]) { used[v] = true; done[at] = v; Place(done, at + 1, used); used[v] = false; }
        }
        Place(new int[n], 0, new bool[n]);
        return all;
    }

    // delta of every dealing: x[object, observer, dimension]; with a standard (the first observer) delta is the sum over the other observers
    // of their mean distance from it, and without one the mean over the pairs of observers
    private static double[] EveryDelta(double[,,] x, bool standard)
    {
        int n = x.GetLength(0), b = x.GetLength(1), c = x.GetLength(2);
        double[,,,] distance = new double[b, b, n, n];
        for (int r = 0; r < b; r++)
            for (int s = 0; s < b; s++)
                for (int i = 0; i < n; i++)
                    for (int j = 0; j < n; j++)
                    {
                        double sum = 0;
                        for (int m = 0; m < c; m++) sum += (x[i, r, m] - x[j, s, m]) * (x[i, r, m] - x[j, s, m]);
                        distance[r, s, i, j] = Math.Sqrt(sum);
                    }
        List<int[]> orderings = Orderings(n);
        List<double> deltas = new();
        int[][] chosen = new int[b][];
        chosen[0] = orderings[0];       // the first observer's measurements stay where they are: the first ordering is 0, 1, 2 ...
        double divisor = standard ? n : n * b * (b - 1) / 2.0;
        void Deal(int observer, double sum)
        {
            if (observer == b) { deltas.Add(sum / divisor); return; }
            foreach (int[] ordering in orderings)
            {
                chosen[observer] = ordering;
                double more = 0;
                for (int q = 0; q < (standard ? 1 : observer); q++)
                    for (int i = 0; i < n; i++) more += distance[q, observer, chosen[q][i], ordering[i]];
                Deal(observer + 1, sum + more);
            }
        }
        Deal(1, 0);
        return deltas.ToArray();        // the first is the delta observed
    }

    private static double TypeThree(double t, double skewness)
    {
        if (Math.Abs(skewness) < 1e-9) return 1 - NormalUpper(t);      // the difference from the normal distribution is below 1E-10
        double r = 2 / Math.Abs(skewness), shape = r * r;
        if (skewness > 0) return shape + r * t <= 0 ? 0 : GammaLower(shape, shape + r * t);
        return shape - r * t <= 0 ? 1 : GammaUpper(shape, shape - r * t);
    }

    private static ParameterBag UniversalInputs(double[,,] x, string[] observers, int reference)
    {
        int n = x.GetLength(0), b = x.GetLength(1), c = x.GetLength(2);
        List<double> values = new(); List<string> who = new(), what = new(), which = new();
        for (int r = 0; r < b; r++)
            for (int m = 0; m < c; m++)
                for (int i = 0; i < n; i++)
                {
                    values.Add(x[i, r, m]); who.Add(observers[r]); what.Add("object " + (i + 1)); which.Add("dimension " + (m + 1));
                }
        ParameterBag bag = new();
        bag.AddInput("data", new DataFrame(new DoubleVariable(values.ToArray(), "Measurement")));
        ClassifierVariable raters = Classifier("Observer", who.ToArray());
        bag.AddInput("raters", new DataFrame(raters));
        bag.AddInput("objects", new DataFrame(Classifier("Object", what.ToArray())));
        if (c > 1) bag.AddInput("categories", new DataFrame(Classifier("Dimension", which.ToArray())));
        if (reference >= 0)
        {
            // as the dialog box gives it: a mark against the chosen name in the list of the observers' names in order
            string[] sorted = raters.SortedCategoryNames;
            bool[] marks = new bool[sorted.Length];
            marks[Array.IndexOf(sorted, observers[reference])] = true;
            bag.AddInput("reference", marks);
        }
        return bag;
    }

    private static double[,,] Measurements(System.Random random, int n, int b, int c, double noise, bool whole)
    {
        double[,,] x = new double[n, b, c];
        for (int i = 0; i < n; i++)
            for (int m = 0; m < c; m++)
            {
                double truth = 10 + 8 * random.NextDouble();
                for (int r = 0; r < b; r++) x[i, r, m] = Math.Round(truth + noise * (random.NextDouble() - 0.5) * 8, whole ? 0 : 1);
            }
        return x;
    }

    private static void UniversalCase(string title, double[,,] x, string[] observers, int reference, bool simulate)
    {
        int before = failures;
        try
        {
            int n = x.GetLength(0), b = x.GetLength(1), c = x.GetLength(2);
            double[,,] arranged = x;
            if (reference > 0)
            {
                // the standard first
                arranged = (double[,,])x.Clone();
                for (int i = 0; i < n; i++) for (int m = 0; m < c; m++) { arranged[i, 0, m] = x[i, reference, m]; arranged[i, reference, m] = x[i, 0, m]; }
            }
            double[] all = EveryDelta(arranged, reference >= 0);
            double observed = all[0], mean = all.Average();
            double variance = all.Sum(v => (v - mean) * (v - mean)) / all.Length, third = all.Sum(v => Math.Pow(v - mean, 3)) / all.Length;
            double skewness = third / Math.Pow(variance, 1.5);
            ParameterBag o = Agreement.RptUniversalAgreement(UniversalInputs(x, observers, reference)).ParameterBag;
            Say(o["n"].AsInt32 == n && o["b"].AsInt32 == b && o["c"].AsInt32 == c && o["nobs"].AsInt32 == n * b * c, title + ": the numbers of objects, observers, dimensions and measurements");
            Say(o["ref"].AsString == (reference >= 0 ? "Observer " + observers[reference] : "None"), title + ": the standard named");
            Check(title + ": delta observed", Relative(o["delta"].AsDouble, observed), 1e-12);
            Check(title + ": mean of delta", Relative(o["edel"].AsDouble, mean), 1e-11);
            Check(title + ": variance of delta", Math.Abs(o["vardel"].AsDouble / variance - 1), 1e-9);
            Check(title + ": skewness of delta", Math.Abs(o["skewdel"].AsDouble - skewness), 1e-8);
            Check(title + ": R", Math.Abs(o["R"].AsDouble - (1 - observed / mean)), 1e-11);
            double t = (observed - mean) / Math.Sqrt(variance), p = TypeThree(t, skewness);
            Check(title + $": P (t = {t:G5}, skewness {skewness:G5}, exact {p:G6}, given {o["p"].AsDouble:G6})", Math.Abs(o["p"].AsDouble - p), 1e-6);
            universalReports.Add((o["R"].AsDouble, o["edel"].AsDouble, o["vardel"].AsDouble, o["skewdel"].AsDouble, o["p"].AsDouble));

            if (simulate && reference < 0)
            {
                double exact = all.Count(v => v <= observed * (1 + 1e-12) || v == observed) / (double)all.Length;
                ParameterBag inputs = UniversalInputs(x, observers, reference);
                inputs.AddInput("iterations", 200000);
                inputs.AddInput("seed", 12345);
                inputs.AddInput("ci", 0.99);
                ParameterBag s = Agreement.RptUniversalAgreementSimulateExactP(new NoProgress(), inputs).ParameterBag;
                double se = Math.Sqrt(Math.Max(exact * (1 - exact), 1.0 / all.Length) / 200000);
                Check(title + $": simulated P ({s["p"].AsDouble:G6}) against the P of every dealing ({exact:G6}), in standard errors", Math.Abs(s["p"].AsDouble - exact) / se, 4, true);
            }
        }
        catch (Exception ex) { checks++; failures++; Console.WriteLine("FAIL  " + title + ": " + Message(ex)); }
        if (failures == before) Console.WriteLine("ok    " + title);
    }

    private static readonly List<(double r, double mean, double variance, double skewness, double p)> universalReports = new();

    private static void Universal()
    {
        Console.WriteLine();
        Console.WriteLine("The universal agreement measure");
        // the example of the help (Mielke and Berry 2007): five objects, three observers, three dimensions
        double[] help =
        {
            8, 10.5, 17.6, 9, 14.6, 9.2, 2.5, 4.5, 12, 6, 6, 11, 13, 14.2, 7.5,
            8.2, 11.2, 20, 9, 14.2, 9, 3, 4.5, 12.5, 6, 6.5, 11.5, 15, 14, 8,
            8.2, 9.5, 21.4, 9.5, 14.5, 9, 2.8, 4.5, 13.5, 5.5, 6.5, 12.5, 17, 14.4, 9.2
        };
        double[,,] example = new double[5, 3, 3];
        for (int r = 0; r < 3; r++) for (int m = 0; m < 3; m++) for (int i = 0; i < 5; i++) example[i, r, m] = help[r * 15 + m * 5 + i];
        string[] three = { "1", "2", "3" };
        {
            ParameterBag o = Agreement.RptUniversalAgreement(UniversalInputs(example, three, -1)).ParameterBag;
            Check("the example of the help: delta, its mean, variance and skewness, and R as the topic gives them",
                new[] { o["delta"].AsDouble - 1.607036, o["edel"].AsDouble - 8.257518, o["vardel"].AsDouble - 1.166045, o["skewdel"].AsDouble + 0.777166, o["R"].AsDouble - 0.805385 }.Max(Math.Abs), 5e-7, true);
            o = Agreement.RptUniversalAgreement(UniversalInputs(example, three, 0)).ParameterBag;
            Check("the example of the help with the first observer as the standard",
                new[] { o["delta"].AsDouble - 3.432041, o["edel"].AsDouble - 16.218413, o["vardel"].AsDouble - 6.363892, o["skewdel"].AsDouble + 0.492177, o["R"].AsDouble - 0.788386 }.Max(Math.Abs), 5e-7, true);
        }
        UniversalCase("the example of the help", example, three, -1, true);
        UniversalCase("the example of the help, the first observer the standard", example, three, 0, false);
        UniversalCase("the example of the help, the third observer the standard", example, three, 2, false);

        System.Random random = new(53);
        string[] names = { "Dr C", "Dr A", "Dr D", "Dr B" };
        (int n, int b, int c)[] shapes = { (3, 2, 1), (4, 2, 1), (5, 2, 2), (6, 2, 1), (7, 2, 3), (3, 3, 1), (4, 3, 2), (5, 3, 1), (6, 3, 1), (3, 4, 2), (4, 4, 1), (5, 4, 1) };
        int set = 0;
        foreach ((int n, int b, int c) in shapes)
            foreach (double noise in new[] { 0.1, 1.0 })
            {
                set++;
                bool whole = set % 3 == 0;
                double[,,] x = Measurements(random, n, b, c, noise, whole);
                string[] observers = names.Take(b).ToArray();
                string title = $"set {set}: {n} objects, {b} observers, {c} dimension{(c > 1 ? "s" : "")}, {(noise < 1 ? "close" : "loose")} agreement" + (whole ? ", whole numbers" : "");
                UniversalCase(title, x, observers, -1, set % 4 == 1 && n > 3);
                UniversalCase(title + ", a standard", x, observers, set % b, false);
            }

        // perfect agreement: delta is nothing, and the only dealings as good are those that leave every object where it was
        {
            double[,,] x = new double[4, 2, 1];
            for (int i = 0; i < 4; i++) for (int r = 0; r < 2; r++) x[i, r, 0] = 3 + 2 * i;
            string title = "perfect agreement of two observers on four objects";
            try
            {
                ParameterBag inputs = UniversalInputs(x, new[] { "A", "B" }, -1);
                ParameterBag o = Agreement.RptUniversalAgreement(inputs).ParameterBag;
                Check(title + ": R", Math.Abs(o["R"].AsDouble - 1), 1e-12, true);
                inputs = UniversalInputs(x, new[] { "A", "B" }, -1);
                inputs.AddInput("iterations", 200000);
                inputs.AddInput("seed", 321);
                inputs.AddInput("ci", 0.99);
                ParameterBag s = Agreement.RptUniversalAgreementSimulateExactP(new NoProgress(), inputs).ParameterBag;
                double exact = 1 / 24.0, se = Math.Sqrt(exact * (1 - exact) / 200000);
                Check(title + $": simulated P ({s["p"].AsDouble:G6}) against 1/24, in standard errors", Math.Abs(s["p"].AsDouble - exact) / se, 4, true);
            }
            catch (Exception ex) { checks++; failures++; Console.WriteLine("FAIL  " + title + ": " + Message(ex)); }
        }

        // the P value of a standardised delta against the gamma distribution
        Console.WriteLine();
        Console.WriteLine("The P value of the universal agreement measure against the gamma distribution");
        MethodInfo pgamt = typeof(Agreement).GetMethod("Pgamt", BindingFlags.NonPublic | BindingFlags.Static);
        {
            // what data with agreement give: a negative skewness, and delta below its mean
            double worst = 0, worstRatio = 0, at = 0, atSkewness = 0;
            for (int k = 0; k <= 80; k++)
                for (double t = -8; t <= 0.0001; t += 0.0625)
                {
                    double skewness = -(0.01 + 1.99 * k / 80.0);
                    double given = (double)pgamt.Invoke(null, new object[] { t, skewness }), exact = TypeThree(t, skewness);
                    if (Math.Abs(given - exact) > worst) { worst = Math.Abs(given - exact); at = t; atSkewness = skewness; }
                    if (exact > 1e-12) worstRatio = Math.Max(worstRatio, Math.Abs(given / exact - 1));
                }
            Check($"skewness of -0.01 to -2 and delta below its mean: P (worst at t = {at}, skewness {atSkewness:G4})", worst, 5e-7, true);
            Check("skewness of -0.01 to -2 and delta below its mean: P above 1E-12, as a proportion of itself", worstRatio, 1e-5, true);
            // next to no skewness: the normal distribution with the first correction for skewness
            worst = 0;
            foreach (double size in new[] { 1e-3, 1e-4, 1e-5, 1e-6, 1e-7, 1e-8, 0 })
                foreach (int sign in new[] { -1, 1 })
                    for (double t = -6; t <= 6.0001; t += 0.25)
                    {
                        double skewness = sign * size, density = Math.Exp(-t * t / 2) / Math.Sqrt(2 * Math.PI);
                        double expected = 1 - NormalUpper(t) - skewness / 6 * (t * t - 1) * density
                            - skewness * skewness / 72 * (t * t * t * t * t - 10 * t * t * t + 15 * t) * density
                            - skewness * skewness / 16 * (t * t * t - 3 * t) * density;       // the kurtosis of a gamma distribution is 1.5 times the square of its skewness
                        double given = (double)pgamt.Invoke(null, new object[] { t, skewness });
                        worst = Math.Max(worst, Math.Abs(given - expected));
                    }
            Check("skewness of 0.001 and less: P against the normal distribution corrected for skewness and kurtosis", worst, 2e-9, true);
        }
        foreach ((double low, double high, string band) in new[] { (0.0, 0.0099, "skewness within 0.01 of nothing"), (0.01, 0.3, "skewness of 0.01 to 0.3"), (0.3, 1.0, "skewness of 0.3 to 1"), (1.0, 2.5, "skewness of 1 to 2.5") })
        {
            double worst = 0, worstRatio = 0, worstT = 0, worstSkewness = 0;
            for (int k = 0; k <= 40; k++)
                foreach (int sign in new[] { -1, 1 })
                    for (double t = -8; t <= 8.0001; t += 0.125)
                    {
                        double skewness = sign * (low + (high - low) * k / 40.0);
                        double given = (double)pgamt.Invoke(null, new object[] { t, skewness }), exact = TypeThree(t, skewness);
                        if (Math.Abs(given - exact) > worst) { worst = Math.Abs(given - exact); worstT = t; worstSkewness = skewness; }
                        if (exact > 1e-12 && exact < 0.05) worstRatio = Math.Max(worstRatio, Math.Abs(given / exact - 1));
                    }
            Check($"{band}: P (worst at t = {worstT}, skewness {worstSkewness:G4})", worst, 1e-6, true);
            Check($"{band}: P below 0.05, as a proportion of itself", worstRatio, 1e-3, true);
        }

        // the calculator that compares two values of R, given what two reports print
        Console.WriteLine();
        Console.WriteLine("The comparison of two values of R");
        for (int k = 0; k + 1 < universalReports.Count; k += 5)
        {
            var first = universalReports[k]; var second = universalReports[k + 1];
            string title = $"reports {k + 1} and {k + 2} (R = {first.r:F4} and {second.r:F4})";
            int before = failures;
            try
            {
                ParameterBag Compare((double r, double mean, double variance, double skewness, double p) a, (double r, double mean, double variance, double skewness, double p) b)
                {
                    ParameterBag bag = new();
                    bag.AddInput("r1_in", a.r); bag.AddInput("r2_in", b.r);
                    bag.AddInput("mu1_in", a.mean); bag.AddInput("mu2_in", b.mean);
                    bag.AddInput("var1_in", a.variance); bag.AddInput("var2_in", b.variance);
                    bag.AddInput("gam1_in", a.skewness); bag.AddInput("gam2_in", b.skewness);
                    return Agreement.RptUniversalRCompare(bag).ParameterBag;
                }
                ParameterBag o = Compare(first, second), swapped = Compare(second, first);
                Check(title + ": P of the first is the P of its report", Math.Abs(o["p1"].AsDouble - first.p), 1e-9);
                Check(title + ": P of the second is the P of its report", Math.Abs(o["p2"].AsDouble - second.p), 1e-9);
                double variance = first.variance / (first.mean * first.mean) + second.variance / (second.mean * second.mean);
                double third = -first.skewness * Math.Pow(first.variance, 1.5) / Math.Pow(first.mean, 3) + second.skewness * Math.Pow(second.variance, 1.5) / Math.Pow(second.mean, 3);
                double skewness = third / Math.Pow(variance, 1.5), lower = TypeThree((first.r - second.r) / Math.Sqrt(variance), skewness);
                Check(title + ": variance and skewness of the difference", Math.Abs(o["vard"].AsDouble / variance - 1) + Math.Abs(o["gamd"].AsDouble - skewness), 1e-10);
                Check(title + ": two sided P of the difference", Math.Abs(o["pd"].AsDouble - Math.Min(1, 2 * Math.Min(lower, 1 - lower))), 2e-6);
                Check(title + ": the same P with the two the other way round", Math.Abs(o["pd"].AsDouble - swapped["pd"].AsDouble), 2e-6);
            }
            catch (Exception ex) { checks++; failures++; Console.WriteLine("FAIL  " + title + ": " + Message(ex)); }
            if (failures == before) Console.WriteLine("ok    " + title);
        }
    }
}
