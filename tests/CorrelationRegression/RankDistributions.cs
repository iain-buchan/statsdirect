// The distributions of Spearman's statistic (S, the sum of the squared differences between the ranks) and of Kendall's score, without ties:
// the program's P values against the count of every ordering.  For Spearman's statistic the program takes every ordering in turn for 10 pairs
// or fewer, and for Kendall's score it counts the orderings for 1000 pairs or fewer; for more pairs each is from a series (the
// series for Kendall's score is checked in tests/Nonparametric).
using StatsDirect.Numerics;

internal static partial class Program
{
    // how many of the orderings of 1..n have each value of S
    private static long[] SpearmanCounts(int n)
    {
        // the orderings are built up a place at a time.  What matters of the places filled so far is which ranks have been used and the
        // sum so far: ways[used][sum] is the number of ways of filling the first places with the ranks marked in used, to that sum
        int size = n * (n * n - 1) / 3 + 1;
        long[][] ways = new long[1 << n][];
        ways[0] = new long[size];
        ways[0][0] = 1;
        for (int used = 0; used < (1 << n) - 1; used++)
        {
            if (ways[used] == null) continue;
            int position = System.Numerics.BitOperations.PopCount((uint)used) + 1;
            for (int rank = 1; rank <= n; rank++)
            {
                if ((used & (1 << (rank - 1))) != 0) continue;
                int next = used | (1 << (rank - 1)), more = (position - rank) * (position - rank);
                ways[next] ??= new long[size];
                for (int sum = 0; sum + more < size; sum++)
                    if (ways[used][sum] != 0) ways[next][sum + more] += ways[used][sum];
            }
            ways[used] = null;
        }
        return ways[(1 << n) - 1];
    }

    private static void SpearmanDistribution()
    {
        int before = checks, failed = failures;
        foreach (int n in new[] { 4, 7, 9, 10, 11, 12, 14 })
        {
            long[] counts = SpearmanCounts(n);
            double total = counts.Sum(c => (double)c);
            double worst = 0, worstOdd = 0, worstTail = 0;
            double upper = total;       // the orderings with S >= s
            for (int s = 0; s < counts.Length; s += 2)
            {
                double exact = upper / total;
                double given = ExFortran.prho(n, s, out int fault);
                if (fault != 0) given = double.NaN;
                worst = Math.Max(worst, double.IsNaN(given) ? double.PositiveInfinity : Math.Abs(given - exact));
                if (exact < 0.05 && exact >= 0.001) worstTail = Math.Max(worstTail, double.IsNaN(given) ? double.PositiveInfinity : Math.Abs(given / exact - 1));
                // S is always even: an odd s stands for the even number above it
                if (s > 0) worstOdd = Math.Max(worstOdd, Math.Abs(ExFortran.prho(n, s - 1, out _) - given));
                upper -= counts[s];
            }
            if (n <= 10)
            {
                Check($"Spearman's S, {n} pairs: P(S >= s) at every s, against every ordering", worst, 1e-12);
                Console.WriteLine($"ok    Spearman's S, {n} pairs: worst difference {worst:E2} from the count of every ordering");
            }
            else
            {
                // the series: within 0.0004 at 11 pairs, and nearer with more, but not exact
                Check($"Spearman's S, {n} pairs: P(S >= s) by the series, against every ordering", worst, 5e-4);
                Check($"Spearman's S, {n} pairs: P from 0.001 to 0.05 by the series, as a proportion of itself", worstTail, 0.2);
                Console.WriteLine($"      Spearman's S, {n} pairs, by the series: worst difference {worst:E2}, and {worstTail:P1} of a P from 0.001 to 0.05");
            }
            Check($"Spearman's S, {n} pairs: an odd s gives what the even number above it gives", worstOdd, 0);
        }
        // more pairs than whole numbers can hold the greatest S of: the same series, which should treat an odd s in the same way
        foreach (long n in new long[] { 2000, 5000 })
        {
            double middle = Math.Floor(n * ((double)n * n - 1) / 6 * 0.97 / 2) * 2;
            Check($"Spearman's S, {n} pairs: an odd s gives what the even number above it gives", Math.Abs(MathDbl.bigprho(n, middle - 1, out _) - MathDbl.bigprho(n, middle, out _)), 0);
        }
        Console.WriteLine($"the distribution of Spearman's statistic: {checks - before} checks, {failures - failed} failed");
    }
    private static void KendallDistribution()
    {
        int before = checks, failed = failures;
        foreach (int n in new[] { 5, 20, 50, 51, 60, 100 })
        {
            int pairs = n * (n - 1) / 2;
            double worst = 0, worstTail = 0;
            // the orderings with each number of inversions, counted once: a score of s belongs to (pairs - s) / 2 inversions
            System.Numerics.BigInteger[] counts = Inversions(n);
            System.Numerics.BigInteger total = System.Numerics.BigInteger.Zero, upper = System.Numerics.BigInteger.Zero;
            foreach (System.Numerics.BigInteger count in counts) total += count;
            double[] tail = new double[pairs + 1];      // tail[k]: the proportion of orderings with k inversions or fewer
            for (int k = 0; k <= pairs; k++) { upper += counts[k]; tail[k] = Math.Exp(System.Numerics.BigInteger.Log(upper) - System.Numerics.BigInteger.Log(total)); }
            // the score has the same parity as the number of pairs
            for (int s = pairs % 2; s <= pairs; s += 2)
            {
                double exact = tail[(pairs - s) / 2];
                double given = MathDbl.kendp(s, n, out bool fault);
                if (fault) given = double.NaN;
                worst = Math.Max(worst, double.IsNaN(given) ? double.PositiveInfinity : Math.Abs(given - exact));
                if (exact < 0.05 && exact >= 0.001) worstTail = Math.Max(worstTail, double.IsNaN(given) ? double.PositiveInfinity : Math.Abs(given / exact - 1));
            }
            if (n <= 1000)
            {
                Check($"Kendall's score, {n} pairs: P(S >= s) at every s, against the count of the orderings", worst, 1e-12);
                Console.WriteLine($"ok    Kendall's score, {n} pairs: worst difference {worst:E2} from the count of the orderings");
            }
            else
            {
                Check($"Kendall's score, {n} pairs: P(S >= s) by the series, against the count of the orderings", worst, 1e-5);
                Check($"Kendall's score, {n} pairs: P from 0.001 to 0.05 by the series, as a proportion of itself", worstTail, 1e-3);
                Console.WriteLine($"      Kendall's score, {n} pairs, by the series: worst difference {worst:E2}, and {worstTail:P3} of a P from 0.001 to 0.05");
            }
        }
        Console.WriteLine($"the distribution of Kendall's score: {checks - before} checks, {failures - failed} failed");
    }
}
