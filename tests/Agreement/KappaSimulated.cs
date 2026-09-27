// The simulated P of kappa (Tables.RptKappaSimulateExactP): tables are drawn at random with the totals of the rows and columns of the table
// observed, and P is the proportion of them with a kappa as great as was observed.  It is checked against every table with those totals,
// listed with its probability.  And the interval of a 2 by 2 table when the raters never disagree.
using StatsDirect.Builtins;
using StatsDirect.Data;
using StatsDirect.Templates;

internal static partial class Program
{
    // every k by k table with the row and column totals of o, with its probability when the ratings are independent
    private static IEnumerable<(double[,] table, double probability)> EveryTable(double[,] o)
    {
        int k = o.GetLength(0);
        int[] rows = new int[k], columns = new int[k];
        int n = 0;
        for (int i = 0; i < k; i++) for (int j = 0; j < k; j++) { rows[i] += (int)o[i, j]; columns[j] += (int)o[i, j]; n += (int)o[i, j]; }
        double constant = -LogGamma(n + 1.0);
        for (int i = 0; i < k; i++) constant += LogGamma(rows[i] + 1.0) + LogGamma(columns[i] + 1.0);
        int[,] cell = new int[k, k];
        List<(double[,], double)> all = new();
        void Fill(int i, int j, int[] left)      // left: what remains of each column's total
        {
            if (i == k - 1)
            {
                // the last row is what remains of the columns
                for (int c = 0; c < k; c++) cell[i, c] = left[c];
                if (left.Sum() != rows[i]) return;
                double log = constant;
                double[,] table = new double[k, k];
                for (int a = 0; a < k; a++) for (int b = 0; b < k; b++) { table[a, b] = cell[a, b]; log -= LogGamma(cell[a, b] + 1.0); }
                all.Add((table, Math.Exp(log)));
                return;
            }
            int used = 0;
            for (int c = 0; c < j; c++) used += cell[i, c];
            if (j == k - 1)
            {
                int last = rows[i] - used;
                if (last < 0 || last > left[j]) return;
                cell[i, j] = last;
                int[] next = (int[])left.Clone();
                for (int c = 0; c < k; c++) next[c] -= cell[i, c];
                Fill(i + 1, 0, next);
                return;
            }
            for (int v = 0; v <= Math.Min(rows[i] - used, left[j]); v++) { cell[i, j] = v; Fill(i, j + 1, left); }
        }
        Fill(0, 0, columns);
        return all;
    }

    private static void KappaSimulated()
    {
        Console.WriteLine();
        Console.WriteLine("The simulated P of kappa, against every table with the same totals");
        (double[,] table, int method)[] cases =
        {
            (new double[,] { { 12, 5 }, { 4, 9 } }, 1),
            (new double[,] { { 3, 1 }, { 2, 6 } }, 1),
            (new double[,] { { 8, 3, 1 }, { 2, 7, 2 }, { 1, 3, 6 } }, 1),
            (new double[,] { { 5, 3, 2 }, { 3, 4, 3 }, { 1, 3, 5 } }, 2),
            (new double[,] { { 4, 2, 1 }, { 2, 3, 2 }, { 0, 2, 4 } }, 3)
        };
        foreach ((double[,] o, int method) in cases)
        {
            int k = o.GetLength(0);
            string title = $"{k} by {k} table of {o.Cast<double>().Sum()} subjects, " + new[] { "", "linear weights", "quadratic weights", "weights given, not symmetric" }[method];
            try
            {
                double[,] w = method <= 2 ? Weights(k, method) : new double[,] { { 1, 0.6, 0.1 }, { 0.3, 1, 0.6 }, { 0, 0.3, 1 } };
                double[,] identity = Identity(k);
                double observed = WeightedKappa(Proportions(o), identity), observedW = WeightedKappa(Proportions(o), w);
                double exact = 0, exactW = 0, total = 0, undefined = 0;
                foreach ((double[,] table, double probability) in EveryTable(o))
                {
                    total += probability;
                    double kappa = WeightedKappa(Proportions(table), identity), kappaW = WeightedKappa(Proportions(table), w);
                    if (double.IsNaN(kappa)) { undefined += probability; continue; }
                    if (kappa >= observed - 1e-12) exact += probability;
                    if (kappaW >= observedW - 1e-12) exactW += probability;
                }
                Check(title + ": the probabilities of the tables add up to 1", Math.Abs(total - 1), 1e-10);
                ParameterBag bag = new();
                DataFrame frame = new(new DoubleVariable(Enumerable.Range(0, k).Select(i => o[i, 0]).ToArray(), "C1"));
                for (int j = 1; j < k; j++) frame.Variables.Add(new DoubleVariable(Enumerable.Range(0, k).Select(i => o[i, j]).ToArray(), "C" + (j + 1)));
                bag.AddInput("responsesCrosstab", frame);
                bag.AddInput("ci", 0.99);
                bag.AddInput("method", method.ToString());
                bag.AddInput("iterations", 400000);
                bag.AddInput("seed", 2468);
                bag.AddInput("kDouble", observed);
                bag.AddInput("kwDouble", observedW);
                if (method == 3)
                {
                    DataFrame weights = new(new DoubleVariable(Enumerable.Range(0, k).Select(i => w[i, 0]).ToArray(), "W1"));
                    for (int j = 1; j < k; j++) weights.Variables.Add(new DoubleVariable(Enumerable.Range(0, k).Select(i => w[i, j]).ToArray(), "W" + (j + 1)));
                    bag.AddInput("weights", weights);
                }
                ParameterBag s = Tables.RptKappaSimulateExactP(new NoProgress(), bag).ParameterBag;
                double se = Math.Sqrt(exact * (1 - exact) / 400000), seW = Math.Sqrt(exactW * (1 - exactW) / 400000);
                Check(title + $": simulated P of kappa ({s["p"].AsDouble:G6}) against {exact:G6}, in standard errors", Math.Abs(s["p"].AsDouble - exact) / se, 4, true);
                Check(title + $": simulated P of weighted kappa ({s["pw"].AsDouble:G6}) against {exactW:G6}, in standard errors", Math.Abs(s["pw"].AsDouble - exactW) / seW, 4, true);
                Say(s["ll"].AsDouble <= s["p"].AsDouble && s["p"].AsDouble <= s["ul"].AsDouble && s["ll"].AsDouble <= exact && exact <= s["ul"].AsDouble, title + ": the interval of the simulated P holds it and the P of every table");
            }
            catch (Exception ex) { checks++; failures++; Console.WriteLine("FAIL  " + title + ": " + Message(ex)); }
        }

        // a 2 by 2 table without a disagreement: kappa is 1, and so is the upper limit; the lower limit is where the chi-square of goodness of
        // fit, which is then n (1 - kappa) (1 + the ratio of the expected to ...), equals the square of the normal deviate
        foreach ((int a, int d) in new[] { (20, 15), (7, 30), (4, 4) })
        {
            string title = $"2 by 2 table of {a} and {d} in agreement and none in disagreement";
            try
            {
                double[,] o = { { a, 0 }, { 0, d } };
                double gamma = 0.95, z = NormalQuantile(1 - (1 - gamma) / 2), n = a + d, pi = a / n;
                List<ParameterBag> interval = Rows(Screen(o, 1, null, gamma), "*deci");
                double Fit(double kap)
                {
                    double e1 = n * (pi * pi + kap * pi * (1 - pi)), e2 = n * 2 * pi * (1 - pi) * (1 - kap), e3 = n * ((1 - pi) * (1 - pi) + kap * pi * (1 - pi));
                    return (a - e1) * (a - e1) / e1 + e2 + (d - e3) * (d - e3) / e3 - z * z;
                }
                double low = -Math.Min(pi / (1 - pi), (1 - pi) / pi) + 1e-9, high = 1;
                for (int i = 0; i < 200; i++) { double mid = (low + high) / 2; if (Fit(mid) > 0) low = mid; else high = mid; }
                Say(interval.Count == 1, title + ": an interval is given");
                Check(title + ": lower limit", Math.Abs(interval[0]["lwr"].AsDouble - (low + high) / 2), 1e-9, true);
                Check(title + ": upper limit", Math.Abs(interval[0]["upr"].AsDouble - 1), 0);
            }
            catch (Exception ex) { checks++; failures++; Console.WriteLine("FAIL  " + title + ": " + Message(ex)); }
        }
    }
}
