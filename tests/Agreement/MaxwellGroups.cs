// Maxwell's test when the categories fall into groups between which the raters never disagree.  The differences between the row and the
// column totals then add up to nothing within each group, the matrix of their variances and covariances has no inverse even with a category
// left out, and the statistic is the quadratic form in its generalised inverse, on as many degrees of freedom as the matrix has rank: the
// number of categories less the number of groups.  The reference here takes the whole matrix, of all the categories, finds its latent roots
// and vectors by Jacobi's method, and leaves out the roots that are nothing.
using StatsDirect.Templates;

internal static partial class Program
{
    // the latent roots of a symmetric matrix and, in the columns of vectors, their vectors
    private static double[] LatentRoots(double[,] matrix, out double[,] vectors)
    {
        int n = matrix.GetLength(0);
        double[,] a = (double[,])matrix.Clone();
        vectors = new double[n, n];
        for (int i = 0; i < n; i++) vectors[i, i] = 1;
        for (int sweep = 0; sweep < 100; sweep++)
        {
            double off = 0;
            for (int p = 0; p < n; p++) for (int q = p + 1; q < n; q++) off += a[p, q] * a[p, q];
            if (off < 1e-30) break;
            for (int p = 0; p < n; p++)
                for (int q = p + 1; q < n; q++)
                {
                    if (Math.Abs(a[p, q]) < 1e-300) continue;
                    double theta = (a[q, q] - a[p, p]) / (2 * a[p, q]);
                    double t = Math.Sign(theta) / (Math.Abs(theta) + Math.Sqrt(theta * theta + 1));
                    if (theta == 0) t = 1;
                    double c = 1 / Math.Sqrt(t * t + 1), s = t * c;
                    for (int k = 0; k < n; k++)
                    {
                        double akp = a[k, p], akq = a[k, q];
                        a[k, p] = c * akp - s * akq; a[k, q] = s * akp + c * akq;
                    }
                    for (int k = 0; k < n; k++)
                    {
                        double apk = a[p, k], aqk = a[q, k];
                        a[p, k] = c * apk - s * aqk; a[q, k] = s * apk + c * aqk;
                    }
                    for (int k = 0; k < n; k++)
                    {
                        double vkp = vectors[k, p], vkq = vectors[k, q];
                        vectors[k, p] = c * vkp - s * vkq; vectors[k, q] = s * vkp + c * vkq;
                    }
                }
        }
        return Enumerable.Range(0, n).Select(i => a[i, i]).ToArray();
    }

    // Maxwell's chi-square as the quadratic form in the generalised inverse of the matrix of all the categories, and its rank
    private static double MaxwellReference(double[,] o, out int rank)
    {
        int k = o.GetLength(0);
        double[] d = new double[k];
        double[,] v = new double[k, k];
        for (int i = 0; i < k; i++)
        {
            double row = 0, column = 0;
            for (int j = 0; j < k; j++) { row += o[i, j]; column += o[j, i]; }
            d[i] = row - column;
            for (int j = 0; j < k; j++) v[i, j] = i == j ? row + column - 2 * o[i, i] : -(o[i, j] + o[j, i]);
        }
        double[] roots = LatentRoots(v, out double[,] vectors);
        double greatest = roots.Max(Math.Abs), chi = 0;
        rank = 0;
        for (int r = 0; r < k; r++)
        {
            if (roots[r] <= 1e-9 * Math.Max(1, greatest)) continue;
            rank++;
            double along = 0;
            for (int i = 0; i < k; i++) along += vectors[i, r] * d[i];
            chi += along * along / roots[r];
        }
        return chi;
    }

    private static void MaxwellGroups()
    {
        Console.WriteLine();
        Console.WriteLine("Maxwell's test when the raters disagree within groups of categories only");
        System.Random random = new(61);
        int tables = 0, inGroups = 0, right = 0, notGiven = 0, wrong = 0;
        List<string> problems = new();
        for (int set = 1; set <= 400; set++)
        {
            int k = 3 + set % 5, n = 8 + random.Next(set % 2 == 0 ? 25 : 120);
            double[,] o = new double[k, k];
            for (int s = 0; s < n; s++)
            {
                int i = random.Next(k);
                // a disagreement is with the next category, up or down
                int j = random.NextDouble() < 0.75 ? i : Math.Min(k - 1, Math.Max(0, i + (random.Next(2) == 0 ? 1 : -1)));
                o[i, j]++;
            }
            double reference = MaxwellReference(o, out int rank);
            tables++;
            if (rank < k - 1) inGroups++;
            try
            {
                ParameterBag report = Screen(o, 1, Weights(k, 1), 0.95);
                double given = report["x2"].AsDouble, df = Convert.ToDouble(report["df"].AsObject), p = report["pmaxwell"].AsDouble;
                if (rank == 0)
                {
                    if (given == M && p == M) right++; else { wrong++; problems.Add($"set {set}: a test given where the raters never disagree"); }
                    continue;
                }
                if (given == M) { notGiven++; if (problems.Count < 6) problems.Add($"set {set} ({k} categories, rank {rank}): no test given"); continue; }
                bool same = Relative(given, reference) <= 1e-8 && df == rank && Math.Abs(p - ChiSquareUpper(reference, rank)) <= 1e-7;
                if (same) right++; else { wrong++; if (problems.Count < 6) problems.Add($"set {set} ({k} categories, rank {rank}): {given:G8} on {df} df given, {reference:G8} on {rank} expected"); }
            }
            catch (Exception ex) { wrong++; problems.Add($"set {set}: " + Message(ex)); }
        }
        Say(right == tables, $"{tables} tables, {inGroups} of them with categories in groups: {right} with the chi-square, degrees of freedom and P of the generalised inverse, {notGiven} without a test, {wrong} with other figures" + (problems.Count > 0 ? "; " + string.Join("; ", problems.Take(6)) : ""), true);
    }
}
