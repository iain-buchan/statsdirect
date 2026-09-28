// The scores of the tests for trend and for the equality of the mean scores have no origin and no unit: a number added to every
// score, or every score times a number, leaves the tests as they were.  And the tables of a simulation that are counted are those
// whose statistic is no less than that of the table observed, however many subjects the table has.  Here the scores are moved and
// stretched, and tables of up to 5 million subjects are simulated; the P values that are expected are from every table that has
// the totals, with its probability.
using StatsDirect.Builtins;
using StatsDirect.Templates;

internal static partial class Program
{
    // what is added to every score, and what every score is then multiplied by
    private static readonly (double add, double times)[] moves = { (1000, 1), (1e6, 1), (1e9, 1), (1e12, 1), (-1e6, 1), (0, 0.001), (0, 7), (0, 1e6), (1e6, 1024), (0, -1) };

    // the scores are moved as decimal numbers are: 0.7 and 1000000 make the number that 1000000.7 is held as
    private static double[] Move(double[] scores, double add, double times) => scores.Select(v => (double)((decimal)v + (decimal)add) * times).ToArray();

    // the r by c analysis with scores that are given, and its simulation
    private static ParameterBag WithScores(int[,] o, double[] rowScores, double[] columnScores, int tables, int seed)
    {
        int r = o.GetLength(0), c = o.GetLength(1);
        double[,] given = new double[r + 1, c + 1];
        for (int a = 0; a < r; a++) for (int b = 0; b < c; b++) given[a + 1, b + 1] = o[a, b];
        TemplateHost.Scores1 = rowScores; TemplateHost.Scores2 = columnScores;
        double level = 0.95;
        try { return Tables.SChi(TemplateHost.New(), ref level, given, r, c, false, true, true, true, true, true, true, 0.99, tables, seed); }
        finally { TemplateHost.Scores1 = null; TemplateHost.Scores2 = null; }
    }

    private static readonly string[] simulated = { "*pmcx2", "*pmcg2", "*pmcx2trend", "*pmcx2eq" };
    private static readonly string[] statistics = { "chi-square", "G-square", "the chi-square for trend", "the chi-square for the equality of the mean scores" };

    // the four simulated P values of a report, or nothing for one that is not given
    private static double[] SimulatedP(ParameterBag report) =>
        simulated.Select(block => { List<ParameterBag> rows = Rows(report, block); return rows.Count == 1 && rows[0] != null ? rows[0]["p"].AsDouble : double.NaN; }).ToArray();

    // The probabilities of the first count of a 2 by 2 table with the totals given, from that of the most probable count outwards
    private static (int least, double[] probability) FirstCount(int row1, int row2, int column1)
    {
        int least = Math.Max(0, column1 - row2), most = Math.Min(row1, column1);
        int mode = Math.Min(most, Math.Max(least, (int)Math.Floor((row1 + 1.0) * (column1 + 1.0) / (row1 + row2 + 2.0))));
        double[] p = new double[most - least + 1];
        p[mode - least] = 1;
        for (int k = mode; k < most; k++) p[k + 1 - least] = p[k - least] * (row1 - k) * (double)(column1 - k) / ((k + 1.0) * (row2 - column1 + k + 1.0));
        for (int k = mode; k > least; k--) p[k - 1 - least] = p[k - least] * k * (double)(row2 - column1 + k) / ((row1 - k + 1.0) * (column1 - k + 1.0));
        double all = p.OrderBy(v => v).Sum();
        return (least, p.Select(v => v / all).ToArray());
    }

    private static void Moved()
    {
        Console.WriteLine();
        Console.WriteLine("Scores that are moved and stretched, and tables of many subjects");
        System.Random random = new(20261002);

        // ---- the r by c analysis: the simulated P values, and the chi-squares of the two tests that have scores
        int before = failures, done = 0;
        double worst = 0;
        for (int i = 0; i < 40; i++)
        {
            int r = random.Next(2, 5), c = random.Next(2, 5), most = i % 4 == 0 ? random.Next(20, 400) : random.Next(2, 12);
            int[,] o = new int[r, c];
            for (int a = 0; a < r; a++) for (int b = 0; b < c; b++) o[a, b] = random.Next(0, most + 1);
            if (Enumerable.Range(0, r).Any(a => Enumerable.Range(0, c).Sum(b => o[a, b]) == 0) || Enumerable.Range(0, c).Any(b => Enumerable.Range(0, r).Sum(a => o[a, b]) == 0)) { i--; continue; }
            // scores that are whole numbers; scores in eighths, which are held as they are, with a number added to them or
            // times a power of 2; and scores in tenths, which are not
            int parts = i % 3;
            double[] rowScores = Enumerable.Range(1, r).Select(a => parts == 1 ? -1.5 + 0.375 * a * a : parts == 2 ? Math.Round(-1.5 + 0.3 * a * a, 1) : a).ToArray();
            double[] columnScores = Enumerable.Range(1, c).Select(a => parts == 1 ? 0.75 * a : parts == 2 ? Math.Round(0.7 * a, 1) : a).ToArray();
            int seed = 1 + random.Next(1000000);
            ParameterBag plain = WithScores(o, rowScores, columnScores, 20000, seed);
            double[] p = SimulatedP(plain);
            done++;
            foreach (var (add, times) in moves)
            {
                ParameterBag moved = WithScores(o, Move(rowScores, add, times), Move(columnScores, add, times), 20000, seed);
                double[] q = SimulatedP(moved);
                string what = $"the table {Text(o)}, scores {Table(rowScores)} and {Table(columnScores)}, plus {add.ToString(inv)} and times {times.ToString(inv)}";
                for (int k = 0; k < 4; k++)
                    if (!(q[k] == p[k])) Say(false, $"{what}: the simulated P value of {statistics[k]} is {q[k]}, and with the scores as they are {p[k]}");
                // the chi-squares, and the correlation; one that is nothing can have what rounding leaves
                foreach (string figure in new[] { "chit", "chie", "r" })
                {
                    double given = moved[figure].AsObject is double d ? d : double.NaN, expected = plain[figure].AsDouble;
                    double by = Math.Abs(given - expected) / Math.Max(Math.Abs(expected), 1e-9);
                    if (by > worst) worst = by;
                    if (!(by <= 1e-9)) Say(false, $"{what}: {figure} is {moved[figure].AsObject}, and with the scores as they are {plain[figure].AsObject}");
                }
            }
        }
        Say(failures == before, $"the r by c analysis with scores that are moved and stretched: {done} tables, {moves.Length} sets of scores of each");
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  the r by c analysis, {done} tables each with {moves.Length} sets of scores: the simulated P values are the same, and the chi-squares differ by no more than {worst:E1} of them");

        // ---- the 2 by k test for trend, and its simulation
        before = failures; done = 0; worst = 0;
        for (int i = 0; i < 30; i++)
        {
            int k = random.Next(3, 6);
            int[] n = Enumerable.Range(0, k).Select(_ => i % 4 == 0 ? random.Next(50, 2000) : random.Next(3, 15)).ToArray();
            int[] s = n.Select(v => random.Next(0, v + 1)).ToArray();
            if (s.Sum() == 0 || s.Sum() == n.Sum()) { i--; continue; }
            int parts = i % 3;
            double[] scores = Enumerable.Range(1, k).Select(a => parts == 1 ? -1.5 + 0.375 * a * a : parts == 2 ? Math.Round(-1.5 + 0.3 * a * a, 1) : a).ToArray();
            double[] successes = s.Select(v => (double)v).ToArray(), failed = n.Zip(s, (a, b) => (double)(a - b)).ToArray();
            int seed = 1 + random.Next(1000000);
            List<ParameterBag> plain = Rows(Simulated(2, successes, failed, scores, 20000, seed, 0.99), "*result");
            Dictionary<string, object> figures = Report("plain", () => TwoByK(2, successes, failed, scores));
            done++;
            foreach (var (add, times) in moves)
            {
                double[] movedScores = Move(scores, add, times);
                string what = $"successes {string.Join(" ", s)} of {string.Join(" ", n)}, scores {Table(scores)}, plus {add.ToString(inv)} and times {times.ToString(inv)}";
                List<ParameterBag> moved = Rows(Simulated(2, successes, failed, movedScores, 20000, seed, 0.99), "*result");
                if (plain.Count != 1 || moved.Count != 1) { Say(false, $"{what}: no P value is simulated"); continue; }
                if (!(moved[0]["p"].AsDouble == plain[0]["p"].AsDouble)) Say(false, $"{what}: the simulated P value is {moved[0]["p"].AsDouble}, and with the scores as they are {plain[0]["p"].AsDouble}");
                // every chi-square and P value of the report
                foreach (var f in Report("plain", () => TwoByK(2, successes, failed, movedScores)))
                {
                    if (f.Value is not double given || !figures.TryGetValue(f.Key, out object had) || had is not double expected) continue;
                    if (!(f.Key.Contains("chi") || f.Key.Contains("|p") || f.Key.EndsWith("|z"))) continue;
                    double by = Math.Abs(given - expected) / Math.Max(Math.Abs(expected), 1e-9);
                    if (by > worst) worst = by;
                    if (by > 1e-9) Say(false, $"{what}: {f.Key} is {given}, and with the scores as they are {expected}");
                }
            }
        }
        Say(failures == before, $"the 2 by k test for trend with scores that are moved and stretched: {done} tables, {moves.Length} sets of scores of each");
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  the 2 by k test for trend, {done} tables each with {moves.Length} sets of scores: the simulated P values are the same, and the chi-squares differ by no more than {worst:E1} of them");

        // ---- Tables of 2 by 2 with many subjects.  Chi-square, the chi-square for trend and that for the equality of the mean
        // scores put the tables in one order, that of the distance of the first count from what is expected: the same tables
        // are counted for the three.  G-square has that order too if the totals of the rows are the same, or those of the columns
        before = failures; done = 0;
        const int draws = 100000;
        foreach (var (row1, row2, column1, from) in new[] { (1000, 1000, 1000, 7), (100000, 100000, 100000, 60), (2500000, 2500000, 2500000, 6), (2500000, 2500000, 2500000, 700), (2500000, 2500000, 2500000, 1),
            (1500, 500, 800, 9), (300000, 100000, 50000, 110), (4000000, 1000000, 2000000, 500), (4990000, 10000, 5000, 3), (3000000, 2000000, 100, 4) })
        {
            // the first count is from what is expected by so much
            var (least, probability) = FirstCount(row1, row2, column1);
            double all = (double)row1 + row2, expected = row1 * (double)column1 / all;
            int first = (int)Math.Round(expected) + from;
            int[,] o = { { first, row1 - first }, { column1 - first, row2 - column1 + first } };
            double[] Of(int a)
            {
                double[] x = { a, row1 - a, column1 - a, row2 - column1 + a };
                double[] e = { expected, row1 - expected, column1 - expected, row2 - column1 + expected };
                double chi = 0, g = 0;
                for (int cell = 0; cell < 4; cell++)
                {
                    chi += (x[cell] - e[cell]) * (x[cell] - e[cell]) / e[cell];
                    // x log(x / e) less x - e, which add to nothing over the cells, from the series of the logarithm if x is near e
                    double t = (x[cell] - e[cell]) / e[cell];
                    if (x[cell] == 0) g += e[cell];
                    else if (Math.Abs(t) < 0.01) g += e[cell] * t * t * (0.5 - t / 6 + t * t / 12 - t * t * t / 20 + t * t * t * t / 30);
                    else g += x[cell] * Math.Log(x[cell] / e[cell]) - (x[cell] - e[cell]);
                }
                return new[] { chi, 2 * g, Math.Abs(a - expected) };
            }
            double[] observed = Of(first);
            double[] exact = new double[3];
            for (int a = least; a < least + probability.Length; a++)
            {
                double[] its = Of(a);
                for (int k = 0; k < 3; k++) if (its[k] >= observed[k] * (1 - 1e-9)) exact[k] += probability[a - least];
            }
            ParameterBag report = WithScores(o, new double[] { 1, 2 }, new double[] { 1, 2 }, draws, 1 + random.Next(1000000));
            double[] p = SimulatedP(report);
            done++;
            string what = $"the table {Text(o)}";
            Say(p[0] == p[2] && p[0] == p[3], $"{what}: the simulated P values of chi-square, of the test for trend and of the test of the equality of the mean scores are of the same tables ({p[0]}, {p[2]}, {p[3]})");
            if (row1 == row2 || column1 == row1 + row2 - column1) Say(p[1] == p[0], $"{what}: and so is that of G-square ({p[1]})");
            for (int k = 0; k < 4; k++)
            {
                double probabilityOf = exact[k == 1 ? 1 : k == 0 ? 0 : 2];
                Say(Math.Abs(p[k] - probabilityOf) <= 5 * Math.Sqrt(probabilityOf * (1 - probabilityOf) / draws) + 1e-12, $"{what}: the simulated P value of {statistics[k]} is {p[k]}, and the probability is {probabilityOf}");
            }
        }
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  {done} tables of 2 by 2 with 2,000 to 5 million subjects: the tables that are counted, and the simulated P values against the probabilities");

        // ---- tables of 2 by 3 and 3 by 3 with hundreds of subjects, near to what is expected and far from it: every table with the
        // totals is gone through
        before = failures; done = 0;
        foreach (var (rows, columns, far) in new[] { (new[] { 300, 500 }, new[] { 200, 250, 350 }, 4), (new[] { 300, 500 }, new[] { 200, 250, 350 }, 25), (new[] { 640, 560 }, new[] { 400, 400, 400 }, 2), (new[] { 640, 560 }, new[] { 400, 400, 400 }, 30),
            (new[] { 40, 50, 60 }, new[] { 50, 50, 50 }, 2), (new[] { 40, 50, 60 }, new[] { 45, 50, 55 }, 7) })
        {
            int r = rows.Length, c = columns.Length;
            double n = rows.Sum();
            // a table near to what is expected, with its first counts moved by so much
            int[,] o = new int[r, c];
            for (int a = 0; a < r; a++) for (int b = 0; b < c; b++) o[a, b] = (int)Math.Floor(rows[a] * (double)columns[b] / n);
            for (int a = 0; a < r; a++) o[a, c - 1] += rows[a] - Enumerable.Range(0, c).Sum(b => o[a, b]);
            // the totals of the columns are put right in the last row, and the move is of four counts, which leaves the totals
            for (int b = 0; b < c; b++) { int had = Enumerable.Range(0, r).Sum(a => o[a, b]); o[r - 1, b] += columns[b] - had; }
            for (int a = 0; a < r; a++) { int had = Enumerable.Range(0, c).Sum(b => o[a, b]); if (had != rows[a]) o[a, 0] += rows[a] - had; }
            o[0, 0] += far; o[0, c - 1] -= far; o[r - 1, 0] -= far; o[r - 1, c - 1] += far;
            bool whole = Enumerable.Range(0, r).All(a => Enumerable.Range(0, c).Sum(b => o[a, b]) == rows[a]) && Enumerable.Range(0, c).All(b => Enumerable.Range(0, r).Sum(a => o[a, b]) == columns[b]) && o.Cast<int>().All(v => v >= 0);
            if (!whole) { Say(false, $"the table {Text(o)} has not the totals {string.Join(" ", rows)} and {string.Join(" ", columns)}"); continue; }
            double[] rowScores = Enumerable.Range(1, r).Select(a => (double)a).ToArray(), columnScores = Enumerable.Range(1, c).Select(a => (double)a).ToArray();
            double[] observed = Four(o, rowScores, columnScores);
            double[] probability = new double[4];
            foreach (var (table, pr) in Every(rows, columns))
            {
                double[] its = Four(table, rowScores, columnScores);
                for (int k = 0; k < 4; k++) if (its[k] >= observed[k] - 1e-9 * Math.Max(1e-6, observed[k])) probability[k] += pr;
            }
            double[] p = SimulatedP(WithScores(o, rowScores, columnScores, draws, 1 + random.Next(1000000)));
            done++;
            for (int k = 0; k < 4; k++)
                Say(Math.Abs(p[k] - probability[k]) <= 5 * Math.Sqrt(probability[k] * (1 - probability[k]) / draws) + 1e-12, $"the table {Text(o)}: the simulated P value of {statistics[k]} is {p[k]}, and the probability is {probability[k]}");
        }
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  {done} tables of 2 by 3 and 3 by 3 with 150 to 1,200 subjects: the simulated P values against the probabilities");
    }
}
