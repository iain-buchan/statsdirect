// The Fisher-Freeman-Halton exact test of tables of many columns with few subjects in each.  The method counts the ways of reaching
// each part of a table, and for these tables the counts are above what a whole number of 32 bits holds.  The tables with the totals
// of such a table are too many to be listed one by one.  They are gone through here by kinds: the columns that have the same
// total are a group, a kind of column is the counts that a column of the group can have, and a table is known by the number of
// columns of each kind.  The arithmetic is of whole numbers of any size, and nothing of the method of the program is in it.
using System.Numerics;
using StatsDirect.Templates;

internal static partial class Program
{
    private static BigInteger Factorial(int n) { BigInteger f = 1; for (int i = 2; i <= n; i++) f *= i; return f; }
    private static BigInteger Choose(int n, int k) { BigInteger c = 1; for (int i = 1; i <= k; i++) c = c * (n - k + i) / i; return c; }

    // every column of so many counts that add to a total
    private static IEnumerable<int[]> Columns(int counts, int total)
    {
        if (counts == 1) { yield return new[] { total }; yield break; }
        for (int first = 0; first <= total; first++)
            foreach (int[] rest in Columns(counts - 1, total - first))
                yield return new[] { first }.Concat(rest).ToArray();
    }

    // The columns of a table from words such as "2 0:10;1 1:10;0 2:10": the counts of a column, and the number of such columns
    private static List<(int[] cells, int count)> Kinds(string text) =>
        text.Split(';').Select(k => k.Trim().Split(':')).Select(k => (k[0].Trim().Split(' ').Select(int.Parse).ToArray(), int.Parse(k[1]))).ToList();

    // the table, its columns in an order that mixes the kinds; on its side if that is asked for
    private static int[,] OfKinds(List<(int[] cells, int count)> kinds, bool onSide)
    {
        int rows = kinds[0].cells.Length, cols = kinds.Sum(k => k.count);
        List<int[]> columns = new();
        foreach (var (cells, count) in kinds) for (int i = 0; i < count; i++) columns.Add(cells);
        System.Random random = new(cols * 1000 + rows);
        columns = columns.OrderBy(_ => random.Next()).ToList();
        int[,] t = onSide ? new int[cols, rows] : new int[rows, cols];
        for (int j = 0; j < cols; j++) for (int i = 0; i < rows; i++) { if (onSide) t[j, i] = columns[j][i]; else t[i, j] = columns[j][i]; }
        return t;
    }

    // The P value of a table: the probability of the tables with its totals that are no more probable than it.  The probability of a
    // table is the product over its columns of the number of ways to put the subjects of the column into its counts, over the
    // number of ways to put all the subjects into the rows; the tables with a given number of columns of each kind are as many as
    // the ways to choose the columns of each kind from those of its group
    private static (double p, BigInteger tables) ByKinds(List<(int[] cells, int count)> kinds)
    {
        int rows = kinds[0].cells.Length;
        int[] rowTotals = new int[rows];
        foreach (var (cells, count) in kinds) for (int i = 0; i < rows; i++) rowTotals[i] += cells[i] * count;
        BigInteger all = Factorial(rowTotals.Sum());
        foreach (int t in rowTotals) all /= Factorial(t);
        BigInteger Weight(int[] cells) { BigInteger w = Factorial(cells.Sum()); foreach (int x in cells) w /= Factorial(x); return w; }
        BigInteger observed = 1;
        foreach (var (cells, count) in kinds) observed *= BigInteger.Pow(Weight(cells), count);
        var groups = kinds.GroupBy(k => k.cells.Sum()).Select(g => (total: g.Key, columns: g.Sum(k => k.count))).OrderBy(g => g.total).ToList();
        var entries = new List<(int group, int[] cells, BigInteger weight, bool last)>();
        for (int g = 0; g < groups.Count; g++)
        {
            List<int[]> columns = Columns(rows, groups[g].total).ToList();
            for (int t = 0; t < columns.Count; t++) entries.Add((g, columns[t], Weight(columns[t]), t == columns.Count - 1));
        }
        int[] left = groups.Select(g => g.columns).ToArray();
        int[] rowLeft = (int[])rowTotals.Clone();
        BigInteger sum = 0, below = 0, tables = 0;
        void Fill(int e, BigInteger ways, BigInteger weight)
        {
            if (e == entries.Count)
            {
                if (rowLeft.Any(v => v != 0)) return;
                tables += ways;
                sum += ways * weight;
                if (weight <= observed) below += ways * weight;
                return;
            }
            var (g, cells, w, last) = entries[e];
            int most = left[g];
            for (int i = 0; i < rows; i++) if (cells[i] > 0) most = Math.Min(most, rowLeft[i] / cells[i]);
            // the last kind of a group has the columns of the group that are left
            for (int k = last ? left[g] : 0; k <= most; k++)
            {
                BigInteger chosen = Choose(left[g], k);
                left[g] -= k;
                for (int i = 0; i < rows; i++) rowLeft[i] -= cells[i] * k;
                Fill(e + 1, ways * chosen, weight * BigInteger.Pow(w, k));
                for (int i = 0; i < rows; i++) rowLeft[i] += cells[i] * k;
                left[g] += k;
            }
        }
        Fill(0, 1, 1);
        // the tables that were gone through are all the tables: their probabilities add to 1
        if (sum != all) throw new InvalidOperationException($"the tables do not add to all the ways of putting the subjects into the rows: {sum} and {all}");
        return ((double)(below * BigInteger.Pow(10, 30) / all) / 1e30, tables);
    }

    private static void Ways()
    {
        Console.WriteLine();
        Console.WriteLine("The exact test of tables of many columns, against the tables with the same totals gone through by kinds of column");
        int before = failures, compared = 0;
        double worst = 0, longest = 0;

        // The way of going through the tables is itself checked, against the list of every table of small tables
        foreach (string words in new[] { "2 0:2;1 1:3;0 2:2", "3 0:1;2 1:2;1 2:1;0 3:2", "1 0:3;0 1:2;2 0:1;1 1:2;0 2:1", "2 0 0:1;0 2 0:1;0 0 2:1;1 1 0:1;1 0 1:1;0 1 1:1", "1 0 0:2;0 1 0:2;0 0 1:1;2 1 0:1;0 1 2:1" })
        {
            var kinds = Kinds(words);
            int[,] t = OfKinds(kinds, false);
            int[] r = Enumerable.Range(0, t.GetLength(0)).Select(i => Enumerable.Range(0, t.GetLength(1)).Sum(j => t[i, j])).ToArray();
            int[] c = Enumerable.Range(0, t.GetLength(1)).Select(j => Enumerable.Range(0, t.GetLength(0)).Sum(i => t[i, j])).ToArray();
            var all = EveryTable(r, c, t);
            var byKinds = ByKinds(kinds);
            Say(Math.Abs(byKinds.p - all.p) <= 1e-12 && byKinds.tables == all.tables, $"the table {Text(t)}: by kinds of column P = {byKinds.p} of {byKinds.tables} tables, and from the list of every table {all.p} of {all.tables}");
        }

        // Tables whose counts of ways are above 2,147,483,647, which had wrong P values, and tables of the same kinds with fewer columns;
        // each as it is and on its side
        foreach (string words in new[] {
            // 2 rows, 2 in each column: 25, 28 and 30 columns are the tables of the report of the defect
            "2 0:6;1 1:8;0 2:6", "2 0:8;1 1:9;0 2:8", "2 0:8;1 1:10;0 2:8", "2 0:9;1 1:10;0 2:9", "2 0:10;1 1:10;0 2:10", "2 0:13;1 1:4;0 2:13", "2 0:5;1 1:20;0 2:5",
            // rows of unlike totals; 3 in each column; columns of 1 and of 2
            "2 0:10;1 1:9;0 2:7", "3 0:5;2 1:5;1 2:6;0 3:5", "3 0:6;2 1:6;1 2:6;0 3:6", "1 0:9;0 1:9;2 0:5;1 1:6;0 2:5", "4 0:4;3 1:3;2 2:2;1 3:3;0 4:4",
            // 3 to 6 rows
            "2 0 0:3;0 2 0:3;0 0 2:3;1 1 0:2;1 0 1:2;0 1 1:2", "2 0 0:4;0 2 0:4;0 0 2:3;1 1 0:2;1 0 1:2;0 1 1:2", "2 0 0:4;0 2 0:4;0 0 2:4;1 1 0:2;1 0 1:2;0 1 1:2",
            "2 0 0 0:2;0 2 0 0:2;0 0 2 0:2;0 0 0 2:2;1 1 0 0:2;0 0 1 1:2;1 0 1 0:2;0 1 0 1:2", "2 0 0 0 0:2;0 2 0 0 0:2;0 0 2 0 0:2;0 0 0 2 0:2;0 0 0 0 2:2;1 1 0 0 0:2;0 0 1 1 0:2",
            "2 0 0 0 0 0:1;0 2 0 0 0 0:1;0 0 2 0 0 0:1;0 0 0 2 0 0:1;0 0 0 0 2 0:1;0 0 0 0 0 2:1;1 1 0 0 0 0:2;0 0 1 1 0 0:2;0 0 0 0 1 1:2",
            // 1 in each column: every table with the totals has the same probability, and the P value is 1
            "1 0 0:8;0 1 0:7;0 0 1:7" })
        {
            var kinds = Kinds(words);
            var reference = ByKinds(kinds);
            foreach (bool onSide in new[] { false, true })
            {
                int[,] t = OfKinds(kinds, onSide);
                System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
                var (p, _, fault) = ExactTest(t);
                double taken = clock.Elapsed.TotalSeconds;
                double by = Math.Abs(p - reference.p) / reference.p;
                compared++;
                if (fault == 0 && by > worst) worst = by;
                if (taken > longest) longest = taken;
                Say(fault == 0 && by <= 1e-9, $"{t.GetLength(0)} by {t.GetLength(1)}, columns {words}: P = {p} (fault {fault}), and from the {reference.tables} tables with its totals {reference.p}");
            }
        }

        // The report has the P value of the test, and does not call it an approximation
        foreach (string words in new[] { "2 0:8;1 1:9;0 2:8", "2 0 0:4;0 2 0:4;0 0 2:3;1 1 0:2;1 0 1:2;0 1 1:2" })
        {
            var kinds = Kinds(words);
            var reference = ByKinds(kinds);
            int[,] t = OfKinds(kinds, false);
            double[][] table = Enumerable.Range(0, t.GetLength(0)).Select(i => Enumerable.Range(0, t.GetLength(1)).Select(j => (double)t[i, j]).ToArray()).ToArray();
            ParameterBag report = Analysis(table, 0.95, null, null, true);
            object p = report["p2"].AsObject;
            Say(p is double given && Math.Abs(given - reference.p) <= 1e-9 * reference.p && report["lb"].AsString == "",
                $"{t.GetLength(0)} by {t.GetLength(1)}, columns {words}: the report has P = {p} {report["lb"].AsString}, and the P value is {reference.p}");
        }
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  {compared} tests of tables of 12 to 34 columns or rows: the greatest difference is {worst:E1} of the P value; the test that takes longest takes {longest:F1} seconds");
    }
}
