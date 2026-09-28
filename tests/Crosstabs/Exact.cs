// The Fisher-Freeman-Halton exact test against the sum over every table with the same totals, which is worked out here by listing
// the tables; and the two bounds that the method uses, against the least and the greatest that the tables give.
using System.Reflection;
using StatsDirect.Builtins;

internal static partial class Program
{
    private static double[] LogFactorials(int n)
    {
        double[] lf = new double[n + 1];
        for (int i = 2; i <= n; i++) lf[i] = lf[i - 1] + Math.Log(i);
        return lf;
    }

    // Every table with the totals of t: the probability of t, the sum of the probabilities of the tables that are no more probable than
    // it, and the least and the greatest sum of the logarithms of the factorials of the cells
    internal static (double observed, double p, double least, double greatest, long tables) EveryTable(int[] r, int[] c, int[,] t)
    {
        int rows = r.Length, cols = c.Length, n = r.Sum();
        double[] lf = LogFactorials(n);
        double constant = -lf[n];
        foreach (int v in r) constant += lf[v];
        foreach (int v in c) constant += lf[v];
        double observed = double.NaN;
        if (t != null)
        {
            observed = constant;
            for (int i = 0; i < rows; i++) for (int j = 0; j < cols; j++) observed -= lf[t[i, j]];
        }
        double total = 0, below = 0, least = double.MaxValue, greatest = double.MinValue;
        long count = 0;
        int[] rowLeft = (int[])r.Clone(), colLeft = (int[])c.Clone();
        void Fill(int i, int j, double sum)
        {
            if (i == rows - 1)
            {
                // the last row is what is left of the columns
                double s = sum;
                for (int jj = 0; jj < cols; jj++) s += lf[colLeft[jj]];
                double p = Math.Exp(constant - s);
                total += p; count++;
                least = Math.Min(least, s); greatest = Math.Max(greatest, s);
                if (t != null && constant - s <= observed + 1e-7) below += p;
                return;
            }
            if (j == cols - 1)
            {
                int v = rowLeft[i];
                if (v > colLeft[j]) return;
                colLeft[j] -= v;
                Fill(i + 1, 0, sum + lf[v]);
                colLeft[j] += v;
                return;
            }
            int most = Math.Min(rowLeft[i], colLeft[j]);
            for (int v = 0; v <= most; v++)
            {
                rowLeft[i] -= v; colLeft[j] -= v;
                Fill(i, j + 1, sum + lf[v]);
                rowLeft[i] += v; colLeft[j] += v;
            }
        }
        Fill(0, 0, 0);
        if (Math.Abs(total - 1) > 1e-9) throw new InvalidOperationException("the probabilities of the tables do not add to 1: " + total);
        return (Math.Exp(observed), below, least, greatest, count);
    }

    private static (double p, double observed, int fault) ExactTest(int[,] t)
    {
        int rows = t.GetLength(0), cols = t.GetLength(1);
        double[,] o = new double[rows + 1, cols + 1];
        for (int i = 0; i < rows; i++) for (int j = 0; j < cols; j++) o[i + 1, j + 1] = t[i, j];
        object[] a = { rows, cols, o, 0.0, 80.0, 1.0, 0.0, 0.0, 0 };
        typeof(Tables).GetMethod("Rcexact", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, a);
        return ((double)a[7], (double)a[6], (int)a[8]);
    }

    private static int[,] Table(string text)
    {
        int[][] rows = text.Split('/').Select(r => r.Trim().Split(' ').Select(int.Parse).ToArray()).ToArray();
        int[,] t = new int[rows.Length, rows[0].Length];
        for (int i = 0; i < rows.Length; i++) for (int j = 0; j < rows[0].Length; j++) t[i, j] = rows[i][j];
        return t;
    }

    private static string Text(int[,] t) =>
        string.Join(" / ", Enumerable.Range(0, t.GetLength(0)).Select(i => string.Join(" ", Enumerable.Range(0, t.GetLength(1)).Select(j => t[i, j]))));

    private static void Against(int[,] t, ref int compared, ref double worst)
    {
        int rows = t.GetLength(0), cols = t.GetLength(1);
        int[] r = Enumerable.Range(0, rows).Select(i => Enumerable.Range(0, cols).Sum(j => t[i, j])).ToArray();
        int[] c = Enumerable.Range(0, cols).Select(j => Enumerable.Range(0, rows).Sum(i => t[i, j])).ToArray();
        var (p, observed, fault) = ExactTest(t);
        var all = EveryTable(r, c, t);
        compared++;
        // the probability of the table has the tolerance of the comparisons of the method in it, which is the root of the machine epsilon
        double by = fault != 0 ? double.PositiveInfinity : Math.Max(Math.Abs(p - all.p), Math.Abs(observed - all.observed) / 100);
        if (by > worst && !double.IsInfinity(by)) worst = by;
        Say(fault == 0 && by <= 1e-9, $"the table {Text(t)}: P = {p}, the probability of the table {observed} (fault {fault}); from every table with its totals ({all.tables}) {all.p} and {all.observed}");
    }

    private static void Exact()
    {
        Console.WriteLine();
        Console.WriteLine("The exact test against every table with the same totals");
        int before = failures, compared = 0;
        double worst = 0;
        // tables that were wrong, or were refused, before the corrections of the method
        foreach (string text in new[]
        {
            "1 0 1 0 / 0 0 2 0 / 0 0 1 1 / 3 3 0 6 / 0 1 0 0",
            "0 0 1 0 0 1 / 0 1 0 1 0 0 / 0 1 2 2 0 1 / 0 1 0 0 0 0 / 1 0 1 0 1 2",
            "1 0 0 3 0 0 1 / 0 0 1 1 0 0 0 / 0 0 0 1 2 0 0 / 0 0 0 0 3 0 0 / 1 1 0 0 0 1 0",
            "0 0 0 0 0 1 0 / 2 0 2 0 1 0 0 / 0 1 0 0 2 0 1 / 0 0 1 0 0 0 0 / 2 0 0 1 0 2 0",
        })
            Against(Table(text), ref compared, ref worst);
        // tables drawn at random, with uneven totals: small ones of up to 20 cells, and sparse ones of 15 to 35 cells
        foreach (var (seed, number, large) in new[] { (1, 1500, false), (3, 1500, false), (11, 1500, true), (12, 1500, true) })
        {
            System.Random random = new(seed);
            for (int trial = 0; trial < number; trial++)
            {
                int rows = large ? random.Next(3, 6) : random.Next(2, 6), cols = large ? random.Next(5, 8) : random.Next(2, 7);
                if (!large && rows * cols > 20) { trial--; continue; }
                int n = large ? random.Next(12, rows * cols > 24 ? 17 : 21) : random.Next(Math.Max(rows, cols), rows * cols > 12 ? 22 : 36);
                int[,] t = new int[rows, cols];
                double[] weight = Enumerable.Range(0, rows * cols).Select(_ => Math.Pow(random.NextDouble(), 2)).ToArray();
                double all = weight.Sum();
                for (int s = 0; s < n; s++)
                {
                    double u = random.NextDouble() * all; int cell = 0;
                    while (cell < weight.Length - 1 && u > weight[cell]) { u -= weight[cell]; cell++; }
                    t[cell / cols, cell % cols]++;
                }
                bool empty = false;
                for (int i = 0; i < rows; i++) if (Enumerable.Range(0, cols).Sum(j => t[i, j]) == 0) empty = true;
                for (int j = 0; j < cols; j++) if (Enumerable.Range(0, rows).Sum(i => t[i, j]) == 0) empty = true;
                if (empty) { trial--; continue; }
                Against(t, ref compared, ref worst);
            }
        }
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  {compared} tables; the greatest difference of a probability is {worst:G3}");

        // the two bounds: the least and the greatest sum of the logarithms of the factorials of the cells of a table with given totals
        before = failures;
        MethodInfo shortpath = typeof(Tables).GetMethod("Shortpath", BindingFlags.NonPublic | BindingFlags.Static);
        MethodInfo longpath = typeof(Tables).GetMethod("Longpath", BindingFlags.NonPublic | BindingFlags.Static);
        System.Random draw = new(314159);
        int sets = 0;
        for (int trial = 0; trial < 1200; trial++)
        {
            bool sparse = trial >= 400;
            int rows = sparse ? draw.Next(2, 6) : draw.Next(2, 5), cols = sparse ? draw.Next(2, 8) : draw.Next(2, 6);
            int n = sparse ? draw.Next(Math.Max(rows, cols), 19) : draw.Next(rows + cols, 26);
            // totals of at least 1, in order of size
            int[] Totals(int k) { int[] v = Enumerable.Repeat(1, k).ToArray(); for (int i = 0; i < n - k; i++) v[draw.Next(k)]++; Array.Sort(v); return v; }
            int[] r = Totals(rows), c = Totals(cols);
            var all = EveryTable(r, c, null);
            int big = Math.Max(rows, cols), both = rows + cols + 1, k = Math.Max(both, big);
            double[] fact = new double[2 * (n + 1) + 1];
            Array.Copy(LogFactorials(n), fact, n + 1);
            int[] irow = new int[rows + 1], icol = new int[cols + 1];
            Array.Copy(r, 0, irow, 1, rows); Array.Copy(c, 0, icol, 1, cols);
            double tol = Math.Sqrt(2.220446049250313E-16);
            object[] s = { rows, irow.Clone(), cols, icol.Clone(), 0.0, n, fact, tol, 0, new int[k + 1], new int[k + 1], new int[k + 1], new int[k + 1], new int[k + 1], new int[big + 1], new int[big + 1], new int[4001], new int[4001], new double[k + 1], new double[4001] };
            object[] l = { big, rows, irow.Clone(), cols, icol.Clone(), 1000.0, fact, tol, new int[both + 1, both + 1], new int[k + 1], new int[k + 1], new int[k + 1], new int[k + 1], new int[k + 1], new int[rows + 1, both + 1], new double[k + 1] };
            string least, greatest;
            try { shortpath.Invoke(null, s); least = (int)s[8] == 0 && Math.Abs(-(double)s[4] - all.least) <= 1e-9 ? null : $"{-(double)s[4]} (fault {s[8]})"; }
            catch (TargetInvocationException ex) { least = ex.InnerException.GetType().Name; }
            try { longpath.Invoke(null, l); greatest = Math.Abs(1000.0 - (double)l[5] - all.greatest) <= 1e-9 ? null : (1000.0 - (double)l[5]).ToString(inv); }
            catch (TargetInvocationException ex) { greatest = ex.InnerException.GetType().Name; }
            sets++;
            Say(least == null, $"row totals {string.Join(",", r)}, column totals {string.Join(",", c)}: the least sum {all.least}; the routine gives {least}");
            Say(greatest == null, $"row totals {string.Join(",", r)}, column totals {string.Join(",", c)}: the greatest sum {all.greatest}; the routine gives {greatest}");
        }
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  the least and the greatest sum of {sets} sets of totals");
    }
}
