// The simulated exact P values: the tables that are drawn, and the tables that are counted.  A simulated P value is the share of
// the tables drawn, with the totals of the table observed, whose statistic is no less than that of the table observed.  Here every
// table that has the totals is gone through, with the probability that the totals give it: the tables that are drawn are to be
// those tables, as often as their probabilities say, and a simulated P value is to be the probability of the tables that are to
// be counted, which are all those whose statistic is that of the table observed or more.
using StatsDirect.Builtins;
using StatsDirect.Numerics;
using StatsDirect.Templates;

internal static partial class Program
{
    // Every table with the totals of the rows and of the columns, with its probability
    private static List<(int[,] table, double probability)> Every(int[] rows, int[] columns)
    {
        int r = rows.Length, c = columns.Length, all = rows.Sum();
        double[] lf = new double[all + 1];
        for (int i = 2; i <= all; i++) lf[i] = lf[i - 1] + Math.Log(i);
        double constant = rows.Sum(v => lf[v]) + columns.Sum(v => lf[v]) - lf[all];
        List<(int[,], double)> tables = new();
        int[,] x = new int[r, c];
        int[] left = (int[])columns.Clone();
        void Go(int i, int j, int leftInRow, double log)
        {
            if (i == r - 1)
            {
                double l = log;
                for (int k = 0; k < c; k++) { x[i, k] = left[k]; l -= lf[left[k]]; }
                tables.Add(((int[,])x.Clone(), Math.Exp(constant + l)));
                return;
            }
            if (j == c - 1)
            {
                if (leftInRow > left[j]) return;
                x[i, j] = leftInRow; left[j] -= leftInRow;
                Go(i + 1, 0, rows[i + 1], log - lf[leftInRow]);
                left[j] += leftInRow;
                return;
            }
            for (int v = 0; v <= Math.Min(leftInRow, left[j]); v++)
            {
                x[i, j] = v; left[j] -= v;
                Go(i, j + 1, leftInRow - v, log - lf[v]);
                left[j] += v;
            }
        }
        Go(0, 0, rows[0], 0);
        return tables;
    }

    private static string Text(int[,] x) => string.Join(" / ", Enumerable.Range(0, x.GetLength(0)).Select(i => string.Join(" ", Enumerable.Range(0, x.GetLength(1)).Select(j => x[i, j]))));

    // The four statistics of the r by c analysis, from their definitions: chi-square; G-square; the chi-square for trend, which is
    // the number of observations less 1 times the square of the correlation of the two scores over the observations; and the
    // chi-square for the equality of the mean scores of the columns, which is the number of observations less 1 times the share
    // of the sum of squares of the row scores that is between the columns
    private static double[] Four(int[,] x, double[] rowScores, double[] columnScores)
    {
        int r = x.GetLength(0), c = x.GetLength(1);
        double n = 0; double[] rt = new double[r], ct = new double[c];
        for (int i = 0; i < r; i++) for (int j = 0; j < c; j++) { rt[i] += x[i, j]; ct[j] += x[i, j]; n += x[i, j]; }
        double chi = 0, g = 0;
        double mx = 0, my = 0;
        for (int i = 0; i < r; i++) mx += rt[i] * rowScores[i] / n;
        for (int j = 0; j < c; j++) my += ct[j] * columnScores[j] / n;
        double sxx = 0, syy = 0, sxy = 0, between = 0;
        for (int i = 0; i < r; i++) sxx += rt[i] * (rowScores[i] - mx) * (rowScores[i] - mx);
        for (int j = 0; j < c; j++)
        {
            syy += ct[j] * (columnScores[j] - my) * (columnScores[j] - my);
            double mean = 0;
            for (int i = 0; i < r; i++)
            {
                double e = rt[i] * ct[j] / n;
                chi += (x[i, j] - e) * (x[i, j] - e) / e;
                if (x[i, j] > 0) g += 2 * x[i, j] * Math.Log(x[i, j] / e);
                sxy += x[i, j] * (rowScores[i] - mx) * (columnScores[j] - my);
                mean += x[i, j] * rowScores[i] / ct[j];
            }
            between += ct[j] * (mean - mx) * (mean - mx);
        }
        return new[] { chi, g, (n - 1) * sxy * sxy / (sxx * syy), (n - 1) * between / sxx };
    }

    private static void Simulations()
    {
        Console.WriteLine();
        Console.WriteLine("The simulated exact P values");
        System.Random random = new(20260930);

        // ---- the tables that are drawn: totals of which some columns, or some rows, have few observations among them
        int before = failures;
        (int[] rows, int[] columns)[] totals =
        {
            (new[] { 5, 5, 5 }, new[] { 13, 1, 1 }), (new[] { 3, 4, 2, 6 }, new[] { 1, 1, 12, 1 }), (new[] { 13, 1, 1 }, new[] { 5, 5, 5 }), (new[] { 4, 4, 4 }, new[] { 4, 4, 4 }),
            (new[] { 2, 9, 1 }, new[] { 1, 1, 1, 9 }), (new[] { 6, 3 }, new[] { 2, 3, 4 }), (new[] { 7, 5, 3 }, new[] { 9, 6 }), (new[] { 1, 1, 8 }, new[] { 8, 1, 1 }),
        };
        foreach (var (rows, columns) in totals)
        {
            var every = Every(rows, columns);
            Dictionary<string, double> probability = every.ToDictionary(t => Text(t.table), t => t.probability);
            Dictionary<string, int> drawn = new();
            int r = rows.Length, c = columns.Length;
            int[] rowTotals = new int[r + 1], columnTotals = new int[c + 1];
            Array.Copy(rows, 0, rowTotals, 1, r); Array.Copy(columns, 0, columnTotals, 1, c);
            int[,] x = new int[r + 1, c + 1];
            bool primed = false; double[] fact = new double[c + 1]; int total = 0, most = 5000000; int[] work = new int[c + 1];
            MersenneTwister rng = new(); rng.Seed(1 + random.Next(100000));
            const int n = 200000;
            for (int i = 0; i < n; i++)
            {
                Chi.Rcont2(1, r, c, rowTotals, columnTotals, ref primed, ref x, ref fact, ref total, ref most, ref work, out int fault, ref rng);
                int[,] t = new int[r, c];
                for (int a = 0; a < r; a++) for (int b = 0; b < c; b++) t[a, b] = x[a + 1, b + 1];
                string key = fault != 0 ? "fault " + fault : Text(t);
                drawn[key] = drawn.TryGetValue(key, out int had) ? had + 1 : 1;
            }
            string name = $"totals {string.Join(" ", rows)} of the rows and {string.Join(" ", columns)} of the columns";
            string[] others = drawn.Keys.Where(k => !probability.ContainsKey(k)).ToArray();
            Say(others.Length == 0, $"{name}: every table that is drawn has the totals ({others.Length} that were drawn have not, {others.Sum(k => drawn[k])} times in {n}; one is {others.FirstOrDefault()})");
            // each table is drawn as often as its probability says: 5 standard errors are allowed
            string far = probability.Where(p => Math.Abs((drawn.TryGetValue(p.Key, out int times) ? times : 0) / (double)n - p.Value) > 5 * Math.Sqrt(p.Value * (1 - p.Value) / n) + 1e-12)
                .Select(p => $"{p.Key}: drawn {(drawn.TryGetValue(p.Key, out int times) ? times : 0) / (double)n:F5}, probability {p.Value:F5}").FirstOrDefault();
            Say(far == null, $"{name}: each of the {probability.Count} tables is drawn as often as its probability says ({far})");
        }
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  the tables that are drawn have the totals, and are drawn as often as their probabilities say");

        // ---- the 2 by k test for trend: the same total in every row, so that a table has the chi-square of its mirror image; scores
        // 1 to k, and scores that are not whole numbers
        before = failures;
        int done = 0;
        for (int i = 0; i < 60; i++)
        {
            int k = random.Next(3, 5);
            int each = i % 3 == 0 ? random.Next(20, k == 3 ? 100 : 30) : random.Next(3, 12);
            int[] n = Enumerable.Repeat(each, k).ToArray();
            int[] s = n.Select(v => random.Next(0, v + 1)).ToArray();
            double first = random.Next(-3, 4), step = random.Next(1, 4) * 0.1;
            double[] v = i % 3 == 0 ? Enumerable.Range(1, k).Select(j => (double)j).ToArray() : Enumerable.Range(0, k).Select(j => first + step * j).ToArray();
            if (s.Sum() == 0 || s.Sum() == n.Sum()) continue;
            double probability = TrendProbability(s, n, v);
            const int tables = 200000;
            ParameterBag report = Simulated(i % 3 == 0 ? 1 : 2, s.Select(a => (double)a).ToArray(), n.Zip(s, (a, b) => (double)(a - b)).ToArray(), v, tables, 1 + random.Next(1000000), 0.99);
            List<ParameterBag> result = Rows(report, "*result");
            done++;
            if (result.Count != 1) { Say(false, $"successes {string.Join(" ", s)} of {each} in each row: no P value is simulated"); continue; }
            double p = result[0]["p"].AsDouble;
            if (Math.Abs(p - probability) > 5 * Math.Sqrt(probability * (1 - probability) / tables) + 1e-12)
                Say(false, $"successes {string.Join(" ", s)} of {each} in each row, scores {Table(v)}: the simulated P value is {p}, and the probability is {probability}");
        }
        Say(failures == before, $"the simulated P value of the test for trend, the same total in every row: {done} tables");
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  the test for trend of tables with the same total in every row, with the scores 1 to k and with scores that are not whole numbers, {done} tables");

        // ---- the r by c analysis: chi-square, G-square, the test for trend and that for the equality of the mean scores
        before = failures; done = 0;
        string[] names = { "chi-square", "G-square", "the chi-square for trend", "the chi-square for the equality of the mean scores" }, blocks = { "*pmcx2", "*pmcg2", "*pmcx2trend", "*pmcx2eq" };
        for (int i = 0; i < 40; i++)
        {
            int r = 3, c = random.Next(2, 4);
            bool parts = i % 2 == 1;
            double[] rowScores = Enumerable.Range(1, r).Select(a => parts ? 2 + 0.3 * a : a).ToArray(), columnScores = Enumerable.Range(1, c).Select(a => parts ? 0.7 * a : a).ToArray();
            int[,] o = new int[r, c];
            int each = random.Next(3, 7);
            for (int a = 0; a < r; a++)
            {
                int left = each;
                for (int b = 0; b < c - 1; b++) { o[a, b] = random.Next(0, left + 1); left -= o[a, b]; }
                o[a, c - 1] = left;
            }
            int[] columns = Enumerable.Range(0, c).Select(b => Enumerable.Range(0, r).Sum(a => o[a, b])).ToArray();
            if (columns.Any(v => v == 0)) continue;
            double[] observed = Four(o, rowScores, columnScores);
            double[] probability = new double[4];
            foreach (var (table, pr) in Every(Enumerable.Repeat(each, r).ToArray(), columns))
            {
                double[] its = Four(table, rowScores, columnScores);
                for (int k = 0; k < 4; k++) if (its[k] >= observed[k] - 1e-9 * Math.Max(1, observed[k])) probability[k] += pr;
            }
            double[,] given = new double[r + 1, c + 1];
            for (int a = 0; a < r; a++) for (int b = 0; b < c; b++) given[a + 1, b + 1] = o[a, b];
            TemplateHost.Scores1 = parts ? rowScores : null; TemplateHost.Scores2 = parts ? columnScores : null;
            double level = 0.95;
            const int tables = 200000;
            ParameterBag report = Tables.SChi(TemplateHost.New(), ref level, given, r, c, false, true, true, true, true, true, parts, 0.99, tables, 1 + random.Next(1000000));
            done++;
            for (int k = 0; k < 4; k++)
            {
                List<ParameterBag> result = Rows(report, blocks[k]);
                if (result.Count != 1 || result[0] == null) { Say(false, $"the table {Text(o)}: no simulated P value of {names[k]}"); continue; }
                double p = result[0]["p"].AsDouble;
                if (Math.Abs(p - probability[k]) > 5 * Math.Sqrt(probability[k] * (1 - probability[k]) / tables) + 1e-12)
                    Say(false, $"the table {Text(o)}, {(parts ? "row scores 2.3, 2.6, 2.9 and column scores 0.7, 1.4 and so on" : "scores 1 to k")}: the simulated P value of {names[k]} is {p}, and the probability is {probability[k]}");
            }
        }
        Say(failures == before, $"the simulated P values of the r by c analysis: {done} tables");
        // scores that are all the same: there is no test for trend, and none for the equality of the mean scores
        {
            double[,] given = { { 0, 0, 0 }, { 0, 3, 1 }, { 0, 2, 2 }, { 0, 1, 3 } };
            TemplateHost.Scores1 = new double[] { 2, 2, 2 }; TemplateHost.Scores2 = new double[] { 1, 2 };
            double level = 0.95;
            ParameterBag report = Tables.SChi(TemplateHost.New(), ref level, given, 3, 2, false, true, true, true, true, true, true, 0.99, 20000, 5);
            Say(Rows(report, "*pmcx2trend").Count == 0 && Rows(report, "*pmcx2eq").Count == 0 && Rows(report, "*pmcx2").Count == 1, $"row scores that are all the same: the tests for trend and for the equality of the mean scores have no simulated P value ({Rows(report, "*pmcx2trend").Count} and {Rows(report, "*pmcx2eq").Count})");
            TemplateHost.Scores1 = null; TemplateHost.Scores2 = null;
        }
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  the four simulated P values of the r by c analysis, with the scores 1 to k and with scores that are not whole numbers, {done} tables");
    }
}
