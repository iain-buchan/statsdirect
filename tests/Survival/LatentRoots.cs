internal static partial class Program
{
    // the latent roots of a symmetric matrix and, in the columns of vectors, their vectors (Jacobi's method)
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
}
